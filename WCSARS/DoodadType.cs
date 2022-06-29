using System;
using System.IO;
using System.Collections.Generic;
using SimpleJSON;

namespace WCSARS // at some point I want to move all these types from the SAR game into their own separate namespace
{
    public class DoodadType // not quite finished I think?
    {
        public int DoodadID; // there are many more attributes which could be added. these just seemed to be the most useful for our purposes
        public string DoodadName;
        public List<Int32Point> MoveCollisionPoints;
        public List<Int32Point> MoveSightCollisionPoints;
        public byte CollisionHeight;
        public bool Destructible;
        public float DestructibleDamagePeak;
        public float DestructibleDamageRadius;
        public bool CanDropLoot;

        public DoodadType()
        {
            DoodadID = -1;
            DoodadName = "MISSING_NAME";
            MoveCollisionPoints = new List<Int32Point>();
            MoveSightCollisionPoints = new List<Int32Point>();
            CollisionHeight = 0;
            Destructible = false;
            DestructibleDamagePeak = 0f;
            DestructibleDamageRadius = 0f;
            CanDropLoot = false;
        }

        public DoodadType(JSONNode data)
        {
            if (data["doodadID"])
            {
                DoodadID = data["doodadID"].AsInt;
            }
            if (data["imgName"])
            {
                DoodadName = data["imgName"];
            }
            if (data["moveCollisionPts"] != null)
            {
                string colptsDataStr = data["moveCollisionPts"];
                if (colptsDataStr.Contains("~"))
                {
                    MoveCollisionPoints = new List<Int32Point>(8);
                    List<string> CollisionRectStrings = new List<string>(8);
                    string[] splitPointsArray = colptsDataStr.Split(' ');
                    foreach (string text in splitPointsArray)
                    {
                        CollisionRectStrings.Add(text);
                        string[] arrayTilda = text.Split('~');
                        string[] arrayComSep = arrayTilda[0].Split(',');
                        string[] arrayX = arrayTilda[1].Split('x');
                        int num = Convert.ToInt32(arrayComSep[0]);
                        int num2 = Convert.ToInt32(arrayComSep[1]);
                        int num3 = num + Convert.ToInt32(arrayX[0]);
                        int num4 = num2 + Convert.ToInt32(arrayX[1]);
                        for (int n = num; n < num3; n++)
                        {
                            for (int num5 = num2; num5 < num4; num5++)
                            {
                                MoveCollisionPoints.Add(new Int32Point(n, num5));
                            }
                        }
                    }
                }
                else
                {
                    string[] sepArray = colptsDataStr.Split(' ');
                    MoveCollisionPoints = new List<Int32Point>(sepArray.Length);
                    foreach (string lol in sepArray)
                    {
                        string[] pointslol = lol.Split(',');
                        MoveCollisionPoints.Add(new Int32Point(Convert.ToInt32(pointslol[0]), Convert.ToInt32(pointslol[1])));
                    }
                }
            }
			if (data["collisionHeight"])
            {
                CollisionHeight = (byte)data["collisionHeight"].AsInt;
            }
            if (data["destructible"])
            {
                Destructible = data["destructible"].AsBool;
            }
            if (data["destructibleDamagePeak"])
            {
                DestructibleDamagePeak = data["destructibleDamagePeak"].AsFloat;
            }
            if (data["destructibleDamageRadius"])
            {
                DestructibleDamageRadius = data["destructibleDamageRadius"].AsFloat;
            }
            if (data["destructibleCanDropLoot"])
            {
                CanDropLoot = data["destructibleCanDropLoot"].AsBool;
            }
        }
        public static Dictionary<int, DoodadType> GetAllDoodadTypes()
        {
            // make sure that the doodad types data file exists...
            if (!File.Exists(Directory.GetCurrentDirectory() + @"\datafiles\DoodadData.json"))
            {
                throw new Exception("The datafile \"DoodadData.json\" is missing/unable to be found. Please verify it is in the correct folder/or exists.");
            }
            // variables needed to create list and scuh
            Dictionary<int, DoodadType> _doodadtypes = new Dictionary<int, DoodadType>();
            JSONArray _array = (JSONArray)JSON.Parse(File.ReadAllText(Directory.GetCurrentDirectory() + @"\datafiles\DoodadData.json"));
            JSONNode _node;
            int entries = _array.Count;
            // actually adds the doodads and stuff
            for (int i = 0; i < entries; i++)
            {
                _node = _array[i];
                DoodadType _newDoodad = new DoodadType();

                // check data
                if (_node["doodadID"])
                {
                    _newDoodad.DoodadID = _node["doodadID"].AsInt;
                }
                if (_node["imgName"])
                {
                    _newDoodad.DoodadName = _node["imgName"];
                }
                if (_node["moveCollisionPts"] != null)
                {
                    string colpts_nodeStr = _node["moveCollisionPts"];
                    if (colpts_nodeStr.Contains("~"))
                    {
                        _newDoodad.MoveCollisionPoints = new List<Int32Point>(8);
                        List<string> CollisionRectStrings = new List<string>(8);
                        string[] splitPointsArray = colpts_nodeStr.Split(' ');
                        foreach (string text in splitPointsArray)
                        {
                            CollisionRectStrings.Add(text);
                            string[] arrayTilda = text.Split('~');
                            string[] arrayComSep = arrayTilda[0].Split(',');
                            string[] arrayX = arrayTilda[1].Split('x');
                            int num = Convert.ToInt32(arrayComSep[0]);
                            int num2 = Convert.ToInt32(arrayComSep[1]);
                            int num3 = num + Convert.ToInt32(arrayX[0]);
                            int num4 = num2 + Convert.ToInt32(arrayX[1]);
                            for (int n = num; n < num3; n++)
                            {
                                for (int num5 = num2; num5 < num4; num5++)
                                {
                                    _newDoodad.MoveCollisionPoints.Add(new Int32Point(n, num5));
                                }
                            }
                        }
                    }
                    else
                    {
                        string[] sepArray = colpts_nodeStr.Split(' ');
                        _newDoodad.MoveCollisionPoints = new List<Int32Point>(sepArray.Length);
                        foreach (string lol in sepArray)
                        {
                            string[] pointslol = lol.Split(',');
                            _newDoodad.MoveCollisionPoints.Add(new Int32Point(Convert.ToInt32(pointslol[0]), Convert.ToInt32(pointslol[1])));
                        }
                    }
                }
                if (_node["moveAndSightCollisionPts"] != null)
                {
                    string colpts_nodeStr = _node["moveAndSightCollisionPts"];
                    if (colpts_nodeStr.Contains("~"))
                    {
                        _newDoodad.MoveSightCollisionPoints = new List<Int32Point>(8);
                        List<string> CollisionRectStrings = new List<string>(8);
                        string[] splitPointsArray = colpts_nodeStr.Split(' ');
                        foreach (string text in splitPointsArray)
                        {
                            CollisionRectStrings.Add(text);
                            string[] arrayTilda = text.Split('~');
                            string[] arrayComSep = arrayTilda[0].Split(',');
                            string[] arrayX = arrayTilda[1].Split('x');
                            int num = Convert.ToInt32(arrayComSep[0]);
                            int num2 = Convert.ToInt32(arrayComSep[1]);
                            int num3 = num + Convert.ToInt32(arrayX[0]);
                            int num4 = num2 + Convert.ToInt32(arrayX[1]);
                            for (int n = num; n < num3; n++)
                            {
                                for (int num5 = num2; num5 < num4; num5++)
                                {
                                    _newDoodad.MoveSightCollisionPoints.Add(new Int32Point(n, num5));
                                }
                            }
                        }
                    }
                    else
                    {
                        string[] sepArray = colpts_nodeStr.Split(' ');
                        _newDoodad.MoveSightCollisionPoints = new List<Int32Point>(sepArray.Length);
                        foreach (string lol in sepArray)
                        {
                            string[] pointslol = lol.Split(',');
                            _newDoodad.MoveSightCollisionPoints.Add(new Int32Point(Convert.ToInt32(pointslol[0]), Convert.ToInt32(pointslol[1])));
                        }
                    }
                }
                if (_node["collisionHeight"])
                {
                    _newDoodad.CollisionHeight = (byte)_node["collisionHeight"].AsInt;
                }
                if (_node["destructible"])
                {
                    _newDoodad.Destructible = _node["destructible"].AsBool;
                }
                if (_node["destructibleDamagePeak"])
                {
                    _newDoodad.DestructibleDamagePeak = _node["destructibleDamagePeak"].AsFloat;
                }
                if (_node["destructibleDamageRadius"])
                {
                    _newDoodad.DestructibleDamageRadius = _node["destructibleDamageRadius"].AsFloat;
                }
                if (_node["destructibleCanDropLoot"])
                {
                    _newDoodad.CanDropLoot = _node["destructibleCanDropLoot"].AsBool;
                }
                // add the newly created doodad to the list!
                _doodadtypes.Add(_newDoodad.DoodadID, _newDoodad);
            }
            return _doodadtypes; // return the list and end this nonsense
        }
    }
}
