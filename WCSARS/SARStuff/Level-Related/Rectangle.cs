namespace SARStuff
{
    /// <summary>
    /// A minimal representation of Unity's "Rect" struct.
    /// </summary>
    internal struct Rectangle
    {
        public readonly float MinX;
        public readonly float MinY;
        public readonly float Width;
        public readonly float Height;

        public Rectangle(float minX, float minY, float width, float height)
        {
            MinX = minX;
            MinY = minY;
            Width = width;
            Height = height;
        }
    }
}