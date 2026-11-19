using System;

namespace ChessFight.Network
{
    // The Royal Vault, R725: a grey-box draft for the late obstacle slot (slot 5's drafts, after the observatory). The timing of
    // every machine, the layout numbers the builder places by and the look-ahead queries the cues, validators and bots ask,
    // worked out from numbers alone so every PC moves the vault alike from the shared obstacle clock and the Core tests check
    // it without Unity. The builder, the components, the validators and the bots read these names and never retype a number.
    //   frame   the section frame: x across (east +), y up, +z the way on, metres. Entry (0, 0, 0) in the middle of the 12 m
    //           threshold, exit (0, ExitY, ExitZ).
    //   time    `t` is ObstacleClock time at Unix scale; all the maths is in double.
    //   cues    one look for every gameplay cue (Cue): Off hidden, Go green ("go now"), Warn red blinking at 4 Hz ("about to
    //           move"), Stop red ("moving or occupied").
    // Trapezoid motions reuse SortingYardRules.Profile.
    public static class RoyalVaultRules
    {
        public enum Cue { Off, Go, Warn, Stop }

        // ------------------------------------------------------------------ frame and layout (section frame, metres)

        public const double ExitY = 12, ExitZ = 196, PitBottom = -40, RunSpeed = 2.5, SprintSpeed = 3.6, BodyHalf = 0.3;
        public const double ForecourtZ1 = 18, CorridorX = -18, J1Z0 = 50, J1Z1 = 60, WalkZ1 = 83, ApproachZ1 = 89,
                            BeltZ0 = 89, BeltZ1 = 103, NeckZ0 = 107, NeckZ1 = 114, StairTwoZ1 = 126, J2Z0 = 126, J2Z1 = 136,
                            PlatesZ1 = 156, TerraceZ0 = 168, DoorZ0 = 178, DoorZ1 = 180, ArchZ = 192;
        // Every stair rises 4 m over 12 m: the metres walked along one.
        public static readonly double StairSlope = Math.Sqrt(12 * 12 + 4 * 4);   // 12.65
        // CP0..CP6: the z of each checkpoint's ground point.
        public static readonly double[] CheckpointZ = { 9, 34, 55, 83.6, 131, 172, 182.5 };
        // The ProgressPath P0..P27: the east tower lane, the walkway, the east belt, the press neck, the west plate row, the door.
        public static readonly double[] RouteX = { 0, 0, 2.2, 2.2, 2.2, 2.2, 2.2, 2.2, 2.2, 2.2, 2.2, 0, 0, 0, 0, 1.5, 1.5, -2, -2, -2, -2, -2, -2, 0, 0, 0, 0, 0 };
        public static readonly double[] RouteY = { 0, 0, 0, .5, 1, 1.5, 2, 2.5, 3, 3.5, 4, 4, 4, 4, 4, 4, 4, 4, 4, 8, 8, 8, 8, 12, 12, 12, 12, 12 };
        public static readonly double[] RouteZ = { 0, 9, 17.6, 20.65, 25.1, 29.55, 34, 38.45, 42.9, 47.35, 50.6, 55, 60, 83, 86, 89, 103, 105, 114,
                                                   126, 131, 136, 156, 168, 172, 179, 182.5, 196 };

        // `t` wrapped into [0, p), negative times too.
        public static double Wrap(double t, double p)
        {
            double u = t % p;
            return u < 0 ? u + p : u;
        }

        // Smoothstep of u clamped to 0..1.
        public static double Smooth(double u)
        {
            u = u < 0 ? 0 : u > 1 ? 1 : u;
            return u * u * (3 - 2 * u);
        }

        static double Hyp(double a, double b) => Math.Sqrt(a * a + b * b);

        // The Warn blink: 4 Hz, the same on every PC.
        public static bool Blink(double t) => ((long)Math.Floor(t * 8.0) & 1) == 0;

        // ------------------------------------------------------------------ V01 coin towers

        // Two straight lanes at x +-TowerLaneX of TowerCount round towers between the forecourt's edge (TowerFieldZ0) and J1
        // (TowerFieldZ1), one TowerStep higher per row; row TowerRest is one shared rest platform. The flat take-off edges are
        // open only for |x -+ TowerLaneX| <= TowerTakeoffHalf.
        public const int TowerCount = 7, TowerRest = 4;
        public const double TowerDiameter = 3.6, TowerStep = 0.5, TowerLaneX = 2.2, TowerFieldZ0 = 18, TowerFieldZ1 = 50, TowerTakeoffHalf = 1.0;
        public static double TowerGap => (TowerFieldZ1 - TowerFieldZ0 - TowerCount * TowerDiameter) / (TowerCount + 1);   // 0.85

        // Row k's centre, k = 1..7.
        public static double TowerZ(int k) => TowerFieldZ0 + TowerGap * k + TowerDiameter * (k - 0.5);

        // Row k's top; J1 is the eighth step (8 x 0.5).
        public static double TowerTop(int k) => TowerStep * k;

