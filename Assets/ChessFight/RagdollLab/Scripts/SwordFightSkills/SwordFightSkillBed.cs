using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Sword Fight skill test bed (scene SwordFight_SkillTest, a copy of the Sword Fight match scene, R91): the match
    /// runs as it is (swords, the 14 m platform, ring-out points, respawns, dummies) and this adds the piece skills. Every
    /// fighter gets a <see cref="SwordFightSkills"/>; as in the other skill test scenes F starts the aim, the left click
    /// uses the skill and the right click (or F again) calls it off; the king's F is his guard. Keys (temporary): 1 킹 · 2 퀸 · 3 룩 · 4 비숍 · 5 나이트 · 6 폰
    /// (or Z / X), F7 puts the dummies where the chosen skill is best tried, F8 makes the nearest enemy come up beside you
    /// and cut at you, anywhere on the platform (the king's guard waits for that cut, R102), F9 sends the dummies home.
    /// The left panel shows the same (Esc frees the mouse for its buttons).
    /// The edge skills (R103) are on E (the settings' skill key): Shift+F7 puts the pieces where the chosen piece's edge
    /// skill is tried, Shift+F8 does what that test needs done to it (an enemy pushes the king off the edge, or the
    /// bishop's ally; the queen's enemy steps in out of the edge zone); the badge at the bottom lights when it can be used.
    /// The effects are design A "잉크 테두리 장난감 체스" (<see cref="SwordFightSkillFx"/>).
    /// </summary>
    [DefaultExecutionOrder(-80)]   // after the match's step (-90), before the skills (-75) and the sword (-60)
    public class SwordFightSkillBed : MonoBehaviour
    {
        public SwordFightSkillParams skills = new SwordFightSkillParams();
        [Tooltip("가장자리 스킬 (E, R103)")]
        public SwordFightEdgeParams edge = new SwordFightEdgeParams();

        [Header("키 (임시, 다른 스킬 시험 씬과 같음: F 조준 → 좌클릭 발동 · 우클릭 취소)")]
        public KeyCode skillKey = KeyCode.F;

        public static readonly PieceKind[] Pieces = { PieceKind.King, PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight, PieceKind.Pawn };
        /// <summary>The platform's half width (SwordFightBuilder: 14 m × 14 m).</summary>
        public const float Half = 7f;

        public SwordFightGame Game { get; private set; }
        public SwordFightPawn Local => Game != null ? Game.Local : null;
        public SwordFightSkillFx FxLayer { get; private set; }
        public SwordFightEdgeFloor Floor { get; private set; }
        /// <summary>The probe or the film drives the local fighter: the bed does not read the mouse.</summary>
        public static bool Scripted { get; set; }

        static readonly List<string> log = new List<string>();
        const int LogLines = 8;
        readonly Dictionary<SwordFightPawn, (Vector3 aim, float age)> cuts = new Dictionary<SwordFightPawn, (Vector3, float)>();
        GUIStyle text, small, header, button, selected, box;
        Texture2D panel, bar;
        bool showPanel = true;

        public static void Report(string line)
        {
            log.Add($"{Time.time:0.0}s  {line}");
            if (log.Count > LogLines) log.RemoveAt(0);
        }

        void Awake()
        {
            log.Clear();
            Game = GetComponent<SwordFightGame>();
            if (Game == null) Game = FindFirstObjectByType<SwordFightGame>();
            SwordFightSkills.Log += OnLog;
            FxLayer = GetComponent<SwordFightSkillFx>();
            if (FxLayer == null) FxLayer = gameObject.AddComponent<SwordFightSkillFx>();
            Floor = GetComponent<SwordFightEdgeFloor>();
            if (Floor == null) Floor = gameObject.AddComponent<SwordFightEdgeFloor>();
        }

        void OnDestroy() => SwordFightSkills.Log -= OnLog;
        static void OnLog(SwordFightSkills s, string line) => Report(line);

        void Start()
        {
            Floor.Find();
            Report("소드 파이트 스킬 시험 준비 완료 — 1~5로 기물, F 조준 → 좌클릭 발동 · 우클릭 취소 · E 가장자리 스킬");
        }

        // ---------------------------------------------------------------- every frame

        void Update()
        {
            if (Game == null) return;
            foreach (var f in Game.Fighters.Values)
            {
                if (f == null || f.Skills != null) continue;
                var s = f.gameObject.AddComponent<SwordFightSkills>();
                s.Init(f, skills, edge);
            }
            var me = Local;
            if (me == null || me.Skills == null) return;
            if (Game.CameraRig != null) me.Skills.SetAimCamera(Game.CameraRig.Cam);
            if (Scripted) return;

            bool free = !Game.MenuOpen && !Game.Finished && !ChatBox.KeysHeld && Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
            // F starts the aim (or is the king's guard); the left click uses it (it comes with the match's input); the right
            // click calls it off. E (the settings' skill key) is the edge skill, one press.
            if (free && Input.GetKeyDown(skillKey)) me.Skills.PressKey();
            if (free && GameSettings.Pressed(GameKey.Grab)) me.Skills.CancelAim();
            if (free && GameSettings.Pressed(GameKey.Skill)) me.Skills.PressEdge();
            if (ChatBox.KeysHeld) return;
            for (int i = 0; i < Pieces.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) SetPiece(me, Pieces[i]);
            if (Input.GetKeyDown(KeyCode.Z)) StepPiece(me, -1);
            if (Input.GetKeyDown(KeyCode.X)) StepPiece(me, 1);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.F7)) { if (shift) StageEdge(me.Pawn.Piece); else Stage(me.Pawn.Piece); }
            if (Input.GetKeyDown(KeyCode.F8)) { if (shift) EdgeHelp(); else EnemyCuts(); }
            if (Input.GetKeyDown(KeyCode.F9)) Home();
            if (Input.GetKeyDown(KeyCode.F10)) showPanel = !showPanel;
        }

        // ---------------------------------------------------------------- things done a moment from now

        readonly List<(float at, System.Action act)> later = new List<(float, System.Action)>();

        /// <summary>Do <paramref name="act"/> <paramref name="seconds"/> of game time from now (in the physics step).</summary>
        public void After(float seconds, System.Action act) => later.Add((Time.fixedTime + seconds, act));

        void RunLater()
        {
            for (int i = later.Count - 1; i >= 0; i--)
            {
                if (Time.fixedTime + 1e-4f < later[i].at) continue;
                var act = later[i].act;
                later.RemoveAt(i);
                act?.Invoke();
            }
        }

        /// <summary>A dummy walking <paramref name="dir"/> for <paramref name="seconds"/> (the match leaves its input alone).</summary>
        public void Walk(SwordFightPawn dummy, Vector3 dir, float seconds)
        {
            if (dummy == null || !dummy.Alive) return;
            dir.y = 0f;
            walks[dummy] = (dir.normalized, seconds);
            dummy.Scripted = true;
        }

        readonly Dictionary<SwordFightPawn, (Vector3 dir, float left)> walks = new Dictionary<SwordFightPawn, (Vector3, float)>();

        /// <summary>A dummy jumps (the queen's edge test: dodging her cut).</summary>
        public void Jump(SwordFightPawn dummy)
        {
            if (dummy == null || !dummy.Alive) return;
            dummy.Scripted = true;
            dummy.SetInput(new PawnInput { jump = true });
            After(0.6f, () => { if (dummy != null && !walks.ContainsKey(dummy) && !cuts.ContainsKey(dummy)) dummy.Scripted = false; });
        }

        void StepWalks()
        {
            var done = new List<SwordFightPawn>();
            foreach (var d in new List<SwordFightPawn>(walks.Keys))
            {
                var (dir, left) = walks[d];
                left -= Time.fixedDeltaTime;
                walks[d] = (dir, left);
                if (d == null || !d.Alive || left <= 0f) { done.Add(d); continue; }
                if (!cuts.ContainsKey(d)) d.SetInput(new PawnInput { move = dir, aim = dir });
            }
            foreach (var d in done)
            {
                walks.Remove(d);
                if (d != null && !cuts.ContainsKey(d)) { d.Scripted = false; d.SetInput(default); }
            }
        }

        void FixedUpdate()
        {
            RunLater();
            StepWalks();
            // Scripted cuts: the match has just emptied every dummy's input (−90); this runs before the sword (−60).
            var done = new List<SwordFightPawn>();
            var keys = new List<SwordFightPawn>(cuts.Keys);
            foreach (var d in keys)
            {
                var (aim, age) = cuts[d];
                age += Time.fixedDeltaTime;
                cuts[d] = (aim, age);
                if (d == null || !d.Alive) { done.Add(d); continue; }
                // One step to change to the click cut, one with the button up, then the click; hold the style through it.
                bool click = age > 2.5f * Time.fixedDeltaTime && age < 3.5f * Time.fixedDeltaTime;
                d.SetInput(new PawnInput { aim = aim, ability2 = true, shove = click, shoveHeld = click });
                if (age > SwordFightPawn.SwingDuration + 0.15f) done.Add(d);
            }
            foreach (var d in done)
            {
                cuts.Remove(d);
                if (d != null) { d.Scripted = false; d.SetInput(default); }
            }
        }

        /// <summary>Make <paramref name="dummy"/> cut once toward <paramref name="at"/> (the click cut: wind-up 0.16 s,
        /// cut 0.2 s).</summary>
        public void Cut(SwordFightPawn dummy, Vector3 at)
        {
            if (dummy == null || !dummy.Alive) return;
            Vector3 aim = at - dummy.Pawn.Hips.position;
            aim.y = 0f;
            dummy.Scripted = true;
            cuts[dummy] = (aim.normalized, 0f);
        }

        // ---------------------------------------------------------------- pieces

        void StepPiece(SwordFightPawn f, int step)
        {
            int i = System.Array.IndexOf(Pieces, f.Pawn.Piece);
            i = i < 0 ? 0 : (i + step + Pieces.Length) % Pieces.Length;
            SetPiece(f, Pieces[i]);
        }

        public void SetPiece(SwordFightPawn f, PieceKind kind)
        {
            f.Pawn.SetPiece(kind);
            f.Skills?.ResetSkill();
            Report($"{f.Pawn.DisplayName} → {ChessPieces.Name(kind)} ({SwordFightSkills.Name(kind)})");
        }

        // ---------------------------------------------------------------- dummies

        public List<SwordFightPawn> Dummies(int team)
        {
            var list = new List<SwordFightPawn>();
            foreach (var f in Game.Fighters.Values)
                if (f != null && f.Bot && f.Pawn.Team == team) list.Add(f);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }

        /// <summary>At least <paramref name="n"/> dummies on <paramref name="team"/> (the match scene starts with one ally
        /// and two enemies).</summary>
        public List<SwordFightPawn> Ensure(int team, int n)
        {
            var list = Dummies(team);
            ulong id = 10;
            while (list.Count < n)
            {
                while (Game.Fighters.ContainsKey(id)) id++;
                var f = Game.Add(id, team, list.Count + 1, true, team == 0 ? $"아군 더미 {list.Count + 1}" : $"적군 더미 {list.Count + 1}");
                list.Add(f);
            }
            return list;
        }

        public static void Place(SwordFightPawn f, Vector3 ground, Vector3 face)
        {
            if (f == null || !f.Alive) return;
            f.Pawn.Teleport(ground + Vector3.up * (f.Pawn.standHeight + 0.02f), face);
            f.SetInput(default);
            f.Skills?.ResetSkill();
        }

        /// <summary>Where each skill is best tried (the previz's scenes on the 14 m platform: its east edge is x = 7).
        /// Returns false if a dummy is still waiting to come back.</summary>
        public bool Stage(PieceKind kind)
        {
            var me = Local;
            if (me == null) return false;
            var enemies = Ensure(1, 3);
            var allies = Ensure(0, 1);
            foreach (var f in enemies) if (!f.Alive) { Report("부활 대기 중인 더미가 있어요 — 잠시 뒤 다시"); return false; }
            Vector3 east = Vector3.right, west = Vector3.left;
            Vector3 far = new Vector3(-5.5f, 0f, 5.5f);
            switch (kind)
            {
                case PieceKind.King:
                    Place(me, new Vector3(4.8f, 0f, 0f), west);
                    Place(enemies[0], new Vector3(5.6f, 0f, -0.75f), Dir(new Vector3(5.6f, 0f, -0.75f), new Vector3(4.8f, 0f, 0f)));
                    Place(enemies[1], new Vector3(3.7f, 0f, 0.5f), east);
                    Place(enemies[2], far + Vector3.right * 2f, east);
                    Place(allies[0], far, east);
                    Report("킹 시험: 가장자리를 등진 킹, 가장자리 쪽 적 1 · 안쪽 적 1 — F 후 F8 (적이 칼을 휘두름)");
                    break;
                case PieceKind.Queen:
                    Place(me, new Vector3(0.6f, 0f, 0f), east);
                    Place(enemies[0], new Vector3(3.0f, 0f, 0.05f), west);
                    Place(enemies[1], new Vector3(5.4f, 0f, -0.05f), west);
                    Place(enemies[2], new Vector3(3.8f, 0f, 2.2f), west);
                    Place(allies[0], far, east);
                    Report("퀸 시험: 동쪽으로 줄 선 적 2 (뒤는 가장자리) · 줄 밖 적 1");
                    break;
                case PieceKind.Rook:
                    Place(me, new Vector3(-3.4f, 0f, -2f), east);
                    Place(enemies[0], new Vector3(0.2f, 0f, -2.05f), west);
                    Place(enemies[1], new Vector3(5.8f, 0f, -1.9f), west);
                    Place(enemies[2], new Vector3(1.5f, 0f, 1.6f), west);
                    Place(allies[0], far, east);
                    Report("룩 시험: 동쪽 통로에 적 2 (3.6 m · 9.2 m) · 통로 밖 적 1");
                    break;
                case PieceKind.Bishop:
                    Place(me, new Vector3(-0.2f, 0f, 0.4f), east);
                    Place(enemies[0], new Vector3(5.9f, 0f, 0.4f), west);
                    Place(enemies[1], new Vector3(1.0f, 0f, -3.6f), east);
                    Place(enemies[2], new Vector3(-4f, 0f, 3.5f), east);
                    Place(allies[0], new Vector3(4.85f, 0f, 0.15f), east);
                    Report("비숍 시험: 가장자리 1 m 앞 적 · 그 옆 아군 — 묶은 뒤 F8은 아군이 와서 벰 (묶기는 맵 어디서든)");
                    break;
                case PieceKind.Knight:
                    Place(me, new Vector3(0.4f, 0f, 0f), east);
                    Place(enemies[0], new Vector3(6.3f, 0f, 1.4f), west);
                    Place(enemies[1], new Vector3(5.5f, 0f, -1.5f), west);
                    Place(enemies[2], new Vector3(-2f, 0f, 3.5f), east);
                    Place(allies[0], far, east);
                    Report("나이트 시험: 착지점 동쪽 5 m 앞 두 자리에 적 2");
                    break;
                default:
                    Home();
                    return true;
            }
            return true;
        }

        static Vector3 Dir(Vector3 from, Vector3 to) { var d = to - from; d.y = 0f; return d.normalized; }

        // ---------------------------------------------------------------- the edge skills' tests (R103)

        /// <summary>Shift+F7: where the chosen piece's edge skill is tried, by the east edge (x = 7). Returns false if a
        /// dummy is still waiting to come back.</summary>
        public bool StageEdge(PieceKind kind)
        {
            var me = Local;
            if (me == null) return false;
            var enemies = Ensure(1, 3);
            var allies = Ensure(0, 1);
            foreach (var f in enemies) if (!f.Alive) { Report("부활 대기 중인 더미가 있어요 — 잠시 뒤 다시"); return false; }
            foreach (var f in allies) if (!f.Alive) { Report("부활 대기 중인 더미가 있어요 — 잠시 뒤 다시"); return false; }
            Vector3 east = Vector3.right, west = Vector3.left;
            Vector3 far = new Vector3(-5.5f, 0f, 5.5f);
            Vector3 look = west;
            switch (kind)
            {
                case PieceKind.King:
                    Place(me, new Vector3(5.7f, 0f, 0f), west);
                    Place(enemies[0], new Vector3(4.9f, 0f, 0.05f), east);
                    Place(enemies[1], new Vector3(5.2f, 0f, 1.45f), east);
                    Place(enemies[2], new Vector3(-4f, 0f, -4f), east);
                    Place(allies[0], far, east);
                    Report("왕의 귀환 시험: 가장자리를 등진 킹 — Shift+F8 = 적이 밀어 떨어뜨림 → 떨어지는 0.6초 안에 E");
                    break;
                case PieceKind.Queen:
                    Place(me, new Vector3(1.6f, 0f, 0.4f), east);
                    Place(enemies[0], new Vector3(5.6f, 0f, 0f), west);
                    Place(enemies[1], new Vector3(2.8f, 0f, -3.2f), west);
                    Place(enemies[2], new Vector3(-5.8f, 0f, -5.8f), east);
                    Place(allies[0], far, east);
                    look = east;
                    Report("체크메이트 일섬 시험: 가장자리 1.4 m 앞 적 (4 m) — E · Shift+F8 = 그 적이 안쪽으로 걸어 들어옴 (금색 선 동안이면 취소)");
                    break;
                case PieceKind.Rook:
                    Place(me, new Vector3(5.9f, 0f, -0.4f), east);
                    Place(enemies[0], new Vector3(6.3f, 0f, 0.6f), west);
                    Place(enemies[1], new Vector3(5.5f, 0f, -1.5f), west);
                    Place(enemies[2], new Vector3(3.4f, 0f, 2.8f), west);
                    Place(allies[0], new Vector3(6.4f, 0f, -1.1f), west);
                    look = east;
                    Report("성벽 붕괴 시험: 동쪽 가장자리를 보고 E → 0.8초 안에 안쪽으로 걸어 나와야 룩도 안 떨어짐 (덩어리 위 적 2 · 아군 1)");
                    break;
                case PieceKind.Bishop:
                    Place(me, new Vector3(5.3f, 0f, 2.0f), east);
                    Place(allies[0], new Vector3(6.0f, 0f, -0.5f), west);
                    Place(enemies[0], new Vector3(5.15f, 0f, -0.55f), east);
                    Place(enemies[1], new Vector3(-3f, 0f, 4f), east);
                    Place(enemies[2], new Vector3(-4f, 0f, -4f), east);
                    look = east;
                    Report("구원의 손 시험: Shift+F8 = 적이 아군을 밀어 떨어뜨림 → 1초 안에 E");
                    break;
                case PieceKind.Knight:
                    Place(me, new Vector3(6.0f, 0f, 0f), west);
                    Place(enemies[0], new Vector3(4.1f, 0f, 0.1f), east);
                    Place(enemies[1], new Vector3(-2f, 0f, 4.5f), east);
                    Place(enemies[2], new Vector3(-4f, 0f, -3f), east);
                    Place(allies[0], far, east);
                    Report("벼랑 끝 역전 시험: 가장자리를 등진 나이트, 앞 1.9 m 적 — 적을 보고 E · Shift+F8 = 적이 뒤로 물러남");
                    break;
                default:
                    Report("폰은 스킬이 없어요");
                    return true;
            }
            if (Game.CameraRig != null) Game.CameraRig.yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>Shift+F8: what the chosen piece's edge test needs done to it.</summary>
        public void EdgeHelp()
        {
            var me = Local;
            if (me == null) return;
            switch (me.Pawn.Piece)
            {
                case PieceKind.King:
                {
                    var foe = NearestTo(me, me.Pawn.Team, false);
                    if (foe == null) return;
                    PushOff(foe, me);
                    Report($"{foe.Pawn.DisplayName} → 킹: 가장자리 밖으로 밀어냄 — 0.6초 안에 E");
                    break;
                }
                case PieceKind.Queen:
                {
                    var target = me.Skills != null && me.Skills.EdgeTarget != null ? me.Skills.EdgeTarget.GetComponent<SwordFightPawn>() : NearestTo(me, me.Pawn.Team, false);
                    if (target == null) return;
                    Vector3 inward = Floor != null && Floor.Nearest(target.Pawn.Hips.position, out var e) ? -e.outward : Vector3.left;
                    Walk(target, inward, 0.9f);
                    Report($"{target.Pawn.DisplayName}: 안쪽으로 걸어 들어옴");
                    break;
                }
                case PieceKind.Bishop:
                {
                    SwordFightPawn ally = null;
                    foreach (var f in Dummies(me.Pawn.Team)) if (f.Alive) { ally = f; break; }
                    if (ally == null) return;
                    var foe = NearestTo(ally, me.Pawn.Team, false);
                    if (foe == null) return;
                    PushOff(foe, ally);
                    Report($"{foe.Pawn.DisplayName} → {ally.Pawn.DisplayName}: 밀어 떨어뜨림 — 1초 안에 E");
                    break;
                }
                case PieceKind.Knight:
                {
                    var foe = me.Skills != null && me.Skills.EdgeTarget != null ? me.Skills.EdgeTarget.GetComponent<SwordFightPawn>() : NearestTo(me, me.Pawn.Team, false);
                    if (foe == null) return;
                    Walk(foe, Dir(me.Pawn.Hips.position, foe.Pawn.Hips.position), 0.8f);
                    Report($"{foe.Pawn.DisplayName}: 뒤로 물러남");
                    break;
                }
                default:
                    Report("이 기물은 Shift+F8 도우미가 없어요 (E만)");
                    break;
            }
        }

        /// <summary>An enemy swings at <paramref name="victim"/> and shoves it off the nearest edge (a set speed whatever it
        /// weighs: the king is three times as heavy and a cut alone hardly moves him).</summary>
        public void PushOff(SwordFightPawn by, SwordFightPawn victim)
        {
            if (by == null || victim == null || !victim.Alive) return;
            Vector3 o = Floor != null && Floor.Nearest(victim.Pawn.Hips.position, out var e) ? e.outward : Vector3.right;
            Cut(by, victim.Pawn.Hips.position);
            After(SwordFightPawn.Windup + 0.04f, () =>
            {
                if (victim == null || !victim.Alive) return;
                victim.Pawn.TakeHit((o * 3.4f + Vector3.up * 1.3f) * victim.Pawn.Weight, 0.65f, 0f, true);
                victim.StopCombat();
            });
        }

        /// <summary>F8: the enemy dummy nearest to me comes up beside me (wherever I am on the platform) and cuts at me; if I
        /// am a king in his guard, the guard waits for that cut. A bishop's ally (when an enemy is slowed or pinned) comes
        /// up to that enemy and cuts at it instead.</summary>
        public void EnemyCuts()
        {
            var me = Local;
            if (me == null) return;
            if (me.Skills != null && me.Skills.KingHanging)
            {
                // The king hangs on his sword (edge skill): the nearest enemy comes to the lip by the hilt and cuts at it.
                var foe = NearestTo(me, me.Pawn.Team, false);
                if (foe == null) return;
                Vector3 o = me.Skills.EdgeOut, lip = me.Skills.EdgeAt;
                Vector3 side = Vector3.Cross(Vector3.up, o);
                Place(foe, lip - o * 0.55f + side * 0.3f, o);
                Cut(foe, me.Skills.Hilt);
                Report($"{foe.Pawn.DisplayName} → 박힌 칼을 벰");
                return;
            }
            SwordFightPawn pinned = null;
            foreach (var s in SwordFightSkills.All)
                if (s != null && s.Fighter != null && s.Fighter.Alive && s.Pawn.Team != me.Pawn.Team && (s.PinLeft > 0f || s.SlowLeft > 0f)) pinned = s.Fighter;
            if (pinned != null)
            {
                var ally = NearestTo(pinned, me.Pawn.Team, true);
                if (ally != null)
                {
                    BringNear(ally, pinned);
                    Cut(ally, pinned.Pawn.Hips.position);
                    Report($"{ally.Pawn.DisplayName}이 {pinned.Pawn.DisplayName}을 벰");
                    return;
                }
            }
            var enemy = NearestTo(me, me.Pawn.Team, false);
            if (enemy == null) return;
            BringNear(enemy, me);
            Cut(enemy, me.Pawn.Hips.position);
            // The cut lands 0.2 to 0.4 s from now: a king already in his guard keeps it up until then.
            me.Skills?.HoldGuard(SwordFightPawn.SwingDuration + 0.1f);
            Report($"{enemy.Pawn.DisplayName}이 나를 벰");
        }

        /// <summary>A dummy more than a sword's reach from <paramref name="target"/> (or lying down) is put beside it first,
        /// on its own side of it if the floor is there (else the first way round that has floor), facing it.</summary>
        public static void BringNear(SwordFightPawn dummy, SwordFightPawn target)
        {
            if (dummy == null || target == null || !dummy.Alive) return;
            Vector3 at = target.Pawn.Hips.position, off = dummy.Pawn.Hips.position - at;
            off.y = 0f;
            if (off.magnitude <= 1.3f && dummy.Pawn.State == PawnState.Active) return;
            Vector3 dir = off.sqrMagnitude > 1e-4f ? off.normalized : target.Pawn.Facing;
            for (int i = 0; i < 8; i++)
            {
                Vector3 d = Quaternion.AngleAxis(45f * ((i + 1) / 2) * (i % 2 == 0 ? 1f : -1f), Vector3.up) * dir;
                Vector3 spot = at + d * 1.0f;
                if (!FloorAt(spot, at.y, out Vector3 floor)) continue;
                Place(dummy, floor, -d);
                return;
            }
        }

        /// <summary>The floor under a spot near a piece's height (not a piece, not a moving body).</summary>
        static bool FloorAt(Vector3 spot, float hipsY, out Vector3 floor)
        {
            floor = spot;
            float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(new Vector3(spot.x, hipsY + 1f, spot.z), Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (RagdollPawn.ColliderOwner.ContainsKey(h.collider)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                if (h.normal.y < 0.5f || h.distance >= best) continue;
                best = h.distance;
                floor = h.point;
            }
            return best < float.MaxValue;
        }

        SwordFightPawn NearestTo(SwordFightPawn of, int team, bool sameTeam)
        {
            SwordFightPawn best = null;
            float bd = float.MaxValue;
            foreach (var f in Game.Fighters.Values)
            {
                if (f == null || f == of || !f.Bot || !f.Alive || (f.Pawn.Team == team) != sameTeam) continue;
                float d = (f.Pawn.Hips.position - of.Pawn.Hips.position).sqrMagnitude;
                if (d < bd) { bd = d; best = f; }
            }
            return best;
        }

        void Home()
        {
            foreach (var f in Game.Fighters.Values)
                if (f != null && f.Bot && f.Alive) Place(f, SwordFightGame.SpawnPoint(f.Pawn.Team, f.Slot), SwordFightGame.Facing(f.Pawn.Team));
            Report("더미를 처음 자리로");
        }

        // ---------------------------------------------------------------- the panel

        static string HowTo(PieceKind kind) => kind switch
        {
            PieceKind.King => "F = 받아내기 자세 0.5초 (제자리, 조준 없음)\n그 사이 적 칼이 오면 막고 반경 2.5 m 적을 2.8 m 밀어냄 · 후딜 0.4초\n안 오면 빈틈 0.6초 · F8 = 가장 가까운 적이 옆으로 와서 벰 (맵 어디서든, 받아내기 중이면 그 칼을 기다림)",
            PieceKind.Queen => "F = 조준: 6 m × 1.2 m 줄 (걸을 수 있음)\n좌클릭 = 0.4초 예고 → 줄을 따라 돌진하며 벰: 첫째 3.0 m · 둘째 2.1 m · 셋째 1.5 m 밀림\n가장자리·벽 앞에서 멈춤 · 후딜 0.45초 · 우클릭(또는 F) = 취소",
            PieceKind.Rook => "F = 조준: 10 m × 1.5 m 통로\n좌클릭 = 0.5초 예고(제자리) → 칸마다 체스 말 모양 돌탑이 솟음(순서는 매번 무작위): 4 m 안 2.6 m · 그 뒤 1.6 m 밀림\n장애물에서 끊김 · 발동 중 방향 못 바꿈 · 후딜 0.4초 · 우클릭 = 취소",
            PieceKind.Bishop => "F = 조준: 화면 가운데가 가리키는 바닥 (8 m까지)\n좌클릭 = 0.4초 예고 → 손 두 개가 대각선으로 뻗어 반경 1 m 적의 발목을 잡음\n묶임 0.8초 + 감속 40% 1.5초 (맵 어디서든) · 우클릭 = 취소",
            PieceKind.Knight => "F = 조준: 착지점 (1.5~6 m)\n좌클릭 = 0.55초 도약 → 착지 때 앞쪽 두 자리(반경 1.2 m) 적 2.4 m 밀림\n두 자리는 뛰기 전부터 보임 · 후딜 0.35초 · 우클릭 = 취소",
            _ => "폰은 스킬이 없어요 (소드 파이트 규칙)",
        };

        static string EdgeHowTo(PieceKind kind, SwordFightEdgeParams e) => kind switch
        {
            PieceKind.King => $"밀려 떨어지는 {e.fallWindow:0.#}초 안에 E → 칼을 가장자리에 박고 {e.kingHang:0.#}초 매달림 → 뛰어올라 착지, 반경 {e.kingLandRadius:0.#} m 적 {e.kingLandPush:0.#} m 밀림\n매달린 동안 착지 자리가 보임 · 박힌 칼을 베면(F8) 떨어짐",
            PieceKind.Queen => $"가장자리 구역({e.edgeZone:0.#} m) 적이 {e.queenRange:0.#} m 안: E → 금색 선 {e.queenLine:0.#}초 → 순식간에 다가가 바깥 위로 {e.queenFling:0.#} m\n그 사이 적이 구역 밖으로 들어오면 취소(쿨 반) · 점프로 피함 · 킹 받아내기로 막힘",
            PieceKind.Rook => $"가장자리 구역에서 E → 보는 쪽 가장자리 {e.rookChunkWidth:0.#} × {e.rookChunkDepth:0.#} m에 {e.rookCrack:0.#}초 금 → 무너져 위의 기물이 모두 떨어짐(룩·아군도)\n{e.rookRestore:0.#}초 뒤 바닥이 다시 솟음",
            PieceKind.Bishop => $"가장자리 구역에서, {e.bishopRange:0.#} m 안 아군이 떨어지는 {e.bishopFallWindow:0.#}초 안에 E → 손이 잡아 {e.bishopPull:0.#}초 끌어올려 발판 위에\n끌어올리는 동안 비숍이 맞으면 놓침",
            PieceKind.Knight => $"가장자리를 등지고({e.knightBackEdge:0.#} m) 앞 {e.knightFront:0.#} m 안 적: E → {e.knightCrouch:0.##}초 웅크림 → 벼랑 밖으로 뛰어 공중제비로 적을 넘어 착지 → 뒷발차기 {e.knightKick:0.#} m\n웅크린 동안 적이 3 m 밖으로 물러나면 취소(쿨 반)",
            _ => "폰은 스킬이 없어요",
        };

        void Styles()
        {
            if (text != null) return;
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 13);
            text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 13, richText = true, wordWrap = true };
            text.normal.textColor = Color.white;
            small = new GUIStyle(text) { fontSize = 12 };
            small.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
            header = new GUIStyle(text) { fontSize = 15, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 12 };
            selected = new GUIStyle(button) { fontStyle = FontStyle.Bold };
            selected.normal.textColor = selected.hover.textColor = new Color(1f, 0.85f, 0.3f);
            panel = Solid(new Color(0.05f, 0.07f, 0.12f, 0.82f));
            bar = Solid(Color.white);
            box = new GUIStyle { padding = new RectOffset(10, 10, 8, 8) };
            box.normal.background = panel;
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        static string StageName(SwordFightSkills s) =>
            s.Stage == SfStage.None ? s.Cooldown > 0f ? "쿨타임" : "준비됨" : string.IsNullOrEmpty(s.Detail) ? s.Stage.ToString() : s.Detail;

        void OnGUI()
        {
            if (Game == null || SwordFightSkillFilm.Rolling) return;
            var me = Local;
            if (me == null || me.Skills == null) return;
            Styles();
            EdgeBadge(me.Skills);
            if (!showPanel) return;
            var s = me.Skills;
            GUILayout.BeginArea(new Rect(8f, 80f, 370f, Mathf.Min(Screen.height - 160f, 720f)), box);
            GUILayout.Label("소드 파이트 스킬 시험 (R91 · R102 · R103)", header);
            GUILayout.Label("1 킹 · 2 퀸 · 3 룩 · 4 비숍 · 5 나이트 · 6 폰  (Z ◀ ▶ X)\n스킬: <b>F</b> 조준 → <b>좌클릭</b> 발동 · <b>우클릭</b>(또는 F) 취소 · F7 시험 배치 · F8 더미가 와서 벰 · F9 더미 원위치 · F10 창 숨김\n가장자리 스킬: <b>E</b> · Shift+F7 시험 배치 · Shift+F8 도우미", small);
            GUILayout.BeginHorizontal();
            foreach (var kind in Pieces)
                if (GUILayout.Button(ChessPieces.Name(kind), me.Pawn.Piece == kind ? selected : button, GUILayout.Height(24f))) SetPiece(me, kind);
            GUILayout.EndHorizontal();
            GUILayout.Label($"<b>{ChessPieces.Name(me.Pawn.Piece)} — {SwordFightSkills.Name(me.Pawn.Piece)}</b>", text);
            GUILayout.Label(HowTo(me.Pawn.Piece), small);
            GUILayout.Label($"단계: <b>{StageName(s)}</b>" + (s.Stage != SfStage.None ? $"  {s.StageTime:0.00}초" : ""), text);
            float cd = s.CooldownTotal > 0f ? s.Cooldown / s.CooldownTotal : 0f;
            Bar(1f - cd, cd > 0f ? new Color(0.45f, 0.5f, 0.6f) : new Color(0.3f, 0.85f, 1f), cd > 0f ? $"쿨타임 {s.Cooldown:0.0}초" : "사용 가능");
            GUILayout.Label(skills.testCooldown > 0f ? $"시험용 쿨타임 {skills.testCooldown:0.#}초 (기획 쿨은 Inspector에서 0으로)" : "기획 쿨타임", small);
            if (FxLayer != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(FxLayer.effects ? "이펙트 켜짐" : "이펙트 꺼짐", FxLayer.effects ? selected : button)) FxLayer.effects = !FxLayer.effects;
                if (GUILayout.Button(FxLayer.hitStop ? "멈춤 켜짐" : "멈춤 꺼짐", FxLayer.hitStop ? selected : button)) FxLayer.hitStop = !FxLayer.hitStop;
                if (GUILayout.Button(FxLayer.slowMotion ? "슬로 켜짐" : "슬로 꺼짐", FxLayer.slowMotion ? selected : button)) FxLayer.slowMotion = !FxLayer.slowMotion;
                if (GUILayout.Button(FxLayer.shake ? "흔들기 켜짐" : "흔들기 꺼짐", FxLayer.shake ? selected : button)) FxLayer.shake = !FxLayer.shake;
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("시험 배치 (F7)", button)) Stage(me.Pawn.Piece);
            if (GUILayout.Button("더미가 와서 벰 (F8)", button)) EnemyCuts();
            if (GUILayout.Button("원위치 (F9)", button)) Home();
            GUILayout.EndHorizontal();

            // The edge skill (E, R103).
            GUILayout.Space(6f);
            GUILayout.Label($"<b>가장자리 스킬 (E) — {SwordFightSkills.EdgeName(me.Pawn.Piece)}</b>", text);
            GUILayout.Label(EdgeHowTo(me.Pawn.Piece, edge), small);
            string state = s.EdgeStage != SfEdge.None ? $"<b>{s.EdgeDetail}</b>  {s.EdgeTime:0.00}초"
                : s.EdgeCooldown > 0f ? "쿨타임" : s.EdgeCondition ? "<color=#7CFF8A><b>지금 쓸 수 있음</b></color>" : $"조건 안 됨: {s.EdgeWhy}";
            GUILayout.Label("상태: " + state, text);
            float ecd = s.EdgeCooldownTotal > 0f ? s.EdgeCooldown / s.EdgeCooldownTotal : 0f;
            Bar(1f - ecd, ecd > 0f ? new Color(0.45f, 0.5f, 0.6f) : s.EdgeCondition ? new Color(1f, 0.82f, 0.3f) : new Color(0.3f, 0.85f, 1f),
                ecd > 0f ? $"쿨타임 {s.EdgeCooldown:0.0}초" : s.EdgeCondition ? "E 사용 가능" : "조건 기다림");
            GUILayout.Label(edge.testCooldown > 0f ? $"시험용 쿨타임 {edge.testCooldown:0.#}초 (기획 {edge.Cooldown(me.Pawn.Piece):0}초, 예고 중 취소 = 반)" : "기획 쿨타임", small);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("가장자리 시험 배치 (Shift+F7)", button)) StageEdge(me.Pawn.Piece);
            if (GUILayout.Button("도우미 (Shift+F8)", button)) EdgeHelp();
            GUILayout.EndHorizontal();
            if (Cursor.lockState == CursorLockMode.Locked) GUILayout.Label("버튼은 Esc로 마우스를 푼 뒤에 눌러요", small);
            GUILayout.Space(4f);
            GUILayout.Label("<b>최근 결과</b>", text);
            for (int i = log.Count - 1; i >= 0; i--) GUILayout.Label(log[i], small);
            GUILayout.EndArea();
        }

        GUIStyle keyStyle;

        /// <summary>The edge skill's badge at the bottom of the view: E in the piece's colour when it can be used now
        /// (blinking), grey otherwise, the cooldown draining it from the top; under the name, what it is waiting for.</summary>
        void EdgeBadge(SwordFightSkills s)
        {
            if (!s.HasSkill || s.EdgeParams == null) return;
            if (keyStyle == null)
            {
                keyStyle = new GUIStyle(header) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
                keyStyle.normal.textColor = Color.white;
            }
            var pal = SwordFightSkills.Colors(s.Piece);
            bool ready = s.EdgeReady, running = s.EdgeStage != SfEdge.None;
            float w = 300f, h = 50f;
            var r = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 58f, w, h);
            GUI.color = new Color(0.05f, 0.07f, 0.12f, 0.8f);
            GUI.DrawTexture(r, bar);
            Color lit = ready || running ? pal.main : new Color(0.42f, 0.46f, 0.55f);
            float blink = ready ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 9f) : 1f;
            var key = new Rect(r.x + 6f, r.y + 6f, h - 12f, h - 12f);
            GUI.color = new Color(pal.ink.r, pal.ink.g, pal.ink.b, 1f);
            GUI.DrawTexture(new Rect(key.x - 2f, key.y - 2f, key.width + 4f, key.height + 4f), bar);
            GUI.color = new Color(lit.r, lit.g, lit.b, blink);
            GUI.DrawTexture(key, bar);
            float cd = s.EdgeCooldownTotal > 0f ? s.EdgeCooldown / s.EdgeCooldownTotal : 0f;
            if (cd > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(key.x, key.y, key.width, key.height * cd), bar);
            }
            GUI.color = Color.white;
            GUI.Label(key, "E", keyStyle);
            string name = SwordFightSkills.EdgeName(s.Piece);
            string line = running ? s.EdgeDetail : cd > 0f ? $"쿨타임 {s.EdgeCooldown:0.0}초" : ready ? "<color=#FFE07A><b>지금 쓸 수 있어요!</b></color>" : s.EdgeWhy;
            GUI.Label(new Rect(key.xMax + 10f, r.y + 5f, w - key.width - 22f, 22f), $"<b>{name}</b>", text);
            GUI.Label(new Rect(key.xMax + 10f, r.y + 25f, w - key.width - 22f, 22f), line, small);
        }

        void Bar(float fill, Color color, string label)
        {
            var r = GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true));
            GUI.color = new Color(1f, 1f, 1f, 0.15f);
            GUI.DrawTexture(r, bar);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fill), r.height), bar);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 6f, r.y, r.width, r.height), label, small);
        }
    }
}
