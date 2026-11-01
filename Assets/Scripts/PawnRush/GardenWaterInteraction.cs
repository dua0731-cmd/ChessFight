using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R279 (승규 11-01: "the waterfalls and the boats should interact with the water"): hands the garden water shader
    // (PR_GardenClearWater) what moves on or falls into the water. Each boat's position, heading, hull size and
    // speed become hull foam, a wake and ripples; each waterfall's landing line becomes churning foam and spreading
    // rings. Read only: it never moves anything (HANDOFF rule 7); the boats are moved by SampledGardenMotion.
    [DefaultExecutionOrder(100)]
    public sealed class GardenWaterInteraction : MonoBehaviour
    {
        public const int MaxBoats = 2, MaxSplashes = 12;
        static readonly int BoatId = Shader.PropertyToID("_GardenBoat");
        static readonly int BoatAxisId = Shader.PropertyToID("_GardenBoatAxis");
        static readonly int BoatCountId = Shader.PropertyToID("_GardenBoatCount");
        static readonly int SplashId = Shader.PropertyToID("_GardenSplash");
        static readonly int SplashInfoId = Shader.PropertyToID("_GardenSplashInfo");
        static readonly int SplashCountId = Shader.PropertyToID("_GardenSplashCount");

        [SerializeField] Transform zone;
        [SerializeField] Transform[] boats;
        [SerializeField] Vector3[] boatForward;       // zone space
        [SerializeField] Vector2[] boatHalfSize;      // half length, half width (m)
        [SerializeField] Vector3[] splashFrom, splashTo; // zone space, on the water surface

        readonly Vector4[] boatData = new Vector4[MaxBoats], boatAxis = new Vector4[MaxBoats];
        readonly Vector4[] splashData = new Vector4[MaxSplashes], splashInfo = new Vector4[MaxSplashes];
        Vector3[] lastPosition, heading;
        float[] speed;

        public void Configure(SampledGardenMotion.Take take, Transform course, Dictionary<string, Transform> nodes)
        {
            zone = course;
            var found = new List<Transform>();
            var forward = new List<Vector3>();
            var half = new List<Vector2>();
            if (take.boats != null)
                foreach (var boat in take.boats)
                {
                    if (found.Count == MaxBoats || boat.track == null || !nodes.TryGetValue(boat.track, out var node)) continue;
                    found.Add(node);
                    // Blender (x, y, z) -> zone (x, z, y), as for the sampled motion.
                    var f = boat.forward != null && boat.forward.Length == 3
                        ? new Vector3(boat.forward[0], boat.forward[2], boat.forward[1]) : Vector3.forward;
                    forward.Add(f.normalized);
                    half.Add(new Vector2(boat.halfLength, boat.halfWidth));
                }
            boats = found.ToArray();
            boatForward = forward.ToArray();
            boatHalfSize = half.ToArray();
            var from = new List<Vector3>();
            var to = new List<Vector3>();
            if (take.splashes != null)
                foreach (var s in take.splashes)
                {
                    if (from.Count == MaxSplashes) break;
                    from.Add(new Vector3(s.x0, s.z, s.y0));
                    to.Add(new Vector3(s.x1, s.z, s.y1));
                }
            splashFrom = from.ToArray();
            splashTo = to.ToArray();
            lastPosition = null;
        }

        void OnEnable() => lastPosition = null;

        void OnDisable()
        {
            Shader.SetGlobalFloat(BoatCountId, 0f);
            Shader.SetGlobalFloat(SplashCountId, 0f);
        }

        void LateUpdate()
        {
            if (zone == null) return;
            int nb = boats == null ? 0 : Mathf.Min(boats.Length, MaxBoats);
            if (lastPosition == null || lastPosition.Length != nb)
            {
                lastPosition = new Vector3[nb];
                heading = new Vector3[nb];
                speed = new float[nb];
                for (int i = 0; i < nb; i++)
                {
                    lastPosition[i] = boats[i] != null ? boats[i].position : Vector3.zero;
                    heading[i] = Flat(zone.TransformDirection(boatForward[i]));
                }
            }
            float dt = Time.deltaTime;
            for (int i = 0; i < nb; i++)
            {
                if (boats[i] == null) { boatAxis[i] = Vector4.zero; continue; }
                var p = boats[i].position;
                var axis = Flat(zone.TransformDirection(boatForward[i]));
                if (dt > 0f)
                {
                    // Boats move in the physics step: smooth the per-frame speed so frames without a step do not flicker.
                    float along = Vector3.Dot(p - lastPosition[i], axis) / dt;
                    speed[i] = Mathf.Lerp(speed[i], Mathf.Abs(along), 1f - Mathf.Exp(-dt * 3f));
                    if (Mathf.Abs(along) > .05f) heading[i] = along >= 0f ? axis : -axis;
                }
                lastPosition[i] = p;
                boatData[i] = new Vector4(p.x, p.y, p.z, speed[i]);
                boatAxis[i] = new Vector4(heading[i].x, heading[i].z, boatHalfSize[i].x, boatHalfSize[i].y);
            }
            int ns = splashFrom == null ? 0 : Mathf.Min(splashFrom.Length, MaxSplashes);
            for (int k = 0; k < ns; k++)
            {
                var a = zone.TransformPoint(splashFrom[k]);
                var b = zone.TransformPoint(splashTo[k]);
                splashData[k] = new Vector4(a.x, a.z, b.x, b.z);
                splashInfo[k] = new Vector4(a.y, 1f, 0f, 0f);
            }
            Shader.SetGlobalVectorArray(BoatId, boatData);
            Shader.SetGlobalVectorArray(BoatAxisId, boatAxis);
            Shader.SetGlobalFloat(BoatCountId, nb);
            Shader.SetGlobalVectorArray(SplashId, splashData);
            Shader.SetGlobalVectorArray(SplashInfoId, splashInfo);
            Shader.SetGlobalFloat(SplashCountId, ns);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }
    }
}
