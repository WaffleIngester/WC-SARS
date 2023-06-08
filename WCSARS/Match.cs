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
        private Dictionary<int, Coconut> _coconutList;
        private Dictionary<int, Hamsterball> _hamsterballs;
        private Campfire[] _campfires;
        private Weapon[] _weapons = Weapon.GetAllWeaponTypes();
        private byte[] _maxAmmo = new byte[] { 120, 30, 90, 25, 25 }; // smg, shotgun, ak, sniper, dart   // todo - dynamically load ammo

        // Actually unsorted
        private bool isSorting, isSorted; // TODO -- Unsure whether these are necessariy
        private List<Trap> _traps;

        // -- Super Skunk Gas --
        private float _ssgTickRateSeconds = 1f;
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
        private short _maxMoleCrates; // Maximum # of MoleCrates allowed in a match.
        private MoleCrate[] _moleCrates; // An array of MoleCrate objects which is the amount of active moles/crates available. The Length is that of Match._maxMoleCrates.
        // -- MoleCrate Crate Stuff --

        // Giant Eagle
        private GiantEagle _giantEagle = new GiantEagle(new Vector2(0f, 0f), new Vector2(4248f, 4248f));
        private bool _isFlightActive = false;

        // -- Healing Values --
        private float _healPerTick = 4.75f; // 4.75 health/drinkies every 0.5s according to the SAR wiki 7/21/22
        private float _healRateSeconds = 0.5f; // 0.5s
        //private byte _tapePerCheck; // Add when can config file
        private float _coconutHeal = 5f;
        private float _campfireHealPer = 4f;
        private float _campfireHealRateSeconds = 1f;
        private float _bleedoutRateSeconds = 1f; // bleed-out rate when downed [SAR default: 1s]
        private byte _resHP = 25;                // HP remaining on downed players that just got picked up 

        // -- Dartgun-Related things --
        private int _ddgMaxTicks = 12; // DDG max amount of Damage ticks someone can get stuck with
        private int _ddgAddTicks = 4; // the amount of DDG ticks to add with each DDG shot
        private float _ddgTickRateSeconds = 0.6f; // the rate at which the server will attempt to make a DDG DamageTick check
        private int _ddgDamagePerTick = 9;
        private List<Player> _poisonDamageQueue; // List of PlayerIDs who're taking skunk damage > for cough sound -- 12/2/22
                                                 //public List<Player> PoisonDamageQueue { get => _poisonDamageQueue; } // Added / unused as of now (12/23/22)

        // -- Level / RNG-Related --
        //private int _lootSeed, _coconutSeed, _vehicleSeed; // Spawnable Item Generation Seeds
        private MersenneTwister _serverRNG = new MersenneTwister((uint)DateTime.UtcNow.Ticks);
        private bool svd_LevelLoaded = false; // Likely able to remove this without any problems
        private SARLevel _level;

        // Lobby Stuff
        private bool _hasMatchStarted = false;
        private bool _isMatchFull = false;
        private double _lobbyRemainingSeconds;
        //public double LobbyRemainingTime { get => _lobbyRemainingSeconds; }

        //mmmmmmmmmmmmmmmmmmmmmmmmmmmmm (unsure section right now
        private bool _canCheckWins = false;
        private bool _hasPlayerDied = true;
        private bool _safeMode = true; // This is currently only used by the /gun ""command"" so you can generate guns with abnormal rarities
        private const int MS_PER_TICK = 41; // (1000ms / 24t/s == 41)

        public Match(int port, string ip) // Original default constructor
        {
            // Initialize LootGenSeeds
            /*_lootSeed = 351301;
            _coconutSeed = 5328522;
            _vehicleSeed = 9037281;*/
            LoadSARLevel(351301, 5328522, 9037281);
            /*
            //Logger.Warn($"DateTime Ticks: {DateTime.UtcNow.Ticks}");
            _lootSeed = (int)_serverRNG.NextUInt(0, (uint)DateTime.UtcNow.Ticks);
            //Logger.Warn($"LootSeed: {_lootSeed}");
            _coconutSeed = (int)_serverRNG.NextUInt(0, (uint)_lootSeed * (uint)DateTime.UtcNow.Ticks);
            //Logger.Warn($"CocoSeed: {_coconutSeed}");
            _vehicleSeed = (int)_serverRNG.NextUInt(0, (uint)_coconutSeed * (uint)_coconutSeed);
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

            //TODO - finish setting this up at some point
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
            _bleedoutRateSeconds = cfg.BleedoutRateSeconds;
            _resHP = cfg.ResurrectHP;

            // Set Dartgun stuff -- TBH could just use the stats found in the Dartgun...
            _ddgMaxTicks = cfg.MaxDartTicks;
            _ddgTickRateSeconds = cfg.DartTickRate;
            _ddgDamagePerTick = cfg.DartPoisonDamage;
            _ssgTickRateSeconds = cfg.SuperSkunkGasTickRate;

            // Others
            _canCheckWins = !cfg.InfiniteMatch;
            _safeMode = cfg.Safemode;
            isSorting = false;
            isSorted = true;
            _maxMoleCrates = cfg.MaxMoleCrates;
            _moleCrates = new MoleCrate[_maxMoleCrates];
            _lobbyRemainingSeconds = cfg.LobbyTime;
            _gamemode = cfg.Gamemode;
            _traps = new List<Trap>(64);

            // Load the SAR Level and stuff!
            int srngLoot = cfg.LootSeed;
            int srngCoconut = cfg.CocoSeed;
            int srngHamster = cfg.HampterSeed;
            if (!cfg.useConfigSeeds)
            {
                MersenneTwister twistItUp = new MersenneTwister((uint)DateTime.UtcNow.Ticks);
                srngLoot = (int)twistItUp.NextUInt(0u, uint.MaxValue);
                srngCoconut = (int)twistItUp.NextUInt(0u, uint.MaxValue);
                srngHamster = (int)twistItUp.NextUInt(0u, uint.MaxValue); 
            }
            Logger.Warn($"[Match] Using Seeds: LootSeed: {srngLoot}; CoconutSeed: {srngCoconut}; HampterSeed: {srngHamster}");
            LoadSARLevel((uint)srngLoot, (uint)srngCoconut, (uint)srngHamster);

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
            Logger.Basic("[Match.ServerNetLoop] Network thread started!");
            NetIncomingMessage msg;
            while (IsServerRunning())
            {
                //Logger.DebugServer($"[{DateTime.UtcNow}] Waiting to receive message.");
                server.MessageReceivedEvent.WaitOne(5000); // Halt thread until NetServer (_server) receives a message OR 5s has passed.
                //Logger.DebugServer($"[{DateTime.UtcNow}] Message has been received.");
                while ((msg = server?.ReadMessage()) != null)
                {
                    switch (msg.MessageType)
                    {
                        case NetIncomingMessageType.Data:
                            try
                            {
                                HandleMessage(msg);
                            }
                            catch (Exception ex)
                            {
                                Logger.Failure($"[HandleMessage - Error] Unhandled, unanticipated, exception has occurred!\n{ex}");
                            }
                            break;
                        case NetIncomingMessageType.StatusChanged:
                            switch (msg.SenderConnection.Status)
                            {
                                case NetConnectionStatus.Connected:
                                    Logger.Success($"[NCS.Connected] New Client connected! Wahoo! Sender Address: {msg.SenderConnection}");
                                    NetOutgoingMessage acceptMsgg = server.CreateMessage(2);
                                    acceptMsgg.Write((byte)0);
                                    acceptMsgg.Write(true);
                                    server.SendMessage(acceptMsgg, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
                                    break;
                                case NetConnectionStatus.Disconnected:
                                    HandleClientDisconnect(msg); // todo - in-match dc players need to stick around instead of dipping
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
                            Logger.Failure("-- NetworkError - EPIC BLUNDER! --\n> " + msg.ReadString());
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
                    UpdateStunnedPlayers();

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
                    UpdateStunnedPlayers();
                    check_DDGTicks(); // STILL TESTING -- (as of: 12/2/22)
                    svu_CheckCoughs(); // NEW TEST FROM 12/2/22 UPDATE

                    //advanceTimeAndEventCheck();

                    // SSG
                    UpdateSSGWarningTime();
                    UpdateSafezoneRadius();
                    CheckSkunkGas();

                    // Others
                    UpdateGiantEagle();
                    CheckMoleCrates();
                    CheckCampfiresMatch();
                    UpdateTraps();

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
                _isFlightActive = true;
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
                    msg.Write(_players[i].HealthJuice);
                    msg.Write(_players[i].SuperTape);
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
                if (_players[i].HealthJuice == 0 || _players[i].HP >= 100)
                {
                    SendPlayerEndDrink(_players[i]);
                    continue;
                }
                // Heal Section
                float hp = _healPerTick;
                if ((hp + _players[i].HP) > 100) hp = (100 - _players[i].HP);
                if ((_players[i].HealthJuice - hp) < 0) hp = _players[i].HealthJuice;
                _players[i].HP += (byte)hp;
                _players[i].HealthJuice -= (byte)hp;
                _players[i].NextHealTime = DateTime.UtcNow.AddSeconds(_healRateSeconds);
                if (_players[i].HP >= 100 || _players[i].HealthJuice == 0) SendPlayerEndDrink(_players[i]);
            }
        }

        private void UpdatePlayerTaping() // Appears OK
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null || !_players[i].isTaping) continue;
                if (DateTime.UtcNow > _players[i].NextTapeTime) // isTaping *should* ONLY get set if tape-checks pass.
                {
                    _players[i].SuperTape -= 1;
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
                if (_players[i].isBeingRevived && (DateTime.UtcNow >= _players[i].ReviveTime)) RevivePlayer(_players[i]);
                else if (!_players[i].isBeingRevived && (DateTime.UtcNow >= _players[i].NextBleedTime))
                {
                    _players[i].NextBleedTime = DateTime.UtcNow.AddSeconds(1);
                    test_damagePlayer(_players[i], 2 + (2 * _players[i].TimesDowned), _players[i].LastAttackerID, _players[i].LastWeaponID);
                    // Please see: https://animalroyale.fandom.com/wiki/Downed_state
                }
            }
        }
        
        private void UpdateStunnedPlayers() // Appears OK
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null || !_players[i].isStunned) continue;
                if (DateTime.UtcNow >= _players[i].StunEndTime)
                {
                    _players[i].WalkMode = 1;
                    _players[i].isStunned = false;
                    SendPlayerDataChange(_players[i]);
                }
            }
        }

        // Held together with glue and duct tape -- improvement opporunity
        private void UpdateTraps()
        {
            for (int i = 0; i < _traps.Count; i++)
            {
                _traps[i].RemainingTime -= (float)DeltaTime.TotalSeconds;
                if (_traps[i].RemainingTime <= 0)
                {
                    _traps.Remove(_traps[i]);
                    continue;
                }

                // bandage fix to prevent Player's getting damaged instantly when Entering>Leaving>Entering-agian with a skunk nade
                if (_traps[i].TrapType == TrapType.SkunkNade)
                {
                    if (_traps[i].HitPlayers.Count > 0)
                    {
                        foreach (Player plr in _traps[i].HitPlayers.Keys)
                        {
                            if (plr == null) continue;
                            if (!Vector2.ValidDistance(_traps[i].Position, plr.Position, _traps[i].EffectRadius, true)) _traps[i].HitPlayers.Remove(plr);
                        }
                    }
                }
                // go through all trap entries and see whether any players are nearby to "activate" the traps
                for (int j = 0; j < _players.Length; j++)
                {
                    if (_players[j] == null || !_players[j].IsPlayerReal()) continue; // todo - vial speedbost + vial speedboost for teammates without killing them
                    if (Vector2.ValidDistance(_traps[i].Position, _players[j].Position, _traps[i].EffectRadius, true))
                    {
                        switch (_traps[i].TrapType)
                        {
                            case TrapType.Banana:
                                // if vehicleID == -1, then they're in a hamsterball. Msg88 handles removing the trap in this case
                                if (_players[j].VehicleID != -1) continue;

                                // player likely isn't in a hamsterball, so stun them
                                _players[j].Stun();
                                SendGrenadeFinished(_traps[i].OwnerID, _traps[i].ThrowableID, _traps[i].Position);
                                if (!Vector2.ValidDistance(_players[j].Position, _traps[i].Position, 2f, true)) SendForcePosition(_players[j], _traps[i].Position);
                                _traps.Remove(_traps[i]);
                                return; // spaghetti fix; probably works idrk
                            case TrapType.SkunkNade:
                                if (_players[j].isGodmode || _players[j].IsPIDMyTeammate(_traps[i].OwnerID)) continue;
                                if (_traps[i].HitPlayers.ContainsKey(_players[j]))
                                {
                                    if (_traps[i].HitPlayers[_players[j]] > DateTime.UtcNow) continue;
                                    _traps[i].HitPlayers[_players[j]] = DateTime.UtcNow.AddSeconds(0.6f);
                                    test_damagePlayer(_players[j], 13, _traps[i].OwnerID, _traps[i].WeaponID);
                                    if (!_poisonDamageQueue.Contains(_players[j])) _poisonDamageQueue.Add(_players[j]);
                                }
                                else _traps[i].HitPlayers.Add(_players[j], DateTime.UtcNow.AddSeconds(0.6f));
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Revives the provided player utilizing the information stored within their fields.
        /// </summary>
        /// <param name="player">Player to revive.</param>
        private void RevivePlayer(Player player)
        {
            SendPickupFinished(player.SaviourID, player.ID);
            if (TryPlayerFromID(player.SaviourID, out Player saviour)) saviour.SaviourFinishedRessing();
            player.DownResurrect(_resHP);
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
        private void svu_CheckCoughs() // appears to work fine; todo - cleanup ?
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
                        CreateSafezone(620, 720, 620, 720, 6000, 3000, 60, 72);
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
                _players[i].hasEjected = false;
                _players[i].Position = _giantEagle.Start;

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
                        if (_players[j] == null || !_players[j].IsPlayerReal()) continue;
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

        private void CheckSkunkGas()
        {
            // todo - SSG damage that scales with each circle
            if (!isSkunkGasActive || !IsServerRunning()) return;
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null || !_players[i].IsPlayerReal() || _players[i].NextGasTime > DateTime.UtcNow) continue;
                Vector2 deltaMag = _players[i].Position - new Vector2(CurrentSafezoneX, CurrentSafezoneY);
                if (deltaMag.magnitude >= CurrentSafezoneRadius && !_players[i].isGodmode)
                {
                    _players[i].NextGasTime = DateTime.UtcNow.AddSeconds(_ssgTickRateSeconds);
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

        // Updates the current server-side "Giant Eagle" position. If any Players are still in flight, they will forcibly be ejected.
        private void UpdateGiantEagle()
        {
            if (!IsServerRunning() || !_isFlightActive || !_hasMatchStarted) return;
            _giantEagle.UpdatePosition((float)DeltaTime.TotalSeconds);
            if (_giantEagle.HasReachedEnd)
            {
                _isFlightActive = false;
                for (int i = 0; i <_players.Length; i++)
                {
                    if (_players[i] == null || _players[i].hasEjected) continue; // Null or Ejected already
                    else SendForcePosition(_players[i], _giantEagle.End, true); // No more chill in eagle.
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
                    HandleChatMessage(msg);
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
                case 92:
                    // v0.90.2? | Seems like Server can't force-cancel reloads. The client reloading ignores it; despite everyone else seeing such
                    // Perhaps in the future that is fixed, or there's another way to fix it in this current version. Or maybe it's just that way...
                    HandleReloadFinished(msg);
                    break;

                    // Msg32 -- Client Landing Finished --> (optional) Msg8 [if spot is invalid, player is forced to a valid spot]
                case 32:
                    HandlePlayerLanded(msg);
                    break;

                    // Msg36 -- Client Request Throwable Start --> Msg37 -- Server Confirm Throw Request
                case 36:
                    {
                        //Logger.Basic("msg 36");
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
                                Logger.Failure($"[ThrowableInitiateReq] Player @ {msg.SenderConnection} caused a NetException!\n{netEx}");
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
                        //Logger.Basic("msg 38");
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
                                    if ((player.LootItems[2].GiveAmount - 1) <= 0) player.LootItems[2] = new LootItem(player.LootItems[2].LootID, LootType.Collectable, "NONE", 0, 0, new Vector2(0, 0));
                                    else player.LootItems[2].GiveAmount -= 1;
                                    player.ThrowableCounter++;
                                    player.ThrownNades.Add(player.ThrowableCounter, new Projectile(grenadeID, 0, targetX, targetY, 0f));
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
                                Logger.Failure($"[ThrowableStartingReq] Player @ {msg.SenderConnection} caused a NetException!\n{netEx}");
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
                    HandleGrenadeFinished(msg);
                    break;

                    // Msg44 -- Spectator Update Request >>> (potentially) Msg78 Update Player Spectator Count
                case 44:
                    HandleSpectatorRequest(msg); // appears OK [v0.90.2]
                    break;

                    // Msg47 -- Initiate Healing Request >>> Msg48 -- Player Initiated Healing
                case 47: // Appears OK
                    HandleHealingRequest(msg);
                    break;

                case 51: // Client - I'm Requesting Coconut Eaten
                    HandleCoconutRequest(msg);
                    break;

                    // Msg53 -- Cut Grass Request >>> Msg54 -- Confirm Grass Cut | OK v0.90.2
                case 53: // Appears OK
                    HandleGrassCutRequest(msg);
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
                case 66: // TODO:: Valid EmoteID
                    HandleEmoteRequest(msg);
                    break;

                    // TODO -- Cleanup/ Separate handle method
                case 70: // Molecrate Open Request v0.90.2
                    HandleMolecrateOpenRequest(msg);
                    break;

                    // TODO -- Cleanup
                case 72:
                    HandleDoodadDestroyed(msg);
                    break;

                    // Msg74 -- Minigun Winding-up Request [ACT: "attackWindup", but has only been seen used by the Minigun? (is used in Bow & Sparrow update?)]
                case 74: // Appears OK [6/6/23]
                    HandleAttackWindUp(msg);
                    break;

                    // Msg76 -- Minigun Winddown Request [ACT: "attackWinddown", but has only been seen used by the Minigun? (is used in Bow & Sparrow update?)]
                case 76: // Appears OK [6/6/23]
                    HandleAttackWindDown(msg);
                    break;

                    // Msg80 -- Teammate Pickup Request
                case 80:
                    HandleTeammatePickupRequest(msg);
                    break;

                    // Msg85 -- Mark Map Request
                case 85:
                    HandleMapMarked(msg);
                    break;

                    // Msg87 -- Trap Deployment Finish Request
                case 87:
                    HandleTrapDeployed(msg);
                    break;

                    // Msg88 -- Vehicle Hit Banana Request
                case 88: // Appears OK [6/6/23]
                    HandleVehicleHitBanana(msg);
                    break;

                    // Msg90 -- Client Request Reload Cancel --> Msg91 -- Server Confirm Reload Canceled 
                case 90: // If Player is a Good Noodle, they send this before server has to check. If they're NOT a Good Noodle, they don't :[
                    if (VerifyPlayer(msg.SenderConnection, "ReloadCancelRequest", out player)) SendCancelReload(player);
                    break;

                    // Msg97 -- Dummy >> Send back another dummy (Msg99)
                case 97:
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
                // Read some values
                string playFabID = msg.ReadString();
                msg.ReadString(); // Unity Analytics Session Ticket; Don't want it, but must read to advance
                //string uAnalyticsSessionTicket = msg.ReadString(); // OK v0.90.2
                bool fills = msg.ReadBoolean();
                msg.ReadString(); // PlayFab Session Ticket; Don't want it, but must read to advance
                //string playFabSessionTicket = msg.ReadString();
                int partyCount = msg.ReadByte();

                // TODO: start making the client real / do some checking
                /*Client newClient = new Client(msg.SenderConnection, playFabID);
                newClient.Fills = fills;
                if (partyCount > 0)
                {
                    int TEST_COUNT = partyCount + GetValidPlayerCount();
                    Logger.DebugServer($"Calc Count: {TEST_COUNT} : length: {_players.Length}");
                    if ( TEST_COUNT> _players.Length)
                    {
                        msg.SenderConnection.Disconnect("Not enough slots for your entire party.");
                        return;
                    }
                    newClient.PartyMemberPlayFabIDs = new string[partyCount];
                    for (int i = 0; i < partyCount; i++) newClient.PartyMemberPlayFabIDs[i] = msg.ReadString();
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
                NetOutgoingMessage acceptMsg = server.CreateMessage(2);
                acceptMsg.Write((byte)2);
                acceptMsg.Write(true);
                server.SendMessage(acceptMsg, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
                Logger.Success($"[ServerAuthentiate] [OK] Sent {msg.SenderConnection} an accept message!");
            } catch (NetException netEx)
            {
                Logger.Failure($"[AuthenticationRequest] Player @ {msg.SenderConnection} caused a NetException!\n{netEx}");
                msg.SenderConnection.Disconnect($"There was an error while reading your packet data.");
            }
        }

        private void HandleIncomingPlayerRequest(NetIncomingMessage msg) // Msg3 >> Msg4
        {
            if (TryPlayerFromConnection(msg.SenderConnection, out Player p)) // todo - cleanup
            {
                Logger.Failure($"[HandleIncomingPlayerRequest] [Error] There already exists a Player object with incoming connection {msg.SenderConnection}!");
                return;
            }
            try
            {
                // read sent character data --  maybe-todo - verify all these ids and such; however... this requires more data files!
                string steamName = msg.ReadString(); // have to mod game to do this
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
                // end of data read v0.90.2
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
                    //Logger.DebugServer("Incoming PlayFabID: " + iPlayFabID);
                    newPlayer.PlayFabID = iPlayFabID; // todo - better playFabID handling; this is just temporary.
                    int playerDataCount = _playerData.Count; // For whatever reason, it is slightly faster to cache the length of lists than to call Count
                    bool doesDataExist = false;
                    for (int j = 0; j < playerDataCount; j++)
                    {
                        if ((_playerData[j]["playfabid"] != null) && (_playerData[j]["playfabid"] == iPlayFabID))
                        {
                            if (_playerData[j]["dev"]) _players[i].isDev = _playerData[j]["dev"];
                            if (_playerData[j]["mod"]) _players[i].isMod = _playerData[j]["mod"];
                            if (_playerData[j]["founder"]) _players[i].isFounder = _playerData[j]["founder"];
                            doesDataExist = true;
                            break;
                        }
                    }

                    // todo - this but cooler and also async probably
                    if (!doesDataExist)
                    {
                        Logger.DebugServer("had to add player!");
                        JSONNode newPData = JSON.Parse($"{{playfabid:\"{iPlayFabID}\",name:\"{steamName}\",dev:false,mod:false,founder:false}}");
                        _playerData.Add(newPData);
                        string baseloc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                        JSONNode.SaveToFile(_playerData, baseloc + @"\player-data.json");
                    } // end of stored data

                    // Teammates | todo - fills
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
                    SendMatchInformation(msg.SenderConnection, assignID); // server done storing data; send next message in sequence
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
            // TODO:: Gallery Targets & MatchUUID
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)4);                 // 1 Byte   |  MessageID 
            msg.Write(assignedID);              // 2 Short  |  AssignedID
            // RNG Seeds
            msg.Write(_level.LootSeed);         // 4 Int  |  LootGenSeed -- INT expected; sending UINT; game converts int back to uint anyways soo
            msg.Write(_level.CoconutSeed);      // 4 Int  |  CocoGenSeed -- INT expected; sending UINT; game converts int back to uint anyways soo
            msg.Write(_level.HamsterballSeed);  // 4 Int  |  VehicleGenSeed -- INT expected; sending UINT; game converts int back to uint anyways soo
            // Match / Lobby Info...
            msg.Write(_lobbyRemainingSeconds);  // 8 Double  |  LobbyTimeRemaining
            msg.Write("yerhAGJ");               // V String  |  MatchUUID
            msg.Write(_gamemode);               // V String  |  Gamemode [solo, duo, squad]
            // Flight Path
            msg.Write(_giantEagle.Start.x);     // 4 Float | FlightPath - StartX
            msg.Write(_giantEagle.Start.y);     // 4 Float | FlightPath - StartY
            msg.Write(_giantEagle.End.x);       // 4 Float | FlightPath - StartX
            msg.Write(_giantEagle.End.y);       // 4 Float | FlightPath - StartY
            // Gallery Targets Positions
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
                    for (int j = 0; j < 6; j++)              // Short[] | PlayerEmotes: Always 6 in v0.90.2
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
            if (!_hasMatchStarted) return; // Shouldn't get this request unless match has started.
            if (VerifyPlayer(pmsg.SenderConnection, "HandleEjectRequest", out Player player))
            {
                if (!player.hasEjected)
                {
                    if (Vector2.ValidDistance(player.Position, _giantEagle.Position, 8f, true)) SendForcePosition(player, player.Position, true);
                    else SendForcePosition(player, _giantEagle.Position, true);
                } else Logger.Failure($"[HandleEjectRequest] Player  @ {pmsg.SenderConnection} tried ejecting but they already have!");
            }
        }

        /// <summary>
        /// Sends a NetMessage to all NetPeers which forces the provided Player to the specified position with the provided parachute mode.
        /// </summary>
        /// <param name="player">Player who is getting force-moved.</param>
        /// <param name="moveToPosition">Position to move this player to.</param>
        /// <param name="isParachute">Whether the player should parachute (default is False).</param>
        private void SendForcePosition(Player player, Vector2 moveToPosition, bool isParachute = false) // Msg8
        {
            // Set server-side stuff
            player.Position = moveToPosition;
            if (isParachute)
            {
                player.hasLanded = false;
                player.hasEjected = true;
                player.isDiving = false;
            }
            NetOutgoingMessage msg = server.CreateMessage(14);
            msg.Write((byte)8);          // 1 Byte  | MsgID (8)
            msg.Write(player.ID);        // 2 Short | PlayerID
            msg.Write(moveToPosition.x); // 4 Float | PositionX
            msg.Write(moveToPosition.y); // 4 Float | PositionY
            msg.Write(isParachute);      // 1 Bool  | Parachute?
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
                // note - msg14 is NEVER sent by a dead player. dead players instead use msg44!
                if (!player.isAlive) return;
                try
                {
                    // Client Data | What they *think* is true
                    float mouseAngle = pmsg.ReadInt16() / 57.295776f;
                    float posX = pmsg.ReadFloat();
                    float posY = pmsg.ReadFloat();
                    byte walkMode = pmsg.ReadByte(); // v0.90.2 OK:: do later versions use this still?
                    if (walkMode > 6)
                    {
                        Logger.missingHandle($"Unhandled walkmode? Mode#: {walkMode}");
                        walkMode = 1;
                    }
                    // Movemode-Downed | BandAid fix for playing forever thinking they're downed.
					// TODO: What happens if we just send Msg45 (PlayerDataUpdated) with the correct states here? Does that fix the problem?
                    if (player.isDown && walkMode != 5) walkMode = 5;
                    else if (!player.isDown && walkMode == 5)
                    {
                        Logger.DebugServer($"Player {player.Name} keeps saying they're down but they ain't. what an idiot, am I right?");
                        walkMode = 1;
                    }
                    // Movemode-Stunned | BandAid fix for forever stuns (hopefully)
                    if (player.isStunned && walkMode != 6) walkMode = 6;
                    else if (!player.isStunned && walkMode == 6)
                    {
                        Logger.DebugServer($"[PositionUpdate] {player} thinks they are still stun-locked, but they aren't!");
                        walkMode = 1;
                        player.WalkMode = walkMode;
                        SendPlayerDataChange(player);
                    }

                    // Server-Side Data | Setting & Checks
                    // TODO: Better check for valid sent player position-- are they in a wall, are they going too fast, etc. (wait actually that's kinda it tbh)
                    /*Vector2 sentPostion = new Vector2(posX, posY);
                    if (Vector2.ValidDistance(sentPostion, player.Position, 10f, true)) player.Position = sentPostion; // If within 10units of prev spot, this sentPos is valid.
                    else SendForcePosition(player, player.Position, false);*/
                    player.Position = new Vector2(posX, posY);
                    player.MouseAngle = mouseAngle;
                    player.WalkMode = walkMode;

                    // For "special" Movement-Modes
                    if (walkMode == 2) // Believe this is jumping?
                    {
                        CheckMovementConflicts(player);
                        if (player.isReloading) SendCancelReload(player);
                    }
					// Movemode - Hamsterball
                    if (walkMode == 4 && player.VehicleID != -1)
                    {
                        float vehicleX = (float)(pmsg.ReadInt16() / 10f);
                        float vehicleY = (float)(pmsg.ReadInt16() / 10f);
                        player.HamsterballVelocity = new Vector2(vehicleX, vehicleY);
                    }
					// Ending Emotes if Player is emoting...
                    // TODO:: if memeMode
                    if (player.isEmoting && !Vector2.ValidDistance(player.Position, player.EmotePosition, 4f, true)) SendPlayerEndedEmoting(player);

                    // Cancel pickup if this player moved too far away | if the player beleives hard enough they can break this
                    // TODO:: this breaks sometimes still... gotta handle downed teammates differently, most likely
                    if (player.isReviving && TryPlayerFromID(player.RevivingID, out Player whoImRessing) && !Vector2.ValidDistance(player.Position, whoImRessing.Position, 3f, true)) HandlePickupCanceled(player);
                    if (player.isBeingRevived && TryPlayerFromID(player.SaviourID, out Player mySaviour) && !Vector2.ValidDistance(player.Position, mySaviour.Position, 3f, true)) HandlePickupCanceled(player);

                    //if (player.isDrinking && !Vector2.ValidDistance(player.Position, player.HealPosition, 4f, true)) SendPlayerEndDrink(player);
                    //if (player.isTaping && !Vector2.ValidDistance(player.Position, player.HealPosition, 4f, true)) SendPlayerEndTape(player);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandlePositionUpdate] Player @ {pmsg.SenderConnection} caused a NetException!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("There was an error while reading your packet data! (HandlePositionUpdate)");
                }
            }
        }

        // Msg15 | "Player Died" --- Sent to all connected NetPeers whenever a Player dies.
        private void SendPlayerDeath(short playerID, Vector2 gravespot, short killerID, short weaponID)
        {
            NetOutgoingMessage msg = server.CreateMessage(15);
            msg.Write((byte)15);    // 1 Byte  | MsgID (15)
            msg.Write(playerID);    // 2 Short | Dying PlayerID
            msg.Write(gravespot.x); // 4 Float | GraveSpawnX
            msg.Write(gravespot.y); // 4 Float | GraveSpawnY
            msg.Write(killerID);    // 2 Short | SourceID
            msg.Write(weaponID);    // 2 Short | WeaponID
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        // Handles the killing of Players
        private void HandlePlayerDied(Player player)
        {
            player.isAlive = false;
            CheckMovementConflicts(player);
            SendPlayerDeath(player.ID, player.Position, player.LastAttackerID, player.LastWeaponID);
            // summon loot
            Vector2 position = player.Position;
            _hasPlayerDied = true;
            if (player.ArmorTier > 0) SendSpawnedLoot(_level.NewLootArmor(player.ArmorTier, player.ArmorTapes, position));
            if (player.HealthJuice > 0) SendSpawnedLoot(_level.NewLootJuice(player.HealthJuice, position));
            if (player.SuperTape > 0) SendSpawnedLoot(_level.NewLootTape(player.SuperTape, position));
            for (int i = 0; i < player.Ammo.Length; i++)
            {
                if (player.Ammo[i] > 0) SendSpawnedLoot(_level.NewLootAmmo((byte)i, player.Ammo[i], position));
            }
            for (int j = 0; j < player.LootItems.Length; j++)
            {
                if (player.LootItems[j] == null) continue;
                if (player.LootItems[j].LootType != LootType.Collectable) SendSpawnedLoot(_level.NewLootWeapon(player.LootItems[j].WeaponIndex, player.LootItems[j].Rarity, player.LootItems[j].GiveAmount, position));
            }
        }

        /// <summary>
        /// Handles an incoming message marked as a "PlayerAttackRequest" packet. If in match, Players are kicked for invalid actions.
        /// </summary>
        /// <param name="amsg">Incoming message to handle.</param>
		// TODO: likely needs another refresher/ utilize the new VerifyPlayer method... although that may need some improvements as well...
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
                    //Logger.DebugServer($"Sent SlotID: {slotID}");
                    //Logger.DebugServer($"AttackHandle Ammo NOW: {player.LootItems[slotID].GiveAmount}");
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
                    if (hitDestructible)
                    {
                        Vector2 hitPos = new Vector2((float)amsg.ReadInt16(), (float)amsg.ReadInt16());
                        if (_level.TryDestroyingDoodad(hitPos, out Doodad[] hitDoodads, _hasMatchStarted))
                        {
                            for (int i = 0; i < hitDoodads.Length; i++)
                            {
                                SendDestroyedDoodad(hitDoodads[i]);
                            }
                        }
                    }
                    // Make sure AttackIDs line up
                    short attackID = amsg.ReadInt16();
                    player.AttackCount++;
                    //Logger.DebugServer($"AttackID: {attackID}; Plr.AttackCount: {player.AttackCount}");
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

        // I feel fairly confident that this function is working as it should now; so it want to rename this simply to "DamagePlayer" you could.
        // However, in the event teammates are worked on/ damage is calculated differently... yeah this needs updating again!
        private void test_damagePlayer(Player player, int damage, short sourceID, short weaponID)
        {
            if (!player.isAlive || player.isGodmode) return; // Don't worry about logging this...
            player.SetLastDamageSource(sourceID, weaponID);
            // Try and Damage
            Logger.DebugServer($"Player {player.Name} (ID: {player.ID}) Health: {player.HP}\nDamage Attempt: {damage}");
            if ((player.HP - damage) <= 0)
            {
                if (!player.isDown && player.Teammates?.Count > 0) // team-based modes; should get downed if teammates are available
                {
                    if (player.AliveNonDownTeammteCount() > 0)
                    {
                        HandlePlayerDowned(player, sourceID, weaponID);
                        return; // prevent falling onto the HandlePlayerDeath()
                    }
                    else // I ain't got no teammates
                    {
                        for (int i = 0; i < player.Teammates.Count; i++)
                        {
                            if (!player.Teammates[i].isAlive) continue; // we don't wanna hear them dying again... trust me
                            HandlePlayerDied(player.Teammates[i]); // Gahhh!
                        }
                    }
                }
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

        // Msg25 >> Msg26 OR Msg94 OR Msg106 | "Client ChatMsg Request" -- Called whenever a player sends a chat... message... what did you expect?
        private void HandleChatMessage(NetIncomingMessage pmsg)
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleChatMessage", out Player player))
            {
                if (!player.isReady)
                {
                    Logger.Failure($"[HandleChatMessage] Player sent a chat message, but they aren't marked as ready. Assuming this is an error on that part; or something else entirely is wrong.");
                    return;
                }
                try
                {
                    string text = pmsg.ReadString();
                    if (text.StartsWith("/"))
                    {
                        string[] command = text.Split(" ");
                        string responseMsg = "command executed... no info given...";
                        Logger.Warn($"Player {player} sent command \"{command[0]}\"");
                        switch (command[0])
                        {
                            case "/help": // most of these ""commands"" are just for testing out features... so they may or may not be removed at any point
                                Logger.Success("/help has been used!");
                                if (command.Length >= 2)
                                {
                                    switch (command[1])
                                    {
                                        case "help":
                                            responseMsg = "\n<<Warning: not updated often; may be out of date/wrong!>>\nProvides information on other commands.\nUsage: /help [page#] OR /help [command] (ex: /help tp)";
                                            break;
                                        case "sethp":
                                            responseMsg = "\n> Sets the provided player's HP to the provided value.\nIf no player is provided, sets the user's HP instead.\nUsage: /sethp [player] [amount] OR /sethp [amount] (ex: /sethp 0 25)";
                                            break;
                                        case "teleport":
                                            responseMsg = "\n> Teleports a player to the provided coordinaes.\nIf no player is specified, the user will be teleported.\nUsage: /teleport [player] [x] [y] OR /teleport [x] [y] (ex: /teleport 500 500)";
                                            break;
                                        case "tp":
                                            responseMsg = "\n> Teleports one player to another player.\nIf only one player is provided, the user will be teleported to them.\nUsage: /tp [playerA] [playerB] OR /teleport [player] (ex: /teleport 2 1; /teleport 2; /teleport A B";
                                            break;
                                        case "gun":
                                            responseMsg = "\n> Spawns a weapon near the user of the command.\nIf no rarity is specified, the gun will be at its lowest rarity.\nUsage: /gun [gun#] [rarity] OR /gun [gun#] (ex: /gun 5 [this spawns a common shotgun])";
                                            break;
                                        case "throwable":
                                            responseMsg = "\n> Spawns a throwable weapons near the user of the command.\nIf no amount is specified, only one (1) is spawned.\nUsage: /throwable [nade#] [amount] OR /throwable [nade#]";
                                            break;
                                        case "safemode":
                                            responseMsg = "\n> Toggles the \"safemode\" variable.\nTypically, this enables game-breaking commands to be used (notably weapons with invalid rarities [beyond legend])\nEnabling this is NOT recommended (use at your own risk).";
                                            break;
                                        case "startshow":
                                            responseMsg = "\n> Initiates a \"show\" in the Beakeasy located in Giant Eagle Landing.\nUsage: /show [show#]\n1 = BlueJayZ; 2 = KellyLarkson; 3 = LadyCawCaw; anything else and I force you to listen to BlueJayZ.";
                                            break;
                                        case "time":
                                            responseMsg = "\n> [NoArgs] Displays the amount of time remaining in the lobby.\n[WithArgs] Sets the remaining time in lobby to the provided value.\nUsage: /time OR /time [time]";
                                            break;
                                        case "safezone":
                                            responseMsg = "\n> Creates a new \"safezone\" with the provided parameters.\nJust try running it and figure it out, It's a bit complicated to explain each parameter...";
                                            break;
                                        case "divemode":
                                            responseMsg = "\n> Updates the provided player's \"divemode\".\nIf no player is specified, then the user has their dive-mode updated.";
                                            break;
                                        case "forceland":
                                            responseMsg = "\n> Forces the provided player to eject from the Giant Eagle.\nIf no player is specified, then the user is ejected.";
                                            break;
                                        case "":
                                            responseMsg = "\nI mean honestly, you're not supposed to see this lol; try inputting a command listed in /help lol";
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
                                    //"\n/ <P> [X] (optional: X)" +
                                    responseMsg = "\n(1) List of usable commads:" +
                                        "\n> /help [page#:command]" +
                                        "\n> /divemode <optional: player>  | alias: /dive" +
                                        "\n> /forceland <optional: player> | alias: /eject" +
                                        "\n> /gun [gun#] (optional: rarity)" +
                                        "\n> /safezone [circle1X] [circle1Y] [circle1Radius] [circle2X] [circle2Y] [circle2Radius] [warningTime] [approachDuration]" +
                                        "\n> /sethp <player> [amount] OR /sethp [amount]" +
                                        "\n> /startshow [show#]" +
                                        "\n> /teleport <player> [x] [y] OR /teleport [x] [y]" +
                                        "\n> /throwable [throwable#] (optional: amount)" +
                                        "\n> /time (optional: newTime)" +
                                        "\n> /togglewin" +
                                        "\n> /tp <playerA> <playerB> OR /tp <player>" +
                                        "\n> Type '/help [command]' for more information";
                                }
                                break;

                            case "/p": // you shouldn't use this unless you know what you're doing
                                for (int i = 0; i < _players.Length; i++)
                                {
                                    if (_players[i] != null) continue;
                                    _players[i] = new Player((short)i, 0, 0, 0, 0, new short[] { -1, -1, -1, -1, -1, -1 }, 0, 0, 0, 0, 0, 0, new short[] { }, new byte[] { }, "tmp", player.Sender);
                                    _players[i].Teammates.Add(player); // team tester comment 1/2
                                    _players[i].isReady = true;
                                    player.Teammates.Add(_players[i]); // team tester comment 2/2
                                    for (int j = 0; j < player.Teammates.Count; j++) 
                                    {
                                        if (player.Teammates[j] == null || player.Teammates[j] == _players[i]) continue;
                                        player.Teammates[j].Teammates.Add(_players[i]);
                                        _players[i].Teammates.Add(player.Teammates[j]);
                                    }
                                    HandleReadyReceived(pmsg);
                                    responseMsg = "added a new player...";
                                    break;
                                }
                                break;

                            case "/d":
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (TryPlayerFromString(command[1], out Player dcP))
                                    {
                                        // only s
                                        NetOutgoingMessage msg = server.CreateMessage(4);
                                        msg.Write((byte)46);            // Byte  | MsgID (46)
                                        msg.Write(dcP.ID);                 // Short | DC PlayerID
                                        msg.Write(false);               // Bool  | isGhostMode (?) -- will always be false... for now -- todo!
                                        server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);

                                        SendTeammateDisconnected(dcP);
                                        _availableIDs.Insert(0, dcP.ID);
                                        _players[dcP.ID] = null; // Find a better way to give pIDs
                                                                //SortPlayerEntries();
                                        isSorted = false;
                                        responseMsg = "";
                                    }
                                    else responseMsg = $"Could not locate player \"{command[1]}\"";
                                }
                                else responseMsg = "Missing some stuff...";
                                break;

                            case "/kick":
                                if (player.isMod || player.isDev)
                                {
                                    if (command.Length >= 2 && command[1] != "")
                                    {
                                        if (TryPlayerFromString(command[1], out Player kicker))
                                        {
                                            responseMsg = $"Kicked {kicker}.";
                                            kicker.Sender.Disconnect("You've been kicked!");
                                        }
                                        else responseMsg = $"Could not locate player \"{command[1]}\"";
                                    }
                                    else responseMsg = "Insufficient # of arguments provided! /kick takes at least one!";
                                }
                                else responseMsg = "You do not have the required permissions to use this command. You must be a DEV or MOD!";
                                break;

                            case "/ban":
                                // todo - improve & bans that expire
                                if (player.isDev)
                                {
                                    if (command.Length >= 2 && command[1] != "")
                                    {
                                        if (TryPlayerFromString(command[1], out Player banP))
                                        {
                                            string reason = "";
                                            if (command.Length >= 3 && command[2] != "") reason = command[2]; // reasons have to be one-line for now...

                                            JSONNode ban = JSON.Parse($"{{playfabid:\"{banP.PlayFabID}\",name:\"{banP.Name}\",source:\"{player.Name}\",reason:\"{reason}\"}}");
                                            _bannedPlayers.Add(ban);
                                            // dump --- todo - better dump
                                            string baseloc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                                            JSONNode.SaveToFile(_bannedPlayers, baseloc + @"\banned-players.json");
                                            responseMsg = $"Banned {banP}";
                                            if (reason == "") reason = "No reason provided.";
                                            banP.Sender.Disconnect($"\nYou've been banned from this server.\nReason: {reason}");
                                        }
                                    }
                                    else responseMsg = "Command Error! /ban [] <-- Here | No Player specified!";
                                }
                                else responseMsg = $"You must have Developer privileges to utilize that command.";
                                //string date = DateTime.UtcNow.ToString();
                                //DateTime newDateTime = DateTime.Parse(date);
                                break;

                            case "/banip":
                            case "/ipban":
                                // todo - improve & bans that expire
                                if (player.isDev)
                                {
                                    if (command.Length >= 2 && command[1] != "")
                                    {
                                        if (TryPlayerFromString(command[1], out Player banP))
                                        {
                                            string ip = banP.Sender.RemoteEndPoint.Address.ToString();
                                            string reason = "";
                                            if (command.Length >= 3 && command[2] != "") reason = command[2];

                                            JSONNode ban = JSON.Parse($"{{ip:\"{ip}\",playfabid:\"{banP.PlayFabID}\",name:\"{banP.Name}\",source:\"{player.Name}\",reason:\"{reason}\"}}");
                                            _bannedIPs.Add(ban);
                                            // dump --- todo - better dump
                                            string baseloc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                                            JSONNode.SaveToFile(_bannedIPs, baseloc + @"\banned-ips.json");
                                            responseMsg = $"Banned {banP}";
                                            if (reason == "") reason = "No reason provided.";
                                            banP.Sender.Disconnect($"\nYou've been banned from this server.\nReason: {reason}");
                                        }
                                    }
                                    else responseMsg = "Command Error! /ban [] <-- Here | No Player specified!";
                                }
                                else responseMsg = $"You must have Developer privileges to utilize that command.";
                                break;

                            case "/safemode":
                                _safeMode = !_safeMode;
                                if (_safeMode) responseMsg = "> Safemode has been ENABLED!";
                                else responseMsg = "> Safemode has been DISABLED!\n(I sure hope you know what you are doing...)";
                                break;

                            // another test thing
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
                                        SendForcePosition(player, start);
                                        break;
                                    }
                                }
                                break;
                            case "/camptest": // another test thing
                                for (int i = 0; i < _campfires.Length; i++)
                                {
                                    if (_campfires[i] == null) continue;
                                    Campfire bruh = _campfires[i];
                                    SendForcePosition(player, bruh.Position);
                                    Thread.Sleep(400);
                                }
                                break;
                            /*case "/shutdown":
                            case "/stop":
                                server.Shutdown("The server is shutting down...");
                            break;*/

                            case "/gun":
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (int.TryParse(command[1], out int reqWeaponID))
                                    {
                                        Weapon[] tmp_guns = Weapon.GetAllGuns(_weapons);
                                        if (reqWeaponID > 0 && reqWeaponID <= tmp_guns.Length) // input range: [1 to MAX] NOT [0 to MAX-1]
                                        {
                                            Weapon gun = tmp_guns[reqWeaponID - 1]; // actual gun we're working with here
                                            byte spawnRarity = gun.RarityMinVal;
                                            // custom rarity value if you'd like!
                                            if (command.Length >= 3 && command[2] != "")
                                            {
                                                if (int.TryParse(command[2], out int desiredRarity))
                                                {
                                                    if (_safeMode)
                                                    {
                                                        if (desiredRarity < gun.RarityMinVal) desiredRarity = gun.RarityMinVal;
                                                        if (desiredRarity > gun.RarityMaxVal) desiredRarity = gun.RarityMaxVal;
                                                    }
                                                    spawnRarity = (byte)desiredRarity;
                                                }
                                                else
                                                {
                                                    responseMsg = $"Command Error: /gun {reqWeaponID} [{command[2]}] <-- Here | Invalid INT!";
                                                    break;
                                                }
                                            }
                                            // spawn this gun; if 2 args, we'll use the custom rarity; otherwise... minimum rarity!
                                            SendSpawnedLoot(_level.NewLootWeapon(gun.JSONIndex, spawnRarity, (byte)gun.ClipSize, player.Position));
                                            responseMsg = $"Created an instance of \"{gun.Name}\" (ID = {gun.JSONIndex}) @ {player}!";
                                        }
                                        else responseMsg = $"Command Error: /gun [{reqWeaponID}] <-- Here | Out of Range! (range: 1 to {tmp_guns.Length})";
                                    }
                                    else responseMsg = $"Command Error: /gun [{command[1]}] <-- Here | Invalid INT!";
                                }
                                else responseMsg = "Insufficient # of arguments provided.\n\"/gun\" takes at least 1!\nUsage: /gun [gunID] OR /gun [gunID] [rarity]";
                                break;

                            case "/nade":
                            case "/throwable":
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (int.TryParse(command[1], out int reqNadeID))
                                    {
                                        Weapon[] tmp_throwables = Weapon.GetAllThrowables();
                                        if (reqNadeID > 0 && reqNadeID <= tmp_throwables.Length) // get in range: [1 to MAX ] as opposed to [1 to MAX-1]
                                        {
                                            Weapon nade = tmp_throwables[reqNadeID - 1];
                                            byte spawnAmount = 1;
                                            if (command.Length > 2 && command[2] != "")
                                            {
                                                if (int.TryParse(command[2], out int desiredAmount))
                                                {
                                                    if (_safeMode)
                                                    {
                                                        if (desiredAmount <= 0) desiredAmount = 1;
                                                        if (desiredAmount > nade.MaxCarry) desiredAmount = nade.MaxCarry;
                                                    }
                                                    spawnAmount = (byte)desiredAmount;
                                                }
                                                else
                                                {
                                                    responseMsg = $"Command Error: /throwable {reqNadeID} [{command[2]}] <-- Here | Invalid INT!";
                                                    break;
                                                }
                                            }
                                            SendSpawnedLoot(_level.NewLootWeapon(nade.JSONIndex, 0, spawnAmount, player.Position));
                                            responseMsg = $"Spawned {nade.Name} ({spawnAmount}) @ {player}.";
                                        }
                                        else responseMsg = $"Command Error: /throwable [{reqNadeID}] <-- Here | Out of Range! (range: 1 to {tmp_throwables.Length})";
                                    }
                                    else responseMsg = $"Command Error: /throwable [{command[1]}] <-- Here | Invalid INT";
                                }
                                else responseMsg = "Insufficient # of arguments provided.\n\"/throwable\" takes at least 1!\nUsage: /throwable [nadeID] OR /throwable [nadeID] [amount]";
                                break;

                            case "/hp":
                            case "/sethp":
                                Logger.Success("/sethp has been used!");
                                if (command.Length > 1)
                                {
                                    if (command.Length >= 3 && command[1] != "")
                                    {
                                        if (TryPlayerFromString(command[1], out Player targetPlayer))
                                        {
                                            if (int.TryParse(command[2], out int desiredHP))
                                            {
                                                if (desiredHP < 0 || desiredHP > 100) desiredHP = 100;
                                                targetPlayer.HP = (byte)desiredHP;
                                                responseMsg = $"Set {targetPlayer} HP to {desiredHP}.";
                                            }
                                            else responseMsg = $"Invalid HP value \"{command[2]}\".";
                                        }
                                        else responseMsg = $"Could not locate player \"{command[1]}\".";
                                    }
                                    else if (int.TryParse(command[1], out int desiredHP))
                                    {
                                        if (desiredHP < 0 || desiredHP > 100) desiredHP = 100;
                                        player.HP = (byte)desiredHP;
                                        responseMsg = $"Set {player} HP to {desiredHP}.";
                                    }
                                    else responseMsg = $"Invalid value \"{command[1]}\".";
                                }
                                else responseMsg = $"Insufficient # of arguments provided.\n\"/sethp takes at least 1!\"\nUsage: /sethp [amount] OR /sethp [player] [amount]";
                                break;

                            /*case "/async": // testing async stuff; pretty neat; just executes the method elsewhere and program gets to continue
                                Logger.Header($"{DateTime.UtcNow} ASYNC command used...");
                                Test_Awaiting(LOL);
                                Logger.Header($"{DateTime.UtcNow} I can move on with my life now");
                                responseMsg = "check the logs lol";
                                break;*/

                            case "/tele":
                            case "/teleport":
                                if (command.Length >= 3 && command[1] != "")
                                {
                                    if (command.Length >= 4)
                                    {
                                        if (TryPlayerFromString(command[1], out Player tpee))
                                        {
                                            if (float.TryParse(command[2], out float parseX) && float.TryParse(command[3], out float parseY))
                                            {
                                                Vector2 newPos = new Vector2(parseX, parseY);
                                                SendForcePosition(tpee, newPos);
                                                responseMsg = $"Teleported {tpee} to {newPos}";
                                            }
                                            else responseMsg = "There was an error processing the coordinates...";
                                        }
                                        else responseMsg = $"Could not locate player \"{command[1]}\"";
                                    }
                                    else if (command.Length == 3)
                                    {
                                        if (float.TryParse(command[1], out float parseX) && float.TryParse(command[2], out float parseY))
                                        {
                                            Vector2 newPos = new Vector2(parseX, parseY);
                                            SendForcePosition(player, newPos);
                                            responseMsg = $"Teleported {player} to {newPos}";
                                        }
                                        else responseMsg = "There was an error processing the coordinates...";
                                    }
                                }
                                else responseMsg = "Insufficient amount of arguments provided. usage: /teleport {ID} {positionX} {positionY}";
                                break;

                            case "/tp":
                                if (command.Length > 1)
                                {
                                    if (command.Length > 2)
                                    {
                                        if (TryPlayerFromString(command[1], out Player movingPlayer))
                                        {
                                            if (TryPlayerFromString(command[2], out Player targetPlayer))
                                            {
                                                SendForcePosition(movingPlayer, targetPlayer.Position);
                                                responseMsg = $"Teleported {movingPlayer} to {targetPlayer}!";
                                            }
                                            else responseMsg = $"Could not locate Player \"{command[2]}\"";
                                        }
                                        else responseMsg = $"Could not locate Player \"{command[1]}\"";
                                    }
                                    else
                                    {
                                        if (TryPlayerFromString(command[1], out Player targetPlayer))
                                        {
                                            SendForcePosition(player, targetPlayer.Position);
                                            responseMsg = $"Teleported {player} to {targetPlayer}!";
                                        }
                                        else responseMsg = $"Could not locate Player \"{command[1]}\"";
                                    }
                                }
                                else responseMsg = "Insufficient # of arguments provided.\n\"/tp\" takes 2!\nUsage: /tp [movingPlayerID] [targetPlayerID]";
                                break;

                            case "/time":
                                if (!_hasMatchStarted)
                                {
                                    if (command.Length == 1 || (command.Length >= 2 && command[1] == ""))
                                    {
                                        SendCurrentLobbyCountdown(_lobbyRemainingSeconds);
                                        responseMsg = $"The match will start in about {(int)_lobbyRemainingSeconds} seconds...";
                                    }
                                    else
                                    {
                                        if (double.TryParse(command[1], out double newTime))
                                        {
                                            _lobbyRemainingSeconds = newTime;
                                            SendCurrentLobbyCountdown(_lobbyRemainingSeconds);
                                            responseMsg = $"Updated time remaining in lobby: {newTime}";
                                        }
                                        else responseMsg = $"Invalid input \"{command[1]}\".\nTry \"/time 20\"";
                                    }
                                }
                                else responseMsg = "You cannot modify the lobby time... The match has already started!!!";
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
                                    server.SendMessage(msg, pmsg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
                                    responseMsg = "The PlayerList has been copied to your clipboard.";
                                }
                                catch (Exception except)
                                {
                                    Logger.Failure($"Command Error:\n{except}");
                                }
                                break;

                            // The ability to use this command at any point is 100% & is likely not not change for quite some time.
                            // The ability to do that is simply useful for other applications such as testing CollisionGrids and stuff
                            case "/eject":
                            case "/forceland":
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (TryPlayerFromString(command[1], out Player eject))
                                    {
                                        SendForcePosition(eject, eject.Position, true);
                                        responseMsg = $"Force-Ejected {eject} from the Giant Eagle.";
                                    }
                                    else responseMsg = $"Could not locate player \"{command[1]}\"";
                                }
                                else
                                {
                                    SendForcePosition(player, player.Position, true);
                                    responseMsg = $"Force-Ejected {player} from the Giant Eagle.";
                                }
                                break;

                            case "/down":
                            case "/forcedown":
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (TryPlayerFromString(command[1], out Player downPlayer))
                                    {
                                        HandlePlayerDowned(downPlayer, downPlayer.ID, -1);
                                        responseMsg = $"Forcibly downed player {downPlayer}.";
                                    }
                                    else responseMsg = $"Couldn't locate player \"{command[1]}\"";
                                }
                                else
                                {
                                    HandlePlayerDowned(player, player.ID, -1);
                                    // this isn't really that funny, I was just extremely bored and very frustrated with life. the bad thoughts won again.
                                    string header = $"\nAnonymous {DateTime.Today.Date.ToShortDateString()}({DateTime.Today.DayOfWeek.ToString().Substring(0, 3)}){DateTime.UtcNow.ToShortTimeString()} No.{(uint)DateTime.UtcNow.Ticks}";
                                    responseMsg = $"{header}\n>be me\n>playing wcsars\n>use /forcedown\n>\"Get fricked.\"\n>altf4\nunfunny joke. day ruined.";
                                }
                                break;

                            case "/res":
                            case "/forceres":
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (TryPlayerFromString(command[1], out Player resPlayer))
                                    {
                                        resPlayer.SaviourID = resPlayer.ID;
                                        RevivePlayer(resPlayer);
                                        responseMsg = $"Forced {resPlayer} to get picked up.";
                                    }
                                    else responseMsg = $"Couldn't locate player \"{command[1]}\"";
                                }
                                else
                                {
                                    player.SaviourID = player.ID;
                                    RevivePlayer(player);
                                    responseMsg = $"Peace be with you, {player.Name}.";
                                }
                                break;

                            case "/dive":
                            case "/divemode":
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (TryPlayerFromString(command[1], out Player diver))
                                    {
                                        diver.isDiving = !diver.isDiving;
                                        SendParachuteUpdate(diver.ID, diver.isDiving);
                                        responseMsg = $"Parachute mode for {diver} updated to {diver.isDiving}";
                                    }
                                    else responseMsg = $"Could not locate player \"{command[1]}\"";
                                }
                                else
                                {
                                    player.isDiving = !player.isDiving;
                                    SendParachuteUpdate(player.ID, player.isDiving);
                                    responseMsg = $"Parachute mode for {player} updated to {player.isDiving}";
                                }
                                break;

                            case "/starte":
                                if (_hasMatchStarted) responseMsg = "Match has already begun.";
                                else
                                {
                                    _lobbyRemainingSeconds = 0;
                                    SendCurrentLobbyCountdown(0);
                                    Thread.Sleep(100);
                                    for (int i = 0; i < _players.Length; i++)
                                    {
                                        if (_players[i] != null) SendForcePosition(_players[i], new Vector2(500, 500), true);
                                    }
                                    responseMsg = "mmm uh huh! yeah? you like that? yeah you like that buddy? yeah uh huh!"; // the eyes they are watching you
                                }
                                break;
                            // the Rebel Hideout is going to need even more work...
                            // we can clear the space easily, but the spots that need to stay's harder to figure out...
                            /*case "/tarp":
                                {
                                    Logger.Warn("tarp junk...");
                                    Int32Point[] tarpOpenSpots = _level.OpenRebelSpot();
                                    if (tarpOpenSpots == null)
                                    {
                                        responseMsg = "Couldn't open tarp. Thing's null...";
                                        break;
                                    }
                                    NetOutgoingMessage open = server.CreateMessage();
                                    open.Write((byte)114);
                                    open.Write(LOL.ID);
                                    open.Write((short)tarpOpenSpots.Length);
                                    for (int i = 0; i < tarpOpenSpots.Length; i++)
                                    {
                                        open.Write((short)tarpOpenSpots[i].x);
                                        open.Write((short)tarpOpenSpots[i].y);
                                    }
                                    open.Write((byte)1);
                                    open.Write(LOL.ID);
                                    server.SendToAll(open, NetDeliveryMethod.ReliableUnordered);

                                    // actually freeing the spots for the client... idk why but OK
                                    NetOutgoingMessage free = server.CreateMessage();
                                    free.Write((byte)115);
                                    free.Write((short)tarpOpenSpots.Length);
                                    for (int i = 0; i < tarpOpenSpots.Length; i++)
                                    {
                                        free.Write((short)tarpOpenSpots[i].x);
                                        free.Write((short)tarpOpenSpots[i].y);
                                    }
                                    server.SendToAll(free, NetDeliveryMethod.ReliableUnordered);
                                    responseMsg = "Should've opened tarp spot...";
                                    // below this is even older; can probs fix/ or delete
                                    Logger.Warn($"Tarp Spot: {_level._tarpSpawnPosition}");
                                    NetOutgoingMessage tarp2 = server.CreateMessage();
                                    tarp2.Write((byte)114);
                                    tarp2.Write(LOL.ID);
                                    tarp2.Write((short)(97 * 65));
                                    for (int x = 0; x < 97; x++)
                                    {
                                        for (int y = 0; y < 65; y++)
                                        {
                                            tarp2.Write((short)(_level._tarpSpawn.x + x));
                                            tarp2.Write((short)(_level._tarpSpawn.y + y));
                                        }
                                    }
                                    if (_level._rebelHideoutDoor == null) Logger.Failure("No barn door doodad :[");
                                    else
                                    {
                                        Logger.Warn("We have a stored barn door doodad...");
                                        for (int i = 0; i < _level._rebelHideoutDoor.HittableSpots.Length; i++)
                                        {
                                            tarp2.Write((short)_level._rebelHideoutDoor.HittableSpots[i].x);
                                            tarp2.Write((short)_level._rebelHideoutDoor.HittableSpots[i].y);
                                        }
                                    }
                                    tarp2.Write((byte)0);
                                    server.SendToAll(tarp2, NetDeliveryMethod.ReliableUnordered);

                                    NetOutgoingMessage tarp = server.CreateMessage();
                                    tarp.Write((byte)115);
                                    tarp.Write((short)(97 * 65));
                                    for (int x = 0; x < 97; x++)
                                    {
                                        for (int y = 0; y < 65; y++)
                                        {
                                            tarp.Write((short)(_level._tarpSpawn.x + x));
                                            tarp.Write((short)(_level._tarpSpawn.y + y));
                                        }
                                    }
                                    server.SendToAll(tarp, NetDeliveryMethod.ReliableUnordered);
                                }
                                break;*/

                            case "/startshow": // messing with the song played at the Beakeasy location
                                if ((command.Length > 1) && (command[1] != ""))
                                {
                                    if (int.TryParse(command[1], out int aviaryShowID))
                                    {
                                        if ((aviaryShowID < 0) || (aviaryShowID > 2)) aviaryShowID = 0;
                                        SendAviaryShow((byte)aviaryShowID);
                                        responseMsg = $"Played Aviary Show #{aviaryShowID + 1} [actualID: {aviaryShowID}]";
                                    }
                                    else responseMsg = $"Invalid value \"{command[1]}\"";
                                }
                                else responseMsg = "Insufficient # of arguments provided.\"/startshow\" takes at least 1!\nUsage: /startshow [aviaryShowID]";
                                break;

                            // Msg110 test --> Banan Praised
                            case "/pray": // msg 110
                                {
                                    NetOutgoingMessage banan = server.CreateMessage();
                                    banan.Write((byte)110); // Byte  | MsgID (110)
                                    banan.Write(player.ID);    // Short | PlayerID
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
                                    banan.Write(player.ID);    // ID for Player to give Milestone to
                                    server.SendToAll(banan, NetDeliveryMethod.ReliableOrdered);
                                    responseMsg = "Praise Banan.";
                                }
                                break;

                            case "/kill": // user can murder themself if no input other names/ fixed up some stuff
                                Logger.Success("/kill has been used!");
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (TryPlayerFromString(command[1], out Player killPlayerLOL))
                                    {
                                        if (killPlayerLOL.isAlive)
                                        {
                                            killPlayerLOL.isGodmode = false;
                                            test_damagePlayer(killPlayerLOL, killPlayerLOL.HP, -3, -1);
                                            responseMsg = $"<insert unfunny joke here> killed {killPlayerLOL}.";
                                        }
                                        else responseMsg = $"Can't kill {killPlayerLOL}, they're already dead!";
                                    }
                                    else responseMsg = $"Could not locate player \"{command[1]}\"";
                                }
                                else if (player.isAlive)
                                {
                                    player.isGodmode = false;
                                    test_damagePlayer(player, player.HP, -3, -1);
                                    responseMsg = $"<insert unfunny joke here> killed {player}.";
                                }
                                else responseMsg = $"Can't kill {player}, they're already dead LOL!";
                                break;

                            case "/godmode":
                            case "/god": // small cleanup to utilize TryPlayerFromString effectively
                                Logger.Success("/god has been used!");
                                // Yes, spamming stuff here looks stupid. cry about it-- the whole command system sucks. gotta fix it at some point!
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (TryPlayerFromString(command[1], out Player foundGod))
                                    {
                                        foundGod.isGodmode = !foundGod.isGodmode;
                                        responseMsg = $"Godmode set to {foundGod.isGodmode.ToString().ToUpperInvariant()} for {foundGod}";
                                    }
                                    else responseMsg = $"Could not locate player \"{command[1]}\"";
                                }
                                else
                                {
                                    player.isGodmode = !player.isGodmode;
                                    responseMsg = $"Godmode set to {player.isGodmode.ToString().ToUpperInvariant()} for {player}";
                                }
                                break;

                            case "/ghost":
                                if (player.isDev || player.isMod)
                                {
                                    if (command.Length >= 2 && command[1] != "")
                                    {
                                        if (TryPlayerFromString(command[1], out Player ghoster))
                                        {
                                            if (ghoster.isGhosted) break; // chat messages are ignored in ghost mode
                                            ghoster.isGhosted = true;
                                            SendGhostmodeEnabled(ghoster.Sender);
                                            SendPlayerDisconnected(ghoster.ID, true);
                                            responseMsg = $"Ghost Mode enabled for {ghoster}!";
                                        }
                                        else responseMsg = $"Could not locate player \"{command[1]}\"";
                                    }
                                    else
                                    {
                                        if (player.isGhosted) break; // chat messages are ignored in ghost mode
                                        responseMsg = "";
                                        player.isGhosted = true;
                                        SendGhostmodeEnabled(player.Sender);
                                        SendPlayerDisconnected(player.ID, true);
                                    }
                                }
                                else responseMsg = "You do not have the permissions required to use /ghost...";
                                break;

                            case "/roll": // from actual sar
                                // v1.8 format (at least) -- "<PlayerName> rolls <generatedNumber> (1-<maximum>)"
                                // so... Billy > /roll 6 --> "Billy rolls 5 (1-6)"
                                int maxRollValue = 6;
                                if (command.Length >= 2 && command[1] != "" && int.TryParse(command[1], out int desiredMaxRoll))
                                {
                                    if (desiredMaxRoll <= 0) desiredMaxRoll = 1;
                                    maxRollValue = desiredMaxRoll;
                                }
                                int roll = _serverRNG.NextInt2(1, maxRollValue);
                                SendRollMsg(player.ID, $"{player} rolls a {roll}! (1-{maxRollValue})");
                                responseMsg = "";
                                break;

                            case "/rain":
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (float.TryParse(command[1], out float rainDuration))
                                    {
                                        SendRainEvent(rainDuration);
                                        responseMsg = $"It's raining it's pouring the old man is snoring!\n(rain duration: ~{rainDuration} seconds)";
                                    }
                                    else responseMsg = $"Command Error: /rain [{command[1]}] <-- Here | Invalid FLOAT!";
                                }
                                else responseMsg = $"Command Error: /rain [] <-- Here | Insufficient # of arguments!";
                                break;

                            case "/togglewin":
                                _canCheckWins = !_canCheckWins;
                                responseMsg = $"_canCheckWins set to \"{_canCheckWins.ToString().ToUpper()}\"";
                                break;

                            case "/setdrinks":
                            case "/drinks":
                                Logger.Success("/drink has been used!");
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (command.Length > 2)
                                    {
                                        if (TryPlayerFromString(command[1], out Player modDrinkPlayer))
                                        {
                                            if (byte.TryParse(command[2], out byte setDrinksModPlayer))
                                            {
                                                modDrinkPlayer.HealthJuice = setDrinksModPlayer;
                                                responseMsg = $"Set {modDrinkPlayer}'s HealthJuiceOz to {setDrinksModPlayer}";
                                            }
                                            else responseMsg = $"Invalid input \"{command[2]}\"";
                                        }
                                        else responseMsg = $"Could not locate player \"{command[1]}\"";
                                    }
                                    else
                                    {
                                        if (byte.TryParse(command[1], out byte setJuice))
                                        {
                                            player.HealthJuice = setJuice;
                                            responseMsg = $"Set {player}'s HealthJuiceOz to {setJuice}";
                                        }
                                        else responseMsg = $"Invalid input \"{command[1]}\"";
                                    }
                                }
                                else responseMsg = "Invalid # of arguments provided.\"/drink\" requires at least 1!\nUsage: /drink [healthJuiceCount] OR /drink [playerID] [healthJuiceCount]";
                                break;

                            case "/tapes":
                                Logger.Success("/tapes has been used!");
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (command.Length >= 3)
                                    {
                                        if (TryPlayerFromString(command[1], out Player tapePlayer))
                                        {
                                            if (byte.TryParse(command[2], out byte setTapes))
                                            {
                                                tapePlayer.SuperTape = setTapes;
                                                responseMsg = $"{tapePlayer} now has {setTapes} Super Tape.";
                                            }
                                            else responseMsg = $"Command Error: /tapes {tapePlayer.Name} [{command[2]}] <-- Here | Invalid BYTE!";
                                        }
                                        else responseMsg = $"Could not locate player \"{command[1]}\"";
                                    }
                                    else if (byte.TryParse(command[1], out byte setTapes))
                                    {
                                        player.SuperTape = setTapes;
                                        responseMsg = $"{player} now has {setTapes} Super Tape.";
                                    }
                                    else responseMsg = $"Command Error: /tapes [{command[1]}] <-- Here | Invalid BYTE!";
                                }
                                else responseMsg = $"Insufficient # of arguments provided. \"/tapes takes at least 1!\"\nUsage: /tapes [amount] OR /tapes <player> [amount]";
                                break;

                            case "/armorticks":
                            case "/ticks": // this is a testing command... ; can make a give-armor command if want, it's possible
                                Logger.Success("/ticks has been used!");
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (command.Length >= 3)
                                    {
                                        if (TryPlayerFromString(command[1], out Player ticksPlayer))
                                        {
                                            if (int.TryParse(command[2], out int setTicks))
                                            {
                                                if (_safeMode)
                                                {
                                                    if (setTicks < 0) setTicks = 0;
                                                    if (setTicks > ticksPlayer.ArmorTier) setTicks = ticksPlayer.ArmorTier;
                                                }
                                                ticksPlayer.ArmorTapes = (byte)setTicks;
                                                responseMsg = $"{ticksPlayer} now has {setTicks}/{ticksPlayer.ArmorTier} ticks!";
                                            }
                                            else responseMsg = $"Command Error: /ticks {ticksPlayer.Name} [{command[2]}] <-- Here | Invalid INT!";
                                        }
                                        else responseMsg = $"Could not locate player \"{command[1]}\"";
                                    }
                                    else if (int.TryParse(command[1], out int setTicks))
                                    {
                                        if (_safeMode)
                                        {
                                            if (setTicks < 0) setTicks = 0;
                                            if (setTicks > player.ArmorTier) setTicks = player.ArmorTier;
                                        }
                                        player.ArmorTapes = (byte)setTicks;
                                        responseMsg = $"{player} now has {setTicks}/{player.ArmorTier} ticks!.";
                                    }
                                    else responseMsg = $"Command Error: /ticks [{command[1]}] <-- Here | Invalid INT!";
                                }
                                else responseMsg = $"Insufficient # of arguments provided. \"/ticks takes at least 1!\"\nUsage: /armortapes [amount] OR /armortapes <player> [amount]";
                                break;

                            case "/getpos":
                            case "/pos":
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (TryPlayerFromString(command[1], out Player pos)) responseMsg = $"{pos} is @ {pos.Position}.";
                                    else responseMsg = $"Couldn't locate player \"{command[1]}\"";
                                }
                                else responseMsg = $"{player} is @ {player.Position}.";
                                break;
                            case "/stopp": // force-canceling reloads testing... doesn't work... sorta
                                // other players see the player we cancel stopping their reload... the one we're trying to cancel ignores this, however.
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (TryPlayerFromString(command[1], out Player reloadCanceler))
                                    {
                                        SendCancelReload(reloadCanceler);
                                        responseMsg = $"Sent reload cancel request with {reloadCanceler}'s details.";
                                    }
                                    else responseMsg = $"Could not locate player \"{command[1]}\"";
                                }
                                else
                                {
                                    SendCancelReload(player);
                                    responseMsg = $"Sent reload cancel request with {player}'s details.";
                                }
                                break;
                            default:
                                Logger.Failure("Invalid command used.");
                                responseMsg = "Invalid command provided. Please see '/help' for a list of commands.";
                                break;
                        }
                        //now send response to player...
                        if (responseMsg != "") SendChatMessageWarning(player, responseMsg);
                        return;
                    }
                    bool wasToTeam = pmsg.ReadBoolean();
                    if (wasToTeam)
                    {
                        // fight me
                        for (int i = 0; i < player.Teammates?.Count; i++) SendChatMessageTeammate(player.ID, text, player.Teammates[i].Sender);
                        SendChatMessageTeammate(player.ID, text, player.Sender); // don't forget to send chat message to the person sending as well...
                    }
                    // obviously, if it's not to the team then it's an all-chat message
                    else if (player.isAlive) SendChatMessageToAll(player.ID, text);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleChatMessage - NetError] There was a NetError while processing this chat message...\n{netEx}");
                    pmsg.SenderConnection.Disconnect("There was an error while processing your data... Sorry about that (ChatMessage)");
                }
                catch (Exception ex)
                {
                    SendChatMessageWarning(player, "<<Error>> There was an error processing your chat message... Considering you're still standing, it's probably just a busted command. Check the console!");
                    Logger.Failure($"[HandleChatMessage - General Error] There was some sort of error while processing a chat message.\n\n{ex}");
                }
            }
        }

        // Msg26 | "Send ChatMsg" -- Sent whenever a client sends a chat message to public chat.
        private void SendChatMessageToAll(short playerID, string text)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(4 + text.Length);
            msg.Write((byte)26);    // 1 Byte   | MsgID (26)
            msg.Write(playerID);    // 2 Short  | PlayerID
            msg.Write(text);        // V String | ChatMsgContents
            msg.Write(false);       // 1 Bool   | ToTeam? [False]
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        // Msg26 | "Send ChatMsg - Teammates" -- Sent to the client & their teammates whenever they (OP) sends a message in team chat.
        private void SendChatMessageTeammate(short playerID, string text, NetConnection destAddress)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(4 + text.Length);
            msg.Write((byte)26);    // 1 Byte   | MsgID (26)
            msg.Write(playerID);    // 2 Short  | PlayerID (who sent it)
            msg.Write(text);        // V String | ChatMsgContents
            msg.Write(true);        // 1 Bool   | ToTeam? [True; also, only send to teammates]
            server.SendMessage(msg, destAddress, NetDeliveryMethod.ReliableUnordered);
        }

        // Msg94 | "Warning Message" -- Sent whenever a client is to receive a "warning" chat message (E.g. swearing/ has been muted)
        private void SendChatMessageWarning(Player player, string text)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(4 + text.Length);
            msg.Write((byte)94);    // 1 Byte   | MsgID (94)
            msg.Write(player.ID);   // 2 Short  | SendPlayerID (yes, still required-- also playerID can be any valid playerId in the match)
            msg.Write(text);        // V String | MessageContents
            server.SendMessage(msg, player.Sender, NetDeliveryMethod.ReliableUnordered);
        }

        // Msg27 | "Slot Update Request"
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

        // Msg 32 | "Player Landed" -- This message type is received after a player lands.
        private void HandlePlayerLanded(NetIncomingMessage pmsg) // OK-enough
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandlePlayerLanded", out Player player))
            {
                try
                {
                    bool wasLandingValid = pmsg.ReadBoolean();
                    float xDir = pmsg.ReadFloat();
                    float yDir = pmsg.ReadFloat();
                    Logger.DebugServer($"[PlayerLanded] Was landing safe? {wasLandingValid}; X:Y--[{xDir}, {yDir}]");
                    if (!wasLandingValid || !_level.IsThisPlayerSpotValid(player.Position))
                    {
                        Vector2 moveSpot = _level.FindValidPlayerPosition(player.Position, xDir, yDir); // method doesn't actually utilize x/y directions lol
                        SendForcePosition(player, moveSpot);
                    }
                    player.isDiving = false;
                    player.hasLanded = true;
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandlePlayerLanded] Player @ NetConnection \"{pmsg.SenderConnection}\" gave NetError!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("There was an error procssing your request. Message: Read past buffer size... [PlayerLand]");
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

        // Msg35 | "Weather (rain) Event" --- When this should be sent is unknown; likely random-- what is known is that this causes rain to fall (snow falls in the taundra).
        private void SendRainEvent(float duration)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(5);
            msg.Write((byte)35);    // 1 Byte | MsgID (35)
            msg.Write(duration);    // 4 Byte | Rain Duration
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        // Msg40 | "Grenade Finished Request" --> Msg41 | "Confirm Grenade Finished" -- Chain: [Msg36 to Msg37] --> [Msg38 to Msg39] --> [Msg40 to Msg41]
        private void HandleGrenadeFinished(NetIncomingMessage msg)
        {
            //Logger.Basic("Msg40 has been sent! :D");
            if (TryPlayerFromConnection(msg.SenderConnection, out Player player))
            {
                try
                {
                    float x = msg.ReadFloat();
                    float y = msg.ReadFloat();
                    float height = msg.ReadFloat();
                    short ID = msg.ReadInt16();
                    //Logger.Warn($"Grenade Height: {height}\nGrenadeID: {ID}");
                    //Logger.Warn($"Player ThrowableCount: {player.ThrowableCounter}");
                    Weapon nade = _weapons[ID];
                    
                    // todo: calculate whether any players were hit; would have to update SendGrenadeFinished though...
                    SendGrenadeFinished(player.ID, ID, new Vector2(x, y), height);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleGrenadeFinished] Player @ {msg.SenderConnection} gave NetException!\n{netEx}");
                    msg.SenderConnection.Disconnect("Error processing your request. Message: \"Error reading packet data.\"");
                }
            }
            else
            {
                Logger.Failure($"[HandleGrenadeFinished] Player @ {msg.SenderConnection} is not in the PlayerList!");
                msg.SenderConnection.Disconnect("There was an error processing your request. Message: \"Invalid Action! Not in PlayerList!\"");
            }
        }

        // Msg41 | "Grenade Finished" --- Sent whenever a grenade has landed and should explode. OR whenever a banana has been stepped on! (does appear to have an effect on skunk nades!)
        private void SendGrenadeFinished(short playerID, short thrownNadeID, Vector2 position, float height = 0f, List<short> hitIDs = null) // v0.90.2 OK [6/6/23]
        {
            int hitPlrCount = hitIDs == null ? 0 : hitIDs.Count;
            NetOutgoingMessage msg = server.CreateMessage(18 + (2 * hitPlrCount));
            msg.Write((byte)41);            // 1 Byte | MsgID (41)
            msg.Write(playerID);            // 2 Short | PlayerID (owner)
            msg.Write(thrownNadeID);        // 2 Short | ThrowableID (id in a list of total thrown nades, similar to attackCount)
            msg.Write(position.x);          // 4 Float | actionPosition.x
            msg.Write(position.y);          // 4 Float | actionPosition.y
            msg.Write(height);              // 4 Float | Height
            msg.Write((byte)hitPlrCount);   // 1 Byte | Numer of Players hit by a nade. For nanners, it's none... I think?
            for (int i = 0; i < hitPlrCount; i++)
            {
                msg.Write(hitIDs[i]);       // 2 Short | playerN.ID
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
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
                            SendChatMessageWarning(player, "mis-match with your and the server's ammo count!");
                            return; // No ammo! What the hey hey!!
                        }
                        //Logger.DebugServer("[HandleReloadRequest] All Passed!");
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

        // Msg43 | "Lobby Countdown Update" --- Sent periodically to make sure that the server and client lobby times are sync'd up!
        private void SendCurrentLobbyCountdown(double countdown) // appears OK! [v0.90.2]
        {
            NetOutgoingMessage msg = server.CreateMessage(9);
            msg.Write((byte)43);    // 1 Byte   | MsgID (43)
            msg.Write(countdown);   // 8 Double | LobbyCountdownSeconds
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        // Msg44 | "Spectator Info" --- Received whenever a dead, spectator client wishes to inform the server who they think they're spectating.
        private void HandleSpectatorRequest(NetIncomingMessage pMsg)
        {
            if (!IsServerRunning()) return;
            if (VerifyPlayer(pMsg.SenderConnection, "HandleSpectatorRequest", out Player player))
            {
                // note - clients basically have the authority over who they're spectating
                // side-note - there may be a specific packet to override this that just hasn't been found/ used correctly (similar to bananas not disappearing)
                // side-side-note - clients NEVER send this packet if they are alive, only when "dead". This includes ghosts!!
                if (player.isAlive && !player.isGhosted)
                {
                    Logger.DebugServer($"[HandleSpectatorRequest] {player} is alive and un-ghosted, yet they sent Msg44?");
                    return;
                }
                try
                {
                    // read packet data
                    float x = pMsg.ReadFloat();
                    float y = pMsg.ReadFloat();
                    short  id = pMsg.ReadInt16();
                    if (id == -1) return; // note - ghosted players & recently died players can/ will send this... doesn't seem of any use though!

                    // server-side setting and checking time
                    player.Position = new Vector2(x, y); // no genuine reason to modify the client-player's position at this point anymore honestly...
                     // note - it would appear v0.90.2's ghost mode is very basic; i.e., there is no way to click onto players and follow them around
                     // this may or may not be true however. it may be more advanced, and it is just unknown HOW to get it to work properly at this time...
                     // ...similar to how it took a while to realize DC messages played a role in enabling ghost modes
                    if (player.isGhosted) return;

                    // otherwise... any other pID should be valid
                    if (TryPlayerFromID(id, out Player spectating))
                    {
                        // is the person they're trying to spectate even alive?
                        if (spectating.isAlive)
                        {
                            // ok so everything about who they want to spectate is right... now check if the server has already acknowledge these updates
                            if (player.WhoImSpectating == spectating.ID) return;

                            // if ogSpectator is -1, requester isn't watching anyone
                            if (player.WhoImSpectating != -1)
                            {
                                if (TryPlayerFromID(player.WhoImSpectating, out Player oldSpectator))
                                {
                                    if (!oldSpectator.MySpectatorsIDs.Remove(player.ID)) Logger.Warn($"[HandleSpectatorRequest] [Warn] {player} wasn't actually in {oldSpectator}'s spectator list???");
                                    SendUpdatedSpectatorCount(oldSpectator.ID, (byte)oldSpectator.MySpectatorsIDs.Count);
                                }
                                else Logger.Warn($"[HandleSpectatorRequest] [Warn] {player} tried switching to pID \"{player.WhoImSpectating}\", but they couldn't be located!");
                            }
                            // OK they've been cleared up, now move this client to their new spectator

                            // set requester's spectating-player to what they want, and make sure to update the list
                            player.WhoImSpectating = spectating.ID;
                            if (!spectating.MySpectatorsIDs.Contains(player.ID))
                            {
                                spectating.MySpectatorsIDs.Add(player.ID);
                                SendUpdatedSpectatorCount(spectating.ID, (byte)spectating.MySpectatorsIDs.Count);
                            }
                            else Logger.Warn($"[HandleSpectatorRequest] [Warn] {player} already in {spectating}'s spectator list for some reason... whoopsies!");
                        }
                        else Logger.Warn($"[HandleSpectatorRequest] [Warn] {player} spectating {spectating}, who's dead!!");
                    }
                    // otherwise-otherwise... this requested pID should be invalid.
                    else Logger.Warn($"[HandleSpectatorRequest] [Warn] {player} requested pID \"{id}\" could not be found.");
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleSpectatorRequest] [Error] {pMsg.SenderConnection} caused a NetException!\n{netEx}");
                    pMsg.SenderConnection.Disconnect("There was an error reading your packet data. [SpectateUpdate]");
                }
            }
        }

        // Msg78 | "Spectator Count Update" -- Sent whenever a client-player has the number of spectators watching them changed.
        private void SendUpdatedSpectatorCount(short pPlayerID, byte pSpecCount) // appears OK [v0.90.2]
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(4);
            msg.Write((byte)78);    // 1 Byte  | MsgID
            msg.Write(pPlayerID);   // 2 Short | PlayerID (who everyone's watching)
            msg.Write(pSpecCount);  // 1 Byte  | # of Spectators
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
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
            msg.Write(player.HealthJuice);     // Byte  | # of Juice
            msg.Write(player.SuperTape);       // Byte  | # of Tape
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        // Msg46 | "Player Disconnected" -- Sent whenever a client disconnects... or goes into ghost mode!
        private void SendPlayerDisconnected(short pID, bool wasGhostMode = false) // appears OK [v0.90.2]
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(4);
            msg.Write((byte)46);        // 1 Byte  | MsgID (46)
            msg.Write(pID);             // 2 Short | PlayerID
            msg.Write(wasGhostMode);    // 1 Bool  | isGhostModeDC << doesn't appear to affect anything in v0.90.2
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        // Msg49 | "Player Ended Drinking" -- Sent once a player has finished drinking.
        private void SendPlayerEndDrink(Player player)
        {
            if (!IsServerRunning()) return;
            player.isDrinking = false;
            NetOutgoingMessage msg = server.CreateMessage(3);
            msg.Write((byte)49);    // 1 Byte  | MsgID (49)
            msg.Write(player.ID);   // 2 Short | PlayerID
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
        private void HandleGrassCutRequest(NetIncomingMessage pMsg)
        {
            if (VerifyPlayer(pMsg.SenderConnection, "HandleGrassCut", out Player player))
            {
                try
                {
                    // init Int16Point array...
                    int cutCount = pMsg.ReadByte();
                    Int16Point[] hitSpots = new Int16Point[cutCount];

                    // alloc values used later in the FOR loop...
                    GameGrass grass;
                    short x, y;

                    // read remaining message data; if valid grass loc, try spawning some loot.
                    // todo - ask Clogg NICELY if he'll tell us the real rng values
                    for (int i = 0; i < cutCount; i++)
                    {
                        x = pMsg.ReadInt16();
                        y = pMsg.ReadInt16();
                        hitSpots[i] = new Int16Point(x, y);
                        grass = _level.GetGrassAtSpot(x, y);
                        if (grass == null)
                        {
                            Logger.DebugServer($"[HandleGrassCut - Erorr!] {player} sent invalid spot @ {(x, y)}!");
                            continue;
                        }
                        if (!_hasMatchStarted) continue;

                        // rng & remove from list if need to; otherwise NO item drops + don't remove
                        if (!grass.Type.Rechoppable)
                        {
                            // remove from list....
                            _level.RemoveGrassFromList(grass); // not currently using any other properties so what's it matter really?
                            
                            // if it isn't rechoppable, then it's regular grass that drops loot
                            uint roll = _serverRNG.NextUInt(0, 255);
                            if (roll < 41)
                            {
                                LootItem spawnLoot;
                                roll = _serverRNG.NextUInt(0, 5);
                                switch (roll)
                                {
                                    case 0: // health juice
                                    case 1:
                                    default:
                                        spawnLoot = _level.NewLootJuice(5, new Vector2(x, y));
                                        break;

                                    case 2: // banana
                                    case 3:
                                        spawnLoot = _level.NewLootWeapon(69, 0, 1, new Vector2(x, y));
                                        break;

                                    case 4: // ammo
                                        // todo - dynamically load ammo
                                        spawnLoot = _level.NewLootAmmo((byte)_serverRNG.NextUInt(0, 5), 3, new Vector2(x, y));
                                        break;
                                }
                                SendSpawnedLoot(spawnLoot);
                                SendGrassLootFoundSound(player.Sender);
                                // meme
                                // Minigun Stats --- ID: 67; RarMin 3 (purp), RarMin 4 (gold), MaxAmmo 100
                                //SendSpawnedLoot(_level.NewLootWeapon(67, 3, 100, new Vector2(x, y)));
                            }
                        }
                    }
                    SendGrassCut(hitSpots);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleGrassCut] [Error] {pMsg.SenderConnection} caused a NetException!\n{netEx}");
                    pMsg.SenderConnection.Disconnect("There was an error reading your packet data. [GrassCut]");
                }
                catch (Exception ex)
                {
                    Logger.Failure($"General exception has been thrown!\n{ex}");
                    SendChatMessageWarning(player, "An error occurred while handing MsgID (53) request. Please check the console.");
                }
            }
        }

        // Msg54 (GrassCut) -- Sent to all other clients after a client sends a valid Msg53 (GrassCut Request)
        private void SendGrassCut(Int16Point[] grassLocs)
        {
            if (!IsServerRunning()) return;
            if (grassLocs.Length > 255) throw new ArgumentOutOfRangeException("grassLocs", "grassLocs length is greater than 255!");
            NetOutgoingMessage msg = server.CreateMessage(2 + (grassLocs.Length * 4));
            msg.Write((byte)54);                // 1 Byte | MsgID (54)
            msg.Write((byte)grassLocs.Length);  // 1 Byte | Length
            for (int i = 0; i < grassLocs.Length; i++)
            {
                msg.Write(grassLocs[i].x);      // 2 Short | Grass[n].X
                msg.Write(grassLocs[i].y);      // 2 Short | Grass[n].Y
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        // Msg59 -- (GrassLootSoundCue) -- Sent to a client whenever loot is found within cut grass.
        private void SendGrassLootFoundSound(NetConnection dest)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(1);
            msg.Write((byte)59);    // Byte | MsgID (59)
            server.SendMessage(msg, dest, NetDeliveryMethod.ReliableUnordered); // ok we're done
        }

        /// <summary>Sends a "Player Exit Hamsterball" packet to all connected NetPeers using the provided parameters.</summary>
        /// <param name="playerID">ID of the Player exiting a Hamsterball.</param>
        /// <param name="hamsterballID">ID of the Hamsterball being left.</param>
        /// <param name="exitPosition">Position the Hamsterball will be placed at after exiting.</param>
        private void SendExitHamsterball(short playerID, short hamsterballID, Vector2 exitPosition)
        {
            NetOutgoingMessage msg = server.CreateMessage(13);
            msg.Write((byte)58);        // 1 Byte  | MsgID (58)
            msg.Write(playerID);        // 2 Short | PlayerID
            msg.Write(hamsterballID);   // 2 Short | HamsterballID
            msg.Write(exitPosition.x);  // 4 Float | ExitPositionX
            msg.Write(exitPosition.y);  // 4 Float | ExitPositionY
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
            NetOutgoingMessage msg = server.CreateMessage(11);
            msg.Write((byte)61);        // 1 Byte  | MsgID (61)
            msg.Write(attackerID);      // 2 Short | AttackerID
            msg.Write(targetID);        // 2 Short | AttackerID
            msg.Write(didKill);         // 1 Bool  | DidKillPlayer
            msg.Write(attackerBallID);  // 2 Short | HamsterballID (Attacker)
            msg.Write(targetBallID);    // 2 Short | HamsterballID (Target)
            if (targetBallID >= 0)      // 1 Byte  | Remaining Hamsterball HP (Target)
            {
                if (!_hamsterballs.ContainsKey(targetBallID)) msg.Write((byte)0);
                else msg.Write(_hamsterballs[targetBallID].HP);
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        // Msg20 | "LootItem Spawned" --- Sent whenver the server spawns LootItems
        private void SendSpawnedLoot(LootItem lootItem)
        {
            //Logger.DebugServer($"LootID: {lootID} - \"{lootItem.Name}\"\nType: {lootItem.LootType}, Rarity: {lootItem.Rarity}, Give: {lootItem.GiveAmount}\nWorldPos: {lootItem.Position}");
            NetOutgoingMessage msg = server.CreateMessage(lootItem.LootType == LootType.Weapon ? 27 : 25);
            msg.Write((byte)20);                // Byte   | MsgID
            msg.Write(lootItem.LootID);         // Int    | LootID
            msg.Write((byte)lootItem.LootType); // Byte   | LootType
            switch (lootItem.LootType)          // Short  | DataValue -- Weapon: WeaponIndex; Armor: ArmorTicksLeft; Others: typically how much to give
            {
                case LootType.Weapon:
                    msg.Write((short)lootItem.WeaponIndex);
                    break;
                default: // upon further inspection, it would seem all other LootTypes can simply use this
                    msg.Write((short)lootItem.GiveAmount);
                    break;
            }
            // Positions | ToDo: What is the second pair of posiions actually used for?
            msg.Write(lootItem.Position.x); // Float  | Position1.x
            msg.Write(lootItem.Position.y); // Float  | Position1.y
            msg.Write(lootItem.Position.x); // Float  | Position2.x
            msg.Write(lootItem.Position.y); // Float  | Position2.y
            // Last Branch | Other "info" about the loot:: Weapon: Ammo / grenadeCount; Armor: armorTier; Others: absolutely nothing
            switch (lootItem.LootType)      // Byte | AllLoot EXCEPT Armor & Weapons:: nothing --- Weapons: ammo/nade count + rarity; Arrmor: armorTier
            {
                case LootType.Weapon:
                    msg.Write((byte)lootItem.GiveAmount);   // Byte   | AmmoCount --- Weapons use "GiveAmount" to store Ammo
                    msg.Write(lootItem.Rarity.ToString());  // String | RarityInfoString -- No clue why it is like this, but it is
                    break;
                case LootType.Ammo:  // Byte | ammoSpawnAmmount
                case LootType.Armor: // Byte | armorTier / armorLevel
                    msg.Write((byte)lootItem.Rarity);
                    break;
                default: // upon further inspection, it would seem all other LootTypes can simply use this
                    msg.Write((byte)0);
                    break;
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
            // ----------------------------------------
            // | --- Msg20 Format ---
            // | 1 Byte   | MsgID (20)
            // | 4 Int    | LootID
            // | 1 Byte   | LootType
            // | 2 Short  | DataValue [Weapons: weaponJSONIndex --- Armor: armorTicksLeft --- All Other Types: amountToGive]
            // | 4 Float  | Position1.x
            // | 4 Float  | Position1.y
            // | 4 Float  | Position2.x <--- unsure what this is truly for... haven't messed with it
            // | 4 Float  | Position2.y <--- unsure what this is truly for... haven't messed with it
            // | 1 Byte   | DataValue2 [Weapon: ammoCount --- Armor: armorTier -- Ammo: ammoType --- All Other Types: <<nothing>> / 0]
            // | 2 String | InfoString? [Weapons: Rarity.ToString() --- All Other Types: do not include]
            // ----------------------------------------
        }

        /// <summary>
        /// Handles an incoming LootRequest packet for while in an in-progress match. (Msg21)
        /// </summary>
        /// <param name="amsg">Incoming message to handle and read data from.</param>
        private void HandleLootRequestMatch(NetIncomingMessage amsg)
        {
            //Logger.DebugServer("LootItems request.");
            if (TryPlayerFromConnection(amsg.SenderConnection, out Player player))
            {
                // Is the Player dead? Has the Player landed yet?
                if (!player.IsPlayerReal()) return; // || player.NextLootTime > DateTime.UtcNow
                try
                {
                    // Read Data. Verify if it is real.
                    int reqLootID = amsg.ReadInt32();
                    byte slotID = amsg.ReadByte();

                    // Verify is real.
                    //Logger.DebugServer($"[BetterHandleLootItem] Sent Slot: {slotID}");
                    if (slotID < 0 || slotID > 3) return; // NOTE: Seems like the game will always try to send the correct slot to change.
                    if (!_level.LootItems.ContainsKey(reqLootID)) // Add infraction thing so like if they do to much kicky?
                    {
                        Logger.Failure($"[Handle MatchLootRequest] Player @ {amsg.SenderConnection} requested a loot item that wasn't in the list.");
                        return;
                        //amsg.SenderConnection.Disconnect($"Requested LootID \"{reqLootID}\" not found.");
                    }

                    // Check if player is close enough.
                    LootItem item = _level.LootItems[reqLootID];  // Item is set here!
                    if (!Vector2.ValidDistance(player.Position, item.Position, 10.5f, true)) // Still testing thresholds...
                    {
                        Logger.DebugServer($"ItemPosition: {item.Position}");
                        Logger.Failure($"[HandleMatchLootReq] Player not close enough to loot...");
                    }
                    CheckMovementConflicts(player);

                    // Ok give the item.
                    //string itemdata = $"-- Found Item! --\nName: {item.Name}; Type: {item.LootType}; WeaponType: {item.WeaponType}\nRarity: {item.Rarity}; Give: {item.GiveAmount}; Position: {item.Position}\n";
                    //Logger.DebugServer(itemdata);
                    switch (item.LootType)
                    {
                        // Health Juice
                        case LootType.Juice:
                            if (player.HealthJuice == 200) return;
                            if ( (player.HealthJuice + item.GiveAmount) > 200)
                            {
                                item.GiveAmount -= (byte)(200 - player.HealthJuice);
                                player.HealthJuice = 200;
                                SendSpawnedLoot(_level.NewLootJuice(item.GiveAmount, item.Position));
                            }
                            else player.HealthJuice += item.GiveAmount;
                            break;

                        // Tape
                        case LootType.Tape:
                            if (player.SuperTape == 5) return;             // If at max -> stop; otherwise...
                            if ((player.SuperTape + item.GiveAmount) > 5)
                            {
                                item.GiveAmount -= (byte)(5 - player.SuperTape);
                                player.SuperTape = 5;
                                SendSpawnedLoot(_level.NewLootTape(item.GiveAmount, item.Position));
                            }
                            else player.SuperTape += item.GiveAmount;
                            break;

                        // Armor
                        case LootType.Armor:
                            if (player.ArmorTier != 0) // Has armor
                            {
                                SendSpawnedLoot(_level.NewLootArmor(player.ArmorTier, player.ArmorTapes, item.Position));
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
                        // TODO: fix when AmmoData is added; cleanup because hard to read.
                        case LootType.Ammo:
                            int ammoArrayIndex = item.Rarity;
                            //Logger.Header($"Sent AmmoType: {ammoArrayIndex}\nGive: {item.GiveAmount}");
                            if ( (ammoArrayIndex < 0) || (ammoArrayIndex > (_maxAmmo.Length - 1) ) ) return;
                            if (player.Ammo[ammoArrayIndex] == _maxAmmo[ammoArrayIndex]) return;

                            if ((player.Ammo[ammoArrayIndex] + item.GiveAmount) > _maxAmmo[ammoArrayIndex])
                            {
                                item.GiveAmount -= (byte)(_maxAmmo[ammoArrayIndex] - player.Ammo[ammoArrayIndex]);
                                player.Ammo[ammoArrayIndex] = _maxAmmo[ammoArrayIndex];
                                SendSpawnedLoot(_level.NewLootAmmo((byte)ammoArrayIndex, item.GiveAmount, item.Position));
                            }
                            else player.Ammo[ammoArrayIndex] += item.GiveAmount;
                            //Logger.Basic($"Player[{ammoArrayIndex}]: {player.Ammo[ammoArrayIndex]}");
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
                                            SendSpawnedLoot(_level.NewLootWeapon(item.WeaponIndex, 0, item.GiveAmount, item.Position));
                                        }
                                        else player.LootItems[2].GiveAmount += item.GiveAmount;
                                    }
                                    else
                                    {
                                        LootItem oldThrowable = player.LootItems[2];
                                        SendSpawnedLoot(_level.NewLootWeapon(oldThrowable.WeaponIndex, 0, oldThrowable.GiveAmount, item.Position));
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
                                    LootItem oldWeapon = player.LootItems[wepSlot];
                                    SendSpawnedLoot(_level.NewLootWeapon(oldWeapon.WeaponIndex, oldWeapon.Rarity, oldWeapon.GiveAmount, item.Position));
                                    player.LootItems[wepSlot] = item;
                                }
                                else player.LootItems[wepSlot] = item; // Item is NOT in this slot, so just replace it
                            }
                            break;
                    }
                    // Send LootItems OK!
                    _level.RemoveLootItem(item);
                    SendPlayerDataChange(player);
                    NetOutgoingMessage msg = server.CreateMessage();
                    msg.Write((byte)22);    // Byte  |  MsgID
                    msg.Write(player.ID);   // Short |  PlayerID
                    msg.Write(reqLootID);   // Int   |  LootID
                    msg.Write(slotID);      // Byte  |  SlotID lol
                    msg.Write((byte)0);     // Byte  |  No clue lol
                    server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
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
        private void ServerHandleLobbyLootRequest(NetIncomingMessage aMsg)
		// TODO: general updates; rarity of picked-up weapon changes per player as they normally should.
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
                NetOutgoingMessage bounce = server.CreateMessage(5);
                bounce.Write((byte)63);         // 1 Byte  | MsgID (63)
                bounce.Write(player.ID);        // 2 Short | PlayerID
                bounce.Write(player.VehicleID); // 2 Short | HamsterballID
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

        // Msg66 >> Msg67 | "Emote Request" -- Called whenever a player attempts to perform an emote
        private void HandleEmoteRequest(NetIncomingMessage pmsg)
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
                    if (!Vector2.ValidDistance(player.Position, reqPos, 2f, true))
                    {
                        Logger.Warn($"[HandleEmoteRequest] {player} requested emote position {reqPos} too far from actual position: {player.Position}");
                        return;
                    }
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
            player.EmotePosition = emotePosition;
            if (duration > -1) player.EmoteEndTime = DateTime.UtcNow.AddSeconds(duration);
            else player.EmoteEndTime = DateTime.MaxValue;
        }

        /// <summary>
        /// Sends a "Player Finished Emoting" packet (Msg67) to all NetPeers.
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

        // Msg70 | "Molecrate Open Request" --> Msg71 | "Confrim Molecrate Open" -- Received whenever a client attempts to open a Molecrate.
        private void HandleMolecrateOpenRequest(NetIncomingMessage pMsg)
        {
            // todo - improvements to molecrates as a whole is in order.
            // it is possible to get the molecrate to spawn and move around in the lobby. however, players can't open them...
            // normally... normally aside from the fact the crate is in the lobby in the first place, but that's besides the point here.
            if (!_hasMatchStarted) return;
            if (VerifyPlayer(pMsg.SenderConnection, "MolecrateOpen", out Player player))
            {
                try
                {
                    short crateID = pMsg.ReadInt16();
                    if (crateID < 0 || crateID >= _moleCrates.Length)
                    {
                        Logger.Failure($"[HandleMolecrateOpen - Error] {player} @ {player.Sender} requested an out-of-bounds MolecrateID!");
                        player.Sender.Disconnect("There was an error processing your request.\nMessage: out-of-bounds molecrateID!");
                        return;
                    }
                    if (_moleCrates[crateID] == null || !_moleCrates[crateID].isCrateReal)
                    {
                        Logger.Failure($"[HandleMolecrateOpen - Error] {player} @ {player.Sender} requested a molecrate still moving. Desync?");
                        if (_safeMode) player.Sender.Disconnect("There was an error processing your request.\nMessage: that molecrate can't be opened.");
                        return;
                    }
                    if (_moleCrates[crateID].isOpened) return;

                    // check if actually close enough to open & then open
                    if (Vector2.ValidDistance(player.Position, _moleCrates[crateID].Position, 14.7f, true))
                    {
                        SendMolecrateOpened(crateID, _moleCrates[crateID].Position);
                        // todo - spawn molecrate loot
                    }
                    else Logger.DebugServer($"[HandleMolecrateOpen - Error/Debug] Appears {player} wasn't close enough to the crate...");
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleMolecrateOpen - Error] {pMsg.SenderConnection} caused a NetException!\n{netEx}");
                    pMsg.SenderConnection.Disconnect("There was an error reading your packet data. [MolecrateOpen]");
                }
                catch (Exception ex) // todo - remove this once this handle working 100%
                {
                    Logger.Failure($"General exception has been thrown!\n{ex}");
                    SendChatMessageWarning(player, "An error occurred while handing MsgID (70) request. Please check the console.");
                }
            }
        }

        // Msg71 | "Molecrate Opened"
        private void SendMolecrateOpened(short crateID, Vector2 position)
        {
            NetOutgoingMessage msg = server.CreateMessage(11);
            msg.Write((byte)71);    // 1 Byte  | MsgID (71)
            msg.Write(crateID);     // 2 Short | MolecrateID
            msg.Write(position.x);  // 4 Float | Open Position.x
            msg.Write(position.y);  // 4 Float | Open Position.x
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        // Msg72 | "Doodad Destroyed" --> Msg73 | "Server Confirm Doodad Destroyed"
        // todo - cleanup + restructure! HANDLES execute server-side functions; SENDS create & send NetMsgs! Sends should NOT do HANDLES + SEND functions!!
        private void HandleDoodadDestroyed(NetIncomingMessage pmsg) // v0.90.2 OK
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleDoodadDestroyed", out Player player))
            {
                try
                {
                    // read the packet data... v0.90.2
                    float hitX = pmsg.ReadInt32();
                    float hitY = pmsg.ReadInt32();
                    short projectileID = pmsg.ReadInt16();

                    // is the projectile id valid? [-1 = melee; 0+ = attackIDs]
                    if (!player.IsValidProjectileID(projectileID))
                    {
                        Logger.Failure($"[HandleDoodadDestroyed] Player @ {pmsg.SenderConnection} sent invalid ProjectileID \"{projectileID}\"");
                        pmsg.SenderConnection.Disconnect($"There was an error processing your request. (doodadHit)");
                        return;
                    }

                    // try searching for the Doodad that the client claims is at the spot they hit
                    Vector2 hitPos = new Vector2(hitX, hitY);
                    if (_level.TryDestroyingDoodad(hitPos, out Doodad[] hitDoodads, _hasMatchStarted))
                    {
                        // if the above method returns true, then yaaay we get a bunch of Doodads to delete
                        for (int i = 0; i < hitDoodads.Length; i++)
                        {
                            // as mentioned, todo - this SEND should **ONLY** send. we're already manipulating the Doodad anyways. ...
                            // ... method defaults exist, and really should've been looked into a long time ago.
                            SendDestroyedDoodad(hitDoodads[i]);
                        }
                    }
                    else Logger.DebugServer($"[hDoodadDestroyed] - Failed to locate Doodad @ reqPos: {hitPos}");
                }
                // if any of the of the packet data reading fails, then this happens
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleDoodadDestroyed] Client @ {pmsg.SenderConnection} caused a NetException!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("Error while processing your request, sorry about that :<");
                }
            }
        }

        // Msg73 | "Confirm Doodad Destroyed"
        private void SendDestroyedDoodad(Doodad doodad) // v0.90.2 OK-enough TODO:: damage drop-off
        {
            // | v0.90.2 Msg73 Format
            // | -----------------------
            // | 1 Byte  | MsgID (73)
            // | 2 Short | ??? (game wants a short at the start, yet it does NOTHING with it)
            // | 2 Short | DoodadPos.x 
            // | 2 Short | DoodadPos.y
            // | 2 Short | #ofHitSpots
            // | 2 Short | CollisionSpot[n]-x
            // | 2 Short | CollisionSpot[n]-y
            // | 1 Byte  | CollisionSpot[n]-NewCollisionType
            // | 1 Byte  | # of hit Players
            // | 2 Short | Hit PlayerID [n]

            NetOutgoingMessage msg = server.CreateMessage(24);
            msg.Write((byte)73);    // 1 Byte  | MsgID (73)
            msg.Write((short)420);  // 2 Short | ??? (game wants a short at the start, yet it does NOTHING with it)
            msg.Write((short)doodad.Position.x);    // 2 Short | DoodadPos.x 
            msg.Write((short)doodad.Position.y);    // 2 Short | DoodadPos.y
            msg.Write((short)doodad.HittableSpots.Length);  // 2 Short | #ofHitSpots
            for (int i = 0; i < doodad.HittableSpots.Length; i++)
            {
                msg.Write((short)doodad.HittableSpots[i].x);    // 2 Short | CollisionSpotChange.x
                msg.Write((short)doodad.HittableSpots[i].y);    // 2 Short | CollisionSpotChange.y
                msg.Write((byte)CollisionType.None);            // 1 Byte  | CollisionSpot New CollisionType
            }
            if (doodad.Type.DestructibleDamageRadius > 0)
            {
                // Players
                List<Player> doodadHitPlayers = new List<Player>(_players.Length);
                for (int j = 0; j < _players.Length; j++)
                {
                    // TOOD:: damage scaling; especially with that 32-range... this is suuuuper unfair
                    if (!_hasMatchStarted || _players[j] == null || !_players[j].IsPlayerReal()) continue;
                    if (Vector2.ValidDistance(doodad.Position, _players[j].Position, doodad.Type.DestructibleDamageRadius, true))
                    {
                        Logger.DebugServer($"[DoodadDestroy Destruct Distances] Player: {_players[j].Position}; DoodadPos: {doodad.Position}, rad: {doodad.Type.DestructibleDamageRadius}");
                        doodadHitPlayers.Add(_players[j]);
                        // https://animalroyale.fandom.com/wiki/Version_1.4.1 << changes scaling
                        _players[j].ArmorTapes = 0; // remember, when damage scaling is real this will change
                        // v0.90.2: 20+ - all
                        // v1.4.1+ 60+ - all
                        test_damagePlayer(_players[j], (int)(doodad.Type.DestructibleDamagePeak / 2), 0, -3); // halfing it so it isn't completely unfair (temporary fix)
                    }
                }
                // Hamsterballs
                foreach (Hamsterball ball in _hamsterballs.Values)
                {
                    if (ball == null) continue;
                    if (Vector2.ValidDistance(doodad.Position, ball.Position, doodad.Type.DestructibleDamageRadius, true))
                    {
                        Logger.DebugServer($"[DoodadDestroy2] Found ball... Trying to destroy");
                        SendConfirmAttack(-1, -1, -1, 0, ball.ID, 0);
                        DestroyHamsterball(ball.ID);
                    }
                }
                // End | Append any hit players to the end of the message
                if (doodadHitPlayers.Count > 0)
                {
                    msg.Write((byte)doodadHitPlayers.Count);
                    foreach (Player player in doodadHitPlayers) msg.Write(player.ID);
                }
                else msg.Write((byte)0); // can still explode and not hit anything
            }
            else msg.Write((byte)0); // # of hit players --> always 0 if the doodad can't explode!
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        // Msg74 | "Request Attack Windup" --> Msg75 "Confirm Attack Windup"
        private void HandleAttackWindUp(NetIncomingMessage pMsg) // v0.90.2 OK | 6/6/23
        {
            if (VerifyPlayer(pMsg.SenderConnection, "HandleAttackWindupReq", out Player player))
            {
                if (!player.IsPlayerReal()) return; // if player isn't ready, is ghosted, dead, OR is currently attempting to land... don't do anything!!
                try
                {
                    // read packet data...
                    short weaponID = pMsg.ReadInt16();
                    byte slot = pMsg.ReadByte();

                    // verify player isn't lying...
                    if (slot >= 3) // v0.90.2:: [0, 1, 2]
                    {
                        Logger.Failure($"[HandleAttackWindupReq - Error] {player} @ {player.Sender} sent invalid slot id \"{slot}\"!");
                        player.Sender.Disconnect("There was an error reading your packet data. [AttackWindup]");
                    }
                    if (player.ActiveSlot != slot)
                    {
                        Logger.Failure($"[HandleAttackWindupReq - Error] {player}'s active slot ({player.ActiveSlot}) doesn't match sent slot \"{slot}\"!");
                        return; // assuming this is VERY a late packet
                        // todo - inc "infraction-count"
                    }
                    if (_hasMatchStarted && player.LootItems[slot].WeaponIndex != weaponID)
                    {
                        Logger.Failure($"[HandleAttackWindupReq - Error] {player} @ {player.Sender} sent WeaponID \"{weaponID}\" doesn't match item in slot {slot}!");
                        return; // assuming this is VERY a late packet
                        // todo - inc "infraction-count"
                    }

                    // packet data checks all passed successfully!
                    SendAttackWindup(player.ID, weaponID, slot);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleAttackWindupReq - Error] {pMsg.SenderConnection} caused a NetException!\n{netEx}");
                    pMsg.SenderConnection.Disconnect("There was an error reading your packet data. [AttackRevUp]");
                }
            }
        }

        // Msg75 | "Send Attack Windup"
        private void SendAttackWindup(short playerID, short weaponID, byte slotID) // v0.90.2 OK | 6/6/23
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(6);
            msg.Write((byte)75);    // 1 Byte  | MsgID (76)
            msg.Write(playerID);    // 2 Short | PlayerID
            msg.Write(weaponID);    // 2 Short | WeaponID
            msg.Write(slotID);      // 1 Byte  | SlotID
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        // Msg77 | "Request Attack Wind Down" --> Msg77 "Confrim Attack Wind Down"
        private void HandleAttackWindDown(NetIncomingMessage pMsg) // v0.90.2 OK | 6/6/23
        {
            if (VerifyPlayer(pMsg.SenderConnection, "HandleAttackWindDownReq", out Player player))
            {
                if (!player.IsPlayerReal()) return; // if player isn't ready, is ghosted, dead, OR is currently attempting to land... don't do anything!!
                try
                {
                    // read packet data... | actually a similar concept to the rev-up, so basically copy and pasted lol!
                    short weaponID = pMsg.ReadInt16();
                    byte slot = pMsg.ReadByte();

                    // verify player isn't lying...
                    if (slot >= 3) // v0.90.2:: [0, 1, 2]
                    {
                        Logger.Failure($"[HandleAttackWindDownReq - Error] {player} @ {player.Sender} sent invalid slot id \"{slot}\"!");
                        player.Sender.Disconnect("There was an error reading your packet data. [AttackWindup]");
                    }
                    if (player.ActiveSlot != slot)
                    {
                        Logger.Failure($"[HandleAttackWindDownReq - Error] {player}'s active slot ({player.ActiveSlot}) doesn't match sent slot \"{slot}\"!");
                        return; // assuming this is VERY a late packet
                        // todo - inc "infraction-count"
                    }
                    if (_hasMatchStarted && player.LootItems[slot].WeaponIndex != weaponID)
                    {
                        Logger.Failure($"[HandleAttackWindDownReq - Error] {player} @ {player.Sender} sent WeaponID \"{weaponID}\" doesn't match item in slot {slot}!");
                        return; // assuming this is VERY a late packet
                        // todo - inc "infraction-count"
                    }

                    // packet data checks all passed successfully!
                    SendAttackWindDown(player.ID, weaponID);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleAttackWindDownReq - Error] {pMsg.SenderConnection} caused a NetException!\n{netEx}");
                    pMsg.SenderConnection.Disconnect("There was an error reading your packet data. [AttackRevDown]");
                }
            }
        }

        // Msg77 | "Attack Rev Down"
        private void SendAttackWindDown(short playerID, short weaponID) // v0.90.2 OK | 6/6/23
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(5); // basically rev up without the slotId
            msg.Write((byte)77);    // 1 Byte  | MsgID (77)
            msg.Write(playerID);    // 2 Short | PlayerID
            msg.Write(weaponID);    // 2 Short | WeaponID
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        /// <summary>Handles an incoming "HealRequest" packet (Msg47); Either ignoring or accepting the message and starting the Player heal.</summary>
        /// <param name="pmsg">Incoming message to read data from.</param>
        private void HandleHealingRequest(NetIncomingMessage pmsg) // Msg47 >> Msg48
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleHealingRequest", out Player player))
            {
                if (!player.IsPlayerReal() || (player.HealthJuice == 0)) return;
                try
                {
                    float posX = pmsg.ReadFloat();
                    float posY = pmsg.ReadFloat();
                    Vector2 requestPostion = new Vector2(posX, posY);
                    if (Vector2.ValidDistance(requestPostion, player.Position, 2, true)){
                        SendForcePosition(player, requestPostion);
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
                if (!player.IsPlayerReal() || player.SuperTape == 0 || player.ArmorTier == 0 || player.ArmorTapes == player.ArmorTier) return;
                try
                {
                    float posX = pmsg.ReadFloat();
                    float posY = pmsg.ReadFloat();
                    Vector2 requestPosition = new Vector2(posX, posY);
                    if (Vector2.ValidDistance(requestPosition, player.Position, 2, true))
                    {
                        SendForcePosition(player, requestPosition);
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

        // Msg80 | "Teammate Pickup Request" --- Received when a teammate tries picking up a downed teammate
        private void HandleTeammatePickupRequest(NetIncomingMessage pMsg) // Msg80 >> Msg81
        {
            if (VerifyPlayer(pMsg.SenderConnection, "HandleTeammatePickupRequest", out Player player))
            {
                if (player.isReviving) Logger.DebugServer($"HandleTeammatePickupRequest | {player} is still reviving but tried picking up teammate!"); // can remove
                if (player.isReviving || !player.IsPlayerReal()) return;
                CheckMovementConflicts(player);
                try
                {
                    // NetMsg Data Read
                    short teammateID = pMsg.ReadInt16();
                    // Validate
                    if (!TryPlayerFromID(teammateID, out Player teammate))
                    {
                        Logger.Failure($"[HandleTeammatePickupRequest] Couldn't find teammate: {teammateID}");
                        return;
                    }
                    if (!player.Teammates.Contains(teammate))
                    {
                        Logger.Failure($"[HandleTeammatePickupRequest] {player} isn't teammates with {teammate}.");
                        return;
                    }
                    if (!Vector2.ValidDistance(player.Position, teammate.Position, 7.4f, true))
                    {
                        Logger.Failure($"Teammate wasn't close enough to revive!");
                        return;
                    }
                    // Server-Side Sets
                    teammate.DownSetSaviour(player.ID);
                    player.SaviourSetRevivee(teammateID);
                    // Send Request OK'd
                    SendPickupBegan(player.ID, teammateID);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleTeammatePickupRequest] Player @ {pMsg.SenderConnection} caused a NetException!\n{netEx}");
                    pMsg.SenderConnection.Disconnect("There was an error while reading your packet data! (TeamPickupStart)");
                }
            }
        }

        // Msg81 | "Teammate Pickup Begin" --- Sent whenever a teammate begins reviving their teammate; it displays to all other players this "action" of doing so
        private void SendPickupBegan(short saviourID, short downedID)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(5); // initCapacity = (byte)[1] + 2x(short)[2] = 5
            msg.Write((byte)81);    // 1 Byte  | MsgID (81)
            msg.Write(saviourID);   // 2 Short | Reviving-PlayerID 
            msg.Write(downedID);    // 2 Short | Downed-PlayerID
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        // Msg82 | "Teammate Pickup Canceled" --- Sent whenever a reviver does something to cancel the revive; it displays the "action" of the player canceling to all others
        private void SendPickupCanceled(short revivingPID, short downedPID)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(5); // initCapacity = (byte)[1] + 2x(short)[2] = 5
            msg.Write((byte)82);    // 1 Byte  | MsgID (82)
            msg.Write(revivingPID); // 2 Short | RevivingPlayerID
            msg.Write(downedPID);   // 2 Short | DownedPlayerID
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        /// <summary>
        /// Sends a "Player-Revived" packet to all NetPeers using the provided paramters. NO server-side setting of required fields.
        /// </summary>
        /// <param name="ressingID">ID of the player who is reviving.</param>
        /// <param name="downedID">ID of the player who has been revived.</param>
        private void SendPickupFinished(short ressingID, short downedID) // Msg83
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(5);
            msg.Write((byte)83);    // 1 Byte  | MsgID (83)
            msg.Write(ressingID);   // 2 Short | RevierID
            msg.Write(downedID);    // 2 Short | RevieeID
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        // Msg84 | "Player Downed" -- Sent in team-based modes when the Player should've died, but they still have teammates alive.
        private void HandlePlayerDowned(Player player, short attackerID, short wepaonID)
        {
            if (!IsServerRunning()) return;
            // Server-Side Vars
            CheckMovementConflicts(player);
            player.DownKnock(_bleedoutRateSeconds);
            // Net Message
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)84);   // Byte  | MsgID (84)
            msg.Write(player.ID);  // Short | Downed PlayerID
            msg.Write(attackerID); // Short | Killer PlayerID [-2 = SSG; any other is nothing, or a PlayerID]
            msg.Write(wepaonID);   // Short | WeaponID / DamageSourceID [-3 Barrel; -2 Hamsterballs; -1 = None; 0+ Weapons]
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        // Handles a reviving-player canceling their revive
        private void HandlePickupCanceled(Player player)
        {
            if (player.isReviving)
            {
                SendPickupCanceled(player.ID, player.RevivingID); // must go first, or store the revivingPlayerID
                if (TryPlayerFromID(player.RevivingID, out Player downedPlayer)) downedPlayer.DownResetState(_bleedoutRateSeconds);
                player.SaviourFinishedRessing();
            }
            else if (player.isDown)
            {
                SendPickupCanceled(player.SaviourID, player.ID);
                if (TryPlayerFromID(player.SaviourID, out Player thisSaviour)) thisSaviour.SaviourFinishedRessing();
                player.DownResetState(_bleedoutRateSeconds);
            }
        }

        // Msg87 | "Trap Deploy Request" --- Received when a player throws a "trap" throwable (banana/skunk nades)
        private void HandleTrapDeployed(NetIncomingMessage pmsg) // Throwables uses Projectile for whatever reason... really needs its own thing tbh!
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleTrapDeployed", out Player player))
            {
                try
                {
                    float reqX = pmsg.ReadFloat();
                    float reqY = pmsg.ReadFloat();
                    short throwableID = pmsg.ReadInt16();
                    if (!player.ThrownNades.ContainsKey(throwableID))
                    {
                        Logger.Warn($"[HandleTrapDeployed] Player does not contain key \"{throwableID}\"!");
                        return;
                    }
                    Weapon trap = _weapons[player.ThrownNades[throwableID].WeaponID];
                    //Logger.Basic($"WeaponID: {player.ThrownNades[throwableID].WeaponID} | Name: {trap.Name} | Rad: {trap.Radius}");
                    switch (trap.Name)
                    {
                        case "GrenadeBanana":
                            _traps.Add(new Trap(TrapType.Banana, new Vector2(reqX, reqY), player.ID, trap.Radius, float.MaxValue, trap.JSONIndex, throwableID));
                            break;
                        case "GrenadeSkunk":
                            _traps.Add(new Trap(TrapType.SkunkNade, new Vector2(reqX, reqY), player.ID, trap.Radius, 6.4f, trap.JSONIndex, throwableID));
                            break;
                        default:
                            Logger.Failure($"Invalid weapon name \"{trap.Name}\"");
                            break;
                    }
                    SendTrapDeployed(player.ID, trap.JSONIndex);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleTrapDeployed] Player @ {pmsg.SenderConnection} caused a NetException!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("There was an error while reading your packet data! (TrapDeploy)");
                }
            }
        }

        // Msg111 | "Send Trap Deployed" --- Sent when the server confirms a trap has been deployed.
        private void SendTrapDeployed(short playerID, short grenadeID)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)111);   // Byte  | MsgID
            msg.Write(playerID);    // Short | Deploying PlayerID
            msg.Write(grenadeID);   // Short | GrenadeID -- Index in weapons list
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        // Msg88 | "Vehicle Hit Banana" --- Appears to be received whenever a client runs over a banana with a Hamsterball
        private void HandleVehicleHitBanana(NetIncomingMessage pMsg) // v0.90.2 OK [6/6/23]
        {
            if (VerifyPlayer(pMsg.SenderConnection, "HandleVehicleHitBanana", out Player player))
            {
                if (!player.IsPlayerReal() || player.VehicleID == -1) return;
                try
                {
                    short ownerID = pMsg.ReadInt16();
                    short throwableID = pMsg.ReadInt16();
                    bool fromVehicle = pMsg.ReadBoolean(); // really not clue what this is about it's always true!
                    //Logger.DebugServer($"-- Msg 88 --\nOwner: {ownerID}\nID: {throwableID}\nHitByVehicle? {fromVehicle}");
                    foreach (Trap trap in _traps)
                    {
                        if (trap.TrapType != TrapType.Banana) continue;
                        if (trap.OwnerID == ownerID && trap.ThrowableID == throwableID)
                        {
                            SendGrenadeFinished(ownerID, throwableID, trap.Position);
                            _traps.Remove(trap);
                            break;
                        }
                    }
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleVehicleHitBanana - Error] {pMsg.SenderConnection} caused a NetException!\n{netEx}");
                    pMsg.SenderConnection.Disconnect("There was an error reading your packet data. [VehicleHitNanner]");
                }
            }
        }

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
                NetOutgoingMessage msg = server.CreateMessage(3);
                msg.Write((byte)95);    // Byte  | MsgID (95)
                msg.Write(player.ID);   // Short | LeavingTeammateID
                server.SendMessage(msg, player.Teammates[i].Sender, NetDeliveryMethod.ReliableUnordered);
                player.Teammates[i].Teammates.Remove(player);
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

        // Msg104 | "Send Aviary-Show Started" --- Usually sent when a player enters the "Beakeasy" location while a show isn't in progress.
        private void SendAviaryShow(byte showID) // 0 = Blue Jay Z (peak); 1 = Kelly Larkson (ew); 2-255 = Lady Caw Caw (not as ew)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(2);
            msg.Write((byte)104);   // 1 Byte | MsgID (104)
            msg.Write(showID);      // 1 Byte | AviaryShowID
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        // Msg105 | "Ghostmode Enabled" --- Sent to get the ghostmode controls enabled for the player and stuff.
        private void SendGhostmodeEnabled(NetConnection pDest) // appears OK [v0.90.2] (don't forget to send Msg46 alongside this one!]
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)105);
            server.SendMessage(msg, pDest, NetDeliveryMethod.ReliableUnordered);
        }

        // Msg106 | "Send Roll Message" --- Usually sent whenever a player uses the /roll command; can be used for other cool stuff though!
        private void SendRollMsg(short playerID, string message)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(3 + message.Length);
            msg.Write((byte)106);   // 1 Byte   | MsgID (106)
            msg.Write(playerID);    // 2 Short  | PlayerID
            msg.Write(message);     // V String | Message
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        /// <summary>
        /// Sends a NetMessage to all connected clients that a player with the provided PlayerID has had their parachute-mode updated.
        /// </summary>
        private void SendParachuteUpdate(short aID, bool aIsDiving) // [probs]Msg108 >> Msg109
        {
            if (!IsServerRunning()) return;
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

        // Msg125 | "Player Weapons Removed" --- Sent whenever a player has their weapons removed lol; at least dispalys it as such...
        private void SendRemovedWeapons(short playerID)
        {
            NetOutgoingMessage msg = server.CreateMessage(3);
            msg.Write((byte)125);   // 1 Byte  | MsgID (109)
            msg.Write(playerID);    // 2 Short | PlayerID
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>
        /// Sends NetMessages to all NetPeers containing the packets that will stop drinking, taping, and emoting.
        /// </summary>
        private void CheckMovementConflicts(Player player) // Player fields are reset within each of these methods.
        {
            SendPlayerEndDrink(player);
            SendPlayerEndTape(player);
            SendPlayerEndedEmoting(player);
            HandlePickupCanceled(player);
        }

        // todo - send player dc message only if in lobby... otherwise they need to stick around for a while longer
        private void HandleClientDisconnect(NetIncomingMessage pmsg)
        {
            if (TryIndexFromConnection(pmsg.SenderConnection, out int index))
            {
                // if you skip out on 46, that's OK. that's how SAR usually works.
                // if teams exist, and the teammate is informed on a DC, they see it as they should without seeing the player disappear from the match
                SendPlayerDisconnected(_players[index].ID);

                // If they're spectating someone... remove them from the total... Probably works (didn't test LOL!)
                if (_players[index].WhoImSpectating != -1)
                {
                    if (TryPlayerFromID(_players[index].WhoImSpectating, out Player spectating))
                    {
                        if (!spectating.MySpectatorsIDs.Remove(_players[index].ID)) Logger.Warn($"[HandleClientDC] [Warn] {_players[index]} not in {spectating}'s spectatorIDs list, but server thought they were?");
                        SendUpdatedSpectatorCount(spectating.ID, (byte)spectating.MySpectatorsIDs.Count);
                    }
                }

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
        private void LoadSARLevel(uint lootSeed, uint coconutSeed, uint hamsterballSeed)
        {
            Logger.Basic("[LoadSARLevel] Attempting to load level data...");
            if (svd_LevelLoaded)
            {
                Logger.Failure("[LoadSARLevel] Already called silly!");
                return;
            }
            // Load level 
            _level = new SARLevel(lootSeed, coconutSeed, hamsterballSeed);
            _campfires = _level.Campfires;
            _coconutList = _level.Coconuts;
            _hamsterballs = _level.Hamsterballs;
            _level.NullUnNeeds();
            svd_LevelLoaded = true;

            // End of Loading in Files
            //Logger.Success("[LoadSARLevel] Finished without encountering any errors.");
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
            Logger.Basic($"Hamsterball[{ballIndex}] has been removed.");
        }
        
        /// <summary> Returns whether this Match's NetServer is still running or not.</summary>
        /// <returns>True if the NetServer's status is "running"; False is otherwise.</returns>
        public bool IsServerRunning()
        {
            return server?.Status == NetPeerStatus.Running;
        }



        // trying to test asyncs more than before
        // now that I think about it, I think the reason async wasn't working with NetMessages before, was because the NetLoop()
        // would let their tasks chill in the background, while it continued. Because each end of the loop tosses out the NetMsg passed to the methods...
        // ...the NetMessage no longer existed, and so would crash. So... how do you fix?
        /*private async void Test_Awaiting(Player player)
        {
            Logger.DebugServer($"{DateTime.UtcNow} Starting task...");
            await player.DoDamage(5);
            Logger.DebugServer($"{DateTime.UtcNow} Task should have finished. At least... we're wating on it.");
        }*/
    }
}