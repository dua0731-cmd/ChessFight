using System.Collections.Generic;
using System.Globalization;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The course 01 v0.4 checks (design doc v0.4 "완료 확인"), run on the built course from the menu
    // ChessFight > Pawn Rush > Validate Course01 (Edit mode: obstacles in their rest poses).
    //   1. every obstacle's reach covers the whole way across where it stands (ObstacleSpan: the
    //      walkable floor, or the climbable face, measured by rays)
    //   2. white's and black's progress paths are within 0.1 m of each other
    //   3. no gap over 2.5 m along either path (a "gap": nothing within 4 m below)
    //   4. floor under every checkpoint's six respawn spots
    //   5. four stations, each with its gate, barrier and a game; every one of the five games, built
    //      into every station, stays inside the station's 20 x 20 m
    //   6. unclimbable surfaces: marble always with NoClimbSurface, climbable stone never; the plaza
    //      north blocks, the clock gate walls and the tower's marble are unclimbable
    //   7. a kill volume under the course
    public static class Course01Validator
    {
        public const float MaxRequiredGap = 2.5f, PathTolerance = .1f, SpanTolerance = .05f;

        static readonly RaycastHit[] hits = new RaycastHit[64];

        public static List<string> Run(PawnRushCourse course, List<string> notes)
        {
            var errors = new List<string>();
            Physics.SyncTransforms();
            var root = course.Root;
            if (root == null)
            {
                errors.Add("코스가 없습니다. 먼저 Build Course01 v4를 실행하세요.");
                return errors;
            }

            // 1. obstacle reach
            var groups = new Dictionary<string, List<ObstacleSpan>>();
            foreach (var span in root.GetComponentsInChildren<ObstacleSpan>(true))
            {
                if (!groups.TryGetValue(span.Group, out var list)) groups[span.Group] = list = new List<ObstacleSpan>();
                list.Add(span);
            }
            foreach (var kv in groups) CheckSpan(kv.Value, errors, notes);
            notes.Add($"장애물 덮음 검사 {groups.Count}곳");

            // 2. equal paths, 3. gaps along them
            float white = ProgressPath.Length(course.Path(Teams.White)), black = ProgressPath.Length(course.Path(Teams.Black));
            if (Mathf.Abs(white - black) > PathTolerance) errors.Add($"두 팀 경로 길이 차 {Mathf.Abs(white - black):0.00} m > {PathTolerance} m");
            var progress = course.Progress;
            if (progress != null)
                foreach (var section in Course01v4Builder.Sections)
                    notes.Add($"구간 {section} 시작: 경로 {progress.SectionStart(section, Teams.White):0} m");
            for (int team = 0; team < 2; team++)
            {
                var path = course.Path(team);
                float run = 0f, worst = 0f;
                Vector3 worstAt = Vector3.zero;
                Collider near = null;
                for (int i = 1; i < path.Count; i++)
                {
                    Vector3 a = path[i - 1], c = path[i];
                    var flat = new Vector3(c.x - a.x, 0f, c.z - a.z);
                    if (flat.magnitude < .01f) { run = 0f; continue; }   // a climb
                    float len = Vector3.Distance(a, c);
                    for (float d = 0f; d <= len; d += .05f)
                    {
                        var p = Vector3.Lerp(a, c, d / len);
                        var floor = FloorUnder(p, 1f, 5f);
                        if (floor == null)
                        {
                            run += .05f;
                            continue;
                        }
                        // A gap crossed on the line can be shorter on another: the path runs straight
                        // past the A3 discs, which sit either side of it. Measure between the two banks.
                        float gap = run > MaxRequiredGap && near != null ? Between(near, floor) : run;
                        if (gap > worst) { worst = gap; worstAt = p; }
                        run = 0f;
                        near = floor;
                    }
                }
                if (worst > MaxRequiredGap + .05f) errors.Add($"필수 경로({Teams.Name(team)}) 틈 {worst:0.00} m > {MaxRequiredGap} m {Fmt(worstAt)}");
                notes.Add($"{Teams.Name(team)}팀 진행 경로 {ProgressPath.Length(path):0.0} m, 가장 넓은 틈 {worst:0.00} m");
            }

            // 4. checkpoints
            int spots = 0;
            var checkpoints = root.GetComponentsInChildren<CourseCheckpoint>(true);
            foreach (var cp in checkpoints)
                for (int i = 0; i < cp.SpotCount; i++)
                {
                    spots++;
                    if (!FloorAt(cp.SpotWorld(i), 1f, 2.5f))
                        errors.Add($"CP{cp.Number} 부활 자리 {i + 1} 아래에 바닥이 없습니다 {Fmt(cp.SpotWorld(i))}");
                }
            notes.Add($"체크포인트 {checkpoints.Length}개, 부활 자리 {spots}곳");

            // 6. climbing, before 5 rebuilds the games
            CheckClimbing(root, course.Kit, errors, notes);
            if (root.GetComponentInChildren<KillVolume>(true) == null) errors.Add("낙사 영역(y −10)이 없습니다");

            // 5. stations and games
            var stations = root.GetComponentsInChildren<MissionStation>(true);
            if (stations.Length != 4) errors.Add($"미션 스테이션이 {stations.Length}개입니다 (4개여야 함)");
            foreach (var s in stations)
            {
                string who = $"광장 {s.Plaza} {Teams.Name(s.Team)} 스테이션";
                if (s.Gate == null) errors.Add(who + "에 팀 문이 없습니다");
                if (s.Socket == null) { errors.Add(who + "에 미니게임 자리가 없습니다"); continue; }
                var barrier = s.GetComponentInChildren<TeamBarrier>(true);
                if (barrier == null || barrier.Team != s.Team) errors.Add(who + "의 팀 문 통로에 같은 팀 장벽이 없습니다");
                if (s.Game == null) errors.Add(who + "에 미니게임이 없습니다");
                int keep = s.GameIndex;
                for (int g = 0; g < PawnRushMissions.Count; g++)
                {
                    var game = s.Install(g);
                    if (game == null) { errors.Add($"{who}: {PawnRushMissions.Ids[g]}를 만들지 못했습니다"); continue; }
                    var outside = Outside(s, game.transform);
                    if (outside != null) errors.Add($"{who}: {PawnRushMissions.Ids[g]}의 {outside}이(가) 스테이션 밖으로 나갑니다");
                }
                s.Install(keep);
            }
            notes.Add($"미니게임 {PawnRushMissions.Count}종 × 스테이션 {stations.Length}곳 크기 검사");
            Physics.SyncTransforms();
            return errors;
        }

        // ------------------------------------------------------------------ 1. reach

        static void CheckSpan(List<ObstacleSpan> spans, List<string> errors, List<string> notes)
        {
            var first = spans[0];
            var axis = first.Across;
            var origin = first.transform.position;
            // The way across, measured from the first one's position in 0.1 m steps both ways.
            float lo = 0f, hi = 0f;
            bool any = Walkable(first, origin);
            if (any)
            {
                for (float s = .1f; s < 30f && Walkable(first, origin + axis * s); s += .1f) hi = s;
                for (float s = -.1f; s > -30f && Walkable(first, origin + axis * s); s -= .1f) lo = s;
            }
            string name = spans.Count > 1 ? first.Group : first.name;
            if (!any)
            {
                errors.Add($"{name}: 장애물 자리에서 {(first.Kind == ObstacleSpan.Probe.Face ? "오르는 면" : "바닥")}을 찾지 못했습니다 {Fmt(origin)}");
                return;
            }
            // Every step of the way inside one span's reach.
            for (float s = lo; s <= hi + 1e-3f; s += .1f)
            {
                bool covered = false;
                foreach (var span in spans)
                {
                    float at = Vector3.Dot(span.transform.position - origin, axis);
                    if (s >= at + span.ReachMin - SpanTolerance && s <= at + span.ReachMax + SpanTolerance) { covered = true; break; }
                }
                if (covered) continue;
                errors.Add($"{name}: 길 폭 {hi - lo + .1f:0.0} m 중 {Fmt(origin + axis * s)}를 장애물이 덮지 못합니다");
                return;
            }
            notes.Add($"{name}: 길 폭 {hi - lo + .1f:0.0} m 모두 덮음");
        }

        static bool Walkable(ObstacleSpan span, Vector3 at)
        {
            if (span.Kind == ObstacleSpan.Probe.Face)
            {
                // The nearest surface ahead that is not an obstacle's own part.
                var from = new Vector3(at.x, span.ProbeY, at.z);
                int m = Physics.RaycastNonAlloc(from, span.FaceDirection, hits, 3f, ~0, QueryTriggerInteraction.Ignore);
                Collider face = null;
                float nearest = float.MaxValue;
                for (int i = 0; i < m; i++)
                    if (!IsObstacle(hits[i].collider) && hits[i].distance < nearest) { nearest = hits[i].distance; face = hits[i].collider; }
                return face != null && face.GetComponentInParent<NoClimbSurface>() == null;
            }
            float y = span.ProbeY;
            int n = Physics.RaycastNonAlloc(new Vector3(at.x, y + 20f, at.z), Vector3.down, hits, 22f, ~0, QueryTriggerInteraction.Ignore);
            bool floor = false;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (IsObstacle(c)) continue;
                float top = hits[i].point.y;
                if (c.GetComponent<CourseFloor>() != null && Mathf.Abs(top - y) <= .6f) floor = true;
                else if (top > y + .3f && c.bounds.min.y < y + 1.5f) return false;   // a wall stands there
            }
            return floor;
        }

        static bool IsObstacle(Collider c) =>
            c.GetComponentInParent<ObstacleSpan>() != null || c.GetComponentInParent<ObstacleContext>() != null ||
            c.GetComponentInParent<ObstacleMotion>() != null;

        // ------------------------------------------------------------------ 5. games inside stations

        // The first part of a game sticking out of its station (u 0..20, v -10..10), or null.
        static string Outside(MissionStation s, Transform game)
        {
            Physics.SyncTransforms();
            foreach (var r in game.GetComponentsInChildren<Renderer>(true))
            {
                var b = r.bounds;
                Vector3 min = s.transform.InverseTransformPoint(b.min), max = s.transform.InverseTransformPoint(b.max);
                if (Mathf.Min(min.x, max.x) < -MissionStation.Half - .05f || Mathf.Max(min.x, max.x) > MissionStation.Half + .05f ||
                    Mathf.Min(min.z, max.z) < -.05f || Mathf.Max(min.z, max.z) > MissionStation.Size + .05f || Mathf.Max(min.y, max.y) > 12.5f)
                    return r.name;
            }
            return null;
        }

        // ------------------------------------------------------------------ 6. climbing

        static readonly string[] MustBeMarble = { "North block", "Clock gate wall", "Tower core", "Tower shoulder", "Outer wall", "Team gate" };

        static void CheckClimbing(Transform root, Course01Kit kit, List<string> errors, List<string> notes)
        {
            int marble = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var c = r.GetComponent<Collider>();
                if (c == null || c.isTrigger || IsObstacle(c)) continue;
                bool noClimb = r.GetComponent<NoClimbSurface>() != null;
                if (kit != null && r.sharedMaterial == kit.wallNoClimb && !noClimb) errors.Add($"{Name(r.transform)}: 대리석인데 NoClimbSurface가 없습니다");
                if (kit != null && r.sharedMaterial == kit.wallClimbable && noClimb) errors.Add($"{Name(r.transform)}: 돌 블록인데 NoClimbSurface가 있습니다");
                foreach (var prefix in MustBeMarble)
                    if (r.name.StartsWith(prefix) && !noClimb) errors.Add($"{Name(r.transform)}: 오를 수 없어야 합니다");
                if (noClimb) marble++;
            }
            notes.Add($"오를 수 없는 면 {marble}개");
        }

        // ------------------------------------------------------------------ helpers

        static string Name(Transform t) => (t.parent != null ? t.parent.name + "/" : "") + t.name;

        // The nearest solid surface under `point + up * above`, within `depth` of it.
        static Collider FloorUnder(Vector3 point, float above, float depth) =>
            Physics.Raycast(point + Vector3.up * above, Vector3.down, out var hit, depth, ~0, QueryTriggerInteraction.Ignore) ? hit.collider : null;

        // Level distance between two (convex) floors: closest points found by projecting back and forth.
        static float Between(Collider a, Collider b)
        {
            var q = b.bounds.center;
            Vector3 pa = q;
            for (int i = 0; i < 24; i++)
            {
                pa = Closest(a, q);
                q = Closest(b, pa);
            }
            var d = q - pa;
            d.y = 0f;
            return d.magnitude;
        }

        // Collider.ClosestPoint, but a concave mesh (the 01 disc's cylinder) is read as the upright disc
        // its bounds hold.
        static Vector3 Closest(Collider c, Vector3 q)
        {
            if (!(c is MeshCollider mesh) || mesh.convex) return c.ClosestPoint(q);
            var b = c.bounds;
            var d = new Vector3(q.x - b.center.x, 0f, q.z - b.center.z);
            float r = Mathf.Min(b.extents.x, b.extents.z);
            if (d.magnitude > r) d = d.normalized * r;
            return new Vector3(b.center.x + d.x, Mathf.Clamp(q.y, b.min.y, b.max.y), b.center.z + d.z);
        }

        // Any solid (non-trigger) surface under `point + up * above`, within `depth` of it.
        static bool FloorAt(Vector3 point, float above, float depth) =>
            Physics.RaycastNonAlloc(point + Vector3.up * above, Vector3.down, hits, depth, ~0, QueryTriggerInteraction.Ignore) > 0;

        static string Fmt(Vector3 p) => string.Format(CultureInfo.InvariantCulture, "({0:0.0}, {1:0.0}, {2:0.0})", p.x, p.y, p.z);
    }
}
