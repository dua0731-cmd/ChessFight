using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChessFight.RagdollLab
{
    // Dedicated offline mechanics scene. Existing KingRush/SwordFight scenes stay intact.
    [DefaultExecutionOrder(-90)]
    public sealed class KingRushPrototype : MonoBehaviour
    {
        public RagdollPawn pawnPrefab;
        public RagdollTuning tuning;
        public KingRushMatch Match { get; private set; }
        public LabCamera CameraRig { get; private set; }
        public KingRushPawn Local => Players[Selected];
        public int Selected { get; private set; }
        public readonly List<KingRushPawn> Players = new List<KingRushPawn>();
        public readonly List<KingRushPad> Pads = new List<KingRushPad>();
        public readonly List<PawnGate> Gates = new List<PawnGate>();
        public AbilityZone Blue { get; private set; }
        public AbilityZone CannonZone { get; private set; }
        public bool Automated { get; private set; }
        public bool ManualSteps { get; set; }
        public WaterZone Water { get; private set; }
        readonly List<Material> materials = new List<Material>();
        readonly Dictionary<KingRushPawn, double> wetUntil = new Dictionary<KingRushPawn, double>();
        readonly List<KingRushPawn> ready = new List<KingRushPawn>();
        readonly List<TextMesh> padLabels = new List<TextMesh>();
        readonly List<TextMesh> doorLabels = new List<TextMesh>();
        readonly List<Renderer> doorViews = new List<Renderer>();
        Material white, black;
        KingRushHud hud;
        Vector3 savedGravity;
        CursorLockMode savedLock;
        bool savedCursor, savedBackground, menu;

        void Awake()
        {
            Automated = Array.IndexOf(Environment.GetCommandLineArgs(), "-kingRushTest") >= 0;
            savedBackground = Application.runInBackground; Application.runInBackground = true;
            savedGravity = Physics.gravity; Physics.gravity = Vector3.down * 9.81f * tuning.values.gravityScale;
            savedLock = Cursor.lockState; savedCursor = Cursor.visible;
            Match = gameObject.AddComponent<KingRushMatch>(); CameraRig = FindFirstObjectByType<LabCamera>();
            BuildCourse(); WaterZone.Entered += EnterWater;
        }
        void Start()
        {
            for (int team = 0; team < 2; team++)
                for (int slot = 0; slot < 6; slot++) Add(team, slot, slot == 5);
            Select(0);
            hud = gameObject.AddComponent<KingRushHud>();
            if (Automated) gameObject.AddComponent<KingRushAutoTest>();
            RefreshCursor();
        }
        public static Vector3 Spawn(int team, int slot) => new Vector3((team == 0 ? -1 : 1) * (2 + slot % 3 * 1.7f), 0, -5 - slot / 3 * 2);
        void Add(int team, int slot, bool king)
        {
            var pawn = Instantiate(pawnPrefab, Spawn(team, slot) + Vector3.up * .02f, Quaternion.identity);
            pawn.tuning = tuning; pawn.Team = team; pawn.DisplayName = (team == 0 ? "백팀" : "흑팀") + " " + (slot + 1);
            pawn.skin.sharedMaterial = team == 0 ? white : black;
            foreach (var body in pawn.bodies) { body.solverIterations = 24; body.solverVelocityIterations = 6; }
            var member = pawn.gameObject.AddComponent<KingRushPawn>();
            member.Initialize(Match, (ulong)(team * 6 + slot + 1), king); Players.Add(member);
        }
        public void Select(int index)
        {
            if (Players.Count == 0) return;
            Players[Selected].SetInput(default); Selected = index % Players.Count;
            CameraRig.soloTarget = Local.Pawn;
        }
        void Update()
        {
            ProcessRespawns();
            if (Automated) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { menu = !menu; RefreshCursor(); }
            if (menu) { Local.SetInput(default); if (Input.GetKeyDown(KeyCode.Backspace)) SceneManager.LoadScene("Lobby"); return; }
            if (Input.GetKeyDown(KeyCode.Tab)) Select(Selected + 1);
            if (Input.GetKeyDown(KeyCode.F2)) Select(5);
            if (Input.GetKeyDown(KeyCode.F3)) ResetRound();
            if (Input.GetKeyDown(KeyCode.F4)) ArrangeAbilityTest();
            if (Input.GetKeyDown(KeyCode.F5)) ArrangeCannonTest(Input.GetKey(KeyCode.LeftShift));
            if (Input.GetKeyDown(KeyCode.F7)) Match.Complete(0, 0);
            if (Input.GetKeyDown(KeyCode.F8)) Match.Complete(0, 1);
            if (Input.GetKeyDown(KeyCode.R)) Respawn(Local);
            if (!Application.isFocused) { Local.SetInput(default); return; }
            float x = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            float y = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            Local.SetInput(new PawnInput { move = Vector3.ClampMagnitude(CameraRig.FlatRight * x + CameraRig.FlatForward * y, 1),
                aim = CameraRig.AimForward, jump = Input.GetKeyDown(KeyCode.Space), sprint = Input.GetKey(KeyCode.LeftShift),
                shove = Input.GetMouseButtonDown(0), shoveHeld = Input.GetMouseButton(0), grab = Input.GetMouseButton(1),
                ability = Input.GetKeyDown(KeyCode.E), interact = Input.GetKey(KeyCode.F) }, Input.GetKey(KeyCode.E));
        }
        void FixedUpdate() { if (!ManualSteps) Step(Time.fixedDeltaTime); }
        public void Step(float dt)
        {
            Match.Advance(dt);
            foreach (var p in Players)
            {
                bool inside = (Blue.Contains(p.BodyPosition) || CannonZone.Contains(p.BodyPosition)) && p.Section <= KingRushSection.Blue1 && !p.Pawn.Floating;
                if (inside) p.Section = Blue.section;
                p.AbilitiesEnabled = inside;
            }
            foreach (var pad in Pads) pad.Step(dt);
            foreach (var gate in Gates) gate.Step();
            foreach (var p in Players) p.Step(dt);
        }
        void EnterWater(ICharacterDriver driver, WaterZone water)
        {
            if (water != Water) return;
            foreach (var p in Players)
                if (ReferenceEquals(p.HitReceiver, driver) && !wetUntil.ContainsKey(p))
                { wetUntil[p] = Match.Now + water.RespawnDelay; p.CancelAbility(); break; }
        }
        void ProcessRespawns()
        {
            ready.Clear();
            foreach (var entry in wetUntil) if (Match.Now >= entry.Value) ready.Add(entry.Key);
            foreach (var p in ready) Respawn(p);
            foreach (var p in Players) if (p.BodyPosition.y < -15 || !p.Pawn.IsFinite()) Respawn(p);
        }
        public void Respawn(KingRushPawn p)
        {
            wetUntil.Remove(p);
            int slot = ((int)p.Id - 1) % 6;
            Vector3 point = p.Section == KingRushSection.Red1 ? Spawn(p.Team, slot) :
                new Vector3((p.Team == 0 ? -1 : 1) * (2 + slot % 3 * 1.5f), 0,
                    p.Section == KingRushSection.Blue1 ? 5 + slot / 3 * 1.5f : 22 + slot / 3 * 1.5f);
            p.Respawn(point);
        }
        public void ResetRound()
        {
            Match.ResetRound(); wetUntil.Clear();
            foreach (var pad in Pads) pad.Charge.Reset();
            foreach (var p in Players) { p.ResetForRound(); Respawn(p); }
            foreach (var gate in Gates) gate.Step();
        }
        public void ArrangeAbilityTest()
        {
            ResetRound(); Select(5);
            // A testing shortcut, not real king selection or mission progression.
            Local.Respawn(new Vector3(0, 0, 10));
            Players[0].Respawn(new Vector3(-2, 0, 10));
            Players[1].Respawn(new Vector3(-3, 0, 12));
            Players[6].Respawn(new Vector3(2, 0, 10));
            Players[7].Respawn(new Vector3(3, 0, 12));
            Players[8].Respawn(new Vector3(5, 0, 10));
        }
        void RefreshCursor()
        { Cursor.lockState = menu || Automated ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = menu || Automated; }
        public void ArrangeCannonTest(bool ally = false)
        {
            ResetRound(); Select(0); Local.Promote(KingRushPiece.Rook);
            Local.Respawn(new Vector3(22, 0, 8));
            var target = Players[ally ? 1 : 6]; target.Respawn(new Vector3(22, 0, 8.7f));
            target.Pawn.Teleport(target.BodyPosition, Vector3.back);
            CameraRig.yaw = 0; CameraRig.pitch = 8;
        }
        void LateUpdate()
        {
            if (Players.Count == 0) return;
            for (int i = 0; i < Pads.Count; i++)
            {
                var pad = Pads[i];
                padLabels[i].text = KingRushPieces.Name(pad.Reward) + "\n" + (pad.Claimed ? "사용 완료" : pad.Charge.Contested ? "쟁탈 중 · 멈춤" : $"{pad.Charge.Seconds / 1.5:0%} · 1.5초");
            }
            for (int i = 0; i < Gates.Count; i++)
            {
                doorViews[i].enabled = !(Gates[i].Open && Gates[i].team == Local.Team);
                doorLabels[i].text = (i == 0 ? "백팀 출구" : "흑팀 출구") + "\n" + DoorText(i) + " · 해당 팀만 통과";
            }
            if (hud != null) hud.Draw(KingRushPieces.Name(Local.Piece), Local.Team, Local.Section.ToString(),
                Local.AbilitiesEnabled, Local.AbilityActive, Local.CooldownSeconds, Local.Piece == KingRushPiece.King || Local.Piece == KingRushPiece.Rook,
                DoorText(0), DoorText(1), menu,
                CountBlueBodies(0), CountBlueBodies(1));
        }
        public int CountBlueBodies(int team)
        {
            int count = 0;
            foreach (var member in Match.Characters)
                if (member.Team == team && member.CountsAsBody &&
                    (Blue.Contains(member.BodyPosition) || CannonZone.Contains(member.BodyPosition))) count++;
            return count;
        }
        string DoorText(int team)
        {
            double left = Match.Rules.OpensAt(0, team) - Match.Now;
            return double.IsInfinity(left) ? "미완료" : left <= 0 ? "열림" : $"{left:0.0}초 뒤";
        }
        Material Mat(Color color)
        { var m = new Material(Shader.Find("Standard")) { color = color }; materials.Add(m); return m; }
        GameObject Box(string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(transform);
            go.transform.position = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        void BuildCourse()
        {
            white = Mat(new Color(.94f, .9f, .8f)); black = Mat(new Color(.15f, .19f, .25f));
            var red = Mat(new Color(.45f, .2f, .19f)); var blue = Mat(new Color(.12f, .31f, .43f));
            var gold = Mat(new Color(.94f, .66f, .16f)); var wall = Mat(new Color(.23f, .26f, .32f));
            Box("RED 1 · approach", new Vector3(0, -.3f, -3), new Vector3(20, .6f, 14), red);
            Box("BLUE 1 · ability area", new Vector3(0, -.3f, 11), new Vector3(20, .6f, 14), blue);
            Box("RED 2 · exit test", new Vector3(0, -.3f, 22), new Vector3(20, .6f, 8), red);
            for (int i = 0; i < 2; i++)
            {
                Vector3 pos = new Vector3(i == 0 ? -3 : 3, 0, 1);
                Box("Promotion " + i, pos + Vector3.up * .025f, new Vector3(2, .05f, 2), gold);
                var root = new GameObject("Pad rule " + i); root.transform.SetParent(transform); root.transform.position = pos + Vector3.up * .05f;
                var pad = root.AddComponent<KingRushPad>(); pad.index = i; pad.match = Match; Pads.Add(pad);
                padLabels.Add(Label(transform, "Pad label", pos + Vector3.up * 2.1f, "", .04f));
            }
            Blue = new GameObject("Blue ability zone").AddComponent<AbilityZone>(); Blue.transform.SetParent(transform);
            Blue.transform.position = new Vector3(0, 3, 11); Blue.size = new Vector3(20, 8, 14);
            Box("Cannon distance test lane", new Vector3(22, -.3f, 20), new Vector3(10, .6f, 34), blue);
            CannonZone = new GameObject("Cannon blue zone").AddComponent<AbilityZone>(); CannonZone.transform.SetParent(transform);
            CannonZone.transform.position = new Vector3(22, 3, 20); CannonZone.size = new Vector3(10, 8, 34);
            foreach (int distance in new[] {5, 10, 20})
            {
                Box("Cannon range " + distance, new Vector3(22, .005f, 8.7f + distance), new Vector3(8, .01f, .08f), gold);
                Label(transform, "Range", new Vector3(25.5f, .5f, 8.7f + distance), distance + " m", .02f);
            }
            Box("Left boundary", new Vector3(-10, 3, 11), new Vector3(.5f, 6, 14), wall);
            Box("Right boundary", new Vector3(10, 3, 11), new Vector3(.5f, 6, 14), wall);
            Box("Exit wall left", new Vector3(-8, 3, 18), new Vector3(4, 6, .5f), wall);
            Box("Exit wall middle", new Vector3(0, 3, 18), new Vector3(4, 6, .5f), wall);
            Box("Exit wall right", new Vector3(8, 3, 18), new Vector3(4, 6, .5f), wall);
            for (int team = 0; team < 2; team++)
            {
                var door = Box(team == 0 ? "White gate" : "Black gate", new Vector3(team == 0 ? -4 : 4, 3, 18), new Vector3(4, 6, .5f), team == 0 ? white : black);
                var gate = door.AddComponent<PawnGate>(); gate.match = Match; gate.team = team; gate.barrier = door.GetComponent<Collider>();
                gate.exitBounds = new Bounds(new Vector3(team == 0 ? -4 : 4, 2, 19), new Vector3(4, 5, 1)); Gates.Add(gate);
                doorViews.Add(door.GetComponent<Renderer>());
                var doorLabel = Label(transform, "Door label", new Vector3(team == 0 ? -4 : 4, 3, 17.65f), "", .04f);
                doorLabel.color = team == 0 ? new Color(.15f, .18f, .24f) : Color.white; doorLabels.Add(doorLabel);
            }
            var sea = Box("Water · five second respawn", new Vector3(0, -4, 7), new Vector3(100, 4, 100), Mat(new Color(.12f, .45f, .6f)));
            Water = sea.AddComponent<WaterZone>();
            Label(transform, "Course title", new Vector3(0, 3.8f, 4), "KING RUSH\n능력 시험장 · 미션 / 최종 맵 아님", .045f);
        }
        public static TextMesh Label(Transform parent, string name, Vector3 position, string text, float scale)
        {
            var label = new GameObject(name).AddComponent<TextMesh>(); label.transform.SetParent(parent);
            label.transform.position = position; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.font = RuntimePanels.KoreanFont; label.fontSize = 64; label.characterSize = scale;
            label.GetComponent<Renderer>().sharedMaterial = label.font.material; label.text = text; return label;
        }
        void OnDestroy()
        {
            WaterZone.Entered -= EnterWater; Physics.gravity = savedGravity;
            Cursor.lockState = savedLock; Cursor.visible = savedCursor;
            Application.runInBackground = savedBackground;
            foreach (var p in Players) if (p != null) Destroy(p.gameObject);
            foreach (var material in materials) if (material != null) Destroy(material);
        }
    }
}
