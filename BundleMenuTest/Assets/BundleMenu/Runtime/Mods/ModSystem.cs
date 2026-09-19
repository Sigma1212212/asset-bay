using System;
using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>What a mod can reach. Everything here is your own player only.</summary>
    public sealed class ModContext
    {
        public GorillaTagPlayer Player;
        public GorillaTagRig Rig;
        public Func<MenuTheme> Theme;
        public Func<bool> GunUsesRightGrip;   // the gun owns the right grip while it's enabled
        public Action<string> Toast;
    }

    /// <summary>
    /// A toggleable mod. Implement this, then add it in <see cref="ModRunner.CreateDefaults"/>.
    /// Tick runs every frame and FixedTick every physics step, but only while the mod is on
    /// AND the lobby allows mods.
    /// </summary>
    public abstract class Mod
    {
        public abstract string Name { get; }
        public abstract string Hint { get; }            // controls, shown in the menu row
        public bool Enabled { get; internal set; }

        public virtual void OnEnable(ModContext ctx) { }
        public virtual void OnDisable(ModContext ctx) { }
        public virtual void Tick(ModContext ctx) { }
        public virtual void FixedTick(ModContext ctx) { }
    }

    /// <summary>
    /// Runs mods, and enforces the lobby rule: mods only work offline, in private rooms, or in modded
    /// rooms. When you join a public lobby every mod is switched off and stays off. Using movement mods
    /// in public lobbies breaks Gorilla Tag's rules and gets accounts banned; this keeps yours safe.
    /// </summary>
    public sealed class ModRunner : MonoBehaviour
    {
        public ModContext Context { get; } = new ModContext();
        public IReadOnlyList<Mod> Mods => mods;
        public LobbyKind Lobby { get; private set; } = LobbyKind.NotGorillaTag;

        public bool Available => Context.Player != null;
        public bool Allowed => Available && Lobby != LobbyKind.Public;

        public event Action Changed;

        private readonly List<Mod> mods = new List<Mod>();
        private float nextLobbyCheck;

        public void Init(ModContext ctx)
        {
            Context.Player = ctx.Player;
            Context.Rig = ctx.Rig;
            Context.Theme = ctx.Theme;
            Context.GunUsesRightGrip = ctx.GunUsesRightGrip;
            Context.Toast = ctx.Toast;
            CreateDefaults();
            Lobby = Available ? Context.Player.Lobby : LobbyKind.NotGorillaTag;
        }

        private void CreateDefaults()
        {
            mods.Add(new PlatformsMod());
            mods.Add(new FlyMod());
            mods.Add(new SpeedBoostMod());
            mods.Add(new LowGravityMod());
            mods.Add(new SizeMod());
        }

        public string Toggle(Mod mod)
        {
            if (mod.Enabled) { SetEnabled(mod, false); return $"{mod.Name} off"; }
            if (!Available) return "Mods only work inside Gorilla Tag.";
            if (!Context.Player.Ready) return "Your player isn't loaded yet - try again in a moment.";
            if (!Allowed) return "Mods are off in public lobbies. Join a private or modded room.";
            SetEnabled(mod, true);
            return $"{mod.Name} on";
        }

        private void SetEnabled(Mod mod, bool on)
        {
            if (mod.Enabled == on) return;
            mod.Enabled = on;
            try { if (on) mod.OnEnable(Context); else mod.OnDisable(Context); }
            catch (Exception e) { Debug.LogException(e); }
            Changed?.Invoke();
        }

        public void DisableAll()
        {
            foreach (var m in mods) SetEnabled(m, false);
        }

        private void Update()
        {
            if (!Available) return;

            if (Time.unscaledTime >= nextLobbyCheck)
            {
                nextLobbyCheck = Time.unscaledTime + 0.5f;
                var lobby = Context.Player.Lobby;
                if (lobby != Lobby)
                {
                    Lobby = lobby;
                    if (lobby == LobbyKind.Public && mods.Exists(m => m.Enabled))
                    {
                        DisableAll();
                        Context.Toast?.Invoke("Public lobby - mods switched off.");
                    }
                    Changed?.Invoke();
                }
            }

            if (!Allowed) return;
            foreach (var m in mods)
            {
                if (!m.Enabled) continue;
                try { m.Tick(Context); }
                catch (Exception e) { Debug.LogException(e); SetEnabled(m, false); }
            }
        }

        private void FixedUpdate()
        {
            if (!Allowed) return;
            foreach (var m in mods)
            {
                if (!m.Enabled) continue;
                try { m.FixedTick(Context); }
                catch (Exception e) { Debug.LogException(e); SetEnabled(m, false); }
            }
        }

        private void OnDestroy() => DisableAll(); // eject restores everything (speed, size, platforms)
    }

    // =====================================================================================================
    // Mods

    /// <summary>
    /// Hold a grip and a platform appears under that hand, so you can stand on air and climb anywhere.
    /// Desktop: hold C for one under your feet. Platforms are local colliders on a layer the game walks on;
    /// they fade in, follow the moment you grab, and dissolve when you let go.
    /// </summary>
    public sealed class PlatformsMod : Mod
    {
        public override string Name => "Platforms";
        public override string Hint => MenuInput.VRActive ? "hold grip" : "hold C";

        private readonly Platform left = new Platform(), right = new Platform(), feet = new Platform();

        public override void Tick(ModContext ctx)
        {
            if (MenuInput.VRActive)
            {
                Drive(left, MenuInput.XRHeld(false, false), ctx.Rig?.WristAnchor, ctx, 0.05f);
                bool rightFree = ctx.GunUsesRightGrip == null || !ctx.GunUsesRightGrip();
                Drive(right, rightFree && MenuInput.XRHeld(true, false), ctx.Rig?.RightHandTransform, ctx, 0.05f);
            }
            else
            {
                var body = ctx.Player.BodyCollider;
                Drive(feet, MenuInput.KeyHeld(KeyCode.C), body != null ? body.transform : null, ctx, 0f, underBody: body);
            }
            left.Animate(); right.Animate(); feet.Animate();
        }

        private static void Drive(Platform p, bool held, Transform hand, ModContext ctx, float below, Collider underBody = null)
        {
            if (held && hand != null && !p.Placed)
            {
                Vector3 pos = underBody != null
                    ? new Vector3(underBody.bounds.center.x, underBody.bounds.min.y - 0.03f, underBody.bounds.center.z)
                    : hand.position + Vector3.down * below;
                var yaw = Quaternion.Euler(0f, hand.eulerAngles.y, 0f);
                p.Place(pos, yaw, ctx.Player.WalkableLayer, ctx.Theme?.Invoke());
            }
            else if (!held && p.Placed)
            {
                p.Release();
            }
        }

        public override void OnDisable(ModContext ctx)
        {
            left.Destroy(); right.Destroy(); feet.Destroy();
        }

        private sealed class Platform
        {
            private GameObject go;
            private Material mat;
            private float t, target;
            private Color color;
            public bool Placed => go != null && target > 0f;

            public void Place(Vector3 pos, Quaternion rot, int layer, MenuTheme theme)
            {
                if (go == null)
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "[BundleMenu] Platform";
                    var shader = Shader.Find("UI/Default");
                    mat = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
                    go.GetComponent<Renderer>().sharedMaterial = mat;
                }
                if (layer >= 0) go.layer = layer;
                go.transform.SetPositionAndRotation(pos, rot);
                go.SetActive(true);
                color = theme != null ? theme.Accent : new Color(0.25f, 0.9f, 1f);
                target = 1f;
                t = 0f;
            }

            public void Release() => target = 0f;

            public void Animate()
            {
                if (go == null) return;
                t = Mathf.MoveTowards(t, target, Time.deltaTime * (target > 0f ? 8f : 5f));
                float e = target > 0f ? Ease.OutBack(t, 2f) : t;
                go.transform.localScale = new Vector3(0.3f * e + 0.001f, 0.025f, 0.3f * e + 0.001f);
                mat.color = new Color(color.r, color.g, color.b, 0.55f * t);
                if (target <= 0f && t <= 0f) go.SetActive(false);
            }

            public void Destroy()
            {
                if (go != null) UnityEngine.Object.Destroy(go);
                if (mat != null) UnityEngine.Object.Destroy(mat);
                go = null;
                target = t = 0f;
            }
        }
    }

    /// <summary>
    /// Fly where you look. VR: hold the left X button. Desktop: hold F.
    /// Speed ramps up smoothly instead of snapping, and letting go keeps a little momentum.
    /// </summary>
    public sealed class FlyMod : Mod
    {
        public override string Name => "Fly";
        public override string Hint => MenuInput.VRActive ? "hold X" : "hold F";

        public float Speed = 11f;
        private float throttle;

        public override void FixedTick(ModContext ctx)
        {
            bool held = MenuInput.VRActive ? MenuInput.XRButtonHeld(false, primary: true) : MenuInput.KeyHeld(KeyCode.F);
            throttle = Mathf.MoveTowards(throttle, held ? 1f : 0f, Time.fixedDeltaTime * (held ? 3f : 6f));
            if (throttle <= 0f) return;

            var rb = ctx.Player.Body;
            var cam = ctx.Rig?.Camera;
            if (rb == null || cam == null) return;
            var target = cam.transform.forward * Speed * Ease.OutCubic(throttle);
            // Blend toward the target velocity: responsive, but not an instant snap.
            rb.velocity = Vector3.Lerp(rb.velocity, target, 0.25f);
        }
    }

    /// <summary>Faster, higher jumps (x1.35). Your original values come back when it's switched off.</summary>
    public sealed class SpeedBoostMod : Mod
    {
        public override string Name => "Speed Boost";
        public override string Hint => "x1.35";
        private float? jump, maxJump;

        public override void OnEnable(ModContext ctx)
        {
            jump = ctx.Player.JumpMultiplier;
            maxJump = ctx.Player.MaxJumpSpeed;
        }

        public override void Tick(ModContext ctx)
        {
            // Re-applied every frame because the game resets these when you change areas.
            if (jump != null) ctx.Player.JumpMultiplier = jump.Value * 1.35f;
            if (maxJump != null) ctx.Player.MaxJumpSpeed = maxJump.Value * 1.35f;
        }

        public override void OnDisable(ModContext ctx)
        {
            if (jump != null) ctx.Player.JumpMultiplier = jump;
            if (maxJump != null) ctx.Player.MaxJumpSpeed = maxJump;
        }
    }

    /// <summary>Moon-like gravity: cancels 60% of gravity on your body only.</summary>
    public sealed class LowGravityMod : Mod
    {
        public override string Name => "Low Gravity";
        public override string Hint => "40% gravity";

        public override void FixedTick(ModContext ctx)
        {
            var rb = ctx.Player.Body;
            if (rb != null) rb.AddForce(-Physics.gravity * 0.6f, ForceMode.Acceleration);
        }
    }

    /// <summary>Grow bigger (x1.5). Your original size comes back when it's switched off.</summary>
    public sealed class SizeMod : Mod
    {
        public override string Name => "Big Monke";
        public override string Hint => "x1.5 size";
        private float original = 1f;

        public override void OnEnable(ModContext ctx)
        {
            original = ctx.Player.Scale ?? 1f;
            ctx.Player.SetScale(original * 1.5f);
        }

        public override void OnDisable(ModContext ctx) => ctx.Player.SetScale(original);
    }

    /// <summary>Save where you're standing, then snap back to it later. Actions, not a toggle.</summary>
    public sealed class Checkpoint
    {
        private Vector3? position;
        private Quaternion rotation;

        public bool Saved => position != null;

        public string Save(ModContext ctx)
        {
            var body = ctx.Player?.BodyCollider;
            if (body == null) return "Your player isn't loaded yet.";
            position = body.transform.position;
            rotation = Quaternion.Euler(0f, (ctx.Rig?.Camera != null ? ctx.Rig.Camera.transform.eulerAngles.y : 0f), 0f);
            return "Checkpoint saved";
        }

        public string Return(ModContext ctx, bool allowed)
        {
            if (!allowed) return "Mods are off in public lobbies.";
            if (position == null) return "Save a checkpoint first.";
            return ctx.Player.TeleportTo(position.Value, rotation) ? "Back at checkpoint" : "Couldn't teleport.";
        }
    }
}
