using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ChessFight.Network;
using UnityEditor;
using UnityEngine;

namespace ChessFight.RagdollLab.Editor
{
    /// <summary>
    /// The Sword Fight skills' Mixamo clips (R190). A clip dropped into <see cref="Folder"/> is imported the first time as
    /// a humanoid (its own avatar, no loop, the root's turn and height baked into the pose, no materials), and the menu
    /// ChessFight/Sword Fight/Skill Clips/Fill From Mixamo Folder puts every clip named &lt;Piece&gt;_F or &lt;Piece&gt;_E
    /// (King_F.fbx, Knight_E_backflip.fbx ...) into Resources/SwordFightClips.asset (<see cref="SwordFightClipSet"/>).
    /// An entry whose clip is the same keeps its marks and shares; a new or changed clip gets marks from the clip's length.
    /// </summary>
    public class SwordFightClipImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Art/SwordFight/Mixamo";
        const string SetPath = "Assets/Resources/SwordFightClips.asset";
        static readonly Regex Name = new Regex(@"^(King|Queen|Rook|Bishop|Knight)_(F|E)(?:$|[_ \-.])", RegexOptions.IgnoreCase);

        static bool InFolder(string path) => path.Replace('\\', '/').StartsWith(Folder + "/", StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!InFolder(assetPath) || !(assetImporter is ModelImporter m) || !m.importSettingsMissing) return;
            m.animationType = ModelImporterAnimationType.Human;
            m.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            m.importAnimation = true;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.importCameras = false;
            m.importLights = false;
        }

        void OnPreprocessAnimation()
        {
            if (!InFolder(assetPath) || !(assetImporter is ModelImporter m) || !m.importSettingsMissing) return;
            var clips = m.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.loopTime = false;
                // Baked into the pose: the clip's turn and rise stay in the hips, where the skill reads them.
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
            }
            m.clipAnimations = clips;
        }

        [MenuItem("ChessFight/Sword Fight/Skill Clips/Fill From Mixamo Folder")]
        public static void Fill()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                EditorUtility.DisplayDialog("Skill Clips", "No folder " + Folder + ". Put the Mixamo clips there first (Assets/Art/SwordFight/Mixamo/README.md).", "OK");
                return;
            }
            var set = AssetDatabase.LoadAssetAtPath<SwordFightClipSet>(SetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<SwordFightClipSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            var entries = new List<SwordFightClipSet.Entry>(set.entries ?? Array.Empty<SwordFightClipSet.Entry>());
            var log = new StringBuilder("[SkillClips]");
            var seen = new HashSet<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var match = Name.Match(Path.GetFileNameWithoutExtension(path));
                if (!match.Success) { log.Append(" skipped ").Append(Path.GetFileName(path)).Append(" (name it <Piece>_F or <Piece>_E);"); continue; }
                var piece = (PieceKind)Enum.Parse(typeof(PieceKind), match.Groups[1].Value, true);
                bool edge = match.Groups[2].Value.Equals("E", StringComparison.OrdinalIgnoreCase);
                string key = piece + (edge ? " E" : " F");
                if (!seen.Add(key)) { log.Append(" second clip for ").Append(key).Append(" skipped: ").Append(Path.GetFileName(path)).Append(';'); continue; }
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
                if (clip == null) { log.Append(' ').Append(Path.GetFileName(path)).Append(" has no clip;"); continue; }
                var e = entries.Find(x => x != null && x.piece == piece && x.edge == edge);
                if (e == null) entries.Add(e = new SwordFightClipSet.Entry { piece = piece, edge = edge });
                if (e.clip != clip || e.marks == null || e.marks.Length != 4)
                {
                    e.clip = clip;
                    // Until the blow is marked by eye: the windup the first 40% of the clip, the release to 60%.
                    float len = clip.length;
                    e.marks = new[] { 0f, len * 0.4f, len * 0.6f, len };
                    log.Append(' ').Append(key).Append(" ← ").Append(clip.name).Append($" ({len:0.00} s, marks set);");
                }
                else log.Append(' ').Append(key).Append(" kept;");
            }
            set.entries = entries.ToArray();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            SwordFightClipSet.Forget();
            Selection.activeObject = set;
            Debug.Log(log.ToString());
        }
    }
}
