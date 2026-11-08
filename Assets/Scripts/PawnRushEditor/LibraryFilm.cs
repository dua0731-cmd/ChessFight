using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
#if CF_URP
using UnityEngine.Rendering.Universal;
#endif

namespace ChessFight.PawnRush.Editor
{
    // A quick film of E's bells and bookcase door (R507, user 11-07: "하려면 오래 걸리니까 동영상 빠르게 찍어"): one menu
    // click opens the mini-game prefab test (asking to save as Unity does), plays, picks E, rings the six bells of the
    // local team's station one by one the way MgBells does it (the bell's rung flag and its rank-gold material, which
    // the models' MiniGameBellLook follows), completes the game as F6 does (the door opens), and films it at 30 fps
    // from two fixed cameras - the towers, then the door - into an H.264 .mp4 with the bells' notes and the door's
    // creak mixed in, with Unity's own encoder. Then it leaves Play mode.
    // Out: ArtSource/PawnRush/MiniGames/Mid/Video/E_BellsDoor.mp4 (Unity Editor only; nothing in a build).
    [InitializeOnLoad]
    public static class LibraryFilm
    {
        const string Pending = "ChessFight.LibraryFilm.Pending";
        public const string OutPath = "ArtSource/PawnRush/MiniGames/Mid/Video/E_BellsDoor.mp4";

