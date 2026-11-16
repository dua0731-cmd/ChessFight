using System;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // A counterweighted loft hoist (도르래 승강대, R662, the user: "다른 매커니즘의 장애물이 한두개 정도는 더"): two pallets on
    // ropes from a lucam - the hoist arm over a storehouse loft's edge - either side of its post, one going up while the
    // other comes down, as a counterweighted pair does. A pallet sits on the floor and is walked onto, rises to the loft
    // deck, sits there and is walked off, and comes down again; its partner does the same half a period later, so one of
    // the pair is always down to board while the other is up at the deck (SortingYardRules.Top, a pure function of
    // ObstacleClock). The loft's face is too high to climb (the caller's), so the pallets are the way up: the danger is the
    // yard's oldest rule, never stand under a suspended load - a pallet coming down shoves whoever is under it out of its
    // well and knocks them down (HoistPallet).
    //
    // Warned: a lantern on the arm over each pallet shows green glass while it is down with BoardWarn seconds and more
    // left, amber in its last BoardWarn seconds, red while it moves or is up (a flame's flicker, R685); the pallet's shadow
    // deepens over its well's floor boards from WellWarn seconds before it starts down until it lands. A ratchet clacks as each pair swaps. Mirrored pairs,
    // the same timing, so neither team's side is luckier.
    //
    // This object is the post's foot, on the floor in the middle of the plates' depth; `outward` is +1 when its outer
    // pallet is to +x (the yard's east pair), -1 for the west one. Built from code on an inactive object: Configure, then
    // switch it on.
    public sealed class PalletHoist : MonoBehaviour
    {
        [Tooltip("+1: the outer pallet to +x of the post (the east pair); -1: to -x")]
        [SerializeField] float outward = 1f;
        [Tooltip("A plate across (x), thick (y), deep (z) (m)")]
        [SerializeField] Vector3 plateSize = new Vector3(2.37f, .22f, 2.4f);
        [Tooltip("The post between the wells, and the gap between it and each plate (m)")]
        [SerializeField] float postWidth = .4f;
        [SerializeField] float gap = .03f;
        [SerializeField] float armHeight = 6f;
        [SerializeField] float down = (float)SortingYardRules.HoistDown;
        [SerializeField] float move = (float)SortingYardRules.HoistMove;
        [SerializeField] float low = (float)SortingYardRules.HoistLow;
        [SerializeField] float high = (float)SortingYardRules.HoistHigh;
        [Tooltip("Push of a pallet coming down onto someone (m/s), out of the well")]
        [SerializeField] float push = 4.5f;

        const float BoardWarn = .8f, WellWarn = 1f;
        // R685 (실사 예고): the lamps are coloured-glass lanterns with a flame's flicker, the well's boards plain weathered
        // ochre paint, and the pallet's shadow deepens over them as it is about to come down (was neon and flashing).
        static readonly Color Green = YardWarn.Go, Amber = YardWarn.Wait, Red = YardWarn.Stop;
        static readonly Color Stripe = new Color(.6f, .47f, .27f, .42f), WellShadow = YardWarn.With(YardWarn.Stop, .55f);   // the lamps' red

        Transform course;
        HoistPallet[] pallets = new HoistPallet[0];
        Transform[] ropes = new Transform[0];
        Material[] lampMaterials = new Material[0], wellMaterials = new Material[0];
        AudioSource clack;
        double clackedSwap = double.NaN;
        bool built;

        public Vector3 PlateSize => plateSize;
        public float Push => push;
        public Transform Course => course;
        public int Count => pallets.Length;
        public HoistPallet Pallet(int k) => pallets[k];
        public float DeckY => transform.position.y + high;

        // Pallet 0 is the inner one (toward the yard's middle), 1 the outer; the outer runs half a period after the inner.
        public SortingYardRules.Hoist Schedule(int k) => new SortingYardRules.Hoist
        {
            Down = down, Move = move, Low = low, High = high, Phase = k == 0 ? 0.0 : down + move,
        };

        // Which way from a pallet's middle its outer side lies (where its sacks are): away from the post.
        public float OuterSide(int k) => k == 0 ? -outward : outward;

        // A pallet's middle across, in this object's frame.
        float PlateX(int k) => (k == 0 ? -outward : outward) * (postWidth * .5f + gap + plateSize.x * .5f);

        public void Configure(float outward, Vector3 plateSize, float postWidth, float gap, float armHeight, float down, float move, float low, float high,
                              float push, Transform course)
        {
            this.outward = Mathf.Sign(outward);
            this.plateSize = plateSize;
            this.postWidth = postWidth;
            this.gap = gap;
            this.armHeight = armHeight;
            this.down = Mathf.Max(1.2f, down);
            this.move = Mathf.Max(.8f, move);
            this.low = low;
            this.high = high;
            this.push = push;
            this.course = course;
            Build();
        }

        // ------------------------------------------------------------------ for the test bots

        public float Top(int k, double time) => (float)SortingYardRules.Top(Schedule(k), time);
        public float BoardingFor(int k, float ahead) => (float)SortingYardRules.BoardingFor(Schedule(k), ObstacleClock.Now + ahead);
        public float NextBoarding(int k, float ahead) => (float)SortingYardRules.NextBoarding(Schedule(k), ObstacleClock.Now + ahead);
        public float ComesDownIn(int k, float ahead) => (float)SortingYardRules.ComesDownIn(Schedule(k), ObstacleClock.Now + ahead);

        // A pallet's well in the world: its footprint from the floor to the deck.
        public Bounds Well(int k)
        {
            var centre = transform.TransformPoint(new Vector3(PlateX(k), high * .5f, 0f));
            return new Bounds(centre, new Vector3(plateSize.x, high, plateSize.z));
        }

        // Where to wait for a pallet: 0.6 m south of its well's middle, on the floor.
        public Vector3 Mouth(int k) => transform.TransformPoint(new Vector3(PlateX(k), 0f, -plateSize.z * .5f - .6f));

        // ------------------------------------------------------------------ building

        void Build()
        {
            if (built) return;
            built = true;
            float top = high + 1.2f;
            // The post between the wells, up to a metre over the deck (never climbed, nobody steps over it from a pallet),
            // and on at its north end up to the arm.
            YardKit.Box(transform, "Hoist post", new Vector3(0f, top * .5f, 0f), new Vector3(postWidth, top, plateSize.z), YardKit.WoodDark, Solid.NoClimb);
            YardKit.Box(transform, "Lucam post", new Vector3(0f, (top + armHeight + .4f) * .5f, plateSize.z * .5f - .2f), new Vector3(postWidth, armHeight + .4f - top, .4f),
                        YardKit.WoodDark, Solid.None);
            float span = postWidth + 2f * (gap + plateSize.x);
            YardKit.Box(transform, "Lucam arm", new Vector3(0f, armHeight, 0f), new Vector3(span + .3f, .3f, .3f), YardKit.WoodDark, Solid.None);
            YardKit.Box(transform, "Arm trim", new Vector3(0f, armHeight + .17f, 0f), new Vector3(span + .3f, .05f, .32f), YardKit.Gold, Solid.None);
            YardKit.Box(transform, "Lucam roof", new Vector3(0f, armHeight + .55f, .2f), new Vector3(span + .6f, .14f, 1.2f), YardKit.Roof, Solid.None,
                        Quaternion.Euler(-12f, 0f, 0f));

            pallets = new HoistPallet[2];
            ropes = new Transform[8];
            lampMaterials = new Material[2];
            wellMaterials = new Material[2];
            for (int k = 0; k < 2; k++)
            {
                float x = PlateX(k);
                var go = new GameObject(k == 0 ? "Hoist pallet (inner)" : "Hoist pallet (outer)");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(x, low, 0f);
                var pallet = go.AddComponent<HoistPallet>();
                pallet.Configure(this, k);
                pallets[k] = pallet;

                // The pulley over the pallet, its lamp, its four ropes (drawn from the plate's height each frame).
                YardKit.Cylinder(transform, "Pulley", new Vector3(x, armHeight - .35f, 0f), Vector3.forward, .5f, .12f, YardKit.Iron, Solid.None);
                // R685 (승규 "신호등도 그래픽 더 좋게"): a lantern on the arm's front, its glass the colour set below.
                lampMaterials[k] = YardLantern.GlassMaterial("M_YardHoistLamp", Red);
                YardLantern.Build(transform, "Hoist lamp", new Vector3(x, armHeight - .12f, -.36f), .3f, lampMaterials[k]);
                for (int r = 0; r < 4; r++)
                    ropes[k * 4 + r] = YardKit.Box(transform, "Hoist rope", Vector3.zero, new Vector3(.05f, 1f, .05f), YardKit.WoodDark, Solid.None).transform;

                // The well's floor: painted boards (the pallet's shadow deepens over them when it will come down on them).
                wellMaterials[k] = new Material(Arena.Glow(Stripe)) { name = "M_YardHoistWell" };
                for (float z = -plateSize.z * .5f + .2f; z < plateSize.z * .5f; z += .5f)
                {
                    var board = YardKit.Box(transform, "Well board", new Vector3(x, .012f, z), new Vector3(plateSize.x - .1f, .02f, .22f), wellMaterials[k], Solid.None);
                    board.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                YardKit.Box(transform, "Well edge", new Vector3(x, .011f, -plateSize.z * .5f - .05f), new Vector3(plateSize.x, .02f, .1f), YardKit.Gold, Solid.None);
            }

            clack = gameObject.AddComponent<AudioSource>();
            clack.clip = ClackSound;
            clack.playOnAwake = false;
            clack.spatialBlend = 1f;
            clack.rolloffMode = AudioRolloffMode.Linear;
            clack.minDistance = 3f;
            clack.maxDistance = 25f;
            clack.dopplerLevel = 0f;
            SectionSoundGate.Gate(clack);
        }

        void LateUpdate()
        {
            if (pallets.Length == 0) return;
            double now = ObstacleClock.Now;
            for (int k = 0; k < pallets.Length; k++)
            {
                var h = Schedule(k);
                // The ropes, from the pulley to the plate's corners where the plate is drawn.
                float x = PlateX(k), plateTop = pallets[k] != null ? transform.InverseTransformPoint(pallets[k].transform.position).y : (float)SortingYardRules.Top(h, now);
                for (int r = 0; r < 4; r++)
                {
                    float cx = x + (r % 2 == 0 ? -1f : 1f) * (plateSize.x * .5f - .12f), cz = (r < 2 ? -1f : 1f) * (plateSize.z * .5f - .12f);
                    var a = new Vector3(x + (cx - x) * .15f, armHeight - .45f, cz * .15f);
                    var b = new Vector3(cx, plateTop + .05f, cz);
                    var along = b - a;
                    ropes[k * 4 + r].localPosition = (a + b) * .5f;
                    ropes[k * 4 + r].localRotation = Quaternion.FromToRotation(Vector3.up, along.normalized);
                    ropes[k * 4 + r].localScale = new Vector3(.05f, along.magnitude, .05f);
                }
                // The lamp: green to board, amber in its last BoardWarn seconds, red moving or up.
                float boarding = (float)SortingYardRules.BoardingFor(h, now);
                var lamp = boarding > BoardWarn ? Green : boarding > 0f ? Amber : Red;
                float flame = YardWarn.Flicker(now, k * 1.7f + outward * 3.1f);
                lampMaterials[k].color = new Color(lamp.r * flame, lamp.g * flame, lamp.b * flame, .92f);
                // The well: the pallet's shadow deepening over it from WellWarn before it starts down until it is down.
                float downIn = (float)SortingYardRules.ComesDownIn(h, now);
                bool coming = !SortingYardRules.IsDown(h, now) && (downIn <= WellWarn);
                wellMaterials[k].color = coming ? Color.Lerp(Stripe, WellShadow, YardWarn.Deepen(1f - downIn / WellWarn)) : Stripe;
            }
            // A ratchet clack as each swap starts (once per swap).
            var first = Schedule(0);
            double half = first.Down + first.Move, t = now;
            double swap = Math.Floor(t / half) * half + first.Down;   // the next or last moment the pair starts moving
            if (t >= swap && t < swap + .2 && swap != clackedSwap)
            {
                clackedSwap = swap;
                if (clack != null) clack.PlayOneShot(clack.clip, 1f);
            }
        }

        void OnDestroy()
        {
            foreach (var m in lampMaterials)
                if (m != null) Destroy(m);
            foreach (var m in wellMaterials)
                if (m != null) Destroy(m);
        }

        static AudioClip clackClip;

        // A wooden ratchet's clack and the rope taking the load: a few hard knocks with a low thump, a third of a second.
        static AudioClip ClackSound
        {
            get
            {
                if (clackClip != null) return clackClip;
                const int rate = 22050, length = rate / 3;
                var random = new System.Random(662);
                var data = new float[length];
                float peak = 1e-4f, last = 0f;
                for (int i = 0; i < length; i++)
                {
                    float t = (float)i / rate, white = (float)(random.NextDouble() * 2.0 - 1.0);
                    float knock = 0f;
                    for (int k = 0; k < 4; k++)
                    {
                        float at = k * .055f;
                        if (t >= at) knock += (white - last) * Mathf.Exp(-(t - at) / .006f) * (1f - k * .15f);
                    }
                    last = white;
                    float thump = .5f * Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t / .08f);
                    data[i] = knock + thump;
                    peak = Mathf.Max(peak, Mathf.Abs(data[i]));
                }
                for (int i = 0; i < length; i++) data[i] *= .75f / peak;
                clackClip = AudioClip.Create("YardHoistClack", length, 1, rate, false);
                clackClip.SetData(data, 0);
                return clackClip;
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, .55f, .2f, .5f);
            for (int k = 0; k < 2; k++)
                Gizmos.DrawWireCube(new Vector3(PlateX(k), high * .5f, 0f), new Vector3(plateSize.x, high, plateSize.z));
        }
    }
}
