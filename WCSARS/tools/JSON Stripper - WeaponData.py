import os
import json
print("Weapon Data JSON Stripper")

# JSON Arrays
oldWeaponDataArray = []
newWeaponDataArray = []

if not os.path.exists("_weapons.txt"):
    print("Could not find file '_weapons.txt'. Please place it into the folder where you are running this script.")
    os.system("pause")
    exit(-1)
with open('_weapons.txt', 'r') as readData:
    oldWeaponDataArray = json.load(readData)

# Adding in new JSON object into array
entryCount = 0 # To keep track of entries
NewEntryObject = {}

for weapon in oldWeaponDataArray:
    entryCount += 1
    NewEntryObject = {}
    if weapon.get("inventoryID"):
        NewEntryObject["inventoryID"] = weapon.get("inventoryID")

    if weapon.get("weaponClass"):
        NewEntryObject["weaponClass"] = weapon.get("weaponClass")

    if weapon.get("spawnRatioRelativeToOthers"):
        NewEntryObject["spawnRatioRelativeToOthers"] = weapon.get("spawnRatioRelativeToOthers")

    if weapon.get("maxRarity"):
        NewEntryObject["maxRarity"] = weapon.get("maxRarity")

    if weapon.get("damageNormal"):
        NewEntryObject["damageNormal"] = weapon.get("damageNormal")

    if weapon.get("addedDamagePerRarity"):
        NewEntryObject["addedDamagePerRarity"] = weapon.get("addedDamagePerRarity")

    if weapon.get("breaksArmorAmount"):
        NewEntryObject["breaksArmorAmount"] = weapon.get("breaksArmorAmount")

    if weapon.get("overrideBreaksVehicleAmount"):
        NewEntryObject["overrideBreaksVehicleAmount"] = weapon.get("overrideBreaksVehicleAmount")

    if weapon.get("clipSize"):
        NewEntryObject["clipSize"] = weapon.get("clipSize")

    if weapon.get("reloadTime"):
        NewEntryObject["reloadTime"] = weapon.get("reloadTime")

    if weapon.get("ammoID"):
        NewEntryObject["ammoID"] = weapon.get("ammoID")

    if weapon.get("ammoSpawnAmount"):
        NewEntryObject["ammoSpawnAmount"] = weapon.get("ammoSpawnAmount")

    if weapon.get("bulletMoveSpeed"):
        NewEntryObject["bulletMoveSpeed"] = weapon.get("bulletMoveSpeed")

    if weapon.get("bulletMoveSpeedAddedPerRarity"):
        NewEntryObject["bulletMoveSpeedAddedPerRarity"] = weapon.get("bulletMoveSpeedAddedPerRarity")

    if weapon.get("bulletDistanceAtWhichDamageIs0"):
        NewEntryObject["bulletDistanceAtWhichDamageIs0"] = weapon.get("bulletDistanceAtWhichDamageIs0")

    if weapon.get("addedBulletDistanceAtWhichDamageIs0PerRarity"):
        NewEntryObject["addedBulletDistanceAtWhichDamageIs0PerRarity"] = weapon.get("addedBulletDistanceAtWhichDamageIs0PerRarity")

    if weapon.get("grenadeInfo"):
        NewEntryObject["grenadeInfo"] = weapon.get("grenadeInfo")
    newWeaponDataArray.append(NewEntryObject)
with open("output.json", "w") as outputjson:
    outputjson.write(json.dumps(newWeaponDataArray, indent=2))
print("Finished copying desired keys. Please see 'output.json'")
print(f"Amount of Entries: {entryCount}")
os.system("pause")
