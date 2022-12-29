using System;
using Lidgren.Network;
using System.Collections.Generic;
using SARStuff;

namespace WCSARS
{
    class Player
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
        public float PositionX = 508.7f;    // Would be nice to set elsewhere
        public float PositionY = 496.7f;    // Would be nice to set elsewhere
        public byte WalkMode = 0;           // Unsure if this is still a thing in future SAR versions
        public float LastPingTime = 0f;     // PONG!

        // Dance Stuff
        public bool isEmoting = false;
        public short EmoteID = -1;
        public float EmoteSpotX;
        public float EmoteSpotY;
        public DateTime EmoteEndTime;

        // Others... ?
        public byte ActiveSlot = 0;
        public LootItem[] LootItems = new LootItem[3];
        //public Dictionary<short, AttackType> AttackList << well this is only really useful for melee attacks I think so I am not quite sure! D:
        public short AttackCount = -1; // This property has some really srange behaviour. Don't forget to reset this and ProjectileList upon MatchStart
        public short ThrowableCounter = -1; // Literally no clue what this does. Only used like twice in Match.cs 12/2/22 -- Likely something like AttackCount/ProjectileList
        public Dictionary<short, Projectile> ProjectileList = new Dictionary<short, Projectile>(); // Must be reset upon MatchStart (after Lobbby)

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
        public short LastAttackerID = -1;
        public short LastWeaponID = -1;
        public short LastShotID = -1;
        public int DartTicks = 0;
        public DateTime DartNextTime;

        // Vehicle
        public short VehicleID = -1;
        public float VehicleVelocityX = 0f;
        public float VehicleVelocityY = 0f;

        // Color Bools
        public bool isDev = false;
        public bool isMod = false;
        public bool isFounder = false;

        // Booleans
        public bool isReloading = false;
        public bool isFalling = false;
        public bool isDiving = false;
        public bool isGodmode = false;

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
                new LootItem(-1, LootType.Collectable, WeaponType.NotWeapon, "NOTHING", 0, 0),
                new LootItem(-1, LootType.Collectable, WeaponType.NotWeapon, "NOTHING", 0, 0),
                new LootItem(-1, LootType.Collectable, WeaponType.NotWeapon, "NOTHING", 0, 0)
            };
        }
    }
}
