using ChessFight.Game;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // Pressing Play on a course or test scene drops one locally controlled
    // character at the first spawn point. No Steam, no lobby, no matchmaking:
    // it is how map, obstacle and ragdoll work gets tested day to day.
    //
    // Any prefab with an ICharacterDriver works. It ships with the capsule
    // stand-in; point characterPrefab at the ragdoll prefab once it has an adapter.
    public sealed class PlaytestSpawner : MonoBehaviour
    {
        // Set by the match flow before loading a scene for a networked match, so
        // this stand-in does not appear on top of the networked pawns.
        public static bool NetworkDriven { get; set; }

        [Tooltip("Swap the ragdoll prefab in here once it implements ICharacterDriver.")]
        [SerializeField] GameObject characterPrefab;
        [SerializeField] Transform spawnPoint;
        [SerializeField] CameraRig cameraRig;
        [Tooltip("Falling below this height counts as falling off the course.")]
        [SerializeField] float fallLimit = -20f;
        [SerializeField] bool showHelp = true;

        IMoveInputSource input;
        ICharacterDriver driver;
        Vector3 startPosition, respawnPosition;
        Quaternion startRotation, respawnRotation;
        int reachedCheckpoint = int.MinValue;
        float runStart;
        string result = "";
        bool finished;
        float respawnAt = -1f;   // set while the character is in the water
        GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => NetworkDriven = false;

        void OnEnable()
        {
            Checkpoint.Reached += OnCheckpoint;
            FinishZone.Reached += OnFinish;
            WaterZone.Entered += OnWater;
        }

        void OnDisable()
        {
            Checkpoint.Reached -= OnCheckpoint;
            FinishZone.Reached -= OnFinish;
            WaterZone.Entered -= OnWater;
        }

        void Start()
        {
            if (NetworkDriven) { enabled = false; return; }
            if (characterPrefab == null)
            {
                Debug.LogError("[ChessFight] PlaytestSpawner에 캐릭터 프리팹이 지정되지 않았습니다.");
                enabled = false;
                return;
            }
            var at = spawnPoint != null ? spawnPoint : transform;
            startPosition = respawnPosition = at.position;
            startRotation = respawnRotation = at.rotation;

            var instance = Instantiate(characterPrefab, startPosition, startRotation);
            instance.name = characterPrefab.name + " (Playtest)";
            driver = instance.GetComponentInChildren<ICharacterDriver>();
            // Spawn points mark the ground; let the character stand itself on it.
            driver?.Teleport(startPosition, startRotation);
            if (driver == null)
                Debug.LogError("[ChessFight] " + characterPrefab.name + "에 ICharacterDriver를 구현한 컴포넌트가 없습니다. " +
                               "Docs/TEAM_GUIDE_KO.md의 래그돌 어댑터를 참고하세요.");

            input = MoveInputSources.Create();
            input.Enable();
            if (cameraRig != null) cameraRig.Initialize();
            runStart = Time.time;
        }

        void Update()
        {
            if (driver == null) return;
            var intent = Application.isFocused ? input.Read() : default;
            // The camera here only pitches, so screen-up is world +Z and no
            // camera-relative conversion is needed.
            var view = cameraRig != null && cameraRig.Target != null ? cameraRig.Target.transform : null;
            driver.SetCommand(new CharacterCommand
            {
                Move = new Vector3(Mathf.Clamp(intent.Move.x, -1f, 1f), 0f, Mathf.Clamp(intent.Move.y, -1f, 1f)),
                Jump = intent.Jump, Shove = intent.Shove, Grab = intent.Grab,
                Sprint = intent.Sprint, Ability = intent.Ability, Ability2 = intent.Ability2,
                Interact = intent.Interact,
                Aim = view != null ? view.forward : Vector3.forward
            });

            var target = driver.FollowTarget;
            if (LegacyKeys.Down(KeyCode.Backspace)) Restart();
            else if (LegacyKeys.Down(KeyCode.R) || (target != null && target.position.y < fallLimit)
                     || (respawnAt >= 0f && Time.time >= respawnAt))
                Respawn();
            if (cameraRig != null && target != null) cameraRig.Follow(target, Time.deltaTime);
        }

        void Respawn()
        {
            respawnAt = -1f;
            driver.Teleport(respawnPosition, respawnRotation);
        }

        void Restart()
        {
            respawnAt = -1f;
            reachedCheckpoint = int.MinValue;
            respawnPosition = startPosition;
            respawnRotation = startRotation;
            finished = false;
            result = "";
            runStart = Time.time;
            driver.Teleport(startPosition, startRotation);
        }

        void OnCheckpoint(ICharacterDriver who, Checkpoint checkpoint)
        {
            if (who != driver || checkpoint.Order <= reachedCheckpoint) return;
            reachedCheckpoint = checkpoint.Order;
            respawnPosition = checkpoint.RespawnPosition;
            respawnRotation = checkpoint.transform.rotation;
        }

        // Stage-one rule: a fall into the water puts the character back on its last
        // checkpoint after the water's delay.
        void OnWater(ICharacterDriver who, WaterZone water)
        {
            if (who != driver || respawnAt >= 0f) return;
            respawnAt = Time.time + water.RespawnDelay;
        }

        void OnFinish(ICharacterDriver who)
        {
            if (who != driver || finished) return;
            finished = true;
            result = $"골인! {Time.time - runStart:0.00}초";
        }

        void OnDestroy() => input?.Disable();

        void OnGUI()
        {
            if (!showHelp || driver == null) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 14, richText = true };
            float height = respawnAt >= 0f ? 132 : 112;
            GUI.Box(new Rect(12, 12, 360, height), GUIContent.none);
            GUI.Label(new Rect(22, 18, 350, height - 8),
                "<b>플레이테스트 (오프라인)</b>\n" +
                "WASD 이동 · Space 점프 · 좌클릭 밀치기 · 우클릭 잡기\n" +
                (respawnAt >= 0f ? "<b>물에 빠졌어요!</b> 곧 체크포인트로 돌아갑니다\n" : "") +
                "R 체크포인트로 · Backspace 처음부터\n" +
                $"기록 {(finished ? result : (Time.time - runStart).ToString("0.0") + "초")}", style);
        }
    }
}
