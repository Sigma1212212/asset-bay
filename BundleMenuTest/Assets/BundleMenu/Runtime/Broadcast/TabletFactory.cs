using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Builds a tablet from a TabletConfig. The front faces -Z (toward the viewer).
    /// Part names the runtime relies on: Body, Bezel, Screen, Accent, CameraDot, Button*, Handle, Kickstand, Bumper*.
    /// </summary>
    public static class TabletFactory
    {
        public struct Materials
        {
            public Material Body, Bezel, Accent, Trim, Screen;
        }

        public static GameObject Build(TabletConfig c, Materials m)
        {
            var root = new GameObject("Tablet");
            float sw = c.width, sh = c.width / c.aspect;
            float bw = sw + c.bezel * 2f, bh = sh + c.bezel * 2f;
            float r = Mathf.Min(c.cornerRadius, Mathf.Min(bw, bh) * 0.5f - 0.001f);
            float t = c.thickness;

            AddMesh(root, "Body", RoundedSlab(bw, bh, r, t, c.cornerSegments), m.Body, new Vector3(0f, 0f, t * 0.5f));

            float innerR = Mathf.Max(0f, r - c.bezel * 0.5f);
            AddMesh(root, "Bezel", RoundedSlab(sw + c.bezel * 0.5f, sh + c.bezel * 0.5f, innerR, 0.002f, c.cornerSegments),
                m.Bezel, new Vector3(0f, 0f, -0.001f));

            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Screen";
            RemoveCollider(screen);
            screen.transform.SetParent(root.transform, false);
            screen.transform.localScale = new Vector3(sw, sh, 1f);
            screen.transform.localPosition = new Vector3(0f, 0f, -0.0025f);
            screen.GetComponent<Renderer>().sharedMaterial = m.Screen;

            if (c.accentStrip)
                AddBox(root, "Accent", new Vector3(sw * 0.35f, 0.003f, 0.002f), new Vector3(0f, -bh * 0.5f + c.bezel * 0.35f, -0.0015f), m.Accent);

            if (c.cameraDot && c.bezel >= 0.02f) // needs a border wide enough to sit in
                AddBox(root, "CameraDot", new Vector3(0.006f, 0.006f, 0.002f), new Vector3(0f, bh * 0.5f - c.bezel * 0.5f, -0.0015f), m.Bezel);

            for (int i = 0; i < c.sideButtons; i++)
                AddBox(root, "Button" + i, new Vector3(0.006f, 0.03f, t * 0.5f),
                    new Vector3(bw * 0.5f + 0.002f, bh * 0.25f - i * 0.04f, t * 0.5f), m.Trim);

            if (c.handle)
                AddBox(root, "Handle", new Vector3(bw * 0.5f, 0.02f, 0.02f), new Vector3(0f, -bh * 0.5f - 0.016f, t * 0.5f), m.Trim);

            if (c.kickstand)
            {
                var stand = AddBox(root, "Kickstand", new Vector3(bw * 0.4f, bh * 0.55f, 0.006f),
                    new Vector3(0f, -bh * 0.1f, t + 0.02f), m.Trim);
                stand.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            }

            if (c.bumpers)
            {
                float s = Mathf.Max(0.03f, r * 1.2f);
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    AddBox(root, $"Bumper{(x > 0 ? "R" : "L")}{(y > 0 ? "T" : "B")}", new Vector3(s, s, t + 0.008f),
                        new Vector3(x * (bw * 0.5f - s * 0.3f), y * (bh * 0.5f - s * 0.3f), t * 0.5f), m.Trim);
            }
            return root;
        }

        private static GameObject AddMesh(GameObject root, string name, Mesh mesh, Material mat, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        private static GameObject AddBox(GameObject root, string name, Vector3 size, Vector3 pos, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            RemoveCollider(go);
            go.transform.SetParent(root.transform, false);
            go.transform.localScale = size;
            go.transform.localPosition = pos;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static void RemoveCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col == null) return;
            if (Application.isPlaying) Object.Destroy(col); else Object.DestroyImmediate(col);
        }

        /// <summary>A rounded rectangle extruded to a given thickness, centred on the origin.</summary>
        public static Mesh RoundedSlab(float width, float height, float radius, float depth, int segments)
        {
            var outline = new List<Vector2>();
            float hw = width * 0.5f, hh = height * 0.5f;
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(hw, hh));
            var centres = new[]
            {
                new Vector2(hw - radius, hh - radius), new Vector2(-hw + radius, hh - radius),
                new Vector2(-hw + radius, -hh + radius), new Vector2(hw - radius, -hh + radius),
            };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= segments; i++)
                {
                    float a = (c * 90f + 90f * i / segments) * Mathf.Deg2Rad;
                    outline.Add(centres[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }

            int n = outline.Count;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float zf = -depth * 0.5f, zb = depth * 0.5f;

            // front cap (faces -Z) and back cap (faces +Z), fanned from the centre
            int front = verts.Count;
            verts.Add(new Vector3(0f, 0f, zf));
            foreach (var p in outline) verts.Add(new Vector3(p.x, p.y, zf));
            for (int i = 0; i < n; i++) { tris.Add(front); tris.Add(front + 1 + (i + 1) % n); tris.Add(front + 1 + i); }

            int back = verts.Count;
            verts.Add(new Vector3(0f, 0f, zb));
            foreach (var p in outline) verts.Add(new Vector3(p.x, p.y, zb));
            for (int i = 0; i < n; i++) { tris.Add(back); tris.Add(back + 1 + i); tris.Add(back + 1 + (i + 1) % n); }

            // side wall
            int side = verts.Count;
            foreach (var p in outline) { verts.Add(new Vector3(p.x, p.y, zf)); verts.Add(new Vector3(p.x, p.y, zb)); }
            for (int i = 0; i < n; i++)
            {
                int a = side + i * 2, b = side + ((i + 1) % n) * 2;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(b); tris.Add(b + 1); tris.Add(a + 1);
            }

            var mesh = new Mesh { name = "RoundedSlab" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