        // The gap between two round tops d metres off the lane line.
        public static double TowerGapOffLine(double d)
        {
            double r = TowerDiameter / 2;
            return TowerDiameter + TowerGap - 2 * Math.Sqrt(Math.Max(0, r * r - d * d));
        }

        // The gap between a flat edge and a round top d metres off the lane line.
        public static double EdgeGapOffLine(double d)
        {
            double r = TowerDiameter / 2;
            return TowerGap + r - Math.Sqrt(Math.Max(0, r * r - d * d));
        }

        // The Pawn Rush jump (PawnRushPhysicsParams: 4.9 m/s up, gravity x1.4 rising, x1.61 falling): seconds in the air
        // landing on a top dh higher.
        public static double JumpAirSeconds(double dh)
        {
            const double g = 9.81 * 1.4, gf = 9.81 * 1.4 * 1.15, v = 4.9;
            double apex = v * v / (2 * g);
            return v / g + Math.Sqrt(2 * Math.Max(0, apex - dh) / gf);   // 0.574 onto +0.5
        }

        // ------------------------------------------------------------------ V02 gold-bar hurdles

        // Two Spinners in the corridor, each a full bar through its hub, phase 0. Unity yaw: local x at angle a points
        // (cos a, 0, -sin a). R725 fun pass: the bar's top 0.55 -> 0.5 (spec 6 "hurdles knock down too often", the lower
        // half of that knob; the speeds stay): a grey bar on a grey floor is judged worse by people than by the bots that
        // read the rule, and the jump window 2 m out widens from 0.185 to 0.217 s (hurdle 1).
        public const double HurdleHubX = -18, HurdleReach = 3.95, HurdleBarLow = 0.25, HurdleBarHigh = 0.5, HurdleBodyHalf = 0.45;
        public static readonly double[] HurdleHubZ = { 14.0, 24.5 }, HurdleDegreesPerSecond = { 110, -100 };

        public static double HurdleAngle(int h, double t) => Wrap(HurdleDegreesPerSecond[h] * t, 360);

        // Seconds for half a turn: each point of the corridor is swept by one arm, then by the other this much later.
        public static double HurdleHalfTurn(int h) => 180 / Math.Abs(HurdleDegreesPerSecond[h]);   // 1.64, 1.8

        // R725 fun pass: how long a hurdle's knock leaves alone a body it has just knocked down (the builder's YardImpact
        // cooldown, per body; one it only staggered keeps the spec's 1 s, staggerCooldown): half a turn and HurdleFreePass
        // more. The other arm sweeps the spot half a turn after the hit, just as the pawn is back up, and with the old 1 s
        // cooldown and the 1.5 s chain grace both run out by then it knocked the pawn down again, three times in a row for
        // every blind runner (the fun1 baseline); now that pass is free. 0.6 s past the half turn, not 0.3 (R725 review):
        // where the bar comes from behind (hurdle 1's west half, hurdle 2's east half, where the entrance puts people) its
        // other arm overtakes a pawn walking on at about 1 m/s and still comes at it 0.3 s past the half turn, so the knock
        // woke under it and floored the pawn again from behind under 0.9 s after it stood up (about a third of the blind runs
        // over those halves in the review's model; none at a run with 0.6).
        public const double HurdleFreePass = 0.6;
        public static double HurdleHitCooldown(int h) => HurdleHalfTurn(h) + HurdleFreePass;   // 2.24, 2.4

        // How long that body is let through the bar's log (the builder's YardImpact ghostSeconds): three quarters of a turn,
        // when the bar's line stands square to the spot it hit - after the other arm's free pass and the cooldown (the log is
        // never solid while the knock is blind to that body), before the first arm is back.
        public static double HurdleLetThroughSeconds(int h) => 1.5 * HurdleHalfTurn(h);   // 2.45, 2.7

        // Seconds until the bar passes over the point (dx, dz) from the hub.
        public static double HurdleArrivesIn(int h, double dx, double dz, double t)
        {
            double dps = HurdleDegreesPerSecond[h], alpha = Math.Atan2(-dz, dx) * 180 / Math.PI;
            return Wrap((alpha - HurdleAngle(h, t)) * Math.Sign(dps), 180) / Math.Abs(dps);
        }

        // When to press jump, hips r from the hub, as HurdleArrivesIn in [lo, hi): the feet stay over the bar's top
        // 0.12..0.57 s after take-off and bar + body pass in +-HurdleBodyHalf / (omega r). lo >= hi: too near the hub to jump
        // it (a slow bar there only staggers): inside 1.04 m (hurdle 1) and 1.14 m (hurdle 2).
        public static void HurdleJumpWindow(int h, double r, out double lo, out double hi)
        {
            const double g = 9.81 * 1.4, gf = 9.81 * 1.4 * 1.15, v = 4.9;
            double apex = v * v / (2 * g), up = (v - Math.Sqrt(v * v - 2 * g * HurdleBarHigh)) / g,
                   down = v / g + Math.Sqrt(2 * (apex - HurdleBarHigh) / gf);
            double half = HurdleBodyHalf / (Math.Abs(HurdleDegreesPerSecond[h]) * Math.PI / 180 * Math.Max(r, 1e-3));
            lo = up + half;
            hi = down - half;
        }

