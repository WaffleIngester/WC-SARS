using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;

namespace WCSARS.Replay
{
    internal class ReplayQueue // place to store deplay messages that'll be dumped to the latest replay file.
    {
        private List<ReplayMessage> _frames;
        private readonly int _maxSize;

        private bool m_active = true;
        private bool m_qLock = false;
        private Thread qThread; // unused

        public ReplayQueue(int queueSize)
        {
            _frames = new List<ReplayMessage>(queueSize);
            _maxSize = queueSize;
            VerifyLatest(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
            //string loc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            //VerifyLatest(loc + @"\replays\latest.wcsrp");
            //qThread = new Thread(CheckQueue);
            //qThread.Start();
        }

        private void CheckQueue() // unused; testing better frames
        {
            Logger.DebugServer("[ReplayQueue] Thread started!");
            while (m_active)
            {
                if (_frames == null)
                {
                    m_active = false;
                    Logger.DebugServer("[ReplayQueue] Frames is Null!");
                    break;
                }
                if (_frames.Count >= _maxSize)
                {
                    Logger.DebugServer("reached capacity");
                    DumpToFile();
                }
                Thread.Sleep(300);
            }
            Logger.DebugServer("[ReplayQueue] Closed!");
        }

        private void DumpToFile()
        {
            try
            {
                // Basics out of the way
                Logger.Header("[Replay Queue] Write Start!");
                m_qLock = true;
                Logger.Header("[Replay Queue] Convert to array!");
                ReplayMessage[] rpMsgs = _frames.ToArray();
                Console.WriteLine(rpMsgs.Length);
                _frames.Clear();
                m_qLock = false;
                Logger.Basic("[Replay Queue] Converted to array + push to queue access back!");

                // Dump
                string baseFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string bruh = baseFolder + "latest.wcsrp";
                Logger.DebugServer(bruh);
                using (BinaryWriter wr = new BinaryWriter(File.Open(baseFolder + @"\replays\latest.wcsrp", FileMode.Append)))
                {
                    Logger.Basic("[Replay Queue] Dumping to latest.wcsrp!");
                    for (int i = 0; i < rpMsgs.Length; i++)
                    {
                        switch (rpMsgs[i].FrameType)
                        {
                            case FrameType.MatchData:
                                wr.Write((byte)FrameType.MatchData);
                                wr.Write(rpMsgs[i].Data);
                                break;
                            case FrameType.NetMsg:
                                wr.Write((byte)FrameType.NetMsg);
                                wr.Write(rpMsgs[i].Data.Length);
                                wr.Write(rpMsgs[i].Data);
                                break;
                        }
                    }
                    rpMsgs = null;
                    Logger.Success("[Replay Queue] Finished Write!");
                }
            } catch (Exception ex)
            {
                Logger.Failure($"Unhandled exception! Uh oh! D:\n{ex}");
            }
        }

        // It's dumb, but it works. Surely there is a better way; but just wanted to test junk so idrc
        public static void VerifyLatest(string location)
        {
            string dstLatest = location + @"\replays\latest.wcsrp";
            if (File.Exists(dstLatest))
            {
                DateTime creationTime = File.GetLastWriteTime(dstLatest);
                string newName = $"{creationTime.Day}-{creationTime.Month}-{creationTime.Year}-{creationTime.Minute}-{creationTime.Second}.wcsrp";
                string newLoc = location + @"\replays\" + newName;
                File.Move(dstLatest, newLoc);
                //Logger.Success($"Moved {dstLatest} to {newLoc}");
            }
            File.Create(dstLatest);
        }

        /// <summary>
        /// Pushes the provided ReplayMessage object onto this ReplayQueue's frame list. If the list is locked; waits 50ms repeatidly until it isn't.
        /// </summary>
        /// <param name="rpMsg">Message to push</param>
        public void Push(ReplayMessage rpMsg)
        {
            while (m_qLock)
            {
                Logger.DebugServer("[Replay Queue] List Locked! Cannot push! Waiting 50ms!");
                Thread.Sleep(50);
            }
            if (_frames.Count >= _maxSize) DumpToFile();
            _frames.Add(rpMsg);
        }

        public void ForceDump() // Just gives public access to the dump method.
        {
            if (m_qLock)
            {
                Logger.DebugServer("was already dumping before calling");
                return;
            }
            DumpToFile();
        }
    }
}
