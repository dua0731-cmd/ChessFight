using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChessFight.PawnRush.Editor
{
    // Bakes a code-built module (Course01Modules, the design doc's §4 tables) into
    // Prefabs/Modules/PR01_*.prefab, once. After that the prefab is the module: people may
    // edit it by hand, and Build Course01 never overwrites it. Regenerating one module from
    // code is only ever done one at a time, from the menu, after a confirmation.
    public static class Course01ModuleBuilder
    {
        public const string ModuleFolder = "Assets/Maps/PawnRush/Course01/Prefabs/Modules";

        public static string PrefabPath(ModuleId id) => ModuleFolder + "/" + Course01Modules.PrefabName(id) + ".prefab";

        // Gives every layout entry without a prefab its baked one (an existing file is reused).
        public static void BakeMissing(Course01Layout layout, Course01Kit kit)
        {
            bool changed = false;
            foreach (var entry in layout.entries)
            {
                if (entry.prefab != null) continue;
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(entry.module));
                entry.prefab = existing != null ? existing : Bake(entry.module, kit);
                changed = true;
            }
            if (changed)
            {
                EditorUtility.SetDirty(layout);
                AssetDatabase.SaveAssets();
            }
        }

        // Builds one module from code and saves it as its prefab, replacing any file there.
        public static GameObject Bake(ModuleId id, Course01Kit kit)
        {
            Directory.CreateDirectory(ModuleFolder);
            var previous = CourseBuilder.Spawn;
            // Keep the link to the imported obstacle prefabs: the course's changes to them are overrides.
            CourseBuilder.Spawn = (prefab, parent) => (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            GameObject built = null;
            try
            {
                built = Course01Modules.Build(id, kit, null, Vector3.zero);
                var prefab = PrefabUtility.SaveAsPrefabAsset(built, PrefabPath(id));
                Debug.Log("[PawnRush] 모듈 프리팹 생성: " + PrefabPath(id));
                return prefab;
            }
            finally
            {
                CourseBuilder.Spawn = previous;
                if (built != null) Object.DestroyImmediate(built);
            }
        }

        [MenuItem("ChessFight/Pawn Rush/Rebuild Selected Module Prefab")]
        static void RebuildSelected()
        {
            var module = Selected();
            var course = Object.FindFirstObjectByType<PawnRushCourse>();
            if (module == null || course == null || course.Kit == null || course.Layout == null) return;
            if (!EditorUtility.DisplayDialog("모듈 프리팹 다시 만들기",
                    Course01Modules.PrefabName(module.module) + ".prefab을 코드(기획서 표)로 다시 만듭니다.\n" +
                    "이 프리팹을 손으로 고친 내용은 사라집니다.", "다시 만들기", "취소")) return;
            var id = module.module;
            var prefab = Bake(id, course.Kit);
            foreach (var entry in course.Layout.entries)
                if (entry.module == id) entry.prefab = prefab;
            EditorUtility.SetDirty(course.Layout);
            AssetDatabase.SaveAssets();
            Course01Assembler.Build();
        }

        [MenuItem("ChessFight/Pawn Rush/Rebuild Selected Module Prefab", true)]
        static bool CanRebuildSelected() => !EditorApplication.isPlayingOrWillChangePlaymode && Selected() != null;

        static CourseModule Selected()
        {
            var go = Selection.activeGameObject;
            return go != null ? go.GetComponentInParent<CourseModule>(true) : null;
        }
    }
}
