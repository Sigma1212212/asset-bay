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
        public string control;     // "off" / "browse" / "full": may others press buttons on this menu
        public string pg;          // "1/3" page counter of the mirror
        public MirrorRow[] rows;   // the visible rows, so others can show (and press) a copy
    }

    /// <summary>One row of a shared menu page: key, label, value, on, navigates.</summary>
    [Serializable]
    public sealed class MirrorRow
    {
        public string k, l, v;
        public bool on, nav;
    }

    /// <summary>A press sent to someone's menu: a = "row" (with key), "back", "home", "next", "prev", "open", "close".</summary>
    [Serializable]
    public sealed class ControlCmd
    {
        public string a, page, key;
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
        public string ControlEndpoint;
        /// <summary>Seconds between updates right now (faster while remote control is in use).</summary>
        public Func<float> IntervalNow;
        /// <summary>A press from another member arrived: (sender hash, command).</summary>
        public event Action<string, ControlCmd> CommandReceived;
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
        [Serializable] private sealed class Command { public string from; public ControlCmd cmd; }
        [Serializable] private sealed class Reply { public Member[] members; public Command[] commands; }

        private string currentRoom;

        /// <summary>The hash the server knows another player by (same formula they use for themselves).</summary>
        public string HashFor(GorillaTagPlayer.OtherPlayer p) =>
            string.IsNullOrEmpty(currentRoom) || string.IsNullOrEmpty(p.UserId) ? null : Hash("assetbay:" + currentRoom + ":" + p.UserId);

        /// <summary>Press a button on another player's menu (only works if they allowed it).</summary>
        public async System.Threading.Tasks.Task<string> SendControl(GorillaTagPlayer.OtherPlayer target, ControlCmd cmd)
        {
            string to = HashFor(target);
            if (to == null || joinedRoom == null || string.IsNullOrEmpty(ControlEndpoint)) return "not in a room";
            string body = $"{{\"room\":\"{joinedRoom}\",\"from\":\"{joinedPlayer}\",\"target\":\"{to}\",\"cmd\":{JsonUtility.ToJson(cmd)}}}";
            try
            {
                using (var req = new UnityWebRequest(ControlEndpoint, "POST"))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.timeout = 8;
                    await req.SendWebRequest().Await();
                    nextPost = Mathf.Min(nextPost, Time.unscaledTime + 0.5f); // refresh their mirror soon
                    return req.responseCode == 200 ? null
                         : req.responseCode == 403 ? "they turned control off"
                         : req.responseCode == 429 ? "slow down" : $"failed ({req.responseCode})";
                }
            }
            catch (Exception e) { return e.Message; }
        }

        private void Update()
        {
            if (busy || Player == null || Time.unscaledTime < nextPost) return;
            nextPost = Time.unscaledTime + (IntervalNow?.Invoke() ?? Interval);

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
            currentRoom = room;
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
                if (reply?.commands != null)
                    foreach (var c in reply.commands)
                        if (c?.cmd != null) { try { CommandReceived?.Invoke(c.from, c.cmd); } catch (Exception e) { Debug.LogException(e); } }
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
