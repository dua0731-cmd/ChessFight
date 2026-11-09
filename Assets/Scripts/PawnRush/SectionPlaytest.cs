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
    //   F          the piece's skill (R494: the round is a Pawn Rush course - the seoungtest pawn movement and the
    //              piece skills, as in 승규 님's test scene; PawnRushRound raises CourseSkills.PawnRush)
    //   F5         switch the pawn to the other team (and start again)
    //   F6         (development builds, R525) the team's next mini-game done now; in a match through its host (SteamPawnRushLink)
    //   F8         a new round: a new seed, the sections drawn again, every random obstacle on a new order
    //   F9         team size 3 -> 4 -> 5 -> 6 a side (the grand stair hall opens one wing for 3 or 4)
    //   R / Backspace   the last checkpoint / the start (the spawner's own keys)
    //   [ / ]      the previous / next section of the round, round the ends (R493: seven sections are long, and the
    //              war room is the seventh): the pawn starts again 4 m in from that section's entry
    //   HUD        the round's seed and slots, the section and the beat the pawn is in, the team size, falls and hits
    //   CSV        every beat entered and left, every fall onto the respawn floor and every hit by a rolling
    //              statue, a carpet or a chandelier, with the time and the team (the level design's §8 "비트별
    //              입장·퇴장 시각과 실패 횟수"; Logs/PawnRush in the project, or persistentDataPath)
    //
    // The match scene (R508, PawnRush_Round) has it too. In a match the link sets NetworkDriver to this PC's pawn (the
    // spawner stays off): no test keys, the HUD and the CSV follow that pawn. Played offline, a round with a crown finish
    // counts the spawner's pawn on it (one of one is a majority: the result scenes follow).
    public sealed class SectionPlaytest : MonoBehaviour
    {
        [SerializeField] PlaytestSpawner spawner;
        [SerializeField] PawnRushRound round;
        [SerializeField] bool writeCsv = true;

        // A match (R508): this PC's pawn and team, set by the match link.
        public static ICharacterDriver NetworkDriver;
        public static int NetworkTeam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            NetworkDriver = null;
            NetworkTeam = 0;
        }

        bool Online => NetworkDriver != null;
        ICharacterDriver Driver => Online ? NetworkDriver : spawner != null ? spawner.Driver : null;
        int Team => Online ? NetworkTeam : spawner != null ? spawner.Team : Teams.White;
        ICharacterDriver registered;

        string currentBeat;
        PawnRushSection currentSection;
        // Between two sections (R517): the HUD and the CSV name the corridor.
        const string CorridorBeat = "구간 사이 복도";
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
            GardenHits.Hit += OnHit;
        }

        void OnDisable()
        {
            KillVolume.Entered -= OnFall;
            RollingSpawner.Hit -= OnHit;
            ChandelierRain.Hit -= OnHit;
            GardenHits.Hit -= OnHit;
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
            if (Driver == null || round == null) return;
            if (Online)
            {
                Track(NetworkDriver);
                return;
            }
            if (spawner == null) return;
            // Whose fanfare is loud, whose aiming arc shows: the spawner's pawn (the match link sets the match's own).
            MissionStation.LocalTeam = spawner.Team;
            MiniGameBase.LocalDriver = spawner.Driver;
            CountOnFinish();
            if (LegacyKeys.Down(KeyCode.F5))
            {
                spawner.SetTeam(spawner.Team == Teams.Black ? Teams.White : Teams.Black);
                Restarted();
            }
            if (LegacyKeys.Down(KeyCode.F8))
            {
                round.Build(Environment.TickCount);
                if (round.Finish != null) round.Finish.Restart();
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
            if (LegacyKeys.Down(KeyCode.LeftBracket)) Jump(-1);
            if (LegacyKeys.Down(KeyCode.RightBracket)) Jump(1);
            // R525: the team's next mini-game done now (a match does it through its host: SteamPawnRushLink).
            if (Debug.isDebugBuild && LegacyKeys.Down(KeyCode.F6) && !ChatBox.KeysHeld) Log("-", round.CompleteNextMiniGame(spawner.Team, "나"));
            Track(spawner.Driver);
        }

        // Offline with a crown finish (the match scene played on its own): the spawner's pawn is the one runner.
        void CountOnFinish()
        {
            var finish = round.Finish;
            if (finish == null || spawner.Driver == null || registered == spawner.Driver) return;
            if (registered != null) finish.Unregister(registered);
            registered = spawner.Driver;
            finish.Register(registered, spawner.Team, "나", counts: true, self: true);
        }

        // The section and the beat the pawn is in, for the HUD and the CSV.
        void Track(ICharacterDriver driver)
        {
            var target = driver != null ? driver.FollowTarget : null;
            if (target == null) return;
            var section = round.SectionAt(target.position, out string beat);
            string key = section != null ? section.DisplayName + " / " + beat : round.InCorridor(target.position) ? CorridorBeat : null;
            if (key != currentBeat)
            {
                if (currentBeat != null) Log(currentBeat, "exit");
                currentBeat = key;
                currentSection = section;
                if (currentBeat != null) Log(currentBeat, "enter");
            }
        }

        // The pawn to the section `step` away from the one it is in (outside them all: the first or the last), on its
        // floor 4 m in from its entry, facing the way on; R, Backspace and a fall bring it back there until it reaches
        // a checkpoint (PlaytestSpawner.SetStart, as 승규 님's test scene enters a zone).
        void Jump(int step)
        {
            var list = round.Sections;
            if (list.Count == 0) return;
            int here = -1;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i] == currentSection) here = i;
            // Where no beat holds it: the last section whose entry it has passed (each in its own frame).
            var target = spawner.Driver.FollowTarget;
            if (here < 0 && target != null)
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null && list[i].transform.InverseTransformPoint(target.position).z >= list[i].EntryLocal.position.z - 1f)
                        here = i;
            int to = here < 0 ? (step > 0 ? 0 : list.Count - 1) : (here + step + list.Count) % list.Count;
            var section = list[to];
            if (section == null) return;
            var entry = section.EntryWorld;
            var at = entry.position + entry.rotation * new Vector3(0f, 0f, 4f);
            if (Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
            if (spawner.Orbit != null && spawner.Orbit.FreeFly) spawner.Orbit.StopFly();
            spawner.SetStart(at, entry.rotation);
            Restarted();
            Log("-", "jump " + (section.Slot + 1) + " " + section.DisplayName);
        }

        void Restarted()
        {
            runStart = Time.time;
            currentBeat = null;
            falls = hits = 0;
            Log("-", "restart");
        }

        bool Mine(GameObject who) => Driver is Component c && who != null && (who == c.gameObject || c.transform.IsChildOf(who.transform) || who.transform.IsChildOf(c.transform));

        void OnFall(ICharacterDriver who, KillVolume volume)
        {
            if (who == null || who != Driver) return;
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
            if (csvPath == null || Driver == null) return;
            try
            {
                File.AppendAllText(csvPath, string.Format(CultureInfo.InvariantCulture, "\"{0}\",{1},{2:0.00},{3},{4},{5}\n",
                    beat, what, Time.time - runStart, Teams.Name(Team), round != null ? round.Seed : 0, round != null ? round.TeamSize : 0));
            }
            catch (Exception) { csvPath = null; }
        }

        // ------------------------------------------------------------------ HUD

        void OnGUI()
        {
            if (Driver == null || round == null) return;
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
            string here = currentSection != null ? currentSection.DisplayName + " · 비트 " + currentBeat.Substring(currentBeat.IndexOf('/') + 2)
                        : currentBeat == CorridorBeat ? CorridorBeat : "구간 밖";
            // R525: a development build shows its mini-game cheat (F6) in a match too.
            string cheat = Debug.isDebugBuild ? " · F6 미니게임 완료 · F1 캐릭터 비행(개발)" : "";
            string text = Online
                ? $"<b>폰 러시</b> · {Teams.Name(Team)}팀\n{here}\n판 구성: {string.Join(" | ", slots)}" + (cheat != "" ? "\n개발 빌드" + cheat : "")
                : $"<b>폰 러시 구간 시험</b> · 판 씨앗 {round.Seed} · {round.TeamSize}대{round.TeamSize} · {Teams.Name(Team)}팀\n" +
                  $"{here}\n" +
                  $"낙하 {falls} · 맞음 {hits}" + (Time.time - lastHitAt < 2f ? $" (<b>{lastHit}</b>)" : "") + "\n" +
                  $"판 구성: {string.Join(" | ", slots)}\n" +
                  "F 스킬 · F5 팀 바꾸기 · F8 새 판(새 씨앗) · F9 팀 인원 3~6\n" +
                  "R 체크포인트 · Backspace 처음 · [ ] 이전/다음 구간" + cheat;
            // As tall as the text (the round's seven slots can take two lines).
            float height = style.CalcHeight(new GUIContent(text), 540f) + 14f;
            var box = new Rect(Screen.width - 572, 12, 560, height);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 10, box.y + 6, box.width - 20, box.height - 8), text, style);
        }
    }
}
