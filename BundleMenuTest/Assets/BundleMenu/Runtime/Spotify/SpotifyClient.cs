using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BundleMenu
{
    /// <summary>What Spotify says is going on right now.</summary>
    public sealed class SpotifyState
    {
        public bool Playing;
        public string Track = "", Artist = "", Album = "", ArtUrl = "", Device = "", Context = "";
        public int PositionMs, DurationMs, Volume = -1;
        public bool Shuffle;
        public string Repeat = "off";
        public bool Premium = true;      // controls need Premium; read-only still works without it
        public DateTime Fetched;
    }

    public sealed class SpotifyItem
    {
        public string Name = "", Id = "", Uri = "", Detail = "";
    }

    /// <summary>
    /// Talks to Spotify's Web API with your own sign-in. Reading works on any account; play / pause /
    /// skip need Premium (Spotify's rule, not ours), and the menu says so when it's refused.
    /// </summary>
    public sealed class SpotifyClient : MonoBehaviour
    {
        private const string Api = "https://api.spotify.com/v1";

        /// <summary>The one running inside the game, for props that follow your music.</summary>
        public static SpotifyClient Live { get; private set; }

        public SpotifyState State { get; private set; } = new SpotifyState();
        public string LastError { get; private set; }
        public bool Busy { get; private set; }
        public readonly List<SpotifyItem> Playlists = new List<SpotifyItem>();
        public readonly List<SpotifyItem> Devices = new List<SpotifyItem>();
        public event Action Changed;

        /// <summary>How often to ask what's playing. Faster while something of ours is showing it.</summary>
        public Func<float> Interval;
        private float nextPoll;
        private bool polling;

        private void Awake() => Live = this;

        private void OnDestroy() { if (Live == this) Live = null; }

        private void Update()
        {
            if (polling || Time.unscaledTime < nextPoll || !SpotifyAuth.SignedIn) return;
            nextPoll = Time.unscaledTime + Mathf.Max(2f, Interval?.Invoke() ?? 15f);
            RefreshNowPlayingAsync().Forget();
        }

        // ------------------------------------------------------------------ reading

        public async Task RefreshNowPlayingAsync()
        {
            polling = true;
            try
            {
                string json = await GetAsync("/me/player");
                if (json == null) return;
                if (json.Length < 5) { State = new SpotifyState { Fetched = DateTime.Now, Device = "nothing playing" }; Changed?.Invoke(); return; }

                var reply = JsonUtility.FromJson<PlayerReply>(Wrap(json));
                var next = new SpotifyState
                {
                    Playing = reply.is_playing,
                    PositionMs = reply.progress_ms,
                    Shuffle = reply.shuffle_state,
                    Repeat = reply.repeat_state ?? "off",
                    Device = reply.device?.name ?? "",
                    Volume = reply.device?.volume_percent ?? -1,
                    Track = reply.item?.name ?? "",
                    DurationMs = reply.item?.duration_ms ?? 0,
                    Album = reply.item?.album?.name ?? "",
                    Fetched = DateTime.Now,
                    Premium = State.Premium,
                };
                if (reply.item?.artists != null && reply.item.artists.Length > 0)
                {
                    var names = new StringBuilder();
                    foreach (var artist in reply.item.artists) names.Append(names.Length > 0 ? ", " : "").Append(artist.name);
                    next.Artist = names.ToString();
                }
                var images = reply.item?.album?.images;
                if (images != null && images.Length > 0) next.ArtUrl = images[images.Length > 1 ? 1 : 0].url; // middle size
                State = next;
                Changed?.Invoke();
            }
            finally { polling = false; }
        }

        public async Task RefreshPlaylistsAsync()
        {
            string json = await GetAsync("/me/playlists?limit=20");
            if (json == null) return;
            Playlists.Clear();
            var reply = JsonUtility.FromJson<PlaylistsReply>(Wrap(json));
            if (reply?.items != null)
                foreach (var p in reply.items)
                    Playlists.Add(new SpotifyItem { Name = p.name, Id = p.id, Uri = p.uri, Detail = $"{p.tracks?.total ?? 0} tracks" });
            Changed?.Invoke();
        }

        public async Task RefreshDevicesAsync()
        {
            string json = await GetAsync("/me/player/devices");
            if (json == null) return;
            Devices.Clear();
            var reply = JsonUtility.FromJson<DevicesReply>(Wrap(json));
            if (reply?.devices != null)
                foreach (var d in reply.devices)
                    Devices.Add(new SpotifyItem { Name = d.name, Id = d.id, Detail = d.is_active ? "playing here" : d.type });
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ controls (Premium)

        public Task PlayPause() => State.Playing ? Command("PUT", "/me/player/pause") : Command("PUT", "/me/player/play");
        public Task Next() => Command("POST", "/me/player/next");
        public Task Previous() => Command("POST", "/me/player/previous");
        public Task Seek(int ms) => Command("PUT", $"/me/player/seek?position_ms={Mathf.Max(0, ms)}");
        public Task SetVolume(int percent) => Command("PUT", $"/me/player/volume?volume_percent={Mathf.Clamp(percent, 0, 100)}");
        public Task Shuffle(bool on) => Command("PUT", $"/me/player/shuffle?state={(on ? "true" : "false")}");
        public Task Repeat(string mode) => Command("PUT", $"/me/player/repeat?state={mode}");
        public Task PlayUri(string uri) => Command("PUT", "/me/player/play", "{\"context_uri\":\"" + uri + "\"}");
        public Task UseDevice(string id) => Command("PUT", "/me/player", "{\"device_ids\":[\"" + id + "\"],\"play\":true}");

        private async Task Command(string method, string path, string body = null)
        {
            string token = await SpotifyAuth.TokenAsync();
            if (token == null) { LastError = SpotifyAuth.LastError; return; }
            Busy = true;
            try
            {
                using (var request = new UnityWebRequest(Api + path, method))
                {
                    if (body != null) request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Authorization", "Bearer " + token);
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = 10;
                    await request.SendWebRequest().Await();
                    LastError = request.responseCode switch
                    {
                        200 or 202 or 204 => null,
                        403 => "Spotify needs Premium for that",
                        404 => "open Spotify on a device first",
                        429 => "too many requests, slow down",
                        _ => $"Spotify said {request.responseCode}",
                    };
                }
            }
            catch (Exception e) { LastError = e.Message; }
            finally
            {
                Busy = false;
                nextPoll = Time.unscaledTime + 0.4f;  // show the result quickly
            }
        }

        private async Task<string> GetAsync(string path)
        {
            string token = await SpotifyAuth.TokenAsync();
            if (token == null) { LastError = SpotifyAuth.LastError; return null; }
            try
            {
                using (var request = UnityWebRequest.Get(Api + path))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + token);
                    request.timeout = 10;
                    await request.SendWebRequest().Await();
                    if (request.responseCode == 204) return "";           // nothing playing
                    if (request.responseCode != 200)
                    {
                        LastError = request.responseCode == 401 ? "sign in again in the launcher" : $"Spotify said {request.responseCode}";
                        return null;
                    }
                    LastError = null;
                    return request.downloadHandler.text;
                }
            }
            catch (Exception e) { LastError = e.Message; return null; }
        }

        /// <summary>JsonUtility can't start at an array, so replies that do are wrapped.</summary>
        private static string Wrap(string json) => json.TrimStart().StartsWith("[") ? "{\"items\":" + json + "}" : json;

        // ------------------------------------------------------------------ the bits of Spotify's replies we use

        [Serializable] private sealed class PlayerReply
        {
            public bool is_playing, shuffle_state;
            public int progress_ms;
            public string repeat_state;
            public Device device;
            public Item item;
        }
        [Serializable] private sealed class Device { public string id, name, type; public bool is_active; public int volume_percent; }
        [Serializable] private sealed class Item { public string name; public int duration_ms; public Album album; public Artist[] artists; }
        [Serializable] private sealed class Album { public string name; public Image[] images; }
        [Serializable] private sealed class Image { public string url; public int width, height; }
        [Serializable] private sealed class Artist { public string name; }
        [Serializable] private sealed class PlaylistsReply { public Playlist[] items; }
        [Serializable] private sealed class Playlist { public string id, name, uri; public Tracks tracks; }
        [Serializable] private sealed class Tracks { public int total; }
        [Serializable] private sealed class DevicesReply { public Device[] devices; }
    }
}
