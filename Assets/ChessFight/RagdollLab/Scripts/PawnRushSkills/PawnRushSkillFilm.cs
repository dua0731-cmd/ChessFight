using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Films the skills and their hit effects for review (Docs/Skills/README.md): plays probe runs in the skill
    /// test scene from a fixed camera per shot, steps game time 1/fps per frame (Time.captureFramerate) so the
    /// video plays at true speed however long a frame takes, and hands every frame to whoever listens: in the
    /// editor, PawnRushSkillFilmEncoder writes them into an .mp4. Each shot plays at full speed, then again in
    /// the lab's slow motion around its hits only (the moments the full-speed take saw). Every tenth frame
    /// (StillEvery) is also kept as a .jpg next to the video.
    /// </summary>
    [DefaultExecutionOrder(300)]   // after the effects shake the camera (210)
    public class PawnRushSkillFilm : MonoBehaviour
    {
        public static event Action<string, int, int, int> Began;
        public static event Action<Texture2D> Frame;
        public static event Action Ended;
        public static string Status { get; private set; } = "idle";
        public static int Frames { get; private set; }
        public static int StillEvery = 10;
        /// <summary>Film each shot again slowed round its hits (false: the full-speed takes only, e.g. to keep every
        /// frame as a still and look at an effect frame by frame, R86).</summary>
        public static bool SlowTakes = true;
        /// <summary>The frame numbers the hits fell on in the last film (to find them among the stills).</summary>
        public static readonly List<int> HitFrames = new List<int>();

        struct Shot
        {
            public string run, title, note;
            public Vector3 eye, look;
            /// <summary>How long before its first hit the slowed take starts (game seconds).</summary>
            public float lead;
        }

        // Where each probe run happens (PawnRushSkillProbe) and a camera that sees all of it.
        static readonly Shot[] Shots =
        {
            // R90: every Pawn Rush skill in design A.
            new Shot { run = "pawn-angles", title = "폰 · 첫 두 걸음", note = "밟은 칸이 우리 색으로 찍힘 · 곧게 밀면 C자 고리 · 대각선으로 치면 넘어지고 잡힌 칸",
                eye = new Vector3(2.8f, 2.6f, -11.6f), look = new Vector3(0f, 0.4f, -7f), lead = 0.4f },
            new Shot { run = "pawn-help", title = "폰 · 일으켜 세우기", note = "넘어진 팀원 칸이 솟아 들어 올림 · 둘 다 발에 청록 띠",
                eye = new Vector3(2.8f, 2.6f, -11.6f), look = new Vector3(0f, 0.4f, -7f), lead = 0.4f },
            new Shot { run = "queen", title = "퀸 · 팔방 밀치기", note = "바닥 경고 원이 차오름 · 금색 체스말이 가슴 불덩이로 모였다가 팡 → 진주 8개 왕관 고리",
                eye = new Vector3(2.4f, 3.6f, -13f), look = new Vector3(-0.2f, 0.3f, -7.8f), lead = 0.5f },
            new Shot { run = "rook-cluster", title = "룩 · 직선 돌파", note = "앞으로 네 칸이 차례로 켜짐 · 잠그면 돌 먼지 고리 · 번개 돌진 → 맞을 때마다 C자 고리, 잡힌 칸",
                eye = new Vector3(-2.8f, 3.8f, -14.6f), look = new Vector3(-6.8f, 0.2f, -9.6f), lead = 0.4f },
            new Shot { run = "rook-wall", title = "룩 · 벽에 쾅", note = "마지막 칸이 벽에 세워짐 · 벽에 성벽 톱니 고리 · 돌 먼지",
                eye = new Vector3(16.2f, 2.6f, -5.2f), look = new Vector3(19.2f, 0.9f, 0f), lead = 0.4f },
            new Shot { run = "bishop-trip", title = "비숍 · 교차 밧줄", note = "보라 작은 칸으로 조준 · X를 던짐 · 걸리면 발밑 보라 초승달과 잡힌 칸 · 쉬는 동안 40% 밝기",
                eye = new Vector3(-3.6f, 4.4f, -13f), look = new Vector3(0f, 0.2f, -7.3f), lead = 0.45f },
            new Shot { run = "knight-turn", title = "나이트 · 꺾어 도약", note = "가는 길 칸과 말굽 착지 표시(내려올수록 작아짐) · 공중에서 꺾으면 ㄱ자 바람 띠와 파란 초승달",
                eye = new Vector3(3.6f, 6f, -18.2f), look = new Vector3(3.4f, 0.4f, -10.4f), lead = 0.45f },
            new Shot { run = "knight-stomp", title = "나이트 · 머리 밟기", note = "적 발밑 빨간 고리 → 밟으면 납작 · 파란 고리 · 잡힌 칸 · 머리 위 작은 크림 폰 3개",
                eye = new Vector3(7f, 2.8f, -11f), look = new Vector3(1f, 1f, -9.2f), lead = 0.5f },
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
        bool rolling, clocking;
        float shotClock;
        readonly List<float> hits = new List<float>();
        string stills;
        int width, height;

        /// <summary>Film every shot (or only the runs whose names start with <paramref name="only"/>) into
        /// <paramref name="path"/>. Needs Play mode in PawnRush_SkillTest.</summary>
        public static string Run(string path, int width = 1280, int height = 720, int fps = 60, string only = null)
        {
            var bed = FindFirstObjectByType<PawnRushSkillBed>();
            if (bed == null) return "no PawnRushSkillBed (open PawnRush_SkillTest and press Play)";
            var film = bed.GetComponent<PawnRushSkillFilm>();
            if (film == null) film = bed.gameObject.AddComponent<PawnRushSkillFilm>();
            film.StopAllCoroutines();
            film.StartCoroutine(film.Main(path, width, height, fps, only));
            return Status = "starting";
        }

        IEnumerator Main(string path, int w, int h, int fps, string only)
        {
            game = GetComponent<LabGame>();
            width = w;
            height = h;
            var go = new GameObject("Skill film camera");
            cam = go.AddComponent<Camera>();
            if (game != null && game.labCamera != null) cam.CopyFrom(game.labCamera.Cam);
            cam.fieldOfView = 45f;
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.enabled = false;
            PawnRushSkillFx.PrepareCamera(cam);   // depth for soft edges, HDR and bloom (R79)
            rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            frame = new Texture2D(w, h, TextureFormat.RGBA32, false);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 64);
            title = new PawnRushSkillFx.Text3D(go.transform, font, " ", Color.white, TitleSize);
            note = new PawnRushSkillFx.Text3D(go.transform, font, " ", new Color(1f, 0.9f, 0.6f), NoteSize);
            banner = Banner(go.transform, 0.345f);
            keyStrip = Banner(go.transform, -0.335f, 0.42f);
            keyStrip.gameObject.SetActive(false);
            key = new PawnRushSkillFx.Text3D(go.transform, font, " ", Color.white, KeySize);
            keySerial = PawnRushSkillProbe.HintSerial;
            stills = Path.Combine(Path.GetDirectoryName(path) ?? ".", Path.GetFileNameWithoutExtension(path) + "_stills");
            Directory.CreateDirectory(stills);

            PawnRushSkillFx.ViewOverride = cam;
            // Keys pressed in the Game view while it films would play into the takes.
            if (game != null) game.SuppressInput = true;
            Time.captureFramerate = fps;
            LabCamera.GameTimeClock = true;
            Frames = 0;
            HitFrames.Clear();
            Began?.Invoke(path, w, h, fps);
            RagdollPawn.SkillFx += OnSkillFx;
            foreach (var shot in Shots)
            {
                if (!string.IsNullOrEmpty(only) && !shot.run.StartsWith(only)) continue;
                float first = 0.5f, last = 1.5f;
                foreach (bool slow in SlowTakes ? new[] { false, true } : new[] { false })
                {
                    Status = $"filming {shot.run}{(slow ? " (slow)" : "")}";
                    eye = shot.eye;
                    look = shot.look;
                    SetCaption(shot.title, slow ? $"느리게 ×{(game != null ? game.slowMotionScale : 0.3f):0.0#}" : shot.note);
                    if (game != null) game.SetSlowMotion(false);
                    // A bishop's wire from the shot before would still be lying there.
                    foreach (var wire in FindObjectsByType<SkillTripwire>(FindObjectsSortMode.None)) Destroy(wire.gameObject);
                    PawnRushSkillProbe.Run(shot.run);
                    yield return null;
                    while (!PawnRushSkillProbe.Staged && !Done) yield return null;
                    shotClock = 0f;
                    hits.Clear();
                    clocking = true;
                    if (!slow)
                    {
                        rolling = true;
                        while (!Done) yield return null;
                        rolling = false;
                        if (hits.Count > 0) { first = hits[0]; last = hits[hits.Count - 1]; }
                    }
                    else
                    {
                        // Unfilmed up to just before the first hit, slowed and filmed until just after the last.
                        while (!Done && shotClock < first - shot.lead) yield return null;
                        if (game != null) game.SetSlowMotion(true);
                        rolling = true;
                        while (!Done && shotClock < last + 0.75f) yield return null;
                        rolling = false;
                        if (game != null) game.SetSlowMotion(false);
                        while (!Done) yield return null;
                    }
                    clocking = false;
                }
            }
            RagdollPawn.SkillFx -= OnSkillFx;
            Ended?.Invoke();
            if (game != null) game.SuppressInput = false;
            Time.captureFramerate = 0;
            LabCamera.GameTimeClock = false;
            PawnRushSkillFx.ViewOverride = null;
            Destroy(go);
            Destroy(rt);
            Destroy(frame);
            Status = $"done {Frames} frames → {path}";
        }

        static bool Done => PawnRushSkillProbe.Status.StartsWith("done");

        void OnSkillFx(SkillFxEvent e)
        {
            if (clocking && e.kind != SkillFxKind.BishopWire) hits.Add(shotClock);
            if (rolling) HitFrames.Add(Frames);
        }

        void Update()
        {
            if (clocking) shotClock += Time.deltaTime;
        }

        void SetCaption(string a, string b)
        {
            title.Destroy();
            note.Destroy();
            title = new PawnRushSkillFx.Text3D(cam.transform, font, a, Color.white, TitleSize);
            note = new PawnRushSkillFx.Text3D(cam.transform, font, b, new Color(1f, 0.9f, 0.6f), NoteSize);
        }

        const float TitleSize = 0.056f, NoteSize = 0.034f, KeySize = 0.05f;

        /// <summary>A dark strip across the picture at <paramref name="y"/> (the captions at the top, the key
        /// just pressed at the bottom).</summary>
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
            if (!rolling)
            {
                // Keys pressed off camera (before a slowed take begins) are not shown when it begins.
                keySerial = PawnRushSkillProbe.HintSerial;
                keyLeft = 0f;
            }
            if (rolling)
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

        /// <summary>The key the probe has just pressed, for a moment at the bottom of the picture.</summary>
        void ShowKey()
        {
            if (PawnRushSkillProbe.HintSerial != keySerial)
            {
                keySerial = PawnRushSkillProbe.HintSerial;
                key.Destroy();
                key = new PawnRushSkillFx.Text3D(cam.transform, font, $"[ {PawnRushSkillProbe.Hint} ]", new Color(1f, 0.85f, 0.3f), KeySize);
                keyLeft = 1.1f;
            }
            keyLeft -= Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
            bool on = keyLeft > 0f;
            keyStrip.gameObject.SetActive(on);
            key.Place(cam.transform.TransformPoint(new Vector3(0f, -0.335f, 1f)), cam, on ? 1f : 0.001f, Mathf.Clamp01(keyLeft / 0.3f));
        }

        void OnDestroy()
        {
            RagdollPawn.SkillFx -= OnSkillFx;
            if (Time.captureFramerate != 0) Time.captureFramerate = 0;
            LabCamera.GameTimeClock = false;
            if (PawnRushSkillFx.ViewOverride == cam) PawnRushSkillFx.ViewOverride = null;
        }
    }
}
