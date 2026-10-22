using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    // ChessFight > Pawn Rush > Art Dress Course01 (Kit v1): puts the blocky cream-stone map kit
    // (Assets/Art/PawnRushMap/Kit, concept art 2) over the demo stretch S_Start -> A_Shared.
    // The dress lives in its own root next to the course root, has no colliders, and only hides the
    // grey-box renderers it covers, so gameplay and the validator see exactly the same course.
    // Build Course01 v4 brings the grey boxes back; run this again after a build.
    public static class Course01ArtDress
    {
        const string KitDir = "Assets/Art/PawnRushMap/Kit";
        const string MatDir = "Assets/Art/PawnRushMap/Materials";
        const string RootName = "Course01_ArtDress";
        const float Lift = 0.005f; // dress floors sit a hair above the grey-box tops (no z-fighting)

        // Grey-box pieces the dress replaces visually (path under Course01v4_Root).
        static readonly string[] Covered =
        {
            "S_Start/Start floor", "S_Start/Back wall", "S_Start/Side wall W", "S_Start/Side wall E",
            "S_Start/Front rail W", "S_Start/Front rail E",
            "A_Shared/A1 road", "A_Shared/A1 kerb W", "A_Shared/A1 kerb E", "A_Shared/A2 wall (face z 28) and edge",
            "A_Shared/A3 end floor", "A_Shared/A3 rail W", "A_Shared/A3 rail N",
            "A_Shared/A4 road", "A_Shared/A4 wall S", "A_Shared/A4 wall N",
            "A_Shared/Plaza 1 yard", "A_Shared/Yard rail S", "A_Shared/Yard rail E",
            "A_Shared/Yard rail W (south)", "A_Shared/Yard rail W (north)",
            "Plaza1/Entry band", "Plaza1/Aisle", "Plaza1/Outer wall W", "Plaza1/Outer wall E",
            "Plaza1/Outer wall S (west of the entry)", "Plaza1/Outer wall S (east of the entry)",
            "Plaza1/North block W", "Plaza1/North block middle", "Plaza1/North block E",
            "Plaza1/Station_1_백/Gate passage", "Plaza1/Station_1_흑/Gate passage",
        };

        static Transform root;

        [MenuItem("ChessFight/Pawn Rush/Art Dress Course01 (Kit v1)", false, 20)]
        public static void Dress()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SetupMaterials();
            SetupCourseKit();
            SetupRuntimeLook(); // before the build: the stations' games read it while they are built
            // Always from fresh grey boxes: AutoDress reads the grey boxes it hides, so dressing an already
            // dressed course a second time would skip them.
            Course01Assembler.Build();
            var course = GameObject.Find("Course01v4_Root");
            if (course == null)
            {
                EditorUtility.DisplayDialog("Art Dress", "Course01v4_Root가 없습니다. Build Course01 v4를 확인하세요.", "확인");
                return;
            }
            ApplySky();
            RemoveRoot();
            root = new GameObject(RootName).transform;
            islands.Clear();
            SetCovered(course.transform, false);

            // ---- S_Start: floor x -8..8, z -12..0; back wall to y 4 at z -13..-12; side walls/rails 1.2 m
            Floor(new Vector3(-8, -1, -12), new Vector3(8, 0, 0));
            Wall(new Vector3(-8.5f, -1, -13), new Vector3(8.5f, 4, -12));
            Battlements(new Vector3(-8.5f, 4, -13), new Vector3(8.5f, 4, -12));
            Wall(new Vector3(-8.5f, -1, -12), new Vector3(-8, 1.2f, 0));
            Wall(new Vector3(8, -1, -12), new Vector3(8.5f, 1.2f, 0));
            Wall(new Vector3(-8.5f, 0, 0), new Vector3(-5.4f, 1.2f, 0.3f));
            Wall(new Vector3(5.4f, 0, 0), new Vector3(8.5f, 1.2f, 0.3f));

            // start decor, all outside the playable floor
            Put("PR_CrownArch", new Vector3(0, -1, -17.5f), 0, Vector3.one * 1.3f);
            foreach (var s in new[] { -1, 1 })
            {
                Put("PR_Tower_4x6", new Vector3(s * 10.65f, -2, -12.5f), 0, Vector3.one);
                TowerBase(new Vector3(s * 10.65f, -2, -12.5f));
                Put("PR_FlagChecker", new Vector3(s * 10.65f, 5.4f, -12.5f), 0, Vector3.one);
                Put("PR_KnightStatue", new Vector3(s * 6.5f, 4, -12.5f), s > 0 ? -90 : 90, Vector3.one * 0.9f);
                Put("PR_BannerNavy", new Vector3(s * 4f, 3.9f, -11.8f), 180, Vector3.one * 0.75f);
                Put("PR_ChainHanging", new Vector3(s * 8.25f, 4.0f, -11.8f), 0, Vector3.one);
            }
            Put("PR_BannerRed", new Vector3(0, 3.9f, -11.7f), 180, Vector3.one * 0.75f);

            // ---- A1 road x -5..5, z 0..28, kerbs 0.15 m; towers over the void on both sides
            Floor(new Vector3(-5, -1, 0), new Vector3(5, 0, 28));
            Trim(new Vector3(-5.4f, 0, 0), new Vector3(-5, 0.15f, 28));
            Trim(new Vector3(5, 0, 0), new Vector3(5.4f, 0.15f, 28));
            foreach (var s in new[] { -1, 1 })
            foreach (var z in new[] { 6f, 22f })
            {
                Put("PR_Tower_4x6", new Vector3(s * 9f, -4, z), 0, Vector3.one);
                TowerBase(new Vector3(s * 9f, -4, z));
                Put("PR_BannerNavy", new Vector3(s * 6.8f, 2.6f, z), s > 0 ? -90 : 90, Vector3.one * 0.6f);
            }
            // 10-22 (승규 "기둥에 엄청난 체스 동상"): giant statues on the four towers, turned to the road and the
            // runners coming up it; king and queen on the far pair, bishop and knight on the near one.
            Statue("King", new Vector3(-9f, -4f, 22f), 135f);
            Statue("Queen", new Vector3(9f, -4f, 22f), -135f);
            Statue("Bishop", new Vector3(-9f, -4f, 6f), 135f);
            Statue("Knight", new Vector3(9f, -4f, 6f), -135f);

            // ---- A2 climbable wall: face z 28, top y 3, block to z 33
            Wall(new Vector3(-5, -1, 28), new Vector3(5, 3, 29));
            Wall(new Vector3(-5, -1, 29), new Vector3(-4.7f, 3, 33));
            Wall(new Vector3(4.7f, -1, 29), new Vector3(5, 3, 33));
            Floor(new Vector3(-5, 2.5f, 28), new Vector3(5, 3, 33), ground: false);
            Island(new Vector3(-5, -1, 28), new Vector3(5, 3, 33));

            // ---- A3 end floor and A4 road (y 2..3), their rails, plaza 1 yard
            Floor(new Vector3(-6, 2, 59), new Vector3(6, 3, 70));
            Wall(new Vector3(-6.3f, 3, 59), new Vector3(-6, 4.2f, 70.3f));
            Wall(new Vector3(-6, 3, 70), new Vector3(6, 4.2f, 70.3f));
            Floor(new Vector3(6, 2, 60), new Vector3(40, 3, 70));
            Wall(new Vector3(6, 3, 59.7f), new Vector3(40, 4.2f, 60));
            Wall(new Vector3(6, 3, 70), new Vector3(40, 4.2f, 70.3f));
            Floor(new Vector3(40, -1, 58), new Vector3(52, 0, 72));
            Wall(new Vector3(39.7f, 0, 57.7f), new Vector3(52.3f, 1.2f, 58));
            Wall(new Vector3(52, 0, 58), new Vector3(52.3f, 1.2f, 71));
            Wall(new Vector3(39.7f, 0, 58), new Vector3(40, 1.2f, 60));
            Wall(new Vector3(39.7f, 0, 70), new Vector3(40, 1.2f, 71));

            // ---- Plaza 1: entry band z 72..76, aisle x 44..48; outer walls to y 6; north blocks to y 10
            //      with the team gate passages x 32..36 (white) and 56..60 (black). The stations' own
            //      floors and mini-game pieces are rebuilt by the game every round and stay as they are.
            Floor(new Vector3(24, -1, 72), new Vector3(68, 0, 76));
            Floor(new Vector3(44, -1, 76), new Vector3(48, 0, 96));
            Floor(new Vector3(32, -1, 96), new Vector3(36, 0, 101));
            Floor(new Vector3(56, -1, 96), new Vector3(60, 0, 101));
            foreach (var (min, max) in new[]
            {
                (new Vector3(23, -1, 71), new Vector3(24, 6, 101)), (new Vector3(68, -1, 71), new Vector3(69, 6, 101)),
                (new Vector3(24, -1, 71), new Vector3(40, 6, 72)), (new Vector3(52, -1, 71), new Vector3(68, 6, 72)),
                (new Vector3(24, -1, 96), new Vector3(32, 10, 101)), (new Vector3(36, -1, 96), new Vector3(56, 10, 101)),
                (new Vector3(60, -1, 96), new Vector3(68, 10, 101)),
            })
            {
                Wall(min, max);
                Battlements(new Vector3(min.x, max.y, min.z), max);
            }
            // crown arches frame the two team gates; knights guard the middle block, banners on the walls
            // flattened to ~1.3 m deep so it reads as a facade on the block face and barely enters the station
            Put("PR_CrownArch", new Vector3(34, 0, 95.6f), 0, new Vector3(1.25f, 1.25f, 0.25f));
            Put("PR_CrownArch", new Vector3(58, 0, 95.6f), 0, new Vector3(1.25f, 1.25f, 0.25f));
            Put("PR_KnightStatue", new Vector3(40, 10, 98.5f), 90, Vector3.one);
            Put("PR_KnightStatue", new Vector3(52, 10, 98.5f), -90, Vector3.one);
            Put("PR_BannerRed", new Vector3(46, 8.5f, 95.75f), 180, Vector3.one);
            foreach (var x in new[] { 28f, 64f }) Put("PR_BannerNavy", new Vector3(x, 8.5f, 95.75f), 180, Vector3.one * 0.8f);
            foreach (var z in new[] { 80f, 90f })
            {
                Put("PR_BannerNavy", new Vector3(24.25f, 5.2f, z), 90, Vector3.one * 0.8f);
                Put("PR_BannerNavy", new Vector3(67.75f, 5.2f, z), -90, Vector3.one * 0.8f);
            }
            Put("PR_FlagChecker", new Vector3(24, 10, 100), 0, Vector3.one);
            Put("PR_FlagChecker", new Vector3(68, 10, 100), 0, Vector3.one);
            Island(new Vector3(23, -1, 71), new Vector3(69, 0, 101));

            // ---- B and plaza 2: dressed from the grey boxes themselves (see AutoDress)
            AutoDress(course.transform.Find("B_Shared"));
            AutoDress(course.transform.Find("Plaza2"));
            PlazaDecor(course.transform.Find("Plaza2"));

            // ---- C and the tower. Sloped ramps are left as they are (their kit checker material already
            //      matches); the tower gets banners on its shoulders and checker flags on the top corners.
            AutoDress(course.transform.Find("C_Shared"));
            AutoDress(course.transform.Find("T_Tower"));
            Put("PR_BannerNavy", new Vector3(-10.5f, 14.6f, 210.35f), 180, Vector3.one);
            Put("PR_BannerNavy", new Vector3(4.5f, 14.6f, 210.35f), 180, Vector3.one);
            foreach (var x in new[] { -13.15f, 7.15f })
            {
                Put("PR_FlagChecker", new Vector3(x, 16.2f, 210.9f), 0, Vector3.one);
                Put("PR_FlagChecker", new Vector3(x, 16.2f, 230.15f), 0, Vector3.one);
            }

            // ---- the floating course: stone bases under every platform, castle islets all around
            Foundations();
            Islets();
            foreach (Transform piece in root)
                GameObjectUtility.SetStaticEditorFlags(piece.gameObject, StaticEditorFlags.BatchingStatic);

            DressDevices(course.transform);
            SmoothCylinders(course.transform);

            var scene = course.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PawnRush] Art Dress Course01 (Kit v1): S_Start ~ Plaza 1 yard dressed.");
        }

        [MenuItem("ChessFight/Pawn Rush/Remove Art Dress", false, 21)]
        public static void Undress()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            RemoveRoot();
            var course = GameObject.Find("Course01v4_Root");
            if (course != null)
            {
                // skins go; swapped device materials stay until the next Build Course01 v4
                RemoveSkins(course.transform, true);
                SetCovered(course.transform, true);
                foreach (var path in hiddenByAuto) { var t = course.transform.Find(path); if (t && t.TryGetComponent<Renderer>(out var r)) r.enabled = true; }
                EditorSceneManager.MarkSceneDirty(course.scene);
                EditorSceneManager.SaveScene(course.scene);
            }
        }

        static void RemoveRoot()
        {
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
        }

        static void SetCovered(Transform course, bool visible)
        {
            foreach (var path in Covered)
            {
                var t = course.Find(path);
                if (t == null) { Debug.LogWarning("[PawnRush] Art Dress: grey box not found: " + path); continue; }
                foreach (var r in t.GetComponentsInChildren<Renderer>()) r.enabled = visible;
            }
        }

        // ---------------------------------------------------------------- dressing from the grey boxes

        // Grey boxes AutoDress hid in this editor session (for Remove Art Dress; a Build restores all anyway).
        static readonly List<string> hiddenByAuto = new();

        // Every static grey-box cube in a section is dressed by what it is: a checker floor becomes kit floor
        // slabs; a wall becomes block-stone wall (with battlements when it is a tall, thick wall); a climbable
        // wall that is also walked on gets stone sides and a checker top. Moving parts (rigidbodies, obstacle
        // prefabs) are left to DressDevices.
        static void AutoDress(Transform section)
        {
            if (section == null) return;
            var course = section.parent;
            foreach (var r in section.GetComponentsInChildren<MeshRenderer>())
            {
                var t = r.transform;
                if (!r.enabled || t.GetComponentInParent<Rigidbody>() != null) continue;
                // station contents are rebuilt every round; only the gate passage floor is permanent
                if (t.GetComponentInParent<MissionStation>() != null && t.name != "Gate passage") continue;
                if (!t.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null || mf.sharedMesh.name != "Cube") continue;
                string mat = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                if (Mathf.Abs(t.eulerAngles.y % 90f) > .5f || t.eulerAngles.x != 0 || t.eulerAngles.z != 0)
                {
                    // a sloped ramp or rail: kit pieces tiled in its own (tilted) frame, as its children
                    if (mat == "PRC_Floor_Checker") SkinTiled(t, true);
                    else if (mat == "PRC_Wall_NoClimb" || mat == "PRC_Wall_Climbable") SkinTiled(t, false);
                    else continue;
                    r.enabled = false;
                    var rp = AnimationUtility.CalculateTransformPath(t, course);
                    if (!hiddenByAuto.Contains(rp)) hiddenByAuto.Add(rp);
                    continue;
                }
                var b = r.bounds;
                bool walked = t.GetComponent<CourseFloor>() != null;
                if (mat == "PRC_Floor_Checker") Floor(b.min, b.max);
                else if (mat == "PRC_Wall_NoClimb" || mat == "PRC_Wall_Climbable")
                {
                    float thin = Mathf.Max(b.size.x, b.size.z);
                    if (!walked && thin <= 2f && b.size.y > 2f)
                    {
                        // a post: stone pillar (base and cap) stretched to the post
                        Put("PR_Pillar_1p2x3", new Vector3(b.center.x, b.min.y, b.center.z), 0,
                            new Vector3(b.size.x / 1.5f, b.size.y / 3f, b.size.z / 1.5f));
                    }
                    else if (!walked && b.size.y < .8f && Mathf.Min(b.size.x, b.size.z) < 1.2f)
                    {
                        // a beam or thin ledge: bricks would be squashed flat, so plain dark stone
                        SetMat(t, "Stone_CreamDark");
                        continue;
                    }
                    else if (walked && b.size.y > .6f)
                    {
                        Wall(b.min, new Vector3(b.max.x, b.max.y - .5f, b.max.z));
                        Floor(new Vector3(b.min.x, b.max.y - .5f, b.min.z), b.max, ground: false);
                        Island(b.min, b.max);
                    }
                    else
                    {
                        Wall(b.min, b.max);
                        if (b.size.y >= 5f && Mathf.Min(b.size.x, b.size.z) >= .9f)
                            Battlements(new Vector3(b.min.x, b.max.y, b.min.z), b.max);
                    }
                }
                else continue;
                r.enabled = false;
                var path = AnimationUtility.CalculateTransformPath(t, course);
                if (!hiddenByAuto.Contains(path)) hiddenByAuto.Add(path);
            }
        }

        // Kit pieces tiled over a rotated unit cube (ramps, sloped rails), parented to it so they share its
        // tilt. The cube's axes stay the pieces' axes (a 90° turn about y maps axes onto axes, so the
        // parent's non-uniform scale never shears them). Floors: checker slabs on the top face; walls:
        // block-stone pieces along the longer side.
        static void SkinTiled(Transform t, bool floor)
        {
            var s = t.lossyScale;
            if (floor)
            {
                int nx = Mathf.Max(1, Mathf.RoundToInt(s.x / 4.5f)), nz = Mathf.Max(1, Mathf.RoundToInt(s.z / 4.5f));
                for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var go = TileChild(t, "PR_Floor_Checker_4p5");
                    go.transform.localPosition = new Vector3(-.5f + (i + .5f) / nx, .5f + Lift / s.y, -.5f + (j + .5f) / nz);
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = new Vector3(s.x / nx / 4.5f / s.x, (s.y / .5f) / s.y, s.z / nz / 4.5f / s.z);
                }
                return;
            }
            bool alongZ = s.z > s.x;
            float length = alongZ ? s.z : s.x, depth = alongZ ? s.x : s.z;
            int n = Mathf.Max(1, Mathf.RoundToInt(length / 4f));
            for (int i = 0; i < n; i++)
            {
                var go = TileChild(t, "PR_Wall_4x3");
                float u = -.5f + (i + .5f) / n;
                go.transform.localPosition = alongZ ? new Vector3(0, -.5f, u) : new Vector3(u, -.5f, 0);
                go.transform.localRotation = Quaternion.Euler(0, alongZ ? 90 : 0, 0);
                // child x runs along the parent's long axis, child z across it
                go.transform.localScale = new Vector3(length / n / 4f / length, (s.y / 3f) / s.y, depth / depth);
            }
        }

        static GameObject TileChild(Transform parent, string part)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(KitDir + "/" + part + ".fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            go.name = SkinPrefix + part;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            return go;
        }

        // A mission plaza's decor, measured from its blocks: crown arches over the two team gates, knights on
        // the middle north block, banners on the walls, checker flags on the north corners.
        static void PlazaDecor(Transform plaza)
        {
            if (plaza == null) return;
            Bounds B(string n) { var t = plaza.Find(n); return t != null && t.TryGetComponent<Renderer>(out var r) ? r.bounds : default; }
            var mid = B("North block middle");
            var west = B("Outer wall W");
            var east = B("Outer wall E");
            float floorY = B("Aisle").max.y;
            Island(new Vector3(west.min.x, B("Aisle").min.y, west.min.z), new Vector3(east.max.x, floorY, mid.max.z));
            foreach (var station in plaza.GetComponentsInChildren<MissionStation>(true))
            {
                var gp = station.transform.Find("Gate passage");
                if (gp == null || !gp.TryGetComponent<Renderer>(out var gr)) continue;
                float s = gr.bounds.size.x / 3.4f * 1.05f; // the arch's opening is 3.4 m at scale 1
                float depth = 5.07f * s * .2f;
                Put("PR_CrownArch", new Vector3(gr.bounds.center.x, floorY, mid.min.z - depth / 2 + .4f), 0, new Vector3(s, s, .2f));
            }
            float top = mid.max.y, face = mid.min.z;
            Put("PR_KnightStatue", new Vector3(mid.min.x + 2, top, mid.center.z), 90, Vector3.one);
            Put("PR_KnightStatue", new Vector3(mid.max.x - 2, top, mid.center.z), -90, Vector3.one);
            Put("PR_BannerRed", new Vector3(mid.center.x, top - 1.5f, face - .25f), 180, Vector3.one);
            float wallTop = west.max.y - .8f;
            for (int i = 1; i <= 2; i++)
            {
                float z = Mathf.Lerp(west.min.z, face, i / 3f);
                Put("PR_BannerNavy", new Vector3(west.max.x + .25f, wallTop, z), 90, Vector3.one * .8f);
                Put("PR_BannerNavy", new Vector3(east.min.x - .25f, wallTop, z), -90, Vector3.one * .8f);
            }
            Put("PR_FlagChecker", new Vector3(west.center.x, top, mid.max.z - 1), 0, Vector3.one);
            Put("PR_FlagChecker", new Vector3(east.center.x, top, mid.max.z - 1), 0, Vector3.one);
        }

        // ---------------------------------------------------------------- moving devices

        const string SkinPrefix = "ArtSkin_";

        // Devices move, so their dress is parented to the moving grey-box piece itself (a skin that
        // copies its box) or is a material swap. Colliders, rigidbodies and motion scripts are untouched.
        static void DressDevices(Transform course)
        {
            // Dress() starts from a fresh Build, so there are no old skins here (and AutoDress's ramp tiles,
            // made just before, use the same prefix and must stay).
            var start = course.Find("S_Start/Start bar");
            if (start != null) Skin(start, "PR_StartBar_16");

            // team gates (they rise to open, the skin is their child): bars in the team's colour
            foreach (var station in course.GetComponentsInChildren<MissionStation>(true))
            {
                var gate = station.transform.Find("Team gate");
                if (gate == null) continue;
                var skin = Skin(gate, "PR_Portcullis_4");
                bool black = station.name.Contains("흑");
                SwapMat(skin, "Iron", black ? "Stone_Charcoal" : "Stone_Cream");
                var band = station.transform.Find("Gate colour");
                if (band != null) SetMat(band, "Gold");
            }

            // B, C and the tower: devices found by name anywhere in the course, recoloured part by part
            // (renderer name -> kit material). Moving parts keep their motion; nothing is resized.
            var parts = new Dictionary<string, string>
            {
                // jump pads
                { "Jump pad base", "Stone_CreamDark" }, { "Spring plate", "Gold" }, { "Spring", "Iron" },
                // hammers
                { "Back pedestal", "Stone_CreamDark" }, { "Axle", "Gold" }, { "Handle", "Wood" },
                { "Hammer head", "Stone_Charcoal" }, { "Golden striking face", "Gold" },
                // clock gates
                { "Gate pillar", "Stone_CreamDark" }, { "Clock arch", "Stone_Cream" }, { "Clock face", "Gold" },
                { "Gate panel", "Wood" }, { "Vertical inset", "Gold" },
                // pendulums
                { "Ceiling mount", "Gold" }, { "Suspension cable", "Iron" }, { "Chain collar", "Iron" }, { "Ball", "Stone_Charcoal" },
            };
            foreach (var t in course.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("Device_RotatingBoardRound"))
                {
                    SetMat(t, "Tile_White");
                    foreach (Transform sq in t) if (sq.name == "Dark square") SetMat(sq, "Tile_Black");
                }
                else if (t.name.StartsWith("Device_RookWall"))
                {
                    var wall = t.Find("Wall"); if (wall != null) Skin(wall, "PR_Wall_4x3");
                    foreach (Transform b in t) if (b.name == "Battlement") SetMat(b, "Stone_Cream");
                }
                else if (t.name == "Hammer head" && t.TryGetComponent<MeshFilter>(out _))
                {
                    // the mallet skin has its own gold faces; the grey box's would double them
                    Skin(t, "PR_HammerHead");
                    foreach (Transform s in t.parent) if (s.name == "Golden striking face" && s.TryGetComponent<Renderer>(out var fr)) fr.enabled = false;
                }
                else if (t.name == "Gate panel" && t.TryGetComponent<MeshFilter>(out _))
                {
                    // plank door; the grey box's gold inset strips would poke through it
                    Skin(t, "PR_DoorPanel");
                    foreach (Transform s in t.parent) if (s.name == "Vertical inset" && s.TryGetComponent<Renderer>(out var ir)) ir.enabled = false;
                }
                else if (t.name == "Clock hand")
                    foreach (var r in t.GetComponentsInChildren<Renderer>()) SetMat(r.transform, "Stone_Charcoal");
                else if (t.name == "8th rank crown" || t.name == "Crown circle")
                    foreach (var r in t.GetComponentsInChildren<Renderer>()) SetMat(r.transform, "Gold");
                else if (t.name.StartsWith("11 Pendulum"))
                    foreach (var r in t.GetComponentsInChildren<Renderer>())
                    {
                        // B's pendulum balls are named by the obstacle prefab; spheres are the weights
                        var mesh = r.TryGetComponent<MeshFilter>(out var f) && f.sharedMesh != null ? f.sharedMesh.name : "";
                        if (r.name.StartsWith("Swing boundary")) continue;
                        if (mesh == "Sphere") SetMat(r.transform, "Stone_Charcoal");
                        else if (r.name != "Ceiling mount" && r.transform.GetComponentInParent<Rigidbody>() != null) SetMat(r.transform, "Iron");
                    }
                if (t.TryGetComponent<Renderer>(out _) && parts.TryGetValue(t.name, out var kitMat) && !t.name.StartsWith("ArtSkin_"))
                    SetMat(t, kitMat);
            }

            var a = course.Find("A_Shared");
            if (a == null) return;
            foreach (Transform t in a)
            {
                if (t.name.StartsWith("Device_SpinBar"))
                {
                    var bar = t.Find("Bar"); if (bar != null) Skin(bar, "PR_StripeBar_12");
                    var hub = t.Find("Hub"); if (hub != null) Skin(hub, "PR_SpinHub");
                }
                else if (t.name.StartsWith("01 Spinning disc"))
                {
                    SetMat(t.Find("Solid disc"), "Stone_CreamDark");
                    SetMat(t.Find("Slow outer ring/Ring plate"), "Tile_Black");
                    SetMat(t.Find("Middle ring/Ring plate"), "Tile_White");
                    SetMat(t.Find("Fast center/Ring plate"), "Trim_Red");
                    foreach (var r in t.GetComponentsInChildren<Renderer>())
                        if (r.name == "Shaft" || r.name == "Arrowhead") SetMat(r.transform, "Gold");
                }
                else if (t.name == "Disc post") Skin(t, "PR_Column");
                else if (t.name.StartsWith("PR_06_Sliding10"))
                {
                    foreach (Transform c in t)
                    {
                        if (c.name == "Wall housing") SetMat(c, "Stone_CreamDark");
                        var wall = c.Find("Rook wall"); if (wall != null) Skin(wall, "PR_Wall_4x3");
                        foreach (Transform b in c) if (b.name == "Battlement") SetMat(b, "Stone_Cream");
                    }
                }
            }
        }

        // Child of target that fills target's own mesh box exactly; hides target's renderer.
        static GameObject Skin(Transform target, string part)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(KitDir + "/" + part + ".fbx");
            var mf = target.GetComponent<MeshFilter>();
            if (model == null || mf == null || mf.sharedMesh == null) { Debug.LogError("[PawnRush] Art Dress: cannot skin " + target.name); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, target);
            go.name = SkinPrefix + part;
            var b = new Bounds();
            bool first = true;
            foreach (var f in go.GetComponentsInChildren<MeshFilter>())
            {
                var mb = f.sharedMesh.bounds;
                var wb = new Bounds(f.transform.localPosition + mb.center, mb.size); // kit FBX: child at origin, no rotation
                if (first) { b = wb; first = false; } else b.Encapsulate(wb);
            }
            var m = mf.sharedMesh.bounds;
            var scale = new Vector3(m.size.x / b.size.x, m.size.y / b.size.y, m.size.z / b.size.z);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = scale;
            go.transform.localPosition = m.center - Vector3.Scale(scale, b.center);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var r = target.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            return go;
        }

        // Replace one kit material (by palette name) on every renderer of a placed piece.
        static void SwapMat(GameObject piece, string from, string to)
        {
            if (piece == null) return;
            var target = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/PR_" + to + ".mat");
            foreach (var r in piece.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name == "PR_" + from) mats[i] = target;
                r.sharedMaterials = mats;
            }
        }

        static void RemoveSkins(Transform course, bool showGreyBox)
        {
            var doomed = new List<GameObject>();
            foreach (var t in course.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(SkinPrefix)) doomed.Add(t.gameObject);
            foreach (var g in doomed)
            {
                var owner = g.transform.parent;
                Object.DestroyImmediate(g);
                if (showGreyBox && owner != null && owner.TryGetComponent<Renderer>(out var r)) r.enabled = true;
            }
        }

        static void SetMat(Transform t, string palette)
        {
            if (t == null || !t.TryGetComponent<Renderer>(out var r)) return;
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/PR_" + palette + ".mat");
            if (m != null) r.sharedMaterial = m;
        }

        // ---------------------------------------------------------------- kit placement

        static GameObject Put(string part, Vector3 pos, float rotY, Vector3 scale)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(KitDir + "/" + part + ".fbx");
            if (model == null) { Debug.LogError("[PawnRush] Art Dress: missing " + part); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, rotY, 0));
            go.transform.localScale = scale;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            return go;
        }

        // Fill a box with a kit piece along its longer horizontal side, stretched to fit exactly.
        // module = piece size (x along its length, y height, z depth) at scale 1; pivot bottom centre.
        static void Run(string part, Vector3 module, Vector3 min, Vector3 max, float pivotY)
        {
            var size = max - min;
            bool alongZ = size.z > size.x;
            float length = alongZ ? size.z : size.x;
            float depth = alongZ ? size.x : size.z;
            int n = Mathf.Max(1, Mathf.RoundToInt(length / module.x));
            float seg = length / n;
            var scale = new Vector3(seg / module.x, size.y / module.y, depth / module.z);
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) * seg;
                var p = alongZ
                    ? new Vector3((min.x + max.x) / 2, min.y + pivotY * size.y, min.z + u)
                    : new Vector3(min.x + u, min.y + pivotY * size.y, (min.z + max.z) / 2);
                Put(part, p, alongZ ? 90 : 0, scale);
            }
        }

        // A block of stone. Thick blocks get a 1 m wall on each of their four sides (one run through the
        // whole depth would stretch the bricks along the sides into long planks).
        static void Wall(Vector3 min, Vector3 max)
        {
            var s = max - min;
            if (Mathf.Min(s.x, s.z) <= 2.5f) { Run("PR_Wall_4x3", new Vector3(4, 3, 1), min, max, 0); return; }
            const float t = 1f;
            Run("PR_Wall_4x3", new Vector3(4, 3, 1), min, new Vector3(max.x, max.y, min.z + t), 0);
            Run("PR_Wall_4x3", new Vector3(4, 3, 1), new Vector3(min.x, min.y, max.z - t), max, 0);
            Run("PR_Wall_4x3", new Vector3(4, 3, 1), new Vector3(min.x, min.y, min.z + t), new Vector3(min.x + t, max.y, max.z - t), 0);
            Run("PR_Wall_4x3", new Vector3(4, 3, 1), new Vector3(max.x - t, min.y, min.z + t), new Vector3(max.x, max.y, max.z - t), 0);
            // lid over the hollow middle (walked tops get a checker floor over it from AutoDress)
            Run("PR_Wall_4x3", new Vector3(4, 3, 1), new Vector3(min.x + t, max.y - .3f, min.z + t), new Vector3(max.x - t, max.y, max.z - t), 0);
        }
        static void Trim(Vector3 min, Vector3 max) => Run("PR_Trim_RedWhite_4p5", new Vector3(4.5f, 0.5f, 0.5f), min, max, 0);

        static void Battlements(Vector3 min, Vector3 max)
        {
            max.y = min.y + 1.4f;
            var module = new Vector3(4, 1.4f, 1);
            if (Mathf.Min(max.x - min.x, max.z - min.z) <= 1.6f)
            {
                Run("PR_Battlement_4m", module, min, max, 0);
                return;
            }
            // 10-22 (승규 "딱딱 맞춰줘"): a deep block (the plazas' 5 m north blocks) gets a parapet on each
            // edge, not one slab of merlons stretched across its whole top
            const float t = .8f;
            Run("PR_Battlement_4m", module, min, new Vector3(max.x, max.y, min.z + t), 0);
            Run("PR_Battlement_4m", module, new Vector3(min.x, min.y, max.z - t), max, 0);
            if (max.z - min.z - 2 * t >= 1f)
            {
                Run("PR_Battlement_4m", module, new Vector3(min.x, min.y, min.z + t), new Vector3(min.x + t, max.y, max.z - t), 0);
                Run("PR_Battlement_4m", module, new Vector3(max.x - t, min.y, min.z + t), new Vector3(max.x, max.y, max.z - t), 0);
            }
        }

        // ---------------------------------------------------------------- floating course

        // Footprints (min/max box) that float and get a stone base under them.
        static readonly List<(Vector3 min, Vector3 max)> islands = new();

        static void Island(Vector3 min, Vector3 max) => islands.Add((min, max));

        // A stand-alone tower over the void gets its own stone base, so it does not hang in the air.
        static void TowerBase(Vector3 foot) =>
            Put("PR_Foundation", foot, 0, new Vector3(4.6f / 4f, 4f / 5f, 4.6f / 4f));

        // A stepped block-stone base under each island, cut into roughly square pieces along its length
        // (one stretched base under a long road would stretch its stones). Inset 0.5 m so a base never
        // pokes into a neighbouring lower platform; deeper under wider platforms.
        static void Foundations()
        {
            foreach (var (min, max) in islands)
            {
                float w = max.x - min.x - 1f, d = max.z - min.z - 1f;
                if (w < 1f || d < 1f) continue;
                float shortSide = Mathf.Min(w, d), longSide = Mathf.Max(w, d);
                float depth = Mathf.Clamp(shortSide * .55f, 2.5f, 9f);
                int n = Mathf.Max(1, Mathf.RoundToInt(longSide / Mathf.Max(shortSide, 4f)));
                float seg = longSide / n;
                for (int i = 0; i < n; i++)
                {
                    bool alongX = w >= d;
                    float cx = alongX ? min.x + .5f + (i + .5f) * seg : (min.x + max.x) / 2;
                    float cz = alongX ? (min.z + max.z) / 2 : min.z + .5f + (i + .5f) * seg;
                    float sx = alongX ? seg : w, sz = alongX ? d : seg;
                    Put("PR_Foundation", new Vector3(cx, min.y, cz), 0, new Vector3(sx / 4f, depth / 5f, sz / 4f));
                }
            }
        }

        // Small floating castles around the course (concept art 2's sky full of castles). Hand placed
        // clear of the course (x -26..69, z -20..235); the far ones are big and fade into the haze.
        // 10-22 (승규 "오픈 월드처럼, 하늘 다리", then "딱딱 맞춰줘"): islets in chains (west, east, and the way to the
        // palace) so every bridge runs straight across, square to both rims; the rest float free.
        static readonly (float x, float y, float z, float size)[] IsletSpots =
        {
            // 0-5 the west chain and its two side islets
            (-70, 6, 40, 22), (-70, 8, 100, 24), (-70, 10, 165, 26), (-70, 8, 230, 22), (-115, 4, 100, 18), (-115, 10, 165, 18),
            // 6-11 the east chain and its two side islets
            (100, 4, 30, 22), (100, 6, 95, 24), (100, 10, 160, 22), (100, 6, 225, 22), (148, 2, 95, 18), (148, 8, 225, 20),
            // 12-14 the way to the palace: the approach islet (the palace's viaduct starts there) and its neighbours
            (-3, 2, 300, 26), (-40, 2, 300, 20), (34, 3, 300, 20),
            // 15- free: small low rocks, islets behind the start, and three far giants in the haze
            (-20, -16, 60, 12), (88, -14, 125, 12), (-34, 18, -46, 14), (60, -12, -52, 18),
            (-165, 30, 120, 48), (205, 24, 140, 52), (40, 36, -140, 44),
        };

        static readonly (int a, int b)[] BridgeLinks =
        {
            (0, 1), (1, 2), (2, 3), (1, 4), (2, 5),
            (6, 7), (7, 8), (8, 9), (7, 10), (9, 11),
            (12, 13), (12, 14),
        };

        static void Islets()
        {
            landings.Clear();
            var links = PlanBridges();
            // the palace's viaduct lands on the approach islet's far rim, as wide as the viaduct
            var palaceLanding = new Landing(PalaceIslet, Vector3.forward, 0f, PalaceViaductHalf);
            landings.Add(palaceLanding);
            for (int i = 0; i < IsletSpots.Length; i++)
            {
                var (x, y, z, size) = IsletSpots[i];
                Islet(i, new Vector3(x, y, z), size);
            }
            foreach (var (a, b, stone) in links) Bridge(Tip(a), Tip(b), stone);
            Palace(Tip(palaceLanding));
        }

        // ---- the royal palace (10-22, 승규 "진짜 압도할 만한 성으로", no blue roofs): one Blender model
        // (ArtSource/PawnRush/build_palace.py) on a floating crag beyond the approach islet, its gate facing the
        // course; its viaduct ends PalaceViaductEnd from the model's origin, on the approach islet's landing.

        const int PalaceIslet = 12;
        const float PalaceViaductHalf = 4.8f;
        static readonly Vector3 PalaceViaductEnd = new(0f, -8f, -155f);
        const string PalaceMatDir = MatDir + "/Palace";

        static void Palace(Vector3 landingTip)
        {
            // the model's gate looks along +z after import; turned round to face the course
            var go = Put("PR_Palace", landingTip - PalaceViaductEnd, 180, Vector3.one);
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null) mats[i] = PalaceMaterial(mats[i].name) ?? mats[i];
                r.sharedMaterials = mats;
            }
        }

        // The model's materials by name: the kit's gold, iron and banners, and the palace's own stone, rock,
        // windows and water under Materials/Palace. v2 (10-22, 승규 "그래픽이 아쉬워"): ashlar blocks on the
        // walls and stratified rock (ArtSource/PawnRush/make_palace_textures.py, one tile = one UV unit) and
        // flowing water (PR_Waterfall.shader); set again on every Dress so changes here reach the saved materials.
        const string PalaceTexDir = "Assets/Art/PawnRushMap/Textures/Palace";

        static Material PalaceMaterial(string name)
        {
            switch (name)
            {
                case "Gold": return AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/PR_Gold.mat");
                case "Iron": return AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/PR_Iron.mat");
                case "Banner_Red": return AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/PR_Banner_Red.mat");
                case "Banner_Navy": return AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/PR_Banner_Navy.mat");
                case "Palace_Stone": case "Palace_Trim": case "Palace_Dark": case "Palace_Rock":
                case "Gem_Red": case "Palace_Window": case "Palace_Water": break;
                default: return null;
            }
            string path = PalaceMatDir + "/PR_" + name + ".mat";
            string shaderName = name == "Palace_Water" ? "ChessFight/Pawn Rush Waterfall" : "Universal Render Pipeline/Lit";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = m == null;
            if (created)
            {
                if (!AssetDatabase.IsValidFolder(PalaceMatDir)) AssetDatabase.CreateFolder(MatDir, "Palace");
                m = new Material(Shader.Find(shaderName));
            }
            else if (m.shader.name != shaderName) m.shader = Shader.Find(shaderName);
            Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(PalaceTexDir + "/" + n + ".png");
            void Stone(Color c, float smooth, string tex, float bump)
            {
                m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", smooth);
                m.SetTexture("_BaseMap", Tex(tex + "_Albedo"));
                m.SetTexture("_BumpMap", Tex(tex + "_Normal"));
                m.SetFloat("_BumpScale", bump);
                m.EnableKeyword("_NORMALMAP");
                m.SetTextureScale("_BaseMap", Vector2.one);
            }
            switch (name)
            {
                case "Palace_Stone": Stone(new Color(1f, .96f, .88f), .16f, "PR_Ashlar", 1f); break;
                case "Palace_Trim": Stone(new Color(1f, .99f, .95f), .26f, "PR_Ashlar", .8f); break;
                case "Palace_Dark": Stone(new Color(.84f, .79f, .72f), .1f, "PR_Ashlar", 1.1f); break;
                case "Palace_Rock": Stone(new Color(.7f, .6f, .48f), .06f, "PR_PalaceRock", 1.3f); break;
                case "Gem_Red":
                    m.SetColor("_BaseColor", new Color(.75f, .04f, .08f));
                    m.SetFloat("_Smoothness", .9f);
                    break;
                case "Palace_Window":
                    // warm lit glass: lamps inside, seen from afar
                    m.SetColor("_BaseColor", new Color(.08f, .07f, .07f));
                    m.SetFloat("_Smoothness", .85f);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(1f, .62f, .3f) * 1.6f);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    break;
                case "Palace_Water":
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    break;
            }
            if (created) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        // ---- bridges that land: 10-22 (승규 "딱딱 맞춰줘", a bridge's end hung inside an islet's wall). A bridge
        // leaves an islet through a gap in its battlement, from a stone landing with two pillars that stands
        // out of the rim; it runs straight across (square to both rims) whenever the islets overlap side-on.

        const float LandingOut = 2f, LandingHalfWidth = 1.8f;

        // One end of a bridge: islet, the rim's outward normal (an axis) and the offset along that rim.
        readonly struct Landing
        {
            public readonly int Islet;
            public readonly Vector3 Normal;
            public readonly float Lateral;
            public readonly float HalfWidth;
            public Landing(int islet, Vector3 normal, float lateral, float halfWidth = LandingHalfWidth)
            {
                Islet = islet; Normal = normal; Lateral = lateral; HalfWidth = halfWidth;
            }
            public Vector3 Side => new Vector3(Mathf.Abs(Normal.z), 0f, Mathf.Abs(Normal.x));
        }

        static readonly List<Landing> landings = new();

        static Vector3 Centre(int i) => new Vector3(IsletSpots[i].x, IsletSpots[i].y, IsletSpots[i].z);
        static float Half(int i) => IsletSpots[i].size / 2;
        static float TowerK(int i) => Mathf.Clamp(IsletSpots[i].size / 18f, .8f, 3f);

        // The stretch of a rim a landing may use: clear of the corner towers.
        static float Usable(int i) => Mathf.Max(0f, Half(i) - 4.4f * TowerK(i) - LandingHalfWidth - .3f);

        static Vector3 Rim(Landing l) => Centre(l.Islet) + l.Normal * Half(l.Islet) + l.Side * l.Lateral;
        static Vector3 Tip(Landing l) => Rim(l) + l.Normal * LandingOut + Vector3.up * .03f;

        // The links of BridgeLinks, each straight across where the two rims' usable stretches overlap (they
        // all do, by the layout); long or heavy links in stone, the rest rope and plank.
        static List<(Landing, Landing, bool)> PlanBridges()
        {
            var links = new List<(Landing, Landing, bool)>();
            foreach (var (i, j) in BridgeLinks)
            {
                var d = Centre(j) - Centre(i);
                bool alongX = Mathf.Abs(d.x) >= Mathf.Abs(d.z);
                var n = alongX ? new Vector3(Mathf.Sign(d.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(d.z));
                float ci = alongX ? IsletSpots[i].z : IsletSpots[i].x, cj = alongX ? IsletSpots[j].z : IsletSpots[j].x;
                float lo = Mathf.Max(ci - Usable(i), cj - Usable(j)), hi = Mathf.Min(ci + Usable(i), cj + Usable(j));
                float wi, wj;
                if (lo <= hi) wi = wj = (lo + hi) / 2;
                else
                {
                    Debug.LogWarning($"[PawnRush] Art Dress: islets {i} and {j} are not side by side; their bridge is angled");
                    wi = Mathf.Clamp(cj, ci - Usable(i), ci + Usable(i));
                    wj = Mathf.Clamp(ci, cj - Usable(j), cj + Usable(j));
                }
                var a = new Landing(i, n, wi - ci);
                var b = new Landing(j, -n, wj - cj);
                landings.Add(a);
                landings.Add(b);
                float gap = (alongX ? Mathf.Abs(d.x) : Mathf.Abs(d.z)) - Half(i) - Half(j) - 2 * LandingOut;
                links.Add((a, b, gap > 26f || Mathf.Min(IsletSpots[i].size, IsletSpots[j].size) > 23f));
            }
            return links;
        }

        static void Bridge(Vector3 from, Vector3 to, bool stone)
        {
            var span = to - from;
            float length = span.magnitude;
            var rot = Quaternion.LookRotation(span.normalized, Vector3.up);
            float s = Mathf.Clamp(length / 12f, 1f, 2.5f);
            Put(stone ? "PR_StoneBridge" : "PR_PlankBridge", (from + to) / 2, 0, new Vector3(1f, stone ? 1f : s, length / 12f))
                .transform.rotation = rot;
        }

        // A landing: a checker-topped stone pier out of the rim, a stepped base under it and two pillars at
        // its outer corners that the bridge's ropes or parapets meet.
        static void LandingPier(Landing l)
        {
            var rim = Rim(l);
            var n = l.Normal;
            var side = l.Side;
            float top = rim.y + .03f;
            var a = rim - n * .4f - side * l.HalfWidth;
            var b = rim + n * LandingOut + side * l.HalfWidth;
            Floor(new Vector3(Mathf.Min(a.x, b.x), top - .5f, Mathf.Min(a.z, b.z)), new Vector3(Mathf.Max(a.x, b.x), top, Mathf.Max(a.z, b.z)), ground: false);
            var mid = rim + n * (LandingOut / 2);
            Put("PR_Foundation", new Vector3(mid.x, top - .5f, mid.z), 0,
                new Vector3((Mathf.Abs(b.x - a.x) - .2f) / 4f, 2.2f / 5f, (Mathf.Abs(b.z - a.z) - .2f) / 4f));
            foreach (int s in new[] { -1, 1 })
                Put("PR_Pillar_1p2x3", new Vector3(0, top, 0) + new Vector3(rim.x, 0, rim.z) + n * (LandingOut - .45f) + side * s * (l.HalfWidth - .4f), 0,
                    new Vector3(.5f, .6f, .5f));
        }

        // A battlement along one rim of an islet, with gaps where its landings are.
        static void RimWall(int islet, Vector3 normal, Vector3 min, Vector3 max)
        {
            var side = new Vector3(Mathf.Abs(normal.z), 0f, Mathf.Abs(normal.x));
            float from = Vector3.Dot(min, side), to = Vector3.Dot(max, side);
            float centre = Vector3.Dot(Centre(islet), side);
            var cuts = new List<(float, float)>();
            foreach (var l in landings)
                if (l.Islet == islet && l.Normal == normal)
                    cuts.Add((centre + l.Lateral - l.HalfWidth, centre + l.Lateral + l.HalfWidth));
            cuts.Sort((p, q) => p.Item1.CompareTo(q.Item1));
            float at = from;
            foreach (var (c0, c1) in cuts.Concat(new[] { (to, to) }))
            {
                if (c0 - at >= 1f)
                {
                    var lo = min + side * (at - Vector3.Dot(min, side));
                    var hi = max + side * (c0 - Vector3.Dot(max, side));
                    Battlements(lo, hi);
                }
                at = Mathf.Max(at, c1);
            }
        }

        static void Islet(int index, Vector3 c, float size)
        {
            var rng = new System.Random(index * 7919 + 13);
            float half = size / 2;
            Put("PR_Foundation", c, rng.Next(4) * 90, new Vector3(size / 4f, size * .75f / 5f, size / 4f));
            // seen from afar: ~9 m slabs (4.5 m checker squares) keep the piece count down
            float tile = size / Mathf.Max(1, Mathf.RoundToInt(size / 9f));
            int n = Mathf.RoundToInt(size / tile);
            for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                Put("PR_Floor_Checker_4p5", new Vector3(c.x - half + (i + .5f) * tile, c.y + .03f, c.z - half + (j + .5f) * tile), 0,
                    new Vector3(tile / 4.5f, 1f, tile / 4.5f));
            // low battlement wall on the rim, open where bridges land
            RimWall(index, Vector3.back, new Vector3(c.x - half, c.y, c.z - half), new Vector3(c.x + half, c.y, c.z - half + .8f));
            RimWall(index, Vector3.forward, new Vector3(c.x - half, c.y, c.z + half - .8f), new Vector3(c.x + half, c.y, c.z + half));
            RimWall(index, Vector3.left, new Vector3(c.x - half, c.y, c.z - half + .8f), new Vector3(c.x - half + .8f, c.y, c.z + half - .8f));
            RimWall(index, Vector3.right, new Vector3(c.x + half - .8f, c.y, c.z - half + .8f), new Vector3(c.x + half, c.y, c.z + half - .8f));
            foreach (var l in landings) if (l.Islet == index) LandingPier(l);
            // towers on two or three corners, each with a flag; a banner flat on the first one's face
            float k = TowerK(index);
            int towers = 2 + rng.Next(2);
            for (int t = 0; t < towers; t++)
            {
                float sx = (t & 1) == 0 ? -1 : 1, sz = t < 2 ? -1 : 1;
                if (t == 1) sz = 1;
                float h = (float)(1 + rng.NextDouble() * .8);
                var at = new Vector3(c.x + sx * (half - 2.2f * k), c.y, c.z + sz * (half - 2.2f * k));
                Put("PR_Tower_4x6", at, 0, new Vector3(k, k * h, k));
                Put("PR_FlagChecker", at + Vector3.up * 6.3f * k * h, 0, Vector3.one * k);
                // the banner's back (0.12 thick at scale 1) against the tower's face at 2.15
                if (t == 0) Put("PR_BannerNavy", at + new Vector3(0, 5.6f * k * h, -(2.15f + .12f * .8f) * k), 180, Vector3.one * k * .8f);
            }
            // a centre piece: a crown arch or a knight
            if (rng.Next(2) == 0) Put("PR_CrownArch", c, rng.Next(4) * 90, Vector3.one * k * .9f);
            else Put("PR_KnightStatue", c, rng.Next(4) * 90, Vector3.one * k * 1.4f);
        }

        // A giant chess statue (ArtSource/PawnRush/clean_statues.py, Tripo models cleaned up) on a kit tower
        // standing at `towerFoot`: its 4 m pedestal sits on the tower's 6.3 m lip and buries the corner merlons.
        // The model looks along +z after import; rotY turns it. All one weathered stone (승규 "돌 동상같이, 금색 빼고").
        static void Statue(string piece, Vector3 towerFoot, float rotY)
        {
            var go = Put("PR_Statue_" + piece, towerFoot + Vector3.up * 6.3f, rotY, Vector3.one);
            if (go == null) return;
            var stone = StatueStone();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = stone;
                r.sharedMaterials = mats;
            }
        }

        static Material StatueStone()
        {
            string path = MatDir + "/PR_Statue_Stone.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = m == null;
            if (created) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", new Color(.86f, .83f, .76f));
            m.SetFloat("_Smoothness", .12f);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ResDir + "/PR_StoneGrain_Albedo.png"));
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ResDir + "/PR_StoneGrain_Normal.png"));
            m.SetFloat("_BumpScale", .9f);
            m.EnableKeyword("_NORMALMAP");
            m.SetTextureScale("_BaseMap", Vector2.one * 8f);
            if (created) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        // Checker slabs: pivot on the top face, so the dress top = box top (+Lift). A ground floor floats:
        // it is also an island (stone base under it).
        static void Floor(Vector3 min, Vector3 max, bool ground = true)
        {
            if (ground) Island(min, max);
            var size = max - min;
            int nx = Mathf.Max(1, Mathf.RoundToInt(size.x / 4.5f));
            int nz = Mathf.Max(1, Mathf.RoundToInt(size.z / 4.5f));
            float sx = size.x / nx, sz = size.z / nz;
            var scale = new Vector3(sx / 4.5f, size.y / 0.5f, sz / 4.5f);
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
                Put("PR_Floor_Checker_4p5", new Vector3(min.x + (i + 0.5f) * sx, max.y + Lift, min.z + (j + 0.5f) * sz), 0, scale);
        }

        // ---------------------------------------------------------------- sky and sun (concept art 2)

        const string SkyMatPath = "Assets/Art/PawnRushMap/Sky/PR_Sky.mat";
        static readonly Color Horizon = new(0.66f, 0.84f, 0.99f);

        // Blue sky with clouds and a cloud sea below, a warm sun from behind the start (front light on
        // everything the runners face), sky-tinted ambient, and haze so the far course fades into the sky.
        [MenuItem("ChessFight/Pawn Rush/Art Kit: Apply Sky", false, 23)]
        public static void ApplySky()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMatPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("ChessFight/Pawn Rush Sky"));
                // tuned 10-17 against concept art 2: mid-size cumulus with wide blue gaps
                sky.SetFloat("_Scale", 2.8f);
                sky.SetFloat("_Coverage", 0.40f);
                sky.SetFloat("_SeaCoverage", 0.66f);
                AssetDatabase.CreateAsset(sky, SkyMatPath);
            }
            var sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            }
            if (sun != null)
            {
                // 10-17 (승규 Play 녹화 "다 붕 떠 보여"): from behind the runners the shadows fell where the
                // pieces hide them and the bright ambient washed them out. Sun from the front-left, full-
                // strength shadows, darker ambient: everything visibly stands on the floor.
                sun.transform.rotation = Quaternion.Euler(48, -55, 0);
                sun.color = new Color(1f, 0.95f, 0.86f);
                sun.intensity = 1.9f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 1f;
                RenderSettings.sun = sun;
                sky.SetVector("_SunDir", -sun.transform.forward);
                EditorUtility.SetDirty(sun);
            }
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.52f, 0.70f);
            RenderSettings.ambientEquatorColor = new Color(0.40f, 0.43f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.27f, 0.27f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Horizon;
            // 10-22: the palace (~470 m from the start, ~190 m tall) must stay readable through the haze
            RenderSettings.fogStartDistance = 160f;
            RenderSettings.fogEndDistance = 900f;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.backgroundColor = Horizon;
                EditorUtility.SetDirty(cam);
            }
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- run-time look (MapLook)

        const string ResDir = "Assets/Art/PawnRushMap/Resources";

        // What MapLook loads at run time: the 96-sided cylinder, the water material and the surface textures
        // (copied into ResDir by hand from ArtSource/PawnRush/textures; normal maps marked as such here).
        [MenuItem("ChessFight/Pawn Rush/Art Kit: Run-time Look", false, 25)]
        public static void SetupRuntimeLook()
        {
            if (!AssetDatabase.IsValidFolder(ResDir)) AssetDatabase.CreateFolder("Assets/Art/PawnRushMap", "Resources");
            var meshPath = ResDir + "/PR_CylinderSmooth.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) == null)
                AssetDatabase.CreateAsset(SmoothCylinderMesh(96), meshPath);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ResDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("_Normal.png")) continue;
                if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.SaveAndReimport();
                }
            }
            var waterPath = ResDir + "/PR_Water.mat";
            var water = AssetDatabase.LoadAssetAtPath<Material>(waterPath);
            if (water == null)
            {
                water = new Material(Shader.Find("ChessFight/Pawn Rush Water"));
                AssetDatabase.CreateAsset(water, waterPath);
            }
            var waves = AssetDatabase.LoadAssetAtPath<Texture2D>(ResDir + "/PR_Water_Normal.png");
            if (waves != null) water.SetTexture("_WaveMap", waves);
            EditorUtility.SetDirty(water);
            AssetDatabase.SaveAssets();
        }

        // Unity's cylinder size (diameter 1, height 2, centred) with `sides` sides: smooth side, flat caps.
        static Mesh SmoothCylinderMesh(int sides)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = 2 * Mathf.PI * i / sides;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * .5f + Vector3.down); n.Add(d); uv.Add(new Vector2((float)i / sides, 0));
                v.Add(d * .5f + Vector3.up); n.Add(d); uv.Add(new Vector2((float)i / sides, 1));
            }
            for (int i = 0; i < sides; i++)
            {
                int b = i * 2;
                tri.AddRange(new[] { b, b + 1, b + 3, b, b + 3, b + 2 });
            }
            foreach (int cap in new[] { -1, 1 })
            {
                int centre = v.Count;
                v.Add(new Vector3(0, cap, 0)); n.Add(new Vector3(0, cap, 0)); uv.Add(new Vector2(.5f, .5f));
                for (int i = 0; i <= sides; i++)
                {
                    float a = 2 * Mathf.PI * i / sides;
                    v.Add(new Vector3(Mathf.Cos(a) * .5f, cap, Mathf.Sin(a) * .5f)); n.Add(new Vector3(0, cap, 0));
                    uv.Add(new Vector2(.5f + Mathf.Cos(a) * .5f, .5f + Mathf.Sin(a) * .5f));
                }
                for (int i = 0; i < sides; i++)
                    if (cap > 0) tri.AddRange(new[] { centre, centre + i + 2, centre + i + 1 });
                    else tri.AddRange(new[] { centre, centre + i + 1, centre + i + 2 });
            }
            var mesh = new Mesh { name = "PR_CylinderSmooth" };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Every primitive cylinder already in the scene's course (spinning discs and their rings, the round
        // board, springs, collars, crown band...) shows the smooth one; colliders keep their own mesh.
        static void SmoothCylinders(Transform course)
        {
            var smooth = AssetDatabase.LoadAssetAtPath<Mesh>(ResDir + "/PR_CylinderSmooth.asset");
            if (smooth == null) return;
            foreach (var f in course.GetComponentsInChildren<MeshFilter>(true))
                if (f.sharedMesh != null && f.sharedMesh.name == "Cylinder") f.sharedMesh = smooth;
        }

        // ---------------------------------------------------------------- course kit materials

        const string KitAssetPath = "Assets/Settings/Course01Kit.asset";
        const string CourseMatDir = MatDir + "/Course";

        // The course kit (Settings/Course01Kit.asset) feeds every grey box and everything the stations build
        // at run time (station floors, planks, drawbridge...). Its floor and wall materials are shared files in
        // Assets/Materials, so Pawn Rush gets its own copies in the map palette and the kit points at them;
        // the originals stay as they are. Takes effect on the next Build Course01 v4 / Play.
        [MenuItem("ChessFight/Pawn Rush/Art Kit: Course Materials", false, 24)]
        public static void SetupCourseKit()
        {
            var kit = AssetDatabase.LoadAssetAtPath<Course01Kit>(KitAssetPath);
            if (kit == null) { Debug.LogError("[PawnRush] Art Kit: no " + KitAssetPath); return; }
            if (!AssetDatabase.IsValidFolder(CourseMatDir)) AssetDatabase.CreateFolder(MatDir, "Course");
            var light = new Color(.80f, .79f, .76f);
            var dark = new Color(.10f, .10f, .13f);
            kit.floorChecker = CourseCopy(kit.floorChecker, "PRC_Floor_Checker", m =>
            {
                m.SetColor("_Color", light); m.SetColor("_Color2", dark);
                m.SetColor("_SideColor", new Color(.80f, .73f, .60f)); m.SetColor("_LineColor", new Color(.50f, .45f, .38f));
                m.SetFloat("_Glossiness", .7f);
            });
            kit.wallClimbable = CourseCopy(kit.wallClimbable, "PRC_Wall_Climbable", m =>
            {
                m.SetColor("_Color", light); m.SetColor("_Color2", dark);
                m.SetColor("_SideColor", new Color(.84f, .78f, .66f)); m.SetColor("_LineColor", new Color(.55f, .48f, .38f));
            });
            kit.wallNoClimb = CourseCopy(kit.wallNoClimb, "PRC_Wall_NoClimb", m =>
            {
                m.SetColor("_Color", light); m.SetColor("_Color2", dark);
                m.SetColor("_SideColor", new Color(.88f, .84f, .76f)); m.SetColor("_LineColor", new Color(.62f, .56f, .48f));
                m.SetFloat("_Glossiness", .4f);
            });
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
        }

        // Copy (once) a kit material into CourseMatDir and recolour it. Already a copy: recolour in place.
        static Material CourseCopy(Material source, string name, System.Action<Material> recolour)
        {
            if (source == null) return null;
            var path = CourseMatDir + "/" + name + ".mat";
            var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (copy == null)
            {
                copy = new Material(source) { name = name };
                AssetDatabase.CreateAsset(copy, path);
            }
            recolour(copy);
            EditorUtility.SetDirty(copy);
            return copy;
        }

        // ---------------------------------------------------------------- shared URP materials

        static readonly Dictionary<string, (Color color, float metal, float smooth)> Palette = new()
        {
            // whites kept below 0.85: the game look's ACES + bloom blows brighter whites out in Play
            { "Stone_Cream", (new Color(0.84f, 0.78f, 0.66f), 0f, 0.3f) },
            { "Stone_CreamDark", (new Color(0.80f, 0.73f, 0.60f), 0f, 0.3f) },
            { "Stone_Shadow", (new Color(0.62f, 0.56f, 0.48f), 0f, 0.25f) },
            { "Stone_Charcoal", (new Color(0.16f, 0.16f, 0.18f), 0f, 0.5f) },
            { "Tile_White", (new Color(0.80f, 0.79f, 0.76f), 0f, 0.7f) },
            { "Tile_Black", (new Color(0.10f, 0.10f, 0.13f), 0f, 0.85f) },
            { "Trim_Red", (new Color(0.78f, 0.18f, 0.20f), 0f, 0.6f) },
            { "Trim_White", (new Color(0.82f, 0.80f, 0.76f), 0f, 0.5f) },
            { "Banner_Navy", (new Color(0.16f, 0.27f, 0.62f), 0f, 0.25f) },
            { "Banner_Red", (new Color(0.75f, 0.15f, 0.17f), 0f, 0.25f) },
            { "Iron", (new Color(0.22f, 0.22f, 0.24f), 0.8f, 0.55f) },
            { "Wood", (new Color(0.42f, 0.28f, 0.16f), 0f, 0.3f) },
            { "Gold", (new Color(0.93f, 0.70f, 0.20f), 0.75f, 0.65f) },
        };

        // One URP Lit material per palette name in MatDir; every kit FBX is remapped to them, so a
        // colour is tuned once for the whole kit.
        [MenuItem("ChessFight/Pawn Rush/Art Kit: Setup Materials", false, 22)]
        public static void SetupMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Art/PawnRushMap", "Materials");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var mats = new Dictionary<string, Material>();
            foreach (var kv in Palette)
            {
                var path = MatDir + "/PR_" + kv.Key + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(lit);
                    m.SetColor("_BaseColor", kv.Value.color);
                    m.SetFloat("_Metallic", kv.Value.metal);
                    m.SetFloat("_Smoothness", kv.Value.smooth);
                    AssetDatabase.CreateAsset(m, path);
                }
                mats[kv.Key] = m;
                Surface(m, kv.Key);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { KitDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                bool changed = false;
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is not Material em || !mats.TryGetValue(em.name, out var target)) continue;
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), em.name);
                    imp.AddRemap(id, target);
                    changed = true;
                }
                if (imp.importAnimation || imp.addCollider) { imp.importAnimation = false; imp.addCollider = false; changed = true; }
                if (changed) imp.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }

        const string TexDir = "Assets/Art/PawnRushMap/Textures";

        // Surface grain (item 6 of the 10-17 list): stone gets a mottled grain and a light bump, the glossy
        // tiles a faint polish cloud, so nothing reads as flat plastic. The maps are near-white, so the
        // material colour still sets the hue. Kit boxes are unwrapped per face, one texture per face.
        static void Surface(Material m, string palette)
        {
            string set = palette.StartsWith("Stone_") ? "PR_StoneGrain" : palette.StartsWith("Tile_") ? "PR_TilePolish" : null;
            if (set == null) return;
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + set + "_Albedo.png");
            var normalPath = TexDir + "/" + set + "_Normal.png";
            if (AssetImporter.GetAtPath(normalPath) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (albedo == null || normal == null) return;
            m.SetTexture("_BaseMap", albedo);
            m.SetTexture("_BumpMap", normal);
            // 승규 10-17: keep the strong stone grain (.8), do not soften it
            m.SetFloat("_BumpScale", set == "PR_StoneGrain" ? .8f : .25f);
            m.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(m);
        }
    }
}
