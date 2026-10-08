using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ChessFight.Game
{
    // Pictures and video kept in Assets/StreamingAssets as they were rendered from
    // the design pages (R84): the logo video, the name screen's wood background and
    // the transition's lit board. Copied into builds byte for byte, so they need no
    // import settings; a picture is decoded once and kept.
    public static class StreamingArt
    {
        public const string LogoVideo = "LogoIntro.mp4", NameBackdrop = "NameBackdrop.jpg", TransitionBoard = "TransitionBoard.jpg";

        static readonly Dictionary<string, Texture2D> pictures = new Dictionary<string, Texture2D>();

        public static string PathOf(string file) => Path.Combine(Application.streamingAssetsPath, file);

        // The picture, or null (with a warning once) when the file is missing.
        public static Texture2D Picture(string file)
        {
            if (pictures.TryGetValue(file, out var cached)) return cached;
            Texture2D texture = null;
            string path = PathOf(file);
            try
            {
                if (File.Exists(path))
                {
                    texture = new Texture2D(2, 2, TextureFormat.RGB24, false)
                    {
                        name = file, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                    };
                    if (!texture.LoadImage(File.ReadAllBytes(path), true)) { Object.Destroy(texture); texture = null; }
                }
            }
            catch (IOException e) { Debug.LogWarning("[ChessFight] " + file + ": " + e.Message); texture = null; }
            if (texture == null) Debug.LogWarning("[ChessFight] StreamingAssets/" + file + "을(를) 읽지 못했습니다.");
            pictures[file] = texture;
            return texture;
        }
    }
}
