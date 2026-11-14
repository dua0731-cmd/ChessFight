using System;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Carriage Yard (왕실 마차뜰, 11-02, the user's level design spec "ROYAL CARRIAGE YARD", reshaped by the user
    // on 11-03, R286): a Pawn Rush map prefab for the map test scene (Assets/Prefabs/PawnRushMaps), for two teams of up
    // to six who may shove each other. A pure obstacle race - no items, keys, missions or anything to carry - in four
    // sections, going north (+z) and up:
    //
    //   1  Outer gate and the yard        z -16..50   start court, a gate that opens at the start, right behind it a
    //                                                 44 x 48 m yard where (R659) five carriages drawn by horses
    //                                                 charge about at random between storage sheds along both walls
    //   2  Canal and the giant wheel      z 50..82    stone landings either side; two wooden walkways between them go
    //                                                 through one giant waterwheel (a round frame and six spokes) -
    //                                                 jump and dive through a hole between two spokes
    //   3  Loading and sorting yard       z 82..126   (R659, the whole 52 m: the cargo ramps and the R532 loading yard
    //                                                 between them are gone; R661 a flow of cargo) a conveyor across
    //                                                 the yard to hop over between its loads, three storage rows whose
    //                                                 cranes keep moving the one gap in each half, the heavy berth
    //                                                 where a giant chest is set down with a shockwave, sorting stalls
    //                                                 along the walls; then the lifting waterwheel up the waterfall, as
    //                                                 it always was (hold right click), barges and sluices beside it
    //   4  The final gate                 z 126..162  the wheel's landing on the platform (y 8); the gate is pushed
    //                                                 open (hold right click on it), the finish is behind it
    //
    // Sections 3 and 4 are written in their own frame (the 11-02 numbers, the arena starting at z 106) and built under
    // a group moved LaterShift south, so the arena starts where the canal ends.
    //
    // Built from code when the prefab is placed (Awake), like the map test's calibration yard, so the prefab itself is
    // this one component and the numbers below are its Inspector. Every obstacle is its own reusable component
    // (CarriageFleet/RoamingCarriage, HorizontalWaterwheel, StableCharger, CargoConveyor/ConveyorCrate, StackRow/StackCrate,
    // CargoSlam/SlamChest, VerticalWaterwheel, TimedGate, PushGate; ChargingCarriage, RotatingCarriageGroup - the centre
    // way's carousel until R532 - CargoChute/RollingCargo - the ramps' cargo until R659 - CraneDrop/CraneCrate - the crane
    // lines until R661 - and TiltingWoodenBridge are not on this map's route)
    // with its own Inspector
    // during Play. Moving parts run on ObstacleClock (absolute rule 7); a match hands the map its start and seed with
    // StartAt. The final gate is state-driven and judged by the host. Layout and the reasons for the numbers:
    // Docs/PawnRush/ROYAL_CARRIAGE_YARD.md.
    //
    // As a section (R508, Early_1-2_RoyalCarriageYard.prefab, slot 1 "1-2 왕실 마차뜰"; asSection): the round's start
    // places stand in the start court (no own ones), the round starts it (ISectionPart: StartAt at the section's start
    // plus the gate's countdown, the section's seed), the finish court, the crown and the palace behind them give way to
    // the boundary every early section ends with - 12 m of runout after the final gate, a terrace 1 m over the next
    // mini-game (CP5), an open arch, four 0.25 m steps down and the 12 x 4 m entrance band - and the final gate's
    // progress goes to the other PCs in the course state (PushGate is an ISectionNetPart). Entry (0, 0, -16): the start
    // court's south wall; exit (0, 7, 164): the band's end. The map test scene keeps the map as it was.
    public sealed partial class RoyalCarriageYard : MonoBehaviour, ISectionPart
    {
        public const string SectionId = "Early_1-2_RoyalCarriageYard";
        public const string SectionName = "왕실 마차뜰";
        public static readonly Vector3 SectionEntry = new Vector3(0f, 0f, -16f), SectionExit = new Vector3(0f, 7f, 164f);

        [Serializable]
        public sealed class GateSettings
        {
            [Tooltip("Seconds after the map is built that the outer gate starts to open")]
            public float startDelay = 3f;
            [Tooltip("Seconds the outer gate takes to swing open")]
            public float startDuration = 2.5f;
            [Tooltip("Seconds of one character pushing to open the final gate all the way (a pawn fits through after about a third)")]
            public float finalPushSeconds = 10.5f;
            [Tooltip("Speed each extra pusher adds to the final gate (0.6 = 60 %)")]
            public float finalCrowdBonus = .6f;
        }

        [Serializable]
        public sealed class CrossingSettings
        {
            [Tooltip("Degrees per second")]
            public float rotationSpeed = 15f;
            [Tooltip("Clockwise as seen walking towards it: the spokes rise through the left walkway and come down "
                     + "through the right one")]
            public bool clockwise = true;
            [Tooltip("Spokes on each face; the holes between them are the way through")]
            [Range(3, 8)] public int spokeCount = 6;
            [Tooltip("Centre to the outside of the round frame (m); the axle's height follows from it")]
            [Range(8f, 10f)] public float wheelRadius = 9f;
            [Tooltip("Width of each of the two walkways (m)")]
            [Range(2.4f, 3.6f)] public float passageWidth = 3.2f;
            [Tooltip("The walkways' break at the wheel (m): up to ~3.2 a sprint jump alone clears it; at 3.4 it takes a "
                     + "sprint jump and a dive at the top of it (the user's \"점프+슬라이딩\")")]
            [Range(2.2f, 3.6f)] public float gapLength = DefaultGap;
            public float knockbackForce = 4f;
        }

        [Serializable]
        public sealed class RoamingSettings
        {
            // R659 (the user's zone 1: "말과 말이 이끄는 마차들이 돌아다니며" - "'말+마차'를 말하는 거지 말이나 마차가 따로
            // 떨어져서 돌아다니고 있는 장애물을 만들어 달라는 이야기가 아니었어"): carriages drawn by horses share the yard
            // between the storage sheds, kept apart in time (RoamingFleet.Rules.Timed).
            [Tooltip("Carriages drawn by a pair of horses (one long body: 6.95 x 2.3 m)")]
            public int teams = 5;
            [Tooltip("Loose horses (2.6 x 0.9 m, they rear through their aim): none in the yard, the user wants horse and carriage together")]
            public int horses = 0;
            [Tooltip("Top speed of a team's charge (m/s); each team after the first is 0.3 m/s faster")]
            public float moveSpeed = 9.3f;
            [Tooltip("Top speed of a loose horse's charge (m/s); each horse after the first is 0.3 m/s faster")]
            public float horseSpeed = 11f;
            [Tooltip("Seconds a team's half turn on the spot takes; a smaller turn takes its share (its ends swing at most ~5 m/s)")]
            public float teamTurnSeconds = 3.4f;
            [Tooltip("Seconds a loose horse's half turn on the spot takes")]
            public float horseTurnSeconds = 1.6f;
            [Tooltip("Seconds a lone carriage's half turn takes (none roam the yard since R659; kept for the fleet's rules)")]
            public float turnSeconds = 2.2f;
            [Tooltip("Keep the bodies apart in time rather than over whole legs, so a yard of long teams keeps moving")]
            public bool timed = true;
            [Tooltip("Random points a body tries before it waits a moment")]
            public int tries = 128;
            [Tooltip("Nowhere free for a charge of 10 m or more, a shorter dart down to this (m) before it waits")]
            public float shortCharge = 4f;
            [Tooltip("The carriages' random points offline: another seed, another dance (a match draws one of "
                     + "RoamingFleet.YardSeeds by its round seed, StartAt)")]
            public int seed = 38;
            [Tooltip("Shortest and longest wait with the lamps red before a charge (s)")]
            public float minAim = .6f, maxAim = 1.2f;
            [Tooltip("Push of a hit (m/s) before the speed adds 0.25 per m/s, at most 7 m/s; it sets the pawn's speed, "
                     + "mostly out to the side of the charge, 60 % of its part up the yard")]
            public float knockbackForce = 4f;
            public float knockdownSeconds = 1f;
        }

        // Zone 2, the royal loading and sorting yard (R661, rebuilt as a flow of cargo; the user: "플레이어들을 확실히 방해할
        // 수 있을만한 동선과 장애물 형태"): a conveyor across the yard, three storage rows whose cranes keep moving their gaps,
        // the heavy berth where a giant chest is set down with a shockwave (RoyalCarriageYard.SortingYard.cs).
        [Serializable]
        public sealed class SortingYardSettings
        {
            [Tooltip("Speed of the conveyor's belt and its loads (m/s): whoever stands on the belt is carried along at it")]
            public float conveyorSpeed = 3.6f;
            [Tooltip("Loads round the conveyor's loop (single crates and two-high stacks, 45 % stacks; 2.6 m apart or more)")]
            public int conveyorLoads = 11;
            [Tooltip("Push of a load running into a pawn on the belt (m/s), off the belt")]
            public float conveyorKnockback = 4.5f;
            [Tooltip("Seconds each storage row's gap stands open before the next stack is lifted, row by row (each crane its "
                     + "own pace, so the rows' gaps never stay lined up)")]
            public float[] rowOpen = { 1.1f, 1.4f, .9f };
            [Tooltip("Seconds a stack is carried one slot over into the gap (0.7: it swings in at up to 4.7 m/s, after the gap "
                     + "has flashed red for 0.8 s and the stack has been lifted)")]
            public float rowCarry = .7f;
            [Tooltip("Push of a stack swinging into a pawn in the gap (m/s), out of the row")]
            public float rowKnockback = 4.5f;
            [Tooltip("How far the heavy chest's wave runs from its centre (m): its red circle (CP3 stays outside it)")]
            public float slamRadius = 5.5f;
            [Tooltip("Seconds the heavy chest is carried to its berth (circle dim), held over it and lowered on its chains "
                     + "(circle bright), sits there and is hoisted: a trip of 10.4 s")]
            public float slamCarry = 2.4f, slamHold = 1f, slamFall = 1.1f, slamSit = 4.5f, slamHoist = 1.4f;
            [Tooltip("Push straight out from the chest (m/s) and how long the wave keeps them down (s)")]
            public float shockPush = 4f, shockKnockdown = 1.3f;
        }

        [Serializable]
        public sealed class LiftSettings
        {
            [Tooltip("Degrees per second (28: the middle way about as quick as the ramps)")]
            public float rotationSpeed = 28f;
            public int bladeCount = 8;
            [Tooltip("Degrees past the top at which riders are let go onto the landing")]
            public float releaseAngle = 30f;
            [Tooltip("Push forward when let go (m/s)")]
            public float releaseForward = 4f;
            [Tooltip("Push up when let go (m/s): 4.5 lifts a rider's feet over the landing's edge without the ledge help")]
            public float releaseUp = 4.5f;
        }

        [Header("1. Gates")]
        [SerializeField] GateSettings gates = new GateSettings();
        [Header("1. Yard: horse-drawn carriages between the sheds")]
        [SerializeField] RoamingSettings roaming = new RoamingSettings();
        [Header("2. Canal: the giant waterwheel")]
        [SerializeField] CrossingSettings crossing = new CrossingSettings();
        [Header("3. Loading and sorting yard: the conveyor, the storage rows, the heavy berth")]
        [SerializeField] SortingYardSettings sorting = new SortingYardSettings();
        [Header("3. The lifting waterwheel")]
        [SerializeField] LiftSettings lift = new LiftSettings();
        [Header("Look")]
        [Tooltip("Towers, battlements, banners and the palace behind the walls (pictures only)")]
        [SerializeField] bool scenery = true;
        [Tooltip("Signs over each section (Korean)")]
        [SerializeField] bool signs = true;
        [Header("Round")]
        [Tooltip("A section of the round (R508): no start places of its own, the round starts it, and it ends in the boundary "
                 + "to the next mini-game instead of the finish court")]
        [SerializeField] bool asSection;

        Transform root, later;
        TimedGate startWest, startEast;
        CarriageFleet carriages;
        bool built;

        // A match's start, the same on every PC (R287): the time the race goes on ObstacleClock (the lobby's go plus any
        // countdown) and the round's seed, both from data every PC holds (absolute rule 15). The outer gate starts to
        // open then and the carriages set off then, dancing to one of the yard's vetted seeds drawn by the round's seed,
        // so every PC sees the same gate and the same carriages without a packet. Until it is called on a shared match
        // clock, the gate stays shut and the carriages wait at home; offline the map starts by itself (startDelay after
        // it is built, roaming.seed). The same start again changes nothing.
        public void StartAt(double clockTime, int roundSeed)
        {
            if (!built) Awake();
            if (startWest != null) startWest.OpenAt(clockTime);
            if (startEast != null) startEast.OpenAt(clockTime);
            if (carriages == null) return;
            uint seed = ChessFight.Network.RoamingFleet.YardSeed(roundSeed);
            carriages.StartAt(clockTime, unchecked((int)seed));
            // The seeds were vetted on the yard as it ships (RoamingFleet.Yard, the Core tests); another yard may dance worse.
            if (carriages.Fleet != null && !carriages.Fleet.Matches(ChessFight.Network.RoamingFleet.Yard(seed, clockTime)))
                Debug.LogWarning("[RoyalCarriageYard] 마차 무리 설정이 시드를 검사한 마당(RoamingFleet.Yard)과 달라, 검사한 시드 목록의 춤이 검사 때와 다를 수 있습니다");
        }

        void Awake()
        {
            if (built) return;
            built = true;
            // Built switched off and switched on at the end: every obstacle's Awake then sees its settings, and no
            // collider is live before the whole map is there.
            var go = new GameObject("Built");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            root = go.transform;
            // Sections 3 and 4 in their own frame, moved so the arena (z 106 there) starts where the canal ends.
            later = Group("3-4 (own frame)");
            later.localPosition = new Vector3(0f, 0f, LaterShift);
            try
            {
                Entrance();
                Canal();
                CarriageArena();
                Platform();
                if (scenery) Scenery();
                if (asSection) SectionParts();
            }
            finally
            {
                go.SetActive(true);
            }
        }

        // The round placed this section and gave it its start and seed (R508; again when a match learns its go).
        public void OnSection(PawnRushSection section)
        {
            if (asSection) StartAt(section.StartClock + gates.startDelay, section.Seed);
        }

        // As a section (R508), in the map's frame: the one way for the HUD and the validator (the left walkway, up the
        // middle of the sorting yard - R661, the real way zigzags through the rows' moving gaps - and the middle lifting
        // wheel - R659, the cargo ramps gone - the gate, the boundary),
        // the walkways' break at the wheel (a sprint jump and a dive: meant), the lifting wheel's ride over its pool (meant),
        // and the section's own description.
        void SectionParts()
        {
            var parts = Group("Section");
            Place<IntendedGap>(parts, "Wheel gap (meant)", new Vector3(-4.6f, .5f, WheelZ))
                .Configure(new Vector3(4f, 3f, crossing.gapLength + 1f), "물레방아 사이 틈: 질주 점프 + 다이빙");
            // The lifting wheel carries its rider up over the pool to the landing (own z 139.85 .. 148.4).
            float pool = PoolZ0 + LaterShift, landing = 148.4f + LaterShift;
            Place<IntendedGap>(parts, "Lifting wheel ride (meant)", new Vector3(0f, Top + .5f, (pool + landing) * .5f))
                .Configure(new Vector3(4f, 3f, landing - pool + .4f), "세로 물레방아를 타고 올라감");
            var route = new[]
            {
                SectionEntry, new Vector3(0f, 0f, -6f), new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, 40f),
                new Vector3(-4.6f, 0f, CanalZ0 + 4f), new Vector3(-4.6f, 0f, WalkZ1), new Vector3(0f, 0f, CanalZ1 + 2f),
                new Vector3(0f, 0f, pool - .25f), new Vector3(0f, Top, pool - .25f), new Vector3(0f, Top, landing + .2f),
                new Vector3(0f, Top, 130f), new Vector3(0f, Top, 152f), new Vector3(0f, Top, 158f), new Vector3(0f, Top - 1f, 160f), SectionExit,
            };
            var beats = new[] { "1", "1", "1", "1", "2", "2", "3", "3", "3", "4", "4", "4", "4", "4", "4" };
            var path = new GameObject("ProgressPath");
            path.transform.SetParent(parts, false);
            path.AddComponent<ProgressPath>().Configure(route, (Vector3[])route.Clone(), beats);
            var section = GetComponent<PawnRushSection>();
            if (section != null)
                section.Describe(SectionId, SectionName, PawnRushSection.SlotKind.Obstacle, SectionEntry, SectionExit, 0f, SectionBeats());
        }

        // Areas for the HUD and the CSV, the first that holds a pawn wins (hips).
        public static SectionBeat[] SectionBeats() => new[]
        {
            new SectionBeat("1 바깥 성문 · 마당", new Vector3(-23f, -2f, -17f), new Vector3(23f, 9f, 50f)),
            new SectionBeat("2 운하 · 대형 물레방아", new Vector3(-23f, -5f, 50f), new Vector3(23f, 12f, 82f)),
            new SectionBeat("3 왕실 하역 창고 · 물레방아", new Vector3(-27f, -4f, 82f), new Vector3(27f, 9f, 126f)),
            new SectionBeat("4 최종 성문", new Vector3(-27f, 5f, 126f), new Vector3(27f, 16f, 165f)),
        };

        // ------------------------------------------------------------------ helpers

        Transform Group(string name, Transform under = null)
        {
            var g = new GameObject(name).transform;
            g.SetParent(under != null ? under : root, false);
            return g;
        }

        // A new object carrying T at a local point, turned `yaw` about y (inactive with the rest of the map until it
        // is built, so T's Awake waits for Configure).
        static T Place<T>(Transform parent, string name, Vector3 at, float yaw = 0f) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go.AddComponent<T>();
        }

        static CourseCheckpoint Checkpoint(Transform parent, int number, Vector3 at, float width) =>
            new CourseBuilder(null, parent).Checkpoint(number, at, 0f, width);

        static T Trigger<T>(Transform parent, string name, Vector3 min, Vector3 max) where T : Component =>
            new CourseBuilder(null, parent).Trigger<T>(name, min, max);

        void Sign(Transform parent, string text, Vector3 at, float size = 1.6f)
        {
            if (signs) YardKit.Label(parent, text, at, size, new Color(.16f, .14f, .2f));
        }
    }
}
