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
        }
    }
}