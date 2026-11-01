using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // World pose samples are evaluated from the shared clock, never accumulated.
    [DefaultExecutionOrder(-200)]
    public sealed class SampledGardenMotion : MonoBehaviour
    {
        [Serializable] public sealed class Track { public string name, source; public float[] samples; }
        [Serializable] public sealed class MeshRole { public string name, source, collider; }
        [Serializable] public sealed class Take { public int fps, frames; public float duration; public Track[] tracks; public MeshRole[] meshes; }
        Take take;
        [SerializeField] TextAsset motionAsset;
        [SerializeField] Transform zone;
        [SerializeField] Transform[] targets;
        [SerializeField] Vector3[] restPositions;
        [SerializeField] Quaternion[] restRotations;
        Quaternion[] sourceRotations;
        SampledGardenSurface[] surfaces;
        double stepKey = double.NaN;
        void OnEnable() { if (motionAsset != null && zone != null) InitializeSaved(); }
        public void Configure(TextAsset asset, Transform course, Dictionary<string, Transform> nodes)
        {
            motionAsset = asset; take = JsonUtility.FromJson<Take>(asset.text); zone = course;
            int count = take.tracks.Length;
            targets = new Transform[count]; restPositions = new Vector3[count];
            restRotations = new Quaternion[count]; sourceRotations = new Quaternion[count]; surfaces = new SampledGardenSurface[count];
            // Capture all rest poses before moving a parent.
            for (int i = 0; i < count; i++)
            {
                var track = take.tracks[i];
                if (!nodes.TryGetValue(track.name, out targets[i])) throw new InvalidOperationException("Garden node missing: " + track.name);
                restPositions[i] = zone.InverseTransformPoint(targets[i].position);
                restRotations[i] = Quaternion.Inverse(zone.rotation) * targets[i].rotation;
            }
            InitializeSaved();
        }
        void InitializeSaved()
        {
            take = JsonUtility.FromJson<Take>(motionAsset.text);
            if (targets == null || targets.Length != take.tracks.Length) { take = null; Debug.LogError("Rebuild the royal garden: saved motion targets are missing."); return; }
            sourceRotations = new Quaternion[targets.Length]; surfaces = new SampledGardenSurface[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                sourceRotations[i] = Rotation(take.tracks[i].samples, 0);
                var body = targets[i].GetComponent<Rigidbody>();
                if (body == null) body = targets[i].gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true; body.useGravity = false;
                surfaces[i] = targets[i].GetComponent<SampledGardenSurface>();
                if (surfaces[i] == null) surfaces[i] = targets[i].gameObject.AddComponent<SampledGardenSurface>();
                surfaces[i].Configure(this);
            }
            stepKey = double.NaN;
            Apply(ObstacleClock.Now);
            foreach (var surface in surfaces) surface.ResetPose();
        }
        void FixedUpdate() => Step();
        public void Step()
        {
            if (take == null || Time.fixedTimeAsDouble == stepKey) return;
            stepKey = Time.fixedTimeAsDouble;
            foreach (var surface in surfaces) surface.BeforeStep();
            Apply(ObstacleClock.Now);
            foreach (var surface in surfaces) surface.AfterStep();
            Physics.SyncTransforms();
        }
        void Apply(double time)
        {
            double seconds = time % take.duration; if (seconds < 0) seconds += take.duration;
            float frame = (float)seconds * take.fps;
            int a = Mathf.Clamp(Mathf.FloorToInt(frame), 0, take.frames - 2), b = a + 1;
            float blend = frame - a;
            // Parent-before-child tracks contain world poses, avoiding double transforms.
            for (int i = 0; i < targets.Length; i++)
            {
                var values = take.tracks[i].samples;
                Vector3 position = Vector3.Lerp(Position(values, a), Position(values, b), blend);
                Quaternion rotation = Quaternion.Slerp(Rotation(values, a), Rotation(values, b), blend);
                Quaternion delta = rotation * Quaternion.Inverse(sourceRotations[i]);
                // Blender (x,y,z) -> zone (x,z,y): reflection reverses quaternion vector signs.
                Quaternion mapped = new Quaternion(-delta.x, -delta.z, -delta.y, delta.w);
                Vector3 offset = position - Position(values, 0);
                targets[i].SetPositionAndRotation(zone.TransformPoint(restPositions[i] + new Vector3(offset.x, offset.z, offset.y)),
                    zone.rotation * mapped * restRotations[i]);
            }
        }
        static Vector3 Position(float[] a, int frame) { int i = frame * 7; return new Vector3(a[i], a[i + 1], a[i + 2]); }
        static Quaternion Rotation(float[] a, int frame) { int i = frame * 7; return new Quaternion(a[i + 3], a[i + 4], a[i + 5], a[i + 6]); }
    }
}
