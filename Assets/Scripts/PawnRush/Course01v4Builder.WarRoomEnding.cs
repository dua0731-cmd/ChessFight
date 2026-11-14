using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;
using Kind = ChessFight.PawnRush.LibraryPaint.Kind;

namespace ChessFight.PawnRush
{
    // R616 (the user, 11-12, a sketch of the round's last section - S the start at the bottom, E the ending at the top,
    // the conveyor's lane between them with its carriage, a turret P at each of the four corners, one of them shooting at
    // the carriage: "마지막 엔딩구간의 레벨디자인을 바꾸려고 해. 지금 정면에 3개의 포탑이 순서대로 공격하게 되어있어. 1. 포탑을 넓게
    // 퍼뜨려줘. 첨부 이미지처럼 4방면으로 나눠줘.(1개 추가) 2. 중간에 트레일이 움직이는 것에 따라 포탑은 트레일 쪽으로 공격하게 돼.
    // 4개의 포탑은 3초마다 한 번씩 랜덤한 포탑 하나가 공격하게 해줘. 직전에 공격한 포탑은 연속으로 공격하지 않게 해줘. 3. 엔딩 구간도
    // 현재 엔딩 구간말고, 문을 열고 나가야 있게 해줘. 엔딩 구간에 대한 공간은 나중에 따로 만들거야."): what the round's war room
    // (BuildWarRoomSection) changes in zone E's room after it is built. 승규 님's test scene (zone E itself) keeps the
    // room as it was, as with R518's belt.
    //
    //   1  The three goal rooks leave the star platform. Four turrets - the two side rooks and two copies, all the side
    //      rooks' size and shot; the larger middle rook is put away - stand on stone towers at the four corners of the
    //      conveyor's well (WarRoomTurretSpots): beside the start floor's balustrade ends and at the head of the goal
    //      stairs. On the well's corners every turret sees the whole of the carriage's travel between the inner piers
    //      (from out on the balconies a pier hides half of it), and on a tower 1.5 m high its shots clear the
    //      balustrades.
    //   2  They fire as one battery (LibraryRookTurret.ConfigureVolley, Core TurretVolleys): one turret every two
    //      seconds, drawn from the section's seed, never the one before, through the carriage at rider height.
    //   3  The great door behind the star platform - until now a relief on the solid rear wall - is the way out: the
    //      relief's two leaves are cut out of it and hung on a PushGate (the carriage yard's final gate: held with the
    //      right mouse button and walked open, its progress in the course state), the wall behind them is opened, and
    //      the round's finish stands beyond it on a plain floor that holds the place of the ending the user will build
    //      (WarRoomEnding - replace that one function's floor when the ending's own space is made).
    //
    // Section frame throughout (x east, y up from the start floor, z north = the way on), as WarRoomSection.
    public static partial class Course01v4Builder
    {
        // Enclose a section of each balcony from the well to the side wall. Both ends and the well-facing side
        // are closed, so diving around a single gate cannot land back on the balcony beyond it. The enclosures
        // stand on the opposite half of each balcony from its blower. The basement stairs still return to the start.
        static void WarRoomBalconyScreens(Transform content)
        {
            var iron = LibraryPaintLook(Kind.Iron, LibraryPaint.Iron);
            var brass = LibraryPaintLook(Kind.Brass, LibraryPaint.Brass);
            foreach (int side in new[] { -1, 1 })
            {
                var enclosure = new GameObject(side < 0 ? "West balcony enclosure" : "East balcony enclosure");
                var t = enclosure.transform;
                t.SetParent(content, false);
                t.localPosition = new Vector3(side * 11.55f, 0f, -side * 4.25f);
                const float width = 7.5f, depth = 8.5f, top = 7.5f;
                var box = enclosure.AddComponent<BoxCollider>();
                box.size = new Vector3(width, top + .15f, depth);
                box.center = new Vector3(0f, (top - .15f) * .5f, 0f);
                enclosure.AddComponent<NoClimbSurface>();
                // Reuse the room's iron and brass. One smooth solid backs all three faces of the grille.
                for (int i = 0; i <= 12; i++)
                {
                    float x = Mathf.Lerp(-width * .5f, width * .5f, i / 12f);
                    foreach (int end in new[] { -1, 1 })
                        YardKit.Box(t, "End grille bar", new Vector3(x, top * .5f, end * depth * .5f),
                            new Vector3(.09f, top, .11f), iron, YardKit.Solid.None);
                }
                float inner = -side * width * .5f;
                for (int i = 1; i < 14; i++)
                    YardKit.Box(t, "Gallery grille bar", new Vector3(inner, top * .5f, depth * (i / 14f - .5f)),
                        new Vector3(.11f, top, .09f), iron, YardKit.Solid.None);
                foreach (float y in new[] { .15f, 1.18f, top - .15f })
                {
                    foreach (int end in new[] { -1, 1 })
                        YardKit.Box(t, "End rail", new Vector3(0f, y, end * depth * .5f),
                            new Vector3(width, .14f, .15f), brass, YardKit.Solid.None);
                    YardKit.Box(t, "Gallery rail", new Vector3(inner, y, 0f),
                        new Vector3(.15f, .14f, depth), brass, YardKit.Solid.None);
                }
            }
            // The long, narrow fixed supports below the carriage are scenery in the race. The moving deck and
            // its fittings still carry riders, and the take-off strip still supports their jump onto it.
            foreach (var collider in content.GetComponentsInChildren<MeshCollider>(true))
                if (collider.name.StartsWith("Fixed_guide_")) collider.enabled = false;
        }

