using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Collections.Generic;
using SARStuff;

namespace WCSARS.Configuration
{
    internal class Config
    {
        #region constants
        // self
        private const string kconfig_file_name = "server-config.txt";

        // server
        private const string kserver_ip = "server-ip";
        private const string kserver_port = "server-port";
        private const string kserver_client_key = "server-client-key"; // string - "no-key-set"
        private const string kserver_mode = "server-game-mode";
        private const string kserver_max_players = "server-maximum-players";

        // lobby
        private const string klobby_duration = "lobby-duration-seconds";
        private const string klobby_players_needed_to_reduce = "lobby-players-until-reduce-wait";

        // rng
        private const string krng_loot = "rng-seed-loot";
        private const string krng_coconuts = "rng-seed-coconuts";
        private const string krng_hamsterballs = "rng-seed-hamsterballs";
        private const string krng_emus = "rng-seed-emus";
        private const string krng_mushrooms = "rng-seed-mushrooms";
        private const string krng_crabs = "rng-seed-crabs";
        private const string krng_clams = "rng-seed-clams";

        // ddg
        private const string kdartgun_max_healStack = "dartgun-tick-maximum-heal-ticks";
        private const string kdartgun_max_damageStack = "dartgun-tick-maximum-damage-ticks";
        private const string kdartgun_tickrate = "dartgun-tickrate-seconds";
        private const string kdartgun_damage = "dartgun-tick-damage";

        // health-like
        private const string kjuice_hp_per_tick = "juice-hp-per-tick";
        private const string kjuice_drink_rate = "juice-drink-rate-seconds";
        private const string kdowned_hp_on_revive = "downed-revival-hp";
        private const string kdowned_bleedout_rate = "downed-bleedout-rate-seconds";

        // campfire
        private const string kcampfire_hp_per_tick = "campfire-heal-per-tick";
        private const string kcampfire_tickrate = "campfire-heal-rate-seconds";

        // coconuts/ edibles?
        private const string kcoconut_hp = "coconuts-heal-hp";

        // ssg
        private const string kskunkgas_disabled = "super-skunk-gas-disabled";
        private const string kskunkgas__tickrate = "super-skunk-gas-damage-rate-seconds";

        // "fun"
        //private const string KEY_fun_emoteIgnore = "";
        private const string kfun_force_rng_seeds = "fun-force-config-seeds";
        private const string kfun_safemode_disabled = "fun-disable-major-safety-checks";
        private const string kfun_winchecks_disabled = "fun-disable-win-checks";
        #endregion constants

        #region public fields
        #region server
        public IPAddress ServerIP = IPAddress.Parse("127.0.0.1"); // IP address to bind to
        public int ServerPort = 42896; // port to bind to
        public string ServerKey = "no-key-set"; // key which clients should send in order to be able to connect to the server
        public string ServerGamemode = SARConstants.GamemodeSolos; // mode the server is being played on.
        public int ServerMaxPlayers = 64; // maximum number of players who can join the server
        #endregion server

        #region lobby
        public float LobbyDurationSeconds = 120f;
        public int LobbyPlayersUntilReduceWait = 32;
        #endregion lobby

        #region rng
        public uint SeedLoot = 0;
        public uint SeedHamsterballs = 0;
        public uint SeedCoconuts = 0;
        public uint SeedEmus = 0;
        public uint SeedMushrooms = 0;
        public uint SeedCrabs = 0;
        public uint SeedClams = 0;
        #endregion rng

        #region dartgun
        public int DartgunTickDamage = 9;
        public int DartgunTickMaxDamageStacks = 12;
        public int DartgunTickMaxHealStacks = 12;
        public float DartgunTickRateSeconds = 0.6f;
        #endregion dartgun

        #region health-related
        public byte JuiceHpPerTick = (byte)4.75f;
        public float JuiceDrinkRateSeconds = 0.5f;
        public float DownedBleedoutRateSeconds = 1.0f;
        public byte DownedRessurectHP = 25;
        public byte CoconutHealHP = 5;
        #endregion health-related

        #region campfires
        public byte CampfireHpPerTick = 4;
        public float CampfireTickRateSeconds = 1.0f;
        #endregion campfires

        #region super skunk gas
        public bool SkunkGasDisabled = false;
        public float SkunkGasTickRateSeconds = 1.0f;
        #endregion super skunk gas

        #region "fun"
        public bool FunDisableSafetyChecks = false; // togglable safety checks are often on volitile functions
        public bool FunDisableWinChecks = false;
        public bool FunForceConfigSeeds = false;
        #endregion "fun"
        #endregion public fields

        private int _errors = 0;
        private Dictionary<string, string> _lazyConfigDict = null;

