using System;
using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Try the shared-menu features on your own. It opens a second connection to your room as a pretend
    /// player ("the ghost"), puts a dummy in front of you to hang its menu and nametag on, and shows you
    /// exactly what the ghost sees of your menu. Presses go both ways through the real server, so this
    /// tests the whole path: your menu -> server -> ghost, and ghost -> server -> your menu.
    /// Everything runs through the same room as a real friend would, and stops when you switch it off.
    /// </summary>
    public sealed class SoloTest : MonoBehaviour
    {
        public BundleMenuController Menu;
        public PresenceClient Presence;
        public Func<Camera> ViewCamera;

        public bool Active { get; private set; }
        public bool Connected => link != null && link.Connected;
        public string Status { get; private set; } = "off";
        /// <summary>What the ghost can currently see of your menu (null until your menu shares something).</summary>
        public MenuState SeenFromOutside { get; private set; }
        public readonly List<string> Log = new List<string>();

        private LiveLink link;
        private Transform dummy;
        private string ghostHash, myHash, roomHash;
        private float nextSend, nextReconnect;
        private MenuState ghostState;

        public void Toggle() { if (Active) Stop("off"); else Start(); }

        public void Start()
        {
            if (Active) return;
            if (Presence == null || Presence.RoomHash == null) { Status = "join a room first (any room, even a private one)"; return; }

            roomHash = Presence.RoomHash;
            myHash = Presence.PlayerHash;
            // A second identity in the same room, derived from yours so it can't collide with a real player.
            ghostHash = PresenceClient.Hash("assetbay-solo-ghost:" + myHash);

            dummy = new GameObject("[BundleMenu] Test dummy").transform;
            DontDestroyOnLoad(dummy.gameObject);
            PlaceDummy();

            ghostState = new MenuState
            {
                open = true, theme = "Test ghost", style = "Cards", control = "browse",
                accent = "#45E08A", accent2 = "#45E08A", panel = "#101820",
                page = "Ghost", tag = "TEST", tagc = "#45E08A",
                rows = new[]
                {
                    new MirrorRow { k = "g1", l = "Press me", v = "test", nav = true },
                    new MirrorRow { k = "g2", l = "And me", v = "", nav = true },
                    new MirrorRow { k = "g3", l = "Not pressable", v = "", nav = false },
                },
            };

            Presence.TestPeer = Peer;
            Active = true;
            Status = "connecting...";
            Log.Clear();
            Note("solo test started");
            Connect();
        }

        public void Stop(string reason)
        {
            Active = false;
            Status = reason;
            if (Presence != null) Presence.TestPeer = null;
            link?.Dispose();
            link = null;
            if (dummy != null) Destroy(dummy.gameObject);
            dummy = null;
            SeenFromOutside = null;
        }

        private void Connect()
        {
            try
            {
                var live = Menu.backendUrl.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/');
                link = new LiveLink(new Uri($"{live}/live?room={roomHash}&player={ghostHash}"));
            }
            catch (Exception e) { Status = "couldn't connect: " + e.Message; }
        }

        private (GorillaTagPlayer.OtherPlayer player, MenuState state)? Peer()
        {
            if (!Active || dummy == null) return null;
            return (new GorillaTagPlayer.OtherPlayer { Body = dummy, Name = "Test ghost", Scale = 1f, UserId = null }, ghostState);
        }

        private void PlaceDummy()
        {
            var cam = ViewCamera?.Invoke();
            if (cam == null || dummy == null) return;
            var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            dummy.position = cam.transform.position + forward.normalized * 2f + Vector3.down * 0.4f;
        }

        /// <summary>Put the dummy back in front of you (it stays where it was placed otherwise).</summary>
        public void Recall() => PlaceDummy();

        private void Update()
        {
            if (!Active) return;
            float now = Time.unscaledTime;

            if (link == null || link.Dead)
            {
                if (link != null) { Status = "dropped: " + link.Error; link.Dispose(); link = null; nextReconnect = now + 3f; }
                if (now >= nextReconnect) Connect();
                return;
            }
            if (!link.Connected) { Status = "connecting..."; return; }

            if (now >= nextSend)
            {
                nextSend = now + 5f;
                link.Send("{\"t\":\"state\",\"state\":" + JsonUtility.ToJson(ghostState) + "}");
                Status = "connected as a second player";
            }

            while (link.Inbox.TryDequeue(out var text))
            {
                LiveMsg msg;
                try { msg = JsonUtility.FromJson<LiveMsg>(text); } catch { continue; }
                if (msg == null) continue;
                if (msg.t == "member" && msg.player == myHash) SeenFromOutside = msg.state;
                else if (msg.t == "cmd") Note($"you pressed \"{msg.cmd?.key ?? msg.cmd?.a}\" on the ghost's menu");
                else if (msg.t == "error") Note("server: " + msg.error);
            }
        }

        // ---- pressing your menu from "outside"

        /// <summary>Press a row of your own menu as the ghost would. Returns what happened.</summary>
        public string PressRow(int index)
        {
            var seen = SeenFromOutside;
            if (!Connected) return "not connected";
            if (seen?.rows == null || seen.rows.Length == 0) return "your menu isn't sharing rows (open it and set 'Let others use my menu')";
            if (index < 0 || index >= seen.rows.Length) return "no such row";
            var row = seen.rows[index];
            Send(new ControlCmd { a = "row", page = seen.page, key = row.k });
            return $"pressed \"{row.l}\" on your menu";
        }

        public string PressAction(string action)
        {
            if (!Connected) return "not connected";
            Send(new ControlCmd { a = action, page = SeenFromOutside?.page });
            return $"sent {action}";
        }

        private void Send(ControlCmd cmd) =>
            link.Send($"{{\"t\":\"control\",\"target\":\"{myHash}\",\"cmd\":{JsonUtility.ToJson(cmd)}}}");

        private void Note(string message)
        {
            Log.Add($"{DateTime.Now:HH:mm:ss}  {message}");
            if (Log.Count > 12) Log.RemoveAt(0);
        }

        [Serializable] private sealed class LiveMsg
        {
            public string t, player, from, error;
            public MenuState state;
            public ControlCmd cmd;
        }

        private void OnDestroy() => Stop("off");
    }
}
