namespace WCSARS
{
    public enum VehicleType
    {
        Hamsterball,
        Emu
    }
    internal class Vehicle
    {
        public byte HP;
        public short VehicleID;
        public Vehicle(byte hp, short vehicleid)
        {
            HP = hp;
            VehicleID = vehicleid;
        }
    }
}
