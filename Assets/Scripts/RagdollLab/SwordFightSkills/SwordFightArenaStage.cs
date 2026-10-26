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
    /// </summary>
    public sealed class SwordFightArenaStage : MonoBehaviour
    {
        [Tooltip("한 칸 크기 (m). 경기의 기본값 2 m → 판 16 m × 16 m")]
        public float cellSize = 2f;

        SwordFightArena arena;
        SwordFightEdgeFloor floor;

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
            // The squares over a rook's collapse, and the chess clock's hands going round.
            if (arena != null) arena.Step(Time.timeSinceLevelLoad);
        }
    }
}
