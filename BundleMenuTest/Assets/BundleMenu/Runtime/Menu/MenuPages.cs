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
                Cycle("theme", "Theme", ctx.CurrentTheme.DisplayName,
                    () => ctx.CycleTheme(+1), () => ctx.CycleTheme(-1)),
                Cycle("source", "Bundle source", ctx.SourceMode.ToString(),
                    () => ctx.SetSource(ctx.SourceMode == BundleSourceMode.Dummy ? BundleSourceMode.Local : BundleSourceMode.Dummy), null),
            };

            if (ctx.SourceMode == BundleSourceMode.Dummy)
                rows.Add(Cycle("fail", "Dummy fail rate", $"{ctx.DummyFailRate * 100f:0}%",
                    () => ctx.CycleFailRate(+1), () => ctx.CycleFailRate(-1)));

            rows.Add(Cycle("unloadmode", "On unload", ctx.UnloadDestroysSpawned ? "destroy spawned" : "keep spawned",
                ctx.ToggleUnloadMode, ctx.ToggleUnloadMode));
            rows.Add(new RowSpec { Key = "rescan", Label = "Rescan bundles", OnClick = ctx.Rescan });
            rows.Add(new RowSpec { Key = "clear", Label = "Clear spawned", Value = ctx.Spawner.Count.ToString(), OnClick = ctx.ClearSpawned });
            rows.Add(new RowSpec { Key = "unloadall", Label = "Unload everything", OnClick = ctx.UnloadEverything });
            return rows;
        }

        public static string PlacementName(MenuPlacement p) =>
            p == MenuPlacement.ScreenRight ? "Screen" : p == MenuPlacement.Floating ? "Floating" : "Wrist";

        private static RowSpec Cycle(string key, string label, string value, Action next, Action prev) =>
            new RowSpec { Key = key, Label = label, Value = value, OnClick = next, OnAltClick = prev ?? next };
    }
}
