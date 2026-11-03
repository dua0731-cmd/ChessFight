using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R287 (승규 11-02: "a boat comes out of the right-hand gate and strolls round the right of the lake; twice, once
    // after the start and once when the player passes the rotating section"): plays the royal boat's take from the
    // east water gate round the east lake and back. A run starts when the local pawn arrives in the start pen; the
    // boat sets out two seconds later, and once more when the pawn leaves the rotating plaza northward (after the
    // first tour if it is still out). Never more than two tours a run; hidden in between. Visual only and local: the
    // boat has no collider, and each PC plays it for its own pawn.
    public sealed class GardenBoatParade : MonoBehaviour
    {
        const float StartDelay = 2f;
        const int MaxTours = 2;
        // Zone space (Blender x, y): the start pen, and the plaza's north edge with the width it is left through.
        const float PenHalfWidth = 7f, PenSouth = -61f, PenNorth = -50f;
        const float PlazaHalfWidth = 19f, PlazaNorth = -16f, PlazaSouth = -50f;

        [SerializeField] TextAsset motionAsset;
        [SerializeField] Transform zone, boat;
        [SerializeField] Vector3 restPosition;
        [SerializeField] Quaternion restRotation;

        SampledGardenMotion.Parade take;
        Quaternion sourceRotation;
        float tourStart = -1f, setOutAt = -1f;
        int tours;
        bool queued, inPen, onPlaza;

        public void Configure(TextAsset asset, Transform course, Transform node)
        {
            motionAsset = asset; zone = course; boat = node;
            restPosition = zone.InverseTransformPoint(boat.position);
            restRotation = Quaternion.Inverse(zone.rotation) * boat.rotation;
            Load();
            boat.gameObject.SetActive(false);
        }

        void OnEnable() { if (motionAsset != null) Load(); }

        void Load()
        {
            take = JsonUtility.FromJson<SampledGardenMotion.Take>(motionAsset.text).parade;
            if (take == null || take.samples == null || take.frames < 2 || take.samples.Length < take.frames * 7) { take = null; return; }
            sourceRotation = SampledGardenMotion.Rotation(take.samples, 0);
            tourStart = setOutAt = -1f; tours = 0; queued = inPen = onPlaza = false;
        }

        void Update()
        {
            if (take == null || zone == null || boat == null) return;
            Watch();
            float now = Time.time;
            if (setOutAt >= 0f && now >= setOutAt) { setOutAt = -1f; SetOut(); }
            if (tourStart < 0f) return;
            float t = now - tourStart;
            if (t >= take.duration)
            {
                tourStart = -1f;
                boat.gameObject.SetActive(false);
                if (queued) { queued = false; SetOut(); }
                return;
            }
            Pose(t);
        }

        void Watch()
        {
            var pawn = MiniGameBase.LocalDriver?.FollowTarget;
            if (pawn == null) return;
            var p = zone.InverseTransformPoint(pawn.position);
            bool pen = Mathf.Abs(p.x) < PenHalfWidth && p.z > PenSouth && p.z < PenNorth;
            if (pen && !inPen)
            {
                // A new run (the start, a respawn or F1 into the garden): a tour already out counts as its first.
                tours = tourStart >= 0f ? 1 : 0;
                queued = false;
                if (tourStart < 0f) setOutAt = Time.time + StartDelay;
            }
            inPen = pen;
            bool wide = Mathf.Abs(p.x) < PlazaHalfWidth;
            if (onPlaza && wide && p.z > PlazaNorth && p.z < PlazaNorth + 12f)
            {
                if (tourStart >= 0f || setOutAt >= 0f) queued = true;
                else SetOut();
            }
            onPlaza = wide && p.z <= PlazaNorth && p.z > PlazaSouth;
        }

        void SetOut()
        {
            if (tours >= MaxTours) return;
            tours++;
            tourStart = Time.time;
            boat.gameObject.SetActive(true);
            Pose(0f);
        }

        void Pose(float seconds)
        {
            float frame = seconds * take.fps;
            int a = Mathf.Clamp(Mathf.FloorToInt(frame), 0, take.frames - 2), b = a + 1;
            float blend = Mathf.Clamp01(frame - a);
            var values = take.samples;
            Vector3 position = Vector3.Lerp(SampledGardenMotion.Position(values, a), SampledGardenMotion.Position(values, b), blend);
            Quaternion rotation = Quaternion.Slerp(SampledGardenMotion.Rotation(values, a), SampledGardenMotion.Rotation(values, b), blend);
            Quaternion delta = rotation * Quaternion.Inverse(sourceRotation);
            // Blender (x,y,z) -> zone (x,z,y), as in SampledGardenMotion.
            Quaternion mapped = new Quaternion(-delta.x, -delta.z, -delta.y, delta.w);
            Vector3 offset = position - SampledGardenMotion.Position(values, 0);
            boat.SetPositionAndRotation(zone.TransformPoint(restPosition + new Vector3(offset.x, offset.z, offset.y)), zone.rotation * mapped * restRotation);
        }
    }
}
