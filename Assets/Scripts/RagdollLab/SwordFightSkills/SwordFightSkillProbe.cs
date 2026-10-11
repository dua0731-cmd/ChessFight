using System.Collections;
using System.Collections.Generic;
using System.Text;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Scripted runs of the Sword Fight skills in the skill test scene (R91), for the AI's checks and the film: each run
    /// puts the pieces where the bed's F7 does, then presses and lets go of the skill button the way a player would (and
    /// has a dummy cut where the test needs it), and reports what happened. "calibrate" pushes three dummies at three
    /// speeds and reports how far they slid (it sets <see cref="SwordFightSkillParams.pushPerMetre"/>'s value).
    /// Runs: calibrate, king, king-whiff, king-mid (the king in the middle of the platform, F8 0.3 s into his guard: the
    /// nearest enemy comes up and cuts, R102), queen, rook, bishop, bishop-mid (an enemy in the middle of the platform is
    /// held by the ankles all the same, R102), bishop-miss (nobody on the point), knight. Needs Play mode in
    /// SwordFight_SkillTest.
    /// </summary>
    public class SwordFightSkillProbe : MonoBehaviour
    {
        public static string Status { get; private set; } = "idle";
        public static bool Staged { get; private set; }
        public static string Hint { get; private set; } = "";
        public static int HintSerial { get; private set; }
        static readonly StringBuilder report = new StringBuilder();

        SwordFightSkillBed bed;

        public static string Run(string name)
        {
            var bed = FindFirstObjectByType<SwordFightSkillBed>();
            if (bed == null) return Status = "no SwordFightSkillBed (open SwordFight_SkillTest and press Play)";
            var probe = bed.GetComponent<SwordFightSkillProbe>();
            if (probe == null) probe = bed.gameObject.AddComponent<SwordFightSkillProbe>();
            probe.bed = bed;
            probe.StopAllCoroutines();
            Staged = false;
            report.Clear();
            probe.StartCoroutine(probe.Main(name));
            return Status = "running " + name;
        }

        public static string Report() => report.ToString();

        void Say(string line) { report.AppendLine(line); }

        void Show(string hint) { Hint = hint; HintSerial++; }

        IEnumerator Main(string name)
        {
            SwordFightSkillBed.Scripted = true;
            var lines = new List<string>();
            void OnLog(SwordFightSkills s, string l) => lines.Add($"{Time.time:0.00} {l}");
            SwordFightSkills.Log += OnLog;
            IEnumerator body = name switch
            {
                "calibrate" => Calibrate(),
                "king" => King(false),
                "king-whiff" => King(true),
                "king-mid" => KingMid(),
                "queen" => Line(PieceKind.Queen, Vector3.right, null),
                "rook" => Line(PieceKind.Rook, Vector3.right, null),
                "bishop" => Bishop(),
                "bishop-mid" => BishopMid(false),
                "bishop-miss" => BishopMid(true),
                "knight" => Line(PieceKind.Knight, Vector3.right, new Vector3(5.0f, 0f, 0f)),
                // The edge skills (R103, E).
                "edge-king" => EdgeKing(false),
                "edge-king-cut" => EdgeKing(true),
                "edge-queen" => EdgeQueen(0),
                "edge-queen-step" => EdgeQueen(1),
                "edge-queen-jump" => EdgeQueen(2),
                "edge-rook" => EdgeRook(),
                "edge-bishop" => EdgeBishop(false),
                "edge-bishop-hit" => EdgeBishop(true),
                "edge-bishop-close" => EdgeBishop(false, true),
                "edge-knight" => EdgeKnight(false),
                "edge-knight-back" => EdgeKnight(true),
                "edge-knight-side" => EdgeKnight(false, 1),
                "edge-knight-fall" => EdgeKnight(false, 2),
                _ => null,
            };
            if (body == null) { Status = "done: unknown run " + name; yield break; }
            var sfx = SwordFightSkillSfx.Current;
            var heardBefore = sfx != null ? new Dictionary<string, int>(sfx.Count) : null;
            yield return body;
            SwordFightSkills.Log -= OnLog;
            foreach (var l in lines) Say("  " + l);
            // The sounds this run started (R106): which moments reached a clip, and how often.
            if (sfx != null)
            {
                var heard = new List<string>();
                foreach (var kv in sfx.Count)
                {
                    int n = kv.Value - (heardBefore.TryGetValue(kv.Key, out int b) ? b : 0);
                    if (n > 0) heard.Add(n > 1 ? $"{kv.Key}×{n}" : kv.Key);
                }
                Say($"  소리 ({sfx.Sounds.Count}개 불러옴): " + (heard.Count > 0 ? string.Join(", ", heard) : "없음"));
            }
            var me = bed.Local;
            if (me != null && me.Skills != null) { me.Skills.CancelAim(); me.Skills.OverrideAim(null); }
            SwordFightSkillBed.Scripted = false;
            Staged = false;
            Status = "done " + name;
        }

        /// <summary>Wait until every fighter is back and unguarded, then put them where the bed's F7 does.</summary>
        IEnumerator StageFor(PieceKind kind)
        {
            var me = bed.Local;
            float waited = 0f;
            while (waited < 8f)
            {
                bool ready = me != null && me.Alive;
                foreach (var f in bed.Game.Fighters.Values) ready &= f != null && f.Alive;
                if (ready) break;
                waited += Time.deltaTime;
                yield return null;
            }
            bed.SetPiece(me, kind);
            bed.Stage(kind);
            yield return new WaitForSeconds(0.2f);
            bed.Stage(kind);   // once more: a piece still settling from a fall drifts
            float t = 0f;
            while (t < 1.4f)
            {
                bool guarded = false;
                foreach (var f in bed.Game.Fighters.Values) guarded |= f.Protection > 0f;
                if (!guarded && t > 0.6f) break;
                t += Time.deltaTime;
                yield return null;
            }
            Staged = true;
        }

        Dictionary<SwordFightPawn, Vector3> Starts()
        {
            var d = new Dictionary<SwordFightPawn, Vector3>();
            foreach (var f in bed.Game.Fighters.Values) if (f != null) d[f] = f.Pawn.Hips.position;
            return d;
        }

        void Moves(Dictionary<SwordFightPawn, Vector3> from, string title)
        {
            Say(title + $"  (점수 백 {bed.Game.White} : 흑 {bed.Game.Black})");
            foreach (var kv in from)
            {
                var f = kv.Key;
                if (f == null) continue;
                Vector3 d = f.Pawn.Hips.position - kv.Value;
                d.y = 0f;
                string state = !f.Alive ? "장외" : f.Pawn.State == PawnState.Ragdoll ? "넘어짐" : f.Pawn.State.ToString();
                Say($"  {f.Pawn.DisplayName}: {d.magnitude:0.00} m ({kv.Value.x:0.0},{kv.Value.z:0.0}) → ({f.Pawn.Hips.position.x:0.0},{f.Pawn.Hips.position.z:0.0}) {state}");
            }
        }

        IEnumerator Press(SwordFightSkills s, float hold, string aimHint = "F · 조준", string goHint = "좌클릭 · 발동")
        {
            Show(aimHint);
            s.PressKey();
            yield return new WaitForSeconds(hold);
            Show(goHint);
            s.Confirm();
        }

        // ---------------------------------------------------------------- runs

        IEnumerator Calibrate()
        {
            yield return StageFor(PieceKind.Pawn);
            var enemies = bed.Ensure(1, 3);
            float[] speeds = { 3f, 5f, 7f };
            var starts = new Dictionary<SwordFightPawn, Vector3>();
            for (int i = 0; i < 3; i++)
            {
                SwordFightSkillBed.Place(enemies[i], new Vector3(-4f, 0f, -3f + 3f * i), Vector3.left);
            }
            SwordFightSkillBed.Place(bed.Local, new Vector3(-5f, 0f, 5.5f), Vector3.right);
            yield return new WaitForSeconds(0.8f);
            var S = bed.skills;
            for (int i = 0; i < 3; i++)
            {
                starts[enemies[i]] = enemies[i].Pawn.Hips.position;
                enemies[i].Pawn.TakeHit(Vector3.right * speeds[i] + Vector3.up * S.pushLift, S.knockdownSeconds, 0f, true);
            }
            yield return new WaitForSeconds(2.5f);
            Say("calibrate (TakeHit 수평 속도 → 밀린 거리, 넘어짐 " + S.knockdownSeconds + "초, 위 " + S.pushLift + " m/s)");
            for (int i = 0; i < 3; i++)
            {
                Vector3 d = enemies[i].Pawn.Hips.position - starts[enemies[i]];
                d.y = 0f;
                Say($"  {speeds[i]:0} m/s → {d.magnitude:0.00} m  (m/s per m = {speeds[i] / Mathf.Max(0.01f, d.magnitude):0.00})");
            }
        }

        IEnumerator King(bool whiff)
        {
            yield return StageFor(PieceKind.King);
            var me = bed.Local;
            var enemy = bed.Dummies(1)[0];
            var from = Starts();
            Show("F · 받아내기 자세");
            me.Skills.PressKey();
            yield return new WaitForFixedUpdate();
            if (whiff) yield return new WaitForSeconds(0.62f);
            else yield return new WaitForSeconds(0.04f);
            Show(whiff ? "적이 늦게 벰" : "적이 벰");
            bed.Cut(enemy, me.Pawn.Hips.position);
            yield return new WaitForSeconds(2.6f);
            Moves(from, whiff ? "king-whiff" : "king");
        }

        /// <summary>The king in the middle of the platform, the enemies far off; F, and 0.3 s later F8 (the bed brings the
        /// nearest enemy up beside him and has it cut; the guard waits for that cut).</summary>
        IEnumerator KingMid()
        {
            yield return StageFor(PieceKind.King);
            var me = bed.Local;
            var enemies = bed.Dummies(1);
            SwordFightSkillBed.Place(me, new Vector3(-0.5f, 0f, -1.5f), Vector3.right);
            SwordFightSkillBed.Place(enemies[0], new Vector3(3.5f, 0f, 1.5f), Vector3.left);
            SwordFightSkillBed.Place(enemies[1], new Vector3(-4.5f, 0f, 4.5f), Vector3.right);
            SwordFightSkillBed.Place(enemies[2], new Vector3(4.5f, 0f, -5f), Vector3.left);
            yield return new WaitForSeconds(0.5f);
            var from = Starts();
            Show("F · 받아내기 자세");
            me.Skills.PressKey();
            yield return new WaitForSeconds(0.3f);
            Show("F8 · 적이 와서 벰");
            bed.EnemyCuts();
            yield return new WaitForSeconds(2.6f);
            Moves(from, "king-mid");
        }

        /// <summary>The bishop aims at an enemy standing in the middle of the platform (nothing behind it but floor), or at an
        /// empty spot (<paramref name="miss"/>); then F8 brings an ally up to cut it.</summary>
        IEnumerator BishopMid(bool miss)
        {
            yield return StageFor(PieceKind.Bishop);
            var me = bed.Local;
            var enemies = bed.Dummies(1);
            var ally = bed.Dummies(0)[0];
            SwordFightSkillBed.Place(me, new Vector3(-3.4f, 0f, -0.6f), Vector3.right);
            SwordFightSkillBed.Place(enemies[0], new Vector3(1.6f, 0f, -0.2f), Vector3.left);
            SwordFightSkillBed.Place(enemies[1], new Vector3(-1f, 0f, 4.5f), Vector3.right);
            SwordFightSkillBed.Place(enemies[2], new Vector3(3.5f, 0f, -5f), Vector3.left);
            SwordFightSkillBed.Place(ally, new Vector3(-4.5f, 0f, 4.5f), Vector3.right);
            yield return new WaitForSeconds(0.5f);
            Vector3 point = miss ? new Vector3(1.2f, 0f, 2.6f) : enemies[0].Pawn.Hips.position;
            me.Skills.OverrideAim(Vector3.right, point);
            var from = Starts();
            yield return Press(me.Skills, 0.7f);
            yield return new WaitForSeconds(bed.skills.bishopWindup + 0.3f);
            var target = enemies[0].Skills;
            Say($"bishop-{(miss ? "miss" : "mid")}: 발동 0.3초 뒤 묶임 {target.PinLeft:0.00}초 남음 · 감속 {target.SlowLeft:0.00}초 남음");
            if (!miss)
            {
                Show("F8 · 아군이 와서 벰");
                bed.EnemyCuts();
            }
            yield return new WaitForSeconds(2.4f);
            Moves(from, miss ? "bishop-miss" : "bishop-mid");
        }

        IEnumerator Line(PieceKind kind, Vector3 dir, Vector3? point)
        {
            yield return StageFor(kind);
            var me = bed.Local;
            me.Skills.OverrideAim(dir, point);
            var from = Starts();
            yield return Press(me.Skills, 0.7f);
            yield return new WaitForSeconds(kind == PieceKind.Rook ? 2.8f : 2.4f);
            Moves(from, kind.ToString());
        }

        // ---------------------------------------------------------------- the edge skills (R103)

        /// <summary>Wait until every fighter is back and the floor whole, then put them where the bed's Shift+F7 does.</summary>
        IEnumerator StageEdgeFor(PieceKind kind)
        {
            var me = bed.Local;
            float waited = 0f;
            while (waited < 10f)
            {
                bool ready = me != null && me.Alive && (bed.Floor == null || bed.Floor.Collapses.Count == 0);
                foreach (var f in bed.Game.Fighters.Values) ready &= f != null && f.Alive;
                if (ready) break;
                waited += Time.deltaTime;
                yield return null;
            }
            bed.SetPiece(me, kind);
            bed.StageEdge(kind);
            yield return new WaitForSeconds(0.2f);
            bed.StageEdge(kind);
            float t = 0f;
            while (t < 1.4f)
            {
                bool guarded = false;
                foreach (var f in bed.Game.Fighters.Values) guarded |= f.Protection > 0f;
                if (!guarded && t > 0.6f) break;
                t += Time.deltaTime;
                yield return null;
            }
            Staged = true;
        }

        void EdgeState(SwordFightSkills s, string when) =>
            Say($"  [{when}] 조건 {(s.EdgeCondition ? "됨" : "안 됨: " + s.EdgeWhy)} · 단계 {s.EdgeStage} {s.EdgeDetail} · 쿨 {s.EdgeCooldown:0.0}/{s.EdgeCooldownTotal:0.0}");

        /// <summary>An enemy pushes the king off the edge; once he has been falling for 0.12 s, E. With
        /// <paramref name="cut"/>, a moment later the enemy comes to the lip and cuts at the hilt.</summary>
        IEnumerator EdgeKing(bool cut)
        {
            yield return StageEdgeFor(PieceKind.King);
            var me = bed.Local;
            var foe = bed.Dummies(1)[0];
            var from = Starts();
            EdgeState(me.Skills, "밀기 전");
            Show("적이 킹을 밀어냄");
            bed.PushOff(foe, me);
            float t = 0f;
            while (me.Skills.FallTime < 0.12f && t < 2.5f) { t += Time.deltaTime; yield return null; }
            Say($"edge-king: 밀고 {t:0.00}초 · 떨어진 지 {me.Skills.FallTime:0.00}초 · 골반 {me.Pawn.Hips.position.y:0.00} m · {me.Pawn.State}");
            EdgeState(me.Skills, "E 직전");
            Show("E · 왕의 귀환");
            me.Skills.PressEdge();
            yield return new WaitForSeconds(0.2f);
            EdgeState(me.Skills, "E 0.2초 뒤");
            Say($"  골반 {me.Pawn.Hips.position.y:0.00} m · 박힌 자리 {me.Skills.Hilt.x:0.00},{me.Skills.Hilt.y:0.00},{me.Skills.Hilt.z:0.00}");
            if (cut)
            {
                Show("F8 · 적이 박힌 칼을 벰");
                bed.EnemyCuts();
            }
            yield return new WaitForSeconds(2.4f);
            EdgeState(me.Skills, "끝");
            Moves(from, cut ? "edge-king-cut" : "edge-king");
        }

        /// <summary>The queen and an enemy 1.4 m from the edge, 4 m off: E. Mode 1: the enemy walks in out of the edge
        /// zone during the gold line (it should be called off); mode 2: it jumps as the cut comes.</summary>
        IEnumerator EdgeQueen(int mode)
        {
            yield return StageEdgeFor(PieceKind.Queen);
            var me = bed.Local;
            var target = bed.Dummies(1)[0];
            // Stepping in: the enemy stands near the zone's inner side (1.75 m from the edge) and walks in as the line comes.
            if (mode == 1) SwordFightSkillBed.Place(target, new Vector3(5.25f, 0f, 0f), Vector3.left);
            me.Skills.OverrideAim(Vector3.right);
            yield return new WaitForSeconds(0.25f);
            var from = Starts();
            EdgeState(me.Skills, "E 직전");
            Show("E · 체크메이트 일섬");
            me.Skills.PressEdge();
            if (mode == 1)
            {
                yield return new WaitForSeconds(0.05f);
                Show("적이 안쪽으로 걸어 들어옴");
                bed.Walk(target, Vector3.left, 0.45f);
            }
            else if (mode == 2)
            {
                yield return new WaitForSeconds(bed.edge.queenLine - 0.22f);
                Show("적이 점프");
                bed.Jump(target);
            }
            yield return new WaitForSeconds(mode == 1 ? 1.2f : 0.3f);
            EdgeState(me.Skills, "뒤");
            yield return new WaitForSeconds(2.0f);
            Moves(from, mode == 0 ? "edge-queen" : mode == 1 ? "edge-queen-step" : "edge-queen-jump");
        }

        /// <summary>The rook by the east edge looks at it and breaks it; once his slam is done he walks in off the chunk.
        /// The two enemies and the ally on it fall with it; then the floor comes back.</summary>
        IEnumerator EdgeRook()
        {
            yield return StageEdgeFor(PieceKind.Rook);
            var me = bed.Local;
            me.Skills.OverrideAim(Vector3.right);
            yield return new WaitForSeconds(0.25f);
            var from = Starts();
            EdgeState(me.Skills, "E 직전");
            Say($"  덩어리 x {me.Skills.EdgeChunk.xMin:0.0}~{me.Skills.EdgeChunk.xMax:0.0} · z {me.Skills.EdgeChunk.yMin:0.0}~{me.Skills.EdgeChunk.yMax:0.0}");
            Show("E · 성벽 붕괴");
            me.Skills.PressEdge();
            float since = 0f;
            yield return new WaitForSeconds(bed.edge.rookSlam + 0.02f);
            since += bed.edge.rookSlam + 0.02f;
            Show("룩이 안쪽으로 걸어 나옴");
            bed.Walk(me, Vector3.left, 0.9f);
            yield return new WaitForSeconds(bed.edge.rookCrack + 1.6f);
            since += bed.edge.rookCrack + 1.6f;
            Moves(from, "edge-rook (무너진 뒤)");
            float t = 0f;
            while (bed.Floor != null && bed.Floor.Collapses.Count > 0 && t < 8f) { t += Time.deltaTime; yield return null; }
            since += t;
            Say($"  바닥이 다시 솟아 원래대로: E 뒤 {since:0.0}초 (무너지고 약 {since - bed.edge.rookCrack:0.0}초)");
            yield return new WaitForSeconds(0.4f);
        }

        /// <summary>An enemy pushes the bishop's ally off the edge; once it has been falling 0.15 s, E. With
        /// <paramref name="hit"/>, an enemy comes up and cuts the bishop while the hands pull.</summary>
        /// <summary>An ally pushed off the east edge, the bishop E. With <paramref name="hit"/> an enemy cuts the bishop during
        /// the pull; with <paramref name="close"/> the bishop stands where the ally would be set down (R109: it used to be set
        /// on him and shoved back off).</summary>
        IEnumerator EdgeBishop(bool hit, bool close = false)
        {
            yield return StageEdgeFor(PieceKind.Bishop);
            var me = bed.Local;
            var ally = bed.Dummies(0)[0];
            var foes = bed.Dummies(1);
            var from = Starts();
            Show("적이 아군을 밀어 떨어뜨림");
            bed.PushOff(foes[0], ally);
            float t = 0f;
            while (ally.Skills.FallTime < 0.15f && t < 2.5f) { t += Time.deltaTime; yield return null; }
            Say($"edge-bishop: 아군이 떨어진 지 {ally.Skills.FallTime:0.00}초 · 골반 {ally.Pawn.Hips.position.y:0.00} m");
            if (close)
            {
                // Where the ally would be set down (1.2 m in from the east edge, by the ally): the bishop stands there.
                Vector3 spot = ally.Pawn.Hips.position;
                spot.x = 7f - bed.edge.bishopSetIn;
                spot.y = 0f;
                SwordFightSkillBed.Place(me, spot, Vector3.right);
                yield return new WaitForSeconds(0.08f);
            }
            EdgeState(me.Skills, "E 직전");
            Show("E · 구원의 손");
            me.Skills.PressEdge();
            if (hit)
            {
                yield return new WaitForSeconds(bed.edge.bishopReach + 0.05f);
                Show("적이 비숍을 벰");
                // From the edge's side, so the cut throws the bishop in (not off the platform as well).
                Vector3 at = me.Pawn.Hips.position + Vector3.right * 0.9f;
                at.y = 0f;
                SwordFightSkillBed.Place(foes[1], at, Vector3.left);
                bed.Cut(foes[1], me.Pawn.Hips.position);
            }
            yield return new WaitForSeconds(1.2f);
            EdgeState(me.Skills, "뒤");
            Say($"  아군: {(ally.Alive ? "살아 있음" : "장외")} · 골반 ({ally.Pawn.Hips.position.x:0.0}, {ally.Pawn.Hips.position.y:0.00}, {ally.Pawn.Hips.position.z:0.0}) · {ally.Pawn.State}");
            yield return new WaitForSeconds(1.4f);
            Say($"  비숍 ↔ 아군 {Vector3.ProjectOnPlane(me.Pawn.Hips.position - ally.Pawn.Hips.position, Vector3.up).magnitude:0.00} m · 비숍 {me.Pawn.State}");
            Moves(from, hit ? "edge-bishop-hit" : close ? "edge-bishop-close" : "edge-bishop");
        }

        /// <summary>The knight with its back to the east edge, an enemy 1.9 m ahead: E. With <paramref name="back"/>, the
        /// enemy stands 2.85 m off and backs away during the crouch (it should be called off).</summary>
        /// <summary>The knight with its back to the east edge, an enemy ahead: E. With <paramref name="back"/>, the enemy
        /// stands 2.85 m off and backs away during the crouch (it should be called off). R109: <paramref name="mode"/> 1 = the
        /// camera looks along the edge, not inward; 2 = a dummy knocks the knight off the edge first and E goes in the fall.
        /// </summary>
        IEnumerator EdgeKnight(bool back, int mode = 0)
        {
            yield return StageEdgeFor(PieceKind.Knight);
            var me = bed.Local;
            var foe = bed.Dummies(1)[0];
            if (back) SwordFightSkillBed.Place(foe, new Vector3(3.15f, 0f, 0.1f), Vector3.right);
            me.Skills.OverrideAim(mode == 1 ? Vector3.forward : Vector3.left);
            yield return new WaitForSeconds(0.3f);
            var from = Starts();
            if (mode == 2)
            {
                Show("적이 나이트를 밀어 떨어뜨림");
                bed.PushOff(bed.Dummies(1)[1], me);
                float t = 0f;
                while (me.Skills.FallTime < 0.12f && t < 2.5f) { t += Time.deltaTime; yield return null; }
                Say($"edge-knight-fall: 떨어진 지 {me.Skills.FallTime:0.00}초 · 골반 ({me.Pawn.Hips.position.x:0.0}, {me.Pawn.Hips.position.y:0.00}) · {me.Pawn.State}");
            }
            EdgeState(me.Skills, "E 직전");
            Show("E · 벼랑 끝 역전");
            me.Skills.PressEdge();
            if (back)
            {
                yield return new WaitForSeconds(0.05f);
                Show("적이 뒤로 물러남");
                bed.Walk(foe, Vector3.left, 0.8f);
            }
            yield return new WaitForSeconds(0.6f);
            EdgeState(me.Skills, "E 0.65초 뒤");
            yield return new WaitForSeconds(2.0f);
            Say($"  나이트: {(me.Alive ? "살아 있음" : "장외")} · 골반 ({me.Pawn.Hips.position.x:0.0}, {me.Pawn.Hips.position.y:0.00}, {me.Pawn.Hips.position.z:0.0}) · 적: {(foe.Alive ? "살아 있음" : "장외")}");
            Moves(from, back ? "edge-knight-back" : mode == 1 ? "edge-knight-side" : mode == 2 ? "edge-knight-fall" : "edge-knight");
        }

        IEnumerator Bishop()
        {
            yield return StageFor(PieceKind.Bishop);
            var me = bed.Local;
            var enemy = bed.Dummies(1)[0];
            var ally = bed.Dummies(0)[0];
            me.Skills.OverrideAim(Vector3.right, enemy.Pawn.Hips.position);
            var from = Starts();
            yield return Press(me.Skills, 0.7f);
            yield return new WaitForSeconds(bed.skills.bishopWindup + 0.12f);
            Show("아군이 벰 (묶인 적)");
            bed.Cut(ally, enemy.Pawn.Hips.position);
            yield return new WaitForSeconds(2.6f);
            Moves(from, "bishop");
        }
    }
}
