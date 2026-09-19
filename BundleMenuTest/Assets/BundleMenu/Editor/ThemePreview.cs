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
            var rt = new RenderTexture(620, 820, 24) { antiAliasing = 8 };
            cam.targetTexture = rt;

            foreach (ThemePreset preset in Enum.GetValues(typeof(ThemePreset)))
            {
                if (preset == ThemePreset.Custom) continue;
                var theme = ThemePresets.Create(preset);

                var canvasGo = new GameObject("Canvas", typeof(RectTransform));
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 5f;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

                var host = UIFactory.Rect("Host", canvasGo.transform);
                var view = host.gameObject.AddComponent<MenuView>();
                view.Build(theme, 6, "Asset Bay  ·  v1.8.0");
                host.sizeDelta = new Vector2(MenuView.Width, view.Height);
                view.SetTitle("Library");
                view.SetBackVisible(false);
                view.SetPaging(0, 2);
                view.ShowRows(SampleRows());
                view.SetStatus("15 bundles from Dummy", theme.SubText);

                Canvas.ForceUpdateCanvases();
                cam.Render();
                Save(rt, $"{TabletPreview.OutputFolder}/theme_{preset}.png");
                UnityEngine.Object.DestroyImmediate(canvasGo);
            }

            cam.targetTexture = null;
            rt.Release();
            Debug.Log("THEMEPREVIEW: done");
        }

        private static List<RowSpec> SampleRows() => new List<RowSpec>
        {
            new RowSpec { Key = "1", Label = "Videos", Value = "3", ShowChevron = true, Light = StatusLight.Ok },
            new RowSpec { Key = "2", Label = "Mods", Value = "2 on", ShowChevron = true, Light = StatusLight.Ok, IsOn = true },
            new RowSpec { Key = "3", Label = "Gun", Value = "Place", ShowChevron = true, Light = StatusLight.Idle },
            new RowSpec { Key = "4", Label = "Props", Value = "1/3", ShowChevron = true, Light = StatusLight.Busy, Progress = 0.45f },
            new RowSpec { Key = "5", Label = "Effects", Value = "0/4", ShowChevron = true, Light = StatusLight.Error },
            new RowSpec { Key = "6", Label = "Vehicles", Value = "0/2", ShowChevron = true, Light = StatusLight.Idle },
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
