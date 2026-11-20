using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // R725 ro-vfx (Royal Observatory R7, device VFX 3 of 4 in this session; user 11-20 "Start ③"): the great telescope's
    // focus beams. FocusBeam moves the hot spot over the deck on the shared clock and throws who stays in it; this only
    // draws it from the same clock, so every PC shows the same spot and nothing is sent.
    //   column  light in air from the objective's focus lens (the pearl bead at the ring's middle, FocusBeam.Lens) down
    //           to the spot: no surface, worked out along each ray of sight inside a bounding cone, fading smoothly to
    //           its sides, brighter seen along it and toward the lens, hidden behind what stands in front of it
    //           (Resources/RoyalObservatory/Vfx/RoFocusBeam; user 11-20 "먼지 지워주고 ... 실사처럼": no dust)
    //   spot    light falling on the deck (a decal multiplying the deck, so its marble shows: RoFocusSpot), a little
    //           hotter in its middle, its edge exactly at FocusBeam.Radius - what throws is what is lit
    //   source  (user 11-20 "여기서 나오는 빛을 더 세게 해서 밑에서 플레이어들이 빛인 거를 알 수 있게") a glowing star of light
    //           at the focus lens with slow turning rays (the sun's corona shader, RoSunCorona), so it reads as the
    //           light's source from the deck; one for both beams (they share the lens)
    // The graybox's flat disc and pale cylinder stop drawing. Further than `farDistance` from the camera nothing is drawn.
    [DisallowMultipleComponent]
    public sealed class RoyalObservatoryVfxBeam : MonoBehaviour
    {
        const string ColumnPath = "RoyalObservatory/Vfx/RoFocusBeam", SpotPath = "RoyalObservatory/Vfx/RoFocusSpot",
            SourcePath = "RoyalObservatory/Vfx/RoSunCorona";
        const int Segments = 32, Rows = 12;
        const float Bound = 1.35f;   // the cone drawn round the light, times its radius

        // Off: the beams as the graybox draws them (a switch for comparing).
        public static bool Enabled = true;

        static readonly int AgeId = Shader.PropertyToID("_Age"), LengthId = Shader.PropertyToID("_Length"),
            BeamStartId = Shader.PropertyToID("_BeamStart"), BeamEndId = Shader.PropertyToID("_BeamEnd"),
            SpotRadiusId = Shader.PropertyToID("_SpotRadius"), LensRadiusId = Shader.PropertyToID("_LensRadius"),
            LightId = Shader.PropertyToID("_Light"), RadiusId = Shader.PropertyToID("_Radius"), HalfId = Shader.PropertyToID("_Half"),
            SunRadiusId = Shader.PropertyToID("_SunRadius"), SizeId = Shader.PropertyToID("_Size"), CoronaReachId = Shader.PropertyToID("_CoronaReach"),
            FlamesId = Shader.PropertyToID("_Flames"), FlameReachId = Shader.PropertyToID("_FlameReach"), FlashId = Shader.PropertyToID("_Flash"),
            RaysId = Shader.PropertyToID("_Rays"), RayReachId = Shader.PropertyToID("_RayReach"), ShockLightId = Shader.PropertyToID("_ShockLight");

        [Header("The column")]
        [Tooltip("The column's radius at the lens (the spot's is FocusBeam's radius).")]
        public float lensRadius = .18f;
        public float columnLight = 1f;
        [Header("The spot")]
        public float spotLight = 1f;
        [Header("The source at the lens")]
        [Tooltip("The glowing star at the focus lens: its size (m), light and rays.")]
        public float sourceRadius = .22f, sourceLight = 1.6f, sourceRays = 2.6f;
        [Header("Cost")]
        [Tooltip("Further than this from the camera (m) nothing is drawn.")]
        public float farDistance = 70f;

        FocusBeam beam;
        Material columnMaterial, spotMaterial, sourceMaterial;
        MeshRenderer column, spot, source;
        Mesh cone, quad;
        MaterialPropertyBlock block;
        bool ready;

        void Start()
        {
            beam = GetComponent<FocusBeam>();
            block = new MaterialPropertyBlock();
            var columnShader = Resources.Load<Shader>(ColumnPath);
            var spotShader = Resources.Load<Shader>(SpotPath);
            if (beam == null || columnShader == null || spotShader == null || !columnShader.isSupported || !spotShader.isSupported)
            {
                Debug.LogWarning("[천문대 VFX] 초점 광선 셰이더가 없습니다(Resources/RoyalObservatory/Vfx/RoFocusBeam · RoFocusSpot): 회색 상자 그대로");
                enabled = false;
                return;
            }
            columnMaterial = new Material(columnShader) { name = "RO focus beam column" };
            spotMaterial = new Material(spotShader) { name = "RO focus beam spot" };
            cone = Cone(Mathf.Clamp(lensRadius / Mathf.Max(beam.Radius, .01f), .02f, 1f));
            column = Part("Light column", cone, columnMaterial);
            spot = Part("Hot spot (decal)", Resources.GetBuiltinResource<Mesh>("Cube.fbx"), spotMaterial);
            // The source: one for both beams (the first draws it; they share the lens).
            var sourceShader = Resources.Load<Shader>(SourcePath);
            if (name.EndsWith(" 1") && sourceShader != null && sourceShader.isSupported)
            {
                sourceMaterial = new Material(sourceShader) { name = "RO focus lens glow" };
                quad = Quad();
                source = Part("Lens glow", quad, sourceMaterial);
                source.transform.localPosition = beam.Lens;
            }
            // The graybox's disc and cylinder: FocusBeam still moves them, they no longer draw.
            foreach (var name in new[] { "Hot spot", "Light shaft" })
            {
                var part = transform.Find(name);
                if (part != null && part.TryGetComponent<Renderer>(out var r)) r.enabled = false;
            }
            ready = true;
        }

        MeshRenderer Part(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return r;
        }

        // A quad round the origin, its corners in uv; the corona shader turns it to the camera and sizes it.
        static Mesh Quad()
        {
            var mesh = new Mesh { name = "RO focus lens glow quad", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(new List<Vector3> { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 6f);
            return mesh;
        }

        // An open cone along +y from y 0 (radius 1, the spot) to y 1 (radius `tip`, the lens): uv.x round it, uv.y up.
        static Mesh Cone(float tip)
        {
            var points = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float slope = 1f - tip;   // how fast it narrows (radius lost per unit up)
            for (int j = 0; j <= Rows; j++)
            {
                float y = j / (float)Rows, r = Mathf.Lerp(1f, tip, y);
                for (int k = 0; k <= Segments; k++)
                {
                    float a = 2f * Mathf.PI * k / Segments;
                    points.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                    normals.Add(new Vector3(Mathf.Cos(a), slope, Mathf.Sin(a)).normalized);
                    uvs.Add(new Vector2(k / (float)Segments, y));
                }
            }
            int row = Segments + 1;
            for (int j = 0; j < Rows; j++)
                for (int k = 0; k < Segments; k++)
                {
                    int a = j * row + k, b = a + 1, c = a + row, d = c + 1;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            var mesh = new Mesh { name = "RO focus beam cone", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        void LateUpdate()
        {
            if (!ready) return;
            var cam = Camera.main;
            bool near = cam != null && (cam.transform.position - transform.position).sqrMagnitude < farDistance * farDistance;
            bool on = Enabled && near;
            if (column.enabled != on) column.enabled = on;
            if (spot.enabled != on) spot.enabled = on;
            if (source != null && source.enabled != on) source.enabled = on;
            if (!on) return;

            double now = ObstacleClock.Now;
            float age = (float)(now % 600.0), radius = beam.Radius;
            var at = beam.SpotAt(now);   // the same spot FocusBeam throws from
            var reach = beam.Lens - at;
            float length = reach.magnitude;

            // The column from the spot (its base) up to the lens (its tip).
            column.transform.localPosition = at;
            column.transform.localRotation = length > .01f ? Quaternion.FromToRotation(Vector3.up, reach / length) : Quaternion.identity;
            // A bound a little wider than the light: the shader works out the light along each ray inside it (user 11-20
            // "실사처럼" - light in air, no surface), from the beam's ends and radii in the world.
            column.transform.localScale = new Vector3(radius * Bound, Mathf.Max(length, .01f), radius * Bound);
            column.GetPropertyBlock(block);
            block.SetFloat(AgeId, age);
            block.SetFloat(LengthId, length);
            block.SetVector(BeamStartId, transform.TransformPoint(at));
            block.SetVector(BeamEndId, transform.TransformPoint(beam.Lens));
            float worldScale = transform.lossyScale.x;
            block.SetFloat(SpotRadiusId, radius * worldScale);
            block.SetFloat(LensRadiusId, lensRadius * worldScale);
            block.SetFloat(LightId, columnLight);
            column.SetPropertyBlock(block);

            // The spot: a box on the deck (this object's frame: the deck is y 0), its floor painted.
            float half = radius + .6f;
            spot.transform.localPosition = at + Vector3.up * .2f;
            spot.transform.localRotation = Quaternion.identity;
            spot.transform.localScale = new Vector3(2f * half, 1.2f, 2f * half);
            spot.GetPropertyBlock(block);
            block.SetFloat(AgeId, age);
            block.SetFloat(RadiusId, radius);
            block.SetFloat(HalfId, half);
            block.SetFloat(LightId, spotLight);
            spot.SetPropertyBlock(block);

            // The source: a small hot star with a soft glow and slow turning rays, breathing a little.
            if (source != null)
            {
                source.GetPropertyBlock(block);
                float r = Mathf.Max(sourceRadius, .02f), breathe = 1f + .08f * Mathf.Sin(age * 2.3f);
                block.SetFloat(SunRadiusId, r);
                block.SetFloat(SizeId, r * 7.5f);
                block.SetFloat(LightId, sourceLight * breathe);
                block.SetFloat(CoronaReachId, 1.6f);
                block.SetFloat(FlamesId, 0f);
                block.SetFloat(FlameReachId, .1f);
                block.SetFloat(FlashId, .9f);
                block.SetFloat(ShockLightId, 0f);
                block.SetFloat(RaysId, sourceRays);
                block.SetFloat(RayReachId, 4.5f);
                block.SetFloat(AgeId, age);
                source.SetPropertyBlock(block);
            }
        }

        void OnDisable()
        {
            if (!ready) return;
            column.enabled = false;
            spot.enabled = false;
            if (source != null) source.enabled = false;
        }

        void OnDestroy()
        {
            if (columnMaterial != null) Destroy(columnMaterial);
            if (spotMaterial != null) Destroy(spotMaterial);
            if (cone != null) Destroy(cone);
            if (sourceMaterial != null) Destroy(sourceMaterial);
            if (quad != null) Destroy(quad);
        }
    }
}
