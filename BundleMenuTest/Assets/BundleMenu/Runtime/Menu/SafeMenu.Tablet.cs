using System;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>Safe mode "tablet" tab: a test bench for the video tablet.</summary>
    public sealed partial class SafeMenu
    {
        private const string TestVideo = "videos/tablet-test.mp4";
        private string playUrl = "";
        private float seekDrag = -1f;

        /// <summary>Open the tablet and play the built-in test clip (timer + beeps) from the server.</summary>
        public static void PlayTestVideo(BundleMenuController menu)
        {
            var feed = menu.Feed;
            var listed = feed?.Current?.videos != null ? Array.Find(feed.Current.videos, v => v.video == TestVideo) : null;
            menu.Tablet.Play(feed.MediaUrl(TestVideo), listed?.title ?? "Tablet test");
            menu.RefreshNow();
        }

        private void DrawTabletTab()
        {
            var tablet = Menu.Tablet;
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(250));
            Section("test videos");
            if (Button("play test video")) PlayTestVideo(Menu);
            var videos = Menu.Feed?.Current?.videos;
            if (videos == null || videos.Length == 0) GUILayout.Label("Feed has no videos yet (check feed now on the online tab).", sDimWrap);
            else foreach (var v in videos) if (Button(v.title ?? v.id)) { tablet.Play(Menu.Feed.MediaUrl(v.video), v.title ?? v.id); Menu.RefreshNow(); }
            EndSection();

            Section("play any video");
            playUrl = GUILayout.TextField(playUrl, 400, sField);
            GUI.enabled = playUrl.Trim().Length > 0;
            if (Button("play on tablet")) { tablet.PlayAny(playUrl); Menu.RefreshNow(); }
            GUI.enabled = true;
            GUILayout.Label("An https link to an .mp4 / .webm, or a file on this PC like C:\\Videos\\clip.mp4", sDimWrap);
            EndSection();
            GUILayout.EndVertical();

            GUILayout.Space(8);
            GUILayout.BeginVertical();
            Section("now playing");
            string state = !tablet.IsOpen ? "tablet put away"
                : !string.IsNullOrEmpty(tablet.LastError) ? "error: " + tablet.LastError
                : tablet.IsPreparing ? "loading..."
                : tablet.IsPlaying ? "playing" : "paused";
            Value("video", tablet.NowPlaying ?? "-");
            Value("state", state);
            var size = tablet.VideoSize;
            if (size.x > 0) Value("resolution", $"{size.x} x {size.y}");

            double len = tablet.PlayLength;
            Value("time", $"{Clock(tablet.PlayTime)} / {Clock(len)}");
            if (len > 0)
            {
                // Drag to seek; the tablet only jumps when you let go, so scrubbing doesn't stutter.
                float shown = seekDrag >= 0f ? seekDrag : (float)tablet.PlayTime;
                float next = GUILayout.HorizontalSlider(shown, 0f, (float)len);
                if (Math.Abs(next - shown) > 0.01f) seekDrag = next;
                if (seekDrag >= 0f && Event.current.rawType == EventType.MouseUp) { tablet.Seek(seekDrag); seekDrag = -1f; }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(tablet.IsPlaying ? "pause" : "play", sButton)) tablet.TogglePause();
            if (GUILayout.Button("-10s", sButton)) tablet.Seek(tablet.PlayTime - 10);
            if (GUILayout.Button("+10s", sButton)) tablet.Seek(tablet.PlayTime + 10);
            if (GUILayout.Button("restart", sButton)) tablet.Restart();
            GUILayout.EndHorizontal();
            if (Check("loop", tablet.Loop)) tablet.SetLoop(!tablet.Loop);

            GUILayout.BeginHorizontal();
            GUILayout.Label("volume", sLabel, GUILayout.Width(80));
            float vol = GUILayout.HorizontalSlider(tablet.Volume, 0f, 1f);
            if (Math.Abs(vol - tablet.Volume) > 0.001f) tablet.SetVolume(vol);
            GUILayout.Label($"{tablet.Volume * 100f:0}%", sValue, GUILayout.Width(40));
            GUILayout.EndHorizontal();
            EndSection();

            Section("tablet");
            Cycle("style", tablet.Style.name, tablet.CycleStyle);
            Cycle("size", $"x{tablet.SizeMultiplier:0.#}", tablet.CycleSize);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(tablet.IsOpen ? "bring in front" : "open", sButton)) tablet.Recall();
            if (GUILayout.Button("throw", sButton)) tablet.Throw();
            if (GUILayout.Button("put away", sButton)) tablet.Close();
            GUILayout.EndHorizontal();
            EndSection();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private static string Clock(double seconds) =>
            seconds <= 0 || double.IsNaN(seconds) ? "0:00" : $"{(int)(seconds / 60)}:{(int)(seconds % 60):00}";
    }
}
