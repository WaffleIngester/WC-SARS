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
        public float magnitudeSquared // If only ever used in ValidDistance(); then could just remove this tbh
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
        /// Attempts to move Vector2 Current towards Vector2 Target.
        /// </summary>
        /// <param name="current">Current point.</param>
        /// <param name="target">Target point..</param>
        /// <param name="moveDelta">Maximum distance for this step.</param>
        /// <returns>Vector2 of the translated vector.</returns>
        public static Vector2 MoveToVector(Vector2 current, Vector2 target, float moveDelta)
        {
            Vector2 diff = target - current;
            float mag = diff.magnitude;
            if (mag <= moveDelta || mag == 0f) return target;
            else return current + diff / mag * moveDelta;
        }

        /// <summary>
        /// Gets the disance between Vector2 A and B; returning whether their distance is within a certain threshold.
        /// </summary>
        /// <param name="a">Compare vector a.</param>
        /// <param name="b">Compare vector b.</param>
        /// <param name="threshold">Maximum distance the two vectors can be apart.</param>
        /// <param name="square">Whether to check the square distance or root distance (square is faster).</param>
        /// <returns>True if the distance is within the theshold; False is otherwise.</returns>
        public static bool ValidDistance(Vector2 a, Vector2 b, float threshold, bool square)
        {
            if (square) return (b - a).magnitudeSquared <= (threshold * threshold);
            else return (a - b).magnitude <= threshold; // Slower by microseconds.. I think? 
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
    }
}