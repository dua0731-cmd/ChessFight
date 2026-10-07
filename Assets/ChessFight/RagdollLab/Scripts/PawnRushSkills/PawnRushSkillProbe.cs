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
        public static readonly string[] Names = { "jump", "pawn", "pawn-angles", "pawn-help", "rook", "rook-free", "rook-wall", "rook-barricade", "rook-cluster", "queen", "knight", "knight-turn", "knight-stomp", "knight-land", "bishop", "bishop-trip", "all" };
        public static string Status { get; private set; } = "idle";
        /// <summary>The current run has set its pieces down and settled them (the film records from here).</summary>
        public static bool Staged { get; private set; }
        /// <summary>The last key the probe pressed, as a player would read it (the film shows it); the serial
        /// counts presses so the same key twice shows twice.</summary>
        public static string Hint { get; private set; } = "";
        public static int HintSerial { get; private set; }
        public static readonly List<string> Results = new List<string>();

        LabGame game;
        PawnRushSkillBed bed;
        bool driving;
        Vector3 move, aim = Vector3.forward;
        bool sprint, jumpEdge, tapPending, clickPending;
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

        /// <summary>Stop the clock (time scale 0, frames still drawn) the first time P1 is this far into this
        /// stage, to look at a telegraph; set Time.timeScale back to 1 to go on.</summary>
        public static void FreezeAt(SkillStage stage, float seconds)
        {
            freezeStage = stage;
            freezeAfter = seconds;
        }

        static SkillStage freezeStage;
        static float freezeAfter;

        void LateUpdate()
        {
            var p1 = P1;
            if (freezeStage == SkillStage.None || p1 == null) return;
            if (p1.SkillStage != freezeStage || p1.SkillStageTime < freezeAfter) return;
            Time.timeScale = 0f;
            freezeStage = SkillStage.None;
        }

        void Update()
        {
            if (!driving) return;
            var p1 = P1;
            if (p1 != null)
            {
                // The left click is the dive's button (shove): an aiming rook or bishop takes it as "go".
                p1.SetInput(new PawnInput { move = move, aim = aim, sprint = sprint, jump = jumpEdge, shove = clickPending });
                // The press goes in with this frame's aim and move, never ahead of them.
                p1.SetSkillInput(tapPending);
                jumpEdge = tapPending = clickPending = false;
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
            float testCooldown = bed.skills.testCooldown;
            bed.skills.testCooldown = 0f;   // the probe checks the design cooldowns
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
                    "rook-free" => RookFree(),
                    "rook-wall" => RookWall(),
                    "rook-barricade" => RookBarricade(),
                    "rook-cluster" => RookCluster(),
                    "queen" => Queen(),
                    "knight" => Knight(false),
                    "knight-turn" => Knight(true),
                    "knight-stomp" => KnightStomp(),
                    "knight-land" => KnightLand(),
                    "bishop" => Bishop(),
                    "bishop-trip" => BishopTrip(),
                    _ => null,
                };
                if (run == null) { Add($"모르는 시험: {n}"); continue; }
                Staged = false;
                yield return run;
                Staged = false;
            }
            Stop();
            bed.skills.testCooldown = testCooldown;
            Status = "done " + name;
        }

        void Stop()
        {
            move = Vector3.zero;
            sprint = tapPending = clickPending = false;
            StopWalker();
            driving = false;
        }

        /// <summary>A dummy keeps its last input until it gets another: stop it explicitly.</summary>
        void StopWalker()
        {
            if (walker != null) walker.SetInput(new PawnInput());
            walker = null;
            walkerMove = Vector3.zero;
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
            // The other local players too: an enemy close by changes what a knight's second press does.
            for (int i = 1; i < game.players.Length; i++)
                if (game.players[i].pawn != null) Place(game.players[i].pawn, new Vector3(12f, 0f, 9f + i * 1.5f), Vector3.back);
            yield return new WaitForSeconds(0.8f);
            Staged = true;
        }

        void Tap(string hint = "F")
        {
            tapPending = true;
            Say(hint);
        }

        void Click()
        {
            clickPending = true;
            Say("좌클릭");
        }

        static void Say(string hint)
        {
            Hint = hint;
            HintSerial++;
        }

        /// <summary>The rook's and the bishop's aim: the key, a moment with the mouse still, the left click.</summary>
        IEnumerator AimThenClick(float seconds)
        {
            Tap();
            yield return new WaitForSeconds(seconds);
            Click();
        }

        /// <summary>Swing the aim (the mouse) about <paramref name="center"/>: out to one side, the other, back.</summary>
        IEnumerator Sweep(Vector3 center, float degrees, float seconds)
        {
            Say("마우스로 방향");
            float t = 0f;
            while (t < seconds)
            {
                aim = Quaternion.Euler(0f, degrees * Mathf.Sin(t / seconds * Mathf.PI * 2f), 0f) * center;
                yield return null;
                t += Time.deltaTime;
            }
            aim = center;
        }

        /// <summary>Move the aim (the mouse) from the head through these floor points, evenly.</summary>
        IEnumerator AimThrough(Vector3[] points, float seconds)
        {
            Say("마우스로 위치");
            var p1 = P1;
            float t = 0f;
            while (t < seconds)
            {
                float u = t / seconds * (points.Length - 1);
                int i = Mathf.Min(points.Length - 2, Mathf.FloorToInt(u));
                Vector3 at = Vector3.Lerp(points[i], points[i + 1], Mathf.SmoothStep(0f, 1f, u - i));
                aim = (at - p1.bodies[(int)BodyId.Head].position).normalized;
                yield return null;
                t += Time.deltaTime;
            }
            aim = (points[points.Length - 1] - p1.bodies[(int)BodyId.Head].position).normalized;
        }

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
                Vector3 from = p1.Hips.position;
                jumpEdge = true;
                yield return Until(() => p1.Hips.linearVelocity.y > 1.5f, 0.5f);
                float peak = from.y, air = 0f;
                // Landed = the hips back down to their take-off height after the top of the arc.
                yield return Until(() =>
                {
                    air += Time.fixedDeltaTime;
                    peak = Mathf.Max(peak, p1.Hips.position.y);
                    return air > 0.15f && p1.Hips.linearVelocity.y < 0f && p1.Hips.position.y <= from.y + 0.01f;
                }, 3f);
                Add($"{(fast ? "전력질주" : "달리기")} 점프: 속도 {speed:0.0} m/s → 멀리 {FlatDistance(from, p1.Hips.position):0.00} m (골반이 출발 높이로 돌아올 때까지), 골반 최고 +{peak - from.y:0.00} m, 체공 {air:0.00}초");
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
            float haste = p1.HasteLeft;
            yield return Until(() => d.State == PawnState.Active, 2f);
            float guard = d.GetUpGuardLeft;
            yield return Until(() => p1.SkillStage == SkillStage.None, 1.5f);
            Add($"폰 부축: 팀원 {before} → {after}, 진군 {haste:0.0}초 (나·팀원 모두), 일어선 팀원 기상 보호 {guard:0.0}초 (기대 1.6), 쿨 {p1.SkillCooldown:0.0}초 (한 걸음 3.5 − 1.5 = 2.0 기대)");
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
            yield return AimThenClick(0.4f);
            yield return Until(() => p1.SkillStage == SkillStage.Active, 1.5f);
            Vector3 charge = p1.Hips.position;
            yield return Until(() => p1.SkillStage != SkillStage.Active, 1.5f);
            int down = 0;
            foreach (var d in game.dummies) if (d.State == PawnState.Ragdoll) down++;
            Add($"룩 돌진: 예고 동안 움직인 거리 {FlatDistance(start, charge):0.00} m, 돌진 거리 {FlatDistance(charge, p1.Hips.position):0.00} m, 넘어진 더미 {down}/4 (기대 3, 4번째는 밀림)");
            yield return Until(() => p1.SkillStage == SkillStage.None, 1.5f);
            Add($"  룩 쿨 {p1.SkillCooldown:0.0}초 (기대 7)");
        }

        /// <summary>The bed's 2 x 2 lump (key V) in front of the rook, then the charge, with the bed's test cooldown.</summary>
        IEnumerator RookCluster()
        {
            yield return Ready(PieceKind.Rook, new Vector3(-6f, 0f, -12f), Vector3.forward, 4);
            var p1 = P1;
            bed.ClusterDummies(p1);
            yield return new WaitForSeconds(0.8f);
            Tap();
            yield return new WaitForSeconds(0.25f);
            yield return Sweep(Vector3.forward, 35f, 1.6f);
            yield return new WaitForSeconds(0.2f);
            Click();
            yield return Until(() => p1.SkillStage == SkillStage.Active, 1.5f);
            Vector3 from = p1.Hips.position;
            yield return Until(() => p1.SkillStage != SkillStage.Active, 1.5f);
            Vector3 end = p1.Hips.position;
            int down = 0, staggered = 0;
            for (int i = 0; i < 4; i++)
            {
                var d = game.dummies[i];
                if (d.State == PawnState.Ragdoll) down++;
                else if (d.Staggered) staggered++;
            }
            yield return new WaitForSeconds(0.6f);
            float spread = 0f;
            for (int i = 0; i < 4; i++) spread = Mathf.Max(spread, FlatDistance(game.dummies[i].Hips.position, p1.Hips.position));
            Add($"룩 4명 뭉치 돌진: 돌진 {FlatDistance(from, end):0.00} m, 돌진이 끝날 때 넘어짐 {down}/4 (기대 3), 휘청 {staggered}, 0.6초 뒤 가장 멀리 날아간 더미 {spread:0.0} m (룩에서)");
        }

        IEnumerator RookFree()
        {
            yield return Ready(PieceKind.Rook, new Vector3(-6f, 0f, -12f), Vector3.forward, 1);
            var p1 = P1;
            yield return AimThenClick(0.4f);
            yield return Until(() => p1.SkillStage == SkillStage.Active, 1.5f);
            Vector3 from = p1.Hips.position;
            float t = 0f, top = 0f;
            yield return Until(() => { t += Time.fixedDeltaTime; top = Mathf.Max(top, p1.HorizontalSpeed); return p1.SkillStage != SkillStage.Active; }, 2f);
            Vector3 end = p1.Hips.position;
            yield return Until(() => p1.SkillStage == SkillStage.None, 1.5f);
            Add($"룩 빈 바닥 돌진: {FlatDistance(from, end):0.00} m (기대 5.95), 돌진 {t:0.00}초 (기대 0.7), 최고 {top:0.0} m/s (기대 8.5), 후딜이 끝날 때까지 미끄러져 총 {FlatDistance(from, p1.Hips.position):0.00} m");
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator RookWall()
        {
            // [3] the walls at x 20..25 (the 3 m one at z 0).
            yield return Ready(PieceKind.Rook, new Vector3(15.5f, 0f, 0f), Vector3.right, 1);
            var p1 = P1;
            yield return AimThenClick(0.4f);
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
            yield return AimThenClick(0.4f);
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
            Vector3 land = takeoff;
            bool down = false;
            while (p1.SkillStage == SkillStage.Active && air < 3f)
            {
                peak = Mathf.Max(peak, p1.Hips.position.y);
                air += Time.fixedDeltaTime;
                if (!down && air > 0.2f && p1.Hips.linearVelocity.y < 0f && p1.Hips.position.y <= takeoff.y + 0.01f)
                {
                    land = p1.Hips.position;
                    down = true;
                }
                if (turn && !turned && air >= 0.3f)
                {
                    move = Vector3.right;   // held first: the key is read in the step that takes the press
                    yield return null;
                    yield return null;
                    Tap("F + 오른쪽(D)");
                    turned = true;
                }
                yield return new WaitForFixedUpdate();
            }
            if (!down) land = p1.Hips.position;
            move = Vector3.zero;
            if (!turn)
                Add($"나이트 도약: 골반 최고 +{peak - takeoff.y:0.00} m (기대 1.6), 비거리 {FlatDistance(takeoff, land):0.00} m (골반이 출발 높이로 돌아올 때, 기대 약 5.5), 착지 판정까지 {air:0.00}초 (기대 약 1.14)");
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
            // An enemy off to the side of the leap's line: in the air it is found (within knightLockRange), the
            // second F flies the knight onto its head, and it lands beside it.
            yield return Ready(PieceKind.Knight, new Vector3(0f, 0f, -12f), Vector3.forward, 1);
            var d = game.dummies[0];
            Place(d, new Vector3(2f, 0f, -7.6f), Vector3.back);
            yield return new WaitForSeconds(0.7f);
            var p1 = P1;
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.Active, 1f);
            yield return Until(() => p1.KnightMarked != null || p1.SkillStage != SkillStage.Active, 1.5f);
            float found = p1.SkillStageTime;
            bool marked = p1.KnightMarked == d;
            yield return new WaitForSeconds(0.15f);
            Tap();
            yield return Until(() => d.State == PawnState.Ragdoll || p1.SkillStage != SkillStage.Active, 2f);
            string hit = d.State == PawnState.Ragdoll ? d.LastSkillHit : "안 맞음";
            yield return Until(() => p1.SkillStage != SkillStage.Active, 3f);
            Add($"나이트 머리 찍기: 공중 {found:0.00}초에 {(marked ? "더미 감지" : "감지 못함")} → F → 더미 {StateText(d)} · {hit}, 착지 지점이 더미에서 {FlatDistance(p1.Hips.position, d.Hips.position):0.0} m");
            yield return new WaitForSeconds(1f);
        }

        IEnumerator KnightLand()
        {
            // Landing about 5 m on; a dummy 1 m beside that spot, another 2.5 m off.
            yield return Ready(PieceKind.Knight, new Vector3(0f, 0f, -12f), Vector3.forward, 2);
            Place(game.dummies[0], new Vector3(1f, 0f, -7f), Vector3.back);
            Place(game.dummies[1], new Vector3(-2.5f, 0f, -7f), Vector3.back);
            yield return new WaitForSeconds(0.7f);
            var p1 = P1;
            Tap();
            bool near = false, far = false;
            yield return Until(() => p1.SkillStage == SkillStage.Recovery, 3f);
            yield return Until(() => { near |= game.dummies[0].Staggered; far |= game.dummies[1].Staggered; return false; }, 0.3f);
            Add($"나이트 착지 충격: 1 m 옆 더미 {(near ? "휘청" : "그대로")} (기대 휘청), 2.5 m 옆 더미 {(far ? "휘청" : "그대로")} (기대 그대로)");
            yield return new WaitForSeconds(1f);
        }

        /// <summary>Only the first half of "bishop": the wire, and an enemy tripping on it (for the film).</summary>
        IEnumerator BishopTrip()
        {
            yield return Ready(PieceKind.Bishop, new Vector3(0f, 0f, -11f), Vector3.forward, 1);
            var p1 = P1;
            var d = game.dummies[0];
            Place(d, new Vector3(-3.5f, 0f, -6.4f), Vector3.right);
            // The see-through X follows the mouse; past the close range it stops at the range's edge.
            Tap();
            yield return new WaitForSeconds(0.25f);
            yield return AimThrough(new[] { new Vector3(-2.5f, 0f, -5f), new Vector3(2.5f, 0f, -2f), new Vector3(1.5f, 0f, -8f), new Vector3(0f, 0f, -6.6f) }, 2.2f);
            yield return new WaitForSeconds(0.25f);
            Click();
            yield return new WaitForSeconds(0.9f);
            walker = d;
            walkerMove = Vector3.right;
            yield return Until(() => d.State == PawnState.Ragdoll, 2.5f);
            StopWalker();
            Add($"비숍 밧줄 걸기: 적 더미 {StateText(d)} · {d.LastSkillHit}");
            yield return new WaitForSeconds(1.4f);
        }

        IEnumerator Bishop()
        {
            yield return Ready(PieceKind.Bishop, new Vector3(0f, 0f, -11f), Vector3.forward, 1);
            var p1 = P1;
            Vector3 head = p1.bodies[(int)BodyId.Head].position;
            aim = (new Vector3(0f, 0f, -6f) - head).normalized;
            yield return AimThenClick(0.4f);
            yield return new WaitForSeconds(0.5f);
            var wire = FindFirstObjectByType<SkillTripwire>();
            Add($"비숍 밧줄: {(wire != null ? $"깔림 (가운데 {wire.Center.x:0.0}, {wire.Center.z:0.0}, 비숍에서 {FlatDistance(wire.Center, p1.Hips.position):0.0} m)" : "안 깔림")}, 쿨 {p1.SkillCooldown:0.0}초");
            // An enemy runs across it, along x.
            var d = game.dummies[0];
            Place(d, new Vector3(-3.5f, 0f, -6f), Vector3.right);
            yield return new WaitForSeconds(0.5f);
            walker = d;
            walkerMove = Vector3.right;
            yield return Until(() => d.State == PawnState.Ragdoll, 2f);
            StopWalker();
            Add($"  가로질러 달려간 적 더미: {StateText(d)} · {d.LastSkillHit}");
            yield return new WaitForSeconds(1.5f);
            // A fresh wire; a rook runs through it: it snaps.
            p1.ResetSkill();
            yield return AimThenClick(0.4f);
            yield return new WaitForSeconds(0.9f);
            d.SetPiece(PieceKind.Rook);
            Place(d, new Vector3(3.5f, 0f, -6f), Vector3.left);
            yield return new WaitForSeconds(0.5f);
            walker = d;
            walkerMove = Vector3.left;
            yield return new WaitForSeconds(1.6f);
            StopWalker();
            wire = FindFirstObjectByType<SkillTripwire>();
            Add($"  룩 더미가 지나감: {StateText(d)}, 밧줄 {(wire == null ? "끊어짐" : "남아 있음")}");
            d.SetPiece(PieceKind.Pawn);
            yield return new WaitForSeconds(0.5f);
        }
    }
}
