using System;
using System.IO;
using System.Net;

namespace WCSARS
{
    internal class ConfigLoader
    {
        // Server
        public string IP = "127.0.0.1";
        public int Port = 42896;
        public string ServerKey = "insertKeyHere";
        public string Gamemode = "solo"; // currently-known strings: "solo", "duo", "squad".
        public bool useConfigSeeds = false;
        public int LootSeed;
        public int CocoSeed;
        public int HampterSeed;

        // Players
        public int MaxPlayers = 64;
        public float LobbyTime = 120f;

        // Objects
        public short MaxMoleCrates = 12;

        // Weapons and stuff
        public int MaxDartTicks = 12;
        public float DartTickRate = 0.6f;
        public int DartPoisonDamage = 9;
        public float SuperSkunkGasTickRate = 1.0f;

        // Healing
        public float HealthPerTick = 4.75f;
        public float DrinkTickRate = 0.5f;
        public float CampfireHealPerTick = 4f;
        public float CampfireRateSeconds = 1f;
        public float CoconutHealAmount = 5f;

        // Downed-Related
        public float BleedoutRateSeconds = 1f;
        public byte ResurrectHP = 25;

        // Bools
        public bool InfiniteMatch = false;
        public bool Safemode = true;
        public bool DebugMode = false; // UNUSED... sort of.. Used in match to use the horrid old Match creator

        public ConfigLoader()
        {
            // Find Location
            string baseloc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string loc = baseloc + @"\server-config.txt";

            // Try Loading
            LoadConfigFile(loc);
        }

        /// <summary>
        /// Attempts to load the config file at the specified location.
        /// </summary>
        /// <param name="location">Location of config file.</param>
        private void LoadConfigFile(string location)
        {
            // Create Config if not real
            if (!File.Exists(location))
            {
                Logger.Failure("Config did NOT exist! Creating!!");
                CreateDefaultConfig(location);
                return;
            }
            // Ok config is real, can steal file now
            string[] readLines = File.ReadAllLines(location);
            int errors = 0;
            foreach(string value in readLines)
            {
                string[] splits = value.Split("=", 2);
                //Logger.Warn($"Splits count: {splits.Length}");
                //Logger.DebugServer($"Name: {splits[0]}; Key: {splits[1]}");
                try
                {
                    switch (splits[0])
                    {
                        // General Server stuff
                        case "server-ip":
                            if (IPEndPoint.Parse(splits[1]) != null) IP = splits[1];
                            else Logger.DebugServer("Couldn't parse IP");
                            break;
                        case "server-port":
                            Port = int.Parse(splits[1]);
                            break;
                        case "server-key":
                            ServerKey = splits[1];
                            break;
                        case "gamemode":
                            if (splits[1] == "solo" || splits[1] == "duo" || splits[1] == "squad") Gamemode = splits[1];
                            else
                            {
                                Logger.Warn("[ConfigLoader] Invalid gamemode: " + splits[1]);
                                errors++;
                                Gamemode = "solo";
                            }
                            break;
                        case "use-config-seeds":
                            useConfigSeeds = bool.Parse(splits[1]);
                            break;
                        case "seed-loot":
                            LootSeed = int.Parse(splits[1]);
                            break;
                        case "seed-coconuts":
                            CocoSeed = int.Parse(splits[1]);
                            break;
                        case "seed-hamsterballs":
                            HampterSeed = int.Parse(splits[1]);
                            break;
                        case "max-players":
                            MaxPlayers = int.Parse(splits[1]);
                            break;
                        case "lobby-time":
                            LobbyTime = float.Parse(splits[1]);
                            break;
                        // Other match
                        case "molecrates-max":
                            MaxMoleCrates = short.Parse(splits[1]);
                            break;
                        case "skunkgas-tickrate-seconds":
                            SuperSkunkGasTickRate = float.Parse(splits[1]);
                            break;
                        //Dartgun
                        case "dart-poisondmg":
                            DartPoisonDamage = int.Parse(splits[1]);
                            break;
                        case "dart-ticks-max":
                            MaxDartTicks = int.Parse(splits[1]);
                            break;
                        case "dart-tickrate":
                            DartTickRate = float.Parse(splits[1]);
                            break;
                        // Health
                        case "heal-per-tick":
                            HealthPerTick = float.Parse(splits[1]);
                            break;
                        case "drink-rate":
                            DrinkTickRate = float.Parse(splits[1]);
                            break;
                        case "campfire-heal-hp":
                            CampfireHealPerTick = float.Parse(splits[1]);
                            break;
                        case "campfire-heal-rate":
                            CampfireRateSeconds = float.Parse(splits[1]);
                            break;
                        case "coconut-heal-base":
                            CoconutHealAmount = float.Parse(splits[1]);
                            break;
                        case "downed-bleedout-rate":
                            BleedoutRateSeconds = float.Parse(splits[1]);
                            break;
                        case "downed-resurrect-hp":
                            ResurrectHP = byte.Parse(splits[1]);
                            break;
                        case "infinite-match":
                            InfiniteMatch = bool.Parse(splits[1]);
                            break;
                        case "safemode":
                            Safemode = bool.Parse(splits[1]);
                            break;
                        case "debugmode":
                            DebugMode = bool.Parse(splits[1]);
                            break;
                        default:
                            Logger.DebugServer($"Unhandled entry \"{splits[0]}\"");
                            errors++;
                            break;
                    }
                } catch (Exception except)
                {
                    Logger.DebugServer($"Error whilst parsing...\n{except}");
                    errors++;
                }
            }
            if (errors > 0)
            {
                Logger.DebugServer($"[ConfigLoader] Encountered {errors} errros while reaidng config.");
                Logger.DebugServer("[ConfigLoader] Going to run CreateDefaultConfig() once more to remake/dump keys");
                CreateDefaultConfig(location);
            }
        }

