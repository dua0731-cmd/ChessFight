using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R493 (승규 11-06): the war room (작전 회의실) - zone E of the SeoungTest scene, LibraryReplacement - as the round's
    // last section (WarRoomSection, Final_WarRoom.prefab, slot 7 마지막 구간). The room is built exactly as zone E; this
    // file only adds what a section needs around it (PAWN_RUSH_SECTIONS.md §3 "새 구간 프리팹의 약속").
    //
    // Section frame = zone E's: x east, z north (the way on), y up = the start floor's top. Entered over the start floor's
    // open south edge (z -22.5, 28.5 m between the side walls), finished in the goal's capture zone on the star platform
    // (GOAL_capture_zone, x +-1.05, z 16.95 .. 19.05, in front of the three rooks), where the exit is put: the arched door
    // behind the rooks is a picture on the solid rear wall, so no section can follow this one. In the middle the conveyor
    // carries the way over the lower library (y -8), the blower cannons stand on the balconies either side, the goal steps
    // (J14) climb from the conveyor's north end; a fall into the lower library is no try lost, its stairs climb back.
    //
    //   Final_WarRoom (WarRoomSection, PawnRushSection)
    //     Content
    //       Library replacement        zone E's room: the model, the conveyor, the cannons, the rooks, the return stairs
    //       CP0                        across the start floor
    //       Kill volume                far under the lower library and the outside stairs: only a pawn thrown out
    //       Conveyor ride              IntendedGap: the conveyor, not a floor, carries the way over the lower library
    //       ProgressPath               start -> conveyor -> goal steps -> capture zone, the same for both teams
    public static partial class Course01v4Builder
    {
        public const string WarRoomName = "작전 회의실";
        public static readonly Vector3 WarRoomEntry = new Vector3(0f, 0f, -22.5f), WarRoomExit = new Vector3(0f, 1.49f, 18f);

        public static GameObject BuildWarRoomSection(WarRoomSection room)
        {
            var old = room.transform.Find(WarRoomSection.ContentName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(WarRoomSection.ContentName);
            root.SetActive(false);
            root.transform.SetParent(room.transform, false);
            var b = new CourseBuilder(room.Kit, root.transform);

            LibraryReplacement(b);
            // The test scene started a pawn at (0, 0, -16.5); the checkpoint spans the start floor so a pawn coming in
            // along either side passes it too (its six respawn spots stay in the middle, clear of the war map's props).
            b.Checkpoint(0, new Vector3(0f, 0f, -15.5f), 0f, 28f);
            b.Trigger<KillVolume>("Kill volume (below the war room)", new Vector3(-40f, -40f, -23f), new Vector3(40f, -14f, 40f));
            var ride = new GameObject("Conveyor ride");
            ride.transform.SetParent(root.transform, false);
            ride.transform.localPosition = new Vector3(0f, .5f, 0f);
            ride.AddComponent<IntendedGap>().Configure(new Vector3(4f, 3f, 25.2f), "컨베이어가 아래층 위로 실어 나름 (16초 왕복)");
            WarRoomPath(root.transform);

            var section = room.GetComponent<PawnRushSection>();
            if (section != null)
                section.Describe(WarRoomSection.Id, WarRoomName, PawnRushSection.SlotKind.Final, WarRoomEntry, WarRoomExit, 0f, WarRoomBeats());
            root.SetActive(true);
            return root;
        }

        // The one way, both teams': across the start floor, onto the conveyor at its south end, up the goal steps at its
        // north end and into the capture zone. Each point names the beat of the leg that starts at it.
        static void WarRoomPath(Transform parent)
        {
            var route = new[]
            {
                WarRoomEntry, new Vector3(0f, 0f, -15.5f),
                new Vector3(0f, .2f, -12.5f), new Vector3(0f, .2f, 12.5f),
                new Vector3(0f, 1.2f, 16.5f), WarRoomExit,
            };
            var beats = new[] { "1", "1", "2", "3", "3", "3" };
            var go = new GameObject("ProgressPath");
            go.transform.SetParent(parent, false);
            go.AddComponent<ProgressPath>().Configure(route, (Vector3[])route.Clone(), beats);
        }

        // Areas for the HUD and the CSV, the first that holds a pawn wins (a pawn is measured at its hips). The lower
        // library and the outside return stairs and galleries (out to x +-22.9) are one beat of their own.
        public static SectionBeat[] WarRoomBeats() => new[]
        {
            new SectionBeat("1 출발 (작전 지도)", new Vector3(-15f, -1f, -23f), new Vector3(15f, 8f, -12.5f)),
            new SectionBeat("2 컨베이어 · 공기 대포", new Vector3(-15f, -1f, -12.5f), new Vector3(15f, 8f, 12.5f)),
            new SectionBeat("3 골 계단 · 룩 포격", new Vector3(-15f, -1f, 12.5f), new Vector3(15f, 8f, 25.5f)),
            new SectionBeat("아래층 · 돌아가는 계단", new Vector3(-23.5f, -9.5f, -23f), new Vector3(23.5f, 1.5f, 25.5f)),
        };
    }
}
