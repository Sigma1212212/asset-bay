using System;
using TMPro;
using UnityEngine;

namespace BundleMenu
{
    // New presets go after Custom so the numbers already saved in players' settings keep meaning the same theme.
    public enum ThemePreset { Halo, Solstice, Circuit, Velvet, Custom, Arcade, Glass, Minimal, Neon, Timber, Parchment, Lava, Frost, Candy, Forest, Retro, Abyss }

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

        [Header("Shape")]
        [Tooltip("Menu type used when the player's Menu type setting is 'Match theme'.")]
        public MenuStyle Style = MenuStyle.Classic;
        public PanelPattern Pattern = PanelPattern.None;
        [Tooltip("Where each piece goes when Style is Custom (made in the launcher's Designer).")]
        public CustomLayoutData Custom;
        [Tooltip("Panel thickness in UI units: the visible edge under the panel, and the 3D slab behind it in the world.")]
        [Range(0, 32)] public float Depth = 12f;
        [Tooltip("How far buttons stand off their base; they travel down this far when pressed.")]
        [Range(0, 10)] public float ButtonDepth = 6f;
        [Tooltip("How strongly buttons are lit like physical keys (top light, bottom shade, edge highlight). 0 = flat.")]
        [Range(0, 1)] public float Bevel = 1f;
        [Tooltip("Light falling on the top of each key.")]
        [Range(0, 2)] public float BevelLight = 1f;
        [Tooltip("Shade under each key.")]
        [Range(0, 2)] public float BevelShadow = 1f;
        public Color PatternColor = new Color(1, 1, 1, 0.06f);

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
        /// <summary>The theme the menu is wearing right now, so a reload never destroys it under itself.</summary>
        public static MenuTheme InUse;

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
                    t.EdgeBottom = MenuTheme.Hex(0x2BB8D0); t.GlowStrength = 0f; t.Accent2 = MenuTheme.Hex(0x2BB8D0);
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
                    t.Style = MenuStyle.Switchboard;
                    t.GlowStrength = 0f; t.Depth = 14f; t.ButtonDepth = 7f;
                    break;

                // Oscilloscope: black glass, phosphor green, square corners, outlined buttons, spaced caps.
                case ThemePreset.Circuit:
                    t.DisplayName = "Circuit";
                    t.PanelTop = MenuTheme.Hex(0x0A120E); t.PanelBottom = MenuTheme.Hex(0x040806);
                    t.EdgeTop = MenuTheme.Hex(0x39FF88); t.EdgeBottom = MenuTheme.Hex(0x00B894);
                    t.EdgeWidth = 1.5f; t.PanelRadius = 14f; t.GlowStrength = 0.45f;
                    t.Accent = MenuTheme.Hex(0x39FF88); t.Accent2 = MenuTheme.Hex(0x00C2A8);
                    t.ButtonRadius = 8f;
                    t.ButtonFill = MenuTheme.Hex(0x0F1F17, 0.95f);
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
                    t.Style = MenuStyle.Console; t.Pattern = PanelPattern.Scanlines; t.PatternColor = MenuTheme.Hex(0x39FF88, 0.05f);
                    t.GlowStrength = 0f; t.Depth = 10f; t.ButtonDepth = 5f;
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
                    t.Style = MenuStyle.Pillars;
                    t.GlowStrength = 0f; t.Depth = 12f;
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
                    t.GlowStrength = 0f; t.Depth = 18f; t.ButtonDepth = 6f;
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
                    t.Style = MenuStyle.Cards;
                    t.GlowStrength = 0f; t.Depth = 3f; t.ButtonDepth = 2f; t.Bevel = 0.4f;
                    break;

