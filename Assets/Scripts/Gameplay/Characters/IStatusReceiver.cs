namespace ChessFight.Gameplay
{
    // Status effects an ability puts on a character (Queen of the Hill M13): the
    // knight's stomp squashes, the bishop's stones stagger. Like IHitReceiver it is
    // looked up next to the driver (GetComponentInParent<IStatusReceiver>()), and
    // only the machine that simulates the character calls it; a network puppet
    // ignores it.
    public interface IStatusReceiver
    {
        // Squashed flat: no moving, jumping or grabbing for `seconds`, and it lets go
        // of any wall, rope or ride it hung from. Afterwards another squash does not
        // take for `immunity` seconds. False when it did not take: immune, knocked
        // down already, or still immune from the last one.
        bool Squash(float seconds, float immunity);

        // Staggered: its footing gone for `seconds` - no moving, jumping or
        // grabbing - but it stays on its feet. False when it did not take.
        bool Stagger(float seconds);
    }
}
