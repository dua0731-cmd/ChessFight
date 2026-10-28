using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ChessFight.VFX.Editor
{
    /// <summary>
    /// The knight's F and E effects, made the new way, one layer a prefab (R179, 10-28; DECISIONS FX7), for the sword
    /// fight's fork dive (the layer plan F1..F6 and E1..E5, the timing and the reference prompts are in
    /// Docs/VFX/VFX_Progress.md), in the knight's deep warm bronze (#7A3E10 with a warm amber core #FFD9A8 since R213, deeper than R177's #A8703D: 사용자 "나이트 vfx 색을 더 진하게" · "B로 해줘"; first a deeper shade of the rook's beige: 사용자
    /// "룩 고유 색보다 진한 색으로"), a square with an enemy on it red (#C24A41, the enemy colour of
    /// King of the Hill: 사용자 "적 칸은 빨강으로"). Menu ChessFight/VFX/Knight/Build writes the generated mesh and materials,
    /// the prefabs of Prefabs/Knight built so far and their names in Resources/VfxSets/Knight; play them in the VFX
    /// workbench (piece 5) or take frames of them with VfxCapture. Values tuned in a prefab's parts are kept when it is
    /// built again; a material is only made when missing, so its tuned colours stay too. Built so far: F5, the fork where
    /// it lands (F_Fork); E3, the crescent its hind hooves draw over the somersault (E_Flip, R182); E4, the star where its
    /// hind hooves strike whoever its buck takes (E_Kick, R189); E2, the flash where its hooves kick off the edge (E_Spring,
    /// R192); E1, the light drawn in to its hooves as it crouches before it (E_Coil, R195). ChessFight/VFX/Knight/Build E1
    /// (coil only), Build E2 (spring only), Build E3 (flip only) and Build E4 (kick only) each build that layer alone,
    /// leaving the other prefabs as they are.
    /// </summary>
    public static class KnightVfxBuilder
    {
        const string SetPath = "Assets/Resources/VfxSets/Knight.asset";
        const string ForkPrefab = VfxAssets.Root + "/Prefabs/Knight/05_Knight_F_Fork.prefab";
        const string FlipPrefab = VfxAssets.Root + "/Prefabs/Knight/12_Knight_E_Flip.prefab";
        const string KickPrefab = VfxAssets.Root + "/Prefabs/Knight/13_Knight_E_Kick.prefab";
        const string SpringPrefab = VfxAssets.Root + "/Prefabs/Knight/11_Knight_E_Spring.prefab";
        const string CoilPrefab = VfxAssets.Root + "/Prefabs/Knight/10_Knight_E_Coil.prefab";
        const string GridMaterial = VfxAssets.Root + "/Materials/M_Knight_ForkGrid.mat";
        const string ShaftMaterial = VfxAssets.Root + "/Materials/M_Knight_ForkShaft.mat";
        const string ArcMaterial = VfxAssets.Root + "/Materials/M_Knight_FlipArc.mat";
        const string StarMaterial = VfxAssets.Root + "/Materials/M_Knight_KickStar.mat";
        const string StreakMaterial = VfxAssets.Root + "/Materials/M_Knight_KickStreak.mat";
        const string SpringStarMaterial = VfxAssets.Root + "/Materials/M_Knight_SpringStar.mat";
        const string SpringStreakMaterial = VfxAssets.Root + "/Materials/M_Knight_SpringStreak.mat";
        const string CoilThreadMaterial = VfxAssets.Root + "/Materials/M_Knight_CoilThread.mat";
        const string CoilGlowMaterial = VfxAssets.Root + "/Materials/M_Knight_CoilGlow.mat";
        const string CoilCoreMaterial = VfxAssets.Root + "/Materials/M_Knight_CoilCore.mat";
        const string ForkMesh = VfxAssets.Root + "/Meshes/SM_Knight_Fork.asset";
        const string StarMesh = VfxAssets.Root + "/Meshes/SM_Knight_Star.asset";
        const string ForkPart = "F5 Fork", FlipPart = "E3 Arc", StarPart = "E4 Star", StreakPart = "E4 Streaks";
        const string SpringStarPart = "E2 Star", SpringStreakPart = "E2 Streaks";
        const string CoilThreadPart = "E1 Threads", CoilGlowPart = "E1 Glow", CoilCorePart = "E1 Core";
        /// <summary>Degrees E2's streaks are tipped down from its +Z (out over the drop). The game tips the effect back up
        /// by as much where it springs from further in than the lip, so they skim back over the board.</summary>
        const float SpringTip = 40f;
        /// <summary>How far the floor reaches each way from the plate's middle, in squares: past the far squares' edges
        /// (2.31 at the default size) and their glow.</summary>
        const float Reach = 2.75f;
        /// <summary>The floor a hair over the board, in squares (about a centimetre and a half).</summary>
        const float Lift = .012f;
        static readonly Color Bronze = new Color(.478f, .243f, .063f), WarmWhite = new Color(1f, .851f, .659f);
        static readonly Color FoeRed = new Color(.761f, .29f, .255f), FoeCore = new Color(1f, .851f, .812f);
        /// <summary>The eight knight moves (across to its right, along its way), in the order of SwordFightSkills.KnightMoves.</summary>
        static readonly Vector2Int[] Moves =
        {
            new Vector2Int(1, 2), new Vector2Int(2, 1), new Vector2Int(2, -1), new Vector2Int(1, -2),
            new Vector2Int(-1, -2), new Vector2Int(-2, -1), new Vector2Int(-2, 1), new Vector2Int(-1, 2),
        };

        [MenuItem("ChessFight/VFX/Knight/Build")]
        public static void Build()
        {
            // In Play mode the layers' Awake runs as they are made and what it does is saved into the prefab (VFX1 found a
            // flash quad saved lit that way): build in edit mode only.
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the knight effects are built in edit mode.");
                return;
            }
            var fork = BuildFork();
            var coil = BuildCoil();
            var spring = BuildSpring();
            var flip = BuildFlip();
            var kick = BuildKick();
            AssetDatabase.SaveAssets();
            Selection.activeObject = kick;
            Debug.Log("[VFX] Knight built: F5 the fork where it lands (F_Fork), E1 the light drawn in as it crouches (E_Coil), "
                + "E2 the flash where it springs (E_Spring), "
                + "E3 the crescent over the somersault (E_Flip), E4 the star where its hooves strike (E_Kick). Workbench: piece 5, "
                + fork.name + ", " + coil.name + ", " + spring.name + ", " + flip.name + ", " + kick.name);
        }

        /// <summary>The menu is greyed out in Play mode.</summary>
        [MenuItem("ChessFight/VFX/Knight/Build", true)]
        static bool CanBuild() => !EditorApplication.isPlaying;

        /// <summary>E1 alone (R195): the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/VFX/Knight/Build E1 (coil only)")]
        public static void BuildE1()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the knight effects are built in edit mode.");
                return;
            }
            var coil = BuildCoil();
            AssetDatabase.SaveAssets();
            Selection.activeObject = coil;
            Debug.Log("[VFX] Knight E1 built: the light drawn in as it crouches (E_Coil). Workbench: piece 5, " + coil.name);
        }

        [MenuItem("ChessFight/VFX/Knight/Build E1 (coil only)", true)]
        static bool CanBuildE1() => !EditorApplication.isPlaying;

        /// <summary>E2 alone (R192): the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/VFX/Knight/Build E2 (spring only)")]
        public static void BuildE2()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the knight effects are built in edit mode.");
                return;
            }
            var spring = BuildSpring();
            AssetDatabase.SaveAssets();
            Selection.activeObject = spring;
            Debug.Log("[VFX] Knight E2 built: the flash where it springs (E_Spring). Workbench: piece 5, " + spring.name);
        }

        [MenuItem("ChessFight/VFX/Knight/Build E2 (spring only)", true)]
        static bool CanBuildE2() => !EditorApplication.isPlaying;

        /// <summary>E3 alone (R182): the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/VFX/Knight/Build E3 (flip only)")]
        public static void BuildE3()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the knight effects are built in edit mode.");
                return;
            }
            var flip = BuildFlip();
            AssetDatabase.SaveAssets();
            Selection.activeObject = flip;
            Debug.Log("[VFX] Knight E3 built: the crescent over the somersault (E_Flip). Workbench: piece 5, " + flip.name);
        }

        [MenuItem("ChessFight/VFX/Knight/Build E3 (flip only)", true)]
        static bool CanBuildE3() => !EditorApplication.isPlaying;

        /// <summary>E4 alone (R189): the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/VFX/Knight/Build E4 (kick only)")]
        public static void BuildE4()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the knight effects are built in edit mode.");
                return;
            }
            var kick = BuildKick();
            AssetDatabase.SaveAssets();
            Selection.activeObject = kick;
            Debug.Log("[VFX] Knight E4 built: the star where its hooves strike (E_Kick). Workbench: piece 5, " + kick.name);
        }

        [MenuItem("ChessFight/VFX/Knight/Build E4 (kick only)", true)]
        static bool CanBuildE4() => !EditorApplication.isPlaying;

        // ---------------------------------------------------------------- F5: the fork

        /// <summary>
        /// F5, the face of the skill (0.8 s from the landing): the plate under it flashes, four lines run out two squares,
        /// a T on to the eight squares it forks, each square flashes with a shaft of light rising off it (an enemy's square
        /// red), and it dies away, the shafts first (<see cref="VfxForkGrid"/>). The root is on the floor in the middle of
        /// the square it lands on, its +Z the knight's way.
        /// </summary>
        static GameObject BuildFork()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(ForkPrefab);
            var root = new GameObject("05_Knight_F_Fork");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = .9f;
            // The plate's flash, the lines running, the T at the squares, the squares lit and the shafts risen, dying away.
            cue.captureTimes = new[] { 0f, .04f, .1f, .16f, .3f, .55f };

            var go = new GameObject(ForkPart);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = VfxAssets.SaveMesh(ForkMesh, ForkMeshOf());
            var view = go.AddComponent<MeshRenderer>();
            view.sharedMaterials = new[]
            {
                Material(GridMaterial, "ChessFight/VFX/Fork Grid Additive",
                    ("_Color", Bronze), ("_CoreColor", WarmWhite), ("_FoeColor", FoeRed), ("_FoeCoreColor", FoeCore)),
                Material(ShaftMaterial, "ChessFight/VFX/Fork Shaft Additive", ("_Color", Bronze), ("_CoreColor", WarmWhite)),
            };
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            view.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            var play = go.AddComponent<VfxForkGrid>();
            // One enemy, ahead to the right, so the workbench and the captures show a red square (the game sets its own).
            play.squareFoe[0] = true;
            Keep(old, ForkPart, play);
            go.transform.localScale = Vector3.one * play.cell;

            var prefab = VfxAssets.Prefab(ForkPrefab, root);
            Register("F_Fork", prefab);
            return prefab;
        }

        /// <summary>
        /// The fork's mesh, one unit a square: the floor (submesh 0, a quad <see cref="Reach"/> squares each way, a hair up)
        /// and the shafts (submesh 1, a quad a square, its four corners at the square's middle on the floor, the corner in
        /// uv and the square's index in uv2.x: the shaft shader stands it up). Its bounds hold the shafts' height.
        /// </summary>
        static Mesh ForkMeshOf()
        {
            var vertices = new List<Vector3>
            {
                new Vector3(-Reach, Lift, -Reach), new Vector3(Reach, Lift, -Reach), new Vector3(-Reach, Lift, Reach), new Vector3(Reach, Lift, Reach),
            };
            var uv = new List<Vector2> { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            var square = new List<Vector2> { new Vector2(-1f, 0f), new Vector2(-1f, 0f), new Vector2(-1f, 0f), new Vector2(-1f, 0f) };
            var floor = new List<int> { 0, 2, 1, 2, 3, 1 };
            var shafts = new List<int>();
            for (int i = 0; i < Moves.Length; i++)
            {
                int v = vertices.Count;
                for (int k = 0; k < 4; k++)
                {
                    vertices.Add(new Vector3(Moves[i].x, 0f, Moves[i].y));
                    uv.Add(new Vector2(k % 2, k / 2));
                    square.Add(new Vector2(i, 0f));
                }
                shafts.AddRange(new[] { v, v + 2, v + 1, v + 2, v + 3, v + 1 });
            }
            var mesh = new Mesh { name = "SM_Knight_Fork" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, square);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(floor, 0);
            mesh.SetTriangles(shafts, 1);
            // The shader stands the shafts up a metre or so over their feet, so the bounds go up too.
            mesh.bounds = new Bounds(new Vector3(0f, 1.25f, 0f), new Vector3(Reach * 2f, 2.5f, Reach * 2f));
            return mesh;
        }

        // ---------------------------------------------------------------- E3: the somersault

        /// <summary>
        /// E3, the face of the reversal (the 0.8 s flight from the spring off the edge): the long crescent its hind hooves
        /// draw over the somersault, out over the cliff, over the enemy's head and down behind it, a warm white core in its
        /// bronze, brightest at the hooves and dying away from its tail (<see cref="VfxPathRibbon"/>), so it never closes
        /// into the reference's ring. The root is on the floor where it springs, its +Z the knight's way. The game traces
        /// the real hooves into it; the prefab keeps a typical way (<see cref="TypicalFlip"/>) for the workbench and the
        /// captures, made again on every build.
        /// </summary>
        static GameObject BuildFlip()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(FlipPrefab);
            var root = new GameObject("12_Knight_E_Flip");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 1.4f;
            // Into the turn, over the enemy's head, over the top, coming down, at the landing, its tail dying away.
            cue.captureTimes = new[] { .42f, .5f, .58f, .66f, .8f, .95f, 1.1f };

            var go = new GameObject(FlipPart);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>();
            var view = go.AddComponent<MeshRenderer>();
            // Its core and head light dimmer and narrower than the shader's and its bronze full to the edge: stacked as the
            // shader has them, ACES washed it white (R182, its first capture's fix 1).
            view.sharedMaterial = Material(ArcMaterial, "ChessFight/VFX/Path Ribbon Additive", new[] { ("_Color", Bronze), ("_CoreColor", WarmWhite) },
                ("_CoreBoost", 1.2f), ("_CoreWidth", .15f), ("_GlowFalloff", .5f), ("_HeadGlow", 1f));
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            view.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            var arc = go.AddComponent<VfxPathRibbon>();
            // Wider than a ribbon's own and wide along most of the arc, as the reference's band (fix 2).
            arc.width = .18f;
            arc.taper = .4f;
            Keep(old, FlipPart, arc);
            // The way is not a tuned value: the builder's typical one every time.
            TypicalFlip(arc.points, arc.times);

            var prefab = VfxAssets.Prefab(FlipPrefab, root);
            Register("E_Flip", prefab);
            return prefab;
        }

        /// <summary>
        /// A typical way of its hind hooves, in the prefab's space, from the turn over (SwordFightSkills.KnightTuckAt of the
        /// flight) to the landing, each point with the second it is drawn at: its hips along the game's FlipPath for a
        /// knight <c>Edge</c> m in from the edge going over an enemy <c>Enemy</c> m ahead and landing <c>Behind</c> m past
        /// it, the hooves <c>Hooves</c> m under its hips turned as SwordFightSkills.KnightFlipTurn turns it.
        /// </summary>
        static void TypicalFlip(List<Vector3> points, List<float> times)
        {
            const float Flight = .8f, Out = .3f, TuckAt = .42f, Height = 1.1f, Stand = .256f, Hooves = .2f;
            const float Edge = .8f, Enemy = 1.5f, Behind = .9f;
            const int Points = 36;
            points.Clear();
            times.Clear();
            Vector3 q = new Vector3(0f, .95f, -Edge - .7f), q2 = q + Vector3.up * .6f;
            Vector3 apex = new Vector3(0f, Height + .35f, Enemy + .2f), land = new Vector3(0f, Stand + .03f, Enemy + Behind);
            for (int i = 0; i <= Points; i++)
            {
                float k = Mathf.Lerp(TuckAt, 1f, i / (float)Points), v = (k - Out) / (1f - Out), w = 1f - v;
                Vector3 hips = w * w * w * q + 3f * w * w * v * q2 + 3f * w * v * v * apex + v * v * v * land;
                // Thrown back (-24) at the turn over, then on over the top into the buck (360 + 40).
                float turn = Mathf.Lerp(-24f, 400f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TuckAt, .95f, k)));
                points.Add(hips + Quaternion.AngleAxis(turn, Vector3.right) * (Vector3.down * Hooves));
                times.Add(k * Flight);
            }
        }

        // ---------------------------------------------------------------- E1: the coil

        /// <summary>
        /// E1, the crouch before the spring (SfFxKind.KnightCrouch, its 0.35 s; the game plays it on the floor between its
        /// hind hooves, its +Z the knight's way, and clears it if the crouch is broken off): thin bronze threads of light
        /// drawn in from round its legs down to the floor between its hooves, more and more of them, and a glow there
        /// filling up round a hot core until the spring, when it is gone (E2 flashes there). No ring.
        /// </summary>
        static GameObject BuildCoil()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(CoilPrefab);
            var root = new GameObject("10_Knight_E_Coil");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = .45f;
            // The first threads, more of them, the glow filling, at its fullest as it springs (0.35 s), gone.
            cue.captureTimes = new[] { 0f, .08f, .16f, .24f, .3f, .34f, .4f };

            var threads = new GameObject(CoilThreadPart);
            threads.transform.SetParent(root.transform, false);
            Threads(threads, Material(CoilThreadMaterial, "ChessFight/VFX/Soft Glow Additive", new[] { ("_Color", Bronze) }, ("_Intensity", 4f), ("_Falloff", 2f)));
            KeepParticles(old, CoilThreadPart, threads);

            var glow = new GameObject(CoilGlowPart);
            glow.transform.SetParent(root.transform, false);
            FloorGlow(glow, Material(CoilGlowMaterial, "ChessFight/VFX/Soft Glow Additive", new[] { ("_Color", Bronze) }, ("_Intensity", 2.5f), ("_Falloff", 2.5f)), .6f);
            KeepParticles(old, CoilGlowPart, glow);

            var core = new GameObject(CoilCorePart);
            core.transform.SetParent(root.transform, false);
            FloorGlow(core, Material(CoilCoreMaterial, "ChessFight/VFX/Soft Glow Additive", new[] { ("_Color", WarmWhite) }, ("_Intensity", 5f), ("_Falloff", 3f)), .16f);
            KeepParticles(old, CoilCorePart, core);

            var prefab = VfxAssets.Prefab(CoilPrefab, root);
            Register("E_Coil", prefab);
            return prefab;
        }

        /// <summary>Thin threads of light born on a ring round its legs (about 0.25 m out, 0.32 m up) through the crouch,
        /// more of them as it goes on, each drawn straight in to the floor between its hooves (the system's middle),
        /// brightening as it nears it and gone as it gets there; only their trails are drawn, as E4's streaks.</summary>
        static void Threads(GameObject go, Material mat)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = .3f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.15f, .18f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(.008f, .014f);
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 40;
            var emission = ps.emission;
            // From a few to many as it coils (about 13 in all).
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 15f), new Keyframe(1f, 70f)));
            var shape = ps.shape;
            // A flat ring round its legs, a little uneven.
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .28f;
            shape.radiusThickness = .35f;
            shape.position = new Vector3(0f, .32f, 0f);
            shape.rotation = new Vector3(90f, 0f, 0f);
            shape.randomPositionAmount = .06f;
            // Straight in to the middle: about 0.4 m at 2.4 m/s, their life.
            var pull = ps.velocityOverLifetime;
            pull.enabled = true;
            pull.space = ParticleSystemSimulationSpace.Local;
            pull.radial = new ParticleSystem.MinMaxCurve(-2.4f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .6f), new Keyframe(1f, 1f)));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .25f), new GradientAlphaKey(1f, .8f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var trails = ps.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = 1f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(.6f);
            trails.minVertexDistance = .015f;
            trails.worldSpace = false;
            trails.dieWithParticles = true;
            trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
            trails.sizeAffectsWidth = true;
            trails.inheritParticleColor = true;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, .2f)));
            var tail = new Gradient();
            tail.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trails.colorOverTrail = tail;
            var view = go.GetComponent<ParticleSystemRenderer>();
            view.sharedMaterial = mat;
            view.trailMaterial = mat;
            Quiet(view);
            view.renderMode = ParticleSystemRenderMode.None;
        }

        /// <summary>One glow <paramref name="size"/> across lying on the floor (half a centimetre up), growing from half
        /// that and filling up through the crouch to its brightest as it springs (0.34 s), then gone by 0.4 s.</summary>
        static void FloorGlow(GameObject go, Material mat, float size)
        {
            go.transform.localPosition = new Vector3(0f, .005f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = .1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = .4f;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 1;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var shape = ps.shape;
            shape.enabled = false;
            var grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .5f), new Keyframe(.85f, 1f), new Keyframe(1f, 1f)));
            var fill = ps.colorOverLifetime;
            fill.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.25f, .35f), new GradientAlphaKey(1f, .85f), new GradientAlphaKey(0f, 1f) });
            fill.color = g;
            var view = go.GetComponent<ParticleSystemRenderer>();
            view.sharedMaterial = mat;
            Quiet(view);
            view.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        }

        // ---------------------------------------------------------------- E2: the spring

        /// <summary>
        /// E2, the spring off the edge (SfFxKind.KnightFlip; the game plays it on the brass lip it kicks off, its +Z out
        /// over the drop, or where its hooves leave the board when it springs from further in): a crisp warm-white star
        /// with a long level streak and a cross, gone in a blink (<see cref="VfxStarFlash"/>, the king's block star with
        /// its own material), and four or five thin straight bronze streaks fanned out and down over the drop. Under
        /// the realistic brass flakes and the bent air of the turn, which go on as they are. No ring.
        /// </summary>
        static GameObject BuildSpring()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(SpringPrefab);
            var root = new GameObject("11_Knight_E_Spring");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = .4f;
            // The kick off, the star blazing and gone, the streaks fanning down, thinning and gone.
            cue.captureTimes = new[] { 0f, .015f, .03f, .05f, .08f, .12f, .18f };
            // Over the stage's floor as over the drop, so the streaks running down past the lip are not cut by it.
            cue.captureHeight = .6f;

            var star = new GameObject(SpringStarPart);
            star.transform.SetParent(root.transform, false);
            star.transform.localPosition = new Vector3(0f, .04f, 0f);
            star.AddComponent<MeshFilter>().sharedMesh = StarMeshAsset();
            var view = star.AddComponent<MeshRenderer>();
            // A level streak along the lip and the cross (the reference's four long rays), few short rays, a tight glow.
            view.sharedMaterial = Material(SpringStarMaterial, "ChessFight/VFX/Star Flash Additive", new[] { ("_Color", Bronze), ("_CoreColor", WarmWhite) },
                ("_Aspect", 2.2f), ("_Streak", .8f), ("_Cross", 1.4f), ("_Rays", 10f), ("_RayStrength", .8f), ("_Glow", .5f), ("_RayThin", 45f), ("_CrossThin", 45f));
            Quiet(view);
            var burst = star.AddComponent<VfxStarFlash>();
            // About half a metre across, quicker than the kick's star: the hooves only push off.
            burst.startSize = .55f;
            burst.endSize = .3f;
            burst.duration = .08f;
            burst.streakTime = .1f;
            Keep(old, SpringStarPart, burst);
            star.transform.localScale = Vector3.one * burst.startSize;

            var streaks = new GameObject(SpringStreakPart);
            streaks.transform.SetParent(root.transform, false);
            Streaks(streaks, Material(SpringStreakMaterial, "ChessFight/VFX/Soft Glow Additive", new[] { ("_Color", Bronze) }, ("_Intensity", 5f), ("_Falloff", 2f)));
            // Fewer and thinner than the kick's, slower, fanned wider and tipped down over the drop.
            var ps = streaks.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.1f, .16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 7.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.012f, .022f);
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 4, 5) });
            var shape = ps.shape;
            shape.angle = 26f;
            shape.rotation = new Vector3(SpringTip, 0f, 0f);
            KeepParticles(old, SpringStreakPart, streaks);

            var prefab = VfxAssets.Prefab(SpringPrefab, root);
            Register("E_Spring", prefab);
            return prefab;
        }

        // ---------------------------------------------------------------- E4: the kick

        /// <summary>
        /// E4, the buck as it takes someone (SfFxKind.KnightKickHit; the game plays it on the body where its hind hooves
        /// meet it, a hair off it, its +Z the way the body is kicked): a hot white star with thin bronze rays there, no
        /// level streak and no cross, gone in a blink (<see cref="VfxStarFlash"/>), and six to eight long straight bronze
        /// streaks shot on the way it is kicked. The realistic horseshoe sparks (SwordFightSkillFx.RealKnightKickHit,
        /// chosen 10-26) go on under it as they are, so the reference's spray is only these few streaks (사용자 10-28:
        /// "E4 줄기를 적게"). No ring.
        /// </summary>
        static GameObject BuildKick()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(KickPrefab);
            var root = new GameObject("13_Knight_E_Kick");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Chest;
            cue.length = .5f;
            // The strike, the star blazing and shrinking, the streaks shooting out, thinning and gone.
            cue.captureTimes = new[] { 0f, .02f, .04f, .07f, .1f, .14f, .18f };
            // Where the hooves meet it: the chest height the realistic hit finds the body at.
            cue.captureHeight = .9f;

            var star = new GameObject(StarPart);
            star.transform.SetParent(root.transform, false);
            star.AddComponent<MeshFilter>().sharedMesh = VfxAssets.SaveMesh(StarMesh, StarQuad());
            var view = star.AddComponent<MeshRenderer>();
            // Square (no level streak to make room for), no cross and fewer rays than the king's block star.
            view.sharedMaterial = Material(StarMaterial, "ChessFight/VFX/Star Flash Additive", new[] { ("_Color", Bronze), ("_CoreColor", WarmWhite) },
                ("_Aspect", 1f), ("_Streak", 0f), ("_Cross", 0f), ("_Rays", 16f), ("_RayStrength", 1.5f));
            Quiet(view);
            var burst = star.AddComponent<VfxStarFlash>();
            // About 0.7 m across (the reference's star on the body), quicker than the king's block star.
            burst.startSize = .75f;
            burst.endSize = .4f;
            burst.duration = .12f;
            burst.streakTime = .12f;
            Keep(old, StarPart, burst);
            star.transform.localScale = Vector3.one * burst.startSize;

            var streaks = new GameObject(StreakPart);
            streaks.transform.SetParent(root.transform, false);
            Streaks(streaks, Material(StreakMaterial, "ChessFight/VFX/Soft Glow Additive", new[] { ("_Color", Bronze) }, ("_Intensity", 3f), ("_Falloff", 2f)));
            KeepParticles(old, StreakPart, streaks);

            var prefab = VfxAssets.Prefab(KickPrefab, root);
            Register("E_Kick", prefab);
            return prefab;
        }

        /// <summary>Six to eight long straight bronze streaks shot the way it is kicked in a narrow cone tipped a little up
        /// (as the horseshoe sparks under them fly), quick, thinning as they fly and gone within a fifth of a second; no
        /// gravity. Each is drawn as its trail, which grows from where it was shot, half its life long (0.3 to 0.8 m).</summary>
        static void Streaks(GameObject go, Material mat)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = .1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.1f, .18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.02f, .035f);
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 12;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6, 8) });
            var shape = ps.shape;
            // Round +Z (the way it is kicked), tipped up 12 degrees, 24 about it.
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = .03f;
            shape.rotation = new Vector3(-12f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, .4f)));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .3f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            // Only their trails are drawn: a stretched particle reaches its whole length back from where it is, so at the
            // start it stuck out behind the hooves (R189, the first capture); a trail is never longer than the way flown.
            var trails = ps.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = 1f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(.5f);
            trails.minVertexDistance = .02f;
            trails.worldSpace = true;
            trails.dieWithParticles = true;
            trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
            trails.sizeAffectsWidth = true;
            trails.inheritParticleColor = true;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, .2f)));
            var tail = new Gradient();
            tail.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trails.colorOverTrail = tail;
            var view = go.GetComponent<ParticleSystemRenderer>();
            view.sharedMaterial = mat;
            view.trailMaterial = mat;
            Quiet(view);
            view.renderMode = ParticleSystemRenderMode.None;
        }

        /// <summary>A 1 x 1 m quad in XY, UVs 0..1, for the starburst shader, which turns it to the camera (as the king's
        /// block star's, KingVfxBuilder): its bounds cover it at any turn and any width the material is given.</summary>
        static Mesh StarQuad()
        {
            var mesh = new Mesh { name = "SM_Knight_Star" };
            mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0f), new Vector3(.5f, -.5f, 0f), new Vector3(-.5f, .5f, 0f), new Vector3(.5f, .5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 8f);
            return mesh;
        }

        /// <summary>E4's star quad as it was saved, made only if it is missing (E2 shares it and leaves it alone).</summary>
        static Mesh StarMeshAsset() => AssetDatabase.LoadAssetAtPath<Mesh>(StarMesh) ?? VfxAssets.SaveMesh(StarMesh, StarQuad());

        // ---------------------------------------------------------------- shared

        /// <summary>The values tuned on the old prefab's layer, copied onto the rebuilt one.</summary>
        static void Keep<T>(GameObject old, string layer, T fresh) where T : Component
        {
            var part = old != null ? old.transform.Find(layer) : null;
            var tuned = part != null ? part.GetComponent<T>() : null;
            if (tuned != null) EditorUtility.CopySerialized(tuned, fresh);
        }

        /// <summary>The same for a particle layer: its system and its renderer.</summary>
        static void KeepParticles(GameObject old, string layer, GameObject fresh)
        {
            Keep(old, layer, fresh.GetComponent<ParticleSystem>());
            Keep(old, layer, fresh.GetComponent<ParticleSystemRenderer>());
        }

        /// <summary>No shadows or probes: the effects are light.</summary>
        static void Quiet(Renderer view)
        {
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            view.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>The material at the path on <paramref name="shader"/>; its colours only when it is first made (then they
        /// are the user's).</summary>
        static Material Material(string path, string shader, params (string name, Color colour)[] first) =>
            Material(path, shader, first, new (string, float)[0]);

        /// <summary>The same with values too, also only when it is first made.</summary>
        static Material Material(string path, string shader, (string name, Color colour)[] colours, params (string name, float value)[] values)
        {
            var mat = VfxAssets.Material(path, shader, out bool made);
            if (made)
            {
                foreach (var (name, colour) in colours) mat.SetColor(name, colour);
                foreach (var (name, value) in values) mat.SetFloat(name, value);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Lists <paramref name="prefab"/> under <paramref name="key"/> in the knight's set (made if missing), the
        /// name the skill code plays it by.</summary>
        static void Register(string key, GameObject prefab)
        {
            var set = AssetDatabase.LoadAssetAtPath<VfxSet>(SetPath);
            if (set == null)
            {
                VfxAssets.Folder("Assets/Resources/VfxSets");
                set = ScriptableObject.CreateInstance<VfxSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            var entry = new VfxSet.Entry { key = key, prefab = prefab };
            int i = set.entries.FindIndex(e => e.key == key);
            if (i >= 0) set.entries[i] = entry;
            else set.entries.Add(entry);
            EditorUtility.SetDirty(set);
        }
    }
}
