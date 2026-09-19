using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;

namespace BundleMenu
{
    /// <summary>
    /// A video tablet that pops out in front of you, plays your uploaded videos, and can be grabbed and thrown.
    ///
    /// Look: one of the TabletConfig styles (Classic, Slim, Rugged, Retro, plus any the content bundle adds),
    /// built by TabletFactory and scaled by the Size setting.
    /// States: Floating (in front of you) → InHand (VR grip near it) → Thrown (real physics until you
    /// grab it again or recall it). Desktop: T throws it where you look, T again calls it back.
    /// It exists only on your screen - it can't hit or affect other players.
    /// </summary>
    public sealed class VideoTablet : MonoBehaviour
    {
        public enum State { Floating, InHand, Thrown }

        public float SizeMultiplier = 1.5f;
        public static readonly float[] Sizes = { 1f, 1.5f, 2f, 3f };

        public bool IsOpen => root != null && root.activeSelf;
        public bool IsPlaying => player != null && player.isPlaying;
        public bool IsThrown => state == State.Thrown;
        public string NowPlaying { get; private set; }
        public float Volume { get; private set; } = 0.7f;
        public string Source { get; private set; } = "built-in";
        public TabletConfig Style => styles[styleIndex];
        /// <summary>The loaded content pack (props, styles, themes), or null.</summary>
        public AssetBundle ContentBundle => bundle;
        public IReadOnlyList<TabletConfig> Styles => styles;

        public Func<Camera> ViewCamera;
        public Func<MenuTheme> Theme;
        public Func<Transform> LeftHand, RightHand;
        public Action<string> Report;
        /// <summary>Called with every TextAsset in a newly loaded content pack (the controller picks up themes).</summary>
        public Action<TextAsset[]> PackLoaded;

        private readonly List<TabletConfig> styles = new List<TabletConfig>(TabletConfig.BuiltIn());
        private int styleIndex;
        private State state;
        private GameObject root;
        private VideoPlayer player;
        private AudioSource audioSource;
        private RenderTexture texture;
        private readonly List<Material> materials = new List<Material>();
        private Material screenMat;
        private float openT;
        private bool closing;
        private AssetBundle bundle;
        private string bundleHash;

        // grab / throw
        private Transform holdingHand;
        private Vector3 holdOffsetPos;
        private Quaternion holdOffsetRot;
        private readonly Queue<(Vector3 pos, Quaternion rot, float t)> handHistory = new Queue<(Vector3, Quaternion, float)>();
        private bool leftGripPrev, rightGripPrev;
        private Rigidbody rb;

        // ================================================================== style / size

        public void CycleStyle(int dir)
        {
            styleIndex = (styleIndex + dir + styles.Count) % styles.Count;
            Rebuild();
            Report?.Invoke($"Tablet style: {Style.name}");
        }

        public void CycleSize(int dir)
        {
            int i = Array.FindIndex(Sizes, s => Mathf.Approximately(s, SizeMultiplier));
            SizeMultiplier = Sizes[((i < 0 ? 1 : i) + dir + Sizes.Length) % Sizes.Length];
            if (rb != null) FitCollider();
        }

