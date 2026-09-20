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
        public float Charge;           // 0..1, how long the trigger was held for charge-up modes
    }

    /// <summary>Where the aim comes from on a keyboard.</summary>
    public enum GunAim { Mouse, Crosshair }

    /// <summary>
    /// The aiming end of the menu: one place that works out what you're pointing at, draws the laser,
    /// reticle and the gun in your hand, and hands the result to the selected <see cref="GunMode"/>.
    ///
    /// Desktop: hold the aim key (right mouse by default) and click to fire. VR: hold the right grip and
    /// pull the right trigger. Modes can be single-shot, automatic, or charged by holding the trigger.
    /// Everything it does happens on your own machine.
    /// </summary>
    public sealed class GunLib : MonoBehaviour
    {
        public bool GunEnabled;
        public KeyCode AimKey = KeyCode.Mouse1;
        public GunAim Aim = GunAim.Mouse;
        public bool ShowModel = true;
        public float MaxDistance = 60f;
        public LayerMask Layers = ~0;

        /// <summary>Supplied by the owner.</summary>
        public Func<IRigProvider> Rig;
        public Func<MenuTheme> Theme;
        public Func<bool> Blocked;              // e.g. cursor is over the menu
        public Action<string> Report;           // status-line messages
        public Func<Canvas> Overlay;            // screen canvas, for the crosshair

        public bool Aiming { get; private set; }
        public bool Firing { get; private set; }
        public GunHit Current { get; private set; }
        public GunMode Mode => modes.Count > 0 ? modes[modeIndex] : null;
        public IReadOnlyList<GunMode> Modes => modes;
        public float Charge { get; private set; }
        public float Cooldown { get; private set; }     // 0..1, how much of the wait is left
        public bool Rebinding { get; private set; }
        public event Action Changed;

        private readonly List<GunMode> modes = new List<GunMode>();
        private int modeIndex;
        private bool prevFire;
        private float readyAt, chargeStart;
        private GunVisuals visuals;

        private const string PrefsAim = "BundleMenu.gun.aim";
        private const string PrefsKey = "BundleMenu.gun.aimkey";
        private const string PrefsModel = "BundleMenu.gun.model";
        private const string PrefsMode = "BundleMenu.gun.mode";

        // ------------------------------------------------------------------ setup

        private void Awake()
        {
            visuals = new GunVisuals();
            Aim = (GunAim)PlayerPrefs.GetInt(PrefsAim, (int)GunAim.Mouse);
            ShowModel = PlayerPrefs.GetInt(PrefsModel, 1) == 1;
            int saved = PlayerPrefs.GetInt(PrefsKey, 0);
            if (saved != 0) AimKey = (KeyCode)saved;
        }

        private void Start() => SelectMode(PlayerPrefs.GetInt(PrefsMode, 0));

        private void OnDestroy() => visuals?.Destroy();

        public void Register(GunMode mode)
        {
            if (mode == null || modes.Contains(mode)) return;
            mode.Gun = this;
            modes.Add(mode);
        }

        public void CycleMode(int dir)
        {
            if (modes.Count == 0) return;
            SelectMode(((modeIndex + dir) % modes.Count + modes.Count) % modes.Count);
        }

        public void SelectMode(int index)
        {
            if (modes.Count == 0) return;
            index = Mathf.Clamp(index, 0, modes.Count - 1);
            if (index != modeIndex) modes[modeIndex]?.OnDeselected();
            modeIndex = index;
            modes[modeIndex]?.OnSelected(this);
            Charge = 0f;
            PlayerPrefs.SetInt(PrefsMode, modeIndex);
            Changed?.Invoke();
        }

        public int ModeIndex => modeIndex;

        public void SetAim(GunAim aim)
        {
            Aim = aim;
            PlayerPrefs.SetInt(PrefsAim, (int)aim);
            Changed?.Invoke();
        }

        public void SetModelVisible(bool visible)
        {
            ShowModel = visible;
            if (!visible) visuals.HideModel();
            PlayerPrefs.SetInt(PrefsModel, visible ? 1 : 0);
            Changed?.Invoke();
        }

        /// <summary>Listen for the next key pressed and use it as the aim button.</summary>
        public void ListenForAimKey() => Rebinding = true;

        // ------------------------------------------------------------------ per-frame

        private void Update()
        {
            if (Rebinding)
            {
                if (MenuInput.KeyDown(KeyCode.Escape)) Rebinding = false;
                else if (MenuInput.AnyKeyDown(out var key))
                {
                    AimKey = key;
                    PlayerPrefs.SetInt(PrefsKey, (int)key);
                    Rebinding = false;
                    Changed?.Invoke();
                }
                return;
            }

            // Switched off: one comparison per frame and nothing else.
            if (!GunEnabled)
            {
                if (Aiming || Firing) { EndFire(default); Aiming = false; visuals.Hide(); }
                return;
            }

            bool vr = MenuInput.VRActive;
            var rig = Rig?.Invoke();
            Cooldown = Mathf.Clamp01((readyAt - Time.unscaledTime) / 0.35f);

            bool aimHeld = vr ? MenuInput.XRHeld(rightHand: true, trigger: false) : MenuInput.KeyHeld(AimKey);
            bool blocked = !vr && Blocked != null && Blocked();
            Aiming = aimHeld && !blocked && rig != null;

            if (!Aiming)
            {
                if (Firing) EndFire(default);
                visuals.Hide();
                prevFire = false;
                Charge = 0f;
                return;
            }

            if (!TryGetRay(rig, vr, out var ray, out var muzzle))
            {
                visuals.Hide();
                return;
            }

            var hit = Cast(ray, rig);
            var mode = Mode;
            bool trigger = vr ? MenuInput.XRHeld(rightHand: true, trigger: true) : MenuInput.MouseHeld(0);

            // Charge modes build up while the trigger is down and go off when it's let go.
            if (mode != null && mode.ChargeTime > 0f)
            {
                if (trigger && !prevFire) chargeStart = Time.unscaledTime;
                Charge = trigger ? Mathf.Clamp01((Time.unscaledTime - chargeStart) / mode.ChargeTime) : 0f;
            }
            hit.Charge = Charge;
            Current = hit;

            if (mode != null)
            {
                if (trigger && !prevFire) { Firing = true; mode.Press(hit); }
                if (trigger) mode.Hold(hit);
                if (!trigger && prevFire) EndFire(hit);

                bool wantsShot = mode.ChargeTime > 0f
                    ? !trigger && prevFire
                    : mode.Automatic ? trigger : trigger && !prevFire;

                if (wantsShot && Time.unscaledTime >= readyAt) Shoot(hit, mode, muzzle);
                mode.Aiming(hit);
            }
            prevFire = trigger;

            visuals.Draw(this, muzzle, hit, Theme?.Invoke(), mode);
            visuals.DrawModel(this, rig, vr);
        }

        private void EndFire(GunHit hit)
        {
            Firing = false;
            Mode?.Release(hit);
        }

        private void Shoot(GunHit hit, GunMode mode, Vector3 muzzle)
        {
            readyAt = Time.unscaledTime + Mathf.Max(0.02f, mode.Cooldown);
            visuals.Kick(0.6f + 0.7f * hit.Charge);

            string message;
            try { message = mode.Fire(hit); }
            catch (Exception e) { message = e.Message; Debug.LogException(e); }

            if (hit.HasHit) visuals.Impact(hit.Point, hit.Normal, mode.Tint(hit) ?? Theme?.Invoke()?.Accent ?? Color.cyan);
            if (!string.IsNullOrEmpty(message)) Report?.Invoke(message);
            Charge = 0f;
        }

        // ------------------------------------------------------------------ aiming

        private bool TryGetRay(IRigProvider rig, bool vr, out Ray ray, out Vector3 muzzle)
        {
            ray = default;
            muzzle = default;

            if (vr)
            {
                var hand = rig.HandAimRay;
                if (hand == null) return false;
                ray = hand.Value;
                muzzle = ray.origin + ray.direction * 0.12f;
                return true;
            }

            var cam = rig.Camera;
            if (cam == null) return false;
            ray = Aim == GunAim.Crosshair
                ? new Ray(cam.transform.position, cam.transform.forward)
                : cam.ScreenPointToRay(MenuInput.MousePosition);
            // The laser leaves a point below-right of the view, so it reads as held rather than as a dot.
            muzzle = cam.transform.TransformPoint(new Vector3(0.22f, -0.18f, 0.4f));
            return true;
        }

        /// <summary>Where the gun's barrel is in the world right now (used by projectiles).</summary>
        public Vector3 Muzzle(Vector3 fallback) => visuals.MuzzlePoint(fallback);

        private readonly RaycastHit[] hits = new RaycastHit[16];

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

        /// <summary>Short line for the menu: what the gun is doing right now.</summary>
        public string StatusText()
        {
            if (Rebinding) return "press a key";
            if (!GunEnabled) return "off";
            var mode = Mode;
            if (mode == null) return "no mode";
            if (!Aiming) return mode.Name;
            return mode.Describe(Current);
        }
    }
}