        public Config(string file_loc = "")
        {
            // load from file
            if (file_loc == "")
                file_loc = AppDomain.CurrentDomain.BaseDirectory + kconfig_file_name;

            if (!File.Exists(file_loc))
            {
                Logger.Warn("[Config] [Warn] Could not locate config file. Attempting to create it!");
                ExportConfigToFile(file_loc);
                Logger.Success($"[Config] [OK] Made config file @ \"{file_loc}\" successfully.");
                return;
            }
            _lazyConfigDict = OpenFileAsDictionaryPair(file_loc);

            // chaos
            string fart; // so we don't type "string x" like I was doing earlier LIKE AN IDIOT

            #region more code that'd get me fired at a real job
            #region server chaos
            if (KeyExists(kserver_ip, out fart))
            {
                if (InterpretAsIPAddress(fart, out IPAddress ip))
                    ServerIP = ip;
            }

            if (KeyExists(kserver_port, out fart))
            {
                if (InterpretAsInt(fart, out int port))
                    ServerPort = port;
            }

            if (KeyExists(kserver_client_key, out fart))
                ServerKey = fart;

            if (KeyExists(kserver_mode, out fart))
            {
                string mode = fart.ToLower();
                if ((mode != SARConstants.GamemodeSolos) &&
                    (mode != SARConstants.GamemodeDuos) &&
                    (mode != SARConstants.GamemodeSquad))
                    _errors += 1; // class initalized with solo
                else
                    ServerGamemode = mode;
            }

            if (KeyExists(kserver_max_players, out fart))
            {
                if (InterpretAsInt(fart, out int max_players))
                    ServerMaxPlayers = max_players;
            }
            #endregion server chaos

            #region rng chaos
            // yes, we convert from strings to ints, then from ints to uints. yes, it's dumb
            // also didn't use the local "fart" variable here to keep what was lost elsewhere
            if (KeyExists(krng_loot, out string str_rngseed_loot))
            {
                if (InterpretAsInt(str_rngseed_loot, out int rngseed_loot))
                    SeedLoot = (uint)rngseed_loot;
            }

            if (KeyExists(krng_coconuts, out string str_rngseed_coconuts))
            {
                if (InterpretAsInt(str_rngseed_coconuts, out int rngseed_coconuts))
                    SeedCoconuts = (uint)rngseed_coconuts;
            }

            if (KeyExists(krng_hamsterballs, out string str_rngseed_hamsterball))
            {
                if (InterpretAsInt(str_rngseed_hamsterball, out int rngseed_hamsterball))
                    SeedHamsterballs = (uint)rngseed_hamsterball;
            }

            if (KeyExists(krng_emus, out string str_rngseed_emu))
            {
                if (InterpretAsInt(str_rngseed_emu, out int rngseed_emu))
                    SeedEmus = (uint)rngseed_emu;
            }

            if (KeyExists(krng_mushrooms, out string str_rngseed_mushrooms))
            {
                if (InterpretAsInt(str_rngseed_mushrooms, out int rngseed_mushrooms))
                    SeedMushrooms = (uint)rngseed_mushrooms;
            }

            if (KeyExists(krng_crabs, out string str_rngseed_crabs))
            {
                if (InterpretAsInt(str_rngseed_crabs, out int rngseed_crabs))
                    SeedCrabs = (uint)rngseed_crabs;
            }

            if (KeyExists(krng_clams, out string str_rngseed_clam))
            {
                if (InterpretAsInt(str_rngseed_clam, out int rngseed_clam))
                    SeedClams = (uint)rngseed_clam;
            }
            #endregion rng chaos

            #region lobby chaos
            if (KeyExists(klobby_duration, out fart))
            {
                if (InterpretAsFloat(fart, out float lobby_duration))
                    LobbyDurationSeconds = lobby_duration;
            }

            if (KeyExists(klobby_players_needed_to_reduce, out fart))
            {
                if (InterpretAsInt(fart, out int lobb_amount_to_reduce))
                    LobbyPlayersUntilReduceWait = lobb_amount_to_reduce;
            }
            #endregion lobby chaos

            #region dartgun chaos
            if (KeyExists(kdartgun_tickrate, out fart))
            {
                if (InterpretAsFloat(fart, out float dartgun_tickrate))
                    DartgunTickRateSeconds = dartgun_tickrate;
            }

            if (KeyExists(kdartgun_damage, out fart))
            {
                if (InterpretAsInt(fart, out int dartgun_damage))
                    DartgunTickDamage = dartgun_damage;
            }

            if (KeyExists(kdartgun_max_damageStack, out fart))
            {
                if (InterpretAsInt(fart, out int dartgun_max_damage_stack))
                    DartgunTickMaxDamageStacks = dartgun_max_damage_stack;
            }

            if (KeyExists(kdartgun_max_healStack, out fart))
            {
                if (InterpretAsInt(fart, out int dartgun_max_heal_stack))
                    DartgunTickMaxHealStacks = dartgun_max_heal_stack;
            }

            #endregion dartgun chaos

            #region health chaos
            if (KeyExists(kjuice_hp_per_tick, out fart))
            {
                if (InterpretAsByte(fart, out byte healHP))
                    JuiceHpPerTick = healHP;
            }

            if (KeyExists(kjuice_drink_rate, out fart))
            {
                if (InterpretAsFloat(fart, out float heal_rate))
                    JuiceDrinkRateSeconds = heal_rate;
            }

            if (KeyExists(kdowned_bleedout_rate, out fart))
            {
                if (InterpretAsFloat(fart, out float bleedoutrate))
                    DownedBleedoutRateSeconds = bleedoutrate;
            }

            if (KeyExists(kdowned_hp_on_revive, out fart))
            {
                if (InterpretAsByte(fart, out byte revival_hp))
                    DownedRessurectHP = revival_hp;
            }

            if (KeyExists(kcoconut_hp, out fart))
            {
                if (InterpretAsByte(fart, out byte coconut_hp))
                    CoconutHealHP = coconut_hp;
            }
            #endregion health chaos

            #region campfire chaos
            if (KeyExists(kcampfire_hp_per_tick, out fart))
            {
                if (InterpretAsByte(fart, out byte campfire_hp))
                    CampfireHpPerTick = campfire_hp;
            }
            if (KeyExists(kcampfire_tickrate, out fart))
            {
                if (InterpretAsFloat(fart, out float campfire_rate_seconds))
                    CampfireTickRateSeconds = campfire_rate_seconds;
            }
            #endregion campfire chaos

            #region super skunk gas choas
            if (KeyExists(kskunkgas_disabled, out fart))
            {
                if (InterpretAsBool(fart, out bool isGasDisabled))
                    SkunkGasDisabled = isGasDisabled;
            }

            if (KeyExists(kskunkgas__tickrate, out fart))
            {
                if (InterpretAsFloat(fart, out float gas_tickrate))
                    SkunkGasTickRateSeconds = gas_tickrate;
            }
            #endregion region super skunk gas choas

            #region fun chaos
            if (KeyExists(kfun_safemode_disabled, out fart))
            {
                if (InterpretAsBool(fart, out bool isSafemodeDisabled))
                    FunDisableSafetyChecks = isSafemodeDisabled;
            }

            if (KeyExists(kfun_force_rng_seeds, out fart))
            {
                if (InterpretAsBool(fart, out bool doForceConfigSeeds))
                    FunForceConfigSeeds = doForceConfigSeeds;
            }

            if (KeyExists(kfun_winchecks_disabled, out fart))
            {
                if (InterpretAsBool(fart, out bool doDisableWinChecks))
                    FunDisableWinChecks = doDisableWinChecks;
            }
            #endregion fun chaos
            #endregion more code that'd get me fired at a real job

            if (_errors > 0)
            {
                Logger.Warn($"[Config] [Warn] Encountered {_errors} errors. Re-exporting config with successfully-loaded values.");
                ExportConfigToFile(file_loc);
            }

            _lazyConfigDict = null;
        }