        /// <summary>Adds tablet styles from the content bundle (TextAssets named "tablet-*.json").</summary>
        public async Task UseBundleAsync(FeedClient feed)
        {
            var info = feed?.Current?.tablet;
            if (info == null || string.IsNullOrEmpty(info.path) || info.sha256 == bundleHash) return;
            try
            {
                byte[] data = await FeedClient.GetBytes(feed.MediaUrl(info.path));
                string hash;
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(hash, info.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Content bundle failed its checksum - using the built-in tablets.");

                var request = AssetBundle.LoadFromMemoryAsync(data);
                await request.Await();
                if (request.assetBundle == null) throw new InvalidOperationException("Content bundle couldn't be opened.");
                if (bundle != null) bundle.Unload(true);
                bundle = request.assetBundle;
                bundleHash = info.sha256;

                int added = 0;
                var texts = bundle.LoadAllAssets<TextAsset>();
                PackLoaded?.Invoke(texts);
                foreach (var asset in texts)
                {
                    if (!asset.name.StartsWith("tablet-", StringComparison.OrdinalIgnoreCase)) continue;
                    var config = TabletConfig.Parse(asset.text);
                    styles.RemoveAll(s => s.name == config.name);
                    styles.Add(config);
                    added++;
                }
                Source = "bundle";
                Report?.Invoke($"Content pack loaded: {added} tablet styles");
            }
            catch (Exception e)
            {
                Report?.Invoke(e.Message);
            }
        }

        // ================================================================== playback

        // ---- for testing (safe-mode tablet tab): what the player is doing right now
        public double PlayTime => player != null ? player.time : 0;
        public double PlayLength => player != null && player.frameRate > 0 ? player.frameCount / player.frameRate : 0;
        public bool IsPreparing => player != null && !player.isPrepared && !string.IsNullOrEmpty(player.url);
        public string LastError { get; private set; }
        public bool Loop { get; set; }
        public Vector2Int VideoSize => player != null && player.texture != null ? new Vector2Int(player.texture.width, player.texture.height) : Vector2Int.zero;

        public void Seek(double seconds)
        {
            if (player == null || !player.canSetTime) return;
            player.time = Math.Max(0, Math.Min(seconds, Math.Max(0, PlayLength - 0.2)));
        }

        /// <summary>Play a web URL, or a video file on this PC (full path).</summary>
        public void PlayAny(string urlOrPath, string title = null)
        {
            urlOrPath = (urlOrPath ?? "").Trim().Trim('"');
            if (urlOrPath.Length == 0) return;
            bool file = System.IO.File.Exists(urlOrPath);
            string url = file ? new Uri(System.IO.Path.GetFullPath(urlOrPath)).AbsoluteUri : urlOrPath;
            Play(url, title ?? (file ? System.IO.Path.GetFileName(urlOrPath) : urlOrPath));
        }

        public void Play(string url, string title)
        {
            EnsurePlayer();
            NowPlaying = title;
            LastError = null;
            player.url = url;
            player.isLooping = Loop;
            player.prepareCompleted -= OnPrepared;
            player.prepareCompleted += OnPrepared;
            player.Prepare();
            Open();
            Report?.Invoke($"Loading {title}...");
        }

        private void OnPrepared(VideoPlayer vp)
        {
            vp.Play();
            Report?.Invoke($"Playing {NowPlaying}");
        }

        public void TogglePause() { if (player == null) return; if (player.isPlaying) player.Pause(); else player.Play(); }
        public void SetLoop(bool on) { Loop = on; if (player != null) player.isLooping = on; }
        public void Restart() { if (player == null) return; player.time = 0; player.Play(); }

        public void SetVolume(float v)
        {
            Volume = Mathf.Clamp01(v);
            if (audioSource != null) audioSource.volume = Volume;
        }

        // ================================================================== open / close / throw

        public void Open()
        {
            if (root == null) Rebuild();
            closing = false;
            if (!root.activeSelf)
            {
                openT = 0f;
                SetState(State.Floating);
                root.SetActive(true);
                PlaceInFront(snap: true);
            }
        }

        public void Close()
        {
            if (!IsOpen) return;
            closing = true;
            SetState(State.Floating);
            if (player != null) player.Pause();
        }

        /// <summary>Throw it where you're looking (desktop, or the menu button).</summary>
        public void Throw()
        {
            var cam = ViewCamera?.Invoke();
            if (!IsOpen || cam == null) return;
            SetState(State.Thrown);
            rb.velocity = cam.transform.forward * 8f + Vector3.up * 2.5f;
            rb.angularVelocity = new Vector3(UnityEngine.Random.Range(-4f, 4f), UnityEngine.Random.Range(-6f, 6f), UnityEngine.Random.Range(-3f, 3f));
            Report?.Invoke("Tablet thrown - press T or Recall to bring it back");
        }

        /// <summary>Bring it back in front of you.</summary>
        public void Recall()
        {
            if (!IsOpen) { Open(); return; }
            SetState(State.Floating);
        }

        private void SetState(State next)
        {
            state = next;
            if (next == State.Thrown)
            {
                if (rb == null)
                {
                    rb = root.AddComponent<Rigidbody>();
                    rb.mass = 0.6f;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    FitCollider();
                }
                rb.isKinematic = false;
            }
            else
            {
                if (rb != null)
                {
                    Destroy(rb);
                    var col = root.GetComponent<BoxCollider>();
                    if (col != null) Destroy(col);
                    rb = null;
                }
                if (next != State.InHand) holdingHand = null;
            }
        }

        private void FitCollider()
        {
            if (!root.TryGetComponent(out BoxCollider col)) col = root.AddComponent<BoxCollider>();
            var c = Style;
            col.size = new Vector3(c.width + c.bezel * 2f, c.width / c.aspect + c.bezel * 2f, c.thickness);
            col.center = new Vector3(0f, 0f, c.thickness * 0.5f);
        }

        // ================================================================== building

        private void Rebuild()
        {
            bool wasOpen = IsOpen;
            var keepPos = root != null ? root.transform.position : Vector3.zero;
            var keepRot = root != null ? root.transform.rotation : Quaternion.identity;
            if (root != null) Destroy(root);
            foreach (var m in materials) if (m != null) Destroy(m);
            materials.Clear();
            rb = null;

            var theme = Theme?.Invoke();
            var c = Style;
            EnsurePlayer();
            root = TabletFactory.Build(c, new TabletFactory.Materials
            {
                Body = Lit(TabletColors.Resolve(c.body, theme, new Color(0.1f, 0.11f, 0.18f))),
                Bezel = Lit(TabletColors.Resolve(c.bezelColor, theme, Color.black)),
                Accent = Glow(TabletColors.Resolve(c.accent, theme, Color.cyan)),
                Trim = Lit(TabletColors.Resolve(c.trim, theme, Color.white)),
                Screen = screenMat,
            });
            root.name = "[BundleMenu] Video Tablet";
            DontDestroyOnLoad(root);
            root.transform.SetPositionAndRotation(keepPos, keepRot);
            state = State.Floating;
            root.SetActive(wasOpen);
        }

        /// <summary>A copy of the game's own default lit material, so the tablet is shaded like the world.</summary>
        private Material Lit(Color color)
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mat = new Material(probe.GetComponent<Renderer>().sharedMaterial);
            DestroyImmediate(probe);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            materials.Add(mat);
            return mat;
        }

