using System;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R684 (승규 11-16 "물 그래픽도 수학적으로 계산해서 물래방아에서 떨어지는 물 등등 해서 그래픽 좋게 넣어"): the yard's water,
    // worked out instead of painted, made in code at the very end of Awake (after the light) under "Water look" beside
    // "Built". The code's water boxes, the flat pour and spill sheets, the scrolling cliff and sluice sheets and the white
    // foam cards stop drawing (they stay in the hierarchy; switching "Water look" off shows them again) and in their place:
    //  - the canal, the flume's trough and the mill pond: PR_YardWater (Gerstner swell on the finite-depth dispersion
    //    relation, Doppler-shifted by the current; ripples and foam carried by the current; Beer-Lambert colour from the
    //    concept's #3D9EBD to #0A3861; Schlick-Fresnel reflections of the yard's probes; white foam along everything
    //    standing in the water and under every fall; rings from every landing and every pawn that falls in);
    //  - the giant wheel: the flume's sheet pouring onto its top, the buckets tipping out down its falling (east) side as
    //    streams that leave at the lip's speed and fall as parabolas, thin as they speed up, break into drops and land in
    //    foam; the rising buckets draining as they come up out of the canal; spray where its rim goes in;
    //  - the lifting wheel: each paddle that comes up out of the pond running off in a sheet, then dripping; spray where
    //    a paddle goes in;
    //  - the cliff's fall and the two sluices: nappes thrown out from their lips, round jets breaking out of them, domes
    //    of white water and drops where they land;
    //  - a pawn that falls into the canal or the pond splashes (the garden's splash) and rings the water.
    // The maths is in YardWaterfall.cs and PR_YardWater.shader; the currents: the canal runs west (the giant wheel's foot
    // sweeps west - it turns clockwise seen from the south, its top going east), the pond south (away from the falls),
    // the flume's water towards its mouth over the wheel. There is no WaterCurrent in the yard (falling in is the kill
    // volume); one put over a water box later sets that water's current instead (YardWater.AddSurface).
    // Docs/PawnRush/RCY_MODELS.md "R684 물": the cost table and what to look at.
    public sealed partial class RoyalCarriageYard
    {
        [Serializable]
        public sealed class WaterLookSettings
        {
            [Tooltip("The worked-out water (R684). Off: the flat water boxes and the scrolling sheets as before")]
            public bool on = true;
            [Tooltip("The most the quality settings may turn on: 0 low, 1 medium, 2 high")]
            [Range(0, 2)] public int maxTier = 2;
            [Tooltip("The canal's current along the map's x (m/s; west is -): the giant wheel's foot sweeps it west")]
            public float canalFlow = -.3f;
            [Tooltip("The mill pond's current along z (m/s; south is -): away from the falls")]
            public float pondFlow = -.35f;
            [Tooltip("The flume's water towards its mouth over the giant wheel (m/s)")]
            public float flumeFlow = 1.1f;
            [Tooltip("Swell height (m) and its main wavelength (m) on the canal and the pond")]
            public float waveHeight = .028f, waveLength = 3.4f;
            [Tooltip("Degrees past the top at which the giant wheel's buckets start to tip out (they are empty by 132 deg, "
                     + "or 6 deg before they reach the water). Under ~65 the streams fall back through the buckets below")]
            [Range(10f, 110f)] public float spillFrom = 65f;
            [Tooltip("Speed the water leaves a bucket at, on top of the bucket's own (m/s)")]
            public float spillOut = .35f;
            [Tooltip("Speed the falls leave the cliff's lip and the sluices' sills (m/s): the nappes land u sqrt(2 h / g) out")]
            public float cliffLip = .7f, sluiceLip = 1f;
            [Tooltip("A pawn falling into the canal or the pond splashes (the garden's splash) and rings the water")]
            public bool splashes = true;
            [Tooltip("A pawn falling into the canal or the pond floats at the surface (hips 0.2 m under) until it is put "
                     + "back, the camera staying over the water (승규 11-16: it sank to the bed and the camera went under). "
                     + "Works with the look off too")]
            public bool floatOnFall = true;
        }

        [Header("Look: water")]
        [SerializeField] WaterLookSettings waterLook = new WaterLookSettings();

        void Water()
        {
            if (waterLook.floatOnFall)
            {
                var canalGroup = root.Find("2 Canal");
                if (canalGroup != null) CatchAtSurface(canalGroup, "canal", -YardHalf, YardHalf, CanalZ0, CanalZ1, CanalWater);
                var pondGroup = root.Find("3-4 (own frame)/3 Loading and sorting yard/Waterfall and lifting wheel");
                if (pondGroup != null) CatchAtSurface(pondGroup, "mill pond", -SortHalf, SortHalf, PoolZ0, CliffZ, -.5f);
            }
            if (!waterLook.on) return;
            var surface = Resources.Load<Material>("PR_YardWater");
            var sheet = Resources.Load<Material>("PR_GardenFallSheet");
            if (surface == null || sheet == null)
            {
                Debug.LogWarning("[마차뜰 물] Resources의 PR_YardWater / PR_GardenFallSheet 재질이 없어 물을 예전 그림대로 둡니다");
                return;
            }
            var go = new GameObject("Water look");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var water = go.AddComponent<YardWater>();
            water.Configure(waterLook.maxTier);
            var canal = root.Find("2 Canal");
            if (canal != null) CanalWaterLook(water, canal, surface, sheet);
            var lifts = root.Find("3-4 (own frame)/3 Loading and sorting yard/Waterfall and lifting wheel");
            if (lifts != null) PondWaterLook(water, lifts, surface, sheet);
            go.SetActive(true);
            Debug.Log("[마차뜰 물] 물 표면 " + water.SurfaceCount + "개(운하 · 수로 · 연못), 떨어지는 물 " + water.FallCount
                      + "곳(거대 물레방아 · 세로 물레방아와 절벽 폭포 · 수문 2)" + (waterLook.splashes ? ", 빠지면 물보라" : ""));
        }

        // Zone 2: the canal, the flume, the giant wheel's water (map frame).
        void CanalWaterLook(YardWater water, Transform canal, Material surface, Material sheet)
        {
            var box = Child<Renderer>(canal, "Water");
            float depth = box != null ? Mathf.Abs(box.transform.localScale.y) : 3.3f;
            water.AddSurface("canal", box, surface, new Vector3(waterLook.canalFlow, 0f, 0f), waterLook.waveHeight, waterLook.waveLength, depth, .32f, root);
            water.AddSurface("flume", Child<Renderer>(canal, "Flume water"), surface, new Vector3(0f, 0f, -waterLook.flumeFlow), .006f, .8f, .3f, .08f, root);
            foreach (var name in new[] { "Pour", "Spill", "Foam" }) water.Hide(Child<Renderer>(canal, name));

            var wheel = canal.GetComponentInChildren<HorizontalWaterwheel>(true);
            if (wheel == null) return;
            var falls = YardWaterfall.Create(canal, "Water falling (giant wheel)", water, sheet, CanalWater);
            float axle = wheel.transform.localPosition.y, radius = crossing.wheelRadius;
            float sign = crossing.clockwise ? -1f : 1f;
            // 28 buckets (HorizontalWaterwheel's), the lip at the boards' outer edge, 0.3 m either side of the middle.
            falls.AddWheel(wheel.transform, Vector3.forward, Vector3.up, radius - .1f, 28, HorizontalWaterwheel.DefaultDepth * .5f - .2f,
                           sign * Mathf.Abs(crossing.rotationSpeed), true, waterLook.spillFrom, waterLook.spillOut, .07f, 1.6f, 10f, true);
            // The flume's mouth (Flume(): its floor's top 0.55 m over the wheel's top, the water 0.3 deep in it) pours a
            // 1.2 m sheet over its end onto the buckets at the top, 0.25 m/s out: it lands 0.1 m on, inside the wheel.
            float floor = axle + radius + .55f;
            falls.AddSheet(new Vector3(-.6f, floor + .15f, WheelZ - .3f), new Vector3(.6f, floor + .15f, WheelZ - .3f), Vector3.back, .25f,
                           axle + radius - .05f, false, 0, .23f);
            falls.Finish();
            if (waterLook.splashes) SplashSlab(water, canal, "canal", -YardHalf, YardHalf, CanalZ0, CanalZ1, CanalWater);
        }

        // Zone 3's end: the mill pond, the lifting wheel, the cliff's fall and the two sluices (own frame).
        void PondWaterLook(YardWater water, Transform lifts, Material surface, Material sheet)
        {
            const float pond = -.5f;   // Lifts(): the pool's water
            water.AddSurface("mill pond", Child<Renderer>(lifts, "Pool water"), surface, new Vector3(0f, 0f, waterLook.pondFlow),
                             waterLook.waveHeight * 1.1f, waterLook.waveLength * .9f, 2.5f, .32f, root);
            foreach (var name in new[] { "Waterfall", "Foam" }) water.Hide(Child<Renderer>(lifts, name));

            var middle = YardWaterfall.Create(lifts, "Water falling (lifting wheel, cliff)", water, sheet, pond);
            var wheel = lifts.GetComponentInChildren<VerticalWaterwheel>(true);
            if (wheel != null)
                middle.AddWheel(wheel.transform, Vector3.right, Vector3.up, LiftRadius - .05f, lift.bladeCount, 1.45f,
                                Mathf.Abs(lift.rotationSpeed), false, 0f, 0f, 0f, 1.1f, 18f, true);
            // The cliff's fall (Lifts(): x -5..5 from under the wheel's landing, 0.12 m off the marble): thrown out at
            // cliffLip it lands cliffLip sqrt(2 x 8.2 / g) = 0.91 m out (0.7 m/s), and passes the lifting wheel's
            // paddles, 0.8 m off the cliff at the axle's height, 0.68 m off the marble (its six jets, 0.08 m/s faster, 0.74).
            middle.AddSheet(new Vector3(-5f, Top - .3f, CliffZ - .12f), new Vector3(5f, Top - .3f, CliffZ - .12f), Vector3.back, waterLook.cliffLip,
                            pond, true, 6, .37f, .12f);
            middle.Finish();

            foreach (float side in new[] { -1f, 1f })
            {
                var dock = lifts.Find(side < 0f ? "Basin dock W" : "Basin dock E");
                if (dock == null) continue;
                foreach (var name in new[] { "Sluice water", "Sluice foam" }) water.Hide(Child<Renderer>(dock, name));
                // BasinDock(): the gate raised to 3.4, the water over its sill 4 m wide; at sluiceLip it lands
                // sluiceLip sqrt(2 x 3.85 / g) = 0.89 m out (1 m/s; its jets 0.96), short of the moored barge's side
                // (1.6 m off the cliff, the sill 0.58).
                float cx = side * 15.5f;
                var fall = YardWaterfall.Create(dock, side < 0f ? "Water falling (sluice W)" : "Water falling (sluice E)", water, sheet, pond);
                fall.AddSheet(new Vector3(cx - 2f, 3.35f, CliffZ - .58f), new Vector3(cx + 2f, 3.35f, CliffZ - .58f), Vector3.back, waterLook.sluiceLip,
                              pond, true, 3, side < 0f ? .61f : .83f, .24f);
                fall.Finish();
            }
            if (waterLook.splashes) SplashSlab(water, lifts, "mill pond", -SortHalf, SortHalf, PoolZ0, CliffZ, pond);
        }

        // 승규 11-16 ("물에 빠지면 아예 이렇게 내려가게 되 있어": a pawn that fell in sank to the canal's bed, 3.3 m down,
        // and the camera followed it under the surface, where the water is not drawn): the garden's Pool(), R442. A
        // WaterZone from the surface 2.2 m down that reports nothing - the pawn floats in it, hips 0.2 m under
        // (RagdollPawn.Float), and the following camera stays 0.3 m over it (OrbitCamera.FloorAt) - and a kill volume
        // 0.1 .. 0.4 m under the surface, as the garden's, since the yard's own (0.3 m under, from Canal() / Lifts()) is
        // below a floating pawn's base (its hips are only 0.256 m over its feet). Both put it back as before: 1.5 s,
        // the last checkpoint.
        void CatchAtSurface(Transform parent, string name, float x0, float x1, float z0, float z1, float surfaceY)
        {
            Trigger<WaterZone>(parent, "Water (" + name + ")", new Vector3(x0, surfaceY - 2.2f, z0), new Vector3(x1, surfaceY, z1)).Report = false;
            Trigger<KillVolume>(parent, "Kill volume (" + name + ", at the surface)", new Vector3(x0, surfaceY - .4f, z0), new Vector3(x1, surfaceY - .1f, z1));
        }

        // A thin trigger slab on a water's surface (the garden's Pool(): 0.6 m under, 0.05 over): the garden's splash and
        // the yard water's rings where a pawn goes in.
        void SplashSlab(YardWater water, Transform parent, string name, float x0, float x1, float z0, float z1, float surfaceY)
        {
            const float under = .6f, over = .05f;
            var pool = Trigger<GardenSplashPool>(parent, "Splash (" + name + ")", new Vector3(x0, surfaceY - under, z0), new Vector3(x1, surfaceY + over, z1));
            pool.Configure((under - over) * .5f);
            pool.gameObject.AddComponent<YardWaterSplash>().Configure(water, (under - over) * .5f);
            water.Own(pool.gameObject);
        }

        static T Child<T>(Transform parent, string name) where T : Component
        {
            var t = parent.Find(name);
            return t != null ? t.GetComponent<T>() : null;
        }
    }
}
