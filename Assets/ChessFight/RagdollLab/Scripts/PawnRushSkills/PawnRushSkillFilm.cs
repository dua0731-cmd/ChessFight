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
            new Shot { run = "queen", title = "퀸 A · 두 겹 충격파", note = "안쪽 1.5 m 넘어짐 · 바깥 3 m 밀림 · 멈춤 0.06초",
                eye = new Vector3(2.4f, 3.1f, -12.6f), look = new Vector3(-0.3f, 0.3f, -7.2f), lead = 0.5f },
            new Shot { run = "rook-cluster", title = "룩 A · 볼링 핀", note = "1 · 2 · 3명째 멈춤 0.04 → 0.06 → 0.08초 · 4번째에서 쿵",
                eye = new Vector3(-2.4f, 2.5f, -12.6f), look = new Vector3(-6f, 0.5f, -9.2f), lead = 0.35f },
            new Shot { run = "bishop-trip", title = "비숍 B · 대각 칸", note = "X가 지나가는 칸이 빛나고, 걸린 칸이 덜컥",
                eye = new Vector3(2.6f, 3.6f, -10.4f), look = new Vector3(-0.4f, 0f, -6.2f), lead = 0.45f },
            new Shot { run = "knight-stomp", title = "나이트 B · 머리 밟기", note = "납작 · 멈춤 0.09초 · 뿅 · 다시 통",
                eye = new Vector3(7.6f, 1.8f, -8.6f), look = new Vector3(0f, 1.1f, -8.4f), lead = 0.5f },
        };

        LabGame game;
        Camera cam;
        RenderTexture rt;
        Texture2D frame;
        Font font;
        PawnRushSkillFx.Text3D title, note;
        Transform banner;
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
            rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            frame = new Texture2D(w, h, TextureFormat.RGBA32, false);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 64);
            title = new PawnRushSkillFx.Text3D(go.transform, font, " ", Color.white, TitleSize);
            note = new PawnRushSkillFx.Text3D(go.transform, font, " ", new Color(1f, 0.9f, 0.6f), NoteSize);
            banner = Banner(go.transform);
            stills = Path.Combine(Path.GetDirectoryName(path) ?? ".", Path.GetFileNameWithoutExtension(path) + "_stills");
            Directory.CreateDirectory(stills);

            PawnRushSkillFx.ViewOverride = cam;
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

        const float TitleSize = 0.056f, NoteSize = 0.034f;

        /// <summary>A dark strip behind the captions at the top of the picture.</summary>
        static Transform Banner(Transform cam)
        {
            var mesh = new Mesh
            {
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
            };
            var go = new GameObject("Caption strip");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0f, 0.345f, 1.05f);
            go.transform.localScale = new Vector3(2f, 0.15f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.05f, 0.08f, 0.19f, 0.72f) };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            if (rolling)
            {
                title.Place(cam.transform.TransformPoint(new Vector3(0f, 0.37f, 1f)), cam, 1f, 1f);
                note.Place(cam.transform.TransformPoint(new Vector3(0f, 0.313f, 1f)), cam, 1f, 1f);
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

        void OnDestroy()
        {
            RagdollPawn.SkillFx -= OnSkillFx;
            if (Time.captureFramerate != 0) Time.captureFramerate = 0;
            LabCamera.GameTimeClock = false;
            if (PawnRushSkillFx.ViewOverride == cam) PawnRushSkillFx.ViewOverride = null;
        }
    }
}
