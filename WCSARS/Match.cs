using Lidgren.Network;
using SARStuff;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using WCSARS.Configuration;

namespace WCSARS
{
    class Match
    {
        // general junk --- kind of unsorted...
        private string _serverKey;
        public NetServer server;
        private Player[] _players;
        private List<short> _availableIDs;
        private Dictionary<NetConnection, Client> _incomingClients;
        private TimeSpan DeltaTime;
        private string _gamemode = "solo";
        //private string _matchUUID;

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
        private bool isSorting, isSorted;
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
        // todo -- molecrate rewrite: make it a dictionary or something...
        private short _maxMoleCrates; // Maximum # of MoleCrates allowed in a match.
        private MoleCrate[] _moleCrates; // An array of MoleCrate objects which is the amount of active moles/crates available. The Length is that of Match._maxMoleCrates.
        // -- MoleCrate Crate Stuff --

        // Giant Eagle
        private bool _isFlightActive = false;
        private GiantEagle _giantEagle = new GiantEagle(new Vector2(0f, 0f), new Vector2(4248f, 4248f));

        // -- Healing Values --
        private float _healPerTick = 4.75f; // 4.75 health/drinkies every 0.5s according to the SAR wiki 7/21/22
        private float _healRateSeconds = 0.5f; // 0.5s
        private float _coconutHealAmountHP = 5f;
        private float _campfireHealPer = 4f;
        private float _campfireHealRateSeconds = 1f;
        private float _bleedoutRateSeconds = 1f; // bleed-out rate when downed [SAR default: 1s]
        private byte _resurrectSetHP = 25; // HP to set a Player's health to upon being revived from a downed-state.

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
        private SARLevel _level;
        private bool _hasLevelBeenLoaded = false; // do we REALLY need this???

        // Lobby Stuff
        private bool _hasMatchStarted = false;
        private bool _isMatchFull = false;
        private double _lobbyRemainingSeconds;
        private int _lobbyPlayersNeededToReduceWait;

        //mmmmmmmmmmmmmmmmmmmmmmmmmmmmm (unsure section right now
        private bool _canCheckWins = false;
        private bool _hasPlayerDied = true;
        private bool _safeMode = true; // This is currently only used by the /gun ""command"" so you can generate guns with abnormal rarities
        private const int MS_PER_TICK = 41; // (1000ms / 24t/s == 41)
        private readonly string _baseloc = AppDomain.CurrentDomain.BaseDirectory;

