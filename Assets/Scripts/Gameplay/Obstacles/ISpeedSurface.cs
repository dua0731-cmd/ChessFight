namespace ChessFight.Gameplay
{
    // A floor that lets a character on it run faster - a speed lane (R461, the war room's north return route). While
    // the character's ground probe finds it, the character's top running speed is at least TopSpeed (its own run or
    // sprint if that is faster). Unlike an IMovingSurface it carries nothing: the character still steers and stops as
    // it always does, only faster.
    public interface ISpeedSurface
    {
        // m/s
        float TopSpeed { get; }
    }
}
