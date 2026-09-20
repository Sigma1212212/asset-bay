using UnityEngine;

namespace BundleMenu
{
    /// <summary>More toys and scenery: things to play with and things to look at.</summary>
    public static partial class PropLibrary
    {
        private static void AddMoreToys()
        {
            Add("Basketball hoop", "Toys", "Board, ring and a ball. Sink one and the board lights up.", b =>
            {
                var root = PropKit.Root("Basketball hoop", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Post", new Vector3(0f, 1.6f, -0.4f),
                    new Vector3(0.14f, 1.6f, 0.14f), PropKit.Body(b.Theme));
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Foot", new Vector3(0f, 0.07f, -0.4f),
                    new Vector3(0.9f, 0.07f, 0.9f), PropKit.Dark(b.Theme));
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Board", new Vector3(0f, 3.1f, -0.3f),
                    new Vector3(1.8f, 1.1f, 0.08f), PropKit.Dark(b.Theme));
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Square", new Vector3(0f, 2.9f, -0.25f),
                    new Vector3(0.7f, 0.5f, 0.02f), PropKit.Accent(b.Theme), collide: false, glow: 1.2f);

                // The ring: short blocks in a circle, with a scoring trigger under it.
                var ring = new GameObject("Ring");
                ring.transform.SetParent(root.transform, false);
                ring.transform.localPosition = new Vector3(0f, 2.75f, 0.25f);
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * Mathf.PI * 2f / 12f;
                    PropKit.Shape(ring.transform, PrimitiveType.Cube, "Ring part",
                        new Vector3(Mathf.Cos(angle) * 0.32f, 0f, Mathf.Sin(angle) * 0.32f),
                        new Vector3(0.1f, 0.06f, 0.18f), new Color(1f, 0.5f, 0.15f), glow: 0.6f,
                        euler: new Vector3(0f, -angle * Mathf.Rad2Deg, 0f));
                }

                var score = root.AddComponent<TargetScore>();
                var pips = new Transform[5];
                for (int i = 0; i < 5; i++)
                {
                    pips[i] = PropKit.Shape(root.transform, PrimitiveType.Sphere, "Pip",
                        new Vector3(-0.6f + i * 0.3f, 3.55f, -0.3f), Vector3.one * 0.12f,
                        PropKit.Accent(b.Theme), collide: false, glow: 1.6f);
                    pips[i].gameObject.SetActive(false);
                }
                score.Pips = pips;

                var net = new GameObject("Net");
                net.transform.SetParent(ring.transform, false);
                net.transform.localPosition = new Vector3(0f, -0.25f, 0f);
                var trigger = net.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(0.5f, 0.3f, 0.5f);
                net.AddComponent<ScoreZone>().Score = score;

