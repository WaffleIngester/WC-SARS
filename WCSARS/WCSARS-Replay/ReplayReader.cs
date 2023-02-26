using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace WCSARS.Replay
{
    internal class ReplayReader // will read whatever replay is provided. only placed used in currently is ReplayMatch, which always tries `latest.wcsrp`.
    {
        /* Layout
         * 
         *  MatchInfoHeader
         * -----
         * | - MatchSeeds
         * |    --> LootGen, Coco, Hamsterballs
         * | - # of Players
         * | --> PlayerData here
         * -----
         * 
         * ....
         * ...
         * ..
         * .
         * 
         *  NetMessages
         * -----
         * | int | num of bytes
         * | bunch-of
         * | bytes-go-here
         * | the-rest-are
         * | the-bytes
         * |
         * -----
         * 
         */

        public readonly int LootSeed;
        public readonly int CoconutSeed;
        public readonly int HamsterballSeed;
        public readonly Player[] Players;
        private List<ReplayFrame> _rpFrames;
        public ReplayFrame[] Frames { get => _rpFrames.ToArray(); }

        /// <summary>
        /// Creates a ReplayReader object that will then read a WC-SARS replay file from the specified location.
        /// </summary>
        /// <param name="loc">Location where the replay is located.</param>
        public ReplayReader(string loc)
        {
            try
            {
                using (FileStream fs = File.OpenRead(loc))
                {
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        Logger.Header("[ReplayReader] Starting load!");
                        _rpFrames = new List<ReplayFrame>((int)(fs.Length / 16)); // 16 is arbitrary!
                        Logger.Basic($"[ReplayReader] _rpFrames.Capacity: {_rpFrames.Capacity}");

                        // Try reading all the junk
                        Logger.Header("[ReplayReader] Starting to read...");
                        while (fs.Position != fs.Length)
                        {
                            FrameType header = (FrameType)br.ReadByte();
                            switch (header)
                            {
                                case FrameType.MatchData:
                                    {
                                        LootSeed = br.ReadInt32();
                                        Logger.Warn($"Seed1: {LootSeed}");
                                        CoconutSeed = br.ReadInt32();
                                        Logger.Warn($"Seed1: {CoconutSeed}");
                                        HamsterballSeed = br.ReadInt32();
                                        Logger.Warn($"Seed1: {HamsterballSeed}");
                                        int pCount = br.ReadInt32();
                                        Player[] players = new Player[pCount];
                                        for (int i = 0; i < pCount; i++) players[i] = LoadPlayer(br);
                                        Players = players;
                                    }
                                    break;
                                case FrameType.NetMsg:
                                    {
                                        int length = br.ReadInt32();
                                        //Logger.Warn($"Length of this NetMsg: {length}");
                                        byte[] data = new byte[length];
                                        for (int i = 0; i < length; i++) data[i] = br.ReadByte();
                                        _rpFrames.Add(new ReplayFrame(data));
                                    }
                                    break;
                            }
                        }
                        Logger.Success("[ReplayReader] Finished the read without any errors! Wahoo!");
                    }
                }
            } catch (FileNotFoundException)
            {
                Logger.Failure($"Unable to locate the specified file.\nSearched at: {loc}");
            }
            catch (EndOfStreamException eosEx)
            {
                Logger.Failure($"There was an error while trying to read data! Threw an EndOfStreamException!\n{eosEx}");
            }
        }

        /// <summary>Reads Player-data from the provided BinaryReader.</summary>
        /// <param name="reader">Supplied BinaryReader to use.</param>
        /// <returns>A Player object from the read stream data.</returns>
        private Player LoadPlayer(BinaryReader reader)
        {
            string name = reader.ReadString();
            short id = reader.ReadInt16();
            short animal = reader.ReadInt16();
            short umbrella = reader.ReadInt16();
            short grave = reader.ReadInt16();
            short deathEffect = reader.ReadInt16();
            short[] emotes =
            {
                reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16(),
                reader.ReadInt16(), reader.ReadInt16(), reader.ReadInt16()
            };
            short hat = reader.ReadInt16();
            short glasses = reader.ReadInt16();
            short beard = reader.ReadInt16();
            short cloth = reader.ReadInt16();
            short melee = reader.ReadInt16();
            byte skinCount = reader.ReadByte();
            short[] skinKeys = new short[skinCount];
            byte[] skinVals = new byte[skinCount];
            for (int i = 0; i < skinCount; i++)
            {
                skinKeys[i] = reader.ReadInt16();
                skinVals[i] = reader.ReadByte();
            }
            return new Player(name, id, animal, umbrella, grave, deathEffect, emotes,
                hat, glasses, beard, cloth, melee, skinCount, skinKeys, skinVals);
        }
    }
}
