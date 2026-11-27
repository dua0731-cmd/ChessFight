using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;
using Look = ChessFight.PawnRush.RoyalClockTowerLook;
using T = ChessFight.PawnRush.RookRampartTimes;

namespace ChessFight.PawnRush
{
    // What makes the rooks' rampart walk a section of the round: eight checkpoints (the design's), the floors that end a run
    // (the gate passage, the lower ward, the ditch, the inner bailey, the gorges), the progress path, the meant gaps (the
    // climbs and the rides), the beats and the signs. A fall of more than 8 m anywhere is the character's own
    // FallDistanceRespawn.
    public static partial class RookRampartBuilder
    {
        static void Course(CourseBuilder b)
        {
            Checkpoints(b);
            KillVolumes(b);
            ProgressRoute(b);
            MeantGaps(b);
            Signs(b);
        }

        // ------------------------------------------------------------------ checkpoints and kill volumes

        // rampart_layout.py CHECKPOINTS; each trigger a box over the floor round its respawn spots (the banquet hall's way,
        // R825). The round makes them 0 .. 7 (slot 0's).
        static void Checkpoints(CourseBuilder b)
        {
            // (number, point, yaw = the way on, spots' width, box across x along in the checkpoint's own frame)
            void Cp(int n, Vector3 at, float yaw, float width, float across, float along) =>
                CourseBuilder.CheckpointBox(b.Checkpoint(n, at, yaw, width), Vector3.zero, V(across, 3f, along));
            Cp(0, V(0f, 0f, 3f), 0f, 6f, 12f, 5f);                                                   // the glacis, in from the entry
            Cp(1, V(-11f, W1, 15f), 90f, 5f, WallT - Parapet - .2f, 14f);                            // the outer wall's walk, at the ladders' tops
            Cp(2, V(15.5f, W1, 15f), 0f, 5f, 8f, WallT - Parapet - .2f);                             // the walk in front of the siege towers
            Cp(3, V(16f, W2, Wall2Face(16f) + 4f), 270f, 5f, WallT - Parapet - .4f, 8f);              // the middle wall, east
            Cp(4, V(-20f, W2, Wall2Face(-20f) + 4f), 270f, 5f, WallT - Parapet - .4f, 14f);           // the middle wall, west
            Cp(5, V(-20f, W3, Wall3Face(-20f) + 4f), 90f, 5f, WallT - Parapet - .4f, 14f);            // the inner wall, west
            Cp(6, V(-4f, GateY, 76f), 0f, 6f, 16f, 10f);                                              // the inner gatehouse's roof
            Cp(7, V(0f, GateY, KeepZ - 3f), 0f, 3f, 2f * BridgeX, 5f);                                // the keep bridge's end
        }

        // The gate passage (under the murder holes) is in MurderHoles. The lower ward (a fall from the outer wall's walk or a
        // siege tower), the ditch, the inner bailey and the gorges end a run; the design's stairs back up are not built.
        static void KillVolumes(CourseBuilder b)
        {
            b.Trigger<KillVolume>("RR kill volume (the lower ward)", V(-SpurX, -.5f, Face1 + WallT + .05f), V(SpurX, .6f, 52f));
            b.Trigger<KillVolume>("RR kill volume (the ditch)", V(-SpurX, DitchY - .5f, 44f), V(SpurX, DitchY + .6f, 72f));
            b.Trigger<KillVolume>("RR kill volume (the inner bailey)", V(-SpurX, BaileyY - .5f, 71f), V(SpurX, BaileyY + .6f, 102f));
            b.Trigger<KillVolume>("RR kill volume (the gorges)", V(-120f, -22f, -40f), V(120f, -20f, 140f));
        }

        // ------------------------------------------------------------------ the progress path

