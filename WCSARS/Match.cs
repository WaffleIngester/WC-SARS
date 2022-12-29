using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using Lidgren.Network;
using SimpleJSON;
using SARStuff;

namespace WCSARS // test test test
{
    class Match
    {
        // Main Stuff...
        public NetServer server;
        private Player[] _playerList;
        //public Player[] PlayerList { get => _playerList; } // Unused as of now (12/23/22)
        private List<short> _availableIDs;
        private Dictionary<NetConnection, string> _incomingConnections;
        private JSONArray _playerJSONdata;
        private TimeSpan DeltaTime;

        // Item-Like Data
        private Dictionary<int, LootItem> _itemList;
        private Dictionary<int, Coconut> _coconutList;
        private Dictionary<int, Hampterball> _hamsterballList;
        private List<Doodad> _doodadList;
        private Weapon[] _weaponsList = Weapon.GetAllWeaponsList();

        // UNSORTED
        private int prevTimeA, matchTime; // TODO -- Phase these out
        private bool _hasMatchStarted = false;
        private bool _isMatchFull = false;
        private bool isSorting, isSorted; // TODO -- Unsure whether these are necessariy
        private double _lobbyRemainingSeconds;
        //public double LobbyRemainingTime { get => _lobbyRemainingSeconds; }

        // -- Super Skunk Gas --
        private float LastSafezoneX, LastSafezoneY, LastSafezoneRadius; // Set before SSG approachment; used for transitioning CurrentSZ to the Target/End SZ
        private float CurrentSafezoneX, CurrentSafezoneY, CurrentSafezoneRadius; // Current *active* Safezone; used for SSG damage checks
        private float EndSafezoneX, EndSafezoneY, EndSafezoneRadius; // Set before SSG approachment; this what the CurrentSafezone will become

        private float SkunkGasTotalApproachDuration; // Total amount of time (in seconds) it will take to move CurrentSZ to EndSZ
        private float SkunkGasRemainingApproachTime; // Starts at the same values at TotalApproachDuration; Slowly ticks down while moving CurrentSZ to EndSZ
        private float SkunkGasWarningDuration; // Match's version of the timer indicating how long it will take for the SSG to start coming in
        private bool isSkunkGasActive, isSkunkGasWarningActive; // SGActive: is the SSG active/do damage ticks; WarningActive: for the SSG warning timer
        private DateTime _nextGasWarnTimerCheck; // Used for the warning timer.
        // -- Super Skunk Gas --

        // -- MoleCrate Crate Stuff --
        private Vector2[] _moleSpawnSpots; // Array containing every mole spawn-spot found while loading the level data in LoadSARLevel().
        private short _maxMoleCrates; // Maximum # of MoleCrates allowed in a match.
        private MoleCrate[] _moleCrates; // An array of MoleCrate objects which is the amount of active moles/crates available. The Length is that of Match._maxMoleCrates.
        // -- MoleCrate Crate Stuff --

        // Healing Values 1
        private float _healPerTick = 4.75f; // 4.75 health/drinkies every 0.5s according to the SAR wiki 7/21/22
        private float _healRateSeconds = 0.5f; // 0.5s
        //private byte _tapePerCheck; // Add when can config file
        //private float _tapeRateSeconds; // Add when can config file? (wait... isn't taping at a set speed??)

        // Dartgun-Related things
        private int _ddgMaxTicks = 12; // DDG max amount of Damage ticks someone can get stuck with
        private int _ddgAddTicks = 4; // the amount of DDG ticks to add with each DDG shot
        private float _ddgTickRateSeconds = 0.6f; // the rate at which the server will attempt to make a DDG DamageTick check
        private int _ddgDamagePerTick = 9;
        private List<Player> _poisonDamageQueue; // List of PlayerIDs who're taking skunk damage > for cough sound -- 12/2/22
        //public List<Player> PoisonDamageQueue { get => _poisonDamageQueue; } // Added / unused as of now (12/23/22)
        
        // Level / RNG-Related
        private int _totalLootCounter, _lootSeed, _coconutSeed, _vehicleSeed; // Spawnable Item Generation Seeds
        //private MersenneTwister _servRNG = new MersenneTwister((uint)DateTime.UtcNow.Ticks);
        private bool svd_LevelLoaded = false; // Likely able to remove this without any problems

        //mmmmmmmmmmmmmmmmmmmmmmmmmmmmm (unsure section right now
        private bool _canCheckWins = false;
        private bool _hasPlayerDied = true;
        private bool _safeMode = true; // This is currently only used by the /gun ""command"" so you can generate guns with abnormal rarities
        private const int MS_PER_TICK = 41; // (1000ms / 24t/s == 41)

