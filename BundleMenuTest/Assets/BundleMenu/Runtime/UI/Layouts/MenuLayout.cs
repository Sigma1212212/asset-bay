using TMPro;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Builds one menu style. Every layout must create: Panel (via kit.BeginPanel), title, brand,
    /// page label, status, rows, and the five buttons (Back, Settings, Close, Prev, Next).
    /// Coordinates in the kit are top-left based, y going down, in panel units.
    /// </summary>
    internal abstract class MenuLayout
    {
        public abstract void Build(LayoutKit k, int rowsPerPage, string brandText);

        /// <summary>Back only makes sense below the home page. Default: hide it.</summary>
        public virtual void SetBackVisible(MenuView v, bool visible)
        {
            if (v.BackButton != null) v.BackButton.gameObject.SetActive(visible);
        }

        /// <summary>Some layouts keep Back in place and just dim it (it's part of a bar).</summary>
        protected static void DimBack(MenuView v, bool visible)
        {
            if (v.BackButton == null) return;
            v.BackButton.interactable = visible;
            if (!v.BackButton.TryGetComponent(out CanvasGroup g)) g = v.BackButton.gameObject.AddComponent<CanvasGroup>();
            g.alpha = visible ? 1f : 0.3f;
            if (v.BackButton.Base != null)
            {
                if (!v.BackButton.Base.TryGetComponent(out CanvasGroup bg)) bg = v.BackButton.Base.AddComponent<CanvasGroup>();
                bg.alpha = g.alpha;
            }
        }

        protected static float RowsHeight(MenuTheme t, int rowsPerPage, int columns, float rowHeight) =>
            RowsHeight(Mathf.CeilToInt(rowsPerPage / (float)columns), rowHeight, t.RowSpacing);

        protected static float RowsHeight(int lines, float rowHeight, float spacing) =>
            lines * rowHeight + Mathf.Max(0, lines - 1) * spacing;

        /// <summary>Title autosizes so long bundle names shrink before they truncate.</summary>
        protected static void AutoTitle(TextMeshProUGUI title, float max, float min = 16f)
        {
            title.enableAutoSizing = true;
            title.fontSizeMax = max;
            title.fontSizeMin = min;
        }
    }
}
