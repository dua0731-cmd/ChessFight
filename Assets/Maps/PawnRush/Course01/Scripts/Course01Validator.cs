using System.Collections.Generic;
using System.Globalization;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The course checks (design doc §6 "완료 확인"), by casting rays at the built course. Run from
    // the menu ChessFight > Pawn Rush > Validate Course01 (Edit mode: obstacles in their rest poses).
    //   1. every module's exit floor is at the next module's entry height
    //   2. no gap over 2.5 m along the required path (a "gap": nothing within 4 m below)
    //   3. floor under every checkpoint's six respawn spots
    //   4. a kill volume under every cliff
    //   5. both teams' paths are the same length
    //   6. every slot's door at its exit height, whichever exit mode
    public static class Course01Validator
    {
        public const float MaxRequiredGap = 2.5f;

        static readonly RaycastHit[] hits = new RaycastHit[32];

        public static List<string> Run(PawnRushCourse course, List<string> notes)
        {
            var errors = new List<string>();
            Physics.SyncTransforms();
            var modules = course.Modules;
            if (modules.Count == 0)
            {
                errors.Add("코스에 모듈이 없습니다. 먼저 Build Course01을 실행하세요.");
                return errors;
            }

            // 1. heights
            for (int i = 0; i + 1 < modules.Count; i++)
            {
                CourseModule a = modules[i], b = modules[i + 1];
                float expected = a.transform.position.y + a.deltaY;
                if (Mathf.Abs(b.transform.position.y - expected) > .01f)
                    errors.Add($"{a.Code}→{b.Code}: 다음 모듈 원점 높이 {b.transform.position.y:0.00} ≠ {expected:0.00}");
                for (int team = 0; team < 2; team++)
                {
                    if (!a.isTeamModule && !b.isTeamModule && team == 1) continue;
                    var exit = a.transform.TransformPoint(a.PathPoint(a.pathPoints.Length - 1, team) - Vector3.forward * .6f);
                    var entry = b.transform.TransformPoint(b.PathPoint(0, team) + Vector3.forward * .6f);
                    if (!FloorAt(exit, 2f, 3f, out float exitY) || Mathf.Abs(exitY - expected) > .05f)
                        errors.Add($"{a.Code} 출구({Teams.Name(team)}) 바닥 {Show(exitY, exit)} ≠ {expected:0.00}");
                    if (!FloorAt(entry, 2f, 3f, out float entryY) || Mathf.Abs(entryY - b.transform.position.y) > .05f)
                        errors.Add($"{b.Code} 입구({Teams.Name(team)}) 바닥 {Show(entryY, entry)} ≠ {b.transform.position.y:0.00}");
                }
            }

            // 2. gaps along the required path, 5. equal paths
            for (int team = 0; team < 2; team++)
            {
                var path = course.Path(team);
                float run = 0f, worst = 0f;
                Vector3 worstAt = Vector3.zero;
                for (int i = 1; i < path.Count; i++)
                {
                    Vector3 a = path[i - 1], b = path[i];
                    var flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
                    if (flat.magnitude < .01f) { run = 0f; continue; }   // a climb
                    float len = Vector3.Distance(a, b);
                    for (float d = 0f; d <= len; d += .05f)
                    {
                        var p = Vector3.Lerp(a, b, d / len);
                        if (FloorAt(p, 1f, 5f, out _)) run = 0f;
                        else
                        {
                            run += .05f;
                            if (run > worst) { worst = run; worstAt = p; }
                        }
                    }
                }
                if (worst > MaxRequiredGap + .1f)
                    errors.Add($"필수 경로({Teams.Name(team)}) 틈 {worst:0.0} m > {MaxRequiredGap} m ({Where(course, worstAt)})");
                notes.Add($"{Teams.Name(team)}팀 진행 경로 {PawnRushCourse.Length(path):0.0} m, 가장 넓은 틈 {worst:0.00} m");
            }
            float white = PawnRushCourse.Length(course.Path(Teams.White)), black = PawnRushCourse.Length(course.Path(Teams.Black));
            if (Mathf.Abs(white - black) > .01f) errors.Add($"두 팀 경로 길이가 다릅니다: 백 {white:0.00} / 흑 {black:0.00}");

            // 3. checkpoints
            int spots = 0;
            foreach (var cp in course.GetComponentsInChildren<CourseCheckpoint>(true))
                for (int i = 0; i < cp.SpotCount; i++)
                {
                    spots++;
                    if (!FloorAt(cp.SpotWorld(i), 1f, 2.5f, out _))
                        errors.Add($"CP{cp.Number}({(cp.Shared ? "공용" : Teams.Name(cp.Team))}) 부활 자리 {i + 1} 아래에 바닥이 없습니다 {Fmt(cp.SpotWorld(i))}");
                }
            notes.Add($"체크포인트 {course.GetComponentsInChildren<CourseCheckpoint>(true).Length}개, 부활 자리 {spots}곳");

            // 4. kill volumes under cliffs
            var kills = course.GetComponentsInChildren<KillVolume>(true);
            int cliffs = 0;
            foreach (var m in modules)
            {
                int missing = 0;
                Vector3 first = Vector3.zero;
                for (float z = .5f; z < m.length; z += 1f)
                    for (float x = -20f; x <= 20f; x += 1f)
                    {
                        var top = m.transform.TransformPoint(new Vector3(x, 40f, z));
                        if (FloorAt(top, 0f, 47f, out _)) continue;
                        cliffs++;
                        var below = m.transform.TransformPoint(new Vector3(x, -8f, z));
                        bool covered = false;
                        foreach (var k in kills) if (k.Bounds.Contains(below)) { covered = true; break; }
                        if (!covered && missing++ == 0) first = below;
                    }
                if (missing > 0) errors.Add($"{m.Code}: 낙사 영역이 없는 낭떠러지 {missing}곳 (예: {Fmt(first)})");
            }
            notes.Add($"낭떠러지 표본 {cliffs}곳 검사, 낙사 영역 {kills.Length}개");

            // 6. slot doors
            foreach (var slot in course.GetComponentsInChildren<MiniGameSlot>(true))
            {
                if (slot.Door == null) { errors.Add($"슬롯 {slot.Slot}({Teams.Name(slot.Team)})에 문이 없습니다"); continue; }
                float doorY = slot.transform.InverseTransformPoint(slot.Door.transform.position).y - 1.75f;
                if (Mathf.Abs(doorY - slot.ExitHeight) > .05f)
                    errors.Add($"슬롯 {slot.Slot}({Teams.Name(slot.Team)}) 문 높이 {doorY:0.00} ≠ 출구 높이 {slot.ExitHeight}");
                if (slot.Game == null) errors.Add($"슬롯 {slot.Slot}({Teams.Name(slot.Team)})에 미니게임이 없습니다");
            }
            notes.Add($"모듈 {modules.Count}개, 결승까지 높이 {modules[modules.Count - 1].transform.position.y + modules[modules.Count - 1].deltaY - modules[0].transform.position.y:0} m");
            return errors;
        }

        // The highest solid (non-trigger) top under `point + up * above`, within `depth` of it.
        static bool FloorAt(Vector3 point, float above, float depth, out float y)
        {
            y = float.NaN;
            int n = Physics.RaycastNonAlloc(point + Vector3.up * above, Vector3.down, hits, depth, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (hits[i].distance < best) { best = hits[i].distance; y = hits[i].point.y; }
            return n > 0;
        }

        static string Where(PawnRushCourse course, Vector3 p)
        {
            var m = course.ModuleAt(p);
            return (m != null ? m.Code + " " : "") + Fmt(p);
        }

        static string Show(float y, Vector3 at) => float.IsNaN(y) ? "없음 " + Fmt(at) : y.ToString("0.00", CultureInfo.InvariantCulture);
        static string Fmt(Vector3 p) => string.Format(CultureInfo.InvariantCulture, "({0:0.0}, {1:0.0}, {2:0.0})", p.x, p.y, p.z);
    }
}
