using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BundleMenu
{
    public enum LobbyKind { NotGorillaTag, Offline, Private, Modded, Public }

    /// <summary>
    /// Reflection bridge to your own Gorilla Tag player (GorillaLocomotion.GTPlayer) and the room you're in
    /// (NetworkSystem). No compile-time reference to the game, so when an update renames something, the
    /// affected mod simply reports "unavailable" instead of the DLL failing to load.
    ///
    /// Everything here touches only the local player.
    /// Members used (Gorilla Tag, Unity 6000.2 build): GTPlayer.Instance, playerRigidBody, bodyCollider,
    /// locomotionEnabledLayers, jumpMultiplier, maxJumpSpeed, SetScaleMultiplier, ScaleMultiplier,
    /// TeleportTo(Vector3, Quaternion, bool, bool); NetworkSystem.Instance, InRoom, SessionIsPrivate, GameModeString.
    /// </summary>
    public sealed class GorillaTagPlayer
    {
        private const BindingFlags Pub = BindingFlags.Public | BindingFlags.Instance;

        private readonly Type playerType, networkType;
        private readonly PropertyInfo playerInstance, rigidbodyProp, jumpMultProp, scaleProp;
        private readonly FieldInfo bodyColliderField, headColliderField, layersField, maxJumpField, networkInstance;
        private readonly Type rigType;
        private readonly FieldInfo rigOffline, rigMine, rigBody, rigName;
        private readonly PropertyInfo rigScale;
        private readonly MethodInfo setScale, teleport;
        private readonly PropertyInfo inRoom, isPrivate, gameMode;

        private GorillaTagPlayer(Type player, Type network)
        {
            playerType = player;
            networkType = network;
            playerInstance = player.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            rigidbodyProp = player.GetProperty("playerRigidBody", Pub);
            jumpMultProp = player.GetProperty("jumpMultiplier", Pub);
            scaleProp = player.GetProperty("ScaleMultiplier", Pub);
            bodyColliderField = player.GetField("bodyCollider", Pub);
            headColliderField = player.GetField("headCollider", Pub);

            // Other players' rigs, for ESP (VRRig: isOfflineVRRig, isMyPlayer, bodyTransform, playerNameVisible, scaleFactor).
            rigType = player.Assembly.GetType("VRRig", false);
            if (rigType != null)
            {
                rigOffline = rigType.GetField("isOfflineVRRig", Pub);
                rigMine = rigType.GetField("isMyPlayer", Pub);
                rigBody = rigType.GetField("bodyTransform", Pub);
                rigName = rigType.GetField("playerNameVisible", Pub);
                rigScale = rigType.GetProperty("scaleFactor", Pub);
            }
            layersField = player.GetField("locomotionEnabledLayers", Pub);
            maxJumpField = player.GetField("maxJumpSpeed", Pub);
            setScale = player.GetMethod("SetScaleMultiplier", Pub, null, new[] { typeof(float) }, null);
            teleport = player.GetMethod("TeleportTo", Pub, null,
                new[] { typeof(Vector3), typeof(Quaternion), typeof(bool), typeof(bool) }, null);

            if (network != null)
            {
                networkInstance = network.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                inRoom = network.GetProperty("InRoom", Pub);
                isPrivate = network.GetProperty("SessionIsPrivate", Pub);
                gameMode = network.GetProperty("GameModeString", Pub);
            }
        }

        /// <summary>Null when not running inside Gorilla Tag.</summary>
        public static GorillaTagPlayer TryCreate()
        {
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            var player = asm?.GetType("GorillaLocomotion.GTPlayer", false);
            if (player == null) return null;
            return new GorillaTagPlayer(player, asm.GetType("NetworkSystem", false));
        }

        private object Player => Safe(() => playerInstance?.GetValue(null));

        public bool Ready => Player != null;

        public Rigidbody Body => Safe(() => rigidbodyProp?.GetValue(Player) as Rigidbody);

        public Collider BodyCollider => Safe(() => bodyColliderField?.GetValue(Player) as Collider);

        public Collider HeadCollider => Safe(() => headColliderField?.GetValue(Player) as Collider);

        /// <summary>Layers your hands and body collide with. Setting it to 0 lets you pass through everything.</summary>
        public int? LocomotionLayers
        {
            get => Safe(() => layersField?.GetValue(Player) is LayerMask m ? m.value : (int?)null);
            set { if (value != null) Safe(() => { layersField?.SetValue(Player, (LayerMask)value.Value); return 0; }); }
        }

        public struct OtherPlayer
        {
            public Transform Body;
            public string Name;
            public float Scale;
        }

        /// <summary>Everyone in the room except you. Cheap enough to call a few times a second, not every frame.</summary>
        public List<OtherPlayer> OtherPlayers()
        {
            var result = new List<OtherPlayer>();
            if (rigType == null) return result;
            UnityEngine.Object[] rigs;
            try { rigs = UnityEngine.Object.FindObjectsByType(rigType, FindObjectsSortMode.None); }
            catch { return result; }

            foreach (var rig in rigs)
            {
                if (rig == null) continue;
                if (Safe(() => (bool?)rigOffline?.GetValue(rig)) == true) continue;
                if (Safe(() => (bool?)rigMine?.GetValue(rig)) == true) continue;
                var body = Safe(() => rigBody?.GetValue(rig) as Transform);
                if (body == null || !body.gameObject.activeInHierarchy) continue;
                result.Add(new OtherPlayer
                {
                    Body = body,
                    Name = Safe(() => rigName?.GetValue(rig) as string) ?? "player",
                    Scale = Safe(() => rigScale?.GetValue(rig) as float?) ?? 1f,
                });
            }
            return result;
        }

        /// <summary>A physics layer the player walks on (first layer in locomotionEnabledLayers), or -1.</summary>
        public int WalkableLayer
        {
            get
            {
                var value = Safe(() => layersField?.GetValue(Player));
                if (value is LayerMask mask)
                    for (int i = 0; i < 32; i++)
                        if ((mask.value & (1 << i)) != 0) return i;
                return -1;
            }
        }

        public float? JumpMultiplier
        {
            get => Safe(() => jumpMultProp?.GetValue(Player) as float?);
            set { if (value != null) Safe(() => { jumpMultProp?.SetValue(Player, value.Value); return 0; }); }
        }

        public float? MaxJumpSpeed
        {
            get => Safe(() => maxJumpField?.GetValue(Player) as float?);
            set { if (value != null) Safe(() => { maxJumpField?.SetValue(Player, value.Value); return 0; }); }
        }

        public float? Scale => Safe(() => scaleProp?.GetValue(Player) as float?);

        public bool SetScale(float s) => Safe(() => { setScale.Invoke(Player, new object[] { s }); return true; });

        public bool TeleportTo(Vector3 position, Quaternion rotation) =>
            Safe(() => { teleport.Invoke(Player, new object[] { position, rotation, false, false }); return true; });

        /// <summary>What kind of room you're in. Modded/private/offline are where mods are allowed.</summary>
        public LobbyKind Lobby
        {
            get
            {
                var net = Safe(() => networkInstance?.GetValue(null));
                if (net == null) return LobbyKind.Offline;
                bool room = Safe(() => (bool?)inRoom?.GetValue(net)) ?? false;
                if (!room) return LobbyKind.Offline;
                if (Safe(() => (bool?)isPrivate?.GetValue(net)) == true) return LobbyKind.Private;
                string mode = Safe(() => gameMode?.GetValue(net) as string) ?? "";
                return mode.IndexOf("MODDED", StringComparison.OrdinalIgnoreCase) >= 0 ? LobbyKind.Modded : LobbyKind.Public;
            }
        }

        private static T Safe<T>(Func<T> f)
        {
            try { return f(); }
            catch { return default; }
        }
    }
}
