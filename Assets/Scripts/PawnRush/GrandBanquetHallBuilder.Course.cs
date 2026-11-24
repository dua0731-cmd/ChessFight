using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;
using Look = ChessFight.PawnRush.RoyalClockTowerLook;

namespace ChessFight.PawnRush
{
    // What makes the grand banquet hall a section of the round: eight checkpoints, the floors that end a run (the dance
    // floor and the orchestra pit, the pantry), the progress path, the meant gaps (the rides and the leaps), the beats and
    // the signs. A fall of more than 8 m anywhere is the character's own FallDistanceRespawn.
    public static partial class GrandBanquetHallBuilder
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

        // One on every floor the way stands on; each trigger covers its floor (R733), the respawn spots stay in a row across
        // the way on. The round makes them 200 .. 207.
        static void Checkpoints(CourseBuilder b)
        {
            CourseBuilder.CoverFloor(b.Checkpoint(0, V(0f, 0f, 2.6f), 90f, 5f), BuffetX1, 12.8f, 0f, ScreenZ - .2f);
            CourseBuilder.CoverFloor(b.Checkpoint(1, V(RunnerX, TableY, 20f), 0f, 10f), TableX0, TableX1, TableZ0, ClothZ1);
            CourseBuilder.CoverFloor(b.Checkpoint(2, V(2f, DaisY, 57.8f), 270f, 3.5f), HallX0, HallX1, DaisZ, TowerZ - 5.2f);
            CourseBuilder.CoverFloor(b.Checkpoint(3, V(-22.2f, StageY, CurtainZ + 2f), 270f, 6f), HallX0, StageX1, StageZ0, StageZ1);
            CourseBuilder.CoverFloor(b.Checkpoint(4, V(-27.5f, BuffetY, 15f), 180f, 7f), HallX0, BuffetX1, BuffetZ0, BuffetZ1);
            CourseBuilder.CoverFloor(b.Checkpoint(5, V(-22f, LoftY, 2.8f), 90f, 5f), LoftX0, LoftX1, 0f, ScreenZ);
            // R817: the great chandelier in the middle of the ropes (its trigger round the ring, from its floor up).
            CourseBuilder.CoverFloor(b.Checkpoint(6, V(0f, GreatY, GreatZ), 0f, 6f), -GreatR, GreatR, GreatZ - GreatR, GreatZ + GreatR);
            CourseBuilder.CoverFloor(b.Checkpoint(7, V(-4.4f, HeadY, TowerZ), 90f, 8f), -5f, 5f, TowerZ - 5f, TowerZ + 5f);
            CourseBuilder.CoverFloor(b.Checkpoint(8, V(0f, BalconyY, HallZ1 - 2.6f), 0f, 6f), -6f, 6f, TowerZ + .95f, HallZ1);
        }

        // The dance floor and the orchestra pit end a run (whoever falls from the dais, a queen, the curtain or the buffet
        // goes back to its checkpoint; nobody climbs a queen from the floor), as does the pantry (a waiter's tray carried in
        // drops its rider there). One far below everything.
        static void KillVolumes(CourseBuilder b)
        {
            b.Trigger<KillVolume>("BH kill volume (dance floor, south)", V(HallX0, -2f, ScreenZ + .2f), V(12.8f, .5f, TableZ0));
            b.Trigger<KillVolume>("BH kill volume (dance floor, north and the pit)", V(HallX0, -2f, TableZ0), V(TableX0, .5f, DaisZ));
            b.Trigger<KillVolume>("BH kill volume (the pantry)", V(26f, -2f, PantryZ0 + .3f), V(HallX1, 1.2f, DaisZ));
            // R817: a fall from the second line of ropes over the dais would land on its gallery (7.4 m: no fall by the 8 m
            // rule) and undo the waltz. A thin box 2.6 - 3.2 m over the gallery under the ropes: over a walker's head
            // (a jump reaches about 2.3 m), under a hanging pawn's feet at the bottom of its swing (about 4 m).
            b.Trigger<KillVolume>("BH kill volume (falls onto the dais)", V(-6f, DaisY + 2.6f, DaisZ - .5f), V(6f, DaisY + 3.2f, TowerZ - 5.2f));
            b.Trigger<KillVolume>("BH kill volume (far below)", V(-60f, -14f, -20f), V(60f, -12f, HallZ1 + 30f));
        }

