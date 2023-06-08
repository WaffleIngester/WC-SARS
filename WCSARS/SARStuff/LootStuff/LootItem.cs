namespace SARStuff
{
    public class LootItem
    {
        // -- Fields and Junk --

        /// <summary>
        /// ID of this LootItem.
        /// </summary>
        public readonly int LootID;

        /// <summary>
        /// Type of loot this LootItems is.
        /// </summary>
        public LootType LootType;

        /// <summary>
        /// WeaponType for this LootItems (if applicable).
        /// </summary>
        public WeaponType WeaponType;

        /// <summary>
        /// Name of this LootItems.
        /// </summary>
        public string Name;

        /// <summary>
        /// Rarity of this LootItems. 
        /// </summary>
        public byte Rarity;

        /// <summary>
        /// Amount of this item to give to Players.
        /// </summary>
        public byte GiveAmount;

        /// <summary>
        /// Index in AllWeaponsArray.
        /// </summary>
        public int WeaponIndex = -1;

        /// <summary>
        /// Position of this LootItem in the level.
        /// </summary>
        public Vector2 Position;

        // -- Constructors -- \\\

        /// <summary>
        /// Creates a Weapon LootItem using the provided parameters.
        /// </summary>
        /// <param name="lootID">LootID to assign this LootItem.</param>
        /// <param name="weaponType">Type of weapon (gun, throwable, melee).</param>
        /// <param name="name">Name to give this LootItem.</param>
        /// <param name="weaponRarity">Rarity of this weapon.</param>
        /// <param name="ammoCount">Amount of ammo this weapon has.</param>
        /// <param name="jsonIndex">Index in AllWeaponArray.</param>
        /// <param name="position">Position to spawn this LootItem.</param>
        public LootItem(int lootID, WeaponType weaponType, string name, byte weaponRarity, byte ammoCount, int jsonIndex, Vector2 position)
        {
            LootID = lootID;
            LootType = LootType.Weapon;
            WeaponType = weaponType;
            Name = name;
            Rarity = weaponRarity;
            GiveAmount = ammoCount;
            WeaponIndex = jsonIndex;
            Position = position;
        }

        /// <summary>
        /// Creates a regular LootItem loop using the provided parameters.
        /// </summary>
        /// <param name="lootID">LootID to assign this LootItem.</param>
        /// <param name="lootType">Type of loot this LootItem is.</param>
        /// <param name="name">Name of this LootItem.</param>
        /// <param name="rarity">Rarity/quality of this LootItem.</param>
        /// <param name="give">The amount of this item to give.</param>
        /// <param name="position">Position to spawn this LootItem.</param>
        public LootItem(int lootID, LootType lootType, string name, byte rarity, byte give, Vector2 position)
        {
            LootID = lootID;
            LootType = lootType;
            Name = name;
            Rarity = rarity;
            GiveAmount = give;
            Position = position;

            WeaponType = WeaponType.None;
        }
    }
}