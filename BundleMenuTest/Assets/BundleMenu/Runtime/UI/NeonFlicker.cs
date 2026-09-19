using UnityEngine;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>Makes a graphic flicker like a neon tube: a steady hum plus the occasional quick stutter.</summary>
    public sealed class NeonFlicker : MonoBehaviour
    {
        public float Strength = 0.5f;
        private Graphic graphic;
        private float baseAlpha, stutterUntil, nextStutter;

        private void Awake()
        {
            graphic = GetComponent<Graphic>();
            baseAlpha = graphic != null ? graphic.color.a : 1f;
            nextStutter = Time.unscaledTime + Random.Range(1f, 4f);
        }

        private void Update()
        {
            if (graphic == null) return;
            float t = Time.unscaledTime;
            float hum = 1f - Strength * 0.12f * (0.5f + 0.5f * Mathf.Sin(t * 47f));
            if (t >= nextStutter)
            {
                stutterUntil = t + Random.Range(0.08f, 0.22f);
                nextStutter = t + Random.Range(1.5f, 5f);
            }
            if (t < stutterUntil) hum *= Random.value < 0.5f ? 1f - Strength * 0.8f : 1f;
            var c = graphic.color;
            c.a = baseAlpha * hum;
            graphic.color = c;
        }
    }
}
