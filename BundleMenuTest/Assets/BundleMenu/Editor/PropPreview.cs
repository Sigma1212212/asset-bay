using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BundleMenu.Editor
{
    /// <summary>
    /// Builds every prop in the library and photographs them, so the shapes can be checked without
    /// starting Gorilla Tag. Writes previews/props.png (all of them in a row) and one close-up each.
    /// </summary>
    public static class PropPreview
    {
        [MenuItem("Tools/Bundle Menu/Render Prop Previews", priority = 43)]
        public static void Render()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(TabletPreview.OutputFolder);

            var theme = ScriptableObject.CreateInstance<MenuTheme>();
            var build = new PropBuild { Theme = theme, WalkableLayer = -1, Allowed = () => true };

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(38f, -35f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 12f;
            ground.GetComponent<Renderer>().sharedMaterial.color = new Color(0.17f, 0.18f, 0.22f);

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.11f, 0.14f);
            cam.fieldOfView = 40f;

            int count = PropLibrary.All.Count;
            float spacing = 3.2f;
            for (int i = 0; i < count; i++)
            {
                var def = PropLibrary.All[i];
                var go = def.Build(build);
                if (go == null) { Debug.LogWarning("PROPPREVIEW: " + def.Name + " built nothing"); continue; }
                go.transform.position = new Vector3((i - (count - 1) * 0.5f) * spacing, 0f, 0f);
                go.transform.rotation = Quaternion.Euler(0f, 205f, 0f);

                // Frame each prop by its own size, so tall ones aren't cut off.
                var renderers = go.GetComponentsInChildren<Renderer>();
                var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(go.transform.position, Vector3.one);
                for (int r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
                Shoot(cam, bounds.center, Mathf.Max(2.5f, bounds.size.magnitude * 1.1f),
                    $"{TabletPreview.OutputFolder}/prop_{def.Name.Replace(' ', '_')}.png", 420, 420);
            }

            // One wide shot of the whole set.
            Shoot(cam, new Vector3(0f, 1.4f, 0f), count * spacing * 0.62f,
                $"{TabletPreview.OutputFolder}/props.png", 1800, 500);

            Object.DestroyImmediate(theme);
            Debug.Log("PROPPREVIEW: done, " + count + " props");
        }

        private static void Shoot(Camera cam, Vector3 target, float distance, string path, int width, int height)
        {
            cam.transform.position = target + new Vector3(distance * 0.25f, distance * 0.28f, -distance);
            cam.transform.LookAt(target);

            var rt = new RenderTexture(width, height, 24) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            rt.Release();
        }
    }
}
