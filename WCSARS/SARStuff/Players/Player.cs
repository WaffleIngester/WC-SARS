﻿using System;
using System.Collections.Generic;

/*
 * -- Idea --
 * > What if: instead of many fields like "isAlive", "isDown", "isResurrecting", etc.; there would be only a couple state fields that are checked?
 * ...this would not only reduce clutter, but also simplify things so one doesn't have to keep in mind to check against all other conflicting "isDoingXThing" fields.
 *
*/

namespace SARStuff
{
    /// <summary>
    ///  Represents a player character found in the overworld + their belongings and such!
    /// </summary>
    public class Player
    {
        // Player Character Data!
        public Client Client;
        public short ID;
        public string Name = "愛子";
        public short AnimalID;          // ID of the Super Animal this Player is using
        public short UmbrellaID;        // UmbrellaID
        public short GravestoneID;      // Gravestone
        public short DeathExplosionID;  // Death Explosion
        public short[] EmoteIDs;        // EmoteIDs -- an array which lists all the emotes this Player has equipped
        public short HatID;             // Hat
        public short GlassesID;         // Glasses
        public short BeardID;           // Beard
        public short ClothesID;         // Clothes
        public short MeleeID;           // Melee
        public byte GunSkinCount;       // Gunskin1 ??? | # of skins?      | Could just store in a different format but whatever
        public short[] GunSkinKeys;     // Gunskin2 ??? | gunskin key?     | Could just store in a different format but whatever
        public byte[] GunSkinValues;    // Gunskin3 ??? | gunskin key val? | Could just store in a different format but whatever
        //public Dictionary<short, byte> GunSkins;

        // Position stuff/ stuff that gets updated frequently
        public float MouseAngle = 0f;       // current look-angle for this Player
        public Vector2 Position = new Vector2(508.7f, 496.7f);  // the current position of this player.
        public MovementMode WalkMode = 0;   // current "playerWalkMode"; run, jump, crawl, etc. | v0.90.2 OK -- unknown whether kept or dropped in future update
        public float LastPingTime = 0f;     // lastReceivedPingTime

        // Emote Stuff
        public bool isEmoting = false;  // whether this Player is currently emoting
        public short EmoteID = -1;      // emoteID# that this Player is performing
        public Vector2 EmotePosition;   // position where this Player started emoting
        public DateTime EmoteEndTime;   // time at which this emote will end

        // Weapons/ Loot
        public byte ActiveSlot = 0;
        public LootItem[] LootItems = new LootItem[3];
        public byte[] Ammo = new byte[5];
        //public Dictionary<short, AttackType> AttackList << well this is only really useful for melee attacks I think so I am not quite sure! D:
        public short AttackCount = -1; // Really weird; make sure the counts line-up properly. Reset on round-start
        public short ThrowableCounter = -1; // AttackCount but throwables. You can't acquire throwables in lobby so no real need to reset.
        public Dictionary<short, Projectile> Projectiles = new Dictionary<short, Projectile>(); // Must be reset upon MatchStart (after Lobbby)
        public Dictionary<short, Projectile> ThrownNades = new Dictionary<short, Projectile>();

        // Health Related
        public bool HealActionFinished { get => _hasHealActionEnded; }
        public HealActionState HealState = HealActionState.None;
        public bool isAlive = true;     // whether this Player is currently alive or not.
        public byte HP = SARConstants.PlayerMaxHP; // current amount of HP [*maybe* should be stored as float as opposed to byte]
        public byte ArmorTier = 0;      // the "level"/ "quality" of this Armor that this Player is currently wearing. [should not exceed 3]
        public byte ArmorTapes = 0;     // number of ticks remaining on this Player's Armor. (should not exceed Player.ArmorTier)
        public byte HealthJuice = 25;   // amount of Health Juice this Player currently has [default start: 25]
        public byte SuperTape = 0;      // amount of Super Tape this Player currently has [default start: 0]

        //public Vector2 HealPosition;    // position this Player is currently healing at (older SAR versions forced players to stay put while healing]
        public DateTime NextCampfireTime;       // time (seconds) until a campfire heal-tick can be performed.
        public DateTime NextGasTime;            // time (seconds) until a gas-tick can be done
        public DateTime NextSkunkBombGasTime;   // time (seconds) until a skunk-bomb gas-damage-tick can be performed.
        public bool hasBeenInGas = false;   // whether this Player has entered the Super Skunk Gas already
        public short LastAttackerID = -1;   // ID# of the Player who last attacked this Player
        public short LastWeaponID = -1;     // ID# of the Weapon used by the lastAttacker
        public int DartTicks = 0;       // number of remaining dart-ticks for this Player.
        public DateTime DartNextTime;   // time (usually seconds) until another dart-poison-damage-tick is to be performed.

        // Downed / Revival
        public bool IsBeingRevived { get => _isBeingRessed; }
        public bool isSupposedToBeDown; // alleviate desync issues
        public byte TimesDowned;        // number of times that this Player has been downed
        public Player DownedTeammate = null; // teammate this player is ressing

