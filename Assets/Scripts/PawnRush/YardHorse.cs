using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // A royal horse for the Royal Carriage Yard's moving things (R659, out of R532's StableCharger): the stable horse,
    // the horses that pull the roaming carriages and the loose horses roaming the yard. Pictures only - whoever owns it
    // gives it one collider over all of it - nose to local +z, standing on y 0, Length x Width x Height. The body hangs
    // from a pivot over the hind legs, so it can rear; Pose moves it, from numbers the owner reads off the shared clock
    // (so every PC draws the same).
    //
    // R678 (승규 11-15 "실제 말처럼 달리는 연출 자연스러운 달리기 연출", with a picture of two chunky royal horses): a
    // skeleton of pivots the yard's horse model is skinned to (RcyHorse.fbx; YardHorseRig copies the pivots onto its
    // bones, the points below are the bones' heads in horse_model.py) - each leg from the shoulder or the hip through
    // the elbow or stifle, the knee or hock and the fetlock, the neck and the head, the tail in three - and a gait: a
    // trot running into a gallop with speed, each hoof planted while it carries the horse, folded up through its swing.
    // The boxes on the pivots are the blockout, drawn when the yard's models are off.
    public sealed class YardHorse
    {
        public const float Length = 2.6f, Width = .9f, Height = 1.95f;

        public enum Coat { Bay, Grey, Black }

        static Material CoatMaterial(Coat coat) =>
            coat == Coat.Grey ? Arena.Lit("M_YardHorseGrey", Arena.Hex("#D8D1C4"), 0f, .6f)
            : coat == Coat.Black ? Arena.Lit("M_YardHorseBlack", Arena.Hex("#2E2925"), 0f, .55f)
            : Arena.Lit("M_YardHorse", Arena.Hex("#7A4A2A"), 0f, .6f);
        static Material Mane(Coat coat) =>
            coat == Coat.Grey ? Arena.Lit("M_YardHorseManeGrey", Arena.Hex("#8E877C"), 0f, .8f)
            : Arena.Lit("M_YardHorseMane", Arena.Hex("#2B1B12"), 0f, .8f);
        static Material Hoof => Arena.Lit("M_YardHoof", Arena.Hex("#1E1A17"), 0f, .5f);

        // The joints, in the standing horse's root space (x across, the left legs at -x; y up; z forward).
        static readonly Vector3 BodyAt = new Vector3(0f, 1.05f, -.75f);
        static readonly Vector3 NeckAt = new Vector3(0f, 1.33f, .6f), HeadAt = new Vector3(0f, 1.86f, .98f);
        static readonly Vector3[] TailAt = { new Vector3(0f, 1.42f, -.915f), new Vector3(0f, 1.35f, -.97f), new Vector3(0f, 1.24f, -1f) };
        // A leg's joints on the right (x mirrored on the left): the shoulder or hip, the elbow or stifle, the knee or
        // hock, the fetlock.
        static readonly Vector3[] ForeAt = { new Vector3(.2f, 1.2f, .68f), new Vector3(.2f, .93f, .54f), new Vector3(.2f, .5f, .52f), new Vector3(.2f, .245f, .52f) };
        static readonly Vector3[] HindAt = { new Vector3(.21f, 1.3f, -.66f), new Vector3(.21f, .98f, -.5f), new Vector3(.21f, .57f, -.82f), new Vector3(.21f, .245f, -.8f) };
        static readonly string[] ForeJoints = { "Leg", "Elbow", "Knee", "Fetlock" }, HindJoints = { "Leg", "Stifle", "Hock", "Fetlock" };
        static readonly float[] ForeThick = { .22f, .18f, .14f, .13f }, HindThick = { .26f, .19f, .14f, .13f };

        // The legs in order: near (left) fore, off (right) fore, near hind, off hind. Where in the stride each hoof
        // comes down: at a trot the diagonal pairs together, at a gallop hind, hind, fore, fore (leading on the right).
        public static readonly string[] Legs = { "FL", "FR", "HL", "HR" };
        static readonly float[] TrotAt = { .5f, 0f, 0f, .5f }, GallopAt = { .4f, .5f, 0f, .1f };

        public Transform Root { get; private set; }
        Transform body, neck, head;
        Transform[] tail;
        Transform[][] legs;
        float phase, lastTravelled = -1f, stir;
        // R683 (승규 11-16 "말이 뛰는 느낌 ... 엄청 자연스럽게"): a team's two horses a little out of step (the off horse
        // a tenth of a stride behind), not one horse drawn twice.
        float gaitOffset;
        double lastNow = double.NaN;
        // 2026-11-17 sound (승규 "1번 말 마차"): told the moment each hoof comes down in the drawn stride - its place in
        // the stride wraps to 0 - with the leg (0 FL, 1 FR, 2 HL, 3 HR), how much it gallops (0..1) and how much it is
        // moving (0..1). Read off the pose only, so every PC hears the hooves the legs show (RoamingCarriage.Sound.cs).
        public System.Action<int, float, float> Footfall;
        readonly float[] footAt = { -1f, -1f, -1f, -1f };
        readonly double[] footWhen = { -9, -9, -9, -9 };

        // Built under `parent` at `at` (local), nose to the parent's +z. `saddle`: a royal cloth on its back (a ridden
        // or stable horse); off, a harness collar (a carriage horse).
        public static YardHorse Build(Transform parent, Vector3 at, Coat coat = Coat.Bay, bool saddle = true, string name = "Horse")
        {
            var horse = new YardHorse();
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = at;
            horse.Root = root;
            horse.gaitOffset = name == "Horse E" ? .1f : 0f;
            var hide = CoatMaterial(coat);
            var mane = Mane(coat);
            var b = Pivot(root, "Body", BodyAt, Vector3.zero);
            horse.body = b;
            Box(b, BodyAt, "Barrel", new Vector3(0f, 1.15f, -.1f), new Vector3(.7f, .74f, 1.7f), hide);
            Box(b, BodyAt, "Chest", new Vector3(0f, 1.2f, .66f), new Vector3(.62f, .62f, .36f), hide);
            Box(b, BodyAt, "Rump", new Vector3(0f, 1.24f, -.86f), new Vector3(.7f, .6f, .38f), hide);
            if (saddle)
            {
                Box(b, BodyAt, "Saddle cloth", new Vector3(0f, 1.48f, .07f), new Vector3(.8f, .2f, .64f), YardKit.Banner);
                Box(b, BodyAt, "Cloth trim", new Vector3(0f, 1.37f, .07f), new Vector3(.82f, .05f, .66f), YardKit.Gold);
            }
            else
            {
                // A harness: a collar round the chest, a band round the barrel, a pad on the back.
                Box(b, BodyAt, "Collar", new Vector3(0f, 1.2f, .78f), new Vector3(.72f, .3f, .14f), YardKit.WoodDark);
                Box(b, BodyAt, "Collar trim", new Vector3(0f, 1.2f, .86f), new Vector3(.74f, .06f, .03f), YardKit.Gold);
                Box(b, BodyAt, "Girth", new Vector3(0f, 1.12f, .3f), new Vector3(.74f, .8f, .1f), YardKit.WoodDark);
                Box(b, BodyAt, "Pad", new Vector3(0f, 1.57f, .3f), new Vector3(.36f, .08f, .3f), YardKit.Banner);
            }
            horse.tail = new Transform[3];
            Transform up = b;
            var upAt = BodyAt;
            for (int k = 0; k < 3; k++)
            {
                horse.tail[k] = Pivot(up, k == 0 ? "Tail" : "Tail " + (k + 1), TailAt[k], upAt);
                up = horse.tail[k];
                upAt = TailAt[k];
            }
            Box(horse.tail[2], TailAt[2], "Tail", new Vector3(0f, .95f, -1.06f), new Vector3(.15f, .6f, .15f), mane, -10f);
            var n = Pivot(b, "Neck", NeckAt, BodyAt);
            horse.neck = n;
            Box(n, NeckAt, "Neck", new Vector3(0f, 1.6f, .8f), new Vector3(.4f, .75f, .42f), hide, 38f);
            Box(n, NeckAt, "Mane", new Vector3(0f, 1.72f, .7f), new Vector3(.12f, .72f, .14f), mane, 38f);
            var h = Pivot(n, "Head", HeadAt, NeckAt);
            horse.head = h;
            Box(h, HeadAt, "Head", new Vector3(0f, 1.63f, 1.175f), new Vector3(.3f, .3f, .66f), hide, 51f);
            Box(h, HeadAt, "Muzzle", new Vector3(0f, 1.42f, 1.36f), new Vector3(.24f, .2f, .2f), mane, 51f);
            Box(h, HeadAt, "Bridle", new Vector3(0f, 1.5f, 1.28f), new Vector3(.33f, .06f, .06f), YardKit.Gold);
            foreach (float side in new[] { -1f, 1f })
                Box(h, HeadAt, "Ear", new Vector3(.08f * side, 2f, 1f), new Vector3(.07f, .16f, .07f), hide);
            Box(h, HeadAt, "Plume", new Vector3(0f, 2.05f, .92f), new Vector3(.06f, .22f, .06f), YardKit.Banner);
            horse.legs = new Transform[4][];
            for (int i = 0; i < 4; i++)
            {
                bool fore = i < 2;
                float side = i % 2 == 0 ? -1f : 1f;
                var joints = fore ? ForeAt : HindAt;
                var chain = new Transform[4];
                up = b;
                upAt = BodyAt;
                for (int k = 0; k < 4; k++)
                {
                    var p = Side(joints[k], side);
                    chain[k] = Pivot(up, (fore ? ForeJoints : HindJoints)[k] + " " + Legs[i], p, upAt);
                    up = chain[k];
                    upAt = p;
                }
                // A box down each bone, from its joint to the next (the last to the hoof), and the hoof.
                for (int k = 0; k < 4; k++)
                {
                    var top = Side(joints[k], side);
                    var bottom = k < 3 ? Side(joints[k + 1], side) : new Vector3(top.x, .12f, top.z + .04f);
                    float thick = (fore ? ForeThick : HindThick)[k];
                    YardKit.Box(chain[k], "Leg", (top + bottom) * .5f - top, new Vector3(thick, (top - bottom).magnitude + .04f, thick),
                                hide, Solid.None, Quaternion.FromToRotation(Vector3.up, top - bottom));
                }
                var fetlock = Side(joints[3], side);
                Box(chain[3], fetlock, "Hoof", new Vector3(fetlock.x, .07f, fetlock.z + .05f), new Vector3(.2f, .14f, .22f), Hoof);
                horse.legs[i] = chain;
            }
            return horse;
        }

        static Vector3 Side(Vector3 v, float side) => new Vector3(v.x * side, v.y, v.z);

        static Transform Pivot(Transform parent, string name, Vector3 at, Vector3 parentAt)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = at - parentAt;
            return t;
        }

        // A box at `at` in root space under a pivot standing at `pivotAt`, tipped forward by `tilt` degrees.
        static void Box(Transform pivot, Vector3 pivotAt, string name, Vector3 at, Vector3 size, Material material, float tilt = 0f) =>
            YardKit.Box(pivot, name, at - pivotAt, size, material, Solid.None, Quaternion.Euler(tilt, 0f, 0f));

        // How far into a rear (0 standing .. 1 up on its hind legs and down again) from how far into a warning (0..1),
        // as the stable horse rears: up in the first 0.3 of it, down over the last quarter.
        public static float Rear(float warned) =>
            warned < 0f ? 0f : Smooth(warned / .3f) * (1f - Smooth((warned - .75f) / .25f));

        // Rearing by `rear` (0..1), a stride with the distance `travelled` (m, running round at 1000) at `speed` of
        // `topSpeed` m/s, still when it stands; `now` (the shared clock) tosses the head while it rears and swishes the
        // tail. Every angle below turns about x: a positive one swings the lower end of a leg's part backward, tips
        // the neck and the head down, lifts the tail.
        // R683: `turn` (degrees a second, + to the right) - turning, the horse leans into the turn and looks where it goes.
        // (The stable's charger: it only runs forward, its stride from `travelled`.)
        public void Pose(double now, float rear, float speed, float topSpeed, float travelled, float turn = 0f)
        {
            float advance = 0f;
            if (lastTravelled >= 0f)
            {
                advance = travelled - lastTravelled;
                if (advance < 0f) advance += 1000f;
                if (advance >= 10f) advance = 0f;
            }
            lastTravelled = travelled;
            Drive(now, rear, new Vector3(0f, 0f, speed), topSpeed, turn, 0f, advance);
        }

        // R686 (승규 11-17 "말 도는 것을 진짜 자연스럽게 표현 해 봐 지금 있는 말 시스템 바꿔도 되니까"): a team turning on
        // the spot carries its pair sideways round its middle at up to ~3 m/s, and they used to slide there with their legs
        // swinging only fore and aft. Now each hoof steps the way the ground goes under it: `velocity` is the horse's own
        // way over the ground (its frame: x to its right, z ahead, m/s) and `turn` how fast it turns (degrees a second, +
        // to the right), so each hoof's way is the horse's plus the turn's about the horse's middle. A planted hoof stays
        // where it came down while the leg sweeps over it, forward and sideways; a leg swinging across the other one
        // crosses in front of it, folded high; the horse sets its body a little toward the way it goes (as far as the
        // traces let it) and looks into the turn, `lookAhead` degrees (+ right) being how far it still has to turn. The
        // stride runs with the ground the hooves cover, shorter stepping sideways, so the legs never skate.
        public void Move(double now, float rear, Vector3 velocity, float topSpeed, float turn, float lookAhead = 0f) =>
            Drive(now, rear, velocity, topSpeed, turn, lookAhead, -1f);

        // Where each hoof stands at rest (root space): fore FL FR, hind HL HR.
        static readonly Vector3[] HoofAt = { new Vector3(-.2f, 0f, .57f), new Vector3(.2f, 0f, .57f), new Vector3(-.21f, 0f, -.75f), new Vector3(.21f, 0f, -.75f) };
        // A stride stepping straight sideways (m): a horse side-passes in short crossing steps.
        const float SideStride = 1.2f;
        // How far a harnessed body turns toward the way it goes (degrees, about its hind pivot: the chest moves ~17 cm).
        const float CrabMost = 7f;
        readonly Vector2[] hoofWay = new Vector2[4];

        // `advance`: the ground covered this frame for the stride (m), or below 0 to take it from the hooves' speed.
        void Drive(double now, float rear, Vector3 velocity, float topSpeed, float turn, float lookAhead, float advance)
        {
            if (body == null) return;
            float dt = double.IsNaN(lastNow) ? 0f : Mathf.Clamp((float)(now - lastNow), 0f, .1f);
            lastNow = now;
            // Each hoof's way over the ground: the horse's, and the turn's about its middle (w x r).
            float spin = turn * Mathf.Deg2Rad, pace = 0f, across = 0f;
            for (int i = 0; i < 4; i++)
            {
                var h = HoofAt[i];
                var v = new Vector2(velocity.x + spin * h.z, velocity.z - spin * h.x);
                hoofWay[i] = v;
                pace += v.magnitude * .25f;
                across += Mathf.Abs(v.x) * .25f;
            }
            float share = Mathf.Clamp01(Mathf.Abs(velocity.z) / Mathf.Max(.1f, topSpeed));
            // How much it is running, eased so it does not snap between a gallop and standing.
            stir = Mathf.MoveTowards(stir, pace > .2f ? 1f : 0f, dt * 2.5f);
            float gallop = Smooth((share - .35f) / .35f);
            float side = pace > 1e-3f ? Mathf.Clamp01(across / pace) * (1f - gallop) : 0f;
            // The stride with the ground it covers, so a hoof stays where it came down while the horse goes over it.
            float stride = Mathf.Lerp(Mathf.Lerp(1.7f, 3.3f, gallop), SideStride, side);
            if (advance < 0f) advance = pace * dt;
            phase = (phase + advance / stride) % 1f;
            // The share of a stride each hoof is down, and how far its leg sweeps meanwhile (the ground covered then,
            // over the leg's length), along and across the horse for each hoof's own way.
            float duty = Mathf.Lerp(Mathf.Lerp(.45f, .32f, gallop), .5f, side);
            float reach = stride * duty * .5f / 1.25f;
            for (int i = 0; i < 4; i++)
            {
                var leg = legs[i];
                if (leg == null || leg[0] == null) continue;
                bool fore = i < 2;
                float legSide = i % 2 == 0 ? -1f : 1f;
                var way = pace > 1e-3f ? hoofWay[i] / pace : new Vector2(0f, 1f);
                float sweep = Mathf.Asin(Mathf.Clamp(reach * way.y, -.9f, .9f)) * Mathf.Rad2Deg * stir;
                float sweepSide = Mathf.Asin(Mathf.Clamp(reach * way.x, -.9f, .9f)) * Mathf.Rad2Deg * stir;
                float u = Mathf.Repeat(phase + gaitOffset + Mathf.Lerp(TrotAt[i], GallopAt[i], gallop), 1f);
                // Sound: the hoof down (its place wraps past 0), at most once in 0.12 s a leg (a gallop's stride is 0.33 s).
                if (Footfall != null && stir > .2f && footAt[i] > .75f && u < .25f && now - footWhen[i] > .12)
                {
                    footWhen[i] = now;
                    Footfall(i, gallop, stir);
                }
                footAt[i] = u;
                float top, mid, low, fetlock, roll;
                if (u < duty)
                {
                    // Down: the leg sweeps back under the horse (and across it, stepping sideways: a positive roll puts
                    // the hoof to the horse's right), the fetlock sinking and the hind leg giving under the weight halfway
                    // (R686: its stifle and hock flex - they used to open, which slid the planted hind hoof ~0.3 m a step).
                    float s = u / duty, load = Mathf.Sin(s * Mathf.PI) * stir;
                    top = Mathf.Lerp(-sweep, sweep, s);
                    roll = Mathf.Lerp(sweepSide, -sweepSide, s);
                    mid = fore ? 0f : 6f * load;
                    low = fore ? 0f : -8f * load;
                    fetlock = -Mathf.Lerp(16f, 24f, gallop) * load;
                }
                else
                {
                    // Up: it swings forward folded, a fore knee folding early and straightening for the reach, a hind
                    // leg drawn up under the belly. Swinging across under the horse toward its other leg (stepping
                    // sideways), it crosses in front of that leg, folded higher, instead of through it.
                    float w = (u - duty) / (1f - duty);
                    float fold = Mathf.Sin(w * Mathf.PI) * stir, early = Mathf.Sin(Mathf.Clamp01(w * 1.25f) * Mathf.PI) * stir;
                    float cross = sweepSide * legSide < 0f ? Mathf.Clamp01(Mathf.Abs(sweepSide) / 8f) * fold : 0f;
                    top = Mathf.Lerp(sweep, -sweep, Smooth(w)) - 14f * cross;
                    roll = Mathf.Lerp(-sweepSide, sweepSide, Smooth(w));
                    if (fore)
                    {
                        mid = -(12f + 18f * gallop) * fold - 6f * cross;
                        low = (65f + 40f * gallop) * early + 20f * cross;
                        fetlock = (25f + 15f * gallop) * early + 10f * cross;
                    }
                    else
                    {
                        // R686: the stifle flexes (the gaskin's lower end back) and the hock folds the cannon up under
                        // it - the hind hoof used to clear the ground by 8 cm, now ~23 cm, as a horse's does.
                        mid = (20f + 10f * gallop) * fold + 6f * cross;
                        low = -(55f + 25f * gallop) * fold - 15f * cross;
                        fetlock = (30f + 15f * gallop) * fold + 10f * cross;
                    }
                }
                if (fore)
                {
                    // Rearing, the fore legs fold up (and come in under the chest).
                    top -= 45f * rear;
                    mid -= 25f * rear;
                    low += 95f * rear;
                    fetlock += 30f * rear;
                    roll *= 1f - rear;
                }
                else top += 32f * rear;         // the hind legs stay under it as the body tips back
                leg[0].localRotation = Quaternion.Euler(top, 0f, roll);
                leg[1].localRotation = Quaternion.Euler(mid, 0f, 0f);
                leg[2].localRotation = Quaternion.Euler(low, 0f, 0f);
                leg[3].localRotation = Quaternion.Euler(fetlock, 0f, 0f);
            }
            // The body rises over the legs between footfalls (twice a stride at a trot, once at a gallop) and at a
            // gallop rocks, up in front as the hind legs push, down as the fore legs land.
            // R683: more of it at a gallop (the body rose 5 cm and rocked 4 degrees: it read as gliding), and a lean
            // into a turn.
            float ph = phase + gaitOffset;
            float bob = Mathf.Lerp(.5f - .5f * Mathf.Cos(4f * Mathf.PI * ph), .5f - .5f * Mathf.Cos(2f * Mathf.PI * (ph - .15f)), gallop);
            float rock = Mathf.Sin(2f * Mathf.PI * (ph - .3f)) * gallop * stir;
            float lean = Mathf.Clamp(-turn * .035f, -5f, 5f);
            // R686: stepping sideways, the chest leads the way it goes (about the hind pivot), as far as the traces let it.
            float crab = Mathf.Clamp(Mathf.Atan2(velocity.x, Mathf.Abs(velocity.z) + 1.5f) * Mathf.Rad2Deg, -CrabMost, CrabMost) * stir * (1f - gallop);
            body.localPosition = BodyAt + new Vector3(0f, (.025f + .075f * gallop) * bob * stir, 0f);
            body.localRotation = Quaternion.Euler(-32f * rear + 6.5f * rock, crab, lean);
            float clock = (float)(now % 1000.0);
            if (neck != null)
            {
                // The neck stretches out and nods with the stride at a gallop; standing, it breathes.
                float nod = Mathf.Sin(2f * Mathf.PI * (ph - .1f)) * (2.5f + 8.5f * gallop) * stir;
                float toss = 8f * Mathf.Sin(clock * 9f) * rear;
                float idle = 2f * Mathf.Sin(clock * .7f) * (1f - stir);
                // R686: into the turn, and ahead to where it is still to face (the head leads the body round).
                float look = Mathf.Clamp(turn * .09f + .45f * lookAhead + .6f * crab, -24f, 24f);
                neck.localRotation = Quaternion.Euler(-12f * rear + toss + 6f * gallop * stir + nod - 5f * rock + idle, look, 0f);
                if (head != null) head.localRotation = Quaternion.Euler(-.5f * nod - 6f * rear, .6f * look, 0f);
            }
            if (tail != null && tail[0] != null)
            {
                float swish = Mathf.Sin(clock * 1.9f) * (6f - 3f * stir);
                float lift = (12f + 10f * gallop) * stir;
                float wave = 6f * Mathf.Sin(2f * Mathf.PI * (ph - .2f)) * stir;
                swish -= Mathf.Clamp(turn * .06f, -10f, 10f);    // the tail swings out of a turn
                tail[0].localRotation = Quaternion.Euler(.6f * lift + 32f * rear, swish, 0f);
                tail[1].localRotation = Quaternion.Euler(.3f * lift + .5f * wave, .8f * swish, 0f);
                tail[2].localRotation = Quaternion.Euler(.2f * lift + wave, .6f * swish, 0f);
            }
        }

        static float Smooth(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }
    }
}
