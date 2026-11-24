using UnityEngine;
using R = ChessFight.Network.RoyalVaultRules;
using Solid = ChessFight.PawnRush.YardKit.Solid;

namespace ChessFight.PawnRush
{
    // Every gameplay cue of the royal vault, and nothing else: separate "Cue ..." pictures (no colliders) in the cue materials
    // only - static CueGold on the edges to jump from or to (the hurdle bars' tops too, turning with them); VaultCue for the
    // moving machines (Off hidden, Go green, Warn red blinking, Stop red), each reading its own RoyalVaultRules query and
    // nothing of the machine itself; the belts' ink slats (VaultSlats from BeltTravel); the door's three countdowns (VaultCue
    // text from DoorSign). Built last: the hurdle tops, the drawer faces and the plate rims hang on their mover roots. Every
    // index a lambda reads is copied inside its loop first.
    public static partial class RoyalVaultBuilder
    {
        static void Cues(CourseBuilder b, Transform section)
        {
            GoldEdges(b);
            BulkheadCues(b);
            HammerLamps(b);
            ForkTwoCues(b);
            PlateRims(b);
            DoorCountdowns(b);
        }

        static GameObject CuePicture(CourseBuilder b, string name, Vector3 a, Vector3 c, Material material) =>
            b.Box(name, a, c, material, false, false);

        static void Watch(GameObject cue, System.Func<double, R.Cue> state) => cue.AddComponent<VaultCue>().Configure(state);

        // ------------------------------------------------------------------ static gold: where to jump from and to

        static void GoldEdges(CourseBuilder b)
        {
            var gold = RoyalVaultLook.CueGold;
            float inner = OpeningInner, outer = OpeningOuter, x = TowerOuterX, landing = TowerInnerX;
            // The take-off openings: the forecourt's north edge and the rest platform's.
            float fz = F(R.ForecourtZ1);
            CuePicture(b, "Cue forecourt lip W", V(-outer, Tier0, fz - .15f), V(-inner, Tier0 + .02f, fz), gold);
            CuePicture(b, "Cue forecourt lip E", V(inner, Tier0, fz - .15f), V(outer, Tier0 + .02f, fz), gold);
            float rest = F(R.TowerTop(R.TowerRest)), mid = F(R.TowerZ(R.TowerRest)), half = F(R.TowerDiameter / 2);
            CuePicture(b, "Cue rest lip N W", V(-outer, rest, mid + half - .15f), V(-inner, rest + .02f, mid + half), gold);
            CuePicture(b, "Cue rest lip N E", V(inner, rest, mid + half - .15f), V(outer, rest + .02f, mid + half), gold);
            // The landing edges, fully open: the rest's south edge, J1's tower landings.
            CuePicture(b, "Cue rest lip S", V(-x, rest, mid - half), V(x, rest + .02f, mid - half + .15f), gold);
            float j1 = F(R.J1Z0);
            CuePicture(b, "Cue J1 lip W", V(-x, Tier1, j1), V(-landing, Tier1 + .02f, j1 + .15f), gold);
            CuePicture(b, "Cue J1 lip E", V(landing, Tier1, j1), V(x, Tier1 + .02f, j1 + .15f), gold);
            // A rim round every tower top.
            float lane = F(R.TowerLaneX);
            for (int k = 1; k <= R.TowerCount; k++)
            {
                if (k == R.TowerRest) continue;
                float top = F(R.TowerTop(k)) + .005f, z = F(R.TowerZ(k));
                YardKit.Ring(b.parent, "Cue tower rim W" + k, V(-lane, top, z), Vector3.up, half - .08f, .15f, .02f, 24, gold, Solid.None);
                YardKit.Ring(b.parent, "Cue tower rim E" + k, V(lane, top, z), Vector3.up, half - .08f, .15f, .02f, 24, gold, Solid.None);
            }
            // The launch pad's rim, 0.12 m strips round its edge, and the shelf's lip where the throw comes down.
            float px = F(R.PadX), pz = F(R.PadZ), ph = F(R.PadSize / 2), y0 = F(R.PadFloor) + .04f, y1 = y0 + .03f;
            const float strip = .12f;
            CuePicture(b, "Cue pad rim S", V(px - ph, y0, pz - ph), V(px + ph, y1, pz - ph + strip), gold);
            CuePicture(b, "Cue pad rim N", V(px - ph, y0, pz + ph - strip), V(px + ph, y1, pz + ph), gold);
            CuePicture(b, "Cue pad rim W", V(px - ph, y0, pz - ph + strip), V(px - ph + strip, y1, pz + ph - strip), gold);
            CuePicture(b, "Cue pad rim E", V(px + ph - strip, y0, pz - ph + strip), V(px + ph, y1, pz + ph - strip), gold);
            float sz = F(R.ShelfZ0);
            CuePicture(b, "Cue shelf lip", V(F(R.ShelfX0), Tier2, sz), V(F(R.ShelfX1), Tier2 + .02f, sz + .15f), gold);
            // R725 fun pass: the hurdle bars' tops, the design's V02 "a full gold bar" - a strip the bar's full length and width
            // laid on its top face, riding the Spinner root so it turns with the bar. A picture 0.03 m thick: the height to clear
            // is the log's top, HurdleBarHigh.
            float barTop = F(R.HurdleBarHigh), barWide = F(R.HurdleBarHigh - R.HurdleBarLow), barLong = F(2 * R.HurdleReach);
            const float barStrip = .03f;
            for (int h = 0; h < R.HurdleHubZ.Length; h++)
            {
                var hurdle = Built(b, "RV hurdle " + (h + 1));
                if (hurdle == null) continue;
                YardKit.Box(hurdle, "Cue hurdle bar " + (h + 1), V(0f, barTop + barStrip * .5f, 0f), V(barLong, barStrip, barWide), gold, Solid.None);
            }
        }