        // ------------------------------------------------------------------ 1, 2: the turrets

        public const int WarRoomTurretCount = 4;
        public const float WarRoomTurretTower = 1.5f;         // the tower under a rook's own pedestal
        public static readonly string[] WarRoomTurretNames = { "SW", "SE", "NW", "NE" };
        // The towers' feet: the start floor's level south of the well, the goal floor's (1.2) at the head of the goal stairs.
        public static readonly Vector3[] WarRoomTurretSpots =
        {
            new Vector3(-8.65f, 0f, -13f), new Vector3(8.65f, 0f, -13f),
            new Vector3(-8.65f, 1.2f, 13f), new Vector3(8.65f, 1.2f, 13f),
        };

        static bool WarRoomTurrets(Transform content)
        {
            Transform middle = null, west = null, east = null;
            foreach (var t in content.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Goal_rook_0") middle = t;
                else if (t.name == "Goal_rook_-1") west = t;
                else if (t.name == "Goal_rook_1") east = t;
            }
            if (west == null || east == null || west.parent == null || west.parent.parent == null
                || west.GetComponent<LibraryRookTurret>() == null || east.GetComponent<LibraryRookTurret>() == null)
            {
                Debug.LogError("[작전 회의실] 포탑으로 옮길 양옆 골 룩(Goal_rook_-1 · Goal_rook_1)을 찾지 못했습니다. 골 룩 3개가 그대로 남습니다.");
                return false;
            }
            // The room's pivot (Library replacement: no scale, the room's sound muffle on it) carries the towers.
            var pivot = west.parent.parent;
            if (middle != null)
            {
                var attack = middle.GetComponent<LibraryRookTurret>();
                if (attack != null) Object.DestroyImmediate(attack);
                middle.gameObject.SetActive(false);
            }
            // The copies first, while the two side rooks still stand where the model put them.
            var rooks = new[] { west, east, Object.Instantiate(west.gameObject, west.parent).transform, Object.Instantiate(east.gameObject, east.parent).transform };
            // In the section's frame (the room is built at the origin and moved to its place after): a rook's window, as
            // modelled, looks down the hall; waiting, it looks at the middle of the well, at the deck's height.
            Vector3 facing = Vector3.back, rest = new Vector3(0f, .2f, 0f);
            var marble = LibraryLook("Library marble dark");
            if (marble == null) marble = LibraryPaintLook(Kind.Stone, LibraryPaint.Marble);
            var stone = LibraryPaintLook(Kind.Stone, LibraryPaint.WallStone);
            var brass = LibraryPaintLook(Kind.Brass, LibraryPaint.Brass);
            for (int i = 0; i < WarRoomTurretCount; i++)
            {
                var tower = new GameObject("Rook turret " + WarRoomTurretNames[i]).transform;
                tower.SetParent(pivot, false);
                tower.SetPositionAndRotation(content.TransformPoint(WarRoomTurretSpots[i]), content.rotation);
                // A stepped stone tower, 1.5 m: a marble foot, a limestone shaft with a brass band, a marble cap. Named as
                // the rooks' own parts, so no turret's line of fire counts a tower as something in its way.
                YardKit.Box(tower, "Goal_rook_tower foot", new Vector3(0f, .14f, 0f), new Vector3(1.5f, .28f, 1.5f), marble, YardKit.Solid.NoClimb);
                YardKit.Box(tower, "Goal_rook_tower shaft", new Vector3(0f, .78f, 0f), new Vector3(1.24f, 1f, 1.24f), stone, YardKit.Solid.NoClimb);
                YardKit.Box(tower, "Goal_rook_tower band", new Vector3(0f, .86f, 0f), new Vector3(1.3f, .09f, 1.3f), brass, YardKit.Solid.None);
                YardKit.Box(tower, "Goal_rook_tower cap", new Vector3(0f, 1.39f, 0f), new Vector3(1.5f, .22f, 1.5f), marble, YardKit.Solid.NoClimb);
                var rook = rooks[i];
                rook.SetParent(tower, true);
                rook.position = tower.TransformPoint(new Vector3(0f, WarRoomTurretTower, 0f));
                Transform piece = RookPart(rook, "_piece"), gap = RookPart(rook, "_gap"), pedestal = RookPart(rook, "_pedestal");
                rook.name = "Goal_rook_" + WarRoomTurretNames[i];
                rook.GetComponent<LibraryRookTurret>().ConfigureVolley(i, WarRoomTurretCount, content, facing, rest, piece, gap, pedestal);
            }
            Debug.Log("[작전 회의실] 2초마다 포탑 한 대가 캐리지 위의 플레이어 높이를 가로질러 발사합니다.");
            return true;
        }

