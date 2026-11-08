using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A stand-in for the late-game obstacle section (R508, the user: "후반은 아직 구현된게 없으니 바닥만 짧게 만들어서
    // 출력되도록 임시조치"): slot 5 has no section of its own yet, so this short plain way fills it until one comes - a
    // floor 12 m wide and 24 m long with railings, a checkpoint at its start, a kill volume far below. Section frame:
    // entry (0, 0, 0), exit (0, 0, 24), both on the floor (y 0), so the late mini-game joins straight on.
    //
    //   Late_3-0_TempFloor (TempLateSection, PawnRushSection)
    //     Content        the floor, the railings, the marker, CP0, the kill volume, ProgressPath
    //
    // Built from code when the prefab wakes, like the garden and the war room (the prefab holds the root only). Take it
    // out of slot 5's list (Assets/Settings/PawnRushSections.asset) when a real late section is in.
    [RequireComponent(typeof(PawnRushSection))]
    public sealed class TempLateSection : MonoBehaviour
    {
        public const string Id = "Late_3-0_TempFloor";
        public const string DisplayName = "후반 장애물 (임시 바닥)";
        public const string ContentName = "Content";
        public const float Length = 24f, Half = 6f;

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
            // A sky-blue strip across the start: the late zone begins here (temporary).
            if (kit != null && kit.marker != null)
                b.Box("Start marker", new Vector3(-Half, .005f, 1f), new Vector3(Half, .03f, 2f), kit.marker, false, false);
            b.Checkpoint(0, new Vector3(0f, 0f, 3f), 0f, 10f);
            b.Trigger<KillVolume>("Kill volume (far below)", new Vector3(-30f, -42f, -10f), new Vector3(30f, -40f, Length + 10f));

            var route = new[] { Vector3.zero, new Vector3(0f, 0f, Length) };
            var path = new GameObject("ProgressPath");
            path.transform.SetParent(root.transform, false);
            path.AddComponent<ProgressPath>().Configure(route, (Vector3[])route.Clone(), new[] { "1", "1" });

            var section = GetComponent<PawnRushSection>();
            if (section != null)
                section.Describe(Id, DisplayName, PawnRushSection.SlotKind.Obstacle, Vector3.zero, new Vector3(0f, 0f, Length), 0f,
                                 new[] { new SectionBeat("1 임시 바닥", new Vector3(-Half - 1f, -2f, 0f), new Vector3(Half + 1f, 6f, Length)) });
            root.SetActive(true);
        }
    }
}
