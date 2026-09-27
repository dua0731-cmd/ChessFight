using System;

namespace ChessFight.Network
{
    // What carries a pawn up the bulk of a section (DESIGN §3.3 A).
    public enum QueenHillRide
    {
        Lift,       // one platform straight up (S1 grand staircase, S7 balcony stair; graybox stand-ins)
        Gondolas,   // chequered cubes on chains, rising in turn (S2)
        Slabs,      // thin stone slabs rising in turn (S3)
        Ring,       // a spinning ring rising slowly (S4 orbit ring)
        Spring,     // a wound spring that throws everyone on it up to the ledge (S5 clockwork)
        Islands,    // floating garden islands rising in turn (S6)
        LightLift,  // a faster lift of light (S8 stair of light; graybox stand-in)
    }

    // The short effort of a section, from the ride's ledge to the next landing.
    public enum QueenHillChallenge
    {
        Climb,      // a wall of ClimbRise to climb
        LaunchPad,  // the L-shaped pad of S3 (the knight's move), and the wall beside it
        Chain,      // a chain hanging down the wall (S4, S6, S8), and the wall beside it
    }

    public sealed class QueenHillSection
    {
        public int Number { get; }
        public string Name { get; }
        public QueenHillRide Ride { get; }
        public QueenHillChallenge Challenge { get; }
        // Both teams' landings are joined all round at the top of this section
        // (S4 first clash, S7 full merge, S8 the gate).
        public bool JoinedAbove { get; }

        public QueenHillSection(int number, string name, QueenHillRide ride, QueenHillChallenge challenge, bool joinedAbove)
        {
            Number = number;
            Name = name;
            Ride = ride;
            Challenge = challenge;
            JoinedAbove = joinedAbove;
        }
    }

    // The numbers of the Queen of the Hill graybox map, "sky palace" (DESIGN §3,
    // 6th revision): heights, sections and ride timings, with no Unity in them so
    // the pacing is checked by a test (Tests/Network/NetworkCoreTests.cs). The
    // level builder (Gameplay/QueenHill/QueenHillLevel.cs) places everything from
    // these values; change a number here and both follow.
    //
    // Every section rises Rise metres: RideRise on its ride, then ClimbRise by the
    // pawn's own effort (the ragdoll climbs about 6 m on one bar of stamina). The
    // uncontested estimate is worked out from the same values the rides use, so a
    // tuning change that breaks the "about four minutes" target fails the test.
    public static class QueenHillCourse
    {
        public const int Sections = 8;
        public const float SeaLevel = 0f;
        public const float BridgeTop = 12f;       // the two broken bridges
        public const float StartTop = 16f;        // the hook terrace on the cliff, landing 0
        public const float Rise = 18f;            // per section
        public const float RideRise = 13f;
        public const float ClimbRise = 5f;

        // The pawn (measured in the ragdoll lab, Docs/RagdollLab/README.md and Player/RAGDOLL §8).
        public const float RunSpeed = 5f;
        public const float ClimbSpeed = 1.2f;
        public const float HookFullReach = 18.4f;   // a fully charged throw (M5)
        public const float HipsHeight = 0.6f;        // above the feet, standing

        // The gap between the broken end of a bridge and the cliff face.
        public const float BridgeGap = 12f;

        // The light pillar's cells (DESIGN §3.3 B): a lift through the middle for each
        // section, shown when its bell is rung.
        public const float CellSpeed = 3.5f, CellPause = 2f, CellThickness = 0.3f;

        // A lift may not stop harder than gravity or it throws its riders off the top
        // (MovingPlatform): ramp for at least speed / 6 seconds.
        public static float RampFor(float speed) => Math.Max(0.5f, speed / 6f);

        public static readonly QueenHillSection[] All =
        {
            new QueenHillSection(1, "폭포 테라스", QueenHillRide.Lift, QueenHillChallenge.Climb, false),
            new QueenHillSection(2, "사슬 곤돌라", QueenHillRide.Gondolas, QueenHillChallenge.Climb, false),
            new QueenHillSection(3, "떠오르는 석판", QueenHillRide.Slabs, QueenHillChallenge.LaunchPad, false),
            new QueenHillSection(4, "궤도 고리", QueenHillRide.Ring, QueenHillChallenge.Chain, true),
            new QueenHillSection(5, "시계 태엽", QueenHillRide.Spring, QueenHillChallenge.Climb, false),
            new QueenHillSection(6, "공중 정원", QueenHillRide.Islands, QueenHillChallenge.Chain, false),
            new QueenHillSection(7, "궁전 대발코니", QueenHillRide.Lift, QueenHillChallenge.Climb, true),
            new QueenHillSection(8, "하늘의 문", QueenHillRide.LightLift, QueenHillChallenge.Chain, true),
        };

        public static QueenHillSection Section(int number) =>
            number >= 1 && number <= Sections ? All[number - 1] : null;

