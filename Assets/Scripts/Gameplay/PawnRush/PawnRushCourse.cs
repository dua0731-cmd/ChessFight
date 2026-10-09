using System;
using UnityEngine;

namespace ChessFight.Gameplay.PawnRush
{
    public enum CoursePlayMode { [InspectorName("저장된 미리보기")] SavedPreview, [InspectorName("고정 시드로 조합")] FixedSeed, [InspectorName("매번 새 무작위 조합")] NewSeed }

    [DefaultExecutionOrder(-200)]
    public sealed class PawnRushCourse : MonoBehaviour
    {
        public PawnRushCatalog catalog;
        public Transform courseEntrance;
        public Transform finishPlatform;
        public CoursePlayMode playMode = CoursePlayMode.NewSeed;
        public int seed = 12345;
        [Tooltip("A1, A2, B1, B2, C1, C2. 빈 칸은 무작위 선택합니다.")]
        public PawnRushSection[] fixedSlots = new PawnRushSection[6];
        [SerializeField] Transform generatedRoot;
        [SerializeField] PawnRushSection[] sections = new PawnRushSection[0];
        [SerializeField] int previewSeed;
        public Transform GeneratedRoot => generatedRoot;
        public PawnRushSection[] Sections => (PawnRushSection[])sections.Clone();
        public int ActiveSeed { get; private set; }
        public int PreviewSeed => previewSeed;
        public bool LocalAuthority => !ObstacleClock.Shared && !PlaytestSpawner.NetworkDriven;
        public static string SlotName(int index) => index >= 0 && index < 6 ? "ABC"[index / 2] + ((index % 2) + 1).ToString() : "?";

        public PawnRushSection[] Plan(int requestedSeed)
        {
            if (catalog == null || courseEntrance == null) throw new InvalidOperationException("Catalog와 Course Entrance를 지정하세요.");
            if ((transform.lossyScale - Vector3.one).sqrMagnitude > .00001f)
                throw new InvalidOperationException("작업환경 루트 Scale은 (1,1,1)이어야 합니다.");
            return catalog.Choose(requestedSeed, fixedSlots);
        }
        // Editor and runtime use the same connector alignment.
        public Pose Place(PawnRushSection section, Pose at, int index)
        {
            section.name = SlotName(index) + " — " + section.sectionId;
            section.AlignTo(at);
            return new Pose(section.exit.position, section.exit.rotation);
        }
        public void SetGenerated(Transform root, PawnRushSection[] instances, int usedSeed)
        {
            generatedRoot = root; sections = instances; previewSeed = ActiveSeed = usedSeed;
            if (finishPlatform != null && sections.Length == 6)
                finishPlatform.SetPositionAndRotation(sections[5].exit.position, sections[5].exit.rotation);
        }
        public string[] SelectionIds()
        {
            var ids = new string[sections.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = sections[i] == null ? "" : sections[i].sectionId;
            return ids;
        }
        void Awake()
        {
            ActiveSeed = previewSeed;
            if (!LocalAuthority)
            {
                Debug.LogWarning("Pawn Rush: 온라인 코스 선정/미션 동기화 연결 전입니다. 저장된 배치만 표시합니다.", this);
                return;
            }
            if (playMode == CoursePlayMode.SavedPreview) return;
            int nextSeed = playMode == CoursePlayMode.NewSeed ? Guid.NewGuid().GetHashCode() : seed;
            try { BuildRuntime(nextSeed); }
            catch (Exception error) { Debug.LogError("Pawn Rush 구성 실패: " + error.Message, this); }
        }
        public void BuildRuntime(int requestedSeed)
        {
            if (!Application.isPlaying || !LocalAuthority) throw new InvalidOperationException("오프라인 Play Mode에서만 재조합할 수 있습니다.");
            var plan = Plan(requestedSeed); // Validate before removing the current course.
            var next = new GameObject("Generated Sections");
            next.SetActive(false);
            next.transform.SetParent(transform, false);
            var instances = new PawnRushSection[6];
            var pose = new Pose(courseEntrance.position, courseEntrance.rotation);
            try
            {
                for (int i = 0; i < 6; i++)
                {
                    instances[i] = Instantiate(plan[i], next.transform);
                    pose = Place(instances[i], pose, i);
                }
            }
            catch { Destroy(next); throw; }
            if (generatedRoot != null) { generatedRoot.gameObject.SetActive(false); Destroy(generatedRoot.gameObject); }
            SetGenerated(next.transform, instances, requestedSeed);
            next.SetActive(true);
        }
    }
}
