using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R308 (승규 11-03): the Blender hall v15 (ArtSource/PawnRush/GrandStairHall, export_unity_v15.py) over the blockout
        // hall the code builds: smooth spiral bands, arches, hanging chandeliers, stone stairs, marble floors, the foyer's
        // four statues and the library walls, all untextured clay. The blockout pieces they stand in for draw nothing but
        // keep their colliders, so walking, falling and the hits are the blockout's as before.
        const string HallModel = "PawnRushGrandStairHall/GrandStairHallV15";

        static void GrandStairHallModel(CourseBuilder b, Transform hall)
        {
            var model = Resources.Load<GameObject>(HallModel);
            var hiddenText = Resources.Load<TextAsset>(HallModel + "_Hidden");
            if (model == null || hiddenText == null)
            {
                Debug.LogError("[대계단 홀] 모델을 불러오지 못했습니다: Resources/" + HallModel + ".fbx, " + HallModel + "_Hidden.txt");
                return;
            }
            // Blender names are the Unity paths under Content with '_' for ' ' and '#2', '#3' for repeated names.
            var hidden = new HashSet<string>();
            foreach (var line in hiddenText.text.Split('\n'))
            {
                var path = line.Trim();
                int mark = path.IndexOf('#');
                if (mark >= 0) path = path.Substring(0, mark);
                if (path.Length > 0) hidden.Add(path);
            }
            var content = hall.Find(GrandStairHall.ContentName);
            int off = 0;
            foreach (var r in content.GetComponentsInChildren<Renderer>(true))
            {
                if (!hidden.Contains(PathUnder(content, r.transform).Replace(' ', '_'))) continue;
                r.enabled = false;
                off++;
            }

            var pivot = new GameObject("Grand stair hall v15").transform;
            pivot.SetParent(hall, false);
            pivot.localRotation = Quaternion.Euler(0, 180, 0);
            var view = Object.Instantiate(model, pivot, false);
            view.name = "Grand stair hall v15 model";
            var clay = HallClay;
            var marble = PieceFigure.Lit(new Color(.8f, .8f, .79f), .78f);
            var under = b.kit.floorChecker;
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string name = slots[i] != null ? slots[i].name : "";
                    // The marble floors' joints show the lowered blockout floor under them.
                    slots[i] = name.StartsWith("Marble") ? marble : name.StartsWith("Section_Floor_under") ? under : clay;
                }
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            Debug.Log("[대계단 홀] v15 모델 " + view.transform.childCount + "개 조각, 블록아웃 " + off + "개 숨김(충돌은 그대로)");
            WidenHallFloor(content);
            HangPads(content, clay);
        }

        // R311 (승규 11-03): the red hall floor runs on under the v17 walls' recesses (x +-38.1, was +-34), so no slot shows
        // at their foot. Only the walls stand out there, so the wider collider changes nothing walked.
        const float HallFloorHalf = 38.1f;

        static void WidenHallFloor(Transform content)
        {
            var floor = FindUnder(content, "Hall floor (respawn)");
            if (floor == null) return;
            var scale = floor.localScale;
            scale.x = HallFloorHalf * 2f;
            floor.localScale = scale;
        }

        // R311: 승규's pad assets (pads_v18.py, flat tops, origin at the top centre) on the pieces that carry them. They
        // move or launch, so they hang on those pieces rather than in the hall model, and every PC builds the same.
        //   floating stones  the solar panel cube, 2.6 m square on the 2.6 m round stone, an edge along the radius; a box
        //                    collider the size of the square, so its corners hold a foot as they look
        //   02 jump pad      the glowing platform in full 3D (승규: "점프 발판은 3D로"), 3 m square on the kit pad's 3 m base,
        //                    its arrow along the launch, its deck where the kit plate's top was (0.41 m); the base's
        //                    collider raised to that plane too, so WASD on the pad meets one flat top, no edge
        const string FloatingPadModel = "PawnRushGrandStairHall/FloatingPad", JumpPadModel = "PawnRushGrandStairHall/JumpPadModel";
        const float PadDeck = .41f;   // the kit plate's top: the launch height is as before

        static void HangPads(Transform content, Material clay)
        {
            var stoneModel = Resources.Load<GameObject>(FloatingPadModel);
            var jumpModel = Resources.Load<GameObject>(JumpPadModel);
            if (stoneModel == null || jumpModel == null)
                Debug.LogError("[대계단 홀] 발판 모델을 불러오지 못했습니다: Resources/" + FloatingPadModel + ".fbx, " + JumpPadModel + ".fbx");
            int stones = 0, pads = 0;
            foreach (var t in content.GetComponentsInChildren<Transform>(true))
            {
                if (stoneModel != null && t.name.StartsWith("Floating stone ") && FloatingPad(content, t, stoneModel, clay)) stones++;
                else if (jumpModel != null && t.name == "02 Jump pad" && JumpPad(t, jumpModel, clay)) pads++;
            }
            Debug.Log("[대계단 홀] 떠 있는 발판 " + stones + "개, 점프 발판 " + pads + "개를 새 모델로");
            FallingKnights(content, clay);
        }

        // R317 (승규 11-03): the hall's falling pieces drawn as 승규's chess knight (knight_v20.py: 1.8 m round base like
        // the kit pawn's foot, 2.4 m high, origin at the base's bottom, head to +z). It hangs on the piece's "Falling
        // pawn", which the obstacle moves; the kit pawn's foot, body and head draw nothing and keep their colliders.
        const string FallingKnightModel = "PawnRushGrandStairHall/FallingKnight";

        static void FallingKnights(Transform content, Material clay)
        {
            var model = Resources.Load<GameObject>(FallingKnightModel);
            if (model == null)
            {
                Debug.LogError("[대계단 홀] 기사 모델을 불러오지 못했습니다: Resources/" + FallingKnightModel + ".fbx");
                return;
            }
            int knights = 0;
            foreach (var t in content.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("10 Falling piece ")) continue;
                var pawn = FindUnder(t, "Falling pawn");
                if (pawn == null || pawn.Find("Knight model") != null) continue;
                foreach (var r in pawn.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                Hang(pawn, model, "Knight model", Vector3.zero, clay);
                knights++;
            }
            Debug.Log("[대계단 홀] 떨어지는 말 " + knights + "개를 기사 모델로");
        }

        static bool FloatingPad(Transform content, Transform stone, GameObject model, Material clay)
        {
            if (stone.Find("Pad model") != null) return false;
            var at = content.InverseTransformPoint(stone.position);
            stone.localRotation = Quaternion.Euler(0f, Mathf.Atan2(at.x - GrandStairHallBuilder.Hub.x, at.z - GrandStairHallBuilder.Hub.y) * Mathf.Rad2Deg, 0f);
            // Immediate, as CourseBuilder.Finish: the module is still inactive while it is built.
            if (stone.TryGetComponent<MeshCollider>(out var round)) Object.DestroyImmediate(round);
            if (!stone.TryGetComponent<BoxCollider>(out var box)) box = stone.gameObject.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = new Vector3(1f, 2f, 1f);          // the cylinder's own size: 2.6 x 0.5 x 2.6 under its scale
            if (stone.TryGetComponent<Renderer>(out var drawn)) drawn.enabled = false;
            Hang(stone, model, "Pad model", Vector3.up, clay);   // the cylinder's top is local y 1
            return true;
        }

        static bool JumpPad(Transform pad, GameObject model, Material clay)
        {
            if (pad.Find("Pad model") != null) return false;
            foreach (var r in pad.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            Sink(FindUnder(pad, "Spring plate"), PadDeck);
            Sink(FindUnder(pad, "Jump pad base"), PadDeck);
            Hang(pad, model, "Pad model", Vector3.up * PadDeck, clay);
            return true;
        }

        // A box piece moved up or down so its top is at `top` (in its parent).
        static void Sink(Transform t, float top)
        {
            if (t == null) return;
            var p = t.localPosition;
            p.y = top - t.localScale.y * .5f;
            t.localPosition = p;
        }

        static void Hang(Transform on, GameObject model, string name, Vector3 localTop, Material clay)
        {
            var view = Object.Instantiate(model, on, false);
            view.name = name;
            view.transform.localPosition = localTop;
            view.transform.localRotation = Quaternion.identity;
            var s = on.localScale;
            view.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);   // metres in the hall, whatever the piece's scale
            foreach (var r in view.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = clay;
                r.sharedMaterials = slots;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var c in view.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        static Transform FindUnder(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // The hall's untextured clay (Blender's Arch_Clay), also the falling chandeliers'.
        public static Material HallClay => PieceFigure.Lit(new Color(.62f, .62f, .62f), .15f);

        static string PathUnder(Transform root, Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
