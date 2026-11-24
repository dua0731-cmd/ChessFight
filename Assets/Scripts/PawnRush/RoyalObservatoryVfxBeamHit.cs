using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // R725 ro-vfx (user 11-20 "온라인데서 경통에서 빛이 다으면 기물이에 타격감 같은게 하게 해줘"): a pawn the focus beam throws
    // is struck where it stands, on every PC. Only the authority (the host) throws, and nothing is sent; so, as with the
    // flare's screen flash, each PC runs FocusBeam's own rule over every pawn it sees - in the spot (its radius, -0.6 ..
    // 2.6 m off the deck), there longer than FocusBeam.Dwell, then left alone for FocusBeam.Rest - and shows the strike
    // the moment it would throw. On the host that is exactly its throw; on a client it is the same rule on the pawns as
    // that PC sees them, so on the spot's very edge it may differ by a hair. What a strike shows:
    //   on the pawn  a white-gold star of light at its body (the sun's corona shader), a real point light flaring on it
    //                and round it for a moment, and hot sparks bursting out of it, most of them down the deck, the way
    //                it is thrown (the hazards' particle shader, drawn from age alone)
    //   to its player  if it is this PC's own pawn (MiniGameBase.LocalDriver), the screen's edges flare white-gold, as the
    //                flare's do (IMGUI, not over the loading screen)
    // Nothing here moves or throws anything. Further than `farDistance` from the camera nothing is looked for or drawn.
    [DisallowMultipleComponent]
    public sealed class RoyalObservatoryVfxBeamHit : MonoBehaviour
    {
        const string BurstShader = "ChessFight/VFX/Burst Particles", FlashPath = "RoyalObservatory/Vfx/RoSunCorona";
        const int Pool = 4;

        public static bool Enabled = true;

        static readonly int AgeId = Shader.PropertyToID("_Age"), LightId = Shader.PropertyToID("_Light"), FloorId = Shader.PropertyToID("_FloorY"),
            SunRadiusId = Shader.PropertyToID("_SunRadius"), SizeId = Shader.PropertyToID("_Size"), CoronaReachId = Shader.PropertyToID("_CoronaReach"),
            FlamesId = Shader.PropertyToID("_Flames"), FlameReachId = Shader.PropertyToID("_FlameReach"), FlashId = Shader.PropertyToID("_Flash"),
            RaysId = Shader.PropertyToID("_Rays"), RayReachId = Shader.PropertyToID("_RayReach"), ShockLightId = Shader.PropertyToID("_ShockLight");

        [Header("On the struck pawn")]
        [Range(1, 80)] public int sparkCount = 40;
        public float sparkLight = 6f;
        public float flashLight = 3f, flashSeconds = .22f;
        public Color lightColor = new Color(1f, .85f, .6f);
        public float lightIntensity = 9f, lightRange = 5f, lightSeconds = .3f;
        [Header("This player's screen, when it is their pawn")]
        public Color edgeFlash = new Color(1f, .9f, .66f);
        [Range(0f, 1f)] public float edgeFlashStrength = .8f;
        public float edgeFlashSeconds = .5f;
        [Header("Cost")]
        public float farDistance = 60f;

        sealed class Strike
        {
            public GameObject root;
            public MeshRenderer sparks, flash;
            public Light light;
            public float age = 99f;
        }

        FocusBeam beam;
        Material burstMaterial, flashMaterial;
        Mesh sparkMesh, quad;
        MaterialPropertyBlock block;
        float sparkLife;
        bool ready;
        readonly List<Strike> strikes = new List<Strike>();
        Dictionary<ImportedObstacleActor, float> held = new Dictionary<ImportedObstacleActor, float>();
        Dictionary<ImportedObstacleActor, float> next = new Dictionary<ImportedObstacleActor, float>();
        readonly Dictionary<ImportedObstacleActor, float> resting = new Dictionary<ImportedObstacleActor, float>();
        readonly Collider[] overlaps = new Collider[64];
        float struckAt = -100f;
        Texture2D edgeWide, edgeTight;

        void Start()
        {
            beam = GetComponent<FocusBeam>();
            block = new MaterialPropertyBlock();
            var burst = ChessFight.VFX.VfxShaderKeep.Find(BurstShader);
            var flashShader = Resources.Load<Shader>(FlashPath);
            if (beam == null || burst == null || flashShader == null || !flashShader.isSupported)
            {
                Debug.LogWarning("[천문대 VFX] 초점 광선 타격 효과: 장치나 셰이더가 없어 끕니다");
                enabled = false;
                return;
            }
            burstMaterial = new Material(burst) { name = "RO beam strike sparks" };
            flashMaterial = new Material(flashShader) { name = "RO beam strike flash" };
            sparkMesh = SparkMesh();
            quad = Quad();
            for (int i = 0; i < Pool; i++) strikes.Add(Make(i));
            ready = true;
        }

        Strike Make(int i)
        {
            var s = new Strike { root = new GameObject("Beam strike " + (i + 1)) };
            s.root.transform.SetParent(transform, false);
            s.sparks = Part(s.root.transform, "Sparks", sparkMesh, burstMaterial);
            s.flash = Part(s.root.transform, "Flash", quad, flashMaterial);
            var lit = new GameObject("Light");
            lit.transform.SetParent(s.root.transform, false);
            s.light = lit.AddComponent<Light>();
            s.light.type = LightType.Point;
            s.light.shadows = LightShadows.None;
            s.light.color = lightColor;
            s.light.range = lightRange;
            s.light.intensity = 0f;
            s.light.enabled = false;
            s.sparks.GetPropertyBlock(block);
            block.SetColor("_ColorStart", new Color(1f, .93f, .75f));
            block.SetColor("_ColorEnd", new Color(1f, .5f, .15f));
            block.SetFloat(LightId, sparkLight);
            block.SetFloat("_AlphaStart", 1f);
            block.SetFloat("_AlphaEnd", 0f);
            block.SetFloat("_FadeIn", .02f);
            block.SetFloat("_SizeStart", .08f);
            block.SetFloat("_SizeEnd", .03f);
            block.SetFloat("_Stretch", .035f);
            block.SetFloat("_Soft", .8f);
            block.SetFloat("_Hard", 0f);
            block.SetFloat("_Additive", 1f);
            block.SetFloat("_DepthFade", 0f);
            block.SetFloat("_Gravity", 9f);
            block.SetFloat("_Drag", 2f);
            block.SetFloat(FloorId, -1f);
            block.SetFloat(AgeId, -1f);
            s.sparks.SetPropertyBlock(block);
            s.root.SetActive(false);
            return s;
        }

        static MeshRenderer Part(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return r;
        }

        static Mesh Quad()
        {
            var mesh = new Mesh { name = "RO beam strike quad", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(new List<Vector3> { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 6f);
            return mesh;
        }

        // VFX_BurstParticles out of the pawn's body every way, most of them down the deck (-z: the way it is thrown) and up.
        Mesh SparkMesh()
        {
            var rng = new System.Random(83);
            float Next() => (float)rng.NextDouble();
            var points = new List<Vector3>();
            var corners = new List<Vector2>();
            var particles = new List<Vector4>();
            var velocities = new List<Vector3>();
            var triangles = new List<int>();
            sparkLife = 0f;
            for (int i = 0; i < sparkCount; i++)
            {
                var dir = new Vector3(Next() * 2f - 1f, Next() * 2f - 1f, Next() * 2f - 1f);
                dir = (dir.normalized + new Vector3(0f, .45f, -.7f)).normalized;
                var start = dir * Mathf.Lerp(.1f, .3f, Next());
                var velocity = dir * Mathf.Lerp(3f, 8.5f, Next());
                float life = Mathf.Lerp(.3f, .7f, Next());
                sparkLife = Mathf.Max(sparkLife, life);
                var particle = new Vector4(life, Mathf.Lerp(.7f, 1.4f, Next()), 0f, 0f);
                int at = points.Count;
                for (int c = 0; c < 4; c++)
                {
                    points.Add(start);
                    corners.Add(new Vector2(c & 1, c >> 1));
                    particles.Add(particle);
                    velocities.Add(velocity);
                }
                triangles.AddRange(new[] { at, at + 2, at + 1, at + 2, at + 3, at + 1 });
            }
            var mesh = new Mesh { name = "RO beam strike sparks", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, particles);
            mesh.SetUVs(2, velocities);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 12f);
            return mesh;
        }

        void Update()
        {
            if (!ready) return;
            var cam = Camera.main;
            bool near = Enabled && cam != null && (cam.transform.position - transform.position).sqrMagnitude < farDistance * farDistance;
            if (!near)
            {
                held.Clear();
                return;
            }
            // FocusBeam's own rule (its FixedUpdate), on every pawn this PC sees.
            var at = beam.SpotAt(ObstacleClock.Now);
            var centre = transform.TransformPoint(at);
            int n = Physics.OverlapSphereNonAlloc(centre + transform.up, beam.Radius + 1f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            next.Clear();
            float now = Time.time;
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(overlaps[i]);
                if (actor == null || next.ContainsKey(actor) || !actor.isActiveAndEnabled) continue;
                // A client's other pawns are puppets that cannot take a hit themselves; only the host asks.
                if (ObstacleClock.Authority && !actor.CanReceiveObstacle) continue;
                var offset = transform.InverseTransformPoint(actor.Position) - at;
                if (offset.y < -.6f || offset.y > 2.6f) continue;
                offset.y = 0f;
                if (offset.magnitude > beam.Radius) continue;
                if (resting.TryGetValue(actor, out float until) && now < until) continue;
                float stayed = (held.TryGetValue(actor, out float before) ? before : 0f) + Time.deltaTime;
                if (stayed >= beam.Dwell)
                {
                    Struck(actor);
                    resting[actor] = now + beam.Rest;
                    stayed = 0f;
                }
                next[actor] = stayed;
            }
            var swap = held;
            held = next;
            next = swap;
            if (resting.Count > 64) resting.Clear();
        }

        void Struck(ImportedObstacleActor actor)
        {
            // the free one, else the oldest
            Strike s = strikes[0];
            foreach (var x in strikes) if (x.age > s.age) s = x;
            var body = actor.Position + transform.up * .3f;
            s.root.transform.SetPositionAndRotation(body, transform.rotation);   // -z down the deck, +y off it
            s.age = 0f;
            s.root.SetActive(true);
            s.sparks.GetPropertyBlock(block);
            block.SetFloat(FloorId, -transform.InverseTransformPoint(body).y);   // the deck, in the strike's frame
            s.sparks.SetPropertyBlock(block);
            bool mine = MiniGameBase.LocalDriver is Component me && me != null && actor.gameObject == me.gameObject;
            if (mine) struckAt = Time.time;
            // R742: the crackle on the thrown pawn (every PC) and, for its own player, the hit
            if (sound == null) sound = RoyalObservatorySound.For(this);
            if (sound != null) sound.BeamStruck(body, mine);
        }

        RoyalObservatorySound sound;

        void LateUpdate()
        {
            if (!ready) return;
            float dt = Time.deltaTime, end = Mathf.Max(sparkLife, Mathf.Max(flashSeconds, lightSeconds));
            foreach (var s in strikes)
            {
                if (!s.root.activeSelf) continue;
                s.age += dt;
                if (s.age > end)
                {
                    s.root.SetActive(false);
                    continue;
                }
                s.sparks.GetPropertyBlock(block);
                block.SetFloat(AgeId, s.age);
                s.sparks.SetPropertyBlock(block);

                bool flashing = s.age < flashSeconds;
                if (s.flash.enabled != flashing) s.flash.enabled = flashing;
                if (flashing)
                {
                    float k = s.age / Mathf.Max(flashSeconds, .01f);
                    s.flash.GetPropertyBlock(block);
                    block.SetFloat(SunRadiusId, .1f);
                    block.SetFloat(SizeId, 2.2f);
                    block.SetFloat(LightId, flashLight * (1f - k) * (1f - k) * Mathf.SmoothStep(0f, 1f, s.age / .03f));
                    block.SetFloat(CoronaReachId, 2.5f);
                    block.SetFloat(FlamesId, 0f);
                    block.SetFloat(FlameReachId, .1f);
                    block.SetFloat(FlashId, 3f);
                    block.SetFloat(ShockLightId, 0f);
                    block.SetFloat(RaysId, 2.6f);
                    block.SetFloat(RayReachId, Mathf.Lerp(5f, 14f, k));
                    block.SetFloat(AgeId, s.age * 3f);
                    s.flash.SetPropertyBlock(block);
                }

                bool lit = s.age < lightSeconds;
                if (s.light.enabled != lit) s.light.enabled = lit;
                if (lit)
                {
                    float k = s.age / Mathf.Max(lightSeconds, .01f);
                    s.light.intensity = lightIntensity * (1f - k) * (1f - k);
                }
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !Enabled || LoadingScreenView.Showing) return;
            float since = Time.time - struckAt, length = Mathf.Max(edgeFlashSeconds, .1f);
            if (since < 0f || since > length) return;
            float k = Mathf.SmoothStep(0f, 1f, since / .04f) * Mathf.Exp(-Mathf.Max(0f, since - .04f) / (length * .35f))
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(length * .7f, length, since))) * edgeFlashStrength;
            var full = new Rect(0, 0, Screen.width, Screen.height);
            var old = GUI.color;
            GUI.color = new Color(1f, .6f, .2f, .6f * k);
            GUI.DrawTexture(full, Edge(ref edgeWide, .45f));
            GUI.color = new Color(edgeFlash.r, edgeFlash.g, edgeFlash.b, .95f * k);
            GUI.DrawTexture(full, Edge(ref edgeTight, .76f));
            GUI.color = old;
        }

        // Clear in the middle, full at the screen's edges (a rounded box): stretched over the screen and tinted.
        static Texture2D Edge(ref Texture2D texture, float inner)
        {
            if (texture != null) return texture;
            const int size = 128;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x + .5f) / size * 2f - 1f), dy = Mathf.Abs((y + .5f) / size * 2f - 1f);
                    float d = Mathf.Pow(Mathf.Pow(dx, 4f) + Mathf.Pow(dy, 4f), .25f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, 1f, d));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        void OnDisable()
        {
            held.Clear();
            resting.Clear();
            foreach (var s in strikes) if (s.root != null) s.root.SetActive(false);
        }

        void OnDestroy()
        {
            if (burstMaterial != null) Destroy(burstMaterial);
            if (flashMaterial != null) Destroy(flashMaterial);
            if (sparkMesh != null) Destroy(sparkMesh);
            if (quad != null) Destroy(quad);
            if (edgeWide != null) Destroy(edgeWide);
            if (edgeTight != null) Destroy(edgeTight);
        }
    }
}
