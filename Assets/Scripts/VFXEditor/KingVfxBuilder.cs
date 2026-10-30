using UnityEditor;
using UnityEngine;

namespace ChessFight.VFX.Editor
{
    /// <summary>
    /// The king's F and E effects, made the new way, one layer a prefab (R183, 10-28; DECISIONS FX7), for the sword
    /// fight's 왕의 반격 and 왕의 귀환 (the layer plan F1..F5 and E1..E6, the timing and the reference prompts are in
    /// Docs/VFX/VFX_Progress.md; the references in Assets/VFX/Reference/king), in the king's deep antique gold (#C9952F,
    /// the board colour BKing: 사용자 R129 "킹이랑 퀸 색을 구별 해줘 퀸이 더 빛이 강하게") with a warm white core
    /// (#FFF4D6). New prefabs are numbered 21.. (F) and 31.. (E), clear of the old way's 01..10, which stay until the user
    /// says to remove them; each new layer takes over its moment's key in Resources/VfxSets/King. Menu
    /// ChessFight/VFX/King/Build writes the generated meshes and materials and the prefabs built so far; play them in the
    /// VFX workbench (piece 1) or take frames of them with VfxCapture. Values tuned in a prefab's parts (and where a part
    /// sits) are kept when it is built again; a material is only made when missing, so its tuned colours stay too. The
    /// landing builders (KingLandBuilder) are separate. Built so far: F3, the counter cut (F_Counter), F2, the block
    /// (F_Block, R185), F4, whoever the counter strikes (F_Hit, R187), F1, the guard (F_Guard, R190), F5, the guard
    /// that came to nothing (F_Whiff, R193), E1, the sword into the wall (E_Stab, R196; its sparks R198), and E2 .. E6
    /// (KingVfxBuilder.Edge.cs, R199 .. R203).
    /// </summary>
    public static partial class KingVfxBuilder
    {
        const string SetPath = "Assets/Resources/VfxSets/King.asset";
        const string GuardPrefab = VfxAssets.Root + "/Prefabs/King/21_King_F_Guard.prefab";
        const string GridMaterial = VfxAssets.Root + "/Materials/M_King_Guard_Grid.mat";
        const string BladeMaterial = VfxAssets.Root + "/Materials/M_King_Guard_Blade.mat";
        const string TipMaterial = VfxAssets.Root + "/Materials/M_King_Guard_Tip.mat";
        const string FloorMesh = VfxAssets.Root + "/Meshes/VFX_FloorQuad.asset";
        const string BillboardMesh = VfxAssets.Root + "/Meshes/SM_BillboardQuad.asset";
        const string SquaresPart = "F1 Squares", BladePart = "F1 Blade", GuardMotesPart = "F1 Motes", WavePart = "F1 Wave";
        const string WaveMaterial = VfxAssets.Root + "/Materials/M_King_Guard_Wave.mat";
        const string WaveMesh = VfxAssets.Root + "/Meshes/SM_King_GuardWave.asset";
        const string CounterPrefab = VfxAssets.Root + "/Prefabs/King/23_King_F_Counter.prefab";
        const string CounterMaterial = VfxAssets.Root + "/Materials/M_King_Counter.mat";
        const string CounterMesh = VfxAssets.Root + "/Meshes/SM_King_Counter.asset";
        const string CounterPart = "F3 Crescent";
        const string ParryPrefab = VfxAssets.Root + "/Prefabs/King/22_King_F_Parry.prefab";
        const string StarMaterial = VfxAssets.Root + "/Materials/M_King_Star.mat";
        const string SliverMaterial = VfxAssets.Root + "/Materials/M_King_Sliver.mat";
        const string StarMesh = VfxAssets.Root + "/Meshes/SM_King_Star.asset";
        const string StarPart = "F2 Star", SliverPart = "F2 Slivers";
        const string HitPrefab = VfxAssets.Root + "/Prefabs/King/24_King_F_Hit.prefab";
        const string CutMaterial = VfxAssets.Root + "/Materials/M_King_Cut.mat";
        const string CutPart = "F4 Cut", HitSliverPart = "F4 Slivers", AirPart = "F4 Air";
        const string WhiffPrefab = VfxAssets.Root + "/Prefabs/King/25_King_F_Whiff.prefab";
        const string MoteMaterial = VfxAssets.Root + "/Materials/M_King_Mote.mat";
        // The game finds these two by name (SwordFightSkillFx.KingWhiffVfx) to keep them on his blade as it drops.
        const string MotesPart = "F5 Motes", PointPart = "F5 Point";
        // His blade, from the hilt end of the steel to the point (SwordFightPawn.BladeRoot to BladeTip).
        const float BladeLength = .75f;
        // Where it lies once the point has dropped, for the workbench and the captures (the game puts the parts on his own
        // blade): the hilt end low in front of him and the blade the way SwordFightSkills.KingD points it, a little to his
        // right, down and forward.
        static readonly Vector3 DroppedHilt = new Vector3(.14f, .72f, .42f);
        static readonly Vector3 DroppedTip = DroppedHilt + new Vector3(.2f, -.75f, .6f).normalized * BladeLength;
        // F4's cut stands this far out from his chest toward the king, so it lies on his front (the bishop's F7 does the same).
        const float CutOut = .3f;
        const string StabPrefab = VfxAssets.Root + "/Prefabs/King/31_King_E_Stab.prefab";
        const string StabWallMaterial = VfxAssets.Root + "/Materials/M_King_StabWall.mat";
        const string StabStoneMaterial = VfxAssets.Root + "/Materials/M_King_StabStone.mat";
        // The game finds these two by name (SwordFightSkillFx.KingStabVfx) and moves them to the breach it has broken.
        const string StabStarPart = "E1 Star", CracksPart = "E1 Cracks";
        // Where the blade crosses the wall's face, from his hilt (where the game plays it, +Z out of the wall): the
        // platform's face is 0.18 m in from it (SwordFightSkills.Hilt), the board's brass trim stands 3 cm out of that, and
        // the blade slants down into it.
        static readonly Vector3 StabCrater = new Vector3(0f, -.04f, -.15f);
        // The glow is given a deeper, more orange gold (#CC851F) than the one it should show as: ACES lifts the green of a
        // bright gold toward yellow and white (R179, the knight), and under it this one shows as the king's #C9952F.
        static readonly Color Gold = new Color(.8f, .52f, .12f), WarmWhite = new Color(1f, .957f, .839f);

