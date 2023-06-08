using Lidgren.Network;
using SARStuff;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace WCSARS // TODO:: Player goes to SARStuff; Player eventually gets a "Client" field to hold Client-Data like No-Fill and stuff
{
    public class Player
    {
        // Player Character Data!
        public string PlayFabID;
        public NetConnection Sender;
        public short ID;
        public string Name = "愛子";
        public short AnimalID;          // ID of the Super Animal this Player is using
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
        public float MouseAngle = 0f;   // current look-angle for this Player
        public Vector2 Position = new Vector2(508.7f, 496.7f);  // the current position of this player.
        public byte WalkMode = 0;       // current "playerWalkMode" for this player (like running, jumping, crawling, downed, stunned) [v0.90.2 OK; may not exist in future versions]
        public float LastPingTime = 0f; // lastReceivedPingTime

        // Emote Stuff
        public bool isEmoting = false;  // whether this Player is currently emoting
        public short EmoteID = -1;      // emoteID# that this Player is performing
        public Vector2 EmotePosition;   // position where this Player started emoting
        public DateTime EmoteEndTime;   // time at which this emote will end

        // Others... ?
        public byte ActiveSlot = 0;
        public LootItem[] LootItems = new LootItem[3];
        public byte[] Ammo = new byte[5];
        //public Dictionary<short, AttackType> AttackList << well this is only really useful for melee attacks I think so I am not quite sure! D:
        public short AttackCount = -1; // Really weird; make sure the counts line-up properly. Reset on round-start
        public short ThrowableCounter = -1; // AttackCount but throwables. You can't acquire throwables in lobby so no real need to reset.
        public Dictionary<short, Projectile> Projectiles = new Dictionary<short, Projectile>(); // Must be reset upon MatchStart (after Lobbby)
        public Dictionary<short, Projectile> ThrownNades = new Dictionary<short, Projectile>();
        public List<Player> Teammates = new List<Player>(3);  // TBH, teams should be server-side...

        // Health Related
        public bool isAlive = true;     // whether this Player is currently alive or not.
        public byte HP = 100;           // amount of HP this Player current has *technically, server-side, this is a float. NetMsg-side, this is a byte.
        public byte ArmorTier = 0;      // the "level"/ "quality" of this Armor that this Player is currently wearing. [should not exceed 3]
        public byte ArmorTapes = 0;     // number of ticks remaining on this Player's Armor. (should not exceed Player.ArmorTier)
        public byte HealthJuice = 25;  // amount of Health Juice this Player currently has [default start: 25]
        public byte SuperTape = 0;      // amount of Super Tape this Player currently has [default start: 0]
        public bool isDrinking = false; // whether this Player is currently drinking
        public bool isTaping = false;   // whether this Player is currently repairing their armor
        //public Vector2 HealPosition;    // position this Player is currently healing at (older SAR versions forced players to stay put while healing]
        public DateTime NextHealTime;           // time (seconds) until another heal-tick can be performed. [pull-out-time: 1.2s: default-rate: 0.5s]
        public DateTime NextTapeTime;           // time (seconds) until this Player has finished repairing their armor. (3-seconds)
        public DateTime NextCampfireTime;       // time (seconds) until a campfire heal-tick can be performed.
        public DateTime NextGasTime;            // time (seconds) until a gas-tick can be done
        public DateTime NextSkunkBombGasTime;   // time (seconds) until a skunk-bomb gas-damage-tick can be performed.
        public bool hasBeenInGas = false;   // whether this Player has entered the Super Skunk Gas already
        public short LastAttackerID = -1;   // ID# of the Player who last attacked this Player
        public short LastWeaponID = -1;     // ID# of the Weapon used by the lastAttacker
        public int DartTicks = 0;       // number of remaining dart-ticks for this Player.
        public DateTime DartNextTime;   // time (usually seconds) until another dart-poison-damage-tick is to be performed.

        // Reviving
        public bool isReviving;     // whether this Player is currently reviving another.
        public short RevivingID;    // ID# of who this Player is reviving.

        //  Downed-State
        public bool isDown;             // whether this Player is currently downed.
        public bool isBeingRevived;     // whether this Player is being revived.
        public short SaviourID;         // ID# of the Player reviving this Player.
        public DateTime ReviveTime;     // time (in seconds) until this Player will be revived.
        public DateTime NextBleedTime;  // time (in seconds) until another bleed-tick can be performed.
        public byte TimesDowned;        // number of times that this Player has been downed

        // Weapon Stuff
        public bool isReloading = false; // If this Player's ActiveSlot is updated while this is True, this gets set to False and Player auto cancels
        // ^ Worked as intended server-side. However, as it turns out- cancel reload is ignored by the reloading Player.
        public int LastThrowable = -1;
        //public DateTime ReloadFinishTime;

        // Vehicle
        public short VehicleID = -1;
        public Vector2 HamsterballVelocity;

        // Color Bools
        public bool isDev = false;
        public bool isMod = false;
        public bool isFounder = false;

        // Spectator
        public bool isGhosted = false;
        public List<short> MySpectatorsIDs = new List<short>(4);
        public short WhoImSpectating = -1;

        // Booleans
        public bool isReady = false;
        public bool isGodmode = false;
        public DateTime StunEndTime;
        public bool isStunned = false;

        // Flight
        /// <summary> Whether this Player has ejected from the eagle or they're still flying.</summary>
        public bool hasEjected = false;
        /// <summary> Whether this Player has ejected and is currently diving.</summary>
        public bool isDiving = false;
        /// <summary> Whether this Player has touched the ground after ejecting. </summary>
        public bool hasLanded = true; // True when in Lobby; reset to False on round-start

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
                new LootItem(-1, LootType.Collectable, "NOTHING", 0, 0, new Vector2(0,0)),
                new LootItem(-1, LootType.Collectable, "NOTHING", 0, 0, new Vector2(0,0)),
                new LootItem(-1, LootType.Collectable, "NOTHING", 0, 0, new Vector2(0,0))
            }; // 0 = weapon1; 1 = weapon2; 2 = melee item;; what about accessories when they get added? where are they at?
        }

        /// <summary>
        /// Finds out if this Player is alive and has landed from the Giant Eagle.
        /// </summary>
        /// <returns>True if the Player is alive AND has landed; False if otherwise</returns>
        public bool IsPlayerReal()
        {
            return isReady && isAlive && hasLanded && !isGhosted;
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

        public void Stun()
        {
            isStunned = true;
            StunEndTime = DateTime.UtcNow.AddSeconds(2.0f); // actual: 2s; may have to do earlier?
        }

        /// <summary>
        /// Sets this Player into the "downed" state. Also sets all necessary downed-state related fields.
        /// </summary>
        /// <param name="nextBleedTImeSeconds">Time (in seconds) until this Player will begin taking bleed-out damage.</param>
        public void DownKnock(float nextBleedTImeSeconds)
        {
            WalkMode = 5;
            HP = 100; // knocked-hp gets reset to 100
            isDown = true;
            isBeingRevived = false;
            NextBleedTime = DateTime.UtcNow.AddSeconds(nextBleedTImeSeconds);
            TimesDowned += 1; // I find this nicer to look at, at this moment in time
        }

        /// <summary>
        /// Sets this Player as being revived + who is reviving them.
        /// </summary>
        /// <param name="saviourPID"></param>
        public void DownSetSaviour(short saviourPID)
        {
            isBeingRevived = true;
            SaviourID = saviourPID;
            ReviveTime = DateTime.UtcNow.AddSeconds(6f);
        }

        /// <summary>
        /// Resets this Player's downed-state fields related to taking bleed-out damage (Player is no longer being revived).
        /// </summary>
        /// <param name="nextBleedTImeSeconds">Time (in seconds) until this Player will begin taking bleed-out damage again.</param>
        public void DownResetState(float nextBleedTImeSeconds)
        {
            isBeingRevived = false;
            SaviourID = -1;
            NextBleedTime = DateTime.UtcNow.AddSeconds(nextBleedTImeSeconds);
        }

        /// <summary>
        /// Resets this Player's downed-state-related fields and puts them in an alive-state with the provided HP. (does not effect Player.isAlive)
        /// </summary>
        public void DownResurrect(byte resHP)
        {
            isDown = false;
            isBeingRevived = false;
            SaviourID = -1;
            WalkMode = 1;
            HP = resHP; // default is 25
        }

        /// <summary>
        /// Marks this Player as reviving another + the ID of who they are reviving.
        /// </summary>
        /// <param name="downedPID">The PlayerID of the downed-player being revived.</param>
        public void SaviourSetRevivee(short downedPID)
        {
            isReviving = true;
            RevivingID = downedPID;
        }

        /// <summary>
        /// Resets this Player's reviving-related fields (must be the Player who is reviving another).
        /// </summary>
        public void SaviourFinishedRessing()
        {
            isReviving = false;
            RevivingID = -1;
        }

        public int AliveNonDownTeammteCount()
        {
            if (Teammates == null || Teammates.Count == 0) return 0;
            int aliveTeammatesCount = 0;
            //int tmp_Count = Teammates.Count
            for (int i = 0; i < Teammates.Count; i++) if ((Teammates[i]?.isAlive == true) && (Teammates[i]?.isDown == false)) aliveTeammatesCount++;
            return aliveTeammatesCount;
        }

        /// <summary>
        /// Returns whether or not this Player has a teammate with the provided PlayerID.
        /// </summary>
        /// <param name="pID">PlayerID to search for.</param>
        /// <returns>True if pID is the ID of one of this Player's teammates; False if otherwise.</returns>
        public bool IsPIDMyTeammate(short pID)
        {
            // cache count/ figure out whether or not there's any point
            int teamCount = Teammates != null ? Teammates.Count : -1;
            if (teamCount <= 0) return false;

            for (int i = 0; i < teamCount; i++)
            {
                if (Teammates[i]?.ID == pID) return true;
            }
            return false;
        }

        public override string ToString() => $"<{Name} ({ID})>";

        // just for messing around/testing
        /*public async Task<bool> DoDamage(byte pDamage)
        {
            Logger.DebugServer($"{DateTime.UtcNow} Delaying for 2s....");
            await Task.Delay(2000);
            Logger.DebugServer($"{DateTime.UtcNow} Finished delay");
            if ((HP - pDamage) < 0)
            {
                //HP = 0;
                return true;
            }
            //HP -= pDamage;
            return false;
        }*/
    }
}