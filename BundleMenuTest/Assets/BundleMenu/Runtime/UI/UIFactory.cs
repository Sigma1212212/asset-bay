using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>Small helpers so the view code reads like a layout, not like plumbing.</summary>
    public static class UIFactory
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 5; // 5 = UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchor to a point of the parent (0..1 each axis) and set size + offset from it.</summary>
        public static RectTransform Pin(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Full width strip at a fixed distance from the top.</summary>
        public static RectTransform TopStrip(this RectTransform rt, float top, float height, float left, float right)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, MenuTheme theme, float size,
            TextAlignmentOptions align, Color color, FontStyles style = FontStyles.Normal, float spacing = 0f)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = theme != null && theme.Font != null ? theme.Font : DefaultFont;
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.fontStyle = style;
            t.characterSpacing = spacing;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        private static TMP_FontAsset cachedFont;

        /// <summary>
        /// TMP's default font, or - when running injected into a game that has no TMP Settings asset -
        /// any font asset that happens to be loaded.
        /// </summary>
        public static TMP_FontAsset DefaultFont
        {
            get
            {
                if (cachedFont != null) return cachedFont;
                try { cachedFont = TMP_Settings.defaultFontAsset; } catch { /* no TMP settings in this build */ }
                if (cachedFont == null)
                    cachedFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
                        .OrderByDescending(f => f.name.ToLowerInvariant().Contains("liberation") ? 1 : 0)
                        .FirstOrDefault();
                if (cachedFont == null)
                    Debug.LogError("[BundleMenu] No TextMeshPro font found. In the Unity editor run " +
                                   "Window > TextMeshPro > Import TMP Essential Resources.");
                return cachedFont;
            }
        }

        /// <summary>Square themed icon button (header / footer controls).</summary>
        public static MenuButton IconButton(Transform parent, string name, MenuTheme theme, Icon icon, float size, float radius)
        {
            var fill = Image(parent, name, UISprites.RoundedFill(radius), theme.ButtonFill, raycast: true);
            var edge = Image(fill.transform, "Edge", UISprites.RoundedEdge(radius, theme.ButtonEdgeWidth), theme.ButtonEdge);
            edge.rectTransform.Stretch();

            var glyph = Image(fill.transform, "Icon", UISprites.Glyph(icon), theme.Text);
            glyph.rectTransform.Stretch(size * 0.2f, size * 0.2f, size * 0.2f, size * 0.2f);
            glyph.preserveAspect = true;

            fill.rectTransform.sizeDelta = new Vector2(size, size);
            var button = fill.gameObject.AddComponent<MenuButton>();
            button.HoverScale = 1.08f;
            button.PressScale = 0.9f;
            button.Init(theme, fill, edge);
            return button;
        }

        /// <summary>Themed button with a text label ("Close", "< PREV", "Return"...).</summary>
        public static MenuButton TextButton(Transform parent, string name, MenuTheme theme, string text, float size,
                                            float radius, FontStyles style = FontStyles.Bold, float spacing = 1f)
        {
            var fill = Image(parent, name, UISprites.RoundedFill(radius), theme.ButtonFill, raycast: true);
            var edge = Image(fill.transform, "Edge", UISprites.RoundedEdge(radius, theme.ButtonEdgeWidth), theme.ButtonEdge);
            edge.rectTransform.Stretch();

            var label = Text(fill.transform, "Label", theme, size, TextAlignmentOptions.Center, theme.Text, style, spacing);
            label.rectTransform.Stretch(6f, 0f, 6f, 0f);
            label.enableAutoSizing = true;
            label.fontSizeMax = size;
            label.fontSizeMin = Mathf.Min(10f, size);
            label.text = text;

            var button = fill.gameObject.AddComponent<MenuButton>();
            button.PressScale = 0.92f;
            button.Init(theme, fill, edge);
            return button;
        }

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
