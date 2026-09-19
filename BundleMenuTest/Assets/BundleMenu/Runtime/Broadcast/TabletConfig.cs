using System;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// How a tablet looks and behaves. Stored as JSON in the content bundle so styles can be edited
    /// without changing the menu. Colours: "#RRGGBB" or "theme:Accent", "theme:PanelBottom", etc.
    /// </summary>
    [Serializable]
    public sealed class TabletConfig
    {
        public string name = "Classic";

        public float width = 0.42f;
        public float aspect = 16f / 9f;
        public float bezel = 0.03f;
        public float cornerRadius = 0.035f;
        public float thickness = 0.022f;
        public int cornerSegments = 8;

        public bool accentStrip = true;
        public bool cameraDot = true;
        public int sideButtons = 2;
        public bool handle;
        public bool kickstand;
        public bool bumpers;

        public string body = "theme:PanelBottom";
        public string bezelColor = "#07080D";
        public string accent = "theme:Accent";
        public string trim = "theme:Accent2";

        public float distance = 0.55f;
        public string follow = "lazy";
        public float followSpeed = 8f;
        public float tiltDegrees = 6f;
        public float bob = 0.004f;
        public string openAnimation = "pop";

        public static TabletConfig Parse(string json)
        {
            var c = JsonUtility.FromJson<TabletConfig>(json) ?? new TabletConfig();
            c.width = Mathf.Clamp(c.width, 0.15f, 1.2f);
            c.aspect = Mathf.Clamp(c.aspect, 0.5f, 3f);
            c.bezel = Mathf.Clamp(c.bezel, 0.005f, 0.15f);
            c.cornerRadius = Mathf.Clamp(c.cornerRadius, 0f, 0.2f);
            c.thickness = Mathf.Clamp(c.thickness, 0.005f, 0.1f);
            c.cornerSegments = Mathf.Clamp(c.cornerSegments, 1, 16);
            c.sideButtons = Mathf.Clamp(c.sideButtons, 0, 4);
            return c;
        }

        public static TabletConfig Classic() => new TabletConfig();

        public static TabletConfig Slim() => new TabletConfig
        {
            name = "Slim", width = 0.46f, bezel = 0.012f, cornerRadius = 0.02f, thickness = 0.01f,
            sideButtons = 1, body = "#D9DDE6", bezelColor = "#101218",
        };

        public static TabletConfig Rugged() => new TabletConfig
        {
            name = "Rugged", width = 0.38f, bezel = 0.05f, cornerRadius = 0.05f, thickness = 0.04f,
            bumpers = true, handle = true, sideButtons = 3, body = "#2B2F24", trim = "#E0A526", accent = "#E0A526",
        };

        public static TabletConfig Retro() => new TabletConfig
        {
            name = "Retro", width = 0.36f, aspect = 4f / 3f, bezel = 0.06f, cornerRadius = 0.015f, thickness = 0.05f,
            kickstand = true, cameraDot = false, sideButtons = 0, body = "#C9B79C", bezelColor = "#3A3228",
            accent = "#FF6A3D", openAnimation = "flip",
        };

        public static TabletConfig[] BuiltIn() => new[] { Classic(), Slim(), Rugged(), Retro() };
    }
}
