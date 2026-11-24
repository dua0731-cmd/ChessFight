using ChessFight.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;
using R = ChessFight.Network.RoyalVaultRules;

namespace ChessFight.PawnRush
{
    // The royal vault (왕실 금고실, R725), late obstacle section 3-3, grey-box: forecourt -> fork 1 (coin towers, or the hurdle
    // corridor with its castling bulkheads) -> J1 -> dial hammers -> fork 2 (jewel belts and the crown press, or the knight's
    // launch pad and the drawer shelf) -> J2 -> flipping plates -> stair -> terrace -> time-lock door -> treasure room -> exit.
    // Section frame: x across (east +), y up, +z the way on, metres; entry (0, 0, 0) in the middle of the 12 m threshold, exit
    // (0, RoyalVaultRules.ExitY, RoyalVaultRules.ExitZ). Every number a machine, a validator or a bot also needs comes from
    // RoyalVaultRules (Core); the rest of the layout is the build spec's tables.
    //
    //   Late_3-3_RoyalVault (RoyalVaultSection, PawnRushSection)
    //     Content                      one flat level, built inactive and switched on at the end
    //       RV ...                     every solid and picture: shell, roof, piers, floors, rails, towers, stairs, cabinets
    //       RV hurdle k, RV hammer k   Spinner roots, each with its ObstacleSpan
    //       RV bulkhead panel W1 .. E2, RV press die, RV drawer k, RV door disc, RV door shove      VaultMover roots
    //       RV plate W1 .. E5          VaultPlate roots (each builds its deck in Configure)
    //       Cue ...                    every gameplay cue (the Cues partial); drawer faces and plate rims ride their movers
    //       Label ...                  the signs (TextMesh, ink)
    //       CP0 .. CP6, RV kill volume ..., ProgressPath, RV span ..., RV pad flight, Start
    //
    // The partials: this file (Build, the shell, the roof, the piers, the helpers, the Start mark), Fork1 (forecourt, towers,
    // rest, corridor, hurdles, bulkheads, stair 1, J1), Hammers (walkway, dials, hammers), Fork2 (approach, pad, belts,
    // landing, cabinets, press, stair 2, shelf, drawers), Bridge (J2, plates, stair 3), Door (terrace, door wall, disc, shove,
    // room, arch, "7"), Course (checkpoints, kill volumes, the path, the spans, the pad's flight, the beats, the signs), Cues.
    //
    // Grey-box (D2): every model wears RoyalVaultLook.Greybox; cues are separate "Cue ..." objects in CueRed / CueGreen /
    // CueGold / Ink only; nothing in the vault can be climbed (NoClimbSurface on every solid, floors included). Every motion is
    // a pure function of ObstacleClock (Spinner, VaultMover, VaultPlate read RoyalVaultRules); every Configure and SetPhase
    // runs while Content is inactive, so each Obstacle wakes with its final pose. No Obstacle is nested in another, no static
    // floor sits under a mover, and only pictures without colliders ride on movers.
    public static partial class RoyalVaultBuilder
    {
        // The floor tops of the four tiers: the forecourt; J1 (the hammer walkway, the approach, the belts, the press neck);
        // J2 (the shelf, the plates); the terrace (the door's sill, the treasure room).
        const float Tier0 = 0f, Tier1 = 4f, Tier2 = 8f, Tier3 = 12f;
        // Every stair: 16 risers of 0.25 m on 0.75 m treads, 4 m up over 12 m (18.4 degrees, under the 19.5 rule).
        const int StairSteps = 16;
        const float StairTread = .75f, StairRun = 12f, StairRise = 4f;
        // The shell's walls stand from the pit's bottom to the roof.
        const float RoofY = 30f;

