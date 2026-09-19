using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BundleMenu.Editor
{
    /// <summary>Renders the nametag badge in a few traits and colours to previews/tags.png.</summary>
    public static class TagPreview
    {
        [MenuItem("Tools/Bundle Menu/Render Tag Preview", priority = 42)]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(TabletPreview.OutputFolder);

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
            cam.orthographic = true;
            var rt = new RenderTexture(520, 420, 24) { antiAliasing = 8 };
            cam.targetTexture = rt;

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            (string text, string colour)[] samples =
            {
                ("ADMIN", "#FF4D6A"), ("OWNER", "#FFD23F"), ("DEV", "#3BE8FF"), ("FRIEND", "#45E08A"), ("AFK", "#8A5CFF"),
            };
            for (int i = 0; i < samples.Length; i++)
            {
                var badge = NameTags.Create();
                badge.Root.transform.SetParent(canvasGo.transform, false);
                var canvasOnBadge = badge.Root.GetComponent<Canvas>();
                canvasOnBadge.overrideSorting = false;
                badge.Root.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 160 - i * 80);
                badge.Root.transform.localScale = Vector3.one;
                badge.Label.text = samples[i].text;
                var colour = TagStore.Parse(samples[i].colour);
                badge.Label.color = colour;
                badge.Rim.color = colour;
            }

            Canvas.ForceUpdateCanvases();
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes($"{TabletPreview.OutputFolder}/tags.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            rt.Release();
            Debug.Log("TAGPREVIEW: done");
        }
    }
}
