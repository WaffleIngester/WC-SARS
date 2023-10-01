using System;
using System.Collections.Generic;
using System.Text;

namespace SARStuff
{
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

        public const float MoleMoveSpeed = 25.0f;

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
