using System;

namespace ChessFight.Network
{
    /// <summary>
    /// R297 (HOST_AUTHORITY P5 without bots): a development build's own player that plays by itself (-autopilot), so a few
    /// testers can keep several PCs moving for the 12-player baselines without sitting at each one. It only makes this PC's
    /// keys - a walk that wanders but keeps going forward, now and then a pause, a sprint, a jump, the skill key, a click -
    /// and the PC's link sends them like a person's: no bot, nothing on the wire changes. The same seed and the same steps
    /// give the same inputs (NetworkCoreTests).
    /// </summary>
    public sealed class AutoPilot
    {
        /// <summary>One step's keys. The move is camera-relative (x right, y forward, length at most 1); Jump, Skill and Click
        /// are presses, true for one step.</summary>
        public struct Frame
        {
            public float MoveX, MoveY;
            public bool Sprint, Jump, Skill, Click;
        }

        public const string Argument = "-autopilot";
        // A heading this far either side of the camera's forward (radians, 55 degrees), turned at a person's pace.
        const float MaxTurn = 0.96f, TurnRate = 2.5f;

        public readonly uint Seed;
        uint rng;
        double clock, turnAt, stopUntil, sprintUntil, jumpAt, skillAt, clickAt;
        float heading, target;

        public AutoPilot(uint seed)
        {
            Seed = seed;
            rng = seed != 0 ? seed : 0x9E3779B9u;
            // Not every PC on its first press at once.
            jumpAt = Range(0.5f, 2f);
            skillAt = Range(2f, 6f);
            clickAt = Range(1f, 4f);
        }

        /// <summary>The command line's ask in a development build: "-autopilot" (a seed made from this PC's id, so every PC
        /// plays differently) or "-autopilot=N". False when it is absent or N is not a number.</summary>
        public static bool Requested(string[] args, ulong self, out uint seed)
        {
            seed = 0;
            if (args == null) return false;
            foreach (string arg in args)
            {
                if (arg == Argument)
                {
                    seed = (uint)(self ^ (self >> 32));
                    if (seed == 0) seed = 1;
                    return true;
                }
                if (arg != null && arg.StartsWith(Argument + "=", StringComparison.Ordinal))
                    return uint.TryParse(arg.Substring(Argument.Length + 1), out seed);
            }
            return false;
        }

        /// <summary>The keys for a step of <paramref name="dt"/> seconds (a frame).</summary>
        public Frame Step(float dt)
        {
            if (!(dt > 0f)) dt = 0f;
            clock += Math.Min(dt, 0.25f);
            var f = new Frame();
            if (clock >= turnAt)
            {
                target = Range(-MaxTurn, MaxTurn);
                turnAt = clock + Range(1.5f, 4f);
                if (Next() < 0.12f) stopUntil = clock + Range(0.4f, 1.2f);
                if (Next() < 0.35f) sprintUntil = clock + Range(1f, 3f);
            }
            float turn = TurnRate * Math.Min(dt, 0.25f);
            heading += Math.Max(-turn, Math.Min(turn, target - heading));
            bool walking = clock >= stopUntil;
            if (walking)
            {
                f.MoveX = (float)Math.Sin(heading);
                f.MoveY = (float)Math.Cos(heading);
            }
            f.Sprint = walking && clock < sprintUntil;
            if (clock >= jumpAt) { f.Jump = true; jumpAt = clock + Range(0.8f, 3f); }
            if (clock >= skillAt) { f.Skill = true; skillAt = clock + Range(4f, 10f); }
            if (clock >= clickAt) { f.Click = true; clickAt = clock + Range(2f, 6f); }
            return f;
        }

        // xorshift32: the same numbers on every PC and runtime for a seed.
        float Next()
        {
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return (rng >> 8) / 16777216f;
        }

        float Range(float min, float max) => min + (max - min) * Next();
    }
}
