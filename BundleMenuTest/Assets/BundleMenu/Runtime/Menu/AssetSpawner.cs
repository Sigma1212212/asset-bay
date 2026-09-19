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
        private Transform container;

        public int Count => spawned.Values.Sum(l => l.Count(g => g != null));

        /// <summary>The last GameObject asset used, so the gun can place more of it.</summary>
        public GameObject LastPrefab { get; private set; }
        public string LastBundleId { get; private set; }

        public bool IsSpawned(GameObject go) =>
            go != null && spawned.Values.Any(list => list.Contains(go));

        /// <summary>Walks up from a collider hit to the spawned root object, if any.</summary>
        public GameObject SpawnedRootOf(Transform t)
        {
            for (; t != null; t = t.parent)
                if (IsSpawned(t.gameObject)) return t.gameObject;
            return null;
        }

        public bool Despawn(GameObject go)
        {
            foreach (var list in spawned.Values)
                if (list.Remove(go)) { Destroy(go); return true; }
            return false;
        }

        /// <summary>Instantiate a prefab at an exact pose (used by the gun).</summary>
        public GameObject SpawnAt(GameObject prefab, string bundleId, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;
            var list = ListFor(bundleId);
            list.RemoveAll(g => g == null);
            if (list.Count >= MaxPerBundle) { Destroy(list[0]); list.RemoveAt(0); }

            var go = Instantiate(prefab, position, rotation, Container);
            go.name = prefab.name;
            go.SetActive(true);
            go.AddComponent<SpawnPop>();
            list.Add(go);
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
                    var list = ListFor(bundleId);
                    list.RemoveAll(g => g == null);
                    if (list.Count >= MaxPerBundle)
                    {
                        Destroy(list[0]);
                        list.RemoveAt(0);
                    }

                    var go = Instantiate(prefab, Container);
                    go.name = prefab.name;
                    go.transform.position = SpawnPoint(viewer, Count);
                    go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    go.SetActive(true);
                    go.AddComponent<SpawnPop>();
                    list.Add(go);
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
            foreach (var g in list) if (g != null) Destroy(g);
            spawned.Remove(bundleId);
        }

        public void ClearAll()
        {
            foreach (var id in spawned.Keys.ToList()) ClearBundle(id);
        }

        private void OnDestroy()
        {
            // Ejecting the menu removes everything it put in the world.
            ClearAll();
            if (container != null) Destroy(container.gameObject);
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
