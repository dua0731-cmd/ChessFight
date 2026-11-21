using System;
using System.Collections;
using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Films the knight's ultimates as a player sees them (JY-skill-lab, 2026-11-21; the user: "영상을 너가 찍어서 보여주면
    /// 안돼?"): in the skill test scene, the game's own picture - P1's third-person camera, the cast's banner, words and
    /// grade - taken off the screen every frame (so the on-screen parts are in it, unlike PawnRushSkillFilm's fixed camera),
    /// at a fixed frame rate (Time.captureFramerate: game time steps 1/fps a frame, the video plays at true speed). The
    /// knight runs for a moment and presses Q; enemies of every piece jog ahead. One take of the family fork, one of the
    /// knight's tour. The test floor is widened for the film only. In the editor PawnRushUltFilmMenu writes the frames and
    /// the sounds into an .mp4.
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
        Vector3 p1Move, p1Aim = Vector3.forward;
        bool p1Q, driving, rolling;
        readonly List<(RagdollPawn pawn, Vector3 move)> runners = new List<(RagdollPawn, Vector3)>();
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
            width = w;
            height = h;
            rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            frame = new Texture2D(w, h, TextureFormat.RGB24, false);
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
            Status = "filming 패밀리 포크";
            yield return Take(PawnRushSkillIds.FamilyFork, "패밀리 포크  Nf7+!!", StageCrowd);
            Status = "filming 나이트 투어";
            yield return Take(PawnRushSkillIds.KnightTour, "나이트 투어  Nf5-d6-e8!!", StageLine);
            rolling = false;
            yield return null;
            yield return null;
            Ended?.Invoke();
            Time.captureFramerate = 0;
            foreach (var r in runners) if (r.pawn != null) r.pawn.SetInput(new PawnInput());
            runners.Clear();
            driving = false;
            game.SuppressInput = false;
            LabGame.CleanView = keptClean;
            bed.skills.testCooldown = keptCooldown;
            if (bigFloor != null) Destroy(bigFloor);
            Status = $"done {Frames} frames → {path}";
        }

        /// <summary>One take: the knight set down with the ultimate in its Q slot, the enemies placed, a moment to settle
        /// (not filmed), then filmed: a run of a second, Q, the whole ultimate, and its afterglow.</summary>
        IEnumerator Take(int id, string title, Action stage)
        {
            rolling = false;
            caption = title;
            if (p1.Piece != PieceKind.Knight) p1.SetPiece(PieceKind.Knight);
            p1.Team = Teams.White;
            p1.ResetSkill();
            int row = Array.IndexOf(PawnRushLoadout.RowPieces, PieceKind.Knight);
            bed.skills.loadout.Set(row, PawnRushLoadout.UltSlot, id);
            p1.Teleport(new Vector3(0f, p1.standHeight + 0.02f, -13f), Vector3.forward);
            p1Move = Vector3.zero;
            p1Aim = Vector3.forward;
            for (int i = 1; i < game.players.Length; i++)
                if (game.players[i].pawn != null) game.players[i].pawn.Teleport(new Vector3(-22f, game.players[i].pawn.standHeight + 0.02f, 30f + i * 2f), Vector3.back);
            foreach (var r in runners) if (r.pawn != null) r.pawn.SetInput(new PawnInput());
            runners.Clear();
            stage();
            if (game.labCamera != null)
            {
                game.labCamera.yaw = 0f;
                game.labCamera.pitch = 13f;
            }
            yield return Wait(0.9f);
            p1.FillGauge();
            rolling = true;
            p1Move = Vector3.forward;
            yield return Wait(1.1f);
            p1Move = Vector3.zero;
            p1Q = true;
            keyHint = "[ Q ]  궁극기";
            keyLeft = 1.2f;
            yield return null;
            float t = 0f;
            while (p1.PlanSkill != id && t < 1f) { t += Time.deltaTime; yield return null; }
            t = 0f;
            while (p1.PlanSkill == id && t < 14f) { t += Time.deltaTime; yield return null; }
            yield return Wait(2.6f);
            rolling = false;
        }

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }

        /// <summary>The family fork's crowd: seven enemies of every piece jogging ahead together, about 16 m off.</summary>
        void StageCrowd()
        {
            var spots = new[] { new Vector2(0f, 3f), new Vector2(1.6f, 4.2f), new Vector2(-1.5f, 4.6f), new Vector2(0.7f, 6f), new Vector2(-0.6f, 2f), new Vector2(2.3f, 2.4f), new Vector2(-2.4f, 3.2f) };
            Enemies(spots, 0.42f);
        }

        /// <summary>The tour's line: six enemies spread up the way, 3 to 4 m apart, jogging ahead.</summary>
        void StageLine()
        {
            var spots = new[] { new Vector2(-1.5f, -2f), new Vector2(1.8f, 1.5f), new Vector2(-1.2f, 5f), new Vector2(2f, 8.5f), new Vector2(-1.8f, 12f), new Vector2(1f, 15.5f) };
            Enemies(spots, 0.35f);
        }

        void Enemies(Vector2[] spots, float jog)
        {
            var kinds = new[] { PieceKind.Pawn, PieceKind.Rook, PieceKind.Bishop, PieceKind.Queen, PieceKind.Knight, PieceKind.King, PieceKind.Pawn };
            while (game.dummies.Count < spots.Length) game.AddDummy();
            for (int i = 0; i < game.dummies.Count; i++)
            {
                var d = game.dummies[i];
                d.ResetSkill();
                if (i >= spots.Length)
                {
                    d.Teleport(new Vector3(-24f + i * 1.5f, d.standHeight + 0.02f, 34f), Vector3.back);   // out of the way
                    continue;
                }
                var kind = kinds[i % kinds.Length];
                if (d.Piece != kind) d.SetPiece(kind);
                d.Team = Teams.Black;
                d.Teleport(new Vector3(spots[i].x, d.standHeight + 0.02f, spots[i].y), Vector3.forward);
                runners.Add((d, Vector3.forward * jog));
            }
        }

        /// <summary>The test floor is 30 m; a fork throws 20 m and the tour's last leap goes further: a wider copy under it
        /// for the film's time.</summary>
        void WidenFloor()
        {
            if (bigFloor != null) return;
            var floor = GameObject.Find("Floor 30x30");
            if (floor == null) return;
            bigFloor = Instantiate(floor, floor.transform.parent);
            bigFloor.name = "Film floor";
            var s = bigFloor.transform.localScale;
            bigFloor.transform.localScale = new Vector3(s.x * 3.4f, s.y, s.z * 3.4f);
            bigFloor.transform.position = floor.transform.position + new Vector3(0f, -0.004f, 12f);
        }

        void Update()
        {
            if (!driving) return;
            if (p1 != null)
            {
                p1.SetInput(new PawnInput { move = p1Move, aim = p1Aim, sprint = p1Move.sqrMagnitude > 0.01f, ability2 = p1Q });
                p1Q = false;
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
                keyStyle = new GUIStyle(captionStyle) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            }
            float ui = Screen.height / 720f;
            var keepMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(ui, ui, 1f));
            var keepColor = GUI.color;
            GUI.color = new Color(0.03f, 0.05f, 0.08f, 0.7f);
            GUI.DrawTexture(new Rect(14f, 14f, 330f, 30f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(24f, 14f, 330f, 30f), "유니티 실제 화면 · " + caption, captionStyle);
            if (keyLeft > 0f)
            {
                float vw = Screen.width / ui;
                GUI.color = new Color(0.03f, 0.05f, 0.08f, 0.7f * Mathf.Clamp01(keyLeft / 0.3f));
                GUI.DrawTexture(new Rect(vw * 0.5f - 110f, 640f, 220f, 40f), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.85f, 0.3f, Mathf.Clamp01(keyLeft / 0.3f));
                GUI.Label(new Rect(vw * 0.5f - 110f, 640f, 220f, 40f), keyHint, keyStyle);
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
        }
    }
}