        private Material Glow(Color color)
        {
            var mat = ModVisuals.Unlit(throughWalls: false); // unlit = always bright, like a light strip
            mat.color = color;
            materials.Add(mat);
            return mat;
        }

        private void EnsurePlayer()
        {
            if (player != null) return;
            texture = new RenderTexture(1280, 720, 0) { name = "BundleMenu Tablet Video" };
            screenMat = ModVisuals.Unlit(throughWalls: false);
            screenMat.mainTexture = texture;
            screenMat.color = Color.white;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.volume = Volume;
            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = texture;
            player.source = VideoSource.Url;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.SetTargetAudioSource(0, audioSource);
            player.errorReceived += (vp, message) => { LastError = message; Report?.Invoke("Video error: " + message); };
        }

        // ================================================================== per frame

        private void Update()
        {
            if (!IsOpen || closing) return;
            if (!MenuInput.VRActive && MenuInput.KeyDown(KeyCode.T)) { if (IsThrown) Recall(); else Throw(); }
            if (MenuInput.VRActive) HandleGrab();
        }

        private void HandleGrab()
        {
            bool left = MenuInput.XRHeld(false, false), right = MenuInput.XRHeld(true, false);
            TryGrab(LeftHand?.Invoke(), left && !leftGripPrev);
            TryGrab(RightHand?.Invoke(), right && !rightGripPrev);

            if (state == State.InHand && holdingHand != null)
            {
                bool stillHeld = holdingHand == LeftHand?.Invoke() ? left : right;
                if (!stillHeld) Release();
            }
            leftGripPrev = left;
            rightGripPrev = right;
        }

