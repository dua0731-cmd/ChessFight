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
            float walk = WallT - Parapet - .4f, mid1 = Face1 + Parapet + (WallT - Parapet) * .5f;
            Cp(0, V(0f, 0f, 4.5f), 0f, 6f, 16f, 6f);                                                  // the glacis, in from the entry
            Cp(1, V(-16f, W1, mid1), 90f, 6f, walk, 20f);                                             // the outer wall's walk, at the ladders' tops
            Cp(2, V(SiegeX[0] + 3.75f, W1, mid1), 0f, 6f, 12f, walk);                                  // the walk in front of the siege towers
            Cp(3, V(25f, W2, Wall2Face(25f) + Parapet + (WallT - Parapet) * .5f), 270f, 6f, walk, 12f); // the middle wall, east
            Cp(4, V(-30f, W2, Wall2Face(-30f) + Parapet + (WallT - Parapet) * .5f), 270f, 6f, walk, 20f); // the middle wall, west
            Cp(5, V(-30f, W3, Wall3Face(-30f) + Parapet + (WallT - Parapet) * .5f), 90f, 6f, walk, 20f);  // the inner wall, west
            Cp(6, V(-6f, GateY, (GateHZ0 + GateHZ1) * .5f), 0f, 6f, 22f, 14f);                          // the inner gatehouse's roof
            Cp(7, V(0f, GateY, KeepZ - 4f), 0f, 4f, 2f * BridgeX, 6f);                                 // the keep bridge's end
        }

        // The gate passage (under the murder holes) is in MurderHoles. The lower ward (a fall from the outer wall's walk or a
        // siege tower), the ditch, the inner bailey and the gorges end a run; the design's stairs back up are not built.
        static void KillVolumes(CourseBuilder b)
        {
            b.Trigger<KillVolume>("RR kill volume (the lower ward)", V(-SpurX, -.5f, Face1 + WallT + .05f), V(SpurX, .6f, 52f * K));
            b.Trigger<KillVolume>("RR kill volume (the ditch)", V(-SpurX, DitchY - .5f, 44f * K), V(SpurX, DitchY + .6f, 72f * K));
            b.Trigger<KillVolume>("RR kill volume (the inner bailey)", V(-SpurX, BaileyY - .5f, 71f * K), V(SpurX, BaileyY + .6f, 102f * K));
            b.Trigger<KillVolume>("RR kill volume (the gorges)", V(-180f, -32f, -60f), V(180f, -30f, 210f));
        }

        // ------------------------------------------------------------------ the progress path

        // One route (rampart_layout.py the_way): the glacis and the mound to ladder 4, up it, east over the murder holes to
        // siege tower A, up inside it, its bridge, west to the board, across it round the rooks' lines, the west walk to the
        // rook crane, its ride, the inner wall east, the banner's two lengths, the roof, the bridge.
        static void ProgressRoute(CourseBuilder b)
        {
            var route = new List<Vector3>();
            var legs = new List<string>();
            void P(string beat, float x, float y, float z)
            {
                route.Add(V(x, y, z));
                legs.Add(beat);
            }
            float lad = T.Ladders[3], sx = SiegeX[0], z1 = SiegeZ1(0), mz = Face1 - MoundDepth, mid1 = Face1 + Parapet + (WallT - Parapet) * .5f;
            P(BeatEntry, 0f, 0f, .75f);
            P(BeatEntry, -8f, 0f, mz - 6f);
            P(BeatLadders, lad, 0f, mz - 5.5f);
            P(BeatLadders, lad, Mound, mz + 1f);
            P(BeatLadders, lad, Mound, LadderFootZ - .8f);
            P(BeatLadders, lad, W1 + SillH, Face1 + .6f);
            P(BeatLadders, lad, W1, Face1 + 3f);
            P(BeatHoles, GateX0 - .8f, W1, mid1);
            P(BeatHoles, GateX1 + .8f, W1, mid1);
            P(BeatSiege, sx, W1, mid1);
            P(BeatSiege, sx, Floors[0], SiegeZ0);
            P(BeatSiege, sx, Floors[3], z1 - .5f);
            P(BeatSiege, sx, W2, Wall2Face(sx) + 3.8f);
            P(BeatBoard, TowerX1 + 2.2f, W2, EntryZ);
            P(BeatBoard, TowerX1 - .6f, BoardY, EntryZ);
            P(BeatBoard, BoardX0 + 6.5f * Square, BoardY, EntryZ);
            P(BeatBoard, BoardX0 + 5.5f * Square, BoardY, BoardZ0 + 4.5f * Square);
            P(BeatBoard, BoardX0 + 3.5f * Square, BoardY, BoardZ0 + 4.5f * Square);
            P(BeatBoard, BoardX0 + 1.5f * Square, BoardY, ExitZ);
            P(BeatBoard, TowerX0 + .6f, BoardY, ExitZ);
            P(BeatCranes, TowerX0 - 2.2f, W2, ExitZ);
            P(BeatCranes, RookCraneX + 1.5f, W2, Wall2Inner(RookCraneX) - 1.2f);
            P(BeatCranes, RookCraneX, W2, RcLowZ);
            P(BeatCranes, RookCraneX, W3, RcLowZ);
            P(BeatCranes, RookCraneX, W3, RcHighZ);
            P(BeatCranes, RookCraneX + 1.5f, W3, Wall3Face(RookCraneX) + 4.5f);
            float bz = (BannerZ0 + BannerZ1) * .5f;
            P(BeatBanner, GateHX0 - 3f, W3, bz);
            P(BeatBanner, GateHX0 - LedgeDepth - .45f, LedgeY, bz);
            P(BeatBanner, GateHX0 - .45f, LedgeY, bz);
            P(BeatBanner, GateHX0 - .45f, GateY, bz);
            P(BeatRoof, GateHX0 + 1.5f, GateY, bz);
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
            Gap(b, "RR meant gap (the siege ladders)", V(-16f, 4.5f, 14.5f), V(24f, 7f, 4f), "공성 사다리를 올라 총안 턱으로");
            Gap(b, "RR meant gap (a siege tower)", V(SiegeX[0] + 3.75f, 10f, 44f), V(14f, 14f, 34f), "공성탑에 실려 아래뜰을 건너고 안에서 오름");
            Gap(b, "RR meant gap (the cranes)", V((DoorX0 + DoorX1) * .5f, 18f, 92f), V(20f, 16f, 26f), "기중기의 룩이나 나이트로 해자를 건넘");
            Gap(b, "RR meant gap (the banner)", V(GateHX0 - 1.2f, LedgeY, (BannerZ0 + BannerZ1) * .5f), V(3.4f, 9f, BannerW + 1f), "왕의 깃발 두 폭을 오름");
        }

        static void Gap(CourseBuilder b, string name, Vector3 center, Vector3 size, string why) =>
            Root(b, name, center).gameObject.AddComponent<IntendedGap>().Configure(size, why);

        // ------------------------------------------------------------------ beats and signs

        const string BeatEntry = "0 성 앞 비탈", BeatLadders = "1 공성 사다리", BeatHoles = "2 살인 구멍", BeatSiege = "3 공성탑",
                     BeatBoard = "4 룩의 훈련장", BeatCranes = "5 룩 · 나이트 기중기", BeatBanner = "6 왕의 깃발", BeatRoof = "7 본성 다리";

        // Areas for the HUD, the CSV and the loading camera, in the section frame; the first that holds a pawn wins.
        public static SectionBeat[] Beats() => new[]
        {
            new SectionBeat(BeatRoof, V(GateHX0 - .8f, GateY - .5f, GateHZ0), V(GateHX1 + .8f, GateY + 8f, KeepZ + .5f)),
            new SectionBeat(BeatBanner, V(GateHX0 - 6f, W3 - .5f, GateHZ0), V(GateHX0 + .3f, GateY + .5f, GateHZ1)),
            new SectionBeat(BeatCranes, V(-60f, W2 - .5f, 46f * K), V(-18.75f, W3 + 10f, 78f * K)),
            new SectionBeat(BeatBoard, V(TowerX0 - 1.5f, W2 - .5f, TowerZ0), V(TowerX1 + 9f, W2 + 6f, TowerZ1)),
            new SectionBeat(BeatSiege, V(15.75f, -1f, 12f * K), V(36f, 20f, 48f * K)),
            new SectionBeat(BeatHoles, V(GateX0 - 1.5f, W1 - .5f, Face1), V(GateX1 + .75f, W1 + 4f, Face1 + WallT + .75f)),
            new SectionBeat(BeatLadders, V(-36f, -1f, 0f), V(GateX0 - 1.5f, W1 + 5f, Face1 + WallT + .75f)),
            new SectionBeat(BeatEntry, V(-SpurX, -1f, -14f * K), V(SpurX, 8f, Face1)),
        };

        // "N. 장소: 할 일" signs over head height, no collider; each turned to whoever comes to it (yaw 0: read coming from
        // -z, 90: coming from -x, -90: coming from +x, 180: coming from +z).
        static void Signs(CourseBuilder b)
        {
            var at = b.parent;
            float mid1 = Face1 + Parapet + (WallT - Parapet) * .5f;
            Look.Sign(at, "룩 성벽길", V(0f, 6f, 6f), 3f);
            Look.Sign(at, "1. 공성 사다리: 수비병이 밀고 지나간 사다리가 다시 서면 바로 오른다 · 발치 남쪽(사다리가 넘어지는 쪽)에 서 있지 말 것",
                      V(-16f, 11f, Face1 - MoundDepth - .5f), 1.1f);
            Look.Sign(at, "2. 살인 구멍: 닫힌 색의 칸으로 · 주황으로 덜컹이는 칸은 곧 열린다", V(GateX0 - 4f, W1 + 4.5f, mid1), 1f, 90f);
            Look.Sign(at, "3. 공성탑: 붙어 있을 때 뒤로 타서 · 구르는 동안 안 사다리 셋을 오른다 · 다리가 내려가면 건넌다", V(SiegeX[0] + 3.75f, W1 + 5f, Face1 + 3f), 1f);
            Look.Sign(at, "4. 룩의 훈련장: 빛나는 줄 · 룩이 미끄러지는 줄 위에 서지 않는다", V(TowerX1 + 4f, W2 + 4.4f, EntryZ), 1.1f, -90f);
            Look.Sign(at, "5. 기중기: 룩(왼쪽)은 멈췄을 때 머리에 탄다 · 나이트(오른쪽)는 돌아설 때 갈기를 잡고(우클릭) 안 성벽에서 돌아설 때 놓는다",
                      V(-25.5f, W2 + 4.6f, Wall2Face(-25.5f) + 6f), 1f, -90f);
            Look.Sign(at, "6. 왕의 깃발: 작은 깃발이 펄럭이면 오르지 말 것 · 젖혀지는 깃발에 매달려 있으면 떨어진다 · 턱에서는 깃발 옆으로 비켜 기다린다",
                      V(-24f, W3 + 5f, Wall3Face(-24f) + 6f), 1f, 90f);
            Look.Sign(at, "본성 다리 → 출구", V(0f, GateY + 4.5f, GateHZ1 - 3f), 1.3f);
        }
    }
}
