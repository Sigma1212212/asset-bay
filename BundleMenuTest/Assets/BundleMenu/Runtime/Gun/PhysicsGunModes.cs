using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BundleMenu
{
    /// <summary>
    /// Tractor beam: hold the trigger on something the menu spawned and carry it around; the scroll wheel
    /// pushes it away or pulls it closer, and letting go throws it. It only picks up your own spawned
    /// things, so the game's own world is never touched.
    /// </summary>
    public sealed class TractorMode : GunMode
    {
        private readonly AssetSpawner spawner;
        private Transform held;
        private Rigidbody heldBody;
        private float distance = 3f;
        private Vector3 lastPoint;
        private Vector3 carryVelocity;

        public TractorMode(AssetSpawner spawner) => this.spawner = spawner;

        public override string Name => "Tractor";
        public override string Hint => "Hold the trigger to carry a spawned object. Scroll moves it, letting go throws it.";
        public override float Cooldown => 0.05f;

        public override Color? Tint(GunHit hit) =>
            held != null ? new Color(0.4f, 0.9f, 1f)
            : Target(hit) != null ? (Color?)new Color(0.6f, 1f, 0.7f)
            : new Color(0.55f, 0.55f, 0.6f);

        public override string Describe(GunHit hit) =>
            held != null ? "carrying " + held.name : Target(hit) is GameObject go ? go.name : "spawned objects only";

        public override string Fire(GunHit hit) => null;   // picking up happens on press, not on the shot

        public override void Press(GunHit hit)
        {
            var target = Target(hit);
            if (target == null) return;
            held = target.transform;
            heldBody = target.GetComponent<Rigidbody>();
            distance = Mathf.Clamp(hit.Distance, 1.2f, 12f);
            lastPoint = held.position;
            carryVelocity = Vector3.zero;
            if (heldBody != null) heldBody.useGravity = false;
        }

        public override void Hold(GunHit hit)
        {
            if (held == null) return;
            float scroll = MenuInput.Scroll;
            if (scroll != 0f) distance = Mathf.Clamp(distance + scroll * 0.6f, 0.8f, 20f);

            var target = hit.Ray.GetPoint(distance);
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            carryVelocity = (held.position - lastPoint) / dt;
            lastPoint = held.position;

            if (heldBody != null)
            {
                heldBody.velocity = (target - held.position) * 9f;
                heldBody.angularVelocity *= 0.9f;
            }
            else
            {
                held.position = Vector3.Lerp(held.position, target, 1f - Mathf.Exp(-12f * dt));
            }
        }

        public override void Release(GunHit hit)
        {
            if (held == null) return;
            if (heldBody != null)
            {
                heldBody.useGravity = true;
                // Throw it the way you were swinging, with a nudge along the aim.
                heldBody.velocity = Vector3.ClampMagnitude(carryVelocity, 14f) + hit.Ray.direction * 3.5f;
            }
            held = null;
            heldBody = null;
        }

        public override void OnDeselected() => Release(default);

        private GameObject Target(GunHit hit) =>
            hit.Collider != null ? spawner.SpawnedRootOf(hit.Collider.transform) : null;
    }

    /// <summary>
    /// Impulse: hold the trigger to charge, let go for a shove. Pointed at something you spawned it
    /// launches that; pointed at the ground near you it launches <em>you</em> the other way, like a
    /// rocket jump (private rooms only, same rule as the movement mods).
    /// </summary>
    public sealed class ImpulseMode : GunMode
    {
        private readonly ModContext ctx;
        private readonly AssetSpawner spawner;
        public ImpulseMode(ModContext ctx, AssetSpawner spawner) { this.ctx = ctx; this.spawner = spawner; }

        public override string Name => "Impulse";
        public override string Hint => "Hold to charge, let go to shove. Fire at your feet to launch yourself.";
        public override float ChargeTime => 0.8f;
        public override float Cooldown => 0.4f;

        private bool Allowed => ctx.Allowed == null || ctx.Allowed();

        public override Color? Tint(GunHit hit) =>
            !hit.HasHit ? new Color(0.55f, 0.55f, 0.6f) : (Color?)new Color(1f, 0.65f, 0.25f);

        public override string Describe(GunHit hit)
        {
            if (!hit.HasHit) return "point at something";
            if (Target(hit) != null) return "shove it";
            return hit.Distance < 6f ? (Allowed ? "launch yourself" : "paused (public lobby)") : "too far to launch from";
        }

        public override string Fire(GunHit hit)
        {
            if (!hit.HasHit) return "Nothing there.";
            float power = 6f + 16f * Mathf.Max(hit.Charge, 0.15f);

            var target = Target(hit);
            if (target != null)
            {
                var rb = target.GetComponent<Rigidbody>();
                if (rb == null) rb = target.AddComponent<Rigidbody>();
                rb.AddForceAtPosition(hit.Ray.direction * power * rb.mass, hit.Point, ForceMode.Impulse);
                return $"Shoved {target.name}";
            }

            if (!Allowed) return "Launching yourself is off in public lobbies.";
            if (hit.Distance > 6f) return "Fire closer to yourself to get launched.";
            var body = ctx.Player?.Body;
            if (body == null) return "Your player isn't loaded yet.";
            var away = (body.position - hit.Point).normalized + Vector3.up * 0.5f;
            body.velocity = away.normalized * power;
            return $"Launched  {power:0} m/s";
        }

        private GameObject Target(GunHit hit) =>
            hit.Collider != null ? spawner.SpawnedRootOf(hit.Collider.transform) : null;
    }

    /// <summary>
    /// Paint: hold the trigger and spray your theme colours over the world. The splats are stickers on
    /// your screen only - nobody else sees them, nothing is changed, and the oldest fade out as you go.
    /// </summary>
    public sealed class PaintMode : GunMode
    {
        private const int Capacity = 48;
        private readonly Transform[] splats = new Transform[Capacity];
        private readonly Func<MenuTheme> theme;
        private Transform root;
        private Material material;
        private MaterialPropertyBlock block;
        private int next;
        private float hue;

        public PaintMode(Func<MenuTheme> theme) => this.theme = theme;

        public override string Name => "Paint";
        public override string Hint => "Sprays your theme colours on the world. Only you see it, and it washes off on eject.";
        public override bool Automatic => true;
        public override float Cooldown => 0.06f;

        public override Color? Tint(GunHit hit) => Next();

        public override string Describe(GunHit hit) => hit.HasHit ? "spray" : "point at a surface";

        public override string Fire(GunHit hit)
        {
            if (!hit.HasHit) return null;
            if (root == null) Build();

            int index = next = (next + 1) % Capacity;
            var splat = splats[index];
            splat.gameObject.SetActive(true);
            splat.position = hit.Point + hit.Normal * 0.012f;
            splat.rotation = Quaternion.LookRotation(-hit.Normal, Vector3.up) * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            splat.localScale = Vector3.one * UnityEngine.Random.Range(0.18f, 0.42f);

            var renderer = splat.GetComponent<Renderer>();
            renderer.GetPropertyBlock(block);
            var color = Next();
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
            hue += 0.07f;
            return null;
        }

        private Color Next()
        {
            var t = theme?.Invoke();
            var a = t != null ? t.Accent : Color.cyan;
            var b = t != null ? t.Accent2 : Color.magenta;
            return Color.Lerp(a, b, Mathf.PingPong(hue, 1f));
        }

        private void Build()
        {
            root = new GameObject("[BundleMenu] Paint").transform;
            Object.DontDestroyOnLoad(root.gameObject);
            block = new MaterialPropertyBlock();

            for (int i = 0; i < Capacity; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Splat";
                var collider = quad.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);
                var renderer = quad.GetComponent<Renderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (material == null)
                {
                    material = new Material(renderer.sharedMaterial) { name = "paint (menu)" };
                }
                renderer.sharedMaterial = material;
                quad.transform.SetParent(root, false);
                quad.SetActive(false);
                splats[i] = quad.transform;
            }
        }

        /// <summary>Wipes everything painted so far.</summary>
        public void Clear()
        {
            for (int i = 0; i < Capacity; i++)
                if (splats[i] != null) splats[i].gameObject.SetActive(false);
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root.gameObject);
            if (material != null) Object.Destroy(material);
            root = null;
            material = null;
        }
    }
}
