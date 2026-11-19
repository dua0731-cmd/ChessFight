using ChessFight.Gameplay;
using UnityEngine;
using R = ChessFight.Network.RoyalVaultRules;

namespace ChessFight.PawnRush
{
    // What makes the royal vault a section of the round rather than a set of floors: the seven checkpoints (CP0 with a reach
    // box at the entry), the kill volumes under the pit, the progress path for the HUD and the validator, each obstacle's
    // span across the way, the one meant gap (the launch pad's flight), the beats and the signs.
    public static partial class RoyalVaultBuilder
    {
        static void Course(CourseBuilder b)
        {
            Checkpoints(b);
            KillVolumes(b);
            ProgressRoute(b);
            Spans(b);
            PadFlight(b);
            Signs(b);
        }

        // ------------------------------------------------------------------ checkpoints and kill volumes

        // CP0 forecourt, CP1 tower rest, CP2 J1, CP3 straddling the walkway's end (every pawn leaving the walkway crosses it before
        // the pad), CP4 J2, CP5 terrace, CP6 2.5 m past the door (the war room's precedent); the round makes them 400..406. CP0
        // also counts from a reach box across the entry, for a pawn turning to the corridor before z 8; its respawn stays on CP0's
        // own floor.
        static void Checkpoints(CourseBuilder b)
        {
            var z = R.CheckpointZ;
            var first = b.Checkpoint(0, V(0f, Tier0, F(z[0])), 0f, 26f);
            Reach(first, V(-14f, Tier0 + .3f, .6f), V(14f, Tier0 + 3f, 2.6f));
            b.Checkpoint(1, V(0f, F(R.TowerTop(R.TowerRest)), F(z[1])), 0f, 2f * TowerOuterX);
            b.Checkpoint(2, V(-7f, Tier1, F(z[2])), 0f, 30f);
            b.Checkpoint(3, V(-3f, Tier1, F(z[3])), 0f, 22f);
            b.Checkpoint(4, V(0f, Tier2, F(z[4])), 0f, 28f);
            b.Checkpoint(5, V(0f, Tier3, F(z[5])), 0f, 28f);
            b.Checkpoint(6, V(0f, Tier3, F(z[6])), 0f, 12f);
        }

        // More of a checkpoint's trigger, min..max in its parent's frame: passing there counts as passing the checkpoint, and the
        // respawn stays on the checkpoint's own floor (Checkpoint.RespawnPosition and the spots belong to its first box). The
        // carriage yard's pattern (RoyalCarriageYard.Sections).
        static void Reach(CourseCheckpoint checkpoint, Vector3 min, Vector3 max)
        {
            var t = checkpoint.transform;
            var box = checkpoint.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = t.InverseTransformPoint(t.parent.TransformPoint((min + max) * .5f));
            box.size = YardKit.Abs(max - min);
        }

        // A fall anywhere into the pit ends in the first (its 1.5 s respawn); the second catches anything that gets past it.
        static void KillVolumes(CourseBuilder b)
        {
            b.Trigger<KillVolume>("RV kill volume (pit)", V(-28f, -8f, .6f), V(16f, -4f, F(R.DoorZ0)));
            b.Trigger<KillVolume>("RV kill volume (far below)", V(-40f, Pit - 2f, -10f), V(30f, Pit, F(R.ExitZ) + 10f));
        }

        // ------------------------------------------------------------------ the progress path

        // The beat of the leg that starts at each of RoyalVaultRules.Route's points (P0..P27).
        static readonly string[] Legs =
        {
            "0 앞뜰", "0 앞뜰",
            "1 금화 탑", "1 금화 탑", "1 금화 탑", "1 금화 탑", "1 금화 탑", "1 금화 탑", "1 금화 탑", "1 금화 탑",
            "J1 합류", "J1 합류",
            "2 다이얼 망치",
            "3 둘째 갈림",
            "3 벨트 · 왕관 프레스", "3 벨트 · 왕관 프레스", "3 벨트 · 왕관 프레스", "3 벨트 · 왕관 프레스", "3 벨트 · 왕관 프레스",
            "J2 합류", "J2 합류",
            "4 뒤집히는 판",
            "5 테라스", "5 테라스",
            "6 시간 자물쇠 문", "6 시간 자물쇠 문", "6 시간 자물쇠 문", "6 시간 자물쇠 문",
        };

