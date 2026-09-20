using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Gun modes that move you around. They all use the mods' lobby rule: offline, private or modded
    /// rooms only, and they only ever move your own player.
    /// </summary>
    public abstract class MovementGunMode : GunMode
    {
        protected readonly ModContext Ctx;
        protected MovementGunMode(ModContext ctx) => Ctx = ctx;

        protected bool Allowed => Ctx.Allowed == null || Ctx.Allowed();

        public override Color? Tint(GunHit hit) =>
            !Allowed || !hit.HasHit ? new Color(0.55f, 0.55f, 0.6f) : (Color?)null;
    }

    /// <summary>Grapple: fire at a surface and you're yanked toward it, arcing up so you clear ledges.</summary>
    public sealed class GrappleMode : MovementGunMode
    {
        public GrappleMode(ModContext ctx) : base(ctx) { }

        public override string Name => "Grapple";
        public override string Hint => "Shoot a wall and you fly to it. Works like the Fly mod - private rooms only.";
        public override float Cooldown => 0.35f;

        public override string Describe(GunHit hit) =>
            !Allowed ? "paused (public lobby)" : hit.HasHit ? $"{hit.Distance:0.0} m" : "no anchor";

        public override string Fire(GunHit hit)
        {
            if (!Allowed) return "Grapple is off in public lobbies.";
            if (!hit.HasHit) return "Nothing to grab onto.";
            var rb = Ctx.Player?.Body;
            if (rb == null) return "Your player isn't loaded yet.";

            var to = hit.Point - rb.position;
            float speed = Mathf.Clamp(to.magnitude * 2.2f, 8f, 32f);
            rb.velocity = to.normalized * speed + Vector3.up * Mathf.Min(6f, to.magnitude * 0.25f);
            return $"Grapple  {to.magnitude:0.0} m";
        }
    }

    /// <summary>Teleport: blink to wherever you point, standing just off the surface.</summary>
    public sealed class TeleportMode : MovementGunMode
    {
        public TeleportMode(ModContext ctx) : base(ctx) { }

        public override string Name => "Teleport";
        public override string Hint => "Blink to where you point. Private rooms only.";
        public override float Cooldown => 0.4f;

        public override string Describe(GunHit hit) =>
            !Allowed ? "paused (public lobby)" : hit.HasHit ? $"{hit.Distance:0.0} m" : "point at a surface";

        public override string Fire(GunHit hit)
        {
            if (!Allowed) return "Teleporting is off in public lobbies.";
            if (!hit.HasHit) return "Point at a surface.";
            var player = Ctx.Player;
            if (player == null) return "Your player isn't loaded yet.";

            // Stand on floors; step off walls and ceilings so you don't end up inside them.
            bool floor = Vector3.Angle(hit.Normal, Vector3.up) < 50f;
            var target = hit.Point + (floor ? Vector3.up * 0.15f : hit.Normal * 0.6f);
            var facing = Quaternion.Euler(0f, Ctx.Rig?.Camera != null ? Ctx.Rig.Camera.transform.eulerAngles.y : 0f, 0f);
            return player.TeleportTo(target, facing) ? $"Teleported {hit.Distance:0.0} m" : "Couldn't teleport.";
        }
    }

    /// <summary>
    /// Platform gun: shoots a solid platform you can stand on, flat on floors and sticking out of walls.
    /// It's the Platforms mod, aimed. They stack up to a limit and the Delete mode clears them.
    /// </summary>
    public sealed class PlatformGunMode : MovementGunMode
    {
        private readonly AssetSpawner spawner;
        private readonly List<GameObject> placed = new List<GameObject>();
        public float Size = 1.1f;
        private const int Limit = 24;

        public PlatformGunMode(ModContext ctx, AssetSpawner spawner) : base(ctx) => this.spawner = spawner;

        public override string Name => "Platform";
        public override string Hint => "Shoot platforms to stand on. Hold the trigger to lay a path.";
        public override float Cooldown => 0.18f;
        public override bool Automatic => true;

        public override string Describe(GunHit hit) =>
            !Allowed ? "paused (public lobby)" : placed.Count + " out, up to " + Limit;

        public override string Fire(GunHit hit)
        {
            if (!Allowed) return "Platforms are off in public lobbies.";
            var player = Ctx.Player;
            if (player == null) return "Your player isn't loaded yet.";

            // Lie flat on floors and ceilings, stick out of walls like a shelf.
            var normal = hit.HasHit ? hit.Normal : Vector3.up;
            bool wall = Vector3.Angle(normal, Vector3.up) > 55f;
            var rotation = wall
                ? Quaternion.LookRotation(Vector3.ProjectOnPlane(normal, Vector3.up), Vector3.up)
                : Quaternion.Euler(0f, Ctx.Rig?.Camera != null ? Ctx.Rig.Camera.transform.eulerAngles.y : 0f, 0f);
            var point = (hit.HasHit ? hit.Point : hit.Ray.GetPoint(8f)) + (wall ? normal * (Size * 0.45f) : Vector3.up * 0.03f);

            var go = PlatformsMod.Drop(point, rotation, player.WalkableLayer, Ctx.Theme?.Invoke(), Size);
            if (go == null) return "Couldn't place a platform.";
            go.name = "Platform";
            go.AddComponent<SelfCleanMaterials>();
            spawner.Adopt(go, "gun-platforms");

            placed.RemoveAll(p => p == null);
            placed.Add(go);
            if (placed.Count > Limit)
            {
                spawner.Despawn(placed[0]);
                placed.RemoveAt(0);
            }
            return null;   // silent: you'll be firing a lot of these
        }
    }

    /// <summary>
    /// Waypoints: fire to drop a beacon, fire at a beacon to travel to it. Handy for long climbs -
    /// leave one at the bottom and come back whenever you like.
    /// </summary>
    public sealed class WaypointMode : MovementGunMode
    {
        private readonly AssetSpawner spawner;
        private readonly List<GameObject> beacons = new List<GameObject>();
        private const int Limit = 8;

        public WaypointMode(ModContext ctx, AssetSpawner spawner) : base(ctx) => this.spawner = spawner;

        public override string Name => "Waypoint";
        public override string Hint => "Shoot the ground to leave a beacon; shoot a beacon to travel to it.";
        public override float Cooldown => 0.3f;

        public override Color? Tint(GunHit hit) =>
            BeaconAt(hit) != null ? new Color(0.4f, 1f, 0.6f) : base.Tint(hit);

        public override string Describe(GunHit hit) =>
            BeaconAt(hit) != null ? "travel here" : beacons.Count + " beacons";

        public override string Fire(GunHit hit)
        {
            var existing = BeaconAt(hit);
            if (existing != null)
            {
                if (!Allowed) return "Travelling is off in public lobbies.";
                var player = Ctx.Player;
                if (player == null) return "Your player isn't loaded yet.";
                var facing = Quaternion.Euler(0f, Ctx.Rig?.Camera != null ? Ctx.Rig.Camera.transform.eulerAngles.y : 0f, 0f);
                return player.TeleportTo(existing.transform.position + Vector3.up * 0.2f, facing) ? "Travelled to a beacon" : "Couldn't travel there.";
            }

            if (!hit.HasHit) return "Point at the ground.";
            var beacon = BuildBeacon(hit.Point, Ctx.Theme?.Invoke());
            spawner.Adopt(beacon, "gun-waypoints");
            beacons.RemoveAll(b => b == null);
            beacons.Add(beacon);
            if (beacons.Count > Limit) { spawner.Despawn(beacons[0]); beacons.RemoveAt(0); }
            return "Beacon dropped";
        }

        private GameObject BeaconAt(GunHit hit)
        {
            if (hit.Collider == null) return null;
            var root = hit.Collider.transform.root.gameObject;
            for (int i = 0; i < beacons.Count; i++)
                if (beacons[i] != null && (beacons[i] == root || beacons[i] == hit.Collider.gameObject || hit.Collider.transform.IsChildOf(beacons[i].transform)))
                    return beacons[i];
            return null;
        }

        private static GameObject BuildBeacon(Vector3 point, MenuTheme theme)
        {
            var root = new GameObject("Beacon");
            root.transform.position = point;

            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.SetParent(root.transform, false);
            post.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            post.transform.localScale = new Vector3(0.07f, 0.45f, 0.07f);

            var top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            top.name = "Light";
            top.transform.SetParent(root.transform, false);
            top.transform.localPosition = new Vector3(0f, 1f, 0f);
            top.transform.localScale = Vector3.one * 0.18f;

            var accent = theme != null ? theme.Accent : new Color(0.3f, 0.9f, 1f);
            Tint(post, theme != null ? theme.PanelTop : Color.gray);
            Tint(top, accent);
            root.AddComponent<SelfCleanMaterials>();
            root.AddComponent<Bobber>().Set(top.transform, accent);
            return root;
        }

        private static void Tint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var mat = new Material(renderer.sharedMaterial) { name = "beacon (menu)" };
            if (mat.HasProperty("_Color")) mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            renderer.sharedMaterial = mat;
        }
    }

    /// <summary>Gentle up-and-down float with a slow spin, for beacons and pick-ups.</summary>
    public sealed class Bobber : MonoBehaviour
    {
        private Transform target;
        private float baseY, phase;

        public void Set(Transform t, Color color)
        {
            target = t;
            baseY = t.localPosition.y;
            phase = Random.value * 10f;
        }

        private void Update()
        {
            if (target == null) { enabled = false; return; }
            float t = Time.time + phase;
            var p = target.localPosition;
            p.y = baseY + Mathf.Sin(t * 2f) * 0.06f;
            target.localPosition = p;
            target.Rotate(0f, 60f * Time.deltaTime, 0f, Space.Self);
        }
    }

    /// <summary>
    /// Destroys the materials an object made for itself, so nothing is left behind. Only materials the
    /// menu created are touched - they're named "(menu)", so the game's own materials are never harmed.
    /// </summary>
    public sealed class SelfCleanMaterials : MonoBehaviour
    {
        private void OnDestroy()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var mats = renderers[i].sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                    if (mats[m] != null && mats[m].name.EndsWith("(menu)")) Destroy(mats[m]);
            }

            var colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                var surface = colliders[i].sharedMaterial;
                if (surface != null && surface.name.EndsWith("(menu)")) Destroy(surface);
            }
        }
    }
}
