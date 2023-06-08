import os
import json
print("JSON Minifier - Decals")

# Note: Due to the manner that the raw-exported _decals.json file is exported, the "" may trip up the parsing.
# in the event parsing is messed up and this script doesn't output the minified decal data, simply re-format the raw _decals data as json then try again

# JSON Arrays
old_Decals = []
ret_DecalData = []

# Lazy Search -- Change 'search' to easily change file to search for.
search = "_decals.txt"
if not os.path.exists(search):
    print(f"Could not find file '{search}'. Please place it into the folder where you are running this script.")
    os.system("pause")
    exit(-1)
with open(search, 'r') as readData:
    old_Decals = json.load(readData)

# Tries to get an entry
def GetEntry(SearchKey, Node, Append):
    if Node.get(SearchKey):
        Append[SearchKey] = Node.get(SearchKey)

# Adding in new JSON object into array
entryCount = 0 # Lazy way to track amount of entries
for entry in old_Decals:
    entryCount += 1
    NewEntryObject = {}
    GetEntry("decalID", entry, NewEntryObject)
    GetEntry("imgName", entry, NewEntryObject)
    GetEntry("walkableRects", entry, NewEntryObject)
    GetEntry("nonWalkableRects", entry, NewEntryObject)
    ret_DecalData.append(NewEntryObject)
   
with open("output-decals.json", "w") as outputjson:
    outputjson.write(json.dumps(ret_DecalData, indent=2))
print("Finished copying desired keys. Please see 'output-decals.json'")
print(f"Amount of Entries: {entryCount}")
os.system("pause")
