using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using Lidgren.Network;
using SimpleJSON;
using SAR_TOOLS;
using SuperAnimalRoyale.Types;

namespace WCSARS
{
    class Match
    {
        // Here's a little guide for naming variables.

        /* svd_ prefix
         * This prefix means "Server Data"
         * ServerData is anything that is well... SERVER DATA!
         * ServerData, in this instance, are things which will NOT be changed often- if ever.
         * LootItemTiles, for instance, only needs to be set once to be used
         * The LootItem seed also is ServerData. It only needs to be set once.
         */

        /* sv_ prefix
         * This is the opposite of ServerData! These are just ServerVariables
         * Think of these more like sv_cheats 1. You can mess with these all you want and it doesn't affect anything too major
         * The side-effects which'll probably occur are probably mostly intentional. Volitile variables maybe for testing and such!
         */

        /* No prefix, just name
         * Probably similar to svd_ prefix. Maybe one-use or just bools that are kind of obvious as to what they do.
         * I would like it, however; if there weren't many single-name stuff without prefixes.
         * Everything having a category would be quite nice I think.
         */

        private NetPeerConfiguration config;
        public NetServer server;
        public Player[] player_list;
        private Weapon[] s_WeaponsList = Weapon.GetAllWeaponsList();
        private List<short> availableIDs;
        private Thread updateThread;

        private Dictionary<NetConnection, string> IncomingConnectionsList;
        private Dictionary<int, LootItem> ItemList;
        //private Dictionary<int, VAL> CoconutList;
        private List<int> CoconutList;
        private Dictionary<int, Hampterball> HamsterballList;

        private JSONArray svd_PlayerDataJSON;
        private int sv_TotalLootCounter, sv_LootSeed, sv_CoconutSeed, sv_VehicleSeed; // Spawnable Item Generation Seeds
        private int prevTime, prevTimeA, matchTime; //TODO -- probably should make server more tick-based
        private bool matchStarted, matchFull;
        private bool isSorting, isSorted;
        public double timeUntilStart, gasAdvanceTimer, gasAdvanceLength;

        private float sv_safeX, sv_safeY, sv_safeRadius; // Server Var -- SafeZone.X, SafeZone.Y, SafeZone.Radius
        private bool sv_isGasReal = false;
        private byte svd_DDGMaxDmgTicks = 12; // DDG max amount of Damage ticks someone can get stuck with
        private byte svd_DDGTicksToAdd = 4; // the amount of DDG ticks to add with each DDG shot
        private float svd_DDGTickRate = 0.6f; // the rate at which the server will attempt to make a DDG DamageTick check
        private int sv_DDGDamagePerTick = 9;

        // TODO make LootGen Tiles actually have... position and stuff? For now, just listing them is fineee
        //private List<short> svd_LootGenTilesR; // normal loot tiles
        //private List<short> svd_LootGenTitlesG; // "better" loot tiles
        //private List<short> svd_LootGenTitlesB; // loot tiles for... bot loot???
        //private int svd_CoconutCount = 0;
        //private int svd_VehicleCount = 0;
        private List<Doodad> svd_Doodads;
        private bool svd_LevelLoaded = false;

        //mmmmmmmmmmmmmmmmmmmmmmmmmmmmm
        public bool ANOYING_DEBUG1;
        private bool sv_doWins = false;
        private bool shouldUpdateAliveCount = true;
        //private const int TICK_RATE = 24;
        private const int MS_PER_TICK = 1000 / 24;

