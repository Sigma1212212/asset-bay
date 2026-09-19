using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;

namespace BundleMenu
{
    /// <summary>
    /// A handheld video tablet that pops out in front of you and plays videos from your Asset Bay library.
    ///
    /// The tablet's look comes from its own asset bundle (feed.tablet: a prefab with a child named "Screen").
    /// The bundle is downloaded once, checked against the SHA-256 in the signed feed, and given theme materials
    /// at runtime, so it renders correctly whatever shaders the game uses. If no bundle is published (or it
    /// fails the check), a clean built-in tablet is used instead.
    ///
    /// Controls live on the Videos page: play/pause, restart, volume, close.
    /// </summary>
    public sealed class VideoTablet : MonoBehaviour
    {
        public float Distance = 0.55f;          // metres in front of your view
        public float Width = 0.42f;             // screen width in metres

        public bool IsOpen => root != null && root.activeSelf;
        public bool IsPlaying => player != null && player.isPlaying;
        public string NowPlaying { get; private set; }
        public float Volume { get; private set; } = 0.7f;
        public string Source { get; private set; } = "built-in";

        public Func<Camera> ViewCamera;
        public Func<MenuTheme> Theme;
        public Action<string> Report;

        private GameObject root;
        private Renderer screen;
        private VideoPlayer player;
        private AudioSource audioSource;
        private RenderTexture texture;
        private Material screenMat, bodyMat;
        private float openT;
        private bool closing;
        private AssetBundle bundle;
        private string bundleHash;

        /// <summary>Use the tablet model from the feed's bundle (downloaded and verified), if there is one.</summary>
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
                    throw new InvalidOperationException("Tablet bundle failed its checksum - using the built-in tablet.");

                var request = AssetBundle.LoadFromMemoryAsync(data);
                await request.Await();
                if (request.assetBundle == null) throw new InvalidOperationException("Tablet bundle couldn't be opened.");
                var load = request.assetBundle.LoadAssetAsync<GameObject>(string.IsNullOrEmpty(info.prefab) ? "Tablet" : info.prefab);
                await load.Await();
                if (!(load.asset is GameObject prefab)) throw new InvalidOperationException("Tablet prefab not found in the bundle.");

                if (bundle != null) bundle.Unload(true);
                bundle = request.assetBundle;
                bundleHash = info.sha256;
                Rebuild(prefab);
                Source = "bundle";
            }
            catch (Exception e)
            {
                Report?.Invoke(e.Message);
            }
        }

        public void Play(string url, string title)
        {
            if (root == null) Rebuild(null);
            NowPlaying = title;
            player.url = url;
            player.isLooping = false;
            player.Prepare();
            player.prepareCompleted -= OnPrepared;
            player.prepareCompleted += OnPrepared;
            Open();
            Report?.Invoke($"Loading {title}...");
        }

        private void OnPrepared(VideoPlayer vp)
        {
            vp.Play();
            Report?.Invoke($"Playing {NowPlaying}");
        }

        public void TogglePause()
        {
            if (player == null) return;
            if (player.isPlaying) player.Pause(); else player.Play();
        }

        public void Restart()
        {
            if (player == null) return;
            player.time = 0;
            player.Play();
        }

        public void SetVolume(float v)
        {
            Volume = Mathf.Clamp01(v);
            if (audioSource != null) audioSource.volume = Volume;
        }

        public void Open()
        {
            if (root == null) Rebuild(null);
            closing = false;
            if (!root.activeSelf) { openT = 0f; root.SetActive(true); PlaceInFront(snap: true); }
        }

        public void Close()
        {
            if (!IsOpen) return;
            closing = true;
            if (player != null) player.Pause();
        }

        // ------------------------------------------------------------------ building

        private void Rebuild(GameObject prefab)
        {
            bool wasOpen = IsOpen;
            if (root != null) Destroy(root);

            root = prefab != null ? Instantiate(prefab) : BuildDefault();
            root.name = "[BundleMenu] Video Tablet";
            DontDestroyOnLoad(root);
            foreach (var c in root.GetComponentsInChildren<Collider>()) Destroy(c); // never blocks you or the gun

            var theme = Theme?.Invoke();
            if (bodyMat == null) bodyMat = ModVisuals.Unlit(throughWalls: false);
            bodyMat.color = theme != null ? theme.PanelBottom : new Color(0.08f, 0.09f, 0.15f);

            var screenT = FindDeep(root.transform, "Screen");
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                if (screenT == null || r.transform != screenT) r.sharedMaterial = bodyMat;

            if (texture == null) texture = new RenderTexture(1280, 720, 0) { name = "BundleMenu Tablet Video" };
            if (screenMat == null) screenMat = ModVisuals.Unlit(throughWalls: false);
            screenMat.mainTexture = texture;
            screenMat.color = Color.white;
            screen = screenT != null ? screenT.GetComponent<Renderer>() : null;
            if (screen != null) screen.sharedMaterial = screenMat;

            if (player == null)
            {
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
                player.errorReceived += (vp, message) => Report?.Invoke("Video error: " + message);
            }

            root.SetActive(wasOpen);
            if (wasOpen) PlaceInFront(snap: true);
        }

        /// <summary>The built-in tablet: rounded-looking slab, bezel, and a 16:9 screen. Faces -Z like UI.</summary>
        private GameObject BuildDefault()
        {
            var go = new GameObject("Tablet");
            float w = 1f, h = 9f / 16f;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localScale = new Vector3(w + 0.08f, h + 0.08f, 0.03f);
            body.transform.localPosition = new Vector3(0f, 0f, 0.02f);

            var scr = GameObject.CreatePrimitive(PrimitiveType.Quad);
            scr.name = "Screen";
            scr.transform.SetParent(go.transform, false);
            scr.transform.localScale = new Vector3(w, h, 1f);
            // A Unity quad's visible face points along -Z, toward the viewer here, so no rotation is needed.
            scr.transform.localPosition = new Vector3(0f, 0f, 0.004f);
            return go;
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = FindDeep(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // ------------------------------------------------------------------ per frame

        private void LateUpdate()
        {
            if (root == null || !root.activeSelf) return;
            PlaceInFront(snap: false);

            // Pop out on open, fold away on close.
            openT = Mathf.MoveTowards(openT, closing ? 0f : 1f, Time.unscaledDeltaTime / (closing ? 0.18f : 0.3f));
            float e = closing ? openT * openT : Ease.OutBack(openT, 1.8f);
            root.transform.localScale = Vector3.one * Width * Mathf.Max(0.001f, e);
            if (closing && openT <= 0f) root.SetActive(false);
        }

        private void PlaceInFront(bool snap)
        {
            var cam = ViewCamera?.Invoke();
            if (cam == null) return;
            var t = cam.transform;
            var target = t.position + t.forward * Distance + t.up * -0.06f;
            var rot = Quaternion.LookRotation(target - t.position, t.up);
            float k = snap ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f); // lazy follow, no jitter
            root.transform.SetPositionAndRotation(Vector3.Lerp(root.transform.position, target, k),
                                                  Quaternion.Slerp(root.transform.rotation, rot, k));
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root);
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (screenMat != null) Destroy(screenMat);
            if (bodyMat != null) Destroy(bodyMat);
            if (bundle != null) bundle.Unload(true);
        }
    }
}
