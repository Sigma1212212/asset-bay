using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BundleMenu
{
    public enum Icon { ChevronLeft, ChevronRight, Close, Back, Gear, Home, Menu, Dot, Refresh, Check, Power }

    /// <summary>
    /// Every sprite the menu uses, generated at runtime from signed-distance functions.
    /// No texture assets means the whole system is copy-one-folder portable, and it keeps
    /// working when compiled into a standalone DLL that is injected into a game.
    /// </summary>
    public static class UISprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>9-sliceable filled rounded rectangle.</summary>
        public static Sprite RoundedFill(float radius) =>
            Get($"fill{radius:0.#}", () => BuildRounded(radius, 0f, 0f));

        /// <summary>9-sliceable rounded outline (a ring following the rounded rect edge).</summary>
        public static Sprite RoundedEdge(float radius, float width) =>
            Get($"edge{radius:0.#}_{width:0.#}", () => BuildRounded(radius, Mathf.Max(0.5f, width), 0f));

        /// <summary>9-sliceable soft glow. Place it `soft` px outside the panel on every side.</summary>
        public static Sprite RoundedGlow(float radius, float soft) =>
            Get($"glow{radius:0.#}_{soft:0.#}", () => BuildRounded(radius, 0f, soft));

        public static Sprite Glyph(Icon icon) => Get("icon" + icon, () => BuildIcon(icon));

        /// <summary>Destroys every generated sprite and texture (called on eject; they're rebuilt on demand).</summary>
        public static void ReleaseAll()
        {
            foreach (var s in Cache.Values)
            {
                if (s == null) continue;
                if (s.texture != null) Object.Destroy(s.texture);
                Object.Destroy(s);
            }
            Cache.Clear();
        }

        private static Sprite Get(string key, Func<Sprite> build)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            s = build();
            s.name = "BundleMenu_" + key;
            Cache[key] = s;
            return s;
        }

        /// <summary>Tileable white surface texture (tint it with the Image colour).</summary>
        public static Sprite Pattern(PanelPattern kind) => Get("pattern" + kind, () => BuildPattern(kind));

        private static Sprite BuildPattern(PanelPattern kind)
        {
            const int n = 128;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float a = 0f;
                switch (kind)
                {
                    case PanelPattern.Wood:
                    {
                        // Long horizontal grain: bands warped by a slow wobble, plus a knot.
                        float warp = 3f * Mathf.Sin(x * 2f * Mathf.PI / n) + 1.5f * Mathf.Sin(x * 6f * Mathf.PI / n + y * 0.2f);
                        float band = Mathf.Sin((y + warp) * 2f * Mathf.PI / 9f);
                        float fine = Mathf.Sin((y + warp * 1.7f) * 2f * Mathf.PI / 3.2f);
                        var k = new Vector2(x - 90f, (y - 40f) * 2.2f);
                        float knot = Mathf.Clamp01(1f - k.magnitude / 22f) * (0.5f + 0.5f * Mathf.Sin(k.magnitude * 0.9f));
                        a = 0.35f * Mathf.Clamp01(band * 0.5f + 0.5f) + 0.15f * Mathf.Clamp01(fine) + 0.5f * knot;
                        break;
                    }
                    case PanelPattern.Scanlines:
                        a = (y % 4) < 2 ? 0.55f : 0f;
                        break;
                    case PanelPattern.Grid:
                        a = (x % 16 == 0 || y % 16 == 0) ? 0.6f : ((x % 16 == 8 && y % 16 == 8) ? 0.4f : 0f);
                        break;
                    case PanelPattern.Paper:
                    {
                        uint h = (uint)(x * 374761393 + y * 668265263);
                        h = (h ^ (h >> 13)) * 1274126177u;
                        float noise = (h & 0xFFFF) / 65535f;
                        float fibre = Mathf.Abs(Mathf.Sin(x * 0.37f + y * 0.11f + noise * 2f));
                        a = 0.18f * noise + 0.12f * (fibre > 0.97f ? 1f : 0f);
                        break;
                    }
                }
                px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
            var sprite = MakeSprite(px, n, Vector4.zero);
            sprite.texture.wrapMode = TextureWrapMode.Repeat;
            return sprite;
        }

        // ------------------------------------------------------------------ rounded rects

        private static Sprite BuildRounded(float radius, float ringWidth, float soft)
        {
            radius = Mathf.Max(0f, radius);
            int pad = Mathf.CeilToInt(soft);
            int core = Mathf.CeilToInt(radius) + 2;
            int size = (core + pad) * 2 + 2;
            var pixels = new Color32[size * size];

            float half = size * 0.5f;
            float boxHalf = half - pad - 1f; // rounded rect half-extent, leaving a 1px AA margin

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                float d = RoundedBox(p, new Vector2(boxHalf, boxHalf), radius);
                float a;
                if (soft > 0f)
                {
                    float outside = Mathf.Clamp01(d / soft);
                    a = d <= 0f ? 1f : (1f - outside) * (1f - outside);
                }
                else if (ringWidth > 0f)
                {
                    a = Coverage(d) - Coverage(d + ringWidth);
                }
                else
                {
                    a = Coverage(d);
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }

            int border = core + pad;
            return MakeSprite(pixels, size, new Vector4(border, border, border, border));
        }

        private static float RoundedBox(Vector2 p, Vector2 half, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x) - (half.x - r), Mathf.Abs(p.y) - (half.y - r));
            var outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        private static float Coverage(float sdf) => Mathf.Clamp01(0.5f - sdf);

        // ------------------------------------------------------------------ icons

        private const int IconSize = 64;

        private static Sprite BuildIcon(Icon icon)
        {
            var pixels = new Color32[IconSize * IconSize];
            for (int y = 0; y < IconSize; y++)
            for (int x = 0; x < IconSize; x++)
            {
                // Work in top-down coordinates (like a drawing app), flip on write.
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = IconDistance(icon, p);
                byte a = (byte)(Coverage(d) * 255f);
                pixels[(IconSize - 1 - y) * IconSize + x] = new Color32(255, 255, 255, a);
            }
            return MakeSprite(pixels, IconSize, Vector4.zero);
        }

        private static float IconDistance(Icon icon, Vector2 p)
        {
            const float w = 3.2f; // half stroke width
            switch (icon)
            {
                case Icon.ChevronLeft:
                    return Poly(p, w, new Vector2(38, 14), new Vector2(24, 32), new Vector2(38, 50));
                case Icon.ChevronRight:
                    return Poly(p, w, new Vector2(26, 14), new Vector2(40, 32), new Vector2(26, 50));
                case Icon.Close:
                    return Mathf.Min(Seg(p, new Vector2(19, 19), new Vector2(45, 45)) - w,
                                     Seg(p, new Vector2(45, 19), new Vector2(19, 45)) - w);
                case Icon.Back:
                    return Mathf.Min(Seg(p, new Vector2(47, 32), new Vector2(18, 32)) - w,
                                     Poly(p, w, new Vector2(30, 19), new Vector2(17, 32), new Vector2(30, 45)));
                case Icon.Menu:
                    return Mathf.Min(Seg(p, new Vector2(17, 20), new Vector2(47, 20)),
                           Mathf.Min(Seg(p, new Vector2(17, 32), new Vector2(47, 32)),
                                     Seg(p, new Vector2(17, 44), new Vector2(47, 44)))) - w;
                case Icon.Dot:
                    return (p - new Vector2(32, 32)).magnitude - 28f;
                case Icon.Home:
                {
                    float roof = Poly(p, w, new Vector2(12, 32), new Vector2(32, 13), new Vector2(52, 32));
                    float body = RoundedBox(p - new Vector2(32, 42), new Vector2(12, 10), 2f);
                    float door = RoundedBox(p - new Vector2(32, 47), new Vector2(4, 7), 1f);
                    return Mathf.Min(roof, Mathf.Max(body, -door));
                }
                case Icon.Gear:
                {
                    var c = p - new Vector2(32, 32);
                    float len = c.magnitude;
                    float ang = Mathf.Atan2(c.y, c.x);
                    float tooth = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.25f, 0.35f, Mathf.Cos(ang * 8f)));
                    float outer = len - Mathf.Lerp(17f, 24f, tooth);
                    return Mathf.Max(outer, 8.5f - len);
                }
                case Icon.Refresh:
                {
                    var c = p - new Vector2(32, 32);
                    float ring = Mathf.Abs(c.magnitude - 16f) - w;
                    float ang = Mathf.Atan2(-c.y, c.x) * Mathf.Rad2Deg; // top-down flip
                    float gap = (ang > 20f && ang < 75f) ? 99f : 0f;       // open wedge at top-right
                    float head = Poly(p, w, new Vector2(42, 10), new Vector2(46, 20), new Vector2(35, 22));
                    return Mathf.Min(Mathf.Max(ring, gap - 1f), head);
                }
                case Icon.Check:
                    return Poly(p, w * 1.25f, new Vector2(17, 34), new Vector2(28, 45), new Vector2(47, 21));
                case Icon.Power:
                {
                    var c = p - new Vector2(32, 34);
                    float ring = Mathf.Abs(c.magnitude - 16f) - w;
                    float ang = Mathf.Atan2(-c.y, c.x) * Mathf.Rad2Deg;
                    float gap = (ang > 55f && ang < 125f) ? 99f : 0f; // open at the top
                    return Mathf.Min(Mathf.Max(ring, gap - 1f), Seg(p, new Vector2(32, 10), new Vector2(32, 32)) - w);
                }
                default:
                    return 99f;
            }
        }

        private static float Seg(Vector2 p, Vector2 a, Vector2 b)
        {
            var pa = p - a;
            var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        private static float Poly(Vector2 p, float halfWidth, params Vector2[] pts)
        {
            float d = float.MaxValue;
            for (int i = 0; i < pts.Length - 1; i++) d = Mathf.Min(d, Seg(p, pts[i], pts[i + 1]));
            return d - halfWidth;
        }

        private static Sprite MakeSprite(Color32[] pixels, int size, Vector4 border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true); // make non-readable to free the CPU copy
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }

    /// <summary>Two-colour gradient over any uGUI graphic's vertices (works with sliced Images).</summary>
    [DisallowMultipleComponent]
    public sealed class UIGradient : BaseMeshEffect
    {
        public Color From = Color.white;   // top (vertical) or left (horizontal)
        public Color To = Color.white;
        public bool Horizontal;

        private static readonly List<UIVertex> Verts = new List<UIVertex>();

        public void Set(Color from, Color to, bool horizontal = false)
        {
            From = from;
            To = to;
            Horizontal = horizontal;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;

            Verts.Clear();
            vh.GetUIVertexStream(Verts);

            float min = float.MaxValue, max = float.MinValue;
            foreach (var v in Verts)
            {
                float c = Horizontal ? v.position.x : v.position.y;
                min = Mathf.Min(min, c);
                max = Mathf.Max(max, c);
            }
            float range = Mathf.Max(0.0001f, max - min);

            for (int i = 0; i < Verts.Count; i++)
            {
                var v = Verts[i];
                float t = ((Horizontal ? v.position.x : v.position.y) - min) / range;
                // vertical: t=1 is the top, so From is at the top
                var c = Horizontal ? Color.Lerp(From, To, t) : Color.Lerp(To, From, t);
                v.color = (Color)v.color * c;
                Verts[i] = v;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(Verts);
        }
    }
}
