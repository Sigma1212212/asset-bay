using System;

namespace BundleMenu
{
    public enum StatusLight { None, Idle, Busy, Ok, Error }

    /// <summary>
    /// Plain data describing one row. Pages produce these; <see cref="RowView"/> renders them.
    /// Because it's data, refreshing a page (e.g. load progress ticking) just re-applies specs
    /// to the existing views without rebuilding or re-animating anything.
    /// </summary>
    public sealed class RowSpec
    {
        /// <summary>Stable identity. Same keys in the same order = update in place instead of rebuild.</summary>
        public string Key;

        public string Label;
        public string Value;                   // right-aligned secondary text ("Loaded", "42%", "Staggered")
        public StatusLight Light = StatusLight.None;
        public float Progress = -1f;           // 0..1 draws a fill bar behind the row; <0 hides it
        public bool IsOn;
        public bool Interactable = true;
        public bool ShowChevron;               // hint that the row navigates somewhere
        public bool Multiline;                 // smaller text that wraps to two lines (error messages)

        public Action OnClick;
        public Action OnAltClick;              // right-click (settings rows use it to cycle backwards)

        /// <summary>Optional small button on the right edge (e.g. "open details").</summary>
        public Action OnSecondary;
        public Icon SecondaryIcon = Icon.ChevronRight;

        public static RowSpec Info(string key, string text, string value = null) =>
            new RowSpec { Key = key, Label = text, Value = value, Interactable = false };

        public static RowSpec Message(string key, string text) =>
            new RowSpec { Key = key, Label = text, Interactable = false, Multiline = true };
    }
}
