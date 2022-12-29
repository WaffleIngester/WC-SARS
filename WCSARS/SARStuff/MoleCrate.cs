namespace SARStuff
{
    public class MoleCrate
    {
        /* -- Short Explanation --
        The MoleCrate class is kind of confusing at first glance. The MoleCrate class/object takes on two roles.
        The first role is that of the Mole-character who throws out the crate.
        The other role is that of the crate itself.
        So remember, the MoleCrate is both the Mole moving around and the Molecrate itself!
        */

        // -- Fields / Properties --
        public bool isCrateReal = false;
        public bool isOpened = false;
        public float IdleTime;
        public Vector2 Position;
        public readonly Vector2[] MovePositions;
        public int MoveIndex = 0;

        // -- Constructor --
        /// <summary>
        /// Creates a MoleCrate object with the provided parameters. MoleCrate does not move on its own!
        /// </summary>
        /// <param name="startPosition">Initial position for this MoleCrate.</param>
        /// <param name="moveToPositions">Array of positions this MoleCrate can move.</param>
        /// <param name="waitTime">Amount of time (in seconds) this MoleCrate will wait for before moving. +2.7s always added onto this value.</param>
        public MoleCrate(Vector2 startPosition, Vector2[] moveToPositions, float waitTime)
        {
            IdleTime = waitTime + 2.7f;
            Position = startPosition;
            MovePositions = moveToPositions;
        }
    }
}
