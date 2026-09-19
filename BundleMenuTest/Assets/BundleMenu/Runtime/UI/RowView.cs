using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>
    /// One row. Hierarchy (each level has exactly one job):
    ///
    ///   Row        LayoutElement - owned by the VerticalLayoutGroup
    ///   └ Visual   CanvasGroup   - owned by the entrance animation (offset / scale / alpha)
    ///     └ Body   MenuButton    - owned by hover / press feedback (scale, colours)
    ///       ├ Progress, Edge, Light, Label, Value, Chevron
    ///       └ Secondary          - optional small button (e.g. open details)
    /// </summary>
    public sealed class RowView : MonoBehaviour
    {
        public RectTransform Visual { get; private set; }
        public CanvasGroup Group { get; private set; }
        public MenuButton Button { get; private set; }
        public AnimatedItem Animated { get; private set; }
        public string Key { get; private set; }

        private MenuTheme theme;
        private Image statusLight, progress, chevron;
        private TextMeshProUGUI label, value;
        private MenuButton secondary;
        private RowSpec spec;

        public static RowView Create(Transform parent, MenuTheme theme, float height, int index)
        {
            var root = UIFactory.Rect($"Row{index}", parent);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = height;
            var view = root.gameObject.AddComponent<RowView>();
            view.Build(theme, height);
            return view;
        }

        private void Build(MenuTheme t, float height)
        {
            theme = t;
            Visual = UIFactory.Rect("Visual", transform).Stretch();
            Group = Visual.gameObject.AddComponent<CanvasGroup>();
            Animated = new AnimatedItem(Visual, Group);

            float r = Mathf.Min(t.ButtonRadius, height * 0.5f);
            var fill = UIFactory.Image(Visual, "Body", UISprites.RoundedFill(r), t.ButtonFill, raycast: true);
            fill.rectTransform.Stretch();
            var body = fill.transform;

            progress = UIFactory.Image(body, "Progress", UISprites.RoundedFill(r), t.Accent.WithAlpha(0.22f));
            progress.rectTransform.anchorMin = Vector2.zero;
            progress.rectTransform.anchorMax = new Vector2(0f, 1f);
            progress.rectTransform.offsetMin = progress.rectTransform.offsetMax = Vector2.zero;

            var edge = UIFactory.Image(body, "Edge", UISprites.RoundedEdge(r, t.ButtonEdgeWidth), t.ButtonEdge);
            edge.rectTransform.Stretch();

            statusLight = UIFactory.Image(body, "Light", UISprites.Glyph(Icon.Dot), t.StatusIdle);
            statusLight.rectTransform.Pin(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(22, 0), new Vector2(11, 11));

            label = UIFactory.Text(body, "Label", t, 21, TextAlignmentOptions.MidlineLeft, t.Text, t.LabelStyle, t.LabelSpacing);
            value = UIFactory.Text(body, "Value", t, 15, TextAlignmentOptions.MidlineRight, t.SubText, t.LabelStyle, t.LabelSpacing * 0.5f);

            chevron = UIFactory.Image(body, "Chevron", UISprites.Glyph(Icon.ChevronRight), t.SubText);
            chevron.rectTransform.Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(18, 18));

            secondary = UIFactory.IconButton(body, "Secondary", t, Icon.ChevronRight, height - 14, Mathf.Max(2f, r - 4f));
            secondary.GetComponent<RectTransform>().Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-7, 0), new Vector2(height - 14, height - 14));

            Button = fill.gameObject.AddComponent<MenuButton>();
            Button.Init(t, fill, edge);
        }

        public void Apply(RowSpec s)
        {
            spec = s;
            Key = s.Key;
            gameObject.name = "Row: " + s.Label;

            label.text = s.Label ?? "";
            label.fontSize = s.Multiline ? 15f : 21f;
            label.enableWordWrapping = s.Multiline;
            label.lineSpacing = s.Multiline ? -12f : 0f;
            value.text = s.Value ?? "";
            value.gameObject.SetActive(!string.IsNullOrEmpty(s.Value));

            bool hasLight = s.Light != StatusLight.None;
            statusLight.gameObject.SetActive(hasLight);
            statusLight.color = LightColor(s.Light);
            statusLight.rectTransform.localScale = Vector3.one;

            bool hasSecondary = s.OnSecondary != null;
            secondary.gameObject.SetActive(hasSecondary);
            if (hasSecondary)
            {
                secondary.transform.Find("Icon").GetComponent<Image>().sprite = UISprites.Glyph(s.SecondaryIcon);
                secondary.OnClick = s.OnSecondary;
            }
            chevron.gameObject.SetActive(s.ShowChevron && !hasSecondary);

            float leftInset = hasLight ? 40f : 20f;
            float rightInset = hasSecondary ? 70f : s.ShowChevron ? 38f : 18f;
            label.rectTransform.Stretch(leftInset, 0, rightInset, 0);
            value.rectTransform.Stretch(leftInset, 0, rightInset, 0);
            // Keep the label from running under the value text.
            label.margin = new Vector4(0, 0, string.IsNullOrEmpty(s.Value) ? 0 : Mathf.Min(150f, value.GetPreferredValues(s.Value).x + 12f), 0);

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