        // One route (rampart_layout.py the_way, with the board level with the walk): the glacis to ladder 4, up it, east over
        // the murder holes to siege tower A, up inside it, its bridge, west to the board, across it round the rooks' lines,
        // the west walk to the rook crane, its ride, the inner wall east, the banner's two lengths, the roof, the bridge.
        static void ProgressRoute(CourseBuilder b)
        {
            var route = new List<Vector3>();
            var legs = new List<string>();
            void P(string beat, float x, float y, float z)
            {
                route.Add(V(x, y, z));
                legs.Add(beat);
            }
            float lad = T.Ladders[3], sx = SiegeX[0], z1 = SiegeZ1(0);
            P(BeatEntry, 0f, 0f, .5f);
            P(BeatEntry, -6f, 0f, 6f);
            P(BeatLadders, lad, 0f, LadderFootZ - .6f);
            P(BeatLadders, lad, LadderClimb, Face1 + .4f);
            P(BeatLadders, lad, W1, 13.5f);
            P(BeatHoles, GateX0 - .5f, W1, 15f);
            P(BeatHoles, GateX1 + .5f, W1, 15f);
            P(BeatSiege, sx, W1, 16.5f);
            P(BeatSiege, sx, Floors[0], SiegeZ0);
            P(BeatSiege, sx, Floors[2], z1 - .5f);
            P(BeatSiege, sx, W2, Wall2Face(sx) + 2.5f);
            P(BeatBoard, TowerX1 + 1.5f, W2, EntryZ);
            P(BeatBoard, TowerX1 - .5f, BoardY, EntryZ);
            P(BeatBoard, BoardX0 + 6.5f * Square, BoardY, EntryZ);
            P(BeatBoard, BoardX0 + 5.5f * Square, BoardY, BoardZ0 + 4.5f * Square);
            P(BeatBoard, BoardX0 + 3.5f * Square, BoardY, BoardZ0 + 4.5f * Square);
            P(BeatBoard, BoardX0 + 1.5f * Square, BoardY, ExitZ);
            P(BeatBoard, TowerX0 + .5f, BoardY, ExitZ);
            P(BeatCranes, TowerX0 - 1.5f, W2, ExitZ);
            P(BeatCranes, RookCraneX + 1f, W2, Wall2Inner(RookCraneX) - .8f);
            P(BeatCranes, RookCraneX, W2, RcLowZ);
            P(BeatCranes, RookCraneX, W3, RcLowZ);
            P(BeatCranes, RookCraneX, W3, RcHighZ);
            P(BeatCranes, RookCraneX + 1f, W3, Wall3Face(RookCraneX) + 3f);
            float bz = (BannerZ0 + BannerZ1) * .5f;
            P(BeatBanner, GateHX0 - 2.2f, W3, bz);
            P(BeatBanner, GateHX0 - LedgeDepth - .45f, LedgeY, bz);
            P(BeatBanner, GateHX0 - .45f, LedgeY, bz);
            P(BeatBanner, GateHX0 - .45f, GateY, bz);
            P(BeatRoof, GateHX0 + 1f, GateY, bz);
            P(BeatRoof, 0f, GateY, GateHZ1);
            P(BeatRoof, 0f, GateY, KeepZ);

            var go = new GameObject("ProgressPath");
            go.transform.SetParent(b.parent, false);
            var points = route.ToArray();
            go.AddComponent<ProgressPath>().Configure(points, (Vector3[])points.Clone(), legs.ToArray());
        }

        // The places with no floor under the way on purpose: up a ladder, the siege tower over the lower ward and its climb,
        // the cranes over the ditch, the banner's lengths.
        static void MeantGaps(CourseBuilder b)
        {
            Gap(b, "RR meant gap (the siege ladders)", V(-11f, 2.5f, 10f), V(18f, 5f, 3f), "공성 사다리를 올라 총안 턱으로");
            Gap(b, "RR meant gap (a siege tower)", V(15.75f, 6.5f, 28f), V(9f, 9f, 22f), "공성탑에 실려 아래뜰을 건너고 안에서 오름");
            Gap(b, "RR meant gap (the cranes)", V(-27.25f, 12f, 61f), V(12f, 12f, 16f), "기중기의 룩이나 나이트로 해자를 건넘");
            Gap(b, "RR meant gap (the banner)", V(GateHX0 - .8f, 17f, (BannerZ0 + BannerZ1) * .5f), V(2.6f, 6.5f, 4f), "왕의 깃발 두 폭을 오름");
        }