        // One route, the same for both teams (the east tower lane, the walkway, the east belt, the press neck, the west plate row,
        // the door), a child of Content.
        static void ProgressRoute(CourseBuilder b)
        {
            var route = new Vector3[R.RouteZ.Length];
            for (int i = 0; i < route.Length; i++) route[i] = V(F(R.RouteX[i]), F(R.RouteY[i]), F(R.RouteZ[i]));
            if (Legs.Length != route.Length) Debug.LogError("[왕실 금고실] 진행 경로 점 " + route.Length + "개, 구간 이름 " + Legs.Length + "개");
            var go = new GameObject("ProgressPath");
            go.transform.SetParent(b.parent, false);
            go.AddComponent<ProgressPath>().Configure(route, (Vector3[])route.Clone(), (string[])Legs.Clone());
        }

        // ------------------------------------------------------------------ obstacle spans and the meant gap

        // What each obstacle sweeps across the way (the validator measures the floor there). A span sits on its mover's root when
        // the mover's Edit-mode pose stands on the measured line (the validator then skips the mover's own colliders); a static
        // "RV span ..." marks a mover that rests off the line - the bulkhead panels in their pockets, the die up, the drawers in
        // their cabinet. The door's span is on the disc: shut across the doorway, a static span would read it as a wall.
        static void Spans(CourseBuilder b)
        {
            for (int h = 0; h < R.HurdleHubZ.Length; h++) SpanOn(Built(b, "RV hurdle " + (h + 1)), F(R.HurdleReach), Tier0);
            for (int k = 0; k < R.BulkheadZ.Length; k++)
                SpanOn(Root(b, "RV span bulkhead " + (k + 1), V(F(R.CorridorX), Tier0, F(R.BulkheadZ[k]))), F(R.BulkheadHalfWidth), Tier0);
            for (int i = 0; i < R.HammerAxleZ.Length; i++) SpanOn(Built(b, "RV hammer " + (i + 1)), F(R.HammerAcross / 2), Tier1);
            SpanOn(Root(b, "RV span press", V(F(R.PressX), Tier1, F(R.PressZ))), F(R.PressDieAcross / 2), Tier1);
            float shelf = F((R.ShelfX0 + R.ShelfX1) / 2), half = F((R.ShelfX1 - R.ShelfX0) / 2);
            for (int k = 0; k < R.DrawerZ.Length; k++)
                SpanOn(Root(b, "RV span drawer " + (k + 1), V(shelf, Tier2, F(R.DrawerZ[k]))), half, Tier2);
            SpanOn(Built(b, "RV door disc"), F(R.DoorRadius), Tier3);
        }

        static void SpanOn(Transform at, float reach, float floor)
        {
            if (at == null) return;
            at.gameObject.AddComponent<ObstacleSpan>().Configure(Vector3.right, -reach, reach, ObstacleSpan.Probe.Floor, floor, Vector3.zero);
        }

        // The launch pad's throw is the one gap on purpose (IntendedGap): a box along the flight, from the pad to its landing.
        // No required plain jump in the vault is wider than 1.5 m.
        static void PadFlight(CourseBuilder b)
        {
            float dx = F(R.PadLandX - R.PadX), dz = F(R.PadLandZ - R.PadZ);
            var gap = Root(b, "RV pad flight", V(F((R.PadX + R.PadLandX) / 2), 7.5f, F((R.PadZ + R.PadLandZ) / 2)), Mathf.Atan2(dx, dz) * Mathf.Rad2Deg);
            gap.gameObject.AddComponent<IntendedGap>().Configure(V(4.4f, 9f, 11f), "발사판 비행");
        }

        // ------------------------------------------------------------------ beats and signs

