namespace BundleMenu
{
    /// <summary>
    /// The menu's shape and structure, separate from its colours (<see cref="MenuTheme"/>).
    /// Each one builds a different panel: where the title, page arrows, close/settings/back
    /// buttons and rows go, and what a row looks like. New values go at the end (saved as ints).
    /// </summary>
    public enum MenuStyle
    {
        Classic,     // header icons, row list, arrow footer
        Pillars,     // tall page pillars on both sides, close tab above
        Console,     // one segmented control bar at the bottom: prev | back | next
        Checklist,   // borderless rows with a tick box, tool tabs down the right side
        Switchboard, // label + big on/off switch square per row, arrows under the corners
        Cards,       // compact cards with chevrons, pill pager in a bottom dock
        Book,        // wide two-page spread with a spine; rows fill left page then right
        Signboard,   // hanging sign on ropes from a beam, controls in a row underneath
        Custom,      // laid out by hand in the launcher's Designer (see CustomLayoutData)
    }

    /// <summary>What one row looks like inside a style.</summary>
    public enum RowLook
    {
        Button,  // filled rounded button (classic)
        Box,     // no fill, square tick box on the left that fills when on
        Switch,  // label left, square switch on the right (red off / green on)
        Card,    // slim card with an underline accent and a chevron on anything clickable
        Tile,    // raised tile with a darker bottom lip (book pages / grids)
        Plank,   // flat board with inset shadow, centred text (signboard)
    }

    /// <summary>Surface detail drawn over the panel background.</summary>
    public enum PanelPattern { None, Wood, Scanlines, Grid, Paper }

    public static class MenuStyles
    {
        public static string Name(MenuStyle s)
        {
            switch (s)
            {
                case MenuStyle.Pillars: return "Pillars";
                case MenuStyle.Console: return "Console";
                case MenuStyle.Checklist: return "Checklist";
                case MenuStyle.Switchboard: return "Switchboard";
                case MenuStyle.Cards: return "Cards";
                case MenuStyle.Book: return "Book";
                case MenuStyle.Signboard: return "Signboard";
                case MenuStyle.Custom: return "Custom";
                default: return "Classic";
            }
        }

        internal static MenuLayout Layout(MenuStyle s)
        {
            switch (s)
            {
                case MenuStyle.Pillars: return new PillarsLayout();
                case MenuStyle.Console: return new ConsoleLayout();
                case MenuStyle.Checklist: return new ChecklistLayout();
                case MenuStyle.Switchboard: return new SwitchboardLayout();
                case MenuStyle.Cards: return new CardsLayout();
                case MenuStyle.Book: return new BookLayout();
                case MenuStyle.Signboard: return new SignboardLayout();
                case MenuStyle.Custom: return new CustomLayout();
                default: return new ClassicLayout();
            }
        }
    }
}
