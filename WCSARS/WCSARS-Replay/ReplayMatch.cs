using Lidgren.Network;
using SARStuff;
using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using WCSARS.Replay;

namespace WCSARS
{
    class ReplayMatch
    {
        // Main Stuff...
        private string _serverKey;
        private NetServer _server;
        private Player[] _players;
        private ReplayFrame[] _frames;
        private string _gamemode = "solo";
        private Player thisPlayer;

        private bool _sendFrames = false;
        private int _frameHead = 0;
        private Thread replayThread;


        // -- Level / RNG-Related --
        private int _lootSeed, _coconutSeed, _vehicleSeed; // Spawnable Item Generation Seeds

        /*
         * Match Send uses:
         7 -- Force Position / Land
         12 -- Match Positions
         17 - PlayerAttack
         45 - PlayerDataMode Changes
         */

        public ReplayMatch(ConfigLoader cfg) // Match but it uses ConfigLoader (EW!)
        {
            Logger.Header("[ReplayMatch] ConfigLoader Match creator used!");
            // Initialize PlayerStuff
            _serverKey = cfg.ServerKey;
            _players = new Player[cfg.MaxPlayers];

            string loc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            loc = loc + @"\replays\latest.wcsrp"; // Replay to read
            ReplayReader rpr = new ReplayReader(loc);
            _lootSeed = rpr.LootSeed;
            _coconutSeed = rpr.CoconutSeed;
            _vehicleSeed = rpr.HamsterballSeed;
            _players = rpr.Players;
            _frames = rpr.Frames;
            Logger.Warn($"[ReplayMatch] Using Seeds: LootSeed: {_lootSeed}; CoconutSeed: {_coconutSeed}; HampterSeed: {_vehicleSeed}");

            // Initialize NetServer
            Logger.Basic($"[ReplayMatch] Attempting to start server on \"{cfg.IP}:{cfg.Port}\".");
            Thread netThread = new Thread(ServerNetLoop);
            replayThread = new Thread(ReadDataFrames);
            NetPeerConfiguration config = new NetPeerConfiguration("BR2D");
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);  // Reminder to not remove this
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);        // Reminder to not remove this
            config.PingInterval = 22f;
            config.LocalAddress = System.Net.IPAddress.Parse(cfg.IP);
            config.Port = cfg.Port;
            _server = new NetServer(config);
            _server.Start();
            netThread.Start();
            Logger.Header("[ReplayMatch] Match created without encountering any errors.");
        }

        /// <summary>
        /// Handles all NetIncomingMessages sent to this Match's server. Continuously runs until this Match.server is no longer running.
        /// </summary>
        private void ServerNetLoop()
        {
            // Make sure no invalid....
            Logger.Basic("[ReplayMatch.ServerNetLoop] Network thread started!");
            // Loop to handle any recieved message from the NetServer... Stops when the server is no longer in the running state.
            NetIncomingMessage msg;
            while (IsServerRunning())
            {
                //Logger.DebugServer($"[{DateTime.UtcNow}] Waiting to receive message.");
                _server.MessageReceivedEvent.WaitOne(5000); // Halt this thread until the NetServer receives a message. Then continue
                //Logger.DebugServer($"[{DateTime.UtcNow}] Message has been received.");
                while ((msg = _server?.ReadMessage()) != null)
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
                                    NetOutgoingMessage acceptMsgg = _server.CreateMessage();
                                    acceptMsgg.Write((byte)0);
                                    acceptMsgg.Write(true);
                                    _server.SendMessage(acceptMsgg, msg.SenderConnection, NetDeliveryMethod.ReliableOrdered);
                                    break;
                                case NetConnectionStatus.Disconnected:
                                    Logger.Warn($"[NCS.Disconnected] Client GoodbyeMsg: {msg.ReadString()}");
                                    break;
                            }
                            break;
                        case NetIncomingMessageType.ConnectionApproval: // MessageType.ConnectionApproval MUST be enabled to work
                            Logger.Header("[Connection Approval] A new connection is awaiting approval!");
                            string clientKey = msg.ReadString();
                            Logger.Basic($"[Connection Approval] Incoming connection {msg.SenderEndPoint} sent key: {clientKey}");
                            if (clientKey == _serverKey) msg.SenderConnection.Approve();
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
                        default:
                            Logger.Failure("Unhandled type: " + msg.MessageType);
                            break;
                    }
                    _server?.Recycle(msg);
                }
                SendDummyMessage();
            }
            // Once the NetServer is no longer running we're basically done... So can just do this and everything is over.
            Logger.DebugServer($"[{DateTime.UtcNow}] [ServerNetLoop] Match.server is no longer running. I shutdown as well... Byebye!");
        }

        private void ReadDataFrames()
        {
            Logger.DebugServer("ReadDataFrames | Starting to read frames!");
            if (_frames != null)
            {
                DateTime _next = DateTime.UtcNow.AddMilliseconds(250);
                while (_sendFrames && _frameHead != _frames.Length)
                {
                    try
                    {
                        //Logger.DebugServer($"FrameHead: {_frameHead}");
                        ReplayFrame frame = _frames[_frameHead];
                        NetOutgoingMessage msg = _server.CreateMessage(_frames[_frameHead].Data.Length);
                        msg.Write(_frames[_frameHead].Data);
                        _server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
                    } catch (Exception ex)
                    {
                        Logger.Failure(ex.ToString());
                    }
                    _frameHead++;
                    Thread.Sleep(16); // still too slow / fast... too silly to figure out what the problem is right now
                }
            }
            else Logger.Failure("ReadDataFrames | Started thread while _frames was null!");
            Logger.DebugServer("ReadDataFrames | Finished!");
        }

        private void ResetForRoundStart() // was from copy-paste; not used for anything
        {

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


        /// <summary>
        /// Sends a "StartMatch" message to all NetPeers. The message will use the default values.
        /// </summary>
        private void SendMatchStart() // used for replay viewer
        {
            NetOutgoingMessage msg = _server.CreateMessage();
            msg.Write((byte)6);
            msg.Write((byte)1);
            msg.Write((short)14);
            msg.Write((short)(3 * 100)); // Desert Wind% -- 45% = DEATH
            msg.Write((byte)1);
            msg.Write((short)14);
            msg.Write((short)(5 * 100)); // Taundra Wind% -- Supposedly...
            _server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        // So the original plan was for HandleMessage to run asynchronously (no spellcheck!); but that didn't really work out.
        // So async HandleMessage has been postponed indefinitely. Sowwy for disappointment 
        // If anyone *really* wants async HandleMessage, then go for it! But just know it'll probably be really hard D:
        private void HandleMessage(NetIncomingMessage msg)
        {
            byte b = msg.ReadByte();
            switch (b)
            {
                    // Msg1 -- Authentication Request >>> Msg2 -- Authentication Response [Confirm / Deny]
                case 1:
                    AcceptConnection(msg);
                    break;

                    // Msg3 -- Client Ready & Send Characters >>> Msg4 -- Confirm ready / send match info
                case 3: // OK for now -- would like to improve | WARNING: game modification required for "SteamName"
                    HandleThisPlayer(msg);
                    break;

                case 5:
                    Logger.Header($"<< Ready-Request @ {msg.SenderEndPoint} received! >>");
                    SendPlayerCharacters(msg);
                    Logger.Basic($"<< Ready Confirmed for {msg.SenderEndPoint}. >>");
                    Thread.Sleep(500); // wait a sec then send positions
                    SendLobbyPlayerPositions(); // replay-viewer needs to know where everyone is if you wanna see them all in-lobby
                    break;

                case 7:
                    ForceLand();
                    break;

                    // Msg25 -- Client Sent Chat Message --> I no write anymore
                case 25:
                    serverHandleChatMessage(msg);
                    break;

                case 14:
                    break;
                case 97:
                    SendDummyMessage();
                    break;

                default:
                    Logger.missingHandle($"Message appears to be missing handle. ID: {b}");
                    break;
            }
        }

        private void ForceLand()
        {
            NetOutgoingMessage pmsg = _server.CreateMessage(22);
            pmsg.Write((byte)8);          // Byte  | MsgID (8)  | 1?
            pmsg.Write(thisPlayer.ID);        // Short | PlayerID   | 4
            pmsg.Write(508f); // Float | PositionX  | 8
            pmsg.Write(508f); // Float | PositionY  | 8
            pmsg.Write(true);      // Bool  | Parachute? | 1?
            pmsg.Write(pmsg.Data);
            _server.SendToAll(pmsg, NetDeliveryMethod.ReliableOrdered);
        }


        // Instant accept message for player
        private void AcceptConnection(NetIncomingMessage pmsg)
        {
            Logger.Header("[AcceptConnection] Accept Start!");
            NetOutgoingMessage msg = _server.CreateMessage();
            msg.Write((byte)2);
            msg.Write(true);
            _server.SendMessage(msg, pmsg.SenderConnection, NetDeliveryMethod.ReliableUnordered);
            Logger.Basic("[AcceptConnection] Accept sent!");
        }

        // Reads ThisPlayer's playerdata so they get dumped in the match
        private void HandleThisPlayer(NetIncomingMessage msg) // Msg3 >> Msg4
        {
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
                short asign = (short)-1;
                // --- end of data read v0.90.2
                thisPlayer = new Player(asign, animal, umbrella, gravestone, deathEffect, emoteIDs, hat, glasses,
                    beard, clothes, melee, gsCount, gsGunIDs, gsSkinIndicies, steamName, msg.SenderConnection);
                SendMatchInformation(msg.SenderConnection, asign);
            }
            catch (NetException netEx)
            {
                Logger.Failure($"[HandleIncomingPlayerRequest] [Error] {msg.SenderConnection} caused a NetException!\n{netEx}");
                msg.SenderConnection.Disconnect("There was an error reading your packet data. [HandleIncomingConnection]");
            }
        }

        // General Match Info
        private void SendMatchInformation(NetConnection client, short assignedID) // Msg4
        {
            NetOutgoingMessage msg = _server.CreateMessage();
            msg.Write((byte)4);                 // Byte   |  MessageID 
            msg.Write(assignedID);              // Short  |  AssignedID
            // Send RNG Seeds
            msg.Write(_lootSeed);               // Int  |  LootGenSeed
            msg.Write(_coconutSeed);            // Int  |  CocoGenSeed
            msg.Write(_vehicleSeed);            // Int  |  VehicleGenSeed
            // Match / Lobby Info...
            msg.Write(double.MaxValue);         // Double  |  LobbyTimeRemaining
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
            _server.SendMessage(msg, client, NetDeliveryMethod.ReliableOrdered);
        }

        // Sends all Player characters
        private void SendPlayerCharacters(NetIncomingMessage pmsg) // Working v0.90.2 - ???
        {
            if (thisPlayer == null)
            {
                Logger.Failure("Player connected while thisPlayer was not real.");
                return;
            }
            NetOutgoingMessage msg = _server.CreateMessage();
            msg.Write((byte)10);                    // Byte | MsgID (10)
            msg.Write((byte)(_players.Length+1));       // PlayersLength MUST always be the amount of valid entries
            Logger.DebugServer($"length: {_players.Length}");
            Logger.DebugServer($"_players[0].Name: {_players[0].Name}");
            for (int i = 0; i < _players.Length; i++)
            {
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
                msg.Write((short)-1);  // Short | Slot1 WeaponID -- TODO: Equiping items in Lobby should be real?
                msg.Write((short)-1);  // Short | Slot2 WeaponID
                msg.Write(_players[i].LootItems[0].Rarity);              // Byte  | Slot1 WeaponRarity
                msg.Write(_players[i].LootItems[1].Rarity);              // Byte  | Slot1 WeaponRarity
                msg.Write(_players[i].ActiveSlot);                       // Byte  | ActiveSlotID
                msg.Write(_players[i].isDev);                            // Bool  | IsDeveloper
                msg.Write(_players[i].isMod);                            // Bool  | IsModerator
                msg.Write(_players[i].isFounder);                        // Bool  | IsFounder
                msg.Write((short)1000);                                  // Short | PlayerLevel
                msg.Write((byte)0);
                //if (_gamemode == "solo") msg.Write((byte)0);             // Byte  | Teammate Count
               // else // NO support for SvR / Mystery Mode / Bwocking Dead
               //{
                   // msg.Write((byte)(_players[i].Teammates.Count + 1));   // Byte  | # o' Teammates << self counted
                   // msg.Write(_players[i].ID);                          // Short | TeammateID << self counted
                  //  for (int w = 0; w < _players[i].Teammates.Count; w++) msg.Write(_players[i].Teammates[w].ID);
                //}
            }
            #region thisPlayer
            msg.Write(thisPlayer.ID);               // Short   | PlayerID
            msg.Write(thisPlayer.AnimalID);         // Short   | CharacterID
            msg.Write(thisPlayer.UmbrellaID);       // Short   | UmbrellaID
            msg.Write(thisPlayer.GravestoneID);     // Short   | GravestoneID
            msg.Write(thisPlayer.DeathExplosionID); // Short   | DeathExplosionID
            for (int j = 0; j < 6; j++)                 // Short[] | PlayerEmotes: Always 6 in v0.90.2
            {
                msg.Write(thisPlayer.EmoteIDs[j]);  // Short   | EmoteID[i]
            }
            msg.Write(thisPlayer.HatID);            // Short   | HatID
            msg.Write(thisPlayer.GlassesID);        // Short   | GlassesID
            msg.Write(thisPlayer.BeardID);          // Short   | BeardID
            msg.Write(thisPlayer.ClothesID);        // Short   | ClothesID
            msg.Write(thisPlayer.MeleeID);          // Short   | MeleeID
            msg.Write(thisPlayer.GunSkinCount);     // Byte    | AmountOfGunSkins
            for (int k = 0; k < thisPlayer.GunSkinKeys.Length; k++)
            {
                msg.Write(thisPlayer.GunSkinKeys[k]);    // Short | GunSkinKey[i]
                msg.Write(thisPlayer.GunSkinValues[k]);  // Byte  | GunSkinValue[i]
            }
            msg.Write(thisPlayer.Position.x);   // Float  | PositionX
            msg.Write(thisPlayer.Position.y);   // Float  | PositionY
            msg.Write(thisPlayer.Name);         // String | Username
            msg.Write(thisPlayer.EmoteID);      // Short  | CurrentEmote (emote players will still dance when joining up)
            msg.Write((short)thisPlayer.LootItems[0].WeaponIndex);  // Short | Slot1 WeaponID -- TODO: Equiping items in Lobby should be real?
            msg.Write((short)thisPlayer.LootItems[1].WeaponIndex);  // Short | Slot2 WeaponID
            msg.Write(thisPlayer.LootItems[0].Rarity);              // Byte  | Slot1 WeaponRarity
            msg.Write(thisPlayer.LootItems[1].Rarity);              // Byte  | Slot1 WeaponRarity
            msg.Write(thisPlayer.ActiveSlot);                       // Byte  | ActiveSlotID
            msg.Write(thisPlayer.isDev);                            // Bool  | IsDeveloper
            msg.Write(thisPlayer.isMod);                            // Bool  | IsModerator
            msg.Write(thisPlayer.isFounder);                        // Bool  | IsFounder
            msg.Write((short)1000);                                  // Short | PlayerLevel
            msg.Write((byte)0);
            /*if (_gamemode == "solo") msg.Write((byte)0);             // Byte  | Teammate Count
            else // NO support for SvR / Mystery Mode / Bwocking Dead
            {
                msg.Write((byte)(thisPlayer.Teammates.Count + 1));   // Byte  | # o' Teammates << self counted
                msg.Write(thisPlayer.ID);                          // Short | TeammateID << self counted
                for (int w = 0; w < thisPlayer.Teammates.Count; w++) msg.Write(thisPlayer.Teammates[w].ID);
            }*/
            Logger.DebugServer("bottom of for");
            #endregion thisPlayer
            Logger.DebugServer("should've written ThisPlayer to thingy");
            _server.SendToAll(msg, NetDeliveryMethod.ReliableUnordered);
        }

        // Chat Message
        private void serverHandleChatMessage(NetIncomingMessage message) // TODO - Cleanup; fix/redo or remove the """Command""" feature
        {
            Logger.Header("Chat message. Wonderful!");
            //this is terrible. we are aware. have fun.

            if (message.PeekString().StartsWith("/"))
            {
                string[] command = message.PeekString().Split(" ", 9);
                string responseMsg = "command executed... no info given...";
                switch (command[0])
                {
                    case "/p": // send all replay-players to replay-viewer again
                        SendLobbyPlayerPositions();
                        responseMsg = "Sent all replay-players current positions to replay-viewer.";
                        break;
                    case "/l": // land the replay-viewer
                        ForceLand();
                        responseMsg = $"Force Ejected {thisPlayer?.Name} ({thisPlayer?.ID}) @ {thisPlayer.Position}";
                        break;
                    case "/start": // start the "replay"
                        if (!_sendFrames)
                        {
                            SendMatchStart();
                            _sendFrames = true;
                            replayThread.Start();
                            responseMsg = "Replay started!";

                        }
                        else responseMsg = "Replay has already started!";
                        break;
                    case "/goto":
                        if (command.Length > 1)
                        {
                            if (int.TryParse(command[1], out int gotoSpot))
                            {
                                if (gotoSpot < 0)
                                {
                                    responseMsg = $"Frame value \"gotoSpot\" too low. Value must be from 0-{_frames.Length}";
                                    break;
                                }
                                if (gotoSpot >= _frames.Length)
                                {
                                    responseMsg = $"Frame value \"gotoSpot\" too high. Max: {_frames.Length}";
                                    break;
                                }
                                _frameHead = gotoSpot;
                                responseMsg = $"FrameHead set to \"{_frameHead}\"";
                            }
                            else responseMsg = $"Could not parse value \"{command[1]}\" as an int.";
                        }
                        else responseMsg = "Not enough arguments. Use: /goto [position]";
                        break;
                    case "/ghost": // msg 105 -- Command is currently testing only!
                                   // Lets you move around like ghost, but your body sticks around for some reason. Oh well...
                        NetOutgoingMessage ghostMsg = _server.CreateMessage();
                        ghostMsg.Write((byte)105);
                        _server.SendMessage(ghostMsg, message.SenderConnection, NetDeliveryMethod.ReliableUnordered);
                        break;
                }
                //now send response to player...
                NetOutgoingMessage allchatmsg = _server.CreateMessage();
                allchatmsg.Write((byte)94);
                allchatmsg.Write((short)-1); //ID of player who sent msg
                allchatmsg.Write(responseMsg);
                _server.SendToAll(allchatmsg, NetDeliveryMethod.ReliableUnordered);
                //server.SendMessage(allchatmsg, message.SenderConnection, NetDeliveryMethod.ReliableUnordered);
            }
            else // Regular Message
            {
                NetOutgoingMessage allchatmsg = _server.CreateMessage();
                allchatmsg.Write((byte)26);             // Byte   | MsgID (26)
                allchatmsg.Write((short)-1);            // Short  | PlayerID
                allchatmsg.Write(message.ReadString()); // String | MessageText
                allchatmsg.Write(false);                // Bool   | ToTeam? (Must make sure message only gets sent to teammate if it's true)
                _server.SendToAll(allchatmsg, NetDeliveryMethod.ReliableUnordered);
            }
        }

        // Dummy Message
        private void SendDummyMessage() // Msg97
        {
            NetOutgoingMessage dummy = _server.CreateMessage();
            dummy.Write((byte)97);
            _server.SendToAll(dummy, NetDeliveryMethod.ReliableOrdered);
        }

        // Eh Figure out if ReplayMatch server is still running or not
        public bool IsServerRunning()
        {
            return _server?.Status == NetPeerStatus.Running;
        }
        private void SendLobbyPlayerPositions() // Msg11
        {
            if (!IsServerRunning()) return;
            Logger.Warn("Sending lobby positions");
            // Make message sending player data. Loops entire list but only sends non-null entries.
            NetOutgoingMessage msg = _server.CreateMessage();
            msg.Write((byte)11);                    // Byte | MsgID (11)
            msg.Write((byte)_players.Length); // Byte | # of Iterations
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null) continue;
                msg.Write(_players[i].ID);                                              // Short | PlayerID
                msg.Write((sbyte)((180f * _players[i].MouseAngle / 3.141592f) / 2));    // sbyte  | LookAngle
                msg.Write((ushort)(_players[i].Position.x * 6f));                       // ushort | PositionX 
                msg.Write((ushort)(_players[i].Position.y * 6f));                       // ushort | PositionY
            }
            _server.SendToAll(msg, NetDeliveryMethod.ReliableSequenced);
        }
    }
}