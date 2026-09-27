using System.Linq;
using ChessFight.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    public static partial class KingRushOpeningBuilder
    {
        static void ExtendCourse()
        {
            var game = Object.FindFirstObjectByType<KingRushOpening>();
            if (game == null) return;
            if (game.seesawBoard != null || game.transform.Find("16_Seesaw") != null) return;
            // Append new prefab sections and explicit overrides only; never regenerate the R54 scene.
            Palette(); var t = game.transform;
            Section(t, "11_RallyWall", p => Rally(p, 0, 168, 20, 155));
            Section(t, "12_CastleUpper", UpperCastle);
            Section(t, "13_CastleLower", LowerCastle);
            Section(t, "14_CrossingGate", CrossCastle);
            Section(t, "15_CastlePromotion", CastlePromotion);
            Section(t, "16_Seesaw", SeesawArena);
            Section(t, "17_CastleWater", p => {
                Box(p, "Castle moat", new Vector3(0, -6, 305), new Vector3(90, 4, 170), water).AddComponent<WaterZone>();
                Box(p, "Seesaw water", new Vector3(0, -10, 421), new Vector3(90, 4, 62), water).AddComponent<WaterZone>();
            });
            var end = t.Find("09_Exit/End wall"); if (end != null) end.gameObject.SetActive(false);
            var title = t.Find("09_Exit/First course end"); if (title != null) title.GetComponent<TextMesh>().text = "03 / 성벽 갈림길\n위: 등반 · 아래: 타이밍";
            game.checkpoints = t.GetComponentsInChildren<KingRushCourseCheckpoint>().OrderBy(c => c.order).ToArray();
            game.pads = t.GetComponentsInChildren<KingRushPad>().OrderBy(p => p.index).ToArray();
            game.gates = t.GetComponentsInChildren<PawnGate>().OrderBy(g => g.blue).ThenBy(g => g.team).ToArray();
            game.waters = t.GetComponentsInChildren<WaterZone>();
            game.padLabels = t.GetComponentsInChildren<TextMesh>().Where(l => l.name.StartsWith("Promotion label")).OrderBy(l => l.name).ToArray();
            game.rallies = t.GetComponentsInChildren<KingRushRallyGate>().OrderBy(g => g.wave).ToArray();
            game.seesawBoard = t.GetComponentInChildren<KingRushSeesawBoard>();
            game.seesawExits = t.GetComponentsInChildren<KingRushSeesawExit>().OrderBy(e => e.team).ToArray();
            EditorUtility.SetDirty(game); EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene); AssetDatabase.SaveAssets();
        }
        static void Rally(Transform t, int wave, float z, float width, float center)
        {
            var go = new GameObject("Rally " + wave); go.transform.SetParent(t);
            var rally = go.AddComponent<KingRushRallyGate>(); rally.wave = wave;
            rally.arrival = new Bounds(new Vector3(0, 2, center), new Vector3(width, 4, 24));
            rally.wall = Box(t, "Countdown wall", new Vector3(0, 4, z), new Vector3(width, 8, .6f), dark).transform;
            rally.label = Label(t, "Rally countdown", new Vector3(0, 4.6f, z - .4f), "승격 인원 대기", .09f);
            rally.label.color = gold.color;
            for (int s = -1; s <= 1; s += 2)
                Box(t, "Rally side", new Vector3(s * (width / 2 + .25f), 4, z - 8), new Vector3(.5f, 8, 16), wood);
            for (int i = 0; i < 12; i++)
                Box(t, "Catch-up spot " + i, rally.Slot(i) + Vector3.up * .01f, new Vector3(1.2f, .02f, 1.2f), i < 6 ? white : black);
        }
        static void CastleCheckpoint(Transform t, int index, string title, float x, float y, float z, float width)
        {
            var go = new GameObject("Checkpoint " + index + " · " + title); go.transform.SetParent(t); go.transform.position = new Vector3(x, y, z);
            var cp = go.AddComponent<KingRushCourseCheckpoint>(); cp.order = index; cp.title = title; cp.size = new Vector3(width, 5, 3);
            Box(t, "Checkpoint stripe", new Vector3(x, y + .01f, z), new Vector3(width, .02f, .2f), gold);
        }
        static KingRushCastleMotion Moving(GameObject go, KingRushCastleMotion.Kind kind, float period)
        {
            var m = go.AddComponent<KingRushCastleMotion>(); m.kind = kind; m.period = period;
            go.GetComponent<Rigidbody>().isKinematic = true; return m;
        }
        static void UpperCastle(Transform t)
        {
            Floor(t, "Fork courtyard", 0, 0, 229, 28, 18, cream);
            CastleCheckpoint(t, 7, "성벽 갈림목", 0, 0, 222, 27);
            Sign(t, new Vector3(-7, 2.2f, 230), "위쪽: 성벽 등반\n우클릭 + W / Space");
            Sign(t, new Vector3(7, 2.2f, 230), "아래쪽: 해자길\n움직이는 발판을 기다려요");
            Box(t, "Climb face 3.5m", new Vector3(-10, 1.45f, 248), new Vector3(8, 4.1f, 20), cream);
            Floor(t, "Temporary swing replacement", -10, 3.5f, 268, 3, 20, wood);
            Sign(t, new Vector3(-14, 5.7f, 255), "좁은 다리\n밧줄 그네 자리 · 임시 통로");
            Floor(t, "Upper mace walk", -10, 3.5f, 290, 8, 24, cream);
            CastleCheckpoint(t, 9, "성벽 철퇴", -10, 3.5f, 280, 7);
            var pivot = new GameObject("Swinging mace pivot"); pivot.transform.SetParent(t); pivot.transform.position = new Vector3(-10, 10, 291);
            Moving(pivot, KingRushCastleMotion.Kind.Mace, 5);
            Box(pivot.transform, "Chain", new Vector3(0, -3, 0), new Vector3(.18f, 6, .18f), dark);
            var head = Shape(pivot.transform, "Mace", PrimitiveType.Sphere, new Vector3(0, -6, 0), Vector3.one * 2, dark);
            head.AddComponent<RagdollHazard>().minImpact = 1;
            var ramp = Box(t, "Upper descent", new Vector3(-10, 1.45f, 309), new Vector3(8, .6f, Mathf.Sqrt(14 * 14 + 3.5f * 3.5f)), cream);
            ramp.transform.rotation = Quaternion.Euler(Mathf.Atan2(3.5f, 14) * Mathf.Rad2Deg, 0, 0);
        }
        static void LowerCastle(Transform t)
        {
            var ferry = Box(t, "Moat ferry", new Vector3(10, -.45f, 249), new Vector3(8, .6f, 14), gold);
            Moving(ferry, KingRushCastleMotion.Kind.Ferry, 8);
            Floor(t, "Lower landing", 10, 0, 269, 8, 18, cream);
            CastleCheckpoint(t, 8, "해자 미는 벽", 10, 0, 262, 7);
            var wall = Box(t, "Sliding moat wall", new Vector3(10, .9f, 270), new Vector3(2.4f, 1.8f, 1.5f), wood);
            Moving(wall, KingRushCastleMotion.Kind.SlidingWall, 5);
            var hinge = new GameObject("Drawbridge hinge"); hinge.transform.SetParent(t); hinge.transform.position = new Vector3(10, 0, 278);
            Moving(hinge, KingRushCastleMotion.Kind.Drawbridge, 10);
            Box(hinge.transform, "Drawbridge deck", new Vector3(0, -.3f, 4), new Vector3(8, .6f, 8.3f), wood);
            Floor(t, "Lower exit walk", 10, 0, 301, 8, 30, cream);
            Sign(t, new Vector3(10, 2.2f, 276), "도개교\n다리가 내려왔을 때 건너요");
        }
        static void CrossCastle(Transform t)
        {
            // Two 4 m wide diagonal paths intersect at the same elevation, not an overpass.
            for (int side = -1; side <= 1; side += 2)
            {
                var cross = Box(t, "Crossing bridge", new Vector3(0, -.3f, 328), new Vector3(4, .6f, Mathf.Sqrt(20 * 20 + 24 * 24)), side < 0 ? white : black);
                cross.transform.rotation = Quaternion.Euler(0, side * Mathf.Atan2(20, 24) * Mathf.Rad2Deg, 0);
            }
            Floor(t, "Gate courtyard", 0, 0, 349, 28, 18, cream);
            CastleCheckpoint(t, 10, "성문", 0, 0, 343, 27);
            var gate = Box(t, "Timed portcullis", new Vector3(0, 2, 351), new Vector3(28, 4, .6f), dark);
            Moving(gate, KingRushCastleMotion.Kind.Portcullis, 8); gate.AddComponent<RagdollHazard>().minImpact = 1;
            foreach (int s in new[] { -1, 1 }) Box(t, "Castle pillar", new Vector3(s * 14, 5, 351), new Vector3(1, 10, 2), wood);
            Sign(t, new Vector3(-7, 2.2f, 346), "성문이 올라갈 때 통과\n다음은 3명 승격");
        }
        static void CastlePromotion(Transform t)
        {
            Floor(t, "Castle promotion courtyard", 0, 0, 375, 24, 34, coral);
            CastleCheckpoint(t, 11, "두 번째 승격", 0, 0, 361, 23);
            for (int i = 0; i < 3; i++)
            {
                var at = new Vector3((i - 1) * 5, .03f, 373);
                var go = new GameObject("Promotion rule " + (i + 2)); go.transform.SetParent(t); go.transform.position = at;
                go.AddComponent<KingRushPad>().index = i + 2;
                Box(t, "Promotion pad", at, new Vector3(2.5f, .06f, 2.5f), gold);
                Label(t, "Promotion label " + (i + 2), at + Vector3.up * 2, "승격", .06f);
            }
            Rally(t, 1, 389, 24, 376);
        }
        static void SeesawArena(Transform t)
        {
            Floor(t, "Seesaw entry pier", 0, 0, 396, 14, 8, blue);
            Label(t, "Seesaw banner", new Vector3(0, 7, 409), "04 / 판 뒤집기\n반대편을 무겁게 → 우리 팀 출구로 4명!", .075f);
            var pivot = new GameObject("Seesaw board"); pivot.transform.SetParent(t); pivot.transform.position = new Vector3(0, 0, 412);
            pivot.AddComponent<KingRushSeesawBoard>(); pivot.GetComponent<Rigidbody>().isKinematic = true;
            for (int x = 0; x < 8; x++) for (int z = 0; z < 8; z++)
                Box(pivot.transform, "Chess tile", new Vector3(-10.5f + x * 3, -.3f, -10.5f + z * 3), new Vector3(3, .6f, 3), (x + z) % 2 == 0 ? white : black);
            Box(pivot.transform, "Neutral spine", new Vector3(0, .015f, 0), new Vector3(1, .03f, 24), gold);
            // Local geometry under the single moving rigidbody; fixed statues can be gripped.
            foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
            {
                Box(pivot.transform, "Statue base", new Vector3(x * 5, .6f, z * 5), new Vector3(1.5f, 1.2f, 1.5f), cream);
                Shape(pivot.transform, "Statue head", PrimitiveType.Sphere, new Vector3(x * 5, 1.8f, z * 5), Vector3.one * 1.6f, cream);
            }
            for (int team = 0; team < 2; team++)
            {
                float x = team == 0 ? -12 : 12; var m = team == 0 ? white : black;
                Floor(t, "High exit ledge", x, 3, 428, 6, 8, m);
                var front = Box(t, "Overhanging exit face", new Vector3(x, 1.25f, 424), new Vector3(6, 3, .5f), m);
                front.transform.rotation = Quaternion.Euler(-24, 0, 0);
                var door = Box(t, "Tilt access " + team, new Vector3(x, 4, 426), new Vector3(6, 2, .25f), m);
                var exit = door.AddComponent<KingRushSeesawExit>(); exit.team = team; exit.barrier = door.GetComponent<Collider>();
                exit.crossing = new Bounds(new Vector3(x, 4, 428), new Vector3(6, 3, 2));
                exit.label = Label(t, "Seesaw exit label " + team, new Vector3(x, 6.3f, 425.8f), "출구", .07f); exit.label.color = gold.color;
                var rescue = Box(t, "Rescue ramp " + team, new Vector3(x > 0 ? 5.5f : -5.5f, 1.2f, 429), new Vector3(4, .6f, Mathf.Sqrt(7 * 7 + 3 * 3)), gold);
                rescue.transform.rotation = Quaternion.Euler(-Mathf.Atan2(3, 7) * Mathf.Rad2Deg, x > 0 ? 90 : -90, 0);
                // Inner end connects to a fixed zero-weight pier, outside the moving board.
                if (team == 0) Floor(t, "Rescue center pier", 0, 0, 427, 4, 6, blue);
                rescue.SetActive(false); exit.rescueBridge = rescue;
                var finalDoor = Box(t, "Seesaw next gate " + team, new Vector3(x, 5.5f, 431), new Vector3(6, 5, .35f), m);
                var next = finalDoor.AddComponent<PawnGate>(); next.team = team; next.blue = 1; next.barrier = finalDoor.GetComponent<Collider>();
                next.exitBounds = new Bounds(new Vector3(x, 4.5f, 433), new Vector3(6, 4, 2));
            }
            Floor(t, "Red3 staging", 0, 3, 437, 34, 10, coral);
            Box(t, "Next act end", new Vector3(0, 5, 442), new Vector3(34, 4, .5f), cream);
            Label(t, "Next act title", new Vector3(0, 6, 438), "두 번째 미션 통과!\n다음: 왕의 계단 / 결승 · 제작 예정", .06f);
        }
    }
}