        public Match(Config cfg) // Match but it uses ConfigLoader (EW!)
        {
            #if DEBUG
            cfg.LazyPrint();
            #endif

            // Initialize PlayerStuff
            _serverKey = cfg.ServerKey;
            _gamemode = cfg.ServerGamemode;
            _players = new Player[cfg.ServerMaxPlayers];
            _availableIDs = new List<short>(_players.Length);
            _incomingClients = new Dictionary<NetConnection, Client>(_players.Length);
            _poisonDamageQueue = new List<Player>(32);
            for (short i = 0; i < _players.Length; i++)
                _availableIDs.Add(i);

            // Load json files
            Logger.Basic("[Match - Status] Loading player-data.json...");
            _playerData = LoadJSONArray(_baseloc + "player-data.json");
            Logger.Basic("[Match - Status] Loading banned-players.json...");
            _bannedPlayers = LoadJSONArray(_baseloc + "banned-players.json");
            Logger.Basic("[Match - Status] Loading banned-ips.json...");
            _bannedIPs = LoadJSONArray(_baseloc + "banned-ips.json");

            // Set healing values
            _healPerTick = cfg.JuiceHpPerTick; // WARN [BAD] | BYTE -> FLOAT
            _healRateSeconds = cfg.JuiceDrinkRateSeconds;
            _coconutHealAmountHP = cfg.CoconutHealHP; // WARN [BAD] | BYTE -> FLOAT
            _campfireHealPer = cfg.CampfireHpPerTick; // WARN [BAD] | BYTE -> FLOAT
            _campfireHealRateSeconds = cfg.CampfireTickRateSeconds;
            _bleedoutRateSeconds = cfg.DownedBleedoutRateSeconds;
            _resurrectSetHP = cfg.DownedRessurectHP;

            // Set Dartgun stuff -- TBH could just use the stats found in the Dartgun...
            _ddgMaxTicks = cfg.DartgunTickMaxDamageStacks;
            _ddgTickRateSeconds = cfg.DartgunTickRateSeconds;
            _ddgDamagePerTick = cfg.DartgunTickDamage;
            _ssgTickRateSeconds = cfg.SkunkGasTickRateSeconds;

            // Others
            _canCheckWins = !cfg.FunDisableWinChecks;
            _safeMode = !cfg.FunDisableSafetyChecks;
            isSorting = false;
            isSorted = true;

            // even dumber
            _lobbyRemainingSeconds = cfg.LobbyDurationSeconds;
            _lobbyPlayersNeededToReduceWait = cfg.LobbyPlayersUntilReduceWait;
            _traps = new List<Trap>(64);

            // MOLECRATE - PHASE OUT
            _maxMoleCrates = 12; //cfg.MaxMoleCrates; // TODO - phase out
            _moleCrates = new MoleCrate[_maxMoleCrates];

            // Load the SAR Level and stuff!
            uint lootSeed = cfg.SeedLoot;
            uint coconutSeed = cfg.SeedCoconuts;
            uint hamsterballSeed = cfg.SeedHamsterballs;
            if (!cfg.FunForceConfigSeeds)
            {
                MersenneTwister twistItUp = new MersenneTwister((uint)DateTime.UtcNow.Ticks);
                lootSeed = twistItUp.NextUInt(0u, uint.MaxValue);
                coconutSeed = twistItUp.NextUInt(0u, uint.MaxValue);
                hamsterballSeed = twistItUp.NextUInt(0u, uint.MaxValue); 
            }
            Logger.Warn($"[Match - Warning] Using Seeds: LootSeed: {lootSeed}; CoconutSeed: {coconutSeed}; HampterSeed: {hamsterballSeed}");
            LoadSARLevel(lootSeed, coconutSeed, hamsterballSeed);

            // Initialize NetServer
            Logger.Basic($"[Match - Status] Attempting to start server on \"{cfg.ServerIP}:{cfg.ServerPort}\".");
            Thread updateThread = new Thread(ServerUpdateLoop);
            Thread netThread = new Thread(ServerNetLoop);
            NetPeerConfiguration config = new NetPeerConfiguration("BR2D");
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);  // Reminder to not remove this
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);        // Reminder to not remove this
            config.PingInterval = 22f;
            config.LocalAddress = cfg.ServerIP;
            config.Port = cfg.ServerPort;
            server = new NetServer(config);
            server.Start();
            netThread.Start();
            updateThread.Start();
            Logger.Header("[Match - OK] Match created without encountering any errors.");
        }

        /// <summary>
        /// Handles all NetIncomingMessages sent to this Match's server. Continuously runs until this Match.server is no longer running.
        /// </summary>
        private void ServerNetLoop()
        {
            Logger.Basic("[Match - ServerNetLoop] Network thread started!");
            NetIncomingMessage msg;
            while (IsServerRunning())
            {
                server.MessageReceivedEvent.WaitOne(5000); // Halt thread until NetServer receives a message OR 5s has passed.
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

                        case NetIncomingMessageType.ConnectionApproval: // MessageType.ConnectionApproval MUST be enabled to work
                            Logger.Header("[Connection Approval] A new connection is awaiting approval!");
                            HandleConnectionApproval(msg);
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
                                    Logger.Warn($"[NCS.Disconnected] Client GoodbyeMsg: {msg.ReadString()}");
                                    HandleNetClientDisconnect(msg);
                                    break;
                            }
                            break;

                        case NetIncomingMessageType.DebugMessage:
                            Logger.DebugServer(msg.ReadString());
                            break;

                        case NetIncomingMessageType.WarningMessage:
                            Logger.Warn("[ServerNetLoop - Warn] " + msg.ReadString());
                            break;
                        case NetIncomingMessageType.ErrorMessage:
                            Logger.Failure("[ServerNetLoop - Error] " + msg.ReadString());
                            break;
                        case NetIncomingMessageType.ConnectionLatencyUpdated:
                            try
                            {
                                float pingTime = msg.ReadFloat();
                                //Logger.Basic($"Received PingFloat: {pingTime}");
                                //Logger.Basic($"Received PingFloatCorrection: {pingTime * 1000}");
                                //Logger.Basic($"Sender RemoteTimeOffset: {msg.SenderConnection.RemoteTimeOffset}");
                                //Logger.Basic($"Sender AverageRoundTrip: {msg.SenderConnection.AverageRoundtripTime}");
                                if (TryPlayerFromConnection(msg.SenderConnection, out Player pinger))
                                    pinger.LastPingTime = pingTime;
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
            Logger.DebugServer($"[{DateTime.UtcNow}] [NetworkLoop] I'm shutting down as well... Byebye!");
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
            // ... However, it is impossible to open this crate. Even after entering a match, the crate is non-interactable
            // - You can change a Player's health, tapes, drink amount; and proably armor as well in lobby.

            Logger.Basic("[Match - ServerUpdateLoop] Update thread started!");
            if (!_safeMode) Logger.Warn("[Match - Warning] Safemode has been DISABLED! Insane shenanigans may occur! (your problem, not mine)");
            if (!_canCheckWins) Logger.Warn("[Match - Warning] Match is NOT checking for wins! Use \"/togglewin\" while in-game to re-enable!");

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
                    int numOfPlayers = GetNumberOfValidPlayerEntries();
                    if (!_isMatchFull && numOfPlayers == _players.Length)
                        _isMatchFull = true;
                    else if (_isMatchFull && numOfPlayers != _players.Length)
                        _isMatchFull = false;

                    // General Lobby stuff...
                    SendDummyMessage(); // Ping!
                    SendCurrentPlayerPings(); // Pong!
                    UpdateLobbyCountdown();
                    SendLobbyPlayerPositions();
                    UpdatePlayerEmotes(); // would you believe me if I told you this used to only be used in in-progress Matches until 6/13/23?
                    CheckCampfiresLobby();
                    // TODO: probs Gallery Targets somewhere here as well

                    // Because /commands are a thing
                    CheckMoleCrates();
                    UpdatePlayerDataChanges();
                    UpdateDownedPlayers();
                    UpdateStunnedPlayers();

                    // For the "tick" system
                    nextTick = nextTick.AddMilliseconds(MS_PER_TICK);
                    if (nextTick > DateTime.UtcNow)
                        Thread.Sleep(nextTick - DateTime.UtcNow);
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

                    if (!isSorted)
                        SortPlayerEntries();

                    SendDummyMessage();
                    SendCurrentPlayerPings();

                    // Check Wins
                    if (_hasPlayerDied && _canCheckWins) // todo - just do this in HandlePlayerDeath?
                        CheckForWinnerWinnerChickenDinner();

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
                    if (nextTick > DateTime.UtcNow)
                        Thread.Sleep(nextTick - DateTime.UtcNow);
                }
            }
            // The End
            Logger.DebugServer($"[{DateTime.UtcNow}] [UpdateLoop] I'm shutting down as well... Byebye!");
        }
        
        private void UpdateLobbyCountdown() // Update LobbyCountdown; Sends MatchStart once countdown reaches zero.
        {
            if (!IsServerRunning())
                return;

            var readiedPlayers = GetAllReadiedAlivePlayers();
            if (readiedPlayers.Count == 0)
                return;

            if ((readiedPlayers.Count >= _lobbyPlayersNeededToReduceWait) && (_lobbyRemainingSeconds > 60.0))
            {
                _lobbyRemainingSeconds = 60.0;
                SendCurrentLobbyCountdown(_lobbyRemainingSeconds);
                return;
            }

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
            if (!isSkunkGasWarningActive || !IsServerRunning())
                return;

            SkunkGasWarningDuration -= (float)DeltaTime.TotalSeconds;
            if (SkunkGasWarningDuration <= 0)
            {
                SendSSGApproachEvent(SkunkGasRemainingApproachTime);
                isSkunkGasWarningActive = false;
                isSkunkGasActive = true;
                canSSGApproach = true;
            }
        }

        // todo - how about instead of spamming these messages, we use it how it is (likely) intended by only sending this when it is necessary.
        private void UpdatePlayerDataChanges() // Sends Msg45 | Intended to ONLY be sent when necessary; but we spam lol
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)45);
            msg.Write((byte)GetNumberOfValidPlayerEntries());
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null)
                {
                    msg.Write(_players[i].ID);
                    msg.Write(_players[i].HP);
                    msg.Write(_players[i].ArmorTier);
                    msg.Write(_players[i].ArmorTapes);
                    msg.Write((byte)_players[i].WalkMode);
                    msg.Write(_players[i].HealthJuice);
                    msg.Write(_players[i].SuperTape);
                }
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        private void UpdatePlayerDrinking() // Appears OK
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null || !_players[i].isDrinking || (_players[i].NextHealTime > DateTime.UtcNow))
                    continue;

                if ((_players[i].HealthJuice > 0) && (_players[i].HP < 100))
                {
                    float hp = _healPerTick;
                    if ((hp + _players[i].HP) > 100)
                        hp = 100 - _players[i].HP;
                    if ((_players[i].HealthJuice - hp) < 0)
                        hp = _players[i].HealthJuice;

                    byte _tmp = (byte)hp; // spaghetti fix; ideally Player.HP is a float or a similar data-type to _healPerTick...
                    _players[i].HP += _tmp;
                    _players[i].HealthJuice -= _tmp;

                    if ((_players[i].HP == 100) || (_players[i].HealthJuice == 0))
                        SendPlayerEndDrink(_players[i]);
                    else
                        _players[i].NextHealTime = DateTime.UtcNow.AddSeconds(_healRateSeconds);
                }
                else
                    SendPlayerEndDrink(_players[i]);
            }
        }

        private void UpdatePlayerTaping() // Appears OK
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null || !_players[i].isTaping)
                    continue;

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
                if (_players[i]?.isEmoting != true)
                    continue;

                if (_players[i].EmoteEndTime <= DateTime.UtcNow)
                    HandleEmoteCancel(_players[i]);
            }
        }

        private void UpdateDownedPlayers() // Appears OK
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null || !_players[i].isAlive || !_players[i].isDown)
                    continue;

                if (_players[i].isBeingRevived && (DateTime.UtcNow >= _players[i].ReviveTime))
                    RevivePlayer(_players[i]);
                else if (!_players[i].isBeingRevived && (DateTime.UtcNow >= _players[i].NextBleedTime))
                {
                    _players[i].NextBleedTime = DateTime.UtcNow.AddSeconds(_bleedoutRateSeconds);
                    DamagePlayer(_players[i], 2 + (2 * _players[i].TimesDowned), _players[i].LastAttackerID, _players[i].LastWeaponID);
                    // Please see: https://animalroyale.fandom.com/wiki/Downed_state
                }
            }
        }
        
        private void UpdateStunnedPlayers() // Appears OK
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null || !_players[i].isStunned)
                    continue;
                
                if (DateTime.UtcNow >= _players[i].StunEndTime)
                {
                    _players[i].WalkMode = MovementMode.Walking;
                    _players[i].isStunned = false;
                    SendPlayerDataChange(_players[i]);
                }
            }
        }

        // Held together with glue and duct tape -- improvement opporunity
        // todo -- smelly smelly smelly smelly; that kind of smelly smell that smells smelly
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
                            if (plr == null)
                                continue;
                            if (!_traps[i].Position.IsNear(plr.Position, _traps[i].EffectRadius))
                                _traps[i].HitPlayers.Remove(plr);
                        }
                    }
                }
                // go through all trap entries and see whether any players are nearby to "activate" the traps
                for (int j = 0; j < _players.Length; j++) // todo - vial speedboost effect when entering skunk grenades (if we ever get to that version)
                {
                    if (_players[j]?.IsPlayerReal() != true)
                        continue;
                    if (_traps[i].Position.IsNear(_players[j].Position, _traps[i].EffectRadius))
                    {
                        switch (_traps[i].TrapType)
                        {
                            case TrapType.Banana:
                                // if vehicleID == -1, then they're in a hamsterball. Msg88 handles removing the trap in this case
                                if (_players[j].HamsterballID != -1)
                                    continue;

                                // player likely isn't in a hamsterball, so stun them
                                _players[j].Stun();
                                SendGrenadeFinished(_traps[i].OwnerID, _traps[i].ThrowableID, _traps[i].Position);

                                // force back if necessary
                                if (!_players[j].Position.IsNear(_traps[i].Position, 2f))
                                    SendForcePosition(_players[j], _traps[i].Position);
                                // remove
                                _traps.Remove(_traps[i]);
                                return; // spaghetti fix; probably works idrk

                            case TrapType.SkunkNade:
                                if (_players[j].isGodmode || _players[j].IsPIDMyTeammate(_traps[i].OwnerID))
                                    continue;

                                if (_traps[i].HitPlayers.ContainsKey(_players[j]))
                                {
                                    if (_traps[i].HitPlayers[_players[j]] > DateTime.UtcNow)
                                        continue;

                                    _traps[i].HitPlayers[_players[j]] = DateTime.UtcNow.AddSeconds(0.6f);
                                    DamagePlayer(_players[j], 13, _traps[i].OwnerID, _traps[i].WeaponID);
                                    
                                    if (!_poisonDamageQueue.Contains(_players[j]))
                                        _poisonDamageQueue.Add(_players[j]);
                                }
                                else
                                    _traps[i].HitPlayers.Add(_players[j], DateTime.UtcNow.AddSeconds(0.6f));
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
            if (TryPlayerFromID(player.SaviourID, out Player saviour))
                saviour.FinishedRessingPlayer();
            player.ReviveFromDownedState(_resurrectSetHP);
        }

        private void CheckForWinnerWinnerChickenDinner()
        {
            _hasPlayerDied = false;
            if (!isSorted)
                SortPlayerEntries();

            int numberOfTeams = GetNumberOfAliveTeams();
            if (numberOfTeams == 1)
            {
                for (int i = 0; i < _players.Length; i++)
                {
                    if ((_players[i] == null) || _players[i].isGhosted)
                        continue;

                    if (_players[i].isAlive)
                        SendRoundEnded(_players[i].ID);
                }
            }
            else if (numberOfTeams == 0)
                SendRoundEnded(_players[0].ID);
        }


        private void svu_CheckCoughs() // appears OK v0.90.2
        {
            if (_poisonDamageQueue.Count == 0)
                return;

            int listCount = _poisonDamageQueue.Count;
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)107);
            msg.Write((byte)listCount);
            for (int i = 0; i < listCount; i++)
                msg.Write(_poisonDamageQueue[i].ID);

            msg.Write((byte)listCount);
            for (int i = 0; i < listCount; i++)
                msg.Write(_poisonDamageQueue[i].LastAttackerID);

            msg.Write((byte)listCount);
            for (int i = 0; i < listCount; i++)
                msg.Write(_poisonDamageQueue[i].ID);

            msg.Write((byte)listCount);
            for (int i = 0; i < listCount; i++)
                msg.Write(_poisonDamageQueue[i].LastAttackerID);

            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
            _poisonDamageQueue.Clear();
        }

        private void ResetForRoundStart()
        {
            if (!IsServerRunning()) // this is used a lot... but there should be a better way to check this
                return;

            // reset Super Skunk Gas --- this is only because /safezone exists to be honest
            SkunkGasTotalApproachDuration = 5.0f;
            SkunkGasRemainingApproachTime = 5.0f;
            SkunkGasWarningDuration = 5.0f;
            canSSGApproach = false;
            isSkunkGasActive = false;
            isSkunkGasWarningActive = false;

            //Logger.Warn("[ResetForRoundStart] Resetting Player fields...");
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null)
                    continue;

                if (!_players[i].hasReadied)
                {
                    Logger.Basic($"[ResetForRoundStart] Player {_players[i].Name} ({_players[i].ID}) wasn't ready!");
                    _players[i].Client?.NetAddress?.Disconnect("Server did not receive ready in time!");
                    continue;
                }

                // flight
                _players[i].hasLanded = false;
                _players[i].hasEjected = false;
                _players[i].Position = _giantEagle.Start;

                // attack count stuff--- client/server has to match or else tomfoolery begins
                _players[i].AttackCount = -1;
                _players[i].Projectiles = new Dictionary<short, Projectile>();
            }
            //Logger.Basic("[ResetForRoundStart] Reset all required Player fields!");
        }

        // todo -- molecrate rewrite: kind of messy/ uses old system from before rewrite
        private void CheckMoleCrates()
        {
            if (_moleCrates == null || _moleCrates[0] == null)
                return;

            MoleCrate mole;
            for (int i = 0; i < _moleCrates.Length; i++)
            {
                mole = _moleCrates[i];
                if (mole == null || mole.isCrateReal)
                    continue;

                if (mole.IdleTime > 0f)
                {
                    mole.IdleTime -= (float)DeltaTime.TotalSeconds;
                    continue;
                }

                // mole is on the move-- are they at the end of the node, or still trying to get there?
                Vector2 thisEndPoint = mole.MovePositions[mole.MoveIndex];
                if (mole.Position == thisEndPoint)
                {
                    mole.MoveIndex++;
                    if (mole.MoveIndex >= mole.MovePositions.Length)
                    {
                        SendMolecrateCrateSpawned((short)i, mole.Position);
                        mole.isCrateReal = true;
                    }
                }
                else
                {
                    float deltaMove = (float)DeltaTime.TotalSeconds * SARConstants.DeliveryMoleMoveSpeed;
                    mole.Position = Vector2.MoveTowards(mole.Position, thisEndPoint, deltaMove);
                }
            }
        }

        /// <summary>
        /// (MATCH) Iterates over Match._campfires, checking the status of all entries. Updates lighting up; healing players; etc. Nulls-out a used campfire.
        /// </summary>
        private void CheckCampfiresMatch()
        {

            for (int i = 0; i < _campfires.Length; i++)
            {
                // another over-complicated system like the molecrate-- when a campfire is used, it is simply nulled.
                if (_campfires[i] == null)
                    continue;

                Campfire campfire = _campfires[i];
                if (campfire.isLit)
                {
                    if (campfire.UseRemainder > 0)
                        campfire.UseRemainder -= (float)DeltaTime.TotalSeconds;
                    else
                    {
                        _campfires[i] = null;
                        continue;
                    }
                }

                // Player stuff- Figure out if a Player is close enough to light it up or get healed by it.
                for (int j = 0; j < _players.Length; j++)
                {
                    if (_players[j] == null)
                        continue;

                    Player player = _players[j];
                    if (player.IsPlayerReal() && (player.HP < 100) && (player.Position.IsNear(campfire.Position, 24f)))
                    {
                        if (!campfire.isLit)
                        {
                            NetOutgoingMessage camplight = server.CreateMessage();
                            camplight.Write((byte)50); // MSG ID -- 50
                            camplight.Write((byte)i);  // byte | CampfireID ( I == this campfire's ID)
                            server.SendToAll(camplight, NetDeliveryMethod.ReliableUnordered);
                            campfire.isLit = true;
                        }
                        else if (player.NextCampfireTime < DateTime.UtcNow)
                        {
                            float healHP = _campfireHealPer; // Default = 4hp every 1 second
                            if ((player.HP + healHP) > 100)
                                healHP = 100 - player.HP;
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
                        if (_campfires[i].Position.IsNear(_players[j].Position, 24f))
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
                        DamagePlayer(_players[i], 1, -2, -1);
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
                        DamagePlayer(_players[i], _ddgDamagePerTick, _players[i].LastAttackerID, _players[i].LastWeaponID);
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

                case 5: // Appears OK
                    HandleReadyReceived(msg); // perhaps make sure that ready player can actually ready/ be stored and didn't reach player limit yet?
                    break;

                    // Msg7 -- Request GiantEagle Ejection >>> Msg8 -- SendForcePosition(land: true)
                case 7: // Appears OK
                    if (!_hasMatchStarted)
                        GoWarnModsAndAdmins($"{msg.SenderConnection} tried ejecting but match hasn't started.");
                    HandleEjectRequest(msg);
                    break;

                    // Msg14 -- Client Position Update >>> No further messages.
                    // data from this packet is saved/ used in Msg11 / Msg12 (lobby/ match position update respectively)
                case 14: // Appears OK
                    HandlePositionUpdate(msg);
                    break;

                    // Msg16 -- Client Attack Request >>> Msg17 --- Confirm Attack Request
                case 16: // OK... enough --- todo - lobby weapons tracked properly
                    HandleAttackRequest(msg);
                    break;

                    // Msg18 -- Confirm Attack >> Msg19 -- Confirmed Attack
                case 18:
                    HandleAttackConfirm(msg); // Cleanup / Other Improvements needed
                    break;

                    // Msg21 -- Client Request Loot --> Msg22 -- Server Confirm Loot  +(optional) Msg20 -- Server Sent Spawn Loot
                case 21:
                    if (_hasMatchStarted)
                        HandleLootRequestMatch(msg);
                    else
                        ServerHandleLobbyLootRequest(msg);
                    break;

                    // Msg25 -- Client Sent Chat Message --> Multiple potential further packets- see method
                    // quick list of potential responses: Msg26 [pub/ team chat], Msg94 [warnMsg], Msg106 [/roll msg]
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

                    // Msg51 - Coconut Eat Request >>> Msg52 -- Coconut Eat Confrim
                case 51: // Appears OK [7/24/23]
                    HandleCoconutEatRequest(msg);
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
                case 66: // Appears OK
                    HandleEmoteRequest(msg);
                    break;

                    // Msg70 -- Request Molecrate Open >>> Msg71 -- Confirm Molecrate Open | OK: v0.90.2
                case 70: // Molecrate Open Request v0.90.2
                    if (_hasMatchStarted)
                        HandleMolecrateOpenRequest(msg);
                    break;

                    // Msg72 -- Request to destroy Doodad object >>> Msg73 -- Confirm DoodadDestroyed
                case 72: // Appears OK-ish [8/11/23] -- todo is item drops from item-droppable Doodads.
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

                    // Msg97 -- "dummy" >>> send msg97 back
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
        private void HandleAuthenticationRequest(NetIncomingMessage pMsg)
        {
            // has this player already tried getting authenticated?
            if (_incomingClients.ContainsKey(pMsg.SenderConnection) || TryPlayerFromConnection(pMsg.SenderConnection, out Player player))
            {
                Logger.Failure($"[HandleAuthReq - Error] {pMsg.SenderConnection} already had their connection verified.");
                return;
            }
            try
            {
                // data reads
                string rPlayFabID = pMsg.ReadString();
                pMsg.ReadString(); // not necessary to keep | UnityAnalytics Sess. Ticket
                //string rUnityAnalysTicket = msg.ReadString(); // here if you want to add back
                bool rFillsDisabled = pMsg.ReadBoolean();
                string rPlayFabAuthTick = pMsg.ReadString();
                //Logger.Basic($"{rPlayFabAuthTick}");
                byte rPartyCount = pMsg.ReadByte();
                string[] rPartyPlayFabIDs = new string[rPartyCount];
                for (int i = 0; i < rPartyPlayFabIDs.Length; i++)
                {
                    rPartyPlayFabIDs[i] = pMsg.ReadString();
                    if (rPartyPlayFabIDs[i] == rPlayFabID)
                        Logger.DebugServer("sender playfab id is included in party count...");
                }

                // verify there is space for this player and their party members
                int numOfPlayers = GetNumberOfValidPlayerEntries();
                if (numOfPlayers == _players.Length || (numOfPlayers + 1 + rPartyCount) > _players.Length)
                {
                    SendAuthResponse(pMsg.SenderConnection, false);
                    return;
                } // this would normally be taken care of by the matchmaking system. however, regardless, one could simply force themsevles to always connect here. this is an extra layer.

                // banned list
                int bannedPlayersCount = _bannedPlayers.Count;
                for (int i = 0; i < bannedPlayersCount; i++)
                {
                    if (_bannedPlayers[i]["playfabid"] == rPlayFabID)
                    {
                        Logger.Warn($"[HandleAuthReq] [WARN] {rPlayFabID} @ {pMsg.SenderEndPoint} is banned. Dropping connection.");
                        string reason = "No reason provided.";
                        if (_bannedPlayers[i]["reason"] != null && _bannedPlayers[i]["reason"] != "")
                            reason = _bannedPlayers[i]["reason"];
                        pMsg.SenderConnection.Disconnect($"\nYou're banned from this server.\n\"{reason}\"");
                        return;
                    }
                } // same with this. the matchmaker should do this themselves, but we don't got one here so... yeah... also if you're banned anyways you can't launch the game so like??

                // todo: actually verify playFabID/ ticket

                // setup client info + add to incoming clients list.
                Client client = new Client(pMsg.SenderConnection, rPlayFabID, rPartyPlayFabIDs, rFillsDisabled);
                JSONNode playerData = GetPlayerNodeFromPlayFab(rPlayFabID);
                if (playerData != null)
                    client.SetUserInfo(playerData["name"], playerData["dev"], playerData["mod"], playerData["founder"]);
                _incomingClients.Add(client.NetAddress, client);

                // allow
                SendAuthResponse(pMsg.SenderConnection, true);
                Logger.Success($"[HandleAuthReq] [OK] Sent {client} an accept message!");
            }
            catch (NetException netEx)
            {
                Logger.Failure($"[HandleAuthReq - Error] {pMsg.SenderConnection} caused a NetException!\n{netEx}");
                pMsg.SenderConnection.Disconnect("There was an error reading your packet data. [CoconutEatReq]");
                _incomingClients.Remove(pMsg.SenderConnection);
            }
        }

        // Msg2 | "Authentication Response" --> None; however, server expects to receive Msg3 once the client's game loads.
        private void SendAuthResponse(NetConnection recipient, bool isAllow)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(2);
            msg.Write((byte)2); // 1 Byte | MsgID
            msg.Write(isAllow); // 1 Bool | IsVerified
            server.SendMessage(msg, recipient, NetDeliveryMethod.ReliableUnordered);
        }

        private void HandleIncomingPlayerRequest(NetIncomingMessage msg) // Msg3 >> Msg4
        {
            try
            {
                // get client - should this fail, they were most likely cheating to send this packet.
                Client incomingClient = _incomingClients[msg.SenderConnection]; // catch at bottom

                // read sent character data...
                // if you want to verify all these values... lots of data files or lazy-checks lol
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

                // create Player from data...
                short assignID = _availableIDs[0];
                _availableIDs.RemoveAt(0);
                Player newPlayer = new Player(assignID, animal, umbrella, gravestone, deathEffect, emoteIDs, hat, glasses, beard, clothes, melee,
                    gsCount, gsGunIDs, gsSkinIndicies, steamName, incomingClient);

                // add to players list...
                SortPlayerEntries();
                for (int i = 0; i < _players.Length; i++)
                {
                    if (_players[i] != null)
                        continue;

                    _players[i] = newPlayer;
                    break;
                }

                // add player to player-data.json if necessary
                if (GetPlayerNodeFromPlayFab(incomingClient.PlayFabID) == null)
                {
                    JSONNode newPlayerData = JSON.Parse($"{{playfabid:\"{incomingClient.PlayFabID}\",name:\"{steamName}\",dev:false,mod:false,founder:false}}");
                    _playerData.Add(newPlayerData);
                    JSONNode.SaveToFile(_playerData, _baseloc + "player-data.json");
                }

                // teams
                if (_gamemode != SARConstants.GamemodeSolos)
                    FindTeamToAddPlayerTo(newPlayer);

                // fin. send back to the client that server has processed their information successfully.
                _incomingClients.Remove(msg.SenderConnection);
                SendMatchInformation(msg.SenderConnection, assignID); // server done storing data; send next message in sequence
            }
            catch (NetException netEx)
            {
                Logger.Failure($"[HandleIncomingPlayerRequest] [Error] {msg.SenderConnection} caused a NetException!\n{netEx}");
                msg.SenderConnection.Disconnect("There was an error reading your packet data. [HandleIncomingConnection]");
            }
            catch (KeyNotFoundException)
            {
                Logger.Failure($"[IncomingPlayerRequest] {msg.SenderConnection} not within incoming connections list. They have been disconnected.");
                msg.SenderConnection.Disconnect("Incoming player already processed.");
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

        // Msg5 | "Client Ready" --> Msg10 "Player Characters" --- called whenever we receive a client's "readied" message (game finished loading and junk)
        private void HandleReadyReceived(NetIncomingMessage pMsg)
        {
            if (VerifyPlayer(pMsg.SenderConnection, "HandlePlayerReadied", out Player player))
            {
                player.hasReadied = true; // somehow have managed to make this whole method so barren...
                SendAllPlayerCharacters(GetAllReadiedAlivePlayers());

                // my stuff here --- not necessary, but I find it useful
                if (!_canCheckWins && _safeMode) // log levels?
                    GoWarnModsAndAdmins("Warning: _canCheckWins is set to false! This means that matches will go on forever!*\nUse '/togglewins' to re-enable this!\n\n*using '/kill' will end the match if there are no more players remaining.");
            }
        }

        // Msg10 | "Player Characters" --> No Further --- we send Msg10 after handling a "player ready"
        private void SendAllPlayerCharacters(List<Player> listOfReadiedPlayers)
        {
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)10);
            msg.Write((byte)listOfReadiedPlayers.Count);
            foreach (Player player in listOfReadiedPlayers)
            {
                msg.Write(player.ID);               // 2 Short   | PlayerID
                msg.Write(player.AnimalID);         // 2 Short   | CharacterID
                msg.Write(player.UmbrellaID);       // 2 Short   | UmbrellaID
                msg.Write(player.GravestoneID);     // 2 Short   | GravestoneID
                msg.Write(player.DeathExplosionID); // 2 Short   | DeathExplosionID
                for (int j = 0; j < 6; j++)         // V Short[] | PlayerEmotes: Note - always 6 in v0.90.2
                    msg.Write(player.EmoteIDs[j]);  // 2 Short   | EmoteID[i]
                msg.Write(player.HatID);            // 2 Short   | HatID
                msg.Write(player.GlassesID);        // 2 Short   | GlassesID
                msg.Write(player.BeardID);          // 2 Short   | BeardID
                msg.Write(player.ClothesID);        // 2 Short   | ClothesID
                msg.Write(player.MeleeID);          // 2 Short   | MeleeID
                msg.Write(player.GunSkinCount);     // 1 Byte    | AmountOfGunSkins
                for (int k = 0; k < player.GunSkinKeys.Length; k++)
                {
                    msg.Write(player.GunSkinKeys[k]);    // 2 Short | GunSkinKey[i]
                    msg.Write(player.GunSkinValues[k]);  // 1 Byte  | GunSkinValue[i]
                }
                msg.Write(player.Position.x);   // 4 Float  | PositionX
                msg.Write(player.Position.y);   // 4 Float  | PositionY
                msg.Write(player.Name);         // V String | Username
                msg.Write(player.EmoteID);      // 2 Short  | CurrentEmote (emote players will still dance when joining up)
                msg.Write((short)player.LootItems[0].WeaponIndex);  // 2 Short | Slot1 WeaponID -- TODO: Equiping items in Lobby should be real?
                msg.Write((short)player.LootItems[1].WeaponIndex);  // 2 Short | Slot2 WeaponID
                msg.Write(player.LootItems[0].Rarity);              // 1 Byte  | Slot1 WeaponRarity
                msg.Write(player.LootItems[1].Rarity);              // 1 Byte  | Slot1 WeaponRarity
                msg.Write(player.ActiveSlot);                       // 1 Byte  | ActiveSlotID
                msg.Write((bool)player.Client?.isDev);              // 1 Bool  | IsDeveloper
                msg.Write((bool)player.Client?.isMod);              // 1 Bool  | IsModerator
                msg.Write((bool)player.Client?.isFounder);          // 1 Bool  | IsFounder
                msg.Write((short)player.Client?.AccountLevel);      // 2 Short | PlayerLevel
                                                                    // V Byte  | # of Teammates 
                if (_gamemode == SARConstants.GamemodeSolos)        // Solos version...
                    msg.Write((byte)0);                             // Solos version...
                else
                {
                    msg.Write((byte)(player.Teammates.Count + 1));  // team version...
                    msg.Write(player.ID);                           // 2 Short | Teammate ID
                    foreach (Player mate in player.Teammates)
                        msg.Write(mate.ID);
                }
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        /// <summary>Handles a NetMessage marked as a "EjectRequest" packet (Msg7).</summary>
        /// <param name="pmsg">NetMessage to read the packet data from.</param>
        private void HandleEjectRequest(NetIncomingMessage pmsg) // Msg7
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleEjectRequest", out Player player))
            {
                if (!player.hasEjected)
                {
                    if (player.Position.IsNear(_giantEagle.Position, 8f))
                        SendForcePosition(player, player.Position, true);
                    else
                        SendForcePosition(player, _giantEagle.Position, true);
                }
                else
                    Logger.Failure($"[EjectRequest] {player} has already ejected from the Giant Eagle!");
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

        // Msg9 | "Match Ended"
            // --> No Further NetMsgs
        private void SendRoundEnded(short winnerID)
        {
            NetOutgoingMessage msg = server.CreateMessage(3);
            msg.Write((byte)9);     // 1 Byte  | MsgID (9)
            msg.Write(winnerID);    // 2 Short | Winning PlayerID
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        // Msg11 | [LOBBY] "Update Player Positions" --- we send to players during lobby
            // --> No Further NetMsgs
        private void SendLobbyPlayerPositions()
        {
            if (!IsServerRunning()) return;
            // Make message sending player data. Loops entire list but only sends non-null entries.
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)11);                    // Byte | MsgID (11)
            msg.Write((byte)GetNumberOfValidPlayerEntries()); // Byte | # of Iterations
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null)
                    continue;

                msg.Write(_players[i].ID);                                              // Short | PlayerID
                msg.Write((sbyte)((180f * _players[i].MouseAngle / 3.141592f) / 2));    // sbyte  | LookAngle
                msg.Write((ushort)(_players[i].Position.x * 6f));                       // ushort | PositionX 
                msg.Write((ushort)(_players[i].Position.y * 6f));                       // ushort | PositionY
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        // Msg12 | [IN-MATCH] "Update Player Positions" --- we send to players during in-progress matches
            // --> No Further NetMsgs
        private void SendMatchPlayerPositions()
        {
            if (!IsServerRunning()) return;
            // Make message sending player data. Loops entire list but only sends non-null entries.
            NetOutgoingMessage msg = server.CreateMessage();
            msg.Write((byte)12);                    // Byte | Header
            msg.Write((byte)GetNumberOfValidPlayerEntries()); // Byte | Count of valid entries the Client is receiving/iterating over
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null)
                    continue;

                msg.Write(_players[i].ID);
                msg.Write((short)(180f * _players[i].MouseAngle / 3.141592f));
                msg.Write((ushort)(_players[i].Position.x * 6f));
                msg.Write((ushort)(_players[i].Position.y * 6f));
                if (_players[i].WalkMode == MovementMode.HampterBalling)
                {
                    msg.Write(true);
                    msg.Write((short)(_players[i].HamsterballVelocity.x * 10f));
                    msg.Write((short)(_players[i].HamsterballVelocity.y * 10f));
                }
                else
                    msg.Write(false);
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        // Msg14 | "Client Player Status" --- spammed by clients; it contains stuff like their perceived current position/ mouse angle/ "move mode"
            // --> No Further NetMsgs
        private void HandlePositionUpdate(NetIncomingMessage pmsg) // Msg14 | TODO -- make sure player isn't bouncing around the whole map
        {
            if (!IsServerRunning()) return;
            if (VerifyPlayer(pmsg.SenderConnection, "HandlePositionUpdate", out Player player))
            {
                // note - msg14 is NEVER sent by a dead player. dead players instead use msg44!
                if (!player.isAlive)
                    return;
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

                    MovementMode rWalkMode = (MovementMode)walkMode;
                    switch (rWalkMode)
                    {
                        case MovementMode.Rolling:
                        case MovementMode.CreepRolling:
                            CheckMovementConflicts(player);
                            if (player.isReloading)
                                SendCancelReload(player);
                            break;

                        case MovementMode.HampterBalling:
                            if (player.HamsterballID >= 0)
                            {
                                float vehicleX = (float)(pmsg.ReadInt16() / 10f);
                                float vehicleY = (float)(pmsg.ReadInt16() / 10f);
                                player.HamsterballVelocity = new Vector2(vehicleX, vehicleY);
                                if (_hamsterballs.ContainsKey(player.HamsterballID))
                                    _hamsterballs[player.HamsterballID].Position = player.Position; // added
                                else
                                    GoWarnModsAndAdmins($"H14) {player} riding in Hamsterball \"{player.HamsterballID}\"; which no longer exists, but they say it does.");
                            }
                            break;

                        case MovementMode.Downed:
                            if (!player.isDown)
                                rWalkMode = MovementMode.Walking;
                            break;

                        case MovementMode.BananaStunned:
                            if (!player.isStunned)
                                rWalkMode = MovementMode.Walking;
                            break;
                        default:
                            break;
                    }

                    // try to force walkmodes if player trying to cheese their way out...
                    if (player.isDown && (rWalkMode != MovementMode.Downed))
                        rWalkMode = MovementMode.Downed;
                    if (player.isStunned && (rWalkMode != MovementMode.BananaStunned))
                        rWalkMode = MovementMode.BananaStunned;

                    // actually set server-side positions
                    player.Position = new Vector2(posX, posY);
                    player.MouseAngle = mouseAngle;
                    player.WalkMode = rWalkMode;

                    // other checks...
                    if (player.isEmoting && !player.Position.IsNear(player.EmotePosition, 4f))
                        HandleEmoteCancel(player);

                    // todo - fix this breaking sometimes...
                    if (player.isReviving && TryPlayerFromID(player.RevivingID, out Player whoImRessing) && !player.Position.IsNear(whoImRessing.Position, 3f))
                        HandlePickupCanceled(player);
                    if (player.isBeingRevived && TryPlayerFromID(player.SaviourID, out Player mySaviour) && !player.Position.IsNear(mySaviour.Position, 3f))
                        HandlePickupCanceled(player);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandlePositionUpdate] Player @ {pmsg.SenderConnection} caused a NetException!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("There was an error while reading your packet data! (HandlePositionUpdate)");
                }
            }
        }

        // Msg15 | "Player Died" --- Sent to all connected NetPeers whenever a Player dies.
            // --> No Further NetMsgs
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

        // Handles the killing of Players. Could be cleaned up
        private void HandlePlayerDied(Player player)
        {
            int aliveTeams = GetNumberOfAliveTeams();
            player.Placement = (byte)aliveTeams;
            player.isAlive = false;

            CheckMovementConflicts(player);
            SendPlayerDeath(player.ID, player.Position, player.LastAttackerID, player.LastWeaponID);
            SendEndScreenItemDrops(player);

            // summon loot
            Vector2 position = player.Position;
            _hasPlayerDied = true;
            if (player.ArmorTier > 0)
                SendSpawnedLoot(_level.NewLootArmor(player.ArmorTier, player.ArmorTapes, position), ref position);

            if (player.HealthJuice > 0)
                SendSpawnedLoot(_level.NewLootJuice(player.HealthJuice, position), ref position);

            if (player.SuperTape > 0)
                SendSpawnedLoot(_level.NewLootTape(player.SuperTape, position), ref position);

            for (int i = 0; i < player.Ammo.Length; i++)
            {
                if (player.Ammo[i] > 0)
                    SendSpawnedLoot(_level.NewLootAmmo((byte)i, player.Ammo[i], position), ref position);
            }
            for (int j = 0; j < player.LootItems.Length; j++)
            {
                if (player.LootItems[j] == null)
                    continue;

                if (player.LootItems[j].LootType != LootType.Collectable)
                    SendSpawnedLoot(_level.NewLootWeapon(player.LootItems[j].WeaponIndex, player.LootItems[j].Rarity, player.LootItems[j].GiveAmount, position), ref position);
            }
        }

        // Msg16 | "Player Attack" --- Received whenever a client/ player tries attacking (swinging melee/ shooting gun)
        private void HandleAttackRequest(NetIncomingMessage amsg)
        {
            if (VerifyPlayer(amsg.SenderConnection, "HandleAttackRequest", out Player player))
            {
                try
                {
                    if (!player.IsPlayerReal())
                        return;

                    if (player.isReloading)
                        SendCancelReload(player);
                    CheckMovementConflicts(player);

                    // data reads -- kind of messy, but it is what it is
                    short rWeaponID = amsg.ReadInt16();
                    byte rSlotID = amsg.ReadByte();
                    float rLookAngle = amsg.ReadInt16();
                    float rSpawnX = amsg.ReadFloat();
                    float rSpawnY = amsg.ReadFloat();
                    bool rIsValidAttack = amsg.ReadBoolean();
                    bool rDidHitDestructble = amsg.ReadBoolean();
                    if (rDidHitDestructble)
                        HandleDoodadDestructionFromPoint(new Vector2(amsg.ReadInt16(), amsg.ReadInt16()));
                    short rAttackID = amsg.ReadInt16();
                    byte rProjectileCount = amsg.ReadByte();
                    float[] rProjectileAngles = new float[rProjectileCount];
                    short[] rProjectileIDs = new short[rProjectileCount];
                    bool[] rProjectileValids = new bool[rProjectileCount];
                    for (byte i = 0; i < rProjectileCount; i++)
                    {
                        rProjectileAngles[i] = amsg.ReadInt16() / 57.295776f;
                        rProjectileIDs[i] = amsg.ReadInt16();
                        rProjectileValids[i] = amsg.ReadBoolean();

                        if (player.Projectiles.ContainsKey(rProjectileIDs[i]))
                        {
                            Logger.Failure($"[HandleAttackRequest] Key \"{rProjectileIDs[i]}\" already exists in Player projectile list!");
                            return;
                        }
                    }

                    // actual verification of whether this action is even valid
                    player.AttackCount += 1;
                    if (player.AttackCount != rAttackID)
                    {
                        Logger.Failure($"[HandleAttackRequest] [ERROR] {player} attack count mis-aligned!! sent: {rAttackID}: stored: {player.AttackCount}");
                        return;
                    }

                    if (_hasMatchStarted && (rSlotID < 2)) // todo - lobby weapons tracked properly
                    {
                        if (!player.IsGunAndSlotValid(rWeaponID, rSlotID))
                        {
                            Logger.Failure($"[Server Handle - AttackRequest] Player @ {amsg.SenderConnection} sent invalid wepaonID / slot.");
                            amsg.SenderConnection.Disconnect("There was an error processing your request. Message: Action Invalid! Weapon / Slot mis-match!");
                            return;
                        }
                        else if (-1 >= (player.LootItems[rSlotID].GiveAmount - 1))
                        {
                            Logger.Failure($"[Server Handle - AttackRequest] Player @ {amsg.SenderConnection} gun shots go into negatives. May be a mis-match.");
                            amsg.SenderConnection.Disconnect("There was an error processing your request. Message: Action Invalid! Shot-count mis-match!");
                            return;
                        }
                        else player.LootItems[rSlotID].GiveAmount -= 1;
                    } // end of verifications

                    // sending & spawning of projectiles
                    SendPlayerAttack(player.ID, player.LastPingTime, rWeaponID, rSlotID, rAttackID, rLookAngle,
                        rSpawnX, rSpawnY, rIsValidAttack, rProjectileAngles, rProjectileIDs, rProjectileValids);

                    for (int i = 0; i < rProjectileCount; i++)
                    {
                        Projectile spawnProj = new Projectile(rWeaponID, player.LootItems[rSlotID].Rarity, rSpawnX, rSpawnY, rProjectileAngles[i]);
                        player.Projectiles.Add(rProjectileIDs[i], spawnProj);
                    }

                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleAttackRequest] Player @ NetConnection \"{amsg.SenderConnection}\" gave NetError!\n{netEx}");
                    amsg.SenderConnection.Disconnect("There was an error procssing your request. Message: Read past buffer size...");
                }
            }
        }

        // Msg17 - Confirm/Send PlayerAttack
        private void SendPlayerAttack(short pPlayerID, float pLastPing, short pWeaponID, byte pSlotID, short pAttackID, float pAimAngle, float pSpawnX, float pSpawnY, bool pIsValid, float[] pProjectileAngles, short[] pProjectileIDs, bool[] pValidProjectiles)
        {
            NetOutgoingMessage msg = server.CreateMessage(22 + (6 * pProjectileIDs.Length));
            msg.Write((byte)17);                                // 1 Byte   | MsgID (17)
            msg.Write(pPlayerID);                               // 2 Short  | PlayerID
            msg.Write((ushort)(pLastPing * 1000f));             // 2 uShort | Ping
            msg.Write(pWeaponID);                               // 2 Short  | WeaponID / WeaponIndex
            msg.Write(pSlotID);                                 // 1 Byte   | Slot
            msg.Write(pAttackID);                               // 2 Short  | AttackID
            msg.Write((short)(3.1415927f / pAimAngle * 180f));  // 2 Short  | Aim-Angle (having to do these shenanigans is so stupid...)
            msg.Write(pSpawnX);                                 // 4 Float  | SpawnX
            msg.Write(pSpawnY);                                 // 4 Float  | SpawnY
            msg.Write(pIsValid);                                // 1 Bool   | isValid
            msg.Write((byte)pProjectileIDs.Length);             // 1 Byte   | Amount of Projectiles
            for (byte i = 0; i < pProjectileIDs.Length; i++)
            {
                msg.Write((short)(pProjectileAngles[i] / 3.1415927f * 180f));   // 2 Short | Angle of Projectile
                msg.Write(pProjectileIDs[i]);                                   // 2 Short | Projectile ID
                msg.Write(pValidProjectiles[i]);                                // 2 Bool  | isThisProjectileValid ?
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        // Msg18 - Request AttackConfirm >>> Msg19 - AttackConfirmed
        private void HandleAttackConfirm(NetIncomingMessage pmsg) // TOOD -- Make sure shot should've hit + damage falloff + cleanup
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleAttackConfirm", out Player player))
            {
                try
                {
                    if (!player.IsPlayerReal())
                        return;

                    short targetID = pmsg.ReadInt16();
                    short weaponID = pmsg.ReadInt16();
                    short projectileID = pmsg.ReadInt16();
                    float hitX = pmsg.ReadFloat();
                    float hitY = pmsg.ReadFloat();
                    // Basic Checks
                    if ((weaponID < 0) || (weaponID >= _weapons.Length))
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
                    if (!target.IsPlayerReal() || (bool)player.Teammates?.Contains(target))
                        return;
                    
                    if (!_hasMatchStarted || target.isGodmode)
                    {
                        if (target.HamsterballID != -1)
                            SendAttackConfirmed(player.ID, target.ID, projectileID, 0, target.HamsterballID, _hamsterballs[target.HamsterballID].HP);
                        else
                            SendAttackConfirmed(player.ID, target.ID, projectileID, 0);
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
                    if (target.HamsterballID >= 0) // Target in Hamsterball ?
                    {
                        if (!_hamsterballs.ContainsKey(target.HamsterballID))
                            return;
                        Hamsterball hamsterball = _hamsterballs[target.HamsterballID];
                        int ballDamage = 1;
                        if (weapon.VehicleDamageOverride > 0)
                            ballDamage = weapon.VehicleDamageOverride;
                        if ((hamsterball.HP - ballDamage) < 0)
                            ballDamage = hamsterball.HP;
                        hamsterball.HP -= (byte)ballDamage;
                        SendAttackConfirmed(player.ID, target.ID, projectileID, 0, hamsterball.ID, hamsterball.HP);
                        if (hamsterball.HP == 0)
                            DestroyHamsterball(hamsterball.ID);
                        return;
                    } // Hamsterball End >> NOT in Hamsterball:

                    if (target.ArmorTapes <= 0) // No ArmorTicks
                    {
                        int damage = weapon.Damage;
                        if (projectileID >= 0)
                        {
                            damage += player.Projectiles[projectileID].WeaponRarity * weapon.DamageIncrease;
                        }
                        SendAttackConfirmed(player.ID, target.ID, projectileID, 0);
                        DamagePlayer(target, damage, player.ID, weaponID);
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
                        DamagePlayer(target, damage, player.ID, weaponID);
                    }
                    else if (weapon.WeaponType == WeaponType.Melee)
                        DamagePlayer(target, (int)Math.Floor(weapon.Damage / 2f), player.ID, weaponID);
                    // Sometimes darts double tick. Did this get fixed? Not sure!
                    SendAttackConfirmed(player.ID, target.ID, projectileID, armorDamage);
                } catch (NetException netEx)
                {
                    Logger.Failure($"[HandleAttackConfirm] Player @ {pmsg.SenderConnection} caused a NetException!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("An error occurred whilst reading your packet data.");
                }
            }
        }

        /// <summary>
        ///  Removes the provided amount of health from a Player. If the damage attempt results in 0hp, downing/ deaths/ team deaths are handled.
        /// </summary>
        /// <param name="player"> Player whose health is to be removed.</param>
        /// <param name="damage"> Amount of HP to remove from the provided Player.</param>
        /// <param name="sourceID"> ID of the source (player/ otherwise) that initated the attack iniating this damage attempt.</param>
        /// <param name="weaponID"> ID of the weapon used during the attack iniating this damage attempt.</param>
        private void DamagePlayer(Player player, int damage, short sourceID, short weaponID)
        {
            // todo - player states:: if they are ever added, they likely can be used here as well
            if (!player.isAlive || player.isGodmode)
                return;
            //Logger.DebugServer($"Player {player.Name} (ID: {player.ID}) Health: {player.HP}\nDamage Attempt: {damage}");
            
            // set source and damage
            player.SetLastDamageSource(sourceID, weaponID);
            if (damage >= player.HP) // hp - damage =< 0
            {
                if (!player.isDown && player.Teammates.Count > 0)
                {
                    if (player.AliveNonDownTeammteCount() == 0)
                    {
                        for (int i = 0; i < player.Teammates.Count; i++)
                            if (player.Teammates[i].isAlive) // clients are fine re-killing players if the server says so apparently
                                HandlePlayerDied(player.Teammates[i]);
                        HandlePlayerDied(player);
                    }
                    else
                        HandlePlayerDowned(player, sourceID, weaponID);
                }
                else
                    HandlePlayerDied(player);
            }
            else // hp - damage > 0
                player.HP -= (byte)damage;
            //Logger.DebugServer($"Final Health: {player.HP}");
        }

        /// <summary>
        ///  Sends "Attack Confirmed" (v0.90.2 = ID 19) packet to all NetConnections connected to the current Match's server.
        /// </summary>
        /// <param name="pAttackerID"> ID of the Player who initated this attack.</param>
        /// <param name="pTargetID"> ID of the Player who was hit by this attack.</param>
        /// <param name="pProjectileID"> ID of the projectile used to carry out this attack (-1 = melee; 0+ = bullet).</param>
        /// <param name="pArmorDamage"> The amount of armor that should be removed from the hit player.</param>
        /// <param name="pHamsterballID"> ID of the hamsterball that was hit during this attack. (-1 = no hampterball hit).</param>
        /// <param name="pHampterNewHP"> The amount of hp remaining on the hit hamsterball. This is instantaneous; that is, a value of 0 destroys the ball, whereas a value of 1 results in a ball with 1 hp remaining.</param>
        private void SendAttackConfirmed(short pAttackerID, short pTargetID, short pProjectileID, byte pArmorDamage, short pHamsterballID = -1, byte pHampterNewHP = 0)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(11);
            msg.Write((byte)19);        // 1 Byte  | MsgID (19)
            msg.Write(pAttackerID);     // 2 Short | AttackingPlayerID
            msg.Write(pTargetID);       // 2 Short | HitPlayerID
            msg.Write(pProjectileID);   // 2 Short | ProjectileID
            msg.Write(pArmorDamage);    // 1 Byte  | ArmorStripAmount
            msg.Write(pHamsterballID);  // 2 Short | HitHamsterballID
            msg.Write(pHampterNewHP);   // 1 Byte  | HamsterballNewHP
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        // Msg25 >> Msg26 OR Msg94 OR Msg106 | "Client ChatMsg Request" -- Called whenever a player sends a chat... message... what did you expect?
        private void HandleChatMessage(NetIncomingMessage pmsg)
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleChatMessage", out Player player))
            {
                try
                {
                    if (!player.hasReadied)
                    {
                        Logger.Failure($"[HandleChatMessage] Player sent a chat message, but they aren't marked as ready. Assuming this is an error on that part; or something else entirely is wrong.");
                        return;
                    }

                    // incoming message data -- guaranteed to be this way v0.90.2
                    string text = pmsg.ReadString();
                    bool wasToTeam = pmsg.ReadBoolean();

                    // """command""" system -- prepare for brain damage!!!
                    // this is what happens when you add simple quick test cases using the chat and then never fix!
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
                                            responseMsg = "\nYou're not supposed to see this... Try inputting a command listed in '/help'? :]";
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

                            #region removable commands
                            // adds new player but if solos or team is full gets put on other team
                            case "/p": // you shouldn't use this unless you know what you're doing
                                for (int i = 0; i < _players.Length; i++)
                                {
                                    if (_players[i] != null)
                                        continue;
                                    Player addPlayer = new Player((short)i, 0, 0, 0, 0, new short[] { -1, -1, -1, -1, -1, -1 }, 0, 0, 0, 0, 0, 0, new short[] { }, new byte[] { }, "tmp", new Client());
                                    addPlayer.hasReadied = true;

                                    _players[i] = addPlayer;
                                    if (_gamemode != SARConstants.GamemodeSolos)
                                        FindTeamToAddPlayerTo(addPlayer);

                                    SendAllPlayerCharacters(GetAllReadiedAlivePlayers());
                                    responseMsg = "added a new player...";
                                    break;
                                }
                                break;

                            case "/gb":
                                responseMsg = "Valid HamsterballIDs: ";
                                foreach (int key in _hamsterballs.Keys)
                                    responseMsg += $"{key}, ";
                                break;
                            case "/b":
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (int.TryParse(command[1], out int ballKey))
                                    {
                                        if (_hamsterballs.ContainsKey(ballKey))
                                        {
                                            SendForcePosition(player, _hamsterballs[ballKey].Position);
                                            responseMsg = $"Teleported {player} to Hamsterball[{ballKey}].";
                                        }
                                        else responseMsg = $"No such hamsterball \"{ballKey}\"!";
                                    }
                                    else responseMsg = $"Command error: not INT.";
                                }
                                else
                                    responseMsg = "Insufficient # of arguments provided.\n\"/tpball\" takes at least 1!\nUsage: /tpball [ballID] OR /tpball [ballID] <X> <Y>.";
                                break;

                            case "/forceb":
                                {

                                    if (TryPlayerFromString(command[1], out Player ballee))
                                    {
                                        short hamsterballID = short.Parse(command[2]);
                                        if (_hamsterballs.ContainsKey(hamsterballID))
                                        {
                                            CheckMovementConflicts(ballee);
                                            ballee.SetHamsterball(hamsterballID);
                                            _hamsterballs[hamsterballID].CurrentOwner = ballee;
                                            SendHamsterballEntered(ballee.ID, (short)hamsterballID, _hamsterballs[hamsterballID].Position);
                                        }
                                    }
                                }
                                break;

                            case "/leaveball":
                                {
                                    if (TryPlayerFromString(command[1], out Player ballee))
                                    {

                                        Hamsterball hamsterball = _hamsterballs[ballee.HamsterballID];
                                        hamsterball.Position = ballee.Position;
                                        hamsterball.CurrentOwner = null;
                                        ballee.ResetHamsterball();
                                        SendHamsterballExit(ballee.ID, hamsterball.ID, ballee.Position);
                                    }
                                }
                                break;

                            case "/setlanded": // gotta remove this broski
                                for (int i = 0; i < _players.Length; i++)
                                {
                                    if (_players[i] != null)
                                        _players[i].hasLanded = true;
                                }
                                responseMsg = "set EVERYONE to as having landed";
                                break;

                            case "/tpbarrel":
                                {
                                    foreach (Doodad doodad in _level.Doodads)
                                    {
                                        if (doodad.Type.DestructibleDamageRadius > 0)
                                        {
                                            SendForcePosition(player, doodad.Position);
                                            responseMsg = $"Sent you to Doodad @ {doodad.Position}";
                                            break;
                                        }
                                    }
                                }
                                break;

                            case "/boomb":
                                {
                                    try
                                    {
                                        float x = int.Parse(command[1]);
                                        float y = int.Parse(command[2]);

                                        HandleDoodadDestructionFromPoint(new Vector2(x, y));
                                        responseMsg = $"Tried destroying Doodad @ {(x, y)}";
                                    }
                                    catch (Exception ex)
                                    {
                                        responseMsg = $"{ex}";
                                    }
                                }
                                break;

                            case "/tpb":
                                try
                                {
                                    short ballID = short.Parse(command[1]);
                                    if (!_hamsterballs.ContainsKey(ballID))
                                    {
                                        responseMsg = $"There is no ball with id: \"{ballID}\"";
                                        break;
                                    }
                                    SendHamsterballExit(player.ID, ballID, player.Position);
                                }
                                catch (Exception ex)
                                {
                                    responseMsg = $"Goofed somewhere";
                                    Logger.Failure($"There was indeed an error at some point for doing something I don't really care what\n{ex}");
                                }
                                break;

                            case "/mates":
                                Logger.Success("/mates has been used!");
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (TryPlayerFromString(command[1], out Player mates))
                                    {
                                        string print = $"{mates} has {mates.Teammates.Count} teammates for a total of {mates.Teammates.Count + 1} players. isFillsDisabled: {mates.Client.isFillsDisabled}\n0) {mates}\n";
                                        for (int i = 0; i < mates.Teammates.Count; i++)
                                        {
                                            print += $"{i + 1}) {mates.Teammates[i]}\n";
                                        }
                                        print += "-----------";
                                        Logger.Basic(print);
                                    }
                                    else responseMsg = $"Could not locate player \"{command[1]}\"";
                                }
                                else
                                {
                                    string print = $"{player} has {player.Teammates.Count} teammates for a total of {player.Teammates.Count + 1} players. isFillsDisabled: {player.Client.isFillsDisabled}\n0) {player}\n";
                                    for (int i = 0; i < player.Teammates.Count; i++)
                                    {
                                        print += $"{i + 1}) {player.Teammates[i]}\n";
                                    }
                                    print += "-----------";
                                    Logger.Basic(print);
                                }
                                break;

                            // testing if teammates can "dc" but stick around and be used for junk
                            case "/d": // you also shouldn't use this unless you know what you are doing.
                                if (command.Length >= 2 && command[1] != "")
                                {
                                    if (TryPlayerFromString(command[1], out Player dcP))
                                        HandleTeammateLeavingMatch(dcP, false);
                                    else
                                        responseMsg = $"Could not locate player \"{command[1]}\"";
                                }
                                else
                                    responseMsg = "Missing some stuff...";
                                break;
                            case "/check": //12/1/23 --> can remove
                                try
                                {
                                    bool wasSafe;
                                    wasSafe = _level.IsValidPlayerLoc(ref player.Position);
                                    responseMsg = $"{player.Position} valid: {wasSafe}";
                                }
                                catch (Exception excp)
                                {
                                    GoWarnModsAndAdmins("command broke");
                                    Logger.Failure($"/check - erorr\n{excp}");
                                }
                                break;
                            case "/quick":
                                try
                                {
                                    int x = (int)player.Position.x;
                                    int y = (int)player.Position.y;
                                    bool wasSafe = _level.QuickIsValidPlayerLoc(x, y);
                                    responseMsg = $"{player.Position} (used: {(x, y)}) valid: {wasSafe}";
                                }
                                catch (Exception excp)
                                {
                                    GoWarnModsAndAdmins("command broke");
                                    Logger.Failure($"/quick - erorr\n{excp}");
                                }
                                break;
                            case "/move":
                                if (command.Length >= 3 && command[1] != "")
                                {
                                    try
                                    {
                                        float x = float.Parse(command[1]);
                                        float y = float.Parse(command[2]);
                                        SendForcePosition(player, new Vector2(x, y), false);
                                        responseMsg = $"teleported you to {(x, y)}";
                                    }
                                    catch (Exception ex)
                                    {
                                        responseMsg = "ouch! error handling the command!";
                                        Logger.Failure($"unhandled general exception:\n{ex}");
                                    }
                                }
                                else
                                    responseMsg = "Insufficient amount of arguments provided. usage: /move {positionX} {positionY}";
                                break;
                            case "/improve":
                                if (_level.QuickIsValidPlayerLoc((int)player.Position.x, (int)player.Position.y))
                                    responseMsg = "Your position is already valid.";
                                else
                                {
                                    int x = (int)player.Position.x, y = (int)player.Position.y;
                                    Vector2 newpos = _level.FindWalkableGridLocation(x, y);
                                    SendForcePosition(player, newpos, false);
                                    responseMsg = $"You have been teleported to {newpos}";
                                }
                                break;
                            case "/levelsize":
                                responseMsg = $"LevelWidth={_level.LevelWidth}\nLevel Height={_level.LevelHeight}";
                                break;
                            case "/type":
                                try
                                {
                                    int x = (int)player.Position.x;
                                    int y = (int)player.Position.y;
                                    responseMsg = $"{(x, y)} type: {_level.CollisionGrid[x][y]}";
                                }
                                catch (Exception excp)
                                {
                                    GoWarnModsAndAdmins("command broke");
                                    Logger.Failure($"/type - erorr\n{excp}");
                                }
                                break;
                            #endregion removable commands

                            case "/kick":
                                if (player.Client.isMod || player.Client.isDev)
                                {
                                    if (command.Length >= 2 && command[1] != "")
                                    {
                                        if (TryPlayerFromString(command[1], out Player kicker))
                                        {
                                            responseMsg = $"Kicked {kicker}.";
                                            HandleTeammateLeavingMatch(kicker);
                                            kicker.Client?.NetAddress?.Disconnect("You've been kicked!");
                                        }
                                        else responseMsg = $"Could not locate player \"{command[1]}\"";
                                    }
                                    else responseMsg = "Insufficient # of arguments provided! /kick takes at least one!";
                                }
                                else responseMsg = "You do not have the required permissions to use this command. You must be a DEV or MOD!";
                                break;

                            case "/ban":
                                // todo - improve & bans that expire
                                if (player.Client.isDev)
                                {
                                    if (command.Length >= 2 && command[1] != "")
                                    {
                                        if (TryPlayerFromString(command[1], out Player banP))
                                        {
                                            string reason = "";
                                            if (command.Length >= 3 && command[2] != "") reason = command[2]; // reasons have to be one-line for now...

                                            JSONNode ban = JSON.Parse($"{{playfabid:\"{banP.Client.PlayFabID}\",name:\"{banP.Name}\",source:\"{player.Name}\",reason:\"{reason}\"}}");
                                            _bannedPlayers.Add(ban);
                                            // dump --- todo - better dump
                                            JSONNode.SaveToFile(_bannedPlayers, _baseloc + "banned-players.json");
                                            if (reason == "")
                                                reason = "No reason provided.";
                                            HandleTeammateLeavingMatch(banP);
                                            banP.Client?.NetAddress?.Disconnect($"\nYou've been banned from this server.\nReason: {reason}");
                                            responseMsg = $"Banned {banP}.";
                                        }
                                    }
                                    else responseMsg = "Command Error! /ban [] <-- Here | No Player specified!";
                                }
                                else responseMsg = $"You must have Developer privileges to utilize that command.";
                                break;

                            case "/banip":
                            case "/ipban":
                                // todo - improve & bans that expire
                                if (player.Client.isDev)
                                {
                                    if (command.Length >= 2 && command[1] != "")
                                    {
                                        if (TryPlayerFromString(command[1], out Player banP))
                                        {
                                            string ip = banP.Client?.NetAddress?.RemoteEndPoint.Address.ToString();
                                            string reason = "";
                                            if (command.Length >= 3 && command[2] != "") reason = command[2];

                                            JSONNode ban = JSON.Parse($"{{ip:\"{ip}\",playfabid:\"{banP.Client.PlayFabID}\",name:\"{banP.Name}\",source:\"{player.Name}\",reason:\"{reason}\"}}");
                                            _bannedIPs.Add(ban);
                                            // dump --- todo - better dump
                                            JSONNode.SaveToFile(_bannedIPs, _baseloc + "banned-ips.json");
                                            if (reason == "")
                                                reason = "No reason provided.";
                                            HandleTeammateLeavingMatch(banP);
                                            banP.Client?.NetAddress?.Disconnect($"\nYou've been banned from this server.\nReason: {reason}");
                                            responseMsg = $"IP Banned {banP} ({banP.Client?.GetIP()}).";
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
                                            SendSpawnedLoot(_level.NewLootWeapon(gun.JSONIndex, spawnRarity, (byte)gun.ClipSize, player.Position), ref player.Position);
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
                                            SendSpawnedLoot(_level.NewLootWeapon(nade.JSONIndex, 0, spawnAmount, player.Position), ref player.Position);
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
                                                if (_safeMode)
                                                    newPos = _level.FindWalkableGridLocation((int)newPos.x, (int)newPos.y);
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
                                            if (_safeMode)
                                                newPos = _level.FindWalkableGridLocation((int)newPos.x, (int)newPos.y);
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
                                    int _initSize = GetNumberOfValidPlayerEntries() * 16;
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
                                        if (_players[i] != null)
                                            SendForcePosition(_players[i], new Vector2(500, 500), true);
                                    responseMsg = "mmm uh huh! yeah? you like that? yeah you like that buddy? yeah uh huh!";
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
                                        if ((aviaryShowID < 0) || (aviaryShowID > 2))
                                            aviaryShowID = 0;
                                        SendAviaryShow((byte)aviaryShowID);
                                        responseMsg = $"Played Aviary Show #{aviaryShowID + 1} [actualID: {aviaryShowID}]";
                                    }
                                    else
                                        responseMsg = $"Invalid value \"{command[1]}\"";
                                }
                                else
                                    responseMsg = "Insufficient # of arguments provided.\"/startshow\" takes at least 1!\nUsage: /startshow [aviaryShowID]";
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
                                            if (i + j >= 256)
                                                continue;
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
                                            DamagePlayer(killPlayerLOL, killPlayerLOL.HP, -3, -1);
                                            responseMsg = $"<insert unfunny joke here> killed {killPlayerLOL}.";
                                        }
                                        else responseMsg = $"Can't kill {killPlayerLOL}, they're already dead!";
                                    }
                                    else responseMsg = $"Could not locate player \"{command[1]}\"";
                                }
                                else if (player.isAlive)
                                {
                                    player.isGodmode = false;
                                    DamagePlayer(player, player.HP, -3, -1);
                                    responseMsg = $"<insert unfunny joke here> killed {player}.";
                                }
                                else responseMsg = $"Can't kill {player}, they're already dead LOL!";
                                if (_safeMode) // todo - true infinite match
                                    CheckForWinnerWinnerChickenDinner();
                                break;

                            case "/godmode":
                            case "/god":
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
                                if (player.Client.isDev || player.Client.isMod)
                                {
                                    if (command.Length >= 2 && command[1] != "")
                                    {
                                        if (TryPlayerFromString(command[1], out Player ghoster))
                                        {
                                            EnableGhostModeForPlayer(ghoster);
                                            responseMsg = $"Ghost Mode enabled for {ghoster}!";
                                        }
                                        else
                                            responseMsg = $"Could not locate player \"{command[1]}\".";
                                    }
                                    else
                                    {
                                        responseMsg = "";
                                        EnableGhostModeForPlayer(player);
                                    }
                                }
                                else
                                    responseMsg = "You do not have the permissions required to use /ghost...";
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

                            case "/togglewins":
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
                        
                        // scuffed ending -- yeah, this whole system stinks!!!
                        if (responseMsg != "")
                            SendChatMessageWarning(player, responseMsg);
                        return;
                    }

                    // dealing wtih actual chat messages -- that command system hurts!
                    if (wasToTeam)
                    {
                        foreach (Player teammate in player.Teammates)
                            if (teammate.Client?.NetAddress != null)
                                SendChatMessageTeammate(player.ID, text, teammate.Client.NetAddress);
                        // sender has to get sent their message too haha
                        SendChatMessageTeammate(player.ID, text, player.Client.NetAddress);
                    }
                    else if (player.isAlive)
                        SendChatMessageToAll(player.ID, text);
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
            if (!IsServerRunning() || (player.Client?.NetAddress == null)) return;
            NetOutgoingMessage msg = server.CreateMessage(4 + text.Length);
            msg.Write((byte)94);    // 1 Byte   | MsgID (94)
            msg.Write(player.ID);   // 2 Short  | SendPlayerID (yes, still required-- also playerID can be any valid playerId in the match)
            msg.Write(text);        // V String | MessageContents
            server.SendMessage(msg, player.Client.NetAddress, NetDeliveryMethod.ReliableUnordered);
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
        private void HandlePlayerLanded(NetIncomingMessage pmsg) // OK... enough
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandlePlayerLanded", out Player player))
            {
                try
                {
                    bool wasLandingValid = pmsg.ReadBoolean();
                    float xDir = pmsg.ReadFloat();
                    float yDir = pmsg.ReadFloat();
                    //Logger.DebugServer($"[PlayerLanded] Was landing safe? {wasLandingValid}; X:Y--[{xDir}, {yDir}]");
                    
                    if (!wasLandingValid)
                    {
                        int x = (int)player.Position.x, y = (int)player.Position.y;
                        Vector2 moveSpot = _level.FindWalkableGridLocation(x, y);
                        SendForcePosition(player, moveSpot);
                        if (_level.QuickIsValidPlayerLoc(x, y))
                        {
                            string message = $"{player} says spot was invalid.\nServer believes {(x, y)} & {moveSpot} to be valid.";
                            Logger.Warn(message);
                            GoWarnModsAndAdmins(message);
                        }
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
                msg.Write(hitIDs[i]);       // 2 Short | playerN.ID
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
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(9);
            msg.Write((byte)43);    // 1 Byte   | MsgID (43)
            msg.Write(countdown);   // 8 Double | LobbyCountdownSeconds
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        // Msg44 | "Spectator Info" --- Received whenever a dead, spectator client wishes to inform the server who they think they're spectating.
        private void HandleSpectatorRequest(NetIncomingMessage pMsg)
        {
            if (!IsServerRunning())
                return;

            if (VerifyPlayer(pMsg.SenderConnection, "HandleSpectatorRequest", out Player player))
            {
                // note - clients basically have the authority over who they're spectating
                // side-note - there may be a specific packet to override this that just hasn't been found/ used correctly (similar to how previously bananas would not disappear)
                // side-side-note - clients NEVER send this packet if they are alive, only when "dead". This includes ghosts!!
                try
                {
                    if (player.isAlive && !player.isGhosted)
                    {
                        Logger.DebugServer($"[HandleSpectatorRequest] {player} is alive and un-ghosted, yet they sent Msg44?");
                        return;
                    }

                    // read packet data
                    float x = pMsg.ReadFloat();
                    float y = pMsg.ReadFloat();
                    short id = pMsg.ReadInt16();

                    if ((id == -1) || player.isGhosted) // recently-died players & ghosted players send an ID of -1
                        return;

                    player.Position = new Vector2(x, y);
                    //if (player.isGhosted) // seems like ghosters can't click players like in modern versions + they send an id of -1?
                       // return;

                    // any other playerID but -1 is "valid" potentially
                    if (TryPlayerFromID(id, out Player newObservee))
                    {
                        if (newObservee.isAlive)
                        {
                            if (player.WhoImSpectating == newObservee.ID)
                                return;

                            // caller watching someone else; remove caller from previous player's list
                            if (player.WhoImSpectating != -1)
                                RemoveMySpectate(player, player.WhoImSpectating);

                            player.WhoImSpectating = newObservee.ID;
                            if (!newObservee.MySpectatorsIDs.Contains(player.ID))
                            {
                                newObservee.MySpectatorsIDs.Add(player.ID);
                                SendUpdatedSpectatorCount(newObservee.ID, (byte)newObservee.MySpectatorsIDs.Count);
                            }
                            else // should be impossible, but anything is possible with enough spaghetti
                                Logger.Warn($"[HandleSpectatorRequest] [Warn] {player} already in {newObservee}'s spectator list for some reason... whoopsies!");
                        }
                        else
                            Logger.Warn($"[HandleSpectatorRequest] [Warn] {player} trying to spectate {newObservee}; who's dead!!");
                    }
                    else
                        Logger.Warn($"[HandleSpectatorRequest] [Warn] {player} requested pID \"{id}\" could not be found.");
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
            msg.Write((byte)45);                // Byte  | MsgID (45)
            msg.Write((byte)1);                 // Byte  | # of Players [Always 1 here]
            msg.Write(player.ID);               // Short | PlayerID
            msg.Write(player.HP);               // Byte  | CurrentHP [game converts to Float]
            msg.Write(player.ArmorTier);        // Byte  | ArmorTier
            msg.Write(player.ArmorTapes);       // Byte  | ArmorTicks / ArmorTapes
            msg.Write((byte)player.WalkMode);   // Byte  | WalkMode
            msg.Write(player.HealthJuice);      // Byte  | # of Juice
            msg.Write(player.SuperTape);        // Byte  | # of Tape
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        private void SendPlayerDataUpdates(List<Player> pPlayers)
        {
            if (!IsServerRunning() || (pPlayers.Count == 0)) return;

            int playerCount = pPlayers.Count;
            NetOutgoingMessage msg = server.CreateMessage(2 + (8 * playerCount));
            msg.Write((byte)45);
            msg.Write((byte)playerCount);
            foreach(Player player in pPlayers)
            {
                msg.Write(player.ID);
                msg.Write((byte)player.HP);
                msg.Write(player.ArmorTier);
                msg.Write(player.ArmorTapes);
                msg.Write((byte)player.WalkMode);
                msg.Write(player.HealthJuice);
                msg.Write(player.SuperTape);
            }
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

        // Msg51 | "Request to Eat Coconut" --> Msg52 | "Confirm Coconut Eaten"
        private void HandleCoconutEatRequest(NetIncomingMessage pMsg) // appears OK | v0.90.2 OK
        {
            // you can't eat coconuts in lobby in this version
            if (!_hasMatchStarted || !IsServerRunning())
                return; // even if you ignore this request, the client still thinks it ate the coconut

            if (VerifyPlayer(pMsg.SenderConnection, "HandleCoconutEatRequest", out Player player))
            {
                if (!player.IsPlayerReal())
                    return;
                try
                {
                    // read msg data...
                    ushort rCoconutIndex = pMsg.ReadUInt16();

                    // verify data...
                    if (!_coconutList.ContainsKey(rCoconutIndex))
                        return;

                    if (!player.Position.IsNear(_coconutList[rCoconutIndex].Position, 10f))
                        return;

                    // act on this information...
                    Logger.DebugServer($"before: {player.HP}; give: {_coconutHealAmountHP}");
                    if ((player.HP + _coconutHealAmountHP) > 100)
                        player.HP = 100;
                    else
                        player.HP += (byte)_coconutHealAmountHP;
                    Logger.DebugServer($"after: {player.HP}");

                    if (!_coconutList.Remove(rCoconutIndex))
                        Logger.Warn("HConutEatenR: coconut already removed??");

                    SendCoconutEaten(player.ID, rCoconutIndex);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleCoconutEatReq - Error] {pMsg.SenderConnection} caused a NetException!\n{netEx}");
                    pMsg.SenderConnection.Disconnect("There was an error reading your packet data. [CoconutEatReq]");
                }
            }
        }

        // Msg52 | "Coconut Eaten"
        private void SendCoconutEaten(short playerID, ushort conutID) // v0.90.2 OK
        {
            // because server can end at any point and we MAYBE get here
            if (!IsServerRunning())
                return;
            // Net Message
            NetOutgoingMessage msg = server.CreateMessage(5);
            msg.Write((byte)52);    // 1 Byte   | MsgID (52)
            msg.Write(playerID);    // 2 Short  | PlayerID
            msg.Write(conutID);     // 2 Ushort | CoconutID
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
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
                                SendSpawnedLoot(spawnLoot, ref spawnLoot.Position);
                                SendGrassLootFoundSound(player.Client.NetAddress);
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
        private void SendHamsterballExit(short playerID, short hamsterballID, Vector2 exitPosition)
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
                if (!_hamsterballs.ContainsKey(targetBallID))
                    msg.Write((byte)0);
                else
                    msg.Write(_hamsterballs[targetBallID].HP);
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        // Msg20 | "LootItem Spawned" --- Sent whenver the server spawns LootItems
        private void SendSpawnedLoot(LootItem lootItem, ref Vector2 startPos)
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
            // Positions | Pair1: where it'll end up | Pair2: where it was ""supposed"" to spawn
            msg.Write(lootItem.Position.x); // Float  | RealPosition.x
            msg.Write(lootItem.Position.y); // Float  | RealPosition.y
            msg.Write(startPos.x);          // Float  | InitalPosition.x
            msg.Write(startPos.y);          // Float  | InitalPosition.y
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
            // | 4 Float  | Position1.x <--- Where the item ends up
            // | 4 Float  | Position1.y
            // | 4 Float  | Position2.x <--- Where the item appears from (plays bounce animation towards END)
            // | 4 Float  | Position2.y
            // | 1 Byte   | DataValue2 [Weapon: ammoCount --- Armor: armorTier -- Ammo: ammoType --- All Other Types: <<nothing>> / 0]
            // | 2 String | InfoString? [Weapons: Rarity.ToString() --- All Other Types: do not include]
            // ----------------------------------------
        }

        // Msg 21 "Loot Request | Match" --> Msg22 "Loot Item Obtained"
        private void HandleLootRequestMatch(NetIncomingMessage pMsg)
        {
            if (VerifyPlayer(pMsg.SenderConnection, "HandleLootItemsMatch", out Player player))
            {
                // Is the Player dead? Has the Player landed yet?
                if (!player.IsPlayerReal()) return; // || player.NextLootTime > DateTime.UtcNow
                try
                {
                    // Read Data. Verify if it is real.
                    int reqLootID = pMsg.ReadInt32();
                    byte slotID = pMsg.ReadByte();

                    // Verify is real.
                    //Logger.DebugServer($"[BetterHandleLootItem] Sent Slot: {slotID}");
                    if (slotID < 0 || slotID > 3) return; // NOTE: Seems like the game will always try to send the correct slot to change.
                    if (!_level.LootItems.ContainsKey(reqLootID)) // Add infraction thing so like if they do to much kicky?
                    {
                        Logger.Failure($"[Handle MatchLootRequest] Player @ {pMsg.SenderConnection} requested a loot item that wasn't in the list.");
                        return;
                        //pMsg.SenderConnection.Disconnect($"Requested LootID \"{reqLootID}\" not found.");
                    }

                    // Check if player is close enough.
                    LootItem item = _level.LootItems[reqLootID];  // Item is set here!
                    if (!player.Position.IsNear(item.Position, 10.5f)) // Still testing thresholds...
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
                            if (player.HealthJuice == 200)
                                return;
                            if ((player.HealthJuice + item.GiveAmount) > 200)
                            {
                                item.GiveAmount -= (byte)(200 - player.HealthJuice);
                                player.HealthJuice = 200;
                                SendSpawnedLoot(_level.NewLootJuice(item.GiveAmount, item.Position), ref player.Position);
                            }
                            else
                                player.HealthJuice += item.GiveAmount;
                            break;

                        // Tape
                        case LootType.Tape:
                            if (player.SuperTape == 5)
                                return;             // If at max -> stop; otherwise...
                            if ((player.SuperTape + item.GiveAmount) > 5)
                            {
                                item.GiveAmount -= (byte)(5 - player.SuperTape);
                                player.SuperTape = 5;
                                SendSpawnedLoot(_level.NewLootTape(item.GiveAmount, item.Position), ref player.Position);
                            }
                            else
                                player.SuperTape += item.GiveAmount;
                            break;

                        // Armor
                        case LootType.Armor:
                            if (player.ArmorTier != 0) // Has armor
                            {
                                SendSpawnedLoot(_level.NewLootArmor(player.ArmorTier, player.ArmorTapes, item.Position), ref player.Position);
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
                            // first checks if the ammoTypeID is actually a valid index in our hard-coded maxAmmoArray. if so, proceeeds to figure whether the player has max ammo for that ammo type.
                            if ((ammoArrayIndex < 0) || (ammoArrayIndex > (_maxAmmo.Length - 1)))
                                return;
                            if (player.Ammo[ammoArrayIndex] == _maxAmmo[ammoArrayIndex])
                                return;

                            if ((player.Ammo[ammoArrayIndex] + item.GiveAmount) > _maxAmmo[ammoArrayIndex])
                            {
                                item.GiveAmount -= (byte)(_maxAmmo[ammoArrayIndex] - player.Ammo[ammoArrayIndex]);
                                player.Ammo[ammoArrayIndex] = _maxAmmo[ammoArrayIndex];
                                SendSpawnedLoot(_level.NewLootAmmo((byte)ammoArrayIndex, item.GiveAmount, item.Position), ref player.Position);
                            }
                            else
                                player.Ammo[ammoArrayIndex] += item.GiveAmount;
                            //Logger.Basic($"Player[{ammoArrayIndex}]: {player.Ammo[ammoArrayIndex]}");
                            break;

                        // Weapon -- May be messy!
                        case LootType.Weapon:
                            if (slotID == 3 && item.WeaponType == WeaponType.Throwable) // Throwables
                            {
                                // If Player doesn't have anything then...
                                if (player.LootItems[2].LootType == LootType.Collectable)
                                    player.LootItems[2] = item;
                                else // Has throwable already...
                                {
                                    if (player.LootItems[2].WeaponIndex == item.WeaponIndex) // add to count
                                    {
                                        int maxCount = _weapons[player.LootItems[2].WeaponIndex].MaxCarry;
                                        if (player.LootItems[2].GiveAmount == maxCount)
                                            return;

                                        if (player.LootItems[2].GiveAmount + item.GiveAmount > maxCount)
                                        {
                                            item.GiveAmount -= (byte)(maxCount - player.LootItems[2].GiveAmount);
                                            player.LootItems[2].GiveAmount = (byte)maxCount;
                                            SendSpawnedLoot(_level.NewLootWeapon(item.WeaponIndex, 0, item.GiveAmount, item.Position), ref player.Position);
                                        }
                                        else
                                            player.LootItems[2].GiveAmount += item.GiveAmount;
                                    }
                                    else
                                    {
                                        LootItem oldThrowable = player.LootItems[2];
                                        SendSpawnedLoot(_level.NewLootWeapon(oldThrowable.WeaponIndex, 0, oldThrowable.GiveAmount, item.Position), ref player.Position);
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
                                    SendSpawnedLoot(_level.NewLootWeapon(oldWeapon.WeaponIndex, oldWeapon.Rarity, oldWeapon.GiveAmount, item.Position), ref player.Position);
                                    player.LootItems[wepSlot] = item;
                                }
                                else
                                    player.LootItems[wepSlot] = item; // Item is NOT in this slot, so just replace it
                            }
                            break;
                    }

                    // OK! Now send the loot items and junk!!!
                    _level.RemoveLootItem(item);
                    SendPlayerDataChange(player);
                    SendPlayerLootedItem(player.ID, reqLootID, slotID);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleLootRequestMatch - Error] {pMsg.SenderConnection} caused a NetException!\n{netEx}");
                    pMsg.SenderConnection.Disconnect("There was an error reading your packet data. [LootRequest]");
                }
            }
        }

        // Msg 21 "Loot Request | Lobby" --> Msg22 "Loot Item Obtained"
        private void ServerHandleLobbyLootRequest(NetIncomingMessage pMsg)
        {
            if (VerifyPlayer(pMsg.SenderConnection, "HandleLootItemsLobby", out Player player))
            {
                try
                {
                    // todo - more checks and actually work
                    // so as it turns out, spawning in lobby weapons is done in a completely different way than how this program handles loot items...
                    // ...who would've guessed that one (haha)! should've kept this in mind when doing the LootItem overhaul!
                    int rLootID = pMsg.ReadInt32();
                    byte rSlot = pMsg.ReadByte();
                    //Logger.DebugServer($"LootID: {rLootID}; Slot: {rSlot}");
                    SendPlayerLootedItem(player.ID, rLootID, rSlot, 4);
                    // here's the rundown: after spawning in all other loot items: check level's list of gallery-target-area objects
                    // for each gallery-target-area items that are actually lobby-weapons, add these lobby-weapons to a list of positions
                    // now, foreach weapon that is a gun, spawn that found weapon(gun) at position[i], then increment i
                    // so you'll eventually notice how that spawned weapon actually becomes a loot item with its own unique id! (how fun!)
                }
                catch (NetException netEx) // todo - standard message
                {
                    Logger.Failure($"[HandleLootItemsLobby - Error] {pMsg.SenderConnection} caused a NetException!\n{netEx}");
                    pMsg.SenderConnection.Disconnect("There was an error reading your packet data. [HandleLootItemsLobby]");
                }
            }
        }

        /// <summary>
        ///  Sends "Player LootedItem" (v0.90.2 = ID 22) packet to all NetConnections connected to the current Match's server.
        /// </summary>
        /// <param name="pPlayerID"> ID of the player that obtained the loot item.</param>
        /// <param name="pLootID"> ID of the LootItem the player obtained.</param>
        /// <param name="pSlotID"> Which item-slot this loot item will be equipped in.</param>
        /// <param name="pForcedRarity"> (optional; usually for the lobby) The forced-rarity of this loot item. (Ex: always give Legend weapons).</param>
        private void SendPlayerLootedItem(short pPlayerID, int pLootID, byte pSlotID, byte pForcedRarity = 0)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(9);
            msg.Write((byte)22);        // 1 Byte  |  MsgID
            msg.Write(pPlayerID);       // 2 Short |  PlayerID
            msg.Write(pLootID);         // 4 Int   |  LootID
            msg.Write(pSlotID);         // 1 Byte  |  SlotID
            msg.Write(pForcedRarity);   // 1 Byte  |  ForcedRarity
            server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }

        // Msg42 | "Send End Rewards" --> None, but to display... TEST::: Solos: Msg9 (game ended)
        private void SendEndScreenItemDrops(Player player)
        {
            NetOutgoingMessage msg = server.CreateMessage(44); // ??
            msg.Write((byte)42);    // 1 Byte | MsgID (42)

            // EXP Reward Display -- can do some silly things with this
            msg.Write((short)0);    // 2 Short | Time Alive XP - Base
            msg.Write((short)0);    // 2 Short | Time Alive XP - "Total" (display = "Total" + "Base")
            msg.Write((short)0);    // 2 Short | Kills XP (total, not each)
            msg.Write((short)1);    // 2 Short | XP for win
            msg.Write((short)0);    // 2 Short | Top 5 XP << note - does NOT display if WinXP is >0
            msg.Write((short)5);    // 2 Short | Total XP without bonus
            msg.Write((short)7);    // 2 Short | Bonus XP
            msg.Write((short)7);    // 2 Short | Total XP --- seems game only displays this, but not the base-total & bonus

            // Player Level | note - v0.92.1 fixes glitched gems over 1k
            msg.Write((short)999);  // 2 Short | New Level
            msg.Write((short)0);    // 2 Short | Remaining XP until next level
            msg.Write((short)999);  // 2 Short | Old Level
            msg.Write((short)0);    // 2 Short | Old-Remaining XP until next level

            // Placement
            msg.Write(player.Placement); // placed at
            msg.Write(player.Placement); // placedAt Teams

            // Delivery Mole
            msg.Write("");          // V String | Loot Item ID (ex: MeleeKnife; see "SEEN_ITEMS5" in Windows Registry
            msg.Write((byte)4);     // 1 Byte | Loot Item Rarity
            msg.Write(0.005f);      // Float | Delivery Mole RNG Drop chance-base (display combines with increase) [% = desire/ 100; ex: 5% == 0.05f)
            msg.Write(0.00001f);    // Float | Delivery Mole RNG Drop chance-increase (displayt

            // Animal DNA
            msg.Write((short)99);   // 2 Short  | # of Animal DNA to reward
            msg.Write("DNA_Fox");   // V String | DNA ID [appears to follow pattern: "DNA_animal" where "animal"; ex: DNA_Fox (see: _languages.txt)]
            msg.Write(true);        // 1 Bool   | Was DNA obtained from Super DNA Magnet?

            // Random Serum & Magnets
            msg.Write((short)5);    // 2 Short | # of Super Serum
            msg.Write((byte)254);   // 1 Byte  | # of Super DNA Magnets

            // ??? --- milestones? 
            msg.Write((byte)0);

            // ??? --- milestones again??
            msg.Write((byte)0);

            // Daily Rewards
            msg.Write((byte)0); // 1 Byte | # of Super Serum to reward for completeing a Daily Challenge
            msg.Write((byte)0); // 1 Byte | # of Super Magnets to reward for completely a Dailly Challenge

            // ??? --- not really sure
            msg.Write((byte)0);

            // send
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        // Msg55 >> Msg56 | "Hamsterball Enter Request" -- Called when trying to enter a hamsterball
        private void HandleHamsterballEnter(NetIncomingMessage pmsg) // OK... enough
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleHamsterballEnter", out Player player))
            {
                try
                {
                    // simply assuming player had their request handled late or something
                    if (!player.IsPlayerReal())
                        return;

                    int hampterID = pmsg.ReadInt16(); // only read for this v0.90.2

                    // beginning of checks
                    if (player.HamsterballID != -1)
                    {
                        Logger.Failure($"[HandleHamsterballEnter] Player @ {pmsg.SenderConnection} was already in a hamsterball server-side. Could be ping related.");
                        pmsg.SenderConnection.Disconnect("There was an error while handling your packet data (Hamster-1).");
                        return;
                    }
                    if (!_hamsterballs.ContainsKey(hampterID))
                    {
                        Logger.Failure($"[HandleHamsterballEnter] Player @ {pmsg.SenderConnection} sent invalid HamsterballID \"{hampterID}\". Likely ping related.");
                        pmsg.SenderConnection.Disconnect($"There was an error while handling your packet data (Hamster-2).");
                        return;
                    }
                    if (_hamsterballs[hampterID].CurrentOwner != null)
                    {
                        Logger.Failure($"[HandleHamsterballEnter] Player @ {pmsg.SenderConnection} tried entering a Hamsterball that's owned by someone!");
                        pmsg.SenderConnection.Disconnect($"There was an error while handling your packet data (Hamster-3).");
                        return;
                    }
                    if (!_hamsterballs[hampterID].Position.IsNear(player.Position, 14f))
                        return;

                    // everything else ok
                    CheckMovementConflicts(player);
                    player.SetHamsterball((short)hampterID);
                    _hamsterballs[hampterID].CurrentOwner = player;
                    SendHamsterballEntered(player.ID, (short)hampterID, _hamsterballs[hampterID].Position);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleHamsterballEnter] Player @ NetConnection \"{pmsg.SenderConnection}\" gave NetError!\n{netEx}");
                    pmsg.SenderConnection.Disconnect("There was an error procssing your request. Message: Read past buffer size...");
                }
            }
        }

        /// <summary>
        ///  Sends "Player Entered Hamsterball" (v0.90.2 = ID 56) packet to all NetConnections connected to the current Match's server.
        /// </summary>
        /// <param name="pPlayerID"> ID of the Player who entered a Hamsterball.</param>
        /// <param name="pHamsterballID"> ID of the Hamsterball that a Player has entered.</param>
        /// <param name="pPosition"> Position of the Hamsterball in the overworld.</param>
        private void SendHamsterballEntered(short pPlayerID, short pHamsterballID, Vector2 pPosition)
        {
            NetOutgoingMessage msg = server.CreateMessage(13);
            msg.Write((byte)56);        // 1 Byte  | MsgID (56)
            msg.Write(pPlayerID);       // 2 Short | PlayerID
            msg.Write(pHamsterballID);  // 2 Short | HamsterballID
            msg.Write(pPosition.x);     // 4 Float | EnterPositionX
            msg.Write(pPosition.y);     // 4 Float | EnterPositionY
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        // Msg57 >> Msg58 | "Hamsterball Exit Request" -- Called when trying to exit a ball; doesn't seem to appear if player's ball gets destroy
        private void HandleHamsterballExit(NetIncomingMessage pmsg)
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HamsterballExitRequest", out Player player))
            {
                if (!player.IsPlayerReal())
                    return;

                if (player.HamsterballID == -1)
                {
                    Logger.Failure($"[HamsterballExitRequest] Player @ {pmsg.SenderConnection} wasn't in a Hamsterball? Likely ping related");
                    return;
                }
                if (!_hamsterballs.ContainsKey(player.HamsterballID))
                {
                    Logger.Failure($"[HamsterballExitRequest] Player @ {pmsg.SenderConnection} had HamsterballID \"{player.HamsterballID}\", which wasn't found in the Hamsterball list.");
                    pmsg.SenderConnection.Disconnect($"Could not find Hamsterball \"{player.HamsterballID}\".");
                    return;
                }

                // server junk + send message
                Hamsterball hamsterball = _hamsterballs[player.HamsterballID];
                hamsterball.Position = player.Position;
                hamsterball.CurrentOwner = null;
                player.ResetHamsterball();
                SendHamsterballExit(player.ID, hamsterball.ID, player.Position);
            }
        }

        // Msg60 >> Msg61 | "Hamsterball Attack" -- Called when a hamsterball rolls into someone
        private void HandleHamsterballAttack(NetIncomingMessage pmsg) // todo - cleanup
        {
            if (VerifyPlayer(pmsg.SenderConnection, "HandleHamsterballAttack", out Player player))
            {
                try
                {
                    if (!player.IsPlayerReal() || (player.HamsterballID == -1))
                        return;

                    // message data reads v0.90.2
                    short targetID = pmsg.ReadInt16();
                    float speed = pmsg.ReadFloat();

                    // Find Target. Figure out if they're alive/godded or not.
                    if (!TryPlayerFromID(targetID, out Player target))
                    {
                        Logger.Failure($"[HandleHamsterballAttack] Player @ {pmsg.SenderConnection} gave an invalid PlayerID");
                        pmsg.SenderConnection.Disconnect("There was an error while processing your request. \"Requested TargetID not found.\"");
                        return;
                    }

                    if (!target.isAlive)
                        return;
                    
                    if (target.isGodmode)
                    {
                        SendHamsterballHurtPlayer(player.ID, target.ID, !target.isAlive, player.HamsterballID, -1);
                        return;
                    }
                    // Damage section:
                    float speedDifference = speed - player.HamsterballVelocity.magnitude;
                    if (speedDifference > 5)
                    {
                        Logger.Warn($"[HandleHamsterballAttack] Player @ {pmsg.SenderConnection} speed difference was too high. Difference: {speedDifference}");
                        return;
                    }

                    if (target.HamsterballID == -1)
                    {
                        DamagePlayer(target, (int)(player.HamsterballVelocity.magnitude * 2), player.ID, -2);
                        SendHamsterballHurtPlayer(player.ID, target.ID, !target.isAlive, player.HamsterballID, -1);
                    }
                    else if (target.HamsterballVelocity.sqrMagnitude < player.HamsterballVelocity.sqrMagnitude)
                    {
                        if (!_hamsterballs.ContainsKey(target.HamsterballID))
                            return;

                        if ((_hamsterballs[target.HamsterballID].HP - 1) < 0)
                            _hamsterballs[target.HamsterballID].HP = 0;
                        else
                            _hamsterballs[target.HamsterballID].HP -= 1;

                        SendHamsterballHurtPlayer(player.ID, target.ID, false, player.HamsterballID, target.HamsterballID);
                        if (_hamsterballs[target.HamsterballID].HP == 0)
                            DestroyHamsterball(target.HamsterballID);
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
                if (player.HamsterballID == -1)
                {
                    Logger.Failure($"[HandleHamsterballBounce] Player @ {pmsg.SenderConnection} VehicleID is -1, but they tried bouncing. Likely ping related.");
                    return;
                }
                player.HamsterballVelocity = new Vector2(0, 0);
                NetOutgoingMessage bounce = server.CreateMessage(5);
                bounce.Write((byte)63);         // 1 Byte  | MsgID (63)
                bounce.Write(player.ID);        // 2 Short | PlayerID
                bounce.Write(player.HamsterballID); // 2 Short | HamsterballID
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
                    if ((weaponID < 0) || (weaponID >= _weapons.Length))
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
        private void HandleEmoteRequest(NetIncomingMessage pMsg)
        {
            if (VerifyPlayer(pMsg.SenderConnection, "HandleEmoteRequest", out Player player))
            {
                try
                {
                    // player is alive && not in hamsterball-- can't emote when in ball!
                    if (!player.IsPlayerReal() || (player.HamsterballID != -1))
                        return;

                    // read sent data --> throws NetException in the event any of these fail
                    short rEmoteID = pMsg.ReadInt16();
                    float rEmotePositionX = pMsg.ReadFloat();
                    float rEmotePositionY = pMsg.ReadFloat();
                    float rEmoteDuration = pMsg.ReadFloat(); // duration is in seconds

                    // verify sent data
                    Vector2 emotingPosition = new Vector2(rEmotePositionX, rEmotePositionY);
                    if (!player.Position.IsNear(emotingPosition, 2))
                    {
                        Logger.Warn($"[HandleEmoteRequest - Warn] {player}'s requested postion \"{emotingPosition}\" too far from actual positon of {player.Position}!");
                        return;
                    }
                    if ((rEmoteID != -1) && (rEmoteID >= 20)) // only ids [-1...19] are valid in v0.90.2 (20 emotes + `-1` to cancel)
                    {
                        Logger.Failure($"[HandleEmoteRequest - BAD] {player} sent invalid EmoteID \"{rEmoteID}\"! Known EmoteIDs are -1+!!!");
                        pMsg.SenderConnection.Disconnect($"\nThere was an error with your packet data.\n{rEmoteID} isn't a valid emoteID!");
                        return;
                    }

                    // send messages/ set server-side variables
                    if (rEmoteID == -1)
                    {
                        if (!player.isEmoting)
                            Logger.Warn($"[HandleEmoteRequest - Warn] {player} requested EmoteID -1 but they ain't emoting!");
                        HandleEmoteCancel(player); // sends 67 with [emoteID -1] then calls [player.EmoteEnded()] (
                    }
                    else
                    {
                        CancelAllNonEmoteActions(player);
                        SendEmotePerformed(player.ID, rEmoteID);
                        player.EmoteStarted(rEmoteID, emotingPosition, rEmoteDuration);
                    }
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleEmoteRequest - Error] {pMsg.SenderConnection} caused a NetException!\n{netEx}");
                    pMsg.SenderConnection.Disconnect("There was an error reading your packet data. [EmoteRequest]");
                }
            }
        }

        // Msg67 | "Player Performed Emote" --- Pretty self-explanatory; Should note EmoteID "-1" is reserved for CANCELING emotes!
        public void SendEmotePerformed(short pPlayerID, short pEmoteID)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(5);
            msg.Write((byte)67);    // 1 Byte  | MsgID (67)
            msg.Write(pPlayerID);   // 2 Short | EmotingPlayerID
            msg.Write(pEmoteID);    // 2 Short | EmoteID
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered); // TIL: ReliableOrdered actually sends it in order. Sending it order make VERY LAG is emote-glitching!!
        }

        /// <summary>
        /// Cancels the provided Player's Emote by calling <see cref="Player.EmoteEnded"/> and <see cref="SendEmotePerformed(short, short)"/>.
        /// </summary>
        /// <param name="player"></param>
        private void HandleEmoteCancel(Player player)
        {
            player.EmoteEnded();
            SendEmotePerformed(player.ID, -1);
        }

        /// <summary>
        ///  Sends a spawn molecrate mole packet to all connected clients.
        /// </summary>
        /// <param name="pCrateID"> ID of the newly spawned crate (corresponds to the MoleID).</param>
        /// <param name="pCratePosition"> Position to spawn this crate.</param>
        private void SendMolecrateCrateSpawned(short pCrateID, Vector2 pCratePosition)
        {
            if (!IsServerRunning())
                return;

            NetOutgoingMessage msg = server.CreateMessage(11);
            msg.Write((byte)69);            // 1 Byte  | MsgID (69)
            msg.Write(pCrateID);            // 2 Short | CrateID
            msg.Write(pCratePosition.x);    // 4 Float | Position.X
            msg.Write(pCratePosition.y);    // 4 Float | Position.Y
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        // Msg70 | "Molecrate Open Request" --> Msg71 | "Confrim Molecrate Open" -- Received whenever a client attempts to open a Molecrate.
        private void HandleMolecrateOpenRequest(NetIncomingMessage pMsg)
        {
            // todo - molecrate rewrite: uuuh everything
            if (VerifyPlayer(pMsg.SenderConnection, "MolecrateOpen", out Player player))
            {
                try
                {
                    // simply assuming player had their request handled late or something
                    if (!player.IsPlayerReal())
                        return;

                    // only read for this message type v0.90.2 lol
                    short crateID = pMsg.ReadInt16();

                    // start of checks
                    if (crateID < 0 || crateID >= _moleCrates.Length)
                    {
                        Logger.Failure($"[HandleMolecrateOpen - Error] {player} @ {pMsg.SenderConnection} requested an out-of-bounds MolecrateID!");
                        player.Client?.NetAddress?.Disconnect("There was an error processing your request.\nMessage: out-of-bounds molecrateID!");
                        return;
                    }

                    if (_moleCrates[crateID] == null || !_moleCrates[crateID].isCrateReal)
                    {
                        Logger.Failure($"[HandleMolecrateOpen - Error] {player} @ {pMsg.SenderConnection} requested a molecrate still moving. Desync?");
                        if (_safeMode)
                            player.Client?.NetAddress?.Disconnect("There was an error processing your request.\nMessage: that molecrate can't be opened.");
                        return;
                    }

                    if (_moleCrates[crateID].isOpened)
                        return;
                    // end of checks

                    if (player.Position.IsNear(_moleCrates[crateID].Position, 14.7f))
                    {
                        SendMolecrateOpened(crateID, _moleCrates[crateID].Position);
                        // todo - molecrate rewrite: spawn molecrate loot
                    }
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleMolecrateOpen - Error] {pMsg.SenderConnection} caused a NetException!\n{netEx}");
                    pMsg.SenderConnection.Disconnect("There was an error reading your packet data. [MolecrateOpen]");
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
        private void HandleDoodadDestroyed(NetIncomingMessage pMsg) // v0.90.2 OK
        {
            if (VerifyPlayer(pMsg.SenderConnection, "HandleDoodadDestroyedReq", out Player player))
            {
                try
                {
                    // simply assuming player had their request handled late or something
                    if (!player.IsPlayerReal())
                        return;

                    // net data
                    Vector2 rHitPosition = new Vector2(pMsg.ReadInt32(), pMsg.ReadInt32());
                    short rProjectileID = pMsg.ReadInt16();

                    // other verification
                    if (!player.IsValidProjectileID(rProjectileID))
                    {
                        Logger.Failure($"[HandleDoodadDestroyedReq] Player @ {pMsg.SenderConnection} sent invalid ProjectileID \"{rProjectileID}\"");
                        pMsg.SenderConnection.Disconnect($"There was an error processing your request. (doodadHit)");
                        return;
                    }

                    // is okii to try searching for spot then!
                    HandleDoodadDestructionFromPoint(rHitPosition); // reused by player sent attack message handle as well btw...
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleDoodadDestroyedReq - Error] {pMsg.SenderConnection} caused a NetException!\n{netEx}");
                    pMsg.SenderConnection.Disconnect("There was an error reading your packet data. [DoodadDestroy]");
                }
            }
        }

        // Msg73 | "Confirm Doodad Destroyed"
        private void SendDoodadDestroyed(Doodad pDoodad, List<Player> pHitPlayers)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(10 + (pDoodad.HittableSpots.Length * 5) + (pHitPlayers.Count * 2));
            msg.Write((byte)73);                                // 1 Byte  | MsgID (73)
            msg.Write((short)420);                              // 2 Short | ??? (game does NOTHING with this!!!)
            msg.Write((short)pDoodad.Position.x);               // 2 Short | Doodad position.x
            msg.Write((short)pDoodad.Position.y);               // 2 Short | Doodad position.y
            msg.Write((short)pDoodad.HittableSpots.Length);     // 2 Short | # of collision spots to change
            for (int i = 0; i < pDoodad.HittableSpots.Length; i++)
            {                                                   // =-----= | -------------------
                msg.Write((short)pDoodad.HittableSpots[i].x);   // 2 Short | CollisionSpotChange.x
                msg.Write((short)pDoodad.HittableSpots[i].y);   // 2 Short | CollisionSpotChange.y
                msg.Write((byte)CollisionType.None);            // 1 Byte  | CollisionSpot New CollisionType
            }                                                   // =-----= | ...free
            byte numOfPlayers = (byte)pHitPlayers.Count;        // =-----= | space...
            msg.Write(numOfPlayers);                            // 1 Byte  | # of hit players
            for (byte j = 0; j < numOfPlayers; j++)             // =-----= | for every hit player...
                msg.Write(pHitPlayers[j].ID);                   // 2 Short | PlayerID
            server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        /// <summary>
        /// Handles the searching and destruction of Doodads that may or may not be around the provided hit location.
        /// </summary>
        /// <param name="pHitLocation"> Location to search for any Doodads with hittable spots at the aforementioned hit locationh.</param>
        private void HandleDoodadDestructionFromPoint(Vector2 pHitLocation)
        {
            if (_level.TryDestroyingDoodad(pHitLocation, out Doodad[] foundDoodads, _hasMatchStarted))
            {
                List<Player> hitPlayers = new List<Player>(4);
                for (int i = 0; i < foundDoodads.Length; i++)
                {
                    // explodable doodads
                    if (_hasMatchStarted && (foundDoodads[i].Type.DestructibleDamageRadius > 0))
                    {
                        // players
                        for (int j = 0; j < _players.Length; j++)
                        {
                            if (_players[j] != null && _players[j].Position.IsNear(foundDoodads[i].Position, foundDoodads[i].Type.DestructibleDamageRadius))
                            {
                                // https://animalroyale.fandom.com/wiki/Version_1.4.1 << changes scaling
                                hitPlayers.Add(_players[j]);
                                DamagePlayer(_players[j], (int)(foundDoodads[i].Type.DestructibleDamagePeak / 2), 0, -3); // halfing it so it isn't completely unfair (temporary fix)
                            }
                        }

                        // hamsterballs
                        foreach (Hamsterball hamsterball in _hamsterballs.Values)
                        {
                            if (hamsterball.Position.IsNear(foundDoodads[i].Position, foundDoodads[i].Type.DestructibleDamageRadius))
                            {
                                SendAttackConfirmed(-1, -1, -1, 0, hamsterball.ID, 0);
                                DestroyHamsterball(hamsterball.ID);
                            }
                        }
                    }

                    // send data out...
                    SendDoodadDestroyed(foundDoodads[i], hitPlayers);
                    hitPlayers.Clear(); // reset for next doodad we'll check
                }
            }
            else
            {
                Logger.Warn($"Failed to loate a doodad @ {pHitLocation}...");
                GoWarnModsAndAdmins($"Failed to locate a doodad @ {pHitLocation} :[");
            }
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
                        Logger.Failure($"[HandleAttackWindupReq - Error] {player} @ {pMsg.SenderConnection} sent invalid slot id \"{slot}\"!");
                        pMsg.SenderConnection.Disconnect("There was an error reading your packet data. [AttackWindup]");
                    }
                    if (player.ActiveSlot != slot)
                    {
                        Logger.Failure($"[HandleAttackWindupReq - Error] {player}'s active slot ({player.ActiveSlot}) doesn't match sent slot \"{slot}\"!");
                        return; // assuming this is VERY a late packet
                        // todo - inc "infraction-count"
                    }
                    if (_hasMatchStarted && player.LootItems[slot].WeaponIndex != weaponID)
                    {
                        Logger.Failure($"[HandleAttackWindupReq - Error] {player} @ {pMsg.SenderConnection} sent WeaponID \"{weaponID}\" doesn't match item in slot {slot}!");
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
                        Logger.Failure($"[HandleAttackWindDownReq - Error] {player} @ {pMsg.SenderConnection} sent invalid slot id \"{slot}\"!");
                        pMsg.SenderConnection.Disconnect("There was an error reading your packet data. [AttackWindup]");
                    }
                    if (player.ActiveSlot != slot)
                    {
                        Logger.Failure($"[HandleAttackWindDownReq - Error] {player}'s active slot ({player.ActiveSlot}) doesn't match sent slot \"{slot}\"!");
                        return; // assuming this is VERY a late packet
                        // todo - inc "infraction-count"
                    }
                    if (_hasMatchStarted && player.LootItems[slot].WeaponIndex != weaponID)
                    {
                        Logger.Failure($"[HandleAttackWindDownReq - Error] {player} @ {pMsg.SenderConnection} sent WeaponID \"{weaponID}\" doesn't match item in slot {slot}!");
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
                    if (requestPostion.IsNear(player.Position, 2)){
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
                if (!player.IsPlayerReal() || player.SuperTape == 0 || player.ArmorTier == 0 || player.ArmorTapes == player.ArmorTier)
                    return;
                try
                {
                    float posX = pmsg.ReadFloat();
                    float posY = pmsg.ReadFloat();
                    Vector2 requestPosition = new Vector2(posX, posY);
                    if (requestPosition.IsNear(player.Position, 2))
                    {
                        SendForcePosition(player, requestPosition);
                        CheckMovementConflicts(player);
                        player.Position = requestPosition;
                        player.isTaping = true;
                        player.NextTapeTime = DateTime.UtcNow.AddSeconds(SARConstants.TapeRepairDurationSeconds);
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

        // Msg85 | "Mark Map Request" -- Sent whenever a player attempts to mark their minimap.
        private void HandleMapMarked(NetIncomingMessage pMsg) // Msg85 >>> Msg86
        {
            if (VerifyPlayer(pMsg.SenderConnection, "HandleMapMarked", out Player player))
            {
                try
                {
                    float rMarkerX = pMsg.ReadFloat();
                    float rMarkerY = pMsg.ReadFloat();
                    if (rMarkerX < 0 || rMarkerX > _level.LevelWidth)
                        return;
                    if (rMarkerY < 0 || rMarkerY > _level.LevelHeight)
                        return;
                    Vector2 markerPosition = new Vector2(rMarkerX, rMarkerY);

                    foreach (Player teammate in player.Teammates)
                        SendMapMarked(teammate, markerPosition, player.ID);
                    SendMapMarked(player, markerPosition, player.ID);
                }
                catch (NetException netEx)
                {
                    Logger.Failure($"[HandleMapMarked] Player @ {pMsg.SenderConnection} caused a NetException!\n{netEx}");
                    pMsg.SenderConnection.Disconnect("There was an error while reading your packet data! (H.MapMark)");
                }
            }
        }

        // Msg 86 | "Mark Map Confirm" -- Sent whenever a player successfully marks their maps. *only send to: [initiating player] & [their mates]
        private void SendMapMarked(Player player, Vector2 coords, short senderID)
        {
            if (!IsServerRunning() || (player.Client.NetAddress == null)) return;
            NetOutgoingMessage msg = server.CreateMessage(11);
            msg.Write((byte)86);    // 1 Byte  | MsgID (86)
            msg.Write(coords.x);    // 4 Float | MarkX
            msg.Write(coords.y);    // 4 Float | MarkY
            msg.Write(senderID);    // 2 Short | PlayerID [who sent this marker; only really utilized by teammates]
            server.SendMessage(msg, player.Client.NetAddress, NetDeliveryMethod.ReliableUnordered);
        }

        // Msg80 | "Teammate Pickup Request" --- Received when a teammate tries picking up a downed teammate
        private void HandleTeammatePickupRequest(NetIncomingMessage pMsg) // Msg80 >> Msg81
        {
            if (VerifyPlayer(pMsg.SenderConnection, "HandleTeammatePickupRequest", out Player player))
            {
                if (!player.IsPlayerReal())
                {
                    Logger.Failure($"[TeammatePickupRequest | Error: {player} is not real.");
                    return;
                }
                if (player.isReviving)
                {
                    Logger.Failure($"[TeammatePickupRequest | Error: {player} is already reviving a player?");
                    return;
                }
                CheckMovementConflicts(player);

                try
                {
                    // reading of messae data...
                    short teammateID = pMsg.ReadInt16();

                    // validating any sent information/ this action is OK
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
                    if (!player.Position.IsNear(teammate.Position, 7.4f))
                    {
                        Logger.Failure($"Teammate wasn't close enough to revive!");
                        return;
                    }

                    // everything is OK
                    teammate.SetMyResurrector(player.ID);
                    player.SetWhoImRessing(teammateID);
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
            player.KnockDown(_bleedoutRateSeconds);
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
                if (TryPlayerFromID(player.RevivingID, out Player downedPlayer)) downedPlayer.ResurrectGotCanceledByRessorector(_bleedoutRateSeconds);
                player.FinishedRessingPlayer();
            }
            else if (player.isDown)
            {
                SendPickupCanceled(player.SaviourID, player.ID);
                if (TryPlayerFromID(player.SaviourID, out Player thisSaviour)) thisSaviour.FinishedRessingPlayer();
                player.ResurrectGotCanceledByRessorector(_bleedoutRateSeconds);
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
                if (!player.IsPlayerReal() || player.HamsterballID == -1) return;
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


        // Msg95 | "Teammate Left" --- Sent whenever a teammate disconnects.
        private void SendTeammateLeftMatch(NetConnection netConnection, short pPlayerID)
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage msg = server.CreateMessage(3);
            msg.Write((byte)95);    // 1 Byte  | MsgID (95)
            msg.Write(pPlayerID);   // 2 Short | LeavingTeammateID
            server.SendMessage(msg, netConnection, NetDeliveryMethod.ReliableUnordered);
        }

        // Msg97 | "Dummy"
        private void SendDummyMessage() // Msg97
        {
            if (!IsServerRunning()) return;
            NetOutgoingMessage dummy = server.CreateMessage(1);
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

        // Msg106 | "Send /roll ChatMsg" --- Typically sent when a player uses `/roll`; however, in actuality, any command/ even normal chats can use this
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
            msg.Write((byte)GetNumberOfValidPlayerEntries());
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null) continue;
                msg.Write(_players[i].ID);  // Short | PlayerID
                msg.Write((ushort)(_players[i].LastPingTime * 1000f));
            }
            server.SendToAll(msg, NetDeliveryMethod.ReliableOrdered);
        }

        // Msg125 | "Player Weapons Removed" --- Sent whenever a player has their weapons removed lol; at least dispalys it as such...
        private void SendRemoveWeapons(short playerID)
        {
            if (!IsServerRunning()) return;
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
            HandleEmoteCancel(player);
            HandlePickupCanceled(player);
        }

        private void CancelAllNonEmoteActions(Player player)
        {
            SendPlayerEndDrink(player);
            SendPlayerEndTape(player);
            HandlePickupCanceled(player);
        }

        /// <summary>
        ///  Handles a disconnection request from a NetConnection leaving this current Match's server.
        /// </summary>
        /// <param name="pmsg"> NetMessage from the disconnecting NetConnection to handle.</param>
        private void HandleNetClientDisconnect(NetIncomingMessage pmsg)
        {
            if (TryIndexFromConnection(pmsg.SenderConnection, out int index))
            {
                // regardless of whether a Match is in progress this happens:
                HandleTeammateLeavingMatch(_players[index], false);
                RemoveMySpectate(_players[index], _players[index].WhoImSpectating);

                // ^^ lobby/ match-- otherwise, dip out early!
                if (_hasMatchStarted)
                    return; // perhaps go into a player's client and also NULL their NetConnection?

                SendPlayerDisconnected(_players[index].ID); // displays to EVERYONE that the person left
                _availableIDs.Insert(0, _players[index].ID);
                _players[index] = null;
                isSorted = false;
            }
        }


        private void RemoveMySpectate(Player pViewer, short pPlayerID)
        {
            // todo - utilize in HandleSpectatorRequest-- that whole thing is a mess
            if ((pViewer == null) || (pViewer.WhoImSpectating == -1))
                return;

            Player player;
            if (!TryPlayerFromID(pPlayerID, out player))
                return;

            if (!player.MySpectatorsIDs.Remove(pViewer.ID))
                Logger.Warn($"[RemoveMySpectat] [WARN] {pViewer} was not in {player}'s spectator list.");

            SendUpdatedSpectatorCount(player.ID, (byte)player.MySpectatorsIDs.Count);
        }

        /// <summary>
        ///  Handles a Player leaving thier teammates. Will send the NetMessage necessary to display this, and optionally whether to actually remove them.
        /// </summary>
        /// <param name="pPlayer"> Player who is disconnecting/ leaving their teammates.</param>
        /// <param name="pRemove"> (optional) Whether to keep this Player attached to their teammates or remove them.</param>
        private void HandleTeammateLeavingMatch(Player pPlayer, bool pRemove = true)
        {
            for (int i = pPlayer.Teammates.Count - 1; i >= 0; i--)
            {
                if (pPlayer.Teammates[i].Client?.NetAddress != null)
                    SendTeammateLeftMatch(pPlayer.Teammates[i].Client.NetAddress, pPlayer.ID);

                if (pRemove)
                {
                    pPlayer.Teammates[i].Teammates.Remove(pPlayer);
                    pPlayer.Teammates.Remove(pPlayer.Teammates[i]);
                }
            }
        }

        /// <summary>
        /// Attempts to load all level-data related files and such to fill the server's own level-data related variables.
        /// </summary>
        private void LoadSARLevel(uint lootSeed, uint coconutSeed, uint hamsterballSeed)
        {
            if (_hasLevelBeenLoaded)
                return;

            Logger.Basic("[LoadSARLevel] Attempting to load level data...");
            _level = new SARLevel(lootSeed, coconutSeed, hamsterballSeed);
            _campfires = _level.Campfires;
            _coconutList = _level.Coconuts;
            _hamsterballs = _level.Hamsterballs;
            _level.NullUnNeeds();
            _hasLevelBeenLoaded = true;
        }

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
        ///  Determines how many Players in this Match._players are non-null.
        /// </summary>
        /// <returns>An Int representing the number of non-null player entries.</returns>
        private int GetNumberOfValidPlayerEntries() // Reminder: any instance where the code below was used can be replaced with calls to this method!
        {
            int ret = 0;
            for (int i = 0; i < _players.Length; i++)
                if (_players[i] != null)
                    ret += 1;
            return ret;
        }

        /// <summary>
        ///  Gathers all readied, alive, and non-ghostmode Players that are in the match.
        /// </summary>
        /// <returns>A List of Players representing the number of alive, readied, non-ghostmode Players.</returns>
        private List<Player> GetAllReadiedAlivePlayers()
        {
            List<Player> foundPlayers = new List<Player>(_players.Length);
            for (int i = 0; i < _players.Length; i++)
                if ((_players[i] != null) && _players[i].hasReadied && _players[i].isAlive && !_players[i].isGhosted)
                    foundPlayers.Add(_players[i]);
            foundPlayers.TrimExcess();
            return foundPlayers;
        }

        /// <summary>
        ///  Determines the current number of alive teams that are left in the match.
        /// </summary>
        /// <returns>An Int representing the number of alive teams left in the match.</returns>
        private int GetNumberOfAliveTeams()
        {
            int ret = 0;
            List<Player> tmp_ignore = new List<Player>(_players.Length);
            for (int i = 0; i < _players.Length; i++)
            {
                // null, not alive/ landed/ readied/ ghosting, or already saw this player's team...
                if (_players[i] == null || !_players[i].IsPlayerReal() || tmp_ignore.Contains(_players[i]))
                    continue;

                // squads-like modes: ignore this Player's teammates
                if (_gamemode != SARConstants.GamemodeSolos)
                {
                    int cache = _players[i].Teammates.Count;
                    for (int j = 0; j < cache; j++)
                        tmp_ignore.Add(_players[i].Teammates[j]);
                }
                ret += 1;
            }

            // hoping this makes cleaning up faster
            tmp_ignore.Clear();
            return ret;
        }

        /// <summary>
        /// Attempts to locate a Player who has a name matching the provided searchName.
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
        /// Attempts to locate a Player who has an ID matching the provided searchID.
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
        /// Attempts to locate the index at which a Player with the given NetConnection is located.
        /// </summary>
        /// <returns>True if the NetConnection is found; False if otherwise.</returns>
        private bool TryIndexFromConnection(NetConnection netConnection, out int returnedIndex)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i]?.Client?.NetAddress == netConnection)
                {
                    returnedIndex = i;
                    return true;
                }
            }
            returnedIndex = -1;
            return false;
        }

        /// <summary>
        /// Attempts to locate a Player who has the given NetConnection associated with them.
        /// </summary>
        /// <returns>True if the NetConnection is found; False if otherwise.</returns>
        private bool TryPlayerFromConnection(NetConnection netConnection, out Player player)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i]?.Client?.NetAddress == netConnection)
                {
                    player = _players[i];
                    return true;
                }
            }
            player = null;
            return false;
        }

        /// <summary>
        /// Attempts to locate a Player using the provided string. Will check if the string matches a playerID or playerName.
        /// </summary>
        /// <param name="search">String used in this search.</param>
        /// <param name="player">Output Player object.</param>
        /// <returns>True if a Player if found; False if otherwise.</returns>
        public bool TryPlayerFromString(string search, out Player player)
        {
            if (TryPlayerFromName(search, out player))
                return true;
            if (int.TryParse(search, out int searchID))
                return TryPlayerFromID(searchID, out player);
            return false;
        }
        #endregion player list methods

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
        /// Removes the provided HamsterballID from _hamsterballs. If a Player is tied to it, their ID is reset to -1.
        /// </summary>
        /// <param name="ballIndex">Index of this Hamsterball in _hamsterballs.</param>
        private void DestroyHamsterball(int ballIndex)
        {
            if (!_hamsterballs.ContainsKey(ballIndex))
            {
                Logger.Failure($"[DestroyHamsterball] Called, but key \"{ballIndex}\" is not within hamsterball list.");
                return;
            }

            if (_hamsterballs[ballIndex].CurrentOwner != null)
            {
                SendHamsterballExit(_hamsterballs[ballIndex].CurrentOwner.ID, (short)ballIndex, _hamsterballs[ballIndex].CurrentOwner.Position);
                _hamsterballs[ballIndex].CurrentOwner.ResetHamsterball();
            }
            _hamsterballs.Remove(ballIndex);
        }

        #region general player methods
        /// <summary>
        ///  Returns a JSONNode from _playerData that represents a player with the provided PlayFabID
        /// </summary>
        /// <param name="pPlayFabID"> PlayFabID to search for.</param>
        /// <returns>A JSONNode representing the player; otherwise, Null.</returns>
        private JSONNode GetPlayerNodeFromPlayFab(string pPlayFabID)
        {
            int numOfEntries = _playerData.Count;
            for (int i = 0; i < numOfEntries; i++)
                if ((_playerData[i]["playfabid"] != null) && (_playerData[i]["playfabid"] == pPlayFabID))
                    return _playerData[i];
            return null;
        }

        /// <summary>
        ///  Tries adding the provided Player to a team.
        /// </summary>
        /// <param name="player"> Player to try appending to other teams.</param>
        private void FindTeamToAddPlayerTo(Player player)
        {
            if ((_gamemode == SARConstants.GamemodeSolos) || (player == null))
                return;

            // max-team-members limit | ignore this player; look at their members rather than the team as a whole
            int maxMembers = 3;
            if (_gamemode == SARConstants.GamemodeDuos)
                maxMembers = 1;

            // adding ThisPlayer to their party-member's team.
            int numOfPartyMembers = player.Client.PartyPlayFabIDs.Count;
            if (numOfPartyMembers > 0)
            {
                for (int i = 0; i < _players.Length; i++)
                {
                    // generally checking whether this player instance is valid to do our checks...
                    if ((_players[i] == null) || (_players[i].Client == null) || (_players[i] == player) || player.Teammates.Contains(_players[i]))
                        continue;

                    // whether Player-J's PlayFabID is in ThisPlayer's list of party-members PlayFabIDs.
                    if (!player.Client.PartyPlayFabIDs.Contains(_players[i].Client.PlayFabID))
                        continue;

                    // all other checks passed... so we can add these players together now!
                    int tmp_NumOfTeammates = _players[i].Teammates.Count;
                    for (int j = 0; j < tmp_NumOfTeammates; j++)
                    {
                        // for each teammate in the party that might've already joined...
                        player.Teammates.Add(_players[i].Teammates[j]);
                        _players[i].Teammates[j].Teammates.Add(player);
                    }
                    _players[i].Teammates.Add(player); // adding ThisPlayer to the party member we found...
                    player.Teammates.Add(_players[i]); // adding the party member we found to start this to ThisPlayer...
                    break;
                }

                // check whether we're at the maximum # of teammates now
                if (player.Teammates.Count == maxMembers)
                    return;
            }

            // more slots available... but does this player have fills on?
            if (player.Client.isFillsDisabled) // think that every party member has whatever their party leader has fills set to
                return;

            // try adding this player and their teammates to a party that has yet to reach max players AND has fills on
            for (int i = 0; i < _players.Length; i++)
            {
                // skip if: player null; player doesn't have a client; or if player has fills disabled
                if ((_players[i] == null) || (_players[i] == player) || (_players[i].Client == null) || (_players[i].Client.isFillsDisabled))
                    continue;

                int numOfTeammates = _players[i].Teammates.Count; // for later use
                // player-i already has maximum number of party memebrs OR they haven't gotten all their party members yet...
                if ((numOfTeammates == maxMembers) || (numOfTeammates < _players[i].Client.PartyPlayFabIDs.Count))
                    continue;

                // will combining these two teams overflow?
                if ((numOfTeammates + player.Teammates.Count + 1) > maxMembers) // would be +2 (not +1) if maxMembers account for ALL members, and not just member-total for this player
                    continue;

                // stupid way of combining teams, but appears to get the job done
                List<Player> combinedMates = new List<Player>(maxMembers);
                combinedMates.AddRange(player.Teammates);
                combinedMates.AddRange(_players[i].Teammates);

                player.Teammates.Add(_players[i]);
                _players[i].Teammates.Add(player);
                foreach (Player mate in combinedMates)
                {
                    if ((mate == player) || (mate == _players[i]))
                        continue;

                    mate.AddTeammate(player);
                    mate.AddTeammate(_players[i]);
                    player.AddTeammate(mate);
                    _players[i].AddTeammate(mate);
                }
                break;
            }
        }

        /// <summary>
        ///  Enables "ghost mode" for a given Player.
        /// </summary>
        /// <param name="pPlayer"> Player to enable ghostmode for.</param>
        private void EnableGhostModeForPlayer(Player pPlayer)
        {
            pPlayer.isGhosted = true;
            if (pPlayer.Client != null && pPlayer.Client.NetAddress != null)
                SendGhostmodeEnabled(pPlayer.Client.NetAddress);
            SendPlayerDisconnected(pPlayer.ID, true);
            HandleTeammateLeavingMatch(pPlayer);
        }
        #endregion general player methods


        /// <summary>
        ///  Traverses _players and sends the provided TXT to each player who's client is marked as Dev or Mod.
        /// </summary>
        /// <param name="txt"> Text to send them.</param>
        private void GoWarnModsAndAdmins(string txt)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] != null && ((bool)_players[i].Client?.isDev | (bool)_players[i].Client?.isMod))
                {
                    if (_players[i].Client?.NetAddress != null)
                    {
                        SendGrassLootFoundSound(_players[i].Client.NetAddress);
                        SendChatMessageWarning(_players[i], txt);
                    }
                }
            }
        }

        /// <summary>
        ///  Handles the approval of inocming NetClients.
        /// </summary>
        /// <param name="msg"> Incoming message with connection request data to process.</param>
        private void HandleConnectionApproval(NetIncomingMessage msg)
        {
            try
            {
                // verify that this ip isn't ip-banned.
                string ip = msg.SenderEndPoint.Address.ToString();
                for (int i = 0; i < _bannedIPs.Count; i++)
                {
                    if (_bannedIPs[i]["ip"] == ip)
                    {
                        string reason = "No reason provided.";
                        if (_bannedIPs[i]["reason"] != null && _bannedIPs[i]["reason"] != "") reason = _bannedIPs[i]["reason"];
                        msg.SenderConnection.Deny($"\nYou're banned from this server.\n\"{reason}\"");
                        Logger.Warn($"[Connection Approval - Warn] Incoming Connection @ {ip} is IP-banned! Their connection has been dropped...");
                        return;
                    }
                }

                // regular junk bc that ip is probably not banned then...
                string clientKey = msg.ReadString();
                Logger.Basic($"[Connection Approval] Incoming Connection @ {ip} sent key: \"{clientKey}\"");
                
                if (clientKey != _serverKey)
                {
                    msg.SenderConnection.Deny($"The key you sent is incorrect.");
                    Logger.Warn($"[Connection Approval - Warn] Incoming Connection @ {ip}'s key did not match the server's!");
                    return;
                }

                if (!_isMatchFull)
                {
                    if (!_hasMatchStarted && (_lobbyRemainingSeconds > 5.0f))
                    {
                        Logger.Success($"[Connection Approval - OK] Incoming Connection @ {ip} has been approved.");
                        msg.SenderConnection.Approve();
                    }
                    else
                        msg.SenderConnection.Deny("The match you are trying to join is already in progress.");
                }
                else
                    msg.SenderConnection.Deny("The match you are trying to join is currently full.");
            }
            catch (NetException)
            {
                Logger.Failure($"[ConnectionApproval - Error] {msg.SenderConnection} caused a NetException to occur!");
                msg.SenderConnection.Deny("There was an error while trying to allow you in.");
            }
        }

        /// <summary> Returns whether this Match's NetServer is still running or not.</summary>
        /// <returns>True if the NetServer's status is "running"; False is otherwise.</returns>
        public bool IsServerRunning() => server?.Status == NetPeerStatus.Running;
    }
}