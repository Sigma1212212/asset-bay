using UnityEngine;

namespace BundleMenu
{
    /// <summary>Place: spawns the last asset you used from a bundle, standing on whatever you point at.</summary>
    public sealed class PlaceMode : IGunMode
    {
        private readonly AssetSpawner spawner;
        public PlaceMode(AssetSpawner spawner) => this.spawner = spawner;

        public string Name => "Place";
        public Color? Tint(GunHit hit) => null;

        public string Describe(GunHit hit) =>
            spawner.LastPrefab != null ? spawner.LastPrefab.name : "spawn an asset first";

        public string Fire(GunHit hit)
        {
            if (spawner.LastPrefab == null) return "Spawn something from a bundle first - the gun places copies of it.";
            if (!hit.HasHit) return "Point at a surface to place it.";

            // Stand upright on floors, face the camera horizontally.
            var up = Vector3.Angle(hit.Normal, Vector3.up) < 45f ? Vector3.up : hit.Normal;
            var facing = Vector3.ProjectOnPlane(-hit.Ray.direction, up);
            var rot = facing.sqrMagnitude > 0.001f ? Quaternion.LookRotation(facing, up) : Quaternion.identity;

            var go = spawner.SpawnAt(spawner.LastPrefab, spawner.LastBundleId, hit.Point, rot);
            if (go == null) return "Couldn't place it.";
            RestOnSurface(go, hit.Point, up);
            return $"Placed {go.name}";
        }

        /// <summary>Lift the object so its lowest point sits on the surface instead of half inside it.</summary>
        private static void RestOnSurface(GameObject go, Vector3 point, Vector3 up)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float below = Vector3.Dot(point - bounds.center, up) + bounds.extents.y;
            go.transform.position += up * Mathf.Max(0f, below);
        }
    }

    /// <summary>Delete: removes objects this menu spawned. Anything else is left alone.</summary>
    public sealed class DeleteMode : IGunMode
    {
        private readonly AssetSpawner spawner;
        public DeleteMode(AssetSpawner spawner) => this.spawner = spawner;

        public string Name => "Delete";

        public Color? Tint(GunHit hit) =>
            Target(hit) != null ? new Color(1f, 0.3f, 0.35f) : (Color?)new Color(0.55f, 0.55f, 0.6f);

        public string Describe(GunHit hit) => Target(hit) is GameObject go ? go.name : "only spawned objects";

        public string Fire(GunHit hit)
        {
            var target = Target(hit);
            if (target == null) return hit.HasHit ? "That wasn't spawned by the menu." : "Nothing there.";
            string name = target.name;
            spawner.Despawn(target);
            return $"Deleted {name}";
        }

        private GameObject Target(GunHit hit) => hit.Collider != null ? spawner.SpawnedRootOf(hit.Collider.transform) : null;
    }

    /// <summary>Inspect: says what you're pointing at (object path, layer, distance).</summary>
    public sealed class InspectMode : IGunMode
    {
        public string Name => "Inspect";
        public Color? Tint(GunHit hit) => null;
        public string Describe(GunHit hit) => hit.Collider != null ? hit.Collider.name : "-";

        public string Fire(GunHit hit)
        {
            if (hit.Collider == null) return "Nothing there.";
            var t = hit.Collider.transform;
            string path = t.name;
            for (var p = t.parent; p != null && path.Length < 60; p = p.parent) path = p.name + "/" + path;
            return $"{path}  ·  layer {LayerMask.LayerToName(hit.Collider.gameObject.layer)}  ·  {hit.Distance:0.0} m";
        }
    }

    /// <summary>Measure: first shot sets point A, second shot reports the distance to point B.</summary>
    public sealed class MeasureMode : IGunMode
    {
        private Vector3? start;

        public string Name => "Measure";
        public Color? Tint(GunHit hit) => start != null ? new Color(1f, 0.8f, 0.3f) : (Color?)null;

        public string Describe(GunHit hit) =>
            start != null && hit.HasHit ? $"{Vector3.Distance(start.Value, hit.Point):0.00} m" : $"{hit.Distance:0.0} m away";

        public string Fire(GunHit hit)
        {
            if (!hit.HasHit) return "Point at a surface.";
            if (start == null)
            {
                start = hit.Point;
                return "Point A set - fire again at point B.";
            }
            float d = Vector3.Distance(start.Value, hit.Point);
            start = null;
            return $"Distance: {d:0.00} m";
        }
    }
}
