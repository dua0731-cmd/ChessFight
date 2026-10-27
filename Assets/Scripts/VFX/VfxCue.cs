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

        readonly List<int> order = new List<int>();
        Vector3? target;
        float clock;
        int next;

        void OnEnable() => Play();

        public void Play()
        {
            clock = 0f;
            next = 0;
            order.Clear();
            for (int i = 0; i < steps.Count; i++) order.Add(i);
            // Ties keep their list order (List.Sort is not stable).
            order.Sort((a, b) => steps[a].time != steps[b].time ? steps[a].time.CompareTo(steps[b].time) : a.CompareTo(b));
            Feed(gameObject);
            Fire();
        }

        /// <summary>The world point the effect aims at, for graphs that expose TargetPosition.</summary>
        public void Aim(Vector3 world)
        {
            target = world;
            Feed(gameObject);
        }

        void Update()
        {
            clock += Time.deltaTime;
            Fire();
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