        // ------------------------------------------------------------------ V03 vault bulkheads

        // Two pocket panels per bulkhead: side -1 is the west panel, which closes the west half; +1 the east one. The panels
        // of a bulkhead run half a cycle apart, so a 4 m gap always stays open and slides from half to half; bulkhead 2 runs
        // half a cycle from bulkhead 1.
        public static readonly double[] BulkheadZ = { 31.4, 35.4 }, BulkheadPhase = { 0.0, 3.0 };
        public const double BulkheadCycle = 6.0, BulkheadLeg = 1.3, BulkheadRamp = 0.3, BulkheadTravel = 4.0, BulkheadHalfWidth = 4.0,
                            BulkheadWarnSeconds = 1.0;
        public static double BulkheadRest => BulkheadCycle / 2 - BulkheadLeg;   // 1.7 s in, 1.7 s out

        static double BU(int b, int side, double t) => Wrap(t + BulkheadPhase[b] + (side > 0 ? BulkheadCycle / 2 : 0), BulkheadCycle);

        // 0 in its pocket .. 1 closing its half.
        public static double BulkheadOut(int b, int side, double t)
        {
            double u = BU(b, side, t), r = BulkheadRest;
            if (u < r) return 0;
            u -= r;
            if (u < BulkheadLeg) return SortingYardRules.Profile(u, BulkheadLeg, BulkheadRamp);
            u -= BulkheadLeg;
            if (u < r) return 1;
            u -= r;
            return 1 - SortingYardRules.Profile(u, BulkheadLeg, BulkheadRamp);
        }

        // Whether a body at x (across from the corridor's middle, -4..4) overlaps either panel.
        public static bool BulkheadBlocked(int b, double x, double t)
        {
            double west = -BulkheadHalfWidth + BulkheadTravel * BulkheadOut(b, -1, t),
                   east = BulkheadHalfWidth - BulkheadTravel * BulkheadOut(b, 1, t);
            return x - BodyHalf < west || x + BodyHalf > east;
        }

        // Seconds a body at x stays clear (0.02 s steps): 0 while blocked, a whole cycle if never.
        public static double BulkheadClearFor(int b, double x, double t)
        {
            if (BulkheadBlocked(b, x, t)) return 0;
            for (double s = 0.02; s < BulkheadCycle; s += 0.02) if (BulkheadBlocked(b, x, t + s)) return s;
            return BulkheadCycle;
        }

        // The floor strip of one half, judged at the half's middle.
        public static Cue BulkheadCue(int b, int side, double t)
        {
            double c = BulkheadClearFor(b, side * BulkheadHalfWidth / 2, t);
            return c >= BulkheadWarnSeconds ? Cue.Go : c > 0 ? Cue.Warn : Cue.Stop;
        }

        // ------------------------------------------------------------------ V04 dial hammers

        // Two Spinners turning about +x over the walkway, half a turn apart. Angle 0 is the head hanging down; positive turns
        // sweep -z at the bottom.
        public static readonly double[] HammerAxleZ = { 65.5, 77.5 };
        public const double HammerDegreesPerSecond = 60, HammerPairSeconds = 3.0, HammerAxleAbove = 4.4, HammerInner = 2.9, HammerOuter = 4.3,
                            HammerAlong = 2.2, HammerAcross = 8.4, HammerBodyTop = 1.2, HammerBodyMargin = 0.4;

        public static double HammerPhase(int i) => i * HammerPairSeconds;   // half a turn apart

        public static double HammerAngle(int i, double t) => Wrap(HammerDegreesPerSecond * (t + HammerPhase(i)), 360);

        // The head's outer corner, from the axle.
        static double Corner => Math.Sqrt(HammerOuter * HammerOuter + HammerAlong * HammerAlong / 4);

        // Half the arc (degrees either side of hanging down) through which part of the head is below HammerBodyTop.
        public static double HammerBodyHalfAngle =>
            (Math.Acos((HammerAxleAbove - HammerBodyTop) / Corner) + Math.Atan2(HammerAlong / 2, HammerOuter)) * 180 / Math.PI;   // 58.22

        // Half the length of walkway the head sweeps at body height, plus a margin.
        public static double HammerZoneHalf => Math.Sqrt(Corner * Corner - Math.Pow(HammerAxleAbove - HammerBodyTop, 2)) + HammerBodyMargin;   // 3.48

        public static bool HammerAtBody(int i, double t)
        {
            double a = HammerAngle(i, t), b = HammerBodyHalfAngle;
            return a <= b || a >= 360 - b;
        }

        // Seconds until the head is down at body height again (0 while it is there).
        public static double HammerClearFor(int i, double t)
        {
            double a = HammerAngle(i, t), b = HammerBodyHalfAngle;
            return a <= b || a >= 360 - b ? 0 : (360 - b - a) / HammerDegreesPerSecond;
        }

        // Seconds since the head left body height (0 while it is there).
        public static double HammerSinceClear(int i, double t)
        {
            double a = HammerAngle(i, t), b = HammerBodyHalfAngle;
            return a <= b || a >= 360 - b ? 0 : (a - b) / HammerDegreesPerSecond;
        }