        // Height of landing `level` (0 = the hook terrace, Sections = the gate at the top).
        public static float LevelTop(int level) => StartTop + Rise * Math.Max(0, Math.Min(Sections, level));

        public static float TopHeight => LevelTop(Sections);

        // The sections alternate sides so the route winds round the tower: +1 east, -1 west
        // (in each team's half; the black half is the white half turned 180 degrees).
        public static int Side(int section) => section % 2 == 1 ? 1 : -1;

        // The light pillar's cell for a section rises through a hole on alternate sides
        // of the spine, so a cell arriving and the next one waiting never overlap.
        public static float CellX(int section) => section % 2 == 1 ? -3.5f : 3.5f;

        public static string RideName(QueenHillRide ride)
        {
            switch (ride)
            {
                case QueenHillRide.Lift: return "승강 계단";
                case QueenHillRide.Gondolas: return "체크 곤돌라";
                case QueenHillRide.Slabs: return "떠오르는 석판";
                case QueenHillRide.Ring: return "궤도 고리";
                case QueenHillRide.Spring: return "태엽 스프링";
                case QueenHillRide.Islands: return "부유섬";
                case QueenHillRide.LightLift: return "빛의 승강기";
                default: return "탈것";
            }
        }

        public static string ChallengeName(QueenHillChallenge challenge)
        {
            switch (challenge)
            {
                case QueenHillChallenge.LaunchPad: return "L자 도약대 (또는 벽)";
                case QueenHillChallenge.Chain: return "사슬 (또는 벽)";
                default: return "벽 오르기";
            }
        }

        // ---------------------------------------------------------------- ride timings

        // How one ride kind moves: `steps` platforms, each travelling `step` metres at
        // `speed` and waiting `pause` at each end. A spring fires every `period` seconds.
        public struct RideTiming
        {
            public int Steps;
            public float Step, Speed, Pause, Period;

            public float Leg => Speed <= 0f ? 0f : Step / Speed + RampFor(Speed);
            public float Cycle => 2f * (Pause + Leg);
        }

        public static RideTiming Timing(QueenHillRide ride)
        {
            switch (ride)
            {
                case QueenHillRide.Lift: return new RideTiming { Steps = 1, Step = RideRise, Speed = 2.5f, Pause = 2.5f };
                case QueenHillRide.LightLift: return new RideTiming { Steps = 1, Step = RideRise, Speed = 3f, Pause = 2f };
                case QueenHillRide.Ring: return new RideTiming { Steps = 1, Step = RideRise, Speed = 1.8f, Pause = 2f };
                // Three cubes 5 m each, overlapping 1 m: 0-5, 4-9, 8-13.
                case QueenHillRide.Gondolas: return new RideTiming { Steps = 3, Step = 5f, Speed = 1.6f, Pause = 1.5f };
                // Four slabs 4 m each, overlapping 1 m: 0-4, 3-7, 6-10, 9-13.
                case QueenHillRide.Slabs: return new RideTiming { Steps = 4, Step = 4f, Speed = 1.6f, Pause = 1f };
                // Two islands 7 m each, overlapping 1 m: 0-7, 6-13.
                case QueenHillRide.Islands: return new RideTiming { Steps = 2, Step = 7f, Speed = 1.5f, Pause = 2f };
                case QueenHillRide.Spring: return new RideTiming { Steps = 1, Period = 5f };
                default: return default;
            }
        }

        // Seconds for an uncontested pawn to get through a section: walk to the ride,
        // wait for it, ride, make the short effort, walk back to the landing and ring
        // the bell. Arriving at a random moment the first platform is waited for about
        // half a cycle; after that the platforms of a chain meet on the beat.
        public static float EstimatedSeconds(QueenHillSection section)
        {
            if (section == null) return 0f;
            var t = Timing(section.Ride);
            float ride;
            if (section.Ride == QueenHillRide.Spring) ride = t.Period * 0.5f + 2f;   // wait, then the flight
            else ride = t.Cycle * 0.35f + t.Steps * t.Leg + (t.Steps - 1) * 0.5f;
            float effort = section.Challenge == QueenHillChallenge.LaunchPad ? 3f : ClimbRise / ClimbSpeed + 1.5f;
            const float walkTo = 15f, walkBack = 30f, bell = 1f;
            return walkTo / RunSpeed + ride + effort + walkBack / RunSpeed + bell;
        }

        // The start: the hook throw from the bridge, the reel in and the short climb over the rim.
        public const float EstimatedStartSeconds = 12f;

        public static float EstimatedTotalSeconds()
        {
            float total = EstimatedStartSeconds;
            foreach (var section in All) total += EstimatedSeconds(section);
            return total;
        }

        // Straight-line distance from a pawn's hips on the broken end of a bridge to
        // the rim of the hook terrace: must be inside a fully charged throw.
        public static float HookDistanceToRim =>
            (float)Math.Sqrt(BridgeGap * BridgeGap + Math.Pow(StartTop - (BridgeTop + HipsHeight), 2));
    }
}