        // ------------------------------------------------------------------ the progress path

        // One route for both teams, each point with the beat of the leg that starts there: the passage, the counter, the
        // second waiter's tray and stack, the runner, the west cloche (round its rim to the north), the dais to the boarding
        // place, couple 0's queen from the dais to the stage (its pose on the clock), the curtain's leading south pleat, the
        // fountain's north side, the leap to the loft, the west line of chandeliers, the tower's south-west, the balcony.
        static void ProgressRoute(CourseBuilder b)
        {
            var route = new List<Vector3>();
            var legs = new List<string>();
            void P(string beat, float x, float y, float z)
            {
                route.Add(V(x, y, z));
                legs.Add(beat);
            }

            P(BeatEntry, 0f, 0f, 0f);
            P(BeatEntry, 0f, 0f, 3f);
            P(BeatEntry, 15f, 0f, 3f);
            P(BeatEntry, 15f, 0f, 8f);
            P(BeatEntry, CounterX0, TrayY, 8f);
            P(BeatWaiters, CounterX1 - .3f, TrayY, 8f);
            P(BeatWaiters, TrayX - .4f, TrayY, 9.6f);
            P(BeatWaiters, TrayX - .4f, StackTop, 9.6f);
            P(BeatWaiters, TrayX - .4f, StackTop, 21f);
            P(BeatWaiters, TableX1 - .5f, TableY, 21.2f);
            P(BeatCloth, RunnerX, TableY, 22f);
            P(BeatCloth, RunnerX, TableY, ClothZ1 - .2f);
            float wx = ClocheX[0], rim = TableY + .06f + ClocheBand;
            P(BeatCloches, wx, TableY, ClocheZ - ClocheR - .3f);
            P(BeatCloches, wx, rim, ClocheZ - ClocheR - .3f);
            P(BeatCloches, wx, rim, ClocheZ - 1.8f);
            P(BeatCloches, wx - 1.8f, rim, ClocheZ);
            P(BeatCloches, wx, rim, ClocheZ + 1.8f);
            P(BeatCloches, wx, rim + ClocheLift, ClocheZ + 1.8f);
            P(BeatCloches, wx, rim + ClocheLift, ClocheZ + ClocheR - .1f);
            P(BeatWaltz, wx, DaisY, DaisZ + .6f);
            P(BeatWaltz, BanquetWaltz.BoardX, DaisY, DaisZ + .6f);
            P(BeatWaltz, BanquetWaltz.BoardX, DaisY, DaisZ + .05f);
            // Onto the queen standing still in front of the dais, and on her shoulders to the stage (8.36 s on the clock).
            var q = BanquetWaltz.Queen(0, 0.0);
            P(BeatWaltz, q.x, StageY, q.y + 1.2f);
            const float ride = 8.36f;
            for (int i = 1; i <= 24; i++)
            {
                q = BanquetWaltz.Queen(0, ride * i / 24f);
                P(BeatWaltz, q.x, StageY, q.y);
            }
            P(BeatWaltz, q.x - 1.2f, StageY, q.y);
            P(BeatCurtain, StageX1 - .5f, StageY, CurtainZ);
            P(BeatCurtain, CurtainX + .4f, StageY, CurtainZ - .5f);
            P(BeatCurtain, CurtainX + .4f, BassTop + .9f, CurtainZ - .5f);
            P(BeatCurtain, CurtainX + .4f, BassTop + .9f, CurtainOpenS);
            P(BeatCurtain, CurtainX + .4f, BuffetY, CurtainOpenS);
            var f = Fountain;
            P(BeatFountain, f.x, BuffetY, f.y + BowlR + .4f);
            P(BeatFountain, f.x, BowlTop, f.y + BowlR - .2f);
            float below = BowlTop;
            for (int k = 0; k < TierR.Length; k++)
            {
                P(BeatFountain, f.x, below, f.y + TierR[k] + .2f);
                P(BeatFountain, f.x, TierTop[k], f.y + TierR[k] + .2f);
                P(BeatFountain, f.x, TierTop[k], f.y + TierR[k] - .3f);
                below = TierTop[k];
            }
            int last = TierR.Length - 1;
            P(BeatFountain, f.x, TierTop[last], f.y - TierR[last] + .1f);
            P(BeatFountain, f.x, LoftY - 2.5f, ScreenZ + .15f);
            P(BeatFountain, f.x, LoftY, ScreenZ + .15f);
            P(BeatChandeliers, f.x, LoftY, ScreenZ - 1f);
            P(BeatChandeliers, LinesX[0], LoftY, ScreenZ - 1f);
            P(BeatChandeliers, LinesX[0], LoftY, ScreenZ - .1f);
            // Hanging under each rope's end: from its south end to its north end, where the next one's south end is; between
            // the lines, across the great chandelier.
            for (int k = 0; k < RopeAlong.Length; k++)
            {
                if (k == FirstLine)
                {
                    P(BeatChandeliers, LinesX[0], GreatY, GreatZ - GreatR + 1.2f);
                    P(BeatChandeliers, LinesX[0], GreatY, GreatZ + GreatR - 1.2f);
                }
                P(BeatChandeliers, LinesX[0], RopeLow[k] - 1.1f, ScreenZ + RopeAlong[k] - RopeEnd(k));
                P(BeatChandeliers, LinesX[0], RopeLow[k] - 1.1f, ScreenZ + RopeAlong[k] + RopeEnd(k));
            }
            // Down onto the pedestal in front of the tower, then up its four levels.
            P(BeatTower, LinesX[0], HeadY, LandZ);
            P(BeatTower, -1.5f, HeadY, LandZ);
            for (int k = 0; k < Levels; k++)
            {
                float face = TowerZ - (Levels - k) * GlassPitch * .5f, x = -1.5f + .5f * k, y = HeadY + LevelH * k;
                P(BeatTower, x, y, face - .15f);
                P(BeatTower, x, y + LevelH, face - .15f);
                P(BeatTower, x, y + LevelH, face + .4f);
            }
            P(BeatTower, 0f, BalconyY, TowerZ + .5f);
            P(BeatTower, 0f, BalconyY, TowerZ + 2f);
            P(BeatTower, 0f, BalconyY, HallZ1);

            var go = new GameObject("ProgressPath");
            go.transform.SetParent(b.parent, false);
            var points = route.ToArray();
            go.AddComponent<ProgressPath>().Configure(points, (Vector3[])points.Clone(), legs.ToArray());
        }

