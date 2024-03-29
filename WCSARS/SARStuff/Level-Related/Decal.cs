﻿using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
using WCSARS; // logging purposes

namespace SARStuff
{
    internal class Decal
	{
		public static Decal[] AllDecals { get; private set; }
		public readonly int DecalID;
		public readonly List<Rectangle> WalkableSpots;
		public readonly List<Rectangle> NonWalkableSpots;

		private Decal(JSONNode node)
		{
			// ID
			if (node["decalID"]) DecalID = node["decalID"];
			else Logger.Failure("No key \"nonWalkableRects\" found for this node.");

			// Walkable Spots
			if (node["walkableRects"])
			{
				string walkableRectsText = node["walkableRects"];
				WalkableSpots = new List<Rectangle>(node["walkableRects"].Count);
				if (walkableRectsText?.Length > 0)
				{
					string[] splitups = walkableRectsText.Split(' ');
					for (int i = 0; i < splitups.Length; i++)
					{
						string[] sep1 = splitups[i].Split('~');
						string[] sep2 = sep1[0].Split(',');
						string[] sep3 = sep1[1].Split('x');
						WalkableSpots.Add(new Rectangle(Convert.ToSingle(sep2[0]), Convert.ToSingle(sep2[1]), Convert.ToSingle(sep3[0]), Convert.ToSingle(sep3[1])));
					}
				}
			}

			// Non-walkable Spots
			if (node["nonWalkableRects"])
			{
				string nonWalkableRects = node["nonwalkableRects"];
				NonWalkableSpots = new List<Rectangle>(node["nonwalkableRects"].Count);
				if (nonWalkableRects?.Length > 0)
				{
					string[] splitups = nonWalkableRects.Split(' ');
					for (int i = 0; i < splitups.Length; i++)
					{
						string[] sep1 = splitups[i].Split('~');
						string[] sep2 = sep1[0].Split(',');
						string[] sep3 = sep1[1].Split('x');
						NonWalkableSpots.Add(new Rectangle(Convert.ToSingle(sep2[0]), Convert.ToSingle(sep2[1]), Convert.ToSingle(sep3[0]), Convert.ToSingle(sep3[1])));
					}
				}
			}
		}

		/// <summary>
		/// Attempts to gather all decal types using SAR's decal data file.
		/// </summary>
		/// <returns>An array of all DecalTypes.</returns>
		public static Decal[] GetAllDecals()
		{
			if (AllDecals != null) return AllDecals;
			string search = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\datafiles\decals.json";
			if (!File.Exists(search))
			{
				Logger.Failure($"Failed to locate \"decals.json\"!\nSearched: {search}");
				Environment.Exit(21); // 20 = tiles; 21 = decals; 22 = doodads; 23 = weapons (goes in order of how they should be loaded)
			}
			string data = File.ReadAllText(search);
			JSONArray tileData = JSON.Parse(data).AsArray;
			AllDecals = new Decal[tileData.Count];
			for (int i = 0; i < AllDecals.Length; i++)
			{
				AllDecals[i] = new Decal(tileData[i]);
			}
			return AllDecals;
		}

		/// <summary>
		/// Attempts to locate a Decal using the provided ID.
		/// </summary>
		/// <param name="searchID">DecalID to search for.</param>
		/// <returns>The found Decal; NULL if otherwise.</returns>
		public static Decal GetDecalFromID(int searchID)
        {
			if (AllDecals == null) GetAllDecals();
			for (int i = 0; i < AllDecals.Length; i++)
            {
				if (AllDecals[i]?.DecalID == searchID) return AllDecals[i];
            }
			return null;
        }

		/// <summary>
		/// Nulls the static "Decal" property so it can collected by the garbage collector... Hopefully.
		/// </summary>
		public static void NullAllDecals()
		{
			//Logger.Warn("[Decal] Nulling AllDecals...");
			AllDecals = null;
			//Logger.Success("[Decal] AllDecals nulled! :]");
		}
	}
}