        // The z of the head's middle.
        public static double HammerHeadZ(int i, double t) => HammerAxleZ[i] - (HammerInner + HammerOuter) / 2 * Math.Sin(HammerAngle(i, t) * Math.PI / 180);

        // Stop at body height, Warn in the last second before it, Go for a second after it leaves, Off otherwise.
        public static Cue HammerCue(int i, double t) =>
            HammerAtBody(i, t) ? Cue.Stop : HammerClearFor(i, t) < 1 ? Cue.Warn : HammerSinceClear(i, t) < 1 ? Cue.Go : Cue.Off;

        // ------------------------------------------------------------------ V05 crown press

        // A full-width die over the neck. The cycle starts at a landing: sit, rise (smoothstep), up, warning, fall. The die's one
        // crush shell (the builder's YardImpact round it) reaches PressCrushBeyond past its faces and ends and PressCrushBelow
        // under its bottom, so a standing piece (its top HammerBodyTop over the floor, as the hammers judge it) is clear of
        // that reach only while the die's bottom stands PressHeadroom over the floor.
        public const double PressX = -2, PressZ = 110, PressDieAcross = 8, PressDieAlong = 2.4, PressDieTall = 1.6, PressDrop = 3.8,
                            PressPeriod = 4.5, PressSit = 0.4, PressRise = 1.0, PressWarnSeconds = 1.2, PressFall = 0.25,
                            PressBandOut = 1.8, PressWaveSeconds = 0.3, PressWavePush = 4.0, PressWaveLift = 2.0,
                            PressKnockdown = 1.0, PressCrushPush = 6.0, PressCrushBeyond = 0.15, PressCrushBelow = 0.3;
        public const double PressHeadroom = HammerBodyTop + PressCrushBelow;   // 1.5
        public static double PressBandZ0 => PressZ - PressDieAlong / 2 - PressBandOut;   // 107
        public static double PressBandZ1 => PressZ + PressDieAlong / 2 + PressBandOut;   // 113

        public enum PressStage { Down, Rising, Up, Warning, Falling }

        public static double PressSinceLanding(double t) => Wrap(t, PressPeriod);

        public static double PressLandsIn(double t) => PressPeriod - PressSinceLanding(t);

        public static PressStage PressAt(double t)
        {
            double s = PressSinceLanding(t);
            if (s < PressSit) return PressStage.Down;
            if (s < PressSit + PressRise) return PressStage.Rising;
            if (s < PressPeriod - PressFall - PressWarnSeconds) return PressStage.Up;   // 1.65 s up
            return s < PressPeriod - PressFall ? PressStage.Warning : PressStage.Falling;
        }

        // 1 on the floor .. 0 up.
        public static double PressLowered(double t)
        {
            double s = PressSinceLanding(t);
            if (s < PressSit) return 1;
            if (s < PressSit + PressRise) return 1 - Smooth((s - PressSit) / PressRise);
            double f = s - (PressPeriod - PressFall);
            if (f < 0) return 0;
            double u = f / PressFall;
            return u * u;   // lands at 2 x 3.8 / 0.25 = 30.4 m/s
        }

        // The die's bottom over the neck's floor.
        public static double PressDieBottom(double t) => PressDrop * (1 - PressLowered(t));

        // Whether a body may step into the band now and cross it at the steady speed that takes crossSeconds over the band's
        // length: a body BodyHalf either way of its middle along z, its front on the band's south edge now (its middle on the
        // wait line, PressBandZ0 - BodyHalf). Not in the landing's wave; its middle (the wave judges the hips) out of the band
        // 0.1 s before the next landing; and no part of it within the crush shell's reach while the die's bottom is under
        // PressHeadroom - checked from its front's way in to its back's way out, at both ends and every 0.05 s between.
        // R725: it used to put the middle on the band's edge and keep only the middle from under the die, so a sprinter let go
        // at 0.33 s met the die with its front while the bottom was 1.05 m up (the shell 0.3 m under that), and a runner let go
        // at 0.30 s had its hips on the band's edge as the wave reached it.
        public static bool PressSafeToCross(double t, double crossSeconds)
        {
            if (!(crossSeconds > 0)) return false;
            double v = (PressBandZ1 - PressBandZ0) / crossSeconds, middle = PressBandZ0 - BodyHalf;
            if (PressSinceLanding(t) < PressWaveSeconds || PressLandsIn(t) <= (PressBandZ1 - middle) / v + 0.1) return false;
            double reach = PressDieAlong / 2 + PressCrushBeyond + BodyHalf;
            double enter = (PressZ - reach - middle) / v, leave = (PressZ + reach - middle) / v;
            for (double tau = enter; tau < leave; tau += 0.05) if (PressDieBottom(t + tau) < PressHeadroom) return false;
            return PressDieBottom(t + leave) >= PressHeadroom;
        }

