using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R725 ro-vfx2 (Royal Observatory R7, the hall's air, PLAN 5 "공중 먼지 · 별가루, 심연에서 올라오는 희미한 성운 빛"):
    //   motes   dust and gold star motes hanging in the air round the camera, world-fixed and drifting
    //           (Resources/RoyalObservatory/Vfx2/RoHallMotes.shader); where they cross ro-post's moon shaft
    //           (RoyalObservatoryLighting's "Moon shaft" mesh, read here, not changed) they light up cold - no second beam
    //   abyss   faint clouds of nebula light welling up out of the abyss under the hall floor and fading before the
    //           floor's level (RoAbyssGlow.shader), over the abyss's own swirling nebula (RoyalObservatoryDressing.Hall)
    // Ambient only: nothing here is timed by a device, so it runs on the render clock. Drawn only while the camera is in
    // the hall's well (its radius, from the abyss up to the dome). Lives on the Content, in its frame.
    [DisallowMultipleComponent]
    public sealed class RoyalObservatoryVfxHall : MonoBehaviour
    {
        const string MotesShaderPath = "RoyalObservatory/Vfx2/RoHallMotes", GlowShaderPath = "RoyalObservatory/Vfx2/RoAbyssGlow";
        // The hall in the Content's frame (RoyalObservatoryBuilder / RoyalObservatoryLighting): its axis at x 0, z 64,
        // 50 m round, the graybox floor at y -18, the dome's top about y 64.
        static readonly Vector3 HallAxis = new Vector3(0f, 0f, 64f);
        const float HallRadius = 50f, HallFloor = -18f, HallTop = 62f, AbyssBottom = -110f;

        public static bool Enabled = true;

        [Header("Motes in the air")]
        [Range(0, 2000)] public int moteCount = 520;
        [Range(0f, 1f)] public float starShare = .2f;
        [Tooltip("Metres a side of the box of motes laid round the camera.")]
        public float box = 28f;
        public float moteSize = .03f;
        public float moteLight = 1f;
        [Tooltip("How bright the dust is out of the moon shaft, and in it.")]
        public float dustLight = .3f, shaftLight = 2.6f;
        public Vector3 drift = new Vector3(.05f, .06f, .03f);

        [Header("Nebula glow from the abyss")]
        [Range(0, 30)] public int cloudCount = 9;
        [Tooltip("Metres across (least, most).")]
        public Vector2 cloudSize = new Vector2(12f, 20f);
        [Tooltip("Where the clouds start (y, the Content's frame) and how far they rise over a life.")]
        public float cloudFrom = -40f, cloudRise = 20f;
        public float cloudSeconds = 18f;
        public float cloudLight = .12f;

        Material motesMaterial, glowMaterial;
        Mesh motesMesh, glowMesh;
        MeshRenderer motes, glow;
        Transform shaft;
        float lookForShaft;
        bool ready;

        void Start()
        {
            var motesShader = Resources.Load<Shader>(MotesShaderPath);
            var glowShader = Resources.Load<Shader>(GlowShaderPath);
            ready = motesShader != null && motesShader.isSupported && glowShader != null && glowShader.isSupported;
            if (!ready)
            {
                Debug.LogWarning("[천문대 VFX2] 홀 먼지 · 심연 성운 셰이더를 찾지 못해 효과를 끕니다");
                enabled = false;
                return;
            }
            motesMaterial = new Material(motesShader) { name = "RO hall motes" };
            glowMaterial = new Material(glowShader) { name = "RO abyss glow" };
            motesMesh = MotesMesh();
            glowMesh = GlowMesh();
            motes = Part("Hall motes", motesMesh, motesMaterial);
            glow = Part("Abyss nebula glow", glowMesh, glowMaterial);
        }

        MeshRenderer Part(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            r.enabled = false;
            return r;
        }

        void LateUpdate()
        {
            if (!ready) return;
            var view = Camera.main;
            bool inHall = false;
            if (Enabled && view != null)
            {
                var p = transform.InverseTransformPoint(view.transform.position);
                var flat = new Vector2(p.x - HallAxis.x, p.z - HallAxis.z);
                inHall = flat.magnitude < HallRadius + 2f && p.y > AbyssBottom && p.y < HallTop + 4f;
            }
            motes.enabled = inHall && moteCount > 0;
            glow.enabled = inHall && cloudCount > 0;
            if (!inHall) return;

            var centre = transform.TransformPoint(HallAxis);
            motesMaterial.SetColor("_Color", new Color(1f, .86f, .66f));
            motesMaterial.SetFloat("_Light", moteLight);
            motesMaterial.SetFloat("_DustLight", dustLight);
            motesMaterial.SetFloat("_ShaftLight", shaftLight);
            motesMaterial.SetFloat("_Box", Mathf.Max(4f, box));
            motesMaterial.SetFloat("_Size", moteSize);
            motesMaterial.SetVector("_Drift", drift);
            motesMaterial.SetVector("_HallCentre", centre);
            motesMaterial.SetFloat("_HallRadius", HallRadius);
            motesMaterial.SetFloat("_HallBottom", transform.TransformPoint(new Vector3(0f, HallFloor, 0f)).y);
            motesMaterial.SetFloat("_HallTop", transform.TransformPoint(new Vector3(0f, HallTop, 0f)).y);
            Shaft();

            glowMaterial.SetFloat("_Light", cloudLight);
            glowMaterial.SetFloat("_Cycle", Mathf.Max(2f, cloudSeconds));
            glowMaterial.SetFloat("_Rise", cloudRise);
        }

        // The moon shaft's two ends and widths from ro-post's mesh (its first plane: corners 0, 1 at the slit, 2, 3 at
        // the foot), looked for until it is there. Without it the motes are only dust.
        void Shaft()
        {
            if (shaft == null && Time.unscaledTime >= lookForShaft)
            {
                lookForShaft = Time.unscaledTime + 1f;
                shaft = Find(transform, "Moon shaft");
            }
            if (shaft == null || !shaft.TryGetComponent<MeshFilter>(out var f) || f.sharedMesh == null || f.sharedMesh.vertexCount < 4)
            {
                motesMaterial.SetVector("_ShaftTop", new Vector4(0f, 1e4f, 0f, 0f));
                motesMaterial.SetVector("_ShaftFoot", new Vector4(0f, 1e4f + 1f, 0f, 0f));
                return;
            }
            var v = f.sharedMesh.vertices;
            Vector3 a = shaft.TransformPoint(v[0]), b = shaft.TransformPoint(v[1]), c = shaft.TransformPoint(v[2]), d = shaft.TransformPoint(v[3]);
            motesMaterial.SetVector("_ShaftTop", (a + b) * .5f);
            motesMaterial.SetVector("_ShaftFoot", (c + d) * .5f);
            motesMaterial.SetVector("_ShaftWidth", new Vector4(Vector3.Distance(a, b), Vector3.Distance(c, d), 0f, 0f));
        }

        static Transform Find(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var found = Find(t.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // The motes: seeds evenly through a unit box (a jittered grid, so no clumps), the shader lays it round the camera.
        Mesh MotesMesh()
        {
            var rng = new System.Random(91);
            float Next() => (float)rng.NextDouble();
            var points = new List<Vector3>();
            var corners = new List<Vector2>();
            var motesData = new List<Vector4>();
            var triangles = new List<int>();
            int side = Mathf.Max(1, Mathf.CeilToInt(Mathf.Pow(moteCount, 1f / 3f)));
            for (int i = 0; i < moteCount; i++)
            {
                int x = i % side, y = i / side % side, z = i / (side * side) % side;
                var seed = new Vector3((x + Next()) / side, (y + Next()) / side, (z + Next()) / side);
                var mote = new Vector4(Mathf.Lerp(.6f, 1.4f, Next()), Next(), Next() < starShare ? 1f : 0f, Next());
                int at = points.Count;
                for (int c = 0; c < 4; c++)
                {
                    points.Add(seed);
                    corners.Add(new Vector2(c & 1, c >> 1));
                    motesData.Add(mote);
                }
                triangles.AddRange(new[] { at, at + 2, at + 1, at + 2, at + 3, at + 1 });
            }
            var mesh = new Mesh { name = "RO hall motes", hideFlags = HideFlags.DontSave };
            if (points.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(points);
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, motesData);
            mesh.SetTriangles(triangles, 0);
            // They are laid round the camera in the shader: never culled while the camera is in the hall.
            mesh.bounds = new Bounds(HallAxis, new Vector3(HallRadius * 2f + 60f, 300f, HallRadius * 2f + 60f));
            return mesh;
        }

        // The clouds: spread round the abyss's well (not under the great globe's root), each its own size, colour and
        // moment in its life, so they rise one after another.
        Mesh GlowMesh()
        {
            var rng = new System.Random(97);
            float Next() => (float)rng.NextDouble();
            var points = new List<Vector3>();
            var corners = new List<Vector2>();
            var clouds = new List<Vector4>();
            var triangles = new List<int>();
            for (int i = 0; i < cloudCount; i++)
            {
                float angle = (i + Next() * .7f) / Mathf.Max(1, cloudCount) * Mathf.PI * 2f;
                float r = Mathf.Lerp(18f, HallRadius - 10f, Mathf.Sqrt(Next()));
                var at = HallAxis + new Vector3(Mathf.Sin(angle) * r, cloudFrom + Next() * 4f, Mathf.Cos(angle) * r);
                var cloud = new Vector4(Mathf.Lerp(cloudSize.x, cloudSize.y, Next()), (i + Next() * .5f) / Mathf.Max(1, cloudCount), Next(), Next());
                int k = points.Count;
                for (int c = 0; c < 4; c++)
                {
                    points.Add(at);
                    corners.Add(new Vector2(c & 1, c >> 1));
                    clouds.Add(cloud);
                }
                triangles.AddRange(new[] { k, k + 2, k + 1, k + 2, k + 3, k + 1 });
            }
            var mesh = new Mesh { name = "RO abyss glow", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, clouds);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(HallAxis + Vector3.up * (cloudFrom + cloudRise * .5f),
                                     new Vector3(HallRadius * 2f + cloudSize.y * 2f, cloudRise + cloudSize.y * 2f + 8f, HallRadius * 2f + cloudSize.y * 2f));
            return mesh;
        }

        void OnDisable()
        {
            if (motes != null) motes.enabled = false;
            if (glow != null) glow.enabled = false;
        }

        void OnDestroy()
        {
            foreach (var m in new[] { motesMaterial, glowMaterial }) if (m != null) Destroy(m);
            foreach (var m in new[] { motesMesh, glowMesh }) if (m != null) Destroy(m);
        }
    }
}
