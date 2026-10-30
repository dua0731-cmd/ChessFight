using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The war room's Blender models (R255, ArtSource/PawnRush/Endgame): PR_EG_manifest.json places every part in course
    // coordinates. Statics and decor go into a group of their own (no colliders); each device skin goes under the moving
    // part it dresses, at that part's rest pose, so it moves with it. The grey boxes keep their colliders and only stop
    // drawing. The models are imported by ChessFight > Pawn Rush > Endgame: Import Blender Models into
    // Assets/Art/PawnRushMap/Endgame/Resources/PawnRushEndgame; without them (or with WarRoomUseModels off) the war
    // room stays the grey box.
    public static partial class Course01v4Builder
    {
        public static bool WarRoomUseModels = true;
        public const string WarRoomModelsPath = "PawnRushEndgame/";
        public const string WarRoomModelsGroup = "Blender models (R255)";
        // The FBX files come out of Blender (forward -Z, up Y) turned half round in Unity: a mesh point the manifest
        // means at (x, y, z) imports at (-x, y, -z). Every model is placed with this turn after the manifest's.
        static readonly Quaternion FbxTurn = Quaternion.Euler(0f, 180f, 0f);

        static readonly Dictionary<string, GameObject> warDevices = new();
        static readonly List<GameObject> warLook = new();
        static readonly List<Transform> warSkins = new();

        static void WarBegin()
        {
            warDevices.Clear();
            warLook.Clear();
            warSkins.Clear();
        }

        // A device root by its manifest id (the `attach` names are "Device_<id>").
        static void WarDevice(string id, GameObject root) => warDevices[id] = root;

        // A look-only grey box (or a collider's box) that the static models draw instead.
        static GameObject WarLook(GameObject go)
        {
            warLook.Add(go);
            return go;
        }

        [Serializable] class Placement
        {
            public string name, fbx, role, attach;
            public float[] position, euler, scale;
        }

        [Serializable] class Manifest { public Placement[] items; }

        // Which part of a device a skin follows (names under the device root, nearest to the skin; none = the root)
        // and which grey renderers it replaces (names under that part; none = all of them).
        static readonly Dictionary<string, (string[] follow, string[] replace)> SkinRules = new()
        {
            { "PR_EG_WarTable16", (null, null) },
            { "PR_EG_SweepBar", (null, null) },
            { "PR_EG_FerryDeck", (null, null) },
            { "PR_EG_HurdleBaton", (null, null) },
            { "PR_EG_ShelfWall6", (new[] { "Left sliding wall", "Right sliding wall" }, null) },
            { "PR_EG_ShelfHousing", (new[] { "Wall housing" }, null) },
            { "PR_EG_GateFrame", (null, new[] { "Gate pillar", "Clock arch", "Clock face" }) },
            { "PR_EG_GateDoor", (new[] { "Left gate", "Right gate" }, null) },
            { "PR_EG_RookScreen6", (null, null) },
            { "PR_EG_PendulumGantry", (null, new[] { "Ceiling mount" }) },
            { "PR_EG_PendulumWeight", (new[] { "Pivot" }, new[] { "Chain collar", "Suspension cable", "Ball" }) },
            { "PR_EG_MapChestTile", (new[] { "Lift tile" }, null) },
            { "PR_EG_LiftShaft", (null, new[] { "Pit edge" }) },
            { "PR_EG_WindVent", (new[] { "VentModel" }, null) },
            { "PR_EG_MapBelt", (null, new[] { "Chess tile", "Conveyor base", "Belt rim" }) },
            { "PR_EG_CrownCircle", (null, new string[0]) },   // the warning circle stays the device's own
            { "PR_EG_GiantPawn", (new[] { "Falling pawn" }, null) },
            { "PR_EG_HangingCrate", (null, null) },
            { "PR_EG_CompassDisc", (null, new[] { "Solid disc" }) },   // the rings and arrows show the speeds
            { "PR_EG_BridgeLanding", (new[] { "LeftLanding", "RightLanding" }, null) },
            { "PR_EG_BridgePanel", (new[] { "LeftPivot", "RightPivot" }, null) },
        };

        static void WarRoomModels(CourseBuilder b)
        {
            var text = WarRoomUseModels ? Resources.Load<TextAsset>(WarRoomModelsPath + "PR_EG_manifest") : null;
            if (text == null) return;
            var manifest = JsonUtility.FromJson<Manifest>("{\"items\":" + text.text + "}");
            var zone = b.parent;
            var group = b.Group(WarRoomModelsGroup);
            var models = new Dictionary<string, GameObject>();
            int placed = 0, skins = 0, missing = 0;
            var lost = new List<string>();
            foreach (var p in manifest.items)
            {
                string part = p.fbx.Substring(0, p.fbx.IndexOf('/'));
                if (!models.TryGetValue(part, out var model))
                    models[part] = model = Resources.Load<GameObject>(WarRoomModelsPath + part + "/" + part);
                if (model == null) { missing++; continue; }
                var at = zone.TransformPoint(V(p.position[0], p.position[1], p.position[2]));
                var turn = zone.rotation * Quaternion.Euler(p.euler[0], p.euler[1], p.euler[2]) * FbxTurn;
                Transform parent = group;
                if (p.role == "skin")
                {
                    parent = SkinParent(p, part, at);
                    if (parent == null) { lost.Add(p.name); continue; }
                    skins++;
                }
                var go = CourseBuilder.Spawn(model, parent);
                go.name = p.name;
                go.transform.SetPositionAndRotation(at, turn);
                var s = V(p.scale[0], p.scale[1], p.scale[2]);
                var ls = parent.lossyScale;
                go.transform.localScale = new Vector3(s.x / ls.x, s.y / ls.y, s.z / ls.z);
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
                if (p.role == "skin") warSkins.Add(go.transform);
                placed++;
            }
            if (placed - skins > 0)
                foreach (var go in warLook)
                    if (go != null)
                        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            Debug.Log($"[PawnRush] 엔드게임 Blender 모델: {placed}개 배치(장치 스킨 {skins}개), FBX 없음 {missing}개" +
                      (lost.Count > 0 ? ", 붙일 장치를 못 찾음: " + string.Join(", ", lost) : ""));
        }

        // The transform a device skin goes under, with the grey renderers it replaces turned off.
        static Transform SkinParent(Placement p, string part, Vector3 at)
        {
            string attach = p.attach;
            int cut = attach.IndexOf(' ');
            string id = (cut < 0 ? attach : attach.Substring(0, cut)).Replace("Device_", "");
            if (!warDevices.TryGetValue(id, out var device) || device == null || !SkinRules.TryGetValue(part, out var rule)) return null;
            var target = device.transform;
            if (rule.follow != null)
            {
                float best = float.MaxValue;
                target = null;
                foreach (var t in device.GetComponentsInChildren<Transform>(true))
                {
                    if (!Starts(t.name, rule.follow)) continue;
                    float d = (t.position - at).sqrMagnitude;
                    if (d < best) { best = d; target = t; }
                }
                if (target == null) return null;
            }
            foreach (var r in target.GetComponentsInChildren<Renderer>(true))
                if ((rule.replace == null || Starts(r.name, rule.replace)) && !warSkins.Exists(skin => r.transform.IsChildOf(skin)))
                    r.enabled = false;
            return target;
        }

        static bool Starts(string name, string[] prefixes)
        {
            foreach (var x in prefixes)
                if (name.StartsWith(x, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
