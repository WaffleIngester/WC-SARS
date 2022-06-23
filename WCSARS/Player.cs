﻿using Lidgren.Network;

namespace WCSARS
{
    class Player
    {
        // Player Data Stuff! :O
        public NetConnection sender;
        public short myID;
        public string myName = "愛子";
        public short charID; // Character / Avatar
        public short umbrellaID; // Umbrella
        public short gravestoneID; // Gravestone
        public short deathEffectID; // Death Explosion
        public short[] emoteIDs; // Emote List (Length of 6; short; array)
        public short hatID; // Hat
        public short glassesID; // Glasses
        public short beardID; // Beard
        public short clothesID; // Clothes
        public short meleeID; // Melee
        public byte gunSkinCount; // Gunskin1 --> Amount of GunSkins
        public short[] gunskinKey; // Gunskin2 IDK //key  --> Gun... ID?
        public byte[] gunskinValue; // Gunskin3 IDK //value --> SkinKey for GunID ?

        //Updated Regularly...
        public float LastPingTime = 0f;
        public float MouseAngle = 0f;
        public float position_X = 508.7f;
        public float position_Y = 496.7f;

        // Dance Stuff
        public short currenteEmote = -1;
        public float EmoteDuration = -1f;
        public bool isDancing = false;

        // Others... ?
        public byte WalkMode = 0;
        public byte ActiveSlot = 0;
        public LootItem[] MyLootItems = new LootItem[3];
        //public LootItem EquipSlot1;
        //public LootItem EquipSlot2;
        //public LootItem EquipSlot3;
        //public short equip1 = -1;
        //public short equip2 = -1;
        //public short equip3 = -1;
        //public byte equip1_rarity = 0;
        //public byte equip2_rarity = 0;
        public short vehicleID = -1;

        // Health Related
        public byte HP = 100;
        public byte ArmorTier = 0;
        public byte ArmorTapes = 0;
        public byte Drinkies = 200;
        public byte Tapies = 0;
        public bool isDrinking = false;
        public bool isTaping = false;
        public bool isAlive = true;

        // Color Bools
        public bool isDev = false;
        public bool isMod = false;
        public bool isFounder = false;

        //Booleans
        public bool isReloading = false;
        public bool isFalling = false;
        public bool isDiving = false;

        // Create
        public Player(short assignedID, short characterID, short parasollID, short gravestoneID, short deathExplosionID, short[] emotes, short hatID, short glassesID, short beardID, short clothingID, short meleeID, byte skinCount, short[] skinKeys, byte[] skinValues, string thisName, NetConnection senderAddress)
        {
            this.myName = thisName;
            this.myID = assignedID;
            this.charID = characterID;
            this.umbrellaID = parasollID;
            this.gravestoneID = gravestoneID;
            this.deathEffectID = deathExplosionID;
            this.emoteIDs = emotes;
            this.hatID = hatID;
            this.glassesID = glassesID;
            this.beardID = beardID;
            this.clothesID = clothingID;
            this.meleeID = meleeID;
            this.gunSkinCount = skinCount;
            this.gunskinKey = skinKeys;
            this.gunskinValue = skinValues;
            this.sender = senderAddress;
            MyLootItems = new LootItem[] {
                new LootItem(-1, LootType.Collectable, WeaponType.NotWeapon, "NOTHING", 0, 0),
                new LootItem(-1, LootType.Collectable, WeaponType.NotWeapon, "NOTHING", 0, 0),
                new LootItem(-1, LootType.Collectable, WeaponType.NotWeapon, "NOTHING", 0, 0)
            };
        }
        /* Death Notes:
         * Weapon: 
         *  0 -- Melee?
         * -1 -- Nothing
         * -2 == Killfeed Hammer
         * -3 == Killfeed Explosiion lol
         * -4 -- Nothing
         * 
         * 
         * Killing Player:
         * 0=> = Player
         * -1 -- Nothing
         * -2 == Gas
         * -3 == Banan Gods (/killed)
         * 
         */
    }
}
