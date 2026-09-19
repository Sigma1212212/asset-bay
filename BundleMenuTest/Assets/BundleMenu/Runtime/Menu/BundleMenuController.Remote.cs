using System;
using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>How much other Asset Bay users in your room may do with your menu.</summary>
    public enum ControlLevel
    {
        Off,     // look but don't touch (default)
        Browse,  // open/close, move between pages, videos on your tablet, theme and menu type
        Full,    // anything you could press yourself, including mods and bundles
    }

    /// <summary>
    /// "Let others use my menu". Deliberately never saved: every time the game starts it's Off again, so it
    /// can't be left on by accident. Presses arrive through the presence service and are re-checked here
    /// against the current level, the current page and the rows actually on screen before anything runs.
    /// </summary>
    public sealed partial class BundleMenuController
    {
        public ControlLevel RemoteControl { get; private set; } = ControlLevel.Off;

        private List<RowSpec> lastSlice = new List<RowSpec>();
        private string lastPaging = "";
        private float remoteToastUntil;

        private static readonly HashSet<string> BrowseKeys = new HashSet<string>
        {
            "theme", "menutype", "entrance", "stagger", "speed", "replay", "videos", "test", "style", "size",
        };

        public string RemoteControlName => RemoteControl switch
        {
            ControlLevel.Browse => "browse",
            ControlLevel.Full => "full control",
            _ => "off",
        };

        public void CycleRemoteControl(int dir = 1)
        {
            int n = Enum.GetValues(typeof(ControlLevel)).Length;
            SetRemoteControl((ControlLevel)((((int)RemoteControl + dir) % n + n) % n));
        }

        public void SetRemoteControl(ControlLevel level)
        {
            RemoteControl = level;
            Toast(level switch
            {
                ControlLevel.Browse => "Others here can browse your menu and use your tablet. Resets when the game restarts.",
                ControlLevel.Full => "Others here can press ANYTHING on your menu, mods included. Resets when the game restarts.",
                _ => "Nobody else can use your menu.",
            }, level == ControlLevel.Full ? ToastKind.Error : ToastKind.Info);
            dirty = true;
        }

        private void SetupRemoteControl()
        {
            if (Presence == null) return;
            Presence.ControlEndpoint = Feed.BaseUrl + "control";
            // Quicker round trips while someone could be pressing things (either direction).
            Presence.IntervalNow = () => RemoteControl != ControlLevel.Off || (Remote != null && Remote.ControllableVisible) ? 1.5f : PresenceClient.Interval;
            Presence.CommandReceived += HandleRemote;
        }

        /// <summary>Called from Render: remember what's on screen so the mirror and incoming presses match it.</summary>
        private void RememberSlice(List<RowSpec> slice, MenuPage page, int count)
        {
            lastSlice = slice;
            lastPaging = count > 1 ? $"{page.PageIndex + 1}/{count}" : "";
        }

        private MirrorRow[] MirrorRows()
        {
            if (RemoteControl == ControlLevel.Off || !IsOpen) return null;
            var rows = new List<MirrorRow>();
            foreach (var r in lastSlice)
            {
                if (rows.Count >= 8) break;
                rows.Add(new MirrorRow
                {
                    k = Clip(r.Key, 24), l = Clip(r.Label, 24), v = Clip(r.Value, 12),
                    on = r.IsOn, nav = r.Interactable && (r.OnClick != null) && Usable(r),
                });
            }
            return rows.ToArray();
        }

        /// <summary>May a remote user press this row at the current level?</summary>
        private bool Usable(RowSpec r)
        {
            if (RemoteControl == ControlLevel.Full) return true;
            if (RemoteControl == ControlLevel.Off) return false;
            var page = pages.Peek();
            return r.ShowChevron || page is VideosPage || BrowseKeys.Contains(r.Key) || r.Key.StartsWith("v:", StringComparison.Ordinal);
        }

        private void HandleRemote(string fromHash, ControlCmd cmd)
        {
            if (RemoteControl == ControlLevel.Off || cmd == null) return;
            string who = "Someone";
            if (Presence != null)
                foreach (var (p, _) in Presence.Others)
                    if (Presence.HashFor(p) == fromHash) { who = p.Name; break; }

            switch (cmd.a)
            {
                case "open": Open(); Tell($"{who} opened your menu"); return;
                case "close": Close(); Tell($"{who} closed your menu"); return;
            }
            if (!IsOpen) return;
            // Presses are for the page they were looking at; if it changed since, ignore rather than guess.
            if (cmd.page != pages.Peek().Title) return;

            switch (cmd.a)
            {
                case "back": Back(); Tell($"{who} went back"); return;
                case "home": Home(); return;
                case "next": Page(+1); return;
                case "prev": Page(-1); return;
                case "row":
                    var row = lastSlice.Find(r => r.Key == cmd.key);
                    if (row == null || !row.Interactable || row.OnClick == null) return;
                    if (!Usable(row)) { Tell($"{who} tried \"{row.Label}\" (needs full control)"); return; }
                    Tell($"{who} pressed \"{row.Label}\"");
                    row.OnClick();
                    dirty = true;
                    return;
            }
        }

        /// <summary>Toasts, but not a wall of them when someone clicks quickly.</summary>
        private void Tell(string message)
        {
            if (Time.unscaledTime < remoteToastUntil) return;
            remoteToastUntil = Time.unscaledTime + 0.8f;
            Toast(message, ToastKind.Info);
        }
    }
}
