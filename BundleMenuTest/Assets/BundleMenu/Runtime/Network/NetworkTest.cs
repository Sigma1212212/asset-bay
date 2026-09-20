using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BundleMenu
{
    /// <summary>
    /// One button that checks the whole online chain and says what worked: the server, your live
    /// connection, other people seeing your menu, and presses coming back to you. It uses a temporary
    /// second connection (like the solo test) so it proves the round trip without anyone else present,
    /// and puts every setting back the way it was afterwards.
    /// </summary>
    public sealed class NetworkTest : MonoBehaviour
    {
        public BundleMenuController Menu;
        public PresenceClient Presence;

        public bool Running { get; private set; }
        public string Summary { get; private set; } = "not run yet";
        public readonly List<string> Results = new List<string>();

        private const string Pass = "ok", Fail = "FAILED";

        public void Run()
        {
            if (!Running) RunAsync().Forget();
        }

        private async Task RunAsync()
        {
            Running = true;
            Results.Clear();
            Summary = "testing...";
            int failures = 0;
            var clock = new Stopwatch();
            LiveLink ghost = null;
            var controlBefore = Menu.RemoteControl;

            void Note(string step, bool ok, string detail = "")
            {
                Results.Add($"{(ok ? Pass : Fail)}  {step}{(detail.Length > 0 ? "  " + detail : "")}");
                if (!ok) failures++;
                Menu.RefreshNow();
            }

            try
            {
                // 1. Is the server there at all?
                clock.Restart();
                string health = await FeedClient.GetText(Menu.backendUrl.TrimEnd('/') + "/health");
                Note("server answers", health != null && health.Contains("true"), $"{clock.ElapsedMilliseconds} ms");

                // 2. Has the spending guard paused anything?
                string budget = await FeedClient.GetText(Menu.backendUrl.TrimEnd('/') + "/budget");
                bool paused = budget != null && budget.Contains("\"all\":true");
                Note("service not paused", !paused, paused ? "the free-tier guard paused it" : "");

                // 3. Are we in a room? Everything below needs one.
                bool inRoom = Presence != null && Presence.RoomHash != null;
                Note("in a room", inRoom, inRoom ? Presence.RoomName : "join any room, even a private one");
                if (!inRoom) return;

                // 4. Is our own live connection up?
                Note("live connection", Presence.IsLive, Presence.IsLive ? "pushing instantly" : "using the slower fallback");

                // 5. A second connection stands in for another player.
                string ghostHash = PresenceClient.Hash("assetbay-selftest:" + Presence.PlayerHash);
                string live = Menu.backendUrl.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/');
                clock.Restart();
                ghost = new LiveLink(new Uri($"{live}/live?room={Presence.RoomHash}&player={ghostHash}"));
                bool connected = await Wait(() => ghost.Connected || ghost.Dead, 8f) && ghost.Connected;
                Note("second player can join", connected, connected ? $"{clock.ElapsedMilliseconds} ms" : ghost.Error ?? "timed out");
                if (!connected) return;

                // 6. Does our menu reach them? Nudge our state and watch for it.
                Menu.Presence.TouchState();
                bool sawUs = await Wait(() => Seen(ghost, "\"member\"") && Seen(ghost, Presence.PlayerHash), 6f);
                Note("others see your menu", sawUs, sawUs ? "" : "your menu state didn't arrive");

                // 7. Do presses come back? Allow control for the test only.
                if (controlBefore == ControlLevel.Off) Menu.SetRemoteControl(ControlLevel.Browse);
                await Task.Delay(400);
                received = false;
                Menu.Presence.CommandReceived += OnCommand;
                clock.Restart();
                ghost.Send($"{{\"t\":\"control\",\"target\":\"{Presence.PlayerHash}\",\"cmd\":{{\"a\":\"ping\"}}}}");
                bool pressOk = await Wait(() => received, 6f);
                Note("presses reach your menu", pressOk, pressOk ? $"{clock.ElapsedMilliseconds} ms" : "nothing arrived");
            }
            catch (Exception e)
            {
                Note("unexpected problem", false, e.Message);
                Debug.LogException(e);
            }
            finally
            {
                Menu.Presence.CommandReceived -= OnCommand;
                ghost?.Dispose();
                if (Menu.RemoteControl != controlBefore) Menu.SetRemoteControl(controlBefore);
                Summary = failures == 0 ? $"all good ({Results.Count} checks)" : $"{failures} of {Results.Count} checks failed";
                Menu.Toast("Network test: " + Summary, failures == 0 ? ToastKind.Success : ToastKind.Error);
                Running = false;
                Menu.RefreshNow();
            }
        }

        private bool received;

        // The test sends a command the menu ignores ("ping"), so nothing on screen changes.
        private void OnCommand(string from, ControlCmd cmd) => received = true;

        private static bool Seen(LiveLink link, string fragment)
        {
            while (link.Inbox.TryDequeue(out var text)) link.Seen.Add(text);
            foreach (var line in link.Seen) if (line.Contains(fragment)) return true;
            return false;
        }

        private static async Task<bool> Wait(Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                if (done()) return true;
                await UnityAsync.Delay(0.05f);
            }
            return done();
        }
    }
}
