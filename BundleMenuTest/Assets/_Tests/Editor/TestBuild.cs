using System.IO;
using BundleMenu;
using BundleMenu.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch-mode entry points used to verify the package. Not part of the deliverable.</summary>
public static class TestBuild
{
    private const string ScenePath = "Assets/_Tests/TestScene.unity";

    // Step 1 (own Unity invocation so the import finishes before anything uses TMP)
    public static void ImportTmp()
    {
        string pkg = Path.GetFullPath("Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage");
        if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            AssetDatabase.ImportPackage(pkg, false);
        AssetDatabase.Refresh();
        Debug.Log("TESTBUILD: TMP imported = " + File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"));
    }

    // Step 2
    public static void SetupAndBuild()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        BundleMenuEditorTools.CreateSamples();
        File.WriteAllText(BundleMenuEditorTools.OutputFolder + "/catalog.json",
            "{ \"bundles\": [ { \"Id\": \"effects/orbs\", \"DisplayName\": \"Glow Orbs\" } ] }");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera";
        cam.transform.position = new Vector3(0, 1.6f, 0);
        cam.GetComponent<Camera>().clearFlags = CameraClearFlags.Skybox;

        var light = new GameObject("Sun", typeof(Light));
        light.GetComponent<Light>().type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(45, -30, 0);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.transform.localScale = new Vector3(4, 1, 4);
        for (int i = 0; i < 6; i++)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            c.transform.position = new Vector3(-6 + i * 2.4f, 0.5f, 6 + (i % 2) * 2f);
        }

        var menuGo = new GameObject("Bundle Menu");
        var menu = menuGo.AddComponent<BundleMenuController>();
        menu.rememberSettings = false;
        menuGo.AddComponent<TestDirector>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.runInBackground = true;

        var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Build/BundleMenuTest.exe",
            BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log("TESTBUILD: player build " + report.summary.result + " errors=" + report.summary.totalErrors);
    }
}
