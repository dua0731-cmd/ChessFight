using System;
using System.Collections;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Scripted runs of the Queen of the Hill skills in their test scene (R89), for checking them without a keyboard (an
    /// AI driving Unity through the editor bridge) and for the film. It places P1 and the dummies (allies or enemies),
    /// presses the skill keys in code (P1's and the dummies': the enemy queen that slashes the king's guard, the rook the
    /// pawn dodges), and writes what happened to <see cref="Results"/>. Start one while playing:
    /// <c>QueenHillSkillProbe.Run("king")</c>; the names are in <see cref="Names"/>. Measuring is not checking: a person
    /// still has to try it in Unity.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class QueenHillSkillProbe : MonoBehaviour
    {
        public static readonly string[] Names = { "king", "queen", "rook", "bishop", "knight", "pawn", "pawn-squeeze", "king-help", "rook-up", "rook-far", "all" };
        public static string Status { get; private set; } = "idle";
        /// <summary>A run is under way (the bed's test helpers stay out of it unless the run asks for them).</summary>
        public static bool Busy => Status.StartsWith("running");
        /// <summary>The current run has set its pieces down and settled them (the film records from here).</summary>
        public static bool Staged { get; private set; }
        /// <summary>The last key pressed, as a player would read it (the film shows it); the serial counts presses.</summary>
        public static string Hint { get; private set; } = "";
        public static int HintSerial { get; private set; }
        public static readonly List<string> Results = new List<string>();

        LabGame game;
        QueenHillSkillBed bed;
        bool driving;

        /// <summary>What one driven piece is told this frame (P1 or a dummy).</summary>
        class Drive
        {
            public Vector3 move, aim = Vector3.forward;
            public bool tap, click, grab;
        }

        readonly Dictionary<RagdollPawn, Drive> drives = new Dictionary<RagdollPawn, Drive>();

        public static string Run(string name)
        {
            var bed = FindFirstObjectByType<QueenHillSkillBed>();
            if (bed == null) return "no QueenHillSkillBed (open QueenOfTheHill_SkillTest and press Play)";
            var probe = bed.GetComponent<QueenHillSkillProbe>();
            if (probe == null) probe = bed.gameObject.AddComponent<QueenHillSkillProbe>();
            probe.StopAllCoroutines();
            Results.Clear();
            probe.StartCoroutine(probe.Main(name));
            return Status = "running " + name;
        }

        public static string Report() => Status + "\n" + string.Join("\n", Results);

        /// <summary>The pieces the next "rook-up" run swaps with, one swap each (the film shows their colours).</summary>
        public static PieceKind[] rookPartners = { PieceKind.Bishop, PieceKind.Queen, PieceKind.Knight };

        /// <summary>Stop the clock (time scale 0, frames still drawn) this many game seconds after the next run is
        /// staged, to look at a moment from several cameras; set Time.timeScale back to 1 to go on.</summary>
        public static void FreezeAfter(float seconds) => freezeAfter = seconds;

        /// <summary>Game seconds since the current run was staged.</summary>
        public static float SinceStaged { get; private set; }

        static float freezeAfter = -1f;

        void LateUpdate()
        {
            if (!Staged) { SinceStaged = 0f; return; }
            SinceStaged += Time.deltaTime;
            if (freezeAfter >= 0f && SinceStaged >= freezeAfter)
            {
                Time.timeScale = 0f;
                freezeAfter = -1f;
            }
        }

        void Update()
        {
            if (!driving) return;
            foreach (var kv in drives)
            {
                var p = kv.Key;
                var d = kv.Value;
                if (p == null) continue;
                p.SetInput(new PawnInput { move = d.move, aim = d.aim, shove = d.click, grab = d.grab });
                p.SetSkillInput(d.tap);
                d.tap = d.click = false;
            }
        }

        RagdollPawn P1 => bed != null ? bed.P1 : null;

        Drive Of(RagdollPawn p)
        {
            if (!drives.TryGetValue(p, out var d)) drives[p] = d = new Drive();
            return d;
        }

        void Tap(RagdollPawn p, string hint = null)
        {
            Of(p).tap = true;
            if (hint != null) Say(hint);
        }

        void Click(RagdollPawn p, string hint = null)
        {
            Of(p).click = true;
            if (hint != null) Say(hint);
        }

        static void Say(string hint)
        {
            Hint = hint;
            HintSerial++;
        }

        /// <summary>Aim <paramref name="p"/> at a spot on the floor the way the camera would (from its aim eye).</summary>
        void AimAt(RagdollPawn p, Vector3 spot)
        {
            Vector3 flat = spot - p.Hips.position;
            flat.y = 0f;
            Vector3 eye = p.AimEye(flat.sqrMagnitude > 1e-4f ? flat.normalized : p.Facing);
            Of(p).aim = (spot - eye).normalized;
        }

        void AimFlat(RagdollPawn p, Vector3 dir)
        {
            dir.y = 0f;
            // A little down, as the lab camera looks (14°).
            Of(p).aim = (dir.normalized + Vector3.down * 0.25f).normalized;
        }

        static void Add(string line)
        {
            Results.Add(line);
            QueenHillSkillBed.Report("[시험] " + line);
        }

        IEnumerator Main(string name)
        {
            game = GetComponent<LabGame>();
            bed = GetComponent<QueenHillSkillBed>();
            yield return null;
            if (P1 == null) { Status = "no P1"; yield break; }
            float testCooldown = bed.skills.testCooldown;
            bed.skills.testCooldown = 0f;   // the probe checks the design cooldowns
            string[] list = name == "all" ? Array.FindAll(Names, n => n != "all") : new[] { name };
            foreach (var n in list)
            {
                Status = "running " + n;
                IEnumerator run = n switch
                {
                    "king" => King(),
                    "queen" => Queen(),
                    "rook" => Rook(),
                    "bishop" => Bishop(),
                    "knight" => Knight(),
                    "pawn" => PawnDodge(),
                    "pawn-squeeze" => PawnSqueeze(),
                    "king-help" => KingHelper(),
                    "rook-up" => RookUp(rookPartners),
                    "rook-far" => RookFar(),
                    // "rook-up-bishop" and the like: one swap with that piece.
                    var r when r.StartsWith("rook-up-") && Enum.TryParse(r.Substring(8), true, out PieceKind one) => RookUp(new[] { one }),
                    _ => null,
                };
                if (run == null) { Add($"모르는 시험: {n}"); continue; }
                Staged = false;
                yield return run;
                Staged = false;
                StopAll();
                QueenHillSkillBed.HelpersForProbe = false;
                QueenHillSkillBed.NextRookPartner = null;
            }
            bed.skills.testCooldown = testCooldown;
            Status = "done " + name;
        }

        void StopAll()
        {
            foreach (var kv in drives)
                if (kv.Key != null) kv.Key.SetInput(new PawnInput());
            drives.Clear();
            driving = false;
            QueenHillSkillBed.PawnRushOnly.Clear();
        }

        /// <summary>Set P1 and the dummies down for a run: P1's piece, then each dummy's piece, side and spot (the rest
        /// parked out of the way), and let them settle.</summary>
        IEnumerator Stage(PieceKind piece, Vector3 at, Vector3 face, params (PieceKind piece, bool ally, Vector3 at, Vector3 face)[] dummies)
        {
            StopAll();
            driving = true;
            var p1 = P1;
            if (p1.Piece != piece) p1.SetPiece(piece);
            p1.ResetSkill();
            QueenHillSkillBed.Place(p1, at, face);
            Of(p1).aim = (FlatDir(face) + Vector3.down * 0.25f).normalized;
            for (int i = 0; i < LabLayout.DummySpawns.Length; i++)
            {
                var d = bed.Dummy(i);
                if (d == null) continue;
                d.ResetSkill();
                d.SetInput(new PawnInput());
                if (i < dummies.Length)
                {
                    var s = dummies[i];
                    if (d.Piece != s.piece) d.SetPiece(s.piece);
                    bed.SetSide(d, p1, s.ally);
                    QueenHillSkillBed.Place(d, s.at, s.face);
                    Of(d).aim = (FlatDir(s.face) + Vector3.down * 0.25f).normalized;
                }
                else
                {
                    if (d.Piece != PieceKind.Pawn) d.SetPiece(PieceKind.Pawn);
                    bed.SetSide(d, p1, false);
                    QueenHillSkillBed.Place(d, new Vector3(12f + i * 1.5f, 0f, -12f), Vector3.back);
                }
            }
            for (int i = 1; i < game.players.Length; i++)
                if (game.players[i].pawn != null) QueenHillSkillBed.Place(game.players[i].pawn, new Vector3(13f, 0f, -14f + i * 1.5f), Vector3.back);
            yield return Wait(0.8f);
            Staged = true;
        }

        static Vector3 FlatDir(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
        }

        /// <summary>Game seconds (the film steps game time; slow motion slows it).</summary>
        static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                yield return null;
                t += Time.deltaTime;
            }
        }

        static string Who(RagdollPawn p) => p == null ? "-" : $"{p.DisplayName}({ChessPieces.Name(p.Piece)})";

        static string StateOf(RagdollPawn p) =>
            p.State == PawnState.Ragdoll ? "넘어짐" : p.Squashed ? "납작" : p.WardLeft > 0f ? "호위 중 서 있음" : "서 있음";

        // ---------------------------------------------------------------- 1.B king: the guard leaves the king out

        IEnumerator King()
        {
            var face = Vector3.right;
            yield return Stage(PieceKind.King, new Vector3(1.2f, 0f, -2.5f), face,
                (PieceKind.Rook, true, new Vector3(3.6f, 0f, -2.3f), Vector3.right),
                (PieceKind.Knight, true, new Vector3(2.4f, 0f, -2.8f), Vector3.right),
                (PieceKind.Pawn, true, new Vector3(1.6f, 0f, -0.9f), Vector3.right),
                (PieceKind.Queen, false, new Vector3(7.0f, 0f, -2.5f), Vector3.left));
            var king = P1;
            var enemy = bed.Dummy(3);
            yield return Wait(0.3f);
            Tap(king, "F");
            yield return Wait(0.5f);
            Add($"킹 호위 받은 아군: {Count(p => p.WardLeft > 0f)}명, 킹 자신 호위 {(king.WardLeft > 0f ? "있음 (틀림)" : "없음")}");
            // The other side's queen slashes down the line through the guarded allies and the king.
            AimFlat(enemy, Vector3.left);
            Tap(enemy);
            yield return Wait(0.12f);
            Click(enemy);
            yield return Wait(1.6f);
            foreach (var p in new[] { bed.Dummy(0), bed.Dummy(1), bed.Dummy(2), king })
                Add($"{Who(p)}: {StateOf(p)} — {p.LastSkillHit}");
            yield return Wait(0.8f);
        }

        int Count(Func<RagdollPawn, bool> f)
        {
            int n = 0;
            foreach (var p in RagdollPawn.All) if (p != null && f(p)) n++;
            return n;
        }

        /// <summary>R93: the king alone presses F; the bed's helper stands three of its pawns ahead and sets the other
        /// side's queen on them, who slashes once the guard is on.</summary>
        IEnumerator KingHelper()
        {
            yield return Stage(PieceKind.King, new Vector3(1.2f, 0f, -2.5f), Vector3.right);
            var king = P1;
            QueenHillSkillBed.HelpersForProbe = true;
            yield return Wait(0.3f);
            Tap(king, "F");
            yield return Wait(0.5f);
            Add($"도우미: 아군 폰 {Count(p => p != king && p.Team == king.Team && king.Team != Teams.None && (p.FeetPoint - king.FeetPoint).magnitude < 2.5f)}명, 호위 {Count(p => p.WardLeft > 0f)}명, 킹 자신 호위 {(king.WardLeft > 0f ? "있음 (틀림)" : "없음")}");
            yield return Wait(1.7f);
            for (int i = 0; i < 4; i++) Add($"{Who(bed.Dummy(i))}: {StateOf(bed.Dummy(i))} — {bed.Dummy(i).LastSkillHit}");
            Add($"{Who(king)}: {StateOf(king)} — {king.LastSkillHit}");
            yield return Wait(0.6f);
        }

        /// <summary>R94: a swap across the floor with an ally 8.4 m off (the range went from 6 to 9 m).</summary>
        IEnumerator RookFar()
        {
            Vector3 rookAt = new Vector3(8.2f, 0f, -3.5f), allyAt = new Vector3(-0.2f, 0f, -3.5f);
            yield return Stage(PieceKind.Rook, rookAt, Vector3.left, (PieceKind.Pawn, true, allyAt, Vector3.right));
            var rook = P1;
            var ally = bed.Dummy(0);
            yield return Wait(0.25f);
            AimFlat(rook, Vector3.left);
            Tap(rook, "F (조준)");
            yield return Wait(0.45f);
            Add($"먼 교대: {Who(rook.QhAimTarget)} {Vector3.Distance(rook.FeetPoint, ally.FeetPoint):0.0} m, {(rook.QhAimValid ? "바꿀 수 있음" : "안 됨 " + rook.QhAimWhy)}");
            Click(rook, "좌클릭 (교대 요청)");
            yield return Wait(0.45f);
            Say("상대 수락 (방장 판정)");
            yield return Wait(1.6f);
            Add($"룩 → x {rook.FeetPoint.x:0.0} (폰이 있던 곳 {allyAt.x:0.0}), 폰 → x {ally.FeetPoint.x:0.0} (룩이 있던 곳 {rookAt.x:0.0}), {StateOf(rook)}/{StateOf(ally)}");
            yield return Wait(0.4f);
        }

        /// <summary>R93: the rook on the floor east of the hill presses F; the bed's helper puts an ally of the given piece
        /// up on the tier ahead; the rook asks, it says yes, they swap (the arch half orange, half the ally's colour).</summary>
        IEnumerator RookUp(PieceKind[] partners)
        {
            float east = QueenHillLayout.HillCenter.x + QueenHillLayout.TierHalf(0);
            foreach (var kind in partners)
            {
                Vector3 rookAt = new Vector3(east + 2.4f, 0f, 6.0f);
                yield return Stage(PieceKind.Rook, rookAt, Vector3.left);
                var rook = P1;
                QueenHillSkillBed.HelpersForProbe = true;
                QueenHillSkillBed.NextRookPartner = kind;
                yield return Wait(0.25f);
                AimFlat(rook, Vector3.left);
                Tap(rook, "F (조준)");
                yield return Wait(0.45f);
                var ally = bed.Dummy(0);
                Add($"도우미: {Who(ally)} 높이 {ally.FeetPoint.y:0.0} m, 룩 조준 {Who(rook.QhAimTarget)} {(rook.QhAimValid ? "바꿀 수 있음" : "안 됨 " + rook.QhAimWhy)}");
                Click(rook, "좌클릭 (교대 요청)");
                yield return Wait(0.45f);
                Say("상대 수락 (방장 판정)");
                yield return Wait(1.6f);
                Add($"룩 → 높이 {rook.FeetPoint.y:0.0} m, {ChessPieces.Name(kind)} → 높이 {ally.FeetPoint.y:0.0} m, 둘 다 {StateOf(rook)}/{StateOf(ally)}");
                yield return Wait(0.4f);
            }
        }

        // ---------------------------------------------------------------- 2.A queen: one long slash, the wall stops it

        IEnumerator Queen()
        {
            yield return Stage(PieceKind.Queen, new Vector3(0.2f, 0f, 3f), Vector3.right,
                (PieceKind.Pawn, false, new Vector3(2.2f, 0f, 3.2f), Vector3.left),
                (PieceKind.Bishop, false, new Vector3(4.2f, 0f, 2.8f), Vector3.left),
                (PieceKind.Knight, false, new Vector3(7.6f, 0f, 3f), Vector3.left),
                (PieceKind.Rook, false, new Vector3(2.4f, 0f, 5.2f), Vector3.left));
            var q = P1;
            yield return Wait(0.3f);
            Tap(q, "F (조준)");
            // The aim swings onto the line, as a player sweeps the mouse.
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float a = Mathf.Lerp(-35f, 0f, Mathf.SmoothStep(0f, 1f, t / 0.6f));
                AimFlat(q, Quaternion.Euler(0f, a, 0f) * Vector3.right);
                yield return null;
            }
            AimFlat(q, Vector3.right);
            yield return Wait(0.1f);
            Click(q, "좌클릭");
            yield return Wait(1.6f);
            Add($"퀸 검격 길이 {q.SlashReach:0.0} m, 벽에 막힘 {(q.SlashBlocked ? "예" : "아니요")}");
            for (int i = 0; i < 4; i++) Add($"{Who(bed.Dummy(i))}: {StateOf(bed.Dummy(i))} — {bed.Dummy(i).LastSkillHit}");
            yield return Wait(0.8f);
        }

        // ---------------------------------------------------------------- 3.B rook: swap with the pawn (any ally)

        IEnumerator Rook()
        {
            Vector3 rookAt = new Vector3(-6.1f, QueenHillLayout.TierTop(1), 5f);
            Vector3 pawnAt = new Vector3(-2.9f, 0f, 4.6f);
            yield return Stage(PieceKind.Rook, rookAt, Vector3.right,
                (PieceKind.Pawn, true, pawnAt, Vector3.left),
                (PieceKind.Knight, false, new Vector3(-1.9f, 0f, 5.6f), Vector3.left));
            var rook = P1;
            var pawn = bed.Dummy(0);
            yield return Wait(0.3f);
            AimFlat(rook, pawn.Hips.position - rook.Hips.position);
            Tap(rook, "F (조준)");
            yield return Wait(0.5f);
            Add($"룩 조준: {Who(rook.QhAimTarget)} {(rook.QhAimValid ? "바꿀 수 있음" : "안 됨 " + rook.QhAimWhy)}");
            Click(rook, "좌클릭 (교대 요청)");
            yield return Wait(0.45f);
            Say("상대 수락 (방장 판정)");
            yield return Wait(1.8f);
            Add($"룩 → 높이 {rook.FeetPoint.y:0.0} m ({Vector3.Distance(rook.FeetPoint, pawnAt):0.0} m 떨어짐), 폰 → 높이 {pawn.FeetPoint.y:0.0} m ({Vector3.Distance(pawn.FeetPoint, rookAt):0.0} m 떨어짐), 둘 다 {StateOf(rook)}/{StateOf(pawn)}");
            yield return Wait(0.5f);
        }

        // ---------------------------------------------------------------- 4.B bishop: knock the climber off the wall

        IEnumerator Bishop()
        {
            float face = QueenHillLayout.HillCenter.x + QueenHillLayout.TierHalf(0);   // the hill's east face
            Vector3 climbAt = new Vector3(face + 0.45f, 0f, 3.6f);
            yield return Stage(PieceKind.Bishop, new Vector3(1.3f, 0f, 3.2f), Vector3.left,
                (PieceKind.Pawn, false, climbAt, Vector3.left),
                (PieceKind.Pawn, false, new Vector3(face - 1.0f, QueenHillLayout.TierTop(0), 6.4f), Vector3.right));
            var bishop = P1;
            var climber = bed.Dummy(0);
            // The enemy pawn walks into the wall holding grab: it climbs.
            Of(climber).move = Vector3.left;
            Of(climber).grab = true;
            Of(climber).aim = Vector3.left;
            float t = 0f;
            while (!climber.Climbing && t < 1.5f) { t += Time.deltaTime; yield return null; }
            Add($"적 폰 벽 오르기: {(climber.Climbing ? "오르는 중" : "못 오름")}");
            yield return Wait(0.15f);
            Of(climber).move = Vector3.zero;   // holding on, mid-wall
            Tap(bishop, "F (떠오름)");
            yield return Wait(0.5f);
            for (float a = 0f; a < 0.7f; a += Time.deltaTime)
            {
                AimAt(bishop, new Vector3(face + 0.35f, 0f, climber.Hips.position.z));
                yield return null;
            }
            Add($"비숍 높이 {bishop.FeetPoint.y:0.0}~{bishop.Hips.position.y - bishop.standHeight:0.0} m, 조준 {(bishop.QhAimValid ? "됨" : bishop.QhAimWhy)}");
            Click(bishop, "좌클릭 (발사)");
            Of(climber).grab = true;
            yield return Wait(1.2f);
            Add($"맞은 적 폰: {StateOf(climber)}, 벽 {(climber.Climbing ? "여전히 매달림" : "떨어짐")}, 높이 {climber.Hips.position.y:0.0} m");
            Of(climber).grab = false;
            yield return Wait(1.6f);
            Add($"비숍 착지: {(bishop.SkillStage == SkillStage.None ? "끝" : bishop.SkillDetail)}");
        }

        // ---------------------------------------------------------------- 5.B knight: one tier up, flattened off the ledge

        IEnumerator Knight()
        {
            float east = QueenHillLayout.HillCenter.x + QueenHillLayout.TierHalf(0);
            Vector3 enemyAt = new Vector3(east - 0.45f, QueenHillLayout.TierTop(0), 7.4f);
            yield return Stage(PieceKind.Knight, new Vector3(east + 3.2f, 0f, 7.1f), Vector3.left,
                (PieceKind.Pawn, false, enemyAt, Vector3.right));
            var knight = P1;
            var enemy = bed.Dummy(0);
            yield return Wait(0.3f);
            // First at the second tier: too high (grey), then down at the first tier beside the enemy.
            Vector3 high = new Vector3(east - 2.2f, QueenHillLayout.TierTop(1), 6.6f);
            AimAt(knight, high);
            Tap(knight, "F (조준)");
            yield return Wait(0.15f);
            Add($"2층 조준: {(knight.QhAimValid ? "됨 (틀림)" : "안 됨 — " + knight.QhAimWhy)}");
            yield return Wait(0.5f);
            // R94: 1.24 m off the enemy the circle does not catch it (knightSnap 1.15 m), 0.7 m off it does.
            Vector3 near = new Vector3(enemyAt.x - 0.3f, QueenHillLayout.TierTop(0), enemyAt.z - 1.2f);
            Vector3 spot = new Vector3(east - 1.05f, QueenHillLayout.TierTop(0), 7.0f);
            for (float a = 0f; a < 0.35f; a += Time.deltaTime)
            {
                AimAt(knight, Vector3.Lerp(high, near, Mathf.SmoothStep(0f, 1f, a / 0.35f)));
                yield return null;
            }
            AimAt(knight, near);
            yield return Wait(0.3f);
            Add($"적에서 {Vector3.Distance(near, enemyAt):0.00} m 조준: 머리 자동 조준 {(knight.QhAimTarget == enemy ? "됨 (틀림)" : "안 됨")} (범위 {bed.skills.knightSnap:0.00} m)");
            for (float a = 0f; a < 0.3f; a += Time.deltaTime)
            {
                AimAt(knight, Vector3.Lerp(near, spot, Mathf.SmoothStep(0f, 1f, a / 0.3f)));
                yield return null;
            }
            AimAt(knight, spot);
            yield return Wait(0.2f);
            Add($"1층 조준: {(knight.QhAimValid ? "됨" : "안 됨 — " + knight.QhAimWhy)} ({knight.QhAimPoint.y:0.0} m), "
                + $"적 머리 자동 조준 {(knight.QhAimTarget == enemy ? "됨" : "안 됨")} (조준점과 적 {Vector3.Distance(spot, enemyAt):0.0} m)");
            Click(knight, "좌클릭 (도약)");
            int downs = enemy.Knockdowns;
            float apex = knight.Hips.position.y;
            for (float t = 0f; t < 2.1f; t += Time.deltaTime)
            {
                apex = Mathf.Max(apex, knight.Hips.position.y);
                yield return null;
            }
            Add($"나이트 골반 최고 {apex - QueenHillLayout.TierTop(0):+0.0;-0.0} m (1층 바닥 기준), 착지 높이 {knight.FeetPoint.y:0.0} m, 적 폰: {enemy.LastSkillHit}, 넘어짐 {enemy.Knockdowns - downs}번, 지금 높이 {enemy.FeetPoint.y:0.0} m");
            yield return Wait(0.9f);
        }

        // ---------------------------------------------------------------- 6.B pawn: crouch and slip the rook's charge

        IEnumerator PawnDodge()
        {
            yield return Stage(PieceKind.Pawn, new Vector3(1.5f, 0f, -3.5f), Vector3.right,
                (PieceKind.Rook, false, new Vector3(7.6f, 0f, -3.5f), Vector3.left));
            var pawn = P1;
            var rook = bed.Dummy(0);
            // The other side's rook charges with the Pawn Rush charge (a straight line, the threat to slip).
            QueenHillSkillBed.PawnRushOnly.Add(rook);
            yield return null;
            yield return Wait(0.25f);
            AimFlat(rook, Vector3.left);
            Tap(rook);
            yield return Wait(0.1f);
            Click(rook);
            yield return Wait(0.18f);
            Of(pawn).move = Vector3.back;   // to the side, off the line
            Tap(pawn, "F (몸 낮춰 옆으로)");
            yield return Wait(0.05f);
            Of(pawn).move = Vector3.zero;
            yield return Wait(1.3f);
            Add($"폰: {StateOf(pawn)} (z {pawn.Hips.position.z:0.0}), 룩 돌진 {(pawn.State == PawnState.Ragdoll ? "맞음" : "빗나감")}");
            yield return Wait(0.8f);
        }

        IEnumerator PawnSqueeze()
        {
            yield return Stage(PieceKind.Pawn, new Vector3(-1.2f, 0f, -5f), Vector3.right,
                (PieceKind.Rook, false, new Vector3(0.9f, 0f, -4.48f), Vector3.left),
                (PieceKind.Bishop, false, new Vector3(0.9f, 0f, -5.52f), Vector3.left));
            var pawn = P1;
            yield return Wait(0.3f);
            Of(pawn).move = Vector3.right;
            Tap(pawn, "F");
            yield return Wait(0.05f);
            Of(pawn).move = Vector3.zero;
            yield return Wait(1.2f);
            Add($"폰 비집고 지나감: x {pawn.Hips.position.x:0.0} (틈 x 0.9), {StateOf(pawn)}; 비킨 적 {StateOf(bed.Dummy(0))}/{StateOf(bed.Dummy(1))}");
            yield return Wait(0.6f);
        }
    }
}
