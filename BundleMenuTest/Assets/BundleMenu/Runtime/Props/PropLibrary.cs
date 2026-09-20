using System;
using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>One thing you can spawn: a name, a line about what it does, and how to build it.</summary>
    public sealed class PropDef
    {
        public string Name;
        public string About;
        public string Group;                                   // Playground / Scenery / Toys
        public Func<PropBuild, GameObject> Build;
        public bool NeedsMods;                                 // moves your player, so private rooms only
    }

    /// <summary>What a prop gets handed while it's being built.</summary>
    public struct PropBuild
    {
        public MenuTheme Theme;
        public int WalkableLayer;                              // the layer Gorilla Tag lets you stand on
        public Func<bool> Allowed;                             // the mods' lobby rule
        public Func<Vector3, bool> Teleport;                   // moves you, the game's own way
        public Action SaveCheckpoint;                          // the checkpoint flag uses this
        public Action<Vector3> Surprise;                       // the chest asks for a random prop here
    }

    /// <summary>
    /// A set of things to spawn that don't need any downloads: they're built out of Unity's own shapes
    /// in your theme's colours, so they always match the menu and always work. Everything here exists on
    /// your machine only - other players don't see any of it.
    /// </summary>
    public static partial class PropLibrary
    {
        private static List<PropDef> all;

        public static IReadOnlyList<PropDef> All
        {
            get
            {
                if (all != null) return all;
                all = new List<PropDef>();
                AddPlayground();
                AddMorePlayground();
                AddScenery();
                AddMoreScenery();
                AddToys();
                AddMoreToys();
                return all;
            }
        }

        public static PropDef Find(string name) =>
            All.Count == 0 ? null : all.Find(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        private static void Add(string name, string group, string about, Func<PropBuild, GameObject> build, bool needsMods = false) =>
            all.Add(new PropDef { Name = name, Group = group, About = about, Build = build, NeedsMods = needsMods });

        /// <summary>
        /// Builds a prop and hands it to the spawner, so it counts as one of your spawned things: the
        /// gun's Delete mode can remove it and "clear what I spawned" tidies it away.
        /// </summary>
        public static GameObject Spawn(PropDef def, PropBuild build, AssetSpawner spawner, Vector3 position, Quaternion rotation)
        {
            if (def == null) return null;
            var go = def.Build(build);
            if (go == null) return null;
            go.transform.SetPositionAndRotation(position, rotation);
            // The grow-in animation scales the whole prop, which upsets loose physics parts, so props
            // with their own moving pieces (the cans, the ball) simply appear.
            if (go.GetComponentInChildren<Rigidbody>() == null) go.AddComponent<PropIntro>();
            if (spawner != null) spawner.Adopt(go, "props");
            return go;
        }

        // =============================================================================== playground

        private static void AddPlayground()
        {
            Add("Trampoline", "Playground", "Land on it and you're fired straight back up.", b =>
            {
                var root = PropKit.Root("Trampoline", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Frame", new Vector3(0f, 0.18f, 0f),
                    new Vector3(1.5f, 0.18f, 1.5f), PropKit.Body(b.Theme));
                var mat = PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Mat", new Vector3(0f, 0.36f, 0f),
                    new Vector3(1.35f, 0.04f, 1.35f), PropKit.Accent(b.Theme), collide: true);
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * Mathf.PI / 3f;
                    PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Leg",
                        new Vector3(Mathf.Cos(angle) * 0.62f, 0.09f, Mathf.Sin(angle) * 0.62f),
                        new Vector3(0.08f, 0.09f, 0.08f), PropKit.Dark(b.Theme), collide: false);
                }
                var bouncer = mat.gameObject.AddComponent<Bouncer>();
                bouncer.Power = 13f;
                bouncer.Squash = mat;
                bouncer.Allowed = b.Allowed;
                return root;
            }, needsMods: true);

            Add("Launch pad", "Playground", "Step on it and you're thrown the way it's pointing.", b =>
            {
                var root = PropKit.Root("Launch pad", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Wedge", new Vector3(0f, 0.16f, 0f),
                    new Vector3(1.1f, 0.3f, 1.1f), PropKit.Body(b.Theme), euler: new Vector3(-18f, 0f, 0f));
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Arrow", new Vector3(0f, 0.33f, 0.1f),
                    new Vector3(0.22f, 0.03f, 0.7f), PropKit.Accent(b.Theme), collide: false,
                    euler: new Vector3(-18f, 0f, 0f), glow: 1.4f);

                var zone = new GameObject("Zone");
                zone.transform.SetParent(root.transform, false);
                zone.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                var box = zone.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1.2f, 1.2f, 1.2f);
                var booster = zone.AddComponent<Booster>();
                booster.Power = 17f;
                booster.Direction = new Vector3(0f, 0.75f, 0.65f);
                booster.Allowed = b.Allowed;
                return root;
            }, needsMods: true);

            Add("Boost ring", "Playground", "Fly through it and you come out the other side much faster.", b =>
            {
                var root = PropKit.Root("Boost ring", b.WalkableLayer);
                var ring = new GameObject("Ring");
                ring.transform.SetParent(root.transform, false);
                ring.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                var accent = PropKit.Accent(b.Theme);
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI * 2f / 16f;
                    PropKit.Shape(ring.transform, PrimitiveType.Cube, "Segment",
                        new Vector3(Mathf.Cos(angle) * 1.1f, Mathf.Sin(angle) * 1.1f, 0f),
                        new Vector3(0.16f, 0.45f, 0.16f), Color.Lerp(accent, PropKit.Accent2(b.Theme), i / 16f),
                        collide: false, euler: new Vector3(0f, 0f, angle * Mathf.Rad2Deg + 90f), glow: 1.2f);
                }
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Post", new Vector3(0f, 0.25f, 0f),
                    new Vector3(0.12f, 0.25f, 0.12f), PropKit.Dark(b.Theme));

                var zone = new GameObject("Zone");
                zone.transform.SetParent(ring.transform, false);
                var box = zone.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1.8f, 1.8f, 0.5f);
                var booster = zone.AddComponent<Booster>();
                booster.Power = 20f;
                booster.Direction = Vector3.forward;
                booster.Allowed = b.Allowed;
                return root;
            }, needsMods: true);

            Add("Climbing pole", "Playground", "A pole with rungs, for getting up where there's nothing to grab.", b =>
            {
                var root = PropKit.Root("Climbing pole", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Pole", new Vector3(0f, 2.5f, 0f),
                    new Vector3(0.12f, 2.5f, 0.12f), PropKit.Body(b.Theme));
                for (int i = 0; i < 10; i++)
                {
                    float y = 0.45f + i * 0.5f;
                    PropKit.Shape(root.transform, PrimitiveType.Cube, "Rung", new Vector3(0f, y, 0f),
                        new Vector3(0.62f, 0.07f, 0.1f), i % 2 == 0 ? PropKit.Accent(b.Theme) : PropKit.Accent2(b.Theme),
                        euler: new Vector3(0f, i * 18f, 0f));
                }
                return root;
            });

            Add("Long ramp", "Playground", "A smooth slope to run up, slide down, or jump off.", b =>
            {
                var root = PropKit.Root("Long ramp", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Slope", new Vector3(0f, 0.75f, 0f),
                    new Vector3(2.2f, 0.15f, 6f), PropKit.Body(b.Theme), euler: new Vector3(-14f, 0f, 0f));
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Kerb left", new Vector3(-1.1f, 0.95f, 0f),
                    new Vector3(0.12f, 0.3f, 6f), PropKit.Accent(b.Theme), euler: new Vector3(-14f, 0f, 0f));
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Kerb right", new Vector3(1.1f, 0.95f, 0f),
                    new Vector3(0.12f, 0.3f, 6f), PropKit.Accent(b.Theme), euler: new Vector3(-14f, 0f, 0f));
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Base", new Vector3(0f, 0.12f, -2.6f),
                    new Vector3(2.2f, 0.25f, 1.2f), PropKit.Dark(b.Theme));
                return root;
            });

            Add("Portal pad", "Playground", "Spawn two: step into one and you come out of the other.", b =>
            {
                var root = PropKit.Root("Portal pad", b.WalkableLayer);
                var accent = PropKit.Accent(b.Theme);
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.06f, 0f),
                    new Vector3(1.3f, 0.06f, 1.3f), PropKit.Dark(b.Theme));
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Disc", new Vector3(0f, 0.14f, 0f),
                    new Vector3(1.1f, 0.02f, 1.1f), accent, collide: false, glow: 1.6f);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4f;
                    PropKit.Shape(root.transform, PrimitiveType.Cube, "Pillar",
                        new Vector3(Mathf.Cos(angle) * 0.62f, 0.4f, Mathf.Sin(angle) * 0.62f),
                        new Vector3(0.07f, 0.4f, 0.07f), PropKit.Accent2(b.Theme), collide: false);
                }
                PropKit.Lamp(root.transform, accent, 4f, 1.2f, new Vector3(0f, 0.5f, 0f));

                var zone = new GameObject("Zone");
                zone.transform.SetParent(root.transform, false);
                zone.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                var capsule = zone.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.radius = 0.55f;
                capsule.height = 1.8f;
                var portal = zone.AddComponent<PortalPad>();
                portal.Allowed = b.Allowed;
                portal.Teleport = b.Teleport;
                return root;
            }, needsMods: true);
        }
    }
}
