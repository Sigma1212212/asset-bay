using UnityEngine;

namespace BundleMenu
{
    /// <summary>Scenery and toys: things to look at, knock over and play with.</summary>
    public static partial class PropLibrary
    {
        // =============================================================================== scenery

        private static void AddScenery()
        {
            Add("Campfire", "Scenery", "Logs with a fire that flickers and lights the ground around it.", b =>
            {
                var root = PropKit.Root("Campfire", b.WalkableLayer);
                var stone = Color.Lerp(PropKit.Dark(b.Theme), Color.gray, 0.4f);
                for (int i = 0; i < 7; i++)
                {
                    float angle = i * Mathf.PI * 2f / 7f;
                    PropKit.Shape(root.transform, PrimitiveType.Sphere, "Stone",
                        new Vector3(Mathf.Cos(angle) * 0.55f, 0.08f, Mathf.Sin(angle) * 0.55f),
                        new Vector3(0.24f, 0.16f, 0.24f), stone);
                }
                for (int i = 0; i < 4; i++)
                    PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Log", new Vector3(0f, 0.12f, 0f),
                        new Vector3(0.09f, 0.42f, 0.09f), new Color(0.32f, 0.2f, 0.12f),
                        euler: new Vector3(62f, i * 45f, 0f));
                var flame = PropKit.Shape(root.transform, PrimitiveType.Sphere, "Flame", new Vector3(0f, 0.38f, 0f),
                    new Vector3(0.34f, 0.5f, 0.34f), new Color(1f, 0.55f, 0.15f), collide: false, glow: 2.2f);
                var light = PropKit.Lamp(root.transform, new Color(1f, 0.6f, 0.25f), 9f, 1.8f, new Vector3(0f, 0.6f, 0f));
                var flicker = root.AddComponent<Flicker>();
                flicker.Target = light;
                root.AddComponent<Spinner>().Speed = new Vector3(0f, 25f, 0f);
                flame.gameObject.AddComponent<Spinner>().Speed = new Vector3(0f, 140f, 0f);
                return root;
            });

