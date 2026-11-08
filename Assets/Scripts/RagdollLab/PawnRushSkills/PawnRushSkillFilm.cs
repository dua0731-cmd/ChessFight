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
        /// <summary>R139 (승규 님 10-14: "동영상은 찍지 마"): keep the stills only and hand no frame on, so no video is written.</summary>
        public static bool StillsOnly;
        /// <summary>R143: film every shot from here instead (e.g. where the player's camera would be, as 승규 님 saw the
        /// queen in play), with this field of view (0: the film's 45°).</summary>
        public static Vector3? EyeOverride, LookOverride;
        public static float FovOverride;
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
            new Shot { run = "pawn-angles", title = "폰 · 첫 두 걸음", note = "밟은 칸이 우리 색으로 찍힘 · 곧게 밀면 밀림 · 대각선으로 치면 넘어지며 먼지 (R138: 반원 고리 없음, 실제 먼지)",
                eye = new Vector3(2.8f, 2.6f, -11.6f), look = new Vector3(0f, 0.4f, -7f), lead = 0.4f },
            new Shot { run = "pawn-help", title = "폰 · 일으켜 세우기", note = "넘어진 팀원 칸이 솟아 들어 올림 (R153: 부축 버프 없음)",
                eye = new Vector3(2.8f, 2.6f, -11.6f), look = new Vector3(0f, 0.4f, -7f), lead = 0.4f },
            // R153: the second step goes for an enemy ahead (a little to the side) and bends onto it.
            new Shot { run = "pawn-home", title = "폰 · 두 번째 걸음 유도", note = "첫 걸음 1.5 m → 두 번째 F는 앞쪽 적에게 꺾어 들어가 밀침 · 뒤에 넘어진 팀원에게 가면 일으켜 세움",
                eye = new Vector3(3.2f, 2.8f, -14.4f), look = new Vector3(0.4f, 0.4f, -9.6f), lead = 0.4f },
            new Shot { run = "queen", title = "퀸 · 팔방 밀치기", note = "R142: 모일 때 체스말에서 가슴으로 번개 · 펑! 팔방 번개 + 충격파 돔·바닥 물결 세 겹, 물결 앞에서 번개가 튐 · 맞은 기물에 번개 · 체스말이 쓸려 넘어짐 (폭발·먼지 없음)",
                eye = new Vector3(2.4f, 3.6f, -13f), look = new Vector3(-0.2f, 0.3f, -7.8f), lead = 0.5f },
            // R153: the rook's straight chain pull (skill plan v0.3), in place of the straight charge.
            new Shot { run = "rook-chain-enemy", title = "룩 · 직선 사슬 견인 (적)", note = "F 조준 흰 선 → 좌클릭 사슬 → 적이 걸려 룩 앞 1.5 m로 끌려옴 → 휘청 · 두 번째는 퀸(무게 2) = 1.3배 느림",
                eye = new Vector3(4.6f, 2.4f, -11.2f), look = new Vector3(0f, 0.6f, -9f), lead = 0.4f },
            new Shot { run = "rook-chain-ally", title = "룩 · 직선 사슬 견인 (팀원)", note = "팀원에게 T자 손잡이 → 우클릭으로 잡으면 낮은 포물선으로 룩 옆 1 m · 두 번째는 안 잡음 → 2초 뒤 되감기 (쿨 4초)",
                eye = new Vector3(4.6f, 2.4f, -11.2f), look = new Vector3(0f, 0.6f, -9.5f), lead = 0.4f },
            new Shot { run = "rook-chain-cancel", title = "룩 · 사슬 끊김", note = "끌어오는 중 룩이 넘어지면 사슬이 끊겨 적은 그 자리에 놓임",
                eye = new Vector3(4.6f, 2.4f, -11.2f), look = new Vector3(0f, 0.6f, -9f), lead = 0.4f },
            new Shot { run = "rook-chain-wall", title = "룩 · 사슬이 벽에", note = "벽에 닿으면 0.2초 되감기 · 아무도 안 끌려옴",
                eye = new Vector3(16.8f, 2.6f, -4.4f), look = new Vector3(18f, 0.9f, 0f), lead = 0.4f },
            // R130 (승규 님: "벽이나 폰들이랑 부딪히거나 이럴 때 … 무슨 키를 눌러도 낑긴 것 같은 거").
            new Shot { run = "stuck-dive", title = "벽 앞 좌클릭 다이빙 — 낑김 없앰 (R130)", note = "벽에 막혀 기대 있어도 멈추면 0.3초 뒤 일어나 바로 뒤로 걸어감 (전에는 최대 6초 동안 어떤 키도 안 먹힘)",
                eye = new Vector3(15.2f, 2.3f, -3.8f), look = new Vector3(18.6f, 0.5f, 0f), lead = 0.3f },
            new Shot { run = "bishop-trip", title = "비숍 · 교차 밧줄", note = "조준 · X를 던짐 · R142: 나무 말뚝이 박히고 밧줄이 매듭으로 묶임 · 세 가닥 꼰 마 밧줄 · 걸리면 넘어지지 않고 네 줄이 허리로 모여 꽁꽁 묶임 → 풀림",
                // R138: closer, so the tie can be seen (it was filmed from 7 m).
                eye = new Vector3(-2.7f, 2.5f, -10.4f), look = new Vector3(-0.5f, 0.35f, -6.5f), lead = 0.45f },
            // R153: the knight's fork (skill plan v0.3) in place of the turned leap and the head stomp.
            new Shot { run = "knight-fork", title = "나이트 · 포크", note = "F로 앞쪽 적 표시 → 머리 위로 뛰어 밟아 넘어뜨림 → 옆에 착지, 옆의 적은 휘청 · 발굽 자국(R157 바닥 데칼)·먼지 · 착지에 편자 로고",
                eye = new Vector3(3.8f, 3.0f, -13.6f), look = new Vector3(0.4f, 0.4f, -8.4f), lead = 0.45f },
            new Shot { run = "knight-whiff", title = "나이트 · 빗나감", note = "뛰는 사이 대상이 3 m 넘게 피함 → 초당 3 m까지만 따라가 빈 자리에 착지 · 아무도 안 휘청 · 쿨은 그대로 7초",
                eye = new Vector3(2.2f, 3.4f, -14.6f), look = new Vector3(2.0f, 0.4f, -8.4f), lead = 0.45f },
            new Shot { run = "bishop-leash", title = "비숍 · 덫 유지 거리", note = "X를 깔고 뒤로 걸어 6 m를 넘으면 밧줄이 풀려 사라짐 · 다시 깔고 비숍이 넘어지면 바로 사라짐",
                eye = new Vector3(3.6f, 3.2f, -14f), look = new Vector3(0f, 0.3f, -9f), lead = 0.45f },
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

        /// <summary>Film every shot (or only the runs whose names start with <paramref name="only"/>, or with any of
        /// its comma-separated parts) into <paramref name="path"/>. Needs Play mode in PawnRush_SkillTest.</summary>
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
            cam.fieldOfView = FovOverride > 0f ? FovOverride : 45f;
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
            if (!StillsOnly) Began?.Invoke(path, w, h, fps);
            RagdollPawn.SkillFx += OnSkillFx;
            foreach (var shot in Shots)
            {
                if (!string.IsNullOrEmpty(only) && !System.Array.Exists(only.Split(','), o => shot.run.StartsWith(o.Trim()))) continue;
                float first = 0.5f, last = 1.5f;
                foreach (bool slow in SlowTakes ? new[] { false, true } : new[] { false })
                {
                    Status = $"filming {shot.run}{(slow ? " (slow)" : "")}";
                    eye = EyeOverride ?? shot.eye;
                    look = LookOverride ?? shot.look;
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
            if (!StillsOnly) Ended?.Invoke();
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
                if (!StillsOnly) Frame?.Invoke(frame);
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
