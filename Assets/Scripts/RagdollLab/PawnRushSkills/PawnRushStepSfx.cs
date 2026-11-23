using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R652 (사용자 11-14 "지금까지 만든 소리들이야. 전부 적용시켜줘"): footsteps on the Pawn Rush course - '탁탁탁탁' at the run
    /// (WASD, 2.5 m/s, 5.2 footfalls a second) and '타다다다닷' at the sprint (Shift, 3.6 m/s, 6.6 a second). The clips are
    /// single steps cut from the user's ElevenLabs takes (Resources/PawnRushStepSfx/Walk*, Run*;
    /// ArtSource/PawnRush/Sfx/bake_moves.py), taken in turn with no step twice in a row and a little off pitch.
    ///
    /// R657 (사용자 11-15 "걷기가 탁탁탁탁 … 달리기는 타다다다닷 … 둘 다 발의 속도에 비해 소리가 느려"): a step for each
    /// footfall. The legs run a cycle 1.8 (walk) to 2.6 (run) to 3.3 (sprint) times a second with both legs in it, so the
    /// feet land 5.2 times a second at the run and 6.6 at the sprint; R652 sounded 2.9 and 3.5, one a cycle. Where the
    /// body is simulated here the step now sounds on the legs' own phase (RagdollPawn.GaitAngle), as a leg reaches the
    /// front; for a network puppet the rate comes from its speed by the same rule as the legs (StepsPerSecond).
    ///
    /// Not sent over the network (five or more a second for every pawn): every PC times the steps of every pawn it sees.
    /// A pawn steps while it is on its feet and its legs are going (a puppet: moving over 0.7 m/s). On its feet: where the body is
    /// simulated here, the pawn's own sense (Grounded, upright, not diving, climbing, swimming or on a rope); for a
    /// network puppet (its floor is not sensed here), its hips at standing height over a floor straight below and
    /// hardly moving up or down. The speed is the hips' over the floor; a floor that carries the pawn (a lift, a
    /// conveyor) is taken off where it is known, so riding is not walking.
    ///
    /// Quiet: under every other sound (the walk step plays at about -30 dB, the sprint's -28 at full effects volume),
    /// as in a course section the sound rule says (CourseSoundPlace) and further off fading out by 26 m from the camera.
    /// </summary>
    [DefaultExecutionOrder(212)]
    public sealed class PawnRushStepSfx : MonoBehaviour
    {
        public const string Folder = "PawnRushStepSfx";
        // bake_moves.py matches every step to one loudness (50 ms: walk -19 dB, sprint -14 dB); these set them under the rest.
        // R656: 1.5x (사용자 "지금보다 1.5배는 음량을 키워도 돼"): the walk step at about -26 dB, the sprint's -24.
        const float WalkGain = .44f, RunGain = .32f;
        const float MinSpeed = .7f, RunFrom = 3.05f;
        // The Pawn Rush run and sprint speeds and the legs' cycles a second (PawnRushPhysicsParams' own defaults, for a
        // pawn without them).
        const float RunSpeed = 2.5f, SprintSpeed = 3.6f, WalkCycles = 1.8f, RunCycles = 2.6f, SprintCycles = 3.3f;
        const float NearAll = 6f, FarNone = 26f;
        const int Voices = 8;
        const float Rescan = .5f;

        sealed class Feet
        {
            public Vector3 last;
            public Vector3 velocity;
            public float phase = .55f;
            public int half = -1;   // R657: which half of the legs' cycle it was in (a step when it changes)
            public bool seen;
            public int lastClip = -1;
            public float offFor;   // seconds off its feet (a landing on glass taps once)
        }

        readonly List<AudioClip> walk = new List<AudioClip>(), run = new List<AudioClip>();
        // 2026-11-22 (승규 "잔잔한 물 헤치는 소리"): on a WetFloor (the garden's lock) the steps are soft water parting,
        // WadeWalk* / WadeRun* (ArtSource/PawnRush/Sfx/bake_moves.py, the ElevenLabs flow "정원 갑문 물 발소리").
        readonly List<AudioClip> wadeWalk = new List<AudioClip>(), wadeRun = new List<AudioClip>();
        const float WadeGain = .4f;   // about the dry walk step's level (bake_wade.py: -18 dB, x0.4)
        // 2026-11-23 (사용자 "대계단 코스에서 유리 밟을 때 나는 소리 넣어줘"): on a GlassFloor (the grand stair hall's glass
        // stepping stones) the steps are crisp glassy taps, GlassWalk* / GlassRun* (ArtSource/PawnRush/Sfx/bake_glass.py,
        // the ElevenLabs flow "대계단 유리 발소리"). The stones stand 1.05 m apart, so most pawns jump them: a landing on
        // glass after a short flight taps once as well (LandGlassAfter).
        readonly List<AudioClip> glassWalk = new List<AudioClip>(), glassRun = new List<AudioClip>();
        // bake_glass.py: walk taps -21 dB, run taps -20 dB (50 ms); x0.6 sets them about the dry steps (-26 / -24 dB),
        // a landing a little over them.
        const float GlassGain = .6f, GlassLandGain = .8f, LandGlassAfter = .15f;
        readonly Dictionary<RagdollPawn, Feet> feet = new Dictionary<RagdollPawn, Feet>();
        readonly List<RagdollPawn> pawns = new List<RagdollPawn>();
        readonly RaycastHit[] hits = new RaycastHit[8];
        AudioSource[] voices;
        int next;
        float scanAt;

        /// <summary>Steps started since the scene began (for tests and the log).</summary>
        public int Steps { get; private set; }

        void Awake()
        {
            foreach (var clip in Resources.LoadAll<AudioClip>(Folder))
            {
                if (clip.name.StartsWith("Walk")) walk.Add(clip);
                else if (clip.name.StartsWith("Run")) run.Add(clip);
                else if (clip.name.StartsWith("WadeWalk")) wadeWalk.Add(clip);
                else if (clip.name.StartsWith("WadeRun")) wadeRun.Add(clip);
                else if (clip.name.StartsWith("GlassWalk")) glassWalk.Add(clip);
                else if (clip.name.StartsWith("GlassRun")) glassRun.Add(clip);
            }
            foreach (var list in new[] { walk, run, wadeWalk, wadeRun, glassWalk, glassRun })
                list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            var holder = new GameObject("Pawn Rush footsteps").transform;
            holder.SetParent(transform, false);
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var a = holder.gameObject.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 0f;
                a.dopplerLevel = 0f;
                voices[i] = a;
            }
        }

        void Update()
        {
            if (walk.Count == 0 && run.Count == 0) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (Time.unscaledTime >= scanAt)
            {
                scanAt = Time.unscaledTime + Rescan;
                pawns.Clear();
                pawns.AddRange(FindObjectsByType<RagdollPawn>(FindObjectsSortMode.None));
            }
            var cam = Camera.main;
            foreach (var pawn in pawns)
            {
                if (pawn == null || !pawn.isActiveAndEnabled || pawn.Hips == null) continue;
                if (!feet.TryGetValue(pawn, out var f)) feet[pawn] = f = new Feet();
                Vector3 hips = pawn.Hips.position;
                if (!f.seen)
                {
                    f.seen = true;
                    f.last = hips;
                    continue;
                }
                Vector3 raw = (hips - f.last) / dt;
                f.last = hips;
                // A respawn or a teleport is not a stride.
                if (raw.sqrMagnitude > 400f) { f.phase = .55f; f.half = -1; continue; }
                f.velocity = Vector3.Lerp(f.velocity, raw, 1f - Mathf.Exp(-dt / .08f));
                Vector3 floorVel;
                bool wet, glass;
                if (!OnFeet(pawn, hips, f.velocity.y, out floorVel, out wet, out glass))
                {
                    f.phase = .55f; f.half = -1; f.offFor += dt;
                    continue;
                }
                Vector3 over = f.velocity - floorVel;
                float speed = new Vector2(over.x, over.z).magnitude;
                bool sprint = pawn.NetworkPuppet ? speed > RunFrom : pawn.Sprinting || speed > RunFrom;
                bool landed = f.offFor >= LandGlassAfter;
                f.offFor = 0f;
                if (landed && glass && glassWalk.Count + glassRun.Count > 0)
                {
                    // A landing on glass: one tap now, the next on the legs' own count.
                    Step(f, sprint, false, true, hips, Mathf.Max(speed, 2.5f), cam, GlassLandGain);
                    continue;
                }
                if (!pawn.NetworkPuppet)
                {
                    // R657: on the legs' own phase, a step each time a leg reaches the front (twice a cycle). Standing,
                    // the legs stop and so do the steps; running in place against a wall, they go on.
                    int half = Mathf.FloorToInt(pawn.GaitAngle / Mathf.PI + .5f) % 2;
                    bool stepped = f.half >= 0 && half != f.half;
                    f.half = half;
                    if (!stepped) continue;
                }
                else
                {
                    if (speed < MinSpeed) { f.phase = .55f; continue; }
                    f.phase += StepsPerSecond(pawn, speed) * dt;
                    if (f.phase < 1f) continue;
                    f.phase = Mathf.Repeat(f.phase, 1f);
                }
                Step(f, sprint, wet, glass, hips, speed, cam, 0f);
            }
        }

        /// <summary>R657: a puppet's footfalls a second from its speed, by the legs' own rule (RagdollPawn.Pose and
        /// PawnRushCadence): cycles from the walk's to the run's or the sprint's as the speed nears the top, two footfalls
        /// a cycle. The sprint share is read from the speed (a puppet's sprint key is not known here).</summary>
        static float StepsPerSecond(RagdollPawn pawn, float speed)
        {
            var q = pawn.PawnRushPhysics;
            float walkC = q != null ? q.walkCadence : WalkCycles, runC = q != null ? q.runCadence : RunCycles;
            float sprintC = q != null ? q.sprintCadence : SprintCycles;
            float blend = Mathf.InverseLerp(RunSpeed, SprintSpeed, speed);
            float speedN = Mathf.Clamp01(speed / Mathf.Lerp(RunSpeed, SprintSpeed, blend));
            float cycles = Mathf.Lerp(walkC, Mathf.Lerp(runC, sprintC, blend), speedN) * Mathf.Clamp01(speedN * 4f);
            return 2f * cycles;
        }

        bool OnFeet(RagdollPawn pawn, Vector3 hips, float vy, out Vector3 floorVel, out bool wet, out bool glass)
        {
            floorVel = Vector3.zero;
            wet = false;
            glass = false;
            if (!pawn.NetworkPuppet)
            {
                if (!pawn.Grounded || pawn.State != PawnState.Active || pawn.Diving || pawn.Climbing || pawn.Floating || pawn.OnRope)
                    return false;
            }
            else if (Mathf.Abs(vy) > 1f) return false;
            // The floor under the hips: for a puppet the only sense of it, for any pawn the floor's own speed.
            int n = Physics.RaycastNonAlloc(hips + Vector3.up * .05f, Vector3.down, hits, pawn.standHeight + .35f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.distance >= best || pawn.Owns(h.collider)) continue;
                best = h.distance;
                found = true;
                var body = h.rigidbody;
                var surface = h.collider.GetComponentInParent<IMovingSurface>();
                floorVel = surface != null ? surface.PointVelocity(h.point) : body != null ? body.GetPointVelocity(h.point) : Vector3.zero;
                wet = h.collider.GetComponent<ChessFight.Gameplay.WetFloor>() != null;
                glass = h.collider.GetComponent<ChessFight.Gameplay.GlassFloor>() != null;
            }
            if (!pawn.NetworkPuppet) return true;
            // Upright: the hips stand about standHeight over the floor (lying down they are much lower).
            return found && best - .05f > pawn.standHeight * .6f;
        }

        // levelOver: the level to use instead of the set's own (0 = the set's).
        void Step(Feet f, bool sprint, bool wet, bool glass, Vector3 at, float speed, Camera cam, float levelOver)
        {
            List<AudioClip> set;
            float level;
            if (glass && (glassWalk.Count > 0 || glassRun.Count > 0))
            {
                set = sprint && glassRun.Count > 0 ? glassRun : glassWalk.Count > 0 ? glassWalk : glassRun;
                level = GlassGain;
            }
            else if (wet && (wadeWalk.Count > 0 || wadeRun.Count > 0))
            {
                set = sprint && wadeRun.Count > 0 ? wadeRun : wadeWalk.Count > 0 ? wadeWalk : wadeRun;
                level = WadeGain;
            }
            else
            {
                set = sprint && run.Count > 0 ? run : walk.Count > 0 ? walk : run;
                level = set == run ? RunGain : WalkGain;
            }
            if (set.Count == 0) return;
            if (levelOver > 0f && set != walk && set != run) level = levelOver;
            int pick = Random.Range(0, set.Count);
            if (set.Count > 1 && pick == f.lastClip) pick = (pick + 1 + Random.Range(0, set.Count - 1)) % set.Count;
            f.lastClip = pick;
            float gain = level * Mathf.Lerp(.75f, 1f, Mathf.InverseLerp(MinSpeed, 2.5f, speed));
            gain *= ChessFight.Game.GameSettings.Effects * ChessFight.Gameplay.CourseSoundPlace.Gain(at);
            float pan = 0f;
            if (cam != null)
            {
                float d = Vector3.Distance(cam.transform.position, at);
                gain *= 1f - Mathf.Clamp01((d - NearAll) / (FarNone - NearAll));
                var v = cam.WorldToViewportPoint(at);
                if (v.z > 0f) pan = Mathf.Clamp(v.x * 2f - 1f, -1f, 1f) * .4f;
            }
            if (gain <= .001f) return;
            var a = voices[next];
            next = (next + 1) % voices.Length;
            a.Stop();
            a.clip = set[pick];
            a.volume = Mathf.Clamp01(gain);
            a.pitch = Mathf.Pow(2f, Random.Range(-.5f, .5f) / 12f);
            a.panStereo = pan;
            a.Play();
            Steps++;
        }
    }
}
