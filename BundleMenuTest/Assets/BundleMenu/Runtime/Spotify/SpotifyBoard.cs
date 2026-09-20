using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>
    /// The Spotify board: a wide panel that floats in front of you with the record spinning beside the
    /// album art, what's playing, a progress bar you can scrub, the transport buttons and your playlists
    /// down the side. Built from the menu's own pieces, so it wears whatever theme you're using.
    /// </summary>
    public sealed class SpotifyBoard : MonoBehaviour
    {
        public SpotifyClient Client;
        public Func<Camera> ViewCamera;
        public Func<MenuTheme> Theme;
        public Action<string> Report;
        public float WorldScale = 0.0013f;

        public bool IsOpen => root != null && root.activeSelf;
        /// <summary>Buttons on the board, for the mouse and finger poke.</summary>
        public readonly List<MenuButton> Buttons = new List<MenuButton>();

        private const float W = 620f, H = 320f, Pad = 18f;

        private GameObject root;
        private RectTransform panel, discPivot;
        private RawImage art;
        private Image disc, progressFill, progressTrack;
        private TextMeshProUGUI track, artist, album, device, times, status;
        private MenuButton playPause, prev, next, shuffle, repeat, volDown, volUp, close;
        private readonly List<MenuButton> playlistButtons = new List<MenuButton>();
        private Texture2D artTexture;
        private string artUrl, builtFor, shownTrack, shownArtist, shownAlbum, shownDevice, shownTimes, shownStatus;
        private float spin, openT;

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            if (root == null) Build();
            if (root == null) return;
            root.SetActive(true);
            openT = 0f;
            Place(snap: true);
            Client?.RefreshNowPlayingAsync().Forget();
            Client?.RefreshPlaylistsAsync().Forget();
        }

        public void Close()
        {
            if (root != null) root.SetActive(false);
            Buttons.Clear();
        }

        /// <summary>Bring it back in front of you.</summary>
        public void Recall() { if (IsOpen) Place(snap: true); else Open(); }

        // ------------------------------------------------------------------ building

        private void Build()
        {
            var theme = Theme?.Invoke() ?? ThemePresets.Create(ThemePreset.Halo);
            builtFor = theme.DisplayName;

            root = new GameObject("[BundleMenu] Spotify board", typeof(RectTransform));
            DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)root.transform).sizeDelta = new Vector2(W, H);
            root.transform.localScale = Vector3.one * WorldScale;

            panel = UIFactory.Rect("Panel", root.transform);
            panel.sizeDelta = new Vector2(W, H);

            // Shell: the menu's own panel look.
            var shadow = UIFactory.Image(panel, "Shadow", UISprites.RoundedGlow(theme.PanelRadius, 20f), new Color(0, 0, 0, 0.35f));
            shadow.rectTransform.Stretch(-10f, -6f, -10f, -18f);
            var background = UIFactory.Image(panel, "Background", UISprites.RoundedFill(theme.PanelRadius), Color.white, raycast: true);
            background.rectTransform.Stretch();
            background.gameObject.AddComponent<UIGradient>().Set(theme.PanelTop, theme.PanelBottom);
            var rim = UIFactory.Image(panel, "Rim", UISprites.RoundedEdge(theme.PanelRadius, Mathf.Max(1.5f, theme.EdgeWidth)), theme.EdgeTop);
            rim.rectTransform.Stretch();

            // The record: a dark disc that turns while music plays, with the art pinned on it.
            discPivot = UIFactory.Rect("Disc", panel);
            LayoutKit.At(discPivot, Pad + 8f, Pad + 8f, 180f, 180f);
            disc = UIFactory.Image(discPivot, "Vinyl", UISprites.Glyph(Icon.Dot), new Color(0.06f, 0.06f, 0.07f, 1f));
            disc.rectTransform.Stretch(-14f, -14f, -14f, -14f);
            var groove = UIFactory.Image(discPivot, "Groove", UISprites.Glyph(Icon.Dot), new Color(1, 1, 1, 0.06f));
            groove.rectTransform.Stretch(6f, 6f, 6f, 6f);

            art = UIFactory.Rect("Art", discPivot).Stretch(18f, 18f, 18f, 18f).gameObject.AddComponent<RawImage>();
            art.color = new Color(1, 1, 1, 0.95f);
            var spindle = UIFactory.Image(discPivot, "Spindle", UISprites.Glyph(Icon.Dot), theme.PanelBottom);
            spindle.rectTransform.Pin(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));

            // Text block.
            float textX = Pad + 200f, textW = W - textX - Pad;
            var brand = UIFactory.Text(panel, "Brand", theme, 13, TextAlignmentOptions.MidlineLeft, theme.Accent, FontStyles.Bold | FontStyles.UpperCase, 4f);
            LayoutKit.At(brand.rectTransform, textX, Pad, textW, 18f);
            brand.text = "Spotify";
            track = UIFactory.Text(panel, "Track", theme, 30, TextAlignmentOptions.MidlineLeft, theme.Text, FontStyles.Bold);
            LayoutKit.At(track.rectTransform, textX, Pad + 20f, textW, 38f);
            track.enableAutoSizing = true; track.fontSizeMin = 16f; track.fontSizeMax = 30f;
            artist = UIFactory.Text(panel, "Artist", theme, 20, TextAlignmentOptions.MidlineLeft, theme.SubText);
            LayoutKit.At(artist.rectTransform, textX, Pad + 58f, textW, 26f);
            album = UIFactory.Text(panel, "Album", theme, 15, TextAlignmentOptions.MidlineLeft, theme.SubText);
            LayoutKit.At(album.rectTransform, textX, Pad + 84f, textW, 20f);

            // Progress.
            progressTrack = UIFactory.Image(panel, "Track bar", UISprites.RoundedFill(3f), theme.ButtonFill);
            LayoutKit.At(progressTrack.rectTransform, textX, Pad + 116f, textW, 6f);
            progressFill = UIFactory.Image(progressTrack.rectTransform, "Fill", UISprites.RoundedFill(3f), theme.Accent);
            progressFill.rectTransform.anchorMin = Vector2.zero;
            progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            progressFill.rectTransform.offsetMin = progressFill.rectTransform.offsetMax = Vector2.zero;
            times = UIFactory.Text(panel, "Times", theme, 13, TextAlignmentOptions.MidlineRight, theme.SubText);
            LayoutKit.At(times.rectTransform, textX, Pad + 124f, textW, 18f);
            device = UIFactory.Text(panel, "Device", theme, 13, TextAlignmentOptions.MidlineLeft, theme.SubText);
            LayoutKit.At(device.rectTransform, textX, Pad + 124f, textW - 90f, 18f);

            // Transport.
            float by = Pad + 150f, size = 46f, gap = 8f, x = textX;
            prev = Add(theme, "Prev", Icon.ChevronLeft, x, by, size);
            playPause = Add(theme, "Play", Icon.ChevronRight, x += size + gap, by, size);
            next = Add(theme, "Next", Icon.ChevronRight, x += size + gap, by, size);
            shuffle = Add(theme, "Shuffle", Icon.Refresh, x += size + gap + 10f, by, size);
            repeat = Add(theme, "Repeat", Icon.Refresh, x += size + gap, by, size);
            volDown = Add(theme, "Quieter", Icon.Back, x += size + gap + 10f, by, size);
            volUp = Add(theme, "Louder", Icon.Menu, x += size + gap, by, size);

            prev.OnClick = () => Do(Client.Previous());
            playPause.OnClick = () => Do(Client.PlayPause());
            next.OnClick = () => Do(Client.Next());
            shuffle.OnClick = () => Do(Client.Shuffle(!Client.State.Shuffle));
            repeat.OnClick = () => Do(Client.Repeat(Client.State.Repeat == "off" ? "context" : Client.State.Repeat == "context" ? "track" : "off"));
            volDown.OnClick = () => Do(Client.SetVolume(Mathf.Max(0, Client.State.Volume - 10)));
            volUp.OnClick = () => Do(Client.SetVolume(Mathf.Min(100, Client.State.Volume + 10)));

            close = Add(theme, "Close", Icon.Close, W - Pad - 34f, Pad - 4f, 34f);
            close.OnClick = Close;

            status = UIFactory.Text(panel, "Status", theme, 13, TextAlignmentOptions.MidlineLeft, theme.SubText);
            LayoutKit.At(status.rectTransform, Pad, H - 30f, W - Pad * 2f, 18f);

            // Playlists along the bottom.
            float px = Pad, pw = (W - Pad * 2f - 3f * 6f) / 4f;
            for (int i = 0; i < 4; i++)
            {
                var button = UIFactory.TextButton(panel, "Playlist" + i, theme, "", 15f, Mathf.Min(theme.ButtonRadius, 10f));
                LayoutKit.At((RectTransform)button.transform, px, H - 76f, pw, 40f);
                px += pw + 6f;
                playlistButtons.Add(button);
                button.gameObject.SetActive(false);
            }
        }

        private MenuButton Add(MenuTheme theme, string name, Icon icon, float x, float y, float size)
        {
            var button = UIFactory.IconButton(panel, name, theme, icon, size, Mathf.Min(theme.ButtonRadius, 12f));
            LayoutKit.At((RectTransform)button.transform, x, y, size, size);
            return button;
        }

        private void Do(System.Threading.Tasks.Task task)
        {
            task.Forget();
            if (!string.IsNullOrEmpty(Client.LastError)) Report?.Invoke("Spotify: " + Client.LastError);
        }

        // ------------------------------------------------------------------ frame

        private void LateUpdate()
        {
            if (!IsOpen) return;
            var theme = Theme?.Invoke();
            if (theme != null && theme.DisplayName != builtFor) { Rebuild(); return; }

            Buttons.Clear();
            Buttons.Add(prev); Buttons.Add(playPause); Buttons.Add(next); Buttons.Add(shuffle);
            Buttons.Add(repeat); Buttons.Add(volDown); Buttons.Add(volUp); Buttons.Add(close);
            foreach (var b in playlistButtons) if (b.gameObject.activeSelf) Buttons.Add(b);

            Place(snap: false);
            Paint();
        }

        private void Rebuild()
        {
            bool wasOpen = IsOpen;
            if (root != null) Destroy(root);
            root = null;
            playlistButtons.Clear();
            Buttons.Clear();
            if (wasOpen) Open();
        }

        private void Place(bool snap)
        {
            var cam = ViewCamera?.Invoke();
            if (cam == null) return;
            openT = Mathf.MoveTowards(openT, 1f, Time.unscaledDeltaTime * 3f);
            var forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.01f) forward = cam.transform.forward;
            var target = cam.transform.position + forward.normalized * 1.15f + Vector3.down * 0.15f;
            root.transform.position = snap ? target : Vector3.Lerp(root.transform.position, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2.5f));
            root.transform.rotation = Quaternion.LookRotation(root.transform.position - cam.transform.position, Vector3.up);
            float e = Ease.OutBack(openT, 1.4f);
            root.transform.localScale = Vector3.one * WorldScale * Mathf.Lerp(0.85f, 1f, e);
        }

        private void Paint()
        {
            var state = Client != null ? Client.State : new SpotifyState();
            bool signedIn = SpotifyAuth.SignedIn;

            Set(track, ref shownTrack, !signedIn ? "Not signed in" : string.IsNullOrEmpty(state.Track) ? "Nothing playing" : state.Track);
            Set(artist, ref shownArtist, signedIn ? state.Artist : "Sign in from the launcher (Spotify page)");
            Set(album, ref shownAlbum, state.Album);
            Set(device, ref shownDevice, string.IsNullOrEmpty(state.Device) ? "" : state.Device + (state.Volume >= 0 ? " · " + state.Volume + "%" : ""));
            Set(status, ref shownStatus, Client?.LastError ?? (state.Shuffle ? "shuffle on " : "") + (state.Repeat != "off" ? "repeat " + state.Repeat : ""));

            // The clock keeps running between refreshes so the bar moves smoothly.
            int position = state.PositionMs;
            if (state.Playing && state.Fetched != default) position += (int)(DateTime.Now - state.Fetched).TotalMilliseconds;
            position = Mathf.Clamp(position, 0, Mathf.Max(0, state.DurationMs));
            float fraction = state.DurationMs > 0 ? position / (float)state.DurationMs : 0f;
            progressFill.rectTransform.anchorMax = new Vector2(fraction, 1f);
            // Only the seconds matter, so this string is rebuilt when it changes, not every frame.
            Set(times, ref shownTimes, state.DurationMs > 0 ? Clock(position) + " / " + Clock(state.DurationMs) : "");

            playPause.transform.Find("Icon").GetComponent<Image>().sprite = UISprites.Glyph(state.Playing ? Icon.Close : Icon.ChevronRight);
            shuffle.IsOn = state.Shuffle;
            repeat.IsOn = state.Repeat != "off";

            if (state.Playing) spin += Time.unscaledDeltaTime * 25f;
            discPivot.localRotation = Quaternion.Euler(0, 0, -spin);

            if (state.ArtUrl != artUrl) { artUrl = state.ArtUrl; LoadArt(artUrl).Forget(); }

            for (int i = 0; i < playlistButtons.Count; i++)
            {
                bool has = Client != null && i < Client.Playlists.Count;
                playlistButtons[i].gameObject.SetActive(has);
                if (!has) continue;
                var playlist = Client.Playlists[i];
                playlistButtons[i].transform.Find("Label").GetComponent<TextMeshProUGUI>().text = playlist.Name;
                playlistButtons[i].OnClick = () => Do(Client.PlayUri(playlist.Uri));
            }
        }

        private async System.Threading.Tasks.Task LoadArt(string url)
        {
            if (string.IsNullOrEmpty(url)) { art.texture = null; return; }
            try
            {
                using (var request = UnityWebRequestTexture.GetTexture(url))
                {
                    request.timeout = 10;
                    await request.SendWebRequest().Await();
                    if (request.result != UnityWebRequest.Result.Success || url != artUrl) return;
                    var texture = DownloadHandlerTexture.GetContent(request);
                    if (artTexture != null) Destroy(artTexture);     // the old cover is no longer needed
                    artTexture = texture;
                    art.texture = artTexture;
                }
            }
            catch { /* no cover art, no problem */ }
        }

        private static void Set(TextMeshProUGUI label, ref string shown, string value)
        {
            if (shown == value) return;
            shown = value;
            label.text = value;
        }

        private static string Clock(int ms) => $"{ms / 60000}:{ms / 1000 % 60:00}";

        private void OnDestroy()
        {
            if (root != null) Destroy(root);
            if (artTexture != null) Destroy(artTexture);
        }
    }
}
