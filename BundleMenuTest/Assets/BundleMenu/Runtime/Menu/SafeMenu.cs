using System;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Safe mode (H): a plain desktop window drawn with Unity's immediate-mode GUI. It shares nothing with
    /// the themed menu - no canvases, sprites, layouts or fonts of its own - so if a theme, menu type or
    /// placement breaks the real UI, this still opens and can put everything back.
    /// </summary>
    public sealed partial class SafeMenu : MonoBehaviour
    {
        public BundleMenuController Menu;
        public KeyCode Key = KeyCode.H;

        public bool Visible { get; private set; }

        private enum Tab { Menu, Mods, Sync, Online, Tablet, Admin, Tools, Info }
        private static readonly string[] TabNames = { "menu", "mods", "sync", "online", "tablet", "admin", "tools", "info" };
        private Tab tab;
        private Rect window = new Rect(80, 80, 680, 520);
        private CursorLockMode savedLock;
        private bool savedVisible;
        private string linkCode = "", error;
        private bool showCode;
        private float confirmResetUntil;

        private void Update()
        {
            // Don't react to H while typing a sync code, or while a mod control is being chosen.
            if (Visible && GUIUtility.keyboardControl != 0) return;
            if (Menu != null && Menu.Mods != null && Menu.Mods.Binds.Listening != null) return;
            if (MenuInput.KeyDown(Key)) SetVisible(!Visible);
            if (Visible) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } // the game re-locks it
        }

        public void SetVisible(bool on)
        {
            if (on == Visible) return;
            Visible = on;
            if (on) { savedLock = Cursor.lockState; savedVisible = Cursor.visible; }
            else { Cursor.lockState = savedLock; Cursor.visible = savedVisible; GUIUtility.keyboardControl = 0; }
        }

        private void OnGUI()
        {
            if (!Visible || Menu == null) return;
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            window = GUI.Window(0x5AFE, window, DrawWindow, GUIContent.none, sWindow);
            window.x = Mathf.Clamp(window.x, 0, Screen.width / scale - 60);
            window.y = Mathf.Clamp(window.y, 0, Screen.height / scale - 30);
        }

        private void DrawWindow(int id)
        {
            // Title bar
            GUI.Label(new Rect(12, 6, 400, 20), "asset bay  |  safe mode", sTitle);
            GUI.Label(new Rect(window.width - 260, 6, 200, 20), $"H to close  |  v{Menu.versionLabel}", sDim);
            if (GUI.Button(new Rect(window.width - 30, 5, 22, 20), "x", sButton)) SetVisible(false);
            Line(new Rect(0, 30, window.width, 1), cAccent);

            // Tab rail on the left
            for (int i = 0; i < TabNames.Length; i++)
            {
                var r = new Rect(0, 40 + i * 34, 86, 30);
                bool on = (int)tab == i;
                if (on) Line(new Rect(0, r.y, 3, r.height), cAccent);
                if (GUI.Button(new Rect(8, r.y, 76, r.height), TabNames[i], on ? sTabOn : sTab)) tab = (Tab)i;
            }
            Line(new Rect(92, 31, 1, window.height - 31), cLine);

            var area = new Rect(102, 40, window.width - 112, window.height - 50);
            GUILayout.BeginArea(area);
            try
            {
                switch (tab)
                {
                    case Tab.Menu: DrawMenuTab(); break;
                    case Tab.Mods: DrawModsTab(); break;
                    case Tab.Sync: DrawSyncTab(); break;
                    case Tab.Online: DrawOnlineTab(); break;
                    case Tab.Tablet: DrawTabletTab(); break;
                    case Tab.Admin: DrawAdminTab(); break;
                    case Tab.Tools: DrawToolsTab(); break;
                    default: DrawInfoTab(); break;
                }
                error = null;
            }
            catch (Exception e)
            {
                // Keep the window alive no matter what the menu does.
                if (error == null) Debug.LogException(e);
                error = e.GetType().Name + ": " + e.Message;
            }
            if (error != null) GUILayout.Label("error: " + error, sError);
            GUILayout.EndArea();

            GUI.DragWindow(new Rect(0, 0, window.width, 30));
        }
    }
}
