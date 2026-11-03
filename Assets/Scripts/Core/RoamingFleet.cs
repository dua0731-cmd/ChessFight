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

        public sealed class Rules
        {
            // Where the carriages' centres may go (the yard's frame, metres).
            public double MinX = -19.4, MaxX = 19.4, MinZ = 7.1, MaxZ = 47.4;
            public double BodyRadius = 2.4;      // a body's reach from its centre, whichever way it faces
            public double Clearance = .6;        // kept between two bodies on top of that
            public double Turn = 1.2;            // turning on the spot to face the next point
            public double MinAim = .6, MaxAim = 1.2;   // standing with the lamps red before the charge
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
        }

        readonly Rules rules;
        readonly uint seed;
        readonly double[] speed, homeX, homeZ, homeYaw;
        readonly List<Leg>[] legs;
        double start;
        SeededRandom rng;

        // `speeds`: each carriage's top speed (m/s); `homeX`/`homeZ`/`homeYaw`: where each stands until its first leg,
        // which must be inside the area and keep 2 BodyRadius + Clearance apart.
        public RoamingFleet(uint seed, double start, double[] speeds, double[] homeX, double[] homeZ, double[] homeYaw, Rules rules = null)
        {
            this.rules = rules ?? new Rules();
            this.seed = seed;
            this.start = start;
            speed = (double[])speeds.Clone();
            this.homeX = (double[])homeX.Clone();
            this.homeZ = (double[])homeZ.Clone();
            this.homeYaw = (double[])homeYaw.Clone();
            legs = new List<Leg>[speed.Length];
            for (int i = 0; i < legs.Length; i++) legs[i] = new List<Leg>();
            rng = new SeededRandom(seed);
        }

        public int Count => legs.Length;
        public Rules Settings => rules;
        public double StartTime => start;
        public int LegCount(int carriage) => legs[carriage].Count;
        public Leg LegAt(int carriage, int i) => legs[carriage][i];

        // How close two carriages' centres may ever come.
        public double Spacing => 2 * rules.BodyRadius + rules.Clearance;

        double NextStart(int c) => legs[c].Count == 0 ? start + c * rules.Stagger : legs[c][legs[c].Count - 1].End;

        // Plans every leg that starts at or before `until`, in the order the legs start (the lower carriage first on
        // a tie), so the legs are the same however the time is advanced.
        public void Advance(double until)
        {
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
            for (int k = 0; k < Math.Max(1, rules.Tries); k++)
            {
                double qx = rng.Range(rules.MinX, rules.MaxX), qz = rng.Range(rules.MinZ, rules.MaxZ);
                double d = Math.Sqrt((qx - px) * (qx - px) + (qz - pz) * (qz - pz));
                if (d < rules.MinCharge || d > rules.MaxCharge || Clashes(c, px, pz, qx, qz)) continue;
                double heading = Math.Atan2(qx - px, qz - pz) * 180.0 / Math.PI;
                legs[c].Add(new Leg
                {
                    Start = t0, FromX = px, FromZ = pz, ToX = qx, ToZ = qz, HeadingFrom = yaw, Heading = heading,
                    Turn = rules.Turn, Aim = rng.Range(Math.Min(rules.MinAim, rules.MaxAim), Math.Max(rules.MinAim, rules.MaxAim)),
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
            double keep = Spacing;
            for (int j = 0; j < legs.Length; j++)
            {
                if (j == c) continue;
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
                if (list.Count == 0) return new Pose { X = homeX[c], Z = homeZ[c], Yaw = homeYaw[c], Stage = Stage.Waiting };
                var first = list[0];
                return new Pose { X = first.FromX, Z = first.FromZ, Yaw = first.HeadingFrom, Stage = Stage.Waiting };
            }
            int i = list.Count - 1;
            while (i > 0 && list[i].Start > time) i--;
            var leg = list[i];
            double u = time - leg.Start;
            var pose = new Pose { X = leg.FromX, Z = leg.FromZ, Yaw = leg.Heading };
            if (leg.Charge <= 0 && leg.Turn <= 0)
            {
                pose.Yaw = leg.Heading;
                pose.Stage = Stage.Waiting;
                return pose;
            }
            if (u < leg.Turn)
            {
                pose.Stage = Stage.Turning;
                pose.Yaw = leg.HeadingFrom + Delta(leg.HeadingFrom, leg.Heading) * Smooth(u / leg.Turn);
                return pose;
            }
            u -= leg.Turn;
            if (u < leg.Aim)
            {
                pose.Stage = Stage.Aiming;
                return pose;
            }
            u -= leg.Aim;
            if (u < leg.Charge)
            {
                double f, v;
                Profile(u / leg.Charge, out f, out v);
                double dx = leg.ToX - leg.FromX, dz = leg.ToZ - leg.FromZ;
                pose.Stage = Stage.Charging;
                pose.X = leg.FromX + dx * f;
                pose.Z = leg.FromZ + dz * f;
                pose.Speed = v * Math.Sqrt(dx * dx + dz * dz) / leg.Charge;
                return pose;
            }
            pose.Stage = Stage.Resting;
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
