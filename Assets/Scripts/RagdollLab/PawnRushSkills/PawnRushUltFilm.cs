using System;
using System.Collections;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Films the six driven ultimates as a player sees them (JY-skill-lab, 2026-11-22; the user: "그냥 너가 영상으로 보여줘.
    /// 지금 확인할 시간이 없다"): in the skill test scene, the game's own picture - P1's third-person camera with every
    /// banner, bar and word on it - taken off the screen every frame at a fixed frame rate (Time.captureFramerate: the
    /// video plays at true speed). P1 is played by this script the way a player would: the camera turned onto a target,
    /// the left click, a held click, the right click, the jump, the move keys. One take each: the nightmare, the long
    /// diagonal (two walls put up for its bounces), the promotion, the rock and roll, the queen's descent, the king's walk
    /// (three teammates behind him to join his train). The test floor is widened for the film only. In the editor
    /// PawnRushUltFilmMenu writes the frames and the sounds into an .mp4.
    /// </summary>
    public class PawnRushUltFilm : MonoBehaviour
    {
        public static event Action<string, int, int, int> Began;
        public static event Action<Texture2D> Frame;
        public static event Action Ended;
        public static string Status { get; private set; } = "idle";
        public static int Frames { get; private set; }

        LabGame game;
        PawnRushSkillBed bed;
        RagdollPawn p1;
        Vector3 p1Move;
        bool p1Q, p1Click, p1Held, p1Right, p1Jump, driving, rolling;
        Vector3? lookAt, lookWay;
        RagdollPawn lookFollow;
        float turnRate = 400f, lookPitch = 14f;
        readonly List<(RagdollPawn pawn, Vector3 move)> runners = new List<(RagdollPawn, Vector3)>();
        readonly List<GameObject> props = new List<GameObject>();
        readonly HashSet<RagdollPawn> aimed = new HashSet<RagdollPawn>();
        RenderTexture rt;
        Texture2D frame;
        int width, height;
        string caption = "", keyHint = "";
        float keyLeft;
        GameObject bigFloor;
        GUIStyle captionStyle, keyStyle;

        public static string Run(string path, int w = 1280, int h = 720, int fps = 30)
        {
            var bed = FindFirstObjectByType<PawnRushSkillBed>();
            if (bed == null) return Status = "no PawnRushSkillBed (open PawnRush_SkillTest and press Play)";
            var film = bed.GetComponent<PawnRushUltFilm>();
            if (film == null) film = bed.gameObject.AddComponent<PawnRushUltFilm>();
            film.StopAllCoroutines();
            film.StartCoroutine(film.Main(path, w, h, fps));
            return Status = "starting";
        }

        IEnumerator Main(string path, int w, int h, int fps)
        {
            game = GetComponent<LabGame>();
            bed = GetComponent<PawnRushSkillBed>();
            yield return null;
            p1 = game != null && game.players.Length > 0 ? game.players[0].pawn : null;
            if (p1 == null || bed == null)
            {
                Status = "done (no P1)";
                yield break;
            }
            Application.runInBackground = true;
            width = w;
            height = h;
            rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            frame = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float keptCooldown = bed.skills.testCooldown;
            bed.skills.testCooldown = 2f;   // keeps the team gauge full between the takes
            bool keptClean = LabGame.CleanView;
            LabGame.CleanView = true;       // no test panel in the picture (the game's own card and words stay)
            game.SuppressInput = true;
            driving = true;
            WidenFloor();
            Time.captureFramerate = fps;
            Frames = 0;
            Began?.Invoke(path, w, h, fps);
            StartCoroutine(Capture());
            var takes = new (int id, PieceKind piece, string title, Func<IEnumerator> play)[]
            {
                (PawnRushSkillIds.Nightmare, PieceKind.Knight, "나이트메어  KNIGHTMARE", PlayNightmare),
                (PawnRushSkillIds.GreatDiagonal, PieceKind.Bishop, "롱 다이애거널  a1–h8!!", PlayDiagonal),
                (PawnRushSkillIds.PromotionRun, PieceKind.Pawn, "승격  e8=Q!!", PlayPromotion),
                (PawnRushSkillIds.RookRoll, PieceKind.Rook, "룩앤롤  Rook'n'Roll", PlayRoll),
                (PawnRushSkillIds.QueenDescent, PieceKind.Queen, "여왕 강림  Qh7+!!", PlayDescent),
                (PawnRushSkillIds.KingWalk, PieceKind.King, "킹 워크  Kg3–f4–g5–h6!!", PlayWalk),
            };
            foreach (var t in takes)
            {
                Status = "filming " + t.title;
                Debug.Log("[UltFilm] " + Status);
                yield return Take(t.id, t.piece, t.title, t.play);
            }
            rolling = false;
            yield return null;
            yield return null;
            Ended?.Invoke();
            Time.captureFramerate = 0;
            foreach (var r in runners) if (r.pawn != null) r.pawn.SetInput(new PawnInput());
            runners.Clear();
            ClearProps();
            driving = false;
            game.SuppressInput = false;
            LabGame.CleanView = keptClean;
            bed.skills.testCooldown = keptCooldown;
            if (bigFloor != null) Destroy(bigFloor);
            Status = $"done {Frames} frames → {path}";
        }

        /// <summary>One take: P1 set down as the piece with the ultimate in its Q slot, the others placed, a moment to
        /// settle (not filmed); then filmed: a short run, Q, the ultimate played by <paramref name="play"/>, its end and
        /// afterglow.</summary>
        IEnumerator Take(int id, PieceKind piece, string title, Func<IEnumerator> play)
        {
            rolling = false;
            caption = title;
            ClearProps();
            aimed.Clear();
            lookAt = lookWay = null;
            lookFollow = null;
            p1Move = Vector3.zero;
            p1Held = p1Right = false;
            if (p1.Piece != piece) p1.SetPiece(piece);
            p1.Team = Teams.White;
            p1.ResetSkill();
            int row = Array.IndexOf(PawnRushLoadout.RowPieces, piece);
            bed.skills.loadout.Set(row, PawnRushLoadout.UltSlot, id);
            foreach (var r in runners) if (r.pawn != null) r.pawn.SetInput(new PawnInput());
            runners.Clear();
            for (int i = 1; i < game.players.Length; i++)
                if (game.players[i].pawn != null) game.players[i].pawn.Teleport(new Vector3(-80f, game.players[i].pawn.standHeight + 0.02f, 90f + i * 2f), Vector3.back);
            Vector3 start = Stage(id);
            p1.Teleport(start + Vector3.up * (p1.standHeight + 0.02f), Vector3.forward);
            SetView(0f, 14f);
            yield return Wait(1f);
            p1.FillGauge();
            rolling = true;
            p1Move = Vector3.forward;
            yield return Wait(0.6f);
            p1Move = Vector3.zero;
            p1Q = true;
            Key("[ Q ]  궁극기");
            float t = 0f;
            while (p1.PlanSkill != id && t < 1f) { t += Time.deltaTime; yield return null; }
            yield return play();
            t = 0f;
            while (p1.PlanSkill == id && t < 26f) { t += Time.deltaTime; yield return null; }
            p1Move = Vector3.zero;
            p1Held = p1Right = false;
            lookAt = null;
            lookFollow = null;
            yield return Wait(2.4f);
            rolling = false;
        }

        bool Running(int id) => p1.PlanSkill == id && p1.PlanPhaseNow == PlanPhase.Active;

        // ---------------------------------------------------------------- the takes

        IEnumerator PlayNightmare()
        {
            for (int hop = 0; hop < 9 && Running(PawnRushSkillIds.Nightmare); hop++)
            {
                var e = NextFoe(p1.FeetPoint, 38f);
                if (e == null) break;
                aimed.Add(e);
                lookFollow = e;
                turnRate = 520f;
                yield return Wait(0.4f);
                Click();
                Key("[ 좌클릭 ]  도약");
                float t = 0f;
                yield return Wait(0.1f);
                while ((p1.PlanNote & RagdollPawn.NmFlying) != 0 && t < 3f) { t += Time.deltaTime; yield return null; }
                yield return Wait(0.2f);
            }
            lookFollow = null;
        }

        IEnumerator PlayDiagonal()
        {
            // The bounce shot: 45° right, off the far wall and the side wall.
            p1Move = Vector3.forward * 0.35f;
            SetLook(new Vector3(1f, 0f, 1f), 10f);
            yield return Wait(0.6f);
            yield return Hold(0.75f);
            Key("[ 좌클릭 떼기 ]  발사");
            yield return Wait(1.8f);
            // Straight on at the two ahead.
            var e = NextFoe(p1.FeetPoint, 80f);
            if (e != null) lookFollow = e;
            yield return Wait(0.5f);
            yield return Hold(0.75f);
            yield return Wait(1.6f);
            lookFollow = NextFoe(p1.FeetPoint, 80f);
            yield return Wait(0.5f);
            yield return Hold(0.75f);
            lookFollow = null;
            p1Move = Vector3.zero;
        }

        IEnumerator PlayPromotion()
        {
            // The run: straight on (through the two in the way), a little steering.
            SetLook(Vector3.forward, 12f);
            p1Move = Vector3.forward;
            yield return Wait(1.4f);
            p1Move = new Vector3(0.15f, 0f, 1f);
            Key("[ 이동 키 ]  질주 방향");
            float t = 0f;
            while (Running(PawnRushSkillIds.PromotionRun) && ((p1.PlanNote >> RagdollPawn.PrStageShift) & 3) != RagdollPawn.PrQueen && t < 4f) { t += Time.deltaTime; yield return null; }
            p1Move = Vector3.zero;
            yield return Wait(0.5f);
            for (int dash = 0; dash < 6 && Running(PawnRushSkillIds.PromotionRun) && (p1.PlanNote & RagdollPawn.PrDashMask) > 0; dash++)
            {
                var e = NextFoe(p1.FeetPoint, 40f);
                if (e == null) break;
                aimed.Add(e);
                lookFollow = e;
                turnRate = 360f;
                yield return Wait(0.45f);
                Click();
                Key("[ 좌클릭 ]  돌진");
                yield return Wait(1.3f);
            }
            lookFollow = null;
        }

        IEnumerator PlayRoll()
        {
            SetLook(Vector3.forward, 12f);
            p1Move = Vector3.forward;
            Key("[ 이동 키 ]  몰기");
            yield return Wait(2.6f);
            // Past the pins: a wide turn to the right, a jump on the way round, round again.
            float t = 0f;
            bool jumped = false;
            while (Running(PawnRushSkillIds.RookRoll) && t < 12f)
            {
                t += Time.deltaTime;
                Vector3 dir = p1.PlanDir;
                Vector3 right = new Vector3(dir.z, 0f, -dir.x);
                p1Move = (dir + right * 0.9f).normalized;
                SetLook(dir, 14f);
                if (!jumped && t > 1.6f)
                {
                    jumped = true;
                    p1Jump = true;
                    Key("[ 점프 ]  크게 튀기");
                }
                yield return null;
            }
            p1Move = Vector3.zero;
        }

        IEnumerator PlayDescent()
        {
            SetLook(Vector3.forward, 20f);
            yield return Wait(1f);
            var groups = new[] { new Vector3(0f, 0f, 32f), new Vector3(26f, 0f, 52f), new Vector3(-22f, 0f, 74f) };
            for (int g = 0; g < groups.Length && Running(PawnRushSkillIds.QueenDescent); g++)
            {
                Vector3 to = groups[g];
                Key("[ 이동 키 ]  날기");
                float t = 0f;
                while (Running(PawnRushSkillIds.QueenDescent) && Flat(to - p1.FeetPoint).magnitude > 16f && t < 3f)
                {
                    t += Time.deltaTime;
                    p1Move = Flat(to - p1.FeetPoint).normalized;
                    SetLook(Flat(to - p1.FeetPoint), 30f);
                    yield return null;
                }
                p1Move = Vector3.zero;
                lookAt = to;
                turnRate = 300f;
                yield return Wait(0.5f);
                if (g == groups.Length - 1)
                {
                    yield return RightClick();
                    Key("[ 우클릭 ]  퀸 희생!!");
                }
                else
                {
                    Click();
                    Key("[ 좌클릭 ]  내리꽂기");
                }
                yield return Wait(1.9f);
                lookAt = null;
            }
        }

        IEnumerator PlayWalk()
        {
            SetLook(Vector3.forward, 14f);
            p1Move = Vector3.forward;
            Key("[ 이동 키 ]  행진 · 카펫에 선 팀원은 행렬로");
            yield return Wait(2.4f);
            for (int wave = 0; wave < 3 && Running(PawnRushSkillIds.KingWalk); wave++)
            {
                Click();
                Key("[ 좌클릭 ]  왕관 충격파");
                yield return Wait(2.6f);
                p1Move = new Vector3(wave == 0 ? 0.25f : -0.2f, 0f, 1f).normalized;
            }
            float t = 0f;
            while (Running(PawnRushSkillIds.KingWalk) && t < 14f) { t += Time.deltaTime; yield return null; }
            p1Move = Vector3.zero;
        }

        // ---------------------------------------------------------------- the stages

        /// <summary>Places the others for a take; P1's start back.</summary>
        Vector3 Stage(int id)
        {
            switch (id)
            {
                case PawnRushSkillIds.Nightmare:
                    // A ring of enemies, two of them close enough for a fork.
                    Place(new[] { V(6f, 14f), V(7.6f, 15f), V(-10f, 24f), V(4f, 36f), V(-6f, 46f), V(14f, 30f), V(-18f, 36f) }, 0.15f, Teams.Black);
                    return new Vector3(0f, 0f, -6f);
                case PawnRushSkillIds.GreatDiagonal:
                    // Two walls for the line to turn on: one across the way at 20 m, one along it 55 m to the right.
                    Wall(new Vector3(28f, 3f, 20.5f), new Vector3(76f, 6f, 1f));
                    Wall(new Vector3(55.5f, 3f, -2f), new Vector3(1f, 6f, 46f));
                    Place(new[] { V(8f, -2f), V(16f, 6f), V(38f, 12f), V(46f, 4f), V(47f, -12f), V(-1f, 10f), V(1.2f, 14f) }, 0f, Teams.Black);
                    return new Vector3(0f, 0f, -10f);
                case PawnRushSkillIds.PromotionRun:
                    Place(new[] { V(0.6f, -12f), V(-0.7f, 6f), V(3f, 43f), V(-4f, 40f), V(1.5f, 48f), V(-8f, 60f), V(9f, 66f) }, 0.12f, Teams.Black);
                    return new Vector3(0f, 0f, -42f);
                case PawnRushSkillIds.RookRoll:
                    // Six pins in a triangle 55 m ahead.
                    Place(new[] { V(0f, 22f), V(-1.2f, 23.6f), V(1.2f, 23.6f), V(-2.3f, 25.2f), V(0f, 25.2f), V(2.3f, 25.2f), V(-26f, 40f) }, 0f, Teams.Black);
                    return new Vector3(0f, 0f, -34f);
                case PawnRushSkillIds.QueenDescent:
                    Place(new[] { V(0f, 32f), V(2.5f, 34f), V(-2.4f, 30.5f), V(26f, 52f), V(23f, 54f), V(-22f, 74f), V(-19f, 76f), V(-25f, 72f) }, 0.1f, Teams.Black);
                    return new Vector3(0f, 0f, -12f);
                default:
                    // The king's walk: three teammates behind him on the carpet's way, enemies spread ahead.
                    Place(new[] { V(0f, 13f), V(-6f, 20f), V(6f, 22f), V(-2f, 30f), V(8f, 34f), V(0.3f, -7f), V(-0.4f, -14f), V(0.5f, -21f) }, 0.1f, Teams.Black, 5);
                    return new Vector3(0f, 0f, 0f);
            }
        }

        static Vector3 V(float x, float z) => new Vector3(x, 0f, z);

        /// <summary>The dummies on <paramref name="spots"/>: the first <paramref name="enemies"/> (all, by default) the other
        /// team jogging ahead at <paramref name="jog"/>, the rest P1's teammates standing.</summary>
        void Place(Vector3[] spots, float jog, int team, int enemies = -1)
        {
            if (enemies < 0) enemies = spots.Length;
            var kinds = new[] { PieceKind.Pawn, PieceKind.Rook, PieceKind.Bishop, PieceKind.Queen, PieceKind.Knight, PieceKind.King, PieceKind.Pawn, PieceKind.Bishop };
            while (game.dummies.Count < spots.Length) game.AddDummy();
            for (int i = 0; i < game.dummies.Count; i++)
            {
                var d = game.dummies[i];
                d.ResetSkill();
                if (i >= spots.Length)
                {
                    d.Teleport(new Vector3(-80f + i * 1.5f, d.standHeight + 0.02f, 95f), Vector3.back);   // out of the way
                    continue;
                }
                var kind = kinds[i % kinds.Length];
                if (d.Piece != kind) d.SetPiece(kind);
                bool enemy = i < enemies;
                d.Team = enemy ? team : Teams.White;
                d.Teleport(spots[i] + Vector3.up * (d.standHeight + 0.02f), Vector3.forward);
                if (enemy && jog > 0f) runners.Add((d, Vector3.forward * jog));
            }
        }

        void Wall(Vector3 at, Vector3 size)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
            w.name = "Film wall";
            w.transform.position = at;
            w.transform.localScale = size;
            w.GetComponent<MeshRenderer>().sharedMaterial = LitMaterials.NewLit(new Color(0.55f, 0.55f, 0.6f), 0.2f);
            props.Add(w);
        }

        void ClearProps()
        {
            foreach (var p in props) if (p != null) Destroy(p);
            props.Clear();
        }

        /// <summary>The nearest standing enemy not aimed at yet within <paramref name="reach"/>.</summary>
        RagdollPawn NextFoe(Vector3 from, float reach)
        {
            RagdollPawn best = null;
            float bestFar = reach;
            foreach (var d in game.dummies)
            {
                if (d == null || d.Team == p1.Team || d.State != PawnState.Active || d.Squashed || aimed.Contains(d)) continue;
                float far = Flat(d.FeetPoint - from).magnitude;
                if (far > bestFar || far < 2f) continue;
                best = d;
                bestFar = far;
            }
            return best;
        }

        /// <summary>The test floor is 30 m; the ultimates go 100 m and more: a wide copy under it for the film's time.</summary>
        void WidenFloor()
        {
            if (bigFloor != null) return;
            var floor = GameObject.Find("Floor 30x30");
            if (floor == null) return;
            bigFloor = Instantiate(floor, floor.transform.parent);
            bigFloor.name = "Film floor";
            var s = bigFloor.transform.localScale;
            bigFloor.transform.localScale = new Vector3(s.x * 6f, s.y, s.z * 6f);
            bigFloor.transform.position = floor.transform.position + new Vector3(0f, -0.004f, 14f);
        }

        // ---------------------------------------------------------------- the player's hands

        void Click() => p1Click = true;

        IEnumerator Hold(float seconds)
        {
            p1Held = true;
            Key("[ 좌클릭 누르고 ]  모으기");
            yield return Wait(seconds);
            p1Held = false;
        }

        IEnumerator RightClick()
        {
            p1Right = true;
            yield return Wait(0.12f);
            p1Right = false;
        }

        void Key(string text)
        {
            keyHint = text;
            keyLeft = 1.3f;
        }

        void SetView(float yaw, float pitch)
        {
            if (game.labCamera == null) return;
            game.labCamera.yaw = yaw;
            game.labCamera.pitch = pitch;
        }

        /// <summary>Turns the camera to look along <paramref name="way"/> (flat) at <paramref name="pitch"/>°.</summary>
        void SetLook(Vector3 way, float pitch)
        {
            lookAt = null;
            lookFollow = null;
            way = Flat(way);
            if (way.sqrMagnitude < 1e-4f || game.labCamera == null) return;
            lookPitch = pitch;
            lookWay = way.normalized;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }

        void Update()
        {
            if (!driving) return;
            var cam = game != null ? game.labCamera : null;
            Vector3? target = lookFollow != null ? lookFollow.FeetPoint : lookAt;
            if (cam != null && !target.HasValue && lookWay.HasValue)
            {
                Vector3 w = lookWay.Value;
                cam.yaw = Mathf.MoveTowardsAngle(cam.yaw, Mathf.Atan2(w.x, w.z) * Mathf.Rad2Deg, turnRate * Time.deltaTime);
                cam.pitch = Mathf.MoveTowards(cam.pitch, lookPitch, 120f * Time.deltaTime);
            }
            if (cam != null && target.HasValue && p1 != null)
            {
                // The look that the host's aim rule turns into this floor point (from about where the camera stands).
                Vector3 flat = Flat(target.Value - p1.Hips.position);
                if (flat.sqrMagnitude > 0.25f)
                {
                    Vector3 eye = p1.Hips.position + Vector3.up * 1.6f - flat.normalized * 3.5f, d = target.Value - eye;
                    float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, pitch = Mathf.Clamp(-Mathf.Asin(d.normalized.y) * Mathf.Rad2Deg, -10f, 75f);
                    cam.yaw = Mathf.MoveTowardsAngle(cam.yaw, yaw, turnRate * Time.deltaTime);
                    cam.pitch = Mathf.MoveTowards(cam.pitch, pitch, turnRate * 0.5f * Time.deltaTime);
                }
            }
            if (p1 != null)
            {
                Vector3 aim = cam != null ? cam.AimForward : Vector3.forward;
                p1.SetInput(new PawnInput
                {
                    move = p1Move, aim = aim, sprint = p1Move.sqrMagnitude > 0.01f && p1.PlanSkill == 0, ability2 = p1Q,
                    shove = p1Click, shoveHeld = p1Held, grab = p1Right, jump = p1Jump,
                });
                p1Q = p1Click = p1Jump = false;
            }
            foreach (var r in runners)
                if (r.pawn != null) r.pawn.SetInput(new PawnInput { move = r.move, aim = Vector3.forward });
            if (keyLeft > 0f) keyLeft -= Time.deltaTime;
        }

        /// <summary>Every frame while filming: the screen as it was drawn (the game and everything on it), at the film's size.</summary>
        IEnumerator Capture()
        {
            var end = new WaitForEndOfFrame();
            while (driving)
            {
                yield return end;
                if (!rolling) continue;
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                if (shot == null) continue;
                Graphics.Blit(shot, rt);
                Destroy(shot);
                var keep = RenderTexture.active;
                RenderTexture.active = rt;
                frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                frame.Apply();
                RenderTexture.active = keep;
                Frame?.Invoke(frame);
                Frames++;
                if (Frames % 150 == 0) Debug.Log($"[UltFilm] {Frames} frames ({Status})");
            }
        }

        void OnGUI()
        {
            if (!rolling || Event.current.type != EventType.Repaint) return;
            if (captionStyle == null)
            {
                var body = ChessFight.Game.RuntimePanels.KoreanFont;
                captionStyle = new GUIStyle { font = body, fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                captionStyle.normal.textColor = Color.white;
                keyStyle = new GUIStyle(captionStyle) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            }
            float ui = Screen.height / 720f;
            var keepMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(ui, ui, 1f));
            var keepColor = GUI.color;
            GUI.color = new Color(0.03f, 0.05f, 0.08f, 0.7f);
            GUI.DrawTexture(new Rect(14f, 14f, 380f, 30f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(24f, 14f, 380f, 30f), "유니티 실제 화면 · " + caption, captionStyle);
            if (keyLeft > 0f)
            {
                float vw = Screen.width / ui, k = Mathf.Clamp01(keyLeft / 0.3f);
                GUI.color = new Color(0.03f, 0.05f, 0.08f, 0.7f * k);
                GUI.DrawTexture(new Rect(vw * 0.5f - 230f, 668f, 460f, 36f), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 1f, 1f, k);
                GUI.Label(new Rect(vw * 0.5f - 230f, 668f, 460f, 36f), keyHint, keyStyle);
            }
            GUI.color = keepColor;
            GUI.matrix = keepMatrix;
        }

        void OnDestroy()
        {
            if (Time.captureFramerate != 0) Time.captureFramerate = 0;
            if (rt != null) Destroy(rt);
            if (frame != null) Destroy(frame);
            if (bigFloor != null) Destroy(bigFloor);
            ClearProps();
        }
    }
}
