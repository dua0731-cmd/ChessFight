using ChessFight.Gameplay;
using UnityEngine;
using Look = ChessFight.PawnRush.RoyalClockTowerLook;

namespace ChessFight.PawnRush
{
    // What makes the royal clock tower a section of the round: ten checkpoints, the kill volumes under the three pits and
    // round the tower, the progress path, the meant gaps (the piers, the ropes, the drum), the beats and the signs.
    public static partial class RoyalClockTowerBuilder
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

        // One on every floor the way stands on; each trigger covers its whole floor (R733), the respawn spots stay in a row.
        // The round makes them 200 .. 209. CP6 (the west floor) is passed over by whoever takes the drum's high road; CP7
        // comes next either way.
        static void Checkpoints(CourseBuilder b)
        {
            b.Checkpoint(0, V(0f, 0f, 3f), 0f, 20f);
            CourseBuilder.CoverFloor(b.Checkpoint(1, V(0f, YardY, 13f), 0f, 22f), -22f, 22f, 10f, 20.5f);
            CourseBuilder.CoverFloor(b.Checkpoint(2, V(-1f, GalleryY, 29f), 90f, 9f), -6f, 2f, 24f, 33.7f);
            CourseBuilder.CoverFloor(b.Checkpoint(3, V(12f, FloorY, 30f), 0f, 16f), 2f, 22f, 24f, PierZ0 - .5f);
            CourseBuilder.CoverFloor(b.Checkpoint(4, V(12f, IslandY, 41.8f), 0f, 12f), 5f, 19f, IslandZ0 + .4f, IslandZ1);
            CourseBuilder.CoverFloor(b.Checkpoint(5, V(13f, FloorY, 55f), 0f, 16f), 2f, 22f, 52f, 58f);
            CourseBuilder.CoverFloor(b.Checkpoint(6, V(-13f, FloorY, 66f), 0f, 12f), -22f, -6f, 58.3f, 75f);
            CourseBuilder.CoverFloor(b.Checkpoint(7, V(-4f, CourtY, 82f), 0f, 20f), -22f, 22f, 76.5f, 89.5f);
            CourseBuilder.CoverFloor(b.Checkpoint(8, V(0f, BellY, 96f), 0f, 12f), -8f, 8f, 90f, 100f);
            CourseBuilder.CoverFloor(b.Checkpoint(9, V(0f, ExitY, 116f), 0f, 12f), -8f, 8f, 112f, ExitZ);
        }

        // The pits (the machine pit north of the movement, the piers' gap, the ropes' pit, the drum's): one box under the
        // floors' level from the movement to the clock court. Outside the tower north of the court there is only sky: a box
        // under the court's level. And one far below everything.
        static void KillVolumes(CourseBuilder b)
        {
            b.Trigger<KillVolume>("CT kill volume (pits)", V(-22f, -6f, 34f), V(22f, .5f, 76f));
            b.Trigger<KillVolume>("CT kill volume (outside the tower)", V(-40f, -6f, 90.4f), V(40f, 9f, ExitZ + 20f));
            b.Trigger<KillVolume>("CT kill volume (far below)", V(-40f, Deep - 4f, -10f), V(40f, Deep - 1f, ExitZ + 20f));
        }

        // ------------------------------------------------------------------ the progress path

