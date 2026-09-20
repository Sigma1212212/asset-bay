using System;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>Settings sync (server copy of your settings) and the hooks the safe-mode window uses.</summary>
    public sealed partial class BundleMenuController
    {
        public SettingsSync Sync { get; private set; }
        public SafeMenu Safe { get; private set; }

        private SyncedSettings defaults;  // inspector values, captured before saved prefs load
        private long savedAt;
        private bool importing;

        private void CaptureDefaults() => defaults = ExportSettings();

        private void SetupSync()
        {
            Sync = gameObject.AddComponent<SettingsSync>();
            Sync.Endpoint = Feed.BaseUrl + "settings";
            Sync.Export = ExportSettings;
            Sync.Import = s => { ImportSettings(s); Toast("Settings loaded from your sync code", ToastKind.Success); };

            Safe = gameObject.AddComponent<SafeMenu>();
            Safe.Menu = this;
        }

        /// <summary>Called at the end of every save: stamp the time and queue an upload.</summary>
        private void AfterSave()
        {
            if (importing) return;
            savedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            PlayerPrefs.SetString(Prefs + "savedat", savedAt.ToString());
            Sync?.MarkDirty();
        }

        private void LoadSavedAt() =>
            long.TryParse(PlayerPrefs.GetString(Prefs + "savedat", "0"), out savedAt);

        public SyncedSettings ExportSettings() => new SyncedSettings
        {
            savedAt = savedAt,
            theme = (int)theme, style = menuStyle, entrance = (int)entrance, stagger = (int)stagger,
            placement = (int)placement, guiview = (int)guiView, source = (int)sourceMode,
            speed = animationSpeed, guiscale = guiScale, guix = guiPosition.x, guiy = guiPosition.y,
            failrate = dummyFailRate, screens = broadcastScreens, sharing = menuSharing, unloadall = unloadDestroysSpawned,
        };

        /// <summary>Take a full set of settings (from the server or a reset) and apply everything live.</summary>
        public void ImportSettings(SyncedSettings s)
        {
            if (s == null) return;
            importing = true;
            try
            {
                theme = Valid<ThemePreset>(s.theme, ThemePreset.Halo);
                if (theme == ThemePreset.Custom && customTheme == null) theme = ThemePreset.Halo;
                menuStyle = s.style >= 0 && Enum.IsDefined(typeof(MenuStyle), s.style) ? s.style : -1;
                entrance = Valid<EntranceStyle>(s.entrance, EntranceStyle.Staggered);
                stagger = Valid<StaggerMode>(s.stagger, StaggerMode.Auto);
                placement = Valid<MenuPlacement>(s.placement, placement);
                guiView = Valid<GuiView>(s.guiview, GuiView.Both);
                animationSpeed = Mathf.Clamp(s.speed, 0.25f, 3f);
                guiScale = Mathf.Clamp(s.guiscale, 0.6f, 1.6f);
                guiPosition = new Vector2(Mathf.Clamp01(s.guix), Mathf.Clamp01(s.guiy));
                dummyFailRate = Mathf.Clamp01(s.failrate);
                broadcastScreens = s.screens;
                menuSharing = s.sharing;
                unloadDestroysSpawned = s.unloadall;
                savedAt = s.savedAt;

                Animator.Speed = animationSpeed;
                if (dummySource != null) dummySource.FailureRate = dummyFailRate;
                Service.UnloadAllLoadedObjects = unloadDestroysSpawned;
                Screens.ScreensEnabled = broadcastScreens;
                Screens.Apply(Feed);
                if (Presence != null) Presence.Sharing = menuSharing;

                var source = Valid<BundleSourceMode>(s.source, sourceMode);
                if (source != sourceMode) SetSource(source);

                packTheme = -1;
                AdoptTheme(ResolveTheme(theme));
                SavePrefs();
                PlayerPrefs.SetString(Prefs + "savedat", savedAt.ToString());
                RebuildLook();
            }
            finally { importing = false; }
        }

        private static T Valid<T>(int value, T fallback) where T : struct, Enum =>
            Enum.IsDefined(typeof(T), value) ? (T)(object)value : fallback;

        // ------------------------------------------------------------------ safe-mode actions

        /// <summary>Tear down and rebuild the whole panel (fixes a stuck or half-built UI).</summary>
        public void RebuildUI()
        {
            try { RebuildLook(); Toast("Menu UI rebuilt", ToastKind.Info); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Back to the default look: Halo theme, matching menu type, medium size, centred window.</summary>
        public void ResetLook()
        {
            menuStyle = -1;
            guiScale = 1f;
            guiPosition = new Vector2(0.5f, 0.5f);
            SetTheme(ThemePreset.Halo);
            if (IsOpen) ApplyPlacement();
        }

        public void SetPlacement(MenuPlacement p)
        {
            placement = p;
            SavePrefs();
            if (!IsOpen) return;
            ApplyPlacement();
            UpdateWorldPose(snap: true);
            Animator.ReplayOpen();
            Render(animate: true, delay: 0.06f);
        }

        /// <summary>Every setting back to how the menu shipped (your sync code is kept).</summary>
        public void FactoryReset()
        {
            var d = defaults ?? ExportSettings();
            d.savedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ImportSettings(d);
            Sync?.MarkDirty();
            Toast("All settings reset", ToastKind.Info);
        }

        public string ThemeName => CurrentTheme != null ? CurrentTheme.DisplayName : theme.ToString();
        public string EntranceName => EntranceLibrary.Get(entrance).DisplayName;
    }
}
