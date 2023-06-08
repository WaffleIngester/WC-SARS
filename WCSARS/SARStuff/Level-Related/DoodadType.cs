using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using WCSARS; // logging purposes

namespace SARStuff
{
    /// <summary>
    /// Represents a particular base-doodad-type that actual "Doodad objects" can then represent in a level.
    /// </summary>
    public class DoodadType // more attributes available; however, currently, these are the only ones of any use.
    {
        public static DoodadType[] AllDoodads { get; private set; }
        public readonly int DoodadID;
        public readonly Int32Point[] MovementCollisionPts;
        public readonly Int32Point[] MovementAndSightCollisionPts;
        public readonly byte CollisionHeight;
        public readonly bool Destructible;
        public readonly float DestructibleDamagePeak;
        public readonly float DestructibleDamageRadius;
        public readonly bool CanDropLoot;

        // Private Doodad Constructor
        private DoodadType(JSONNode data)
        {
            // doodadID
            if (data["doodadID"]) DoodadID = data["doodadID"].AsInt;
            else Logger.Failure("[DoodadType] No such key \"doodadID\".");

            // collisionHeight
            if (data["collisionHeight"]) CollisionHeight = (byte)data["collisionHeight"].AsInt;

            // moveCollisionPts
            if (data["moveCollisionPts"])
            {
                // Would be smart to validate the data in some way-- but you shouldn't ever get those exceptions unless you mod the files!
                string collisionPtsString = data["moveCollisionPts"];
                if (collisionPtsString.Contains("~"))
                {
                    List<Int32Point> tmp_moveColPts = new List<Int32Point>(8);
                    string[] coords = collisionPtsString.Split(' ');
                    for (int i = 0; i < coords.Length; i++)
                    {
                        string[] arrayTilda = coords[i].Split('~'); // Splits the XY & WH values apart?
                        string[] split_xy = arrayTilda[0].Split(','); // Splits the XY values
                        string[] split_wh = arrayTilda[1].Split('x'); // Splits the Width / Height values; at least it seems to be W/H.
                        int ogX = Convert.ToInt32(split_xy[0]); // Start X
                        int ogY = Convert.ToInt32(split_xy[1]); // Start Y
                        int width = ogX + Convert.ToInt32(split_wh[0]);  // X-Loop EndPoint
                        int height = ogY + Convert.ToInt32(split_wh[1]); // Y-Loop EndPoint
                        for (int actX = ogX; actX < width; actX++)
                        {
                            for (int actY = ogY; actY < height; actY++)
                            {
                                tmp_moveColPts.Add(new Int32Point(actX, actY));
                            }
                        }
                    }
                    MovementCollisionPts = tmp_moveColPts.ToArray();
                }
                else
                {
                    string[] coords = collisionPtsString.Split(' ');
                    List<Int32Point> tmp_moveColPts = new List<Int32Point>(coords.Length);
                    for (int i = 0; i < coords.Length; i++)
                    {
                        string[] pointLol = coords[i].Split(',');
                        tmp_moveColPts.Add(new Int32Point(Convert.ToInt32(pointLol[0]), Convert.ToInt32(pointLol[1])));
                    }
                    MovementCollisionPts = tmp_moveColPts.ToArray();
                }
            }

            // moveAndSightCollisionPts (totally not copy of moveCollisionPts)
            if (data["moveAndSightCollisionPts"])
            {
                // Basically the same thing as moveCollisionPts but with moveAndSight collision spots instead.
                string collisionPtsString = data["moveAndSightCollisionPts"];
                if (collisionPtsString.Contains("~"))
                {
                    List<Int32Point> tmp_moveSightColPts = new List<Int32Point>(16);
                    string[] coords = collisionPtsString.Split(' ');
                    for (int i = 0; i < coords.Length; i++)
                    {
                        string[] arrayTilda = coords[i].Split('~'); // Splits the XY & WH values apart?
                        string[] split_xy = arrayTilda[0].Split(','); // Splits the XY values
                        string[] split_wh = arrayTilda[1].Split('x'); // Splits the Width / Height values; at least it seems to be W/H.
                        int ogX = Convert.ToInt32(split_xy[0]); // Start X
                        int ogY = Convert.ToInt32(split_xy[1]); // Start Y
                        int width = ogX + Convert.ToInt32(split_wh[0]);  // X-Loop EndPoint
                        int height = ogY + Convert.ToInt32(split_wh[1]); // Y-Loop EndPoint
                        for (int actX = ogX; actX < width; actX++)
                        {
                            for (int actY = ogY; actY < height; actY++)
                            {
                                tmp_moveSightColPts.Add(new Int32Point(actX, actY));
                            }
                        }
                    }
                    MovementAndSightCollisionPts = tmp_moveSightColPts.ToArray();
                }
                else
                {
                    string[] coords = collisionPtsString.Split(' ');
                    List<Int32Point> tmp_moveSightColPts = new List<Int32Point>(coords.Length);
                    for (int i = 0; i < coords.Length; i++)
                    {
                        string[] pointLol = coords[i].Split(',');
                        tmp_moveSightColPts.Add(new Int32Point(Convert.ToInt32(pointLol[0]), Convert.ToInt32(pointLol[1])));
                    }
                    MovementAndSightCollisionPts = tmp_moveSightColPts.ToArray();
                }
            }

            // Destructible
            if (data["destructible"]) Destructible = data["destructible"].AsBool;

            // Destruct Damage Peak | Probs only Explosive Barrels
            if (data["destructibleDamagePeak"]) DestructibleDamagePeak = data["destructibleDamagePeak"].AsFloat;

            // Destruct Damage Radius
            if (data["destructibleDamageRadius"]) DestructibleDamageRadius = data["destructibleDamageRadius"].AsFloat;

            // Destruct Drops Loot
            if (data["destructibleCanDropLoot"]) CanDropLoot = data["destructibleCanDropLoot"].AsBool;
        }

