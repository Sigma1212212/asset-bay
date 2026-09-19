using TMPro;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// A hanging sign: a beam wider than the sign across the top, two ropes, the sign board with a
    /// nameplate for the title and plank rows, and a little shelf of controls hung underneath:
    /// [ Close ] (⚙) (↩)   (◀) (▶)
    /// </summary>
    internal sealed class SignboardLayout : MenuLayout
    {
        private const float W = 440f, BeamH = 22f, Rope = 34f, SignW = 392f, Pad = 22f, Shelf = 46f;

        public override void Build(LayoutKit k, int rowsPerPage, string brandText)
        {
            var t = k.T;
            float rowH = Mathf.Min(t.RowHeight, 50f);
            float signTop = BeamH + Rope;
            float rowsTop = 108f;
            float rowsH = RowsHeight(rowsPerPage, rowH, 7f);
            float signH = rowsTop + rowsH + 12f + 20f + 14f;
            float shelfTop = signTop + signH + 14f;
            float h = shelfTop + Shelf;
            float sx = (W - SignW) * 0.5f;

            var p = k.BeginPanel(W, h);
            Color dark = RowView.Darken(t.EdgeBottom, 0.85f), rope = Color.Lerp(t.ButtonFill, t.SubText, 0.4f);

            // Beam with end caps, ropes down to the sign's shoulders.
            k.Solid(p, "Beam", 0, 4f, W, BeamH, dark, 6f);
            k.Solid(p, "BeamTop", 4f, 6f, W - 8f, 4f, Color.white.WithAlpha(0.12f), 2f);
            foreach (float x in new[] { sx + 46f, sx + SignW - 52f })
            {
                k.Solid(p, "Rope", x, BeamH, 6f, Rope + 10f, rope, 3f);
                k.Solid(p, "Knot", x - 4f, BeamH - 2f, 14f, 10f, rope, 5f);
            }

            var sign = k.Shell(p, "Sign", sx, signTop, SignW, signH);
            foreach (float x in new[] { sx + 46f - 3f, sx + SignW - 52f - 3f })
                k.Solid(p, "Ring", x, signTop - 6f, 12f, 12f, dark, 6f);

            // Nameplate: a pale board for the title so it reads on any wood or colour.
            var plate = k.Box(sign, "Plate", 44f, 18f, SignW - 88f, 58f);
            var plateFill = UIFactory.Image(plate, "Fill", UISprites.RoundedFill(Mathf.Min(t.ButtonRadius, 10f)), t.ButtonFill);
            plateFill.rectTransform.Stretch();
            k.Rim(plate, Mathf.Min(t.ButtonRadius, 10f), 2f);
            var title = k.Title(plate, 12f, 4f, SignW - 88f - 24f, 50f, 30f, TextAlignmentOptions.Center);
            title.color = t.Text;
            k.Brand(sign, brandText, Pad, 80f, SignW - Pad * 2f, 18f, TextAlignmentOptions.Center, 10f);

            k.Rows(sign, Pad, rowsTop, SignW - Pad * 2f, rowsPerPage, 1, RowLook.Plank, rowH, 7f);
            k.Status(sign, Pad, rowsTop + rowsH + 10f, SignW - Pad * 2f - 70f, 20f, 12f).alignment = TextAlignmentOptions.MidlineLeft;
            k.PageLabel(sign, SignW - Pad - 70f, rowsTop + rowsH + 10f, 70f, 20f, 13f).alignment = TextAlignmentOptions.MidlineRight;

            // The shelf of controls, hung on two short chains.
            foreach (float x in new[] { sx + 30f, sx + SignW - 36f }) k.Solid(p, "Chain", x, signTop + signH, 5f, 16f, rope, 2f);
            float r = Mathf.Min(t.ButtonRadius, 10f), s = Shelf;
            k.View.CloseButton = k.TextButton(p, "Close", "Close", sx, shelfTop, 120f, s, r, 18f, t.LabelStyle | FontStyles.Bold, t.LabelSpacing);
            k.View.SettingsButton = k.Icon(p, "Settings", BundleMenu.Icon.Gear, sx + 128f, shelfTop, s, s, r, 0.6f);
            k.View.BackButton = k.Icon(p, "Back", BundleMenu.Icon.Back, sx + 128f + s + 8f, shelfTop, s, s, r, 0.6f);
            k.View.PrevButton = k.Icon(p, "Prev", BundleMenu.Icon.ChevronLeft, sx + SignW - s * 2f - 8f, shelfTop, s, s, r, 0.55f);
            k.View.NextButton = k.Icon(p, "Next", BundleMenu.Icon.ChevronRight, sx + SignW - s, shelfTop, s, s, r, 0.55f);
        }

        public override void SetBackVisible(MenuView v, bool visible) => DimBack(v, visible);
    }
}
