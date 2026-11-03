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
    //   4  The final gate                 z 126..171  the three ways meet on the platform (y 8); the gate is pushed
    //                                                 open (hold right click on it), the finish is behind it
    //
    // Sections 3 and 4 are written in their own frame (the 11-02 numbers, the arena starting at z 106) and built under
    // a group moved LaterShift south, so the arena starts where the canal ends.
    //
    // Built from code when the prefab is placed (Awake), like the map test's calibration yard, so the prefab itself is
    // this one component and the numbers below are its Inspector. Every obstacle is its own reusable component
    // (RoamingCarriage, HorizontalWaterwheel, RotatingCarriageGroup, VerticalWaterwheel, CargoChute/RollingCargo,
    // TimedGate, PushGate; ChargingCarriage and TiltingWoodenBridge are not on this map's route) with its own Inspector
    // during Play. Moving parts run on ObstacleClock (absolute rule 7); the final gate is state-driven and offline
    // only. Layout and the reasons for the numbers: Docs/KingRush/ROYAL_CARRIAGE_YARD.md.
    public sealed partial class RoyalCarriageYard : MonoBehaviour
    {
        [Serializable]
        public sealed class GateSettings
        {
            [Tooltip("Seconds after the map is built that the outer gate starts to open")]
            public float startDelay = 3f;
            [Tooltip("Seconds the outer gate takes to swing open")]
            public float startDuration = 2.5f;
            [Tooltip("Seconds of one character pushing to open the final gate")]
            public float finalPushSeconds = 3f;
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
            public float knockbackForce = 4f;
        }

        [Serializable]
        public sealed class RoamingSettings
        {
            [Tooltip("Top speed of a charge (m/s); each carriage after the first is 0.3 m/s faster")]
            public float moveSpeed = 9f;
            [Tooltip("Seconds a carriage takes to turn to its next point")]
            public float turnSeconds = 1.2f;
            [Tooltip("The carriages' random points: another seed, another dance")]
            public int seed = 101;
            [Tooltip("Shortest and longest wait with the lamps red before a charge (s)")]
            public float minAim = .6f, maxAim = 1.2f;
            public float knockbackForce = 7f;
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
            public float knockbackForce = 6f;
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
            public float knockbackForce = 6f;
        }

        [Serializable]
        public sealed class LiftSettings
        {
            [Tooltip("Degrees per second")]
            public float rotationSpeed = 20f;
            public int bladeCount = 8;
            [Tooltip("Degrees past the top at which riders are let go onto the landing")]
            public float releaseAngle = 30f;
            [Tooltip("Push forward when let go (m/s)")]
            public float releaseForward = 4f;
            [Tooltip("Push up when let go (m/s)")]
            public float releaseUp = 3.5f;
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

        Transform root, later;
        bool built;

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
            }
            finally
            {
                go.SetActive(true);
            }
        }

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
