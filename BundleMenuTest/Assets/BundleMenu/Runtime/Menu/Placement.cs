using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    public enum MenuPlacement
    {
        ScreenRight,     // classic overlay on the right edge of the screen (mouse / keyboard)
        Floating,        // world-space panel hovering in front of you, lazily follows your view
        Wrist,           // world-space panel attached to a hand / wrist anchor
        ClickGui,        // desktop: the world panel lives hidden off-map, a camera renders it into a clickable on-screen window
    }

    /// <summary>
    /// Where the player's head and hands are. The default reads Inspector fields; game integrations
    /// (see GorillaTagRig) supply their own so nothing in the menu depends on a specific game.
    /// </summary>
    public interface IRigProvider
    {
        Camera Camera { get; }
        Transform WristAnchor { get; }            // null = simulate one in front of the camera
        IReadOnlyList<Transform> PokeTips { get; } // fingertips that can press world-space buttons

        /// <summary>Where the gun points from in VR (null = no tracked hand; desktop uses the mouse instead).</summary>
        Ray? HandAimRay { get; }
    }

    public sealed class InspectorRig : IRigProvider
    {
        private readonly BundleMenuController owner;
        public InspectorRig(BundleMenuController owner) => this.owner = owner;

        public Camera Camera => owner.viewCamera != null ? owner.viewCamera : Camera.main;
        public Transform WristAnchor => owner.wristAnchor;
        public IReadOnlyList<Transform> PokeTips => owner.pokeTips;

        public Ray? HandAimRay =>
            owner.gunHand != null ? new Ray(owner.gunHand.position, owner.gunHand.forward) : (Ray?)null;
    }

    /// <summary>
    /// Presses world-space MenuButtons with fingertips, without physics or colliders:
    /// each frame every tip is converted into each button's local space and tested against its rect.
    /// Hover within <see cref="HoverDistance"/> in front of the surface; press when the tip crosses it.
    /// </summary>
    public sealed class PokeInteractor : MonoBehaviour
    {
        public float HoverDistance = 0.05f;   // metres in front of the button
        public float PressDepth = 0.004f;     // metres; crossing this far in front of the surface counts as a press
        public float ReleaseDistance = 0.015f;// must pull back this far before the next press
        public float EdgeTolerance = 4f;      // UI units of slack around each button

        public RectTransform Root;            // only buttons under here are considered
        public IRigProvider Rig;

        private readonly List<MenuButton> buttons = new List<MenuButton>();
        private readonly Dictionary<Transform, TipState> tips = new Dictionary<Transform, TipState>();

        private sealed class TipState
        {
            public MenuButton Hovered;
            public bool Armed = true;
        }

        private void LateUpdate()
        {
            var tipList = Rig?.PokeTips;
            if (Root == null || tipList == null || tipList.Count == 0 || !Root.gameObject.activeInHierarchy)
            {
                ClearAll();
                return;
            }

            Root.GetComponentsInChildren(false, buttons);

            foreach (var tip in tipList)
            {
                if (tip == null) continue;
                if (!tips.TryGetValue(tip, out var state)) tips[tip] = state = new TipState();
                Process(tip.position, state);
            }
        }

        private void Process(Vector3 tipWorld, TipState state)
        {
            MenuButton best = null;
            float bestArea = float.MaxValue, bestFront = 0f;

            foreach (var b in buttons)
            {
                if (b == null || !b.IsInteractable()) continue;
                var rt = (RectTransform)b.transform;
                Vector3 local = rt.InverseTransformPoint(tipWorld);
                var rect = rt.rect;
                rect.xMin -= EdgeTolerance; rect.yMin -= EdgeTolerance;
                rect.xMax += EdgeTolerance; rect.yMax += EdgeTolerance;
                if (!rect.Contains(new Vector2(local.x, local.y))) continue;

                // UI faces -Z: a finger in front of the button has negative local z.
                float unitsPerMetre = 1f / Mathf.Max(1e-6f, rt.lossyScale.z);
                float front = -local.z / unitsPerMetre; // metres in front of the surface
                if (front > HoverDistance || front < -0.03f) continue;

                float area = rect.width * rect.height; // prefer the smallest (innermost) button
                if (area < bestArea) { best = b; bestArea = area; bestFront = front; }
            }

            if (best != state.Hovered)
            {
                if (state.Hovered != null) state.Hovered.SetPokeHover(false);
                if (best != null) best.SetPokeHover(true);
                state.Hovered = best;
                state.Armed = best != null && bestFront > PressDepth; // entering from behind never presses
            }

            if (best == null) return;

            if (state.Armed && bestFront <= PressDepth)
            {
                best.PokePress();
                state.Armed = false;
            }
            else if (!state.Armed && bestFront > ReleaseDistance)
            {
                state.Armed = true;
            }
        }

        private void ClearAll()
        {
            foreach (var s in tips.Values)
                if (s.Hovered != null) s.Hovered.SetPokeHover(false);
            tips.Clear();
        }

        private void OnDisable() => ClearAll();
    }
}
