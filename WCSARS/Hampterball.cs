namespace WCSARS
{
    public class Hampterball
    {
        public readonly short VehicleID;
        public byte HP;
        public float PositionX;
        public float PositionY;
        public Hampterball(byte hp, short vehicleid, float x, float y)
        {
            VehicleID = vehicleid;
            HP = hp;
            PositionX = x;
            PositionY = y;
        }
    }
}
