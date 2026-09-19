using System;
using UnityEngine;

namespace BundleMenu
{
    public sealed partial class SafeMenu
    {
        // ------------------------------------------------------------------ tabs

        private void DrawMenuTab()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(250));
            Section("main menu");
            Value("state", Menu.IsOpen ? "open" : "closed");
            Value("toggle key", Menu.toggleKey.ToString());
            if (Button(Menu.IsOpen ? "close menu" : "open menu")) Menu.Toggle();
            if (Button("rebuild ui")) Menu.RebuildUI();
            if (Button("reset look to default")) Menu.ResetLook();
            EndSection();

            Section("placement");
            foreach (MenuPlacement p in Enum.GetValues(typeof(MenuPlacement)))
                if (Check(SettingsPage.PlacementName(p), Menu.Placement == p)) Menu.SetPlacement(p);
            EndSection();
            GUILayout.EndVertical();

            GUILayout.Space(8);
            GUILayout.BeginVertical();
            Section("look");
            Cycle("theme", Menu.ThemeName, Menu.CycleTheme);
            Cycle("menu type", Menu.MenuStyleName, Menu.CycleMenuStyle);
            Cycle("gui size", Menu.GuiSizeName, Menu.CycleGuiSize);
            Cycle("gui view", Menu.GuiViewName, Menu.CycleGuiView);
            EndSection();

            Section("animation");
            Cycle("entrance", Menu.EntranceName, Menu.CycleEntrance);
            Cycle("stagger", Menu.Stagger.ToString(), Menu.CycleStagger);
            Cycle("speed", $"{Menu.AnimationSpeed:0.##}x", Menu.CycleSpeed);
            if (Button("replay animation")) Menu.ReplayEntrance();
            EndSection();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawSyncTab()
        {
            var sync = Menu.Sync;
            Section("your sync code");
            GUILayout.Label("Your settings are saved on the Asset Bay server under this code. Enter it on another PC " +
                            "to bring them over. Anyone with the code can change your settings, so keep it to yourself.", sDimWrap);
            GUILayout.BeginHorizontal();
            GUILayout.Label(showCode ? SettingsSync.Pretty(sync.Code) : "••••-••••-••••", sCode, GUILayout.Width(200));
            if (GUILayout.Button(showCode ? "hide" : "show", sButton, GUILayout.Width(60))) showCode = !showCode;
            if (GUILayout.Button("copy", sButton, GUILayout.Width(60))) GUIUtility.systemCopyBuffer = SettingsSync.Pretty(sync.Code);
            GUILayout.EndHorizontal();
            Value("status", sync.Busy ? "working..." : sync.Status);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("save to server now", sButton)) sync.Upload().Forget();
            if (GUILayout.Button("load from server", sButton)) sync.Download(applyOnlyIfNewer: false).Forget();
            GUILayout.EndHorizontal();
            EndSection();

