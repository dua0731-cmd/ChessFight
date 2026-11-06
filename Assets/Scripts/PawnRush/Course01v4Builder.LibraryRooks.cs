using UnityEngine;

namespace ChessFight.PawnRush
{
    public static partial class Course01v4Builder
    {
        // R448 (승규 11-05): the three marble rooks behind the war room's goal (ArtSource/PawnRush/Library/goal_rooks.py:
        // Goal_rook_<-1|0|1>, each a _pedestal, a _piece that rises off it and a _gap where the light gathers) shoot at the
        // way up to the goal - the side two fast and light, the middle one (1.2 times their size) slow, charged and
        // heavy - in LibraryRookTurret. The piece moves (it rises and turns), so its collider gets a kinematic body.
        static void LibraryRooks(GameObject hall)
        {
            Transform middle = null, goal = null;
            var roots = new System.Collections.Generic.List<Transform>();
            foreach (var t in hall.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "GOAL_capture_zone") goal = t;
                if (t.name == "Goal_rook_-1" || t.name == "Goal_rook_0" || t.name == "Goal_rook_1") roots.Add(t);
                if (t.name == "Goal_rook_0") middle = t;
            }
            if (middle == null || goal == null || roots.Count == 0)
            {
                Debug.LogError("[작전 회의실] 골 룩(Goal_rook_*)이나 골 구역(GOAL_capture_zone)이 모델에 없습니다 (goal_rooks.py를 돌렸는지 확인)");
                return;
            }
            int made = 0;
            foreach (var root in roots)
            {
                if (root.GetComponent<LibraryRookTurret>() != null) continue;
                string n = root.name;
                var piece = root.Find(n + "_piece");
                var gap = root.Find(n + "_gap");
                var pedestal = root.Find(n + "_pedestal");
                if (piece == null || gap == null)
                {
                    Debug.LogError("[작전 회의실] " + n + "에 _piece나 _gap이 없습니다");
                    continue;
                }
                if (piece.GetComponent<Rigidbody>() == null)
                {
                    var body = piece.gameObject.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                }
                bool heavy = n == "Goal_rook_0";
                root.gameObject.AddComponent<LibraryRookTurret>().Configure(heavy, heavy ? 1.2f : 1f, piece, gap, pedestal, middle, goal);
                made++;
            }
            Debug.Log("[작전 회의실] 골 룩 " + made + "개가 공격(양옆 1.6초마다 가볍게, 가운데 7초마다 3초 차징 후 크게)");
        }
    }
}
