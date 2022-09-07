namespace WCSARS
{
#pragma warning disable CS0660
#pragma warning disable CS0661
    public struct Int32Point // this is a custom datatype that sar uses. think of it as a Vector2 without all the extra stuff
    {
        public int x;
        public int y;
        public Int32Point(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
        //overloads
        public static bool operator ==(Int32Point pointA, Int32Point pointB)
        {
            return (pointA.x == pointB.x) && (pointA.y == pointB.y);
        }
        public static bool operator !=(Int32Point pointA, Int32Point pointB)
        {
            return (pointA.x != pointB.x) || (pointA.y != pointB.y);
        }
        //public override string ToString() => $"({x}, {y})";
    }
}
