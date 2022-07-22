namespace WCSARS
{
    public class Hampterball
    {
        public short ID;
        public byte HP;
        public float X;
        public float Y;
        public Hampterball(byte hp, short vehicleid, float x, float y)
        {
            ID = vehicleid;
            HP = hp;
            X = x;
            Y = y;
        }
    }
}
