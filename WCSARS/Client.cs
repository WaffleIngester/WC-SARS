using System;
using System.Collections.Generic;
using System.Text;
using Lidgren.Network;

namespace WCSARS
{
    internal class Client
    {
        public readonly NetConnection NetAddress;
        public readonly string PlayFabID;
        public string Username = "NO USERNAME";
        public bool Fills = true;
        public bool Party = false;
        public string[] PartyMemberPlayFabIDs;

        public Client(NetConnection netConnection, string playFabID)
        {
            NetAddress = netConnection;
            PlayFabID = playFabID;
        }
    }
}