                // Minimal: no boxes, just text rows with a thin accent line; hovering slides the row.
                case ThemePreset.Minimal:
                    t.DisplayName = "Minimal";
                    t.PanelTop = MenuTheme.Hex(0x111214); t.PanelBottom = MenuTheme.Hex(0x111214);
                    t.EdgeWidth = 0f; t.PanelRadius = 18f; t.GlowStrength = 0f;
                    t.Accent = MenuTheme.Hex(0xF2F2F2); t.Accent2 = MenuTheme.Hex(0x8A8A8A);
                    t.ButtonRadius = 10f;
                    t.ButtonFill = MenuTheme.Hex(0xFFFFFF, 0f); t.ButtonFillHover = MenuTheme.Hex(0xFFFFFF, 0.05f);
                    t.ButtonFillPressed = MenuTheme.Hex(0xFFFFFF, 0.1f);
                    t.ButtonEdge = MenuTheme.Hex(0xFFFFFF, 0f); t.ButtonEdgeHover = MenuTheme.Hex(0xFFFFFF, 0f);
                    t.Text = MenuTheme.Hex(0xEDEDED); t.SubText = MenuTheme.Hex(0x7A7A7A);
                    t.Hover = HoverStyle.Slide;
                    t.RowHeight = 44f; t.RowSpacing = 2f; t.LabelSize = 19f; t.ValueSize = 14f;
                    t.EntranceOverride = (int)EntranceStyle.SlideRight;
                    t.StatusIdle = MenuTheme.Hex(0x3A3A3A);
                    t.Style = MenuStyle.Checklist;
                    t.Depth = 0f; t.ButtonDepth = 0f; t.Bevel = 0f;
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
                    t.Style = MenuStyle.Pillars; t.Pattern = PanelPattern.Grid; t.PatternColor = MenuTheme.Hex(0x22E4FF, 0.05f);
                    t.Depth = 8f; t.ButtonDepth = 4f; t.Bevel = 0.5f;
                    break;

                // Timber: a carved wooden sign. Wood grain, pale planks, rope-hung. Pairs with Signboard.
                case ThemePreset.Timber:
                    t.DisplayName = "Timber";
                    t.PanelTop = MenuTheme.Hex(0x7A4E2D); t.PanelBottom = MenuTheme.Hex(0x4E3019);
                    t.EdgeTop = MenuTheme.Hex(0x3A2211); t.EdgeBottom = MenuTheme.Hex(0x24150A);
                    t.EdgeWidth = 5f; t.PanelRadius = 18f; t.GlowStrength = 0f;
                    t.Accent = MenuTheme.Hex(0xF4C95D); t.Accent2 = MenuTheme.Hex(0xE08A3C);
                    t.ButtonRadius = 12f;
                    t.ButtonFill = MenuTheme.Hex(0xD9C3A0); t.ButtonFillHover = MenuTheme.Hex(0xEBD8B6);
                    t.ButtonFillPressed = MenuTheme.Hex(0xC4A87F);
                    t.ButtonEdge = MenuTheme.Hex(0x3A2211, 0.6f); t.ButtonEdgeHover = MenuTheme.Hex(0xF4C95D);
                    t.ButtonEdgeWidth = 2f;
                    t.Text = MenuTheme.Hex(0x2E1C0E); t.SubText = MenuTheme.Hex(0xF1DEC0);
                    t.LabelStyle = FontStyles.UpperCase | FontStyles.Bold; t.LabelSpacing = 3f;
                    t.TitleStyle = FontStyles.UpperCase | FontStyles.Bold; t.TitleSpacing = 6f;
                    t.StatusIdle = MenuTheme.Hex(0x8C6A4A); t.StatusBusy = MenuTheme.Hex(0xE0A030);
                    t.StatusOk = MenuTheme.Hex(0x4C9A2A); t.StatusError = MenuTheme.Hex(0xC0392B);
                    t.Hover = HoverStyle.Grow; t.RowHeight = 50f; t.LabelSize = 18f;
                    t.EntranceOverride = (int)EntranceStyle.Pop;
                    t.Style = MenuStyle.Signboard; t.Pattern = PanelPattern.Wood; t.PatternColor = MenuTheme.Hex(0x1E1008, 0.35f);
                    t.Depth = 22f; t.ButtonDepth = 7f;
                    break;

