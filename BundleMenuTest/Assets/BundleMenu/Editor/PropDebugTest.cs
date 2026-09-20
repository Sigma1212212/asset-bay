using System.Collections;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BundleMenu.Editor
{
    /// <summary>
    /// A close look at one prop when the main test says something is wrong: drops a probe on it and
    /// writes down every contact and velocity. Temporary scaffolding, kept because it's cheap.
    ///
    ///   Unity.exe -batchmode -projectPath BundleMenuTest -executeMethod BundleMenu.Editor.PropDebugTest.Run
    /// </summary>
    public static class PropDebugTest
    {
        public const string PropName = "Trampoline";
        public static string ResultPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../../propdebug.txt"));

        [MenuItem("Tools/Bundle Menu/Debug One Prop", priority = 45)]
        public static void Run()
        {
            if (File.Exists(ResultPath)) File.Delete(ResultPath);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Prop debug").AddComponent<PropDebugRunner>();

            EditorApplication.playModeStateChanged += Done;
            EditorApplication.EnterPlaymode();
        }

        private static void Done(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= Done;
            Debug.Log(File.Exists(ResultPath) ? File.ReadAllText(ResultPath) : "PROPDEBUG: nothing written");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }

    public sealed class PropDebugRunner : MonoBehaviour
    {
        private readonly StringBuilder log = new StringBuilder();

        private void Start() => StartCoroutine(Go());

        private IEnumerator Go()
        {
            var theme = ScriptableObject.CreateInstance<MenuTheme>();
            var def = PropLibrary.Find(PropDebugTest.PropName);
            var prop = def.Build(new PropBuild { Theme = theme, WalkableLayer = 0, Allowed = () => true });
            prop.transform.position = Vector3.zero;

            foreach (var collider in prop.GetComponentsInChildren<Collider>(true))
                log.AppendLine($"collider on {collider.name}: {collider.GetType().Name} trigger={collider.isTrigger} " +
                               $"bounds={collider.bounds.min.y:0.00}..{collider.bounds.max.y:0.00} enabled={collider.enabled}");

            var bouncer = prop.GetComponentInChildren<Bouncer>();
            log.AppendLine("bouncer on: " + (bouncer != null ? bouncer.gameObject.name : "none"));

            var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            probe.transform.localScale = Vector3.one * 0.35f;
            var rb = probe.AddComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            probe.AddComponent<ContactLog>().Log = log;
            rb.position = new Vector3(0f, 1.6f, 0f);
            rb.velocity = Vector3.down * 3f;

            for (int i = 0; i < 90; i++)
            {
                if (i % 10 == 0) log.AppendLine($"step {i}: y={rb.position.y:0.00} vy={rb.velocity.y:0.00}");
                yield return new WaitForFixedUpdate();
            }

            File.WriteAllText(PropDebugTest.ResultPath, log.ToString());
            EditorApplication.isPlaying = false;
        }
    }

    /// <summary>Writes down everything the probe touches.</summary>
    public sealed class ContactLog : MonoBehaviour
    {
        public StringBuilder Log;

        private void OnCollisionEnter(Collision collision) =>
            Log?.AppendLine($"probe hit {collision.collider.name} at y={transform.position.y:0.00}");
    }
}
