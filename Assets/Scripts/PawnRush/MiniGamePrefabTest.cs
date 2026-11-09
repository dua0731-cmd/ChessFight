using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A local authoring scene: select any of the ten actual prefabs without loading the old two-plaza course.
    public sealed class MiniGamePrefabTest : MonoBehaviour
    {
        [SerializeField] GameObject[] prefabs = new GameObject[0];
        [SerializeField] PlaytestSpawner spawner;
        [SerializeField] int selected;
        GameObject current;
        MiniGameSection plaza;
        GUIStyle style;

        public MiniGameSection Current => plaza;

        public void Configure(GameObject[] choices, PlaytestSpawner player)
        {
            prefabs = choices;
            spawner = player;
        }

        void Awake()
        {
            MiniGameBase.Remote = false;
            Select(selected, false);
            // The normal section test also keeps the Queen of the Hill grappling hook out of its course.
            var noHook = new GameObject("No grappling hook in the mini-game test");
            noHook.transform.SetParent(transform, false);
            noHook.transform.position = new Vector3(0f, -500f, 0f);
            noHook.AddComponent<BoxCollider>().isTrigger = true;
            noHook.AddComponent<HookStartZone>();
        }

        void Start() => LocalPlayer();

        public void Select(int index, bool respawn = true)
        {
            if (prefabs == null || prefabs.Length == 0) return;
            index = Mathf.Clamp(index, 0, prefabs.Length - 1);
            if (prefabs[index] == null) return;
            if (current != null)
            {
                current.SetActive(false);
                Destroy(current);
            }
            selected = index;
            current = new GameObject("Mini-game being tested");
            current.SetActive(false);
            var instance = Instantiate(prefabs[index], current.transform);
            instance.name = prefabs[index].name;
            plaza = instance.GetComponent<MiniGameSection>();
            instance.GetComponent<PawnRushSection>()?.Configure(0, 312 + index, 6, ObstacleClock.Now);
            for (int team = 0; team < 2; team++)
            {
                var spawn = new GameObject("Test start " + team);
                spawn.transform.SetParent(current.transform, false);
                spawn.transform.localPosition = new Vector3(team == Teams.White ? -3f : 3f, 0f, 1.5f);
                spawn.AddComponent<SpawnPoint>().Configure(0, team);
            }
            current.SetActive(true);
            if (respawn && spawner != null && spawner.Driver != null) spawner.SetTeam(spawner.Team);
            LocalPlayer();
        }

        void LocalPlayer()
        {
            if (spawner == null) return;
            MissionStation.LocalTeam = spawner.Team;
            MiniGameBase.LocalDriver = spawner.Driver;
        }

        void Update()
        {
            if (spawner != null && MiniGameBase.LocalDriver != spawner.Driver) LocalPlayer();
            for (int i = 0; i < 10 && i < prefabs.Length; i++)
                if (LegacyKeys.Down(i == 9 ? KeyCode.Alpha0 : (KeyCode)((int)KeyCode.Alpha1 + i))) Select(i);
            if (spawner == null) return;
            if (LegacyKeys.Down(KeyCode.F5))
            {
                spawner.SetTeam(spawner.Team == Teams.White ? Teams.Black : Teams.White);
                LocalPlayer();
            }
            if (LegacyKeys.Down(KeyCode.F6) && plaza != null)
                foreach (var station in plaza.Stations)
                    if (station != null && station.Team == spawner.Team) station.Game?.ForceComplete();
            if (LegacyKeys.Down(KeyCode.F8)) Select(selected);
        }

        void OnDestroy()
        {
            if (current != null) Destroy(current);
            if (spawner != null && MiniGameBase.LocalDriver == spawner.Driver) MiniGameBase.LocalDriver = null;
        }

        void OnGUI()
        {
            if (style == null) style = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 14, richText = true, wordWrap = true };
            GUILayout.BeginArea(new Rect(12f, 12f, Mathf.Min(Screen.width - 24f, 560f), 186f), GUI.skin.box);
            GUILayout.Label("<b>미니게임 프리팹 시험</b> · " + (plaza != null ? MiniGameSectionBuilder.Names[plaza.GameIndex] : "준비 중"), style);
            // R500: one row per phase (slots 2 · 4 · 6), the same order as the number keys.
            GUILayout.Label("<b>1단계</b>  1 도개교 · 2 쇠살문 · 3 의장 광장\n<b>2단계</b>  4 동관 · 5 서고 · 6 무도회장 · 7 세탁장\n<b>3단계</b>  8 행차 회랑 · 9 체스 홀 · 0 침실", style);
            GUILayout.Label("F5 팀 전환 · F6 자기 팀 완료 시험 · F8 다시 시작\nR 체크포인트 · Backspace 처음 · F1 캐릭터 비행(개발) · F4 자유 카메라", style);
            if (plaza != null)
                foreach (var station in plaza.Stations)
                    if (station != null && station.Game != null)
                        GUILayout.Label(Teams.Name(station.Team) + " " + Mathf.RoundToInt(station.Game.Progress01 * 100f) + "% · " + (station.Gate.IsOpen ? "문 열림" : "진행 중"), style);
            GUILayout.EndArea();
        }
    }
}
