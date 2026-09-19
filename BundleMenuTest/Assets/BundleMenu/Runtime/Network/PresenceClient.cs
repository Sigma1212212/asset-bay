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
        public string tag;         // the trait shown above this player ("ADMIN", "DEV", ...)
        public string tagc;        // its colour, "#RRGGBB"
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

        /// <summary>Everyone in the room (Asset Bay or not), refreshed with the member list.</summary>
        public readonly List<GorillaTagPlayer.OtherPlayer> Roster = new List<GorillaTagPlayer.OtherPlayer>();

        /// <summary>What this player's menu is sharing, or null if they don't run Asset Bay.</summary>
        public MenuState StateFor(GorillaTagPlayer.OtherPlayer p)
        {
            string h = HashFor(p);
            return h != null && liveMembers.TryGetValue(h, out var m) ? m.state : null;
        }

        /// <summary>Other players in this room who run Asset Bay, keyed to their rig.</summary>
        public readonly List<(GorillaTagPlayer.OtherPlayer player, MenuState state)> Others =
            new List<(GorillaTagPlayer.OtherPlayer, MenuState)>();

        public string LastError { get; private set; }

        /// <summary>The menu changed: share it now (rate limited). Cheaper than checking every frame.</summary>
        public void TouchState() => stateDirty = true;
        public event Action Changed;

        private float nextPost;
        private bool busy, polled;
        private string joinedRoom, joinedPlayer; // hashes we last announced, so we can leave cleanly

        // ---- live mode: one WebSocket, updates pushed both ways (polling is the fallback)
        /// <summary>Use the live connection when it works. Off = always poll.</summary>
        public bool UseLive = true;
        public bool IsLive => link != null && link.Connected;
        private LiveLink link;
        private string linkRoom;
        private int liveFailures;
        private float liveRetryAt, nextIdentity, lastSentAt, nextRebuild;
        private bool stateDirty = true;
        private string roomHash, playerHash, room, lastSentJson;
        private bool membersDirty;
        private readonly Dictionary<string, (MenuState state, float seen)> liveMembers = new Dictionary<string, (MenuState, float)>();

        [Serializable] private sealed class LiveMsg
        {
            public string t, player, from, error;
            public MenuState state;
            public Member[] members;
            public ControlCmd cmd;
        }

        private string LiveUrl
        {
            get
            {
                if (string.IsNullOrEmpty(Endpoint)) return null;
                var baseUrl = Endpoint.Replace("https://", "wss://").Replace("http://", "ws://");
                return baseUrl.Substring(0, baseUrl.LastIndexOf('/') + 1) + $"live?room={roomHash}&player={playerHash}";
            }
        }

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
            if (to != null && IsLive)
            {
                link.Send($"{{\"t\":\"control\",\"target\":\"{to}\",\"cmd\":{JsonUtility.ToJson(cmd)}}}");
                return null; // instant; refusals come back as an "error" message
            }
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
            if (Player == null) return;
            float now = Time.unscaledTime;

            if (now >= nextIdentity)
            {
                nextIdentity = now + 0.5f;
                string r = Player.RoomName, me = Player.LocalUserId;
                if (!Sharing || string.IsNullOrEmpty(r) || string.IsNullOrEmpty(me))
                {
                    room = null;
                    Leave();
                    if (Others.Count > 0) { Others.Clear(); Changed?.Invoke(); }
                    return;
                }
                if (r != room)
                {
                    if (joinedRoom != null) Leave(); // switched rooms
                    room = r;
                    roomHash = Hash("assetbay:" + r);
                    playerHash = Hash("assetbay:" + r + ":" + me);
                    liveMembers.Clear();
                }
                currentRoom = room;
            }
            if (room == null) return;

            // Live link: open, replace if the room changed, retire if it died.
            if (link != null && (link.Dead || linkRoom != roomHash))
            {
                if (link.Dead) { LastError = link.Error; liveRetryAt = now + (++liveFailures <= 3 ? 5f : 60f); }
                link.Dispose();
                link = null;
            }
            if (UseLive && link == null && now >= liveRetryAt && LiveUrl != null)
            {
                try { link = new LiveLink(new Uri(LiveUrl)); linkRoom = roomHash; }
                catch (Exception e) { LastError = e.Message; liveRetryAt = now + 60f; }
            }
            if (IsLive)
            {
                liveFailures = 0;
                if (polled) LeavePolling();
                joinedRoom = roomHash;
                joinedPlayer = playerHash;
                LiveTick(now);
                return;
            }

            // Fallback: poll.
            if (busy || now < nextPost) return;
            nextPost = now + (IntervalNow?.Invoke() ?? Interval);
            Post(roomHash, playerHash, JsonUtility.ToJson(LocalState?.Invoke() ?? new MenuState()), room).Forget();
        }

        /// <summary>Live mode, every frame: apply what arrived, send our state when it changes.</summary>
        private void LiveTick(float now)
        {
            while (link.Inbox.TryDequeue(out var text))
            {
                LiveMsg msg;
                try { msg = JsonUtility.FromJson<LiveMsg>(text); } catch { continue; }
                switch (msg?.t)
                {
                    case "members":
                        liveMembers.Clear();
                        if (msg.members != null) foreach (var m in msg.members) liveMembers[m.player] = (m.state, now);
                        membersDirty = true;
                        break;
                    case "member":
                        if (msg.player != null) { liveMembers[msg.player] = (msg.state, now); membersDirty = true; }
                        break;
                    case "leave":
                        if (msg.player != null && liveMembers.Remove(msg.player)) membersDirty = true;
                        break;
                    case "cmd":
                        if (msg.cmd != null) { try { CommandReceived?.Invoke(msg.from, msg.cmd); } catch (Exception e) { Debug.LogException(e); } }
                        break;
                    case "error":
                        LastError = msg.error;
                        break;
                }
            }

            // Our state goes out when the menu says it changed (at most 5x a second), plus a 10 s "still here".
            if ((stateDirty && now - lastSentAt >= 0.2f) || now - lastSentAt > 10f)
            {
                string json = JsonUtility.ToJson(LocalState?.Invoke() ?? new MenuState());
                stateDirty = false;
                if (json != lastSentJson || now - lastSentAt > 10f)
                {
                    link.Send($"{{\"t\":\"state\",\"state\":{json}}}");
                    lastSentJson = json;
                    lastSentAt = now;
                }
            }

            // Anyone we haven't heard from in 30 s is gone (live peers refresh every 10 s, pollers every 5).
            List<string> stale = null;
            foreach (var kv in liveMembers) if (now - kv.Value.seen > 30f) (stale ??= new List<string>()).Add(kv.Key);
            if (stale != null) { foreach (var k in stale) liveMembers.Remove(k); membersDirty = true; }

            // Match hashes to the rigs around us (also re-run each second: players join and leave the lobby).
            if (membersDirty || now >= nextRebuild)
            {
                // Finding rigs means a scene scan, so it's rare unless someone just joined or left.
                nextRebuild = now + (membersDirty ? 0.5f : 3f);
                membersDirty = false;
                Others.Clear();
                Roster.Clear();
                Roster.AddRange(Player.OtherPlayers());
                for (int i = 0; i < Roster.Count; i++)
                {
                    var p = Roster[i];
                    string h = HashFor(p);
                    if (h != null && liveMembers.TryGetValue(h, out var m) && m.state != null) Others.Add((p, m.state));
                }
                LastError = null;
                Changed?.Invoke();
            }
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
                polled = true;

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
            link?.Dispose();
            link = null;
            lastSentJson = null;
            if (polled) LeavePolling();
            joinedRoom = joinedPlayer = null;
        }

        /// <summary>Tell the server we stopped polling (live members leave by closing their socket).</summary>
        private void LeavePolling()
        {
            polled = false;
            if (joinedRoom == null) return;
            Send($"{{\"room\":\"{joinedRoom}\",\"player\":\"{joinedPlayer}\",\"state\":null}}").Forget();
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
