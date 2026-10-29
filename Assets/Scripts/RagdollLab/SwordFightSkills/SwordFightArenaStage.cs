using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// 10-25 (승규 님: "JY-chessboxing_testgame 맵을 불러 와서 스킬 넷에 테스트용으로"): the Sword Fight skill test on the
    /// prototype match's board (<see cref="SwordFightArena"/>: the study, 64 maple and walnut squares, the rook pedestals,
    /// the rails) in place of the flat 14 m platform. As in the match (SwordFightMatch.SizeBoard), the platform is still
    /// what stands on and what a rook's skill breaks, sized to the board and hidden; the arena draws it, and its squares
    /// tremble, fall and rise with a collapse. No ghost pieces here: the squares only go with a skill.
    /// R191 (10-28, user: "내가 칸이 떨어지게 하는 시스템이 없어"): offline, K drops the match's next group of squares
    /// (a rook's row, a bishop's diagonal, a knight's L, the queen's edge: the same plan as the match, SwordGhosts.Plan),
    /// warned 1.5 s then gone as in the match, so their landing on the desk can be seen here; L puts the board back.
    /// </summary>
    public sealed class SwordFightArenaStage : MonoBehaviour
    {
        [Tooltip("한 칸 크기 (m). 경기의 기본값 2 m → 판 16 m × 16 m")]
        public float cellSize = 2f;

        [Tooltip("다음 칸 묶음 떨어뜨리기 (오프라인 시험)")]
        public KeyCode dropKey = KeyCode.K;
        [Tooltip("떨어진 칸 되돌리기")]
        public KeyCode resetKey = KeyCode.L;

        SwordFightArena arena;
        SwordFightEdgeFloor floor;
        List<SwordGhost> plan;
        int nextGroup;
        readonly List<(int cell, double at)> dropping = new List<(int, double)>();

        void Start()
        {
            floor = GetComponent<SwordFightEdgeFloor>();
            if (floor == null) floor = gameObject.AddComponent<SwordFightEdgeFloor>();
            var platform = floor.Platform;
            if (platform == null) { Debug.LogWarning("[SwordFightArenaStage] no platform found under the scene's centre"); return; }
            float size = SwordBoard.Size * cellSize;
            platform.localScale = new Vector3(size, platform.localScale.y, size);
            platform.position = new Vector3(0f, platform.position.y, 0f);
            floor.Hidden = true;
            floor.Snap = cellSize;
            var r = platform.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
            Physics.SyncTransforms();
            floor.Refind();
            arena = gameObject.AddComponent<SwordFightArena>();
            arena.Build(cellSize, floor);
        }

        void Update()
        {
            if (arena == null) return;
            double now = Time.timeSinceLevelLoad;
            // Offline only (the net link is always in the scene; an online match would have one PC's board fall alone).
            bool online = LabNetLink.Current != null && LabNetLink.Current.MatchActive;
            if (!online && !ChatBox.KeysHeld && !LabNetLink.KeysTaken)
            {
                if (Input.GetKeyDown(dropKey)) DropNext(now);
                if (Input.GetKeyDown(resetKey)) ResetBoard();
            }
            // A dropped square's floor goes when its picture does (whoever stands there falls with it).
            for (int i = dropping.Count - 1; i >= 0; i--)
                if (now >= dropping[i].at)
                {
                    floor.Erase(CellRect(dropping[i].cell), true);
                    dropping.RemoveAt(i);
                }
            // The squares over a rook's collapse or dropped by K, and the chess clock's hands going round.
            arena.Step(now);
        }

        void DropNext(double now)
        {
            if (plan == null) plan = SwordGhosts.Plan(Random.Range(0, 100000));
            if (nextGroup >= plan.Count)
            {
                Debug.Log("[SwordFightArenaStage] every group has dropped; L puts the board back");
                return;
            }
            var g = plan[nextGroup++];
            foreach (int c in g.Cells)
            {
                arena.Plan(c, now, now + SwordGhost.Warning);
                dropping.Add((c, now + SwordGhost.Warning));
            }
            Debug.Log($"[SwordFightArenaStage] dropping group {nextGroup}/{plan.Count}: {g.Cells.Length} squares");
        }

        void ResetBoard()
        {
            dropping.Clear();
            plan = null;
            nextGroup = 0;
            arena.ResetPlan();
            if (floor != null) floor.ClearAll();
        }

        Rect CellRect(int cell)
        {
            float x0 = (SwordBoard.X(cell) - SwordBoard.Size / 2) * cellSize, z0 = (SwordBoard.Y(cell) - SwordBoard.Size / 2) * cellSize;
            return new Rect(x0, z0, cellSize, cellSize);
        }
    }
}
