using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The Royal Carriage Yard's roaming carriages as one fleet (R286, user 11-03: "넓은 공간 구역에서만 랜덤하게 돌진하며
    // 돌아다니는 마차 4개"): the carriages share the whole area and keep charging to random points anywhere in it,
    // never touching one another (Core RoamingFleet plans every leg clear of the others'). This component holds the
    // plan and builds its carriages (RoamingCarriage, each an Obstacle reading its pose from the plan).
    //
    // The area is where the carriages' centres may go, in this object's frame (keep it BodyRadius + a little inside
    // the walls). Offline the plan starts startDelay seconds after the fleet is switched on (when the yard's gate
    // opens); a match hands every PC the same start and seed (StartAt; until then the carriages wait at home) and then
    // every PC sees the same carriages, as with the grand stair hall's random rolls. Self-building; built from code
    // on an inactive object, call Configure first.
    public sealed class CarriageFleet : MonoBehaviour
    {
        [Header("Area (the carriages' centres, this object's frame)")]
        [SerializeField] float minX = -19.4f;
        [SerializeField] float maxX = 19.4f;
        [SerializeField] float minZ = -20f;
        [SerializeField] float maxZ = 20f;

        [Header("Carriages")]
        [SerializeField] int count = 4;
        [Tooltip("Top speed of the first carriage's charges (m/s)")]
        [SerializeField] float moveSpeed = 9f;
        [Tooltip("Each further carriage this much faster (m/s)")]
        [SerializeField] float speedStep = .3f;
        [Tooltip("Seconds a half turn on the spot takes; a smaller turn takes its share")]
        [SerializeField] float turnSeconds = 2.2f;
        [Tooltip("Shortest and longest wait with the lamps red before a charge (s)")]
        [SerializeField] float minAim = .6f, maxAim = 1.2f;
        [Tooltip("Shortest and longest rest after a charge (s)")]
        [SerializeField] float minRest = .4f, maxRest = 1.4f;
        [Tooltip("Shortest and longest charge (m)")]
        [SerializeField] float minCharge = 10f, maxCharge = 32f;
        [Tooltip("Seconds after it is switched on that the first carriage moves (offline)")]
        [SerializeField] float startDelay = 3f;
        [Tooltip("The plan's own random points (a match draws one of RoamingFleet.YardSeeds, StartAt)")]
        [SerializeField] int seed = 38;

        [Header("Hit")]
        [Tooltip("Push of a hit (m/s) before the carriage's speed adds 0.25 per m/s (at most 7 in all), see RoamingCarriage")]
        [SerializeField] float knockbackForce = 4f;
        [SerializeField] float knockdownSeconds = 1f;

        RoamingFleet fleet;
        bool built;

        public RoamingFleet Fleet => fleet;
        public int Count => count;

        public void Configure(float minX, float maxX, float minZ, float maxZ, int count, float moveSpeed, float turnSeconds,
                              float minAim, float maxAim, float startDelay, int seed, float knockbackForce, float knockdownSeconds)
        {
            this.minX = minX;
            this.maxX = maxX;
            this.minZ = minZ;
            this.maxZ = maxZ;
            this.count = count;
            this.moveSpeed = moveSpeed;
            this.turnSeconds = turnSeconds;
            this.minAim = minAim;
            this.maxAim = maxAim;
            this.startDelay = startDelay;
            this.seed = seed;
            this.knockbackForce = knockbackForce;
            this.knockdownSeconds = knockdownSeconds;
        }

        // A match's shared start (a clock time on ObstacleClock), after Configure, before or after the fleet is switched
        // on: the plan is made again from it, the same on every PC. Each carriage goes straight to where the new plan
        // has it (a cut, not a charge across the yard). The same start again changes nothing.
        public void StartAt(double clockTime) => StartAt(clockTime, seed);

        // The same with the round's own seed (every PC must pass the same one).
        // (Against the plan's own start, which moves when the clock leaps: handed before the switch to the match clock
        // and again after it, the start is planned again on the new clock instead of left on each PC's switch.)
        public void StartAt(double clockTime, int seed)
        {
            if (fleet != null && fleet.StartTime == clockTime && this.seed == seed) return;
            this.seed = seed;
            fleet = Plan(clockTime);
        }

        void Awake()
        {
            if (built) return;
            built = true;
            // Offline the carriages set off startDelay after the yard is built. On a shared match clock they stand at
            // home until the match hands every PC the same start (StartAt): a start of its own would differ per PC.
            if (fleet == null) fleet = Plan(ObstacleClock.Shared ? double.NaN : ObstacleClock.Now + startDelay);
            int n = Mathf.Max(1, count);
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Roaming carriage " + (i + 1));
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                // Green and blue: red is the carriages' warning (their lamps), never a body.
                go.AddComponent<RoamingCarriage>().Join(this, i, knockbackForce, knockdownSeconds, i % 2 == 0 ? 0 : 2);
                go.SetActive(true);
            }
        }

        RoamingFleet Plan(double start)
        {
            int n = Mathf.Max(1, count);
            var rules = new RoamingFleet.Rules
            {
                MinX = Mathf.Min(minX, maxX), MaxX = Mathf.Max(minX, maxX), MinZ = Mathf.Min(minZ, maxZ), MaxZ = Mathf.Max(minZ, maxZ),
                BodyRadius = RoamingCarriage.BodyRadius, Turn = Mathf.Max(.05f, turnSeconds),
                MinAim = Mathf.Max(0f, minAim), MaxAim = Mathf.Max(Mathf.Max(0f, minAim), maxAim),
                MinRest = Mathf.Max(0f, minRest), MaxRest = Mathf.Max(Mathf.Max(0f, minRest), maxRest),
                MinCharge = Mathf.Max(1f, minCharge), MaxCharge = Mathf.Max(Mathf.Max(1f, minCharge), maxCharge),
            };
            // Homes: two to a row, rows spread along the area's depth, facing the middle.
            var speeds = new double[n];
            var hx = new double[n];
            var hz = new double[n];
            var yaw = new double[n];
            int rows = (n + 1) / 2;
            for (int i = 0; i < n; i++)
            {
                speeds[i] = Mathf.Max(.5f, moveSpeed + speedStep * i);
                bool alone = n % 2 == 1 && i == n - 1;
                double side = alone ? 0 : (i % 2 == 0 ? -1 : 1);
                hx[i] = (rules.MinX + rules.MaxX) * .5 + side * (rules.MaxX - rules.MinX) * .25;
                hz[i] = rules.MinZ + (i / 2 + .5) * (rules.MaxZ - rules.MinZ) / rows;
                yaw[i] = side < 0 ? 90 : side > 0 ? -90 : 0;
            }
            return new RoamingFleet(unchecked((uint)seed), start, speeds, hx, hz, yaw, rules);
        }

        void FixedUpdate()
        {
            if (fleet != null) fleet.Prune(ObstacleClock.Now - 30.0);
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, .55f, .2f, .8f);
            Gizmos.DrawWireCube(new Vector3((minX + maxX) * .5f, .05f, (minZ + maxZ) * .5f), new Vector3(Mathf.Abs(maxX - minX), .1f, Mathf.Abs(maxZ - minZ)));
            Gizmos.color = new Color(1f, .55f, .2f, .3f);
            float r = RoamingCarriage.BodyRadius;
            Gizmos.DrawWireCube(new Vector3((minX + maxX) * .5f, YardKit.CarriageHeight * .5f, (minZ + maxZ) * .5f),
                                new Vector3(Mathf.Abs(maxX - minX) + 2f * r, YardKit.CarriageHeight, Mathf.Abs(maxZ - minZ) + 2f * r));
        }
    }
}
