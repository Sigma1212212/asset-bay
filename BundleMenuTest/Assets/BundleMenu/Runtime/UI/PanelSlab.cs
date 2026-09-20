using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Real thickness for a panel: a rounded, extruded mesh sitting just behind the UI, lit by the game's
    /// own lights. Only drawn when the menu is in the world (floating, wrist, the desktop GUI's 3D view):
    /// on a screen overlay a mesh has no place to live, so it hides itself there.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class PanelSlab : MonoBehaviour
    {
        private MeshFilter filter;
        private MeshRenderer meshRenderer;
        private Material material;
        private Vector2 builtSize;
        private float radius, depth;
        private Canvas canvas;

        private static Material template;

        /// <summary>Drop the material every slab copies from (called when the menu is ejected).</summary>
        public static void ReleaseShared()
        {
            if (template == null) return;
            if (Application.isPlaying) Destroy(template); else DestroyImmediate(template);
            template = null;
        }

        public static PanelSlab Add(RectTransform body, Color color, float cornerRadius, float thickness)
        {
            var go = new GameObject("Slab", typeof(RectTransform));
            go.layer = body.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(body, false);
            rt.SetAsFirstSibling();
            rt.Stretch();

            var slab = go.AddComponent<PanelSlab>();
            slab.radius = cornerRadius;
            slab.depth = thickness;
            slab.filter = go.AddComponent<MeshFilter>();
            slab.meshRenderer = go.AddComponent<MeshRenderer>();
            slab.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            slab.meshRenderer.receiveShadows = false;
            slab.material = MakeMaterial(color);
            slab.meshRenderer.sharedMaterial = slab.material;
            return slab;
        }

        /// <summary>A copy of the game's default lit material (from a throwaway cube), so it's shaded like the world.</summary>
        private static Material MakeMaterial(Color color)
        {
            if (template == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                template = new Material(probe.GetComponent<Renderer>().sharedMaterial) { hideFlags = HideFlags.DontSave };
                if (Application.isPlaying) Destroy(probe); else DestroyImmediate(probe);
            }
            var mat = new Material(template) { hideFlags = HideFlags.DontSave };
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            return mat;
        }

        private void LateUpdate() => Refresh();

        /// <summary>Show/hide for the current canvas and rebuild the mesh if the panel size changed.</summary>
        public void Refresh()
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            bool world = canvas != null && canvas.rootCanvas.renderMode == RenderMode.WorldSpace;
            meshRenderer.enabled = world;
            if (!world) return;

            var size = ((RectTransform)transform).rect.size;
            if (size != builtSize && size.x > 1f && size.y > 1f)
            {
                if (filter.sharedMesh != null) { if (Application.isPlaying) Destroy(filter.sharedMesh); else DestroyImmediate(filter.sharedMesh); }
                // Inset by a pixel so the mesh edge never peeks past the drawn rim.
                filter.sharedMesh = TabletFactory.RoundedSlab(size.x - 2f, size.y - 2f, radius, depth, 6);
                builtSize = size;
            }
            // Centred on the panel (shells use a centre pivot), pushed back so its front face sits just behind the UI.
            transform.localPosition = new Vector3(0f, 0f, depth * 0.5f + 0.5f);
        }

        private void OnDestroy()
        {
            if (filter != null && filter.sharedMesh != null) Destroy(filter.sharedMesh);
            if (material != null) Destroy(material);
        }
    }
}
