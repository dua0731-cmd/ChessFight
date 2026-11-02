using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The map test scene's built-in yard (11-02): the pawn measured against known sizes before a map is drawn - step
    // heights it can jump onto, gaps it can jump across, slopes it can run up, walls it can climb, a sweeper bar to
    // jump, a moving platform to ride and beams over a pit with a KillVolume and a checkpoint. Each one carries its
    // number on a label. Built from code (CourseBuilder, the course kit's materials) so it needs no prefab. North (+z)
    // is the way to go; the start is (0, 0, 0).
    //
    //   x -22..-14  steps  0.30 / 0.45 / 0.60 / 0.75 / 0.90 / 1.20 m
    //   x -11..-5   gaps   1.0 / 1.5 / 2.0 / 2.5 / 3.0 / 4.0 m between 1.5 m high platforms
    //   x -2..2     open run (sprint, dive)
    //   x 3..15.5   slopes 10 / 20 / 30 degrees, 8 m long
    //   x 18..22    walls  1 / 2 / 3 / 5 m (stone, climbable), 3 m marble (cannot be climbed)
    //   z 66..96    sweeper bar (top 0.6 m, 60 deg/s), moving platform (+-5 m, 6 s)
    //   z 96..106   beams 2 / 1 / 0.5 m wide over a pit, checkpoint at z 92
    public static class PawnRushCalibrationYard
    {
        static readonly float[] Steps = { .3f, .45f, .6f, .75f, .9f, 1.2f };
        static readonly float[] Gaps = { 1f, 1.5f, 2f, 2.5f, 3f, 4f };
        static readonly float[] SlopeDegrees = { 10f, 20f, 30f };
        static readonly float[] WallHeights = { 1f, 2f, 3f, 5f };

        public static void Build(CourseBuilder b)
        {
            var start = new GameObject("Start").transform;
            start.SetParent(b.parent, false);
            start.gameObject.AddComponent<SpawnPoint>().Configure(0, Teams.White);

            b.Floor("Yard floor", -24f, 24f, -6f, 64f, 0f);
            b.Floor("Obstacle floor", -12f, 12f, 64f, 96f, 0f);

            b.In("Steps", () =>
            {
                for (int i = 0; i < Steps.Length; i++)
                {
                    float z = 6f + i * 6f;
                    b.Wall("Step " + Steps[i].ToString("0.00"), -21f, -15f, 0f, Steps[i], z, z + 3f, true, true);
                    Label(b, Steps[i].ToString("0.00") + " m", new Vector3(-18f, Steps[i] + .9f, z));
                }
                Label(b, "STEP", new Vector3(-18f, 2.2f, 2f), 1.6f);
            });

            b.In("Gaps", () =>
            {
                const float top = 1.5f;
                b.Ramp("Gap ramp", -11f, -5f, 2f, 10f, 0f, top, 1f);
                b.Wall("Gap platform 0", -11f, -5f, 0f, top, 10f, 14f, true, true);
                float z = 14f;
                foreach (var gap in Gaps)
                {
                    Label(b, gap.ToString("0.0") + " m", new Vector3(-8f, top + .9f, z + gap * .5f));
                    z += gap;
                    b.Wall("Gap platform after " + gap.ToString("0.0"), -11f, -5f, 0f, top, z, z + 3f, true, true);
                    z += 3f;
                }
                Label(b, "GAP", new Vector3(-8f, 3.2f, 2f), 1.6f);
            });

            b.In("Slopes", () =>
            {
                for (int i = 0; i < SlopeDegrees.Length; i++)
                {
                    float x0 = 3f + i * 4.5f, x1 = x0 + 3.5f;
                    float rise = 8f * Mathf.Tan(SlopeDegrees[i] * Mathf.Deg2Rad);
                    b.Ramp("Slope " + SlopeDegrees[i] + " deg", x0, x1, 8f, 16f, 0f, rise, 1f);
                    b.Wall("Slope top " + SlopeDegrees[i] + " deg", x0, x1, 0f, rise, 16f, 19f, true, true);
                    Label(b, SlopeDegrees[i] + "°", new Vector3((x0 + x1) * .5f, .9f, 7f));
                }
                Label(b, "SLOPE", new Vector3(9.25f, 2.2f, 4f), 1.6f);
            });

            b.In("Walls", () =>
            {
                for (int i = 0; i < WallHeights.Length; i++)
                {
                    float z = 8f + i * 8f;
                    b.Wall("Climb wall " + WallHeights[i] + " m", 18f, 22f, 0f, WallHeights[i], z, z + 2f, true, true);
                    Label(b, WallHeights[i] + " m", new Vector3(20f, WallHeights[i] + .9f, z));
                }
                b.Wall("No-climb wall 3 m", 18f, 22f, 0f, 3f, 42f, 44f, false, true);
                Label(b, "3 m NO CLIMB", new Vector3(20f, 3.9f, 42f));
                Label(b, "CLIMB", new Vector3(20f, 2.2f, 4f), 1.6f);
            });

            b.In("Sweeper", () =>
            {
                var pivot = Mover(b, "Sweeper bar (Spinner 60 deg/s)", new Vector3(0f, 0f, 74f));
                pivot.gameObject.AddComponent<Spinner>().Configure(Vector3.up, 60f);
                var under = b.parent;
                b.parent = pivot;
                b.Box("Bar", new Vector3(-6f, .25f, -.2f), new Vector3(6f, .6f, .2f), b.kit.hazard, true);
                b.Cylinder("Hub", Vector3.zero, .8f, 1f, b.kit.wallNoClimb, true, true);
                b.parent = under;
                Label(b, "BAR 0.6 m", new Vector3(0f, 2f, 67f));
            });

            b.In("Moving platform", () =>
            {
                var pivot = Mover(b, "Platform (Oscillator +-5 m, 6 s)", new Vector3(0f, 0f, 86f));
                pivot.gameObject.AddComponent<Oscillator>().Configure(new Vector3(5f, 0f, 0f), 6f);
                var under = b.parent;
                b.parent = pivot;
                b.Box("Deck", new Vector3(-2f, 0f, -2f), new Vector3(2f, .6f, 2f), b.kit.boardLight, false).AddComponent<CourseFloor>();
                b.parent = under;
                Label(b, "RIDE", new Vector3(0f, 2f, 81f));
            });

            b.In("Pit", () =>
            {
                b.Checkpoint(1, new Vector3(0f, 0f, 92f), 0f, 24f);
                b.Floor("Pit bottom", -12f, 12f, 96f, 106f, -8f);
                b.Trigger<KillVolume>("Pit (fall)", new Vector3(-12f, -7.9f, 96f), new Vector3(12f, -3f, 106f));
                float[] widths = { 2f, 1f, .5f };
                float[] centres = { -7f, 0f, 7f };
                for (int i = 0; i < widths.Length; i++)
                {
                    float h = widths[i] * .5f;
                    b.Wall("Beam " + widths[i] + " m", centres[i] - h, centres[i] + h, -.5f, 0f, 96f, 106f, false, true);
                    Label(b, widths[i] + " m", new Vector3(centres[i], .9f, 95f));
                }
                b.Floor("Far floor", -12f, 12f, 106f, 114f, 0f);
                Label(b, "END", new Vector3(0f, 2f, 112f), 1.6f);
            });
        }

        // An empty moving root at `at`: the Obstacle on it adds the kinematic Rigidbody, and its pose is read from
        // ObstacleClock when the yard is switched on (it is built inactive).
        static Transform Mover(CourseBuilder b, string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(b.parent, false);
            go.transform.localPosition = at;
            return go.transform;
        }

        static Font font;

        // A number standing over the thing it measures, readable from the start (looking north).
        static void Label(CourseBuilder b, string text, Vector3 at, float size = 1f)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Label " + text);
            go.transform.SetParent(b.parent, false);
            go.transform.localPosition = at;
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = .06f * size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(.12f, .14f, .2f);
            if (font != null) go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }
}
