using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R701 (승규 11-17 "지금 미니게임에 소품이 충돌이 안되는 메시야 조사해봐", then "다른 건 충돌 넣어주고 ... 행차 회랑 ...
    // 사진에서 보이는 건 충돌 처리를 해버리면 시스템 상 기물을 저 안에 넣어야 되는데 충돌을 넣어버리면 막히잖아"): the rooms'
    // props by the pawns' ways get colliders. Until now every model was a picture over the blockout's colliders (R312,
    // PAWN_RUSH_ART_PHASES.md "아트는 그림만 얹는다"), on the promise that props keep off the pawns' ways; at the entry
    // bands and the rear corridors they did not, and pawns walked through gate posts, pots, benches and tables.
    //
    // After a room's models are hung (Apply), each prop gets boxes:
    //   - a prop hung on its own holder (A, D, F, G, H, I): one box per mesh, the mesh's own bounds, on the mesh;
    //   - C's environment keeps its props as separate meshes (crate, gear, lifebuoy, rope coil, banner posts): the same;
    //   - B, E and J merged their props into one mesh: boxes from tables measured off the FBX (every vertex to the
    //     nearest prop place of b_core_scene_v1.py / b_hall_v1.py / e_props_v1.py / j_laundry_props_v1.py), in the
    //     frame the model hangs in (Blender x, y, z = the parent's x, z, y).
    // Every box is static and NoClimbSurface (stood on, never climbed by hand) and the same on every PC, so nothing is
    // sent. Left as pictures on purpose: G's rook garages (G_GarageAlcove - the rooks are pushed into them) and the aisle
    // stanchions along the king's road, anything flat on the floor (A's carpet, I's slippers, B's sawdust, the robe on
    // I's bench), anything out of reach (walls, towers' tops, lamps, mirrors, galleries, bells, chandeliers).
    public static partial class MiniGameModels
    {
        const string PropColliderName = "PROP_COL";

        // Holders that carry one prop each (their names as the rooms make them).
        static readonly string[] SolidPropHolders =
        {
            "A_GatePost", "A_Cypress", "A_Planter", "A_Bench", "A_PaintRack", "A_PotWhite", "A_PotBlack",
            "D_GatePost", "D_BarRack", "D_ChainPile", "D_FlowerPot", "D_HelmetBench", "D_Cypress", "Corner tower W", "Corner tower E",
            "Crown pedestal", "Fanfare trumpets",
            "Notation tablet", "Spare-piece chest", "Chess clock",
            "Wardrobe", "Washstand", "Nightstand", "Velvet bench", "Dressing screen",
        };
        // F's holders are "<model> <n>".
        static readonly string[] SolidPropPrefixes = { "F_CoatStand ", "F_DrinksTable ", "F_Topiary " };
        // C_Drawbridge_EnvV2's prop meshes (Blender names, a ".001" suffix for the copies).
        static readonly string[] SolidKitMeshes = { "crate", "gear", "lifebuoy", "rope coil", "banner post" };

        static readonly float[] EBoxes =   // 16 props: min x, y, z, max x, y, z in the hang parent's frame
        {
            -18.30f, -0.00f, 1.34f, -16.30f, 1.02f, 2.45f,   // Table
            -20.90f, 0.00f, 0.80f, -20.30f, 1.05f, 2.00f,   // Trolley
            -15.58f, -0.01f, 0.67f, -14.90f, 1.37f, 1.35f,   // Globe
            -21.32f, -0.00f, 3.10f, -20.86f, 0.42f, 3.51f,   // Stack
            -21.86f, 0.40f, 7.13f, -20.91f, 3.41f, 7.87f,   // Ladder
            -18.68f, -0.00f, 33.16f, -17.32f, 1.09f, 33.97f,   // Desk
            -16.23f, -0.00f, 34.57f, -15.76f, 0.42f, 35.01f,   // Stack
            -20.90f, 0.00f, 30.80f, -20.30f, 1.05f, 32.01f,   // Trolley
            16.30f, -0.00f, 1.34f, 18.30f, 1.02f, 2.45f,   // Table
            20.30f, 0.00f, 0.80f, 20.90f, 1.05f, 2.00f,   // Trolley
            14.90f, -0.01f, 0.67f, 15.58f, 1.37f, 1.35f,   // Globe
            20.89f, -0.00f, 3.09f, 21.33f, 0.42f, 3.49f,   // Stack
            20.91f, 0.40f, 7.13f, 21.86f, 3.41f, 7.87f,   // Ladder
            17.32f, -0.00f, 33.16f, 18.68f, 1.09f, 33.97f,   // Desk
            15.77f, -0.00f, 34.57f, 16.25f, 0.42f, 35.03f,   // Stack
            20.30f, 0.00f, 30.80f, 20.90f, 1.05f, 32.01f,   // Trolley
        };
        static readonly float[] JBoxes =   // 22 props: min x, y, z, max x, y, z in the hang parent's frame
        {
            -19.00f, -0.00f, 0.70f, -17.40f, 0.52f, 1.10f,   // Bench
            -16.11f, -0.00f, 1.04f, -15.11f, 0.51f, 1.86f,   // Basket
            -21.81f, -0.00f, 1.17f, -21.31f, 0.35f, 1.81f,   // Sheets
            -20.44f, -0.00f, 2.61f, -19.76f, 0.30f, 3.19f,   // SoapCrate
            -17.28f, -0.00f, 2.44f, -16.74f, 0.71f, 2.96f,   // Bucket
            -21.96f, -0.01f, 1.80f, -21.67f, 0.93f, 2.46f,   // Washboard
            -21.58f, -0.01f, 32.10f, -20.82f, 1.26f, 33.50f,   // DryingRack
            -21.19f, -0.00f, 34.57f, -20.22f, 0.51f, 35.43f,   // Basket
            -18.80f, -0.00f, 30.20f, -17.20f, 0.52f, 30.60f,   // Bench
            -16.48f, -0.00f, 30.34f, -15.94f, 0.71f, 30.86f,   // Bucket
            -19.73f, -0.00f, 35.15f, -19.04f, 0.35f, 35.68f,   // Sheets
            17.40f, -0.00f, 0.70f, 19.00f, 0.52f, 1.10f,   // Bench
            15.11f, -0.00f, 1.04f, 16.11f, 0.51f, 1.86f,   // Basket
            21.31f, -0.00f, 1.15f, 21.81f, 0.35f, 1.81f,   // Sheets
            19.76f, -0.00f, 2.61f, 20.44f, 0.30f, 3.19f,   // SoapCrate
            16.72f, -0.00f, 2.44f, 17.26f, 0.71f, 2.96f,   // Bucket
            21.67f, -0.01f, 1.80f, 21.96f, 0.93f, 2.46f,   // Washboard
            20.82f, -0.01f, 32.10f, 21.58f, 1.26f, 33.50f,   // DryingRack
            20.22f, -0.00f, 34.57f, 21.19f, 0.51f, 35.43f,   // Basket
            17.20f, -0.00f, 30.20f, 18.80f, 0.52f, 30.60f,   // Bench
            15.92f, -0.00f, 30.34f, 16.46f, 0.71f, 30.86f,   // Bucket
            19.05f, -0.00f, 35.14f, 19.73f, 0.35f, 35.67f,   // Sheets
        };
        static readonly float[] BStationBoxes =   // 4 props: min x, y, z, max x, y, z in the hang parent's frame
        {
            -9.26f, -0.00f, 0.26f, -7.93f, 1.28f, 1.60f,   // sawhorse
            7.93f, -0.00f, 18.16f, 8.88f, 1.01f, 19.11f,   // chest
            6.42f, -0.00f, 18.65f, 7.38f, 0.46f, 19.15f,   // buckets
            -8.48f, -0.00f, 17.91f, -7.49f, 0.64f, 19.48f,   // roll
        };
        static readonly float[] BHallBoxes =   // 4 props: min x, y, z, max x, y, z in the hang parent's frame
        {
            -16.96f, 0.07f, 21.65f, -15.80f, 0.92f, 22.76f,   // stool
            15.84f, 0.07f, 21.82f, 17.02f, 0.92f, 22.93f,   // stool
            -18.07f, 0.03f, 20.33f, -17.15f, 0.72f, 22.08f,   // roll
            17.12f, 0.03f, 20.33f, 18.04f, 0.72f, 22.06f,   // roll
        };

        static void SolidProps(MiniGameSection owner)
        {
            var content = owner.transform.Find(MiniGameSection.ContentName);
            if (content == null) return;
            int boxes = 0;
            foreach (var t in content.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                string n = t.name;
                bool holder = System.Array.IndexOf(SolidPropHolders, n) >= 0;
                if (!holder)
                    foreach (var p in SolidPropPrefixes)
                        if (n.StartsWith(p)) { holder = true; break; }
                if (holder) { boxes += MeshBoxes(t); continue; }
                foreach (var m in SolidKitMeshes)
                    if (n == m || n.StartsWith(m + ".") || n.StartsWith(m + " "))
                    {
                        if (InModel(t, "C_Drawbridge_EnvV2")) boxes += MeshBox(t);
                        break;
                    }
                if (n == "E_PropsEnv") boxes += TableBoxes(t, EBoxes);
                else if (n == "J_PropsEnv") boxes += TableBoxes(t, JBoxes);
                else if (n == "B_StationEnv") boxes += TableBoxes(t, BStationBoxes);
                else if (n == "B_HallEnv") boxes += TableBoxes(t, BHallBoxes);
            }
            if (boxes > 0) Debug.Log("[미니게임 모델] 소품 충돌 상자 " + boxes + "개 (" + owner.name + ")");
        }

        static bool InModel(Transform t, string model)
        {
            for (var p = t.parent; p != null; p = p.parent)
                if (p.name == model) return true;
            return false;
        }

        // Every drawn mesh under a prop's holder, but a code-made flame (MiniGameFlame, the nightstand's candle).
        static int MeshBoxes(Transform holder)
        {
            int n = 0;
            foreach (var filter in holder.GetComponentsInChildren<MeshFilter>(true))
                if (!InModel(filter.transform, "Lantern flame")) n += MeshBox(filter.transform);
            return n;
        }

        // One box round a mesh, on the mesh (its own bounds, so any scale and turn follow), once.
        static int MeshBox(Transform t)
        {
            var filter = t.GetComponent<MeshFilter>();
            var renderer = t.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null || renderer == null || !renderer.enabled) return 0;
            if (t.GetComponent<Collider>() != null) return 0;
            var bounds = filter.sharedMesh.bounds;
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = Vector3.Max(bounds.size, Vector3.one * .02f);
            if (t.GetComponent<NoClimbSurface>() == null) t.gameObject.AddComponent<NoClimbSurface>();
            return 1;
        }

        // The table's boxes under the frame the model hangs in (the parent of its 180-degree pivot), once.
        static int TableBoxes(Transform model, float[] table)
        {
            var pivot = model.parent;
            var frame = pivot != null ? pivot.parent : null;
            if (frame == null || frame.Find(PropColliderName + " " + model.name) != null) return 0;
            var group = new GameObject(PropColliderName + " " + model.name).transform;
            group.SetParent(frame, false);
            int n = 0;
            for (int i = 0; i + 5 < table.Length; i += 6)
            {
                var min = new Vector3(table[i], table[i + 1], table[i + 2]);
                var max = new Vector3(table[i + 3], table[i + 4], table[i + 5]);
                var go = new GameObject("Prop box");
                go.transform.SetParent(group, false);
                var box = go.AddComponent<BoxCollider>();
                box.center = (min + max) * .5f;
                box.size = Vector3.Max(max - min, Vector3.one * .02f);
                go.AddComponent<NoClimbSurface>();
                n++;
            }
            return n;
        }
    }
}
