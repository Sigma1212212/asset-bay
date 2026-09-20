using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>Shared bits for mods that draw things.</summary>
    internal static class ModVisuals
    {
        /// <summary>An unlit material; `throughWalls` makes it ignore depth so it shows behind geometry.</summary>
        public static Material Unlit(bool throughWalls)
        {
            var shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (throughWalls)
            {
                mat.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                mat.renderQueue = 4000;
            }
            return mat;
        }

        public static LineRenderer Line(string name, Material mat, int points, bool loop = false)
        {
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.positionCount = points;
            lr.loop = loop;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.numCapVertices = 2;
            return lr;
        }

        public static Color Accent(ModContext ctx) => ctx.Theme?.Invoke()?.Accent ?? new Color(0.25f, 0.9f, 1f);
        public static Color Accent2(ModContext ctx) => ctx.Theme?.Invoke()?.Accent2 ?? new Color(0.55f, 0.35f, 1f);
    }

    // ----------------------------------------------------------------------------------------------- movement

    /// <summary>
    /// Pass through walls, floors and everything else. Your hands and body stop colliding, gravity is cancelled
    /// so you don't fall out of the world, and you drift to a stop. Pair it with Fly to move.
    /// Everything is restored exactly when it's switched off.
    /// </summary>
    public sealed class NoclipMod : Mod
    {
        public override string Name => "Noclip";
        public override string Hint => "use with Fly";
        public override KeyCode DefaultKey => KeyCode.N;
        public override VRInput DefaultButton => VRInput.None;

        private int? savedLayers;
        private bool bodyWasOn, headWasOn;

        public override void OnEnable(ModContext ctx)
        {
            savedLayers = ctx.Player.LocomotionLayers;
            var body = ctx.Player.BodyCollider;
            var head = ctx.Player.HeadCollider;
            bodyWasOn = body != null && body.enabled;
            headWasOn = head != null && head.enabled;
            Apply(ctx);
        }

        public override void Tick(ModContext ctx) => Apply(ctx); // the game re-enables things on area changes

        private static void Apply(ModContext ctx)
        {
            ctx.Player.LocomotionLayers = 0;
            var body = ctx.Player.BodyCollider;
            var head = ctx.Player.HeadCollider;
            if (body != null) body.enabled = false;
            if (head != null) head.enabled = false;
        }

        public override void FixedTick(ModContext ctx)
        {
            var rb = ctx.Player.Body;
            if (rb == null) return;
            rb.AddForce(-Physics.gravity, ForceMode.Acceleration);
            rb.velocity *= 0.92f; // drift to a stop; Fly overrides this while held
        }

        public override void OnDisable(ModContext ctx)
        {
            if (savedLayers != null) ctx.Player.LocomotionLayers = savedLayers;
            var body = ctx.Player.BodyCollider;
            var head = ctx.Player.HeadCollider;
            if (body != null) body.enabled = bodyWasOn;
            if (head != null) head.enabled = headWasOn;
        }
    }

    /// <summary>Float instead of fall: gravity is cancelled and vertical speed eases toward zero.</summary>
    public sealed class HoverMod : Mod
    {
        public override string Name => "Hover";
        public override string Hint => "float";
        public override KeyCode DefaultKey => KeyCode.J;

        public override void FixedTick(ModContext ctx)
        {
            var rb = ctx.Player.Body;
            if (rb == null) return;
            rb.AddForce(-Physics.gravity, ForceMode.Acceleration);
            var v = rb.velocity;
            v.y *= 0.9f;
            rb.velocity = v;
        }
    }

    /// <summary>A burst of speed where you look. Desktop: V. VR: the right B button. 0.8 s cooldown.</summary>
    public sealed class DashMod : Mod
    {
        public override string Name => "Dash";
        public override string Hint => "tap " + ControlText;
        public override ModTrigger Trigger => ModTrigger.Tap;
        public override KeyCode DefaultKey => KeyCode.V;
        public override VRInput DefaultButton => VRInput.RightSecondary;

        public float Power = 16f;
        private float cooldownUntil;

        private ModSetting[] settings;
        public override IEnumerable<ModSetting> Settings => settings ?? (settings = new[]
        {
            ModSetting.Choice("Dash power", new[] { 10f, 16f, 24f, 34f }, () => Power, v => Power = v, "{0:0}"),
        });

        public override void Tick(ModContext ctx)
        {
            if (!Pressed || Time.unscaledTime < cooldownUntil) return;

            var rb = ctx.Player.Body;
            var cam = ctx.Rig?.Camera;
            if (rb == null || cam == null) return;
            rb.velocity = cam.transform.forward * Power + Vector3.up * 1.5f;
            cooldownUntil = Time.unscaledTime + 0.8f;
        }
    }

    /// <summary>
    /// Rewind: the last 5 seconds of your movement are recorded; hold R (desktop) or the left trigger (VR) to run
    /// back along that path, shown as a fading line. Let go to carry on from there.
    /// </summary>
    public sealed class RewindMod : Mod
    {
        public override string Name => "Rewind";
        public override string Hint => "hold " + ControlText;
        public override ModTrigger Trigger => ModTrigger.Held;
        public override KeyCode DefaultKey => KeyCode.R;
        public override VRInput DefaultButton => VRInput.LeftTrigger;

        private const int Capacity = 250;            // 5 s at 50 Hz
        private readonly Vector3[] path = new Vector3[Capacity];
        private int head, count;
        private LineRenderer line;
        private Material mat;
        private int step;

        public override void OnEnable(ModContext ctx)
        {
            head = count = 0;
            mat = ModVisuals.Unlit(throughWalls: false);
            line = ModVisuals.Line("[BundleMenu] Rewind path", mat, 0);
            line.widthMultiplier = 0.02f;
        }

        public override void FixedTick(ModContext ctx)
        {
            var body = ctx.Player.BodyCollider;
            if (body == null) return;
            bool held = Using;

            if (!held)
            {
                path[head] = body.transform.position;
                head = (head + 1) % Capacity;
                if (count < Capacity) count++;
                line.positionCount = 0;
                return;
            }

            if (count <= 1) return;
            // Step back three samples per physics tick: rewinds at 3x the speed it was recorded.
            for (int i = 0; i < 3 && count > 1; i++)
            {
                head = (head - 1 + Capacity) % Capacity;
                count--;
            }
            if (++step % 2 == 0)
                ctx.Player.TeleportTo(path[(head - 1 + Capacity) % Capacity], body.transform.rotation);
            var rb = ctx.Player.Body;
            if (rb != null) rb.velocity = Vector3.zero;

            // Show what's left of the path.
            int shown = Mathf.Min(count, 120);
            line.positionCount = shown;
            for (int i = 0; i < shown; i++) line.SetPosition(i, path[(head - 1 - i + Capacity * 2) % Capacity]);
            var a = ModVisuals.Accent(ctx);
            line.startColor = new Color(a.r, a.g, a.b, 0.9f);
            line.endColor = new Color(a.r, a.g, a.b, 0f);
        }

        public override void OnDisable(ModContext ctx)
        {
            if (line != null) Object.Destroy(line.gameObject);
            if (mat != null) Object.Destroy(mat);
        }
    }

    // ----------------------------------------------------------------------------------------------- visual

    /// <summary>
    /// ESP: a box, name and distance for every other player, visible through walls, plus an optional tracer
    /// line from you. Local drawing only - nothing is sent anywhere. Rescans the room four times a second.
    /// </summary>
    public sealed class EspMod : Mod
    {
        public override string Name => "ESP";
        public override KeyCode DefaultKey => KeyCode.X;
        public override string Hint => $"{tracked.Count} players";

        public bool Tracers = true;
        public bool Names = true;

        private readonly List<GorillaTagPlayer.OtherPlayer> tracked = new List<GorillaTagPlayer.OtherPlayer>();
        private readonly List<Marker> markers = new List<Marker>();
        private Material mat;
        private float nextScan;

        public override IEnumerable<ModSetting> Settings => new[]
        {
            new ModSetting { Name = "Tracers", Value = () => Tracers ? "on" : "off", Cycle = _ => Tracers = !Tracers },
            new ModSetting { Name = "Names", Value = () => Names ? "on" : "off", Cycle = _ => Names = !Names },
        };

        private sealed class Marker
        {
            public LineRenderer Box, Tracer;
            public TextMeshPro Label;
        }

        public override void OnEnable(ModContext ctx)
        {
            mat = ModVisuals.Unlit(throughWalls: true);
            nextScan = 0f;
        }

        public override void Tick(ModContext ctx)
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.25f;
                tracked.Clear();
                tracked.AddRange(ctx.Player.OtherPlayers());
            }

            var cam = ctx.Rig?.Camera;
            if (cam == null) return;
            var camT = cam.transform;
            var from = camT.TransformPoint(new Vector3(0f, -0.25f, 0.4f));
            Color a = ModVisuals.Accent(ctx), b = ModVisuals.Accent2(ctx);

            while (markers.Count < tracked.Count) markers.Add(Create());
            for (int i = 0; i < markers.Count; i++)
            {
                var m = markers[i];
                bool on = i < tracked.Count && tracked[i].Body != null;
                m.Box.gameObject.SetActive(on);
                m.Tracer.gameObject.SetActive(on && Tracers);
                m.Label.gameObject.SetActive(on && Names);
                if (!on) continue;

                var p = tracked[i];
                float s = Mathf.Max(0.3f, p.Scale);
                var centre = p.Body.position + Vector3.up * 0.05f * s;
                float dist = Vector3.Distance(camT.position, centre);
                var col = Color.Lerp(a, b, Mathf.InverseLerp(3f, 40f, dist)); // near = accent, far = accent2

                // Camera-facing box around the body.
                Vector3 right = camT.right * 0.32f * s, up = Vector3.up * 0.45f * s;
                m.Box.SetPosition(0, centre - right - up * 0.9f);
                m.Box.SetPosition(1, centre + right - up * 0.9f);
                m.Box.SetPosition(2, centre + right + up);
                m.Box.SetPosition(3, centre - right + up);
                m.Box.widthMultiplier = Mathf.Clamp(dist * 0.004f, 0.006f, 0.08f);
                m.Box.startColor = m.Box.endColor = col;

                m.Tracer.SetPosition(0, from);
                m.Tracer.SetPosition(1, centre);
                m.Tracer.widthMultiplier = 0.004f;
                m.Tracer.startColor = new Color(col.r, col.g, col.b, 0.15f);
                m.Tracer.endColor = new Color(col.r, col.g, col.b, 0.8f);

                m.Label.text = $"{p.Name}  <size=70%>{dist:0} m</size>";
                m.Label.color = col;
                m.Label.transform.position = centre + up * 1.25f;
                m.Label.transform.rotation = Quaternion.LookRotation(m.Label.transform.position - camT.position);
                m.Label.transform.localScale = Vector3.one * Mathf.Clamp(dist * 0.03f, 0.08f, 1.2f);
            }
        }

        private Marker Create()
        {
            var m = new Marker
            {
                Box = ModVisuals.Line("[BundleMenu] ESP box", mat, 4, loop: true),
                Tracer = ModVisuals.Line("[BundleMenu] ESP tracer", mat, 2),
            };
            var labelGo = new GameObject("[BundleMenu] ESP label");
            Object.DontDestroyOnLoad(labelGo);
            m.Label = labelGo.AddComponent<TextMeshPro>();
            if (UIFactory.DefaultFont != null) m.Label.font = UIFactory.DefaultFont;
            m.Label.fontSize = 3f;
            m.Label.alignment = TextAlignmentOptions.Center;
            m.Label.fontStyle = FontStyles.Bold;
            m.Label.enableWordWrapping = false;
            // Draw on top of the world like the boxes.
            m.Label.fontMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            m.Label.fontMaterial.renderQueue = 4000;
            return m;
        }

        public override void OnDisable(ModContext ctx)
        {
            foreach (var m in markers)
            {
                if (m.Box != null) Object.Destroy(m.Box.gameObject);
                if (m.Tracer != null) Object.Destroy(m.Tracer.gameObject);
                if (m.Label != null) Object.Destroy(m.Label.gameObject);
            }
            markers.Clear();
            tracked.Clear();
            if (mat != null) Object.Destroy(mat);
        }
    }

    /// <summary>Glowing trails behind both hands in the theme colours. Only you see them.</summary>
    public sealed class HandTrailsMod : Mod
    {
        public override string Name => "Hand Trails";
        public override KeyCode DefaultKey => KeyCode.T;
        public override string Hint => "theme colours";

        private TrailRenderer left, right;
        private Material mat;

        public override void OnEnable(ModContext ctx)
        {
            mat = ModVisuals.Unlit(throughWalls: false);
            left = Attach(ctx.Rig?.WristAnchor, ctx);
            right = Attach(ctx.Rig?.RightHandTransform, ctx);
        }

        private TrailRenderer Attach(Transform hand, ModContext ctx)
        {
            if (hand == null) return null;
            var go = new GameObject("[BundleMenu] Hand trail");
            go.transform.SetParent(hand, false);
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = mat;
            tr.time = 0.35f;
            tr.minVertexDistance = 0.01f;
            tr.widthCurve = AnimationCurve.EaseInOut(0f, 0.035f, 1f, 0f);
            Color a = ModVisuals.Accent(ctx), b = ModVisuals.Accent2(ctx);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.shadowCastingMode = ShadowCastingMode.Off;
            return tr;
        }

        public override void OnDisable(ModContext ctx)
        {
            if (left != null) Object.Destroy(left.gameObject);
            if (right != null) Object.Destroy(right.gameObject);
            if (mat != null) Object.Destroy(mat);
        }
    }

    /// <summary>Your speed and this session's top speed, in a small readout at the top of the screen.</summary>
    public sealed class SpeedometerMod : Mod
    {
        public override string Name => "Speedometer";
        public override KeyCode DefaultKey => KeyCode.M;
        public override string Hint => $"top {top:0.0} m/s";

        private TextMeshProUGUI text;
        private float top, shown;

        public override void OnEnable(ModContext ctx)
        {
            if (ctx.ScreenCanvas == null) return;
            var theme = ctx.Theme?.Invoke();
            text = UIFactory.Text(ctx.ScreenCanvas.transform, "Speedometer", theme, 22, TextAlignmentOptions.Top,
                theme != null ? theme.Text : Color.white, FontStyles.Bold);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            text.rectTransform.pivot = new Vector2(0.5f, 1f);
            text.rectTransform.anchoredPosition = new Vector2(0f, -18f);
            text.rectTransform.sizeDelta = new Vector2(400f, 40f);
        }

        public override void Tick(ModContext ctx)
        {
            var rb = ctx.Player.Body;
            if (rb == null || text == null) return;
            float speed = rb.velocity.magnitude;
            top = Mathf.Max(top, speed);
            shown = Mathf.Lerp(shown, speed, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f));
            var theme = ctx.Theme?.Invoke();
            string accent = theme != null ? ColorUtility.ToHtmlStringRGB(theme.Accent) : "3BE8FF";
            text.text = $"<color=#{accent}>{shown:0.0}</color> m/s   <size=70%>top {top:0.0}</size>";
        }

        public override void OnDisable(ModContext ctx)
        {
            if (text != null) Object.Destroy(text.gameObject);
        }
    }

    /// <summary>
    /// Desktop freecam: a separate camera you fly around (WASD, Q/E down/up, hold right mouse to look, Shift
    /// for speed) while your player stays where it is. Great for screenshots. Not available in VR.
    /// </summary>
    public sealed class FreecamMod : Mod
    {
        public override string Name => "Freecam";
        public override string Hint => "WASD, Q/E, right mouse looks";
        public override KeyCode DefaultKey => KeyCode.P;

        private Camera cam;
        private float yaw, pitch;
        private Vector2 lastMouse;

        public override void OnEnable(ModContext ctx)
        {
            if (MenuInput.VRActive) { ctx.Toast?.Invoke("Freecam is desktop only."); return; }
            var main = ctx.Rig?.Camera;
            var go = new GameObject("[BundleMenu] Freecam");
            Object.DontDestroyOnLoad(go);
            cam = go.AddComponent<Camera>();
            if (main != null)
            {
                cam.CopyFrom(main);
                go.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
                cam.depth = main.depth + 10; // draws over the game camera
            }
            var e = go.transform.eulerAngles;
            yaw = e.y;
            pitch = e.x > 180f ? e.x - 360f : e.x;
            lastMouse = MenuInput.MousePosition;
        }

        public override void Tick(ModContext ctx)
        {
            if (cam == null) return;
            var mouse = MenuInput.MousePosition;
            if (MenuInput.MouseHeld(1))
            {
                var d = mouse - lastMouse;
                yaw += d.x * 0.15f;
                pitch = Mathf.Clamp(pitch - d.y * 0.15f, -89f, 89f);
            }
            lastMouse = mouse;
            cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            var move = Vector3.zero;
            if (MenuInput.KeyHeld(KeyCode.W)) move += Vector3.forward;
            if (MenuInput.KeyHeld(KeyCode.S)) move += Vector3.back;
            if (MenuInput.KeyHeld(KeyCode.D)) move += Vector3.right;
            if (MenuInput.KeyHeld(KeyCode.A)) move += Vector3.left;
            if (MenuInput.KeyHeld(KeyCode.E)) move += Vector3.up;
            if (MenuInput.KeyHeld(KeyCode.Q)) move += Vector3.down;
            float speed = MenuInput.KeyHeld(KeyCode.LeftShift) ? 14f : 4f;
            cam.transform.Translate(move * speed * Time.unscaledDeltaTime, Space.Self);
        }

        public override void OnDisable(ModContext ctx)
        {
            if (cam != null) Object.Destroy(cam.gameObject);
            cam = null;
        }
    }
}
