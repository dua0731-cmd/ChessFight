using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace ChessFight.VFX
{
    /// <summary>
    /// The root of one effect prefab (Assets/VFX/Prefabs/&lt;Piece&gt;/, 10-27): where on the piece it starts and the
    /// timing of its parts. Each <see cref="Step"/> at its time sends a VFX Graph event to a part, turns a part on or
    /// off, or plays or stops a part's particle systems, so a slash arc, its trail and its splatters start in order
    /// without a Timeline. It also hands its graphs two exposed properties when they declare them: TargetPosition (the
    /// world point the effect aims at: the workbench's target pawn, later a skill's victim) and Density (the global
    /// particle multiplier <see cref="Density"/>; VFX Graph cannot expose Capacity, so a graph sizes its Capacity for a
    /// strong PC and multiplies its spawn counts by Density). A graph timed by steps leaves its Initial Event Name empty,
    /// or it plays once on enable and again at its step. A prefab without a cue still plays in the workbench, from the
    /// chest, for three seconds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxCue : MonoBehaviour
    {
        public enum Anchor { Feet, Chest, Head, Target }
        public enum Act { VfxEvent, TurnOn, TurnOff, PlayParticles, StopParticles }

        [Serializable]
        public struct Step
        {
            [Min(0f)] public float time;
            public GameObject part;
            public Act act;
            [Tooltip("The VFX Graph event for VfxEvent; empty sends OnPlay.")]
            public string vfxEvent;
        }

        public const string TargetName = "TargetPosition", DensityName = "Density";
        static readonly int TargetId = Shader.PropertyToID(TargetName), DensityId = Shader.PropertyToID(DensityName);

        /// <summary>Particle density for every cue, 1 = as authored. Lower on a weak laptop (the workbench's G).</summary>
        public static float Density = 1f;

        [Tooltip("Where on the piece the effect starts.")]
        public Anchor anchor = Anchor.Chest;
        [Tooltip("Moves with the piece (a dash) instead of staying where it started.")]
        public bool follow;
        [Min(.1f), Tooltip("Seconds the effect runs; the workbench clears or replays it after this.")]
        public float length = 3f;
        public List<Step> steps = new List<Step>();
        [Tooltip("Seconds after the start the capture tool (ChessFight/VFX/Capture) takes a frame at.")]
        public float[] captureTimes = { .05f, .15f, .3f, .6f, 1f };
        [Tooltip("Metres over the capture floor the capture tool puts the effect (one that plays at a chest, not the feet).")]
        public float captureHeight;

        /// <summary>The shader property a cue sets to its own clock on its parts' materials, in place of _Time (R172).</summary>
        public const string TimeName = "_VfxTime";
        static readonly int TimeId = Shader.PropertyToID(TimeName);

        readonly List<int> order = new List<int>();
        readonly List<Renderer> timed = new List<Renderer>();
        List<KeyValuePair<GameObject, bool>> startActive;
        MaterialPropertyBlock timeBlock;
        Vector3? target;
        float clock;
        int next;
        bool held;

        /// <summary>Seconds an effect is kept after its cue ends, for its last particles to fade.</summary>
        public const float Linger = 2f;

        /// <summary>
        /// Plays an effect prefab from game code: a copy at <paramref name="at"/> turned to <paramref name="facing"/> (+Z
        /// is the way it goes), under <paramref name="parent"/> if given, aimed at <paramref name="target"/>, and removed
        /// once its cue has run (three seconds for a prefab without one) plus <see cref="Linger"/>.
        /// </summary>
        public static GameObject Spawn(GameObject prefab, Vector3 at, Quaternion facing, Transform parent = null, Vector3? target = null)
        {
            if (prefab == null) return null;
            var go = Instantiate(prefab, at, facing, parent);
            go.name = prefab.name;
            var cue = go.GetComponent<VfxCue>();
            if (cue != null && target.HasValue) cue.Aim(target.Value);
            Destroy(go, (cue != null ? cue.length : 3f) + Linger);
            return go;
        }

        void OnEnable() => Play();

        public void Play()
        {
            clock = 0f;
            next = 0;
            held = false;
            Order();
            Feed(gameObject);
            Fire();
            Tick();
        }

        void Order()
        {
            order.Clear();
            for (int i = 0; i < steps.Count; i++) order.Add(i);
            // Ties keep their list order (List.Sort is not stable).
            order.Sort((a, b) => steps[a].time != steps[b].time ? steps[a].time.CompareTo(steps[b].time) : a.CompareTo(b));
            if (startActive == null)
            {
                // How the parts stood before any step ran, so a seek can start from there again.
                startActive = new List<KeyValuePair<GameObject, bool>>();
                foreach (var step in steps)
                    if (step.part != null && (step.act == Act.TurnOn || step.act == Act.TurnOff))
                        startActive.Add(new KeyValuePair<GameObject, bool>(step.part, step.part.activeSelf));
                timed.Clear();
                foreach (var view in GetComponentsInChildren<Renderer>(true))
                    if (view.sharedMaterial != null && view.sharedMaterial.HasProperty(TimeId)) timed.Add(view);
            }
        }

        /// <summary>
        /// Puts the effect in the state it has <paramref name="t"/> seconds after it starts, and holds it there (R172, the
        /// capture tool; works in edit mode too): parts are turned on and off as the steps up to t say, every particle
        /// system is simulated from when it started (fixed seeds, so the same t gives the same frame), every
        /// <see cref="IVfxSeekable"/> part is set to its own age and _VfxTime is set to t. VFX Graph parts are not
        /// seeked. <see cref="Play"/> lets it run again.
        /// </summary>
        public void Seek(float t)
        {
            held = true;
            Order();
            foreach (var pair in startActive) pair.Key.SetActive(pair.Value);
            var onAt = new Dictionary<GameObject, float>();
            var playAt = new Dictionary<GameObject, float>();
            var stopAt = new Dictionary<GameObject, float>();
            next = 0;
            while (next < order.Count && steps[order[next]].time <= t)
            {
                var step = steps[order[next++]];
                if (step.part == null) continue;
                switch (step.act)
                {
                    case Act.TurnOn: step.part.SetActive(true); onAt[step.part] = step.time; break;
                    case Act.TurnOff: step.part.SetActive(false); break;
                    case Act.PlayParticles: playAt[step.part] = step.time; stopAt.Remove(step.part); break;
                    case Act.StopParticles: stopAt[step.part] = step.time; break;
                }
            }
            clock = t;

            uint seed = 1;
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(false))
            {
                // Each system is simulated on its own (sub-emitters are not used by the builders).
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.useAutoRandomSeed = false;
                ps.randomSeed = seed++ * 7919u;
                float on = Latest(onAt, ps.transform, 0f);
                float start = Latest(playAt, ps.transform, ps.main.playOnAwake ? on : float.NaN);
                if (float.IsNaN(start) || start > t) continue;
                float stop = Latest(stopAt, ps.transform, float.NaN);
                if (!float.IsNaN(stop) && stop >= start && stop <= t)
                {
                    ps.Simulate(stop - start, false, true, true);
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                    ps.Simulate(t - stop, false, false, true);
                }
                else ps.Simulate(t - start, false, true, true);
            }
            foreach (var part in GetComponentsInChildren<MonoBehaviour>(false))
                if (part is IVfxSeekable seekable) seekable.Seek(t - Latest(onAt, part.transform, 0f));
            Tick();
        }

        /// <summary>The latest time in <paramref name="at"/> for <paramref name="from"/> or a parent of it up to this cue.</summary>
        float Latest(Dictionary<GameObject, float> at, Transform from, float none)
        {
            float best = none;
            for (var x = from; x != null; x = x == transform ? null : x.parent)
                if (at.TryGetValue(x.gameObject, out float time) && (float.IsNaN(best) || time > best)) best = time;
            return best;
        }

        /// <summary>The cue's clock into _VfxTime on the materials that have it.</summary>
        void Tick()
        {
            if (timed.Count == 0) return;
            if (timeBlock == null) timeBlock = new MaterialPropertyBlock();
            foreach (var view in timed)
            {
                if (view == null) continue;
                view.GetPropertyBlock(timeBlock);
                timeBlock.SetFloat(TimeId, clock);
                view.SetPropertyBlock(timeBlock);
            }
        }

        /// <summary>The world point the effect aims at, for graphs that expose TargetPosition.</summary>
        public void Aim(Vector3 world)
        {
            target = world;
            Feed(gameObject);
        }

        void Update()
        {
            if (held) return;
            clock += Time.deltaTime;
            Fire();
            Tick();
        }

        void Fire()
        {
            while (next < order.Count && steps[order[next]].time <= clock) Run(steps[order[next++]]);
        }

        void Feed(GameObject root)
        {
            foreach (var vfx in root.GetComponentsInChildren<VisualEffect>(true))
            {
                if (target.HasValue && vfx.HasVector3(TargetId)) vfx.SetVector3(TargetId, target.Value);
                if (vfx.HasFloat(DensityId)) vfx.SetFloat(DensityId, Density);
            }
        }

        void Run(Step step)
        {
            if (step.part == null) return;
            switch (step.act)
            {
                case Act.TurnOn:
                    step.part.SetActive(true);
                    // A graph that was off when the cue started gets its inputs now.
                    Feed(step.part);
                    break;
                case Act.TurnOff:
                    step.part.SetActive(false);
                    break;
                case Act.PlayParticles:
                    foreach (var ps in step.part.GetComponentsInChildren<ParticleSystem>()) ps.Play(false);
                    break;
                case Act.StopParticles:
                    foreach (var ps in step.part.GetComponentsInChildren<ParticleSystem>())
                        ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                    break;
                default:
                    string vfxEvent = string.IsNullOrEmpty(step.vfxEvent) ? "OnPlay" : step.vfxEvent;
                    foreach (var vfx in step.part.GetComponentsInChildren<VisualEffect>()) vfx.SendEvent(vfxEvent);
                    break;
            }
        }
    }
}
