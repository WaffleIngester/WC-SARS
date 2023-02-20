using System;
using System.IO;
using SimpleJSON;
using SARStuff;

namespace WCSARS
{
    internal class Weapon
    {
        public WeaponType WeaponType;
        public string Name;
        public short JSONIndex;
        public int ClipSize;
        public byte AmmoType;
        public byte AmmoSpawnAmount;
        public int Damage;
        public int DamageIncrease;
        public byte ArmorDamage;
        public byte VehicleDamageOverride;
        public bool PenetratesArmor; // Reminder that in future versions this was changed from a True/False to %-based values.
        public byte RarityMaxVal;
        public byte RarityMinVal;
        public byte SpawnSizeOverworld;
        public int SpawnFrequency;
        public int MaxCarry;

        public Weapon(JSONNode data, short index)
        {
            JSONIndex = index;
            if (data["inventoryID"]) Name = data["inventoryID"];
            if (data["weaponClass"])
            {
                string classValue = data["weaponClass"];
                switch (classValue)
                {
                    case "Melee":
                        WeaponType = WeaponType.Melee;
                        break;
                    case "Gun":
                        WeaponType = WeaponType.Gun;
                        break;
                    case "Grenade":
                        WeaponType = WeaponType.Throwable;
                        if (data["grenadeInfo"] == null) throw new Exception($"{Name} was found to be a throwable, yet no \"grenadeInfo\" key found.");
                        if (data["grenadeInfo"]["worldSpawnAmount"] == null) throw new Exception($"{Name} contains grenadeInfo, but no key \"worldSpawnAmount\".");
                        if (data["grenadeInfo"]["carryMax"] == null) throw new Exception($"{Name} contains grenadeInfo, but no key \"carryMax\".");
                        SpawnSizeOverworld = (byte)data["grenadeInfo"]["worldSpawnAmount"].AsInt;
                        MaxCarry = data["grenadeInfo"]["carryMax"].AsInt;
                            break;
                    default:
                        throw new Exception($"Invalid class identifier: \"{classValue}\".");
                }
            }
            if (data["minRarity"]) RarityMinVal = (byte)data["minRarity"].AsInt;
            if (data["maxRarity"]) RarityMaxVal = (byte)data["maxRarity"].AsInt;

            if (data["damageNormal"]) Damage = data["damageNormal"].AsInt;
            if (data["addedDamagePerRarity"]) DamageIncrease = data["addedDamagePerRarity"].AsInt;

            if (data["breaksArmorAmount"]) ArmorDamage = (byte)data["breaksArmorAmount"].AsInt;
            if (data["overrideBreaksVehicleAmount"]) VehicleDamageOverride = (byte)data["overrideBreaksVehicleAmount"].AsInt;
            // In v0.90.2, this only applies to Dartgun. Also it is a bool. At some point later, this was changed to a %. Neat!
            if (data["damageThroughArmor"]) PenetratesArmor = data["damageThroughArmor"].AsBool;

            if (data["clipSize"]) ClipSize = data["clipSize"].AsInt;
            if (data["ammoID"]) AmmoType = (byte)data["ammoID"].AsInt;
            if (data["ammoSpawnAmount"]) AmmoSpawnAmount = (byte)data["ammoSpawnAmount"].AsInt;

            if (data["spawnRatioRelativeToOthers"]) SpawnFrequency = data["spawnRatioRelativeToOthers"].AsInt;
        }

        static public Weapon[] GetAllWeaponsList() // Attempts to read weapondata.json @ ProgramLocation\datafiles-- Will crash if any exceptions are thrown
        {
            // Verify file exists...
            string fileLoc = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\datafiles\weapondata.json";
            if (!File.Exists(fileLoc)) throw new FileNotFoundException($"Could not locate weapondata.json!\nSearch location: {fileLoc}");

            // Go load/read JSON, fill out a new Weapon[], then return it back.
            string readData = File.ReadAllText(fileLoc);
            JSONArray json = (JSONArray)JSON.Parse(readData);
            Weapon[] weapons = new Weapon[json.Count];
            for (int i = 0; i < json.Count; i++)
            {
                weapons[i] = new Weapon(json[i], (short)i);
            }
            return weapons;
        }

        /// <summary>
        /// Returns an array of Weapon objects which only contains Weapon with WeaponType.Gun. Calls Weapon.GetAllWeaponsList() to initialize the original array.
        /// </summary>
        public static Weapon[] GetAllGunsList()
        {
            // Get all Weapons then find the amount of entries that are actually guns
            Weapon[] allWeapons = GetAllWeaponsList();
            int entries = 0;
            for (int i = 0; i < allWeapons.Length; i++)
            {
                if (allWeapons[i].WeaponType == WeaponType.Gun) entries++;
            }
            // Make new Guns array and reset entries back to 0 for reuse
            Weapon[] guns = new Weapon[entries];
            entries = 0;
            for (int i = 0; i < allWeapons.Length; i++)
            {
                if (allWeapons[i].WeaponType == WeaponType.Gun)
                {
                    guns[entries] = allWeapons[i];
                    entries++;
                }
            }
            return guns;
        }

        /// <summary>
        /// Uses the provided Weapon[] array to return a new array of Weapon objects which only contains Weapons with WeaponType.Gun.
        /// </summary>
        public static Weapon[] GetAllGunsList(Weapon[] pweapons)
        {
            // Go through all entries in the provided Weapons array and get the count of gun entries
            int entries = 0;
            for (int i = 0; i < pweapons.Length; i++)
            {
                if (pweapons[i].WeaponType == WeaponType.Gun) entries++;
            }
            // Make new Guns array and reset entries back to 0 for reuse
            Weapon[] guns = new Weapon[entries];
            entries = 0;
            for (int i = 0; i < pweapons.Length; i++)
            {
                if (pweapons[i].WeaponType == WeaponType.Gun)
                {
                    guns[entries] = pweapons[i];
                    entries++;
                }
            }
            return guns;
        }
    }
}