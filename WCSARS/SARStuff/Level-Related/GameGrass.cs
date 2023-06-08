// Grass, GameGrass, Foliage; It's all the same!
// one just makes conflicts with any use of the word "grass". and foliage, but foliage is like not grass I think
namespace SARStuff
{
    /// <summary>
    /// Represents "grass" found in Super Animal Royale. Funnily enough, bushes and other plants are counted as grass as well.
    /// </summary>
    public class GameGrass
    {
        // todo: if you want to simulate breaking grass/ crates & other junk, you have to utlize GrassType.HitboxXY probably!
        public readonly GrassType Type;
        public readonly short X;
        public readonly short Y;
        //public readonly Vector2 Position;

        /// <summary>
        /// Creates a new GameGrass object using the provided parameters.
        /// </summary>
        /// <param name="type">Which GrassType this GameGrass is.</param>
        /// <param name="x">X position.</param>
        /// <param name="y">Y position.</param>
        public GameGrass(GrassType type, short x, short y)
        {
            Type = type;
            (X, Y) = (x, y);
        }
    }
}
