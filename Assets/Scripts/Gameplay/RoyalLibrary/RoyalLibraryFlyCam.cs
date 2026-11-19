using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.Gameplay
{
    // TEMPORARY (seoungtest R687, 승규 11-18 "임시로 f2키를 맵 둘러보기로 설정"): F2 in the Royal Library course
    // (GiantChessCourse scene) toggles a free fly camera to look the map over. 11-18 (승규 "카메라만 움직이지 말고 기물도
    // 같이 움직이면서 장애물 프리팹 시험"): time keeps running, so the giants' moves, the closing books, the stamps, the
    // typewriter keys and the bots go on while you fly up to them. 11-18 (승규 "안따라가잖아"): the player's pawn rides
    // along - it hangs a few metres ahead of and below the camera while you fly, and F2 again sets it down on the ground
    // under that spot and hands the camera back to the course's rig there, so any obstacle can be tried at once.
    // 11-18 (승규 "거대 기물이 공격을 안하는데"): the giants only start playing once a pawn has walked the front section's
    // checkpoints in order into the chess room (GiantChessApproach.Observe), which a pawn set down by F2 never did - so a
    // pawn set down in the chess room (board, entry deck or exit deck) is marked as having arrived and the game starts
    // (Enter must still have been pressed once).
    // The course's camera rig is switched off and its freeMode flag raised meanwhile; the course's own keys
    // (Enter, N, B, R, F3) still work.
    //   mouse  look        W A S D  move        Q / E  down / up        Shift  x4        wheel  speed
    // Attaches itself to every RoyalLibraryDressing in a loaded scene (as RoyalLibraryLighting does), so F2 works in a
    // Pawn Rush round's library section too. On main as it is (R706, 승규 11-18 "지금처럼 두기"); a debug key the team
    // agrees on can replace it later.
    public sealed class RoyalLibraryFlyCam : MonoBehaviour
    {
        const KeyCode Toggle = KeyCode.F2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneLoaded += Loaded;
            Attach();
        }

        static void Loaded(Scene scene, LoadSceneMode mode) => Attach();

        static void Attach()
        {
            foreach (var d in FindObjectsByType<RoyalLibraryDressing>(FindObjectsSortMode.None))
                if (d.GetComponent<RoyalLibraryFlyCam>() == null)
                    d.gameObject.AddComponent<RoyalLibraryFlyCam>();
        }

        bool flying;
        float speed = 25f, yaw, pitch;
        Vector3 savedPos;
        Quaternion savedRot;
        Transform savedParent;
        bool savedVisible;
        CursorLockMode savedLock;
        Camera cam;
        readonly List<Behaviour> paused = new List<Behaviour>();
        readonly List<(Object rig, System.Reflection.FieldInfo field, bool was)> freed = new List<(Object, System.Reflection.FieldInfo, bool)>();
        GUIStyle style;
        Component pawn;            // RagdollLab's RagdollPawn (out of this assembly's reach: reflection)
        Rigidbody[] pawnBodies;
        System.Reflection.MethodInfo teleport;
        float standHeight = .26f;
        MonoBehaviour course;      // RagdollLab's GiantChessCourse, when that is what is being played
        int courseIndex;
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        static readonly Vector3 Carry = new Vector3(0, -1.8f, 4.5f);   // camera space: where the pawn hangs while flying

        void Update()
        {
            if (Input.GetKeyDown(Toggle))
            {
                if (flying) Land(); else TakeOff();
            }
            if (!flying || cam == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            yaw += Input.GetAxisRaw("Mouse X") * 2.2f;
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 2.2f, -89f, 89f);
            cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            speed = Mathf.Clamp(speed * (1 + Input.mouseScrollDelta.y * .15f), 2f, 400f);
            var move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) move += Vector3.back;
            if (Input.GetKey(KeyCode.D)) move += Vector3.right;
            if (Input.GetKey(KeyCode.A)) move += Vector3.left;
            var up = 0f;
            if (Input.GetKey(KeyCode.E)) up += 1;
            if (Input.GetKey(KeyCode.Q)) up -= 1;
            float s = speed * (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 4f : 1f) * dt;
            cam.transform.position += (cam.transform.rotation * move.normalized + Vector3.up * up) * s;
            if (pawn != null) Teleport(CarryPoint(), cam.transform.forward);
        }

        Vector3 CarryPoint() => cam.transform.position + Quaternion.Euler(0, yaw, 0) * Carry;

        // The pawn under control: the giant chess course's selected one, else the lab's local pawn.
        void FindPawn()
        {
            pawn = null; pawnBodies = null; teleport = null; course = null;
            foreach (var b in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                var t = b.GetType();
                if (t.Name == "GiantChessCourse" && b.enabled)
                {
                    var list = t.GetField("players", Any)?.GetValue(b) as System.Collections.IList;
                    var sel = t.GetField("selected", Any)?.GetValue(b);
                    if (list != null && sel is int i && i >= 0 && i < list.Count) { pawn = list[i] as Component; course = b; courseIndex = i; }
                    break;
                }
                if (t.Name == "LabGame" && pawn == null)
                    pawn = t.GetProperty("LocalPawn", Any)?.GetValue(b) as Component;
            }
            if (pawn == null) return;
            var pt = pawn.GetType();
            teleport = pt.GetMethod("Teleport", new[] { typeof(Vector3), typeof(Vector3) });
            pawnBodies = pt.GetField("bodies")?.GetValue(pawn) as Rigidbody[];
            if (pt.GetField("standHeight")?.GetValue(pawn) is float h) standHeight = h;
            if (teleport == null) pawn = null;
        }

        void Teleport(Vector3 hips, Vector3 face)
        {
            if (pawn == null || teleport == null) return;
            if (pawn is Behaviour be && !be.isActiveAndEnabled) return;
            teleport.Invoke(pawn, new object[] { hips, face });
        }

        // A pawn set down in the giant chess room counts as having come through the front section (see the header).
        void EnterChessRoom(Vector3 at)
        {
            if (course == null) return;
            var t = course.GetType();
            float tile = t.GetField("tileSize", Any)?.GetValue(course) is float ts ? ts : 10f;
            float depth = t.GetField("chessSpawnDepth", Any)?.GetValue(course) is float sd ? sd : 10f;
            var p = course.transform.InverseTransformPoint(at);
            if (p.z < -(depth + 8) || p.z > tile * 8 + 25 || Mathf.Abs(p.x) > tile * 5) return;
            var approach = t.GetField("approach", Any)?.GetValue(course) as Component;
            if (approach == null) return;
            var at2 = approach.GetType();
            if (at2.GetField("entered", Any)?.GetValue(approach) is bool[] entered && courseIndex < entered.Length) entered[courseIndex] = true;
            if (at2.GetField("checkpoints", Any)?.GetValue(approach) is int[] cps && courseIndex < cps.Length
                && at2.GetProperty("CheckpointCount", Any)?.GetValue(approach) is int count) cps[courseIndex] = count;
            at2.GetProperty("ChessStarted", Any)?.SetValue(approach, true);
        }

        // The ground under p (the pawn's own bodies skipped), or p itself when nothing is below.
        Vector3 GroundUnder(Vector3 p)
        {
            var hits = Physics.RaycastAll(p + Vector3.up * .5f, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (pawnBodies != null && h.rigidbody != null && System.Array.IndexOf(pawnBodies, h.rigidbody) >= 0) continue;
                return h.point + Vector3.up * (standHeight + .1f);
            }
            return p;
        }

        void TakeOff()
        {
            cam = Camera.main;
            if (cam == null) return;
            flying = true;
            savedPos = cam.transform.position; savedRot = cam.transform.rotation; savedParent = cam.transform.parent;
            savedVisible = Cursor.visible; savedLock = Cursor.lockState;
            // The course's camera rig (RagdollLab's LabCamera, out of this assembly's reach: found by name) would
            // pull the camera back every frame.
            paused.Clear();
            foreach (var b in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (b.enabled && b.GetType().Name == "LabCamera")
                {
                    b.enabled = false; paused.Add(b);
                    // LabGame skips the keyboard pawn's input while the rig is in free mode
                    var f = b.GetType().GetField("freeMode");
                    if (f != null && f.FieldType == typeof(bool))
                    {
                        freed.Add((b, f, (bool)f.GetValue(b)));
                        f.SetValue(b, true);
                    }
                }
            cam.transform.SetParent(null, true);
            var e = cam.transform.rotation.eulerAngles;
            yaw = e.y; pitch = e.x > 180 ? e.x - 360 : e.x;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            FindPawn();
        }

        void Land()
        {
            flying = false;
            foreach (var (rig, field, was) in freed)
                if (rig != null) field.SetValue(rig, was);
            freed.Clear();
            bool carried = pawn != null && cam != null;
            if (carried)
            {
                var spot = GroundUnder(CarryPoint());
                Teleport(spot, Quaternion.Euler(0, yaw, 0) * Vector3.forward);
                EnterChessRoom(spot);
            }
            if (cam != null)
            {
                cam.transform.SetParent(savedParent, true);
                // with the pawn set down somewhere new the rig picks the camera up behind it, looking the same way
                if (!carried) cam.transform.SetPositionAndRotation(savedPos, savedRot);
            }
            foreach (var b in paused)
            {
                if (b == null) continue;
                if (carried && b.GetType().GetField("yaw") is var fy && fy != null && fy.FieldType == typeof(float)) fy.SetValue(b, yaw);
                b.enabled = true;
            }
            pawn = null; pawnBodies = null; teleport = null; course = null;
            paused.Clear();
            Cursor.lockState = savedLock; Cursor.visible = savedVisible;
        }

        void OnDisable()
        {
            if (flying) Land();
        }

        void OnGUI()
        {
            if (!flying) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, normal = { textColor = Color.white } };
            GUI.Label(new Rect(16, 16, 900, 30),
                      "맵 둘러보기 (F2 끄면 폰을 그 아래 바닥에 내려놓음 · 게임은 계속 진행 · Enter 시작 · N 다음 단계) · 마우스 시선 · WASD 이동 · Q/E 아래/위 · Shift 빠르게 · 휠 속도 " + speed.ToString("0") + " m/s",
                      style);
        }
    }
}
