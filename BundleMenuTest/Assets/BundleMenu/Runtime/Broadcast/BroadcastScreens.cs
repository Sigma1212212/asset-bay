using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace BundleMenu
{
    /// <summary>
    /// Video screens placed in the world by the signed feed ("screens"). Opt-in: nothing appears unless the
    /// player turns Broadcast screens on in Settings. Screens only show in the scene they name (empty = any),
    /// play muted until you're within 6 m, and are purely local - no one else is affected.
    /// </summary>
    public sealed class BroadcastScreens : MonoBehaviour
    {
        public bool ScreensEnabled;
        public System.Func<Camera> ViewCamera;

        private readonly Dictionary<string, ScreenEntry> live = new Dictionary<string, ScreenEntry>();
        private float nextSceneCheck;
        private FeedDoc applied;
        private FeedClient feed;

        private sealed class ScreenEntry
        {
            public GameObject Root;
            public VideoPlayer Player;
            public AudioSource Audio;
            public RenderTexture Texture;
            public Material Material;
            public FeedScreen Def;
        }

        public void Apply(FeedClient client)
        {
            feed = client;
            applied = null; // force a rebuild on the next update
        }

        private void Update()
        {
            var doc = ScreensEnabled ? feed?.Current : null;
            if (doc != applied || Time.unscaledTime >= nextSceneCheck)
            {
                nextSceneCheck = Time.unscaledTime + 2f;
                applied = doc;
                Sync(doc);
            }

            // Volume by distance: silent unless you're close.
            var cam = ViewCamera?.Invoke();
            if (cam == null) return;
            foreach (var s in live.Values)
            {
                float d = Vector3.Distance(cam.transform.position, s.Root.transform.position);
                s.Audio.volume = s.Def.volume * Mathf.Clamp01(1f - (d - 2f) / 4f);
            }
        }

        private void Sync(FeedDoc doc)
        {
            var wanted = new HashSet<string>();
            if (doc?.screens != null)
                foreach (var def in doc.screens)
                    if (!string.IsNullOrEmpty(def.id) && !string.IsNullOrEmpty(def.video) && SceneLoaded(def.scene))
                        wanted.Add(def.id);

            foreach (var id in new List<string>(live.Keys))
                if (!wanted.Contains(id)) { Remove(live[id]); live.Remove(id); }

            if (doc?.screens == null) return;
            foreach (var def in doc.screens)
                if (wanted.Contains(def.id) && !live.ContainsKey(def.id)) live[def.id] = Create(def);
        }

        private static bool SceneLoaded(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return true;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).name.IndexOf(scene, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private ScreenEntry Create(FeedScreen def)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "[BundleMenu] Screen " + def.id;
            DontDestroyOnLoad(go);
            Destroy(go.GetComponent<Collider>());
            var p = def.position != null && def.position.Length == 3 ? new Vector3(def.position[0], def.position[1], def.position[2]) : Vector3.zero;
            go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, def.rotationY, 0f));
            go.transform.localScale = new Vector3(def.width, def.width * 9f / 16f, 1f);

            var s = new ScreenEntry { Root = go, Def = def, Texture = new RenderTexture(1280, 720, 0), Material = ModVisuals.Unlit(false) };
            s.Material.mainTexture = s.Texture;
            go.GetComponent<Renderer>().sharedMaterial = s.Material;

            s.Audio = go.AddComponent<AudioSource>();
            s.Audio.spatialBlend = 1f;
            s.Audio.volume = 0f;
            s.Player = go.AddComponent<VideoPlayer>();
            s.Player.source = VideoSource.Url;
            s.Player.url = feed.MediaUrl(def.video);
            s.Player.renderMode = VideoRenderMode.RenderTexture;
            s.Player.targetTexture = s.Texture;
            s.Player.isLooping = def.loop;
            s.Player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            s.Player.SetTargetAudioSource(0, s.Audio);
            s.Player.prepareCompleted += vp => vp.Play();
            s.Player.Prepare();
            return s;
        }

        private static void Remove(ScreenEntry s)
        {
            if (s.Root != null) Destroy(s.Root);
            if (s.Texture != null) { s.Texture.Release(); Destroy(s.Texture); }
            if (s.Material != null) Destroy(s.Material);
        }

        private void OnDestroy()
        {
            foreach (var s in live.Values) Remove(s);
            live.Clear();
        }
    }
}
