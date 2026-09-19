using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>
    /// One row. Hierarchy (each level has exactly one job):
    ///
    ///   Row        LayoutElement - owned by the layout group
    ///   └ Visual   CanvasGroup   - owned by the entrance animation (offset / scale / alpha)
    ///     ├ Lip                  - Tile look only: the darker "thickness" under a raised tile
    ///     └ Body   MenuButton    - owned by hover / press feedback (scale, colours)
    ///       ├ Progress, Edge, Light, Label, Value, Chevron
    ///       ├ Mark               - Box / Switch looks: tick box or on/off square (see RowMark)
    ///       └ Secondary          - optional small button (e.g. open details)
    /// </summary>
    public sealed class RowView : MonoBehaviour
    {
        public RectTransform Visual { get; private set; }
        public CanvasGroup Group { get; private set; }
        public MenuButton Button { get; private set; }
        public AnimatedItem Animated { get; private set; }
        public string Key { get; private set; }
        public RowLook Look { get; private set; }

        private MenuTheme theme;
        private Image statusLight, progress, chevron, underline;
        private TextMeshProUGUI label, value;
        private MenuButton secondary;
        private RowMark mark;
        private RowSpec spec;
        private float height;
        private bool compact;

        public static RowView Create(Transform parent, MenuTheme theme, float height, int index) =>
            Create(parent, theme, height, index, RowLook.Button, theme.Layout == ThemeLayout.Grid);

        public static RowView Create(Transform parent, MenuTheme theme, float height, int index, RowLook look, bool compact)
        {
            var root = UIFactory.Rect($"Row{index}", parent);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = height;
            var view = root.gameObject.AddComponent<RowView>();
            view.Build(theme, height, look, compact);
            return view;
        }

        private void Build(MenuTheme t, float h, RowLook look, bool isCompact)
        {
            theme = t;
            height = h;
            Look = look;
            compact = isCompact;
            Visual = UIFactory.Rect("Visual", transform).Stretch();
            Group = Visual.gameObject.AddComponent<CanvasGroup>();
            Animated = new AnimatedItem(Visual, Group);

            float r = Mathf.Min(t.ButtonRadius, h * 0.5f);

            // Every boxed look stands on a darker base peeking out underneath; pressing sinks the row onto it.
            // Tiles are chunkier. Borderless looks (Box, Switch) stay flat on the panel.
            float depth = look == RowLook.Tile ? Mathf.Max(5f, t.ButtonDepth) : look == RowLook.Box || look == RowLook.Switch ? 0f : t.ButtonDepth;
            if (depth > 0f) Bevel.Base(Visual, t, r, depth);

            var fill = UIFactory.Image(Visual, "Body", UISprites.RoundedFill(r), t.ButtonFill, raycast: true);
            fill.rectTransform.Stretch();
            var body = fill.transform;

            progress = UIFactory.Image(body, "Progress", UISprites.RoundedFill(r), t.Accent.WithAlpha(0.22f));
            progress.rectTransform.anchorMin = Vector2.zero;
            progress.rectTransform.anchorMax = new Vector2(0f, 1f);
            progress.rectTransform.offsetMin = progress.rectTransform.offsetMax = Vector2.zero;

            float edgeWidth = look == RowLook.Plank ? Mathf.Max(2f, t.ButtonEdgeWidth) : t.ButtonEdgeWidth;
            var edge = UIFactory.Image(body, "Edge", UISprites.RoundedEdge(r, edgeWidth), t.ButtonEdge);
            edge.rectTransform.Stretch();

            if (look == RowLook.Card)
            {
                underline = UIFactory.Image(body, "Underline", UISprites.RoundedFill(1f), Color.white);
                underline.rectTransform.Pin(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 1), new Vector2(0, 2));
                underline.rectTransform.anchorMin = new Vector2(0.06f, 0f);
                underline.rectTransform.anchorMax = new Vector2(0.94f, 0f);
                underline.rectTransform.sizeDelta = new Vector2(0, 2);
                underline.color = t.Accent.WithAlpha(0.45f);
            }
            if (look == RowLook.Plank)
            {
                // Two nail heads, one at each end of the board.
                foreach (float side in new[] { 0f, 1f })
                {
                    var nail = UIFactory.Image(body, "Nail", UISprites.Glyph(Icon.Dot), Darken(t.ButtonEdge, 0.7f).WithAlpha(0.8f));
                    nail.rectTransform.Pin(new Vector2(side, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(side == 0f ? 11f : -11f, 0), new Vector2(7, 7));
                }
            }

            statusLight = UIFactory.Image(body, "Light", UISprites.Glyph(Icon.Dot), t.StatusIdle);
            statusLight.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(22, 0), new Vector2(11, 11));

            var align = look == RowLook.Plank || (look == RowLook.Tile && compact) ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            label = UIFactory.Text(body, "Label", t, t.LabelSize, align, t.Text, t.LabelStyle, t.LabelSpacing);
            // Planks are pale boards: the theme's sub-text colour is meant for the panel, so use faded ink instead.
            var valueColor = look == RowLook.Plank ? t.Text.WithAlpha(0.6f) : t.SubText;
            value = UIFactory.Text(body, "Value", t, t.ValueSize, TextAlignmentOptions.MidlineRight, valueColor, t.LabelStyle, t.LabelSpacing * 0.5f);
            if (compact) label.enableAutoSizing = true; // tiles are narrower
            label.fontSizeMin = 11f;
            label.fontSizeMax = t.LabelSize;

            chevron = UIFactory.Image(body, "Chevron", UISprites.Glyph(Icon.ChevronRight), t.SubText);
            chevron.rectTransform.Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(look == RowLook.Plank ? -22 : -12, 0), new Vector2(18, 18));
            if (look == RowLook.Plank) chevron.color = valueColor;

            if (look == RowLook.Box || look == RowLook.Switch) mark = RowMark.Create(body, t, look, h);

            secondary = UIFactory.IconButton(body, "Secondary", t, Icon.ChevronRight, h - 14, Mathf.Max(2f, r - 4f));
            secondary.GetComponent<RectTransform>().Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-7, 0), new Vector2(h - 14, h - 14));

            Button = fill.gameObject.AddComponent<MenuButton>();
            // Borderless looks: the row itself is almost invisible until hovered; the mark carries the state.
            Button.FillAlpha = look == RowLook.Box ? 0.3f : look == RowLook.Switch ? 0.15f : 1f;
            Button.PressDepth = depth;
            Button.Init(t, fill, edge);
            if (depth > 0f)
            {
                Bevel.Face(body, t, r);
                Bevel.Block((RectTransform)body, t, r, depth);
            }
        }

        public void Apply(RowSpec s)
        {
            spec = s;
            Key = s.Key;
            gameObject.name = "Row: " + s.Label;

            label.text = s.Label ?? "";
            if (!label.enableAutoSizing) label.fontSize = s.Multiline ? 15f : theme.LabelSize;
            label.enableWordWrapping = s.Multiline;
            label.lineSpacing = s.Multiline ? -12f : 0f;
            value.text = s.Value ?? "";
            value.gameObject.SetActive(!string.IsNullOrEmpty(s.Value));

            bool hasSecondary = s.OnSecondary != null;
            secondary.gameObject.SetActive(hasSecondary);
            if (hasSecondary)
            {
                secondary.transform.Find("Icon").GetComponent<Image>().sprite = UISprites.Glyph(s.SecondaryIcon);
                secondary.OnClick = s.OnSecondary;
            }

            // Cards always hint that anything clickable leads somewhere.
            bool wantsChevron = Look == RowLook.Card ? s.Interactable && (s.ShowChevron || s.OnClick != null) : s.ShowChevron;
            bool markShown = mark != null && mark.Apply(s, hasSecondary);
            chevron.gameObject.SetActive(wantsChevron && !hasSecondary && !(markShown && Look == RowLook.Switch));

            bool hasLight = s.Light != StatusLight.None && !(markShown && Look == RowLook.Box);
            statusLight.gameObject.SetActive(hasLight);
            statusLight.color = LightColor(s.Light);
            statusLight.rectTransform.localScale = Vector3.one;
            if (underline != null) underline.gameObject.SetActive(s.Interactable);

            bool plank = Look == RowLook.Plank;
            float leftInset = markShown && Look == RowLook.Box ? height + 4f : hasLight ? (plank ? 48f : 40f) : plank ? 26f : 20f;
            if (hasLight && markShown && Look == RowLook.Box) leftInset += 22f;
            statusLight.rectTransform.anchoredPosition = new Vector2(markShown && Look == RowLook.Box ? height + 4f : plank ? 30f : 22f, 0f);

            float rightInset = hasSecondary ? 70f
                : markShown && Look == RowLook.Switch ? height + 6f
                : chevron.gameObject.activeSelf ? (plank ? 46f : 38f)
                : Look == RowLook.Plank ? 26f : 18f;
            label.rectTransform.Stretch(leftInset, 0, rightInset, 0);
            value.rectTransform.Stretch(leftInset, 0, rightInset, 0);
            // Keep the label from running under the value text.
            float valueWidth = string.IsNullOrEmpty(s.Value) ? 0 : Mathf.Min(compact ? 70f : 150f, value.GetPreferredValues(s.Value).x + 12f);
            bool centred = label.alignment == TextAlignmentOptions.Center;
            label.margin = new Vector4(centred ? valueWidth : 0, 0, valueWidth, 0);

            bool showProgress = s.Progress >= 0f && s.Progress < 1f;
            progress.gameObject.SetActive(showProgress);
            if (showProgress) progress.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(s.Progress), 1f);

            Button.IsOn = s.IsOn;
            Button.interactable = s.Interactable;
            Button.OnClick = s.OnClick;
            Button.OnAltClick = s.OnAltClick;
            label.color = s.Interactable ? theme.Text : theme.SubText;
        }

        private Color LightColor(StatusLight l)
        {
            switch (l)
            {
                case StatusLight.Busy:  return theme.StatusBusy;
                case StatusLight.Ok:    return theme.StatusOk;
                case StatusLight.Error: return theme.StatusError;
                default:                return theme.StatusIdle;
            }
        }

        internal static Color Darken(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, Mathf.Max(c.a, 0.9f));

        private void Update()
        {
            // Busy lights breathe so a loading row is obvious even at a glance.
            if (spec == null || spec.Light != StatusLight.Busy || !statusLight.gameObject.activeSelf) return;
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 9f);
            statusLight.color = theme.StatusBusy.WithAlpha(pulse);
            float s = 1f + 0.25f * pulse;
            statusLight.rectTransform.localScale = new Vector3(s, s, 1f);
        }
    }
}
