using System;
using UnityEditor;
using UnityEngine;

namespace ChessFight.VFX.Editor
{
    /// <summary>
    /// The bishop's F and E effects, made the new way, one layer a prefab (R175, 10-28; DECISIONS FX7), for the X-cut F of
    /// 10-27 (사용자: "비숍 프리팹을 처음부터 다시 작업 할거야"; the layer plan F1..F9 and E1..E3, the timing and the reference
    /// prompts are in Docs/VFX/VFX_Progress.md), in the bishop's own silver (#C2C9D3, BSilver in SwordFightSkillFx.Board).
    /// Menu ChessFight/VFX/Bishop/Build writes the generated mesh and materials, the prefabs of Prefabs/Bishop built so far
    /// and their names in Resources/VfxSets/Bishop; play them in the VFX workbench (piece 4) or take frames of them with
    /// VfxCapture. Values tuned in a prefab's parts are kept when it is built again; a material is only made when
    /// missing, so its tuned colour stays too. Built so far: F1, the second sword forming in his left hand (F_Draw, R194);
    /// F3, the light up the blades as he draws them back (F_Windup, R188); F4, the flash at his hands and the air split as he lets them go (F_Throw, R191); F5, the two swords on their
    /// way out (F_Flight, R181); F6, the X where the swords cross (F_Cross); F7, whoever it strikes (F_Hit, R178); F8, the
    /// swords on their way back (F_Return, R186). ChessFight/VFX/Bishop/Build F1 (draw only), Build F3 (windup only), Build F4 (throw only),
    /// Build F5 (flight only) and Build F8 (return only) build that layer alone and leave the other prefabs as they are.
    /// </summary>
    public static class BishopVfxBuilder
    {
        const string SetPath = "Assets/Resources/VfxSets/Bishop.asset";
        const string CrossPrefab = VfxAssets.Root + "/Prefabs/Bishop/06_Bishop_F_Cross.prefab";
        const string HitPrefab = VfxAssets.Root + "/Prefabs/Bishop/07_Bishop_F_Hit.prefab";
        const string FlightPrefab = VfxAssets.Root + "/Prefabs/Bishop/05_Bishop_F_Flight.prefab";
        const string ReturnPrefab = VfxAssets.Root + "/Prefabs/Bishop/08_Bishop_F_Return.prefab";
        const string WindupPrefab = VfxAssets.Root + "/Prefabs/Bishop/03_Bishop_F_Windup.prefab";
        const string ThrowPrefab = VfxAssets.Root + "/Prefabs/Bishop/04_Bishop_F_Throw.prefab";
        const string DrawPrefab = VfxAssets.Root + "/Prefabs/Bishop/01_Bishop_F_Draw.prefab";
        const string SlashMaterial = VfxAssets.Root + "/Materials/M_Bishop_Slash.mat";
        const string GlintMaterial = VfxAssets.Root + "/Materials/M_Bishop_Glint.mat";
        const string CutMaterial = VfxAssets.Root + "/Materials/M_Bishop_Cut.mat";
        const string SparkMaterial = VfxAssets.Root + "/Materials/M_Bishop_Spark.mat";
        const string TrailMaterial = VfxAssets.Root + "/Materials/M_Bishop_Trail.mat";
        const string SpinMaterial = VfxAssets.Root + "/Materials/M_Bishop_Spin.mat";
        const string ReturnMaterial = VfxAssets.Root + "/Materials/M_Bishop_Return.mat";
        const string ChargeMaterial = VfxAssets.Root + "/Materials/M_Bishop_Charge.mat";
        const string TipMaterial = VfxAssets.Root + "/Materials/M_Bishop_TipGlint.mat";
        const string PreviewMaterial = VfxAssets.Root + "/Materials/M_Bishop_PreviewSteel.mat";
        const string ReleaseMaterial = VfxAssets.Root + "/Materials/M_Bishop_Release.mat";
        const string SplitMaterial = VfxAssets.Root + "/Materials/M_Bishop_Split.mat";
        const string FormMaterial = VfxAssets.Root + "/Materials/M_Bishop_Form.mat";
        const string ShardMaterial = VfxAssets.Root + "/Materials/M_Bishop_Shard.mat";
        const string GlintMesh = VfxAssets.Root + "/Meshes/SM_BillboardQuad.asset";
        /// <summary>His sword model (SwordFightPawn.UpdateSwordModel loads it from Resources/SwordFightSwords), drawn a fifth
        /// bigger than modelled, his hand just above its pommel's knob: for the preview's copy of it (F3).</summary>
        const string BishopSword = "Assets/Resources/SwordFightSwords/Bishop.fbx";
        const float SwordSize = 1.2f, SwordKnob = .095f;
        const string SlashRight = "F6 Slash R", SlashLeft = "F6 Slash L", GlintPart = "F6 Glint";
        const string CutRight = "F7 Cut R", CutLeft = "F7 Cut L", FlashPart = "F7 Flash", SparkPart = "F7 Sparks", StreakPart = "F7 Streaks";
        const string FlightRight = "F5 Sword R", FlightLeft = "F5 Sword L";
        const string ReturnRight = "F8 Sword R", ReturnLeft = "F8 Sword L";
        const string WindupRight = "F3 Blade R", WindupLeft = "F3 Blade L";
        const string ThrowRight = "F4 Hand R", ThrowLeft = "F4 Hand L";
        const string DrawLeft = "F1 Blade L", DrawRight = "F1 Blade R";
        /// <summary>How many shards fly in to make the second sword (F1).</summary>
        const int Shards = 44;
        /// <summary>How high over the point the swords cross (SwordFightSkillParams.bishopCrossHeight).</summary>
        const float CrossHeight = .55f;
        /// <summary>How far the X cut is drawn in front of the struck piece's chest, toward the bishop: out of the body, on
        /// the side the blow came from.</summary>
        const float CutOut = .3f;
        static readonly Color BishopSilver = new Color(.76f, .79f, .83f);