        public Match(int port, string ip)
        {
            sv_TotalLootCounter = 0;

            // Spawn Loot Generation Seeds
            sv_LootSeed = 351301; // TODO -- Make these... you know... RANDOM ???
            sv_CoconutSeed = 5328522;
            sv_VehicleSeed = 9037281;

            matchStarted = false;
            matchFull = false;
            isSorting = false;
            isSorted = true;
            player_list = new Player[64];
            availableIDs = new List<short>(64);
            IncomingConnectionsList = new Dictionary<NetConnection, string>();
            for (short i = 0; i < 64; i++) { availableIDs.Add(i); }

            timeUntilStart = 90.00;
            gasAdvanceTimer = -1;
            prevTime = DateTime.Now.Second;
            updateThread = new Thread(serverUpdateThread);

            // Initializing JSON stuff
            if (File.Exists(Directory.GetCurrentDirectory() + @"\playerdata.json"))
            {
                JSONNode PlayerDataJSON = JSON.Parse(File.ReadAllText(Directory.GetCurrentDirectory() + @"\playerdata.json"));
                svd_PlayerDataJSON = (JSONArray)PlayerDataJSON["PlayerData"];
            }
            else
            {
                Logger.Failure("No such file 'PlayerData.json'");
                Environment.Exit(2);
            }

            LoadSARLevel();

            // NetServer Initialization and starting
            config = new NetPeerConfiguration("BR2D");
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated); // Reminder to not remove this
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval); // Reminder to not remove this
            config.PingInterval = 22f;
            config.LocalAddress = System.Net.IPAddress.Parse(ip);
            config.Port = port;
            server = new NetServer(config);
            server.Start();
            updateThread.Start();
            NetIncomingMessage msg;
            DateTime _nextTick = DateTime.Now;
            while (true)
            {
                //_nextTick = DateTime.Now;
                while (_nextTick < DateTime.Now)
                {
                    while ( (msg = server.ReadMessage() ) != null)
                    {
                        switch (msg.MessageType)
                        {
                            case NetIncomingMessageType.Data:
                                HandleMessage(msg);
                                break;
                            case NetIncomingMessageType.StatusChanged:
                                Logger.Header("~-- { Status Change} --~");
                                switch (msg.SenderConnection.Status)
                                {
                                    case NetConnectionStatus.Connected:
                                        Logger.Success($"A new client has connected successfuly! Sender Address: {msg.SenderConnection}");
                                        NetOutgoingMessage acceptMsgg = server.CreateMessage();
                                        acceptMsgg.Write((byte)0);
                                        acceptMsgg.Write(true);
                                        server.SendMessage(acceptMsgg, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
                                        isSorted = false;
                                        break;
                                    case NetConnectionStatus.Disconnected:
                                        Logger.Warn($"[Connection Update - Disconnected] A client has disconnected (message: {msg.ReadString()}). Attempting to remove their data from server...");
                                        if (tryFindIndexbyConnection(msg.SenderConnection, out int index))
                                        {
                                            NetOutgoingMessage msg_disconnect = server.CreateMessage();
                                            msg_disconnect.Write((byte)46);
                                            msg_disconnect.Write(player_list[index].myID); // player left ID
                                            msg_disconnect.Write(false); // no clue? IsVanish? [as in, moderator ghosting like MC]
                                            server.SendToAll(msg_disconnect, NetDeliveryMethod.ReliableOrdered);

                                            // TODO - Major Problem 1 - Handling incoming Player IDs is a bit messy, and calls for this convoluted solution
                                            availableIDs.Insert(0, player_list[index].myID);
                                            player_list[index] = null;
                                            isSorted = false;
                                            Logger.Success($"[Connection Update - Disconnected] The disconnecting client has been delt with successfully!");
                                        }
                                        else
                                        {
                                            Logger.Failure($"[Connection Update - Disconnected.ERROR] There seems to have been a problem actually locating the player in the array. Uh oh!");
                                        }
                                        break;
                                    case NetConnectionStatus.Disconnecting:
                                        Logger.Warn($"[Connection Update - Disconnecting] A client is attempting to disconnect.");
                                        break;
                                }
                                break;
                            case NetIncomingMessageType.ConnectionApproval: // must enable MessageType ConnectionApproval for this to work
                                Logger.Header("[Connection Approval] A new connection is awaiting approval!");
                                string clientKey = msg.ReadString();
                                Logger.Basic($"[Connection Approval] Incoming connection {msg.SenderEndPoint} sent key: {clientKey}");
                                if (clientKey == "flwoi51nawudkowmqqq")
                                {
                                    if (!matchStarted && timeUntilStart > 5.0)
                                    {
                                        Logger.Success("[Connection Approval] Incoming connection's key was the same as the server's. Connection approved.");
                                        msg.SenderConnection.Approve();
                                    }
                                    else
                                    {
                                        Logger.Failure("[Connection Approval] Incoming connection had the right key, however the match was in progress. Connection denied.");
                                        msg.SenderConnection.Deny($"The match you are trying to join is already in progress. Sorry!");
                                    }
                                }
                                else
                                {
                                    msg.SenderConnection.Deny($"Your client version key is incorrect.\n\nYour version key: {clientKey}");
                                    Logger.Failure($"[Connection Approval] Incoming connection {msg.SenderEndPoint}'s sent key was incorrect. Connection denied.");
                                }
                                break;
                            case NetIncomingMessageType.DebugMessage:
                                Logger.DebugServer(msg.ReadString());
                                break;
                            case NetIncomingMessageType.WarningMessage:
                            case NetIncomingMessageType.ErrorMessage:
                                Logger.Failure("EPIC BLUNDER! " + msg.ReadString());
                                break;
                            case NetIncomingMessageType.ConnectionLatencyUpdated:
                                Logger.Header("--> ConnectionLatencyUpdated:");
                                try
                                {
                                    float pingTime = msg.ReadFloat();
                                    Logger.Basic($"  -> Ping time: {pingTime}");
                                    Logger.Basic($"  -> Remote Time Offset: {msg.SenderConnection.RemoteTimeOffset}");
                                    Logger.Basic($"  -> Average Round Time: {msg.SenderConnection.AverageRoundtripTime}");
                                    try
                                    {
                                        player_list[getPlayerArrayIndex(msg.SenderConnection)].LastPingTime = pingTime; //not actually ping whoopsies
                                    }
                                    catch
                                    {
                                        Logger.Failure($"  -> Connection {msg.SenderEndPoint} does not exist. Cannot write their last ping time. (likely not yet fully connected)");
                                    }
                                }
                                catch
                                {
                                    Logger.Failure("ConnectionLatencyUpdated -- Error:: No float to read");
                                }
                                break;
                            default:
                                Logger.Failure("Unhandled type: " + msg.MessageType);
                                break;
                        }
                        server.Recycle(msg);
                    }
                    _nextTick = _nextTick.AddMilliseconds(MS_PER_TICK);
                    if (_nextTick > DateTime.Now)
                    {
                        Thread.Sleep(_nextTick - DateTime.Now);
                    }

                }
            }
        }
        //lots of important things go on in here
        #region server update thread
        private void serverUpdateThread() //where most things get updated...
        {
            // TODO - cleanese.
            Logger.Success("Server update thread started.");
            //GenerateItemLootList(sv_LootSeed); // Loot Generation Seed << refer to bottom
            //GenerateHamsterballs(sv_VehicleSeed); << this is now done within the method "Load SAR Level" (spaces so doesn't show in Ctrl+F lol)
            if (!sv_doWins)
            {
                Logger.Warn("\n[ServerUpdateThread] [WARNING] Server variable \"sv_doWins\" has been set to false.\nThis means the server will NOT check for a winner.\nIf you wish to reactive the win check, use the command \"/togglewin\" while in a match.\n");
            }
            DateTime nextTick = DateTime.Now;
            //lobby
            while (!matchStarted)
            {
                while (nextTick < DateTime.Now)
                {
                    if (!isSorted) { sortPlayersListNull(); }
                    if (player_list[player_list.Length - 1] != null && !matchFull)
                    {
                        matchFull = true;
                        Logger.Basic("Match seems to be full!");
                    }
                    send_dummy();

                    //check the count down timer
                    if (!matchStarted && (player_list[0] != null)) { checkStartTime(); }
                    //inform everyone of new time ^^


                    //updating player info to all people in the match
                    updateEveryoneOnPlayerPositions();
                    updateEveryoneOnPlayerInfo();
                    //updateServerTapeCheck(); --similar idea, about as stupid.
                    nextTick = nextTick.AddMilliseconds(MS_PER_TICK);
                    if (nextTick > DateTime.Now)
                    {
                        Thread.Sleep(nextTick - DateTime.Now);
                    }
                }
            }
            // this is to clear player ProjectileList and stuff lol
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null)
                {
                    player_list[i].AttackCount = -1;
                    player_list[i].ProjectileList = new Dictionary<short, Projectile>();
                }
            }
            //main game
            while (matchStarted)
            {
                if (nextTick < DateTime.Now)
                {
                    if (!isSorted) { sortPlayersListNull(); }

                    send_dummy();

                    //check for win
                    if (shouldUpdateAliveCount && sv_doWins) { svu_Wincheck(); }
                    check_DDGTicks(); // TESTING
                    //updating player info to all people in the match
                    updateEveryoneOnPlayerPositions();
                    updateEveryoneOnPlayerInfo();

                    updateServerDrinkCheck();
                    //updateServerTapeCheck(); --similar idea, about as stupid.
                    updateEveryonePingList();

                    advanceTimeAndEventCheck();
                    checkGasTime();
                    if (sv_isGasReal)
                    {
                        checkSkunkGas();
                    }
                    nextTick = nextTick.AddMilliseconds(MS_PER_TICK);
                    if (nextTick > DateTime.Now)
                    {
                        Thread.Sleep(nextTick - DateTime.Now);
                    }
                }
            }
        }

        private void updateEveryoneLobbyStartTime()
        {
            NetOutgoingMessage sTimeMsg = server.CreateMessage();
            sTimeMsg.Write((byte)43);
            sTimeMsg.Write(timeUntilStart);
            server.SendToAll(sTimeMsg, NetDeliveryMethod.ReliableOrdered);
        }
        private void updateEveryoneOnPlayerPositions() // TODO -- 1. Make Send like Client should actually receive 2. Use only in Lobby instead of all the time.
        {
            /* format
             * H - 11
             * Byte -- List Length / times to loop
             * 
             * For Each Person in the list
             * Short
             * SByte - Mouse Angle or whatever
             * Short - PosX
             * Short - Pos Y
             * 
             * How to determine these values
             * Mouse Angle Convert1 = (int)(ReadSByte)*2
             * Mouse Angle = PI * (float)(Mouse Angle Convert1) / 180f;
             * 
             * PosX and PosY = (float)ReadShort / 6f;
             */
            NetOutgoingMessage msg = server.CreateMessage(); // changed as of 6/1/22
            msg.Write((byte)11); // I think this is working?
            //find list length of players
            if (!isSorted)
            {
                sortPlayersListNull();
            }
            for (byte i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] == null)
                {
                    msg.Write(i);
                    break;
                }
            }
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null)
                {
                    msg.Write(player_list[i].myID);
                    sbyte whatthefrick = (sbyte)((180f * player_list[i].MouseAngle / 3.141592f) / 2);
                    //msg.Write((sbyte)((player_list[i].MouseAngle / 3.141592f * 180f) / 2));
                    msg.Write(whatthefrick);
                    msg.Write((ushort)(player_list[i].position_X * 6f));
                    msg.Write((ushort)(player_list[i].position_Y * 6f));
                } // why the frick did I break-out early of these. there's like no real benefit in doing so other than causing more problems...
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }
        private void updateEveryoneOnPlayerInfo()
        {
            // this is probably only meant to only be sent when someone actually has a change in their HP, Armor, ArmorTicks, Drinks, TapeAmount, or WalkMode
            // however, this works just as fine right now so ya!
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)45);
            for (int list_length = 0; list_length < player_list.Length; list_length++)
            {
                if (player_list[list_length] == null)
                {
                    msg.Write((byte)list_length);
                    for (int i = 0; i < list_length; i++)
                    {
                        if (player_list[i] != null)
                        {
                            msg.Write(player_list[i].myID);
                            msg.Write(player_list[i].HP);
                            msg.Write(player_list[i].ArmorTier);
                            msg.Write(player_list[i].ArmorTapes);
                            msg.Write(player_list[i].WalkMode);
                            msg.Write(player_list[i].Drinkies);
                            msg.Write(player_list[i].Tapies);
                        }
                        else { break; }
                    }
                    server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
                    break;
                }
            }
        }
        private void send_dummy()
        {
            NetOutgoingMessage dummy = server.CreateMessage();
            dummy.Write((byte)97);
            server.SendToAll(dummy, NetDeliveryMethod.ReliableOrdered);
        }

        private void updateEveryonePingList()
        {
            NetOutgoingMessage pings = server.CreateMessage();
            pings.Write((byte)112);
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] == null)
                {
                    pings.Write((byte)i);
                    for (int j = 0; j < i; j++)
                    {
                        pings.Write(player_list[j].myID);
                        pings.Write((short)(player_list[j].LastPingTime * 1000f)); //this appears to be correct.
                    }
                    break;
                }
            }
            server.SendToAll(pings, NetDeliveryMethod.ReliableOrdered);
        }

        private void updateServerDrinkCheck()
        {
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].isDrinking)
                {
                    if (player_list[i].Drinkies > 0)
                    {
                        byte ToDrink = 5;
                        if ((player_list[i].Drinkies - 5) < 0) // If we'll go into the negatives then let's just get all that we can instead
                        {
                            ToDrink = (byte)(5 - player_list[i].Drinkies);
                        }
                        if ((player_list[i].HP + ToDrink) >= 100)
                        {
                            ToDrink = (byte)(100 - player_list[i].HP); // store needed difference somewhere
                            player_list[i].HP += ToDrink;
                            player_list[i].Drinkies -= ToDrink;
                            player_list[i].isDrinking = false;
                            ServerAMSG_EndedDrinking(player_list[i].myID);
                            break;
                        }
                        player_list[i].HP += ToDrink;
                        if (0 >= (player_list[i].Drinkies - ToDrink))
                        {
                            player_list[i].Drinkies = 0; // seems a bit redundant if drinkies is going to be 0, but you know in case it won't
                            player_list[i].isDrinking = false;
                            ServerAMSG_EndedDrinking(player_list[i].myID);
                            break;
                        } // if drinkies amount won't go into the negatives or equal 0, then just subtract like normal
                        player_list[i].Drinkies -= ToDrink; 
                        break; // yes, important. prevents running the last part below
                    }
                    player_list[i].isDrinking = false;
                    ServerAMSG_EndedDrinking(player_list[i].myID);
                }
            }
        }

        private void svu_Wincheck()
        {
            shouldUpdateAliveCount = false;
            List<short> aIDs = new List<short>(player_list.Length);
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].isAlive)
                {
                    aIDs.Add(player_list[i].myID);
                }
            }
            if (aIDs.Count == 1)
            {
                NetOutgoingMessage congratulationsyouvewoncongratulationsyouvewoncongratulationsyouvewon = server.CreateMessage();
                congratulationsyouvewoncongratulationsyouvewoncongratulationsyouvewon.Write((byte)9);
                congratulationsyouvewoncongratulationsyouvewoncongratulationsyouvewon.Write(aIDs[0]);
                server.SendToAll(congratulationsyouvewoncongratulationsyouvewoncongratulationsyouvewon, NetDeliveryMethod.ReliableUnordered);
            }
        }

        private void checkStartTime()
        {
            if (timeUntilStart > 0)
            {
                if (prevTime != DateTime.Now.Second)
                {
                    if ((timeUntilStart % 2) < 1)
                    {
                        Logger.Basic($"seconds until start: {timeUntilStart }");
                        if (timeUntilStart != 0) { updateEveryoneLobbyStartTime(); }
                    }
                    timeUntilStart -= 1;
                    prevTime = DateTime.Now.Second;
                }
            }
            else if (timeUntilStart == 0)
            {
                //waits an extra second
                if (prevTime != DateTime.Now.Second)
                {
                    sendStartGame();
                    matchStarted = true;
                }
            }
        }
        //can be simplified with eventCheck
        private void checkGasTime()
        {
            //basically a copy and paste from checkStartTimer(). Any inaccuracies/inefficency there also appear here.
            if (gasAdvanceTimer > 0)
            {
                if (prevTime != DateTime.Now.Second)
                {
                    if ((gasAdvanceTimer % 2) < 1)
                    {
                        Logger.Basic($"time until send gas message: {gasAdvanceTimer}");
                    }
                    gasAdvanceTimer -= 1;
                    prevTime = DateTime.Now.Second;
                }
            }
            else if (gasAdvanceTimer == 0)
            {
                if (prevTime != DateTime.Now.Second)
                {
                    gasAdvanceTimer = -1;
                    NetOutgoingMessage gasMoveMsg = server.CreateMessage();
                    gasMoveMsg.Write((byte)34);
                    //TODO: gasAdvanceLength should just be a float not a double silly billy!
                    gasMoveMsg.Write((float)gasAdvanceLength); //move time -- high time = slow move; low time = fast move
                    server.SendToAll(gasMoveMsg, NetDeliveryMethod.ReliableOrdered);
                }
            }
        }
        //can be simplified with gasCheck
        private void advanceTimeAndEventCheck()
        {
            //literally just a copy and paste
            if (prevTimeA != DateTime.Now.Second)
            {
                matchTime += 1;
                prevTimeA = DateTime.Now.Second;

                switch (matchTime)
                {
                    case 60:
                        createNewSafezone(620, 720, 620, 720, 6000, 3000, 60);
                        gasAdvanceLength = 72;
                        break;
                    case 212:

                        break;
                }
            }
        }

        private void createNewSafezone(float x1, float y1, float x2, float y2, float r1, float r2, float time)
        {
            // Fixup
            NetOutgoingMessage gasMsg = server.CreateMessage();
            gasMsg.Write((byte)33);
            gasMsg.Write(x1);   // Start-Circle CenterX
            gasMsg.Write(y1);   // Start-Circle CenterY
            gasMsg.Write(x2);   // End-Circle   CenterX
            gasMsg.Write(y2);   // End-Circle   CenterY
            gasMsg.Write(r1);   // Start-Circle Radius
            gasMsg.Write(r2);   // End-Circle   Radius
            gasMsg.Write(time); // Time until approachment 
            server.SendToAll(gasMsg, NetDeliveryMethod.ReliableOrdered);
            gasAdvanceTimer = time;
            sv_safeX = x2;
            sv_safeY = y2;
            sv_safeRadius = r2;
            sv_isGasReal = true;
        }

        private void checkForWinnerWinnerChickenDinner()
        {
            Logger.Failure("You shouldn't have done that.");
        }

        //TODO : Make this better :3
        private void sendStartGame()
        {
            NetOutgoingMessage smsg = server.CreateMessage();
            smsg.Write((byte)6);
            smsg.Write((byte)1);
            smsg.Write((short)14);
            smsg.Write((short)(5 * 100)); // 45 = DEATH
            smsg.Write((byte)1);
            smsg.Write((short)14);
            smsg.Write((short)(5 * 100));
            server.SendToAll(smsg, NetDeliveryMethod.ReliableUnordered);
        }

        private void checkSkunkGas()
        {
            float playerX, playerY;
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null)
                {
                    playerX = player_list[i].position_X;
                    playerY = player_list[i].position_Y;
                    float dX = playerX - sv_safeX;
                    float dY = playerY - sv_safeY;

                    bool isCircle = Math.Sqrt(dX*dX + dY*dY) <= sv_safeRadius;
                    Logger.testmsg($"Gas Check -- Player {i}({player_list[i].myName}): {isCircle}");
                    Logger.Warn($"safezoneX: {sv_safeX}\nsafezoneY:{sv_safeY}\nsafeRadius{sv_safeRadius}");
                    Logger.Warn($"playerX: {playerX}\nplayerY: {playerY}\ndX: {dX}\ndY: {dY}\n");
                    if (!isCircle)
                    {
                        if ( (player_list[i].HP - 2) > 0 )
                        {
                            player_list[i].HP -= 2;
                        }
                    }

                }
            }
        }
        private void check_DDGTicks()
        {
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].DartTicks > 0)
                {
                    if (player_list[i].DartNextTime <= DateTime.Now)
                    {
                        if ((player_list[i].DartTicks - 1) >= 0)
                        {
                            player_list[i].DartTicks -= 1;
                        }
                        player_list[i].DartNextTime = DateTime.Now.AddMilliseconds(600);
                        serverSendShotInfo(player_list[i].LastAttackerID, player_list[i].myID, player_list[i].LastShotID, 0, -1, 0);
                        test_damagePlayer(player_list[i], sv_DDGDamagePerTick, player_list[i].LastAttackerID, player_list[i].LastWeaponID);
                    }
                }
            }
        }
        #endregion

        // right now there really is no reason for a handle message thingy to exist. maybe when things can run asynchronous
        private void HandleMessage(NetIncomingMessage msg)
        {
            byte b = msg.ReadByte();
            switch (b)
            {
                // Request Authentication
                case 1:
                    Logger.Header($"[Authentication Request] {msg.SenderEndPoint} is sending an authentication request!");
                    sendAuthenticationResponseToClient(msg);
                    break;

                case 3: // still has work to be done
                    Logger.Header($"Sender {msg.SenderEndPoint}'s Ready Received. Now reading character data.");
                    serverHandlePlayerConnection(msg);
                    break;

                case 5:
                    Logger.Header($"<< sending {msg.SenderEndPoint} player characters... >>");
                    sendPlayerCharacters();
                    break;
                case 7: // Client - Request Eagle Eject
                    // TODO - Probably just make a ForceMove message and stuff
                    Player ejectedPlayer = player_list[getPlayerArrayIndex(msg.SenderConnection)];
                    NetOutgoingMessage sendEject = server.CreateMessage();
                    sendEject.Write((byte)8); // Server - Force Moveplayer
                    sendEject.Write(ejectedPlayer.myID); // PlayerID
                    sendEject.Write(ejectedPlayer.position_X); // Where2Go X
                    sendEject.Write(ejectedPlayer.position_Y); // Where2Go Y
                    sendEject.Write(true);                     // Is Parachute? | Always Yes in this case.
                    server.SendToAll(sendEject, NetDeliveryMethod.ReliableSequenced);
                    Logger.Warn($"Player ID {ejectedPlayer.myID} ({ejectedPlayer.myName}) has ejected!\nX, Y: ({ejectedPlayer.position_X}, {ejectedPlayer.position_Y})");
                    break;

                case 14: // player sends info about them and junk. pretty neat. seems fine where it is at now
                    try
                    {
                        float mouseAngle = msg.ReadInt16() / 57.295776f;
                        float rX = msg.ReadFloat();
                        float rY = msg.ReadFloat();
                        byte walkMode = msg.ReadByte();
                        if (tryFindPlayerbyConnection(msg.SenderConnection, out Player plr))
                        {
                            plr.position_X = rX;
                            plr.position_Y = rY;
                            plr.MouseAngle = mouseAngle;
                            plr.WalkMode = walkMode;
                        }
                        else
                        {
                            msg.SenderConnection.Disconnect("There was an error processing your request. Sorry about that!");
                            Logger.Failure("[ClientUpdatePlayer] [ERROR] Unable to locate Player with the NetConnection given. Disconnect the connection.");
                        }
                    } catch (Exception ex)
                    {
                        Logger.Failure($"[ClientUpdatePlayer] [ERROR] {ex}");
                    }
                    break;
                case 16:
                    try
                    {
                        HandlePlayerShotMessage(msg);
                    } catch (Exception ex)
                    {
                        Logger.Failure($"[Player Attack] [ERROR] {ex}");
                    }
                    break;
                case 18:
                    if (matchStarted)
                    {
                        serverSendPlayerShoteded(msg);
                    }
                    break;
                case 21: // TODO - make give lobby weapons and junk
                    if (matchStarted)
                    {
                        ServerH_PlayerLoot(msg);
                        // need to add break statement when done orms etomthinge
                    }
                    // gibe lobby weapno :)
                    break;
                case 25:
                    serverHandleChatMessage(msg);
                    break;

                case 27: // Client Slot Update
                    serverSendSlotUpdate(msg.SenderConnection, msg.ReadByte());
                    break;

                case 29: //Received Reloading
                    // TODO - make awesome
                    NetOutgoingMessage sendReloadMsg = server.CreateMessage();
                    sendReloadMsg.Write((byte)30);
                    sendReloadMsg.Write(getPlayerID(msg.SenderConnection)); //sent ID
                    sendReloadMsg.Write(msg.ReadInt16()); //weapon ID
                    sendReloadMsg.Write(msg.ReadByte()); //slot ID
                    server.SendToAll(sendReloadMsg, NetDeliveryMethod.ReliableOrdered);
                    break;
                case 92: //Received DONE reloading
                    NetOutgoingMessage doneReloading = server.CreateMessage();
                    doneReloading.Write((byte)93);
                    doneReloading.Write(getPlayerID(msg.SenderConnection)); //playerID
                    server.SendToAll(doneReloading, NetDeliveryMethod.ReliableOrdered); //yes it's that simple
                    break;
                //figure it out.
                case 36:
                    serverSendBeganGrenadeThrow(msg);
                    break;
                case 38:
                    serverSendGrenadeThrowing(msg);
                    break;
                case 40:
                    serverSendGrenadeFinished(msg);
                    break;

                case 55: //Entering a hamball
                    short vehPlr = getPlayerArrayIndex(msg.SenderConnection);
                    short enteredVehicleID = msg.ReadInt16();
                    if (!HamsterballList.ContainsKey(enteredVehicleID))
                    {
                        Logger.Failure($"Error @ Packet-Type 55 [Enter Vehicle] -- Vehicle ID '{enteredVehicleID}' does not exist.");
                        break;
                    }
                    if (HamsterballList[enteredVehicleID].HP <= 0)
                    {
                        Logger.Failure($"Error @ Packet-Type 55 [Enter Vehicle] -- Player attempted to enter a vehicle (Vehicle ID: {enteredVehicleID}), but it should be broken. Refusing their request.");
                        break;
                    }
                    // TODO - probably should check ownership of the vehicle. like, is anyone already in it and stuff?
                    NetOutgoingMessage enterVehicle = server.CreateMessage();
                    enterVehicle.Write((byte)56);
                    enterVehicle.Write(player_list[vehPlr].myID); //sent ID
                    enterVehicle.Write(enteredVehicleID); //vehicle ID
                    enterVehicle.Write(player_list[vehPlr].position_X); //X
                    enterVehicle.Write(player_list[vehPlr].position_Y); //Y
                    player_list[vehPlr].vehicleID = enteredVehicleID;
                    server.SendToAll(enterVehicle, NetDeliveryMethod.ReliableOrdered);
                    break;

                case 57: // Client - Exit Hammerball
                    // TODO -- when messing with hammerball owners, reset owner.
                    short vehPlrEx = getPlayerArrayIndex(msg.SenderConnection);
                    NetOutgoingMessage exitVehicle = server.CreateMessage();
                    exitVehicle.Write((byte)58);
                    exitVehicle.Write(player_list[vehPlrEx].myID); //sent ID
                    exitVehicle.Write(msg.ReadInt16()); //vehicle ID
                    exitVehicle.Write(player_list[vehPlrEx].position_X); //X
                    exitVehicle.Write(player_list[vehPlrEx].position_Y); //Y
                    player_list[vehPlrEx].vehicleID = -1;
                    server.SendToAll(exitVehicle, NetDeliveryMethod.ReliableOrdered); //yes it's that simple
                    break;

                case 44: // Client - Request Current Spectator
                    /* msg.ReadFloat() // cam X
                     * msg.ReadFloat() // cam Y
                     * msg.ReadInt16() // player id (who they're watching)
                     */
                    //
                    break;

                case 47: // Client - I'm Requesting To Start Healing
                    serverSendPlayerStartedHealing(msg.SenderConnection, msg.ReadFloat(), msg.ReadFloat());
                    break;

                case 51: // Client - I'm Requesting Coconut Eaten
                    serverSendCoconutEaten(msg);
                    break;

                case 53: // Client - Sent Cutgras
                    serverSendCutGrass(msg);
                    break;

                case 60: // Client - Hammerball Hitsomeone
                    serverSendVehicleHitPlayer(msg);
                    break;

                case 62: // Client - My Hamsterball Hit a Wall
                    if (tryFindPlayerbyConnection(msg.SenderConnection, out Player fPlayer))
                    {
                        ServerMSG_PlayerHamsterballBounce(fPlayer);
                    }
                    else
                    {
                        Logger.Failure($"[Receive Message 62 ERROR] - Could NOT find player with the provided NetConnection... NOT GOOD");
                    }
                    break;

                case 64: // Client - I Hit a Vehicle
                    // TODO - Make sure everything works and cleanup
                    short vehShotWepID = msg.ReadInt16();//WeaponID, ID of the weapon that shot the vehicle
                    short targetedVehicleID = msg.ReadInt16(); //targetVehicleID, vehicle that was shot at
                    short optionalProjectileID = msg.ReadInt16();
                    if (!HamsterballList.ContainsKey(targetedVehicleID))
                    {
                        Logger.Failure($"Error @ Packet-Type 64 [ClientHitVehcile] - Vehicle which was hit doesn't exist in the vehicle list.");
                        break;
                    }
                    if (HamsterballList[targetedVehicleID].HP <= 0)
                    {
                        Logger.Failure("Error @ Packet-Type 64 [ClientHitVehcile] - Vehicle which was hit has 0 or less HP. Which means it's gone.");
                        break;
                    }

                    // it would seem that the game actually just looks at the item's index in the json list and just says "frick it that's our item"
                    try
                    {
                        Logger.Basic($"Sent Weapon ID: {vehShotWepID}\nWeapon's Name if used as an index: {s_WeaponsList[vehShotWepID].Name}");
                    }
                    catch
                    {
                        Logger.Failure("Yeah, I bonked this one up.");
                    }

                    Logger.Warn(s_WeaponsList[vehShotWepID].ArmorDamageOverride.ToString());

                    int dink = s_WeaponsList[vehShotWepID].ArmorDamage;
                    if (s_WeaponsList[vehShotWepID].ArmorDamageOverride > 0)
                    {
                        dink = s_WeaponsList[vehShotWepID].ArmorDamageOverride;
                    }
                    if ((HamsterballList[targetedVehicleID].HP - dink) < 0)
                    {
                        HamsterballList[targetedVehicleID].HP = 0;
                    }
                    else
                    {
                        HamsterballList[targetedVehicleID].HP -= (byte)dink;
                    }
                    NetOutgoingMessage ballHit = server.CreateMessage();
                    ballHit.Write((byte)65);
                    ballHit.Write(getPlayerID(msg.SenderConnection));
                    ballHit.Write(targetedVehicleID);
                    ballHit.Write(HamsterballList[targetedVehicleID].HP);
                    ballHit.Write(optionalProjectileID);
                    server.SendToAll(ballHit, NetDeliveryMethod.ReliableUnordered);
                    break;

                case 66: // Client - Sent Emote
                    ServerMSG_SendPerformedEmote(player_list[getPlayerArrayIndex(msg.SenderConnection)], msg);
                    break;

                case 72: // CLIENT_DESTORY_DOODAD // TODO - Make this actually work/do something.
                         // TODO - make doodads real.
                    int checkX = msg.ReadInt32();
                    int checkY = msg.ReadInt32();
                    short projectileID = msg.ReadInt16();
                    Logger.Basic(" : Doodad Destroyed : "
                        + $"X: {checkX}"
                        + $"Y: {checkY}"
                        + $"ProjectileID: {projectileID}");
                    int _doodadCount = svd_Doodads.Count;
                    Int32Point hitCoords = new Int32Point(checkX, checkY);
                    for (int i = 0; i < _doodadCount; i++)
                    {
                        bool flipper = false;
                        if (!flipper && (svd_Doodads[i].X == checkX) && (svd_Doodads[i].Y == checkY))
                        {
                            Logger.DebugServer("[DoodadDestroy] [FOUND] This is the simple check working.");
                            flipper = true;
                        }
                        if (!flipper && (svd_Doodads[i].OffsetCollisionPoints != null))
                        {
                            int ptsCount = svd_Doodads[i].OffsetCollisionPoints.Count; // for whatever reason, .Count is slower than caching
                            for (int j = 0; j < ptsCount; j++)
                            {
                                if (svd_Doodads[i].OffsetCollisionPoints[j] == hitCoords)
                                {
                                    Logger.DebugServer("[DoodadDestroy] [BAD-CHECK] [FOUND] This is the more inefficient method of locating a doodad returning true");
                                    flipper = true;
                                }
                            }
                        }
                        if (flipper)
                        {
                            NetOutgoingMessage test = server.CreateMessage();
                            test.Write((byte)73);
                            test.Write((short)420);
                            test.Write((short)svd_Doodads[i].X);
                            test.Write((short)svd_Doodads[i].Y);
                            //List<Int32Point> pts = svd_Doodads[i].OffsetCollisionPoints;
                            //int d_count = pts.Count;
                            int d_count = svd_Doodads[i].OffsetCollisionPoints.Count;
                            test.Write((short)d_count);
                            for (int m = 0; m < d_count; m++)
                            {
                                test.Write((short)svd_Doodads[i].OffsetCollisionPoints[m].x);
                                test.Write((short)svd_Doodads[i].OffsetCollisionPoints[m].y);
                                test.Write((byte)1);
                            }
                            /*for (int m = 0; m < d_count; m++)
                            {
                                test.Write((short)(pts[m].x));
                                test.Write((short)(pts[m].y));
                                test.Write((byte)1);
                            }*/
                            test.Write((byte)0);
                            //test.Write((short)PLAYERID) << would only use this if someone got hit
                            server.SendToAll(test, NetDeliveryMethod.ReliableSequenced);
                            if (matchStarted) svd_Doodads.RemoveAt(i); // only pop from list if match is in progress
                            break;
                        }
                    }
                    break;

                case 74: // Client - Sent Attack Windup
                    Logger.testmsg("\nAttack Windup used.\nIf you notice this say something.");
                    ServerMSG_DealWithWindup(msg);
                    break;

                case 75: // Client - Sent Attack Winddown
                    Logger.testmsg("\nAttack Wind Downed used.\nNote if you see this message/find out when.");
                    serverSendAttackWindDown(getPlayerID(msg.SenderConnection), msg.ReadInt16());
                    break;
                case 87:
                    serverSendDepployedTrap(msg);
                    break;
                case 90: // Client - Request Reload Cancel
                    NetOutgoingMessage plrCancelReloadMsg = server.CreateMessage();
                    plrCancelReloadMsg.Write((byte)91);
                    plrCancelReloadMsg.Write(getPlayerID(msg.SenderConnection));
                    server.SendToAll(plrCancelReloadMsg, NetDeliveryMethod.ReliableSequenced);
                    break;

                case 97: // Client - Dummy  -- Periodically sent. If you get this... That's not a good thing.
                    NetOutgoingMessage dummyMsg = server.CreateMessage();
                    dummyMsg.Write((byte)97);
                    server.SendMessage(dummyMsg, msg.SenderConnection, NetDeliveryMethod.Unreliable);
                    break;
                case 98: // Client - I want to Tape-Up [ Request Duct Taping ]
                    serverSendPlayerStartedTaping(msg.SenderConnection, msg.ReadFloat(), msg.ReadFloat());
                    break;
                //case 108: // Player Request DiveMode update / Parachute mode update idrk...
                    // unused as of yet...
                    //break;

                default:
                    Logger.missingHandle($"Message appears to be missing handle. ID: {b}");
                    break;
            }
        }
        /// <summary>
        /// Sends an Authentication Response to a connected client.
        /// </summary>
        private void sendAuthenticationResponseToClient(NetIncomingMessage msg) // Receive PT 1 >> Send PacketType 2
        {
            //TODO -- Actually try approving the connection.
            // string  --   PlayFabID
            // bool    --   FillYayorNay
            // string  --   Auth Ticket
            // byte    --   Party Count
            // string --    PartyMember PlayFabID
            string _PlayFabID = msg.ReadString();
            /* bool _FillsChoice = msg.ReadBoolean();
            string _AuthenticationTicket = msg.ReadString();
            byte _PartyCount = msg.ReadByte();
            string[] _PartyIDs; */
            //Logger.DebugServer("This user's PlayFabID: " + _PlayFabID);
            if (!IncomingConnectionsList.ContainsKey(msg.SenderConnection))
            {
                IncomingConnectionsList.Add(msg.SenderConnection, _PlayFabID);
            }
            NetOutgoingMessage acceptMsg = server.CreateMessage();
            acceptMsg.Write((byte)2);
            acceptMsg.Write(true); //todo - maaaybe actually try authenticating the player?
            server.SendMessage(acceptMsg, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
            Logger.Success($"Server sent {msg.SenderConnection.RemoteEndPoint} their accept message!");
        }

        private void serverHandlePlayerConnection(NetIncomingMessage msg)
        {
            //Read the player's character info and stuff
            //ulong steamID = msg.ReadUInt64();     // only comes from modified client -->> removed for 6/1/22 update
            string steamName = msg.ReadString();  // only comes from modified client
            short charID = msg.ReadInt16();
            short umbrellaID = msg.ReadInt16();
            short graveID = msg.ReadInt16();
            short deathEffectID = msg.ReadInt16();
            short[] emoteIDs =
            {
                msg.ReadInt16(),
                msg.ReadInt16(),
                msg.ReadInt16(),
                msg.ReadInt16(),
                msg.ReadInt16(),
                msg.ReadInt16(), };
            short hatID = msg.ReadInt16();
            short glassesID = msg.ReadInt16();
            short beardID = msg.ReadInt16();
            short clothesID = msg.ReadInt16();
            short meleeID = msg.ReadInt16();
            byte gunSkinCount = msg.ReadByte();
            short[] gunskinGunID = new short[gunSkinCount];
            byte[] gunSkinIndex = new byte[gunSkinCount];
            for (int l = 0; l < gunSkinCount; l++)
            {
                gunskinGunID[l] = msg.ReadInt16();
                gunSkinIndex[l] = msg.ReadByte();
            }
            if (!isSorted && !isSorting) // It seems a bit dumb, but just making sure it nulls are at the end.
            {
                sortPlayersListNull();
            }
            //TODO: I think there is a better way of finding the ID that is available and stuff but not sure how
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] == null)
                {
                    player_list[i] = new Player(availableIDs[0], charID, umbrellaID, graveID, deathEffectID, emoteIDs, hatID, glassesID, beardID, clothesID, meleeID, gunSkinCount, gunskinGunID, gunSkinIndex, steamName, msg.SenderConnection);
                    sendClientMatchInfo2Connect(availableIDs[0], msg.SenderConnection);
                    availableIDs.RemoveAt(0);
                    //find if person who connected is mod, admin, or whatever!
                    try
                    {
                        string _ThisPlayFabID = IncomingConnectionsList[msg.SenderConnection];
                        JSONObject _PlayerJSONData;
                        for (int p = 0; p < svd_PlayerDataJSON.Count; p++)
                        {
                            if (svd_PlayerDataJSON[p] != null && svd_PlayerDataJSON[p]["PlayerID"] == _ThisPlayFabID)
                            {
                                _PlayerJSONData = (JSONObject)svd_PlayerDataJSON[p];
                                //Logger.Basic($"PlayerDataJSON for this Player:\n{_PlayerJSONData}");
                                // It would seem that not only does this check for if the key exists, but also its bool value. Probably wrong though lol
                                if (_PlayerJSONData["Admin"])
                                {
                                    player_list[i].isDev = true;
                                }
                                if (_PlayerJSONData["Moderator"])
                                {
                                    player_list[i].isMod = true;
                                }
                                if (_PlayerJSONData["Founder"])
                                {
                                    player_list[i].isFounder = true;
                                }
                                break;
                            }
                        }
                    }
                    catch (Exception exceptlol)
                    {
                        Logger.Failure("absolute blunder doing... IDK!\n" + exceptlol);
                    }
                    break;
                }
            }
        }
        private void sendClientMatchInfo2Connect(short sendingID, NetConnection receiver)
        {
            // send a message back to the connecting player... \\
            NetOutgoingMessage mMsg = server.CreateMessage();
            mMsg.Write((byte)4);
            mMsg.Write(sendingID); // Assigned Player ID

            // todo: *must* make match seeds random. could just use system.Random() but didn't wanna bother yet
            mMsg.Write(sv_LootSeed);    // int32 -- seed 1 (GameServer: Sent LootGen Seed)
            mMsg.Write(sv_CoconutSeed); // int32 -- seed 2 (GameServer: Sent CoconutGen Seed)
            mMsg.Write(sv_VehicleSeed); // int32 -- seed 2 (GameServer: Sent VehicleGen Seed)

            mMsg.Write(timeUntilStart); // double -- clientTimeAtWhichGameWillStart
            mMsg.Write("yerhAGJ");      // string -- Match UUID ||  [ MatchUUID ]
            mMsg.Write("solo");         // string -- gamemode ||  [ gameMode ]

            //todo: *must find a way to "randomize" flight path (I say "random" because it seems as though paths aren't entirely random...)
            mMsg.Write((float)0);       // float -- x1 - flightStartPoint
            mMsg.Write((float)0);       // float -- y1 
            mMsg.Write((float)8000);    // float -- x2 -- flightEndPoint
            mMsg.Write((float)8000);    // float -- y2

            /*this last part is the server telling the game info about where the gallery targets are and stuff
             * if nothing is included (like it is here) the targets just don't appear. which is fine by me.
             * whether or not the targets are working is not really game-changing so not dealing with that headache right now. */
            mMsg.Write((byte)0); // amount of times to loop through thing ig but I skipped out. something with gallery targets
            mMsg.Write((byte)0); // that gallery target's score or whatever but I don't give a darn

            server.SendMessage(mMsg, receiver, NetDeliveryMethod.ReliableOrdered);
            //msg.SenderConnection.Disconnect("Currently Testing Stuff! Please come back later!");
        }

        //message 5 > send10
        /// <summary>
        /// Sends a message to everyone in the lobby with data about everyone in the lobby. Likely only called once someone connects so everyone knows of the new player.
        /// </summary>
        private void sendPlayerCharacters()
        {
            Logger.Header($"Beginning to send player characters to everyone once again.");
            if (!isSorted) // make sure list is sorted still... but it probably is so... yeah
            {
                sortPlayersListNull();
            }
            NetOutgoingMessage sendPlayerPosition = server.CreateMessage();
            sendPlayerPosition.Write((byte)10);
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] == null)
                {
                    sendPlayerPosition.Write((byte)i); // Ammount of times to loop (for amount of players, you know?
                    break;
                }
            }
            // Theoretically, I think it would be faster to just use the previously gotten list-length-to-null, and loop that many times then just stop
            // as opposed to just doing it over. However, in the previous tests the results said otherwise so... okii!
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null)
                {
                    sendPlayerPosition.Write(player_list[i].myID);                      // MyAssignedID          |   Short  -- fixed as of 6/1/22 -- for some reason was just writing I as a short even though player IDs get mixed????
                    sendPlayerPosition.Write(player_list[i].charID);                    // CharacterID           |   Short
                    sendPlayerPosition.Write(player_list[i].umbrellaID);                // UmbrellaID            |   Short
                    sendPlayerPosition.Write(player_list[i].gravestoneID);              // GravestoneID          |   Short
                    sendPlayerPosition.Write(player_list[i].deathEffectID);             // ExplosionID           |   Short
                    for (int j = 0; j < player_list[i].emoteIDs.Length; j++)            // EmoteIDs              |   Short[6]
                    {
                        sendPlayerPosition.Write(player_list[i].emoteIDs[j]);
                    }
                    sendPlayerPosition.Write(player_list[i].hatID);                     // HatID                 |   Short
                    sendPlayerPosition.Write(player_list[i].glassesID);                 // GlassesID             |   Short
                    sendPlayerPosition.Write(player_list[i].beardID);                   // BeardID               |   Short
                    sendPlayerPosition.Write(player_list[i].clothesID);                 // ClothesID             |   Short
                    sendPlayerPosition.Write(player_list[i].meleeID);                   // MeleeID               |   Short
                    //Gun skins
                    sendPlayerPosition.Write(player_list[i].gunSkinCount);              // Amount of GunSkins    |   Byte
                    for (byte l = 0; l < player_list[i].gunSkinCount; l++)
                    {
                        sendPlayerPosition.Write(player_list[i].gunskinKey[l]);         // GunSkin GunID         |   Short in Short[]
                        sendPlayerPosition.Write(player_list[i].gunskinValue[l]);       // GunSkin SkinID        |   Byte in Byte[]
                    }

                    //Positioni?
                    sendPlayerPosition.Write(player_list[i].position_X);                // PositionX             |   Float
                    sendPlayerPosition.Write(player_list[i].position_Y);                // PositionY             |   Float
                    sendPlayerPosition.Write(player_list[i].myName);                    // PlayerName            |   String

                    sendPlayerPosition.Write(player_list[i].currenteEmote);             // CurrentEmoteID        |   Short
                    sendPlayerPosition.Write((short)player_list[i].MyLootItems[0].LootID);     // Equip 1 ID            |   Short
                    sendPlayerPosition.Write((short)player_list[i].MyLootItems[1].LootID);     // Equip 2 ID            |   Short
                    sendPlayerPosition.Write(player_list[i].MyLootItems[0].ItemRarity); // Equip 1 Rarity        |   Byte
                    sendPlayerPosition.Write(player_list[i].MyLootItems[1].ItemRarity); // Equip 2 Rarity        |   Byte
                    sendPlayerPosition.Write(player_list[i].ActiveSlot);                // Current Equip Index   |   Byte
                    sendPlayerPosition.Write(player_list[i].isDev);                     // Is User Developer     |   Bool
                    sendPlayerPosition.Write(player_list[i].isMod);                     // Is User Moderator     |   Bool
                    sendPlayerPosition.Write(player_list[i].isFounder);                 // Is User Founder       |   Bool
                    sendPlayerPosition.Write((short)450);                               // Player Level          |   Short
                    sendPlayerPosition.Write((byte)0);                                  // Amount of Teammates   |   Byte
                    //sendPlayerPosition.Write((short)25);                              // Teammate ID           |   Short
                }
                else { break; }
            }
            Logger.Success("Going to be sending new player all other player positions.");
            server.SendToAll(sendPlayerPosition, NetDeliveryMethod.ReliableSequenced);
        }
        /// <summary>
        /// Sends a message to all connected clients that a player had been killed. 
        /// </summary>
        private void ServerAMSG_KillAnnouncement(short aOffedID, float aGraveX, float aGraveY, short aKillerID, short aWeaponID)
        {
            NetOutgoingMessage deathMsg = server.CreateMessage();
            deathMsg.Write((byte)15);    // Header                  | Byte
            deathMsg.Write(aOffedID);    // Deceased ID             | Short
            deathMsg.Write(aGraveX);     // Deceased Gravestone X   | Float
            deathMsg.Write(aGraveY);     // Deceased Gravestone Y   | Float
            deathMsg.Write(aKillerID);   // Killing PlayerID        | Short  // -3 = Banan; -2 = Gas; -1 = Nothing??; 0+ = Player; 
            deathMsg.Write(aWeaponID);   // WeaponID                | Short  // -1/-4 = Nothing?; -3 = Explosion; -2 = Hamsterball; 0+ Weapon
            server.SendToAll(deathMsg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Handles a NetIncoming message with the header byte "16"
        /// </summary>
        private void HandlePlayerShotMessage(NetIncomingMessage msg)
        {
            if (!tryFindPlayerbyConnection(msg.SenderConnection))
            {
                throw new Exception("Could not locate ShotPlayer's NetConnection in player_list.");
            }
            Player player = getPlayerWithConnection(msg.SenderConnection);
            short weaponID = msg.ReadInt16(); // short WeaponID << remember this is just the index in the WeaponsList array
            byte itemSlot = msg.ReadByte();
            float shotAngle = msg.ReadInt16() / 57.295776f;
            float spawnX = msg.ReadFloat();
            float spawnY = msg.ReadFloat();
            bool isValid = msg.ReadBoolean();
            bool didHit = msg.ReadBoolean();
            short hitX = -1; // Hit Destructible CollisionPointX | makes an Int32Point
            short hitY = -1; // Hit Destructible CollisionPointY | makes an Int32Point
            if (didHit)
            {
                // TODO -- perhaps make separate doodad hit handle?
                //Logger.testmsg("It is indeed possible to hit a destructible with this method...");
                hitX = msg.ReadInt16();
                hitY = msg.ReadInt16();
            }
            short attackID = msg.ReadInt16();
            player.AttackCount++;
            if (attackID != player.AttackCount)
            {
                Logger.Failure($"[ClientSentShot] [ERROR] There was a mis-match with server-side player attack count and the client-side attack count. (received:stored:: {attackID}:{player.AttackCount})");
                return;
            }
            Logger.DebugServer($"Item Slot: {itemSlot}");
            int projectileAnglesCount = msg.ReadByte(); // WARNING: the count will always be sent as a byte, but for simplicity is stored as int.
            if (projectileAnglesCount < 0) // if we somehow manage to get an invalid value (a negative number less than 0) then we're going to crash anyways
            {
                throw new Exception($"Invalid value \"{projectileAnglesCount}\" received when attempting to find Projectile-Count");
            }
            float[] projectileAngles = new float[projectileAnglesCount];
            short[] projectileIDs = new short[projectileAnglesCount];
            bool[] projectileValids = new bool[projectileAnglesCount]; // because projectileAnglesCount will always be some number 0+
            if (projectileAnglesCount > 0)
            {
                // Weapon1 = 0; Weapon2 = 1; Melee = 2 (can't encounter under normal conditions); Throwables = 3 (can't encounter under normal conditions)
                LootItem _weapon = player.MyLootItems[itemSlot]; // there's potential for failure here because ^^
                for (int i = 0; i < projectileAnglesCount; i++)
                {
                    projectileAngles[i] = msg.ReadInt16() / 57.295776f;
                    projectileIDs[i] = msg.ReadInt16();
                    projectileValids[i] = msg.ReadBoolean();
                    if (player.ProjectileList.ContainsKey(projectileIDs[i]))
                    {
                        Logger.Failure($"[ClientSentShot] [ERROR] The key {projectileIDs[i]} already exists in the found Player's projectile list. (method exited)");
                        return;
                    }
                    player.ProjectileList.Add(projectileIDs[i], new Projectile(_weapon.IndexInList, _weapon.ItemRarity, spawnX, spawnY, projectileAngles[i])); // unfinished...
                    Logger.Warn($"Projectile ID: {projectileIDs[i]};\nListCount: {player.ProjectileList.Count}");
                    Logger.Warn("Amount of ProjectileKeys in this list: " + player.ProjectileList.Keys.Count.ToString());
                    Logger.testmsg(player.ProjectileList.Keys.ToString());
                    Logger.testmsg($"keys: {player.ProjectileList.Keys.Count}");
                }
            }

            // this should probably be moved into its own little method, but right now it is fine for this to be here I think
            NetOutgoingMessage pmsg = server.CreateMessage();
            pmsg.Write((byte)17);
            pmsg.Write(player.myID);
            pmsg.Write((ushort)(player.LastPingTime * 1000f));
            pmsg.Write(weaponID);
            pmsg.Write(itemSlot);
            pmsg.Write(attackID); // I think this is like how the servers deal with it. each player has AttackID/ProjectileIDs attached just to them
            pmsg.Write((short)(3.1415927f / shotAngle * 180f));
            pmsg.Write(spawnX);
            pmsg.Write(spawnY);
            pmsg.Write(isValid);
            pmsg.Write((byte)projectileAnglesCount);
            if (projectileAnglesCount > 0)
            {
                for (int i = 0; i < projectileAnglesCount; i++)
                {
                    pmsg.Write((short) (projectileAngles[i] / 3.1415927f * 180f));
                    pmsg.Write(projectileIDs[i]);
                    pmsg.Write(projectileValids[i]);

                }
            }
            server.SendToAll(pmsg, NetDeliveryMethod.ReliableSequenced);
        }

        //18 > 19
        private void serverSendPlayerShoteded(NetIncomingMessage aMsg) // todo - make sure player wasn't lying
        {
            try // hammer balls being damaged still doesn't really work for some reason
            {
                short targetID = aMsg.ReadInt16();
                short weaponID = aMsg.ReadInt16();
                short projectileID = aMsg.ReadInt16();
                float hitX = aMsg.ReadFloat();
                float hitY = aMsg.ReadFloat();
                if (tryFindPlayerbyConnection(aMsg.SenderConnection, out Player attacker))
                {
                    if (tryFindPlayerByID(targetID, out Player target))
                    {
                        Logger.testmsg($"Found Target: {target.myName} (ID: {target.myID})");
                        Logger.testmsg($"Received ProjectileID: {projectileID}");
                        Logger.testmsg($"Target AttackIDCounts: {attacker.AttackCount}");
                        Logger.testmsg($"Target ProjectileList Key Count: {attacker.ProjectileList.Keys.Count}");
                        Logger.testmsg($"IsProjectileID -1: {projectileID == -1}\nProjectile ID: {projectileID}\nContainsKey? {attacker.ProjectileList.ContainsKey(projectileID)}");
                        if ((projectileID == -1) || attacker.ProjectileList.ContainsKey(projectileID))
                        {
                            Weapon weapon = s_WeaponsList[weaponID];
                            target.LastAttackerID = attacker.myID;
                            target.LastWeaponID = weaponID;
                            target.LastShotID = projectileID;
                            if (target.vehicleID == -1)
                            {
                                if (target.ArmorTapes == 0)
                                {
                                    int damage_c = weapon.Damage;
                                    Logger.testmsg($"Damage Calculation1: {damage_c}");
                                    if (projectileID >= 0)
                                    {
                                        Projectile p_proj = attacker.ProjectileList[projectileID];
                                        damage_c = weapon.Damage + (p_proj.WeaponRarity * weapon.DamageIncrease);
                                        //Logger.testmsg($"Projectile Stats\nRarity: {p_proj.WeaponRarity}\nPID: RID :: {p_proj.WeaponID}:{weapon.JSONIndex}");
                                        Logger.testmsg($"New Damage Calculation: {damage_c}");
                                    }
                                    test_damagePlayer(target, damage_c, attacker.myID, weaponID);
                                    serverSendShotInfo(attacker.myID, target.myID, projectileID, 0, -1, 0);
                                    if (weapon.Name == "GunDart")
                                    {
                                        bool _reset = (target.DartTicks == 0);
                                        int ticks_c = svd_DDGTicksToAdd;
                                        if ((target.DartTicks + ticks_c) > svd_DDGMaxDmgTicks)
                                        {
                                            ticks_c = svd_DDGMaxDmgTicks - target.DartTicks;
                                        }
                                        target.DartTicks += ticks_c;
                                        if (_reset)
                                        {
                                            target.DartNextTime = DateTime.Now.AddMilliseconds(600);
                                        }
                                    }
                                }
                                else
                                {
                                    Logger.DebugServer($"Weapon Name: {weapon.Name}\nWeapon ADamage: {weapon.ArmorDamage}\nWeapon ADamageOver: {weapon.ArmorDamageOverride}");
                                    byte dink = weapon.ArmorDamage;
                                    if (weapon.ArmorDamageOverride > 0)
                                    {
                                        dink = weapon.ArmorDamage;
                                    }
                                    if (weapon.Name == "GunMagnum")
                                    {
                                        Logger.missingHandle("THIS IS JUST TO SAY YEAH THIS MAGNUM CHECK WORKY");
                                        dink = 2;
                                    }
                                    if ((target.ArmorTapes - dink) <= 0)
                                    {
                                        target.ArmorTapes = 0;
                                        dink = target.ArmorTapes;
                                    }
                                    else
                                    {
                                        target.ArmorTapes -= dink;
                                    }
                                    serverSendShotInfo(attacker.myID, target.myID, projectileID, dink, -1, 0);
                                    if (weapon.PenetratesArmor) // this is another update spot for 2020+ versions. this went from True/False > Percent-Based
                                    {
                                        bool _reset = (target.DartTicks == 0);
                                        int damage_c = weapon.Damage + (attacker.ProjectileList[projectileID].WeaponRarity * weapon.DamageIncrease);
                                        int ticks_c = svd_DDGTicksToAdd;
                                        if ( (target.DartTicks + ticks_c) > svd_DDGMaxDmgTicks)
                                        {
                                            ticks_c = svd_DDGMaxDmgTicks - target.DartTicks;
                                        }
                                        target.DartTicks += ticks_c;
                                        if (_reset)
                                        {
                                            target.DartNextTime = DateTime.Now.AddMilliseconds(600);
                                        }
                                        test_damagePlayer(target, damage_c, attacker.myID, weaponID);
                                        serverSendShotInfo(attacker.myID, target.myID, projectileID, 0, -1, 0);
                                    }
                                    else if (weapon.WeaponType == WeaponType.Melee)
                                    {
                                        test_damagePlayer(target, (int)Math.Floor(weapon.Damage / 0.5f), attacker.myID, weaponID);
                                    }
                                }
                            }
                            else // do hammer stuff
                            {
                                Hampterball _hammer = HamsterballList[target.vehicleID];
                                int _balldmg = weapon.ArmorDamage;
                                if (weapon.ArmorDamageOverride > 0)
                                {
                                    _balldmg = weapon.ArmorDamageOverride;
                                }
                                if ((_hammer.HP - _balldmg) <= 0)
                                {
                                    _hammer.HP = 0;
                                    NetOutgoingMessage vehiclegone = server.CreateMessage();
                                    vehiclegone.Write((byte)58);
                                    vehiclegone.Write(_hammer.VehicleID);
                                    vehiclegone.Write(target.position_X);
                                    vehiclegone.Write(target.position_Y);
                                    server.SendToAll(vehiclegone, NetDeliveryMethod.ReliableOrdered);
                                    target.vehicleID = -1;
                                }
                                else
                                {
                                    _hammer.HP = (byte)(_hammer.HP - _balldmg);
                                }
                                serverSendShotInfo(attacker.myID, target.myID, projectileID, 0, _hammer.VehicleID, _hammer.HP);
                            }
                        }
                        else
                        {
                            Logger.Failure($"[PlayerShotHandle] [ERROR] ProjectileID not MeleeID nor in Target's ProjectileList");
                        }
                    }
                    else
                    {
                        Logger.Failure($"[PlayerShotHandle] [ERROR] Could not locate the desired target player. Skipping out on this method.");
                        return;
                    }
                }
                else
                {
                    Logger.Failure("[PlayerShotHandle] [ERROR] Could not locate the sender in player list.");
                    return;
                }
            } catch (Exception except)
            {
                Logger.Failure($"[ServerSendPlayerShoted] ERROR\n{except}");
            }
        }
        // working on this
        private void test_damagePlayer(Player aPlayer, int aDamage, short aSourceID, short aWeaponID)
        {
            Logger.DebugServer($"Player {aPlayer.myName} (ID: {aPlayer.myID}) Health: {aPlayer.HP}\nDamage Attempt: {aDamage}");
            if ((aPlayer.HP - aDamage) <= 0)
            {
                Logger.DebugServer("This damage attempt resulted in the death of the player.");
                aPlayer.HP = 0;
                aPlayer.isAlive = false;
                shouldUpdateAliveCount = true;
                ServerAMSG_KillAnnouncement(aPlayer.myID, aPlayer.position_X, aPlayer.position_Y, aSourceID, aWeaponID);
                return;
            }
            aPlayer.HP -= (byte)aDamage;
            Logger.DebugServer($"Final Health: {aPlayer.HP}");
        }

        private void serverSendShotInfo(short aMeanie, short aTarget, short aProjectileID, byte aDinkAmount, short aVehicleID, byte aVehicleHP)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)19);
            msg.Write(aMeanie);        // Short | AttackerID
            msg.Write(aTarget);        // Short | TargetID
            msg.Write(aProjectileID);  // Short | ProjectileID
            msg.Write(aDinkAmount);    // Byte  | ArmorDinkCount << How much armor to remove
            msg.Write(aVehicleID);     // Short | VehicleID << use -1 if there's no vehicle to deal with
            msg.Write(aVehicleHP);     // Byte  | VehicleHP << I think this just sets it to whatever the value is
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        /// <summary>
        /// Handles a chat message the server has received.
        /// </summary>
        /// <param name="message"></param>
        private void serverHandleChatMessage(NetIncomingMessage message) // TODO - Fix/redo or remove the """Command""" feature
        {
            Logger.Header("Chat message. Wonderful!");
            //this is terrible. we are aware. have fun.
            if (message.PeekString().StartsWith("/"))
            {

                string[] command = message.PeekString().Split(" ", 8);
                string responseMsg = "command executed... no info given...";
                short id, id2, amount;
                float cPosX, cPosY;
                Logger.Warn($"Player {player_list[getPlayerArrayIndex(message.SenderConnection)].myID} ({player_list[getPlayerArrayIndex(message.SenderConnection)].myName}) used {command[0]}");
                switch (command[0])
                {
                    case "/help":
                        Logger.Success("user has used help command");
                        if (command.Length >= 2)
                        {
                            switch (command[1])
                            {
                                case "help":
                                    responseMsg = "\nThis command will give information about other commands!\nUsage: /help {page}";
                                    break;
                                case "heal":
                                    responseMsg = "\nHeals a certain player's health by the inputed value.\nExample: /heal 0 50";
                                    break;
                                case "teleport":
                                    responseMsg = "\nTeleports the user to provided cordinates.\nExample: /teleport 200 500";
                                    break;
                                case "tp":
                                    responseMsg = "\nTeleports given player_1 TO given player_2.\nExample: /teleport 2 0";
                                    break;
                                case "moveplayer":
                                    responseMsg = "\nTeleports provided player to given cordinates.\nExample: /teleport 0 5025 1020";
                                    break;
                                /*case "2":
                                    responseMsg = "Command Page 2 -- List of usabble Commands\n/help {page/command}\n/heal {ID} {AMOUNT}";
                                    break;
                                case "3":

                                    break;*/
                                default:
                                    responseMsg = $"Invalid help entry '{command[1]}'.\nPlease see '/help' for a list of usable commands.";
                                    break;
                            }
                        }
                        else
                        {
                            responseMsg = "\n(1) List of usable commads:" +
                                "\n/help {page}" +
                                "\n/heal {ID} {AMOUNT}" +
                                "\n/sethp {ID} {AMOUNT}" +
                                "\n/teleport {positionX} {positionY}" +
                                "\n/tp {playerID1} {playerID2}" +
                                "\n/moveplayer {playerID} {X} {Y}" +
                                "\nType '/help [command]' for more information";
                        }

                        break;
                    case "/heal":
                        Logger.Success("user has heal command");
                        if (command.Length > 2)
                        {
                            try
                            {
                                id = short.Parse(command[1]);
                                amount = short.Parse(command[2]);
                                if (amount - player_list[id].HP <= 0)
                                {
                                    player_list[id].HP += (byte)amount;
                                    if (player_list[id].HP > 100) { player_list[id].HP = 100; }
                                    responseMsg = $"Healed player {id} ({player_list[id].myName} by {amount})";
                                }
                                else
                                {
                                    responseMsg = "Wrong player ID or provided health value is too high.";
                                }
                            }
                            catch
                            {
                                responseMsg = "One or both arguments were not integer values. please try again.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /heal {ID} {AMOUNT}"; }
                        break;
                    case "/sethp":
                        if (command.Length > 2)
                        {
                            try
                            {
                                id = short.Parse(command[1]);
                                amount = short.Parse(command[2]);
                                if (amount > 100) { amount = 100; }
                                player_list[id].HP = (byte)amount;
                                responseMsg = $"Set player {id} ({player_list[id].myName})'s health to {amount}";
                            }
                            catch
                            {
                                responseMsg = "One or both arguments were not integer values. please try again.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /sethp {ID} {AMOUNT}"; }
                        break;
                    case "/teleport":
                        if (command.Length > 3)
                        {
                            try
                            {
                                id = short.Parse(command[1]);
                                cPosX = float.Parse(command[2]);
                                cPosY = float.Parse(command[3]);

                                NetOutgoingMessage forcetoPos = server.CreateMessage();
                                forcetoPos.Write((byte)8); forcetoPos.Write(id);
                                forcetoPos.Write(cPosX); forcetoPos.Write(cPosY); forcetoPos.Write(false);
                                server.SendToAll(forcetoPos, NetDeliveryMethod.ReliableOrdered);

                                player_list[id].position_X = cPosX;
                                player_list[id].position_Y = cPosY;
                                responseMsg = $"Moved player {id} ({player_list[id].myName}) to ({cPosX}, {cPosY}). ";
                            }
                            catch
                            {
                                responseMsg = "One or both arguments were not integer values. please try again.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /teleport {ID} {positionX} {positionY}"; }
                        break;
                    case "/tp":
                        if (command.Length > 2)
                        {
                            try
                            {
                                id = short.Parse(command[1]);
                                id2 = short.Parse(command[2]);


                                NetOutgoingMessage forcetoPos = server.CreateMessage();
                                forcetoPos.Write((byte)8); forcetoPos.Write(id);
                                forcetoPos.Write(player_list[id2].position_X);
                                forcetoPos.Write(player_list[id2].position_Y);
                                forcetoPos.Write(false);
                                server.SendToAll(forcetoPos, NetDeliveryMethod.ReliableOrdered);

                                player_list[id].position_X = player_list[id2].position_X;
                                player_list[id].position_Y = player_list[id2].position_Y;
                                responseMsg = $"Moved player {id} ({player_list[id].myName}) to player {id2} ({player_list[id2].myName}). ";
                            }
                            catch
                            {
                                responseMsg = "One or both arguments were not integer values. please try again.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /tp {playerID1} {playerID2}"; }
                        break;
                    case "/time":
                        if (!matchStarted)
                        {
                            if (command.Length == 2)
                            {
                                double newTime;
                                if (double.TryParse(command[1], out newTime))
                                {
                                    responseMsg = $"New time which the game will start: {newTime}";
                                    timeUntilStart = newTime;
                                    NetOutgoingMessage sTimeMsg2 = server.CreateMessage();
                                    sTimeMsg2.Write((byte)43);
                                    sTimeMsg2.Write(timeUntilStart);
                                    server.SendToAll(sTimeMsg2, NetDeliveryMethod.ReliableOrdered);
                                }
                                else
                                {
                                    responseMsg = $"Inputed value '{command[1]}' is not a valid time.\nValid input example: /time 20";
                                }
                            }
                            else
                            {
                                responseMsg = $"The game will begin in {timeUntilStart} seconds.";
                                NetOutgoingMessage sTimeMsg = server.CreateMessage();
                                sTimeMsg.Write((byte)43);
                                sTimeMsg.Write(timeUntilStart);
                                server.SendToAll(sTimeMsg, NetDeliveryMethod.ReliableOrdered);
                            }
                        }
                        else
                        {
                            responseMsg = "You cannot change the start time. The match has already started.";
                        }
                        break;
                    case "/makecircle":
                        if (command.Length == 8)
                        {
                            try
                            {
                                float gx1, gy1, gx2, gy2, gr1, gr2, gtime;
                                gx1 = float.Parse(command[1]);
                                gy1 = float.Parse(command[2]);
                                gr1 = float.Parse(command[3]);
                                gx2 = float.Parse(command[4]);
                                gy2 = float.Parse(command[5]);
                                gr2 = float.Parse(command[6]);
                                gtime = float.Parse(command[7]);

                                NetOutgoingMessage gCircCmdMsg = server.CreateMessage();
                                gCircCmdMsg.Write((byte)33);
                                gCircCmdMsg.Write(gx1); gCircCmdMsg.Write(gy1);
                                gCircCmdMsg.Write(gx2); gCircCmdMsg.Write(gy2);
                                gCircCmdMsg.Write(gr1); gCircCmdMsg.Write(gr2);
                                gCircCmdMsg.Write(gtime);

                                server.SendToAll(gCircCmdMsg, NetDeliveryMethod.ReliableOrdered);
                                gasAdvanceTimer = (double)gtime;
                                sv_safeX = gx2;
                                sv_safeY = gy2;
                                sv_safeRadius = gr2;
                                sv_isGasReal = true;
                                responseMsg = $"Started Gas Warning:\nCirlce Major:\nCenter: ({gx1}, {gy1})\nRadius: {gr1}\nCirlce Minor:\nCenter: ({gx2}, {gy2})\nRadius: {gr2}\n\nTime Until Incoming: ~{gtime} seconds";

                            }
                            catch
                            {
                                responseMsg = "All fields for this command are integer values. One or more argument was not an integer. Please try again. (Valid Ex: 1, 0.25; Invalid: 1/2, one)";
                            }
                        }
                        else
                        {
                            responseMsg = "Invalid arguments. Command Usage: /makecricle {C1 Position X} {C1 Position Y} {C1 Radius} {C2 Position X} {C2 Position Y} {C2 Radius} {DELAY}";
                        }
                        break;
                    case "/list":
                        NetOutgoingMessage plrlistMsg = server.CreateMessage();
                        plrlistMsg.Write((byte)97);
                        plrlistMsg.Write("heeey idk what this really does tbh...");
                        server.SendToAll(plrlistMsg, NetDeliveryMethod.ReliableOrdered);
                        responseMsg = "command executed successfully. anything happen?";
                        break;
                    case "/divemode":
                        if (command.Length > 1)
                        {
                            if (short.TryParse(command[1], out short diveID))
                            {
                                if (tryFindPlayerByID(diveID, out Player diver))
                                {
                                    if (command.Length > 2 && bool.TryParse(command[2], out bool givenMode))
                                    {
                                        diver.isDiving = givenMode;
                                        ServerAMSG_ParachuteUpdate(diver.myID, diver.isDiving);
                                        responseMsg = $"Parachute mode for {diver.myName} (ID: {diver.myID}) set to: {diver.isDiving}";
                                    }
                                    else
                                    {
                                        diver.isDiving = !diver.isDiving;
                                        ServerAMSG_ParachuteUpdate(diver.myID, diver.isDiving);
                                        responseMsg = $"Parachute mode for {diver.myName} (ID: {diver.myID}) set to: {diver.isDiving}";
                                    }
                                }
                                else
                                {
                                    responseMsg = $"Could not locate a player with the ID \"{diveID}\"";
                                }
                            }
                            else
                            {
                                responseMsg = $"Could not locate a player with the ID \"{command[1]}\"";
                            }
                        }
                        else
                        {
                            if (tryFindPlayerbyConnection(message.SenderConnection, out Player diver))
                            {
                                diver.isDiving = !diver.isDiving;
                                ServerAMSG_ParachuteUpdate(diver.myID, diver.isDiving);
                                responseMsg = $"Parachute mode for Player {diver.myID}({diver.myName}) set to {diver.isDiving.ToString().ToUpper()}";
                            }
                            else
                            {
                                responseMsg = $"There was an error locating the player who used dive mode for some reason...";
                            }
                        }
                        break;
                    case "/startshow":
                        if (command.Length > 1)
                        {
                            byte showNum;
                            if (byte.TryParse(command[1], out showNum))
                            {
                                NetOutgoingMessage cParaMsg = server.CreateMessage();
                                cParaMsg.Write((byte)104);
                                cParaMsg.Write(showNum);
                                server.SendToAll(cParaMsg, NetDeliveryMethod.ReliableOrdered);
                                responseMsg = $"Played AviaryShow #{showNum}";
                            }
                            else
                            {
                                responseMsg = $"Provided value '{command[1]}' is not a valid show.\nValid shows IDs: [0, 1, 2]";
                            }
                        }
                        else
                        {
                            responseMsg = $"Insufficient amount of arguments provided. This command takes 1. Given: {command.Length - 1}.";
                        }
                        break;
                    case "/forceland":
                        if (command.Length > 1)
                        {
                            short forceID;
                            if (short.TryParse(command[1], out forceID))
                            {
                                if (!(forceID < 0) && !(forceID > 64))
                                {
                                    for (int fl = 0; fl < player_list.Length; fl++)
                                    {
                                        if (player_list[fl] != null && player_list[fl]?.myID == forceID)
                                        {
                                            NetOutgoingMessage sendEject = server.CreateMessage();
                                            sendEject.Write((byte)8);
                                            sendEject.Write(player_list[fl].myID);
                                            sendEject.Write(player_list[fl].position_X);
                                            sendEject.Write(player_list[fl].position_Y);
                                            sendEject.Write(true);
                                            server.SendToAll(sendEject, NetDeliveryMethod.ReliableSequenced);
                                            responseMsg = "Command executed successfully?";
                                            break;
                                        }
                                        responseMsg = $"Player ID {forceID} not found.";
                                    }
                                }
                                else
                                {
                                    responseMsg = $"Provided argument, '{forceID}' not valid. 0-64.";
                                }
                            }
                        }
                        else
                        {
                            responseMsg = $"Insufficient amount of arguments provided. This command takes 1. Given: {command.Length - 1}.";
                        }
                        break;
                    case "/sendjunk":
                        Logger.Success("user used... sending JUNK?!!");
                        NetOutgoingMessage LOL = server.CreateMessage();
                        LOL.Write((byte)97);
                        LOL.Write("Hello, this is a string!");
                        LOL.Write(245f);
                        LOL.Write("There was a float somewhere there (2)...");
                        LOL.Write((short)5);
                        LOL.Write("There was a short somewhere there (5)...");
                        LOL.Write(true);
                        LOL.Write("There was a bool somewhere there (true)...");
                        LOL.Write("But that's all for now. Have fun with this RELIABLE ORDERED message!");
                        server.SendToAll(LOL, NetDeliveryMethod.ReliableUnordered);
                        responseMsg = "All good. Have fun lol";
                        break;
                    case "/pray":
                        NetOutgoingMessage banan = server.CreateMessage();
                        banan.Write((byte)110);
                        banan.Write(getPlayerID(message.SenderConnection)); //player
                        banan.Write(0f);//interval
                        banan.Write((byte)255);
                        int count = 0;
                        for (int i = 0; i < 8; i++)
                        {
                            for (int j = 0; j < 32; j++)
                            {
                                count++;
                                if (count != 256)
                                {
                                    banan.Write((float)(3683f + j)); //x 
                                    banan.Write((float)(3549f - i)); //y
                                }
                            }
                        }
                        Console.WriteLine(count);
                        banan.Write((byte)1);
                        banan.Write(getPlayerID(message.SenderConnection));

                        server.SendToAll(banan, NetDeliveryMethod.ReliableUnordered);
                        responseMsg = "Praise Banan.";
                        break;
                    case "/kill":
                        //just wanted to test. this is very very broken just like the rest of the """"commands"""".
                        if (command.Length > 1)
                        {
                            if (tryFindPlayerIDbyName(command[1], out short sPlayerID) || short.TryParse(command[1], out sPlayerID))
                            {
                                if (tryFindIndexByID(sPlayerID, out int index))
                                {
                                    Player killPlayer = player_list[index]; //is this worth it?
                                    NetOutgoingMessage kill = server.CreateMessage();
                                    kill.Write((byte)15);               // Message Type 15

                                    kill.Write(killPlayer.myID);        //Dying player ID
                                    kill.Write(killPlayer.position_X);  //Dying Player X
                                    kill.Write(killPlayer.position_Y);  //Dying Player Y

                                    kill.Write((short)-3);              //Killer's Player ID
                                    kill.Write((short)-1);              //Killer's Weapon ID

                                    server.SendToAll(kill, NetDeliveryMethod.ReliableSequenced);
                                    responseMsg = $"Killed Player {sPlayerID} ({killPlayer.myName}).";
                                    Logger.Basic($"Killed player {sPlayerID} ({killPlayer.myName}).");
                                }
                                else
                                { responseMsg = $"Could not locate player '{command[1]}'."; }
                            }
                            else { responseMsg = $"Could not locate player '{command[1]}'."; }
                        }
                        else { responseMsg = "Not enough arguments provided."; }
                        break;

                    case "/removeweapons":
                        NetOutgoingMessage rmMsg = server.CreateMessage();
                        rmMsg.Write((byte)125);
                        rmMsg.Write(getPlayerID(message.SenderConnection));
                        server.SendToAll(rmMsg, NetDeliveryMethod.ReliableUnordered);
                        responseMsg = $"Weapons removed for {player_list[getPlayerArrayIndex(message.SenderConnection)].myName}";
                        break;
                    case "/ghost":
                        //TODO : remove or make better command -- testing only
                        NetOutgoingMessage ghostMsg = server.CreateMessage();
                        ghostMsg.Write((byte)105);
                        server.SendMessage(ghostMsg, message.SenderConnection, NetDeliveryMethod.ReliableUnordered);
                        break;
                    case "/rain":
                        if (command.Length > 1)
                        {
                            if (float.TryParse(command[1], out float duration))
                            {
                                NetOutgoingMessage rainTime = server.CreateMessage();
                                rainTime.Write((byte)35);
                                rainTime.Write(duration);
                                server.SendToAll(rainTime, NetDeliveryMethod.ReliableSequenced);
                                responseMsg = $"It's raining it's pouring the old man is snoring! (rain duration: ~{duration} seconds)";
                            }
                            else
                            {
                                responseMsg = $"Invalid duration '{command[1]}'";
                            }
                        }
                        else
                        {
                            responseMsg = "Please enter a duration amount.";
                        }
                        break;
                    case "/togglewin":
                        sv_doWins = !sv_doWins;
                        responseMsg = $"sv_doWins set to \"{sv_doWins.ToString().ToUpper()}\"";
                        break;
                    case "/drink":
                        Logger.Success("drink command");
                        byte drinkiesAmount;
                        if (command.Length > 2)
                        {
                            try
                            {
                                id = short.Parse(command[1]);
                                amount = short.Parse(command[2]);
                                player_list[id].Drinkies = (byte)amount;
                                responseMsg = $"done {amount}";
                            }
                            catch
                            {
                                responseMsg = "One or both arguments were not integer values. please try again.";
                            }
                        } else if (command.Length > 1)
                        {
                            try
                            {
                                drinkiesAmount = byte.Parse(command[1]);
                                player_list[getPlayerArrayIndex(message.SenderConnection)].Drinkies = drinkiesAmount;
                                responseMsg = $"Set your drink amount to {drinkiesAmount}.";
                            }
                            catch
                            {
                                responseMsg = "Error parsing AMOUNT argument. Usage: /drink {Amount (Integer)}";
                            }
                        }
                        break;
                    case "/tapes":
                        Logger.Success("tape command");
                        if (command.Length > 2)
                        {
                            try
                            {
                                id = short.Parse(command[1]);
                                amount = short.Parse(command[2]);
                                player_list[id].ArmorTapes = (byte)amount;
                                responseMsg = $"done {amount}";
                            }
                            catch
                            {
                                responseMsg = "One or both arguments were not integer values. please try again.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /tape {ID} {AMOUNT}"; }
                        break;
                    case "/pos":
                        Logger.Success("position command");
                        try
                        {
                            Player ___this = player_list[getPlayerArrayIndex(message.SenderConnection)];
                            responseMsg = $"Your position: ({___this.position_X}, {___this.position_Y})";
                        }
                        catch
                        {
                            responseMsg = "Error processing command.";
                        }
                        break;
                    case "/spawndrink":
                        Logger.Success("spawndrink command");
                        if (command.Length > 3)
                        {
                            try
                            {
                                amount = short.Parse(command[1]);
                                cPosX = float.Parse(command[2]);
                                cPosY = float.Parse(command[3]);
                                server.SendToAll(MakeNewDrinkLootItem(amount, new float[] { cPosX, cPosY, cPosX, cPosY }), NetDeliveryMethod.ReliableSequenced);
                                responseMsg = $"Created Drink item with {amount}Oz of Juice @ ({cPosX}, {cPosY})";
                            }
                            catch
                            {
                                responseMsg = "Error processing command.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /spawndrink {amount}, {X}, {Y}"; }
                        break;
                    case "/spawntape":
                        Logger.Success("spawntape command");
                        if (command.Length > 3)
                        {
                            try
                            {
                                amount = short.Parse(command[1]);
                                cPosX = float.Parse(command[2]);
                                cPosY = float.Parse(command[3]);
                                server.SendToAll(MakeNewTapeLootItem((byte)amount, new float[] { cPosX, cPosY, cPosX, cPosY }), NetDeliveryMethod.ReliableSequenced);
                                responseMsg = $"Created Tape LootItem which gives {amount} Tape(s) @ ({cPosX}, {cPosY})";
                            }
                            catch
                            {
                                responseMsg = "Error processing command.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /spawntape {amount}, {X}, {Y}"; }
                        break;
                    case "/advancetimer":
                        Logger.Success("gas advance timer set command");
                        if (command.Length > 1)
                        {
                            try
                            {
                                double time = double.Parse(command[1]);
                                gasAdvanceLength = time;
                            }
                            catch
                            {
                                responseMsg = $"Invalid double \"{command[1]}\".";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /advancetimer (SECONDS)"; }
                        break;
                    case "/gascheck":
                        Logger.Success("Toggle Gas-Check command used.");
                        if (command.Length > 1)
                        {
                            if (bool.TryParse(command[1], out bool newset))
                            {
                                sv_isGasReal = newset;
                                responseMsg = $"sv_isGasReal set to \"{sv_isGasReal}\".";
                            }
                            else
                            {
                                responseMsg = $"Unabled to parse provided argument \"{command[1]}\" as a BOOL value.";
                            }
                        }
                        else
                        {
                            sv_isGasReal = !sv_isGasReal;
                            responseMsg = $"sv_isGasReal set to \"{sv_isGasReal}\".";
                        }
                        break;
                    default:
                        Logger.Failure("Invalid command used.");
                        responseMsg = "Invalid command provided. Please see '/help' for a list of commands.";
                        break;
                }
                //now send response to player...
                NetOutgoingMessage allchatmsg = server.CreateMessage();
                allchatmsg.Write((byte)94);
                allchatmsg.Write(getPlayerID(message.SenderConnection)); //ID of player who sent msg
                allchatmsg.Write(responseMsg);
                server.SendToAll(allchatmsg, NetDeliveryMethod.ReliableUnordered);
                //server.SendMessage(allchatmsg, message.SenderConnection, NetDeliveryMethod.ReliableUnordered);
            }
            else
            {
                //Regular message.
                NetOutgoingMessage allchatmsg = server.CreateMessage();
                allchatmsg.Write((byte)26);
                allchatmsg.Write(getPlayerID(message.SenderConnection)); //ID of player who sent msg
                allchatmsg.Write(message.ReadString());
                allchatmsg.Write(false);
                server.SendToAll(allchatmsg, NetDeliveryMethod.ReliableUnordered);
            }
        }

        //got 27 > send 28
        private void serverSendSlotUpdate(NetConnection snd, byte sentSlot)
        {
            Player plr = player_list[getPlayerArrayIndex(snd)];
            plr.ActiveSlot = sentSlot;

            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)28);
            msg.Write(plr.myID);
            msg.Write(sentSlot);
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        //got 36 > send 37
        private void serverSendBeganGrenadeThrow(NetIncomingMessage message)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)37);
            msg.Write(getPlayerID(message.SenderConnection));
            msg.Write(message.ReadInt16());
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }
        //got 38 > send 39
        private void serverSendGrenadeThrowing(NetIncomingMessage message)
        {
            Player _plr = player_list[getPlayerArrayIndex(message.SenderConnection)];
            if ((_plr.MyLootItems[2].GiveAmount - 1) >= 0)
            {
                _plr.MyLootItems[2].GiveAmount -= 1;
                NetOutgoingMessage msg = server.CreateMessage();
                msg.Write((byte)39);
                for (byte i = 0; i < 3; i++)
                {
                    msg.Write(message.ReadFloat()); //x
                    msg.Write(message.ReadFloat()); //y
                }
                short grenadeID = message.ReadInt16();
                msg.Write(grenadeID);
                msg.Write(grenadeID);//likely needs to be unique. not sure how. maybe just make the server have its own counter
                server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
                if (_plr.MyLootItems[2].GiveAmount == 0)
                {
                    _plr.MyLootItems[2] = new LootItem(-1, LootType.Collectable, WeaponType.NotWeapon, "NOTHING", 0, 0);
                }
                return;
            }
            Logger.Failure($"[[serverSendGrenadeThrowing]] - Amount of grenades-1 = <0 | {_plr.MyLootItems[2].GiveAmount - 1}");
        }
        // ClientSentGrenadeFinished [40] >> ServerSentGrenadeFinished[41]
        private void serverSendGrenadeFinished(NetIncomingMessage aMsg)
        {
            // TODO -- server must figure out who was in the blast.
            float x = aMsg.ReadFloat();
            float y = aMsg.ReadFloat();
            float nadeHeight = aMsg.ReadFloat(); ;
            short nadeID = aMsg.ReadInt16();

            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)41); msg.Write(getPlayerID(aMsg.SenderConnection));
            msg.Write(nadeID);
            msg.Write(x); msg.Write(y);
            msg.Write(nadeHeight);
            msg.Write((byte)1);
            msg.Write((short)0);// this should be a list of all players that are within the blast radius
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }
        /// <summary>
        /// Sends a message to all connected clients that the provided PlayerID has stopped drinking. This method does not set the player's IsDrinking property to false.
        /// </summary>
        private void ServerAMSG_EndedDrinking(short aID) // Server Announcement Message - Someone Ended Drinking
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)49);
            msg.Write(aID);
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        //send 51
        private void serverSendCoconutEaten(NetIncomingMessage message)
        {
            Player client = player_list[getPlayerArrayIndex(message.SenderConnection)];
            if (client.HP < 200)
            {
                client.HP += 5;
                if (client.HP > 200) { client.HP = 200; }
            }
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)52);
            msg.Write(getPlayerID(message.SenderConnection));
            msg.Write(message.ReadInt16());
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        // ClientSentCutGrass[53] >> ServerSentCutGrass[54]
        private void serverSendCutGrass(NetIncomingMessage message)
        {
            // TOOD - Generate some item loot after cutting grass ?
            byte bladesCut = message.ReadByte();
            NetOutgoingMessage grassMsg = server.CreateMessage();
            grassMsg.Write((byte)54);
            grassMsg.Write(getPlayerID(message.SenderConnection));
            grassMsg.Write(bladesCut);
            for (byte i = 0; i < bladesCut; i++)
            {
                grassMsg.Write(message.ReadInt16()); //x
                grassMsg.Write(message.ReadInt16()); //y
            }
            server.SendToAll(grassMsg, NetDeliveryMethod.ReliableOrdered);
        }

        //send 61
        private void serverSendVehicleHitPlayer(NetIncomingMessage aMsg) // TODO: needs updating/cleanup
        {
            Logger.Header("[Vehicle Hit Player] Packet Received");
            if (tryFindPlayerbyConnection(aMsg.SenderConnection, out Player _attacker))
            {
                if (_attacker.vehicleID != -1) // make sure the player is actually in a vehicle?
                {
                    try
                    {
                        short _targetID = aMsg.ReadInt16();
                        float _speed = aMsg.ReadFloat();
                        Logger.Basic($"[Vehicle Hit Player - Calculations] Attacker ID: {_attacker}; Target ID: {_targetID}; Hamsterball Speed: {_speed}");
                        if (tryFindPlayerByID(_targetID, out Player _target))
                        {
                            NetOutgoingMessage msg = server.CreateMessage();
                            msg.Write((byte)61);
                            msg.Write(_attacker.myID); // person who is attacking
                            msg.Write(_targetID); // person who got hit
                            msg.Write(false); // has killed
                            msg.Write((short)0); // vehicle ID
                            msg.Write((byte)0); // no clue
                            msg.Write((byte)2); // no clue

                            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
                        }
                        else
                        {
                            Logger.Failure($"[Vehicle Hit Player - Error] Could not locate Player object for found _targetID. Search ID: {_targetID}");
                        }
                    }
                    catch (Exception vhpEx)
                    {
                        Logger.Failure($"[Vehicle Hit Player - Error] There was an error processing this packet. Wrong header or malformed data?\n{vhpEx}");
                    }
                }
                else
                {
                    Logger.Failure("[Vehicle Hit Player - Error] The player doesn't even have an assigned VehicleID. Request ignored. Check *why* this player could do this without being in a vehicle.");
                }
            }
            else
            {
                Logger.Failure("[Vehicle Hit Player - Error] Packet Received, however sent PlayerID is nonexistent in Player List. (Ignoring the rest of the request.)");
            }
            /*
            try
            {
                short _attacker = getPlayerID(aMsg.SenderConnection);
                short _target = aMsg.ReadInt16();
                float _speed = aMsg.ReadFloat();

                NetOutgoingMessage msg = server.CreateMessage();
                msg.Write((byte)61);
                msg.Write(_attacker); // person who is attacking
                msg.Write(_target); // person who got hit
                msg.Write(false); // has killed
                msg.Write((short)0); // vehicle ID
                msg.Write((byte)0); // no clue
                msg.Write((byte)2); // no clue

                server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
            }*/
            /*
            Logger.Header("--  Vehicle Hit Player  --");
            Logger.Basic($"Target Player ID: {message.ReadInt16()}\nSpeed: {message.ReadFloat()}");
            Player plrA = player_list[getPlayerArrayIndex(message.SenderConnection)];
            NetOutgoingMessage vehicleHit = server.CreateMessage();
            vehicleHit.Write((byte)61); //Message #61
            vehicleHit.Write(plrA.myID); //player who hit
            vehicleHit.Write(message.ReadInt16()); //player who GOT hit
            //TODO: redo player list so that can actually figure out how to find whether or not palyer died
            if (message.ReadFloat() > 50f)
            {
                vehicleHit.Write(true);
            }
            else { vehicleHit.Write(false); }
            vehicleHit.Write(plrA.vehicleID);
            vehicleHit.Write((byte)0); //idk
            vehicleHit.Write((byte)2);

            server.SendToAll(vehicleHit, NetDeliveryMethod.ReliableOrdered);*/
        }

        /// <summary>
        /// Server Handle Player Loot - Handles incoming Looted message
        /// </summary>
        private void ServerH_PlayerLoot(NetIncomingMessage msg)
        {
            //TODO -- cleanup / fix
            /* main issue -- client game seems to repeatedly try and claim a loot item. it does this so fast it can claim...
            ...the same loot item the server is already dealing with. duping it, and also taking another. glitches mostly drinks and tape
            */
            Logger.Header("Player Looted Item");
            try
            {
                Player thisPlayer = player_list[getPlayerArrayIndex(msg.SenderConnection)];
                NetOutgoingMessage _extraLootMSG = null; // Extra loot message to send after telling everyone about loot and junk
                short m_LootID = (short)msg.ReadInt32();
                byte m_PlayerSlot = msg.ReadByte();
                LootItem m_LootToGive = ItemList[m_LootID];

                switch (m_LootToGive.LootType)
                {
                    case LootType.Weapon: // Stupidity
                        Logger.Basic($" -> Player found a weapon.\n{m_LootToGive.LootName}");
                        if (m_PlayerSlot == 1 || m_PlayerSlot == 0) // Weapon 1 or 2 | Not Melee
                        {
                            if (thisPlayer.MyLootItems[m_PlayerSlot].WeaponType != WeaponType.NotWeapon) // means there's already something here
                            {
                                // So problem, can also dupe weapons. Might need to put a cooldown on when a player can pick them up again...
                                LootItem oldLoot = thisPlayer.MyLootItems[m_PlayerSlot];
                                _extraLootMSG = MakeNewGunLootItem(oldLoot.LootName, (short)oldLoot.IndexInList, oldLoot.ItemRarity, oldLoot.GiveAmount, new float[] { thisPlayer.position_X, thisPlayer.position_Y, thisPlayer.position_X, thisPlayer.position_Y });
                                thisPlayer.MyLootItems[m_PlayerSlot] = m_LootToGive;
                            }
                            thisPlayer.MyLootItems[m_PlayerSlot] = m_LootToGive;
                            //Logger.Failure("  -> WARNING NOT YET FULLY HANDLED");
                            break;
                        }
                        if (m_PlayerSlot != 3) // Slot 3 = Throwables. Slot 2 can't be used; and so anything other than 0, 1, and 3 just skip.
                        {
                            Logger.Failure("  -> Player has found a weapon. However, none of the slot it claims to be accessing are valid here.");
                            break;
                        }

                        // Throwable / Slot_3 | PlayerSlot 4 | so... m_PlayerSlot-1 = 2 >> right array index
                        if (thisPlayer.MyLootItems[2].WeaponType != WeaponType.NotWeapon) // Player has throwable here already
                        {
                            // uuuh how do we figure this out ?????
                            Logger.DebugServer($"Throwable LootName Test: Plr: {thisPlayer.MyLootItems[2].LootName}\nThis new loot: {m_LootToGive.LootName}");
                            if (thisPlayer.MyLootItems[2].LootName == m_LootToGive.LootName) // Has this throwable-type already
                            {
                                thisPlayer.MyLootItems[2].GiveAmount += m_LootToGive.GiveAmount;
                                Logger.Basic($"{thisPlayer.MyLootItems[2].LootName} - Amount: {thisPlayer.MyLootItems[2].GiveAmount}");
                                break;
                            }
                            // Else = Player has a throwable, BUT it is a different type so we need to re-spawn the old one
                            _extraLootMSG = MakeNewThrowableLootItem((short)thisPlayer.MyLootItems[2].IndexInList, thisPlayer.MyLootItems[2].GiveAmount, thisPlayer.MyLootItems[2].LootName, new float[] { thisPlayer.position_X, thisPlayer.position_Y, thisPlayer.position_X, thisPlayer.position_Y });
                            // Go give player the loot item.
                            thisPlayer.MyLootItems[2] = m_LootToGive;
                            break;
                        }
                        // Player doesn't have a throwable
                        thisPlayer.MyLootItems[2] = m_LootToGive;
                        break;
                    case LootType.Juices:
                        Logger.Basic($" -> Player found some drinkies. +{m_LootToGive.GiveAmount}");
                        if (thisPlayer.Drinkies + m_LootToGive.GiveAmount > 200)
                        {
                            m_LootToGive.GiveAmount -= (byte)(200 - thisPlayer.Drinkies);
                            thisPlayer.Drinkies += (byte)(200 - thisPlayer.Drinkies);
                            _extraLootMSG = MakeNewDrinkLootItem(m_LootToGive.GiveAmount, new float[] { thisPlayer.position_X, thisPlayer.position_Y, thisPlayer.position_X, thisPlayer.position_Y });
                            break;
                        }
                        thisPlayer.Drinkies += m_LootToGive.GiveAmount;
                        break;
                    case LootType.Tape:
                        Logger.Basic(" -> Player found some tape.");
                        if ((thisPlayer.Tapies + m_LootToGive.GiveAmount) > 5)
                        {
                            m_LootToGive.GiveAmount -= (byte)(5 - thisPlayer.Tapies);
                            thisPlayer.Tapies += (byte)(5 - thisPlayer.Tapies);
                            _extraLootMSG = MakeNewTapeLootItem(m_LootToGive.GiveAmount, new float[] { thisPlayer.position_X, thisPlayer.position_Y, thisPlayer.position_X, thisPlayer.position_Y });
                            break;
                        }
                        thisPlayer.Tapies += m_LootToGive.GiveAmount;
                        break;
                    case LootType.Armor:
                        Logger.Basic($" -> Player got some armor. Tier{m_LootToGive.ItemRarity} - Ticks: {m_LootToGive.GiveAmount}");
                        if (thisPlayer.ArmorTier != 0)
                        {
                            _extraLootMSG = MakeNewArmorLootItem(thisPlayer.ArmorTapes, thisPlayer.ArmorTier, new float[] { thisPlayer.position_X, thisPlayer.position_Y, thisPlayer.position_X, thisPlayer.position_Y });
                            // Update the player's armor.
                            thisPlayer.ArmorTier = m_LootToGive.ItemRarity;
                            thisPlayer.ArmorTapes = m_LootToGive.GiveAmount;
                            break;
                        }
                        thisPlayer.ArmorTier = m_LootToGive.ItemRarity;
                        thisPlayer.ArmorTapes = m_LootToGive.GiveAmount;
                        break;

                    // TODO - make sure server tracks ammo not just spawns it and stuff.
                    case LootType.Ammo: // Ammo Type is stored in LootItem.ItemRarity
                        Logger.Basic($" -> Ammo Loot: AmmoType:Ammount -- {m_LootToGive.ItemRarity}:{m_LootToGive.GiveAmount}");
                        // I can't be bothered right now
                        break;
                }
                NetOutgoingMessage testMessage = server.CreateMessage();
                testMessage.Write((byte)22); // Header / Packet ID
                testMessage.Write(thisPlayer.myID); // Player ID
                testMessage.Write((int)m_LootID); // Loot Item ID
                testMessage.Write(m_PlayerSlot); // Player Slot to update
                if (!matchStarted) // Write Forced Rarity
                {
                    testMessage.Write((byte)4); // Only matters in a lobby
                }
                testMessage.Write((byte)0);
                server.SendToAll(testMessage, NetDeliveryMethod.ReliableSequenced);
                if (_extraLootMSG != null)
                {
                    server.SendToAll(_extraLootMSG, NetDeliveryMethod.ReliableSequenced);
                }
            }
            catch (Exception Except)
            {
                Logger.Failure(Except.ToString());
            }
        }
        /// <summary>
        /// Sends a message to all clients connected to the server telling them that a Player in a certain Vehicle bounced off a wall.
        /// </summary>
        private void ServerMSG_PlayerHamsterballBounce(Player aPlayer) // Send PacketType 63
        {
            // TODO
            // Right now it is fine to just have server call this after calling method to find player object.
            // In the future, I think it would be nice if the method could just be called with the received MSG object and put here to...
            // ... deal with and do all that junk and stuff instead of the way it is now with some somewhat redundant calling and junk
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)63);
            msg.Write(aPlayer.myID);
            msg.Write(aPlayer.vehicleID);
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Sends message to all clients connected to the server that a particular Player started emoting.
        /// </summary>
        private void ServerMSG_SendPerformedEmote(Player aPlayer, NetIncomingMessage aMsg) // Send PacketType 67
        {
            try
            {
                // TODO - Still need to figure out when the player should finish emoting and stuff... :<
                short _EmoteID = aMsg.ReadInt16();
                float _sEmotePosX = aMsg.ReadFloat();
                float _sEmotePosY = aMsg.ReadFloat();
                //float _Duration = aMsg.ReadFloat(); << This is Duration variable we need to figure out how long it should last.

                NetOutgoingMessage msg = server.CreateMessage();
                msg.Write((byte)67);
                msg.Write(aPlayer.myID);
                msg.Write(_EmoteID);
                server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);

                // This is where we can update the info and junk. Like you know, how long the emote is supposed to last?
                aPlayer.position_X = _sEmotePosX;
                aPlayer.position_Y = _sEmotePosY;
            } catch (Exception EX)
            {
                Logger.Failure($"Error processing EmoteMsg from Player {aPlayer.myID}({aPlayer.myName}).\n{EX}");
            }
        }


        //send 75 -- something with minigun?
        private void ServerMSG_DealWithWindup(NetIncomingMessage aMSG)
        {
            // TODO - Maybe not just take client's word on windup?
            try
            {
                short _WeaponID = aMSG.ReadInt16();
                byte _SlotIndex = aMSG.ReadByte();
                NetOutgoingMessage msg = server.CreateMessage();
                msg.Write((byte)75);
                msg.Write(getPlayerID(aMSG.SenderConnection));
                msg.Write(_WeaponID);
                msg.Write(_SlotIndex);
                server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
            } catch (Exception ex)
            {
                Logger.Failure($"[SendPacket 75 ERROR] - {ex}");
            }
        }

        //send 77 -- something wtih minigun?
        private void serverSendAttackWindDown(short plrID, short weaponID)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)77);
            msg.Write(plrID);
            msg.Write(weaponID);
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        //client[47] > server[48] -- pretty much a copy of sendingTape and stuff... info inside btw...
        private void serverSendPlayerStartedHealing(NetConnection sender, float posX, float posY)
        {
            Player plr = player_list[getPlayerArrayIndex(sender)];
            plr.position_X = posX;
            plr.position_Y = posY;
            plr.isDrinking = true;
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)48);
            msg.Write(plr.myID);
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        //r[87] > s[111]
        private void serverSendDepployedTrap(NetIncomingMessage message)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)111);
            msg.Write(getPlayerID(message.SenderConnection));
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        //client[98] > server[99] -- started taping
        /// <summary>
        /// Sends to everyone in the match a player who has started to tape along with their position.
        /// </summary>
        private void serverSendPlayerStartedTaping(NetConnection sender, float posX, float posY)
        {
            Player plr = player_list[getPlayerArrayIndex(sender)];
            plr.position_X = posX;
            plr.position_Y = posY;
            plr.isTaping = true;

            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)99);
            msg.Write(plr.myID);
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }
        /// <summary>
        /// Sends an announcement message to ALL connected NetClients that a particular Player's Parachute Mode has been updated.
        /// </summary>
        private void ServerAMSG_ParachuteUpdate(short aID, bool aIsDiving) // Server Announcement Message -- Parachute Mode Update
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)109); // Byte Header -- Message Type 109 >> Update Parachute Mode
            msg.Write(aID);       // Short PlayerID >> ID of the Player to be updated
            msg.Write(aIsDiving); // Bool IsDiving >> Whether or not the person is Parachuting or diving (is actually flipped --> dive = false; chute = true)
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Creates a NetOutgoingMessage which tells the client about a new Drink LootItem
        /// </summary>
        private NetOutgoingMessage MakeNewDrinkLootItem(short aDrinkAmount, float[] aPositions)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            sv_TotalLootCounter++;              // Increase LootCounter by 1
            msg.Write((byte)20);                // Header       |  Byte
            msg.Write(sv_TotalLootCounter);     // LootID       |  Int
            msg.Write((byte)LootType.Juices);   // LootType     |  Byte
            msg.Write(aDrinkAmount);            // Info/Amount  |  Short
            msg.Write(aPositions[0]);           // Postion X1   |  Float
            msg.Write(aPositions[1]);           // Postion Y1   |  Float
            msg.Write(aPositions[2]);           // Postion X2   |  Float
            msg.Write(aPositions[3]);           // Postion Y2   |  Float
            msg.Write((byte)0);
            ItemList.Add(sv_TotalLootCounter, new LootItem(sv_TotalLootCounter, LootType.Juices, WeaponType.NotWeapon, $"Health Juice-{aDrinkAmount}", 0, (byte)aDrinkAmount));
            return msg;
        }
        /// <summary>
        /// Creates a new Armor LootItem generation message to be sent out, and adds the new item to the loot list. This message MUST be sent.
        /// </summary>
        private NetOutgoingMessage MakeNewArmorLootItem(byte armorTicks, byte ArmorTier, float[] aPositions)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            sv_TotalLootCounter++;              // Increase LootCounter by 1
            msg.Write((byte)20);                // Header       |  Byte
            msg.Write(sv_TotalLootCounter);     // LootID       |  Int
            msg.Write((byte)LootType.Armor);    // LootType     |  Byte
            msg.Write((short)armorTicks);       // Info/Amount  |  Short
            msg.Write(aPositions[0]);           // Postion X1   |  Float
            msg.Write(aPositions[1]);           // Postion Y1   |  Float
            msg.Write(aPositions[2]);           // Postion X2   |  Float
            msg.Write(aPositions[3]);           // Postion Y2   |  Float
            msg.Write(ArmorTier);               // Rarity       |  Byte
            ItemList.Add(sv_TotalLootCounter, new LootItem(sv_TotalLootCounter, LootType.Armor, WeaponType.NotWeapon, $"Armor-Tier{ArmorTier}", ArmorTier, armorTicks));
            return msg;
        }

        /// <summary>
        /// Creates a new Tape LootItem generation message to be sent out, also adding the newly created item into the loot list. This message must be used.
        /// </summary>
        private NetOutgoingMessage MakeNewTapeLootItem(byte tapeAmount, float[] aPositions)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            sv_TotalLootCounter++;              // Increase LootCounter by 1
            msg.Write((byte)20);                // Header       |  Byte
            msg.Write(sv_TotalLootCounter);     // LootID       |  Int
            msg.Write((byte)LootType.Tape);     // LootType     |  Byte
            msg.Write((short)tapeAmount);       // Info/Amount  |  Short
            msg.Write(aPositions[0]);           // Postion X1   |  Float
            msg.Write(aPositions[1]);           // Postion Y1   |  Float
            msg.Write(aPositions[2]);           // Postion X2   |  Float
            msg.Write(aPositions[3]);           // Postion Y2   |  Float
            msg.Write((byte)0);
            ItemList.Add(sv_TotalLootCounter, new LootItem(sv_TotalLootCounter, LootType.Tape, WeaponType.NotWeapon, "Tape", 0, tapeAmount));
            return msg;
        }
        /// <summary>
        /// Creates a new Throwable LootItem generation message to be sent out, also adding the newly created item into the loot list. This message must be used.
        /// </summary>
        private NetOutgoingMessage MakeNewThrowableLootItem(short itemIndex, byte spawnCount, string name, float[] aPositions)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            sv_TotalLootCounter++;              // Increase LootCounter by 1
            msg.Write((byte)20);                // Header              |  Byte
            msg.Write(sv_TotalLootCounter);     // LootID              |  Int
            msg.Write((byte)LootType.Weapon);   // LootType            |  Byte
            msg.Write(itemIndex);               // Info/ Which Weapon  |  Short
            msg.Write(aPositions[0]);           // Postion X1          |  Float
            msg.Write(aPositions[1]);           // Postion Y1          |  Float
            msg.Write(aPositions[2]);           // Postion X2          |  Float
            msg.Write(aPositions[3]);           // Postion Y2          |  Float
            msg.Write(spawnCount);              // Spawn Amount        |  Byte
            ItemList.Add(sv_TotalLootCounter, new LootItem(sv_TotalLootCounter, LootType.Weapon, WeaponType.Throwable, name, 0, (byte)itemIndex, spawnCount));
            return msg;
        }
        /// <summary>
        /// Creates a new Gun LootItem generation message to be sent out, also adding the newly created item into the loot list. This message must be used.
        /// </summary>
        private NetOutgoingMessage MakeNewGunLootItem(string name, short weaponIndex, byte itemRarity, byte clipAmount, float[] aPositions)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            sv_TotalLootCounter++;               // Increase LootCounter by 1
            msg.Write((byte)20);                 // Header              |  Byte
            msg.Write(sv_TotalLootCounter);      // LootID              |  Int
            msg.Write((byte)LootType.Weapon);    // LootType            |  Byte
            msg.Write(weaponIndex);              // Info/ Which Weapon  |  Short
            msg.Write(aPositions[0]);            // Postion X1          |  Float
            msg.Write(aPositions[1]);            // Postion Y1          |  Float
            msg.Write(aPositions[2]);            // Postion X2          |  Float
            msg.Write(aPositions[3]);            // Postion Y2          |  Float
            msg.Write(clipAmount);               // Spawn Amount        |  Byte
            msg.Write(itemRarity.ToString());    // Spawn Amount        |  Byte
            ItemList.Add(sv_TotalLootCounter, new LootItem(sv_TotalLootCounter, LootType.Weapon, WeaponType.Gun, name, itemRarity, (byte)weaponIndex, clipAmount));
            return msg;
        }

        #region Level Data Loading Method
        /// <summary>
        /// Attempts to load all level-data related files and such to fill the server's own level-data related variables.
        /// </summary>
        private void LoadSARLevel() // This will become quite the mess.
        {
            Logger.Header("[LoadSARLevel] Attempting to Load SAR LevelData!");
            if (svd_LevelLoaded) // Has this method already been called?
            {
                throw new Exception("LoadSARLevel has already been called. You cannot call this method multiple times.");
            }
            //JSONNode
            if (!File.Exists(Directory.GetCurrentDirectory() + @"\datafiles\EarlyAccessMap1.txt"))
            {
                throw new Exception("Could not locate \"EarlyAccessMap1.txt\" in the \\datafiles folder!");
            }

            JSONNode LevelJSON = JSONNode.LoadFromFile(Directory.GetCurrentDirectory() + @"\datafiles\EarlyAccessMap1.txt");
            Logger.Success("[LoadSARLevel] Successfully located LevelData file!");
            // use this if you wanna print out all the keys for whatever reason
            /*foreach (string JSONKey in LevelJSON.Keys)
            {
                Logger.testmsg(JSONKey);
            }*/

            // Item Loot Tiles
            // TOOD - update LootItem generation section because it is very old. Also probably overhaul LootItems in general because they are a mess.
            #region Loot
            int _sNormal = 0;
            int _sGood = 0;
            int _sBot = 0;
            Logger.Warn("[LoadSARLevel] Attempting to read item loot spawn tiles keys...");
            if (LevelJSON["lootSpawns"] != null && LevelJSON["lootSpawns"].Count > 0)
            {
                _sNormal = LevelJSON["lootSpawns"].Count;
            }
            if (LevelJSON["lootSpawnsGood"] != null && LevelJSON["lootSpawnsGood"].Count > 0)
            {
                _sGood = LevelJSON["lootSpawnsGood"].Count;
            }
            if (LevelJSON["lootSpawnsNoBot"] != null && LevelJSON["lootSpawnsNoBot"].Count > 0)
            {
                Logger.Header("[LoadSARLevel] The lootSpawnsNoBot key actually has entries for once. This... THIS IS BIG GUYS!!! (not really this is already handled incase anything actually happens...)");
                _sBot = LevelJSON["lootSpawnsNoBot"].Count;
            }
            else
            {
                Logger.Warn("[LoadSARLevel] Checked lootSpawnsNoBot << usual emptiness.");
            }
            Logger.Success("[LoadSARLevel] Successfully read and loaded item loot spawn tiles.");

            #region LootItemList generation
            Logger.testmsg("THIS ITEM LOOT GENERATION IS NOT YET COMPELTED- MUST USE ITEM TILES");
            Logger.Warn("Attempting to Generate ItemList");
            sv_TotalLootCounter = 0;
            MersenneTwister MerTwist = new MersenneTwister((uint)sv_LootSeed);
            ItemList = new Dictionary<int, LootItem>();
            int LootID;
            bool YesMakeBetter;
            uint MinGenValue;
            uint num;
            List<short> WeaponsToChooseByIndex = new List<short>();

            //for each weapon in the game/dataset, add each into a frequency list of all weapons by its-Frequency-amount-of-times
            // does that make sense?
            for (int i = 0; i < s_WeaponsList.Length; i++)
            {
                for (int j = 0; j < s_WeaponsList[i].SpawnFrequency; j++)
                {
                    //Logger.Basic($"My Frequency:Index -- {MyWeaponsList[i].SpawnFrequency}:{MyWeaponsList[i].JSONIndex}"); --remove but looks cool
                    WeaponsToChooseByIndex.Add(s_WeaponsList[i].JSONIndex);
                }
            }
            // Generate Loot \\
            //i < ( [Ammount of Regular Loot Spawns] + [Amount of Special Loot Spawns] + [Amount of 'no-bot' Loot Spawns] )
            int _total = _sNormal + _sGood + _sBot;
            //Logger.testmsg($"Total: {_total}\nNormal Tile Count: {_sNormal}\nGood Tile Count: {_sGood}\nBot Tile Count: {_sBot}\n");
            for (int i = 0; i < _total; i++)
            {
                //LootID++; -- > LootID++ after completing a loop. sorta.
                LootID = sv_TotalLootCounter;
                sv_TotalLootCounter++;
                MinGenValue = 0U;
                YesMakeBetter = false;

                if (i >= _sNormal) // once we reach the end of the normal tile list let's move onto the better tiles...
                {
                    YesMakeBetter = true;
                    MinGenValue = 20U;
                }
                num = MerTwist.NextUInt(MinGenValue, 100U);
                //Logger.DebugServer($"This generated number: {num}");
                if (num > 33.0)
                {
                    //Logger.Basic(" -> Greater than 33.0");

                    if (num <= 47.0) // Create Health Juice LootItem
                    {
                        //Logger.Basic(" --> Less than or equal to 47.0 | Juices");
                        byte JuiceAmount = 40;
                        // We get here in 1 loop. MinGenValue is always set each time we check for a new num. YesMakeBetter is also set
                        // up there as well. So, if we want to make another 0-100 & [NewMinimum]-100; MinGenValue is already 0, and 
                        // YesMakeBetter will already be set to true/false so we can check that and set our minimum if we need to
                        if (YesMakeBetter) MinGenValue = 15U;
                        num = MerTwist.NextUInt(MinGenValue, 100U);

                        if (num <= 55.0)
                        {
                            JuiceAmount = 10;
                        }
                        else if (num <= 89.0)
                        {
                            JuiceAmount = 20;
                        }
                        ItemList.Add(LootID, new LootItem(LootID, LootType.Juices, WeaponType.NotWeapon, $"Health Juice-{JuiceAmount}", 0, JuiceAmount));
                    }
                    else if (num <= 59.0) // LootType.Armor
                    {
                        // Logger.Basic(" --> Less than or equal to 59.0 | Armor");
                        if (YesMakeBetter) MinGenValue = 24U;
                        num = MerTwist.NextUInt(MinGenValue, 100U);
                        //Logger.Basic($" - -- > Armor Generate | Min: {MinGenValue}");
                        byte GenTier = 3;
                        if (num <= 65.0)
                        {
                            GenTier = 1;
                        }
                        else if (num <= 92)
                        {
                            GenTier = 2;
                        }
                        ItemList.Add(LootID, new LootItem(LootID, LootType.Armor, WeaponType.NotWeapon, $"Armor-Tier{GenTier}", GenTier, GenTier)); // GiveAmount for armor is how many tick it has. gotta reuse stuff
                    }
                    else
                    {
                        if (num <= 60.0) // Skip ??
                        {
                        }
                        else if (num > 60.0 && num <= 66.0) // Tape
                        {
                            ItemList.Add(LootID, new LootItem(LootID, LootType.Tape, WeaponType.NotWeapon, "Tape", 0, 1));
                        }
                        else // Weapon Generation
                        {
                            short thisInd = WeaponsToChooseByIndex[(int)MerTwist.NextUInt(0U, (uint)WeaponsToChooseByIndex.Count)];
                            Weapon GeneratedWeapon = s_WeaponsList[thisInd]; // Any change to the WeaponList will have an impact on WeaponLoot generation
                            // therefore, this is another section which'll have huge consequences if something is out of sync
                            if (GeneratedWeapon.WeaponType == WeaponType.Gun)
                            {
                                byte ItemRarity = 0;
                                if (YesMakeBetter) MinGenValue = 22U;
                                num = MerTwist.NextUInt(MinGenValue, 100U);
                                if (58.0 < num && num <= 80.0)
                                {
                                    ItemRarity = 1;
                                }
                                else if (80.0 < num && num <= 91.0)
                                {
                                    ItemRarity = 2;
                                }
                                else if (91.0 < num && num <= 97.0)
                                {
                                    ItemRarity = 3;
                                }
                                if (ItemRarity > GeneratedWeapon.RarityMaxVal) ItemRarity = GeneratedWeapon.RarityMaxVal;
                                if (ItemRarity < GeneratedWeapon.RarityMinVal) ItemRarity = GeneratedWeapon.RarityMinVal;
                                ItemList.Add(LootID, new LootItem(LootID, LootType.Weapon, WeaponType.Gun, GeneratedWeapon.Name, ItemRarity, (byte)GeneratedWeapon.JSONIndex, GeneratedWeapon.ClipSize));

                                // Spawn Ammo
                                for (int a = 0; a < 2; a++)
                                {
                                    LootID = sv_TotalLootCounter;
                                    sv_TotalLootCounter++;
                                    ItemList.Add(LootID, new LootItem(LootID, LootType.Ammo, WeaponType.NotWeapon, $"Ammo-Type{GeneratedWeapon.AmmoType}", GeneratedWeapon.AmmoType, GeneratedWeapon.AmmoSpawnAmount));
                                }
                            }
                            else if (GeneratedWeapon.WeaponType == WeaponType.Throwable)
                            {
                                ItemList.Add(LootID, new LootItem(LootID, LootType.Weapon, WeaponType.Throwable, GeneratedWeapon.Name, 0, (byte)GeneratedWeapon.JSONIndex, GeneratedWeapon.SpawnSizeOverworld));
                            }
                        }
                    }
                }
            }
            //Logger.Success($"Successfully generated the ItemList.Count:LootIDCount {ItemList.Keys.Count}:{LootID + 1}");
            //Logger.Success($"ItemList.Count:LootIDCount -- {ItemList.Keys.Count}:{sv_TotalLootCounter}");
        #endregion // end of actual loot generation section
        #endregion

            // Load Coconuts
            #region Coconuts
            Logger.Warn("[LoadSARLevel] Attempting to load coconut data and generate coconut list...");
            Logger.Warn("[LoadSARLevel] The coconut list as a whole needs a rework. Does not track coco positions...");
            if (LevelJSON["coconuts"] == null || LevelJSON["coconuts"].Count < 1)
            {
                throw new Exception("It would seem that the \"coconuts\" is null in this data set or there are no entries.");
            }
            int cocoCount = LevelJSON["coconuts"].Count;
            CoconutList = new List<int>(cocoCount);
            MersenneTwister cocoTwist = new MersenneTwister((uint)sv_CoconutSeed);
            for (int i = 0; i < cocoCount; i++) // while for Hamsterballs they HAVE to be in sequential order, here this is fine I am pretty sure
            {
                uint rng = cocoTwist.NextUInt(0U, 100U);
                if (rng > 65.0)
                {
                    CoconutList.Add(i);
                }
            }
            Logger.Success("[LoadSARLevel] Successfully generated coconut list.");
            #endregion

            // Load Vehicles
            #region Hamsterballs
            Logger.Header("[LoadSARLevel] Attempting to load hamsterballs list. (key = \"vehicles\"; not to be confused with \"emus\")");
            if (LevelJSON["vehicles"] == null || LevelJSON["vehicles"] == 0)
            {
                throw new Exception("Missing key \"vehicles\" in LevelJSON file or key \"vehicles\" has no entries.\nEither create this key or add entries. However, if this key doesn't exist you're probably doing something wrong.");
            }
            Logger.Basic("[LoadSARLevel] The \"vehicle\" key does exist so we are able to attempt to generate the hammerballs list.");
           
            JSONNode vehicleNode = LevelJSON["vehicles"];
            MersenneTwister hampterTwist = new MersenneTwister((uint)sv_VehicleSeed);
            int hampterballs = vehicleNode.Count; // so apparently it is actually
            int hTrueID = 0; // keeps track of the TO-assign HamsterballID so Hamsterballs are all sequentially ordered and stuff
            HamsterballList = new Dictionary<int, Hampterball>(hampterballs);
            Logger.Warn("[LoadSARLevel] Attempting to populate server Hamsterball list now...");
            for (int i = 0; i < hampterballs; i++)
            {
                if (hampterTwist.NextUInt(0U, 100U) > 55.0)
                {
                    HamsterballList.Add(hTrueID, new Hampterball( (byte)3, (short)hTrueID, vehicleNode[i]["x"].AsFloat, vehicleNode[i]["y"].AsFloat) );
                    hTrueID++; // once again, this just is so Hammers only exist in sequential order
                }
            }
            Logger.Success("[LoadSARLevel] Successfully generated the hamsterball list.");
            #endregion

            // Load Doodads
            #region Doodads
            Logger.Header("[LoadSARLevel] Attempting to load doodad data");
            Dictionary<int, DoodadType> doodadTypeList = DoodadType.GetAllDoodadTypes(); // this is where we get the data lol
            Logger.Success("LoadSARLevel] Successfully populated [LIST]DoodadTypes with every DoodadType");

            Logger.Warn("[LoadSARLevel] Attempting to now locate whether or not the LevelJSON has the key \"doodads\"");
            if (LevelJSON["doodads"] == null || LevelJSON["doodads"] == 0)
            {
                throw new Exception("Missing key \"doodads\" in LevelJSON file or key \"doodads\" has no entries.");
            }
            // time to read le list
            JSONNode doodadJSON = LevelJSON["doodads"];
            int doodadCount = doodadJSON.Count;
            Logger.Warn($"[LoadSARLevel] There are about {doodadCount} different doodad entries in this list. Attempting to read this now, but expect some delay due to the sheer length and calculations required.");
            svd_Doodads = new List<Doodad>();
            for (int i = 0; i < doodadCount; i++)
            {
                if (!doodadTypeList.ContainsKey(doodadJSON[i]["i"].AsInt)){
                    Logger.Failure("FRICK");
                }
                if (doodadTypeList[doodadJSON[i]["i"].AsInt].Destructible)
                {
                    svd_Doodads.Add(new Doodad(doodadTypeList[doodadJSON[i]["i"].AsInt], doodadJSON[i]["x"].AsFloat, doodadJSON[i]["y"].AsFloat));
                }
                // was just this bottom stuff...
                //svd_Doodads.Add(new Doodad(doodadTypeList[doodadJSON[i]["i"].AsInt], doodadJSON[i]["x"].AsFloat, doodadJSON[i]["y"].AsFloat));
            }
            Logger.Success("[LoadSARLevel] We were able to load the doodad section without failure! This is actually really big! Nice!");
            #endregion

            // End of Loading in Files
            GC.Collect();
            svd_LevelLoaded = true; // Pretty obvious. just a little flag saying we indeed are finished with all this.
            Logger.Success("[LoadSARLevel] Finished everything within this method without any errors. Success up to your interpretation. Good luck!");
        }
        #endregion


        #region player list methods
        /// <summary>
        /// Puts null instances in the playerlist at the bottom of the list. Does not any of them in sequential order.
        /// </summary>
        private void sortPlayersListNull()
        {
            //Logger.Warn("[PlayerList Sort-Null] Attempting to sort the PlayerList...");
            if (!isSorting)
            {
                isSorting = true;
                Player[] temp_plrlst = new Player[player_list.Length];
                int newIndex = 0;
                for (int i = 0; i < player_list.Length; i++)
                {
                    if (player_list[i] != null)
                    {
                        temp_plrlst[newIndex] = player_list[i];
                        newIndex++;
                    }
                }
                player_list = temp_plrlst;
                isSorting = false;
                isSorted = true;
                //Logger.Success("[PlayerList Sort-Null] Successfully sorted the PlayerList!");
            }
            else
            {
                Logger.Warn("Attempted to sort out nulls in playerlist while sorting already in progress.\n");
                return;
            }
        }
        private void sortPlayersListIDs()
        {
            Player[] temp_plrlst = new Player[player_list.Length]; ;
            for (int i = 0; i < player_list.Length; i++)
            {
                for (int j = i + 1; j < player_list.Length; j++)
                {
                    if (player_list[i]?.myID < player_list[j]?.myID)
                    {
                        temp_plrlst[i] = player_list[i];
                        player_list[i] = player_list[j];
                        player_list[j] = temp_plrlst[i];
                    }
                }
            }
            player_list = temp_plrlst;
        }

        private short getPlayerListLength()
        {
            // This isn't used but the older version was so stupidly overcomplicated.
            short length;
            if (!isSorted)
            {
                sortPlayersListNull();
            }
            Logger.Basic($"length of playerList array = {player_list.Length}");
            for (length = 0; length < player_list.Length; length++)
            {
                if (player_list[length] == null)
                {
                    break;
                }
            }
            Logger.Basic($"returned value: {length}");
            return length;
        }

        /// <summary>
        /// Traverses the Server's Player list in search of the provided String
        /// </summary>
        /// <returns>True if the String is found in the array; False if otherwise.</returns>
        private bool tryFindPlayerIDbyName(string searchName, out short outID)
        {
            searchName = searchName.ToLower(); // This may bring up some problems?
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].myName.ToLower() == searchName) // Have to lowercase the comparison each time. Which really sucks man :(
                {
                    outID = player_list[i].myID;
                    return true;
                }
            }
            outID = -1;
            return false;
        }
        /// <summary>
        /// Traverses the player list array in search of the index which the provided Player ID is located.
        /// </summary>
        /// <returns>True if ID is found in the array; False if otherwise.</returns>
        private bool tryFindIndexByID(int searchID, out int returnedIndex)
        {
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].myID == searchID) //searchID is int, myID is short.
                {
                    returnedIndex = player_list[i].myID;
                    return true;
                }
            }
            returnedIndex = -1;
            return false;
        }
        /// <summary>
        /// Traverses the player list in search of a Player with the provided PlayerID.
        /// </summary>
        /// <returns>True if the ID is found; False if otherwise.</returns>
        private bool tryFindPlayerByID(int searchID, out Player returnedPlayer)
        {
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].myID == searchID) //searchID is int, myID is short.
                {
                    returnedPlayer = player_list[i];
                    return true;
                }
            }
            returnedPlayer = null;
            return false;
        }

        /// <summary>
        /// Finds the ID of a player with a given name. Returns the first found instance. Returns -1 if the player cannot be found.
        /// </summary>
        /// How sad would you be if I were to tell you this is just a worse version of TryFindIDbyUsername?
        private short getPlayerIDwithUsername(string searchName)
        {
            short retID = -1;
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].myName == searchName)
                {
                    retID = player_list[i].myID;
                }
            }
            return retID;
        }
        /// <summary>
        /// Traverses the Server's Player list array in search of the Index at which the provided NetConnection occurrs.
        /// </summary>
        /// <returns>True if the NetConnection is found; False if otherwise.</returns>
        private bool tryFindIndexbyConnection(NetConnection netConnection, out int returnedIndex)
        {
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].sender == netConnection) // searchID is int, myID is short.
                {
                    returnedIndex = player_list[i].myID;
                    return true;
                }
            }
            returnedIndex = -1; // putting this above does like the same thing. just this looks... better?
            return false;
        }
        /// <summary>
        /// Traverses the Server's Player list array in search of a Player with the provided NetConnection.
        /// </summary>
        /// <returns>True if the NetConnection is found; False if otherwise.</returns>
        private bool tryFindPlayerbyConnection(NetConnection netConnection, out Player retPlayer)
        {
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].sender == netConnection) // searchID is int, myID is short.
                {
                    retPlayer = player_list[i];
                    return true;
                }
            }
            retPlayer = null;
            return false;
        }
        private bool tryFindPlayerbyConnection(NetConnection aNetConnection) // so you don't HAVE to use a returned Player object
        {
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].sender == aNetConnection)
                {
                    return true;
                }
            }
            return false;
        }
        private Player getPlayerWithConnection(NetConnection aNetConnection)
        {
            for (int i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null && player_list[i].sender == aNetConnection)
                {
                    return player_list[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Grabs the ID of a player with the provided NetConnection. If a player is unable to be located "-1" is returned.
        /// </summary>
        private short getPlayerID(NetConnection thisSender)
        {
            short id = -1;
            for (byte i = 0; i < player_list.Length; i++)
            {
                if (player_list[i] != null)
                {
                    if (player_list[i].sender == thisSender)
                    {
                        id = player_list[i].myID;
                        break;
                    }
                }
            }
            return id;
        }
        /// <summary>
        /// Returns the index at which the provided NetConnection is found in the Server's Player list. -1 otherwise. (Short)
        /// </summary>
        private short getPlayerArrayIndex(NetConnection thisSender)
        {
            short id = -1;
            for (id = 0; id < player_list.Length; id++)
            {
                if (player_list[id] != null)
                {
                    if (player_list[id].sender == thisSender)
                    {
                        //Logger.Success($"This sender ({thisSender.RemoteEndPoint}) is at array-index {id}, with an assigned ID of {player_list[id].myID}");
                        return id;
                    }
                }
            }
            Logger.Failure("NO PLAYER WAS FOUND WITH THE GIVEN SENDER ADDRESS");
            return -1;
        }
        #endregion
    }
}