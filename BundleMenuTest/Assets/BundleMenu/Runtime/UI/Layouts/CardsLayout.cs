using TMPro;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// App-style: a small close pill floats above, a centred title with a faint version line,
    /// slim cards with chevrons, and a dock at the bottom holding every control:
    /// (⚙) (‹ PREV) (NEXT ›) (↩).
    /// </summary>
    internal sealed class CardsLayout : MenuLayout
    {
        private const float W = 400f, Pill = 30f, Pad = 16f, Dock = 46f;

        public override void Build(LayoutKit k, int rowsPerPage, string brandText)
        {
            var t = k.T;
            float rowH = Mathf.Min(t.RowHeight, 50f);
            float bodyTop = Pill + 10f;
            float rowsTop = 96f;
            float rowsH = RowsHeight(rowsPerPage, rowH, 8f);
            float statusTop = rowsTop + rowsH + 8f;
            float dockTop = statusTop + 26f;
            float bodyH = dockTop + Dock + 12f;
            float h = bodyTop + bodyH;

            var p = k.BeginPanel(W, h);
            k.View.CloseButton = k.TextButton(p, "Close", "CLOSE", W * 0.5f - 60f, 0, 120f, Pill, Pill * 0.5f, 13f,
                FontStyles.Bold, 3f);

            var body = k.Shell(p, "Body", 0, bodyTop, W, bodyH);
            k.Title(body, Pad, 16f, W - Pad * 2f, 40f, 27f, TextAlignmentOptions.Center);
            k.Brand(body, brandText, Pad, 56f, W - Pad * 2f, 16f, TextAlignmentOptions.Center, 10f);
            var line = k.Accent(body, Pad, 80f, W - Pad * 2f, 1f);
            line.GetComponent<UnityEngine.UI.Image>().color = new Color(1, 1, 1, 0.5f);

            k.Rows(body, Pad, rowsTop, W - Pad * 2f, rowsPerPage, 1, RowLook.Card, rowH, 8f);
            k.Status(body, Pad, statusTop, W - Pad * 2f, 20f, 12f);
            k.PageLabel(body, W - Pad - 60f, 16f, 60f, 16f, 11f).alignment = TextAlignmentOptions.TopRight;

            // Dock: a recessed strip with round controls.
            var dock = k.Box(body, "Dock", Pad, dockTop, W - Pad * 2f, Dock);
            var well = UIFactory.Image(dock, "Well", UISprites.RoundedFill(Dock * 0.5f), Color.black.WithAlpha(0.22f));
            well.rectTransform.Stretch();

            float dw = W - Pad * 2f, round = Dock - 10f, pill = 104f;
            k.View.SettingsButton = k.Icon(dock, "Settings", BundleMenu.Icon.Gear, 5f, 5f, round, round, round * 0.5f, 0.6f);
            k.View.BackButton = k.Icon(dock, "Back", BundleMenu.Icon.Back, dw - 5f - round, 5f, round, round, round * 0.5f, 0.6f);
            k.View.PrevButton = k.TextButton(dock, "Prev", "< PREV", dw * 0.5f - pill - 4f, 5f, pill, round, round * 0.5f, 13f, FontStyles.Bold, 2f);
            k.View.NextButton = k.TextButton(dock, "Next", "NEXT >", dw * 0.5f + 4f, 5f, pill, round, round * 0.5f, 13f, FontStyles.Bold, 2f);
        }

        public override void SetBackVisible(MenuView v, bool visible) => DimBack(v, visible);
    }
}
