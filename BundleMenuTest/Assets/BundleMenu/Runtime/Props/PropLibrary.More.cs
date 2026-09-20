using UnityEngine;

namespace BundleMenu
{
    /// <summary>More playground props: things that move you around.</summary>
    public static partial class PropLibrary
    {
        private static void AddMorePlayground()
        {
            Add("Moving platform", "Playground", "Slides back and forth and carries you with it.", b =>
            {
                var root = PropKit.Root("Moving platform", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Deck", new Vector3(0f, 0.1f, 0f),
                    new Vector3(2f, 0.2f, 2f), PropKit.Body(b.Theme));
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Stripe", new Vector3(0f, 0.21f, 0f),
                    new Vector3(1.7f, 0.02f, 0.2f), PropKit.Accent(b.Theme), collide: false, glow: 1.2f);

                var mover = root.AddComponent<Mover>();
                mover.Travel = new Vector3(0f, 0f, 7f);
                mover.Seconds = 5f;

                // Anything standing in this box travels exactly as far as the deck does.
                var zone = new GameObject("Carry");
                zone.transform.SetParent(root.transform, false);
                zone.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                var box = zone.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(2f, 2f, 2f);
                zone.AddComponent<Carrier>().Platform = mover;
                return root;
            }, needsMods: true);

