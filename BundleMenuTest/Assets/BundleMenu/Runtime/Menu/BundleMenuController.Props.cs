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
            SaveCheckpoint = SaveCheckpointHere,
            Surprise = SpawnSomethingRandom,
        };

        private void SaveCheckpointHere()
        {
            if (Mods == null) return;
            Toast(Checkpoint.Save(Mods.Context), ToastKind.Success);
            dirty = true;
        }

        /// <summary>What the surprise chest drops: any prop but another chest.</summary>
        private void SpawnSomethingRandom(Vector3 where)
        {
            var props = PropLibrary.All;
            if (props.Count == 0) return;
            PropDef pick = null;
            for (int tries = 0; tries < 8 && pick == null; tries++)
            {
                var candidate = props[UnityEngine.Random.Range(0, props.Count)];
                if (candidate.Name == "Surprise chest") continue;
                if (candidate.NeedsMods && Mods != null && !Mods.Allowed) continue;
                pick = candidate;
            }
            if (pick == null) return;
            var spot = where + Vector3.up * 0.4f;
            PropLibrary.Spawn(pick, PropBuildInfo(), Spawner, spot, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            Toast("The chest had a " + pick.Name.ToLowerInvariant() + " in it", ToastKind.Success);
            dirty = true;
        }

        /// <summary>Moves you, the way Gorilla Tag expects. False means it couldn't.</summary>
        private bool Blink(Vector3 to)
        {
            var player = Mods != null ? Mods.Context.Player : null;
            if (player == null || Mods == null || !Mods.Allowed) return false;
            var facing = Quaternion.Euler(0f, Rig?.Camera != null ? Rig.Camera.transform.eulerAngles.y : 0f, 0f);
            return player.TeleportTo(to, facing);
        }

        /// <summary>The prop self-test, made the first time it's asked for.</summary>
        public PropSelfTest PropTest { get; private set; }

        /// <summary>
        /// Checks every prop where you're standing - well above the map, so nothing of the game is
        /// touched and nobody else sees a thing.
        /// </summary>
        public void TestProps()
        {
            if (PropTest == null)
            {
                PropTest = gameObject.AddComponent<PropSelfTest>();
                PropTest.Build = PropBuildInfo;
                PropTest.Where = () => (Rig?.Camera != null ? Rig.Camera.transform.position : Vector3.zero) + Vector3.up * 400f;
                PropTest.Progress = _ => dirty = true;
                PropTest.Finished = () =>
                {
                    Toast(PropTest.Summary, PropTest.Failed > 0 ? ToastKind.Error : ToastKind.Success);
                    dirty = true;
                };
            }
            if (PropTest.Running) return;
            Toast("Checking every prop...", ToastKind.Info);
            PropTest.Run();
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
