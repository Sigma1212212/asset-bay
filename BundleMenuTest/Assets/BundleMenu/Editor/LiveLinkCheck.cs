using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BundleMenu.Editor
{
    /// <summary>
    /// Checks the live room connection from inside Unity's runtime (Mono's WebSocket + TLS), against the real
    /// server: two links join a random room, one presses a button on the other, and the round trip is timed.
    ///   Unity -batchmode -executeMethod BundleMenu.Editor.LiveLinkCheck.Run -quit
    /// </summary>
    public static class LiveLinkCheck
    {
        private const string Base = "wss://assetbay.randomthingsthatarecool.dev/asset-bay/live";

        public static void Run()
        {
            string room = PresenceClient.Hash("livecheck:" + Guid.NewGuid());
            string a = PresenceClient.Hash("a" + room), b = PresenceClient.Hash("b" + room);
            var clock = Stopwatch.StartNew();
            using (var linkA = new LinkHandle(new LiveLink(new Uri($"{Base}?room={room}&player={a}"))))
            using (var linkB = new LinkHandle(new LiveLink(new Uri($"{Base}?room={room}&player={b}"))))
            {
                bool ok = Wait(() => linkA.Link.Connected && linkB.Link.Connected, 10000);
                Debug.Log($"LIVECHECK: connected={ok} in {clock.ElapsedMilliseconds} ms  errA={linkA.Link.Error} errB={linkB.Link.Error}");
                if (!ok) return;

                linkA.Link.Send("{\"t\":\"state\",\"state\":{\"open\":true,\"control\":\"browse\"}}");
                ok = Wait(() => linkB.Has("\"member\""), 5000);
                Debug.Log($"LIVECHECK: state pushed to other link={ok}");

                clock.Restart();
                linkB.Link.Send($"{{\"t\":\"control\",\"target\":\"{a}\",\"cmd\":{{\"a\":\"row\",\"page\":\"Library\",\"key\":\"videos\"}}}}");
                ok = Wait(() => linkA.Has("\"cmd\""), 5000);
                Debug.Log($"LIVECHECK: press delivered={ok} in {clock.ElapsedMilliseconds} ms");
            }
            Debug.Log("LIVECHECK: done");
        }

        private static bool Wait(Func<bool> condition, int ms)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms) { if (condition()) return true; Thread.Sleep(10); }
            return condition();
        }

        /// <summary>Keeps everything a link received so the checks can look through it.</summary>
        private sealed class LinkHandle : IDisposable
        {
            public readonly LiveLink Link;
            private readonly System.Text.StringBuilder seen = new System.Text.StringBuilder();
            public LinkHandle(LiveLink link) { Link = link; }
            public bool Has(string fragment)
            {
                while (Link.Inbox.TryDequeue(out var m)) seen.AppendLine(m);
                return seen.ToString().Contains(fragment);
            }
            public void Dispose() => Link.Dispose();
        }
    }
}
