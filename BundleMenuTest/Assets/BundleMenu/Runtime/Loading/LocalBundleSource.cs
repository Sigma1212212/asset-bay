using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BundleMenu
{
    /// <summary>
    /// Loads AssetBundles from a folder on disk (default: StreamingAssets/Bundles).
    ///
    /// Discovery, in order of preference:
    ///  1. The build manifest bundle Unity writes next to your bundles (named after the folder,
    ///     e.g. "Bundles"). Gives the exact bundle list plus dependencies.
    ///  2. A plain file scan (every file that isn't .manifest / .meta / .json).
    ///
    /// Optional "catalog.json" in the same folder overrides display names / categories:
    ///   { "bundles": [ { "Id": "props/crates", "DisplayName": "Crates", "Category": "Props" } ] }
    ///
    /// Default naming: a bundle named "props/wooden_crates" shows as "Wooden Crates" in category "Props".
    /// Note: file scanning works on desktop platforms. On Android, StreamingAssets lives inside the APK,
    /// so ship the manifest (or implement a UnityWebRequest-based source against the same interface).
    /// </summary>
    public sealed class LocalBundleSource : IBundleSource
    {
        private readonly string folder;

        public LocalBundleSource(string folder = null)
        {
            this.folder = string.IsNullOrEmpty(folder)
                ? Path.Combine(Application.streamingAssetsPath, "Bundles")
                : folder;
        }

        public string Name => "Local";
        public string Folder => folder;

        public async Task<IReadOnlyList<BundleDescriptor>> ListAsync()
        {
            if (!Directory.Exists(folder))
            {
                Debug.LogWarning($"[BundleMenu] Bundle folder not found: {folder}");
                return Array.Empty<BundleDescriptor>();
            }

            var overrides = ReadCatalog();
            var list = await TryListFromManifest() ?? ListFromFiles();

            foreach (var d in list)
            {
                if (overrides.TryGetValue(d.Id, out var o))
                {
                    if (!string.IsNullOrEmpty(o.DisplayName)) d.DisplayName = o.DisplayName;
                    if (!string.IsNullOrEmpty(o.Category)) d.Category = o.Category;
                }
            }

            return list.OrderBy(d => d.Category).ThenBy(d => d.DisplayName).ToList();
        }

        public async Task<ILoadedBundle> LoadAsync(BundleDescriptor descriptor, IProgress<float> progress)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (!File.Exists(descriptor.Location))
                throw new FileNotFoundException($"Bundle file is missing: {descriptor.Location}");

            // Unity refuses to load the same bundle twice. If something else already loaded it, reuse it.
            var existing = AssetBundle.GetAllLoadedAssetBundles().FirstOrDefault(b => b != null && b.name == descriptor.Id);
            if (existing != null)
            {
                progress?.Report(1f);
                return new AssetBundleHandle(existing);
            }

            var request = await AssetBundle.LoadFromFileAsync(descriptor.Location).Await(progress);
            if (request.assetBundle == null)
                throw new InvalidOperationException(
                    "Unity could not open the bundle. It may be corrupt, built for a different platform, " +
                    "or built with an incompatible Unity version.");

            return new AssetBundleHandle(request.assetBundle);
        }

        // ---------------------------------------------------------------- discovery

        private async Task<List<BundleDescriptor>> TryListFromManifest()
        {
            string manifestPath = Path.Combine(folder, Path.GetFileName(folder.TrimEnd('/', '\\')));
            if (!File.Exists(manifestPath)) return null;

            AssetBundle manifestBundle = null;
            try
            {
                var req = await AssetBundle.LoadFromFileAsync(manifestPath).Await();
                manifestBundle = req.assetBundle;
                if (manifestBundle == null) return null;

                var assetReq = await manifestBundle.LoadAssetAsync<AssetBundleManifest>("AssetBundleManifest").Await();
                var manifest = assetReq.asset as AssetBundleManifest;
                if (manifest == null) return null;

                return manifest.GetAllAssetBundles()
                    .Select(name => MakeDescriptor(name, manifest.GetAllDependencies(name)))
                    .ToList();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BundleMenu] Could not read manifest bundle, falling back to a file scan. {e.Message}");
                return null;
            }
            finally
            {
                if (manifestBundle != null) manifestBundle.Unload(false);
            }
        }

        private List<BundleDescriptor> ListFromFiles()
        {
            string manifestName = Path.GetFileName(folder.TrimEnd('/', '\\'));
            return Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .Where(p =>
                {
                    string ext = Path.GetExtension(p).ToLowerInvariant();
                    return ext != ".manifest" && ext != ".meta" && ext != ".json"
                        && Path.GetFileName(p) != manifestName;
                })
                .Select(p => MakeDescriptor(RelativeId(p), Array.Empty<string>()))
                .ToList();
        }

        private BundleDescriptor MakeDescriptor(string id, string[] deps)
        {
            string[] parts = id.Split('/');
            return new BundleDescriptor
            {
                Id = id,
                DisplayName = Prettify(parts[parts.Length - 1]),
                Category = parts.Length > 1 ? Prettify(parts[0]) : "General",
                Location = Path.Combine(folder, id.Replace('/', Path.DirectorySeparatorChar)),
                Dependencies = deps ?? Array.Empty<string>(),
            };
        }

        private string RelativeId(string fullPath) =>
            fullPath.Substring(folder.Length).TrimStart('/', '\\').Replace('\\', '/');

        private Dictionary<string, BundleDescriptor> ReadCatalog()
        {
            var result = new Dictionary<string, BundleDescriptor>();
            string path = Path.Combine(folder, "catalog.json");
            if (!File.Exists(path)) return result;

            try
            {
                var catalog = JsonUtility.FromJson<CatalogFile>(File.ReadAllText(path));
                if (catalog?.bundles != null)
                    foreach (var b in catalog.bundles)
                        if (!string.IsNullOrEmpty(b?.Id)) result[b.Id] = b;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BundleMenu] catalog.json is invalid and was ignored: {e.Message}");
            }
            return result;
        }

        internal static string Prettify(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            string spaced = raw.Replace('_', ' ').Replace('-', ' ');
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
        }

        [Serializable]
        private sealed class CatalogFile { public BundleDescriptor[] bundles; }

        // ---------------------------------------------------------------- handle

        private sealed class AssetBundleHandle : ILoadedBundle
        {
            private AssetBundle bundle;
            private readonly string[] names;

            public AssetBundleHandle(AssetBundle bundle)
            {
                this.bundle = bundle;
                // Scene bundles can't list assets; show scene paths instead.
                names = bundle.isStreamedSceneAssetBundle
                    ? bundle.GetAllScenePaths()
                    : bundle.GetAllAssetNames();
            }

            public IReadOnlyList<string> AssetNames => names;

            public async Task<Object> LoadAssetAsync(string assetName)
            {
                if (bundle == null) throw new InvalidOperationException("Bundle has already been unloaded.");
                if (bundle.isStreamedSceneAssetBundle)
                    throw new NotSupportedException("This is a scene bundle. Load it with SceneManager.LoadSceneAsync.");

                var req = await bundle.LoadAssetAsync(assetName).Await();
                if (req.asset == null) throw new InvalidOperationException($"Asset '{assetName}' failed to load.");
                return req.asset;
            }

            public void Unload(bool unloadAllLoadedObjects)
            {
                if (bundle == null) return;
                bundle.Unload(unloadAllLoadedObjects);
                bundle = null;
            }
        }
    }
}