                // Parchment: an old field journal. Cream paper, ink text, leather spine. Pairs with Book.
                case ThemePreset.Parchment:
                    t.DisplayName = "Parchment";
                    t.PanelTop = MenuTheme.Hex(0xF3E7C9); t.PanelBottom = MenuTheme.Hex(0xE6D3A8);
                    t.EdgeTop = MenuTheme.Hex(0x6B3F22); t.EdgeBottom = MenuTheme.Hex(0x4A2A15);
                    t.EdgeWidth = 6f; t.PanelRadius = 16f; t.GlowStrength = 0.15f;
                    t.Accent = MenuTheme.Hex(0x8C2F1B); t.Accent2 = MenuTheme.Hex(0x2F5D8C);
                    t.ButtonRadius = 10f;
                    t.ButtonFill = MenuTheme.Hex(0xFFF8E6, 0.95f); t.ButtonFillHover = MenuTheme.Hex(0xFFFDF4);
                    t.ButtonFillPressed = MenuTheme.Hex(0xE9D9B4);
                    t.ButtonEdge = MenuTheme.Hex(0x6B3F22, 0.35f); t.ButtonEdgeHover = MenuTheme.Hex(0x8C2F1B);
                    t.ButtonEdgeWidth = 1.5f;
                    t.Text = MenuTheme.Hex(0x2B1D12); t.SubText = MenuTheme.Hex(0x7D6247);
                    t.LabelStyle = FontStyles.SmallCaps; t.LabelSpacing = 1.5f;
                    t.TitleStyle = FontStyles.SmallCaps | FontStyles.Bold | FontStyles.Italic; t.TitleSpacing = 2f;
                    t.StatusIdle = MenuTheme.Hex(0xB9A27E); t.StatusBusy = MenuTheme.Hex(0xC98A2E);
                    t.StatusOk = MenuTheme.Hex(0x3E7D3A); t.StatusError = MenuTheme.Hex(0xA8322D);
                    t.RowHeight = 52f; t.LabelSize = 19f;
                    t.EntranceOverride = (int)EntranceStyle.Fade;
                    t.Style = MenuStyle.Book; t.Pattern = PanelPattern.Paper; t.PatternColor = MenuTheme.Hex(0x6B3F22, 0.5f);
                    t.GlowStrength = 0f; t.Depth = 16f; t.ButtonDepth = 5f;
                    break;

                // Lava: cooled black rock with glowing orange cracks. Tall pillars, heavy keys.
                case ThemePreset.Lava:
                    t.DisplayName = "Lava";
                    t.PanelTop = MenuTheme.Hex(0x221410); t.PanelBottom = MenuTheme.Hex(0x0E0806);
                    t.EdgeTop = MenuTheme.Hex(0xFF7A1A); t.EdgeBottom = MenuTheme.Hex(0xC2270E);
                    t.EdgeWidth = 3f; t.PanelRadius = 20f; t.GlowStrength = 0.5f; t.Flicker = 0.15f;
                    t.Accent = MenuTheme.Hex(0xFF7A1A); t.Accent2 = MenuTheme.Hex(0xFF3B1F);
                    t.ButtonRadius = 12f;
                    t.ButtonFill = MenuTheme.Hex(0x3A2119); t.ButtonFillHover = MenuTheme.Hex(0x55291A);
                    t.ButtonFillPressed = MenuTheme.Hex(0x7A3417);
                    t.ButtonEdge = MenuTheme.Hex(0xFF7A1A, 0.35f); t.ButtonEdgeHover = MenuTheme.Hex(0xFFB05A);
                    t.ButtonEdgeWidth = 2f;
                    t.Text = MenuTheme.Hex(0xFFE9D6); t.SubText = MenuTheme.Hex(0xC08A6E);
                    t.LabelStyle = FontStyles.Bold; t.TitleStyle = FontStyles.Bold | FontStyles.UpperCase; t.TitleSpacing = 4f;
                    t.StatusIdle = MenuTheme.Hex(0x5A3A2E); t.StatusBusy = MenuTheme.Hex(0xFFB547);
                    t.StatusOk = MenuTheme.Hex(0x9BE36B); t.StatusError = MenuTheme.Hex(0xFF3B1F);
                    t.Depth = 16f; t.ButtonDepth = 7f; t.Style = MenuStyle.Pillars; t.EntranceOverride = (int)EntranceStyle.Pop;
                    break;