        static Transform RookPart(Transform rook, string ending)
        {
            foreach (Transform child in rook)
                if (child.name.EndsWith(ending)) return child;
            return null;
        }

        // ------------------------------------------------------------------ 3: the way out

        // The door's opening, as the relief is modelled: its leaves' outer edges (the hinges) at x +-2.15, from the goal
        // floor (1.2) up to the transom's underside, the leaves' faces from z 24.07 back. The rear wall behind them is a
        // course of limestone blocks (z 24.31 .. 24.49) on a solid backing (to 25.07).
        public const float WarRoomGoalFloor = 1.2f, WarRoomExitHalf = 2.15f, WarRoomExitTop = 5.72f;
        public const float WarRoomExitGate = 24.21f, WarRoomWallBack = 25.07f;
        const float ExitCutHalf = 2.17f, ExitCutFront = 24f, ExitLeafThickness = .3f;
        // What stands out in front of the leaves' faces - their ring pulls - comes off with them: inside the jambs' reach
        // the cut starts further forward, under the transom.
        const float ExitPullsHalf = 1.92f, ExitPullsTop = 5.6f, ExitPullsFront = 23.8f;
        const float WallBackingFront = 24.47f, WallHalf = 14.85f, WallFoot = -8f, WallHead = 12.25f;
        // One character walks the door open in ExitPushSeconds; a pawn fits through after about a third of them.
        const float ExitPushSeconds = 8f, ExitCrowdBonus = .6f;

        // Opens the great door: false (and the room left as it was) when the model is not the one this was measured on.
        static PushGate WarRoomExitDoor(Transform content)
        {
            MeshFilter door = null, course = null, backing = null;
            foreach (var f in content.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.name.Contains("Arched_double_door")) door = f;
                else if (f.name.StartsWith("Rear_wall_staggered_limestone")) course = f;
                else if (f.name.StartsWith("Rear_wall_solid_backing")) backing = f;
            }
            if (door == null || course == null || backing == null || !Cuttable(door.sharedMesh) || !Cuttable(course.sharedMesh))
            {
                Debug.LogError("[작전 회의실] 출구 문을 열 수 없습니다: 문 조형(Arched_double_door) · 뒷벽 돌 줄(Rear_wall_staggered_limestone) · 뒷벽 속(Rear_wall_solid_backing)" +
                               " 가운데 없는 것이 있거나 메시를 읽을 수 없습니다. 결승이 예전처럼 별 발판 위에 남습니다.");
                return null;
            }

