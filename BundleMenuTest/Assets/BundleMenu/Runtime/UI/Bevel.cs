using UnityEngine;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>
    /// Makes a flat button read as a physical key: light falls on the top half, the bottom half is in
    /// shade, a thin highlight catches the top edge, a darker base with a soft shadow sits underneath,
    /// and in the world a real extruded block gives it thickness you can see from the side.
    /// </summary>
    public static class Bevel
    {
        /// <summary>Lighting layers on the button face. Put them under the label (sibling index 0).</summary>
        public static void Face(Transform body, MenuTheme t, float radius, float strength = 1f)
        {
            float k = t.Bevel * strength;
            if (k <= 0f) return;

            var sheen = UIFactory.Image(body, "Sheen", UISprites.RoundedFill(radius), Color.white);
            sheen.rectTransform.anchorMin = new Vector2(0f, 0.4f);
            sheen.rectTransform.anchorMax = Vector2.one;
            sheen.rectTransform.offsetMin = new Vector2(1f, 0f);
            sheen.rectTransform.offsetMax = new Vector2(-1f, -1f);
            sheen.gameObject.AddComponent<UIGradient>().Set(new Color(1, 1, 1, 0.20f * k), new Color(1, 1, 1, 0f));
            sheen.transform.SetSiblingIndex(0);

            var shade = UIFactory.Image(body, "Shade", UISprites.RoundedFill(radius), Color.white);
            shade.rectTransform.anchorMin = Vector2.zero;
            shade.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            shade.rectTransform.offsetMin = new Vector2(1f, 1f);
            shade.rectTransform.offsetMax = new Vector2(-1f, 0f);
            shade.gameObject.AddComponent<UIGradient>().Set(new Color(0, 0, 0, 0f), new Color(0, 0, 0, 0.22f * k));
            shade.transform.SetSiblingIndex(1);

            var line = UIFactory.Image(body, "Highlight", UISprites.RoundedFill(1f), new Color(1, 1, 1, 0.32f * k));
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = Vector2.one;
            float inset = Mathf.Max(4f, radius * 0.7f);
            line.rectTransform.offsetMin = new Vector2(inset, -3.5f);
            line.rectTransform.offsetMax = new Vector2(-inset, -2f);
            line.transform.SetSiblingIndex(2);
        }

        /// <summary>
        /// The base a key stands on, built in <paramref name="parent"/> behind the key's rect:
        /// a soft floor shadow, then the base itself (darker toward the bottom). Returns the base.
        /// </summary>
        public static Image Base(Transform parent, MenuTheme t, float radius, float depth)
        {
            var shadow = UIFactory.Image(parent, "Shadow", UISprites.RoundedGlow(radius, 10f), new Color(0, 0, 0, 0.28f * Mathf.Max(0.3f, t.Bevel)));
            shadow.rectTransform.Stretch(-6f, depth - 2f, -6f, -depth - 10f);

            var lip = UIFactory.Image(parent, "Lip", UISprites.RoundedFill(radius), Color.white);
            lip.rectTransform.Stretch(0, depth, 0, -depth);
            var c = LayoutKit.LipColor(t);
            lip.gameObject.AddComponent<UIGradient>().Set(c, RowView.Darken(c, 0.7f).WithAlpha(c.a));
            return lip;
        }

        /// <summary>Real thickness behind the key when it's in the world (hidden on screen overlays).</summary>
        public static void Block(RectTransform body, MenuTheme t, float radius, float depth)
        {
            if (t.Bevel <= 0f || depth <= 0f || t.ButtonFill.a < 0.5f) return;
            PanelSlab.Add(body, LayoutKit.LipColor(t).WithAlpha(1f), radius, depth * 2.2f);
        }
    }
}
