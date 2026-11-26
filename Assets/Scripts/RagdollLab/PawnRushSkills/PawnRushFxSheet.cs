using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Films the skills' effects to look at them frame by frame with nobody at the keys (2026-11-24; the user:
    /// "이전에 구현했던 스킬들도 이펙트를 제대로 만들어줘 ... 웃기고 코믹한 느낌이되 너무 허접하진 않고 퀄리티 좋게", then
    /// "찍은 영상 올려주면 안돼?"). In the skill test scene's Play mode each shot puts one skill in a slot of P1's piece, stages
    /// P1 and enemy dummies and plays a few keys (PawnRushSkillProbe.Perform), and films game time at 30 frames a second
    /// (Time.captureFramerate, so the result is the same however slow the machine) from a fixed camera, captioned with the
    /// skill, its piece and key, and the key as it is pressed: every frame as <c>&lt;folder&gt;/&lt;shot&gt;/f_0000.jpg</c>
    /// (960 x 540, for a video: Tools/Film-SkillFx.ps1 encodes them) and a contact sheet <c>&lt;folder&gt;/&lt;shot&gt;.jpg</c>
    /// (24 frames, four across). Batch: Tools/Film-SkillFx.ps1 (PawnRushFxSheetBatch). In the editor while playing:
    /// <c>PawnRushFxSheet.Begin("Logs/FxSheets", "BackKick,StoneThrow", null)</c>.
    /// </summary>
    [DefaultExecutionOrder(300)]   // after the effects shake the camera (210), as PawnRushSkillFilm
    public class PawnRushFxSheet : MonoBehaviour
    {
        public struct Shot
        {
            public string name;
            public int id;
            public PieceKind piece;
            public int slot;
            public string script;
            /// <summary>The enemies, in P1's frame: x to its right, y ahead (metres).</summary>
            public Vector2[] enemies;
            /// <summary>Filmed from when the stage has settled (after the lead-in).</summary>
            public float seconds;
            /// <summary>The camera, in P1's frame (x right, y up, z ahead).</summary>
            public Vector3 eye, look;
        }

        public static string Status { get; private set; } = "idle";
        /// <summary>The caption's second line (e.g. which build of the effects this is).</summary>
        public static string Note = "";
        const int Cols = 4, MaxFrames = 24, CellW = 480, CellH = 270, FrameW = 960, FrameH = 540, Fps = 30;
        const float Lead = 0.4f;
        static readonly Vector3 Ground = new Vector3(0f, 0f, 9f), Face = Vector3.back;

        static Shot S(string name, int id, PieceKind piece, int slot, string script, float seconds, Vector3 eye, Vector3 look, params Vector2[] enemies) =>
            new Shot { name = name, id = id, piece = piece, slot = slot, script = script, seconds = seconds, eye = eye, look = look, enemies = enemies };

        static Vector2 V(float x, float y) => new Vector2(x, y);

        /// <summary>The shots by name; "set3" is all of 3판's.</summary>
        public static readonly Shot[] Shots =
        {
            S("Bolas", PawnRushSkillIds.Bolas, PieceKind.Pawn, 0, "k w1.25 c", 2.6f, new Vector3(3.5f, 2.4f, -2.5f), new Vector3(0f, 0.6f, 4.5f), V(0f, 8f)),
            S("TwoSteps", PawnRushSkillIds.TwoSteps, PieceKind.Pawn, 1, "k w0.35 k", 1.8f, new Vector3(3f, 2f, -1.5f), new Vector3(0f, 0.5f, 2f), V(0.6f, 3.2f)),
            S("HighStomp", PawnRushSkillIds.HighStomp, PieceKind.Knight, 0, "k w0.55 k", 2.2f, new Vector3(4f, 2.8f, -1f), new Vector3(0f, 1.2f, 3f), V(0.5f, 5f)),
            S("BackKick", PawnRushSkillIds.BackKick, PieceKind.Knight, 1, "k", 1.6f, new Vector3(4f, 1.8f, 0.3f), new Vector3(0f, 0.6f, 0f), V(0f, -1.6f), V(0.8f, -1.4f)),
            S("HolySwords", PawnRushSkillIds.HolySwords, PieceKind.Bishop, 0, "k", 6f, new Vector3(0f, 6f, -3f), new Vector3(0f, 0f, 5.5f), V(-2.5f, 6f), V(2.5f, 8f), V(3.5f, 3.5f)),
            S("StoneThrow", PawnRushSkillIds.StoneThrow, PieceKind.Bishop, 1, "k", 2.8f, new Vector3(4f, 2.8f, -1.5f), new Vector3(0f, 0.6f, 6f), V(0f, 6f), V(0.7f, 10f)),
            S("HolyWater", PawnRushSkillIds.HolyWater, PieceKind.Bishop, 2, "k", 2.6f, new Vector3(3.5f, 3f, 2f), new Vector3(0f, 0.4f, 7.5f), V(0f, 8f), V(1f, 8.8f)),
            S("RollShort", PawnRushSkillIds.RollShort, PieceKind.Rook, 0, "k", 3f, new Vector3(5f, 3f, 3f), new Vector3(0f, 0.5f, 8f), V(0f, 7f), V(0.6f, 10f), V(-0.6f, 10.5f)),
            S("TowerTrap", PawnRushSkillIds.TowerTrap, PieceKind.Rook, 1, "k", 2.6f, new Vector3(4f, 2.8f, 2f), new Vector3(0f, 0.8f, 6.5f), V(0f, 7f)),
            S("QueenStar", PawnRushSkillIds.QueenStar, PieceKind.Queen, 0, "k", 2f, new Vector3(0f, 9f, -5.5f), new Vector3(0f, 0f, 0.5f), V(3f, 3f), V(-4f, 2f), V(0.5f, -5f), V(5f, -2f)),
            S("CheckChain", PawnRushSkillIds.CheckChain, PieceKind.Queen, 1, "k w0.6 k w0.6 k", 2.6f, new Vector3(6f, 5f, 1f), new Vector3(0f, 0.5f, 8f), V(0f, 6f), V(4f, 10f), V(-2f, 13f)),
            // The set 4판's bundle 가 (11-26).
            S("Trip4", PawnRushSkillIds.Trip4, PieceKind.Pawn, 0, "m0,1 w0.45 k w0.7 m-", 2.8f, new Vector3(3.5f, 2.6f, 8f), new Vector3(0f, 0.6f, 3f), V(1.3f, 3.2f), V(-1.3f, 3.7f)),
            S("Brick", PawnRushSkillIds.Brick, PieceKind.Pawn, 1, "k", 3.2f, new Vector3(4.5f, 3.2f, 0.5f), new Vector3(0f, 2.1f, 6f), V(0.3f, 6f)),
            S("Pit", PawnRushSkillIds.Pit, PieceKind.Pawn, 2, "k", 2.8f, new Vector3(3f, 2.4f, 1.5f), new Vector3(0f, 0.4f, -1f), V(0f, -1.1f)),
            S("PitHole", PawnRushSkillIds.Pit, PieceKind.Pawn, 2, "k", 1.8f, new Vector3(2.5f, 2.6f, 1.5f), new Vector3(0f, 0.2f, -1f)),
            S("ForkBonk", PawnRushSkillIds.ForkBonk, PieceKind.Knight, 0, "k", 3.4f, new Vector3(5f, 3f, 3f), new Vector3(0f, 1f, 5.5f), V(0.75f, 6f), V(-0.75f, 6.3f)),
            S("ForkOne", PawnRushSkillIds.ForkBonk, PieceKind.Knight, 0, "k", 2.8f, new Vector3(4.5f, 2.8f, 3f), new Vector3(0f, 1f, 5.5f), V(0.3f, 6f)),
            S("BackKick4", PawnRushSkillIds.BackKick4, PieceKind.Knight, 1, "k", 2.6f, new Vector3(4f, 1.8f, 0.3f), new Vector3(0f, 0.6f, 0f), V(0f, -1.6f), V(0.8f, -1.4f)),
            S("HolyWater4", PawnRushSkillIds.HolyWater4, PieceKind.Bishop, 0, "k", 3.4f, new Vector3(4f, 4f, 10f), new Vector3(0f, 0.4f, 14.5f), V(0f, 15f), V(1f, 15.6f)),
            S("Sermon", PawnRushSkillIds.Sermon, PieceKind.Bishop, 1, "k", 3.6f, new Vector3(3f, 2.2f, 4.5f), new Vector3(0f, 1.1f, 7f), V(0.5f, 7f)),
            S("Charge4", PawnRushSkillIds.Charge4, PieceKind.Rook, 0, "k", 2.6f, new Vector3(6f, 3.5f, 6f), new Vector3(0f, 0.6f, 7f), V(0.3f, 5f), V(-0.5f, 8.5f)),
            S("StoneCap", PawnRushSkillIds.StoneCap, PieceKind.Rook, 1, "k", 3.6f, new Vector3(2.6f, 2f, 1f), new Vector3(0f, 1.1f, 2.6f), V(0.2f, 2.6f)),
            S("CrownToss", PawnRushSkillIds.CrownToss, PieceKind.Queen, 0, "k", 3.6f, new Vector3(3.5f, 2.4f, 2.5f), new Vector3(0f, 1.1f, 5f), V(0.4f, 6f)),
            S("Insolent", PawnRushSkillIds.Insolent, PieceKind.Queen, 1, "k", 4.8f, new Vector3(1.5f, 2.6f, -3f), new Vector3(0f, 0.8f, 4f), V(0.5f, 3f), V(-1.5f, 5f), V(2f, 6.5f)),
            // The set 4판's bundle 나 (11-26).
            S("PoisonTrap", PawnRushSkillIds.PoisonPawn, PieceKind.Pawn, 0, "k w0.3 m-1,0 w0.7 m-", 2.4f, new Vector3(2.5f, 2f, 2.8f), new Vector3(0f, 0.3f, 0f)),
            S("Poison", PawnRushSkillIds.PoisonPawn, PieceKind.Pawn, 0, "k", 3.6f, new Vector3(2.8f, 2.2f, 2.6f), new Vector3(0f, 0.6f, 0.5f), V(0.6f, 0.6f)),
            S("PawnStorm", PawnRushSkillIds.PawnStorm, PieceKind.Pawn, 1, "k", 4.6f, new Vector3(5f, 3f, 3f), new Vector3(0f, 0.3f, 5f), V(0f, 6f), V(1.3f, 8f)),
            S("ButtSlam", PawnRushSkillIds.ButtSlam, PieceKind.Knight, 0, "k", 2.8f, new Vector3(4.5f, 2.6f, 0.5f), new Vector3(0f, 0.5f, -1.5f), V(0.3f, -3f)),
            S("Neigh", PawnRushSkillIds.Neigh, PieceKind.Knight, 1, "k", 3f, new Vector3(4f, 2.4f, 1.5f), new Vector3(0f, 0.8f, 3f), V(0f, 3.5f)),
            S("RainCloud", PawnRushSkillIds.RainCloud, PieceKind.Bishop, 0, "k", 4.2f, new Vector3(3f, 2.4f, 5f), new Vector3(0f, 1.4f, 8f), V(0.5f, 8f)),
            S("Censer", PawnRushSkillIds.Censer, PieceKind.Bishop, 1, "k m0,1 w2.6 m-", 4.6f, new Vector3(4.5f, 3f, 1f), new Vector3(0f, 0.4f, 3f), V(0.8f, 2.5f), V(-0.8f, 4.2f)),
            S("Bell", PawnRushSkillIds.Bell, PieceKind.Bishop, 0, "k", 4.4f, new Vector3(6f, 4f, 2f), new Vector3(0f, 3f, 7f), V(0.3f, 7f)),
            S("Crook", PawnRushSkillIds.Crook, PieceKind.Bishop, 1, "k", 3.2f, new Vector3(6f, 3f, 2.5f), new Vector3(0f, 1f, 2.5f), V(0f, 6f)),
            S("WallUp", PawnRushSkillIds.WallUp, PieceKind.Rook, 0, "k", 2.6f, new Vector3(4f, 2.8f, -4.5f), new Vector3(0f, 1f, -1.5f)),
            S("SideSlide", PawnRushSkillIds.SideSlide, PieceKind.Rook, 1, "k", 2.4f, new Vector3(1f, 3f, -4.5f), new Vector3(2f, 0.6f, 0f), V(2.2f, 0f)),
            S("Mower", PawnRushSkillIds.Mower, PieceKind.Rook, 1, "k m0,1 w2.8 m-", 3.8f, new Vector3(5f, 3f, 4f), new Vector3(0f, 0.8f, 4f), V(0.2f, 3f), V(-0.3f, 6f)),
            S("ScholarMate", PawnRushSkillIds.ScholarMate, PieceKind.Queen, 0, "k", 2.8f, new Vector3(4f, 2.4f, 2f), new Vector3(0f, 0.8f, 3.5f), V(0f, 4f)),
            S("PerpetualCheck", PawnRushSkillIds.PerpetualCheck, PieceKind.Queen, 1, "k", 3.8f, new Vector3(4.5f, 2.8f, 1.5f), new Vector3(0f, 0.8f, 3.5f), V(0.3f, 4f)),
            S("QueenSac4", PawnRushSkillIds.QueenSac4, PieceKind.Queen, 1, "k", 3.8f, new Vector3(0f, 6f, -6.5f), new Vector3(0f, 0.5f, 0f), V(2f, 1f), V(-2.5f, 2f), V(0.5f, -3f)),
            // The set 4판's bundle 다 (11-26). The skills aimed at the floor look a little down ("a0,1,-0.1", from a head
            // about 0.9 m up) to land about 9 m ahead, not at their whole reach (the queen's head is lower: "a0,1,-0.08").
            S("PromoCountdown", PawnRushSkillIds.PromoCountdown, PieceKind.Pawn, 0, "k", 6.6f, new Vector3(3.5f, 2.4f, -2f), new Vector3(0f, 1f, 1.5f), V(1.6f, 2.2f), V(-2.2f, 1.2f), V(0.5f, 3.8f)),
            S("Burrow", PawnRushSkillIds.Burrow, PieceKind.Knight, 0, "k m0,1 w1.1 k m-", 3.4f, new Vector3(6f, 3.5f, -2.5f), new Vector3(0f, 0.4f, 4f), V(0.3f, 7f), V(-0.8f, 7.4f)),
            S("SafetyPin", PawnRushSkillIds.SafetyPin, PieceKind.Bishop, 0, "k", 4.2f, new Vector3(4f, 2.3f, -0.5f), new Vector3(0f, 0.8f, 4f), V(0.2f, 6.5f)),
            S("RookDive", PawnRushSkillIds.RookDive, PieceKind.Rook, 0, "a0,1,-0.1 w0.1 k", 4.6f, new Vector3(8f, 4.5f, -1f), new Vector3(0f, 1f, 5.5f), V(0.8f, 9f), V(-1.2f, 9.5f)),
            S("Timber", PawnRushSkillIds.Timber, PieceKind.Rook, 1, "k", 4.8f, new Vector3(13f, 7f, 1f), new Vector3(0f, 3f, 4f), V(0.3f, 4f), V(-0.4f, 6.5f)),
            S("GatePig", PawnRushSkillIds.GatePig, PieceKind.Rook, 1, "k", 5.6f, new Vector3(6f, 3f, 1f), new Vector3(0f, 1f, 3f), V(0.5f, 4f), V(-1f, 6f)),
            S("RookLift", PawnRushSkillIds.RookLift, PieceKind.Rook, 0, "k", 4.4f, new Vector3(10f, 5f, -4f), new Vector3(0f, 2.2f, 0f), V(0.8f, 0.8f)),
            S("Procession", PawnRushSkillIds.Procession, PieceKind.Queen, 0, "k w0.6 m0,1 w1.4 m-", 4.4f, new Vector3(6f, 4f, -2.5f), new Vector3(0f, 0.3f, 7f), V(0.5f, 5f), V(-0.8f, 9f)),
            S("ThroneDrop", PawnRushSkillIds.ThroneDrop, PieceKind.Queen, 1, "a0,1,-0.08 w0.1 k", 3.8f, new Vector3(7f, 4.5f, 0f), new Vector3(0f, 1.2f, 6.5f), V(0.5f, 9f), V(-1.2f, 8.3f)),
            S("QueensGambit", PawnRushSkillIds.QueensGambit, PieceKind.Queen, 1, "a0,1,-0.07 w0.1 k", 4.2f, new Vector3(11f, 7f, 1f), new Vector3(0f, 3f, 10f), V(0.3f, 10f), V(2.8f, 12.8f)),
            // The user's own (JY, 11-26), bundle 1.
            S("StepsDiag", PawnRushSkillIds.StepsDiag, PieceKind.Pawn, 0, "k w0.9 k", 3.2f, new Vector3(5f, 3f, 0f), new Vector3(0f, 0.6f, 3f), V(1.2f, 4.2f)),
            S("RopePassant", PawnRushSkillIds.RopePassant, PieceKind.Pawn, 1, "k w0.3 c+ w1.05 c-", 4.6f, new Vector3(6.5f, 3.8f, -2.5f), new Vector3(0f, 0.8f, 4.5f), V(0.3f, 9.5f)),
            S("RopeMiss", PawnRushSkillIds.RopePassant, PieceKind.Pawn, 1, "k w0.3 c+ w0.25 c-", 3.2f, new Vector3(5f, 3f, 0f), new Vector3(0f, 0.6f, 3f)),
            S("JumpStomp", PawnRushSkillIds.JumpStomp, PieceKind.Knight, 0, "a0,1,-0.22 w0.1 k w0.75 k", 3.6f, new Vector3(6f, 4f, -1f), new Vector3(0f, 1.5f, 3.5f), V(0.3f, 4.5f), V(1.8f, 3.6f), V(-1.2f, 3.2f), V(-3.5f, 1f)),
            S("FlatPop", PawnRushSkillIds.JumpStomp, PieceKind.Knight, 1, "x10 w1 j w0.35 j w0.35 j w0.35 j w0.35 j", 4.2f, new Vector3(3f, 2f, 2.5f), new Vector3(0f, 0.3f, 0f)),
        };

        static readonly string[] SetThree = { "Bolas", "TwoSteps", "HighStomp", "BackKick", "HolySwords", "StoneThrow", "HolyWater", "RollShort", "TowerTrap", "QueenStar", "CheckChain" };
        static readonly string[] SetFourA = { "Trip4", "Brick", "Pit", "PitHole", "ForkBonk", "ForkOne", "BackKick4", "HolyWater4", "Sermon", "Charge4", "StoneCap", "CrownToss", "Insolent" };
        static readonly string[] SetFourB = { "PoisonTrap", "Poison", "PawnStorm", "ButtSlam", "Neigh", "RainCloud", "Censer", "Bell", "Crook", "WallUp", "SideSlide", "Mower", "ScholarMate", "PerpetualCheck", "QueenSac4" };
        static readonly string[] SetJY1 = { "StepsDiag", "RopePassant", "RopeMiss", "JumpStomp", "FlatPop" };
        static readonly string[] SetFourC = { "PromoCountdown", "Burrow", "SafetyPin", "RookDive", "Timber", "GatePig", "RookLift", "Procession", "ThroneDrop", "QueensGambit" };

        LabGame game;
        PawnRushSkillBed bed;
        Camera cam;
        RenderTexture rtBig, rtCell;
        Texture2D sheet, frame;
        Font font;
        PawnRushSkillFx.Text3D title, note, key;
        Transform banner, keyStrip;
        int keySerial;
        float keyLeft;
        Vector3 eye, look;
        bool rolling, grab;
        int cell, frameNo;
        string frameDir;

        /// <summary>Films <paramref name="names"/> (comma-separated shot names, or "set3") into <paramref name="folder"/>;
        /// <paramref name="done"/> when the last shot is written.</summary>
        public static void Begin(string folder, string names, Action done)
        {
            var list = new List<Shot>();
            foreach (string raw in (string.IsNullOrEmpty(names) ? "set3" : names).Split(','))
            {
                string n = raw.Trim();
                if (n == "set3" || n == "set4a" || n == "set4b" || n == "set4c" || n == "jy1")
                {
                    foreach (string m in n == "set3" ? SetThree : n == "set4a" ? SetFourA : n == "set4b" ? SetFourB : n == "jy1" ? SetJY1 : SetFourC) list.Add(Array.Find(Shots, s => s.name == m));
                    continue;
                }
                int at = Array.FindIndex(Shots, s => s.name == n);
                if (at >= 0) list.Add(Shots[at]);
                else Debug.LogWarning($"[FxSheet] no shot named {n}");
            }
            var go = new GameObject("Fx sheet film");
            DontDestroyOnLoad(go);
            var film = go.AddComponent<PawnRushFxSheet>();
            film.StartCoroutine(film.Main(folder, list, done));
        }

        IEnumerator Main(string folder, List<Shot> shots, Action done)
        {
            Status = "waiting for the scene";
            float waited = 0f;
            while (bed == null || game == null || game.players.Length == 0 || game.players[0].pawn == null)
            {
                bed = FindFirstObjectByType<PawnRushSkillBed>();
                game = bed != null ? bed.GetComponent<LabGame>() : null;
                waited += Time.unscaledDeltaTime;
                if (waited > 30f)
                {
                    Status = "done: no skill test scene";
                    Debug.LogError("[FxSheet] no PawnRushSkillBed with a P1 after 30 s");
                    done?.Invoke();
                    yield break;
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            Directory.CreateDirectory(folder);
            // The gauge's crowns turn round the test scene's start, where the shots are staged: out of the way.
            foreach (var crown in PawnRushGaugeCrown.All) if (crown != null) crown.gameObject.SetActive(false);

            cam = new GameObject("Fx sheet camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            if (game.labCamera != null) cam.CopyFrom(game.labCamera.Cam);
            cam.fieldOfView = 45f;
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.enabled = false;
            PawnRushSkillFx.PrepareCamera(cam);
            rtBig = new RenderTexture(FrameW, FrameH, 24) { antiAliasing = 4 };
            rtCell = new RenderTexture(CellW, CellH, 0);
            frame = new Texture2D(FrameW, FrameH, TextureFormat.RGB24, false);
            sheet = new Texture2D(Cols * CellW, (MaxFrames / Cols) * CellH, TextureFormat.RGB24, false);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 64);
            banner = Banner(cam.transform, 0.345f, 2f, 0.15f);
            keyStrip = Banner(cam.transform, -0.335f, 0.42f, 0.085f);
            keyStrip.gameObject.SetActive(false);
            PawnRushSkillFx.ViewOverride = cam;
            game.SuppressInput = true;
            LabCamera.GameTimeClock = true;
            Time.captureFramerate = Fps;

            var face = Face.normalized;
            var right = Vector3.Cross(Vector3.up, face);
            foreach (var shot in shots)
            {
                Status = "filming " + shot.name;
                // The skill in its slot (on top of 3판, or 4판 가 for a 4판 skill: their system plays every slot).
                var lo = bed.skills.loadout;
                int basis = PawnRushSkillIds.SetFour(shot.id) ? 13 : 12;
                lo.CopyFrom(PawnRushLoadout.Preset(basis));
                lo.Apply(PawnRushLoadout.OpSystem, PawnRushLoadout.SystemOf(basis));   // (a preset leaves the system as it was)
                int row = Array.IndexOf(PawnRushLoadout.RowPieces, shot.piece);
                if (row >= 0) lo.Apply(row * PawnRushLoadout.Slots + shot.slot, shot.id);
                string keyName = shot.slot == 0 ? "F" : shot.slot == 1 ? "E" : shot.slot == 2 ? "R" : "Q";
                SetCaption($"{PawnRushSkillBook.Name(shot.id)}  ·  {ChessPieces.Name(shot.piece)} {keyName}", Note);
                PawnRushSkillProbe.Perform(shot.piece, shot.slot, $"w{Lead.ToString(System.Globalization.CultureInfo.InvariantCulture)} {shot.script}", Ground, face, shot.enemies, shot.seconds + 0.5f);
                yield return null;
                while (!PawnRushSkillProbe.Staged && !PawnRushSkillProbe.Status.StartsWith("done")) yield return null;
                eye = Ground + right * shot.eye.x + Vector3.up * shot.eye.y + face * shot.eye.z;
                look = Ground + right * shot.look.x + Vector3.up * shot.look.y + face * shot.look.z;
                var clear = new Color32(13, 18, 31, 255);
                var fill = sheet.GetPixels32();
                for (int i = 0; i < fill.Length; i++) fill[i] = clear;
                sheet.SetPixels32(fill);
                frameDir = Path.Combine(folder, shot.name);
                Directory.CreateDirectory(frameDir);
                foreach (var old in Directory.GetFiles(frameDir, "f_*.jpg")) File.Delete(old);
                frameNo = 0;
                keySerial = PawnRushSkillProbe.HintSerial;
                keyLeft = 0f;
                int total = Mathf.Max(1, Mathf.RoundToInt((shot.seconds + Lead) * Fps));
                int step = Mathf.Max(1, Mathf.CeilToInt(total / (float)MaxFrames));
                cell = 0;
                rolling = true;
                for (int f = 0; f < total; f++)
                {
                    grab = f % step == 0 && cell < MaxFrames;
                    yield return null;
                }
                rolling = grab = false;
                sheet.Apply();
                string path = Path.Combine(folder, shot.name + ".jpg");
                File.WriteAllBytes(path, sheet.EncodeToJPG(88));
                Debug.Log($"[FxSheet] {shot.name}: {frameNo} frames in {frameDir}, sheet {cell} frames → {path}");
                // Let the last shot's things (a puddle, a tower, the swords' last wave) end before the next is staged.
                yield return new WaitForSeconds(2.5f);
            }
            Status = "done";
            Clean();
            done?.Invoke();
        }

        void LateUpdate()
        {
            if (cam == null) return;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
            if (!rolling) return;
            title?.Place(cam.transform.TransformPoint(new Vector3(0f, 0.37f, 1f)), cam, 1f, 1f);
            note?.Place(cam.transform.TransformPoint(new Vector3(0f, 0.313f, 1f)), cam, 1f, 1f);
            ShowKey();
            cam.targetTexture = rtBig;
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rtBig;
            frame.ReadPixels(new Rect(0, 0, FrameW, FrameH), 0, 0);
            frame.Apply();
            File.WriteAllBytes(Path.Combine(frameDir, $"f_{frameNo:D4}.jpg"), frame.EncodeToJPG(90));
            frameNo++;
            if (grab)
            {
                Graphics.Blit(rtBig, rtCell);
                RenderTexture.active = rtCell;
                int col = cell % Cols, rowFromTop = cell / Cols;
                sheet.ReadPixels(new Rect(0, 0, CellW, CellH), col * CellW, (MaxFrames / Cols - 1 - rowFromTop) * CellH);
                cell++;
                grab = false;
            }
            RenderTexture.active = null;
        }

        void SetCaption(string a, string b)
        {
            title?.Destroy();
            note?.Destroy();
            title = new PawnRushSkillFx.Text3D(cam.transform, font, a, Color.white, 0.056f);
            note = new PawnRushSkillFx.Text3D(cam.transform, font, string.IsNullOrEmpty(b) ? " " : b, new Color(1f, 0.9f, 0.6f), 0.034f);
        }

        /// <summary>The key the probe has just pressed, for a moment at the bottom of the picture.</summary>
        void ShowKey()
        {
            if (PawnRushSkillProbe.HintSerial != keySerial)
            {
                keySerial = PawnRushSkillProbe.HintSerial;
                key?.Destroy();
                key = new PawnRushSkillFx.Text3D(cam.transform, font, $"[ {PawnRushSkillProbe.Hint} ]", new Color(1f, 0.85f, 0.3f), 0.05f);
                keyLeft = 0.8f;
            }
            keyLeft -= 1f / Fps;
            bool on = keyLeft > 0f && key != null;
            keyStrip.gameObject.SetActive(on);
            key?.Place(cam.transform.TransformPoint(new Vector3(0f, -0.335f, 1f)), cam, on ? 1f : 0.001f, Mathf.Clamp01(keyLeft / 0.3f));
        }

        /// <summary>A dark strip across the picture at <paramref name="y"/> (the caption's at the top, the key's at the bottom).</summary>
        static Transform Banner(Transform parent, float y, float width, float height)
        {
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
            };
            var go = new GameObject("Caption strip");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 1.05f);
            go.transform.localScale = new Vector3(width, height, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.05f, 0.08f, 0.19f, 0.72f) };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void Clean()
        {
            Time.captureFramerate = 0;
            LabCamera.GameTimeClock = false;
            if (game != null) game.SuppressInput = false;
            if (PawnRushSkillFx.ViewOverride == cam) PawnRushSkillFx.ViewOverride = null;
            foreach (var crown in PawnRushGaugeCrown.All) if (crown != null) crown.gameObject.SetActive(true);
            title?.Destroy();
            note?.Destroy();
            key?.Destroy();
            if (rtBig != null) Destroy(rtBig);
            if (rtCell != null) Destroy(rtCell);
            if (sheet != null) Destroy(sheet);
            if (frame != null) Destroy(frame);
        }

        void OnDestroy()
        {
            if (Time.captureFramerate == Fps) Clean();
        }
    }
}
