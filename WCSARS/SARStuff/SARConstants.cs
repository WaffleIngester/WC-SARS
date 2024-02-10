﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SARStuff
{
    /// <summary>
    ///  Class containing a list of values that should be consistent between clients and the server.
    /// </summary>
    internal static class SARConstants
    {
        /// <summary>
        ///  Width of the tarp located over the rebel hideout found in Super Animal Farm. (97)
        /// </summary>
        public const int BarnHideoutTarpSizeX = 97;
        /// <summary>
        ///  Height of the tarp located over the rebel hideout found in Super Animal Farm. (67)
        /// </summary>
        public const int BarnHideoutTarpSizeY = 67;


        // obviously we could just write "solo", "duo", "squad", etc. any time; but in the event this ever changes, ya know... you can easily change it here!
        #region Gamemode Strings
        /// <summary>
        ///  String constant representing the "solos" gamemode (used in Steam status)
        /// </summary>
        public const string GamemodeSolos = "solo";

        /// <summary>
        /// String constant representing the "duos" gamemode (used in Steam status)
        /// </summary>
        public const string GamemodeDuos = "duo";

        /// <summary>
        /// String constant representing the "squad" gamemode (used in Steam status)
        /// </summary>
        public const string GamemodeSquad = "squad";
        #endregion Gamemode Strings

        #region NPC Stuff
        /// <summary>
        ///  The speed at which the "Delivery Mole" NPC moves.
        /// </summary>
        public const float DeliveryMoleMoveSpeed = 25.0f;

        /// <summary>
        ///  The speed at which the "Giant Eagle" NPC moves.
        /// </summary>
        public const float EagleMoveSpeed = 95.0f;
        #endregion NPC Stuff

        #region Players
        /// <summary>
        ///  The maximum amount of HP a player can have.
        /// </summary>
        public const byte PlayerMaxHP = 100;

        /// <summary>
        ///  The maximum amount of health juice a player can have.
        /// </summary>
        public const byte PlayerMaxJuice = 200;

        /// <summary>
        ///  The maximum item slot id for player items.
        /// </summary>
        public const byte PlayerMaxItemSlotID = 2;

        /// <summary>
        ///  The (estimated) amount of time it takes for a player to take out their sippy cup
        /// </summary>
        public const float DrinkWindupDuration = 1.2f;

        /// <summary>
        ///  The amount of time it takes for a player to repair their armor.
        /// </summary>
        public const float TapeRepairDurationSeconds = 3.0f;

        /// <summary>
        ///  The amount of time it takes for a player to revive a downed teammate.
        /// </summary>
        public const float TeammateReviveDurationSeconds = 6.0f;

        /// <summary>
        ///  The maximum distance a player can be from their teammate to initate/ not cancel a revive by moving.
        /// </summary>
        public const float TeammateReviveMaxDistance = 8.0f;
        #endregion Players

        /// <summary>
        ///  The amount of time a banana will stun a player for.
        /// </summary>
        public const float BananaStunDurationSeconds = 2.0f;

        // it's possible to do this... but is it really all that necessary at this point in time?
        /*
        /// <summary>
        ///  LevelJSON key for finding player spawn points when they load into the lobby.
        /// </summary>
        public static string PlayerSpawns = "playerSpawns";

        /// <summary>
        ///  LevelJSON key for finding the location of the tarp found over the Rebel hideout in Super Animal Farm.
        /// </summary>
        public static string BarnTarp = "barnTarp";


        
        /// <summary>
        ///  LevelJSON key for finding the location of every Emu spawn point available in a level.
        /// </summary>
        public static string Emus = "emus";*/
    }
}