using System.Text;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // Offline playtest tools for the Queen of the Hill graybox. Sits beside the
    // PlaytestSpawner.
    //
    //   split times: every time the playtest character first gets up to a rank's
    //   height, the seconds since the previous one are recorded (floor 1 includes
    //   the hook start). Measured by height, so a floor can be practised again.
    //   the character's own highest rank (where its vacuum tube goes) and which of
    //   its team's shortcuts are open; the other team's too
    //   PageUp / PageDown: jump to the next / previous rank's plaza on its team's side
    //   F11: close every shortcut and forget every rank (the round starts over)
    public sealed class QueenHillPlaytestPanel : MonoBehaviour
    {
        [SerializeField] PlaytestSpawner spawner;
        [SerializeField] bool show = true;

        readonly float[] splits = new float[QueenHillCourse.Floors + 1];
        readonly bool[] jumped = new bool[QueenHillCourse.Floors + 1];
        int lastBest;
        float mark;
        GUIStyle style;

        void Start()
        {
            if (spawner == null) spawner = GetComponent<PlaytestSpawner>();
            if (spawner == null) spawner = FindFirstObjectByType<PlaytestSpawner>();
            mark = Time.time;
        }

        // Floors the character has climbed, from the height of its hips: rank - 1.
        static int LevelAt(float y) => QueenHillCourse.RankAt(y) - 1;

        void Update()
        {
            var driver = spawner != null ? spawner.Driver : null;
            if (driver == null || driver.FollowTarget == null) return;
            float y = driver.FollowTarget.position.y;

            int best = Mathf.Max(lastBest, LevelAt(y));
            if (best > lastBest)
            {
                for (int s = lastBest + 1; s <= best && s < splits.Length; s++)
                {
                    splits[s] = s == best ? Time.time - mark : 0f;
                    jumped[s] = false;
                }
                lastBest = best;
                mark = Time.time;
            }

            if (LegacyKeys.Down(KeyCode.PageUp)) Jump(Mathf.Min(QueenHillCourse.Ranks, QueenHillCourse.RankAt(y) + 1));
            else if (LegacyKeys.Down(KeyCode.PageDown)) Jump(Mathf.Max(1, QueenHillCourse.RankAt(y) - 1));
            else if (LegacyKeys.Down(KeyCode.F11))
            {
                QueenHillRaceMatch.Current?.ResetRound();
                QueenHillMatch.Current?.ResetRound();
                Clear();
                lastBest = LevelAt(y);
                for (int s = 1; s <= lastBest; s++) jumped[s] = true;
            }
        }

        void Clear()
        {
            for (int s = 0; s < splits.Length; s++)
            {
                splits[s] = 0f;
                jumped[s] = false;
            }
            lastBest = 0;
            mark = Time.time;
        }

        // To a rank's plaza on the team's side (its rank pad then counts it, as if climbed).
        void Jump(int rank)
        {
            Vector3 at = QueenHillLevel.Plaza(rank, spawner.Team);
            spawner.MoveTo(at, QueenHillLevel.PlazaFacing(spawner.Team));
            int level = rank - 1;
            for (int s = Mathf.Max(1, level); s < splits.Length; s++)
            {
                splits[s] = 0f;
                jumped[s] = s == level;
            }
            lastBest = level;
            mark = Time.time;
        }

        void OnGUI()
        {
            var driver = spawner != null ? spawner.Driver : null;
            if (!show || driver == null || driver.FollowTarget == null) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 14, richText = true };
            var race = QueenHillRaceMatch.Current;
            int team = spawner.Team >= 0 ? spawner.Team : Teams.White;

            var text = new StringBuilder();
            text.Append("<b>퀸 오브 더 힐 그레이박스</b> · 층마다 기록 (지름길은 팀별)\n");
            float total = 0f;
            for (int s = 1; s <= QueenHillCourse.Floors; s++)
            {
                var floor = QueenHillCourse.Floor(s);
                string time;
                if (s <= lastBest)
                {
                    total += jumped[s] ? 0f : splits[s];
                    time = jumped[s] ? "이동" : splits[s] <= 0f ? "건너뜀" : $"<b>{splits[s]:0.0}초</b>";
                }
                else if (s == lastBest + 1) time = $"진행 중 {Time.time - mark:0.0}초";
                else time = "-";
                string shortcut = race == null ? "" :
                    " · 지름길 " + (race.IsOpen(s, team) ? "<b>열림</b>" : "닫힘") + (race.IsOpen(s, 1 - team) ? " (상대 열림)" : "");
                text.Append($"{s}층 {floor.Name}: {time}{shortcut}\n");
            }
            int rank = QueenHillCourse.RankAt(driver.FollowTarget.position.y);
            text.Append($"기록 합계 {total:0.0}초 · 지금 {rank}랭크");
            if (race != null) text.Append($" · 내 최고 {race.ReachedRank(driver)}랭크 (진공관 → {race.TubeTarget(driver)}랭크)");
            text.Append("\nPageUp / PageDown 다음·이전 랭크 광장 · F11 지름길·랭크 초기화");

            float width = 470f;
            float height = style.CalcHeight(new GUIContent(text.ToString()), width - 20f) + 12f;
            var box = new Rect(Screen.width - width - 12f, 12f, width, height);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 10f, box.y + 6f, width - 20f, height - 8f), text.ToString(), style);
        }
    }
}