            // The relief: everything of it inside the opening, from the leaves' faces back, comes off as two leaves.
            var toSection = content.worldToLocalMatrix * door.transform.localToWorldMatrix;
            CutDoor(door.sharedMesh, toSection, out var rest, out var leftLeaf, out var rightLeaf);
            if (leftLeaf.vertexCount == 0 || rightLeaf.vertexCount == 0)
            {
                Debug.LogError("[작전 회의실] 출구 문을 열 수 없습니다: 문 조형에서 문짝 자리(x ±" + ExitCutHalf + ", 높이 " + WarRoomExitTop + " 아래)를 찾지 못했습니다.");
                return null;
            }
            door.sharedMesh = rest;
            if (door.TryGetComponent<MeshCollider>(out var doorSolid)) doorSolid.sharedMesh = rest;
            var looks = door.GetComponent<Renderer>().sharedMaterials;
            Transform left = DoorLeaf(door.transform, "Exit door leaf (left)", leftLeaf, looks);
            Transform right = DoorLeaf(door.transform, "Exit door leaf (right)", rightLeaf, looks);

            // The wall behind: the blocks of the stone course that reach into the opening go (those the relief's jambs and
            // transom hide), the backing is built again round the opening.
            var courseToSection = content.worldToLocalMatrix * course.transform.localToWorldMatrix;
            var opened = WithoutBlocks(course.sharedMesh, courseToSection, out int blocks);
            course.sharedMesh = opened;
            if (course.TryGetComponent<MeshCollider>(out var courseSolid)) courseSolid.sharedMesh = opened;
            var wallLook = backing.GetComponent<Renderer>().sharedMaterial;
            backing.GetComponent<Renderer>().enabled = false;
            foreach (var c in backing.GetComponents<Collider>()) c.enabled = false;
            var wall = new GameObject("Exit wall").transform;
            wall.SetParent(content, false);
            float mid = (WallBackingFront + WarRoomWallBack) * .5f, deep = WarRoomWallBack - WallBackingFront;
            float side = WallHalf - ExitCutHalf;
            foreach (float sign in new[] { -1f, 1f })
                YardKit.Box(wall, sign < 0f ? "Rear wall (west of the door)" : "Rear wall (east of the door)",
                            new Vector3(sign * (ExitCutHalf + side * .5f), (WallFoot + WallHead) * .5f, mid), new Vector3(side, WallHead - WallFoot, deep),
                            wallLook, YardKit.Solid.NoClimb);
            YardKit.Box(wall, "Rear wall (over the door)", new Vector3(0f, (WarRoomExitTop + WallHead) * .5f, mid),
                        new Vector3(ExitCutHalf * 2f, WallHead - WarRoomExitTop, deep), wallLook, YardKit.Solid.NoClimb);
            YardKit.Box(wall, "Rear wall (under the door)", new Vector3(0f, (WallFoot + WarRoomGoalFloor) * .5f, mid),
                        new Vector3(ExitCutHalf * 2f, WarRoomGoalFloor - WallFoot, deep), wallLook, YardKit.Solid.Solid);
            // Between the relief and the backing (the stone course's depth) the passage is lined: jambs and a lintel.
            var stone = LibraryPaintLook(Kind.Stone, LibraryPaint.WallStone);
            const float lining = .25f;
            float liningMid = (ExitCutFront + .02f + WallBackingFront + .01f) * .5f, liningDeep = WallBackingFront + .01f - (ExitCutFront + .02f);
            foreach (float sign in new[] { -1f, 1f })
                YardKit.Box(wall, sign < 0f ? "Door jamb lining (west)" : "Door jamb lining (east)",
                            new Vector3(sign * (ExitCutHalf + lining * .5f), (WarRoomGoalFloor + WarRoomExitTop + lining) * .5f, liningMid),
                            new Vector3(lining, WarRoomExitTop + lining - WarRoomGoalFloor, liningDeep), stone, YardKit.Solid.NoClimb);
            YardKit.Box(wall, "Door lintel lining", new Vector3(0f, WarRoomExitTop + lining * .5f, liningMid),
                        new Vector3(ExitCutHalf * 2f, lining, liningDeep), stone, YardKit.Solid.NoClimb);

