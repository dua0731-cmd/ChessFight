using System;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The grand banquet hall's moving things (대연회장, R815 design v0.4, R816 grey box). Each pose is a pure function of the
    // shared obstacle clock (HANDOFF rule 7), so every PC sees them in the same place without a packet, and whoever stands
    // on one or hangs on it is carried through IMovingSurface (Obstacle). The numbers are in GrandBanquetHallBuilder and
    // Docs/PawnRush/PAWN_RUSH_BANQUET_HALL_DESIGN_v0.4.md; ArtSource/PawnRush/BanquetHall/banquet_layout.py has the same
    // time functions.

    // 1. A waiter of the parade: walks up the waiters' lane at a steady pace, out of the kitchen and in at the pantry door,
    //    and is back at the kitchen door the moment he is through it (a cut, not a walk back: JumpsBetween). His tray and
    //    the plate stack on it are the things to ride and climb.
    public sealed class BanquetWaiter : Obstacle
    {
        [SerializeField] float lane = 49f, speed = 2.2f, offset;

        public float Lane => lane;
        public float Speed => speed;

        // Call on an INACTIVE object placed where it is at clock 0 - `offset` metres along the lane from its start - before
        // Awake evaluates the first pose.
        public void Configure(float laneMetres, float metresPerSecond, float offsetMetres)
        {
            lane = laneMetres;
            speed = metresPerSecond;
            offset = offsetMetres;
        }

        // Metres from the lane's start at `time`.
        public double Along(double time)
        {
            double d = (speed * time + offset) % lane;
            return d < 0.0 ? d + lane : d;
        }

        long Lap(double time) => (long)Math.Floor((speed * time + offset) / lane);

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            position = StartPosition + StartRotation * new Vector3(0f, 0f, (float)(Along(time) - offset));
        }

        protected override bool JumpsBetween(double from, double to) => Lap(from) != Lap(to);
    }

    // 3. A silver cloche at the long table's north end: the glove shakes it, lifts it (eased so whoever stands on its rim
    //    is not thrown off), holds it up level with the dais, puts it down quickly and leaves it covered.
    //    warning (a jiggle) -> up -> held -> down -> covered.
    public sealed class BanquetCloche : Obstacle
    {
        [SerializeField] float lift = 5.6f, warning = .8f, up = 1.2f, held = 2.4f, down = .6f, covered = 2.4f, delay = 2.6f;

        public float Lift => lift;
        public float Period => warning + up + held + down + covered;

        public void Configure(float liftMetres, float warningSeconds, float upSeconds, float heldSeconds, float downSeconds,
                              float coveredSeconds, float delaySeconds)
        {
            lift = liftMetres;
            warning = warningSeconds;
            up = upSeconds;
            held = heldSeconds;
            down = downSeconds;
            covered = coveredSeconds;
            delay = delaySeconds;
        }

        // Metres over its resting place at `time`.
        public float Height(double time)
        {
            double period = Period, u = (time - delay) % period;
            if (u < 0.0) u += period;
            float t = (float)u;
            if (t < warning) return .04f * Mathf.Abs(Mathf.Sin(t * 40f));
            t -= warning;
            // A cosine ease: it starts and stops no harder than gravity (5.6 m in 1.2 s peaks at 9.6 m/s2), so a pawn on the
            // rim rides it instead of being thrown up past the dais when it stops.
            if (t < up) return lift * .5f * (1f - Mathf.Cos(Mathf.PI * t / up));
            t -= up;
            if (t < held) return lift;
            t -= held;
            if (t < down) { float k = t / down; return lift * (1f - k * k); }
            return 0f;
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            position = StartPosition + StartRotation * new Vector3(0f, Height(time), 0f);
        }
    }

    // 4. The waltz. Ten couples go round the dance floor anticlockwise along a rounded rectangle (the line of dance) and
    //    each couple turns clockwise about its middle at the rate that makes the queen stand still whenever she is on the
    //    outside: v = SPIN * PAIR. She stops every 2 pi PAIR along the way, so every 4.8 s some queen stands still at the
    //    same places - in front of the dais (where a pawn jumps onto her) and in front of the stage (where it steps off).
    public static class BanquetWaltz
    {
        public const int Couples = 10;
        public const float Pair = 1.9f, Speed = 2.5f;
        public const float TrackW = -16.2f, TrackE = 7.5f, TrackS = 10.2f, TrackN = 51.8f, BoardX = -4.25f;
        public static readonly float Stop = 2f * Mathf.PI * Pair, Spin = Speed / Pair;
        // The corner radius that makes the way round exactly ten stops long, so the whole dance repeats every lap.
        public static readonly float Radius = (2f * (TrackE - TrackW) + 2f * (TrackN - TrackS) - Couples * Stop) / (8f - 2f * Mathf.PI);
        public static readonly float Length = Couples * Stop, Lap = Couples * Stop / Speed;
        static readonly float BoardS = TrackE - Radius - BoardX;

        // (x, z) of the way round the floor `s` metres on from the north straight's east end, anticlockwise from above.
        public static Vector2 Track(double s)
        {
            float r = Radius, w = TrackW, e = TrackE, so = TrackS, n = TrackN;
            double left = s % Length;
            if (left < 0.0) left += Length;
            float d = (float)left, quarter = .5f * Mathf.PI * r;
            Vector2 Line(Vector2 a, Vector2 b, float k) => Vector2.Lerp(a, b, k);
            Vector2 Arc(Vector2 c, float startDeg, float along)
            {
                float a = startDeg * Mathf.Deg2Rad + along / r;
                return new Vector2(c.x + r * Mathf.Cos(a), c.y + r * Mathf.Sin(a));
            }
            float north = (e - r) - (w + r), side = (n - r) - (so + r);
            if (d <= north) return Line(new Vector2(e - r, n), new Vector2(w + r, n), d / north);
            d -= north;
            if (d <= quarter) return Arc(new Vector2(w + r, n - r), 90f, d);
            d -= quarter;
            if (d <= side) return Line(new Vector2(w, n - r), new Vector2(w, so + r), d / side);
            d -= side;
            if (d <= quarter) return Arc(new Vector2(w + r, so + r), 180f, d);
            d -= quarter;
            if (d <= north) return Line(new Vector2(w + r, so), new Vector2(e - r, so), d / north);
            d -= north;
            if (d <= quarter) return Arc(new Vector2(e - r, so + r), 270f, d);
            d -= quarter;
            if (d <= side) return Line(new Vector2(e, so + r), new Vector2(e, n - r), d / side);
            d -= side;
            return Arc(new Vector2(e - r, n - r), 0f, Mathf.Min(d, quarter));
        }

        // The couple's middle (x, z) and the queen's bearing (radians from +x toward +z) at `time`; couple 0's queen stands
        // still in front of the dais at clock 0.
        public static void Pose(int couple, double time, out Vector2 middle, out float bearing)
        {
            double t = time % Lap;
            if (t < 0.0) t += Lap;
            middle = Track(BoardS + couple * Stop + Speed * t);
            bearing = .5f * Mathf.PI - Spin * (float)t;
        }

        public static Vector2 Queen(int couple, double time)
        {
            Pose(couple, time, out var middle, out float bearing);
            return middle + Pair * new Vector2(Mathf.Cos(bearing), Mathf.Sin(bearing));
        }
    }

    public sealed class WaltzCouple : Obstacle
    {
        [SerializeField] int couple;

        public int Couple => couple;

        // Call on an INACTIVE object, a child of the section's content, before Awake.
        public void Configure(int index) => couple = index;

        // The queen stands at local +x, the king at -x: a turn of -bearing about y puts local +x on the bearing.
        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            BanquetWaltz.Pose(couple, time, out var middle, out float bearing);
            var frame = transform.parent;
            var local = new Vector3(middle.x, 0f, middle.y);
            var turn = Quaternion.Euler(0f, -bearing * Mathf.Rad2Deg, 0f);
            position = frame != null ? frame.TransformPoint(local) : local;
            rotation = frame != null ? frame.rotation * turn : turn;
        }
    }

    // 5. A pleat of the stage curtain: it hangs in its place in the shut curtain and is drawn along the rail when the
    //    curtain opens (eased), held open, and drawn back. The south half's leading pleat goes 23.5 m, over the orchestra
    //    pit to above the dessert buffet: whoever hangs on it high enough to clear the double basses is carried there.
    public sealed class CurtainPleat : Obstacle
    {
        [SerializeField] float travel, shut = 6f, opening = 5f, open = 2f, closing = 2f, delay;

        public float Travel => travel;
        public float Period => shut + opening + open + closing;

        public void Configure(float travelMetres, float shutSeconds, float openingSeconds, float openSeconds, float closingSeconds,
                              float delaySeconds)
        {
            travel = travelMetres;
            shut = shutSeconds;
            opening = openingSeconds;
            open = openSeconds;
            closing = closingSeconds;
            delay = delaySeconds;
        }

        // 0 shut .. 1 open.
        public float Open(double time)
        {
            double period = Period, u = (time - delay) % period;
            if (u < 0.0) u += period;
            float t = (float)u;
            if (t < shut) return 0f;
            t -= shut;
            if (t < opening) return Smooth(t / opening);
            t -= opening;
            if (t < open) return 1f;
            return 1f - Smooth((t - open) / closing);
        }

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            rotation = StartRotation;
            position = StartPosition + StartRotation * new Vector3(0f, 0f, travel * Open(time));
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
