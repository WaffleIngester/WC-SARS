import os
import json
print("JSON Minifier - Grass")

# JSON Arrays
old_Grass = []
ret_GrassData = []

# Lazy Search -- Change 'search' to easily change file to search for.
search = "_grass.txt"
if not os.path.exists(search):
    print(f"Could not find file '{search}'. Please place it into the folder where you are running this script.")
    os.system("pause")
    exit(-1)
with open(search, 'r') as readData:
    old_Grass = json.load(readData)

# Tries to get an entry
def GetEntry(SearchKey, Node, Append):
    if Node.get(SearchKey):
        Append[SearchKey] = Node.get(SearchKey)

# Adding in new JSON object into array
entryCount = 0 # Lazy way to track amount of entries
for entry in old_Grass:
    entryCount += 1
    NewEntryObject = {}
    GetEntry("grassID", entry, NewEntryObject)
    GetEntry("imgBaseName", entry, NewEntryObject)
    GetEntry("walkableRects", entry, NewEntryObject)
    GetEntry("variations", entry, NewEntryObject)
    GetEntry("choppable", entry, NewEntryObject)
    GetEntry("rechoppable", entry, NewEntryObject)
    GetEntry("hitboxExtentX", entry, NewEntryObject)
    GetEntry("hitboxExtentY", entry, NewEntryObject)
    ret_GrassData.append(NewEntryObject)
   
with open("output-grass.json", "w") as outputjson:
    outputjson.write(json.dumps(ret_GrassData, indent=2))
print("Finished copying desired keys. Please see 'output-Grass.json'")
print(f"Amount of Entries: {entryCount}")
os.system("pause")
