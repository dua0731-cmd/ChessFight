namespace ChessFight.Gameplay
{
    // Contact-triggered pad. Ballistics and rider deduplication live in LaunchPad.
    public sealed class ContactLaunchPad : LaunchPad
    {
        void FixedUpdate() => ThrowAllOn();
    }
}