            // The leaves on a push gate: hinged at the opening's sides, swinging 90 degrees away from the hall to lie
            // against the passage's sides.
            var gate = new GameObject("Exit gate");
            gate.transform.SetParent(content, false);
            gate.transform.localPosition = new Vector3(0f, WarRoomGoalFloor, WarRoomExitGate);
            var push = gate.AddComponent<PushGate>();
            push.Configure(WarRoomExitHalf * 2f, WarRoomExitTop - WarRoomGoalFloor, ExitPushSeconds, ExitCrowdBonus, .5f);
            push.Dress(left, right, ExitLeafThickness, 90f, .75f);
            Debug.Log("[작전 회의실] 뒷벽의 큰 문을 실제 출구로(R616): 문짝 두 장을 조형에서 떼어 밀어서 여는 문에 달고(삼각형 " +
                      (leftLeaf.triangles.Length + rightLeaf.triangles.Length) / 3 + "개), 뒷벽 돌 " + blocks + "개 면을 걷어 냄");
            return push;
        }

        static bool Cuttable(Mesh mesh) => mesh != null && mesh.isReadable && mesh.subMeshCount == 1 && mesh.vertexCount > 0;

        // A leaf's model: a piece of the door's own mesh in the door's own place (the door's picture is laid on it from
        // its object's origin and x axis - PR_LibraryFacade - so it rides the leaf as painted).
        static Transform DoorLeaf(Transform door, string name, Mesh mesh, Material[] looks)
        {
            var go = new GameObject(name);
            go.transform.SetParent(door.parent, false);
            go.transform.localPosition = door.localPosition;
            go.transform.localRotation = door.localRotation;
            go.transform.localScale = door.localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = looks;
            return go.transform;
        }

        // ------------------------------------------------------------------ cutting the model

        struct CutPoint { public Vector3 local, normal, at; }      // in the mesh's space; `at` in the section's

        sealed class CutPart
        {
            readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            readonly List<int> triangles = new List<int>();
            readonly Dictionary<int, int> kept = new Dictionary<int, int>();

            // A whole triangle of the mesh, its corners shared with the others kept.
            public void Whole(int a, int b, int c, Vector3[] v, Vector3[] n)
            {
                triangles.Add(Corner(a, v, n));
                triangles.Add(Corner(b, v, n));
                triangles.Add(Corner(c, v, n));
            }

            int Corner(int index, Vector3[] v, Vector3[] n)
            {
                if (kept.TryGetValue(index, out int at)) return at;
                vertices.Add(v[index]);
                normals.Add(n[index]);
                return kept[index] = vertices.Count - 1;
            }

