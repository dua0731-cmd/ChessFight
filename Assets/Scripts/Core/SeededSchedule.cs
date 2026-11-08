using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    // Random obstacles that every PC computes alike (Pawn Rush grand stair hall level design v0.3 §5,
    // "무작위를 온라인에서 맞추는 법"): one integer generator (mulberry32, the same numbers as the design
    // doc's 3D blockout page), seeded from the round's seed mixed with the section's number, writes an
    // event list from the section's start time on the shared obstacle clock. Nothing about the events is
    // sent: the same seed and start give every PC the same list, and an obstacle's pose is a function of
    // that list and the clock. No Unity: the tests check the rules over ten minutes of events.
    public struct SeededRandom
    {
        uint state;

        public SeededRandom(uint seed) { state = seed; }

        // mulberry32.
        public uint NextUInt()
        {
            unchecked
            {
                state += 0x6D2B79F5u;
                uint t = state;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return t ^ (t >> 14);
            }
        }

        public double NextDouble() => NextUInt() / 4294967296.0;

        public double Range(double min, double max) => min + (max - min) * NextDouble();

        // 0 .. n-1.
        public int Next(int n) => n <= 1 ? 0 : (int)(NextDouble() * n);
    }

    public static class SeededSchedule
    {
        // Two numbers into one well-mixed seed (a round's seed and a section's number, a section's seed and
        // an obstacle's own number, ...). Murmur3's finaliser.
        public static uint Hash(uint a, uint b)
        {
            unchecked
            {
                uint h = a * 0x9E3779B1u + b * 0x85EBCA77u + 0x27D4EB2Fu;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        public static uint Hash(int a, int b) => Hash(unchecked((uint)a), unchecked((uint)b));
    }

    // The grand stair's random rolls (v0.3 §5 "무작위 굴림"): every 1.6 ~ 2.6 s a lying pawn statue or a rolled
    // carpet comes out of one of four holes at the top of the stair. Kinds come from a bag of two statues and
    // three carpets, shuffled and refilled. A lane is drawn again while it breaks a rule:
    //   1  the same lane not again within 3 s
    //   2  no statue next to a lane that had a statue within 1.5 s (a lane to step aside into stays free)
    //   3  at most two statues in any 4 s
    // A statue that cannot go anywhere now waits in the bag and a carpet goes instead.
    public sealed class RollingSchedule
    {
        public struct Roll
        {
            public double Time;   // when it leaves the hole
            public int Lane;      // 0 = west .. Lanes - 1
            public bool Statue;   // else a carpet roll
            public long Serial;   // 0, 1, 2 ... in order
        }

        public sealed class Rules
        {
            public int Lanes = 4;
            public double MinGap = 1.6, MaxGap = 2.6;
            public int BagStatues = 2, BagCarpets = 3;
            public double SameLane = 3.0, NeighbourStatue = 1.5, StatueWindow = 4.0;
            public int StatuesPerWindow = 2;
        }

        readonly Rules rules;
        SeededRandom rng;
        double next;
        long serial;
        readonly List<Roll> events = new List<Roll>();
        readonly double[] laneLast, laneLastStatue;
        readonly List<double> statues = new List<double>();
        readonly List<bool> bag = new List<bool>();
        readonly List<int> candidates = new List<int>();

        public RollingSchedule(uint seed, double start, Rules rules = null)
        {
            this.rules = rules ?? new Rules();
            rng = new SeededRandom(seed);
            int lanes = Math.Max(1, this.rules.Lanes);
            laneLast = new double[lanes];
            laneLastStatue = new double[lanes];
            for (int i = 0; i < lanes; i++) laneLast[i] = laneLastStatue[i] = double.NegativeInfinity;
            next = start + rng.Range(this.rules.MinGap, this.rules.MaxGap);
        }

        public Rules Settings => rules;
        public int Count => events.Count;
        public Roll this[int i] => events[i];

        // Writes every roll up to `until` (the clock time plus the hole's warning).
        public void Advance(double until)
        {
            while (next <= until)
            {
                Generate(next);
                next += rng.Range(rules.MinGap, rules.MaxGap);
            }
        }

        // Forgets rolls that left before `before` (they are at the drain by then).
        public void Prune(double before)
        {
            int n = 0;
            while (n < events.Count && events[n].Time < before) n++;
            if (n > 0) events.RemoveRange(0, n);
        }

        void Generate(double t)
        {
            bool statue = TakeFromBag();
            if (statue && !StatueRoom(t))
            {
                statue = false;
                PutBackStatue();
            }
            Candidates(t, statue);
            if (statue && candidates.Count == 0)
            {
                statue = false;
                PutBackStatue();
                Candidates(t, false);
            }
            int lane;
            if (candidates.Count > 0) lane = candidates[rng.Next(candidates.Count)];
            else
            {
                // Cannot happen with the doc's numbers (gaps of 1.6 s, four lanes): the lane rested longest.
                lane = 0;
                for (int i = 1; i < laneLast.Length; i++) if (laneLast[i] < laneLast[lane]) lane = i;
            }
            laneLast[lane] = t;
            if (statue)
            {
                laneLastStatue[lane] = t;
                statues.Add(t);
            }
            events.Add(new Roll { Time = t, Lane = lane, Statue = statue, Serial = serial++ });
        }

        void Candidates(double t, bool statue)
        {
            candidates.Clear();
            for (int lane = 0; lane < laneLast.Length; lane++)
            {
                if (t - laneLast[lane] < rules.SameLane) continue;
                if (statue && (Recent(lane - 1, t) || Recent(lane + 1, t))) continue;
                candidates.Add(lane);
            }
        }

        bool Recent(int lane, double t) =>
            lane >= 0 && lane < laneLastStatue.Length && t - laneLastStatue[lane] < rules.NeighbourStatue;

        bool StatueRoom(double t)
        {
            int n = 0;
            for (int i = statues.Count - 1; i >= 0; i--)
            {
                if (t - statues[i] >= rules.StatueWindow) { statues.RemoveRange(0, i + 1); break; }
                n++;
            }
            return n < rules.StatuesPerWindow;
        }

        bool TakeFromBag()
        {
            if (bag.Count == 0)
            {
                for (int i = 0; i < rules.BagStatues; i++) bag.Add(true);
                for (int i = 0; i < rules.BagCarpets; i++) bag.Add(false);
                // Fisher-Yates.
                for (int i = bag.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    bool swap = bag[i];
                    bag[i] = bag[j];
                    bag[j] = swap;
                }
            }
            if (bag.Count == 0) return false;
            bool kind = bag[bag.Count - 1];
            bag.RemoveAt(bag.Count - 1);
            return kind;
        }

        // The statue goes back for the next roll; a carpet waiting in the bag is used up instead, so the bag
        // still holds its two statues and three carpets.
        void PutBackStatue()
        {
            for (int i = bag.Count - 1; i >= 0; i--)
                if (!bag[i])
                {
                    bag.RemoveAt(i);
                    break;
                }
            bag.Add(true);
        }
    }

    // The central landing's falling chandeliers (v0.3 §5 "샹들리에 낙하"): every 0.7 ~ 1.4 s a chandelier falls
    // somewhere random in the landing's area. Its shadow warns 1.2 s, it falls in 0.4 s. The centre is never
    // on the king's base, and never within 6 m of a drop still warning or just fallen (so a way between the
    // shadows is always open). No room left: that drop is skipped.
    public sealed class ChandelierSchedule
    {
        public struct Drop
        {
            public double Time;   // the warning starts
            public double X, Z;   // the centre, in the section's frame
            public long Serial;
        }

        public sealed class Rules
        {
            public double MinGap = .7, MaxGap = 1.4;
            public double XMin = -13, XMax = 13, ZMin = 54, ZMax = 65;
            public double KeepOutX0 = -2, KeepOutX1 = 2, KeepOutZ0 = 61, KeepOutZ1 = 65;
            public double MinDistance = 6;
            // How long a drop keeps others away from its warning start: warning 1.2 + fall 0.4 + 0.6 "just fallen".
            public double Busy = 2.2;
            public int Tries = 40;
        }

        readonly Rules rules;
        SeededRandom rng;
        double next;
        long serial;
        readonly List<Drop> events = new List<Drop>();

        public ChandelierSchedule(uint seed, double start, Rules rules = null)
        {
            this.rules = rules ?? new Rules();
            rng = new SeededRandom(seed);
            next = start + rng.Range(this.rules.MinGap, this.rules.MaxGap);
        }

        public Rules Settings => rules;
        public int Count => events.Count;
        public Drop this[int i] => events[i];

        public void Advance(double until)
        {
            while (next <= until)
            {
                Generate(next);
                next += rng.Range(rules.MinGap, rules.MaxGap);
            }
        }

        // Forgets drops whose warning started before `before`; keeps what the distance rule still needs.
        public void Prune(double before)
        {
            int n = 0;
            while (n < events.Count && events[n].Time < before) n++;
            if (n > 0) events.RemoveRange(0, n);
        }

        void Generate(double t)
        {
            for (int k = 0; k < Math.Max(1, rules.Tries); k++)
            {
                double x = rng.Range(rules.XMin, rules.XMax), z = rng.Range(rules.ZMin, rules.ZMax);
                if (x > rules.KeepOutX0 && x < rules.KeepOutX1 && z > rules.KeepOutZ0 && z < rules.KeepOutZ1) continue;
                if (Crowded(t, x, z)) continue;
                events.Add(new Drop { Time = t, X = x, Z = z, Serial = serial++ });
                return;
            }
        }

        bool Crowded(double t, double x, double z)
        {
            double min2 = rules.MinDistance * rules.MinDistance;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                var d = events[i];
                if (t - d.Time >= rules.Busy) break;
                double dx = d.X - x, dz = d.Z - z;
                if (dx * dx + dz * dz < min2) return true;
            }
            return false;
        }
    }
}
