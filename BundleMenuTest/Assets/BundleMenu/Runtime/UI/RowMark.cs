using UnityEngine;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>
    /// The state square of Box and Switch rows.
    ///   Box:    outlined tick box on the left; fills with the accent and shows a tick when the row is on.
    ///   Switch: solid square on the right; red when off, green when on, a chevron when the row navigates.
    /// Colour and fill animate so toggling feels physical.
    /// </summary>
    public sealed class RowMark : MonoBehaviour
    {
        private MenuTheme theme;
        private RowLook look;
        private Image frame, fill, glyph;
        private float on, target;
        private Color fillColor;
        private bool navigates;

        public static RowMark Create(Transform body, MenuTheme t, RowLook look, float rowHeight)
        {
            float size = look == RowLook.Box ? rowHeight - 22f : rowHeight - 12f;
            float radius = Mathf.Min(t.ButtonRadius, look == RowLook.Box ? 5f : 10f);
            var root = UIFactory.Rect("Mark", body);
            if (look == RowLook.Box)
                root.Pin(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12f, 0), new Vector2(size, size));
            else
                root.Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6f, 0), new Vector2(size, size));

            var m = root.gameObject.AddComponent<RowMark>();
            m.theme = t;
            m.look = look;
            m.fill = UIFactory.Image(root, "Fill", UISprites.RoundedFill(radius), Color.white);
            m.fill.rectTransform.Stretch();
            m.frame = UIFactory.Image(root, "Frame", UISprites.RoundedEdge(radius, look == RowLook.Box ? 2.5f : 2f), Color.white);
            m.frame.rectTransform.Stretch();
            // Switch squares are chunky 3D keys; tick boxes get a softer lit face.
            if (look == RowLook.Switch && t.Bevel > 0f)
            {
                var baseImg = Bevel.Base(root, t, radius, 4f);
                baseImg.transform.SetAsFirstSibling();
                root.Find("Shadow")?.SetAsFirstSibling();
            }
            Bevel.Face(m.fill.transform, t, radius, look == RowLook.Switch ? 1f : 0.6f);
            m.glyph = UIFactory.Image(root, "Glyph", UISprites.Glyph(Icon.Check), Color.white);
            m.glyph.rectTransform.Stretch(size * 0.14f, size * 0.14f, size * 0.14f, size * 0.14f);
            m.glyph.preserveAspect = true;
            return m;
        }

        /// <summary>Returns whether the mark is shown for this row.</summary>
        public bool Apply(RowSpec s, bool hasSecondary)
        {
            // Info lines and rows with their own side button don't get a switch.
            bool show = s.Interactable && !(look == RowLook.Switch && hasSecondary);
            gameObject.SetActive(show);
            if (!show) return false;

            navigates = s.ShowChevron;
            target = s.IsOn ? 1f : 0f;
            glyph.sprite = UISprites.Glyph(look == RowLook.Switch && navigates ? Icon.ChevronRight : Icon.Check);
            if (!isActiveAndEnabled || Time.frameCount <= 1) on = target;
            Paint();
            return true;
        }

        private void Update()
        {
            if (Mathf.Approximately(on, target)) return;
            on = Mathf.MoveTowards(on, target, Time.unscaledDeltaTime * 7f);
            Paint();
        }

        private void Paint()
        {
            float e = Ease.OutBack(on, 1.8f);
            if (look == RowLook.Box)
            {
                frame.color = Color.Lerp(theme.SubText, theme.Accent, on);
                fillColor = theme.Accent;
                fill.color = fillColor.WithAlpha(on);
                fill.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, e);
                glyph.color = Contrast(theme.Accent).WithAlpha(on);
                glyph.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, e);
            }
            else
            {
                // Navigation rows: a neutral accent square with a chevron. Toggles: red / green.
                fillColor = navigates ? theme.Accent : Color.Lerp(theme.StatusError, theme.StatusOk, on);
                fill.color = fillColor;
                fill.rectTransform.localScale = Vector3.one;
                frame.color = RowView.Darken(fillColor, 0.6f);
                glyph.color = Contrast(fillColor).WithAlpha(navigates ? 1f : on);
                glyph.rectTransform.localScale = Vector3.one * (navigates ? 0.8f : Mathf.Lerp(0.3f, 1f, e));
            }
        }

        /// <summary>Black or white, whichever reads better on this colour.</summary>
        private static Color Contrast(Color c) =>
            c.r * 0.299f + c.g * 0.587f + c.b * 0.114f > 0.6f ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
    }
}