        // One route for both teams: the third weight, the drop's ledge, the east side of the first pier, the middle rope, the
        // drum's low road, the hammer wall at x -12, the dial's east pilasters and the east side face. Each point with the
        // beat of the leg that starts there.
        static void ProgressRoute(CourseBuilder b)
        {
            var route = new System.Collections.Generic.List<Vector3>();
            var legs = new System.Collections.Generic.List<string>();
            void P(string beat, float x, float y, float z)
            {
                route.Add(V(x, y, z));
                legs.Add(beat);
            }

            P(BeatEntry, 0f, 0f, 0f);
            P(BeatWall, 0f, 0f, 9.9f);
            P(BeatWeights, 0f, YardY, 10.1f);
            // The third lane: onto the low weight (down), ride, onto the high weight (down), ride, off onto the gallery.
            P(BeatWeights, WeightX[2], YardY, LowZ0 - .1f);
            P(BeatWeights, WeightX[2], LowDown, LowZ0);
            P(BeatWeights, WeightX[2], LowDown, (LowZ0 + LowZ1) * .5f);
            P(BeatWeights, WeightX[2], LowDown + WeightLift, (LowZ0 + LowZ1) * .5f);
            P(BeatWeights, WeightX[2], LowDown + WeightLift, LowZ1 + .1f);
            P(BeatWeights, WeightX[2], HighDown, HighZ0);
            P(BeatWeights, WeightX[2], HighDown, (HighZ0 + HighZ1) * .5f);
            P(BeatWeights, WeightX[2], HighDown + WeightLift, (HighZ0 + HighZ1) * .5f);
            P(BeatWeights, WeightX[2], GalleryY, 24.3f);
            P(BeatDrop, WeightX[2], GalleryY, 29f);
            P(BeatDrop, 2f, GalleryY, 31.5f);
            P(BeatDrop, 3.4f, 8.5f, 31.5f);
            P(BeatPiers, 5.4f, FloorY, 31.5f);
            P(BeatPiers, 9.6f, FloorY, PierZ0 - .1f);
            P(BeatPiers, 9.6f, FloorY + .6f, PierZ0 - .05f);
            P(BeatPiers, 10.45f, FloorY + .6f, PierZ0 - .05f);
            P(BeatPiers, 10.65f, IslandY + .4f, PierZ1 - .2f);
            P(BeatRopes, 10.9f, IslandY, IslandZ0 + .6f);
            P(BeatRopes, 12f, IslandY, IslandZ1 - .1f);
            float pivotY = IslandY + 1.1f + RopeLength * Mathf.Cos(RopeSwing * Mathf.Deg2Rad);
            for (int i = 1; i <= 9; i++)
            {
                float a = (-RopeSwing + 2f * RopeSwing * i / 10f) * Mathf.Deg2Rad;
                P(BeatRopes, 12f, pivotY - RopeLength * Mathf.Cos(a) - 1.1f, RopePivotZ + RopeLength * Mathf.Sin(a));
            }
            P(BeatDrum, 12f, FloorY, 52.3f);
            P(BeatDrum, 12f, FloorY, 57f);
            P(BeatDrum, 10.5f, FloorY, 65.3f);
            // Hanging on the drum: over the first comb, under the second, off at the west tongue (bearing, hips' height).
            float[] bearing = { 350f, 390f, 430f, 460f, 495f, 520f, 540f }, hips = { 5.5f, 7.4f, 8.9f, 8.3f, 6.7f, 5.9f, 5.5f };
            for (int k = 0; k < bearing.Length - 1; k++)
                for (int i = 0; i < 5; i++)
                {
                    float u = i / 5f;
                    var at = OnDrum(Mathf.Lerp(bearing[k], bearing[k + 1], u), DrumRadius + .3f, Mathf.Lerp(hips[k], hips[k + 1], u) - .5f);
                    P(BeatDrum, at.x, at.y, at.z);
                }
            P(BeatHammer, -3.4f, FloorY, DrumZ);
            P(BeatHammer, -7f, FloorY, DrumZ);
            P(BeatHammer, -12f, FloorY, 75f);
            // The ratchet wall: ride a block of each row, climbing across to the next one.
            P(BeatHammer, -12f, RatchetLow[0], 75.5f);
            P(BeatHammer, -12f, RatchetLow[0] + RatchetRise[0], 75.5f);
            P(BeatHammer, -10f, RatchetLow[1], 75.5f);
            P(BeatHammer, -10f, RatchetLow[1] + RatchetRise[1], 75.5f);
            P(BeatHammer, -12f, RatchetLow[2], 75.5f);
            P(BeatHammer, -12f, CourtY, 75.9f);
            P(BeatDial, -12f, CourtY, 76.6f);
            // Onto the plinth, take the minute hand at six and ride it up the west side to noon.
            P(BeatDial, 0f, CourtY, 85.8f);
            P(BeatDial, 0f, PlinthTop, 86.6f);
            for (int i = 1; i <= 6; i++)
            {
                float a = (180f + 30f * i) * Mathf.Deg2Rad, r = MinuteLength - .8f;
                P(BeatDial, DialX + r * Mathf.Sin(a), DialY + r * Mathf.Cos(a) - .5f, 87.1f);
            }
            P(BeatDial, 0f, NoonLedgeTop, 88.6f);
            P(BeatDial, 0f, NoonLedgeTop, 89.3f);
            P(BeatDial, 0f, BellY, 89.6f);
            P(BeatRoof, 0f, BellY, 90.4f);
            P(BeatRoof, 0f, BellY, 96f);
            P(BeatRoof, 0f, BellY, 100f);
            P(BeatRoof, 0f, ExitY, 112f);
            P(BeatRoof, 0f, ExitY, ExitZ);

            var go = new GameObject("ProgressPath");
            go.transform.SetParent(b.parent, false);
            var points = route.ToArray();
            go.AddComponent<ProgressPath>().Configure(points, (Vector3[])points.Clone(), legs.ToArray());
        }

        // The three places with no floor under the way on purpose (IntendedGap): hanging on a pier over the pit, on a rope,
        // on the drum. Everywhere else the way is over a floor or straight up a wall.
        static void MeantGaps(CourseBuilder b)
        {
            Gap(b, "CT meant gap (corner piers)", V(12f, 6.5f, (PierZ0 + IslandZ0) * .5f + .3f), V(14f, 9f, IslandZ0 - PierZ0 + 1.4f), "기둥의 옆면에 매달려 건넘");
            Gap(b, "CT meant gap (bell ropes)", V(12f, 7f, 48f), V(16f, 12f, 8.8f), "종 줄 그네");
            Gap(b, "CT meant gap (drum)", V(DrumX, 7f, DrumZ + .5f), V(17f, 12f, 19.5f), "원통에 매달려 실려 감");
        }

