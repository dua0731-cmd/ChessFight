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
    /// Runs: calibrate, king, king-whiff, queen, rook, bishop, knight. Needs Play mode in SwordFight_SkillTest.
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
                "queen" => Line(PieceKind.Queen, Vector3.right, null),
                "rook" => Line(PieceKind.Rook, Vector3.right, null),
                "bishop" => Bishop(),
                "knight" => Line(PieceKind.Knight, Vector3.right, new Vector3(5.0f, 0f, 0f)),
                _ => null,
            };
            if (body == null) { Status = "done: unknown run " + name; yield break; }
            yield return body;
            SwordFightSkills.Log -= OnLog;
            foreach (var l in lines) Say("  " + l);
            var me = bed.Local;
            if (me != null && me.Skills != null) { me.Skills.SetButton(false); me.Skills.OverrideAim(null); }
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

        IEnumerator Press(SwordFightSkills s, float hold, string aimHint = "우클릭 누름 · 조준", string goHint = "우클릭 뗌 · 발동")
        {
            Show(aimHint);
            s.SetButton(true);
            yield return new WaitForSeconds(hold);
            Show(goHint);
            s.SetButton(false);
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
            Show("우클릭 · 받아내기 자세");
            me.Skills.SetButton(true);
            yield return new WaitForFixedUpdate();
            me.Skills.SetButton(false);
            if (whiff) yield return new WaitForSeconds(0.62f);
            else yield return new WaitForSeconds(0.04f);
            Show(whiff ? "적이 늦게 벰" : "적이 벰");
            bed.Cut(enemy, me.Pawn.Hips.position);
            yield return new WaitForSeconds(2.6f);
            Moves(from, whiff ? "king-whiff" : "king");
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
