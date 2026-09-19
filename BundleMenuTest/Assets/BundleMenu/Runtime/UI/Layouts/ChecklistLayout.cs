using TMPro;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// A to-do sheet: big left-aligned title, borderless rows with tick boxes, and a column of
    /// square tool tabs (close / settings / back) clipped onto the right edge. The page arrows
    /// hang under the bottom corners as separate squares.
    ///
    ///   ┌──────────────┐[✕]
    ///   │ TITLE        │[⚙]
    ///   │ ☐ row        │[↩]
    ///   │ ☑ row        │
    ///   └──────────────┘
    ///    [◀]        [▶]
    /// </summary>
    internal sealed class ChecklistLayout : MenuLayout
    {
        private const float BodyW = 390f, TabS = 46f, TabGap = 8f, Pad = 18f, Arrow = 52f;
        private const float W = BodyW + TabGap + TabS;

        public override void Build(LayoutKit k, int rowsPerPage, string brandText)
        {
            var t = k.T;
            float rowH = Mathf.Min(t.RowHeight, 50f);
            float rowsTop = 104f;
            float rowsH = RowsHeight(rowsPerPage, rowH, 4f);
            float bodyH = rowsTop + rowsH + 14f + 22f + 14f;
            float h = bodyH + 12f + Arrow;

            var p = k.BeginPanel(W, h);
            var body = k.Shell(p, "Body", 0, 0, BodyW, bodyH);

            k.Brand(body, brandText, Pad + 2f, 18f, BodyW - Pad * 2f, 18f, TextAlignmentOptions.MidlineRight, 11f);
            var title = k.Title(body, Pad + 2f, 34f, BodyW - Pad * 2f, 50f, 36f, TextAlignmentOptions.MidlineLeft);
            title.fontStyle |= FontStyles.Bold;
            k.Accent(body, Pad + 2f, 88f, 64f, 4f);

            k.Rows(body, Pad - 6f, rowsTop, BodyW - (Pad - 6f) * 2f, rowsPerPage, 1, RowLook.Box, rowH, 4f);
            k.Status(body, Pad, rowsTop + rowsH + 12f, BodyW - Pad * 2f, 22f, 13f);

            // Tool tabs on the right edge.
            float r = Mathf.Min(t.ButtonRadius, 8f);
            k.View.CloseButton = k.Icon(p, "Close", BundleMenu.Icon.Close, BodyW + TabGap, 14f, TabS, TabS, r, 0.55f);
            k.View.SettingsButton = k.Icon(p, "Settings", BundleMenu.Icon.Gear, BodyW + TabGap, 14f + TabS + 8f, TabS, TabS, r, 0.55f);
            k.View.BackButton = k.Icon(p, "Back", BundleMenu.Icon.Back, BodyW + TabGap, 14f + (TabS + 8f) * 2f, TabS, TabS, r, 0.55f);

            // Arrows as free-standing squares under the corners, page count between them.
            k.View.PrevButton = k.Icon(p, "Prev", BundleMenu.Icon.ChevronLeft, 26f, bodyH + 12f, Arrow, Arrow, r, 0.5f);
            k.View.NextButton = k.Icon(p, "Next", BundleMenu.Icon.ChevronRight, BodyW - 26f - Arrow, bodyH + 12f, Arrow, Arrow, r, 0.5f);
            k.PageLabel(p, 26f + Arrow, bodyH + 12f, BodyW - (26f + Arrow) * 2f, Arrow, 15f);
        }
    }
}
