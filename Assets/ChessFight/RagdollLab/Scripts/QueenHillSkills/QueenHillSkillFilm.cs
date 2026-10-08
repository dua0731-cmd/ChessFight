using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Films the Queen of the Hill skills for review (R89): plays the probe's runs from a fixed camera per shot, steps game
    /// time 1/fps per frame (Time.captureFramerate) so the video plays at true speed however long a frame takes, and hands
    /// every frame to whoever listens (in the editor QueenHillSkillFilmEncoder writes the .mp4). Two films in one go:
    /// every shot at full speed, then every shot again in the lab's slow motion (×0.3) from the moment it is staged.
    /// Every StillEvery-th frame is kept as a .jpg next to each video (1 = every frame, to look at an effect frame by
    /// frame). A caption at the top names the skill, the key the probe presses shows at the bottom (only in the film).
    /// </summary>
    [DefaultExecutionOrder(300)]   // after the effects shake the camera (210)
    public class QueenHillSkillFilm : MonoBehaviour
    {
        public static event Action<string, int, int, int> Began;
        public static event Action<Texture2D> Frame;
        public static event Action Ended;
        public static string Status { get; private set; } = "idle";
        public static int Frames { get; private set; }
        public static int StillEvery = 10;
        /// <summary>A film is recording this frame (the bed hides its panel).</summary>
        public static bool Rolling { get; private set; }
        /// <summary>Per shot: the frame its takes began on, in each film (to find a shot among the stills).</summary>
        public static readonly List<string> Index = new List<string>();

        struct Shot
        {
            public string run, title, note;
            public Vector3 eye, look;
            public float fov;
        }

        static readonly Shot[] Shots =
        {
            new Shot { run = "king", title = "1. 킹 · 근접 호위 (B: 킹 자신은 빠짐)", note = "에메랄드 물결 → 아군 발밑 호위 고리 · 적 퀸 검격에 아군은 버티고(초록 고리) 킹만 넘어짐",
                eye = new Vector3(4.3f, 4.6f, -9.4f), look = new Vector3(3.6f, 0.4f, -2.4f), fov = 42f },
            new Shot { run = "queen", title = "2. 퀸 · 팔방 검격 (A: 한 방향 긴 검격)", note = "체스판 4칸이 차오름 → 금색 검기가 날아감 · 줄 위 적 넘어짐 · 벽에 막혀 뒤는 안전",
                eye = new Vector3(3.4f, 4.4f, -3.6f), look = new Vector3(3.6f, 0.4f, 3.4f), fov = 44f },
            new Shot { run = "rook", title = "3. 룩 · 캐슬링 교대 (B: 아군 누구나, 수락 받고)", note = "아군 폰을 골라 요청 → 수락(시계가 참) → 함정문 열리고 두 색 리본 아치로 자리 바꿈",
                eye = new Vector3(2.6f, 4.8f, -2.8f), look = new Vector3(-4.2f, 1.0f, 4.9f), fov = 44f },
            new Shot { run = "bishop", title = "4. 비숍 · 교차 공중 포격 (B: 맞으면 밀려 넘어짐)", note = "보라 마름모 칸 위로 떠오름 → 돌 던짐 → X자 파동 → 벽을 오르던 적이 떨어짐",
                eye = new Vector3(4.2f, 3.6f, -3.6f), look = new Vector3(-2.0f, 1.0f, 3.4f), fov = 46f },
            new Shot { run = "knight", title = "5. 나이트 · 도약 압착 (B: 한 층까지, 높은 곳에서 납작하면 떨어짐)", note = "2층은 너무 높아 회색 → 1층 말굽 표식 → 도약 → 적이 납작해져 가장자리에서 떨어짐",
                eye = new Vector3(2.4f, 3.8f, 13.6f), look = new Vector3(-3.4f, 0.9f, 7.3f), fov = 44f },
            new Shot { run = "pawn", title = "6. 폰 · 비집고 돌파 (B: 몸을 낮추는 새 동작)", note = "적 룩 돌진 경고(빨간 줄무늬 칸) → 다리를 앞뒤로 벌리고 낮게 옆으로 빠짐 → 룩이 빗나감",
                eye = new Vector3(2.8f, 2.6f, -8.6f), look = new Vector3(3.2f, 0.4f, -2.6f), fov = 44f },
            new Shot { run = "pawn-squeeze", title = "6. 폰 · 비집고 돌파 (좁은 틈)", note = "낮게 몸을 숙이고 두 적 사이를 비집고 지나감 · 무적 없음, 부딪힌 적은 옆으로 비킴",
                eye = new Vector3(-1.4f, 2.4f, -9.0f), look = new Vector3(0.6f, 0.3f, -5f), fov = 40f },
        };

        LabGame game;
        Camera cam;
        RenderTexture rt;
        Texture2D frame;
        Font font;
        PawnRushSkillFx.Text3D title, note, key;
        Transform banner, keyStrip;
        int keySerial;
        float keyLeft;
        Vector3 eye, look;
        string stills;
        int width, height;

        /// <summary>Film every shot (or only those whose run starts with <paramref name="only"/>) into
        /// <paramref name="path"/> (full speed) and the same name + "_slow" (slow motion). Needs Play mode in
        /// QueenOfTheHill_SkillTest.</summary>
        public static string Run(string path, int width = 1280, int height = 720, int fps = 60, string only = null, bool slow = true)
        {
            var bed = FindFirstObjectByType<QueenHillSkillBed>();
            if (bed == null) return "no QueenHillSkillBed (open QueenOfTheHill_SkillTest and press Play)";
            var film = bed.GetComponent<QueenHillSkillFilm>();
            if (film == null) film = bed.gameObject.AddComponent<QueenHillSkillFilm>();
            film.StopAllCoroutines();
            film.StartCoroutine(film.Main(path, width, height, fps, only, slow));
            return Status = "starting";
        }

        IEnumerator Main(string path, int w, int h, int fps, string only, bool slowToo)
        {
            game = GetComponent<LabGame>();
            width = w;
            height = h;
            var go = new GameObject("QotH skill film camera");
            cam = go.AddComponent<Camera>();
            if (game != null && game.labCamera != null) cam.CopyFrom(game.labCamera.Cam);
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.enabled = false;
            PawnRushSkillFx.PrepareCamera(cam);
            rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            frame = new Texture2D(w, h, TextureFormat.RGBA32, false);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 64);
            title = new PawnRushSkillFx.Text3D(go.transform, font, " ", Color.white, TitleSize);
            note = new PawnRushSkillFx.Text3D(go.transform, font, " ", new Color(1f, 0.9f, 0.6f), NoteSize);
            banner = Banner(go.transform, 0.345f);
            keyStrip = Banner(go.transform, -0.335f, 0.5f);
            keyStrip.gameObject.SetActive(false);
            key = new PawnRushSkillFx.Text3D(go.transform, font, " ", Color.white, KeySize);

            QueenHillSkillFx.ViewOverride = cam;
            if (game != null) game.SuppressInput = true;
            Time.captureFramerate = fps;
            LabCamera.GameTimeClock = true;
            Index.Clear();
            foreach (bool slow in slowToo ? new[] { false, true } : new[] { false })
            {
                string file = slow ? Path.Combine(Path.GetDirectoryName(path) ?? ".", Path.GetFileNameWithoutExtension(path) + "_slow.mp4") : path;
                stills = Path.Combine(Path.GetDirectoryName(file) ?? ".", Path.GetFileNameWithoutExtension(file) + "_stills");
                Directory.CreateDirectory(stills);
                Frames = 0;
                Began?.Invoke(file, w, h, fps);
                foreach (var shot in Shots)
                {
                    if (!string.IsNullOrEmpty(only) && !shot.run.StartsWith(only)) continue;
                    Status = $"filming {shot.run}{(slow ? " (slow)" : "")}";
                    eye = shot.eye;
                    look = shot.look;
                    cam.fieldOfView = shot.fov;
                    SetCaption(shot.title, slow ? $"느리게 ×{(game != null ? game.slowMotionScale : 0.3f):0.0#} · {shot.note}" : shot.note);
                    if (game != null) game.SetSlowMotion(false);
                    QueenHillSkillProbe.Run(shot.run);
                    yield return null;
                    while (!QueenHillSkillProbe.Staged && !Done) yield return null;
                    if (slow && game != null) game.SetSlowMotion(true);
                    Index.Add($"{(slow ? "slow" : "full")} {shot.run} from frame {Frames}");
                    keySerial = QueenHillSkillProbe.HintSerial;
                    Rolling = true;
                    while (!Done) yield return null;
                    Rolling = false;
                    if (game != null) game.SetSlowMotion(false);
                }
                Ended?.Invoke();
            }
            if (game != null) game.SuppressInput = false;
            Time.captureFramerate = 0;
            LabCamera.GameTimeClock = false;
            QueenHillSkillFx.ViewOverride = null;
            Destroy(go);
            Destroy(rt);
            Destroy(frame);
            Status = $"done {Frames} frames → {path}";
        }

        static bool Done => QueenHillSkillProbe.Status.StartsWith("done");

        void SetCaption(string a, string b)
        {
            title.Destroy();
            note.Destroy();
            title = new PawnRushSkillFx.Text3D(cam.transform, font, a, Color.white, TitleSize);
            note = new PawnRushSkillFx.Text3D(cam.transform, font, b, new Color(1f, 0.9f, 0.6f), NoteSize);
        }

        const float TitleSize = 0.05f, NoteSize = 0.03f, KeySize = 0.046f;

        static Transform Banner(Transform cam, float y, float width = 2f)
        {
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
            };
            var go = new GameObject("Caption strip");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0f, y, 1.05f);
            go.transform.localScale = new Vector3(width, y > 0f ? 0.15f : 0.085f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.05f, 0.08f, 0.19f, 0.72f) };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            if (Rolling)
            {
                title.Place(cam.transform.TransformPoint(new Vector3(0f, 0.37f, 1f)), cam, 1f, 1f);
                note.Place(cam.transform.TransformPoint(new Vector3(0f, 0.313f, 1f)), cam, 1f, 1f);
                ShowKey();
                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.active = rt;
                frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                frame.Apply();
                RenderTexture.active = null;
                Frame?.Invoke(frame);
                if (StillEvery > 0 && Frames % StillEvery == 0) File.WriteAllBytes(Path.Combine(stills, $"f_{Frames:D4}.jpg"), frame.EncodeToJPG(85));
                Frames++;
            }
            // Back to the shot's own pose: the effects shake it from there again next frame.
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
        }

        void ShowKey()
        {
            if (QueenHillSkillProbe.HintSerial != keySerial)
            {
                keySerial = QueenHillSkillProbe.HintSerial;
                key.Destroy();
                key = new PawnRushSkillFx.Text3D(cam.transform, font, $"[ {QueenHillSkillProbe.Hint} ]", new Color(1f, 0.85f, 0.3f), KeySize);
                keyLeft = 1.1f;
            }
            keyLeft -= Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
            bool on = keyLeft > 0f;
            keyStrip.gameObject.SetActive(on);
            key.Place(cam.transform.TransformPoint(new Vector3(0f, -0.335f, 1f)), cam, on ? 1f : 0.001f, Mathf.Clamp01(keyLeft / 0.3f));
        }

        void OnDestroy()
        {
            Rolling = false;
            if (Time.captureFramerate != 0) Time.captureFramerate = 0;
            LabCamera.GameTimeClock = false;
            if (QueenHillSkillFx.ViewOverride == cam) QueenHillSkillFx.ViewOverride = null;
        }
    }
}
