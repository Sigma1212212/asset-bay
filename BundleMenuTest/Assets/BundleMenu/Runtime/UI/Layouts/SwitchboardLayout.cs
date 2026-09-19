using TMPro;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// A control panel: folder tabs (back · settings · close) stick up from the top edge, a big
    /// two-colour title, and every row is a label with a chunky switch square on the right
    /// (red off, green on). Round arrow studs sit just outside the bottom corners.
    ///
    ///                     ╭↩╮╭⚙╮╭✕╮
    ///   ┌──────────────────────────┐
    ///   │        T I T L E         │
    ///   │ label               [■]  │
    ///   └──────────────────────────┘
    ///  (◀)          1/3          (▶)
    /// </summary>
    internal sealed class SwitchboardLayout : MenuLayout
    {
        private const float BodyW = 400f, TabH = 38f, TabW = 50f, Pad = 18f, Stud = 50f;
        private const float W = BodyW + 24f; // studs overhang the body a little

        public override void Build(LayoutKit k, int rowsPerPage, string brandText)
        {
            var t = k.T;
            float rowH = Mathf.Max(48f, Mathf.Min(t.RowHeight, 58f));
            float bodyTop = TabH - 6f;
            float rowsTop = 86f;
            float rowsH = RowsHeight(rowsPerPage, rowH, 6f);
            float bodyH = rowsTop + rowsH + 12f + 22f + 12f;
            float h = bodyTop + bodyH + 10f + Stud;
            const float bx = 12f;

            var p = k.BeginPanel(W, h);

            // Tabs are built first so the body covers their lower edge (they look tucked in).
            float r = Mathf.Min(t.ButtonRadius + 4f, 14f);
            float tx = bx + BodyW - 20f - TabW * 3f - 12f;
            k.View.BackButton = k.Icon(p, "Back", BundleMenu.Icon.Back, tx, 0, TabW, TabH + 10f, r, 0.5f);
            k.View.SettingsButton = k.Icon(p, "Settings", BundleMenu.Icon.Gear, tx + TabW + 6f, 0, TabW, TabH + 10f, r, 0.5f);
            k.View.CloseButton = k.Icon(p, "Close", BundleMenu.Icon.Close, tx + (TabW + 6f) * 2f, 0, TabW, TabH + 10f, r, 0.5f);
            foreach (var b in new[] { k.View.BackButton, k.View.SettingsButton, k.View.CloseButton })
            {
                var icon = (RectTransform)b.transform.Find("Icon");
                icon.anchoredPosition = new Vector2(0, 5f); // centre in the visible part above the body
            }

            var body = k.Shell(p, "Body", bx, bodyTop, BodyW, bodyH);

            var title = k.Title(body, Pad, 14f, BodyW - Pad * 2f, 50f, 40f, TextAlignmentOptions.Center);
            title.fontStyle |= FontStyles.Bold;
            title.color = t.Accent;
            k.Brand(body, brandText, Pad, 62f, BodyW - Pad * 2f, 16f, TextAlignmentOptions.Center, 10f);

            k.Rows(body, Pad - 4f, rowsTop, BodyW - (Pad - 4f) * 2f, rowsPerPage, 1, RowLook.Switch, rowH, 6f);
            k.Status(body, Pad, rowsTop + rowsH + 10f, BodyW - Pad * 2f, 22f, 13f);

            float sy = bodyTop + bodyH + 10f;
            k.View.PrevButton = k.Icon(p, "Prev", BundleMenu.Icon.ChevronLeft, 0, sy, Stud, Stud, Stud * 0.5f, 0.5f);
            k.View.NextButton = k.Icon(p, "Next", BundleMenu.Icon.ChevronRight, W - Stud, sy, Stud, Stud, Stud * 0.5f, 0.5f);
            k.PageLabel(p, Stud, sy, W - Stud * 2f, Stud, 15f);
        }
    }
}
