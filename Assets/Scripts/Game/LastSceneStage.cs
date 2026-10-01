using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // The finish of the Pawn Rush course as the last scene shows it, drawn after the
    // PAWN RUSH reference picture: a glossy checkerboard plaza and runway lined with
    // red and cream blocks, the coral finish arch with its gold crown, a cream castle
    // with crown banners, giant black bishops swinging maces and a giant rolling rook,
    // under a blue sky. Built from code, so no scene or asset file changes.
    //
    // Layout: the camera looks along +Z; the winners stand just in front of the arch
    // at Z = 15 and the runway runs on behind it to the gatehouse at Z = 78.
    public sealed class LastSceneStage
    {
        public const float Top = .6f;       // the plaza and runway surface
        public const float ArchZ = 15f;
        public const float PlazaHalfWidth = 8.6f, RunwayHalfWidth = 6.4f, RunwayEnd = 71f, PlazaFront = -10f;

        readonly List<(Transform t, float phase, float amp, float sign)> flags = new List<(Transform, float, float, float)>();
        readonly List<(Transform arm, float phase, float sign)> maces = new List<(Transform, float, float)>();
        readonly List<(Transform t, Vector3 home, float phase)> clouds = new List<(Transform, Vector3, float)>();
        Transform crown, roller;

        public static float GroundY(float x, float z) => OnStage(x, z) ? Top : 0f;
        public static bool OnStage(float x, float z) =>
            (Mathf.Abs(x) < PlazaHalfWidth && z > PlazaFront && z < ArchZ) || (Mathf.Abs(x) < RunwayHalfWidth && z >= ArchZ && z < RunwayEnd);

        // `sunny` false is the defeat mood: grey sky, cool light.
        public static LastSceneStage Build(Transform parent, Camera view, bool sunny)
        {
            var stage = new LastSceneStage();
            var fixedParts = LastSceneArt.Group(parent, "Stage (static)", Vector3.zero).transform;
            var moving = LastSceneArt.Group(parent, "Stage (moving)", Vector3.zero).transform;
            Environment(parent, view, sunny);
            Floors(fixedParts);
            Barriers(fixedParts);
            stage.Arch(fixedParts, moving);
            stage.Castle(fixedParts, moving);
            stage.Giants(moving);
            stage.Clouds(moving, sunny);
            // Hundreds of still parts drawn in a few batches.
            StaticBatchingUtility.Combine(fixedParts.gameObject);
            return stage;
        }

        public void Update(float time)
        {
            foreach (var f in flags) f.t.localRotation = Quaternion.Euler(0, Mathf.Sin(time * 2.4f + f.phase) * f.amp * f.sign * Mathf.Rad2Deg, 0);
            foreach (var m in maces)
                m.arm.localRotation = Quaternion.Euler(0, Mathf.Sin(time * 1.1f + m.phase) * 63f, m.sign * (8.6f + Mathf.Sin(time * 2.2f + m.phase) * 7f));
            if (crown != null) crown.localRotation = Quaternion.Euler(0, Mathf.Sin(time * .6f) * 14f, 0);
            if (roller != null) roller.localRotation = Quaternion.Euler(time * 52f, 0, 0);
            foreach (var c in clouds) c.t.localPosition = c.home + new Vector3(Mathf.Sin(time * .03f + c.phase) * 12f, 0, 0);
        }

        // ---------- light and air ----------

        static void Environment(Transform parent, Camera view, bool sunny)
        {
            var sky = LastSceneArt.Sky(sunny);
            if (sky != null) { RenderSettings.skybox = sky; view.clearFlags = CameraClearFlags.Skybox; }
            else { view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = LastSceneArt.Hex(sunny ? 0x6AAEF0 : 0x7D8AA0); }
            view.farClipPlane = 600f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = LastSceneArt.Hex(sunny ? 0xD6ECFB : 0xC3CCD8);
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 210f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sunny ? new Color(.80f, .88f, 1f) : new Color(.62f, .66f, .74f);
            RenderSettings.ambientEquatorColor = sunny ? new Color(.86f, .86f, .86f) : new Color(.6f, .62f, .66f);
            RenderSettings.ambientGroundColor = sunny ? new Color(.78f, .72f, .62f) : new Color(.48f, .47f, .45f);

            var sun = Light(parent, "Sun", new Vector3(16f, -28f, 20f), LastSceneArt.Hex(sunny ? 0xFFF0D6 : 0xD9E3F0), sunny ? 1.2f : .55f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = sunny ? .62f : .4f;
            sun.shadowBias = .04f;
            sun.shadowNormalBias = .3f;
            Light(parent, "Fill", new Vector3(-6f, -4f, 9f), LastSceneArt.Hex(sunny ? 0xFFF1E4 : 0xC9D6E8), sunny ? .28f : .2f);
        }

        static Light Light(Transform parent, string name, Vector3 direction, Color color, float intensity)
        {
            var go = LastSceneArt.Group(parent, name, Vector3.zero, Quaternion.LookRotation(direction.normalized));
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        // ---------- ground ----------

        static void Floors(Transform parent)
        {
            var checker = LastSceneArt.Checker();
            var side = LastSceneArt.Lit(0xE3D5BD, .1f);
            void Floor(string name, float width, float length, float centerZ)
            {
                var material = LastSceneArt.Textured(name, checker, .62f, new Vector2(width / 3.2f, length / 3.2f));
                LastSceneArt.Part(parent, name, LastSceneArt.Panel(width, length), material, new Vector3(0, Top + .002f, centerZ), Quaternion.Euler(90f, 0, 0), null, false);
                StageKit.Box(parent, name + " Sides", new Vector3(0, Top / 2, centerZ), new Vector3(width, Top, length), side);
            }
            Floor("Plaza", PlazaHalfWidth * 2, ArchZ - PlazaFront, (ArchZ + PlazaFront) / 2);
            Floor("Runway", RunwayHalfWidth * 2, RunwayEnd - ArchZ, (RunwayEnd + ArchZ) / 2);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Courtyard";
            Object.Destroy(ground.GetComponent<Collider>());
            ground.transform.SetParent(parent, false);
            ground.transform.localScale = new Vector3(50f, 1f, 50f);
            var renderer = ground.GetComponent<Renderer>();
            renderer.sharedMaterial = LastSceneArt.Lit(0xE6DAC6, .05f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        // Chunky red and cream blocks along both edges, as in the reference.
        static void Barriers(Transform parent)
        {
            Mesh block = LastSceneArt.RoundedBox(new Vector3(1f, .9f, 1.52f), .16f);
            Material red = LastSceneArt.Lit(0xD8433C, .55f), cream = LastSceneArt.Lit(0xF4ECDD, .5f);
            for (int i = 0; i < 15; i++)
                for (int s = -1; s <= 1; s += 2)
                    LastSceneArt.Part(parent, "Block", block, i % 2 == 0 ? red : cream, new Vector3(s * PlazaHalfWidth, Top + .45f, ArchZ - .8f - i * 1.6f));
            for (int i = 0; i < 35; i++)
                for (int s = -1; s <= 1; s += 2)
                    LastSceneArt.Part(parent, "Block", block, i % 2 == 1 ? red : cream, new Vector3(s * 6.9f, Top + .45f, ArchZ + .8f + i * 1.6f));
            for (int s = -1; s <= 1; s += 2)
            {
                LastSceneArt.Part(parent, "Corner Post", LastSceneArt.RoundedBox(new Vector3(1.2f, 1.5f, 1.2f), .18f), red, new Vector3(s * PlazaHalfWidth, Top + .75f, PlazaFront));
                LastSceneArt.Part(parent, "Corner Ball", LastSceneArt.Sphere(.32f, 18, 14), LastSceneArt.Lit(0xF2C14E, .7f, .7f), new Vector3(s * PlazaHalfWidth, Top + 1.7f, PlazaFront));
            }
        }

        // ---------- the finish arch ----------

        void Arch(Transform fixedParts, Transform moving)
        {
            const float w = 9.6f, h = 7.4f, ow = 6.2f, oh = 5.9f, depth = 1.5f;
            var arch = LastSceneArt.Part(fixedParts, "Finish Arch", LastSceneArt.Arch(w, h, ow, oh, depth), LastSceneArt.Lit(0xE8735F, .5f), new Vector3(0, Top, ArchZ));
            arch.GetComponent<MeshRenderer>().sharedMaterials = new[] { LastSceneArt.Lit(0xE8735F, .5f), LastSceneArt.Lit(0xC4554B, .45f) };
            var gold = LastSceneArt.Lit(0xF2C14E, .72f, .75f);
            LastSceneArt.Part(fixedParts, "Arch Trim", LastSceneArt.RoundedBox(new Vector3(w + .5f, .32f, depth + .2f), .12f), gold, new Vector3(0, Top + h + .1f, ArchZ));

            crown = LastSceneArt.Group(moving, "Arch Crown", new Vector3(0, Top + h + .25f, ArchZ)).transform;
            LastSceneArt.Part(crown, "Band", LastSceneArt.Cylinder(1.0f, 1.15f, .85f, 32), gold, new Vector3(0, .42f, 0));
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2;
                var spot = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                LastSceneArt.Part(crown, "Point", LastSceneArt.Cone(.3f, .95f, 14), gold, spot * 1.0f + Vector3.up * 1.3f);
                LastSceneArt.Part(crown, "Pearl", LastSceneArt.Sphere(.15f, 14, 10), gold, spot * 1.0f + Vector3.up * 1.82f);
            }
            int[] jewels = { 0xD8433C, 0x2F6FE0, 0x2F6FE0 };
            float[] at = { 0, .5f, -.5f };
            for (int i = 0; i < 3; i++)
                LastSceneArt.Part(crown, "Jewel", LastSceneArt.Sphere(.17f, 16, 12), LastSceneArt.Lit(jewels[i], .85f, .1f),
                                  new Vector3(Mathf.Sin(Mathf.PI + at[i]) * 1.13f, .45f, Mathf.Cos(Mathf.PI + at[i]) * 1.13f));

            for (int s = -1; s <= 1; s += 2)
            {
                Banner(fixedParts, 0x5B3FA6, 1.3f, 3.4f, new Vector3(s * (w + ow) / 4f, Top + 3.6f, ArchZ - depth / 2 - .03f), 0);
                CheckerFlag(fixedParts, moving, new Vector3(s * 5.6f, Top, ArchZ - .6f), s);
            }
        }

        static void Banner(Transform parent, int rgb, float w, float h, Vector3 position, float yawDegrees)
        {
            var material = LastSceneArt.Cutout("banner " + rgb, LastSceneArt.Banner(rgb), .2f);
            LastSceneArt.Part(parent, "Banner", LastSceneArt.Panel(w, h), material, position, Quaternion.Euler(0, yawDegrees, 0));
        }

        void CheckerFlag(Transform fixedParts, Transform moving, Vector3 foot, int side)
        {
            LastSceneArt.Part(fixedParts, "Flag Pole", LastSceneArt.Cylinder(.07f, .07f, 4.8f, 10), LastSceneArt.Lit(0xD9D3C7, .6f, .3f), foot + Vector3.up * 2.4f);
            LastSceneArt.Part(fixedParts, "Flag Knob", LastSceneArt.Sphere(.12f, 12, 10), LastSceneArt.Lit(0xF2C14E, .72f, .75f), foot + Vector3.up * 4.86f);
            var pivot = LastSceneArt.Group(moving, "Checker Flag", foot + Vector3.up * 4.25f).transform;
            var material = LastSceneArt.Textured("checker flag", LastSceneArt.CheckerFlag(), .2f, Vector2.one);
            LastSceneArt.Part(pivot, "Cloth", LastSceneArt.Panel(1.6f, 1.05f), material, new Vector3(side * .8f, 0, 0));
            flags.Add((pivot, foot.x * .5f, .35f, side));
        }

        // ---------- the castle ----------

        const int Blue = 0x2F58B5, Red = 0xC63B3F, RoofBlue = 0x4E64A6, RoofRed = 0xC9483F;
        static readonly Vector3 CameraSpot = new Vector3(0, 0, -16f);

        void Castle(Transform fixedParts, Transform moving)
        {
            // Low battlements beside the plaza.
            Wall(fixedParts, new Vector2(-12, -9), new Vector2(-12, 14), 3.6f, 1.6f);
            Wall(fixedParts, new Vector2(12, -9), new Vector2(12, 14), 3.6f, 1.6f);
            Tower(fixedParts, moving, -12, -9, 1.7f, 7, 0, 0, Blue);
            Tower(fixedParts, moving, 12, -9, 1.7f, 7, 0, 0, Red);
            // Walls along the runway beyond the arch, with crown banners facing it.
            var left = Wall(fixedParts, new Vector2(-10.5f, 16), new Vector2(-10.5f, 52), 8f, 2f);
            var right = Wall(fixedParts, new Vector2(10.5f, 16), new Vector2(10.5f, 52), 7.5f, 2f);
            for (int i = 0; i < 3; i++)
            {
                WallBanner(fixedParts, left, -12 + i * 12, i % 2 == 0 ? Blue : Red);
                WallBanner(fixedParts, right, -12 + i * 12, i % 2 == 0 ? Red : Blue);
            }
            Tower(fixedParts, moving, -11.5f, 15, 2.6f, 14, 0, Blue, Red);
            Tower(fixedParts, moving, -10.5f, 33, 2.8f, 17, RoofBlue, Red, Blue);
            Tower(fixedParts, moving, -10.5f, 52, 3.2f, 20, 0, Blue, Red);
            Tower(fixedParts, moving, 11.5f, 15, 2.6f, 13, 0, Red, Blue);
            Tower(fixedParts, moving, 10.5f, 33, 2.8f, 16, 0, Blue, Red);
            Tower(fixedParts, moving, 10.5f, 52, 3.2f, 21, RoofRed, Red, Blue);
            // The gatehouse at the far end of the runway.
            Wall(fixedParts, new Vector2(-22, 78), new Vector2(22, 78), 11f, 2.6f);
            LastSceneArt.Part(fixedParts, "Gate", LastSceneArt.RoundedBox(new Vector3(6f, 7.2f, .5f), .6f), LastSceneArt.Lit(0x2C3044, .3f), new Vector3(0, 3.6f, 76.6f), null, null, false);
            Tower(fixedParts, moving, -8, 78, 3.6f, 24, RoofBlue, Blue, Red);
            Tower(fixedParts, moving, 8, 78, 3.6f, 24, RoofRed, Red, Blue);
            Wall(fixedParts, new Vector2(-10.5f, 52), new Vector2(-22, 64), 7f, 2f);
            Wall(fixedParts, new Vector2(10.5f, 52), new Vector2(24, 66), 7f, 2f);
            Tower(fixedParts, moving, -22, 64, 3.0f, 22, 0, 0, Blue);
            Tower(fixedParts, moving, 24, 66, 3.2f, 24, RoofBlue, 0, Red);
            // The city beyond, fading into the haze.
            float[,] far = { { -16, 104, 4.2f, 32, 1 }, { 18, 112, 4.4f, 34, 0 }, { -44, 84, 4, 28, 2 }, { 46, 94, 4, 30, 0 }, { 0, 132, 5, 40, 1 },
                             { -30, 122, 3.6f, 30, 0 }, { 32, 128, 3.8f, 32, 2 }, { -60, 60, 4, 24, 1 }, { 60, 66, 4, 26, 0 } };
            for (int i = 0; i < far.GetLength(0); i++)
            {
                int roof = far[i, 4] == 1 ? RoofBlue : far[i, 4] == 2 ? RoofRed : 0;
                Tower(fixedParts, moving, far[i, 0], far[i, 1], far[i, 2], far[i, 3], roof, 0, 0);
            }
        }

        struct WallInfo { public Vector3 Center, Inward, Along; public float Length, Height, Thickness; }

        static WallInfo Wall(Transform parent, Vector2 a, Vector2 b, float height, float thickness)
        {
            Vector3 from = new Vector3(a.x, 0, a.y), to = new Vector3(b.x, 0, b.y), center = (from + to) / 2;
            float length = Vector3.Distance(from, to);
            Vector3 along = (to - from).normalized;
            var rotation = Quaternion.LookRotation(along);
            StageKit.Box(parent, "Wall", center + Vector3.up * height / 2, new Vector3(thickness, height, length), LastSceneArt.Lit(0xEAD9BF, .1f)).transform.localRotation = rotation;
            StageKit.Box(parent, "Wall Cap", center + Vector3.up * (height - .2f), new Vector3(thickness + .3f, .36f, length), LastSceneArt.Lit(0xDCCAAE, .1f)).transform.localRotation = rotation;
            Vector3 right = rotation * Vector3.right;
            Vector3 inward = Vector3.Dot(right, -center) > 0 ? right : -right;
            Mesh merlon = LastSceneArt.RoundedBox(new Vector3(.8f, .9f, .6f), .1f, 3);
            Material stone = LastSceneArt.Lit(0xEFE3CE, .1f), window = LastSceneArt.Lit(0x3A3E52, .3f);
            for (int i = 0, n = Mathf.FloorToInt(length / 1.5f); i < n; i++)
            {
                float d = -length / 2 + .75f + i * 1.5f;
                LastSceneArt.Part(parent, "Merlon", merlon, stone, center + inward * thickness * .32f + along * d + Vector3.up * (height + .45f), rotation);
            }
            for (int i = 0, n = Mathf.FloorToInt(length / 4f); i < n; i++)
            {
                float d = -length / 2 + 2 + i * 4;
                LastSceneArt.Part(parent, "Window", LastSceneArt.RoundedBox(new Vector3(.62f, 1.05f, .22f), .24f, 3), window,
                                  center + inward * (thickness / 2 + .03f) + along * d + Vector3.up * height * .5f, Quaternion.LookRotation(inward), null, false);
            }
            return new WallInfo { Center = center, Inward = inward, Along = along, Length = length, Height = height, Thickness = thickness };
        }

        static void WallBanner(Transform parent, WallInfo wall, float offset, int rgb) =>
            Banner(parent, rgb, 1.7f, 4.2f, wall.Center + wall.Inward * (wall.Thickness / 2 + .06f) + wall.Along * offset + Vector3.up * wall.Height * .55f,
                   Quaternion.LookRotation(-wall.Inward).eulerAngles.y);

        // A round tower facing the camera: crenellated (rook) top or a cone roof, stone
        // bands, two windows, a crown banner and a pennant when asked for.
        void Tower(Transform fixedParts, Transform moving, float x, float z, float r, float h, int roof, int banner, int pennant)
        {
            Vector3 foot = new Vector3(x, 0, z);
            Material stone = LastSceneArt.Lit(0xEFE3CE, .1f), band = LastSceneArt.Lit(0xE0D0B5, .1f);
            LastSceneArt.Part(fixedParts, "Tower", LastSceneArt.Cylinder(r * 1.06f, r, h, 28), stone, foot + Vector3.up * h / 2);
            for (int k = 1; k <= 3; k++)
                LastSceneArt.Part(fixedParts, "Band", LastSceneArt.Cylinder(r * 1.035f, r * 1.035f, .3f, 28), band, foot + Vector3.up * h * k / 4, null, null, false);
            float face = Mathf.Atan2(CameraSpot.x - x, CameraSpot.z - z);
            var window = LastSceneArt.RoundedBox(new Vector3(.62f, 1.05f, .22f), .24f, 3);
            for (int i = 0; i < 2; i++)
            {
                float a = face + (i == 0 ? -.32f : .42f);
                var outward = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                LastSceneArt.Part(fixedParts, "Window", window, LastSceneArt.Lit(0x3A3E52, .3f), foot + outward * r * .99f + Vector3.up * h * (i == 0 ? .42f : .7f),
                                  Quaternion.LookRotation(outward), null, false);
            }
            float top;
            if (roof != 0)
            {
                LastSceneArt.Part(fixedParts, "Roof Ring", LastSceneArt.Cylinder(r * 1.18f, r * 1.18f, .7f, 28), band, foot + Vector3.up * (h + .35f), null, null, false);
                LastSceneArt.Part(fixedParts, "Roof", LastSceneArt.Cone(r * 1.28f, r * 2.1f, 28), LastSceneArt.Lit(roof, .45f), foot + Vector3.up * (h + .7f + r * 1.05f));
                top = h + .7f + r * 2.1f;
            }
            else
            {
                LastSceneArt.Part(fixedParts, "Battlement", LastSceneArt.Cylinder(r * 1.14f, r * 1.2f, 1.1f, 28), band, foot + Vector3.up * (h + .55f));
                int n = Mathf.Max(6, Mathf.RoundToInt(r * 3));
                var merlon = LastSceneArt.RoundedBox(new Vector3(.8f, .9f, .6f) * (r > 3 ? 1.2f : 1f), .1f, 3);
                for (int i = 0; i < n; i++)
                {
                    float a = i / (float)n * Mathf.PI * 2;
                    LastSceneArt.Part(fixedParts, "Merlon", merlon, stone, foot + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * r * 1.1f + Vector3.up * (h + 1.55f),
                                      Quaternion.Euler(0, a * Mathf.Rad2Deg, 0));
                }
                top = h + 1.1f;
            }
            if (banner != 0)
            {
                var outward = new Vector3(Mathf.Sin(face), 0, Mathf.Cos(face));
                Banner(fixedParts, banner, r * .9f, r * 2.15f, foot + outward * r * 1.08f + Vector3.up * h * .6f, face * Mathf.Rad2Deg);
            }
            if (pennant != 0)
            {
                LastSceneArt.Part(fixedParts, "Pennant Pole", LastSceneArt.Cylinder(.05f, .05f, 2.6f, 8), LastSceneArt.Lit(0xD9D3C7, .6f, .3f), foot + Vector3.up * (top + 1.3f), null, null, false);
                var pivot = LastSceneArt.Group(moving, "Pennant", foot + Vector3.up * (top + 2.3f)).transform;
                LastSceneArt.Part(pivot, "Cloth", LastSceneArt.Pennant(), LastSceneArt.Lit(pennant, .3f), Vector3.zero, null, null, false);
                flags.Add((pivot, x * .37f + z * .21f, .45f, 1f));
            }
        }

        // ---------- the course behind the arch ----------

        void Giants(Transform moving)
        {
            Material dark = LastSceneArt.Lit(0x2F2E37, .6f), trim = LastSceneArt.Lit(0x1D1C23, .5f);
            const float S = 3f;
            Vector2[] body = { new Vector2(0, 0), new Vector2(.55f, 0), new Vector2(.6f, .1f), new Vector2(.56f, .22f), new Vector2(.48f, .3f), new Vector2(.42f, .45f), new Vector2(.36f, .7f), new Vector2(.32f, .92f), new Vector2(0, .92f) };
            Vector2[] mitre = { new Vector2(0, 1.0f), new Vector2(.3f, 1.02f), new Vector2(.37f, 1.18f), new Vector2(.38f, 1.36f), new Vector2(.33f, 1.54f), new Vector2(.22f, 1.71f), new Vector2(.08f, 1.82f), new Vector2(0, 1.84f) };
            for (int i = 0; i < body.Length; i++) body[i] *= S;
            for (int i = 0; i < mitre.Length; i++) mitre[i] *= S;
            float[,] spots = { { -3.4f, 30, 0, 1 }, { 3.6f, 46, 1.6f, -1 } };
            for (int k = 0; k < 2; k++)
            {
                float x = spots[k, 0], z = spots[k, 1], phase = spots[k, 2], s = spots[k, 3];
                var bishop = LastSceneArt.Group(moving, "Giant Bishop", new Vector3(x, Top, z), Quaternion.Euler(0, 180f + s * 14f, 0)).transform;
                LastSceneArt.Part(bishop, "Body", LastSceneArt.Lathe("giant bishop", body, 28), dark, Vector3.zero);
                LastSceneArt.Part(bishop, "Collar", LastSceneArt.Cylinder(.42f * S, .42f * S, .1f * S, 28), trim, new Vector3(0, .95f * S, 0));
                LastSceneArt.Part(bishop, "Mitre", LastSceneArt.Lathe("giant mitre", mitre, 28), dark, Vector3.zero);
                LastSceneArt.Part(bishop, "Knob", LastSceneArt.Sphere(.1f * S, 16, 12), dark, new Vector3(0, 1.9f * S, 0));
                LastSceneArt.Part(bishop, "Cross", PieceFigure.Cube(), trim, new Vector3(0, 1.4f * S, .37f * S), Quaternion.Euler(14f, 0, 0), new Vector3(.07f, .4f, .05f) * S, false);
                LastSceneArt.Part(bishop, "Cross Bar", PieceFigure.Cube(), trim, new Vector3(0, 1.46f * S, .355f * S), Quaternion.Euler(14f, 0, 0), new Vector3(.25f, .07f, .05f) * S, false);
                var arm = LastSceneArt.Group(bishop, "Arm", new Vector3(s * .42f * S, .78f * S, 0)).transform;
                LastSceneArt.Part(arm, "Sleeve", LastSceneArt.Cylinder(.08f * S, .09f * S, .3f * S, 14), dark, new Vector3(s * .14f * S, 0, 0), Quaternion.Euler(0, 0, 90f));
                var pole = LastSceneArt.Group(arm, "Mace", new Vector3(s * .32f * S, 0, 0), Quaternion.Euler(0, 0, s * 31.5f)).transform;
                LastSceneArt.Part(pole, "Handle", LastSceneArt.Cylinder(.035f * S, .035f * S, 1.6f * S, 10), LastSceneArt.Lit(0x6B5A48, .3f), new Vector3(s * .8f * S, 0, 0), Quaternion.Euler(0, 0, 90f));
                var ball = LastSceneArt.Group(pole, "Ball", new Vector3(s * 1.65f * S, 0, 0)).transform;
                LastSceneArt.Part(ball, "Head", LastSceneArt.Sphere(.26f * S, 20, 16), LastSceneArt.Lit(0x3D3C46, .6f, .4f), Vector3.zero);
                var spike = LastSceneArt.Cone(.07f * S, .22f * S, 10);
                var steel = LastSceneArt.Lit(0x9AA0AE, .7f, .6f);
                for (int i = 0; i < 14; i++)
                {
                    float a = i * 2.399f, y = 1 - 2 * (i + .5f) / 14, rr = Mathf.Sqrt(1 - y * y);
                    var n = new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
                    LastSceneArt.Part(ball, "Spike", spike, steel, n * .3f * S, Quaternion.FromToRotation(Vector3.up, n));
                }
                maces.Add((arm, phase, s));
            }
            // A giant rook rolling across the far runway.
            roller = LastSceneArt.Group(moving, "Giant Rook", new Vector3(0, Top + 1.45f, 60f)).transform;
            var lying = LastSceneArt.Group(roller, "Lying", Vector3.zero, Quaternion.Euler(0, 0, 90f)).transform;
            Vector2[] rook = { new Vector2(0, -1.9f), new Vector2(1.35f, -1.9f), new Vector2(1.4f, -1.6f), new Vector2(1.25f, -1.3f), new Vector2(1.2f, 1.2f), new Vector2(1.4f, 1.4f), new Vector2(1.4f, 1.9f), new Vector2(0, 1.9f) };
            LastSceneArt.Part(lying, "Body", LastSceneArt.Lathe("giant rook", rook, 28), LastSceneArt.Lit(0xF1E6D2, .5f), Vector3.zero);
            LastSceneArt.Part(lying, "Base", LastSceneArt.Cylinder(1.42f, 1.42f, .5f, 28), LastSceneArt.Lit(0x34323C, .55f), new Vector3(0, -1.75f, 0));
        }

        void Clouds(Transform moving, bool sunny)
        {
            var material = LastSceneArt.Blended(sunny ? "cloud" : "rain cloud", LastSceneArt.Cloud(sunny), Color.white);
            float[,] spots = { { -120, 46, 210, 90 }, { -40, 58, 250, 120 }, { 50, 50, 230, 100 }, { 130, 40, 200, 80 }, { -80, 30, 170, 70 }, { 90, 64, 270, 110 }, { 0, 36, 190, 60 } };
            for (int i = 0; i < spots.GetLength(0); i++)
            {
                var home = new Vector3(spots[i, 0], spots[i, 1], spots[i, 2]);
                float w = spots[i, 3];
                var cloud = LastSceneArt.Part(moving, "Cloud", LastSceneArt.Panel(w, w * .5f), material, home, null, null, false);
                cloud.GetComponent<MeshRenderer>().receiveShadows = false;
                clouds.Add((cloud.transform, home, i * 1.7f));
            }
        }
    }

    // Two confetti cannons at the plaza's sides that fire every 3.4 seconds from 1 s.
    public sealed class LastSceneCannons
    {
        public const float First = 1f, Period = 3.4f;
        readonly Transform[] barrels = new Transform[2];
        public readonly Vector3[] Muzzles = new Vector3[2], Directions = new Vector3[2];

        public LastSceneCannons(Transform parent)
        {
            Material navy = LastSceneArt.Lit(0x1F2B5B, .6f), red = LastSceneArt.Lit(0xD8433C, .55f), gold = LastSceneArt.Lit(0xF2C14E, .72f, .75f);
            for (int s = -1, i = 0; s <= 1; s += 2, i++)
            {
                var root = LastSceneArt.Group(parent, "Confetti Cannon", new Vector3(s * 7.3f, LastSceneStage.Top, -1.5f)).transform;
                LastSceneArt.Part(root, "Base", LastSceneArt.RoundedBox(new Vector3(.9f, .42f, .9f), .12f), navy, new Vector3(0, .21f, 0));
                barrels[i] = LastSceneArt.Group(root, "Barrel", new Vector3(0, .42f, 0), Quaternion.Euler(25.8f, 0, s * 24f)).transform;
                LastSceneArt.Part(barrels[i], "Tube", LastSceneArt.Cylinder(.21f, .25f, 1.05f, 18), red, new Vector3(0, .52f, 0));
                LastSceneArt.Part(barrels[i], "Rim", LastSceneArt.Cylinder(.27f, .27f, .14f, 18), gold, new Vector3(0, .98f, 0));
                LastSceneArt.Part(barrels[i], "Ring", LastSceneArt.Cylinder(.24f, .24f, .1f, 18), gold, new Vector3(0, .2f, 0));
                Muzzles[i] = barrels[i].TransformPoint(new Vector3(0, 1.1f, 0));
                Directions[i] = (Muzzles[i] - barrels[i].position).normalized;
            }
        }

        public void Update(float tp)
        {
            float since = tp < First ? -1 : (tp - First) % Period, kick = since < 0 ? 0 : Mathf.Exp(-since * 7f);
            foreach (var barrel in barrels) barrel.localScale = new Vector3(1 + kick * .18f, 1 - kick * .25f, 1 + kick * .18f);
        }
    }

    // Confetti from the cannons and from the sky, where everything is a function of the
    // time since the scene started (so a replay is the same show). Drawn instanced.
    public sealed class LastSceneConfetti
    {
        const int FromCannons = 240, FromSky = 150;
        static readonly int[] Colors = { 0xD8433C, 0xF6C445, 0xFFF6E6, 0x2F6FE0, 0x7A57D1, 0xFFFFFF, 0x3CC8F5 };

        struct Piece
        {
            public bool sky; public int group, color;
            public float jitter, vx, vy, vz, x0, y0, z0, ax, ay, az, delay, fall, flutter, phase, w1, w2, w3;
        }

        readonly Piece[] pieces;
        readonly Mesh mesh;
        readonly Material[] materials;
        readonly List<Matrix4x4>[] batches;

        public LastSceneConfetti(LastSceneCannons cannons)
        {
            mesh = LastSceneArt.Panel(.12f, .19f);
            materials = new Material[Colors.Length];
            batches = new List<Matrix4x4>[Colors.Length];
            for (int i = 0; i < Colors.Length; i++) { materials[i] = LastSceneArt.Instanced(Colors[i]); batches[i] = new List<Matrix4x4>(); }
            var random = new System.Random(7);
            float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
            pieces = new Piece[FromCannons + FromSky];
            for (int k = 0; k < pieces.Length; k++)
            {
                int c = k % 2;
                float speed = R(7f, 10.5f);
                Vector3 d = cannons.Directions[c], m = cannons.Muzzles[c];
                pieces[k] = new Piece
                {
                    sky = k >= FromCannons, group = (k >> 1) % 2, color = k % Colors.Length, jitter = R(0, .14f),
                    vx = d.x * speed + R(-1.4f, 1.4f), vy = d.y * speed + R(-.8f, 1.6f), vz = d.z * speed + R(-1.4f, 1.4f), x0 = m.x, y0 = m.y, z0 = m.z,
                    ax = R(-9, 9), az = R(-7, 7), ay = R(9, 12.5f), delay = R(0, 11), fall = R(.8f, 1.5f), flutter = R(1.6f, 3.2f), phase = R(0, 6.28f),
                    w1 = R(3, 9), w2 = R(2, 7), w3 = R(3, 8)
                };
            }
        }

        public void Draw(float tp)
        {
            foreach (var batch in batches) batch.Clear();
            for (int k = 0; k < pieces.Length; k++)
            {
                var p = pieces[k];
                float age, x, y, z;
                bool landed = false;
                if (p.sky)
                {
                    age = tp - LastSceneCannons.First - p.delay;
                    if (age < 0) continue;
                    age %= 12f;
                    x = p.ax + Mathf.Sin(age * p.flutter + p.phase) * .5f;
                    z = p.az + Mathf.Cos(age * p.flutter * .8f + p.phase) * .4f;
                    y = p.ay - p.fall * age;
                    float ground = LastSceneStage.GroundY(x, z) + .012f;
                    if (y <= ground) { y = ground; landed = true; }
                }
                else
                {
                    age = tp - (LastSceneCannons.First + p.group * LastSceneCannons.Period + p.jitter);
                    if (age < 0) continue;
                    age %= LastSceneCannons.Period * 2;
                    const float g = 9.8f;
                    float rise = Mathf.Max(.05f, p.vy / g);
                    if (age < rise) { x = p.x0 + p.vx * age; y = p.y0 + p.vy * age - .5f * g * age * age; z = p.z0 + p.vz * age; }
                    else
                    {
                        float top = p.y0 + p.vy * rise - .5f * g * rise * rise, f = age - rise;
                        Drift(p, rise, f, out x, out z);
                        y = top - p.fall * f;
                        float ground = LastSceneStage.GroundY(x, z) + .012f;
                        if (y <= ground)
                        {
                            float at = Mathf.Max(0, (top - ground) / p.fall);
                            Drift(p, rise, at, out x, out z);
                            y = LastSceneStage.GroundY(x, z) + .012f;
                            landed = true;
                        }
                    }
                }
                var rotation = landed ? Quaternion.Euler(90f, p.phase * Mathf.Rad2Deg, 0)
                                      : Quaternion.Euler((age * p.w1 + p.phase) * Mathf.Rad2Deg, age * p.w2 * Mathf.Rad2Deg, age * p.w3 * Mathf.Rad2Deg);
                batches[p.color].Add(Matrix4x4.TRS(new Vector3(x, y, z), rotation, Vector3.one));
            }
            for (int i = 0; i < batches.Length; i++)
                if (batches[i].Count > 0)
                    Graphics.DrawMeshInstanced(mesh, 0, materials[i], batches[i], null, ShadowCastingMode.Off, false);
        }

        static void Drift(Piece p, float rise, float f, out float x, out float z)
        {
            x = p.x0 + p.vx * rise + p.vx * .22f * f + Mathf.Sin(f * p.flutter + p.phase) * .4f;
            z = p.z0 + p.vz * rise + p.vz * .22f * f + Mathf.Cos(f * p.flutter * .8f + p.phase) * .3f;
        }
    }

    // A white ring that spreads and fades where something heavy lands.
    public sealed class LastSceneDust
    {
        readonly GameObject ring;
        readonly Material material;

        public LastSceneDust(Transform parent)
        {
            material = LastSceneArt.Blended("dust", null, new Color(1, 1, 1, 0), false);
            ring = LastSceneArt.Part(parent, "Dust", LastSceneArt.Ring(.35f, .55f), material, Vector3.zero, null, null, false);
            ring.SetActive(false);
        }

        // `life` 0..1 since the landing; anything else hides it.
        public void Show(Vector3 at, float life, float size = 1f)
        {
            if (!(life >= 0 && life <= 1)) { if (ring.activeSelf) ring.SetActive(false); return; }
            if (!ring.activeSelf) ring.SetActive(true);
            ring.transform.position = at + Vector3.up * .03f;
            float k = size * (1 + life * 2.6f);
            ring.transform.localScale = new Vector3(k, k, k);
            material.color = new Color(1, 1, 1, .6f * (1 - life));
        }
    }
}
