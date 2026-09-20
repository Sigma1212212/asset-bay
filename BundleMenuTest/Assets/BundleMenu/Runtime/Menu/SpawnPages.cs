using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Everything you can spawn, in one place: props from the content pack and assets from any bundle
    /// you've loaded. Spawned things are yours alone - they exist on your PC, nobody else sees them.
    /// </summary>
    public sealed class SpawnPage : MenuPage
    {
        public override string Title => "Spawn";

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            var rows = new List<RowSpec>();
            var spawner = ctx.Spawner;

            rows.Add(RowSpec.Info("note", "Only you see what you spawn", spawner.Count > 0 ? spawner.Count + " out" : ""));
            if (spawner.LastPrefab != null)
                rows.Add(new RowSpec
                {
                    Key = "again", Label = "Spawn that again", Value = spawner.LastPrefab.name,
                    OnClick = () => { ctx.Toast(spawner.Use(spawner.LastPrefab, spawner.LastBundleId ?? "spawn", ctx.Rig.Camera), ToastKind.Info); ctx.RefreshNow(); },
                });

            // Props that came with the content pack.
            var pack = ctx.Tablet != null ? ctx.Tablet.ContentBundle : null;
            if (pack != null)
                foreach (var path in pack.GetAllAssetNames())
                {
                    if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                    string assetPath = path;
                    string name = LocalBundleSource.Prettify(Path.GetFileNameWithoutExtension(path));
                    rows.Add(new RowSpec
                    {
                        Key = "pack:" + path, Label = name, Value = "pack",
                        OnClick = () =>
                        {
                            var prefab = pack.LoadAsset<GameObject>(assetPath);
                            ctx.Toast(prefab != null ? spawner.Use(prefab, "content", ctx.Rig.Camera) : "Couldn't load " + name, ToastKind.Info);
                            ctx.RefreshNow();
                        },
                    });
                }

            // Anything inside bundles that are loaded right now.
            foreach (var entry in ctx.Service.Entries)
            {
                if (!entry.IsLoaded || entry.Handle == null) continue;
                var bundleEntry = entry;
                foreach (var asset in entry.Handle.AssetNames)
                {
                    string assetName = asset;
                    rows.Add(new RowSpec
                    {
                        Key = $"a:{entry.Id}:{asset}", Label = LocalBundleSource.Prettify(asset), Value = entry.DisplayName,
                        OnClick = () => { ctx.UseAsset(bundleEntry, assetName); ctx.RefreshNow(); },
                    });
                }
            }

            if (rows.Count <= 2)
                rows.Add(RowSpec.Message("empty", "Load a bundle in Library, or publish a content pack, to have things to spawn."));

            if (spawner.Count > 0)
                rows.Add(new RowSpec
                {
                    Key = "clear", Label = "Clear what I spawned", Value = spawner.Count.ToString(),
                    OnClick = () => { ctx.ClearSpawned(); ctx.RefreshNow(); },
                });
            return rows;
        }
    }

    /// <summary>
    /// Videos from this PC, for the tablet. Anyone can drop files in their own folder and play them;
    /// the Videos page stays as it is - only what the menu's owner publishes is shared with everyone.
    /// </summary>
    public sealed class MyVideosPage : MenuPage
    {
        public override string Title => "My videos";

        /// <summary>%USERPROFILE%\Videos\AssetBay - made the first time it's needed.</summary>
        public static string Folder
        {
            get
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "AssetBay");
                try { Directory.CreateDirectory(path); } catch { /* read-only profile */ }
                return path;
            }
        }

        private static readonly string[] Kinds = { ".mp4", ".webm", ".mov", ".m4v" };

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            var rows = new List<RowSpec>();
            var tablet = ctx.Tablet;
            string folder = Folder;

            rows.Add(RowSpec.Info("where", "Folder", "Videos\\AssetBay"));
            rows.Add(RowSpec.Message("how", "Put .mp4 files in that folder. They play on your tablet and stay on your PC."));

            string[] files;
            try { files = Directory.GetFiles(folder); }
            catch (Exception e) { rows.Add(RowSpec.Message("err", "Couldn't read the folder: " + e.Message)); return rows; }

            int found = 0;
            foreach (var file in files)
            {
                if (Array.IndexOf(Kinds, Path.GetExtension(file).ToLowerInvariant()) < 0) continue;
                string path = file, name = Path.GetFileNameWithoutExtension(file);
                found++;
                rows.Add(new RowSpec
                {
                    Key = "f:" + name, Label = name, Value = Size(file),
                    IsOn = tablet.IsOpen && tablet.NowPlaying == name,
                    OnClick = () => { tablet.PlayAny(path, name); ctx.RefreshNow(); },
                });
            }
            if (found == 0) rows.Add(RowSpec.Info("none", "No videos in that folder yet"));
            rows.Add(new RowSpec { Key = "refresh", Label = "Look again", OnClick = ctx.RefreshNow });
            return rows;
        }

        private static string Size(string file)
        {
            try
            {
                long bytes = new FileInfo(file).Length;
                return bytes > 1024L * 1024L * 1024L ? $"{bytes / 1024f / 1024f / 1024f:0.#} GB" : $"{bytes / 1024f / 1024f:0} MB";
            }
            catch { return ""; }
        }
    }
}