        public static void Build(RoyalVaultSection vault)
        {
            var root = vault.transform;
            var old = root.Find(RoyalVaultSection.ContentName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var content = new GameObject(RoyalVaultSection.ContentName);
            content.SetActive(false);
            content.transform.SetParent(root, false);
            var b = new CourseBuilder(RoyalVaultLook.Kit, content.transform);

            Shell(b);
            Fork1(b, root);
            Hammers(b, root);
            Fork2(b, root);
            Bridge(b);
            Door(b, root);
            Course(b);
            Cues(b, root);   // last: the drawer and plate roots it hangs cues on exist by now

            vault.GetComponent<PawnRushSection>().Describe(RoyalVaultSection.Id, RoyalVaultSection.DisplayName,
                PawnRushSection.SlotKind.Obstacle, RoyalVaultSection.Entry, RoyalVaultSection.Exit, 0f, Beats());
            content.SetActive(true);   // the Obstacles wake here with their final world pose
        }

        // ------------------------------------------------------------------ the shell, the roof, the piers

        // Walls round 44 x 180 m from the pit's bottom to the roof, the wall over the 12 m entrance closed down to the corridor's
        // lid (6.5), a shadowless roof (the scene's sun still lights every model; the roof only hides the sky) and the piers
        // that carry the floors down out of sight: there is no pit floor.
        static void Shell(CourseBuilder b)
        {
            float pit = Pit, d0 = F(R.DoorZ0), d1 = F(R.DoorZ1);
            Block(b, "RV wall S-W", -28f, -6f, pit, RoofY, 0f, .6f);
            Block(b, "RV wall S-E", 6f, 16f, pit, RoofY, 0f, .6f);
            Block(b, "RV entrance lintel", -6f, 6f, 6.5f, RoofY, 0f, .6f);
            Block(b, "RV wall W", -28.6f, -28f, pit, RoofY, 0f, d1);
            Block(b, "RV wall E", 16f, 16.6f, pit, RoofY, 0f, d1);
            Block(b, "RV wall N-W", -28f, -14f, pit, RoofY, d0, d1);
            Block(b, "RV wall N-E", 14f, 16f, pit, RoofY, d0, d1);
            Shadowless(Picture(b, "RV roof", V(-28.6f, RoofY, 0f), V(16.6f, RoofY + .5f, d1)));
            Shadowless(Picture(b, "RV room roof", V(-6.6f, Tier3 + 7.5f, d1), V(6.6f, Tier3 + 8f, F(R.ExitZ))));

            // 2 x 2 m from the pit's bottom to each slab's underside.
            Pier(b, "forecourt W", -7f, 9f, Tier0 - 1f);
            Pier(b, "forecourt E", 7f, 9f, Tier0 - 1f);
            Pier(b, "corridor", F(R.CorridorX), 19f, Tier0 - 1f);
            Pier(b, "tower rest", 0f, F(R.TowerZ(R.TowerRest)), F(R.TowerTop(R.TowerRest)) - 1f);
            Pier(b, "J1 W", -15f, 55f, Tier1 - 1f);
            Pier(b, "J1 middle", 0f, 55f, Tier1 - 1f);
            Pier(b, "walkway", 0f, 71.5f, Tier1 - 1f);
            Pier(b, "approach W", -9f, 86f, Tier1 - 1f);
            Pier(b, "approach E", 9f, 86f, Tier1 - 1f);
            Pier(b, "belt W", -3.5f, 96f, Tier1 - 1f);
            Pier(b, "belt E", 1.5f, 96f, Tier1 - 1f);
            Pier(b, "landing", -1f, 105f, Tier1 - 1f);
            Pier(b, "drawer cabinet", 9.1f, 99.9f, ShelfBottom);
            Pier(b, "shelf N", 5f, 120f, ShelfBottom);
            Pier(b, "J2 W", -9f, 131f, Tier2 - 1f);
            Pier(b, "J2 E", 9f, 131f, Tier2 - 1f);
            Pier(b, "terrace W", -9f, 173f, Tier3 - 1f);
            Pier(b, "terrace E", 9f, 173f, Tier3 - 1f);

            // Where the map test drops the pawn (PawnRushMapTest: a child named "Start"); nothing else reads it.
            var start = new GameObject("Start").transform;
            start.SetParent(b.parent, false);
            start.localPosition = V(0f, Tier0, 3f);
            start.localRotation = Quaternion.identity;
        }

        static void Pier(CourseBuilder b, string where, float x, float z, float top) =>
            Picture(b, "RV pier " + where, V(x - 1f, Pit, z - 1f), V(x + 1f, top, z + 1f));

        static void Shadowless(GameObject picture)
        {
            var renderer = picture.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ the vocabulary

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static float F(double v) => (float)v;

        // The bottom of the pit: everything that goes down into it starts here.
        static float Pit => (float)R.PitBottom;

        // The shelf's slab (and the drawer cabinet's) is 0.6 m thick under the J2 tier.
        const float ShelfBottom = Tier2 - .6f;

        // A walkable top (CourseFloor) that is never climbed (NoClimbSurface on the same object), x0..x1, y0..y1, z0..z1.
        static GameObject FloorNC(CourseBuilder b, string name, float x0, float x1, float y0, float y1, float z0, float z1)
        {
            var go = b.Box(name, V(x0, y0, z0), V(x1, y1, z1), b.kit.floorChecker);
            if (!go.TryGetComponent<CourseFloor>(out _)) go.AddComponent<CourseFloor>();
            if (!go.TryGetComponent<NoClimbSurface>(out _)) go.AddComponent<NoClimbSurface>();
            return go;
        }

        // A 1 m slab whose top is `top`: the vault's floors.
        static GameObject Slab(CourseBuilder b, string name, float x0, float x1, float z0, float z1, float top) =>
            FloorNC(b, name, x0, x1, top - 1f, top, z0, z1);

        // A solid that is never climbed: a wall, a cabinet, a pocket.
        static GameObject Block(CourseBuilder b, string name, float x0, float x1, float y0, float y1, float z0, float z1) =>
            b.Box(name, V(x0, y0, z0), V(x1, y1, z1), b.kit.wallNoClimb, true);

        // A railing 1.2 m over its floor, reaching `below` under it (a slab's metre; the shelf's 0.6).
        static GameObject Rail(CourseBuilder b, string name, float x0, float x1, float z0, float z1, float floor, float below = 1f) =>
            Block(b, name, x0, x1, floor - below, floor + 1.2f, z0, z1);

        // A picture: no collider (GrandStairHallBuilder's).
        static GameObject Picture(CourseBuilder b, string name, Vector3 a, Vector3 c, Material material) =>
            b.Box(name, a, c, material, false, false);

        static GameObject Picture(CourseBuilder b, string name, Vector3 a, Vector3 c) => Picture(b, name, a, c, RoyalVaultLook.Greybox);

        // The walking surface of a flight: the slope stays, its picture goes (GrandStairHallBuilder's).
        static GameObject Walk(GameObject slope)
        {
            var renderer = slope.GetComponent<MeshRenderer>();
            if (renderer != null) Object.DestroyImmediate(renderer);
            var filter = slope.GetComponent<MeshFilter>();
            if (filter != null) Object.DestroyImmediate(filter);
            slope.AddComponent<CourseFloor>().sloped = true;
            return slope;
        }

        // A flight rising along +z from zFoot: `steps` steps of `rise` x `depth`, x0..x1 wide. The steps are pictures standing
        // on `bottom`; the walking slope runs along their middle line, from half a step before the first riser (yFoot) to half
        // a step before the last one (the top) - the last step overlaps the upper floor. GrandStairHallBuilder's, with the
        // steps' bottom a parameter (the hall's is its Deep, -5; the vault's the pit's bottom).
        static void FlightZ(CourseBuilder b, string name, float x0, float x1, float zFoot, int steps, float depth, float yFoot, float bottom,
                            float rise = .25f)
        {
            for (int k = 1; k <= steps; k++)
                Picture(b, name + " step " + k, V(x0, bottom, zFoot + (k - 1) * depth), V(x1, yFoot + k * rise, zFoot + k * depth), b.kit.floorChecker);
            Walk(b.Slope(name + " (walking slope)", x0, x1, zFoot - depth * .5f, zFoot + (steps - .5f) * depth,
                         yFoot, yFoot + steps * rise, .6f, 0f, b.kit.floorChecker, true));
        }

        // A railing along a flight (along z): the walking line + 1.2 on top, 6 m below (GrandStairHallBuilder's).
        static void RailAlongZ(CourseBuilder b, string name, float x0, float x1, float zFoot, int steps, float depth, float yFoot, float rise = .25f)
        {
            float z0 = zFoot - depth * .5f, z1 = zFoot + (steps - .5f) * depth;
            b.Slope(name, x0, x1, z0, z1, yFoot, yFoot + steps * rise, 6f, 1.2f, b.kit.wallNoClimb, true);
        }

        // One of the three stairs: from (zA, yA) 4 m up over 12 m along z, x0..x1 wide, railed both sides 0.3 m out. The lower
        // floor stops at zA and the first riser stands half a tread on, so that first half tread is drawn at the lower floor's
        // height too (a picture: the walking slope carries the pawns there), or the pit would show through a slot at the foot.
        static void Stair(CourseBuilder b, string name, string rails, float x0, float x1, float zA, float yA)
        {
            float foot = zA + StairTread * .5f;
            FlightZ(b, name, x0, x1, foot, StairSteps, StairTread, yA, Pit);
            Picture(b, name + " step 0", V(x0, Pit, zA), V(x1, yA, foot), b.kit.floorChecker);
            RailAlongZ(b, rails + " W", x0 - .3f, x0, foot, StairSteps, StairTread, yA);
            RailAlongZ(b, rails + " E", x1, x1 + .3f, foot, StairSteps, StairTread, yA);
        }

        // A box on a moving root, in the root's frame (YardKit's), in the grey-box material.
        static GameObject Part(Transform parent, string name, Vector3 center, Vector3 size, YardKit.Solid solid) =>
            YardKit.Box(parent, name, center, size, RoyalVaultLook.Greybox, solid);

        // An empty root at a section point: a mover's, a span's, a marker's.
        static Transform Root(CourseBuilder b, string name, Vector3 at, float yaw = 0f)
        {
            var t = b.Group(name);
            t.localPosition = at;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        // A piece built by an earlier partial, by name, straight under Content.
        static Transform Built(CourseBuilder b, string name)
        {
            var t = b.parent.Find(name);
            if (t == null) Debug.LogError("[왕실 금고실] '" + name + "'이(가) 지어지지 않았습니다");
            return t;
        }
    }
}
