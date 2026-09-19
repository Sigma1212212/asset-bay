using System;
using System.Threading;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Entry point for loading the menu from outside a Unity project: a Mono injector
    /// (namespace "BundleMenu", class "Loader", method "Inject") or a BepInEx plugin (call Loader.Inject()).
    ///
    /// An injector calls Inject/Eject on ITS OWN thread. Unity APIs are only safe on the main thread (using
    /// them elsewhere is what crashed the game in 1.0.0), so Inject touches no Unity API at all: it only
    /// subscribes to Canvas.willRenderCanvases - a plain managed event Unity raises on the main thread every
    /// frame - and builds everything the first time that fires. If a game somehow never raises it, a
    /// fallback after 5 seconds uses the old direct path.
    /// </summary>
    public static class Loader
    {
        private static GameObject bootstrap;
        private static volatile bool ejectRequested;
        private static int started;               // 0 = idle, 1 = waiting for the main thread / running
        private static Timer fallback;

        public static void Inject()
        {
            Trace("Inject called");
            try
            {
                if (Interlocked.CompareExchange(ref started, 1, 0) != 0) { Trace("already injected - ignored"); return; }
                ejectRequested = false;

                // 1. The guaranteed path first: a plain .NET timer (no Unity API involved, safe on any thread).
                //    It uses the direct start that works from the injector thread, if the hook below hasn't fired.
                fallback = new Timer(_ => FallbackStart(), null, 1500, Timeout.Infinite);
                Trace("fallback timer armed");

                // 2. The nicer path: build on Unity's main thread at the next canvas render. Optional - if touching
                //    Unity's Canvas type from this thread fails in some game, the timer above still starts the menu.
                try
                {
                    Canvas.willRenderCanvases += OnMainThread;
                    Trace("main-thread hook armed");
                }
                catch (Exception e)
                {
                    Trace("main-thread hook unavailable: " + e.GetType().Name + " " + e.Message);
                }
            }
            catch (Exception e)
            {
                Trace("Inject failed: " + e);
                started = 0;
            }
        }

        public static void Eject() => ejectRequested = true;

        private static readonly object StartLock = new object();

        private static void OnMainThread()
        {
            try { Canvas.willRenderCanvases -= OnMainThread; } catch { }
            StartBootstrap("main thread");
        }

        private static void FallbackStart()
        {
            try { Canvas.willRenderCanvases -= OnMainThread; } catch { }
            StartBootstrap("fallback timer");
        }

        private static void StartBootstrap(string via)
        {
            lock (StartLock)
            {
                if (bootstrap != null) return;
                try { fallback?.Dispose(); } catch { }
                fallback = null;
                try
                {
                    bootstrap = new GameObject("[BundleMenu]");
                    bootstrap.AddComponent<Bootstrap>(); // no Awake work: the real setup happens in its Update
                    Trace("bootstrap created via " + via);
                }
                catch (Exception e)
                {
                    Trace("bootstrap failed via " + via + ": " + e);
                    bootstrap = null;
                    started = 0;
                }
            }
        }

        /// <summary>
        /// Writes to %TEMP%\AssetBay-inject.log with plain file IO (works from any thread, even when Unity's
        /// logger can't be used), so a failed injection always leaves a trail.
        /// </summary>
        internal static void Trace(string message)
        {
            try
            {
                System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AssetBay-inject.log"),
                    $"{DateTime.Now:HH:mm:ss.fff}  [{Thread.CurrentThread.ManagedThreadId}]  {message}{Environment.NewLine}");
            }
            catch { /* never let logging break injection */ }
        }

        /// <summary>Runs on the main thread. Builds the menu on its first frame, tears it all down on eject.</summary>
        private sealed class Bootstrap : MonoBehaviour
        {
            private GameObject menuRoot;
            private GorillaTagRig gtRig;

            private void Update()
            {
                if (ejectRequested)
                {
                    Destroy(gameObject); // OnDestroy does the cleanup
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
                        menu.wristOffset = new Vector3(0f, 0.07f, 0.03f);
                    }
                    // VR headset: menu on the wrist, pressed with your finger.
                    // Desktop / PC mode: the desktop GUI window, clicked with the mouse.
                    menu.placement = MenuInput.VRActive ? MenuPlacement.Wrist : MenuPlacement.ClickGui;

                    menuRoot.SetActive(true);
                    Trace("menu built");
                    Debug.Log($"[BundleMenu] Loaded v{menu.versionLabel} ({(gtRig != null ? "Gorilla Tag rig" : "generic camera rig")}). Press Tab to open.");
                }
                catch (Exception e)
                {
                    Trace("menu failed to start: " + e);
                    Debug.LogError("[BundleMenu] Failed to start: " + e);
                    if (menuRoot != null) Destroy(menuRoot);
                    menuRoot = null;
                    enabled = false; // don't retry every frame
                }
            }

            private void OnDestroy()
            {
                // Everything the menu created goes with it: the controller (bundles, canvases, GUI camera,
                // spawned objects, gun visuals), the async helper, and the generated sprites.
                if (menuRoot != null) DestroyImmediate(menuRoot);
                UnityAsync.Shutdown();
                UISprites.ReleaseAll();
                if (bootstrap == gameObject) bootstrap = null;
                ejectRequested = false;
                started = 0;
                Debug.Log("[BundleMenu] Ejected and cleaned up.");
            }
        }
    }
}
