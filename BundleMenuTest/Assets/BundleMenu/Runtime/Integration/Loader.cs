using System;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Entry point for loading the menu from outside a Unity project: a Mono injector
    /// (namespace "BundleMenu", class "Loader", method "Inject") or a BepInEx plugin (call Loader.Inject()).
    ///
    /// IMPORTANT: an injector calls Inject/Eject on ITS OWN thread, not Unity's main thread. Unity APIs
    /// that touch the renderer (textures, canvases, materials) crash the game when used off the main
    /// thread ("Graphics device is null"). So Inject only creates one empty GameObject with a tiny
    /// bootstrap component; the real setup runs in that component's first Update, on the main thread.
    /// Eject just raises a flag that the same component acts on.
    /// </summary>
    public static class Loader
    {
        private static GameObject bootstrap;
        private static volatile bool ejectRequested;

        public static void Inject()
        {
            try
            {
                if (bootstrap != null) return;
                ejectRequested = false;
                bootstrap = new GameObject("[BundleMenu]");
                bootstrap.AddComponent<Bootstrap>(); // no Awake/OnEnable work: safe from any thread
            }
            catch (Exception e)
            {
                Debug.LogError("[BundleMenu] Inject failed: " + e);
            }
        }

        public static void Eject() => ejectRequested = true;

        /// <summary>Runs on the main thread. Builds the menu on its first frame, tears it down on eject.</summary>
        private sealed class Bootstrap : MonoBehaviour
        {
            private GameObject menuRoot;
            private GorillaTagRig gtRig;

            private void Update()
            {
                if (ejectRequested)
                {
                    if (menuRoot != null) Destroy(menuRoot);
                    Destroy(gameObject);
                    bootstrap = null;
                    ejectRequested = false;
                    Debug.Log("[BundleMenu] Ejected.");
                    return;
                }

                if (menuRoot == null) Build();
                if (gtRig != null) gtRig.Tick();
            }

            private void Build()
            {
                try
                {
                    DontDestroyOnLoad(gameObject);

                    menuRoot = new GameObject("[BundleMenu] Menu");
                    DontDestroyOnLoad(menuRoot);
                    menuRoot.SetActive(false); // configure before Awake runs

                    var menu = menuRoot.AddComponent<BundleMenuController>();
                    menu.brand = "Asset Bay";
                    // Injected: show the DLL's own version (set by publish.ps1), not the game's.
                    var v = typeof(Loader).Assembly.GetName().Version;
                    if (v != null) menu.versionLabel = $"{v.Major}.{v.Minor}.{v.Build}";
                    menu.rememberSettings = true;

                    // Pick up the player's head and hands if this is Gorilla Tag; otherwise stay generic.
                    gtRig = GorillaTagRig.TryCreate();
                    if (gtRig != null)
                    {
                        menu.Rig = gtRig;
                        menu.placement = MenuPlacement.Wrist;
                        menu.wristOffset = new Vector3(0f, 0.07f, 0.03f);
                    }

                    menuRoot.SetActive(true);
                    Debug.Log($"[BundleMenu] Loaded v{menu.versionLabel} ({(gtRig != null ? "Gorilla Tag rig" : "generic camera rig")}). Press Tab to open.");
                }
                catch (Exception e)
                {
                    Debug.LogError("[BundleMenu] Failed to start: " + e);
                    if (menuRoot != null) Destroy(menuRoot);
                    menuRoot = null;
                    enabled = false; // don't retry every frame
                }
            }

            private void OnDestroy()
            {
                if (menuRoot != null) Destroy(menuRoot);
                if (bootstrap == gameObject) bootstrap = null;
            }
        }
    }
}
