using System;
using System.Collections.Generic;
using System.Text;

namespace SuperAnimalRoyale.Types
{
    internal class Projectile_
    {
    }
    public struct Projectile
    {
        public int WeaponID { get; private set; } // WeaponID == WeaponJSONIndex
        public int WeaponRarity { get; private set; }
        public float OriginX { get; private set; }
        public float OriginY { get; private set; }
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
