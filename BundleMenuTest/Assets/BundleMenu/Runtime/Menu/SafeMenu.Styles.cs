using UnityEngine;

namespace BundleMenu
{
    /// <summary>Safe-mode look: flat charcoal, one teal accent, small text, square corners. Built from 1x1 textures.</summary>
    public sealed partial class SafeMenu
    {
        private static readonly Color cBg = new Color32(0x14, 0x16, 0x19, 0xF5);
        private static readonly Color cBox = new Color32(0x1C, 0x1F, 0x23, 0xFF);
        private static readonly Color cLine = new Color32(0x2C, 0x31, 0x37, 0xFF);
        private static readonly Color cButton = new Color32(0x25, 0x29, 0x2F, 0xFF);
        private static readonly Color cHover = new Color32(0x2F, 0x35, 0x3C, 0xFF);
        private static readonly Color cAccent = new Color32(0x2E, 0xC4, 0xB6, 0xFF);
        private static readonly Color cText = new Color32(0xD6, 0xDB, 0xE0, 0xFF);
        private static readonly Color cDim = new Color32(0x86, 0x8F, 0x99, 0xFF);

        private GUIStyle sWindow, sTitle, sDim, sDimWrap, sButton, sTab, sTabOn, sBox, sSection, sLabel, sValue,
                         sValueCentre, sCheck, sCheckOn, sField, sCode, sError;
        private Texture2D[] textures;

        private void EnsureStyles()
        {
            if (sWindow != null && textures != null && textures[0] != null) return;
            var tBg = Tex(cBg); var tBox = Tex(cBox); var tButton = Tex(cButton); var tHover = Tex(cHover);
            var tAccent = Tex(cAccent); var tLine = Tex(cLine); var tClear = Tex(Color.clear);
            textures = new[] { tBg, tBox, tButton, tHover, tAccent, tLine, tClear };

            sWindow = new GUIStyle { normal = { background = tBg }, onNormal = { background = tBg }, border = new RectOffset(1, 1, 1, 1) };
            sTitle = Text(13, cText, FontStyle.Bold);
            sDim = Text(11, cDim);
            sDimWrap = Text(11, cDim); sDimWrap.wordWrap = true;
            sLabel = Text(12, cDim);
            sValue = Text(12, cText);
            sValueCentre = Text(12, cText); sValueCentre.alignment = TextAnchor.MiddleCenter;
            sSection = Text(11, cAccent, FontStyle.Bold); sSection.margin = new RectOffset(0, 0, 0, 4);
            sError = Text(11, new Color(1f, 0.45f, 0.45f)); sError.wordWrap = true;
            sCode = Text(16, cText, FontStyle.Bold);

            sButton = new GUIStyle
            {
                normal = { background = tButton, textColor = cText },
                hover = { background = tHover, textColor = Color.white },
                active = { background = tAccent, textColor = Color.black },
                fontSize = 12, alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 4, 4), margin = new RectOffset(0, 4, 2, 2),
            };
            sTab = new GUIStyle(sButton) { normal = { background = tClear, textColor = cDim }, alignment = TextAnchor.MiddleLeft };
            sTabOn = new GUIStyle(sTab) { normal = { background = tBox, textColor = cText }, fontStyle = FontStyle.Bold };
            sBox = new GUIStyle { normal = { background = tBox }, padding = new RectOffset(10, 10, 8, 8) };
            sCheck = new GUIStyle { normal = { background = tLine }, hover = { background = tHover }, margin = new RectOffset(0, 6, 4, 2) };
            sCheckOn = new GUIStyle(sCheck) { normal = { background = tAccent }, hover = { background = tAccent } };
            sField = new GUIStyle
            {
                normal = { background = tButton, textColor = cText }, focused = { background = tHover, textColor = Color.white },
                fontSize = 14, padding = new RectOffset(8, 8, 5, 5), margin = new RectOffset(0, 4, 2, 2),
            };
        }

        private static GUIStyle Text(int size, Color color, FontStyle style = FontStyle.Normal) => new GUIStyle
        {
            fontSize = size, fontStyle = style, normal = { textColor = color },
            alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 2, 2), margin = new RectOffset(0, 0, 1, 1),
        };

        private static void Line(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void OnDestroy()
        {
            if (Visible) SetVisible(false);
            if (textures != null) foreach (var t in textures) if (t != null) Destroy(t);
        }
    }
}
