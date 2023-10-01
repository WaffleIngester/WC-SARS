namespace SARStuff
{
    #pragma warning disable CS0660
    #pragma warning disable CS0661
    public struct Vector2
    {
        // -- Fields / Properties --
        public float x;
        public float y;

        /// <summary>
        ///  Returns the magnitude of this Vector2.
        /// </summary>
        public float magnitude // square root --> slow...
        {
            get => (float)System.Math.Sqrt((x * x) + (y * y));
        }

        /// <summary>
        ///  Returns the squared magnitude of this Vector2.
        /// </summary>
        public float sqrMagnitude // squared --> faster
        {
            get => (x * x) + (y * y);
        }

        // -- Other --
        public Vector2(float vx, float vy)
        {
            x = vx;
            y = vy;
        }
        
        /// <summary>
        ///  Moves a vector towards a target vector.
        /// </summary>
        /// <param name="current">Current point.</param>
        /// <param name="target">Target point.</param>
        /// <param name="moveDelta">Maximum distance current can be moved towards target.</param>
        /// <returns>The newly translated vector.</returns>
        public static Vector2 MoveTowards(Vector2 current, Vector2 target, float moveDelta)
        {
            Vector2 diff = target - current; // if the Current and Target vectors are swapped; Current moves away from Target
            float mag = diff.magnitude;
            if (mag <= moveDelta || mag == 0f)
                return target;
            return current + diff / mag * moveDelta;
        }

        /// <summary>
        ///  Determines whether this vector is close enough to vector B (squared).
        /// </summary>
        /// <param name="b"> Target vector to compare against.</param>
        /// <param name="threshold"> Maximum distance these vectors can be from each other.</param>
        /// <returns> True if the two vectors are within the threshold; False if otherwise.</returns>
        public bool IsNear(Vector2 b, float threshold)
        {
            Vector2 delta = new Vector2(b.x - x, b.y - y);
            return delta.sqrMagnitude <= (threshold * threshold);
        }

        /// <summary>
        ///  Determines whether this vector is close enough to vector B (root).
        /// </summary>
        /// <param name="b"> Target vector to compare against.</param>
        /// <param name="threshold"> Maximum distance these vectors can be from each other.</param>
        /// <returns> True if the two vectors are within the threshold; False if otherwise.</returns>
        public bool IsNearSqrt(Vector2 b, float threshold)
        {
            Vector2 delta = new Vector2(b.x - x, b.y - y);
            return delta.magnitude <= threshold;
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
        public static Vector2 operator *(Vector2 a, Vector2 b)
        {
            return new Vector2(a.x * b.x, a.y * b.y);
        }
        public static Vector2 operator *(Vector2 a, float num)
        {
            return new Vector2(a.x * num, a.y * num);
        }

        /// <summary>
        /// Converts a Vector2 into a string representation. (x, y)
        /// </summary>
        public override string ToString() => $"({x}, {y})";
    }
}