        // ------------------------------------------------------------------ the machines

        // A floor strip before each half of each bulkhead (BulkheadCue: Go while the half's middle stays clear >= 1 s, Warn in its
        // last second, Stop while blocked).
        static void BulkheadCues(CourseBuilder b)
        {
            float cx = F(R.CorridorX), w = F(R.BulkheadHalfWidth);
            for (int k = 0; k < R.BulkheadZ.Length; k++)
                for (int s = -1; s <= 1; s += 2)
                {
                    int bb = k, side = s;
                    float z = F(R.BulkheadZ[bb]), x0 = side < 0 ? cx - w + .1f : cx + .1f, x1 = side < 0 ? cx - .1f : cx + w - .1f;
                    var cue = CuePicture(b, "Cue bulkhead " + (bb + 1) + (side < 0 ? " W" : " E"), V(x0, Tier0 + .005f, z - 1.6f),
                                         V(x1, Tier0 + .025f, z - .6f), RoyalVaultLook.CueRed);
                    Watch(cue, t => R.BulkheadCue(bb, side, t));
                }
        }

        // A lamp beside each hammer's zone (HammerCue: Stop at body height, Warn in the second before it, Go for a second after).
        static void HammerLamps(CourseBuilder b)
        {
            for (int i = 0; i < R.HammerAxleZ.Length; i++)
            {
                int h = i;
                var lamp = YardKit.Box(b.parent, "Cue hammer lamp " + (h + 1), V(-4.6f, Tier1 + 2f, F(R.HammerAxleZ[h]) - 3.9f), Vector3.one * .4f,
                                       RoyalVaultLook.CueRed, Solid.None);
                Watch(lamp, t => R.HammerCue(h, t));
            }
        }

