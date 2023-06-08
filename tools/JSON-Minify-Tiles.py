import os
import json
print("JSON Minifier - Tiles")

# JSON Arrays
old_Tiles = []
ret_TileData = []

# Lazy Search -- Change 'search' to easily change file to search for.
search = "_tiles.txt"
if not os.path.exists(search):
    print(f"Could not find file '{search}'. Please place it into the folder where you are running this script.")
    os.system("pause")
    exit(-1)
with open(search, 'r') as readData:
    old_Tiles = json.load(readData)

# Tries to get an entry
def GetEntry(SearchKey, Node, Append):
    if Node.get(SearchKey):
        Append[SearchKey] = Node.get(SearchKey)

# Adding in new JSON object into array
entryCount = 0 # Lazy way to track amount of entries
for entry in old_Tiles:
    entryCount += 1
    NewEntryObject = {}
    GetEntry("tileID", entry, NewEntryObject)
    GetEntry("imgName", entry, NewEntryObject)
    GetEntry("footType", entry, NewEntryObject)
    GetEntry("walkable", entry, NewEntryObject)
    ret_TileData.append(NewEntryObject)
   
with open("output-tiles.json", "w") as outputjson:
    outputjson.write(json.dumps(ret_TileData, indent=2))
print("Finished copying desired keys. Please see 'output-tiles.json'")
print(f"Amount of Entries: {entryCount}")
os.system("pause")
