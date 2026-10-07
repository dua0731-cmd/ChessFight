using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The offline playtest's course-01 extras, next to the PlaytestSpawner:
    //   F5      switch the pawn to the other team (and start again from that team's start)
    //   CSV     every section (S+A, W1, B, W2, C, T) entered and left, with the time and the team,
    //           for comparing with the design doc's expected times (Logs/PawnRush in the project,
    //           or persistentDataPath)
    //   drop    gives the pawn the 8 m drop rule (FallDistanceRespawn)
    //   card    the slot's picture card for 3 s when the island is entered, then the button's
    //           progress while inside
    //   HUD     section, rank, metres along the progress path, the start countdown
    // Team size (2..6) decides which promotion zones show their pads.
    public sealed class Course01Playtest : MonoBehaviour
    {
        [SerializeField] PlaytestSpawner spawner;
        [SerializeField] PawnRushCourse course;
        [Tooltip("Players per team: which promotion zones are on (2: zone 2; 3: 1 and 2; 4~6: all three).")]
        [SerializeField, Range(2, 6)] int teamSize = 6;
        [SerializeField] bool writeCsv = true;

        string current;
        bool dropRule;
        float runStart;
        string csvPath;
        MiniGameSlot cardSlot;
        float cardUntil;
        GUIStyle style, big;

        void OnEnable() => MiniGameSlot.Entered += OnSlotEntered;
        void OnDisable() => MiniGameSlot.Entered -= OnSlotEntered;

        void Start()
        {
            if (course == null) course = PawnRushCourse.Current;
            foreach (var zone in FindObjectsByType<PromotionZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                zone.ApplyTeamSize(teamSize);
            // The pawn's grappling hook (a Queen of the Hill ability) would climb any wall. One hook
            // zone far below the course leaves nowhere it may be thrown from.
            var noHook = new GameObject("No hook here");
            noHook.transform.position = new Vector3(0f, -500f, 0f);
            noHook.AddComponent<BoxCollider>().isTrigger = true;
            noHook.AddComponent<HookStartZone>();
            runStart = Time.time;
            if (writeCsv) OpenCsv();
        }

        void Update()
        {
            if (spawner == null || spawner.Driver == null || course == null) return;
            if (LegacyKeys.Down(KeyCode.F5))
            {
                spawner.SetTeam(spawner.Team == Teams.Black ? Teams.White : Teams.Black);
                Restarted();
            }
            if (LegacyKeys.Down(KeyCode.Backspace)) Restarted();
            if (!dropRule && spawner.Driver is Component body)
            {
                var root = body.transform.root.gameObject;
                if (root.GetComponent<FallDistanceRespawn>() == null) root.AddComponent<FallDistanceRespawn>();
                dropRule = true;
            }
            var target = spawner.Driver.FollowTarget;
            if (target == null) return;
            course.Along(target.position, Mathf.Max(0, spawner.Team), out string section);
            if (section != current)
            {
                if (current != null) Log(current, "exit");
                current = section;
                Log(current, "enter");
            }
        }

        void Restarted()
        {
            runStart = Time.time;
            current = null;
            StartBar.Current?.Rearm();
            foreach (var slot in FindObjectsByType<MiniGameSlot>(FindObjectsSortMode.None)) slot.ResetRound();
            Log(null, "restart");
        }

        void OnSlotEntered(MiniGameSlot slot, ICharacterDriver who)
        {
            if (spawner == null || who != spawner.Driver || slot == cardSlot && Time.time < cardUntil + 10f) return;
            cardSlot = slot;
            cardUntil = Time.time + 3f;
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
                File.WriteAllText(csvPath, "module,event,seconds,team\n");
                Debug.Log("[PawnRush] 모듈 통과 기록(CSV): " + csvPath);
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
                    section ?? "-", what, Time.time - runStart, Teams.Name(spawner.Team)));
            }
            catch (Exception) { csvPath = null; }
        }

        // ------------------------------------------------------------------ HUD

        void OnGUI()
        {
            if (spawner == null || spawner.Driver == null || course == null) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 14, richText = true };
                big = new GUIStyle(style) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            }
            var target = spawner.Driver.FollowTarget;
            if (target == null) return;
            int team = Mathf.Max(0, spawner.Team);
            float along = course.Along(target.position, team, out string section), total = ProgressPath.Length(course.Path(team));
            int rank = PawnRushCourse.RankAt(target.position.y - course.transform.position.y);
            string text = $"<b>폰 러시 코스 01 「여덟 번째 랭크」</b> · {Teams.Name(spawner.Team)}팀 (F5 팀 바꾸기)\n" +
                          $"구간 {section} · 랭크 {rank} · 진행 {along:0} / {total:0} m";
            var box = new Rect(Screen.width - 452, 12, 440, 52);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 10, box.y + 6, box.width - 20, box.height - 8), text, style);

            var bar = StartBar.Current;
            if (bar != null && bar.Remaining > 0f)
                GUI.Label(new Rect(0, Screen.height * .3f, Screen.width, 60), Mathf.CeilToInt(bar.Remaining).ToString(), big);

            if (cardSlot != null && Time.time < cardUntil)
            {
                var card = new Rect((Screen.width - 520) * .5f, Screen.height * .18f, 520, 74);
                GUI.Box(card, GUIContent.none);
                GUI.Label(card, $"미니게임 슬롯 {cardSlot.Slot} (자리 표시)\n가운데 버튼 위에 서 있으면 진척이 오르고, 다 차면 위쪽 문이 열립니다", style);
            }
            var slot = SlotAround(target.position);
            if (slot != null && slot.Game != null)
            {
                var g = slot.Game;
                var back = new Rect((Screen.width - 300) * .5f, Screen.height - 80, 300, 14);
                GUI.Box(back, GUIContent.none);
                GUI.Box(new Rect(back.x, back.y, back.width * g.Progress01, back.height), GUIContent.none);
                GUI.Label(new Rect(back.x, back.y - 20, 300, 20), g.Completed ? "문이 열렸습니다" : $"미니게임 진척 {g.Progress01 * 100f:0}%", style);
            }
        }

        // The slot whose island the point is on (inside its 12 m width and its length).
        MiniGameSlot SlotAround(Vector3 point)
        {
            foreach (var slot in FindObjectsByType<MiniGameSlot>(FindObjectsSortMode.None))
            {
                var local = slot.transform.InverseTransformPoint(point);
                if (Mathf.Abs(local.x) <= 6.3f && local.z >= 0f && local.z <= 22f && local.y > -1f && local.y < 4f) return slot;
            }
            return null;
        }
    }
}