                // Frost: bright ice. White-blue panel, crisp navy text, cards with soft keys.
                case ThemePreset.Frost:
                    t.DisplayName = "Frost";
                    t.PanelTop = MenuTheme.Hex(0xF4FAFF); t.PanelBottom = MenuTheme.Hex(0xDCEBF7);
                    t.EdgeTop = MenuTheme.Hex(0xA9D6F5); t.EdgeBottom = MenuTheme.Hex(0x7FB7E0);
                    t.EdgeWidth = 2f; t.PanelRadius = 24f; t.GlowStrength = 0f;
                    t.Accent = MenuTheme.Hex(0x2F8FD8); t.Accent2 = MenuTheme.Hex(0x5CC6F2);
                    t.ButtonRadius = 14f;
                    t.ButtonFill = MenuTheme.Hex(0xFFFFFF); t.ButtonFillHover = MenuTheme.Hex(0xEEF7FF);
                    t.ButtonFillPressed = MenuTheme.Hex(0xCFE6F7);
                    t.ButtonEdge = MenuTheme.Hex(0xB9D9F0); t.ButtonEdgeHover = MenuTheme.Hex(0x2F8FD8);
                    t.ButtonEdgeWidth = 1.5f;
                    t.Text = MenuTheme.Hex(0x16324A); t.SubText = MenuTheme.Hex(0x6B8BA6);
                    t.StatusIdle = MenuTheme.Hex(0xB8CCDD); t.StatusBusy = MenuTheme.Hex(0xE8A33A);
                    t.StatusOk = MenuTheme.Hex(0x2DB37A); t.StatusError = MenuTheme.Hex(0xE0455A);
                    t.Depth = 12f; t.ButtonDepth = 5f; t.Bevel = 0.7f; t.Style = MenuStyle.Cards; t.EntranceOverride = (int)EntranceStyle.Fade;
                    break;

                // Candy: pastel pink and mint, big round shapes, bouncy.
                case ThemePreset.Candy:
                    t.DisplayName = "Candy";
                    t.PanelTop = MenuTheme.Hex(0xFFE3F1); t.PanelBottom = MenuTheme.Hex(0xFFCFE6);
                    t.EdgeTop = MenuTheme.Hex(0xFF8FC7); t.EdgeBottom = MenuTheme.Hex(0xC98BFF);
                    t.EdgeWidth = 4f; t.PanelRadius = 32f; t.GlowStrength = 0f;
                    t.Accent = MenuTheme.Hex(0xFF5FA8); t.Accent2 = MenuTheme.Hex(0x7FE3C4);
                    t.ButtonRadius = 22f;
                    t.ButtonFill = MenuTheme.Hex(0xFFFFFF); t.ButtonFillHover = MenuTheme.Hex(0xFFF2F8);
                    t.ButtonFillPressed = MenuTheme.Hex(0xFFD6EA);
                    t.ButtonEdge = MenuTheme.Hex(0xFFB3D6); t.ButtonEdgeHover = MenuTheme.Hex(0xFF5FA8);
                    t.ButtonEdgeWidth = 2.5f;
                    t.Text = MenuTheme.Hex(0x5A2A4A); t.SubText = MenuTheme.Hex(0xB07A9A);
                    t.LabelStyle = FontStyles.Bold; t.TitleStyle = FontStyles.Bold;
                    t.StatusIdle = MenuTheme.Hex(0xE7B7CF); t.StatusBusy = MenuTheme.Hex(0xFFB547);
                    t.StatusOk = MenuTheme.Hex(0x3CCB9A); t.StatusError = MenuTheme.Hex(0xFF4F7B);
                    t.Depth = 14f; t.ButtonDepth = 6f; t.Style = MenuStyle.Switchboard; t.EntranceOverride = (int)EntranceStyle.Pop;
                    break;

                // Forest: mossy green board on a wooden frame, leafy accents.
                case ThemePreset.Forest:
                    t.DisplayName = "Forest";
                    t.PanelTop = MenuTheme.Hex(0x1E3322); t.PanelBottom = MenuTheme.Hex(0x122017);
                    t.EdgeTop = MenuTheme.Hex(0x6B4A2B); t.EdgeBottom = MenuTheme.Hex(0x4A3219);
                    t.EdgeWidth = 5f; t.PanelRadius = 18f; t.GlowStrength = 0f;
                    t.Accent = MenuTheme.Hex(0x8FD16A); t.Accent2 = MenuTheme.Hex(0xE3C45A);
                    t.ButtonRadius = 12f;
                    t.ButtonFill = MenuTheme.Hex(0x2C4A31); t.ButtonFillHover = MenuTheme.Hex(0x375C3D);
                    t.ButtonFillPressed = MenuTheme.Hex(0x44704A);
                    t.ButtonEdge = MenuTheme.Hex(0x8FD16A, 0.25f); t.ButtonEdgeHover = MenuTheme.Hex(0x8FD16A);
                    t.Text = MenuTheme.Hex(0xEAF5DF); t.SubText = MenuTheme.Hex(0x9DB58E);
                    t.StatusIdle = MenuTheme.Hex(0x3D5A42); t.StatusBusy = MenuTheme.Hex(0xE3C45A);
                    t.StatusOk = MenuTheme.Hex(0x8FD16A); t.StatusError = MenuTheme.Hex(0xE0664F);
                    t.Pattern = PanelPattern.Wood; t.PatternColor = MenuTheme.Hex(0x000000, 0.12f);
                    t.Depth = 16f; t.ButtonDepth = 6f; t.Style = MenuStyle.Checklist; t.EntranceOverride = (int)EntranceStyle.Cascade;
                    break;

