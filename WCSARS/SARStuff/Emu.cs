namespace SAR.Types
{
    public class Emu
    {
        public readonly EmuType EmuType;
        public float HP;
        //public float Speed = 54f;
        public int Damage;
        public int DamagePierce;
        public float X;
        public float Y;
        
        public Emu(EmuType emutype, float hp, float x, float y)
        {
            EmuType = emutype;
            X = x;
            Y = y;
            HP = 140;
            Damage = 35;
            DamagePierce = 10;
            switch (EmuType)
            {
                case EmuType.Speedy:
                    HP = 140;
                    break;
                case EmuType.Chonky:
                    HP = 190;
                    break;
                case EmuType.Battle:
                    HP = 150;
                    Damage = 45;
                    DamagePierce = 15;
                    break;
            }

        }
    }
}
