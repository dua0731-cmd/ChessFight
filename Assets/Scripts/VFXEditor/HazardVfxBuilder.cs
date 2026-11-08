using UnityEditor;
using UnityEngine;

namespace ChessFight.VFX.Editor
{
    /// <summary>
    /// The course obstacles' effects, made the new way, one layer a prefab (R341, 11-04; DECISIONS FX7), first the grand
    /// stair hall's falling knights (승규 "말 장애물 예고 실제 타격 vfx", the hall only). They live in Prefabs/Common so the
    /// workbench lists them under every piece, and in Resources/VfxSets/Hazards under the names the course code plays them
    /// by (PawnRush/FallingPieceFx). Menu ChessFight/VFX/Hazard/Build builds every layer so far; Build W1 (falling piece
    /// warning) builds W1, the floor warning that fills to its rim as the piece lands (FallingPiece_Warn), and Build W2
    /// (falling piece fall) builds W2, the shadow straight under the piece and the speed lines above it as it falls
    /// (FallingPiece_Fall), and Build I1 (falling piece landing) builds I1, the flash, dust, stone chips and sparks as it
    /// lands (FallingPiece_Land; its first values are VfxHazardLooks'), and Build I2 (falling piece shockwave) builds I2,
    /// the shockwave at exactly the hit's reach, a band on the floor and a low wall of light (FallingPiece_Shock), and
    /// Build I3 (falling piece after) builds I3, the glowing cracks that cool and fade, embers rising out of them and a
    /// puff as the piece goes (FallingPiece_After), and Build V1 (vent wind) builds V1, a royal fan's wind - a swirl on
    /// its face, streaks of air and dust running out along its front (Vent_Wind, R351; PawnRush/VentWindFx plays it), and
    /// Build K1 (knight charge warning) builds K1, a guard knight's warnings in standing light - a ring curtain where it
    /// lands, curtains and floating arrowheads down its lane (Knight_Warn, R354; PawnRush/CavalryWarnFx plays it). Values tuned in a
    /// prefab's parts are kept when it is built again; a material is only made when missing, so its tuned colours stay too.
    /// </summary>
    public static class HazardVfxBuilder
    {
        const string SetPath = "Assets/Resources/VfxSets/Hazards.asset";
        const string KnightPrefab = VfxAssets.Root + "/Prefabs/Common/31_Hazard_Knight_Warn.prefab";
        const string KnightLightMaterial = VfxAssets.Root + "/Materials/M_Hazard_WarnLight.mat";
        const string KnightLandingPart = "K1 Landing", KnightLanePart = "K1 Lane";

