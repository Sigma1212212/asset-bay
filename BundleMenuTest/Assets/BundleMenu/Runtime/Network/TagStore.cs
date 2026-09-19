using System;
using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Tags: your own trait (shared with other Asset Bay users) and tags you pin on other players
    /// (kept on this PC only - the ids never leave it). Everything lives in PlayerPrefs.
    /// </summary>
    public static class TagStore
    {
        public const int MaxLength = 14;
        private const string MyTagKey = "BundleMenu.mytag", MyColourKey = "BundleMenu.mytagc", LocalKey = "BundleMenu.tags";

        /// <summary>Ready-made traits, in the order the menu cycles them.</summary>
        public static readonly string[] Presets =
        {
            "", "ADMIN", "OWNER", "DEV", "MOD", "VIP", "FRIEND", "GUEST", "NEW", "AFK", "BUILDER", "TESTER",
        };

        public static readonly (string name, string hex)[] Colours =
        {
            ("Cyan", "#3BE8FF"), ("Green", "#45E08A"), ("Yellow", "#FFD23F"), ("Orange", "#FF7A1A"),
            ("Red", "#FF4D6A"), ("Pink", "#FF5FA8"), ("Violet", "#8A5CFF"), ("White", "#FFFFFF"),
        };

        // ------------------------------------------------------------------ your own trait

        public static string MyTag
        {
            get => PlayerPrefs.GetString(MyTagKey, "");
            set { PlayerPrefs.SetString(MyTagKey, Clean(value)); PlayerPrefs.Save(); }
        }

        public static string MyColourHex
        {
            get => PlayerPrefs.GetString(MyColourKey, Colours[0].hex);
            set { PlayerPrefs.SetString(MyColourKey, value); PlayerPrefs.Save(); }
        }

        public static Color MyColour => Parse(MyColourHex);

        public static void CycleMyTag(int dir)
        {
            int i = Array.IndexOf(Presets, MyTag);
            MyTag = Presets[(((i < 0 ? 0 : i) + dir) % Presets.Length + Presets.Length) % Presets.Length];
        }

        public static void CycleMyColour(int dir)
        {
            int i = Array.FindIndex(Colours, c => c.hex.Equals(MyColourHex, StringComparison.OrdinalIgnoreCase));
            MyColourHex = Colours[(((i < 0 ? 0 : i) + dir) % Colours.Length + Colours.Length) % Colours.Length].hex;
        }

        // ------------------------------------------------------------------ tags you pin on other players

        [Serializable] private sealed class Entry { public string id, text, colour; }
        [Serializable] private sealed class Book { public List<Entry> entries = new List<Entry>(); }

        private static Book book;

        private static Book Load()
        {
            if (book != null) return book;
            try { book = JsonUtility.FromJson<Book>(PlayerPrefs.GetString(LocalKey, "")) ?? new Book(); }
            catch { book = new Book(); }
            return book;
        }

        private static void Save()
        {
            PlayerPrefs.SetString(LocalKey, JsonUtility.ToJson(Load()));
            PlayerPrefs.Save();
        }

        /// <summary>The tag you pinned on this player, or null.</summary>
        public static string LocalTag(string userId, out Color colour)
        {
            colour = Color.white;
            if (string.IsNullOrEmpty(userId)) return null;
            var entries = Load().entries;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].id == userId)
                {
                    colour = Parse(entries[i].colour);
                    return string.IsNullOrEmpty(entries[i].text) ? null : entries[i].text;
                }
            return null;
        }

        public static string LocalTag(string userId) => LocalTag(userId, out _);

        public static void SetLocalTag(string userId, string text, string colourHex = null)
        {
            if (string.IsNullOrEmpty(userId)) return;
            text = Clean(text);
            var entries = Load().entries;
            int at = entries.FindIndex(e => e.id == userId);
            if (string.IsNullOrEmpty(text))
            {
                if (at >= 0) entries.RemoveAt(at);
            }
            else if (at >= 0)
            {
                entries[at].text = text;
                if (colourHex != null) entries[at].colour = colourHex;
            }
            else
            {
                entries.Add(new Entry { id = userId, text = text, colour = colourHex ?? Colours[0].hex });
            }
            Save();
        }

        /// <summary>Next preset for this player (right-click steps backwards).</summary>
        public static void CycleLocalTag(string userId, int dir)
        {
            string current = LocalTag(userId) ?? "";
            int i = Array.IndexOf(Presets, current);
            string next = Presets[(((i < 0 ? 0 : i) + dir) % Presets.Length + Presets.Length) % Presets.Length];
            SetLocalTag(userId, next, ColourOf(userId));
        }

        public static void CycleLocalColour(string userId, int dir)
        {
            string hex = ColourOf(userId);
            int i = Array.FindIndex(Colours, c => c.hex.Equals(hex, StringComparison.OrdinalIgnoreCase));
            SetLocalTag(userId, LocalTag(userId), Colours[(((i < 0 ? 0 : i) + dir) % Colours.Length + Colours.Length) % Colours.Length].hex);
        }

        public static string ColourOf(string userId)
        {
            var entries = Load().entries;
            for (int i = 0; i < entries.Count; i++) if (entries[i].id == userId) return entries[i].colour;
            return Colours[0].hex;
        }

        public static int LocalCount => Load().entries.Count;

        public static void ClearLocal()
        {
            Load().entries.Clear();
            Save();
        }

        // ------------------------------------------------------------------ helpers

        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Trim();
            if (text.Length > MaxLength) text = text.Substring(0, MaxLength);
            return text;
        }

        public static string ColourName(string hex)
        {
            foreach (var c in Colours) if (c.hex.Equals(hex, StringComparison.OrdinalIgnoreCase)) return c.name;
            return hex;
        }

        public static Color Parse(string hex) =>
            !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : new Color(0.23f, 0.91f, 1f);
    }
}
