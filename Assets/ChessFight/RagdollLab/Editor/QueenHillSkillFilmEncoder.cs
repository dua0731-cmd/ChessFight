using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>Writes QueenHillSkillFilm's frames into H.264 .mp4 files with Unity's own encoder (no Recorder package,
    /// no ffmpeg), one file per Began … Ended (the full-speed film, then the slow one). Hooked up whenever scripts load,
    /// so a film started in Play mode is written. No sound (R89).</summary>
    [InitializeOnLoad]
    public static class QueenHillSkillFilmEncoder
    {
        static MediaEncoder encoder;

        static QueenHillSkillFilmEncoder()
        {
            QueenHillSkillFilm.Began += Begin;
            QueenHillSkillFilm.Frame += Add;
            QueenHillSkillFilm.Ended += End;
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
