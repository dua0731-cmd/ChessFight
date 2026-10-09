using System;
using ChessFight.Gameplay.PawnRush;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ChessFight.Maps.Editor
{
    [CustomEditor(typeof(PawnRushCourse))]
    public sealed class PawnRushCourseInspector : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox("A1 → A2 → B1 → B2 → C1 → C2. 각 목록에서 중복 없이 3개를 선정합니다. 구역 수정은 프리팹에서 하세요. 미리보기 재조합은 Generated Sections만 교체하며 Undo가 가능합니다.", HelpBoxMessageType.Info));
            Add(root, "catalog", "구역 프리팹 목록");
            Add(root, "courseEntrance", "전체 코스 입구");
            Add(root, "finishPlatform", "도착 발판");
            Add(root, "playMode", "Play 시 조합 방식");
            Add(root, "seed", "재현용 시드");
            Add(root, "fixedSlots", "고정 배치 A1/A2/B1/B2/C1/C2 (빈 칸 = 무작위)");
            var status = new Label(); status.style.whiteSpace = WhiteSpace.Normal;
            Action<Action> run = action => { try { serializedObject.ApplyModifiedProperties(); action(); serializedObject.Update(); status.text = "완료. 씬 저장 후 Play로 확인하세요."; } catch (Exception e) { status.text = e.Message; Debug.LogException(e); } };
            var course = (PawnRushCourse)target;
            var selection = new Label(); selection.style.whiteSpace = WhiteSpace.Normal;
            root.Add(selection);
            root.schedule.Execute(() => {
                if (course == null) return;
                selection.text = "현재 조합 시드: " + (Application.isPlaying ? course.ActiveSeed : course.PreviewSeed) + "\n" + string.Join(" → ", course.SelectionIds());
            }).Every(500);
            var edit = new VisualElement(); edit.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            edit.Add(new Button(() => run(() => PawnRushWorkspaceTools.BuildPreview(course, course.seed))) { text = "현재 시드로 미리보기 조합" });
            edit.Add(new Button(() => run(() => { Undo.RecordObject(course, "New preview seed"); course.seed = Guid.NewGuid().GetHashCode(); PawnRushWorkspaceTools.BuildPreview(course, course.seed); })) { text = "새 무작위 조합 미리보기" });
            edit.Add(new Button(() => run(() => PawnRushWorkspaceTools.Validate(course))) { text = "배치·연결 검사" });
            edit.Add(new Button(() => run(() => PawnRushWorkspaceTools.SetPlaytestStart(course.courseEntrance.position - course.courseEntrance.forward * 5 + Vector3.up * .04f))) { text = "출발점에서 테스트 시작" });
            for (int i = 0; i < 6; i++)
            {
                int index = i;
                edit.Add(new Button(() => run(() => {
                    var sections = course.Sections;
                    if (sections.Length != 6) throw new InvalidOperationException("먼저 미리보기를 조합하세요.");
                    var entry = sections[index].entrance;
                    PawnRushWorkspaceTools.SetPlaytestStart(entry.position + entry.forward * 3 + Vector3.up * .04f);
                    Undo.RecordObject(course, "Test saved section"); course.playMode = CoursePlayMode.SavedPreview; EditorUtility.SetDirty(course);
                })) { text = PawnRushCourse.SlotName(i) + " 구역부터 테스트 (저장된 배치 사용)" });
            }
            root.Add(edit); root.Add(status);
            root.Add(new HelpBox("현재 구성은 오프라인 제작용입니다. 온라인에서는 각 클라이언트가 별도로 추첨하지 않고, 호스트가 선정한 구역 ID 목록과 미션 상태를 전달하도록 후속 연결해야 합니다.", HelpBoxMessageType.Info));
            return root;
        }
        void Add(VisualElement root, string field, string label) => root.Add(new PropertyField(serializedObject.FindProperty(field), label));
        void OnSceneGUI()
        {
            foreach (var section in ((PawnRushCourse)target).Sections)
                if (section != null && section.entrance != null) Handles.Label(section.entrance.position + Vector3.up * 5, section.name);
        }
    }
}
