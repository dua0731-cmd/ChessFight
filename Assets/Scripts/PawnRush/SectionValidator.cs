using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Checks for a section prefab (the grand stair hall level design v0.3 §8 "완료 확인"), run on a built section
    // in Edit mode (obstacles in their rest poses) from ChessFight > Pawn Rush > Validate Sections:
    //   1. every obstacle's reach covers the whole way across where it stands (ObstacleSpan, measured by rays)
    //   2. both teams' routes the same length within 0.1 m, no gap over 1.5 m along either but where an
    //      IntendedGap says so (a jump pad's flight, a dive)
    //   3. floor under every checkpoint's respawn spots
    //   4. unclimbable: grey grid = NoClimbSurface, and the walls, statue, alcoves, gate walls, the dais, the beams,
    //      the vent posts and the glass squares are unclimbable
    //   5. a kill volume (the respawn floor), and floor at the entry and the exit
    public static class SectionValidator
    {
        static readonly string[] MustBeUnclimbable =
        {
            "Hall wall", "Foyer wall", "Porch wall", "Foyer pillar", "King statue", "Knight alcove", "Gate wall", "Balcony wall",
            "Royal dais", "Chandelier beam", "Wind vent post", "Glass square",
            // The royal water garden (1-1).
            "Garden wall", "Garden tower", "Garden terrace", "Garden island", "Plaza kerb", "Lock wall", "Lock gate post", "Lock gate beam",
            "Dyke wall", "Robot gantry", "Reservoir wall", "Boat tunnel", "Gate arch", "Exit arch",
        };

        public static List<string> Run(PawnRushSection section, List<string> notes)
        {
            var errors = new List<string>();
            Physics.SyncTransforms();
            var root = section.transform;
            notes.Add($"구간 {section.DisplayName} ({section.Id})");

            // 1. obstacle reach
            var groups = new Dictionary<string, List<ObstacleSpan>>();
            foreach (var span in root.GetComponentsInChildren<ObstacleSpan>(true))
            {
                if (!groups.TryGetValue(span.Group, out var list)) groups[span.Group] = list = new List<ObstacleSpan>();
                list.Add(span);
            }
            foreach (var kv in groups) Course01Validator.CheckSpan(kv.Value, errors, notes);
            notes.Add($"장애물 덮음 검사 {groups.Count}곳");

            // 2. routes
            var path = root.GetComponentInChildren<ProgressPath>(true);
            if (path == null) errors.Add("진행 경로(ProgressPath)가 없습니다");
            else
            {
                var west = path.World(Teams.White);
                var east = path.World(Teams.Black);
                float a = ProgressPath.Length(west), c = ProgressPath.Length(east);
                if (Mathf.Abs(a - c) > Course01Validator.PathTolerance) errors.Add($"서쪽·동쪽 길 길이 차 {Mathf.Abs(a - c):0.00} m > {Course01Validator.PathTolerance} m");
                var intended = root.GetComponentsInChildren<IntendedGap>(true);
                Gaps(west, "백팀 길", intended, errors, notes);
                if (!SameRoute(west, east)) Gaps(east, "흑팀 길", intended, errors, notes);
                if (intended.Length > 0) notes.Add($"뜻한 틈 {intended.Length}곳(점프대·다이빙 등, 1.5 m 검사에서 뺌)");
            }

            // 3. checkpoints
            int spots = 0;
            var checkpoints = root.GetComponentsInChildren<CourseCheckpoint>(true);
            foreach (var cp in checkpoints)
                for (int i = 0; i < cp.SpotCount; i++)
                {
                    spots++;
                    if (!Course01Validator.FloorAt(cp.SpotWorld(i), 1f, 2.5f))
                        errors.Add($"CP{cp.Number} 부활 자리 {i + 1} 아래에 바닥이 없습니다 {Course01Validator.Fmt(cp.SpotWorld(i))}");
                }
            notes.Add($"체크포인트 {checkpoints.Length}개, 부활 자리 {spots}곳");

            // 4. climbing
            Course01Validator.CheckClimbing(root, KitOf(section), errors, notes);
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c.isTrigger) continue;
                foreach (var prefix in MustBeUnclimbable)
                    if (c.name.StartsWith(prefix) && !Unclimbable(c.transform))
                        errors.Add($"{Course01Validator.Name(c.transform)}: 오를 수 없어야 합니다(NoClimbSurface 없음)");
            }

            // 5. kill volume, entry and exit
            if (root.GetComponentInChildren<KillVolume>(true) == null) errors.Add("리스폰 바닥(낙사 영역)이 없습니다");
            var entry = section.EntryWorld;
            var exit = section.ExitWorld;
            if (!Course01Validator.FloorAt(entry.position + entry.rotation * Vector3.forward * .5f, 1f, 2f))
                errors.Add("입구에 바닥이 없습니다 " + Course01Validator.Fmt(entry.position));
            if (!Course01Validator.FloorAt(exit.position - exit.rotation * Vector3.forward * .5f, 1f, 2f))
                errors.Add("출구에 바닥이 없습니다 " + Course01Validator.Fmt(exit.position));
            notes.Add($"입구 {Course01Validator.Fmt(entry.position)} → 출구 {Course01Validator.Fmt(exit.position)}");
            return errors;
        }

        // The kit a built section was made with (its materials tell the grey grid from the blue).
        static CourseKit KitOf(PawnRushSection section)
        {
            var hall = section.GetComponent<GrandStairHall>();
            if (hall != null) return hall.Kit;
            var garden = section.GetComponent<RoyalWaterGarden>();
            return garden != null ? garden.Kit : null;
        }

        // NoClimbSurface on it or above it, active or not (the portcullis and the east lift's wall wait switched off).
        static bool Unclimbable(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.GetComponent<NoClimbSurface>() != null) return true;
            return false;
        }

        // No gap over 1.5 m along a route (a "gap": nothing within 4 m below); a long one is measured between
        // its two banks. Straight climbs (the lift) and intended gaps (IntendedGap: a jump pad's flight, a dive) are
        // left out.
        static void Gaps(List<Vector3> path, string name, IntendedGap[] intended, List<string> errors, List<string> notes)
        {
            float run = 0f, worst = 0f;
            Vector3 worstAt = Vector3.zero;
            Collider near = null;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 a = path[i - 1], c = path[i];
                var flat = new Vector3(c.x - a.x, 0f, c.z - a.z);
                if (flat.magnitude < .01f) { run = 0f; continue; }
                float len = Vector3.Distance(a, c);
                for (float d = 0f; d <= len; d += .05f)
                {
                    var p = Vector3.Lerp(a, c, d / len);
                    var floor = Course01Validator.FloorUnder(p, 1f, 5f);
                    if (floor == null && Inside(intended, p)) { run = 0f; near = null; continue; }
                    if (floor == null) { run += .05f; continue; }
                    float gap = run > Course01Validator.MaxRequiredGap && near != null ? Course01Validator.Between(near, floor) : run;
                    if (gap > worst) { worst = gap; worstAt = p; }
                    run = 0f;
                    near = floor;
                }
            }
            if (worst > Course01Validator.MaxRequiredGap + .05f)
                errors.Add($"{name} 틈 {worst:0.00} m > {Course01Validator.MaxRequiredGap} m {Course01Validator.Fmt(worstAt)}");
            notes.Add($"{name} {ProgressPath.Length(path):0.0} m, 가장 넓은 틈 {worst:0.00} m");
        }

        static bool Inside(IntendedGap[] gaps, Vector3 point)
        {
            foreach (var gap in gaps) if (gap != null && gap.Contains(point)) return true;
            return false;
        }

        static bool SameRoute(List<Vector3> a, List<Vector3> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if ((a[i] - b[i]).sqrMagnitude > 1e-4f) return false;
            return true;
        }
    }
}
