using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;
using Look = ChessFight.PawnRush.RoyalClockTowerLook;

namespace ChessFight.PawnRush
{
    // What makes the grand banquet hall a section of the round: twelve checkpoints (R905: one before every pair of
    // chandeliers - the loft's end and the three candle beams more), the floors that end a run (the dance
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

        // One on every floor the way stands on. R825: each trigger is a box round its respawn point, big enough to be crossed
        // wherever the way comes onto that floor - offline the respawn is the bottom of the trigger's middle, and with the
        // triggers over whole floors (R733) that was inside the chocolate fountain (the user: "초코분수 체크포인트가 분수 안에
        // 있는지 태어나면 분수 속에 박히는 버그"), the great chandelier's column and behind the curtain. The six spots (online)
        // stay in a row across the way at the same point. The round makes them 200 .. 211.
        static void Checkpoints(CourseBuilder b)
        {
            // (number, point, yaw = the way on, spots' width, box across x along in the checkpoint's own frame)
            void Cp(int n, Vector3 at, float yaw, float width, float across, float along) =>
                CourseBuilder.CheckpointBox(b.Checkpoint(n, at, yaw, width), Vector3.zero, V(across, 3f, along));
            Cp(0, V(0f, 0f, 2.8f), 90f, 5f, 5.6f, 12f);                                     // the passage, the doorway's half
            Cp(1, V(RunnerX, TableY, 20.5f), 0f, 10f, TableX1 - TableX0, 7f);               // the table where the trays pass
            Cp(2, V(9.5f, DaisY, 58.9f), 270f, 3f, 5f, 34f);                                // the dais's gallery, its whole length
            Cp(3, V(StageX1 - 2.2f, StageY, StageZ0 + 10f), 270f, 6f, StageZ1 - StageZ0, 4.4f); // the stage before the curtain
            Cp(4, V((HallX0 + BuffetX1) * .5f, BuffetY, 15.3f), 180f, 7f, BuffetX1 - HallX0, 3.4f); // the buffet north of the bowl
            Cp(5, V(-22f, LoftY, 2.8f), 90f, 5f, 5.6f, 20f);                                 // the loft from its ladder on
            // R905: every candle beam, so that whoever falls with a loose chandelier tries the other one from where it
            // chose (a rider passing from rope to rope over a beam goes through its box too).
            // The loft's own is 20 m west, by its ladder: one more at its end by the ropes, where the first pair is chosen.
            int n = 6;
            Cp(n++, V(0f, LoftY, ScreenZ - 1.8f), 0f, 6f, 8f, 3.2f);
            bool great = false;
            void Great()
            {
                Cp(n++, V(0f, GreatY, GreatZ - 2.2f), 0f, 6f, 2f * GreatR, 4f);              // the great chandelier's south half
                great = true;
            }
            foreach (int k in BeamAfter)
            {
                if (!great && k >= FirstLine) Great();                                       // it hangs before rope FirstLine
                Cp(n++, V(0f, BeamY(k), BeamZ(k)), 0f, 6f, 2f * BeamX, BeamW);
            }
            if (!great) Great();
            Cp(n, V(0f, BalconyY, BalconyZ0 + 2.4f), 0f, 6f, 2f * BalconyX, 4.8f);          // the king's balcony
        }

