using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class ChessTimerMinigame
    {
        [Header("Physical wire puzzle (F3 resets)")]
        [SerializeField, Min(.1f)] float cutSeconds = 7f;
        [SerializeField, Range(.2f, 1f), Tooltip("Puzzle piece visual and collision scale; restart Play to apply.")] float rookScale = .6f;
        [SerializeField, Min(0)] float wrongWireLock = 2f;
        readonly ChessWirePuzzle[] puzzles = new ChessWirePuzzle[2];
        readonly bool[] hasCutter = new bool[12];
        readonly GameObject[] carriedCutters = new GameObject[12];
        readonly GameObject[,] wireCentres = new GameObject[2, 6];
        readonly TextMesh[,] wireLabels = new TextMesh[2, 6];
        readonly TextMesh[] boardSigns = new TextMesh[2];
        readonly GameObject[] attackHints = new GameObject[2];
        readonly GameObject[,] cuttingTools = new GameObject[2, 6];
        readonly int[] cuttingWire = { -1, -1 };
        readonly float[] solveTimes = { -1f, -1f };
        ChessWirePuzzle.Layout previousFirst;
        readonly ChessWirePuzzle.Course[] courses = new ChessWirePuzzle.Course[2];
        readonly System.Random puzzleRandom = new System.Random();
        Material[] boardMaterials, wireMaterials;
        Material hintMaterial;
        readonly GameObject[] boardRoots = new GameObject[2];
        readonly System.Collections.Generic.List<Mesh>[] boardMeshes = { new System.Collections.Generic.List<Mesh>(), new System.Collections.Generic.List<Mesh>() };
        static readonly string[] WireColorNames = { "빨강", "파랑", "노랑", "초록", "보라", "주황" };
        float elapsed;
        Material toolMetal, toolGrip;
        static float BoardCentreX(int team) => team == 0 ? -21f : 21f;
        static Vector3 BoardSquare(int team, int file, int rank) =>
            new Vector3(BoardCentreX(team) + (file - 3.5f) * TileSize, .06f, 30 + ((team == 0 ? 7 - rank : rank) - 3.5f) * TileSize);
        static Vector3 ToolStation(int team) => new Vector3(BoardCentreX(team), .9f, 11.5f);
        Vector3 WirePoint(int team, int wire) => BoardSquare(team, puzzles[team].Board.File(wire), puzzles[team].Board.Rank(wire)) + Vector3.up * .7f;

        void BuildDefuseBoards(int team, Material ivory, Material walnut, Material cream, Material charcoal)
        {
            int firstChild = transform.childCount, firstMesh = meshes.Count;
            if (wireMaterials == null)
            {
                toolMetal = charcoal; toolGrip = Mat(new Color(1f, .68f, .15f));
                wireMaterials = new[] { Mat(new Color(.92f, .16f, .12f)), Mat(new Color(.12f, .4f, 1f)), Mat(new Color(1f, .75f, .05f)),
                    Mat(new Color(.15f, .8f, .3f)), Mat(new Color(.65f, .2f, .9f)), Mat(new Color(1f, .45f, .08f)) };
                hintMaterial = Mat(new Color(.8f, .28f, .19f));
            }
            var colors = wireMaterials; var hint = hintMaterial;
            var puzzleLayout = puzzles[team].Board;
            var pieceMaterial = team == 0 ? charcoal : cream;
            {
                attackHints[team] = new GameObject("Piece attack hints"); attackHints[team].transform.SetParent(transform, false);
                for (int file = 0; file < ChessWirePuzzle.Size; file++) for (int rank = 0; rank < ChessWirePuzzle.Size; rank++)
                {
                    var centre = BoardSquare(team, file, rank);
                    // Flush walking surface: tile graphics have no extra collision lips.
                    Box("Board tile " + (char)('A' + file) + (rank + 1), new Vector3(centre.x, .015f, centre.z),
                        new Vector3(TileSize, .03f, TileSize), (file + rank) % 2 == 0 ? ivory : walnut, false);
                    if (puzzleLayout.Attacked(file, rank))
                    {
                        var mark = Box("Attack hint", new Vector3(centre.x, .04f, centre.z), new Vector3(TileSize - .2f, .015f, .18f), hint, false);
                        mark.transform.SetParent(attackHints[team].transform, true);
                    }
                }
                attackHints[team].SetActive(false);
                for (int index = 0; index < 8; index++)
                {
                    Label("File coordinate", BoardSquare(team, index, 0) + new Vector3(0, .15f, team == 0 ? 2.5f : -2.5f), ((char)('A' + index)).ToString(), .5f);
                    Label("Rank coordinate", BoardSquare(team, 0, index) + new Vector3(-2.6f, .15f, 0), (index + 1).ToString(), .5f);
                }
                for (int piece = 0; piece < puzzleLayout.PieceCount; piece++)
                {
                    var rook = BoardSquare(team, puzzleLayout.PieceFile(piece), puzzleLayout.PieceRank(piece));
                    var kind = puzzleLayout.PieceKind(piece);
                    bool firstOfKind = true;
                    for (int earlier = 0; earlier < piece; earlier++) if (puzzleLayout.PieceKind(earlier) == kind) firstOfKind = false;
                    if (firstOfKind) BuildPieceGuide(team, kind, walnut, cream, charcoal);
                    string coordinate = ((char)('A' + puzzleLayout.PieceFile(piece))).ToString() + (puzzleLayout.PieceRank(piece) + 1);
                    if (BuildDetailedPiece(rook, kind, coordinate, team == 0)) continue;
                    if (kind == ChessWirePuzzle.Kind.Bishop) { BuildBishop(rook, coordinate, pieceMaterial, team == 0 ? cream : charcoal); continue; }
                    if (kind == ChessWirePuzzle.Kind.Queen) { BuildQueen(rook, coordinate, pieceMaterial); continue; }
                    if (kind == ChessWirePuzzle.Kind.Knight || kind == ChessWirePuzzle.Kind.Pawn)
                    { BuildSmallPiece(rook, kind, coordinate, pieceMaterial); continue; }
                    var rookRoot = new GameObject("Puzzle rook");
                    rookRoot.transform.SetParent(transform, false); rookRoot.transform.position = rook;
                    RoundedBox("Giant rook base", rook + Vector3.up * .4f, new Vector3(2.8f, .8f, 2.8f), .25f, pieceMaterial).transform.SetParent(rookRoot.transform, true);
                    RoundedBox("Giant rook tower", rook + Vector3.up * 3f, new Vector3(1.9f, 4.8f, 1.9f), .2f, pieceMaterial).transform.SetParent(rookRoot.transform, true);
                    Box("Giant rook crown", rook + Vector3.up * 5.5f, new Vector3(2.7f, .6f, 2.7f), pieceMaterial).transform.SetParent(rookRoot.transform, true);
                    for (int corner = 0; corner < 4; corner++)
                        Box("Rook battlement", rook + new Vector3(corner % 2 == 0 ? -1 : 1, 6.1f, corner < 2 ? -1 : 1), new Vector3(.65f, .7f, .65f), pieceMaterial).transform.SetParent(rookRoot.transform, true);
                    float scale = Mathf.Clamp(rookScale, .2f, 1f);
                    rookRoot.transform.localScale = Vector3.one * scale;
                    Label("Rook location", rook + new Vector3(0, 6.45f * scale + .6f, 0), "ROOK / " + (char)('A' + puzzleLayout.PieceFile(piece)) + (puzzleLayout.PieceRank(piece) + 1), .35f);
                }
                for (int wire = 0; wire < puzzles[team].Board.WireCount; wire++)
                {
                    cuttingTools[team, wire] = MakeCutter("Cutter at wire " + (wire + 1), transform);
                    cuttingTools[team, wire].SetActive(false);
                    var p = WirePoint(team, wire);
                    RoundedBox("Wire cutting pedestal", p + Vector3.down * .38f, new Vector3(2.8f, .6f, 1.4f), .15f, charcoal);
                    // Two fixed ends and a removable central segment clearly show the cut.
                    Box("Wire left end", p + Vector3.left * .85f, new Vector3(.9f, .18f, .18f), colors[wire], false);
                    Box("Wire right end", p + Vector3.right * .85f, new Vector3(.9f, .18f, .18f), colors[wire], false);
                    wireCentres[team, wire] = Box("Cuttable wire " + (wire + 1), p, new Vector3(.8f, .18f, .18f), colors[wire], false);
                    // A cable runs down to a floor junction, making this a physical device on the board.
                    Box("Cable to junction", p + new Vector3(1.2f, -.3f, 0), new Vector3(.14f, .6f, .14f), colors[wire], false);
                    wireLabels[team, wire] = Label("Wire instruction", p + new Vector3(0, 1.3f, 0), "", .27f);
                }
                RoundedBox("Cutter supply bench", ToolStation(team) + Vector3.down * .45f, new Vector3(3, .9f, 1.5f), .2f, charcoal);
                var displayTool = MakeCutter("Cutter on supply bench", transform);
                displayTool.transform.position = ToolStation(team) + Vector3.up * .15f;
                displayTool.transform.localScale = Vector3.one * 2;
                boardSigns[team] = Label("Puzzle briefing", ToolStation(team) + new Vector3(0, 2.4f, 1.8f),
                    $"{courses[team].Stage + 1}/3단계 · {(team == 0 ? "흑 기물" : "백 기물")} {puzzleLayout.PieceCount}개\n안전 전선 {puzzleLayout.RequiredCuts}/{puzzleLayout.WireCount}개를 모두 자르세요\nF: 절단기 / H: 공격 힌트", .25f);
                Label("Court route", new Vector3(BoardCentreX(team), .8f, 8), "해체 구역 / 절단기 → 전선 → 출구", .3f);
            }
            // Own all generated board objects, labels and meshes so F3 leaves no stale colliders or hints.
            var children = new System.Collections.Generic.List<Transform>();
            for (int i = firstChild; i < transform.childCount; i++) children.Add(transform.GetChild(i));
            boardRoots[team] = new GameObject("Team " + team + " puzzle board"); boardRoots[team].transform.SetParent(transform, false);
            foreach (var child in children) child.SetParent(boardRoots[team].transform, true);
            for (int i = firstMesh; i < meshes.Count; i++) boardMeshes[team].Add(meshes[i]);
        }
        void BuildPieceGuide(int team, ChessWirePuzzle.Kind kind, Material frame, Material paper, Material ink)
        {
            // Mount on the inner face of the outer wall, outside the playable chessboard.
            bool bishop = kind == ChessWirePuzzle.Kind.Bishop;
            string pieceName = kind == ChessWirePuzzle.Kind.Rook ? "룩" : bishop ? "비숍" : kind == ChessWirePuzzle.Kind.Knight ? "나이트" : kind == ChessWirePuzzle.Kind.Queen ? "퀸" : "폰";
            float side = team == 0 ? -1f : 1f;
            var centre = new Vector3(side * (ArenaWidth * .5f - .36f), 2.5f, 18f + (int)kind * 6f);
            var root = new GameObject(kind + " movement guide");
            root.transform.SetParent(transform, false); root.transform.position = centre;
            Box("Guide frame", centre, new Vector3(3.2f, 3.8f, .18f), frame, false).transform.SetParent(root.transform, true);
            var content = new GameObject("Guide face"); content.transform.SetParent(root.transform, false);
            Box("Guide paper", centre + Vector3.back * .105f, new Vector3(3f, 3.6f, .025f), paper, false).transform.SetParent(content.transform, true);
            var title = Label("Guide title", centre + new Vector3(0, 1.45f, -.13f), pieceName + " · 공격 방향", .25f, 2.7f, .4f);
            title.color = Color.black; title.transform.SetParent(content.transform, true);
            for (int x = -2; x <= 2; x++) for (int y = -2; y <= 2; y++)
            {
                bool middle = x == 0 && y == 0;
                bool reachable = kind == ChessWirePuzzle.Kind.Pawn ? Mathf.Abs(x) == 1 && y == 1 :
                        kind == ChessWirePuzzle.Kind.Knight ? Mathf.Abs(x * y) == 2 :
                        kind == ChessWirePuzzle.Kind.Queen ? Mathf.Abs(x) == Mathf.Abs(y) || x == 0 || y == 0 :
                        bishop ? Mathf.Abs(x) == Mathf.Abs(y) : x == 0 || y == 0;
                var pos = centre + new Vector3(x * .33f, .28f + y * .33f, -.135f);
                Box("Movement diagram cell", pos, new Vector3(.30f, .30f, .012f),
                    middle ? ink : reachable ? toolGrip : frame, false).transform.SetParent(content.transform, true);
            }
            var symbol = Label("Diagram piece", centre + new Vector3(0, .28f, -.15f), kind == ChessWirePuzzle.Kind.Rook ? "R" : bishop ? "B" : kind == ChessWirePuzzle.Kind.Knight ? "N" : kind == ChessWirePuzzle.Kind.Queen ? "Q" : "P", .22f, .26f, .26f);
            symbol.color = Color.white; symbol.transform.SetParent(content.transform, true);
            string explanation = kind == ChessWirePuzzle.Kind.Pawn ?
                "앞 대각선 한 칸 공격\n직진은 공격 아님\n이 판에서는 출구 방향" :
                kind == ChessWirePuzzle.Kind.Knight ? "두 칸 + 옆 한 칸 (L자)\n다른 기물을 뛰어넘음" :
                (kind == ChessWirePuzzle.Kind.Queen ? "직선·대각선으로 이동" : bishop ? "대각선으로 이동" : "가로·세로로 이동") + "\n막힐 때까지 여러 칸\n다른 기물은 넘지 못함";
            var caption = Label("Guide explanation", centre + new Vector3(0, -1.18f, -.13f), explanation, .18f, 2.7f, .65f);
            caption.color = Color.black; caption.transform.SetParent(content.transform, true);
            var legend = Label("Diagram legend", centre + new Vector3(0, -.66f, -.13f), "금색 칸 = 공격 방향", .15f, 2.7f, .23f);
            legend.color = Color.black; legend.transform.SetParent(content.transform, true);
            // Text faces local -Z; turn the complete frame toward its team court.
            root.transform.localRotation = Quaternion.Euler(0, side * 90f, 0);
        }
        void BuildBishop(Vector3 position, string coordinate, Material cream, Material dark)
        {
            var root = new GameObject("Puzzle bishop"); root.transform.SetParent(transform, false); root.transform.position = position;
            RoundedBox("Bishop base", position + Vector3.up * .4f, new Vector3(2.8f, .8f, 2.8f), .3f, cream).transform.SetParent(root.transform, true);
            RoundedBox("Bishop stem", position + Vector3.up * 2.4f, new Vector3(1.3f, 3.4f, 1.3f), .5f, cream).transform.SetParent(root.transform, true);
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere); head.name = "Bishop mitre";
            head.transform.SetParent(root.transform, false); head.transform.localPosition = Vector3.up * 4.9f;
            head.transform.localScale = new Vector3(2, 2.8f, 2);
            head.GetComponent<Renderer>().sharedMaterial = cream;
            var slit = Box("Bishop diagonal slit", position + new Vector3(0, 5.2f, -.96f), new Vector3(.13f, 1.4f, .06f), dark, false);
            slit.transform.rotation = Quaternion.Euler(0, 0, -30); slit.transform.SetParent(root.transform, true);
            float scale = Mathf.Clamp(rookScale, .2f, 1f); root.transform.localScale = Vector3.one * scale;
            Label("Bishop location", position + Vector3.up * (6.3f * scale + .6f), "BISHOP / " + coordinate, .35f);
        }
        void BuildSmallPiece(Vector3 position, ChessWirePuzzle.Kind kind, string coordinate, Material material)
        {
            var root = new GameObject("Puzzle " + kind); root.transform.SetParent(transform, false); root.transform.position = position;
            RoundedBox("Piece base", position + Vector3.up * .3f, new Vector3(2.5f, .6f, 2.5f), .25f, material).transform.SetParent(root.transform, true);
            RoundedBox("Piece neck", position + Vector3.up * 1.6f, new Vector3(1.1f, 2.2f, 1.1f), .35f, material).transform.SetParent(root.transform, true);
            if (kind == ChessWirePuzzle.Kind.Pawn)
            {
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere); head.name = "Pawn head";
                head.transform.SetParent(root.transform, false); head.transform.localPosition = Vector3.up * 3f;
                head.transform.localScale = Vector3.one * 1.7f; head.GetComponent<Renderer>().sharedMaterial = material;
                // Pawns on both physically equivalent boards attack toward the exit.
                Box("Pawn forward marker", position + new Vector3(0, .65f, 1f), new Vector3(.25f, .1f, .8f), toolGrip, false).transform.SetParent(root.transform, true);
            }
            else
            {
                RoundedBox("Horse head", position + new Vector3(0, 3f, -.4f), new Vector3(1.2f, 1.4f, 2f), .3f, material).transform.SetParent(root.transform, true);
                for (int side = -1; side <= 1; side += 2)
                    Box("Horse ear", position + new Vector3(side * .4f, 3.9f, .2f), new Vector3(.25f, .7f, .4f), material).transform.SetParent(root.transform, true);
            }
            root.transform.localScale = Vector3.one * Mathf.Clamp(rookScale, .2f, 1f);
            Label("Piece location", position + Vector3.up * 3.2f, (kind == ChessWirePuzzle.Kind.Pawn ? "PAWN / " : "KNIGHT / ") + coordinate, .3f);
        }
        void BuildQueen(Vector3 position, string coordinate, Material material)
        {
            var root = new GameObject("Puzzle Queen"); root.transform.SetParent(transform, false); root.transform.position = position;
            RoundedBox("Queen base", position + Vector3.up * .4f, new Vector3(2.8f, .8f, 2.8f), .3f, material).transform.SetParent(root.transform, true);
            RoundedBox("Queen stem", position + Vector3.up * 2.7f, new Vector3(1.4f, 4f, 1.4f), .4f, material).transform.SetParent(root.transform, true);
            RoundedBox("Queen crown", position + Vector3.up * 4.8f, new Vector3(2.7f, .5f, 2.7f), .2f, material).transform.SetParent(root.transform, true);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.PI * 2f / 5f;
                var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere); tip.name = "Queen crown tip";
                tip.transform.SetParent(root.transform, false); tip.transform.localPosition = new Vector3(Mathf.Cos(angle), 5.45f, Mathf.Sin(angle));
                tip.transform.localScale = new Vector3(.5f, .9f, .5f); tip.GetComponent<Renderer>().sharedMaterial = material;
            }
            root.transform.localScale = Vector3.one * Mathf.Clamp(rookScale, .2f, 1f);
            Label("Queen location", position + Vector3.up * (5.9f * Mathf.Clamp(rookScale, .2f, 1f) + .6f), "QUEEN / " + coordinate, .35f);
        }
        GameObject MakeCutter(string name, Transform parent)
        {
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1 : 1;
                var handle = Box("Cutter handle", Vector3.zero, new Vector3(.09f, .12f, .48f), toolGrip, false);
                handle.transform.SetParent(root.transform, false); handle.transform.localPosition = new Vector3(sign * .12f, 0, -.15f);
                handle.transform.localRotation = Quaternion.Euler(0, sign * -15, 0);
                var jaw = Box("Cutter jaw", Vector3.zero, new Vector3(.09f, .13f, .28f), toolMetal, false);
                jaw.transform.SetParent(root.transform, false); jaw.transform.localPosition = new Vector3(sign * .08f, 0, .19f);
                jaw.transform.localRotation = Quaternion.Euler(0, sign * -20, 0);
            }
            return root;
        }
        void RebuildBoard(int team)
        {
            var root = boardRoots[team];
            if (root != null)
            {
                boundedLabels.RemoveAll(entry => entry.text == null || entry.text.transform.IsChildOf(root.transform));
                root.SetActive(false); Destroy(root);
                foreach (var mesh in boardMeshes[team]) { meshes.Remove(mesh); Destroy(mesh); }
                boardMeshes[team].Clear();
            }
            BuildDefuseBoards(team, boardMaterials[0], boardMaterials[1], boardMaterials[2], boardMaterials[3]);
            cuttingWire[team] = -1;
            ResetHelperRoutes(team);
        }
        void ResetPuzzle()
        {
            elapsed = 0;
            var layouts = ChessWirePuzzle.GenerateCourse(puzzleRandom, previousFirst);
            previousFirst = layouts[0];
            for (int team = 0; team < 2; team++)
            {
                var teamLayouts = new ChessWirePuzzle.Layout[layouts.Length];
                for (int stage = 0; stage < layouts.Length; stage++) teamLayouts[stage] = team == 0 ? layouts[stage] : layouts[stage].MirrorRanks();
                courses[team] = new ChessWirePuzzle.Course(teamLayouts, cutSeconds, wrongWireLock);
                puzzles[team] = courses[team].Current; solveTimes[team] = -1;
                RebuildBoard(team);
            }
            for (int i = 0; i < players.Count; i++) LoseCutter(i);
        }
        void AdvancePuzzle(int team)
        {
            if (!courses[team].Advance()) return;
            puzzles[team] = courses[team].Current;
            // Clear the changing board before enabling new piece colliders; preserve equipped tools.
            for (int i = 0; i < players.Count; i++)
            {
                var pos = players[i].Hips.position;
                if (Mathf.Abs(pos.x - BoardCentreX(team)) < 17f && pos.z > 13f && pos.z < 47f)
                    players[i].Teleport(new Vector3(BoardCentreX(team) + (i % 12 - 5.5f) * 2f,
                        players[i].standHeight + .04f, 10.1f), Vector3.forward);
            }
            RebuildBoard(team);
            notice = (team == 0 ? "백팀" : "흑팀") + $" {courses[team].Stage}단계 완료 · {courses[team].Stage + 1}단계 시작 (도구 유지 / 판 입구로 이동)";
        }
        void LoseCutter(int index)
        {
            hasCutter[index] = false;
            if (carriedCutters[index] != null) Destroy(carriedCutters[index]);
            carriedCutters[index] = null;
        }
        bool Near(Vector3 a, Vector3 b) => Mathf.Abs(a.y - b.y) < 2f && new Vector2(a.x - b.x, a.z - b.z).magnitude <= interactDistance;
        bool TryEquipCutter(int index)
        {
            int team = index / 6;
            if (!started || respawnAt[index] > 0 || rules.Teams[team].Finished ||
                players[index].State != PawnState.Active || !players[index].Grounded || hasCutter[index] ||
                !Near(players[index].Hips.position, ToolStation(team))) return false;
            hasCutter[index] = true;
            carriedCutters[index] = MakeCutter("Equipped wire cutter", players[index].handR.transform);
            carriedCutters[index].transform.localPosition = new Vector3(0, 0, .2f);
            return true;
        }
        void PickupCutter()
        {
            if (!menu && TryEquipCutter(selected)) notice = "절단기 획득 · 안전한 전선 앞에서 F를 길게 누르세요";
        }
        int ManualCutWire(int team)
        {
            if (selected / 6 != team || rules.Teams[team].Finished || respawnAt[selected] > 0 ||
                menu || !Application.isFocused || !Input.GetKey(KeyCode.F) || !hasCutter[selected] ||
                players[selected].State != PawnState.Active || !players[selected].Grounded) return -1;
            for (int n = 0; n < puzzles[team].Board.WireCount; n++)
                if (!puzzles[team].Cut(n) && Near(players[selected].Hips.position, WirePoint(team, n))) return n;
            return -1;
        }
        int TickPhysicalPuzzle(float dt)
        {
            elapsed += dt;
            bool[] solved = new bool[2]; float[] penalties = new float[2];
            for (int team = 0; team < 2; team++)
            {
                var requests = new System.Collections.Generic.List<ChessWirePuzzle.CutRequest>();
                int manual = ManualCutWire(team);
                if (manual >= 0) requests.Add(new ChessWirePuzzle.CutRequest(selected, manual));
                for (int i = team * 6; i < team * 6 + 6; i++)
                {
                    int target = helperCutWire[i];
                    if (i == selected || !IsCuttingHelper(i) || menu || !Application.isFocused ||
                        target < 0 || target >= puzzles[team].Board.WireCount || target == manual ||
                        respawnAt[i] > 0 || !hasCutter[i] || !players[i].Grounded || players[i].State != PawnState.Active ||
                        rules.Teams[team].Finished || !Near(players[i].Hips.position, WirePoint(team, target))) continue;
                    requests.Add(new ChessWirePuzzle.CutRequest(i, target));
                }
                cuttingWire[team] = manual;
                workers[team] = requests.Count;
                var result = puzzles[team].StepAll(dt, requests.ToArray());
                if (result.WrongCuts > 0)
                { penalties[team] = rules.WrongWirePenalty * result.WrongCuts; notice = $"오답 전선 {result.WrongCuts}개 · {penalties[team]:0}초 차감 / 잠시 잠금"; }
                solved[team] = courses[team].Complete;
                for (int wire = 0; wire < puzzles[team].Board.WireCount; wire++)
                    if (puzzles[team].Cut(wire)) wireCentres[team, wire].SetActive(false);
            }
            int blasts = rules.StepPuzzle(dt, riders[0], riders[1], solved[0], solved[1], penalties[0], penalties[1]);
            for (int team = 0; team < 2; team++)
            {
                if ((rules.CompletedTeams & (1 << team)) != 0) solveTimes[team] = elapsed;
                if (!rules.Teams[team].Finished && puzzles[team].Solved && !courses[team].Complete) AdvancePuzzle(team);
            }
            return blasts;
        }
        void UpdatePuzzleViews()
        {
            for (int i = 0; i < players.Count; i++)
            {
                bool working = false;
                var puzzle = puzzles[i / 6];
                for (int wire = 0; wire < puzzle.Board.WireCount; wire++)
                    if (puzzle.WorkerAt(wire) == i && puzzle.WireProgress(wire) > 0) working = true;
                if (carriedCutters[i] != null) carriedCutters[i].SetActive(!working);
            }
            for (int team = 0; team < 2; team++) for (int wire = 0; wire < puzzles[team].Board.WireCount; wire++)
            {
                float progress = puzzles[team].WireProgress(wire);
                bool cutting = progress > 0 && !rules.Teams[team].Finished;
                var tool = cuttingTools[team, wire]; tool.SetActive(cutting);
                if (!cutting) continue;
                tool.transform.position = WirePoint(team, wire) + new Vector3(0, .1f, -.19f);
                float opening = 20 * (1 - Mathf.Clamp01(progress / puzzles[team].CutSeconds));
                tool.transform.GetChild(1).localRotation = Quaternion.Euler(0, opening, 0);
                tool.transform.GetChild(3).localRotation = Quaternion.Euler(0, -opening, 0);
            }
            for (int team = 0; team < 2; team++) for (int wire = 0; wire < puzzles[team].Board.WireCount; wire++)
            {
                string coord = ((char)('A' + puzzles[team].Board.File(wire))).ToString() + (puzzles[team].Board.Rank(wire) + 1);
                string color = WireColorNames[wire];
                wireLabels[team, wire].text = (wire + 1) + " / " + color + " / " + coord + "\n" +
                    (rules.Teams[team].Finished ? "종료" : puzzles[team].Cut(wire) ? "절단됨" : puzzles[team].LockLeft > 0 ? "잠금" : puzzles[team].WireProgress(wire) > 0 ? $"절단 {puzzles[team].WireProgress(wire):0.0}/{puzzles[team].CutSeconds:0}초" : "절단기 + F 길게");
            }
        }
    }
}
