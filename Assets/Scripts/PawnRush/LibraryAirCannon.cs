using UnityEngine;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;

namespace ChessFight.PawnRush
{
    // The library's two blower cannons fire air across the conveyor (R291; before, they only stood there looking
    // like suction fans). One 5 s round on the shared obstacle clock, so every PC sees the same shot:
    //   0.0-2.6  idle    the fan turns slowly
    //   2.6-4.0  charge  the fan spins up, the barrel draws back 3 cm and shivers (1.4 s since R294, was 1.0 s)
    //   4.0      fire    the barrel kicks back 15 cm and springs home (a punchy hit, not a soft pop); for 0.3 s every
    //                    pawn in the air column in front (12 m long, about the muzzle's width) is shoved away from
    //                    the cannon and a little up, harder near the muzzle
    //   4.0-5.0          the fan winds back down to idle
    // The fan always turns clockwise as seen from in front of the muzzle. No VFX yet (승규 님: later).
    // Sound (R292): the fan's whoosh, the charge whine and the blast, near and far layers. R294 (승규 님's picks):
    // the fan is option D (soft toy whirr), the charge and blast keep the R292 sound, the blast louder and heard
    // farther. R314 (승규 님: "바람 소리 지워주고 D 팬 소리를 ... 안으로 들어가면 어디서도 들릴 수 있게"): the fan's
    // muffled far layer (it read as wind) is gone; the D whirr itself is heard at one level anywhere inside the
    // library hall and fades out within 4 m of leaving it.
    // Only bodies this PC simulates are pushed (ImportedObstacleActor skips kinematic network puppets), the same
    // way each PC moves its own riders on the conveyor.
    public sealed class LibraryAirCannon : MonoBehaviour
    {
        const float Period = 5f, ChargeAt = 2.6f, FireAt = 4f, BlastTime = .3f;
        const float IdleSpin = 120f, TopSpin = 1440f;      // degrees per second
        const float Reach = 12f, HalfWidth = 1.6f, HalfHeight = 1.4f;
        const float Push = 70f, Lift = 18f;                 // m/s^2 while the blast lasts
        const float NearSpeed = 10f, FarSpeed = 5f;         // the most the blast adds, at the muzzle and at Reach

        [SerializeField] Transform fan;
        [SerializeField] float phase;

        Vector3 rest, muzzleLocal, dirWorld, fanCentreWorld;
        float angle, spin, lastClock = -1f;
        // R292 sound: the fan's whoosh loop follows the spin; the charge whine and the blast are one-shots on the
        // clock's edges. All three are NearFarSound: detailed up close, a muffled hall boom across the library.
        NearFarSound chargeSound, fireSound;
        AudioSource fanSound;
        Bounds room;
        static AudioListener listener;
        const float RoomFade = 4f;
        AudioClip chargeNear, chargeFar, fireNear, fireFar;
        const string Sfx = "PawnRushLibrarySfx/";
        Quaternion fanRest;
        readonly Collider[] buffer = new Collider[256];
        readonly System.Collections.Generic.HashSet<ImportedObstacleActor> pushed = new System.Collections.Generic.HashSet<ImportedObstacleActor>();

        // roomBounds: the library hall in world space; inside it the fan is heard everywhere.
        public void Configure(Transform fanPart, float phaseSeconds, Bounds roomBounds)
        {
            fan = fanPart;
            phase = phaseSeconds;
            room = roomBounds;
        }

        void Awake()
        {
            if (fan == null) { enabled = false; return; }
            rest = transform.localPosition;
            fanRest = fan.localRotation;
            // The muzzle looks from the cannon's origin toward the fan, level with the floor.
            Vector3 d = fan.position - transform.position;
            d.y = 0f;
            dirWorld = d.normalized;
            fanCentreWorld = fan.position;
            muzzleLocal = transform.parent != null ? transform.parent.InverseTransformDirection(dirWorld) : dirWorld;
            spin = IdleSpin;

            fanSound = RoomLoop(fan, "Air cannon fan sound", Load("CannonFanLoop"));
            chargeSound = new NearFarSound(fan, "Air cannon charge sound", null, null, false)
                { NearFull = 4f, NearEnd = 24f, FarStart = 6f, FarEnd = 55f, FarGain = .8f };
            fireSound = new NearFarSound(fan, "Air cannon blast sound", null, null, false)
                { NearFull = 9f, NearEnd = 40f, FarStart = 8f, FarEnd = 100f, FarGain = 1f };
            chargeNear = Load("CannonCharge");
            chargeFar = Load("CannonChargeFar");
            fireNear = Load("CannonFire");
            fireFar = Load("CannonFireFar");
        }

        static AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>(Sfx + name);
            if (clip == null) Debug.LogWarning("Air cannon sound missing: Resources/" + Sfx + name);
            return clip;
        }

        // The fan loop: no distance roll-off, half 3D so it still leans toward the cannon it comes from.
        static AudioSource RoomLoop(Transform at, string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(at, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.clip = clip;
            a.loop = true;
            a.spatialBlend = .5f;
            a.dopplerLevel = 0f;
            a.rolloffMode = AudioRolloffMode.Custom;
            a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Constant(0f, 1f, 1f));
            a.maxDistance = 500f;
            a.volume = 0f;
            if (clip != null)
            {
                a.time = Random.value * clip.length;   // the two cannons never share a seam
                a.Play();
            }
            return a;
        }