        // How many checkpoints the hall has: the six up to the loft's ladder, the loft's end by the ropes, the great
        // chandelier, the candle beams, the balcony.
        public static int CheckpointCount => 9 + BeamAfter.Length;

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
            b.Trigger<KillVolume>("BH kill volume (falls onto the dais)", V(-BalconyX, DaisY + 2.6f, DaisZ - .5f), V(BalconyX, DaisY + 3.2f, BalconyZ0 - .05f));
            b.Trigger<KillVolume>("BH kill volume (far below)", V(-60f, -14f, -20f), V(60f, -12f, HallZ1 + 30f));
        }

        // ------------------------------------------------------------------ the progress path

        // One route for both teams, each point with the beat of the leg that starts there: the passage, the counter, the
        // second waiter's tray and stack (off before the pantry door), the runner, the west cloche lift up to the dais, the
        // dais to the boarding place, couple 0's queen from the dais to the stage (its pose on the clock), the curtain's
        // leading south pleat, the fountain's north side against the flow (R828: no ladders on it), the leap to the loft's
        // ladder, the west line of chandeliers with the great chandelier between, the balcony.
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
            float wx = ClocheX[0];
            P(BeatCloches, wx, TableY, ClocheZ - ClocheR - .4f);
            P(BeatCloches, wx, PlatterTop, ClocheZ - .5f);
            P(BeatCloches, wx, DaisY, ClocheZ - .5f);
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
            // Up the fountain's north side, the way in from the curtain: onto the bowl, then each tier straight up against the
            // flow and over its rim onto its ring; round the crown on the top, and from its south edge onto the loft's ladder.
            var f = Fountain;
            P(BeatFountain, f.x, BuffetY, f.y + BowlR + .5f);
            P(BeatFountain, f.x, BowlTop, f.y + BowlR - .3f);
            float below = BowlTop;
            for (int k = 0; k < TierR.Length; k++)
            {
                P(BeatFountain, f.x, below, f.y + TierR[k] + .25f);
                P(BeatFountain, f.x, TierTop[k], f.y + TierR[k] + .25f);
                P(BeatFountain, f.x, TierTop[k], f.y + TierR[k] - .4f);
                below = TierTop[k];
            }
            int last = TierR.Length - 1;
            P(BeatFountain, f.x - .85f, TierTop[last], f.y);
            P(BeatFountain, f.x, TierTop[last], f.y - TierR[last] + .2f);
            P(BeatFountain, f.x, TierTop[last] + .6f, ScreenZ + LadderOut + .2f);
            P(BeatFountain, f.x, LoftY, ScreenZ + LadderOut + .2f);
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
                // R905: onto the candle beam under the handover (the west end of it; either line goes on from it).
                if (System.Array.IndexOf(BeamAfter, k) >= 0) P(BeatChandeliers, LinesX[0], BeamY(k), BeamZ(k));
            }
            // Off the last rope onto the king's balcony, and on to the exit at its back.
            P(BeatBalcony, LinesX[0], BalconyY, LandZ);
            P(BeatBalcony, 0f, BalconyY, BalconyZ0 + 2f);
            P(BeatBalcony, 0f, BalconyY, HallZ1);

            var go = new GameObject("ProgressPath");
            go.transform.SetParent(b.parent, false);
            var points = route.ToArray();
            go.AddComponent<ProgressPath>().Configure(points, (Vector3[])points.Clone(), legs.ToArray());
        }

        // The places with no floor under the way on purpose (IntendedGap): riding a waiter's stack over the lane, stepping off
        // a cloche lift at the dais (it is down at the table when the grey box is built), a queen over the dance floor, the
        // curtain over the pit, the leap from the fountain to the loft's ladder, the chandeliers.
        static void MeantGaps(CourseBuilder b)
        {
            Gap(b, "BH meant gap (a waiter's stack)", V(TrayX - .5f, 5.5f, 15f), V(5f, 4f, 14f), "웨이터의 접시 탑에 실려 감");
            Gap(b, "BH meant gap (a cloche lift)", V(20f, 10.5f, ClocheZ + 1.2f), V(12f, 3f, 5f), "은 뚜껑 승강기로 단까지 올라감");
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
                     BeatBalcony = "8 왕의 발코니";

        // Areas for the HUD, the CSV and the loading camera, in the section frame; the first that holds a pawn wins.
        public static SectionBeat[] Beats() => new[]
        {
            new SectionBeat(BeatWaiters, V(25.9f, 1.5f, 0f), V(32.5f, 9f, PantryZ0)),
            new SectionBeat(BeatCurtain, V(HallX0 - .5f, 6f, BuffetZ1), V(StageX1 + .5f, 22f, StageZ1 + 2f)),
            new SectionBeat(BeatFountain, V(HallX0 - .5f, 6f, BuffetZ0 - 1f), V(BuffetX1 + .5f, 19.5f, BuffetZ1)),
            new SectionBeat(BeatBalcony, V(-BalconyX - 1f, BalconyY - .5f, BalconyZ0 - .5f), V(BalconyX + 1f, BalconyY + 4f, HallZ1 + .5f)),
            new SectionBeat(BeatChandeliers, V(-32f, 14f, -.5f), V(32f, 28f, BalconyZ0 - .5f)),
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
            Look.Sign(at, "1. 웨이터 행렬: 쟁반 위 접시 탑에 올라 · 식료품실 문 전에 서쪽 긴 식탁으로 뛰어내린다", V(26.2f, 7.6f, 15f), .8f, 90f);
            Look.Sign(at, "2. 식탁보 빼기: 식탁보가 빨개지면 초록 접시 위로 · 천 위에 있으면 배식실로 날아간다", V(20f, 8.6f, 19f), .9f);
            Look.Sign(at, "3. 은 뚜껑 승강기: 뚜껑이 열리면 접시에 올라탄다 · 단에서 다시 열리면 내린다", V(20f, 13.6f, 49.5f), .9f);
            Look.Sign(at, "4. 왈츠 ← 카펫 끝의 난간 틈으로", V(10f, 13.2f, 58.4f), 1f, -90f);
            Look.Sign(at, "초록 등이 켜지면 여왕이 멈춘다 — 난간 틈에서 드레스로 뛰어 붙어(우클릭) 어깨로", V(BanquetWaltz.BoardX + 2.6f, 12.9f, 59.6f), .8f, -90f);
            Look.Sign(at, "무대 앞의 초록 등이 켜질 때 무대로 건너 내린다", V(BanquetWaltz.BoardX, 12.4f, 56.1f), .7f, 180f);
            Look.Sign(at, "5. 무대 막: 콘트라베이스보다 높이(3.5 m) 올라 매달린다 · 막이 열리면 뷔페까지", V(-21.5f, 11.4f, 44f), .9f, -90f);
            Look.Sign(at, "6. 초콜릿 분수: 흘러내리는 초콜릿을 거슬러 한 층씩 오른다 · 꼭대기 남쪽에서 회랑의 사다리로 뛰어 붙기", V(-27.7f, 19.8f, 15.5f), .85f, 180f);
            // R905: the guessing stretch says so before the first choice.
            Look.Sign(at, "7. 샹들리에: 두 개 중 하나는 줄이 풀려 있다 — 타고 한가운데를 지날 때 내려앉는다 · 내려앉은 샹들리에는 이 판 내내 그대로", V(-8f, 23.4f, 5.6f), .9f);
            Look.Sign(at, "끝이 가까이 오면 우클릭 · 촛대 들보에서 줄을 고른다 · 촛불이 켜진 샹들리에는 누가 타고 건넌 것", V(-8f, 22.3f, 5.6f), .8f);
            Look.Sign(at, "8. 왕의 발코니: 마지막 샹들리에 끝에서 놓으면 도착", V(0f, BalconyY + 4.6f, BalconyZ0 - 2f), .9f);
        }
    }
}