            Add("Lamp post", "Scenery", "A tall lamp - handy in the dark bits of a map.", b =>
            {
                var root = PropKit.Root("Lamp post", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Post", new Vector3(0f, 1.5f, 0f),
                    new Vector3(0.11f, 1.5f, 0.11f), PropKit.Body(b.Theme));
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Foot", new Vector3(0f, 0.06f, 0f),
                    new Vector3(0.42f, 0.06f, 0.42f), PropKit.Dark(b.Theme));
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Arm", new Vector3(0f, 2.95f, 0.22f),
                    new Vector3(0.09f, 0.09f, 0.55f), PropKit.Body(b.Theme), collide: false);
                PropKit.Shape(root.transform, PrimitiveType.Sphere, "Bulb", new Vector3(0f, 2.82f, 0.46f),
                    new Vector3(0.3f, 0.3f, 0.3f), PropKit.Accent(b.Theme), collide: false, glow: 2f);
                PropKit.Lamp(root.transform, PropKit.Accent(b.Theme), 13f, 2.2f, new Vector3(0f, 2.75f, 0.46f));
                return root;
            });

            Add("Disco ball", "Scenery", "Spins overhead and throws your theme's colours around the room.", b =>
            {
                var root = PropKit.Root("Disco ball", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Chain", new Vector3(0f, 2.4f, 0f),
                    new Vector3(0.03f, 0.6f, 0.03f), PropKit.Dark(b.Theme), collide: false);
                var ball = PropKit.Shape(root.transform, PrimitiveType.Sphere, "Ball", new Vector3(0f, 1.6f, 0f),
                    new Vector3(0.7f, 0.7f, 0.7f), new Color(0.75f, 0.78f, 0.85f));
                // Facets: little tiles all over the ball, in the theme's two colours.
                for (int i = 0; i < 26; i++)
                {
                    var dir = Random.onUnitSphere;
                    var tile = PropKit.Shape(ball, PrimitiveType.Cube, "Facet", dir * 0.52f,
                        new Vector3(0.22f, 0.22f, 0.05f), i % 2 == 0 ? PropKit.Accent(b.Theme) : PropKit.Accent2(b.Theme),
                        collide: false, glow: 1.3f);
                    tile.localRotation = Quaternion.LookRotation(dir);
                }
                ball.gameObject.AddComponent<Spinner>().Speed = new Vector3(0f, 55f, 0f);
                PropKit.Lamp(root.transform, PropKit.Accent(b.Theme), 10f, 1.6f, new Vector3(0f, 1.6f, 0f));
                return root;
            });

            Add("Monke statue", "Scenery", "A little statue of you-know-who, in your theme's colours.", b =>
            {
                var root = PropKit.Root("Monke statue", b.WalkableLayer);
                var stone = PropKit.Body(b.Theme);
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Plinth", new Vector3(0f, 0.2f, 0f),
                    new Vector3(0.9f, 0.4f, 0.9f), PropKit.Dark(b.Theme));
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Trim", new Vector3(0f, 0.42f, 0f),
                    new Vector3(1f, 0.06f, 1f), PropKit.Accent(b.Theme), glow: 0.8f);
                PropKit.Shape(root.transform, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.85f, 0f),
                    new Vector3(0.52f, 0.36f, 0.45f), stone);
                PropKit.Shape(root.transform, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.28f, 0.04f),
                    new Vector3(0.42f, 0.4f, 0.4f), stone);
                PropKit.Shape(root.transform, PrimitiveType.Sphere, "Snout", new Vector3(0f, 1.2f, 0.2f),
                    new Vector3(0.24f, 0.18f, 0.2f), Color.Lerp(stone, Color.white, 0.25f), collide: false);
                for (int side = -1; side <= 1; side += 2)
                {
                    PropKit.Shape(root.transform, PrimitiveType.Sphere, "Ear", new Vector3(0.21f * side, 1.3f, 0f),
                        new Vector3(0.14f, 0.16f, 0.06f), stone, collide: false);
                    PropKit.Shape(root.transform, PrimitiveType.Sphere, "Eye", new Vector3(0.1f * side, 1.33f, 0.17f),
                        new Vector3(0.07f, 0.08f, 0.05f), PropKit.Accent(b.Theme), collide: false, glow: 1.4f);
                    PropKit.Shape(root.transform, PrimitiveType.Capsule, "Arm", new Vector3(0.34f * side, 0.82f, 0.02f),
                        new Vector3(0.18f, 0.3f, 0.18f), stone, collide: false, euler: new Vector3(8f, 0f, 14f * side));
                }
                return root;
            });
        }

        // =============================================================================== toys

        private static void AddToys()
        {
            Add("Beach ball", "Toys", "A big light ball that rolls around and bounces off everything.", b =>
            {
                var root = PropKit.Root("Beach ball", b.WalkableLayer);
                var ball = PropKit.Shape(root.transform, PrimitiveType.Sphere, "Ball", Vector3.zero,
                    new Vector3(0.9f, 0.9f, 0.9f), PropKit.Accent(b.Theme));
                for (int i = 0; i < 5; i++)
                    PropKit.Shape(ball, PrimitiveType.Cube, "Stripe", Vector3.zero,
                        new Vector3(1.01f, 0.24f, 0.18f), i % 2 == 0 ? PropKit.Accent2(b.Theme) : Color.white,
                        collide: false, euler: new Vector3(0f, i * 36f, 0f));
                var rb = PropKit.Physical(root, 0.6f, 0.12f);
                rb.angularDrag = 0.15f;
                var collider = ball.GetComponent<SphereCollider>();
                if (collider != null) collider.sharedMaterial = PropKit.Bouncy(0.75f);
                return root;
            });

            Add("Balloon", "Toys", "Floats up on its own and pops when something hits it.", b =>
            {
                var root = PropKit.Root("Balloon", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Sphere, "Balloon", new Vector3(0f, 0.3f, 0f),
                    new Vector3(0.5f, 0.62f, 0.5f), PropKit.Accent2(b.Theme), glow: 0.6f);
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "String", new Vector3(0f, -0.25f, 0f),
                    new Vector3(0.012f, 0.32f, 0.012f), Color.white, collide: false);
                PropKit.Physical(root, 0.08f, 0.6f);
                root.AddComponent<Balloon>();
                return root;
            });

            Add("Can stack", "Toys", "Six cans to knock over - the sign keeps score. Try the Impulse gun.", b =>
            {
                var root = PropKit.Root("Can stack", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Table", new Vector3(0f, 0.45f, 0f),
                    new Vector3(1.4f, 0.1f, 0.7f), PropKit.Body(b.Theme));
                for (int side = -1; side <= 1; side += 2)
                    PropKit.Shape(root.transform, PrimitiveType.Cube, "Leg", new Vector3(0.6f * side, 0.22f, 0f),
                        new Vector3(0.12f, 0.45f, 0.5f), PropKit.Dark(b.Theme));

                var sign = PropKit.Shape(root.transform, PrimitiveType.Cube, "Sign", new Vector3(0f, 1.35f, -0.3f),
                    new Vector3(0.9f, 0.3f, 0.06f), PropKit.Dark(b.Theme), collide: false);
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Sign post", new Vector3(0f, 0.95f, -0.3f),
                    new Vector3(0.06f, 0.45f, 0.06f), PropKit.Body(b.Theme), collide: false);

                var score = root.AddComponent<TargetScore>();
                var pips = new Transform[6];
                for (int i = 0; i < 6; i++)
                {
                    pips[i] = PropKit.Shape(root.transform, PrimitiveType.Sphere, "Pip",
                        new Vector3(-0.33f + i * 0.132f, 1.35f, -0.35f), new Vector3(0.08f, 0.08f, 0.08f),
                        PropKit.Accent(b.Theme), collide: false, glow: 1.6f);
                    pips[i].gameObject.SetActive(false);
                }
                score.Pips = pips;

                // The cans are separate objects so they can be knocked flying on their own.
                for (int i = 0; i < 6; i++)
                {
                    var can = PropKit.Root("Can", b.WalkableLayer);
                    can.transform.SetParent(root.transform, false);
                    can.transform.localPosition = new Vector3(-0.45f + (i % 3) * 0.45f, 0.62f + (i / 3) * 0.24f, 0f);
                    PropKit.Shape(can.transform, PrimitiveType.Cylinder, "Body", Vector3.zero,
                        new Vector3(0.14f, 0.11f, 0.14f), i % 2 == 0 ? PropKit.Accent(b.Theme) : PropKit.Accent2(b.Theme));
                    PropKit.Physical(can, 0.25f, 0.02f);
                    can.AddComponent<TargetCan>().Score = score;
                }
                return root;
            });

            Add("Boombox", "Toys", "Shows your Spotify cover art and thumps along with whatever's playing.", b =>
            {
                var root = PropKit.Root("Boombox", b.WalkableLayer);
                var body = PropKit.Body(b.Theme);
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 0.38f, 0f),
                    new Vector3(1.25f, 0.68f, 0.42f), body);
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Handle", new Vector3(0f, 0.78f, 0f),
                    new Vector3(0.05f, 0.35f, 0.05f), PropKit.Dark(b.Theme), collide: false,
                    euler: new Vector3(0f, 0f, 90f));

                var left = PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Cone left", new Vector3(-0.42f, 0.38f, 0.22f),
                    new Vector3(0.32f, 0.02f, 0.32f), PropKit.Dark(b.Theme), collide: false, euler: new Vector3(90f, 0f, 0f));
                var right = PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Cone right", new Vector3(0.42f, 0.38f, 0.22f),
                    new Vector3(0.32f, 0.02f, 0.32f), PropKit.Dark(b.Theme), collide: false, euler: new Vector3(90f, 0f, 0f));

                // The cover art goes on this panel; it stays blank until Spotify is signed in.
                var screen = PropKit.Shape(root.transform, PrimitiveType.Quad, "Screen", new Vector3(0f, 0.45f, 0.222f),
                    new Vector3(0.36f, 0.36f, 1f), Color.white, collide: false, euler: new Vector3(0f, 180f, 0f));
                var needle = PropKit.Shape(root.transform, PrimitiveType.Cube, "Needle", new Vector3(0f, 0.16f, 0.22f),
                    new Vector3(0.02f, 0.12f, 0.02f), PropKit.Accent(b.Theme), collide: false, glow: 1.4f);

                var boombox = root.AddComponent<Boombox>();
                boombox.LeftCone = left;
                boombox.RightCone = right;
                boombox.Screen = screen.GetComponent<Renderer>();
                boombox.Needle = needle;
                boombox.Glow = PropKit.Lamp(root.transform, PropKit.Accent(b.Theme), 5f, 0.8f, new Vector3(0f, 0.45f, 0.4f));
                return root;
            });
        }
    }
}
