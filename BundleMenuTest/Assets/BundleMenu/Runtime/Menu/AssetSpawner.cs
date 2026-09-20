using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BundleMenu
{
    /// <summary>
    /// Turns loaded assets into something you can see in Play Mode:
    /// prefabs are spawned in front of the viewer, audio plays, everything else is described.
    /// Tracks what came from which bundle so "unload + destroy" can clean up after itself.
    /// </summary>
    public sealed class AssetSpawner : MonoBehaviour
    {
        public float SpawnDistance = 1.6f;
        public int MaxPerBundle = 24;

        private readonly Dictionary<string, List<GameObject>> spawned = new Dictionary<string, List<GameObject>>();
        private readonly HashSet<GameObject> roots = new HashSet<GameObject>();
        private Transform container;

        /// <summary>How many things of yours are in the world right now.</summary>
        public int Count => roots.Count;

        /// <summary>The last GameObject asset used, so the gun can place more of it.</summary>
        public GameObject LastPrefab { get; private set; }
        public string LastBundleId { get; private set; }

        public bool IsSpawned(GameObject go) => go != null && roots.Contains(go);

        /// <summary>Walks up from a collider hit to the spawned root object, if any.</summary>
        public GameObject SpawnedRootOf(Transform t)
        {
            for (; t != null; t = t.parent)
                if (roots.Contains(t.gameObject)) return t.gameObject;
            return null;
        }

        public bool Despawn(GameObject go)
        {
            if (go == null || !roots.Remove(go)) return false;
            foreach (var list in spawned.Values)
                if (list.Remove(go)) break;
            Destroy(go);
            return true;
        }

        /// <summary>
        /// Takes ownership of something built elsewhere (the gun's platforms and beacons), so it counts
        /// as yours: the Delete mode can remove it and "clear what I spawned" tidies it away.
        /// </summary>
        public GameObject Adopt(GameObject go, string bundleId)
        {
            if (go == null) return null;
            go.transform.SetParent(Container, worldPositionStays: true);
            Track(bundleId, go);
            return go;
        }

        /// <summary>Puts an object on the books, dropping the oldest when a bundle is over its limit.</summary>
        private void Track(string bundleId, GameObject go)
        {
            var list = ListFor(bundleId);
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i] == null) list.RemoveAt(i);
            if (list.Count >= MaxPerBundle)
            {
                roots.Remove(list[0]);
                Destroy(list[0]);
                list.RemoveAt(0);
            }
            list.Add(go);
            roots.Add(go);
        }

        /// <summary>Instantiate a prefab at an exact pose (used by the gun).</summary>
        public GameObject SpawnAt(GameObject prefab, string bundleId, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, position, rotation, Container);
            go.name = prefab.name;
            FixMaterials(go);
            go.SetActive(true);
            go.AddComponent<SpawnPop>();
            Track(bundleId, go);
            return go;
        }

        /// <summary>Returns a one-line human description of what happened.</summary>
        public string Use(Object asset, string bundleId, Camera viewer)
        {
            switch (asset)
            {
                case null:
                    return "Nothing to spawn.";

                case GameObject prefab:
                {
                    LastPrefab = prefab;
                    LastBundleId = bundleId;
                    var go = Instantiate(prefab, Container);
                    go.name = prefab.name;
                    FixMaterials(go);
                    go.transform.position = SpawnPoint(viewer, Count);
                    go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    go.SetActive(true);
                    go.AddComponent<SpawnPop>();
                    Track(bundleId, go);
                    return $"Spawned {prefab.name}";
                }

                case AudioClip clip:
                    AudioSource.PlayClipAtPoint(clip, viewer != null ? viewer.transform.position : Vector3.zero);
                    return $"Playing {clip.name} ({clip.length:0.0}s)";

                case Texture2D tex:
                    return $"Texture {tex.name} {tex.width}x{tex.height}";

                case Material mat:
                    return $"Material {mat.name} ({(mat.shader != null ? mat.shader.name : "no shader")})";

                default:
                    return $"Loaded {asset.GetType().Name} '{asset.name}'";
            }
        }

        private readonly List<Material> fixedMaterials = new List<Material>();
        private Material litTemplate;

        /// <summary>
        /// Bundles built for a different render pipeline carry shaders the game doesn't have (they render pink).
        /// Swap those for a copy of the game's own lit material, keeping each material's colour.
        /// </summary>
        public void FixMaterials(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m != null && m.shader != null && m.shader.isSupported && m.shader.name != "Hidden/InternalErrorShader") continue;
                    if (litTemplate == null)
                    {
                        var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        litTemplate = new Material(probe.GetComponent<Renderer>().sharedMaterial);
                        DestroyImmediate(probe);
                    }
                    var color = m != null && m.HasProperty("_Color") ? m.GetColor("_Color") : Color.gray;
                    var replacement = new Material(litTemplate) { name = (m != null ? m.name : "Material") + " (fixed)" };
                    if (replacement.HasProperty("_Color")) replacement.SetColor("_Color", color);
                    if (replacement.HasProperty("_BaseColor")) replacement.SetColor("_BaseColor", color);
                    fixedMaterials.Add(replacement);
                    mats[i] = replacement;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        public void OnBundleUnloading(BundleEntry entry, bool destroysObjects)
        {
            // With Unload(false) spawned copies keep working, so leave them.
            // With Unload(true) their meshes/materials vanish, so remove them rather than leave pink husks.
            if (destroysObjects) ClearBundle(entry.Id);
        }

        public void ClearBundle(string bundleId)
        {
            if (LastBundleId == bundleId) { LastPrefab = null; LastBundleId = null; }
            if (!spawned.TryGetValue(bundleId, out var list)) return;
            foreach (var g in list)
            {
                if (g == null) continue;
                roots.Remove(g);
                Destroy(g);
            }
            spawned.Remove(bundleId);
        }

        private readonly List<string> bundleIds = new List<string>();

        public void ClearAll()
        {
            bundleIds.Clear();
            bundleIds.AddRange(spawned.Keys);
            foreach (var id in bundleIds) ClearBundle(id);
            roots.Clear();
        }

        private void OnDestroy()
        {
            // Ejecting the menu removes everything it put in the world.
            ClearAll();
            if (container != null) Destroy(container.gameObject);
            foreach (var m in fixedMaterials) if (m != null) Destroy(m);
            if (litTemplate != null) Destroy(litTemplate);
        }

        private List<GameObject> ListFor(string id)
        {
            if (!spawned.TryGetValue(id, out var list)) spawned[id] = list = new List<GameObject>();
            return list;
        }

        private Transform Container
        {
            get
            {
                if (container == null) container = new GameObject("[BundleMenu Spawned]").transform;
                return container;
            }
        }

        private Vector3 SpawnPoint(Camera viewer, int index)
        {
            if (viewer == null) return new Vector3((index % 6) - 2.5f, 0.5f, 3f);
            var t = viewer.transform;
            var forward = Vector3.ProjectOnPlane(t.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = t.up;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            // Fan new spawns left/right so they don't stack on each other.
            float side = ((index % 7) - 3) * 0.45f;
            return t.position + forward * SpawnDistance + right * side + Vector3.down * 0.35f;
        }

        private sealed class SpawnPop : MonoBehaviour
        {
            private Vector3 target;
            private float t;

            private void Start()
            {
                target = transform.localScale;
                transform.localScale = Vector3.zero;
            }

            private void Update()
            {
                t += Time.unscaledDeltaTime / 0.35f;
                transform.localScale = target * Ease.OutBack(Mathf.Clamp01(t), 2f);
                if (t >= 1f) Destroy(this);
            }
        }
    }
}
