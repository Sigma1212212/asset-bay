using System;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Prop gun: puts down whichever surprise prop you've picked, standing upright on whatever you point
    /// at and facing you. Pick the prop on the Props page (or right here, by cycling through them).
    /// </summary>
    public sealed class PropGunMode : GunMode
    {
        private readonly AssetSpawner spawner;
        private readonly Func<MenuTheme> theme;
        private int index;

        public PropGunMode(AssetSpawner spawner, Func<MenuTheme> theme)
        {
            this.spawner = spawner;
            this.theme = theme;
        }

        public override string Name => "Props";
        public override string Hint => "Places the prop you picked on the Props page. Right-click the mode row to change it.";
        public override float Cooldown => 0.3f;

        /// <summary>Everything the build needs: theme colours, the layer you can stand on, the lobby rule.</summary>
        public Func<PropBuild> Build;

        public PropDef Chosen =>
            PropLibrary.All.Count == 0 ? null : PropLibrary.All[Mathf.Clamp(index, 0, PropLibrary.All.Count - 1)];

        public void Choose(PropDef def)
        {
            for (int i = 0; i < PropLibrary.All.Count; i++)
                if (PropLibrary.All[i] == def) { index = i; return; }
        }

        public void Cycle(int dir)
        {
            int count = PropLibrary.All.Count;
            if (count == 0) return;
            index = ((index + dir) % count + count) % count;
        }

        public override string Describe(GunHit hit) => Chosen != null ? Chosen.Name : "no props";

        public override Color? Tint(GunHit hit) => hit.HasHit ? (Color?)null : new Color(0.55f, 0.55f, 0.6f);

        public override string Fire(GunHit hit)
        {
            var def = Chosen;
            if (def == null) return "No props to place.";
            if (!hit.HasHit) return "Point at the ground.";
            if (Build == null) return "Props aren't ready yet.";

            // Stand it up on floors, and have it face back toward you.
            var facing = Vector3.ProjectOnPlane(-hit.Ray.direction, Vector3.up);
            var rotation = facing.sqrMagnitude > 0.001f ? Quaternion.LookRotation(facing, Vector3.up) : Quaternion.identity;
            var go = PropLibrary.Spawn(def, Build(), spawner, hit.Point, rotation);
            return go != null ? "Placed " + def.Name : "Couldn't place " + def.Name;
        }
    }
}