        [MenuItem("ChessFight/VFX/King/Build")]
        public static void Build()
        {
            // In Play mode the layers' Awake runs as they are made and what it does is saved into the prefab (VFX1 found a
            // flash quad saved lit that way): build in edit mode only.
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the king effects are built in edit mode.");
                return;
            }
            var guard = BuildGuard();
            var counter = BuildCounter();
            var parry = BuildParry();
            var hit = BuildHit();
            var whiff = BuildWhiff();
            var stab = BuildStab();
            var mark = BuildMark();
            var vault = BuildVault();
            var land = BuildLand();
            var landHit = BuildLandHit();
            var drop = BuildDrop();
            AssetDatabase.SaveAssets();
            Selection.activeObject = counter;
            Debug.Log("[VFX] King built: F1 the guard (F_Guard), F3 the counter cut (F_Counter), F2 the block (F_Block), "
                + "F4 whoever the counter strikes (F_Hit), F5 the whiff (F_Whiff), E1 the stab (E_Stab), E2 the crown foretold (E_Gather), E3 the spring (E_Vault), "
                + "E4 the landing (E_Land), E5 whoever it throws (E_Hit), E6 the fall (E_Drop). Workbench: piece 1, "
                + guard.name + ", " + counter.name + ", " + parry.name + ", " + hit.name + ", " + whiff.name + ", " + stab.name + ", "
                + mark.name + ", " + vault.name + ", " + land.name + ", " + landHit.name + ", " + drop.name);
        }

        /// <summary>The menu is greyed out in Play mode.</summary>
        [MenuItem("ChessFight/VFX/King/Build", true)]
        static bool CanBuild() => !EditorApplication.isPlaying;

        [MenuItem("ChessFight/VFX/King/Build F1 (guard only)")]
        public static void BuildF1() => BuildOne(BuildGuard, "F1 the guard (F_Guard)");

        [MenuItem("ChessFight/VFX/King/Build F1 (guard only)", true)]
        static bool CanBuildF1() => !EditorApplication.isPlaying;

        [MenuItem("ChessFight/VFX/King/Build F2 (block only)")]
        public static void BuildF2() => BuildOne(BuildParry, "F2 the block (F_Block)");

        [MenuItem("ChessFight/VFX/King/Build F2 (block only)", true)]
        static bool CanBuildF2() => !EditorApplication.isPlaying;

        [MenuItem("ChessFight/VFX/King/Build F4 (hit only)")]
        public static void BuildF4() => BuildOne(BuildHit, "F4 whoever the counter strikes (F_Hit)");

        [MenuItem("ChessFight/VFX/King/Build F4 (hit only)", true)]
        static bool CanBuildF4() => !EditorApplication.isPlaying;

        [MenuItem("ChessFight/VFX/King/Build F5 (whiff only)")]
        public static void BuildF5() => BuildOne(BuildWhiff, "F5 the whiff (F_Whiff)");

        [MenuItem("ChessFight/VFX/King/Build F5 (whiff only)", true)]
        static bool CanBuildF5() => !EditorApplication.isPlaying;

        [MenuItem("ChessFight/VFX/King/Build E1 (stab only)")]
        public static void BuildE1() => BuildOne(BuildStab, "E1 the stab (E_Stab)");

        [MenuItem("ChessFight/VFX/King/Build E1 (stab only)", true)]
        static bool CanBuildE1() => !EditorApplication.isPlaying;

