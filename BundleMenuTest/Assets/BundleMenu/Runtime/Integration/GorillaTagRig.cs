using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Finds the Gorilla Tag player's camera and hands by reflection. There is deliberately no compile-time
    /// reference to the game: when an update renames something, this returns null / falls back to the camera
    /// instead of throwing MissingFieldException and killing the whole menu.
    ///
    /// Members used (as of the 6000.2 build): GorillaTagger.Instance, .mainCamera, .leftHandTransform,
    /// .rightHandTransform, .leftHandTriggerCollider, .rightHandTriggerCollider (the fingertip spheres the
    /// game itself uses for pressing buttons).
    /// </summary>
    public sealed class GorillaTagRig : IRigProvider
    {
        private readonly Type taggerType;
        private readonly Func<object> instance;

        private Camera camera;
        private Transform leftHand, rightHand;
        private readonly List<Transform> tips = new List<Transform>();

        private GorillaTagRig(Type taggerType, Func<object> instance)
        {
            this.taggerType = taggerType;
            this.instance = instance;
        }

        /// <summary>Returns null when not running inside Gorilla Tag.</summary>
        public static GorillaTagRig TryCreate()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name == "Assembly-CSharp")
                .Select(a => a.GetType("GorillaTagger", false))
                .FirstOrDefault(t => t != null);
            if (type == null) return null;

            const BindingFlags S = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var prop = type.GetProperty("Instance", S);
            var field = type.GetField("_instance", S) ?? type.GetField("instance", S);
            Func<object> get = prop != null ? () => prop.GetValue(null)
                             : field != null ? (Func<object>)(() => field.GetValue(null))
                             : null;
            return get == null ? null : new GorillaTagRig(type, get);
        }

        public Camera Camera
        {
            get
            {
                if (camera == null) Refresh();
                return camera != null ? camera : Camera.main;
            }
        }

        public Transform WristAnchor
        {
            get
            {
                if (leftHand == null) Refresh();
                return leftHand;
            }
        }

        /// <summary>Gorilla Tag's hand transforms point along -up (the back of the hand faces +up).</summary>
        public Ray? HandAimRay
        {
            get
            {
                if (rightHand == null) Refresh();
                if (rightHand == null) return null;
                var dir = -rightHand.up;
                return new Ray(rightHand.position + dir * 0.05f, dir);
            }
        }

        public IReadOnlyList<Transform> PokeTips
        {
            get
            {
                if (tips.Count == 0 || tips.Any(t => t == null)) Refresh();
                return tips;
            }
        }

        private float nextRefresh;

        private void Refresh()
        {
            if (Time.unscaledTime < nextRefresh) return; // don't hammer reflection while the rig is still spawning
            nextRefresh = Time.unscaledTime + 1f;

            object tagger;
            try { tagger = instance(); } catch { return; }
            if (tagger == null) return;

            var camRoot = AsTransform(Member(tagger, "mainCamera"));
            var cam = camRoot != null ? camRoot.GetComponentInChildren<Camera>(true) : null;
            if (cam != null) camera = cam;
            var hand = AsTransform(Member(tagger, "leftHandTransform"));
            if (hand != null) leftHand = hand;
            var rHand = AsTransform(Member(tagger, "rightHandTransform"));
            if (rHand != null) rightHand = rHand;

            tips.Clear();
            // Only the right fingertip presses: the menu sits on the left wrist, so the left finger would
            // constantly brush it.
            var right = AsTransform(Member(tagger, "rightHandTriggerCollider"));
            if (right == null) right = AsTransform(Member(tagger, "rightHandTransform"));
            if (right != null) tips.Add(right);
        }

        private object Member(object target, string name)
        {
            const BindingFlags I = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            try
            {
                var f = taggerType.GetField(name, I);
                if (f != null) return f.GetValue(target);
                var p = taggerType.GetProperty(name, I);
                return p?.GetValue(target);
            }
            catch { return null; }
        }

        private static Transform AsTransform(object o)
        {
            switch (o)
            {
                case Transform t: return t;
                case Component c: return c != null ? c.transform : null;
                case GameObject g: return g != null ? g.transform : null;
                default: return null;
            }
        }

        /// <summary>Call every frame (main thread): the game creates its player a few seconds after launch.</summary>
        public void Tick()
        {
            if (camera == null || leftHand == null) Refresh();
        }
    }
}
