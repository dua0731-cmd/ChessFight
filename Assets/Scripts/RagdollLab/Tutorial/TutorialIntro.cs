using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The tutorial's opening (R839, user 11-25: "시작 초기에 독수리가 날 때 폰이 독수리 위에 올라가서 맵 안에 우스광 스럽게 착지
    /// 하는 연출"): the royal eagle (Assets/Art/Tutorial/Models/TU_Eagle.fbx, ArtSource/Tutorial/tu_eagle_v1.py) flies in
    /// over the south wall with the player's pawn standing on its back, comes down too fast, stumbles, belly-flops and
    /// skids across the yard; the pawn is thrown off over its head and tumbles to the start. The eagle lies winded,
    /// lifts and shakes its head, gets up, ruffles itself, and flaps off over the north wall. The first lesson waits
    /// until the pawn is back on its feet.
    ///
    /// The motion is written after how a large eagle moves (user 11-25: "비행을 4초 정도 늘려주고 날개짓 착지 했을 때
    /// 애니메이션을 극사실적으로"): a long approach of flapping bursts and glides, banking with the turn; each beat a slower
    /// power stroke with the wing stretched and its leading edge down, then a quicker recovery with the wrist folded and
    /// the tip trailing; the body rising on the down stroke; the head held level while the body moves under it; the
    /// flare with the body reared back, deep braking beats, the tail spread down and the legs thrown forward; the feet
    /// taking the weight, two running hops, the trip, the chest hitting and sliding to a stop; panting, the dazed head,
    /// the push up on the wings, the ruffle, the crouch and the leap into the first down stroke.
    ///
    /// The eagle has a skeleton (user 11-25: "독수리 뼈대 넣어서 더 사실적으로": neck, head, tail, and for each side upper
    /// arm, forearm, hand, thigh, shank and foot) and no animation clips: this script turns the bones, so the wings fold
    /// at the elbow and wrist and the legs bend under its weight.
    /// Nothing here collides. The pawn is held on the back by teleporting it there every frame (its keys are off, the
    /// spawner is disabled); the throw is a knockdown and a launch, so its fall is the game's own ragdoll.
    /// No key does anything while it plays (user 11-25; the jump key's skip and its screen line are gone, the Esc menu
    /// is off until the lessons start). In batch mode (the automatic checks) there is no opening.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    public sealed partial class TutorialIntro : MonoBehaviour
    {
        /// <summary>The opening is running: the lessons and their screen wait (TutorialCourse).</summary>
        public static bool Playing { get; private set; }
        /// <summary>The pawn is still on the eagle's back (the lessons' screen waits).</summary>
        public static bool Riding { get; private set; }

        // Seconds: the approach (a lap round the yard), the landing (feet, hops, belly slide), lying winded, getting up, leaving.
        const float Approach = 10f, Landing = 1.7f, Dazed = 2.5f, Rise = 1.6f, Leave = 3.4f;
        const float FlareFrom = 8.6f;        // into the approach: the body rears back and the beats turn into braking
        const float TripAt = 0.6f;           // into the landing: the chest goes down; the pawn is thrown just after
        const float LessonsAfter = 1.5f;     // after the throw: the pawn is up again
        const float StandHeight = 0.84f;     // the body's middle above the floor standing on bent legs
        const float SquatHeight = 0.64f;     // with the legs folded under it
        const float BellyHeight = 0.5f;      // lying on its belly
        const float CruiseBeat = 2.3f, BrakeBeat = 4.3f;   // wing beats a second
        const float DownShare = 0.56f;       // the power stroke's share of a beat
        const float RunSpeed = 10.5f, SlideSpeed = 8f, SlideDrag = 4f;   // the landing: m/s at the feet, at the trip, and the slide's decay
        static readonly Vector3 SeatLocal = new Vector3(0f, 0.5f, -0.05f);   // on the back between the wings (forward z, up y)

        // The skeleton (TU_Eagle.fbx, ArtSource/Tutorial/tu_eagle_v1.py bones()): each bone's turn is given about the
        // model's own axes in its rest pose (wings spread, legs straight down), a child's on top of its parent's.
        enum B { Neck1, Neck2, Head, Tail, HumerusL, ForearmL, HandL, ThighL, ShankL, FootL, HumerusR, ForearmR, HandR, ThighR, ShankR, FootR, Count }
        static readonly string[] BoneNames = { "Neck1", "Neck2", "Head", "Tail", "Humerus_L", "Forearm_L", "Hand_L", "Thigh_L", "Shank_L", "Foot_L",
                                               "Humerus_R", "Forearm_R", "Hand_R", "Thigh_R", "Shank_R", "Foot_R" };
        readonly Transform[] bone = new Transform[(int)B.Count];
        readonly Quaternion[] rest = new Quaternion[(int)B.Count];
        Vector3 fwd = Vector3.forward, up = Vector3.up, left = Vector3.left;   // the model's own axes in this object's space
        Quaternion modelToLook = Quaternion.identity;
        PlaytestSpawner spawner;
        RagdollPawn pawn;
        float started = -1f;
        Vector3 touchdown, stop, face, side;
        readonly List<Vector3> inPath = new List<Vector3>();
        readonly List<float> inLength = new List<float>();
        Vector3[] outPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Playing = Riding = false;

        void Awake()
        {
            bool whole = !Application.isBatchMode;
            if (whole)
            {
                var all = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < BoneNames.Length; i++)
                {
                    foreach (var t in all)
                        if (t.name == BoneNames[i]) { bone[i] = t; break; }
                    whole &= bone[i] != null;
                }
            }
            if (!whole)
            {
                gameObject.SetActive(false);
                return;
            }
            // It flies far from where the mesh was bound: its bounds follow the bones.
            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
            // The model's axes, from where its joints are (the FBX comes in with Blender's own axes under this object:
            // forward -Y, up +Z, left -X; this only reads the signs, so another import still flies head first, back up).
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Vector3 P(B b) => bone[(int)b].position;
            Vector3 toHead = P(B.Head) - P(B.Tail), toLeft = P(B.HumerusL) - P(B.HumerusR);
            fwd = Mathf.Abs(toHead.z) >= Mathf.Abs(toHead.y) ? new Vector3(0f, 0f, Mathf.Sign(toHead.z)) : new Vector3(0f, Mathf.Sign(toHead.y), 0f);
            left = new Vector3(Mathf.Sign(toLeft.x), 0f, 0f);
            Vector3 toBack = (P(B.HumerusL) + P(B.HumerusR)) * 0.5f - (P(B.ThighL) + P(B.ThighR)) * 0.5f;
            up = fwd.z != 0f ? new Vector3(0f, Mathf.Sign(toBack.y), 0f) : new Vector3(0f, 0f, Mathf.Sign(toBack.z));
            modelToLook = Quaternion.Inverse(Quaternion.LookRotation(fwd, up));
            for (int i = 0; i < rest.Length; i++) rest[i] = Quaternion.Inverse(transform.rotation) * bone[i].rotation;

            // The landing runs along the yard's middle line toward the start and stops just short of it.
            face = Quaternion.Euler(0f, TutorialLayout.StartYaw, 0f) * Vector3.forward;
            side = Vector3.Cross(Vector3.up, face);
            float run = RunSpeed * TripAt - 0.5f * (RunSpeed - SlideSpeed) * TripAt;   // on its feet, slowing from RunSpeed to SlideSpeed
            float slide = SlideSpeed / SlideDrag * (1f - Mathf.Exp(-SlideDrag * (Landing - TripAt)));
            stop = TutorialLayout.Start - face * 1.2f;
            touchdown = stop - face * (run + slide);
            // A lap round the yard (user 11-25: "멀리서 날아오지 말고 맵 주위에서 비행"): it is already over the east side when
            // the scene opens, circles north, west and south over the moat inside the courtyard's rim, coming down as it
            // goes, and turns in over the south end onto a straight, shallow final. Points from the yard's middle
            // (east, north, height); a spline through them, walked at an even pace that slows into the flare.
            Vector3 mid = new Vector3(0f, 0f, -1f);
            Vector3 At(float east, float north, float high) => mid + side * east + face * north + Vector3.up * high;
            var way = new[]
            {
                At(38f, -27f, 22f),
                At(34f, -6f, 21.5f), At(28f, 19f, 21f), At(7f, 31.5f, 20f), At(-19f, 27f, 18.5f), At(-33f, 8f, 16.5f),
                At(-31.5f, -14f, 13.5f), At(-19f, -29f, 10f),
                touchdown - face * 17.5f - side * 5f + Vector3.up * 6.6f,
                touchdown - face * 8f - side * 0.4f + Vector3.up * 3.2f,
                touchdown + Vector3.up * (StandHeight + 0.3f),
                touchdown + face * 7f + Vector3.up * 0.2f,
            };
            for (int i = 1; i < way.Length - 2; i++)
                for (int k = 0; k < 24; k++)
                    Add(CatmullRom(way[i - 1], way[i], way[i + 1], way[i + 2], k / 24f));
            Add(way[way.Length - 2]);
            Vector3 lift = stop + Vector3.up * StandHeight;
            outPath = new[] { lift, lift + face * 5f - side * 1.5f + Vector3.up * 4.5f, lift + face * 26f - side * 22f + Vector3.up * 17f,
                              lift + face * 74f - side * 64f + Vector3.up * 36f };
            Playing = Riding = true;
            Pose(0f);
        }

        void Add(Vector3 p)
        {
            inLength.Add(inPath.Count == 0 ? 0f : inLength[inLength.Count - 1] + Vector3.Distance(inPath[inPath.Count - 1], p));
            inPath.Add(p);
        }

        static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u) =>
            0.5f * (2f * b + (c - a) * u + (2f * a - 5f * b + 4f * c - d) * u * u + (3f * b - a - 3f * c + d) * u * u * u);

        // The approach path at a share (0..1) of its length.
        Vector3 Along(float share)
        {
            float at = Mathf.Clamp01(share) * inLength[inLength.Count - 1];
            int hi = inLength.BinarySearch(at);
            if (hi < 0) hi = ~hi;
            hi = Mathf.Clamp(hi, 1, inPath.Count - 1);
            float span = inLength[hi] - inLength[hi - 1];
            return Vector3.Lerp(inPath[hi - 1], inPath[hi], span > 1e-5f ? (at - inLength[hi - 1]) / span : 0f);
        }

        // How far along the approach at t: an even pace that eases down to under half of it at the floor.
        static float Pace(float t)
        {
            float x = Mathf.Clamp01(t / Approach);
            return (x - 0.29f * x * x) / 0.71f;
        }

        void OnDestroy()
        {
            if (started >= 0f || Playing) Playing = Riding = false;
        }

        void Update()
        {
            if (started < 0f)
            {
                // Wait for the spawner's pawn, then take its keys away for the ride.
                if (spawner == null) spawner = FindFirstObjectByType<PlaytestSpawner>();
                if (spawner == null || !(spawner.Driver is RagdollDriver driver) || driver.Pawn == null) return;
                pawn = driver.Pawn;
                spawner.enabled = false;
                started = Time.time;
                // The view for the ride: fixed behind the eagle's head (TutorialIntroCamera, after the orbit camera).
                if (spawner.Orbit != null) spawner.Orbit.gameObject.AddComponent<TutorialIntroCamera>().intro = this;
            }
            // R841, coming from the intro: the opening's clock waits while the scene transition still covers the screen
            // (the eagle held at the start of its lap, the pawn already on its back), so none of the flight is missed.
            if (SceneTransition.Busy) started = Time.time;
            float t = Time.time - started;
            // No key does anything while the opening plays (user 11-25 "이 연출 장면에서는 어떤 키를 눌러도 작용 할 수 없게 해":
            // the jump key's skip left the pawn at the start unable to move): no skip, and the Esc menu is off.
            Pose(t);
            SoundStep(t);
            float throwAt = Approach + TripAt + 0.04f;
            if (Riding)
            {
                if (t >= throwAt) Throw();
                else Seat();
            }
            else if (Playing && t >= throwAt + LessonsAfter) Playing = false;
            HoldMenu(Playing);
            if (t >= Approach + Landing + Dazed + Rise + Leave) gameObject.SetActive(false);
        }

        // The scene's Esc menu (GameMenu, installed by the spawner) is switched off while the opening plays.
        GameMenu menu;

        void HoldMenu(bool hold)
        {
            if (menu == null) menu = FindFirstObjectByType<GameMenu>();
            if (menu == null) return;
            if (hold && GameMenu.IsOpen) GameMenu.Close();
            menu.enabled = !hold;
        }

        void OnDisable()
        {
            if (menu != null) menu.enabled = true;
        }

        // Also after the physics step, so the pawn never drifts off the seat between frames.
        void FixedUpdate()
        {
            if (Riding && started >= 0f) Seat();
        }

        /// <summary>The ride's view (user 11-25: "비행 할 때 카메라도 독수리 머리 부분에 맞춰서 고정"): behind and a little above
        /// the eagle's head, looking on past it the way the eagle is heading - level, whatever the body's bank and pitch,
        /// with the pawn on its back in the foreground. The mouse does not turn it.</summary>
        /// Then (11-25 "카메라를 맵쪽이랑 독수리 머리 사이에 두고 마우스로 약간만 움직일 수 있게"): the camera stands between the
        /// yard's middle and the eagle's head, a little ahead of it and above, looking back at the head and the pawn
        /// riding behind it; on the final straight that is in front of the eagle, so it lands toward the camera.
        /// yawOffset and pitchOffset (degrees, kept small by TutorialIntroCamera) swing it round what it looks at.
        public void ViewFromHead(float yawOffset, float pitchOffset, out Vector3 position, out Quaternion rotation)
        {
            Vector3 at = bone[(int)B.Head].position;
            Vector3 heading = Heading();
            Vector3 inward = new Vector3(0f, 0f, -1f) - at;
            inward.y = 0f;
            inward = inward.sqrMagnitude > 1f ? inward.normalized : heading;
            Vector3 target = Vector3.Lerp(at, RiderSeat(), 0.35f);
            // 11-25 "카메라 방향 반대로 했어": the camera is on the far side of the eagle from the yard and behind it, so it
            // looks over the rider and the head toward the map; on the final straight that is from behind, into the yard.
            Vector3 offset = Quaternion.AngleAxis(yawOffset, Vector3.up) * (-inward * 4.2f - heading * 3.2f + Vector3.up * 1.5f);
            offset = Quaternion.AngleAxis(pitchOffset, Vector3.Cross(Vector3.up, -offset).normalized) * offset;
            position = target + offset;
            rotation = Quaternion.LookRotation(target - position, Vector3.up);
        }

        Vector3 Heading()
        {
            Vector3 heading = transform.rotation * fwd;
            heading.y = 0f;
            return heading.sqrMagnitude > 1e-4f ? heading.normalized : face;
        }

        // Where the pawn's hips are: on the back between the wings, sunk a little into the feathers.
        Vector3 RiderSeat()
        {
            Quaternion body = transform.rotation * Quaternion.Inverse(modelToLook);
            return transform.TransformPoint(Quaternion.Inverse(modelToLook) * SeatLocal) + body * Vector3.up * (0.1f + riderDrop);
        }

        // The player's own camera again, looking the way the pawn was thrown.
        void GiveViewBack()
        {
            var orbit = spawner != null ? spawner.Orbit : null;
            if (orbit == null) return;
            orbit.FlyAt(orbit.transform.position, TutorialLayout.StartYaw, 14f);
            orbit.StopFly();
        }

        void Seat()
        {
            if (pawn == null) return;
            Vector3 heading = Heading();
            Vector3 seat = RiderSeat();
            pawn.Teleport(seat, heading);
            // The pawn leans and banks with the eagle under it, most of the way.
            Quaternion body = transform.rotation * Quaternion.Inverse(modelToLook);
            Quaternion tilt = Quaternion.Slerp(Quaternion.identity, body * Quaternion.Inverse(Quaternion.LookRotation(heading, Vector3.up)), 0.8f);
            PoseRider(seat, heading, tilt);
        }

        // The rider (user 11-25: "폰도 독수리를 타는 것 같은 연출"): the pawn sits astride the eagle's back instead of standing
        // on it - thighs spread round the body with the knees forward, shins down its sides, leaning into the wind with
        // both hands out on the neck feathers, its head up and turned into the turn - and it is jolted by the ride:
        // pressed down into the seat on each wing beat, thrown back with its arms in the air when the eagle rears to
        // brake, bounced and flailing through the landing run. The teleport has just put every body part in its bind
        // pose facing ahead; this turns them from there, down the pawn's own chain (hips, chest, head, arms, legs),
        // each about its joint.
        float riderLean, riderArms, riderFlail, riderLook, riderDrop;
        readonly Vector3[] bindAt = new Vector3[RagdollPawn.Count], pivot = new Vector3[RagdollPawn.Count], nowAt = new Vector3[RagdollPawn.Count];
        readonly Quaternion[] bindTurn = new Quaternion[RagdollPawn.Count], turned = new Quaternion[RagdollPawn.Count];

        void PoseRider(Vector3 hipsAt, Vector3 ahead, Quaternion tilt)
        {
            var bodies = pawn.bodies;
            var joints = pawn.joints;
            Vector3 right = Vector3.Cross(Vector3.up, ahead);
            for (int i = 0; i < RagdollPawn.Count; i++)
            {
                if (bodies[i] == null) return;
                var t = bodies[i].transform;
                bindAt[i] = t.position;
                bindTurn[i] = t.rotation;
                pivot[i] = i > 0 && joints[i] != null ? t.TransformPoint(joints[i].anchor) : bindAt[i];
            }
            float time = Time.time;
            Quaternion Forward(float degrees) => Quaternion.AngleAxis(degrees, right);   // > 0: leans forward; swings a hanging limb back
            for (int i = 0; i < RagdollPawn.Count; i++)
            {
                float out_ = Mathf.Sign(Vector3.Dot(pivot[i] - bindAt[0], right));       // +1: the right side's limb
                float flail = riderFlail * 28f * Mathf.Sin(time * 17f + i * 1.7f);
                Quaternion local;
                switch (i)
                {
                    case 0: local = Forward(riderLean * 0.4f); break;                                    // hips
                    case 1: local = Forward(riderLean * 0.6f + 8f); break;                                 // chest
                    case 2: local = Quaternion.AngleAxis(riderLook, Vector3.up) * Forward(-riderLean - 6f); break;   // head: level, looking round
                    case 3: case 5: local = Quaternion.AngleAxis(-out_ * 10f, Vector3.up) * Forward(riderArms + flail); break;   // upper arms out ahead
                    case 4: case 6: local = Forward(-16f + flail * 0.5f); break;                           // hands
                    case 7: case 9: local = Quaternion.AngleAxis(out_ * 55f, ahead) * Forward(-38f + flail * 0.3f); break;   // thighs astride
                    default: local = Quaternion.AngleAxis(-out_ * 35f, ahead) * Forward(48f); break;       // shins down the sides
                }
                int parent = RagdollPawn.ParentOf[i];
                turned[i] = parent < 0 ? tilt * local : turned[parent] * local;
                Vector3 joint = parent < 0 ? hipsAt : nowAt[parent] + turned[parent] * (pivot[i] - bindAt[parent]);
                nowAt[i] = joint + turned[i] * (bindAt[i] - pivot[i]);
                Quaternion turn = turned[i] * bindTurn[i];
                bodies[i].transform.SetPositionAndRotation(nowAt[i], turn);
                bodies[i].position = nowAt[i];
                bodies[i].rotation = turn;
            }
        }

        // Once more after the pawn's own late work, before its look is drawn (RagdollVisualSync runs later).
        void LateUpdate()
        {
            if (Riding && started >= 0f) Seat();
        }

        // The chest goes down: the pawn goes on over the eagle's head, limp, and lands near the start.
        void Throw()
        {
            Riding = false;
            if (spawner != null) spawner.enabled = true;
            GiveViewBack();
            if (pawn == null) return;
            pawn.Knockdown("튜토리얼 착지", 1.0f);
            pawn.Launch(face * (SlideSpeed - 1.5f) + Vector3.up * 5.5f);
        }

        static Vector3 Curve(Vector3[] p, float u)
        {
            float v = 1f - u;
            return v * v * v * p[0] + 3f * v * v * u * p[1] + 3f * v * u * u * p[2] + u * u * u * p[3];
        }

        static Vector3 CurveDir(Vector3[] p, float u)
        {
            float v = 1f - u;
            return (3f * v * v * (p[1] - p[0]) + 6f * v * u * (p[2] - p[1]) + 3f * u * u * (p[3] - p[2])).normalized;
        }

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        // 1 between a and b, easing in and out over fade seconds on each side.
        static float Window(float x, float a, float b, float fade = 0.4f) => Smooth(a - fade, a, x) * (1f - Smooth(b, b + fade, x));

        // A damped swing: 1 at x = 0, ringing at hertz and dying at decay.
        static float Ring(float x, float hertz, float decay) => x < 0f ? 0f : Mathf.Exp(-decay * x) * Mathf.Cos(x * hertz * Mathf.PI * 2f);

        // Everything that turns, in degrees.
        //   Wing: the upper arm at the shoulder - raise (up from level), sweep (back toward the tail), twist (leading edge
        //   down); the forearm at the elbow - elbow (back; a folding wing brings it forward, so negative) and elbowRaise;
        //   the hand at the wrist - handRaise, handSweep (back), handTwist.
        //   Leg: thigh, shank and foot, each forward from the one above (a standing bird: thigh forward, shank back).
        //   Neck: neckPitch and neckYaw shared along the neck and head; curl draws the neck up and the head down into
        //   the S a perched bird holds; headPitch (nose down), headYaw and headRoll are the head's own.
        struct Bird
        {
            public float raiseL, raiseR, sweep, twist, elbow, elbowRaise, handRaiseL, handRaiseR, handSweep, handTwist;
            public float thighL, thighR, shankL, shankR, footL, footR;
            public float neckPitch, neckYaw, curl, headPitch, headYaw, headRoll, tailPitch, tailRoll;

            public void Legs(float thigh, float shank, float foot) { thighL = thighR = thigh; shankL = shankR = shank; footL = footR = foot; }
        }

        // Leg shapes (thigh, shank, foot): tucked against the belly in flight, thrown forward with the toes open to land,
        // standing, folded under it, and left trailing behind it on its belly.
        static readonly Vector3 Tucked = new Vector3(62f, -118f, 50f), Reaching = new Vector3(58f, -22f, -30f), Standing = new Vector3(34f, -56f, 22f),
                                Squatting = new Vector3(62f, -112f, 50f), Trailing = new Vector3(-62f, -8f, -35f), Pushing = new Vector3(-30f, -5f, -40f);

        static void Legs(ref Bird b, Vector3 a, Vector3 c, float x)
        {
            var l = Vector3.LerpUnclamped(a, c, x);
            b.Legs(l.x, l.y, l.z);
        }

        // The wings from one shape to another (x 0..1): spread flat, or folded against the body.
        static void Fold(ref Bird b, float x)
        {
            b.raiseL = b.raiseR = Mathf.Lerp(b.raiseL, -22f, x);
            b.sweep = Mathf.Lerp(b.sweep, 58f, x);
            b.elbow = Mathf.Lerp(b.elbow, -112f, x);
            b.elbowRaise = Mathf.Lerp(b.elbowRaise, 0f, x);
            b.handRaiseL = b.handRaiseR = Mathf.Lerp(b.handRaiseL, -6f, x);
            b.handSweep = Mathf.Lerp(b.handSweep, 128f, x);
            b.twist = Mathf.Lerp(b.twist, 0f, x);
            b.handTwist = Mathf.Lerp(b.handTwist, 0f, x);
        }

        // One wing beat at phase (in beats) blended in by weight over the shape the wings hold otherwise. The power
        // stroke comes down slower with the arm stretched and a little forward, the leading edge down, the hand bent up
        // by the air; the recovery is quicker: the elbow and wrist fold so the wing shortens and its tip trails below,
        // the leading edge up. rise: how far the body is lifted by it (m); kick: its nod (degrees, nose up on the power
        // stroke).
        static void Beat(ref Bird b, float phase, float weight, float mid, float reach, out float rise, out float kick)
        {
            float p = Mathf.Repeat(phase, 1f), angle, speed;
            if (p < DownShare) { float x = p / DownShare; angle = Mathf.Cos(x * Mathf.PI); speed = -Mathf.Sin(x * Mathf.PI); }
            else { float x = (p - DownShare) / (1f - DownShare); angle = -Mathf.Cos(x * Mathf.PI); speed = Mathf.Sin(x * Mathf.PI); }
            float upStroke = Mathf.Max(0f, speed), downStroke = Mathf.Max(0f, -speed);
            b.raiseL = b.raiseR = Mathf.Lerp(b.raiseL, mid + reach * angle, weight);
            b.sweep = Mathf.Lerp(b.sweep, 15f * upStroke - 6f * downStroke, weight);
            b.twist = Mathf.Lerp(b.twist, 9f * downStroke - 15f * upStroke, weight);
            b.elbow = Mathf.Lerp(b.elbow, -30f * upStroke, weight);
            b.elbowRaise = Mathf.Lerp(b.elbowRaise, 5f * downStroke - 7f * upStroke, weight);
            b.handRaiseL = b.handRaiseR = Mathf.Lerp(b.handRaiseL, 17f * downStroke - 22f * upStroke, weight);
            b.handSweep = Mathf.Lerp(b.handSweep, 52f * upStroke, weight);
            b.handTwist = Mathf.Lerp(b.handTwist, 8f * downStroke - 12f * upStroke, weight);
            rise = -0.085f * Mathf.Cos((p - 0.3f) * Mathf.PI * 2f) * weight;
            kick = -2.4f * speed * weight;
        }

        // The eagle at t seconds into the opening.
        void Pose(float t)
        {
            var b = new Bird();
            Vector3 pos, dir;
            float pitch = 0f, roll = 0f, yaw = 0f;
            float air = Mathf.Sin(t * 1.3f) + 0.5f * Mathf.Sin(t * 2.9f + 1f);   // the air is never quite still
            riderLean = 14f; riderArms = -72f; riderFlail = 0f; riderLook = 0f; riderDrop = 0f;

            if (t < Approach)
            {
                pos = Along(Pace(t));
                Vector3 soon = Along(Pace(t + 0.12f)), later = Along(Pace(t + 0.45f));
                dir = (soon - pos).sqrMagnitude > 1e-6f ? (soon - pos).normalized : face;
                // Banked into the turn it is making, and its head already looking round it.
                float turn = Vector3.SignedAngle(new Vector3(dir.x, 0f, dir.z), new Vector3(later.x - soon.x, 0f, later.z - soon.z), Vector3.up) / 0.33f;
                roll = -Mathf.Clamp(turn * 1.0f, -44f, 44f) + 1.6f * air;
                float flare = Smooth(FlareFrom, Approach - 0.15f, t);
                // The glide it holds between beats: wings a little above level, hands higher, elbow and wrist just bent.
                b.raiseL = b.raiseR = 9f + 1.5f * air;
                b.handRaiseL = b.handRaiseR = 7f;
                b.sweep = 5f; b.elbow = -8f; b.handSweep = 9f; b.twist = 2f;
                // Bursts of beats and glides round the lap, then braking.
                float beats = Mathf.Max(Mathf.Max(Window(t, -1f, 2.0f), Window(t, 4.2f, 5.7f)), Smooth(FlareFrom - 0.1f, FlareFrom + 0.3f, t));
                float phase = t < FlareFrom ? t * CruiseBeat : FlareFrom * CruiseBeat + (t - FlareFrom) * BrakeBeat;
                Beat(ref b, phase, beats, Mathf.Lerp(8f, 20f, flare), Mathf.Lerp(33f, 45f, flare), out float rise, out float kick);
                beatPhase = phase; beatWeight = beats; beatStage = 0;   // for the wing sound (TutorialIntro.Sound)
                pos += Vector3.up * rise;
                // The flare: reared back with the wings cupping the air ahead, the tail spread down, the feet thrown out.
                pitch = kick - 40f * flare;
                b.sweep -= 14f * flare;
                b.twist -= 20f * flare;
                b.handTwist -= 10f * flare;
                b.tailPitch = 36f * flare + 4f * Mathf.Abs(roll) / 44f;
                b.tailRoll = -roll * 0.45f;
                Legs(ref b, Tucked, Reaching, Smooth(FlareFrom - 0.5f, Approach - 0.1f, t));
                b.footL += 8f * flare * Mathf.Sin(t * 19f);            // the toes working for the floor
                b.footR -= 8f * flare * Mathf.Sin(t * 19f);
                // The head keeps level and steady while the body rears and rolls under it.
                b.neckPitch = -pitch * 0.85f + 10f * flare;
                b.headRoll = -roll * 0.65f;
                b.neckYaw = Mathf.Clamp(turn * 0.55f, -30f, 30f);
                // The rider: pressed into the seat by each beat, looking into the turn; thrown back, arms up, by the flare.
                float alarm = Smooth(FlareFrom + 0.15f, Approach - 0.2f, t);
                riderLean = 14f + 2f * air - 2.5f * kick - 36f * alarm;
                riderArms = Mathf.Lerp(-72f, -165f, alarm);
                riderFlail = alarm;
                riderLook = Mathf.Clamp(turn * 0.6f, -35f, 35f);
                riderDrop = -0.7f * rise;
            }
            else if (t < Approach + Landing)
            {
                float s = t - Approach;
                float run = Mathf.Min(s, TripAt), slid = Mathf.Max(0f, s - TripAt);
                float gone = RunSpeed * run - 0.5f * (RunSpeed - SlideSpeed) / TripAt * run * run
                           + SlideSpeed / SlideDrag * (1f - Mathf.Exp(-SlideDrag * slid));
                pos = touchdown + face * gone;
                dir = face;
                float stride = (s - 0.1f) / 0.3f * Mathf.PI * 2f;     // the running hops
                if (s < TripAt)
                {
                    // The feet take it: the legs fold right down under the weight and push back up, then two hops, the
                    // body further and further ahead of its feet, the wings held high and beating the air for balance.
                    float squat = Smooth(0f, 0.1f, s), up2 = Smooth(0.1f, 0.24f, s);
                    float hop = s < 0.1f ? 0f : Mathf.Abs(Mathf.Sin((s - 0.1f) / 0.25f * Mathf.PI));
                    pos.y += Mathf.Lerp(Mathf.Lerp(StandHeight + 0.3f, SquatHeight, squat), StandHeight - 0.06f, up2) + 0.2f * hop * up2;
                    pitch = Mathf.Lerp(-26f, -14f, squat) + 40f * Smooth(0.12f, TripAt, s);   // -26: the flare's pitch less the glide slope
                    roll = 9f * Mathf.Sin(stride) * up2;
                    b.raiseL = 46f + 13f * Mathf.Sin(s * 27f);
                    b.raiseR = 46f - 13f * Mathf.Sin(s * 27f + 0.6f);
                    b.handRaiseL = b.handRaiseR = 16f;
                    b.sweep = -8f; b.elbow = -14f; b.handSweep = 10f; b.twist = -24f; b.handTwist = -10f;
                    Legs(ref b, Reaching, Squatting, squat);
                    Legs(ref b, new Vector3(b.thighL, b.shankL, b.footL), Standing, up2);
                    float step = 30f * Mathf.Sin(stride) * up2;
                    b.thighL += step; b.shankL -= step * 0.7f; b.footL -= step * 0.3f;
                    b.thighR -= step; b.shankR += step * 0.7f; b.footR += step * 0.3f;
                    b.neckPitch = -pitch * 0.75f + 16f;
                    b.curl = 14f * up2;
                    b.headPitch = 6f * up2;                              // eyes on the floor rushing up
                    b.headRoll = -roll * 0.6f;
                    b.tailPitch = Mathf.Lerp(36f, -20f, Smooth(0.1f, TripAt, s));   // up as a counterweight
                    // The rider is bounced off the seat by each hop and pitched forward, arms flailing.
                    riderLean = Mathf.Lerp(-22f, 34f, Smooth(0.05f, TripAt, s));
                    riderArms = -165f;
                    riderFlail = 1f;
                    riderDrop = 0.14f * hop * up2;
                }
                else
                {
                    // The trip: the chest slams down, bounces once, and it slides on its belly, slewing a little, wings
                    // slapped flat and dragging, legs left behind, tail whipped up by the stop, neck out and chin ploughing.
                    float fall = Smooth(0f, 0.12f, slid);
                    float bounce = slid < 0.12f ? 0f : 0.17f * Mathf.Abs(Mathf.Sin((slid - 0.12f) / 0.3f * Mathf.PI)) * Mathf.Exp(-(slid - 0.12f) * 5f);
                    pos.y += Mathf.Lerp(StandHeight - 0.12f, BellyHeight, fall) + bounce;
                    pitch = Mathf.Lerp(26f, 40f, fall) - 12f * (1f - Ring(slid - 0.12f, 2.6f, 4.5f)) * Smooth(0.12f, 0.5f, slid);
                    roll = 6f * Ring(slid, 3.2f, 3.5f);
                    yaw = 13f * Smooth(0.1f, 0.9f, slid);
                    float slap = Smooth(0f, 0.16f, slid);
                    b.raiseL = Mathf.Lerp(46f, -9f, slap) + 9f * Ring(slid - 0.16f, 4f, 6f) * slap;
                    b.raiseR = Mathf.Lerp(46f, -8f, slap) + 7f * Ring(slid - 0.2f, 4f, 6f) * slap;
                    b.handRaiseL = Mathf.Lerp(16f, -5f, slap) + 12f * Ring(slid - 0.2f, 5f, 6f) * slap;
                    b.handRaiseR = Mathf.Lerp(16f, -4f, slap) + 12f * Ring(slid - 0.24f, 5f, 6f) * slap;
                    b.sweep = Mathf.Lerp(-8f, 18f, Smooth(0f, 0.5f, slid));
                    b.elbow = Mathf.Lerp(-14f, -30f, Smooth(0.1f, 0.7f, slid));
                    b.handSweep = Mathf.Lerp(10f, 34f, Smooth(0.1f, 0.7f, slid));
                    b.twist = Mathf.Lerp(-24f, 4f, slap);
                    Legs(ref b, Standing, Trailing, Smooth(0f, 0.24f, slid));
                    b.thighL += 8f * Ring(slid - 0.22f, 3f, 5f);
                    b.thighR += 8f * Ring(slid - 0.28f, 3f, 5f);
                    b.neckPitch = Mathf.Lerp(-4f, 30f, fall) - 22f * Ring(slid - 0.12f, 2.6f, 4f) * Smooth(0.12f, 0.2f, slid);
                    b.curl = Mathf.Lerp(14f, -10f, fall);
                    b.tailPitch = -58f * Ring(slid - 0.02f, 1.5f, 3.2f) * Smooth(0f, 0.1f, slid) - 16f;
                    b.tailRoll = 10f * Ring(slid, 2.2f, 3f);
                }
            }
            else if (t < Approach + Landing + Dazed + Rise)
            {
                float s = t - Approach - Landing;
                pos = stop;
                dir = face;
                yaw = 13f;
                // Winded on its belly: fast shallow breaths that slow, the wings limp where they fell, one of them
                // twitching; the head comes up unsteadily on its neck, rolls, shakes hard, then looks about.
                float breath = Mathf.Sin(s * Mathf.PI * 2f * Mathf.Lerp(3.2f, 1.7f, Mathf.Clamp01(s / Dazed)));
                float up01 = Smooth(Dazed + 0.1f, Dazed + 0.8f, s);    // getting up
                float folded = Smooth(Dazed + 0.6f, Dazed + 1.2f, s);  // wings drawn in
                // It gathers its legs under it and pushes up with its wrists on the floor, then draws the wings in
                // against its sides and shakes itself from head to tail.
                float gather = Smooth(Dazed - 0.25f, Dazed + 0.2f, s);
                float push = Window(s, Dazed - 0.05f, Dazed + 0.5f, 0.2f);
                float ruffle = Window(s, Dazed + 1.05f, Dazed + 1.45f, 0.08f);
                float shiver = Mathf.Sin(s * Mathf.PI * 2f * 9.5f) * ruffle;
                pos.y += Mathf.Lerp(BellyHeight, StandHeight, up01) + 0.018f * breath * (1f - up01);
                pitch = Mathf.Lerp(28f + 1.6f * breath, -36f, up01);
                roll = 7f * shiver;
                b.raiseL = -9f - 14f * push + 6f * Window(s, 0.85f, 1.0f, 0.06f);
                b.raiseR = -8f - 14f * push;
                b.sweep = 18f; b.elbow = -30f - 20f * push; b.handSweep = 34f + 20f * push; b.twist = 4f;
                b.handRaiseL = -5f + 10f * Window(s, 0.85f, 1.0f, 0.06f);
                b.handRaiseR = -4f;
                Fold(ref b, folded);
                b.raiseL += 9f * shiver; b.raiseR -= 9f * shiver; b.elbow += 10f * Mathf.Abs(shiver);
                Legs(ref b, Trailing, Squatting, gather);
                Legs(ref b, new Vector3(b.thighL, b.shankL, b.footL), Standing, up01);
                float lifted = Smooth(0.45f, 1.15f, s);
                float shake = Window(s, 1.35f, 1.95f, 0.12f);
                b.neckPitch = Mathf.Lerp(30f, -14f, lifted) * (1f - up01) + 24f * up01 + 5f * Mathf.Sin(s * 2.1f) * lifted * (1f - up01) - 6f * ruffle;
                b.curl = Mathf.Lerp(-10f, 8f, lifted) + 22f * up01;
                b.headRoll = 13f * Mathf.Sin(s * 3.3f) * lifted * (1f - shake) * (1f - up01) + 10f * shiver;
                b.headYaw = 32f * Mathf.Sin((s - 1.35f) * Mathf.PI * 2f * 6.5f) * shake + 8f * shiver;
                b.neckYaw = 30f * Window(s, 2.05f, 2.4f, 0.15f) - 22f * Window(s, Dazed + 0.9f, Dazed + Rise, 0.2f);
                b.tailPitch = Mathf.Lerp(-16f, 6f, lifted) + 14f * up01 + 14f * shiver;
                b.tailRoll = 12f * shiver;
            }
            else
            {
                // The crouch with the wings thrown up and open, the leap off straightening legs, and hard beats that
                // slow as it climbs away, the feet drawn up under the tail.
                float s = t - Approach - Landing - Dazed - Rise;
                const float crouch = 0.34f;
                float open = Smooth(0f, crouch, s);
                float fly = Mathf.Max(0f, s - crouch);
                float u = Mathf.Clamp01(fly / (Leave - crouch));
                float eased = u * u * (1.7f - 0.7f * u);
                pos = Curve(outPath, eased);
                dir = fly > 0f ? CurveDir(outPath, Mathf.Max(eased, 0.03f)) : face;
                float airborne = Smooth(0f, 0.25f, fly);
                yaw = 13f * (1f - airborne);
                pos.y -= (StandHeight - SquatHeight) * open * (1f - airborne);
                Fold(ref b, 1f - open);
                b.raiseL = b.raiseR = Mathf.Lerp(b.raiseL, 58f, open);
                b.handRaiseL = b.handRaiseR = Mathf.Lerp(b.handRaiseL, 14f, open);
                float phase = fly * 3.9f - 0.24f * fly * fly;         // from the top of the first power stroke
                Beat(ref b, phase, airborne, 10f, Mathf.Lerp(48f, 36f, u), out float rise, out float kick);
                if (phase >= 0f) { beatPhase = phase; beatWeight = airborne; beatStage = 1; }   // for the wing sound
                pos += Vector3.up * rise * airborne;
                Vector3 flat = new Vector3(dir.x, 0f, dir.z);
                float turn = fly > 0f ? Vector3.SignedAngle(flat, Vector3.ProjectOnPlane(CurveDir(outPath, Mathf.Min(1f, eased + 0.06f)), Vector3.up), Vector3.up) : 0f;
                roll = -Mathf.Clamp(turn * 3f, -30f, 30f) * airborne + 1.6f * air * airborne;
                pitch = Mathf.Lerp(-36f, -16f, open) * (1f - airborne) + (kick - 20f * (1f - u)) * airborne;
                Legs(ref b, Standing, Squatting, open);
                Legs(ref b, new Vector3(b.thighL, b.shankL, b.footL), Pushing, Smooth(0f, 0.16f, fly));
                Legs(ref b, new Vector3(b.thighL, b.shankL, b.footL), Tucked, Smooth(0.35f, 1.1f, fly));
                b.neckPitch = -pitch * 0.8f;
                b.curl = 30f * (1f - open);
                b.headRoll = -roll * 0.6f;
                b.tailPitch = Mathf.Lerp(20f, 30f, open) * (1f - airborne);
                b.tailRoll = -roll * 0.4f;
            }

            // The body: nose along dir, slewed, then pitched about its own side axis and rolled about its length.
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(pitch, yaw, roll);
            transform.SetPositionAndRotation(pos, look * modelToLook);

            // The bones, each turned about the model's own axes from its rest, down each chain from the body.
            Vector3 nod = Vector3.Cross(up, fwd), tilt = Vector3.Cross(fwd, -up);
            Quaternion root = transform.rotation;
            void Set(B which, Quaternion turned) => bone[(int)which].rotation = root * turned * rest[(int)which];
            Quaternion Wing(Vector3 out_, float raise, float sweep, float twist) =>
                Quaternion.AngleAxis(raise, Vector3.Cross(out_, up)) * Quaternion.AngleAxis(sweep, Vector3.Cross(out_, -fwd)) * Quaternion.AngleAxis(twist, tilt);
            Quaternion Nod(float degrees) => Quaternion.AngleAxis(degrees, nod);
            Quaternion Yaw(float degrees) => Quaternion.AngleAxis(degrees, up);

            Quaternion q = Wing(left, b.raiseL, b.sweep, b.twist);
            Set(B.HumerusL, q);
            q *= Wing(left, b.elbowRaise, b.elbow, 0f);
            Set(B.ForearmL, q);
            Set(B.HandL, q * Wing(left, b.handRaiseL, b.handSweep, b.handTwist));
            q = Wing(-left, b.raiseR, b.sweep, b.twist);
            Set(B.HumerusR, q);
            q *= Wing(-left, b.elbowRaise, b.elbow, 0f);
            Set(B.ForearmR, q);
            Set(B.HandR, q * Wing(-left, b.handRaiseR, b.handSweep, b.handTwist));

            q = Nod(-b.thighL); Set(B.ThighL, q);                     // > 0: forward
            q *= Nod(-b.shankL); Set(B.ShankL, q);
            Set(B.FootL, q * Nod(-b.footL));
            q = Nod(-b.thighR); Set(B.ThighR, q);
            q *= Nod(-b.shankR); Set(B.ShankR, q);
            Set(B.FootR, q * Nod(-b.footR));

            q = Yaw(b.neckYaw * 0.3f) * Nod(b.neckPitch * 0.35f - b.curl);   // > 0: nose down
            Set(B.Neck1, q);
            q *= Yaw(b.neckYaw * 0.3f) * Nod(b.neckPitch * 0.3f - b.curl * 0.2f);
            Set(B.Neck2, q);
            Set(B.Head, q * Yaw(b.neckYaw * 0.4f + b.headYaw) * Nod(b.neckPitch * 0.35f + b.curl * 1.2f + b.headPitch) * Quaternion.AngleAxis(b.headRoll, fwd));
            Set(B.Tail, Nod(-b.tailPitch) * Quaternion.AngleAxis(b.tailRoll, fwd));   // tailPitch > 0: tail tip down
        }
    }
}