        static void Gap(CourseBuilder b, string name, Vector3 center, Vector3 size, string why) =>
            Root(b, name, center).gameObject.AddComponent<IntendedGap>().Configure(size, why);

        // ------------------------------------------------------------------ beats and signs

        const string BeatEntry = "0 입구 마당", BeatWall = "1 배우는 벽", BeatWeights = "2 짝 추", BeatDrop = "3 낙하 벽",
                     BeatPiers = "4 모서리 기둥", BeatRopes = "5 종 줄", BeatDrum = "6 편종 원통", BeatHammer = "7 망치 벽",
                     BeatDial = "8 대시계판", BeatRoof = "9 구리 지붕";

        // Areas for the HUD, the CSV and the loading camera, in the section frame; the first that holds a pawn wins. Each
        // reaches from the pit to well over its walls, so a pawn on a wall or in the air over a pit is still in its beat.
        public static SectionBeat[] Beats() => new[]
        {
            new SectionBeat(BeatEntry, V(-13f, -9f, -.5f), V(13f, 8f, 8f)),
            new SectionBeat(BeatWall, V(-23f, -9f, 8f), V(23f, 12f, 12f)),
            new SectionBeat(BeatDrop, V(-4f, 4f, 24f), V(23f, 20f, 34f)),
            new SectionBeat(BeatWeights, V(-23f, -9f, 12f), V(23f, 22f, 34f)),
            new SectionBeat(BeatPiers, V(1f, -9f, 34f), V(23f, 18f, IslandZ0 + .4f)),
            new SectionBeat(BeatRopes, V(1f, -9f, IslandZ0 + .4f), V(23f, 24f, 52f)),
            new SectionBeat(BeatHammer, V(-23f, -9f, 58f), V(-6f, 22f, 76.3f)),
            new SectionBeat(BeatDrum, V(-6f, -9f, 52f), V(23f, 20f, 76.3f)),
            new SectionBeat(BeatDial, V(-23f, 9f, 76.3f), V(23f, 27.5f, 100f)),
            new SectionBeat(BeatRoof, V(-13f, 18f, 90f), V(13f, 36f, ExitZ + .5f)),
        };

        // "N. 장소: 할 일" signs in ink, read coming from -z, over head height or outside the walked width, no collider.
        static void Signs(CourseBuilder b)
        {
            var at = b.parent;
            Look.Sign(at, "왕실 시계탑", V(0f, 6.2f, 9.6f), 2.2f);
            Look.Sign(at, "1. 배우는 벽: 우클릭을 누른 채 W — 벽을 탄다 · 멈춰 매달리면 숨이 안 닳는다", V(0f, 4.6f, 9.6f), 1f);
            Look.Sign(at, "2. 짝 추: 내려온 추에 올라타 → 올라가면 옆 추(내려와 있음)로 옮겨 타기 → 회랑", V(-9f, 9.8f, 17.4f), 1f);
            Look.Sign(at, "절벽: 한 번에 8.5 m", V(-9f, 13.2f, 23.6f), .7f);
            Look.Sign(at, "3. 낙하 벽: 턱을 밟고 내려서기 · 뛰어내리기 · 다이빙", V(-2f, 14.8f, 29f), 1f, 90f);
            Look.Sign(at, "4. 모서리 기둥: 넘을 수 없다 · 주황 띠를 잡고 모서리를 돌아(ㄱ자) 섬 위로", V(12f, 9.8f, 36.6f), .9f);
            Look.Sign(at, "5. 종 줄: 줄이 이쪽 끝에 올 때 우클릭으로 잡고 저쪽 끝에서 놓는다", V(12f, 10.6f, 43.6f), .9f);
            Look.Sign(at, "6. 편종 원통: 붙거나 초록 판자에 서면 원통이 실어 간다 · 높은 판자로 빗살 1을 넘고, 빗살 2 전에 낮은 판자로", V(13f, 8.6f, 58.4f), .9f);
            Look.Sign(at, "7. 톱니 블록 벽: 나온 블록에 서면 한 줄 올려 준다 · 위 줄 블록으로 옮겨 타기", V(-14f, 8.2f, 60f), 1f);
            Look.Sign(at, "8. 대시계판: 받침대에서 분침을 잡고 12시까지 · 정오 턱으로", V(0f, 18.6f, 77f), 1.1f);
            Look.Sign(at, "9. 구리 지붕: 좌클릭 다이빙으로 미끄러진다", V(0f, 30.6f, 99f), 1f);
        }
    }
}
