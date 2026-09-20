using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BundleMenu.Editor
{
    /// <summary>
    /// Runs the prop self-test for real: enters play mode, lets physics run, and writes the results to
    /// proptest.txt next to the project. Same checks the in-game button runs, so a green run here means
    /// the props behave; only Gorilla Tag's own rig can prove the last mile.
    ///
    ///   Unity.exe -batchmode -projectPath BundleMenuTest -executeMethod BundleMenu.Editor.PropPlayTest.Run
    ///
    /// (no -quit: the test quits the editor itself when it's finished)
    /// </summary>
    public static class PropPlayTest
    {
        public static string ResultPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../../proptest.txt"));

        private static double deadline;

        [MenuItem("Tools/Bundle Menu/Run Prop Test (play mode)", priority = 44)]
        public static void Run()
        {
            if (File.Exists(ResultPath)) File.Delete(ResultPath);

            // Play mode starts far more reliably in batch mode without a domain reload.
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Prop test").AddComponent<PropTestRunner>();

            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            deadline = EditorApplication.timeSinceStartup + 300.0;
            EditorApplication.update += Watchdog;
            EditorApplication.EnterPlaymode();
        }

        /// <summary>If play mode never gets going, say so and quit rather than hanging forever.</summary>
        private static void Watchdog()
        {
            if (EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Watchdog;
            Debug.Log("PROPTEST: gave up waiting (play mode = " + EditorApplication.isPlaying + ")");
            if (Application.isBatchMode) EditorApplication.Exit(2);
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            Debug.Log("PROPTEST: play mode -> " + change);
            if (change != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.update -= Watchdog;

            string results = File.Exists(ResultPath) ? File.ReadAllText(ResultPath) : "PROPTEST: no results were written";
            Debug.Log(results);
            if (Application.isBatchMode) EditorApplication.Exit(results.Contains("PROPTEST: all") ? 0 : 1);
        }
    }

    /// <summary>Drives the runtime self-test inside play mode and writes what happened.</summary>
    public sealed class PropTestRunner : MonoBehaviour
    {
        private PropSelfTest test;
        private MenuTheme theme;

        private void Start()
        {
            Debug.Log("PROPTEST: runner started");
            // A floor, so props that need something to stand on behave as they would in a map.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, 398.5f, 0f);
            floor.transform.localScale = Vector3.one * 20f;

            theme = ScriptableObject.CreateInstance<MenuTheme>();
            test = gameObject.AddComponent<PropSelfTest>();
            test.Build = () => new PropBuild
            {
                Theme = theme,
                WalkableLayer = 0,
                Allowed = () => true,
                Teleport = _ => false,          // no player here: the probe is moved directly
                SaveCheckpoint = () => { },
                Surprise = _ => { },
            };
            test.Where = () => new Vector3(0f, 400f, 0f);
            test.Progress = message => Debug.Log("PROPTEST step: " + message);
            test.Finished = Write;
            test.Run();
        }

        private void Write()
        {
            var text = new StringBuilder();
            text.AppendLine(test.Failed == 0
                ? $"PROPTEST: all {test.Passed} props behaved"
                : $"PROPTEST: {test.Failed} of {test.Passed + test.Failed} props had a problem");
            foreach (var result in test.Results)
                text.AppendLine($"  [{(result.Ok ? "ok  " : "FAIL")}] {result.Name,-20} {result.Note}");

            File.WriteAllText(PropPlayTest.ResultPath, text.ToString());
            if (theme != null) Destroy(theme);
            EditorApplication.isPlaying = false;
        }
    }
}