        /// <summary>K1 alone: the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/Development/VFX/Hazard/Build K1 (knight charge warning)")]
        public static void BuildK1()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the hazard effects are built in edit mode.");
                return;
            }
            var knight = BuildKnight();
            AssetDatabase.SaveAssets();
            Selection.activeObject = knight;
            Debug.Log("[VFX] Hazard K1 built: the guard knight's landing and charge warnings (Knight_Warn). Workbench: any piece, " + knight.name);
        }

        [MenuItem("ChessFight/Development/VFX/Hazard/Build K1 (knight charge warning)", true)]
        static bool CanBuildK1() => !EditorApplication.isPlaying;

        static GameObject BuildKnight()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(KnightPrefab);
            var root = new GameObject("31_Hazard_Knight_Warn");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 4.3f;
            // The warning, mid-leap, landing, the lane full and blinking, mid-charge, nearly gone.
            cue.captureTimes = new[] { .3f, 1.5f, 2.3f, 2.9f, 3.6f, 4.0f };

            // Standing light on one material (승규 "데칼 말고"); the parts make their own meshes.
            var material = Material(KnightLightMaterial, "ChessFight/VFX/Warn Light");
            var landing = new GameObject(KnightLandingPart);
            landing.transform.SetParent(root.transform, false);
            landing.AddComponent<MeshFilter>();
            var landingView = landing.AddComponent<MeshRenderer>();
            landingView.sharedMaterial = material;
            Quiet(landingView);
            Keep(old, KnightLandingPart, landing.AddComponent<VfxRingGlow>());

            var lane = new GameObject(KnightLanePart);
            lane.transform.SetParent(root.transform, false);
            lane.AddComponent<MeshFilter>();
            var laneView = lane.AddComponent<MeshRenderer>();
            laneView.sharedMaterial = material;
            Quiet(laneView);
            Keep(old, KnightLanePart, lane.AddComponent<VfxLaneGlow>());

            var prefab = VfxAssets.Prefab(KnightPrefab, root);
            Register("Knight_Warn", prefab);
            return prefab;
        }
        const string WindPrefab = VfxAssets.Root + "/Prefabs/Common/30_Hazard_Vent_Wind.prefab";
        const string WindFlowMaterial = VfxAssets.Root + "/Materials/M_Hazard_WindFlow.mat";
        const string WindSwirlMaterial = VfxAssets.Root + "/Materials/M_Hazard_WindSwirl.mat";
        const string WindSwirlPart = "V1 Swirl";
        const string WarnPrefab = VfxAssets.Root + "/Prefabs/Common/20_Hazard_FallingPiece_Warn.prefab";
        const string FallPrefab = VfxAssets.Root + "/Prefabs/Common/21_Hazard_FallingPiece_Fall.prefab";
        const string LandPrefab = VfxAssets.Root + "/Prefabs/Common/22_Hazard_FallingPiece_Land.prefab";
        const string ShockPrefab = VfxAssets.Root + "/Prefabs/Common/23_Hazard_FallingPiece_Shock.prefab";
        const string AfterPrefab = VfxAssets.Root + "/Prefabs/Common/24_Hazard_FallingPiece_After.prefab";
        const string CrackMaterial = VfxAssets.Root + "/Materials/M_Hazard_Cracks.mat";
        const string CrackPart = "I3 Cracks";
        const string WarnMaterial = VfxAssets.Root + "/Materials/M_Hazard_WarnDecal.mat";
        const string ShadowMaterial = VfxAssets.Root + "/Materials/M_Hazard_DropShadow.mat";
        const string StreakMaterial = VfxAssets.Root + "/Materials/M_Hazard_FallStreak.mat";
        const string BurstMaterial = VfxAssets.Root + "/Materials/M_Hazard_Burst.mat";
        const string ShockDecalMaterial = VfxAssets.Root + "/Materials/M_Hazard_ShockDecal.mat";
        const string ShockWallMaterial = VfxAssets.Root + "/Materials/M_Hazard_ShockWall.mat";
        const string WarnPart = "W1 Decal", ShadowPart = "W2 Shadow", StreakPart = "W2 Streaks";
        const string ShockDecalPart = "I2 Decal", ShockWallPart = "I2 Wall";

        [MenuItem("ChessFight/Development/VFX/Hazard/Build")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the hazard effects are built in edit mode.");
                return;
            }
            var warn = BuildWarn();
            var fall = BuildFall();
            var land = BuildLand();
            var shock = BuildShock();
            var after = BuildAfter();
            var wind = BuildWind();
            BuildKnight();
            AssetDatabase.SaveAssets();
            Selection.activeObject = wind;
            Debug.Log("[VFX] Hazard built (and K1 the guard knight's warnings, Knight_Warn): W1 the falling piece's floor warning (FallingPiece_Warn), W2 its shadow and speed lines "
                + "as it falls (FallingPiece_Fall), I1 the flash, dust, chips and sparks as it lands (FallingPiece_Land), "
                + "I2 the shockwave at the hit's reach (FallingPiece_Shock), I3 the cooling cracks, embers and its going "
                + "(FallingPiece_After), V1 the royal fan's wind (Vent_Wind). Workbench: any piece, "
                + warn.name + ", " + fall.name + ", " + land.name + ", " + shock.name + ", " + after.name + ", " + wind.name);
        }

        [MenuItem("ChessFight/Development/VFX/Hazard/Build", true)]
        static bool CanBuild() => !EditorApplication.isPlaying;

        /// <summary>V1 alone: the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/Development/VFX/Hazard/Build V1 (vent wind)")]
        public static void BuildV1()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the hazard effects are built in edit mode.");
                return;
            }
            var wind = BuildWind();
            AssetDatabase.SaveAssets();
            Selection.activeObject = wind;
            Debug.Log("[VFX] Hazard V1 built: the royal fan's wind (Vent_Wind). Workbench: any piece, " + wind.name);
        }

        [MenuItem("ChessFight/Development/VFX/Hazard/Build V1 (vent wind)", true)]
        static bool CanBuildV1() => !EditorApplication.isPlaying;

        static GameObject BuildWind()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(WindPrefab);
            var root = new GameObject("30_Hazard_Vent_Wind");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Chest;
            cue.length = 4.6f;
            cue.captureHeight = 1.3f;
            // Stirring in the warning, the first streaks out, full flow, the last runs leaving, the swirl dying.
            cue.captureTimes = new[] { .4f, .75f, 1f, 1.5f, 2.6f, 3.9f, 4.3f };

            // Unity's quad on the fan's face (its z), sized by the part.
            var swirl = GameObject.CreatePrimitive(PrimitiveType.Quad);
            swirl.name = WindSwirlPart;
            Object.DestroyImmediate(swirl.GetComponent<Collider>());
            swirl.transform.SetParent(root.transform, false);
            var swirlView = swirl.GetComponent<MeshRenderer>();
            swirlView.sharedMaterial = Material(WindSwirlMaterial, "ChessFight/VFX/Wind Swirl");
            Quiet(swirlView);
            Keep(old, WindSwirlPart, swirl.AddComponent<VfxWindSwirl>());

            // The flow layers on one material, each one's look its own; their meshes are made by the parts.
            var material = Material(WindFlowMaterial, "ChessFight/VFX/Wind Flow");
            foreach (var layer in VfxHazardLooks.WindLayers)
            {
                var go = new GameObject(layer);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>();
                var view = go.AddComponent<MeshRenderer>();
                view.sharedMaterial = material;
                Quiet(view);
                var part = go.AddComponent<VfxWindFlow>();
                VfxHazardLooks.Apply(layer, part);
                Keep(old, layer, part);
            }

            var prefab = VfxAssets.Prefab(WindPrefab, root);
            Register("Vent_Wind", prefab);
            return prefab;
        }

        /// <summary>I3 alone: the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/Development/VFX/Hazard/Build I3 (falling piece after)")]
        public static void BuildI3()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the hazard effects are built in edit mode.");
                return;
            }
            var after = BuildAfter();
            AssetDatabase.SaveAssets();
            Selection.activeObject = after;
            Debug.Log("[VFX] Hazard I3 built: the falling piece's cooling cracks, embers and its going (FallingPiece_After). "
                + "Workbench: any piece, " + after.name);
        }

        [MenuItem("ChessFight/Development/VFX/Hazard/Build I3 (falling piece after)", true)]
        static bool CanBuildI3() => !EditorApplication.isPlaying;

        /// <summary>I2 alone: the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/Development/VFX/Hazard/Build I2 (falling piece shockwave)")]
        public static void BuildI2()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the hazard effects are built in edit mode.");
                return;
            }
            var shock = BuildShock();
            AssetDatabase.SaveAssets();
            Selection.activeObject = shock;
            Debug.Log("[VFX] Hazard I2 built: the falling piece's shockwave at the hit's reach (FallingPiece_Shock). "
                + "Workbench: any piece, " + shock.name);
        }

        [MenuItem("ChessFight/Development/VFX/Hazard/Build I2 (falling piece shockwave)", true)]
        static bool CanBuildI2() => !EditorApplication.isPlaying;

        /// <summary>I1 alone: the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/Development/VFX/Hazard/Build I1 (falling piece landing)")]
        public static void BuildI1()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the hazard effects are built in edit mode.");
                return;
            }
            var land = BuildLand();
            AssetDatabase.SaveAssets();
            Selection.activeObject = land;
            Debug.Log("[VFX] Hazard I1 built: the falling piece's flash, dust, chips and sparks as it lands (FallingPiece_Land). "
                + "Workbench: any piece, " + land.name);
        }

        [MenuItem("ChessFight/Development/VFX/Hazard/Build I1 (falling piece landing)", true)]
        static bool CanBuildI1() => !EditorApplication.isPlaying;

        /// <summary>W2 alone: the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/Development/VFX/Hazard/Build W2 (falling piece fall)")]
        public static void BuildW2()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the hazard effects are built in edit mode.");
                return;
            }
            var fall = BuildFall();
            AssetDatabase.SaveAssets();
            Selection.activeObject = fall;
            Debug.Log("[VFX] Hazard W2 built: the falling piece's shadow and speed lines (FallingPiece_Fall). Workbench: any piece, " + fall.name);
        }

        [MenuItem("ChessFight/Development/VFX/Hazard/Build W2 (falling piece fall)", true)]
        static bool CanBuildW2() => !EditorApplication.isPlaying;

        /// <summary>W1 alone: the other layers' prefabs are left as they are.</summary>
        [MenuItem("ChessFight/Development/VFX/Hazard/Build W1 (falling piece warning)")]
        public static void BuildW1()
        {
            // In Play mode a layer's Awake runs as it is made and what it does is saved into the prefab: edit mode only.
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VFX] Stop Play mode first: the hazard effects are built in edit mode.");
                return;
            }
            var warn = BuildWarn();
            AssetDatabase.SaveAssets();
            Selection.activeObject = warn;
            Debug.Log("[VFX] Hazard W1 built: the falling piece's floor warning (FallingPiece_Warn). Workbench: any piece, " + warn.name);
        }

        [MenuItem("ChessFight/Development/VFX/Hazard/Build W1 (falling piece warning)", true)]
        static bool CanBuildW1() => !EditorApplication.isPlaying;

        static GameObject BuildWarn()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(WarnPrefab);
            var root = new GameObject("20_Hazard_FallingPiece_Warn");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 2.4f;
            // Just shown, a quarter, half and three quarters filled, blinking, full as it lands, gone.
            cue.captureTimes = new[] { .1f, .5f, 1f, 1.5f, 1.8f, 2.03f, 2.2f };

            // Unity's cube: the decal box, one unit each way; the part sizes and lifts it.
            var decal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            decal.name = WarnPart;
            Object.DestroyImmediate(decal.GetComponent<Collider>());
            decal.transform.SetParent(root.transform, false);
            var view = decal.GetComponent<MeshRenderer>();
            view.sharedMaterial = Material(WarnMaterial, "ChessFight/VFX/Ground Warn Decal");
            Quiet(view);
            var part = decal.AddComponent<VfxGroundWarn>();
            Keep(old, WarnPart, part);

            var prefab = VfxAssets.Prefab(WarnPrefab, root);
            Register("FallingPiece_Warn", prefab);
            return prefab;
        }

        static GameObject BuildFall()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(FallPrefab);
            var root = new GameObject("21_Hazard_FallingPiece_Fall");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 3.1f;
            // Hanging (wide, faint), falling (a quarter, half, nearly down), landing, lying there, gone.
            cue.captureTimes = new[] { .5f, 1.6f, 1.8f, 1.95f, 2.04f, 2.5f, 2.8f };

            // Unity's cube: the shadow's decal box; the part sizes and lifts it.
            var shadow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shadow.name = ShadowPart;
            Object.DestroyImmediate(shadow.GetComponent<Collider>());
            shadow.transform.SetParent(root.transform, false);
            var shadowView = shadow.GetComponent<MeshRenderer>();
            shadowView.sharedMaterial = Material(ShadowMaterial, "ChessFight/VFX/Ground Shadow Decal");
            Quiet(shadowView);
            Keep(old, ShadowPart, shadow.AddComponent<VfxDropShadow>());

            // The streaks' mesh is made by the part when it is turned on.
            var streaks = new GameObject(StreakPart);
            streaks.transform.SetParent(root.transform, false);
            streaks.AddComponent<MeshFilter>();
            var streakView = streaks.AddComponent<MeshRenderer>();
            streakView.sharedMaterial = Material(StreakMaterial, "ChessFight/VFX/Fall Streak Additive");
            Quiet(streakView);
            Keep(old, StreakPart, streaks.AddComponent<VfxFallStreaks>());

            var prefab = VfxAssets.Prefab(FallPrefab, root);
            Register("FallingPiece_Fall", prefab);
            return prefab;
        }

        static GameObject BuildLand()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(LandPrefab);
            var root = new GameObject("22_Hazard_FallingPiece_Land");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 1.6f;
            // The bursts at 0.1 s: the flash at its height, sparks out, chips up, dust spreading, settling, gone.
            cue.captureTimes = new[] { .1f, .13f, .18f, .3f, .5f, .8f, 1.2f };

            // One material for every layer: each layer's look is its part's, set per renderer.
            var material = Material(BurstMaterial, "ChessFight/VFX/Burst Particles");
            foreach (var layer in VfxHazardLooks.LandLayers)
            {
                var go = new GameObject(layer);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>();
                var view = go.AddComponent<MeshRenderer>();
                view.sharedMaterial = material;
                Quiet(view);
                var part = go.AddComponent<VfxParticleBurst>();
                VfxHazardLooks.Apply(layer, part);
                Keep(old, layer, part);
            }

            var prefab = VfxAssets.Prefab(LandPrefab, root);
            Register("FallingPiece_Land", prefab);
            return prefab;
        }

        static GameObject BuildShock()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(ShockPrefab);
            var root = new GameObject("23_Hazard_FallingPiece_Shock");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 3.05f;
            // The shock runs from the landing (2.05 s) to its most reach (2.75 s).
            cue.captureTimes = new[] { 2.1f, 2.2f, 2.35f, 2.5f, 2.65f, 2.74f };

            // Unity's cube: the band's decal box; the part sizes and lifts it.
            var decal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            decal.name = ShockDecalPart;
            Object.DestroyImmediate(decal.GetComponent<Collider>());
            decal.transform.SetParent(root.transform, false);
            var decalView = decal.GetComponent<MeshRenderer>();
            decalView.sharedMaterial = Material(ShockDecalMaterial, "ChessFight/VFX/Shock Decal");
            Quiet(decalView);
            Keep(old, ShockDecalPart, decal.AddComponent<VfxShockDecal>());

            // The wall's ring is made by the part when it shows.
            var wall = new GameObject(ShockWallPart);
            wall.transform.SetParent(root.transform, false);
            wall.AddComponent<MeshFilter>();
            var wallView = wall.AddComponent<MeshRenderer>();
            wallView.sharedMaterial = Material(ShockWallMaterial, "ChessFight/VFX/Shock Wall Additive");
            Quiet(wallView);
            Keep(old, ShockWallPart, wall.AddComponent<VfxShockWall>());

            var prefab = VfxAssets.Prefab(ShockPrefab, root);
            Register("FallingPiece_Shock", prefab);
            return prefab;
        }

        static GameObject BuildAfter()
        {
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(AfterPrefab);
            var root = new GameObject("24_Hazard_FallingPiece_After");
            var cue = root.AddComponent<VfxCue>();
            cue.anchor = VfxCue.Anchor.Feet;
            cue.length = 4.6f;
            // From the landing (2.05 s): the cracks running hot, cooling, the piece going (2.75 s), embers, dark, fading.
            cue.captureTimes = new[] { 2.1f, 2.3f, 2.8f, 3.2f, 3.8f, 4.2f };

            // Unity's cube: the cracks' decal box; the part sizes and lifts it.
            var cracks = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cracks.name = CrackPart;
            Object.DestroyImmediate(cracks.GetComponent<Collider>());
            cracks.transform.SetParent(root.transform, false);
            var crackView = cracks.GetComponent<MeshRenderer>();
            crackView.sharedMaterial = Material(CrackMaterial, "ChessFight/VFX/Crack Decal");
            Quiet(crackView);
            Keep(old, CrackPart, cracks.AddComponent<VfxCrackDecal>());

            // The particle layers on the burst material, timed in the workbench to the obstacle's own landing and going.
            var material = Material(BurstMaterial, "ChessFight/VFX/Burst Particles");
            var drop = VfxDrop.Default;
            foreach (var layer in VfxHazardLooks.AfterLayers)
            {
                var go = new GameObject(layer);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>();
                var view = go.AddComponent<MeshRenderer>();
                view.sharedMaterial = material;
                Quiet(view);
                var part = go.AddComponent<VfxParticleBurst>();
                VfxHazardLooks.Apply(layer, part);
                part.burstAt = drop.lead + (part.moment == VfxParticleBurst.Moment.Gone ? drop.hold : 0f);
                Keep(old, layer, part);
            }

            var prefab = VfxAssets.Prefab(AfterPrefab, root);
            Register("FallingPiece_After", prefab);
            return prefab;
        }

        /// <summary>The values tuned on the old prefab's layer, copied onto the rebuilt one.</summary>
        static void Keep<T>(GameObject old, string layer, T fresh) where T : Component
        {
            var part = old != null ? old.transform.Find(layer) : null;
            var tuned = part != null ? part.GetComponent<T>() : null;
            if (tuned != null) EditorUtility.CopySerialized(tuned, fresh);
        }

        /// <summary>No shadows or probes: the effects are light.</summary>
        static void Quiet(Renderer view)
        {
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            view.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            view.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>The material at the path on <paramref name="shader"/>, with the shader's own values when it is first made.</summary>
        static Material Material(string path, string shader)
        {
            var mat = VfxAssets.Material(path, shader, out _);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Lists <paramref name="prefab"/> under <paramref name="key"/> in the hazards' set (made if missing).</summary>
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
