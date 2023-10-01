using System;

namespace SARStuff
{
    /// <summary>
    /// Represents the "Giant Eagle" NPC that flies along the flight path during round-start.
    /// </summary>
    public class GiantEagle
    {
        /// <summary> Current position where this Giant Eagle is located.</summary>
        public Vector2 Position { get; private set; }

        /// <summary> Starting position of this Giant Eagle. </summary>
		public Vector2 Start { get; private set; }

        /// <summary> Ending position of this Giant Eagle. </summary>
		public Vector2 End { get; private set; }

        /// <summary> Whether this Giant Eagle has reached the end of its flight-path or not.</summary>
        public bool HasReachedEnd { get => Position.IsNear(End, 0.5f); }

        // Speed at which the eagle will move at.
        private const float _giantEagleMoveSpeed = 95f; // 0.90.2 OK

        /// <summary>
        /// Creates a GiantEagle with a randomly-generated flight-path.
        /// </summary>
        public GiantEagle() // I personally believe SAR uses pre-determined flight paths rather than pure RNG like this. I just don't have the list of real paths.
        {
            GenerateFlightPath();
            //Console.WriteLine($"Flight RNG-Genned\nFlight Start: {Start}\nFlight End: {End}");
        }

        /// <summary>
        /// Creates a GiantEagle with a specified flight-path.
        /// </summary>
        /// <param name="start">Where the flight will begin.</param>
        /// <param name="end">Where the flight will end.</param>
        public GiantEagle(Vector2 start, Vector2 end)
        {
            Start = start;
            End = end;
        }

        // Used in Math.UpdateGiantEagle
        public void UpdatePosition(float deltaTime)
        {
            Position = Vector2.MoveTowards(Position, End, deltaTime * _giantEagleMoveSpeed);
        }

        private void GenerateFlightPath() // can update this at some point; this is just for basics
        {
            // RNG
            //uint seed = (uint)DateTime.UtcNow.Ticks | 0xfbc21; // seed was randomly decided on. no particular reason it is this way lol
            uint seed = (uint)DateTime.UtcNow.Ticks;
            MersenneTwister random = new MersenneTwister(seed);

            // Spots
            float inital = random.NextUInt(0, 4206); // Spot either the X-Y will be placed at
            float end = random.NextUInt(0, 4206); // Spot either the X-Y will be placed at

            // Dir 1 | Vertical or Horizontal type?
            bool vertical = (random.NextUInt(0, 20) >> 4) == 0 ? true : false; // 0 = vertical; 1 = horizontal
            bool flip = (random.NextUInt(0, 20) >> 4) == 0 ? false : true; // 1 = doFlip; 0 = dontFlip
            // Zones will only ever go bottom > up; or left > right; can't be bothered to make them not
            if (vertical)
            {
                if (flip)
                {
                    Start = new Vector2(end, 4206f);
                    End = new Vector2(inital, 0f);
                }
                else
                {
                    Start = new Vector2(inital, 0f);
                    End = new Vector2(end, 4206f);
                }
            }
            else
            {
                if (flip)
                {
                    Start = new Vector2(4206f, end);
                    End = new Vector2(0f, inital);
                }
                else
                {
                    Start = new Vector2(0f, inital);
                    End = new Vector2(4206f, end);
                }
            }
        }
    }
}
