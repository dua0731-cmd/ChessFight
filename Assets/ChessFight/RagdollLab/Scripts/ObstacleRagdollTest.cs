using System;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // Offline fixture: the existing pawn and imported obstacle adapters own all physics.
    // Stations are ordinary saved scene objects so level designers can edit them in place.
    public sealed class ObstacleRagdollTest : MonoBehaviour
    {
        [Serializable]
        public sealed class Station
        {
            public string title;
            [TextArea] public string hint;
            public Transform spawn;
        }

        public RagdollPawn pawnPrefab;
        public RagdollTuning tuning;
        public LabCamera cameraRig;
        public Station[] stations;
        public float fallHeight = -6f;
        public int CurrentStation { get; private set; }
        public RagdollPawn Player { get; private set; }

        Vector3 savedGravity;
        CursorLockMode savedLock;
        bool savedCursor, initialized, menu;
        GUIStyle label, heading, button;

        void Awake()
        {
            if (pawnPrefab == null || tuning == null || cameraRig == null || stations == null || stations.Length != 15)
            {
                Debug.LogError("Obstacle test requires the existing pawn, tuning, camera and 15 stations.", this);
                enabled = false;
                return;
            }
            foreach (var station in stations)
                if (station == null || station.spawn == null)
                {
                    Debug.LogError("Obstacle test station has no recovery point.", this);
                    enabled = false;
                    return;
                }
            savedGravity = Physics.gravity;
            savedLock = Cursor.lockState;
            savedCursor = Cursor.visible;
            Physics.gravity = Vector3.down * 9.81f * tuning.values.gravityScale;
            initialized = true;
        }

        void Start()
        {
            Player = Instantiate(pawnPrefab, stations[0].spawn.position, stations[0].spawn.rotation);
            Player.name = "Obstacle test player";
            Player.DisplayName = "장애물 테스트";
            Player.tuning = tuning;
            Player.Team = 0;
            foreach (var body in Player.bodies)
            {
                body.solverIterations = 24;
                body.solverVelocityIterations = 6;
            }
            cameraRig.soloTarget = Player;
            SelectStation(0);
        }

        public void SelectStation(int index)
        {
            if (Player == null || index < 0 || index >= stations.Length) return;
            CurrentStation = index;
            menu = false;
            ReturnToStation();
            RefreshCursor();
        }

        public void ReturnToStation()
        {
            if (Player == null) return;
            var point = stations[CurrentStation].spawn;
            Player.SetInput(default);
            // Teleport releases grabs, climbing and moving supports and clears body velocities.
            Player.Teleport(point.position + Vector3.up * (Player.standHeight + .02f), point.forward);
            cameraRig.yaw = point.eulerAngles.y;
            cameraRig.pitch = 18;
        }

        void Update()
        {
            if (Player == null) return;
            if (!Player.IsFinite() || Player.Hips.position.y < fallHeight) ReturnToStation();
            if (!Application.isFocused) { Player.SetInput(default); return; }
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
            {
                menu = !menu;
                RefreshCursor();
            }
            if (Input.GetKeyDown(KeyCode.LeftBracket)) SelectStation((CurrentStation + stations.Length - 1) % stations.Length);
            if (Input.GetKeyDown(KeyCode.RightBracket)) SelectStation((CurrentStation + 1) % stations.Length);
            if (Input.GetKeyDown(KeyCode.R)) ReturnToStation();
            if (menu || Cursor.lockState != CursorLockMode.Locked)
            {
                Player.SetInput(default);
                return;
            }
            var move = GameSettings.Move;
            Player.SetInput(new PawnInput
            {
                move = Vector3.ClampMagnitude(cameraRig.FlatRight * move.x + cameraRig.FlatForward * move.y, 1),
                aim = cameraRig.AimForward,
                jump = GameSettings.Pressed(GameKey.Jump),
                sprint = GameSettings.Held(GameKey.Sprint),
                shove = Input.GetMouseButtonDown(0), shoveHeld = Input.GetMouseButton(0),
                grab = Input.GetMouseButton(1),
                ability = Input.GetKeyDown(KeyCode.E), ability2 = Input.GetKeyDown(KeyCode.Q),
                interact = Input.GetKey(KeyCode.F)
            });
        }

        void RefreshCursor()
        {
            Cursor.lockState = menu ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = menu;
        }

        void OnApplicationFocus(bool focused)
        {
            if (!initialized || !focused || Player == null) return;
            // Returning from another window leaves the mouse available until the player resumes.
            menu = true;
            RefreshCursor();
        }

        void OnGUI()
        {
            if (Player == null) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 16, wordWrap = true };
                heading = new GUIStyle(label) { fontSize = 21, fontStyle = FontStyle.Bold };
                button = new GUIStyle(GUI.skin.button) { font = RuntimePanels.KoreanFont, fontSize = 16 };
            }
            var previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1100f, Screen.height / 700f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            if (menu)
            {
                GUI.Box(new Rect(16, 16, 550, 660), GUIContent.none);
                GUI.Label(new Rect(30, 26, 520, 32), "래그돌 장애물 시험장 · 15종", heading);
                GUI.Label(new Rect(30, 62, 520, 42), "장애물을 선택하면 안전한 출발 지점으로 이동합니다.\n메뉴를 열어도 장애물과 물리는 계속 움직입니다.", label);
                for (int i = 0; i < stations.Length; i++)
                    if (GUI.Button(new Rect(30, 112 + i * 32, 520, 29),
                        $"{i + 1:00}. {stations[i].title}" + (i == CurrentStation ? "  · 현재" : ""), button))
                        SelectStation(i);
                if (GUI.Button(new Rect(30, 612, 250, 42), "현재 시험대 처음부터 (R)", button)) SelectStation(CurrentStation);
                if (GUI.Button(new Rect(292, 612, 258, 42), "계속하기 (Tab / Esc)", button))
                { menu = false; RefreshCursor(); }
            }
            else
            {
                GUI.Box(new Rect(16, 16, 660, 150), GUIContent.none);
                GUI.Label(new Rect(28, 23, 635, 30), $"{CurrentStation + 1:00} / 15 · {stations[CurrentStation].title}", heading);
                GUI.Label(new Rect(28, 57, 635, 48), stations[CurrentStation].hint, label);
                GUI.Label(new Rect(28, 108, 635, 52),
                    "WASD 이동 · Space 점프 · Shift 질주 · 마우스 시점 / 휠 줌\n좌클릭 태클 · 우클릭 잡기 · R 복귀 · [ / ] 이전·다음 · Tab 선택", label);
            }
            GUI.matrix = previous;
        }

        void OnDestroy()
        {
            if (!initialized) return;
            Physics.gravity = savedGravity;
            Cursor.lockState = savedLock;
            Cursor.visible = savedCursor;
            if (Player != null) Destroy(Player.gameObject);
        }
    }
}
