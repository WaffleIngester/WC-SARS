using SimpleJSON;
using System;
using System.IO;
using WCSARS; // for logging purposes...

namespace SARStuff
{
    /// <summary> Represents one of many grass variations found in Super Animal Royale. </summary>
    public class GrassType
    {
        /// <summary>
        /// Every single loaded in GrassType. Initialized with <see cref="GetAllGrassTypes"/>; deleted by <see cref="NullAllGrassTypes"/>.
        /// </summary>
        public static GrassType[] AllGrassTypes;

        /// <summary>
        /// ID of this GrassType.
        /// </summary>
        public readonly byte GrassID; // always has

        /// <summary>
        /// Name of this GrassType found in the grass data file.
        /// </summary>
        //public readonly string ImgName; // always has

        /// <summary>
        /// Number of variations this GrassType has.
        /// </summary>
        public readonly byte Variations; // always has

        /// <summary>
        /// Whether or not it is possible for this GrassType/ Grass to be chopped.
        /// </summary>
        public readonly bool Choppable;

        /// <summary>
        /// Whether or not it is possible for this GrassType/ Grass to be chopped again (typically used by bushes).
        /// </summary>
        public readonly bool Rechoppable;

        /// <summary> Use Unknown </summary>
        //public readonly float HitboxXExtent; // always has  --- unsure how & what this is used for

        /// <summary> Use Unknown </summary>
        //public readonly float HitboxYExtent; // always has --- unsure how & what this is used for

        private GrassType(JSONNode data)
        {
            // grassID
            if (data["grassID"]) GrassID = (byte)data["grassID"].AsInt;
            else Logger.Failure("[GrassType] No such key \"grassID\".");

            // imgBaseName
            //if (data["imgBaseName"]) ImgName = data["imgBaseName"];
            //else Logger.Failure("[GrassType] No such key \"imgBaseName\".");

            // variations
            if (data["variations"]) Variations = (byte)data["variations"].AsInt;
            else Logger.Failure("[GrassType] No such key \"variations\".");

            // choppable
            if (data["choppable"] != null) Choppable = data["choppable"].AsBool;
            //else Logger.Failure($"[GrassType] No such key \"choppable\". @ {GrassID}");

            // rechoppable
            if (data["rechoppable"] != null) Rechoppable = data["rechoppable"].AsBool;
            //else Logger.Failure($"[GrassType] No such key \"rechoppable\". @ {GrassID}");

            // hitboxExtentX
            //if (data["hitboxExtentX"]) HitboxXExtent = data["hitboxExtentX"].AsFloat;
            //else Logger.Failure("[GrassType] No such key \"hitboxExtentX\".");

            // hitboxExtentY
            //if (data["hitboxExtentY"]) HitboxYExtent = data["hitboxExtentY"].AsFloat;
            //else Logger.Failure("[GrassType] No such key \"hitboxExtentY\".");
        }

        /// <summary>Attempts to load every GrassType stored in the grass data file.</summary>
        /// <returns>Array containing all found GrassTypes.</returns>
        public static GrassType[] GetAllGrassTypes()
        {
            if (AllGrassTypes != null) return AllGrassTypes;

            string search = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\datafiles\grass.json";
            if (!File.Exists(search))
            {
                Logger.Failure($"Failed to locate \"grass.json\"!\nSearched: {search}");
                Environment.Exit(24); // order of loaded grass doesn't matter as much as the grids-related stuff
            }
            string data = File.ReadAllText(search);
            JSONArray grassData = JSON.Parse(data).AsArray;
            AllGrassTypes = new GrassType[grassData.Count];
            for (int i = 0; i < AllGrassTypes.Length; i++)
            {
                AllGrassTypes[i] = new GrassType(grassData[i]);
            }
            return AllGrassTypes;
        }

        /// <summary>
        /// Attempts to locate a GrassType using the provided SearchID.
        /// </summary>
        /// <param name="searchID">GrassID to search for.</param>
        /// <returns>GrassType matching the SearchID; NULL if the SearchID is not found.</returns>
        public static GrassType GetGrassTypeFromID(byte searchID)
        {
            if (AllGrassTypes == null) GetAllGrassTypes();
            for (int i = 0; i < AllGrassTypes.Length; i++)
            {
                if (AllGrassTypes[i]?.GrassID == searchID) return AllGrassTypes[i];
            }
            return null;
        }

        public static void NullAllGrassTypes()
        {
            AllGrassTypes = null;
        }
    }
}
