using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>
    /// Draws other Asset Bay users' menus: a small panel in their theme next to them while their menu is open
    /// (their current page and tablet video), or a glowing dot above their head while it's closed.
    /// Positions follow their avatar from the game itself; everything is local to your screen.
    /// </summary>
    public sealed class RemoteMenus : MonoBehaviour
    {
        public PresenceClient Presence;
        public System.Func<Camera> ViewCamera;

        private readonly Dictionary<Transform, Card> cards = new Dictionary<Transform, Card>();

        private sealed class Card
        {
            public GameObject Root;
            public RectTransform Panel;
            public Image Background, Rim, Dot;
            public TextMeshProUGUI Title, Detail;
            public float Open; // 0..1 animation
        }

        private void LateUpdate()
        {
            var cam = ViewCamera?.Invoke();
            var seen = new HashSet<Transform>();

            if (Presence != null && cam != null)
            {
                foreach (var (player, state) in Presence.Others)
                {
                    if (player.Body == null) continue;
                    seen.Add(player.Body);
                    if (!cards.TryGetValue(player.Body, out var card)) cards[player.Body] = card = Create();
                    Apply(card, player, state, cam);
                }
            }

            foreach (var body in new List<Transform>(cards.Keys))
            {
                if (seen.Contains(body) && body != null) continue;
                Destroy(cards[body].Root);
                cards.Remove(body);
            }
        }

        private static Card Create()
        {
            var go = new GameObject("[BundleMenu] Remote menu", typeof(RectTransform));
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(260f, 150f);
            go.transform.localScale = Vector3.one * 0.0012f;

            var card = new Card { Root = go };
            card.Panel = UIFactory.Rect("Panel", go.transform).Stretch();
            card.Background = UIFactory.Image(card.Panel, "Background", UISprites.RoundedFill(18f), Color.black);
            card.Background.rectTransform.Stretch();
            card.Rim = UIFactory.Image(card.Panel, "Rim", UISprites.RoundedEdge(18f, 2f), Color.white);
            card.Rim.rectTransform.Stretch();

            var brand = UIFactory.Text(card.Panel, "Brand", null, 13, TextAlignmentOptions.TopLeft, new Color(1, 1, 1, 0.6f), FontStyles.Bold | FontStyles.UpperCase, 3f);
            brand.rectTransform.Stretch(16f, 12f, 16f, 110f);
            brand.text = "Asset Bay";
            card.Title = UIFactory.Text(card.Panel, "Title", null, 26, TextAlignmentOptions.TopLeft, Color.white, FontStyles.Bold);
            card.Title.rectTransform.Stretch(16f, 36f, 16f, 60f);
            card.Detail = UIFactory.Text(card.Panel, "Detail", null, 16, TextAlignmentOptions.TopLeft, new Color(1, 1, 1, 0.75f));
            card.Detail.rectTransform.Stretch(16f, 90f, 16f, 12f);

            card.Dot = UIFactory.Image(go.transform, "Dot", UISprites.Glyph(Icon.Dot), Color.white);
            card.Dot.rectTransform.sizeDelta = new Vector2(40f, 40f);
            return card;
        }

        private static void Apply(Card card, GorillaTagPlayer.OtherPlayer player, MenuState state, Camera cam)
        {
            Color accent = Parse(state.accent, new Color(0.23f, 0.91f, 1f));
            Color accent2 = Parse(state.accent2, accent);
            Color panel = Parse(state.panel, new Color(0.06f, 0.07f, 0.13f));

            float s = Mathf.Max(0.3f, player.Scale);
            card.Open = Mathf.MoveTowards(card.Open, state.open ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            float e = Ease.OutBack(card.Open, 1.6f);

            // Open: panel beside them at chest height. Closed: just a dot above their head.
            var basePos = player.Body.position + Vector3.up * (0.35f + 0.25f * card.Open) * s;
            var toCam = cam.transform.position - basePos;
            var side = Vector3.Cross(Vector3.up, toCam).normalized;
            card.Root.transform.position = basePos + side * 0.35f * s * card.Open;
            card.Root.transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
            card.Root.transform.localScale = Vector3.one * 0.0012f * s;

            card.Panel.gameObject.SetActive(card.Open > 0.01f);
            card.Panel.localScale = new Vector3(e, e, 1f);
            card.Background.color = new Color(panel.r, panel.g, panel.b, 0.92f);
            card.Rim.color = Color.Lerp(accent, accent2, 0.5f);
            card.Title.text = string.IsNullOrEmpty(state.page) ? state.theme ?? "Menu" : state.page;
            card.Title.color = Color.white;
            card.Detail.text = string.IsNullOrEmpty(state.video) ? $"{player.Name} · {state.theme}" + (string.IsNullOrEmpty(state.style) ? "" : $" / {state.style}") : $"Playing: {state.video}";

            card.Dot.gameObject.SetActive(card.Open < 0.99f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f);
            card.Dot.color = new Color(accent.r, accent.g, accent.b, (1f - card.Open) * pulse);
            card.Dot.rectTransform.anchoredPosition = new Vector2(0f, 60f);
        }

        private static Color Parse(string hex, Color fallback) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback;

        private void OnDestroy()
        {
            foreach (var c in cards.Values) if (c.Root != null) Destroy(c.Root);
            cards.Clear();
        }
    }
}
