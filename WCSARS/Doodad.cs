using System.Collections.Generic;

namespace WCSARS
{
    public class Doodad
    {
        public DoodadType DoodadType;
        public List<Int32Point> OffsetCollisionPoints; // every CollisionPoint that this Doodad instance has. Move Collisions + Move-Sight Collisions
        public float X;
        public float Y;

        public Doodad(DoodadType doodadType, float x, float y)
        {
            DoodadType = doodadType;
            X = x;
            Y = y;
            List<Int32Point> _moveCollisionPts = DoodadType.MoveCollisionPoints;
            if (_moveCollisionPts != null && _moveCollisionPts.Count >= 1)
            {
                OffsetCollisionPoints = new List<Int32Point>();
                int points = _moveCollisionPts.Count;
                Int32Point _instancePoint;
                for (int i = 0; i < points; i++)
                {
                    _instancePoint = _moveCollisionPts[i];
                    OffsetCollisionPoints.Add( new Int32Point((int)X + _instancePoint.x, (int)Y + _instancePoint.y) );
                }
            }
            List<Int32Point> _moveSightCollisionPoints = DoodadType.MoveSightCollisionPoints;
            if (_moveSightCollisionPoints != null && _moveSightCollisionPoints.Count >= 1)
            {
                /* If DoodadType.MoveCollisionPoints is null, that means there are NO MoveCollisionPoints for this Doodad. (also if List.Count = 0)
                 * This means thate OffsetCollisionPoints is never initialized in the following method... so we have to do it here
                 */
                if (OffsetCollisionPoints == null)
                {
                    OffsetCollisionPoints = new List<Int32Point>();
                }
                int points = _moveSightCollisionPoints.Count;
                Int32Point _instancePoint;
                for (int i = 0; i < points; i++)
                {
                    _instancePoint = _moveSightCollisionPoints[i];
                    OffsetCollisionPoints.Add(new Int32Point((int)X + _instancePoint.x, (int)Y + _instancePoint.y));
                }
            }
        }
    }
    /*
    public enum CollisionType : byte
    {
        Movement,
        None,
        MovementAndSight
    }*/
}
