using System.Text;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // Offline playtest tools for the Queen of the Hill graybox (DESIGN §3.8 step 2:
    // "does a section take 25-35 s uncontested"). Sits beside the PlaytestSpawner.
    //
    //   split times: every time the playtest character first gets up to a landing's
    //   height, the seconds since the previous one are recorded (S1 includes the
    //   hook start). Measured by height, not by the match's checkpoints, so a
    //   section can be practised again after PageDown.
    //   PageUp / PageDown: jump to the next / previous landing (0 = the bridge)
    //   F11: silence every bell (the round starts over; checkpoints forgotten)
    //   the opened paths and the seconds left of their team's head start
    public sealed class QueenHillPlaytestPanel : MonoBehaviour
    {
        [SerializeField] PlaytestSpawner spawner;
        [SerializeField] bool show = true;

        readonly float[] splits = new float[QueenHillCourse.Sections + 1];
        readonly bool[] jumped = new bool[QueenHillCourse.Sections + 1];
        int lastBest;
        float mark;
        GUIStyle style;

        void Start()
        {
            if (spawner == null) spawner = GetComponent<PlaytestSpawner>();
            if (spawner == null) spawner = FindFirstObjectByType<PlaytestSpawner>();
            mark = Time.time;
        }

        void Update()
        {
            var match = QueenHillMatch.Current;
            var driver = spawner != null ? spawner.Driver : null;
            if (match == null || driver == null) return;

            int best = Mathf.Max(lastBest, LevelAt(driver.FollowTarget != null ? driver.FollowTarget.position.y : 0f));
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
            else if (lastBest > 0 && match.PersonalBest(driver) == 0 && LevelAt(driver.FollowTarget.position.y) == 0)
            {
                // The round was started over (Backspace).
                Clear();
            }

            if (LegacyKeys.Down(KeyCode.PageUp)) Jump(Mathf.Min(QueenHillCourse.Sections, best + 1));
            else if (LegacyKeys.Down(KeyCode.PageDown)) Jump(Mathf.Max(0, LevelAt(driver.FollowTarget.position.y) - 1));
            else if (LegacyKeys.Down(KeyCode.F11))
            {
                match.ResetRound();
                Clear();
                // Splits start again from wherever the character stands.
                lastBest = LevelAt(driver.FollowTarget.position.y);
                for (int s = 1; s <= lastBest; s++) jumped[s] = true;
            }
        }

        // The highest landing at or below this height (of the hips): the level the character is up to.
        static int LevelAt(float y)
        {
            int level = 0;
            for (int l = 1; l <= QueenHillCourse.Sections; l++)
                if (y >= QueenHillCourse.LevelTop(l) + 0.2f) level = l;
            return level;
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

        // To a landing (0: this team's bridge). Its split will not count as a climb.
        void Jump(int level)
        {
            if (level <= 0)
            {
                spawner.MoveTo(spawner.StartPosition, spawner.StartRotation);
                QueenHillMatch.Current?.ResetRound();
                Clear();
                return;
            }
            foreach (var landing in SectionCheckpoint.All)
            {
                if (landing == null || landing.Section != level) continue;
                spawner.MoveTo(landing.FreeSpawnPosition(), landing.SpawnRotation);
                // Splits above here are measured again from this landing.
                for (int s = level; s < splits.Length; s++)
                {
                    splits[s] = 0f;
                    jumped[s] = s == level;
                }
                lastBest = level;
                mark = Time.time;
                return;
            }
        }

        void OnGUI()
        {
            var match = QueenHillMatch.Current;
            if (!show || match == null || spawner == null || spawner.Driver == null) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 14, richText = true };

            var text = new StringBuilder();
            text.Append("<b>퀸 오브 더 힐 그레이박스</b> · 목표: 구간당 25~35초, 전체 약 4분\n");
            float total = 0f;
            for (int s = 1; s <= QueenHillCourse.Sections; s++)
            {
                var section = QueenHillCourse.Section(s);
                string time;
                if (s <= lastBest)
                {
                    total += jumped[s] ? 0f : splits[s];
                    time = jumped[s] ? "이동" : splits[s] <= 0f ? "건너뜀" : $"<b>{splits[s]:0.0}초</b>";
                }
                else if (s == lastBest + 1) time = $"진행 중 {Time.time - mark:0.0}초";
                else time = "-";
                string open = match.IsOpen(s)
                    ? $" · 길 열림({Teams.Name(match.Pioneer(s))}" + (match.Exclusive(s) ? $" {match.ExclusiveLeft(s):0}초 독점)" : ")")
                    : "";
                text.Append($"S{s} {section.Name}: {time}{open}\n");
            }
            text.Append($"기록 합계 {total:0.0}초 (예상 {QueenHillCourse.EstimatedTotalSeconds():0}초)\n");
            text.Append("PageUp / PageDown 다음·이전 착지대 · F11 종 초기화");

            float width = 400f;
            float height = style.CalcHeight(new GUIContent(text.ToString()), width - 20f) + 12f;
            var box = new Rect(Screen.width - width - 12f, 12f, width, height);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 10f, box.y + 6f, width - 20f, height - 8f), text.ToString(), style);
        }
    }
}
