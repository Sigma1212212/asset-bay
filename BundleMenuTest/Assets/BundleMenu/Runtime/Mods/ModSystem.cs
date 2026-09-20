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
        public Canvas ScreenCanvas;           // for on-screen readouts (speedometer)
        public Func<bool> Allowed;            // lobby gate, for things outside the runner (gun modes)
        public ModBinds Binds;                // the controls, shared by every mod
        public AssetSpawner Spawner;          // for mods that put things in the world
        public Func<bool> MenuBusy;           // true while the menu is taking your clicks / re-binding
    }

    /// <summary>
    /// A toggleable mod. Implement this, then add it in <see cref="ModRunner.CreateDefaults"/>.
    /// Tick runs every frame and FixedTick every physics step, but only while the mod is on
    /// AND the lobby allows mods.
    /// </summary>
    public abstract class Mod
    {
        public abstract string Name { get; }
        public abstract string Hint { get; }            // what it's doing, shown in the menu row
        public bool Enabled { get; internal set; }

        /// <summary>How this mod is used: a switch, held down, or tapped.</summary>
        public virtual ModTrigger Trigger => ModTrigger.Switch;

        /// <summary>The control it starts with on a keyboard. Everyone can change it in Mods &gt; Controls.</summary>
        public virtual KeyCode DefaultKey => KeyCode.None;

        /// <summary>The control it starts with in VR.</summary>
        public virtual VRInput DefaultButton => VRInput.None;

        /// <summary>This mod's control, whichever device you're on. Set by the runner.</summary>
        public ModBind Bind { get; internal set; }

        /// <summary>True while the control is being used (held, or latched on if you chose tap-to-stick).</summary>
        protected bool Using => Bind != null && Bind.held;

        /// <summary>True on the frame the control is pressed.</summary>
        protected bool Pressed => Bind != null && Bind.down;

        /// <summary>The control as words, for the menu: "F" on a keyboard, "left X" in VR.</summary>
        public string ControlText =>
            Bind == null ? "-" : MenuInput.VRActive ? Bind.ButtonText : Bind.KeyText;

        /// <summary>Adjustable values shown under the mod in the menu (click cycles, right-click goes back).</summary>
        public virtual IEnumerable<ModSetting> Settings => Array.Empty<ModSetting>();

        public virtual void OnEnable(ModContext ctx) { }
        public virtual void OnDisable(ModContext ctx) { }
        public virtual void Tick(ModContext ctx) { }
        public virtual void FixedTick(ModContext ctx) { }
    }

    public sealed class ModSetting
    {
        public string Name;
        public Func<string> Value;
        public Action<int> Cycle;

        public static ModSetting Choice(string name, float[] options, Func<float> get, Action<float> set, string format)
        {
            return new ModSetting
            {
                Name = name,
                Value = () => string.Format(format, get()),
                Cycle = dir =>
                {
                    int i = Array.FindIndex(options, o => Mathf.Approximately(o, get()));
                    set(options[((i < 0 ? 0 : i) + dir + options.Length) % options.Length]);
                },
            };
        }
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
        public ModBinds Binds { get; } = new ModBinds();
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
            Context.ScreenCanvas = ctx.ScreenCanvas;
            Context.Spawner = ctx.Spawner;
            Context.MenuBusy = ctx.MenuBusy;
            Context.Allowed = () => Allowed;
            Context.Binds = Binds;
            Binds.Load();                       // saved choices first, so defaults never overwrite them
            CreateDefaults();
            foreach (var mod in mods) mod.Bind = Binds.Register(mod.Name, mod.DefaultKey, mod.DefaultButton);
            Binds.Save();                       // keeps newly added mods in the file
            Lobby = Available ? Context.Player.Lobby : LobbyKind.NotGorillaTag;
        }

        private void CreateDefaults()
        {
            // Movement
            mods.Add(new PlatformsMod());
            mods.Add(new FlyMod());
            mods.Add(new NoclipMod());
            mods.Add(new SpeedBoostMod());
            mods.Add(new LowGravityMod());
            mods.Add(new HoverMod());
            mods.Add(new DashMod());
            mods.Add(new RewindMod());
            mods.Add(new SizeMod());
            // Visual
            mods.Add(new EspMod());
            mods.Add(new HandTrailsMod());
            mods.Add(new SpeedometerMod());
            mods.Add(new FreecamMod());
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
            if (!on) Binds.Release(mod.Name);
            try { if (on) mod.OnEnable(Context); else mod.OnDisable(Context); }
            catch (Exception e) { Debug.LogException(e); }
            Changed?.Invoke();
        }

        public Mod Find(string name) =>
            mods.Find(m => string.Equals(m.Name.Replace(" ", ""), name.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));

        public string Set(Mod mod, bool on) => mod.Enabled == on ? $"{mod.Name} already {(on ? "on" : "off")}" : Toggle(mod);

        /// <summary>Puts every control back to the key and button the mod started with.</summary>
        public void ResetBinds()
        {
            Binds.ResetAll(() =>
            {
                foreach (var mod in mods) mod.Bind = Binds.Register(mod.Name, mod.DefaultKey, mod.DefaultButton);
            });
        }

        public void DisableAll()
        {
            foreach (var m in mods) SetEnabled(m, false);
        }

        private void Update()
        {
            Binds.Tick();
            if (!Available) return;

            // Switch-style mods can be turned on and off by their key without opening the menu.
            bool busy = Binds.Listening != null || (Context.MenuBusy != null && Context.MenuBusy());
            if (!busy)
                for (int i = 0; i < mods.Count; i++)
                {
                    var mod = mods[i];
                    if (mod.Trigger != ModTrigger.Switch || !mod.Bind.down) continue;
                    Context.Toast?.Invoke(Toggle(mod));
                }

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
        public override string Hint => "hold " + ControlText + " - " + WhereName;
        public override ModTrigger Trigger => ModTrigger.Held;
        public override KeyCode DefaultKey => KeyCode.C;
        public override VRInput DefaultButton => VRInput.LeftGrip;

        /// <summary>0 = under your feet, 1 = where you're looking, 2 = both.</summary>
        public int Where = 2;
        public float Reach = 12f;
        private string WhereName => Where == 0 ? "at your feet" : Where == 1 ? "where you look" : "feet + aim";

        private ModSetting[] settings;
        public override IEnumerable<ModSetting> Settings => settings ?? (settings = new[]
        {
            new ModSetting
            {
                Name = "Put it", Value = () => WhereName,
                Cycle = dir => Where = (Where + dir + 3) % 3,
            },
            ModSetting.Choice("Reach", new[] { 6f, 12f, 20f, 35f }, () => Reach, v => Reach = v, "{0:0} m"),
        });

        private readonly Platform left = new Platform(), right = new Platform(), feet = new Platform(), aimed = new Platform();

        public override void Tick(ModContext ctx)
        {
            if (MenuInput.VRActive)
            {
                // In VR a platform appears under each hand you grip, exactly as before.
                Drive(left, Using || MenuInput.XRHeld(false, false), ctx.Rig?.WristAnchor, ctx, 0.05f);
                bool rightFree = ctx.GunUsesRightGrip == null || !ctx.GunUsesRightGrip();
                Drive(right, rightFree && MenuInput.XRHeld(true, false), ctx.Rig?.RightHandTransform, ctx, 0.05f);
            }
            else
            {
                // On a keyboard one key does the same job: under your feet, or out where you're looking.
                bool held = Using;
                var body = ctx.Player.BodyCollider;
                Drive(feet, held && Where != 1, body != null ? body.transform : null, ctx, 0f, underBody: body);

                var cam = ctx.Rig?.Camera;
                if (held && Where != 0 && cam != null && !aimed.Placed)
                {
                    var t = cam.transform;
                    var point = Physics.Raycast(t.position, t.forward, out var hit, Reach, ~0, QueryTriggerInteraction.Ignore)
                        ? hit.point + Vector3.up * 0.02f
                        : t.position + t.forward * Reach;
                    aimed.Place(point, Quaternion.Euler(0f, t.eulerAngles.y, 0f), ctx.Player.WalkableLayer, ctx.Theme?.Invoke());
                }
                else if (!held && aimed.Placed)
                {
                    aimed.Release();
                }
            }
            left.Animate(); right.Animate(); feet.Animate(); aimed.Animate();
        }

        /// <summary>Used by the platform gun: drop a standalone platform that stays put.</summary>
        public static GameObject Drop(Vector3 position, Quaternion rotation, int layer, MenuTheme theme, float size)
        {
            var platform = new Platform();
            platform.Place(position, rotation, layer, theme);
            return platform.Freeze(size);
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
            left.Destroy(); right.Destroy(); feet.Destroy(); aimed.Destroy();
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
            public GameObject Object => go;

            /// <summary>Leaves the platform in the world at full size and hands it over (the gun owns it then).</summary>
            public GameObject Freeze(float size)
            {
                if (go == null) return null;
                t = target = 1f;
                mat.name = "platform (menu)";
                go.transform.localScale = new Vector3(size, 0.05f, size);
                mat.color = new Color(color.r, color.g, color.b, 0.6f);
                var handed = go;
                go = null;       // the caller keeps it; this platform no longer animates or destroys it
                mat = null;
                return handed;
            }

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
        public override string Hint => "hold " + ControlText + (MenuInput.VRActive ? "" : " (+ WASD)");
        public override ModTrigger Trigger => ModTrigger.Held;
        public override KeyCode DefaultKey => KeyCode.F;
        public override VRInput DefaultButton => VRInput.LeftPrimary;

        public float Speed = 11f;
        private float throttle;

        private ModSetting[] settings;
        public override IEnumerable<ModSetting> Settings => settings ?? (settings = new[]
        {
            ModSetting.Choice("Fly speed", new[] { 6f, 11f, 18f, 28f }, () => Speed, v => Speed = v, "{0:0} m/s"),
        });

        public override void FixedTick(ModContext ctx)
        {
            bool held = Using;
            throttle = Mathf.MoveTowards(throttle, held ? 1f : 0f, Time.fixedDeltaTime * (held ? 3f : 6f));
            if (throttle <= 0f) return;

            var rb = ctx.Player.Body;
            var cam = ctx.Rig?.Camera;
            if (rb == null || cam == null) return;

            // Where you look, plus WASD / Space / Ctrl steering on a keyboard so it flies like a PC game.
            var t = cam.transform;
            var direction = t.forward;
            if (!MenuInput.VRActive)
            {
                var steer = Vector3.zero;
                if (MenuInput.KeyHeld(KeyCode.W)) steer += t.forward;
                if (MenuInput.KeyHeld(KeyCode.S)) steer -= t.forward;
                if (MenuInput.KeyHeld(KeyCode.D)) steer += t.right;
                if (MenuInput.KeyHeld(KeyCode.A)) steer -= t.right;
                if (MenuInput.KeyHeld(KeyCode.Space)) steer += Vector3.up;
                if (MenuInput.KeyHeld(KeyCode.LeftControl)) steer += Vector3.down;
                if (steer.sqrMagnitude > 0.001f) direction = steer.normalized;
            }

            var target = direction * (Speed * Ease.OutCubic(throttle));
            // Blend toward the target velocity: responsive, but not an instant snap.
            rb.velocity = Vector3.Lerp(rb.velocity, target, 0.25f);
        }
    }

    /// <summary>Faster, higher jumps (x1.35). Your original values come back when it's switched off.</summary>
    public sealed class SpeedBoostMod : Mod
    {
        public override string Name => "Speed Boost";
        public override string Hint => $"x{Multiplier:0.##}";
        public override KeyCode DefaultKey => KeyCode.B;
        public float Multiplier = 1.35f;
        private float? jump, maxJump;

        private ModSetting[] settings;
        public override IEnumerable<ModSetting> Settings => settings ?? (settings = new[]
        {
            ModSetting.Choice("Boost", new[] { 1.15f, 1.35f, 1.6f, 2f, 2.5f }, () => Multiplier, v => Multiplier = v, "x{0:0.##}"),
        });

        public override void OnEnable(ModContext ctx)
        {
            jump = ctx.Player.JumpMultiplier;
            maxJump = ctx.Player.MaxJumpSpeed;
        }

        public override void Tick(ModContext ctx)
        {
            // Re-applied every frame because the game resets these when you change areas.
            if (jump != null) ctx.Player.JumpMultiplier = jump.Value * Multiplier;
            if (maxJump != null) ctx.Player.MaxJumpSpeed = maxJump.Value * Multiplier;
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
        public override KeyCode DefaultKey => KeyCode.G;

        public override void FixedTick(ModContext ctx)
        {
            var rb = ctx.Player.Body;
            if (rb != null) rb.AddForce(-Physics.gravity * 0.6f, ForceMode.Acceleration);
        }
    }

    /// <summary>Grow bigger (x1.5). Your original size comes back when it's switched off.</summary>
    public sealed class SizeMod : Mod
    {
        public override string Name => "Size";
        public override string Hint => $"x{Factor:0.##}";
        public override KeyCode DefaultKey => KeyCode.U;
        public float Factor = 1.5f;
        private float original = 1f;
        private float applied;

        private ModSetting[] settings;
        public override IEnumerable<ModSetting> Settings => settings ?? (settings = new[]
        {
            ModSetting.Choice("Size", new[] { 0.5f, 0.75f, 1.5f, 2f }, () => Factor, v => Factor = v, "x{0:0.##}"),
        });

        public override void OnEnable(ModContext ctx)
        {
            original = ctx.Player.Scale ?? 1f;
            applied = Factor;
            ctx.Player.SetScale(original * Factor);
        }

        public override void Tick(ModContext ctx)
        {
            if (Mathf.Approximately(applied, Factor)) return; // setting changed while on
            applied = Factor;
            ctx.Player.SetScale(original * Factor);
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
