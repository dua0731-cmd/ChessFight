using UnityEngine;

namespace ChessFight.Gameplay
{
    // Something a launch pad or a spring lift can throw (Queen of the Hill M7):
    // the character is SET to this velocity, not pushed by it, so every throw
    // flies the same arc whatever the character was doing when it stepped on.
    // It lets go of whatever it hangs from or holds and keeps the horizontal
    // part through the flight - it does not steer back to a standstill - but it
    // is not knocked down. Only the machine that simulates the character calls
    // it; a network puppet ignores it. Look it up next to the driver
    // (GetComponentInParent<ILaunchable>()).
    public interface ILaunchable
    {
        // The ground point under the character as it stands - where its feet
        // would be, even mid-jump. The throw is worked out from here to the
        // landing, so a character caught in the air lands on the same spot.
        Vector3 LaunchFrom { get; }

        void Launch(Vector3 velocity);
    }
}
