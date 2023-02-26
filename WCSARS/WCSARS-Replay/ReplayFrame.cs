using System;
using System.Collections.Generic;
using System.Text;

namespace WCSARS.Replay
{
    /// <summary>
    /// Represents a "frame" of replay data.
    /// </summary>
    internal class ReplayFrame // RPFrame = StoredNetMessages; RPMessages = discount NetOutgoingMessage
    {
        /// <summary>
        /// FrameType of this ReplayFrame.
        /// </summary>
        public readonly FrameType FrameType;

        /// <summary>
        /// The data this ReplayFrame holds.
        /// </summary>
        public readonly byte[] Data;

        /// <summary>
        /// Creates a NetMsg ReplayFrame object.
        /// </summary>
        /// <param name="pdata">PacketData to store.</param>
        public ReplayFrame(byte[] pdata)
        {
            FrameType = FrameType.NetMsg;
            Data = pdata;
        }
    }
}
