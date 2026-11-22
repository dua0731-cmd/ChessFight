using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A fixed comb beside a wall that carries its climbers (the royal clock tower's carillon drum, R775): whoever touches it
    // lets go of the wall and falls. It does not move, does not knock anyone down and takes no stamina - the wall brings the
    // climber to it, and the climber has to be at another height by then. A trigger box; only the PC that simulates the
    // character judges it (YardKit.Movable: offline, or the host of a match).
    //
    // The hit is given in FixedUpdate, not in the trigger callback: letting go destroys the hands' joints, and Unity refuses
    // that inside a physics callback (HANDOFF rule 14).
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ClimbPeel : MonoBehaviour
    {
        [Tooltip("Which way the climber is nudged as it lets go, in this object's frame.")]
        [SerializeField] Vector3 away = Vector3.forward;
        [Tooltip("How hard, m/s.")]
        [SerializeField] float push = 2f;
        [Tooltip("Seconds before the same character can be peeled again.")]
        [SerializeField] float again = 1f;

        readonly Dictionary<ICharacterDriver, float> last = new Dictionary<ICharacterDriver, float>();
        readonly List<ICharacterDriver> waiting = new List<ICharacterDriver>();
        readonly List<IHitReceiver> waitingHits = new List<IHitReceiver>();

        // How many it has peeled off since it woke (the checks read it).
        public int Peeled { get; private set; }

        public void Configure(Vector3 awayLocal, float pushSpeed)
        {
            away = awayLocal.sqrMagnitude > 1e-6f ? awayLocal.normalized : Vector3.forward;
            push = pushSpeed;
        }

        void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        void OnTriggerEnter(Collider other) => Touch(other);

        void OnTriggerStay(Collider other) => Touch(other);

        void Touch(Collider other)
        {
            if (!YardKit.Movable(other, out var driver, out var hits)) return;
            if (waiting.Contains(driver)) return;
            if (last.TryGetValue(driver, out float t) && Time.time - t < again) return;
            waiting.Add(driver);
            waitingHits.Add(hits);
        }

        void FixedUpdate()
        {
            if (waiting.Count == 0) return;
            Vector3 velocity = transform.TransformDirection(away) * push;
            for (int i = 0; i < waiting.Count; i++)
            {
                var driver = waiting[i];
                if (driver is Object o && o == null) continue;
                last[driver] = Time.time;
                waitingHits[i].ApplyHit(velocity, 0f, 0f, true);
                YardKit.ReportHit(driver, this, velocity, 0f);
                Peeled++;
            }
            waiting.Clear();
            waitingHits.Clear();
        }
    }
}
