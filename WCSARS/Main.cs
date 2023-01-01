using System;
using System.IO;
using SimpleJSON;

namespace WCSARS
{
    class Program
    {
        static void Main(string[] args)
        {
            Logger.Basic("<< Super Animal Royale Server  >>");
            Logger.Header("Super Animal Royale Version: 0.90.2\n");

            // Create necessary files for Match initialization.
            InitializeData();

            if (args.Length > 0)
            {
                Match m = new Match(int.Parse(args[1]), args[0]);
            }
            else if (File.Exists(Directory.GetCurrentDirectory() + @"\config.json"))
            {
                Logger.Basic("Starting server using config located at: " + Directory.GetCurrentDirectory() + @"\config.json");
                JSONNode config = JSON.Parse(File.ReadAllText(Directory.GetCurrentDirectory() + @"\config.json"));
                if (config["ServerIP"] != null && config["ServerPort"] != null)
                {
                    Match configuredMatch = new Match(config["ServerPort"].AsInt, config["ServerIP"]);
                }
                else
                {
                    Logger.Failure("Missing either ServerIP or ServerPort key in ConfigJSON. ServerIP should be STRING and ServerPort a INT");
                }
            }
            else
            {
                bool runSetup = true;
                Logger.Warn("If you know, you know. ['Y' OR 'N' key]");
                while (runSetup)
                {
                    switch (Console.ReadKey(true).Key)
                    {
                        case ConsoleKey.Y:
                            Logger.Basic("attempting to start a server! (port: 4206; local address: 192.168.1.13)");
                            runSetup = false;
                            Match match2 = new Match(4206, "192.168.1.13");
                            break;
                        case ConsoleKey.N:
                            Logger.Basic("attempting to start a server! (port: 42896; local address: 192.168.1.198)");
                            runSetup = false;
                            Match match1 = new Match(42896, "192.168.1.198");
                            break;
                        default:
                            Logger.Failure($"Invalid key. Please try again");
                            Logger.Warn("If you know, you know. ['Y' OR 'N' key]");
                            break;
                    }
                }
            }
            Logger.DebugServer("[MAIN] I have reached the end!");
            /*string text;
            while ((text = Console.ReadLine()) != "stop"){

                Console.WriteLine(text);
            }*/
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

            // Create a default config file... -- Currently unused as config is still not real!
            /*if (!File.Exists(location + @"\server-config.json"))
            {
                using (FileStream configStream = File.Create(location + @"\server-config.json"))
                {
                    byte[] data;
                    configStream.Write(new byte[] { 0x7B, 0x0A, 0x09 }); // { + \n + \LF
                    data = new System.Text.UTF8Encoding(true).GetBytes("\"server-ip\": \"127.0.0.1\"");
                    configStream.Write(data);
                    configStream.Write(new byte[] { 0x2C, 0x0A, 0x09 }); // , + \n + \LF
                    data = new System.Text.UTF8Encoding(true).GetBytes("\"server-port\": 42896");
                    configStream.Write(data);
                    configStream.Write(new byte[] { 0x0A, 0x7D}); // , + \n + \LF
                }
                Logger.DebugServer("Created config.json");
            }*/
        }

        /// <summary>
        /// Creates file at the speciifed location, writes "[]" to it, then closes.
        /// </summary>
        static void CreateFile(string filename)
        {
            if (File.Exists(filename))
            {
                Logger.Warn("[Main] [WARN] Attempted to call method but file already exists");
                return;
            }
            using (FileStream fileStream = File.Create(filename))
            {
                fileStream.Write(new byte[] { 0x5B, 0X5D });
            }
            Logger.Success("[Main] [GOOD] Created file: " + filename);
        }//*/
    }
}