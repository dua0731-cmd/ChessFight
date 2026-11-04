using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.PawnRush
{
    // Drives the library shuttle's take (one 16 s round trip) from the shared obstacle clock instead of the
    // legacy Animation component (R259: with the component, 승규 님 saw the conveyor not go back and forth).
    // Every physics step it samples the clip at ObstacleClock.Now modulo its length, so it always loops, every
    // PC sees the same pose (G2), and the carriage moves in the step the riders read it. Runs before the
    // ragdoll (-50) so a rider never reads last step's carriage.
    [DefaultExecutionOrder(-200)]
    public sealed class LibraryShuttleDriver : MonoBehaviour
    {
        [SerializeField] AnimationClip clip;
        [SerializeField] Transform carriage;

        double stepKey = double.NaN;
        Vector3 carriageVelocity, beltVelocity;
        bool beltMeasured, beltLogged;
        Transform[] links;
        Vector3[] lastLink;

        public Vector3 CarriageVelocity { get { Step(); return carriageVelocity; } }

        // R393 (승규 11-04 "컨베이어 벨트 역방향이랑 이제 상호 작용 할 수 있게 해"): how fast the belt's top run moves in the
        // world - the carriage's travel plus the links running back along it, measured from the links the take moves
        // (the ones on top, a step that wraps round a drum left out), so a rider goes exactly as the drawn belt does.
        public bool BeltMeasured { get { Step(); return beltMeasured; } }
        public Vector3 BeltVelocity { get { Step(); return beltVelocity; } }

        public void Configure(AnimationClip take, Transform movingCarriage)
        {
            clip = take;
            carriage = movingCarriage;
        }

        void FindLinks()
        {
            if (links != null || carriage == null) return;
            var found = new System.Collections.Generic.List<Transform>();
            foreach (var t in carriage.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("BELT_LINK_")) found.Add(t);
            links = found.ToArray();
            lastLink = new Vector3[links.Length];
        }

        void MeasureBelt(bool first)
        {
            FindLinks();
            beltMeasured = false;
            if (links == null || links.Length == 0) return;   // not configured yet (AddComponent runs OnEnable first)
            float top = float.MinValue;
            foreach (var l in links) top = Mathf.Max(top, l.position.y);
            Vector3 sum = Vector3.zero;
            int count = 0;
            float dt = Mathf.Max(Time.fixedDeltaTime, 1e-4f);
            for (int i = 0; i < links.Length; i++)
            {
                var p = links[i].position;
                var step = p - lastLink[i];
                lastLink[i] = p;
                if (first || p.y < top - .05f || step.sqrMagnitude > .25f) continue;
                sum += step;
                count++;
            }
            if (count == 0) return;
            beltVelocity = sum / count / dt;
            beltMeasured = (beltVelocity - carriageVelocity).sqrMagnitude > .05f * .05f;   // a belt that does run
            if (beltMeasured && !beltLogged)
            {
                beltLogged = true;
                Debug.Log("[작전 회의실] 컨베이어 벨트 윗면이 캐리지에 대해 " + (beltVelocity - carriageVelocity).magnitude.ToString("F2") +
                          " m/s로 돎(폰에 그대로 전해짐)");
            }
        }

        void OnEnable()
        {
            var anim = GetComponent<Animation>();
            if (anim != null) anim.enabled = false;
            stepKey = double.NaN;
            Sample(ObstacleClock.Now);
            carriageVelocity = Vector3.zero;
            MeasureBelt(true);
        }

        void FixedUpdate() => Step();

        // Once per physics step, for whoever asks first (this FixedUpdate or a rider's).
        void Step()
        {
            double key = Time.fixedTimeAsDouble;
            if (key == stepKey || clip == null) return;
            stepKey = key;
            Vector3 before = carriage != null ? carriage.position : Vector3.zero;
            Sample(ObstacleClock.Now);
            carriageVelocity = carriage != null
                ? (carriage.position - before) / Mathf.Max(Time.fixedDeltaTime, 1e-4f)
                : Vector3.zero;
            // The loop's seam is continuous (the take ends where it starts), so no step jumps.
            if (carriageVelocity.sqrMagnitude > 25f) carriageVelocity = Vector3.zero;
            MeasureBelt(false);
        }

        void Sample(double time)
        {
            if (clip == null || clip.length <= 0f) return;
            float t = (float)(time % clip.length);
            if (t < 0f) t += clip.length;
            clip.SampleAnimation(gameObject, t);
        }
    }
}