            Section("use a code from another pc");
            GUILayout.BeginHorizontal();
            linkCode = GUILayout.TextField(linkCode, 20, sField, GUILayout.Width(200));
            GUI.enabled = !sync.Busy && linkCode.Trim().Length > 0;
            if (GUILayout.Button("link", sButton, GUILayout.Width(80))) LinkAsync(linkCode).Forget();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label("Linking replaces this PC's settings with the ones saved under that code.", sDimWrap);
            EndSection();
        }

        private async System.Threading.Tasks.Task LinkAsync(string code)
        {
            if (await Menu.Sync.Link(code)) { linkCode = ""; showCode = false; }
        }

        private void DrawOnlineTab()
        {
            Section("other asset bay users");
            var pr = Menu.Presence;
            if (pr == null) GUILayout.Label("Only in Gorilla Tag.", sDim);
            else
            {
                if (Check("menu sharing", Menu.MenuSharing)) Menu.ToggleMenuSharing();
                Cycle("others use it", Menu.RemoteControlName, Menu.CycleRemoteControl);
                GUILayout.Label("browse = pages, videos, theme. full = everything, mods too. Always back to off when the game restarts.", sDimWrap);
                Value("in this room", pr.Others.Count.ToString());
                Value("connection", string.IsNullOrEmpty(pr.LastError) ? "ok" : pr.LastError);
                foreach (var (player, state) in pr.Others)
                    Value("  " + player.Name, (state.open ? "menu open" : "menu closed") + $" · {state.theme} / {state.style}");
            }
            EndSection();

            Section("solo test (no friend needed)");
            var solo = Menu.Solo;
            if (solo == null) GUILayout.Label("Gorilla Tag only.", sDim);
            else
            {
                if (Check(solo.Active ? "running" : "start a test ghost", solo.Active)) Menu.ToggleSoloTest();
                Value("state", solo.Status);
                if (solo.Active)
                {
                    GUILayout.Label("A test ghost joined your room: it has a nametag and a menu card you can press, " +
                                    "and it can press your menu back. Turn on 'let others use my menu' for presses to land.", sDimWrap);
                    var seen = solo.SeenFromOutside;
                    Value("ghost sees", seen == null ? "nothing yet" : $"{seen.page} ({seen.rows?.Length ?? 0} rows)");
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("press row 1", sButton)) Toast(solo.PressRow(0));
                    if (GUILayout.Button("press row 2", sButton)) Toast(solo.PressRow(1));
                    if (GUILayout.Button("next page", sButton)) Toast(solo.PressAction("next"));
                    if (GUILayout.Button("back", sButton)) Toast(solo.PressAction("back"));
                    GUILayout.EndHorizontal();
                    if (Button("move the ghost in front of me")) solo.Recall();
                    foreach (var line in solo.Log) GUILayout.Label(line, sDim);
                }
            }
            EndSection();

            Section("broadcasts");
            if (Check("broadcast screens", Menu.BroadcastScreensOn)) Menu.ToggleBroadcastScreens();
            Value("feed", Menu.Feed?.Current != null ? "loaded" : "not loaded");
            if (Button("check feed now")) Menu.RefreshFeedAsync().Forget();
            EndSection();
        }

        /// <summary>Show a one-line result from the solo test in the window and the game.</summary>
        private void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            error = null;
            Menu.Toast(message, ToastKind.Info);
        }

        private void DrawToolsTab()
        {
            Section("bundles");
            Value("source", Menu.SourceMode.ToString());
            if (Button("rescan bundles")) Menu.Rescan();
            if (Button($"clear spawned ({Menu.Spawner.Count})")) Menu.ClearSpawned();
            if (Button("unload everything")) Menu.UnloadEverything();
            EndSection();

            Section("reset");
            bool armed = Time.unscaledTime < confirmResetUntil;
            if (Button(armed ? "click again to reset everything" : "reset all settings"))
            {
                if (armed) { Menu.FactoryReset(); confirmResetUntil = 0f; }
                else confirmResetUntil = Time.unscaledTime + 3f;
            }
            GUILayout.Label("Puts every setting back to default. Your sync code stays.", sDimWrap);
            EndSection();
        }

        private void DrawInfoTab()
        {
            Section("about");
            Value("version", Menu.versionLabel);
            Value("unity", Application.unityVersion);
            Value("backend", Menu.backendUrl);
            Value("mods", Menu.Mods.Available ? (Menu.Mods.Allowed ? "allowed here" : "paused (public lobby)") : "gorilla tag only");
            EndSection();

            Section("logs");
            GUILayout.Label("Game log:  %USERPROFILE%\\AppData\\LocalLow\\Another Axiom\\Gorilla Tag\\Player.log", sDimWrap);
            GUILayout.Label("Inject log:  %TEMP%\\AssetBay-inject.log", sDimWrap);
            EndSection();
        }

        // ------------------------------------------------------------------ building blocks

        private void Section(string title)
        {
            GUILayout.BeginVertical(sBox);
            GUILayout.Label(title, sSection);
        }

        private static void EndSection()
        {
            GUILayout.EndVertical();
            GUILayout.Space(6);
        }

        private bool Button(string text) => GUILayout.Button(text, sButton);

        private void Value(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, sLabel, GUILayout.Width(110));
            GUILayout.Label(value ?? "-", sValue);
            GUILayout.EndHorizontal();
        }

        /// <summary>A square tick box row. Returns true when clicked.</summary>
        private bool Check(string label, bool on)
        {
            GUILayout.BeginHorizontal();
            bool clicked = GUILayout.Button("", on ? sCheckOn : sCheck, GUILayout.Width(14), GUILayout.Height(14));
            clicked |= GUILayout.Button(label, sLabel);
            GUILayout.EndHorizontal();
            return clicked;
        }

        private void Cycle(string label, string value, Action<int> step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, sLabel, GUILayout.Width(80));
            if (GUILayout.Button("<", sButton, GUILayout.Width(24))) step(-1);
            GUILayout.Label(value, sValueCentre);
            if (GUILayout.Button(">", sButton, GUILayout.Width(24))) step(+1);
            GUILayout.EndHorizontal();
        }
    }
}