        // 1 inside the library hall, down to 0 RoomFade metres outside it.
        float InRoom()
        {
            if (listener == null || !listener.isActiveAndEnabled) listener = Object.FindAnyObjectByType<AudioListener>();
            if (listener == null || room.size == Vector3.zero) return 1f;
            float outside = Mathf.Sqrt(room.SqrDistance(listener.transform.position));
            return 1f - Mathf.SmoothStep(0f, 1f, outside / RoomFade);
        }

        void Sound(float t)
        {
            // Louder and higher as the fan spins up; the same level anywhere in the hall.
            float s = Mathf.InverseLerp(IdleSpin, TopSpin, spin);
            float k = 1f - Mathf.Exp(-6f * Time.deltaTime);
            fanSound.volume = Mathf.Lerp(fanSound.volume, Mathf.Lerp(.22f, .45f, s) * InRoom(), k);
            fanSound.pitch = Mathf.Lerp(fanSound.pitch, Mathf.Lerp(.85f, 1.35f, s), k);
            // Edges of the round; a clock that jumped back (a new round, a resync) does not double-fire.
            if (lastClock >= 0f && t > lastClock)
            {
                if (lastClock < ChargeAt && t >= ChargeAt) chargeSound.Shot(chargeNear, chargeFar, .8f, 1f);
                if (lastClock < FireAt && t >= FireAt) fireSound.Shot(fireNear, fireFar, 1f, Random.Range(.95f, 1.04f));
            }
            lastClock = t;
        }

        float Clock()
        {
            float t = (float)((ObstacleClock.Now + phase) % Period);
            return t < 0f ? t + Period : t;
        }

        void Update()
        {
            float t = Clock();
            float target, back = 0f, shiver = 0f;
            if (t < ChargeAt) target = IdleSpin;
            else if (t < FireAt)
            {
                float c = (t - ChargeAt) / (FireAt - ChargeAt);
                target = Mathf.Lerp(IdleSpin, TopSpin, c * c);
                back = .03f * Mathf.SmoothStep(0f, 1f, c);
                shiver = .01f * c;
            }
            else
            {
                float s = t - FireAt;
                target = Mathf.Lerp(TopSpin, IdleSpin, Mathf.Clamp01(s / (Period - FireAt)));
                back = Kick(s);
            }
            // Spin-up follows the motor quickly, spin-down coasts.
            spin = Mathf.Lerp(spin, target, 1f - Mathf.Exp(-(target > spin ? 8f : 2.5f) * Time.deltaTime));
            angle = Mathf.Repeat(angle + spin * Time.deltaTime, 360f);
            // A positive turn looks clockwise to someone the axis points at (AngleAxis(90, forward) takes right
            // to up, counterclockwise for a camera looking down +Z); the muzzle axis points at the viewer in front.
            Vector3 axisLocal = fan.parent.InverseTransformDirection(dirWorld);
            fan.localRotation = Quaternion.AngleAxis(angle, axisLocal) * fanRest;

            Vector3 jitter = shiver > 0f
                ? new Vector3(Mathf.Sin(t * 251f), Mathf.Sin(t * 197f + 1f), Mathf.Sin(t * 223f + 2f)) * shiver
                : Vector3.zero;
            transform.localPosition = rest - muzzleLocal * back + jitter;
            Sound(t);
        }

        // Barrel kick after the shot: 15 cm back in 0.05 s, then a damped spring home in about 0.7 s.
        static float Kick(float s)
        {
            const float Max = .15f, Snap = .05f;
            if (s < Snap) return Mathf.Lerp(.03f, Max, s / Snap);
            float u = s - Snap;
            return Max * Mathf.Exp(-6f * u) * Mathf.Cos(14f * u);
        }

        void FixedUpdate()
        {
            float t = Clock();
            if (t < FireAt || t >= FireAt + BlastTime) return;
            Vector3 centre = fanCentreWorld + dirWorld * (Reach * .5f) + Vector3.down * .3f;
            Quaternion facing = Quaternion.LookRotation(dirWorld, Vector3.up);
            int n = Physics.OverlapBoxNonAlloc(centre, new Vector3(HalfWidth, HalfHeight, Reach * .5f), buffer, facing,
                ~0, QueryTriggerInteraction.Ignore);
            pushed.Clear();
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(buffer[i]);
                if (actor == null || !actor.CanReceiveObstacle || !pushed.Add(actor)) continue;
                float along = Mathf.Clamp01(Vector3.Dot(actor.Position - fanCentreWorld, dirWorld) / Reach);
                actor.Accelerate(dirWorld * Push + Vector3.up * Lift, Mathf.Lerp(NearSpeed, FarSpeed, along), Time.fixedDeltaTime);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (fan == null) return;
            Vector3 d = fan.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return;
            d.Normalize();
            Gizmos.matrix = Matrix4x4.TRS(fan.position + d * (Reach * .5f) + Vector3.down * .3f, Quaternion.LookRotation(d), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(HalfWidth * 2f, HalfHeight * 2f, Reach));
        }
    }
}
