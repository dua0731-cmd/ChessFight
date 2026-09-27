using System.Linq;
using ChessFight.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    public static partial class KingRushOpeningBuilder
    {
        static void ExtendFinal()
        {
            var game = Object.FindFirstObjectByType<KingRushOpening>();
            if (game == null || game.finalBoard != null || game.transform.Find("22_Checkmate") != null) return;
            Palette(); var t = game.transform;
            Section(t, "18_KingsStairFork", SkyFork);
            Section(t, "19_FallingSteps", FallingSteps);
            Section(t, "20_GiantHandStair", HandStair);
            Section(t, "21_FinalPromotion", FinalPromotion);
            Section(t, "22_Checkmate", Checkmate);
            Section(t, "23_SkyWater", p => Box(p, "Sky fall water", new Vector3(0, -6, 550), new Vector3(100, 4, 216), water).AddComponent<WaterZone>());
            var end = t.Find("16_Seesaw/Next act end"); if (end != null) end.gameObject.SetActive(false);
            var title = t.Find("16_Seesaw/Next act title"); if (title != null) title.GetComponent<TextMesh>().text = "05 / 왕의 계단\n왼쪽 외나무 / 오른쪽 긴 계단";
            game.checkpoints = t.GetComponentsInChildren<KingRushCourseCheckpoint>().OrderBy(c => c.order).ToArray();
            game.pads = t.GetComponentsInChildren<KingRushPad>().OrderBy(p => p.index).ToArray();
            game.waters = t.GetComponentsInChildren<WaterZone>();
            game.padLabels = t.GetComponentsInChildren<TextMesh>().Where(l => l.name.StartsWith("Promotion label")).OrderBy(l => l.name).ToArray();
            game.rallies = t.GetComponentsInChildren<KingRushRallyGate>().OrderBy(r => r.wave).ToArray();
            game.finalBoard = t.GetComponentInChildren<KingRushFinalBoard>();
            EditorUtility.SetDirty(game); EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene); AssetDatabase.SaveAssets();
        }
        static void StairFlight(Transform t, Vector3 start, Vector3 end, int steps, float width, bool landing = true)
        {
            Vector3 flat = end - start; flat.y = 0; float length = flat.magnitude / steps;
            float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            for (int i = 0; i < steps; i++)
            {
                Vector3 p = Vector3.Lerp(start, end, (i + .5f) / steps); p.y = Mathf.Lerp(start.y, end.y, (i + 1f) / steps) - .2f;
                var tread = Box(t, "King stair tread", p, new Vector3(width, .4f, length + .025f), cream);
                tread.transform.rotation = Quaternion.Euler(0, yaw, 0); tread.GetComponent<Collider>().enabled = false;
            }
            WalkRamp(t, start, end, width, landing);
            if (landing) Floor(t, "Stair landing", end.x, end.y, end.z, width + .4f, width + .4f, cream);
        }
        static void WalkRamp(Transform t, Vector3 start, Vector3 end, float width, bool landing = true)
        {
            var ramp = Box(t, "Smooth stair collision", Vector3.zero, Vector3.one, cream);
            SetRamp(ramp.transform, start, end, width, landing);
            ramp.GetComponent<Renderer>().enabled = false;
        }
        static void SetRamp(Transform ramp, Vector3 start, Vector3 end, float width, bool landing)
        {
            if (landing) end -= Vector3.ProjectOnPlane(end - start, Vector3.up).normalized * (width * .5f + .2f);
            Vector3 delta = end - start; float flat = new Vector2(delta.x, delta.z).magnitude;
            ramp.position = (start + end) * .5f + Vector3.down * .1f; ramp.localScale = new Vector3(width, .2f, delta.magnitude);
            ramp.rotation = Quaternion.Euler(-Mathf.Atan2(delta.y, flat) * Mathf.Rad2Deg, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 0);
        }
        static void SkyFork(Transform t)
        {
            Floor(t, "Sky fork", 0, 3, 447, 34, 10, coral); CastleCheckpoint(t, 12, "왕의 계단 갈림", 0, 3, 445, 32);
            Sign(t, new Vector3(-13, 5.2f, 450), "왼쪽: 폭 0.8m 지름길\n바람 주의 · 빠르지만 위험");
            Sign(t, new Vector3(12, 5.2f, 444), "오른쪽: 돌아가는 계단\n길지만 넓은 길");
            var narrow = Box(t, "Windy shortcut", new Vector3(-8, 11.7f, 494), new Vector3(.8f, .6f, Mathf.Sqrt(84 * 84 + 18 * 18)), wood);
            narrow.transform.rotation = Quaternion.Euler(-Mathf.Atan2(18, 84) * Mathf.Rad2Deg, 0, 0);
            var points = new[] { new Vector3(4, 3, 452), new Vector3(20, 7.5f, 452), new Vector3(20, 12, 480), new Vector3(4, 16.5f, 480), new Vector3(4, 21, 452) };
            for (int i = 0; i < 4; i++) StairFlight(t, points[i], points[i + 1], 18, 4);
            Floor(t, "Spiral upper walk", 0, 21, 496, 4, 88, cream);
            for (int s = -1; s <= 1; s += 2) Box(t, "Upper walk rail", new Vector3(s * 2, 21.3f, 498), new Vector3(.18f, .6f, 84), gold);
            Floor(t, "Sky merge", 0, 21, 540, 28, 8, coral); CastleCheckpoint(t, 13, "무너지는 칸 앞", 0, 21, 540, 27);
            Label(t, "Sky merge guide", new Vector3(0, 24, 541), "붉게 흔들리면 곧 떨어집니다\n옆 칸과 박자를 비교하세요", .055f);
        }
        static void FallingSteps(Transform t)
        {
            for (int z = 0; z < 8; z++) for (int x = 0; x < 4; x++)
            {
                var go = Box(t, "Returning tile", new Vector3(-4.5f + x * 3, 20.7f, 545.5f + z * 3), new Vector3(3, .6f, 3), (x + z) % 2 == 0 ? white : black);
                var motion = go.AddComponent<KingRushSkyMotion>(); motion.offset = -z * .65f + x % 2 * 4;
                go.GetComponent<Rigidbody>().isKinematic = true;
            }
        }
        static void HandStair(Transform t)
        {
            Floor(t, "Hand corridor", 0, 21, 580, 12, 24, cream); CastleCheckpoint(t, 14, "거대한 손", 0, 21, 572, 11);
            var shadow = Box(t, "Hand warning shadow", new Vector3(0, 21.015f, 584), new Vector3(12, .025f, 5), dark);
            Object.DestroyImmediate(shadow.GetComponent<Collider>());
            var hand = new GameObject("Giant sweeping hand"); hand.transform.SetParent(t); hand.transform.position = new Vector3(0, 22, 584);
            var m = hand.AddComponent<KingRushSkyMotion>(); m.hand = true; m.shadow = shadow; hand.GetComponent<Rigidbody>().isKinematic = true;
            Box(hand.transform, "Palm", Vector3.zero, new Vector3(4, 1.3f, 3), cream).AddComponent<RagdollHazard>().minImpact = 1;
            for (int i = 0; i < 4; i++) Box(hand.transform, "Finger", new Vector3(-1.5f + i, -.25f, 2.3f), new Vector3(.7f, .7f, 3.4f), cream).AddComponent<RagdollHazard>().minImpact = 1;
            Sign(t, new Vector3(-10, 23.2f, 577), "손 그림자 = 1.5초 예고\n쓸고 지나간 뒤 통과");
            StairFlight(t, new Vector3(0, 21, 592), new Vector3(0, 40, 632), 76, 10);
        }
        static void FinalPromotion(Transform t)
        {
            Floor(t, "Last promotion courtyard", 0, 40, 643, 28, 22, coral);
            CastleCheckpoint(t, 15, "마지막 승격", 0, 40, 635, 27);
            for (int i = 0; i < 4; i++)
            {
                var at = new Vector3((i - 1.5f) * 5, 40.03f, 640);
                var go = new GameObject("Promotion rule " + (i + 5)); go.transform.SetParent(t); go.transform.position = at; go.AddComponent<KingRushPad>().index = i + 5;
                Box(t, "Promotion pad", at, new Vector3(2.5f, .06f, 2.5f), gold); Label(t, "Promotion label " + (i + 5), at + Vector3.up * 2, "승격", .06f);
            }
            var group = new GameObject("Raised rally"); group.transform.SetParent(t);
            Rally(group.transform, 2, 651, 28, 642);
            group.transform.position += Vector3.up * 40;
            var rally = group.GetComponentInChildren<KingRushRallyGate>(); var bounds = rally.arrival; bounds.center += Vector3.up * 40; rally.arrival = bounds;
        }
        static void Checkmate(Transform t)
        {
            var board = t.gameObject.AddComponent<KingRushFinalBoard>(); board.tiles = new Transform[64]; board.islands = new Transform[4];
            board.arena = new Bounds(new Vector3(0, 44, 674), new Vector3(24, 8, 24));
            board.dais = new Bounds(new Vector3(0, 42.5f, 674), new Vector3(6, 3, 6));
            board.throne = new Bounds(new Vector3(0, 41.8f, 674), new Vector3(1.6f, 2, 1.6f));
            var approach = new GameObject("Collapsing approaches"); approach.transform.SetParent(t); board.approaches = approach;
            Floor(approach.transform, "Final approach", 0, 40, 658, 6, 8, blue);
            for (int z = 0; z < 8; z++) for (int x = 0; x < 8; x++)
                board.tiles[z * 8 + x] = Box(t, "Final tile " + x + " " + z, new Vector3(-10.5f + x * 3, 39.7f, 663.5f + z * 3), new Vector3(3, .6f, 3), (x + z) % 2 == 0 ? white : black).transform;
            Floor(t, "Permanent dais", 0, 41, 674, 6, 6, gold);
            StairFlight(approach.transform, new Vector3(0, 40, 668), new Vector3(0, 41, 671), 4, 4, false);
            StairFlight(approach.transform, new Vector3(0, 40, 680), new Vector3(0, 41, 677), 4, 4, false);
            // The thin seat marker remains physically pushable across; no king is pinned or made kinematic.
            Box(t, "Throne seat marker", new Vector3(0, 41.015f, 674), new Vector3(1.6f, .03f, 1.6f), coral);
            Box(t, "Throne back", new Vector3(0, 42, 675), new Vector3(1.8f, 2, .2f), dark);
            Label(t, "Final throne guide", new Vector3(0, 45.5f, 676), "06 / 체크메이트\n킹은 왕좌로 · 동료는 단상을 지켜요", .06f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 at = new Vector3(i % 2 == 0 ? -18 : 18, 40, i < 2 ? 656 : 692);
                Floor(t, "Reentry tower", at.x, at.y, at.z, 5, 5, cream);
                var pad = new GameObject("Reentry launch " + i); pad.transform.SetParent(t); pad.transform.position = at; board.islands[i] = pad.transform;
                Box(t, "Tower launch marking", at + Vector3.up * .025f, new Vector3(2, .05f, 2), gold);
            }
            var crown = new GameObject("Winner crown"); crown.transform.SetParent(t); board.crown = crown.transform;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                var tooth = Box(crown.transform, "Crown point", new Vector3(Mathf.Cos(a) * .32f, i % 2 == 0 ? .13f : .07f, Mathf.Sin(a) * .32f), new Vector3(.12f, i % 2 == 0 ? .3f : .18f, .12f), gold);
                Object.DestroyImmediate(tooth.GetComponent<Collider>());
            }
            crown.SetActive(false);
        }
    }
}
