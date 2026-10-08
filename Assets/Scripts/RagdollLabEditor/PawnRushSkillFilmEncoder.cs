using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>Writes PawnRushSkillFilm's frames into an H.264 .mp4 with Unity's own encoder (no Recorder
    /// package, no ffmpeg). Hooked up whenever scripts load, so a film started in Play mode is written.</summary>
    [InitializeOnLoad]
    public static class PawnRushSkillFilmEncoder
    {
        static MediaEncoder encoder;

        static PawnRushSkillFilmEncoder()
        {
            PawnRushSkillFilm.Began += Begin;
            PawnRushSkillFilm.Frame += Add;
            PawnRushSkillFilm.Ended += End;
            EditorApplication.playModeStateChanged += _ => { if (!EditorApplication.isPlayingOrWillChangePlaymode) End(); };
        }

        static void Begin(string path, int width, int height, int fps)
        {
            End();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var video = new VideoTrackAttributes
            {
                frameRate = new MediaRational(fps),
                width = (uint)width,
                height = (uint)height,
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.High,
            };
            encoder = new MediaEncoder(path, video);
        }

        static void Add(Texture2D frame) => encoder?.AddFrame(frame);

        static void End()
        {
            encoder?.Dispose();
            encoder = null;
        }
    }
}
