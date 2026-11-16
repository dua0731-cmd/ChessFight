using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A stand-in for the round's ending section, the king's audience hall (R688, the user 2026-11-16: "엔딩구간은 국왕
    // 알현실로 아직 구현이 안됐고 레벨디자인 진행 중이야"): the round ends in a section of its own now, after the late
    // obstacle section, and until that hall is built this plain floor holds its place and the round's finish - a floor
    // 12 m wide and 20 m long with railings on three sides, a checkpoint at its start, the crown platform's gold
    // circle (a 4.8 m circle, room for a team's majority at once: CrownFinish takes the box's narrower half as its
    // radius) and a kill volume far below. Section frame: entry (0, 0, 0); the exit (0, 0, 20) only ends its path -
    // nothing follows the ending.
    //
    //   Final_AudienceHall_Temp (TempEndingSection, PawnRushSection)
    //     Content        the floor, the railings, the marker, CP0, the finish, the kill volume, ProgressPath
    //
    // Built from code when the prefab wakes, like the temporary late floor (the prefab holds the root only). When the
    // audience hall is built, put its prefab in the last slot's list (Assets/Settings/PawnRushSections.asset) in this
    // one's place; it needs a FinishZone (CrownFinish binds the first one under the round).
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class TempEndingSection : MonoBehaviour
    {
        public const string Id = "Final_AudienceHall_Temp";
        public const string DisplayName = "국왕 알현실 (임시)";
        public const string ContentName = "Content";
        public const float Length = 20f, Half = 6f, FinishAt = 13f, FinishRadius = 2.4f;

        [SerializeField] CourseKit kit;

        void Awake()
        {
            if (transform.Find(ContentName) == null) Build();
        }

        void Build()
        {
            var root = new GameObject(ContentName);
            root.SetActive(false);
            root.transform.SetParent(transform, false);
            var b = new CourseBuilder(kit, root.transform);

            b.Floor("Floor", -Half, Half, 0f, Length, 0f);
            b.Rail("Railing W", -Half - .3f, -Half, 0f, Length, 0f, 1.1f);
            b.Rail("Railing E", Half, Half + .3f, 0f, Length, 0f, 1.1f);
            b.Rail("Railing N", -Half - .3f, Half + .3f, Length, Length + .3f, 0f, 1.1f);
            // A strip across the start: the ending begins here (temporary).
            if (kit != null && kit.marker != null)
                b.Box("Start marker", new Vector3(-Half, .005f, 1f), new Vector3(Half, .03f, 2f), kit.marker, false, false);
            b.Checkpoint(0, new Vector3(0f, 0f, 3f), 0f, 10f);
            // The finish's circle, drawn (the trigger itself is a box round it).
            YardKit.Cylinder(root.transform, "Finish circle", new Vector3(0f, .015f, FinishAt), Vector3.up, FinishRadius * 2f, .03f, YardKit.Gold, YardKit.Solid.None);
            b.Trigger<FinishZone>("Finish (goal)", new Vector3(-FinishRadius, 0f, FinishAt - FinishRadius), new Vector3(FinishRadius, 3.4f, FinishAt + FinishRadius)).Configure(true);
            b.Trigger<KillVolume>("Kill volume (far below)", new Vector3(-30f, -42f, -10f), new Vector3(30f, -40f, Length + 10f));

            var route = new[] { Vector3.zero, new Vector3(0f, 0f, FinishAt), new Vector3(0f, 0f, Length) };
            var path = new GameObject("ProgressPath");
            path.transform.SetParent(root.transform, false);
            path.AddComponent<ProgressPath>().Configure(route, (Vector3[])route.Clone(), new[] { "1", "1", "1" });

            var section = GetComponent<PawnRushSection>();
            if (section != null)
                section.Describe(Id, DisplayName, PawnRushSection.SlotKind.Final, Vector3.zero, new Vector3(0f, 0f, Length), 0f,
                                 new[] { new SectionBeat("1 엔딩 (임시 바닥)", new Vector3(-Half - 1f, -2f, 0f), new Vector3(Half + 1f, 6f, Length + 1f)) });
            root.SetActive(true);
        }
    }
}
