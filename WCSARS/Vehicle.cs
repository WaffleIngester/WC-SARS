using System;
using System.Numerics;
namespace WCSARS
{
    public enum VehicleType
    {
        Hamsterball,
        Emu
    }
    internal class Vehicle
    {
        public readonly short VehicleID;
        public byte HP;
        public float PositionX;
        public float PositionY;
        public Vehicle(byte hp, short vehicleid, float x, float y)
        {
            VehicleID = vehicleid;
            HP = hp;
            PositionX = x;
            PositionY = y;
        }
    }
}
