using System;
using System.Collections.Generic;
using System.Text;

namespace WCSARS.Replay
{
    /* 
     * You *could* use a NetOutgoingMessage's `Data` property; but it dumps the entire list.
     * So, if a message got 500-bytes allocated; all 500 bytes gets dumped, even if only 50 were used.
     * 
     * This version shouldn't do that. Which makes life a lot easier.
     * 
     * The current system of doing things still has its own share of issues however. So, a better
     * replay system should be devised at some point. This is just for getting the idea out there.
     */
    internal class ReplayMessage // RPMessages = discount NetOutgoingMessage; RPFrame = read NetMessage data from a "replay" file
    {
        public readonly FrameType FrameType;
        private List<byte> _data;

        /// <summary>
        /// Returns an array of the bytes stored in this ReplayMessage's _data list.
        /// </summary>
        public byte[] Data { get => _data.ToArray(); } // cheap trick

        /// <summary>
        /// Creates a new ReplayMessage object with an unspecified inital data capacity. (can be slower!)
        /// </summary>
        public ReplayMessage()
        {
            _data = new List<byte>();
        }

        /// <summary>
        /// Creates a new NetMsg ReplayMessage object with a specified inital data capacity.
        /// </summary>
        /// <param name="initSize">Initial capacity.</param>
        public ReplayMessage(int initSize)
        {
            FrameType = FrameType.NetMsg;
            _data = new List<byte>(initSize);
        }

        /// <summary>
        /// Creates a MatchData ReplayMessage object using the provided parameters
        /// </summary>
        /// <param name="loot">LootGenSeed</param>
        /// <param name="coco">CoconutGenSeed</param>
        /// <param name="hamster">HamsterGenSeed</param>
        /// <param name="players">Match PlayerList (auto trimmed).</param>
        public ReplayMessage(int loot, int coco, int hamster, Player[] players)
        {
            FrameType = FrameType.MatchData;
            _data = new List<byte>((12 + (players.Length * 32)));
            Write(loot);
            Write(coco);
            Write(hamster);
            WritePlayers(players);
        }

        #region Writes
        public void Write(byte source)
        {
            _data.Add(source);
        }
        public void Write(int source)
        {
            byte[] bytes = BitConverter.GetBytes(source);
            for (int i = 0; i < bytes.Length; i++) _data.Add(bytes[i]);
        }
        public void Write(uint source)
        {
            byte[] bytes = BitConverter.GetBytes(source);
            for (int i = 0; i < bytes.Length; i++) _data.Add(bytes[i]);
        }
        public void Write(short source)
        {
            byte[] bytes = BitConverter.GetBytes(source);
            for (int i = 0; i < bytes.Length; i++)
            {
                _data.Add(bytes[i]);
            }
        }
        public void Write(ushort source)
        {
            byte[] bytes = BitConverter.GetBytes(source);
            for (int i = 0; i < bytes.Length; i++) _data.Add(bytes[i]);
        }
        public void Write(long source)
        {
            byte[] bytes = BitConverter.GetBytes(source);
            for (int i = 0; i < bytes.Length; i++) _data.Add(bytes[i]);
        }
        public void Write(ulong source)
        {
            byte[] bytes = BitConverter.GetBytes(source);
            for (int i = 0; i < bytes.Length; i++) _data.Add(bytes[i]);
        }
        public void Write(float source)
        {
            byte[] bytes = BitConverter.GetBytes(source);
            for (int i = 0; i < bytes.Length; i++) _data.Add(bytes[i]);
        }
        public void Write(double source)
        {
            byte[] bytes = BitConverter.GetBytes(source);
            for (int i = 0; i < bytes.Length; i++) _data.Add(bytes[i]);
        }
        public void Write(bool source)
        {
            _data.Add(source ? (byte)1 : (byte)0);
        }
        public void Write(string source)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(source);
            WriteVariableUInt32((uint)bytes.Length);
            for (int i = 0; i < bytes.Length; i++) _data.Add(bytes[i]);
        }
        public int WriteVariableUInt32(uint value) // From Lidgren
        {
            int retval = 1;
            uint num1 = value;
            while (num1 >= 0x80)
            {
                Write((byte)(num1 | 0x80));
                num1 = num1 >> 7;
                retval++;
            }
            Write((byte)num1);
            return retval;
        }

        private void WritePlayers(Player[] players)
        {
            int count = 0;
            for (int i = 0; i < players.Length; i++) if (players[i] != null) count++;
            Write(count);
            Logger.DebugServer($"players[i].: {count}");
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                // Player is Real
                Write(players[i].Name);
                Logger.DebugServer($"Name: {players[i].Name}");
                Write(players[i].ID);
                Logger.DebugServer($"ID: {players[i].ID}");
                Write(players[i].AnimalID);
                Logger.DebugServer($"A: {players[i].AnimalID}");
                Write(players[i].UmbrellaID);
                Logger.DebugServer($"U: {players[i].UmbrellaID}");
                Write(players[i].GravestoneID);
                Logger.DebugServer($"G: {players[i].GravestoneID}");
                Write(players[i].DeathExplosionID);
                Logger.DebugServer($"DE: {players[i].DeathExplosionID}");
                for (int j = 0; j < 6; j++) Write(players[i].EmoteIDs[j]);
                Write(players[i].HatID);
                Logger.DebugServer($"H: {players[i].HatID}");
                Write(players[i].GlassesID);
                Logger.DebugServer($"G.: {players[i].GlassesID}");
                Write(players[i].BeardID);
                Logger.DebugServer($"B.: {players[i].BeardID}");
                Write(players[i].ClothesID);
                Logger.DebugServer($"C.: {players[i].ClothesID}");
                Write(players[i].MeleeID);
                Logger.DebugServer($"M: {players[i].ClothesID}");
                Write(players[i].GunSkinCount);
                Logger.DebugServer($"GSC: {players[i].GunSkinCount}");
                for (int k = 0; k < players[i].GunSkinCount; k++)
                {
                    Write(players[i].GunSkinKeys[k]);
                    Write(players[i].GunSkinValues[k]);
                }
            }
        }
        #endregion Writes
    }
}
