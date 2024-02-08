using System;
using System.IO;
using WCSARS.Configuration;

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

            try
            {
                Match mMatch;
                mMatch = new Match(new Config());
                //if (args.Length > 0) // executable.exe IP PORT -- notice how there's no "-"
                //mMatch = new Match(int.Parse(args[1]), args[0]);

                // Do whatever stuff you want here
            }
            catch (Exception ex)
            {
                Logger.Failure($"Some sort of general, unhandled exception has occurred.\n{ex}");
            }
        }

        /// <summary>
        /// Attempts to create all the necessary files required for the Match to run.
        /// </summary>
        static void InitializeData()
        {
            string location = AppDomain.CurrentDomain.BaseDirectory;
            CreateFile(location + @"\player-data.json");
            CreateFile(location + @"\banned-players.json");
            CreateFile(location + @"\banned-ips.json");
        }

        /// <summary>
        ///  Creates file at the speciifed location, writes "[]" to it, then closes.
        /// </summary>
        static void CreateFile(string filename)
        {
            if (File.Exists(filename))
                return;

            using (FileStream fileStream = File.Create(filename))
            {
                fileStream.Write(new byte[] { 0x5B, 0X5D });
            }
            Logger.Success("[Main] [OK] Created file: " + filename);
        }
    }
}