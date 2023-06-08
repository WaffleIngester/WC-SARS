import os
import json
print("JSON Minifier - Weapons")

# JSON Arrays
old_Weapons = []
ret_WeaponData = []

# Lazy Search -- Change 'search' to easily change file to search for.
search = "_weapons.txt"
if not os.path.exists(search):
    print(f"Could not find file '{search}'. Please place it into the folder where you are running this script.")
    os.system("pause")
    exit(-1)
with open(search, 'r') as readData:
    old_Weapons = json.load(readData)

# Tries to get an entry
def GetEntry(SearchKey, Node, Append):
    if Node.get(SearchKey):
        Append[SearchKey] = Node.get(SearchKey)

# Adding in new JSON object into array
entryCount = 0 # Lazy way to track amount of entries
for entry in old_Weapons:
    entryCount += 1
    NewEntryObject = {}
    GetEntry("inventoryID", entry, NewEntryObject)
    GetEntry("weaponClass", entry, NewEntryObject)
    GetEntry("spawnRatioRelativeToOthers", entry, NewEntryObject)
    GetEntry("minRarity", entry, NewEntryObject)
    GetEntry("maxRarity", entry, NewEntryObject)
    GetEntry("damageNormal", entry, NewEntryObject)
    GetEntry("addedDamagePerRarity", entry, NewEntryObject)
    GetEntry("breaksArmorAmount", entry, NewEntryObject)
    GetEntry("overrideBreaksVehicleAmount", entry, NewEntryObject)
    GetEntry("clipSize", entry, NewEntryObject)
    GetEntry("reloadTime", entry, NewEntryObject)
    GetEntry("ammoID", entry, NewEntryObject)
    GetEntry("ammoSpawnAmount", entry, NewEntryObject)
    GetEntry("bulletMoveSpeed", entry, NewEntryObject)
    GetEntry("bulletMoveSpeedAddedPerRarity", entry, NewEntryObject)
    GetEntry("bulletDistanceAtWhichDamageIs0", entry, NewEntryObject)
    GetEntry("grenadeInfo", entry, NewEntryObject)
    ret_WeaponData.append(NewEntryObject)
   
with open("output-weapons.json", "w") as outputjson:
    outputjson.write(json.dumps(ret_WeaponData, indent=2))
print("Finished copying desired keys. Please see 'output-weapons.json'")
print(f"Amount of Entries: {entryCount}")
os.system("pause")