        // Stop during the landing's wave; Go while a runner may step in (PressSafeToCross at RunSpeed); Warn from "too late to
        // step in" through the fall.
        public static Cue PressCue(double t) => PressSinceLanding(t) < PressWaveSeconds ? Cue.Stop
            : PressSafeToCross(t, (PressBandZ1 - PressBandZ0) / RunSpeed) ? Cue.Go : Cue.Warn;

        // ------------------------------------------------------------------ V06 jewel belts

        // Belt 0 west, 1 east; m/s along +z. The west belt always runs back; the east one stands stopped, then runs back, in
        // EastHalf halves with EastRamp smoothstep ramps. Neither ever runs forward or back faster than 1.5 m/s (R518).
        public const double BeltWestX0 = -6, BeltSplitX = -1, BeltEastX1 = 4;
        public const double WestBeltSpeed = -1.0, EastStopSpeed = 0.0, EastBackSpeed = -1.5, EastHalf = 5.0, EastRamp = 0.5, EastWarnSeconds = 1.0;

        public static double BeltSpeed(int belt, double t)
        {
            if (belt == 0) return WestBeltSpeed;
            double u = Wrap(t, 2 * EastHalf);
            if (u < EastHalf) return u < EastRamp ? EastBackSpeed + (EastStopSpeed - EastBackSpeed) * Smooth(u / EastRamp) : EastStopSpeed;
            u -= EastHalf;
            return u < EastRamp ? EastStopSpeed + (EastBackSpeed - EastStopSpeed) * Smooth(u / EastRamp) : EastBackSpeed;
        }

        // Seconds the east belt still stands stopped (0 while it ramps or runs back).
        public static double EastStoppedFor(double t)
        {
            double u = Wrap(t, 2 * EastHalf);
            return u >= EastRamp && u < EastHalf ? EastHalf - u : 0;
        }

        // The east belt's last EastWarnSeconds stopped.
        public static bool EastWarn(double t)
        {
            double u = Wrap(t, 2 * EastHalf);
            return u >= EastHalf - EastWarnSeconds && u < EastHalf;
        }

        public static Cue BeltCue(int belt, double t) => belt == 0 ? Cue.Stop : EastWarn(t) ? Cue.Warn : EastStoppedFor(t) > 0 ? Cue.Go : Cue.Stop;

        // Metres a point of the belt has moved along +z since clock 0 (closed form; the slats use it).
        public static double BeltTravel(int belt, double t)
        {
            if (belt == 0) return WestBeltSpeed * t;
            double c = 2 * EastHalf, n = Math.Floor(t / c), u = t - n * c, a = EastBackSpeed, s = EastStopSpeed, r = EastRamp, h = EastHalf;
            double d = n * (RampTravel(a, s, r) + (h - r) * s + RampTravel(s, a, r) + (h - r) * a);
            if (u < h) return d + RampTravel(a, s, Math.Min(u, r)) + Math.Max(0, u - r) * s;
            d += RampTravel(a, s, r) + (h - r) * s;
            u -= h;
            return d + RampTravel(s, a, Math.Min(u, r)) + Math.Max(0, u - r) * a;
        }

        // Integral of lo + (hi - lo) Smooth(v / r) over v in [0, tau]: lo tau + (hi - lo) r (x^3 - x^4 / 2), x = tau / r.
        static double RampTravel(double lo, double hi, double tau)
        {
            double x = tau / EastRamp;
            return lo * tau + (hi - lo) * EastRamp * (x * x * x - x * x * x * x / 2);
        }

        // Seconds to ride `length` metres of a belt walking at v from t (0.02 s steps, at most 60): the bots' belt choice and
        // the fork 2 model. Arrival allows 1e-9 m: the east belt's two ramps are 250 steps apart and cancel step for step, so
        // from a whole range of start times a run reaches `length` exactly, and rounding alone then decided the last step,
        // differently at clock 0 and at Unix-scale clocks (R725 Core test "Royal vault clock").
        public static double BeltRideSeconds(int belt, double t, double v, double length)
        {
            double z = 0, s = 0;
            while (z < length - 1e-9 && s < 60)
            {
                z += (v + BeltSpeed(belt, t + s)) * 0.02;
                s += 0.02;
            }
            return s;
        }

        // ------------------------------------------------------------------ V07 knight launch pad

        // A sameThrow LaunchPad: LaunchPad.ArcVelocity under plain gravity (launched pawns skip the Pawn Rush gravity), from
        // the pad's floor to (PadLandX, PadLandY, PadLandZ), peaking PadClearance over the landing. PadCatch: how far outside
        // the trigger a pawn's hips may still be when the pad first catches it.
        public const double PadX = 6.0, PadZ = 84.6, PadSize = 1.6, PadFloor = 4.0, PadLandX = 5.0, PadLandY = 8.0, PadLandZ = 93.3,
                            PadClearance = 1.9, PadGravity = 9.81, PadCatch = 0.6;

        public static void PadThrow(out double up, out double vx, out double vz, out double seconds)
        {
            double rise = PadLandY - PadFloor, apex = Math.Max(rise, 0) + PadClearance;              // 5.9
            up = Math.Sqrt(2 * PadGravity * apex);                                                   // 10.76
            seconds = Math.Sqrt(2 * apex / PadGravity) + Math.Sqrt(2 * (apex - rise) / PadGravity);  // 1.719
            vx = (PadLandX - PadX) / seconds;                                                        // -0.58
            vz = (PadLandZ - PadZ) / seconds;                                                        // 5.06
        }