        // The places with no floor under the way on purpose (IntendedGap): riding a waiter's stack over the lane, a queen
        // over the dance floor, the curtain over the pit, the leap from the fountain to the loft, the chandeliers.
        static void MeantGaps(CourseBuilder b)
        {
            Gap(b, "BH meant gap (a waiter's stack)", V(TrayX - .5f, 5.5f, 15f), V(5f, 4f, 14f), "웨이터의 접시 탑에 실려 감");
            Gap(b, "BH meant gap (a queen)", V(-11.5f, 8.5f, 47.5f), V(20f, 5f, 20f), "춤추는 여왕의 어깨에 실려 감");
            Gap(b, "BH meant gap (the curtain)", V(CurtainX + .4f, 10.5f, 27.5f), V(3f, 5f, 26f), "열리는 막에 매달려 실려 감");
            Gap(b, "BH meant gap (fountain to loft)", V(Fountain.x, 18f, 7f), V(5f, 6f, 3f), "분수 꼭대기에서 회랑 앞면으로 뛰어 붙음");
            Gap(b, "BH meant gap (chandeliers)", V(0f, 19f, 34f), V(9f, 9f, 57f), "샹들리에에 매달려 건넘");
        }

        static void Gap(CourseBuilder b, string name, Vector3 center, Vector3 size, string why) =>
            Root(b, name, center).gameObject.AddComponent<IntendedGap>().Configure(size, why);

        // ------------------------------------------------------------------ beats and signs

        const string BeatEntry = "0 입구 · 배식실", BeatWaiters = "1 웨이터 행렬", BeatCloth = "2 식탁보 빼기", BeatCloches = "3 은 뚜껑",
                     BeatWaltz = "4 왈츠", BeatCurtain = "5 무대 막", BeatFountain = "6 초콜릿 분수", BeatChandeliers = "7 샹들리에",
                     BeatTower = "8 샴페인 탑";

