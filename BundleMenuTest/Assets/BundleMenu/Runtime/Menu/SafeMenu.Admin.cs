using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Safe mode "admin" tab: switch between the normal and admin menus, set the PIN that locks the
    /// switch, and type tags (the in-game rows only cycle presets - here you can write your own).
    /// </summary>
    public sealed partial class SafeMenu
    {
        private string pinEntry = "", pinOld = "", pinNew = "", myTagEntry;
        private string tagTarget, tagEntry = "";

        private void DrawAdminTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(250));

            Section("menu");
            Value("using", Menu.IsAdmin ? "admin menu" : "normal menu");
            Value("pin", Menu.PinSet ? "set" : "not set");
            if (Menu.IsAdmin)
            {
                if (Button("switch to the normal menu")) Menu.SetMode(MenuMode.Normal);
            }
            else if (!Menu.PinSet)
            {
                if (Button("switch to the admin menu")) Menu.SetMode(MenuMode.Admin);
            }
            else
            {
                GUILayout.Label("Enter the PIN to unlock the admin menu:", sDimWrap);
                pinEntry = GUILayout.PasswordField(pinEntry, '*', 32, sField);
                if (Button("unlock") && Menu.SetMode(MenuMode.Admin, pinEntry)) pinEntry = "";
            }
            EndSection();

            Section("admin pin");
            GUILayout.Label("With a PIN set, nobody can switch this menu to admin without it - handy when a friend uses your build.", sDimWrap);
            if (Menu.PinSet)
            {
                GUILayout.Label("current pin", sLabel);
                pinOld = GUILayout.PasswordField(pinOld, '*', 32, sField);
            }
            GUILayout.Label("new pin (empty removes it)", sLabel);
            pinNew = GUILayout.PasswordField(pinNew, '*', 32, sField);
            if (Button(Menu.PinSet ? "change pin" : "set pin") && Menu.SetPin(pinOld, pinNew)) { pinOld = pinNew = ""; }
            EndSection();
            GUILayout.EndVertical();

            GUILayout.Space(8);
            GUILayout.BeginVertical();

            Section("my tag");
            GUILayout.Label("Shown above your head to everyone running Asset Bay near you.", sDimWrap);
            myTagEntry ??= TagStore.MyTag;
            GUILayout.BeginHorizontal();
            myTagEntry = GUILayout.TextField(myTagEntry, TagStore.MaxLength, sField, GUILayout.Width(150));
            if (GUILayout.Button("set", sButton, GUILayout.Width(60))) { TagStore.MyTag = myTagEntry; Menu.Presence?.TouchState(); }
            if (GUILayout.Button("clear", sButton, GUILayout.Width(60))) { myTagEntry = ""; TagStore.MyTag = ""; Menu.Presence?.TouchState(); }
            GUILayout.EndHorizontal();
            Cycle("colour", TagStore.ColourName(TagStore.MyColourHex), Menu.CycleMyTagColour);
            if (Check("show tags above players", Menu.TagsOn)) Menu.ToggleTags();
            EndSection();

            Section("tag someone (only you see these)");
            var roster = Menu.Presence?.Roster;
            if (roster == null || roster.Count == 0) GUILayout.Label("Nobody else in the room.", sDim);
            else
                foreach (var player in roster)
                {
                    var p = player;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(p.Name, sLabel, GUILayout.Width(110));
                    string mine = TagStore.LocalTag(p.UserId);
                    GUILayout.Label(mine ?? "-", sValue, GUILayout.Width(90));
                    if (GUILayout.Button("next", sButton, GUILayout.Width(50))) TagStore.CycleLocalTag(p.UserId, +1);
                    if (GUILayout.Button("colour", sButton, GUILayout.Width(60))) TagStore.CycleLocalColour(p.UserId, +1);
                    if (GUILayout.Button("type", sButton, GUILayout.Width(50))) { tagTarget = p.UserId; tagEntry = mine ?? ""; }
                    GUILayout.EndHorizontal();
                }
            if (tagTarget != null)
            {
                GUILayout.BeginHorizontal();
                tagEntry = GUILayout.TextField(tagEntry, TagStore.MaxLength, sField, GUILayout.Width(150));
                if (GUILayout.Button("save", sButton, GUILayout.Width(60)))
                {
                    TagStore.SetLocalTag(tagTarget, tagEntry, TagStore.ColourOf(tagTarget));
                    tagTarget = null;
                }
                if (GUILayout.Button("cancel", sButton, GUILayout.Width(60))) tagTarget = null;
                GUILayout.EndHorizontal();
            }
            EndSection();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
    }
}
