using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public sealed partial class ChessTimerMinigame
    {
        [Header("Physical wire puzzle (F3 resets)")]
        [SerializeField, Min(.1f)] float cutSeconds = 1.5f;
        [SerializeField, Range(.2f, 1f), Tooltip("Puzzle piece visual and collision scale; restart Play to apply.")] float rookScale = .6f;
        [SerializeField, Min(0)] float wrongWireLock = 2f;
        readonly ChessWirePuzzle[] puzzles = new ChessWirePuzzle[2];
        readonly bool[] hasCutter = new bool[12];
        readonly GameObject[] carriedCutters = new GameObject[12];
        readonly GameObject[,] wireCentres = new GameObject[2, 3];
        readonly TextMesh[,] wireLabels = new TextMesh[2, 3];
        readonly TextMesh[] boardSigns = new TextMesh[2];
        readonly GameObject[] attackHints = new GameObject[2];
        readonly GameObject[] cuttingTools = new GameObject[2];
        readonly int[] cuttingWire = { -1, -1 };
        readonly float[] solveTimes = { -1f, -1f };
        float elapsed;
        Material toolMetal, toolGrip;
        static float BoardCentreX(int team) => team == 0 ? -21f : 21f;
        static Vector3 BoardSquare(int team, int file, int rank) =>
            new Vector3(BoardCentreX(team) + (file - 3.5f) * TileSize, .06f, 30 + (rank - 3.5f) * TileSize);
        static Vector3 ToolStation(int team) => new Vector3(BoardCentreX(team), .9f, 11.5f);
        static Vector3 WirePoint(int team, int wire) => BoardSquare(team, ChessWirePuzzle.File(wire), ChessWirePuzzle.Rank(wire)) + Vector3.up * .7f;

        void BuildDefuseBoards(Material ivory, Material walnut, Material cream, Material charcoal)
        {
            toolMetal = charcoal; toolGrip = Mat(new Color(1f, .68f, .15f));
            var colors = new[] { Mat(new Color(.92f, .16f, .12f)), Mat(new Color(.12f, .4f, 1f)), Mat(new Color(1f, .75f, .05f)) };
            var hint = Mat(new Color(.8f, .28f, .19f));
            for (int team = 0; team < 2; team++)
            {
                attackHints[team] = new GameObject("Piece attack hints"); attackHints[team].transform.SetParent(transform, false);
                for (int file = 0; file < ChessWirePuzzle.Size; file++) for (int rank = 0; rank < ChessWirePuzzle.Size; rank++)
                {
                    var centre = BoardSquare(team, file, rank);
                    // Flush walking surface: tile graphics have no extra collision lips.
                    Box("Board tile " + (char)('A' + file) + (rank + 1), new Vector3(centre.x, .015f, centre.z),
                        new Vector3(TileSize, .03f, TileSize), (file + rank) % 2 == 0 ? ivory : walnut, false);
                    if (ChessWirePuzzle.Attacked(file, rank))
                    {
                        var mark = Box("Attack hint", new Vector3(centre.x, .04f, centre.z), new Vector3(TileSize - .2f, .015f, .18f), hint, false);
                        mark.transform.SetParent(attackHints[team].transform, true);
                    }
                }
                attackHints[team].SetActive(false);
                cuttingTools[team] = MakeCutter("Cutter at wire", transform); cuttingTools[team].SetActive(false);
                for (int index = 0; index < 8; index++)
                {
                    Label("File coordinate", BoardSquare(team, index, 0) + new Vector3(0, .15f, -2.5f), ((char)('A' + index)).ToString(), .5f);
                    Label("Rank coordinate", BoardSquare(team, 0, index) + new Vector3(-2.6f, .15f, 0), (index + 1).ToString(), .5f);
                }
                for (int piece = 0; piece < ChessWirePuzzle.PieceCount; piece++)
                {
                    var rook = BoardSquare(team, ChessWirePuzzle.PieceFile(piece), ChessWirePuzzle.PieceRank(piece));
                    BuildPieceGuide(team, piece, ChessWirePuzzle.IsBishop(piece), walnut, cream, charcoal);
                    if (ChessWirePuzzle.IsBishop(piece)) { BuildBishop(rook, cream, charcoal); continue; }
                    var rookRoot = new GameObject("Puzzle rook");
                    rookRoot.transform.SetParent(transform, false); rookRoot.transform.position = rook;
                    RoundedBox("Giant rook base", rook + Vector3.up * .4f, new Vector3(2.8f, .8f, 2.8f), .25f, cream).transform.SetParent(rookRoot.transform, true);
                    RoundedBox("Giant rook tower", rook + Vector3.up * 3f, new Vector3(1.9f, 4.8f, 1.9f), .2f, cream).transform.SetParent(rookRoot.transform, true);
                    Box("Giant rook crown", rook + Vector3.up * 5.5f, new Vector3(2.7f, .6f, 2.7f), cream).transform.SetParent(rookRoot.transform, true);
                    for (int corner = 0; corner < 4; corner++)
                        Box("Rook battlement", rook + new Vector3(corner % 2 == 0 ? -1 : 1, 6.1f, corner < 2 ? -1 : 1), new Vector3(.65f, .7f, .65f), cream).transform.SetParent(rookRoot.transform, true);
                    float scale = Mathf.Clamp(rookScale, .2f, 1f);
                    rookRoot.transform.localScale = Vector3.one * scale;
                    Label("Rook location", rook + new Vector3(0, 6.45f * scale + .6f, 0), "ROOK / " + (char)('A' + ChessWirePuzzle.PieceFile(piece)) + (ChessWirePuzzle.PieceRank(piece) + 1), .35f);
                }
                for (int wire = 0; wire < 3; wire++)
                {
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
                    "8 x 8 / ROOK x2 + BISHOP\n세 기물 모두에게 안전한 칸의 전선을 자르세요\nF: 절단기 획득 / H: 공격 방향 힌트", .25f);
                Label("Court route", new Vector3(BoardCentreX(team), .8f, 8), "해체 구역 / 절단기 → 전선 → 출구", .3f);
            }
        }
        void BuildPieceGuide(int team, int piece, bool bishop, Material frame, Material paper, Material ink)
        {
            // Mount on the inner face of the outer wall, outside the playable chessboard.
            float side = team == 0 ? -1f : 1f;
            var centre = new Vector3(side * (ArenaWidth * .5f - .36f), 2.5f, 18f + piece * 12f);
            var root = new GameObject(bishop ? "Bishop movement guide" : "Rook movement guide");
            root.transform.SetParent(transform, false); root.transform.position = centre;
            Box("Guide frame", centre, new Vector3(3.2f, 3.8f, .18f), frame, false).transform.SetParent(root.transform, true);
            var content = new GameObject("Guide face"); content.transform.SetParent(root.transform, false);
            Box("Guide paper", centre + Vector3.back * .105f, new Vector3(3f, 3.6f, .025f), paper, false).transform.SetParent(content.transform, true);
            var title = Label("Guide title", centre + new Vector3(0, 1.45f, -.13f), bishop ? "비숍 · 대각선" : "룩 · 가로와 세로", .25f);
            title.color = Color.black; title.transform.SetParent(content.transform, true);
            for (int x = -2; x <= 2; x++) for (int y = -2; y <= 2; y++)
            {
                bool middle = x == 0 && y == 0;
                bool reachable = bishop ? Mathf.Abs(x) == Mathf.Abs(y) : x == 0 || y == 0;
                var pos = centre + new Vector3(x * .33f, .28f + y * .33f, -.135f);
                Box("Movement diagram cell", pos, new Vector3(.30f, .30f, .012f),
                    middle ? ink : reachable ? toolGrip : frame, false).transform.SetParent(content.transform, true);
            }
            var symbol = Label("Diagram piece", centre + new Vector3(0, .28f, -.15f), bishop ? "B" : "R", .22f);
            symbol.color = Color.white; symbol.transform.SetParent(content.transform, true);
            string explanation = (bishop ? "대각선으로 이동" : "가로·세로로 이동") +
                "\n막힐 때까지 여러 칸\n다른 기물은 넘지 못함";
            var caption = Label("Guide explanation", centre + new Vector3(0, -1.18f, -.13f), explanation, .18f);
            caption.color = Color.black; caption.transform.SetParent(content.transform, true);
            var legend = Label("Diagram legend", centre + new Vector3(0, -.66f, -.13f), "금색 칸 = 이동 방향", .15f);
            legend.color = Color.black; legend.transform.SetParent(content.transform, true);
            // Text faces local -Z; turn the complete frame toward its team court.
            root.transform.localRotation = Quaternion.Euler(0, side * 90f, 0);
        }
        void BuildBishop(Vector3 position, Material cream, Material dark)
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
            Label("Bishop location", position + Vector3.up * (6.3f * scale + .6f), "BISHOP / C6", .35f);
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
        void ResetPuzzle()
        {
            elapsed = 0;
            for (int team = 0; team < 2; team++)
            {
                puzzles[team] = new ChessWirePuzzle(cutSeconds, wrongWireLock); solveTimes[team] = -1; cuttingWire[team] = -1;
                cuttingTools[team].SetActive(false);
                attackHints[team].SetActive(false);
                for (int wire = 0; wire < 3; wire++) wireCentres[team, wire].SetActive(true);
            }
            for (int i = 0; i < players.Count; i++) LoseCutter(i);
        }
        void LoseCutter(int index)
        {
            hasCutter[index] = false;
            if (carriedCutters[index] != null) Destroy(carriedCutters[index]);
            carriedCutters[index] = null;
        }
        bool Near(Vector3 a, Vector3 b) => Mathf.Abs(a.y - b.y) < 2f && new Vector2(a.x - b.x, a.z - b.z).magnitude <= interactDistance;
        void PickupCutter()
        {
            int team = selected / 6;
            if (!started || menu || respawnAt[selected] > 0 || rules.Teams[team].Finished ||
                players[selected].State != PawnState.Active || !players[selected].Grounded || hasCutter[selected] ||
                !Near(players[selected].Hips.position, ToolStation(team))) return;
            hasCutter[selected] = true;
            carriedCutters[selected] = MakeCutter("Equipped wire cutter", players[selected].handR.transform);
            carriedCutters[selected].transform.localPosition = new Vector3(0, 0, .2f);
            notice = "절단기 획득 · 체스판으로 이동해 안전한 전선 앞에서 F를 길게 누르세요";
        }
        int TickPhysicalPuzzle(float dt)
        {
            elapsed += dt;
            bool[] solved = new bool[2]; float[] penalties = new float[2];
            for (int team = 0; team < 2; team++)
            {
                int wire = -1, worker = -1;
                if (selected / 6 == team && !rules.Teams[team].Finished && respawnAt[selected] <= 0 &&
                    !menu && Application.isFocused && Input.GetKey(KeyCode.F) && hasCutter[selected] &&
                    players[selected].State == PawnState.Active && players[selected].Grounded)
                {
                    for (int n = 0; n < 3; n++)
                        if (!puzzles[team].Cut(n) && Near(players[selected].Hips.position, WirePoint(team, n)))
                        { wire = n; worker = selected; break; }
                }
                cuttingWire[team] = wire;
                workers[team] = worker >= 0 ? 1 : 0;
                var result = puzzles[team].Step(dt, worker, wire, worker >= 0);
                if (result == ChessWirePuzzle.Result.Wrong)
                { penalties[team] = rules.WrongWirePenalty; notice = $"오답! 전체 제한시간의 1/9인 {rules.WrongWirePenalty:0.##}초 차감 / 잠시 잠금"; }
                solved[team] = result == ChessWirePuzzle.Result.Solved;
                if (wire >= 0 && puzzles[team].Cut(wire)) wireCentres[team, wire].SetActive(false);
            }
            int blasts = rules.StepPuzzle(dt, riders[0], riders[1], solved[0], solved[1], penalties[0], penalties[1]);
            for (int team = 0; team < 2; team++)
                if ((rules.CompletedTeams & (1 << team)) != 0) solveTimes[team] = elapsed;
            return blasts;
        }
        void UpdatePuzzleViews()
        {
            for (int i = 0; i < players.Count; i++)
                if (carriedCutters[i] != null) carriedCutters[i].SetActive(i != selected || puzzles[i / 6].Progress <= 0);
            for (int team = 0; team < 2; team++)
            {
                bool cutting = cuttingWire[team] >= 0 && puzzles[team].Progress > 0 && !rules.Teams[team].Finished;
                cuttingTools[team].SetActive(cutting);
                if (!cutting) continue;
                cuttingTools[team].transform.position = WirePoint(team, cuttingWire[team]) + new Vector3(0, .1f, -.19f);
                float opening = 20 * (1 - Mathf.Clamp01(puzzles[team].Progress / puzzles[team].CutSeconds));
                cuttingTools[team].transform.GetChild(1).localRotation = Quaternion.Euler(0, opening, 0);
                cuttingTools[team].transform.GetChild(3).localRotation = Quaternion.Euler(0, -opening, 0);
            }
            for (int team = 0; team < 2; team++) for (int wire = 0; wire < 3; wire++)
            {
                string coord = ((char)('A' + ChessWirePuzzle.File(wire))).ToString() + (ChessWirePuzzle.Rank(wire) + 1);
                string color = wire == 0 ? "빨강" : wire == 1 ? "파랑" : "노랑";
                wireLabels[team, wire].text = (wire + 1) + " / " + color + " / " + coord + "\n" +
                    (rules.Teams[team].Finished ? "종료" : puzzles[team].Cut(wire) ? "절단됨" : puzzles[team].LockLeft > 0 ? "잠금" : "절단기 + F 길게");
            }
        }
    }
}
