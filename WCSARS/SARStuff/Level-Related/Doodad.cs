using System.Collections.Generic;

namespace SARStuff
{
    /// <summary>
    /// Represents a "Doodad" object in the game world.
    /// </summary>
    public class Doodad
    {
        //public DoodadType DoodadType;
        public readonly DoodadType Type;
        public readonly Vector2 Position;
        public readonly Vector2[] HittableSpots;    // Un-Rounded Spots for Server-Side checking
        //public readonly Int32Point[] SendHitSpots; // Intended for sending net messages, but doesn't seem to fix any issues like the scarecrow glitch.

        public Doodad(DoodadType doodadType, Vector2 spawnPos)
        {
            Type = doodadType;
            Position = spawnPos;
            //List<Int32Point> offsetCollPts = new List<Int32Point>();
            List<Vector2> realOffsetSpots = new List<Vector2>();
            if (doodadType.MovementCollisionPts?.Length > 0)
            {
                Int32Point disPt;
                for (int i = 0; i < doodadType.MovementCollisionPts.Length; i++)
                {
                    disPt = doodadType.MovementCollisionPts[i];
                    //Int32Point newPoint = new Int32Point((int)spawnPos.x + disPt.x, (int)spawnPos.y + disPt.y);
                    //offsetCollPts.Add(newPoint);
                    realOffsetSpots.Add(new Vector2((spawnPos.x + disPt.x), (spawnPos.y + disPt.y)));
                }
            }
            if (doodadType.MovementAndSightCollisionPts?.Length > 0)
            {
                Int32Point disPt;
                for (int i = 0; i < doodadType.MovementAndSightCollisionPts.Length; i++)
                {
                    disPt = doodadType.MovementAndSightCollisionPts[i];
                    //Int32Point newPoint = new Int32Point((int)spawnPos.x + disPt.x, (int)spawnPos.y + disPt.y);
                    //offsetCollPts.Add(newPoint);
                    realOffsetSpots.Add(new Vector2(spawnPos.x + disPt.x, (spawnPos.y + disPt.y)));
                }
            }
            //OffsetCollisionPoints = offsetCollPts;
            //SendHitSpots = offsetCollPts.ToArray();
            HittableSpots = realOffsetSpots.ToArray();
            //offsetCollPts = null;
            realOffsetSpots = null;
        }
    }
}