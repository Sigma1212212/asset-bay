using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace BundleMenu
{
    /// <summary>
    /// Fake bundles for testing the menu with zero real content.
    /// Loads take a random 0.25-1.2 s with smooth progress, "assets" are coloured primitives,
    /// one bundle always fails, and <see cref="FailureRate"/> makes any load fail randomly.
    /// </summary>
    public sealed class DummyBundleSource : IBundleSource
    {
        public string Name => "Dummy";

        /// <summary>0..1 chance that any load throws. Change it at runtime from the Settings page.</summary>
        public float FailureRate { get; set; }

        public float MinLoadSeconds = 0.25f;
        public float MaxLoadSeconds = 1.2f;

        private static readonly (string id, string category, string name, string[] assets, string[] deps)[] Table =
        {
            ("shared/materials",   "Shared",      "Shared Materials", new[] { "Palette" },                          new string[0]),
            ("props/crates",       "Props",       "Crates",           new[] { "Crate", "Crate Stack", "Barrel" },   new[] { "shared/materials" }),
            ("props/furniture",    "Props",       "Furniture",        new[] { "Table", "Stool", "Lamp Post" },      new[] { "shared/materials" }),
            ("props/signs",        "Props",       "Signs",            new[] { "Arrow Sign", "Stop Sign" },          new string[0]),
            ("characters/bots",    "Characters",  "Patrol Bots",      new[] { "Bot", "Drone" },                     new[] { "shared/materials" }),
            ("characters/npcs",    "Characters",  "Townsfolk",        new[] { "Villager", "Guard" },                new string[0]),
            ("environment/rocks",  "Environment", "Rocks",            new[] { "Boulder", "Pebbles", "Pillar" },     new string[0]),
            ("environment/trees",  "Environment", "Trees",            new[] { "Pine", "Oak", "Shrub" },             new string[0]),
            ("environment/water",  "Environment", "Water Pack",       new[] { "Pond", "Fountain" },                 new string[0]),
            ("effects/sparks",     "Effects",     "Sparks",           new[] { "Spark Orb", "Ember" },               new string[0]),
            ("effects/portals",    "Effects",     "Portals",          new[] { "Portal Ring" },                      new string[0]),
            ("effects/weather",    "Effects",     "Weather",          new[] { "Rain Cloud", "Snow Globe" },         new string[0]),
            ("effects/corrupted",  "Effects",     "Corrupted Pack",   new[] { "Nothing" },                          new string[0]),
            ("vehicles/karts",     "Vehicles",    "Karts",            new[] { "Kart", "Wheel" },                    new[] { "shared/materials" }),
            ("vehicles/boats",     "Vehicles",    "Boats",            new[] { "Dinghy", "Buoy" },                   new string[0]),
        };

        public Task<IReadOnlyList<BundleDescriptor>> ListAsync()
        {
            IReadOnlyList<BundleDescriptor> list = Table.Select(t => new BundleDescriptor
            {
                Id = t.id,
                Category = t.category,
                DisplayName = t.name,
                Location = "dummy://" + t.id,
                Dependencies = t.deps,
            }).ToList();
            return Task.FromResult(list);
        }

        public async Task<ILoadedBundle> LoadAsync(BundleDescriptor descriptor, IProgress<float> progress)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            var row = Table.FirstOrDefault(t => t.id == descriptor.Id);
            if (row.id == null) throw new KeyNotFoundException($"Unknown dummy bundle '{descriptor.Id}'.");

            float duration = Random.Range(MinLoadSeconds, MaxLoadSeconds);
            bool corrupted = row.id == "effects/corrupted";
            bool randomFail = Random.value < FailureRate;
            float failAt = corrupted || randomFail ? Random.Range(0.3f, 0.85f) : 2f;

            float start = Time.unscaledTime;
            while (true)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - start) / duration);
                if (t >= failAt)
                    throw new InvalidOperationException(corrupted
                        ? "CRC mismatch - the bundle file is corrupted (simulated)."
                        : "Simulated network failure (dummy fail rate).");
                progress?.Report(t);
                if (t >= 1f) break;
                await UnityAsync.NextFrame();
            }

            return new DummyBundle(row.id, row.assets);
        }

        private sealed class DummyBundle : ILoadedBundle
        {
            private readonly string id;
            private readonly string[] assets;
            private readonly List<Object> created = new List<Object>();
            private bool unloaded;

            public DummyBundle(string id, string[] assets)
            {
                this.id = id;
                this.assets = assets;
            }

            public IReadOnlyList<string> AssetNames => assets;

            public async Task<Object> LoadAssetAsync(string assetName)
            {
                if (unloaded) throw new InvalidOperationException("Bundle has already been unloaded.");
                if (!assets.Contains(assetName)) throw new KeyNotFoundException($"'{assetName}' is not in {id}.");
                await UnityAsync.Delay(0.05f);

                if (assetName == "Palette")
                {
                    var tex = new Texture2D(4, 1) { name = "Palette", filterMode = FilterMode.Point };
                    tex.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white });
                    tex.Apply();
                    created.Add(tex);
                    return tex;
                }

                var template = BuildPrimitive(assetName);
                created.Add(template);
                return template;
            }

            public void Unload(bool unloadAllLoadedObjects)
            {
                unloaded = true;
                if (!unloadAllLoadedObjects) return;
                foreach (var o in created)
                    if (o != null) Object.Destroy(o);
                created.Clear();
            }

            private GameObject BuildPrimitive(string assetName)
            {
                // Pick a stable shape and colour from the name so the same asset always looks the same.
                int hash = Math.Abs(assetName.GetHashCode());
                var shapes = new[] { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Cylinder, PrimitiveType.Capsule };
                var go = GameObject.CreatePrimitive(shapes[hash % shapes.Length]);
                go.name = assetName;
                go.SetActive(false); // acts like a prefab: inactive template, spawner clones and activates

                // Copy the primitive's own default material instead of Shader.Find-ing one: that works in any
                // render pipeline, including a game this DLL was injected into (where "Standard" may be stripped).
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    var color = Color.HSVToRGB((hash % 360) / 360f, 0.55f, 0.95f);
                    var mat = new Material(renderer.sharedMaterial) { name = assetName };
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                    renderer.sharedMaterial = mat;
                    created.Add(mat);
                }
                return go;
            }
        }
    }
}
