using System;
using TMPro;
using UnityEngine;

namespace BundleMenu
{
    // New presets go after Custom so the numbers already saved in players' settings keep meaning the same theme.
    public enum ThemePreset { Halo, Solstice, Circuit, Velvet, Custom, Arcade, Glass, Minimal, Neon }

    /// <summary>How rows are arranged: a single column, or two columns of tiles.</summary>
    public enum ThemeLayout { List, Grid }

    /// <summary>What a button does when you hover it.</summary>
    public enum HoverStyle { Grow, Slide, Glow }

    /// <summary>
    /// Everything visual about the menu. Create your own via Assets > Create > Bundle Menu > Theme,
    /// assign it to the controller's Custom Theme slot, or tweak the built-in presets below.
    /// Changing theme at runtime rebuilds the panel (cheap - it is ~40 objects).
    /// </summary>
    [CreateAssetMenu(menuName = "Bundle Menu/Theme", fileName = "MenuTheme")]
    public sealed class MenuTheme : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "Custom";

        [Header("Panel")]
        public Color PanelTop = Hex(0x1B1F3A);
        public Color PanelBottom = Hex(0x0D0F22);
        public Color EdgeTop = Hex(0x3BE8FF);
        public Color EdgeBottom = Hex(0x8A5CFF);
        [Range(0, 6)] public float EdgeWidth = 2f;
        [Range(0, 48)] public float PanelRadius = 24f;
        [Range(0, 1)] public float GlowStrength = 0.35f;

        [Header("Accent")]
        public Color Accent = Hex(0x3BE8FF);
        public Color Accent2 = Hex(0x8A5CFF);

        [Header("Buttons")]
        [Range(0, 32)] public float ButtonRadius = 14f;
        public Color ButtonFill = Hex(0x252A52, 0.92f);
        public Color ButtonFillHover = Hex(0x2F3668);
        public Color ButtonFillPressed = Hex(0x3B4585);
        public Color ButtonEdge = new Color(1, 1, 1, 0f);
        public Color ButtonEdgeHover = Hex(0x3BE8FF, 0.75f);
        [Range(0, 4)] public float ButtonEdgeWidth = 1.5f;

        [Header("Text")]
        public TMP_FontAsset Font;                  // null = TMP default font
        public Color Text = Hex(0xE9ECFF);
        public Color SubText = Hex(0x8C95C6);
        public FontStyles LabelStyle = FontStyles.Normal;
        public FontStyles TitleStyle = FontStyles.Bold;
        public float LabelSpacing = 0.5f;
        public float TitleSpacing = 1f;

        [Header("Behaviour")]
        public ThemeLayout Layout = ThemeLayout.List;
        public HoverStyle Hover = HoverStyle.Grow;
        public float RowHeight = 56f;
        public float RowSpacing = 8f;
        public float LabelSize = 21f;
        public float ValueSize = 15f;
        [Tooltip("0 = steady. Above 0, the rim and edges flicker like neon tubes.")]
        [Range(0, 1)] public float Flicker;
        [Tooltip("Entrance animation this theme prefers (-1 = use the menu setting).")]
        public int EntranceOverride = -1;

        [Header("Status lights")]
        public Color StatusIdle = Hex(0x5A6290);
        public Color StatusBusy = Hex(0xFFB547);
        public Color StatusOk = Hex(0x45E08A);
        public Color StatusError = Hex(0xFF4D6A);

