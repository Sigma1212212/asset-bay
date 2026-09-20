using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>
    /// Other Asset Bay users' menus, floating next to them:
    ///   - menu closed: a glowing dot above their head
    ///   - menu open: a card in their colours with their page and tablet video
    ///   - menu open AND they allow control: a live copy of their page you can click / poke; each press is
    ///     sent to their menu through the presence service (they see who pressed what).
    /// Positions follow their avatar from the game itself; everything here is local to your screen.
    /// </summary>
    public sealed class RemoteMenus : MonoBehaviour
    {
        public PresenceClient Presence;
        public Func<Camera> ViewCamera;
        public Func<MenuTheme> Theme;
        public Action<string> Report;

        /// <summary>Pressable buttons on shared menus (for mouse and finger poke).</summary>
        public readonly List<MenuButton> Buttons = new List<MenuButton>();
        /// <summary>True while at least one controllable menu is on screen (speeds up syncing).</summary>
        public bool ControllableVisible { get; private set; }

        private readonly Dictionary<Transform, Card> cards = new Dictionary<Transform, Card>();
        private readonly HashSet<Transform> seen = new HashSet<Transform>();
        private readonly List<Transform> dropped = new List<Transform>();

        /// <summary>Stop drawing menus further away than this (metres).</summary>
        public float MaxDistance = 30f;

        private const float W = 300f, RowH = 34f, Gap = 5f, Pad = 12f;

        private sealed class Card
        {
            public GameObject Root;
            public RectTransform Panel, Mirror;
            public Image Background, Rim, Dot;
            public TextMeshProUGUI Title, Detail;
            public float Open;
            public MenuState Shown;   // the state object the mirror was built from (compared by reference)
            public MenuTheme Look;
            public readonly List<MenuButton> Buttons = new List<MenuButton>();
        }

        // Runs every frame so the panels track their owner smoothly. The per-frame work is deliberately
        // tiny: move a transform, and only touch the contents when that player's menu actually changed.
        private void LateUpdate()
        {
            var cam = ViewCamera?.Invoke();
            seen.Clear();
            Buttons.Clear();
            ControllableVisible = false;

            if (Presence != null && cam != null)
            {
                var camPos = cam.transform.position;
                var others = Presence.Others;
                for (int i = 0; i < others.Count; i++)
                {
                    var (player, state) = others[i];
                    if (player.Body == null) continue;
                    seen.Add(player.Body);

                    // Too far away, or behind you: keep the card but stop doing anything with it.
                    var toPlayer = player.Body.position - camPos;
                    bool near = toPlayer.sqrMagnitude < MaxDistance * MaxDistance;
                    if (!cards.TryGetValue(player.Body, out var card))
                    {
                        if (!near) continue;               // don't even build it until they're close
                        cards[player.Body] = card = Create();
                    }
                    if (!near)
                    {
                        if (card.Root.activeSelf) card.Root.SetActive(false);
                        continue;
                    }
                    if (!card.Root.activeSelf) card.Root.SetActive(true);
                    Apply(card, player, state, cam);
                }
            }

            dropped.Clear();
            foreach (var body in cards.Keys) if (body == null || !seen.Contains(body)) dropped.Add(body);
            for (int i = 0; i < dropped.Count; i++)
            {
                Destroy(cards[dropped[i]].Root);
                cards.Remove(dropped[i]);
            }
        }

        private static Card Create()
        {
            var go = new GameObject("[BundleMenu] Remote menu", typeof(RectTransform));
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(W, 150f);
            go.transform.localScale = Vector3.one * 0.0012f;

            var card = new Card { Root = go };
            card.Panel = UIFactory.Rect("Panel", go.transform);
            card.Panel.anchorMin = card.Panel.anchorMax = new Vector2(0.5f, 0f);
            card.Panel.pivot = new Vector2(0.5f, 0f);
            card.Panel.sizeDelta = new Vector2(W, 150f);
            card.Background = UIFactory.Image(card.Panel, "Background", UISprites.RoundedFill(18f), Color.black);
            card.Background.rectTransform.Stretch();
            card.Rim = UIFactory.Image(card.Panel, "Rim", UISprites.RoundedEdge(18f, 2f), Color.white);
            card.Rim.rectTransform.Stretch();

            var brand = UIFactory.Text(card.Panel, "Brand", null, 12, TextAlignmentOptions.TopLeft, new Color(1, 1, 1, 0.6f), FontStyles.Bold | FontStyles.UpperCase, 3f);
            brand.rectTransform.TopStrip(10f, 16f, Pad + 2f, Pad);
            brand.text = "Asset Bay";
            card.Title = UIFactory.Text(card.Panel, "Title", null, 24, TextAlignmentOptions.TopLeft, Color.white, FontStyles.Bold);
            card.Title.rectTransform.TopStrip(26f, 30f, Pad + 2f, Pad);
            card.Detail = UIFactory.Text(card.Panel, "Detail", null, 14, TextAlignmentOptions.TopLeft, new Color(1, 1, 1, 0.75f));
            card.Detail.rectTransform.TopStrip(56f, 20f, Pad + 2f, Pad);

            card.Mirror = UIFactory.Rect("Mirror", card.Panel);
            card.Mirror.anchorMin = new Vector2(0f, 0f);
            card.Mirror.anchorMax = new Vector2(1f, 1f);
            card.Mirror.offsetMin = new Vector2(Pad, Pad);
            card.Mirror.offsetMax = new Vector2(-Pad, -82f);

            card.Dot = UIFactory.Image(go.transform, "Dot", UISprites.Glyph(Icon.Dot), Color.white);
            card.Dot.rectTransform.sizeDelta = new Vector2(40f, 40f);
            return card;
        }

        private void Apply(Card card, GorillaTagPlayer.OtherPlayer player, MenuState state, Camera cam)
        {
            Color accent = Parse(state.accent, new Color(0.23f, 0.91f, 1f));
            Color panel = Parse(state.panel, new Color(0.06f, 0.07f, 0.13f));
            bool controllable = Controllable(state.control);
            // With control on, the card also appears while their menu is closed (so you can open it for them).
            bool show = state.open || controllable;

            float s = Mathf.Max(0.3f, player.Scale);
            card.Open = Mathf.MoveTowards(card.Open, show ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            float e = Ease.OutBack(card.Open, 1.6f);

            var basePos = player.Body.position + Vector3.up * (0.25f + 0.1f * card.Open) * s;
            var toCam = cam.transform.position - basePos;
            var side = Vector3.Cross(Vector3.up, toCam).normalized;
            card.Root.transform.position = basePos + side * 0.4f * s * card.Open;
            card.Root.transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
            card.Root.transform.localScale = Vector3.one * 0.0012f * s;

            card.Panel.gameObject.SetActive(card.Open > 0.01f);
            card.Panel.localScale = new Vector3(e, e, 1f);
            card.Background.color = new Color(panel.r, panel.g, panel.b, 0.94f);
            card.Rim.color = accent;
            card.Title.text = !state.open ? $"{player.Name}'s menu" : string.IsNullOrEmpty(state.page) ? state.theme ?? "Menu" : state.page;
            card.Detail.text = controllable
                ? $"{player.Name} lets you {(state.control == "full" ? "use everything" : state.control == "mods" ? "browse + use their mods" : "browse")}" + (string.IsNullOrEmpty(state.pg) ? "" : $" · page {state.pg}")
                : string.IsNullOrEmpty(state.video) ? $"{player.Name} · {state.theme}" + (string.IsNullOrEmpty(state.style) ? "" : $" / {state.style}")
                : $"Playing: {state.video}";

            // Mirror: rebuilt only when a new state arrives for them (the link replaces the object).
            if (!ReferenceEquals(card.Shown, state))
            {
                card.Shown = state;
                BuildMirror(card, player, state, accent, panel);
            }
            if (controllable && card.Open > 0.5f)
            {
                ControllableVisible = true;
                Buttons.AddRange(card.Buttons);
            }

            card.Dot.gameObject.SetActive(card.Open < 0.99f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f);
            card.Dot.color = new Color(accent.r, accent.g, accent.b, (1f - card.Open) * pulse);
            card.Dot.rectTransform.anchoredPosition = new Vector2(0f, 60f);
        }

        private void BuildMirror(Card card, GorillaTagPlayer.OtherPlayer player, MenuState state, Color accent, Color panel)
        {
            for (int i = card.Mirror.childCount - 1; i >= 0; i--) Destroy(card.Mirror.GetChild(i).gameObject);
            card.Buttons.Clear();

            bool controllable = Controllable(state.control);
            int rowCount = controllable && state.open && state.rows != null ? state.rows.Length : 0;
            float mirrorH = !controllable ? 0f : !state.open ? 44f : rowCount * (RowH + Gap) + 40f;
            card.Panel.sizeDelta = new Vector2(W, 82f + mirrorH + Pad);
            if (!controllable) return;

            card.Look = TintedTheme(accent, panel);
            if (!state.open)
            {
                var open = UIFactory.TextButton(card.Mirror, "Open", card.Look, $"Open {player.Name}'s menu", 16f, 10f);
                LayoutKit.At((RectTransform)open.transform, 0f, 0f, W - Pad * 2f, 40f);
                open.OnClick = () => Send(player, new ControlCmd { a = "open" });
                card.Buttons.Add(open);
                return;
            }

            float y = 0f, w = W - Pad * 2f;
            foreach (var row in state.rows)
            {
                string label = string.IsNullOrEmpty(row.v) ? row.l : $"{row.l}   <alpha=#99>{row.v}";
                var b = UIFactory.TextButton(card.Mirror, "Row " + row.k, card.Look, label, 15f, 8f, FontStyles.Normal, 0f);
                LayoutKit.At((RectTransform)b.transform, 0f, y, w, RowH);
                b.transform.Find("Label").GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
                b.IsOn = row.on;
                b.interactable = row.nav;
                if (!row.nav) b.GetComponent<Image>().color = card.Look.ButtonFill.WithAlpha(0.35f);
                string key = row.k, page = state.page;
                b.OnClick = () => Send(player, new ControlCmd { a = "row", page = page, key = key });
                card.Buttons.Add(b);
                y += RowH + Gap;
            }

            // Bottom bar: previous page · back · next page.
            float third = (w - Gap * 2f) / 3f;
            (string text, string action)[] bar = { ("<", "prev"), ("back", "back"), (">", "next") };
            for (int i = 0; i < bar.Length; i++)
            {
                var (text, action) = bar[i];
                var b = UIFactory.TextButton(card.Mirror, "Nav " + action, card.Look, text, 15f, 8f);
                LayoutKit.At((RectTransform)b.transform, i * (third + Gap), y + 2f, third, 32f);
                string page = state.page;
                b.OnClick = () => Send(player, new ControlCmd { a = action, page = page });
                card.Buttons.Add(b);
            }
        }

        private async void Send(GorillaTagPlayer.OtherPlayer player, ControlCmd cmd)
        {
            string problem = await Presence.SendControl(player, cmd);
            if (problem != null) Report?.Invoke($"Couldn't press on {player.Name}'s menu: {problem}");
        }

        /// <summary>Button styling in the other player's colours.</summary>
        private MenuTheme TintedTheme(Color accent, Color panel)
        {
            var t = ThemePresets.Create(ThemePreset.Halo);
            bool light = panel.r * 0.3f + panel.g * 0.59f + panel.b * 0.11f > 0.6f;
            t.ButtonFill = Color.Lerp(panel, light ? Color.black : Color.white, 0.12f).WithAlpha(0.95f);
            t.ButtonFillHover = Color.Lerp(panel, accent, 0.35f);
            t.ButtonFillPressed = Color.Lerp(panel, accent, 0.6f);
            t.ButtonEdge = accent.WithAlpha(0.35f);
            t.ButtonEdgeHover = accent;
            t.Accent = accent;
            t.Text = light ? new Color(0.1f, 0.1f, 0.12f) : Color.white;
            return t;
        }

        private static bool Controllable(string control) =>
            control == "browse" || control == "mods" || control == "full";

        private static Color Parse(string hex, Color fallback) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;

        private void OnDestroy()
        {
            foreach (var c in cards.Values) if (c.Root != null) Destroy(c.Root);
            cards.Clear();
        }
    }
}
