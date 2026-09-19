using System;
using TMPro;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>One placed piece of a hand-made menu. Sizes are in menu units, measured from the top-left.</summary>
    [Serializable]
    public sealed class CustomSlot
    {
        public float x, y, w, h;
        public int align;           // text only: 0 left, 1 centre, 2 right

        public bool Used => w > 1f && h > 1f;
        public CustomSlot() { }
        public CustomSlot(float x, float y, float w, float h, int align = 0)
        {
            this.x = x; this.y = y; this.w = w; this.h = h; this.align = align;
        }
    }

    /// <summary>
    /// A menu laid out by hand in the launcher's Designer (Layout tab). The theme carries it, the
    /// Custom menu type draws it. Anything left at zero size simply isn't drawn, so you can build a
    /// menu with, say, no brand line and the arrows wherever you like.
    /// </summary>
    [Serializable]
    public sealed class CustomLayoutData
    {
        public float width = 440f, height = 560f;
        public int columns = 1;
        public int rowLook;                 // matches RowLook: Button, Box, Switch, Card, Tile, Plank
        public float rowHeight;             // 0 = use the theme's row height
        public float rowSpacing = -1f;      // < 0 = use the theme's spacing

        public CustomSlot body = new CustomSlot(0, 0, 440f, 560f);
        public CustomSlot rows = new CustomSlot(20f, 120f, 400f, 380f);
        public CustomSlot title = new CustomSlot(20f, 40f, 300f, 44f, 0);
        public CustomSlot brand = new CustomSlot(20f, 18f, 300f, 20f, 0);
        public CustomSlot page = new CustomSlot(170f, 508f, 100f, 28f, 1);
        public CustomSlot status = new CustomSlot(20f, 532f, 400f, 22f, 1);
        public CustomSlot back = new CustomSlot(340f, 36f, 40f, 40f);
        public CustomSlot settings = new CustomSlot(384f, 36f, 40f, 40f);
        public CustomSlot close = new CustomSlot(384f, 8f, 40f, 24f);
        public CustomSlot prev = new CustomSlot(20f, 508f, 92f, 40f);
        public CustomSlot next = new CustomSlot(328f, 508f, 92f, 40f);

        public bool Valid => width > 50f && height > 50f;

        public static TextAlignmentOptions Align(int align) => align switch
        {
            1 => TextAlignmentOptions.Center,
            2 => TextAlignmentOptions.MidlineRight,
            _ => TextAlignmentOptions.MidlineLeft,
        };
    }
}
