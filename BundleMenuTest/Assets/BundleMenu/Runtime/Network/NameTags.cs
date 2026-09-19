using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>
    /// Badges above players' heads. Two sources:
    ///   - shared: your own trait travels with your menu state, so every Asset Bay user sees it above you
    ///   - local:  a tag you pin on anyone in the room; only you see it, and it's kept on this PC only
    /// Built to be cheap: one small canvas per tagged player, reused, only re-textured when the text or
    /// colour changes, hidden past a distance, and never allocating during a normal frame.
    /// </summary>
    public sealed class NameTags : MonoBehaviour
    {
        public PresenceClient Presence;
        public Func<Camera> ViewCamera;
        public bool Enabled = true;
        public bool ShowOthersShared = true;   // tags other people set for themselves
        public float MaxDistance = 40f;

        private readonly Dictionary<Transform, Badge> badges = new Dictionary<Transform, Badge>();
        private readonly HashSet<Transform> seen = new HashSet<Transform>();
        private readonly List<Transform> dropped = new List<Transform>();

        /// <summary>One badge: root canvas, background, rim and label.</summary>
        public sealed class Badge
        {
            public GameObject Root;
            public RectTransform Panel;
            public Image Background, Rim;
            public TextMeshProUGUI Label;
            public string Text;
            public Color Colour;
            public float Show;
        }

        private void LateUpdate()
        {
            var cam = ViewCamera?.Invoke();
            seen.Clear();
            if (Enabled && cam != null && Presence != null)
            {
                var camPos = cam.transform.position;
                var roster = Presence.Roster;
                for (int i = 0; i < roster.Count; i++)
                {
                    var p = roster[i];
                    if (p.Body == null) continue;

                    string text = TagStore.LocalTag(p.UserId, out var colour);
                    if (text == null && ShowOthersShared)
                    {
                        var state = Presence.StateFor(p);
                        if (!string.IsNullOrEmpty(state?.tag))
                        {
                            text = state.tag;
                            colour = TagStore.Parse(state.tagc);
                        }
                    }
                    if (string.IsNullOrEmpty(text)) continue;

                    seen.Add(p.Body);
                    if ((p.Body.position - camPos).sqrMagnitude > MaxDistance * MaxDistance) { Hide(p.Body); continue; }
                    if (!badges.TryGetValue(p.Body, out var badge)) badges[p.Body] = badge = Create();
                    Apply(badge, p, text, colour, cam);
                }
            }

            dropped.Clear();
            foreach (var kv in badges) if (kv.Key == null || !seen.Contains(kv.Key)) dropped.Add(kv.Key);
            for (int i = 0; i < dropped.Count; i++)
            {
                Destroy(badges[dropped[i]].Root);
                badges.Remove(dropped[i]);
            }
        }

        private void Hide(Transform body)
        {
            if (badges.TryGetValue(body, out var b) && b.Root.activeSelf) b.Root.SetActive(false);
        }

        /// <summary>Build an unattached badge (the preview tool uses this too).</summary>
        public static Badge Create()
        {
            var go = new GameObject("[BundleMenu] Tag", typeof(RectTransform));
            if (Application.isPlaying) DontDestroyOnLoad(go); // survives scene loads in game; not allowed in the editor
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(220f, 52f);
            go.transform.localScale = Vector3.one * 0.0016f;

            var badge = new Badge { Root = go };
            badge.Panel = UIFactory.Rect("Badge", go.transform).Stretch();
            badge.Background = UIFactory.Image(badge.Panel, "Background", UISprites.RoundedFill(14f), new Color(0, 0, 0, 0.72f));
            badge.Background.rectTransform.Stretch();
            badge.Rim = UIFactory.Image(badge.Panel, "Rim", UISprites.RoundedEdge(14f, 2.5f), Color.white);
            badge.Rim.rectTransform.Stretch();
            badge.Label = UIFactory.Text(badge.Panel, "Label", null, 26, TextAlignmentOptions.Center, Color.white,
                FontStyles.Bold | FontStyles.UpperCase, 4f);
            badge.Label.rectTransform.Stretch(10f, 4f, 10f, 4f);
            badge.Label.enableAutoSizing = true;
            badge.Label.fontSizeMin = 14f;
            badge.Label.fontSizeMax = 26f;
            return badge;
        }

        private static void Apply(Badge badge, GorillaTagPlayer.OtherPlayer p, string text, Color colour, Camera cam)
        {
            if (!badge.Root.activeSelf) badge.Root.SetActive(true);

            // Text and colours only change when the tag does.
            if (text != badge.Text)
            {
                badge.Text = text;
                badge.Label.text = text;
            }
            if (colour != badge.Colour)
            {
                badge.Colour = colour;
                badge.Rim.color = colour;
                badge.Label.color = colour;
            }

            float s = Mathf.Max(0.3f, p.Scale);
            var pos = p.Body.position + Vector3.up * (1.05f * s);
            badge.Root.transform.position = pos;
            badge.Root.transform.rotation = Quaternion.LookRotation(pos - cam.transform.position, Vector3.up);

            // Grow a little with distance so it stays readable across the map, but never bigger than close up.
            float distance = Vector3.Distance(pos, cam.transform.position);
            float scale = 0.0016f * s * Mathf.Clamp(distance / 6f, 1f, 3.5f);
            badge.Show = Mathf.MoveTowards(badge.Show, 1f, Time.unscaledDeltaTime * 5f);
            badge.Root.transform.localScale = Vector3.one * scale * Mathf.Lerp(0.7f, 1f, badge.Show);
        }

        private void OnDestroy()
        {
            foreach (var b in badges.Values) if (b.Root != null) Destroy(b.Root);
            badges.Clear();
        }
    }
}