        // The feet of a pawn thrown from the ground point (x0, PadFloor, z0), s seconds into the flight (sameThrow: every pawn
        // gets the same velocity).
        public static void PadFlight(double x0, double z0, double s, out double x, out double y, out double z)
        {
            PadThrow(out double up, out double vx, out double vz, out _);
            x = x0 + vx * s;
            y = PadFloor + up * s - PadGravity * s * s / 2;
            z = z0 + vz * s;
        }

        // ------------------------------------------------------------------ V08 vault drawers

        // Two drawers sweep the shelf's full width out of the cabinet: the face is at ShelfX1 when in, at ShelfX0 when out.
        // Drawer 2 runs 1.5 s after drawer 1 (the design's wave along the wall).
        public static readonly double[] DrawerZ = { 98.0, 101.8 }, DrawerPhase = { 0.0, -1.5 };
        public const double ShelfX0 = 3, ShelfX1 = 7, ShelfZ0 = 91, ShelfOpenZ0 = 96.3, ShelfOpenZ1 = 103.3;
        public const double DrawerLong = 2.4, DrawerTravel = 4.0, DrawerRest = 2.4, DrawerOutSeconds = 1.0, DrawerHold = 0.4,
                            DrawerInSeconds = 1.0, DrawerRamp = 0.25, DrawerWarnSeconds = 1.0;
        public static double DrawerCycle => DrawerRest + DrawerOutSeconds + DrawerHold + DrawerInSeconds;   // 4.8

        // R725 fun pass: how long a drawer's knockdown keeps the pawn down (the builder's YardImpact knockdownSeconds). The
        // ragdoll counts it while the body lies low or at rest - on the shelf from the hit on, then on the belt - but not in
        // the 0.71 s fall, and a Pawn Rush pawn then stands and settles in 0.2 s (PawnRushPhysicsParams getUpBlendTime and
        // upSettleTime, 0.1 each): 2.71 s from the hit to running wherever on the shelf the hit came, the old shove's 1.21 s
        // (ForkTwoDropCost's 0.5 s on the shelf and the fall) and the 1.5 s a sweep was meant to add. R725 review: the
        // default 0.9 s added only about 0.6 s, so a swept pad run still tied the belts at a run.
        public const double DrawerKnockdown = 1.8;

        // R725 review: the drawer's hit (the builder's YardImpact shell round the drawer) starts DrawerShellBeyond west of the
        // face, as the press's crush shell reaches PressCrushBeyond past the die, so DrawerClearFor and the fork 2 model time a
        // body against that reach (DrawerReachX), not the face: by the face alone a careful bot's window ended about 0.06 s
        // after the knockdown could come.
        public const double DrawerShellBeyond = 0.3;

        static double DU(int i, double t) => Wrap(t + DrawerPhase[i], DrawerCycle);

        // 0 in the cabinet .. 1 out across the shelf.
        public static double DrawerOut(int i, double t)
        {
            double u = DU(i, t);
            if (u < DrawerRest) return 0;
            u -= DrawerRest;
            if (u < DrawerOutSeconds) return SortingYardRules.Profile(u, DrawerOutSeconds, DrawerRamp);
            u -= DrawerOutSeconds;
            if (u < DrawerHold) return 1;
            u -= DrawerHold;
            return 1 - SortingYardRules.Profile(u, DrawerInSeconds, DrawerRamp);   // face peak 4 / 0.75 = 5.33 m/s
        }

        public static double DrawerFaceX(int i, double t) => ShelfX1 - DrawerTravel * DrawerOut(i, t);

        // How far west the drawer's hit reaches: its shell's west face.
        public static double DrawerReachX(int i, double t) => DrawerFaceX(i, t) - DrawerShellBeyond;

        // The last DrawerWarnSeconds of the rest.
        public static bool DrawerWarn(int i, double t)
        {
            double u = DU(i, t);
            return u >= DrawerRest - DrawerWarnSeconds && u < DrawerRest;
        }

        // Seconds the drawer's reach (DrawerReachX) stays east of a body at x (0.02 s steps): 0 while it is not, a whole cycle
        // if always.
        public static double DrawerClearFor(int i, double x, double t)
        {
            if (DrawerReachX(i, t) < x + BodyHalf) return 0;
            for (double s = 0.02; s < DrawerCycle; s += 0.02) if (DrawerReachX(i, t + s) < x + BodyHalf) return s;
            return DrawerCycle;
        }

        // Warn in the last second of the rest, Stop while out or moving, Off in the cabinet.
        public static Cue DrawerCue(int i, double t) => DrawerWarn(i, t) ? Cue.Warn : DrawerOut(i, t) > 0 ? Cue.Stop : Cue.Off;

        // ------------------------------------------------------------------ fork 2 time model

