namespace WCSARS
{
    public struct Int32Point // this is a custom datatype that sar uses. think of it as a Vector2 without all the extra stuff
    {
        public Int32Point(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public int x;
        public int y;
        public static bool operator ==(Int32Point pointA, Int32Point pointB)
        {
            if ((pointA.x == pointB.x) && (pointA.y == pointB.y))
            {
                return true;
            }
            return false;
        }
        public static bool operator !=(Int32Point pointA, Int32Point pointB)
        {
            if ((pointA.x != pointB.x) || (pointA.y != pointB.y))
            {
                return true;
            }
            return false;
        }
        //public override string ToString() => $"({x}, {y})";
    }
}
