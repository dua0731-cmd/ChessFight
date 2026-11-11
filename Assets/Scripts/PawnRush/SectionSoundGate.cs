using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R529 (승규 11-09, the drawbridge moat as the model: "도개교에 있는 플레이어만 그 안에서 소리 듣게 ... 멀리 있는 사람은
    // 물소리 약하고 가까이 있는 사람은 물소리 들리잖아" -> "모든 미니게임 코스 구간 그렇게 할거야"): a course sound is heard
    // only by a listener inside the section it plays in. Inside, each sound keeps its own distance fall-off (near loud,
    // far quiet); out of the section it fades to nothing over Fade metres, so the section next door, the corridor
    // between them and the room past it stay quiet, whatever the sound's own range.
    //
    // The section is the PawnRushSection whose beats (the level design's boxes, in its frame) hold the sound's spot;
    // the listener counts as inside when it is within any beat of that same section (a little loose: the camera rides
    // above and behind its pawn). A sound outside every section (the lobby, a skill test) is left alone.
    //
    // The gate never takes over a source's volume: it lets the owner set AudioSource.volume as before during Update,
    // multiplies it by the gate's gain in LateUpdate, and puts the owner's value back first thing next frame (only if
    // nobody changed it since), so code that reads or ramps its own volume sees its own numbers. It covers every
    // AudioSource on its GameObject, also ones added later.
    [DefaultExecutionOrder(-1000)]
    public sealed class SectionSoundGate : MonoBehaviour
    {
        const float Fade = 3f;             // metres past the section's beats to silence
        const float Follow = 10f;          // how fast the gain follows the listener (1/s)
        const float SourceLoose = 3f;      // a sound this far off a beat (sideways and down) still belongs to its section
        const float SourceLooseUp = 12f;   // ... and this far over it (chandeliers, the hall's vault)
        const float EarLoose = 1.5f;       // the listener this far off a beat still counts as inside
        const float EarLooseUp = 6f;       // ... and this far over it (the camera)

        static readonly List<PawnRushSection> sections = new List<PawnRushSection>();
        static AudioListener ear;

        readonly List<AudioSource> sources = new List<AudioSource>();
        readonly List<float> written = new List<float>();
        readonly List<float> owned = new List<float>();
        PawnRushSection room;
        Vector3 lookedAt;
        bool looked, settled;
        float gain = 1f;

        // The skill and pawn sounds (2D voices, CourseSoundPlace): full within NearFull of the listener, none at FarEnd,
        // as the course's own sounds fall off.
        const float SkillNearFull = 8f, SkillFarEnd = 40f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            sections.Clear();
            ear = null;
            ChessFight.Gameplay.CourseSoundPlace.Rule = PlaceGain;
        }

        // A one-off sound at `at` with no source of its own: 1 outside every section; inside one, its presence for
        // the listener times a linear fall-off with distance.
        static float PlaceGain(Vector3 at)
        {
            var room = SectionAt(at);
            if (room == null) return 1f;
            float presence = Presence(room);
            if (presence <= 0f || ear == null) return presence;
            float d = Vector3.Distance(ear.transform.position, at);
            return presence * (1f - Mathf.Clamp01((d - SkillNearFull) / (SkillFarEnd - SkillNearFull)));
        }

        // PawnRushSection tells us it is there (Start) and gone (OnDestroy).
        internal static void Register(PawnRushSection section)
        {
            if (section != null && !sections.Contains(section)) sections.Add(section);
        }

        internal static void Unregister(PawnRushSection section) => sections.Remove(section);

        // Gates the source (and every other one on its GameObject). Safe to call again.
        public static void Gate(AudioSource source)
        {
            if (source != null && source.GetComponent<SectionSoundGate>() == null)
                source.gameObject.AddComponent<SectionSoundGate>();
        }

        // Every source already under `root` (a section's prefab sounds, e.g. the folding bridge's).
        public static void GateAll(Transform root)
        {
            if (root == null) return;
            foreach (var a in root.GetComponentsInChildren<AudioSource>(true)) Gate(a);
        }

        // The section a world spot belongs to, or null.
        public static PawnRushSection SectionAt(Vector3 world)
        {
            for (int i = sections.Count - 1; i >= 0; i--)
            {
                var s = sections[i];
                if (s == null) { sections.RemoveAt(i); continue; }
                if (s.isActiveAndEnabled && Outside(s, world, SourceLoose, SourceLooseUp) <= 0f) return s;
            }
            return null;
        }

        // 1 with the listener inside `section`, down to 0 Fade metres out of it.
        public static float Presence(PawnRushSection section)
        {
            if (section == null) return 1f;
            if (ear == null || !ear.isActiveAndEnabled) ear = FindAnyObjectByType<AudioListener>();
            if (ear == null) return 1f;
            float o = Outside(section, ear.transform.position, EarLoose, EarLooseUp);
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(o / Fade));
        }

        // How far a world spot is out of the section's nearest beat (each beat grown by `loose`, `up` on top);
        // 0 or less inside. A section with no beats counts everything within 40 m of its root.
        static float Outside(PawnRushSection section, Vector3 world, float loose, float up)
        {
            var p = section.transform.InverseTransformPoint(world);
            var beats = section.Beats;
            if (beats == null || beats.Length == 0) return p.magnitude - 40f;
            float best = float.MaxValue;
            var grow = new Vector3(loose, loose, loose);
            foreach (var b in beats)
            {
                var min = b.min - grow;
                var max = b.max + grow + new Vector3(0f, up - loose, 0f);
                var d = Vector3.Max(Vector3.Max(min - p, p - max), Vector3.zero);
                float o = d.magnitude;
                if (o < best) best = o;
                if (best <= 0f) return 0f;
            }
            return best;
        }

        // First of all Updates: the owners' values back, unless an owner already changed one.
        void Update()
        {
            for (int i = 0; i < sources.Count; i++)
            {
                var a = sources[i];
                if (a != null && Mathf.Approximately(a.volume, written[i])) a.volume = owned[i];
            }
        }

        void LateUpdate()
        {
            var p = transform.position;
            if (!looked || (p - lookedAt).sqrMagnitude > .25f || (room != null && !room.isActiveAndEnabled))
            {
                var was = room;
                room = SectionAt(p);
                lookedAt = p;
                looked = true;
                // a pooled voice moved to another section plays its new sound at once at that section's level
                if (room != was) settled = false;
            }
            float target = Presence(room);
            gain = settled ? Mathf.Lerp(gain, target, 1f - Mathf.Exp(-Follow * Time.deltaTime)) : target;
            settled = true;

            GetComponents(sources);
            while (written.Count < sources.Count) { written.Add(-1f); owned.Add(1f); }
            for (int i = 0; i < sources.Count; i++)
            {
                var a = sources[i];
                owned[i] = a.volume;
                a.volume = owned[i] * gain;
                written[i] = a.volume;
            }
        }

        void OnDisable()
        {
            for (int i = 0; i < sources.Count; i++)
            {
                var a = sources[i];
                if (a != null && Mathf.Approximately(a.volume, written[i])) a.volume = owned[i];
            }
            sources.Clear();
            written.Clear();
            owned.Clear();
            settled = false;
        }

        // The gain now (tests and the F3 card).
        public float Gain => gain;
        public PawnRushSection Room => room;
    }
}
