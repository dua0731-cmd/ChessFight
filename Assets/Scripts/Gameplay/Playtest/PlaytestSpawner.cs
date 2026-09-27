using ChessFight.Game;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // Pressing Play on a course or test scene drops one locally controlled
    // character at the first spawn point. No Steam, no lobby, no matchmaking:
    // it is how map, obstacle and ragdoll work gets tested day to day.
    //
    // Any prefab with an ICharacterDriver works. KingRush ships with the capsule
    // stand-in and a fixed-pitch CameraRig; the Queen of the Hill scene uses the
    // ragdoll and an OrbitCamera (mouse look; moves and aim are camera-relative).
    //
    // With no spawn point assigned it starts at the scene's SpawnPoint 0 of its
    // team (a level built from code places them), else at its own transform. In a
    // scene with a QueenHillMatch the water puts the character back where the
    // match's checkpoint rules say (QueenHillMatch.TryRespawn); with a
    // QueenHillRaceMatch (the current map) on rank 1 by its team's vacuum tube.
    public sealed class PlaytestSpawner : MonoBehaviour
    {
        // Set by the match flow before loading a scene for a networked match, so
        // this stand-in does not appear on top of the networked pawns.
        public static bool NetworkDriven { get; set; }

        [Tooltip("Swap the ragdoll prefab in here once it implements ICharacterDriver.")]
        [SerializeField] GameObject characterPrefab;
        [SerializeField] Transform spawnPoint;
        [SerializeField] CameraRig cameraRig;
        [Tooltip("Mouse-look camera for climbing scenes. Used instead of the camera rig when set.")]
        [SerializeField] OrbitCamera orbitCamera;
        [Tooltip("-1 = no team. 0 = white, 1 = black (Teams): bells, en passant and team checkpoints need one.")]
        [SerializeField, Range(-1, 1)] int team = -1;
        [Tooltip("Falling below this height counts as falling off the course.")]
        [SerializeField] float fallLimit = -20f;
        [Tooltip("0 = off. Falling faster than this (m/s) counts as falling into the water: a long drop onto a lower "
                 + "level of a stacked tower ends the climb like the sea does (15 m/s is a free fall of about 11 m).")]
        [SerializeField] float fallCatchSpeed;
        [SerializeField] bool showHelp = true;

        IMoveInputSource input;
        ICharacterDriver driver;
        Vector3 startPosition, respawnPosition;
        Quaternion startRotation, respawnRotation;
        int reachedCheckpoint = int.MinValue;
        float runStart;
        string result = "";
        bool finished;
        float respawnAt = -1f;   // set while the character is in the water or caught falling
        bool caughtFalling;
        float lastY = float.NaN, fallSpeed;
        GUIStyle style;
        GameObject instance;

        public ICharacterDriver Driver => driver;
        public int Team => team;
        public Vector3 StartPosition => startPosition;
        public Quaternion StartRotation => startRotation;
        public float RunSeconds => Time.time - runStart;

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
            var at = spawnPoint != null ? spawnPoint : FindSpawn();
            startPosition = respawnPosition = at.position;
            startRotation = respawnRotation = at.rotation;

            instance = Instantiate(characterPrefab, startPosition, startRotation);
            instance.name = characterPrefab.name + " (Playtest)";
            driver = instance.GetComponentInChildren<ICharacterDriver>();
            if (driver is ITeamAssignable member && team >= 0) member.AssignTeam(team);
            // Spawn points mark the ground; let the character stand itself on it.
            driver?.Teleport(startPosition, startRotation);
            if (driver == null)
                Debug.LogError("[ChessFight] " + characterPrefab.name + "에 ICharacterDriver를 구현한 컴포넌트가 없습니다. " +
                               "Docs/TEAM_GUIDE_KO.md의 래그돌 어댑터를 참고하세요.");

            input = MoveInputSources.Create();
            input.Enable();
            if (orbitCamera != null && driver != null)
                orbitCamera.Follow(driver.FollowTarget, instance.transform, startRotation * Vector3.forward);
            else if (cameraRig != null) cameraRig.Initialize();
            runStart = Time.time;
        }

        // The scene's first spawn point of this team (index 0 first), else this object.
        Transform FindSpawn()
        {
            SpawnPoint best = null;
            foreach (var point in FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (team >= 0 && point.Team != team) continue;
                if (best == null || point.Index < best.Index) best = point;
            }
            return best != null ? best.transform : transform;
        }

        void Update()
        {
            if (driver == null) return;
            bool flying = orbitCamera != null && orbitCamera.FreeFly;
            var intent = Application.isFocused && !flying ? input.Read() : default;
            Vector3 move, aim;
            if (orbitCamera != null)
            {
                // Keys are read against the camera; the command is in world space.
                move = orbitCamera.FlatRight * intent.Move.x + orbitCamera.FlatForward * intent.Move.y;
                aim = orbitCamera.AimForward;
            }
            else
            {
                // The rig here only pitches, so screen-up is world +Z and no
                // camera-relative conversion is needed.
                var view = cameraRig != null && cameraRig.Target != null ? cameraRig.Target.transform : null;
                move = new Vector3(intent.Move.x, 0f, intent.Move.y);
                aim = view != null ? view.forward : Vector3.forward;
            }
            driver.SetCommand(new CharacterCommand
            {
                Move = Vector3.ClampMagnitude(move, 1f),
                Jump = intent.Jump, Shove = intent.Shove, ShoveHeld = intent.ShoveHeld, Grab = intent.Grab,
                Sprint = intent.Sprint, Ability = intent.Ability, Ability2 = intent.Ability2,
                Interact = intent.Interact,
                Aim = aim
            });

            var target = driver.FollowTarget;
            CatchLongFall(target);
            if (!flying && LegacyKeys.Down(KeyCode.Backspace)) Restart();
            else if ((!flying && LegacyKeys.Down(KeyCode.R)) || (target != null && target.position.y < fallLimit)
                     || (respawnAt >= 0f && Time.time >= respawnAt))
                Respawn();
            if (orbitCamera == null && cameraRig != null && target != null) cameraRig.Follow(target, Time.deltaTime);
        }

        // A long fall: the same as the water, a little sooner.
        void CatchLongFall(Transform target)
        {
            if (fallCatchSpeed <= 0f || target == null || Time.deltaTime <= 0f) return;
            float y = target.position.y;
            if (!float.IsNaN(lastY))
                fallSpeed = Mathf.Lerp(fallSpeed, (lastY - y) / Time.deltaTime, 1f - Mathf.Exp(-12f * Time.deltaTime));
            lastY = y;
            // Over open water the sea catches it (and floats it) instead.
            if (respawnAt < 0f && fallSpeed > fallCatchSpeed && SolidBelow(target.position))
            {
                respawnAt = Time.time + 0.6f;
                caughtFalling = true;
            }
        }

        readonly RaycastHit[] below = new RaycastHit[16];

        bool SolidBelow(Vector3 from)
        {
            int n = Physics.RaycastNonAlloc(from, Vector3.down, below, 400f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (instance == null || !below[i].collider.transform.IsChildOf(instance.transform)) return true;
            return false;
        }

        // Put the character somewhere else (the playtest tools' section jumps).
        public void MoveTo(Vector3 position, Quaternion rotation)
        {
            respawnAt = -1f;
            caughtFalling = false;
            lastY = float.NaN;
            fallSpeed = 0f;
            driver?.Teleport(position, rotation);
            if (orbitCamera != null) orbitCamera.Cut();
        }

        void Respawn()
        {
            respawnAt = -1f;
            var race = QueenHillRaceMatch.Current;
            if (race != null)
            {
                // Queen of the Hill: no checkpoints. Rank 1 beside the team's vacuum tube
                // (its bridge if it never made it across).
                if (!race.TryRespawn(driver, team, out var at, out var facing))
                {
                    at = startPosition;
                    facing = startRotation;
                }
                MoveTo(at, facing);
                return;
            }
            var match = QueenHillMatch.Current;
            if (match != null)
            {
                // Queen of the Hill: the higher of its own and its team's checkpoint, else the bridge.
                if (!match.TryRespawn(driver, team, out var position, out var rotation))
                {
                    position = startPosition;
                    rotation = startRotation;
                }
                MoveTo(position, rotation);
                return;
            }
            MoveTo(respawnPosition, respawnRotation);
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
            // A fresh round: every bell silent, every checkpoint and rank forgotten.
            QueenHillMatch.Current?.ResetRound();
            QueenHillRaceMatch.Current?.ResetRound();
            MoveTo(startPosition, startRotation);
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

        Texture2D white;

        // A plain bar at the bottom middle: green to red as it empties.
        void StaminaBar(float stamina)
        {
            if (white == null) white = Texture2D.whiteTexture;
            const float width = 240f, height = 12f;
            var back = new Rect((Screen.width - width) * 0.5f, Screen.height - 44f, width, height);
            var old = GUI.color;
            GUI.color = new Color(0.08f, 0.1f, 0.14f, 0.7f);
            GUI.DrawTexture(back, white);
            GUI.color = Color.Lerp(new Color(1f, 0.32f, 0.26f), new Color(0.4f, 0.92f, 0.46f), stamina);
            GUI.DrawTexture(new Rect(back.x + 2f, back.y + 2f, (width - 4f) * Mathf.Clamp01(stamina), height - 4f), white);
            GUI.color = old;
            GUI.Label(new Rect(back.x, back.y - 20f, width, 20f), "스테미나", style);
        }

        void OnGUI()
        {
            if (!showHelp || driver == null) return;
            if (style == null)
                style = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 14, richText = true };
            string text =
                "<b>플레이테스트 (오프라인)</b>" + (team >= 0 ? $" · {Teams.Name(team)}팀" : "") + "\n" +
                "WASD 이동 · Space 점프 · 좌클릭 밀치기 · 우클릭 잡기\n" +
                (orbitCamera != null
                    ? "Shift 질주 · E 능력(폰: 갈고리) · 좌클릭 꾹 = 게이지, 떼면 던짐 · F 종·승격·앙파상\n" +
                      "마우스 시점(게임 화면 클릭) · 휠 줌 · Esc 커서 · V 자유 카메라(WASD·Space·C)\n"
                    : "") +
                (orbitCamera != null && orbitCamera.FreeFly ? "<b>자유 카메라</b> — V로 캐릭터에게 돌아가기\n" : "") +
                (respawnAt >= 0f ? (caughtFalling ? "<b>추락!</b>" : "<b>물에 빠졌어요!</b>") + " 곧 체크포인트로 돌아갑니다\n" : "") +
                "R 체크포인트로 · Backspace 처음부터\n" +
                $"기록 {(finished ? result : (Time.time - runStart).ToString("0.0") + "초")}";
            float width = orbitCamera != null ? 520 : 360;
            float height = style.CalcHeight(new GUIContent(text), width - 20) + 12;
            GUI.Box(new Rect(12, 12, width, height), GUIContent.none);
            GUI.Label(new Rect(22, 18, width - 20, height - 8), text, style);
            if (driver is IStaminaReadout readout) StaminaBar(readout.Stamina01);
        }
    }
}