                // ...and a ball to shoot with.
                var ball = PropKit.Root("Ball", b.WalkableLayer);
                ball.transform.SetParent(root.transform, false);
                ball.transform.localPosition = new Vector3(0.9f, 0.4f, 1.4f);
                var sphere = PropKit.Shape(ball.transform, PrimitiveType.Sphere, "Ball", Vector3.zero,
                    Vector3.one * 0.5f, new Color(0.95f, 0.45f, 0.12f));
                var sphereCollider = sphere.GetComponent<SphereCollider>();
                if (sphereCollider != null) sphereCollider.sharedMaterial = PropKit.Bouncy(0.7f);
                PropKit.Physical(ball, 0.6f, 0.08f);
                return root;
            });

            Add("Bowling set", "Toys", "Ten pins and a heavy ball. Roll it, or shove it with the gun.", b =>
            {
                var root = PropKit.Root("Bowling set", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Lane", new Vector3(0f, 0.03f, 1.5f),
                    new Vector3(2f, 0.06f, 6f), PropKit.Dark(b.Theme));
                for (int side = -1; side <= 1; side += 2)
                    PropKit.Shape(root.transform, PrimitiveType.Cube, "Gutter", new Vector3(1.05f * side, 0.1f, 1.5f),
                        new Vector3(0.12f, 0.2f, 6f), PropKit.Accent(b.Theme), glow: 0.7f);

                var score = root.AddComponent<TargetScore>();
                var pips = new Transform[10];
                int made = 0;
                for (int row = 0; row < 4; row++)
                    for (int i = 0; i <= row; i++)
                    {
                        var pin = PropKit.Root("Pin", b.WalkableLayer);
                        pin.transform.SetParent(root.transform, false);
                        pin.transform.localPosition = new Vector3((i - row * 0.5f) * 0.38f, 0.22f, 3.6f + row * 0.36f);
                        PropKit.Shape(pin.transform, PrimitiveType.Capsule, "Pin", Vector3.zero,
                            new Vector3(0.13f, 0.17f, 0.13f), Color.white);
                        PropKit.Shape(pin.transform, PrimitiveType.Cylinder, "Band", new Vector3(0f, 0.07f, 0f),
                            new Vector3(0.14f, 0.02f, 0.14f), PropKit.Accent(b.Theme), collide: false);
                        PropKit.Physical(pin, 0.3f, 0.02f);
                        pin.AddComponent<TargetCan>().Score = score;

                        pips[made] = PropKit.Shape(root.transform, PrimitiveType.Sphere, "Pip",
                            new Vector3(-0.65f + made * 0.145f, 0.9f, -1.4f), Vector3.one * 0.09f,
                            PropKit.Accent(b.Theme), collide: false, glow: 1.5f);
                        pips[made].gameObject.SetActive(false);
                        made++;
                        if (made >= 10) break;
                    }
                score.Pips = pips;

                var ball = PropKit.Root("Bowling ball", b.WalkableLayer);
                ball.transform.SetParent(root.transform, false);
                ball.transform.localPosition = new Vector3(0f, 0.35f, -1f);
                PropKit.Shape(ball.transform, PrimitiveType.Sphere, "Ball", Vector3.zero,
                    Vector3.one * 0.42f, PropKit.Accent2(b.Theme), glow: 0.3f);
                PropKit.Physical(ball, 4f, 0.05f);
                return root;
            });

            Add("Punching bag", "Toys", "Hangs from a frame and swings when you hit it.", b =>
            {
                var root = PropKit.Root("Punching bag", b.WalkableLayer);
                var frame = PropKit.Body(b.Theme);
                for (int side = -1; side <= 1; side += 2)
                    PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Leg", new Vector3(0.9f * side, 1.2f, 0f),
                        new Vector3(0.1f, 1.2f, 0.1f), frame, euler: new Vector3(0f, 0f, 8f * side));
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Beam", new Vector3(0f, 2.45f, 0f),
                    new Vector3(2.2f, 0.12f, 0.12f), frame);

                // The frame needs a body of its own for the joint to hang from.
                var anchor = PropKit.Physical(root, 1f);
                anchor.isKinematic = true;

                var bag = PropKit.Root("Bag", b.WalkableLayer);
                bag.transform.SetParent(root.transform, false);
                bag.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                PropKit.Shape(bag.transform, PrimitiveType.Capsule, "Bag", Vector3.zero,
                    new Vector3(0.45f, 0.6f, 0.45f), PropKit.Accent2(b.Theme));
                PropKit.Shape(bag.transform, PrimitiveType.Cylinder, "Strap", new Vector3(0f, 0.62f, 0f),
                    new Vector3(0.1f, 0.35f, 0.1f), PropKit.Dark(b.Theme), collide: false);
                var body = PropKit.Physical(bag, 2.5f, 0.3f);
                body.angularDrag = 1.2f;

                var joint = bag.AddComponent<HingeJoint>();
                joint.connectedBody = anchor;
                joint.anchor = new Vector3(0f, 0.95f, 0f);
                joint.axis = Vector3.right;
                joint.useSpring = true;
                joint.spring = new JointSpring { spring = 12f, damper = 2f, targetPosition = 0f };
                return root;
            });

            Add("Firework", "Toys", "Walk into it and it fires a burst of sparks over your head.", b =>
            {
                var root = PropKit.Root("Firework", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Tube", new Vector3(0f, 0.45f, 0f),
                    new Vector3(0.3f, 0.45f, 0.3f), PropKit.Dark(b.Theme));
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Ring", new Vector3(0f, 0.88f, 0f),
                    new Vector3(0.36f, 0.04f, 0.36f), PropKit.Accent(b.Theme), collide: false, glow: 1.6f);
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(root.transform, false);
                muzzle.localPosition = new Vector3(0f, 1f, 0f);

                var zone = new GameObject("Touch");
                zone.transform.SetParent(root.transform, false);
                zone.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                var sphere = zone.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = 1.1f;
                var firework = zone.AddComponent<Firework>();
                firework.Muzzle = muzzle;
                firework.A = PropKit.Accent(b.Theme);
                firework.B = PropKit.Accent2(b.Theme);
                return root;
            });

            Add("Surprise chest", "Toys", "Open it and something else from this list falls out.", b =>
            {
                var root = PropKit.Root("Surprise chest", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Box", new Vector3(0f, 0.32f, 0f),
                    new Vector3(1.1f, 0.64f, 0.75f), new Color(0.35f, 0.24f, 0.14f));
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Band", new Vector3(0f, 0.32f, 0f),
                    new Vector3(1.12f, 0.12f, 0.77f), PropKit.Accent(b.Theme), collide: false, glow: 0.9f);

                var lid = new GameObject("Lid").transform;
                lid.SetParent(root.transform, false);
                lid.localPosition = new Vector3(0f, 0.64f, -0.37f);
                PropKit.Shape(lid, PrimitiveType.Cube, "Top", new Vector3(0f, 0.06f, 0.37f),
                    new Vector3(1.14f, 0.14f, 0.79f), new Color(0.4f, 0.28f, 0.16f));
                PropKit.Shape(lid, PrimitiveType.Sphere, "Lock", new Vector3(0f, 0f, 0.77f),
                    new Vector3(0.16f, 0.16f, 0.1f), PropKit.Accent(b.Theme), collide: false, glow: 1.3f);

                var zone = new GameObject("Touch");
                zone.transform.SetParent(root.transform, false);
                zone.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                var box = zone.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1.6f, 1.4f, 1.3f);
                var chest = zone.AddComponent<SurpriseChest>();
                chest.Lid = lid;
                chest.Open = b.Surprise;
                return root;
            });
        }

        private static void AddMoreScenery()
        {
            Add("Tree", "Scenery", "A tree. Sometimes a map just needs one.", b =>
            {
                var root = PropKit.Root("Tree", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 1.2f, 0f),
                    new Vector3(0.28f, 1.2f, 0.28f), new Color(0.33f, 0.22f, 0.14f));
                var leaf = new Color(0.22f, 0.55f, 0.28f);
                PropKit.Shape(root.transform, PrimitiveType.Sphere, "Canopy", new Vector3(0f, 2.7f, 0f),
                    new Vector3(2.2f, 1.9f, 2.2f), leaf);
                PropKit.Shape(root.transform, PrimitiveType.Sphere, "Canopy", new Vector3(0.7f, 2.2f, 0.4f),
                    new Vector3(1.4f, 1.2f, 1.4f), Color.Lerp(leaf, Color.black, 0.15f));
                PropKit.Shape(root.transform, PrimitiveType.Sphere, "Canopy", new Vector3(-0.6f, 2.4f, -0.5f),
                    new Vector3(1.5f, 1.3f, 1.5f), Color.Lerp(leaf, Color.white, 0.1f));
                return root;
            });

            Add("Arcade sign", "Scenery", "A big lit sign with bulbs chasing around it.", b =>
            {
                var root = PropKit.Root("Arcade sign", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Post", new Vector3(0f, 0.9f, 0f),
                    new Vector3(0.12f, 0.9f, 0.12f), PropKit.Dark(b.Theme));
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Panel", new Vector3(0f, 2.3f, 0f),
                    new Vector3(2.6f, 1.1f, 0.12f), PropKit.Dark(b.Theme));
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Face", new Vector3(0f, 2.3f, 0.07f),
                    new Vector3(2.3f, 0.85f, 0.02f), PropKit.Body(b.Theme), collide: false);

                var bulbs = new Renderer[14];
                for (int i = 0; i < 14; i++)
                {
                    bool top = i < 7;
                    var bulb = PropKit.Shape(root.transform, PrimitiveType.Sphere, "Bulb",
                        new Vector3(-1.2f + (i % 7) * 0.4f, top ? 2.78f : 1.82f, 0.09f),
                        Vector3.one * 0.13f, PropKit.Accent(b.Theme), collide: false, glow: 1.8f);
                    bulbs[i] = bulb.GetComponent<Renderer>();
                }
                var chase = root.AddComponent<SignChase>();
                chase.Bulbs = bulbs;
                chase.A = PropKit.Accent(b.Theme);
                chase.B = PropKit.Accent2(b.Theme);
                PropKit.Lamp(root.transform, PropKit.Accent(b.Theme), 8f, 1.2f, new Vector3(0f, 2.3f, 0.6f));
                return root;
            });

            Add("Picnic bench", "Scenery", "Somewhere to sit and watch the chaos.", b =>
            {
                var root = PropKit.Root("Picnic bench", b.WalkableLayer);
                var wood = new Color(0.45f, 0.32f, 0.2f);
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Table", new Vector3(0f, 0.75f, 0f),
                    new Vector3(1.9f, 0.09f, 0.85f), wood);
                for (int side = -1; side <= 1; side += 2)
                {
                    PropKit.Shape(root.transform, PrimitiveType.Cube, "Leg", new Vector3(0.8f * side, 0.37f, 0f),
                        new Vector3(0.1f, 0.75f, 0.8f), Color.Lerp(wood, Color.black, 0.2f));
                    PropKit.Shape(root.transform, PrimitiveType.Cube, "Bench", new Vector3(0.9f * side, 0.45f, 0f),
                        new Vector3(0.45f, 0.08f, 1.9f), wood, euler: new Vector3(0f, 90f, 0f));
                }
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Trim", new Vector3(0f, 0.8f, 0f),
                    new Vector3(1.9f, 0.02f, 0.15f), PropKit.Accent(b.Theme), collide: false, glow: 0.7f);
                return root;
            });
        }
    }
}
