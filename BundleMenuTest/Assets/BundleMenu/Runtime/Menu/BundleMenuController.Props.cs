using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Spawning the menu's own props. They're built here and handed to the spawner, so the same
    /// "clear what I spawned" button tidies them away and the gun's Delete mode can remove them.
    /// </summary>
    public partial class BundleMenuController
    {
        /// <summary>Everything a prop needs to match your menu and follow the lobby rule.</summary>
        public PropBuild PropBuildInfo() => new PropBuild
        {
            Theme = CurrentTheme,
            WalkableLayer = Mods != null && Mods.Context.Player != null ? Mods.Context.Player.WalkableLayer : -1,
            Allowed = () => Mods == null || Mods.Allowed,
            Teleport = Blink,
        };

        /// <summary>Moves you, the way Gorilla Tag expects. False means it couldn't.</summary>
        private bool Blink(Vector3 to)
        {
            var player = Mods != null ? Mods.Context.Player : null;
            if (player == null || Mods == null || !Mods.Allowed) return false;
            var facing = Quaternion.Euler(0f, Rig?.Camera != null ? Rig.Camera.transform.eulerAngles.y : 0f, 0f);
            return player.TeleportTo(to, facing);
        }

        /// <summary>Puts a prop on the ground a couple of steps in front of you.</summary>
        public void SpawnProp(PropDef def)
        {
            if (def == null) return;
            if (def.NeedsMods && Mods != null && !Mods.Allowed)
            {
                Toast(def.Name + " only works in private or modded rooms.", ToastKind.Error);
                return;
            }

            var cam = Rig?.Camera;
            var forward = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.forward;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            var from = cam != null ? cam.transform.position : Vector3.zero;
            var spot = from + forward * 2.5f;
            // Drop it onto the floor below that spot, so it doesn't hang in the air.
            if (Physics.Raycast(spot + Vector3.up * 1.5f, Vector3.down, out var ground, 8f, ~0, QueryTriggerInteraction.Ignore))
                spot = ground.point;
            else
                spot = from + forward * 2.5f + Vector3.down * 1f;

            var facing = Quaternion.LookRotation(-forward, Vector3.up);
            var go = PropLibrary.Spawn(def, PropBuildInfo(), Spawner, spot, facing);
            Toast(go != null ? "Spawned " + def.Name : "Couldn't spawn " + def.Name, go != null ? ToastKind.Success : ToastKind.Error);
        }

        /// <summary>Loads a prop into the gun so you can place copies wherever you point.</summary>
        public void ChooseProp(PropDef def)
        {
            if (Props == null || def == null) return;
            Props.Choose(def);
            UsePropGun();
            Toast("Gun loaded with " + def.Name, ToastKind.Info);
        }

        /// <summary>Switches the gun on and selects the prop mode.</summary>
        public void UsePropGun()
        {
            if (Gun == null || Props == null) return;
            Gun.GunEnabled = true;
            for (int i = 0; i < Gun.Modes.Count; i++)
                if (Gun.Modes[i] == Props) { Gun.SelectMode(i); break; }
            dirty = true;
        }
    }
}
