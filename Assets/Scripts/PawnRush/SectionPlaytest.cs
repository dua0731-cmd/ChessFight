using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The section test scene's extras (Assets/Scenes/PawnRush_Sections.unity), next to the PlaytestSpawner:
    //   F5         switch the pawn to the other team (and start again)
    //   F6         finish our team's mini-game in the plaza the pawn is in (or the next plaza on): its gate opens (R153)
    //   F8         a new round: a new seed, the sections drawn again, every random obstacle on a new order
    //   F9         team size 3 -> 4 -> 5 -> 6 a side (the grand stair hall opens one wing for 3 or 4)
    //   R / Backspace   the last checkpoint / the start (the spawner's own keys)
    //   HUD        the round's seed and slots, the section and the beat the pawn is in, the team size, falls and hits
    //   CSV        every beat entered and left, every fall onto the respawn floor and every hit by a rolling
    //              statue, a carpet or a chandelier, with the time and the team (the level design's §8 "비트별
    //              입장·퇴장 시각과 실패 횟수"; Logs/PawnRush in the project, or persistentDataPath)
    public sealed class SectionPlaytest : MonoBehaviour
    {
        [SerializeField] PlaytestSpawner spawner;
        [SerializeField] PawnRushRound round;
        [SerializeField] bool writeCsv = true;

        string currentBeat;
        PawnRushSection currentSection;
        float runStart;
        string csvPath;
        int falls, hits;
        string lastHit = "";
        float lastHitAt = -10f;
        GUIStyle style;

        void OnEnable()
        {
            KillVolume.Entered += OnFall;
            RollingSpawner.Hit += OnHit;
            ChandelierRain.Hit += OnHit;
        }

        void OnDisable()
        {
            KillVolume.Entered -= OnFall;
            RollingSpawner.Hit -= OnHit;
            ChandelierRain.Hit -= OnHit;
        }

        void Start()
        {
            if (round == null) round = PawnRushRound.Current;
            // The grappling hook (a Queen of the Hill ability) would climb any wall: one hook zone far below.
            var noHook = new GameObject("No hook here");
            noHook.transform.position = new Vector3(0f, -500f, 0f);
            noHook.AddComponent<BoxCollider>().isTrigger = true;
            noHook.AddComponent<HookStartZone>();
            runStart = Time.time;
            if (writeCsv) OpenCsv();
            GameMenu.UseIfNone(gameObject, () => UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Lobby), false);
        }

        void Update()
        {
            if (spawner == null || spawner.Driver == null || round == null) return;
            // The plazas' games: whose fanfare is loud, whose aiming arc shows (Course01Playtest does the same).
            MissionStation.LocalTeam = Mathf.Max(0, spawner.Team);
            MiniGameBase.LocalDriver = spawner.Driver;
            if (LegacyKeys.Down(KeyCode.F6)) FinishOurGame();
            if (LegacyKeys.Down(KeyCode.F5))
            {
                spawner.SetTeam(spawner.Team == Teams.Black ? Teams.White : Teams.Black);
                Restarted();
            }
            if (LegacyKeys.Down(KeyCode.F8))
            {
                round.Build(Environment.TickCount);
                // The start spots are the new round's: start again from them.
                spawner.SetTeam(Mathf.Max(0, spawner.Team));
                Restarted();
            }
            if (LegacyKeys.Down(KeyCode.F9))
            {
                round.SetTeamSize(round.TeamSize >= 6 ? 3 : round.TeamSize + 1);
                Log("-", "team size " + round.TeamSize);
            }
            if (LegacyKeys.Down(KeyCode.Backspace)) Restarted();
            var target = spawner.Driver.FollowTarget;
            if (target == null) return;
            var section = round.SectionAt(target.position, out string beat);
            string key = section != null ? section.DisplayName + " / " + beat : null;
            if (key != currentBeat)
            {
                if (currentBeat != null) Log(currentBeat, "exit");
                currentBeat = key;
                currentSection = section;
                if (currentBeat != null) Log(currentBeat, "enter");
            }
        }

        void Restarted()
        {
            runStart = Time.time;
            currentBeat = null;
            falls = hits = 0;
            // From the start again: the plazas' games back to their start, the gates shut, the pads ready.
            foreach (var s in round.Sections)
            {
                var plaza = s != null ? s.GetComponent<EarlyPlaza>() : null;
                if (plaza != null) plaza.ResetRound();
            }
            Log("-", "restart");
        }

        // F6: our team's game in the plaza the pawn is in, or else the first plaza after it.
        void FinishOurGame()
        {
            var target = spawner.Driver.FollowTarget;
            var here = target != null ? round.SectionAt(target.position, out _) : null;
            bool reached = here == null;
            foreach (var s in round.Sections)
            {
                if (s == here) reached = true;
                if (!reached || s == null) continue;
                var plaza = s.GetComponent<EarlyPlaza>();
                var game = plaza != null ? plaza.GameOf(spawner.Team) : null;
                if (game == null) continue;
                game.ForceComplete();
                Log(currentBeat ?? "-", "F6 finish " + s.DisplayName);
                return;
            }
        }

        bool Mine(GameObject who) => spawner != null && spawner.Driver is Component c && who != null && (who == c.gameObject || c.transform.IsChildOf(who.transform) || who.transform.IsChildOf(c.transform));

        void OnFall(ICharacterDriver who, KillVolume volume)
        {
            if (spawner == null || who != spawner.Driver) return;
            falls++;
            Log(currentBeat ?? "-", "fall");
        }

        void OnHit(GameObject who, string what)
        {
            if (!Mine(who)) return;
            hits++;
            lastHit = what;
            lastHitAt = Time.time;
            Log(currentBeat ?? "-", "hit " + what);
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
                csvPath = Path.Combine(folder, "sections_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
                File.WriteAllText(csvPath, "beat,event,seconds,team,seed,team_size\n");
                Debug.Log("[PawnRush] 비트 통과 기록(CSV): " + csvPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PawnRush] CSV를 열지 못했습니다: " + e.Message);
                csvPath = null;
            }
        }

        void Log(string beat, string what)
        {
            if (csvPath == null || spawner == null) return;
            try
            {
                File.AppendAllText(csvPath, string.Format(CultureInfo.InvariantCulture, "\"{0}\",{1},{2:0.00},{3},{4},{5}\n",
                    beat, what, Time.time - runStart, Teams.Name(spawner.Team), round != null ? round.Seed : 0, round != null ? round.TeamSize : 0));
            }
            catch (Exception) { csvPath = null; }
        }

        // ------------------------------------------------------------------ HUD

        void OnGUI()
        {
            if (spawner == null || spawner.Driver == null || round == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 14, richText = true };
            var slots = new List<string>();
            var set = round.Set;
            for (int i = 0; set != null && i < set.SlotCount; i++)
            {
                string picked = "-";
                foreach (var s in round.Sections)
                    if (s != null && s.Slot == i) picked = s.DisplayName;
                slots.Add((i + 1) + " " + picked);
            }
            string here = currentSection != null ? currentSection.DisplayName + " · 비트 " + currentBeat.Substring(currentBeat.IndexOf('/') + 2) : "구간 밖";
            string text = $"<b>폰 러시 구간 시험</b> · 판 씨앗 {round.Seed} · {round.TeamSize}대{round.TeamSize} · {Teams.Name(spawner.Team)}팀\n" +
                          $"{here}\n" +
                          $"낙하 {falls} · 맞음 {hits}" + (Time.time - lastHitAt < 2f ? $" (<b>{lastHit}</b>)" : "") + "\n" +
                          $"판 구성: {string.Join(" | ", slots)}\n" +
                          "F5 팀 바꾸기 · F6 우리 미니게임 완료 · F8 새 판(새 씨앗) · F9 팀 인원 3~6 · R 체크포인트 · Backspace 처음";
            var box = new Rect(Screen.width - 572, 12, 560, 104);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 10, box.y + 6, box.width - 20, box.height - 8), text, style);
        }
    }
}
