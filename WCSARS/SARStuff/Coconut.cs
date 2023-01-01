namespace SARStuff
{
    // maybe in the future change this to "Edible" so Coconuts, Mushrooms, and EventTokens, all have the same base thing?
    public struct Coconut
    {
        public Vector2 Position;

        public Coconut(float x, float y)
        {
            Position = new Vector2(x, y);
        }
    }
}
