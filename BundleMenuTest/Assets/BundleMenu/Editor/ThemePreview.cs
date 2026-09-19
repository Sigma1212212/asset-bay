using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BundleMenu.Editor
{
    /// <summary>Renders the real menu panel in every theme to PNGs (previews/theme_*.png).</summary>
    public static class ThemePreview
    {
        [MenuItem("Tools/Bundle Menu/Render Theme Previews", priority = 41)]
        public static void RenderAll()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(TabletPreview.OutputFolder);

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.17f);
            cam.orthographic = true;

            // Every colour theme in the menu type it pairs with...
            foreach (ThemePreset preset in Enum.GetValues(typeof(ThemePreset)))
            {
                if (preset == ThemePreset.Custom) continue;
                var theme = ThemePresets.Create(preset);
                Render(cam, theme, theme.Style, $"{TabletPreview.OutputFolder}/theme_{preset}.png");
            }
            // A few in the world at an angle, to check thickness and layering in 3D.
            foreach (var preset in new[] { ThemePreset.Timber, ThemePreset.Arcade, ThemePreset.Parchment, ThemePreset.Halo })
            {
                var theme = ThemePresets.Create(preset);
                RenderAngled(theme, theme.Style, $"{TabletPreview.OutputFolder}/angle_{preset}.png");
            }
            // ...and every menu type in one neutral theme, so shapes can be compared directly.
            foreach (MenuStyle style in Enum.GetValues(typeof(MenuStyle)))
                Render(cam, ThemePresets.Create(ThemePreset.Halo), style, $"{TabletPreview.OutputFolder}/style_{style}.png");

            Debug.Log("THEMEPREVIEW: done");
        }

        private static void RenderAngled(MenuTheme theme, MenuStyle style, string path)
        {
            var root = new GameObject("Angled");
            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(root.transform);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(35f, -30f, 0f);

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(root.transform);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.transform.localScale = Vector3.one * 0.001f;
            var host = UIFactory.Rect("Host", canvasGo.transform);
            var view = host.gameObject.AddComponent<MenuView>();
            view.Build(theme, style, 6, "Asset Bay  ·  v1.9.0");
            host.sizeDelta = new Vector2(view.PanelWidth, view.Height);
            ((RectTransform)canvasGo.transform).sizeDelta = host.sizeDelta;
            view.SetTitle("Library");
            view.SetBackVisible(true);
            view.SetPaging(0, 2);
            view.ShowRows(SampleRows());
            view.SetStatus("15 bundles from Dummy", theme.SubText);
            Canvas.ForceUpdateCanvases();
            foreach (var slab in canvasGo.GetComponentsInChildren<PanelSlab>()) slab.Refresh();

            var camGo = new GameObject("Cam");
            camGo.transform.SetParent(root.transform);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.17f);
            cam.fieldOfView = 40f;
            float size = Mathf.Max(view.PanelWidth, view.Height) * 0.001f;
            var target = Vector3.zero;
            camGo.transform.position = target + Quaternion.Euler(18f, 38f, 0f) * new Vector3(0f, 0f, -size * 1.9f);
            camGo.transform.LookAt(target);
            var rt = new RenderTexture(900, 900, 24) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();
            Save(rt, path);
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(root);
        }

        /// <summary>Render one theme to a file (used by the designed-theme check).</summary>
        public static void RenderOne(MenuTheme theme, MenuStyle style, string path)
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            System.IO.Directory.CreateDirectory(TabletPreview.OutputFolder);
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.17f);
            cam.orthographic = true;
            Render(cam, theme, style, path);
        }

        private static void Render(Camera cam, MenuTheme theme, MenuStyle style, string path)
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var host = UIFactory.Rect("Host", canvasGo.transform);
            var view = host.gameObject.AddComponent<MenuView>();
            view.Build(theme, style, 6, "Asset Bay  ·  v1.9.0");
            host.sizeDelta = new Vector2(view.PanelWidth, view.Height);
            view.SetTitle("Library");
            view.SetBackVisible(true);
            view.SetPaging(0, 2);
            view.ShowRows(SampleRows());
            view.SetStatus("15 bundles from Dummy", theme.SubText);

            var rt = new RenderTexture(Mathf.CeilToInt(view.PanelWidth) + 120, Mathf.CeilToInt(view.Height) + 120, 24) { antiAliasing = 8 };
            cam.targetTexture = rt;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            Canvas.ForceUpdateCanvases();
            cam.Render();
            Save(rt, path);
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(canvasGo);
        }

        private static List<RowSpec> SampleRows() => new List<RowSpec>
        {
            new RowSpec { Key = "1", Label = "Videos", Value = "3", ShowChevron = true, Light = StatusLight.Ok },
            new RowSpec { Key = "2", Label = "Fly", Value = "on", IsOn = true, OnClick = () => { } },
            new RowSpec { Key = "3", Label = "Gun", Value = "Place", ShowChevron = true, Light = StatusLight.Idle },
            new RowSpec { Key = "4", Label = "Props", Value = "1/3", ShowChevron = true, Light = StatusLight.Busy, Progress = 0.45f },
            new RowSpec { Key = "5", Label = "Effects", Value = "0/4", ShowChevron = true, Light = StatusLight.Error },
            new RowSpec { Key = "6", Label = "Platforms", OnClick = () => { } },
        };

        private static void Save(RenderTexture rt, string path)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