            Add("Fan", "Playground", "A column of air that holds you up - stand over it and float.", b =>
            {
                var root = PropKit.Root("Fan", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Housing", new Vector3(0f, 0.2f, 0f),
                    new Vector3(1.3f, 0.2f, 1.3f), PropKit.Body(b.Theme));
                var blades = PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Hub", new Vector3(0f, 0.42f, 0f),
                    new Vector3(0.2f, 0.03f, 0.2f), PropKit.Dark(b.Theme), collide: false);
                for (int i = 0; i < 4; i++)
                    PropKit.Shape(blades, PrimitiveType.Cube, "Blade", new Vector3(0f, 0f, 0f),
                        new Vector3(4.6f, 1.2f, 1.1f), PropKit.Accent(b.Theme), collide: false,
                        euler: new Vector3(0f, i * 45f, 12f));
                blades.gameObject.AddComponent<Spinner>().Speed = new Vector3(0f, 520f, 0f);

                var column = new GameObject("Air");
                column.transform.SetParent(root.transform, false);
                column.transform.localPosition = new Vector3(0f, 4f, 0f);
                var box = column.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1.6f, 8f, 1.6f);
                var draft = column.AddComponent<Updraft>();
                draft.Lift = 22f;
                draft.Allowed = b.Allowed;
                return root;
            }, needsMods: true);

            Add("Conveyor strip", "Playground", "A moving floor that carries you along it.", b =>
            {
                var root = PropKit.Root("Conveyor strip", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Belt", new Vector3(0f, 0.08f, 0f),
                    new Vector3(1.6f, 0.16f, 6f), PropKit.Dark(b.Theme));
                for (int i = 0; i < 8; i++)
                    PropKit.Shape(root.transform, PrimitiveType.Cube, "Arrow", new Vector3(0f, 0.17f, -2.6f + i * 0.75f),
                        new Vector3(0.5f, 0.02f, 0.18f), PropKit.Accent(b.Theme), collide: false, glow: 1.1f);

                var zone = new GameObject("Belt zone");
                zone.transform.SetParent(root.transform, false);
                zone.transform.localPosition = new Vector3(0f, 0.7f, 0f);
                var box = zone.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1.6f, 1.2f, 6f);
                var belt = zone.AddComponent<Conveyor>();
                belt.Speed = 6f;
                belt.Direction = Vector3.forward;
                belt.Allowed = b.Allowed;
                return root;
            }, needsMods: true);

            Add("Ice patch", "Playground", "Slippery ground - run on and keep sliding.", b =>
            {
                var root = PropKit.Root("Ice patch", b.WalkableLayer);
                var slab = PropKit.Shape(root.transform, PrimitiveType.Cube, "Ice", new Vector3(0f, 0.05f, 0f),
                    new Vector3(4f, 0.1f, 4f), new Color(0.6f, 0.85f, 1f, 1f), glow: 0.4f);
                var collider = slab.GetComponent<Collider>();
                if (collider != null) collider.sharedMaterial = PropKit.Slippery();
                for (int i = 0; i < 5; i++)
                    PropKit.Shape(root.transform, PrimitiveType.Cube, "Crack",
                        new Vector3(Random.Range(-1.6f, 1.6f), 0.11f, Random.Range(-1.6f, 1.6f)),
                        new Vector3(0.04f, 0.01f, Random.Range(0.6f, 1.8f)), Color.white, collide: false,
                        euler: new Vector3(0f, Random.Range(0f, 180f), 0f));
                return root;
            });

            Add("Cannon", "Playground", "Climb in the mouth and it fires you across the map.", b =>
            {
                var root = PropKit.Root("Cannon", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cube, "Carriage", new Vector3(0f, 0.3f, -0.2f),
                    new Vector3(1.1f, 0.6f, 1.4f), PropKit.Dark(b.Theme));
                for (int side = -1; side <= 1; side += 2)
                    PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Wheel", new Vector3(0.6f * side, 0.35f, -0.3f),
                        new Vector3(0.7f, 0.08f, 0.7f), PropKit.Body(b.Theme), euler: new Vector3(0f, 0f, 90f));

                var barrel = PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Barrel", new Vector3(0f, 1.05f, 0.5f),
                    new Vector3(0.5f, 0.95f, 0.5f), PropKit.Body(b.Theme), euler: new Vector3(45f, 0f, 0f));
                PropKit.Shape(barrel, PrimitiveType.Cylinder, "Rim", new Vector3(0f, 0.95f, 0f),
                    new Vector3(1.15f, 0.06f, 1.15f), PropKit.Accent(b.Theme), collide: false, glow: 1.2f);

                // The mouth: step in and you're fired along the barrel.
                var mouth = new GameObject("Mouth");
                mouth.transform.SetParent(root.transform, false);
                mouth.transform.localPosition = new Vector3(0f, 1.75f, 1.2f);
                mouth.transform.localRotation = Quaternion.Euler(-45f, 0f, 0f);
                var box = mouth.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(1f, 1f, 1.2f);
                var booster = mouth.AddComponent<Booster>();
                booster.Power = 30f;
                booster.Direction = Vector3.forward;
                booster.Allowed = b.Allowed;
                return root;
            }, needsMods: true);

            Add("Scaffold tower", "Playground", "Three floors with rungs between them - good practice for climbing.", b =>
            {
                var root = PropKit.Root("Scaffold tower", b.WalkableLayer);
                var frame = PropKit.Body(b.Theme);
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                        PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Post", new Vector3(0.9f * x, 3f, 0.9f * z),
                            new Vector3(0.11f, 3f, 0.11f), frame);
                for (int floor = 1; floor <= 3; floor++)
                {
                    float y = floor * 1.9f;
                    PropKit.Shape(root.transform, PrimitiveType.Cube, "Floor", new Vector3(0f, y, 0f),
                        new Vector3(2.1f, 0.12f, 2.1f), floor % 2 == 0 ? PropKit.Dark(b.Theme) : frame);
                    PropKit.Shape(root.transform, PrimitiveType.Cube, "Edge", new Vector3(0f, y + 0.1f, 1.02f),
                        new Vector3(2.1f, 0.08f, 0.08f), PropKit.Accent(b.Theme), collide: false, glow: 0.9f);
                    for (int rung = 0; rung < 4; rung++)
                        PropKit.Shape(root.transform, PrimitiveType.Cube, "Rung",
                            new Vector3(0.9f, y - 1.5f + rung * 0.45f, 0.9f),
                            new Vector3(0.5f, 0.06f, 0.5f), PropKit.Accent2(b.Theme), euler: new Vector3(0f, 45f, 0f));
                }
                return root;
            });

            Add("Checkpoint flag", "Playground", "Touch it and your checkpoint is saved there.", b =>
            {
                var root = PropKit.Root("Checkpoint flag", b.WalkableLayer);
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Pole", new Vector3(0f, 1.2f, 0f),
                    new Vector3(0.08f, 1.2f, 0.08f), PropKit.Dark(b.Theme));
                PropKit.Shape(root.transform, PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.06f, 0f),
                    new Vector3(0.7f, 0.06f, 0.7f), PropKit.Body(b.Theme));
                var flag = PropKit.Shape(root.transform, PrimitiveType.Cube, "Flag", new Vector3(0.42f, 2.05f, 0f),
                    new Vector3(0.8f, 0.5f, 0.03f), PropKit.Accent(b.Theme), collide: false, glow: 1.1f);

                var zone = new GameObject("Touch");
                zone.transform.SetParent(root.transform, false);
                zone.transform.localPosition = new Vector3(0f, 1f, 0f);
                var capsule = zone.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.radius = 0.7f;
                capsule.height = 2.2f;
                var checkpoint = zone.AddComponent<CheckpointFlag>();
                checkpoint.Touched = b.SaveCheckpoint;
                checkpoint.Glow = flag.GetComponent<Renderer>();
                checkpoint.Lit = PropKit.Accent(b.Theme);
                return root;
            });
        }
    }
}
