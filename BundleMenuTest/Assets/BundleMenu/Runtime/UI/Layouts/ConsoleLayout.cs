using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>
    /// A device screen: a filled title band across the top (settings · TITLE · close), a prompt-style
    /// readout line, square-ish rows, and one segmented control bar welded to the bottom edge:
    /// [ ◀ | Back | ▶ ]. Back stays in the bar and dims on the home page.
    /// </summary>
    internal sealed class ConsoleLayout : MenuLayout
    {
        private const float W = 420f, Band = 62f, Pad = 16f, Bar = 54f;

        public override void Build(LayoutKit k, int rowsPerPage, string brandText)
        {
            var t = k.T;
            float rowH = Mathf.Min(t.RowHeight, 48f);
            float rowsTop = Band + 40f;
            float rowsH = RowsHeight(rowsPerPage, rowH, 5f);
            float statusTop = rowsTop + rowsH + 10f;
            float barTop = statusTop + 24f;
            float h = barTop + Bar + Pad;

            var p = k.BeginPanel(W, h);
            float radius = Mathf.Min(t.PanelRadius, 14f);
            var body = k.Shell(p, "Body", 0, 0, W, h, radius);

            // Title band: accent tinted, full width, square bottom.
            var band = k.Box(body, "Band", 4f, 4f, W - 8f, Band);
            var bandFill = UIFactory.Image(band, "Fill", UISprites.RoundedFill(Mathf.Max(0f, radius - 4f)), Color.white);
            bandFill.rectTransform.Stretch();
            bandFill.gameObject.AddComponent<UIGradient>().Set(t.Accent.WithAlpha(0.28f), t.Accent2.WithAlpha(0.12f), horizontal: true);
            k.Accent(body, 4f, Band + 4f, W - 8f, 2f);

            float ib = 36f;
            k.View.SettingsButton = k.Icon(band, "Settings", BundleMenu.Icon.Gear, 12f, (Band - ib) * 0.5f, ib, ib, 6f, 0.6f);
            k.View.CloseButton = k.Icon(band, "Close", BundleMenu.Icon.Power, W - 8f - 12f - ib, (Band - ib) * 0.5f, ib, ib, 6f, 0.6f);
            k.Title(band, 60f, 6f, W - 8f - 120f, Band - 12f, 28f, TextAlignmentOptions.Center);

            // Readout line: "> brand" on the left, page counter on the right.
            k.Brand(body, "> " + brandText, Pad, Band + 12f, W * 0.62f, 20f, TextAlignmentOptions.MidlineLeft, 12f);
            var page = k.PageLabel(body, W * 0.62f, Band + 12f, W * 0.38f - Pad, 20f, 13f);
            page.alignment = TextAlignmentOptions.MidlineRight;

            k.Rows(body, Pad, rowsTop, W - Pad * 2f, rowsPerPage, 1, RowLook.Button, rowH, 5f);
            var status = k.Status(body, Pad, statusTop, W - Pad * 2f, 20f, 13f);
            status.alignment = TextAlignmentOptions.MidlineLeft;

            // The segmented bar: three buttons sharing one outline, split by thin seams.
            float bw = W - Pad * 2f, side = 86f, seam = 3f;
            var bar = k.Box(body, "Bar", Pad, barTop, bw, Bar);
            float r = Mathf.Min(t.ButtonRadius, 12f);
            k.View.PrevButton = k.Icon(bar, "Prev", BundleMenu.Icon.ChevronLeft, 0, 0, side, Bar, r, 0.6f);
            k.View.BackButton = k.TextButton(bar, "Back", "Back", side + seam, 0, bw - (side + seam) * 2f, Bar, 2f, 20f,
                t.LabelStyle | FontStyles.Bold, t.LabelSpacing + 2f);
            k.View.NextButton = k.Icon(bar, "Next", BundleMenu.Icon.ChevronRight, bw - side, 0, side, Bar, r, 0.6f);
            foreach (var b in new[] { k.View.PrevButton, k.View.BackButton, k.View.NextButton }) b.HoverScale = 1.02f;
        }

        public override void SetBackVisible(MenuView v, bool visible) => DimBack(v, visible);
    }
}
