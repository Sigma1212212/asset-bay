using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BundleMenu
{
    /// <summary>
    /// A page turns current state into rows. Pages hold no GameObjects and no bundle state of their own,
    /// so they're trivial to add: subclass, return rows, call ctx.Navigate(new YourPage()).
    /// </summary>
    public abstract class MenuPage
    {
        public int PageIndex;
        public abstract string Title { get; }
        public abstract List<RowSpec> BuildRows(BundleMenuController ctx);

        /// <summary>False when the thing this page shows no longer exists (e.g. after a rescan).</summary>
        public virtual bool IsValid(BundleMenuController ctx) => true;

        protected static RowSpec BundleRow(BundleMenuController ctx, BundleEntry e, bool withDetails)
        {
            var row = new RowSpec { Key = "b:" + e.Id, Label = e.DisplayName, ShowChevron = false };

            switch (e.Status)
            {
                case BundleStatus.Loading:
                    row.Light = StatusLight.Busy;
                    row.Value = $"{e.Progress * 100f:0}%";
                    row.Progress = e.Progress;
                    row.OnClick = () => ctx.Toast($"{e.DisplayName} is still loading...", ToastKind.Info);
                    break;
                case BundleStatus.Loaded:
                    row.Light = StatusLight.Ok;
                    row.IsOn = e.Requested;
                    row.Value = e.IsHeldOnlyAsDependency ? "in use" : $"{e.Handle?.AssetNames.Count ?? 0} assets";
                    row.OnClick = e.Requested ? () => ctx.UnloadBundle(e) : (Action)(() => ctx.LoadBundle(e));
                    break;
                case BundleStatus.Failed:
                    row.Light = StatusLight.Error;
                    row.Value = "retry";
                    row.OnClick = () => ctx.LoadBundle(e);
                    break;
                default:
                    row.Light = StatusLight.Idle;
                    row.OnClick = () => ctx.LoadBundle(e);
                    break;
            }

            if (withDetails)
            {
                row.OnSecondary = () => ctx.Navigate(new BundlePage(e.Id));
                row.OnAltClick = row.OnSecondary;
            }
            return row;
        }
    }

    /// <summary>Home: one row per category, with a loaded/total count.</summary>
    public sealed class LibraryPage : MenuPage
    {
        public override string Title => "Library";

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            var svc = ctx.Service;
            var rows = new List<RowSpec>();

            if (svc.IsRefreshing && svc.Entries.Count == 0)
            {
                rows.Add(RowSpec.Info("scan", "Scanning for bundles..."));
                return rows;
            }
            if (svc.Entries.Count == 0)
            {
                rows.Add(RowSpec.Info("none", "No bundles found"));
                rows.Add(RowSpec.Info("hint1", svc.Source is LocalBundleSource local
                    ? "Put bundles in StreamingAssets/Bundles" : "Source returned nothing"));
                if (!string.IsNullOrEmpty(svc.LastCatalogError)) rows.Add(RowSpec.Message("err", svc.LastCatalogError));
                rows.Add(new RowSpec { Key = "dummy", Label = "Use dummy bundles", ShowChevron = true,
                                       OnClick = () => ctx.SetSource(BundleSourceMode.Dummy) });
                rows.Add(new RowSpec { Key = "rescan", Label = "Rescan", OnClick = ctx.Rescan });
                return rows;
            }

            var news = ctx.Feed?.Current?.announcement;
            if (!string.IsNullOrEmpty(news)) rows.Add(RowSpec.Message("news", news));

            rows.Add(new RowSpec
            {
                Key = "videos",
                Label = "Videos",
                Value = ctx.Tablet.IsOpen ? "playing" : $"{ctx.Feed?.Current?.videos?.Length ?? 0}",
                Light = ctx.Tablet.IsPlaying ? StatusLight.Ok : StatusLight.Idle,
                ShowChevron = true,
                OnClick = () => ctx.Navigate(new VideosPage()),
            });

            foreach (string category in svc.Categories)
            {
                var items = svc.InCategory(category).ToList();
                int loaded = items.Count(e => e.IsLoaded);
                bool busy = items.Any(e => e.Status == BundleStatus.Loading);
                bool failed = items.Any(e => e.Status == BundleStatus.Failed);

                string cat = category;
                rows.Add(new RowSpec
                {
                    Key = "c:" + category,
                    Label = category,
                    Value = $"{loaded}/{items.Count}",
                    Light = busy ? StatusLight.Busy : failed ? StatusLight.Error : loaded > 0 ? StatusLight.Ok : StatusLight.Idle,
                    ShowChevron = true,
                    OnClick = () => ctx.Navigate(new CategoryPage(cat)),
                });
            }

            if (ctx.Presence != null)
                rows.Add(new RowSpec
                {
                    Key = "players", Label = "Players", Value = ctx.Presence.Roster.Count.ToString(),
                    ShowChevron = true, OnClick = () => ctx.Navigate(new PlayersPage()),
                });
            if (!ctx.IsAdmin)
            {
                rows.Add(new RowSpec { Key = "mode", Label = "Admin menu", Value = ctx.PinSet ? "locked" : "off",
                    Light = StatusLight.Idle, OnClick = () => ctx.SetMode(MenuMode.Admin) });
                return rows;
            }
            rows.Add(new RowSpec
            {
                Key = "mods",
                Label = "Mods",
                Value = !ctx.Mods.Available ? "Gorilla Tag only"
                      : !ctx.Mods.Allowed ? "paused (public)"
                      : $"{ctx.Mods.Mods.Count(m => m.Enabled)} on",
                Light = !ctx.Mods.Available ? StatusLight.Idle : ctx.Mods.Allowed ? StatusLight.Ok : StatusLight.Error,
                ShowChevron = true,
                OnClick = () => ctx.Navigate(new ModsPage()),
            });
            if (ctx.Presence != null)
            {
                var pr = ctx.Presence;
                rows.Add(RowSpec.Info("users", "Asset Bay users here",
                    !pr.Sharing ? "sharing off"
                    : !string.IsNullOrEmpty(pr.LastError) ? "offline"
                    : pr.Others.Count.ToString()));
            }
            rows.Add(new RowSpec
            {
                Key = "gun",
                Label = "Gun",
                Value = ctx.Gun.GunEnabled ? ctx.Gun.Mode?.Name ?? "on" : "off",
                Light = ctx.Gun.GunEnabled ? StatusLight.Ok : StatusLight.Idle,
                ShowChevron = true,
                OnClick = () => ctx.Navigate(new GunPage()),
            });
            return rows;
        }
    }

    /// <summary>A category: a load-all/unload-all row, then one row per bundle.</summary>
    public sealed class CategoryPage : MenuPage
    {
        private readonly string category;
        public CategoryPage(string category) => this.category = category;

        public override string Title => category;
        public override bool IsValid(BundleMenuController ctx) => ctx.Service.InCategory(category).Any();

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            var items = ctx.Service.InCategory(category).ToList();
            bool allRequested = items.All(e => e.Requested && e.IsLoaded);
            var rows = new List<RowSpec>
            {
                new RowSpec
                {
                    Key = "all",
                    Label = allRequested ? "Unload all" : "Load all",
                    Value = $"{items.Count(e => e.IsLoaded)}/{items.Count}",
                    OnClick = allRequested ? (Action)(() => ctx.UnloadCategory(category)) : () => ctx.LoadCategory(category),
                },
            };
            rows.AddRange(items.Select(e => BundleRow(ctx, e, withDetails: true)));
            return rows;
        }
    }

    /// <summary>One bundle: status, error, actions, and (when loaded) its assets - tap an asset to spawn/use it.</summary>
    public sealed class BundlePage : MenuPage
    {
        private readonly string id;
        public BundlePage(string id) => this.id = id;

        public override string Title => title ?? "Bundle";
        private string title;

        public override bool IsValid(BundleMenuController ctx) => ctx.Service.Get(id) != null;

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            var e = ctx.Service.Get(id);
            var rows = new List<RowSpec>();
            if (e == null)
            {
                rows.Add(RowSpec.Info("gone", "This bundle no longer exists"));
                return rows;
            }
            title = e.DisplayName;

            var main = BundleRow(ctx, e, withDetails: false);
            main.Key = "main";
            main.Label = e.Status == BundleStatus.Loaded ? (e.Requested ? "Unload" : "Load")
                       : e.Status == BundleStatus.Failed ? "Retry"
                       : e.Status == BundleStatus.Loading ? "Loading..." : "Load";
            if (e.Status == BundleStatus.Failed) main.Value = null; // label already says "Retry"
            rows.Add(main);

            if (e.Status == BundleStatus.Failed && !string.IsNullOrEmpty(e.Error))
                rows.Add(RowSpec.Message("err", e.Error));

            var deps = e.Descriptor.Dependencies ?? Array.Empty<string>();
            if (deps.Length > 0)
            {
                string names = string.Join(", ", deps.Select(d => ctx.Service.Get(d)?.DisplayName ?? d));
                rows.Add(RowSpec.Info("deps", "Needs " + names));
            }
            if (e.IsHeldOnlyAsDependency)
                rows.Add(RowSpec.Info("held", $"Kept loaded by {e.DependentCount} other bundle(s)"));

            if (e.IsLoaded && e.Handle != null)
            {
                foreach (string asset in e.Handle.AssetNames)
                {
                    string a = asset;
                    rows.Add(new RowSpec
                    {
                        Key = "a:" + asset,
                        Label = LocalBundleSource.Prettify(Path.GetFileNameWithoutExtension(asset)),
                        Value = Path.GetExtension(asset).TrimStart('.'),
                        Light = StatusLight.None,
                        OnClick = () => ctx.UseAsset(e, a),
                    });
                }
            }
            else if (e.Status != BundleStatus.Loading)
            {
                rows.Add(RowSpec.Info("hint", "Load the bundle to see its assets"));
            }
            return rows;
        }
    }

    /// <summary>Your uploaded videos, played on the tablet that pops out in front of you.</summary>
    public sealed class VideosPage : MenuPage
    {
        private static readonly float[] Volumes = { 0f, 0.25f, 0.5f, 0.7f, 1f };
        public override string Title => "Videos";

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            var feed = ctx.Feed;
            var tablet = ctx.Tablet;
            var rows = new List<RowSpec>();

            if (tablet.IsOpen)
            {
                rows.Add(new RowSpec
                {
                    Key = "np", Label = tablet.IsPlaying ? "Pause" : "Play",
                    Value = tablet.NowPlaying ?? "", IsOn = tablet.IsPlaying, Light = tablet.IsPlaying ? StatusLight.Ok : StatusLight.Idle,
                    OnClick = () => { tablet.TogglePause(); ctx.RefreshNow(); },
                });
                rows.Add(new RowSpec { Key = "restart", Label = "Restart", OnClick = () => { tablet.Restart(); ctx.RefreshNow(); } });
                rows.Add(new RowSpec
                {
                    Key = "vol", Label = "Volume", Value = $"{tablet.Volume * 100f:0}%",
                    OnClick = () => { Step(tablet, +1); ctx.RefreshNow(); },
                    OnAltClick = () => { Step(tablet, -1); ctx.RefreshNow(); },
                });
                rows.Add(new RowSpec
                {
                    Key = "throw", Label = tablet.IsThrown ? "Recall tablet" : "Throw tablet",
                    Value = UnityEngine.XR.XRSettings.isDeviceActive ? "or grab it" : "or press T",
                    OnClick = () => { if (tablet.IsThrown) tablet.Recall(); else tablet.Throw(); ctx.RefreshNow(); },
                });
                rows.Add(new RowSpec { Key = "close", Label = "Put tablet away", Value = tablet.Source,
                    OnClick = () => { tablet.Close(); ctx.RefreshNow(); } });
            }

            rows.Add(new RowSpec
            {
                Key = "test", Label = "Test the tablet", Value = "timer + beeps",
                OnClick = () => SafeMenu.PlayTestVideo(ctx),
            });
            rows.Add(new RowSpec
            {
                Key = "pack", Label = "Content pack", Value = tablet.ContentBundle != null ? "loaded" : "not published",
                ShowChevron = true, OnClick = () => ctx.Navigate(new ContentPage()),
            });
            rows.Add(new RowSpec
            {
                Key = "style", Label = "Tablet style", Value = tablet.Style.name,
                OnClick = () => { tablet.CycleStyle(+1); ctx.RefreshNow(); },
                OnAltClick = () => { tablet.CycleStyle(-1); ctx.RefreshNow(); },
            });
            rows.Add(new RowSpec
            {
                Key = "size", Label = "Tablet size", Value = $"x{tablet.SizeMultiplier:0.#}",
                OnClick = () => { tablet.CycleSize(+1); ctx.RefreshNow(); },
                OnAltClick = () => { tablet.CycleSize(-1); ctx.RefreshNow(); },
            });

            var videos = feed?.Current?.videos;
            if (videos == null || videos.Length == 0)
            {
                rows.Add(RowSpec.Message("none", feed?.LastError != null
                    ? "Couldn't load your library: " + feed.LastError
                    : "No videos yet. Publish some with backend/publish-feed.ps1."));
            }
            else
            {
                foreach (var v in videos)
                {
                    var video = v;
                    rows.Add(new RowSpec
                    {
                        Key = "v:" + video.id,
                        Label = string.IsNullOrEmpty(video.title) ? video.id : video.title,
                        Value = video.length ?? "",
                        IsOn = tablet.IsOpen && tablet.NowPlaying == video.title,
                        OnClick = () => { tablet.Play(feed.MediaUrl(video.video), video.title ?? video.id); ctx.RefreshNow(); },
                    });
                }
            }

            rows.Add(new RowSpec
            {
                Key = "refresh", Label = "Refresh library",
                Value = feed?.LastUpdated != null ? feed.LastUpdated.Value.ToString("HH:mm") : "",
                OnClick = () => ctx.RefreshFeedAsync().Forget(),
            });
            return rows;
        }

        private static void Step(VideoTablet tablet, int dir)
        {
            int i = System.Array.FindIndex(Volumes, v => UnityEngine.Mathf.Approximately(v, tablet.Volume));
            tablet.SetVolume(Volumes[((i < 0 ? 3 : i) + dir + Volumes.Length) % Volumes.Length]);
        }
    }

    /// <summary>Props from the content pack. Tap one to spawn it in front of you (the gun's Place mode copies it).</summary>
    public sealed class ContentPage : MenuPage
    {
        public override string Title => "Content pack";

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            var rows = new List<RowSpec>();
            var bundle = ctx.Tablet.ContentBundle;
            if (bundle == null)
            {
                rows.Add(RowSpec.Message("none", "No content pack yet. Publish build/bundles/content with publish-feed.ps1 and list it in feed.json (tablet)."));
                return rows;
            }
            foreach (var path in bundle.GetAllAssetNames())
            {
                if (!path.EndsWith(".prefab")) continue;
                string p = path;
                string name = LocalBundleSource.Prettify(System.IO.Path.GetFileNameWithoutExtension(path));
                rows.Add(new RowSpec
                {
                    Key = "prop:" + path, Label = name, Value = "spawn",
                    OnClick = () =>
                    {
                        var prefab = bundle.LoadAsset<UnityEngine.GameObject>(p);
                        ctx.Toast(prefab != null ? ctx.Spawner.Use(prefab, "content", ctx.Rig.Camera) : "Couldn't load " + name, ToastKind.Info);
                    },
                });
            }
            rows.Add(RowSpec.Info("styles", "Tablet styles", ctx.Tablet.Styles.Count.ToString()));
            rows.Add(RowSpec.Info("themes", "Pack themes", ThemePresets.PackThemes.Count.ToString()));
            return rows;
        }
    }

    /// <summary>Movement mods for your own player, plus the lobby they're allowed in.</summary>
    /// <summary>Everyone in the room, and the tag you've pinned on each of them (only you see those).</summary>
    public sealed class PlayersPage : MenuPage
    {
        public override string Title => "Players";

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            var rows = new List<RowSpec>();
            var presence = ctx.Presence;
            if (presence == null) { rows.Add(RowSpec.Info("none", "Gorilla Tag only")); return rows; }

            rows.Add(Cycle("mytag", "My tag", string.IsNullOrEmpty(TagStore.MyTag) ? "off" : TagStore.MyTag,
                () => ctx.CycleMyTag(+1), () => ctx.CycleMyTag(-1)));
            rows.Add(Cycle("mytagc", "My tag colour", TagStore.ColourName(TagStore.MyColourHex),
                () => ctx.CycleMyTagColour(+1), () => ctx.CycleMyTagColour(-1)));
            rows.Add(Cycle("tagson", "Show tags", ctx.TagsOn ? "on" : "off", ctx.ToggleTags, ctx.ToggleTags));

            if (presence.Roster.Count == 0) rows.Add(RowSpec.Info("empty", "Nobody else here"));
            foreach (var player in presence.Roster)
            {
                var p = player;
                var state = presence.StateFor(p);
                string mine = TagStore.LocalTag(p.UserId);
                string shown = mine ?? (string.IsNullOrEmpty(state?.tag) ? null : state.tag + " (theirs)");
                rows.Add(new RowSpec
                {
                    Key = "p:" + p.Name,
                    Label = p.Name,
                    Value = shown ?? (state != null ? "asset bay" : ""),
                    Light = state == null ? StatusLight.None : state.open ? StatusLight.Ok : StatusLight.Idle,
                    IsOn = mine != null,
                    // Left click steps through the tags, right click steps back; the side button changes colour.
                    OnClick = () => { TagStore.CycleLocalTag(p.UserId, +1); ctx.RefreshNow(); },
                    OnAltClick = () => { TagStore.CycleLocalTag(p.UserId, -1); ctx.RefreshNow(); },
                    OnSecondary = mine == null ? (Action)null : () => { TagStore.CycleLocalColour(p.UserId, +1); ctx.RefreshNow(); },
                    SecondaryIcon = Icon.Dot,
                });
            }
            if (TagStore.LocalCount > 0)
                rows.Add(new RowSpec { Key = "clear", Label = "Clear my tags", Value = TagStore.LocalCount.ToString(),
                    OnClick = () => { TagStore.ClearLocal(); ctx.RefreshNow(); } });
            return rows;
        }

        private static RowSpec Cycle(string key, string label, string value, Action next, Action prev) =>
            new RowSpec { Key = key, Label = label, Value = value, OnClick = next, OnAltClick = prev ?? next };
    }

    public sealed class ModsPage : MenuPage
    {
        public override string Title => "Mods";

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            // The normal menu doesn't include this page; if it's still on the stack, stop here.
            if (!ctx.IsAdmin) return new List<RowSpec> { RowSpec.Info("locked", "Admin menu only") };

            var runner = ctx.Mods;
            var rows = new List<RowSpec>();

            if (!runner.Available)
            {
                rows.Add(RowSpec.Message("na", "These mods control your Gorilla Tag player, so they only work when the menu is loaded into Gorilla Tag."));
                return rows;
            }

            rows.Add(new RowSpec
            {
                Key = "lobby", Label = "Lobby", Interactable = false,
                Value = LobbyName(runner.Lobby),
                Light = runner.Allowed ? StatusLight.Ok : StatusLight.Error,
            });
            if (!runner.Allowed)
                rows.Add(RowSpec.Message("rule", "Mods are paused in public lobbies (they'd get your account banned). Join a private or modded room."));

            foreach (var mod in runner.Mods)
            {
                var m = mod;
                rows.Add(new RowSpec
                {
                    Key = "mod:" + m.Name,
                    Label = m.Name,
                    Value = m.Enabled ? m.Hint : "off",
                    IsOn = m.Enabled,
                    Light = m.Enabled ? StatusLight.Ok : StatusLight.Idle,
                    Interactable = runner.Allowed || m.Enabled,
                    OnClick = () => { ctx.Toast(runner.Toggle(m), ToastKind.Info); ctx.RefreshNow(); },
                });

                if (!m.Enabled) continue;
                foreach (var setting in m.Settings)
                {
                    var st = setting;
                    rows.Add(new RowSpec
                    {
                        Key = "set:" + m.Name + ":" + st.Name,
                        Label = "   " + st.Name,
                        Value = st.Value(),
                        OnClick = () => { st.Cycle(+1); ctx.RefreshNow(); },
                        OnAltClick = () => { st.Cycle(-1); ctx.RefreshNow(); },
                    });
                }
            }

            rows.Add(new RowSpec
            {
                Key = "cp-save", Label = "Save checkpoint", Value = ctx.Checkpoint.Saved ? "saved" : "",
                Interactable = runner.Allowed,
                OnClick = () => { ctx.Toast(ctx.Checkpoint.Save(runner.Context), ToastKind.Info); ctx.RefreshNow(); },
            });
            rows.Add(new RowSpec
            {
                Key = "cp-go", Label = "Return to checkpoint",
                Interactable = runner.Allowed && ctx.Checkpoint.Saved,
                OnClick = () => ctx.Toast(ctx.Checkpoint.Return(runner.Context, runner.Allowed), ToastKind.Info),
            });
            rows.Add(new RowSpec
            {
                Key = "alloff", Label = "All mods off",
                OnClick = () => { runner.DisableAll(); ctx.Toast("All mods off", ToastKind.Info); ctx.RefreshNow(); },
            });
            return rows;
        }

        private static string LobbyName(LobbyKind k) =>
            k == LobbyKind.Offline ? "offline" : k == LobbyKind.Private ? "private room"
            : k == LobbyKind.Modded ? "modded room" : k == LobbyKind.Public ? "public - paused" : "-";
    }

    /// <summary>The gun: on/off, mode, and a live readout of what it's pointing at.</summary>
    public sealed class GunPage : MenuPage
    {
        public override string Title => "Gun";

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            // The normal menu doesn't include this page; if it's still on the stack, stop here.
            if (!ctx.IsAdmin) return new List<RowSpec> { RowSpec.Info("locked", "Admin menu only") };

            var gun = ctx.Gun;
            var mode = gun.Mode;
            var rows = new List<RowSpec>
            {
                new RowSpec
                {
                    Key = "enabled", Label = "Gun", Value = gun.GunEnabled ? "on" : "off", IsOn = gun.GunEnabled,
                    Light = gun.GunEnabled ? StatusLight.Ok : StatusLight.Idle,
                    OnClick = () => { gun.GunEnabled = !gun.GunEnabled; ctx.Toast(gun.GunEnabled ? "Gun on" : "Gun off", ToastKind.Info); ctx.RefreshNow(); },
                },
                new RowSpec
                {
                    Key = "mode", Label = "Mode", Value = mode?.Name ?? "-",
                    OnClick = () => { gun.CycleMode(+1); ctx.RefreshNow(); },
                    OnAltClick = () => { gun.CycleMode(-1); ctx.RefreshNow(); },
                },
                RowSpec.Info("target", gun.Aiming ? "Aiming" : "Target",
                    gun.Aiming && mode != null ? mode.Describe(gun.Current) : mode?.Describe(default) ?? ""),
                RowSpec.Message("how", MenuInput.VRActive
                    ? "Hold the right grip to aim, pull the right trigger to fire."
                    : "Hold right mouse to aim, left-click to fire. Doesn't fire while the cursor is on the menu."),
            };
            return rows;
        }
    }

    /// <summary>Runtime settings. Click cycles forward, right-click cycles back. Everything applies instantly.</summary>
    public sealed class SettingsPage : MenuPage
    {
        public override string Title => "Settings";

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            var rows = new List<RowSpec>
            {
                Cycle("entrance", "Entrance", EntranceLibrary.Get(ctx.Entrance).DisplayName,
                    () => ctx.CycleEntrance(+1), () => ctx.CycleEntrance(-1)),
                Cycle("stagger", "Stagger", ctx.Stagger.ToString(),
                    () => ctx.CycleStagger(+1), () => ctx.CycleStagger(-1)),
                Cycle("speed", "Speed", $"{ctx.AnimationSpeed:0.##}x",
                    () => ctx.CycleSpeed(+1), () => ctx.CycleSpeed(-1)),
                new RowSpec { Key = "replay", Label = "Replay animation", Value = "preview",
                    OnClick = ctx.ReplayEntrance },
                Cycle("placement", "Placement", PlacementName(ctx.Placement),
                    () => ctx.CyclePlacement(+1), () => ctx.CyclePlacement(-1)),
                ctx.Placement == MenuPlacement.ClickGui
                    ? Cycle("guiview", "GUI view", ctx.GuiViewName, () => ctx.CycleGuiView(+1), () => ctx.CycleGuiView(-1))
                    : null,
                ctx.Placement == MenuPlacement.ClickGui
                    ? Cycle("guisize", "GUI size", ctx.GuiSizeName, () => ctx.CycleGuiSize(+1), () => ctx.CycleGuiSize(-1))
                    : null,
                Cycle("theme", "Theme", ctx.CurrentTheme.DisplayName,
                    () => ctx.CycleTheme(+1), () => ctx.CycleTheme(-1)),
                Cycle("menutype", "Menu type", ctx.MenuStyleName,
                    () => ctx.CycleMenuStyle(+1), () => ctx.CycleMenuStyle(-1)),
                !ctx.IsAdmin ? null : Cycle("source", "Bundle source", ctx.SourceMode.ToString(),
                    () => ctx.SetSource(ctx.SourceMode == BundleSourceMode.Dummy ? BundleSourceMode.Local : BundleSourceMode.Dummy), null),
            };

            rows.RemoveAll(r => r == null);

            if (ctx.IsAdmin && ctx.SourceMode == BundleSourceMode.Dummy)
                rows.Add(Cycle("fail", "Dummy fail rate", $"{ctx.DummyFailRate * 100f:0}%",
                    () => ctx.CycleFailRate(+1), () => ctx.CycleFailRate(-1)));

            if (ctx.IsAdmin) rows.Add(Cycle("unloadmode", "On unload", ctx.UnloadDestroysSpawned ? "destroy spawned" : "keep spawned",
                ctx.ToggleUnloadMode, ctx.ToggleUnloadMode));
            rows.Add(Cycle("screens", "Broadcast screens", ctx.BroadcastScreensOn ? "on" : "off",
                ctx.ToggleBroadcastScreens, ctx.ToggleBroadcastScreens));
            if (ctx.Presence != null)
                rows.Add(new RowSpec
                {
                    Key = "control", Label = "Let others use my menu", Value = ctx.RemoteControlName,
                    IsOn = ctx.RemoteControl != ControlLevel.Off, Light = ctx.RemoteControl == ControlLevel.Full ? StatusLight.Error
                        : ctx.RemoteControl == ControlLevel.Browse ? StatusLight.Busy : StatusLight.None,
                    OnClick = () => ctx.CycleRemoteControl(+1), OnAltClick = () => ctx.CycleRemoteControl(-1),
                });
            if (ctx.Presence != null)
                rows.Add(new RowSpec
                {
                    Key = "sharing", Label = "Menu sharing", Value = ctx.MenuSharing ? "on" : "off", IsOn = ctx.MenuSharing,
                    OnClick = ctx.ToggleMenuSharing, OnAltClick = ctx.ToggleMenuSharing,
                });
            rows.Add(RowSpec.Info("safe", "Safe mode + sync code", $"press {ctx.Safe?.Key.ToString() ?? "H"}"));
            rows.Add(Cycle("mytag", "My tag", string.IsNullOrEmpty(TagStore.MyTag) ? "off" : TagStore.MyTag,
                () => ctx.CycleMyTag(+1), () => ctx.CycleMyTag(-1)));
            if (!string.IsNullOrEmpty(TagStore.MyTag))
                rows.Add(Cycle("mytagc", "Tag colour", TagStore.ColourName(TagStore.MyColourHex),
                    () => ctx.CycleMyTagColour(+1), () => ctx.CycleMyTagColour(-1)));
            rows.Add(Cycle("tagson", "Show tags", ctx.TagsOn ? "on" : "off", ctx.ToggleTags, ctx.ToggleTags));
            rows.Add(Cycle("mode", "Menu", ctx.IsAdmin ? "admin" : "normal",
                () => ctx.ToggleMode(), () => ctx.ToggleMode()));
            if (!ctx.IsAdmin) return rows;

            rows.Add(new RowSpec { Key = "rescan", Label = "Rescan bundles", OnClick = ctx.Rescan });
            rows.Add(new RowSpec { Key = "clear", Label = "Clear spawned", Value = ctx.Spawner.Count.ToString(), OnClick = ctx.ClearSpawned });
            rows.Add(new RowSpec { Key = "unloadall", Label = "Unload everything", OnClick = ctx.UnloadEverything });
            return rows;
        }

        public static string PlacementName(MenuPlacement p) =>
            p == MenuPlacement.ScreenRight ? "Screen" : p == MenuPlacement.Floating ? "Floating"
            : p == MenuPlacement.Wrist ? "Wrist" : "Desktop GUI";

        private static RowSpec Cycle(string key, string label, string value, Action next, Action prev) =>
            new RowSpec { Key = key, Label = label, Value = value, OnClick = next, OnAltClick = prev ?? next };
    }
}