            // A convex piece of a cut triangle, as a fan.
            public void Piece(List<CutPoint> polygon)
            {
                if (polygon.Count < 3) return;
                int first = vertices.Count;
                foreach (var p in polygon)
                {
                    vertices.Add(p.local);
                    normals.Add(p.normal);
                }
                for (int i = 1; i + 1 < polygon.Count; i++)
                {
                    triangles.Add(first);
                    triangles.Add(first + i);
                    triangles.Add(first + i + 1);
                }
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // A box of the cut, in the section's frame: everything of the relief with |x| <= half, y <= top and z >= front.
        struct CutBox
        {
            public float half, top, front;

            public bool Holds(Vector3 p) => Mathf.Abs(p.x) <= half && p.y <= top && p.z >= front;

            // wholly beyond one of the box's faces
            public bool Misses(Vector3 a, Vector3 b, Vector3 c) =>
                (a.x <= -half && b.x <= -half && c.x <= -half) || (a.x >= half && b.x >= half && c.x >= half)
                || (a.y >= top && b.y >= top && c.y >= top) || (a.z <= front && b.z <= front && c.z <= front);
        }

        // The door's relief in three: what lies inside the opening - the two leaves with their centre post and bands
        // (|x| <= ExitCutHalf, under WarRoomExitTop, from ExitCutFront back) and, between the jambs, the ring pulls that
        // stand out in front of them (the ExitPulls box) - west and east of the middle, and the rest. A triangle that
        // crosses a box's faces is cut along them, so the opening's edges are straight.
        static void CutDoor(Mesh mesh, Matrix4x4 toSection, out Mesh rest, out Mesh west, out Mesh east)
        {
            var v = mesh.vertices;
            var n = mesh.normals;
            if (n == null || n.Length != v.Length)
            {
                n = new Vector3[v.Length];
                for (int i = 0; i < n.Length; i++) n[i] = Vector3.back;
            }
            var at = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) at[i] = toSection.MultiplyPoint3x4(v[i]);
            var boxes = new[]
            {
                new CutBox { half = ExitCutHalf, top = WarRoomExitTop, front = ExitCutFront },
                new CutBox { half = ExitPullsHalf, top = ExitPullsTop, front = ExitPullsFront },
            };
            var tris = mesh.triangles;
            CutPart keep = new CutPart(), left = new CutPart(), right = new CutPart();
            List<CutPoint> low = new List<CutPoint>(8), high = new List<CutPoint>(8);
            List<List<CutPoint>> pieces = new List<List<CutPoint>>(), outside = new List<List<CutPoint>>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                Vector3 pa = at[a], pb = at[b], pc = at[c];
                if (boxes[0].Misses(pa, pb, pc) && boxes[1].Misses(pa, pb, pc))
                {
                    keep.Whole(a, b, c, v, n);
                    continue;
                }
                bool inside = (boxes[0].Holds(pa) && boxes[0].Holds(pb) && boxes[0].Holds(pc)) || (boxes[1].Holds(pa) && boxes[1].Holds(pb) && boxes[1].Holds(pc));
                if (inside && pa.x <= 0f && pb.x <= 0f && pc.x <= 0f) { left.Whole(a, b, c, v, n); continue; }
                if (inside && pa.x >= 0f && pb.x >= 0f && pc.x >= 0f) { right.Whole(a, b, c, v, n); continue; }
                pieces.Clear();
                pieces.Add(new List<CutPoint>(3)
                {
                    new CutPoint { local = v[a], normal = n[a], at = pa }, new CutPoint { local = v[b], normal = n[b], at = pb },
                    new CutPoint { local = v[c], normal = n[c], at = pc },
                });
                foreach (var box in boxes)
                {
                    outside.Clear();
                    foreach (var piece in pieces)
                    {
                        // off each face of the box in turn: what is outside it waits for the next box, what is inside goes on
                        var polygon = piece;
                        Split(polygon, 0, -box.half, low, high); Keep(outside, low); polygon = new List<CutPoint>(high);
                        Split(polygon, 0, box.half, low, high); Keep(outside, high); polygon = new List<CutPoint>(low);
                        Split(polygon, 1, box.top, low, high); Keep(outside, high); polygon = new List<CutPoint>(low);
                        Split(polygon, 2, box.front, low, high); Keep(outside, low); polygon = new List<CutPoint>(high);
                        Split(polygon, 0, 0f, low, high);
                        left.Piece(low);
                        right.Piece(high);
                    }
                    pieces.Clear();
                    pieces.AddRange(outside);
                }
                foreach (var piece in pieces) keep.Piece(piece);
            }
            rest = keep.Build(mesh.name + " (opened)");
            west = left.Build("Exit door leaf (left)");
            east = right.Build("Exit door leaf (right)");
        }

        static void Keep(List<List<CutPoint>> pieces, List<CutPoint> polygon)
        {
            if (polygon.Count >= 3) pieces.Add(new List<CutPoint>(polygon));
        }

        // A convex polygon in two along the plane where the section coordinate `axis` is `bound`: `low` the part under
        // it, `high` the part over it (a corner on the plane is in both).
        static void Split(List<CutPoint> polygon, int axis, float bound, List<CutPoint> low, List<CutPoint> high)
        {
            low.Clear();
            high.Clear();
            int count = polygon.Count;
            for (int i = 0; i < count; i++)
            {
                CutPoint a = polygon[i], b = polygon[(i + 1) % count];
                float fa = a.at[axis] - bound, fb = b.at[axis] - bound;
                if (fa <= 0f) low.Add(a);
                if (fa >= 0f) high.Add(a);
                if ((fa < 0f && fb > 0f) || (fa > 0f && fb < 0f))
                {
                    float u = fa / (fa - fb);
                    var m = new CutPoint
                    {
                        local = Vector3.Lerp(a.local, b.local, u), normal = Vector3.Lerp(a.normal, b.normal, u).normalized, at = Vector3.Lerp(a.at, b.at, u),
                    };
                    low.Add(m);
                    high.Add(m);
                }
            }
            if (low.Count < 3) low.Clear();
            if (high.Count < 3) high.Clear();
        }

