using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>Opens the Sword Fight skill test scene (R91): a copy of the Sword Fight match scene (not the ragdoll lab)
    /// with the skill test bed on the match object. If the scene is not there yet, it is made: SwordFight copied, the bed
    /// added. The match scene itself is not touched.</summary>
    public static class SwordFightSkillTestMenu
    {
        public const string ScenePath = "Assets/Scenes/SkillTest/SwordFight_SkillTest/SwordFight_SkillTest.unity";

        [MenuItem("ChessFight/Sword Fight/Open Skill Test", false, 42)]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) Debug.Log(Make());
            else EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>Copy the match scene into the skill test folder and put the bed on its match object. Returns what it did
        /// (for the editor bridge).</summary>
        public static string Make()
        {
            if (File.Exists(ScenePath)) return "already there: " + ScenePath;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? ".");
            AssetDatabase.Refresh();
            if (!AssetDatabase.CopyAsset(SwordFightBuilder.ScenePath, ScenePath)) return "could not copy " + SwordFightBuilder.ScenePath;
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var game = Object.FindFirstObjectByType<SwordFightGame>();
            if (game == null) return "no SwordFightGame in the copy";
            if (game.GetComponent<SwordFightSkillBed>() == null) game.gameObject.AddComponent<SwordFightSkillBed>();
            game.gameObject.name = "Sword Fight · skill test";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return "made " + ScenePath + " (bed on " + game.name + ")";
        }
    }

    /// <summary>Writes SwordFightSkillFilm's frames into H.264 .mp4 files with Unity's own encoder, one file per Began …
    /// Ended. No sound (R91).</summary>
    [InitializeOnLoad]
    public static class SwordFightSkillFilmEncoder
    {
        static MediaEncoder encoder;

        static SwordFightSkillFilmEncoder()
        {
            SwordFightSkillFilm.Began += Begin;
            SwordFightSkillFilm.Frame += f => encoder?.AddFrame(f);
            SwordFightSkillFilm.Ended += End;
            EditorApplication.playModeStateChanged += _ => { if (!EditorApplication.isPlayingOrWillChangePlaymode) End(); };
        }

        static void Begin(string path, int width, int height, int fps)
        {
            End();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            encoder = new MediaEncoder(path, new VideoTrackAttributes
            {
                frameRate = new MediaRational(fps),
                width = (uint)width,
                height = (uint)height,
                includeAlpha = false,
                bitRateMode = VideoBitrateMode.Medium,
            });
        }

        static void End()
        {
            encoder?.Dispose();
            encoder = null;
        }
    }
}
