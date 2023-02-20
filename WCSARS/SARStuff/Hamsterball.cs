using WCSARS;
namespace SARStuff
{
    /// <summary>
    /// Represents a "Hamsterball" object in the game world.
    /// </summary>
    public class Hamsterball
    {
        /// <summary>
        /// ID of this Hampterball.
        /// </summary>
        public short ID;

        /// <summary>
        /// The amount of HitPoints this Hampterball currently has.
        /// </summary>
        public byte HP;

        /// <summary>
        /// The current Player who owns this Hampterball.
        /// </summary>
        public Player CurrentOwner;

        /// <summary>
        /// Position in the overworld this Hampterball is currently at.
        /// </summary>
        public Vector2 Position;

        /// <summary>
        /// Creates a new "Hampterball" at the provided position.
        /// </summary>
        /// <param name="spawnPosition">Position to spawn this Hampterball.</param>
        public Hamsterball(short id, Vector2 spawnPosition)
        {
            ID = id;
            HP = 3;
            CurrentOwner = null;
            Position = spawnPosition;
        }
    }
}