                // Retro: a beige 80s computer. Chunky keys, brown text, scanlines, console layout.
                case ThemePreset.Retro:
                    t.DisplayName = "Retro";
                    t.PanelTop = MenuTheme.Hex(0xE8DDC4); t.PanelBottom = MenuTheme.Hex(0xD4C6A6);
                    t.EdgeTop = MenuTheme.Hex(0x9C8C6A); t.EdgeBottom = MenuTheme.Hex(0x7A6B4E);
                    t.EdgeWidth = 3f; t.PanelRadius = 14f; t.GlowStrength = 0f;
                    t.Accent = MenuTheme.Hex(0xD9621E); t.Accent2 = MenuTheme.Hex(0x3C7A8C);
                    t.ButtonRadius = 8f;
                    t.ButtonFill = MenuTheme.Hex(0xF4EEDF); t.ButtonFillHover = MenuTheme.Hex(0xFFF9EC);
                    t.ButtonFillPressed = MenuTheme.Hex(0xDCD0B4);
                    t.ButtonEdge = MenuTheme.Hex(0x9C8C6A, 0.7f); t.ButtonEdgeHover = MenuTheme.Hex(0xD9621E);
                    t.ButtonEdgeWidth = 2f;
                    t.Text = MenuTheme.Hex(0x3A2F22); t.SubText = MenuTheme.Hex(0x7F705A);
                    t.LabelStyle = FontStyles.UpperCase | FontStyles.Bold; t.LabelSpacing = 3f;
                    t.TitleStyle = FontStyles.UpperCase | FontStyles.Bold; t.TitleSpacing = 8f;
                    t.StatusIdle = MenuTheme.Hex(0xB7A988); t.StatusBusy = MenuTheme.Hex(0xD9A21E);
                    t.StatusOk = MenuTheme.Hex(0x4C9A2A); t.StatusError = MenuTheme.Hex(0xC0392B);
                    t.Pattern = PanelPattern.Scanlines; t.PatternColor = MenuTheme.Hex(0x000000, 0.035f);
                    t.Depth = 18f; t.ButtonDepth = 8f; t.Style = MenuStyle.Console; t.EntranceOverride = (int)EntranceStyle.Staggered;
                    break;

                // Abyss: deep sea. Midnight teal, bioluminescent cyan, a book of the deep.
                case ThemePreset.Abyss:
                    t.DisplayName = "Abyss";
                    t.PanelTop = MenuTheme.Hex(0x0B2A33); t.PanelBottom = MenuTheme.Hex(0x04141A);
                    t.EdgeTop = MenuTheme.Hex(0x1FB5C9); t.EdgeBottom = MenuTheme.Hex(0x0B5E6E);
                    t.EdgeWidth = 2.5f; t.PanelRadius = 22f; t.GlowStrength = 0f;
                    t.Accent = MenuTheme.Hex(0x3FF0E0); t.Accent2 = MenuTheme.Hex(0x7A8CFF);
                    t.ButtonRadius = 14f;
                    t.ButtonFill = MenuTheme.Hex(0x0F3A45); t.ButtonFillHover = MenuTheme.Hex(0x145060);
                    t.ButtonFillPressed = MenuTheme.Hex(0x1B6678);
                    t.ButtonEdge = MenuTheme.Hex(0x3FF0E0, 0.25f); t.ButtonEdgeHover = MenuTheme.Hex(0x3FF0E0);
                    t.Text = MenuTheme.Hex(0xDDFBF8); t.SubText = MenuTheme.Hex(0x7FB3B8);
                    t.StatusIdle = MenuTheme.Hex(0x1E4C56); t.StatusBusy = MenuTheme.Hex(0xF2C14E);
                    t.StatusOk = MenuTheme.Hex(0x3FF0E0); t.StatusError = MenuTheme.Hex(0xFF6B8A);
                    t.Depth = 14f; t.ButtonDepth = 6f; t.Style = MenuStyle.Classic; t.EntranceOverride = (int)EntranceStyle.Cascade;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset, "Custom themes come from an asset.");
            }
            return t;
        }
    }
}
