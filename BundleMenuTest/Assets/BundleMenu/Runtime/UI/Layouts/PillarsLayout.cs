using TMPro;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// A narrow body between two tall page pillars (the whole side is the arrow), a close tab
    /// hanging above, and a centred title. Settings / back sit as small corner studs.
    ///
    ///        [ close ]
    ///   ┃▌┃ ┌ title ┐ ┃▐┃
    ///   ┃◀┃ │ rows  │ ┃▶┃
    ///   ┃▌┃ └ page  ┘ ┃▐┃
    /// </summary>
    internal sealed class PillarsLayout : MenuLayout
    {
        private const float PillarW = 58f, Gap = 12f, BodyW = 360f, Tab = 44f, Pad = 18f;
        private const float W = PillarW * 2f + Gap * 2f + BodyW;

        public override void Build(LayoutKit k, int rowsPerPage, string brandText)
        {
            var t = k.T;
            float rowH = Mathf.Min(t.RowHeight, 50f);
            float rowsH = RowsHeight(rowsPerPage, rowH, 6f);
            float bodyTop = Tab + 8f;
            float rowsTop = 92f;
            float bodyH = rowsTop + rowsH + 16f + 22f + 12f;
            float h = bodyTop + bodyH;

            var p = k.BeginPanel(W, h);

            // Close tab: a pill above the body, joined to it by a short neck.
            float bx = PillarW + Gap;
            float tabW = 170f;
            k.Solid(p, "Neck", W * 0.5f - 18f, Tab - 4f, 36f, 16f, t.EdgeTop.WithAlpha(0.9f), 3f);
            k.View.CloseButton = k.TextButton(p, "Close", "Close", W * 0.5f - tabW * 0.5f, 0, tabW, Tab, Tab * 0.5f, 20f,
                t.TitleStyle | FontStyles.Bold, t.TitleSpacing);

            var body = k.Shell(p, "Body", bx, bodyTop, BodyW, bodyH);

            // Pillars: the entire side column pages the menu.
            float pillarR = Mathf.Min(t.PanelRadius, 22f);
            k.View.PrevButton = k.Icon(p, "Prev", BundleMenu.Icon.ChevronLeft, 0, bodyTop + 18f, PillarW, bodyH - 36f, pillarR, 0.55f);
            k.View.NextButton = k.Icon(p, "Next", BundleMenu.Icon.ChevronRight, W - PillarW, bodyTop + 18f, PillarW, bodyH - 36f, pillarR, 0.55f);
            foreach (var b in new[] { k.View.PrevButton, k.View.NextButton }) b.HoverScale = 1.03f;

            const float stud = 30f;
            k.View.BackButton = k.Icon(body, "Back", BundleMenu.Icon.Back, 12f, 14f, stud, stud, stud * 0.5f, 0.6f);
            k.View.SettingsButton = k.Icon(body, "Settings", BundleMenu.Icon.Gear, BodyW - 12f - stud, 14f, stud, stud, stud * 0.5f, 0.6f);

            k.Title(body, 50f, 10f, BodyW - 100f, 44f, 32f, TextAlignmentOptions.Center);
            k.Brand(body, brandText, 50f, 54f, BodyW - 100f, 18f, TextAlignmentOptions.Center, 11f);
            k.Accent(body, BodyW * 0.3f, 78f, BodyW * 0.4f, 2f);

            k.Rows(body, Pad, rowsTop, BodyW - Pad * 2f, rowsPerPage, 1, RowLook.Button, rowH, 6f);
            k.PageLabel(body, Pad, rowsTop + rowsH + 10f, BodyW - Pad * 2f, 20f, 14f);
            k.Status(body, Pad, rowsTop + rowsH + 30f, BodyW - Pad * 2f, 20f, 13f);
        }
    }
}