        #region private methods that do most of the work
        private Dictionary<string, string> OpenFileAsDictionaryPair(string fileLocation)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>(26);
            using (StreamReader reader = File.OpenText(fileLocation))
            {
                string line;
                string[] split;
                while ((line = reader.ReadLine()) != null)
                {
                    split = line.Split('=');
                    if (split.Length >= 2)
                    {
                        if (!dict.ContainsKey(split[0]))
                            dict.Add(split[0], split[1]);
                        else
                        {
                            _errors += 1;
                            Logger.Warn($"[Config] [Warn] Key \"{split[0]}\" already exists!");
                        }
                    }
                    else
                    {
                        _errors += 1;
                        Logger.Warn($"[Config] [Warn] LENGTH 0 @ \"{line}\"");
                    }
                }
            }
            return dict;
        }

        private bool KeyExists(string key, out string value)
        {
            if (_lazyConfigDict == null)
                throw new NullReferenceException("Config._lazyConfigDict null when trying to call Config.KeyExists!");

            // don't hate me because I'm stupid. hate me because I'm really stupid.
            if (_lazyConfigDict.ContainsKey(key))
            {
                value = _lazyConfigDict[key];
                return true;
            }
            else
            {
                Logger.Failure($"[Config] [Error] Could not locate key \"{key}\".");
                _errors += 1;
                value = "";
                return false;
            }
        }

        private bool InterpretAsBool(string value, out bool ret)
        {
            try
            {
                ret = bool.Parse(value);
                return true;
            }
            catch
            {
                Logger.Failure($"[Config] [Error] Value \"{value}\" is not a true/ false value.");
                _errors += 1;
                ret = false;
                return false;
            }
        }

