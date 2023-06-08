using SimpleJSON;
using System;
using System.Collections.Generic;
using System.IO;
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
        /// The seed used by this SARLevel to generate the inital LootItem list.
        /// </summary>
        public readonly uint LootSeed;

        /// <summary>
        /// The seed used by this SARLevel to generate the Coconut list.
        /// </summary>
        public readonly uint CoconutSeed;

        /// <summary>
        /// The seed used by this SARLevel to generate the Hamsterball list.
        /// </summary>
        public readonly uint HamsterballSeed;


        private Doodad _rebelHideoutDoor;   // Sliding door that gets moved around and stuff!
        private Int32Point _barnTarpSpawn;  // Inital position of the barn tarp
        private const int tarpSizeX = 97;   // barn tarp width
        private const int tarpSizeY = 67;   // barn tarp height
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
            #region LoadJSON
            string search = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\datafiles\earlyaccessmap1.txt";
            if (!File.Exists(search))
            {
                Logger.Failure($"Failed to locate \"earlyaccessmap1.txt\"!\nSearched: {search}");
                Environment.Exit(19); // 19 = map data; 20 = tiles; 21 = decals; 22 = doodads; 23 = weapons (sorta based on the order loaded here)
            }
            JSONNode levelJSON = JSONNode.LoadFromFile(search);
            #endregion LoadJSON

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
                // Bsae values
                int id = tileNode["i"].AsInt;
                Tile tile = Tile.GetTileFromID(id);
                if (tile == null)
                {
                    Logger.Warn($"[SARLevel - Tiles] TileID \"{id}\" gave a null return.");
                    continue;
                }
                // Initial Position & Size (unsure why it is multiplied by 9, but it certainly has to!)
                Int32Point position = new Int32Point(tileNode["x"].AsInt * 9, tileNode["y"].AsInt * 9);
                Int32Point size = new Int32Point(tileNode["w"].AsInt * 9, tileNode["h"].AsInt * 9);

                // Place CollisionPoints
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
            Tile.NullAllTiles(); // Clear AllTiles from memory.
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
                                if ((x < LevelWidth) && (y < LevelHeight)) CollisionGrid[x][y] = CollisionType.None;
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
                } else if (doodad.Destructible) Doodads.Add(new Doodad(doodad, new Vector2(subNode["x"].AsFloat, subNode["y"].AsFloat)));
                else if (doodad.DoodadID == 5282)
                {
                    _rebelHideoutDoor = new Doodad(doodad, new Vector2(subNode["x"].AsFloat, subNode["y"].AsFloat));
                }
                if (doodad.MovementCollisionPts != null) // Array version as opposed to the List<T> one
                {
                    for (int j = 0; j < doodad.MovementCollisionPts.Length; j++)
                    {
                        int x = doodadPosition.x + doodad.MovementCollisionPts[j].x;
                        int y = doodadPosition.y + doodad.MovementCollisionPts[j].y;
                        if ((x < LevelWidth) && (y < LevelHeight))
                        {
                            // HeightGrid stuff here too, but not handled
                            if (CollisionGrid[x][y] != CollisionType.MovementAndSight) CollisionGrid[x][y] = CollisionType.Movement;
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

            Campfires = new Campfire[levelJSON["campfires"].Count];
            for (int i = 0; i < Campfires.Length; i++)
            {
                JSONNode subNode = levelJSON["campfires"][i];
                Vector2 position = new Vector2(subNode["x"].AsFloat, subNode["y"].AsFloat);
                Campfires[i] = new Campfire(position);

                int x1 = (int)position.x - 6;
                int y1 = (int)position.y - 4;
                int x2 = (int)position.x + 6;
                int y2 = (int)position.y + 6;
                for (int x = x1; x <= x2; x++) // unsure what is being achieved right now and really don't feel like messing things up.
                {
                    for (int y = y1; y <= y2; y++)
                    {
                        if (x != x1 || y != y1)
                        {
                            if (x != x2 || y != y1)
                            {
                                if (x != x1 || y != y2)
                                {
                                    if (x != x2 || y != y2)
                                    {
                                        if ((x < LevelWidth) && (y < LevelHeight)) CollisionGrid[x][y] = CollisionType.Movement;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            //Logger.Basic("[SARLevel - Campfires] Done!");
            #endregion Campfires

            // OK - v0.90.2
            #region BarnTarp
            Logger.Header("[SARLevel - BarnTarp] Beginning to load barn tarp...");
            if (levelJSON["barnTarp"] != null)
            {
                Int32Point tarpSpawn = new Int32Point(levelJSON["barnTarp"]["x"].AsInt, levelJSON["barnTarp"]["y"].AsInt);
                _barnTarpSpawn = tarpSpawn;
                int tarpSpawnOffsetX = tarpSpawn.x + tarpSizeX; // 97
                int tarpSpawnOffsetY = tarpSpawn.y + tarpSizeY; // 67
                for (int x = tarpSpawn.x; x < tarpSpawnOffsetX; x++)
                {
                    for (int y = (tarpSpawn.y - 11); y < tarpSpawnOffsetY; y++)
                    {
                        if ((x < LevelWidth) && (y < LevelHeight) && (CollisionGrid[x][y] != CollisionType.MovementAndSight)) CollisionGrid[x][y] = CollisionType.Movement;
                    }
                }
            }
            else Logger.Failure("[SARLevel - BarnTarp] Key \"barnTarp\" does not exist.");
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
            else Logger.Failure($"[SARLevel - Coconuts] No key \"coconuts\"!");
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
            else Logger.Failure($"[SARLevel - Hamsterballs] No key \"vehicles\"!");
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
            // RNG Stuff Still-- This is used for generating Weapons
            List<short> WeaponsByFrequency = new List<short>();
            Weapon[] allWeapons = Weapon.GetAllWeaponTypes();
            for (int i = 0; i < allWeapons.Length; i++)
            {
                for (int j = 0; j < allWeapons[i].SpawnFrequency; j++)
                {
                    WeaponsByFrequency.Add(allWeapons[i].JSONIndex);
                }
            }

            // Go through every loot tile and try spawning something in!
            for (int i = 0; i < totalSpawnSpots; i++)
            {
                // Set LootIDs!
                currentLootID = LootCounter;
                LootCounter++; // always increments no matter way ig
                doBetterGen = false;
                minGenNum = 0U;

                // See if this tile is a better loot tile...
                if (i >= lootSpotsNormal)
                {
                    doBetterGen = true;
                    minGenNum = 20U;
                }
                // Now roll!
                rngNum = mersenneTwister.NextUInt(minGenNum, 100U);

                // Figure out what to do/spawn
                if (rngNum <= 33.0) continue; // 33 or less? You get NOTHING! (maybe) (maybe also || (rngNum > 59 && rngNum <= 60))?
                if (rngNum > 33.0 && rngNum <= 47.0) // Drinks
                {
                    byte juiceAmount = 40;

                    if (doBetterGen) minGenNum = 15U;
                    rngNum = mersenneTwister.NextUInt(minGenNum, 100U);
                    if (rngNum <= 55) juiceAmount = 10;
                    else if (rngNum <= 89) juiceAmount = 20;

                    LootItem drinkLoot = new LootItem(currentLootID, LootType.Juice, $"Health Juice-{juiceAmount}", 0, juiceAmount, lootSpots[i]);
                    LootItems.Add(currentLootID, drinkLoot);
                }
                else if (rngNum <= 59.0) // Armor
                {
                    if (doBetterGen) minGenNum = 24U;
                    rngNum = mersenneTwister.NextUInt(minGenNum, 100U);

                    byte armorTier = 3;
                    if (rngNum <= 65.0) armorTier = 1;
                    else if (rngNum <= 92.0) armorTier = 2;

                    // LootItems.Rarity = armor tier; LootItems.GiveAmout = amount of armor ticks.
                    LootItem armorLoot = new LootItem(currentLootID, LootType.Armor, $"Armor-{armorTier}", armorTier, armorTier, lootSpots[i]);
                    LootItems.Add(currentLootID, armorLoot);
                }
                else if (rngNum > 60.0 && rngNum <= 66.0) // Tape
                {
                    LootItem tapeLoot = new LootItem(currentLootID, LootType.Tape, "Tape (x1)", 0, 1, lootSpots[i]);
                    LootItems.Add(currentLootID, tapeLoot);
                }
                else if (rngNum > 66.0) // Weapon Generation toiemm!! -- The .GiveAmount property becomes the amount to give/amount of ammo the gun has.
                {
                    // Find le weapon
                    rngNum = mersenneTwister.NextUInt(0U, (uint)WeaponsByFrequency.Count);
                    int leGenIndex = WeaponsByFrequency[(int)rngNum];
                    Weapon foundWeapon = allWeapons[leGenIndex];

                    // Make Le Weapon
                    if (foundWeapon.WeaponType == WeaponType.Gun)
                    {
                        // Figure out rarity
                        byte rarity = 0;
                        if (doBetterGen) minGenNum = 22U;
                        minGenNum = mersenneTwister.NextUInt(minGenNum, 100U);
                        // See if should gen higher-rarity item...
                        if (minGenNum > 58.0 && minGenNum <= 80.0) rarity = 1;
                        else if (minGenNum > 80.0 && minGenNum <= 91.0) rarity = 2;
                        else if (minGenNum > 91.0 && minGenNum <= 97.0) rarity = 3;
                        // Verify rarity isn't too high nor low...
                        if (rarity > foundWeapon.RarityMaxVal) rarity = foundWeapon.RarityMaxVal;
                        if (rarity < foundWeapon.RarityMinVal) rarity = foundWeapon.RarityMinVal;
                        // Make gun...
                        LootItem gunLoot = new LootItem(currentLootID, WeaponType.Gun, foundWeapon.Name, rarity, (byte)foundWeapon.ClipSize, foundWeapon.JSONIndex, lootSpots[i]);
                        LootItems.Add(currentLootID, gunLoot);
                        // Spawn in ammo as welll
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
                        }
                        for (int asp = 0; asp < 2; asp++)
                        {
                            // LootItems.GiveAmount stays the same, BUT! LootItems.Rarity is the ammo type!!
                            currentLootID = LootCounter;
                            LootCounter++;
                            Vector2 spot = lootSpots[i] + new Vector2(ammoSpotsX[asp], ammoSpotsY[asp]);
                            LootItem ammoLoot = new LootItem(currentLootID, LootType.Ammo, $"Ammo-{foundWeapon.AmmoType}", foundWeapon.AmmoType, foundWeapon.AmmoSpawnAmount, spot);
                            LootItems.Add(currentLootID, ammoLoot);
                        }
                    }
                    else if (foundWeapon.WeaponType == WeaponType.Throwable)
                    {
                        LootItem throwLoot = new LootItem(currentLootID, WeaponType.Throwable, foundWeapon.Name, 0, foundWeapon.SpawnSizeOverworld, foundWeapon.JSONIndex, lootSpots[i]);
                        LootItems.Add(currentLootID, throwLoot);

                    }
                }
            }
            //Logger.Basic("[SARLevel - LootItems] Done!");
            #endregion Loot Items

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
            else Logger.Failure("[SARLevel - MoleSpawns] No such key in LevelJSON \"moleSpawns\"!");
            //Logger.Basic("[SARLevel - MoleSpawns] Done!");
            #endregion Molecrate Spots

            // OK - v0.90.2
            #region Grass
            Logger.Header("[SARLevel - Grass] Beginning to load grass...");
            if (levelJSON["grass"] != null)
            {
                int grassCache = levelJSON["grass"].Count;
                this.Grass = new List<GameGrass>(grassCache);
                for (int i = 0; i < grassCache; i++)
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
                    if (type.Choppable) Grass.Add(new GameGrass(type, x, y));
                    //else Logger.DebugServer($"non choppable grass @ {(x, y)}");
                }
                GrassType.NullAllGrassTypes();
            }
            else Logger.Failure("[SARLevel - Grass] No such key in LevelJSON \"grass\"!");
            //Logger.Basic("[SARLevel - Grass] Done!");
            #endregion Grass

            // The End
            levelJSON = null;
            GC.Collect();
            Logger.Success("[SARLevel] Finished! :]");
        }

        #region collision grid methods
        /// <summary>
        /// Determines whether the provided position is a walkable spot on the collision grid or not.
        /// </summary>
        /// <param name="position">Position to check against.</param>
        /// <returns>True if the spot is walkable; False if  otherwise.</returns>
        private bool IsGridSpotWalkable(Vector2 position) // Again, supposed to have in-air stuff here, but we don't.
        {
            int x = (int)position.x;
            int y = (int)position.y;
            if (x < 0 || x >= LevelWidth) return false;
            if (y < 0 || y >= LevelHeight) return false;
            if (CollisionGrid[x][y] != CollisionType.None) return false;
            return true;
        }

        /// <summary>
        /// Determines whether this player position is valid or not.
        /// </summary>
        /// <param name="position">Player position to check.</param>
        /// <returns>True if the spot is valid; False if otherwise.</returns>
        public bool IsThisPlayerSpotValid(Vector2 position)
        {
            // There is potential for checking in-air spots; however that is not implemented here.
            float x1 = position.x - 3f;
            float y1 = position.y - 1.5f;
            float xStore1 = x1;
            float yStore1 = y1;
            float max_x = position.x + 3.4f;
            float max_y = position.y + 1.9f;
            while (xStore1 < max_x)
            {
                while (yStore1 < max_y)
                {
                    if (!IsGridSpotWalkable(new Vector2(xStore1, yStore1))) return false;
                    else yStore1 += 1f;
                }
                xStore1 += 1f;
                yStore1 = y1;
            }
            return true;
        }

        private void FreeCollisionSpot(Vector2[] spots)
        {
            for (int i = 0; i < spots.Length; i++)
            {
                int x = (int)spots[i].x;
                int y = (int)spots[i].y;
                if ((x < 0 || x >= LevelWidth) || (y < 0 || y >= LevelHeight)) continue;
                else CollisionGrid[x][y] = CollisionType.None;
            }
        }

        private void FreeCollisionSpot(Int32Point[] spots)
        {
            for (int i = 0; i < spots.Length; i++)
            {
                int x = spots[i].x;
                int y = spots[i].y;
                if ((x < 0 || x >= LevelWidth) || (y < 0 || y >= LevelHeight)) continue;
                else CollisionGrid[x][y] = CollisionType.None;
            }
        }

        #region player spots on the grid
        /// <summary>
        /// Attempts to locate a valid player position given an inital invalid starting position.
        /// </summary>
        /// <param name="init">The initial starting position to begin searching around.</param>
        /// <param name="xDir">[Unused] Search direction to lean towards on the X-axis.</param>
        /// <param name="yDir">[Unused] Search direction to lean towards on the Y-axis.</param>
        /// <returns></returns>
        public Vector2 FindValidPlayerPosition(Vector2 init, float xDir, float yDir)
        {
            Int32Point initPos = new Int32Point((int)init.x, (int)init.y);
            Vector2 foundPosition = new Vector2(0f, 0f);
            bool found = false;
            int searchMagnitude = 1;
            int maxX, maxY, minX, minY;
            while (!found)
            {
                searchMagnitude++;
                minX = initPos.x - searchMagnitude;
                maxX = initPos.x + searchMagnitude;
                minY = initPos.y - (searchMagnitude - 1);
                maxY = initPos.y + (searchMagnitude - 1);
                if (SearchX(minX, maxX, maxY, out foundPosition)) break;
                if (SearchX(minX, maxX, minY, out foundPosition)) break;
                if (SearchY(minY, maxY, maxX, out foundPosition)) break;
                if (SearchY(minY, maxY, minX, out foundPosition)) break;
            }
            return foundPosition;
        }

        private bool SearchX(int xMin, int xMax, int yLevel, out Vector2 foundSpot) // searches from x_min to x_max for a valid spot.
        {
            foundSpot = new Vector2(xMin, yLevel);
            for (; xMin < xMax; xMin++)
            {
                foundSpot.x = xMin;
                if (IsThisPlayerSpotValid(foundSpot)) return true;
            }
            return false;
        }
        private bool SearchY(int yMin, int yMax, int xSpot, out Vector2 foundSpot) // searches from y_min to y_max for a valid spot
        {
            foundSpot = new Vector2(xSpot, yMin);
            for (; yMin < yMax; yMin++)
            {
                foundSpot.y = yMin;
                if (IsThisPlayerSpotValid(foundSpot)) return true;
            }
            return false;
        }
        #endregion player spots on the grid

        #region ItemPosition
        // can obviously improve
        public bool IsThereAnItemhere(Vector2 position)
        {
            foreach (LootItem item in LootItems.Values)
            {
                if ((Math.Abs(item.Position.x - position.x) <= 4) && (Math.Abs(item.Position.y - position.y) <= 4)) return true;
            }
            return false;
        }

        // can obviously improve
        public Vector2 FindNewItemPosition(Vector2 initalSpot) // basically a copy and paste of the player version tbh!
        {
            // I am too stupid to make a better "algorithm" right now. this works fine-enough, but it's very obvious something is wrong...
            Int32Point initPos = new Int32Point((int)initalSpot.x, (int)initalSpot.y);
            if (initPos.x % 4 > 0) initPos.x = 4 * (initPos.x / 4);
            if (initPos.y % 4 > 0) initPos.y = 4 * (initPos.y / 4);
            Vector2 foundPosition = new Vector2(0f, 0f);
            bool found = false;
            int searchMagnitude = 0;
            int maxX, maxY, minX, minY;
            while (!found)
            {
                searchMagnitude++;
                minX = initPos.x - (4 * searchMagnitude);
                if (minX < 0) minX = 0;
                maxX = initPos.x + (4 * searchMagnitude);
                if (maxX > CollisionGrid.Length) maxX = CollisionGrid.Length;
                minY = initPos.y - (4 * (searchMagnitude - 1));
                if (minY < 0) minY = 0;
                maxY = initPos.y + (4 * (searchMagnitude - 1));
                //Logger.DebugServer($"{searchMagnitude} minY: {minY}; maxY: {maxY}");
                if (maxY > LevelHeight) maxY = LevelHeight;
                if (ItemSearchY(minY, maxY, minX, out foundPosition)) break;
                if (ItemSearchX(minX, maxX, maxY, out foundPosition)) break;
                if (ItemSearchX(minX, maxX, minY, out foundPosition)) break;
                if (ItemSearchY(minY, maxY, maxX, out foundPosition)) break;
            }
            //Logger.Success($"Found: {foundPosition}");
            //Logger.DebugServer($"Start: {initalSpot}\nSearchStart: {initPos}\nFound: {foundPosition}");
            return foundPosition;
        }
        private bool ItemSearchX(int xMin, int xMax, int yLevel, out Vector2 foundSpot) // much mess yes
        {
            foundSpot = new Vector2(xMin, yLevel);
            for (; xMin < xMax; xMin += 4)
            {
                foundSpot.x = xMin;
                if (IsThisPlayerSpotValid(foundSpot) && !IsThereAnItemhere(foundSpot) && (CollisionGrid[xMin][yLevel] == CollisionType.None)) return true;
            }
            return false;
        }
        private bool ItemSearchY(int yMin, int yMax, int xSpot, out Vector2 foundSpot) // much mess yes
        {
            foundSpot = new Vector2(xSpot, yMin);
            for (; yMin < yMax; yMin += 4)
            {
                foundSpot.y = yMin;
                if (IsThisPlayerSpotValid(foundSpot) && !IsThereAnItemhere(foundSpot) && (CollisionGrid[xSpot][yMin] == CollisionType.None)) return true;
            }
            return false;
        }
        #endregion ItemPosition

        #endregion collision grid methods

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
                // first, check if Hit Spot is just the Doodad's origin. if it is, then that is all there is to this check.
                if (Vector2.ValidDistance(Doodads[i].Position, checkPosition, 1.5f, true))
                {
                    listODoodads.Add(Doodads[i]); // add to list of Doodads we've hit

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
                    continue; // move onto the next Doodad in the list
                }

                // first check failed. Hit Spot is NOT the origin (for this Doodad at least)
                // instead, it may actually be a spot within the hitbox. So... let's go through every hittable spot then!
                for (int j = 0; j < Doodads[i].HittableSpots.Length; j++)
                {
                    if (Vector2.ValidDistance(checkPosition, Doodads[i].HittableSpots[j], 1.5f, true))
                    {
                        // the code below to add it to the list/ blow up more if we need to, is the same as the one above.
                        listODoodads.Add(Doodads[i]); // add to list of Doodads we've hit

                        // does this Doodad explode? if so, what will we hit? (similar concept to this method)
                        if (Doodads[i].Type.DestructibleDamageRadius > 0) // If this Doodad explodes then let's blow it up and see what else gets hit!
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
                    {
                        Doodads.Remove(foundDoodads[i]);
                    }
                }
                return true;
            }
            return false;
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
            blowups = null;
            int numOfDoodads = Doodads.Count;
            List<Doodad> listODoodads = new List<Doodad>(8); // 8 is arbitrary; just need "some" length.
            //List<Doodad> copy = new List<Doodad>(listODoodads);
            for (int i = 0; i < numOfDoodads; i++)
            {
                if (Doodads[i] == thisDoodad) continue;
                if (Vector2.ValidDistance(thisDoodad.Position, Doodads[i].Position, thisDoodad.Type.DestructibleDamageRadius, true))
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
            else return false;
        }

        // Unfinished -- 4/26/23
        private void ChainExplosion()
        {
            Logger.Failure("[ChainExplosion] Unimplemented.");
        }

        /// <summary>
        /// Frees the collision spots taken up by the given Doodads.
        /// </summary>
        /// <param name="doodads">Array of Doodads that must have their collision spots removed from this SARLevel's CollisionGrid.</param>
        private void FreeDoodadCollisionSpots(Doodad[] doodads)
        {
            // assumes doodads is never null
            for (int i = 0; i < doodads.Length; i++)
            {
                for (int j = 0; j < doodads[i].HittableSpots.Length; j++)
                {
                    int x = (int)doodads[i].HittableSpots[j].x;
                    int y = (int)doodads[i].HittableSpots[j].y;
                    if (x >= LevelWidth || y >= LevelHeight) continue;
                    CollisionGrid[x][y] = CollisionType.None;
                    //Logger.DebugServer($"[SARLevel - FDCS] Freed spot ({x} {y}).");
                }
            }
        }
        #endregion Doodad Junk

        #region LootItems
        // Locates a valid LootItem spot from the inital spot. 
        private Vector2 GetLootPosition(Vector2 pInitialSpot)
        {
            /*if (!IsThereAnItemhere(pInitialSpot))
            {
                return pInitialSpot;
            }*/
            return FindNewItemPosition(pInitialSpot);
            // old lol:: return pInitialSpot;
        }

        // Checks whether the provided LootItem (pItem)'s LootID exists in the this.LootItems dictionary. If so, then pItem is removed from this.LootItems.
        public void RemoveLootItem(LootItem item)
        {
            if (LootItems.ContainsKey(item.LootID)) LootItems.Remove(item.LootID);
            else Logger.DebugServer($"Level.RemoveLootItem: Item.LootID \"{item.LootID}\" does not exist in Level.LootItems!");
        }

        // Creates a new "Juice" LootItem and adds it to this.LootItems.
        public LootItem NewLootJuice(byte pCount, Vector2 pTryPos) // appears OK
        {
            pTryPos = GetLootPosition(pTryPos);
            LootCounter += 1;
            LootItem retLoot = new LootItem(LootCounter, LootType.Juice, $"Health Juice-{pCount}", 0, pCount, pTryPos);
            LootItems.Add(LootCounter, retLoot);
            return retLoot;
        }
        
        // Creates a new "Tape" LootItem and adds it to this.LootItems.
        public LootItem NewLootTape(byte pCount, Vector2 pTryPos) // appears OK
        {
            pTryPos = GetLootPosition(pTryPos);
            LootCounter += 1;
            LootItem retLoot = new LootItem(LootCounter, LootType.Tape, $"Tape-{pCount}", 0, pCount, pTryPos);
            LootItems.Add(LootCounter, retLoot);
            return retLoot;
        }

        // Creates a new "Armor" LootItem and adds it to this.LootItems.
        public LootItem NewLootArmor(byte pArmorLevel, byte pArmorTicks, Vector2 pTryPos) // appears OK
        {
            pTryPos = GetLootPosition(pTryPos);
            LootCounter += 1;
            LootItem retLoot = new LootItem(LootCounter, LootType.Armor, $"Armor-{pArmorLevel}/{pArmorTicks}", pArmorLevel, pArmorTicks, pTryPos);
            LootItems.Add(LootCounter, retLoot);
            return retLoot;
        }

        // Creates a new "Weapon" LootItem and adds it to this.LootItems.
        public LootItem NewLootWeapon(int pWepIndex, byte pRarity, byte pAmmo, Vector2 pTryPos) // appears OK | haven't tested fully with throwables
        {
            Weapon weapon = Weapon.GetWeaponFromID(pWepIndex);
            pTryPos = GetLootPosition(pTryPos);
            LootCounter += 1;
            LootItem retLoot = new LootItem(LootCounter, weapon.WeaponType, weapon.Name, pRarity, pAmmo, pWepIndex, pTryPos);
            LootItems.Add(LootCounter, retLoot);
            return retLoot;
        }

        // Creates a new "Ammo" LootItem and adds it to this.LootItems.
        public LootItem NewLootAmmo(byte pAmmoType, byte pCount, Vector2 pTryPos) // appears OK
        {
            pTryPos = GetLootPosition(pTryPos);
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
            {
                if (Grass[i].X == searchX && Grass[i].Y == searchY) return Grass[i];
            }
            return null;
        }

        /// <summary> Attempts to remove the provided grass patch from this SARLevel's Grass list. </summary>
        /// <param name="item">GameGrass object to attempt to remove.</param>
        public void RemoveGrassFromList(GameGrass item)
        {
            if (!Grass.Remove(item)) Logger.Failure("[SARLevel] encountered an error while removing grass from list!");
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
                    // ...while calculating whether projectiles hit things as well... which overall makes this a big ol' pain.
                    // So, may just add collisionHeight into the actual offsets and call it a day. who knows!
                    Logger.Basic($"Added spot: {newSpot.x}, {newSpot.y}");
                }
                _rebelHideoutDoor = null;
            }
            for (int x = 0; x < tarpSizeX; x++)
            {
                for (int y = 0; y < tarpSizeY; y++)
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

        // todo - generate molecrate spots; it's a lot more complicated than anticipated...
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
            //if (Doodads != null) Doodads = null;
            if (Campfires != null) Campfires = null;
            //if (LootItems != null) LootItems = null;
            if (MolecrateSpots != null) MolecrateSpots = null;
            if (Coconuts != null) Coconuts = null;
            if (Hamsterballs != null) Hamsterballs = null;
            GC.Collect();
        }

        /// <summary>
        /// Disposes of this SARLevel; not to be confuzed with "NullUnuseds".
        /// </summary>
        public void Dispose()
        {
            if (CollisionGrid != null) CollisionGrid = null;
            if (Doodads != null) Doodads = null;
            if (Campfires != null) Campfires = null;
            if (LootItems != null) LootItems = null;
            if (MolecrateSpots != null) MolecrateSpots = null;
            if (Coconuts != null) Coconuts = null;
            if (Hamsterballs != null) Hamsterballs = null;
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
