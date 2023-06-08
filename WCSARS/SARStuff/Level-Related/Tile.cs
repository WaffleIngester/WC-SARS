using SimpleJSON;
using System.IO;
using WCSARS; // logging purposes

namespace SARStuff
{
    internal class Tile
    {
        // Every Tiles that can exist
        public static Tile[] AllTiles { get; private set; }

        // ID of this Tile
        public readonly int TileID;

        // Name of this Tile
        //public readonly string Name;

        // This Tile's position in the Overworld.
        //public readonly Vector2 Position; // actual_position = pos_in_map_data * 9 << v0.90.2 at least

        // Whether this is a walkable Tile
        public readonly bool Walkable;

        public Tile(int id, bool walkable)
        {
            TileID = id;
            Walkable = walkable;
        }

        public static Tile[] GetAllTiles()
        {
			if (AllTiles != null) return AllTiles;
            string search = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\datafiles\tiles.json";
            if (!File.Exists(search))
            {
                Logger.Failure("Failed to locate \"tiles.json\"! Defaulting to hard-coded tile list...");
                return BackupTilesLol();
            }
            Tile[] tiles;
            string data = File.ReadAllText(search);
            JSONArray tileData = JSON.Parse(data).AsArray;
			tiles = new Tile[tileData.Count];
			for (int i = 0; i < tiles.Length; i++)
			{
				JSONNode node = tileData[i];
				tiles[i] = new Tile(node["tileID"].AsInt, node["walkable"].AsBool);
			}
			AllTiles = tiles;
            return AllTiles;
        }

        /// <summary>
        /// Attempts to locate a Tile with the provided ID.
        /// </summary>
        /// <param name="id">TileID to search for.</param>
        /// <returns>The located TileType if found; NULL if otherwise.</returns>
        public static Tile GetTileFromID(int id)
        {
            if (AllTiles == null) AllTiles = GetAllTiles();
            for (int i = 0; i < AllTiles.Length; i++)
            {
                if (AllTiles[i].TileID == id)
                {
                    return AllTiles[i];
                }
            }
            return null;
        }

        /// <summary>
        /// v0.90.2 OK | Forces the creation of the known-tile-set for SAR v0.90.2 if the program fails to find the actual data set file (tiles.json).
        /// </summary>
        private static Tile[] BackupTilesLol() // Can only exist because there are so few tiles in v0.90.2
        {
            Logger.Warn("[Tile - BackupTilesLol] [WARN] This tile data is only guaranteed for v0.90.2!");
            Tile[] ret = new Tile[11];
			ret[0] = new Tile(0, true);
			ret[1] = new Tile(1, true);
			ret[2] = new Tile(2, true);
			ret[3] = new Tile(3, true);
			ret[4] = new Tile(6, true);
			ret[5] = new Tile(7, false);
			ret[6] = new Tile(8, true);
			ret[7] = new Tile(9, true);
			ret[8] = new Tile(12, true);
			ret[9] = new Tile(13, true);
			ret[10] = new Tile(20, true);
            return ret;
		}

        /// <summary>
        /// Nulls the static "AllTiles" property so it can collected by the garbage collector... Hopefully.
        /// </summary>
        public static void NullAllTiles()
        {
            //Logger.Warn("[Tile] Nulling AllTiles...");
            AllTiles = null;
            //Logger.Success("[Tile] AllTiles nulled! :]");
        }
    }
}
