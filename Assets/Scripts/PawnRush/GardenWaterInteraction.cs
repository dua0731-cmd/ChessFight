using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R279 (승규 11-01: "the waterfalls and the boats should interact with the water"): hands the garden water shader
    // (PR_GardenClearWater) what moves on or falls into the water. Each boat's position, heading, hull size and
    // speed become hull foam, a wake and ripples; each waterfall's landing line becomes churning foam and spreading
    // rings. R280: each bobbing platform's rim, radius and vertical speed become rings that spread by the water's
    // dispersion relation. R283: up to 24 landing lines (one per outlet), each boat's heave and rocking speed (its rings
    // grow with them) and the moat height the falls blend into (_GardenFoot). The landing arrays were renamed from
    // _GardenSplash* because Unity keeps a global array's first size for the whole editor session.
    // Read only: it never moves anything (HANDOFF rule 7); SampledGardenMotion moves them.
    [DefaultExecutionOrder(100)]
    public sealed class GardenWaterInteraction : MonoBehaviour
    {
        public const int MaxBoats = 2, MaxSplashes = 24, MaxBobbers = 8;
        const float MoatLevel = -1.7f;   // Blender z of the moat surface the platforms stand in
        static readonly int BoatId = Shader.PropertyToID("_GardenBoat");
        static readonly int BoatAxisId = Shader.PropertyToID("_GardenBoatAxis");
        static readonly int BoatCountId = Shader.PropertyToID("_GardenBoatCount");
        static readonly int BoatInfoId = Shader.PropertyToID("_GardenBoatInfo");
        static readonly int FootId = Shader.PropertyToID("_GardenFoot");
        static readonly int SplashId = Shader.PropertyToID("_GardenLanding");
        static readonly int SplashInfoId = Shader.PropertyToID("_GardenLandingInfo");
        static readonly int SplashCountId = Shader.PropertyToID("_GardenLandingCount");
        static readonly int BobberId = Shader.PropertyToID("_GardenBobber");
        static readonly int BobberInfoId = Shader.PropertyToID("_GardenBobberInfo");
        static readonly int BobberCountId = Shader.PropertyToID("_GardenBobberCount");

        [SerializeField] Transform zone;
        [SerializeField] Transform[] boats;
        [SerializeField] Vector3[] boatForward;       // zone space
        [SerializeField] Vector2[] boatHalfSize;      // half length, half width (m)
        [SerializeField] Vector3[] splashFrom, splashTo; // zone space, on the water surface
        [SerializeField] Transform[] bobbers;
        [SerializeField] float[] bobberRadius;

        readonly Vector4[] boatData = new Vector4[MaxBoats], boatAxis = new Vector4[MaxBoats], boatInfo = new Vector4[MaxBoats];
        readonly Vector4[] splashData = new Vector4[MaxSplashes], splashInfo = new Vector4[MaxSplashes];
        readonly Vector4[] bobberData = new Vector4[MaxBobbers], bobberInfo = new Vector4[MaxBobbers];
        Vector3[] lastPosition, heading, lastBobber;
        float[] speed, bobberSpeed, heave, rock;
        Quaternion[] lastRotation;

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
            var bob = new List<Transform>();
            var radius = new List<float>();
            if (take.bobbers != null)
                foreach (var b in take.bobbers)
                {
                    if (bob.Count == MaxBobbers || b.track == null || !nodes.TryGetValue(b.track, out var node)) continue;
                    bob.Add(node);
                    radius.Add(b.radius);
                }
            bobbers = bob.ToArray();
            bobberRadius = radius.ToArray();
            lastPosition = null;
            lastBobber = null;
        }

        void OnEnable() { lastPosition = null; lastBobber = null; }

        void OnDisable()
        {
            Shader.SetGlobalFloat(BoatCountId, 0f);
            Shader.SetGlobalFloat(SplashCountId, 0f);
            Shader.SetGlobalFloat(BobberCountId, 0f);
            Shader.SetGlobalVector(FootId, Vector4.zero);
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
                heave = new float[nb];
                rock = new float[nb];
                lastRotation = new Quaternion[nb];
                for (int i = 0; i < nb; i++)
                {
                    lastPosition[i] = boats[i] != null ? boats[i].position : Vector3.zero;
                    lastRotation[i] = boats[i] != null ? boats[i].rotation : Quaternion.identity;
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
                    // Heave (m/s) and rocking (rad/s) push rings out of the hull.
                    float up = (p.y - lastPosition[i].y) / dt;
                    float turn = Quaternion.Angle(lastRotation[i], boats[i].rotation) * Mathf.Deg2Rad / dt;
                    heave[i] = Mathf.Lerp(heave[i], up, 1f - Mathf.Exp(-dt * 4f));
                    rock[i] = Mathf.Lerp(rock[i], turn, 1f - Mathf.Exp(-dt * 4f));
                }
                lastPosition[i] = p;
                lastRotation[i] = boats[i].rotation;
                boatData[i] = new Vector4(p.x, p.y, p.z, speed[i]);
                boatAxis[i] = new Vector4(heading[i].x, heading[i].z, boatHalfSize[i].x, boatHalfSize[i].y);
                boatInfo[i] = new Vector4(heave[i], rock[i], 0f, 0f);
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
            Shader.SetGlobalVectorArray(BoatInfoId, boatInfo);
            Shader.SetGlobalFloat(BoatCountId, nb);
            Shader.SetGlobalVectorArray(SplashId, splashData);
            Shader.SetGlobalVectorArray(SplashInfoId, splashInfo);
            Shader.SetGlobalFloat(SplashCountId, ns);
            float water = zone.TransformPoint(new Vector3(0f, MoatLevel, 0f)).y;
            Shader.SetGlobalVector(FootId, new Vector4(water, 0f, 0f, 1f));

            int nbob = bobbers == null ? 0 : Mathf.Min(bobbers.Length, MaxBobbers);
            if (lastBobber == null || lastBobber.Length != nbob)
            {
                lastBobber = new Vector3[nbob];
                bobberSpeed = new float[nbob];
                for (int i = 0; i < nbob; i++) lastBobber[i] = bobbers[i] != null ? bobbers[i].position : Vector3.zero;
            }
            for (int i = 0; i < nbob; i++)
            {
                if (bobbers[i] == null) { bobberData[i] = Vector4.zero; continue; }
                var p = bobbers[i].position;
                if (dt > 0f) bobberSpeed[i] = Mathf.Lerp(bobberSpeed[i], (p.y - lastBobber[i].y) / dt, 1f - Mathf.Exp(-dt * 4f));
                lastBobber[i] = p;
                bobberData[i] = new Vector4(p.x, water, p.z, bobberRadius[i]);
                bobberInfo[i] = new Vector4(bobberSpeed[i], water, 0f, 0f);
            }
            Shader.SetGlobalVectorArray(BobberId, bobberData);
            Shader.SetGlobalVectorArray(BobberInfoId, bobberInfo);
            Shader.SetGlobalFloat(BobberCountId, nbob);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }
    }
}