        // The stone course without the faces of the blocks that reach into the opening (a block is a separate box in
        // the mesh: every face with a corner inside the opening goes, so a block the opening only clips goes whole but
        // for its far end, behind the relief).
        static Mesh WithoutBlocks(Mesh mesh, Matrix4x4 toSection, out int removed)
        {
            var v = mesh.vertices;
            var inside = new bool[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var p = toSection.MultiplyPoint3x4(v[i]);
                inside[i] = Mathf.Abs(p.x) <= ExitCutHalf && p.y >= WarRoomGoalFloor - .05f && p.y <= WarRoomExitTop;
            }
            var tris = mesh.triangles;
            var kept = new List<int>(tris.Length);
            removed = 0;
            for (int t = 0; t < tris.Length; t += 3)
            {
                if (inside[tris[t]] || inside[tris[t + 1]] || inside[tris[t + 2]]) { removed++; continue; }
                kept.Add(tris[t]);
                kept.Add(tris[t + 1]);
                kept.Add(tris[t + 2]);
            }
            var opened = Object.Instantiate(mesh);
            opened.name = mesh.name + " (opened)";
            opened.SetTriangles(kept, 0);
            opened.RecalculateBounds();
            return opened;
        }

        // ------------------------------------------------------------------ beyond the door

        // The ending's place, for now: the user will build the ending's own space later ("엔딩 구간에 대한 공간은 나중에 따로
        // 만들거야"). Until then a plain floor at the goal floor's height behind the rear wall, railed on three sides, a
        // checkpoint just past the door (through the door is through: a pawn that falls is not sent back to the room's
        // start) and the round's finish on it - a 4.8 m circle, as the star platform's was (R508). When the ending's
        // space is made, it takes this function's place; the door, the checkpoint's number and the finish's kind stay.
        public const float WarRoomEndingHalf = 6f, WarRoomEndingLength = 14f, WarRoomFinishAt = 33f, WarRoomFinishRadius = 2.4f;

        static void WarRoomEnding(CourseBuilder b)
        {
            var parent = b.parent;
            var ending = new GameObject("Ending (temporary floor)").transform;
            ending.SetParent(parent, false);
            b.parent = ending;
            float z0 = WarRoomWallBack, z1 = WarRoomWallBack + WarRoomEndingLength, y = WarRoomGoalFloor;
            b.Floor("Floor", -WarRoomEndingHalf, WarRoomEndingHalf, z0, z1, y);
            b.Rail("Railing W", -WarRoomEndingHalf - .3f, -WarRoomEndingHalf, z0, z1, y, 1.1f);
            b.Rail("Railing E", WarRoomEndingHalf, WarRoomEndingHalf + .3f, z0, z1, y, 1.1f);
            b.Rail("Railing N", -WarRoomEndingHalf - .3f, WarRoomEndingHalf + .3f, z1, z1 + .3f, y, 1.1f);
            // The finish's circle, drawn (the trigger itself is a box round it; CrownFinish takes its narrower half).
            var gold = YardKit.Gold;
            YardKit.Cylinder(ending, "Finish circle", new Vector3(0f, y + .015f, WarRoomFinishAt), Vector3.up, WarRoomFinishRadius * 2f, .03f, gold, YardKit.Solid.None);
            b.Checkpoint(1, new Vector3(0f, y, WarRoomWallBack + 2.6f), 0f, 10f);
            b.Trigger<FinishZone>("Finish (goal)", new Vector3(-WarRoomFinishRadius, y, WarRoomFinishAt - WarRoomFinishRadius),
                                  new Vector3(WarRoomFinishRadius, y + 3.4f, WarRoomFinishAt + WarRoomFinishRadius)).Configure(true);
            b.parent = parent;
        }
    }
}
