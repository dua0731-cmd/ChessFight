using ChessFight.Gameplay;
using UnityEngine;
using R = ChessFight.Network.RoyalVaultRules;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // Beat 2 of the royal vault: the dial-hammer walkway (y 4, 8 m wide, no rails) from J1 to the approach. Two hammers turn
    // about +x over it (Spinner: HammerAngle, half a turn apart - two chess clocks running in turn), each beside a big dial
    // picture on a pedestal west of the walkway. Between their sweeps lies the 5 m rest cell. The lamps are the Cues partial's.
    public static partial class RoyalVaultBuilder
    {
        static void Hammers(CourseBuilder b, Transform section)
        {
            Slab(b, "RV hammer walkway", -4f, 4f, F(R.J1Z1), F(R.WalkZ1), Tier1);
            for (int i = 0; i < R.HammerAxleZ.Length; i++) Hammer(b, section, i);
        }

        // V04: the axle HammerAxleAbove over the walkway; the head reaches radially HammerInner..HammerOuter, so it passes 0.1 m
        // over the floor, HammerAlong along the way and HammerAcross across it; angle 0 hangs down, and a positive turn sweeps
        // -z at the bottom (back the way the pawns come). The hit knocks straight back along -z (alongShare 1: never sideways
        // off the unrailed walkway), its push capped at 3.8 m/s so a knight's slide stays inside the rest cell; one hit a pass.
        static void Hammer(CourseBuilder b, Transform section, int i)
        {
            float z = F(R.HammerAxleZ[i]), axle = Tier1 + F(R.HammerAxleAbove);
            YardKit.Cylinder(b.parent, "RV dial " + (i + 1), V(-5.9f, axle, z), Vector3.right, 8f, 1f, RoyalVaultLook.Greybox, Solid.None);
            Picture(b, "RV dial pedestal " + (i + 1), V(-6.4f, Pit, z - .5f), V(-5.4f, axle - 4f, z + .5f));

            var root = Root(b, "RV hammer " + (i + 1), V(0f, axle, z));
            var spinner = root.gameObject.AddComponent<Spinner>();
            spinner.Configure(Vector3.right, F(R.HammerDegreesPerSecond));
            spinner.SetPhase(F(R.HammerPhase(i)));   // its angle is then HammerDegreesPerSecond x (Now + phase): HammerAngle

            float headY = -F((R.HammerInner + R.HammerOuter) / 2), headTall = F(R.HammerOuter - R.HammerInner);
            float across = F(R.HammerAcross), along = F(R.HammerAlong), inner = F(R.HammerInner), armX = across * .5f + .15f;
            var head = Part(root, "RV hammer head", V(0f, headY, 0f), V(across, headTall, along), Solid.NoClimb);
            head.AddComponent<ChessFight.Game.CameraSeeThrough>();   // always on: it sweeps through the follow camera's view
            Part(root, "RV hammer arm W", V(-armX, -inner * .5f, 0f), V(.2f, inner, .5f), Solid.None);
            Part(root, "RV hammer arm E", V(armX, -inner * .5f, 0f), V(.2f, inner, .5f), Solid.None);
            YardKit.Cylinder(root, "RV hammer axle", V(-.9f, 0f, 0f), Vector3.right, .6f, 11f, RoyalVaultLook.Greybox, Solid.None);

            var impact = YardKit.Shell(root, "RV hammer " + (i + 1) + " (hit)", V(0f, headY, 0f), V(across + .3f, headTall + .3f, along + .3f), 3f);
            impact.solid = head.GetComponent<BoxCollider>();
            impact.knockdownSpeed = 2.5f;   // the head moves ~4.1 m/s at hip radius
            impact.knockdownSeconds = 1f;
            impact.speedScale = .5f;
            impact.maxPush = 3.8f;
            impact.lift = 1.5f;
            impact.alongShare = 1f;
            impact.replaceSpeed = true;
            impact.cooldown = 3f;   // a turn is 6 s
            // R725 bot probe: a knocked-down pawn is let through the head for as long as the head can be at body height in
            // one pass (2 x HammerBodyHalfAngle at HammerDegreesPerSecond, 1.94 s), not the spec's 1 s: the floored pawn slides
            // back the way the head sweeps, so the head stays over it for up to 1.5 s, and when the 1 s ran out inside the
            // head the box took it back and threw it out at 12-17 m/s. YardImpact still takes it back as soon as it is clear.
            impact.ghostSeconds = F(2 * R.HammerBodyHalfAngle / R.HammerDegreesPerSecond) + .1f;
            impact.course = section;
            impact.forwardShare = .6f;
        }
    }
}
