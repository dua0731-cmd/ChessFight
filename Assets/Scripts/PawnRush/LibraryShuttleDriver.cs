using UnityEngine;
using ChessFight.Gameplay;

namespace ChessFight.PawnRush
{
    // Drives the library shuttle's take (one 16 s round trip) from the shared obstacle clock instead of the
    // legacy Animation component (R259: with the component, 승규 님 saw the conveyor not go back and forth).
    // Every physics step it samples the clip at ObstacleClock.Now modulo its length, so it always loops, every
    // PC sees the same pose (G2), and the carriage moves in the step the riders read it. Runs before the
    // ragdoll (-50) so a rider never reads last step's carriage.
    //
    // R443 (승규 11-05 "컨베이어 벨트 멈출 때 덜컹 하면서 멈출 수 있게 하고 ... 덜컹 할 때 파티클"): the take eases the carriage
    // out to a stop at each end (8 s apart, then 1.5 s still). Now it comes in still running and stops with a jolt. From
    // JoltLead seconds before the take's own stop the carriage leaves the take on a curve that starts with the take's
    // position and speed and reaches the end JoltEarly seconds early, still at JoltSpeed; there the end stop throws it
    // JoltSpeed / BounceW past the end and back in a damped bounce, with a little hop and its front dipping and rocking
    // back. JoltSettle seconds later it is on the take again (resting at the end). The belt keeps running on it as
    // drawn (the links ride the carriage). The stops are found in the take itself (Analyse), and every offset is a
    // function of the shared clock, so every PC sees - and every rider feels - the same jolt, with no packet. Jolts
    // counts the impacts (LibraryConveyorSound plays the clunk on it, LibraryConveyorJoltFx the dust and sparks).
    [DefaultExecutionOrder(-200)]
    public sealed class LibraryShuttleDriver : MonoBehaviour
    {
        const float JoltLead = 1.05f, JoltEarly = .45f, JoltSpeed = .85f;       // s, s, m/s at the impact
        const float BounceW = 26f, BounceDamp = .3f, JoltSettle = .9f;           // rad/s, damping ratio, s
        const float HopHeight = .025f, HopW = 26f, HopTau = .15f;                // m, rad/s, s
        const float NodDegrees = .6f, NodW = 22f, NodTau = .18f;                 // the front's dip and rock

        [SerializeField] AnimationClip clip;
        [SerializeField] Transform carriage;

        double stepKey = double.NaN;
        Vector3 carriageVelocity, beltVelocity;
        bool beltMeasured, beltLogged;
        Transform[] links;
        Vector3[] lastLink;

        // the take's stops, in the carriage parent's space: s = distance along the travel axis from one end
        struct Stop { public float hit, sStart, vStart, end, dir; }
        Stop[] stops;
        Vector3 axis, origin;
        Vector3[] travel;          // the carriage's local position through the take, sampled (Analyse)
        float travelStep;
        float metresPerUnit = 1f;
        bool analysed;
        float lastClip = float.NaN;

        /// <summary>How many jolting stops this PC has seen (an impact on the shared clock adds one).</summary>
        public int Jolts { get; private set; }
        /// <summary>The way the carriage was going at the last impact (world, unit).</summary>
        public Vector3 JoltFront { get; private set; } = Vector3.forward;
        /// <summary>True once the take's stops are found: the stops jolt and Jolts counts them.</summary>
        public bool Jolting => stops != null && stops.Length > 0;
        public Transform Carriage => carriage;

        /// <summary>R454: where the carriage will be (world) at a time on the shared clock, from the take sampled at the
        /// start (the jolts' few centimetres left out) - LibraryRookTurret lands shots on its deck.</summary>
        public bool CarriageAt(double time, out Vector3 world)
        {
            world = default;
            if (travel == null || carriage == null || clip == null) return false;
            float x = Wrap((float)(time % clip.length)) / travelStep;
            int n = travel.Length, i0 = Mathf.FloorToInt(x) % n;
            var local = Vector3.Lerp(travel[i0], travel[(i0 + 1) % n], x - Mathf.Floor(x));
            world = carriage.parent != null ? carriage.parent.TransformPoint(local) : local;
            return true;
        }

        public Vector3 CarriageVelocity { get { Step(); return carriageVelocity; } }

