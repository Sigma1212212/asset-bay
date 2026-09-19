using TMPro;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// An open book: a cover with two pages and a spine. The title heads the left page, the brand
    /// the right; rows fill the left page, then the right. Close / settings / back are index tabs
    /// sticking out of the right-hand edge, and the page arrows are the bottom corners of each page.
    /// </summary>
    internal sealed class BookLayout : MenuLayout
    {
        private const float PageW = 300f, Spine = 26f, Cover = 14f, TabW = 40f, TabH = 50f, Pad = 18f;
        private const float BookW = Cover * 2f + PageW * 2f + Spine;
        private const float W = BookW + TabW - 6f;

        public override void Build(LayoutKit k, int rowsPerPage, string brandText)
        {
            var t = k.T;
            int leftCount = Mathf.CeilToInt(rowsPerPage / 2f), rightCount = rowsPerPage - leftCount;
            float rowH = Mathf.Min(t.RowHeight, 52f);
            float rowsTop = 76f;
            float rowsH = RowsHeight(leftCount, rowH, 10f);
            float cornerTop = rowsTop + rowsH + 16f;
            float pageH = cornerTop + 44f + 14f;
            float h = pageH + Cover * 2f;

            var p = k.BeginPanel(W, h);

            // Index tabs first, so the cover overlaps their inner ends.
            float r = Mathf.Min(t.ButtonRadius + 2f, 10f), tx = BookW - 10f;
            k.View.CloseButton = k.Icon(p, "Close", BundleMenu.Icon.Close, tx, 40f, TabW + 4f, TabH, r, 0.55f);
            k.View.SettingsButton = k.Icon(p, "Settings", BundleMenu.Icon.Gear, tx, 40f + TabH + 8f, TabW + 4f, TabH, r, 0.55f);
            k.View.BackButton = k.Icon(p, "Back", BundleMenu.Icon.Back, tx, 40f + (TabH + 8f) * 2f, TabW + 4f, TabH, r, 0.55f);
            foreach (var b in new[] { k.View.CloseButton, k.View.SettingsButton, k.View.BackButton })
                ((RectTransform)b.transform.Find("Icon")).anchoredPosition = new Vector2(4f, 0f);

            // Cover (the theme's rim colours read as leather), then the two pages on top.
            var cover = k.Box(p, "Cover", 0, 0, BookW, h);
            var leather = UIFactory.Image(cover, "Leather", UISprites.RoundedFill(t.PanelRadius), Color.white, raycast: true);
            leather.rectTransform.Stretch();
            leather.gameObject.AddComponent<UIGradient>().Set(t.EdgeTop, t.EdgeBottom);

            var left = k.Shell(cover, "LeftPage", Cover, Cover, PageW + Spine * 0.5f, pageH, 6f, glow: false, rim: 1.5f);
            var right = k.Shell(cover, "RightPage", Cover + PageW + Spine * 0.5f, Cover, PageW + Spine * 0.5f, pageH, 6f, glow: false, rim: 1.5f);
            // Spine shadow: dark in the fold, fading out onto both pages, with a crease line.
            float foldX = Cover + PageW + Spine * 0.5f, foldW = Spine + 10f;
            var foldL = k.Solid(cover, "FoldL", foldX - foldW, Cover, foldW, pageH, Color.white);
            foldL.gameObject.AddComponent<UIGradient>().Set(t.EdgeBottom.WithAlpha(0f), t.EdgeBottom.WithAlpha(0.45f), horizontal: true);
            var foldR = k.Solid(cover, "FoldR", foldX, Cover, foldW, pageH, Color.white);
            foldR.gameObject.AddComponent<UIGradient>().Set(t.EdgeBottom.WithAlpha(0.45f), t.EdgeBottom.WithAlpha(0f), horizontal: true);
            k.Solid(cover, "Crease", foldX - 1.5f, Cover, 3f, pageH, t.EdgeBottom.WithAlpha(0.6f));

            // Left page: title; right page: brand.
            k.Title(left, Pad, 16f, PageW - Pad * 2f, 42f, 30f, TextAlignmentOptions.MidlineLeft);
            k.Accent(left, Pad, 60f, 90f, 3f);
            k.Brand(right, brandText, Pad + Spine * 0.5f, 24f, PageW - Pad * 2f, 20f, TextAlignmentOptions.MidlineRight, 11f);

            k.Rows(left, Pad, rowsTop, PageW - Pad * 2f + 4f, leftCount, 1, RowLook.Tile, rowH, 10f);
            if (rightCount > 0)
                k.Rows(right, Pad + Spine * 0.5f - 4f, rowsTop, PageW - Pad * 2f + 4f, rightCount, 1, RowLook.Tile, rowH, 10f);

            // Page corners: the arrows, with the page number and status in between.
            k.View.PrevButton = k.TextButton(left, "Prev", "< prev", Pad, cornerTop, 96f, 40f, r, 17f, t.LabelStyle, 1f);
            k.View.NextButton = k.TextButton(right, "Next", "next >", PageW + Spine * 0.5f - Pad - 96f, cornerTop, 96f, 40f, r, 17f, t.LabelStyle, 1f);
            k.PageLabel(left, Pad + 100f, cornerTop, PageW - Pad * 2f - 100f, 40f, 14f).fontStyle = FontStyles.Italic;
            k.Status(right, Pad + Spine * 0.5f, cornerTop, PageW - Pad * 2f - 104f, 40f, 12f).alignment = TextAlignmentOptions.MidlineLeft;
        }
    }
}
