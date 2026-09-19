using TMPro;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// The original: header with back · title · settings · close, accent line, rows (list, or grid if
    /// the theme asks), and a wide-arrow footer with the page number.
    /// </summary>
    internal sealed class ClassicLayout : MenuLayout
    {
        private const float W = 440f, Pad = 20f, Header = 98f, Icon = 40f;

        public override void Build(LayoutKit k, int rowsPerPage, string brandText)
        {
            var t = k.T;
            int cols = t.Layout == ThemeLayout.Grid ? 2 : 1;
            float rowsH = RowsHeight(t, rowsPerPage, cols, t.RowHeight);
            float rowsTop = Header + 18f;
            float footerTop = rowsTop + rowsH + 14f;
            float statusTop = footerTop + 52f + 8f;
            float h = statusTop + 22f + 14f;

            var p = k.BeginPanel(W, h);
            var body = k.Shell(p, "Body", 0, 0, W, h);

            float r = Mathf.Min(t.ButtonRadius, 12f), iy = Header * 0.5f + 4f - Icon * 0.5f;
            k.View.BackButton = k.Icon(body, "Back", BundleMenu.Icon.Back, Pad, iy, Icon, Icon, r, 0.6f);
            k.View.CloseButton = k.Icon(body, "Close", BundleMenu.Icon.Close, W - Pad - Icon, iy, Icon, Icon, r, 0.6f);
            k.View.SettingsButton = k.Icon(body, "Settings", BundleMenu.Icon.Gear, W - Pad - Icon * 2f - 8f, iy, Icon, Icon, r, 0.6f);

            k.Brand(body, brandText, 0, 20, 10, 22, TextAlignmentOptions.BottomLeft);
            k.Title(body, 0, 44, 10, 44, 31f, TextAlignmentOptions.TopLeft);
            k.Accent(body, Pad, Header, W - Pad * 2f, 3f);

            k.Rows(body, Pad, rowsTop, W - Pad * 2f, rowsPerPage, cols, RowLook.Button, t.RowHeight, t.RowSpacing);

            const float navW = 92f;
            k.View.PrevButton = k.Icon(body, "Prev", BundleMenu.Icon.ChevronLeft, Pad, footerTop, navW, 52f, t.ButtonRadius);
            k.View.NextButton = k.Icon(body, "Next", BundleMenu.Icon.ChevronRight, W - Pad - navW, footerTop, navW, 52f, t.ButtonRadius);
            k.PageLabel(body, Pad + navW, footerTop, W - (Pad + navW) * 2f, 52f);
            k.Status(body, Pad, statusTop, W - Pad * 2f, 22f);
        }

        public override void SetBackVisible(MenuView v, bool visible)
        {
            base.SetBackVisible(v, visible);
            // The title slides over to make room for Back.
            float left = visible ? Pad + Icon + 14f : Pad + 4f;
            float right = Pad + Icon * 2f + 8f + 12f;
            LayoutKit.At(v.brand.rectTransform, left, 20f, W - left - right, 22f);
            LayoutKit.At(v.title.rectTransform, left, 44f, W - left - right, 44f);
        }
    }
}
