using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.VFX;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R725 ro-vfx (Royal Observatory R7, device VFX 1 of 7): the meteor shower's effects, beside MeteorShowerDress's stone,
    // tail and engraved ring. MeteorShower decides where and when every meteor falls (a pure function of the section's
    // seed and the shared obstacle clock); this only draws, and works each meteor's moment out from the same clock, so
    // every PC shows the same instant and nothing is sent. Nothing here moves a collider or touches a hit.
    //   warning   an astrolabe face projected on the bridge round the engraved ring (Resources/RoyalObservatory/Vfx/
    //             RoMeteorWarn.shader): the ring's gold bleeding onto the stone, a countdown lighting under its 48 ticks,
    //             a gold fill rising to the knock-down's line, a turning star chart and an eight-point star where it
    //             lands - in place of the shower's flat fill disc; gold to the end, hotter and blinking as the stone
    //             comes down (user "빨갛게 하지마")
    //   falling   thin streaks of air round the stone, riding down with it, and sparks it sheds along the way that hang
    //             where they were shed and fall away
    //   landing   a gold shockwave out to the shove's reach (a band on the bridge and a low wall of light), star dust
    //             swept along the bridge and a spray of gold sparks; the shower's own flash stays its own
    // The layers are the grand stair hall's hazard shaders (VFX_ShockDecal, VFX_ShockWallAdd, VFX_FallStreakAdd,
    // VFX_BurstParticles), drawn from age alone. Meteors further than `farDistance` from the camera are not drawn.
    [DisallowMultipleComponent]
    public sealed class RoyalObservatoryVfxMeteor : MonoBehaviour
    {
        const string DecalShader = "ChessFight/VFX/Shock Decal", WallShader = "ChessFight/VFX/Shock Wall Additive",
            StreakShader = "ChessFight/VFX/Fall Streak Additive", BurstShader = "ChessFight/VFX/Burst Particles";

        // R725 (user "유성 예고 그래픽도 좋게 해줘"): the warning's own decal, kept in builds by its Resources folder.
        const string WarnShaderPath = "RoyalObservatory/Vfx/RoMeteorWarn";

        static readonly int RadiusId = Shader.PropertyToID("_Radius"), InnerId = Shader.PropertyToID("_Inner"),
            BoxId = Shader.PropertyToID("_Box"), FillId = Shader.PropertyToID("_Fill"), DangerId = Shader.PropertyToID("_Danger"),
            TurnId = Shader.PropertyToID("_Turn"), AgeId = Shader.PropertyToID("_Age"),
            LengthId = Shader.PropertyToID("_Length"), LightId = Shader.PropertyToID("_Light");

        // Off: the meteors as modelling left them (a switch for comparing, as the dressing has).
        public static bool Enabled = true;

        [Header("Warning: the astrolabe face round the ring (RoMeteorWarn)")]
        [ColorUsage(false, true)] public Color glowColor = new Color(1f, .62f, .22f);
        [ColorUsage(false, true)] public Color glowHot = new Color(1f, .86f, .56f);
        [Range(0f, 1f), Tooltip("Darkness laid under it, so the gold reads on the floor.")]
        public float warnShade = .3f;
        [Tooltip("Times its light once the stone starts down (still gold, blinking).")]
        public float warnDangerBoost = 1.6f;

        [Header("Falling: air streaks and shed sparks")]
        [Range(1, 24)] public int streakCount = 9;
        [Tooltip("Metres long once the stone is at speed.")]
        public float streakLength = 4.2f;
        public float streakLight = 2.2f;
        [Range(1, 80)] public int trailCount = 34;
        public float trailLight = 5f;

        [Header("Landing: shockwave, dust and sparks")]
        [Tooltip("Seconds the shockwave takes out to the shove's reach.")]
        public float shockSeconds = .38f;
        public float shockWallLight = 1.7f;
        [Range(1, 40)] public int dustCount = 14;
        [Range(1, 40)] public int sparkCount = 18;
        public float sparkLight = 5f;

        [Header("Cost")]
        [Tooltip("Meteors further than this from the camera (m) are not drawn.")]
        public float farDistance = 60f;

        sealed class View
        {
            public GameObject root;
            public Transform spin, fall;
            public MeshRenderer glow, streaks, trail;
            public VfxShockDecal shock;
            public VfxShockWall wall;
            public VfxParticleBurst dust, sparks;
        }

        MeteorShower shower;
        Material glowMaterial, shockMaterial, wallMaterial, streakMaterial, burstMaterial;
        Mesh streakMesh, trailMesh;
        MaterialPropertyBlock block;
        readonly List<View> views = new List<View>();
        readonly HashSet<Transform> flatHidden = new HashSet<Transform>();
        float trailLife;
        bool ready;

        // Seconds a meteor's effects outlast its landing (the dust is the longest).
        float Linger => Mathf.Max(shockSeconds, 1.3f);

        // How many meteor views are drawing now (the check batch reads it).
        public int Drawing { get; private set; }

        void Start()
        {
            shower = GetComponent<MeteorShower>();
            block = new MaterialPropertyBlock();
            var warnShader = Resources.Load<Shader>(WarnShaderPath);
            glowMaterial = warnShader != null && warnShader.isSupported ? new Material(warnShader) { name = "RO meteor warning" } : null;
            shockMaterial = Make(DecalShader, "RO meteor shockwave");
            wallMaterial = Make(WallShader, "RO meteor shock wall");
            streakMaterial = Make(StreakShader, "RO meteor air streaks");
            burstMaterial = Make(BurstShader, "RO meteor particles");
            ready = shower != null && shockMaterial != null && wallMaterial != null
                    && streakMaterial != null && burstMaterial != null;
            // Without its shader the warning stays the shower's own flat fill; the rest still draws.
            if (glowMaterial == null) Debug.LogWarning("[천문대 VFX] 유성 예고 셰이더가 없습니다(Resources/" + WarnShaderPath + ")");
            if (!ready)
            {
                Debug.LogWarning("[천문대 VFX] 유성우 효과 셰이더를 찾지 못해 효과를 끕니다");
                enabled = false;
                return;
            }
            Look();
            streakMesh = StreakMesh();
            trailMesh = TrailMesh();
        }

        static Material Make(string shaderName, string name)
        {
            var shader = VfxShaderKeep.Find(shaderName);
            return shader != null ? new Material(shader) { name = name } : null;
        }

        // The materials' look: the shower's gold on the HDR ladder of Docs/VFX §6 (glow ~1.5, front ~3, sparks ~5).
        void Look()
        {
            if (glowMaterial != null)
            {
                glowMaterial.SetColor("_Color", glowColor);
                glowMaterial.SetColor("_HotColor", glowHot);
                glowMaterial.SetFloat("_Shade", warnShade);
                glowMaterial.SetFloat("_DangerBoost", warnDangerBoost);
            }

            shockMaterial.SetColor("_Color", new Color(1f, .6f, .2f));
            shockMaterial.SetColor("_HotColor", new Color(1f, .9f, .7f));
            shockMaterial.SetFloat("_FrontWidth", .14f);
            shockMaterial.SetFloat("_FrontLight", 3f);
            shockMaterial.SetFloat("_Trail", .8f);
            shockMaterial.SetFloat("_TrailLight", 1.1f);
            shockMaterial.SetFloat("_Flicker", .3f);
            shockMaterial.SetFloat("_LimitLight", .35f);

            wallMaterial.SetColor("_Color", new Color(1f, .6f, .22f));
            wallMaterial.SetColor("_CoreColor", new Color(1f, .9f, .7f));

            streakMaterial.SetColor("_Color", new Color(1f, .66f, .3f));
            streakMaterial.SetColor("_CoreColor", new Color(1f, .93f, .8f));
            streakMaterial.SetFloat("_Width", .045f);
            streakMaterial.SetFloat("_Falloff", 1.4f);
        }

        void LateUpdate()
        {
            if (!ready) return;
            double start = shower.StartClock;
            if (!Enabled || double.IsNaN(start) || shower.LineCount == 0)
            {
                Park(0);
                return;
            }
            if (glowMaterial != null) HideFlatFills();
            double now = ObstacleClock.Now;
            float warning = shower.Warning, interval = shower.Interval;
            var eye = Camera.main != null ? Camera.main.transform.position : transform.position;
            long current = (long)Math.Floor((now - start) / interval);
            int used = 0;
            int back = Mathf.CeilToInt(Linger / interval) + 2;
            for (long n = Math.Max(0L, current - back); n <= current + 2; n++)
            {
                var m = shower.At(n);
                float age = (float)(now - (m.Time - warning));   // since its warning began
                if (age < 0f || age > warning + Linger) continue;
                var at = shower.Point(m);
                if ((transform.TransformPoint(at) - eye).sqrMagnitude > farDistance * farDistance) continue;
                if (used == views.Count) views.Add(MakeView());
                Draw(views[used++], m, at, age);
            }
            Park(used);
        }

        void Park(int from)
        {
            for (int i = from; i < views.Count; i++)
                if (views[i].root.activeSelf) views[i].root.SetActive(false);
            Drawing = from;
        }

        void Draw(View v, MeteorShower.Meteor m, Vector3 at, float age)
        {
            float warning = shower.Warning, streak = shower.Streak, fallStart = warning - streak;
            float outer = shower.OuterRadius;
            v.root.transform.localPosition = at;
            v.root.transform.localRotation = Facing(m);
            // Each meteor's own turn of the round layers (the golden angle), the same on every PC.
            v.spin.localRotation = Quaternion.Euler(0f, (m.Serial * 137.508f) % 360f, 0f);
            if (!v.root.activeSelf) v.root.SetActive(true);

            // Warning: the astrolabe face, its countdown and fill following the warning, hotter once the stone comes.
            bool warned = v.glow != null && age < warning;
            if (v.glow != null) v.glow.enabled = warned;
            if (warned)
            {
                float danger = Mathf.Clamp01((age - fallStart) / .08f);
                Warn(v.glow, outer, shower.InnerRadius, age / warning, danger, Mathf.Clamp01(age / .15f),
                     (m.Serial * 2.39996f) % (2f * Mathf.PI), age);
            }

            // Falling: the streaks ride the stone down (MeteorShowerDress: the stone at the rod's lower end).
            float u = (age - fallStart) / Mathf.Max(streak, .01f);
            bool falling = u >= 0f && age < warning + .05f;
            v.streaks.enabled = falling;
            if (falling)
            {
                float h = MeteorShower.StreakFrom * (1f - Mathf.Clamp01(u));
                v.fall.localPosition = Vector3.up * h;
                float fade = age < warning ? Mathf.Clamp01(u / .15f) : 1f - (age - warning) / .05f;
                v.streaks.GetPropertyBlock(block);
                block.SetFloat(LengthId, streakLength * Mathf.Clamp01(.35f + u * 2f));
                block.SetFloat(LightId, streakLight * fade);
                v.streaks.SetPropertyBlock(block);
            }
            float trailAge = age - fallStart;
            v.trail.enabled = trailAge >= 0f && trailAge < streak + trailLife;
            if (v.trail.enabled)
            {
                v.trail.GetPropertyBlock(block);
                block.SetFloat(AgeId, trailAge);
                v.trail.SetPropertyBlock(block);
            }

            // Landing: the shockwave, the dust and the sparks seek the meteor's age (their timing set when made).
            v.shock.radius = outer;
            v.wall.radius = outer;
            v.wall.brightness = shockWallLight;
            v.sparks.brightness = sparkLight;
            v.shock.Seek(age);
            v.wall.Seek(age);
            v.dust.Seek(age);
            v.sparks.Seek(age);
        }

        // The view's frame: flat on the meteor's line, as MeteorShower lays its own.
        Quaternion Facing(MeteorShower.Meteor m)
        {
            var line = shower.LineAt(m.Line);
            var along = (line.to - line.from).normalized;
            var side = Vector3.Cross(Vector3.up, along);
            return side.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(along, Vector3.Cross(along, side.normalized)) : Quaternion.identity;
        }

        // The warning decal (RoMeteorWarn) on the bridge under the view: its box past the ring's bleed, its countdown
        // starting at the line's forward end (the view's +z).
        void Warn(MeshRenderer r, float radius, float inner, float fill, float danger, float light, float turn, float age)
        {
            const float height = 1.4f, sink = .6f;
            float side = 2f * (radius + .8f);
            r.transform.localScale = new Vector3(side, height, side);
            r.transform.localPosition = new Vector3(0f, height * .5f - sink, 0f);
            r.GetPropertyBlock(block);
            block.SetFloat(RadiusId, radius);
            block.SetFloat(InnerId, inner);
            block.SetFloat(BoxId, side);
            block.SetFloat(FillId, Mathf.Clamp01(fill));
            block.SetFloat(DangerId, danger);
            block.SetFloat(LightId, light);
            block.SetFloat(TurnId, turn);
            block.SetFloat(AgeId, age);
            r.SetPropertyBlock(block);
        }

        // The shower's own flat gold fill disc (MeteorShower still grows it; MeteorShowerDress reads its width) stops
        // drawing where the warning decal draws instead.
        void HideFlatFills()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var view = transform.GetChild(i);
                if (view.name != "Meteor" || !flatHidden.Add(view)) continue;
                var fill = view.Find("Warning fill (knock-down)");
                if (fill != null && fill.TryGetComponent<Renderer>(out var r)) r.enabled = false;
            }
        }

        // ------------------------------------------------------------------ one meteor's layers

        View MakeView()
        {
            var root = new GameObject("Meteor VFX");
            root.transform.SetParent(transform, false);
            var v = new View { root = root };
            v.spin = new GameObject("turn").transform;
            v.spin.SetParent(root.transform, false);
            v.fall = new GameObject("with the stone").transform;
            v.fall.SetParent(root.transform, false);

            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            v.glow = glowMaterial != null ? Part("Warning (astrolabe)", root.transform, cube, glowMaterial) : null;
            v.streaks = Part("Air streaks", v.fall, streakMesh, streakMaterial);
            v.trail = Part("Shed sparks", root.transform, trailMesh, burstMaterial);
            TrailLook(v.trail);

            float warning = shower.Warning, streak = shower.Streak;
            var drop = new VfxDrop { lead = warning, fall = Mathf.Max(streak, .05f), height = MeteorShower.StreakFrom, hold = shockSeconds };
            v.shock = Part("Shockwave", v.spin, cube, shockMaterial).gameObject.AddComponent<VfxShockDecal>();
            v.shock.enabled = false;   // sought from the meteor's age, not its own clock
            v.shock.drop = drop;
            v.shock.height = 1.4f;
            v.shock.sink = .6f;
            v.shock.fadeOut = .4f;
            v.shock.endLight = .3f;
            v.wall = Part("Shock wall", v.spin, null, wallMaterial).gameObject.AddComponent<VfxShockWall>();
            v.wall.enabled = false;
            v.wall.drop = drop;
            v.wall.heightStart = .75f;
            v.wall.heightEnd = .25f;
            v.wall.fadeOut = .45f;
            v.wall.segments = 56;
            v.dust = Burst("Star dust", v.spin, warning);
            Dust(v.dust);
            v.sparks = Burst("Gold sparks", v.spin, warning);
            Sparks(v.sparks);
            return v;
        }

        static MeshRenderer Part(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
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

        VfxParticleBurst Burst(string name, Transform parent, float at)
        {
            var b = Part(name, parent, null, burstMaterial).gameObject.AddComponent<VfxParticleBurst>();
            b.enabled = false;   // sought from the meteor's age
            b.burstAt = at;
            return b;
        }

        // Star dust swept out along the bridge from where it lands: warm gold turning dusk violet, gone in about a second.
        void Dust(VfxParticleBurst b)
        {
            b.count = dustCount;
            b.seed = 41;
            b.ringRadius = new Vector2(.35f, .6f);
            b.startHeight = new Vector2(.05f, .2f);
            b.outSpeed = new Vector2(2.6f, 5f);
            b.upSpeed = new Vector2(.2f, .9f);
            b.life = new Vector2(.7f, 1.15f);
            b.sizeScale = new Vector2(.8f, 1.3f);
            b.delay = .03f;
            b.spin = 40f;
            b.gravity = .5f;
            b.drag = 3.4f;
            b.floor = true;
            b.colorStart = new Color(.86f, .74f, .52f);
            b.colorEnd = new Color(.5f, .48f, .62f);
            b.brightness = 1f;
            b.alphaStart = .42f;
            b.alphaEnd = 0f;
            b.fadeIn = .08f;
            b.sizeStart = .35f;
            b.sizeEnd = 1.15f;
            b.stretch = 0f;
            b.soft = 1f;
            b.hard = 0f;
            b.additive = .25f;
            b.depthFade = .25f;
        }

        // A spray of gold sparks out and up, quick.
        void Sparks(VfxParticleBurst b)
        {
            b.count = sparkCount;
            b.seed = 43;
            b.ringRadius = new Vector2(.15f, .45f);
            b.startHeight = new Vector2(.1f, .35f);
            b.outSpeed = new Vector2(3.5f, 7.5f);
            b.upSpeed = new Vector2(2f, 5.5f);
            b.life = new Vector2(.3f, .55f);
            b.sizeScale = new Vector2(.7f, 1.2f);
            b.delay = 0f;
            b.spin = 0f;
            b.gravity = 9f;
            b.drag = 1.8f;
            b.floor = true;
            b.colorStart = new Color(1f, .88f, .55f);
            b.colorEnd = new Color(1f, .5f, .14f);
            b.brightness = sparkLight;
            b.alphaStart = 1f;
            b.alphaEnd = 0f;
            b.fadeIn = .01f;
            b.sizeStart = .05f;
            b.sizeEnd = .025f;
            b.stretch = .04f;
            b.soft = .8f;
            b.hard = 0f;
            b.additive = 1f;
            b.depthFade = 0f;
        }

        // The shed sparks' look on their own renderer (VFX_BurstParticles, set as VfxParticleBurst sets it).
        void TrailLook(MeshRenderer r)
        {
            r.GetPropertyBlock(block);
            block.SetColor("_ColorStart", new Color(1f, .92f, .7f));
            block.SetColor("_ColorEnd", new Color(1f, .45f, .12f));
            block.SetFloat(LightId, trailLight);
            block.SetFloat("_AlphaStart", 1f);
            block.SetFloat("_AlphaEnd", 0f);
            block.SetFloat("_FadeIn", .02f);
            block.SetFloat("_SizeStart", .07f);
            block.SetFloat("_SizeEnd", .02f);
            block.SetFloat("_Stretch", .035f);
            block.SetFloat("_Soft", .8f);
            block.SetFloat("_Hard", 0f);
            block.SetFloat("_Additive", 1f);
            block.SetFloat("_DepthFade", 0f);
            block.SetFloat("_Gravity", 5f);
            block.SetFloat("_Drag", 2.5f);
            block.SetFloat("_FloorY", 0f);
            block.SetFloat(AgeId, -1f);
            r.SetPropertyBlock(block);
        }

        // ------------------------------------------------------------------ meshes

        // VFX_FallStreakAdd: a quad a streak, its corners at the streak's foot round the stone (the shader stands it up).
        Mesh StreakMesh()
        {
            var rng = new System.Random(17);
            float Next() => (float)rng.NextDouble();
            var points = new List<Vector3>();
            var uvs = new List<Vector2>();
            var shares = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i < streakCount; i++)
            {
                float angle = (i + Next() * .7f) / streakCount * Mathf.PI * 2f, radius = Mathf.Lerp(.28f, .7f, Next());
                var foot = new Vector3(Mathf.Sin(angle) * radius, Mathf.Lerp(.1f, 1.1f, Next()), Mathf.Cos(angle) * radius);
                var share = new Vector2(Mathf.Lerp(.5f, 1f, Next()), Mathf.Lerp(.45f, 1f, Next()));
                int at = points.Count;
                for (int c = 0; c < 4; c++)
                {
                    points.Add(foot);
                    uvs.Add(new Vector2(c & 1, c >> 1));
                    shares.Add(share);
                }
                triangles.AddRange(new[] { at, at + 2, at + 1, at + 2, at + 3, at + 1 });
            }
            var mesh = new Mesh { name = "RO meteor air streaks", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, shares);
            mesh.SetTriangles(triangles, 0);
            float top = 1.1f + streakLength + .5f;
            mesh.bounds = new Bounds(new Vector3(0f, top * .5f, 0f), new Vector3(2f, top, 2f));
            return mesh;
        }

        // VFX_BurstParticles laid out along the fall: particle i is shed as the stone passes its height, so its delay is
        // the moment the stone is there; it starts beside the path (its place in the world), drifts out and falls away.
        Mesh TrailMesh()
        {
            var rng = new System.Random(29);
            float Next() => (float)rng.NextDouble();
            float streak = Mathf.Max(shower.Streak, .05f), from = MeteorShower.StreakFrom;
            var points = new List<Vector3>();
            var corners = new List<Vector2>();
            var particles = new List<Vector4>();
            var velocities = new List<Vector3>();
            var triangles = new List<int>();
            trailLife = 0f;
            for (int i = 0; i < trailCount; i++)
            {
                float share = (i + Next()) / trailCount;   // of the fall, when it is shed
                float angle = Next() * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                var start = outward * Mathf.Lerp(.05f, .35f, Next()) + Vector3.up * (from * (1f - share) + Next() * .5f);
                var velocity = outward * Mathf.Lerp(.8f, 3.2f, Next()) + Vector3.up * Mathf.Lerp(-.5f, 1.4f, Next());
                float life = Mathf.Lerp(.3f, .65f, Next());
                trailLife = Mathf.Max(trailLife, life);
                var particle = new Vector4(life, Mathf.Lerp(.7f, 1.3f, Next()), share * streak, 0f);
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
            var mesh = new Mesh { name = "RO meteor shed sparks", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(points);
            mesh.SetUVs(0, corners);
            mesh.SetUVs(1, particles);
            mesh.SetUVs(2, velocities);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(new Vector3(0f, from * .5f, 0f), new Vector3(6f, from + 3f, 6f));
            return mesh;
        }

        void OnDisable() => Park(0);

        void OnDestroy()
        {
            foreach (var m in new[] { glowMaterial, shockMaterial, wallMaterial, streakMaterial, burstMaterial })
                if (m != null) Destroy(m);
            if (streakMesh != null) Destroy(streakMesh);
            if (trailMesh != null) Destroy(trailMesh);
        }
    }
}