        static LibraryFilm()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.SetBool(Pending, false);
                new GameObject("E library film").AddComponent<LibraryFilmDriver>();
            };
        }

        [MenuItem("ChessFight/Development/E 서고 종·문 영상 찍기", false, 900)]
        public static void Film()
        {
            if (EditorApplication.isPlaying)
            {
                if (Object.FindAnyObjectByType<MiniGamePrefabTest>() == null)
                {
                    Debug.LogWarning("[E 영상] 미니게임 시험 장면에서만 찍습니다. Play를 끄고 다시 누르세요.");
                    return;
                }
                new GameObject("E library film").AddComponent<LibraryFilmDriver>();
                return;
            }
            MiniGamePrefabMenu.OpenTest();
            if (Object.FindAnyObjectByType<MiniGamePrefabTest>() == null) return;   // the user kept the open scene
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }
    }

    public sealed class LibraryFilmDriver : MonoBehaviour
    {
        const int Fps = 30, Width = 1280, Height = 720, Rate = 44100;
        const int ELibrary = 4;                                        // keys 1..0: C D A B E ...
        const float FirstBell = 1.2f, BellGap = 1.5f, AfterLast = 1.6f, DoorShot = 6.5f;

        sealed class Voice { public float[] Data; public int Channels, Rate; public float Pitch, Gain; public double At; }

        MediaEncoder encoder;
        Camera cam;
        RenderTexture rt;
        Texture2D frame;
        readonly List<Voice> voices = new List<Voice>();
        long frames, samplesWritten;

        IEnumerator Start()
        {
            var test = FindAnyObjectByType<MiniGamePrefabTest>();
            if (test == null) { Debug.LogWarning("[E 영상] 시험 장면이 아닙니다"); Destroy(gameObject); yield break; }
            test.Select(ELibrary);
            for (int i = 0; i < 30; i++) yield return null;            // the section installs, the models hang
            var plaza = test.Current;
            MissionStation station = null;
            if (plaza != null)
                foreach (var s in plaza.Stations)
                    if (s != null && s.Team == MissionStation.LocalTeam) station = s;
            var bells = station != null ? station.Game as MgBells : null;
            if (bells == null) { Debug.LogWarning("[E 영상] E 스테이션을 찾지 못했습니다"); Destroy(gameObject); yield break; }

            var clips = new[] { Resources.Load<AudioClip>("PawnRushMiniGameSfx/sfx_bell_ring"), Resources.Load<AudioClip>("PawnRushMiniGameSfx/sfx_shelf_door_open") };
            foreach (var c in clips) if (c != null && c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();
            for (int i = 0; i < 120; i++)
            {
                bool ready = true;
                foreach (var c in clips) if (c != null && c.loadState == AudioDataLoadState.Loading) ready = false;
                if (ready) break;
                yield return null;
            }
            var bellClip = Clip(clips[0], "sfx_bell_ring");
            var doorClip = Clip(clips[1], "sfx_shelf_door_open");
            var game = station.Game.transform;
            Begin();
            Time.captureFramerate = Fps;
            double t0 = Time.time;
            float Now() => (float)(Time.time - t0);
            // shot 1: the towers from the entry side, high
            Aim(game.TransformPoint(new Vector3(-11f, 10f, -4f)), game.TransformPoint(new Vector3(0f, 4.5f, 12f)), 50f);
            int[] semis = { 0, 2, 4, 7, 9, 12 };
            int last = MgBells.Towers.Length - 1;
            void RingWithSound(int i)
            {
                Ring(bells, station, i);
                if (bellClip.data != null) voices.Add(new Voice { Data = bellClip.data, Channels = bellClip.channels, Rate = bellClip.rate, Pitch = Mathf.Pow(2f, semis[i] / 12f), Gain = .9f, At = Now() });
            }
            for (int i = 0; i < last; i++)
            {
                while (Now() < FirstBell + i * BellGap) yield return Capture();
                RingWithSound(i);
            }
            float shotEnd = FirstBell + (last - 1) * BellGap + AfterLast;
            while (Now() < shotEnd) yield return Capture();
            // shot 2: the bookcase door, with the last (tallest) tower near it; its bell completes the game as in
            // play (MgBells completes on its next FixedUpdate) and the door swings open
            var gate = station.Gate != null ? station.Gate.transform.position : game.TransformPoint(new Vector3(0f, 2f, 19f));
            Aim(gate - station.transform.forward * 9f + station.transform.right * 4.5f + Vector3.up * 3.2f, gate + Vector3.up * 1.2f, 58f);
            for (int i = 0; i < 12; i++) yield return Capture();
            RingWithSound(last);
            for (int i = 0; i < 8 && !bells.Completed; i++) yield return Capture();
            if (!bells.Completed) bells.ForceComplete();
            if (doorClip.data != null) voices.Add(new Voice { Data = doorClip.data, Channels = doorClip.channels, Rate = doorClip.rate, Pitch = 1f, Gain = 1f, At = Now() });
            float end = Now() + DoorShot;
            while (Now() < end) yield return Capture();
            End();
            Time.captureFramerate = 0;
            Debug.Log("[E 영상] 저장했습니다: " + Path.GetFullPath(LibraryFilm.OutPath));
            EditorApplication.isPlaying = false;
        }

        // As MgBells.FixedUpdate when a lone own pawn stands on a top: the bell's flag, the count, the rank gold.
        static void Ring(MgBells game, MissionStation station, int i)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var rung = (bool[])typeof(MgBells).GetField("rung", flags).GetValue(game);
            var renderers = (Renderer[])typeof(MgBells).GetField("bells", flags).GetValue(game);
            if (rung[i]) return;
            rung[i] = true;
            var count = typeof(MgBells).GetField("count", flags);
            count.SetValue(game, (int)count.GetValue(game) + 1);
            if (renderers[i] != null) renderers[i].sharedMaterial = station.Kit.rankGold;
        }

        void Aim(Vector3 from, Vector3 at, float fov)
        {
            cam.transform.position = from;
            cam.transform.rotation = Quaternion.LookRotation(at - from, Vector3.up);
            cam.fieldOfView = fov;
        }

        void Begin()
        {
            var go = new GameObject("Film camera");
            go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>();
            cam.nearClipPlane = .1f; cam.farClipPlane = 300f;
            cam.enabled = false;
            var main = Camera.main;
            if (main != null) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }
#if CF_URP
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
#endif
            rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            frame = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            string dir = Path.GetDirectoryName(LibraryFilm.OutPath);
            Directory.CreateDirectory(dir);
            var video = new VideoTrackAttributes { frameRate = new MediaRational(Fps), width = Width, height = Height, includeAlpha = false, bitRateMode = VideoBitrateMode.High };
            var audio = new AudioTrackAttributes { sampleRate = new MediaRational(Rate), channelCount = 2, language = "ko" };
            encoder = new MediaEncoder(LibraryFilm.OutPath, video, audio);
        }

        IEnumerator Capture()
        {
            yield return new WaitForEndOfFrame();
            if (encoder == null) yield break;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            frame.Apply(false);
            RenderTexture.active = prev;
            encoder.AddFrame(frame);
            frames++;
            WriteAudio();
        }

        // The audio up to this frame's end: every voice from its start time, resampled by its pitch, mono to both sides.
        void WriteAudio()
        {
            long target = frames * Rate / Fps;
            int n = (int)(target - samplesWritten);
            if (n <= 0) return;
            using (var buf = new NativeArray<float>(n * 2, Allocator.Temp))
            {
                var b = buf;
                for (int s = 0; s < n; s++)
                {
                    double time = (samplesWritten + s) / (double)Rate;
                    float sum = 0f;
                    foreach (var v in voices)
                    {
                        double pos = (time - v.At) * v.Rate * v.Pitch;
                        if (pos < 0) continue;
                        int i = (int)pos, len = v.Data.Length / v.Channels;
                        if (i + 1 >= len) continue;
                        float f = (float)(pos - i), x0 = 0f, x1 = 0f;
                        for (int c = 0; c < v.Channels; c++) { x0 += v.Data[i * v.Channels + c]; x1 += v.Data[(i + 1) * v.Channels + c]; }
                        sum += (x0 + (x1 - x0) * f) / v.Channels * v.Gain;
                    }
                    sum = Mathf.Clamp(sum, -1f, 1f);
                    b[s * 2] = sum; b[s * 2 + 1] = sum;
                }
                encoder.AddSamples(0, buf);
            }
            samplesWritten = target;
        }

        static (float[] data, int channels, int rate) Clip(AudioClip clip, string path)
        {
            if (clip == null) { Debug.LogWarning("[E 영상] 소리 파일이 없습니다: " + path); return (null, 1, Rate); }
            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0)) { Debug.LogWarning("[E 영상] 소리를 읽지 못했습니다: " + path); return (null, 1, Rate); }
            return (data, clip.channels, clip.frequency);
        }

        void End()
        {
            if (encoder != null) { encoder.Dispose(); encoder = null; }
            if (rt != null) { cam.targetTexture = null; rt.Release(); rt = null; }
        }

        void OnDestroy()
        {
            End();
            Time.captureFramerate = 0;
        }
    }
}