        // Weapon Stuff
        // if Player.ActiveSlot is updated when this is true, then this should get set to false and their reload canceled.
        // This works 100% as intendedd server-side, but sar works in mysterious ways and the client represented by this Player ignores it. LOL!
        public bool isReloading = false; // program must pray that the actual client represented by this Player is truthful because game stinky
        public int LastThrowable = -1;
        //public DateTime ReloadFinishTime;

        // Vehicle
        public short HamsterballID = -1;
        public Vector2 HamsterballVelocity;

        // Spectator
        public bool isGhosted = false;
        public List<short> MySpectatorsIDs = new List<short>(4);
        public short WhoImSpectating = -1;

        // Booleans
        public bool hasReadied = false;
        public bool isGodmode = false;
        public bool isStunned = false;
        public DateTime StunEndTime;

        // Flight
        /// <summary> Whether this Player has ejected from the Giant Eagle or not.</summary>
        public bool hasEjected = false;

        /// <summary> Whether this Player is currently "diving" whilst falling.</summary>
        public bool isDiving = false;

        /// <summary> Whether this Player has landed on the ground after ejecting from the Giant Eagle.</summary>
        public bool hasLanded = true; // True when in Lobby; reset to False on round-start

        // other
        public List<Player> Teammates = new List<Player>(3); // perhaps teams should be stored/ handled by Matches not Players.
        public byte Placement = 0;

        // TEST START
        private float _healStateTimer = 0.0f;
        private bool _hasHealActionEnded = true;
        private float _bleedoutTimer = 0.0f;
        private bool _isBeingRessed = false;
        // TEST END

