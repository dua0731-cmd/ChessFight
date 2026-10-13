using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The skill annotations (R128, 승규 님 10-13: "C 모양으로 unity에 적용"): every cast in the three skill test scenes
    /// is judged on its result and earns a chess annotation over the caster's head (Core <see cref="SkillGrades"/>):
    /// !! a crowd or a ring-out, ! a save or two at once, !? it paid but the caster fell, ? an attack that hit nothing,
    /// ?? the caster (or only allies) fell. !! shows on every screen, the rest only on the caster's own.
    ///
    /// It only listens: the three modes' skill moments (<see cref="RagdollPawn.SkillFx"/>, <see cref="RagdollPawn.QueenHillFx"/>,
    /// <see cref="SwordFightSkills.Fx"/>) and the pieces' positions. Online, the clients raise the same moments when they
    /// replay the host's (LabNetLink.Play), so every screen judges for itself and nothing new goes on the wire (protocol
    /// v18 as it was). A cast is a caster's moments until they go quiet; a few skills stay open for what they leave
    /// behind (the Pawn Rush bishop's wire, the Queen of the Hill bishop's hover and rook's request). !! is stamped the
    /// moment it is earned; anything else when the cast ends; a ring-out found up to 2.5 s after the hit still raises a
    /// lesser stamp to !!.
    /// </summary>
    public class SkillGradeJudge : MonoBehaviour
    {
        /// <summary>A cast was judged: the caster, its grade, and whether this screen shows it.</summary>
        public static event Action<RagdollPawn, SkillGrade, bool> Graded;
        public static SkillGradeJudge Current { get; private set; }

        const float Quiet = 0.8f;          // a cast ends this long after its caster's last moment
        const float OutWatch = 2.5f;       // a hit enemy that falls within this long was put out by it
        const float OutDrop = 5f;          // ... falling this far below where it was hit (m)
        const float SelfDrop = 7f;         // the caster falling this far below where it cast (m)
        const float SwordOutY = -2.5f;     // Sword Fight: off the board on the way to its ring-out at -3 m (SwordFightGame)

        enum Mode { PawnRush, QueenHill, SwordF, SwordEdge }

        class Watched
        {
            public RagdollPawn pawn;
            public float y0, until;
            public Vector3 last;
            public bool enemy, fell;
        }

        class Cast
        {
            public RagdollPawn by;
            public Mode mode;
            public PieceKind piece;
            public float start, last, holdUntil;
            public float startY;
            public Vector3 lastHips;
            public readonly List<Watched> watched = new List<Watched>();
            public readonly HashSet<RagdollPawn> enemies = new HashSet<RagdollPawn>();
            public int helped;
            public bool failed, cancelled, selfFell, closed, pending;
            public SkillGrade shown;
            public bool Watching(float now) { foreach (var w in watched) if (!w.fell && now < w.until) return true; return false; }
        }

        readonly Dictionary<RagdollPawn, Cast> open = new Dictionary<RagdollPawn, Cast>();
        readonly List<Cast> after = new List<Cast>();
        readonly Dictionary<RagdollPawn, Cast> wards = new Dictionary<RagdollPawn, Cast>();   // guarded ally → the king's cast
        readonly List<RagdollPawn> scratch = new List<RagdollPawn>();
        SkillGradeStamps stamps;
        LabGame lab;
        SwordFightGame sword;
        float nextLookup;

        // ---------------------------------------------------------------- set up in the three skill test scenes

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        static void Ensure()
        {
            if (Current != null) return;
            bool bed = FindAnyObjectByType<PawnRushSkillBed>() != null || FindAnyObjectByType<QueenHillSkillBed>() != null
                       || FindAnyObjectByType<SwordFightSkillBed>() != null;
            if (!bed) return;
            var go = new GameObject("Skill grades (R128)");
            go.AddComponent<SkillGradeJudge>();
        }

        void Awake()
        {
            Current = this;
            stamps = gameObject.AddComponent<SkillGradeStamps>();
        }

        void OnEnable()
        {
            RagdollPawn.SkillFx += OnPawnRush;
            RagdollPawn.QueenHillFx += OnQueenHill;
            SwordFightSkills.Fx += OnSword;
        }

        void OnDisable()
        {
            RagdollPawn.SkillFx -= OnPawnRush;
            RagdollPawn.QueenHillFx -= OnQueenHill;
            SwordFightSkills.Fx -= OnSword;
        }

        void OnDestroy() { if (Current == this) Current = null; }

        // ---------------------------------------------------------------- whose screen this is

        /// <summary>The pawn this screen plays (the Sword Fight test's fighter, or the lab's local player).</summary>
        public RagdollPawn Me
        {
            get
            {
                if (Time.unscaledTime >= nextLookup && (lab == null && sword == null))
                {
                    nextLookup = Time.unscaledTime + 1f;
                    sword = FindAnyObjectByType<SwordFightGame>();
                    if (sword == null) lab = FindAnyObjectByType<LabGame>();
                }
                if (sword != null) { var f = sword.Local; return f != null ? f.Pawn : null; }
                return lab != null ? lab.LocalPawn : null;
            }
        }

        static bool Allies(RagdollPawn a, RagdollPawn b) => a != null && b != null && a != b && a.Team != Teams.None && a.Team == b.Team;
        static bool Enemies(RagdollPawn a, RagdollPawn b) => a != null && b != null && a != b && Teams.AreEnemies(a.Team, b.Team);

        // ---------------------------------------------------------------- the three modes' moments

        void OnPawnRush(SkillFxEvent e)
        {
            var c = Touch(e.by, Mode.PawnRush, PieceOf(e.kind.ToString(), e.by));
            if (c == null) return;
            switch (e.kind)
            {
                case SkillFxKind.QueenHit: case SkillFxKind.RookHit: case SkillFxKind.RookStop:
                case SkillFxKind.BishopTrip: case SkillFxKind.KnightStomp: case SkillFxKind.PawnHit:
                    Hit(c, e.target); break;
                case SkillFxKind.PawnHelp:
                    c.helped++; break;
                case SkillFxKind.BishopWire:
                    c.holdUntil = Mathf.Max(c.holdUntil, Now + 6.5f); break;   // the wire lies 6 s
            }
            Check(c);
        }

        void OnQueenHill(QueenHillFxEvent e)
        {
            var c = Touch(e.by, Mode.QueenHill, PieceOf(e.kind.ToString(), e.by));
            if (c == null) return;
            switch (e.kind)
            {
                case QueenHillFxKind.QueenHit: case QueenHillFxKind.BishopHit: case QueenHillFxKind.PawnBump:
                    if (e.count == 0) Hit(c, e.target);   // 1 = the king's guard took it (QhHit)
                    break;
                case QueenHillFxKind.KnightFlatten: case QueenHillFxKind.KnightFall: case QueenHillFxKind.Down:
                    Hit(c, e.target); break;
                case QueenHillFxKind.WardOn:
                    // The king's guard on an ally: his cast stays open while it lasts (size = its time), and a blow
                    // it takes counts as his save, whoever raised it.
                    c.holdUntil = Mathf.Max(c.holdUntil, Now + e.size + 0.1f);
                    if (e.target != null) wards[e.target] = c;
                    break;
                case QueenHillFxKind.WardBlock:
                    if (e.target != null && wards.TryGetValue(e.target, out var king) && !king.closed) { king.helped++; Check(king); }
                    break;
                // Aiming is part of the cast: it stays open from the lock to the blow, however long the aim.
                case QueenHillFxKind.QueenLock: case QueenHillFxKind.KnightLock: c.holdUntil = Now + 6f; break;
                case QueenHillFxKind.QueenSlash: c.holdUntil = 0f; break;
                case QueenHillFxKind.KnightLeap: c.holdUntil = Now + e.size + 0.2f; break;   // size = the flight time
                case QueenHillFxKind.BishopRise: c.holdUntil = Now + 8f; break;     // open while it hovers
                case QueenHillFxKind.BishopLand: case QueenHillFxKind.BishopDrop: c.holdUntil = 0f; break;
                case QueenHillFxKind.RookRequest: c.holdUntil = Now + 6f; break;    // open while the ally decides
                case QueenHillFxKind.RookSwap: case QueenHillFxKind.RookCancel: c.holdUntil = 0f; break;
            }
            Check(c);
        }

        void OnSword(SfFxEvent e)
        {
            var by = e.by != null ? e.by.Pawn : null;
            bool edge = e.kind >= SfFxKind.EdgeCast;
            var c = Touch(by, edge ? Mode.SwordEdge : Mode.SwordF, e.by != null ? e.by.Piece : PieceKind.Pawn);
            if (c == null) return;
            if (edge) c.mode = Mode.SwordEdge;
            switch (e.kind)
            {
                case SfFxKind.KingCounterHit: case SfFxKind.QueenHit: case SfFxKind.RookHit: case SfFxKind.BishopPin:
                case SfFxKind.KnightHit: case SfFxKind.KingLandHit: case SfFxKind.QueenCut: case SfFxKind.KnightKickHit:
                    Hit(c, e.target); break;
                case SfFxKind.KingParry: case SfFxKind.BishopCatch:
                    c.helped++; break;
                case SfFxKind.KingStab: c.holdUntil = Now + 4f; break;              // hanging on the sword
                case SfFxKind.KingLand: c.helped++; c.holdUntil = 0f; break;        // saved himself
                case SfFxKind.KingDrop: c.holdUntil = 0f; break;
                case SfFxKind.KingWhiff: case SfFxKind.QueenMiss:
                    c.failed = true; break;
                case SfFxKind.Interrupted: case SfFxKind.EdgeCancel:
                    c.cancelled = true; break;
                case SfFxKind.RookBreak:
                    // The crumbling edge takes whoever stands on it: watch everyone near for a fall.
                    foreach (var p in FindObjectsByType<RagdollPawn>(FindObjectsSortMode.None))
                        if (p != null && p != c.by && p.Hips != null && Flat(p.Hips.position - e.at).magnitude < 3.5f) Watch(c, p, Enemies(c.by, p));
                    break;
            }
            Check(c);
        }

        static PieceKind PieceOf(string kind, RagdollPawn by)
        {
            if (kind.StartsWith("King")) return PieceKind.King;
            if (kind.StartsWith("Queen")) return PieceKind.Queen;
            if (kind.StartsWith("Rook")) return PieceKind.Rook;
            if (kind.StartsWith("Bishop")) return PieceKind.Bishop;
            if (kind.StartsWith("Knight")) return PieceKind.Knight;
            if (kind.StartsWith("Pawn")) return PieceKind.Pawn;
            return by != null ? by.Piece : PieceKind.Pawn;
        }

        // ---------------------------------------------------------------- casts

        static float Now => Time.time;

        Cast Touch(RagdollPawn by, Mode mode, PieceKind piece)
        {
            if (by == null) return null;
            if (open.TryGetValue(by, out var c) && !c.closed)
            {
                c.last = Now;
                return c;
            }
            c = new Cast { by = by, mode = mode, piece = piece, start = Now, last = Now };
            if (by.Hips != null) { c.startY = by.Hips.position.y; c.lastHips = by.Hips.position; }
            open[by] = c;
            return c;
        }

        void Hit(Cast c, RagdollPawn target)
        {
            if (target == null || target == c.by) return;
            if (Enemies(c.by, target)) { c.enemies.Add(target); Watch(c, target, true); }
        }

        void Watch(Cast c, RagdollPawn p, bool enemy)
        {
            foreach (var w in c.watched) if (w.pawn == p) { w.until = Now + OutWatch; return; }
            if (p.Hips == null) return;
            c.watched.Add(new Watched { pawn = p, y0 = p.Hips.position.y, last = p.Hips.position, until = Now + OutWatch, enemy = enemy });
        }

        SkillGradeRule RuleOf(Cast c)
        {
            switch (c.mode)
            {
                case Mode.PawnRush:
                    switch (c.piece)
                    {
                        case PieceKind.Queen: return SkillGradeRule.Attack(3);
                        case PieceKind.Bishop: return SkillGradeRule.Attack(2);
                        case PieceKind.Rook: return SkillGradeRule.Move(3);      // also a way through
                        default: return SkillGradeRule.Move(2);                  // the pawn's steps, the knight's leap
                    }
                case Mode.QueenHill:
                    switch (c.piece)
                    {
                        case PieceKind.Queen: case PieceKind.Bishop: case PieceKind.Knight: return SkillGradeRule.Attack(2);
                        case PieceKind.King: return SkillGradeRule.Move(3);       // a guard: three saves is !!
                        default: return SkillGradeRule.Move(3);                   // the rook's swap, the pawn's dash
                    }
                case Mode.SwordF:
                    switch (c.piece)
                    {
                        case PieceKind.King: case PieceKind.Knight: case PieceKind.Bishop: return SkillGradeRule.Attack(2);
                        default: return SkillGradeRule.Attack(3);
                    }
                default:   // the edge skills: one enemy is what they aim at; the king's and the bishop's are saves
                    bool attack = c.piece == PieceKind.Queen || c.piece == PieceKind.Rook || c.piece == PieceKind.Knight;
                    return new SkillGradeRule { Big = 2, MissIsMistake = attack, OneIsGreat = true };
            }
        }

        SkillTally TallyOf(Cast c)
        {
            var t = new SkillTally { Enemies = c.enemies.Count, Helped = c.helped, SelfFell = c.selfFell, Failed = c.failed };
            foreach (var w in c.watched)
            {
                if (!w.fell) continue;
                if (w.enemy) { t.RingOuts++; if (!c.enemies.Contains(w.pawn)) t.Enemies++; }
                else t.AllyFalls++;
            }
            // The king's guard counts its saves as the others count enemies.
            if (c.mode == Mode.QueenHill && c.piece == PieceKind.King && c.helped >= 3) t.Enemies = Mathf.Max(t.Enemies, c.helped);
            return t;
        }

        /// <summary>!! is stamped the moment it is earned, not when the cast ends.</summary>
        void Check(Cast c)
        {
            if (c.cancelled || c.shown == SkillGrade.Brilliant || c.shown == SkillGrade.Interesting) return;
            var g = SkillGrades.Decide(TallyOf(c), RuleOf(c));
            if (g == SkillGrade.Brilliant) Show(c, g);
        }

        void Show(Cast c, SkillGrade g)
        {
            if (g == SkillGrade.None || g == c.shown) return;
            c.shown = g;
            bool mine = c.by == Me;
            bool visible = SkillGrades.ShowsTo(g, mine);
            var t = TallyOf(c);
            Debug.Log($"[R128] {c.by.DisplayName} {c.mode} {c.piece}: {SkillGrades.Symbol(g)} {SkillGrades.Name(g)} "
                      + $"(적 {t.Enemies}, 장외 {t.RingOuts}, 도움 {t.Helped}, 아군 떨어짐 {t.AllyFalls}, 나 떨어짐 {(t.SelfFell ? 1 : 0)}) "
                      + (visible ? "보임" : "안 보임(남의 화면)"));
            Graded?.Invoke(c.by, g, visible);
            if (visible && stamps != null) stamps.Stamp(c.by, g, c.mode == Mode.SwordF || c.mode == Mode.SwordEdge);
        }

        // ---------------------------------------------------------------- every frame: falls and endings

        void Update()
        {
            float now = Now;
            scratch.Clear();
            foreach (var kv in open) scratch.Add(kv.Key);
            foreach (var by in scratch)
            {
                var c = open[by];
                if (c.by == null) { open.Remove(by); continue; }
                Track(c, now);
                if (now - c.last < Quiet || now < c.holdUntil) continue;
                c.closed = true;
                open.Remove(by);
                if (c.cancelled) continue;
                var g = SkillGrades.Decide(TallyOf(c), RuleOf(c));
                // The edge skills are there to put someone out: wait for the fall rather than stamp ! and then !!.
                c.pending = c.mode == Mode.SwordEdge && g != SkillGrade.Brilliant && c.Watching(now);
                if (!c.pending && (c.shown == SkillGrade.None || SkillGrades.Raises(c.shown, g))) Show(c, g);
                if (c.Watching(now)) after.Add(c);
            }
            for (int i = after.Count - 1; i >= 0; i--)
            {
                var c = after[i];
                if (c.by == null) { after.RemoveAt(i); continue; }
                if (!c.Watching(now))
                {
                    if (c.pending) { c.pending = false; var last = SkillGrades.Decide(TallyOf(c), RuleOf(c)); if (c.shown == SkillGrade.None) Show(c, last); }
                    after.RemoveAt(i);
                    continue;
                }
                Track(c, now);
                var g = SkillGrades.Decide(TallyOf(c), RuleOf(c));
                if (SkillGrades.Raises(c.shown, g)) Show(c, g);
            }
        }

        void Track(Cast c, float now)
        {
            if (c.by.Hips != null && !c.selfFell && !c.closed)
            {
                Vector3 h = c.by.Hips.position;
                if (h.y < (Sword(c) ? SwordOutY : c.startY - SelfDrop)) c.selfFell = true;
                c.lastHips = h;
                if (c.selfFell) Check(c);
            }
            foreach (var w in c.watched)
            {
                if (w.fell || w.pawn == null || w.pawn.Hips == null || now > w.until) continue;
                Vector3 h = w.pawn.Hips.position;
                if (h.y < (Sword(c) ? SwordOutY : w.y0 - OutDrop)) { w.fell = true; Check(c); }
                w.last = h;
            }
        }

        static bool Sword(Cast c) => c.mode == Mode.SwordF || c.mode == Mode.SwordEdge;

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
