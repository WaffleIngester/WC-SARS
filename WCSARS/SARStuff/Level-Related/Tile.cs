using SimpleJSON;
using System;
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
        //public readonly string Name; // not really needed...

        // This Tile's position in the Overworld.
        //public readonly Vector2 Position; // actPosition = madDataPosition * 9 << v0.90.2

        // Whether this is a walkable Tile
        public readonly bool Walkable;

        public Tile(int id, bool walkable)
        {
            TileID = id;
            Walkable = walkable;
        }

        /// <summary>
        ///  Loads all parsable tiles from "_tiles.json" into memory as "Tile" objects.
        /// </summary>
        /// <param name="pForceGeneration"> Whether to force the reloading of Tile.AllTiles if is not Null.</param>
        /// <returns> All Tile(s) generated from "_tiles.json" OR Tile.AllTiles if it is not Null.</returns>
        public static Tile[] GetAllTiles(bool pForceGeneration = false)
        {
			if ((AllTiles != null) && !pForceGeneration)
                return AllTiles;

            string search = AppDomain.CurrentDomain.BaseDirectory + @"datafiles\tiles.json";
            if (!File.Exists(search))
            {
                Logger.Failure("Failed to locate \"tiles.json\"! Defaulting to hard-coded tile list...");
                return BackupTilesLol();
            }

            string fileText = File.ReadAllText(search);
            JSONArray tileData = JSON.Parse(fileText).AsArray;

            Tile[] tiles = new Tile[tileData.Count];
			for (int i = 0; i < tiles.Length; i++)
			{
				JSONNode node = tileData[i];
				tiles[i] = new Tile(node["tileID"].AsInt, node["walkable"].AsBool);
			}
			AllTiles = tiles;
            return AllTiles;
        }

        /// <summary>
        ///  Attempts to locate a Tile with the provided TileID.
        /// </summary>
        /// <param name="pID">TileID to search for.</param>
        /// <returns>The located TileType if found; NULL if otherwise.</returns>
        public static Tile GetTileFromID(int pID)
        {
            if (AllTiles == null)
                AllTiles = GetAllTiles();

            for (int i = 0; i < AllTiles.Length; i++)
            {
                if (AllTiles[i].TileID == pID)
                    return AllTiles[i];
            }
            return null;
        }

        /// <summary>
        /// v0.90.2 | Returns a hard-coded version of "_tiles.txt"
        /// </summary>
        private static Tile[] BackupTilesLol() // this is pretty bad to be honest lol
        {
            Logger.Warn("[Tile - BackupTilesLol] [WARN] Utilizing hard-coded tile data. This could be incorrect for your game version!");
            
            Tile[] tiles = new Tile[11];
			tiles[0] = new Tile(0, true);
			tiles[1] = new Tile(1, true);
			tiles[2] = new Tile(2, true);
			tiles[3] = new Tile(3, true);
			tiles[4] = new Tile(6, true);
			tiles[5] = new Tile(7, false);
			tiles[6] = new Tile(8, true);
			tiles[7] = new Tile(9, true);
			tiles[8] = new Tile(12, true);
			tiles[9] = new Tile(13, true);
			tiles[10] = new Tile(20, true);
            return tiles;
		}

        /// <summary>
        /// Nulls the static "AllTiles" property so it can collected by the garbage collector... Hopefully.
        /// </summary>
        public static void NullAllTiles()
        {
            AllTiles = null;
        }
    }
}
