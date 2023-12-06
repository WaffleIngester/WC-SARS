using SimpleJSON;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Xml;
using WCSARS; // logging purposes

namespace SARStuff
{
    internal class SARLevel
    {
        // SARLevel basics
        /// <summary>
        /// The height of this SARLevel.
        /// </summary>
        public int LevelHeight { get; private set; }

        /// <summary>
        /// The width of this SARLevel.
        /// </summary>
        public int LevelWidth { get; private set; }

        /// <summary>
        /// A grid representing walkable/ unwalkable spots for this SARLevel. (GridSize = LevelWidth * LevelHeight)
        /// </summary>
        public CollisionType[][] CollisionGrid { get; private set; }

        // Loadable from map data

        /// <summary>
        ///  List of spots that joining players can spawn at.
        /// </summary>
        public List<Vector2> PlayerSpawns; // todo - optionally use these when players spawn in!

        /// <summary>
        /// A list containing all Doodads within this SARLevel.
        /// </summary>
        public List<Doodad> Doodads { get; private set; }

        /// <summary>
        /// An array of Campfire objects stored by this SARLevel.
        /// </summary>
        public Campfire[] Campfires { get; private set; }

        /// <summary> List contianing all loaded choppable grass items.</summary>
        public List<GameGrass> Grass { get; private set; }

        /// <summary>
        /// All spawn spots where a Molecrate Mole can spawn.
        /// </summary>
        public Vector2[] MolecrateSpots { get; private set; }

        // Generated Stuff
        /// <summary>
        /// Total number of generated LootItems within this SARLevel.
        /// </summary>
        public int LootCounter { get; private set; }

        /// <summary>
        /// A dictionary containing all LootItems within this SARLevel.
        /// </summary>
        public Dictionary<int, LootItem> LootItems { get; private set; }

        /// <summary>
        /// A dictionary containing all Coconuts within this SARLevel.
        /// </summary>
        public Dictionary<int, Coconut> Coconuts { get; private set; }

        /// <summary>
        /// A dictionary containing all Hamsterballs within this SARLevel.
        /// </summary>
        public Dictionary<int, Hamsterball> Hamsterballs { get; private set; }

        /// <summary>
        ///  Seed used to generate this SARLevel's initial LootItem list.
        /// </summary>
        public readonly uint LootSeed;

        /// <summary>
        ///  Seed used to generate this SARLevel's Coconut list.
        /// </summary>
        public readonly uint CoconutSeed;

        /// <summary>
        ///  Seed used to generate this SARLevel's Hamsterball list.
        /// </summary>
        public readonly uint HamsterballSeed;


        /// <summary>
        ///  Doodad representing the door to the hideout found in Super Animal Farm.
        /// </summary>
        private Doodad _rebelHideoutDoor;

        /// <summary>
        ///  Location where the tarp covering the Rebel Hideout in Super Animal Farm spawns.
        /// </summary>
        private Int32Point _barnTarpSpawn = new Int32Point(-1, -1);

        // TODO: barn tarp opening properly. the room is easy, just take the origin / size = all spots to remove
        // harder part is the door (which will need to account for collisionHeight) and also the other doodads that should be stopping you lol
        // because if you remove the whole barn tarp you can actually walk into the walls and stuff. which is pretty funny, but unintentional.

        public SARLevel(uint lootSeed, uint coconutSeed, uint hamsterballSeed)
        {
            //Logger.Header("[SARLevel] Created!");
            LootSeed = lootSeed;
            CoconutSeed = coconutSeed;
            HamsterballSeed = hamsterballSeed;
            LoadLol();
        }