        /// <summary>Only one layer, the other layers' prefabs left as they are.</summary>
        static void BuildOne(System.Func<GameObject> build, string what)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the king effects are built in edit mode.");
                return;
            }
            var prefab = build();
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            Debug.Log("[VFX] King built: " + what + ". Workbench: piece 1, " + prefab.name);
        }

        // ---------------------------------------------------------------- F1: the guard

        /// <summary>
        /// F1, the guard (SfFxKind.Cast as he sets himself, for the 0.9 s of it; the game plays it on the floor under his
        /// hips, its +Z the way he faces): the board's lines round his square and the eight round it, his moves, drawn out
        /// in gold from his square in a blink and kept lit through the guard, running on a little past the outer edge, the
        /// eight squares faintly filled and a soft band going slowly round (<see cref="VfxSquareGrid"/>), and a run of gold
        /// light up his blade from the hilt to the point, which keeps a small sparkle, a soft glow left on the steel
        /// (<see cref="VfxBladeGlow"/>). Both blink through the guard's last 0.2 s and go out as it ends. The squares are
        /// round him and turned his way, as the knight's are (SwordFightSkillParams.knightCell), not on the board's own
        /// lines. Nothing hides his body.
        /// </summary>
        static GameObject BuildGuard()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(GuardPrefab);
            var root = new GameObject("21_King_F_Guard");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 1.2f;
            // The lines running out, out and flashing, settled with the blade lit, the band going round, blinking at the
            // end of the guard (0.7..0.9 s), going out.
            cue.captureTimes = new[] { .02f, .06f, .12f, .2f, .45f, .72f, .76f, .95f };

            var squares = new GameObject(SquaresPart);
            squares.transform.SetParent(root.transform, false);
            squares.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(FloorMesh)
                ?? VfxAssets.SaveMesh(FloorMesh, VfxAssets.FloorQuad());
            var floor = squares.AddComponent<MeshRenderer>();
            floor.sharedMaterial = Material(GridMaterial, "ChessFight/VFX/Square Grid Additive", new[] { ("_Color", Gold), ("_CoreColor", WarmWhite) });
            Quiet(floor);
            var grid = squares.AddComponent<VfxSquareGrid>();
            Keep(old, SquaresPart, grid);
            squares.transform.localPosition = Vector3.up * grid.lift;
            squares.transform.localRotation = Quaternion.identity;
            squares.transform.localScale = Vector3.one * ((3f + 2f * grid.overrun) * grid.cell + .2f);

            // 10-28: gold specks lifting off the four sides of his square. The grid emits them itself along the sides
            // (VfxSquareGrid.EmitMotes), so this system has no emission of its own and keeps running until the cue ends.
            var specks = new GameObject(GuardMotesPart);
            specks.transform.SetParent(root.transform, false);
            var speckSystem = Motes(specks, 1f, new Vector2(.6f, 1.1f), Vector2.zero, new Vector2(.024f, .046f), -.04f);
            var speckMain = speckSystem.main;
            speckMain.loop = true;
            speckMain.maxParticles = 96;
            Embers(speckSystem, MoteMat());
            KeepParticles(old, GuardMotesPart, specks);
            // After Keep: it copies the old prefab's field values, the old emitter reference among them.
            grid.motes = speckSystem;

            // 10-28 (the user: a ripple like water rising softly off the border as far as his belly): a wall of gold light on
            // the four sides of his square, an open box 1 m on a side and 1 m high that the grid sizes (VfxSquareGrid.DrawWave).
            var wave = new GameObject(WavePart);
            wave.transform.SetParent(root.transform, false);
            wave.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WaveMesh) ?? VfxAssets.SaveMesh(WaveMesh, WaveTube());
            var waveView = wave.AddComponent<MeshRenderer>();
            waveView.sharedMaterial = Material(WaveMaterial, "ChessFight/VFX/Square Wave Additive", new[] { ("_Color", Gold) });
            Quiet(waveView);
            wave.transform.localPosition = Vector3.zero;
            wave.transform.localRotation = Quaternion.identity;
            wave.transform.localScale = new Vector3(grid.cell, grid.waveHeight, grid.cell);
            grid.wave = waveView;

            var blade = new GameObject(BladePart);
            blade.transform.SetParent(root.transform, false);
            var line = blade.AddComponent<LineRenderer>();
            // A run of light up the steel and a glow left on it, brighter toward the point (the run's head held past it);
            // no fade at the scene's depth: it lies on the blade.
            line.sharedMaterial = Material(BladeMaterial, "ChessFight/VFX/Slash Additive",
                new[] { ("_Color", Gold), ("_CoreColor", WarmWhite) }, ("_CoreBoost", 2f), ("_CoreWidth", .2f),
                ("_HeadLength", .25f), ("_TipFade", .05f), ("_DepthFade", 0f));
            Quiet(line);
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            var glow = blade.AddComponent<VfxBladeGlow>();
            Keep(old, BladePart, glow);
            blade.transform.localPosition = Vector3.zero;
            blade.transform.localRotation = Quaternion.identity;
            line.positionCount = 2;
            line.SetPositions(new[] { glow.previewGuard, glow.previewTip });
            line.widthMultiplier = glow.width;

            var tip = new GameObject(VfxBladeGlow.TipName);
            tip.transform.SetParent(blade.transform, false);
            tip.transform.localPosition = glow.previewTip;
            tip.transform.localScale = Vector3.one * glow.sparkleSize;
            tip.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(BillboardMesh);
            var spark = tip.AddComponent<MeshRenderer>();
            spark.sharedMaterial = Material(TipMaterial, "ChessFight/VFX/Glint Additive", new[] { ("_Color", Color.Lerp(Gold, WarmWhite, .6f)) });
            Quiet(spark);

            var prefab = VfxAssets.Prefab(GuardPrefab, root);
            Register("F_Guard", prefab);
            return prefab;
        }

        /// <summary>An open box on its side: four walls round a unit square on the floor, 1 m high, u running once round them
        /// (0 to 4) and v up them (0 floor to 1 top), for the wave rising off the sides of his square.</summary>
        static Mesh WaveTube()
        {
            var mesh = new Mesh { name = "SM_King_GuardWave" };
            var corners = new[] { new Vector3(-.5f, 0f, -.5f), new Vector3(.5f, 0f, -.5f), new Vector3(.5f, 0f, .5f), new Vector3(-.5f, 0f, .5f) };
            var vertices = new Vector3[16];
            var uv = new Vector2[16];
            var triangles = new int[24];
            for (int side = 0; side < 4; side++)
            {
                Vector3 a = corners[side], b = corners[(side + 1) % 4];
                int v = side * 4, t = side * 6;
                vertices[v] = a;
                vertices[v + 1] = b;
                vertices[v + 2] = a + Vector3.up;
                vertices[v + 3] = b + Vector3.up;
                uv[v] = new Vector2(side, 0f);
                uv[v + 1] = new Vector2(side + 1f, 0f);
                uv[v + 2] = new Vector2(side, 1f);
                uv[v + 3] = new Vector2(side + 1f, 1f);
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 2;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 1;
            }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------------- F2: the block

        /// <summary>
        /// F2, the block (SfFxKind.KingParry; the game plays it where his blade met the blow, its +Z toward whoever struck):
        /// a blinding star there, white-hot in the middle with a burst of thin gold rays and a long level streak through
        /// it, gone in a blink (<see cref="VfxStarFlash"/>), and a spray of fine gold slivers thrown back and up at the
        /// attacker, arcing down and fading within half a second. No ring.
        /// </summary>
        static GameObject BuildParry()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(ParryPrefab);
            var root = new GameObject("22_King_F_Parry");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Chest;
            cue.length = .6f;
            // The blow, the star blazing and shrinking, the streak thinning, the slivers flying, falling and gone.
            cue.captureTimes = new[] { 0f, .02f, .04f, .07f, .1f, .15f, .22f, .32f, .45f };
            // Where his guard meets a blow, about chest high.
            cue.captureHeight = 1f;

            var star = new GameObject(StarPart);
            star.transform.SetParent(root.transform, false);
            star.AddComponent<MeshFilter>().sharedMesh = VfxAssets.SaveMesh(StarMesh, StarQuad());
            var view = star.AddComponent<MeshRenderer>();
            view.sharedMaterial = Material(StarMaterial, "ChessFight/VFX/Star Flash Additive", new[] { ("_Color", Gold), ("_CoreColor", WarmWhite) });
            Quiet(view);
            var burst = star.AddComponent<VfxStarFlash>();
            Keep(old, StarPart, burst);
            star.transform.localScale = Vector3.one * burst.startSize;

            var slivers = new GameObject(SliverPart);
            slivers.transform.SetParent(root.transform, false);
            slivers.transform.localPosition = new Vector3(0f, 0f, .02f);
            Slivers(slivers, SliverMat());
            KeepParticles(old, SliverPart, slivers);

            var prefab = VfxAssets.Prefab(ParryPrefab, root);
            Register("F_Block", prefab);
            return prefab;
        }

        /// <summary>Fine gold slivers knocked off the meeting blades, thrown back and up at whoever struck in a wide fan
        /// (the reference's spray over the clash), stretched along their way, white-hot as they fly off, cooling to a deeper
        /// gold, arcing down and gone within half a second.</summary>
        static void Slivers(GameObject go, Material mat)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = .1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(.014f, .03f);
            main.startColor = Color.white;
            main.gravityModifier = 1.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 46), new ParticleSystem.Burst(.03f, 14) });
            var shape = ps.shape;
            // Round +Z (back at the attacker), tipped well up, 50 degrees about it.
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 50f;
            shape.radius = .03f;
            shape.rotation = new Vector3(-35f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, .35f)));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = Cooling();
            var view = go.GetComponent<ParticleSystemRenderer>();
            view.sharedMaterial = mat;
            Quiet(view);
            view.renderMode = ParticleSystemRenderMode.Stretch;
            view.velocityScale = .03f;
            view.lengthScale = 1f;
        }

        /// <summary>A 1 x 1 m quad in XY, UVs 0..1, for the starburst shader, which turns it to the camera and draws it up to
        /// eight times as wide as it is high (the streak): its bounds cover that at any turn, so it is not culled.</summary>
        static Mesh StarQuad()
        {
            var mesh = new Mesh { name = "SM_King_Star" };
            mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0f), new Vector3(.5f, -.5f, 0f), new Vector3(-.5f, .5f, 0f), new Vector3(.5f, .5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 8f);
            return mesh;
        }

        // ---------------------------------------------------------------- F3: the counter cut

        /// <summary>
        /// F3, the face of the skill (SfFxKind.KingCounterSpin, as the cut starts; 0.34 s, the blow landing 0.085 s in):
        /// a crescent of gold light from over his right shoulder down through his front to low on his left, drawn as the
        /// blade goes, flaring where it lands, then burning away to the edge near its end (<see cref="VfxCrescent"/>). The
        /// root is on the floor under his hips, its +Z the way he cuts; the crescent turns about his sword shoulder, in the
        /// plane the blade's lines span (SwordFightSkills.Motion.cs KingC, KingX, KingL: up and to his right, then ahead,
        /// then low left, about 3, 102 and 138 degrees round it).
        /// </summary>
        static GameObject BuildCounter()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(CounterPrefab);
            var root = new GameObject("23_King_F_Counter");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = .9f;
            // Drawing, the blow landing (0.085 s), on to the end of the swing (0.34 s), hanging, burning away.
            cue.captureTimes = new[] { .03f, .06f, .085f, .15f, .25f, .34f, .48f, .62f, .7f };

            var go = new GameObject(CounterPart);
            go.transform.SetParent(root.transform, false);
            // His sword shoulder, a metre up; its XY the swing's plane: X up and to his right (the blade cocked), Y ahead.
            go.transform.localPosition = new Vector3(.2f, 1f, .05f);
            go.transform.localRotation = Quaternion.LookRotation(new Vector3(.669f, -.743f, 0f), Vector3.forward);
            // (It brings its own MeshFilter and MeshRenderer.)
            var crescent = go.AddComponent<VfxCrescent>();
            crescent.radius = 1.6f;
            crescent.width = .55f;
            Keep(old, CounterPart, crescent);
            go.GetComponent<MeshFilter>().sharedMesh = VfxAssets.SaveMesh(CounterMesh, crescent.MakeMesh());
            var view = go.GetComponent<MeshRenderer>();
            view.sharedMaterial = Material(CounterMaterial, "ChessFight/VFX/Crescent Additive", new[] { ("_Color", Gold), ("_EdgeColor", WarmWhite) });
            Quiet(view);

            var prefab = VfxAssets.Prefab(CounterPrefab, root);
            Register("F_Counter", prefab);
            return prefab;
        }

        // ---------------------------------------------------------------- F4: whoever the counter strikes

        /// <summary>
        /// F4, on each piece the counter strikes (SfFxKind.KingCounterHit, 0.085 s into the cut; 0.6 s from the blow): the
        /// cut across his front as one long thin stroke of gold light on the blade's line, high on the king's right to low
        /// on his left, drawn in a blink, flashing and gone in 0.4 s (<see cref="VfxSlash"/>, the bishop's F7 part), fine
        /// gold slivers off it thrown the way he is pushed and a few faint streaks of air going that way. No ring. The root
        /// is at his chest, its +Z the way he is pushed (so the king is behind it); the cut is <see cref="CutOut"/> back
        /// toward the king. The game hangs it on his chest so it goes with him.
        /// </summary>
        static GameObject BuildHit()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(HitPrefab);
            var root = new GameObject("24_King_F_Hit");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Target;
            cue.length = .6f;
            // The blow, the cut drawn and flashing, the slivers and the air out, the cut dying, the last slivers falling.
            cue.captureTimes = new[] { 0f, .02f, .05f, .09f, .15f, .25f, .4f };
            // His chest.
            cue.captureHeight = .9f;

            var cut = new GameObject(CutPart);
            cut.transform.SetParent(root.transform, false);
            cut.transform.localPosition = new Vector3(0f, 0f, -CutOut);
            var line = cut.AddComponent<LineRenderer>();
            // On a body the light fades only right at the wood (the bishop's F7 cut: 0.03 m), a lower core than the slash
            // shader's 3 keeps the gold from washing out white under ACES, and a thin core in a wide soft glow is the
            // reference's streak (R187 review: 0.24 and 2.2 read as a hard line).
            line.sharedMaterial = Material(CutMaterial, "ChessFight/VFX/Slash Additive",
                new[] { ("_Color", Gold), ("_CoreColor", WarmWhite) }, ("_DepthFade", .03f), ("_CoreBoost", 2f),
                ("_CoreWidth", .15f), ("_GlowFalloff", 1.8f));
            Quiet(line);
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            var stroke = cut.AddComponent<VfxSlash>();
            Cut(stroke);
            Keep(old, CutPart, stroke);
            stroke.Shape();

            var slivers = new GameObject(HitSliverPart);
            slivers.transform.SetParent(root.transform, false);
            slivers.transform.localPosition = new Vector3(0f, 0f, .02f - CutOut);
            HitSlivers(slivers, SliverMat());
            KeepParticles(old, HitSliverPart, slivers);

            var air = new GameObject(AirPart);
            air.transform.SetParent(root.transform, false);
            air.transform.localPosition = new Vector3(0f, 0f, -.1f);
            Air(air, SliverMat());
            KeepParticles(old, AirPart, air);

            var prefab = VfxAssets.Prefab(HitPrefab, root);
            Register("F_Hit", prefab);
            return prefab;
        }

        /// <summary>One long thin stroke on the counter's line, leaning 42 degrees (the swing's plane, KingC to KingL, seen
        /// from behind the king), reaching well past his outline as the reference's does (R187 review: 1.5 m by 0.07 read
        /// short and thin) and bowed a little low in the middle: drawn from its high end in 0.045 s, a flash, gone in
        /// 0.4 s.</summary>
        static void Cut(VfxSlash stroke)
        {
            stroke.length = 1.8f;
            stroke.lean = 42f;
            stroke.leftward = true;
            stroke.bend = .07f;
            stroke.depth = 0f;
            stroke.width = .1f;
            stroke.taper = 1.1f;
            stroke.segments = 20;
            stroke.sweep = .045f;
            stroke.preroll = 0f;
            stroke.duration = .4f;
            stroke.fadeOut = .7f;
            stroke.endWidth = .3f;
            stroke.flashIntensity = 3.5f;
            stroke.intensity = 1.6f;
            stroke.flashTime = .08f;
        }

        /// <summary>Ninety fine gold specks off the cut, from all along it, thrown the way he is pushed, tipped up and spread
        /// wide so they show round his outline from behind the king (the reference's specks all round the stroke; R187
        /// review: seventy at half the spread, drawn twice as long, read as strands along the cut), barely stretched,
        /// white-hot cooling to a deeper gold, arcing down and gone within half a second.</summary>
        static void HitSlivers(GameObject go, Material mat)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = .1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.2f, .5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.01f, .022f);
            main.startColor = Color.white;
            main.gravityModifier = 1.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 70), new ParticleSystem.Burst(.03f, 20) });
            var shape = ps.shape;
            // A thin box along the cut (its long side turned onto the 42 degrees) sends them along +Z; tipped up 20 degrees
            // and thrown about by 0.7.
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.1f, .02f, .02f);
            shape.rotation = new Vector3(-20f, 0f, 42f);
            shape.randomDirectionAmount = .7f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, .35f)));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            fade.color = Cooling();
            var view = go.GetComponent<ParticleSystemRenderer>();
            view.sharedMaterial = mat;
            Quiet(view);
            view.renderMode = ParticleSystemRenderMode.Stretch;
            view.velocityScale = .015f;
            view.lengthScale = 1f;
        }

        /// <summary>Six long thin streaks of air shot the way he is pushed from all round his outline, quick and barely there
        /// (R187 review: ten at 0.65, drawn half again as long, read as drawn speed lines).</summary>
        static void Air(GameObject go, Material mat)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = .1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.14f, .26f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(.01f, .018f);
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 12;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });
            var shape = ps.shape;
            // A box about his outline; a box sends them straight along +Z.
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1f, .9f, .1f);
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.3f, .15f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var view = go.GetComponent<ParticleSystemRenderer>();
            view.sharedMaterial = mat;
            Quiet(view);
            view.renderMode = ParticleSystemRenderMode.Stretch;
            view.velocityScale = .06f;
            view.lengthScale = 4f;
        }

        // ---------------------------------------------------------------- F5: the whiff

        /// <summary>
        /// F5, the guard that came to nothing (SfFxKind.KingWhiff, when it ran out with no blow to take; the game plays it on
        /// the floor under his hips, its +Z the way he faces): the gold left on his blade breaks up as the point drops to
        /// the board, a few fine motes shed along the steel as it falls and a few more off the point, where F1's glow was
        /// brightest, drifting slowly down and about, cooling from warm white to a deep gold and gone within about 0.7 s.
        /// F1's squares and its glow on the blade go out as the guard ends, so it is the light that held him that
        /// scatters. No haze, no dust, no ring.
        /// </summary>
        static GameObject BuildWhiff()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(WhiffPrefab);
            var root = new GameObject("25_King_F_Whiff");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 1f;
            // Shed as the point drops (to 0.12 s), the point at the board, drifting and cooling, the last going out.
            cue.captureTimes = new[] { .02f, .06f, .1f, .16f, .25f, .4f, .6f, .85f };
            var mat = MoteMat();

            var motes = new GameObject(MotesPart);
            motes.transform.SetParent(root.transform, false);
            // The middle of the blade, its +X down the steel to the point.
            motes.transform.localPosition = (DroppedHilt + DroppedTip) * .5f;
            motes.transform.localRotation = Quaternion.FromToRotation(Vector3.right, DroppedTip - DroppedHilt);
            BladeMotes(motes, mat);
            KeepParticles(old, MotesPart, motes);

            var point = new GameObject(PointPart);
            point.transform.SetParent(root.transform, false);
            point.transform.localPosition = DroppedTip;
            point.transform.localRotation = Quaternion.identity;
            PointMotes(point, mat);
            KeepParticles(old, PointPart, point);

            var prefab = VfxAssets.Prefab(WhiffPrefab, root);
            Register("F_Whiff", prefab);
            return prefab;
        }

        /// <summary>A few motes off the whole blade as it drops, in four small bursts while it falls, barely moving.</summary>
        static void BladeMotes(GameObject go, Material mat)
        {
            // R193 review: 0.014..0.03 m were a pixel or two from where the player sees him.
            var ps = Motes(go, .15f, new Vector2(.5f, .85f), new Vector2(.05f, .25f), new Vector2(.019f, .04f), .08f);
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 5), new ParticleSystem.Burst(.03f, 5),
                new ParticleSystem.Burst(.06f, 5), new ParticleSystem.Burst(.09f, 5) });
            var shape = ps.shape;
            // Anywhere along the steel (an edge down its +X, the blade's length), off it every way.
            shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
            shape.radius = BladeLength * .5f;
            shape.randomDirectionAmount = 1f;
            Embers(ps, mat);
        }

        /// <summary>A few more off the point as it reaches the board, and one or two after while it rests there, hanging
        /// about it rather than falling.</summary>
        static void PointMotes(GameObject go, Material mat)
        {
            var ps = Motes(go, .4f, new Vector2(.3f, .6f), new Vector2(.08f, .3f), new Vector2(.022f, .043f), 0f);
            var emission = ps.emission;
            emission.rateOverTime = 10f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(.1f, 7) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .04f;
            Embers(ps, mat);
        }

        /// <summary>A system of motes: emitted over <paramref name="duration"/> into the world, slowed by the air and
        /// wandering a little as a mote does, <paramref name="gravity"/> drawing them down.</summary>
        static ParticleSystem Motes(GameObject go, float duration, Vector2 life, Vector2 speed, Vector2 size, float gravity)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = duration;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = Color.white;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var air = ps.limitVelocityOverLifetime;
            air.enabled = true;
            air.limit = 5f;
            air.drag = 1.5f;
            var wander = ps.noise;
            wander.enabled = true;
            wander.strength = .12f;
            wander.frequency = 2f;
            wander.scrollSpeed = .4f;
            wander.damping = false;
            wander.quality = ParticleSystemNoiseQuality.Medium;
            return ps;
        }

        /// <summary>Round soft dots, shrinking a little, warm white as they leave the steel and cooling to a deep gold, whole
        /// for the first half of their life, then fading out (the material is a pale gold: times white it is that, times
        /// the orange the deeper gold).</summary>
        static void Embers(ParticleSystem ps, Material mat)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, .4f)));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            // R193 review: an end of (0.85, 0.5, 0.18) cooled them to a red-brown under ACES; this stays a gold.
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, .8f, .45f), .4f),
                    new GradientColorKey(new Color(1f, .66f, .3f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .06f), new GradientAlphaKey(1f, .45f),
                    new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var view = ps.GetComponent<ParticleSystemRenderer>();
            view.sharedMaterial = mat;
            Quiet(view);
            view.renderMode = ParticleSystemRenderMode.Billboard;
        }

        // ---------------------------------------------------------------- E1: the stab

        /// <summary>
        /// E1, the sword into the wall (SfFxKind.KingStab as the blade goes home in the stone; the game plays it at his hilt,
        /// +Z out of the wall, and moves both parts to the breach it has broken): a small gold star where the blade meets the
        /// stone, blazing a tenth of a second (<see cref="VfxStarFlash"/>, F2's star smaller and quicker), and the cracks the
        /// stab breaks into the wall glowing gold, the glow seeping out along them from the crater's rim and cooling to dark
        /// stone within a second (<see cref="VfxCrackGlow"/>, which the game's breach reads; for the workbench and the
        /// captures it draws a breach of its own in a plain brass wall). No dust, no ring.
        /// </summary>
        static GameObject BuildStab()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(StabPrefab);
            var root = new GameObject("31_King_E_Stab");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Chest;
            cue.length = 1.2f;
            // The blade going in, the star, the glow running out, held, cooling, gone.
            cue.captureTimes = new[] { 0f, .02f, .05f, .1f, .2f, .3f, .45f, .65f, .9f };
            // Its preview wall reaches 0.64 m below the hilt: this keeps it over the capture floor.
            cue.captureHeight = .7f;

            var star = new GameObject(StabStarPart);
            star.transform.SetParent(root.transform, false);
            star.transform.localPosition = StabCrater + Vector3.forward * .06f;
            star.AddComponent<MeshFilter>().sharedMesh = VfxAssets.SaveMesh(StarMesh, StarQuad());
            var view = star.AddComponent<MeshRenderer>();
            view.sharedMaterial = Material(StarMaterial, "ChessFight/VFX/Star Flash Additive", new[] { ("_Color", Gold), ("_CoreColor", WarmWhite) });
            Quiet(view);
            var burst = star.AddComponent<VfxStarFlash>();
            // Steel into stone: smaller and quicker than F2's blade on blade.
            burst.duration = .1f;
            burst.streakTime = .14f;
            burst.intensity = 3f;
            burst.startSize = .8f;
            burst.endSize = .35f;
            burst.turnRange = 20f;
            Keep(old, StabStarPart, burst);
            star.transform.localScale = Vector3.one * burst.startSize;

            var cracks = new GameObject(CracksPart);
            cracks.transform.SetParent(root.transform, false);
            cracks.transform.localPosition = StabCrater;
            var glow = cracks.AddComponent<VfxCrackGlow>();
            Keep(old, CracksPart, glow);
            // The preview: a plain brass wall face and top with the breach's two quads over them, as the game lays its own.
            var wall = new GameObject(VfxCrackGlow.PreviewName);
            wall.transform.SetParent(cracks.transform, false);
            var quad = AssetDatabase.LoadAssetAtPath<Mesh>(BillboardMesh);
            var stone = Material(StabStoneMaterial, "Universal Render Pipeline/Unlit", new[] { ("_BaseColor", new Color(.55f, .42f, .24f)) },
                ("_Cull", 0f));
            var breach = Material(StabWallMaterial, "ChessFight/Skill Breach", new (string, Color)[0]);
            float half = glow.previewRadius * 1.2f + glow.previewCracks + .08f, up = glow.previewTop;
            var flat = Quaternion.LookRotation(Vector3.up, Vector3.back);
            Quad(wall, VfxCrackGlow.StoneName, quad, stone, new Vector3(0f, (up - glow.previewBottom) * .5f, 0f), Quaternion.identity,
                new Vector3(2f * half, up + glow.previewBottom, 1f));
            Quad(wall, VfxCrackGlow.StoneTopName, quad, stone, new Vector3(0f, up, -half * .5f), flat, new Vector3(2f * half, half, 1f));
            Quad(wall, VfxCrackGlow.FaceName, quad, breach, new Vector3(0f, 0f, .002f), Quaternion.identity, new Vector3(2f * half, 2f * half, 1f));
            Quad(wall, VfxCrackGlow.TopName, quad, breach, new Vector3(0f, up + .002f, -half * .5f), flat, new Vector3(2f * half, half, 1f));
            StabSparks(root, old);

            var prefab = VfxAssets.Prefab(StabPrefab, root);
            // Takes over the old way's 04_King_E_Stab, which stays until the user says to remove it.
            Register("E_Stab", prefab);
            return prefab;
        }

        static void Quad(GameObject parent, string name, Mesh mesh, Material mat, Vector3 at, Quaternion turn, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.SetLocalPositionAndRotation(at, turn);
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var view = go.AddComponent<MeshRenderer>();
            view.sharedMaterial = mat;
            Quiet(view);
        }

        // ---------------------------------------------------------------- shared

        /// <summary>The pale gold the slivers of F2 and F4 (and F4's air) share.</summary>
        static Material SliverMat() => Material(SliverMaterial, "ChessFight/VFX/Soft Glow Additive",
            new[] { ("_Color", Color.Lerp(Gold, WarmWhite, .5f)) }, ("_Intensity", 3f), ("_Falloff", 2f));

        /// <summary>F5's motes: the slivers' pale gold, softer, and dimmer than a spark struck off steel.</summary>
        static Material MoteMat() => Material(MoteMaterial, "ChessFight/VFX/Soft Glow Additive",
            new[] { ("_Color", Color.Lerp(Gold, WarmWhite, .5f)) }, ("_Intensity", 2.2f), ("_Falloff", 2.5f));

        /// <summary>White-hot as they fly off, cooling to a deeper gold (the material is a pale gold; times white it is that,
        /// times this orange the deeper gold), whole for the first part of their life, then fading out.</summary>
        static Gradient Cooling()
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, .7f, .25f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .45f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        /// <summary>The values tuned on the old prefab's layer, and where it sat, copied onto the rebuilt one.</summary>
        static void Keep<T>(GameObject old, string layer, T fresh) where T : Component
        {
            var part = old != null ? old.transform.Find(layer) : null;
            if (part == null) return;
            fresh.transform.localPosition = part.localPosition;
            fresh.transform.localRotation = part.localRotation;
            var tuned = part.GetComponent<T>();
            if (tuned != null) EditorUtility.CopySerialized(tuned, fresh);
        }

        static void KeepParticles(GameObject old, string layer, GameObject fresh)
        {
            Keep(old, layer, fresh.GetComponent<ParticleSystem>());
            Keep(old, layer, fresh.GetComponent<ParticleSystemRenderer>());
        }

        static void Quiet(Renderer view)
        {
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            view.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>The material at the path on <paramref name="shader"/>; its colours and values only when it is first made
        /// (then they are the user's).</summary>
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

        /// <summary>Lists <paramref name="prefab"/> under <paramref name="key"/> in the king's set (made if missing), the
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
