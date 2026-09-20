using System;
using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>A VR controller input a mod can be bound to (buttons, grips and triggers).</summary>
    public enum VRInput
    {
        None,
        LeftPrimary, LeftSecondary, RightPrimary, RightSecondary,
        LeftGrip, RightGrip, LeftTrigger, RightTrigger,
        LeftMenu,
    }

    /// <summary>How a mod is driven: a switch you turn on, or something you hold to use.</summary>
    public enum ModTrigger
    {
        /// <summary>On or off. The key flips it.</summary>
        Switch,
        /// <summary>Armed from the menu, then used by holding the key (fly, platforms).</summary>
        Held,
        /// <summary>Armed from the menu, then used by tapping the key (dash, rewind).</summary>
        Tap,
    }

    /// <summary>One mod's controls: a keyboard key for PC and a controller button for VR.</summary>
    public sealed class ModBind
    {
        public string Name;
        public KeyCode Key = KeyCode.None;
        public VRInput Button = VRInput.None;
        /// <summary>Hold-style mods only: tap once to keep it going instead of holding the key down.</summary>
        public bool Sticky;

        internal bool held, down, rawBefore, latched;

        public string KeyText => Key == KeyCode.None ? "unbound" : Pretty(Key);
        public string ButtonText => Button == VRInput.None ? "unbound" : Pretty(Button);

        public static string Pretty(KeyCode key)
        {
            string name = key.ToString();
            if (name.StartsWith("Alpha")) return name.Substring(5);
            if (name.StartsWith("Mouse")) return "mouse " + (name[5] - '0' + 1);
            if (name.StartsWith("Left")) return "left " + name.Substring(4).ToLowerInvariant();
            if (name.StartsWith("Right")) return "right " + name.Substring(5).ToLowerInvariant();
            return name.ToLowerInvariant();
        }

        public static string Pretty(VRInput button)
        {
            switch (button)
            {
                case VRInput.LeftPrimary:    return "left X";
                case VRInput.LeftSecondary:  return "left Y";
                case VRInput.RightPrimary:   return "right A";
                case VRInput.RightSecondary: return "right B";
                case VRInput.LeftGrip:       return "left grip";
                case VRInput.RightGrip:      return "right grip";
                case VRInput.LeftTrigger:    return "left trigger";
                case VRInput.RightTrigger:   return "right trigger";
                case VRInput.LeftMenu:       return "menu button";
                default:                     return "unbound";
            }
        }
    }

    /// <summary>
    /// The controls for every mod, in one place, so the same mod behaves the same whether you're in VR
    /// or playing on a keyboard. Each mod gets a PC key and a VR button; either can be changed in
    /// Mods &gt; Controls (or the H window) and both are saved on this PC.
    ///
    /// Nothing here reads input more than once per frame: <see cref="Tick"/> samples everything, and the
    /// mods just ask "am I being held?".
    /// </summary>
    public sealed class ModBinds
    {
        private const string PrefsKey = "BundleMenu.binds";

        private readonly Dictionary<string, ModBind> binds = new Dictionary<string, ModBind>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ModBind> ordered = new List<ModBind>();

        public IReadOnlyList<ModBind> All => ordered;
        public event Action Changed;

        /// <summary>The bind being re-assigned right now, or null.</summary>
        public ModBind Listening { get; private set; }
        public bool ListeningForVR { get; private set; }

        /// <summary>Registers a mod's default controls (existing saved choices win).</summary>
        public ModBind Register(string name, KeyCode key, VRInput button)
        {
            if (binds.TryGetValue(name, out var existing)) return existing;
            var bind = new ModBind { Name = name, Key = key, Button = button };
            binds[name] = bind;
            ordered.Add(bind);
            return bind;
        }

        public ModBind Get(string name) => name != null && binds.TryGetValue(name, out var bind) ? bind : null;

        /// <summary>True while the control is being used this frame (VR button in VR, key on PC).</summary>
        public bool Held(string name) => Get(name)?.held ?? false;

        /// <summary>True on the frame the control is pressed.</summary>
        public bool Down(string name) => Get(name)?.down ?? false;

        /// <summary>Reads every control once for this frame.</summary>
        public void Tick()
        {
            bool vr = MenuInput.VRActive;
            for (int i = 0; i < ordered.Count; i++)
            {
                var bind = ordered[i];
                bool raw = vr ? VRHeld(bind.Button) : bind.Key != KeyCode.None && MenuInput.KeyHeld(bind.Key);
                bool pressed = raw && !bind.rawBefore;
                bind.rawBefore = raw;
                bind.down = pressed;

                if (bind.Sticky)
                {
                    if (pressed) bind.latched = !bind.latched;
                    bind.held = bind.latched;
                }
                else
                {
                    bind.latched = false;
                    bind.held = raw;
                }
            }
        }

        /// <summary>Forgets any latched "sticky" control (used when a mod is switched off).</summary>
        public void Release(string name)
        {
            var bind = Get(name);
            if (bind == null) return;
            bind.latched = false;
            bind.held = false;
        }

        public static bool VRHeld(VRInput button)
        {
            switch (button)
            {
                case VRInput.None:           return false;
                case VRInput.LeftGrip:       return MenuInput.XRHeld(false, trigger: false);
                case VRInput.RightGrip:      return MenuInput.XRHeld(true, trigger: false);
                case VRInput.LeftTrigger:    return MenuInput.XRHeld(false, trigger: true);
                case VRInput.RightTrigger:   return MenuInput.XRHeld(true, trigger: true);
                case VRInput.LeftPrimary:    return MenuInput.XRButtonHeld(false, primary: true);
                case VRInput.LeftSecondary:  return MenuInput.XRButtonHeld(false, primary: false);
                case VRInput.RightPrimary:   return MenuInput.XRButtonHeld(true, primary: true);
                case VRInput.RightSecondary: return MenuInput.XRButtonHeld(true, primary: false);
                case VRInput.LeftMenu:       return MenuInput.VRButtonDown(VRToggleButton.LeftMenu);
                default:                     return false;
            }
        }

        // ------------------------------------------------------------------ re-binding

        /// <summary>Start listening for the next key (or VR button) and give it to this bind.</summary>
        public void Listen(ModBind bind, bool forVR)
        {
            Listening = bind;
            ListeningForVR = forVR;
        }

        public void StopListening()
        {
            Listening = null;
            Changed?.Invoke();
        }

        /// <summary>Call every frame: finishes a re-bind when something is pressed. Returns true when it captured.</summary>
        public bool PollListening()
        {
            if (Listening == null) return false;

            if (MenuInput.KeyDown(KeyCode.Escape))
            {
                StopListening();
                return true;
            }

            if (ListeningForVR)
            {
                for (var button = VRInput.LeftPrimary; button <= VRInput.LeftMenu; button++)
                {
                    if (!VRHeld(button)) continue;
                    Listening.Button = button;
                    Save();
                    StopListening();
                    return true;
                }
                return false;
            }

            if (!MenuInput.AnyKeyDown(out var key)) return false;
            Listening.Key = key == KeyCode.Backspace || key == KeyCode.Delete ? KeyCode.None : key;
            Listening.rawBefore = true;   // don't count the binding press as a use
            Save();
            StopListening();
            return true;
        }

        /// <summary>Other mods that use the same key (so the menu can warn about clashes).</summary>
        public string ConflictFor(ModBind bind)
        {
            if (bind == null || bind.Key == KeyCode.None) return null;
            for (int i = 0; i < ordered.Count; i++)
            {
                var other = ordered[i];
                if (other != bind && other.Key == bind.Key) return other.Name;
            }
            return null;
        }

        public void ResetAll(Action reapplyDefaults)
        {
            binds.Clear();
            ordered.Clear();
            reapplyDefaults?.Invoke();
            Save();
        }

        // ------------------------------------------------------------------ saving
        // One line per bind: name|key|button|sticky, separated by ';'.

        public void Save()
        {
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < ordered.Count; i++)
            {
                var bind = ordered[i];
                if (i > 0) text.Append(';');
                text.Append(bind.Name.Replace(';', ' ').Replace('|', ' ')).Append('|')
                    .Append((int)bind.Key).Append('|')
                    .Append((int)bind.Button).Append('|')
                    .Append(bind.Sticky ? '1' : '0');
            }
            PlayerPrefs.SetString(PrefsKey, text.ToString());
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Applies saved choices. Call once, before the mods register their defaults.</summary>
        public void Load()
        {
            string raw = PlayerPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(raw)) return;
            foreach (var part in raw.Split(';'))
            {
                var bits = part.Split('|');
                if (bits.Length < 4) continue;
                if (!int.TryParse(bits[1], out int key) || !int.TryParse(bits[2], out int button)) continue;
                var bind = new ModBind
                {
                    Name = bits[0],
                    Key = (KeyCode)key,
                    Button = (VRInput)button,
                    Sticky = bits[3] == "1",
                };
                if (binds.ContainsKey(bind.Name)) continue;
                binds[bind.Name] = bind;
                ordered.Add(bind);
            }
        }
    }
}
