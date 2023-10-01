namespace SARStuff
{
    /// <summary>
    ///  Represents the different movement states that the game recognizes a player as being in.
    /// </summary>
    public enum MovementMode : byte
    {
        Creeping,
        Walking,
        Rolling,
        CreepRolling,
        HampterBalling,
        Downed,
        BananaStunned
    }
}
