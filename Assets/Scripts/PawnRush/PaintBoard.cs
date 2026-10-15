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
        static void ResetStatics() => boards.Clear();

        [SerializeField] int plaza;
        [SerializeField] MissionStation white, black;
        [SerializeField] Transform[] squares = new Transform[64];
        [SerializeField] Renderer[] tiles = new Renderer[64];
        [SerializeField] Transform[] plates = new Transform[64];
        [SerializeField] Renderer[] rims = new Renderer[64];
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
            boards.RemoveAll(b => b == null);
            foreach (var b in boards)
                if (b.plaza == station.Plaza)
                {
                    b.Link(station);
                    return b;
                }
            var go = new GameObject("Shared board");
            go.transform.SetParent(under, false);
            var board = go.AddComponent<PaintBoard>();
            board.plaza = station.Plaza;
            board.Link(station);
            foreach (var other in Object.FindObjectsByType<MissionStation>(FindObjectsSortMode.None))
                if (other != station && other.Plaza == station.Plaza && other.Team != station.Team) board.Link(other);
            board.Make();
            boards.Add(board);
            return board;
        }

        void Awake()
        {
            if (!boards.Contains(this)) boards.Add(this);
        }

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
                    var dais = b.Box("Aisle dais", new Vector3(-aisle, -Arena.DaisHeight, 0f), new Vector3(aisle, 0f, MissionStation.Size), Arena.DaisMaterial);
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

            var slate = Arena.Lit("M_BoardSquare", Arena.Slate, 0f, .45f);
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
                // The colour dots in the south-west and north-east corners: the board's colours before any paint.
                var dot = TeamOf(s) == Teams.White ? lightDot : darkDot;
                MiniGameParts.Part(sq, PrimitiveType.Cylinder, new Vector3(-.8f, TileHeight + .01f, -.8f), new Vector3(.26f, .005f, .26f), dot);
                MiniGameParts.Part(sq, PrimitiveType.Cylinder, new Vector3(.8f, TileHeight + .01f, .8f), new Vector3(.26f, .005f, .26f), dot);
                int team = TeamOf(s);
                // The charging plate: 1 x 1 m scaled to its size, its glowing rim just wider underneath.
                var plate = new GameObject("Plate");
                plate.transform.SetParent(sq, false);
                plate.transform.localPosition = new Vector3(0f, TileHeight + .008f, 0f);
                rims[s] = Arena.Picture(plate.transform, "Plate rim", new Vector3(-.53f, 0f, -.53f), new Vector3(.53f, .006f, .53f), Arena.Glow(Arena.TeamColor(team))).GetComponent<Renderer>();
                Arena.Picture(plate.transform, "Plate paint", new Vector3(-.5f, .004f, -.5f), new Vector3(.5f, .014f, .5f), PaintMaterial(team));
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

        static readonly Material[] paint = new Material[2];

        // §3.4: white's paint is cream glowing sky blue, black's is near-black glowing orange.
        static Material PaintMaterial(int team)
        {
            int i = Slot(team);
            if (paint[i] != null) return paint[i];
            var m = new Material(Arena.Lit(team == Teams.Black ? "M_PaintBlack" : "M_PaintWhite", Arena.Hex(team == Teams.Black ? "#231E1B" : "#F3EADB"), 0f, .5f));
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Arena.TeamColor(team) * .25f);
            paint[i] = m;
            return m;
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
            var color = Arena.TeamColor(team);
            ArenaFx.Ring(at, 0f, 1.9f, .45f, color);
            ArenaFx.Burst(at, new[] { color, Color.white }, 10, 1.6f, 2f, .7f, .08f);
            ArenaFx.Sfx("sfx_paint_done", at);
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
                    ArenaFx.Sfx("sfx_rank_complete", transform.TransformPoint(new Vector3(0f, .2f, PawnRushBoard.Center(0, rank).U)));
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
        }

        void Cut(int s)
        {
            ArenaFx.Cut(chargeVoice[s]);
            chargeVoice[s] = null;
        }

        void Update()
        {
            if (squares == null) return;
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
                if (charging && !wasCharging[s])
                {
                    // White's note high, black's a fourth lower (§3.4).
                    chargeVoice[s] = ArenaFx.Hold("sfx_paint_charge", at, TeamOf(s) == Teams.Black ? .749f : 1f, .7f);
                }
                if (!charging && wasCharging[s]) Cut(s);
                wasCharging[s] = charging;
                if (charging && blocked[s] && !wasBlocked[s]) ArenaFx.Sfx("sfx_paint_blocked", at, volume: .6f);
                wasBlocked[s] = charging && blocked[s];

                // The plate grows with the host's time (eased on a client), and shrinks away in 0.2 s.
                float want = target[s] * PlateMax;
                shown[s] = charging
                    ? (MiniGameBase.Remote ? Mathf.Lerp(shown[s], want, 1f - Mathf.Exp(-18f * Time.deltaTime)) : want)
                    : Mathf.MoveTowards(shown[s], 0f, Time.deltaTime * PlateMax / .2f);
                var plate = plates[s];
                bool show = shown[s] > .002f;
                if (plate.gameObject.activeSelf != show) plate.gameObject.SetActive(show);
                if (!show) continue;
                plate.localScale = new Vector3(shown[s] * Tile, 1f, shown[s] * Tile);
                bool flash = charging && blocked[s] && Mathf.Repeat(t / .3f, 1f) < .5f;
                var rimWant = Arena.Glow(flash ? Arena.Red : Arena.TeamColor(TeamOf(s)));
                if (rims[s].sharedMaterial != rimWant) rims[s].sharedMaterial = rimWant;
            }
            // A finished rank: a gold light runs along it, left to right, in 0.5 s.
            if (sweepAt >= 0f)
            {
                float k = (t - sweepAt) / .5f;
                if (k > 1f) sweepAt = -1f;
                else if (Random.value < .6f)
                {
                    float half = PawnRushBoard.N * PawnRushBoard.Cell * .5f;
                    var at = transform.TransformPoint(new Vector3(Mathf.Lerp(-half, half, k), TileHeight + .05f, PawnRushBoard.Center(0, sweepRank).U));
                    ArenaFx.Burst(at, new[] { Arena.Gold }, 2, .3f, 1.2f, .5f, .08f);
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