        [MenuItem("ChessFight/VFX/Bishop/Build")]
        public static void Build()
        {
            // In Play mode the layers' Awake runs as they are made and what it does is saved into the prefab (VFX1 found a
            // flash quad saved lit that way): build in edit mode only.
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the bishop effects are built in edit mode.");
                return;
            }
            var draw = BuildDraw();
            var windup = BuildWindup();
            var release = BuildThrow();
            var flight = BuildFlight();
            var cross = BuildCross();
            var hit = BuildHit();
            var back = BuildReturn();
            AssetDatabase.SaveAssets();
            Selection.activeObject = back;
            Debug.Log("[VFX] Bishop built: F1 the second sword forming in his left hand (F_Draw), F3 the light up the blades as he draws them back (F_Windup), F4 the flash at his hands as he lets them go (F_Throw), F5 the swords on their way out (F_Flight), F6 the X where the swords cross (F_Cross), F7 whoever it strikes (F_Hit), F8 the swords on their way back (F_Return). Workbench: piece 4, "
                + draw.name + ", " + windup.name + ", " + release.name + ", " + flight.name + ", " + cross.name + ", " + hit.name + ", " + back.name);
        }

        /// <summary>The menu is greyed out in Play mode.</summary>
        [MenuItem("ChessFight/VFX/Bishop/Build", true)]
        static bool CanBuild() => !EditorApplication.isPlaying;

