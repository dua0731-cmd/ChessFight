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
    //                                                 44 x 48 m yard where four carriages charge about at random
    //   2  Canal and the giant wheel      z 50..82    stone landings either side; two wooden walkways between them go
    //                                                 through one giant waterwheel (a round frame and six spokes) -
    //                                                 jump and dive through a hole between two spokes
    //   3  Arena and waterfall            z 82..126   three ways: centre - the carousel of carriages, then the lifting
    //                                                 waterwheel up the waterfall (hold right click); left and right -
    //                                                 ramps up to the platform with cargo rolling down them
    //   4  The final gate                 z 126..162  the three ways meet on the platform (y 8); the gate is pushed
    //                                                 open (hold right click on it), the finish is behind it
    //
    // Sections 3 and 4 are written in their own frame (the 11-02 numbers, the arena starting at z 106) and built under
    // a group moved LaterShift south, so the arena starts where the canal ends.
    //
    // Built from code when the prefab is placed (Awake), like the map test's calibration yard, so the prefab itself is
    // this one component and the numbers below are its Inspector. Every obstacle is its own reusable component
    // (RoamingCarriage, HorizontalWaterwheel, RotatingCarriageGroup, VerticalWaterwheel, CargoChute/RollingCargo,
    // TimedGate, PushGate; ChargingCarriage and TiltingWoodenBridge are not on this map's route) with its own Inspector
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
            [Tooltip("Top speed of a charge (m/s); each carriage after the first is 0.3 m/s faster")]
            public float moveSpeed = 9f;
            [Tooltip("Seconds a half turn on the spot takes; a smaller turn takes its share (the ends swing at most ~5 m/s)")]
            public float turnSeconds = 2.2f;
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

        [Serializable]
        public sealed class CarouselSettings
        {
            [Tooltip("Degrees per second")]
            public float rotationSpeed = 22f;
            [Tooltip("Clockwise seen from above")]
            public bool clockwise = true;
            public int carriageCount = 4;
            [Tooltip("Centre post to carriage (m); keep it at 7.5 or under, the planters stand at 10 m")]
            [Range(5f, 7.5f)] public float rotationRadius = 7.5f;
            [Tooltip("Push of a hit (m/s) before the speed adds 0.6 per m/s; it sets the pawn's speed")]
            public float knockbackForce = 4.5f;
        }

        [Serializable]
        public sealed class CargoSettings
        {
            [Tooltip("Seconds between pieces on each ramp")]
            public float spawnInterval = 1.7f;
            [Tooltip("Speed down the ramp (m/s)")]
            public float rollingSpeed = 5f;
            [Tooltip("Piece diameters (m), taken in turn")]
            public float[] cargoSizes = { 1f, 1.4f, 1.8f };
            [Tooltip("Seconds the right ramp runs ahead of the left, so the two are not in step")]
            public float rightRampOffset = .85f;
            [Tooltip("Push of a hit (m/s) before the piece's speed adds 0.6 per m/s, at most 6: down the ramp and aside, "
                     + "a stagger (R287, was 6 + speed straight down)")]
            public float knockbackForce = 3f;
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
        [Header("1. Yard: the four roaming carriages")]
        [SerializeField] RoamingSettings roaming = new RoamingSettings();
        [Header("2. Canal: the giant waterwheel")]
        [SerializeField] CrossingSettings crossing = new CrossingSettings();
        [Header("3. Arena: the carousel")]
        [SerializeField] CarouselSettings carousel = new CarouselSettings();
        [Header("3. Arena: cargo ramps")]
        [SerializeField] CargoSettings cargo = new CargoSettings();
        [Header("3. Arena: the lifting waterwheel")]
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

        // As a section (R508), in the map's frame: the one way for the HUD and the validator (the left walkway, the west
        // cargo ramp, the gate, the boundary), the walkways' break at the wheel (a sprint jump and a dive: meant), and the
        // section's own description.
        void SectionParts()
        {
            var parts = Group("Section");
            Place<IntendedGap>(parts, "Wheel gap (meant)", new Vector3(-4.6f, .5f, WheelZ))
                .Configure(new Vector3(4f, 3f, crossing.gapLength + 1f), "물레방아 사이 틈: 질주 점프 + 다이빙");
            var route = new[]
            {
                SectionEntry, new Vector3(0f, 0f, -6f), new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, 40f),
                new Vector3(-4.6f, 0f, CanalZ0 + 4f), new Vector3(-4.6f, 0f, WalkZ1), new Vector3(0f, 0f, CanalZ1 + 2f),
                new Vector3(-22f, 0f, 94f), new Vector3(-22f, Top, 126f), new Vector3(0f, Top, 130f), new Vector3(0f, Top, 152f),
                new Vector3(0f, Top, 158f), new Vector3(0f, Top - 1f, 160f), SectionExit,
            };
            var beats = new[] { "1", "1", "1", "1", "2", "2", "3", "3", "4", "4", "4", "4", "4", "4" };
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
            new SectionBeat("3 마차 회전목마 · 폭포", new Vector3(-27f, -4f, 82f), new Vector3(27f, 9f, 126f)),
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
