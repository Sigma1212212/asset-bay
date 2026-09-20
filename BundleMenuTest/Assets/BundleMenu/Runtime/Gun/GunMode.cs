using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// One thing the gun can do. Override what you need: most modes only fill in <see cref="Name"/> and
    /// <see cref="Fire"/>. Set <see cref="ChargeTime"/> to make the trigger charge up, or
    /// <see cref="Automatic"/> to keep firing while it's held.
    ///
    /// Modes run on your own machine only - they never touch anybody else's game.
    /// </summary>
    public abstract class GunMode
    {
        public abstract string Name { get; }

        /// <summary>One line of help shown on the gun page.</summary>
        public virtual string Hint => "";

        /// <summary>The gun this mode is registered with (set for you).</summary>
        public GunLib Gun { get; internal set; }

        /// <summary>Shortest gap between shots, in seconds.</summary>
        public virtual float Cooldown => 0.15f;

        /// <summary>Seconds to a full charge. 0 means it fires the moment you pull the trigger.</summary>
        public virtual float ChargeTime => 0f;

        /// <summary>Keep firing while the trigger is held.</summary>
        public virtual bool Automatic => false;

        /// <summary>Colour for the laser and reticle while aiming (null = the theme's accent).</summary>
        public virtual Color? Tint(GunHit hit) => null;

        /// <summary>Short text for the menu, e.g. "3.2 m" or "nothing there".</summary>
        public virtual string Describe(GunHit hit) => "";

        /// <summary>The shot. Return a line for the menu's status, or null for silence.</summary>
        public abstract string Fire(GunHit hit);

        /// <summary>The frame the trigger goes down (before the shot).</summary>
        public virtual void Press(GunHit hit) { }

        /// <summary>Every frame the trigger is held (carrying, charging).</summary>
        public virtual void Hold(GunHit hit) { }

        /// <summary>The frame the trigger is let go (drop, throw).</summary>
        public virtual void Release(GunHit hit) { }

        /// <summary>Every frame you're aiming, fired or not (previews).</summary>
        public virtual void Aiming(GunHit hit) { }

        public virtual void OnSelected(GunLib gun) { }
        public virtual void OnDeselected() { }
    }
}
