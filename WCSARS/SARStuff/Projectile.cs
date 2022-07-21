using System;
using System.Collections.Generic;
using System.Text;

namespace SuperAnimalRoyale.Types
{
    public struct Projectile
    {
        /// <summary>
        /// The ID-Index of the Weapon used to create this Projectile.
        /// </summary>
        public int WeaponID { get; private set; } // WeaponID == WeaponJSONIndex
        /// <summary>
        /// The Rarity of the Weapon which was used to craete this Projectile.
        /// </summary>
        public int WeaponRarity { get; private set; }
        /// <summary>
        /// The SpawnX of this Projectile.
        /// </summary>
        public float OriginX { get; private set; }
        /// <summary>
        /// The SpawnY of this Projectile.
        /// </summary>
        public float OriginY { get; private set; }
        /// <summary>
        /// The Launch Angle of this Projectile.
        /// </summary>
        public float Angle { get; private set; }

        public Projectile(int _id, int _rarity, float _oX, float _oY, float _angle)
        {
            WeaponID = _id;
            WeaponRarity = _rarity;
            OriginX = _oX;
            OriginY = _oY;
            Angle = _angle;
        }
    }
}