        public Match(int port, string ip)
        {
            // Initialize LootGenSeeds
            _lootSeed = 351301;
            _coconutSeed = 5328522;
            _vehicleSeed = 9037281;
            LoadSARLevel(); // _totalLootCounter set here
            /*
            //Logger.Warn($"DateTime Ticks: {DateTime.UtcNow.Ticks}");
            _lootSeed = (int)_servRNG.NextUInt(0, (uint)DateTime.UtcNow.Ticks);
            //Logger.Warn($"LootSeed: {_lootSeed}");
            _coconutSeed = (int)_servRNG.NextUInt(0, (uint)_lootSeed * (uint)DateTime.UtcNow.Ticks);
            //Logger.Warn($"CocoSeed: {_coconutSeed}");
            _vehicleSeed = (int)_servRNG.NextUInt(0, (uint)_coconutSeed * (uint)_coconutSeed);
            //Logger.Warn($"VehicleSeed: {_vehicleSeed}"); */

            // Initialize PlayerList stuff / ID stuff
            _playerList = new Player[64]; // Default
            _availableIDs = new List<short>(_playerList.Length); // So whenever playerList is changed don't have to constantly change here...
            for (short i = 0; i < _playerList.Length; i++) _availableIDs.Add(i);
            _incomingConnections = new Dictionary<NetConnection, string>(4);

            isSorting = false;
            isSorted = true;

            // MoleCrate Crate fields...
            _maxMoleCrates = 12;
            _moleCrates = new MoleCrate[_maxMoleCrates];

            //TODO - finish settings this up at some point
            _poisonDamageQueue = new List<Player>(32);

            _lobbyRemainingSeconds = 90.00;


            // Initializing JSON stuff
            string plrJsonPath = Directory.GetCurrentDirectory() + @"\playerdata.json";
            if (File.Exists(plrJsonPath))
            {
                JSONNode PlayerDataJSON = JSON.Parse(File.ReadAllText(plrJsonPath));
                _playerJSONdata = (JSONArray)PlayerDataJSON["PlayerData"];
            }
            else
            {
                Logger.Failure($"Could not locate playerdata.json using specified path.\nSearched Path: {plrJsonPath}\n(press any key to exit)");
                Console.ReadKey();
                Environment.Exit(2);
            }

            // NetServer Initialization and starting
            Thread updateThread = new Thread(ServerUpdateLoop);
            Thread netThread = new Thread(ServerNetLoop);
            NetPeerConfiguration config = new NetPeerConfiguration("BR2D");
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);  // Reminder to not remove this
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);        // Reminder to not remove this
            config.PingInterval = 22f;
            config.LocalAddress = System.Net.IPAddress.Parse(ip);
            config.Port = port;
            server = new NetServer(config);
            server.Start();
            updateThread.Start();
            netThread.Start();
            //Logger.DebugServer("[MATCH.ctor] I have reached the end of my create routine.");
        }

        /// <summary>
        /// Handles all NetIncomingMessages sent to this Match's server. Continuously runs until this Match.server is no longer running.
        /// </summary>
        private void ServerNetLoop()
        {
            // Make sure no invalid....
            if (server == null) throw new Exception("Attempted to start ServerNetLoop when Match.server was null!");
            if (server.Status != NetPeerStatus.Running) throw new Exception("Attempted ServerNetLoop() while Match.server was not running.");
            Logger.DebugServer("[NetworkLoopThread] [DEBUG] NetworkLoop has started");

            // Loop to handle any recieved message from the NetServer... Stops when the server is no longer in the running state.
            NetIncomingMessage msg;
            while (server.Status == NetPeerStatus.Running)
            {
                //Logger.DebugServer($"[{DateTime.UtcNow}] Waiting to receive message.");
                server.MessageReceivedEvent.WaitOne(); // Halt this thread until the NetServer receives a message. Then continue
                //Logger.DebugServer($"[{DateTime.UtcNow}] Message has been received.");
                while ((msg = server.ReadMessage()) != null)
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
                                    Logger.Warn($"[Satus Change - Disconnected] Client says: {msg.ReadString()}");
                                    if (TryIndexFromConnection(msg.SenderConnection, out int index))
                                    {
                                        NetOutgoingMessage disc = server.CreateMessage();
                                        disc.Write((byte)46); // Header -- Message Type 46
                                        disc.Write(_playerList[index].ID); // Short -- Disconnecting pID
                                        disc.Write(false); // Absolutely no clue what this does. Earlier notes said something about "IsVanish / IsGhost"
                                        server.SendToAll(disc, NetDeliveryMethod.ReliableSequenced);

                                        // TOOD -- refer to below comments
                                        if (!_hasMatchStarted) _availableIDs.Insert(0, _playerList[index].ID); // Stop rewriting the above section. 
                                        if (!_hasMatchStarted && _isMatchFull && (_playerList[_playerList.Length - 1] != null)) _isMatchFull = false;
                                        _playerList[index] = null; // Find a better way to give pIDs
                                        isSorted = false;
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
                                if (_isMatchFull)
                                {
                                    Logger.Basic("[Connection Approval] An incoming connection attempted to join, but the match is currently full...");
                                    msg.SenderConnection.Deny("The match you are trying to join is currently full. Sorry about that :[");
                                    break;
                                }
                                if (!_hasMatchStarted && _lobbyRemainingSeconds > 5.0)
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
                                Logger.Basic($"Received PingFloat: {pingTime}");
                                Logger.Basic($"Received PingFloatCorrection: {pingTime * 1000}");
                                Logger.Basic($"Sender RemoteTimeOffset: {msg.SenderConnection.RemoteTimeOffset}");
                                Logger.Basic($"Sender AverageRoundTrip: {msg.SenderConnection.AverageRoundtripTime}");
                                if (TryPlayerFromConnection(msg.SenderConnection, out Player pinger))
                                {
                                    pinger.LastPingTime = pingTime;
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Failure($"Error while attempting to read ConnectionLatencyUpdate ping. Is okii!\n{ex}");
                            }
                            break;
                        default:
                            Logger.Failure("Unhandled type: " + msg.MessageType);
                            break;
                    }
                    server.Recycle(msg);
                }
            }
            // Once the NetServer is no longer running we're basically done... So can just do this and everything is over.
            Logger.DebugServer($"[{DateTime.UtcNow}] [ServerNetLoop] Match.server is no longer running. I shutdown as well... Byebye!");
        }

        /// <summary>
        /// Handles this Match's update loop. Runs until this Match.server is no longer in the "running" state.
        /// </summary>
        #region server update thread
        private void ServerUpdateLoop() // TODO -- lot of stuff... cleanup- make run loop better
        {
            // -- List of Absurd Things You Can Do in the Lobby --
            // This list only contains known stuff. Because might've forgotten the others... But still wanted to mention these
            // - Super Skunk Gas -- The message of where everything goes will be received and will silently count down. Instead of the usual...
            // ...enclosement of the gas, the "old" safezone will stay as it was and SSG will never appraoch the "new" safezone.
            // - A Molecrate spawn message can be sent, and the mole will actually appear and everything. ...
            // ... The only thing that doesn't work with this though, is opening the crate. Even when the match starts, you cannot interact with it.
            // - You can change a Player's health, tapes, drink amount; and proably armor as well in lobby.

            // TODO - cleanese.
            Logger.Success("Server update thread started.");
            if (!_canCheckWins)
            {
                Logger.Warn("\n[ServerUpdateThread] [WARNING] Match will NOT check for wins. To re-enable this please type \"/togglewin\" while in-game.");
            }
            DateTime nextTick = DateTime.UtcNow;

            DateTime lastDateTime = DateTime.UtcNow; // Used for DeltaTime calculation

            //lobby
            int lb_pTime = DateTime.UtcNow.Second; // lobby_previousTime
            while (!_hasMatchStarted && (server.Status == NetPeerStatus.Running))
            {
                while (nextTick < DateTime.UtcNow)
                {
                    // Calculate DeltaTime
                    DeltaTime = DateTime.UtcNow - lastDateTime;
                    lastDateTime = DateTime.UtcNow;

                    // Check PlayerList to see whether it's full
                    if (!isSorted) SortPlayerEntries();
                    if (_playerList[_playerList.Length - 1] != null && !_isMatchFull)
                    {
                        _isMatchFull = true;
                        Logger.Basic("[Lobby Update] The Match appears to now be full.");
                    }
                    SendDummy(); // ping

                    // Check the Lobby Countdown Timer
                    #region Check_Lobby_Time
                    if (_playerList[0] != null && (lb_pTime != DateTime.UtcNow.Second)) // Add "&& !_hasMatchStarted" if break below startGame() is removed
                    {
                        lb_pTime = DateTime.UtcNow.Second;
                        _lobbyRemainingSeconds -= 1.0d;
                        svu_LobbyCurrentCountdown();
                        Logger.Basic($"Lobby Time After: {_lobbyRemainingSeconds}");
                        if (_lobbyRemainingSeconds <= 0.0d)
                        {
                            SendMatchStart();
                            _hasMatchStarted = true;
                            break; // Break the while loop and start doing stuff for an in-progress match. Skip everything below early
                        }
                    }
                    #endregion Check_Lobby_Time
                    // Send Lobby-PlayerPosition update message to all players
                    #region Lobby_UpdatePositions
                    // Make message sending player data. Loops entire list but only sends non-null entries.
                    NetOutgoingMessage msg = server.CreateMessage();
                    msg.Write((byte)11);                        // Byte | Header
                    msg.Write((byte)GetValidPlayerCount());     // Byte | Count of valid entries the Client is receiving/iterating over
                    for (int i = 0; i < _playerList.Length; i++)
                    {
                        if (_playerList[i] != null)
                        {
                            msg.Write(_playerList[i].ID);
                            msg.Write((sbyte)((180f * _playerList[i].MouseAngle / 3.141592f) / 2));
                            msg.Write((ushort)(_playerList[i].PositionX * 6f));
                            msg.Write((ushort)(_playerList[i].PositionY * 6f));
                        }
                    }
                    server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
                    #endregion Lobby_UpdatePositions

                    // Not much else to do here. Probably deal with gallery targets or whatever
                    CheckMoleCrates(); // Only here because /commands
                    svu_PlayerDataChanges(); // Only here because /commands
                    nextTick = nextTick.AddMilliseconds(MS_PER_TICK);
                    if (nextTick > DateTime.UtcNow)
                    {
                        Thread.Sleep(nextTick - DateTime.UtcNow);
                    }
                }
            }

            // The code in this section only gets executed between Lobby-Time ending and Match-Time start
            #region reset for match start

            // Reset Safezone variables to their defaults
            Logger.Warn("Resetting Safezone variables to their defaults");
            SkunkGasTotalApproachDuration = 5.0f;
            SkunkGasRemainingApproachTime = 5.0f;
            SkunkGasWarningDuration = 5.0f;
            isSkunkGasActive = false;
            isSkunkGasWarningActive = false;
            Logger.Basic("Reset safezone variables without encountering any errors.");

            // Go through the PlayerList and reset AttackCount and ProjectileList to their default values || [[ThrowableCounter reset is commented-out]]
            Logger.Warn("Resetting all Playes' Attack AND ProjectileList fields...");
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null)
                {
                    _playerList[i].AttackCount = -1; // Reset value and stuff. AttackCounts and stuff is super complicated
                    _playerList[i].ProjectileList = new Dictionary<short, Projectile>();
                    //_playerList[i].ThrowableCounter = -1; // Don't think you can have throwables during lobby, so this is kind of useless right now
                }
            }
            Logger.Basic("Reset ALL Players' AttackCount / ProjectileList without encountering any errors.");
            #endregion reset vars for match

            // A Match that is in progress
            while (_hasMatchStarted && (server.Status == NetPeerStatus.Running))
            {
                while (nextTick < DateTime.UtcNow)
                {
                    DeltaTime = DateTime.UtcNow - lastDateTime;
                    lastDateTime = DateTime.UtcNow;
                    if (!isSorted) { SortPlayerEntries(); }

                    SendDummy();

                    //check for win
                    if (_hasPlayerDied && _canCheckWins) { svu_checkForWinnerWinnerChickenDinner(); }
                    check_DDGTicks(); // STILL TESTING -- (as of: 12/2/22)
                    svu_CheckCoughs(); // NEW TEST FROM 12/2/22 UPDATE
                    //updating player info to all people in the match
                    svu_MatchPositionUpdate();
                    svu_PlayerDataChanges();

                    updateServerDrinkCheck();
                    updateServerTapeCheck();
                    svu_EmoteCheck();
                    svu_AnnouncePingTimes();

                    advanceTimeAndEventCheck();

                    // This appears to work as indended
                    #region NEW_Check_Gas_Timer
                    if (isSkunkGasWarningActive && (_nextGasWarnTimerCheck < DateTime.UtcNow))
                    {
                        SkunkGasWarningDuration -= 1.0f;
                        if (SkunkGasWarningDuration <= 0)
                        {
                            isSkunkGasWarningActive = false;
                            isSkunkGasActive = true;
                            NetOutgoingMessage msg = server.CreateMessage();
                            msg.Write((byte)34);                // Byte  | Header / MessageID
                            msg.Write(SkunkGasTotalApproachDuration);   // Float | Duration of the advancement event (could also be seen as the speed)
                            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
                            continue;
                        }
                        _nextGasWarnTimerCheck = DateTime.UtcNow.AddSeconds(1.0d);
                    }
                    #endregion NEW_Check_Gas_Timer


                    if (isSkunkGasActive)
                    {
                        svu_SkunkGasRadius();
                        //checkSkunkGas(); // Gas check is currently disabled
                    }

                    CheckMoleCrates();

                    nextTick = nextTick.AddMilliseconds(MS_PER_TICK);
                    if (nextTick > DateTime.UtcNow)
                    {
                        Thread.Sleep(nextTick - DateTime.UtcNow);
                    }
                }
            }
            Logger.DebugServer($"[{DateTime.UtcNow}] [ServerUpdateLoop] Match.server no longer running? I stop too... Byebye!");
        }

        // Currently Unused. At a later date there may be a reason to use this separate function
        private void svu_LobbyPositionUpdate() // 11 -- Copied over from svu_MatchPosUpdate() and modified to fit this message's format
        {
            // Make message sending player data. Loops entire list but only sends non-null entries.
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)11);       // Byte | Header
            msg.Write((byte)GetValidPlayerCount());   // Byte | Count of valid entries the Client is receiving/iterating over
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null)
                {
                    msg.Write(_playerList[i].ID);
                    msg.Write((sbyte)((180f * _playerList[i].MouseAngle / 3.141592f) / 2));
                    msg.Write((ushort)(_playerList[i].PositionX * 6f));
                    msg.Write((ushort)(_playerList[i].PositionY * 6f));
                }
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }
        private void svu_MatchPositionUpdate() // 12 -- This is only used by the server once a match has began and is in progress.
        {
            // Make message sending player data. Loops entire list but only sends non-null entries.
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)12);       // Byte | Header
            msg.Write((byte)GetValidPlayerCount());   // Byte | Count of valid entries the Client is receiving/iterating over
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null)
                {
                    msg.Write(_playerList[i].ID);
                    msg.Write((short)(180f * _playerList[i].MouseAngle / 3.141592f));
                    msg.Write((ushort)(_playerList[i].PositionX * 6f));
                    msg.Write((ushort)(_playerList[i].PositionY * 6f));
                    if (_playerList[i].WalkMode == 4) // Determine whether or not the player is in a vehicle
                    {
                        msg.Write(true);
                        //msg.Write((short)(_playerList[i].PositionX * 10f)); // Add these lines back for some funny stuff
                        //msg.Write((short)(_playerList[i].PositionY * 10f)); // Correct usage is to use VehiclePositions NOT PlayerPositions
                        msg.Write((short)(_playerList[i].VehicleVelocityX * 10f));
                        msg.Write((short)(_playerList[i].VehicleVelocityY * 10f));
                    }
                    else
                    {
                        msg.Write(false);
                    }
                }
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }
        private void svu_LobbyCurrentCountdown() // 43 -- Informs connected NetPeers of current Lobby countdown time
        {
            NetOutgoingMessage sTimeMsg = server.CreateMessage();
            sTimeMsg.Write((byte)43);               // Byte   | MessageID
            sTimeMsg.Write(_lobbyRemainingSeconds);     // Double | Time Remaining
            server.SendToAll(sTimeMsg, NetDeliveryMethod.ReliableOrdered);
        }
        private void svu_PlayerDataChanges()   // 45 -- Likely only intended to be used when a Player's state is changed
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)45);
            msg.Write((byte)GetValidPlayerCount()); // This function gets the amount of non-null entries. Should be the same as the count below
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null)
                {
                    msg.Write(_playerList[i].ID);
                    msg.Write(_playerList[i].HP);
                    msg.Write(_playerList[i].ArmorTier);
                    msg.Write(_playerList[i].ArmorTapes);
                    msg.Write(_playerList[i].WalkMode);
                    msg.Write(_playerList[i].Drinkies);
                    msg.Write(_playerList[i].Tapies);
                }
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }
        private void SendDummy()
        {
            NetOutgoingMessage dummy = server.CreateMessage();
            dummy.Write((byte)97);
            server.SendToAll(dummy, NetDeliveryMethod.ReliableOrdered);
        }

        private void svu_AnnouncePingTimes() // 112 -- Tells all NetPeers everyone's Ping.
        {
            if (_playerList.Length <= 0) return;
            NetOutgoingMessage pings = server.CreateMessage(); // GS_SendPlayerPingList
            pings.Write((byte)112);
            pings.Write((byte)GetValidPlayerCount());
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null)
                {
                    pings.Write(_playerList[i].ID);
                    pings.Write((short)(_playerList[i].LastPingTime * 1000f));
                }
            }
            server.SendToAll(pings, NetDeliveryMethod.ReliableOrdered);
        }

        private void updateServerDrinkCheck()
        {
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null && _playerList[i].isDrinking && (_playerList[i].NextHealTime < DateTime.UtcNow))
                {
                    if ((_playerList[i].Drinkies > 0) && (_playerList[i].HP < 100)) // in the event we somehow manage to get in this situation
                    {
                        float _heal = _healPerTick;
                        if ((_heal + _playerList[i].HP) > 100) // find Desired HP to Add
                        {
                            _heal = 100 - _playerList[i].HP; // remainder (how much to get to 100)
                        }
                        if ((_playerList[i].Drinkies - _heal) < 0) // check if we can even take our DesiredHP from drinkies!
                        {
                            _heal = _playerList[i].Drinkies; // if we don't have enough drinkies, then that means we can only take what we got!
                        }
                        _playerList[i].HP += (byte)_heal;
                        _playerList[i].Drinkies -= (byte)_heal;
                        _playerList[i].NextHealTime = DateTime.UtcNow.AddSeconds(_healRateSeconds); // default = 0.5s
                        if ((_playerList[i].HP == 100) || (_playerList[i].Drinkies == 0)) // so we don't have to wait an extra tick-loop to check
                        {
                            ServerAMSG_EndedDrinking(_playerList[i].ID);
                            _playerList[i].isDrinking = false;
                        }
                    }
                    else
                    {
                        ServerAMSG_EndedDrinking(_playerList[i].ID);
                        _playerList[i].isDrinking = false;
                    }
                }
            }
        }
        private void updateServerTapeCheck() // blatant copy of ServerDrinkCheck || maybe TODO:: make amount of tape per check dynamic
        {
            for (int i = 0; i < _playerList.Length; i++)
            {
                if ((_playerList[i] != null) && _playerList[i].isTaping && (_playerList[i].NextTapeTime < DateTime.UtcNow))
                {
                    if ((_playerList[i].ArmorTier > 0) && (_playerList[i].Tapies > 0) && (_playerList[i].ArmorTapes < _playerList[i].ArmorTier)) // stuff could happen!
                    {
                        _playerList[i].Tapies -= 1;
                        _playerList[i].ArmorTapes += 1;
                        _playerList[i].isTaping = false;
                        ServerAMSG_EndedTaping(_playerList[i].ID);
                    }
                    else
                    {
                        ServerAMSG_EndedTaping(_playerList[i].ID);
                        _playerList[i].isTaping = false;
                    }
                }
            }
        }
        private void svu_EmoteCheck()
        {
            for (int i = 0; i < _playerList.Length; i++)
            {
                if ((_playerList[i] != null) && _playerList[i].isEmoting && (_playerList[i].EmoteEndTime < DateTime.UtcNow))
                {
                    ServerEndEmoter(_playerList[i].ID);
                    _playerList[i].isEmoting = false;
                    _playerList[i].EmoteID = -1;
                    _playerList[i].EmoteSpotX = -1;
                    _playerList[i].EmoteSpotY = -1;
                }
            }
        }

        private void svu_checkForWinnerWinnerChickenDinner()
        {
            _hasPlayerDied = false;
            List<short> aIDs = new List<short>(_playerList.Length);
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null && _playerList[i].isAlive)
                {
                    aIDs.Add(_playerList[i].ID);
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
        private void svu_CheckCoughs() // Added 12/2/22 // TODO: verify it works correctly
        {
            if (_poisonDamageQueue.Count == 0) return;
            int listCount = _poisonDamageQueue.Count;
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)107);
            msg.Write((byte)listCount);
            for (int i = 0; i < listCount; i++)
            {
                msg.Write(_poisonDamageQueue[i].ID);
            }
            msg.Write((byte)listCount);
            for (int i = 0; i < listCount; i++)
            {
                msg.Write(_poisonDamageQueue[i].LastAttackerID);
            }
            msg.Write((byte)listCount);
            for (int i = 0; i < listCount; i++)
            {
                msg.Write(_poisonDamageQueue[i].ID);
            }
            msg.Write((byte)listCount);
            for (int i = 0; i < listCount; i++)
            {
                msg.Write(_poisonDamageQueue[i].LastAttackerID);
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
            _poisonDamageQueue.Clear();
        }

        // So... Notes I guess?
        // After the Giant Eagle is gone (think that takes ~60 seconds?) the first "Gas Approach Event" takes place.
        // NEED CHECKING: Think warning message lasts another 60 seconds, and then approachment is 72 seconds long.
        // NEED CHECKING: Believe after that is the first Molecrate Event. Unsure how long it lasts for.
        // Everything else I'm not certain on. What I am certain on is there needs to be a better event system because oh my lordy is this dumb!
        // Oh, and one more thing. Most numbers that are here for the time being are probably going to be based off of modern version of the game.
        // Even if someone finds footage of the old version to base the timings on; the actual values are completely unknown.
        // For the time being it's probs just complete guessing. Even if you somehow got numbers from playing the game now, some of them are different...
        // ...more than likely. At least at some point in 2021 final circle was changed to be longer. So yeah!
        
        //can be simplified with gasCheck
        private void advanceTimeAndEventCheck()
        {
            //literally just a copy and paste
            if (prevTimeA != DateTime.UtcNow.Second)
            {
                matchTime += 1;
                prevTimeA = DateTime.UtcNow.Second;

                switch (matchTime)
                {
                    case 60:
                        CreateSafezone(620, 720, 620, 720, 6000, 3000, 60, 72); // TODO -- Still need good first safezone
                        break;
                    case 212:

                        break;
                }
            }
        }

        private void CheckMoleCrates()
        {
            // Make sure there are actually any current moles waiting to do anything
            if (_moleCrates == null || _moleCrates[0] == null) return;
            // There are Moles in the game right now; let's figure out what's going on.
            MoleCrate mole;
            for (int i = 0; i < _moleCrates.Length; i++)
            {
                // Check if entry is null; or if this MoleCrate is already at its endpoint (isCrateReal only gets set to true if it is).
                mole = _moleCrates[i];
                if (mole == null || mole.isCrateReal) continue;

                // Check if this mole has finished its countdown or not...
                if (mole.IdleTime > 0f)
                {
                    mole.IdleTime -= (float)DeltaTime.TotalSeconds;
                    continue;
                }
                // If it has finished its countdown, we get here now.
                Vector2 thisEndPoint = mole.MovePositions[mole.MoveIndex];
                if (mole.Position == thisEndPoint)
                {
                    // Think this works fine... Either way, we're at the end point. Increment the MoveIndex and see whether we've reached the end.
                    mole.MoveIndex++;
                    if (mole.MoveIndex >= mole.MovePositions.Length)
                    {
                        NetOutgoingMessage msg = server.CreateMessage(14);
                        msg.Write((byte)69);    // Byte  | MessageID -- 69
                        msg.Write((short)i);    // Short | MoleCrateID (this should correspond to this index in the mole array. If not idk something's wrong)
                        msg.Write(mole.Position.x); // Float | LandingSpotX
                        msg.Write(mole.Position.y); // Float | LandingSpotY
                        server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
                        mole.isCrateReal = true;
                        continue;
                    }
                }
                // If the above check fails, then that must mean we have to move towards this position.
                float deltaMove = (float)DeltaTime.TotalSeconds * 25f; // This may be too high. Not completely sure yet
                mole.Position = Vector2.MoveToVector(mole.Position, thisEndPoint, deltaMove);
            }
        }

        /// <summary>
        /// Sends a NetMessage to all connected Clients which tells them the gas is advancing to the specified Safezones. Also sets the necessary Safezone variables
        /// </summary>
        private void CreateSafezone(float oldX, float oldY, float oldRadius, float newX, float newY, float newRadius, float warnTime, float aprTime)
        {
            // Create and send a NetOutGoingMessage
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)33);    // Byte  | Header / MessageID
            msg.Write(oldX);        // Float | Old Safezone Center X
            msg.Write(oldY);        // Float | Old Safezone Center Y
            msg.Write(newX);        // Float | New Safezone X
            msg.Write(newY);        // Float | New Safezone Y
            msg.Write(oldRadius);   // Float | Old Safezone Radius
            msg.Write(newRadius);   // Float | New Safezone Radius
            msg.Write(warnTime);    // Float | Time (in seconds) until the Super Skunk Gas starts advancing
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);

            // Set the Match's Safezone and SkunkGas related variables.
            LastSafezoneRadius = oldRadius;
            LastSafezoneX = oldX;
            LastSafezoneY = oldY;
            EndSafezoneX = newX;
            EndSafezoneY = newY;
            EndSafezoneRadius = newRadius;
            SkunkGasTotalApproachDuration = aprTime; // approach time
            SkunkGasRemainingApproachTime = aprTime;
            // For the Gas Warning message
            SkunkGasWarningDuration = warnTime;
            _nextGasWarnTimerCheck = DateTime.UtcNow.AddMilliseconds(1000);
            isSkunkGasWarningActive = true;
            // Fair warning! SkunkGasActive variable is not set to true here. That must be done elsewhere. One way it is always set is the gas warning message expiring.
        }

        /// <summary>
        /// Sends a "StartMatch" message to all NetPeers. The message will use the default values.
        /// </summary>
        private void SendMatchStart()
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)6);
            msg.Write((byte)1);
            msg.Write((short)14);
            msg.Write((short)(3 * 100)); // Desert Wind% -- 45% = DEATH
            msg.Write((byte)1);
            msg.Write((short)14);
            msg.Write((short)(5 * 100)); // Taundra Wind% -- Supposedly...
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        /// <summary>
        /// Sends a "StartMatch" message to all connected NetPeers. The message will contain the provided WeatherEvent time/intensity arrays.
        /// </summary>
        private void SendMatchStart(float[] desWindTimes, float[] desWindIntensity, float[] snowTimes, float[] snowIntensity)
        {
            if (desWindTimes.Length != desWindIntensity.Length)
            {
                Logger.Failure($"There is a mis-match in the desWindTimes / desWindIntensities array. dWT:dWI: {desWindTimes.Length}:{desWindIntensity.Length}.\nPlease correct this and try again.");
                return;
            }
            if (snowTimes.Length != snowIntensity.Length)
            {
                Logger.Failure($"There is a mis-match in the snowTimes / snowIntensities array. sT:sI: {snowTimes.Length}:{snowIntensity.Length}.\nPlease correct this and try again.");
                return;
            }

            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)6);
            msg.Write((byte)desWindTimes.Length);
            for (int i = 0; i < desWindTimes.Length; i++)
            {
                msg.Write((short)desWindTimes[i]);
                msg.Write((short)desWindIntensity[i]);
            }
            msg.Write((byte)snowTimes.Length); 
            for (int i = 0; i < snowTimes.Length; i++)
            {
                msg.Write((short)snowTimes[i]);
                msg.Write((short)snowIntensity[i]);
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered); // probbably could be unordered.
        }

        private void checkSkunkGas() // TODO -- check if the Current Safezone is a valid Safezone (you know, not absurdly small or something)
        {
            if (!isSkunkGasActive) return;
            //Logger.DebugServer($"SZ.X: {sv_CurrentSafezoneX}\nSZ.Y: {sv_CurrentSafezoneY}\nSZ.Rad: {sv_CurrentSafezoneRadius}\n");
            //bool inSafezone;
            bool inGas;
            float playerX, playerY, dX, dY;
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] == null) continue;
                playerX = _playerList[i].PositionX;
                playerY = _playerList[i].PositionY;
                float r2 = CurrentSafezoneRadius * CurrentSafezoneRadius;
                dX = playerX - CurrentSafezoneX; // It should be fine to use CurrentSafezone stuff if CurrentSafezone is consistently updated
                dY = playerY - CurrentSafezoneY;
                //inGas = (dX * dX + dY * dY) >= r2;
                inGas = Math.Sqrt(dX * dX + dY * dY) >= CurrentSafezoneRadius; // The only thing that should need to be changed around and stuff
                if (inGas && _playerList[i].isAlive)
                {
                    //_playerList[i].HP -= 1;
                    test_damagePlayer(_playerList[i], 1, -2, -1);
                    //Logger.testmsg("PLAYER IS NOT IN THE SAFEZONE");
                }

            }
        }
        // Resizes the current Safezone radius to match the target "END" safezone radius
        private void svu_SkunkGasRadius() // TODO -- Realizing now that the TargetSafezone.CenterX / CenterY likely needs to get translated as well
        {
            if (!isSkunkGasActive) // Insurance
            {
                Logger.Warn("Attempted to call svu_SkunkGasRadius while sv_SkunkGasActive was FALSE."); return;
            }
            if (SkunkGasRemainingApproachTime > 0.0f)
            {
                // DeltaTime Testing

                // Log --
                    // Timings
                //Logger.Warn($"Total Duration: {sv_GasAdvanceDuration}"
                       // + $"\nTime Remainder: {sv_GasAdvanceDurationRemainder}"
                       // + $"\nCurrent DeltaTime: {DeltaTime.TotalSeconds}s");

                    // VARS
                //Logger.Warn($"R.Current: {sv_CurrentSafezoneRadius}"
                       // + $"\nR.Target: {sv_EndSafezoneRadius}");

                // Log --- ^^

                SkunkGasRemainingApproachTime -= (float)DeltaTime.TotalSeconds;
                if (SkunkGasRemainingApproachTime < 0.0f) SkunkGasRemainingApproachTime = 0.0f;
                //Logger.Warn($"TimeRemaining DELTA'D: {sv_GasAdvanceDurationRemainder}s");

                //Logger.Warn($"Old Radius: {sv_CurrentSafezoneRadius}\nDesired Radius: {sv_EndSafezoneRadius}");

                float scew = (SkunkGasTotalApproachDuration - SkunkGasRemainingApproachTime) / SkunkGasTotalApproachDuration;
                CurrentSafezoneRadius = (LastSafezoneRadius * (1.0f - scew)) + (EndSafezoneRadius * scew);
                CurrentSafezoneX = (LastSafezoneX * (1.0f - scew)) + (EndSafezoneX * scew);
                CurrentSafezoneY = (LastSafezoneY * (1.0f - scew)) + (EndSafezoneY * scew);

                // Log --
                //Logger.Warn($"SCEW: {scew}");
                //Logger.Warn($"R.Current.NOW: {sv_CurrentSafezoneRadius}"
                //    + $"\nR.Target: {sv_EndSafezoneRadius}");
            }
        }
        private void check_DDGTicks() // Still messing with this from time to time...
        {
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null && _playerList[i].DartTicks > 0)
                {
                    if (_playerList[i].DartNextTime <= DateTime.UtcNow)
                    {
                        if ((_playerList[i].DartTicks - 1) >= 0)
                        {
                            _playerList[i].DartTicks -= 1;
                        }
                        _playerList[i].DartNextTime = DateTime.UtcNow.AddMilliseconds(_ddgTickRateSeconds * 1000);
                        test_damagePlayer(_playerList[i], _ddgDamagePerTick, _playerList[i].LastAttackerID, _playerList[i].LastWeaponID);
                        if (!_poisonDamageQueue.Contains(_playerList[i]))
                        {
                            _poisonDamageQueue.Add(_playerList[i]);
                        }
                    }
                }
            }
        }
        #endregion

        // "right now there really is no reason for a handle message thingy to exist. maybe when things can run asynchronous"
        // Right now, HandleMessage just helps the main network section not look like a complete mess.
        // It also allows for some custom netmessage handles to still use this in their data section if they'd like.
        // It would be cool if this could run async, but not quite sure how that could be accomplished right now.
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
                    if (TryPlayerFromConnection(msg.SenderConnection, out Player ejector))
                    {
                        SendForcePosition(ejector.ID, ejector.PositionX, ejector.PositionY, true);

                        Logger.Warn($"Player ID {ejector.ID} ({ejector.Name}) has ejected!\nX, Y: ({ejector.PositionX}, {ejector.PositionY})");
                    }
                    else
                    {
                        Logger.Failure($"[Server HandleClient Land-Request] Unable to locate NetConnection \"{msg.SenderConnection}\" in PlayerList. Disconnecting client.");
                        msg.SenderConnection.Disconnect("There was an error while processing your request, so your connection was dropped. Sorry for the inconvenience.");
                    }
                    break;

                case 14: // TOOD -- Make sure Client no cheating and zooming accross the map!
                    try
                    {
                        if (TryPlayerFromConnection(msg.SenderConnection, out Player player))
                        {
                            if (!player.isAlive) Logger.Warn("yes this is fired");
                            float mouseAngle = msg.ReadInt16() / 57.295776f;
                            float rX = msg.ReadFloat();
                            float rY = msg.ReadFloat();
                            byte walkMode = msg.ReadByte();

                            player.PositionX = rX;
                            player.PositionY = rY;
                            player.MouseAngle = mouseAngle;
                            player.WalkMode = walkMode;
                            // add more checks or something ig not really but sure
                            //Logger.DebugServer($"WalkMode: {walkMode}");
                            if (walkMode == 2)
                            {
                                CheckMovementConflicts(player);
                            }
                            if (walkMode == 4 && player.VehicleID != -1) // is in hampterball
                            {
                                float vX = (float)(msg.ReadInt16() / 10f);
                                float vY = (float)(msg.ReadInt16() / 10f);
                                player.VehicleVelocityX = vX;
                                player.VehicleVelocityY = vY;
                            }
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
                case 18: // HandleAttackConfirm could use some more work!
                    HandleAttackConfirm(msg); // Will now exit early if match is in lobby or anything invalid happens!
                    break;
                case 21:
                    if (_hasMatchStarted)
                    {
                        ServerHandleMatchLootRequest(msg);
                    } else
                    {
                        ServerHandleLobbyLootRequest(msg);
                    }
                    break;
                case 25:
                    serverHandleChatMessage(msg);
                    break;
                case 27: // Client Slot Update
                    try
                    {
                        if (TryPlayerFromConnection(msg.SenderConnection, out Player player))
                        {
                            if (!player.isAlive) break; // Prevent doing anything if Player is now dead
                            byte newSlot = msg.ReadByte();
                            if (newSlot < 0 || newSlot > 4)
                            {
                                throw new Exception("Player sent an invalid slot.");
                            }
                            player.ActiveSlot = newSlot;
                            NetOutgoingMessage slotupdate = server.CreateMessage();
                            slotupdate.Write((byte)28);
                            slotupdate.Write(player.ID);
                            slotupdate.Write(newSlot);
                            server.SendToAll(slotupdate, NetDeliveryMethod.ReliableOrdered);

                        }
                    } catch (Exception except)
                    {
                        Logger.Failure($"[ServerHandle Client Slot-Update-Request] ERROR\n{except}");
                    }
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
                case 36: // Client Send Start Throwing<< TODO -- cleanup
                    try
                    {
                        if (TryPlayerFromConnection(msg.SenderConnection, out Player player))
                        {
                            short grenadeID = msg.ReadInt16();
                            Weapon lol = _weaponsList[grenadeID];
                            if (lol.Name == "GrenadeBanana" || lol.Name == "GrenadeSkunk" || lol.Name == "GrenadeFrag"){
                                NetOutgoingMessage throwStart = server.CreateMessage();
                                throwStart.Write((byte)37);
                                throwStart.Write(player.ID);
                                throwStart.Write(grenadeID);
                                server.SendToAll(throwStart, NetDeliveryMethod.ReliableOrdered);
                            }
                        }
                        else
                        {
                            Logger.Failure($"[Client.Throwable Initiate Request] Could not find the player initiating this request. Removing them.");
                            msg.SenderConnection.Disconnect("There was an error while processing your request so you have been disconnected. Sorry for the inconvenience.");
                        }
                    }
                    catch (Exception except)
                    {
                        Logger.Failure($"[Client.Throwable Initiate Request] ERROR\n{except}");
                    }
                    break;
                case 38: // Client Send Grenade Throwing << TODO -- cleanup
                    try // little warning: grenades dupe because grenade count and stuff never really dealt with lol
                    {
                        if (TryPlayerFromConnection(msg.SenderConnection, out Player player))
                        {
                            CheckMovementConflicts(player);
                            float spX = msg.ReadFloat();
                            float spY = msg.ReadFloat();
                            float spGX = msg.ReadFloat();
                            float spGY = msg.ReadFloat();
                            float tpX = msg.ReadFloat();
                            float tpY = msg.ReadFloat();
                            short grenadeID = msg.ReadInt16();
                            Weapon throwable = _weaponsList[grenadeID];
                            if (throwable.Name == "GrenadeBanana" || throwable.Name == "GrenadeSkunk" || throwable.Name == "GrenadeFrag"){
                                player.ThrowableCounter++; // it starts from -1
                                NetOutgoingMessage throwmsg = server.CreateMessage();
                                throwmsg.Write((byte)39);
                                throwmsg.Write(player.ID);
                                throwmsg.Write(spX);
                                throwmsg.Write(spY);
                                throwmsg.Write(spGX);
                                throwmsg.Write(spGY);
                                throwmsg.Write(tpX);
                                throwmsg.Write(tpY);
                                throwmsg.Write(grenadeID);
                                throwmsg.Write(player.ThrowableCounter);
                                server.SendToAll(throwmsg, NetDeliveryMethod.ReliableOrdered);
                            }
                        }
                        else
                        {
                            Logger.Failure($"[Client.Throwable Initiate Request] Could not find the player initiating this request. Removing them.");
                            msg.SenderConnection.Disconnect("There was an error while processing your request so you have been disconnected. Sorry for the inconvenience.");
                        }
                    }
                    catch (Exception except)
                    {
                        Logger.Failure($"[Client.Throwable Initiate Request] ERROR\n{except}");
                    }
                    break;
                case 40: // SentGrenadeFinish TODO -- finish / calculate whether or not a player got hit
                    try
                    {
                        if (TryPlayerFromConnection(msg.SenderConnection, out Player player))
                        {
                            float x = msg.ReadFloat();
                            float y = msg.ReadFloat();
                            float height = msg.ReadFloat();
                            short ID = msg.ReadInt16();
                            //Logger.Warn($"Grenade Height: {height}\nGrenadeID: {ID}");
                            //Logger.Warn($"Player ThrowableCount: {player.ThrowableCounter}");

                            NetOutgoingMessage fragout = server.CreateMessage();
                            fragout.Write((byte)41);    // Header
                            fragout.Write(player.ID); // Short | PlayerID << who sent the grenade out
                            fragout.Write(ID);          // Short | GrenadeID
                            fragout.Write(x);           // Float | Grenade.X
                            fragout.Write(y);           // Float | Grenade.Y
                            fragout.Write(height);      // Float | Grenade.Height
                            fragout.Write((byte)0);     // Byte  | # of HitPlayers
                            //fragout.Write(playerID)   // Short | HitPlayerID
                            server.SendToAll(fragout, NetDeliveryMethod.ReliableOrdered);
                        }
                    } catch(Exception except)
                    {
                        Logger.Failure($"[Client.GrenadeFinishRequest] ERROR\n{except}");
                    }
                    break;

                case 55: //Entering a hamball
                    try // todo: more checks to make sure this is a valid enter attempt
                    {
                        short vehicleID = msg.ReadInt16();
                        if (TryPlayerFromConnection(msg.SenderConnection, out Player player))
                        {
                            if (_hamsterballList.ContainsKey(vehicleID))
                            {
                                CheckMovementConflicts(player);
                                NetOutgoingMessage hampterlol = server.CreateMessage();
                                hampterlol.Write((byte)56);
                                hampterlol.Write(player.ID);
                                hampterlol.Write(vehicleID);
                                hampterlol.Write(_hamsterballList[vehicleID].X);
                                hampterlol.Write(_hamsterballList[vehicleID].Y);
                                player.VehicleID = vehicleID;
                                server.SendToAll(hampterlol, NetDeliveryMethod.ReliableUnordered);
                            }
                        }
                    }
                    catch (Exception except)
                    {
                        Logger.Failure($"[Hammerball EnterAttempt] ERROR\n{except}");
                    }
                    break;

                case 57: // Client - Exit Hammerball
                    try
                    {
                        //short exitVehicleID = msg.ReadInt16();
                        if (TryPlayerFromConnection(msg.SenderConnection, out Player player))
                        {
                            NetOutgoingMessage nohampter = server.CreateMessage();
                            nohampter.Write((byte)58);
                            nohampter.Write(player.ID);
                            nohampter.Write(player.VehicleID);
                            nohampter.Write(player.PositionX);
                            nohampter.Write(player.PositionY);
                            server.SendToAll(nohampter, NetDeliveryMethod.ReliableUnordered);
                            _hamsterballList[player.VehicleID].X = player.PositionX;
                            _hamsterballList[player.VehicleID].Y = player.PositionY;
                            player.VehicleID = -1; // NEVER FORGET THIS
                        }
                    } catch (Exception except)
                    {
                        Logger.Failure($"[Hammerball ExitAttempt] ERROR\n{except}");
                    }
                    break;

                case 44: // Client - Request Current Spectator // TODO-- Implement
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
                    try // todo -- damage
                    {
                        if (TryPlayerFromConnection(msg.SenderConnection, out Player player))
                        {
                            short targetID = msg.ReadInt16();
                            float speed = msg.ReadFloat();
                            if (TryPlayerFromID(targetID, out Player target))
                            {
                                if (player.VehicleID != -1 && _hamsterballList.TryGetValue(player.VehicleID, out Hampterball ball))
                                {
                                    NetOutgoingMessage hampterhurt = server.CreateMessage();
                                    hampterhurt.Write((byte)61);
                                    hampterhurt.Write(player.ID);
                                    hampterhurt.Write(targetID);
                                    hampterhurt.Write(!target.isAlive); // DidKillPlayer >> False = NoKill (if wasn't obvious)
                                    hampterhurt.Write(ball.HP);
                                    server.SendToAll(hampterhurt, NetDeliveryMethod.ReliableOrdered);
                                }
                            }
                            else
                            {
                                Logger.Failure($"[Client.HamsterballHurtedSomeone] Could not find TargetPlayer. Simply ignoring the request.");
                            }
                        }
                        else
                        {
                            Logger.Failure($"[Client.HamsterballHurtedSomeone] Could not find the player initiating this request. Removing them.");
                            msg.SenderConnection.Disconnect("There was an error while processing your request so you have been disconnected. Sorry for the inconvenience.");
                        }
                    } catch (Exception except)
                    {
                        Logger.Failure($"[Client.HamsterballHurtedSomeone] ERROR\n{except}");
                    }
                    break;

                case 62: // Client - My Hamsterball Hit a Wall
                    if (TryPlayerFromConnection(msg.SenderConnection, out Player fPlayer))
                    {
                        HandleHamsterballBounce(fPlayer);
                    }
                    else
                    {
                        Logger.Failure($"[Receive Message 62 ERROR] - Could NOT find player with the provided NetConnection... NOT GOOD");
                    }
                    break;

                case 64: // ClientHitVehicle -- should be worky just fine
                    try
                    {
                        if (TryPlayerFromConnection(msg.SenderConnection, out Player player))
                        {
                            short _weaponID = msg.ReadInt16();
                            short _vehicleID = msg.ReadInt16();
                            short _projectileID = msg.ReadInt16();
                            if (_hamsterballList.ContainsKey(_vehicleID))
                            {
                                // go figure out how much damage we WANT to do
                                byte _wdam = 0;
                                if (_projectileID >= 0)
                                {
                                    if (player.ProjectileList.ContainsKey(_projectileID) && (player.ProjectileList[_projectileID].WeaponID == _weaponID))
                                    {
                                        Weapon weaponlol = _weaponsList[_weaponID];
                                        _wdam = weaponlol.ArmorDamage;
                                        if (weaponlol.VehicleDamageOverride > 0)
                                        {
                                            _wdam = weaponlol.VehicleDamageOverride;
                                        }
                                    }
                                    else
                                    {
                                        Logger.DebugServer("[Client Destroy-Vehicle-Request] Provided ProjectileID >= 0 but ProjectileList[_ProjID].WeaponID != this.weaponID");
                                    }
                                }
                                else if (_projectileID == -1)
                                {
                                    Weapon weaponlol = _weaponsList[_weaponID];
                                    _wdam = weaponlol.ArmorDamage; // if you are a big enough lunatic to go through every melee and change its ArmorDamage you can change how much armor is dinked. it's only fair.
                                }
                                // figure out how much we CAN remove
                                if ((_hamsterballList[_vehicleID].HP - _wdam) < 0)
                                {
                                    _wdam = _hamsterballList[_vehicleID].HP;
                                }
                                // subtract WantedDamage from This-Ball.HP
                                _hamsterballList[_vehicleID].HP -= _wdam;
                                //go send info over
                                NetOutgoingMessage ballhit = server.CreateMessage();
                                ballhit.Write((byte)65);
                                ballhit.Write(player.ID);
                                ballhit.Write(_vehicleID);
                                ballhit.Write(_hamsterballList[_vehicleID].HP);
                                ballhit.Write(_projectileID);
                                server.SendToAll(ballhit, NetDeliveryMethod.ReliableOrdered);
                            }
                        }
                        else
                        {
                            Logger.Failure($"[Client Destroy-Vehicle-Request] Unable to locate NetConnection \"{msg.SenderConnection}\" in the PlayerList. NetClient disconnected and request ignored.");
                            msg.SenderConnection.Disconnect("There was an error while processing your request, so your connection was dropped. Sorry for the inconvenience.");
                        }
                    }
                    catch (Exception except)
                    {
                        Logger.Failure($"[ClientHitVehicle] ERROR\n{except}");
                    }
                    break;

                case 66: // Client - Sent Emote // TODO:: make sure emote is valid, XY is valid
                    try
                    {
                        if (TryPlayerFromConnection(msg.SenderConnection, out Player player))
                        {
                            short emoteID = msg.ReadInt16();
                            float x = msg.ReadFloat();
                            float y = msg.ReadFloat();
                            float duration = msg.ReadFloat(); // TODO -- server should probably have a list of correct timings instead of taking word
                            //Logger.Warn($"Send EmoteID: {emoteID}");
                            //Logger.Warn($"Sent Emote Duration: {duration}");
                            //Logger.Warn($"Player XY: {player.PositionX}, {player.PositionY}\nSent XY: {x}, {y}");

                            CheckDrinkTape(player);
                            NetOutgoingMessage emote = server.CreateMessage();
                            emote.Write((byte)67);
                            emote.Write(player.ID);
                            emote.Write(emoteID);
                            server.SendToAll(emote, NetDeliveryMethod.ReliableSequenced);
                            player.isEmoting = true;
                            player.EmoteID = emoteID;
                            player.EmoteSpotX = x;
                            player.EmoteSpotY = y;
                            if (duration > -1)
                            {
                                player.EmoteEndTime = DateTime.UtcNow.AddSeconds(duration);
                            }
                            else
                            {
                                player.EmoteEndTime = DateTime.MaxValue;
                            }
                        }
                        else
                        {
                            Logger.Failure($"[Client.PerformEmoteRequest] Could not find the player initiating this request. Removing them.");
                            msg.SenderConnection.Disconnect("There was an error while processing your request so you have been disconnected. Sorry for the inconvenience.");
                        }
                    }
                    catch (Exception except)
                    {
                        Logger.Failure($"[Client.PerformEmoteRequest] ERROR\n{except}");
                    }
                    break;
                case 70: // Molecrate Open Request v0.90.2
                    try // Honestly, think nestesd ifs would've been better bc this is kind of hard to read. Perhaps cleanup
                    {
                        if (!_hasMatchStarted) return; // Don't bother doing anything if the match hasn't started.
                        // Is this connection actually a loaded-in Player?
                        if (!TryPlayerFromConnection(msg.SenderConnection, out Player player))
                        {
                            Logger.Failure($"[HandleMolecrateOpen] [Error] Could not locate Player @ NetConnection \"{msg.SenderConnection}\"; Connection has been dropped.");
                            msg.SenderConnection.Disconnect("There was an error processing your request. Sorry for the inconvenience.\nMessage: ACTION INVALID! PLAYER NOT IN SERVER_LIST");
                            return;
                        }
                        // If above is true, we get our Player and can start trying to read what they sent
                        short molecrateID = msg.ReadInt16();
                        if ((molecrateID < 0) || (molecrateID > _moleCrates.Length-1))
                        {
                            Logger.Failure($"[HandleMolecrateOpen] [Error] Player @ NetConnection \"{msg.SenderConnection}\" requested a MolecrateID that's outside the bounds of the array; Connection has been dropped.");
                            msg.SenderConnection.Disconnect("There was an error processing your request. Sorry for the inconvenience.\nMessage: Invalid MolecrateID.");
                            return;
                        }
                        // Make sure this molecrate is valid
                        // Is this too excessive though? As in, is it fair to kick people?
                        if (_moleCrates[molecrateID] == null || !_moleCrates[molecrateID].isCrateReal)
                        {
                            Logger.Failure($"[HandleMolecrateOpen] [Error] Player @ NetConnection \"{msg.SenderConnection}\"'s requested Molecrate isn't opened yet; Connection has been dropped.");
                            msg.SenderConnection.Disconnect("There was an error processing your request. Sorry for the inconvenience.\nMessage: Molecrate unavailable.");
                            return;
                        }
                        if (_moleCrates[molecrateID].isOpened) return; // Crate already opened. Just leave...
                        // Calculate distance between player and molecrate:
                        float distance = (_moleCrates[molecrateID].Position - new Vector2(player.PositionX, player.PositionY)).magnitude;
                        //Logger.DebugServer($"Distance: {distance}"); // distance seems to be ~10 in any direction; but diag ~14-15
                        if (distance > 14.5f)
                        {
                            // If all other checks pass, then this Player opened the molecrate.
                            NetOutgoingMessage openmsg = server.CreateMessage();
                            openmsg.Write((byte)71);
                            openmsg.Write(molecrateID);
                            openmsg.Write(_moleCrates[molecrateID].Position.x);
                            openmsg.Write(_moleCrates[molecrateID].Position.y);
                            server.SendToAll(openmsg, NetDeliveryMethod.ReliableOrdered);
                            // Do we need a separate Server-SendMolecrateOpened() method?
                            // TODO -- Drop Molecrate loot
                        }
                        else
                        {
                            Logger.Failure($"[HandleMoleCrateDrop] Player was not within the threshold. Returning, but investigate.\nPlayer {player.ID} ({player.Name}) @ {msg.SenderConnection}");
                        }
                    } catch (Exception except)
                    {
                        Logger.Failure($"[HandleMolecrateOpen] ERROR\n{except}");
                    }
                    break;

                case 72: // CLIENT_DESTORY_DOODAD
                    try // todo :: maybe do more with data obtained. maybe don't allocate more variables and stuff if not going to do anything
                    {
                        int checkX = msg.ReadInt32();
                        int checkY = msg.ReadInt32();
                        short projectileID = msg.ReadInt16();
                        serverDoodadDestroy(checkX, checkY);
                    }
                    catch (Exception except)
                    {
                        Logger.Failure($"[Server Destroy Doodad] ERROR\n{except}");
                    }
                    break;

                case 74: // Client - Sent Attack Windup
                    HandleAttackWindUp(msg);
                    break;

                case 76: // Client - Sent Attack Winddown
                    HandleAttackWindDown(msg);
                    break;
                case 87:
                    try
                    {
                        if (TryPlayerFromConnection(msg.SenderConnection, out Player player))
                        {
                            float posX = msg.ReadFloat();
                            float posY = msg.ReadFloat();
                            short gID = msg.ReadInt16();
                            NetOutgoingMessage lol = server.CreateMessage();
                            lol.Write((byte)111);
                            lol.Write(player.ID);
                            lol.Write(gID);
                        }
                        else
                        {
                            Logger.Failure("87 error");
                        }
                    }
                    catch (Exception except)
                    {
                        Logger.Failure($"87 error\n{except}");
                    }
                    break;
                case 90: // Client - Request Reload Cancel
                    // TOOD : actually do reloading
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
                    try
                    {
                        if (TryPlayerFromConnection(msg.SenderConnection, out Player taper))
                        {
                            if ((taper.ArmorTier > 0) && (taper.ArmorTapes != taper.ArmorTier))
                            {
                                CheckMovementConflicts(taper); // this may or may not cut someone off from taping for a second
                                taper.PositionX = msg.ReadFloat();
                                taper.PositionY = msg.ReadFloat();
                                taper.isTaping = true;
                                taper.NextTapeTime = DateTime.UtcNow.AddSeconds(3.0d);

                                NetOutgoingMessage tapetiem = server.CreateMessage();
                                tapetiem.Write((byte)99);
                                tapetiem.Write(taper.ID);
                                server.SendToAll(tapetiem, NetDeliveryMethod.ReliableUnordered);
                            }
                            else
                            {
                                Logger.Warn($"[Client Tape-Request] Seems as though this taper's ArmorTapes is equal to their ArmorTier or has no armor. Weird");
                            }
                        }
                        else
                        {
                            Logger.Failure($"[Client Tape-Request] Unable to locate NetConnection \"{msg.SenderConnection}\" in the PlayerList. NetClient disconnected.");
                            msg.SenderConnection.Disconnect("There was an error while processing your request, so your connection was dropped. Sorry for the inconvenience.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Failure($"[Client Tape-Request] [ERROR]\n{ex}");
                    }
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
        private void sendAuthenticationResponseToClient(NetIncomingMessage msg) // Receive PT 1 >> Send PacketType 2 // TODO -- Authenticate
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
            if (!_incomingConnections.ContainsKey(msg.SenderConnection))
            {
                _incomingConnections.Add(msg.SenderConnection, _PlayFabID);
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
                SortPlayerEntries();
            }
            //TODO: I think there is a better way of finding the ID that is available and stuff but not sure how
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] == null)
                {
                    _playerList[i] = new Player(_availableIDs[0], charID, umbrellaID, graveID, deathEffectID, emoteIDs, hatID, glassesID, beardID, clothesID, meleeID, gunSkinCount, gunskinGunID, gunSkinIndex, steamName, msg.SenderConnection);
                    SendMatchInformation(msg.SenderConnection, _availableIDs[0]);
                    //sendClientMatchInfo2Connect(_availableIDs[0], msg.SenderConnection);
                    _availableIDs.RemoveAt(0);
                    //find if person who connected is mod, admin, or whatever!
                    try
                    {
                        string _ThisPlayFabID = _incomingConnections[msg.SenderConnection];
                        JSONObject _PlayerJSONData;
                        for (int p = 0; p < _playerJSONdata.Count; p++)
                        {
                            if (_playerJSONdata[p] != null && _playerJSONdata[p]["PlayerID"] == _ThisPlayFabID)
                            {
                                _PlayerJSONData = (JSONObject)_playerJSONdata[p];
                                //Logger.Basic($"PlayerDataJSON for this Player:\n{_PlayerJSONData}");
                                // It would seem that not only does this check for if the key exists, but also its bool value. Probably wrong though lol
                                if (_PlayerJSONData["Admin"])
                                {
                                    _playerList[i].isDev = true;
                                }
                                if (_PlayerJSONData["Moderator"])
                                {
                                    _playerList[i].isMod = true;
                                }
                                if (_PlayerJSONData["Founder"])
                                {
                                    _playerList[i].isFounder = true;
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

        /// <summary>
        /// Sends the provided NetConnection a NetOutgoingMessage that contians all the information required in order for them to load into the match
        /// </summary>
        private void SendMatchInformation(NetConnection client, short assignedID) // Receive 3, Send 4
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)4);                 // Byte   |  MessageID 
            msg.Write(assignedID);              // Short  |  AssignedID
            // Send RNG Seeds
            msg.Write(_lootSeed);               // Int  |  LootGenSeed
            msg.Write(_coconutSeed);            // Int  |  CocoGenSeed
            msg.Write(_vehicleSeed);            // Int  | VehicleGenSeed
            // Match / Lobby Info...
            msg.Write(_lobbyRemainingSeconds);  // Double  |  LobbyTimeRemaining
            msg.Write("yerhAGJ");               // String  |  MatchUUID  // TODO-- Unique IDs
            msg.Write("solo");                  // String  |  Gamemode [solo, duos, squads]
            // Flight Path  // TOOD-- RANDOMIZE
            msg.Write(0f);                      // Float  | FlightPath X1
            msg.Write(0f);                      // Float  | FlightPath Y1
            msg.Write(4206f);                   // Float  | FlightPath X1
            msg.Write(4206f);                   // Float  | FlightPath Y1
            // Gallery Targets Positions // TODO -- MAKE REAL
            msg.Write((byte)0);
            msg.Write((byte)0);
            server.SendMessage(msg, client, NetDeliveryMethod.ReliableOrdered);
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
                SortPlayerEntries();
            }
            NetOutgoingMessage sendPlayerPosition = server.CreateMessage();
            sendPlayerPosition.Write((byte)10);
            sendPlayerPosition.Write((byte)GetValidPlayerCount()); // Added 12/2/22
            // Theoretically, I think it would be faster to just use the previously gotten list-length-to-null, and loop that many times then just stop
            // as opposed to just doing it over. However, in the previous tests the results said otherwise so... okii!
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null)
                {
                    sendPlayerPosition.Write(_playerList[i].ID);                        // MyAssignedID          |   Short  -- fixed as of 6/1/22
                    sendPlayerPosition.Write(_playerList[i].AnimalID);                  // CharacterID           |   Short
                    sendPlayerPosition.Write(_playerList[i].UmbrellaID);                // UmbrellaID            |   Short
                    sendPlayerPosition.Write(_playerList[i].GravestoneID);              // GravestoneID          |   Short
                    sendPlayerPosition.Write(_playerList[i].DeathExplosionID);          // ExplosionID           |   Short
                    for (int j = 0; j < _playerList[i].EmoteIDs.Length; j++)            // EmoteIDs              |   Short[6]
                    {
                        sendPlayerPosition.Write(_playerList[i].EmoteIDs[j]);
                    }
                    sendPlayerPosition.Write(_playerList[i].HatID);                     // HatID                 |   Short
                    sendPlayerPosition.Write(_playerList[i].GlassesID);                 // GlassesID             |   Short
                    sendPlayerPosition.Write(_playerList[i].BeardID);                   // BeardID               |   Short
                    sendPlayerPosition.Write(_playerList[i].ClothesID);                 // ClothesID             |   Short
                    sendPlayerPosition.Write(_playerList[i].MeleeID);                   // MeleeID               |   Short
                    //Gun skins
                    sendPlayerPosition.Write(_playerList[i].GunSkinCount);              // Amount of GunSkins    |   Byte
                    for (byte l = 0; l < _playerList[i].GunSkinCount; l++)
                    {
                        sendPlayerPosition.Write(_playerList[i].GunSkinKeys[l]);        // GunSkin GunID         |   Short in Short[]
                        sendPlayerPosition.Write(_playerList[i].GunSkinValues[l]);      // GunSkin SkinID        |   Byte in Byte[]
                    }

                    //Positioni?
                    sendPlayerPosition.Write(_playerList[i].PositionX);                     // PositionX             |   Float
                    sendPlayerPosition.Write(_playerList[i].PositionY);                     // PositionY             |   Float
                    sendPlayerPosition.Write(_playerList[i].Name);                          // PlayerName            |   String

                    sendPlayerPosition.Write(_playerList[i].EmoteID);                       // CurrentEmoteID        |   Short
                    sendPlayerPosition.Write((short)_playerList[i].LootItems[0].LootID);    // Equip 1 ID            |   Short
                    sendPlayerPosition.Write((short)_playerList[i].LootItems[1].LootID);    // Equip 2 ID            |   Short
                    sendPlayerPosition.Write(_playerList[i].LootItems[0].ItemRarity);       // Equip 1 Rarity        |   Byte
                    sendPlayerPosition.Write(_playerList[i].LootItems[1].ItemRarity);       // Equip 2 Rarity        |   Byte
                    sendPlayerPosition.Write(_playerList[i].ActiveSlot);                    // Current Equip Index   |   Byte
                    sendPlayerPosition.Write(_playerList[i].isDev);                         // Is User Developer     |   Bool
                    sendPlayerPosition.Write(_playerList[i].isMod);                         // Is User Moderator     |   Bool
                    sendPlayerPosition.Write(_playerList[i].isFounder);                     // Is User Founder       |   Bool
                    sendPlayerPosition.Write((short)450);                                   // Player Level          |   Short
                    sendPlayerPosition.Write((byte)0);                                      // Amount of Teammates   |   Byte
                    //sendPlayerPosition.Write((short)25);                                  // Teammate ID           |   Short
                }
            }
            Logger.Success("Going to be sending new player all other player positions.");
            server.SendToAll(sendPlayerPosition, NetDeliveryMethod.ReliableSequenced);
        }
        /// <summary>
        /// Sends a NetMessage to all NetPeers which forces the Player with the specified PlayerID to the provided X-Y coordinates and sets ParachuteMode.
        /// </summary>
        private void SendForcePosition(short id, float x, float y, bool parachute)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)8);     // Byte  | MessageID
            msg.Write(id);          // Short | PlayerID
            msg.Write(x);           // Float | PositionX
            msg.Write(y);           // Float | PositionY
            msg.Write(parachute);   // Bool  | Parachute? (fall from eagle)
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }
        /// <summary>
        /// Sends a NetMessage to all NetPeers which forces the Player with the specified PlayerID to the provided X-Y coordinates.
        /// </summary>
        private void SendForcePosition(short id, float x, float y)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)8);     // Byte  | MessageID
            msg.Write(id);          // Short | PlayerID
            msg.Write(x);           // Float | PositionX
            msg.Write(y);           // Float | PositionY
            msg.Write(false);       // Bool  | Parachute? << ALWAYS false with this version
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        /// <summary>
        /// Sends a NetMessage to all connected clients that contains information about a particular Player dying.
        /// </summary>
        private void SendPlayerDeath(short deceasedID, float gravestoneX, float gravestoneY, short killerID, short weaponID)
        {
            NetOutgoingMessage deathMsg = server.CreateMessage();
            deathMsg.Write((byte)15);       // Header                  | Byte
            deathMsg.Write(deceasedID);     // Deceased ID             | Short
            deathMsg.Write(gravestoneX);    // Deceased Gravestone X   | Float
            deathMsg.Write(gravestoneY);    // Deceased Gravestone Y   | Float
            deathMsg.Write(killerID);       // Killing PlayerID        | Short  // -3 = Banan; -2 = Gas; -1 = Nothing??; 0+ = Player; 
            deathMsg.Write(weaponID);       // WeaponID                | Short  // -1/-4 = Nothing?; -3 = Explosion; -2 = Hamsterball; 0+ Weapon
            server.SendToAll(deathMsg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Handles a NetIncomingMessage with a byte-header of 16.
        /// </summary>
        private void HandlePlayerShotMessage(NetIncomingMessage msg)
        {
            if (TryPlayerFromConnection(msg.SenderConnection, out Player player))
            {
                CheckMovementConflicts(player);
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
                    hitX = msg.ReadInt16();
                    hitY = msg.ReadInt16();
                    serverDoodadDestroy(hitX, hitY); // no clue how badly this messes with the efficiency
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
                    LootItem _weapon = player.LootItems[itemSlot]; // there's potential for failure here because ^^
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
                pmsg.Write(player.ID);
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
                        pmsg.Write((short)(projectileAngles[i] / 3.1415927f * 180f));
                        pmsg.Write(projectileIDs[i]);
                        pmsg.Write(projectileValids[i]);

                    }
                }
                server.SendToAll(pmsg, NetDeliveryMethod.ReliableSequenced);
            }
        }

        private void HandleAttackConfirm(NetIncomingMessage pmsg) // TOOD -- Make sure shot should've hit + damage falloff
        {
            try
            {
                // Attempt to read values. If any of these fail, then the catch below will prevent a crash
                short targetID = pmsg.ReadInt16();
                short weaponID = pmsg.ReadInt16();
                short projectileID = pmsg.ReadInt16();
                float hitX = pmsg.ReadFloat(); // NEED TO USE THIS AT SOME POINT
                float hitY = pmsg.ReadFloat(); // NEED TO USE THIS AT SOME POINT
                Player attacker, target;
                // Find the Players we're going to be messing with...
                if (!TryPlayerFromConnection(pmsg.SenderConnection, out attacker)) // If true, Attacker is given.
                {
                    Logger.Failure($"[HandleAttackConfirm] [Error] Could not locate Player @ NetConnection \"{pmsg.SenderConnection}\"; Connection has been dropped.");
                    pmsg.SenderConnection.Disconnect("There was an error processing your request. Sorry for the inconvenience.\nMessage: ACTION INVALID! PLAYER NOT IN SERVER_LIST");
                    return;
                }
                if (!TryPlayerFromID(targetID, out target)) // If true, Target is given.
                {
                    Logger.Failure($"[HandleAttackConfirm] [Error] Could not locate Player with ID {targetID}; The requester @ NetConnection \"{pmsg.SenderConnection}\" has been dropped.");
                    pmsg.SenderConnection.Disconnect($"There was an error processing your request. Sorry for the inconvenience.\nMessage: COULD NOT LOCATE PLAYER {targetID}");
                    return;
                }
                // Ok.... So have Players, but should they actually be damaged or anything?
                if (!_hasMatchStarted || !attacker.isAlive || !target.isAlive || target.isGodmode) return;
                // Figure out whether the attacker used a valid move...
                if ( (projectileID != -1) && !attacker.ProjectileList.ContainsKey(projectileID)) // Not meele-ing and Player hasn't shot this ProjectileID
                {
                    Logger.Failure($"[HandleAttackConfirm] [Error] Could not validate NetConnection @ \"{pmsg.SenderConnection}\"'s ProjectileID as valid; Connection has been dropped.");
                    pmsg.SenderConnection.Disconnect("There was an error processing your request. Sorry for the inconvenience.\nMessage: INVALID PROJECTILE-ID");
                    return;
                }
                // Time to figure out how to do damage then...
                // TODO -- Check if ProjectileID matches the WeaponID ?
                Weapon weapon = _weaponsList[weaponID];
                target.LastAttackerID = attacker.ID;
                target.LastWeaponID = weaponID;
                target.LastShotID = projectileID;
                // Check if Player is in a Hamsterball or not
                if (target.VehicleID >= 0)
                {
                    Hampterball hamsterball = _hamsterballList[target.VehicleID];
                    int ballDamage = weapon.ArmorDamage;
                    if (weapon.VehicleDamageOverride > 0) ballDamage = weapon.VehicleDamageOverride;
                    if ((hamsterball.HP - ballDamage) <= 0)
                    {
                        hamsterball.HP = 0;
                        SendExitVehicle(target.ID, target.VehicleID, target.PositionX, target.PositionY);
                        target.VehicleID = -1; // don't forget to reset this...
                    }
                    else
                    {
                        hamsterball.HP = (byte)(hamsterball.HP - ballDamage);
                    }
                    SendShotConfirmed(attacker.ID, targetID, projectileID, 0, hamsterball.ID, hamsterball.HP);
                    return;
                }
                // Surely we can attack the actual Player now- right?
                if (target.ArmorTapes > 0) // Has armor
                {
                    // Doing armor damage...
                    byte armorDamage = weapon.ArmorDamage;
                    if ((target.ArmorTapes - armorDamage) < 0) armorDamage = target.ArmorTapes; // Set armorDamage to taget.ArmorTapes to prevent going negative
                    target.ArmorTapes -= armorDamage;
                    // Figure out if it was just a regular gun, or dart/melee
                    // TODO -- damageThroughArmor became a % at some point. Likely when bow/sparrow launcher was added.
                    // So pretty much anything can potentially penetrate through armor and these checks will have to change.
                    if (weapon.PenetratesArmor) // Dartgun
                    {
                        // Calculate Dartgun Stuff....
                        int damage = weapon.Damage + (attacker.ProjectileList[projectileID].WeaponRarity * weapon.DamageIncrease);
                        int tickAdd = _ddgAddTicks;
                        if ((target.DartTicks + tickAdd) > _ddgMaxTicks) tickAdd = _ddgMaxTicks - target.DartTicks;
                        target.DartTicks += tickAdd;
                        if (target.DartTicks == 0) target.DartNextTime = DateTime.UtcNow.AddSeconds(_ddgTickRateSeconds);
                        test_damagePlayer(target, damage, attacker.ID, weaponID);
                    } else if (weapon.WeaponType == WeaponType.Melee)
                    {
                        test_damagePlayer(target, (int)Math.Floor(weapon.Damage / 2f), attacker.ID, weaponID);
                    }
                    // Figure out why sometimes darts double tick. At least, it happens more if the SendShotConfirmed is at the very end of everything.
                    SendShotConfirmed(attacker.ID, target.ID, projectileID, armorDamage, -1, 0);
                }
                else // No armor
                {
                    int damage = weapon.Damage;
                    Logger.DebugServer($"Calc'd Weapon Damage: {damage}");
                    if (projectileID >= 0)
                    {
                        damage += attacker.ProjectileList[projectileID].WeaponRarity * weapon.DamageIncrease;
                    }
                    Logger.DebugServer($"Check for weapon damage increase: {damage}");
                    SendShotConfirmed(attacker.ID, target.ID, projectileID, 0, -1, 0);
                    test_damagePlayer(target, damage, attacker.ID, weaponID);
                    if (weapon.Name == "GunDart")
                    {
                        int tickAdd = _ddgAddTicks;
                        if ((target.DartTicks + tickAdd) > _ddgMaxTicks) tickAdd = _ddgMaxTicks - target.DartTicks;
                        target.DartTicks += tickAdd;
                        if (target.DartTicks == 0) target.DartNextTime = DateTime.UtcNow.AddSeconds(_ddgTickRateSeconds);
                    }
                }
            }
            catch (Exception except)
            {
                Logger.Failure($"[HandleAttackConfirm] [Error]\n{except}");
            }
        }

        // TODO -- This can likely be improved. I want to be comfortable using this method without wondering whether or not it is OK
        private void test_damagePlayer(Player player, int damage, short sourceID, short weaponID)
        {
            // Check to see if the Player is DEAD or... /GOD
            if (!player.isAlive || player.isGodmode) return; // Don't worry about logging this...
            // Try and Damage
            Logger.DebugServer($"Player {player.Name} (ID: {player.ID}) Health: {player.HP}\nDamage Attempt: {damage}");
            if ((player.HP - damage) <= 0)
            {
                Logger.DebugServer("This damage attempt resulted in the death of the player.");
                player.HP = 0;
                player.isAlive = false;
                _hasPlayerDied = true;
                SendPlayerDeath(player.ID, player.PositionX, player.PositionY, sourceID, weaponID);
                return;
            }
            player.HP -= (byte)damage;
            Logger.DebugServer($"Final Health: {player.HP}");
        }

        /// <summary>
        /// Sends the "ShotInformation" message to all NetPeers with using the provided parameters.
        /// </summary>
        private void SendShotConfirmed(short attacker, short target, short projectileID, byte armorDamage, short vehicleID, byte vehicleHP)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)19);        // Byte  | MessageID - 19
            msg.Write(attacker);        // Short | AttackerID
            msg.Write(target);          // Short | TargetID
            msg.Write(projectileID);    // Short | ProjectileID
            msg.Write(armorDamage);     // Byte  | ArmorDinkCount << How much armor to remove
            msg.Write(vehicleID);       // Short | VehicleID << Use -1 if no vehicle was hit
            msg.Write(vehicleHP);       // Byte  | VehicleHP << I think it just sets the VehicleHP to this. Doesn't subtract; just sets
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
                string[] command = message.PeekString().Split(" ", 9);
                string responseMsg = "command executed... no info given...";
                short id, id2, amount;
                float cPosX, cPosY;
                if (TryPlayerFromConnection(message.SenderConnection, out Player LOL))
                {
                    Logger.Warn($"Player {LOL.ID} ({LOL.Name}) sent command \"{command[0]}\"");
                }
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
                    case "/safemode":
                        _safeMode = !_safeMode;
                        responseMsg = $"Safemode has been set to \"{_safeMode.ToString().ToUpper()}\".";
                        break;
                    case "/mole": // only for testing right now. sorry
                        {
                            float t = 3f;
                            Vector2 start = new Vector2(2000f, 2000f);
                            Vector2[] points = new Vector2[]
                            {
                                new Vector2(2020f, 2020f),
                                new Vector2(2000f, 2020f),
                                new Vector2(1990, 2030),
                                new Vector2(1950, 2000),
                                new Vector2(1860, 1984),
                                new Vector2(1890, 1600)
                            };
                            responseMsg = "Couldn't find a valid slot to put the mole in. Likely reached the limit";
                            for (int i = 0; i < _moleCrates.Length; i++)
                            {
                                if (_moleCrates[i] != null) continue;

                                // Send junk to client...
                                NetOutgoingMessage msg = server.CreateMessage();
                                msg.Write((byte)68); // msg id      -- byte
                                msg.Write((short)i); // molecrate id -- short
                                msg.Write(t);        // waittime    -- float
                                msg.Write(start.x);  // start x     -- float
                                msg.Write(start.y);  // start y     -- float
                                msg.Write((byte)points.Length); // amount of endpoints  -- byte
                                for (int j = 0; j < points.Length; j++)
                                {
                                    msg.Write(points[j].x); // newPointx    -- float
                                    msg.Write(points[j].y); // newPointx    -- float
                                }
                                server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);

                                // Actual Server-Side stuff
                                _moleCrates[i] = new MoleCrate(start, points, t);
                                responseMsg = $"MoleCrate Message Sent! ID: {i}\nStart: ({start.x}, {start.y})\nMove0: {points[0].x}, {points[0].y}\nTotal wait: {t + 2.7f}s; real: {t}s";
                                if (TryPlayerFromConnection(message.SenderConnection, out Player p)) SendForcePosition(p.ID, start.x, start.y);
                                break;
                            }
                        }
                        break;
                    /*case "/shutdown":
                    case "/stop":
                        server.Shutdown("The server is shutting down...");
                    break;*/
                    case "/gun":
                        try
                        {   
                            if (TryPlayerFromConnection(message.SenderConnection, out Player player))
                            {
                                if (command.Length >= 2)
                                {
                                    if (!short.TryParse(command[1], out short wepID))
                                    {
                                        responseMsg = $"Could not parse value as INT! (Read Value: {command[1]}";
                                    }
                                    // Find Weapon
                                    Weapon[] _gunList = Weapon.GetAllGunsList(_weaponsList);
                                    if (wepID <= 0 || (wepID > _gunList.Length)) // Make sure not going out of bounds
                                    {
                                        responseMsg = $"Value \"{wepID}\" is out of bounds!\nTotal Amount: {_gunList.Length} (IDs start at 1!)";
                                        break;
                                    }
                                    Weapon weapon = _gunList[wepID - 1]; // Acquire the weapon (EnteredID is +1 from actual; goes 1,2,3; NOT 0,1,2!
                                    byte rarity = weapon.RarityMaxVal; // In the event didn't provide a desired rarity, let's just set it to the max :]
                                    // Figure out if tried inputting a certain desired rarity
                                    if (command.Length >= 3 && int.TryParse(command[2], out int desiredRarity))
                                    {
                                        if (_safeMode)
                                        {
                                            //Logger.DebugServer($"Desired Rarity: {desiredRarity}; Min, Max: {weapon.RarityMaxVal}, {weapon.RarityMinVal}");
                                            //bool flag = (desiredRarity <= weapon.RarityMaxVal) && (desiredRarity >= weapon.RarityMinVal);
                                            if (desiredRarity < weapon.RarityMinVal) rarity = weapon.RarityMinVal;
                                            if (desiredRarity > weapon.RarityMaxVal) rarity = weapon.RarityMaxVal;
                                        }
                                        else
                                        {
                                            rarity = (byte)desiredRarity;
                                        }
                                    }
                                    // Make NewLoot MSG + Send it out
                                    NetOutgoingMessage gunMsg = MakeNewGunLootItem(weapon.Name, weapon.JSONIndex, rarity, (byte)weapon.ClipSize,
                                        new float[] { player.PositionX, player.PositionY, player.PositionX, player.PositionY });
                                    server.SendToAll(gunMsg, NetDeliveryMethod.ReliableOrdered);
                                    responseMsg = $"Created an instance of \"{weapon.Name}\" (ID = {weapon.JSONIndex}) @ Player {player.ID} ({player.Name})";
                                }
                                else
                                {
                                    responseMsg = "Invalid amount of arguments provided. This command takes 1-2.\nUsage: /gun {GunID} [OPTIONAL: RARITY]";
                                }
                            }
                            else
                            {
                                Logger.Failure($"[ServerHandle - ChatMessage (Command)] Could not locate Player @ NetConnection \"{message.SenderConnection}\"; Connection has been dropped.");
                                message.SenderConnection.Disconnect("There was an error processing your request. Sorry for the inconvenience.\nERROR: ACTION INVALID! PLAYER NOT IN SERVER_LIST");
                            }
                            
                        } catch (Exception except)
                        {
                            Logger.Failure($"Command Error:\n{except}");
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
                                if (amount - _playerList[id].HP <= 0)
                                {
                                    _playerList[id].HP += (byte)amount;
                                    if (_playerList[id].HP > 100) { _playerList[id].HP = 100; }
                                    responseMsg = $"Healed player {id} ({_playerList[id].Name} by {amount})";
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
                                _playerList[id].HP = (byte)amount;
                                responseMsg = $"Set player {id} ({_playerList[id].Name})'s health to {amount}";
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

                                _playerList[id].PositionX = cPosX;
                                _playerList[id].PositionY = cPosY;
                                responseMsg = $"Moved player {id} ({_playerList[id].Name}) to ({cPosX}, {cPosY}). ";
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
                                forcetoPos.Write(_playerList[id2].PositionX);
                                forcetoPos.Write(_playerList[id2].PositionY);
                                forcetoPos.Write(false);
                                server.SendToAll(forcetoPos, NetDeliveryMethod.ReliableOrdered);

                                _playerList[id].PositionX = _playerList[id2].PositionX;
                                _playerList[id].PositionY = _playerList[id2].PositionY;
                                responseMsg = $"Moved player {id} ({_playerList[id].Name}) to player {id2} ({_playerList[id2].Name}). ";
                            }
                            catch
                            {
                                responseMsg = "One or both arguments were not integer values. please try again.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /tp {playerID1} {playerID2}"; }
                        break;
                    case "/time":
                        if (!_hasMatchStarted)
                        {
                            if (command.Length == 2)
                            {
                                double newTime;
                                if (double.TryParse(command[1], out newTime))
                                {
                                    responseMsg = $"New time which the game will start: {newTime}";
                                    _lobbyRemainingSeconds = newTime;
                                    NetOutgoingMessage sTimeMsg2 = server.CreateMessage();
                                    sTimeMsg2.Write((byte)43);
                                    sTimeMsg2.Write(_lobbyRemainingSeconds);
                                    server.SendToAll(sTimeMsg2, NetDeliveryMethod.ReliableOrdered);
                                }
                                else
                                {
                                    responseMsg = $"Inputed value '{command[1]}' is not a valid time.\nValid input example: /time 20";
                                }
                            }
                            else
                            {
                                responseMsg = $"The game will begin in {_lobbyRemainingSeconds} seconds.";
                                NetOutgoingMessage sTimeMsg = server.CreateMessage();
                                sTimeMsg.Write((byte)43);
                                sTimeMsg.Write(_lobbyRemainingSeconds);
                                server.SendToAll(sTimeMsg, NetDeliveryMethod.ReliableOrdered);
                            }
                        }
                        else
                        {
                            responseMsg = "You cannot change the start time. The match has already started.";
                        }
                        break;
                    case "/safezone":
                        if (command.Length == 9)
                        {
                            try
                            {
                                float gx1, gy1, gx2, gy2, gr1, gr2, gwarn, gadvtime;
                                gx1 = float.Parse(command[1]);
                                gy1 = float.Parse(command[2]);
                                gr1 = float.Parse(command[3]);
                                gx2 = float.Parse(command[4]);
                                gy2 = float.Parse(command[5]);
                                gr2 = float.Parse(command[6]);
                                gwarn = float.Parse(command[7]);
                                gadvtime = float.Parse(command[8]);

                                NetOutgoingMessage gCircCmdMsg = server.CreateMessage();
                                gCircCmdMsg.Write((byte)33);
                                gCircCmdMsg.Write(gx1); gCircCmdMsg.Write(gy1);
                                gCircCmdMsg.Write(gx2); gCircCmdMsg.Write(gy2);
                                gCircCmdMsg.Write(gr1); gCircCmdMsg.Write(gr2);
                                gCircCmdMsg.Write(gwarn);

                                server.SendToAll(gCircCmdMsg, NetDeliveryMethod.ReliableOrdered);
                                isSkunkGasActive = false; // skunk gas is always turned off temporairly with this "command"
                                CreateSafezone(gx1, gy1, gr1, gx2, gy2, gr2, gwarn, gadvtime);
                                /* this all was copied into CreateSafezone()
                                 * SkunkGasWarningDuration = gwarn;
                                SkunkGasTotalApproachDuration = gadvtime;
                                SkunkGasRemainingApproachTime = gadvtime;
                                sv_EndSafezoneX = gx2;
                                sv_EndSafezoneY = gy2;
                                sv_EndSafezoneRadius = gr2;
                                sv_LastSafezoneX = gx1; 
                                sv_LastSafezoneY = gy1;
                                sv_LastSafezoneRadius = gr1;
                                isSkunkGasWarningActive = true;
                                _nextGasWarnTimerCheck = DateTime.UtcNow.AddMilliseconds(1000);*/
                                responseMsg = $"Started Gas Warning:\n-- Start Circle -- \nCenter: ({gx1}, {gy1})\nRadius: {gr1}\n-- End Circle -- :\nCenter: ({gx2}, {gy2})\nRadius: {gr2}\n\nTime until Approachment: ~{gwarn} seconds.\nMay Banan have mercy on your soul";

                            }
                            catch
                            {
                                responseMsg = "Error occurred while parsing values. Likely invalid type-- this command takes FLOATS.";
                            }
                        }
                        else
                        {
                            responseMsg = "Invalid arguments. Command Usage: /safezone {C1 Position X} {C1 Position Y} {C1 Radius} {C2 Position X} {C2 Position Y} {C2 Radius} {DELAY}";
                        }
                        break;
                    case "/list":
                        try
                        {
                            if (TryPlayerFromConnection(message.SenderConnection, out Player player))
                            {
                                int _initSize = GetValidPlayerCount() * 16;
                                System.Text.StringBuilder listText = new System.Text.StringBuilder("|-- Players --\n", _initSize);
                                //string list = "-- Player--\n";
                                for (int i = 0; i < _playerList.Length; i++)
                                {
                                    if (_playerList[i] != null)
                                    {
                                        listText.AppendLine($"| {_playerList[i].ID} ({_playerList[i].Name})");
                                    }
                                }
                                listText.Append($"|-------------");
                                string _dump = listText.ToString();
                                NetOutgoingMessage msg = server.CreateMessage();
                                msg.Write((byte)96);
                                msg.Write(_dump);
                                server.SendMessage(msg, message.SenderConnection, NetDeliveryMethod.ReliableOrdered);
                                responseMsg = "The PlayerList has been copied to your clipboard.";
                            }
                            else
                            {
                                Logger.Failure($"[ServerHandle - ChatMessage (Command)] Could not locate Player @ NetConnection \"{message.SenderConnection}\"; Connection has been dropped.");
                                message.SenderConnection.Disconnect("There was an error processing your request. Sorry for the inconvenience.\nERROR: ACTION INVALID! PLAYER NOT IN SERVER_LIST");
                            }
                        }
                        catch (Exception except)
                        {
                            Logger.Failure($"Command Error:\n{except}");
                        }
                        break;
                    case "/forceland": // msg 8
                        if (command.Length > 1)
                        {
                            short forceID;
                            if (short.TryParse(command[1], out forceID))
                            {
                                if (TryPlayerFromID(forceID, out Player newlander))
                                {
                                    SendForcePosition(forceID, newlander.PositionX, newlander.PositionX, true);
                                    responseMsg = $"Successfully forced player {forceID} ({newlander.Name}) to eject.";
                                }
                            }
                        }
                        else
                        {
                            if (TryPlayerFromConnection(message.SenderConnection, out Player con))
                            {
                                SendForcePosition(con.ID, con.PositionX, con.PositionY, true);
                                responseMsg = $"Ejected player {con.ID} ({con.Name}).";
                            }
                        }
                        break;
                    case "/divemode":
                        if (command.Length > 1)
                        {
                            if (short.TryParse(command[1], out short diveID))
                            {
                                if (TryPlayerFromID(diveID, out Player diver))
                                {
                                    if (command.Length > 2 && bool.TryParse(command[2], out bool givenMode))
                                    {
                                        diver.isDiving = givenMode;
                                        ServerAMSG_ParachuteUpdate(diver.ID, diver.isDiving);
                                        responseMsg = $"Parachute mode for {diver.Name} (ID: {diver.ID}) set to: {diver.isDiving}";
                                    }
                                    else
                                    {
                                        diver.isDiving = !diver.isDiving;
                                        ServerAMSG_ParachuteUpdate(diver.ID, diver.isDiving);
                                        responseMsg = $"Parachute mode for {diver.Name} (ID: {diver.ID}) set to: {diver.isDiving}";
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
                            if (TryPlayerFromConnection(message.SenderConnection, out Player diver))
                            {
                                diver.isDiving = !diver.isDiving;
                                ServerAMSG_ParachuteUpdate(diver.ID, diver.isDiving);
                                responseMsg = $"Parachute mode for Player {diver.ID}({diver.Name}) set to {diver.isDiving.ToString().ToUpper()}";
                            }
                            else
                            {
                                responseMsg = $"There was an error locating the player who used dive mode for some reason...";
                            }
                        }
                        break;
                    case "/startshow": // msg 104
                        if (command.Length > 1)
                        {
                            if (byte.TryParse(command[1], out byte showNum))
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
                    case "/pray": // msg 110
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
                    case "/kill": // ERROR MESSAGE WHEN COMMAND FAILS DOESN'T DISPLAY PROPERLY. LOOK AT /GOD MODE FOR EXAMPLE ON HOW TO FIX -- 12/24/22
                        try
                        {
                            Logger.DebugServer($"command length: {command.Length}");
                            if (command.Length >= 2 && (command[1] != ""))
                            {
                                if (TryPlayerFromName(command[1], out Player killPlayer) || (int.TryParse(command[1], out int retID) && TryPlayerFromID(retID, out killPlayer)))
                                {
                                    if (!killPlayer.isAlive)
                                    {
                                        responseMsg = $"Can't kill Player {killPlayer.ID} ({killPlayer.Name})! They're already dead!";
                                        break;
                                    }
                                    if (killPlayer.isGodmode) killPlayer.isGodmode = false;
                                    test_damagePlayer(killPlayer, killPlayer.HP, -3, -1);
                                    responseMsg = $"Player {killPlayer.ID} ({killPlayer.Name}) has been killed.";
                                }
                                else
                                {
                                    responseMsg = $"Could not locate player \"{command[1]}\"";
                                }
                            }
                            else { responseMsg = "Not enough arguments provided!\nUsage: /kill {PlayerID} OR /kill {PlayerName}"; }
                        } catch
                        {
                            responseMsg = $"Value \"{command[1]}\" is invalid.";
                        }
                        break;
                    case "/godmode":
                    case "/god":
                        try
                        {
                            if (command.Length == 1 || (command.Length >= 2 && command[1] == "")) // If it was just meant to be the /command (the || prevents spaces from making this check fail)
                            {
                                if (TryPlayerFromConnection(message.SenderConnection, out Player sender))
                                {
                                    sender.isGodmode = !sender.isGodmode;
                                    responseMsg = string.Format("Godmode set to {0} for Player {1} ({2})", sender.isGodmode.ToString().ToUpperInvariant(), sender.ID, sender.Name);
                                }
                            }
                            else if (command.Length >= 2) // The /command + any arguments
                            {
                                if (TryPlayerFromName(command[1], out Player newgod) || ( int.TryParse(command[1], out int Id) && TryPlayerFromID(Id, out newgod) ))
                                {
                                    newgod.isGodmode = !newgod.isGodmode;
                                    responseMsg = string.Format("Godmode set to {0} for Player {1} ({2}).", newgod.isGodmode.ToString().ToUpperInvariant(), newgod.ID, newgod.Name);
                                }
                                else
                                {
                                    responseMsg = $"Could not locate player \"{command[1]}\".";
                                }
                            }
                        }
                        catch (Exception except)
                        {
                            // In theory you never see this. So yeah logging to see what went wrong I guess
                            Logger.Failure($"[Command Handle] [Error] There was an error while processing the \"/god\" command.\n{except}");
                            responseMsg = "An error occurred while parsing the command! Try again?\nUsage: /god (PlayerID, PlayerName, or NoArgs)";
                        }
                        break;
                    case "/ghost": // msg 105
                        //TODO : remove or make better command -- testing only
                        NetOutgoingMessage ghostMsg = server.CreateMessage();
                        ghostMsg.Write((byte)105);
                        server.SendMessage(ghostMsg, message.SenderConnection, NetDeliveryMethod.ReliableUnordered);
                        break;
                    case "/roll": // msg 106
                        NetOutgoingMessage roll = server.CreateMessage();
                        roll.Write((byte)106);
                        roll.Write((short)0);
                        roll.Write("45 lol"); // "PLAYER_NAME rolls a NUM! (NUM / ODDS)"
                        server.SendMessage(roll, message.SenderConnection, NetDeliveryMethod.ReliableSequenced);
                        break;
                    /* this works it's just useless
                    case "/removeweapons":
                        NetOutgoingMessage rmMsg = server.CreateMessage();
                        rmMsg.Write((byte)125);
                        rmMsg.Write(getPlayerID(message.SenderConnection));
                        server.SendToAll(rmMsg, NetDeliveryMethod.ReliableUnordered);
                        responseMsg = $"Weapons removed for {_playerList[getPlayerArrayIndex(message.SenderConnection)].Name}";
                        break;*/
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
                        _canCheckWins = !_canCheckWins;
                        responseMsg = $"_canCheckWins set to \"{_canCheckWins.ToString().ToUpper()}\"";
                        break;
                    case "/drink":
                        Logger.Success("drink command");
                        try
                        {
                            if (TryPlayerFromConnection(message.SenderConnection, out Player drinky)) // band aid fix
                            {
                                if (command.Length == 2)
                                {
                                    drinky.Drinkies = byte.Parse(command[1]);
                                }
                                else if (command.Length == 3)
                                {
                                    short searchid = short.Parse(command[1]);
                                    if (TryPlayerFromID(searchid, out Player plr))
                                    {
                                        plr.Drinkies = byte.Parse(command[2]);
                                    }
                                }
                                else
                                {
                                    responseMsg = "Invalid parameters. Usage: /drink [ID] [AMOUNT] OR /drink [AMOUNT]";
                                }
                            }
                        } catch
                        {
                            responseMsg = "There was an error while processing your request.";
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
                                _playerList[id].ArmorTapes = (byte)amount;
                                responseMsg = $"done {amount}";
                            }
                            catch
                            {
                                responseMsg = "One or both arguments were not integer values. please try again.";
                            }
                        }
                        else { responseMsg = "Insufficient amount of arguments provided. usage: /tape {ID} {AMOUNT}"; }
                        break;
                    case "/getpos":
                    case "/pos":
                        Logger.Success("position command");
                        try
                        {
                            if (TryPlayerFromConnection(message.SenderConnection, out Player _posplayer))
                            {
                                responseMsg = $"Player {_posplayer.ID} ({_posplayer.Name}) X: {_posplayer.PositionX}; Y: {_posplayer.PositionY}";
                            }
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

        /// <summary>
        /// Sends a NetMessage to all NetConnections that a Player with the given PlayerID has stopped emoting.
        /// </summary>
        private void ServerEndEmoter(short id)
        {
            NetOutgoingMessage emote = server.CreateMessage();
            emote.Write((byte)67);
            emote.Write(id);
            emote.Write((short)-1);
            server.SendToAll(emote, NetDeliveryMethod.ReliableSequenced);
        }

        // Client[51] >> Server[52] -- Coconut Eat Request
        private void serverSendCoconutEaten(NetIncomingMessage msg) // If we ever get to 2020+ versions with powerups; this'll need some work.
        {
            try
            {
                if (TryPlayerFromConnection(msg.SenderConnection, out Player cocop))
                {
                    ushort cocoID = msg.ReadUInt16();

                    if (_coconutList.ContainsKey(cocoID)) // maybe add check if Client is close enough? << nah, not a big deal right now. if requested.
                    {
                        byte heal = 5; // perhaps this can become a server var so users can define their own heal-rate?
                        if ((cocop.HP + heal) > 100)
                        {
                            heal = (byte)(100 - cocop.HP);
                        }
                        cocop.HP += heal;
                        CheckMovementConflicts(cocop);
                        NetOutgoingMessage coco = server.CreateMessage();
                        coco.Write((byte)52);
                        coco.Write(cocop.ID);
                        coco.Write(cocoID);
                        server.SendToAll(coco, NetDeliveryMethod.ReliableUnordered);
                        if (_hasMatchStarted)
                        {
                            _coconutList.Remove(cocoID);
                        }
                    }
                    else
                    {
                        Logger.Failure($"[ServerSendCoconutEaten] Unable to locate Coconut with an ID of \"{cocoID}\".");
                    }
                }
                else
                {
                    Logger.Failure("[ServerSendCoconutEaten] There was an error while trying to locate a Player with this NetConnection");
                }
            } catch (Exception except)
            {
                Logger.Failure($"[ServerSendCoconutEaten] [ERROR]\n{except}");
            }
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

        /// <summary>
        /// Sends a "PlayerExitedVehicle" message to all connected NetPeers using the provided parameters. (Does not reset Player's VehicleID field)
        /// </summary>
        private void SendExitVehicle(short playerID, short hamsterballID, float exitPosX, float exitPosY)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)58);        // Byte  | Message ID - 58
            msg.Write(playerID);        // Short | PlayerID
            msg.Write(hamsterballID);   // Short | HamsterballID
            msg.Write(exitPosX);        // Float | ExitPositionX
            msg.Write(exitPosY);        // Float | ExitPositionY
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        //send 61
        private void serverSendVehicleHitPlayer(NetIncomingMessage aMsg) // TODO: needs updating/cleanup
        {
            Logger.Header("[Vehicle Hit Player] Packet Received");
            if (TryPlayerFromConnection(aMsg.SenderConnection, out Player _attacker))
            {
                if (_attacker.VehicleID != -1) // make sure the player is actually in a vehicle?
                {
                    try
                    {
                        short _targetID = aMsg.ReadInt16();
                        float _speed = aMsg.ReadFloat();
                        Logger.Basic($"[Vehicle Hit Player - Calculations] Attacker ID: {_attacker}; Target ID: {_targetID}; Hamsterball Speed: {_speed}");
                        if (TryPlayerFromID(_targetID, out Player _target))
                        {
                            NetOutgoingMessage msg = server.CreateMessage();
                            msg.Write((byte)61);
                            msg.Write(_attacker.ID); // person who is attacking
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
        }

        /// <summary>
        /// Server Handle Player Loot - Handles incoming Looted message
        /// </summary>
        private void ServerHandleMatchLootRequest(NetIncomingMessage msg)
        {
            //TODO -- cleanup / fix
            /* main issue -- client game seems to repeatedly try and claim a loot item. it does this so fast it can claim...
            ...the same loot item the server is already dealing with. duping it, and also taking another. glitches mostly drinks and tape
            */
            Logger.Header("Player Looted Item");
            try
            {
                //Player thisPlayer = _playerList[getPlayerArrayIndex(msg.SenderConnection)];
                if (TryPlayerFromConnection(msg.SenderConnection, out Player thisPlayer))
                {
                    NetOutgoingMessage _extraLootMSG = null; // Extra loot message to send after telling everyone about loot and junk
                    short m_LootID = (short)msg.ReadInt32();
                    byte m_PlayerSlot = msg.ReadByte();
                    LootItem m_LootToGive = _itemList[m_LootID];

                    switch (m_LootToGive.LootType)
                    {
                        case LootType.Weapon: // Stupidity
                            Logger.Basic($" -> Player found a weapon.\n{m_LootToGive.LootName}");
                            if (m_PlayerSlot == 1 || m_PlayerSlot == 0) // Weapon 1 or 2 | Not Melee
                            {
                                if (thisPlayer.LootItems[m_PlayerSlot].WeaponType != WeaponType.NotWeapon) // means there's already something here
                                {
                                    // So problem, can also dupe weapons. Might need to put a cooldown on when a player can pick them up again...
                                    LootItem oldLoot = thisPlayer.LootItems[m_PlayerSlot];
                                    _extraLootMSG = MakeNewGunLootItem(oldLoot.LootName, (short)oldLoot.IndexInList, oldLoot.ItemRarity, oldLoot.GiveAmount, new float[] { thisPlayer.PositionX, thisPlayer.PositionY, thisPlayer.PositionX, thisPlayer.PositionY });
                                    thisPlayer.LootItems[m_PlayerSlot] = m_LootToGive;
                                }
                                thisPlayer.LootItems[m_PlayerSlot] = m_LootToGive;
                                //Logger.Failure("  -> WARNING NOT YET FULLY HANDLED");
                                break;
                            }
                            if (m_PlayerSlot != 3) // Slot 3 = Throwables. Slot 2 can't be used; and so anything other than 0, 1, and 3 just skip.
                            {
                                Logger.Failure("  -> Player has found a weapon. However, none of the slot it claims to be accessing are valid here.");
                                break;
                            }

                            // Throwable / Slot_3 | PlayerSlot 4 | so... m_PlayerSlot-1 = 2 >> right array index
                            if (thisPlayer.LootItems[2].WeaponType != WeaponType.NotWeapon) // Player has throwable here already
                            {
                                // uuuh how do we figure this out ?????
                                Logger.DebugServer($"Throwable LootName Test: Plr: {thisPlayer.LootItems[2].LootName}\nThis new loot: {m_LootToGive.LootName}");
                                if (thisPlayer.LootItems[2].LootName == m_LootToGive.LootName) // Has this throwable-type already
                                {
                                    thisPlayer.LootItems[2].GiveAmount += m_LootToGive.GiveAmount;
                                    Logger.Basic($"{thisPlayer.LootItems[2].LootName} - Amount: {thisPlayer.LootItems[2].GiveAmount}");
                                    break;
                                }
                                // Else = Player has a throwable, BUT it is a different type so we need to re-spawn the old one
                                _extraLootMSG = MakeNewThrowableLootItem((short)thisPlayer.LootItems[2].IndexInList, thisPlayer.LootItems[2].GiveAmount, thisPlayer.LootItems[2].LootName, new float[] { thisPlayer.PositionX, thisPlayer.PositionY, thisPlayer.PositionX, thisPlayer.PositionY });
                                // Go give player the loot item.
                                thisPlayer.LootItems[2] = m_LootToGive;
                                break;
                            }
                            // Player doesn't have a throwable
                            thisPlayer.LootItems[2] = m_LootToGive;
                            break;
                        case LootType.Juices:
                            Logger.Basic($" -> Player found some drinkies. +{m_LootToGive.GiveAmount}");
                            if (thisPlayer.Drinkies + m_LootToGive.GiveAmount > 200)
                            {
                                m_LootToGive.GiveAmount -= (byte)(200 - thisPlayer.Drinkies);
                                thisPlayer.Drinkies += (byte)(200 - thisPlayer.Drinkies);
                                _extraLootMSG = MakeNewDrinkLootItem(m_LootToGive.GiveAmount, new float[] { thisPlayer.PositionX, thisPlayer.PositionY, thisPlayer.PositionX, thisPlayer.PositionY });
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
                                _extraLootMSG = MakeNewTapeLootItem(m_LootToGive.GiveAmount, new float[] { thisPlayer.PositionX, thisPlayer.PositionY, thisPlayer.PositionX, thisPlayer.PositionY });
                                break;
                            }
                            thisPlayer.Tapies += m_LootToGive.GiveAmount;
                            break;
                        case LootType.Armor:
                            Logger.Basic($" -> Player got some armor. Tier{m_LootToGive.ItemRarity} - Ticks: {m_LootToGive.GiveAmount}");
                            if (thisPlayer.ArmorTier != 0)
                            {
                                _extraLootMSG = MakeNewArmorLootItem(thisPlayer.ArmorTapes, thisPlayer.ArmorTier, new float[] { thisPlayer.PositionX, thisPlayer.PositionY, thisPlayer.PositionX, thisPlayer.PositionY });
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
                    testMessage.Write(thisPlayer.ID); // Player ID
                    testMessage.Write((int)m_LootID); // Loot Item ID
                    testMessage.Write(m_PlayerSlot); // Player Slot to update
                    if (!_hasMatchStarted) // Write Forced Rarity
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
            }
            catch (Exception Except)
            {
                Logger.Failure(Except.ToString());
            }
        }
        /// <summary>
        /// Handles a LootMessage for while in the Lobby!
        /// </summary>
        /// <param name="aMsg"></param>
        private void ServerHandleLobbyLootRequest(NetIncomingMessage aMsg) // TODO: Forced-Rarity updates per-player, right now not so! Also maybe keep track of LootItems in lobby?
        {
            try
            {
                if (!TryPlayerFromConnection(aMsg.SenderConnection, out Player player)) // PAIN!! CAN JUST DO THIS INSTEAD OF INDENTING OVER AND OVER
                {
                    Logger.Failure($"[ServerHandle - LootRequest (LOBBY)] Could not locate Player @ NetConnection \"{aMsg.SenderConnection}\"; Connection has been dropped.");
                    aMsg.SenderConnection.Disconnect("There was an error processing your request. Sorry for the inconvenience.\nERROR: ACTION INVALID! PLAYER NOT IN SERVER_LIST");
                    return;
                }
                int _lootID = aMsg.ReadInt32();
                byte _slot = aMsg.ReadByte();
                NetOutgoingMessage msg = server.CreateMessage(9); // byte, short, int, byte, byte
                msg.Write((byte)22);        // Byte   |  MessageID
                msg.Write(player.ID);     // Short  |  PlayerID
                msg.Write(_lootID);         // Int    |  LootID
                msg.Write(_slot);           // Byte   |  SlotID
                msg.Write((byte)4);         // Byte   |  WeaponRarity
                server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
            }
            catch (Exception except)
            {
                Logger.Failure($"[ServerHandle - LootRequest (LOBBY)] ERROR\n{except}");
            }
        }

        /// <summary>
        /// Sends a message to all clients connected to the server telling them that a Player in a certain Vehicle bounced off a wall.
        /// </summary>
        private void HandleHamsterballBounce(Player aPlayer) // Send PacketType 63
        {
            // TODO
            // Right now it is fine to just have server call this after calling method to find player object.
            // In the future, I think it would be nice if the method could just be called with the received MSG object and put here to...
            // ... deal with and do all that junk and stuff instead of the way it is now with some somewhat redundant calling and junk
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)63);
            msg.Write(aPlayer.ID);
            msg.Write(aPlayer.VehicleID);
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }
        /// <summary>
        /// Attempts to locate a Doodad at the given Coordinates and then destroys it!
        /// </summary>
        private void serverDoodadDestroy(int checkX, int checkY) // todo :: check for explosions
        {
            int _doodadCount = _doodadList.Count;
            Int32Point hitCoords = new Int32Point(checkX, checkY);
            for (int i = 0; i < _doodadCount; i++)
            {
                bool flipper = false;
                if (!flipper && (_doodadList[i].X == checkX) && (_doodadList[i].Y == checkY))
                {
                    Logger.DebugServer("[DoodadDestroy] [FOUND] This is the simple check working.");
                    flipper = true;
                }
                if (!flipper && (_doodadList[i].OffsetCollisionPoints != null))
                {
                    int ptsCount = _doodadList[i].OffsetCollisionPoints.Count; // for whatever reason, .Count is slower than caching
                    for (int j = 0; j < ptsCount; j++)
                    {
                        if (_doodadList[i].OffsetCollisionPoints[j] == hitCoords)
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
                    test.Write((short)_doodadList[i].X);
                    test.Write((short)_doodadList[i].Y);
                    int d_count = _doodadList[i].OffsetCollisionPoints.Count;
                    test.Write((short)d_count);
                    for (int m = 0; m < d_count; m++)
                    {
                        test.Write((short)_doodadList[i].OffsetCollisionPoints[m].x);
                        test.Write((short)_doodadList[i].OffsetCollisionPoints[m].y);
                        test.Write((byte)1);
                    }
                    test.Write((byte)0);
                    //test.Write((short)PLAYERID) << would only use this if someone got hit
                    server.SendToAll(test, NetDeliveryMethod.ReliableSequenced);
                    if (_hasMatchStarted) _doodadList.RemoveAt(i); // only pop from list if match is in progress
                    break;
                }
            }
        }
        // Receive 74, Send 75 [[Seems to have something to do with minigun]]
        private void HandleAttackWindUp(NetIncomingMessage aMsg) // TODO - Check if vaid action
        {
            try
            {
                if (TryPlayerFromConnection(aMsg.SenderConnection, out Player player))
                {
                    short weaponID = aMsg.ReadInt16(); // WeaponID | Short
                    byte slot = aMsg.ReadByte();       // SlotID   | Byte
                    // Weapon1 = 0; Weapon2 = 1; Melee = 2
                    // Figure out if player actually has weapon in that slot
                    if ( (player.LootItems[slot].IndexInList == weaponID) || !_hasMatchStarted)
                    {
                        NetOutgoingMessage msg = server.CreateMessage();
                        msg.Write((byte)75);    // Byte   |  MessageID
                        msg.Write(player.ID); // Short  |  PlayerID
                        msg.Write(weaponID);    // Short  |  WeaponID
                        msg.Write(slot);        // Byte   |  SlotIndex
                        server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
                    }
                    else
                    {
                        Logger.Failure($"[ServerHandle - AttackWindUp] Player @ NetConnection \"{aMsg.SenderConnection}\" sent AttackWindUp message, but WeaponID is not in Slot#; Connection has been dropped.");
                        aMsg.SenderConnection.Disconnect($"There was an error processing your request. Sorry for the inconvenience.\n(NO WEAPON IN SLOT [ID, Slot: {weaponID}, {slot}])");
                    }
                }
                else
                {
                    Logger.Failure($"[ServerHandle - AttackWindUp] Could not locate Player @ NetConnection \"{aMsg.SenderConnection}\"; Connection has been dropped.");
                    aMsg.SenderConnection.Disconnect("There was an error processing your request. Sorry for the inconvenience.\n(CONNECTION NOT IN PLAYERLIST)");
                }
            } catch (Exception except)
            {
                Logger.Failure($"[ServerHandle - AttackWindUp] ERROR\n{except}");
            }
        }

        // Receive 76, Send 77 [[Seems to have something to do with minigun]]
        private void HandleAttackWindDown(NetIncomingMessage aMsg) // TODO -- Clients can actually send this AFTER looting sometimes, which kicks them. Kind of funny, but probably should be fixed
        {
            try
            {
                Player player; // if TryPlayerFromConnection is true, this gets set
                if (!TryPlayerFromConnection(aMsg.SenderConnection, out player))
                {
                    Logger.Failure($"[ServerHandle - AttackWindDown] Could not locate Player @ NetConnection \"{aMsg.SenderConnection}\"; Connection has been dropped.");
                    aMsg.SenderConnection.Disconnect("There was an error processing your request. Sorry for the inconvenience.\n(CONNECTION NOT IN PLAYERLIST)");
                }
                short weaponID = aMsg.ReadInt16();
                byte slot = aMsg.ReadByte();
                // If you can read the values above, just use them to make the message for ending the rev.
                // Perhaps it isn't the best idea to be doing this, but it's the easiest solution to prevent kicking for no reason.
                NetOutgoingMessage msg = server.CreateMessage();
                msg.Write((byte)77);    // Byte   |  MessageID
                msg.Write(player.ID); // Short  |  PlayerID
                msg.Write(weaponID);    // Short  |  WeaponID  //msg.Write(slot); // Appears un-needed as of now
                server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
                if ((player.LootItems[slot].IndexInList != weaponID) && _hasMatchStarted) // Let's you know when this fails
                {
                    Logger.Failure($"[ServerHandle - AttackWindDown] Player @ NetConnection \"{aMsg.SenderConnection}\" sent AttackWindDown message, but WeaponID is not in Slot#.");
                }
            } catch (Exception except)
            {
                Logger.Failure($"[ServerHandle - AttackWindDown] ERROR\n{except}");
            }
        }

        //client[47] > server[48] -- pretty much a copy of sendingTape and stuff... info inside btw...
        private void serverSendPlayerStartedHealing(NetConnection sender, float posX, float posY) // TODO << update. I want it in HandleMessage method not separate from it.
        {
            if (TryPlayerFromConnection(sender, out Player player))
            {
                CheckMovementConflicts(player); // this may or may not accidently cut-off someone from healing for a second.
                player.PositionX = posX;
                player.PositionY = posY;
                player.isDrinking = true;
                player.NextHealTime = DateTime.UtcNow.AddSeconds(1.2d);
                NetOutgoingMessage msg = server.CreateMessage();
                msg.Write((byte)48);
                msg.Write(player.ID);
                server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
            }
            else
            {
                Logger.Failure($"[ServerSendPlayerStartedHealing] There was an error while attempting to locate the Sender");
                sender.Disconnect("There was an error while processing your request. We are sorry for the inconvenience");
            }
        }

        //r[87] > s[111]
        private void GSH_HandleDeployedTrap(NetIncomingMessage message) // unfinished :/
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)111);
            msg.Write(getPlayerID(message.SenderConnection));
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }
        /// <summary>
        /// Sends a NetMessage to all connected clients that states a player with the provided PlayerID has finished/stopped taping their armor.
        /// </summary>
        private void ServerAMSG_EndedTaping(short aID) // Server Announcement Message - Someone Finished Taping || Type-100
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)100);
            msg.Write(aID);
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }
        /// <summary>
        /// Sends a NetMessage to all connected clients that a player with the provided PlayerID has had their parachute-mode updated.
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
            _totalLootCounter++;              // Increase LootCounter by 1
            msg.Write((byte)20);                // Header       |  Byte
            msg.Write(_totalLootCounter);     // LootID       |  Int
            msg.Write((byte)LootType.Juices);   // LootType     |  Byte
            msg.Write(aDrinkAmount);            // Info/Amount  |  Short
            msg.Write(aPositions[0]);           // Postion X1   |  Float
            msg.Write(aPositions[1]);           // Postion Y1   |  Float
            msg.Write(aPositions[2]);           // Postion X2   |  Float
            msg.Write(aPositions[3]);           // Postion Y2   |  Float
            msg.Write((byte)0);
            _itemList.Add(_totalLootCounter, new LootItem(_totalLootCounter, LootType.Juices, WeaponType.NotWeapon, $"Health Juice-{aDrinkAmount}", 0, (byte)aDrinkAmount));
            return msg;
        }
        /// <summary>
        /// Creates a new Armor LootItem generation message to be sent out, and adds the new item to the loot list. This message MUST be sent.
        /// </summary>
        private NetOutgoingMessage MakeNewArmorLootItem(byte armorTicks, byte ArmorTier, float[] aPositions)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            _totalLootCounter++;              // Increase LootCounter by 1
            msg.Write((byte)20);                // Header       |  Byte
            msg.Write(_totalLootCounter);     // LootID       |  Int
            msg.Write((byte)LootType.Armor);    // LootType     |  Byte
            msg.Write((short)armorTicks);       // Info/Amount  |  Short
            msg.Write(aPositions[0]);           // Postion X1   |  Float
            msg.Write(aPositions[1]);           // Postion Y1   |  Float
            msg.Write(aPositions[2]);           // Postion X2   |  Float
            msg.Write(aPositions[3]);           // Postion Y2   |  Float
            msg.Write(ArmorTier);               // Rarity       |  Byte
            _itemList.Add(_totalLootCounter, new LootItem(_totalLootCounter, LootType.Armor, WeaponType.NotWeapon, $"Armor-Tier{ArmorTier}", ArmorTier, armorTicks));
            return msg;
        }

        /// <summary>
        /// Creates a new Tape LootItem generation message to be sent out, also adding the newly created item into the loot list. This message must be used.
        /// </summary>
        private NetOutgoingMessage MakeNewTapeLootItem(byte tapeAmount, float[] aPositions)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            _totalLootCounter++;              // Increase LootCounter by 1
            msg.Write((byte)20);                // Header       |  Byte
            msg.Write(_totalLootCounter);     // LootID       |  Int
            msg.Write((byte)LootType.Tape);     // LootType     |  Byte
            msg.Write((short)tapeAmount);       // Info/Amount  |  Short
            msg.Write(aPositions[0]);           // Postion X1   |  Float
            msg.Write(aPositions[1]);           // Postion Y1   |  Float
            msg.Write(aPositions[2]);           // Postion X2   |  Float
            msg.Write(aPositions[3]);           // Postion Y2   |  Float
            msg.Write((byte)0);
            _itemList.Add(_totalLootCounter, new LootItem(_totalLootCounter, LootType.Tape, WeaponType.NotWeapon, "Tape", 0, tapeAmount));
            return msg;
        }
        /// <summary>
        /// Creates a new Throwable LootItem generation message to be sent out, also adding the newly created item into the loot list. This message must be used.
        /// </summary>
        private NetOutgoingMessage MakeNewThrowableLootItem(short itemIndex, byte spawnCount, string name, float[] aPositions)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            _totalLootCounter++;              // Increase LootCounter by 1
            msg.Write((byte)20);                // Header              |  Byte
            msg.Write(_totalLootCounter);     // LootID              |  Int
            msg.Write((byte)LootType.Weapon);   // LootType            |  Byte
            msg.Write(itemIndex);               // Info/ Which Weapon  |  Short
            msg.Write(aPositions[0]);           // Postion X1          |  Float
            msg.Write(aPositions[1]);           // Postion Y1          |  Float
            msg.Write(aPositions[2]);           // Postion X2          |  Float
            msg.Write(aPositions[3]);           // Postion Y2          |  Float
            msg.Write(spawnCount);              // Spawn Amount        |  Byte
            _itemList.Add(_totalLootCounter, new LootItem(_totalLootCounter, LootType.Weapon, WeaponType.Throwable, name, 0, (byte)itemIndex, spawnCount));
            return msg;
        }
        /// <summary>
        /// Creates a new Gun LootItem generation message to be sent out, also adding the newly created item into the loot list. This message must be used.
        /// </summary>
        private NetOutgoingMessage MakeNewGunLootItem(string name, short weaponIndex, byte itemRarity, byte clipAmount, float[] aPositions)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            _totalLootCounter++;               // Increase LootCounter by 1
            msg.Write((byte)20);                 // Header              |  Byte
            msg.Write(_totalLootCounter);      // LootID              |  Int
            msg.Write((byte)LootType.Weapon);    // LootType            |  Byte
            msg.Write(weaponIndex);              // Info/ Which Weapon  |  Short
            msg.Write(aPositions[0]);            // Postion X1          |  Float
            msg.Write(aPositions[1]);            // Postion Y1          |  Float
            msg.Write(aPositions[2]);            // Postion X2          |  Float
            msg.Write(aPositions[3]);            // Postion Y2          |  Float
            msg.Write(clipAmount);               // Spawn Amount        |  Byte
            msg.Write(itemRarity.ToString());    // Spawn Amount        |  Byte
            _itemList.Add(_totalLootCounter, new LootItem(_totalLootCounter, LootType.Weapon, WeaponType.Gun, name, itemRarity, (byte)weaponIndex, clipAmount));
            return msg;
        }
        /// <summary>
        /// Checks if the provided Player isDrinking then if isTaping; forces any property found True to False AND sends STOP message.
        /// </summary>
        private void CheckDrinkTape(Player player)
        {
            if (player.isDrinking)
            {
                ServerAMSG_EndedDrinking(player.ID);
                player.isDrinking = false;
            }
            if (player.isTaping)
            {
                ServerAMSG_EndedTaping(player.ID);
                player.isTaping = false;
            }
        }
        /// <summary>
        /// Checks whether the given Player is currently taping, drinking, and or emoting; and stops any action that returns True.
        /// </summary>
        private void CheckMovementConflicts(Player player)
        {
            if (player.isDrinking)
            {
                ServerAMSG_EndedDrinking(player.ID);
                player.isDrinking = false;
            }
            if (player.isTaping)
            {
                ServerAMSG_EndedTaping(player.ID);
                player.isTaping = false;
            }
            if (player.isEmoting)
            {
                ServerEndEmoter(player.ID);
                player.isEmoting = false;
                player.EmoteID = -1;
                player.EmoteSpotX = -1;
                player.EmoteSpotY = -1;
            }
        }

        #region Level Data Loading Method
        /// <summary>
        /// Attempts to load all level-data related files and such to fill the server's own level-data related variables.
        /// </summary>
        private void LoadSARLevel() // This will become quite the mess.
        {
            Logger.Header("[LoadSARLevel] Attempting to Load SAR LevelData!");
            // Make sure this isn't called twice. Results in everything getting re-initialized. Which isn't awesome possum...
            if (svd_LevelLoaded) throw new Exception("LoadSARLevel has already been called. You cannot call this method multiple times.");
            //JSONNode
            if (!File.Exists(Directory.GetCurrentDirectory() + @"\datafiles\EarlyAccessMap1.txt"))
            {
                throw new Exception("Could not locate \"EarlyAccessMap1.txt\" in the \\datafiles folder!");
            }

            JSONNode LevelJSON = JSONNode.LoadFromFile(Directory.GetCurrentDirectory() + @"\datafiles\EarlyAccessMap1.txt");
            //Logger.Success("[LoadSARLevel] Successfully located LevelData file!");
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
            //Logger.Warn("[LoadSARLevel] Attempting to locate/read LootTiles...");
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
            //else
            //{
               // Logger.Warn("[LoadSARLevel] Checked lootSpawnsNoBot << usual emptiness.");
            //}
            //Logger.Success("[LoadSARLevel] Located LootTiles successfully.");

            #region LootItemList generation
            //Logger.Warn("[LoadSARLevel] Attempting to Generate _itemList...");
            _totalLootCounter = 0;
            MersenneTwister MerTwist = new MersenneTwister((uint)_lootSeed);
            _itemList = new Dictionary<int, LootItem>();
            int LootID;
            bool YesMakeBetter;
            uint MinGenValue;
            uint num;
            List<short> WeaponsToChooseByIndex = new List<short>();

            //for each weapon in the game/dataset, add each into a frequency list of all weapons by its-Frequency-amount-of-times
            // does that make sense?
            for (int i = 0; i < _weaponsList.Length; i++)
            {
                for (int j = 0; j < _weaponsList[i].SpawnFrequency; j++)
                {
                    //Logger.Basic($"My Frequency:Index -- {MyWeaponsList[i].SpawnFrequency}:{MyWeaponsList[i].JSONIndex}"); --remove but looks cool
                    WeaponsToChooseByIndex.Add(_weaponsList[i].JSONIndex);
                }
            }
            // Generate Loot \\
            //i < ( [Ammount of Regular Loot Spawns] + [Amount of Special Loot Spawns] + [Amount of 'no-bot' Loot Spawns] )
            int _total = _sNormal + _sGood + _sBot;
            //Logger.testmsg($"Total: {_total}\nNormal Tile Count: {_sNormal}\nGood Tile Count: {_sGood}\nBot Tile Count: {_sBot}\n");
            for (int i = 0; i < _total; i++)
            {
                //LootID++; -- > LootID++ after completing a loop. sorta.
                LootID = _totalLootCounter;
                _totalLootCounter++;
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
                        _itemList.Add(LootID, new LootItem(LootID, LootType.Juices, WeaponType.NotWeapon, $"Health Juice-{JuiceAmount}", 0, JuiceAmount));
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
                        _itemList.Add(LootID, new LootItem(LootID, LootType.Armor, WeaponType.NotWeapon, $"Armor-Tier{GenTier}", GenTier, GenTier)); // GiveAmount for armor is how many tick it has. gotta reuse stuff
                    }
                    else
                    {
                        if (num <= 60.0) // Skip ??
                        {
                        }
                        else if (num > 60.0 && num <= 66.0) // Tape
                        {
                            _itemList.Add(LootID, new LootItem(LootID, LootType.Tape, WeaponType.NotWeapon, "Tape", 0, 1));
                        }
                        else // Weapon Generation
                        {
                            short thisInd = WeaponsToChooseByIndex[(int)MerTwist.NextUInt(0U, (uint)WeaponsToChooseByIndex.Count)];
                            Weapon GeneratedWeapon = _weaponsList[thisInd]; // Any change to the WeaponList will have an impact on WeaponLoot generation
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
                                _itemList.Add(LootID, new LootItem(LootID, LootType.Weapon, WeaponType.Gun, GeneratedWeapon.Name, ItemRarity, (byte)GeneratedWeapon.JSONIndex, GeneratedWeapon.ClipSize));

                                // Spawn Ammo
                                for (int a = 0; a < 2; a++)
                                {
                                    LootID = _totalLootCounter;
                                    _totalLootCounter++;
                                    _itemList.Add(LootID, new LootItem(LootID, LootType.Ammo, WeaponType.NotWeapon, $"Ammo-Type{GeneratedWeapon.AmmoType}", GeneratedWeapon.AmmoType, GeneratedWeapon.AmmoSpawnAmount));
                                }
                            }
                            else if (GeneratedWeapon.WeaponType == WeaponType.Throwable)
                            {
                                _itemList.Add(LootID, new LootItem(LootID, LootType.Weapon, WeaponType.Throwable, GeneratedWeapon.Name, 0, (byte)GeneratedWeapon.JSONIndex, GeneratedWeapon.SpawnSizeOverworld));
                            }
                        }
                    }
                }
            }
            //Logger.Success("[LoadSARLevel] Loaded all LootItems successfully.");
            //Logger.Success($"Successfully generated the _itemList.Count:LootIDCount {_itemList.Keys.Count}:{LootID + 1}");
            //Logger.Success($"_itemList.Count:LootIDCount -- {_itemList.Keys.Count}:{_totalLootCounter}");
            #endregion // end of actual loot generation section
            #endregion

            // Load Coconuts
            #region Coconuts
            //Logger.Warn("[LoadSARLevel] Attempting to load/spawn Coconuts...");
            if (LevelJSON["coconuts"] == null || LevelJSON["coconuts"].Count < 1)
            {
                throw new Exception("It would seem that the \"coconuts\" is null in this data set or there are no entries.");
            }
            int cocoIndexID = 0;
            int cocoCount = LevelJSON["coconuts"].Count;
            _coconutList = new Dictionary<int, Coconut>(cocoCount);
            MersenneTwister cocoTwist = new MersenneTwister((uint)_coconutSeed);
            for (int i = 0; i < cocoCount; i++)
            {
                uint rng = cocoTwist.NextUInt(0U, 100U);
                if (rng > 65.0)
                {
                    _coconutList.Add(cocoIndexID, new Coconut(LevelJSON["coconuts"][i]["x"].AsFloat, LevelJSON["coconuts"][i]["y"].AsFloat));
                    cocoIndexID++;
                }
            }
            //Logger.Success("[LoadSARLevel] Successfully loaded in the Coconuts.");
            #endregion

            // Load Vehicles
            #region Hamsterballs
            // do listen to this commented-out line. There really is a key called "emus" in later versions of the game.
            //Logger.Header("[LoadSARLevel] Attempting to load hamsterballs list. (key = \"vehicles\"; not to be confused with \"emus\")");
            if (LevelJSON["vehicles"] == null || LevelJSON["vehicles"] == 0)
            {
                throw new Exception("Missing key \"vehicles\" in LevelJSON file or key \"vehicles\" has no entries.\nEither create this key or add entries. However, if this key doesn't exist you're probably doing something wrong.");
            }
            JSONNode vehicleNode = LevelJSON["vehicles"];
            MersenneTwister hampterTwist = new MersenneTwister((uint)_vehicleSeed);
            int hampterballs = vehicleNode.Count; // slightly faster to cache. maybe things changed
            int hTrueID = 0; // need to keep track so hampterballs actually spawn with the correct IDs
            _hamsterballList = new Dictionary<int, Hampterball>(hampterballs);
            for (int i = 0; i < hampterballs; i++)
            {
                if (hampterTwist.NextUInt(0U, 100U) > 55.0)
                {
                    _hamsterballList.Add(hTrueID, new Hampterball( (byte)3, (short)hTrueID, vehicleNode[i]["x"].AsFloat, vehicleNode[i]["y"].AsFloat) );
                    hTrueID++;
                }
            }
            //Logger.Success("[LoadSARLevel] Successfully generated the hamsterball list.");
            #endregion

            // Load Doodads
            #region Doodads
            if (LevelJSON["doodads"] == null || LevelJSON["doodads"] == 0) // does "doodads" key exist, does it actually have entries?
            {
                throw new Exception("Missing key \"doodads\" in LevelJSON file or key \"doodads\" has no entries.");
            }
            // time to read le list
            Dictionary<int, DoodadType> doodadTypeList = DoodadType.GetAllDoodadTypes(); // Load in DoodadTypes now that we know we can put them to use
            JSONNode doodadJSON = LevelJSON["doodads"]; // would technically be better to not have this, but spawning new Doodads gets messy
            int doodadCount = doodadJSON.Count;
            _doodadList = new List<Doodad>(doodadCount);
            for (int i = 0; i < doodadCount; i++)
            {
                if (!doodadTypeList.ContainsKey(doodadJSON[i]["i"].AsInt)){
                    Logger.Failure("FRICK");
                }
                if (doodadTypeList[doodadJSON[i]["i"].AsInt].Destructible)
                {
                    _doodadList.Add(new Doodad(doodadTypeList[doodadJSON[i]["i"].AsInt], doodadJSON[i]["x"].AsFloat, doodadJSON[i]["y"].AsFloat));
                }
            }
            //Logger.Success("[LoadSARLevel] Loaded all Doodads tagged with \"destructible\" successfully.");
            #endregion

            // -- Load MoleCrate SpawnSpots
            #region moleSpots
            if (LevelJSON["moleSpawns"] == null) throw new Exception("No such key \"moleSpawns\" in loaded LevelJSON.");
            _moleSpawnSpots = new Vector2[LevelJSON["moleSpawns"].Count];
            for (int i = 0; i < _moleSpawnSpots.Length; i++)
            {
                _moleSpawnSpots[i] = new Vector2(LevelJSON["moleSpawns"][i]["x"].AsFloat, LevelJSON["moleSpawns"][i]["y"].AsFloat);
            }
            Logger.DebugServer($"[LoadSARLevel] [OK] -- Loaded moleSpawns without error. Count: {_moleSpawnSpots.Length}");

            #endregion moleSpots
            // -- End of MoleCrate SpawnSpots

            // End of Loading in Files
            GC.Collect();
            svd_LevelLoaded = true; // Pretty obvious. just a little flag saying we indeed are finished with all this.
            Logger.Success("[LoadSARLevel] Finished without encountering any errors.");
        }
        #endregion


        #region player list methods
        /// <summary>
        /// Sorts the playerlist by moving null entries to the bottom. The returned list is not in sequential order (by ID).
        /// </summary>
        private void SortPlayerEntries() // TODO -- Maybe remove?
        {
            //Logger.Warn("[PlayerList Sort-Null] Attempting to sort the PlayerList...");
            if (isSorting)
            {
                Logger.Warn("Attempted to sort out nulls in playerlist while already sorting.\n");
                return;
            }
            isSorting = true;
            Player[] temp_plrlst = new Player[_playerList.Length];
            int newIndex = 0;
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null)
                {
                    temp_plrlst[newIndex] = _playerList[i];
                    newIndex++;
                }
            }
            _playerList = temp_plrlst;
            isSorting = false;
            isSorted = true;
            //Logger.Success("[PlayerList Sort-Null] Successfully sorted the PlayerList!");
        }

        /// <summary>
        /// Iterates over the entire Length of Match._playerList to find the count of all non-null entries.
        /// </summary>
        /// <returns>Int representing the amount of non-null Player objects in the PlayerList.</returns>
        private int GetValidPlayerCount() // Reminder: any instance where the code below was used can be replaced with calls to this method!
        {
            int count = 0;
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Traverses Match._playerList in search of a Player with a Name that matches the provided search name.
        /// </summary>
        /// <returns>True if searchName is found in the array; False if otherwise.</returns>
        private bool TryIDFronName(string searchName, out short outID) // UNUSED?
        {
            searchName = searchName.ToLower(); // This may bring up some problems?
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null && _playerList[i].Name.ToLower() == searchName) // Have to lowercase the comparison each time. Which really sucks man :(
                {
                    outID = _playerList[i].ID;
                    return true;
                }
            }
            outID = -1;
            return false;
        }

        /// <summary>
        /// Traverses Match._playerList in search of a Player with a Name that matches the provided search name.
        /// </summary>
        /// <returns>True if searchName is found; False if otherwise.</returns>
        private bool TryPlayerFromName(string searchName, out Player player)
        {
            searchName = searchName.ToLower(); // This may bring up some problems?
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null && _playerList[i].Name.ToLower() == searchName) // searchID is int, ID is short.
                {
                    player = _playerList[i];
                    return true;
                }
            }
            player = null;
            return false;
        }

        /// <summary>
        /// Traverses the player list array in search of the index which the provided Player ID is located.
        /// </summary>
        /// <returns>True if ID is found in the array; False if otherwise.</returns>
        private bool TryIndexFromID(int searchID, out int returnedIndex) // UNUSED?
        {
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null && _playerList[i].ID == searchID) //searchID is int, ID is short.
                {
                    returnedIndex = _playerList[i].ID;
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
        private bool TryPlayerFromID(int searchID, out Player returnedPlayer)
        {
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null && _playerList[i].ID == searchID) //searchID is int, ID is short.
                {
                    returnedPlayer = _playerList[i];
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
        private short GetIDFromUsername(string searchName) // UNUSED?
        {
            short retID = -1;
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null && _playerList[i].Name == searchName)
                {
                    retID = _playerList[i].ID;
                }
            }
            return retID;
        }

        /// <summary>
        /// Traverses the Server's Player list array in search of the Index at which the provided NetConnection occurrs.
        /// </summary>
        /// <returns>True if the NetConnection is found; False if otherwise.</returns>
        private bool TryIndexFromConnection(NetConnection netConnection, out int returnedIndex)
        {
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null && _playerList[i].Sender == netConnection) // searchID is int, ID is short.
                {
                    returnedIndex = _playerList[i].ID;
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
        private bool TryPlayerFromConnection(NetConnection netConnection, out Player player)
        {
            for (int i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null && _playerList[i].Sender == netConnection) // searchID is int, ID is short.
                {
                    player = _playerList[i];
                    return true;
                }
            }
            player = null;
            return false;
        }

        /// <summary>
        /// Grabs the ID of a player with the provided NetConnection. If a player is unable to be located "-1" is returned.
        /// </summary>
        private short getPlayerID(NetConnection thisSender) // PHASE OUT
        {
            short id = -1;
            for (byte i = 0; i < _playerList.Length; i++)
            {
                if (_playerList[i] != null)
                {
                    if (_playerList[i].Sender == thisSender)
                    {
                        id = _playerList[i].ID;
                        break;
                    }
                }
            }
            return id;
        }
        #endregion
    }
}