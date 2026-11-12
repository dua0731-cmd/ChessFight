using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // R548: the Pawn Rush course table (PawnRushCourseTable) the entrance screen reveals the round from, read once from
    // Resources/Loading/PawnRushCourse.txt (written by the editor from the section set, PawnRushCourseTableWriter).
    public static class PawnRushCourseNames
    {
        const string TablePath = "Loading/PawnRushCourse";
        static PawnRushCourseTable table;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTable() => table = null;

        // Never null: an empty table (every slot "?") when the file is missing.
        public static PawnRushCourseTable Table
        {
            get
            {
                if (table != null) return table;
                var text = Resources.Load<TextAsset>(TablePath);
                table = PawnRushCourseTable.Parse(text != null ? text.text : "");
                if (table.Count == 0)
                    Debug.LogWarning("[Loading] Resources/" + TablePath + ".txt가 없거나 비어 있어 코스 공개 화면에 구간 이름이 나오지 않습니다 " +
                                     "(ChessFight/Pawn Rush/Build/Course Name Table).");
                if (text != null) Resources.UnloadAsset(text);
                return table;
            }
        }
    }
}
