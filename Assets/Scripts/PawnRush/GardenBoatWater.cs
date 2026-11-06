using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R482 (승규 11-06, "배랑 물이랑 상호작용 하게 만들어"): the garden's eight boats and the reservoir's water act on each
    // other. Drawn by every PC for itself from the shared obstacle clock (nothing sent, nothing that collides moves):
    //  - the water: each boat's waterline position, heading, speed, its sinking or rising and its rocking go to the garden
    //    water shader (PR_GardenClearWater's _GardenBoatHulls arrays) every frame, which raises a bow wave, a trough along the
    //    sides and a stern wave, a Kelvin wake behind it (crests inside the 19.47 degree wedge, wavelength 2 pi U^2 / g),
    //    rings that leave the hull and grow while it rocks, sinks or rises, and water heaped and lit against the hull;
    //    a boat going down under its tunnel, under the water or coming back up makes none (R482b: no splash, no sound);
    //  - the boats: GardenBoatRock rocks each boat's dressing on the swell (roll, pitch, heave).
    // R486 (승규 "물이랑 상호 작용 시켜"): the lake's three boats (GardenLakeBoat) go to the same arrays after the
    // reservoir's eight, so they raise the same waves on the lake (the shader keeps each to its own pool by height).
    // The old Blender garden's GardenWaterInteraction filled three hulls; R482's arrays were eight long, R486's twelve
    // under new names (_GardenBoatHulls*: Unity keeps a global array's first size for the editor session).
    [DefaultExecutionOrder(100)]
    public sealed class GardenBoatWater : MonoBehaviour
    {
        public const int Max = 12, Reservoir = 8;                        // all the shader takes, the reservoir's share
        const float HalfLength = 3f, HalfWidth = 1.2f, Hidden = .02f;   // R482b: none once it starts down
        static readonly int HullId = Shader.PropertyToID("_GardenBoatHulls");
        static readonly int AxisId = Shader.PropertyToID("_GardenBoatHullsAxis");
        static readonly int InfoId = Shader.PropertyToID("_GardenBoatHullsInfo");
        static readonly int CountId = Shader.PropertyToID("_GardenBoatHullsCount");

        [SerializeField] GardenBoat[] boats = new GardenBoat[0];
        [Tooltip("The reservoir's surface in the section's frame (this transform's parent space).")]
        [SerializeField] float waterY = 3f;

        readonly Vector4[] hull = new Vector4[Max], axis = new Vector4[Max], info = new Vector4[Max];
        Vector3[] last;
        float[] lastDrop;
        GardenBoatRock[] rock;
        bool primed;

        // For the builder: the boats (their roots, GardenBoat), the water's height (section frame).
        public void Configure(GardenBoat[] boats, float waterY)
        {
            this.boats = boats;
            this.waterY = waterY;
        }

        void OnEnable()
        {
            int n = Mathf.Min(Reservoir, boats.Length);
            last = new Vector3[n];
            lastDrop = new float[n];
            rock = new GardenBoatRock[n];
            for (int i = 0; i < n; i++)
                if (boats[i] != null) rock[i] = boats[i].GetComponent<GardenBoatRock>();
            primed = false;
        }

        void LateUpdate()
        {
            int n = Mathf.Min(Reservoir, boats.Length);
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float surface = transform.parent != null ? transform.parent.TransformPoint(0f, waterY, 0f).y : waterY;
            for (int i = 0; i < Max; i++)
                hull[i] = axis[i] = info[i] = Vector4.zero;
            for (int i = 0; i < Reservoir; i++)
            {
                if (i >= n || boats[i] == null) continue;
                var t = boats[i].transform;
                var p = t.position;
                float drop = (float)RoyalWaterGardenRules.BoatDrop(boats[i].X);
                var v = primed ? (p - last[i]) / dt : Vector3.zero;
                var flat = new Vector3(v.x, 0f, v.z);
                float speed = flat.magnitude;
                bool wrapped = speed > 30f;                   // it came round under the water to the other end
                if (wrapped) { v = Vector3.zero; flat = Vector3.zero; speed = 0f; }
                var along = t.rotation * Vector3.right;
                var forward = speed > .05f ? flat / speed : new Vector3(along.x, 0f, along.z).normalized;
                last[i] = p;
                lastDrop[i] = drop;
                // R482b (승규 "보트가 내려 갈 때는 상호 작용 안해도 돼"): a boat going down under its tunnel (or coming up) makes
                // no waves, rings or splashes; only one riding the surface does.
                if (drop > Hidden) continue;
                float rate = rock[i] != null ? rock[i].Rate : 0f;
                hull[i] = new Vector4(p.x, surface, p.z, speed);
                axis[i] = new Vector4(forward.x, forward.z, HalfLength, HalfWidth);
                info[i] = new Vector4(0f, rate, 0f, 0f);
            }
            // R486: the lake's boats (each moved itself already: GardenLakeBoat runs first)
            int count = Reservoir;
            foreach (var lake in GardenLakeBoat.All)
            {
                if (count >= Max) break;
                var w = lake.Waterline;
                hull[count] = new Vector4(w.x, w.y, w.z, lake.Speed);
                axis[count] = new Vector4(lake.Heading.x, lake.Heading.z, GardenLakeBoat.HalfLength, GardenLakeBoat.HalfWidth);
                info[count] = new Vector4(0f, lake.RockRate, 0f, 0f);
                count++;
            }
            primed = true;
            Shader.SetGlobalVectorArray(HullId, hull);
            Shader.SetGlobalVectorArray(AxisId, axis);
            Shader.SetGlobalVectorArray(InfoId, info);
            Shader.SetGlobalFloat(CountId, count);
        }

        void OnDisable() => Shader.SetGlobalFloat(CountId, 0f);
    }

    // R482: a boat's dressing rocks on the swell - roll, pitch and heave of its own phase, a pure function of the shared
    // obstacle clock (HANDOFF rule 7), so every PC sees the same; only the drawn boat moves (its hull's collider rides the
    // mover still). The dressing is the child of the same name hung on the mover by ConceptDressing (R437).
    public sealed class GardenBoatRock : MonoBehaviour
    {
        const float Roll = 1.6f, Pitch = .8f, Heave = .04f;    // degrees, degrees, metres
        const float RollPeriod = 3.3f, PitchPeriod = 4.4f, HeavePeriod = 2.9f;
        Transform dress;
        Vector3 basePosition;
        Quaternion baseRotation;
        float phase;

        // How fast it is rolling and pitching (rad/s), for the water's rings.
        public float Rate { get; private set; }

        void Start()
        {
            foreach (Transform child in transform)
                if (child.name == name) dress = child;
            if (dress == null) return;
            basePosition = dress.localPosition;
            baseRotation = dress.localRotation;
            int h = 17;
            foreach (char ch in name) h = h * 31 + ch;           // the same on every PC (string hashes are not)
            phase = (h & 1023) * .0137f;
        }

        void LateUpdate()
        {
            if (dress == null) return;
            float t = (float)(ObstacleClock.Now % 3600.0) + phase;
            float wr = 2f * Mathf.PI / RollPeriod, wp = 2f * Mathf.PI / PitchPeriod, wh = 2f * Mathf.PI / HeavePeriod;
            float roll = Roll * (Mathf.Sin(wr * t) + .3f * Mathf.Sin(2.3f * wr * t + 1.1f));
            float pitch = Pitch * Mathf.Sin(wp * t + .7f);
            float heave = Heave * Mathf.Sin(wh * t + 2.1f);
            Rate = Mathf.Deg2Rad * (Mathf.Abs(Roll * wr * Mathf.Cos(wr * t)) + Mathf.Abs(Pitch * wp * Mathf.Cos(wp * t + .7f)));
            // about the mover's own axes and origin (the deck's middle): its long axis is x - roll about it, pitch about
            // the beam (z); the dressing's own frame is the FBX's, so it is turned from outside
            var turn = Quaternion.Euler(roll, 0f, pitch);
            dress.localRotation = turn * baseRotation;
            dress.localPosition = turn * basePosition + Vector3.up * heave;
        }
    }
}
