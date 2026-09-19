using TMPro;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// A menu arranged by hand: every piece sits exactly where it was dragged in the launcher's Designer
    /// (Layout tab). Pieces left at zero size are skipped, so a layout can leave out anything it doesn't
    /// want. Falls back to Classic if the theme has no layout in it.
    /// </summary>
    internal sealed class CustomLayout : MenuLayout
    {
        private CustomLayoutData data;

        public override void Build(LayoutKit k, int rowsPerPage, string brandText)
        {
            var t = k.T;
            data = t.Custom != null && t.Custom.Valid ? t.Custom : new CustomLayoutData();

            var p = k.BeginPanel(data.width, data.height);

            // Body: the panel itself. A layout can shrink it to leave room for pieces hanging outside.
            var body = data.body.Used
                ? k.Shell(p, "Body", data.body.x, data.body.y, data.body.w, data.body.h)
                : k.Shell(p, "Body", 0, 0, data.width, data.height);

            // Everything is placed on the panel, so pieces can sit on or off the body.
            if (data.title.Used)
            {
                var title = k.Title(p, data.title.x, data.title.y, data.title.w, data.title.h,
                    Mathf.Clamp(data.title.h * 0.7f, 14f, 42f), CustomLayoutData.Align(data.title.align));
                title.enableAutoSizing = true;
            }
            if (data.brand.Used)
                k.Brand(p, brandText, data.brand.x, data.brand.y, data.brand.w, data.brand.h,
                    CustomLayoutData.Align(data.brand.align), Mathf.Clamp(data.brand.h * 0.6f, 8f, 16f));
            if (data.page.Used)
                k.PageLabel(p, data.page.x, data.page.y, data.page.w, data.page.h,
                    Mathf.Clamp(data.page.h * 0.5f, 10f, 20f)).alignment = CustomLayoutData.Align(data.page.align);
            if (data.status.Used)
                k.Status(p, data.status.x, data.status.y, data.status.w, data.status.h,
                    Mathf.Clamp(data.status.h * 0.6f, 9f, 18f)).alignment = CustomLayoutData.Align(data.status.align);

            if (data.rows.Used)
            {
                int columns = Mathf.Clamp(data.columns, 1, 3);
                int lines = Mathf.CeilToInt(rowsPerPage / (float)columns);
                float spacing = data.rowSpacing >= 0f ? data.rowSpacing : t.RowSpacing;
                // Fit the rows into the box that was drawn for them.
                float height = data.rowHeight > 0f ? data.rowHeight
                    : Mathf.Clamp((data.rows.h - spacing * (lines - 1)) / Mathf.Max(1, lines), 20f, 96f);
                k.Rows(p, data.rows.x, data.rows.y, data.rows.w, rowsPerPage, columns,
                    (RowLook)Mathf.Clamp(data.rowLook, 0, 5), height, spacing);
            }

            float radius = Mathf.Min(t.ButtonRadius, 14f);
            k.View.BackButton = Button(k, data.back, "Back", Icon.Back, radius);
            k.View.SettingsButton = Button(k, data.settings, "Settings", Icon.Gear, radius);
            k.View.CloseButton = Button(k, data.close, "Close", Icon.Close, radius);
            k.View.PrevButton = Button(k, data.prev, "Prev", Icon.ChevronLeft, radius);
            k.View.NextButton = Button(k, data.next, "Next", Icon.ChevronRight, radius);
        }

        private static MenuButton Button(LayoutKit k, CustomSlot slot, string name, Icon icon, float radius) =>
            slot.Used ? k.Icon(k.Panel, name, icon, slot.x, slot.y, slot.w, slot.h, radius, 0.55f) : null;
    }
}
