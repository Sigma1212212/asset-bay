using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// The mods tab of the H window: switch mods on, see and change their keys, and spawn a prop -
    /// all without the themed menu. If a theme ever breaks the real UI, everything still works here.
    /// </summary>
    public sealed partial class SafeMenu
    {
        private Vector2 modScroll, propScroll;

        private void DrawModsTab()
        {
            var runner = Menu.Mods;
            var binds = runner.Binds;
            bool vr = MenuInput.VRActive;

            if (!runner.Available)
            {
                Section("mods");
                GUILayout.Label("Mods control your Gorilla Tag player, so they only work with the menu loaded into the game.", sDimWrap);
                EndSection();
                DrawPropList();
                return;
            }

            Section("mods");
            Value("lobby", runner.Allowed ? "mods allowed" : "paused (public lobby)");
            if (binds.Listening != null)
                GUILayout.Label("Press a " + (binds.ListeningForVR ? "controller button" : "key") + " for " + binds.Listening.Name +
                                "  -  backspace clears it, escape cancels.", sDimWrap);

            modScroll = GUILayout.BeginScrollView(modScroll, GUILayout.Height(190));
            foreach (var mod in runner.Mods)
            {
                var m = mod;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button((m.Enabled ? "[on]  " : "[  ]  ") + m.Name, sButton, GUILayout.Width(210)))
                    Menu.Toast(runner.Toggle(m), ToastKind.Info);
                GUILayout.Label(m.Enabled ? m.Hint : "", sDim, GUILayout.Width(170));
                if (GUILayout.Button(vr ? m.Bind.ButtonText : m.Bind.KeyText, sButton, GUILayout.Width(110)))
                    binds.Listen(m.Bind, vr);
                if (m.Trigger != ModTrigger.Switch &&
                    GUILayout.Button(m.Bind.Sticky ? "tap" : "hold", sButton, GUILayout.Width(60)))
                {
                    m.Bind.Sticky = !m.Bind.Sticky;
                    binds.Release(m.Name);
                    binds.Save();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("all off", sButton, GUILayout.Width(110))) runner.DisableAll();
            if (GUILayout.Button("controls back to normal", sButton, GUILayout.Width(220))) Menu.ResetModControls();
            GUILayout.EndHorizontal();
            EndSection();

            DrawPropList();
        }

        private void DrawPropList()
        {
            Section("props");
            GUILayout.Label("Built into the menu - spawns in front of you, only you can see it.", sDimWrap);
            propScroll = GUILayout.BeginScrollView(propScroll, GUILayout.Height(150));
            foreach (var prop in PropLibrary.All)
            {
                var def = prop;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(def.Name, sButton, GUILayout.Width(170))) Menu.SpawnProp(def);
                if (GUILayout.Button("to the gun", sButton, GUILayout.Width(110))) Menu.ChooseProp(def);
                GUILayout.Label(def.About, sDim);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"clear what I spawned ({Menu.Spawner.Count})", sButton, GUILayout.Width(220))) Menu.ClearSpawned();
            var test = Menu.PropTest;
            if (GUILayout.Button(test != null && test.Running ? "checking..." : "check every prop", sButton, GUILayout.Width(170))) Menu.TestProps();
            GUILayout.EndHorizontal();
            if (test != null) GUILayout.Label(test.Running ? test.Step : test.Summary, sDim);
            EndSection();
        }
    }
}
