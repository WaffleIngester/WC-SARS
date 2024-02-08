using System;
using System.IO;
using SimpleJSON;
using System.Collections.Generic;
using WCSARS; // logging purposes

namespace SARStuff
{
    public class Weapon
    {
        public static Weapon[] AllWeapons { get; private set; }
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
        public float Radius;
        public readonly short BulletMoveSpeed;
        public readonly byte BulletMoveSpeedIncPerRarity;
        public readonly short BulletMaxDistanceBase;
        public readonly byte BulletMaxDistanceIncPerRarity;

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
                        if (data["grenadeInfo"]["damageRadius"]) Radius = data["grenadeInfo"]["damageRadius"].AsFloat;
                        if (data["grenadeInfo"]["isTrap"] == true)
                        {
                            Radius = data["grenadeInfo"]["trapRadius"].AsFloat;
                            if (data["grenadeInfo"]["trapDamagePerTick"]) Damage = data["grenadeInfo"]["trapDamagePerTick"].AsInt;
                            //if (data["grenadeInfo"]["isTrap"] == true)
                        }
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


            // bulletMoveSpeed
            if (data["bulletMoveSpeed"])
                BulletMoveSpeed = (short)data["bulletMoveSpeed"].AsInt;

            // bulletMoveSpeedAddedPerRarity
            if (data["bulletMoveSpeedAddedPerRarity"])
                BulletMoveSpeedIncPerRarity = (byte)data["bulletMoveSpeedAddedPerRarity"].AsInt;

            // bulletDistanceAtWhichDamageIs0
            if (data["bulletDistanceAtWhichDamageIs0"])
                BulletMaxDistanceBase = (short)data["bulletDistanceAtWhichDamageIs0"].AsInt;

            // addedBulletDistanceAtWhichDamageIs0PerRarity
            if (data["addedBulletDistanceAtWhichDamageIs0PerRarity"])
                BulletMaxDistanceIncPerRarity = (byte)data["addedBulletDistanceAtWhichDamageIs0PerRarity"].AsInt;
        }

        public static Weapon[] GetAllWeaponTypes()
        {
            if (AllWeapons != null) return AllWeapons;
            string search = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\datafiles\weapons.json";
            if (!File.Exists(search))
            {
                Logger.Failure($"Failed to locate \"weapons.json\"!\nSearched: {search}");
                Environment.Exit(23); // 20 = tiles; 21 = decals; 22 = doodads; 23 = weapons ([somewhat] goes in order of how they should be loaded)
            }
            string data = File.ReadAllText(search);
            JSONArray weaponData = JSON.Parse(data).AsArray;
            AllWeapons = new Weapon[weaponData.Count];
            for (int i = 0; i < AllWeapons.Length; i++)
            {
                AllWeapons[i] = new Weapon(weaponData[i], (short)i);
            }
            return AllWeapons;
        }

        /// <summary>
        /// Attempts to locate a Weapon using the provided WeaponID
        /// </summary>
        /// <param name="pWeaponID">JSONIndex of the Weapon to search for.</param>
        /// <returns>Returns the found Weapon; NULL if otherwise.</returns>
        public static Weapon GetWeaponFromID(int pWeaponID)
        {
            if (AllWeapons == null) GetAllWeaponTypes();
            for (int i = 0; i < AllWeapons.Length; i++)
            {
                if (AllWeapons[i].JSONIndex == pWeaponID) return AllWeapons[i];
            }
            return null;
        }

        /// <summary>
        /// Uses the provided Weapon[] array to return a new array of Weapon objects which only contains Weapons with WeaponType.Gun.
        /// </summary>
        public static Weapon[] GetAllGuns(Weapon[] pWeapons)
        {
            List<Weapon> retGuns = new List<Weapon>(AllWeapons.Length);
            for (int i = 0; i < pWeapons.Length; i++)
            {
                if (pWeapons[i].WeaponType == WeaponType.Gun) retGuns.Add(AllWeapons[i]);
            }
            return retGuns.ToArray();
        }

        public static Weapon[] GetAllGuns()
        {
            if (AllWeapons == null) AllWeapons = GetAllWeaponTypes();
            List<Weapon> retGuns = new List<Weapon>(AllWeapons.Length);

            for (int i = 0; i < AllWeapons.Length; i++)
            {
                if (AllWeapons[i].WeaponType == WeaponType.Gun) retGuns.Add(AllWeapons[i]);
            }
            //NullAllWeaponsList();
            return retGuns.ToArray();
        }

        public static Weapon[] GetAllThrowables()
        {
            if (AllWeapons == null) AllWeapons = GetAllWeaponTypes();
            List<Weapon> ret = new List<Weapon>(AllWeapons.Length);
            for (int i = 0; i < AllWeapons.Length; i++)
            {
                if (AllWeapons[i].WeaponType == WeaponType.Throwable) ret.Add(AllWeapons[i]);
            }
            //NullAllWeaponsList();
            return ret.ToArray();
        }

        /// <summary>
        /// Nulls the static "AllWeapons" property so it can collected by the garbage collector... Hopefully.
        /// </summary>
        public static void NullAllWeaponsList()
        {
            Logger.Warn("[Weapon] Nulling AllWeapons...");
            AllWeapons = null;
            Logger.Success("[Weapon] AllWeapons nulled! :]");
        }
    }
}