        // Fork 2: the press band (PressCue), a strip at each belt's mouth (BeltCue), the belts' moving ink slats, the drawer faces
        // (DrawerCue).
        static void ForkTwoCues(CourseBuilder b)
        {
            float x = F(R.PressX), half = F(R.PressDieAcross / 2);
            Watch(CuePicture(b, "Cue press band", V(x - half, Tier1 + .005f, F(R.PressBandZ0)), V(x + half, Tier1 + .025f, F(R.PressBandZ1)),
                             RoyalVaultLook.CueRed), t => R.PressCue(t));

            float x0 = F(R.BeltWestX0), split = F(R.BeltSplitX), x1 = F(R.BeltEastX1), z0 = F(R.BeltZ0), z1 = F(R.BeltZ1);
            Watch(CuePicture(b, "Cue belt W", V(x0, Tier1 + .005f, z0 + .1f), V(split, Tier1 + .025f, z0 + .5f), RoyalVaultLook.CueRed),
                  t => R.BeltCue(0, t));
            Watch(CuePicture(b, "Cue belt E", V(split, Tier1 + .005f, z0 + .1f), V(x1, Tier1 + .025f, z0 + .5f), RoyalVaultLook.CueRed),
                  t => R.BeltCue(1, t));
            // Bars every 2 m that ride each belt's travel, so its motion shows (on the east belt they stand still while it is
            // stopped). Children of Content, not of the scaled belt boxes; a holder beside them carries the driver.
            const float pitch = 2f;
            int count = Mathf.RoundToInt((z1 - z0) / pitch);
            for (int belt = 0; belt < 2; belt++)
            {
                string side = belt == 0 ? "W" : "E";
                float mid = belt == 0 ? (x0 + split) * .5f : (split + x1) * .5f;
                var slats = new Transform[count];
                for (int k = 0; k < count; k++)
                    slats[k] = YardKit.Box(b.parent, "Cue belt slat " + side + " " + k, V(mid, Tier1 + .005f, z0 + k * pitch), V(4.6f, .02f, .2f),
                                           RoyalVaultLook.Ink, Solid.None).transform;
                var holder = new GameObject("Cue belt slats " + side);
                holder.transform.SetParent(b.parent, false);
                holder.AddComponent<VaultSlats>().Configure(belt, slats, z0, z1 - z0, pitch);
            }

            float face = F(R.DrawerTravel / 2) + .01f;
            for (int k = 0; k < R.DrawerZ.Length; k++)
            {
                int d = k;
                var root = Built(b, "RV drawer " + (d + 1));
                if (root == null) continue;
                Watch(YardKit.Box(root, "Cue drawer face " + (d + 1), V(-face, 0f, 0f), V(.02f, 1.6f, 2.2f), RoyalVaultLook.CueRed, Solid.None),
                      t => R.DrawerCue(d, t));
            }
        }

        // A strip along each plate's inner (lifting) edge, riding the plate (PlateCue: Warn during the warning, Stop while tilted).
        static void PlateRims(CourseBuilder b)
        {
            float wide = F(R.PlateWide), length = F(R.PlateLong);
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < R.PlateCount; i++)
                {
                    int r = row;
                    var root = Built(b, "RV plate " + (r == 0 ? "W" : "E") + (i + 1));
                    if (root == null) continue;
                    Watch(YardKit.Box(root, "Cue plate rim", V(wide - .075f, .012f, length * .5f), V(.15f, .02f, length - .1f), RoyalVaultLook.CueRed,
                                      Solid.None), t => R.PlateCue(r, t));
                }
        }

        // The time-lock door's countdown (DoorSign: "3" / "2" / "1" in ink, "열림" green while passable, "닫힘" red blinking while
        // it closes, else nothing): over the doorway as the landmark, and twice at eye level from the terrace, where the default
        // camera does not see the top one.
        static void DoorCountdowns(CourseBuilder b)
        {
            float z = F(R.DoorZ0) - .1f;
            Countdown(b, "Cue door countdown", V(0f, Tier3 + 8.2f, z), 3f);
            Countdown(b, "Cue door countdown W", V(-6f, Tier3 + 2.8f, z), 2.5f);
            Countdown(b, "Cue door countdown E", V(6f, Tier3 + 2.8f, z), 2.5f);
        }

        static void Countdown(CourseBuilder b, string name, Vector3 at, float size)
        {
            var text = RoyalVaultLook.Sign(b.parent, "", at, size);
            text.gameObject.name = name;
            text.gameObject.AddComponent<VaultCue>().ConfigureText(t => R.DoorSign(t, out _), t =>
            {
                R.DoorSign(t, out var look);
                return look;
            });
        }
    }
}
