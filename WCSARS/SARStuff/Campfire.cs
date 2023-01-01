namespace SARStuff
{
    /// <summary>
    /// Represents a campfire object in the game world.
    /// </summary>
    public class Campfire
    {
        /// <summary>
        /// Position of this Campfire.
        /// </summary>
        public Vector2 Position;

        /// <summary>
        /// Indicates whether this Campfire has been used up already.
        /// </summary>
        public bool hasBeenUsed = false; // Currently only used in Lobby version of handling Campfires

        /// <summary>
        /// Indicates if this Campfire is currently lit up.
        /// </summary>
        public bool isLit = false; // Currently only used by the Match version of handling Campfires

        /// <summary>
        /// The amount of time (in seconds) that this Campfire has until it is to be put-out.
        /// </summary>
        public float UseRemainder = 15f; // Only used while Match is in progress

        /// <summary>
        /// Creates a Campfire object at the given points.
        /// </summary>
        /// <param name="x">Campfire X position.</param>
        /// <param name="y">Campfire Y position.</param>
        public Campfire(float x, float y)
        {
            Position = new Vector2(x, y);
        }
    }
}
