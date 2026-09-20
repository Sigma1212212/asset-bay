using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Themes you designed yourself. The launcher's Designer page writes .json files into
    /// %APPDATA%\AssetBay\themes; the menu picks them up at startup and whenever you ask it to reload,
    /// and they join the theme list right after the built-in ones.
    /// </summary>
    public static class ThemeFolder
    {
        public static string Path =>
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AssetBay", "themes");

        public static string LastError { get; private set; }

        /// <summary>Re-read the folder. Returns how many themes are now available.</summary>
        public static int Reload()
        {
            LastError = null;
            var loaded = new List<MenuTheme>();
            try
            {
                if (Directory.Exists(Path))
                    foreach (var file in Directory.GetFiles(Path, "*.json"))
                    {
                        try
                        {
                            var theme = ThemePresets.FromJson(File.ReadAllText(file));
                            if (string.IsNullOrEmpty(theme.DisplayName) || theme.DisplayName == "Pack theme")
                                theme.DisplayName = System.IO.Path.GetFileNameWithoutExtension(file);
                            loaded.Add(theme);
                        }
                        catch (Exception e)
                        {
                            LastError = System.IO.Path.GetFileName(file) + ": " + e.Message;
                            Debug.LogWarning("[BundleMenu] Bad theme file " + file + ": " + e.Message);
                        }
                    }
            }
            catch (Exception e)
            {
                LastError = e.Message;
            }

            // Replace the previous folder themes, keeping any that came from the content pack.
            foreach (var old in ThemePresets.PackThemes)
                if (old != null && old != ThemePresets.InUse && FromFolder.Contains(old.DisplayName))
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(old);
                    else UnityEngine.Object.DestroyImmediate(old);
                }
            ThemePresets.PackThemes.RemoveAll(t => t == null || FromFolder.Contains(t.DisplayName));
            FromFolder.Clear();
            foreach (var t in loaded)
            {
                ThemePresets.PackThemes.RemoveAll(x => x.DisplayName == t.DisplayName);
                ThemePresets.PackThemes.Add(t);
                FromFolder.Add(t.DisplayName);
            }
            return loaded.Count;
        }

        /// <summary>Names of the themes that came from the folder (so a reload can replace just those).</summary>
        public static readonly HashSet<string> FromFolder = new HashSet<string>();

        public static int Count => FromFolder.Count;
    }
}
