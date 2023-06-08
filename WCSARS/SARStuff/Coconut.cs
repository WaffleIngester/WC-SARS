namespace SARStuff
{
    // maybe in the future change this to "Edible" so Coconuts, Mushrooms, and EventTokens, all have the same base thing?
    public struct Coconut
    {
        public Vector2 Position { get; private set; }

        public Coconut(Vector2 position)
        {
            Position = position;
        }
    }
}
