using System;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>Slides between two points and carries whatever is standing on it.</summary>
    public sealed class Mover : MonoBehaviour
    {
        public Vector3 Travel = new Vector3(0f, 0f, 6f);
        public float Seconds = 4f;
        public Vector3 Velocity { get; private set; }

        private Vector3 start;
        private float t;

        private void Start() => start = transform.position;

        private void FixedUpdate()
        {
            t += Time.fixedDeltaTime / Mathf.Max(0.5f, Seconds);
            float wave = Mathf.SmoothStep(0f, 1f, Mathf.PingPong(t, 1f));
            var next = start + transform.TransformVector(Travel) * wave;
            Velocity = (next - transform.position) / Time.fixedDeltaTime;
            transform.position = next;
        }
    }

    /// <summary>
    /// Carries riders along with a moving platform. Unity's physics won't do it on its own: anything
    /// inside this zone is moved by exactly what the platform moved, so you can stand and walk normally.
    /// </summary>
    public sealed class Carrier : MonoBehaviour
    {
        public Mover Platform;

        private void OnTriggerStay(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null || Platform == null) return;
            rb.position += Platform.Velocity * Time.fixedDeltaTime;
        }
    }

    /// <summary>Blows everything inside it upward - a fan, or a column of air.</summary>
    public sealed class Updraft : MonoBehaviour
    {
        public float Lift = 18f;
        public Func<bool> Allowed;

        private void OnTriggerStay(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null || (Allowed != null && !Allowed())) return;
            rb.AddForce(transform.up * Lift, ForceMode.Acceleration);
            if (rb.velocity.y > 14f) rb.velocity = new Vector3(rb.velocity.x, 14f, rb.velocity.z);
        }
    }

    /// <summary>Drags whatever is on it along, like a conveyor belt or a speed strip.</summary>
    public sealed class Conveyor : MonoBehaviour
    {
        public float Speed = 5f;
        public Vector3 Direction = Vector3.forward;
        public Func<bool> Allowed;

        private void OnTriggerStay(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null || (Allowed != null && !Allowed())) return;
            rb.position += transform.TransformDirection(Direction).normalized * (Speed * Time.fixedDeltaTime);
        }
    }

    /// <summary>Saves your checkpoint the moment you touch it, and lights up when it has one.</summary>
    public sealed class CheckpointFlag : MonoBehaviour
    {
        public Action Touched;
        public Renderer Glow;
        public Color Lit = Color.green;
        private float coolUntil, litUntil;
        private MaterialPropertyBlock block;

        private void OnTriggerEnter(Collider other)
        {
            if (other.attachedRigidbody == null || Time.unscaledTime < coolUntil) return;
            coolUntil = Time.unscaledTime + 2f;
            litUntil = Time.unscaledTime + 1.2f;
            Touched?.Invoke();
        }

        private void Update()
        {
            if (Glow == null) return;
            bool on = Time.unscaledTime < litUntil;
            if (block == null) block = new MaterialPropertyBlock();
            Glow.GetPropertyBlock(block);
            var color = on ? Color.white : Lit;
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            Glow.SetPropertyBlock(block);
        }
    }

    /// <summary>Counts things that drop through it (a hoop, or a goal) and lights the score pips.</summary>
    public sealed class ScoreZone : MonoBehaviour
    {
        public TargetScore Score;
        public float Cooldown = 0.6f;
        private float readyAt;

        private void OnTriggerEnter(Collider other)
        {
            if (Score == null || Time.unscaledTime < readyAt) return;
            var rb = other.attachedRigidbody;
            if (rb == null || rb.velocity.y > 0.2f) return;    // must be coming down through it
            readyAt = Time.unscaledTime + Cooldown;
            Score.Add();
        }
    }

    /// <summary>Fires a burst of glowing sparks upward when it's touched, then reloads.</summary>
    public sealed class Firework : MonoBehaviour
    {
        public Color A = Color.cyan, B = Color.magenta;
        public Transform Muzzle;

        private const int Count = 24;
        private readonly Transform[] sparks = new Transform[Count];
        private readonly Vector3[] velocity = new Vector3[Count];
        private readonly float[] life = new float[Count];
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private Material material;
        private float readyAt;
        private int live;

        public void Launch()
        {
            if (Time.unscaledTime < readyAt) return;
            readyAt = Time.unscaledTime + 1.5f;
            if (renderers == null) Build();

            var from = Muzzle != null ? Muzzle.position : transform.position + Vector3.up;
            for (int i = 0; i < Count; i++)
            {
                sparks[i].gameObject.SetActive(true);
                sparks[i].position = from;
                sparks[i].localScale = Vector3.one * 0.07f;
                velocity[i] = (Vector3.up * 2.2f + UnityEngine.Random.insideUnitSphere).normalized * UnityEngine.Random.Range(5f, 9f);
                life[i] = 1f;
            }
            live = Count;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.attachedRigidbody != null) Launch();
        }

        private void Update()
        {
            if (live <= 0) return;
            float dt = Time.deltaTime;
            live = 0;
            for (int i = 0; i < Count; i++)
            {
                if (life[i] <= 0f) continue;
                life[i] -= dt * 0.7f;
                if (life[i] <= 0f) { sparks[i].gameObject.SetActive(false); continue; }
                live++;
                velocity[i] += Physics.gravity * dt * 0.6f;
                sparks[i].position += velocity[i] * dt;
                sparks[i].localScale = Vector3.one * (0.09f * life[i]);
                var color = Color.Lerp(B, A, life[i]);
                renderers[i].GetPropertyBlock(block);
                block.SetColor("_Color", color);
                block.SetColor("_BaseColor", color);
                renderers[i].SetPropertyBlock(block);
            }
        }

        private void Build()
        {
            block = new MaterialPropertyBlock();
            renderers = new Renderer[Count];
            for (int i = 0; i < Count; i++)
            {
                var spark = PropKit.Shape(transform, PrimitiveType.Cube, "Spark", Vector3.zero,
                    Vector3.one * 0.07f, A, collide: false, glow: 2f);
                spark.SetParent(null, true);                  // they fly off on their own
                if (material == null) material = spark.GetComponent<Renderer>().sharedMaterial;
                else spark.GetComponent<Renderer>().sharedMaterial = material;
                spark.gameObject.SetActive(false);
                sparks[i] = spark;
                renderers[i] = spark.GetComponent<Renderer>();
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < Count; i++) if (sparks[i] != null) Destroy(sparks[i].gameObject);
            if (material != null) Destroy(material);
        }
    }

    /// <summary>A chest that opens when you touch it and leaves a surprise behind.</summary>
    public sealed class SurpriseChest : MonoBehaviour
    {
        public Transform Lid;
        public Action<Vector3> Open;      // asks the menu to spawn something random here
        private bool opened;
        private float angle;

        private void OnTriggerEnter(Collider other)
        {
            if (opened || other.attachedRigidbody == null) return;
            opened = true;
            Open?.Invoke(transform.position + transform.forward * 1.2f);
        }

        private void Update()
        {
            if (!opened || Lid == null) return;
            angle = Mathf.MoveTowards(angle, -105f, Time.deltaTime * 220f);
            Lid.localRotation = Quaternion.Euler(angle, 0f, 0f);
            if (Mathf.Approximately(angle, -105f)) enabled = false;
        }
    }

    /// <summary>Runs a chase of lights along a sign.</summary>
    public sealed class SignChase : MonoBehaviour
    {
        public Renderer[] Bulbs;
        public Color A = Color.cyan, B = Color.magenta;
        public float Speed = 4f;
        private MaterialPropertyBlock block;

        private void Update()
        {
            if (Bulbs == null || Bulbs.Length == 0) { enabled = false; return; }
            if (block == null) block = new MaterialPropertyBlock();
            float t = Time.time * Speed;
            for (int i = 0; i < Bulbs.Length; i++)
            {
                if (Bulbs[i] == null) continue;
                float wave = 0.5f + 0.5f * Mathf.Sin(t - i * 0.6f);
                var color = Color.Lerp(A, B, wave);
                Bulbs[i].GetPropertyBlock(block);
                block.SetColor("_Color", color * (0.4f + 0.6f * wave));
                block.SetColor("_BaseColor", color);
                Bulbs[i].SetPropertyBlock(block);
            }
        }
    }
}
