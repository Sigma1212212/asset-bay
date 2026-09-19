using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>
    /// Builds and owns the panel's GameObjects. Pure presentation: it renders RowSpecs, exposes
    /// its buttons, and knows nothing about bundles, pages or placement.
    ///
    ///   Panel (CanvasGroup)
    ///   ├ Glow, Background (gradient), Rim (gradient outline)
    ///   ├ Header: Back · Brand · Title · Settings · Close
    ///   ├ AccentLine
    ///   ├ Rows (VerticalLayoutGroup) → RowView × RowsPerPage
    ///   ├ Footer: Prev · PageLabel · Next
    ///   └ Status
    /// </summary>
    public sealed class MenuView : MonoBehaviour
    {
        public const float Width = 440f;
        public const float RowHeight = 56f;
        public const float RowSpacing = 8f;
        private const float HeaderHeight = 98f;
        private const float Pad = 20f;

        public RectTransform Panel { get; private set; }
        public CanvasGroup PanelGroup { get; private set; }
        public RectTransform AccentLine { get; private set; }
        public MenuButton BackButton { get; private set; }
        public MenuButton SettingsButton { get; private set; }
        public MenuButton CloseButton { get; private set; }
        public MenuButton PrevButton { get; private set; }
        public MenuButton NextButton { get; private set; }
        public float Height { get; private set; }
        public MenuTheme Theme { get; private set; }

        private readonly List<RowView> rows = new List<RowView>();
        private TextMeshProUGUI brand, title, pageLabel, status;
        private float statusUntil;

        public IReadOnlyList<RowView> Rows => rows;

        /// <summary>(Re)build everything for a theme. Existing children are destroyed.</summary>
        public void Build(MenuTheme theme, int rowsPerPage, string brandText)
        {
            Theme = theme;
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            rows.Clear();

            float rowsHeight = rowsPerPage * RowHeight + (rowsPerPage - 1) * RowSpacing;
            float rowsTop = HeaderHeight + 18f;
            float footerTop = rowsTop + rowsHeight + 14f;
            float statusTop = footerTop + 52f + 8f;
            Height = statusTop + 22f + 14f;

            Panel = UIFactory.Rect("Panel", transform);
            Panel.sizeDelta = new Vector2(Width, Height);
            PanelGroup = Panel.gameObject.AddComponent<CanvasGroup>();

            // --- shell
            const float glowSoft = 36f;
            if (theme.GlowStrength > 0f)
            {
                var glow = UIFactory.Image(Panel, "Glow", UISprites.RoundedGlow(theme.PanelRadius, glowSoft), Color.white);
                glow.rectTransform.Stretch(-glowSoft, -glowSoft, -glowSoft, -glowSoft);
                glow.gameObject.AddComponent<UIGradient>().Set(theme.EdgeTop.WithAlpha(theme.GlowStrength),
                                                               theme.EdgeBottom.WithAlpha(theme.GlowStrength));
            }

            var bg = UIFactory.Image(Panel, "Background", UISprites.RoundedFill(theme.PanelRadius), Color.white, raycast: true);
            bg.rectTransform.Stretch();
            bg.gameObject.AddComponent<UIGradient>().Set(theme.PanelTop, theme.PanelBottom);

            if (theme.EdgeWidth > 0f)
            {
                var rim = UIFactory.Image(Panel, "Rim", UISprites.RoundedEdge(theme.PanelRadius, theme.EdgeWidth), Color.white);
                rim.rectTransform.Stretch();
                rim.gameObject.AddComponent<UIGradient>().Set(theme.EdgeTop, theme.EdgeBottom);
            }

            // --- header
            float iconSize = 40f, iconRadius = Mathf.Min(theme.ButtonRadius, 12f);
            BackButton = UIFactory.IconButton(Panel, "Back", theme, Icon.Back, iconSize, iconRadius);
            BackButton.GetComponent<RectTransform>().Pin(new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(Pad, -HeaderHeight * 0.5f - 4f), Vector2.one * iconSize);

            CloseButton = UIFactory.IconButton(Panel, "Close", theme, Icon.Close, iconSize, iconRadius);
            CloseButton.GetComponent<RectTransform>().Pin(new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-Pad, -HeaderHeight * 0.5f - 4f), Vector2.one * iconSize);

            SettingsButton = UIFactory.IconButton(Panel, "Settings", theme, Icon.Gear, iconSize, iconRadius);
            SettingsButton.GetComponent<RectTransform>().Pin(new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-Pad - iconSize - 8f, -HeaderHeight * 0.5f - 4f), Vector2.one * iconSize);

            brand = UIFactory.Text(Panel, "Brand", theme, 13, TextAlignmentOptions.BottomLeft, theme.SubText,
                FontStyles.UpperCase | FontStyles.Bold, 4f);
            title = UIFactory.Text(Panel, "Title", theme, 31, TextAlignmentOptions.TopLeft, theme.Text,
                theme.TitleStyle, theme.TitleSpacing);
            title.enableAutoSizing = true;   // long bundle names shrink before they truncate
            title.fontSizeMin = 18f;
            title.fontSizeMax = 31f;
            brand.text = brandText;
            SetBackVisible(false);

            AccentLine = UIFactory.Image(Panel, "AccentLine", UISprites.RoundedFill(1.5f), Color.white).rectTransform;
            AccentLine.TopStrip(HeaderHeight, 3f, Pad, Pad);
            AccentLine.gameObject.AddComponent<UIGradient>().Set(theme.Accent, theme.Accent2, horizontal: true);

            // --- rows
            var list = UIFactory.Rect("Rows", Panel).TopStrip(rowsTop, rowsHeight, Pad, Pad);
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = RowSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            for (int i = 0; i < rowsPerPage; i++) rows.Add(RowView.Create(list, theme, RowHeight, i));

            // --- footer
            var footer = UIFactory.Rect("Footer", Panel).TopStrip(footerTop, 52f, Pad, Pad);
            float navW = 92f;
            PrevButton = UIFactory.IconButton(footer, "Prev", theme, Icon.ChevronLeft, 52f, theme.ButtonRadius);
            PrevButton.GetComponent<RectTransform>().Pin(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(navW, 52f));
            NextButton = UIFactory.IconButton(footer, "Next", theme, Icon.ChevronRight, 52f, theme.ButtonRadius);
            NextButton.GetComponent<RectTransform>().Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(navW, 52f));
            foreach (var nav in new[] { PrevButton, NextButton })
            {
                var icon = nav.transform.Find("Icon").GetComponent<RectTransform>();
                icon.Stretch((navW - 26f) / 2f, 13f, (navW - 26f) / 2f, 13f);
                nav.HoverScale = 1.04f;
            }

            pageLabel = UIFactory.Text(footer, "Page", theme, 16, TextAlignmentOptions.Center, theme.SubText, FontStyles.Bold, 3f);
            pageLabel.rectTransform.Stretch(navW, 0, navW, 0);

            status = UIFactory.Text(Panel, "Status", theme, 14, TextAlignmentOptions.Center, theme.SubText);
            status.rectTransform.TopStrip(statusTop, 22f, Pad, Pad);
            status.overflowMode = TextOverflowModes.Ellipsis;
        }

        public void SetTitle(string text) => title.text = text;

        public void SetBackVisible(bool visible)
        {
            BackButton.gameObject.SetActive(visible);
            float left = visible ? Pad + 40f + 14f : Pad + 4f;
            float right = Pad + 40f * 2f + 8f + 12f;
            brand.rectTransform.TopStrip(20f, 22f, left, right);
            title.rectTransform.TopStrip(44f, 44f, left, right);
        }

        public void SetPaging(int page, int pageCount)
        {
            pageLabel.text = pageCount <= 1 ? "" : $"{page + 1} / {pageCount}";
            foreach (var nav in new[] { PrevButton, NextButton })
            {
                nav.interactable = pageCount > 1;
                // Disabled arrows fade right back instead of looking clickable.
                if (!nav.TryGetComponent(out CanvasGroup group)) group = nav.gameObject.AddComponent<CanvasGroup>();
                group.alpha = pageCount > 1 ? 1f : 0.3f;
            }
        }

        public void SetStatus(string text, Color color, float seconds = 4f)
        {
            status.text = text;
            status.color = color;
            statusUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Show a slice of rows. Returns the animated items for the rows that are visible.</summary>
        public List<AnimatedItem> ShowRows(IReadOnlyList<RowSpec> specs)
        {
            var visible = new List<AnimatedItem>();
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i < specs.Count;
                rows[i].gameObject.SetActive(on);
                if (!on) continue;
                rows[i].Apply(specs[i]);
                visible.Add(rows[i].Animated);
            }
            return visible;
        }

        /// <summary>True if these specs have the same identities as what's on screen (so we can update in place).</summary>
        public bool Matches(IReadOnlyList<RowSpec> specs)
        {
            int active = 0;
            foreach (var r in rows) if (r.gameObject.activeSelf) active++;
            if (active != specs.Count) return false;
            for (int i = 0; i < specs.Count; i++)
                if (rows[i].Key != specs[i].Key) return false;
            return true;
        }

        public void UpdateRows(IReadOnlyList<RowSpec> specs)
        {
            for (int i = 0; i < specs.Count && i < rows.Count; i++) rows[i].Apply(specs[i]);
        }

        public Selectable FirstRowButton()
        {
            foreach (var r in rows)
                if (r.gameObject.activeSelf && r.Button.interactable) return r.Button;
            return null;
        }

        private void Update()
        {
            // Status text fades back to the neutral colour once it's stale.
            if (status != null && Theme != null && Time.unscaledTime > statusUntil)
                status.color = Color.Lerp(status.color, Theme.SubText.WithAlpha(0.8f), Time.unscaledDeltaTime * 3f);
        }
    }
}
