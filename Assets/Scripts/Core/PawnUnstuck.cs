using System;

namespace ChessFight.Network
{
    // R786 (user 11-23, "끼임 판정 + R 길게"; Docs/Architecture/UI.md §12 R786): a Pawn Rush pawn stuck in the course
    // - its ragdoll caught in a wall, a gap it cannot climb out of - may go back to a checkpoint, and only then, and
    // never forward. A free "back to the checkpoint" key would be a shortcut (knocked back, a checkpoint ahead is a
    // teleport forward), a way out of a grab or a skill's pull, and a skip of every knockdown. No Unity: the match host
    // runs it for every pawn, each client for its own prompt, the tests run it.
    //
    //  * Stuck: the move keys are pushed and the hips stay within Radius of one place for StuckAfter seconds of pushing
    //    (letting go of the keys pauses the count). Held by another pawn, carried by a skill, flying, a respawn already
    //    waiting, or in a mini-game room: no count, it starts again. A knockdown does count - a normal one is over well
    //    before StuckAfter, so there is nothing to skip.
    //  * Then the key is held HoldSeconds (letting go, or the stuck state ending, starts the hold again); the host takes
    //    the request if its own count is within Slack of StuckAfter, and puts the pawn back BackDelay later.
    //  * Back where: the highest checkpoint it has reached that is not ahead of where it stands (Back). One use, then
    //    Cooldown seconds before the next.
    public sealed class StuckWatch
    {
        public const float Radius = 1f;
        public const float StuckAfter = 5f;
        public const float HoldSeconds = 2f;
        public const float BackDelay = 2f;
        public const float Cooldown = 30f;
        // The host takes a request from a count this much short of StuckAfter: the client counted on its puppet, which is
        // drawn a little behind the host's body, from its own keys, which reach the host a little later.
        public const float Slack = 1.5f;

        float ax, ay, az;
        bool anchored;

        // Seconds of pushing at this place.
        public float Pushed { get; private set; }
        public bool Stuck => Pushed >= StuckAfter;
        public bool StuckWithin(float slack) => Pushed >= StuckAfter - slack;

        public void Step(float dt, float x, float y, float z, bool pushing, bool barred)
        {
            bool finite = !float.IsNaN(x + y + z) && !float.IsInfinity(x + y + z);
            float dx = x - ax, dy = y - ay, dz = z - az;
            if (!anchored || barred || !finite || !(dx * dx + dy * dy + dz * dz <= Radius * Radius))
            {
                ax = x; ay = y; az = z;
                anchored = finite;
                Pushed = 0f;
                if (barred || !finite) return;
            }
            if (pushing && dt > 0f && !float.IsNaN(dt) && !float.IsInfinity(dt)) Pushed += dt;
        }

        public void Reset()
        {
            anchored = false;
            Pushed = 0f;
        }

        // One use, then Cooldown seconds (lastUsed < 0: never used).
        public static bool Ready(float now, float lastUsed) => lastUsed < 0f || now - lastUsed >= Cooldown;

        public static int SecondsLeft(float now, float lastUsed) =>
            Ready(now, lastUsed) ? 0 : (int)Math.Ceiling(Cooldown - (now - lastUsed));

        // Within one section a checkpoint up to Margin metres along the way past the pawn still counts as not ahead (its
        // spots stand across the way, the pawn may be stuck right on it).
        public const float Margin = 3f;

        // Where back: of the checkpoints it has reached (order, and where each stands: section index, metres along that
        // section), the one with the highest order that is not ahead of the pawn (section, metres). A section of -1 is a
        // place the course cannot measure. -1: none (the caller sends it to its start). The pawn unmeasured: the nearest of
        // them by straight-line distance (dist), which a stuck pawn is never far past.
        public static int Back(int[] orders, int[] sections, float[] metres, float[] dist, int count, int section, float at)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                if (section < 0)
                {
                    if (best < 0 || dist[i] < dist[best]) best = i;
                    continue;
                }
                if (sections[i] < 0) continue;
                bool notAhead = sections[i] < section || (sections[i] == section && !(metres[i] > at + Margin));
                if (notAhead && (best < 0 || orders[i] > orders[best])) best = i;
            }
            return best;
        }
    }

    // The client's side: the key held while stuck. Done is true on the one step the hold completes.
    public sealed class UnstuckHold
    {
        public float Held { get; private set; }
        public float Share => Math.Min(1f, Held / StuckWatch.HoldSeconds);

        public bool Step(float dt, bool stuck, bool keyHeld)
        {
            if (!stuck || !keyHeld)
            {
                Held = 0f;
                return false;
            }
            float before = Held;
            if (dt > 0f && !float.IsNaN(dt) && !float.IsInfinity(dt)) Held += dt;
            return before < StuckWatch.HoldSeconds && Held >= StuckWatch.HoldSeconds;
        }

        public void Reset() => Held = 0f;
    }
}