        /// <summary>
        /// Begins loading SAR map data into this SARLevel's fields and such!
        /// </summary>
        private void LoadLol() // Good-enough v0.90.2
        {
            // honestly, I feel like we could potentially represent this better ourselves somehow
            // load level data...
            string search = AppDomain.CurrentDomain.BaseDirectory + @"datafiles\earlyaccessmap1.txt";
            if (!File.Exists(search))
            {
                Logger.Failure($"Failed to locate \"earlyaccessmap1.txt\"!\nSearched: {search}");
                Environment.Exit(19); // 19 = map data; 20 = tiles; 21 = decals; 22 = doodads; 23 = weapons (sorta based on the order loaded here)
            }
            JSONNode levelJSON = JSONNode.LoadFromFile(search);
            // end of loading level data into JSONNode

            LevelWidth = levelJSON["levelWidth"].AsInt;
            LevelHeight = levelJSON["levelHeight"].AsInt;
            CollisionGrid = new CollisionType[LevelWidth][];
            for (int c = 0; c < CollisionGrid.Length; c++)
            {
                // Other level-related fields that use width/height exist; however, only the collision-grid is of any use at this moment.
                CollisionGrid[c] = new CollisionType[LevelHeight];
            }

            // Tiles > Decals > Doodads > Campfires > not important > "barnTarp"
            // OK - v0.90.2
            #region Tiles
            Logger.Header("[SARLevel - Tiles] Beginning to load tiles...");

            int numOTiles = levelJSON["tilesList"].Count;
            for (int i = 0; i < numOTiles; i++)
            {
                JSONObject tileNode = levelJSON["tilesList"][i].AsObject;
                int id = tileNode["i"].AsInt;
                Tile tile = Tile.GetTileFromID(id);
                if (tile == null)
                {
                    Logger.Warn($"[SARLevel - Tiles] TileID \"{id}\" gave a null return.");
                    continue;
                }

                // stored position & size is divided by 9. not entirely sure why
                Int32Point position = new Int32Point(tileNode["x"].AsInt * 9, tileNode["y"].AsInt * 9);
                Int32Point size = new Int32Point(tileNode["w"].AsInt * 9, tileNode["h"].AsInt * 9);

                // place collision points for this Tile on ColliionGrid
                if (tile.Walkable)
                {
                    int tile_maxX = position.x + size.x; // MaxPos X
                    int tile_maxY = position.y + size.y; // MaxPos Y
                    for (int x = position.x; x < tile_maxX; x++) // I'm too dumb to comprehend why this checks out
                    {
                        for (int y = position.y; y < tile_maxY; y++) // All I know is that this is how SAR does it more or less
                        {
                            CollisionGrid[x][y] = CollisionType.None;
                        }
                    }
                }
            }
            Tile.NullAllTiles(); // Free Tile.AllTiles/ any that are not currently in use from memory

            //Logger.Basic("[SARLevel - Tiles] Done!");
            #endregion Tiles

            // OK - v0.90.2
            #region Decals
            Logger.Header("[SARLevel - Decals] Beginning to load decals...");

            int numOfDecals = levelJSON["decals"].Count;
            for (int i = 0; i < numOfDecals; i++)
            {
                JSONNode subNode = levelJSON["decals"][i];
                int decalID = subNode["i"];
                Int32Point decalPosition = new Int32Point(subNode["x"].AsInt, subNode["y"].AsInt);
                Decal decal = Decal.GetDecalFromID(decalID);
                if (decal == null)
                {
                    Logger.Failure($"[SARLevel - Decals] Couldn't locate DecalID \"{decalID}\"!");
                    continue;
                }

                // place this Decal's collision spots on the collision grid
                if (decal.WalkableSpots != null)
                {
                    foreach (Rectangle rect in decal.WalkableSpots)
                    {
                        int n1 = decalPosition.x + (int)rect.MinX;
                        int n2 = decalPosition.y + (int)rect.MinY;
                        int n3 = n1 + (int)rect.Width;
                        int n4 = n2 + (int)rect.Height;
                        for (int x = n1; x < n3; x++)
                        {
                            for (int y = n2; y < n4; y++)
                            {
                                if ((x < LevelWidth) && (y < LevelHeight))
                                    CollisionGrid[x][y] = CollisionType.None;
                            }
                        }
                    }
                }
                if (decal.NonWalkableSpots != null)
                {
                    foreach (Rectangle rect in decal.NonWalkableSpots)
                    {
                        int n1 = decalPosition.x + (int)rect.MinX;
                        int n2 = decalPosition.y + (int)rect.MinY;
                        int n3 = n1 + (int)rect.Width;
                        int n4 = n2 + (int)rect.Height;
                        for (int x = n1; x < n3; x++)
                        {
                            for (int y = n2; y < n4; y++)
                            {
                                if ((x < LevelWidth) && (y < LevelHeight)) CollisionGrid[x][y] = CollisionType.Movement;
                            }
                        }
                    }
                }
            }
            Decal.NullAllDecals(); // Clear up all DecalTypes from memory.
            //Logger.Basic("[SARLevel - Decals] Done!");
            #endregion Decals

            // OK - v0.90.2
            #region Doodads
            Logger.Header("[SARLevel - Doodads] Beginning to load doodads...");

            int numOfdoodads = levelJSON["doodads"].Count;
            Doodads = new List<Doodad>(levelJSON["doodads"].Count);
            for (int i = 0; i < numOfdoodads; i++)
            {
                JSONNode subNode = levelJSON["doodads"][i];
                int doodadID = subNode["i"];
                Int32Point doodadPosition = new Int32Point(subNode["x"].AsInt, subNode["y"].AsInt);
                DoodadType doodad = DoodadType.GetDoodadFromID(doodadID);
                if (doodad == null)
                {
                    Logger.Failure($"[SARLevel - Decals] Couldn't locate DecalID \"{doodadID}\"!");
                    continue;
                }
                else if (doodad.Destructible)
                    Doodads.Add(new Doodad(doodad, new Vector2(subNode["x"].AsFloat, subNode["y"].AsFloat)));
                else if (doodad.DoodadID == 5282)
                    _rebelHideoutDoor = new Doodad(doodad, new Vector2(subNode["x"].AsFloat, subNode["y"].AsFloat));

                if (doodad.MovementCollisionPts != null) // Array version as opposed to the List<T> one
                {
                    for (int j = 0; j < doodad.MovementCollisionPts.Length; j++)
                    {
                        int x = doodadPosition.x + doodad.MovementCollisionPts[j].x;
                        int y = doodadPosition.y + doodad.MovementCollisionPts[j].y;

                        if ((x < LevelWidth) && (y < LevelHeight))
                        {
                            // HeightGrid stuff here too, but not handled
                            if (CollisionGrid[x][y] != CollisionType.MovementAndSight)
                                CollisionGrid[x][y] = CollisionType.Movement;
                        }
                    }
                }
                if (doodad.MovementAndSightCollisionPts != null) // Array version as opposed to the List<T> one
                {
                    for (int j = 0; j < doodad.MovementAndSightCollisionPts.Length; j++)
                    {
                        int x = doodadPosition.x + doodad.MovementAndSightCollisionPts[j].x;
                        int y = doodadPosition.y + doodad.MovementAndSightCollisionPts[j].y;
                        if ((x < LevelWidth) && (y < LevelHeight))
                        {
                            // HeightGrid stuff here too, but not handled
                            CollisionGrid[x][y] = CollisionType.MovementAndSight;
                        }
                    }
                }
            }
            DoodadType.NullAllDoodadsList();
            GC.Collect();
            //Logger.Basic("[SARLevel - Doodads] Done!");
            #endregion Doodads

            // OK? - v0.90.2
            #region Campfires
            Logger.Header("[SARLevel - Campfires] Beginning to load campfires...");
            if (levelJSON["campfires"] != null)
            {
                Campfires = new Campfire[levelJSON["campfires"].Count];
                for (int i = 0; i < Campfires.Length; i++)
                {
                    Vector2 position = new Vector2(levelJSON["campfires"][i]["x"].AsFloat, levelJSON["campfires"][i]["y"].AsFloat);
                    Campfires[i] = new Campfire(position);

                    int campfireHitboxWidthMIN = (int)position.x - 6;
                    int campfireHitboxHeightMIN = (int)position.y - 4;
                    int campfireHitboxWidthMAX = (int)position.x + 6;
                    int campfireHitboxHeightMAX = (int)position.y + 6;
                    for (int x = campfireHitboxWidthMIN; x <= campfireHitboxWidthMAX; x++)
                    {
                        for (int y = campfireHitboxHeightMIN; y <= campfireHitboxHeightMAX; y++)
                        {
                            if (x != campfireHitboxWidthMIN || y != campfireHitboxHeightMIN)
                            {
                                if (x != campfireHitboxWidthMAX || y != campfireHitboxHeightMIN)
                                {
                                    if (x != campfireHitboxWidthMIN || y != campfireHitboxHeightMAX)
                                    {
                                        if (x != campfireHitboxWidthMAX || y != campfireHitboxHeightMAX)
                                        {
                                            if ((x < LevelWidth) && (y < LevelHeight)) CollisionGrid[x][y] = CollisionType.Movement;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
                Logger.Failure($"[SARLevel - Campfires] No key \"campfires\"!");
            //Logger.Basic("[SARLevel - Campfires] Done!");
            #endregion Campfires

            // OK - v0.90.2
            // todo - level height collision-height grid if that is ever added...
            #region BarnTarp
            Logger.Header("[SARLevel - BarnTarp] Beginning to load barn tarp...");
            if (levelJSON["barnTarp"] != null)
            {
                Int32Point _barnTarpSpawn = new Int32Point(levelJSON["barnTarp"]["x"].AsInt, levelJSON["barnTarp"]["y"].AsInt);
                int tarpSpawnOffsetX = _barnTarpSpawn.x + SARConstants.BarnHideoutTarpSizeX;
                int tarpSpawnOffsetY = _barnTarpSpawn.y + SARConstants.BarnHideoutTarpSizeY;
                for (int x = _barnTarpSpawn.x; x < tarpSpawnOffsetX; x++)
                {
                    for (int y = _barnTarpSpawn.y - 11; y < tarpSpawnOffsetY; y++)
                    {
                        if ((LevelWidth > x) && (LevelHeight > y))
                        {
                            if (CollisionGrid[x][y] != CollisionType.MovementAndSight)
                                CollisionGrid[x][y] = CollisionType.Movement;
                        }
                    }
                }
            }
            else
                Logger.Failure("[SARLevel - BarnTarp] Key \"barnTarp\" does not exist.");
            //Logger.Basic("[SARLevel - BarnTarp] Done!");
            #endregion BarnTarp

            // OK - v0.90.2
            #region Coconuts
            Logger.Header("[SARLevel - Coconuts] Beginning to load coconuts...");
            if (levelJSON["coconuts"] != null)
            {
                MersenneTwister coconutTwister = new MersenneTwister(CoconutSeed);
                int trueCoconutID = 0;
                int numOfCocounts = levelJSON["coconuts"].Count;
                Coconuts = new Dictionary<int, Coconut>(numOfCocounts);
                for (int i = 0; i < numOfCocounts; i++)
                {
                    JSONNode subNode = levelJSON["coconuts"][i];
                    if (coconutTwister.NextUInt(0U, 100U) > 65.0)
                    {
                        Vector2 spawnPos = new Vector2(subNode["x"].AsFloat, subNode["y"].AsFloat);
                        Coconuts.Add(trueCoconutID, new Coconut(spawnPos));
                        trueCoconutID++;
                    }
                }
            }
            else
                Logger.Failure($"[SARLevel - Coconuts] No key \"coconuts\"!");
            //Logger.Basic("[SARLevel - Coconuts] Done!");
            #endregion Coconuts

            // OK - v0.90.2
            #region Hamsterballs
            Logger.Header("[SARLevel - Hamsterballs] Beginning to load hamsterballs...");
            if (levelJSON["vehicles"] != null)
            {
                MersenneTwister hamsterballTwister = new MersenneTwister(HamsterballSeed);
                int trueHampterID = 0;
                int hamsterballCache = levelJSON["vehicles"].Count;
                Hamsterballs = new Dictionary<int, Hamsterball>(hamsterballCache);
                for (int i = 0; i < hamsterballCache; i++)
                {
                    JSONNode subNode = levelJSON["vehicles"][i];
                    if (hamsterballTwister.NextUInt(0U, 100U) > 55.0)
                    {
                        Vector2 spawnPos = new Vector2(subNode["x"].AsFloat, subNode["y"].AsFloat);
                        Hamsterballs.Add(trueHampterID, new Hamsterball((short)trueHampterID, spawnPos));
                        trueHampterID++;
                    }
                }
            }
            else
                Logger.Failure($"[SARLevel - Hamsterballs] No key \"vehicles\"!");
            //Logger.Basic("[SARLevel - Hamsterballs] Done!");
            #endregion Hamsterballs

            // OK - v0.90.2
            #region Loot Items
            Logger.Header("[SARLevel - LootItems] Beginning to load loot-items...");
            int lootSpotsNormal = 0;
            int lootSpotsGood = 0;
            int lootSpotsBot = 0;
            if (levelJSON["lootSpawns"] != null) lootSpotsNormal = levelJSON["lootSpawns"].Count;
            if (levelJSON["lootSpawnsGood"] != null) lootSpotsGood = levelJSON["lootSpawnsGood"].Count;
            if (levelJSON["lootSpawnsNoBot"] != null) lootSpotsBot = levelJSON["lootSpawnsNoBot"].Count;
            if (lootSpotsBot > 0) Logger.missingHandle("[LoadSARLevel] lootSpawnsNoBot actually contains entries for once. Is this intentional?");
            int totalSpawnSpots = lootSpotsNormal + lootSpotsGood + lootSpotsBot;

            // Load Tile Spots -- Could be better. Am tired though
            Vector2[] lootSpots = new Vector2[totalSpawnSpots];
            for (int n = 0; n < lootSpotsNormal; n++)
            {
                lootSpots[n] = new Vector2(levelJSON["lootSpawns"][n]["x"].AsFloat, levelJSON["lootSpawns"][n]["y"].AsFloat);
            }
            for (int g = 0; g < lootSpotsGood; g++)
            {
                int ind = g + lootSpotsNormal;
                lootSpots[ind] = new Vector2(levelJSON["lootSpawnsGood"][g]["x"].AsFloat, levelJSON["lootSpawnsGood"][g]["y"].AsFloat);
            }

            // Setup variables for genration
            MersenneTwister mersenneTwister = new MersenneTwister(LootSeed);
            LootItems = new Dictionary<int, LootItem>();
            LootCounter = 0;

            // Stuff for RNG
            int currentLootID;  // For whatever reason, LootItems isn't like Coconuts/Hamsterball; where IDs are always in order. IDs can have huge leaps.
            bool doBetterGen;   // Whether to have a higher chance to get better loot 
            uint rngNum;        // Number returned on an RNG attempt
            uint minGenNum;     // Mini value that can be generated...

            // build the weighted list of spawnable weapons; "spawnFrequency" from _weapons.txt is used for this purpose
            List<short> weightedSpawnableWeapons = new List<short>();
            Weapon[] allWeapons = Weapon.GetAllWeaponTypes();
            for (int i = 0; i < allWeapons.Length; i++)
            {
                for (int j = 0; j < allWeapons[i].SpawnFrequency; j++)
                {
                    weightedSpawnableWeapons.Add(allWeapons[i].JSONIndex);
                }
            }

            for (int i = 0; i < totalSpawnSpots; i++)
            {
                // increment loot counters/ reset RNG values
                currentLootID = LootCounter;
                LootCounter++;

                // increased chances of better loot
                doBetterGen = false;
                minGenNum = 0U;

                if (i >= lootSpotsNormal)
                {
                    doBetterGen = true;
                    minGenNum = 20U;
                }


                // Now roll!
                rngNum = mersenneTwister.NextUInt(minGenNum, 100U);

                // this is messy -- someone can probably find a better way of doing this junk than I can at this time
                //
                // one should note purposeful gaps. For example: values [50.1 - 60.0] are intended to not spawn anything 

                if (rngNum <= 33.0)
                    continue; // <=33 == no item spawns

                if (rngNum > 33.0 && rngNum <= 47.0) // Drinks
                {
                    if (doBetterGen)
                        minGenNum = 15U;
                    rngNum = mersenneTwister.NextUInt(minGenNum, 100U);

                    byte juiceAmount = 40;
                    if (rngNum <= 55)
                        juiceAmount = 10;
                    else if (rngNum <= 89)
                        juiceAmount = 20;

                    LootItem drinkLoot = new LootItem(currentLootID,
                                            LootType.Juice,
                                            $"Health Juice-{juiceAmount}",
                                            0,
                                            juiceAmount,
                                            lootSpots[i]);
                    LootItems.Add(currentLootID, drinkLoot);
                }
                else if (rngNum <= 59.0) // Armor
                {
                    if (doBetterGen)
                        minGenNum = 24U;
                    rngNum = mersenneTwister.NextUInt(minGenNum, 100U);

                    byte armorTier = 3;
                    if (rngNum <= 65.0)
                        armorTier = 1;
                    else if (rngNum <= 92.0)
                        armorTier = 2;

                    // LootItems.Rarity = armor tier; LootItems.GiveAmout = amount of armor ticks.
                    LootItem armorLoot = new LootItem(currentLootID,
                                            LootType.Armor,
                                            $"Armor-{armorTier}",
                                            armorTier,
                                            armorTier,
                                            lootSpots[i]);
                    LootItems.Add(currentLootID, armorLoot);
                }
                else if (rngNum > 60.0 && rngNum <= 66.0) // Tape
                {
                    LootItem tapeLoot = new LootItem(currentLootID, LootType.Tape, "Tape (x1)", 0, 1, lootSpots[i]);
                    LootItems.Add(currentLootID, tapeLoot);
                }
                else if (rngNum > 66.0) // Weapons [Guns & Throwables]
                {
                    // note -- LootItem.GiveAmount = numOfAmmoInWeapon

                    // find which weapon to generate
                    rngNum = mersenneTwister.NextUInt(0U, (uint)weightedSpawnableWeapons.Count);
                    int genWeaponIndex = weightedSpawnableWeapons[(int)rngNum];
                    Weapon foundWeapon = allWeapons[genWeaponIndex];

                    // substats
                    if (foundWeapon.WeaponType == WeaponType.Gun)
                    {
                        // "better loot" in this case is higher-rarity weapons-- just to make that clear.
                        if (doBetterGen)
                            minGenNum = 22U;
                        minGenNum = mersenneTwister.NextUInt(minGenNum, 100U);


                        // actual rarity rolls using the higher/ lower base odds determiend from above
                        byte rarity = 0;
                        if (minGenNum > 58.0 && minGenNum <= 80.0)
                            rarity = 1;
                        else if (minGenNum > 80.0 && minGenNum <= 91.0)
                            rarity = 2;
                        else if (minGenNum > 91.0 && minGenNum <= 97.0)
                            rarity = 3;

                        // out of bounds rarity checking...
                        if (rarity > foundWeapon.RarityMaxVal)
                            rarity = foundWeapon.RarityMaxVal;

                        if (rarity < foundWeapon.RarityMinVal)
                            rarity = foundWeapon.RarityMinVal;

                        // actually adding the weapon into list of available loot
                        LootItem gunLoot = new LootItem(currentLootID,
                                            WeaponType.Gun,
                                            foundWeapon.Name,
                                            rarity,
                                            (byte)foundWeapon.ClipSize,
                                            foundWeapon.JSONIndex,
                                            lootSpots[i]);
                        LootItems.Add(currentLootID, gunLoot);

                        // ammo -- this was the real messy part as well!
                        float[] ammoSpotsX = new float[] { -7f, 7f };
                        float[] ammoSpotsY = new float[2];
                        for (int lol = 0; lol < 2; lol++)
                        {
                            int x1 = (int)(lootSpots[i].x + ammoSpotsX[1]);
                            int y1 = (int)lootSpots[i].y;
                            if (CollisionGrid[x1][y1] != CollisionType.None)
                            {
                                ammoSpotsX[0] = 0f;
                                ammoSpotsX[1] = 0f;
                                ammoSpotsY[0] = -4f;
                                ammoSpotsY[1] = 4f;
                                break;
                            }
                        } // ^^ this gets positions where we can spawn the ammo loot items in the world...

                        // ...but now we can actually spawn ammo items in
                        for (int asp = 0; asp < 2; asp++)
                        {
                            // LootItem.GiveAmount used normally again; HOWEVER: LootItem.Rarity is "ammoType".
                            currentLootID = LootCounter;
                            LootCounter++;

                            Vector2 spot = lootSpots[i] + new Vector2(ammoSpotsX[asp], ammoSpotsY[asp]);
                            LootItem ammoLoot = new LootItem(currentLootID,
                                                LootType.Ammo,
                                                $"Ammo-{foundWeapon.AmmoType}",
                                                foundWeapon.AmmoType,
                                                foundWeapon.AmmoSpawnAmount,
                                                spot);
                            LootItems.Add(currentLootID, ammoLoot);
                        }
                    }
                    else if (foundWeapon.WeaponType == WeaponType.Throwable)
                    {
                        LootItem throwLoot = new LootItem(currentLootID,
                                                WeaponType.Throwable,
                                                foundWeapon.Name,
                                                0,
                                                foundWeapon.SpawnSizeOverworld,
                                                foundWeapon.JSONIndex,
                                                lootSpots[i]);
                        LootItems.Add(currentLootID, throwLoot);

                    }
                }
            }
            //Logger.Basic("[SARLevel - LootItems] Done!");
            #endregion Loot Items

            // OK - v0.90.2
            #region PlayerSpawns
            Logger.Header("[SARLevel - PlayerSpawns] Beginning to load player spawn spots...");
            if (levelJSON["playerSpawns"] != null)
            {
                int playerSpawnCount = levelJSON["playerSpawns"].Count;
                PlayerSpawns = new List<Vector2>(playerSpawnCount);
                for (int i = 0; i < playerSpawnCount; i++)
                {
                    float x = levelJSON["playerSpawns"][i]["x"].AsFloat;
                    float y = levelJSON["playerSpawns"][i]["y"].AsFloat;
                    PlayerSpawns.Add(new Vector2(x, y));
                }
            }
            else
                Logger.Warn("[SARLevel - PlayerSpawns] key 'playerSpawns' does not exist.");
            #endregion PlayerSpawns

            // OK - v0.90.2
            #region Molecrate Spots
            Logger.Header("[SARLevel - MoleSpawns] Beginning to load molecrate spots...");
            if (levelJSON["moleSpawns"] != null)
            {
                MolecrateSpots = new Vector2[(levelJSON["moleSpawns"].Count)];
                for (int i = 0; i < MolecrateSpots.Length; i++)
                {
                    JSONNode subNode = levelJSON["moleSpawns"][i];
                    MolecrateSpots[i] = new Vector2(subNode["x"].AsFloat, subNode["y"].AsFloat);
                }
            }
            else
                Logger.Failure("[SARLevel - MoleSpawns] No such key in LevelJSON \"moleSpawns\"!");
            //Logger.Basic("[SARLevel - MoleSpawns] Done!");
            #endregion Molecrate Spots

            // OK - v0.90.2
            #region Grass
            Logger.Header("[SARLevel - Grass] Beginning to load grass...");
            if (levelJSON["grass"] != null)
            {
                int grassSpawnCount = levelJSON["grass"].Count;
                Grass = new List<GameGrass>(grassSpawnCount);
                for (int i = 0; i < grassSpawnCount; i++)
                {
                    JSONNode subNode = levelJSON["grass"][i];
                    byte id = (byte)subNode["i"].AsInt;
                    short x = (short)subNode["x"].AsFloat;
                    short y = (short)subNode["y"].AsFloat;
                    byte variation = (byte)subNode["v"].AsInt;

                    GrassType type = GrassType.GetGrassTypeFromID(id);
                    if (type == null)
                    {
                        Logger.Warn($"[SARLevel - Grass] No such GrassID \"{id}\"!");
                        continue;
                    }

                    if (variation > (type.Variations - 1))
                    {
                        Logger.Warn($"[SARLevel - Grass] Variation \"{variation}\" too high! {id}'s # of variants: {type.Variations}!");
                        continue;
                    }

                    if (type.Choppable)
                        Grass.Add(new GameGrass(type, x, y));
                    //else
                        //Logger.DebugServer($"non choppable grass @ {(x, y)}");
                }
                GrassType.NullAllGrassTypes();
            }
            else
                Logger.Failure("[SARLevel - Grass] No such key in LevelJSON \"grass\"!");
            //Logger.Basic("[SARLevel - Grass] Done!");
            #endregion Grass

            // The End
            levelJSON = null;
            GC.Collect();
            Logger.Success("[SARLevel] Finished! :]");
        }

        #region collision grid methods
        private void FreeCollisionSpot(Int32Point[] spots) // todo - replace with new version
        {
            for (int i = 0; i < spots.Length; i++)
            {
                // todo - code better
                // >>> you don't have to check if you simply make sure it's not out of bounds
                int x = spots[i].x;
                int y = spots[i].y;

                if ((x < 0 || x >= LevelWidth) || (y < 0 || y >= LevelHeight))
                    continue;
                else
                    CollisionGrid[x][y] = CollisionType.None;
            }
        }

        /// <summary>
        ///  Takes a list of points on this SARLevel's collision grid and sets them as "moveable".
        /// </summary>
        /// <param name="points"> List of points to clear.</param>
        private void FreeCollisionPoints(ref Int32Point[] points) // OK
        {
            int x, y; // assumes x-y pair is valid grid indicies
            for (int i = 0; i < points.Length; i++)
            {
                x = points[i].x;
                y = points[i].y;
                CollisionGrid[x][y] = CollisionType.None;
            }
        }

        #region player spots on the grid
        /// <summary>
        ///  Attempts to locate a collision grid position that players are able to walk on.
        /// </summary>
        /// <param name="startX"> Inital invalid x position.</param>
        /// <param name="startY"> Inital invalid y position.</param>
        /// <param name="dirX"> [NOT IMPLEMENTED] Horizontal direction the player was traveling whilst landing.</param>
        /// <param name="dirY"> [NOT IMPLEMENTED] Vertical direction the player was traveling whilst landing.</param>
        /// <returns>Vector2 representing a valid, walkable grid location.</returns>
        public Vector2 FindWalkableGridLocation(int startX, int startY) //, int dirX, int dirY <-- did not implement this yet
        {
            // max regions
            int xMin = startX, xMax = startX, yMin = startY, yMax = startY;

            // start loop
            int depth = 0, x, y;
            do
            {
                if (xMin > 0)
                    xMin -= 1;
                if (xMax < LevelWidth)
                    xMax += 1;
                if (yMax < LevelHeight)
                    yMax += 1;
                if (yMin > 0)
                    yMin -= 1;

                // goes from left to right, top to bottom. QuickIsValidPlayerLoc likely visits the same points multiple times...
                for (x = xMin; x < xMax; x++)
                {
                    // check [x][yMax]
                    if (QuickIsValidPlayerLoc(x, yMax))
                        return new Vector2(x, yMax);
                }
                for (y = yMax; y > yMin; y--) // (y = yMin; y < yMax; y++) <-- goes l-r-up
                {
                    // check [xMin][y]
                    if (QuickIsValidPlayerLoc(xMin, y))
                        return new Vector2((float)xMin, (float)y);

                    // check [xMax][y]
                    if (QuickIsValidPlayerLoc(xMax, y))
                        return new Vector2(xMax, y);
                }
                for (x = xMin; x < xMax; x++)
                {
                    // check [x][yMin]
                    if (QuickIsValidPlayerLoc(x, yMin))
                        return new Vector2(x, yMin);
                }
                depth += 1;
            } while (depth < 2124); // half level size, because searches 2x

            // all else fails
            Console.WriteLine($"Failed to locate a safe position for init {(startX, startY)} within {depth} iterations.");
            return new Vector2(508.7f, 496.7f);
        }

        /// <summary>
        ///  Determines whether the provided player position is walkable or not.
        /// </summary>
        /// <param name="playerPos"> Inital position to check.</param>
        /// <returns> True if all points surrounding are valid. False if otherwise.</returns>
        public bool IsValidPlayerLoc(ref Vector2 playerPos) // OK --> used for "QuickIsValidPlayerLoc" |
        {
            /* -- Note --
             * This function should check for mid-air positions.
             * However, the collision-height grid is not implemented currently.
             * Will do that at a later date!
             */

            // get min/ max values -- floats casted to ints should rounds towards 0, so we should be OK
            int xMin = (int)(playerPos.x - 3f);
            int yMin = (int)(playerPos.y - 1.5f);
            int xMax = (int)(playerPos.x + 3.4f);
            int yMax = (int)(playerPos.y + 1.9f);

            // took from the "quick" check
            if ((xMin < 0) || (xMax > LevelWidth) || (yMin < 0) || (yMax > LevelHeight))
                return false;

            while (xMin < xMax) // note - xMin is "x" parameter for collision grid!
            {
                for (int y = yMin; y < yMax; y++)
                {
                    if (CollisionGrid[xMin][y] != CollisionType.None)
                        return false;
                }
                xMin += 1;
            }
            return true;
        }

        /// <summary>
        ///  "Quickly" determines whether the provided  x,y position is a valid player spot
        /// </summary>
        /// <param name="startX"> X paramter of position.</param>
        /// <param name="startY"> Y paramter of position.</param>
        /// <returns> True if the x,y position is player walkable. Otherwise, false.</returns>
        public bool QuickIsValidPlayerLoc(int startX, int startY) // it's "quick" bc skipping as many type conversions as possible
        {
            // values corrected to account for floats moving numbers towards 0
            int xMin = startX - 3; // 3f is just 3 so like
            int yMin = startY - 2; // 3 - 1.9 --> 1.1 --> 1 | effectively 3 - *2*
            int xMax = startX + 4; // 5 - 3.4 --> 1.6 --> 1 | effectively 5 - *4*
            int yMax = startY + 2; // 4 - 1.9 --> 2.1 --> 2 | effectively 4 - *2*

            // if any of the values are OOB; then it's not a valid spot.
            // the map is surrounded by ocean, so no clue how this works for a custom map covering the entire level
            if ((xMin < 0) || (xMax > LevelWidth) || (yMin < 0) || (yMax > LevelHeight))
                return false;

            while (xMin < xMax) // note - xMin is "x" parameter for collision grid!
            {
                for (int y = yMin; y < yMax; y++)
                {
                    if (CollisionGrid[xMin][y] != CollisionType.None)
                        return false;
                }
                xMin += 1;
            }
            return true;
        }
        #endregion player spots on the grid

        #endregion collision grid methods

        // todo - cleaning doodads up
        // it seems there's some sort of chain-explosions with barrels, but it difficult to follow/ understand whether...
        // ...it's really "going deep" or is just like a 1-level deep check and any other potential explosions are skipped.
        #region Doodad Junk
        /// <summary>
        /// Attempts to locate a Doodad at the provided position.
        /// </summary>
        /// <param name="checkPosition">The position to check for hits at.</param>
        /// <param name="foundDoodads">An array containing any found Doodads.</param>
        /// <param name="remove">Whether to remove any found Doodads from this SARLevel's Doodad list.</param>
        /// <returns>True, along with any found Doodads; Otherwise, False and NULL.</returns>
        public bool TryDestroyingDoodad(Vector2 checkPosition, out Doodad[] foundDoodads, bool remove)
        {
            // noticed potential optimization? if a HitSpot can only ever hit one Doodad (they never overlap)...
            // ... it should then be possible to skip checking all other Doodads; because we found the only one...
            // ... that will ever be on at particular HitSpot.

            // Explosive Barrels damaging Players & Hamsterballs is calculated by the Match itself if this method returns any Doodads
            foundDoodads = null;
            if (Doodads == null)
            {
                Logger.DebugServer("[SARLevel - TryDestroyingDoodad] Doodads was null!");
                return false;
            }

            // iterate through every. single. [destructible] doodad. all 3,127 to be exact!
            int numOfDoodads = Doodads.Count;
            //Logger.DebugServer($"Num of doodads: {numOfDoodads}");
            List<Doodad> listODoodads = new List<Doodad>(8); // 8 is arbitrary; just wanted an inital length that wasn't other than "1".
            for (int i = 0; i < numOfDoodads; i++)
            {
                // simpliest check: is HitSpot just Doodad[i]'s origin?
                if (Doodads[i].Position.IsNear(checkPosition, 1.5f))
                {
                    listODoodads.Add(Doodads[i]);

                    // does this Doodad explode? if so, what will we hit? (similar concept to this method)
                    if (Doodads[i].Type.DestructibleDamageRadius > 0)
                    {
                        if (TryExplodingDoodad(Doodads[i], out Doodad[] founds))
                        {
                            for (int l = 0; l < founds.Length; l++)
                            {
                                listODoodads.Add(founds[l]);
                            }
                        }
                    }
                    continue;
                }

                // second check: is HitSpot just a spot somewhere on Doodad[i]? (suuuper slow bc. we check ALL of them)
                for (int j = 0; j < Doodads[i].HittableSpots.Length; j++)
                {
                    if (checkPosition.IsNear(Doodads[i].HittableSpots[j], 1.5f))
                    {
                        listODoodads.Add(Doodads[i]);

                        // (copied explosion check code from simple origin-point check)
                        if (Doodads[i].Type.DestructibleDamageRadius > 0)
                        {
                            if (TryExplodingDoodad(Doodads[i], out Doodad[] founds))
                            {
                                for (int l = 0; l < founds.Length; l++)
                                {
                                    listODoodads.Add(founds[l]);
                                }
                            }
                        }
                        break; // move onto the next Doodad in the list.
                    }
                }
            }

            // return to the caller whether we've found any Doodads at this check spot
            if (listODoodads.Count > 0) // If found any doodads... return them!
            {
                foundDoodads = listODoodads.ToArray();
                if (remove)
                {
                    FreeDoodadCollisionSpots(foundDoodads);
                    for (int i = 0; i < foundDoodads.Length; i++)
                        Doodads.Remove(foundDoodads[i]);
                }
                return true;
            }
            return false; // only if nothing is found of course
        }

        /// <summary>
        /// Simulates this Doodad's explosiion by searching for any surrounding Doodads that are within its blast radius.
        /// </summary>
        /// <param name="thisDoodad">The Doodad that "exploded".</param>
        /// <param name="blowups">An array containing any found Doodads.</param>
        /// <returns>True, along with the hit Doodads; Otherwise, False along wtih NULL.</returns>
        private bool TryExplodingDoodad(Doodad thisDoodad, out Doodad[] blowups)
        {
            // TOOD: chain explosions ?
            // the inital thing noticed is that ONLY thisDoodad is ignored
            // any other Doodads seen up until this point that have been marked/ ignored are checked again!

            blowups = null;
            int numOfDoodads = Doodads.Count;
            List<Doodad> listODoodads = new List<Doodad>(8); // 8 is arbitrary
            for (int i = 0; i < numOfDoodads; i++)
            {
                if (Doodads[i] == thisDoodad)
                    continue;

                if (thisDoodad.Position.IsNear(Doodads[i].Position, thisDoodad.Type.DestructibleDamageRadius))
                {
                    listODoodads.Add(Doodads[i]);
                    // if (canExplode) chain();
                    continue;
                }
            }

            if (listODoodads.Count > 0)
            {
                blowups = listODoodads.ToArray();
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Frees the collision spots taken up by the given Doodads.
        /// </summary>
        /// <param name="doodads">Array of Doodads that must have their collision spots removed from this SARLevel's CollisionGrid.</param>
        private void FreeDoodadCollisionSpots(Doodad[] doodads)
        {
            for (int i = 0; i < doodads.Length; i++)
            {
                for (int j = 0; j < doodads[i].HittableSpots.Length; j++)
                {
                    int x = (int)doodads[i].HittableSpots[j].x;
                    int y = (int)doodads[i].HittableSpots[j].y;
                    if ((x >= LevelWidth) || (y >= LevelHeight))
                        continue;

                    CollisionGrid[x][y] = CollisionType.None;
                }
            }
        }
        #endregion Doodad Junk

        #region LootItems
        /// <summary>
        ///  Attempts to locate a position that is a valid loot grid position.
        /// </summary>
        /// <param name="pPoint"> The inital point to try and spawn an item.</param>
        /// <returns>Vector2 representing a found grid location. Note: it will be about the inital point if no other valid point can be found!</returns>
        private Vector2 FindOKLootSpotFromPoint(ref Vector2 pPoint)
        {
            // align pPoint with loot grid (every 5n)
            int x = (int)pPoint.x;
            int y = (int)pPoint.y;
            int rX = x % 5;
            int rY = y % 5;
            if (rX > 0)
                x -= rX;
            if (rY > 0)
                y -= rY;

            if (QuickIsValidPlayerLoc(x, y) && NoItemAtThisSpot(x, y))
                return new Vector2(x, y);

            int xMin = x, xMax = x;
            int yMin = y, yMax = y;
            int depth = 0;
            int i, j;
            do
            {
                if (xMin >= 5)
                    xMin -= 5;
                if (xMax < LevelWidth - 1)
                    xMax += 5;
                if (yMin >= 5)
                    yMin -= 5;
                if (yMax < LevelHeight - 1)
                    yMax += 5;

                // check orig, maxY
                if (QuickIsValidPlayerLoc(x, yMax) && NoItemAtThisSpot(x, yMax))
                    return new Vector2(x, yMax);

                // alternate between the right/ left "splits"
                i = j = x; // weird reuse of I and J; I = xCoord; J = yCoord
                while ((i > xMin) && (j < xMax))
                {
                    // right split
                    j += 5;
                    if (QuickIsValidPlayerLoc(j, yMax) && NoItemAtThisSpot(j, yMax))
                        return new Vector2(j, yMax);

                    // left split
                    i -= 5;
                    if (QuickIsValidPlayerLoc(i, yMax) && NoItemAtThisSpot(i, yMax))
                        return new Vector2(i, yMax);
                } // this is really stupid, but this was the easiest/ first thought that came to mind

                // go from (xMax, yMax) --> (xMax, yMin)
                for (j = yMax; j > yMin; j -= 5)
                {
                    if (QuickIsValidPlayerLoc(xMax, j) && NoItemAtThisSpot(xMax, j))
                        return new Vector2(xMax, j);
                }

                // go from (xMax, yMin) --> (xMin, yMin)
                for (i = xMax; i > xMin; i -= 5)
                {
                    if (QuickIsValidPlayerLoc(i, yMin) && NoItemAtThisSpot(i, yMin))
                        return new Vector2(i, yMin);
                }

                // go from (xMin, yMin) --> (xMin, yMax)
                for (j = yMin; j < yMax; j += 5)
                {
                    if (j == yMax)
                        continue;

                    if (QuickIsValidPlayerLoc(xMin, j) && NoItemAtThisSpot(xMin, j))
                        return new Vector2(xMin, j);
                }
                depth += 1;
            } while (depth < 4); // testing in modern versions shows it'll try making a 9x9 area (so 4 each side)
            //Console.WriteLine($"Failed to locate a safe position for init {(x, y)} within {depth} iterations.");
            return new Vector2(x, y);
        }

        /// <summary>
        ///  Determines whether there is an item at the specified location already.
        /// </summary>
        /// <param name="pX"> x parameter.</param>
        /// <param name="pY"> y parameter.</param>
        /// <returns> True if no LootItem objects are found at the specified location. Otherwise, False.</returns>
        private bool NoItemAtThisSpot(int pX, int pY)
        {
            int x, y, rX, rY;
            foreach (LootItem item in LootItems.Values)
            {
                x = (int)item.Position.x;
                y = (int)item.Position.y;
                rX = x % 5;
                rY = y % 5;
                if (rX > 0)
                    x -= rX;
                if (rY > 0)
                    y -= rY;
                if ((pX == x) && (pY == y))
                    return false;
            }
            return true;
        }

        // Checks whether the provided LootItem (pItem)'s LootID exists in the this.LootItems dictionary. If so, then pItem is removed from this.LootItems.
        public void RemoveLootItem(LootItem item)
        {
            if (LootItems.ContainsKey(item.LootID))
                LootItems.Remove(item.LootID);
            else
                Logger.DebugServer($"Level.RemoveLootItem: Item.LootID \"{item.LootID}\" does not exist in Level.LootItems!");
        }

        // Creates a new "Juice" LootItem and adds it to this.LootItems.
        public LootItem NewLootJuice(byte pCount, Vector2 pTryPos) // appears OK
        {
            pTryPos = FindOKLootSpotFromPoint(ref pTryPos);
            LootCounter += 1;
            LootItem retLoot = new LootItem(LootCounter, LootType.Juice, $"Health Juice-{pCount}", 0, pCount, pTryPos);
            LootItems.Add(LootCounter, retLoot);
            return retLoot;
        }
        
        // Creates a new "Tape" LootItem and adds it to this.LootItems.
        public LootItem NewLootTape(byte pCount, Vector2 pTryPos) // appears OK
        {
            pTryPos = FindOKLootSpotFromPoint(ref pTryPos);
            LootCounter += 1;
            LootItem retLoot = new LootItem(LootCounter, LootType.Tape, $"Tape-{pCount}", 0, pCount, pTryPos);
            LootItems.Add(LootCounter, retLoot);
            return retLoot;
        }

        // Creates a new "Armor" LootItem and adds it to this.LootItems.
        public LootItem NewLootArmor(byte pArmorLevel, byte pArmorTicks, Vector2 pTryPos) // appears OK
        {
            pTryPos = FindOKLootSpotFromPoint(ref pTryPos);
            LootCounter += 1;
            LootItem retLoot = new LootItem(LootCounter, LootType.Armor, $"Armor-{pArmorLevel}/{pArmorTicks}", pArmorLevel, pArmorTicks, pTryPos);
            LootItems.Add(LootCounter, retLoot);
            return retLoot;
        }

        // Creates a new "Weapon" LootItem and adds it to this.LootItems.
        public LootItem NewLootWeapon(int pWepIndex, byte pRarity, byte pAmmo, Vector2 pTryPos) // appears OK | haven't tested fully with throwables
        {
            Weapon weapon = Weapon.GetWeaponFromID(pWepIndex);
            pTryPos = FindOKLootSpotFromPoint(ref pTryPos);
            LootCounter += 1;
            LootItem retLoot = new LootItem(LootCounter, weapon.WeaponType, weapon.Name, pRarity, pAmmo, pWepIndex, pTryPos);
            LootItems.Add(LootCounter, retLoot);
            return retLoot;
        }

        // Creates a new "Ammo" LootItem and adds it to this.LootItems.
        public LootItem NewLootAmmo(byte pAmmoType, byte pCount, Vector2 pTryPos) // appears OK
        {
            pTryPos = FindOKLootSpotFromPoint(ref pTryPos);
            LootCounter += 1;
            LootItem retLoot = new LootItem(LootCounter, LootType.Ammo, $"Ammo-{pAmmoType}", pAmmoType, pCount, pTryPos); // Rarity = AmmoType; GiveAmount = numOfAmmo
            LootItems.Add(LootCounter, retLoot);
            //Logger.DebugServer($"LootCounter: {LootCounter}; Contains? {LootItems.ContainsKey(LootCounter)}");
            return retLoot;
        }
        #endregion LootItems

        //public void MolecrateLoot() {};

        /// <summary> Attempts to locate a "grass patch" at the provided location. </summary>
        /// <param name="searchX">The x position to search at.</param>
        /// <param name="searchY">The y position to search at.</param>
        /// <returns>A GameGrass object representing the found path; Otherwise, NULL is returned.</returns>
        public GameGrass GetGrassAtSpot(short searchX, short searchY)
        {
            int grassCache = Grass.Count;
            for (int i = 0; i < grassCache; i++)
                if (Grass[i].X == searchX && Grass[i].Y == searchY)
                    return Grass[i];
            return null;
        }

        /// <summary> Attempts to remove the provided grass patch from this SARLevel's Grass list. </summary>
        /// <param name="item">GameGrass object to attempt to remove.</param>
        public void RemoveGrassFromList(GameGrass item)
        {
            if (!Grass.Remove(item))
                Logger.Failure("[SARLevel - Error] Was unable to remove the provided grass item from the list...");
        }

        /// <summary>
        /// Attempts to open up The Rebellion hideout located in Super Animal Farm.
        /// </summary>
        public Int32Point[] OpenRebelSpot() // Works with minor modifications; Wouldn't bother with it right now though.
        {
            List<Int32Point> collisionSpots = new List<Int32Point>(7000);
            if (_rebelHideoutDoor == null) Logger.Warn("[OpenRebelSpot] _rebelHideoutDoor Doodad does not exist in this SARLevel! Are you missing it in the level data, or has the ID changed?");
            else
            {
                for (int i = 0; i < _rebelHideoutDoor.HittableSpots.Length; i++)
                {
                    Int32Point newSpot = new Int32Point((int)_rebelHideoutDoor.HittableSpots[i].x, (int)_rebelHideoutDoor.HittableSpots[i].y);
                    collisionSpots.Add(newSpot);
                    // If you factor in collisionHeight while setting up Doodad.Hittable spots ( origin - height // origin + max + height)
                    // Then this actually works fully as invisioned. However, according to current understandings, this doesn't appear to be the correct...
                    // ...way to be using collisionHeight. Instead, there is another separate collisionGrid for CollisionHeights which appears to be used...
                    // ...while calculating whether projectiles hit things as well... which overall makes this a big ol' NoItemAtThisSpot.
                    // So, may just add collisionHeight into the actual offsets and call it a day. who knows!
                    Logger.Basic($"Added spot: {newSpot.x}, {newSpot.y}");
                }
                _rebelHideoutDoor = null;
            }
            for (int x = 0; x < SARConstants.BarnHideoutTarpSizeX; x++)
            {
                for (int y = 0; y < SARConstants.BarnHideoutTarpSizeY; y++)
                {
                    collisionSpots.Add(new Int32Point(_barnTarpSpawn.x + x, _barnTarpSpawn.y + y));
                }
            }
            Int32Point[] ret = collisionSpots.ToArray(); // am lazy and it works good enough.
            Logger.missingHandle($"ShorrMax: {short.MaxValue}; retLength: {ret.Length}");
            FreeCollisionSpot(ret); // double lazy
            return ret;
        }

        // todo - generating molecrate paths and junk
        #region Molecrate
        /*public void RebuildMolecrateSpawnOptions(Vector2 pSafezoneOrigin, float pRadius)
        /// <summary>
        /// Rebuilds this level's MolecrateSpots field to only include spots within the current safezone.
        /// </summary>
        /// <param name="pSafezoneOrigin">Safezone origin position.</param>
        /// <param name="pRadius">Safezone circle radius.</param>
        {
            List<Vector2> newSpawns = new List<Vector2>(MolecrateSpots.Length);
            for (int i = 0; i < MolecrateSpots.Length; i++)
            {
                Vector2 dist = MolecrateSpots[i] - pSafezoneOrigin;
                float magnitude = dist.magnitude;
                if (magnitude < pRadius) newSpawns.Add(MolecrateSpots[i]);
            }
            MolecrateSpots = newSpawns.ToArray();
        }*/

        // generating molecrate spawn spots is a lot more difficult than anticipated...
        /*public Vector2[] GenerateMolecratePath(Vector2 pOrigin)
        {
            List<Vector2> movePoints = new List<Vector2>(8);
            MersenneTwister twist = new MersenneTwister((uint)DateTime.UtcNow.Ticks);
            switch (twist.NextUInt(0, 4))
            {

            }
            while (twist.NextUInt(0, 8) < 6) // ~2/8 to stop
            {

            }
            return movePoints.ToArray();
        }*/
        #endregion Molecrate

        #region SARLevel disposing stuff
        /// <summary>
        /// Disposes all other fields in this SARLevel other than the CollisionGrid.
        /// </summary>
        public void NullUnNeeds()
        {
            if (Campfires != null)
                Campfires = null;

            if (MolecrateSpots != null)
                MolecrateSpots = null;

            if (Coconuts != null)
                Coconuts = null;

            if (Hamsterballs != null)
                Hamsterballs = null;
            GC.Collect();
        }

        /// <summary>
        /// Disposes of this SARLevel; not to be confuzed with "NullUnuseds".
        /// </summary>
        public void Dispose()
        {
            if (CollisionGrid != null)
                CollisionGrid = null;

            if (Doodads != null)
                Doodads = null;

            if (Campfires != null)
                Campfires = null;

            if (LootItems != null)
                LootItems = null;

            if (MolecrateSpots != null)
                MolecrateSpots = null;

            if (Coconuts != null)
                Coconuts = null;

            if (Hamsterballs != null)
                Hamsterballs = null;
        }
        ~SARLevel()
        {
            Logger.DebugServer("[SARLevel] Finalize?");
            Dispose();
            Logger.DebugServer("[SARLevel] Finialize finished.");
        }
        #endregion SARLevel disposing stuff
    }
}
