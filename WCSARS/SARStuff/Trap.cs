using System.Collections.Generic;
using System;
using WCSARS;
namespace SARStuff
{
    /// <summary>
    /// Represents a deployable object in the world. (such as bananas as skunk grenades)
    /// </summary>
    public class Trap
    {
        /// <summary>
        /// Which deployable-type this Trap is.
        /// </summary>
        public readonly TrapType TrapType;

        /// <summary>
        /// Where in the world this Trap exists.
        /// </summary>
        public readonly Vector2 Position;

        /// <summary>
        /// The player who owns this Trap.
        /// </summary>
        public readonly short OwnerID;

        /// <summary>
        /// How close one has to be for this Trap to take effect.
        /// </summary>
        public readonly float EffectRadius;

        /// <summary>
        /// The remaining amount of time this Trap has before it disappears.
        /// </summary>
        public float RemainingTime;

        /// <summary>
        /// Index in WeaponJSON for this trap (used for kills and junk)
        /// </summary>
        public readonly short WeaponID;

        /// <summary>
        /// ID of this throwable in the sent Player's ThrowableCounter.
        /// </summary>
        public readonly short ThrowableID;

        /// <summary>
        /// Players who have touched this particular Trap (skunk nades only)
        /// </summary>
        public Dictionary<Player, DateTime> HitPlayers;

        /// <summary>
        /// Creates a new Trap object with the provided parameters.
        /// </summary>
        /// <param name="trapType">Which type of trap this Trap is.</param>
        /// <param name="position">Where this Trap exists in the world.</param>
        /// <param name="playerID">Player who deployed this Trap.</param>
        /// <param name="radius">This Trap's radius of effect.</param>
        /// <param name="lifetime">How long this trap should exist in the world.</param>
        /// <param name="weaponID">Index at which this trap-type appears in the weapons list.</param>
        public Trap(TrapType trapType, Vector2 position, short playerID, float radius, float lifetime, short weaponID, short throwableID)
        {
            TrapType = trapType;
            Position = position;
            OwnerID = playerID;
            EffectRadius = radius;
            RemainingTime = lifetime;
            WeaponID = weaponID;
            ThrowableID = throwableID;
            if (trapType == TrapType.SkunkNade) HitPlayers = new Dictionary<Player, DateTime>(4);
        }
    }
}
