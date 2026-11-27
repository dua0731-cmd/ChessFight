using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The rooks' rampart walk's devices (룩 성벽길, R843 design v0.1, R859 2D explainer, R860 grey box): the siege ladders and
    // their rook defenders, the murder holes, the siege towers and their drop-bridges, the rooks' drill square, the rook and
    // knight cranes and the king's banner. Every one is a pure function of the shared obstacle clock (RookRampartTimes, the
    // C# twin of ArtSource/PawnRush/RookRampart/rampart_layout.py); nothing here is sent. The movers are Obstacles built at
    // their reference pose with no turn of their own; Preview(t) puts one where it is at clock time t (the edit-mode
    // pictures of RookRampartValidation).

    // The design's time functions, in the section frame (x east, y up, z north, metres, seconds).
    public static class RookRampartTimes
    {
        public static double Mod(double a, double n) { double r = a % n; return r < 0.0 ? r + n : r; }
        public static float Ease(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

        // ① The siege ladders: degrees from the ground on the wall's side - LEAN against the wall, 90 upright, FALLEN on the glacis.
        public const float Lean = 75f, Fallen = 180f, PushCycle = 9f, Wind = .6f, Tip = .9f, Lie = .6f, Raise = 1.4f;
        public static readonly float[] Ladders = { -18.5f, -15.5f, -12.5f, -9.5f, -6.5f, -3.5f };
        public static readonly float[] PushAt = { 0f, 3f, 6f, 1.5f, 4.5f, 7.5f };

        public static float LadderAngle(int i, double t)
        {
            float u = (float)Mod(t - PushAt[i], PushCycle);
            if (u < Wind) return Lean;
            u -= Wind;
            if (u < Tip) { float k = u / Tip; return Lean + (Fallen - Lean) * k * k; }   // slow off the wall, then faster
            u -= Tip;
            if (u < Lie) return Fallen;
            u -= Lie;
            if (u < Raise) return Fallen - (Fallen - Lean) * Ease(u / Raise);
            return Lean;
        }

        // Where defender d (0: ladders 0-2, 1: ladders 3-5) stands along the walk: at each ladder while it pushes it.
        public static float DefenderX(int d, double t)
        {
            int first = 3 * d;
            float u = (float)Mod(t - PushAt[first], PushCycle), step = PushCycle / 3f;
            int k = Mathf.Min(2, (int)(u / step));
            float v = u - k * step, hold = Wind + Tip;
            float here = Ladders[first + k], there = Ladders[first + (k + 1) % 3];
            return v < hold ? here : Mathf.Lerp(here, there, Ease((v - hold) / (step - hold)));
        }

        // Seconds into ladder i's cycle and whether its pole is down on it (the warning and the push).
        public static float LadderPhase(int i, double t) => (float)Mod(t - PushAt[i], PushCycle);

        // ② The murder holes: 0 shut, 1 rattling (about to open), 2 open; light squares (i + j even) first.
        public const float HoleRattle = .4f, HoleOpen = 1.2f, HoleShut = .6f, HoleCycle = 2f * (HoleRattle + HoleOpen + HoleShut);

        public static int Trapdoor(int i, int j, double t)
        {
            float u = (float)Mod(t, HoleCycle), half = HoleCycle * .5f;
            bool dark = (i + j) % 2 != 0;
            if (u >= half != dark) return 0;
            u %= half;
            return u < HoleRattle ? 1 : u < HoleRattle + HoleOpen ? 2 : 0;
        }

        // ③ The siege towers: the z of its middle and the drop-bridge's angle (90 up, 0 down onto the wall).
        public const float Boarding = 4f, Roll = 8f, BridgeMove = .6f, BridgeOpen = 3.4f, RollBack = 7.4f;
        public const float SiegeCycle = Boarding + Roll + BridgeMove + BridgeOpen + BridgeMove + RollBack;

        public static void Siege(double t, float delay, float z0, float z1, out float z, out float bridge)
        {
            float u = (float)Mod(t - delay, SiegeCycle);
            z = z0; bridge = 90f;
            if (u < Boarding) return;
            u -= Boarding;
            if (u < Roll) { z = Mathf.Lerp(z0, z1, Ease(u / Roll)); return; }
            u -= Roll;
            z = z1;
            if (u < BridgeMove) { bridge = 90f * (1f - Ease(u / BridgeMove)); return; }
            u -= BridgeMove;
            if (u < BridgeOpen) { bridge = 0f; return; }
            u -= BridgeOpen;
            if (u < BridgeMove) { bridge = 90f * Ease(u / BridgeMove); return; }
            u -= BridgeMove;
            z = Mathf.Lerp(z1, z0, Ease(u / RollBack));
        }

        // ④ The drill square: a rook rests at one end of its file or rank, its line glows, it slides to the other end, rests,
        //    glows, slides back. `s` 0 .. 1 from its start end; `state` 0 rest, 1 glowing, 2 sliding.
        public const float RookRest = 2f, RookGlow = 1f, RookSpeed = 8f;

        public static float Slide(double t, float delay, float slide, out int state)
        {
            float cycle = 2f * (RookRest + RookGlow + slide), half = cycle * .5f;
            float u = (float)Mod(t - delay, cycle);
            bool back = u >= half;
            u %= half;
            float k = 0f;
            state = 0;
            if (u < RookRest) state = 0;
            else if (u < RookRest + RookGlow) state = 1;
            else { k = Mathf.Min(1f, (u - RookRest - RookGlow) / slide); state = k < 1f ? 2 : 0; }
            return back ? 1f - k : k;
        }

        // ⑤ The rook crane: (crown's y, crown's z) - straight up, straight across, a stop at each end.
        public const float RcBoard = 3.5f, RcUp = 1.8f, RcAcross = 1.2f, RcOff = 3f, RcDelay = 7.85f;
        public const float RcCycle = RcBoard + RcUp + RcAcross + RcOff + RcAcross + RcUp;

        public static void RookCrane(double t, float lo, float top, float zLow, float zHigh, out float y, out float z)
        {
            float u = (float)Mod(t - RcDelay, RcCycle);
            y = lo; z = zLow;
            if (u < RcBoard) return;
            u -= RcBoard;
            if (u < RcUp) { y = Mathf.Lerp(lo, top, Ease(u / RcUp)); return; }
            u -= RcUp;
            y = top;
            if (u < RcAcross) { z = Mathf.Lerp(zLow, zHigh, Ease(u / RcAcross)); return; }
            u -= RcAcross;
            z = zHigh;
            if (u < RcOff) return;
            u -= RcOff;
            if (u < RcAcross) { z = Mathf.Lerp(zHigh, zLow, Ease(u / RcAcross)); return; }
            u -= RcAcross;
            z = zLow;
            y = Mathf.Lerp(top, lo, Ease(u / RcUp));
        }

        // ⑤ The knight crane (R859): a leap there, a 0.6 s turn, a leap back, a turn - it never waits. `s` 0 at the middle
        //    wall's edge .. 1 on the inner wall; `facing` degrees about the vertical, 0 north.
        public const float KnightLeap = 3f, KnightTurn = .6f, KnightHop = 4f, KnightDelay = 4.2f;
        public const float KnightCycle = 2f * (KnightTurn + KnightLeap);

        public static float KnightPhase(double t, out float facing)
        {
            float u = (float)Mod(t - KnightDelay, KnightCycle);
            if (u < KnightTurn) { facing = 180f * (1f - Ease(u / KnightTurn)); return 0f; }
            u -= KnightTurn;
            if (u < KnightLeap) { facing = 0f; return .5f * (1f - Mathf.Cos(Mathf.PI * u / KnightLeap)); }
            u -= KnightLeap;
            if (u < KnightTurn) { facing = 180f * Ease(u / KnightTurn); return 1f; }
            u -= KnightTurn;
            facing = 180f;
            return .5f * (1f + Mathf.Cos(Mathf.PI * u / KnightLeap));
        }

        public static void Knight(double t, float lowY, float highY, float lowZ, float highZ, out float y, out float z, out float facing)
        {
            float s = KnightPhase(t, out facing);
            y = Mathf.Lerp(lowY, highY, s) + KnightHop * Mathf.Sin(Mathf.PI * s);
            z = Mathf.Lerp(lowZ, highZ, s);
        }

        // ⑥ The king's banner: degrees out from the wall (0 flat, BILLOW at the gust's height).
        public const float GustCycle = 7f, GustWarn = 1f, GustOut = .6f, GustHold = 1f, GustBack = .8f, Billow = 40f;

        public static float BannerAngle(double t)
        {
            float u = (float)Mod(t, GustCycle);
            if (u < GustWarn) return 0f;
            u -= GustWarn;
            if (u < GustOut) return Billow * Ease(u / GustOut);
            u -= GustOut;
            if (u < GustHold) return Billow;
            u -= GustHold;
            if (u < GustBack) return Billow * (1f - Ease(u / GustBack));
            return 0f;
        }

        // The pennants stream from the warning to the end of the gust.
        public static bool Windy(double t) => Mod(t, GustCycle) < GustWarn + GustOut + GustHold + GustBack;
    }

    // A mover that can be shown where it is at a clock time in Edit mode (no Awake there): the pictures.
    public interface IRampartPreview
    {
        void Preview(double time);
    }

    // The base of the rampart's movers: built at their reference pose in the section frame, unturned; `Local` gives the
    // pose at a time as an offset and a turn from there.
    public abstract class RampartMover : Obstacle, IRampartPreview
    {
        protected abstract void Local(double time, out Vector3 offset, out Quaternion turn);

        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            Local(time, out var offset, out var turn);
            position = StartPosition + StartRotation * offset;
            rotation = StartRotation * turn;
        }

        // Edit mode only: from the pose it was built in (the transform's own, which Awake has not moved).
        public void Preview(double time)
        {
            if (Application.isPlaying) return;
            if (!previewed)
            {
                previewed = true;
                built = transform.position;
                builtTurn = transform.rotation;
            }
            Local(time + phase, out var offset, out var turn);
            transform.SetPositionAndRotation(built + builtTurn * offset, builtTurn * turn);
        }

        bool previewed;
        Vector3 built;
        Quaternion builtTurn = Quaternion.identity;
    }

    // ① A siege ladder: it turns about its foot (this object) on the wall's side, local +y up the ladder at 90 - angle about x.
    //    Past upright it cannot be held (NoClimbSurface on): a climber still on it is let go, as the design has it.
    public sealed class SiegeLadder : RampartMover
    {
        [SerializeField] int index;
        [SerializeField] NoClimbSurface unheld;

        public int Index => index;

        public void Configure(int ladder, NoClimbSurface pastUpright)
        {
            index = ladder;
            unheld = pastUpright;
        }

        public float Angle(double time) => RookRampartTimes.LadderAngle(index, time);

        protected override void Local(double time, out Vector3 offset, out Quaternion turn)
        {
            offset = Vector3.zero;
            turn = Quaternion.Euler(90f - Angle(time), 0f, 0f);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (unheld != null) unheld.enabled = Angle(ObstacleClock.Now + phase) > 90f;
        }
    }

    // ① A rook defender on the outer wall's walk: walks between its three ladders, stops at each while its pole pushes it.
    //    Its body pushes whoever is in the way; the pole is a picture (LateUpdate) - lowered onto the ladder's top during the
    //    warning, following it past upright, raised again.
    public sealed class LadderDefender : RampartMover
    {
        [SerializeField] int defender;
        [SerializeField] Transform pole;
        [SerializeField] float ladderLength = 5.15f, ladderFootZ = 9.77f, walkY = 3.5f, standZ = 13.2f;

        // `walk` and `stand`: where this object was built in the section frame (its y and z); the ladders' feet are at y 0.
        public void Configure(int which, Transform poleUnder, float ladderLengthMetres, float footZ, float walk, float stand)
        {
            defender = which;
            pole = poleUnder;
            ladderLength = ladderLengthMetres;
            ladderFootZ = footZ;
            walkY = walk;
            standZ = stand;
        }

        protected override void Local(double time, out Vector3 offset, out Quaternion turn)
        {
            offset = new Vector3(RookRampartTimes.DefenderX(defender, time) - RookRampartTimes.Ladders[3 * defender], 0f, 0f);
            turn = Quaternion.identity;
        }

        void LateUpdate() => Pole(ObstacleClock.Now + phase);

        public void PosePole(double time) => Pole(time);

        // The pole's tip on the ladder this defender is at (its top while the pole is down on it), or held up.
        void Pole(double time)
        {
            if (pole == null) return;
            int first = 3 * defender;
            float u = (float)RookRampartTimes.Mod(time - RookRampartTimes.PushAt[first], RookRampartTimes.PushCycle);
            int k = Mathf.Min(2, (int)(u / (RookRampartTimes.PushCycle / 3f)));
            int i = first + k;
            float at = RookRampartTimes.LadderPhase(i, time), angle = RookRampartTimes.LadderAngle(i, time);
            float w = RookRampartTimes.Wind;
            float upright = w + RookRampartTimes.Tip * Mathf.Sqrt((90f - RookRampartTimes.Lean) / (RookRampartTimes.Fallen - RookRampartTimes.Lean));
            // 0 held up .. 1 on the ladder's top: lowered in the warning, on it while it tips to upright, raised after.
            float down = at < w ? RookRampartTimes.Ease(at / w) : at < upright ? 1f
                       : at < upright + .8f ? 1f - RookRampartTimes.Ease((at - upright) / .8f) : 0f;
            // The ladder's top in this object's frame (built at y = walkY, z = standZ; the ladder's foot at y 0).
            float a = Mathf.Min(angle, 90f) * Mathf.Deg2Rad;
            Vector3 hands = new Vector3(0f, 1.3f, .3f);
            Vector3 top = new Vector3(0f, ladderLength * Mathf.Sin(a) - walkY + .05f, ladderFootZ + ladderLength * Mathf.Cos(a) - standZ + .05f);
            Vector3 up = hands + new Vector3(0f, 2.9f, -.75f);
            Vector3 tip = Vector3.Lerp(up, top, down);
            Vector3 d = (hands - tip).normalized;
            pole.localPosition = tip + d * 1.8f;     // a 3.6 m pole: its tip at `tip`, its butt past the hands
            pole.localRotation = Quaternion.FromToRotation(Vector3.up, d);
        }
    }

    // ② The murder holes over the outer gate: 5 x 3 trapdoors in the walk, light squares, then dark ones, each after its lids
    //    rattle. An open trapdoor has no floor (its collider off) and its lid hangs down into the passage.
    public sealed class MurderHoles : MonoBehaviour, IRampartPreview
    {
        [SerializeField] Collider[] floors = new Collider[0];
        [SerializeField] Transform[] lids = new Transform[0];
        [SerializeField] GameObject[] rattles = new GameObject[0];
        [SerializeField] int across = 5, rows = 3;
        [SerializeField] float hole = 2f;

        public int Across => across;
        public int Rows => rows;

        public void Configure(Collider[] floorColliders, Transform[] lidPictures, GameObject[] rattleRims, int nx, int nz, float size)
        {
            floors = floorColliders;
            lids = lidPictures;
            rattles = rattleRims;
            across = nx;
            rows = nz;
            hole = size;
        }

        public int State(int i, int j, double time) => RookRampartTimes.Trapdoor(i, j, time);

        void FixedUpdate()
        {
            double now = ObstacleClock.Now;
            for (int i = 0; i < across; i++)
                for (int j = 0; j < rows; j++)
                {
                    var c = floors[i * rows + j];
                    if (c != null) c.enabled = State(i, j, now) != 2;
                }
        }

        void LateUpdate() => Pose(ObstacleClock.Now, Time.time);

        public void Preview(double time)
        {
            for (int i = 0; i < across; i++)
                for (int j = 0; j < rows; j++)
                    if (floors[i * rows + j] != null) floors[i * rows + j].enabled = State(i, j, time) != 2;
            Pose(time, (float)time);
        }

        // Each lid: flat when shut, shaking when it rattles (its rim glows), hanging down from its south edge when open.
        void Pose(double time, float shake)
        {
            for (int i = 0; i < across; i++)
                for (int j = 0; j < rows; j++)
                {
                    int n = i * rows + j, s = State(i, j, time);
                    var lid = lids[n];
                    if (lid != null)
                    {
                        float jitter = s == 1 ? Mathf.Sin(shake * 70f + n) * 3f : 0f;
                        lid.localRotation = Quaternion.Euler(s == 2 ? 88f : jitter, 0f, s == 1 ? Mathf.Cos(shake * 53f + n) * 2f : 0f);
                    }
                    if (rattles[n] != null && rattles[n].activeSelf != (s == 1)) rattles[n].SetActive(s == 1);
                }
        }
    }

    // ③ A siege tower: its body (floors, hides, the two inner ladders, the roof) rolls along its ruts on the clock; whoever
    //    stands in it or climbs its ladders is carried (it is one kinematic body). Its crew push it from inside the base.
    public sealed class SiegeTower : RampartMover
    {
        [SerializeField] float delay, z0, z1;
        [SerializeField] Transform[] wheels = new Transform[0];
        [SerializeField] Transform[] crew = new Transform[0];

        public float Delay => delay;

        public void Configure(float delaySeconds, float dockedOuter, float dockedMiddle, Transform[] wheelPictures, Transform[] crewPictures)
        {
            delay = delaySeconds;
            z0 = dockedOuter;
            z1 = dockedMiddle;
            wheels = wheelPictures;
            crew = crewPictures;
        }

        public float Z(double time)
        {
            RookRampartTimes.Siege(time, delay, z0, z1, out float z, out _);
            return z;
        }

        protected override void Local(double time, out Vector3 offset, out Quaternion turn)
        {
            offset = new Vector3(0f, 0f, Z(time) - z0);
            turn = Quaternion.identity;
        }

        void LateUpdate() => Pictures(ObstacleClock.Now + phase);

        public void PosePictures(double time) => Pictures(time);

        // The wheels turn with the way rolled; the crew lean the way it goes.
        void Pictures(double time)
        {
            float z = Z(time), v = (Z(time + .05) - Z(time - .05)) / .1f;
            foreach (var w in wheels)
                if (w != null) w.localRotation = Quaternion.Euler((z - z0) / .6f * Mathf.Rad2Deg, 0f, 90f);
            foreach (var c in crew)
                if (c != null) c.localRotation = Quaternion.Euler(Mathf.Clamp(v * 6f, -18f, 18f), 0f, 0f);
        }
    }

    // ③ A siege tower's drop-bridge: hinged on the tower's front over its top floor, it rolls with the tower and falls onto
    //    the middle wall's sill when the tower is there. Local +z along the plank; 0 degrees down, 90 up.
    public sealed class SiegeBridge : RampartMover
    {
        [SerializeField] float delay, z0, z1;

        public void Configure(float delaySeconds, float dockedOuter, float dockedMiddle)
        {
            delay = delaySeconds;
            z0 = dockedOuter;
            z1 = dockedMiddle;
        }

        public float Angle(double time)
        {
            RookRampartTimes.Siege(time, delay, z0, z1, out _, out float bridge);
            return bridge;
        }

        protected override void Local(double time, out Vector3 offset, out Quaternion turn)
        {
            RookRampartTimes.Siege(time, delay, z0, z1, out float z, out float bridge);
            offset = new Vector3(0f, 0f, z - z0);
            turn = Quaternion.Euler(-bridge, 0f, 0f);
        }
    }

    // ④ A drilling rook on the great tower's roof: slides the whole length of its file or rank (from `from` to `to`, local
    //    offsets from where it was built) at 8 m/s after its line has glowed a second. While it slides, whoever it reaches is
    //    knocked down and swept ahead of it (the authority's hit, as RobotSweep's).
    public sealed class DrillRook : RampartMover
    {
        [SerializeField] Vector3 travel;
        [SerializeField] float delay, slide = 2.4f, reach = 1.35f, push = 6f, lift = 1.5f, knockdown = 1.2f;
        [SerializeField] Renderer line;
        [SerializeField] Material lineRest, lineGlow, lineSlide;

        readonly Collider[] overlaps = new Collider[48];
        readonly HashSet<ImportedObstacleActor> struck = new HashSet<ImportedObstacleActor>();
        long struckSlide = long.MinValue;

        public void Configure(Vector3 travelLocal, float delaySeconds, float slideSeconds, Renderer linePicture, Material rest, Material glow, Material sliding)
        {
            travel = travelLocal;
            delay = delaySeconds;
            slide = slideSeconds;
            line = linePicture;
            lineRest = rest;
            lineGlow = glow;
            lineSlide = sliding;
        }

        public int State(double time)
        {
            RookRampartTimes.Slide(time, delay, slide, out int state);
            return state;
        }

        protected override void Local(double time, out Vector3 offset, out Quaternion turn)
        {
            float s = RookRampartTimes.Slide(time, delay, slide, out _);
            offset = travel * s;
            turn = Quaternion.identity;
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!ObstacleClock.Authority) return;
            double now = ObstacleClock.Now + phase;
            if (State(now) != 2) return;
            float cycle = 2f * (RookRampartTimes.RookRest + RookRampartTimes.RookGlow + slide);
            long which = (long)Math.Floor((now - delay) / (cycle * .5f));
            if (which != struckSlide) { struck.Clear(); struckSlide = which; }
            float s0 = RookRampartTimes.Slide(now - .05, delay, slide, out _), s1 = RookRampartTimes.Slide(now, delay, slide, out _);
            Vector3 dir = StartRotation * (travel * Mathf.Sign(s1 - s0)).normalized;
            Vector3 centre = transform.position + Vector3.up * 1f;
            int n = Physics.OverlapSphereNonAlloc(centre, reach, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor == null || !actor.CanReceiveObstacle || !struck.Add(actor)) continue;
                actor.AddObstacleHit(dir * push + Vector3.up * lift, knockdown);
            }
        }

        void LateUpdate() => PoseLine(ObstacleClock.Now + phase);

        public void PoseLine(double time)
        {
            if (line == null) return;
            int s = State(time);
            line.enabled = true;
            line.sharedMaterial = s == 1 ? lineGlow : s == 2 ? lineSlide : lineRest;
        }
    }

    // ⑤ The giant rook on its crane: its crown (the floor ridden) goes straight up from the middle wall's edge and straight
    //    across to the inner wall's hoist door, stopping at each end. Built at its low end.
    public sealed class RookCrane : RampartMover
    {
        [SerializeField] float low = 9f, top = 14.5f, zLow, zHigh;
        [SerializeField] Transform trolley;
        [SerializeField] Transform[] chains = new Transform[0];
        [SerializeField] Vector3[] chainTops = new Vector3[0];
        [SerializeField] float beamY = 23f;

        public void Configure(float lowY, float topY, float lowZ, float highZ, Transform trolleyPicture, Transform[] chainPictures, Vector3[] chainLocalTops, float beam)
        {
            low = lowY;
            top = topY;
            zLow = lowZ;
            zHigh = highZ;
            trolley = trolleyPicture;
            chains = chainPictures;
            chainTops = chainLocalTops;
            beamY = beam;
        }

        public void Crown(double time, out float y, out float z) => RookRampartTimes.RookCrane(time, low, top, zLow, zHigh, out y, out z);

        protected override void Local(double time, out Vector3 offset, out Quaternion turn)
        {
            Crown(time, out float y, out float z);
            offset = new Vector3(0f, y - low, z - zLow);
            turn = Quaternion.identity;
        }

        void LateUpdate() => Rig(ObstacleClock.Now + phase);

        public void PoseRig(double time) => Rig(time);

        // The trolley over the crown on the gantry's beam, and a chain from it to each of the crown's lugs.
        void Rig(double time)
        {
            Crown(time, out float y, out float z);
            if (trolley != null) trolley.localPosition = new Vector3(trolley.localPosition.x, trolley.localPosition.y, z);
            for (int k = 0; k < chains.Length && k < chainTops.Length; k++)
            {
                var c = chains[k];
                if (c == null) continue;
                Vector3 from = new Vector3(chainTops[k].x, y + chainTops[k].y, z + chainTops[k].z), to = new Vector3(chainTops[k].x * .4f, beamY - .8f, z);
                c.localPosition = (from + to) * .5f;
                c.localRotation = Quaternion.FromToRotation(Vector3.up, (to - from).normalized);
                c.localScale = new Vector3(c.localScale.x, (to - from).magnitude * .5f, c.localScale.z);
            }
        }
    }

    // ⑤ The giant knight on its crane: leaps from the middle wall's edge over the ditch into the inner wall's hoist door and
    //    back, turning round at each end (R859), never waiting. Its mane, on its back, can be held: a rider swings round with
    //    it as it turns and lets go on its far side. Built at its low end, facing north.
    public sealed class KnightCrane : RampartMover
    {
        [SerializeField] float lowY, highY, lowZ, highZ;
        [SerializeField] Transform trolley, cable;
        [SerializeField] float beamY = 23f, height = 4.6f;

        public void Configure(float yLow, float yHigh, float zLow, float zHigh, Transform trolleyPicture, Transform cablePicture, float beam, float knightHeight)
        {
            lowY = yLow;
            highY = yHigh;
            lowZ = zLow;
            highZ = zHigh;
            trolley = trolleyPicture;
            cable = cablePicture;
            beamY = beam;
            height = knightHeight;
        }

        public void Pose(double time, out float y, out float z, out float facing) =>
            RookRampartTimes.Knight(time, lowY, highY, lowZ, highZ, out y, out z, out facing);

        protected override void Local(double time, out Vector3 offset, out Quaternion turn)
        {
            Pose(time, out float y, out float z, out float facing);
            offset = new Vector3(0f, y - lowY, z - lowZ);
            turn = Quaternion.Euler(0f, facing, 0f);
        }

        void LateUpdate() => Rig(ObstacleClock.Now + phase);

        public void PoseRig(double time) => Rig(time);

        void Rig(double time)
        {
            Pose(time, out float y, out float z, out _);
            if (trolley != null) trolley.localPosition = new Vector3(trolley.localPosition.x, trolley.localPosition.y, z);
            if (cable != null)
            {
                Vector3 from = new Vector3(cable.localPosition.x, y + height - .05f, z), to = new Vector3(cable.localPosition.x, beamY - .8f, z);
                cable.localPosition = (from + to) * .5f;
                cable.localScale = new Vector3(cable.localScale.x, Mathf.Max(.05f, (to.y - from.y) * .5f), cable.localScale.z);
            }
        }
    }

    // ⑥ A length of the king's banner: hangs from its top edge (this object, local -y down the cloth) and billows out from
    //    the wall (local -x) on the gust. Past 35 degrees its face is flatter than a climber can hold (RagdollPawn's 55
    //    degrees from level): whoever is on it lets go by itself.
    public sealed class KingsBanner : RampartMover
    {
        protected override void Local(double time, out Vector3 offset, out Quaternion turn)
        {
            offset = Vector3.zero;
            turn = Quaternion.Euler(0f, 0f, -RookRampartTimes.BannerAngle(time));
        }
    }

    // ⑥ The pennants on the gatehouse's roof: hanging limp, streaming out west a second before every gust (pictures).
    public sealed class RampartPennants : MonoBehaviour, IRampartPreview
    {
        [SerializeField] Transform[] flags = new Transform[0];

        public void Configure(Transform[] flagPictures) => flags = flagPictures;

        void LateUpdate() => Pose(ObstacleClock.Now);

        public void Preview(double time) => Pose(time);

        void Pose(double time)
        {
            bool windy = RookRampartTimes.Windy(time);
            for (int k = 0; k < flags.Length; k++)
            {
                if (flags[k] == null) continue;
                float flutter = windy ? Mathf.Sin((float)RookRampartTimes.Mod(time, 100.0) * 17f + k * 1.7f) * 8f : 0f;
                // Limp: the flag hangs down the pole (turned 80 degrees down); streaming: out west (-x), fluttering.
                flags[k].localRotation = windy ? Quaternion.Euler(flutter, 0f, 0f) : Quaternion.Euler(0f, 0f, 80f);
            }
        }
    }

    // ⑤ The treadwheels: walked round by the workers whenever their crane's load moves (pictures).
    public sealed class Treadwheel : MonoBehaviour, IRampartPreview
    {
        [SerializeField] RookCrane rook;
        [SerializeField] KnightCrane knight;
        [SerializeField] Transform wheel;

        public void Configure(RookCrane rookCrane, KnightCrane knightCrane, Transform wheelPicture)
        {
            rook = rookCrane;
            knight = knightCrane;
            wheel = wheelPicture;
        }

        void LateUpdate() => Pose(ObstacleClock.Now);

        public void Preview(double time) => Pose(time);

        void Pose(double time)
        {
            if (wheel == null) return;
            float turned = 0f;
            if (rook != null) { rook.Crown(time, out float y, out float z); turned = (y + z) * 40f; }
            else if (knight != null) { knight.Pose(time, out float y, out float z, out _); turned = z * 30f; }
            wheel.localRotation = Quaternion.Euler(turned, 0f, 0f);
        }
    }
}