        /// <summary>R518 (the user's list, 11-09, 13: "엔딩구간 난이도가 너무 어려움(혼자서도 못감). 우선 중앙 레일이 역방향으로
        /// 돌아가는 것만 빼봐도 해볼만 할듯"): false holds the belt still on the carriage - every part the take runs on it
        /// (the links, the drums) keeps the place the take's start gives it there - so a rider only travels with the
        /// carriage (LibraryConveyorSurface). The carriage still goes back and forth and jolts. The round's war room sets
        /// it (Course01v4Builder.BuildWarRoomSection); 승규 님's test scene (zone E) keeps the reverse run (R258).</summary>
        public bool BeltRuns
        {
            get => beltRuns;
            set
            {
                beltRuns = value;
                beltMeasured = false;
            }
        }
        bool beltRuns = true;
        Transform[] heldParts;
        Vector3[] heldPosition;        // in the carriage's space
        Quaternion[] heldRotation;

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
            beltVelocity = carriageVelocity;
            if (!beltRuns) return;                             // R518: held, the belt's top run is the carriage
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
            Analyse();
            Vector3 before = carriage != null ? carriage.position : Vector3.zero;
            float t = Sample(ObstacleClock.Now);
            CountJolts(t);
            carriageVelocity = carriage != null
                ? (carriage.position - before) / Mathf.Max(Time.fixedDeltaTime, 1e-4f)
                : Vector3.zero;
            // The loop's seam is continuous (the take ends where it starts), so no step jumps.
            if (carriageVelocity.sqrMagnitude > 25f) carriageVelocity = Vector3.zero;
            MeasureBelt(false);
        }

        // The take's pose at the clock's time, the jolt laid over it; returns the time into the take.
        float Sample(double time)
        {
            if (clip == null || clip.length <= 0f) return 0f;
            if (!beltRuns) FindHeldParts();
            float t = (float)(time % clip.length);
            if (t < 0f) t += clip.length;
            clip.SampleAnimation(gameObject, t);
            Jolt(t);
            if (!beltRuns) HoldParts();
            return t;
        }

