namespace SARStuff
{
#pragma warning disable CS0660 // thog don't care
#pragma warning disable CS0661 // thog don't care
    /// <summary> Represents a point made up of two INTS (Int32) </summary>
    public struct Int32Point
    {
        // --- The Usual --- \\
        // todo - readonly; we're just being lazy in sar level with figuring out loot positions
        public int x;
        public int y;

        public Int32Point(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        // --- Overloads --- \\
        // ==
        public static bool operator ==(Int32Point pointA, Int32Point pointB)
        {
            return (pointA.x == pointB.x) && (pointA.y == pointB.y);
        }

        // !=
        public static bool operator !=(Int32Point pointA, Int32Point pointB)
        {
            return (pointA.x != pointB.x) || (pointA.y != pointB.y);
        }

        /*
        totally didn't take a few pointers from sar
        // GetHashCode
        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode();
        }

        // Equals
        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != typeof(Int32Point)) return false;
            Int32Point oInt32point = (Int32Point)obj;
            return (x == oInt32point.x) && (y == oInt32point.y);
        }*/

        // Int32Point.ToString()
        public override string ToString() => $"({x}, {y})";
    }
}