        // Create
        public Player(short id, short characterID, short umbrellaID, short gravestoneID, short deathExplosionID, short[] emotes, short hatID, short glassesID, short beardID, short clothingID, short meleeID, byte skinCount, short[] skinKeys, byte[] skinValues, string thisName, Client client)
        {
            Client = client;
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
            return hasReadied && isAlive && hasLanded && !isGhosted;
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
        ///  Marks this Player as no longer riding a hamsterball; resetting their hamsterball-related fields to their default values.
        /// </summary>
        public void ResetHamsterball()
        {
            HamsterballVelocity = new Vector2(0f, 0f);
            HamsterballID = -1;
        }

        /// <summary>
        ///  Marks this Player as riding a hamsterball; also setting which hamsterballID is now to be associated with them.
        /// </summary>
        /// <param name="pHamsterballID"> ID of the hamsterball that this Player is riding.</param>
        public void SetHamsterball(short pHamsterballID)
        {
            HamsterballID = pHamsterballID;
            HamsterballVelocity = new Vector2(0f, 0f);
        }

        /// <summary>
        ///  Marks this Player as stunned.
        /// </summary>
        public void Stun()
        {
            isStunned = true;
            StunEndTime = DateTime.UtcNow.AddSeconds(SARConstants.BananaStunDurationSeconds);
        }

        /// <summary>
        ///  Determines the number of alive & non-downed Players this Player is teammates with.
        /// </summary>
        /// <returns> The number of non-downed, alive teammates of this Player.</returns>
        public int AliveNonDownTeammteCount()
        {
            int ret = 0;
            foreach (Player mate in Teammates)
            {
                if (mate.isAlive && (WalkMode != MovementMode.Downed))
                    ret++;
            }
            return ret;
        }

        /// <summary>
        ///  Determines whether the provided PlayerID is a teammate of this current Player. 
        /// </summary>
        /// <param name="pID">PlayerID to search for.</param>
        /// <returns> True if pID matches any ID of this current Player's teammates; False if otherwise.</returns>
        public bool IsPIDMyTeammate(short pID)
        {
            int teamCount = Teammates != null ? Teammates.Count : -1;
            if (teamCount <= 0)
                return false;

            for (int i = 0; i < teamCount; i++)
            {
                if (Teammates[i]?.ID == pID)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Sets this Player as emoting with the provided parameters.
        /// </summary>
        /// <param name="pEmoteID">ID of the Emote this Player is performing.</param>
        /// <param name="pEmotePosition">Position that this Player is performing the emote.</param>
        /// <param name="pEmoteDuration">How long the emote will last for (-1 = infinite).</param>
        public void EmoteStarted(short pEmoteID, Vector2 pEmotePosition, float pEmoteDuration)
        {
            isEmoting = true;
            EmoteID = pEmoteID;
            EmotePosition = pEmotePosition;
            if (pEmoteDuration == -1)
                EmoteEndTime = DateTime.MaxValue;
            else
                EmoteEndTime = DateTime.UtcNow.AddSeconds(pEmoteDuration); // emote duration is always in seconds
        }

        /// <summary>
        /// Sets this Player as not emoting by setting <see cref="EmoteID"/> to -1 and <see cref="isEmoting"/> to False.
        /// </summary>
        public void EmoteEnded()
        {
            isEmoting = false;
            EmoteID = -1;
        }

        /// <summary>
        ///  Attempts to add the provided Player to this current player's teammate list.
        /// </summary>
        /// <param name="pTeammate"> Player to attempt to add.</param>
        /// <returns> True if the Player was added successfully; otherwise, False.</returns>
        public bool AddTeammate(Player pTeammate)
        {
            if (pTeammate == this || Teammates.Contains(pTeammate))
                return false;

            Teammates.Add(pTeammate);
            return true;
        }

        public void ElapseTimerOrEnd(float deltaTime)
        {
            if (_hasHealActionEnded)
                return;

            _healStateTimer -= deltaTime;
            if (_healStateTimer <= 0.0f)
                ClearHealActionState();
        }

        public void SetHealAction(HealActionState healAction, float duration)
        {
            HealState = healAction;
            _healStateTimer = duration;
            _hasHealActionEnded = false;
        }

        public void ClearHealActionState()
        {
            HealState = HealActionState.None;
            _healStateTimer = 0.0f;
            _hasHealActionEnded = true;
        }

        /// <summary>
        ///  Deducts 1 tape item from this player while simultaneously repairing 1 armor tick.
        /// </summary>
        public void DeductTapeAndAddArmorTick()
        {
            SuperTape -= 1;
            ArmorTapes += 1;
        }

        /// <summary>
        ///  Causes this player to enter the reviving-teammate heal-state.
        /// </summary>
        /// <param name="downedTeammate"> Player this player will try reviving.</param>
        public void BeginRevivingHomie(Player downedTeammate)
        {
            DownedTeammate = downedTeammate;
            SetHealAction(HealActionState.Reviving, SARConstants.TeammateReviveDurationSeconds);
        }

        /// <summary>
        ///  Clears this player's reviving-teammate field. Modifies nothing else.
        /// </summary>
        public void ClearRevivingHomie()
        {
            DownedTeammate = null;
        }

        /// <summary>
        ///  Sets this player into the downed state--- setting all necessary variables in the process.
        /// </summary>
        public void KnockDown()
        {
            HP = SARConstants.PlayerMaxHP;
            WalkMode = MovementMode.Downed;
            if (TimesDowned < byte.MaxValue)
                TimesDowned++;
        }

        /// <summary>
        ///  Initates the bleedout state for this player.
        /// </summary>
        /// <param name="secondsUntilBleedout"> Seconds until first bleedout tick.</param>
        public void BeginBleedoutState(float secondsUntilBleedout)
        {
            _bleedoutTimer = secondsUntilBleedout;
            _isBeingRessed = false;
            isSupposedToBeDown = true;
        }

        /// <summary>
        ///  Sets this player's bleedout timer to the provided value.
        /// </summary>
        /// <param name="secondsUntilNextBleedout"> Seconds until next bleedout tick.</param>
        public void SetNextBleedoutTime(float secondsUntilNextBleedout)
        {
            _bleedoutTimer = secondsUntilNextBleedout;
        }

        /// <summary>
        ///  Elapses this player's bleedout timer and checks whether it has finished.
        /// </summary>
        /// <param name="frameTime"> Time to remove from the bleedout timer.</param>
        /// <returns> True if the bleedout timer reaches its end; otherwise, False.</returns>
        public bool ElapseBleedoutEndsTimer(float frameTime)
        {
            _bleedoutTimer -= frameTime;
            if (_bleedoutTimer <= 0.0f)
                return true;

            return false;
        }

        /// <summary>
        ///  Marks this downed-player as getting revived by a teammate.
        /// </summary>
        public void BeginResState()
        {
            _bleedoutTimer = 100;
            _isBeingRessed = true;
        }
        
        /// <summary>
        ///  Ends this player's downed state. Setting all necessary variables in the process.
        /// </summary>
        /// <param name="newHP"> Amount of HP this player will have.</param>
        public void EndDownedState(byte newHP)
        {
            isSupposedToBeDown = false;
            _isBeingRessed = false;
            WalkMode = MovementMode.Walking;
            HP = newHP;
        }

        /// <summary>
        ///  Determines whether it is possible for this player to drink any health juice.
        /// </summary>
        /// <returns></returns>
        public bool CanDrinkJuice()
        {
            return (HP < SARConstants.PlayerMaxHP) && (HealthJuice > 0);
        }

        /// <summary>
        ///  Attempts to drink the provided amount of health juice. Heals this player for however much they are able to drink.
        /// </summary>
        /// <param name="juiceAmount"> Amount of juice to try drinking.</param>
        public void DrinkJuice(byte juiceAmount)
        {
            if (!CanDrinkJuice())
                return;

            if ((HP + juiceAmount) > SARConstants.PlayerMaxHP) // hp overflow
                juiceAmount = (byte)(100 - juiceAmount);

            if ((HealthJuice - juiceAmount) < 0) // no negative juice amount
                juiceAmount = HealthJuice;

            HP += juiceAmount;
            HealthJuice -= juiceAmount;
        }

        public override string ToString() => $"<{Name} ({ID})>";
    }
}