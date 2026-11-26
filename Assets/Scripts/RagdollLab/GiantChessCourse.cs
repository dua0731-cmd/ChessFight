using System;
using System.Collections.Generic;
using System.Globalization;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // The giant chess room (pr/2): two giant grandmasters replay a candidates' game on an 8 x 8 board of 10 m squares; the
    // square a piece lands on throws pawns off, then the squares it attacks light up and strike.
    //
    // seoungtest R689 (승규 11-18 "자이언트 체스 코스 온라인 동기화"): the replay is a pure function of the seconds since the
    // chess started (Core GiantChessReplay: which game of the library, which move, which phase), so it runs two ways:
    //   its own scene     (GiantChessCourse.unity, removed from main on 2026-11-26) the offline test as before: twelve pawns of its own, bots, its camera,
    //                     a pausable clock that starts when the first pawn reaches the room, F3 a new random series
    //   a round section   (a RoyalLibraryMatch above it: Late_3-1_RoyalLibrary in a Pawn Rush round) the round's pawns, no
    //                     camera, input or gravity of its own; the game series comes from the section's seed and the
    //                     clock from the host's chess start (RoyalLibraryMatch.ChessStart, sent in the course state), so
    //                     every PC of a match, a late joiner and a new host draw the same move at the same moment
    // When a game ends the next one follows (승규 "끝나면 다음 대국"). The board throws and strikes only where the bodies are
    // simulated (ObstacleClock.Authority: offline, or the match's host), on whatever stands there (ImportedObstacleActor),
    // and never while it catches up on moves it missed (a late joiner, a new host). The giants are each PC's own drawing
    // of the same move.
    // StateDrivenMover: the offline scene's clock pauses with its menu; in a round it is the shared clock (see above).
    public sealed class GiantChessCourse : MonoBehaviour
    {
        public RagdollPawn pawnPrefab;
        public RagdollTuning tuning;
        [SerializeField, Min(10)] float tileSize = 18;
        [SerializeField, Range(.25f, 16f), Tooltip("Replay speed only; does not speed up players or physics.")] float replaySpeed = 8;
        [SerializeField, Min(1)] float moveSeconds = 3;
        [SerializeField, Min(1)] float warningSeconds = 5;
        [SerializeField, Min(.5f)] float restSeconds = 2;
        [SerializeField, Min(1)] float knockbackSpeed = 24;
        [SerializeField, Min(0)] float upwardSpeed = 9;
        [SerializeField] bool botsEnabled = true;
        [SerializeField] GameObject boardPreview;
        [SerializeField, Min(2)] float chessSpawnDepth = 10;
        [SerializeField, Min(1), Tooltip("Real seconds between a game's last move and the next game's first (the board is set up again).")]
        float endSeconds = 7;
        [SerializeField, Min(.5f), Tooltip("Real seconds the squares the new game's pieces will stand on warn before they appear.")]
        float setupWarningSeconds = 3;
        const float StrikeSeconds = .5f;   // replay seconds the shock wave lasts
        readonly List<Material> materials = new List<Material>();
        readonly List<RagdollPawn> players = new List<RagdollPawn>();
        readonly GameObject[] pieces = new GameObject[64], warnings = new GameObject[64];
        readonly GameObject[] occupiedMarks = new GameObject[64];
        readonly bool[] finished = new bool[12];
        readonly Vector3[] botGoals = new Vector3[12];
        readonly float[] nextShove = new float[12];
        readonly Transform[] hands = new Transform[2], arms = new Transform[2];
        readonly Vector3[] shoulders = new Vector3[2];
        readonly GiantGrandmaster[] giants = new GiantGrandmaster[2];
        // The move in hand: carried = the giant holds the piece; thrown = released mid-move toward a square out of reach.
        bool carryFrom, carryTo, released;
        Vector3 releasePoint;
        Material white, black, gold, red;
        LabCamera cameraRig;
        GiantChessApproach approach;
        string[][] score;
        GiantChessGames library;
        public GiantChessGame CurrentGame { get; private set; }
        // phase = GiantChessReplay's (Rest, Move, Warn, Strike, End); elapsed = replay seconds into it.
        int selected, ply, phase, from, to, rookFrom, rookTo;
        float elapsed;
        ulong attack, shownWarnings = ulong.MaxValue;
        bool running, menu = true, initialized;
        // The series: its seed, the turn (cycle, -1 = nothing set up yet) and the game of the library it is on.
        RoyalLibraryMatch match;
        int seed = 1, cycle = -1, gameIndex = -1, loadedSeed;
        double localReplay;                       // the offline scene's own replay seconds (its menu pauses it)
        readonly Dictionary<int, string[][]> parsed = new Dictionary<int, string[][]>();
        readonly Collider[] boardHits = new Collider[512];
        readonly List<ImportedObstacleActor> boardActors = new List<ImportedObstacleActor>();
        bool InRound => match != null;
        float Speed => Mathf.Clamp(replaySpeed, .25f, 16f);
        GiantChessReplay.Timing Timing => new GiantChessReplay.Timing(restSeconds, moveSeconds, warningSeconds, StrikeSeconds, endSeconds * Speed);
        int GameCount => library != null && library.games != null ? library.games.Length : 0;
        int Plies(int game) => game >= 0 && game < GameCount && library.games[game].rows != null ? library.games[game].rows.Length : 0;
        // The chess clock runs: offline once started and while the menu is closed; in a round once the host started it.
        bool Playing => InRound ? match.ChessStarted : running && !menu && Application.isFocused && (approach == null || !approach.Available || approach.ChessStarted);
        bool Moving => phase == GiantChessReplay.Move;
        const string Setup = "RNBQKBNRPPPPPPPP................................pppppppprnbqkbnr";
        GameObject moving;
        Vector3 savedGravity;
        CursorLockMode savedLock;
        bool savedCursor;
        GUIStyle hud;
        // Rotate chess coordinates 90 degrees: white home rank faces the left GM.
        Vector3 SquareLocal(int square) => new Vector3((square / 8 - 3.5f) * tileSize, 0, (7 - square % 8 + .5f) * tileSize);
        Vector3 Square(int square) => transform.TransformPoint(SquareLocal(square));
        int BoardSquare(Vector3 p)
        {
            p = transform.InverseTransformPoint(p);
            int rank = Mathf.FloorToInt(p.x / tileSize + 4), row = Mathf.FloorToInt(p.z / tileSize);
            return rank >= 0 && rank < 8 && row >= 0 && row < 8 ? rank * 8 + 7 - row : -1;
        }
        bool Occupied(int s) => pieces[s] && !(Moving && pieces[s] == moving);
        void RefreshOccupiedMarks()
        {
            for (int s = 0; s < 64; s++) occupiedMarks[s].SetActive(Occupied(s));
        }
        Vector3 Spawn(int i) => approach != null && approach.Available && !approach.Entered(i) ? approach.Spawn(i) : transform.TransformPoint(new Vector3((i / 6 == 0 ? -1 : 1) * tileSize + (i % 3 - 1) * 2.5f, 0, -chessSpawnDepth - i % 2 * 3));
        Material Mat(Color color) { var m = LitMaterials.NewLit(color); materials.Add(m); return m; }
        // R687 R6 (rl_r6_chess_model.py): the Blender board, marks, decks, arch and pieces, drawn in the Royal Library
        // materials. Every collider stays the code's box; a model only replaces what is drawn. Models are authored at a
        // 10 m square and scaled by tileSize / 10 across the board.
        const string ChessModels = "RoyalLibrary/Models/Chess/";
        static readonly Dictionary<string, string> LibrarySlots = new Dictionary<string, string>
        {
            { "stone", "RL_CreamStone" }, { "walnut", "RL_Walnut" }, { "varnished", "RL_VarnishedWalnut" }, { "brass", "RL_Brass" },
            { "marble", "RL_Marble" }, { "parchment", "RL_Parchment" }, { "ivory", "RL_Ivory" }, { "ebony", "RL_Ebony" },
        };
        readonly Dictionary<string, Material> libraryMaterials = new Dictionary<string, Material>();
        Material redRim;
        float ModelScale => tileSize / 10f;
        Material Library(string slot)
        {
            if (!LibrarySlots.TryGetValue(slot, out var asset)) return null;
            if (!libraryMaterials.TryGetValue(asset, out var m)) libraryMaterials[asset] = m = Resources.Load<Material>("RoyalLibrary/Materials/" + asset);
            return m;
        }
        // "RL _ ivory" (Unity's import of "RL | ivory") -> "ivory"
        static string SlotKey(Material m)
        {
            if (!m) return "";
            string n = m.name.Replace(" (Instance)", "").Trim();
            return n.StartsWith("RL", StringComparison.Ordinal) ? n.Substring(2).TrimStart(' ', '_', '|').Trim().ToLowerInvariant() : "";
        }
        // A model under `parent` at localPos, turned by yaw; paint(renderer name, slot) may pick a material (null: the library's).
        // The FBX needs a half turn about y to land Blender x, y, z on Unity x, z, y (RoyalLibraryDressing's pivot).
        GameObject Model(string name, Transform parent, Vector3 localPos, Vector3 scale, float yaw = 0, Func<string, string, Material> paint = null)
        {
            var fbx = Resources.Load<GameObject>(ChessModels + name);
            if (!fbx) return null;
            var pivot = new GameObject(name + " (model)").transform; pivot.SetParent(parent, false);
            pivot.localPosition = localPos; pivot.localRotation = Quaternion.Euler(0, 180 + yaw, 0); pivot.localScale = scale;
            var model = Instantiate(fbx, pivot, false); model.name = name;
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string key = SlotKey(slots[i]);
                    var m = (paint != null ? paint(r.name, key) : null) ?? Library(key);
                    if (m) slots[i] = m;
                }
                r.sharedMaterials = slots;
            }
            return pivot.gameObject;
        }
        static void Hide(GameObject go) { if (go && go.TryGetComponent<Renderer>(out var r)) r.enabled = false; }
        GameObject Shape(string name, Vector3 pos, Vector3 size, Material mat, PrimitiveType type = PrimitiveType.Cube, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(transform, false);
            go.transform.localPosition = pos; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!solid) { var c = go.GetComponent<Collider>(); c.enabled = false; Destroy(c); }
            return go;
        }
        void Awake()
        {
            match = GetComponentInParent<RoyalLibraryMatch>();
            approach = GetComponent<GiantChessApproach>();
            books = GetComponentsInChildren<ChessFight.ProtectKing.ClosingBook>();
            if (approach != null) approach.ResetProgress();
            var data = Resources.Load<TextAsset>("GiantChessGames");
            if (InRound)
            {
                // A round section: the round owns the pawns, the camera, the input and gravity; the section the books.
                if (!data) { Debug.LogError("Giant chess requires its score (Resources/GiantChessGames).", this); enabled = false; return; }
            }
            else
            {
                cameraRig = FindFirstObjectByType<LabCamera>();
                foreach (var book in books) book.Crushed += BookCrush;
                if (!pawnPrefab || !tuning || !cameraRig || !data) { Debug.LogError("Giant chess requires pawn, tuning, camera and score.", this); enabled = false; return; }
            }
            library = JsonUtility.FromJson<GiantChessGames>(data.text);
            if (GameCount == 0) { Debug.LogError("Giant chess: the GM replay library is empty.", this); enabled = false; return; }
            seed = InRound ? match.Seed : UnityEngine.Random.Range(1, int.MaxValue);
            LoadGame(0);
            if (boardPreview != null) boardPreview.SetActive(false);
            savedGravity = Physics.gravity; savedLock = Cursor.lockState; savedCursor = Cursor.visible; initialized = true;
            if (!InRound) Physics.gravity = Vector3.down * 9.81f * tuning.values.gravityScale;
            white = Mat(new Color(.88f, .85f, .74f)); black = Mat(new Color(.10f, .13f, .18f)); gold = Mat(new Color(.7f, .46f, .16f));
            red = Mat(new Color(.9f, .08f, .025f)); red.EnableKeyword("_EMISSION");
            redRim = Mat(new Color(1f, .18f, .06f)); redRim.EnableKeyword("_EMISSION");
            // R6 models: the board's squares, frame and brass corners; the code's 64 boxes stay as the walking colliders
            float k = ModelScale;
            var board = Model("RlChessBoard", transform, Vector3.zero, new Vector3(k, 1, k));
            for (int s = 0; s < 64; s++)
            {
                var square = Shape("Board " + (char)('a' + s % 8) + (s / 8 + 1), SquareLocal(s) - Vector3.up * .4f,
                    new Vector3(tileSize, .8f, tileSize), (s % 8 + s / 8) % 2 == 0 ? white : black);
                if (board) Hide(square);
                // the attack warning: the same blinking red plate (slot wax = red, its rim = the brighter red)
                warnings[s] = Model("RlChessWarning", transform, SquareLocal(s), new Vector3(k, 1, k), 0, (r, key) => key == "brass" ? redRim : red);
                if (!warnings[s]) warnings[s] = Shape("Attack warning", SquareLocal(s) + Vector3.up * .025f, new Vector3(tileSize - .3f, .04f, tileSize - .3f), red, solid: false);
                warnings[s].SetActive(false);
                var mark = Model("RlChessOccupied", transform, SquareLocal(s), new Vector3(k, 1, k));
                if (!mark)
                {
                    mark = new GameObject("Occupied square border"); mark.transform.SetParent(transform, false);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var edge = Shape("Blocked square edge", SquareLocal(s) + new Vector3(side * (tileSize * .5f - .2f), .09f, 0), new Vector3(.35f, .12f, tileSize), gold, solid: false);
                        edge.transform.SetParent(mark.transform, true);
                        edge = Shape("Blocked square edge", SquareLocal(s) + new Vector3(0, .09f, side * (tileSize * .5f - .2f)), new Vector3(tileSize, .12f, .35f), gold, solid: false);
                        edge.transform.SetParent(mark.transform, true);
                    }
                }
                occupiedMarks[s] = mark;
            }
            float entryDepth = approach != null && approach.Available ? 8 : 20;
            var entry = Shape("Entry platform", new Vector3(0, -.4f, -entryDepth * .5f), new Vector3(tileSize * 8, .8f, entryDepth), gold);
            if (Model(entryDepth < 12 ? "RlChessDeck8" : "RlChessDeck20", transform, Vector3.zero, new Vector3(k, 1, entryDepth < 12 ? entryDepth / 8 : entryDepth / 20))) Hide(entry);
            var exit = Shape("Exit platform", new Vector3(0, -.4f, tileSize * 8 + 10), new Vector3(tileSize * 8, .8f, 20), gold);
            if (Model("RlChessDeck20", transform, new Vector3(0, 0, tileSize * 8), new Vector3(k, 1, 1), 180)) Hide(exit);
            var stripe = Shape("Finish stripe", new Vector3(0, .03f, tileSize * 8 + 3), new Vector3(tileSize * 8, .04f, 1), white, solid: false);
            if (Model("RlChessFinish", transform, new Vector3(0, 0, tileSize * 8 + 3), new Vector3(k, 1, 1))) Hide(stripe);
            var archParts = new List<GameObject>();
            for (int side = -1; side <= 1; side += 2)
                archParts.Add(Shape("Exit arch pillar", new Vector3(side * 12, 5, tileSize * 8 + 12), new Vector3(2, 10, 2), black));
            archParts.Add(Shape("Exit arch header", new Vector3(0, 10, tileSize * 8 + 12), new Vector3(26, 2, 2), gold));
            if (Model("RlChessArch", transform, new Vector3(0, 0, tileSize * 8 + 12), Vector3.one)) foreach (var part in archParts) Hide(part);
            var sign = new GameObject("Exit sign"); sign.transform.SetParent(transform, false);
            sign.transform.localPosition = new Vector3(0, 10, tileSize * 8 + 10.9f);
            var label = sign.AddComponent<TextMesh>(); label.text = "다음 구간 · EXIT"; label.fontSize = 48; label.characterSize = .18f;
            label.anchor = TextAnchor.MiddleCenter; label.font = RuntimePanels.KoreanFont;
            if (label.font) sign.GetComponent<Renderer>().sharedMaterial = label.font.material;
            for (int side = 0; side < 2; side++) BuildGiant(side);
            var warRoom = GetComponent<GiantChessWarRoom>();
            if (warRoom != null && warRoom.enabled) warRoom.Build(tileSize);
            ResetBoard(false);
            cycle = 0; loadedSeed = seed;
        }
        void Start()
        {
            if (!initialized || InRound) return;
            for (int i = 0; i < 12; i++)
            {
                var pawn = Instantiate(pawnPrefab, Spawn(i) + Vector3.up * 2, Quaternion.identity, transform);
                pawn.tuning = tuning; pawn.Team = i / 6; pawn.DisplayName = (i < 6 ? "백팀 " : "흑팀 ") + (i % 6 + 1);
                if (pawn.skin) pawn.skin.sharedMaterial = i < 6 ? white : black;
                players.Add(pawn);
            }
            ResetPlayers(); Select(0); CursorState();
        }
        void BuildGiant(int side)
        {
            // The Blender-built, rigged grandmasters (R687 R6): side 0 sits at -x and plays white (the cream giant B),
            // side 1 at +x plays black (the charcoal giant A). Seat top 3 m up, 24 m off the board's edge.
            var cloth = Resources.Load<Material>("RoyalLibrary/Materials/RL_CreamCloth");
            var brass = Resources.Load<Material>("RoyalLibrary/Materials/RL_Brass");
            if (cloth)
            {
                // The face: skin with the soft blush painted in (RlGiantFace.png, the head's longitude / latitude UVs).
                var faceTexture = Resources.Load<Texture2D>("RoyalLibrary/Models/ChessRoom/RlGiantFace");
                var skin = Mat(new Color(.94f, .81f, .66f));
                var face = skin;
                if (faceTexture) { face = Mat(Color.white); face.SetTexture("_BaseMap", faceTexture); face.mainTexture = faceTexture; }
                Material robe = side == 0 ? cloth : Tinted(cloth, new Color(.36f, .38f, .5f)), inside = Tinted(robe, side == 0 ? new Color(.55f, .5f, .45f) : new Color(.4f, .4f, .4f));
                giants[side] = GiantGrandmaster.Load(side == 0 ? "RlGiant_B" : "RlGiant_A", transform, new Vector3((side == 0 ? -1 : 1) * (tileSize * 4 + 24), 3, tileSize * 4),
                    new Vector3(0, 3, tileSize * 4), robe, inside, brass ? brass : gold, skin, Mat(new Color(.2f, .12f, .09f)), Mat(new Color(.97f, .67f, .62f)), face);
                if (giants[side] != null) return;
            }
            // Fallback when the model is missing: the primitive stand-in with a stretching arm.
            float x = (side == 0 ? -1 : 1) * (tileSize * 4 + 22), z = tileSize * 4;
            var suit = side == 0 ? black : white;
            Shape("GM seated torso", new Vector3(x, 16, z), new Vector3(18, 26, 22), suit, PrimitiveType.Capsule, false);
            Shape("GM head", new Vector3(x, 44, z), new Vector3(14, 17, 14), white, PrimitiveType.Sphere, false);
            for (int eye = -1; eye <= 1; eye += 2)
                Shape("GM eye", new Vector3(x + (side == 0 ? 6 : -6), 46, z + eye * 3), Vector3.one * 2, black, PrimitiveType.Sphere, false);
            var shoulder = new Vector3(x, 30, z - 7);
            shoulders[side] = transform.TransformPoint(shoulder);
            arms[side] = Shape("GM reaching arm", shoulder, Vector3.one, suit, solid: false).transform;
            hands[side] = Shape("GM moving hand", shoulder, new Vector3(7, 3, 7), white, PrimitiveType.Sphere, false).transform;
        }
        Material Tinted(Material source, Color tint)
        {
            var m = new Material(source); m.name = source.name + " tinted"; materials.Add(m);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * tint);
            return m;
        }
        // The top of a piece (its renderers' highest point, centred on the piece) and how far above the piece's root it is.
        Vector3 PieceTop(GameObject piece, out float height)
        {
            var rs = piece.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { height = 15; return piece.transform.position + Vector3.up * height; }
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            height = b.max.y - piece.transform.position.y;
            return new Vector3(piece.transform.position.x, b.max.y, piece.transform.position.z);
        }
        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        // The giant whose move it is reaches, grips, carries or throws; the other one rests. Runs after the move logic.
        void DriveGiant(int side, float dt, float dtReal)
        {
            var giant = giants[side];
            bool mine = Playing && ply < score.Length && ply % 2 == side;
            Vector3 boardCentre = transform.TransformPoint(new Vector3(0, 0, tileSize * 4));
            Vector3 aim = giant.RestGrip(boardCentre); float weight = 0, grip = 0;
            float pieceHeight = 15;
            if (mine && phase == GiantChessReplay.Rest)
            {
                int next = int.Parse(score[ply][0]);
                if (pieces[next])
                {
                    Vector3 top = PieceTop(pieces[next], out pieceHeight);
                    float u = elapsed / restSeconds;
                    bool reach = giant.Reachable(top);
                    if (u > .2f) { aim = reach ? top + Vector3.up * Mathf.Lerp(6, 0, Ease((u - .2f) / .6f)) : giant.Toward(top + Vector3.up * 10); weight = 1; grip = reach ? Ease((u - .75f) / .25f) : .2f; }
                }
            }
            else if (mine && Moving && moving)
            {
                float t = Mathf.Clamp01(elapsed / moveSeconds);
                Vector3 a = Square(from), b = Square(to);
                PieceTop(moving, out pieceHeight);
                Vector3 up = Vector3.up * pieceHeight;
                weight = 1;
                if (carryFrom && carryTo) { float e = Ease(t / .85f); aim = Vector3.Lerp(a, b, e) + up + Vector3.up * Mathf.Sin(e * Mathf.PI) * 14; grip = 1; }
                else if (carryFrom) // throw: lift and swing back a little, then fling toward the square, follow through
                {
                    if (t < .3f) { float e = Ease(t / .3f); aim = a + up + Vector3.up * 22 * e + Vector3.ProjectOnPlane(giant.root.position - a, Vector3.up).normalized * 6 * e; grip = 1; }
                    else { aim = giant.Toward(b + Vector3.up * 25); grip = 0; }
                }
                else { aim = giant.Toward(Vector3.Lerp(a, b, t) + up + Vector3.up * 20); grip = .3f; }   // beckon: the piece hops by itself
            }
            // What the giant watches (GiantGrandmaster turns its head): the moving piece, the square just landed on, its
            // own next piece, else the nearest player on the board; nothing = it looks around by itself.
            Vector3 watch = boardCentre; float watchWeight = 0;
            int nextFrom = Playing && ply < score.Length ? int.Parse(score[ply][0]) : -1;
            if (Moving && moving) { watch = moving.transform.position + Vector3.up * 8; watchWeight = 1; }
            else if (Playing && (phase == GiantChessReplay.Warn || phase == GiantChessReplay.Strike)) { watch = Square(to); watchWeight = .8f; }
            else if (mine && phase == GiantChessReplay.Rest && nextFrom >= 0 && pieces[nextFrom]) { watch = Square(nextFrom); watchWeight = .9f; }
            else if (InRound)
            {
                // A round: this PC's own pawn, when it is on the board (each PC draws its own giants).
                var local = match.LocalFollow;
                if (local != null && BoardSquare(local.position) >= 0) { watch = local.position; watchWeight = .6f; }
            }
            else
            {
                float best = float.MaxValue;
                foreach (var pawn in players)
                    if (pawn && pawn.gameObject.activeInHierarchy && BoardSquare(pawn.Hips.position) >= 0)
                    {
                        float d = (pawn.Hips.position - giant.root.position).sqrMagnitude;
                        if (d < best) { best = d; watch = pawn.Hips.position; watchWeight = .6f; }
                    }
            }
            giant.Watch(watch, watchWeight);
            giant.SetReach(aim, weight, grip);
            giant.Tick(dt, dtReal, 7 / moveSeconds);
            if (!(mine && Moving && moving && carryFrom)) return;
            float tt = Mathf.Clamp01(elapsed / moveSeconds);
            Vector3 held = giant.Grip() - Vector3.up * pieceHeight;
            if (carryTo) { moving.transform.position = Vector3.Lerp(held, Square(to), Ease((tt - .85f) / .15f)); return; }
            if (tt < .3f) { moving.transform.position = held; released = false; return; }
            if (!released) { released = true; releasePoint = held; }
            float u2 = (tt - .3f) / .7f;
            moving.transform.position = Vector3.Lerp(releasePoint, Square(to), u2) + Vector3.up * Mathf.Sin(u2 * Mathf.PI) * 30;
        }
        // The game's end without a mate on the board: the winner cheers, the loser slumps; a draw pleases both.
        void EndReactions()
        {
            if (score.Length > 0 && score[score.Length - 1][5].Contains("#")) return;
            string r = CurrentGame.result ?? "";
            int winner = r.StartsWith("1-0") ? 0 : r.StartsWith("0-1") ? 1 : -1;
            for (int side = 0; side < 2; side++)
                giants[side]?.React(winner < 0 ? GiantReaction.Pleased : side == winner ? GiantReaction.Win : GiantReaction.Lose);
        }
        // The game for the series' turn `turn`: the seed's own order through the library (GiantChessReplay.Game).
        void LoadGame(int turn)
        {
            gameIndex = GiantChessReplay.Game(seed, turn, GameCount);
            CurrentGame = library.games[gameIndex];
            if (!parsed.TryGetValue(gameIndex, out score))
            {
                score = new string[CurrentGame.rows.Length][];
                for (int i = 0; i < score.Length; i++) score[i] = CurrentGame.rows[i].Split(',');
                parsed[gameIndex] = score;
            }
            foreach (var panel in GetComponentsInChildren<WarRoomScoreLabel>()) panel.Refresh(CurrentGame);
        }
        // Every piece back on its starting square. live: where the simulation is (the authority), a pawn standing where a
        // piece appears is thrown off first - a full-square collider must not appear inside a ragdoll.
        void ResetBoard(bool live)
        {
            for (int s = 0; s < 64; s++)
            {
                if (pieces[s]) { pieces[s].GetComponent<Collider>().enabled = false; Destroy(pieces[s]); }
                pieces[s] = null;
            }
            ulong setup = 0;
            for (int s = 0; s < 64; s++) if (Setup[s] != '.') { pieces[s] = BuildPiece(Setup[s], s); setup |= 1UL << s; }
            ply = 0; phase = GiantChessReplay.Rest; elapsed = 0; attack = 0; moving = null;
            if (live) Evacuate(setup);
            RefreshOccupiedMarks();
        }
        GameObject BuildPiece(char code, int square)
        {
            bool light = char.IsUpper(code); char kind = char.ToUpperInvariant(code);
            string name = kind == 'P' ? "Pawn" : kind == 'R' ? "Rook" : kind == 'N' ? "Knight" : kind == 'B' ? "Bishop" : "Queen";
            var root = new GameObject(kind == 'K' ? "King" : name); root.transform.SetParent(transform, false); root.transform.position = Square(square);
            // R6: the D6 ivory / ebony pieces with brass bands (the black king's cross in brass, as on the sheet)
            var ivory = Library("ivory") ?? white; var ebony = Library("ebony") ?? black;
            var piece = Model("RlChess" + (kind == 'K' ? "King" : name), root.transform, Vector3.zero, Vector3.one * ModelScale, 0,
                (r, key) => key == "ivory" ? (r == "Cross" && !light ? Library("brass") ?? gold : light ? ivory : ebony) : null);
            var prefab = piece ? null : Resources.Load<GameObject>("ChessTimerPieces/" + name);
            float height = 15;
            if (piece)
            {
                var renderers = piece.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                height = bounds.max.y - root.transform.position.y;
            }
            else if (prefab)
            {
                var model = Instantiate(prefab, root.transform, false); // Preserve the importer axis correction.
                var renderers = model.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (var r in renderers)
                {
                    bounds.Encapsulate(r.bounds); var slots = r.sharedMaterials;
                    for (int j = 0; j < slots.Length; j++) slots[j] = slots[j] && slots[j].name.Contains("Trim") ? gold : light ? white : black;
                    r.sharedMaterials = slots;
                }
                model.transform.localScale *= tileSize * .48f / Mathf.Max(bounds.size.x, bounds.size.z);
                bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                model.transform.position += new Vector3(root.transform.position.x - bounds.center.x, root.transform.position.y - bounds.min.y, root.transform.position.z - bounds.center.z);
                height = bounds.size.y;
            }
            else
            {
                var fallback = Shape(name, SquareLocal(square) + Vector3.up * 7.5f, new Vector3(7, 15, 7), light ? white : black);
                fallback.transform.SetParent(root.transform, true);
            }
            if (kind == 'K' && !piece)
            {
                var cross = Shape("King cross vertical", SquareLocal(square) + Vector3.up * (height + 2), new Vector3(1, 5, 1), gold, solid: false); cross.transform.SetParent(root.transform, true);
                cross = Shape("King cross horizontal", SquareLocal(square) + Vector3.up * (height + 2.5f), new Vector3(4, 1, 1), gold, solid: false); cross.transform.SetParent(root.transform, true);
            }
            var collider = root.AddComponent<BoxCollider>(); collider.size = new Vector3(tileSize, Mathf.Max(32, height), tileSize); collider.center = Vector3.up * collider.size.y * .5f;
            root.transform.rotation = transform.rotation * Quaternion.Euler(0, light ? -90 : 90, 0);
            return root;
        }
        void ResetPlayers() { var portal=GetComponent<RoyalLibraryPortal>(); if(portal!=null) portal.ResetPresentation(); if (approach != null) approach.ResetProgress(); for (int i = 0; i < players.Count; i++) { finished[i] = false; Place(i); } }
        readonly float[] bookRecovery = new float[12];
        readonly Transform[] bookRecoveryPoints = new Transform[12];
        readonly GameObject[] flattened = new GameObject[12];
        ChessFight.ProtectKing.ClosingBook[] books;
        void BookCrush(GameObject victim, Transform recovery, float delay)
        {
            int i = players.FindIndex(p => p.gameObject == victim);
            if (i < 0 || bookRecovery[i] > 0) return;
            var pawn = players[i]; pawn.SetInput(default); pawn.Teleport(pawn.Hips.position,pawn.Facing);
            var flat = new GameObject("Flattened book victim");
            flat.transform.position = pawn.Hips.position - Vector3.up * pawn.standHeight;
            foreach (var mesh in pawn.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = mesh.GetComponent<MeshRenderer>();
                if (mesh.sharedMesh == null || renderer == null || !renderer.enabled) continue;
                var part = new GameObject("Flattened body part"); part.transform.SetParent(flat.transform,false);
                part.transform.position = mesh.transform.position; part.transform.rotation = mesh.transform.rotation;
                part.transform.localScale = mesh.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            }
            flat.transform.localScale = new Vector3(1.4f,.1f,1.4f);
            flat.transform.position += Vector3.up * .65f;
            flattened[i] = flat; bookRecovery[i] = delay; bookRecoveryPoints[i] = recovery;
            pawn.gameObject.SetActive(false);
        }
        void ClearBookCrush(int i)
        {
            bookRecovery[i] = 0; bookRecoveryPoints[i] = null;
            if (flattened[i] != null) Destroy(flattened[i]); flattened[i] = null;
            if (i < players.Count) players[i].gameObject.SetActive(true);
        }
        void Place(int i)
        {
            ClearBookCrush(i);
            bool onApproach = approach != null && approach.Available && !approach.Entered(i);
            Vector3 facing = onApproach ? approach.Facing(i) : transform.forward;
            players[i].SetInput(default);
            players[i].Teleport(Spawn(i) + Vector3.up * (players[i].standHeight + .1f), facing);
            if (onApproach) approach.RecoverRoute(i);
            botGoals[i] = facing;
            if (i == selected) cameraRig.yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
        }
        void Select(int i) { selected = i % players.Count; cameraRig.soloTarget = players[selected]; cameraRig.distance = 12; cameraRig.maxDistance = 45; }
        void CursorState() { Cursor.visible = menu; Cursor.lockState = menu ? CursorLockMode.None : CursorLockMode.Locked; }
        void OnApplicationFocus(bool focus) { if (initialized && !focus) { menu = true; CursorState(); } }
        // The replay seconds now (negative: the chess has not started).
        double ReplayNow() => InRound ? (match.ChessStarted ? (ObstacleClock.Now - match.ChessStart) * Speed : -1) : localReplay;

        // Brings the board to the replay's spot `now`, one phase at a time. An event more than a real second old is a
        // catch-up (this PC was not there when it happened: a late joiner, a new host, a hitch): the board takes it
        // silently - no throws, no strikes, no reactions - and whole games behind are skipped outright.
        void Advance(double now)
        {
            var timing = Timing;
            int count = GameCount;
            var target = GiantChessReplay.Locate(now, seed, count, Plies, timing);
            if (cycle < 0 || seed != loadedSeed)
            {
                loadedSeed = seed;
                LoadGame(target.Cycle); ResetBoard(false); cycle = target.Cycle;
            }
            for (int guard = 0; guard < 4096; guard++)
            {
                var here = new GiantChessReplay.Spot { Cycle = cycle, Ply = ply, Phase = phase };
                if (!here.Before(target)) break;
                double at = GiantChessReplay.PhaseStart(here, seed, count, Plies, timing) + timing.Length(phase);
                bool live = now - at < Speed;
                if (!live && cycle < target.Cycle) { LoadGame(target.Cycle); ResetBoard(false); cycle = target.Cycle; continue; }
                Step(live);
            }
            elapsed = (float)target.T;
        }
        // The end of the phase the board is in.
        void Step(bool live)
        {
            switch (phase)
            {
                case GiantChessReplay.Rest:
                    if (ply < score.Length) BeginMove();
                    else { ply = score.Length; phase = GiantChessReplay.End; elapsed = 0; }
                    break;
                case GiantChessReplay.Move: Land(live); break;
                case GiantChessReplay.Warn: Strike(live); break;
                case GiantChessReplay.Strike: FinishMove(live); break;
                default: cycle++; LoadGame(cycle); ResetBoard(live); break;   // End: the next game of the series
            }
        }
        // A move's piece lifts off (Rest -> Move); its landing square warns while it flies.
        void BeginMove()
        {
            var row = score[ply]; from = int.Parse(row[0]); to = int.Parse(row[1]); rookFrom = int.Parse(row[2]); rookTo = int.Parse(row[3]);
            moving = pieces[from];
            phase = GiantChessReplay.Move; elapsed = 0;
            if (!moving) { Debug.LogWarning($"Giant chess: no piece on square {from} for move {ply + 1} of {CurrentGame.id}.", this); return; }
            moving.GetComponent<Collider>().enabled = false;
            if (giants[ply % 2] != null)
            {
                carryFrom = giants[ply % 2].Reachable(PieceTop(moving, out float height));
                carryTo = carryFrom && giants[ply % 2].Reachable(Square(to) + Vector3.up * height);
                released = false;
            }
            RefreshOccupiedMarks();
        }
        // The piece lands (Move -> Warn): a capture goes, a promotion becomes its new piece, castling brings the rook, and
        // the squares the piece now attacks warn. live: it happens now, so the giants react and, where the bodies are
        // simulated, whoever stands on the landing squares is thrown off first.
        void Land(bool live)
        {
            int captured=int.Parse(score[ply][6]);
            // The giants answer the move (GiantGrandmaster): mate = the mover wins, the other loses; a capture or a check
            // pleases the mover and startles the other.
            string san = score[ply][5];
            bool took = pieces[to] || (captured >= 0 && captured != to && pieces[captured]);
            if (live)
            {
                GiantGrandmaster mover = giants[ply % 2], waiting = giants[1 - ply % 2];
                if (san.Contains("#")) { mover?.React(GiantReaction.Win); waiting?.React(GiantReaction.Lose); }
                else if (took || san.Contains("+")) { mover?.React(GiantReaction.Pleased); waiting?.React(GiantReaction.Startled); }
            }
            if (moving)
            {
                if (pieces[to]) { pieces[to].GetComponent<Collider>().enabled = false; Destroy(pieces[to]); }
                if(captured>=0 && captured!=to && pieces[captured]) { pieces[captured].GetComponent<Collider>().enabled=false; Destroy(pieces[captured]);pieces[captured]=null; }
                pieces[to] = moving; pieces[from] = null; moving.transform.position = Square(to); moving.GetComponent<Collider>().enabled = true;
                if(score[ply][7]!=".") { moving.GetComponent<Collider>().enabled=false;Destroy(moving); moving=BuildPiece(score[ply][7][0],to);pieces[to]=moving; }
                if (rookFrom >= 0 && pieces[rookFrom]) { pieces[rookTo] = pieces[rookFrom]; pieces[rookFrom] = null; pieces[rookTo].transform.position = Square(rookTo); }
            }
            // Evacuate before the next physics step: a full-square collider must not spawn inside a ragdoll.
            phase = GiantChessReplay.Warn; elapsed = 0;
            if (live) Evacuate((1UL << to) | (rookTo >= 0 ? 1UL << rookTo : 0));
            RefreshOccupiedMarks();
            attack = ulong.Parse(score[ply][4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        // The warned squares strike (Warn -> Strike): where the bodies are simulated, whoever stands on one is thrown.
        void Strike(bool live)
        {
            phase = GiantChessReplay.Strike; elapsed = 0;
            if (!live || !ObstacleClock.Authority) return;
            foreach (var actor in ActorsOnBoard())
            {
                int s = SquareUnder(actor.Position);
                if (s >= 0 && (attack & (1UL << s)) != 0)
                    actor.AddObstacleHit(-transform.forward * knockbackSpeed + Vector3.up * upwardSpeed, 1.2f);
            }
        }
        // The shock wave is over (Strike -> the next move's Rest, or the game's End).
        void FinishMove(bool live)
        {
            attack = 0; moving = null; ply++; phase = GiantChessReplay.Rest; elapsed = 0;
            if (ply >= score.Length) { ply = score.Length; phase = GiantChessReplay.End; if (live) EndReactions(); }
        }
        // The square a body is on (its hips between 2 m under the board and 25 m over it), or -1.
        int SquareUnder(Vector3 p)
        {
            float y = transform.InverseTransformPoint(p).y;
            return y > -2 && y < 25 ? BoardSquare(p) : -1;
        }
        // Everything with a body on or over the board (the simulated ones: ImportedObstacleActor.CanReceiveObstacle).
        List<ImportedObstacleActor> ActorsOnBoard()
        {
            boardActors.Clear();
            Vector3 centre = transform.TransformPoint(new Vector3(0, 11.5f, tileSize * 4));
            int n = Physics.OverlapBoxNonAlloc(centre, new Vector3(tileSize * 4 + 1, 13.5f, tileSize * 4 + 1), boardHits, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(boardHits[i]);
                if (actor != null && actor.CanReceiveObstacle && !boardActors.Contains(actor)) boardActors.Add(actor);
            }
            return boardActors;
        }
        // Where the bodies are simulated: whoever stands on one of `squares` goes to the nearest free square (not further on
        // if it can help it) and is knocked back.
        void Evacuate(ulong squares)
        {
            if (!ObstacleClock.Authority || squares == 0) return;
            foreach (var actor in ActorsOnBoard())
            {
                int on = SquareUnder(actor.Position);
                if (on < 0 || (squares & (1UL << on)) == 0) continue;
                Vector3 p = actor.Position;
                Vector3 local = transform.InverseTransformPoint(p);
                Vector3 safe = transform.TransformPoint(new Vector3(Mathf.Clamp(local.x, -tileSize * 4 + 2, tileSize * 4 - 2), 0, -3));
                float distance = (safe - p).sqrMagnitude;
                for (int s = 0; s < 64; s++) if (!Occupied(s) && (squares & (1UL << s)) == 0)
                {
                    Vector3 candidate = Square(s);
                    float cost = (candidate - p).sqrMagnitude + (Vector3.Dot(candidate - p, transform.forward) > 0 ? tileSize : 0);
                    if (cost < distance) { distance = cost; safe = candidate; }
                }
                var driver = actor.gameObject != null ? actor.gameObject.GetComponent<ICharacterDriver>() : null;
                if (driver != null) driver.Teleport(safe, transform.rotation);
                actor.AddObstacleHit(-transform.forward * knockbackSpeed + Vector3.up * upwardSpeed, 1);
            }
        }
        // The squares that warn now: the landing squares while a piece flies, the attacked ones until they strike, and the
        // empty starting squares before the next game's pieces appear.
        void RefreshWarnings()
        {
            ulong mask = 0;
            if (Moving) { mask = 1UL << to; if (rookTo >= 0) mask |= 1UL << rookTo; }
            else if (phase == GiantChessReplay.Warn || phase == GiantChessReplay.Strike) mask = attack;
            else if (phase == GiantChessReplay.End && elapsed >= Timing.End - setupWarningSeconds * Speed)
                for (int s = 0; s < 64; s++) if (Setup[s] != '.' && !pieces[s]) mask |= 1UL << s;
            if (mask == shownWarnings) return;
            shownWarnings = mask;
            for (int s = 0; s < 64; s++) warnings[s].SetActive((mask & (1UL << s)) != 0);
        }
        // For the offline bots: a square that warns now.
        bool Dangerous(Vector3 p)
        {
            int s = BoardSquare(p);
            return s >= 0 && shownWarnings != ulong.MaxValue && (shownWarnings & (1UL << s)) != 0;
        }
        // The flying piece, the warnings' blink and the giants, every frame. dt: the giants' replay time step.
        void Visuals(float dt, float dtReal)
        {
            if (Moving && moving)
            {
                float t = Mathf.Clamp01(elapsed / moveSeconds);
                moving.transform.position = Vector3.Lerp(Square(from), Square(to), t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 22);
            }
            RefreshWarnings();
            bool strike = phase == GiantChessReplay.Strike;
            red.SetColor("_EmissionColor", Color.red * (strike ? 4 : .4f + Mathf.PingPong(Time.time * 2, 1)));
            redRim.SetColor("_EmissionColor", Color.red * 1.6f * (strike ? 4 : .4f + Mathf.PingPong(Time.time * 2, 1)));   // same blink, brighter rim
            for (int side = 0; side < 2; side++)
            {
                if (giants[side] != null) { DriveGiant(side, dt, dtReal); continue; }
                Vector3 target = Moving && ply % 2 == side && moving ? moving.transform.position + Vector3.up * 19 : shoulders[side] + Vector3.up * 3;
                hands[side].position = target; arms[side].position = (shoulders[side] + target) * .5f;
                arms[side].rotation = Quaternion.LookRotation(target - shoulders[side]); arms[side].localScale = new Vector3(5, 5, Vector3.Distance(target, shoulders[side]));
            }
        }
        // A round section: the host starts the chess when the first pawn reaches the chess room (the course's approach exit,
        // just inside the room's door); every PC then plays the same replay from that moment.
        void RoundUpdate()
        {
            seed = match.Seed;
            if (!match.ChessStarted && match.Configured && ObstacleClock.Authority && SomeoneAtChessEntry())
                match.StartChess(ObstacleClock.Now + .3);
            Advance(ReplayNow());
            Visuals(match.ChessStarted ? Time.deltaTime * Speed : 0, Time.deltaTime);
        }
        bool SomeoneAtChessEntry()
        {
            var entry = approach != null ? approach.ChessEntryPoint : null;
            if (entry == null) return false;
            float half = approach.LaneWidth * .5f;
            int n = Physics.OverlapBoxNonAlloc(entry.TransformPoint(new Vector3(0, 1.4f, 0)), new Vector3(half, 1.6f, 2.5f), boardHits, entry.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(boardHits[i]);
                if (actor != null && actor.isActiveAndEnabled) return true;
            }
            return false;
        }
        void Update()
        {
            if (!initialized) return;
            if (InRound) { RoundUpdate(); return; }
            if (players.Count == 0) return;
            if (Application.isFocused)
            {
                if (Input.GetKeyDown(KeyCode.Return)) { running = true; menu = false; if (approach != null) approach.BeginTiming(); CursorState(); }
                if (Input.GetKeyDown(KeyCode.Escape)) { menu = !menu; CursorState(); }
                if (Input.GetKeyDown(KeyCode.Tab)) Select((selected + 1) % 12);
                if (Input.GetKeyDown(KeyCode.B)) botsEnabled = !botsEnabled;
                if (Input.GetKeyDown(KeyCode.R)) Place(selected);
                if (Input.GetKeyDown(KeyCode.F3))
                {
                    // A new random series from its first game, everyone back to the start.
                    seed = UnityEngine.Random.Range(1, int.MaxValue); localReplay = 0; cycle = -1; running = false;
                    Advance(0); ResetPlayers();
                    foreach (var book in books) book.ResetCycle();
                    foreach (var keyboard in GetComponentsInChildren<ChessFight.ProtectKing.TypewriterStairs>()) keyboard.NewRound();
                }
                // Advance one phase, never skip a chess move.
                if (Input.GetKeyDown(KeyCode.N) && Playing)
                {
                    var here = new GiantChessReplay.Spot { Cycle = cycle, Ply = ply, Phase = phase };
                    localReplay = GiantChessReplay.PhaseStart(here, seed, GameCount, Plies, Timing) + Timing.Length(phase) + .001;
                }
            }
            bool active = running && !menu && Application.isFocused;
            foreach (var book in books) book.Simulating = !menu && Application.isFocused;
            if (approach != null) approach.AdvanceTimer(!menu && Application.isFocused ? Time.deltaTime : 0);
            if (Playing) localReplay += Time.deltaTime * Speed;
            Advance(localReplay);
            Visuals((active ? Time.deltaTime : 0) * Speed, !menu && Application.isFocused ? Time.deltaTime : 0);
            for (int i = 0; i < players.Count; i++)
            {
                var pawn = players[i]; pawn.SetInput(default);
                if (bookRecovery[i] > 0)
                {
                    if (!menu && Application.isFocused) bookRecovery[i] = Mathf.Max(0,bookRecovery[i]-Time.deltaTime);
                    if (bookRecovery[i] <= 0)
                    {
                        var point = bookRecoveryPoints[i];
                        ClearBookCrush(i);
                        if (point != null) pawn.Teleport(point.TransformPoint(new Vector3((i%4-1.5f)*1.5f,pawn.standHeight+.1f,(i/4-1)*1.2f)),point.forward);
                        else Place(i);
                        if (approach != null) approach.RecoverRoute(i);
                    }
                    continue;
                }
                if (!pawn.IsFinite() || transform.InverseTransformPoint(pawn.Hips.position).y < -8) Place(i);
                if (approach != null) approach.Observe(i, pawn);
                var boardPosition = transform.InverseTransformPoint(pawn.Hips.position);
                if ((approach == null || approach.Entered(i)) && boardPosition.z > tileSize * 8 + 3 && Mathf.Abs(boardPosition.x) < tileSize * 4) finished[i] = true;
                if (i != selected && active && botsEnabled && !finished[i]) DriveBot(i);
            }
            // Manual movement remains available after the last move and after crossing the finish.
            if (!menu && Application.isFocused && bookRecovery[selected] <= 0)
            {
                var input = GameSettings.Move;
                players[selected].SetInput(new PawnInput { move = Vector3.ClampMagnitude(cameraRig.FlatRight * input.x + cameraRig.FlatForward * input.y, 1), aim = cameraRig.AimForward,
                    jump = GameSettings.Pressed(GameKey.Jump), sprint = GameSettings.Held(GameKey.Sprint), grab = Input.GetMouseButton(1), shove = Input.GetMouseButtonDown(0), shoveHeld = Input.GetMouseButton(0) });
            }
        }
        void DriveBot(int i)
        {
            var pawn = players[i];
            if (approach != null && approach.Available && !approach.Entered(i)) { approach.DriveBot(i, pawn); return; }
            var p = pawn.Hips.position;
            Vector3 best = Vector3.zero; float bestScore = float.NegativeInfinity;
            // Short obstacle-aware steering; bots are test opponents, not puzzle-solving teammates.
            for (int direction = 0; direction < 16; direction++)
            {
                var d = transform.TransformDirection(Quaternion.Euler(0, direction * 22.5f, 0) * Vector3.forward);
                var future = p + d * 4;
                var localFuture = transform.InverseTransformPoint(future);
                if (Mathf.Abs(localFuture.x) > tileSize * 4 - 2 || localFuture.z < -17) continue;
                int square = BoardSquare(future);
                bool blocked = square >= 0 && Occupied(square);
                if (blocked) continue;
                float value = Vector3.Dot(d, transform.forward) * 2 - (Dangerous(future) ? 8 : 0) + Vector3.Dot(d, botGoals[i]) * .2f;
                if (value > bestScore) { bestScore = value; best = d; }
            }
            botGoals[i] = best;
            bool shove = false; Vector3 aim = best;
            for (int other = 0; other < players.Count; other++) if (other / 6 != i / 6 && (players[other].Hips.position - p).sqrMagnitude < 9)
            { aim = (players[other].Hips.position - p).normalized; shove = Time.time > nextShove[i]; if (shove) nextShove[i] = Time.time + 2; break; }
            pawn.SetInput(new PawnInput { move = best, aim = aim, shove = shove });
        }
        void OnGUI()
        {
            if (!initialized || InRound) return;
            if (hud == null) hud = new GUIStyle(GUI.skin.label) { fontSize = 18, font = RuntimePanels.KoreanFont, wordWrap = true };
            int a = 0, b = 0; for (int i = 0; i < 12; i++) if (finished[i]) { if (i < 6) a++; else b++; }
            bool inOffices = approach != null && approach.Available && !approach.Entered(selected);
            GUI.Box(new Rect(12, 12, 720, inOffices ? 250 : 195), "");
            string move = ply < score.Length ? $"{ply / 2 + 1}. {(ply % 2 == 0 ? "백" : "흑")} {score[ply][5]}" : "기보 종료 · " + CurrentGame.result + " · 곧 다음 대국";
            int officeSeconds = approach != null ? Mathf.FloorToInt(approach.Elapsed(selected)) : 0;
            string stage = approach != null && approach.Available && !approach.Entered(selected)
                ? $"왕의 서재 · 체크포인트 {approach.Checkpoint(selected)}/{approach.CheckpointCount} · {officeSeconds / 60}:{officeSeconds % 60:00} / 전체 목표 2~3분"
                : $"왕의 체스 탁자 · 거인 체스 · 서재 기록 {officeSeconds / 60}:{officeSeconds % 60:00}";
            string state = approach != null && approach.Available && !approach.ChessStarted
                ? "체스판 입구 도착 대기"
                : phase == GiantChessReplay.Move ? "기물 착지 예고" : phase == GiantChessReplay.Warn ? "빨간 칸 공격 예고"
                : phase == GiantChessReplay.Strike ? "충격파!" : phase == GiantChessReplay.End ? "대국 끝 · 빨간 칸에 다음 대국 기물이 놓임" : "다음 수 준비";
            GUI.Label(new Rect(24, 20, 690, 175), $"{CurrentGame.Title}\n{stage} · {move}\n{state} · 백팀 도착 {a}/6 / 흑팀 {b}/6\nEnter 시작 · WASD 이동 · 마우스 좌 밀치기 / 우 잡기 · Space 점프\nTab 캐릭터 · B 봇 · R 현재 구간 복귀 · F3 전체 초기화 · N 다음 연출 단계 · Esc 기보 일시정지", hud);
            if (inOffices) GUI.Label(new Rect(24, 202, 690, 50),
                "왕의 서재: 책이 열렸을 때 전진 · 타자기: 힌트 1~5를 기억하고 각 단의 글자 선택\n책이 덮히면 납작해진 뒤 직전 지점 부활 · 액자: 후반 기보 예고", hud);
        }
        void OnDestroy()
        {
            if (books != null) foreach (var book in books) if (book != null) book.Crushed -= BookCrush;
            for (int i=0;i<players.Count;i++) if (flattened[i] != null) Destroy(flattened[i]);
            if (!initialized) return;
            if (!InRound) { Physics.gravity = savedGravity; Cursor.lockState = savedLock; Cursor.visible = savedCursor; }
            foreach (var m in materials) if (m) Destroy(m);
        }
    }
}
