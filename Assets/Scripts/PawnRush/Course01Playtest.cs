using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The offline playtest's course-01 extras, next to the PlaytestSpawner:
    //   F5         switch the pawn to the other team (and start again from that team's start)
    //   F6         finish the mini-game of the pawn's team in the plaza it is in (or the next one)
    //   F7         the other team's reverse input in C and D on / off (design doc v0.4: a test switch)
    //   F8         a new round: two games drawn afresh, every gate shut
    //   F9 / F10   the next game in plaza 1 / plaza 2
    //   Backspace  start again: back to the start square, a new round
    //   CSV        every section (S+A, M1, B, M2, C, T) entered and left, with the time and the team,
    //              for comparing with the design doc's expected times (Logs/PawnRush in the project,
    //              or persistentDataPath)
    //   card       the plaza's game, its name and one line, for 3 s when the plaza is entered
    //   HUD        section, metres along the progress path, this round's games, the start countdown,
    //              and in a plaza both teams' progress there
    // Team size (2..6) decides which promotion zones show their pads.
    public sealed class Course01Playtest : MonoBehaviour
    {
        [SerializeField] PlaytestSpawner spawner;
        [SerializeField] PawnRushCourse course;
        [Tooltip("Players per team: which promotion zones are on (2: zone 2; 3: 1 and 2; 4~6: all three).")]
        [SerializeField, Range(2, 6)] int teamSize = 6;
        [SerializeField] bool writeCsv = true;

        // Online (R92) the network link places the pawns, not the spawner: it hands this the local one
        // so the HUD, the plaza cards and the CSV follow it. The development keys stay offline only.
        public static ICharacterDriver NetworkDriver;
        public static int NetworkTeam = Teams.White;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            NetworkDriver = null;
            NetworkTeam = Teams.White;
        }

        bool Online => NetworkDriver != null;
        ICharacterDriver Driver => Online ? NetworkDriver : spawner != null ? spawner.Driver : null;
        int Team => Online ? NetworkTeam : spawner != null ? spawner.Team : Teams.White;

        string current;
        float runStart;
        string csvPath;
        int cardPlaza;
        float cardUntil;
        GUIStyle style, big;

        void OnEnable() => PlazaEntry.Entered += OnPlazaEntered;
        void OnDisable() => PlazaEntry.Entered -= OnPlazaEntered;

        void Start()
        {
            if (course == null) course = PawnRushCourse.Current;
            ApplyTeamSize();
            // The pawn's grappling hook (a Queen of the Hill ability) would climb any wall. One hook
            // zone far below the course leaves nowhere it may be thrown from.
            var noHook = new GameObject("No hook here");
            noHook.transform.position = new Vector3(0f, -500f, 0f);
            noHook.AddComponent<BoxCollider>().isTrigger = true;
            noHook.AddComponent<HookStartZone>();
            runStart = Time.time;
            if (writeCsv) OpenCsv();
        }

        void ApplyTeamSize()
        {
            foreach (var zone in FindObjectsByType<PromotionZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                zone.ApplyTeamSize(teamSize);
        }

        void Update()
        {
            if (Driver == null || course == null) return;
            var picker = Online ? null : course.Missions;   // no development keys in a match
            if (!Online && LegacyKeys.Down(KeyCode.F5))
            {
                spawner.SetTeam(spawner.Team == Teams.Black ? Teams.White : Teams.Black);
                Restarted(false);
            }
            if (!Online && LegacyKeys.Down(KeyCode.Backspace)) Restarted(true);
            if (!Online && LegacyKeys.Down(KeyCode.F7))
            {
                MissionStation.ReverseSabotage = !MissionStation.ReverseSabotage;
                Debug.Log("[PawnRush] C·D 상대 반대 입력: " + (MissionStation.ReverseSabotage ? "켜짐" : "꺼짐"));
            }
            if (picker != null)
            {
                if (LegacyKeys.Down(KeyCode.F8)) NewRound(picker);
                if (LegacyKeys.Down(KeyCode.F9)) { picker.Cycle(1); ApplyTeamSize(); }
                if (LegacyKeys.Down(KeyCode.F10)) { picker.Cycle(2); ApplyTeamSize(); }
            }
            var target = Driver.FollowTarget;
            if (target == null) return;
            if (LegacyKeys.Down(KeyCode.F6) && picker != null)
            {
                int plaza = PlazaAround(target.position, picker);
                if (plaza == 0) plaza = course.Along(target.position, Mathf.Max(0, Team), out _) <
                                        course.Progress.SectionStart("B", Mathf.Max(0, Team)) ? 1 : 2;
                var game = picker.Station(plaza, Mathf.Max(0, Team))?.Game;
                if (game != null) game.ForceComplete();
            }
            course.Along(target.position, Mathf.Max(0, Team), out string section);
            if (section != current)
            {
                if (current != null) Log(current, "exit");
                current = section;
                Log(current, "enter");
            }
        }

        void NewRound(MissionPicker picker)
        {
            picker.NewRound(Environment.TickCount);
            ApplyTeamSize();
        }

        void Restarted(bool redraw)
        {
            runStart = Time.time;
            current = null;
            StartBar.Current?.Rearm();
            var picker = course.Missions;
            if (picker != null)
            {
                if (redraw) NewRound(picker);
                else picker.ResetRound();
            }
            Log(null, "restart");
        }

        void OnPlazaEntered(PlazaEntry entry, ICharacterDriver who)
        {
            if (Driver == null || who != Driver || entry.Plaza == cardPlaza && Time.time < cardUntil + 10f) return;
            cardPlaza = entry.Plaza;
            cardUntil = Time.time + 3f;
        }

        // 1 or 2 when the point is in a mission plaza (entry band to the gate passages), else 0.
        static int PlazaAround(Vector3 point, MissionPicker picker)
        {
            for (int plaza = 1; plaza <= 2; plaza++)
                for (int team = 0; team < 2; team++)
                {
                    var station = picker.Station(plaza, team);
                    if (station == null) continue;
                    var local = station.transform.InverseTransformPoint(point);
                    if (Mathf.Abs(local.x) <= 12.5f && local.z >= -4f && local.z <= 27f && local.y > -1.5f && local.y < 13f) return plaza;
                }
            return 0;
        }

        // ------------------------------------------------------------------ CSV

        void OpenCsv()
        {
            try
            {
                string folder = Application.isEditor
                    ? Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", "PawnRush")
                    : Path.Combine(Application.persistentDataPath, "PawnRush");
                Directory.CreateDirectory(folder);
                csvPath = Path.Combine(folder, "course01_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
                File.WriteAllText(csvPath, "section,event,seconds,team\n");
                Debug.Log("[PawnRush] 구간 통과 기록(CSV): " + csvPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PawnRush] CSV를 열지 못했습니다: " + e.Message);
                csvPath = null;
            }
        }

        void Log(string section, string what)
        {
            if (csvPath == null) return;
            try
            {
                File.AppendAllText(csvPath, string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:0.00},{3}\n",
                    section ?? "-", what, Time.time - runStart, Teams.Name(Team)));
            }
            catch (Exception) { csvPath = null; }
        }

        // ------------------------------------------------------------------ HUD

        void OnGUI()
        {
            if (Driver == null || course == null) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 14, richText = true };
                big = new GUIStyle(style) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            }
            var target = Driver.FollowTarget;
            if (target == null) return;
            int team = Mathf.Max(0, Team);
            float along = course.Along(target.position, team, out string section), total = ProgressPath.Length(course.Path(team));
            var picker = course.Missions;
            string games = picker != null ? $"광장 ① {MissionPicker.Name(picker.First)} · 광장 ② {MissionPicker.Name(picker.Second)}" : "미니게임 없음";
            string text = $"<b>폰 러시 코스 01 v0.4</b> · {Teams.Name(Team)}팀{(Online ? " · 온라인 경기 (Esc 나가기)" : " (F5 팀 바꾸기)")}\n" +
                          $"구간 {section} · 진행 {along:0} / {total:0} m\n" +
                          $"{games}\n" +
                          (Online ? "방장 PC가 모든 폰과 미니게임을 계산합니다"
                                  : $"F6 우리 미니게임 완료 · F7 반대 입력 {(MissionStation.ReverseSabotage ? "켜짐" : "꺼짐")} · F8 새 판 · F9/F10 게임 바꾸기");
            var box = new Rect(Screen.width - 512, 12, 500, 88);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 10, box.y + 6, box.width - 20, box.height - 8), text, style);

            var bar = StartBar.Current;
            if (bar != null && bar.Remaining > 0f)
                GUI.Label(new Rect(0, Screen.height * .3f, Screen.width, 60), Mathf.CeilToInt(bar.Remaining).ToString(), big);
            if (picker == null) return;

            if (cardPlaza > 0 && Time.time < cardUntil)
            {
                int game = cardPlaza == 1 ? picker.First : picker.Second;
                var card = new Rect((Screen.width - 560) * .5f, Screen.height * .18f, 560, 74);
                GUI.Box(card, GUIContent.none);
                string hint = game >= 0 && game < PawnRushMissions.Count ? PawnRushMissions.Hints[game] : "";
                GUI.Label(new Rect(card.x + 12, card.y + 8, card.width - 24, card.height - 12),
                          $"<b>미션 광장 {cardPlaza} · {MissionPicker.Name(game)}</b>\n{hint}", style);
            }
            int plaza = PlazaAround(target.position, picker);
            if (plaza == 0) return;
            for (int t = 0; t < 2; t++)
            {
                var g = picker.Station(plaza, t)?.Game;
                if (g == null) continue;
                var back = new Rect((Screen.width - 300) * .5f, Screen.height - 96 + t * 40, 300, 14);
                GUI.Box(back, GUIContent.none);
                GUI.Box(new Rect(back.x, back.y, back.width * g.Progress01, back.height), GUIContent.none);
                string who = t == team ? "우리" : "상대";
                GUI.Label(new Rect(back.x, back.y - 20, 300, 20),
                          g.Completed ? $"{who}({Teams.Name(t)}) 팀 문이 열렸습니다" : $"{who}({Teams.Name(t)}) 진척 {g.Progress01 * 100f:0}%", style);
            }
        }
    }
}
