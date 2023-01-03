using System;
using System.IO;
using SimpleJSON;

namespace WCSARS
{
    class Program
    {
        static void Main(string[] args)
        {
            Logger.Basic("<< WC-SARS >>");
            Logger.Header("> SAR: v0.90.2");

            // Create necessary files for Match initialization.
            InitializeData();
            ConfigLoader mCfg = new ConfigLoader();
            Match mMatch;

            if (args.Length > 0) // try loading using command line args. really basic, likely will just break
            {
                mMatch = new Match(int.Parse(args[1]), args[0]); // executable.exe IP PORT -- notice how there's no "-"
            }
            else
            {
                mMatch = new Match(mCfg);
            }

            // Do whatever stuff you want here 
        }

        /// <summary>
        /// Attempts to create all the necessary files required for the Match to run.
        /// </summary>
        static void InitializeData()
        {
            string location = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Logger.DebugServer("Current Running Location:\n" + location);
            CreateFile(location + @"\player-data.json");
            CreateFile(location + @"\banned-players.json");
            CreateFile(location + @"\banned-ips.json");
        }

        /// <summary>
        /// Creates file at the speciifed location, writes "[]" to it, then closes.
        /// </summary>
        static void CreateFile(string filename)
        {
            if (File.Exists(filename)) return; // Feel like no need to log anymore. At least right now :]
            using (FileStream fileStream = File.Create(filename))
            {
                fileStream.Write(new byte[] { 0x5B, 0X5D });
            }
            Logger.Success("[Main] [OK] Created file: " + filename);
        }
    }
}