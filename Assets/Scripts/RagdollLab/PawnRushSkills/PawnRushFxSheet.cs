using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Contact sheets of the skills' effects, to look at them frame by frame with nobody at the keys (2026-11-24; the user:
    /// "이전에 구현했던 스킬들도 이펙트를 제대로 만들어줘 ... 웃기고 코믹한 느낌이되 너무 허접하진 않고 퀄리티 좋게"). In the
    /// skill test scene's Play mode each shot puts one skill in a slot of P1's piece, stages P1 and enemy dummies and plays
    /// a few keys (PawnRushSkillProbe.Perform), and keeps every few frames of game time (30 a second, Time.captureFramerate,
    /// so the picture is the same however slow the machine) from a fixed camera: one JPG a shot, four frames across, left
    /// to right and down. Batch: Tools/Film-SkillFx.ps1 (PawnRushFxSheetBatch). In the editor while playing:
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
            /// <summary>Filmed from when the stage has settled.</summary>
            public float seconds;
            /// <summary>The camera, in P1's frame (x right, y up, z ahead).</summary>
            public Vector3 eye, look;
        }

        public static string Status { get; private set; } = "idle";
        const int Cols = 4, MaxFrames = 24, CellW = 480, CellH = 270, Fps = 30;
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
            S("HolySwords", PawnRushSkillIds.HolySwords, PieceKind.Bishop, 0, "k", 3f, new Vector3(0f, 6f, -3f), new Vector3(0f, 0f, 5.5f), V(-2.5f, 6f), V(2.5f, 8f), V(3.5f, 3.5f)),
            S("StoneThrow", PawnRushSkillIds.StoneThrow, PieceKind.Bishop, 1, "k", 2.8f, new Vector3(4f, 2.8f, -1.5f), new Vector3(0f, 0.6f, 6f), V(0f, 6f), V(0.7f, 10f)),
            S("HolyWater", PawnRushSkillIds.HolyWater, PieceKind.Bishop, 2, "k", 2.6f, new Vector3(3.5f, 3f, 2f), new Vector3(0f, 0.4f, 7.5f), V(0f, 8f), V(1f, 8.8f)),
            S("RollShort", PawnRushSkillIds.RollShort, PieceKind.Rook, 0, "k", 3f, new Vector3(5f, 3f, 3f), new Vector3(0f, 0.5f, 8f), V(0f, 7f), V(0.6f, 10f), V(-0.6f, 10.5f)),
            S("TowerTrap", PawnRushSkillIds.TowerTrap, PieceKind.Rook, 1, "k", 2.6f, new Vector3(4f, 2.8f, 2f), new Vector3(0f, 0.8f, 6.5f), V(0f, 7f)),
            S("QueenStar", PawnRushSkillIds.QueenStar, PieceKind.Queen, 0, "k", 2f, new Vector3(0f, 9f, -5.5f), new Vector3(0f, 0f, 0.5f), V(3f, 3f), V(-4f, 2f), V(0.5f, -5f), V(5f, -2f)),
            S("CheckChain", PawnRushSkillIds.CheckChain, PieceKind.Queen, 1, "k w0.6 k w0.6 k", 2.6f, new Vector3(6f, 5f, 1f), new Vector3(0f, 0.5f, 8f), V(0f, 6f), V(4f, 10f), V(-2f, 13f)),
        };

        static readonly string[] SetThree = { "Bolas", "TwoSteps", "HighStomp", "BackKick", "HolySwords", "StoneThrow", "HolyWater", "RollShort", "TowerTrap", "QueenStar", "CheckChain" };

        LabGame game;
        PawnRushSkillBed bed;
        Camera cam;
        RenderTexture rt;
        Texture2D sheet;
        Vector3 eye, look;
        bool grab;
        int cell;

        /// <summary>Films <paramref name="names"/> (comma-separated shot names, or "set3") into <paramref name="folder"/>;
        /// <paramref name="done"/> when the last sheet is written.</summary>
        public static void Begin(string folder, string names, Action done)
        {
            var list = new List<Shot>();
            foreach (string raw in (string.IsNullOrEmpty(names) ? "set3" : names).Split(','))
            {
                string n = raw.Trim();
                if (n == "set3")
                {
                    foreach (string m in SetThree) list.Add(Array.Find(Shots, s => s.name == m));
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

            cam = new GameObject("Fx sheet camera").AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            if (game.labCamera != null) cam.CopyFrom(game.labCamera.Cam);
            cam.fieldOfView = 50f;
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.enabled = false;
            PawnRushSkillFx.PrepareCamera(cam);
            rt = new RenderTexture(CellW, CellH, 24) { antiAliasing = 4 };
            sheet = new Texture2D(Cols * CellW, (MaxFrames / Cols) * CellH, TextureFormat.RGB24, false);
            PawnRushSkillFx.ViewOverride = cam;
            game.SuppressInput = true;
            LabCamera.GameTimeClock = true;
            Time.captureFramerate = Fps;

            var face = Face.normalized;
            var right = Vector3.Cross(Vector3.up, face);
            foreach (var shot in shots)
            {
                Status = "filming " + shot.name;
                // The skill in its slot (on top of 3판, whose system plays every slot).
                var lo = bed.skills.loadout;
                lo.CopyFrom(PawnRushLoadout.Preset(12));
                lo.Apply(PawnRushLoadout.OpSystem, PawnRushLoadout.SystemOf(12));   // (a preset leaves the system as it was)
                int row = Array.IndexOf(PawnRushLoadout.RowPieces, shot.piece);
                if (row >= 0) lo.Apply(row * PawnRushLoadout.Slots + shot.slot, shot.id);
                PawnRushSkillProbe.Perform(shot.piece, shot.slot, shot.script, Ground, face, shot.enemies, shot.seconds + 0.5f);
                yield return null;
                while (!PawnRushSkillProbe.Staged && !PawnRushSkillProbe.Status.StartsWith("done")) yield return null;
                eye = Ground + right * shot.eye.x + Vector3.up * shot.eye.y + face * shot.eye.z;
                look = Ground + right * shot.look.x + Vector3.up * shot.look.y + face * shot.look.z;
                var clear = new Color32(13, 18, 31, 255);
                var fill = sheet.GetPixels32();
                for (int i = 0; i < fill.Length; i++) fill[i] = clear;
                sheet.SetPixels32(fill);
                int total = Mathf.Max(1, Mathf.RoundToInt(shot.seconds * Fps));
                int step = Mathf.Max(1, Mathf.CeilToInt(total / (float)MaxFrames));
                cell = 0;
                for (int f = 0; f < total && cell < MaxFrames; f++)
                {
                    grab = f % step == 0;
                    yield return null;
                }
                grab = false;
                sheet.Apply();
                string path = Path.Combine(folder, shot.name + ".jpg");
                File.WriteAllBytes(path, sheet.EncodeToJPG(88));
                Debug.Log($"[FxSheet] {shot.name}: {cell} frames, one each {step / (float)Fps:0.###} s → {path}");
                // Let the last shot's things (a puddle, a tower) end before the next is staged.
                yield return new WaitForSeconds(0.6f);
            }
            Status = "done";
            Clean();
            done?.Invoke();
        }

        void LateUpdate()
        {
            if (cam == null) return;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
            if (!grab || cell >= MaxFrames) return;
            grab = false;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rt;
            int col = cell % Cols, rowFromTop = cell / Cols;
            sheet.ReadPixels(new Rect(0, 0, CellW, CellH), col * CellW, (MaxFrames / Cols - 1 - rowFromTop) * CellH);
            RenderTexture.active = null;
            cell++;
        }

        void Clean()
        {
            Time.captureFramerate = 0;
            LabCamera.GameTimeClock = false;
            if (game != null) game.SuppressInput = false;
            if (PawnRushSkillFx.ViewOverride == cam) PawnRushSkillFx.ViewOverride = null;
            if (rt != null) Destroy(rt);
            if (sheet != null) Destroy(sheet);
        }

        void OnDestroy()
        {
            if (Time.captureFramerate == Fps) Clean();
        }
    }
}