        // For the Core test and the bots' report; not used in play. From (0, 4, WalkZ1) at t0 to (0, 8, 129), ideal play at
        // speed v: the faster belt, the first safe press start, the first fully clear pass at each drawer (body at x 5), the
        // pad's first contact 0.3 m outside its south-west corner, 0.4 s to stand after landing.
        public static double ForkTwoSafeSeconds(double t0, double v)
        {
            double t = t0 + Hyp(1.5, BeltZ0 - WalkZ1) / v, length = BeltZ1 - BeltZ0;
            t += Math.Min(BeltRideSeconds(0, t, v, length), BeltRideSeconds(1, t, v, length));
            return SafeFromBeltEnd(t, v) - t0;
        }

        public static double ForkTwoRiskySeconds(double t0, double v)
        {
            PadThrow(out _, out _, out _, out double flight);
            double cx = PadX - PadSize / 2 - BodyHalf, cz = PadZ - PadSize / 2, z = cz + (PadLandZ - PadZ);
            double t = ShelfWalk(t0 + Hyp(cx, cz - WalkZ1) / v + flight + 0.4, v, ref z);
            return t + (StairTwoZ1 - z) / v + Hyp(5, 3) / v - t0;
        }

        // At the drawer-1 wait line: seconds lost by being knocked off onto the belts now, against waiting on the shelf. The
        // knockdown (DrawerKnockdown, counted on the shelf and the belt) and the 0.2 s get-up, the 4 m fall, then the belts.
        public static double ForkTwoDropCost(double t0, double v)
        {
            double t = t0 + DrawerKnockdown + 0.2 + Math.Sqrt(2 * 4.0 / (9.81 * 1.4 * 1.15)), length = BeltZ1 - DrawerZ[0];
            t += Math.Min(BeltRideSeconds(0, t, v, length), BeltRideSeconds(1, t, v, length));
            double pushed = SafeFromBeltEnd(t, v), z = DrawerZ[0] - DrawerLong / 2 - BodyHalf - 0.05;
            double kept = ShelfWalk(t0, v, ref z) + (StairTwoZ1 - z) / v + Hyp(5, 3) / v;
            return pushed - kept;
        }

        // From the belts' end at t: to the press wait line (the front on the band's edge, as PressSafeToCross has it), the first
        // safe start, through the neck and up stair 2 to (0, 8, 129).
        static double SafeFromBeltEnd(double t, double v)
        {
            double wait = PressBandZ0 - BodyHalf, cross = (PressBandZ1 - PressBandZ0) / v;
            t += Hyp(1.5 - PressX, wait - BeltZ1) / v;
            for (int k = 0; k < 1000 && !PressSafeToCross(t, cross); k++) t += 0.01;
            return t + (NeckZ1 - wait + StairSlope) / v + Hyp(PressX, 3) / v;
        }

        // Along the shelf (at x 5) from z past both drawers, each at its first fully clear pass; z ends behind the last.
        static double ShelfWalk(double t, double v, ref double z)
        {
            for (int i = 0; i < DrawerZ.Length; i++)
            {
                double back = DrawerZ[i] + DrawerLong / 2 + BodyHalf, line = DrawerZ[i] - DrawerLong / 2 - BodyHalf - 0.05;
                if (z < line)
                {
                    t += (line - z) / v;
                    z = line;
                }
                for (int k = 0; k < 2000 && !ShelfPassClear(i, t, z, back, v); k++) t += 0.01;
                t += (back - z) / v;
                z = back;
            }
            return t;
        }

        static bool ShelfPassClear(int i, double t, double z, double back, double v)
        {
            double front = DrawerZ[i] - DrawerLong / 2 - BodyHalf;
            for (double s = 0; z + v * s < back; s += 0.01) if (z + v * s > front && DrawerReachX(i, t + s) < 5 + BodyHalf) return false;
            return true;
        }

        // ------------------------------------------------------------------ V09 flipping plates

        // Row 0 west, 1 east, PlateCount plates each, hinged on their outer edges so the inner edge lifts. The rows run half a
        // cycle apart: one is always flat, and both are flat for 1.2 s twice a cycle.
        public const int PlateCount = 5;
        public const double PlateZ0 = 136, PlatePitch = 4, PlateWide = 3.97, PlateLong = 3.95, PlateCycle = 8.0, PlateRaise = 1.0,
                            PlateHold = 0.8, PlateLower = 1.0, PlateAngleMax = 50, PlateWarnSeconds = 1.6, PlateSlipAngle = 12;
        public static double PlateFlat => PlateCycle - PlateRaise - PlateHold - PlateLower;   // 5.2

        public static double PlatePhase(int row) => row * PlateCycle / 2;   // 0, 4

        static double PU(int row, double t) => Wrap(t + PlatePhase(row), PlateCycle);

        // Degrees the inner edge is lifted.
        public static double PlateAngle(int row, double t)
        {
            double u = PU(row, t);
            if (u < PlateFlat) return 0;
            u -= PlateFlat;
            if (u < PlateRaise) return PlateAngleMax * Smooth(u / PlateRaise);
            u -= PlateRaise;
            if (u < PlateHold) return PlateAngleMax;
            u -= PlateHold;
            return PlateAngleMax * (1 - Smooth(u / PlateLower));   // peak 75 deg/s: inner edge 5.2 m/s
        }

