using UnityEngine;
using UnityEngine.Rendering;

namespace BundleMenu
{
    /// <summary>
    /// Small helpers for building props out of Unity's own primitives. Every material made here is named
    /// "(menu)" so <see cref="SelfCleanMaterials"/> knows it can throw it away, and nothing casts shadows
    /// (cheaper, and Gorilla Tag's maps are already lit).
    /// </summary>
    public static class PropKit
    {
        public static GameObject Root(string name, int layer = -1)
        {
            var go = new GameObject(name);
            if (layer >= 0) go.layer = layer;
            go.AddComponent<SelfCleanMaterials>();
            return go;
        }

        public static Transform Shape(Transform parent, PrimitiveType type, string name,
                                      Vector3 position, Vector3 scale, Color color,
                                      bool collide = true, Vector3? euler = null, float glow = 0f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = scale;
            if (parent != null) go.layer = parent.gameObject.layer;

            var collider = go.GetComponent<Collider>();
            if (collider != null && !collide)
            {
                Kill(collider);
            }
            else if (collide && type == PrimitiveType.Cylinder)
            {
                // A cylinder comes with a capsule collider: rounded ends, so flat discs wobble and
                // platforms are the wrong shape. Its own mesh is a much better fit.
                var filter = go.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    Kill(collider);
                    var mesh = go.AddComponent<MeshCollider>();
                    mesh.sharedMesh = filter.sharedMesh;
                    mesh.convex = true;
                }
            }

            Paint(go, color, glow);
            return go.transform;
        }

        /// <summary>A surface with almost no grip, for ice.</summary>
#if UNITY_6000_0_OR_NEWER
        public static PhysicsMaterial Slippery() =>
            new PhysicsMaterial("ice (menu)")
            {
                dynamicFriction = 0.02f,
                staticFriction = 0.02f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
            };
#else
        public static PhysicMaterial Slippery() =>
            new PhysicMaterial("ice (menu)")
            {
                dynamicFriction = 0.02f,
                staticFriction = 0.02f,
                frictionCombine = PhysicMaterialCombine.Minimum,
            };
#endif

        /// <summary>
        /// Takes a component off now rather than at the end of the frame. Props are built and used in the
        /// same breath, so a collider that lingers for a frame is a collider in the wrong shape.
        /// </summary>
        private static void Kill(Object thing) => Object.DestroyImmediate(thing);

        /// <summary>Gives an object its own material in the given colour (bright colours glow a little).</summary>
        public static Material Paint(GameObject go, Color color, float glow = 0f)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return null;
            var mat = new Material(renderer.sharedMaterial) { name = "prop (menu)" };
            if (mat.HasProperty("_Color")) mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (glow > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * glow);
            }
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return mat;
        }

        /// <summary>A soft light, the kind that makes a campfire or a lamp read at night.</summary>
        public static Light Lamp(Transform parent, Color color, float range, float intensity, Vector3 position)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>Makes something physical: mass, drag and a sensible collision mode.</summary>
        public static Rigidbody Physical(GameObject go, float mass, float drag = 0.05f)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.drag = drag;
            rb.angularDrag = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            return rb;
        }

        /// <summary>
        /// A springy surface. Unity 6 (what Gorilla Tag runs) renamed this class, so both spellings are
        /// here and the right one is picked when the menu is compiled.
        /// </summary>
#if UNITY_6000_0_OR_NEWER
        public static PhysicsMaterial Bouncy(float bounce) =>
            new PhysicsMaterial("bouncy (menu)")
            {
                bounciness = bounce,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Minimum,
            };
#else
        public static PhysicMaterial Bouncy(float bounce) =>
            new PhysicMaterial("bouncy (menu)")
            {
                bounciness = bounce,
                bounceCombine = PhysicMaterialCombine.Maximum,
                frictionCombine = PhysicMaterialCombine.Minimum,
            };
#endif

        /// <summary>The theme's colours, with a safe fallback when there's no theme yet.</summary>
        public static Color Accent(MenuTheme theme) => theme != null ? theme.Accent : new Color(0.25f, 0.9f, 1f);
        public static Color Accent2(MenuTheme theme) => theme != null ? theme.Accent2 : new Color(0.55f, 0.35f, 1f);
        public static Color Body(MenuTheme theme) => theme != null ? theme.PanelTop : new Color(0.16f, 0.17f, 0.22f);
        public static Color Dark(MenuTheme theme) => theme != null ? theme.PanelBottom : new Color(0.08f, 0.09f, 0.13f);
    }
}
