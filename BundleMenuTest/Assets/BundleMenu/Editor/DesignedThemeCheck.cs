using UnityEngine;

namespace BundleMenu.Editor
{
    /// <summary>
    /// Loads the themes the launcher's Designer saved (%APPDATA%\AssetBay\themes) and renders the first
    /// one, so a designed theme can be checked without starting the game.
    ///   Unity -batchmode -executeMethod BundleMenu.Editor.DesignedThemeCheck.Run -quit
    /// </summary>
    public static class DesignedThemeCheck
    {
        public static void Run()
        {
            int count = ThemeFolder.Reload();
            Debug.Log($"DESIGNED: folder={ThemeFolder.Path} loaded={count} error={ThemeFolder.LastError ?? "none"}");
            foreach (var t in ThemePresets.PackThemes)
                Debug.Log($"DESIGNED: \"{t.DisplayName}\" style={t.Style} depth={t.Depth} buttonDepth={t.ButtonDepth} bevel={t.Bevel} radius={t.PanelRadius}");
            if (ThemePresets.PackThemes.Count == 0) { Debug.Log("DESIGNED: nothing to render"); return; }

            foreach (var theme in ThemePresets.PackThemes)
            {
                string safe = theme.DisplayName.Replace(" ", "_");
                ThemePreview.RenderOne(theme, theme.Style, $"{TabletPreview.OutputFolder}/designed_{safe}.png");
            }
            Debug.Log("DESIGNED: done");
        }
    }
}
