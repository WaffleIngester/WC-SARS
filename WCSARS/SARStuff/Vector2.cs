namespace SARStuff
{
    #pragma warning disable CS0660
    #pragma warning disable CS0661
    public struct Vector2 // Pretty much Unity's Vector2 struct without most other useful feaures.
    {
        // -- Fields / Properties --
        public float x;
        public float y;

        public float magnitude
        {
            get => (float)System.Math.Sqrt((x * x) + (y * y));
        }

        // -- Other --
        public Vector2(float vx, float vy)
        {
            x = vx;
            y = vy;
        }
        
        public static Vector2 MoveToVector(Vector2 current, Vector2 target, float moveDelta)
        {
            Vector2 diff = target - current;
            float mag = diff.magnitude;
            if (mag <= moveDelta || mag == 0f) return target;
            else return current + diff / mag * moveDelta;
        }

        // -- Overloads --

        // ==
        public static bool operator ==(Vector2 a, Vector2 b)
        {
            return a.x == b.x && a.y == b.y;
        }

        // !=
        public static bool operator !=(Vector2 a, Vector2 b)
        {
            return a.x != b.x || a.y != b.y;
        }

        // Addition
        public static Vector2 operator +(Vector2 a, Vector2 b)
        {
            return new Vector2(a.x + b.x, a.y + b.y);
        }

        // Subtraction
        public static Vector2 operator -(Vector2 a, Vector2 b)
        {
            return new Vector2(a.x - b.x, a.y - b.y);
        }

        // Division
        public static Vector2 operator /(Vector2 a, float num)
        {
            return new Vector2(a.x / num, a.y / num);
        }

        // Multiplication
        public static Vector2 operator *(Vector2 a, float num)
        {
            return new Vector2(a.x * num, a.y * num);
        }
    }
}