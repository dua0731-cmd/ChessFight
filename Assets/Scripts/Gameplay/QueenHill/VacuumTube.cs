using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // A team's vacuum tube (8th revision, R50): a glass column beside the team's
    // stair of rank plazas. Walking into its mouth on rank 1 sucks the character up
    // to the highest rank IT has stepped on (QueenHillRace.TubeTarget) and puts it
    // out on that rank's station. A teammate's progress never carries anyone, and
    // the other team's players are not taken.
    //
    // The ride moves the character through ICharacterDriver.Teleport every physics
    // step (up the axis, then out of the door), so it works for any character. The
    // box on this object is the mouth, a trigger; it only notes who walked in, and
    // the ride starts in FixedUpdate (no teleport inside a physics callback).
    [RequireComponent(typeof(BoxCollider))]
    public sealed class VacuumTube : MonoBehaviour
    {
        [SerializeField] int team;
        [Tooltip("Metres a second up the tube.")]
        [SerializeField] float speed = 32f;
        [Tooltip("Where the tube lets characters out, by rank (index 0 and 1 unused).")]
        [SerializeField] Transform[] stations = new Transform[0];

        sealed class Ride
        {
            public ICharacterDriver who;
            public Vector3 from, top, to;
            public Quaternion facing;
            public float started, rise, outTime;
        }

        readonly List<ICharacterDriver> waiting = new List<ICharacterDriver>();
        readonly Dictionary<ICharacterDriver, Ride> riding = new Dictionary<ICharacterDriver, Ride>();
        readonly List<ICharacterDriver> done = new List<ICharacterDriver>();

        public int Team => team;
        public int Riders => riding.Count;
        public int Rides { get; private set; }

        public void Configure(int team, float speed, Transform[] stations)
        {
            this.team = team;
            this.speed = Mathf.Max(1f, speed);
            this.stations = stations;
        }

        void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            var driver = other.GetComponentInParent<ICharacterDriver>();
            if (driver == null || riding.ContainsKey(driver) || waiting.Contains(driver)) return;
            if (QueenHillRaceMatch.TeamOf(driver) != team) return;
            waiting.Add(driver);
        }

        void FixedUpdate()
        {
            var match = QueenHillRaceMatch.Current;
            foreach (var who in waiting) Begin(who, match);
            waiting.Clear();

            done.Clear();
            foreach (var pair in riding)
            {
                var ride = pair.Value;
                if (pair.Key is Object o && o == null) { done.Add(pair.Key); continue; }
                float t = Time.time - ride.started;
                Vector3 at;
                if (t < ride.rise)
                {
                    // Ease in and out up the axis: a smooth whoosh, not a jolt.
                    float u = t / ride.rise;
                    u = u * u * (3f - 2f * u);
                    at = Vector3.Lerp(ride.from, ride.top, u);
                }
                else if (t < ride.rise + ride.outTime)
                    at = Vector3.Lerp(ride.top, ride.to, (t - ride.rise) / ride.outTime);
                else
                {
                    pair.Key.Teleport(QueenHillRaceMatch.FreeSpot(ride.to, ride.facing), ride.facing);
                    done.Add(pair.Key);
                    continue;
                }
                pair.Key.Teleport(at, ride.facing);
            }
            foreach (var who in done) riding.Remove(who);
        }

        void Begin(ICharacterDriver who, QueenHillRaceMatch match)
        {
            if (match == null) return;
            int rank = match.TubeTarget(who);
            if (rank < 2 || stations == null || rank >= stations.Length || stations[rank] == null) return;
            var station = stations[rank];
            Vector3 axis = transform.position;
            var ride = new Ride
            {
                who = who,
                from = new Vector3(axis.x, transform.position.y - GetComponent<BoxCollider>().size.y * 0.5f + 0.2f, axis.z),
                to = station.position,
                facing = station.rotation,
                started = Time.time,
            };
            ride.top = new Vector3(axis.x, station.position.y + 0.3f, axis.z);
            ride.rise = Mathf.Max(0.6f, (ride.top.y - ride.from.y) / speed + 0.5f);
            ride.outTime = 0.35f;
            riding[who] = ride;
            Rides++;
        }
    }
}
