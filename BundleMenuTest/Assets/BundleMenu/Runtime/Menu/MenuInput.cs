using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace BundleMenu
{
    public enum VRToggleButton { None, LeftSecondary, LeftPrimary, RightSecondary, RightPrimary, LeftMenu }

    /// <summary>
    /// One place that answers "was this key pressed?" regardless of which input backend the game uses.
    /// New Input System is preferred when present; the legacy Input Manager is used otherwise; if the legacy
    /// manager is disabled in the player settings (it throws), that path turns itself off quietly.
    /// </summary>
    public static class MenuInput
    {
        private static bool legacyBroken;
        private static readonly Dictionary<VRToggleButton, bool> vrPrevious = new Dictionary<VRToggleButton, bool>();
        private static int vrFrame = -1;
        private static readonly Dictionary<VRToggleButton, bool> vrDownThisFrame = new Dictionary<VRToggleButton, bool>();

        public static bool KeyDown(KeyCode key)
        {
            if (key == KeyCode.None) return false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && TryMap(key, out var k))
            {
                var control = kb[k];
                if (control != null) return control.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (!legacyBroken)
            {
                try { return Input.GetKeyDown(key); }
                catch (InvalidOperationException) { legacyBroken = true; }
            }
#endif
            return false;
        }

        // ------------------------------------------------------------------ mouse
        // Read directly (not through the EventSystem) so clicks work even in games whose EventSystem
        // is set up for VR only, like Gorilla Tag.

        public static Vector2 MousePosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                if (mouse != null) return mouse.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                if (!legacyBroken)
                {
                    try { return Input.mousePosition; }
                    catch (InvalidOperationException) { legacyBroken = true; }
                }
#endif
                return new Vector2(-1, -1);
            }
        }

        /// <summary>0 = left, 1 = right, 2 = middle.</summary>
        public static bool MouseHeld(int button)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
                return button == 0 ? mouse.leftButton.isPressed : button == 1 ? mouse.rightButton.isPressed : mouse.middleButton.isPressed;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (!legacyBroken)
            {
                try { return Input.GetMouseButton(button); }
                catch (InvalidOperationException) { legacyBroken = true; }
            }
#endif
            return false;
        }

        /// <summary>Scroll wheel this frame: positive = up.</summary>
        public static float Scroll
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    float wheel = mouse.scroll.ReadValue().y;
                    return Mathf.Abs(wheel) > 0.01f ? Mathf.Sign(wheel) : 0f;
                }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
                if (!legacyBroken)
                {
                    try
                    {
                        float wheel = Input.mouseScrollDelta.y;
                        return Mathf.Abs(wheel) > 0.01f ? Mathf.Sign(wheel) : 0f;
                    }
                    catch (InvalidOperationException) { legacyBroken = true; }
                }
