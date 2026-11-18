using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R708 (승규 11-18 "미니게임 프리팹 같이 코스도 코스 1 코스 2 코스 3 이런식으로 구분 해서 넣어줘"): the obstacle courses'
    // own test scene, as the mini-game prefab test (MiniGamePrefabTest) is for the rooms - one section prefab at a time, no
    // round, no corridors. The courses come from the section set (Assets/Settings/PawnRushSections.asset) in the round's
    // order: course 1 = the first obstacle slot's prefabs (early), course 2 = the second's (mid), course 3 = the third's
    // (late), numbered on through the number keys, so a prefab dropped into a slot shows up here by itself.
    // The section is built as slot (its own), with a new seed each start; the pawn starts 4 m in from its entry.
    public sealed class CoursePrefabTest : MonoBehaviour
    {
        [SerializeField] PawnRushSectionSet set;
        [SerializeField] PlaytestSpawner spawner;
        [SerializeField] int selected;
        [SerializeField, Range(1, 6)] int teamSize = 6;

        struct Entry { public int course, slot; public GameObject prefab; }
        readonly List<Entry> entries = new List<Entry>();
        GameObject current;
        PawnRushSection section;
        int seed;
        GUIStyle style;

        public PawnRushSection Current => section;

        public void Configure(PawnRushSectionSet sections, PlaytestSpawner player)
        {
            set = sections;
            spawner = player;
        }

        void Awake()
        {
            MiniGameBase.Remote = false;
            // A course is a Pawn Rush course: the pieces' movement and skills, as in the round (PawnRushRound).
            CourseSkills.PawnRush = true;
            List();
        }

        void Start()
        {
            LocalPlayer();
            Select(selected);
        }

        void OnDestroy()
        {
            CourseSkills.PawnRush = false;
            if (current != null) Destroy(current);
            if (spawner != null && MiniGameBase.LocalDriver == spawner.Driver) MiniGameBase.LocalDriver = null;
        }

        void List()
        {
            entries.Clear();
            if (set == null) return;
            int course = 0;
            for (int s = 0; s < set.SlotCount; s++)
            {
                var slot = set[s];
                if (slot == null || slot.kind != PawnRushSection.SlotKind.Obstacle) continue;
                course++;
                foreach (var prefab in slot.prefabs)
                    if (prefab != null) entries.Add(new Entry { course = course, slot = s, prefab = prefab });
            }
        }

        public void Select(int index)
        {
            if (entries.Count == 0) return;
            index = Mathf.Clamp(index, 0, entries.Count - 1);
            selected = index;
            if (current != null)
            {
                current.SetActive(false);
                Destroy(current);
            }
            var e = entries[index];
            current = new GameObject("Course being tested");
            current.SetActive(false);
            var instance = Instantiate(e.prefab, current.transform);
            instance.name = e.prefab.name;
            section = instance.GetComponent<PawnRushSection>();
            seed = System.Environment.TickCount & 0xFFFFF;
            if (section != null)
            {
                // Its entry on the scene's origin, as the round puts the first section.
                section.PlaceEntryOn(new Pose(Vector3.zero, Quaternion.identity));
                section.Configure(e.slot, seed, teamSize, ObstacleClock.Now);
            }
            current.SetActive(true);
            Physics.SyncTransforms();
            ToEntry();
        }

        // The pawn 4 m in from the course's entry, on its floor, facing the way on (the section test's [ / ] jump);
        // R, Backspace and a fall bring it back here until it reaches a checkpoint.
        void ToEntry()
        {
            if (spawner == null || spawner.Driver == null || section == null) return;
            var entry = section.EntryWorld;
            var at = entry.position + entry.rotation * new Vector3(0f, 0f, 4f);
            if (Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
            if (spawner.Orbit != null && spawner.Orbit.FreeFly) spawner.Orbit.StopFly();
            spawner.SetStart(at, entry.rotation);
        }

        void LocalPlayer()
        {
            if (spawner == null) return;
            MissionStation.LocalTeam = spawner.Team;
            MiniGameBase.LocalDriver = spawner.Driver;
        }

        void Update()
        {
            if (spawner != null && MiniGameBase.LocalDriver != spawner.Driver)
            {
                LocalPlayer();
                ToEntry();   // the pawn came up after the course was built
            }
            for (int i = 0; i < 10 && i < entries.Count; i++)
                if (LegacyKeys.Down(i == 9 ? KeyCode.Alpha0 : (KeyCode)((int)KeyCode.Alpha1 + i))) Select(i);
            if (LegacyKeys.Down(KeyCode.F8)) Select(selected);
            if (spawner == null) return;
            if (LegacyKeys.Down(KeyCode.F5))
            {
                spawner.SetTeam(spawner.Team == Teams.White ? Teams.Black : Teams.White);
                LocalPlayer();
                ToEntry();
            }
        }

        static readonly string[] CourseNames = { "", "코스 1 (초반)", "코스 2 (중반)", "코스 3 (후반)" };

        void OnGUI()
        {
            if (style == null) style = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 14, richText = true, wordWrap = true };
            GUILayout.BeginArea(new Rect(12f, 12f, Mathf.Min(Screen.width - 24f, 560f), 190f), GUI.skin.box);
            string now = section != null ? section.DisplayName : "준비 중";
            int course = entries.Count > 0 ? entries[Mathf.Clamp(selected, 0, entries.Count - 1)].course : 0;
            GUILayout.Label($"<b>코스 프리팹 시험</b> · {(course < CourseNames.Length ? CourseNames[course] : "코스 " + course)} · {now} · 씨앗 {seed}", style);
            var rows = new System.Text.StringBuilder();
            int last = 0;
            for (int i = 0; i < entries.Count && i < 10; i++)
            {
                var e = entries[i];
                if (e.course != last)
                {
                    if (last != 0) rows.Append('\n');
                    rows.Append("<b>").Append(e.course < CourseNames.Length ? CourseNames[e.course] : "코스 " + e.course).Append("</b> ");
                    last = e.course;
                }
                var root = e.prefab.GetComponent<PawnRushSection>();
                rows.Append(' ').Append(i == 9 ? 0 : i + 1).Append(' ').Append(root != null ? root.DisplayName : e.prefab.name).Append(" ·");
            }
            GUILayout.Label(rows.ToString().TrimEnd('·', ' ').Replace(" ·\n", "\n"), style);
            GUILayout.Label("F8 다시 시작(새 씨앗) · F5 팀 전환\nR 체크포인트 · Backspace 처음 · F1 캐릭터 비행(개발) · F4 자유 카메라", style);
            GUILayout.EndArea();
        }
    }
}
