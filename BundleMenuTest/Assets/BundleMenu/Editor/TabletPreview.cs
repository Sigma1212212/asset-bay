using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BundleMenu.Editor
{
    /// <summary>Renders every built-in tablet style to PNGs (front + angled) so designs can be reviewed.</summary>
    public static class TabletPreview
    {
        public const string OutputFolder = "../previews";

        [MenuItem("Tools/Bundle Menu/Render Tablet Previews", priority = 40)]
        public static void RenderAll()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(OutputFolder);

            var theme = ThemePresets.Create(ThemePreset.Halo);
            var screenTex = SampleScreen(640, 360, theme);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
            RenderSettings.ambientLight = new Color(0.35f, 0.37f, 0.45f);

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.12f);
            cam.fieldOfView = 30f;
            var rt = new RenderTexture(1000, 700, 24) { antiAliasing = 8 };
            cam.targetTexture = rt;

            foreach (var config in TabletConfig.BuiltIn())
            {
                var mats = new TabletFactory.Materials
                {
                    Body = Lit(TabletColors.Resolve(config.body, theme, Color.gray), 0.55f),
                    Bezel = Lit(TabletColors.Resolve(config.bezelColor, theme, Color.black), 0.8f),
                    Accent = Emissive(TabletColors.Resolve(config.accent, theme, Color.cyan)),
                    Trim = Lit(TabletColors.Resolve(config.trim, theme, Color.white), 0.4f),
                    Screen = new Material(Shader.Find("Unlit/Texture")) { mainTexture = screenTex },
                };
                var tablet = TabletFactory.Build(config, mats);

                float size = config.width + config.bezel * 2f;
                Shoot(cam, tablet, rt, $"{OutputFolder}/tablet_{config.name}_front.png", size, 0f, 0f);
                Shoot(cam, tablet, rt, $"{OutputFolder}/tablet_{config.name}_angle.png", size, -32f, 14f);
                Object.DestroyImmediate(tablet);
            }

            cam.targetTexture = null;
            rt.Release();
            Debug.Log("TABLETPREVIEW: rendered to " + Path.GetFullPath(OutputFolder));
        }

        private static void Shoot(Camera cam, GameObject tablet, RenderTexture rt, string path, float size, float yaw, float pitch)
        {
            tablet.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            float dist = size * 2.4f / (2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad)) * 0.75f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -dist), Quaternion.identity);
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static Material Lit(Color c, float smooth)
        {
            var m = new Material(Shader.Find("Standard")) { color = c };
            m.SetFloat("_Glossiness", smooth);
            return m;
        }

        private static Material Emissive(Color c)
        {
            var m = new Material(Shader.Find("Standard")) { color = c };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 1.6f);
            return m;
        }

        /// <summary>A stand-in "video frame": theme gradient, soft bars and a play symbol.</summary>
        private static Texture2D SampleScreen(int w, int h, MenuTheme theme)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                var c = Color.Lerp(theme.PanelBottom, Color.Lerp(theme.Accent, theme.Accent2, u), 0.55f * v + 0.2f);
                // play triangle in the middle
                float px2 = (u - 0.5f) * w / h, py = v - 0.5f;
                if (px2 > -0.07f && px2 < 0.09f && Mathf.Abs(py) < (0.09f - px2) * 0.62f) c = Color.white;
                // progress bar along the bottom
                if (v < 0.035f) c = u < 0.38f ? theme.Accent : new Color(1f, 1f, 1f, 1f) * 0.25f;
                px[y * w + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
