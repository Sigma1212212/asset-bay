using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace BundleMenu
{
    /// <summary>What one menu shares with other Asset Bay users in the same room.</summary>
    [Serializable]
    public sealed class MenuState
    {
        public bool open;
        public string theme;
        public string style;    // menu type name ("Book", "Pillars"...)
        public string accent;   // "#RRGGBB"
        public string accent2;
        public string panel;
        public string page;
        public string video;
    }

    /// <summary>
    /// Finds other Asset Bay users in your Gorilla Tag room through the Worker's presence service.
    /// Only SHA-256 hashes of (room) and (room + player id) are sent, never names or ids. Positions come
    /// from the game itself, so this only syncs small state every 5 seconds and never touches game networking.
    /// </summary>
    public sealed class PresenceClient : MonoBehaviour
    {
        public const float Interval = 5f;

        public bool Sharing = true;
        public string Endpoint;
        public GorillaTagPlayer Player;
        public Func<MenuState> LocalState;

        /// <summary>Other players in this room who run Asset Bay, keyed to their rig.</summary>
        public readonly List<(GorillaTagPlayer.OtherPlayer player, MenuState state)> Others =
            new List<(GorillaTagPlayer.OtherPlayer, MenuState)>();

        public string LastError { get; private set; }
        public event Action Changed;

        private float nextPost;
        private bool busy;
        private string joinedRoom, joinedPlayer; // hashes we last announced, so we can leave cleanly

        [Serializable] private sealed class Member { public string player; public MenuState state; }
        [Serializable] private sealed class Reply { public Member[] members; }

        private void Update()
        {
            if (busy || Player == null || Time.unscaledTime < nextPost) return;
            nextPost = Time.unscaledTime + Interval;

            string room = Player.RoomName, me = Player.LocalUserId;
            if (!Sharing || string.IsNullOrEmpty(room) || string.IsNullOrEmpty(me))
            {
                Leave();
                if (Others.Count > 0) { Others.Clear(); Changed?.Invoke(); }
                return;
            }

            string roomHash = Hash("assetbay:" + room);
            string playerHash = Hash("assetbay:" + room + ":" + me);
            if (joinedRoom != null && joinedRoom != roomHash) Leave(); // switched rooms
            Post(roomHash, playerHash, JsonUtility.ToJson(LocalState?.Invoke() ?? new MenuState()), room).Forget();
        }

        private async System.Threading.Tasks.Task Post(string roomHash, string playerHash, string stateJson, string room)
        {
            busy = true;
            try
            {
                string body = $"{{\"room\":\"{roomHash}\",\"player\":\"{playerHash}\",\"state\":{stateJson}}}";
                string text = await Send(body);
                joinedRoom = roomHash;
                joinedPlayer = playerHash;

                var reply = JsonUtility.FromJson<Reply>(text);
                var byHash = new Dictionary<string, MenuState>();
                if (reply?.members != null) foreach (var m in reply.members) byHash[m.player] = m.state;

                // Match hashes to the rigs around us by hashing their ids the same way.
                Others.Clear();
                foreach (var p in Player.OtherPlayers())
                {
                    if (string.IsNullOrEmpty(p.UserId)) continue;
                    if (byHash.TryGetValue(Hash("assetbay:" + room + ":" + p.UserId), out var state)) Others.Add((p, state));
                }
                LastError = null;
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                LastError = e.Message;
            }
            finally
            {
                busy = false;
            }
        }

        private void Leave()
        {
            if (joinedRoom == null) return;
            Send($"{{\"room\":\"{joinedRoom}\",\"player\":\"{joinedPlayer}\",\"state\":null}}").Forget();
            joinedRoom = joinedPlayer = null;
        }

        private async System.Threading.Tasks.Task<string> Send(string json)
        {
            using (var req = new UnityWebRequest(Endpoint, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 10;
                await req.SendWebRequest().Await();
                if (req.result != UnityWebRequest.Result.Success) throw new InvalidOperationException(req.error);
                return req.downloadHandler.text;
            }
        }

        public static string Hash(string s)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
                var sb = new StringBuilder(64);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private void OnDestroy() => Leave();
    }
}
