using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>Bounces whatever lands on it straight back up. Yours only - nobody else is affected.</summary>
    public sealed class Bouncer : MonoBehaviour
    {
        public float Power = 12f;
        public Transform Squash;
        public System.Func<bool> Allowed;        // the mods' lobby rule
        private float squashTime;
        private Vector3 restScale;

        private void Start()
        {
            if (Squash != null) restScale = Squash.localScale;
        }

        private void OnCollisionEnter(Collision collision) => Launch(collision.rigidbody, Vector3.up);

        internal void Launch(Rigidbody rb, Vector3 direction)
        {
            if (rb == null || (Allowed != null && !Allowed())) return;
            var velocity = rb.velocity;
            velocity = Vector3.Reflect(Vector3.Project(velocity, direction), direction) + Vector3.ProjectOnPlane(velocity, direction);
            rb.velocity = velocity + direction * Power;
            squashTime = 0.25f;
        }

        private void Update()
        {
            if (Squash == null || squashTime <= 0f) return;
            squashTime -= Time.deltaTime;
            // Squashes flat on the hit and springs back to the size it was built at.
            float squash = Mathf.Clamp01(squashTime / 0.25f);
            Squash.localScale = new Vector3(restScale.x, restScale.y * (1f - 0.5f * squash), restScale.z);
        }
    }

    /// <summary>A trigger that shoves anything passing through it along the prop's forward direction.</summary>
    public sealed class Booster : MonoBehaviour
    {
        public float Power = 22f;
        public Vector3 Direction = Vector3.forward;
        public System.Func<bool> Allowed;        // the mods' lobby rule

        private void OnTriggerEnter(Collider other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null || (Allowed != null && !Allowed())) return;
            rb.velocity = transform.TransformDirection(Direction).normalized * Power;
        }
    }

    /// <summary>Turns at a steady rate. Used by disco balls and spinning signs.</summary>
    public sealed class Spinner : MonoBehaviour
    {
        public Vector3 Speed = new Vector3(0f, 45f, 0f);
        private void Update() => transform.Rotate(Speed * Time.deltaTime, Space.Self);
    }

    /// <summary>Makes a light wobble like a flame, and its glow bob with it.</summary>
    public sealed class Flicker : MonoBehaviour
    {
        public Light Target;
        public float Base = 1.6f, Amount = 0.5f;
        private float seed;

        private void Start() => seed = Random.value * 100f;

        private void Update()
        {
            if (Target == null) { enabled = false; return; }
            float t = Time.time * 7f + seed;
            Target.intensity = Base + Amount * (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f;
        }
    }

    /// <summary>
    /// A pair of rings: step in one and you come out of the other. Only you travel, and only where the
    /// mods are allowed (offline, private or modded rooms), same as the teleport gun.
    /// </summary>
    public sealed class PortalPad : MonoBehaviour
    {
        private static readonly List<PortalPad> waiting = new List<PortalPad>();
        public PortalPad Partner;
        public System.Func<bool> Allowed;
        public System.Func<Vector3, bool> Teleport;      // the menu's own teleport, when there is one
        private float coolUntil;

        private void Awake()
        {
            // The first portal waits; the next one spawned pairs up with it.
            waiting.RemoveAll(p => p == null);
            for (int i = 0; i < waiting.Count; i++)
                if (waiting[i] != null && waiting[i].Partner == null)
                {
                    Partner = waiting[i];
                    waiting[i].Partner = this;
                    waiting.RemoveAt(i);
                    return;
                }
            waiting.Add(this);
        }

        private void OnDestroy() => waiting.Remove(this);

        public bool Linked => Partner != null;

        private void OnTriggerEnter(Collider other)
        {
            if (Partner == null || Time.unscaledTime < coolUntil) return;
            if (Allowed != null && !Allowed()) return;
            var rb = other.attachedRigidbody;
            if (rb == null) return;

            coolUntil = Partner.coolUntil = Time.unscaledTime + 1f;   // stops an instant round trip
            var exit = Partner.transform.position + Vector3.up * 0.3f;
            if (Teleport == null || !Teleport(exit)) rb.position = exit;
            rb.velocity = Partner.transform.forward * Mathf.Max(3f, rb.velocity.magnitude);
        }
    }

    /// <summary>A tin can that counts as knocked over once it's been tipped or shoved.</summary>
    public sealed class TargetCan : MonoBehaviour
    {
        public TargetScore Score;

        private void Update()
        {
            if (Score == null || Vector3.Angle(transform.up, Vector3.up) < 45f) return;
            Score.Add();
            enabled = false;            // counted once, then it stops costing anything
        }
    }

    /// <summary>Keeps the score for a stack of cans and shows it on the sign.</summary>
    public sealed class TargetScore : MonoBehaviour
    {
        public Transform[] Pips;
        private int score;

        public void Add()
        {
            score++;
            for (int i = 0; i < Pips.Length; i++)
                if (Pips[i] != null) Pips[i].gameObject.SetActive(i < score);
        }
    }

    /// <summary>Drifts upward, sways, and pops when something touches it.</summary>
    public sealed class Balloon : MonoBehaviour
    {
        public float Rise = 0.6f;
        private float seed;
        private Rigidbody body;

        private void Start()
        {
            seed = Random.value * 10f;
            body = GetComponent<Rigidbody>();
            if (body != null) body.useGravity = false;
        }

        private void FixedUpdate()
        {
            float sway = Mathf.Sin(Time.time * 1.3f + seed) * 0.4f;
            if (body != null) body.velocity = new Vector3(sway, Rise, Mathf.Cos(Time.time * 0.9f + seed) * 0.3f);
            else transform.position += Vector3.up * Rise * Time.fixedDeltaTime;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.relativeVelocity.sqrMagnitude < 1f) return;
            Destroy(gameObject);
        }
    }

    /// <summary>Slowly fades in when spawned so props don't appear with a snap.</summary>
    public sealed class PropIntro : MonoBehaviour
    {
        private Vector3 target;
        private float t;

        private void Start()
        {
            target = transform.localScale;
            transform.localScale = target * 0.01f;
        }

        private void Update()
        {
            t += Time.unscaledDeltaTime / 0.4f;
            transform.localScale = target * Ease.OutBack(Mathf.Clamp01(t), 1.7f);
            if (t >= 1f) Destroy(this);
        }
    }
}
