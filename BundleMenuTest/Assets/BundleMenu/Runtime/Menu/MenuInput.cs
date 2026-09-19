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
