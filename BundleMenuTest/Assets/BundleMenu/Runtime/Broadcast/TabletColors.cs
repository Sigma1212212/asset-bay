using UnityEngine;

namespace BundleMenu
{
    /// <summary>Turns tablet colour settings ("#RRGGBB", "#RRGGBBAA", "theme:Accent") into colours.</summary>
    public static class TabletColors
    {
        public static Color Resolve(string spec, MenuTheme theme, Color fallback)
        {
            if (string.IsNullOrEmpty(spec)) return fallback;
            if (spec.StartsWith("theme:"))
            {
                if (theme == null) return fallback;
                switch (spec.Substring(6))
                {
                    case "Accent": return theme.Accent;
                    case "Accent2": return theme.Accent2;
                    case "PanelTop": return theme.PanelTop;
                    case "PanelBottom": return theme.PanelBottom;
                    case "Text": return theme.Text;
                    case "SubText": return theme.SubText;
                    case "Button": return theme.ButtonFill;
                    default: return fallback;
                }
            }
            return ColorUtility.TryParseHtmlString(spec, out var c) ? c : fallback;
        }
    }
}
