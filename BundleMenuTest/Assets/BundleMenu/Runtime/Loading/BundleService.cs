using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BundleMenu
{
    /// <summary>
    /// Owns every bundle's runtime state. The UI only reads <see cref="BundleEntry"/> objects and calls
    /// the public methods here; it never touches AssetBundle directly.
    ///
    /// Dependencies: loading X also loads everything in X.Descriptor.Dependencies (already transitive).
    /// Each dependency is reference-counted, so unloading X only frees a dependency once nothing else holds it.
    /// </summary>
    public sealed class BundleService : IDisposable
    {
        private readonly Dictionary<string, BundleEntry> entries = new Dictionary<string, BundleEntry>();
        private readonly List<BundleEntry> ordered = new List<BundleEntry>();

        public IBundleSource Source { get; private set; }
        public bool IsRefreshing { get; private set; }
        public string LastCatalogError { get; private set; }

        /// <summary>How unloading treats objects already pulled out of the bundle.</summary>
        public bool UnloadAllLoadedObjects { get; set; }

        /// <summary>Fires whenever any single entry changes (status, progress, error).</summary>
        public event Action<BundleEntry> EntryChanged;

        /// <summary>Fires after the bundle list is (re)built.</summary>
        public event Action CatalogChanged;

        /// <summary>Fires right before a bundle is unloaded (spawners use it to clean up instances).</summary>
        public event Action<BundleEntry, bool> BundleUnloading;

        public IReadOnlyList<BundleEntry> Entries => ordered;

        public BundleService(IBundleSource source) => Source = source ?? throw new ArgumentNullException(nameof(source));

        // ------------------------------------------------------------------ catalog

        public IEnumerable<string> Categories => ordered.Select(e => e.Category).Distinct();

        public IEnumerable<BundleEntry> InCategory(string category) => ordered.Where(e => e.Category == category);

        public BundleEntry Get(string id) => id != null && entries.TryGetValue(id, out var e) ? e : null;

        /// <summary>Swap to a different source. Unloads everything from the old one first.</summary>
        public Task SetSourceAsync(IBundleSource source)
        {
            UnloadAll();
            entries.Clear();   // different source = different bundles; start clean
            ordered.Clear();
            Source = source ?? throw new ArgumentNullException(nameof(source));
            return RefreshCatalogAsync();
        }

        public async Task RefreshCatalogAsync()
        {
            if (IsRefreshing) return;
            IsRefreshing = true;
            LastCatalogError = null;
            try
            {
                var descriptors = await Source.ListAsync() ?? Array.Empty<BundleDescriptor>();

                // Keep live entries for bundles that still exist, drop the rest.
                var keep = new HashSet<string>(descriptors.Where(d => d != null).Select(d => d.Id));
                foreach (var stale in ordered.Where(e => !keep.Contains(e.Id)).ToList())
                    ForceUnload(stale);

                ordered.Clear();
                foreach (var d in descriptors)
                {
                    if (d == null || string.IsNullOrEmpty(d.Id)) continue;
                    if (!entries.TryGetValue(d.Id, out var entry))
                        entries[d.Id] = entry = new BundleEntry(d);
                    if (!ordered.Contains(entry)) ordered.Add(entry);
                }
                foreach (var id in entries.Keys.Where(k => !keep.Contains(k)).ToList())
                    entries.Remove(id);
            }
            catch (Exception e)
            {
                LastCatalogError = e.Message;
                Debug.LogError($"[BundleMenu] Listing bundles from '{Source.Name}' failed: {e}");
            }
            finally
            {
                IsRefreshing = false;
                CatalogChanged?.Invoke();
            }
        }

        // ------------------------------------------------------------------ load / unload

        /// <summary>Load a bundle the user asked for. Returns false (never throws) on failure.</summary>
        public async Task<bool> LoadAsync(string id)
        {
            var entry = Get(id);
            if (entry == null)
            {
                Debug.LogWarning($"[BundleMenu] No bundle with id '{id}'.");
                return false;
            }

            entry.Requested = true;

            // Already in memory because another bundle depends on it: take our own hold on its dependencies
            // so they survive if that other bundle is unloaded first.
            if (entry.IsLoaded && entry.HeldDependencies.Count == 0 && (entry.Descriptor.Dependencies?.Length ?? 0) > 0)
            {
                try { await AcquireDependencies(entry); }
                catch (Exception e) { Debug.LogWarning($"[BundleMenu] {entry.Id}: {e.Message}"); }
                Notify(entry);
                return true;
            }

            bool ok = await EnsureLoaded(entry, holdDependencies: true);
            if (!ok) entry.Requested = false;
            return ok;
        }

        /// <summary>User-level unload. A bundle still needed by others stays in memory until they let go.</summary>
        public void Unload(string id)
        {
            var entry = Get(id);
            if (entry == null) return;
            entry.Requested = false;
            TryRelease(entry);
            Notify(entry);
        }

        public Task<bool[]> LoadCategoryAsync(string category) =>
            Task.WhenAll(InCategory(category).Where(e => !e.IsLoaded || !e.Requested).Select(e => LoadAsync(e.Id)).ToList());

        public void UnloadCategory(string category)
        {
            foreach (var e in InCategory(category).ToList()) Unload(e.Id);
        }

        public void UnloadAll()
        {
            foreach (var e in ordered.ToList()) e.Requested = false;
            foreach (var e in ordered.ToList()) TryRelease(e);
            // Anything left (e.g. mid-load) gets dropped hard.
            foreach (var e in ordered.Where(x => x.Handle != null).ToList()) ForceUnload(e);
        }

        public async Task<Object> LoadAssetAsync(string bundleId, string assetName)
        {
            var entry = Get(bundleId);
            if (entry?.Handle == null)
                throw new InvalidOperationException($"Bundle '{bundleId}' is not loaded.");
            return await entry.Handle.LoadAssetAsync(assetName);
        }

        public void Dispose()
        {
            UnloadAll();
            EntryChanged = null;
            CatalogChanged = null;
            BundleUnloading = null;
        }

        // ------------------------------------------------------------------ internals

        private Task<bool> EnsureLoaded(BundleEntry entry, bool holdDependencies)
        {
            if (entry.Status == BundleStatus.Loaded) return Task.FromResult(true);
            if (entry.InFlight != null) return entry.InFlight;

            var task = LoadCore(entry, holdDependencies);
            // LoadCore can finish synchronously (e.g. a missing dependency throws before the first await).
            // Only remember it as in-flight if it is genuinely still running, or retries would be stuck.
            entry.InFlight = task.IsCompleted ? null : task;
            return task;
        }

        private async Task<bool> LoadCore(BundleEntry entry, bool holdDependencies)
        {
            entry.Status = BundleStatus.Loading;
            entry.Progress = 0f;
            entry.Error = null;
            Notify(entry);

            try
            {
                // Dependencies first. Loaded as leaves: Descriptor.Dependencies is already the full transitive set,
                // so we never recurse (which also makes cyclic bundle graphs safe).
                if (holdDependencies) await AcquireDependencies(entry);

                var progress = new InlineProgress(p =>
                {
                    if (entry.Status != BundleStatus.Loading) return;
                    entry.Progress = p;
                    Notify(entry);
                });

                var handle = await Source.LoadAsync(entry.Descriptor, progress);
                if (handle == null) throw new InvalidOperationException("Source returned no bundle.");

                // The catalog was rebuilt / source swapped while we were loading: this entry is orphaned.
                if (!entries.TryGetValue(entry.Id, out var current) || current != entry)
                {
                    handle.Unload(true);
                    entry.InFlight = null;
                    entry.Status = BundleStatus.Unloaded;
                    ReleaseDependencies(entry);
                    return false;
                }

                entry.Handle = handle;
                entry.Status = BundleStatus.Loaded;
                entry.Progress = 1f;
                entry.InFlight = null;
                Notify(entry);

                // Somebody may have hit "unload" while we were loading.
                TryRelease(entry);
                return entry.Status == BundleStatus.Loaded;
            }
            catch (Exception e)
            {
                entry.InFlight = null;
                entry.Handle = null;
                entry.Status = BundleStatus.Failed;
                entry.Error = e is OperationCanceledException ? "Cancelled." : e.Message;
                Debug.LogWarning($"[BundleMenu] Loading '{entry.Id}' failed: {entry.Error}");
                ReleaseDependencies(entry);
                Notify(entry);
                return false;
            }
        }

        private async Task AcquireDependencies(BundleEntry entry)
        {
            foreach (string depId in entry.Descriptor.Dependencies ?? Array.Empty<string>())
            {
                var dep = Get(depId);
                if (dep == null) throw new InvalidOperationException($"Missing dependency '{depId}'.");

                dep.DependentCount++;
                entry.HeldDependencies.Add(dep);
                if (!await EnsureLoaded(dep, holdDependencies: false))
                    throw new InvalidOperationException($"Dependency '{dep.DisplayName}' failed: {dep.Error}");
            }
        }

        /// <summary>Unload if nobody wants this bundle any more.</summary>
        private void TryRelease(BundleEntry entry)
        {
            if (entry.Requested || entry.DependentCount > 0) return;
            if (entry.Status == BundleStatus.Loading) return; // LoadCore re-checks when it finishes

            if (entry.Handle != null)
            {
                BundleUnloading?.Invoke(entry, UnloadAllLoadedObjects);
                try { entry.Handle.Unload(UnloadAllLoadedObjects); }
                catch (Exception e) { Debug.LogException(e); }
                entry.Handle = null;
            }

            if (entry.Status == BundleStatus.Loaded) entry.Status = BundleStatus.Unloaded;
            entry.Progress = 0f;
            ReleaseDependencies(entry);
            Notify(entry);
        }

        private void ReleaseDependencies(BundleEntry entry)
        {
            var held = entry.HeldDependencies.ToList();
            entry.HeldDependencies.Clear();
            foreach (var dep in held)
            {
                dep.DependentCount = Mathf.Max(0, dep.DependentCount - 1);
                TryRelease(dep);
            }
        }

        private void ForceUnload(BundleEntry entry)
        {
            entry.Requested = false;
            entry.DependentCount = 0;
            if (entry.Handle != null)
            {
                BundleUnloading?.Invoke(entry, UnloadAllLoadedObjects);
                try { entry.Handle.Unload(UnloadAllLoadedObjects); }
                catch (Exception e) { Debug.LogException(e); }
            }
            entry.Handle = null;
            entry.HeldDependencies.Clear();
            entry.Status = BundleStatus.Unloaded;
            entry.Progress = 0f;
            Notify(entry);
        }

        private void Notify(BundleEntry entry)
        {
            try { EntryChanged?.Invoke(entry); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
