using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Graybox parts the audience hall's variants share (JY-audience-hall, the variants draft v0.2,
    // Docs/PawnRush/PAWN_RUSH_AUDIENCE_HALL_VARIANTS_v0.2.md): B CathedralHallSection, C RotundaHallSection,
    // D GiantThroneHallSection. A (AudienceHallSection) keeps its own. Positions are the section's frame: entry
    // (0, 0, 0) in the middle of the south door, +z north toward the throne.
    public static class HallParts
    {
        // Step pictures reach this far under the floor they start on; a walking slope is this thick under its line.
        public const float Deep = -.5f, Under = .6f;

        // A step's picture: seen, never touched.
        public static GameObject Picture(CourseBuilder b, string name, Vector3 a, Vector3 c) =>
            b.Box(name, a, c, b.kit.wallNoClimb, false, false);

        // A flight's walking slope: walked on, not seen (GrandStairHallBuilder.Walk: real risers catch a ragdoll's feet).
        public static GameObject Walk(GameObject slope)
        {
            var renderer = slope.GetComponent<MeshRenderer>();
            if (renderer != null) Object.DestroyImmediate(renderer);
            var filter = slope.GetComponent<MeshFilter>();
            if (filter != null) Object.DestroyImmediate(filter);
            slope.AddComponent<CourseFloor>().sloped = true;
            return slope;
        }

        // A flight rising along +z from its first riser at zFoot to a landing risers x rise up, x0..x1 wide. The last
        // step is the landing's own floor, so the landing starts (risers - 1) treads on.
        public static void FlightNorth(CourseBuilder b, string name, float x0, float x1, float zFoot, int risers, float rise, float tread, float yFoot = 0f)
        {
            for (int k = 1; k < risers; k++)
                Picture(b, name + " step " + k, new Vector3(x0, yFoot + Deep, zFoot + (k - 1) * tread), new Vector3(x1, yFoot + k * rise, zFoot + k * tread));
            Walk(b.Slope(name + " (walking slope)", x0, x1, zFoot - tread * .5f, zFoot + (risers - .5f) * tread,
                         yFoot, yFoot + risers * rise, Under, 0f, b.kit.floorChecker, true));
        }

        // The same rising along x toward `dir` (+1 east, -1 west) from its first riser at xFoot, z0..z1 wide.
        public static void FlightAcross(CourseBuilder b, string name, float z0, float z1, float xFoot, float dir, int risers, float rise, float tread, float yFoot = 0f)
        {
            for (int k = 1; k < risers; k++)
            {
                float a = xFoot + dir * (k - 1) * tread, c = xFoot + dir * k * tread;
                Picture(b, name + " step " + k, new Vector3(Mathf.Min(a, c), yFoot + Deep, z0), new Vector3(Mathf.Max(a, c), yFoot + k * rise, z1));
            }
            Walk(b.SlopeX(name + " (walking slope)", z0, z1, xFoot - dir * tread * .5f, xFoot + dir * (risers - .5f) * tread,
                          yFoot, yFoot + risers * rise, Under, 0f, b.kit.floorChecker, true));
        }

        // The finish: the gold circle on the floor at `middle` and the box CrownFinish reads (its narrower half is the
        // radius, its floor the middle).
        public static void Platform(CourseBuilder b, Vector3 middle, float radius)
        {
            YardKit.Cylinder(b.parent, "Finish circle", middle + Vector3.up * .02f, Vector3.up, radius * 2f, .03f, YardKit.Gold, YardKit.Solid.None);
            b.Trigger<FinishZone>("Finish (goal)", middle + new Vector3(-radius, 0f, -radius), middle + new Vector3(radius, 3.4f, radius)).Configure(true);
        }

        // A throne facing south, its seat's front middle at `front` on the floor it stands on, `scale` times the
        // audience hall A's (2 m wide, seat 0.5 m, arms 0.9 m, back 2.6 m): hands hold it, feet climb it.
        public static void Throne(CourseBuilder b, Vector3 front, float scale)
        {
            float w = scale, arm = 1.25f * scale, d = 1.2f * scale, backD = .3f * scale;
            var gold = b.kit.rankGold;
            b.Box("Throne seat", front + new Vector3(-w, 0f, 0f), front + new Vector3(w, .5f * scale, d), gold);
            b.Box("Throne back", front + new Vector3(-arm, 0f, d - backD), front + new Vector3(arm, 2.6f * scale, d), gold);
            b.Box("Throne arm W", front + new Vector3(-arm, 0f, 0f), front + new Vector3(-w, .9f * scale, d - backD), gold);
            b.Box("Throne arm E", front + new Vector3(w, 0f, 0f), front + new Vector3(arm, .9f * scale, d - backD), gold);
        }

        // A crown whose band stands on `at`, seen from anywhere, touching nothing.
        public static void Crown(CourseBuilder b, Vector3 at, float scale)
        {
            b.Cylinder("Crown band", at, 1.6f * scale, .5f * scale, b.kit.rankGold, false);
            var half = new Vector3(.12f, 0f, .12f) * scale;
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                var p = at + new Vector3(Mathf.Sin(a) * .7f, .5f, Mathf.Cos(a) * .7f) * scale;
                b.Box("Crown point " + (i + 1), p - half, p + half + Vector3.up * .45f * scale, b.kit.rankGold, false, false);
            }
        }

        // A candle-coloured lamp without shadows (GrandStairHallModel.Lamp's settings).
        public static Light Lamp(CourseBuilder b, string name, Vector3 at, float range, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(b.parent, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.color = new Color(1f, .82f, .58f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            return light;
        }

        // A spotlight straight down on `at` from `height` above it: the window's light on the throne.
        public static Light Shaft(CourseBuilder b, string name, Vector3 at, float height, float angle, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(b.parent, false);
            light.transform.localPosition = at + Vector3.up * height;
            light.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            light.type = LightType.Spot;
            light.spotAngle = angle;
            light.range = height * 1.3f;
            light.intensity = intensity;
            light.color = new Color(1f, .92f, .76f);
            light.shadows = LightShadows.Soft;
            return light;
        }

        // A box turned to `rotation` (the builder's boxes are square to the frame): the rotunda's wall and dome panels.
        public static GameObject Panel(CourseBuilder b, string name, Vector3 center, Quaternion rotation, Vector3 size, Material material, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(b.parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rotation;
            go.transform.localScale = size;
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var collider = go.GetComponent<Collider>();
            if (!solid) Object.DestroyImmediate(collider);
            else go.AddComponent<NoClimbSurface>();
            return go;
        }

        // A rectangular hall's walls (x -halfWidth..halfWidth, z 0..length), nobody climbs them; the south wall open
        // doorHalf either side of the middle up to doorHeight, where the corridor joins (12 m wide).
        public static void Walls(CourseBuilder b, float halfWidth, float length, float height, float doorHalf, float doorHeight, float thick = .6f)
        {
            float w = halfWidth + thick;
            b.Wall("Wall W", -w, -halfWidth, -1f, height, 0f, length + thick, false);
            b.Wall("Wall E", halfWidth, w, -1f, height, 0f, length + thick, false);
            b.Wall("Wall N", -w, w, -1f, height, length, length + thick, false);
            b.Wall("Wall S W", -w, -doorHalf, -1f, height, 0f, thick, false);
            b.Wall("Wall S E", doorHalf, w, -1f, height, 0f, thick, false);
            b.Wall("Door lintel", -doorHalf, doorHalf, doorHeight, height, 0f, thick, false);
        }

        // A tall team banner hanging on the west (white) or east (black) wall, z0..z1 (looks only).
        public static void Banner(CourseBuilder b, int team, float halfWidth, float z0, float z1, float y0, float y1)
        {
            float x = team == Teams.White ? -halfWidth : halfWidth - .08f;
            b.Box(team == Teams.White ? "Banner W (white)" : "Banner E (black)", new Vector3(x, y0, z0), new Vector3(x + .08f, y1, z1),
                  b.kit.Team(team), false, false);
        }

        // What every hall ends with: CP0 over the floor inside the door (z 0..cpDepth), a kill volume far below, the
        // progress path along `route`.
        public static void Ends(CourseBuilder b, Transform root, float halfWidth, float length, float cpDepth, Vector3[] route)
        {
            var cp = b.Checkpoint(0, new Vector3(0f, 0f, 2f), 0f, 12f);
            CourseBuilder.CoverFloor(cp, -halfWidth, halfWidth, 0f, cpDepth);
            b.Trigger<KillVolume>("Kill volume (far below)", new Vector3(-halfWidth - 20f, -42f, -10f), new Vector3(halfWidth + 20f, -40f, length + 10f));
            var path = new GameObject("ProgressPath");
            path.transform.SetParent(root, false);
            var labels = new string[route.Length];
            for (int i = 0; i < labels.Length; i++) labels[i] = "1";
            path.AddComponent<ProgressPath>().Configure(route, (Vector3[])route.Clone(), labels);
        }
    }
}