        // R518: the carriage's parts the take moves on it (whatever changes its place in the carriage's space between
        // three times of the take: the belt's links, its drums), each with its place at the take's start.
        void FindHeldParts()
        {
            if (heldParts != null || carriage == null) return;
            var all = carriage.GetComponentsInChildren<Transform>(true);
            float[] times = { 0f, clip.length * .31f, clip.length * .67f };
            var position = new Vector3[times.Length, all.Length];
            var rotation = new Quaternion[times.Length, all.Length];
            for (int k = 0; k < times.Length; k++)
            {
                clip.SampleAnimation(gameObject, times[k]);
                var inverse = Quaternion.Inverse(carriage.rotation);
                for (int i = 0; i < all.Length; i++)
                {
                    position[k, i] = carriage.InverseTransformPoint(all[i].position);
                    rotation[k, i] = inverse * all[i].rotation;
                }
            }
            var parts = new System.Collections.Generic.List<int>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == carriage) continue;
                for (int k = 1; k < times.Length; k++)
                    if ((position[k, i] - position[0, i]).sqrMagnitude > 1e-6f || Quaternion.Angle(rotation[k, i], rotation[0, i]) > .1f)
                    {
                        parts.Add(i);
                        break;
                    }
            }
            heldParts = new Transform[parts.Count];
            heldPosition = new Vector3[parts.Count];
            heldRotation = new Quaternion[parts.Count];
            for (int j = 0; j < parts.Count; j++)
            {
                heldParts[j] = all[parts[j]];
                heldPosition[j] = position[0, parts[j]];
                heldRotation[j] = rotation[0, parts[j]];
            }
            Debug.Log("[작전 회의실] 컨베이어 벨트를 캐리지 위에 멈춤(R518): 테이크가 돌리던 부품 " + heldParts.Length + "개를 제자리에");
        }

        // Every held part back on its place on the carriage as the carriage now stands (a parent before its children).
        void HoldParts()
        {
            if (heldParts == null || carriage == null) return;
            for (int j = 0; j < heldParts.Length; j++)
            {
                if (heldParts[j] == null) continue;
                heldParts[j].SetPositionAndRotation(carriage.TransformPoint(heldPosition[j]), carriage.rotation * heldRotation[j]);
            }
        }

        float Wrap(float t)
        {
            float l = clip.length;
            t %= l;
            return t < 0f ? t + l : t;
        }

        // Finds the take's stops once: the carriage's travel sampled 60 times a second, its axis between its two ends,
        // a stop wherever it goes still after moving.
        void Analyse()
        {
            if (analysed || clip == null || carriage == null || clip.length <= 0f) return;
            analysed = true;
            stops = new Stop[0];
            int n = Mathf.Max(120, Mathf.CeilToInt(clip.length * 60f));
            var p = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                clip.SampleAnimation(gameObject, clip.length * i / n);
                p[i] = carriage.localPosition;
            }
            int a = 0, b = 0;
            float far = 0f;
            for (int i = 0; i < n; i++)
                if ((p[i] - p[0]).sqrMagnitude > far) { far = (p[i] - p[0]).sqrMagnitude; b = i; }
            far = 0f;
            for (int i = 0; i < n; i++)
                if ((p[i] - p[b]).sqrMagnitude > far) { far = (p[i] - p[b]).sqrMagnitude; a = i; }
            if (far < 1e-4f) return;
            origin = p[a];
            axis = (p[b] - p[a]).normalized;
            metresPerUnit = carriage.parent != null ? Mathf.Max(carriage.parent.TransformVector(axis).magnitude, 1e-6f) : 1f;
            float dt = clip.length / n, still = .02f * dt / metresPerUnit;      // under 2 cm/s counts as still
            var s = new float[n];
            for (int i = 0; i < n; i++) s[i] = Vector3.Dot(p[i] - origin, axis);
            var found = new System.Collections.Generic.List<Stop>();
            for (int i = 0; i < n; i++)
            {
                float before = s[i] - s[(i - 1 + n) % n], after = s[(i + 1) % n] - s[i];
                if (Mathf.Abs(before) <= still || Mathf.Abs(after) > still) continue;
                float tStop = i * dt, tStart = tStop - JoltLead;
                if (found.Count > 0 && Wrap(tStop - JoltEarly - found[found.Count - 1].hit) < 2f) continue;
                found.Add(new Stop
                {
                    hit = Wrap(tStop - JoltEarly),
                    sStart = Along(s, tStart, dt),
                    vStart = (Along(s, tStart + dt, dt) - Along(s, tStart - dt, dt)) / (2f * dt),
                    end = s[i],
                    dir = Mathf.Sign(before),
                });
            }
            stops = found.ToArray();
            travel = p;
            travelStep = dt;
            Debug.Log("[작전 회의실] 컨베이어 멈춤 " + stops.Length + "곳을 덜컹 멈춤으로(" + (JoltSpeed).ToString("F2") + " m/s로 들어와 끝에서 튕김)");
        }

        // s at time t from the samples, wrapping round the take.
        float Along(float[] s, float t, float dt)
        {
            int n = s.Length;
            float x = Wrap(t) / dt;
            int i0 = Mathf.FloorToInt(x) % n, i1 = (i0 + 1) % n;
            return Mathf.Lerp(s[i0], s[i1], x - Mathf.Floor(x));
        }

        // Lays the jolt over the take's pose at time t (the carriage's local position and rotation, as just sampled).
        void Jolt(float t)
        {
            if (stops == null || carriage == null) return;
            float approach = JoltLead - JoltEarly;
            foreach (var st in stops)
            {
                float since = Wrap(t - (st.hit - approach));        // since this stop's approach began
                if (since >= approach + JoltSettle) continue;
                Vector3 pose = carriage.localPosition;
                float now = Vector3.Dot(pose - origin, axis), speed = st.dir * JoltSpeed / metresPerUnit;
                float q, hop = 0f, nod = 0f;
                if (since < approach)
                {
                    // Hermite from the take's position and speed to the end, arriving still running
                    float u = since / approach, u2 = u * u, u3 = u2 * u;
                    q = (2f * u3 - 3f * u2 + 1f) * st.sStart + (u3 - 2f * u2 + u) * approach * st.vStart
                      + (-2f * u3 + 3f * u2) * st.end + (u3 - u2) * approach * speed;
                }
                else
                {
                    // the end stop: a damped bounce that leaves the end at the impact's speed, a hop, a nod
                    float x = since - approach;
                    q = st.end + speed / BounceW * Mathf.Exp(-BounceDamp * BounceW * x) * Mathf.Sin(BounceW * x);
                    hop = HopHeight * Mathf.Exp(-x / HopTau) * Mathf.Abs(Mathf.Sin(HopW * x));
                    nod = NodDegrees * Mathf.Exp(-x / NodTau) * Mathf.Sin(NodW * x);
                }
                var parent = carriage.parent;
                Vector3 up = parent != null ? parent.InverseTransformVector(Vector3.up) : Vector3.up;
                carriage.localPosition = pose + axis * (q - now) + up * hop;
                if (nod != 0f)
                {
                    // the front dips first (about the axis across the travel, in the parent's space)
                    Vector3 front = axis * st.dir;
                    carriage.localRotation = Quaternion.AngleAxis(nod, Vector3.Cross(up.normalized, front)) * carriage.localRotation;
                }
                return;
            }
        }

        // An impact between the last step's time and this one's: one more jolt (a clock that jumped is left alone).
        void CountJolts(float t)
        {
            if (stops != null && !float.IsNaN(lastClip))
            {
                float step = Wrap(t - lastClip);
                if (step > 0f && step < .5f)
                    foreach (var st in stops)
                    {
                        float ahead = Wrap(st.hit - lastClip);
                        if (ahead <= 0f || ahead > step) continue;
                        Jolts++;
                        var parent = carriage.parent;
                        JoltFront = (parent != null ? parent.TransformDirection(axis * st.dir) : axis * st.dir).normalized;
                    }
            }
            lastClip = t;
        }
    }
}
