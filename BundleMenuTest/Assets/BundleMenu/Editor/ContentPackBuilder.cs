using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace BundleMenu.Editor
{
    /// <summary>
    /// Builds the Asset Bay content pack (one AssetBundle named "content"):
    ///   tablet-*.json   tablet styles (edit these to change shape, parts, colours, behaviour)
    ///   theme-*.json    extra menu themes (start from Halo, override any field)
    ///   prefabs         spawnable props: Tablet Stand, Wall Screen, Billboard
    /// Output: ../build/bundles/content  (+ its SHA-256 for feed.json -> tablet.sha256)
    /// </summary>
    public static class ContentPackBuilder
    {
        private const string Folder = "Assets/ContentPack";
        private const string OutFolder = "../build/bundles";

        [MenuItem("Tools/Bundle Menu/Build Content Pack", priority = 42)]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory(Folder + "/Meshes");

            // ---- tablet styles
            var styles = new System.Collections.Generic.List<TabletConfig>(TabletConfig.BuiltIn())
            {
                new TabletConfig { name = "Aurora", width = 0.44f, bezel = 0.02f, cornerRadius = 0.04f, thickness = 0.016f,
                    body = "#10131F", accent = "theme:Accent", trim = "theme:Accent2", openAnimation = "slide", bob = 0.006f },
                new TabletConfig { name = "Walnut", width = 0.4f, aspect = 1.5f, bezel = 0.045f, cornerRadius = 0.02f, thickness = 0.035f,
                    body = "#6B4226", bezelColor = "#1E140C", trim = "#C9A26B", accent = "#F2C14E", kickstand = true, cameraDot = false },
            };
            foreach (var s in styles) WriteText($"tablet-{s.name.ToLowerInvariant()}", JsonUtility.ToJson(s, true));

            // ---- pack-only themes (any MenuTheme field can be overridden)
            var sunset = ThemePresets.Create(ThemePreset.Halo);
            sunset.DisplayName = "Sunset";
            sunset.PanelTop = MenuTheme.Hex(0x3A1238); sunset.PanelBottom = MenuTheme.Hex(0x14061A);
            sunset.EdgeTop = MenuTheme.Hex(0xFFB25B); sunset.EdgeBottom = MenuTheme.Hex(0xE8456B);
            sunset.Accent = MenuTheme.Hex(0xFFB25B); sunset.Accent2 = MenuTheme.Hex(0xE8456B);
            sunset.ButtonFill = MenuTheme.Hex(0x4A1A45, 0.9f); sunset.ButtonFillHover = MenuTheme.Hex(0x5E2358);
            sunset.ButtonEdgeHover = MenuTheme.Hex(0xFFB25B); sunset.Hover = HoverStyle.Slide;
            sunset.EntranceOverride = (int)EntranceStyle.Staggered;
            WriteText("theme-sunset", JsonUtility.ToJson(sunset, true));

            var ocean = ThemePresets.Create(ThemePreset.Halo);
            ocean.DisplayName = "Ocean";
            ocean.PanelTop = MenuTheme.Hex(0x06324A); ocean.PanelBottom = MenuTheme.Hex(0x021624);
            ocean.EdgeTop = MenuTheme.Hex(0x5FE3D0); ocean.EdgeBottom = MenuTheme.Hex(0x2E7BFF);
            ocean.Accent = MenuTheme.Hex(0x5FE3D0); ocean.Accent2 = MenuTheme.Hex(0x2E7BFF);
            ocean.ButtonFill = MenuTheme.Hex(0x0A3D5A, 0.9f); ocean.ButtonFillHover = MenuTheme.Hex(0x0F5073);
            ocean.Layout = ThemeLayout.Grid; ocean.RowHeight = 60f; ocean.LabelSize = 18f;
            ocean.EntranceOverride = (int)EntranceStyle.Cascade;
            WriteText("theme-ocean", JsonUtility.ToJson(ocean, true));

            // ---- props
            var baseMat = SaveMaterial("PropBody", new Color(0.12f, 0.13f, 0.2f));
            var trimMat = SaveMaterial("PropTrim", new Color(0.23f, 0.91f, 1f));
            var screenMat = SaveMaterial("PropScreen", new Color(0.02f, 0.02f, 0.04f));

            SaveProp("Tablet Stand", root =>
            {
                Slab(root, "Base", 0.3f, 0.2f, 0.04f, 0.03f, baseMat, new Vector3(0f, 0.015f, 0f), Quaternion.Euler(90f, 0f, 0f));
                Slab(root, "Back", 0.26f, 0.22f, 0.02f, 0.015f, baseMat, new Vector3(0f, 0.12f, 0.05f), Quaternion.Euler(-20f, 0f, 0f));
                Slab(root, "Lip", 0.26f, 0.03f, 0.01f, 0.02f, trimMat, new Vector3(0f, 0.04f, -0.03f), Quaternion.identity);
            });
            SaveProp("Wall Screen", root =>
            {
                Slab(root, "Frame", 1.9f, 1.1f, 0.06f, 0.06f, baseMat, new Vector3(0f, 0f, 0.03f), Quaternion.identity);
                Slab(root, "Screen", 1.78f, 1.0f, 0.03f, 0.005f, screenMat, new Vector3(0f, 0f, -0.002f), Quaternion.identity);
                Slab(root, "Accent", 0.6f, 0.02f, 0.01f, 0.01f, trimMat, new Vector3(0f, -0.53f, -0.004f), Quaternion.identity);
            });
            SaveProp("Billboard", root =>
            {
                Slab(root, "Pole", 0.12f, 2.4f, 0.06f, 0.12f, baseMat, new Vector3(0f, 1.2f, 0.1f), Quaternion.identity);
                Slab(root, "Board", 3.2f, 1.8f, 0.08f, 0.1f, baseMat, new Vector3(0f, 3.1f, 0f), Quaternion.identity);
                Slab(root, "Screen", 3.0f, 1.66f, 0.04f, 0.005f, screenMat, new Vector3(0f, 3.1f, -0.052f), Quaternion.identity);
                Slab(root, "Trim", 3.2f, 0.04f, 0.02f, 0.02f, trimMat, new Vector3(0f, 2.18f, -0.05f), Quaternion.identity);
            });

            // ---- bundle
            foreach (var guid in AssetDatabase.FindAssets("", new[] { Folder }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (importer != null && !AssetDatabase.IsValidFolder(importer.assetPath)) importer.assetBundleName = "content";
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(OutFolder);
            var manifest = BuildPipeline.BuildAssetBundles(OutFolder,
                new[] { new AssetBundleBuild { assetBundleName = "content", assetNames = AssetDatabase.GetAssetPathsFromAssetBundle("content") } },
                BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);

            string path = Path.Combine(OutFolder, "content");
            if (manifest == null || !File.Exists(path)) { Debug.LogError("CONTENTPACK: build failed"); return; }
            string hash;
            using (var sha = SHA256.Create()) hash = System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
            Debug.Log($"CONTENTPACK: built {Path.GetFullPath(path)}  {new FileInfo(path).Length} bytes  sha256 {hash}");
        }

        private static void WriteText(string name, string text)
        {
            string path = $"{Folder}/{name}.json";
            File.WriteAllText(path, text);
            AssetDatabase.ImportAsset(path);
        }

        private static Material SaveMaterial(string name, Color color)
        {
            string path = $"{Folder}/{name}.mat";
            var mat = new Material(Shader.Find("Standard")) { color = color };
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private static void SaveProp(string name, System.Action<GameObject> build)
        {
            var root = new GameObject(name);
            build(root);
            PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        private static void Slab(GameObject root, string name, float w, float h, float radius, float depth, Material mat, Vector3 pos, Quaternion rot)
        {
            var mesh = TabletFactory.RoundedSlab(w, h, radius, depth, 6);
            string meshPath = $"{Folder}/Meshes/{root.name}-{name}.asset";
            AssetDatabase.CreateAsset(mesh, meshPath);
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        }
    }
}
