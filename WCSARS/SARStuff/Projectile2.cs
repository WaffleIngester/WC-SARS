using System;
using WCSARS;

namespace SARStuff
{
    public class Projectile2
    {
        #region readonly
        /// <summary>
        ///  Original positon of this Projectile.
        /// </summary>
        public readonly Vector2 Origin;

        /// <summary>
        ///  Angle this Projectile is pointing.
        /// </summary>
        public readonly float Angle;

        /// <summary>
        ///  Cosine of the origin angle.
        /// </summary>
        public readonly float CosA;

        /// <summary>
        ///  Sine of the origin angle.
        /// </summary>
        public readonly float SinA;

        /// <summary>
        ///  ID of the Player who created this Projectile.
        /// </summary>
        public readonly short PlayerID;

        /// <summary>
        ///  ID of the Weapon used to create this Projectile.
        /// </summary>
        public readonly int WeaponID;

        /// <summary>
        ///  Rarity of the Weapon used to create this Projectile.
        /// </summary>
        public readonly byte WeaponRarity;

        /// <summary>
        ///  Maximum distance that this Projectile can travel before it no longer does any damage.
        /// </summary>
        public readonly short MaxTravelDistance;

        /// <summary>
        ///  Movement speed of this Projectile.
        /// </summary>
        public readonly short MoveSpeed;
        #endregion readonly

        #region public
        /// <summary>
        ///  Current position of this Projectile.
        /// </summary>
        public Vector2 Position { get => _position; }

        /// <summary>
        ///  Whether or not this Projectile is currently valid.
        /// </summary>
        public bool hasReachedEnd { get; private set; } = false;
        #endregion public

        #region private
        private Vector2 _position;
        #endregion private

        float test_totalDistance = 0.0f;

        /// <summary>
        ///  Creates a new Projectile object.
        /// </summary>
        /// <param name="origin"> Origin point of the Projectile.</param>
        /// <param name="angle"> (degrees) Angle the Projectile is traveling towards.</param>
        /// <param name="playerID"> ID of the Player creating this Projectile.</param>
        /// <param name="weaponID"> ID of the Weapon used to create this Projectile.</param>
        /// <param name="weaponRarity">Rarity of the Weapon used to create this Projectile</param>
        public Projectile2(Vector2 origin, float angle, short playerID, Weapon weapon, byte weaponRarity)
        {
            Origin = origin;
            _position = origin;
            Angle = angle;
            Console.WriteLine($"Projectile Init Angle: {angle * 57.295779f}");
            PlayerID = playerID;
            WeaponID = weapon.JSONIndex;
            WeaponRarity = weaponRarity;

            // maxTravelDistance = regularMaxDistance + (maxDistInc * weaponRarity)
            MaxTravelDistance = (short)(weapon.BulletMaxDistanceBase + (weapon.BulletMaxDistanceIncPerRarity * weaponRarity));

            // moveSpeed = baseMoveSpeed * (moveSpeedIncPerRarity * rarity)
            MoveSpeed = (short)(weapon.BulletMoveSpeed + (weapon.BulletMoveSpeedIncPerRarity * weaponRarity));

            // store SinA / CosA for later
            //angle *= 0.017453f; // 0.017453f is about pi/180
            SinA = (float)Math.Sin(angle);
            CosA = (float)Math.Cos(angle);
        }

        /// <summary>
        ///  Marks this Projectile as having reached its maximum distance.
        /// </summary>
        public void MarkEndReached()
        {
            hasReachedEnd = true;
        }

        /// <summary>
        ///  Updates this Projectile's current position. Will be marked invlaid if it hits its maximum distance.
        /// </summary>
        /// <param name="lastFrameTime"></param>
        public void Update(float lastFrameTime)
        {
            if (hasReachedEnd)
                return;
            // (x, y) = (CosA, SinA)
            ///Console.WriteLine($"MoveSpeed: {MoveSpeed}\nMaxDist:{MaxTravelDistance}\nLastTime: {lastFrameTime}\n");
            ///Console.WriteLine($"Deg: {Angle}\nCosX: {CosA}\nSinY: {SinA}");
            ///Console.WriteLine($"Prev: {_position}");

            //float x = CosA * MaxTravelDistance;
            //float y = SinA * MaxTravelDistance;
            //Vector2 endpoint = new Vector2(x, y);
            //Vector2.MoveTowards(_position, endpoint, lastFrameTime * MoveSpeed);
            float instanceDistance = lastFrameTime * MoveSpeed;
            test_totalDistance += instanceDistance;
            if ((instanceDistance >= 120.0f) || (instanceDistance >= MaxTravelDistance))
            {
                MarkEndReached();
                return;
            }

            float cosFactor = CosA * instanceDistance; // (lastFrameTime * MoveSpeed);
            float sinFactor = SinA * instanceDistance; // (lastFrameTime * MoveSpeed);
            Vector2 goal = new Vector2(_position.x + cosFactor, _position.y + sinFactor);
            _position = Vector2.MoveTowards(_position, goal, MoveSpeed * lastFrameTime);
            
            //_position.x += cosFactor;
            //_position.y += sinFactor;
            
            ///Console.WriteLine($"Cos: {cosFactor}\nSin: {sinFactor}");
            //_position.x += CosA * (lastFrameTime * MoveSpeed) * MaxTravelDistance;
            //_position.y += SinA * (lastFrameTime * MoveSpeed) * MaxTravelDistance;
            ///Console.WriteLine($"Updt: {_position}");

            // problem: player ends up higher on straight-up shots/ lower on straight-down shots
            // this is probably because the game is actually trying to simulate bullets falling...
            // ... and moves the marker down from its original height or something... idk right now!
            Vector2 difference = _position - Origin;
            float magnitude = difference.magnitude;
            if ((magnitude >= 120f) || (magnitude >= MaxTravelDistance))
            {
                MarkEndReached();
            }
        }

        public override string ToString()
        {
            return $"PROJECTILE.TOSTRING OVERRIDE UNFINISHED";
        }
    }
}
