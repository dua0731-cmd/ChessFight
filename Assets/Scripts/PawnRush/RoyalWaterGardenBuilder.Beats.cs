using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The royal water garden's beats in the order a pawn runs them (section frame: x east, z north, y = a floor's top).
    // Numbers are the v31 layout the user approved as a picture on 11-02 (plaza option A, the shortcut off the fourth
    // platform, four boat lanes with two boats each, both teams riding them); where the graybox differs, the reason is
    // next to it and in Docs/PawnRush/PAWN_RUSH_ROYAL_GARDEN.md.
    public static partial class RoyalWaterGardenBuilder
    {
        // ------------------------------------------------------------------ beat 1: the fountain plaza (y 0)

        public static readonly Vector2 PlazaCenter = new Vector2(0f, -35f);
        public const float PlazaRadius = 17f;

        // The five spin bars (option A): x, z, reach, degrees a second (the sign is the way round), phase in turns,
        // tall (1) or low (0). Four low bars 0.25..0.55 m up to jump; the middle one tall (0.1..1.2 m, three arms) to
        // wait out. The two south bars turn opposite ways, so they close the gap between them together.
        static readonly float[,] Bars =
        {
            { -6.5f, -43.5f, 4.5f, 80f, 0f, 0f },
            { 6.5f, -43.5f, 4.5f, -80f, 0f, 0f },
            { 0f, -35f, 5f, 360f / 7f, 0f, 1f },
            { -8f, -27.5f, 4.5f, -72f, .25f, 0f },
            { 8f, -27.5f, 4.5f, 72f, .25f, 0f },
        };

        static Vector3 PlazaPoint(float phi, float r, float y = 0f)
        {
            float a = phi * Mathf.Deg2Rad;
            return V(PlazaCenter.x + r * Mathf.Cos(a), y, PlazaCenter.y + r * Mathf.Sin(a));
        }

        static void FountainPlaza(CourseBuilder b)
        {
            var k = b.kit;
            // The start yard (x -6..6, z -61..-50, under the plaza's rim): the section before joins at z -61, 12 m wide.
            Mass(b, "Start yard", -6f, 6f, -61f, -50f, 0f);
            Rail(b, "Start yard rail W", -6.3f, -6f, -61f, -51.2f, 0f);
            Rail(b, "Start yard rail E", 6f, 6.3f, -61f, -51.2f, 0f);
            b.Checkpoint(0, V(0f, 0f, -57f), 0f, 10f);
            // The plaza: 34 m across round (0, -35), a 1.1 m kerb with 13 m openings south (the yard) and north (the
            // canal walk), so nobody is knocked into the water from it.
            b.Cylinder("Fountain plaza", V(PlazaCenter.x, Deep, PlazaCenter.y), PlazaRadius * 2f, -Deep, k.floorChecker, true, true)
             .AddComponent<CourseFloor>();
            Mass(b, "Plaza north apron", -7f, 7f, -21f, -18.5f, 0f);
            for (float phi = 0f; phi < 360f - .01f; phi += 6f)
            {
                float mid = phi + 3f;
                if (Mathf.Abs(Mathf.DeltaAngle(mid, 90f)) < 24f || Mathf.Abs(Mathf.DeltaAngle(mid, 270f)) < 24f) continue;
                b.SlopeBetween("Plaza kerb", PlazaPoint(phi - .3f, PlazaRadius - .3f), PlazaPoint(phi + 6.3f, PlazaRadius - .3f), .6f, 0f, 1.1f, k.wallNoClimb, true);
            }
            for (int i = 0; i < Bars.GetLength(0); i++)
                SpinBar(b, "Spin bar " + (i + 1), V(Bars[i, 0], 0f, Bars[i, 1]), Bars[i, 2], Bars[i, 5] > .5f, Bars[i, 3], Bars[i, 4]);
        }

        // A spin bar: low (0.25..0.55 m, jumped; one bar through the hub) or tall (0.1..1.2 m, waited out; three arms).
        // It only moves; what it touches it pushes, like course 01's bars.
        static void SpinBar(CourseBuilder b, string name, Vector3 hub, float reach, bool tall, float degreesPerSecond, float phaseTurns)
        {
            var motion = Mover(b, name, hub, MotionKind.Rotate, Vector3.up, phaseTurns);
            motion.degreesPerSecond = degreesPerSecond;
            float y0 = tall ? .1f : .25f, y1 = tall ? 1.2f : .55f, half = tall ? .25f : .175f;
            int arms = tall ? 3 : 2;
            Under(b, motion.transform, () =>
            {
                for (int i = 0; i < arms; i++)
                {
                    var arm = b.Group("Arm " + (i + 1));
                    arm.localRotation = Quaternion.Euler(0f, i * 360f / arms, 0f);
                    Under(b, arm, () => b.Box("Bar", V(.3f, y0, -half), V(reach, y1, half), b.kit.hazard));
                }
                b.Cylinder("Hub", Vector3.zero, tall ? 1.4f : 1f, y1 + .35f, b.kit.rankGold, true, true);
            });
        }

        // ------------------------------------------------------------------ beat 2: the geyser canal (y 0 -> 1.8)

        // The jets in the order a pawn passes them: six along the walk going west, two on the ramp going north. They
        // fire the other way round (the ramp's top one first), so the wave runs at the pawns.
        static readonly Vector2[] GeyserAt =
        {
            new Vector2(-3f, -17f), new Vector2(-8f, -14f), new Vector2(-13f, -17f), new Vector2(-18f, -14f),
            new Vector2(-23f, -17f), new Vector2(-28f, -14f), new Vector2(-33f, -9f), new Vector2(-29f, -4.5f),
        };

        static void GeyserCanal(CourseBuilder b)
        {
            var k = b.kit;
            // The canal walk (x -35..7, z -18.5..-12.5) west from the plaza's north opening, railed both sides.
            Mass(b, "Canal walk", -35f, 7f, -18.5f, -12.5f, 0f);
            Rail(b, "Canal walk rail S", -35f, -7f, -18.8f, -18.5f, 0f);
            Rail(b, "Canal walk rail N", -27f, 7f, -12.5f, -12.2f, 0f);
            Rail(b, "Canal walk rail E", 7f, 7.3f, -18.5f, -12.2f, 0f);
            Solid(b, "Garden wall W (canal)", V(-35.6f, Deep, -18.8f), V(-35f, WestLandingY + 1.4f, 8.3f));
            // The ramp north (x -35..-27, z -12.5 -> -1, y 0 -> 1.8, 9°).
            b.Slope("West ramp", -35f, -27f, -12.5f, -1f, 0f, WestLandingY, 3f, 0f, k.floorChecker, true).AddComponent<CourseFloor>().sloped = true;
            b.Slope("West ramp rail E", -27.3f, -27f, -12.2f, -1f, 0f, WestLandingY, 3f, 1.1f, k.wallNoClimb, true);
            b.Checkpoint(1, V(-4f, 0f, -15.5f), -90f, 5f);
            for (int i = 0; i < GeyserAt.Length; i++) Geyser(b, i);
        }

        static void Geyser(CourseBuilder b, int along)
        {
            var k = b.kit;
            var p = GeyserAt[along];
            float y = p.y > -12.5f ? WestLandingY * (p.y + 12.5f) / 11.5f : 0f;
            var g = b.Group("Geyser " + (along + 1));
            g.localPosition = V(p.x, y + .02f, p.y);
            Transform column = null, bubbles = null;
            Under(b, g, () =>
            {
                b.Cylinder("Geyser rim", V(0f, -.03f, 0f), 2.4f, .03f, k.hazard, false);
                b.Cylinder("Geyser grate", V(0f, -.02f, 0f), 2.1f, .03f, k.trimBlack, false);
                column = b.Cylinder("Geyser water", Vector3.zero, 1.8f, .02f, k.glass, false).transform;
                bubbles = b.Cylinder("Geyser bubbles", Vector3.zero, 2f, .02f, k.glass, false).transform;
            });
            g.gameObject.AddComponent<GardenGeyser>().Configure(GeyserAt.Length - 1 - along, column, bubbles, along < 6 ? Vector3.right : Vector3.back);
        }

        // ------------------------------------------------------------------ beat 3: the bobbing platforms (y 1.8 -> 4.1)

        public const int PlatformCount = 8;
        public const float PlatformDiameter = 6f, PlatformBob = .35f, PlatformPeriod = 6f;

        // The middle of platform i's top (half way up its bob): 6 m apart east-west, zigzagging 4 m, 1.2 m of water
        // between neighbours, each 0.25 m higher than the one before; it rises and sinks 0.35 m either way every 6 s,
        // an eighth of a cycle after the one before (a wave running east).
        public static Vector3 PlatformTop(int i) => V(-21f + 6f * i, 2f + .25f * i + PlatformBob, i % 2 == 0 ? 2.5f : 6.5f);

        static void Platforms(CourseBuilder b)
        {
            var k = b.kit;
            // The west landing (x -35..-25.2, z -1..8, y 1.8): the top of the ramp, CP2. Falling off any platform or the
            // shortcut comes back here.
            Mass(b, "West landing", -35f, -25.2f, -1f, 8f, WestLandingY);
            Rail(b, "West landing rail N", -35f, -25.2f, 8f, 8.3f, WestLandingY);
            Rail(b, "West landing rail S", -27f, -25.2f, -1.3f, -1f, WestLandingY);
            b.Checkpoint(2, V(-30f, WestLandingY, 3.5f), 90f, 7f);
            for (int i = 0; i < PlatformCount; i++)
            {
                var top = PlatformTop(i);
                var root = Bobbing(b, "Bobbing platform P" + (i + 1), top, PlatformBob, PlatformPeriod, -i / 8f);
                Under(b, root, () =>
                {
                    b.Cylinder("Drum", V(0f, -.8f, 0f), PlatformDiameter, .8f, k.floorChecker, true, true).AddComponent<CourseFloor>();
                    b.Cylinder("Drum rim", V(0f, -.05f, 0f), PlatformDiameter + .1f, .07f, k.hazard, false);
                });
                b.Cylinder("Bobbing platform P" + (i + 1) + " stem", V(top.x, Deep, top.z), 1.8f, top.y - PlatformBob - .9f - Deep, k.wallNoClimb, true, true);
            }
        }

        // ------------------------------------------------------------------ the shortcut (P4 -> the dyke, y 3.25 -> 3.8)

        // Where the shortcut comes onto the dyke: between the two robot arms, CP5.
        public static readonly Vector3 ShortcutLanding = new Vector3(19.4f, Upper, 24f);

        // `s` metres from P4's middle towards the landing, on the water.
        static Vector3 ShortcutAt(float s)
        {
            var from = PlatformTop(3);
            var along = new Vector3(ShortcutLanding.x - from.x, 0f, ShortcutLanding.z - from.z).normalized;
            return V(from.x, 0f, from.z) + along * s;
        }

        // A very hard way straight across the water (the user: "중간에 발판 부분에서 엄청 어려운 난이도의 지름길"): every
        // gap is 2.2 ~ 2.5 m - more than a running jump carries (about 1.9 m), less than a sprinting one (about 2.8 m) -
        // and the posts rise and sink on their own, so each jump wants a sprint and the right moment. K1 wears a gold
        // rim so the way is seen from the platforms. A fall goes back to CP2 (the west landing).
        static void Shortcut(CourseBuilder b)
        {
            var k = b.kit;
            var from = PlatformTop(3);
            var along = new Vector3(ShortcutLanding.x - from.x, 0f, ShortcutLanding.z - from.z).normalized;
            // K1 ~ K3: posts 1.2 m across, rising and sinking 0.4 m either way every 3 s, a third of a cycle apart.
            float[] postS = { 5.8f, 9.3f, 12.8f }, postBase = { 3.25f, 3.35f, 3.45f };
            for (int i = 0; i < postS.Length; i++)
            {
                var at = ShortcutAt(postS[i]);
                at.y = postBase[i] + .4f;
                var root = Bobbing(b, "Shortcut post K" + (i + 1), at, .4f, 3f, i / 3f);
                var rim = i == 0 ? k.rankGold : k.hazard;
                Under(b, root, () =>
                {
                    b.Cylinder("Post", V(0f, -.6f, 0f), 1.2f, .6f, k.floorChecker, true, true).AddComponent<CourseFloor>();
                    b.Cylinder("Post rim", V(0f, -.05f, 0f), 1.3f, .07f, rim, false);
                });
                b.Cylinder("Shortcut post K" + (i + 1) + " stem", V(at.x, Deep, at.z), .5f, postBase[i] - .7f - Deep, k.wallNoClimb, true, true);
            }
            // K4: a wider post that stays put, with a small low spin bar on it (1.25 m each way, a turn every 1.6 s) -
            // the one place to get the stamina back.
            var k4 = ShortcutAt(16.9f);
            b.Cylinder("Shortcut post K4", V(k4.x, Deep, k4.z), 2.6f, 3.4f - Deep, k.floorChecker, true, true).AddComponent<CourseFloor>();
            SpinBar(b, "Shortcut post K4 spin bar", V(k4.x, 3.4f, k4.z), 1.25f, false, -225f, 0f);
            // L1, L2: lotus pads 1.6 m across that tip 40° to the side half a second after a pawn lands (TiltingPad).
            float[] padS = { 21.2f, 25.1f };
            for (int i = 0; i < padS.Length; i++)
            {
                var at = ShortcutAt(padS[i]);
                at.y = 3.3f;
                var pad = b.Group("Shortcut lotus L" + (i + 1));
                pad.localPosition = at;
                var body = pad.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                Under(b, pad, () =>
                {
                    b.Cylinder("Pad", V(0f, -.4f, 0f), 1.6f, .4f, k.floorChecker, true, true).AddComponent<CourseFloor>();
                    b.Cylinder("Pad rim", V(0f, -.05f, 0f), 1.7f, .07f, k.hazard, false);
                });
                pad.gameObject.AddComponent<TiltingPad>().Configure(along, .8f);
                b.Cylinder("Shortcut lotus L" + (i + 1) + " stem", V(at.x, Deep, at.z), .5f, 3.3f - .6f - Deep, k.wallNoClimb, true, true);
            }
        }

        // ------------------------------------------------------------------ beat 4: the east lock (y 3.8)

        static void EastLock(CourseBuilder b)
        {
            var k = b.kit;
            // The east landing (x 25.2..35, z 0..10): off the last platform, CP3.
            Mass(b, "East landing", 25.2f, 35f, 0f, 10f, Upper);
            Rail(b, "East landing rail S", 25.2f, 35f, -.3f, 0f, Upper);
            Rail(b, "East landing rail N", 25.2f, 26.4f, 9.7f, 10f, Upper);
            b.Checkpoint(3, V(30.1f, Upper, 4.5f), 0f, 7f);
            // The lock (x 27..35, z 10..24), walled both sides: water runs south over its floor at 1.5 m/s, and three
            // sluice gates rise for about 3 s and drop for about 3 s every 6 s, each a second after the one before.
            Mass(b, "Lock floor", 27f, 35f, 10f, 24f, Upper);
            Solid(b, "Lock wall W", V(26.4f, Deep, 10f), V(27f, Upper + 1.8f, 24f));
            Solid(b, "Lock wall E", V(35f, Deep, -.3f), V(35.6f, Upper + 1.8f, 32.6f));
            float[] gateZ = { 13f, 17.5f, 22f };
            for (int i = 0; i < gateZ.Length; i++)
            {
                float z = gateZ[i];
                Solid(b, "Lock gate post W " + (i + 1), V(26.4f, Upper + 1.8f, z - .45f), V(27f, 10.2f, z + .45f));
                Solid(b, "Lock gate post E " + (i + 1), V(35f, Upper + 1.8f, z - .45f), V(35.6f, 10.2f, z + .45f));
                Solid(b, "Lock gate beam " + (i + 1), V(27f, 9.6f, z - .45f), V(35f, 10.2f, z + .45f));
                var gate = Mover(b, "Sluice gate " + (i + 1), V(31f, Upper, z), MotionKind.Gate, Vector3.up, 0f);
                gate.distance = 3f;
                gate.period = 6f;
                gate.shove = true;
                Under(b, gate.transform, () => b.Box("Gate panel", V(-4f, 0f, -.3f), V(4f, 2.6f, .3f), k.hazard));
                ObstaclePhase.Shift(gate.gameObject, i * 1f);
                Span(gate.gameObject, Vector3.right, -4f, 4f, Upper);
            }
            b.Trigger<WaterCurrent>("Lock current", V(27f, Upper, 10f), V(35f, Upper + 1f, 24f)).Configure(V(0f, 0f, -1.5f));
            Picture(b, "Lock water", V(27f, Upper + .005f, 10f), V(35f, Upper + .02f, 24f), k.glass);
            foreach (float z in new[] { 11.5f, 15.2f, 19.8f, 23.2f })
                foreach (float x in new[] { 29f, 31f, 33f })
                {
                    Line(b, "Current arrow", V(x - .7f, Upper + .03f, z + .7f), V(x, Upper + .03f, z), .15f, k.trimWhite);
                    Line(b, "Current arrow", V(x + .7f, Upper + .03f, z + .7f), V(x, Upper + .03f, z), .15f, k.trimWhite);
                }
        }

        // ------------------------------------------------------------------ beat 5: the robot dyke (y 3.8)

        static void RobotDyke(CourseBuilder b)
        {
            // The dyke (x 8..35, z 24..30 to walk, a wall at z 30, the gantries behind it), west from the lock to the
            // boarding quay. Its south edge is open over the water. CP4 at its start, CP5 between the robots where the
            // shortcut lands.
            Mass(b, "Robot dyke", 8f, 35f, 24f, 32.6f, Upper);
            Rail(b, "Dyke wall N", 10f, 35f, 30f, 30.5f, Upper);
            b.Checkpoint(4, V(31f, Upper, 27f), -90f, 5f);
            b.Checkpoint(5, V(ShortcutLanding.x, Upper, 27f), -90f, 6f);
            Robot(b, "1", 26f, 0f);
            Robot(b, "2", 13f, 3f);
        }

        // A robot arm hung from a gantry at x: pivot 5.8 m over the dyke's middle line, a hand 1.8 m across 4.7 m down (R434)
        // that passes 0.2 m over the floor (RobotSweep). The second robot runs half a cycle (3 s) behind the first.
        static void Robot(CourseBuilder b, string n, float x, float phaseSeconds)
        {
            var k = b.kit;
            // R434, R435: the drawn robot's stone pillar (승규's ball-and-chain, GardenLockDyke) stands here, east of the
            // swing (the arm rests and winds up to the north, through the gantry's middle), over the east post.
            Solid(b, "Robot pillar block " + n, V(x + 1.15f, Upper, 30.55f), V(x + 2.95f, 9.5f, 32.3f));
            Solid(b, "Robot gantry post W " + n, V(x - 1.8f, Upper, 30.6f), V(x - 1.3f, 10.4f, 31.6f));
            Solid(b, "Robot gantry post E " + n, V(x + 1.3f, Upper, 30.6f), V(x + 1.8f, 10.4f, 31.6f));
            Solid(b, "Robot gantry beam W " + n, V(x - 1.8f, 9.9f, 26.6f), V(x - 1.3f, 10.4f, 30.6f));
            Solid(b, "Robot gantry beam E " + n, V(x + 1.3f, 9.9f, 26.6f), V(x + 1.8f, 10.4f, 30.6f));
            Picture(b, "Robot axle " + n, V(x - 1.3f, 9.35f, 26.75f), V(x + 1.3f, 9.85f, 27.25f), k.trimBlack);
            var pivot = b.Group("Robot arm " + n);
            pivot.localPosition = V(x, 9.6f, 27f);
            GameObject hand = null;
            Under(b, pivot, () =>
            {
                b.Box("Robot upper arm", V(-.3f, -4.6f, -.3f), V(.3f, 0f, .3f), k.hazard);
                // R434 (승규 "해머 부분이 통과가 돼"): the hand is the drawn ball - 1.8 m across, its middle 4.7 m down
                // (it was 1.3 m at 4.9 m, so the ball's top and sides let pawns through); it still clears the dyke by 0.2 m.
                hand = b.Sphere("Robot hand", V(0f, -4.7f, 0f), 1.8f, k.hazard);
            });
            var sweep = pivot.gameObject.AddComponent<RobotSweep>();
            sweep.Configure(Vector3.left, hand.transform);
            sweep.SetPhase(phaseSeconds);
            Span(pivot.gameObject, Vector3.forward, -5f, 5f, Upper);
        }

        // ------------------------------------------------------------------ beat 6: the boat crossing (y 3.8)

        // The four lanes' middles (2.4 m boats, 1.2 m of water between lanes, from the quay at z 33 to the gate yard at
        // z 48.6).
        public static readonly float[] LaneZ = { 35.4f, 39f, 42.6f, 46.2f };

        static readonly float[][] TunnelWalls =
        {
            new[] { 33f, 34.2f }, new[] { 36.6f, 37.8f }, new[] { 40.2f, 41.4f }, new[] { 43.8f, 45f }, new[] { 47.4f, 48.6f },
        };

        // R446 (승규 "처마를 올리기"): the tunnels' eave 1.1 m higher (4.4 -> 5.5) so the royal boats (royal_boat.py:
        // lanterns to 5.38, scroll 4.99, furniture backs 4.68) pass under it; the roof and the lane walls go up with it.
        public const float TunnelEaveY = 5.5f, TunnelRoofY = TunnelEaveY + .6f, TunnelTopY = TunnelRoofY + 1.2f;

        static void BoatCrossing(CourseBuilder b)
        {
            var k = b.kit;
            // The boarding quay (x -10..10, z 27..33), CP6.
            Mass(b, "Boarding quay", -10f, 10f, 27f, 33f, Upper);
            Rail(b, "Boarding quay rail W", -10.3f, -10f, 27f, 33f, Upper);
            Rail(b, "Boarding quay rail S", -10f, 8f, 26.7f, 27f, Upper);
            // R531: CP6's trigger is the whole quay (it was 8 m across its west half, x -8..0, z 29..31: a runner off the dyke
            // who went straight to the boats east of that never touched it, and a fall from the boats sent it back to CP5,
            // between the robot arms - 11-09 build test). Its spots across the middle (x -5..5 at z 30, were -7.5..-0.5).
            b.Checkpoint(6, V(0f, Upper, 30f), 0f, 20f).GetComponent<BoxCollider>().size = new Vector3(20f, 3f, 6f);
            // The reservoir (x -26..26, z 33..48.6, water at ReservoirY): falling in goes back to CP6.
            // R441: the wall's collider as it is drawn - west of the quay the spillway's crest (the water pours over it,
            // ReservoirY - 0.2), from there east flush with the quay and the dyke and on to the lock (x -10.4 .. 35: it
            // stood 0.45 m under them, an unseen ledge over the water, and the dyke's face now covers it to z 33).
            Solid(b, "Reservoir wall S (spillway)", V(-26f, Deep, 32.6f), V(-10.4f, SpillwayCrestY, 33f));
            Solid(b, "Reservoir wall S", V(-10.4f, Deep, 32.6f), V(35f, Upper, 33f));
            Picture(b, "Reservoir water (respawn)", V(-26f, ReservoirY - .05f, 33f), V(26f, ReservoirY, 48.6f), CourseKit.Or(k.respawnFloor, k.trimBlack));
            // R441: from z 32.6, so the spillway's crest (under 0.2 m of water) is in it too.
            b.Trigger<KillVolume>("Kill volume (reservoir)", V(-26f, .3f, 32.6f), V(26f, ReservoirY - .1f, 48.6f));
            // R442: a piece floats where it falls in and splashes (as the other pools: Pool).
            Pool(b, "reservoir", -26f, 26f, 32.6f, 48.6f, ReservoirY);
            // A tunnel at either end (|x| 18..26): an eave over the decks (R446: 1.7 m, TunnelEaveY, was 0.6 m and knocked
            // riders off), the falls hanging in its mouth, a roof, and walls between the lanes. The boats sink out of
            // sight under it - whoever still rides one goes into the water - and come round under the water.
            foreach (float s in new[] { -1f, 1f })
            {
                float xa = s > 0 ? 18f : -26f, xb = s > 0 ? 26f : -18f;
                Solid(b, "Boat tunnel roof", V(xa, TunnelRoofY, 33f), V(xb, TunnelTopY, 48.6f));
                Solid(b, "Boat tunnel eave", V(s > 0 ? 18f : -18.6f, TunnelEaveY, 33f), V(s > 0 ? 18.6f : -18f, TunnelRoofY, 48.6f));
                foreach (var w in TunnelWalls) Solid(b, "Boat tunnel wall", V(xa, Deep, w[0]), V(xb, TunnelRoofY, w[1]));
                foreach (float z in LaneZ)
                    Picture(b, "Boat tunnel falls", V(s > 0 ? 18.65f : -18.85f, ReservoirY, z - 1.2f), V(s > 0 ? 18.85f : -18.65f, TunnelEaveY, z + 1.2f), k.glass);
            }
            // Two boats a lane (6 x 2.4 m, decks at 3.8), placed 18 m apart; BoatLanes gives the lanes their way and speed
            // from the round's seed. Both teams ride them.
            var boats = new List<GardenBoat>();
            var laneOf = new List<int>();
            var group = b.In("Boats", () =>
            {
                for (int lane = 0; lane < LaneZ.Length; lane++)
                    for (int n = 0; n < RoyalWaterGardenRules.BoatsPerLane; n++)
                    {
                        float x = -9f + 18f * n;
                        var root = b.Group("Boat " + (lane + 1) + "-" + (n + 1));
                        root.localPosition = V(x, Upper, LaneZ[lane]);
                        Under(b, root, () =>
                        {
                            b.Box("Hull", V(-3f, -1.8f, -1.2f), V(3f, 0f, 1.2f), k.hazard).AddComponent<CourseFloor>();
                            Picture(b, "Deck", V(-2.6f, 0f, -.9f), V(2.6f, .02f, .9f), k.floorChecker);
                        });
                        var boat = root.gameObject.AddComponent<GardenBoat>();
                        boat.Configure(n, x);
                        root.gameObject.AddComponent<GardenBoatRock>();     // R482: its dressing rocks on the swell
                        boats.Add(boat);
                        laneOf.Add(lane);
                    }
            });
            group.gameObject.AddComponent<BoatLanes>().Configure(boats.ToArray(), laneOf.ToArray());
            // R482 (승규 "배랑 물이랑 상호작용 하게 만들어"): the boats' wakes, bow waves, rings and dive splashes on the water.
            group.gameObject.AddComponent<GardenBoatWater>().Configure(boats.ToArray(), ReservoirY);
            Gap(b, "Boat crossing", V(0f, Upper, 33f), V(0f, Upper, 48.6f), 18f);
        }

        // ------------------------------------------------------------------ beat 7: the gate and the way out (y 3.8 -> 2.8)

        static void Gate(CourseBuilder b)
        {
            var k = b.kit;
            // The gate yard (x -14..14, z 48.6..60.5). R462 (승규 "앞문 지우고 뒷문을 이걸로"): its arch at z 55 is gone; the
            // exit's gate is the garden's gate now.
            Mass(b, "Gate yard", -14f, 14f, 48.6f, 60.5f, Upper);
            Rail(b, "Gate yard rail W", -14.3f, -14f, 48.6f, 60.5f, Upper);
            Rail(b, "Gate yard rail E", 14f, 14.3f, 48.6f, 60.5f, Upper);
            // R474: the garden's north wall is gone (the castle ring takes the gate in, Shell): the yard's north edge has a
            // balustrade over the strip (2.8) either side of the exit terrace, whose rails now start at the yard.
            Rail(b, "Gate yard rail N W", -14.3f, -6.3f, 60.2f, 60.5f, Upper);
            Rail(b, "Gate yard rail N E", 6.3f, 14.3f, 60.2f, 60.5f, Upper);
            // The exit terrace (12 x 6 m, CP 끝 = CP7), 1 m over the mini-game.
            Mass(b, "Exit terrace", -6f, 6f, 60.5f, 66.5f, Upper);
            Marker(b, "Exit terrace (CP 끝)", -6f, 6f, 60.5f, 66.5f, Upper);
            Rail(b, "Exit terrace rail W", -6.3f, -6f, 60.5f, 66.1f, Upper);
            Rail(b, "Exit terrace rail E", 6f, 6.3f, 60.5f, 66.1f, Upper);
            b.Checkpoint(7, V(0f, Upper, 63.5f), 0f, 10f);
            // The open arch, four steps of 0.25 down, the entrance band (z 68.5..72.5, y 2.8): the section contract.
            // R462: the arch is 승규's reference gate (gate_sample_v2.py, its sizes in units of the 12 m opening, from the
            // band's 2.8): the jambs (x +-6..8.6, z 64.7..68.3) up to where the round arch (springing 13, crown 19) clears
            // them, a haunch block either side, the ring's crown over the opening (was a 1 m lintel at 8.8, now inside the
            // open arch), the towers (x +-8.6..13.5, z 64.05..68.95) to their cornice. R474: the wings collide too now
            // that the strip runs up to them inside the castle ring - the vine wall (x +-13.5..18.5) to its coping, the urn
            // pillar (x +-18.5..22.6) to its cap, the low end block (x +-22.6..24.8) the first castle wall leaves.
            // R483 (승규 "성문 쪽 에셋 바꾸고 똑같이 텍스쳐 입혀"): the gate is 승규's Tripo gate (gate_arch_asset.py, 50.6 times
            // its size so its wings still end at x +-24.8), so the blocks are its parts: the jambs' faces at x +-7.1 (the
            // pillar block still starts at the walk's edge, x +-6, closing the gap between the terrace's and the band's
            // rails), the round arch's crown at 19.4, the towers x +-9.2..14.1 to their eaves (24.4), z 64.0..69.0; the
            // vine wall to 14.4 with a planter before it (and one behind) to 6.3, the urn pillar x +-18.9..22.6 to its cap,
            // the end block to 14.2.
            foreach (float s in new[] { -1f, 1f })
            {
                string side = s < 0 ? "W" : "E";
                Solid(b, "Exit arch pillar " + side, V(s < 0 ? -9.21f : 6f, Upper - 1f, 64.02f), V(s < 0 ? -6f : 9.21f, 17.98f, 69.03f));
                Solid(b, "Exit arch haunch " + side, V(s < 0 ? -6f : 5.06f, 17.98f, 64.02f), V(s < 0 ? -5.06f : 6f, 19.4f, 69.03f));
                Solid(b, "Exit gate tower " + side, V(s < 0 ? -14.07f : 9.21f, Upper - 1f, 64.02f), V(s < 0 ? -9.21f : 14.07f, 24.4f, 69.03f));
                Solid(b, "Exit gate wing " + side, V(s < 0 ? -18.93f : 14.07f, Upper - 1f, 64.68f), V(s < 0 ? -14.07f : 18.93f, 14.4f, 68.32f));
                Solid(b, "Exit gate wing planter S " + side, V(s < 0 ? -19.23f : 13.67f, Upper - 1f, 61.24f), V(s < 0 ? -13.67f : 19.23f, 6.3f, 64.68f));
                Solid(b, "Exit gate wing planter N " + side, V(s < 0 ? -19.23f : 13.67f, Upper - 1f, 68.32f), V(s < 0 ? -13.67f : 19.23f, 6.3f, 71.76f));
                Solid(b, "Exit gate urn pillar " + side, V(s < 0 ? -22.57f : 18.93f, Upper - 1f, 63.97f), V(s < 0 ? -18.93f : 22.57f, 15.96f, 69.03f));
                Solid(b, "Exit gate end block " + side, V(s < 0 ? -24.8f : 22.57f, Upper - 1f, 64.68f), V(s < 0 ? -22.57f : 24.8f, 14.19f, 68.32f));
            }
            Solid(b, "Exit arch lintel", V(-9.21f, 19.4f, 64.02f), V(9.21f, 23f, 69.03f));
            for (int i = 1; i <= 4; i++)
                Mass(b, "Step down " + i, -6f, 6f, 66.5f + (i - 1) * .5f, 66.5f + i * .5f, Upper - .25f * i, Upper - 6f);
            Mass(b, "Mini-game entrance band", -6f, 6f, 68.5f, 72.5f, Upper - 1f, Upper - 6f);
            Rail(b, "Band rail W", -6.3f, -6f, 66.9f, 72.5f, Upper - 1f);
            Rail(b, "Band rail E", 6f, 6.3f, 66.9f, 72.5f, Upper - 1f);
        }
    }
}
