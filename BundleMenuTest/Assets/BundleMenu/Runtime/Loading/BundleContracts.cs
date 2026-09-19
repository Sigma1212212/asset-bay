using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BundleMenu
{
    /// <summary>Static description of a bundle: what it is, before anything is loaded.</summary>
    [Serializable]
    public sealed class BundleDescriptor
    {
        public string Id;             // stable key, e.g. "props/crates"
        public string DisplayName;    // "Crates"
        public string Category;       // "Props"
        public string Location;       // file path / URL / whatever the source understands
        public string[] Dependencies = Array.Empty<string>(); // ids, fully transitive

        public override string ToString() => $"{Category}/{DisplayName} ({Id})";
    }

    /// <summary>A bundle that is currently in memory.</summary>
    public interface ILoadedBundle
    {
        IReadOnlyList<string> AssetNames { get; }

        /// <summary>Throws if the asset is missing or fails to load.</summary>
        Task<Object> LoadAssetAsync(string assetName);

        void Unload(bool unloadAllLoadedObjects);
    }

    /// <summary>
    /// Where bundles come from. Swap implementations (local files, dummy data, a CDN,
    /// Addressables...) without touching the service or the UI.
    /// </summary>
    public interface IBundleSource
    {
        string Name { get; }

        Task<IReadOnlyList<BundleDescriptor>> ListAsync();

        /// <summary>Throws on failure; the service turns exceptions into a Failed state.</summary>
        Task<ILoadedBundle> LoadAsync(BundleDescriptor descriptor, IProgress<float> progress);
    }

    public enum BundleStatus { Unloaded, Loading, Loaded, Failed }

    /// <summary>Live runtime state of one bundle. Owned and mutated only by <see cref="BundleService"/>.</summary>
    public sealed class BundleEntry
    {
        public BundleDescriptor Descriptor { get; }
        public string Id => Descriptor.Id;
        public string DisplayName => Descriptor.DisplayName;
        public string Category => Descriptor.Category;

        public BundleStatus Status { get; internal set; }
        public float Progress { get; internal set; }
        public string Error { get; internal set; }
        public ILoadedBundle Handle { get; internal set; }

        /// <summary>True when the user asked for this bundle (as opposed to it being pulled in as a dependency).</summary>
        public bool Requested { get; internal set; }

        /// <summary>How many other loaded bundles are holding this one as a dependency.</summary>
        public int DependentCount { get; internal set; }

        internal readonly List<BundleEntry> HeldDependencies = new List<BundleEntry>();
        internal Task<bool> InFlight;

        public bool IsLoaded => Status == BundleStatus.Loaded;
        public bool IsHeldOnlyAsDependency => IsLoaded && !Requested && DependentCount > 0;

        internal BundleEntry(BundleDescriptor descriptor) => Descriptor = descriptor;
    }

    /// <summary>Progress reporter that is safe to use without a SynchronizationContext hop.</summary>
    internal sealed class InlineProgress : IProgress<float>
    {
        private readonly Action<float> report;
        public InlineProgress(Action<float> report) => this.report = report;
        public void Report(float value) => report(Mathf.Clamp01(value));
    }
}