        /// <summary>
        /// Attempts to load every DoodadType as an array.
        /// </summary>
        /// <returns>Array contianing all the loaded-in Doodads.</returns>
        public static DoodadType[] GetAllDoodadTypes()
        {
            if (AllDoodads != null) return AllDoodads;
            string search = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\datafiles\doodads.json";
            if (!File.Exists(search))
            {
                Logger.Failure($"Failed to locate \"doodads.json\"!\nSearched: {search}");
                Environment.Exit(22); // 20 = tiles; 21 = decals; 22 = doodads; 23 = weapons (goes in order of how they should be loaded)
            }
            string data = File.ReadAllText(search);
            JSONArray doodadData = JSON.Parse(data).AsArray;
            AllDoodads = new DoodadType[doodadData.Count];
            for (int i = 0; i < AllDoodads.Length; i++)
            {
                AllDoodads[i] = new DoodadType(doodadData[i]);
            }
            return AllDoodads;
        }

        /// <summary>
        /// Attempts to locate a DoodadType with the provided ID. (a DoodadType with an ID corresponding to the searched-id)
        /// </summary>
        /// <param name="searchID">DoodadID to search for.</param>
        /// <returns>The found DoodadType; NULL if otherwise.</returns>
        public static DoodadType GetDoodadFromID(int searchID)
        {
            if (AllDoodads == null) GetAllDoodadTypes();
            for (int i = 0; i < AllDoodads.Length; i++)
            {
                if (AllDoodads[i]?.DoodadID == searchID)
                {
                    return AllDoodads[i];
                }
            }
            return null;
        }

        /// <summary>
        /// Nulls the static "AllDoodads" property so it can collected by the garbage collector... Hopefully.
        /// </summary>
        public static void NullAllDoodadsList()
        {
            //Logger.Warn("[DoodadType] Nulling AllDoodads...");
            AllDoodads = null;
            //Logger.Success("[DoodadType] AllDoodads nulled! :]");
        }
    }
}
