using System.Collections.Generic;

namespace BundleMenu
{
    /// <summary>
    /// Spotify in the menu: what's playing, the controls, your playlists and which device is playing.
    /// Sign in once from the launcher's Spotify page. Reading works on any account; play / pause / skip
    /// are Spotify's Premium-only controls, and the row says so if it's refused.
    /// </summary>
    public sealed class SpotifyPage : MenuPage
    {
        public override string Title => "Spotify";

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            var rows = new List<RowSpec>();
            var spotify = ctx.Spotify;
            if (spotify == null) { rows.Add(RowSpec.Info("no", "Not available here")); return rows; }

            if (!SpotifyAuth.SignedIn)
            {
                rows.Add(RowSpec.Message("signin", "Sign in on the launcher's Spotify page, then come back here."));
                rows.Add(new RowSpec { Key = "recheck", Label = "Check again", OnClick = () => { SpotifyAuth.Reload(force: true); ctx.RefreshNow(); } });
                return rows;
            }

            var state = spotify.State;
            rows.Add(new RowSpec
            {
                Key = "board", Label = ctx.SpotifyBoard.IsOpen ? "Hide the board" : "Show the board",
                Value = "floating dashboard", IsOn = ctx.SpotifyBoard.IsOpen,
                OnClick = () => { ctx.SpotifyBoard.Toggle(); ctx.RefreshNow(); },
            });
            rows.Add(new RowSpec
            {
                Key = "np", Label = string.IsNullOrEmpty(state.Track) ? "Nothing playing" : state.Track,
                Value = state.Artist, Light = state.Playing ? StatusLight.Ok : StatusLight.Idle, Interactable = false,
            });
            rows.Add(new RowSpec
            {
                Key = "playpause", Label = state.Playing ? "Pause" : "Play", IsOn = state.Playing,
                OnClick = () => { spotify.PlayPause().Forget(); ctx.RefreshNow(); },
            });
            rows.Add(new RowSpec { Key = "next", Label = "Next track", OnClick = () => { spotify.Next().Forget(); ctx.RefreshNow(); } });
            rows.Add(new RowSpec { Key = "prev", Label = "Previous track", OnClick = () => { spotify.Previous().Forget(); ctx.RefreshNow(); } });
            rows.Add(new RowSpec
            {
                Key = "shuffle", Label = "Shuffle", Value = state.Shuffle ? "on" : "off", IsOn = state.Shuffle,
                OnClick = () => { spotify.Shuffle(!state.Shuffle).Forget(); ctx.RefreshNow(); },
            });
            rows.Add(new RowSpec
            {
                Key = "repeat", Label = "Repeat", Value = state.Repeat,
                IsOn = state.Repeat != "off",
                OnClick = () => { spotify.Repeat(state.Repeat == "off" ? "context" : state.Repeat == "context" ? "track" : "off").Forget(); ctx.RefreshNow(); },
            });
            if (state.Volume >= 0)
                rows.Add(new RowSpec
                {
                    Key = "vol", Label = "Volume", Value = state.Volume + "%",
                    OnClick = () => { spotify.SetVolume(state.Volume + 10).Forget(); ctx.RefreshNow(); },
                    OnAltClick = () => { spotify.SetVolume(state.Volume - 10).Forget(); ctx.RefreshNow(); },
                });

            rows.Add(new RowSpec
            {
                Key = "playlists", Label = "Playlists", Value = spotify.Playlists.Count.ToString(), ShowChevron = true,
                OnClick = () => { spotify.RefreshPlaylistsAsync().Forget(); ctx.Navigate(new SpotifyListPage(false)); },
            });
            rows.Add(new RowSpec
            {
                Key = "devices", Label = "Play on", Value = string.IsNullOrEmpty(state.Device) ? "pick a device" : state.Device, ShowChevron = true,
                OnClick = () => { spotify.RefreshDevicesAsync().Forget(); ctx.Navigate(new SpotifyListPage(true)); },
            });
            if (!string.IsNullOrEmpty(spotify.LastError)) rows.Add(RowSpec.Message("err", spotify.LastError));
            return rows;
        }
    }

    /// <summary>Your playlists, or the devices Spotify can play on.</summary>
    public sealed class SpotifyListPage : MenuPage
    {
        private readonly bool devices;
        public SpotifyListPage(bool devices) => this.devices = devices;
        public override string Title => devices ? "Play on" : "Playlists";

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            var rows = new List<RowSpec>();
            var spotify = ctx.Spotify;
            var items = devices ? spotify.Devices : spotify.Playlists;
            if (items.Count == 0) rows.Add(RowSpec.Info("none", devices ? "No devices - open Spotify somewhere" : "No playlists found"));

            foreach (var entry in items)
            {
                var item = entry;
                rows.Add(new RowSpec
                {
                    Key = (devices ? "d:" : "p:") + item.Id, Label = item.Name, Value = item.Detail,
                    OnClick = () =>
                    {
                        if (devices) spotify.UseDevice(item.Id).Forget();
                        else spotify.PlayUri(item.Uri).Forget();
                        ctx.RefreshNow();
                    },
                });
            }
            rows.Add(new RowSpec
            {
                Key = "refresh", Label = "Refresh",
                OnClick = () => { (devices ? spotify.RefreshDevicesAsync() : spotify.RefreshPlaylistsAsync()).Forget(); ctx.RefreshNow(); },
            });
            return rows;
        }
    }
}
