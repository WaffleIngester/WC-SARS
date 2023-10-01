using Lidgren.Network;
using System.Collections.Generic;

namespace SARStuff
{
    /// <summary>
    ///  Represents some server-like data for each client, rather than their actual character representation in game.
    /// </summary>
    public class Client
    {
        // Readonly
        /// <summary>
        ///  NetConnection associated with this Client.
        /// </summary>
        public readonly NetConnection NetAddress;

        /// <summary>
        ///  PlayFabID associated with this Client.
        /// </summary>
        public readonly string PlayFabID;

        /// <summary>
        ///  Whether or not this Client has fills enabled or not.
        /// </summary>
        public readonly bool isFillsDisabled = false;

        // Properties
        /// <summary>
        ///  List of PlayFabIDs that are associated with this Client's incoming party when joining a Match.
        /// </summary>
        public List<string> PartyPlayFabIDs { get; private set; } = new List<string>();

        // Just public stuff; try not to set these for no reason
        /// <summary>
        /// The username associated with this Client.
        /// </summary>
        public string Username = "No-Name-恥"; // unused as of yet

        /// <summary>
        ///  The account level of this Client.
        /// </summary>
        public short AccountLevel = 1000;

        /// <summary>
        ///  Whether or not this Client is a Developer.
        /// </summary>
        public bool isDev= false;

        /// <summary>
        ///  Whether or not this Client is a Moderator.
        /// </summary>
        public bool isMod= false;

        /// <summary>
        ///  Whether or not this Client has the Founder's Edition.
        /// </summary>
        public bool isFounder = false;

        /// <summary>
        ///  Creates a new Client with the provided parameters.
        /// </summary>
        /// <param name="netConnection"> NetConnection to assign to this Client.</param>
        /// <param name="playFabID"> PlayFabID to assign to this Client.</param>
        /// <param name="partyMemberPlayFabIDs"> Any party-member PlayFabIDs that should be associated with this Client.</param>
        public Client(NetConnection netConnection, string playFabID, string[] partyMemberPlayFabIDs, bool disableFills)
        {
            NetAddress = netConnection;
            PlayFabID = playFabID;
            isFillsDisabled = disableFills;

            if (partyMemberPlayFabIDs != null)
                PartyPlayFabIDs = new List<string>(partyMemberPlayFabIDs);
        }

        /// <summary>
        ///  Creates an empty Client object. (only use for testing)
        /// </summary>
        public Client()
        {
            PlayFabID = "No-AssignedID";
        }

        /// <summary>
        ///  Returns a string that represents this Client object.
        /// </summary>
        /// <returns> A string that represents this Client object.</returns>
        public override string ToString()
        {
            return $"<{NetAddress.RemoteEndPoint} | {PlayFabID} ({Username})>";
        }

        /// <summary>
        ///  Sets some of this Client's user information.
        /// </summary>
        /// <param name="pName"> New username to assign this Client.</param>
        /// <param name="pIsDev"> If this Client is a developer.</param>
        /// <param name="pIsMod"> If this Client is a moderator.</param>
        /// <param name="pIsFounder"> If this Client is a founder.</param>
        /// <param name="pAccountLevel"> New account level to assign this Client.</param>
        public void SetUserInfo(string pName, bool pIsDev, bool pIsMod, bool pIsFounder, short pAccountLevel = 550)
        {
            Username = pName; // unused as of yet
            if (Username == "")
                Username = "No-Name";

            isDev = pIsDev;
            isMod = pIsMod;
            isFounder = pIsFounder;
            AccountLevel = pAccountLevel;
        }

        /// <summary>
        ///  Attempts to gather the IP address attached to his Client.
        /// </summary>
        /// <returns>A string representing this Client's NetAddress's IP.</returns>
        public string GetIP()
        {
            return $"{NetAddress?.RemoteEndPoint.Address}";
        }
    }
}