#endif
                return 0f;
            }
        }

        /// <summary>Held state of a VR controller's grip or trigger (analog values count past 0.5).</summary>
        public static bool XRHeld(bool rightHand, bool trigger)
        {
            try
            {
                var device = InputDevices.GetDeviceAtXRNode(rightHand ? XRNode.RightHand : XRNode.LeftHand);
                if (!device.isValid) return false;
                if (device.TryGetFeatureValue(trigger ? UnityEngine.XR.CommonUsages.trigger : UnityEngine.XR.CommonUsages.grip, out float v))
                    return v > 0.5f;
                return device.TryGetFeatureValue(trigger ? UnityEngine.XR.CommonUsages.triggerButton : UnityEngine.XR.CommonUsages.gripButton, out bool b) && b;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Held state of a controller face button (primary = A/X, secondary = B/Y).</summary>
        public static bool XRButtonHeld(bool rightHand, bool primary)
        {
            try
            {
                var device = InputDevices.GetDeviceAtXRNode(rightHand ? XRNode.RightHand : XRNode.LeftHand);
                if (!device.isValid) return false;
                var usage = primary ? UnityEngine.XR.CommonUsages.primaryButton : UnityEngine.XR.CommonUsages.secondaryButton;
                return device.TryGetFeatureValue(usage, out bool pressed) && pressed;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Held state of a key, including KeyCode.Mouse0/1/2.</summary>
        public static bool KeyHeld(KeyCode key)
        {
            if (key == KeyCode.None) return false;
            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse2) return MouseHeld(key - KeyCode.Mouse0);
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && TryMap(key, out var k))
            {
                var control = kb[k];
                if (control != null) return control.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (!legacyBroken)
            {
                try { return Input.GetKey(key); }
                catch (InvalidOperationException) { legacyBroken = true; }
            }
#endif
            return false;
        }

        /// <summary>
        /// The key (or mouse button) pressed this frame, for re-binding controls. Modifier keys on their
        /// own are ignored so holding shift while picking doesn't steal the binding.
        /// </summary>
        public static bool AnyKeyDown(out KeyCode key)
        {
            key = KeyCode.None;

            for (int i = 0; i < 3; i++)
                if (MouseDown(i)) { key = KeyCode.Mouse0 + i; return true; }

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                foreach (var control in kb.allKeys)
                {
                    if (!control.wasPressedThisFrame) continue;
                    if (!TryUnmap(control.keyCode, out key)) continue;
                    return true;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (!legacyBroken)
            {
                try
                {
                    if (!Input.anyKeyDown) return false;
                    foreach (var code in Bindable)
                        if (Input.GetKeyDown(code)) { key = code; return true; }
                }
                catch (InvalidOperationException) { legacyBroken = true; }
            }
#endif
            return false;
        }

        /// <summary>Rising edge of a mouse button.</summary>
        public static bool MouseDown(int button)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
                return button == 0 ? mouse.leftButton.wasPressedThisFrame
                     : button == 1 ? mouse.rightButton.wasPressedThisFrame
                                   : mouse.middleButton.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (!legacyBroken)
            {
                try { return Input.GetMouseButtonDown(button); }
                catch (InvalidOperationException) { legacyBroken = true; }
            }
#endif
            return false;
        }

        /// <summary>Keys worth offering as controls (built once, in the order people expect).</summary>
        private static KeyCode[] bindable;
        private static KeyCode[] Bindable
        {
            get
            {
                if (bindable != null) return bindable;
                var list = new List<KeyCode>(128);
                for (var code = KeyCode.A; code <= KeyCode.Z; code++) list.Add(code);
                for (var code = KeyCode.Alpha0; code <= KeyCode.Alpha9; code++) list.Add(code);
                for (var code = KeyCode.F1; code <= KeyCode.F12; code++) list.Add(code);
                for (var code = KeyCode.Keypad0; code <= KeyCode.KeypadEquals; code++) list.Add(code);
                list.AddRange(new[]
                {
                    KeyCode.Space, KeyCode.Tab, KeyCode.Return, KeyCode.LeftShift, KeyCode.RightShift,
                    KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt,
                    KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
                    KeyCode.Insert, KeyCode.Home, KeyCode.End, KeyCode.PageUp, KeyCode.PageDown,
                    KeyCode.Minus, KeyCode.Equals, KeyCode.LeftBracket, KeyCode.RightBracket,
                    KeyCode.Semicolon, KeyCode.Quote, KeyCode.Comma, KeyCode.Period, KeyCode.Slash,
                    KeyCode.Backslash, KeyCode.BackQuote, KeyCode.CapsLock,
                });
                return bindable = list.ToArray();
            }
        }

        /// <summary>True when a VR headset is actually running (as opposed to desktop / PC mode).</summary>
        public static bool VRActive
        {
            get
            {
                try { return XRSettings.isDeviceActive; }
                catch { return false; }
            }
        }

        /// <summary>Rising edge of a VR controller button (works with any XR plugin via UnityEngine.XR).</summary>
        public static bool VRButtonDown(VRToggleButton button)
        {
            if (button == VRToggleButton.None) return false;

            if (vrFrame != Time.frameCount)
            {
                vrFrame = Time.frameCount;
                vrDownThisFrame.Clear();
            }
            if (vrDownThisFrame.TryGetValue(button, out bool cached)) return cached;

            bool now = ReadVR(button);
            vrPrevious.TryGetValue(button, out bool before);
            vrPrevious[button] = now;
            bool down = now && !before;
            vrDownThisFrame[button] = down;
            return down;
        }

        private static bool ReadVR(VRToggleButton button)
        {
            try
            {
                XRNode node = button == VRToggleButton.RightPrimary || button == VRToggleButton.RightSecondary
                    ? XRNode.RightHand : XRNode.LeftHand;
                var device = InputDevices.GetDeviceAtXRNode(node);
                if (!device.isValid) return false;

                InputFeatureUsage<bool> usage;
                switch (button)
                {
                    case VRToggleButton.LeftPrimary:
                    case VRToggleButton.RightPrimary:  usage = UnityEngine.XR.CommonUsages.primaryButton; break;
                    case VRToggleButton.LeftMenu:      usage = UnityEngine.XR.CommonUsages.menuButton; break;
                    default:                           usage = UnityEngine.XR.CommonUsages.secondaryButton; break;
                }
                return device.TryGetFeatureValue(usage, out bool pressed) && pressed;
            }
            catch
            {
                return false; // XR module absent / not initialised
            }
        }

#if ENABLE_INPUT_SYSTEM
        private static bool TryUnmap(Key key, out KeyCode code)
        {
            string name = key.ToString();
            if (name.StartsWith("Digit")) name = "Alpha" + name.Substring(5);
            else if (name.StartsWith("Numpad") && name.Length == 7 && char.IsDigit(name[6])) name = "Keypad" + name[6];
            else switch (key)
            {
                case Key.Enter:       name = "Return"; break;
                case Key.NumpadEnter: name = "KeypadEnter"; break;
                case Key.Backquote:   name = "BackQuote"; break;
                case Key.LeftCtrl:    name = "LeftControl"; break;
                case Key.RightCtrl:   name = "RightControl"; break;
                case Key.LeftMeta:    name = "LeftCommand"; break;
            }
            return Enum.TryParse(name, true, out code) && code != KeyCode.None;
        }

        private static bool TryMap(KeyCode code, out Key key)
        {
            string name = code.ToString();
            if (name.StartsWith("Alpha")) name = "Digit" + name.Substring(5);
            else if (name.StartsWith("Keypad") && name.Length == 7 && char.IsDigit(name[6])) name = "Numpad" + name[6];
            else switch (code)
            {
                case KeyCode.Return:       name = "Enter"; break;
                case KeyCode.KeypadEnter:  name = "NumpadEnter"; break;
                case KeyCode.BackQuote:    name = "Backquote"; break;
                case KeyCode.LeftControl:  name = "LeftCtrl"; break;
                case KeyCode.RightControl: name = "RightCtrl"; break;
                case KeyCode.LeftCommand:  name = "LeftCommand"; break;
            }
            return Enum.TryParse(name, true, out key) && key != Key.None;
        }
#endif
    }
}