        // Areas for the HUD, the CSV and the loading camera, in the section frame; the first that holds a pawn wins.
        public static SectionBeat[] Beats() => new[]
        {
            new SectionBeat(BeatWaiters, V(25.9f, 1.5f, 0f), V(32.5f, 9f, PantryZ0)),
            new SectionBeat(BeatCurtain, V(HallX0 - .5f, 6f, BuffetZ1), V(StageX1 + .5f, 22f, StageZ1 + 2f)),
            new SectionBeat(BeatFountain, V(HallX0 - .5f, 6f, BuffetZ0 - 1f), V(BuffetX1 + .5f, 19.5f, BuffetZ1)),
            new SectionBeat(BeatTower, V(-7f, HeadY - .5f, TowerZ - 5.5f), V(7f, BalconyY + 4f, HallZ1 + .5f)),
            new SectionBeat(BeatChandeliers, V(-32f, 14f, -.5f), V(32f, 28f, TowerZ - 4f)),
            new SectionBeat(BeatCloth, V(11f, 3.5f, TableZ0 - .5f), V(26.5f, 9f, ClothZ1)),
            new SectionBeat(BeatCloches, V(13f, 3.5f, ClothZ1), V(26.5f, 14f, DaisZ + .5f)),
            new SectionBeat(BeatWaltz, V(HallX0, -2f, ScreenZ), V(HallX1, 14f, HallZ1)),
            new SectionBeat(BeatEntry, V(-33f, -2f, -1f), V(33f, 9f, TableZ0)),
        };

        // "N. 장소: 할 일" signs in ink, over head height, no collider; each turned to whoever comes to it (yaw 0: read coming
        // from -z, 90: coming from -x, -90: coming from +x, 180: coming from +z). R817: where to go next is the first thing
        // each one says.
        static void Signs(CourseBuilder b)
        {
            var at = b.parent;
            Look.Sign(at, "대연회장", V(4f, 5.2f, 5.4f), 2.2f);
            Look.Sign(at, "→ 빨간 카펫을 따라 배식실로", V(4f, 3.6f, 5.5f), 1f);
            Look.Sign(at, "배식실: 카운터 끝에서 지나가는 쟁반에 올라탄다", V(24f, 5.2f, 3.9f), .9f);
            Look.Sign(at, "1. 웨이터 행렬: 쟁반 위에서 접시 탑을 올라 · 서쪽 긴 식탁으로 뛰어내린다 (늦으면 식료품실로)", V(26.2f, 7.6f, 15f), .8f, 90f);
            Look.Sign(at, "2. 식탁보 빼기: 식탁보가 빨개지면 초록 접시 위로 · 천 위에 있으면 배식실로 날아간다", V(20f, 8.6f, 19f), .9f);
            Look.Sign(at, "3. 은 뚜껑: 보라 띠를 올라 가장자리에 서면 들어 올려 준다 · 위에서 단(난간 틈)으로", V(20f, 13.6f, 50.5f), .9f);
            Look.Sign(at, "4. 왈츠 ← 카펫 끝의 난간 틈으로", V(10f, 13.2f, 58.4f), 1f, -90f);
            Look.Sign(at, "초록 등이 켜지면 여왕이 멈춘다 — 난간 틈에서 드레스로 뛰어 붙어(우클릭) 어깨로", V(BanquetWaltz.BoardX + 2.6f, 12.9f, 59.6f), .8f, -90f);
            Look.Sign(at, "무대 앞의 초록 등이 켜질 때 무대로 건너 내린다", V(BanquetWaltz.BoardX, 12.4f, 56.1f), .7f, 180f);
            Look.Sign(at, "5. 무대 막: 콘트라베이스보다 높이(3.5 m) 올라 매달린다 · 막이 열리면 뷔페까지", V(-21.5f, 11.4f, 44f), .9f, -90f);
            Look.Sign(at, "6. 초콜릿 분수: 어두운 아래쪽은 흘러내린다 — 멈추지 말고 밝은 띠까지 · 꼭대기 남쪽 끝에서 회랑 앞면으로 뛰어 붙기", V(-27.7f, 19.8f, 15.5f), .85f, 180f);
            Look.Sign(at, "7. 샹들리에: 끝이 가까이 오면 우클릭 · 이웃 줄이 마주 올 때 옮겨 잡기 · 가운데 큰 샹들리에에서 쉰다", V(-8f, 23.4f, 5.6f), .9f);
            Look.Sign(at, "8. 샴페인 탑: 샴페인이 흘러내리는 층에 매달리면 씻겨 내려간다 · 아래 턱에서 기다렸다 오르기", V(0f, 24.4f, TowerZ - 6f), .9f);
        }
    }
}
