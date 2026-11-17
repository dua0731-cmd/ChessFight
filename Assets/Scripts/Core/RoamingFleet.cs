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
    //
    // R686 (승규 11-17 "말 도는 것을 진짜 자연스럽게" ... "더 나아가" - a team turning like a coach, round a circle): a body
    // with a pivot (a team: its coach's rear axle, Pivot behind its centre) drives each turn round a point on its rear
    // axle's line, Circles[k] to the inside of the turn - its rear wheels roll round that circle, its pair walk forward
    // ahead of them - widest first, down to the rear axle itself and, where no swing keeps inside the yard, on the spot.
    // Its centre swings round that point through the turn and its charge starts where the turn leaves it, straight at the
    // drawn point. The planner keeps the whole swing in the area and apart from the others in time; a body of pivot 0
    // turns on the spot exactly as before.
    public sealed class RoamingFleet
    {
        public const double Leap = 3600;

        public enum Stage { Waiting, Turning, Aiming, Charging, Resting }

        // The Royal Carriage Yard's seeds (R287, vetted again for R659's horse-drawn carriages and for R686's teams driving
        // their turns round circles - 9 of seeds 1-512 passed, these the 8 with the most room): a match draws one by
        // its round seed (YardSeed), so each round has its own dance but never a poor one. The Core tests check each on
        // the yard's own settings over ten minutes: someone charging at least 85 % of the time, a charging body over the
        // straight way up the middle (|x| <= 3) at least 50 % of it, waits under 4 % of the legs, after its first charge
        // no body standing more than 6 s at a time (a team's rest, half turn and aim), every body reaching every edge of
        // its area, never two inside their spacing, the same dance on scene time and on the match clock, and in the first
        // 20 s the charges near the gate split at least 40 / 60 between its west and east halves (neither team's side of
        // the start is favoured). The tests also pin each seed's first legs: a change to the yard's numbers fails them
        // until the list is vetted again (R287's list: 38, 41, 160, 44, 18, 252, 20, 113).
        public static readonly uint[] YardSeeds = { 491, 328, 460, 239, 94, 455, 66, 23 };

        public static uint YardSeed(int roundSeed) => YardSeeds[SeededSchedule.Hash(roundSeed, 0xCA27) % (uint)YardSeeds.Length];

        // The yard the seeds were vetted on, as RoyalCarriageYard and CarriageFleet build it (R659): between the storage
        // sheds (their fronts at x +-YardShedFront) from z 2 to 50, YardTeams teams - a carriage drawn by a pair of
        // horses, 6.95 x 2.3 m (the user: "말+마차", never a horse or a carriage on its own) - at 9.3 + 0.3 i m/s turning
        // a half turn in 3.4 s (YardHorses loose horses after them, 2.6 x 0.9 m at 11 + 0.3 i m/s, none in the yard); the
        // centres 0.8 m past the biggest body's reach inside the sheds and the parapet and 2.5 m more off the gate, homes
        // two to a row facing the middle (an odd one out in the middle), kept apart in time (Rules.Timed), 128 tries, a
        // 4 m dart when no full charge is free, charges of 10 to 30 m and rests of 0.3 to 0.9 s. The tests vet the seeds on it; the map warns when its
        // own fleet does not match it (Matches), as the seeds may then dance worse. (R287-R532: four carriages between
        // the walls at +-22, apart over whole legs.)
        public const int YardTeams = 5, YardHorses = 0;
        public const double YardShedFront = 19;

        public static RoamingFleet Yard(uint seed, double start)
        {
            double team = .5 * Math.Sqrt(6.95 * 6.95 + 2.3 * 2.3), horse = .5 * Math.Sqrt(2.6 * 2.6 + .9 * .9), keep = team + .8;
            var rules = new Rules
            {
                MinX = -(YardShedFront - keep), MaxX = YardShedFront - keep, MinZ = 2 + keep + 2.5, MaxZ = 50 - keep, BodyRadius = team,
                Timed = true, Tries = 128, ShortCharge = 4, MinRest = .3, MaxRest = .9, MaxCharge = 30,
            };
            int n = YardTeams + YardHorses, rows = (n + 1) / 2;
            var speeds = new double[n];
            var hx = new double[n];
            var hz = new double[n];
            var yaw = new double[n];
            var radii = new double[n];
            var turns = new double[n];
            var pivots = new double[n];
            for (int i = 0; i < n; i++)
            {
                bool loose = i >= YardTeams;
                pivots[i] = loose ? 0 : TeamPivot;
                speeds[i] = loose ? 11 + .3 * (i - YardTeams) : 9.3 + .3 * i;
                radii[i] = loose ? horse : team;
                turns[i] = loose ? 1.6 : 3.4;
                bool alone = n % 2 == 1 && i == n - 1;
                double side = alone ? 0 : (i % 2 == 0 ? -1 : 1);
                hx[i] = (rules.MinX + rules.MaxX) * .5 + side * (rules.MaxX - rules.MinX) * .25;
                hz[i] = rules.MinZ + (i / 2 + .5) * (rules.MaxZ - rules.MinZ) / rows;
                yaw[i] = side < 0 ? 90 : side > 0 ? -90 : 0;
            }
            return new RoamingFleet(seed, start, speeds, hx, hz, yaw, rules, radii, turns, pivots);
        }

        // R686: a team turns about its coach's rear axle, this far behind the team's middle (6.95 m long: the coach's back
        // 3.475 m behind it, the rear axle 0.8 m in from there).
        public const double TeamPivot = 2.675;

        // The same carriages under the same rules (within `tolerance`, the Unity side works in floats): everything a
        // seed's dance depends on but the seed and the start.
        public bool Matches(RoamingFleet other, double tolerance = 1e-3)
        {
            if (other == null || other.Count != Count) return false;
            var a = rules;
            var b = other.rules;
            double[] mine = { a.MinX, a.MaxX, a.MinZ, a.MaxZ, a.BodyRadius, a.Clearance, a.Turn, a.MinTurn, a.MinAim, a.MaxAim, a.MinLead,
                              a.MinRest, a.MaxRest, a.Wait, a.SpeedUp, a.SlowDown, a.MinCharge, a.MaxCharge, a.Stagger, a.Tries,
                              a.Timed ? 1 : 0, a.TimedStep, a.ShortCharge };
            double[] theirs = { b.MinX, b.MaxX, b.MinZ, b.MaxZ, b.BodyRadius, b.Clearance, b.Turn, b.MinTurn, b.MinAim, b.MaxAim, b.MinLead,
                                b.MinRest, b.MaxRest, b.Wait, b.SpeedUp, b.SlowDown, b.MinCharge, b.MaxCharge, b.Stagger, b.Tries,
                                b.Timed ? 1 : 0, b.TimedStep, b.ShortCharge };
            for (int i = 0; i < mine.Length; i++)
                if (Math.Abs(mine[i] - theirs[i]) > tolerance) return false;
            for (int c = 0; c < Count; c++)
                if (Math.Abs(speed[c] - other.speed[c]) > tolerance || Math.Abs(homeX[c] - other.homeX[c]) > tolerance
                    || Math.Abs(homeZ[c] - other.homeZ[c]) > tolerance || Math.Abs(Delta(homeYaw[c], other.homeYaw[c])) > tolerance
                    || Math.Abs(radius[c] - other.radius[c]) > tolerance || Math.Abs(turn[c] - other.turn[c]) > tolerance
                    || Math.Abs(pivot[c] - other.pivot[c]) > tolerance)
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
            // R659: keep bodies apart in time, not over whole legs. Off (the R287 yard), a new charge's whole sweep keeps
            // off every other body's whole current leg, whenever either happens - safe but, with long teams and more
            // bodies, mostly waiting. On, two bodies only clash where they would be too close AT THE SAME TIME: each
            // body is at its leg's start until its charge starts, moves along its line while it charges, and stays at
            // its end until its next leg (planned later, against everyone's latest legs in turn). Never closer than
            // the two reaches + Clearance at any moment either way.
            public bool Timed;
            // The time steps a timed check walks two moving bodies in (s): each step judged by the short stretches both
            // can be on during it, so it errs only on the safe side.
            public double TimedStep = .2;
            // R659: nowhere free for a charge of MinCharge or more, a shorter one down to this (m) is tried before
            // standing to wait (0: never) - in a crowded yard a body darts a few metres instead of standing idle.
            public double ShortCharge;
        }

        public struct Leg
        {
            public double Start;                 // the turn starts (or the wait)
            public double TurnX, TurnZ;          // R686: where the centre stands as the turn starts (the last leg's end)
            public double Pivot;                 // R686: how far behind the centre this turn's turning point is
            public double Side;                  // R686: and how far to the centre's right (signed); both 0: on the spot
            public double FromX, FromZ, ToX, ToZ; // the charge's start (where the turn leaves the centre) and end
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
            // When the stage it is in began and ends (R659: a loose horse rears through its aim); NaN when it waits.
            public double StageStart, StageEnd;
            // R686: the leg's turn, from and to (degrees; a wait or home: the way it faces), for whoever draws it.
            public double HeadingFrom, Heading;
            // R686: while turning, where its turning point is: Pivot behind its centre, Side to its right (both 0: on
            // the spot). The point stays put through the turn; the body swings round it.
            public double Pivot, Side;
        }

        readonly Rules rules;
        readonly uint seed;
        readonly double[] speed, homeX, homeZ, homeYaw, radius, turn, pivot;
        readonly List<Leg>[] legs;
        double start;
        SeededRandom rng;

        // `speeds`: each body's top speed (m/s); `homeX`/`homeZ`/`homeYaw`: where each stands until its first leg,
        // which must be inside its area and keep the two bodies' reaches + Clearance apart.
        // `radii` and `turns` (R659, optional): each body's own reach from its centre and its own half turn (s), for a
        // fleet of different bodies - horse-drawn carriages and loose horses in one yard. Without them every body is
        // Rules.BodyRadius and Rules.Turn. The area (MinX..MaxZ) is where a body of Rules.BodyRadius may put its centre;
        // a smaller body may come that much nearer the walls (its own reach still keeps the same margin off them).
        // `pivots` (R686, optional): how far behind its centre each body turns about (0, or none: on the spot).
        public RoamingFleet(uint seed, double start, double[] speeds, double[] homeX, double[] homeZ, double[] homeYaw, Rules rules = null,
                            double[] radii = null, double[] turns = null, double[] pivots = null)
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
            pivot = new double[speed.Length];
            for (int i = 0; i < speed.Length; i++)
            {
                pivot[i] = pivots != null && i < pivots.Length && pivots[i] > 0 ? pivots[i] : 0;
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
        // R686: how far behind its centre it turns about (0: on the spot).
        public double Pivot(int body) => pivot[body];

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
            double back = pivot[c];
            if (back > 0)
            {
                // R686: a body with a pivot (a team: its coach's rear axle) drives its turn round a circle, its pivot
                // swinging round a point Circles[k] to the inside - a full charge on the widest circle that is free (the
                // widest tried twice as often: near a wall only some headings keep its swing inside), then turning about
                // the rear axle itself, then on the spot (a team's pair side-step it round), then a short dart on a
                // middling circle or on the spot, before it waits.
                if (Full(c, t0, px, pz, yaw, back, Circles[0]) || Full(c, t0, px, pz, yaw, back, Circles[0])) return;
                for (int k = 1; k < Circles.Length; k++)
                    if (Full(c, t0, px, pz, yaw, back, Circles[k])) return;
                if (Full(c, t0, px, pz, yaw, 0, 0) || Dart(c, t0, px, pz, yaw, back, Circles[1]) || Dart(c, t0, px, pz, yaw, 0, 0)) return;
            }
            // A body turning on the spot tries as it always did: a full charge, then a dart.
            else if (Full(c, t0, px, pz, yaw, 0, 0) || Dart(c, t0, px, pz, yaw, 0, 0)) return;
            // Nowhere free: stand where it is and try again in a moment.
            legs[c].Add(new Leg
            {
                Start = t0, TurnX = px, TurnZ = pz, FromX = px, FromZ = pz, ToX = px, ToZ = pz, HeadingFrom = yaw, Heading = yaw,
                Rest = Math.Max(.05, rules.Wait), Speed = speed[c],
            });
        }

        // R686: how far to the inside of the turn a pivoting body's turning point is, widest first (m): a circle of that
        // radius under its rear axle - at 4 m a team's pair walk mostly forward round it - down to the rear axle itself.
        public static readonly double[] Circles = { 4.0, 2.0, 0.0 };

        // Body c's next full charge from (px, pz) facing `yaw`, if one is free: to a random point in its area, turning
        // about the point `back` behind its centre and `side` to the inside of the turn (both 0: on the spot).
        bool Full(int c, double t0, double px, double pz, double yaw, double back, double side)
        {
            Area(c, out double minX, out double maxX, out double minZ, out double maxZ);
            for (int k = 0; k < Math.Max(1, rules.Tries); k++)
            {
                double qx = rng.Range(minX, maxX), qz = rng.Range(minZ, maxZ);
                double d = Math.Sqrt((qx - px) * (qx - px) + (qz - pz) * (qz - pz));
                if (rules.Timed)
                {
                    // Timed (R659): the leg's own times first - its aim drawn per try - then the check in time.
                    if (d < rules.MinCharge || d > rules.MaxCharge) continue;
                    double head = Facing(back, side, px, pz, yaw, qx, qz, out double s), spin = TurnSeconds(c, yaw, head);
                    double aim = Math.Max(rng.Range(Math.Min(rules.MinAim, rules.MaxAim), Math.Max(rules.MinAim, rules.MaxAim)), rules.MinLead - spin);
                    if (double.IsNaN(head)) continue;
                    var leg = NewLeg(c, t0, px, pz, qx, qz, yaw, head, spin, aim, d, back, s);
                    if (!Inside(c, leg, rules.MinCharge) || ClashesTimed(c, t0, leg)) continue;
                    leg.Rest = rng.Range(Math.Min(rules.MinRest, rules.MaxRest), Math.Max(rules.MinRest, rules.MaxRest));
                    legs[c].Add(leg);
                    return true;
                }
                if (d < rules.MinCharge || d > rules.MaxCharge) continue;
                double heading = Facing(back, side, px, pz, yaw, qx, qz, out double sw), turn = TurnSeconds(c, yaw, heading);
                if (double.IsNaN(heading)) continue;
                var whole = NewLeg(c, t0, px, pz, qx, qz, yaw, heading, turn, 0, d, back, sw);
                if (!Inside(c, whole, rules.MinCharge) || Clashes(c, whole)) continue;
                AddLeg(c, whole);
                return true;
            }
            return false;
        }

        // R659 (timed): nowhere free for a full charge - a short dart, ShortCharge up to MinCharge long, if one is free.
        bool Dart(int c, double t0, double px, double pz, double yaw, double back, double side)
        {
            Area(c, out double minX, out double maxX, out double minZ, out double maxZ);
            if (rules.Timed && rules.ShortCharge > 0 && rules.ShortCharge < rules.MinCharge)
                for (int k = 0; k < Math.Max(1, rules.Tries); k++)
                {
                    double angle = rng.Range(0, 2 * Math.PI), d = rng.Range(rules.ShortCharge, rules.MinCharge);
                    double qx = px + Math.Sin(angle) * d, qz = pz + Math.Cos(angle) * d;
                    if (qx < minX || qx > maxX || qz < minZ || qz > maxZ) continue;
                    double head = Facing(back, side, px, pz, yaw, qx, qz, out double s), spin = TurnSeconds(c, yaw, head);
                    double aim = Math.Max(rng.Range(Math.Min(rules.MinAim, rules.MaxAim), Math.Max(rules.MinAim, rules.MaxAim)), rules.MinLead - spin);
                    if (double.IsNaN(head)) continue;
                    var leg = NewLeg(c, t0, px, pz, qx, qz, yaw, head, spin, aim, d, back, s);
                    if (!Inside(c, leg, rules.ShortCharge) || ClashesTimed(c, t0, leg)) continue;
                    leg.Rest = rng.Range(Math.Min(rules.MinRest, rules.MaxRest), Math.Max(rules.MinRest, rules.MaxRest));
                    legs[c].Add(leg);
                    return true;
                }
            return false;
        }

        // A leg from (px, pz) facing `yaw` to the drawn point q, `d` away: turned to `head` (Facing), it charges from where
        // the turn leaves its centre (R686: round its turning point, `back` behind and `side` to its right - signed),
        // straight along `head` to q (to within a hair: the heading is solved to a hundredth of a millimetre). A body
        // turning on the spot charges from p to q itself.
        Leg NewLeg(int c, double t0, double px, double pz, double qx, double qz, double yaw, double head, double spin, double aim, double d,
                   double back, double side)
        {
            var leg = new Leg
            {
                Start = t0, TurnX = px, TurnZ = pz, FromX = px, FromZ = pz, ToX = qx, ToZ = qz, HeadingFrom = yaw, Heading = head,
                Turn = spin, Aim = aim, Speed = speed[c], Pivot = back, Side = side,
            };
            if (Swings(leg))
            {
                Arc(leg, head, out double fx, out double fz);
                double h = head * Math.PI / 180.0;
                d = Math.Sqrt((qx - fx) * (qx - fx) + (qz - fz) * (qz - fz));
                leg.FromX = fx;
                leg.FromZ = fz;
                leg.ToX = fx + Math.Sin(h) * d;
                leg.ToZ = fz + Math.Cos(h) * d;
            }
            leg.Charge = ChargeSeconds(d, speed[c]);
            return leg;
        }

        // The heading body c turns to, from (px, pz) facing `yaw`, to charge at q: straight at q from where it stands; R686:
        // turning round a point (`back` behind it, `side` to the inside of the turn) moves its centre with the heading, so
        // the heading at q from where the turn will leave it is solved (rounds of aiming from where the last aim left it,
        // to a hundredth of a millimetre). `signedSide`: the turning point's side, + to the right. NaN: no such heading
        // (it would not settle, or it would turn the other way than the side it was solved for).
        double Facing(double back, double side, double px, double pz, double yaw, double qx, double qz, out double signedSide)
        {
            signedSide = 0;
            double head = Math.Atan2(qx - px, qz - pz) * 180.0 / Math.PI;
            if (back <= 0 && side <= 0) return head;
            double sign = Delta(yaw, head) >= 0 ? 1 : -1;
            signedSide = sign * side;
            var leg = new Leg { TurnX = px, TurnZ = pz, HeadingFrom = yaw, Pivot = back, Side = signedSide };
            for (int k = 0; k < 24; k++)
            {
                Arc(leg, head, out double fx, out double fz);
                double next = Math.Atan2(qx - fx, qz - fz) * 180.0 / Math.PI;
                bool done = Math.Abs(Delta(head, next)) < 1e-7;
                head = next;
                if (done) return side > 0 && Delta(yaw, head) * sign < 0 ? double.NaN : head;
            }
            return double.NaN;
        }

        // The R287 planner's leg (aim and rest drawn after the point, as it always did: the vetted dances stay the same).
        void AddLeg(int c, Leg leg)
        {
            // A small turn aims longer, so every charge is shown (its line on the floor) MinLead ahead.
            leg.Aim = Math.Max(rng.Range(Math.Min(rules.MinAim, rules.MaxAim), Math.Max(rules.MinAim, rules.MaxAim)), rules.MinLead - leg.Turn);
            leg.Rest = rng.Range(Math.Min(rules.MinRest, rules.MaxRest), Math.Max(rules.MinRest, rules.MaxRest));
            legs[c].Add(leg);
        }

        // R686: does the leg's turn move its centre (round a turning point, not on the spot)?
        static bool Swings(in Leg leg) => leg.Pivot > 0 || leg.Side != 0;

        // R686: the leg's turning point (the start's frame: `Pivot` behind the centre, `Side` to its right).
        static void TurningPoint(in Leg leg, out double x, out double z)
        {
            Rotate(leg.Side, -leg.Pivot, leg.HeadingFrom, out double ox, out double oz);
            x = leg.TurnX + ox;
            z = leg.TurnZ + oz;
        }

        // A vector (x right, z ahead) of a body facing `yaw` degrees, in the yard's frame.
        static void Rotate(double x, double z, double yaw, out double wx, out double wz)
        {
            double a = yaw * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
            wx = x * c + z * s;
            wz = -x * s + z * c;
        }

        // R686: a leg whose turn swings the centre keeps it in its area all the way round and where the charge starts and
        // ends, and its charge (from where the turn leaves it) at least `shortest` and at most MaxCharge. The arc is checked
        // where it goes furthest each way: its ends and every point on it due north, east, south or west of the turning
        // point.
        bool Inside(int c, in Leg leg, double shortest)
        {
            if (!Swings(leg)) return true;
            double length = Math.Sqrt((leg.ToX - leg.FromX) * (leg.ToX - leg.FromX) + (leg.ToZ - leg.FromZ) * (leg.ToZ - leg.FromZ));
            if (length < shortest || length > rules.MaxCharge) return false;
            Area(c, out double minX, out double maxX, out double minZ, out double maxZ);
            bool In(double x, double z) => x >= minX - 1e-9 && x <= maxX + 1e-9 && z >= minZ - 1e-9 && z <= maxZ + 1e-9;
            if (!In(leg.FromX, leg.FromZ) || !In(leg.ToX, leg.ToZ)) return false;
            TurningPoint(leg, out double ox, out double oz);
            double vx = leg.TurnX - ox, vz = leg.TurnZ - oz, r = Math.Sqrt(vx * vx + vz * vz);
            double turn = Delta(leg.HeadingFrom, leg.Heading), a0 = Math.Atan2(vx, vz) * 180.0 / Math.PI, a1 = a0 + turn;
            double lo = Math.Min(a0, a1), hi = Math.Max(a0, a1);
            for (double q = Math.Ceiling(lo / 90.0) * 90.0; q < hi; q += 90.0)
            {
                double h = q * Math.PI / 180.0;
                if (!In(ox + r * Math.Sin(h), oz + r * Math.Cos(h))) return false;
            }
            return true;
        }

        // Where a body's centre is on its leg's turn (round the leg's turning point) when it faces `yaw`.
        static void Arc(in Leg leg, double yaw, out double x, out double z)
        {
            TurningPoint(leg, out double ox, out double oz);
            Rotate(leg.TurnX - ox, leg.TurnZ - oz, yaw - leg.HeadingFrom, out double vx, out double vz);
            x = ox + vx;
            z = oz + vz;
        }

        // How far a leg's arc bows out of its chord between two headings (its radius: the turning point's distance).
        static double Bulge(in Leg leg, double a, double b)
        {
            if (!Swings(leg)) return 0;
            double r = Math.Sqrt(leg.Pivot * leg.Pivot + leg.Side * leg.Side);
            return r * (1 - Math.Cos(Math.Abs(Delta(a, b)) * Math.PI / 360.0));
        }

        // Body j standing where it is from t0 (home, before its first leg).
        Leg Standing(int j, double t0) =>
            new Leg { Start = t0, TurnX = homeX[j], TurnZ = homeZ[j], FromX = homeX[j], FromZ = homeZ[j], ToX = homeX[j], ToZ = homeZ[j],
                      HeadingFrom = homeYaw[j], Heading = homeYaw[j] };

        // Would carriage c's sweep - the turn's swing (R686: its chord, wider by how far the arc bows) and the line from
        // the charge's start to its end - come too close to any other carriage's current leg?
        bool Clashes(int c, in Leg mine)
        {
            for (int j = 0; j < legs.Length; j++)
            {
                if (j == c) continue;
                var list = legs[j];
                var l = list.Count == 0 ? Standing(j, mine.Start) : list[list.Count - 1];
                double keep = SpacingOf(c, j) + Bulge(mine, mine.HeadingFrom, mine.Heading) + Bulge(l, l.HeadingFrom, l.Heading);
                if (SegmentDistance(mine.FromX, mine.FromZ, mine.ToX, mine.ToZ, l.FromX, l.FromZ, l.ToX, l.ToZ) < keep) return true;
                if (Swings(mine) && SegmentDistance(mine.TurnX, mine.TurnZ, mine.FromX, mine.FromZ, l.FromX, l.FromZ, l.ToX, l.ToZ) < keep) return true;
                if (Swings(l) && SegmentDistance(mine.FromX, mine.FromZ, mine.ToX, mine.ToZ, l.TurnX, l.TurnZ, l.FromX, l.FromZ) < keep) return true;
                if (Swings(mine) && Swings(l)
                    && SegmentDistance(mine.TurnX, mine.TurnZ, mine.FromX, mine.FromZ, l.TurnX, l.TurnZ, l.FromX, l.FromZ) < keep) return true;
            }
            return false;
        }

        // Timed (R659): would body c on `mine` - where its turn starts until the turn, swinging round its turning point
        // through the turn (R686; on the spot, no move), at the charge's start until the charge, charging to its end, there
        // after - come within the two reaches + Clearance of any other body AT THE SAME TIME, from t0 on? Each other body
        // follows its latest leg the same way and stays at its end after it (its next leg, planned later, is checked
        // against this one). Time is cut where either body starts or stops moving; while both move, or either swings
        // round a turning point, it is walked in steps of TimedStep, each step judged by the two stretches the bodies can
        // be on during it - an arc's chord, kept wider by how far the arc bows out of it - so a miss is never wrongly
        // allowed.
        readonly double[] cuts = new double[8];

        bool ClashesTimed(int c, double t0, in Leg mine)
        {
            double from = mine.ChargeStart, to = mine.ChargeEnd, turned = mine.Start + mine.Turn;
            bool swings = Swings(mine) && mine.Turn > 0;
            for (int j = 0; j < legs.Length; j++)
            {
                if (j == c) continue;
                double keep = SpacingOf(c, j);
                var list = legs[j];
                var m = list.Count == 0 ? Standing(j, t0) : list[list.Count - 1];
                bool theySwing = Swings(m) && m.Turn > 0;
                double theyTurned = m.Start + m.Turn;
                int n = 0;
                cuts[n++] = t0;
                if (swings && turned > t0) cuts[n++] = turned;
                if (from > t0) cuts[n++] = from;
                if (to > t0) cuts[n++] = to;
                if (theySwing && theyTurned > t0) cuts[n++] = theyTurned;
                if (m.ChargeStart > t0) cuts[n++] = m.ChargeStart;
                if (m.ChargeEnd > t0) cuts[n++] = m.ChargeEnd;
                Array.Sort(cuts, 0, n);
                for (int i = 0; i < n; i++)
                {
                    double a = cuts[i], b = i + 1 < n ? cuts[i + 1] : double.PositiveInfinity;
                    if (b - a < 1e-9) continue;
                    bool myArc = swings && a < turned && b > mine.Start, theirArc = theySwing && a < theyTurned && b > m.Start;
                    bool myMove = (a < to && b > from) || myArc;
                    bool theirMove = (m.Charge > 0 && a < m.ChargeEnd && b > m.ChargeStart) || theirArc;
                    if (double.IsPositiveInfinity(b))
                    {
                        // Both at rest for good: c at its charge's end, the other at its leg's end.
                        Where(mine, a, out double cx, out double cz, out _);
                        Where(m, a, out double ox, out double oz, out _);
                        if (Math.Sqrt((cx - ox) * (cx - ox) + (cz - oz) * (cz - oz)) < keep) return true;
                        continue;
                    }
                    double step = (myMove && theirMove) || myArc || theirArc ? Math.Max(.02, rules.TimedStep) : b - a;
                    for (double s = a; s < b - 1e-9; s += step)
                    {
                        double e = Math.Min(b, s + step);
                        Where(mine, s, out double c0x, out double c0z, out double c0y);
                        Where(mine, e, out double c1x, out double c1z, out double c1y);
                        Where(m, s, out double o0x, out double o0z, out double o0y);
                        Where(m, e, out double o1x, out double o1z, out double o1y);
                        double slack = Bulge(mine, c0y, c1y) + Bulge(m, o0y, o1y);
                        if (SegmentDistance(c0x, c0z, c1x, c1z, o0x, o0z, o1x, o1z) < keep + slack) return true;
                    }
                }
            }
            return false;
        }

        // Where a body's centre is at `time` on `leg` (from the leg's start on; before it, where the turn starts): through
        // the turn, round the leg's turning point (R686; on the spot, where it stands), then at the charge's start until
        // the charge, along it, at its end after. `yaw`: the way it faces (for how far an arc bows).
        void Where(in Leg leg, double time, out double x, out double z, out double yaw)
        {
            if (Swings(leg) && leg.Turn > 0 && time < leg.Start + leg.Turn)
            {
                double u = time <= leg.Start ? 0 : (time - leg.Start) / leg.Turn;
                yaw = leg.HeadingFrom + Delta(leg.HeadingFrom, leg.Heading) * Smooth(u);
                Arc(leg, yaw, out x, out z);
                return;
            }
            yaw = leg.Heading;
            double from = leg.ChargeStart, to = leg.ChargeEnd, f;
            if (to <= from || time <= from) f = 0;   // no charge (a wait or home: from and to the same point) or not yet
            else if (time >= to) f = 1;
            else Profile((time - from) / (to - from), out f, out _);
            x = leg.FromX + (leg.ToX - leg.FromX) * f;
            z = leg.FromZ + (leg.ToZ - leg.FromZ) * f;
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
                return new Pose { X = homeX[c], Z = homeZ[c], Yaw = homeYaw[c], Stage = Stage.Waiting, LegStart = double.NaN, ToX = homeX[c], ToZ = homeZ[c], StageStart = double.NaN, StageEnd = double.NaN, HeadingFrom = homeYaw[c], Heading = homeYaw[c] };
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
                    return new Pose { X = homeX[c], Z = homeZ[c], Yaw = homeYaw[c], Stage = Stage.Waiting, LegStart = double.NaN, ToX = homeX[c], ToZ = homeZ[c], StageStart = double.NaN, StageEnd = double.NaN, HeadingFrom = homeYaw[c], Heading = homeYaw[c] };
                var first = list[0];
                return new Pose { X = first.TurnX, Z = first.TurnZ, Yaw = first.HeadingFrom, Stage = Stage.Waiting, LegStart = double.NaN, ToX = first.TurnX, ToZ = first.TurnZ, StageStart = double.NaN, StageEnd = double.NaN, HeadingFrom = first.HeadingFrom, Heading = first.HeadingFrom };
            }
            int i = list.Count - 1;
            while (i > 0 && list[i].Start > time) i--;
            var leg = list[i];
            double u = time - leg.Start;
            var pose = new Pose { X = leg.FromX, Z = leg.FromZ, Yaw = leg.Heading, LegStart = leg.Start, ToX = leg.ToX, ToZ = leg.ToZ,
                                  StageStart = leg.Start, StageEnd = leg.End, HeadingFrom = leg.HeadingFrom, Heading = leg.Heading };
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
                // R686: turning round its turning point, its centre swings round that point.
                pose.Pivot = leg.Pivot;
                pose.Side = leg.Side;
                if (Swings(leg)) Arc(leg, pose.Yaw, out pose.X, out pose.Z);
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