        /// <summary>F1 alone (R194): the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/VFX/Bishop/Build F1 (draw only)")]
        public static void BuildF1()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the bishop effects are built in edit mode.");
                return;
            }
            var draw = BuildDraw();
            AssetDatabase.SaveAssets();
            Selection.activeObject = draw;
            Debug.Log("[VFX] Bishop F1 built: the second sword forming in his left hand (F_Draw). Workbench: piece 4, " + draw.name);
        }

        [MenuItem("ChessFight/VFX/Bishop/Build F1 (draw only)", true)]
        static bool CanBuildF1() => !EditorApplication.isPlaying;

        /// <summary>F3 alone (R188): the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/VFX/Bishop/Build F3 (windup only)")]
        public static void BuildF3()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the bishop effects are built in edit mode.");
                return;
            }
            var windup = BuildWindup();
            AssetDatabase.SaveAssets();
            Selection.activeObject = windup;
            Debug.Log("[VFX] Bishop F3 built: the light up the blades as he draws them back (F_Windup). Workbench: piece 4, " + windup.name);
        }

        [MenuItem("ChessFight/VFX/Bishop/Build F3 (windup only)", true)]
        static bool CanBuildF3() => !EditorApplication.isPlaying;

        /// <summary>F4 alone (R191): the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/VFX/Bishop/Build F4 (throw only)")]
        public static void BuildF4()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the bishop effects are built in edit mode.");
                return;
            }
            var release = BuildThrow();
            AssetDatabase.SaveAssets();
            Selection.activeObject = release;
            Debug.Log("[VFX] Bishop F4 built: the flash at his hands and the air split as he lets them go (F_Throw). Workbench: piece 4, " + release.name);
        }

        [MenuItem("ChessFight/VFX/Bishop/Build F4 (throw only)", true)]
        static bool CanBuildF4() => !EditorApplication.isPlaying;

        /// <summary>F5 alone (R181): the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/VFX/Bishop/Build F5 (flight only)")]
        public static void BuildF5()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the bishop effects are built in edit mode.");
                return;
            }
            var flight = BuildFlight();
            AssetDatabase.SaveAssets();
            Selection.activeObject = flight;
            Debug.Log("[VFX] Bishop F5 built: the swords on their way out (F_Flight). Workbench: piece 4, " + flight.name);
        }

        [MenuItem("ChessFight/VFX/Bishop/Build F5 (flight only)", true)]
        static bool CanBuildF5() => !EditorApplication.isPlaying;

        /// <summary>F8 alone (R186): the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/VFX/Bishop/Build F8 (return only)")]
        public static void BuildF8()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the bishop effects are built in edit mode.");
                return;
            }
            var back = BuildReturn();
            AssetDatabase.SaveAssets();
            Selection.activeObject = back;
            Debug.Log("[VFX] Bishop F8 built: the swords on their way back (F_Return). Workbench: piece 4, " + back.name);
        }

        [MenuItem("ChessFight/VFX/Bishop/Build F8 (return only)", true)]
        static bool CanBuildF8() => !EditorApplication.isPlaying;

        // ---------------------------------------------------------------- F1: the second sword

        /// <summary>
        /// F1, the second sword (the cue starts as he calls the skill and the game puts a sword in his empty left hand):
        /// small silver shards fly in from the air round it and land on the blade from where it starts up to the tip in
        /// 0.3 s, the first 0.1 s in; the part of the blade they have not reached yet is light, a crisp glint rides the
        /// forming edge and flares at the tip as the last one lands, then it is plain steel. The sword is the game's: in
        /// the game the part follows the drawn left blade (SwordFightSkillFx.BishopDraw); in the workbench and the capture
        /// tool it holds its own copy of his sword the way the game does while he aims. The root is on the floor where he
        /// stands, its +Z his way.
        /// </summary>
        static GameObject BuildDraw()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(DrawPrefab);
            var root = new GameObject("01_Bishop_F_Draw");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = .6f;
            // The sword all light with the shards on their way, landing at the start, half way, most of the way, the tip's
            // flare, plain steel.
            cue.captureTimes = new[] { .02f, .08f, .14f, .22f, .32f, .4f, .5f };

            var form = Material(FormMaterial, "ChessFight/VFX/Blade Charge Additive", BishopSilver,
                ("_CoreBoost", 1.4f), ("_Base", .75f), ("_TipRise", 1f), ("_CoreWidth", .2f), ("_GlowFalloff", 1.4f),
                ("_Streaks", 18f), ("_StreakSharp", 10f), ("_StreakMix", .75f));
            var shard = Material(ShardMaterial, "ChessFight/VFX/Glint Additive", Color.Lerp(BishopSilver, Color.white, .5f),
                ("_Core", .18f), ("_Falloff", 5f), ("_RayThin", 30f), ("_ShortRays", .3f), ("_RayStrength", .5f));
            // F4's crisp star at his hands, as it is (not changed here).
            var tip = Material(ReleaseMaterial, "ChessFight/VFX/Glint Additive", Color.Lerp(BishopSilver, Color.white, .6f),
                ("_RayThin", 60f), ("_RayStrength", 1.2f), ("_ShortRays", .5f), ("_Core", .1f));
            var steel = PreviewSteel();
            Draw(root, DrawLeft, 1, form, shard, tip, steel, old);
            // 10-28 (사용자 "비숍 f 누를 때 왼쪽 칼에만 vfx가 적용 돼 양쪽 칼 vfx 적용 되게 해줘"): the same on the sword in his
            // right hand, its tuned values taken from the left's (a first build has no right part of its own to keep).
            Draw(root, DrawRight, 0, form, shard, tip, steel, old);
            if (old == null || old.transform.Find(DrawRight) == null)
            {
                var right = root.transform.Find(DrawRight).GetComponent<VfxBladeForm>();
                EditorUtility.CopySerialized(root.transform.Find(DrawLeft).GetComponent<VfxBladeForm>(), right);
                right.hand = 0;
            }

            var prefab = VfxAssets.Prefab(DrawPrefab, root);
            Register("F_Draw", prefab);
            return prefab;
        }

        /// <summary>The blade still to form (a view-facing LineRenderer laid along it by <see cref="VfxBladeForm"/>), the
        /// shards and the glint (billboard quads) and the preview's copy of his sword.</summary>
        static void Draw(GameObject root, string name, int hand, Material form, Material shard, Material tipMat, Material steel, GameObject old)
        {
            var go = Part(root, name, Vector3.zero);
            var view = go.AddComponent<LineRenderer>();
            Renderer(view, form);
            view.useWorldSpace = false;
            view.alignment = LineAlignment.View;
            view.textureMode = LineTextureMode.Stretch;
            view.numCapVertices = 0;
            view.numCornerVertices = 0;
            view.positionCount = 0;
            // Narrowing toward the tip as the blade does.
            view.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.7f, .85f), new Keyframe(1f, .3f));
            var quad = VfxAssets.SaveMesh(GlintMesh, BillboardQuad());
            var glint = Part(go, VfxBladeForm.GlintName, Vector3.zero);
            glint.AddComponent<MeshFilter>().sharedMesh = quad;
            Renderer(glint.AddComponent<MeshRenderer>(), tipMat);
            for (int i = 0; i < Shards; i++)
            {
                var bit = Part(go, VfxBladeForm.ShardPrefix + i.ToString("00"), Vector3.zero);
                bit.AddComponent<MeshFilter>().sharedMesh = quad;
                Renderer(bit.AddComponent<MeshRenderer>(), shard);
            }
            PreviewSword(go, steel);
            var play = go.AddComponent<VfxBladeForm>();
            Keep(old, name, play);
            play.hand = hand;
        }

        // ---------------------------------------------------------------- F3: the draw-back

        /// <summary>
        /// F3, the draw-back (0.2 s; the cue starts as he lets go of the aim and begins to draw both swords up and back over
        /// his shoulders): along each blade silver light runs up from the guard to the tip, a brighter band at its edge, the
        /// blade brighter toward the tip, and as it gets there the tip lights up with a soft glint. He lets go with the
        /// blades lit and the tips brightest; the light flares a little and dies away on the flying swords in 0.15 s (F4's
        /// flash at his hands and F5's trails take over). The swords are the game's: in the game each part follows its
        /// drawn blade (SwordFightSkillFx.BishopWindup); in the workbench and the capture tool each draws its own copy of
        /// the game's draw-back on a copy of his sword. The root is on the floor where he stands, its +Z his way.
        /// </summary>
        static GameObject BuildWindup()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(WindupPrefab);
            var root = new GameObject("03_Bishop_F_Windup");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = .5f;
            // The light a quarter, half and most of the way up, at the tip, as he lets go, flaring off, dying away.
            cue.captureTimes = new[] { .04f, .08f, .12f, .16f, .2f, .25f, .32f };

            var charge = Material(ChargeMaterial, "ChessFight/VFX/Blade Charge Additive", BishopSilver);
            var tip = Material(TipMaterial, "ChessFight/VFX/Glint Additive", Color.Lerp(BishopSilver, Color.white, .6f),
                ("_RayStrength", .2f), ("_ShortRays", .25f), ("_RayThin", 60f));
            var steel = PreviewSteel();
            Windup(root, WindupRight, 0, charge, tip, steel, old);
            Windup(root, WindupLeft, 1, charge, tip, steel, old);

            var prefab = VfxAssets.Prefab(WindupPrefab, root);
            Register("F_Windup", prefab);
            return prefab;
        }

        /// <summary>One blade's light (a view-facing LineRenderer laid along it by <see cref="VfxBladeCharge"/>), its tip's
        /// glint (the billboard quad) and the preview's copy of his sword.</summary>
        static void Windup(GameObject root, string name, int hand, Material charge, Material tipMat, Material steel, GameObject old)
        {
            var go = Part(root, name, Vector3.zero);
            var view = go.AddComponent<LineRenderer>();
            Renderer(view, charge);
            view.useWorldSpace = false;
            view.alignment = LineAlignment.View;
            view.textureMode = LineTextureMode.Stretch;
            view.numCapVertices = 0;
            view.numCornerVertices = 0;
            view.positionCount = 0;
            // Narrowing toward the tip as the blade does, so it reads as a lit blade and not a bar.
            view.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.7f, .85f), new Keyframe(1f, .3f));
            var tip = Part(go, VfxBladeCharge.TipName, Vector3.zero);
            tip.AddComponent<MeshFilter>().sharedMesh = VfxAssets.SaveMesh(GlintMesh, BillboardQuad());
            Renderer(tip.AddComponent<MeshRenderer>(), tipMat);
            PreviewSword(go, steel);
            var play = go.AddComponent<VfxBladeCharge>();
            Keep(old, name, play);
            play.hand = hand;
        }

        /// <summary>A copy of his sword for the workbench and the capture tool (the game hides it): his sword model under
        /// its grip as the game places it (SwordFightPawn.UpdateSwordModel: up the blade so the hand is round the grip's
        /// lower end, turned so the blade runs along +Z, a fifth bigger), in plain steel.</summary>
        static void PreviewSword(GameObject part, Material steel)
        {
            var holder = Part(part, VfxBladeCharge.PreviewName, Vector3.zero);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(BishopSword);
            if (model == null)
            {
                Debug.LogWarning("[VFX] No sword model at " + BishopSword + ": the F3 preview has no sword to light.");
                return;
            }
            var copy = UnityEngine.Object.Instantiate(model, holder.transform, false);
            copy.name = "Sword model";
            var mesh = model.GetComponentInChildren<MeshFilter>();
            float below = mesh != null && mesh.sharedMesh != null ? -mesh.sharedMesh.bounds.min.y : .1f;
            copy.transform.SetLocalPositionAndRotation(new Vector3(0f, 0f, Mathf.Max(0f, below - SwordKnob) * SwordSize), Quaternion.Euler(90f, 0f, 0f));
            copy.transform.localScale = Vector3.one * SwordSize;
            foreach (var c in copy.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var a in copy.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(a);
            foreach (var view in copy.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[view.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = steel;
                view.sharedMaterials = mats;
                view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                view.receiveShadows = false;
            }
        }

        /// <summary>Plain steel for the preview's sword, as the game draws the sword models (SwordFightPawn.ModelPlain,
        /// smoothness 0.45); made only when missing.</summary>
        static Material PreviewSteel()
        {
            var mat = VfxAssets.Material(PreviewMaterial, "Universal Render Pipeline/Lit", out bool made);
            if (made)
            {
                mat.SetColor("_BaseColor", new Color(.8f, .8f, .8f));
                mat.SetFloat("_Smoothness", .45f);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- F4: the throw

        /// <summary>
        /// F4, the throw (the cue starts as the swords leave his hands, up over his shoulders): at each hand a crisp silver
        /// star gone in 0.09 s, and the air split by a thin bright straight line from that point to its sword as it flies
        /// off, brightest at the sword, held 0.05 s and then closing up into the sword as it fades in 0.1 s (F3's light
        /// dies away on the blades meanwhile and F5's trail takes over). The swords are the game's: in the game each part
        /// starts at the hand that let go and follows its sword's drawn copy (SwordFightSkillFx.BishopRelease); in the
        /// workbench and the capture tool each throws its own copy of the game's way, 4.2 m in 0.3 s, on a copy of his
        /// sword. The root is on the floor where he stands, its +Z his way.
        /// </summary>
        static GameObject BuildThrow()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(ThrowPrefab);
            var root = new GameObject("04_Bishop_F_Throw");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = .4f;
            // As he lets go, the star at its brightest, going, gone; the line held, closing up, gone.
            cue.captureTimes = new[] { 0f, .02f, .04f, .06f, .09f, .12f, .16f };

            var flash = Material(ReleaseMaterial, "ChessFight/VFX/Glint Additive", Color.Lerp(BishopSilver, Color.white, .6f),
                ("_RayThin", 60f), ("_RayStrength", 1.2f), ("_ShortRays", .5f), ("_Core", .1f));
            var split = Material(SplitMaterial, "ChessFight/VFX/Slash Additive", BishopSilver,
                ("_CoreBoost", 2f), ("_CoreWidth", .3f), ("_HeadGlow", 1.5f), ("_HeadLength", .35f), ("_TipFade", .15f), ("_DepthFade", .03f));
            var steel = PreviewSteel();
            Throw(root, ThrowRight, 0, split, flash, steel, old);
            Throw(root, ThrowLeft, 1, split, flash, steel, old);

            var prefab = VfxAssets.Prefab(ThrowPrefab, root);
            Register("F_Throw", prefab);
            return prefab;
        }

        /// <summary>One hand's split (a view-facing LineRenderer from the hand to the sword, laid by
        /// <see cref="VfxSwordRelease"/>), its flash (the billboard quad) and the preview's copy of his sword.</summary>
        static void Throw(GameObject root, string name, int hand, Material split, Material flash, Material steel, GameObject old)
        {
            var go = Part(root, name, Vector3.zero);
            var view = go.AddComponent<LineRenderer>();
            Renderer(view, split);
            view.useWorldSpace = false;
            view.alignment = LineAlignment.View;
            view.textureMode = LineTextureMode.Stretch;
            view.numCapVertices = 0;
            view.numCornerVertices = 0;
            view.positionCount = 0;
            // Hair-thin where it leaves his hand, full at the sword.
            view.widthCurve = new AnimationCurve(new Keyframe(0f, .35f), new Keyframe(1f, 1f));
            var star = Part(go, VfxSwordRelease.FlashName, Vector3.zero);
            star.AddComponent<MeshFilter>().sharedMesh = VfxAssets.SaveMesh(GlintMesh, BillboardQuad());
            Renderer(star.AddComponent<MeshRenderer>(), flash);
            PreviewSword(go, steel);
            var play = go.AddComponent<VfxSwordRelease>();
            Keep(old, name, play);
            play.hand = hand;
        }

        // ---------------------------------------------------------------- F5: the swords' flight

        /// <summary>
        /// F5, the two swords on their way out (from his hands to the X, 0.28 to 0.6 s; the cue starts as they leave his
        /// hands): round each, where it spins flat six times a second, a disc of fine silver streaks brightest right behind
        /// the blade, and behind it a trail along its curved way, bright at the sword and gone 1.6 m back. The disc goes as
        /// the sword turns into its cut and the trail dies away into the sword as the X begins (F6 takes over). The swords
        /// are the game's: in the game each part follows its sword's drawn copy (SwordFightSkillFx.BishopFlight); in the
        /// workbench and the capture tool they fly their own copy of the game's way, 4.2 m in 0.3 s. The root is on the
        /// floor where he stands, its +Z his way.
        /// </summary>
        static GameObject BuildFlight()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(FlightPrefab);
            var root = new GameObject("05_Bishop_F_Flight");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = .8f;
            // Off his hands, out wide, curving in, turning into the cut, the trail drawing in as the X begins.
            cue.captureTimes = new[] { .02f, .06f, .1f, .15f, .2f, .26f, .34f };

            var trail = Material(TrailMaterial, "ChessFight/VFX/Trail Additive", BishopSilver);
            var spin = Material(SpinMaterial, "ChessFight/VFX/Spin Blur Additive", BishopSilver);
            Flight(root, FlightRight, 0, trail, spin, old);
            Flight(root, FlightLeft, 1, trail, spin, old);

            var prefab = VfxAssets.Prefab(FlightPrefab, root);
            Register("F_Flight", prefab);
            return prefab;
        }

        /// <summary>One sword's trail (a view-facing LineRenderer laid out by <see cref="VfxSwordFlight"/>) with its blur
        /// disc under it (the billboard quad, here laid in the plane the sword spins in).</summary>
        static void Flight(GameObject root, string name, int hand, Material trail, Material spin, GameObject old)
        {
            var go = Part(root, name, Vector3.zero);
            var view = go.AddComponent<LineRenderer>();
            Renderer(view, trail);
            view.useWorldSpace = false;
            view.alignment = LineAlignment.View;
            view.textureMode = LineTextureMode.Stretch;
            view.numCapVertices = 0;
            view.numCornerVertices = 0;
            view.positionCount = 0;
            var disc = Part(go, VfxSwordFlight.DiscName, Vector3.zero);
            disc.AddComponent<MeshFilter>().sharedMesh = VfxAssets.SaveMesh(GlintMesh, BillboardQuad());
            Renderer(disc.AddComponent<MeshRenderer>(), spin);
            var play = go.AddComponent<VfxSwordFlight>();
            Keep(old, name, play);
            play.hand = hand;
        }

        // ---------------------------------------------------------------- F8: the swords' way back

        /// <summary>
        /// F8, the two swords on their way back (from the end of their cut to his hands, 0.85 s; the cue starts at the X,
        /// half the cut before they turn back): behind each, along its loose, wobbling curve, a thin faint trail, bright at
        /// the sword and gone 2.6 m back, swaying a very little at its far end, no blur disc (they slow as they come back). It
        /// draws in to the sword as the sword drops into his hand (F9 takes over). In the game each part follows its
        /// sword's drawn copy (SwordFightSkillFx.BishopReturn); in the workbench and the capture tool they fly their own
        /// copy of the game's way back. The root is on the floor where he stands, its +Z his way.
        /// </summary>
        static GameObject BuildReturn()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(ReturnPrefab);
            var root = new GameObject("08_Bishop_F_Return");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 1.3f;
            // Off the X, out on their loose curves, swinging round behind him, coming in, into his hands, drawn in.
            cue.captureTimes = new[] { .1f, .25f, .4f, .55f, .7f, .8f, .92f };

            // Its own material: softer, fainter streaks than the way out's (R186 review: five bold ones read as stripes).
            var trail = Material(ReturnMaterial, "ChessFight/VFX/Trail Additive", BishopSilver,
                ("_Streaks", 9f), ("_StreakMix", .45f), ("_CoreBoost", .8f), ("_GlowFalloff", 1.2f));
            Return(root, ReturnRight, 0, trail, old);
            Return(root, ReturnLeft, 1, trail, old);

            var prefab = VfxAssets.Prefab(ReturnPrefab, root);
            Register("F_Return", prefab);
            return prefab;
        }

        /// <summary>One sword's trail on its way back (a view-facing LineRenderer laid out by <see cref="VfxSwordFlight"/>
        /// with <see cref="VfxSwordFlight.back"/>), with no disc, with its values until the prefab's are tuned.</summary>
        static void Return(GameObject root, string name, int hand, Material trail, GameObject old)
        {
            var go = Part(root, name, Vector3.zero);
            var view = go.AddComponent<LineRenderer>();
            Renderer(view, trail);
            view.useWorldSpace = false;
            view.alignment = LineAlignment.View;
            view.textureMode = LineTextureMode.Stretch;
            view.numCapVertices = 0;
            view.numCornerVertices = 0;
            view.positionCount = 0;
            var play = go.AddComponent<VfxSwordFlight>();
            play.back = true;
            play.outTime = .85f;
            play.spin = 4.8f;
            // R186 review: 1.8 m by 0.14 swaying 0.06 every 0.8 m read short and wormy against the reference's long smooth
            // sweeps.
            play.length = 2.6f;
            play.width = .12f;
            play.endWidth = .1f;
            play.attach = 0f;
            play.intensity = 1.4f;
            play.fadeOut = .15f;
            play.waver = .02f;
            play.waverLength = 1.2f;
            Keep(old, name, play);
            play.hand = hand;
        }

        // ---------------------------------------------------------------- F6: the X

        /// <summary>
        /// F6, the face of the skill (0.6 s; the cue starts as the blades cross, halfway through their 0.14 s cut): two
        /// crescent slashes of light along the swords' cuts, each from high on its own side down to low on the other, an X
        /// about 2 m across and 1.35 m high that hangs in the air and dies away, and a star glint where they cross. The
        /// root is on the floor under the crossing, its +Z the bishop's way; the X is <see cref="CrossHeight"/> over it.
        /// </summary>
        static GameObject BuildCross()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(CrossPrefab);
            var root = new GameObject("06_Bishop_F_Cross");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = .8f;
            // As the blades cross, the cut drawn to its end, the X hanging, dying away.
            cue.captureTimes = new[] { 0f, .03f, .07f, .12f, .2f, .32f, .45f };

            var slash = Material(SlashMaterial, "ChessFight/VFX/Slash Additive", BishopSilver);
            var at = new Vector3(0f, CrossHeight, 0f);
            // His right sword cuts from high on his right down to his left, the left one the other way.
            Slash(root, SlashRight, slash, at, true, old);
            Slash(root, SlashLeft, slash, at, false, old);
            Glint(root, GlintPart, GlintMat(), at, old);

            var prefab = VfxAssets.Prefab(CrossPrefab, root);
            Register("F_Cross", prefab);
            return prefab;
        }

        // ---------------------------------------------------------------- F7: whoever it strikes

        /// <summary>
        /// F7, on each piece the X strikes (0.6 s from the blow): the X cut into his front as two long thin strokes of light
        /// along the blades' cuts, drawn in a blink, flashing and gone in 0.3 s, a hot X-shaped flash where they cross,
        /// fine silver sparks thrown out round the way he is pushed and a few thin streaks of air going that way. No ring.
        /// The root is at his chest, its +Z the way he is pushed (so the bishop is behind it); the cut is
        /// <see cref="CutOut"/> back toward the bishop. The game hangs it on his chest so it goes with him.
        /// </summary>
        static GameObject BuildHit()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(HitPrefab);
            var root = new GameObject("07_Bishop_F_Hit");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Target;
            cue.length = .6f;
            // The blow, the cut drawn and flashing, the sparks out, the cut dying, the last sparks falling.
            cue.captureTimes = new[] { 0f, .02f, .05f, .09f, .15f, .25f, .4f };
            cue.captureHeight = .9f;

            // Its own material: on a body the light has to fade only right at the wood, not the 0.1 m F6 fades by at the floor.
            var cut = Material(CutMaterial, "ChessFight/VFX/Slash Additive", BishopSilver, ("_DepthFade", .03f));
            var at = new Vector3(0f, 0f, -CutOut);
            Slash(root, CutRight, cut, at, true, old, Cut);
            Slash(root, CutLeft, cut, at, false, old, Cut);
            Glint(root, FlashPart, GlintMat(), at + new Vector3(0f, 0f, -.04f), old, Flash);

            var spark = Material(SparkMaterial, "ChessFight/VFX/Soft Glow Additive", Color.Lerp(BishopSilver, Color.white, .55f),
                ("_Intensity", 6f), ("_Falloff", 2f));
            var sparks = Part(root, SparkPart, at + new Vector3(0f, 0f, .02f));
            Sparks(sparks, spark);
            KeepParticles(old, SparkPart, sparks);
            var streaks = Part(root, StreakPart, new Vector3(0f, 0f, -.1f));
            Streaks(streaks, spark);
            KeepParticles(old, StreakPart, streaks);

            var prefab = VfxAssets.Prefab(HitPrefab, root);
            Register("F_Hit", prefab);
            return prefab;
        }

        /// <summary>A long thin, nearly straight stroke of the X cut, reaching well past his outline as the reference's rays
        /// do (R178 review: 0.85 m by 0.07 read short and thick): drawn in 0.045 s, a flash, gone in 0.3 s.</summary>
        static void Cut(VfxSlash stroke)
        {
            stroke.length = 1.1f;
            stroke.lean = 42f;
            stroke.bend = .04f;
            stroke.depth = .02f;
            stroke.width = .055f;
            stroke.taper = 1.1f;
            stroke.segments = 16;
            stroke.sweep = .045f;
            stroke.preroll = 0f;
            stroke.duration = .3f;
            stroke.fadeOut = .75f;
            stroke.endWidth = .3f;
            stroke.flashIntensity = 3.5f;
            stroke.intensity = 1.8f;
            stroke.flashTime = .07f;
        }

        /// <summary>The hot flash where the strokes cross: the glint's long rays turned onto the diagonals (an X, where F6's
        /// star is a +), smaller and quicker.</summary>
        static void Flash(VfxGlint flash)
        {
            flash.duration = .14f;
            flash.intensity = 5f;
            flash.startSize = .6f;
            flash.endSize = .3f;
            flash.turn = 45f;
            flash.spin = 15f;
        }

        /// <summary>Sixty fine silver sparks thrown out round the way he is pushed, stretched along their way, falling and
        /// fading in under two thirds of a second (R184, 사용자 "상대 hit 할 때 파티클 더 강하게": twice as many as R178's
        /// thirty, faster, bigger and longer lived).</summary>
        static void Sparks(GameObject go, Material mat)
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = .1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.25f, .6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 12f);
            main.startSize = new ParticleSystem.MinMaxCurve(.016f, .034f);
            main.startColor = Color.white;
            main.gravityModifier = 1.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 96;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });
            var shape = ps.shape;
            // Round +Z, tipped up a little, 35 degrees about it: they spray the way he is pushed, as in the reference
            // (R178 review; at 55 they fanned out all round him, seen from behind the bishop).
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = .05f;
            shape.rotation = new Vector3(-15f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, .3f)));
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .5f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var view = go.GetComponent<ParticleSystemRenderer>();
            Renderer(view, mat);
            view.renderMode = ParticleSystemRenderMode.Stretch;
            view.velocityScale = .035f;
            view.lengthScale = 1f;
        }

        /// <summary>Twelve long thin streaks of air shot the way he is pushed from all round his outline, quick (R178 review:
        /// stronger, 0.55 to 0.8, and drawn twice as long for their width, 2 to 4, as they barely showed; R184: seven to
        /// twelve, full strength, a little thicker and drawn longer along their way, 0.06 to 0.1).</summary>
        static void Streaks(GameObject go, Material mat)
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
            main.maxParticles = 24;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
            var shape = ps.shape;
            // A box about his outline; a box sends them straight along +Z.
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1f, .9f, .1f);
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var view = go.GetComponent<ParticleSystemRenderer>();
            Renderer(view, mat);
            view.renderMode = ParticleSystemRenderMode.Stretch;
            view.velocityScale = .1f;
            view.lengthScale = 4f;
        }

        // ---------------------------------------------------------------- parts

        /// <summary>One crescent slash (a view-facing LineRenderer laid out by <see cref="VfxSlash"/>) at
        /// <paramref name="at"/>, with <paramref name="first"/>'s values until the prefab's are tuned.</summary>
        static void Slash(GameObject root, string name, Material mat, Vector3 at, bool leftward, GameObject old, Action<VfxSlash> first = null)
        {
            var go = Part(root, name, at);
            var view = go.AddComponent<LineRenderer>();
            Renderer(view, mat);
            view.useWorldSpace = false;
            view.alignment = LineAlignment.View;
            view.textureMode = LineTextureMode.Stretch;
            view.numCapVertices = 0;
            view.numCornerVertices = 0;
            var play = go.AddComponent<VfxSlash>();
            first?.Invoke(play);
            play.leftward = leftward;
            Keep(old, name, play);
            play.Shape();
        }

        /// <summary>A star glint at <paramref name="at"/>: a quad the glint shader turns to the camera, with
        /// <paramref name="first"/>'s values until the prefab's are tuned.</summary>
        static void Glint(GameObject root, string name, Material mat, Vector3 at, GameObject old, Action<VfxGlint> first = null)
        {
            var go = Part(root, name, at);
            go.AddComponent<MeshFilter>().sharedMesh = VfxAssets.SaveMesh(GlintMesh, BillboardQuad());
            Renderer(go.AddComponent<MeshRenderer>(), mat);
            var play = go.AddComponent<VfxGlint>();
            first?.Invoke(play);
            Keep(old, name, play);
            go.transform.localScale = Vector3.one * play.startSize;
        }

        /// <summary>The glint material F6 and F7 share.</summary>
        static Material GlintMat() =>
            Material(GlintMaterial, "ChessFight/VFX/Glint Additive", Color.Lerp(BishopSilver, Color.white, .6f));

        /// <summary>A 1 x 1 m quad in XY, UVs 0..1, for a shader that turns it to the camera: its bounds are a 1 m cube so
        /// it is not culled at any turn.</summary>
        static Mesh BillboardQuad()
        {
            var mesh = new Mesh { name = "SM_BillboardQuad" };
            mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0f), new Vector3(.5f, -.5f, 0f), new Vector3(-.5f, .5f, 0f), new Vector3(.5f, .5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            return mesh;
        }

        // ---------------------------------------------------------------- shared

        static GameObject Part(GameObject root, string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = at;
            return go;
        }

        static void Renderer(Renderer view, Material mat)
        {
            view.sharedMaterial = mat;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            view.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>The values tuned on the old prefab's layer, copied onto the rebuilt one.</summary>
        static void Keep<T>(GameObject old, string layer, T fresh) where T : Component
        {
            var part = old != null ? old.transform.Find(layer) : null;
            var tuned = part != null ? part.GetComponent<T>() : null;
            if (tuned != null) EditorUtility.CopySerialized(tuned, fresh);
        }

        static void KeepParticles(GameObject old, string layer, GameObject fresh)
        {
            Keep(old, layer, fresh.GetComponent<ParticleSystem>());
            Keep(old, layer, fresh.GetComponent<ParticleSystemRenderer>());
        }

        /// <summary>The material at the path on <paramref name="shader"/>; its colour and <paramref name="first"/> values
        /// only when it is first made (then they are the user's).</summary>
        static Material Material(string path, string shader, Color colour, params (string name, float value)[] first)
        {
            var mat = VfxAssets.Material(path, shader, out bool made);
            if (made)
            {
                mat.SetColor("_Color", colour);
                foreach (var (name, value) in first) mat.SetFloat(name, value);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Lists <paramref name="prefab"/> under <paramref name="key"/> in the bishop's set (made if missing), the
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
