using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    public enum LabDevice { KeyboardMouse, KeyboardArrows, Pad1, Pad2, Pad3, Pad4 }

    /// <summary>
    /// Local two-player ragdoll test rig: spawns pawns, routes input, and owns the lab hotkeys
    /// (R respawn, T slow motion, F free camera, F2 split screen, Tab tuning panel). No networking
    /// by design. Each player has their own third-person camera; P2's appears on the right half of
    /// the screen the moment P2 touches their controls.
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
        public LabParamSlots slots;
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

        /// <summary>A networked match owns the pawns and the input routing; local play stands down.</summary>
        public bool NetworkControlled { get; set; }

        /// <summary>Set while the player types in a network field so the keys do not also drive a pawn.</summary>
        public bool SuppressInput { get; set; }

        /// <summary>Another on-screen panel needs the mouse; keep the cursor free so its buttons work.</summary>
        public bool UiWantsCursor { get; set; }

        /// <summary>Each local player has their own half of the screen and their own camera.</summary>
        public bool SplitScreen { get; private set; }
        public string Status { get; set; } = "";
        public string ParamsPath => Path.Combine(Application.persistentDataPath, "RagdollLabParams.json");

        readonly Dictionary<RagdollPawn, (Vector3 position, Vector3 forward)> spawnOf =
            new Dictionary<RagdollPawn, (Vector3, Vector3)>();
        Vector3 savedGravity;
        float savedFixedDelta;
        bool swallowMouse;
        LabCamera[] cameras = Array.Empty<LabCamera>();
        bool splitChosen;   // F2 was used, so stop switching split screen on by itself

        void Awake()
        {
            savedGravity = Physics.gravity;
            savedFixedDelta = Time.fixedDeltaTime;
            AutoTest = LabAutoTest.Requested;
            // Two people testing on one PC shouldn't freeze the build when the window loses focus.
            Application.runInBackground = true;
        }

        void OnDestroy()
        {
            Physics.gravity = savedGravity;
            Time.fixedDeltaTime = savedFixedDelta;
            Time.timeScale = 1f;
        }

        void Start()
        {
            ApplyTime();
            ApplyGravity();
            if (AutoTest) return;
            SetUpCameras();
            if (GetComponent<StaminaHud>() == null) gameObject.AddComponent<StaminaHud>().game = this;
            SpawnLocalPlayers();
        }

        /// <summary>
        /// One camera per player slot. The scene's camera is P1's; the others are copies of it made
        /// here, without an AudioListener (Unity wants exactly one).
        /// </summary>
        void SetUpCameras()
        {
            cameras = new LabCamera[players.Length];
            if (labCamera == null) return;
            labCamera.game = this;
            labCamera.playerIndex = 0;
            cameras[0] = labCamera;
            var source = labCamera.Cam;
            for (int i = 1; i < players.Length; i++)
            {
                var go = new GameObject(players[i].name + " Camera");
                var cam = go.AddComponent<Camera>();
                if (source != null)
                {
                    cam.CopyFrom(source);
                    cam.depth = source.depth + i;
                }
                var follow = go.AddComponent<LabCamera>();
                follow.game = this;
                follow.playerIndex = i;
                follow.mouseSensitivity = labCamera.mouseSensitivity;
                cam.enabled = false;
                cameras[i] = follow;
            }
            ApplyViewports();
        }

        /// <summary>The camera a slot looks through: its own when the screen is split, P1's otherwise.</summary>
        public LabCamera CameraFor(int slotIndex) =>
            SplitScreen && slotIndex >= 0 && slotIndex < cameras.Length && cameras[slotIndex] != null
                ? cameras[slotIndex] : labCamera;

        public void SetSplitScreen(bool on)
        {
            splitChosen = true;
            SplitScreen = on;
            ApplyViewports();
        }

        void ApplyViewports()
        {
            // Online there is one player per PC; the extra local cameras stand down.
            bool split = SplitScreen && !NetworkControlled && cameras.Length > 1;
            int views = split ? cameras.Length : 1;
            for (int i = 0; i < cameras.Length; i++)
            {
                var c = cameras[i] != null ? cameras[i].Cam : null;
                if (c == null) continue;
                bool on = i == 0 || split;
                if (i > 0) c.enabled = on;
                if (on) c.rect = new Rect((float)i / views, 0f, 1f / views, 1f);
            }
        }

        /// <summary>Pawns to draw a stamina gauge for, and the camera each one is seen through.</summary>
        public IEnumerable<(RagdollPawn pawn, LabCamera camera)> HudTargets()
        {
            if (labCamera == null) yield break;
            if (NetworkControlled)
            {
                if (labCamera.soloTarget != null) yield return (labCamera.soloTarget, labCamera);
                yield break;
            }
            for (int i = 0; i < players.Length; i++)
                if (players[i].pawn != null) yield return (players[i].pawn, CameraFor(i));
        }

        public void SpawnLocalPlayers()
        {
            if (players.Length > 1 && PadIndex(players[1].device) >= 0 && !XInputPad.Get(PadIndex(players[1].device)).connected)
                players[1].device = LabDevice.KeyboardArrows;
            foreach (var slot in players)
                if (slot.pawn == null) slot.pawn = Spawn(slot.spawn, Vector3.forward, slot.material, slot.name);
            if (dummies.Count == 0) AddDummy();
        }

        /// <summary>Clear the arena before a networked match takes over (and after it ends).</summary>
        public void DespawnAll()
        {
            foreach (var pawn in RagdollPawn.All.ToArray())
            {
                spawnOf.Remove(pawn);
                Destroy(pawn.gameObject);
            }
            dummies.Clear();
            foreach (var slot in players) slot.pawn = null;
        }

        public Material TeamMaterial(int team) =>
            team == 0 ? players[0].material : players.Length > 1 ? players[1].material : players[0].material;

        /// <summary>Input for one local player slot, so a network link can read it without duplicating key maps.</summary>
        public PawnInput ReadPlayerInput(int index) =>
            index >= 0 && index < players.Length ? ReadInput(index) : default;

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
            ApplyViewports();
            if (!NetworkControlled)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    var slot = players[i];
                    if (slot.pawn == null) continue;
                    var input = ReadInput(i);
                    // P2 joins the moment they touch their controls: split the screen so they get
                    // their own camera. F2 turns it off (or on) for good.
                    if (i > 0 && !SplitScreen && !splitChosen && Touched(input))
                    {
                        SplitScreen = true;
                        ApplyViewports();
                        input = ReadInput(i);   // re-read against the camera it will actually use
                    }
                    slot.pawn.SetInput(input);
                }
                foreach (var pawn in RagdollPawn.All)
                    if (pawn.Hips.position.y < LabLayout.KillHeight) Respawn(pawn);
            }
            swallowMouse = false;
        }

        void HandleHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.F1)) PanelOpen = !PanelOpen;
            for (int i = 0; i < XInputPad.MaxPads; i++)
                if (XInputPad.Pressed(i, XInputPad.Start)) PanelOpen = !PanelOpen;
            if (Input.GetKeyDown(KeyCode.R) && !NetworkControlled) RespawnAll();
            if (Input.GetKeyDown(KeyCode.T) && !NetworkControlled) SetSlowMotion(!SlowMotion);
            if (Input.GetKeyDown(KeyCode.F)) labCamera.SetFreeMode(!labCamera.freeMode);
            if (Input.GetKeyDown(KeyCode.F2) && !NetworkControlled) SetSplitScreen(!SplitScreen);
            if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;

            if (slots != null)
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                for (int i = 0; i < LabParamSlots.Count; i++)
                {
                    if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;
                    if (shift) slots.Save(i);
                    else slots.Load(i);
                }
                if (Input.GetKeyDown(KeyCode.B)) slots.Randomize();
                if (Input.GetKeyDown(KeyCode.LeftBracket)) slots.Vote(false);
                if (Input.GetKeyDown(KeyCode.RightBracket)) slots.Vote(true);
            }

            if (PanelOpen || UiWantsCursor) Cursor.lockState = CursorLockMode.None;
            else if (Cursor.lockState != CursorLockMode.Locked && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
            {
                Cursor.lockState = CursorLockMode.Locked;
                swallowMouse = true;
            }
            Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
        }

        static bool Touched(PawnInput input) =>
            input.move.sqrMagnitude > 0.04f || input.jump || input.shove || input.grab || input.sprint;

        PawnInput ReadInput(int slotIndex)
        {
            var slot = players[slotIndex];
            var cam = CameraFor(slotIndex);
            var input = new PawnInput();
            Vector2 mv = Vector2.zero;
            if (SuppressInput) return input;
            switch (slot.device)
            {
                case LabDevice.KeyboardMouse:
                    if (labCamera.freeMode) break;
                    mv.x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
                    mv.y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
                    input.jump = Input.GetKeyDown(KeyCode.Space);
                    input.sprint = Input.GetKey(KeyCode.LeftShift);
                    // The clicks only count once the cursor is locked to the game (click the view once;
                    // Esc frees it again). Otherwise the click that locks it would also dive.
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
                    input.sprint = Input.GetKey(KeyCode.Slash);
                    break;
                default:
                    int index = PadIndex(slot.device);
                    var pad = XInputPad.Get(index);
                    if (!pad.connected) break;
                    mv = pad.left;
                    input.jump = XInputPad.Pressed(index, XInputPad.A);
                    input.shove = XInputPad.Pressed(index, XInputPad.RB) || XInputPad.Pressed(index, XInputPad.B);
                    input.grab = XInputPad.Held(index, XInputPad.LB);
                    input.sprint = pad.leftTrigger > 0.35f || XInputPad.Held(index, XInputPad.LeftThumb);
                    if (XInputPad.Pressed(index, XInputPad.Back)) Respawn(slot.pawn);
                    if (!cam.freeMode)
                    {
                        cam.AddYaw(pad.right.x * 150f * Time.unscaledDeltaTime);
                        cam.AddPitch(-pad.right.y * 90f * Time.unscaledDeltaTime);
                    }
                    break;
            }
            if (mv.sqrMagnitude > 1f) mv.Normalize();
            input.move = cam.FlatRight * mv.x + cam.FlatForward * mv.y;
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

        /// <summary>
        /// The measured "walks on its feet" set. Slower step cadence with a much wider leg swing is
        /// what does most of the work: at the spec defaults the legs only swing 16 degrees at full
        /// speed, which is why the pawn reads as a sliding ball. The rest adds weight to the controls.
        /// </summary>
        /// Written with single quotes and swapped for real ones on use, to keep it readable.
        public const string WeightPresetJson =
            "{'lowerBodySpring':2600," +
            "'moveSpeed':6.0,'acceleration':10.0,'stopDeceleration':14.0," +
            "'turnResponsiveness':8.0,'turnRateTopSpeed':260.0,'jumpImpulse':3.0," +
            "'balanceDamper':120.0,'yawStrength':600.0," +
            "'strideLength':1.5,'legSwing':60.0,'armSwing':55.0,'runLean':8.0,'runLift':0.045," +
            "'stepLock':0.3,'stanceThrust':0.45,'stepBob':0.05,'stepRoll':8.0," +
            "'turnLean':14.0,'landingDip':0.06,'stepLength':0.22," +
            "'boundGait':1.0,'driveFeedForward':1.0,'hopCadence':2.6," +
            "'knockdownImpulseThreshold':4.5,'hitImpactThreshold':2.2," +
            "'getUpDelay':0.7,'getUpBlendTime':0.22,'hitRecoveryTime':0.5,'momentumRetention':1.0}";

        /// <summary>
        /// The party-game set: legs alternate, but big and slow rather than honest. The cadence is
        /// locked low (1.6 cycles a second) while the speed is 12 m/s, so each stride is huge and the
        /// feet slide - which is exactly what Gang Beasts and Party Animals do. Amplitude is what
        /// reads at speed; foot contact is not. 140 deg is commanded and 95 comes out, because the
        /// hip joint's own limit (-75..60) is the ceiling - asking for more only adds limp.
        ///
        /// The cadence is 3.2 cycles a second - cartoon-fast legs, twice what amplitude alone wanted.
        /// Tripling a swing's frequency needs nine times the torque, so this only holds because the
        /// legs get their own damping ratio (0.04 = 4 Hz of tracking against the shared 1.59). Past
        /// 3.2 the top speed falls to 9.5 m/s and the legs push hard enough downhill that running
        /// beats rolling, which breaks the spec's rule 3. Lighter legs were tried and did not help:
        /// the bottleneck is drive bandwidth, not inertia.
        ///
        /// Speed was then cut to 80% (12 -> 9.6 m/s) on playtest feedback. That approved run is now the
        /// SPRINT (held Shift, spends stamina: sprintSpeed and the sprint* gait numbers). The everyday
        /// run at 5.5 m/s is its own gait, not the sprint slowed down. It runs rather than walks: 2.9
        /// steps a second with the legs at the hip's full 60 degrees, a light bounce (the hips follow
        /// the legs down only 30% of the way, so there is a short float at each stride), 8 degrees of
        /// forward lean plus a nod on each footfall (runDrive), arms pumping at the sides, a little
        /// kick-out of the back foot. The sprint is the approved cartoon one rebuilt inside the hip
        /// joint: legs to the 60 degree stop and no further (the old 140 rammed it), the back foot
        /// kicked out beside the skirt on purpose (sprintSplay), a deep forward lean (hips 12 +
        /// chest 16 + a 7 degree drive on each footfall), 3.5 steps a second, big bounding hops.
        /// Both lean into their acceleration (accelLean) instead of rolling with the facing.
        /// Baked into Settings/RagdollTuning.asset, so the lab and the prefab start with it;
        /// "명세 시작값" still resets to the spec for A/B, and this button brings the set back.
        /// </summary>
        public const string StepPresetJson =
            "{'lowerBodySpring':2600,'upperBodySpring':1600," +
            "'moveSpeed':5.5,'sprintSpeed':9.6,'acceleration':20.0,'stopDeceleration':20.0," +
            "'turnResponsiveness':8.0,'turnRateTopSpeed':260.0,'jumpImpulse':4.5," +
            "'balanceDamper':120.0,'yawStrength':600.0,'overspeedClamp':1.1," +
            "'strideLength':1.5,'legSwing':60.0,'armSwing':55.0,'runLean':8.0,'runLift':0.0," +
            "'runArmDown':35.0,'runTwist':8.0,'runLegDrop':0.3,'runDrive':4.0,'runSplay':10.0," +
            "'sprintCadence':3.5,'sprintLegSwing':60.0,'sprintArmSwing':76.0,'sprintLean':12.0," +
            "'sprintLift':0.05,'sprintBob':0.07,'sprintRoll':6.0,'sprintTurnLean':15.0," +
            "'sprintChestLean':16.0,'sprintDrive':7.0,'sprintSplay':22.0," +
            "'stepLock':0.0,'stanceThrust':0.45,'stepBob':0.0,'stepRoll':3.0," +
            "'turnLean':10.0,'accelLean':0.6,'anchorBrakeLeash':0.3,'landingDip':0.06,'stepLength':0.22," +
            "'boundGait':0.0,'driveFeedForward':1.0,'hopCadence':2.9,'legDamperRatio':0.04," +
            "'knockdownImpulseThreshold':6.0,'hitImpactThreshold':2.2," +
            "'getUpDelay':0.7,'getUpBlendTime':0.22,'hitRecoveryTime':0.5,'momentumRetention':1.0}";

        public void ApplyStepPreset()
        {
            tuning.LoadJson(StepPresetJson.Replace('\'', '"'));
            MarkTuningDirty();
            Status = "기본 프리셋: 달리기 5.5 m/s · Shift 전력질주 9.6 m/s. 명세 시작값과 비교하려면 각각 Shift+1·2로 저장하고 1·2 키로 전환하세요";
        }

        public void ApplyWeightPreset()
        {
            tuning.LoadJson(WeightPresetJson.Replace('\'', '"'));
            MarkTuningDirty();
            Status = "무게감 프리셋을 적용했어요 (Shift+2로 B 슬롯에 저장해 A와 비교해보세요)";
        }

        public void PasteParams()
        {
            string json = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(json) || !json.TrimStart().StartsWith("{"))
            {
                Status = "클립보드에 파라미터 JSON이 없어요";
                return;
            }
            try
            {
                tuning.LoadJson(json);
                MarkTuningDirty();
                Status = "클립보드의 값을 적용했어요";
            }
            catch (Exception e)
            {
                Status = "붙여넣기 실패: " + e.Message;
            }
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
