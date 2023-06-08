namespace SARStuff
{
#pragma warning disable CS0660 // thog don't care
#pragma warning disable CS0661 // thog don't care
    /// <summary> Represents a point made up of two SHORTS (Int16)</summary>
    public struct Int16Point
    {
        // --- The Usual --- \\
        public readonly short x;
        public readonly short y;

        public Int16Point(short x, short y)
        {
            this.x = x;
            this.y = y;
        }

        // --- Overloads --- \\
        // ==
        public static bool operator ==(Int16Point pointA, Int16Point pointB)
        {
            return (pointA.x == pointB.x) && (pointA.y == pointB.y);
        }

        // !=
        public static bool operator !=(Int16Point pointA, Int16Point pointB)
        {
            return (pointA.x != pointB.x) || (pointA.y != pointB.y);
        }

        /*
        // GetHashCode
        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode();
        }

        // Equals
        public override bool Equals(object obj)
        {
            if (obj == null || obj.GetType() != typeof(Int16Point)) return false;
            Int16Point oInt16point = (Int16Point)obj;
            return (x == oInt16point.x) && (y == oInt16point.y);
        }*/

        // Int16Point.ToString()
        public override string ToString() => $"({x}, {y})";
    }
}