        // Seconds the row still lies flat (0 while it is tilted).
        public static double PlateFlatFor(int row, double t)
        {
            double u = PU(row, t);
            return u < PlateFlat ? PlateFlat - u : 0;
        }

        // The PlateWarnSeconds warning, and the lift after it.
        public static bool PlateWarn(int row, double t) => PU(row, t) >= PlateFlat - PlateWarnSeconds;

        // Stop while tilted, Warn during the warning, Off otherwise.
        public static Cue PlateCue(int row, double t) => PlateAngle(row, t) > 0 ? Cue.Stop : PlateWarn(row, t) ? Cue.Warn : Cue.Off;

        // ------------------------------------------------------------------ V10 time-lock door

        // A disc rolls DoorTravel east out of an octagonal doorway (inradius DoorWayInradius, centre DoorCentreAbove over the
        // floor) and back. The period starts as the disc starts rolling open, toward +x: roll open, open, roll shut, shut.
        public const double DoorPeriod = 15, DoorRoll = 2.3, DoorRollRamp = 0.5, DoorOpenHold = 3.1, DoorTravel = 9, DoorRadius = 4.5,
                            DoorThick = 1, DoorCentreAbove = 3, DoorWayInradius = 3.9, DoorPassWidth = 2.5, DoorClosingWarnSeconds = 1.5;
        public static double DoorShutHold => DoorPeriod - DoorOpenHold - 2 * DoorRoll;   // 7.3

        // 0 shut .. 1 rolled open.
        public static double DoorOpen(double t)
        {
            double u = Wrap(t, DoorPeriod);
            if (u < DoorRoll) return SortingYardRules.Profile(u, DoorRoll, DoorRollRamp);   // 5 m/s cruise
            u -= DoorRoll;
            if (u < DoorOpenHold) return 1;
            u -= DoorOpenHold;
            if (u < DoorRoll) return 1 - SortingYardRules.Profile(u, DoorRoll, DoorRollRamp);
            return 0;
        }

        // The doorway's clear width 1 m over the floor.
        public static double DoorClearWidth(double t)
        {
            double y = 1 - DoorCentreAbove, tan = Math.Tan(Math.PI / 8);
            double half = Math.Abs(y) <= DoorWayInradius * tan ? DoorWayInradius : DoorWayInradius * (1 + tan) - Math.Abs(y);   // 3.515
            double chord = Math.Sqrt(DoorRadius * DoorRadius - y * y);                                                     // 4.031
            return Math.Max(0, Math.Min(half, DoorTravel * DoorOpen(t) - chord) + half);
        }

        // Seconds into the period from which, and until which, the clear width is at least DoorPassWidth.
        public static readonly double DoorPassableStart = FindPassableStart();                       // 0.853
        public static double DoorPassableEnd => 2 * DoorRoll + DoorOpenHold - DoorPassableStart;   // 6.847: 5.99 s passable per 15 s

        static double FindPassableStart()
        {
            double lo = 0, hi = DoorRoll;
            for (int k = 0; k < 60; k++)
            {
                double mid = (lo + hi) / 2;
                if (DoorClearWidth(mid) >= DoorPassWidth) hi = mid;
                else lo = mid;
            }
            return hi;
        }

        // Seconds the door stays passable (0 while it is not).
        public static double DoorPassableFor(double t)
        {
            double u = Wrap(t, DoorPeriod);
            return u >= DoorPassableStart && u < DoorPassableEnd ? DoorPassableEnd - u : 0;
        }

        // Seconds until the door is next passable (0 while it is).
        public static double DoorOpensIn(double t)
        {
            double u = Wrap(t, DoorPeriod);
            return u < DoorPassableStart ? DoorPassableStart - u : u < DoorPassableEnd ? 0 : DoorPeriod - u + DoorPassableStart;
        }

        // 3, 2, 1 through the last three seconds before the door is passable, 0 otherwise.
        public static int DoorCountdown(double t)
        {
            double w = DoorOpensIn(t);
            return w > 0 && w <= 3 ? (int)Math.Ceiling(w) : 0;
        }

        // The last DoorClosingWarnSeconds of the passable time.
        public static bool DoorClosingWarn(double t)
        {
            double u = Wrap(t, DoorPeriod);
            return u >= DoorPassableEnd - DoorClosingWarnSeconds && u < DoorPassableEnd;
        }

        // The countdown signs' text and look: the digits in ink (Off), "closed" in Warn while closing, "open" in Go while
        // passable, nothing otherwise.
        public static string DoorSign(double t, out Cue look)
        {
            int n = DoorCountdown(t);
            if (n > 0)
            {
                look = Cue.Off;
                return n.ToString();
            }
            if (DoorClosingWarn(t))
            {
                look = Cue.Warn;
                return "닫힘";
            }
            if (DoorPassableFor(t) > 0)
            {
                look = Cue.Go;
                return "열림";
            }
            look = Cue.Off;
            return "";
        }
    }
}
