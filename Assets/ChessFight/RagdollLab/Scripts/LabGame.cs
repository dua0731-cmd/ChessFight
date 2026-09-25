using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public enum LabDevice { KeyboardMouse, KeyboardArrows, Pad1, Pad2, Pad3, Pad4 }

    /// <summary>
    /// Local two-player ragdoll test rig: spawns pawns, routes input, and owns the lab hotkeys
    /// (R respawn, T slow motion, F free camera, Tab tuning panel). No networking by design.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class LabGame : MonoBehaviour
    {
        [Serializable]
        public class Slot
        {
            public string name = "P1";
            public LabDevice device = LabDevice.KeyboardMouse;
            public Vector3 spawn;
            public Material material;
            [NonSerialized] public RagdollPawn pawn;
        }

        public RagdollPawn pawnPrefab;
        public RagdollTuning tuning;
        public LabCamera labCamera;
        public SpinningBar bar;
        public Slot[] players = { new Slot(), new Slot { name = "P2", device = LabDevice.Pad1 } };
        public Material dummyMaterial;
        [Tooltip("120 Hz: the leg chain needs it (or ~40 solver iterations at 60 Hz) to carry the body weight.")]
        public int physicsRate = 120;
        public int solverIterations = 24;
        public float slowMotionScale = 0.3f;

        public readonly List<RagdollPawn> dummies = new List<RagdollPawn>();
        public bool PanelOpen { get; set; }
        public bool SlowMotion { get; private set; }
        public bool AutoTest { get; private set; }
        public string Status { get; set; } = "";
        public string ParamsPath => Path.Combine(Application.persistentDataPath, "RagdollLabParams.json");

        readonly Dictionary<RagdollPawn, (Vector3 position, Vector3 forward)> spawnOf =
            new Dictionary<RagdollPawn, (Vector3, Vector3)>();
        Vector3 savedGravity;
        float savedFixedDelta;
        bool swallowMouse;

        void Awake()
        {
            savedGravity = Physics.gravity;
            savedFixedDelta = Time.fixedDeltaTime;
            AutoTest = LabAutoTest.Requested;
        }

        void OnDestroy()
        {
            Physics.gravity = savedGravity;
            Time.fixedDeltaTime = savedFixedDelta;
            Time.timeScale = 1f;
            // The lab locks the mouse for look controls; the lobby HUD needs it back.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Start()
        {
            ApplyTime();
            ApplyGravity();
            if (AutoTest) return;
            if (players.Length > 1 && PadIndex(players[1].device) >= 0 && !XInputPad.Get(PadIndex(players[1].device)).connected)
                players[1].device = LabDevice.KeyboardArrows;
            foreach (var slot in players)
                slot.pawn = Spawn(slot.spawn, Vector3.forward, slot.material, slot.name);
            AddDummy();
        }

        public RagdollPawn Spawn(Vector3 groundPosition, Vector3 forward, Material material, string displayName)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            var pawn = Instantiate(pawnPrefab, groundPosition + Vector3.up * 0.02f, Quaternion.LookRotation(forward, Vector3.up));
            pawn.name = displayName;
            pawn.DisplayName = displayName;
            pawn.tuning = tuning;
            if (material != null && pawn.skin != null) pawn.skin.sharedMaterial = material;
            ApplySolver(pawn);
            spawnOf[pawn] = (groundPosition, forward);
            return pawn;
        }

        void ApplySolver(RagdollPawn pawn)
        {
            foreach (var rb in pawn.bodies)
            {
                rb.solverIterations = solverIterations;
                rb.solverVelocityIterations = Mathf.Max(4, solverIterations / 4);
            }
        }

        public void SetSolverIterations(int iterations)
        {
            solverIterations = iterations;
            foreach (var pawn in RagdollPawn.All) ApplySolver(pawn);
        }

        public void Respawn(RagdollPawn pawn)
        {
            if (pawn == null) return;
            var spot = spawnOf.TryGetValue(pawn, out var s) ? s : (LabLayout.SpawnP1, Vector3.forward);
            pawn.Teleport(spot.Item1 + Vector3.up * (pawn.standHeight + 0.02f), spot.Item2);
        }

        public void RespawnAll()
        {
            foreach (var pawn in RagdollPawn.All) Respawn(pawn);
        }

        public void AddDummy()
        {
            var spots = LabLayout.DummySpawns;
            if (dummies.Count >= spots.Length) return;
            dummies.Add(Spawn(spots[dummies.Count], Vector3.back, dummyMaterial, "더미 " + (dummies.Count + 1)));
        }

        public void RemoveDummy()
        {
            if (dummies.Count == 0) return;
            var dummy = dummies[dummies.Count - 1];
            dummies.RemoveAt(dummies.Count - 1);
            spawnOf.Remove(dummy);
            Destroy(dummy.gameObject);
        }

        void Update()
        {
            XInputPad.Poll();
            ApplyGravity();
            if (AutoTest) return;
            HandleHotkeys();
            foreach (var slot in players)
                if (slot.pawn != null) slot.pawn.SetInput(ReadInput(slot));
            swallowMouse = false;
            foreach (var pawn in RagdollPawn.All)
                if (pawn.Hips.position.y < LabLayout.KillHeight) Respawn(pawn);
        }

        void HandleHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.F1)) PanelOpen = !PanelOpen;
            for (int i = 0; i < XInputPad.MaxPads; i++)
                if (XInputPad.Pressed(i, XInputPad.Start)) PanelOpen = !PanelOpen;
            if (Input.GetKeyDown(KeyCode.R)) RespawnAll();
            if (Input.GetKeyDown(KeyCode.T)) SetSlowMotion(!SlowMotion);
            if (Input.GetKeyDown(KeyCode.F)) labCamera.SetFreeMode(!labCamera.freeMode);
            if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;

            if (PanelOpen) Cursor.lockState = CursorLockMode.None;
            else if (Cursor.lockState != CursorLockMode.Locked && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
            {
                Cursor.lockState = CursorLockMode.Locked;
                swallowMouse = true;
            }
            Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
        }

        PawnInput ReadInput(Slot slot)
        {
            var input = new PawnInput();
            Vector2 mv = Vector2.zero;
            switch (slot.device)
            {
                case LabDevice.KeyboardMouse:
                    if (labCamera.freeMode) break;
                    mv.x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
                    mv.y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
                    input.jump = Input.GetKeyDown(KeyCode.Space);
                    if (Cursor.lockState == CursorLockMode.Locked && !PanelOpen && !swallowMouse)
                    {
                        input.shove = Input.GetMouseButtonDown(0);
                        input.grab = Input.GetMouseButton(1);
                    }
                    break;
                case LabDevice.KeyboardArrows:
                    mv.x = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
                    mv.y = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
                    input.jump = Input.GetKeyDown(KeyCode.RightShift);
                    input.shove = Input.GetKeyDown(KeyCode.RightControl);
                    input.grab = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
                    break;
                default:
                    int index = PadIndex(slot.device);
                    var pad = XInputPad.Get(index);
                    if (!pad.connected) break;
                    mv = pad.left;
                    input.jump = XInputPad.Pressed(index, XInputPad.A);
                    input.shove = XInputPad.Pressed(index, XInputPad.RB);
                    input.grab = XInputPad.Held(index, XInputPad.LB);
                    if (XInputPad.Pressed(index, XInputPad.Back)) Respawn(slot.pawn);
                    if (!labCamera.freeMode) labCamera.AddYaw(pad.right.x * 140f * Time.unscaledDeltaTime);
                    break;
            }
            if (mv.sqrMagnitude > 1f) mv.Normalize();
            input.move = labCamera.FlatRight * mv.x + labCamera.FlatForward * mv.y;
            return input;
        }

        public static int PadIndex(LabDevice device) => device >= LabDevice.Pad1 ? device - LabDevice.Pad1 : -1;

        public static string DeviceName(LabDevice device)
        {
            switch (device)
            {
                case LabDevice.KeyboardMouse: return "키보드+마우스";
                case LabDevice.KeyboardArrows: return "키보드 방향키";
                default: return "게임패드 " + (PadIndex(device) + 1);
            }
        }

        public void CycleDevice(int slotIndex)
        {
            var slot = players[slotIndex];
            slot.device = (LabDevice)(((int)slot.device + 1) % 6);
        }

        public void SetSlowMotion(bool on)
        {
            SlowMotion = on;
            ApplyTime();
        }

        public void ApplyTime()
        {
            Time.timeScale = SlowMotion ? slowMotionScale : 1f;
            Time.fixedDeltaTime = Time.timeScale / Mathf.Max(20, physicsRate);
        }

        void ApplyGravity() => Physics.gravity = new Vector3(0f, -9.81f * tuning.values.gravityScale, 0f);

        public void SaveParams()
        {
            try
            {
                File.WriteAllText(ParamsPath, tuning.ToJson());
                Status = "저장했어요: " + ParamsPath;
            }
            catch (Exception e)
            {
                Status = "저장 실패: " + e.Message;
            }
        }

        public void LoadParams()
        {
            if (!File.Exists(ParamsPath))
            {
                Status = "저장된 파일이 없어요: " + ParamsPath;
                return;
            }
            tuning.LoadJson(File.ReadAllText(ParamsPath));
            MarkTuningDirty();
            Status = "불러왔어요: " + ParamsPath;
        }

        public void CopyParams()
        {
            GUIUtility.systemCopyBuffer = tuning.ToJson();
            Status = "파라미터 JSON을 클립보드에 복사했어요";
        }

        public void ResetParams()
        {
            tuning.ResetToSpec();
            MarkTuningDirty();
            Status = "명세서 시작값으로 되돌렸어요";
        }

        public void MarkTuningDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(tuning);
#endif
        }
    }
}
