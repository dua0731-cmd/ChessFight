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
            new Shot { run = "queen", title = "퀸 · 금빛 원과 불씨", note = "예고 원이 터지는 빛과 같은 금색 · 바닥 원에서 작은 불빛이 연기처럼 피어오름",
                eye = new Vector3(2.4f, 3.1f, -12.6f), look = new Vector3(-0.3f, 0.6f, -7.2f), lead = 0.5f },
            new Shot { run = "bishop-trip", title = "비숍 · 줄이 늘어남", note = "적이 지나가면 줄이 다리를 따라 고무줄처럼 늘어났다 → 잡아채고 튕겨 돌아옴",
                eye = new Vector3(2.0f, 4.4f, -10.4f), look = new Vector3(-0.6f, 0.1f, -6.6f), lead = 0.45f },
            new Shot { run = "knight", title = "나이트 · 착지 표식", note = "파란 원 대신 하얀 원형 표식이 바닥에서 돌고 · 내려올수록 안쪽 원이 조여듦",
                eye = new Vector3(5f, 3.8f, -12.5f), look = new Vector3(0f, 0.7f, -8f), lead = 0.45f },
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
            Began?.Invoke(path, w, h, fps);
            RagdollPawn.SkillFx += OnSkillFx;
            foreach (var shot in Shots)
            {
                if (!string.IsNullOrEmpty(only) && !shot.run.StartsWith(only)) continue;
                float first = 0.5f, last = 1.5f;
                foreach (bool slow in new[] { false, true })
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
