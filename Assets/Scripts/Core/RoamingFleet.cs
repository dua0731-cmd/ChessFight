using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    // The Royal Carriage Yard's roaming carriages (R286, user 11-03: "넓은 공간 구역에서만 랜덤하게 돌진하며 돌아다니는
    // 마차 4개"): the carriages share one yard and keep charging to random points anywhere in it - turn on the spot to
    // face the next point, stand with the lamps red, charge, rest - and never touch one another. Like the grand stair
    // hall's schedules (SeededSchedule): one generator (mulberry32 from the yard's seed) writes every carriage's legs
    // from a start time on the shared obstacle clock, and a carriage's pose is a function of its legs and the clock.
    // The same seed and start give every PC the same carriages; nothing is sent.
    //
    // Never touching: a carriage plans its next leg when the last one ends, and its whole sweep - the straight line it
    // will charge along, with BodyRadius round it, which also covers turning on the spot at one end and resting at the
    // other - must keep Clearance off every other carriage's current leg. Legs are planned in the order they start, so
    // each carriage's current leg is already fixed for every carriage that plans after it, and the next leg it plans
    // will keep off theirs in turn. No point free: it waits a moment where it is and tries again.
    //
    // A clock that leaps by more than Leap (switched to the shared match clock or back: Unix time against seconds since
    // the scene loaded) starts the plan again there, from the homes, as a fleet made then would. Planning every leg in
    // between would never end, and a clock gone back would leave the carriages waiting for a start that never comes.
    //
    // No Unity: the tests check that no two carriages ever come within 2 BodyRadius + Clearance of each other.
    public sealed class RoamingFleet
    {
        public const double Leap = 3600;

        public enum Stage { Waiting, Turning, Aiming, Charging, Resting }

        // The Royal Carriage Yard's seeds (R287): a match draws one by its round seed (YardSeed), so each round has its
        // own dance but never a poor one. The Core tests check each on the yard's own settings over ten minutes:
        // someone charging at least 85 % of the time, a charging body over the straight way up the middle (|x| <= 3)
        // at least 30 % of it, waits under 4 % of the legs, after its first charge no carriage standing more than 5.5 s
        // at a time, every carriage reaching every edge of the yard, never two closer than Spacing, the same dance on
        // scene time and on the match clock, and in the first 20 s the charges near the gate split at least 40 / 60
        // between its west and east halves (neither team's side of the start is favoured). The tests also pin each
        // seed's first legs: a change to the yard's numbers fails them until the list is vetted again.
        public static readonly uint[] YardSeeds = { 38, 41, 160, 44, 18, 252, 20, 113 };

        public static uint YardSeed(int roundSeed) => YardSeeds[SeededSchedule.Hash(roundSeed, 0xCA27) % (uint)YardSeeds.Length];

        // The yard the seeds were vetted on, as RoyalCarriageYard and CarriageFleet build it (R287): the centres 0.8 m
        // past the body's reach inside its walls (x +-22, z 2..50) and 2.5 m more off the gate, four carriages at
        // 9 + 0.3 i m/s, homes two to a row facing the middle. The tests vet the seeds on it; the map warns when its own
        // fleet does not match it (Matches), as the seeds may then dance worse.
        public static RoamingFleet Yard(uint seed, double start)
        {
            double reach = .5 * Math.Sqrt(4.2 * 4.2 + 2.3 * 2.3), keep = reach + .8;
            var rules = new Rules { MinX = -(22 - keep), MaxX = 22 - keep, MinZ = 2 + keep + 2.5, MaxZ = 50 - keep, BodyRadius = reach };
            var speeds = new double[4];
            var hx = new double[4];
            var hz = new double[4];
            var yaw = new double[4];
            for (int i = 0; i < 4; i++)
            {
                speeds[i] = 9 + .3 * i;
                double side = i % 2 == 0 ? -1 : 1;
                hx[i] = (rules.MinX + rules.MaxX) * .5 + side * (rules.MaxX - rules.MinX) * .25;
                hz[i] = rules.MinZ + (i / 2 + .5) * (rules.MaxZ - rules.MinZ) / 2;
                yaw[i] = side < 0 ? 90 : -90;
            }
            return new RoamingFleet(seed, start, speeds, hx, hz, yaw, rules);
        }

        // The same carriages under the same rules (within `tolerance`, the Unity side works in floats): everything a
        // seed's dance depends on but the seed and the start.
        public bool Matches(RoamingFleet other, double tolerance = 1e-3)
        {
            if (other == null || other.Count != Count) return false;
            var a = rules;
            var b = other.rules;
            double[] mine = { a.MinX, a.MaxX, a.MinZ, a.MaxZ, a.BodyRadius, a.Clearance, a.Turn, a.MinTurn, a.MinAim, a.MaxAim, a.MinLead,
                              a.MinRest, a.MaxRest, a.Wait, a.SpeedUp, a.SlowDown, a.MinCharge, a.MaxCharge, a.Stagger, a.Tries };
            double[] theirs = { b.MinX, b.MaxX, b.MinZ, b.MaxZ, b.BodyRadius, b.Clearance, b.Turn, b.MinTurn, b.MinAim, b.MaxAim, b.MinLead,
                                b.MinRest, b.MaxRest, b.Wait, b.SpeedUp, b.SlowDown, b.MinCharge, b.MaxCharge, b.Stagger, b.Tries };
            for (int i = 0; i < mine.Length; i++)
                if (Math.Abs(mine[i] - theirs[i]) > tolerance) return false;
            for (int c = 0; c < Count; c++)
                if (Math.Abs(speed[c] - other.speed[c]) > tolerance || Math.Abs(homeX[c] - other.homeX[c]) > tolerance
                    || Math.Abs(homeZ[c] - other.homeZ[c]) > tolerance || Math.Abs(Delta(homeYaw[c], other.homeYaw[c])) > tolerance
                    || Math.Abs(radius[c] - other.radius[c]) > tolerance || Math.Abs(turn[c] - other.turn[c]) > tolerance)
                    return false;
            return true;
        }

        public sealed class Rules
        {
            // Where the carriages' centres may go (the yard's frame, metres). The defaults are the yard's (R287:
            // its walls 22 m either side, its south edge at 2 m and north at 50 m, the bodies 0.8 m off them and
            // 2.5 m more off the gate); RoyalCarriageYard passes its own from the Inspector.
            public double MinX = -18.805736, MaxX = 18.805736, MinZ = 7.694264, MaxZ = 46.805736;
            public double BodyRadius = 2.394264; // a body's reach from its centre, whichever way it faces
            public double Clearance = .6;        // kept between two bodies on top of that
            // Turning on the spot to face the next point: seconds for a half turn, a smaller turn its share of them
            // (so the body's ends never swing faster than a half turn swings them), but never under MinTurn.
            public double Turn = 2.2;
            public double MinTurn = .4;
            public double MinAim = .6, MaxAim = 1.2;   // standing with the lamps red before the charge
            public double MinLead = 1.8;         // the turn and the aim together at least this: the charge's warning
            public double MinRest = .4, MaxRest = 1.4; // standing after it
            public double Wait = .5;             // no point free: stand this long and try again
            public double SpeedUp = .2, SlowDown = .15;   // parts of the charge spent speeding up and slowing down
            public double MinCharge = 10, MaxCharge = 32; // how far a charge goes
            public int Tries = 32;               // random points tried before waiting
            public double Stagger = 1.1;         // carriage i starts its first leg i * Stagger after the start
        }

        public struct Leg
        {
            public double Start;                 // the turn starts (or the wait)
            public double FromX, FromZ, ToX, ToZ;
            public double HeadingFrom, Heading;  // degrees, 0 = +z, 90 = +x
            public double Turn, Aim, Charge, Rest;
            public double Speed;                 // top speed of this carriage's charges
            public double ChargeStart => Start + Turn + Aim;
            public double ChargeEnd => ChargeStart + Charge;
            public double End => ChargeEnd + Rest;
        }

        public struct Pose
        {
            public double X, Z, Yaw, Speed;
            public Stage Stage;
            // The leg it is on: its start (NaN before the first) - one per charge, what a hit counts by - and where
            // the leg's charge ends, for whoever draws the charge line ahead of it.
            public double LegStart, ToX, ToZ;
            // When the stage it is in began and ends (R626: a loose horse rears through its aim); NaN when it waits.
            public double StageStart, StageEnd;
        }

        readonly Rules rules;
        readonly uint seed;
        readonly double[] speed, homeX, homeZ, homeYaw, radius, turn;
        readonly List<Leg>[] legs;
        double start;
        SeededRandom rng;

        // `speeds`: each body's top speed (m/s); `homeX`/`homeZ`/`homeYaw`: where each stands until its first leg,
        // which must be inside its area and keep the two bodies' reaches + Clearance apart.
        // `radii` and `turns` (R626, optional): each body's own reach from its centre and its own half turn (s), for a
        // fleet of different bodies - horse-drawn carriages and loose horses in one yard. Without them every body is
        // Rules.BodyRadius and Rules.Turn. The area (MinX..MaxZ) is where a body of Rules.BodyRadius may put its centre;
        // a smaller body may come that much nearer the walls (its own reach still keeps the same margin off them).
        public RoamingFleet(uint seed, double start, double[] speeds, double[] homeX, double[] homeZ, double[] homeYaw, Rules rules = null,
                            double[] radii = null, double[] turns = null)
        {
            this.rules = rules ?? new Rules();
            this.seed = seed;
            this.start = start;
            speed = (double[])speeds.Clone();
            this.homeX = (double[])homeX.Clone();
            this.homeZ = (double[])homeZ.Clone();
            this.homeYaw = (double[])homeYaw.Clone();
            radius = new double[speed.Length];
            turn = new double[speed.Length];
            for (int i = 0; i < speed.Length; i++)
            {
                radius[i] = radii != null && i < radii.Length && radii[i] > 0 ? radii[i] : this.rules.BodyRadius;
                turn[i] = turns != null && i < turns.Length && turns[i] > 0 ? turns[i] : this.rules.Turn;
            }
            legs = new List<Leg>[speed.Length];
            for (int i = 0; i < legs.Length; i++) legs[i] = new List<Leg>();
            rng = new SeededRandom(seed);
        }

        public int Count => legs.Length;
        public Rules Settings => rules;
        public double StartTime => start;
        // Made with no start yet (NaN): every carriage stands at home until a fleet with a start replaces it (a match
        // waiting for its shared go).
        public bool Held => double.IsNaN(start);
        public int LegCount(int carriage) => legs[carriage].Count;
        public Leg LegAt(int carriage, int i) => legs[carriage][i];
        public double TopSpeed(int body) => speed[body];
        // A body's reach from its centre, whichever way it faces, and its half turn (s).
        public double Radius(int body) => radius[body];
        public double HalfTurn(int body) => turn[body];

        // How close two bodies' centres may ever come: the uniform fleet's spacing, and any two bodies'.
        public double Spacing => 2 * rules.BodyRadius + rules.Clearance;
        public double SpacingOf(int a, int b) => radius[a] + radius[b] + rules.Clearance;

        // Where body c may put its centre: the area, wider by how much less it reaches than Rules.BodyRadius.
        public void Area(int c, out double minX, out double maxX, out double minZ, out double maxZ)
        {
            double grow = rules.BodyRadius - radius[c];
            minX = rules.MinX - grow;
            maxX = rules.MaxX + grow;
            minZ = rules.MinZ - grow;
            maxZ = rules.MaxZ + grow;
        }

        double NextStart(int c) => legs[c].Count == 0 ? start + c * rules.Stagger : legs[c][legs[c].Count - 1].End;

        // Plans every leg that starts at or before `until`, in the order the legs start (the lower carriage first on
        // a tie), so the legs are the same however the time is advanced.
        public void Advance(double until)
        {
            if (Held) return;
            while (true)
            {
                int c = -1;
                double next = double.MaxValue;
                for (int i = 0; i < legs.Length; i++)
                {
                    double s = NextStart(i);
                    if (s < next) { next = s; c = i; }
                }
                if (c < 0 || next > until) return;
                Plan(c, next);
            }
        }

        // Forgets legs that ended before `before`, keeping each carriage's last one (where it stands).
        public void Prune(double before)
        {
            foreach (var list in legs)
            {
                int n = 0;
                while (n < list.Count - 1 && list[n].End < before) n++;
                if (n > 0) list.RemoveRange(0, n);
            }
        }

        void Current(int c, out double x, out double z, out double yaw)
        {
            var list = legs[c];
            if (list.Count == 0) { x = homeX[c]; z = homeZ[c]; yaw = homeYaw[c]; return; }
            var last = list[list.Count - 1];
            x = last.ToX;
            z = last.ToZ;
            yaw = last.Heading;
        }

        void Plan(int c, double t0)
        {
            double px, pz, yaw;
            Current(c, out px, out pz, out yaw);
            Area(c, out double minX, out double maxX, out double minZ, out double maxZ);
            for (int k = 0; k < Math.Max(1, rules.Tries); k++)
            {
                double qx = rng.Range(minX, maxX), qz = rng.Range(minZ, maxZ);
                double d = Math.Sqrt((qx - px) * (qx - px) + (qz - pz) * (qz - pz));
                if (d < rules.MinCharge || d > rules.MaxCharge || Clashes(c, px, pz, qx, qz)) continue;
                double heading = Math.Atan2(qx - px, qz - pz) * 180.0 / Math.PI, turn = TurnSeconds(c, yaw, heading);
                legs[c].Add(new Leg
                {
                    Start = t0, FromX = px, FromZ = pz, ToX = qx, ToZ = qz, HeadingFrom = yaw, Heading = heading,
                    // A small turn aims longer, so every charge is shown (its line on the floor) MinLead ahead.
                    Turn = turn, Aim = Math.Max(rng.Range(Math.Min(rules.MinAim, rules.MaxAim), Math.Max(rules.MinAim, rules.MaxAim)), rules.MinLead - turn),
                    Charge = ChargeSeconds(d, speed[c]),
                    Rest = rng.Range(Math.Min(rules.MinRest, rules.MaxRest), Math.Max(rules.MinRest, rules.MaxRest)),
                    Speed = speed[c],
                });
                return;
            }
            // Nowhere free: stand where it is and try again in a moment.
            legs[c].Add(new Leg
            {
                Start = t0, FromX = px, FromZ = pz, ToX = px, ToZ = pz, HeadingFrom = yaw, Heading = yaw,
                Rest = Math.Max(.05, rules.Wait), Speed = speed[c],
            });
        }

        // Would carriage c's sweep from p to q come too close to any other carriage's current leg?
        bool Clashes(int c, double px, double pz, double qx, double qz)
        {
            for (int j = 0; j < legs.Length; j++)
            {
                if (j == c) continue;
                double keep = SpacingOf(c, j);
                var list = legs[j];
                double ax, az, bx, bz;
                if (list.Count == 0) { ax = bx = homeX[j]; az = bz = homeZ[j]; }
                else
                {
                    var l = list[list.Count - 1];
                    ax = l.FromX; az = l.FromZ; bx = l.ToX; bz = l.ToZ;
                }
                if (SegmentDistance(px, pz, qx, qz, ax, az, bx, bz) < keep) return true;
            }
            return false;
        }

        double Peak => 1.0 / (1.0 - (Clamp01(rules.SpeedUp) + Clamp01(rules.SlowDown)) * .5);

        public double ChargeSeconds(double distance, double topSpeed) => distance * Peak / Math.Max(.1, topSpeed);

        // Seconds to turn from one heading to another: a half turn takes Turn, a smaller one its share.
        public double TurnSeconds(double from, double to) =>
            Math.Max(Math.Min(rules.MinTurn, rules.Turn), rules.Turn * Math.Abs(Delta(from, to)) / 180.0);

        // The same for body c, with its own half turn.
        public double TurnSeconds(int c, double from, double to) =>
            Math.Max(Math.Min(rules.MinTurn, turn[c]), turn[c] * Math.Abs(Delta(from, to)) / 180.0);

        // True when `time` is more than Leap past what is planned, or before it.
        bool Leapt(double time)
        {
            double first = double.MaxValue, next = double.MaxValue;
            for (int c = 0; c < legs.Length; c++)
            {
                first = Math.Min(first, legs[c].Count == 0 ? start : legs[c][0].Start);
                next = Math.Min(next, NextStart(c));
            }
            return time > next + Leap || time < first - Leap;
        }

        // Carriage c at `time` (the shared obstacle clock). Plans what it needs first.
        public Pose At(int c, double time)
        {
            if (Held)
                return new Pose { X = homeX[c], Z = homeZ[c], Yaw = homeYaw[c], Stage = Stage.Waiting, LegStart = double.NaN, ToX = homeX[c], ToZ = homeZ[c], StageStart = double.NaN, StageEnd = double.NaN };
            if (Leapt(time))
            {
                start = time;
                foreach (var planned in legs) planned.Clear();
                rng = new SeededRandom(seed);
            }
            Advance(time);
            var list = legs[c];
            if (list.Count == 0 || time < list[0].Start)
            {
                if (list.Count == 0)
                    return new Pose { X = homeX[c], Z = homeZ[c], Yaw = homeYaw[c], Stage = Stage.Waiting, LegStart = double.NaN, ToX = homeX[c], ToZ = homeZ[c], StageStart = double.NaN, StageEnd = double.NaN };
                var first = list[0];
                return new Pose { X = first.FromX, Z = first.FromZ, Yaw = first.HeadingFrom, Stage = Stage.Waiting, LegStart = double.NaN, ToX = first.FromX, ToZ = first.FromZ, StageStart = double.NaN, StageEnd = double.NaN };
            }
            int i = list.Count - 1;
            while (i > 0 && list[i].Start > time) i--;
            var leg = list[i];
            double u = time - leg.Start;
            var pose = new Pose { X = leg.FromX, Z = leg.FromZ, Yaw = leg.Heading, LegStart = leg.Start, ToX = leg.ToX, ToZ = leg.ToZ,
                                  StageStart = leg.Start, StageEnd = leg.End };
            if (leg.Charge <= 0 && leg.Turn <= 0)
            {
                pose.Yaw = leg.Heading;
                pose.Stage = Stage.Waiting;
                return pose;
            }
            if (u < leg.Turn)
            {
                pose.Stage = Stage.Turning;
                pose.StageEnd = leg.Start + leg.Turn;
                pose.Yaw = leg.HeadingFrom + Delta(leg.HeadingFrom, leg.Heading) * Smooth(u / leg.Turn);
                return pose;
            }
            u -= leg.Turn;
            if (u < leg.Aim)
            {
                pose.Stage = Stage.Aiming;
                pose.StageStart = leg.Start + leg.Turn;
                pose.StageEnd = leg.ChargeStart;
                return pose;
            }
            u -= leg.Aim;
            if (u < leg.Charge)
            {
                double f, v;
                Profile(u / leg.Charge, out f, out v);
                double dx = leg.ToX - leg.FromX, dz = leg.ToZ - leg.FromZ;
                pose.Stage = Stage.Charging;
                pose.StageStart = leg.ChargeStart;
                pose.StageEnd = leg.ChargeEnd;
                pose.X = leg.FromX + dx * f;
                pose.Z = leg.FromZ + dz * f;
                pose.Speed = v * Math.Sqrt(dx * dx + dz * dz) / leg.Charge;
                return pose;
            }
            pose.Stage = Stage.Resting;
            pose.StageStart = leg.ChargeEnd;
            pose.X = leg.ToX;
            pose.Z = leg.ToZ;
            return pose;
        }

        // Distance fraction and speed ("distances per charge") for a speed that rises over SpeedUp of the time,
        // holds, and falls over SlowDown.
        void Profile(double u, out double f, out double v)
        {
            u = Clamp01(u);
            double up = Math.Max(1e-6, Clamp01(rules.SpeedUp)), down = Math.Max(1e-6, Clamp01(rules.SlowDown)), peak = Peak;
            if (u < up)
            {
                f = peak * u * u / (2 * up);
                v = peak * u / up;
            }
            else if (u < 1 - down)
            {
                f = peak * (up * .5 + (u - up));
                v = peak;
            }
            else
            {
                double left = 1 - u;
                f = 1 - peak * left * left / (2 * down);
                v = peak * left / down;
            }
        }

        // The shortest distance between segments ab and cd (points allowed: a = b).
        public static double SegmentDistance(double ax, double az, double bx, double bz, double cx, double cz, double dx, double dz)
        {
            if (Cross(ax, az, bx, bz, cx, cz) * Cross(ax, az, bx, bz, dx, dz) < 0 && Cross(cx, cz, dx, dz, ax, az) * Cross(cx, cz, dx, dz, bx, bz) < 0)
                return 0;
            return Math.Min(Math.Min(PointDistance(ax, az, cx, cz, dx, dz), PointDistance(bx, bz, cx, cz, dx, dz)),
                            Math.Min(PointDistance(cx, cz, ax, az, bx, bz), PointDistance(dx, dz, ax, az, bx, bz)));
        }

        static double Cross(double ax, double az, double bx, double bz, double px, double pz) => (bx - ax) * (pz - az) - (bz - az) * (px - ax);

        static double PointDistance(double px, double pz, double ax, double az, double bx, double bz)
        {
            double vx = bx - ax, vz = bz - az, len = vx * vx + vz * vz;
            double t = len < 1e-12 ? 0 : Clamp01(((px - ax) * vx + (pz - az) * vz) / len);
            double x = ax + vx * t - px, z = az + vz * t - pz;
            return Math.Sqrt(x * x + z * z);
        }

        // The short way round from a to b, degrees (-180, 180].
        public static double Delta(double a, double b)
        {
            double d = (b - a) % 360.0;
            if (d > 180) d -= 360;
            if (d <= -180) d += 360;
            return d;
        }

        static double Smooth(double u)
        {
            u = Clamp01(u);
            return u * u * (3 - 2 * u);
        }

        static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
