using System.Collections.Generic;
using System.IO;
using System.Text;
using ChessFight.Network;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    // R548: keeps Resources/Loading/PawnRushCourse.txt (PawnRushCourseTable) in step with the section set: the slots'
    // prefabs in the set's order, each by its prefab's name and its section's display name. The entrance screen reveals
    // the round's sections from that table before the match scene (and the set with it) has loaded. Written whenever Play
    // starts, before every build and from the menu; the file only changes when the set or a section's name has.
    [InitializeOnLoad]
    public sealed class PawnRushCourseTableWriter : IPreprocessBuildWithReport
    {
        const string SetPath = "Assets/Settings/PawnRushSections.asset";
        const string TablePath = "Assets/Resources/Loading/PawnRushCourse.txt";
        const string Header =
            "R548: the Pawn Rush sections each slot may draw (slot, prefab, name), for the entrance screen's course reveal.\n" +
            "Written from Assets/Settings/PawnRushSections.asset by PawnRushCourseTableWriter (on every Play, every build, and the\n" +
            "menu ChessFight/Pawn Rush/Build/Course Name Table). Do not edit by hand: change the section set or a section's name.";

        static PawnRushCourseTableWriter()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Write(false);
            };
        }

        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => Write(false);

        [MenuItem("ChessFight/Pawn Rush/Build/Course Name Table", false, 110)]
        static void Menu() => Write(true);

        // True when the table on disk matches the set (written now or already).
        public static bool Write(bool tell)
        {
            var set = AssetDatabase.LoadAssetAtPath<PawnRushSectionSet>(SetPath);
            if (set == null)
            {
                Debug.LogWarning("[PawnRush] " + SetPath + "이 없어 코스 이름표(" + TablePath + ")를 만들지 못했습니다.");
                return false;
            }
            var rows = new List<PawnRushCourseTable.Row>();
            var keyOf = new Dictionary<GameObject, string>();
            var taken = new Dictionary<string, GameObject>();
            for (int s = 0; s < set.SlotCount && s < PawnRushSections.SlotCount; s++)
            {
                var slot = set[s];
                if (slot == null || slot.prefabs == null) continue;
                foreach (var prefab in slot.prefabs)
                {
                    if (prefab == null) continue;
                    if (!keyOf.TryGetValue(prefab, out string key))
                    {
                        // The prefab's name; two prefabs of one name tell apart by their asset ids.
                        key = prefab.name;
                        if (taken.TryGetValue(key, out var other) && other != prefab)
                            key += "@" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
                        keyOf[prefab] = key;
                        taken[key] = prefab;
                    }
                    var section = prefab.GetComponent<PawnRushSection>();
                    rows.Add(new PawnRushCourseTable.Row(s, key, section != null ? section.DisplayName : prefab.name));
                }
            }
            string text = PawnRushCourseTable.Format(rows, Header);
            string full = Path.GetFullPath(TablePath);
            string old = File.Exists(full) ? File.ReadAllText(full, Encoding.UTF8) : null;
            if (old == text)
            {
                if (tell) Debug.Log("[PawnRush] 코스 이름표가 이미 구간 목록과 같습니다 (" + rows.Count + "개 구간).");
                return true;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, text, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(TablePath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[PawnRush] 코스 이름표를 다시 썼습니다: " + TablePath + " (" + rows.Count + "개 구간). 저장소에 같이 올려 주세요.");
            return true;
        }
    }
}