        public static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        public MenuTheme Clone()
        {
            var copy = Instantiate(this);
            copy.hideFlags = HideFlags.DontSave;
            return copy;
        }
    }

    /// <summary>The four built-in looks. Original designs; none are copies of existing menus.</summary>
    public static class ThemePresets
    {
        /// <summary>Themes added by the content bundle (theme-*.json), shown after the built-in ones.</summary>
        public static readonly System.Collections.Generic.List<MenuTheme> PackThemes = new System.Collections.Generic.List<MenuTheme>();

        /// <summary>
        /// A theme from JSON. Starts from Halo, so a pack theme only needs the fields it changes.
        /// Colours are {"r":1,"g":0.5,"b":0,"a":1}; export a built-in theme with the editor tool to get a template.
        /// </summary>
        public static MenuTheme FromJson(string json)
        {
            var t = Create(ThemePreset.Halo);
            JsonUtility.FromJsonOverwrite(json, t);
            t.hideFlags = HideFlags.DontSave;
            t.RowHeight = Mathf.Clamp(t.RowHeight, 32f, 96f);
            t.RowSpacing = Mathf.Clamp(t.RowSpacing, 0f, 24f);
            t.LabelSize = Mathf.Clamp(t.LabelSize, 10f, 32f);
            t.ValueSize = Mathf.Clamp(t.ValueSize, 8f, 24f);
            if (string.IsNullOrEmpty(t.DisplayName)) t.DisplayName = "Pack theme";
            return t;
        }

        public static MenuTheme Create(ThemePreset preset)
        {
            var t = ScriptableObject.CreateInstance<MenuTheme>();
            t.hideFlags = HideFlags.DontSave;

            switch (preset)
            {
                // Night-sky glass: cyan-to-violet rim, soft glow, filled pills. The default.
                case ThemePreset.Halo:
                    t.DisplayName = "Halo";
                    break;

                // Warm daylight: cream panel, coral rim, chunky rounded buttons, dark text.
                case ThemePreset.Solstice:
                    t.DisplayName = "Solstice";
                    t.PanelTop = MenuTheme.Hex(0xFFF7EE); t.PanelBottom = MenuTheme.Hex(0xFFE4CC);
                    t.EdgeTop = MenuTheme.Hex(0xFF9A3D); t.EdgeBottom = MenuTheme.Hex(0xFF4F7B);
                    t.EdgeWidth = 3f; t.PanelRadius = 34f; t.GlowStrength = 0.28f;
                    t.Accent = MenuTheme.Hex(0xFF6A3D); t.Accent2 = MenuTheme.Hex(0xFF4F7B);
                    t.ButtonRadius = 22f;
                    t.ButtonFill = MenuTheme.Hex(0xFFFFFF, 0.95f);
                    t.ButtonFillHover = MenuTheme.Hex(0xFFF0E2);
                    t.ButtonFillPressed = MenuTheme.Hex(0xFFDCC2);
                    t.ButtonEdge = MenuTheme.Hex(0xFFC9A6);
                    t.ButtonEdgeHover = MenuTheme.Hex(0xFF6A3D);
                    t.ButtonEdgeWidth = 2f;
                    t.Text = MenuTheme.Hex(0x3B2A22); t.SubText = MenuTheme.Hex(0x9A7A68);
                    t.LabelStyle = FontStyles.Bold; t.TitleStyle = FontStyles.Bold;
                    t.StatusIdle = MenuTheme.Hex(0xD9C2B0); t.StatusBusy = MenuTheme.Hex(0xF2A33A);
                    t.StatusOk = MenuTheme.Hex(0x2DBE72); t.StatusError = MenuTheme.Hex(0xE8334F);
                    break;

                // Oscilloscope: black glass, phosphor green, square corners, outlined buttons, spaced caps.
                case ThemePreset.Circuit:
                    t.DisplayName = "Circuit";
                    t.PanelTop = MenuTheme.Hex(0x0A120E); t.PanelBottom = MenuTheme.Hex(0x040806);
                    t.EdgeTop = MenuTheme.Hex(0x39FF88); t.EdgeBottom = MenuTheme.Hex(0x00B894);
                    t.EdgeWidth = 1.5f; t.PanelRadius = 4f; t.GlowStrength = 0.45f;
                    t.Accent = MenuTheme.Hex(0x39FF88); t.Accent2 = MenuTheme.Hex(0x00C2A8);
                    t.ButtonRadius = 2f;
                    t.ButtonFill = MenuTheme.Hex(0x0F1F17, 0.9f);
                    t.ButtonFillHover = MenuTheme.Hex(0x163324);
                    t.ButtonFillPressed = MenuTheme.Hex(0x1F4A33);
                    t.ButtonEdge = MenuTheme.Hex(0x39FF88, 0.35f);
                    t.ButtonEdgeHover = MenuTheme.Hex(0x39FF88);
                    t.ButtonEdgeWidth = 1f;
                    t.Text = MenuTheme.Hex(0xC4FFDD); t.SubText = MenuTheme.Hex(0x4FA97A);
                    t.LabelStyle = FontStyles.UpperCase; t.LabelSpacing = 6f;
                    t.TitleStyle = FontStyles.UpperCase | FontStyles.Bold; t.TitleSpacing = 10f;
                    t.StatusIdle = MenuTheme.Hex(0x1F4A33); t.StatusBusy = MenuTheme.Hex(0xE8FF5A);
                    t.StatusOk = MenuTheme.Hex(0x39FF88); t.StatusError = MenuTheme.Hex(0xFF5A5A);
                    break;

                // Theatre: deep wine panel, brushed-gold rim and accents, small caps, slim outlined buttons.
                case ThemePreset.Velvet:
                    t.DisplayName = "Velvet";
                    t.PanelTop = MenuTheme.Hex(0x2B0F20); t.PanelBottom = MenuTheme.Hex(0x13060E);
                    t.EdgeTop = MenuTheme.Hex(0xF5D27A); t.EdgeBottom = MenuTheme.Hex(0xA8742A);
                    t.EdgeWidth = 2.5f; t.PanelRadius = 14f; t.GlowStrength = 0.22f;
                    t.Accent = MenuTheme.Hex(0xF2C14E); t.Accent2 = MenuTheme.Hex(0xC98A2E);
                    t.ButtonRadius = 10f;
                    t.ButtonFill = MenuTheme.Hex(0x3A1529, 0.85f);
                    t.ButtonFillHover = MenuTheme.Hex(0x4B1C36);
                    t.ButtonFillPressed = MenuTheme.Hex(0x5E2444);
                    t.ButtonEdge = MenuTheme.Hex(0xF2C14E, 0.28f);
                    t.ButtonEdgeHover = MenuTheme.Hex(0xF2C14E, 0.9f);
                    t.ButtonEdgeWidth = 1.5f;
                    t.Text = MenuTheme.Hex(0xFBEFD9); t.SubText = MenuTheme.Hex(0xB98F86);
                    t.LabelStyle = FontStyles.SmallCaps; t.LabelSpacing = 2f;
                    t.TitleStyle = FontStyles.SmallCaps | FontStyles.Bold; t.TitleSpacing = 3f;
                    t.StatusIdle = MenuTheme.Hex(0x6B3A52); t.StatusBusy = MenuTheme.Hex(0xF2C14E);
                    t.StatusOk = MenuTheme.Hex(0x7EE0A1); t.StatusError = MenuTheme.Hex(0xFF6B6B);
                    break;

                // Arcade: chunky two-column tiles, bold caps, a bouncy pop-in.
                case ThemePreset.Arcade:
                    t.DisplayName = "Arcade";
                    t.PanelTop = MenuTheme.Hex(0x2A1B5E); t.PanelBottom = MenuTheme.Hex(0x120A2E);
                    t.EdgeTop = MenuTheme.Hex(0xFFD23F); t.EdgeBottom = MenuTheme.Hex(0xFF3F81);
                    t.EdgeWidth = 4f; t.PanelRadius = 20f; t.GlowStrength = 0.4f;
                    t.Accent = MenuTheme.Hex(0xFFD23F); t.Accent2 = MenuTheme.Hex(0xFF3F81);
                    t.ButtonRadius = 16f;
                    t.ButtonFill = MenuTheme.Hex(0x3D2A8A); t.ButtonFillHover = MenuTheme.Hex(0x5A3FC0);
                    t.ButtonFillPressed = MenuTheme.Hex(0xFF3F81);
                    t.ButtonEdge = MenuTheme.Hex(0xFFD23F, 0.5f); t.ButtonEdgeHover = MenuTheme.Hex(0xFFD23F);
                    t.ButtonEdgeWidth = 3f;
                    t.Text = MenuTheme.Hex(0xFFF6D6); t.SubText = MenuTheme.Hex(0xC9B8FF);
                    t.LabelStyle = FontStyles.UpperCase | FontStyles.Bold; t.TitleStyle = FontStyles.UpperCase | FontStyles.Bold;
                    t.LabelSpacing = 2f; t.TitleSpacing = 4f;
                    t.Layout = ThemeLayout.Grid; t.Hover = HoverStyle.Grow;
                    t.RowHeight = 64f; t.RowSpacing = 10f; t.LabelSize = 17f; t.ValueSize = 13f;
                    t.EntranceOverride = (int)EntranceStyle.Pop;
                    break;

                // Glass: see-through frosted panel, soft white edges, light text.
                case ThemePreset.Glass:
                    t.DisplayName = "Glass";
                    t.PanelTop = MenuTheme.Hex(0xFFFFFF, 0.16f); t.PanelBottom = MenuTheme.Hex(0xB8C8FF, 0.10f);
                    t.EdgeTop = MenuTheme.Hex(0xFFFFFF, 0.75f); t.EdgeBottom = MenuTheme.Hex(0xFFFFFF, 0.2f);
                    t.EdgeWidth = 1.5f; t.PanelRadius = 28f; t.GlowStrength = 0.12f;
                    t.Accent = MenuTheme.Hex(0x9FE7FF); t.Accent2 = MenuTheme.Hex(0xE0B8FF);
                    t.ButtonRadius = 18f;
                    t.ButtonFill = MenuTheme.Hex(0xFFFFFF, 0.10f); t.ButtonFillHover = MenuTheme.Hex(0xFFFFFF, 0.22f);
                    t.ButtonFillPressed = MenuTheme.Hex(0xFFFFFF, 0.32f);
                    t.ButtonEdge = MenuTheme.Hex(0xFFFFFF, 0.25f); t.ButtonEdgeHover = MenuTheme.Hex(0xFFFFFF, 0.8f);
                    t.Text = MenuTheme.Hex(0xFFFFFF); t.SubText = MenuTheme.Hex(0xDDE6FF, 0.75f);
                    t.Hover = HoverStyle.Glow;
                    t.EntranceOverride = (int)EntranceStyle.Fade;
                    break;

                // Minimal: no boxes, just text rows with a thin accent line; hovering slides the row.
                case ThemePreset.Minimal:
                    t.DisplayName = "Minimal";
                    t.PanelTop = MenuTheme.Hex(0x111214); t.PanelBottom = MenuTheme.Hex(0x111214);
                    t.EdgeWidth = 0f; t.PanelRadius = 10f; t.GlowStrength = 0f;
                    t.Accent = MenuTheme.Hex(0xF2F2F2); t.Accent2 = MenuTheme.Hex(0x8A8A8A);
                    t.ButtonRadius = 4f;
                    t.ButtonFill = MenuTheme.Hex(0xFFFFFF, 0f); t.ButtonFillHover = MenuTheme.Hex(0xFFFFFF, 0.05f);
                    t.ButtonFillPressed = MenuTheme.Hex(0xFFFFFF, 0.1f);
                    t.ButtonEdge = MenuTheme.Hex(0xFFFFFF, 0f); t.ButtonEdgeHover = MenuTheme.Hex(0xFFFFFF, 0f);
                    t.Text = MenuTheme.Hex(0xEDEDED); t.SubText = MenuTheme.Hex(0x7A7A7A);
                    t.Hover = HoverStyle.Slide;
                    t.RowHeight = 44f; t.RowSpacing = 2f; t.LabelSize = 19f; t.ValueSize = 14f;
                    t.EntranceOverride = (int)EntranceStyle.SlideRight;
                    t.StatusIdle = MenuTheme.Hex(0x3A3A3A);
                    break;

                // Neon: black glass, hot pink / cyan tubes that flicker, outlined rows.
                case ThemePreset.Neon:
                    t.DisplayName = "Neon";
                    t.PanelTop = MenuTheme.Hex(0x07030D); t.PanelBottom = MenuTheme.Hex(0x02010A);
                    t.EdgeTop = MenuTheme.Hex(0xFF2BD6); t.EdgeBottom = MenuTheme.Hex(0x22E4FF);
                    t.EdgeWidth = 2.5f; t.PanelRadius = 16f; t.GlowStrength = 0.6f;
                    t.Accent = MenuTheme.Hex(0xFF2BD6); t.Accent2 = MenuTheme.Hex(0x22E4FF);
                    t.ButtonRadius = 12f;
                    t.ButtonFill = MenuTheme.Hex(0x0E0620, 0.9f); t.ButtonFillHover = MenuTheme.Hex(0x1B0B3A);
                    t.ButtonFillPressed = MenuTheme.Hex(0x2A0F55);
                    t.ButtonEdge = MenuTheme.Hex(0x22E4FF, 0.55f); t.ButtonEdgeHover = MenuTheme.Hex(0xFF2BD6);
                    t.ButtonEdgeWidth = 2f;
                    t.Text = MenuTheme.Hex(0xF4E9FF); t.SubText = MenuTheme.Hex(0x9A7FC2);
                    t.LabelSpacing = 1.5f; t.TitleStyle = FontStyles.Bold | FontStyles.Italic;
                    t.Hover = HoverStyle.Glow; t.Flicker = 0.6f;
                    t.EntranceOverride = (int)EntranceStyle.Cascade;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset, "Custom themes come from an asset.");
            }
            return t;
        }
    }
}
