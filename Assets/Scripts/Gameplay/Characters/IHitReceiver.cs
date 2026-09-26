using UnityEngine;

namespace ChessFight.Gameplay
{
    // What an attack does to a character: the rook's charge, the bishop's stones,
    // the king's shockwave, the queen's sword. Every attack speaks this one call,
    // so the pieces never need to know what kind of body they hit.
    //
    // Separate from ICharacterDriver on purpose: something that can be driven is
    // not necessarily something that can be hit. Look it up next to the driver
    // (GetComponentInParent<IHitReceiver>()). Only the machine that simulates the
    // character (the host in a match) calls it; a network puppet ignores it.
    public interface IHitReceiver
    {
        // push                velocity change in m/s, world space, given to the whole
        //                     body. m/s rather than an impulse, so a hit sends every
        //                     piece the same distance unless the piece itself says
        //                     otherwise (heavier pieces can scale it down).
        // knockdownSeconds    0 = a stagger: it loses its footing for a moment but
        //                     stays up. More knocks it down and keeps it down this
        //                     long before it starts getting up.
        // staminaDamage       stamina taken, in the seconds the stamina bar is
        //                     measured in (a full bar is 8).
        // dropFromWallOrRide  let go of whatever it hangs from - a wall it climbs, a
        //                     ledge, later a hook or a rope - and fall. Standing on a
        //                     moving platform is left to the push.
        void ApplyHit(Vector3 push, float knockdownSeconds, float staminaDamage, bool dropFromWallOrRide);
    }
}
