using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R652 (사용자 11-14 "지금까지 만든 소리들이야. 전부 적용시켜줘"): footsteps on the Pawn Rush course - '총총' at the run
    /// (WASD, 2.5 m/s, about 2.9 steps a second) and '다다다' at the sprint (Shift, 3.6 m/s, 3.5 a second). The clips are
    /// single steps cut from the user's ElevenLabs takes (Resources/PawnRushStepSfx/Walk*, Run*;
    /// ArtSource/PawnRush/Sfx/bake_moves.py), taken in turn with no step twice in a row and a little off pitch.
    ///
    /// Not sent over the network (three a second for every pawn): every PC times the steps of every pawn it sees. A pawn
    /// steps while it is on its feet and moving over 0.7 m/s, at a cadence from its speed. On its feet: where the body is
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
        const float NearAll = 6f, FarNone = 26f;
        const int Voices = 8;
        const float Rescan = .5f;

        sealed class Feet
        {
            public Vector3 last;
            public Vector3 velocity;
            public float phase = .55f;
            public bool seen;
            public int lastClip = -1;
        }

        readonly List<AudioClip> walk = new List<AudioClip>(), run = new List<AudioClip>();
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
            }
            walk.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            run.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
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
                if (raw.sqrMagnitude > 400f) { f.phase = .55f; continue; }
                f.velocity = Vector3.Lerp(f.velocity, raw, 1f - Mathf.Exp(-dt / .08f));
                Vector3 floorVel;
                if (!OnFeet(pawn, hips, f.velocity.y, out floorVel)) { f.phase = .55f; continue; }
                Vector3 over = f.velocity - floorVel;
                float speed = new Vector2(over.x, over.z).magnitude;
                if (speed < MinSpeed) { f.phase = .55f; continue; }
                float cadence = speed < 2.5f ? Mathf.Lerp(1.6f, 2.9f, speed / 2.5f) : Mathf.Lerp(2.9f, 3.5f, Mathf.InverseLerp(2.5f, 3.6f, speed));
                f.phase += cadence * dt;
                if (f.phase < 1f) continue;
                f.phase = Mathf.Repeat(f.phase, 1f);
                bool sprint = pawn.NetworkPuppet ? speed > RunFrom : pawn.Sprinting || speed > RunFrom;
                Step(f, sprint, hips, speed, cam);
            }
        }

        bool OnFeet(RagdollPawn pawn, Vector3 hips, float vy, out Vector3 floorVel)
        {
            floorVel = Vector3.zero;
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
            }
            if (!pawn.NetworkPuppet) return true;
            // Upright: the hips stand about standHeight over the floor (lying down they are much lower).
            return found && best - .05f > pawn.standHeight * .6f;
        }

        void Step(Feet f, bool sprint, Vector3 at, float speed, Camera cam)
        {
            var set = sprint && run.Count > 0 ? run : walk.Count > 0 ? walk : run;
            int pick = Random.Range(0, set.Count);
            if (set.Count > 1 && pick == f.lastClip) pick = (pick + 1 + Random.Range(0, set.Count - 1)) % set.Count;
            f.lastClip = pick;
            float gain = (set == run ? RunGain : WalkGain) * Mathf.Lerp(.75f, 1f, Mathf.InverseLerp(MinSpeed, 2.5f, speed));
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
