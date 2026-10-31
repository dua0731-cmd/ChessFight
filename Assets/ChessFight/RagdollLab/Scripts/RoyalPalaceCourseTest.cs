using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    // Offline course fixture. Only the selected saved prefab is simulated.
    public sealed class RoyalPalaceCourseTest : MonoBehaviour
    {
        [SerializeField] RagdollPawn pawnPrefab;
        [SerializeField] RagdollTuning tuning;
        [SerializeField] LabCamera cameraRig;
        [SerializeField] CourseModule[] courses;
        [SerializeField] CourseModule initialInstance;
        [SerializeField] float fallHeight = -8;
        CourseModule current;
        RagdollPawn player;
        int index, checkpoint, falls;
        float started, finished = -1;
        bool menu, initialized;
        Vector3 savedGravity;
        CursorLockMode savedLock;
        bool savedCursor;
        GUIStyle label, button;

        void Awake()
        {
            if (pawnPrefab == null || tuning == null || cameraRig == null || courses == null || courses.Length == 0)
            { Debug.LogError("Assign the pawn, tuning, camera and course prefabs.", this); enabled = false; return; }
            foreach (var course in courses)
                if (course == null || course.Entry == null || course.Exit == null)
                { Debug.LogError("Course prefab needs entry and exit connectors.", this); enabled = false; return; }
            savedGravity = Physics.gravity;
            savedLock = Cursor.lockState;
            savedCursor = Cursor.visible;
            Physics.gravity = Vector3.down * 9.81f * tuning.values.gravityScale;
            initialized = true;
        }

        void Start()
        {
            current = initialInstance;
            player = Instantiate(pawnPrefab);
            player.name = "Royal palace test player";
            player.DisplayName = "코스 테스트";
            player.tuning = tuning;
            player.Team = 0;
            foreach (var body in player.bodies)
            { body.solverIterations = 24; body.solverVelocityIterations = 6; }
            cameraRig.soloTarget = player;
            Select(0, current != null);
        }

        void Select(int next, bool keepInitial = false)
        {
            if (!keepInitial)
            {
                if (current != null) { current.gameObject.SetActive(false); Destroy(current.gameObject); }
                current = Instantiate(courses[next], Vector3.zero, Quaternion.identity);
            }
            index = next;
            checkpoint = falls = 0;
            finished = -1;
            started = Time.time;
            menu = false;
            Respawn();
            RefreshCursor();
        }

        void Respawn()
        {
            var point = checkpoint == 0 ? current.Entry : current.Checkpoints[checkpoint - 1];
            player.SetInput(default);
            player.Teleport(point.position + Vector3.up * (player.standHeight + .02f), point.forward);
            cameraRig.yaw = point.eulerAngles.y;
            cameraRig.pitch = 18;
        }

        bool Near(Transform point)
        {
            var local = point.InverseTransformPoint(player.Hips.position);
            return Mathf.Abs(local.x) < current.LaneWidth * .5f && Mathf.Abs(local.z) < 2
                && local.y > -.2f && local.y < 3;
        }

        void Update()
        {
            if (player == null || current == null) return;
            if (!player.IsFinite() || player.Hips.position.y < fallHeight) { falls++; Respawn(); }
            var points = current.Checkpoints;
            if (points != null && checkpoint < points.Length && points[checkpoint] != null && Near(points[checkpoint])) checkpoint++;
            if (finished < 0 && checkpoint == (points == null ? 0 : points.Length) && Near(current.Exit)) finished = Time.time - started;
            if (!Application.isFocused) { player.SetInput(default); return; }
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape)) { menu = !menu; RefreshCursor(); }
            if (Input.GetKeyDown(KeyCode.LeftBracket)) Select((index + courses.Length - 1) % courses.Length);
            if (Input.GetKeyDown(KeyCode.RightBracket)) Select((index + 1) % courses.Length);
            if (Input.GetKeyDown(KeyCode.R)) Respawn();
            if (Input.GetKeyDown(KeyCode.F3)) Select(index);
            if (menu || Cursor.lockState != CursorLockMode.Locked) { player.SetInput(default); return; }
            var move = GameSettings.Move;
            player.SetInput(new PawnInput
            {
                move = Vector3.ClampMagnitude(cameraRig.FlatRight * move.x + cameraRig.FlatForward * move.y, 1),
                aim = cameraRig.AimForward,
                jump = GameSettings.Pressed(GameKey.Jump),
                sprint = GameSettings.Held(GameKey.Sprint),
                shove = Input.GetMouseButtonDown(0), shoveHeld = Input.GetMouseButton(0), grab = Input.GetMouseButton(1),
                ability = Input.GetKeyDown(KeyCode.E), ability2 = Input.GetKeyDown(KeyCode.Q), interact = Input.GetKey(KeyCode.F)
            });
        }

        void RefreshCursor()
        { Cursor.lockState = menu ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = menu; }

        void OnApplicationFocus(bool focused)
        { if (initialized && focused && player != null) { menu = true; RefreshCursor(); } }

        void OnGUI()
        {
            if (current == null || player == null) return;
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { font = RuntimePanels.KoreanFont, fontSize = 16, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { font = RuntimePanels.KoreanFont, fontSize = 16 };
            }
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * Mathf.Min(Screen.width / 1100f, Screen.height / 700f));
            GUI.Box(new Rect(16, 16, 660, menu ? 450 : 164), GUIContent.none);
            GUI.Label(new Rect(28, 25, 635, 148),
                $"{current.CourseId} · {current.DisplayName}\n" +
                $"주행 {(finished < 0 ? Time.time - started : finished):F1}초 · 목표 {current.TargetSeconds:F0}초 · 낙사 {falls}회 · 체크포인트 {checkpoint}/3\n" +
                (finished < 0 ? "4분은 조정 목표입니다. 미니게임은 교체용 빈 공간입니다.\n" : "코스 통과! 계속 움직이거나 F3으로 처음부터 다시 시험하세요.\n") +
                "WASD 이동 · Space 점프 · Shift 질주 · 좌클릭 태클 · 우클릭 잡기\nR 체크포인트 · F3 처음부터 · [ / ] 코스 변경 · Tab 메뉴", label);
            if (menu)
            {
                for (int i = 0; i < courses.Length; i++)
                    if (GUI.Button(new Rect(28, 190 + 42 * i, 630, 36), courses[i].DisplayName, button)) Select(i);
                if (GUI.Button(new Rect(28, 405, 630, 36), "계속하기", button)) { menu = false; RefreshCursor(); }
            }
            GUI.matrix = matrix;
        }

        void OnDestroy()
        {
            if (!initialized) return;
            Physics.gravity = savedGravity;
            Cursor.lockState = savedLock;
            Cursor.visible = savedCursor;
            if (player != null) Destroy(player.gameObject);
        }
    }
}
