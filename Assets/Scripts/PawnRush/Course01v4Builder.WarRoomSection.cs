using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R493 (승규 11-06): the war room (작전 회의실) - zone E of the SeoungTest scene, LibraryReplacement - as the round's
    // last section (WarRoomSection, Final_WarRoom.prefab, slot 7 마지막 구간). The room is built exactly as zone E; this
    // file only adds what a section needs around it (PAWN_RUSH_SECTIONS.md §3 "새 구간 프리팹의 약속").
    //
    // Section frame = zone E's: x east, z north (the way on), y up = the start floor's top. Entered over the start floor's
    // open south edge (z -22.5, 28.5 m between the side walls). In the middle the conveyor carries the way over the lower
    // library (y -8), the blower cannons stand on the balconies either side, the goal steps (J14) climb from the
    // conveyor's north end; a fall into the lower library is no try lost, its stairs climb back.
    //
    // R616 (the user, 11-12; Course01v4Builder.WarRoomEnding.cs): the goal's three rooks are four turrets on the corners
    // of the conveyor's well, firing in turn at the carriage, and the round no longer ends on the star platform - the
    // great door behind it (until now a picture on the solid rear wall) is pushed open, and the finish stands beyond it,
    // for now on a plain floor where the user will build the ending. This is still the last section.
    //
    //   Final_WarRoom (WarRoomSection, PawnRushSection)
    //     Content
    //       Library replacement        zone E's room: the model, the conveyor, the cannons, the return stairs,
    //                                  the four turret towers (R616), the great door opened
    //       Exit wall, Exit gate       the rear wall round the opening, the door's two leaves on a PushGate (R616)
    //       CP0                        across the start floor
    //       Kill volume                far under the lower library and the outside stairs: only a pawn thrown out
    //       Conveyor ride              IntendedGap: the conveyor, not a floor, carries the way over the lower library
    //       Ending (temporary floor)   beyond the door: CP1, the finish (R616)
    //       ProgressPath               start -> conveyor -> goal steps -> the door -> the finish, the same for both teams
    public static partial class Course01v4Builder
    {
        public const string WarRoomName = "작전 회의실";
        // The exit is the finish beyond the great door (R616); WarRoomStarExit is the star platform's capture zone, where
        // the round ended before and still does if the door cannot be opened (a model this was not measured on).
        public static readonly Vector3 WarRoomEntry = new Vector3(0f, 0f, -22.5f), WarRoomExit = new Vector3(0f, WarRoomGoalFloor, WarRoomFinishAt),
            WarRoomStarExit = new Vector3(0f, 1.49f, 18f);

        public static GameObject BuildWarRoomSection(WarRoomSection room)
        {
            var old = room.transform.Find(WarRoomSection.ContentName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(WarRoomSection.ContentName);
            root.SetActive(false);
            root.transform.SetParent(room.transform, false);
            var b = new CourseBuilder(room.Kit, root.transform);

            LibraryReplacement(b);
            // R518 (the user's list 13, "엔딩구간 난이도가 너무 어려움(혼자서도 못감). 우선 중앙 레일이 역방향으로 돌아가는 것만
            // 빼봐도 해볼만 할듯"): in the round the conveyor's belt stands still on its carriage, which still goes back and
            // forth. The reverse run pushed a rider back about 2.6 m/s along the deck (R425). Zone E of 승규 님's test scene,
            // built by the same LibraryReplacement, keeps it.
            foreach (var shuttle in root.GetComponentsInChildren<LibraryShuttleDriver>(true)) shuttle.BeltRuns = false;
            WarRoomBalconyScreens(root.transform);
            // R616: the goal's three rooks become four turrets round the conveyor's well, and the great door the way out.
            WarRoomTurrets(root.transform);
            bool opened = WarRoomExitDoor(root.transform) != null;
            // The test scene started a pawn at (0, 0, -16.5); the checkpoint spans the start floor so a pawn coming in
            // along either side passes it too (its six respawn spots stay in the middle, clear of the war map's props).
            b.Checkpoint(0, new Vector3(0f, 0f, -15.5f), 0f, 28f);
            b.Trigger<KillVolume>("Kill volume (below the war room)", new Vector3(-40f, -40f, -23f), new Vector3(40f, -14f, 62f));
            var ride = new GameObject("Conveyor ride");
            ride.transform.SetParent(root.transform, false);
            ride.transform.localPosition = new Vector3(0f, .5f, 0f);
            ride.AddComponent<IntendedGap>().Configure(new Vector3(4f, 3f, 25.2f), "컨베이어가 아래층 위로 실어 나름 (16초 왕복)");
            // R508: the round's finish (a match's CrownFinish binds it; offline the playtest shows "골인"): a 4.8 m circle,
            // room for a team's majority at once (CrownFinish takes the box's narrower half as its radius). R616: beyond
            // the great door (WarRoomEnding); only if the door could not be opened, on the star platform as before.
            if (opened) WarRoomEnding(b);
            else b.Trigger<FinishZone>("Finish (goal)", new Vector3(-2.4f, 1.2f, 15.6f), new Vector3(2.4f, 4.6f, 20.4f)).Configure(true);
            WarRoomPath(root.transform, opened);

            var section = room.GetComponent<PawnRushSection>();
            if (section != null)
                section.Describe(WarRoomSection.Id, WarRoomName, PawnRushSection.SlotKind.Final, WarRoomEntry, opened ? WarRoomExit : WarRoomStarExit, 0f,
                                 WarRoomBeats());
            root.SetActive(true);
            return root;
        }

        // The one way, both teams': across the start floor, onto the conveyor at its south end, up the goal steps at its
        // north end, over the star platform, through the great door and onto the finish (R616; with the door shut for
        // good, into the star platform's capture zone as before). Each point names the beat of the leg that starts at it.
        static void WarRoomPath(Transform parent, bool opened)
        {
            var route = opened
                ? new[]
                {
                    WarRoomEntry, new Vector3(0f, 0f, -15.5f),
                    new Vector3(0f, .2f, -12.5f), new Vector3(0f, .2f, 12.5f),
                    new Vector3(0f, 1.2f, 16.5f), new Vector3(0f, 1.49f, 18f), new Vector3(0f, 1.49f, 22.8f),
                    new Vector3(0f, WarRoomGoalFloor, WarRoomExitGate), new Vector3(0f, WarRoomGoalFloor, WarRoomWallBack + 2.6f), WarRoomExit,
                }
                : new[]
                {
                    WarRoomEntry, new Vector3(0f, 0f, -15.5f),
                    new Vector3(0f, .2f, -12.5f), new Vector3(0f, .2f, 12.5f),
                    new Vector3(0f, 1.2f, 16.5f), WarRoomStarExit,
                };
            var beats = opened ? new[] { "1", "1", "2", "3", "3", "3", "3", "4", "4", "4" } : new[] { "1", "1", "2", "3", "3", "3" };
            var go = new GameObject("ProgressPath");
            go.transform.SetParent(parent, false);
            go.AddComponent<ProgressPath>().Configure(route, (Vector3[])route.Clone(), beats);
        }

        // Areas for the HUD and the CSV, the first that holds a pawn wins (a pawn is measured at its hips). The lower
        // library and the outside return stairs and galleries (out to x +-22.9) are one beat of their own.
        public static SectionBeat[] WarRoomBeats() => new[]
        {
            new SectionBeat("1 출발 (작전 지도)", new Vector3(-15f, -1f, -23f), new Vector3(15f, 8f, -12.5f)),
            new SectionBeat("2 컨베이어 · 공기 대포 · 포탑", new Vector3(-15f, -1f, -12.5f), new Vector3(15f, 8f, 12.5f)),
            new SectionBeat("3 골 계단 · 출구 문", new Vector3(-15f, -1f, 12.5f), new Vector3(15f, 8f, WarRoomWallBack)),
            new SectionBeat("4 엔딩 (임시 바닥)", new Vector3(-WarRoomEndingHalf - 1f, -1f, WarRoomWallBack),
                            new Vector3(WarRoomEndingHalf + 1f, 8f, WarRoomWallBack + WarRoomEndingLength + 1f)),
            new SectionBeat("아래층 · 돌아가는 계단", new Vector3(-23.5f, -9.5f, -23f), new Vector3(23.5f, 1.5f, 25.5f)),
        };
    }
}
