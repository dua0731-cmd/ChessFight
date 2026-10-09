using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Films the Sword Fight skills for review (R91), the way QueenHillSkillFilm does: the probe's runs from a fixed
    /// camera per shot, game time stepped 1/fps per frame (Time.captureFramerate), every frame handed to whoever listens
    /// (in the editor SwordFightSkillFilmEncoder writes the .mp4), every StillEvery-th frame kept as a .jpg. Two films:
    /// full speed, then the same shots slowed ×0.3 from the moment each is staged. A caption names the skill at the top;
    /// the button the probe presses shows at the bottom (only in the film).
    /// </summary>
    [DefaultExecutionOrder(300)]   // after the effects shake the camera (210)
    public class SwordFightSkillFilm : MonoBehaviour
    {
        public static event Action<string, int, int, int> Began;
        public static event Action<Texture2D> Frame;
        public static event Action Ended;
        public static string Status { get; private set; } = "idle";
        public static int Frames { get; private set; }
        public static int StillEvery = 10;
        public static float SlowScale = 0.3f;
        public static bool Rolling { get; private set; }
        public static readonly List<string> Index = new List<string>();

        struct Shot
        {
            public string run, title, note;
            public Vector3 eye, look;
            public float fov;
        }

        static readonly Shot[] Shots =
        {
            new Shot { run = "king", title = "킹 · 왕의 반격", note = "가장자리를 등지고 받아내기 자세 → 적의 칼을 막고 반경 2.5 m를 밀어냄 → 가장자리 쪽 적은 장외",
                eye = new Vector3(3.0f, 3.6f, -3.6f), look = new Vector3(5.2f, 0.2f, -0.2f), fov = 46f },
            new Shot { run = "king-whiff", title = "킹 · 왕의 반격 (상대가 늦게 벨 때)", note = "칼이 안 오면 자세가 풀리고 0.6초 빈틈 → 그때 맞음 · 쿨타임은 그대로",
                eye = new Vector3(3.0f, 3.6f, -3.6f), look = new Vector3(5.2f, 0.2f, -0.2f), fov = 46f },
            new Shot { run = "king-mid", title = "킹 · 왕의 반격 (맵 가운데)", note = "가운데에서 F → 0.3초 뒤 F8: 가장 가까운 적이 옆으로 와서 벰 → 받아내고 밀어냄 · 머리 위에 왕관 표시",
                eye = new Vector3(-2.6f, 2.7f, -4.9f), look = new Vector3(-0.1f, 0.3f, -1.1f), fov = 44f },
            new Shot { run = "queen", title = "퀸 · 꼬치 베기", note = "줄 예고 0.4초 → 퀸이 줄을 따라 돌진하며 지나가는 적을 벰 (첫째 3.0 m · 둘째 2.1 m, 앞·바깥으로) → 가장자리 앞에서 멈춤",
                eye = new Vector3(1.1f, 5.5f, -5.6f), look = new Vector3(3.6f, 0f, 0.5f), fov = 46f },
            new Shot { run = "rook", title = "룩 · 열린 파일 포격", note = "칼을 들면 통로가 갈라짐 0.5초 → 내려치면 칸마다 체스 말이 솟아(순서는 매번 무작위) 적을 띄워 밀어냄 (가까이 2.6 m · 멀리 1.6 m)",
                eye = new Vector3(-6.0f, 4.4f, -7.5f), look = new Vector3(1.2f, 0.2f, -1.9f), fov = 44f },
            new Shot { run = "bishop", title = "비숍 · 관통 핀", note = "손 두 개가 대각선으로 뻗어 두 발목을 잡음 → 묶임 0.8초 + 감속 → 아군이 와서 칼 한 번에 장외",
                eye = new Vector3(8.6f, 2.0f, -2.0f), look = new Vector3(5.2f, 0.15f, 0.6f), fov = 46f },
            new Shot { run = "bishop-mid", title = "비숍 · 관통 핀 (맵 가운데)", note = "가장자리가 아니어도 묶임: 손이 대각선으로 뻗어 발목을 잡음 → 아군이 와서 벰",
                eye = new Vector3(-6.9f, 4.4f, -3.6f), look = new Vector3(-0.2f, 0.1f, -0.3f), fov = 46f },
            new Shot { run = "bishop-miss", title = "비숍 · 관통 핀 (빗나감)", note = "점에 적이 없으면 손이 바닥을 치고 주먹을 쥔 뒤 돌아옴",
                eye = new Vector3(-6.9f, 4.4f, -3.6f), look = new Vector3(-0.6f, 0.1f, 0.6f), fov = 46f },
            new Shot { run = "knight", title = "나이트 · 포크 강하", note = "착지점과 두 자리를 미리 보여 줌 → 도약 0.55초 → 착지 때 두 자리의 적을 함께 밀어냄",
                eye = new Vector3(1.3f, 5.3f, -5.6f), look = new Vector3(3.6f, 0.3f, 0.2f), fov = 46f },
        };

        SwordFightSkillBed bed;
        Camera cam;
        RenderTexture rt;
        Texture2D frame;
        Font font;
        PawnRushSkillFx.Text3D title, note, key;
        Transform banner, keyStrip;
        int keySerial;
        float keyLeft, depth = 1f, titleFit = 1f, noteFit = 1f, keyFit = 1f;
        Vector3 eye, look;
        string stills;
        int width, height;
        static GameObject rig;

        /// <summary>Film every shot (or only those whose run starts with <paramref name="only"/>) into <paramref name="path"/>
        /// and, if <paramref name="slow"/>, the same name + "_slow".</summary>
        public static string Run(string path, int width = 1280, int height = 720, int fps = 60, string only = null, bool slow = true)
        {
            var bed = FindFirstObjectByType<SwordFightSkillBed>();
            if (bed == null) return "no SwordFightSkillBed (open SwordFight_SkillTest and press Play)";
            var film = bed.GetComponent<SwordFightSkillFilm>();
            if (film == null) film = bed.gameObject.AddComponent<SwordFightSkillFilm>();
            film.bed = bed;
            film.StopAllCoroutines();
            if (rig != null) Destroy(rig);
            rig = null;
            Rolling = false;
            film.StartCoroutine(film.Main(path, width, height, fps, only, slow));
            return Status = "starting";
        }

        /// <summary>One picture from a camera into a .jpg (to try a shot's framing).</summary>
        public static string Snap(string path, Vector3 eye, Vector3 look, float fov = 44f, int width = 960, int height = 540)
        {
            var go = new GameObject("Sword Fight snap camera");
            var c = go.AddComponent<Camera>();
            var bed = FindFirstObjectByType<SwordFightSkillBed>();
            if (bed != null && bed.Game.CameraRig != null) c.CopyFrom(bed.Game.CameraRig.Cam);
            c.rect = new Rect(0f, 0f, 1f, 1f);
            c.enabled = false;
            c.fieldOfView = fov;
            c.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
            var target = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            c.targetTexture = target;
            c.Render();
            c.targetTexture = null;
            RenderTexture.active = target;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            File.WriteAllBytes(path, tex.EncodeToJPG(88));
            DestroyImmediate(go);
            DestroyImmediate(target);
            DestroyImmediate(tex);
            return path;
        }

        IEnumerator Main(string path, int w, int h, int fps, string only, bool slowToo)
        {
            width = w;
            height = h;
            var go = new GameObject("Sword Fight skill film camera");
            rig = go;
            cam = go.AddComponent<Camera>();
            if (bed.Game.CameraRig != null) cam.CopyFrom(bed.Game.CameraRig.Cam);
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.enabled = false;
            rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            frame = new Texture2D(w, h, TextureFormat.RGBA32, false);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 64);
            title = new PawnRushSkillFx.Text3D(go.transform, font, " ", Color.white, TitleSize);
            note = new PawnRushSkillFx.Text3D(go.transform, font, " ", new Color(1f, 0.9f, 0.6f), NoteSize);
            key = new PawnRushSkillFx.Text3D(go.transform, font, " ", Color.white, KeySize);
            banner = Banner(go.transform, 0.345f);
            keyStrip = Banner(go.transform, -0.335f, 0.5f);
            keyStrip.gameObject.SetActive(false);

            SwordFightSkillFx.ViewOverride = cam;
            bed.Game.SetMenu(true);   // the match reads no keys or mouse while the film runs
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
                    depth = Mathf.Tan(22f * Mathf.Deg2Rad) / Mathf.Tan(shot.fov * 0.5f * Mathf.Deg2Rad);
                    banner.localPosition = new Vector3(0f, 0.345f, 1.05f * depth);
                    keyStrip.localPosition = new Vector3(0f, -0.335f, 1.05f * depth);
                    SetCaption(shot.title, slow ? $"느리게 ×{SlowScale:0.0#} · {shot.note}" : shot.note);
                    Time.timeScale = 1f;
                    SwordFightSkillProbe.Run(shot.run);
                    yield return null;
                    while (!SwordFightSkillProbe.Staged && !Done) yield return null;
                    if (slow) Time.timeScale = SlowScale;
                    Index.Add($"{(slow ? "slow" : "full")} {shot.run} from frame {Frames}");
                    keySerial = SwordFightSkillProbe.HintSerial;
                    Rolling = true;
                    while (!Done) yield return null;
                    Rolling = false;
                    Time.timeScale = 1f;
                }
                Ended?.Invoke();
            }
            bed.Game.SetMenu(false);
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            LabCamera.GameTimeClock = false;
            SwordFightSkillFx.ViewOverride = null;
            Destroy(go);
            rig = null;
            Destroy(rt);
            Destroy(frame);
            Status = $"done {Frames} frames → {path}";
        }

        static bool Done => SwordFightSkillProbe.Status.StartsWith("done");

        void SetCaption(string a, string b)
        {
            title.Destroy();
            note.Destroy();
            title = new PawnRushSkillFx.Text3D(cam.transform, font, a, Color.white, TitleSize);
            note = new PawnRushSkillFx.Text3D(cam.transform, font, b, new Color(1f, 0.9f, 0.6f), NoteSize);
            titleFit = Fit(a, TitleSize);
            noteFit = Fit(b, NoteSize);
        }

        const float TitleSize = 0.05f, NoteSize = 0.028f, KeySize = 0.046f;

        float Fit(string text, float size)
        {
            font.RequestCharactersInTexture(text, 64, FontStyle.Bold);
            float px = 0f;
            foreach (char c in text)
                if (font.GetCharacterInfo(c, out var info, 64, FontStyle.Bold)) px += info.advance;
            float wide = px * size / 64f, room = 2f * Mathf.Tan(22f * Mathf.Deg2Rad) * width / height * 0.92f;
            return wide > room ? room / wide : 1f;
        }

        static Transform Banner(Transform parent, float y, float width = 2f)
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
                title.Place(cam.transform.TransformPoint(new Vector3(0f, 0.37f, depth)), cam, titleFit, 1f);
                note.Place(cam.transform.TransformPoint(new Vector3(0f, 0.313f, depth)), cam, noteFit, 1f);
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
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
        }

        void ShowKey()
        {
            if (SwordFightSkillProbe.HintSerial != keySerial)
            {
                keySerial = SwordFightSkillProbe.HintSerial;
                key.Destroy();
                string hint = $"[ {SwordFightSkillProbe.Hint} ]";
                key = new PawnRushSkillFx.Text3D(cam.transform, font, hint, new Color(1f, 0.85f, 0.3f), KeySize);
                keyFit = Fit(hint, KeySize);
                keyLeft = 1.1f;
            }
            keyLeft -= Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;
            bool on = keyLeft > 0f;
            keyStrip.gameObject.SetActive(on);
            key.Place(cam.transform.TransformPoint(new Vector3(0f, -0.335f, depth)), cam, on ? keyFit : 0.001f, Mathf.Clamp01(keyLeft / 0.3f));
        }

        void OnDestroy()
        {
            Rolling = false;
            if (rig != null) Destroy(rig);
            rig = null;
            if (Time.captureFramerate != 0) Time.captureFramerate = 0;
            LabCamera.GameTimeClock = false;
            if (SwordFightSkillFx.ViewOverride == cam) SwordFightSkillFx.ViewOverride = null;
        }
    }
}
