using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R530 (승규 11-09, "정원 sfx 넣으려고 … 1번 부터 시작"): a fountain-plaza spin bar's sounds (garden beat 1).
    //  hub     the wooden bar turning on its gold hub: a creak-and-rumble loop, pitched by how fast the bar turns
    //  whoosh  an arm sweeping past: when an arm crosses the listener's bearing from the hub (listener close enough
    //          sideways), one deep air whoosh, louder the faster the arm moves there and the nearer it passes
    //  hit     a pawn knocked by the bar: a dull wooden thud where they touched, once a touch
    // R545 (승규 11-10, main 온라인 점검 "회전 막대 부딪힘 소리를 참가자 PC에서도"): the host (and offline) hears the
    // physics contact. A client draws every pawn - its own too - as a kinematic puppet posed from the host's snapshots,
    // and kinematic against kinematic makes no contact (DynamicsManager m_ContactPairsMode 0), so it heard no thud at all.
    // There this checks the drawn pawns against the bar's colliders every frame (Watch): the same moment, nothing sent.
    // Every sound is a NearFarSound (detailed take up close, muffled take farther off). The motion itself is
    // ObstacleMotion on the shared clock; this only listens to it.
    [RequireComponent(typeof(ObstacleMotion))]
    public sealed class GardenSpinBarSound : MonoBehaviour
    {
        const string Folder = "PawnRushGardenSfx/";
        const float HitCooldown = .45f;

        [SerializeField] float reach = 4.5f;
        [SerializeField] int arms = 2;
        [SerializeField] bool tall;

        ObstacleMotion motion;
        NearFarSound hub, swish, hit;
        Transform swishAt, hitAt;
        AudioClip whooshNear, whooshFar, hitNear, hitFar;
        float[] lastDelta, lastLead;
        const float WhooshLead = .28f;   // s from SpinWhoosh.wav's start to its loudest moment (plaza_sounds.py take 2)
        readonly Dictionary<ICharacterDriver, float> hitAtTime = new Dictionary<ICharacterDriver, float>();
        // R540 (승규 11-10, "충돌 했을 때 1번만 나오면 돼 그 다음 또 충돌하면 나오고"): when each piece last touched the bar
        // (any of its ragdoll's colliders). A pawn is many colliders and is pushed along for a while, so contacts come
        // and go during one hit; only a touch after it has been clear of the bar for Rearm s is a new hit.
        readonly Dictionary<ICharacterDriver, float> touchedAt = new Dictionary<ICharacterDriver, float>();
        const float Rearm = .4f;
        static AudioListener listener;
        // R545: a client's check (Watch). Touch: m between a pawn's collider and the bar that counts as touching (a fixed
        // step of the bar's turn at its tip, and the gap the host's contact offset leaves). Heard out to the thud's far end.
        const float Touch = .08f, Heard = 42f;
        Rigidbody body;
        readonly List<Collider> parts = new List<Collider>();
        readonly Collider[] near = new Collider[128];
        readonly HashSet<ICharacterDriver> touching = new HashSet<ICharacterDriver>();

        // For the builder: the bar's reach (m from the hub), how many arms and whether it is the tall bar.
        public void Configure(float barReach, int armCount, bool isTall)
        {
            reach = barReach;
            arms = Mathf.Max(1, armCount);
            tall = isTall;
        }

        void Start()
        {
            motion = GetComponent<ObstacleMotion>();
            body = GetComponent<Rigidbody>();
            // R539 (승규 11-10): the tall bar turns with the merry-go-round take (SpinHubTall), the low bars keep SpinHub.
            hub = new NearFarSound(transform, "Spin bar hub sound", Load(tall ? "SpinHubTall" : "SpinHub"), Load(tall ? "SpinHubTallFar" : "SpinHubFar"), true)
                // R540 (승규 11-10, "회전 막대 소리를 훨씬 크게"): full volume out to 5 m and heard to 24 m (was 2 and 14).
                { NearFull = 5f, NearEnd = 24f, FarStart = 8f, FarEnd = 40f, FarGain = .8f };
            swishAt = Child("Spin bar whoosh point");
            swish = new NearFarSound(swishAt, "Spin bar whoosh sound", null, null, false)
                { NearFull = 3f, NearEnd = 16f, FarStart = 6f, FarEnd = 30f, FarGain = .5f };
            hitAt = Child("Spin bar hit point");
            hit = new NearFarSound(hitAt, "Spin bar hit sound", null, null, false)
                { NearFull = 4f, NearEnd = 22f, FarStart = 6f, FarEnd = 40f, FarGain = .8f };
            whooshNear = Load("SpinWhoosh");
            whooshFar = Load("SpinWhooshFar");
            // R538 (승규 11-10): the tall bar a deep heavy wooden bonk, the low bars a bright dry wooden clack (spinhit_sounds.py).
            hitNear = Load(tall ? "SpinHitTall" : "SpinHitSmall");
            hitFar = Load(tall ? "SpinHitTallFar" : "SpinHitSmallFar");
            lastDelta = new float[arms];
            lastLead = new float[arms];
            for (int i = 0; i < arms; i++) lastDelta[i] = lastLead[i] = float.NaN;
        }

        Transform Child(string n)
        {
            var t = new GameObject(n).transform;
            t.SetParent(transform, false);
            return t;
        }

        static AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>(Folder + name);
            if (clip == null) Debug.LogWarning("[정원] 소리 파일이 없습니다: Resources/" + Folder + name);
            return clip;
        }

        float Speed => Mathf.Abs(motion != null ? motion.degreesPerSecond : 72f);

        void Update()
        {
            float speed = Speed;
            // The tall bar is heavier and slower: a little lower.
            // R541 (승규 11-10): 2x the first volume on the big bar, 3.5x on the small ones (the loop files were raised 1.7 / 5.7 dB in R540).
            hub.Mix(tall ? .82f : .73f, Mathf.Lerp(.85f, 1.1f, Mathf.InverseLerp(40f, 90f, speed)) * (tall ? .9f : 1f));
            Whoosh(speed);
            if (!ObstacleClock.Authority) Watch(speed);
        }

        // An arm crossing the listener's bearing (in the hub's own frame, where the arms stand still and the
        // listener goes round the other way).
        void Whoosh(float speed)
        {
            if (listener == null || !listener.isActiveAndEnabled) listener = FindAnyObjectByType<AudioListener>();
            if (listener == null || whooshNear == null) return;
            Vector3 l = transform.InverseTransformPoint(listener.transform.position);
            float r = new Vector2(l.x, l.z).magnitude;
            bool near = r > .3f && r < reach + 4f && Mathf.Abs(l.y) < 7f;
            float bearing = Mathf.Atan2(l.z, l.x) * Mathf.Rad2Deg;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            for (int i = 0; i < arms; i++)
            {
                // Arm i points along local +x turned by i * 360 / arms about y (Unity's y turn goes x -> -z).
                float arm = -i * 360f / arms;
                float d = Mathf.DeltaAngle(arm, bearing);
                float last = lastDelta[i], lastPredicted = lastLead[i];
                lastDelta[i] = d;
                // Where the angle will be WhooshLead from now (the take is loudest that long after it starts), so the
                // whoosh peaks as the arm goes by, not after.
                float predicted = float.IsNaN(last) ? float.NaN : d + Mathf.DeltaAngle(last, d) / dt * WhooshLead;
                lastLead[i] = predicted;
                if (!near || float.IsNaN(lastPredicted) || float.IsNaN(predicted)) continue;
                // A sign change near 0 (not the jump at 180 on the far side) is the arm about to pass.
                if (Mathf.Sign(predicted) == Mathf.Sign(lastPredicted) || Mathf.Abs(predicted) > 40f || Mathf.Abs(lastPredicted) > 40f) continue;
                float along = Mathf.Min(r, reach);
                float tip = speed * Mathf.Deg2Rad * along;                    // m/s where it passes
                float side = Mathf.Max(0f, r - reach);                         // past its end: how far beyond
                float gain = Mathf.Clamp01(tip / 6f) * (1f - Mathf.Clamp01(side / 4f));
                if (gain < .05f) continue;
                Vector3 at = Quaternion.Euler(0f, -arm, 0f) * Vector3.right * along;
                swishAt.localPosition = new Vector3(at.x, tall ? .65f : .4f, at.z);
                swish.Shot(whooshNear, whooshFar, Mathf.Lerp(.35f, .9f, gain), Mathf.Lerp(.85f, 1.1f, Mathf.Clamp01(tip / 7f)) * Random.Range(.96f, 1.04f));
            }
        }

        void OnCollisionStay(Collision c)
        {
            var driver = c.collider.GetComponentInParent<ICharacterDriver>();
            if (driver != null) touchedAt[driver] = Time.time;
        }

        void OnCollisionEnter(Collision c)
        {
            var driver = c.collider.GetComponentInParent<ICharacterDriver>();
            if (driver == null) return;
            Touched(driver, c.contactCount > 0 ? c.GetContact(0).point : c.collider.bounds.center, c.relativeVelocity.magnitude);
        }

        // A pawn touching the bar (a contact on the host, a drawn overlap on a client), at speed v m/s against it.
        void Touched(ICharacterDriver driver, Vector3 at, float v)
        {
            // Still the same hit: another of its colliders, or the same one back after a bounce.
            bool same = touchedAt.TryGetValue(driver, out float touched) && Time.time - touched < Rearm;
            touchedAt[driver] = Time.time;
            if (same || hitNear == null) return;
            if (hitAtTime.TryGetValue(driver, out float last) && Time.time - last < HitCooldown) return;
            if (v < 1f) return;
            hitAtTime[driver] = Time.time;
            hitAt.position = at;
            hit.Shot(hitNear, hitFar, Mathf.Lerp(.6f, 1f, Mathf.InverseLerp(1f, 7f, v)), Random.Range(.93f, 1.05f));
        }

        // R545: a client's thud. Every pawn collider near the hub is tested against the bar's own colliders (as the
        // drawing has them now: the graybox's logs, or GardenSpinBarFit's hulls); a pawn touching it this frame is
        // Touched at the bar's speed there (its turn x the distance from the axis - the puppet has no speed of its own).
        void Watch(float speed)
        {
            if (hitNear == null || listener == null
                || (listener.transform.position - transform.position).sqrMagnitude > Heard * Heard) return;
            if (!Parts()) return;
            int n = Physics.OverlapSphereNonAlloc(transform.position, reach + 1f, near, ~0, QueryTriggerInteraction.Ignore);
            touching.Clear();
            for (int i = 0; i < n; i++)
            {
                var c = near[i];
                if (c.attachedRigidbody == body || (c is MeshCollider m && !m.convex)) continue;
                var driver = c.GetComponentInParent<ICharacterDriver>();
                if (driver == null || touching.Contains(driver) || !Touches(c, out var at)) continue;
                touching.Add(driver);
                var off = Vector3.ProjectOnPlane(at - transform.position, transform.up);
                Touched(driver, at, speed * Mathf.Deg2Rad * off.magnitude);
            }
        }

        bool Touches(Collider pawn, out Vector3 at)
        {
            var box = pawn.bounds;
            box.Expand(Touch * 2f);
            foreach (var part in parts)
            {
                if (!part.bounds.Intersects(box)) continue;
                var q = part.ClosestPoint(box.center);
                if ((pawn.ClosestPoint(q) - q).sqrMagnitude < Touch * Touch)
                {
                    at = q;
                    return true;
                }
            }
            at = default;
            return false;
        }

        // The bar's colliders, gathered again when GardenSpinBarFit has swapped them (a gathered one gone or off).
        bool Parts()
        {
            bool stale = parts.Count == 0;
            foreach (var part in parts)
                if (part == null || !part.enabled || !part.gameObject.activeInHierarchy) { stale = true; break; }
            if (stale)
            {
                parts.Clear();
                foreach (var c in GetComponentsInChildren<Collider>())
                    if (c.enabled && !c.isTrigger && !(c is MeshCollider m && !m.convex)) parts.Add(c);
            }
            return parts.Count > 0;
        }
    }
}
