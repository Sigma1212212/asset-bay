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
    /// Where each piece goes depends on the <see cref="MenuStyle"/>: a <see cref="MenuLayout"/> builds
    /// the Panel (bounds of everything, including outside tabs), the body shell, header, rows and
    /// controls. Every style provides the same pieces, so the controller never cares which one is up.
    /// </summary>
    public sealed class MenuView : MonoBehaviour
    {
        /// <summary>Design width of the Classic panel. Other styles report their own <see cref="PanelWidth"/>.</summary>
        public const float Width = 440f;
        public const float RowHeight = 56f;
        public const float RowSpacing = 8f;

        public RectTransform Panel { get; internal set; }
        public CanvasGroup PanelGroup { get; internal set; }
        public RectTransform AccentLine { get; internal set; }
        public MenuButton BackButton { get; internal set; }
        public MenuButton SettingsButton { get; internal set; }
        public MenuButton CloseButton { get; internal set; }
        public MenuButton PrevButton { get; internal set; }
        public MenuButton NextButton { get; internal set; }
        /// <summary>Full size of everything this style draws, including tabs outside the main body.</summary>
        public float Height { get; internal set; }
        public float PanelWidth { get; internal set; } = Width;
        public MenuTheme Theme { get; private set; }
        public MenuStyle Style { get; private set; }

        internal readonly List<RowView> rows = new List<RowView>();
        internal TextMeshProUGUI brand, title, pageLabel, status;
        private float statusUntil;
        private MenuLayout layout;

        public IReadOnlyList<RowView> Rows => rows;

        public void Build(MenuTheme theme, int rowsPerPage, string brandText) =>
            Build(theme, MenuStyle.Classic, rowsPerPage, brandText);

        /// <summary>(Re)build everything for a theme and menu style. Existing children are destroyed.</summary>
        public void Build(MenuTheme theme, MenuStyle style, int rowsPerPage, string brandText)
        {
            Theme = theme;
            Style = style;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false); // Destroy is deferred; hide now so the rebuild never overlaps it
                Destroy(child);
            }
            rows.Clear();
            AccentLine = null;
            PanelWidth = Width;

            layout = MenuStyles.Layout(style);
            var kit = new LayoutKit(this, theme);
            layout.Build(kit, rowsPerPage, brandText);
            kit.EnsureButtons();
            SetBackVisible(false);
        }

        public void SetTitle(string text) => title.text = text;

        public void SetBackVisible(bool visible) => layout?.SetBackVisible(this, visible);

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
