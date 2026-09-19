using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BundleMenu.Editor
{
    public static class BundleMenuEditorTools
    {
        public const string OutputFolder = "Assets/StreamingAssets/Bundles";
        private const string SampleFolder = "Assets/BundleMenuSamples";

        [MenuItem("Tools/Bundle Menu/Add Menu To Scene", priority = 0)]
        public static void AddToScene()
        {
            var existing = Object.FindObjectOfType<BundleMenuController>();
            if (existing != null)
            {
                Selection.activeObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                Debug.Log("[BundleMenu] The scene already has a Bundle Menu - selected it.");
                return;
            }

            var go = new GameObject("Bundle Menu");
            go.AddComponent<BundleMenuController>();
            Undo.RegisterCreatedObjectUndo(go, "Add Bundle Menu");
            Selection.activeObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[BundleMenu] Added. Press Play, then Tab to open the menu.");
        }

        [MenuItem("Tools/Bundle Menu/Build Bundles For Current Platform", priority = 20)]
        public static void BuildBundles()
        {
            Directory.CreateDirectory(OutputFolder);
            var manifest = BuildPipeline.BuildAssetBundles(OutputFolder,
                BuildAssetBundleOptions.ChunkBasedCompression,
                EditorUserBuildSettings.activeBuildTarget);
            AssetDatabase.Refresh();

            if (manifest == null) Debug.LogError("[BundleMenu] Bundle build failed - see errors above.");
            else Debug.Log($"[BundleMenu] Built {manifest.GetAllAssetBundles().Length} bundles into {OutputFolder}.");
        }

        /// <summary>
        /// Makes a handful of coloured prefabs, tags them with bundle names (including a shared-material
        /// dependency), and builds them - so the Local source has something real to load.
        /// </summary>
        [MenuItem("Tools/Bundle Menu/Create Sample Bundles", priority = 21)]
        public static void CreateSamples()
        {
            Directory.CreateDirectory(SampleFolder);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
            var shared = new Material(shader) { color = new Color(0.35f, 0.8f, 1f) };
            string sharedPath = $"{SampleFolder}/SharedBlue.mat";
            AssetDatabase.CreateAsset(shared, sharedPath);
            AssetImporter.GetAtPath(sharedPath).assetBundleName = "shared/materials";

            MakePrefab("Crate", PrimitiveType.Cube, sharedPath, "props/crates");
            MakePrefab("Barrel", PrimitiveType.Cylinder, sharedPath, "props/crates");
            MakePrefab("Orb", PrimitiveType.Sphere, null, "effects/orbs", new Color(1f, 0.55f, 0.2f));
            MakePrefab("Pill", PrimitiveType.Capsule, null, "effects/orbs", new Color(0.9f, 0.3f, 0.7f));
            MakePrefab("Pillar", PrimitiveType.Cylinder, null, "environment/stone", new Color(0.6f, 0.6f, 0.55f));

            AssetDatabase.SaveAssets();
            BuildBundles();
        }

        private static void MakePrefab(string name, PrimitiveType type, string materialPath, string bundle, Color? color = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var renderer = go.GetComponent<Renderer>();
            if (materialPath != null)
            {
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            }
            else
            {
                string matPath = $"{SampleFolder}/{name}.mat";
                var mat = new Material(renderer.sharedMaterial) { color = color ?? Color.white };
                AssetDatabase.CreateAsset(mat, matPath);
                AssetImporter.GetAtPath(matPath).assetBundleName = bundle;
                renderer.sharedMaterial = mat;
            }

            string path = $"{SampleFolder}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            AssetImporter.GetAtPath(path).assetBundleName = bundle;
        }
    }
}