        private void TryGrab(Transform hand, bool pressed)
        {
            if (!pressed || hand == null || state == State.InHand) return;
            float reach = 0.18f * SizeMultiplier + 0.1f;
            if (Vector3.Distance(hand.position, root.transform.position) > reach) return;

            SetState(State.InHand);
            holdingHand = hand;
            holdOffsetPos = Quaternion.Inverse(hand.rotation) * (root.transform.position - hand.position);
            holdOffsetRot = Quaternion.Inverse(hand.rotation) * root.transform.rotation;
            handHistory.Clear();
        }

        private void Release()
        {
            // Throw with the hand's recent speed and spin (averaged over the last ~0.1 s so it feels natural).
            Vector3 velocity = Vector3.zero, angular = Vector3.zero;
            if (handHistory.Count >= 2)
            {
                var samples = handHistory.ToArray();
                var first = samples[0];
                var last = samples[samples.Length - 1];
                float dt = Mathf.Max(0.001f, last.t - first.t);
                velocity = (last.pos - first.pos) / dt;
                (last.rot * Quaternion.Inverse(first.rot)).ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f) angle -= 360f;
                if (!float.IsNaN(axis.x)) angular = axis * (angle * Mathf.Deg2Rad / dt);
            }

            SetState(State.Thrown);
            rb.velocity = velocity * 1.25f;
            rb.angularVelocity = angular;
        }

        private void LateUpdate()
        {
            if (!IsOpen) return;

            switch (state)
            {
                case State.Floating:
                    PlaceInFront(snap: false);
                    break;
                case State.InHand when holdingHand != null:
                    root.transform.SetPositionAndRotation(holdingHand.position + holdingHand.rotation * holdOffsetPos,
                        holdingHand.rotation * holdOffsetRot);
                    handHistory.Enqueue((root.transform.position, root.transform.rotation, Time.unscaledTime));
                    while (handHistory.Count > 6) handHistory.Dequeue();
                    break;
            }

            // Open / close animation, scaled by the Size setting.
            openT = Mathf.MoveTowards(openT, closing ? 0f : 1f, Time.unscaledDeltaTime / (closing ? 0.18f : 0.32f));
            float e = closing ? openT * openT : Style.openAnimation == "fade" ? openT : Ease.OutBack(openT, 1.8f);
            root.transform.localScale = Vector3.one * SizeMultiplier * Mathf.Max(0.001f, e);
            if (closing && openT <= 0f) root.SetActive(false);
        }

        private void PlaceInFront(bool snap)
        {
            var cam = ViewCamera?.Invoke();
            if (cam == null) return;
            var c = Style;
            var t = cam.transform;
            // Further away as it gets bigger, so the whole tablet stays in view.
            float distance = c.distance * Mathf.Lerp(1f, SizeMultiplier, 0.8f);
            float bob = Mathf.Sin(Time.unscaledTime * 1.6f) * c.bob * SizeMultiplier;
            var target = t.position + t.forward * distance + t.up * (-0.06f * SizeMultiplier + bob);
            var rot = Quaternion.LookRotation(target - t.position, t.up) * Quaternion.Euler(-c.tiltDegrees, 0f, 0f);

            if (c.openAnimation == "flip" && openT < 1f) rot *= Quaternion.Euler(0f, (1f - Ease.OutCubic(openT)) * 180f, 0f);
            if (c.openAnimation == "slide" && openT < 1f) target -= t.up * (1f - Ease.OutCubic(openT)) * 0.4f;

            float k = snap || c.follow == "locked" ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * c.followSpeed);
            if (c.follow == "fixed" && !snap) return;
            root.transform.SetPositionAndRotation(Vector3.Lerp(root.transform.position, target, k),
                                                  Quaternion.Slerp(root.transform.rotation, rot, k));
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root);
            foreach (var m in materials) if (m != null) Destroy(m);
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (screenMat != null) Destroy(screenMat);
            if (bundle != null) bundle.Unload(true);
        }
    }
}
