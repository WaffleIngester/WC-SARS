using System;
using Lidgren.Network;
using System.Collections.Generic;
using SARStuff;

namespace WCSARS
{
    public class Player
    {
        // Player Character Data!
        public NetConnection Sender;
        public short ID;
        public string Name = "愛子";
        public short AnimalID;          // The ID of the Super Animal this particular Player is using
        public short UmbrellaID;        // UmbrellaID -- Short
        public short GravestoneID;      // Gravestone -- Short
        public short DeathExplosionID;  // Death Explosion
        public short[] EmoteIDs;        // EmoteIDs -- an array which lists all the emotes this Player has equipped
        public short HatID;             // Hat
        public short GlassesID;         // Glasses
        public short BeardID;           // Beard
        public short ClothesID;         // Clothes
        public short MeleeID;           // Melee
        public byte GunSkinCount;       // Gunskin1 --> Amount of GunSkins | Could just store in a different format but whatever
        public short[] GunSkinKeys;     // Gunskin2 IDK //key  --> Gun... ID? | Could just store in a different format but whatever
        public byte[] GunSkinValues;    // Gunskin3 IDK //value --> SkinKey for GunID ? | Could just store in a different format but whatever

        // Position stuff / things that get updated quite a lot
        public float MouseAngle = 0f;
        public Vector2 Position = new Vector2(508.7f, 496.7f); // Should still get set elsewhere probs
        public byte WalkMode = 0;   // v0.90.2 -- OK
        public float LastPingTime = 0f; // PONG!

        // Emote Stuff
        public bool isEmoting = false;
        public short EmoteID = -1;
        public Vector2 EmotePosition;
        public DateTime EmoteEndTime;

        // Others... ?
        public byte ActiveSlot = 0;
        public LootItem[] LootItems = new LootItem[3];
        public byte[] Ammo = new byte[5];
        //public Dictionary<short, AttackType> AttackList << well this is only really useful for melee attacks I think so I am not quite sure! D:
        public short AttackCount = -1; // Really weird; make sure the counts line-up properly. Reset on round-start
        public short ThrowableCounter = -1; // AttackCount but throwables. You can't acquire throwables in lobby so no real need to reset.
        public Dictionary<short, Projectile> Projectiles = new Dictionary<short, Projectile>(); // Must be reset upon MatchStart (after Lobbby)
        public List<Player> Teammates = new List<Player>(3);

        // Health Related
        public bool isAlive = true;
        public byte HP = 100; // This should be Float or something so the program could do damage calculations correctly. At least the 0.25hp thing
        public byte ArmorTier = 0;
        public byte ArmorTapes = 0;
        public byte Drinkies = 200; // Default should be like 25 or something-- this was just for testing
        public byte Tapies = 0;
        public bool isDrinking = false;
        public bool isTaping = false;
        public DateTime NextHealTime; // time before start = 1.2s; time between = 0.5s (probably wrong but this is good enough)
        public DateTime NextTapeTime; // time until complete tape-up << ~3seconds total
        public DateTime NextCampfireTime; // time at next campfire heal check
        public DateTime NextGasTime; // time (in seconds) until a gas-tick can be done
        public bool hasBeenInGas = false;
        public short LastAttackerID = -1;
        public short LastWeaponID = -1;
        public int DartTicks = 0;
        public DateTime DartNextTime;

        // Reviving
        public bool isDown = false;
        public bool isBeingRevived;
        public bool isReviving;
        public short ReviverID;
        public DateTime ReviveTime;
        public DateTime NextDownDamageTick;
        public byte TimesDowned;

        // Weapon Stuff
        public bool isReloading = false; // If this Player's ActiveSlot is updated while this is True, this gets set to False and Player auto cancels
        // ^ Worked as intended server-side. However, as it turns out- cancel reload is ignored by the reloading Player.
        public int LastThrowable = -1;
        public DateTime NextLootTime;
        //public DateTime ReloadFinishTime;

        // Vehicle
        public short VehicleID = -1;
        public Vector2 HamsterballVelocity;

        // Color Bools
        public bool isDev = false;
        public bool isMod = false;
        public bool isFounder = false;

        // Booleans
        public bool isReady = false;
        public bool isFalling = false;
        public bool isDiving = false;
        public bool isGodmode = false;
        public bool hasLanded = true;

        // Create
        public Player(short id, short characterID, short umbrellaID, short gravestoneID, short deathExplosionID, short[] emotes, short hatID, short glassesID, short beardID, short clothingID, short meleeID, byte skinCount, short[] skinKeys, byte[] skinValues, string thisName, NetConnection senderAddress)
        {
            Name = thisName;
            ID = id;
            AnimalID = characterID;
            UmbrellaID = umbrellaID;
            GravestoneID = gravestoneID;
            DeathExplosionID = deathExplosionID;
            EmoteIDs = emotes;
            HatID = hatID;
            GlassesID = glassesID;
            BeardID = beardID;
            ClothesID = clothingID;
            MeleeID = meleeID;
            GunSkinCount = skinCount;
            GunSkinKeys = skinKeys;
            GunSkinValues = skinValues;
            Sender = senderAddress;
            LootItems = new LootItem[] {
                new LootItem(LootType.Collectable, "NOTHING", 0, 0, new Vector2(0,0)),
                new LootItem(LootType.Collectable, "NOTHING", 0, 0, new Vector2(0,0)),
                new LootItem(LootType.Collectable, "NOTHING", 0, 0, new Vector2(0,0))
            };
        }

        /// <summary>
        /// Finds out if this Player is alive and has landed from the Giant Eagle.
        /// </summary>
        /// <returns>True if the Player is alive AND has landed; False if otherwise</returns>
        public bool IsPlayerReal()
        {
            return isReady && isAlive && hasLanded;
        }

        /// <summary>
        /// Determines if the provided slot is within bounds and also if the provided wepaonID matches the item in that slot.
        /// </summary>
        /// <param name="weaponID">WeaponID / WeaponIndex to compare to.</param>
        /// <param name="slotID">Player LootItem slot to look at.</param>
        /// <returns>True if all checks pass; False if otherwise.</returns>
        public bool IsGunAndSlotValid(int weaponID, int slotID)
        {
            return slotID >= 0 && slotID < 2 && LootItems[slotID].WeaponIndex == weaponID;
        }

        /// <summary>
        /// Determines whether the provided ProjectileID is valid for this Player object.
        /// </summary>
        /// <param name="projectileID"></param>
        /// <returns>True, if the ProjectileID is -1 OR in the ProjectileList; False if otherwise.</returns>
        public bool IsValidProjectileID(int projectileID)
        {
            return projectileID == -1 || Projectiles.ContainsKey((short)projectileID);
        }

        public void SetLastDamageSource(short attackerID, short weaponID)
        {
            LastAttackerID = attackerID;
            LastWeaponID = weaponID;
        }

        /// <summary>
        /// Resets this Player's Hamsterball-related fields to their defaults.
        /// </summary>
        public void ResetHamsterball()
        {
            HamsterballVelocity = new Vector2(0f, 0f);
            VehicleID = -1;
        }
    }
}