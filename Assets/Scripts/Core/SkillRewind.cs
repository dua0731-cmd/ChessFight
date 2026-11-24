using System;

namespace ChessFight.Network
{
    /// <summary>
    /// R819 (2026-11-24, 사용자 "폰 러쉬 스킬 판정이 온라인에서 애매하대 … 뛰고 계속 움직이니까 스킬이 안 걸릴 때도 있어"): how old the
    /// picture is that a client aimed its skill at. The host judges every skill against the pieces where they are now, but a
    /// client draws the others a playback delay behind the newest snapshot, and its press reaches the host half a round trip
    /// later still: a piece running at 5..9.6 m/s has moved one or two metres by the time the host hears the press, so a
    /// target picked on the client's screen was often out of the host's cone, range or reach. The client already sends its
    /// round trip and its playback delay with every input (RagdollLabInput.roundTripMs / delayMs); this turns them into the
    /// seconds the host looks back (Docs/Skills/PAWN_RUSH_V03.md §3.3).
    /// </summary>
    public static class SkillLag
    {
        /// <summary>The look-back never goes further than this (s): past it a dodge on the target's own screen would count
        /// for nothing (SKILLS_ONLINE D-S9: 150 ms round trips are the ones that must play well).</summary>
        public const float MostSeconds = 0.25f;

        /// <summary>What the averages add on top of the two numbers the client reports (ms): the drawn snapshot's age (half
        /// a 30 Hz interval) and the input's wait for its send (half a 60 Hz one).</summary>
        public const float ExtraMs = 25f;

        /// <summary>The seconds a client's picture of the others was behind the host when its input arrived: its round trip
        /// (there and back: the snapshot it drew came half a trip, its press goes half a trip), its playback delay and
        /// <see cref="ExtraMs"/>, at most <see cref="MostSeconds"/>. Zero for a player who has reported nothing yet (the host
        /// itself, a bot, a client's first packets).</summary>
        public static float Seconds(int roundTripMs, int delayMs)
        {
            if (roundTripMs <= 0 && delayMs <= 0) return 0f;
            float ms = Math.Max(0, roundTripMs) + Math.Max(0, delayMs) + ExtraMs;
            return Math.Min(MostSeconds, ms / 1000f);
        }
    }

    /// <summary>
    /// R819: a short history of a few numbers (a piece's hips, chest and head) by time, kept by the host for every piece so a
    /// skill can ask where a piece was as the caster's screen showed it (<see cref="SkillLag"/>). A fixed ring: the newest
    /// <c>capacity</c> samples, read back with a straight line between the two around the asked time.
    /// </summary>
    public sealed class SkillTrail
    {
        readonly double[] times;
        readonly float[] values;
        readonly int width;
        int count, next;

        public SkillTrail(int capacity, int width)
        {
            if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            this.width = width;
            times = new double[capacity];
            values = new float[capacity * width];
        }

        public int Count => count;
        public int Width => width;

        /// <summary>The oldest time held (NaN when empty).</summary>
        public double Oldest => count == 0 ? double.NaN : times[Index(0)];

        /// <summary>The newest time held (NaN when empty).</summary>
        public double Newest => count == 0 ? double.NaN : times[Index(count - 1)];

        public void Clear() { count = next = 0; }

        /// <summary>A sample at <paramref name="time"/> (s). One not after the newest replaces nothing and is dropped (a
        /// repeated physics step, a clock that went back after a reload).</summary>
        public void Add(double time, float[] row)
        {
            if (row == null || row.Length < width) throw new ArgumentException("row is shorter than the trail's width");
            if (count > 0 && time <= Newest) return;
            times[next] = time;
            Array.Copy(row, 0, values, next * width, width);
            next = (next + 1) % times.Length;
            if (count < times.Length) count++;
        }

        /// <summary>The numbers at <paramref name="time"/>: between two samples a straight line, before the oldest the oldest,
        /// after the newest the newest. False when there is nothing yet.</summary>
        public bool At(double time, float[] into)
        {
            if (into == null || into.Length < width) throw new ArgumentException("into is shorter than the trail's width");
            if (count == 0) return false;
            if (time <= times[Index(0)]) { Copy(Index(0), into); return true; }
            if (time >= times[Index(count - 1)]) { Copy(Index(count - 1), into); return true; }
            // The newest sample not after the time (the ring is in time order from its oldest).
            int lo = 0, hi = count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (times[Index(mid)] <= time) lo = mid;
                else hi = mid;
            }
            int a = Index(lo), b = Index(hi);
            double span = times[b] - times[a];
            float u = span > 1e-9 ? (float)((time - times[a]) / span) : 0f;
            for (int i = 0; i < width; i++)
            {
                float va = values[a * width + i], vb = values[b * width + i];
                into[i] = va + (vb - va) * u;
            }
            return true;
        }

        int Index(int k) => (next - count + k + times.Length) % times.Length;

        void Copy(int slot, float[] into) => Array.Copy(values, slot * width, into, 0, width);
    }
}