        static void Gap(CourseBuilder b, string name, Vector3 center, Vector3 size, string why) =>
            Root(b, name, center).gameObject.AddComponent<IntendedGap>().Configure(size, why);

        // ------------------------------------------------------------------ beats and signs

        const string BeatEntry = "0 성 앞 비탈", BeatLadders = "1 공성 사다리", BeatHoles = "2 살인 구멍", BeatSiege = "3 공성탑",
                     BeatBoard = "4 룩의 훈련장", BeatCranes = "5 룩 · 나이트 기중기", BeatBanner = "6 왕의 깃발", BeatRoof = "7 본성 다리";

        // Areas for the HUD, the CSV and the loading camera, in the section frame; the first that holds a pawn wins.
        public static SectionBeat[] Beats() => new[]
        {
            new SectionBeat(BeatRoof, V(-10.5f, GateY - .5f, 70f), V(10.5f, GateY + 6f, KeepZ + .5f)),
            new SectionBeat(BeatBanner, V(-14f, W3 - .5f, 70f), V(GateHX0 + .2f, GateY + .5f, GateHZ1)),
            new SectionBeat(BeatCranes, V(-40f, W2 - .5f, 46f), V(-12.5f, W3 + 8f, 78f)),
            new SectionBeat(BeatBoard, V(TowerX0 - 1f, W2 - .5f, TowerZ0), V(TowerX1 + 6f, W2 + 4f, TowerZ1)),
            new SectionBeat(BeatSiege, V(10.5f, -1f, 12f), V(24f, 14f, 48f)),
            new SectionBeat(BeatHoles, V(-1f, W1 - .5f, 11f), V(10.5f, W1 + 3f, 18.5f)),
            new SectionBeat(BeatLadders, V(-24f, -1f, 4f), V(-1f, W1 + 4f, 18.5f)),
            new SectionBeat(BeatEntry, V(-46f, -1f, -14f), V(46f, 6f, 11f)),
        };

        // "N. 장소: 할 일" signs over head height, no collider; each turned to whoever comes to it (yaw 0: read coming from
        // -z, 90: coming from -x, -90: coming from +x, 180: coming from +z).
        static void Signs(CourseBuilder b)
        {
            var at = b.parent;
            Look.Sign(at, "룩 성벽길", V(0f, 5f, 4f), 2.2f);
            Look.Sign(at, "1. 공성 사다리: 수비병이 밀고 지나간 사다리가 다시 서면 바로 오른다 · 발치 남쪽(사다리가 넘어지는 쪽)에 서 있지 말 것", V(-11f, 7.6f, 8f), .8f);
            Look.Sign(at, "2. 살인 구멍: 닫힌 색의 칸으로 · 주황으로 덜컹이는 칸은 곧 열린다", V(-2.5f, W1 + 3.6f, 15f), .8f, 90f);
            Look.Sign(at, "3. 공성탑: 붙어 있을 때 뒤로 타서 · 구르는 동안 안 사다리 둘을 오른다 · 다리가 내려가면 건넌다", V(15.5f, W1 + 4.2f, 13.4f), .8f);
            Look.Sign(at, "4. 룩의 훈련장: 빛나는 줄 · 룩이 미끄러지는 줄 위에 서지 않는다", V(TowerX1 + 2.5f, W2 + 3.4f, EntryZ), .85f, -90f);
            Look.Sign(at, "5. 기중기: 룩(왼쪽)은 멈췄을 때 머리에 탄다 · 나이트(오른쪽)는 돌아설 때 갈기를 잡고(우클릭) 안 성벽에서 돌아설 때 놓는다",
                      V(-17f, W2 + 3.6f, Wall2Face(-17f) + 4f), .75f, -90f);
            Look.Sign(at, "6. 왕의 깃발: 작은 깃발이 펄럭이면 오르지 말 것 · 턱에서는 깃발 옆으로 비켜 기다린다", V(-16f, W3 + 4f, Wall3Face(-16f) + 4f), .8f, 90f);
            Look.Sign(at, "본성 다리 → 출구", V(0f, GateY + 3.6f, 80f), 1f);
        }
    }
}
