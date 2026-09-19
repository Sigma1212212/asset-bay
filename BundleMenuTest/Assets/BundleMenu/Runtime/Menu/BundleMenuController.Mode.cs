using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>Which menu you get: the everyday one, or the full one.</summary>
    public enum MenuMode
    {
        Normal, // library, videos, tablet, look settings, your own tag
        Admin,  // everything: mods, gun, bundles, tools, remote control, tags for other players
    }

    /// <summary>
    /// Admin / normal split. Normal is the default, so a friend running the same menu only gets the safe
    /// half. Setting a PIN locks the switch: after that, Admin needs the PIN typed in the safe-mode
    /// window (H). The PIN is never stored, only a salted SHA-256 of it, and it never leaves this PC.
    /// </summary>
    public sealed partial class BundleMenuController
    {
        public MenuMode Mode { get; private set; } = MenuMode.Normal;
        public bool IsAdmin => Mode == MenuMode.Admin;
        public bool PinSet => !string.IsNullOrEmpty(PlayerPrefs.GetString(Prefs + "adminpin", ""));

        private void LoadMode()
        {
            var saved = (MenuMode)PlayerPrefs.GetInt(Prefs + "mode", (int)MenuMode.Normal);
            Mode = Enum.IsDefined(typeof(MenuMode), saved) ? saved : MenuMode.Normal;
        }

        private void SaveMode()
        {
            PlayerPrefs.SetInt(Prefs + "mode", (int)Mode);
            PlayerPrefs.Save();
        }

        /// <summary>Switch menus. Going to Admin needs the PIN once one is set (pass it in, or use H).</summary>
        public bool SetMode(MenuMode mode, string pin = null)
        {
            if (mode == MenuMode.Admin && PinSet && !PinMatches(pin))
            {
                Toast("Admin is locked. Enter the PIN in the safe-mode window (H > admin).", ToastKind.Error);
                return false;
            }
            Mode = mode;
            SaveMode();
            while (pages.Count > 1) pages.Pop();   // leave any page the other mode owned
            Toast(mode == MenuMode.Admin ? "Admin menu unlocked" : "Normal menu", ToastKind.Info);
            dirty = true;
            Presence?.TouchState();
            return true;
        }

        public void ToggleMode() => SetMode(IsAdmin ? MenuMode.Normal : MenuMode.Admin);

        public bool PinMatches(string pin) =>
            !string.IsNullOrEmpty(pin) && Hash(pin) == PlayerPrefs.GetString(Prefs + "adminpin", "");

        /// <summary>Set, change or clear the PIN. The old one is required once a PIN exists.</summary>
        public bool SetPin(string oldPin, string newPin)
        {
            if (PinSet && !PinMatches(oldPin))
            {
                Toast("That's not the current PIN.", ToastKind.Error);
                return false;
            }
            newPin = (newPin ?? "").Trim();
            if (newPin.Length == 0)
            {
                PlayerPrefs.DeleteKey(Prefs + "adminpin");
                PlayerPrefs.Save();
                Toast("Admin PIN removed.", ToastKind.Info);
                return true;
            }
            if (newPin.Length < 4)
            {
                Toast("Use at least 4 characters.", ToastKind.Error);
                return false;
            }
            PlayerPrefs.SetString(Prefs + "adminpin", Hash(newPin));
            PlayerPrefs.Save();
            Toast("Admin PIN set. Keep it to yourself.", ToastKind.Success);
            return true;
        }

        private static string Hash(string pin)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes("assetbay-pin:" + pin));
                var sb = new StringBuilder(64);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // ------------------------------------------------------------------ tags

        public NameTags Tags { get; private set; }

        private void SetupTags()
        {
            Tags = gameObject.AddComponent<NameTags>();
            Tags.Presence = Presence;
            Tags.ViewCamera = () => Rig.Camera;
            Tags.Enabled = PlayerPrefs.GetInt(Prefs + "tagson", 1) == 1;
        }

        public bool TagsOn => Tags != null && Tags.Enabled;

        public void ToggleTags()
        {
            if (Tags == null) return;
            Tags.Enabled = !Tags.Enabled;
            PlayerPrefs.SetInt(Prefs + "tagson", Tags.Enabled ? 1 : 0);
            PlayerPrefs.Save();
            dirty = true;
        }

        /// <summary>Change your own trait; everyone running Asset Bay near you sees it above your head.</summary>
        public void CycleMyTag(int dir)
        {
            TagStore.CycleMyTag(dir);
            Presence?.TouchState();
            Toast(string.IsNullOrEmpty(TagStore.MyTag) ? "Your tag is off" : $"Your tag: {TagStore.MyTag}", ToastKind.Info);
            dirty = true;
        }

        public void CycleMyTagColour(int dir)
        {
            TagStore.CycleMyColour(dir);
            Presence?.TouchState();
            dirty = true;
        }
    }
}
