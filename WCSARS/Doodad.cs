using System.Collections.Generic;

namespace WCSARS
{
    public class Doodad
    {
        public DoodadType DoodadType;
        public List<Int32Point> OffsetCollisionPoints;
        public List<Int32Point> OffsetCollisionPoints2; // working title
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
            List<Int32Point> _moveCollisionPts2 = DoodadType.MoveSightCollisionPoints;
            if (_moveCollisionPts2 != null && _moveCollisionPts2.Count >= 1)
            {
                OffsetCollisionPoints2 = new List<Int32Point>();
                int points = _moveCollisionPts2.Count;
                Int32Point _instancePoint;
                for (int i = 0; i < points; i++)
                {
                    _instancePoint = _moveCollisionPts2[i];
                    OffsetCollisionPoints2.Add(new Int32Point((int)X + _instancePoint.x, (int)Y + _instancePoint.y));
                }
            }
        }
    }
    public enum CollisionType : byte
    {
        Movement,
        None,
        MovementAndSight
    }
}
