using System.Collections.Generic;
using System.Globalization;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The course 01 v0.2 checks (design doc v0.2 §8 "완료 확인"), run on the built course from the
    // menu ChessFight > Pawn Rush > Validate Course01 (Edit mode: obstacles in their rest poses).
    //   1. two floors stacked one over the other are at least 9 m apart (ramps: reported only)
    //   2. no team zone overlaps a shared floor or ramp
    //   3. white's and black's progress paths are within 0.1 m of each other
    //   4. no gap over 2.5 m along the required path (a "gap": nothing within 4 m below)
    //   5. floor under every checkpoint's six respawn spots
    //   6. every slot's door at its exit height, and a mini-game in every slot
    public static class Course01Validator
    {
        public const float MinStack = 9f, MaxRequiredGap = 2.5f, PathTolerance = .1f;

        static readonly RaycastHit[] hits = new RaycastHit[32];

        public static List<string> Run(PawnRushCourse course, List<string> notes)
        {
            var errors = new List<string>();
            Physics.SyncTransforms();
            var root = course.Root;
            if (root == null)
            {
                errors.Add("코스가 없습니다. 먼저 Build Course01 v2를 실행하세요.");
                return errors;
            }

            // 1. stacked floors
            var floors = root.GetComponentsInChildren<CourseFloor>(false);
            int pairs = 0;
            for (int i = 0; i < floors.Length; i++)
                for (int j = i + 1; j < floors.Length; j++)
                {
                    Bounds a = Box(floors[i]), c = Box(floors[j]);
                    if (!OverlapXZ(a, c)) continue;
                    // The higher one's top over the lower one's; a block standing on another is one wall.
                    var lower = a.max.y <= c.max.y ? floors[i] : floors[j];
                    var upper = lower == floors[i] ? floors[j] : floors[i];
                    Bounds lo = Box(lower), up = Box(upper);
                    if (up.min.y <= lo.max.y + .05f) continue;
                    // Only when the lower top is open to the upper floor: a block in between (the tower's
                    // stepped north face) makes it one wall, not two floors.
                    var probe = new Vector3((Mathf.Max(lo.min.x, up.min.x) + Mathf.Min(lo.max.x, up.max.x)) * .5f, up.min.y - .02f,
                                            (Mathf.Max(lo.min.z, up.min.z) + Mathf.Min(lo.max.z, up.max.z)) * .5f);
                    var lowerCollider = lower.GetComponent<Collider>();
                    if (lowerCollider != null && Physics.Raycast(probe, Vector3.down, out var first, up.min.y - lo.max.y + .5f, ~0,
                            QueryTriggerInteraction.Ignore) && first.collider != lowerCollider) continue;
                    pairs++;
                    float gap = up.max.y - lo.max.y;
                    if (gap >= MinStack - .01f) continue;
                    string line = $"겹친 바닥 높이 차 {gap:0.0} m < {MinStack} m: {Name(upper)} 위, {Name(lower)} 아래";
                    if (upper.sloped || lower.sloped) notes.Add("(경사로, 오류 아님) " + line);
                    else errors.Add(line);
                }
            notes.Add($"위아래로 겹친 바닥 {pairs}쌍 검사");

            // 2. team zones vs shared floors
            var zones = root.GetComponentsInChildren<TeamZone>(true);
            foreach (var zone in zones)
            {
                var zb = zone.Bounds;
                foreach (var f in floors)
                {
                    if (InTeamSection(f.transform, root)) continue;
                    var fb = Box(f);
                    if (zb.min.x < fb.max.x - .01f && zb.max.x > fb.min.x + .01f && zb.min.y < fb.max.y - .01f &&
                        zb.max.y > fb.min.y + .01f && zb.min.z < fb.max.z - .01f && zb.max.z > fb.min.z + .01f)
                        errors.Add($"{zone.name}({Teams.Name(zone.Team)})이 공용 바닥 {Name(f)}과 겹칩니다");
                }
            }
            notes.Add($"팀 구역 {zones.Length}개");

            // 3. equal paths, 4. gaps along them
            float white = ProgressPath.Length(course.Path(Teams.White)), black = ProgressPath.Length(course.Path(Teams.Black));
            if (Mathf.Abs(white - black) > PathTolerance) errors.Add($"두 팀 경로 길이 차 {Mathf.Abs(white - black):0.00} m > {PathTolerance} m");
            var progress = course.Progress;
            if (progress != null)
                foreach (var section in new[] { "S+A", "W1", "B", "W2", "C", "T" })
                    notes.Add($"구간 {section} 시작: 경로 {progress.SectionStart(section, Teams.White):0} m");
            for (int team = 0; team < 2; team++)
            {
                var path = course.Path(team);
                float run = 0f, worst = 0f;
                Vector3 worstAt = Vector3.zero;
                for (int i = 1; i < path.Count; i++)
                {
                    Vector3 a = path[i - 1], c = path[i];
                    var flat = new Vector3(c.x - a.x, 0f, c.z - a.z);
                    if (flat.magnitude < .01f) { run = 0f; continue; }   // a climb
                    float len = Vector3.Distance(a, c);
                    for (float d = 0f; d <= len; d += .05f)
                    {
                        var p = Vector3.Lerp(a, c, d / len);
                        if (FloorAt(p, 1f, 5f)) run = 0f;
                        else
                        {
                            run += .05f;
                            if (run > worst) { worst = run; worstAt = p; }
                        }
                    }
                }
                if (worst > MaxRequiredGap + .1f) errors.Add($"필수 경로({Teams.Name(team)}) 틈 {worst:0.0} m > {MaxRequiredGap} m {Fmt(worstAt)}");
                notes.Add($"{Teams.Name(team)}팀 진행 경로 {ProgressPath.Length(path):0.0} m, 가장 넓은 틈 {worst:0.00} m");
            }

            // 5. checkpoints
            int spots = 0;
            var checkpoints = root.GetComponentsInChildren<CourseCheckpoint>(true);
            foreach (var cp in checkpoints)
                for (int i = 0; i < cp.SpotCount; i++)
                {
                    spots++;
                    if (!FloorAt(cp.SpotWorld(i), 1f, 2.5f))
                        errors.Add($"CP{cp.Number}({(cp.Shared ? "공용" : Teams.Name(cp.Team))}) 부활 자리 {i + 1} 아래에 바닥이 없습니다 {Fmt(cp.SpotWorld(i))}");
                }
            notes.Add($"체크포인트 {checkpoints.Length}개, 부활 자리 {spots}곳");

            // 6. slots
            foreach (var slot in root.GetComponentsInChildren<MiniGameSlot>(true))
            {
                if (slot.Door == null) { errors.Add($"슬롯 {slot.Slot}({Teams.Name(slot.Team)})에 문이 없습니다"); continue; }
                float doorY = slot.transform.InverseTransformPoint(slot.Door.transform.position).y - 1.75f;
                if (Mathf.Abs(doorY - slot.ExitHeight) > .05f)
                    errors.Add($"슬롯 {slot.Slot}({Teams.Name(slot.Team)}) 문 높이 {doorY:0.00} ≠ 출구 높이 {slot.ExitHeight}");
                if (slot.Game == null) errors.Add($"슬롯 {slot.Slot}({Teams.Name(slot.Team)})에 미니게임이 없습니다");
            }
            if (root.GetComponentInChildren<KillVolume>(true) == null) errors.Add("낙사 영역(y −10)이 없습니다");
            return errors;
        }

        // A floor's world box (its renderer's: the collider of an inactive one has no bounds).
        static Bounds Box(CourseFloor f)
        {
            var r = f.GetComponent<Renderer>();
            if (r != null) return r.bounds;
            var c = f.GetComponent<Collider>();
            return c != null ? c.bounds : new Bounds(f.transform.position, Vector3.zero);
        }

        static bool OverlapXZ(Bounds a, Bounds b) =>
            Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x) > .3f &&
            Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z) > .3f;

        // Under W1_White, W1_Black, W2_White or W2_Black.
        static bool InTeamSection(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
                if (t.parent == root && t.name.StartsWith("W")) return true;
            return false;
        }

        static string Name(CourseFloor f) => (f.transform.parent != null ? f.transform.parent.name + "/" : "") + f.name;

        // Any solid (non-trigger) surface under `point + up * above`, within `depth` of it.
        static bool FloorAt(Vector3 point, float above, float depth) =>
            Physics.RaycastNonAlloc(point + Vector3.up * above, Vector3.down, hits, depth, ~0, QueryTriggerInteraction.Ignore) > 0;

        static string Fmt(Vector3 p) => string.Format(CultureInfo.InvariantCulture, "({0:0.0}, {1:0.0}, {2:0.0})", p.x, p.y, p.z);
    }
}
