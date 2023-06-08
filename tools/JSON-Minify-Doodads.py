import os
import json
print("JSON Minifier - Doodads")

# JSON Arrays
old_Doodads = []
ret_DoodadData = []

# Lazy Search -- Change 'search' to easily change file to search for.
search = "_doodads.txt"
if not os.path.exists(search):
    print(f"Could not find file '{search}'. Please place it into the folder where you are running this script.")
    os.system("pause")
    exit(-1)
with open(search, 'r') as readData:
    old_Doodads = json.load(readData)

# Tries to get an entry
def GetEntry(SearchKey, Node, Append):
    if Node.get(SearchKey):
        Append[SearchKey] = Node.get(SearchKey)

# Adding in new JSON object into array
entryCount = 0 # Lazy way to track amount of entries
for entry in old_Doodads:
    entryCount += 1
    NewEntryObject = {}
    GetEntry("doodadID", entry, NewEntryObject)
    GetEntry("imgName", entry, NewEntryObject)
    GetEntry("moveCollisionPts", entry, NewEntryObject)
    GetEntry("moveAndSightCollisionPts", entry, NewEntryObject)
    GetEntry("shadowPts", entry, NewEntryObject)
    GetEntry("collisionHeight", entry, NewEntryObject)
    GetEntry("destructible", entry, NewEntryObject)
    GetEntry("destructibleDamagePeak", entry, NewEntryObject)
    GetEntry("destructibleDamageRadius", entry, NewEntryObject)
    GetEntry("destructibleCanDropLoot", entry, NewEntryObject)
    ret_DoodadData.append(NewEntryObject)
   
with open("output-doodads.json", "w") as outputjson:
    outputjson.write(json.dumps(ret_DoodadData, indent=2))
print("Finished copying desired keys. Please see 'output-doodads.json'")
print(f"Amount of Entries: {entryCount}")
os.system("pause")
