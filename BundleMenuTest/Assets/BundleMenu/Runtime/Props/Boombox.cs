using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BundleMenu
{
    /// <summary>
    /// A boombox that follows your own Spotify: the cover art appears on the front, the speakers thump
    /// in time, and the lights go out when the music stops. It reads the same sign-in the Spotify page
    /// uses - no sound is played here, it's the music already coming out of your speakers.
    /// </summary>
    public sealed class Boombox : MonoBehaviour
    {
        public Transform LeftCone, RightCone;
        public Renderer Screen;
        public Light Glow;
        public Transform Needle;

        private Material screenMaterial;
        private Texture2D art;
        private string artUrl;
        private float pulse, level;
        private int lastBeat;

        private void Start()
        {
            if (Screen != null)
            {
                screenMaterial = Screen.sharedMaterial;
                if (screenMaterial != null && !screenMaterial.name.EndsWith("(menu)"))
                {
                    screenMaterial = new Material(screenMaterial) { name = "boombox screen (menu)" };
                    Screen.sharedMaterial = screenMaterial;
                }
            }
        }

        private void Update()
        {
            var client = SpotifyClient.Live;
            var state = client != null ? client.State : null;
            bool playing = state != null && state.Playing;

            // No audio to listen to, so the beat is guessed from the track position: a steady thump
            // that speeds up a little for shorter songs, and stops the moment you pause.
            if (playing)
            {
                float seconds = state.PositionMs / 1000f + (float)(System.DateTime.Now - state.Fetched).TotalSeconds;
                int beat = Mathf.FloorToInt(seconds * 2f);
                if (beat != lastBeat) { lastBeat = beat; pulse = 1f; }
            }
            pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 3.5f);
            level = Mathf.Lerp(level, playing ? 1f : 0f, Time.deltaTime * 4f);

            float push = 1f + 0.18f * pulse * level;
            if (LeftCone != null) LeftCone.localScale = new Vector3(push, push, 1f) * 0.32f;
            if (RightCone != null) RightCone.localScale = new Vector3(push, push, 1f) * 0.32f;
            if (Glow != null) Glow.intensity = 0.4f + 1.6f * level * (0.6f + 0.4f * pulse);
            if (Needle != null) Needle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(40f, -40f, Progress(state)));

            if (state != null && state.ArtUrl != artUrl)
            {
                artUrl = state.ArtUrl;
                LoadArt(artUrl).Forget();
            }
        }

        /// <summary>How far through the track we are, counting on from the last time we asked Spotify.</summary>
        private static float Progress(SpotifyState state)
        {
            if (state == null || state.DurationMs <= 0) return 0f;
            float ms = state.PositionMs + (state.Playing ? (float)(System.DateTime.Now - state.Fetched).TotalMilliseconds : 0f);
            return Mathf.Clamp01(ms / state.DurationMs);
        }

        private async Task LoadArt(string url)
        {
            if (screenMaterial == null) return;
            if (string.IsNullOrEmpty(url)) { screenMaterial.mainTexture = null; return; }
            try
            {
                using (var request = UnityWebRequestTexture.GetTexture(url))
                {
                    request.timeout = 10;
                    await request.SendWebRequest().Await();
                    if (request.result != UnityWebRequest.Result.Success || url != artUrl || screenMaterial == null) return;
                    var texture = DownloadHandlerTexture.GetContent(request);
                    if (art != null) Destroy(art);
                    art = texture;
                    screenMaterial.mainTexture = art;
                }
            }
            catch { /* no cover art, no problem */ }
        }

        private void OnDestroy()
        {
            if (art != null) Destroy(art);
        }
    }
}
