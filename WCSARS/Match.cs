using Lidgren.Network;
using SARStuff;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace WCSARS
{
    class Match
    {
        // Main Stuff...
        private string _serverKey;
        public NetServer server;
        private Player[] _players;
        //public Player[] PlayerList { get => _playerList; } // Unused as of now (12/23/22)
        private List<short> _availableIDs;
        private Dictionary<NetConnection, string> _incomingConnections;
        private TimeSpan DeltaTime;
        //private string _uuid;
        private string _gamemode = "solo";

        // IO PlayerData
        private JSONArray _playerData;
        private JSONArray _bannedPlayers;
        private JSONArray _bannedIPs;

        // Item-Like Data
        private Dictionary<int, LootItem> _lootItems;
        private Dictionary<int, Coconut> _coconutList;
        private Dictionary<int, Hamsterball> _hamsterballs;
        private Campfire[] _campfires;
        private List<Doodad> _doodadList;
        private Weapon[] _weapons = Weapon.GetAllWeaponsList();
        private byte[] _maxAmmo = new byte[] { 120, 30, 90, 25, 25 }; // smg, shotgun, ak, sniper, dart

        // UNSORTED
        //private int prevTimeA, matchTime; // TODO -- Phase these out
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
        private bool canSSGApproach;
        // -- Super Skunk Gas --

        // -- MoleCrate Crate Stuff --
        private Vector2[] _moleSpawnSpots; // Array containing every mole spawn-spot found while loading the level data in LoadSARLevel().
        private short _maxMoleCrates; // Maximum # of MoleCrates allowed in a match.
        private MoleCrate[] _moleCrates; // An array of MoleCrate objects which is the amount of active moles/crates available. The Length is that of Match._maxMoleCrates.
        // -- MoleCrate Crate Stuff --

        // -- Healing Values --
        private float _healPerTick = 4.75f; // 4.75 health/drinkies every 0.5s according to the SAR wiki 7/21/22
        private float _healRateSeconds = 0.5f; // 0.5s
        //private byte _tapePerCheck; // Add when can config file
        private float _coconutHeal = 5f;
        private float _campfireHealPer = 4f;
        private float _campfireHealRateSeconds = 1f;

        // -- Dartgun-Related things --
        private int _ddgMaxTicks = 12; // DDG max amount of Damage ticks someone can get stuck with
        private int _ddgAddTicks = 4; // the amount of DDG ticks to add with each DDG shot
        private float _ddgTickRateSeconds = 0.6f; // the rate at which the server will attempt to make a DDG DamageTick check
        private int _ddgDamagePerTick = 9;
        private List<Player> _poisonDamageQueue; // List of PlayerIDs who're taking skunk damage > for cough sound -- 12/2/22
        //public List<Player> PoisonDamageQueue { get => _poisonDamageQueue; } // Added / unused as of now (12/23/22)
        
        // -- Level / RNG-Related --
        private int _totalLootCounter, _lootSeed, _coconutSeed, _vehicleSeed; // Spawnable Item Generation Seeds
        //private MersenneTwister _servRNG = new MersenneTwister((uint)DateTime.UtcNow.Ticks);
        private bool svd_LevelLoaded = false; // Likely able to remove this without any problems

        //mmmmmmmmmmmmmmmmmmmmmmmmmmmmm (unsure section right now
        private bool _canCheckWins = false;
        private bool _hasPlayerDied = true;
        private bool _safeMode = true; // This is currently only used by the /gun ""command"" so you can generate guns with abnormal rarities
        private const int MS_PER_TICK = 41; // (1000ms / 24t/s == 41)

        public Match(int port, string ip) // Original default constructor
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
            _players = new Player[64]; // Default
            _availableIDs = new List<short>(_players.Length); // So whenever playerList is changed don't have to constantly change here...
            for (short i = 0; i < _players.Length; i++) _availableIDs.Add(i);
            _incomingConnections = new Dictionary<NetConnection, string>(4);

            isSorting = false;
            isSorted = true;

            // MoleCrate Crate fields...
            _maxMoleCrates = 12;
            _moleCrates = new MoleCrate[_maxMoleCrates];

            //TODO - finish settings this up at some point
            _poisonDamageQueue = new List<Player>(32);

            _lobbyRemainingSeconds = 90.00;

            // Load JSON Arrays
            Logger.Warn("[Match] Loading player-data.json...");
            string baseloc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            _playerData = LoadJSONArray(baseloc + @"\player-data.json");
            Logger.Warn("[Match] Loading banned-players.json...");
            _bannedPlayers = LoadJSONArray(baseloc + @"\banned-players.json");
            Logger.Warn("[Match] Loading banned-ips.json...");
            _bannedIPs = LoadJSONArray(baseloc + @"\banned-ips.json");
            Logger.Success("[Match] Loaded player data and banlists!");

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

        public Match(ConfigLoader cfg) // Match but it uses ConfigLoader (EW!)
        {
            Logger.Header("[Match] ConfigLoader Match creator used!");
            // Initialize PlayerStuff
            _serverKey = cfg.ServerKey;
            _players = new Player[cfg.MaxPlayers];
            _availableIDs = new List<short>(_players.Length);
            _incomingConnections = new Dictionary<NetConnection, string>(4);
            _poisonDamageQueue = new List<Player>(32);
            for (short i = 0; i < _players.Length; i++) _availableIDs.Add(i);

            // Load json files
            Logger.Basic("[Match] Loading player-data.json...");
            string baseloc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            _playerData = LoadJSONArray(baseloc + @"\player-data.json");
            Logger.Basic("[Match] Loading banned-players.json...");
            _bannedPlayers = LoadJSONArray(baseloc + @"\banned-players.json");
            Logger.Basic("[Match] Loading banned-ips.json...");
            _bannedIPs = LoadJSONArray(baseloc + @"\banned-ips.json");
            Logger.Success("[Match] Loaded player data and ban lists!");

            // Set healing values
            _healPerTick = cfg.HealthPerTick;
            _healRateSeconds = cfg.DrinkTickRate;
            _coconutHeal = cfg.CoconutHealAmount;
            _campfireHealPer = cfg.CampfireHealPerTick;
            _campfireHealRateSeconds = cfg.CampfireRateSeconds;

            // Set Dartgun stuff -- TBH could just use the stats found in the Dartgun...
            _ddgMaxTicks = cfg.MaxDartTicks;
            _ddgTickRateSeconds = cfg.DartTickRate;
            _ddgDamagePerTick = cfg.DartPoisonDamage;

            // TODO: SkunkGas-DamageRate-Seconds

            // Others
            _safeMode = cfg.Safemode;
            isSorting = false;
            isSorted = true;
            _maxMoleCrates = cfg.MaxMoleCrates;
            _moleCrates = new MoleCrate[_maxMoleCrates];
            _lobbyRemainingSeconds = cfg.MaxLobbyTime;
            _gamemode = cfg.Gamemode;

            // Initialize LootGenSeeds / call LoadSARLevel()
            if (cfg.useConfigSeeds)
            {
                _lootSeed = cfg.LootSeed;
                _coconutSeed = cfg.CocoSeed;
                _vehicleSeed = cfg.HampterSeed;
            }
            else
            {
                MersenneTwister twistItUp = new MersenneTwister((uint)DateTime.UtcNow.Ticks);
                _lootSeed = (int)twistItUp.NextUInt(0u, uint.MaxValue);
                _coconutSeed = (int)twistItUp.NextUInt(0u, uint.MaxValue);
                _vehicleSeed = (int)twistItUp.NextUInt(0u, uint.MaxValue); 
            }
            Logger.Warn($"[Match] Using Seeds: LootSeed: {_lootSeed}; CoconutSeed: {_coconutSeed}; HampterSeed: {_vehicleSeed}");
            LoadSARLevel();

            // Initialize NetServer
            Logger.Basic($"[Match] Attempting to start server on \"{cfg.IP}:{cfg.Port}\".");
            Thread updateThread = new Thread(ServerUpdateLoop);
            Thread netThread = new Thread(ServerNetLoop);
            NetPeerConfiguration config = new NetPeerConfiguration("BR2D");
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);  // Reminder to not remove this
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);        // Reminder to not remove this
            config.PingInterval = 22f;
            config.LocalAddress = System.Net.IPAddress.Parse(cfg.IP);
            config.Port = cfg.Port;
            server = new NetServer(config);
            server.Start();
            netThread.Start();
            updateThread.Start();
            Logger.Header("[Match] Match created without encountering any errors.");
        }

        /// <summary>
        /// Handles all NetIncomingMessages sent to this Match's server. Continuously runs until this Match.server is no longer running.
        /// </summary>
        private void ServerNetLoop()
        {
            // Make sure no invalid....
            Logger.Basic("[Match.ServerNetLoop] Network thread started!");
            // Loop to handle any recieved message from the NetServer... Stops when the server is no longer in the running state.
            NetIncomingMessage msg;
            while (IsServerRunning())
            {
                //Logger.DebugServer($"[{DateTime.UtcNow}] Waiting to receive message.");
                server.MessageReceivedEvent.WaitOne(5000); // Halt this thread until the NetServer receives a message. Then continue
                //Logger.DebugServer($"[{DateTime.UtcNow}] Message has been received.");
                while ((msg = server?.ReadMessage()) != null)
                {
                    switch (msg.MessageType)
                    {
                        case NetIncomingMessageType.Data:
                            HandleMessage(msg);
                            break;
                        case NetIncomingMessageType.StatusChanged:
                            switch (msg.SenderConnection.Status)
                            {
                                case NetConnectionStatus.Connected:
                                    Logger.Success($"[NCS.Connected] New Client connected! Wahoo! Sender Address: {msg.SenderConnection}");
                                    NetOutgoingMessage acceptMsgg = server.CreateMessage();
                                    acceptMsgg.Write((byte)0);
                                    acceptMsgg.Write(true);
                                    server.SendMessage(acceptMsgg, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
                                    break;
                                case NetConnectionStatus.Disconnected:
                                    HandleClientDisconnect(msg);
                                    Logger.Warn($"[NCS.Disconnected] Client GoodbyeMsg: {msg.ReadString()}");
                                    break;
                            }
                            break;
                        case NetIncomingMessageType.ConnectionApproval: // MessageType.ConnectionApproval MUST be enabled to work
                            Logger.Header("[Connection Approval] A new connection is awaiting approval!");
                            // Make sure this person isn't IP-banned...
                            bool allowConnection = true;
                            string ip = msg.SenderEndPoint.Address.ToString();
                            Logger.DebugServer($"IP Address: {ip}");
                            for (int i = 0; i < _bannedIPs.Count; i++)
                            {
                                if (_bannedIPs[i]["ip"] == ip)
                                {
                                    Logger.DebugServer($"[ServerAuthentiate] [WARN] Player @ {msg.SenderEndPoint} is IP-banned. Dropping connection.");
                                    string reason = "No reason provided.";
                                    if (_bannedIPs[i]["reason"] != null && _bannedIPs[i]["reason"] != "") reason = _bannedIPs[i]["reason"];
                                    msg.SenderConnection.Deny($"\nYou're banned from this server.\n\"{reason}\"");
                                    allowConnection = false;
                                }
                            }
                            if (!allowConnection) break; // probs better way than this...

                            // Player likely isn't banned
                            string clientKey = msg.ReadString();
                            Logger.Basic($"[Connection Approval] Incoming connection {msg.SenderEndPoint} sent key: {clientKey}");
                            if (clientKey == _serverKey) // Update to use ServerKey 1/2/23; ServerKey found in ConfigLoader
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
                                if (TryPlayerFromConnection(msg.SenderConnection, out Player pinger)) pinger.LastPingTime = pingTime;
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
                    server?.Recycle(msg);
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
            Logger.Basic("[Match.ServerUpdateLoop] Update thread started!");
            if (!_safeMode) Logger.Warn("[Match] Warning! Safemode is set to FALSE! Crazy stuff might happen!!");
            if (!_canCheckWins) Logger.Warn("[Match] Warning! Match will NOT check for wins! Use \"/togglewin\" in-game to re-enable");

            DateTime nextTick = DateTime.UtcNow;
            DateTime lastDateTime = DateTime.UtcNow; // Used for DeltaTime calculation

            // Lobby | Not in-progress yet
            while (IsServerRunning() && !_hasMatchStarted)
            {
                while (nextTick < DateTime.UtcNow && IsServerRunning() && !_hasMatchStarted)
                {
                    // Calculate DeltaTime
                    DeltaTime = DateTime.UtcNow - lastDateTime;
                    lastDateTime = DateTime.UtcNow;

                    // PlayerList stuff / Matchfull
                    if (!isSorted) SortPlayerEntries();
                    int numOfPlayers = GetValidPlayerCount();
                    if (!_isMatchFull && numOfPlayers == _players.Length) _isMatchFull = true;
                    else if (_isMatchFull && numOfPlayers != _players.Length) _isMatchFull = false;

                    // General Lobby stuff...
                    SendDummyMessage(); // Ping!
                    SendCurrentPlayerPings(); // Pong!
                    if (GetValidPlayerCount() > 0) UpdateLobbyCountdown();
                    SendLobbyPlayerPositions();
                    CheckCampfiresLobby();
                    // TODO: probs Gallery Targets somewhere here as well

                    // Because /commands are a thing
                    CheckMoleCrates();
                    UpdatePlayerDataChanges();
                    UpdateDownedPlayers();

                    // For the "tick" system
                    nextTick = nextTick.AddMilliseconds(MS_PER_TICK);
                    if (nextTick > DateTime.UtcNow) Thread.Sleep(nextTick - DateTime.UtcNow);
                }
            }
            ResetForRoundStart();

            // Match | In-Progress Currently
            while (_hasMatchStarted && IsServerRunning())
            {
                while (nextTick < DateTime.UtcNow && IsServerRunning())
                {
                    DeltaTime = DateTime.UtcNow - lastDateTime;
                    lastDateTime = DateTime.UtcNow;

                    if (!isSorted) SortPlayerEntries();
                    SendDummyMessage();
                    SendCurrentPlayerPings();

                    // Check Wins
                    if (_hasPlayerDied && _canCheckWins) svu_checkForWinnerWinnerChickenDinner();

                    // Match Player Updates
                    SendMatchPlayerPositions();
                    UpdatePlayerDataChanges();
                    UpdatePlayerDrinking();
                    UpdatePlayerTaping();
                    UpdatePlayerEmotes();
                    UpdateDownedPlayers();
                    check_DDGTicks(); // STILL TESTING -- (as of: 12/2/22)
                    svu_CheckCoughs(); // NEW TEST FROM 12/2/22 UPDATE

                    //advanceTimeAndEventCheck();

                    // SSG
                    UpdateSSGWarningTime();
                    UpdateSafezoneRadius();
                    CheckSkunkGas();

                    // Others
                    CheckMoleCrates();
                    CheckCampfiresMatch();

                    // For the "tick" system
                    nextTick = nextTick.AddMilliseconds(MS_PER_TICK);
                    if (nextTick > DateTime.UtcNow) Thread.Sleep(nextTick - DateTime.UtcNow);
                }
            }
            // The End
            Logger.DebugServer($"[{DateTime.UtcNow}] [ServerUpdateLoop] Match.server no longer running? I stop too... Byebye!");
        }
        
        private void UpdateLobbyCountdown() // Update LobbyCountdown; Sends MatchStart once countdown reaches zero.
        {
            if (!IsServerRunning()) return;
            _lobbyRemainingSeconds -= DeltaTime.TotalSeconds;
            SendCurrentLobbyCountdown(_lobbyRemainingSeconds);
            if (_lobbyRemainingSeconds <= 0)
            {
                SendMatchStart();
                _hasMatchStarted = true;
            }
        }

        private void UpdateSSGWarningTime()
        {
            if (!isSkunkGasWarningActive || !IsServerRunning()) return;
            SkunkGasWarningDuration -= (float)DeltaTime.TotalSeconds;
            if (SkunkGasWarningDuration <= 0)
            {
                SendSSGApproachEvent(SkunkGasRemainingApproachTime);
                isSkunkGasWarningActive = false;
                isSkunkGasActive = true;
                canSSGApproach = true;
            }
        }

        private void UpdatePlayerDataChanges() // Sends Msg45 | Intended to ONLY be sent when necessary; but we spam lol
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)45);
            msg.Write((byte)GetValidPlayerCount());
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null)
                {
                    msg.Write(_players[i].ID);
                    msg.Write(_players[i].HP);
                    msg.Write(_players[i].ArmorTier);
                    msg.Write(_players[i].ArmorTapes);
                    msg.Write(_players[i].WalkMode);
                    msg.Write(_players[i].Drinkies);
                    msg.Write(_players[i].Tapies);
                }
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        private void UpdatePlayerDrinking() // Appears OK
        {
            if (!IsServerRunning()) return;
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null || !_players[i].isDrinking || _players[i].NextHealTime > DateTime.UtcNow) continue;
                // Make sure they're not stuck in an infinite loop of drinking.
                if (_players[i].Drinkies == 0 || _players[i].HP >= 100)
                {
                    SendPlayerEndDrink(_players[i]);
                    continue;
                }
                // Heal Section
                float hp = _healPerTick;
                if ((hp + _players[i].HP) > 100) hp = (100 - _players[i].HP);
                if ((_players[i].Drinkies - hp) < 0) hp = _players[i].Drinkies;
                _players[i].HP += (byte)hp;
                _players[i].Drinkies -= (byte)hp;
                _players[i].NextHealTime = DateTime.UtcNow.AddSeconds(_healRateSeconds);
                if (_players[i].HP >= 100 || _players[i].Drinkies == 0) SendPlayerEndDrink(_players[i]);
            }
        }

        private void UpdatePlayerTaping() // Appears OK
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null || !_players[i].isTaping) continue;
                if (DateTime.UtcNow > _players[i].NextTapeTime) // isTaping *should* ONLY get set if tape-checks pass.
                {
                    _players[i].Tapies -= 1;
                    _players[i].ArmorTapes += 1;
                    SendPlayerEndTape(_players[i]);
                }
            }
        }

        private void UpdatePlayerEmotes() // Appears OK
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null || !_players[i].isEmoting) continue;
                else if (DateTime.UtcNow > _players[i].EmoteEndTime) SendPlayerEndedEmoting(_players[i]);
            }
        }

        private void UpdateDownedPlayers() // Appears OK
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null || !_players[i].isAlive || !_players[i].isDown) continue;
                if (_players[i].isBeingRevived && (DateTime.UtcNow > _players[i].ReviveTime)) RevivePlayer(_players[i]);
                else if (!_players[i].isBeingRevived && (DateTime.UtcNow >= _players[i].NextDownDamageTick))
                {
                    _players[i].NextDownDamageTick = DateTime.UtcNow.AddSeconds(1);
                    test_damagePlayer(_players[i], 2 + (2 * _players[i].TimesDowned), _players[i].LastAttackerID, _players[i].LastWeaponID);
                    // Please see: https://animalroyale.fandom.com/wiki/Downed_state
                }
            }
        }

        /// <summary>
        /// Revives the provided player utilizing the information stored within their fields.
        /// </summary>
        /// <param name="player">Player to revive.</param>
        private void RevivePlayer(Player player)
        {
            SendTeammateRevived(player.ReviverID, player.ID);
            player.isDown = false;
            player.ReviverID = -1;
            player.HP = 10;
            player.WalkMode = 1;
        }

        private void svu_checkForWinnerWinnerChickenDinner()
        {
            _hasPlayerDied = false;
            List<short> aIDs = new List<short>(_players.Length);
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null && _players[i].isAlive)
                {
                    aIDs.Add(_players[i].ID);
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
        
        //can be simplified with gasCheck -- TODO: Make real
        /*private void advanceTimeAndEventCheck()
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
        }*/

        private void ResetForRoundStart()
        {
            if (!IsServerRunning()) return;
            // Reset SSG stuff-- shouldn't happen normally, but /commands are fun to use
            SkunkGasTotalApproachDuration = 5.0f;
            SkunkGasRemainingApproachTime = 5.0f;
            SkunkGasWarningDuration = 5.0f;
            canSSGApproach = false;
            isSkunkGasActive = false;
            isSkunkGasWarningActive = false;

            // Reset Player fields and junk
            Logger.Warn("[ResetForRoundStart] Resetting Player fields...");
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null) continue;
                if (!_players[i].isReady)
                {
                    Logger.Basic($"[ResetForRoundStart] Player {_players[i].Name} ({_players[i].ID}) wasn't ready!");
                    _players[i].Sender.Disconnect("Didn't receive ready in time! D:");
                    continue;
                }
                // Giant Eagle stuff
                _players[i].hasLanded = false;
                // TODO: Reset PlayerPosition to the origin point of the Giant Eagle

                // Attack / Weapons
                _players[i].AttackCount = -1;
                _players[i].Projectiles = new Dictionary<short, Projectile>();
            }
            Logger.Basic("[ResetForRoundStart] Reset all required Player fields!");
        }

        private void CheckMoleCrates()
        {
            // Make sure there are actually any current moles waiting to do anything
            if (_moleCrates == null || _moleCrates[0] == null) return; // The OR is fine actually, because Molecrates aren't removed once finished
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
        /// (MATCH) Iterates over Match._campfires, checking the status of all entries. Updates lighting up; healing players; etc. Nulls-out a used campfire.
        /// </summary>
        private void CheckCampfiresMatch()
        {
            for (int i = 0; i < _campfires.Length; i++) // After a Campfire is used up, it its index in the array is nulled-out.
            {
                if (_campfires[i] == null) continue;
                // Get campfire; figure out if it's lit and needs countdown
                Campfire campfire = _campfires[i];
                if (campfire.isLit)
                {
                    if (campfire.UseRemainder > 0) campfire.UseRemainder -= (float)DeltaTime.TotalSeconds;
                    else
                    {
                        _campfires[i] = null; // Make the entry not real so we don't have to ever check it again. Sowwy...
                        continue; // Leave this iteration to not execute below stuff. Perhaps can refactor to not have to do this though?
                    }
                }
                // Player stuff- Figure out if a Player is close enough to light it up or get healed by it.
                for (int j = 0; j < _players.Length; j++)
                {
                    if (_players[j] == null) continue;
                    Player player = _players[j];
                    if (player.IsPlayerReal() && (player.HP < 100) && Vector2.ValidDistance(campfire.Position, player.Position, 24f, true))
                    {
                        if (!campfire.isLit)
                        {
                            Logger.DebugServer($"Saw player at campfire near {campfire.Position.x}, {campfire.Position.y}");
                            NetOutgoingMessage camplight = server.CreateMessage();
                            camplight.Write((byte)50); // MSG ID -- 50
                            camplight.Write((byte)i);  // byte | CampfireID ( I == this campfire's ID)
                            server.SendToAll(camplight, NetDeliveryMethod.ReliableUnordered);
                            campfire.isLit = true;
                        }
                        else if (player.NextCampfireTime < DateTime.UtcNow)
                        {
                            float healHP = _campfireHealPer; // Default = 4hp every 1 second
                            if ((player.HP + healHP) > 100) healHP = 100 - player.HP;
                            player.HP += (byte)healHP;
                            player.NextCampfireTime = DateTime.UtcNow.AddSeconds(_campfireHealRateSeconds); // Default = 1 second / 1f
                        }
                    }
                }
            }
        }

        /// <summary>
        /// (LOBBY) Iterates over Match._campfires, checking to see if Players are close enough to any Campfires in order to light them up.
        /// </summary>
        private void CheckCampfiresLobby()
        {
            for (int i = 0; i < _campfires.Length; i++) // Does NOT null campfires once used. Lobby campfires are infinite-use.
            {
                if (_campfires[i] != null && !_campfires[i].hasBeenUsed)
                {
                    for (int j = 0; j < _players.Length; j++)
                    {
                        if (_players[j] == null) continue;
                        if (Vector2.ValidDistance(_campfires[i].Position, _players[j].Position, 24f, true))
                        {
                            _campfires[i].hasBeenUsed = true;
                            NetOutgoingMessage lobbyLight = server.CreateMessage();
                            lobbyLight.Write((byte)89);
                            lobbyLight.Write((byte)i);
                            server.SendToAll(lobbyLight, NetDeliveryMethod.ReliableUnordered);
                        }
                    }
                }
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
            isSkunkGasWarningActive = true;
            canSSGApproach = false;
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

        private void CheckSkunkGas() // TODO:: try doing correct amount of gas damage
        {
            if (!isSkunkGasActive || !IsServerRunning()) return;
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null || !_players[i].IsPlayerReal() || _players[i].NextGasTime > DateTime.UtcNow) continue;
                Vector2 deltaMag = _players[i].Position - new Vector2(CurrentSafezoneX, CurrentSafezoneY);
                if (deltaMag.magnitude >= CurrentSafezoneRadius && !_players[i].isGodmode)
                {
                    _players[i].NextGasTime = DateTime.UtcNow.AddSeconds(1.0d);
                    if (_players[i].hasBeenInGas)
                    {
                        if (!_poisonDamageQueue.Contains(_players[i])) _poisonDamageQueue.Add(_players[i]);
                        test_damagePlayer(_players[i], 1, -2, -1);
                    }
                    _players[i].hasBeenInGas = true;
                }
                else _players[i].hasBeenInGas = false;
            }
        }

        // Resizes the current Safezone radius to match the target "END" safezone radius
        private void UpdateSafezoneRadius() // Appears OK
        {
            if (!canSSGApproach || !IsServerRunning()) return;
            if (SkunkGasRemainingApproachTime > 0.0f)
            {
                // Update approachment time
                SkunkGasRemainingApproachTime -= (float)DeltaTime.TotalSeconds;
                if (SkunkGasRemainingApproachTime < 0.0f) SkunkGasRemainingApproachTime = 0.0f;
                // Translate the Circle over using new approachment time
                float scew = (SkunkGasTotalApproachDuration - SkunkGasRemainingApproachTime) / SkunkGasTotalApproachDuration;
                CurrentSafezoneRadius = (LastSafezoneRadius * (1.0f - scew)) + (EndSafezoneRadius * scew);
                CurrentSafezoneX = (LastSafezoneX * (1.0f - scew)) + (EndSafezoneX * scew);
                CurrentSafezoneY = (LastSafezoneY * (1.0f - scew)) + (EndSafezoneY * scew);
            }
            else canSSGApproach = false;
        }

        private void check_DDGTicks() // Still messing with this from time to time...
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null && _players[i].DartTicks > 0)
                {
                    if (_players[i].DartNextTime <= DateTime.UtcNow)
                    {
                        if ((_players[i].DartTicks - 1) >= 0)
                        {
                            _players[i].DartTicks -= 1;
                        }
                        _players[i].DartNextTime = DateTime.UtcNow.AddMilliseconds(_ddgTickRateSeconds * 1000);
                        test_damagePlayer(_players[i], _ddgDamagePerTick, _players[i].LastAttackerID, _players[i].LastWeaponID);
                        if (!_poisonDamageQueue.Contains(_players[i]))
                        {
                            _poisonDamageQueue.Add(_players[i]);
                        }
                    }
                }
            }
        }
        #endregion

        // So the original plan was for HandleMessage to run asynchronously (no spellcheck!); but that didn't really work out.
        // So async HandleMessage has been postponed indefinitely. Sowwy for disappointment 
        // If anyone *really* wants async HandleMessage, then go for it! But just know it'll probably be really hard D:
        private void HandleMessage(NetIncomingMessage msg)
        {
            byte b = msg.ReadByte();
            Player player; // Used in the TryFindPlayers
            switch (b)
            {
                    // Msg1 -- Authentication Request >>> Msg2 -- Authentication Response [Confirm / Deny]
                case 1: // OK enough... would like improvements
                    Logger.Header($"[Authentication Request] {msg.SenderEndPoint} is sending an authentication request!");
                    HandleAuthenticationRequest(msg);
                    break;

                    // Msg3 -- Client Ready & Send Characters >>> Msg4 -- Confirm ready / send match info
                case 3: // OK for now -- would like to improve | WARNING: game modification required for "SteamName"
                    Logger.Header($"Sender {msg.SenderEndPoint}'s Ready Received. Now reading character data.");
                    HandleIncomingPlayerRequest(msg);
                    break;

                case 5:
                    Logger.Header($"<< Ready-Request @ {msg.SenderEndPoint} received! >>");
                    HandleReadyReceived(msg);
                    Logger.Basic($"<< Ready Confirmed for {msg.SenderEndPoint}. >>");
                    break;

                    // Msg7 -- Request GiantEagle Ejection >>> Msg8 -- SendForcePosition(land: true)
                case 7: // Appears OK
                    HandleEjectRequest(msg);
                    break;

                    // Msg14 -- Client Position Update >>> No further messages; although these use the data: Msg11 / Msg12:: LobbyPositionUpdate / MatchPositionUpdate
                case 14: // Appears OK
                    HandlePositionUpdate(msg);
                    break;

                case 16: // Cleanup / Other Improvements needed
                    try
                    {
                        HandleAttackRequest(msg);
                    } catch (Exception ex)
                    {
                        Logger.Failure($"[Player Attack] [ERROR] {ex}");
                    }
                    break;

                    // Msg18 -- Confirm Attack >> Msg19 -- Confirmed Attack
                case 18:
                    HandleAttackConfirm(msg); // Cleanup / Other Improvements needed
                    break;

                    // Msg21 -- Client Request Loot --> Msg22 -- Server Confirm Loot  +(optional) Msg20 -- Server Sent Spawn Loot
                case 21:
                    if (_hasMatchStarted) HandleLootRequestMatch(msg);
                    else ServerHandleLobbyLootRequest(msg);
                    break;

                    // Msg25 -- Client Sent Chat Message --> I no write anymore
                case 25:
                    serverHandleChatMessage(msg);
                    break;

                    // Msg27 -- Client SlotUpdate >>> Msg28 -- Server SlotUpdate
                case 27: // Appears OK
                    HandleSlotUpdate(msg);
                    break;

                    // Msg29 -- Client Request Reload --> Msg30 -- Server Confirm Reload Request
                case 29:
                    HandleReloadRequest(msg);
                    break;

                    // Msg92 -- Client Request Finish Reload --> Msg 93 -- Server Confirm Reload Finished
                case 92: // ToDo: See if there's something else to do for reload ignore bug; Future TODO: See if cancel reload ignoring is still a thing
                    HandleReloadFinished(msg);
                    break;

                    // Msg32 -- Client Done Landing! --> Msg??? -- Server Confirm Landing -- Just check if positions are right and force if not
                case 32:
                    Logger.missingHandle("Message appears to be missing handle. ID: 32");
                    try
                    {
                        if (TryPlayerFromConnection(msg.SenderConnection, out Player p)) p.hasLanded = true;
                        else // TODO -- Handle this
                        {
                            Logger.Failure($"Couldn't find player");
                            msg.SenderConnection.Disconnect($"LOL YOU'RE NOT REAL YOU'RE NOT REAL!!");
                        }
                    } catch (NetException netEx) // TODO -- FINISH ERROR HANDLE
                    {
                        Logger.Failure($"Error processing landing data!\n{netEx}");
                    }
                    break;

                    // Msg36 -- Client Request Throwable Start --> Msg37 -- Server Confirm Throw Request
                case 36:
                    {
                        if (TryPlayerFromConnection(msg.SenderConnection, out player))
                        {
                            try
                            {
                                if (!player.IsPlayerReal()) return;
                                short grenadeID = msg.ReadInt16();
                                if (player.LootItems[2].WeaponIndex == grenadeID || player.LastThrowable == grenadeID)
                                {
                                    player.LastThrowable = grenadeID;
                                    NetOutgoingMessage throwStart = server.CreateMessage();
                                    throwStart.Write((byte)37);
                                    throwStart.Write(player.ID);
                                    throwStart.Write(grenadeID);
                                    server.SendToAll(throwStart, NetDeliveryMethod.ReliableOrdered);
                                }
                                else
                                {
                                    Logger.Failure($"[ThrowableInitiateReq] Player @ {msg.SenderConnection} does not have throwable \"{grenadeID}\" in SlotIndex 2.");
                                    msg.SenderConnection.Disconnect("There was an error processing your request. Message: \"Throwable not in slot.\"");
                                }
                            }
                            catch (NetException netEx)
                            {
                                Logger.Failure($"[ThrowableInitiateReq] Player @ {msg.SenderConnection} gave NetException!\n{netEx}");
                                msg.SenderConnection.Disconnect("Error processing your request. Message: \"No data in sent packet.\"");
                            }
                        }
                        else
                        {
                            Logger.Failure($"[ThrowableInitiateReq] Player @ {msg.SenderConnection} is not in the PlayerList!");
                            msg.SenderConnection.Disconnect("There was an error processing your request. Message: \"Invalid Action! Not in PlayerList!\"");
                        }
                    }
                    break;

                    // Msg38 -- Client Request REALLY Start Throwing --> Msg39 -- Server Confirm REALLY Start Throwing
                case 38: // Still need to make throwables actually do stuff. 
                    {
                        if (TryPlayerFromConnection(msg.SenderConnection, out player))
                        {
                            if (player.LootItems[2].WeaponType != WeaponType.Throwable) return;
                            if (!player.IsPlayerReal()) return;
                            CheckMovementConflicts(player);
                            try
                            {
                                float spawnX = msg.ReadFloat();
                                float spawnY = msg.ReadFloat();
                                float apexX = msg.ReadFloat();
                                float apexY = msg.ReadFloat();
                                float targetX = msg.ReadFloat();
                                float targetY = msg.ReadFloat();
                                short grenadeID = msg.ReadInt16();
                                if (player.LootItems[2].WeaponIndex == grenadeID)
                                {
                                    if ((player.LootItems[2].GiveAmount - 1) <= 0) player.LootItems[2] = new LootItem(LootType.Collectable, "NONE", 0, 0, new Vector2(0, 0));
                                    else player.LootItems[2].GiveAmount -= 1;
                                    player.ThrowableCounter++;
                                    NetOutgoingMessage throwmsg = server.CreateMessage();
                                    throwmsg.Write((byte)39);
                                    throwmsg.Write(player.ID);
                                    throwmsg.Write(spawnX);
                                    throwmsg.Write(spawnY);
                                    throwmsg.Write(apexX);
                                    throwmsg.Write(apexY);
                                    throwmsg.Write(targetX);
                                    throwmsg.Write(targetY);
                                    throwmsg.Write(grenadeID);
                                    throwmsg.Write(player.ThrowableCounter);
                                    server.SendToAll(throwmsg, NetDeliveryMethod.ReliableOrdered);
                                }
                                else
                                {
                                    Logger.Failure($"[ThrowableStartingReq] Player @ {msg.SenderConnection} does not have throwable \"{grenadeID}\" in SlotIndex 2.");
                                    msg.SenderConnection.Disconnect("There was an error processing your request. Message: \"Throwable not in slot.\"");
                                }
                            }
                            catch (NetException netEx)
                            {
                                Logger.Failure($"[ThrowableStartingReq] Player @ {msg.SenderConnection} gave NetException!\n{netEx}");
                                msg.SenderConnection.Disconnect("Error processing your request. Message: \"Error reading packet data.\"");
                            }
                        }
                        else
                        {
                            Logger.Failure($"[ThrowableStartingeReq] Player @ {msg.SenderConnection} is not in the PlayerList!");
                            msg.SenderConnection.Disconnect("There was an error processing your request. Message: \"Invalid Action! Not in PlayerList!\"");
                        }
                    }
                    break;

                    // Msg40 -- Client Throwable Landed --> Msg41 -- Server Confirm Throwable Landed
                case 40: // Still need to make throwables actually do stuff other than get taken away
                    {
                        if (TryPlayerFromConnection(msg.SenderConnection, out player))
                        {
                            try
                            {
                                float x = msg.ReadFloat();
                                float y = msg.ReadFloat();
                                float height = msg.ReadFloat();
                                short ID = msg.ReadInt16();
                                //Logger.Warn($"Grenade Height: {height}\nGrenadeID: {ID}");
                                //Logger.Warn($"Player ThrowableCount: {player.ThrowableCounter}");

                                NetOutgoingMessage fragout = server.CreateMessage();
                                fragout.Write((byte)41);    // Header
                                fragout.Write(player.ID);   // Short | PlayerID << who sent the grenade out
                                fragout.Write(ID);          // Short | GrenadeID
                                fragout.Write(x);           // Float | Grenade.X
                                fragout.Write(y);           // Float | Grenade.Y
                                fragout.Write(height);      // Float | Grenade.Height
                                fragout.Write((byte)0);     // Byte  | # of HitPlayers
                                                            //fragout.Write(playerID)   // Short | HitPlayerID
                                server.SendToAll(fragout, NetDeliveryMethod.ReliableOrdered);
                            }
                            catch (NetException netEx)
                            {
                                Logger.Failure($"[ThrowableFinishedReq] Player @ {msg.SenderConnection} gave NetException!\n{netEx}");
                                msg.SenderConnection.Disconnect("Error processing your request. Message: \"Error reading packet data.\"");
                            }
                        }
                        else
                        {
                            Logger.Failure($"[ThrowableFinishedReq] Player @ {msg.SenderConnection} is not in the PlayerList!");
                            msg.SenderConnection.Disconnect("There was an error processing your request. Message: \"Invalid Action! Not in PlayerList!\"");
                        }
                    }
                    break;

                case 44: // Client - Request Current Spectator // TODO-- Implement
                    /* msg.ReadFloat() // cam X
                     * msg.ReadFloat() // cam Y
                     * msg.ReadInt16() // player id (who they're watching)
                     */
                    //
                    break;

                    // Msg47 -- Initiate Healing Request >>> Msg48 -- Player Initiated Healing
                case 47: // Appears OK
                    HandleHealingRequest(msg);
                    break;

                case 51: // Client - I'm Requesting Coconut Eaten
                    HandleCoconutRequest(msg);
                    break;

                case 53: // Client - Sent Cutgras
                    serverSendCutGrass(msg);
                    break;

                    // Msg55 -- Enter Hamsterball Request >>> Msg56 -- Confirm Hamsterball Enter | OK: v0.90.2
                case 55: // Appears OK
                    HandleHamsterballEnter(msg);
                    break;

                    // Msg57 -- Exit Hamsterball Request >>> Msg58 Confirm Hamsterball Exit | OK: v0.90.2
                case 57: // Appears OK
                    HandleHamsterballExit(msg);
                    break;

                    // Msg60 -- Hamsterball HitPlayer Request >>> Msg61 -- Confirm Hamsterball HitPlayer | OK: v0.90.2
                case 60: // Appears OK
                    HandleHamsterballAttack(msg);
                    break;

                    // Msg62 -- Request Hamsterball Bounce >>> Msg63 -- Confirm Hamsterball Bounce | OK: v0.90.2
                case 62: // Appears OK
                    HandleHamsterballBounce(msg);
                    break;

                    // Msg64 -- Damage Hamsterball Request >>> Msg65 Hamsterball Damaged Confirm | OK: v0.90.2
                case 64: // Appears OK
                    HandleHamsterballHit(msg);
                    break;

                    // Msg66 -- Request Emote Begin >>> Msg67 -- Player Emote Begin | OK: v0.90.2
                case 66: // TODO:: Valid EmoteID; EmotePosition not too far off.
                    HandleEmoteRequest(msg);
                    break;

                    // TODO -- Cleanup
                case 70: // Molecrate Open Request v0.90.2
                    try // Honestly, think nestesd ifs would've been better bc this is kind of hard to read. Perhaps cleanup
                    {
                        if (!_hasMatchStarted) return; // Don't bother doing anything if the match hasn't started.
                        // Is this connection actually a loaded-in Player?
                        if (!TryPlayerFromConnection(msg.SenderConnection, out player))
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
                        float distance = (_moleCrates[molecrateID].Position - player.Position).magnitude;
                        //Logger.DebugServer($"Distance: {distance}"); // distance seems to be ~10 in any direction; but diag ~14-15
                        if (distance <= 14.7f) // WHOOOAA-- MY GAAH- WHY-Y=YYY ARE YOU SOOO~ SILLY~ (before it was checking if distance was greater not within)
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

                    // TODO -- Cleanup
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


                case 80:
                    HandleTeammatePickupRequest(msg);
                    break;

                //
                case 85:
                    HandleMapMarked(msg);
                    break;

                    // TODO -- Cleanup
                case 87: // Literally no clue what this one is because its so unspeciifc. Guessing this has something to do with grenades bc "GID"
                    try // TODO -- Redo this whole thing like oh my goodness is this so silly
                    {
                        if (TryPlayerFromConnection(msg.SenderConnection, out player))
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

                    // Msg90 -- Client Request Reload Cancel --> Msg91 -- Server Confirm Reload Canceled 
                case 90: // If Player is a Good Noodle, they send this before server has to check. If they're NOT a Good Noodle, they don't :[
                    if (VerifyPlayer(msg.SenderConnection, "ReloadCancelRequest", out player)) SendCancelReload(player);
                    break;

                    // Msg97 -- Dummy >> Send back another dummy (Msg99)
                case 97: // Appears OK
                    SendDummyMessage();
                    break;

                    // Msg98 -- StartTapingRequest >> Msg99 -- ConfirmStartTaping
                case 98: // Why the big jump from 48 to 98 ;-;; | Appears OKthough
                    HandleTapeRequest(msg);
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
        /// Sends an Authentication Response packet to sender of this NetMessage. If the sent PlayFabID is in the banlist, then the connection is dropped.
        /// </summary>
        private void HandleAuthenticationRequest(NetIncomingMessage msg) // Receive PT 1 >> Send PacketType 2 | Likely can be improved upon!
        {
            // -- SAR v0.90.2 Format --
            // String | PlayFabID
            // String | AnalyticsSessionTicket (empty if it doesn't exist)
            // Bool   | FillYayorNay
            // String | PlayFab AuthTicket
                // -- (Foreach PartyMember) -- 
                    // Byte   | PartyCount
                    // String | PartyMemberN.PlayFabID
            try
            {
                string playFabID = msg.ReadString();
                /*string uAnalyticsSessionTicket = msg.ReadString(); // If you want it, just uncomment. Works fine v0.90.2
                bool fill = msg.ReadBoolean();
                string playFabSessionTicket = msg.ReadString();
                int partyCount = msg.ReadByte();
                string[] partyPlayFabIDs = null;
                if (partyCount > 0)
                {
                    partyPlayFabIDs = new string[partyCount];
                    for (int i = 0; i < partyCount; i++)
                    {
                        partyPlayFabIDs[i] = msg.ReadString();
                    }
                }*/
                Logger.DebugServer($"[HandleAuthenticationRequest] Incoming PlayFabID: {playFabID}");
                // See if this client's PlayFabID is in the banlist.
                for (int i = 0; i < _bannedPlayers.Count; i++)
                {
                    if (_bannedPlayers[i]["playfabid"] == playFabID)
                    {
                        Logger.Warn($"[ServerAuthentiate] [WARN] Player {playFabID} @ {msg.SenderEndPoint} is banned. Dropping connection.");
                        string reason = "No reason provided.";
                        if (_bannedPlayers[i]["reason"] != null && _bannedPlayers[i]["reason"] != "") reason = _bannedPlayers[i]["reason"];
                        msg.SenderConnection.Disconnect($"\nYou're banned from this server.\n\"{reason}\"");
                        return;
                    }
                }
                // If not in the banlist, let's let them in!
                if (!_incomingConnections.ContainsKey(msg.SenderConnection))
                {
                    _incomingConnections.Add(msg.SenderConnection, playFabID);
                }
                NetOutgoingMessage acceptMsg = server.CreateMessage();
                acceptMsg.Write((byte)2);
                acceptMsg.Write(true);
                server.SendMessage(acceptMsg, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
                Logger.Success($"[ServerAuthentiate] [OK] Sent {msg.SenderConnection} an accept message!");
            } catch (NetException netEx)
            {
                Logger.Failure($"{msg.SenderConnection} gave a NetException!\n{netEx}");
                msg.SenderConnection.Disconnect($"There was an error while reading your packet data.");
            }
        }

        private void HandleIncomingPlayerRequest(NetIncomingMessage msg) // Msg3 >> Msg4
        {
            if (TryPlayerFromConnection(msg.SenderConnection, out Player p))
            {
                Logger.Failure($"[HandleIncomingPlayerRequest] [Error] There already exists a Player object with incoming connection {msg.SenderConnection}!");
                return;
            }
            try
            {
                // Read Sent Character Data
                string steamName = msg.ReadString();
                short animal = msg.ReadInt16();
                short umbrella = msg.ReadInt16();
                short gravestone = msg.ReadInt16();
                short deathEffect = msg.ReadInt16();
                short[] emoteIDs = { msg.ReadInt16(), msg.ReadInt16(), msg.ReadInt16(), msg.ReadInt16(), msg.ReadInt16(), msg.ReadInt16() }; // v0.90.2 | OK
                short hat = msg.ReadInt16();
                short glasses = msg.ReadInt16();
                short beard = msg.ReadInt16();
                short clothes = msg.ReadInt16();
                short melee = msg.ReadInt16();
                byte gsCount = msg.ReadByte();
                short[] gsGunIDs = new short[gsCount];
                byte[] gsSkinIndicies = new byte[gsCount];
                for (int index = 0; index < gsCount; index++)
                {
                    gsGunIDs[index] = msg.ReadInt16();
                    gsSkinIndicies[index] = msg.ReadByte();
                }
                // --- end of data read v0.90.2
                SortPlayerEntries();
                for (int i = 0; i < _players.Length; i++)
                {
                    if (_players[i] != null) continue;
                    short assignID = _availableIDs[0];
                    _availableIDs.RemoveAt(0);
                    Player newPlayer = new Player(assignID, animal, umbrella, gravestone, deathEffect, emoteIDs, hat, glasses, beard, clothes, melee,
                        gsCount, gsGunIDs, gsSkinIndicies, steamName, msg.SenderConnection);
                    _players[i] = newPlayer;

                    // is playfab id in dataset? || TODO:: separate method + add to dataset if not.
                    string iPlayFabID = _incomingConnections[msg.SenderConnection];
                    Logger.DebugServer("Incoming PlayFabID: " + iPlayFabID);
                    int playerDataCount = _playerData.Count; // For whatever reason, it is slightly faster to cache the length of lists than to call Count
                    for (int j = 0; j < playerDataCount; j++)
                    {
                        if ((_playerData[j]["playfabid"] != null) && (_playerData[j]["playfabid"] == iPlayFabID))
                        {
                            if (_playerData[j]["dev"]) _players[i].isDev = _playerData[j]["dev"];
                            if (_playerData[j]["mod"]) _players[i].isMod = _playerData[j]["mod"];
                            if (_playerData[j]["founder"]) _players[i].isFounder = _playerData[j]["founder"];
                            break;
                        }
                    }
                    // Teammates | TODO: Fills
                    if (_gamemode != "solo")
                    {
                        int lim = 0;
                        if (_gamemode == "duo") lim = 1;
                        else if (_gamemode == "squad") lim = 3;
                        
                        // Find Players Without Teammates 
                        for (int x = 0; x < _players.Length; x++)
                        {
                            // Null? At limit? FoundPlayer is ThisPlayer? FoundPlayer & ThisPlayer already teammates? 
                            if (_players[x] == null || _players[x].Teammates.Count == lim || _players[x].Equals(_players[i]) || _players[i].Teammates.Contains(_players[x])) continue;
                            _players[i].Teammates.Add(_players[x]);
                            _players[x].Teammates.Add(_players[i]);
                        }
                    }

                    // end of playfabChecj
                    SendMatchInformation(msg.SenderConnection, assignID); // We're done. They can continue connecting now.
                    break;
                }
            } catch (NetException netEx)
            {
                Logger.Failure($"[HandleIncomingPlayerRequest] [Error] {msg.SenderConnection} caused a NetException!\n{netEx}");
                msg.SenderConnection.Disconnect("There was an error reading your packet data. [HandleIncomingConnection]");
            }
        }

        /// <summary>
        /// Sends the provided NetConnection a NetOutgoingMessage that contians all the information required in order for them to load into the match
        /// </summary>
        private void SendMatchInformation(NetConnection client, short assignedID) // Msg4
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)4);                 // Byte   |  MessageID 
            msg.Write(assignedID);              // Short  |  AssignedID
            // Send RNG Seeds
            msg.Write(_lootSeed);               // Int  |  LootGenSeed
            msg.Write(_coconutSeed);            // Int  |  CocoGenSeed
            msg.Write(_vehicleSeed);            // Int  |  VehicleGenSeed
            // Match / Lobby Info...
            msg.Write(_lobbyRemainingSeconds);  // Double  |  LobbyTimeRemaining
            msg.Write("yerhAGJ");               // String  |  MatchUUID  // TODO-- Unique IDs
            msg.Write(_gamemode);               // String  |  Gamemode [solo, duo, squad]
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

        private void HandleReadyReceived(NetIncomingMessage pmsg) // Working v0.90.2 - ???
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleReadyReceived", out Player player))
            {
                SortPlayerEntries();
                NetOutgoingMessage msg = server.CreateMessage();
                msg.Write((byte)10);                    // Byte | MsgID (10)
                msg.Write((byte)GetValidPlayerCount()); // Byte | AmountOfIterations
                for (int i = 0; i < _players.Length; i++)
                {
                    if (_players[i] == null) continue;
                    msg.Write(_players[i].ID);               // Short   | PlayerID
                    msg.Write(_players[i].AnimalID);         // Short   | CharacterID
                    msg.Write(_players[i].UmbrellaID);       // Short   | UmbrellaID
                    msg.Write(_players[i].GravestoneID);     // Short   | GravestoneID
                    msg.Write(_players[i].DeathExplosionID); // Short   | DeathExplosionID
                    for (int j = 0; j < 6; j++)                 // Short[] | PlayerEmotes: Always 6 in v0.90.2
                    {
                        msg.Write(_players[i].EmoteIDs[j]);  // Short   | EmoteID[i]
                    }
                    msg.Write(_players[i].HatID);            // Short   | HatID
                    msg.Write(_players[i].GlassesID);        // Short   | GlassesID
                    msg.Write(_players[i].BeardID);          // Short   | BeardID
                    msg.Write(_players[i].ClothesID);        // Short   | ClothesID
                    msg.Write(_players[i].MeleeID);          // Short   | MeleeID
                    msg.Write(_players[i].GunSkinCount);     // Byte    | AmountOfGunSkins
                    for (int k = 0; k < _players[i].GunSkinKeys.Length; k++)
                    {
                        msg.Write(_players[i].GunSkinKeys[k]);    // Short | GunSkinKey[i]
                        msg.Write(_players[i].GunSkinValues[k]);  // Byte  | GunSkinValue[i]
                    }
                    msg.Write(_players[i].Position.x);   // Float  | PositionX
                    msg.Write(_players[i].Position.y);   // Float  | PositionY
                    msg.Write(_players[i].Name);         // String | Username
                    msg.Write(_players[i].EmoteID);      // Short  | CurrentEmote (emote players will still dance when joining up)
                    msg.Write((short)_players[i].LootItems[0].WeaponIndex);  // Short | Slot1 WeaponID -- TODO: Equiping items in Lobby should be real?
                    msg.Write((short)_players[i].LootItems[1].WeaponIndex);  // Short | Slot2 WeaponID
                    msg.Write(_players[i].LootItems[0].Rarity);              // Byte  | Slot1 WeaponRarity
                    msg.Write(_players[i].LootItems[1].Rarity);              // Byte  | Slot1 WeaponRarity
                    msg.Write(_players[i].ActiveSlot);                       // Byte  | ActiveSlotID
                    msg.Write(_players[i].isDev);                            // Bool  | IsDeveloper
                    msg.Write(_players[i].isMod);                            // Bool  | IsModerator
                    msg.Write(_players[i].isFounder);                        // Bool  | IsFounder
                    msg.Write((short)1000);                                  // Short | PlayerLevel
                    if (_gamemode == "solo") msg.Write((byte)0);             // Byte  | Teammate Count
                    else // NO support for SvR / Mystery Mode / Bwocking Dead
                    {
                        msg.Write((byte)(_players[i].Teammates.Count+1));   // Byte  | # o' Teammates << self counted
                        msg.Write(_players[i].ID);                          // Short | TeammateID << self counted
                        for (int w = 0; w < _players[i].Teammates.Count; w++) msg.Write(_players[i].Teammates[w].ID);
                    }
                }
                server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
                player.isReady = true;
            }
        }

        /// <summary>Handles a NetMessage marked as a "EjectRequest" packet (Msg7).</summary>
        /// <param name="pmsg">NetMessage to read the packet data from.</param>
        private void HandleEjectRequest(NetIncomingMessage pmsg) // Msg7
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleEjectRequest", out Player player))
            {
                // TODO:: When Giant Eagle is real, check this again.
                if (!player.IsPlayerReal()) SendForcePosition(player, player.Position, true);
                else Logger.Failure($"[HandleEjectRequest] Player  @ {pmsg.SenderConnection} tried ejecting but they're already real!");
            }
        }

        /// <summary>
        /// Sends a NetMessage to all NetPeers which forces the provided Player to the specified position; with the provided parachute mode.
        /// </summary>
        /// <param name="player">Player who is getting force-moved.</param>
        /// <param name="moveToPosition">Position to move this player to.</param>
        /// <param name="isParachute">Whether the player should parachute.</param>
        private void SendForcePosition(Player player, Vector2 moveToPosition, bool isParachute) // Msg8
        {
            // Set server-side stuff
            player.Position = moveToPosition;
            if (isParachute) player.hasLanded = false;
            // Send Message out
            NetOutgoingMessage msg = server.CreateMessage(22); // 22 == Allocate 22 bytes; should really use this more
            msg.Write((byte)8);          // Byte  | MsgID (8)  | 1?
            msg.Write(player.ID);        // Short | PlayerID   | 4
            msg.Write(moveToPosition.x); // Float | PositionX  | 8
            msg.Write(moveToPosition.y); // Float | PositionY  | 8
            msg.Write(isParachute);      // Bool  | Parachute? | 1?
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        // Is only used while in Lobby.
        private void SendLobbyPlayerPositions() // Msg11
        {
            if (!IsServerRunning()) return;
            // Make message sending player data. Loops entire list but only sends non-null entries.
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)11);                    // Byte | MsgID (11)
            msg.Write((byte)GetValidPlayerCount()); // Byte | # of Iterations
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null) continue;
                msg.Write(_players[i].ID);                                              // Short | PlayerID
                msg.Write((sbyte)((180f * _players[i].MouseAngle / 3.141592f) / 2));    // sbyte  | LookAngle
                msg.Write((ushort)(_players[i].Position.x * 6f));                       // ushort | PositionX 
                msg.Write((ushort)(_players[i].Position.y * 6f));                       // ushort | PositionY
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        // Only used while match is in progress.
        private void SendMatchPlayerPositions() // Msg12
        {
            if (!IsServerRunning()) return;
            // Make message sending player data. Loops entire list but only sends non-null entries.
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)12);                    // Byte | Header
            msg.Write((byte)GetValidPlayerCount()); // Byte | Count of valid entries the Client is receiving/iterating over
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null)
                {
                    msg.Write(_players[i].ID);
                    msg.Write((short)(180f * _players[i].MouseAngle / 3.141592f));
                    msg.Write((ushort)(_players[i].Position.x * 6f));
                    msg.Write((ushort)(_players[i].Position.y * 6f));
                    if (_players[i].WalkMode == 4) // Determine whether or not the player is in a vehicle
                    {
                        msg.Write(true);
                        //msg.Write((short)(_playerList[i].PositionX * 10f)); // Add these lines back for some funny stuff
                        //msg.Write((short)(_playerList[i].PositionY * 10f)); // Correct usage is to use VehiclePositions NOT PlayerPositions
                        msg.Write((short)(_players[i].HamsterballVelocity.x * 10f));
                        msg.Write((short)(_players[i].HamsterballVelocity.y * 10f));
                    }
                    else msg.Write(false);
                }
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        /// <summary>Handles a NetMessage marked as a "PositionUpdate" packet (Msg14).</summary>
        /// <param name="pmsg">NetMessage to read the packet data from.</param>
        private void HandlePositionUpdate(NetIncomingMessage pmsg) // Msg14 | TODO -- make sure player isn't bouncing around the whole map
        {
            if (!IsServerRunning()) return;
            if (VerifyPlayer(pmsg.SenderConnection, "HandlePositionUpdate", out Player player))
            {
                if (!player.isAlive) return; // No, this isn't called when a player is dead. That's Msg44 silly. Go there!
                try
                {
                    // Read Values
                    float mouseAngle = pmsg.ReadInt16() / 57.295776f;
                    float posX = pmsg.ReadFloat();
                    float posY = pmsg.ReadFloat();
                    byte walkMode = pmsg.ReadByte(); // v0.90.2 OK:: do later versions use this still?
                    if (walkMode > 5)
                    {
                        Logger.missingHandle($"Unhandled walkmode? Mode#: {walkMode}");
                        walkMode = 1;
                    }
                    // Downed Player Movement-Mode::
                    if (player.isDown && walkMode != 5) walkMode = 5;
                    else if (!player.isDown && walkMode == 5)
                    {
                        Logger.DebugServer($"Player {player.Name} keeps saying they're down but they ain't. what an idiot, am I right?");
                        walkMode = 1;
                    }

                    // Try setting some values
                    player.Position = new Vector2(posX, posY);
                    //Vector2 sentPostion = new Vector2(posX, posY); // too buggy right now. do when giant eagle exists
                    //if (Vector2.ValidDistance(sentPostion, player.Position, 10f, true)) player.Position = sentPostion;
                    //else SendForcePosition(player, player.Position, false);
                    player.MouseAngle = mouseAngle;
                    player.WalkMode = walkMode;

                    // For "special" Movement-Modes
                    if (walkMode == 2)
                    {
                        CheckMovementConflicts(player);
                        if (player.isReloading) SendCancelReload(player);
                    }
                    if (walkMode == 4 && player.VehicleID != -1)
                    {
                        float vehicleX = (float)(pmsg.ReadInt16() / 10f);
                        float vehicleY = (float)(pmsg.ReadInt16() / 10f);
                        player.HamsterballVelocity = new Vector2(vehicleX, vehicleY);
                    }
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandlePositionUpdate] Player @ {pmsg.SenderConnection} caused a NetException!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("There was an error while reading your packet data! (HandlePositionUpdate)");
                }
            }
        }

        private void HandlePlayerDied(Player player)
        {
            player.isAlive = false;
            SendPlayerDeath(player.ID, player.Position, player.LastAttackerID, player.LastWeaponID);
            if (player?.Teammates.Count > 0)
            {
                Logger.Warn("hey so yeah this passes correctly. cool, right?");
                for (int i = 0; i < player.Teammates.Count; i++)
                {
                    player.Teammates[i].isAlive = false;
                    SendPlayerDeath(player.Teammates[i].ID, player.Teammates[i].Position, player.Teammates[i].LastAttackerID, player.Teammates[i].LastWeaponID);
                }
            }
            _hasPlayerDied = true;
        }

        private void SendPlayerDeath(short playerID, Vector2 gravespot, short killerID, short weaponID)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)15);    // Byte  | MsgID (15)
            msg.Write(playerID);    // Short | Dying PlayerID
            msg.Write(gravespot.x); // Float | GraveSpawnX
            msg.Write(gravespot.y); // Float | GraveSpawnY
            msg.Write(killerID);    // Short | SourceID
            msg.Write(weaponID);    // Short | WeaponID
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
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
        /// Handles an incoming message marked as a "PlayerAttackRequest" packet. If in match, Players are kicked for invalid actions.
        /// </summary>
        /// <param name="amsg">Incoming message to handle.</param>
        private void HandleAttackRequest(NetIncomingMessage amsg)
        {
            if (TryPlayerFromConnection(amsg.SenderConnection, out Player player))
            {
                if (!player.IsPlayerReal()) return;
                CheckMovementConflicts(player);
                if (player.isReloading) SendCancelReload(player);
                try
                {
                    // Read WeaponID / SlotID
                    short weaponID = amsg.ReadInt16();
                    byte slotID = amsg.ReadByte();
                    // Now, if the sent slot isn't the Melee slot; then figure out if its valid.
                    Logger.DebugServer($"Sent SlotID: {slotID}");
                    Logger.DebugServer($"AttackHandle Ammo NOW: {player.LootItems[slotID].GiveAmount}");
                    if (_hasMatchStarted && slotID != 2) // If not 2, then *should be a weapon*; If it isn't
                    {
                        if (!player.IsGunAndSlotValid(weaponID, slotID))
                        {
                            Logger.Failure($"[Server Handle - AttackRequest] Player @ {amsg.SenderConnection} sent invalid wepaonID / slot.");
                            amsg.SenderConnection.Disconnect("There was an error processing your request. Message: Action Invalid! Weapon / Slot mis-match!");
                            return;
                        }
                        else if (-1 >= (player.LootItems[slotID].GiveAmount - 1))
                        {
                            Logger.Failure($"[Server Handle - AttackRequest] Player @ {amsg.SenderConnection} gun shots go into negatives. May be a mis-match.");
                            amsg.SenderConnection.Disconnect("There was an error processing your request. Message: Action Invalid! Shot-count mis-match!");
                            return;
                        }
                        else player.LootItems[slotID].GiveAmount -= 1;
                    }
                    // Continue Reading Values
                    float shotAngle = amsg.ReadInt16() / 57.295776f;
                    float spawnX = amsg.ReadFloat();
                    float spawnY = amsg.ReadFloat();
                    bool isValid = amsg.ReadBoolean();
                    bool hitDestructible = amsg.ReadBoolean();
                    if (hitDestructible) serverDoodadDestroy(amsg.ReadInt16(), amsg.ReadInt16());
                    // Make sure AttackIDs line up
                    short attackID = amsg.ReadInt16();
                    player.AttackCount++; // Added with Lance Test
                    Logger.DebugServer($"AttackID: {attackID}; Plr.AttackCount: {player.AttackCount}");
                    if (player.AttackCount != attackID)
                    {
                        Logger.Failure($"[HandleAttackRequest] Player @ {amsg.SenderConnection} Attack count mis-aligned.");
                        amsg.SenderConnection.Disconnect("There was an error processing your request. Message: Attack Count mis-match!");
                        return;
                    }
                    // If the attack counts line up, again- Continue reading
                    int projectileCount = amsg.ReadByte(); // Reminder that when sending this back it must be a byte
                    if (projectileCount < 0)
                    {
                        Logger.Failure($"[Server Handle - AttackRequest] Player @ {amsg.SenderConnection} sent invalid projectile angle count \"{projectileCount}\".");
                        amsg.SenderConnection.Disconnect($"There was an error processing your request. Message: Invalid projectile count \"{projectileCount}\"");
                    }
                    float[] projectileAngles = new float[projectileCount];
                    short[] projectileIDs = new short[projectileCount];
                    bool[] validProjectiles = new bool[projectileCount];
                    // If ProjectileAngleCount is 0, then it was a Melee attack.
                    if (projectileCount > 0)
                    {
                        for (int i = 0; i < projectileCount; i++)
                        {
                            projectileAngles[i] = amsg.ReadInt16() / 57.295776f;
                            projectileIDs[i] = amsg.ReadInt16();
                            validProjectiles[i] = amsg.ReadBoolean();
                            if (player.Projectiles.ContainsKey(projectileIDs[i]))
                            {
                                Logger.Failure($"[HandleAttackRequest] Key \"{projectileIDs[i]}\" already exists in Player projectile list!");
                                return;
                            }
                            else
                            {
                                Projectile spawnProj = new Projectile(weaponID, player.LootItems[slotID].Rarity, spawnX, spawnY, projectileAngles[i]);
                                player.Projectiles.Add(projectileIDs[i], spawnProj);
                            }
                        }
                    }
                    // Send Message back to everyone with shot info...
                    NetOutgoingMessage pmsg = server.CreateMessage();
                    pmsg.Write((byte)17);
                    pmsg.Write(player.ID);
                    pmsg.Write((ushort)(player.LastPingTime * 1000f));
                    pmsg.Write(weaponID);
                    pmsg.Write(slotID);
                    pmsg.Write(attackID);
                    pmsg.Write((short)(3.1415927f / shotAngle * 180f));
                    pmsg.Write(spawnX);
                    pmsg.Write(spawnY);
                    pmsg.Write(isValid);
                    pmsg.Write((byte)projectileCount);
                    if (projectileCount > 0)
                    {
                        for (int i = 0; i < projectileCount; i++)
                        {
                            pmsg.Write((short)(projectileAngles[i] / 3.1415927f * 180f));
                            pmsg.Write(projectileIDs[i]);
                            pmsg.Write(validProjectiles[i]);

                        }
                    }
                    server.SendToAll(pmsg, NetDeliveryMethod.ReliableSequenced);

                } catch (NetException netEx)
                {
                    Logger.Failure($"[HandleAttackRequest] Player @ NetConnection \"{amsg.SenderConnection}\" gave NetError!\n{netEx}");
                    amsg.SenderConnection.Disconnect("There was an error procssing your request. Message: Read past buffer size...");
                }
            }
            else
            {
                Logger.Failure($"[ServerHandle - AttackRequest] Could not locate Player @ NetConnection \"{amsg.SenderConnection}\"; Connection has been dropped.");
                amsg.SenderConnection.Disconnect("There was an error processing your request. Message: Action Invalid! Not in PlayerList!");
            }
        }

        /* // Msg17 - Confirm/Send PlayerAttack
        private void SendPlayerAttack(short playerID, float lastPing, short weaponID, byte slotID, short attackID, float aimAngle, float spawnX, float spawnY, bool isValid, float[] projectileAngles, short[] projectileIDs, bool[] validProjectiles)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)17);                                // Byte   | MsgID (17)
            msg.Write(playerID);                                // Short  | PlayerID
            msg.Write((ushort)(lastPing * 1000f));              // uShort | Ping
            msg.Write(weaponID);                                // Short  | WeaponID / WeaponIndex
            msg.Write(slotID);                                  // Byte   | Slot (slot this weapon's in)
            msg.Write(attackID);                                // Short  | AttackID
            msg.Write((short)(3.1415927f / aimAngle * 180f));   // Short  | Aim-Angle (so stupid...)
            msg.Write(spawnX);                                  // Float  | SpawnX
            msg.Write(spawnY);                                  // Float  | SpawnY
            msg.Write(isValid);                                 // Bool   | isValid
            msg.Write((byte)projectileIDs.Length);              // Byte   | Amount of Projectiles
            for (int i = 0; i < projectileIDs.Length; i++)
            {
                msg.Write((short)(projectileAngles[i] / 3.1415927f * 180f));    // Short | Angle of Projectile
                msg.Write(projectileIDs[i]);                                    // Short | Projectile ID
                msg.Write(validProjectiles[i]);                                 // Bool  | isThisProjectileValid ?
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }*/

        // Msg18 - Request AttackConfirm >>> Msg19 - AttackConfirmed
        private void HandleAttackConfirm(NetIncomingMessage pmsg) // TOOD -- Make sure shot should've hit + damage falloff
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleAttackConfirm", out Player player))
            {
                if (!player.IsPlayerReal()) return;
                try
                {
                    short targetID = pmsg.ReadInt16();
                    short weaponID = pmsg.ReadInt16();
                    short projectileID = pmsg.ReadInt16();
                    float hitX = pmsg.ReadFloat();
                    float hitY = pmsg.ReadFloat();
                    // Basic Checks
                    if (!ValidWeaponIndex(weaponID))
                    {
                        Logger.Failure($"[HandleAttackConfirm] Player @ NetConnection \"{pmsg.SenderConnection}\" sent invalid WeaponID \"{weaponID}\"");
                        pmsg.SenderConnection.Disconnect($"There was an error processing your request. WeaponID \"{weaponID}\" is OoB.");
                        return;
                    }
                    if (!TryPlayerFromID(targetID, out Player target))
                    {
                        Logger.Failure($"[HandleAttackConfirm] Player @ NetConnection \"{pmsg.SenderConnection}\" sent invalid PlayerID \"{targetID}\"");
                        pmsg.SenderConnection.Disconnect($"There was an error processing your request. Player \"{targetID}\" does not exist.");
                        return;
                    }
                    if (!target.IsPlayerReal() || (bool)player.Teammates?.Contains(target)) return;
                    if (!_hasMatchStarted || target.isGodmode)
                    {
                        SendConfirmAttack(player.ID, target.ID, projectileID, 0, 0, 0);
                        return;
                    }
                    // Match in Progress; Target wasn't god-mode'd
                    if (!player.IsValidProjectileID(projectileID))
                    {
                        Logger.Failure($"[HandleAttackConfirm] Player @ {pmsg.SenderConnection} sent invalid ProjectileID \"{projectileID}\"");
                        pmsg.SenderConnection.Disconnect($"There was an error processing your request. ProjectileID \"{projectileID}\" not found.");
                        return;
                    }
                    if (projectileID >= 0 && (player.Projectiles[projectileID].WeaponID != weaponID))
                    {
                        Logger.Failure($"[HandleAttackConfirm] Player @ {pmsg.SenderConnection} sent WeaponID does not match found ProjectileID!");
                        pmsg.SenderConnection.Disconnect($"There was an error processing your request. WeaponID does not match SentProjectileID.");
                        return;
                    }
                    // Do Damage / Real Confirming
                    Weapon weapon = _weapons[weaponID];
                    if (target.VehicleID >= 0) // Target in Hamsterball ?
                    {
                        if (!_hamsterballs.ContainsKey(target.VehicleID)) return;
                        Hamsterball hamsterball = _hamsterballs[target.VehicleID];
                        int ballDamage = 1;
                        if (weapon.VehicleDamageOverride > 0) ballDamage = weapon.VehicleDamageOverride;
                        if ((hamsterball.HP - ballDamage) < 0) ballDamage = hamsterball.HP;
                        hamsterball.HP -= (byte)ballDamage;
                        SendConfirmAttack(player.ID, target.ID, projectileID, 0, hamsterball.ID, hamsterball.HP);
                        if (hamsterball.HP == 0) DestroyHamsterball(hamsterball.ID);
                        return;
                    } // Hamsterball End >> NOT in Hamsterball:
                    if (target.ArmorTapes <= 0) // No ArmorTicks
                    {
                        int damage = weapon.Damage;
                        if (projectileID >= 0)
                        {
                            damage += player.Projectiles[projectileID].WeaponRarity * weapon.DamageIncrease;
                        }
                        SendConfirmAttack(player.ID, target.ID, projectileID, 0, -1, 0);
                        test_damagePlayer(target, damage, player.ID, weaponID);
                        if (weapon.Name == "GunDart")
                        {
                            int tickAdd = _ddgAddTicks;
                            if ((target.DartTicks + tickAdd) > _ddgMaxTicks) tickAdd = _ddgMaxTicks - target.DartTicks;
                            target.DartTicks += tickAdd;
                            if (target.DartTicks == 0) target.DartNextTime = DateTime.UtcNow.AddSeconds(_ddgTickRateSeconds);
                        }
                        return;
                    }
                    byte armorDamage = weapon.ArmorDamage;
                    if ((target.ArmorTapes - armorDamage) < 0) armorDamage = target.ArmorTapes;
                    target.ArmorTapes -= armorDamage;
                    if (weapon.PenetratesArmor) // vanilla weapon-data: ONLY applies to dartgun. read Weapon.cs for more info.
                    {
                        // Calculate Dartgun Stuff....
                        int damage = weapon.Damage + (player.Projectiles[projectileID].WeaponRarity * weapon.DamageIncrease);
                        int tickAdd = _ddgAddTicks;
                        if ((target.DartTicks + tickAdd) > _ddgMaxTicks) tickAdd = _ddgMaxTicks - target.DartTicks;
                        target.DartTicks += tickAdd;
                        if (target.DartTicks == 0) target.DartNextTime = DateTime.UtcNow.AddSeconds(_ddgTickRateSeconds);
                        test_damagePlayer(target, damage, player.ID, weaponID);
                    }
                    else if (weapon.WeaponType == WeaponType.Melee) test_damagePlayer(target, (int)Math.Floor(weapon.Damage / 2f), player.ID, weaponID);
                    // Sometimes darts double tick. Did this get fixed? Not sure!
                    SendConfirmAttack(player.ID, target.ID, projectileID, armorDamage, -1, 0);
                } catch (NetException netEx)
                {
                    Logger.Failure($"[HandleAttackConfirm] Player @ {pmsg.SenderConnection} caused a NetException!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("An error occurred whilst reading your packet data.");
                }
            }
        }

        // TODO -- This can likely be improved. I want to be comfortable using this method without wondering whether or not it is OK
        private void test_damagePlayer(Player player, int damage, short sourceID, short weaponID)
        {
            // Check to see if the Player is DEAD or... /GOD
            if (!player.isAlive || player.isGodmode) return; // Don't worry about logging this...
            player.SetLastDamageSource(sourceID, weaponID);
            // Try and Damage
            Logger.DebugServer($"Player {player.Name} (ID: {player.ID}) Health: {player.HP}\nDamage Attempt: {damage}");
            if ((player.HP - damage) <= 0)
            {
                if (player.Teammates != null && player.Teammates.Count > 0)
                {
                    for (int i = 0; i < player.Teammates.Count; i++)
                    {
                        if (player.Teammates[i].isAlive && !player.Teammates[i].isDown) // Alive and NOT down
                        {
                            HandlePlayerDowned(player, sourceID, weaponID);
                            return;
                        }
                        Logger.Warn("Player had no teammates left that were ALIVE and NOT-DOWN");
                    }
                }
                Logger.DebugServer("This damage attempt resulted in the death of the player.");
                HandlePlayerDied(player);
            }
            else
            {
                player.HP -= (byte)damage;
                Logger.DebugServer($"Final Health: {player.HP}");
            }
        }

        /// <summary>
        /// Sends the "ShotInformation" message to all NetPeers with using the provided parameters.
        /// </summary>
        private void SendConfirmAttack(short attacker, short target, short projectileID, byte armorDamage, short vehicleID, byte vehicleHP)
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
        private void serverHandleChatMessage(NetIncomingMessage message) // TODO - Cleanup; fix/redo or remove the """Command""" feature
        {
            Logger.Header("Chat message. Wonderful!");
            //this is terrible. we are aware. have fun.
            if (TryPlayerFromConnection(message.SenderConnection, out Player LOL))
            {
                if (message.PeekString().StartsWith("/"))
                {
                    string[] command = message.PeekString().Split(" ", 9);
                    string responseMsg = "command executed... no info given...";
                    short id, id2, amount;
                    float cPosX, cPosY;
                    Logger.Warn($"Player {LOL.ID} ({LOL.Name}) sent command \"{command[0]}\"");
                    switch (command[0])
                    {
                        case "/help":
                            Logger.Success("user has used help command");
                            if (command.Length >= 2)
                            {
                                switch (command[1])
                                {
                                    case "help":
                                        responseMsg = "\nThis command will give information about other commands!\nUsage: /help {page} [WARNING: NOT UPDATED CONSISTENTLY!!]";
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
                            responseMsg = $"Safemode has been set to \"{_safeMode.ToString().ToUpper()}\".\nI sure hope you know what you're doing...";
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
                                    SendForcePosition(LOL, start, false);
                                    break;
                                }
                            }
                            break;
                        case "/camptest":
                            for (int i = 0; i < _campfires.Length; i++)
                            {
                                if (_campfires[i] == null) continue;
                                Campfire bruh = _campfires[i];
                                SendForcePosition(LOL, bruh.Position, false);
                                Thread.Sleep(400);
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
                                        Weapon[] _gunList = Weapon.GetAllGunsList(_weapons);
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
                                        // Send Loot
                                        SendNewGunItem(weapon.JSONIndex, rarity, (byte)weapon.ClipSize, player.Position);
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

                            }
                            catch (Exception except)
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
                                    if (amount - _players[id].HP <= 0)
                                    {
                                        _players[id].HP += (byte)amount;
                                        if (_players[id].HP > 100) { _players[id].HP = 100; }
                                        responseMsg = $"Healed player {id} ({_players[id].Name} by {amount})";
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
                                    _players[id].HP = (byte)amount;
                                    responseMsg = $"Set player {id} ({_players[id].Name})'s health to {amount}";
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

                                    _players[id].Position.x = cPosX;
                                    _players[id].Position.y = cPosY;
                                    responseMsg = $"Moved player {id} ({_players[id].Name}) to ({cPosX}, {cPosY}). ";
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
                                    forcetoPos.Write(_players[id2].Position.x);
                                    forcetoPos.Write(_players[id2].Position.y);
                                    forcetoPos.Write(false);
                                    server.SendToAll(forcetoPos, NetDeliveryMethod.ReliableOrdered);

                                    _players[id].Position = _players[id2].Position;
                                    responseMsg = $"Moved player {id} ({_players[id].Name}) to player {id2} ({_players[id2].Name}). ";
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
                            if (!_hasMatchStarted) responseMsg = "Cannot modify the safezone while in-lobby. Try again once the match begins.";
                            else if (command.Length == 9)
                            {
                                try
                                {
                                    float gx1 = float.Parse(command[1]);
                                    float gy1 = float.Parse(command[2]);
                                    float gr1 = float.Parse(command[3]);
                                    float gx2 = float.Parse(command[4]);
                                    float gy2 = float.Parse(command[5]);
                                    float gr2 = float.Parse(command[6]);
                                    float gwarn = float.Parse(command[7]);
                                    float gadvtime = float.Parse(command[8]);
                                    isSkunkGasActive = false; // skunk gas is always turned off temporairly with this "command"
                                    CreateSafezone(gx1, gy1, gr1, gx2, gy2, gr2, gwarn, gadvtime);
                                    responseMsg = $"Started Gas Warning:\n-- Start Circle -- \nCenter: ({gx1}, {gy1})\nRadius: {gr1}\n-- End Circle -- :\nCenter: ({gx2}, {gy2})\nRadius: {gr2}\n\nTime until Approachment: ~{gwarn} seconds.\nMay Banan have mercy on your soul";
                                }
                                catch
                                {
                                    responseMsg = "Error occurred while parsing values. Likely invalid type-- this command takes FLOATS.";
                                }
                            }
                            else responseMsg = "Invalid argument count. Usage: /safezone {C1.CPosX} {C1.CPosY} {C1.Radius} {C2.CPosX} {C2.CPosY} {C2.Radius} {Delay} {Duration}";
                            break;
                        case "/list":
                            try
                            {
                                int _initSize = GetValidPlayerCount() * 16;
                                System.Text.StringBuilder listText = new System.Text.StringBuilder("|-- Players --\n", _initSize);
                                //string list = "-- Player--\n";
                                for (int i = 0; i < _players.Length; i++)
                                {
                                    if (_players[i] != null)
                                    {
                                        listText.AppendLine($"| {_players[i].ID} ({_players[i].Name})");
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
                            catch (Exception except)
                            {
                                Logger.Failure($"Command Error:\n{except}");
                            }
                            break;
                        case "/forceland":
                            if (command.Length > 1)
                            {
                                if (!TryPlayerFromString(command[1], out Player eject))
                                {
                                    responseMsg = $"Could not locate player \"{command[1]}\"";
                                    break;
                                }
                                SendForcePosition(eject, eject.Position, true);
                                responseMsg = $"Force-Ejected {eject.Name} ({eject.ID}) from the Giant Eagle.";
                            }
                            else
                            {
                                SendForcePosition(LOL, LOL.Position, true);
                                responseMsg = $"Force-Ejected {LOL.Name} ({LOL.ID}) from the Giant Eagle.";
                            }
                            break;

                        case "/down":
                        case "/forcedown":
                            if (command.Length > 1)
                            {
                                if (TryPlayerFromString(command[1], out Player player))
                                {
                                    HandlePlayerDowned(player, LOL.ID, -1);
                                    responseMsg = $"Forcibly downed player {player.ID} ({player.Name}).\nGet fricking... owned...";
                                }
                                else responseMsg = $"Couldn't locate player \"{command[1]}\"";
                            }
                            else
                            {
                                HandlePlayerDowned(LOL, LOL.ID, -1);
                                responseMsg = $"Forcibly downed player {LOL.ID} ({LOL.Name}). Get fricking... owned...";
                            }
                            break;

                        case "/res":
                        case "/forceres":
                            if (command.Length > 1)
                            {
                                if (TryPlayerFromString(command[1], out Player player))
                                {
                                    player.ReviverID = LOL.ID;
                                    RevivePlayer(player);
                                    responseMsg = $"Forced {player.ID} ({player.Name}) to get picked up.";
                                }
                                else responseMsg = $"Couldn't locate player \"{command[1]}\"";
                            }
                            else
                            {
                                LOL.ReviverID = LOL.ID;
                                RevivePlayer(LOL);
                                responseMsg = $"Forced {LOL.ID} ({LOL.Name}) to get picked up.";
                            }
                            //else responseMsg = $"Not enough arguments provided. Use: /res [PlayerID] | [PlayerName]";
                            break;

                        case "/divemode":
                            if (command.Length > 1)
                            {
                                if (!TryPlayerFromString(command[1], out Player diver))
                                {
                                    responseMsg = $"Couldn't locate player \"{command[1]}\"";
                                    break;
                                }
                                diver.isDiving = !diver.isDiving;
                                SendParachuteUpdate(diver.ID, diver.isDiving);
                                responseMsg = $"Parachute mode for {diver.Name} ({diver.ID}) updated to {diver.isDiving}";
                            }
                            else
                            {
                                LOL.isDiving = !LOL.isDiving;
                                SendParachuteUpdate(LOL.ID, LOL.isDiving);
                                responseMsg = $"Parachute mode for {LOL.Name} ({LOL.ID}) updated to {LOL.isDiving}";
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

                        // Msg110 test --> Banan Praised
                        case "/pray": // msg 110
                            {
                                NetOutgoingMessage banan = server.CreateMessage();
                                banan.Write((byte)110); // Byte  | MsgID (110)
                                banan.Write(LOL.ID);    // Short | PlayerID
                                banan.Write(0f);        // Float | Interbal to spawn bananas
                                banan.Write((byte)255); // Byte  | Amount of Nanners to spawn
                                for (int i = 0; i < 8; i++)
                                {
                                    for (int j = 0; j < 32; j++)
                                    {
                                        if (i + j >= 256) continue;
                                        banan.Write((float)(3683f + j)); //x 
                                        banan.Write((float)(3549f - i)); //y
                                    }
                                }
                                banan.Write((byte)1);   // Byte | # of Players to give Milestone to
                                banan.Write(LOL.ID);    // ID for Player to give Milestone to
                                server.SendToAll(banan, NetDeliveryMethod.ReliableOrdered);
                                responseMsg = "Praise Banan.";
                            }
                            break;
                        case "/kill":
                            if (command.Length > 1)
                            {
                                if (TryPlayerFromString(command[1], out Player killLol))
                                {
                                    if (!killLol.isAlive)
                                    {
                                        responseMsg = $"Can't kill player {killLol.Name} ({killLol.ID})! They're already dead!";
                                        break;
                                    }
                                    killLol.isGodmode = false;
                                    test_damagePlayer(killLol, killLol.HP, -3, -1);
                                    responseMsg = $"Killed player {killLol.Name} ({killLol.ID}).";
                                }
                                else responseMsg = $"Could not locate player \"{command[1]}\".";
                            }
                            else responseMsg = $"Not enough arguments provided! Usage: /kill (playerName) | (playerID)";
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
                                    if (TryPlayerFromName(command[1], out Player newgod) || (int.TryParse(command[1], out int Id) && TryPlayerFromID(Id, out newgod)))
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
                        case "/ghost": // msg 105 -- Command is currently testing only!
                            // Lets you move around like ghost, but your body sticks around for some reason. Oh well...
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
                            }
                            catch
                            {
                                responseMsg = "There was an error while processing your request.";
                            }
                            break;
                        case "/tapes":
                            Logger.Success("Armor-Tick Set Command!");
                            if (command.Length > 2)
                            {
                                try
                                {
                                    id = short.Parse(command[1]);
                                    amount = short.Parse(command[2]);
                                    _players[id].ArmorTapes = (byte)amount;
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
                            if (command.Length > 1)
                            {
                                if (TryPlayerFromString(command[1], out Player pos)) responseMsg = $"Player {pos.Name} ({pos.ID}) @ {pos.Position}.";
                                else responseMsg = $"Couldn't find player \"{command[1]}\"";
                            }
                            else if (command.Length == 1) responseMsg = $"Player {LOL.Name} ({LOL.ID}) @ {LOL.Position}.";
                            break;
                        case "/spawnjuice":
                        case "/spawndrink":
                            Logger.Success("Health Juice spawn used!");
                            {
                                if (command.Length == 1)
                                {
                                    SendNewJuiceItem(0, LOL.Position); // "LOL" set up near the start of the Switch
                                    responseMsg = "Spawned 1 tape!";
                                }
                                else if (command.Length > 1 && int.TryParse(command[1], out int juiceAmount))
                                {
                                    SendNewTapeItem((short)juiceAmount, LOL.Position);
                                    responseMsg = $"Spawned tape x{juiceAmount}!";
                                }
                                else responseMsg = $"Invalid value \"{command[1]}\".";
                            }
                            break;
                        case "/spawntape":
                            Logger.Success("Tape spawn used!");
                            {
                                if (command.Length == 1)
                                {
                                    SendNewTapeItem(1, LOL.Position); // "LOL" set up near the start of the Switch
                                    responseMsg = "Spawned 1 tape!";
                                }
                                else if (command.Length > 1 && int.TryParse(command[1], out int tapeCount))
                                {
                                    SendNewTapeItem((short)tapeCount, LOL.Position);
                                    responseMsg = $"Spawned tape x{tapeCount}!";
                                }
                                else responseMsg = $"Invalid value \"{command[1]}\".";
                            }
                            break;
                        case "/stopp":
                            try
                            {
                                int pid = int.Parse(command[1]);
                                if (_players[pid] != null)
                                {
                                    SendCancelReload(_players[pid]);
                                    responseMsg = $"ended player {_players[pid].ID} ({_players[pid].Name})";
                                }
                                else
                                {
                                    responseMsg = $"that is a null player entry! {command[1]}";
                                }
                            }
                            catch
                            {
                                responseMsg = "some errror occrured...";
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
                    allchatmsg.Write(LOL.ID); //ID of player who sent msg
                    allchatmsg.Write(responseMsg);
                    server.SendToAll(allchatmsg, NetDeliveryMethod.ReliableUnordered);
                    //server.SendMessage(allchatmsg, message.SenderConnection, NetDeliveryMethod.ReliableUnordered);
                }
                else // Regular Message
                {
                    NetOutgoingMessage allchatmsg = server.CreateMessage();
                    allchatmsg.Write((byte)26);             // Byte   | MsgID (26)
                    allchatmsg.Write(LOL.ID);               // Short  | PlayerID
                    allchatmsg.Write(message.ReadString()); // String | MessageText
                    allchatmsg.Write(false);                // Bool   | ToTeam? (Must make sure message only gets sent to teammate if it's true)
                    server.SendToAll(allchatmsg, NetDeliveryMethod.ReliableUnordered);
                }
            }
        }

        private void HandleSlotUpdate(NetIncomingMessage pmsg) // Msg27 >> Msg28
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleSlotUpdate", out Player player))
            {
                if (!player.IsPlayerReal()) return;
                try
                {
                    byte slot = pmsg.ReadByte();
                    if (slot > 4) return;
                    if (player.isReloading) SendCancelReload(player);
                    player.ActiveSlot = slot;
                    NetOutgoingMessage msg = server.CreateMessage();
                    msg.Write((byte)28);    // Byte  | MsgID (28)
                    msg.Write(player.ID);   // Short | PlayerID
                    msg.Write(slot);        // Byte  | SlotID
                    server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleSlotUpdate] Player @ {pmsg.SenderConnection} caused a NetException.\n{netEx}");
                    pmsg.SenderConnection.Disconnect("There was an error while readin your packet data! (SlotUpdate)");
                }
            }
        }

        // Msg34 | "Super Skunk Gas Approach" -- Sent once the Server acknowledges the SSG should approach. Duration is how long the gas will last
        private void SendSSGApproachEvent(float duration)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(5);
            msg.Write((byte)34);    // Byte  | MsgID (34)
            msg.Write(duration);    // Float | ApproachmentDuration
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        /// <summary>
        /// Handles a ReloadRequest message received from a NetPeer. Sends a "ReloadConfirm" message to ALL NetPeers if checks pass. Othersise, the sender is DC'd.
        /// </summary>
        /// <param name="amsg">Incoming message to handle and read data from.</param>
        private void HandleReloadRequest(NetIncomingMessage amsg) // TOOD -- Calculate Reload EndTime ourselves
        {
            if (TryPlayerFromConnection(amsg.SenderConnection, out Player player))
            {
                try
                {
                    short weaponID = amsg.ReadInt16();
                    byte slotID = amsg.ReadByte();
                    if (_hasMatchStarted) // If match is in progress then do checks. If in lobby, it's fine to just send for now.
                    {
                        // Bunch of checks and stuff.
                        if (!player.IsPlayerReal() || !player.IsGunAndSlotValid(weaponID, slotID)) return;
                        if (player.Ammo[_weapons[weaponID].AmmoType] == 0)
                        {
                            Logger.Warn($"Server Desync? Player doesn't have any ammo in this slot!");
                            return; // No ammo! What the hey hey!!
                        }
                        Logger.DebugServer("[HandleReloadRequest] All Passed!");
                        player.isReloading = true;
                        //player.ReloadFinishTime = DateTime.Ut
                    }
                    CheckMovementConflicts(player);
                    // Send Reload :]
                    NetOutgoingMessage msg = server.CreateMessage();
                    msg.Write((byte)30);    // Byte  | MessageID (30)
                    msg.Write(player.ID);   // Short | PlayerID
                    msg.Write(weaponID);    // Short | WeaponID
                    msg.Write(slotID);      // Byte  | SlotID
                    server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);

                } catch (NetException netEx)
                {
                    Logger.Failure($"[HandleReloadRequest] Player @ {amsg.SenderConnection} caused a NetException.\n{netEx}");
                    amsg.SenderConnection.Disconnect("There was an error processing data.");
                }
            }
            else
            {
                Logger.Failure($"[HandleReloadRequest] Player @ {amsg.SenderConnection} not in PlayerList!");
                amsg.SenderConnection.Disconnect("There was an error processing your request. Message: Invalid Action! Not in PlayerList!");
            }
        }

        private void HandleReloadFinished(NetIncomingMessage pmsg) // Msg92 >> Msg93
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleReloadFinished", out Player player))
            {
                if (_hasMatchStarted)
                {
                    if (!player.isReloading) Logger.Failure("[HandleReloadFinish] Player wasn't reloading! Probably just ignored cancel request from earlier!");
                    if (player.ActiveSlot > 1)
                    {
                        Logger.Failure("[HandleReloadFinish] Player's ActiveSlot was greater than 1. ");
                        pmsg.SenderConnection.Disconnect("Slots got desynced! Sorry! D: (switch too fast; ignored reload cancel)");
                        return;
                    }
                    player.isReloading = false;
                    Weapon weapon = _weapons[player.LootItems[player.ActiveSlot].WeaponIndex];
                    int ammoType = weapon.AmmoType;
                    //Logger.Header($"Ammo Type: {ammoType}\nPlayer Ammo: {player.Ammo[ammoType]}");
                    //Logger.Basic($"Ammo in gun NOW: {player.LootItems[player.ActiveSlot].GiveAmount}");
                    // Get the remaining ammo needed to fill this wepaon to max
                    int reloadAmmo = weapon.ClipSize - player.LootItems[player.ActiveSlot].GiveAmount;
                    if ((player.Ammo[ammoType] - reloadAmmo) < 0) reloadAmmo = player.Ammo[ammoType];
                    // Stuff the remaining into the wepaon
                    player.Ammo[ammoType] -= (byte)reloadAmmo;
                    player.LootItems[player.ActiveSlot].GiveAmount += (byte)reloadAmmo;
                    //Logger.Basic($"Ammo in gun FINAL: {player.LootItems[player.ActiveSlot].GiveAmount}");
                    //Logger.Basic($"Sent finsihed reloading message!\nFinal Ammo: {player.Ammo[ammoType]}");
                }
                // Send Message Out
                NetOutgoingMessage msg = server.CreateMessage();
                msg.Write((byte)93);        // Byte  | MsgID (93)
                msg.Write(player.ID);    // Short | PlayerID
                server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
            }
        }

        private void SendCurrentLobbyCountdown(double countdown) // Msg43
        {
            NetOutgoingMessage msg = server.CreateMessage(9);
            msg.Write((byte)43);    // Byte   | MsgID (43)
            msg.Write(countdown);   // Double | LobbyCountdownSeconds
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        // Msg 45 | "HP / Armor / MoveMode; etc. Changes" -- Sent when players have their data changed in some way; Should be a large list, here it is just one player
        private void SendPlayerDataChange(Player player)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)45);            // Byte  | MsgID (45)
            msg.Write((byte)1);             // Byte  | # of Players [Always 1 here]
            msg.Write(player.ID);           // Short | PlayerID
            msg.Write(player.HP);           // Byte  | CurrentHP [game converts to Float]
            msg.Write(player.ArmorTier);    // Byte  | ArmorTier
            msg.Write(player.ArmorTapes);   // Byte  | ArmorTicks / ArmorTapes
            msg.Write(player.WalkMode);     // Byte  | WalkMode
            msg.Write(player.Drinkies);     // Byte  | # of Juice
            msg.Write(player.Tapies);       // Byte  | # of Tape
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        // Msg49 | "Player Ended Drinking" -- Sent once a player has finished drinking.
        private void SendPlayerEndDrink(Player player)
        {
            player.isDrinking = false;
            NetOutgoingMessage msg = server.CreateMessage(5);
            msg.Write((byte)49);    // Byte  | MsgID (49)
            msg.Write(player.ID);   // Short | PlayerID
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        // Client[51] >> Server[52] -- Coconut Eat Request
        private void HandleCoconutRequest(NetIncomingMessage amsg) // If we ever get to 2020+ versions with powerups; this'll need some work!
        {
            if (TryPlayerFromConnection(amsg.SenderConnection, out Player player)) // Get Player
            {
                try // So if the data isn't there program doesn't crash.
                {
                    // Get CoconutID -- Funny note: It's actually the index in the coconut array, which changes size based on RNG seeds lol
                    // So if you thought this was correlated with the ID of the CoconutTile in LevelData, then nuh uh
                    ushort coconutID = amsg.ReadUInt16();
                    // Figure out if requested coconut is actually real still
                    if (_coconutList.ContainsKey(coconutID))
                    {
                        Coconut thisCoco = _coconutList[coconutID];
                        if (Vector2.ValidDistance(thisCoco.Position, player.Position, 10f, true))
                        {
                            // Send Message
                            NetOutgoingMessage coconutMsg = server.CreateMessage(14);
                            coconutMsg.Write((byte)52);
                            coconutMsg.Write(player.ID);
                            coconutMsg.Write((short)coconutID);
                            server.SendToAll(coconutMsg, NetDeliveryMethod.ReliableUnordered);
                            // Figure out heal amount
                            float healAmount = _coconutHeal; // Future TODO -- 2020: Banana Forker and stuff
                            if ((player.HP + healAmount) > 100) healAmount = 100 - player.HP;
                            player.HP += (byte)healAmount;
                            // Remove entry if match is in progress
                            if (_hasMatchStarted) _coconutList.Remove(coconutID);
                        }
                        else Logger.Warn($"[HandleCoconutRequest] Player @ {amsg.SenderConnection} not within threshold but sent coconut eat message.");
                    } // Right now, the Player isn't dropped if the coconut is found because maybe they just lagged and sent message twice.
                    // If this becomes an issue, then yes- Players will get dropped for this invalid action.
                } catch (NetException ex)
                {
                    Logger.Failure($"[HandleCoconutRequest] Likely read past bufffer size. Meaning this is an invalid msg.\n{ex}");
                    amsg.SenderConnection.Deny("There was an error processing your request. Message: Error while reading packet data.");
                }
            }
            else
            {
                Logger.Failure($"[HandleCoconutRequest] Could not locate Player @ NetConnection \"{amsg.SenderConnection}\"; Connection has been dropped.");
                amsg.SenderConnection.Deny("There was an error processing your request. Sorry for the inconvenience.\nMessage: INVALID ACTION- NOT IN PLAYER-LIST");
            }
        }

        // ClientSentCutGrass[53] >> ServerSentCutGrass[54]
        private void serverSendCutGrass(NetIncomingMessage message) // POTENTIAL CRASH SPOT
        {
            if (TryPlayerFromConnection(message.SenderConnection, out Player player))
            {
                // TOOD - Generate some item loot after cutting grass ?
                byte bladesCut = message.ReadByte();
                NetOutgoingMessage grassMsg = server.CreateMessage();
                grassMsg.Write((byte)54);
                grassMsg.Write(player.ID);
                grassMsg.Write(bladesCut);
                for (byte i = 0; i < bladesCut; i++)
                {
                    grassMsg.Write(message.ReadInt16()); //x
                    grassMsg.Write(message.ReadInt16()); //y
                }
                server.SendToAll(grassMsg, NetDeliveryMethod.ReliableOrdered);
            }
            else
            {
                Logger.Failure($"Grass Cut- Player not in player list.");
                message.SenderConnection.Disconnect("Invalid Action! Not in PlayerList!");
            }
        }

        /// <summary>Sends a "Player Exit Hamsterball" packet to all connected NetPeers using the provided parameters.</summary>
        /// <param name="playerID">ID of the Player exiting a Hamsterball.</param>
        /// <param name="hamsterballID">ID of the Hamsterball being left.</param>
        /// <param name="exitPosition">Position the Hamsterball will be placed at after exiting.</param>
        private void SendExitHamsterball(short playerID, short hamsterballID, Vector2 exitPosition)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)58);        // Byte  | MsgID (58)
            msg.Write(playerID);        // Short | PlayerID
            msg.Write(hamsterballID);   // Short | HamsterballID
            msg.Write(exitPosition.x);  // Float | ExitPositionX
            msg.Write(exitPosition.y);  // Float | ExitPositionY
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary> Sends a HamsterballDamagePlayer packet to all connected NetPeers using the provided parameters. </summary>
        /// <param name="attackerID">ID of the attacking player.</param>
        /// <param name="targetID">ID of the target, rolled, player.</param>
        /// <param name="didKill">Whether the target player died or not.</param>
        /// <param name="attackerBallID">ID of the attacking Player's Hamsterball.</param>
        /// <param name="targetBallID">ID of the TargetPlayer's Hamsterball. (use -1 if they're not in a ball)</param>
        private void SendHamsterballHurtPlayer(short attackerID, short targetID, bool didKill, short attackerBallID, short targetBallID)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)61);        // Byte  | MsgID (61)
            msg.Write(attackerID);      // Short | AttackerID
            msg.Write(targetID);        // Short | AttackerID
            msg.Write(didKill);         // Bool  | DidKillPlayer
            msg.Write(attackerBallID);  // Short | HamsterballID (Attacker)
            msg.Write(targetBallID);    // Short | HamsterballID (Target)
            if (targetBallID >= 0)      // Byte  | Remaining Hamsterball HP (Target)
            {
                if (!_hamsterballs.ContainsKey(targetBallID)) msg.Write((byte)0);
                else msg.Write(_hamsterballs[targetBallID].HP);
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Handles an incoming LootRequest packet for while in an in-progress match. (Msg21)
        /// </summary>
        /// <param name="amsg">Incoming message to handle and read data from.</param>
        private void HandleLootRequestMatch(NetIncomingMessage amsg)
        {
            Logger.DebugServer("LootItems request.");
            if (TryPlayerFromConnection(amsg.SenderConnection, out Player player))
            {
                // Is the Player dead? Has the Player landed yet?
                if (!player.IsPlayerReal() || player.NextLootTime > DateTime.UtcNow) return;
                try
                {
                    // Read Data. Verify if it is real.
                    int reqLootID = amsg.ReadInt32();
                    byte slotID = amsg.ReadByte();

                    // Verify is real.
                    Logger.DebugServer($"[BetterHandleLootItem] Sent Slot: {slotID}");
                    if (slotID < 0 || slotID > 3) return; // NOTE: Seems like the game will always try to send the correct slot to change.
                    if (!_lootItems.ContainsKey(reqLootID)) // Add infraction thing so like if they do to much kicky?
                    {
                        Logger.Failure($"[Handle MatchLootRequest] Player @ {amsg.SenderConnection} requested a loot item that wasn't in the list.");
                        return;
                        //amsg.SenderConnection.Disconnect($"Requested LootID \"{reqLootID}\" not found.");
                    }

                    // Check if player is close enough.
                    LootItem item = _lootItems[reqLootID];  // Item is set here!
                    // TODO -- Spawn Loot Vertically so this doesn't give false negative. << Need to know all valid tile spots though...
                    if (!Vector2.ValidDistance(item.Position, player.Position, 10.5f, true)) // Still testing thresholds...
                    {
                        Logger.Failure($"[HandleMatchLootReq] Player not close enough to loot...");
                    }
                    CheckMovementConflicts(player);

                    // Ok give the item.
                    string itemdata = $"-- Found Item! --\nName: {item.Name}; Type: {item.LootType}; WeaponType: {item.WeaponType}\nRarity: {item.Rarity}; Give: {item.GiveAmount}; Position: {item.Position}\n";
                    Logger.DebugServer(itemdata);

                    switch (item.LootType)
                    {
                        // Health Juice
                        case LootType.Juice:
                            if (player.Drinkies == 200) return;
                            if ( (player.Drinkies + item.GiveAmount) > 200)
                            {
                                item.GiveAmount -= (byte)(200 - player.Drinkies);
                                player.Drinkies = 200;
                                SendNewJuiceItem(item.GiveAmount, item.Position); // Player kind of spams requests when getting loot. Oops...
                            }
                            else player.Drinkies += item.GiveAmount;
                            break;

                        // Tape
                        case LootType.Tape:
                            if (player.Tapies == 5) return;             // If at max -> stop; otherwise...
                            if ((player.Tapies + item.GiveAmount) > 5)
                            {
                                item.GiveAmount -= (byte)(5 - player.Tapies);
                                player.Tapies = 5;
                                SendNewTapeItem(item.GiveAmount, item.Position);
                            }
                            else player.Tapies += item.GiveAmount;
                            break;

                        // Armor
                        case LootType.Armor:
                            if (player.ArmorTier != 0) // Has armor
                            {
                                SendNewArmorItem(player.ArmorTier, player.ArmorTapes, item.Position);
                                player.ArmorTier = item.Rarity;
                                player.ArmorTapes = item.GiveAmount;
                            }
                            else // No armor
                            {
                                player.ArmorTier = item.Rarity;
                                player.ArmorTapes = item.GiveAmount;
                            }
                            break;

                        // Ammo -- No clue if worky. Have to track ammo and stuff first. Probably works fine
                        case LootType.Ammo: // Forgot there is an ammo data file- could use that maybe to define types at some point?
                            int ammoArrayIndex = item.Rarity;
                            Logger.Header($"Sent AmmoType: {ammoArrayIndex}\nGive: {item.GiveAmount}");
                            if ( (ammoArrayIndex < 0) || (ammoArrayIndex > (_maxAmmo.Length - 1) ) ) return;
                            if (player.Ammo[ammoArrayIndex] == _maxAmmo[ammoArrayIndex]) return;

                            if ((player.Ammo[ammoArrayIndex] + item.GiveAmount) > _maxAmmo[ammoArrayIndex])
                            {
                                item.GiveAmount -= (byte)(_maxAmmo[ammoArrayIndex] - player.Ammo[ammoArrayIndex]);
                                player.Ammo[ammoArrayIndex] = _maxAmmo[ammoArrayIndex];
                                SendNewAmmoItem((byte)ammoArrayIndex, item.GiveAmount, item.Position);
                            }
                            else player.Ammo[ammoArrayIndex] += item.GiveAmount;
                            Logger.Basic($"Player[{ammoArrayIndex}]: {player.Ammo[ammoArrayIndex]}");
                            break;

                        // Weapon -- May be messy!
                        case LootType.Weapon:
                            if (slotID == 3 && item.WeaponType == WeaponType.Throwable) // Throwables
                            {
                                // If Player doesn't have anything then...
                                if (player.LootItems[2].LootType == LootType.Collectable) player.LootItems[2] = item;
                                else // Has throwable already...
                                {
                                    if (player.LootItems[2].WeaponIndex == item.WeaponIndex) // add to count
                                    {
                                        int maxCount = _weapons[player.LootItems[2].WeaponIndex].MaxCarry;
                                        if (player.LootItems[2].GiveAmount == maxCount) return;
                                        if (player.LootItems[2].GiveAmount + item.GiveAmount > maxCount)
                                        {
                                            item.GiveAmount -= (byte)(maxCount - player.LootItems[2].GiveAmount);
                                            player.LootItems[2].GiveAmount = (byte)maxCount;
                                            SendNewThrowableItem(item.WeaponIndex, item.GiveAmount, item.Position);
                                        }
                                        else player.LootItems[2].GiveAmount += item.GiveAmount;
                                    }
                                    else
                                    {
                                        SendNewThrowableItem(player.LootItems[2].WeaponIndex, player.LootItems[2].GiveAmount, item.Position);
                                        player.LootItems[2] = item;
                                    }
                                }
                            }
                            else if (item.WeaponType == WeaponType.Gun) // Actual Weapons
                            {
                                // Slots 0-1 are weapons; slot 2 is throwables; SENT slot 3 is throwable. SENT SLOT 2 is melee.
                                int wepSlot = slotID;
                                if (player.LootItems[wepSlot].LootType != LootType.Collectable)
                                {
                                    // Spawn old weapon on the ground
                                    SendNewGunItem((short)player.LootItems[wepSlot].WeaponIndex, player.LootItems[wepSlot].Rarity, player.LootItems[wepSlot].GiveAmount, item.Position);
                                    // Now replace the slot with the newly found item.
                                    player.LootItems[wepSlot] = item;
                                }
                                else player.LootItems[wepSlot] = item; // Item is NOT in this slot, so just replace it
                            }
                            break;
                    }

                    // Send LootItems OK!
                    _lootItems.Remove(reqLootID);
                    SendPlayerDataChange(player);
                    NetOutgoingMessage msg = server.CreateMessage();
                    msg.Write((byte)22);    // Byte  |  MsgID
                    msg.Write(player.ID);   // Short |  PlayerID
                    msg.Write(reqLootID);   // Int   |  LootID
                    msg.Write(slotID);      // Byte  |  SlotID lol
                    msg.Write((byte)0);     // Byte  |  No clue lol
                    server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
                    player.NextLootTime = DateTime.UtcNow.AddMilliseconds(100);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"Read past buffer!\n{netEx}");
                    amsg.SenderConnection.Deny("INVALID PACKET"); // make real
                } catch (Exception ex) // You can remove this once this method works just fine.
                {
                    Logger.Failure($"LootItems Request error:\n{ex}");
                }
            }
            else // make real
            {
                Logger.Failure("player no foundy");
                amsg.SenderConnection.Disconnect("You've been DC'd Couldn't your your connection LOL! Are you cheating?");
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
                msg.Write(player.ID);       // Short  |  PlayerID
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

        // Msg55 >> Msg56 | "Hamsterball Enter Request" -- Called when trying to enter a hamsterball
        private void HandleHamsterballEnter(NetIncomingMessage pmsg)
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleHamsterballEnter", out Player player))
            {
                if (!player.IsPlayerReal()) return;
                try
                {
                    int hampterID = pmsg.ReadInt16();
                    if (player.VehicleID != -1)
                    {
                        Logger.Failure($"[HandleHamsterballEnter] Player @ {pmsg.SenderConnection} was already in a hamsterball server-side. Could be ping related.");
                        pmsg.SenderConnection.Disconnect("You're already in a Hamsterball. Sent request too fast?");
                        return;
                    }
                    if (!_hamsterballs.ContainsKey(hampterID))
                    {
                        Logger.Failure($"[HandleHamsterballEnter] Player @ {pmsg.SenderConnection} sent invalid HamsterballID \"{hampterID}\". Likely ping related.");
                        if (hampterID < 0) pmsg.SenderConnection.Disconnect($"Hamsterball ID: {hampterID} is not valid.");
                        return;
                    }
                    if (_hamsterballs[hampterID].CurrentOwner != null)
                    {
                        Logger.Failure($"[HandleHamsterballEnter] Player @ {pmsg.SenderConnection} tried entering a Hamsterball that's owned by someone!");
                        return;
                    }
                    // Is Player close enough? Is Player dancing and stuff?
                    if (!Vector2.ValidDistance(_hamsterballs[hampterID].Position, player.Position, 14f, true)) return;
                    CheckMovementConflicts(player);

                    // Do server-side stuff...
                    player.VehicleID = (short)hampterID;
                    player.HamsterballVelocity = new Vector2(0f, 0f);
                    _hamsterballs[hampterID].CurrentOwner = player;

                    // Make and Send Enter Hamsterball Message.
                    NetOutgoingMessage msg = server.CreateMessage();
                    msg.Write((byte)56);                            // Byte  | MsgID (56)
                    msg.Write(player.ID);                           // Short | PlayerID
                    msg.Write((short)hampterID);                    // Short | HamsterballID
                    msg.Write(_hamsterballs[hampterID].Position.x); // Float | EnterPositionX
                    msg.Write(_hamsterballs[hampterID].Position.y); // Float | EnterPositionY
                    server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleHamsterballEnter] Player @ NetConnection \"{pmsg.SenderConnection}\" gave NetError!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("There was an error procssing your request. Message: Read past buffer size...");
                }
            }
        }

        // Msg57 >> Msg58 | "Hamsterball Exit Request" -- Called when trying to exit a ball; doesn't seem to appear if player's ball gets destroy
        private void HandleHamsterballExit(NetIncomingMessage pmsg)
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HamsterballExitRequest", out Player player))
            {
                if (!player.IsPlayerReal()) return;
                if (player.VehicleID == -1)
                {
                    Logger.Failure($"[HamsterballExitRequest] Player @ {pmsg.SenderConnection} wasn't in a Hamsterball? Likely ping related");
                    return;
                }
                if (!_hamsterballs.ContainsKey(player.VehicleID))
                {
                    Logger.Failure($"[HamsterballExitRequest] Player @ {pmsg.SenderConnection} had HamsterballID \"{player.VehicleID}\", which wasn't found in the Hamsterball list.");
                    pmsg.SenderConnection.Disconnect($"Could not find Hamsterball \"{player.VehicleID}\".");
                    return;
                }
                Hamsterball hamsterball = _hamsterballs[player.VehicleID];
                hamsterball.CurrentOwner = null;
                hamsterball.Position = player.Position;
                player.ResetHamsterball();
                SendExitHamsterball(player.ID, hamsterball.ID, player.Position);
            }
        }

        // Msg60 >> Msg61 | "Hamsterball Attack" -- Called when a hamsterball rolls into someone
        private void HandleHamsterballAttack(NetIncomingMessage pmsg)
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleHamsterballAttack", out Player player))
            {
                if (!player.IsPlayerReal()) return;
                if (player.VehicleID == -1)
                {
                    Logger.Failure($"[HandleHamsterballAttack] Player @ {pmsg.SenderConnection} was NOT in a Hamsterball, but they called this method!");
                    return;
                }
                try
                {
                    short targetID = pmsg.ReadInt16();
                    float speed = pmsg.ReadFloat();
                    // Find Target. Figure out if they're alive/godded or not.
                    if (!TryPlayerFromID(targetID, out Player target))
                    {
                        Logger.Failure($"[HandleHamsterballAttack] Player @ {pmsg.SenderConnection} gave an invalid PlayerID");
                        pmsg.SenderConnection.Disconnect("There was an error while processing your request. \"Requested TargetID not found.\"");
                        return;
                    }
                    if (!target.isAlive) return;
                    if (target.isGodmode)
                    {
                        SendHamsterballHurtPlayer(player.ID, target.ID, !target.isAlive, player.VehicleID, -1);
                        return;
                    }
                    // Damage section:
                    float speedDifference = speed - player.HamsterballVelocity.magnitude;
                    if (speedDifference > 5)
                    {
                        Logger.Warn($"[HandleHamsterballAttack] Player @ {pmsg.SenderConnection} speed difference was too high. Difference: {speedDifference}");
                        return;
                    }
                    if (target.VehicleID == -1)
                    {
                        test_damagePlayer(target, (int)(player.HamsterballVelocity.magnitude * 2), player.ID, -2);
                        SendHamsterballHurtPlayer(player.ID, target.ID, !target.isAlive, player.VehicleID, -1);
                    }
                    else if (target.HamsterballVelocity.magnitudeSquared < player.HamsterballVelocity.magnitudeSquared)
                    {
                        if (!_hamsterballs.ContainsKey(target.VehicleID)) return;
                        if ((_hamsterballs[target.VehicleID].HP - 1) < 0) _hamsterballs[target.VehicleID].HP = 0;
                        else _hamsterballs[target.VehicleID].HP -= 1;
                        SendHamsterballHurtPlayer(player.ID, target.ID, false, player.VehicleID, target.VehicleID);
                        if (_hamsterballs[target.VehicleID].HP == 0) DestroyHamsterball(target.VehicleID);
                    }
                }
                catch (NetException)
                {
                    Logger.Failure($"[HandleHamsterballAttack] Player @ {pmsg.SenderConnection} caused a NetException!");
                    pmsg.SenderConnection.Disconnect("An error occurred while trying to read packet data. Likely a length mis-mach.");
                }
            }
        }

        // Msg62 >> Msg63 | "Hamsterball Bounced" -- Called when a player bounces their hamsterball off a wall; doesn't happen when rolling into people
        private void HandleHamsterballBounce(NetIncomingMessage pmsg)
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleHamsterballBounce", out Player player))
            {
                if (player.VehicleID == -1)
                {
                    Logger.Failure($"[HandleHamsterballBounce] Player @ {pmsg.SenderConnection} VehicleID is -1, but they tried bouncing. Likely ping related.");
                    return;
                }
                player.HamsterballVelocity = new Vector2(0, 0);
                NetOutgoingMessage bounce = server.CreateMessage();
                bounce.Write((byte)63);
                bounce.Write(player.ID);
                bounce.Write(player.VehicleID);
                server.SendToAll(bounce, NetDeliveryMethod.ReliableOrdered);
            }
        }

        // Msg64 >> Msg65 | "Hamsterball Damage Request" -- Called when a hamsterball is hit by something; Melee, bullets, emu pecks (? - not verified)
        private void HandleHamsterballHit(NetIncomingMessage pmsg)
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleHamsterballHit", out Player player))
            {
                try
                {
                    short weaponID = pmsg.ReadInt16();
                    short vehicleID = pmsg.ReadInt16();
                    short projectileID = pmsg.ReadInt16();
                    // Make sure these read values are valid
                    if (!_hamsterballs.ContainsKey(vehicleID)) return;
                    if (!ValidWeaponIndex(weaponID))
                    {
                        Logger.Failure($"[HandleHamsterballHit] WeaponID \"{weaponID}\" is out of bounds!");
                        pmsg.SenderConnection.Disconnect($"There was an error processing your request. Message: WeaponID \"{weaponID}\" is invalid.");
                        return;
                    }
                    if (!player.IsValidProjectileID(projectileID))
                    {
                        Logger.Failure($"[HandleHamsterballHit] ProjectileID \"{projectileID}\" was an invalid ProjectileID.");
                        pmsg.SenderConnection.Disconnect($"There was an error processing your request. Message: ProjectileID \"{projectileID}\" is invalid.");
                        return;
                    }
                    if (projectileID >= 0 && (player.Projectiles[projectileID].WeaponID != weaponID))
                    {
                        Logger.Failure($"[HandleHamsterballHit] WeaponID \"{weaponID}\" did not match WeaponID for Projectile[{projectileID}]!");
                        pmsg.SenderConnection.Disconnect($"There was an error processing your request. Message: WeaponID \"{weaponID}\" didn't match found Projectile.");
                        return;
                    }
                    // Do damage to the ball.
                    int ballDamage = 1;
                    if (_weapons[weaponID].VehicleDamageOverride > 0) ballDamage = _weapons[weaponID].VehicleDamageOverride;
                    Hamsterball ball = _hamsterballs[vehicleID];
                    if ((ball.HP - ballDamage) <= 0) ballDamage = ball.HP;
                    ball.HP -= (byte)ballDamage;
                    // Send NetMessage
                    NetOutgoingMessage hitBall = server.CreateMessage();
                    hitBall.Write((byte)65);        // Byte  | MsgID (65) 
                    hitBall.Write(player.ID);       // Short | PlayerID
                    hitBall.Write(vehicleID);       // Short | HamsterballID / Hamsterball Index
                    hitBall.Write(ball.HP);         // Byte  | HamsterballHP
                    hitBall.Write(projectileID);    // Short | ProjectileID
                    server.SendToAll(hitBall, NetDeliveryMethod.ReliableOrdered);
                    if (_hamsterballs[vehicleID].HP == 0) DestroyHamsterball(vehicleID); // Destroy Hamsterball if needed.
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleHamsterballHit] Player @ NetConnection \"{pmsg.SenderConnection}\" gave NetError!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("There was an error while reading your packet data!\n[HamsterballHit]");
                }
            }
        }

        /// <summary>Handles an incoming EmoteRequest packet (Msg66).</summary>
        /// <param name="pmsg">NetMessage to read data from.</param>
        private void HandleEmoteRequest(NetIncomingMessage pmsg) // Msg66 >> Msg67
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleEmoteRequest", out Player player))
            {
                if (!player.IsPlayerReal()) return;
                try
                {
                    short emoteID = pmsg.ReadInt16();
                    float posX = pmsg.ReadFloat();
                    float posY = pmsg.ReadFloat();
                    float duration = pmsg.ReadFloat();
                    Vector2 reqPos = new Vector2(posX, posY);
                    CheckMovementConflicts(player);
                    SendPlayerEmote(player, emoteID, reqPos, duration);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleEmoteRequest] Player @ {pmsg.SenderConnection} caused a NetException!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("There was an error while reading your packet data! D:\n(HandleEmoteReq)");
                }
            }
        }

        /// <summary>
        /// Sends a "PlayerEmoted" packet to all NetPeers; also sets server-side fields.
        /// </summary>
        /// <param name="player">Player who's emoting.</param>
        /// <param name="emoteID">Index/ID of the emote being used.</param>
        /// <param name="duration">How long the emote will last for. (-1 = infinite)</param>
        private void SendPlayerEmote(Player player, short emoteID, Vector2 emotePosition, float duration) // Msg67
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)67);    // Byte  | MsgID (67)
            msg.Write(player.ID);   // Short | PlayerID
            msg.Write(emoteID);     // Short | EmoteID
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered); // So today I learned that ReliableOrdered breaks this

            player.isEmoting = true;
            player.EmoteID = emoteID;
            player.Position = emotePosition;
            if (duration > -1) player.EmoteEndTime = DateTime.UtcNow.AddSeconds(duration);
            else player.EmoteEndTime = DateTime.MaxValue;
        }

        /// <summary>
        /// Sends a "Player Finished Emoting" packet (Msg67) to all NetPeers. Does NOT set any server-side variables.
        /// </summary>
        private void SendPlayerEndedEmoting(Player player) // Msg67
        {
            player.isEmoting = false;
            player.EmoteID = -1;
            NetOutgoingMessage emote = server.CreateMessage();
            emote.Write((byte)67);  // Byte  | MsgID (67)
            emote.Write(player.ID); // Short | PlayerID
            emote.Write((short)-1); // Short | EmoteID (-1 will cancel emotes).
            server.SendToAll(emote, NetDeliveryMethod.ReliableSequenced);
        }

        /// <summary>
        /// Attempts to locate a Doodad at the given Coordinates and then destroys it!
        /// </summary>
        private void serverDoodadDestroy(int checkX, int checkY) // todo :: check for explosions
        {
            int _doodadCount = _doodadList.Count; // Performance from caching this maybe significant I forgot. But this list is HUGE
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
                    test.Write((short)420); // For whatever reason, expects a short before all other data. | v??? -> v0.90.2 -> v???
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
                    if ( (player.LootItems[slot].WeaponIndex == weaponID) || !_hasMatchStarted)
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
                if (!TryPlayerFromConnection(aMsg.SenderConnection, out Player player))
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
                if ((player.LootItems[slot].WeaponIndex != weaponID) && _hasMatchStarted) // Let's you know when this fails
                {
                    Logger.Failure($"[ServerHandle - AttackWindDown] Player @ NetConnection \"{aMsg.SenderConnection}\" sent AttackWindDown message, but WeaponID is not in Slot#.");
                }
            } catch (Exception except)
            {
                Logger.Failure($"[ServerHandle - AttackWindDown] ERROR\n{except}");
            }
        }

        /// <summary>Handles an incoming "HealRequest" packet (Msg47); Either ignoring or accepting the message and starting the Player heal.</summary>
        /// <param name="pmsg">Incoming message to read data from.</param>
        private void HandleHealingRequest(NetIncomingMessage pmsg) // Msg47 >> Msg48
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleHealingRequest", out Player player))
            {
                if (!player.IsPlayerReal() || (player.Drinkies == 0)) return;
                try
                {
                    float posX = pmsg.ReadFloat();
                    float posY = pmsg.ReadFloat();
                    Vector2 requestPostion = new Vector2(posX, posY);
                    if (Vector2.ValidDistance(requestPostion, player.Position, 2, true)){
                        SendForcePosition(player, requestPostion, false);
                        CheckMovementConflicts(player);
                        player.Position = requestPostion;
                        player.isDrinking = true;
                        player.NextHealTime = DateTime.UtcNow.AddSeconds(1.2d);
                        NetOutgoingMessage msg = server.CreateMessage();
                        msg.Write((byte)48);
                        msg.Write(player.ID);
                        server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
                    }
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleHealingRequest] Player @ {pmsg.SenderConnection} caused a NetException!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("There was an error while reading your packet data! (HealRequest)");
                }
            }
        }

        /// <summary> Handles an incoming "TapingRequest" packet (Msg98); Either ignoring, or accepting the request and setting the required fields.</summary>
        /// <param name="pmsg">Incoming message that contains TapeRequest data.</param>
        private void HandleTapeRequest(NetIncomingMessage pmsg) // Msg98 >> Msg99
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleTapeRequest", out Player player))
            {
                if (!player.IsPlayerReal() || player.Tapies == 0 || player.ArmorTier == 0 || player.ArmorTapes == player.ArmorTier) return;
                try
                {
                    float posX = pmsg.ReadFloat();
                    float posY = pmsg.ReadFloat();
                    Vector2 requestPosition = new Vector2(posX, posY);
                    if (Vector2.ValidDistance(requestPosition, player.Position, 2, true))
                    {
                        SendForcePosition(player, requestPosition, false);
                        CheckMovementConflicts(player);
                        player.Position = requestPosition;
                        player.isTaping = true;
                        player.NextTapeTime = DateTime.UtcNow.AddSeconds(3.0d);
                        NetOutgoingMessage tapetiem = server.CreateMessage();
                        tapetiem.Write((byte)99);
                        tapetiem.Write(player.ID);
                        server.SendToAll(tapetiem, NetDeliveryMethod.ReliableUnordered);
                    }
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleTapeRequest] Player @ {pmsg.SenderConnection} caused a NetException!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("There was an error while reading your packet data! (TapeRequest)");
                }
            }
        }

        /// <summary>Handles an incoming "MapMarkRequest" packet (Msg85); Either accepting the request or disconnecting the connection if the request is invalid.</summary>
        /// <param name="pmsg">Incoming message that contains the packet data.</param>
        private void HandleMapMarked(NetIncomingMessage pmsg) // Msg85 >>> Msg86
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleMapMarked", out Player player))
            {
                try
                {
                    // TOOD:: If you ever care about MapSize, then make sure these coords are not out of bounds
                    Vector2 markPosition = new Vector2(pmsg.ReadFloat(), pmsg.ReadFloat());
                    SendMapMarked(player, markPosition, player.ID);
                    if (player?.Teammates.Count > 0)
                    {
                        for (int i = 0; i < player.Teammates.Count; i++)
                        {
                            if (player.Teammates[i] == null) continue; // If this somehow happens
                            SendMapMarked(player.Teammates[i], markPosition, player.ID);
                        }
                    }
                } catch (NetException netEx)
                {
                    Logger.Failure($"[HandleMapMarked] Player @ {pmsg.SenderConnection} caused a NetException!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("There was an error while reading your packet data! (H.MapMark)");
                }
            }
        }

        // Msg 86 | "Map Marked" -- Sent to players who marked something on their map. Also should be sent to their teamamtes
        private void SendMapMarked(Player player, Vector2 coords, short senderID)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)86);    // Byte  | MsgID (86)
            msg.Write(coords.x);    // Float | MarkX
            msg.Write(coords.y);    // Float | MarkY
            msg.Write(senderID);    // Short | PlayerID [who sent this marker; only really utilized by teammates]
            server.SendMessage(msg, player.Sender, NetDeliveryMethod.ReliableUnordered);
        }

        private void HandleTeammatePickupRequest(NetIncomingMessage pmsg) // Msg80 >> Msg81
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleTeammatePickupRequest", out Player player))
            {
                if (!player.IsPlayerReal()) return;
                try
                {
                    short teammateID = pmsg.ReadInt16();
                    if (!TryPlayerFromID(teammateID, out Player teammate))
                    {
                        Logger.Failure($"[HandleTeammatePickupRequest] Couldn't find teammate: {teammateID}");
                        return;
                    }
                    if (!player.Teammates.Contains(teammate)) return;
                    teammate.ReviverID = player.ID;
                    teammate.ReviveTime = DateTime.UtcNow.AddSeconds(6);
                    teammate.isBeingRevived = true;
                    NetOutgoingMessage msg = server.CreateMessage();
                    msg.Write((byte)81);
                    msg.Write(player.ID);
                    msg.Write(teammate.ID);
                    server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
              
                } catch (NetException netEx)
                {
                    Logger.Failure($"[HandleTeammatePickupRequest] Player @ {pmsg.SenderConnection} caused a NetException!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("There was an error while reading your packet data! (TeamPickupStart)");
                }
            }
        }

        /// <summary>
        /// Sends a "Player-Revived" packet to all NetPeers using the provided paramters. NO server-side setting of required fields.
        /// </summary>
        /// <param name="ressingID">ID of the player who is reviving.</param>
        /// <param name="downedID">ID of the player who has been revived.</param>
        private void SendTeammateRevived(short ressingID, short downedID) // Msg83
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)83);    // Byte | MsgID (83)
            msg.Write(ressingID);   // Short | RevierID
            msg.Write(downedID);    // Short | RevieeID
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        // Msg84 | "Player Downed" -- Sent in team-based modes when the Player should've died, but they still have teammates alive.
        private void HandlePlayerDowned(Player player, short attackerID, short wepaonID)
        {
            if (!IsServerRunning()) return;
            // Server-Side Vars
            player.HP = 100;
            player.isDown = true;
            player.isBeingRevived = false;
            player.NextDownDamageTick = DateTime.UtcNow.AddSeconds(1);
            player.TimesDowned++;
            // Net Message
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)84);   // Byte  | MsgID (84)
            msg.Write(player.ID);  // Short | Downed PlayerID
            msg.Write(attackerID); // Short | Killer PlayerID [-2 = SSG; any other is nothing, or a PlayerID]
            msg.Write(wepaonID);   // Short | WeaponID / DamageSourceID [-3 Barrel; -2 Hamsterballs; -1 = None; 0+ Weapons]
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        //r[87] > s[111]
        /*private void GSH_HandleDeployedTrap(NetIncomingMessage message) // unfinished :/
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)111);
            msg.Write(getPlayerID(message.SenderConnection));
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }*/

        /// <summary>
        /// Cancels the provided Player's reload. Sets server-side variables AND sends the "EndReload" packet to all NetPeers.
        /// </summary>
        /// <param name="player">Player to end reloading for.</param>
        private void SendCancelReload(Player player) // Msg90 >> Msg91
        {
            player.isReloading = false;
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)91);    // Byte  | MsgID (91)
            msg.Write(player.ID);   // Short | PlayerID
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Sends a "TeammateLeftLobby" packet to all teammates of LeavingPlayer. Each teammate will also have LeavingPlayer removed from their list server-side.
        /// </summary>
        /// <param name="player">The leaving player.</param>
        private void SendTeammateDisconnected(Player player) // Msg95
        {
            if (!IsServerRunning() || player?.Teammates.Count == 0) return;
            // Go through LeavingPlayer's Teammates
            for (int i = 0; i < player.Teammates.Count; i++)
            {
                if (player.Teammates[i] == null) continue; // This shouldn't ever happen, but in case it somehow does this should solve it
                player.Teammates[i].Teammates.Remove(player);

                NetOutgoingMessage msg = server.CreateMessage(3);
                msg.Write((byte)95);    // Byte  | MsgID (95)
                msg.Write(player.ID);   // Short | LeavingTeammateID
                server.SendMessage(msg, player.Teammates[i].Sender, NetDeliveryMethod.ReliableUnordered);
            }
        }

        /// <summary>
        /// Sends a dummy/ping packet to all NetPeers.
        /// </summary>
        private void SendDummyMessage() // Msg97
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage dummy = server.CreateMessage();
            dummy.Write((byte)97);
            server.SendToAll(dummy, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Sends a NetMessage to all connected clients that states a player with the provided PlayerID has finished/stopped taping their armor.
        /// </summary>
        private void SendPlayerEndTape(Player player) // Msg100
        {
            player.isTaping = false;
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)100);   // Byte  | MsgID (100)
            msg.Write(player.ID);   // Short | PlayerID
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Sends a NetMessage to all connected clients that a player with the provided PlayerID has had their parachute-mode updated.
        /// </summary>
        private void SendParachuteUpdate(short aID, bool aIsDiving) // [probs]Msg108 >> Msg109
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)109); // Byte  | MsgID (109)
            msg.Write(aID);       // Short | PlayerID
            msg.Write(aIsDiving); // Bool  | isDiving
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        // Msg112 | "Player Pings" -- Current pings for each Player in the match
        private void SendCurrentPlayerPings() // Msg112
        {
            if (_players.Length == 0 || !IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)112);
            msg.Write((byte)GetValidPlayerCount());
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null) continue;
                msg.Write(_players[i].ID);  // Short | PlayerID
                msg.Write((ushort)(_players[i].LastPingTime * 1000f));
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        #region Msg20 Variation Methods
        /// <summary>
        /// Sends a "SpawnItem" packet to all NetPeers containing data on a new Health Juice LootItem to spawn.
        /// </summary>
        /// <param name="drinkCount">Amount of juice this item gives.</param>
        /// <param name="position">Position to spawn this LootItem.</param>
        private void SendNewJuiceItem(short drinkCount, Vector2 position)
        {
            _totalLootCounter++; // Increment LootCounter...
            _lootItems.Add(_totalLootCounter, new LootItem(LootType.Juice, $"Health Juice-{drinkCount}", 0, (byte)drinkCount, position));
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)20);                // Byte   | Header / MsgID << 20
            msg.Write(_totalLootCounter);       // Int    | LootID
            msg.Write((byte)LootType.Juice);    // Byte   | LootType >> Juice
            msg.Write(drinkCount);              // Short  | Amount of Juice
            msg.Write(position.x);              // Float  | PosX 1
            msg.Write(position.y);              // Float  | PosY 1
            msg.Write(position.x);              // Float  | PosX 2
            msg.Write(position.y);              // Float  | PosY 2
            msg.Write((byte)0);                 // Byte   | Not sure... All am know is it's needed
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Sends a "SpawnItem" packet to all NetPeers containing data on a new armor LootItem to spawn.
        /// </summary>
        /// <param name="armorLevel">Maximum ticks this armor has.</param>
        /// <param name="armorTicks">Amount of ticks left on this armor.</param>
        /// <param name="position">Position to spawn this LootItem.</param>
        private void SendNewArmorItem(byte armorLevel, byte armorTicks, Vector2 position)
        {
            _totalLootCounter++; // Increment LootCounter... | Armor LootItem:: Item.Rarity = Tier; Item.GiveAmount = ArmorTicks
            _lootItems.Add(_totalLootCounter, new LootItem(LootType.Armor, $"Armor-{armorTicks}/{armorLevel}", armorLevel, armorTicks, position));
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)20);                // Byte   | Header / MsgID << 20
            msg.Write(_totalLootCounter);       // Int    | LootID
            msg.Write((byte)LootType.Armor);    // Byte   | LootType
            msg.Write((short)armorTicks);       // Short  | # of Ticks Left
            msg.Write(position.x);              // Float  | PosX 1
            msg.Write(position.y);              // Float  | PosY 1
            msg.Write(position.x);              // Float  | PosX 2
            msg.Write(position.y);              // Float  | PosY 2
            msg.Write(armorLevel);              // Byte   | Armor Tier
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Sends a "SpawnItem" packet to all NetPeers containing data on a new Tape LootItem to spawn.
        /// </summary>
        /// <param name="tapeCount">Amount of tape to spawn (cannot exceeed 255).</param>
        /// <param name="position">Position to spawn this LootItem.</param>
        private void SendNewTapeItem(short tapeCount, Vector2 position)
        {
            if (tapeCount > byte.MaxValue) tapeCount = byte.MaxValue; // TODO -- If you ever make GiveAmount NOT a byte, and a short or something; change this
            else if (tapeCount < 0) tapeCount = 0;
            _totalLootCounter++; // Increment LootCounter...
            _lootItems.Add(_totalLootCounter, new LootItem(LootType.Tape, $"Tape (x{tapeCount})", 0, (byte)tapeCount, position));
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)20);                // Byte   | Header / MsgID << 20
            msg.Write(_totalLootCounter);       // Int    | LootID
            msg.Write((byte)LootType.Tape);     // Byte   | LootType >> Tape
            msg.Write(tapeCount);               // Short  | Amount of Tape to give
            msg.Write(position.x);              // Float  | PosX 1
            msg.Write(position.y);              // Float  | PosY 1
            msg.Write(position.x);              // Float  | PosX 2
            msg.Write(position.y);              // Float  | PosY 2
            msg.Write((byte)0);                 // Byte   | Unsure... Clipsize? If you remove, it breaks spawning these.
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Sends a "SpawnItem" packet to all NetPeers contianing data on a new throwable LootItem to spawn.
        /// </summary>
        /// <param name="weaponIndex">Index in AllWeaponsArray.</param>
        /// <param name="giveAmount">Amount in this pile.</param>
        /// <param name="position">Position to spawn this LootItem.</param>
        private void SendNewThrowableItem(int weaponIndex, byte giveAmount, Vector2 position)
        {
            _totalLootCounter++; // Increment LootCounter...
            _lootItems.Add(_totalLootCounter, new LootItem(WeaponType.Throwable, _weapons[weaponIndex].Name, 0, giveAmount, weaponIndex, position));
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)20);                // Byte   | Header / MsgID << 20
            msg.Write(_totalLootCounter);       // Int    | LootID
            msg.Write((byte)LootType.Weapon);   // Byte   | LootType >> Weapon (which should be 0)
            msg.Write((short)weaponIndex);      // Short  | WeaponIndex
            msg.Write(position.x);              // Float  | PosX 1
            msg.Write(position.y);              // Float  | PosY 1
            msg.Write(position.x);              // Float  | PosX 2
            msg.Write(position.y);              // Float  | PosY 2
            msg.Write(giveAmount);              // Byte   | Ammo in Clip?
            msg.Write(0.ToString());
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Sends a "SpawnItem" packet to all NetPeers contianing data on a new gun LootItem to spawn.
        /// </summary>
        /// <param name="weaponIndex">Index in AllWeaponsArray for this Weapon.</param>
        /// <param name="rarity">Rarity of this Weapon.</param>
        /// <param name="ammoInClip">Amount of ammo in this Weapon's "clip".</param>
        /// <param name="position">Position to spawn this LootItem.</param>
        private void SendNewGunItem(short weaponIndex, byte rarity, byte ammoInClip, Vector2 position)
        {
            _totalLootCounter++; // Increment LootCounter...
            _lootItems.Add(_totalLootCounter, new LootItem(_weapons[weaponIndex].WeaponType, _weapons[weaponIndex].Name, rarity, ammoInClip, weaponIndex, position));
            // Make MSG
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)20);                // Byte   | Header / MsgID << 20
            msg.Write(_totalLootCounter);       // Int    | LootID
            msg.Write((byte)LootType.Weapon);   // Byte   | LootType >> Weapon (which should be 0)
            msg.Write(weaponIndex);             // Short  | WeaponIndex
            msg.Write(position.x);              // Float  | PosX 1
            msg.Write(position.y);              // Float  | PosY 1
            msg.Write(position.x);              // Float  | PosX 2
            msg.Write(position.y);              // Float  | PosY 2
            msg.Write(ammoInClip);              // Byte   | Ammo in Clip
            msg.Write(rarity.ToString());       // String | Rarity? What the fart...
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Sends a "SpawnItem" packet to all NetPeers containing data on a new ammo LootItem to spawn.
        /// </summary>
        /// <param name="ammoType">Ammo Type to spawn.</param>
        /// <param name="ammoCount">Amount of ammo to spawn.</param>
        /// <param name="position">Position to spawn this LootItem.</param>
        private void SendNewAmmoItem(byte ammoType, byte ammoCount, Vector2 position)
        {
            _totalLootCounter++; // Increment LootCounter... | Ammo LootItem:: Item.Rarity = Type;
            _lootItems.Add(_totalLootCounter, new LootItem(LootType.Ammo, $"Ammo-{ammoType}", ammoType, ammoCount, position));
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)20);                // Byte   | Header / MsgID << 20
            msg.Write(_totalLootCounter);       // Int    | LootID
            msg.Write((byte)LootType.Ammo);     // Byte   | LootType << (ammo)
            msg.Write((short)ammoCount);        // Short  | Type of Ammo
            msg.Write(position.x);              // Float  | PosX 1
            msg.Write(position.y);              // Float  | PosY 1
            msg.Write(position.x);              // Float  | PosX 2
            msg.Write(position.y);              // Float  | PosY 2
            msg.Write(ammoType);                // Byte   | Amount of Ammo to spawn
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }
        #endregion Msg20 Variation Methods

        /// <summary>
        /// Sends NetMessages to all NetPeers containing the packets that will stop drinking, taping, and emoting.
        /// </summary>
        private void CheckMovementConflicts(Player player) // Player fields are reset within each of these methods.
        {
            SendPlayerEndDrink(player);
            SendPlayerEndTape(player);
            SendPlayerEndedEmoting(player);
        }
        private void HandleClientDisconnect(NetIncomingMessage pmsg)
        {
            if (TryIndexFromConnection(pmsg.SenderConnection, out int index))
            {
                NetOutgoingMessage msg = server.CreateMessage(4);
                msg.Write((byte)46);            // Byte  | MsgID (46)
                msg.Write(_players[index].ID);  // Short | DC PlayerID
                msg.Write(false);               // Bool  | isGhostMode (?) -- will always be false... for now -- todo!
                server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);

                SendTeammateDisconnected(_players[index]);
                _availableIDs.Insert(0, _players[index].ID);
                _players[index] = null; // Find a better way to give pIDs
                //SortPlayerEntries();
                isSorted = false;
            }
        }

        #region Load LevelData
        /// <summary>
        /// Attempts to load all level-data related files and such to fill the server's own level-data related variables.
        /// </summary>
        private void LoadSARLevel() // This will become quite the mess.
        {
            Logger.Header("[LoadSARLevel] Attempting to load SAR level data...");
            // Make sure this isn't called twice. Results in everything getting re-initialized. Which isn't awesome possum...
            if (svd_LevelLoaded) throw new Exception("LoadSARLevel has already been called. You cannot call this method multiple times.");

            // Get JSONNode
            string mapLoc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\datafiles\EarlyAccessMap1.txt";
            JSONNode LevelJSON = JSONNode.LoadFromFile(mapLoc);
            //Logger.Success("[LoadSARLevel] Successfully located LevelData file!");

            // use this if you wanna print out all the keys for whatever reason
            /*foreach (string JSONKey in LevelJSON.Keys)
            {
                Logger.testmsg(JSONKey);
            }*/

            // Item LootItems Tiles
            // TODO -- Better item position placement
            #region Loot
            // Find LootItems SpawnSpots
            int lootSpotsNormal = 0;
            int lootSpotsGood = 0;
            int lootSpotsBot = 0;
            if (LevelJSON["lootSpawns"] != null) lootSpotsNormal = LevelJSON["lootSpawns"].Count;
            if (LevelJSON["lootSpawnsGood"] != null) lootSpotsGood = LevelJSON["lootSpawnsGood"].Count;
            if (LevelJSON["lootSpawnsNoBot"] != null) lootSpotsBot = LevelJSON["lootSpawnsNoBot"].Count;
            if (lootSpotsBot > 0) Logger.missingHandle("[LoadSARLevel] lootSpawnsNoBot actually contains entries for once. Is this intentional?");
            int totalSpawnSpots = lootSpotsNormal + lootSpotsGood + lootSpotsBot;

            // Load Tile Spots -- Could be better. Am tired though
            Vector2[] tileSpots = new Vector2[totalSpawnSpots];
            for (int n = 0; n < lootSpotsNormal; n++)
            {
                tileSpots[n] = new Vector2(LevelJSON["lootSpawns"][n]["x"].AsFloat, LevelJSON["lootSpawns"][n]["y"].AsFloat);
            }
            for (int g = 0; g < lootSpotsGood; g++)
            {
                int ind = g + lootSpotsNormal;
                tileSpots[ind] = new Vector2(LevelJSON["lootSpawnsGood"][g]["x"].AsFloat, LevelJSON["lootSpawnsGood"][g]["y"].AsFloat);
            }

            // Setup variables for genration
            MersenneTwister mersenneTwister = new MersenneTwister((uint)_lootSeed);
            _lootItems = new Dictionary<int, LootItem>();
            _totalLootCounter = 0;

            // Stuff for RNG
            int currentLootID;  // For whatever reason, LootItems isn't like Coconuts/Hamsterball; where IDs are always in order. IDs can have huge leaps.
            bool doBetterGen;   // Whether to have a higher chance to get better loot 
            uint rngNum;        // Number returned on an RNG attempt
            uint minGenNum;     // Mini value that can be generated...
            // RNG Stuff Still-- This is used for generating Weapons
            List<short> WeaponsByFrequency = new List<short>();
            for (int i = 0; i < _weapons.Length; i++)
            {
                for (int j = 0; j < _weapons[i].SpawnFrequency; j++)
                {
                    WeaponsByFrequency.Add(_weapons[i].JSONIndex);
                }
            }

            // Go through every loot tile and try spawning something in!
            for (int i = 0; i < totalSpawnSpots; i++)
            {
                // Set LootIDs!
                currentLootID = _totalLootCounter;
                _totalLootCounter++; // always increments no matter way ig
                doBetterGen = false;
                minGenNum = 0U;

                // See if this tile is a better loot tile...
                if (i >= lootSpotsNormal)
                {
                    doBetterGen = true;
                    minGenNum = 20U;
                }
                // Now roll!
                rngNum = mersenneTwister.NextUInt(minGenNum, 100U);

                // Figure out what to do/spawn
                if (rngNum <= 33.0) continue; // 33 or less? You get NOTHING! (maybe) (maybe also || (rngNum > 59 && rngNum <= 60))?
                if (rngNum > 33.0 && rngNum <= 47.0) // Drinks
                {
                    byte juiceAmount = 40;

                    if (doBetterGen) minGenNum = 15U;
                    rngNum = mersenneTwister.NextUInt(minGenNum, 100U);
                    if (rngNum <= 55) juiceAmount = 10;
                    else if (rngNum <= 89) juiceAmount = 20;

                    LootItem drinkLoot = new LootItem(LootType.Juice, $"Health Juice-{juiceAmount}", 0, juiceAmount, tileSpots[i]);
                    _lootItems.Add(currentLootID, drinkLoot);
                }
                else if (rngNum <= 59.0) // Armor
                {
                    if (doBetterGen) minGenNum = 24U;
                    rngNum = mersenneTwister.NextUInt(minGenNum, 100U);

                    byte armorTier = 3;
                    if (rngNum <= 65.0) armorTier = 1;
                    else if (rngNum <= 92.0) armorTier = 2;

                    // LootItems.Rarity = armor tier; LootItems.GiveAmout = amount of armor ticks.
                    LootItem armorLoot = new LootItem(LootType.Armor, $"Armor-{armorTier}", armorTier, armorTier, tileSpots[i]);
                    _lootItems.Add(currentLootID, armorLoot);
                } else if (rngNum > 60.0 && rngNum <= 66.0) // Tape
                {
                    LootItem tapeLoot = new LootItem(LootType.Tape, "Tape (x1)", 0, 1, tileSpots[i]);
                    _lootItems.Add(currentLootID, tapeLoot);
                }
                else if (rngNum > 66.0) // Weapon Generation toiemm!! -- The .GiveAmount property becomes the amount to give/amount of ammo the gun has.
                {
                    // Find le weapon
                    rngNum = mersenneTwister.NextUInt(0U, (uint)WeaponsByFrequency.Count);
                    int leGenIndex = WeaponsByFrequency[(int)rngNum];
                    Weapon foundWeapon = _weapons[leGenIndex];

                    // Make Le Weapon
                    if (foundWeapon.WeaponType == WeaponType.Gun)
                    {
                        // Figure out rarity
                        byte rarity = 0;
                        if (doBetterGen) minGenNum = 22U;
                        minGenNum = mersenneTwister.NextUInt(minGenNum, 100U);
                            // See if should gen higher-rarity item...
                        if (minGenNum > 58.0 && minGenNum <= 80.0) rarity = 1;
                        else if (minGenNum > 80.0 && minGenNum <= 91.0) rarity = 2;
                        else if (minGenNum > 91.0 && minGenNum <= 97.0) rarity = 3;
                            // Verify rarity isn't too high nor low...
                        if (rarity > foundWeapon.RarityMaxVal) rarity = foundWeapon.RarityMaxVal;
                        if (rarity < foundWeapon.RarityMinVal) rarity = foundWeapon.RarityMinVal;
                        // Make gun...
                        LootItem gunLoot = new LootItem(WeaponType.Gun, foundWeapon.Name, rarity, (byte)foundWeapon.ClipSize, foundWeapon.JSONIndex, tileSpots[i]);
                        _lootItems.Add(currentLootID, gunLoot);
                        // Spawn in ammo as welll

                        float[] tmp = new float[] { -7f, 7f }; // where to move the ammo lol; not sure where the middle is. 7 seems OK
                        for (int ammoSpawned = 0; ammoSpawned < 2; ammoSpawned++)
                        {
                            currentLootID = _totalLootCounter;
                            _totalLootCounter++;

                            // LootItems.GiveAmount stays the same, BUT! LootItems.Rarity is the ammo type!!
                            Vector2 ammoSpot = new Vector2(tileSpots[i].x + tmp[ammoSpawned], tileSpots[i].y);
                            LootItem ammoLoot = new LootItem(LootType.Ammo, $"Ammo-{foundWeapon.AmmoType}", foundWeapon.AmmoType, foundWeapon.AmmoSpawnAmount, ammoSpot);
                            _lootItems.Add(currentLootID, ammoLoot);
                        }

                    } else if (foundWeapon.WeaponType == WeaponType.Throwable)
                    {
                        LootItem throwLoot = new LootItem(WeaponType.Throwable, foundWeapon.Name, 0, foundWeapon.SpawnSizeOverworld, foundWeapon.JSONIndex, tileSpots[i]);
                        _lootItems.Add(currentLootID, throwLoot);

                    }
                }
            }
            //Logger.Success("[LoadSARLevel] Loaded all LootItems successfully.");
            // End of Load LootItems
            #endregion Loot 

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
            #endregion Coconuts

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
            _hamsterballs = new Dictionary<int, Hamsterball>(hampterballs);
            for (int i = 0; i < hampterballs; i++)
            {
                if (hampterTwist.NextUInt(0U, 100U) > 55.0)
                {
                    _hamsterballs.Add(hTrueID, new Hamsterball((short)hTrueID, new Vector2(vehicleNode[i]["x"].AsFloat, vehicleNode[i]["y"].AsFloat)));
                    hTrueID++;
                }
            }
            //Logger.Success("[LoadSARLevel] Successfully generated the hamsterball list.");
            #endregion Hamsterballs

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

            // -- Load Campfire Spots --
            #region campfires
            if (LevelJSON["campfires"] == null) throw new Exception("No such key \"campfires\" in loaded LevelJSON.");
            _campfires = new Campfire[LevelJSON["campfires"].Count];
            for (int i = 0; i < _campfires.Length; i++)
            {
                _campfires[i] = new Campfire(LevelJSON["campfires"][i]["x"].AsFloat, LevelJSON["campfires"][i]["y"].AsFloat);
            }
            #endregion campfires
            // -- End of Campfire spots --


            // -- Load MoleCrate SpawnSpots
            #region moleSpots
            if (LevelJSON["moleSpawns"] == null) throw new Exception("No such key \"moleSpawns\" in loaded LevelJSON.");
            _moleSpawnSpots = new Vector2[LevelJSON["moleSpawns"].Count];
            for (int i = 0; i < _moleSpawnSpots.Length; i++)
            {
                _moleSpawnSpots[i] = new Vector2(LevelJSON["moleSpawns"][i]["x"].AsFloat, LevelJSON["moleSpawns"][i]["y"].AsFloat);
            }
            //Logger.DebugServer($"[LoadSARLevel] [OK] -- Loaded moleSpawns without error. Count: {_moleSpawnSpots.Length}");

            #endregion moleSpots
            // -- End of MoleCrate SpawnSpots

            // End of Loading in Files
            GC.Collect();
            svd_LevelLoaded = true; // Pretty obvious. just a little flag saying we indeed are finished with all this.
            Logger.Success("[LoadSARLevel] Finished without encountering any errors.");
        }
        #endregion Load LevelData

        #region player list methods

        /// <summary>
        /// Sorts the playerlist by moving null entries to the bottom. The returned list is not in sequential order (by ID).
        /// </summary>
        private void SortPlayerEntries() // TODO -- Maybe remove?
        {
            //Logger.Warn("[PlayerList Sort-Null] Attempting to sort the PlayerList...");
            if (isSorting) return;
            isSorting = true;
            Player[] temp_plrlst = new Player[_players.Length];
            int newIndex = 0;
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null)
                {
                    temp_plrlst[newIndex] = _players[i];
                    newIndex++;
                }
            }
            _players = temp_plrlst;
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
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null) count++;
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
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null && _players[i].Name.ToLower() == searchName) // Have to lowercase the comparison each time. Which really sucks man :(
                {
                    outID = _players[i].ID;
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
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null && _players[i].Name.ToLower() == searchName) // searchID is int, ID is short.
                {
                    player = _players[i];
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
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null && _players[i].ID == searchID) //searchID is int, ID is short.
                {
                    returnedIndex = _players[i].ID;
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
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null && _players[i].ID == searchID) //searchID is int, ID is short.
                {
                    returnedPlayer = _players[i];
                    return true;
                }
            }
            returnedPlayer = null;
            return false;
        }

        /// <summary>
        /// Traverses the Server's Player list array in search of the Index at which the provided NetConnection occurrs.
        /// </summary>
        /// <returns>True if the NetConnection is found; False if otherwise.</returns>
        private bool TryIndexFromConnection(NetConnection netConnection, out int returnedIndex)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null && _players[i].Sender == netConnection) // searchID is int, ID is short.
                {
                    returnedIndex = (short)i;
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
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null && _players[i].Sender == netConnection) // searchID is int, ID is short.
                {
                    player = _players[i];
                    return true;
                }
            }
            player = null;
            return false;
        }

        /// <summary>
        /// Attempts to locate a Player in the PlayerList using the provided string. Calls TryPlayerFromName / TryPlayerFromID.
        /// </summary>
        /// <param name="search">String used in this search.</param>
        /// <param name="player">Output Player object.</param>
        /// <returns>True and outputs a Player if found; False and null if otherwise.</returns>
        public bool TryPlayerFromString(string search, out Player player)
        {
            bool found;
            if (int.TryParse(search, out int searchID)) found = TryPlayerFromID(searchID, out player);
            else found = TryPlayerFromName(search, out player);
            return found;
        }
        #endregion

        /// <summary>
        /// Attempts to load the File at the provided Path as a JSONArray.
        /// </summary>
        /// <param name="loc">Location of the file to load</param>
        /// <returns>The File's read text as a JSONArray object.</returns>
        private JSONArray LoadJSONArray(string loc) // TESTING -- Hopefully can move this elsewhere at some point.
        {
            string txt = File.ReadAllText(loc);
            return (JSONArray)JSONNode.Parse(txt);
        }

        /// <summary>
        /// Checks if the provided NetConnection is in the PlayerList.
        /// </summary>
        /// <param name="netConnection">NetConnection to search for.</param>
        /// <param name="callLoc">Provided location to print if failure.</param>
        /// <param name="player">Returning Player.</param>
        /// <returns>True and found Player if the NetConnection is in the list; False and NULL if otherwise.</returns>
        private bool VerifyPlayer(NetConnection netConnection, string callLoc, out Player player)
        {
            bool isInList = TryPlayerFromConnection(netConnection, out player);
            if (!isInList)
            {
                Logger.Failure($"[{callLoc}] Player @ {netConnection} was NOT in the PlayerList!");
                netConnection.Disconnect("Invalid action! You are not in the PlayerList!");
            }
            return isInList;
        }

        /// <summary>
        /// Determines whether the provided index is valid in this Match's Weapon list.
        /// </summary>
        /// <param name="index">Index to validate.</param>
        /// <returns>True if the index is valid; False is otherwise.</returns>
        private bool ValidWeaponIndex(int index) // Could maybe make public if commands ever added / use external thing to handle them.
        {
            return index >= 0 && index < _weapons.Length;
        }

        /// <summary>
        /// Removes the provided HamsterballID from _hamsterballs. If a Player is tied to it, their ID is reset to -1.
        /// </summary>
        /// <param name="ballIndex">Index of this Hamsterball in _hamsterballs.</param>
        private void DestroyHamsterball(int ballIndex)
        {
            if (!_hamsterballs.ContainsKey(ballIndex)) return;
            Logger.DebugServer($"Destroying Hamsterball[{ballIndex}]...");
            if (_hamsterballs[ballIndex].CurrentOwner != null)
            {
                SendExitHamsterball(_hamsterballs[ballIndex].CurrentOwner.ID, (short)ballIndex, _hamsterballs[ballIndex].CurrentOwner.Position);
                Logger.Header($"Current Owner: {_hamsterballs[ballIndex].CurrentOwner.Name}\nOld Vehicle ID: {_hamsterballs[ballIndex].CurrentOwner.VehicleID}");
                _hamsterballs[ballIndex].CurrentOwner.ResetHamsterball();
                Logger.Header($"New Vehicle ID: {_hamsterballs[ballIndex].CurrentOwner.VehicleID}");
                Logger.Header($"Actual ID: {_players[_hamsterballs[ballIndex].CurrentOwner.ID].VehicleID}");
            }
            _hamsterballs.Remove(ballIndex);
            Logger.Basic($"[{DateTime.UtcNow}] Hamsterball[{ballIndex}] has been removed.");
        }
        
        /// <summary> Returns whether this Match's NetServer is still running or not.</summary>
        /// <returns>True if the NetServer's status is "running"; False is otherwise.</returns>
        public bool IsServerRunning()
        {
            return server?.Status == NetPeerStatus.Running;
        }
    }
}