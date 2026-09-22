using System;
using System.Collections.Generic;

namespace ChessFight.Network
{
    // Host-side roaming for bot roster entries. This is deliberately not gameplay
    // AI: it exists so a filled room shows movement, bandwidth and host CPU load
    // under 12 pawns. King-rush behaviour belongs to the future game layer.
    //
    // Pure C# and seeded from the bot ID, so a given bot replays the same route
    // in tests and the host stays the only writer of authoritative positions.
    public sealed class BotBrain
    {
        public const float ArriveRadius = 1.25f, RoamRadius = 16f;
        readonly Random random;
        float targetX, targetZ;
        double repathAt, pauseUntil, jumpAt;
        uint sequence;

        public BotBrain(ulong id, double now)
        {
            random = new Random(unchecked((int)(id ^ (id >> 32))));
            Repath(now);
            jumpAt = now + Next(1.5, 6);
        }
        double Next(double min, double max) => min + random.NextDouble() * (max - min);
        void Repath(double now)
        {
            targetX = (float)Next(-RoamRadius, RoamRadius);
            targetZ = (float)Next(-RoamRadius, RoamRadius);
            repathAt = now + Next(4, 12); // Abandon a waypoint that is taking too long.
        }
        public MoveInput Think(PawnState state, double now)
        {
            float dx = targetX - state.X, dz = targetZ - state.Z;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);
            if (distance <= ArriveRadius || now >= repathAt)
            {
                Repath(now);
                pauseUntil = now + Next(.2, 1.4);
                dx = targetX - state.X; dz = targetZ - state.Z;
                distance = (float)Math.Sqrt(dx * dx + dz * dz);
            }
            var input = new MoveInput { Sequence = ++sequence };
            if (now >= pauseUntil && distance > 1e-4f) { input.X = dx / distance; input.Z = dz / distance; }
            if (now >= jumpAt) { input.Jump = true; jumpAt = now + Next(2.5, 9); }
            return input;
        }
    }

    // One brain per bot currently in the roster. The host owns this; clients only
    // ever see the resulting snapshots.
    public sealed class BotDirector
    {
        readonly Dictionary<ulong, BotBrain> brains = new Dictionary<ulong, BotBrain>();
        public MoveInput Think(ulong id, PawnState state, double now)
        {
            if (!brains.TryGetValue(id, out var brain)) brains[id] = brain = new BotBrain(id, now);
            return brain.Think(state, now);
        }
        public void Forget(ulong id) => brains.Remove(id);
        public void Clear() => brains.Clear();
    }
}
