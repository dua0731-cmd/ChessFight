using ChessFight.Gameplay;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // The Royal Carriage Yard's obstacles one at a time (11-02, spec step 2: "each obstacle tested on its own"; 11-03 the
    // roaming carriage and the giant waterwheel): a map prefab for the map test scene with a bay per obstacle along +z,
    // each with its sign and, where a fall is possible, its own checkpoint. The charging carriage and the tilting wooden
    // bridge, which are not on the yard's route, are tested here (the bridge in both hinge modes). T in the map test
    // scene drops the pawn wherever the camera looks, to go straight to a bay.
    //
    //   z 10..26     charging carriage (a 26 m charge across the bay)
    //   z 30..42     roaming carriage (a 30 x 6 m area)
    //   z 46..70     carousel of carriages
    //   z 76..96     giant waterwheel over water, two walkways through it
    //   z 106..116   tilting wooden bridges over a pit: long-side hinge (left), near-end hinge (right)
    //   z 122..160   cargo ramp up to y 5, down again beyond
    //   z 168..179   lifting waterwheel up to the y 8 platform
    //   z 179..210   the push gate on the platform, the finish behind it
    public sealed class CarriageYardObstacleBench : MonoBehaviour
    {
        Transform root;
        bool built;

        void Awake()
        {
            if (built) return;
            built = true;
            var go = new GameObject("Built");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            root = go.transform;
            try { Build(); }
            finally { go.SetActive(true); }
        }

        static T Place<T>(Transform parent, string name, Vector3 at, float yaw = 0f) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go.AddComponent<T>();
        }

        static void Sign(Transform s, string text, Vector3 at) => YardKit.Label(s, text, at, 1.3f, new Color(.16f, .14f, .2f));

        void Build()
        {
            var s = root;
            var cb = new CourseBuilder(null, s);
            Place<SpawnPoint>(s, "Start", Vector3.zero).Configure(0, Teams.White);
            YardKit.Floor(s, "Floor 1", -20f, 20f, -6f, 76f, 0f);

            Sign(s, "돌진 마차", new Vector3(0f, 3.5f, 9f));
            var cart = Place<ChargingCarriage>(s, "Charging carriage", new Vector3(-14f, 0f, 18f), 90f);
            cart.Configure(new Vector3(0f, 0f, 26f), 7f, 3f, 1.5f, .8f, 7f, 1f);

            Sign(s, "돌아다니는 마차 (무작위 돌진)", new Vector3(0f, 4.2f, 28.5f));
            Place<RoamingCarriage>(s, "Roaming carriage", new Vector3(0f, 0f, 36f)).Configure(15f, 3f, 9f, 1.2f, .6f, 1.2f, 7, 7f, 1f, 2);

            Sign(s, "회전 마차", new Vector3(0f, 6.5f, 45f));
            Place<RotatingCarriageGroup>(s, "Carousel", new Vector3(0f, 0f, 58f)).Configure(22f, true, 6f, 4, 6f);

            // The giant waterwheel over water, two walkways through it, broken at the wheel (as on the yard).
            const float wheelZ = 86f, inner = 3f, width = 3.2f, radius = 9f, outer = inner + width;
            float inside = radius - HorizontalWaterwheel.DefaultRim;
            float axle = Mathf.Sqrt(inside * inside - outer * outer) - .4f, bed = Mathf.Min(-4.5f, axle - radius - .5f);
            float gap = RoyalCarriageYard.WheelGapHalf;
            cb.Checkpoint(1, new Vector3(0f, 0f, 72f), 0f, 16f);
            Sign(s, "거대 물레방아: 구멍으로 점프 + 다이빙", new Vector3(0f, 3.2f, 72.5f));
            YardKit.Floor(s, "Pit bed 1", -20f, 20f, 76f, 96f, bed, YardKit.WallStone);
            YardKit.Picture(s, "Water", new Vector3(-20f, bed, 76f), new Vector3(20f, -1.2f, 96f), YardKit.Water);
            cb.Trigger<KillVolume>("Water (fall)", new Vector3(-20f, bed, 76f), new Vector3(20f, -1.5f, 96f));
            foreach (float side in new[] { -1f, 1f })
            {
                float x0 = side < 0f ? -outer : inner, x1 = side < 0f ? -inner : outer;
                YardKit.Span(s, "Walkway", new Vector3(x0, -.35f, 76f), new Vector3(x1, 0f, wheelZ - gap), YardKit.WoodLight).AddComponent<CourseFloor>();
                YardKit.Span(s, "Walkway", new Vector3(x0, -.35f, wheelZ + gap), new Vector3(x1, 0f, 96f), YardKit.WoodLight).AddComponent<CourseFloor>();
                YardKit.Picture(s, "Gap edge", new Vector3(x0, 0f, wheelZ - gap - .18f), new Vector3(x1, .04f, wheelZ - gap), YardKit.Gold);
                YardKit.Picture(s, "Gap edge", new Vector3(x0, 0f, wheelZ + gap), new Vector3(x1, .04f, wheelZ + gap + .18f), YardKit.Gold);
            }
            Place<HorizontalWaterwheel>(s, "Giant waterwheel", new Vector3(0f, axle, wheelZ))
                .Configure(15f, true, radius, 6, 4f, inner + width * .5f, width, axle);
            foreach (float z in new[] { wheelZ - 1.2f, wheelZ + 1.2f })
                YardKit.Span(s, "Axle post", new Vector3(-.35f, bed, z - .3f), new Vector3(.35f, axle - .3f, z + .3f), YardKit.WoodDark, Solid.NoClimb);
            YardKit.Cylinder(s, "Axle", new Vector3(0f, axle, wheelZ), Vector3.forward, .45f, 3.2f, YardKit.Iron, Solid.None);

            // The tilting bridges over a pit.
            YardKit.Floor(s, "Floor 2", -20f, 20f, 96f, 106f, 0f);
            cb.Checkpoint(2, new Vector3(0f, 0f, 100f), 0f, 14f);
            Sign(s, "목재 다리 (긴 변 경첩 / 끝 경첩)", new Vector3(0f, 3.2f, 101f));
            YardKit.Floor(s, "Pit bed 2", -20f, 20f, 106f, 116f, -6f, YardKit.WallStone);
            cb.Trigger<KillVolume>("Pit (fall)", new Vector3(-20f, -6f, 106f), new Vector3(20f, -3f, 116f));
            Place<TiltingWoodenBridge>(s, "Tilting bridge (long side)", new Vector3(-5.5f, 0f, 106f))
                .Configure(TiltingWoodenBridge.Hinge.LongSide, 10f, 3f, 40f, 1.4f, .6f, 2f, 3f);
            Place<TiltingWoodenBridge>(s, "Tilting bridge (near end)", new Vector3(6f, 0f, 106f))
                .Configure(TiltingWoodenBridge.Hinge.NearEnd, 10f, 3f, 45f, 1.4f, .6f, 2f, 3f);

            // The cargo ramp: up to y 5 and down again.
            YardKit.Floor(s, "Floor 3", -20f, 20f, 116f, 122f, 0f);
            Sign(s, "굴러오는 화물", new Vector3(0f, 3.2f, 118f));
            YardKit.Slab(s, "Ramp up", new Vector3(0f, 0f, 122f), new Vector3(0f, 5f, 142f), 8f, 6f, YardKit.Stone).AddComponent<CourseFloor>().sloped = true;
            YardKit.Span(s, "Ramp top", new Vector3(-4f, -1f, 142f), new Vector3(4f, 5f, 148f), YardKit.Stone).AddComponent<CourseFloor>();
            YardKit.Slab(s, "Ramp down", new Vector3(0f, 5f, 148f), new Vector3(0f, 0f, 160f), 8f, 6f, YardKit.Stone).AddComponent<CourseFloor>().sloped = true;
            YardKit.Span(s, "Gatehouse lintel", new Vector3(-4f, 7.4f, 138f), new Vector3(4f, 10f, 143f), YardKit.WallStone, Solid.NoClimb);
            YardKit.Span(s, "Gatehouse pillar", new Vector3(-4f, 3f, 138.5f), new Vector3(-3.4f, 10f, 140f), YardKit.WallStone, Solid.NoClimb);
            YardKit.Span(s, "Gatehouse pillar", new Vector3(3.4f, 3f, 138.5f), new Vector3(4f, 10f, 140f), YardKit.WallStone, Solid.NoClimb);
            float yTop = 5f * (140f - 122f) / 20f;
            var down = new Vector3(0f, -yTop, 122f - 140f);
            var chute = Place<CargoChute>(s, "Cargo chute", new Vector3(0f, yTop, 140f));
            chute.transform.localRotation = Quaternion.LookRotation(down.normalized, Vector3.up);
            chute.Configure(1.7f, 5f, new[] { 1f, 1.4f, 1.8f }, down.magnitude, 7f, 3.4f, 0f, 5, 6f);
            YardKit.Floor(s, "Floor 4", -20f, 20f, 160f, 168f, 0f);

            // The lifting wheel: the same fit as on the yard (landing edge 4.7 m before the axle, cliff 6 m after it).
            const float lz = 172.7f;
            cb.Checkpoint(3, new Vector3(0f, 0f, 165f), 0f, 10f);
            Sign(s, "세로 물레방아 (우클릭으로 매달리기)", new Vector3(0f, 3.2f, 164f));
            YardKit.Floor(s, "Pool bed", -6f, 6f, lz - 4.7f, lz + 6f, -3f, YardKit.WallStone);
            YardKit.Floor(s, "Pool side W", -20f, -6f, lz - 4.7f, lz + 6f, 0f);
            YardKit.Floor(s, "Pool side E", 6f, 20f, lz - 4.7f, lz + 6f, 0f);
            YardKit.Picture(s, "Pool water", new Vector3(-6f, -3f, lz - 4.7f), new Vector3(6f, -.5f, lz + 6f), YardKit.Water);
            cb.Trigger<KillVolume>("Pool (fall)", new Vector3(-6f, -3f, lz - 4.7f), new Vector3(6f, -.8f, lz + 6f));
            Place<VerticalWaterwheel>(s, "Lifting waterwheel", new Vector3(0f, 4.6f, lz)).Configure(20f, 5.2f, 8, 30f, 4f, 3.5f);
            foreach (float x in new[] { -2.5f, 2.5f })
                YardKit.Span(s, "Pier", new Vector3(x - .5f, -3f, lz - .7f), new Vector3(x + .5f, 4.3f, lz + .7f), YardKit.WallStone, Solid.NoClimb);
            YardKit.Span(s, "Cliff", new Vector3(-20f, -3f, lz + 6f), new Vector3(20f, 8f, lz + 6.6f), YardKit.Marble, Solid.NoClimb).AddComponent<CourseFloor>();
            YardKit.Span(s, "Platform", new Vector3(-20f, -3f, lz + 6.6f), new Vector3(20f, 8f, 210f), YardKit.Stone).AddComponent<CourseFloor>();
            YardKit.Span(s, "Landing", new Vector3(-3.5f, 7.7f, lz + 4.4f), new Vector3(3.5f, 8f, lz + 6f), YardKit.WoodLight).AddComponent<CourseFloor>();

            // The push gate on the platform.
            cb.Checkpoint(4, new Vector3(0f, 8f, 186f), 0f, 14f);
            Sign(s, "밀어서 여는 성문 (우클릭을 누른 채로)", new Vector3(0f, 10.6f, 188f));
            YardKit.Wall(s, "Gate wall W", -20f, -4f, 8f, 17f, 197f, 199f, YardKit.WallStone);
            YardKit.Wall(s, "Gate wall E", 4f, 20f, 8f, 17f, 197f, 199f, YardKit.WallStone);
            YardKit.Wall(s, "Gate lintel", -4f, 4f, 15.3f, 17f, 197f, 199f, YardKit.WallStone);
            Place<PushGate>(s, "Push gate", new Vector3(0f, 8f, 198f)).Configure(8f, 7.2f, 3f, .6f);
            cb.Trigger<FinishZone>("Finish", new Vector3(-10f, 8f, 204f), new Vector3(10f, 11f, 210f)).Configure(true);
        }
    }
}