        private bool InterpretAsByte(string value, out byte ret)
        {
            try
            {
                ret = byte.Parse(value);
                return true;
            }
            catch
            {
                Logger.Failure($"[Config] [Error] Value \"{value}\" is not convertable to a byte.");
                _errors += 1;
                ret = 0;
                return false;
            }
        }

        private bool InterpretAsFloat(string value, out float ret)
        {
            try
            {
                ret = float.Parse(value);
                return true;
            }
            catch
            {
                Logger.Failure($"[Config] [Error] Value \"{value}\" is not convertable to a float.");
                _errors += 1;
                ret = 0.0f;
                return false;
            }
        }

        private bool InterpretAsInt(string value, out int ret)
        {
            try
            {
                ret = int.Parse(value);
                return true;
            }
            catch
            {
                Logger.Failure($"[Config] [Error] Value \"{value}\" is not convertable to an integer.");
                _errors += 1;
                ret = 0;
                return false;
            }
        }

        private bool InterpretAsIPAddress(string value, out IPAddress ret)
        {
            try
            {
                ret = IPAddress.Parse(value);
                return true;
            }
            catch
            {
                Logger.Failure($"[Config] [Error] Value \"{value}\" is not a valid IP address.");
                ret = null;
                _errors += 1;
                return false;
            }
        }
        private void ExportConfigToFile(string fileLocation)
        {
            try
            {
                using (StreamWriter sw = File.CreateText(fileLocation))
                {
                    // this is where we start to get stupid
                    sw.WriteLine($"{kserver_ip}={ServerIP}");
                    sw.WriteLine($"{kserver_port}={ServerPort}");
                    sw.WriteLine($"{kserver_client_key}={ServerKey}");
                    sw.WriteLine($"{kserver_mode}={ServerGamemode}");
                    sw.WriteLine($"{kserver_max_players}={ServerMaxPlayers}");

                    sw.WriteLine($"{klobby_duration}={LobbyDurationSeconds}");
                    sw.WriteLine($"{klobby_players_needed_to_reduce}={LobbyPlayersUntilReduceWait}");

                    sw.WriteLine($"{krng_loot}={SeedLoot}");
                    sw.WriteLine($"{krng_hamsterballs}={SeedHamsterballs}");
                    sw.WriteLine($"{krng_coconuts}={SeedCoconuts}");
                    sw.WriteLine($"{krng_emus}={SeedEmus}");
                    sw.WriteLine($"{krng_mushrooms}={SeedMushrooms}");
                    sw.WriteLine($"{krng_crabs}={SeedCrabs}");
                    sw.WriteLine($"{krng_clams}={SeedClams}");

                    sw.WriteLine($"{kdartgun_damage}={DartgunTickDamage}");
                    sw.WriteLine($"{kdartgun_max_damageStack}={DartgunTickMaxDamageStacks}");
                    sw.WriteLine($"{kdartgun_max_healStack}={DartgunTickMaxHealStacks}");
                    sw.WriteLine($"{kdartgun_tickrate}={DartgunTickRateSeconds}");

                    sw.WriteLine($"{kjuice_hp_per_tick}={JuiceHpPerTick}");
                    sw.WriteLine($"{kjuice_drink_rate}={JuiceDrinkRateSeconds}");
                    sw.WriteLine($"{kdowned_bleedout_rate}={DownedBleedoutRateSeconds}");
                    sw.WriteLine($"{kdowned_hp_on_revive}={DownedRessurectHP}");
                    sw.WriteLine($"{kcoconut_hp}={CoconutHealHP}");

                    sw.WriteLine($"{kcampfire_hp_per_tick}={CampfireHpPerTick}");
                    sw.WriteLine($"{kcampfire_tickrate}={CampfireTickRateSeconds}");

                    sw.WriteLine($"{kskunkgas_disabled}={SkunkGasDisabled}");
                    sw.WriteLine($"{kskunkgas__tickrate}={SkunkGasTickRateSeconds}");

                    sw.WriteLine($"{kfun_force_rng_seeds}={FunForceConfigSeeds}");
                    sw.WriteLine($"{kfun_safemode_disabled}={FunDisableSafetyChecks}");
                    sw.WriteLine($"{kfun_winchecks_disabled}={FunDisableWinChecks}");
                    //sw.Write($"{}={}");
                }
            }
            catch (Exception ex) // previously handled other anticipated exceptions, but honestly we shouldn't be getting one in the first place.
            {
                Logger.Failure($"[Config] [Error] {ex}");
            }
        }
        #endregion private methods that do most of the work

        public void LazyPrint()
        {
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            var all_pub_fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in all_pub_fields)
            {
                Console.WriteLine($"{field.Name}: {field.GetValue(this)}");
            }
            Console.ResetColor();
        }
    }
}