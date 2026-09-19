using System;
using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>What the gun is pointing at this frame.</summary>
    public struct GunHit
    {
        public Ray Ray;
        public bool HasHit;
        public Vector3 Point;          // hit point, or a point far along the ray when nothing was hit
        public Vector3 Normal;
        public float Distance;
        public Collider Collider;      // null when nothing was hit
    }

    /// <summary>
    /// A gun mode: what happens when you fire, plus optional per-frame feedback.
    /// Add your own by implementing this and calling <see cref="GunLib.Register"/>.
    /// </summary>
    public interface IGunMode
    {
        string Name { get; }

        /// <summary>Colour hint for the reticle while aiming at this hit (null = theme accent).</summary>
        Color? Tint(GunHit hit);

        /// <summary>Short text shown next to the reticle state in the menu, e.g. "3.2 m".</summary>
        string Describe(GunHit hit);

        /// <summary>Called on the trigger's rising edge. Return a message for the menu's status line.</summary>
        string Fire(GunHit hit);
    }

    /// <summary>
    /// Aim-and-fire utility (a "gun lib"). Desktop: hold the aim button (right mouse by default) and
    /// left-click to fire. VR: hold the right grip and pull the right trigger.
    ///
    /// It raycasts the world (ignoring the player's own body and trigger volumes), draws a themed laser
    /// and reticle, and hands the hit to the selected <see cref="IGunMode"/>. It only ever affects things
    /// on your own machine.
    /// </summary>
    public sealed class GunLib : MonoBehaviour
    {
        public bool GunEnabled;
        public KeyCode AimKey = KeyCode.Mouse1;
        public float MaxDistance = 60f;
        public LayerMask Layers = ~0;

        /// <summary>Supplied by the owner.</summary>
        public Func<IRigProvider> Rig;
        public Func<MenuTheme> Theme;
        public Func<bool> Blocked;              // e.g. cursor is over the menu
        public Action<string> Report;           // status-line messages

        public bool Aiming { get; private set; }
        public GunHit Current { get; private set; }
        public IGunMode Mode => modes.Count > 0 ? modes[modeIndex] : null;
        public IReadOnlyList<IGunMode> Modes => modes;

        private readonly List<IGunMode> modes = new List<IGunMode>();
        private int modeIndex;
        private bool prevFire;
        private LineRenderer line;
        private Transform reticle, ring;
        private Material lineMat, reticleMat;
        private float fireFlash;
        private readonly RaycastHit[] hits = new RaycastHit[16];

        public void Register(IGunMode mode)
        {
            if (mode != null && !modes.Contains(mode)) modes.Add(mode);
        }

        public void CycleMode(int dir)
        {
            if (modes.Count == 0) return;
            modeIndex = (modeIndex + dir + modes.Count) % modes.Count;
        }

        public void SelectMode(int index) => modeIndex = Mathf.Clamp(index, 0, Math.Max(0, modes.Count - 1));
        public int ModeIndex => modeIndex;

        private void Awake() => BuildVisuals();

        private void OnDestroy()
        {
            if (line != null) Destroy(line.gameObject);
            if (reticle != null) Destroy(reticle.gameObject);
            if (lineMat != null) Destroy(lineMat);
            if (reticleMat != null) Destroy(reticleMat);
        }

        private void Update()
        {
            bool vr = MenuInput.VRActive;
            var rig = Rig?.Invoke();

            bool aimHeld = GunEnabled && (vr ? MenuInput.XRHeld(rightHand: true, trigger: false) : MenuInput.KeyHeld(AimKey));
            bool blocked = !vr && Blocked != null && Blocked();
            Aiming = aimHeld && !blocked && rig != null;

            if (!Aiming)
            {
                SetVisible(false);
                prevFire = false;
                return;
            }

            if (!TryGetRay(rig, vr, out var ray, out var visualOrigin))
            {
                SetVisible(false);
                return;
            }

            var hit = Cast(ray, rig);
            Current = hit;

            bool fire = vr ? MenuInput.XRHeld(rightHand: true, trigger: true) : MenuInput.MouseHeld(0);
            if (fire && !prevFire && Mode != null)
            {
                fireFlash = 1f;
                string message;
                try { message = Mode.Fire(hit); }
                catch (Exception e) { message = e.Message; Debug.LogException(e); }
                if (!string.IsNullOrEmpty(message)) Report?.Invoke(message);
            }
            prevFire = fire;

            DrawVisuals(visualOrigin, hit);
        }

        private static bool TryGetRay(IRigProvider rig, bool vr, out Ray ray, out Vector3 visualOrigin)
        {
            ray = default;
            visualOrigin = default;

            if (vr)
            {
                var hand = rig.HandAimRay;
                if (hand == null) return false;
                ray = hand.Value;
                visualOrigin = ray.origin;
                return true;
            }

            var cam = rig.Camera;
            if (cam == null) return false;
            ray = cam.ScreenPointToRay(MenuInput.MousePosition);
            // Start the laser a little below-right of the view, like it's held, so it isn't a dot.
            visualOrigin = cam.transform.TransformPoint(new Vector3(0.22f, -0.18f, 0.35f));
            return true;
        }

        private GunHit Cast(Ray ray, IRigProvider rig)
        {
            var result = new GunHit { Ray = ray, Point = ray.GetPoint(MaxDistance), Distance = MaxDistance };
            // NonAlloc results are unordered: if the buffer filled up, the nearest hit may be missing,
            // so fall back to the allocating version rather than aim through walls.
            RaycastHit[] results = hits;
            int count = Physics.RaycastNonAlloc(ray, hits, MaxDistance, Layers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
            {
                results = Physics.RaycastAll(ray, MaxDistance, Layers, QueryTriggerInteraction.Ignore);
                count = results.Length;
            }

            var camRoot = rig.Camera != null ? rig.Camera.transform.root : null;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var h = results[i];
                // Skip the player's own body: anything very close to the ray origin or under the camera's rig.
                if (h.distance < 0.25f) continue;
                if (camRoot != null && h.collider.transform.root == camRoot && h.distance < 2f) continue;
                if (h.distance >= best) continue;
                best = h.distance;
                result.HasHit = true;
                result.Point = h.point;
                result.Normal = h.normal;
                result.Distance = h.distance;
                result.Collider = h.collider;
            }
            return result;
        }

        // ------------------------------------------------------------------ visuals

        private void BuildVisuals()
        {
            // UI/Default is included in every build that has uGUI, so it exists even inside an injected game.
            var shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var lineGo = new GameObject("[BundleMenu] Gun Laser");
            DontDestroyOnLoad(lineGo);
            line = lineGo.AddComponent<LineRenderer>();
            lineMat = new Material(shader);
            line.sharedMaterial = lineMat;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            lineGo.SetActive(false);

            reticle = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            reticle.name = "[BundleMenu] Gun Reticle";
            DontDestroyOnLoad(reticle.gameObject);
            Destroy(reticle.GetComponent<Collider>()); // must never block our own ray
            reticleMat = new Material(shader);
            var r = reticle.GetComponent<Renderer>();
            r.sharedMaterial = reticleMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
            ring.name = "Ring";
            Destroy(ring.GetComponent<Collider>());
            ring.SetParent(reticle, false);
            ring.GetComponent<Renderer>().sharedMaterial = reticleMat;
            reticle.gameObject.SetActive(false);
        }

        private void SetVisible(bool visible)
        {
            if (line != null && line.gameObject.activeSelf != visible) line.gameObject.SetActive(visible);
            if (reticle != null && reticle.gameObject.activeSelf != visible) reticle.gameObject.SetActive(visible);
        }

        private void DrawVisuals(Vector3 origin, GunHit hit)
        {
            SetVisible(true);
            var theme = Theme?.Invoke();
            Color a = theme != null ? theme.Accent : Color.cyan;
            Color b = theme != null ? theme.Accent2 : Color.magenta;
            var tint = Mode?.Tint(hit);
            if (tint != null) { a = tint.Value; b = Color.Lerp(tint.Value, b, 0.35f); }

            fireFlash = Mathf.MoveTowards(fireFlash, 0f, Time.unscaledDeltaTime * 4f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);

            // Laser: thin at the hand, wider at the target; flashes white on fire.
            line.SetPosition(0, origin);
            line.SetPosition(1, hit.Point);
            float w = 0.006f + 0.01f * fireFlash;
            line.startWidth = w * 0.6f;
            line.endWidth = w * (hit.HasHit ? 1.6f : 0.8f);
            var start = Color.Lerp(a, Color.white, fireFlash); start.a = 0.9f;
            var end = Color.Lerp(b, Color.white, fireFlash); end.a = hit.HasHit ? 0.9f : 0.25f;
            line.startColor = start;
            line.endColor = end;

            // Reticle: a dot plus a ring lying on the surface, sized so it reads at any distance.
            reticle.position = hit.Point;
            float size = Mathf.Clamp(hit.Distance * 0.012f, 0.03f, 0.35f) * (1f + 0.15f * pulse + 0.6f * fireFlash);
            reticle.localScale = Vector3.one * size;
            reticleMat.color = Color.Lerp(a, Color.white, fireFlash * 0.7f);
            ring.gameObject.SetActive(hit.HasHit);
            if (hit.HasHit)
            {
                reticle.rotation = Quaternion.FromToRotation(Vector3.up, hit.Normal);
                ring.localScale = new Vector3(2.4f + 0.4f * pulse, 0.02f, 2.4f + 0.4f * pulse);
                ring.localPosition = Vector3.zero;
            }
        }
    }
}