        /// <summary>
        /// Creates a default config file at the specified location.
        /// </summary>
        public void CreateDefaultConfig(string location)
        {
            using (StreamWriter streamWriter = File.CreateText(location))
            {
                //streamWriter.WriteLine($"={}");

                streamWriter.WriteLine($"server-ip={IP}");
                streamWriter.WriteLine($"server-port={Port}");
                streamWriter.WriteLine($"server-key={ServerKey}");
                streamWriter.WriteLine($"gamemode={Gamemode}");
                streamWriter.WriteLine($"use-config-seeds={useConfigSeeds}");
                streamWriter.WriteLine($"seed-loot={LootSeed}");
                streamWriter.WriteLine($"seed-coconuts={CocoSeed}");
                streamWriter.WriteLine($"seed-hamsterballs={HampterSeed}");
                streamWriter.WriteLine($"max-players={MaxPlayers}");

                streamWriter.WriteLine($"lobby-time={LobbyTime}");
                streamWriter.WriteLine($"molecrates-max={MaxMoleCrates}");

                streamWriter.WriteLine($"dart-ticks-max={MaxDartTicks}");
                streamWriter.WriteLine($"dart-tickrate={DartTickRate}");
                streamWriter.WriteLine($"dart-poisondmg={DartPoisonDamage}");
                streamWriter.WriteLine($"skunkgas-tickrate-seconds={SuperSkunkGasTickRate}");

                streamWriter.WriteLine($"heal-per-tick={HealthPerTick}");
                streamWriter.WriteLine($"drink-rate={DrinkTickRate}");
                streamWriter.WriteLine($"campfire-heal-hp={CampfireHealPerTick}");
                streamWriter.WriteLine($"campfire-heal-rate={CampfireRateSeconds}");
                streamWriter.WriteLine($"coconut-heal-base={CoconutHealAmount}");
                streamWriter.WriteLine($"downed-bleedout-rate={BleedoutRateSeconds}");
                streamWriter.WriteLine($"downed-resurrect-hp={ResurrectHP}");

                // "debug" / other
                streamWriter.WriteLine($"infinite-match={InfiniteMatch}");
                streamWriter.WriteLine($"safemode={Safemode}");
                streamWriter.WriteLine($"debugmode={DebugMode}");
            }
            Logger.DebugServer("[ConfigLoader] Created config file!");
        }
    }
}
