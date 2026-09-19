using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BundleMenu
{
    public enum BundleSourceMode { Dummy, Local }
    public enum ToastKind { Info, Success, Error }

    /// <summary>
    /// The one component you add to a scene. It wires the layers together:
    ///   BundleService (data)  ←  pages (rows)  →  MenuView (GameObjects)  ←  MenuAnimator (motion)
    /// and owns placement, input, navigation and settings. Everything is built at runtime,
    /// so there is no prefab or hierarchy to set up by hand.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class BundleMenuController : MonoBehaviour
    {
        [Header("General")]
        public string brand = "Asset Bay";
        [Tooltip("Version shown in the header. Empty = Application.version.")]
        public string versionLabel = "";
        [Tooltip("Open the menu as soon as the scene starts.")]
        public bool startOpen;
        [Range(3, 10)] public int rowsPerPage = 6;
        [Tooltip("Remember runtime changes (animation, theme, placement...) between sessions via PlayerPrefs.")]
        public bool rememberSettings = true;

        [Header("Input")]
        public KeyCode toggleKey = KeyCode.Tab;
        public KeyCode cycleEntranceKey = KeyCode.F2;
        public KeyCode cyclePlacementKey = KeyCode.F3;
        public KeyCode cycleThemeKey = KeyCode.F4;
        public VRToggleButton vrToggle = VRToggleButton.LeftSecondary;
        [Tooltip("Small always-visible button in the top-right corner that opens/closes the menu.")]
        public bool showToggleButton = true;
        [Tooltip("Free and show the mouse cursor while the menu is open, restore it afterwards.")]
        public bool unlockCursorWhileOpen = true;

        [Header("Animation")]
        public EntranceStyle entrance = EntranceStyle.Staggered;
        public StaggerMode stagger = StaggerMode.Auto;
        [Range(0.25f, 3f)] public float animationSpeed = 1f;

        [Header("Look")]
        public ThemePreset theme = ThemePreset.Halo;
        [Tooltip("Used when Theme = Custom.")]
        public MenuTheme customTheme;
        [Tooltip("Menu type (the panel's shape). -1 = whatever the theme pairs with.")]
        public int menuStyle = -1;

        [Header("Placement")]
        public MenuPlacement placement = MenuPlacement.ScreenRight;
        [Tooltip("Camera the world-space menu faces / follows. Empty = Camera.main.")]
        public Camera viewCamera;
        [Tooltip("Metres in front of the camera for Floating placement.")]
        public float floatingDistance = 0.8f;
        [Tooltip("Floating panel re-centres when you look more than this many degrees away.")]
        public float followAngle = 32f;
        [Tooltip("Hand / wrist transform for Wrist placement. Empty = simulated wrist in front of the camera.")]
        public Transform wristAnchor;
        public Vector3 wristOffset = new Vector3(0f, 0.06f, 0.02f);
        public bool wristFacesCamera = true;
        [Tooltip("World size of one UI unit, in metres. 0.001 = the 440-unit panel is 44 cm wide.")]
        public float worldScale = 0.0009f;
        [Range(0.2f, 1f)] public float wristSizeMultiplier = 0.55f;
        [Tooltip("Fingertip transforms that can press world-space buttons (VR).")]
        public List<Transform> pokeTips = new List<Transform>();
        [Header("Broadcasts")]
        [Tooltip("Asset Bay backend (the Cloudflare Worker). Feed, videos and the tablet bundle come from here.")]
        public string backendUrl = FeedClient.DefaultBaseUrl;
        [Tooltip("Show video screens the feed places in the world. Off by default; players opt in.")]
        public bool broadcastScreens;
        [Tooltip("Let other Asset Bay users in your room see your menu (theme, page, video). Only hashed ids are sent.")]
        public bool menuSharing = true;

        [Header("Gun")]
        [Tooltip("Desktop: hold this to aim (right mouse by default), left-click to fire. VR: right grip + trigger.")]
        public KeyCode gunAimKey = KeyCode.Mouse1;
        [Tooltip("VR hand the gun points from when not using a game rig. Aims along its forward axis.")]
        public Transform gunHand;

        [Tooltip("Click GUI window size relative to the panel's design size.")]
        [Range(0.6f, 1.6f)] public float guiScale = 1f;
        [Tooltip("Click GUI window centre, 0..1 of the screen. Remembered when you drag it.")]
        public Vector2 guiPosition = new Vector2(0.5f, 0.5f);
        [Tooltip("Desktop GUI panes: the flat 2D menu, the live 3D preview, or both side by side.")]
        public GuiView guiView = GuiView.Both;

        [Header("Content")]
        public BundleSourceMode sourceMode = BundleSourceMode.Dummy;
        [Tooltip("Folder for the Local source. Empty = StreamingAssets/Bundles.")]
        public string localFolder = "";
        [Range(0f, 1f)] public float dummyFailRate;
        [Tooltip("Unload(true): also destroy objects that came from the bundle. Off = Unload(false), spawned copies survive.")]
        public bool unloadDestroysSpawned;

        // ------------------------------------------------------------------ public state

        public BundleService Service { get; private set; }
        public AssetSpawner Spawner { get; private set; }
        public GunLib Gun { get; private set; }
        public ModRunner Mods { get; private set; }
        public Checkpoint Checkpoint { get; } = new Checkpoint();
        public FeedClient Feed { get; private set; }
        public VideoTablet Tablet { get; private set; }
        public BroadcastScreens Screens { get; private set; }
        public bool BroadcastScreensOn => broadcastScreens;
        public MenuAnimator Animator { get; private set; }
        public MenuTheme CurrentTheme { get; private set; }
        public PresenceClient Presence { get; private set; }
        public RemoteMenus Remote { get; private set; }
        /// <summary>The panel shape in use: the player's pick, or the theme's partner style.</summary>
        public MenuStyle ActiveStyle => menuStyle >= 0 && Enum.IsDefined(typeof(MenuStyle), menuStyle)
            ? (MenuStyle)menuStyle : CurrentTheme != null ? CurrentTheme.Style : MenuStyle.Classic;
        public string MenuStyleName => (menuStyle < 0 ? "Match theme: " : "") + MenuStyles.Name(ActiveStyle);
        public bool MenuSharing => menuSharing;
        public bool IsOpen => Animator != null && Animator.PanelTargetOpen;

        /// <summary>Head / hands provider. Game integrations replace this (see GorillaTagRig).</summary>
        public IRigProvider Rig
        {
            get => rig ?? (rig = new InspectorRig(this));
            set { rig = value; if (poker != null) poker.Rig = Rig; }
        }

        public EntranceStyle Entrance => entrance;
        public StaggerMode Stagger => stagger;
        public float AnimationSpeed => animationSpeed;
        public MenuPlacement Placement => placement;
        public BundleSourceMode SourceMode => sourceMode;
        public float DummyFailRate => dummyFailRate;
        public bool UnloadDestroysSpawned => unloadDestroysSpawned;

        public event Action<bool> OpenChanged;

        // ------------------------------------------------------------------ internals

        private IRigProvider rig;
        private MenuView view;
        private PokeInteractor poker;
        private Canvas screenCanvas, worldCanvas;
        private RectTransform host;
        private MenuButton toggleButton;
        private readonly Stack<MenuPage> pages = new Stack<MenuPage>();
        private bool dirty;
        private Transform simulatedWrist;
        private CursorLockMode savedLock;
        private bool savedVisible, cursorSaved;
        private DummyBundleSource dummySource;
        private ClickGui gui;
        private MenuView flatView;          // the 2D pane of the desktop GUI
        private MenuAnimator flatAnimator;  // its own entrance timeline
        private DesktopPointer pointer;
        private readonly List<MenuButton> buttonCache = new List<MenuButton>();
        private readonly List<MenuButton> overlayScratch = new List<MenuButton>();
        private readonly List<MenuButton> worldScratch = new List<MenuButton>();

        private static readonly float[] Speeds = { 0.5f, 0.75f, 1f, 1.5f, 2f };
        private static readonly float[] FailRates = { 0f, 0.25f, 0.5f, 1f };
        private static readonly float[] GuiSizes = { 0.85f, 1f, 1.15f };
        private const int PrefsVersion = 3; // bump to drop saved placement once (3: desktop players get the Desktop GUI)
        private const string Prefs = "BundleMenu.";

        // ================================================================== lifecycle

        private void Awake()
        {
            CaptureDefaults();
            if (rememberSettings) { LoadPrefs(); LoadSavedAt(); }

            Animator = gameObject.AddComponent<MenuAnimator>();
            Animator.Speed = animationSpeed;
            Animator.PanelUpdated += ApplyPanelProgress;
            Animator.PanelClosed += OnPanelClosed;

            Spawner = gameObject.AddComponent<AssetSpawner>();

            Service = new BundleService(CreateSource(sourceMode)) { UnloadAllLoadedObjects = unloadDestroysSpawned };
            Service.EntryChanged += _ => dirty = true;
            Service.CatalogChanged += OnCatalogChanged;
            Service.BundleUnloading += Spawner.OnBundleUnloading;

            EnsureEventSystem();
            BuildCanvases();

            poker = gameObject.AddComponent<PokeInteractor>();
            poker.Rig = Rig;
            poker.Root = host;

            gui = new ClickGui(screenCanvas);
            gui.ViewChanged += v =>
            {
                guiView = v;
                SavePrefs();
                if (!IsOpen || placement != MenuPlacement.ClickGui) return;
                ApplyPlacement();
                Render(animate: true);
            };
            gui.CloseRequested += Close;
            var flatHost = UIFactory.Rect("BundleMenu Flat", gui.FlatSlot).Stretch();
            flatView = flatHost.gameObject.AddComponent<MenuView>();
            flatAnimator = gameObject.AddComponent<MenuAnimator>();

            pointer = gameObject.AddComponent<DesktopPointer>();
            pointer.OverlayButtons = OverlayButtons;
            pointer.WorldButtons = WorldButtons;
            pointer.Gui = () => placement == MenuPlacement.ClickGui && host.gameObject.activeSelf ? gui : null;
            pointer.PanelHost = () => host;
            pointer.WorldCamera = () => Rig.Camera;
            pointer.WorldMouseEnabled = () => placement == MenuPlacement.Floating || placement == MenuPlacement.Wrist;
            pointer.OnScroll = dir => { if (IsOpen) Page(dir); };
            pointer.OnWindowDragged = pos => { guiPosition = pos; SavePrefs(); };

            Gun = gameObject.AddComponent<GunLib>();
            Gun.AimKey = gunAimKey;
            Gun.Rig = () => Rig;
            Gun.Theme = () => CurrentTheme;
            Gun.Blocked = () => pointer != null && pointer.OverMenu;
            Gun.Report = message => { Toast(message, ToastKind.Info); dirty = true; };
            Gun.Register(new PlaceMode(Spawner));
            Gun.Register(new DeleteMode(Spawner));
            Gun.Register(new InspectMode());
            Gun.Register(new MeasureMode());

            Mods = gameObject.AddComponent<ModRunner>();
            Mods.Init(new ModContext
            {
                Player = GorillaTagPlayer.TryCreate(),
                Rig = Rig as GorillaTagRig,
                Theme = () => CurrentTheme,
                GunUsesRightGrip = () => Gun != null && Gun.GunEnabled,
                Toast = message => Toast(message, ToastKind.Info),
                ScreenCanvas = screenCanvas,
            });
            Mods.Changed += () => dirty = true;

            Feed = new FeedClient(backendUrl);
            Tablet = gameObject.AddComponent<VideoTablet>();
            Tablet.ViewCamera = () => Rig.Camera;
            Tablet.Theme = () => CurrentTheme;
            Tablet.Report = message => { Toast(message, ToastKind.Info); dirty = true; };
            Tablet.PackLoaded = LoadPackThemes;
            Tablet.LeftHand = () => (Rig as GorillaTagRig)?.WristAnchor;
            Tablet.RightHand = () => (Rig as GorillaTagRig)?.RightHandTransform;
            Screens = gameObject.AddComponent<BroadcastScreens>();
            Screens.ScreensEnabled = broadcastScreens;
            Screens.ViewCamera = () => Rig.Camera;
            if (Mods.Available) Gun.Register(new GrappleMode(Mods.Context));

            // Menus seeing each other: only in Gorilla Tag (needs the room and player ids).
            var gtPlayer = Mods.Context.Player;
            if (gtPlayer != null)
            {
                Presence = gameObject.AddComponent<PresenceClient>();
                Presence.Player = gtPlayer;
                Presence.Endpoint = Feed.BaseUrl + "presence";
                Presence.Sharing = menuSharing;
                Presence.LocalState = BuildPresenceState;
                Presence.Changed += () => dirty = true;
                Remote = gameObject.AddComponent<RemoteMenus>();
                Remote.Presence = Presence;
                Remote.ViewCamera = () => Rig.Camera;
            }

            SetupSync();

            CurrentTheme = ResolveTheme(theme);
            BuildView();
            pages.Push(new LibraryPage());

            host.gameObject.SetActive(false);
            Animator.SetPanelOpen(false, instant: true);
        }

        private void Start()
        {
            if (Service == null) { enabled = false; return; } // Awake failed; its error is already in the log
            Service.RefreshCatalogAsync().Forget();
            FeedLoop().Forget();
            if (startOpen) Open();
        }

        private void OnDestroy()
        {
            Service?.Dispose();
            if (worldCanvas != null) Destroy(worldCanvas.gameObject);
            gui?.Dispose();
            if (simulatedWrist != null) Destroy(simulatedWrist.gameObject);
            RestoreCursor();
        }

        private void Update()
        {
            if (MenuInput.KeyDown(toggleKey) || MenuInput.VRButtonDown(vrToggle)) Toggle();
            if (MenuInput.KeyDown(cycleEntranceKey)) CycleEntrance(+1);
            if (MenuInput.KeyDown(cyclePlacementKey)) CyclePlacement(+1);
            if (MenuInput.KeyDown(cycleThemeKey)) CycleTheme(+1);

            if (!IsOpen) return;

            if (MenuInput.KeyDown(KeyCode.Escape)) { if (pages.Count > 1) Back(); else Close(); }
            if (MenuInput.KeyDown(KeyCode.Backspace)) Back();
            if (MenuInput.KeyDown(KeyCode.PageDown)) Page(+1);
            if (MenuInput.KeyDown(KeyCode.PageUp)) Page(-1);

            // Arrow keys with nothing focused: focus the first row so keyboard/gamepad nav can start.
            if ((MenuInput.KeyDown(KeyCode.DownArrow) || MenuInput.KeyDown(KeyCode.UpArrow)) && EventSystem.current != null)
            {
                var sel = EventSystem.current.currentSelectedGameObject;
                var navView = FlatActive ? flatView : view;
                if (sel == null || !sel.transform.IsChildOf(navView.transform))
                {
                    var first = navView.FirstRowButton();
                    if (first != null) first.Select();
                }
            }
        }

        private void LateUpdate()
        {
            if (host.gameObject.activeSelf) UpdateWorldPose(snap: false);

            if (Gun != null && Gun.Aiming && host.gameObject.activeSelf) dirty = true; // live target readout

            bool rendered = false;
            if (dirty && host.gameObject.activeSelf)
            {
                dirty = false;
                Render(animate: false);
                rendered = true;
            }

            if (gui.Visible)
                gui.Tick(MenuInput.MousePosition,
                    activity: rendered || Animator.IsPanelAnimating || Animator.IsPlayingItems || pointer.OverMenu);
        }

        /// <summary>True while the desktop GUI's 2D pane is on screen.</summary>
        private bool FlatActive => placement == MenuPlacement.ClickGui && gui.Visible && gui.View != GuiView.Preview;

        // ================================================================== open / close

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            if (IsOpen) return;
            host.gameObject.SetActive(true);
            ApplyPlacement();
            UpdateWorldPose(snap: true);
            Animator.SetPanelOpen(true);
            Render(animate: true, delay: 0.06f);
            GrabCursor();
            OpenChanged?.Invoke(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            Animator.SetPanelOpen(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            RestoreCursor();
            OpenChanged?.Invoke(false);
        }

        private void OnPanelClosed()
        {
            Animator.StopEntrance();
            host.gameObject.SetActive(false);
            gui?.Hide();
        }

        private void ApplyPanelProgress(float p, bool opening)
        {
            if (view == null || view.Panel == null) return;
            // Ease out on the way in, ease in on the way out: both feel snappy at the "visible" end.
            float e = opening ? Ease.OutCubic(p) : p * p;

            if (placement == MenuPlacement.ClickGui)
            {
                // The panel sits still inside the hidden render; the on-screen window does the moving.
                view.Panel.anchoredPosition = Vector2.zero;
                view.Panel.localScale = Vector3.one;
                view.PanelGroup.alpha = 1f;
                view.PanelGroup.interactable = opening && p > 0.5f;
                float gs = Mathf.Lerp(0.9f, 1f, opening ? Ease.OutBack(p, 1.6f) : e) * gui.BaseScale;
                gui.Window.localScale = new Vector3(gs, gs, 1f);
                gui.Group.alpha = Mathf.Clamp01(e * 1.4f);
                gui.Group.blocksRaycasts = opening;
                var line = new Vector3(Ease.InOutCubic(Mathf.Clamp01(p * 1.3f - 0.3f)), 1f, 1f);
                if (view.AccentLine != null) view.AccentLine.localScale = line;
                if (flatView != null && flatView.Panel != null)
                {
                    flatView.PanelGroup.alpha = 1f;
                    flatView.PanelGroup.interactable = opening && p > 0.5f;
                    if (flatView.AccentLine != null) flatView.AccentLine.localScale = line;
                }
                gui.RequestRender();
                return;
            }

            if (placement == MenuPlacement.ScreenRight)
            {
                view.Panel.anchoredPosition = new Vector2((1f - e) * (view.PanelWidth * 0.6f), 0f);
                view.Panel.localScale = Vector3.one;
            }
            else
            {
                view.Panel.anchoredPosition = new Vector2(0f, (1f - e) * -30f);
                float s = Mathf.Lerp(0.82f, 1f, opening ? Ease.OutBack(p, 1.3f) : e);
                view.Panel.localScale = new Vector3(s, s, 1f);
            }

            view.PanelGroup.alpha = Mathf.Clamp01(e * 1.4f);
            view.PanelGroup.blocksRaycasts = opening;
            view.PanelGroup.interactable = opening && p > 0.5f;
            if (view.AccentLine != null)
                view.AccentLine.localScale = new Vector3(Ease.InOutCubic(Mathf.Clamp01(p * 1.3f - 0.3f)), 1f, 1f);
        }

        // ================================================================== navigation & rendering

        public void Navigate(MenuPage page)
        {
            pages.Push(page);
            Render(animate: true);
        }

        public void Back()
        {
            if (pages.Count <= 1) return;
            pages.Pop();
            Render(animate: true);
        }

        public void Home()
        {
            while (pages.Count > 1) pages.Pop();
            Render(animate: true);
        }

        public void Page(int delta)
        {
            var page = pages.Peek();
            int count = PageCount(page.BuildRows(this).Count);
            if (count <= 1) return;
            page.PageIndex = (page.PageIndex + delta + count) % count;
            Render(animate: true);
        }

        private int PageCount(int rows) => Mathf.Max(1, Mathf.CeilToInt(rows / (float)rowsPerPage));

        private void Render(bool animate, float delay = 0f)
        {
            if (view == null) return;

            // Drop pages whose content disappeared (e.g. a category after switching source).
            while (pages.Count > 1 && !pages.Peek().IsValid(this)) pages.Pop();

            var page = pages.Peek();
            var all = page.BuildRows(this);
            int count = PageCount(all.Count);
            page.PageIndex = Mathf.Clamp(page.PageIndex, 0, count - 1);
            var slice = all.Skip(page.PageIndex * rowsPerPage).Take(rowsPerPage).ToList();

            RenderInto(view, Animator, slice, page, count, animate, delay);
            if (FlatActive) RenderInto(flatView, flatAnimator, slice, page, count, animate, delay);
            gui?.RequestRender();
        }

        private void RenderInto(MenuView target, MenuAnimator anim, List<RowSpec> slice, MenuPage page, int count, bool animate, float delay)
        {
            target.SetTitle(page.Title);
            target.SetBackVisible(pages.Count > 1);
            target.SetPaging(page.PageIndex, count);

            if (animate)
            {
                var items = target.ShowRows(slice);
                // A theme can prefer its own entrance (Arcade pops, Minimal slides...).
                var style = CurrentTheme.EntranceOverride >= 0 ? (EntranceStyle)CurrentTheme.EntranceOverride : entrance;
                anim.PlayEntrance(items, EntranceLibrary.Get(style), stagger, delay);
            }
            else if (target.Matches(slice))
            {
                target.UpdateRows(slice);
            }
            else
            {
                target.ShowRows(slice);
            }
        }

        private void OnCatalogChanged()
        {
            if (Service.Entries.Count > 0)
                Toast($"{Service.Entries.Count} bundles from {Service.Source.Name}", ToastKind.Info);
            else if (!Service.IsRefreshing)
                Toast($"No bundles from {Service.Source.Name}", ToastKind.Error);
            if (host.gameObject.activeSelf) Render(animate: true);
        }

        public void Toast(string message, ToastKind kind)
        {
            if (view == null) return;
            var color = kind == ToastKind.Error ? CurrentTheme.StatusError
                      : kind == ToastKind.Success ? CurrentTheme.StatusOk
                      : CurrentTheme.Text;
            view.SetStatus(message, color);
            if (flatView != null && flatView.Panel != null) flatView.SetStatus(message, color);
        }

        // ================================================================== bundle actions (called by pages)

        public async void LoadBundle(BundleEntry e)
        {
            if (e == null) return;
            bool ok = await Service.LoadAsync(e.Id);
            if (this == null) return;
            if (ok) Toast($"Loaded {e.DisplayName}", ToastKind.Success);
            else Toast($"{e.DisplayName}: {e.Error ?? "failed"}", ToastKind.Error);
        }

        public void UnloadBundle(BundleEntry e)
        {
            if (e == null) return;
            Service.Unload(e.Id);
            Toast(e.IsLoaded ? $"{e.DisplayName} kept - other bundles need it" : $"Unloaded {e.DisplayName}", ToastKind.Info);
        }

        public async void LoadCategory(string category)
        {
            var results = await Service.LoadCategoryAsync(category);
            if (this == null) return;
            int failed = results.Count(r => !r);
            Toast(failed == 0 ? $"Loaded {category}" : $"{category}: {failed} failed", failed == 0 ? ToastKind.Success : ToastKind.Error);
        }

        public void UnloadCategory(string category)
        {
            Service.UnloadCategory(category);
            Toast($"Unloaded {category}", ToastKind.Info);
        }

        public async void UseAsset(BundleEntry e, string assetName)
        {
            try
            {
                var asset = await Service.LoadAssetAsync(e.Id, assetName);
                if (this == null) return;
                Toast(Spawner.Use(asset, e.Id, Rig.Camera), ToastKind.Success);
                dirty = true;
            }
            catch (Exception ex)
            {
                if (this == null) return;
                Toast(ex.Message, ToastKind.Error);
            }
        }

        public void Rescan()
        {
            Toast("Scanning...", ToastKind.Info);
            Service.RefreshCatalogAsync().Forget();
        }

        public void ClearSpawned()
        {
            Spawner.ClearAll();
            Toast("Cleared spawned objects", ToastKind.Info);
            dirty = true;
        }

        public void UnloadEverything()
        {
            Service.UnloadAll();
            Toast("Unloaded all bundles", ToastKind.Info);
        }

        // ================================================================== settings (called by pages + hotkeys)

        public void CycleEntrance(int dir)
        {
            entrance = CycleEnum(entrance, dir);
            SavePrefs();
            Toast($"Entrance: {EntranceLibrary.Get(entrance).DisplayName}", ToastKind.Info);
            if (IsOpen) Render(animate: true); // show it off immediately
        }

        public void CycleStagger(int dir)
        {
            stagger = CycleEnum(stagger, dir);
            SavePrefs();
            if (IsOpen) Render(animate: true);
        }

        public void CycleSpeed(int dir)
        {
            int i = Array.FindIndex(Speeds, s => Mathf.Approximately(s, animationSpeed));
            animationSpeed = Speeds[((i < 0 ? 2 : i) + dir + Speeds.Length) % Speeds.Length];
            Animator.Speed = animationSpeed;
            SavePrefs();
            if (IsOpen) Render(animate: true);
        }

        public string GuiSizeName
        {
            get
            {
                int i = Array.FindIndex(GuiSizes, s => Mathf.Approximately(s, guiScale));
                return i == 0 ? "Small" : i == 2 ? "Large" : i == 1 ? "Medium" : $"{guiScale:0.##}x";
            }
        }

        public string GuiViewName => guiView == GuiView.Both ? "2D + 3D" : guiView == GuiView.Flat ? "2D" : "3D";

        public void CycleGuiView(int dir)
        {
            var next = CycleEnum(guiView, dir);
            if (gui.Visible) gui.SetView(next); // raises ViewChanged → applies + saves
            else { guiView = next; SavePrefs(); }
            dirty = true;
        }

        public void CycleGuiSize(int dir)
        {
            int i = Array.FindIndex(GuiSizes, s => Mathf.Approximately(s, guiScale));
            guiScale = GuiSizes[((i < 0 ? 1 : i) + dir + GuiSizes.Length) % GuiSizes.Length];
            SavePrefs();
            if (IsOpen && placement == MenuPlacement.ClickGui) ApplyPlacement();
            dirty = true;
        }

        /// <summary>Re-render the current page in place (no entrance animation).</summary>
        public void RefreshNow() => dirty = true;

        /// <summary>Checks the broadcast feed now and then every 5 minutes.</summary>
        private async System.Threading.Tasks.Task FeedLoop()
        {
            while (this != null)
            {
                await RefreshFeedAsync();
                await UnityAsync.Delay(300f);
            }
        }

        public async System.Threading.Tasks.Task RefreshFeedAsync()
        {
            bool changed = await Feed.RefreshAsync();
            if (this == null) return;
            if (changed)
            {
                if (!string.IsNullOrEmpty(Feed.Current.announcement)) Toast(Feed.Current.announcement, ToastKind.Info);
                Screens.Apply(Feed);
                Tablet.UseBundleAsync(Feed).Forget();
            }
            dirty = true;
        }

        public void ToggleBroadcastScreens()
        {
            broadcastScreens = !broadcastScreens;
            Screens.ScreensEnabled = broadcastScreens;
            Screens.Apply(Feed);
            SavePrefs();
            dirty = true;
        }

        public void ReplayEntrance()
        {
            if (!IsOpen) return;
            Render(animate: true);
        }

        public void CyclePlacement(int dir)
        {
            placement = CycleEnum(placement, dir);
            SavePrefs();
            Toast($"Placement: {SettingsPage.PlacementName(placement)}", ToastKind.Info);
            if (!IsOpen) return;
            ApplyPlacement();
            UpdateWorldPose(snap: true);
            Animator.ReplayOpen();
            Render(animate: true, delay: 0.06f);
        }

        private int packTheme = -1; // index into ThemePresets.PackThemes, or -1 for a built-in preset

        public void CycleTheme(int dir)
        {
            var values = ((ThemePreset[])Enum.GetValues(typeof(ThemePreset)))
                .Where(t => t != ThemePreset.Custom || customTheme != null).ToArray();
            int total = values.Length + ThemePresets.PackThemes.Count;
            int current = packTheme >= 0 ? values.Length + packTheme : Array.IndexOf(values, theme);
            int next = ((current < 0 ? 0 : current) + dir + total) % total;
            if (next < values.Length) SetTheme(values[next]);
            else SetPackTheme(next - values.Length);
        }

        public void SetPackTheme(int index)
        {
            if (index < 0 || index >= ThemePresets.PackThemes.Count) return;
            packTheme = index;
            CurrentTheme = ThemePresets.PackThemes[index];
            RebuildLook();
            Toast($"Theme: {CurrentTheme.DisplayName}", ToastKind.Info);
        }

        private void LoadPackThemes(TextAsset[] assets)
        {
            int added = 0;
            foreach (var asset in assets)
            {
                if (!asset.name.StartsWith("theme-", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var t = ThemePresets.FromJson(asset.text);
                    ThemePresets.PackThemes.RemoveAll(x => x.DisplayName == t.DisplayName);
                    ThemePresets.PackThemes.Add(t);
                    added++;
                }
                catch (Exception e) { Debug.LogWarning($"[BundleMenu] Bad theme {asset.name}: {e.Message}"); }
            }
            if (added > 0) Toast($"{added} themes added from the content pack", ToastKind.Info);
        }

        public void SetTheme(ThemePreset preset)
        {
            packTheme = -1;
            theme = preset;
            CurrentTheme = ResolveTheme(theme);
            SavePrefs();
            RebuildLook();
            Toast($"Theme: {CurrentTheme.DisplayName}", ToastKind.Info);
        }

        /// <summary>Next / previous menu type. Order: Match theme, then every style.</summary>
        public void CycleMenuStyle(int dir)
        {
            int slots = Enum.GetValues(typeof(MenuStyle)).Length + 1; // slot 0 = match theme (-1)
            int next = ((menuStyle + 1 + dir) % slots + slots) % slots;
            menuStyle = next - 1;
            SavePrefs();
            RebuildLook();
            Toast($"Menu type: {MenuStyleName}", ToastKind.Info);
        }

        /// <summary>Rebuild the panel after a theme or style change, keeping open/closed state and placement.</summary>
        private void RebuildLook()
        {
            BuildView();
            if (IsOpen)
            {
                ApplyPlacement();          // the new shape may be a different size (Book is wide)
                UpdateWorldPose(snap: true);
            }
            ApplyPanelProgress(Animator.PanelProgress, Animator.PanelTargetOpen);
            if (IsOpen) Render(animate: true);
        }

        public void ToggleMenuSharing()
        {
            menuSharing = !menuSharing;
            if (Presence != null) Presence.Sharing = menuSharing;
            SavePrefs();
            dirty = true;
        }

        private MenuState BuildPresenceState()
        {
            var t = CurrentTheme;
            return new MenuState
            {
                open = IsOpen,
                theme = Clip(t.DisplayName, 24),
                style = MenuStyles.Name(ActiveStyle),
                accent = "#" + ColorUtility.ToHtmlStringRGB(t.Accent),
                accent2 = "#" + ColorUtility.ToHtmlStringRGB(t.Accent2),
                panel = "#" + ColorUtility.ToHtmlStringRGB(t.PanelTop),
                page = IsOpen && pages.Count > 0 ? Clip(pages.Peek().Title, 40) : null,
                video = Tablet != null && Tablet.IsPlaying ? Clip(Tablet.NowPlaying, 48) : null,
            };
        }

        private static string Clip(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max - 3) + "...";

        public void SetSource(BundleSourceMode mode)
        {
            sourceMode = mode;
            SavePrefs();
            while (pages.Count > 1) pages.Pop();
            Spawner.ClearAll();
            Service.SetSourceAsync(CreateSource(mode)).Forget();
            Render(animate: true);
        }

        public void CycleFailRate(int dir)
        {
            int i = Array.FindIndex(FailRates, f => Mathf.Approximately(f, dummyFailRate));
            dummyFailRate = FailRates[((i < 0 ? 0 : i) + dir + FailRates.Length) % FailRates.Length];
            if (dummySource != null) dummySource.FailureRate = dummyFailRate;
            SavePrefs();
            dirty = true;
        }

        public void ToggleUnloadMode()
        {
            unloadDestroysSpawned = !unloadDestroysSpawned;
            Service.UnloadAllLoadedObjects = unloadDestroysSpawned;
            SavePrefs();
            dirty = true;
        }

        private static T CycleEnum<T>(T value, int dir) where T : struct, Enum
        {
            var values = (T[])Enum.GetValues(typeof(T));
            int i = Array.IndexOf(values, value);
            return values[(i + dir + values.Length) % values.Length];
        }

        // ================================================================== building

        private IBundleSource CreateSource(BundleSourceMode mode)
        {
            if (mode == BundleSourceMode.Local)
            {
                dummySource = null;
                return new LocalBundleSource(string.IsNullOrWhiteSpace(localFolder) ? null : localFolder);
            }
            dummySource = new DummyBundleSource { FailureRate = dummyFailRate };
            return dummySource;
        }

        private MenuTheme ResolveTheme(ThemePreset preset)
        {
            if (preset == ThemePreset.Custom)
            {
                if (customTheme != null) return customTheme;
                Debug.LogWarning("[BundleMenu] Theme is Custom but no Custom Theme asset is assigned; using Halo.");
                theme = ThemePreset.Halo;
                preset = ThemePreset.Halo;
            }
            return ThemePresets.Create(preset);
        }

        private void BuildCanvases()
        {
            // Screen overlay: hosts the toggle button always, and the panel in ScreenRight mode.
            var screenGo = new GameObject("BundleMenu Screen", typeof(RectTransform));
            screenGo.transform.SetParent(transform, false);
            screenCanvas = screenGo.AddComponent<Canvas>();
            screenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            screenCanvas.sortingOrder = 1000;
            var scaler = screenGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.6f;
            // No GraphicRaycaster on purpose: DesktopPointer handles the mouse itself, so it works in games
            // whose EventSystem ignores the mouse, and nothing can fire twice.

            // World canvas: hosts the panel in Floating / Wrist mode. Not parented to us so its pose is free.
            var worldGo = new GameObject("BundleMenu World", typeof(RectTransform));
            worldCanvas = worldGo.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.sortingOrder = 1000;
            worldGo.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f; // crisper text up close
            ((RectTransform)worldGo.transform).sizeDelta = new Vector2(MenuView.Width, 700f);
            worldGo.SetActive(false);

            host = UIFactory.Rect("BundleMenu Panel", screenCanvas.transform);
            view = host.gameObject.AddComponent<MenuView>();
        }

        private void BuildView()
        {
            view.Build(CurrentTheme, ActiveStyle, rowsPerPage,
                $"{brand}  ·  v{(string.IsNullOrEmpty(versionLabel) ? Application.version : versionLabel)}");
            host.sizeDelta = new Vector2(view.PanelWidth, view.Height);
            ((RectTransform)worldCanvas.transform).sizeDelta = new Vector2(view.PanelWidth, view.Height);

            WireHeader(view);

            if (flatView != null)
            {
                flatView.Build(CurrentTheme, ActiveStyle, rowsPerPage,
                    $"{brand}  ·  v{(string.IsNullOrEmpty(versionLabel) ? Application.version : versionLabel)}");
                WireHeader(flatView);
            }

            BuildToggleButton();
            if (gui != null)
            {
                gui.ApplyTheme(CurrentTheme);
                if (placement == MenuPlacement.ClickGui) ClickGui.SetLayerRecursively(worldCanvas.transform, gui.Layer);
            }
        }

        private void WireHeader(MenuView v)
        {
            v.BackButton.OnClick = Back;
            v.CloseButton.OnClick = Close;
            v.SettingsButton.OnClick = () =>
            {
                if (pages.Peek() is SettingsPage) Back();
                else Navigate(new SettingsPage());
            };
            v.SettingsButton.OnAltClick = Home;
            v.PrevButton.OnClick = () => Page(-1);
            v.NextButton.OnClick = () => Page(+1);
        }

        private void BuildToggleButton()
        {
            if (toggleButton != null) Destroy(toggleButton.gameObject);
            if (!showToggleButton) return;
            toggleButton = UIFactory.IconButton(screenCanvas.transform, "Toggle", CurrentTheme, Icon.Menu, 56f, 16f);
            toggleButton.GetComponent<RectTransform>().Pin(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -24), new Vector2(56, 56));
            toggleButton.OnClick = Toggle;
            toggleButton.transform.SetAsLastSibling();
        }

        private void ApplyPlacement()
        {
            bool world = placement != MenuPlacement.ScreenRight;
            worldCanvas.gameObject.SetActive(world);
            var cam = Rig.Camera;
            worldCanvas.worldCamera = cam;

            if (world)
            {
                host.SetParent(worldCanvas.transform, false);
                host.anchorMin = host.anchorMax = host.pivot = new Vector2(0.5f, 0.5f);
                host.anchoredPosition = Vector2.zero;
                float scale = worldScale * (placement == MenuPlacement.Wrist ? wristSizeMultiplier : 1f);
                worldCanvas.transform.localScale = Vector3.one * scale;
            }
            else
            {
                host.SetParent(screenCanvas.transform, false);
                host.anchorMin = host.anchorMax = new Vector2(1f, 0.5f);
                host.pivot = new Vector2(1f, 0.5f);
                host.anchoredPosition = new Vector2(-40f, 0f);
                ClickGui.SetLayerRecursively(host, 5);
            }

            if (placement == MenuPlacement.ClickGui)
            {
                // Park the real panel far off-map on its own layer; the hidden camera renders it into the window.
                worldCanvas.transform.SetPositionAndRotation(ClickGui.HiddenOrigin, Quaternion.identity);
                worldCanvas.worldCamera = gui.Camera;
                ClickGui.SetLayerRecursively(worldCanvas.transform, gui.Layer);
                gui.Show(worldCanvas.transform, host.sizeDelta, guiView, guiScale, guiPosition, CurrentTheme,
                    $"{brand}  ·  desktop");
            }
            else
            {
                gui.Hide();
                ClickGui.SetLayerRecursively(worldCanvas.transform, 5); // UI layer, drawn by the game camera
            }
            if (toggleButton != null) toggleButton.transform.SetAsLastSibling();
            host.localScale = Vector3.one;
            host.localRotation = Quaternion.identity;
            poker.Root = host;
        }

        private void UpdateWorldPose(bool snap)
        {
            if (placement == MenuPlacement.ScreenRight || placement == MenuPlacement.ClickGui) return;
            var cam = Rig.Camera;
            if (cam == null) return;
            var camT = cam.transform;
            var canvasT = worldCanvas.transform;

            if (placement == MenuPlacement.Floating)
            {
                var flat = Vector3.ProjectOnPlane(camT.forward, Vector3.up);
                if (flat.sqrMagnitude < 0.001f) flat = camT.forward;
                flat.Normalize();
                Vector3 desired = camT.position + flat * floatingDistance + Vector3.down * 0.05f;

                Vector3 toPanel = canvasT.position - camT.position;
                float angle = Vector3.Angle(Vector3.ProjectOnPlane(toPanel, Vector3.up), flat);
                bool far = Mathf.Abs(toPanel.magnitude - floatingDistance) > 0.35f;

                if (snap) canvasT.position = desired;
                else if (angle > followAngle || far || following)
                {
                    following = Vector3.Distance(canvasT.position, desired) > 0.02f;
                    canvasT.position = Vector3.Lerp(canvasT.position, desired, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 5f));
                }
                canvasT.rotation = Quaternion.LookRotation(canvasT.position - camT.position, Vector3.up);
            }
            else // Wrist
            {
                var anchor = Rig.WristAnchor;
                if (anchor == null)
                {
                    // No hand on desktop: fake a raised left wrist, low-left in view.
                    if (simulatedWrist == null) simulatedWrist = new GameObject("[BundleMenu Simulated Wrist]").transform;
                    simulatedWrist.position = camT.TransformPoint(new Vector3(-0.2f, -0.07f, 0.48f));
                    simulatedWrist.rotation = camT.rotation;
                    anchor = simulatedWrist;
                }
                Vector3 pos = anchor == simulatedWrist ? anchor.position : anchor.TransformPoint(wristOffset);
                canvasT.position = pos;
                canvasT.rotation = wristFacesCamera
                    ? Quaternion.LookRotation(pos - camT.position, camT.up)
                    : anchor.rotation;
            }
        }

        private bool following;

        private IEnumerable<MenuButton> OverlayButtons()
        {
            buttonCache.Clear();
            if (toggleButton != null) buttonCache.Add(toggleButton);
            overlayScratch.Clear();
            if (placement == MenuPlacement.ScreenRight && host.gameObject.activeSelf)
                host.GetComponentsInChildren(false, overlayScratch);
            buttonCache.AddRange(overlayScratch);
            if (placement == MenuPlacement.ClickGui && gui.Visible)
            {
                gui.Window.GetComponentsInChildren(false, overlayScratch);
                buttonCache.AddRange(overlayScratch);
            }
            return buttonCache;
        }

        private IEnumerable<MenuButton> WorldButtons()
        {
            worldScratch.Clear();
            if (placement != MenuPlacement.ScreenRight && host.gameObject.activeSelf)
                host.GetComponentsInChildren(false, worldScratch);
            return worldScratch;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private void GrabCursor()
        {
            bool desktop = placement == MenuPlacement.ScreenRight || placement == MenuPlacement.ClickGui;
            if (!unlockCursorWhileOpen || (!desktop && Rig.PokeTips.Count > 0)) return;
            if (!cursorSaved)
            {
                savedLock = Cursor.lockState;
                savedVisible = Cursor.visible;
                cursorSaved = true;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestoreCursor()
        {
            if (!cursorSaved) return;
            Cursor.lockState = savedLock;
            Cursor.visible = savedVisible;
            cursorSaved = false;
        }

        // ================================================================== persistence

        private void LoadPrefs()
        {
            entrance = (EntranceStyle)PlayerPrefs.GetInt(Prefs + "entrance", (int)entrance);
            stagger = (StaggerMode)PlayerPrefs.GetInt(Prefs + "stagger", (int)stagger);
            animationSpeed = PlayerPrefs.GetFloat(Prefs + "speed", animationSpeed);
            // Placement saved by older versions (which defaulted to Wrist in Gorilla Tag) is dropped once.
            if (PlayerPrefs.GetInt(Prefs + "version", 1) >= PrefsVersion)
                placement = (MenuPlacement)PlayerPrefs.GetInt(Prefs + "placement", (int)placement);
            guiScale = PlayerPrefs.GetFloat(Prefs + "guiscale", guiScale);
            guiView = (GuiView)PlayerPrefs.GetInt(Prefs + "guiview", (int)guiView);
            broadcastScreens = PlayerPrefs.GetInt(Prefs + "screens", broadcastScreens ? 1 : 0) == 1;
            menuSharing = PlayerPrefs.GetInt(Prefs + "sharing", menuSharing ? 1 : 0) == 1;
            menuStyle = PlayerPrefs.GetInt(Prefs + "style", menuStyle);
            if (menuStyle >= 0 && !Enum.IsDefined(typeof(MenuStyle), menuStyle)) menuStyle = -1;
            if (!Enum.IsDefined(typeof(GuiView), guiView)) guiView = GuiView.Both;
            guiPosition = new Vector2(PlayerPrefs.GetFloat(Prefs + "guix", guiPosition.x), PlayerPrefs.GetFloat(Prefs + "guiy", guiPosition.y));
            theme = (ThemePreset)PlayerPrefs.GetInt(Prefs + "theme", (int)theme);
            sourceMode = (BundleSourceMode)PlayerPrefs.GetInt(Prefs + "source", (int)sourceMode);
            dummyFailRate = PlayerPrefs.GetFloat(Prefs + "failrate", dummyFailRate);
            unloadDestroysSpawned = PlayerPrefs.GetInt(Prefs + "unloadall", unloadDestroysSpawned ? 1 : 0) == 1;

            // Guard against values from an older build that no longer exist.
            if (!Enum.IsDefined(typeof(EntranceStyle), entrance)) entrance = EntranceStyle.Staggered;
            if (!Enum.IsDefined(typeof(MenuPlacement), placement)) placement = MenuPlacement.ScreenRight;
            if (!Enum.IsDefined(typeof(ThemePreset), theme)) theme = ThemePreset.Halo;
        }

        private void SavePrefs()
        {
            if (!rememberSettings) return;
            PlayerPrefs.SetInt(Prefs + "entrance", (int)entrance);
            PlayerPrefs.SetInt(Prefs + "stagger", (int)stagger);
            PlayerPrefs.SetFloat(Prefs + "speed", animationSpeed);
            PlayerPrefs.SetInt(Prefs + "placement", (int)placement);
            PlayerPrefs.SetInt(Prefs + "theme", (int)theme);
            PlayerPrefs.SetInt(Prefs + "source", (int)sourceMode);
            PlayerPrefs.SetFloat(Prefs + "failrate", dummyFailRate);
            PlayerPrefs.SetInt(Prefs + "unloadall", unloadDestroysSpawned ? 1 : 0);
            PlayerPrefs.SetFloat(Prefs + "guiscale", guiScale);
            PlayerPrefs.SetInt(Prefs + "guiview", (int)guiView);
            PlayerPrefs.SetInt(Prefs + "screens", broadcastScreens ? 1 : 0);
            PlayerPrefs.SetInt(Prefs + "sharing", menuSharing ? 1 : 0);
            PlayerPrefs.SetInt(Prefs + "style", menuStyle);
            PlayerPrefs.SetFloat(Prefs + "guix", guiPosition.x);
            PlayerPrefs.SetFloat(Prefs + "guiy", guiPosition.y);
            PlayerPrefs.SetInt(Prefs + "version", PrefsVersion);
            AfterSave();
            PlayerPrefs.Save();
        }
    }
}
