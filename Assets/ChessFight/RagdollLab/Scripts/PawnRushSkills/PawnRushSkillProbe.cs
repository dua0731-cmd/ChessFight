using System;
using System.Collections;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Scripted runs of the Pawn Rush skills in the skill test scene, for checking them without a keyboard
    /// (an AI driving Unity through the editor bridge, or a person who wants numbers). It moves P1 and the
    /// dummies, presses the skill key in code and writes what it measured to <see cref="Results"/> and the
    /// bed's log. Start one from code while playing: <c>PawnRushSkillProbe.Run("pawn")</c>; names are listed
    /// in <see cref="Names"/>. Measuring is not checking: a person still has to see it in Unity.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class PawnRushSkillProbe : MonoBehaviour
    {
        public static readonly string[] Names = { "jump", "pawn", "pawn-angles", "pawn-help", "rook", "rook-wall", "rook-barricade", "queen", "knight", "knight-turn", "knight-stomp", "bishop", "all" };
        public static string Status { get; private set; } = "idle";
        public static readonly List<string> Results = new List<string>();

        LabGame game;
        PawnRushSkillBed bed;
        bool driving;
        Vector3 move, aim = Vector3.forward;
        bool sprint, jumpEdge, skillHold;
        RagdollPawn walker;
        Vector3 walkerMove;

        public static string Run(string name)
        {
            var bed = FindFirstObjectByType<PawnRushSkillBed>();
            if (bed == null) return "no PawnRushSkillBed (open PawnRush_SkillTest and press Play)";
            var probe = bed.GetComponent<PawnRushSkillProbe>();
            if (probe == null) probe = bed.gameObject.AddComponent<PawnRushSkillProbe>();
            probe.StopAllCoroutines();
            Results.Clear();
            probe.StartCoroutine(probe.Main(name));
            return Status = "running " + name;
        }

        public static string Report() => Status + "\n" + string.Join("\n", Results);

        void Update()
        {
            if (!driving) return;
            var p1 = P1;
            if (p1 != null)
            {
                p1.SetInput(new PawnInput { move = move, aim = aim, sprint = sprint, jump = jumpEdge });
                p1.SetSkillInput(false, skillHold);
                jumpEdge = false;
            }
            if (walker != null) walker.SetInput(new PawnInput { move = walkerMove, aim = walkerMove });
        }

        RagdollPawn P1 => game != null && game.players.Length > 0 ? game.players[0].pawn : null;

        IEnumerator Main(string name)
        {
            game = GetComponent<LabGame>();
            bed = GetComponent<PawnRushSkillBed>();
            yield return null;
            if (P1 == null) { Status = "no P1"; yield break; }
            driving = true;
            string[] list = name == "all" ? Array.FindAll(Names, n => n != "all") : new[] { name };
            foreach (var n in list)
            {
                Status = "running " + n;
                IEnumerator run = n switch
                {
                    "jump" => Jump(),
                    "pawn" => PawnSteps(),
                    "pawn-angles" => PawnAngles(),
                    "pawn-help" => PawnHelp(),
                    "rook" => Rook(),
                    "rook-wall" => RookWall(),
                    "rook-barricade" => RookBarricade(),
                    "queen" => Queen(),
                    "knight" => Knight(false),
                    "knight-turn" => Knight(true),
                    "knight-stomp" => KnightStomp(),
                    "bishop" => Bishop(),
                    _ => null,
                };
                if (run == null) { Add($"모르는 시험: {n}"); continue; }
                yield return run;
            }
            Stop();
            Status = "done " + name;
        }

        void Stop()
        {
            move = Vector3.zero;
            sprint = skillHold = false;
            walker = null;
            walkerMove = Vector3.zero;
            driving = false;
        }

        static void Add(string line)
        {
            Results.Add(line);
            PawnRushSkillBed.Report("[시험] " + line);
        }

        // ---------------------------------------------------------------- set-up helpers

        void Place(RagdollPawn p, Vector3 ground, Vector3 face)
        {
            p.Teleport(ground + Vector3.up * (p.standHeight + 0.02f), face);
        }

        IEnumerator Ready(PieceKind piece, Vector3 ground, Vector3 face, int dummies)
        {
            Stop();
            driving = true;
            var p1 = P1;
            if (p1.Piece != piece) p1.SetPiece(piece);
            p1.ResetSkill();
            aim = face;
            Place(p1, ground, face);
            while (game.dummies.Count < dummies) game.AddDummy();
            for (int i = 0; i < game.dummies.Count; i++)
            {
                var d = game.dummies[i];
                d.Team = Teams.None;
                if (d.Piece != PieceKind.Pawn) d.SetPiece(PieceKind.Pawn);
                d.ResetSkill();
                if (d.skin != null) d.skin.sharedMaterial = game.dummyMaterial;
                Place(d, new Vector3(12f + i * 1.5f, 0f, 13f), Vector3.back);   // parked out of the way
            }
            yield return new WaitForSeconds(0.8f);
        }

        void Tap() => P1.SetSkillInput(true, false);

        static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

        static string StateText(RagdollPawn p) =>
            p.State == PawnState.Ragdoll ? "넘어짐" : p.Staggered ? "휘청" : p.State == PawnState.GettingUp ? "일어나는 중" : "서 있음";

        IEnumerator Until(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout)
            {
                t += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }
        }

        // ---------------------------------------------------------------- the runs

        IEnumerator Jump()
        {
            foreach (bool fast in new[] { false, true })
            {
                yield return Ready(PieceKind.Pawn, new Vector3(-14f, 0f, -12f), Vector3.right, 1);
                var p1 = P1;
                move = Vector3.right;
                sprint = fast;
                yield return new WaitForSeconds(1.3f);
                float speed = p1.HorizontalSpeed;
                jumpEdge = true;
                yield return new WaitForFixedUpdate();
                Vector3 from = p1.Hips.position;
                yield return Until(() => !p1.Grounded, 0.4f);
                float peak = from.y;
                yield return Until(() => { peak = Mathf.Max(peak, p1.Hips.position.y); return p1.Grounded; }, 3f);
                Add($"{(fast ? "전력질주" : "달리기")} 점프: 속도 {speed:0.0} m/s → 멀리 {FlatDistance(from, p1.Hips.position):0.00} m, 골반 최고 +{peak - from.y:0.00} m");
                move = Vector3.zero;
                sprint = false;
                yield return new WaitForSeconds(0.6f);
            }
        }

        IEnumerator PawnSteps()
        {
            yield return Ready(PieceKind.Pawn, new Vector3(0f, 0f, -12f), Vector3.forward, 1);
            var p1 = P1;
            Vector3 start = p1.Hips.position;
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.Link, 1f);
            Vector3 one = p1.Hips.position;
            yield return new WaitForSeconds(0.15f);
            aim = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;   // the second step turns 45 degrees
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.None, 1f);
            Vector3 two = p1.Hips.position;
            Add($"폰 두 걸음: 첫 걸음 {FlatDistance(start, one):0.00} m, 두 번째 {FlatDistance(one, two):0.00} m (45° 꺾음), 쿨 {p1.SkillCooldown:0.0}초");
            yield return new WaitForSeconds(1f);
            p1.ResetSkill();
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.None && p1.SkillCooldown > 0f, 2f);
            Add($"폰 한 걸음만: 쿨 {p1.SkillCooldown:0.0}초 (기대 3.5)");
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator PawnAngles()
        {
            foreach (float angle in new[] { 0f, 45f, 90f, 180f })
            {
                yield return Ready(PieceKind.Pawn, new Vector3(0f, 0f, -8.3f), Vector3.forward, 1);
                var d = game.dummies[0];
                Place(d, new Vector3(0f, 0f, -6f), Quaternion.Euler(0f, angle, 0f) * Vector3.back);
                yield return new WaitForSeconds(0.6f);
                Vector3 before = d.Hips.position;
                Tap();
                yield return new WaitForSeconds(0.45f);
                Add($"폰 → 상대가 {angle:0}° 돌아선 더미: {StateText(d)}, 밀려난 거리 {FlatDistance(before, d.Hips.position):0.00} m");
                yield return new WaitForSeconds(0.6f);
            }
        }

        IEnumerator PawnHelp()
        {
            yield return Ready(PieceKind.Pawn, new Vector3(0f, 0f, -8.3f), Vector3.forward, 1);
            var p1 = P1;
            var d = game.dummies[0];
            d.Team = p1.Team;
            Place(d, new Vector3(0f, 0f, -6f), Vector3.back);
            yield return new WaitForSeconds(0.5f);
            d.Knockdown("시험: 넘어뜨리기", 4f);
            yield return new WaitForSeconds(1f);
            string before = StateText(d);
            Tap();
            yield return Until(() => d.State != PawnState.Ragdoll, 0.6f);
            string after = StateText(d);
            yield return Until(() => p1.SkillStage == SkillStage.None, 1.5f);
            Add($"폰 부축: 팀원 {before} → {after}, 진군 남은 시간 나 {p1.HasteLeft:0.0}초 / 팀원 {d.HasteLeft:0.0}초, 쿨 {p1.SkillCooldown:0.0}초 (한 걸음 3.5 − 1.5 = 2.0 기대)");
            yield return Until(() => d.State == PawnState.Active, 2f);
            Add($"  팀원 일어난 뒤 기상 보호 {d.GetUpGuardLeft:0.0}초 (기대 1.6)");
            d.Team = Teams.None;
        }

        IEnumerator Rook()
        {
            yield return Ready(PieceKind.Rook, new Vector3(0f, 0f, -10f), Vector3.forward, 4);
            float[] z = { -7.6f, -6.6f, -5.6f, -4.6f };
            for (int i = 0; i < 4; i++) Place(game.dummies[i], new Vector3(i % 2 == 0 ? 0.2f : -0.2f, 0f, z[i]), Vector3.back);
            yield return new WaitForSeconds(0.7f);
            var p1 = P1;
            Vector3 start = p1.Hips.position;
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.Active, 1.5f);
            Vector3 charge = p1.Hips.position;
            yield return Until(() => p1.SkillStage != SkillStage.Active, 1.5f);
            int down = 0;
            foreach (var d in game.dummies) if (d.State == PawnState.Ragdoll) down++;
            Add($"룩 돌진: 예고 동안 움직인 거리 {FlatDistance(start, charge):0.00} m, 돌진 거리 {FlatDistance(charge, p1.Hips.position):0.00} m, 넘어진 더미 {down}/4 (기대 3, 4번째는 밀림)");
            yield return Until(() => p1.SkillStage == SkillStage.None, 1.5f);
            Add($"  룩 쿨 {p1.SkillCooldown:0.0}초 (기대 7)");
        }

        IEnumerator RookWall()
        {
            // [3] the walls at x 20..25 (the 3 m one at z 0).
            yield return Ready(PieceKind.Rook, new Vector3(15.5f, 0f, 0f), Vector3.right, 1);
            var p1 = P1;
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.Active, 1.5f);
            yield return Until(() => p1.SkillStage != SkillStage.Active, 1.5f);
            Add($"룩 → 벽: 멈춘 곳 x {p1.Hips.position.x:0.00} (벽 앞면 20), 상태 {StateText(p1)}, 휘청 남은 {p1.StaggerLeft:0.00}초");
            yield return new WaitForSeconds(1.2f);
        }

        IEnumerator RookBarricade()
        {
            var barricade = FindFirstObjectByType<SkillBarricade>();
            Vector3 at = barricade != null ? barricade.transform.position : bed.barricadeAt;
            yield return Ready(PieceKind.Rook, at + new Vector3(4.6f, 0f, 0f), Vector3.left, 1);
            var p1 = P1;
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.Active, 1.5f);
            yield return Until(() => p1.SkillStage != SkillStage.Active, 1.5f);
            Add($"룩 → 바리케이드: 바리케이드 {(barricade != null && !barricade.Standing ? "부서짐" : "그대로")}, 룩 x {p1.Hips.position.x:0.00} (바리케이드 x {at.x:0.0}, 지나갔으면 더 작음)");
            yield return new WaitForSeconds(0.8f);
        }

        IEnumerator Queen()
        {
            yield return Ready(PieceKind.Queen, new Vector3(0f, 0f, -8f), Vector3.forward, 3);
            Vector3 c = new Vector3(0f, 0f, -8f);
            Vector3[] spots = { c + Vector3.right * 1.0f, c + Vector3.left * 2.2f, c + Vector3.forward * 3.6f };
            for (int i = 0; i < 3; i++) Place(game.dummies[i], spots[i], -(spots[i] - c).normalized);
            yield return new WaitForSeconds(0.7f);
            var before = new Vector3[3];
            for (int i = 0; i < 3; i++) before[i] = game.dummies[i].Hips.position;
            var p1 = P1;
            Vector3 queenAt = p1.Hips.position;
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.Recovery, 1f);
            Vector3 queenMid = p1.Hips.position;
            yield return new WaitForSeconds(0.3f);
            string[] label = { "1.0 m", "2.2 m", "3.6 m" };
            for (int i = 0; i < 3; i++)
                Add($"퀸 → 더미 {label[i]}: {StateText(game.dummies[i])}, 밀려난 거리 {FlatDistance(before[i], game.dummies[i].Hips.position):0.00} m");
            Add($"  퀸이 예고 동안 움직인 거리 {FlatDistance(queenAt, queenMid):0.00} m (제자리 기대)");
            yield return new WaitForSeconds(1f);
        }

        IEnumerator Knight(bool turn)
        {
            yield return Ready(PieceKind.Knight, new Vector3(0f, 0f, -12f), Vector3.forward, 1);
            var p1 = P1;
            Vector3 start = p1.Hips.position;
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.Active, 1f);
            Vector3 takeoff = p1.Hips.position;
            float peak = takeoff.y, air = 0f;
            bool turned = false;
            while (p1.SkillStage == SkillStage.Active && air < 3f)
            {
                peak = Mathf.Max(peak, p1.Hips.position.y);
                air += Time.fixedDeltaTime;
                if (turn && !turned && air >= 0.35f)
                {
                    move = Vector3.right;
                    Tap();
                    turned = true;
                }
                yield return new WaitForFixedUpdate();
            }
            Vector3 land = p1.Hips.position;
            move = Vector3.zero;
            if (!turn)
                Add($"나이트 도약: 골반 최고 +{peak - takeoff.y:0.00} m (기대 1.6), 비거리 {FlatDistance(takeoff, land):0.00} m (기대 약 5.5), 체공 {air:0.00}초 (기대 약 1.14)");
            else
            {
                Vector3 d = land - takeoff;
                Add($"나이트 꺾기: 착지 앞 {d.z:0.00} m · 옆 {d.x:0.00} m (0.35초에 오른쪽으로 꺾음), 체공 {air:0.00}초");
            }
            yield return Until(() => p1.SkillStage == SkillStage.None, 1f);
            Add($"  착지 후딜 뒤 쿨 {p1.SkillCooldown:0.0}초 (기대 7)");
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator KnightStomp()
        {
            yield return Ready(PieceKind.Knight, new Vector3(0f, 0f, -12f), Vector3.forward, 2);
            Place(game.dummies[0], new Vector3(0f, 0f, -7.1f), Vector3.back);
            Place(game.dummies[1], new Vector3(1.2f, 0f, -5.4f), Vector3.back);
            yield return new WaitForSeconds(0.7f);
            var p1 = P1;
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.Recovery || p1.SkillStage == SkillStage.None, 3f);
            yield return new WaitForSeconds(0.1f);
            Add($"나이트 밟기: 더미1(착지 길 위) {StateText(game.dummies[0])} · {game.dummies[0].LastSkillHit} / 더미2(옆) {StateText(game.dummies[1])} · {game.dummies[1].LastSkillHit}");
            yield return new WaitForSeconds(1f);
        }

        IEnumerator Bishop()
        {
            yield return Ready(PieceKind.Bishop, new Vector3(0f, 0f, -11f), Vector3.forward, 1);
            var p1 = P1;
            Vector3 head = p1.bodies[(int)BodyId.Head].position;
            aim = (new Vector3(0f, 0f, -6f) - head).normalized;
            skillHold = true;
            Tap();
            yield return new WaitForSeconds(0.4f);
            skillHold = false;
            yield return new WaitForSeconds(0.5f);
            var wire = FindFirstObjectByType<SkillTripwire>();
            Add($"비숍 밧줄: {(wire != null ? $"깔림 (x {wire.transform.position.x:0.0})" : "안 깔림")}, 쿨 {p1.SkillCooldown:0.0}초");
            // An enemy runs across it, along x.
            var d = game.dummies[0];
            Place(d, new Vector3(-3.5f, 0f, -6f), Vector3.right);
            yield return new WaitForSeconds(0.5f);
            walker = d;
            walkerMove = Vector3.right;
            yield return Until(() => d.State == PawnState.Ragdoll, 2.5f);
            walker = null;
            Add($"  달려간 적 더미: {StateText(d)} · {d.LastSkillHit}");
            yield return new WaitForSeconds(1.5f);
            // A rook runs through it: it snaps.
            d.SetPiece(PieceKind.Rook);
            Place(d, new Vector3(3.5f, 0f, -6f), Vector3.left);
            yield return new WaitForSeconds(0.5f);
            walker = d;
            walkerMove = Vector3.left;
            yield return new WaitForSeconds(1.6f);
            walker = null;
            wire = FindFirstObjectByType<SkillTripwire>();
            Add($"  룩 더미가 지나감: {StateText(d)}, 밧줄 {(wire == null ? "끊어짐" : "남아 있음")}");
            d.SetPiece(PieceKind.Pawn);
            yield return new WaitForSeconds(0.5f);
        }
    }
}
