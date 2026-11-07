using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A's one chessboard for both teams of a plaza (mini-games v1.0 §3): 18 x 18 m across white's station, the
    // aisle and black's station, files a..h from white's side to black's, ranks 1..8 from u 2 to u 20, a1 dark.
    // White paints the 32 light squares, black the 32 dark; a square takes 1.5 s of a standing pawn of its
    // colour's team within 1 m of its middle, at most four squares a team at once, and stops (not resets) while
    // a pawn of the other team stands on it. All 64 squares start the same slate, so the board shows as it fills.
    //
    // Both stations of the plaza build an MgPaint (their progress, their gate); the first one built makes this
    // board in its game, the second finds it (For). The host steps the board; each station sends its team's half
    // (§3.6: painted bits and four charges), and a client draws the plates and plays the moments from those.
    // Board frame: x = v (toward black's station), y = 0 the dais top, z = u.
    public sealed class PaintBoard : MonoBehaviour
    {
        public const int PerTeam = 32;
        public const float Hold = 1.5f, Reach = 1f, Tile = 2.15f, PlateMax = .87f, TileHeight = .1f;

        static readonly List<PaintBoard> boards = new List<PaintBoard>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            boards.Clear();
        }

        [SerializeField] int plaza;
        [SerializeField] PawnRushSection section;
        [SerializeField] MissionStation white, black;
        [SerializeField] Transform[] squares = new Transform[64];
        [SerializeField] Renderer[] tiles = new Renderer[64];
        [SerializeField] Transform[] plates = new Transform[64];
        [SerializeField] Renderer[] rims = new Renderer[64];
        [SerializeField] Renderer[] strokes = new Renderer[64];
        [SerializeField] GameObject[] emblems = new GameObject[64];
        [SerializeField] TextMesh[] rankText = new TextMesh[16];

        struct Charge
        {
            public int Square;
            public float Time;
            public bool Blocked;
        }

        readonly bool[] painted = new bool[64];
        readonly List<Charge>[] charging = { new List<Charge>(), new List<Charge>() };
        readonly float[] target = new float[64], shown = new float[64], hopAt = new float[64], hopHeight = new float[64];
        readonly bool[] blocked = new bool[64], wasCharging = new bool[64], wasBlocked = new bool[64], rankDone = new bool[9];
        readonly AudioSource[] chargeVoice = new AudioSource[64];
        readonly float[] nextSpark = new float[64];
        PaintBoardFx fx;

        // R506: A's own particle effects (PaintBoardFx), made on first use on this board.
        PaintBoardFx Fx
        {
            get
            {
                if (fx == null) fx = GetComponent<PaintBoardFx>();
                if (fx == null) fx = gameObject.AddComponent<PaintBoardFx>();
                return fx;
            }
        }
        readonly bool[] heard = new bool[2];
        readonly int[] count = new int[2];
        readonly List<int>[] standing = { new List<int>(), new List<int>() };
        readonly HashSet<ICharacterDriver> seen = new HashSet<ICharacterDriver>();
        readonly Collider[] buffer = new Collider[256];
        float sweepAt = -1f;
        int sweepRank;

        public int Plaza => plaza;
        public int Count(int team) => team == Teams.Black ? count[1] : count[0];

        // The team whose colour a square is: white the light squares, black the dark (a1 is dark).
        public static int TeamOf(int square) =>
            PawnRushBoard.IsLight(PawnRushBoard.FileOf(square), PawnRushBoard.RankOf(square)) ? Teams.White : Teams.Black;

        static int Slot(int team) => team == Teams.Black ? 1 : 0;

        // ------------------------------------------------------------------ building

        // The plaza's board: the one already built, or a new one in `under` (the asking station's game).
        public static PaintBoard For(MissionStation station, Transform under)
        {
            var section = station.GetComponentInParent<PawnRushSection>(true);
            boards.RemoveAll(b => b == null);
            foreach (var b in boards)
                if (b.plaza == station.Plaza && b.section == section)
                {
                    b.Link(station);
                    return b;
                }
            var go = new GameObject("Shared board");
            go.transform.SetParent(under, false);
            var board = go.AddComponent<PaintBoard>();
            board.plaza = station.Plaza;
            board.section = section;
            board.Link(station);
            // A section is built while its Content is inactive. Its other station must still be linked before
            // Make chooses the shared board's centre; unrelated prefab instances never share a board.
            var neighbours = section != null ? section.GetComponentsInChildren<MissionStation>(true)
                : Object.FindObjectsByType<MissionStation>(FindObjectsSortMode.None);
            foreach (var other in neighbours)
                if (other != station && other.Plaza == station.Plaza && other.Team != station.Team
                    && other.GetComponentInParent<PawnRushSection>(true) == section) board.Link(other);
            board.Make();
            boards.Add(board);
            return board;
        }

        void Awake()
        {
            if (!boards.Contains(this)) boards.Add(this);
        }

        void OnDestroy() => boards.Remove(this);

        bool PalaceStyle => (white != null ? white : black) is MissionStation station && station.PalaceStyle;
        Color TeamColor(int team) => Arena.TeamColor(team, PalaceStyle);

        void Link(MissionStation station)
        {
            if (station.Team == Teams.Black) black = station;
            else white = station;
        }

        void Make()
        {
            var first = white != null ? white : black;
            // Between the two stations, on the dais; files run from white's side to black's.
            var mid = white != null && black != null ? (white.transform.position + black.transform.position) * .5f : first.transform.position;
            transform.SetPositionAndRotation(mid + first.transform.up * Arena.DaisHeight, first.transform.rotation);
            if (white != null && black != null && Vector3.Dot(black.transform.position - white.transform.position, first.transform.right) < 0f)
                Debug.LogWarning("[PawnRush] A 공유 체스판: 흑 스테이션이 백의 서쪽에 있습니다. 파일 a~h가 흑 쪽에서 시작합니다: " + name);
            var b = new CourseBuilder(first.Kit, transform);

            // The aisle between the stations becomes dais too (§3.1: one arena under the shared board).
            if (white != null && black != null)
            {
                float aisle = (Vector3.Distance(Flat(white.transform.position), Flat(black.transform.position)) - MissionStation.Size) * .5f;
                if (aisle > .05f)
                {
                    var dais = b.Box("Aisle dais", new Vector3(-aisle, -Arena.DaisHeight, 0f), new Vector3(aisle, 0f, MissionStation.Size),
                        PalaceStyle ? MiniGamePalaceArt.DaisMaterial(PawnRushMissions.Paint) : Arena.DaisMaterial);
                    dais.AddComponent<CourseFloor>();
                }
            }

            // One collider for the whole board, its slate showing in the joints; gold lines in the joints.
            float half = PawnRushBoard.N * PawnRushBoard.Cell * .5f, u0 = PawnRushBoard.U0, u1 = u0 + 2f * half;
            b.Box("Board", new Vector3(-half, 0f, u0), new Vector3(half, TileHeight, u1), Arena.Lit("M_BoardJoint", Arena.Groove, 0f, .8f)).AddComponent<CourseFloor>();
            var joint = Arena.Emissive(Arena.Gold, .6f);
            for (int k = 1; k < PawnRushBoard.N; k++)
            {
                float at = -half + k * PawnRushBoard.Cell;
                Arena.Picture(transform, "Joint file", new Vector3(at - .035f, 0f, u0), new Vector3(at + .035f, .08f, u1), joint);
                at = u0 + k * PawnRushBoard.Cell;
                Arena.Picture(transform, "Joint rank", new Vector3(-half, 0f, at - .035f), new Vector3(half, .08f, at + .035f), joint);
            }

            var slate = (PalaceStyle ? Stone("PR_A_Slate") : null) ?? Arena.Lit("M_BoardSquare", Arena.Slate, 0f, .45f);
            var lightDot = Arena.Lit("M_BoardDotLight", Arena.Hex("#F3EADB"), .3f, .5f);
            var darkDot = Arena.Lit("M_BoardDotDark", Arena.Hex("#15110F"), 0f, .5f);
            for (int s = 0; s < 64; s++)
            {
                int f = PawnRushBoard.FileOf(s), r = PawnRushBoard.RankOf(s);
                var c = PawnRushBoard.Center(f, r);
                var sq = new GameObject("Square " + PawnRushBoard.Name(f, r)).transform;
                sq.SetParent(transform, false);
                sq.localPosition = new Vector3(c.V, 0f, c.U);
                squares[s] = sq;
                float h = Tile * .5f;
                tiles[s] = Arena.Picture(sq, "Tile", new Vector3(-h, 0f, -h), new Vector3(h, TileHeight + .005f, h), slate).GetComponent<Renderer>();
                if (PalaceStyle) StoneUV(tiles[s], s);
                // The colour dots in the south-west and north-east corners: the board's colours before any paint.
                var dot = TeamOf(s) == Teams.White ? lightDot : darkDot;
                MiniGameParts.Part(sq, PrimitiveType.Cylinder, new Vector3(-.8f, TileHeight + .01f, -.8f), new Vector3(.26f, .005f, .26f), dot);
                MiniGameParts.Part(sq, PrimitiveType.Cylinder, new Vector3(.8f, TileHeight + .01f, .8f), new Vector3(.26f, .005f, .26f), dot);
                int team = TeamOf(s);
                // The charging plate (R506, user 11-07: "진짜 칠하는 걸로"): the square is brushed, not a square growing
                // from its middle. A full-size sheet of the team's stone, revealed stroke by stroke as the charge runs
                // (BrushStrokes: back and forth in bands, a ragged bristle edge, streaks); each square brushes its own
                // way (a quarter turn by its index). A thin glowing outline marks the square while it is painted.
                var plate = new GameObject("Plate");
                plate.transform.SetParent(sq, false);
                plate.transform.localPosition = new Vector3(0f, TileHeight + .008f, 0f);
                plate.transform.localRotation = Quaternion.Euler(0f, 90f * ((s * 7 + 3) % 4), 0f);
                var rim = new GameObject("Plate rim").transform;
                rim.SetParent(plate.transform, false);
                float rh = Tile * .5f, w = .07f;
                foreach (var (lo, hi) in new[] { (new Vector3(-rh - w, 0f, -rh - w), new Vector3(rh + w, .006f, -rh)), (new Vector3(-rh - w, 0f, rh), new Vector3(rh + w, .006f, rh + w)),
                                                 (new Vector3(-rh - w, 0f, -rh), new Vector3(-rh, .006f, rh)), (new Vector3(rh, 0f, -rh), new Vector3(rh + w, .006f, rh)) })
                    Arena.Picture(rim, "Plate rim edge", lo, hi, Arena.Glow(TeamColor(team)));
                rims[s] = rim.GetChild(0).GetComponent<Renderer>();
                var sheet = GameObject.CreatePrimitive(PrimitiveType.Quad);
                sheet.name = "Plate paint";
                if (Application.isPlaying) Destroy(sheet.GetComponent<Collider>()); else DestroyImmediate(sheet.GetComponent<Collider>());
                sheet.transform.SetParent(plate.transform, false);
                sheet.transform.localPosition = new Vector3(0f, .012f, 0f);
                sheet.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                sheet.transform.localScale = new Vector3(Tile, Tile, 1f);
                strokes[s] = sheet.GetComponent<Renderer>();
                strokes[s].sharedMaterial = StrokeMaterial(team);
                strokes[s].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                plates[s] = plate.transform;
                plate.SetActive(false);
                emblems[s] = Emblem(sq, team);
                emblems[s].SetActive(false);
            }

            // Rank numbers on both sides, file letters along the south edge (the north edge is the gate wall).
            var dim = Arena.Gold * .4f;
            dim.a = 1f;
            for (int r = 1; r <= PawnRushBoard.N; r++)
            {
                float u = PawnRushBoard.Center(0, r).U;
                rankText[r - 1] = FloorText("Rank " + r, new Vector3(-half - .45f, u), r.ToString(), dim);
                rankText[7 + r] = FloorText("Rank " + r, new Vector3(half + .45f, u), r.ToString(), dim);
            }
            for (int f = 0; f < PawnRushBoard.N; f++)
                FloorText("File " + (char)('a' + f), new Vector3(PawnRushBoard.Center(f, 1).V, u0 - .45f), ((char)('a' + f)).ToString(), dim);
        }

        static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0f, p.z);

        TextMesh FloorText(string name, Vector3 vu, string text, Color color)
        {
            var t = Arena.Text(transform, name, new Vector3(vu.x, TileHeight + .01f, vu.y), .4f, color);
            t.text = text;
            t.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return t;
        }

        // The team's pawn drawn flat on a painted square (0.9 m): white for white, black for black.
        static GameObject Emblem(Transform square, int team)
        {
            var root = new GameObject("Emblem");
            root.transform.SetParent(square, false);
            root.transform.localPosition = new Vector3(0f, TileHeight + .03f, 0f);
            var m = MiniGameParts.PieceMaterial(team);
            MiniGameParts.Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0f, -.36f), new Vector3(.62f, .004f, .14f), m);
            MiniGameParts.Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0f, -.1f), new Vector3(.32f, .004f, .42f), m);
            MiniGameParts.Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, .24f), new Vector3(.36f, .002f, .36f), m);
            return root;
        }

        static readonly Material[] paint = new Material[4];

        // R506 (picture only): in the palace sections the squares wear the court's stone, unpainted slate and the
        // teams' ivory limestone and black basalt (ArtSource/PawnRush/MiniGames/Early/Materials/r506_material_swatches.py,
        // installed by a_install.py). Missing materials leave the flat colours. Each square shifts the 2 m tile so
        // neighbours never repeat; the offset lives on the renderer, so it stays when the square's material changes.
        static Material Stone(string name) => Resources.Load<Material>("PawnRushMiniGames/Materials/" + name);
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        static void StoneUV(Renderer tile, int square)
        {
            var block = new MaterialPropertyBlock();
            tile.GetPropertyBlock(block);
            block.SetVector(BaseMapST, new Vector4(Tile * .5f, Tile * .5f, Mathf.Repeat(square * .618f, 1f), Mathf.Repeat(square * .382f + .17f, 1f)));
            tile.SetPropertyBlock(block);
        }

        // §3.4: white's paint is cream glowing sky blue, black's is near-black glowing orange.
        Material PaintMaterial(int team)
        {
            int i = Slot(team) + (PalaceStyle ? 2 : 0);
            if (paint[i] != null) return paint[i];
            var stone = PalaceStyle ? Stone(team == Teams.Black ? "PR_A_Basalt" : "PR_A_IvoryLimestone") : null;
            var m = new Material(stone ?? Arena.Lit(team == Teams.Black ? "M_PaintBlack" : "M_PaintWhite", Arena.Hex(team == Teams.Black ? "#231E1B" : "#F3EADB"), 0f, .5f));
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", TeamColor(team) * .25f);
            paint[i] = m;
            return m;
        }

        // R498: a station's gold rim (Arena.Rim) that runs under this board has the board's top (both TileHeight
        // over the dais), so it flickered through the joints. Keep only the parts of `game`'s rims outside the
        // board's footprint. Both stations call this for their own game once the board exists.
        public void ClipRims(Transform game)
        {
            if (game == null) return;
            float half = PawnRushBoard.N * PawnRushBoard.Cell * .5f, u0 = PawnRushBoard.U0, u1 = u0 + 2f * half;
            // The footprint in the game's frame (the frames differ by a shift and a turn of 0 or 180 degrees).
            var a = game.InverseTransformPoint(transform.TransformPoint(new Vector3(-half, 0f, u0)));
            var c = game.InverseTransformPoint(transform.TransformPoint(new Vector3(half, 0f, u1)));
            float x0 = Mathf.Min(a.x, c.x), x1 = Mathf.Max(a.x, c.x), z0 = Mathf.Min(a.z, c.z), z1 = Mathf.Max(a.z, c.z);
            var rimList = new List<Transform>();
            foreach (Transform child in game) if (child.name.StartsWith("Rim ")) rimList.Add(child);
            foreach (var rim in rimList)
            {
                Vector3 p = rim.localPosition, s = rim.localScale;
                float rx0 = p.x - s.x * .5f, rx1 = p.x + s.x * .5f, rz0 = p.z - s.z * .5f, rz1 = p.z + s.z * .5f;
                if (rx1 <= x0 || rx0 >= x1 || rz1 <= z0 || rz0 >= z1) continue;
                // What is left of the strip once the footprint is cut out: (x0, x1, z0, z1) rectangles.
                var pieces = new List<Vector4>();
                if (rx0 < x0) pieces.Add(new Vector4(rx0, x0, rz0, rz1));
                if (rx1 > x1) pieces.Add(new Vector4(x1, rx1, rz0, rz1));
                float cx0 = Mathf.Max(rx0, x0), cx1 = Mathf.Min(rx1, x1);
                if (rz0 < z0) pieces.Add(new Vector4(cx0, cx1, rz0, z0));
                if (rz1 > z1) pieces.Add(new Vector4(cx0, cx1, z1, rz1));
                pieces.RemoveAll(q => q.y - q.x < .02f || q.w - q.z < .02f);
                if (pieces.Count == 0)
                {
                    Object.DestroyImmediate(rim.gameObject);
                    continue;
                }
                float y0 = p.y - s.y * .5f, y1 = p.y + s.y * .5f;
                var material = rim.GetComponent<Renderer>().sharedMaterial;
                for (int i = 0; i < pieces.Count; i++)
                {
                    var q = pieces[i];
                    Vector3 min = new Vector3(q.x, y0, q.z), max = new Vector3(q.y, y1, q.w);
                    if (i == 0)
                    {
                        rim.localPosition = (min + max) * .5f;
                        rim.localScale = max - min;
                    }
                    else Arena.Picture(game, rim.name, min, max, material);
                }
            }
        }

        // ------------------------------------------------------------------ the host's game

        void FixedUpdate()
        {
            if (MiniGameBase.Remote) return;
            Gather();
            float dt = Time.fixedDeltaTime;
            for (int team = 0; team < 2; team++)
            {
                var list = charging[team];
                var mine = standing[team];
                for (int k = list.Count - 1; k >= 0; k--)
                {
                    var c = list[k];
                    if (painted[c.Square] || !mine.Contains(c.Square)) { list.RemoveAt(k); continue; }
                    c.Blocked = blocked[c.Square];
                    if (!c.Blocked) c.Time += dt;
                    if (c.Time >= Hold)
                    {
                        list.RemoveAt(k);
                        Paint(c.Square, true);
                        continue;
                    }
                    list[k] = c;
                }
                foreach (int s in mine)
                {
                    if (list.Count >= PawnRushMissions.WorkerCap) break;
                    if (painted[s] || Has(list, s)) continue;
                    list.Add(new Charge { Square = s, Time = 0f, Blocked = blocked[s] });
                }
            }
            for (int s = 0; s < 64; s++) target[s] = 0f;
            for (int team = 0; team < 2; team++)
                foreach (var c in charging[team])
                {
                    target[c.Square] = Mathf.Clamp01(c.Time / Hold);
                    if (target[c.Square] <= 0f) target[c.Square] = .001f;
                }
        }

        static bool Has(List<Charge> list, int square)
        {
            foreach (var c in list) if (c.Square == square) return true;
            return false;
        }

        // Every standing pawn over the board: its team's squares it can paint (within 1 m of the middle), and the
        // squares it blocks for the other team (anywhere in the square).
        void Gather()
        {
            standing[0].Clear();
            standing[1].Clear();
            for (int s = 0; s < 64; s++) blocked[s] = false;
            seen.Clear();
            float half = PawnRushBoard.N * PawnRushBoard.Cell * .5f;
            var center = transform.TransformPoint(new Vector3(0f, 1.5f, PawnRushBoard.U0 + half));
            int n = Physics.OverlapBoxNonAlloc(center, new Vector3(half + .5f, 2.5f, half + .5f), buffer, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var driver = buffer[i].GetComponentInParent<ICharacterDriver>();
                if (driver == null || driver.FollowTarget == null || !seen.Add(driver)) continue;
                if (driver is IMiniGamePawn mg && mg.Down) continue;
                int team = driver is ITeamMember m ? m.Team : Teams.None;
                if (team != Teams.White && team != Teams.Black) continue;
                var local = transform.InverseTransformPoint(driver.FollowTarget.position);
                if (local.y > 2.5f) continue;
                var at = new StationPoint(local.x, local.z);
                if (!PawnRushBoard.SquareAt(at, out int f, out int r)) continue;
                int s = PawnRushBoard.Index(f, r);
                if (TeamOf(s) != team) { blocked[s] = true; continue; }
                var c = PawnRushBoard.Center(f, r);
                if (Mathf.Abs(at.V - c.V) <= Reach && Mathf.Abs(at.U - c.U) <= Reach && !standing[Slot(team)].Contains(s)) standing[Slot(team)].Add(s);
            }
        }

        // ------------------------------------------------------------------ moments (every PC)

        void Paint(int s, bool moments)
        {
            if (painted[s]) return;
            painted[s] = true;
            int team = TeamOf(s);
            count[Slot(team)]++;
            tiles[s].sharedMaterial = PaintMaterial(team);
            emblems[s].SetActive(true);
            target[s] = 0f;
            shown[s] = 0f;
            plates[s].gameObject.SetActive(false);
            Cut(s);
            if (!moments) return;
            hopAt[s] = Time.time;
            hopHeight[s] = .18f;
            var at = transform.TransformPoint(squares[s].localPosition + Vector3.up * (TileHeight + .02f));
            var color = TeamColor(team);
            // R506: A's own particles (PaintBoardFx) instead of the shared ring, cube bits and light column: the square's
            // new stone (ivory / basalt) splashes on, droplets fly, it twinkles and a soft light ring runs out.
            Fx.Painted(at, color, Arena.Hex(team == Teams.Black ? "#2E2A26" : "#EEE3D0"), Tile);
            int rank = PawnRushBoard.RankOf(s);
            if (!rankDone[rank])
            {
                bool all = true;
                for (int f = 0; f < PawnRushBoard.N && all; f++) all = painted[PawnRushBoard.Index(f, rank)];
                if (all)
                {
                    rankDone[rank] = true;
                    var gold = Arena.Gold;
                    rankText[rank - 1].color = gold;
                    rankText[7 + rank].color = gold;
                    sweepAt = Time.time;
                    sweepRank = rank;
                    // R506: a gold fountain of twinkles and a ring at both ends of the rank, where its numbers are.
                    float half = PawnRushBoard.N * PawnRushBoard.Cell * .5f;
                    foreach (float v in new[] { -half, half })
                        Fx.RankEnd(transform.TransformPoint(new Vector3(v, TileHeight, PawnRushBoard.Center(0, rank).U)), gold);
                }
            }
            if (count[Slot(team)] >= PerTeam) Wave(team);
        }

        // A team done: its 32 squares hop 0.1 m one after another from a1's corner to h8's, 0.8 s in all.
        void Wave(int team)
        {
            for (int s = 0; s < 64; s++)
            {
                if (TeamOf(s) != team) continue;
                int f = PawnRushBoard.FileOf(s), r = PawnRushBoard.RankOf(s);
                hopAt[s] = Time.time + (f + r - 1) / 14f * .8f;
                hopHeight[s] = .1f;
            }
            // R506: the moment heard and seen from anywhere on the court: a fanfare, confetti thrown up from the board's
            // four corners (team colour, gold, cream), a wide soft ring and glitter over the whole board.
            float half = PawnRushBoard.N * PawnRushBoard.Cell * .5f, u0 = PawnRushBoard.U0;
            var centre = transform.TransformPoint(new Vector3(0f, TileHeight, u0 + half));
            var corners = new Vector3[4];
            int i = 0;
            foreach (float v in new[] { -half, half })
                foreach (float u in new[] { u0, u0 + 2f * half })
                    corners[i++] = transform.TransformPoint(new Vector3(v, TileHeight, u));
            Fx.TeamDone(centre, corners, half, TeamColor(team), Arena.Gold, Arena.Hex("#F3EADB"));
        }

        // ------------------------------------------------------------------ brushed paint (R506)

        static readonly Material[] strokeMaterials = new Material[4];
        static Texture2D brushTexture, brushNormal;
        static byte[] brushTone;                                 // BrushMaps' streak tone, 256 x 256, for the live paint
        static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        const int Bands = 5;                                     // strokes across a square

        // The painting sheet, wet paint in the team's stone colour (user, 11-07: "실제로 렌더링 되게 해줘 칠해보이는 게"):
        // alpha-clipped by the brush order, lit through a normal map of bristle grooves and the ridges of paint at each
        // stroke's sides (BrushMaps), glossy as fresh paint, a little of the team's glow. Done, the square takes its stone.
        Material StrokeMaterial(int team)
        {
            int i = Slot(team) + (PalaceStyle ? 2 : 0);
            if (strokeMaterials[i] != null) return strokeMaterials[i];
            BrushMaps();
            var m = new Material(Arena.Lit(team == Teams.Black ? "M_BrushBlack" : "M_BrushWhite", Arena.Hex(team == Teams.Black ? "#2E2A26" : "#EEE3D0"), 0f, .2f));
            m.SetTexture("_BaseMap", brushTexture);
            m.mainTexture = brushTexture;
            m.SetTexture("_BumpMap", brushNormal);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Smoothness", .82f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat(CutoffId, .5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = 2450;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", TeamColor(team) * .08f);
            strokeMaterials[i] = m;
            return m;
        }

        // The brush order and the paint's relief, 256 px over the square:
        //   alpha   1 - the moment the brush passes (0..1): Bands strokes, the first along the sheet's bottom left to
        //           right, the next back right to left; the bands' borders wave and each bristle line runs a little ahead
        //           or behind (a ragged, dry-brush leading edge). With the clip at 1 - progress the paint shows exactly
        //           where the brush has been.
        //   colour  bristle streaks and a darker, thicker line at each stroke's sides
        //   normal  grooves along each stroke (the bristles), finer bristle scratches, a ridge of piled paint at each
        //           stroke's sides; packed for UnpackNormal on PC (x in alpha, y in green, red 1)
        static void BrushMaps()
        {
            if (brushTexture != null && brushNormal != null) return;
            const int n = 256;
            var height = new float[n * n];
            var px = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float u = (i + .5f) / n, v = (j + .5f) / n;
                    float bf = v * Bands + .22f * Mathf.Sin(u * 7f + 1.3f) + .1f * Mathf.Sin(u * 17f + 4f);
                    int b = Mathf.Clamp(Mathf.FloorToInt(bf), 0, Bands - 1);
                    float w = Mathf.Clamp01(bf - b);                                   // across the stroke, 0..1
                    float along = (b & 1) == 0 ? u : 1f - u;
                    along = Mathf.Clamp01(along + (Mathf.PerlinNoise(v * 70f, b * 3.1f + 7f) - .5f) * .22f);
                    float order = Mathf.Lerp(.004f, .996f, (b + along) / Bands);
                    float grooves = .5f + .5f * Mathf.Sin((w * 11f + (Mathf.PerlinNoise(u * 2.5f, b * 1.7f) - .5f) * 1.2f) * Mathf.PI * 2f);
                    float scratch = Mathf.PerlinNoise(u * 4f + b * 9f, w * 90f);
                    float side = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Min(w, 1f - w) / .12f);
                    height[j * n + i] = .38f * grooves + .22f * scratch + .55f * side;
                    float tone = .9f + .1f * scratch - .12f * side + .04f * (Mathf.PerlinNoise(u * 6f, v * 6f) - .5f);
                    byte c = (byte)(Mathf.Clamp01(tone) * 255f);
                    px[j * n + i] = new Color32(c, c, c, (byte)((1f - order) * 255f));
                }
            brushTone = new byte[n * n];
            for (int q = 0; q < n * n; q++) brushTone[q] = px[q].r;
            brushTexture = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "A brush strokes" };
            brushTexture.SetPixels32(px);
            brushTexture.Apply(true, true);
            var nm = new Color32[n * n];
            const float k = 5f;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float dx = height[j * n + Mathf.Min(n - 1, i + 1)] - height[j * n + Mathf.Max(0, i - 1)];
                    float dy = height[Mathf.Min(n - 1, j + 1) * n + i] - height[Mathf.Max(0, j - 1) * n + i];
                    var normal = new Vector3(-dx * k, -dy * k, 1f).normalized;
                    nm[j * n + i] = new Color32(255, (byte)((normal.y * .5f + .5f) * 255f), 0, (byte)((normal.x * .5f + .5f) * 255f));
                }
            brushNormal = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Clamp, name = "A brush relief" };
            brushNormal.SetPixels32(nm);
            brushNormal.Apply(true, true);
        }

        // ---- live paint (user, 11-07: "실제로 그려지는 모습 … 실시간으로 채워지게"): each charging square has its own
        // small texture the brush paints into as it goes, frame by frame. The brush's path is the zigzag of Bands
        // strokes, with a hand's wobble that differs on every new charge (pathSeed); each stamp is a line of bristles
        // across the stroke, every bristle carrying its own load of paint, so its trail shows as a streak, the edges
        // thinning out. Stamps lie a pixel apart from the last painted point to the brush's place now, so a jump in
        // progress (a client catching up) still leaves a continuous stroke. The sheet's clip stays at 0.5; when the
        // charge stops, the paint fades (clip to 1) as it falls back, and the next charge paints afresh.
        const int MaskSize = 128;
        readonly Texture2D[] masks = new Texture2D[64];
        readonly Color32[][] maskPx = new Color32[64][];
        readonly float[] paintedTo = new float[64], pathSeed = new float[64], stopShown = new float[64];
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        // The brush's place at `progress`, in the sheet's 0..1 space (x across, y up the bands).
        Vector2 PathUV(int s, float progress)
        {
            float f = Mathf.Clamp(progress, 0f, .9999f) * Bands, seed = pathSeed[s];
            int b = Mathf.FloorToInt(f);
            float along = f - b;
            float u = -.06f + 1.12f * ((b & 1) == 0 ? along : 1f - along);
            float v = (b + .5f) / Bands + .022f * Mathf.Sin(along * 7f + seed + b * 2.1f) + .01f * Mathf.Sin(along * 23f + seed * 3f);
            return new Vector2(u, v);
        }

        Vector3 TipLocal(int s, float progress)
        {
            var uv = PathUV(s, progress);
            return new Vector3((uv.x - .5f) * Tile, .02f, (uv.y - .5f) * Tile);
        }

        void StartPaint(int s)
        {
            pathSeed[s] = Random.Range(0f, 100f);
            paintedTo[s] = 0f;
            if (masks[s] == null)
            {
                masks[s] = new Texture2D(MaskSize, MaskSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "A live paint" };
                maskPx[s] = new Color32[MaskSize * MaskSize];
            }
            System.Array.Clear(maskPx[s], 0, maskPx[s].Length);
            masks[s].SetPixels32(maskPx[s]);
            masks[s].Apply(false);
        }

        // Paint the brush's trail from paintedTo to `progress`, a stamp per pixel of travel.
        void PaintTo(int s, float progress)
        {
            if (masks[s] == null || progress <= paintedTo[s]) return;
            var px = maskPx[s];

            float step = 1f / (Bands * MaskSize * 1.4f);
            for (float q = paintedTo[s]; q <= progress; q += step)
            {
                int band = Mathf.FloorToInt(Mathf.Clamp(q, 0f, .9999f) * Bands);
                var uv = PathUV(s, q);
                Stamp(px, uv, band, pathSeed[s]);

            }
            paintedTo[s] = progress;
            masks[s].SetPixels32(px);
            masks[s].Apply(false);
        }

        // A line of bristles across the stroke at uv: 30 bristles over 1.3 band heights, each with its own paint load
        // (steady along the stroke, so it leaves a streak), thinning to nothing at the line's ends.
        void Stamp(Color32[] px, Vector2 uv, int band, float seed)
        {
            float half = .65f / Bands * MaskSize;
            int cx = Mathf.RoundToInt(uv.x * MaskSize), cy = Mathf.RoundToInt(uv.y * MaskSize);
            int h = Mathf.CeilToInt(half);
            for (int dy = -h; dy <= h; dy++)
            {
                int y = cy + dy;
                if (y < 0 || y >= MaskSize) continue;
                float t = dy / half;
                if (Mathf.Abs(t) > 1f) continue;
                int bristle = Mathf.FloorToInt((t + 1f) * 15f);
                float load = .55f + .6f * Mathf.PerlinNoise(band * 7.3f + bristle * 1.37f, seed * .1f + band);
                float a = Mathf.Clamp01(load - Mathf.Pow(Mathf.Abs(t), 6f) * .9f);
                byte alpha = (byte)(a * 255f);
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = cx + dx;
                    if (x < 0 || x >= MaskSize) continue;
                    int i = y * MaskSize + x;
                    if (alpha <= px[i].a) continue;
                    byte tone = brushTone != null ? brushTone[(y * 2) * 256 + x * 2] : (byte)230;
                    px[i] = new Color32(tone, tone, tone, alpha);
                }
            }
        }

        MaterialPropertyBlock brushBlock;                        // made in Update: Unity refuses one in a MonoBehaviour's field initializer
        readonly Transform[] brushes = new Transform[64];

        // A painter's brush over the square while it is painted (user, 11-07: "실제로 … 칠해보이는 게"): oak handle, brass
        // ferrule, bristles dipped in the team's stone colour; leaning back against its stroke, its bristle tip on the
        // paint's leading edge (TipLocal), bobbing a little with each stroke. Picture only, under the plate.
        Transform Brush(int s)
        {
            if (brushes[s] != null) return brushes[s];
            int team = TeamOf(s);
            var root = new GameObject("Painting brush").transform;
            root.SetParent(plates[s], false);
            var tilt = new GameObject("Brush tilt").transform;          // pivot at the bristle tip
            tilt.SetParent(root, false);
            var oak = Arena.Lit("M_BrushHandle", Arena.Hex("#8A5A2E"), 0f, .55f);
            var brass = Arena.Lit("M_BrushFerrule", Arena.Hex("#9C8354"), 0f, .3f);
            var hair = Arena.Lit("M_BrushHair", Arena.Hex("#5A4030"), 0f, .8f);
            var dip = Arena.Lit(team == Teams.Black ? "M_BrushDipBlack" : "M_BrushDipWhite", Arena.Hex(team == Teams.Black ? "#2E2A26" : "#EEE3D0"), 0f, .2f);
            void Part(PrimitiveType type, string name, Vector3 at, Vector3 scale, Material m)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name;
                if (Application.isPlaying) Destroy(go.GetComponent<Collider>()); else DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(tilt, false);
                go.transform.localPosition = at;
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = m;
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            Part(PrimitiveType.Cube, "Brush paint tip", new Vector3(0f, .06f, 0f), new Vector3(.36f, .12f, .09f), dip);
            Part(PrimitiveType.Cube, "Brush bristles", new Vector3(0f, .2f, 0f), new Vector3(.38f, .2f, .1f), hair);
            Part(PrimitiveType.Cube, "Brush ferrule", new Vector3(0f, .37f, 0f), new Vector3(.4f, .16f, .13f), brass);
            Part(PrimitiveType.Cylinder, "Brush handle", new Vector3(0f, .85f, 0f), new Vector3(.09f, .4f, .09f), oak);
            Part(PrimitiveType.Sphere, "Brush handle end", new Vector3(0f, 1.25f, 0f), new Vector3(.11f, .11f, .11f), oak);
            brushes[s] = root;
            return root;
        }

        void PlaceBrush(int s, float progress, bool charging, float t)
        {
            if (!charging)
            {
                if (brushes[s] != null && brushes[s].gameObject.activeSelf) brushes[s].gameObject.SetActive(false);
                return;
            }
            var brush = Brush(s);
            if (!brush.gameObject.activeSelf) brush.gameObject.SetActive(true);
            float f = Mathf.Clamp(progress, 0f, .999f) * Bands;
            int band = Mathf.FloorToInt(f);
            float dir = (band & 1) == 0 ? 1f : -1f;                       // travelling +x on even bands
            brush.localPosition = TipLocal(s, progress) + Vector3.up * (.015f + .02f * Mathf.Abs(Mathf.Sin(t * 9f)));
            // the bristles' wide side across the stroke; the handle leaning back against the travel
            brush.localRotation = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(-dir * 35f, 0f, 0f);
        }

        void Cut(int s)
        {
            ArenaFx.Cut(chargeVoice[s]);
            chargeVoice[s] = null;
        }

        void Update()
        {
            if (squares == null) return;
            if (brushBlock == null) brushBlock = new MaterialPropertyBlock();
            float t = Time.time;
            for (int s = 0; s < 64; s++)
            {
                var sq = squares[s];
                if (sq == null) continue;
                float y = 0f;
                if (hopHeight[s] > 0f && t >= hopAt[s])
                {
                    float k = (t - hopAt[s]) / .35f;
                    if (k >= 1f) hopHeight[s] = 0f;
                    else y = hopHeight[s] * Mathf.Sin(k * Mathf.PI);
                }
                sq.localPosition = new Vector3(sq.localPosition.x, y, sq.localPosition.z);
                if (painted[s]) continue;

                bool charging = target[s] > 0f;
                var at = transform.TransformPoint(sq.localPosition + Vector3.up * TileHeight);
                if (!charging && wasCharging[s]) { Cut(s); stopShown[s] = Mathf.Max(.001f, shown[s]); }
                if (charging && !wasCharging[s] && strokes[s] != null) StartPaint(s);
                wasCharging[s] = charging;
                wasBlocked[s] = charging && blocked[s];       // A has no sounds for now (user, 11-07: "사운드는 다 없애고")

                // The brushing follows the host's time (eased on a client), and wipes back in 0.2 s when it stops.
                float want = target[s] * PlateMax;
                shown[s] = charging
                    ? (MiniGameBase.Remote ? Mathf.Lerp(shown[s], want, 1f - Mathf.Exp(-18f * Time.deltaTime)) : want)
                    : Mathf.MoveTowards(shown[s], 0f, Time.deltaTime * PlateMax / .2f);
                var plate = plates[s];
                bool show = shown[s] > .002f;
                if (plate.gameObject.activeSelf != show) plate.gameObject.SetActive(show);
                if (!show) continue;
                if (!charging) PlaceBrush(s, 0f, false, t);
                float progress = shown[s] / PlateMax;
                if (strokes[s] != null && masks[s] != null)
                {
                    if (charging) PaintTo(s, progress);
                    strokes[s].GetPropertyBlock(brushBlock);
                    brushBlock.SetTexture(BaseMapId, masks[s]);
                    // painted: the clip at 0.5; stopped: the paint fades out as the charge falls back
                    brushBlock.SetFloat(CutoffId, charging ? .5f : Mathf.Lerp(1f, .5f, shown[s] / stopShown[s]));
                    strokes[s].SetPropertyBlock(brushBlock);
                }
                else plate.localScale = new Vector3(shown[s] * Tile, 1f, shown[s] * Tile);   // a board built before R506's brush
                bool flash = charging && blocked[s] && Mathf.Repeat(t / .3f, 1f) < .5f;
                var rimWant = Arena.Glow(flash ? Arena.Red : TeamColor(TeamOf(s)));
                if (rims[s].sharedMaterial != rimWant)
                    foreach (var edge in rims[s].transform.parent.GetComponentsInChildren<Renderer>()) edge.sharedMaterial = rimWant;
                PlaceBrush(s, progress, charging, t);
                // R506: soft glowing motes at the brush's tip while it paints (red while blocked).
                if (charging && t >= nextSpark[s])
                {
                    nextSpark[s] = t + .07f;
                    var tip = plate.TransformPoint(TipLocal(s, progress)) + Random.insideUnitSphere * .12f;
                    Fx.Mote(tip, blocked[s] ? Arena.Red : TeamColor(TeamOf(s)));
                }
            }
            // A finished rank: a gold light runs along it, left to right, in 0.5 s.
            if (sweepAt >= 0f)
            {
                float k = (t - sweepAt) / .5f;
                if (k > 1f) sweepAt = -1f;
                else
                {
                    float half = PawnRushBoard.N * PawnRushBoard.Cell * .5f;
                    var at = transform.TransformPoint(new Vector3(Mathf.Lerp(-half, half, k), TileHeight + .05f, PawnRushBoard.Center(0, sweepRank).U));
                    Fx.RankDust(at, Arena.Gold);                    // R506: gold dust and twinkles (PaintBoardFx)
                }
            }
        }

        // ------------------------------------------------------------------ the wire (§3.6, one team's half)

        public void Write(int team, ref NetBlock block)
        {
            ulong bits = 0;
            for (int s = 0; s < 64; s++) if (painted[s] && TeamOf(s) == team) bits |= 1UL << s;
            block.U64(bits);
            var list = charging[Slot(team)];
            for (int k = 0; k < PawnRushMissions.WorkerCap; k++)
            {
                if (k >= list.Count) { block.U16(0); continue; }
                var c = list[k];
                int steps = Mathf.Clamp(Mathf.FloorToInt(c.Time / Hold * 16f), 0, 15);
                block.U16(1 << 15 | (c.Blocked ? 1 << 10 : 0) | steps << 6 | c.Square & 63);
            }
        }

        public void Read(int team, ref NetBlock block)
        {
            ulong bits = block.ReadU64();
            bool moments = heard[Slot(team)];
            heard[Slot(team)] = true;
            for (int s = 0; s < 64; s++)
            {
                if (TeamOf(s) != team) continue;
                target[s] = 0f;
                blocked[s] = false;
                bool on = (bits >> s & 1UL) != 0;
                if (on && !painted[s]) Paint(s, moments);
                else if (!on && painted[s]) Unpaint(s);
            }
            for (int k = 0; k < PawnRushMissions.WorkerCap; k++)
            {
                int v = block.ReadU16();
                if ((v & 1 << 15) == 0) continue;
                int s = v & 63;
                if (painted[s]) continue;
                target[s] = Mathf.Max(.001f, ((v >> 6 & 15) + .5f) / 16f);
                blocked[s] = (v & 1 << 10) != 0;
            }
        }

        void Unpaint(int s)
        {
            painted[s] = false;
            count[Slot(TeamOf(s))]--;
            tiles[s].sharedMaterial = Arena.Lit("M_BoardSquare", Arena.Slate, 0f, .45f);
            emblems[s].SetActive(false);
            int rank = PawnRushBoard.RankOf(s);
            if (rankDone[rank])
            {
                rankDone[rank] = false;
                var dim = Arena.Gold * .4f;
                dim.a = 1f;
                rankText[rank - 1].color = dim;
                rankText[7 + rank].color = dim;
            }
        }

        // A new round for one team: its squares back to slate, its charges gone.
        public void ResetTeam(int team)
        {
            charging[Slot(team)].Clear();
            heard[Slot(team)] = false;
            for (int s = 0; s < 64; s++)
            {
                if (TeamOf(s) != team) continue;
                if (painted[s]) Unpaint(s);
                target[s] = 0f;
                blocked[s] = false;
            }
        }
    }
}
