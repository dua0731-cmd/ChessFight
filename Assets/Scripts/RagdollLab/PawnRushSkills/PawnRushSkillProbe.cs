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
        // R153 (skill plan v0.3 §9.3): the rook's chain and the knight's fork in place of the charge and the bent leap; the
        // king is out of Pawn Rush.
        public static readonly string[] Names = { "jump", "pawn", "pawn-angles", "pawn-help", "pawn-home", "pawn-none", "rook-chain-enemy", "rook-chain-ally", "rook-chain-cancel", "rook-chain-wall", "queen", "knight-fork", "knight-whiff", "knight-none", "knight-e", "bishop", "bishop-trip", "bishop-leash", "rook-ground", "rook-aim", "all" };
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
        bool sprint, jumpEdge, tapPending, clickPending, grab, abilityEdge;
        RagdollPawn walker;
        Vector3 walkerMove;
        bool walkerJump;
        float aimTopSpeed;
        bool aimSprinted;

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

        /// <summary>R145: a pawn held to this input every frame from outside a run (e.g. a dummy wearing a piece made to
        /// sprint or drag, to look at how its model is drawn); null for none.</summary>
        public static RagdollPawn Steered;
        public static PawnInput Steer;

        void Update()
        {
            if (Steered != null) Steered.SetInput(Steer);
            if (!driving) return;
            var p1 = P1;
            if (p1 != null)
            {
                // The left click is the dive's button (shove): an aiming rook or bishop takes it as "go".
                p1.SetInput(new PawnInput { move = move, aim = aim, sprint = sprint, jump = jumpEdge, shove = clickPending, grab = grab, ability = abilityEdge });
                // The press goes in with this frame's aim and move, never ahead of them.
                p1.SetSkillInput(tapPending);
                jumpEdge = tapPending = clickPending = abilityEdge = false;
                if (p1.SkillAiming)
                {
                    aimTopSpeed = Mathf.Max(aimTopSpeed, p1.HorizontalSpeed);
                    aimSprinted |= p1.Sprinting;
                }
            }
            if (walker != null) walker.SetInput(new PawnInput { move = walkerMove, aim = walkerMove, jump = walkerJump });
            walkerJump = false;
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
                    "pawn-home" => PawnHome(),
                    "pawn-none" => PawnNone(),
                    "rook-chain-enemy" => RookChainEnemy(),
                    "rook-chain-ally" => RookChainAlly(),
                    "rook-chain-cancel" => RookChainCancel(),
                    "rook-chain-wall" => RookChainWall(),
                    "queen" => Queen(),
                    "knight-fork" => KnightFork(),
                    "knight-whiff" => KnightWhiff(),
                    "knight-none" => KnightNone(),
                    "knight-e" => KnightE(),
                    // R130 (승규 님: "벽이나 폰들이랑 부딪히거나 이럴 때 좌클릭 우클릭 무슨 키를 눌러도 낑긴 것 같은 거"):
                    // press into a wall or a crowd of pawns, then try to get away (and click, and grab) and log the body.
                    "stuck-wall" => Stuck(false),
                    "stuck-crowd" => Stuck(true),
                    "stuck-dive" => StuckDive(),
                    "bishop" => Bishop(),
                    "bishop-trip" => BishopTrip(),
                    "bishop-leash" => BishopLeash(),
                    "rook-ground" => RookGroundRun(),
                    "rook-aim" => RookAimRun(),
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
        IEnumerator Sweep(Vector3 center, float degrees, float seconds, string hint = "마우스로 방향")
        {
            Say(hint);
            float t = 0f;
            while (t < seconds)
            {
                aim = Quaternion.Euler(0f, degrees * Mathf.Sin(t / seconds * Mathf.PI * 2f), 0f) * center;
                yield return null;
                t += Time.deltaTime;
            }
            aim = center;
        }

        /// <summary>Turn the aim (the mouse) onto <paramref name="target"/> across the ground.</summary>
        IEnumerator AimAt(Vector3 target, float seconds)
        {
            var p1 = P1;
            Vector3 from = aim;
            from.y = 0f;
            float t = 0f;
            while (t < seconds)
            {
                Vector3 to = target - p1.Hips.position;
                to.y = 0f;
                aim = Vector3.Slerp(from.normalized, to.normalized, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
                t += Time.deltaTime;
            }
            Vector3 end = target - p1.Hips.position;
            end.y = 0f;
            aim = end.normalized;
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
            p.SkillBound ? $"묶임 (남은 {p.SkillBindLeft:0.0}초)" : p.State == PawnState.Ragdoll ? "넘어짐" : p.Staggered ? "휘청" : p.State == PawnState.GettingUp ? "일어나는 중" : "서 있음";

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

        /// <summary>R141 (승규 님 10-14: "나이트 E키를 누르면 높이 나는 버그가 있어 고쳐 줘"): E pressed with the knight. The lab's
        /// L-jump (6 m up) is off with the skills on; the knight stays on the floor.</summary>
        IEnumerator KnightE()
        {
            yield return Ready(PieceKind.Knight, new Vector3(-14f, 0f, -12f), Vector3.right, 0);
            var p1 = P1;
            yield return new WaitForSeconds(0.4f);
            float start = p1.Hips.position.y, top = start;
            int jumps = p1.KnightJumps;
            abilityEdge = true;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                top = Mathf.Max(top, p1.Hips.position.y);
                yield return null;
            }
            Add($"나이트 E: L자 점프 {p1.KnightJumps - jumps}번, 골반 최고 {top - start:+0.00;-0.00} m (뜨지 않아야 함)");
            yield return new WaitForSeconds(0.3f);
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
                yield return Ready(PieceKind.Pawn, new Vector3(0f, 0f, -8.3f), Vector3.forward, 1);   // R153: the step is 1.5 m again
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
            yield return Ready(PieceKind.Pawn, new Vector3(0f, 0f, -8.3f), Vector3.forward, 1);   // R153: the step is 1.5 m again
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
            yield return Until(() => d.State == PawnState.Active, 2f);
            float guard = d.GetUpGuardLeft;
            yield return Until(() => p1.SkillStage == SkillStage.None, 1.5f);
            Add($"폰 부축: 팀원 {before} → {after}, 일어선 팀원 기상 보호 {guard:0.0}초 (기대 1.5, 속도 버프 없음), 쿨 {p1.SkillCooldown:0.0}초 (한 걸음 3.5 − 1.5 = 2.0 기대)");
            d.Team = Teams.None;
        }

        /// <summary>R130: walk (and sprint) into the 3 m wall at x 20, or into four dummies packed in front; then each way
        /// out in turn — along it, straight back, after a grab (right click) held a moment, after a dive (left click) — and
        /// log how far P1 got in each and what state its body was in (stiffness, holding, climbing, standing).</summary>
        IEnumerator Stuck(bool crowd)
        {
            if (crowd)
            {
                yield return Ready(PieceKind.Pawn, new Vector3(-6f, 0f, -12f), Vector3.forward, 4);
                bed.ClusterDummies(P1);
            }
            else yield return Ready(PieceKind.Pawn, new Vector3(18.4f, 0f, 0f), Vector3.right, 0);
            var p1 = P1;
            Vector3 into = crowd ? Vector3.forward : Vector3.right;
            Vector3 along = crowd ? Vector3.right : Vector3.forward;
            yield return new WaitForSeconds(0.6f);
            string Line() => $"{StateText(p1)} · 단단함 {p1.Stiffness:0.00} · 속도 {p1.HorizontalSpeed:0.0} · 잡음 {(p1.Grabbing ? "예" : "아니오")} · 벽타기 {(p1.Climbing ? "예" : "아니오")} · 잡힘 {(p1.BeingHeld ? "예" : "아니오")}";
            IEnumerator Leg(string label, Vector3 dir, float seconds, bool run = false, bool hold = false)
            {
                Vector3 from = p1.Hips.position;
                move = dir;
                sprint = run;
                grab = hold;
                float worst = 99f, t = 0f, slowFor = 0f;
                while (t < seconds)
                {
                    t += Time.deltaTime;
                    if (t > 0.25f)
                    {
                        worst = Mathf.Min(worst, p1.HorizontalSpeed);
                        if (dir != Vector3.zero && p1.HorizontalSpeed < 0.8f) slowFor += Time.deltaTime;
                    }
                    yield return null;
                }
                Vector3 d = p1.Hips.position - from;
                d.y = 0f;
                Add($"  {label}: {d.magnitude:0.00} m 이동 (원하는 쪽 {Vector3.Dot(d, dir.sqrMagnitude > 0f ? dir.normalized : Vector3.zero):0.00} m) · 느린 시간 {slowFor:0.00}초 · 끝 {Line()}");
            }
            Add(crowd ? "낑김 시험 — 더미 4명 뭉치에 밀고 들어가기" : "낑김 시험 — 벽(x 20)에 밀고 들어가기");
            yield return Leg("밀고 들어감 (W, 1.2초)", into, 1.2f);
            yield return Leg("옆으로 (D, 0.8초)", along, 0.8f);
            yield return Leg("다시 밀고 (W, 0.8초)", into, 0.8f);
            yield return Leg("뒤로 (S, 0.8초)", -into, 0.8f);
            yield return Leg("질주로 밀고 (Shift+W, 1초)", into, 1f, true);
            yield return Leg("옆으로 (A, 0.8초)", -along, 0.8f);
            yield return Leg("밀며 우클릭 잡기 (0.6초)", into, 0.6f, false, true);
            yield return Leg("잡기 놓고 뒤로 (S, 0.8초)", -into, 0.8f);
            yield return Leg("다시 밀고 (W, 0.6초)", into, 0.6f);
            clickPending = true;
            yield return Leg("밀며 좌클릭 다이빙 (1.2초 기다림)", into, 1.2f);
            yield return Leg("일어나 뒤로 (S, 1초)", -into, 1f);
            yield return Leg("옆으로 (D, 0.8초)", along, 0.8f);
            move = Vector3.zero;
            sprint = grab = false;
            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>R130: a dive (left click) straight into the wall from 1.4 m, then S held; P1's state every 0.1 s for 3.5 s
        /// (with its knockdowns and what caused them), to see how long it lies there and why.</summary>
        IEnumerator StuckDive()
        {
            yield return Ready(PieceKind.Pawn, new Vector3(18.2f, 0f, 0f), Vector3.right, 0);
            var p1 = P1;
            yield return new WaitForSeconds(0.5f);
            int k0 = p1.Knockdowns;
            Staged = true;
            Say("W · 벽 쪽으로");
            move = Vector3.right;
            yield return new WaitForSeconds(0.25f);
            Say("좌클릭 · 다이빙");
            clickPending = true;
            Add("다이빙 → 벽: 0.1초마다 상태");
            float t = 0f;
            string last = "";
            while (t < 3.5f)
            {
                if (t > 0.6f && move != Vector3.left)
                {
                    Say("S · 뒤로");
                    move = Vector3.left;
                }
                string line = $"{StateText(p1)} · 넘어짐 {p1.Knockdowns - k0}번 ({p1.LastKnockdownCause}) · 다이빙 {(p1.Diving ? "예" : "아니오")} · 속도 {p1.HorizontalSpeed:0.0}";
                if (line != last) { Add($"  {t:0.0}초: {line}"); last = line; }
                t += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
            move = Vector3.zero;
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
            // R138: caught, it is tied up standing (no longer tripped); it keeps trying to walk on.
            yield return Until(() => d.SkillBound || d.State == PawnState.Ragdoll, 2.5f);
            var wire = FindFirstObjectByType<SkillTripwire>();
            Vector3 caughtAt = d.Hips.position;
            Add($"비숍 밧줄 걸기: 적 더미 {StateText(d)} · {d.LastSkillHit}");
            yield return new WaitForSeconds(0.8f);
            Add($"  묶인 동안 계속 걸으려 함: {StateText(d)}, 0.8초 동안 {FlatDistance(d.Hips.position, caughtAt):0.00} m 움직임 (기대 0.1 m 안)");
            StopWalker();
            yield return Until(() => !d.SkillBound, 2f);
            Add($"  풀림: {StateText(d)} (기대 서 있음)");
            yield return new WaitForSeconds(0.9f);
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
            yield return Until(() => d.SkillBound || d.State == PawnState.Ragdoll, 2f);
            StopWalker();
            Add($"  가로질러 달려간 적 더미: {StateText(d)} · {d.LastSkillHit} (R138 기대: 묶임)");
            // Free again, it walks back through: the line stretches after its legs and slips off them, and it is not
            // caught a second time (R82). Since R138 a wire that tied someone is spent (it twangs and goes once the tie is
            // over), so the line is not pulled out any more, or is gone.
            yield return Until(() => !d.SkillBound && d.State == PawnState.Active, 3.5f);
            yield return new WaitForSeconds(0.2f);
            int pulls = wire != null ? wire.Pulls : 0;
            walker = d;
            walkerMove = Vector3.left;
            yield return new WaitForSeconds(1.4f);
            StopWalker();
            bool pulled = wire != null && wire.Pulls > pulls;
            Add($"  일어나 다시 지나간 더미: {StateText(d)}, 줄 {(pulled ? $"{wire.LastPull:0.00} m 늘어났다 빠져나감" : wire == null ? "없음" : "안 늘어남")} (R138부터 기대: 묶은 줄은 다 써서 안 늘어나거나 없음, 안 넘어짐)");
            yield return new WaitForSeconds(0.5f);
            yield return new WaitForSeconds(0.5f);
        }

        // ---------------------------------------------------------------- R153 (skill plan v0.3)

        /// <summary>The dummies on P1's team (the rook's chain offers them its handle; they grab it by themselves).</summary>
        void Mates(RagdollPawn p1, params RagdollPawn[] dummies)
        {
            foreach (var d in dummies)
            {
                d.Team = p1.Team;
                d.SkillBot = true;
                if (d.skin != null) d.skin.sharedMaterial = game.TeamMaterial(p1.Team);
            }
        }

        /// <summary>The pawn's second step homes (plan §3): an enemy ahead and off to the right; after the first step the
        /// second goes for it and bends onto it. Then a downed teammate off to the left: helped up.</summary>
        IEnumerator PawnHome()
        {
            yield return Ready(PieceKind.Pawn, new Vector3(0f, 0f, -12f), Vector3.forward, 2);
            var p1 = P1;
            var d = game.dummies[0];
            Place(d, new Vector3(1.1f, 0f, -8.9f), Vector3.back);   // about 1.9 m ahead and 30° right of where the first step ends
            yield return new WaitForSeconds(0.6f);
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.Link, 1f);
            var target = p1.PawnTarget;
            Vector3 one = p1.Hips.position;
            yield return new WaitForSeconds(0.12f);
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.None, 1.2f);
            yield return new WaitForSeconds(0.1f);
            Add($"폰 두 번째 걸음 (적): 대상 {(target == d ? "더미" : target != null ? target.DisplayName : "없음")}, 두 번째 걸음 {FlatDistance(one, p1.Hips.position):0.00} m, 더미 {StateText(d)} · {d.LastSkillHit}, 쿨 {p1.SkillCooldown:0.0}초 (기대 5)");
            yield return new WaitForSeconds(1.5f);
            // A downed teammate ahead to the left.
            yield return Ready(PieceKind.Pawn, new Vector3(0f, 0f, -12f), Vector3.forward, 1);
            p1 = P1;
            d = game.dummies[0];
            Mates(p1, d);
            Place(d, new Vector3(-1.2f, 0f, -8.8f), Vector3.back);
            yield return new WaitForSeconds(0.5f);
            d.Knockdown("시험: 넘어뜨리기", 4f);
            yield return new WaitForSeconds(0.8f);
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.Link, 1f);
            target = p1.PawnTarget;
            yield return new WaitForSeconds(0.12f);
            Tap();
            yield return Until(() => d.State != PawnState.Ragdoll || p1.SkillStage == SkillStage.None, 1.2f);
            Add($"폰 두 번째 걸음 (넘어진 팀원): 대상 {(target == d ? "팀원" : target != null ? target.DisplayName : "없음")} → 팀원 {StateText(d)} (부축 기대)");
            d.Team = Teams.None;
            d.SkillBot = false;
            yield return new WaitForSeconds(1f);
        }

        /// <summary>No one to go for: the second F is ignored (a short "no" sound), only the first step counts (3.5 s).</summary>
        IEnumerator PawnNone()
        {
            yield return Ready(PieceKind.Pawn, new Vector3(0f, 0f, -12f), Vector3.forward, 1);
            var p1 = P1;
            Vector3 start = p1.Hips.position;
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.Link, 1f);
            Vector3 one = p1.Hips.position;
            yield return new WaitForSeconds(0.12f);
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.None, 1.5f);
            Add($"폰 대상 없음: 첫 걸음 {FlatDistance(start, one):0.00} m (기대 1.5), 두 번째 F 뒤 움직임 {FlatDistance(one, p1.Hips.position):0.00} m (기대 0 근처), 쿨 {p1.SkillCooldown:0.0}초 (기대 3.5)");
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>The rook's chain at an enemy 5 m ahead (plan §5.3): hooked, pulled in about 0.35 s to 1.5 m in front,
        /// staggered; the rook does not move. Then the same at a queen (weight 2): 1.3 times as long.</summary>
        IEnumerator RookChainEnemy()
        {
            foreach (bool queen in new[] { false, true })
            {
                yield return Ready(PieceKind.Rook, new Vector3(0f, 0f, -12f), Vector3.forward, 1);
                var p1 = P1;
                var d = game.dummies[0];
                if (queen) d.SetPiece(PieceKind.Queen);
                Place(d, new Vector3(0f, 0f, -7f), Vector3.back);
                yield return new WaitForSeconds(0.6f);
                Vector3 rookAt = p1.Hips.position;
                Tap();
                yield return new WaitForSeconds(0.3f);
                Click();
                yield return Until(() => p1.RookChainPhase == ChainPhase.Pull || (p1.SkillStage == SkillStage.None && p1.SkillCooldown > 0f), 1.5f);
                float hookAt = Time.time;
                bool pulled = p1.RookChainPhase == ChainPhase.Pull;
                bool staggered = false;
                yield return Until(() => !d.ChainCarried && p1.RookChainPhase != ChainPhase.Pull, 1.5f);
                float took = Time.time - hookAt;
                yield return Until(() => { staggered |= d.Staggered; return false; }, 0.15f);
                Vector3 fwd = p1.Facing;
                Add($"룩 사슬 → {(queen ? "퀸 더미" : "폰 더미")} 5 m: {(pulled ? "걸림" : "안 걸림")}, 끌려온 시간 {took:0.00}초 (기대 {(queen ? 0.46f : 0.35f):0.00}), 룩에서 {FlatDistance(d.Hips.position, p1.Hips.position):0.00} m (기대 1.5), 더미 {(staggered ? "휘청" : StateText(d))}, 룩 움직임 {FlatDistance(rookAt, p1.Hips.position):0.00} m, 쿨 {p1.SkillCooldown:0.0}초 (기대 8)");
                if (queen) d.SetPiece(PieceKind.Pawn);
                yield return new WaitForSeconds(0.8f);
            }
        }

        /// <summary>The chain to a teammate 4 m ahead (plan §5.4): its handle held out, the dummy grabs it (0.6 s), it rides in
        /// 0.5 s to 1 m beside the rook and lands standing. Then with the dummies told not to grab: wound back, 4 s.</summary>
        IEnumerator RookChainAlly()
        {
            foreach (bool grabs in new[] { true, false })
            {
                yield return Ready(PieceKind.Rook, new Vector3(0f, 0f, -12f), Vector3.forward, 1);
                var p1 = P1;
                var d = game.dummies[0];
                Mates(p1, d);
                Place(d, new Vector3(0.3f, 0f, -8f), Vector3.back);
                RagdollPawn.RookChainBotsGrab = grabs;
                try
                {
                    yield return new WaitForSeconds(0.6f);
                    Tap();
                    yield return new WaitForSeconds(0.3f);
                    Click();
                    yield return Until(() => p1.RookChainPhase == ChainPhase.Offer, 1f);
                    float offered = Time.time;
                    bool wasOffered = p1.RookChainPhase == ChainPhase.Offer;
                    yield return Until(() => p1.RookChainPhase != ChainPhase.Offer, 3f);
                    float waited = Time.time - offered;
                    bool rode = p1.RookChainPhase == ChainPhase.Pull;
                    float rideAt = Time.time, top = d.Hips.position.y, start = d.Hips.position.y;
                    yield return Until(() => { top = Mathf.Max(top, d.Hips.position.y); return !d.ChainCarried && p1.RookChainPhase != ChainPhase.Pull; }, 1.5f);
                    float ride = Time.time - rideAt;
                    yield return Until(() => p1.SkillStage == SkillStage.None, 1f);
                    yield return new WaitForSeconds(0.2f);
                    if (grabs)
                        Add($"룩 사슬 → 팀원 4 m: 손잡이 {(wasOffered ? "내밂" : "안 내밂")}, 잡기까지 {waited:0.00}초, {(rode ? "끌려옴" : "안 끌려옴")} {ride:0.00}초 (기대 0.5), 골반 최고 +{top - start:0.00} m (기대 0.6 안), 룩 옆 {FlatDistance(d.Hips.position, p1.Hips.position):0.00} m (기대 1.0 근처), 팀원 {StateText(d)} (서 있음 기대), 쿨 {p1.SkillCooldown:0.0}초 (기대 8)");
                    else
                        Add($"  팀원이 안 잡음: 손잡이 {waited:0.00}초 (기대 2.0) 뒤 되감기, 팀원 움직임 없음 {FlatDistance(d.Hips.position, new Vector3(0.3f, d.Hips.position.y, -8f)):0.00} m, 쿨 {p1.SkillCooldown:0.0}초 (기대 4)");
                }
                finally { RagdollPawn.RookChainBotsGrab = true; }
                d.Team = Teams.None;
                d.SkillBot = false;
                yield return new WaitForSeconds(0.6f);
            }
        }

        /// <summary>The rook knocked down while it pulls (plan §5.6): the chain breaks, the enemy drops where it is.</summary>
        IEnumerator RookChainCancel()
        {
            yield return Ready(PieceKind.Rook, new Vector3(0f, 0f, -12f), Vector3.forward, 1);
            var p1 = P1;
            var d = game.dummies[0];
            d.SetPiece(PieceKind.Queen);   // the slower pull, to catch it half way
            Place(d, new Vector3(0f, 0f, -6.5f), Vector3.back);
            yield return new WaitForSeconds(0.6f);
            Tap();
            yield return new WaitForSeconds(0.3f);
            Click();
            yield return Until(() => p1.RookChainPhase == ChainPhase.Pull, 1.5f);
            yield return new WaitForSeconds(0.15f);
            Vector3 dropAt = d.Hips.position;
            p1.Knockdown("시험: 끄는 중인 룩을 넘어뜨림");
            yield return new WaitForSeconds(0.6f);
            Add($"룩이 끄는 중 넘어짐: 사슬 {(p1.RookChainPhase == ChainPhase.None ? "끊김" : "남음 (틀림)")}, 더미 끌림 {(d.ChainCarried ? "계속 (틀림)" : "멈춤")}, 끊긴 곳에서 {FlatDistance(d.Hips.position, dropAt):0.00} m · 룩에서 {FlatDistance(d.Hips.position, p1.Hips.position):0.00} m (1.5보다 멀면 도중에 놓임), 더미 {StateText(d)}, 룩 쿨 {p1.SkillCooldown:0.0}초");
            d.SetPiece(PieceKind.Pawn);
            yield return new WaitForSeconds(2.5f);
        }

        /// <summary>The chain into the 3 m wall at x 20 (4.5 m ahead): it winds back from the wall, no one pulled.</summary>
        IEnumerator RookChainWall()
        {
            yield return Ready(PieceKind.Rook, new Vector3(15.5f, 0f, 0f), Vector3.right, 0);
            var p1 = P1;
            yield return new WaitForSeconds(0.4f);
            Tap();
            yield return new WaitForSeconds(0.3f);
            Click();
            yield return Until(() => p1.RookChainPhase == ChainPhase.Rewind, 1f);
            float tipX = p1.RookChainTip.x;
            yield return Until(() => p1.SkillStage == SkillStage.None, 1f);
            Add($"룩 사슬 → 벽: 사슬 끝 x {tipX:0.00} (벽 앞면 20), 되감고 끝, 쿨 {p1.SkillCooldown:0.0}초 (기대 8)");
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>The knight's fork (plan §7): an enemy 4 m ahead (the target) and one beside it; F leaps onto the target's
        /// head (it falls), the knight lands beside it, the other staggers.</summary>
        IEnumerator KnightFork()
        {
            yield return Ready(PieceKind.Knight, new Vector3(0f, 0f, -12f), Vector3.forward, 2);
            var p1 = P1;
            var target = game.dummies[0];
            var other = game.dummies[1];
            Place(target, new Vector3(0f, 0f, -8f), Vector3.back);
            Place(other, new Vector3(1.0f, 0f, -7.2f), Vector3.back);   // beside the target, a little past it (where the knight comes down)
            yield return new WaitForSeconds(0.7f);
            var marked = p1.KnightMarked;
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.Active, 1f);
            float off = Time.time;
            bool forked = false;
            yield return Until(() => target.State == PawnState.Ragdoll || p1.SkillStage != SkillStage.Active, 2.5f);
            float toHead = Time.time - off;
            string atStomp = StateText(target);
            yield return Until(() => p1.SkillStage != SkillStage.Active, 2.5f);
            Vector3 land = p1.Hips.position, targetAt = target.Hips.position;
            yield return Until(() => { forked |= other.Staggered; return false; }, 0.3f);
            Add($"나이트 포크: 대상 표시 {(marked == target ? "더미 1" : marked != null ? marked.DisplayName : "없음")}, 머리까지 {toHead:0.00}초 (0.8~1.14) → 대상 {atStomp} · {target.LastSkillHit}, 착지는 대상에서 {FlatDistance(land, targetAt):0.0} m · 옆 더미에서 {FlatDistance(land, other.Hips.position):0.0} m, 옆 더미 {(forked ? "휘청" : StateText(other))} (휘청 기대)");
            yield return Until(() => p1.SkillStage == SkillStage.None, 1f);
            Add($"  쿨 {p1.SkillCooldown:0.0}초 (기대 7)");
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>The target dodges far (plan §7.2): moved 3 m aside as the knight takes off; the knight follows at most
        /// 3 m/s, comes down on its last spot, nothing staggers (a dummy stands close to that spot).</summary>
        IEnumerator KnightWhiff()
        {
            yield return Ready(PieceKind.Knight, new Vector3(0f, 0f, -12f), Vector3.forward, 2);
            var p1 = P1;
            var target = game.dummies[0];
            var near = game.dummies[1];
            Place(target, new Vector3(0f, 0f, -8f), Vector3.back);
            Place(near, new Vector3(-1.4f, 0f, -8.3f), Vector3.back);
            yield return new WaitForSeconds(0.7f);
            Tap();
            yield return Until(() => p1.SkillStage == SkillStage.Active, 1f);
            Place(target, new Vector3(5f, 0f, -8f), Vector3.back);
            target.LastSkillHit = "-";
            bool staggered = false;
            yield return Until(() => p1.SkillStage != SkillStage.Active, 2.5f);
            yield return Until(() => { staggered |= near.Staggered; return false; }, 0.3f);
            Add($"나이트 빗나감: 대상 {StateText(target)} · {target.LastSkillHit} (서 있음 기대), 착지 x {p1.Hips.position.x:0.0} (대상은 x 5로 피함, 따라가기 초당 3 m), 가까운 더미 {(staggered ? "휘청 (틀림)" : StateText(near))}");
            yield return Until(() => p1.SkillStage == SkillStage.None, 1f);
            Add($"  쿨 {p1.SkillCooldown:0.0}초 (빗나가도 7)");
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>No enemy in range: F does nothing (no leap, no cooldown).</summary>
        IEnumerator KnightNone()
        {
            yield return Ready(PieceKind.Knight, new Vector3(0f, 0f, -12f), Vector3.forward, 1);
            var p1 = P1;
            yield return new WaitForSeconds(0.4f);
            Vector3 start = p1.Hips.position;
            Tap();
            yield return new WaitForSeconds(0.8f);
            Add($"나이트 대상 없음: 단계 {StageName()} · 움직임 {FlatDistance(start, p1.Hips.position):0.00} m · 쿨 {p1.SkillCooldown:0.0}초 (모두 0 기대)");
            string StageName() => p1.SkillStage.ToString();
        }

        /// <summary>R153: the bed's rook test ground (H, J): the chain at the enemy behind the crate (it is dropped at the
        /// crate), then from the 1.4 m ledge (J) down to the teammate at its foot (it comes up onto the ledge).</summary>
        IEnumerator RookGroundRun()
        {
            Stop();
            driving = true;
            var p1 = P1;
            bed.RookGround(p1, false);
            yield return new WaitForSeconds(0.9f);
            Staged = true;
            var crateEnemy = game.dummies[1];
            Vector3 eye = p1.bodies[(int)BodyId.Chest].position;
            aim = (crateEnemy.bodies[(int)BodyId.Chest].position - eye).normalized;
            yield return AimThenClick(0.35f);
            yield return Until(() => p1.RookChainPhase == ChainPhase.Pull, 1.5f);
            bool hooked = p1.RookChainPhase == ChainPhase.Pull;
            yield return Until(() => !crateEnemy.ChainCarried, 1.5f);
            yield return new WaitForSeconds(0.4f);
            Add($"룩 시험장 · 낮은 벽 뒤 적: {(hooked ? "걸림 (틀림)" : "안 걸림 (벽에 막힘)")}, 적은 룩에서 {FlatDistance(crateEnemy.Hips.position, p1.Hips.position):0.0} m, {StateText(crateEnemy)}");
            yield return Until(() => p1.SkillStage == SkillStage.None, 2f);
            // The enemy 7 m ahead (the reach is 8 m).
            p1.ResetSkill();
            var far = game.dummies[0];
            float farFrom = FlatDistance(far.Hips.position, p1.Hips.position);
            aim = (far.bodies[(int)BodyId.Chest].position - p1.bodies[(int)BodyId.Chest].position).normalized;
            yield return AimThenClick(0.35f);
            Vector3 lastTip = Vector3.zero;
            float maxOut = 0f;
            yield return Until(() =>
            {
                if (p1.RookChainPhase == ChainPhase.Fly) { lastTip = p1.RookChainTip; maxOut = Mathf.Max(maxOut, FlatDistance(lastTip, p1.Hips.position)); }
                return p1.RookChainPhase == ChainPhase.Pull || p1.RookChainPhase == ChainPhase.Rewind;
            }, 1.5f);
            Add($"  7 m 사슬: 단계 {p1.RookChainPhase}, 날아간 끝 {lastTip:F2} (룩에서 {maxOut:0.0} m), 적 가슴 {far.bodies[(int)BodyId.Chest].position:F2}, 쿨 {p1.SkillCooldown:0.0}, 단계 {p1.SkillStage}");
            bool farHooked = p1.RookChainPhase == ChainPhase.Pull;
            yield return Until(() => !far.ChainCarried && p1.RookChainPhase != ChainPhase.Pull, 1.5f);
            Add($"룩 시험장 · 앞 {farFrom:0.0} m 적: {(farHooked ? "걸림" : "안 걸림")}, 룩에서 {FlatDistance(far.Hips.position, p1.Hips.position):0.00} m로 끌려옴 (기대 1.5)");
            yield return Until(() => p1.SkillStage == SkillStage.None, 3f);
            p1.ResetSkill();
            bed.RookGround(p1, true);
            yield return new WaitForSeconds(0.9f);
            var below = game.dummies[3];
            eye = p1.bodies[(int)BodyId.Chest].position;
            aim = (below.bodies[(int)BodyId.Chest].position - eye).normalized;
            float startY = below.FeetPoint.y;
            yield return AimThenClick(0.35f);
            yield return Until(() => p1.RookChainPhase == ChainPhase.Offer || p1.RookChainPhase == ChainPhase.Rewind, 1.5f);
            var phase = p1.RookChainPhase;
            Vector3 tipAt = p1.RookChainTip;
            yield return Until(() => p1.SkillStage == SkillStage.None, 3f);
            yield return new WaitForSeconds(0.4f);
            Add($"  사슬 끝 {tipAt:F2}, 룩 가슴 {p1.bodies[(int)BodyId.Chest].position:F2}, 팀원 가슴 {below.bodies[(int)BodyId.Chest].position:F2}");
            Add($"룩 시험장 · 단 위에서 아래 팀원: 사슬 {(phase == ChainPhase.Offer ? "손잡이 내밂" : phase.ToString())}, 팀원 발 높이 {startY:0.0} → {below.FeetPoint.y:0.0} m (단 0.8), 룩에서 {FlatDistance(below.Hips.position, p1.Hips.position):0.0} m, {StateText(below)}");
        }

        /// <summary>R153 (승규 님 10-22: "마우스를 좀 많이 내려야 걸려"): aimed like a player — the camera's look (14° down), a
        /// little off to the side of the enemy 7 m ahead — the chain still takes it; aimed at no one, it goes out level.</summary>
        IEnumerator RookAimRun()
        {
            Stop();
            driving = true;
            var p1 = P1;
            bed.RookGround(p1, false);
            yield return new WaitForSeconds(0.9f);
            Staged = true;
            var far = game.dummies[0];
            Vector3 flat = far.Hips.position - p1.Hips.position;
            flat.y = 0f;
            aim = Quaternion.LookRotation(flat.normalized) * Quaternion.Euler(14f, 6f, 0f) * Vector3.forward;   // camera pitch 14, 6° off
            Tap();
            yield return new WaitForSeconds(0.3f);
            var marked = p1.RookChainAimTarget;
            Click();
            yield return Until(() => p1.RookChainPhase == ChainPhase.Pull || p1.RookChainPhase == ChainPhase.Rewind, 1.5f);
            Add($"룩 조준 도움: 카메라처럼 14° 아래 · 6° 옆으로 조준 → 표시 {(marked == far ? "7 m 적" : marked != null ? marked.DisplayName : "없음")}, 사슬 {(p1.RookChainPhase == ChainPhase.Pull ? "걸림" : "안 걸림")}");
            yield return Until(() => p1.SkillStage == SkillStage.None, 3f);
            p1.ResetSkill();
            // Aimed where no one stands (east), at the camera's own pitch: the chain goes out level.
            aim = Quaternion.Euler(14f, 90f, 0f) * Vector3.forward;
            Tap();
            yield return new WaitForSeconds(0.3f);
            Click();
            yield return Until(() => p1.RookChainPhase == ChainPhase.Fly, 0.6f);
            float chestY = p1.bodies[(int)BodyId.Chest].position.y, tipY = 0f, outMost = 0f;
            yield return Until(() =>
            {
                if (p1.RookChainPhase == ChainPhase.Fly) { tipY = p1.RookChainTip.y; outMost = FlatDistance(p1.RookChainTip, p1.Hips.position); }
                return p1.RookChainPhase == ChainPhase.Rewind || p1.RookChainPhase == ChainPhase.None;
            }, 1.5f);
            Add($"  아무도 없는 쪽: 사슬이 {outMost:0.0} m 나감, 끝 높이 {tipY:0.00} m (가슴 {chestY:0.00} — 수평이면 비슷)");
            yield return Until(() => p1.SkillStage == SkillStage.None, 3f);
        }

        /// <summary>The bishop's leash (plan §6): its X laid 4.5 m ahead, then it walks away; past 6 m from the middle the
        /// rope comes loose and goes. Then a fresh one and the bishop knocked down: it goes too.</summary>
        IEnumerator BishopLeash()
        {
            yield return Ready(PieceKind.Bishop, new Vector3(0f, 0f, -11f), Vector3.forward, 0);
            var p1 = P1;
            aim = (new Vector3(0f, 0f, -6.5f) - p1.bodies[(int)BodyId.Head].position).normalized;
            yield return AimThenClick(0.4f);
            yield return new WaitForSeconds(0.7f);
            var wire = FindFirstObjectByType<SkillTripwire>();
            float laid = wire != null ? wire.LeashDistance : -1f;
            move = Vector3.back;
            Say("S · 뒤로 걷기");
            float lastSeen = laid;
            yield return Until(() =>
            {
                if (wire != null) lastSeen = wire.LeashDistance;
                return wire == null;
            }, 4f);
            move = Vector3.zero;
            Add($"비숍 덫 유지 거리: 깔 때 {laid:0.0} m → 뒤로 걷다 풀린 거리 {lastSeen:0.00} m (기대 6), 덫 {(wire == null ? "사라짐" : "남음 (틀림)")}");
            yield return new WaitForSeconds(0.5f);
            p1.ResetSkill();
            Place(p1, new Vector3(0f, 0f, -11f), Vector3.forward);
            yield return new WaitForSeconds(0.5f);
            aim = (new Vector3(0f, 0f, -6.5f) - p1.bodies[(int)BodyId.Head].position).normalized;
            yield return AimThenClick(0.4f);
            yield return new WaitForSeconds(0.7f);
            wire = FindFirstObjectByType<SkillTripwire>();
            bool there = wire != null;
            p1.Knockdown("시험: 비숍을 넘어뜨림");
            yield return new WaitForSeconds(0.2f);
            Add($"  비숍이 넘어짐: 덫 {(there ? (wire == null ? "사라짐" : "남음 (틀림)") : "안 깔림")}");
            yield return new WaitForSeconds(2.5f);
        }
    }
}
