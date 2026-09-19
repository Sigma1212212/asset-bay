using System;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Entry point for loading the menu from outside a Unity project: a Mono injector
    /// (namespace "BundleMenu", class "Loader", method "Inject") or a BepInEx plugin (call Loader.Inject()).
    /// Safe to call more than once. <see cref="Eject"/> removes everything again.
    /// </summary>
    public static class Loader
    {
        private static GameObject root;

        public static void Inject()
        {
            try
            {
                if (root != null) return;

                root = new GameObject("[BundleMenu]");
                UnityEngine.Object.DontDestroyOnLoad(root);
                root.SetActive(false); // configure before Awake runs

                var menu = root.AddComponent<BundleMenuController>();
                menu.brand = "Asset Bay";
                // Injected: show the DLL's own version (set by publish.ps1), not the game's.
                var v = typeof(Loader).Assembly.GetName().Version;
                if (v != null) menu.versionLabel = $"{v.Major}.{v.Minor}.{v.Build}";
                menu.rememberSettings = true;

                // Pick up the player's head and hands if this is Gorilla Tag; otherwise stay generic.
                var gt = GorillaTagRig.TryCreate();
                if (gt != null)
                {
                    menu.Rig = gt;
                    menu.placement = MenuPlacement.Wrist;
                    menu.wristOffset = new Vector3(0f, 0.07f, 0.03f);
                    root.AddComponent<GorillaTagRig.Binder>().Rig = gt;
                }

                root.SetActive(true);
                Debug.Log($"[BundleMenu] Injected ({(gt != null ? "Gorilla Tag rig" : "generic camera rig")}). Press Tab to open.");
            }
            catch (Exception e)
            {
                Debug.LogError("[BundleMenu] Inject failed: " + e);
            }
        }

        public static void Eject()
        {
            if (root == null) return;
            UnityEngine.Object.Destroy(root);
            root = null;
            Debug.Log("[BundleMenu] Ejected.");
        }
    }
}
