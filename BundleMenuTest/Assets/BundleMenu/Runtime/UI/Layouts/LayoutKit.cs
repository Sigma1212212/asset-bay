using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>Building blocks shared by every <see cref="MenuLayout"/>. Top-left coordinates, y down.</summary>
    internal sealed class LayoutKit
    {
        public readonly MenuView View;
        public readonly MenuTheme T;
        public RectTransform Panel => View.Panel;

        public LayoutKit(MenuView view, MenuTheme theme) { View = view; T = theme; }

        // ------------------------------------------------------------------ frame

        public RectTransform BeginPanel(float width, float height)
        {
            var panel = UIFactory.Rect("Panel", View.transform);
            height += T.Depth > 0f ? T.Depth + 10f : 0f; // room for the edge and shadow under the body
            panel.sizeDelta = new Vector2(width, height);
            View.Panel = panel;
            View.PanelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            View.PanelWidth = width;
            View.Height = height;
            return panel;
        }

        /// <summary>A rect placed from the parent's top-left corner.</summary>
        public static RectTransform At(RectTransform rt, float x, float y, float w, float h)
        {
            // Centre pivot, so buttons grow / squish around their middle rather than a corner.
            rt.Pin(new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(x + w * 0.5f, -y - h * 0.5f), new Vector2(w, h));
            return rt;
        }

        public RectTransform Box(Transform parent, string name, float x, float y, float w, float h) =>
            At(UIFactory.Rect(name, parent), x, y, w, h);

        /// <summary>
        /// Themed body. Outside-in: shadow on the "floor", the thick edge (the panel's side, seen from a
        /// little above), a real 3D slab when in the world, then fill, surface pattern and rim.
        /// Only neon-style themes (Flicker) get a coloured glow; everything else casts a plain shadow.
        /// </summary>
        public RectTransform Shell(Transform parent, string name, float x, float y, float w, float h,
                                   float radius = -1f, bool glow = true, bool pattern = true, float rim = -1f)
        {
            float r = radius < 0f ? T.PanelRadius : radius;
            var body = Box(parent, name, x, y, w, h);
            bool raised = glow && T.Depth > 0f; // pages inside a book pass glow:false - they're not separate objects

            if (raised)
            {
                var shadow = UIFactory.Image(body, "Shadow", UISprites.RoundedGlow(r, 18f), new Color(0f, 0f, 0f, 0.35f * PanelAlpha));
                shadow.rectTransform.Stretch(-10f, -T.Depth + 2f, -10f, -T.Depth - 14f);
                var side = UIFactory.Image(body, "Edge", UISprites.RoundedFill(r), SideColor);
                side.rectTransform.Stretch(0f, T.Depth, 0f, -T.Depth);
                if (PanelAlpha > 0.5f) PanelSlab.Add(body, SideColor, r, T.Depth * 1.5f);
            }

            const float soft = 36f;
            if (glow && T.GlowStrength > 0f && T.Flicker > 0f)
            {
                var g = UIFactory.Image(body, "Glow", UISprites.RoundedGlow(r, soft), Color.white);
                g.rectTransform.Stretch(-soft, -soft, -soft, -soft);
                g.gameObject.AddComponent<UIGradient>().Set(T.EdgeTop.WithAlpha(T.GlowStrength), T.EdgeBottom.WithAlpha(T.GlowStrength));
            }

            var bg = UIFactory.Image(body, "Background", UISprites.RoundedFill(r), Color.white, raycast: true);
            bg.rectTransform.Stretch();
            bg.gameObject.AddComponent<UIGradient>().Set(T.PanelTop, T.PanelBottom);
            if (raised && PanelAlpha > 0.5f) Bevel.Face(bg.transform, T, r, 0.4f); // the panel is a lit slab too

            if (pattern && T.Pattern != PanelPattern.None)
            {
                var p = UIFactory.Image(body, "Pattern", UISprites.Pattern(T.Pattern), T.PatternColor);
                p.type = Image.Type.Tiled;
                p.rectTransform.Stretch(r * 0.3f, r * 0.3f, r * 0.3f, r * 0.3f);
            }

            Rim(body, r, rim < 0f ? T.EdgeWidth : rim);
            return body;
        }

        private float PanelAlpha => Mathf.Max(T.PanelTop.a, T.PanelBottom.a);

        /// <summary>The panel's side: its bottom colour pushed toward the rim colour and darkened.</summary>
        public Color SideColor
        {
            get
            {
                var c = RowView.Darken(Color.Lerp(T.PanelBottom, T.EdgeBottom, 0.35f), 0.6f);
                c.a = PanelAlpha;
                return c;
            }
        }

        /// <summary>The base a button stands on (it sinks onto this when pressed).</summary>
        public static Color LipColor(MenuTheme t)
        {
            var c = RowView.Darken(t.ButtonFillPressed, 0.55f);
            c.a = Mathf.Clamp01(t.ButtonFill.a * 1.2f);
            return c;
        }

        /// <summary>Gives a placed button its base and press travel, and lifts it toward the viewer in 3D.</summary>
        public MenuButton Raise(MenuButton b, float radius)
        {
            float d = T.ButtonDepth;
            if (d <= 0f) return b;
            var rt = (RectTransform)b.transform;
            // A holder exactly where the key is; the base and its shadow hang below it inside.
            var holder = UIFactory.Rect(b.name + " Base", rt.parent);
            holder.anchorMin = rt.anchorMin; holder.anchorMax = rt.anchorMax; holder.pivot = rt.pivot;
            holder.sizeDelta = rt.sizeDelta;
            holder.anchoredPosition = rt.anchoredPosition;
            holder.SetSiblingIndex(rt.GetSiblingIndex());
            Bevel.Base(holder, T, radius, d);
            Bevel.Face(rt, T, radius);
            Bevel.Block(rt, T, radius, d);
            b.Base = holder.gameObject;
            b.PressDepth = d;
            Lift(rt, d);
            return b;
        }

        /// <summary>Move a piece toward the viewer. Only visible in the world, where it gives real parallax.</summary>
        public static void Lift(RectTransform rt, float units)
        {
            var p = rt.localPosition;
            rt.localPosition = new Vector3(p.x, p.y, -units);
        }

        public Image Rim(Transform body, float radius, float width)
        {
            if (width <= 0f) return null;
            var rim = UIFactory.Image(body, "Rim", UISprites.RoundedEdge(radius, width), Color.white);
            rim.rectTransform.Stretch();
            rim.gameObject.AddComponent<UIGradient>().Set(T.EdgeTop, T.EdgeBottom);
            if (T.Flicker > 0f) rim.gameObject.AddComponent<NeonFlicker>().Strength = T.Flicker;
            return rim;
        }

        /// <summary>Plain solid shape (decorations: beams, ropes, spines).</summary>
        public Image Solid(Transform parent, string name, float x, float y, float w, float h, Color c, float radius = 0f)
        {
            var img = UIFactory.Image(parent, name, UISprites.RoundedFill(radius), c);
            At(img.rectTransform, x, y, w, h);
            return img;
        }

        // ------------------------------------------------------------------ text

        public TextMeshProUGUI Text(Transform parent, string name, float x, float y, float w, float h, float size,
                                    TextAlignmentOptions align, Color color, FontStyles style = FontStyles.Normal, float spacing = 0f)
        {
            var t = UIFactory.Text(parent, name, T, size, align, color, style, spacing);
            At(t.rectTransform, x, y, w, h);
            return t;
        }

        public TextMeshProUGUI Title(Transform parent, float x, float y, float w, float h, float size, TextAlignmentOptions align)
        {
            var t = Text(parent, "Title", x, y, w, h, size, align, T.Text, T.TitleStyle, T.TitleSpacing);
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = Mathf.Min(16f, size);
            View.title = t;
            return t;
        }

        public TextMeshProUGUI Brand(Transform parent, string text, float x, float y, float w, float h, TextAlignmentOptions align, float size = 13f)
        {
            var t = Text(parent, "Brand", x, y, w, h, size, align, T.SubText, FontStyles.UpperCase | FontStyles.Bold, 4f);
            t.text = text;
            View.brand = t;
            return t;
        }

        public TextMeshProUGUI PageLabel(Transform parent, float x, float y, float w, float h, float size = 16f)
        {
            var t = Text(parent, "Page", x, y, w, h, size, TextAlignmentOptions.Center, T.SubText, FontStyles.Bold, 3f);
            View.pageLabel = t;
            return t;
        }

        public TextMeshProUGUI Status(Transform parent, float x, float y, float w, float h, float size = 14f)
        {
            var t = Text(parent, "Status", x, y, w, h, size, TextAlignmentOptions.Center, T.SubText);
            t.overflowMode = TextOverflowModes.Ellipsis;
            View.status = t;
            return t;
        }

        public RectTransform Accent(Transform parent, float x, float y, float w, float h)
        {
            var line = UIFactory.Image(parent, "AccentLine", UISprites.RoundedFill(h * 0.5f), Color.white).rectTransform;
            At(line, x, y, w, h);
            line.GetComponent<Image>().color = T.Accent;
            View.AccentLine = line;
            return line;
        }

        // ------------------------------------------------------------------ buttons

        /// <summary>Icon button of any size; the glyph stays square and centred.</summary>
        public MenuButton Icon(Transform parent, string name, Icon icon, float x, float y, float w, float h, float radius, float glyph = 0.5f)
        {
            var b = UIFactory.IconButton(parent, name, T, icon, Mathf.Min(w, h), radius);
            At((RectTransform)b.transform, x, y, w, h);
            float g = Mathf.Min(w, h) * glyph;
            var ic = b.transform.Find("Icon").GetComponent<RectTransform>();
            ic.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(g, g));
            if (w > h * 1.6f || h > w * 1.6f) b.HoverScale = 1.04f; // long bars shouldn't balloon
            return Raise(b, radius);
        }

        public MenuButton TextButton(Transform parent, string name, string label, float x, float y, float w, float h,
                                     float radius, float size, FontStyles style = FontStyles.Bold, float spacing = 1f)
        {
            var b = UIFactory.TextButton(parent, name, T, label, size, radius, style, spacing);
            At((RectTransform)b.transform, x, y, w, h);
            b.HoverScale = w > 150f ? 1.03f : 1.07f;
            return Raise(b, radius);
        }

        /// <summary>The standard five: builds any the layout left out, parked invisibly so the controller can wire them.</summary>
        public void EnsureButtons()
        {
            if (View.BackButton == null) View.BackButton = Hidden("Back");
            if (View.SettingsButton == null) View.SettingsButton = Hidden("Settings");
            if (View.CloseButton == null) View.CloseButton = Hidden("Close");
            if (View.PrevButton == null) View.PrevButton = Hidden("Prev");
            if (View.NextButton == null) View.NextButton = Hidden("Next");
        }

        private MenuButton Hidden(string name)
        {
            var b = UIFactory.IconButton(Panel, name, T, BundleMenu.Icon.Dot, 1f, 0f);
            b.gameObject.SetActive(false);
            return b;
        }

        // ------------------------------------------------------------------ rows

        /// <summary>Rows in 1 or more columns. Returns the container.</summary>
        public RectTransform Rows(Transform parent, float x, float y, float w, int count, int columns, RowLook look,
                                  float rowHeight, float spacing)
        {
            int lines = Mathf.CeilToInt(count / (float)columns);
            float h = lines * rowHeight + Mathf.Max(0, lines - 1) * spacing;
            var list = Box(parent, "Rows", x, y, w, h);
            Lift(list, T.ButtonDepth);
            if (columns > 1)
            {
                var grid = list.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2((w - spacing * (columns - 1)) / columns, rowHeight);
                grid.spacing = new Vector2(spacing, spacing);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = columns;
                grid.childAlignment = TextAnchor.UpperCenter;
            }
            else
            {
                var v = list.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = spacing;
                v.childAlignment = TextAnchor.UpperCenter;
                v.childControlHeight = v.childControlWidth = true;
                v.childForceExpandHeight = false;
                v.childForceExpandWidth = true;
            }
            for (int i = 0; i < count; i++)
                View.rows.Add(RowView.Create(list, T, rowHeight, View.rows.Count, look, compact: columns > 1));
            return list;
        }
    }
}