        // Areas for the HUD and the CSV, in the section frame; the first that holds a pawn wins (PawnRushSection.BeatAt), so the
        // shelf goes before the belts it overhangs.
        public static SectionBeat[] Beats() => new[]
        {
            new SectionBeat("3 서랍 선반", V(2.5f, 7f, 89f), V(11.5f, 12f, 126f)),
            new SectionBeat("1 금화 탑", V(-4.5f, -9f, 18f), V(4.5f, 6f, 50f)),
            new SectionBeat("1 허들 회랑", V(-27f, -9f, .6f), V(-13.5f, 10f, 50f)),
            new SectionBeat("0 앞뜰", V(-15f, -9f, -.5f), V(15f, 9f, 18f)),
            new SectionBeat("J1 합류", V(-23f, -9f, 50f), V(9f, 12f, 60f)),
            new SectionBeat("2 다이얼 망치", V(-8f, -9f, 60f), V(8f, 16f, 83f)),
            new SectionBeat("3 둘째 갈림", V(-15f, -9f, 83f), V(15f, 14f, 89f)),
            new SectionBeat("3 벨트 · 왕관 프레스", V(-15f, -9f, 89f), V(15f, 18f, 126f)),
            new SectionBeat("J2 합류", V(-15f, -9f, 126f), V(15f, 16f, 136f)),
            new SectionBeat("4 뒤집히는 판", V(-8f, -9f, 136f), V(8f, 16f, 156f)),
            new SectionBeat("5 테라스", V(-15f, -9f, 156f), V(15f, 20f, 178f)),
            new SectionBeat("6 시간 자물쇠 문", V(-15f, -9f, 178f), V(15f, 24f, 196.5f)),
        };

        // The carriage yard's "N. 장소: 할 일" signs, in ink, yaw 0 (read coming from -z), outside the walkable width or over head
        // height, with no collider. The chess words never lean on a colour; a sign names a cue's green only where it explains
        // that cue. The castling labels stand at the bulkheads (Fork1), "7" on the arch (Door).
        // R725 fun pass (the critic's risk 4): from the entry the default camera has the corridor's mouth 55-71 deg to the left,
        // out of view, and the fork's sign stood by the towers (z 17.5), read only once the mouth was behind. It now hangs under
        // the title, with an arrow to the corridor over the forecourt's west half (29 deg left of the entry's view, over head
        // height, in open air: a Label draws through walls); the towers' own rule stays by the towers.
        static void Signs(CourseBuilder b)
        {
            var at = b.parent;
            RoyalVaultLook.Sign(at, "왕실 금고실", V(0f, 5.5f, 6f), 2.2f);
            RoyalVaultLook.Sign(at, "1. 첫 갈림 — 금화 탑: 짧고 위험 · 허들 회랑: 길고 안전", V(0f, 4f, 6f), 1f);
            RoyalVaultLook.Sign(at, "← 허들 회랑: 길고 안전", V(-5f, 2.8f, 7f), .9f);
            RoyalVaultLook.Sign(at, "탑은 폰처럼 한 칸씩 앞으로 · 앞 탑에 누가 있으면 기다리기", V(0f, 3.4f, 17.5f), .8f);
            RoyalVaultLook.Sign(at, "금괴 허들: 막대가 오면 점프 · 격벽(캐슬링): 열린 쪽으로", V(F(R.CorridorX), 3.2f, 8.5f), .9f);
            RoyalVaultLook.Sign(at, "2. 다이얼 망치(체스 시계): 망치가 지나간 직후가 내 차례 · 사이 칸에서 쉬기", V(0f, 7.6f, 59.6f), 1f);
            RoyalVaultLook.Sign(at, "3. 둘째 갈림 — 벨트 + 왕관 프레스(안전) · 나이트 발사판 + 서랍 선반(위험)", V(0f, 7.8f, 88.5f), 1f);
            RoyalVaultLook.Sign(at, "보석 벨트: 초록 불 벨트는 멈춤 · 뒤로 가는 벨트는 템포를 잃는다", V(0f, 7.15f, 88.5f), .7f);
            RoyalVaultLook.Sign(at, "발사판(나이트 도약) → 서랍 선반", V(F(R.PadX), 6.4f, 83.3f), .8f);
            RoyalVaultLook.Sign(at, "왕관 프레스: 아래 줄은 체크 — 초록일 때 건너기 · 깜빡이면 줄 밖에서 기다리기", V(F(R.PressX), 6.4f, 106.6f), .8f);
            RoyalVaultLook.Sign(at, "서랍이 들어간 열린 줄에서 기다리기 · 깜빡이는 서랍은 곧 나옴", V(5f, 10.8f, 95.9f), .7f);
            RoyalVaultLook.Sign(at, "4. 뒤집히는 판: 깜빡이는 줄이 일어선다 · 누운 줄로 옮겨 타기", V(0f, 11.6f, 135.6f), 1f);
            int open = Mathf.RoundToInt(F(R.DoorPassableEnd - R.DoorPassableStart));   // 6 s passable a period
            RoyalVaultLook.Sign(at, "6. 시간 자물쇠 문(7랭크): 3 · 2 · 1 뒤 " + open + "초 동안 열림", V(0f, 14.6f, 167.6f), 1.2f);
        }
    }
}
