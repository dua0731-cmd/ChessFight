using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The prototype match's stage as the demo draws it (R149, Docs/GameModes/SwordFight/DEMO.md): a board of 64 separate
    /// squares (maple and walnut boxes 0.6 m deep, brass round the edge, a band on each home rank) floating over a study
    /// desk six metres below, held up on four stacks of books; four stone rook pedestals on b2, g2, b7, g7; low wooden rails
    /// on the outer side of files a and h, ranks 3–6; a chess clock, a green banker's lamp and two brass trays on the desk
    /// (the fallen pieces lie there); the demo's dark room, fog and warm light.
    /// The squares only draw: what stands on is still SwordFightEdgeFloor's platform (hidden). A square warned by a ghost
    /// glows and trembles, then drops, spinning, onto the desk and stays there; one over a rook's collapse trembles, falls
    /// and rises back with it. The pedestals and the rails are solid.
    /// </summary>
    public sealed class SwordFightArena : MonoBehaviour
    {
        public const float DeskY = -6f;

        sealed class Square
        {
            public int cell;
            public Transform t;
            public Material mat;
            public Color baseColor;
            public Vector3 home, rest;
            public float spin;
            public double warn = double.NaN, drop = double.NaN;
            public Collider rail;
        }

        readonly List<Square> squares = new List<Square>();
        readonly List<Object> owned = new List<Object>();
        Transform root, clockLeft, clockRight;
        float cell;
        SwordFightEdgeFloor floor;
        static readonly Color GlowColor = SwordFightShapes.Hex("#ffbf45");

        public Material Brass { get; private set; }
        public Material Stone { get; private set; }

        /// <summary>The tray a team's fallen pieces lie on (white left, black right), slot by slot.</summary>
        public Vector3 TrayPoint(int team, int slot) => new Vector3((team == 0 ? -12f : 12f) * cell / 2f, DeskY + 0.15f, (-3f + (slot % 6) * 1.6f) * cell / 2f);

        public void Build(float cellSize, SwordFightEdgeFloor edgeFloor)
        {
            Clear();
            cell = cellSize;
            floor = edgeFloor;
            float k = cell / 2f, half = SwordBoard.Size * cell * 0.5f;
            root = new GameObject("Sword Fight arena (demo)").transform;   // at the world's origin, the board's centre
            var maple = Own(SwordFightShapes.Lit("#ad9c80", 0.62f));
            var walnut = Own(SwordFightShapes.Lit("#5a3d2a", 0.5f));
            Brass = Own(SwordFightShapes.Lit("#c9a04c", 0.35f, 0.8f));
            Stone = Own(SwordFightShapes.Lit("#8d8478", 0.85f));
            var railMat = Own(SwordFightShapes.Lit("#7a5233", 0.5f));
            var bandDark = Own(SwordFightShapes.Glow("#2b2420", 0.7f));
            var bandLight = Own(SwordFightShapes.Glow("#f3ecdd", 0.7f));

            // ---- the squares
            for (int c = 0; c < SwordBoard.Count; c++)
            {
                int i = SwordBoard.X(c), j = SwordBoard.Y(c);
                SwordBoard.Centre(c, cell, out double cx, out double cz);
                bool light = (i + j) % 2 == 0;   // the demo's colouring
                var mat = Own(new Material(light ? maple : walnut));
                var sq = new Square { cell = c, mat = mat, baseColor = mat.color, home = new Vector3((float)cx, -0.3f, (float)cz) };
                var go = new GameObject($"Square {(char)('a' + i)}{j + 1}");
                go.transform.SetParent(root, false);
                go.transform.localPosition = sq.home;
                sq.t = go.transform;
                SwordFightShapes.Part(PrimitiveType.Cube, mat, sq.t, Vector3.zero, new Vector3(cell - 0.04f, 0.6f, cell - 0.04f));
                // Brass round the board's edge.
                if (i == 0) SwordFightShapes.Part(PrimitiveType.Cube, Brass, sq.t, new Vector3(-k, 0f, 0f), new Vector3(0.06f, 0.62f, cell));
                if (i == SwordBoard.Size - 1) SwordFightShapes.Part(PrimitiveType.Cube, Brass, sq.t, new Vector3(k, 0f, 0f), new Vector3(0.06f, 0.62f, cell));
                if (j == 0) SwordFightShapes.Part(PrimitiveType.Cube, Brass, sq.t, new Vector3(0f, 0f, -k), new Vector3(cell, 0.62f, 0.06f));
                if (j == SwordBoard.Size - 1) SwordFightShapes.Part(PrimitiveType.Cube, Brass, sq.t, new Vector3(0f, 0f, k), new Vector3(cell, 0.62f, 0.06f));
                // A band on each home rank (dark on white's, light on black's, toward the middle).
                if (j == 0 || j == SwordBoard.Size - 1)
                    SwordFightShapes.Part(PrimitiveType.Quad, j == 0 ? bandDark : bandLight, sq.t, new Vector3(0f, 0.305f, j == 0 ? 0.75f * k : -0.75f * k),
                        new Vector3(1.8f * k, 0.12f, 1f), Quaternion.Euler(90f, 0f, 0f));
                // The low rails: a picture on the square, a collider of its own (it goes when the square drops).
                if (SwordBoard.Rail(c))
                {
                    float sx = i == 0 ? -1f : 1f;
                    SwordFightShapes.Part(PrimitiveType.Cube, railMat, sq.t, new Vector3(sx * 0.9f * k, 0.51f, 0f), new Vector3(0.14f, 0.42f, 1.9f * k));
                    var rail = new GameObject("Rail collider");
                    rail.transform.SetParent(root, false);
                    rail.transform.localPosition = new Vector3((float)cx + sx * 0.9f * k, 0.21f, (float)cz);
                    var box = rail.AddComponent<BoxCollider>();
                    box.size = new Vector3(0.14f, 0.42f, 1.9f * k);
                    sq.rail = box;
                }
                int seed = (i * 7 + j * 3) % 5 - 2;
                sq.spin = seed * 0.35f;
                sq.rest = new Vector3((float)cx * 1.15f + seed * 0.5f, DeskY + 0.3f, (float)cz * 1.15f + ((i + 2 * j) % 3 - 1) * 0.6f);
                squares.Add(sq);
            }
            // ---- the rook pedestals
            foreach (int c in SwordBoard.Obstacles)
            {
                SwordBoard.Centre(c, cell, out double cx, out double cz);
                SwordFightShapes.Pedestal(Stone, root, new Vector3((float)cx, 0f, (float)cz));
            }

            // ---- the study under it
            var desk = Own(SwordFightShapes.Lit("#4a2f1f", 0.55f));
            var deskEdge = Own(SwordFightShapes.Lit("#3a2417", 0.6f));
            SwordFightShapes.Part(PrimitiveType.Cube, desk, root, new Vector3(0f, DeskY - 0.5f, 0f), new Vector3(80f, 1f, 50f));
            for (float z = -24f; z <= 24f; z += 1.6f) SwordFightShapes.Part(PrimitiveType.Cube, deskEdge, root, new Vector3(0f, DeskY + 0.005f, z), new Vector3(80f, 0.02f, 0.05f));
            string[] bookCols = { "#7a2b2b", "#2f4f3a", "#2c3b5e", "#9a7a3a", "#5b3a5e", "#3d3d3d" };
            var books = new Material[bookCols.Length];
            for (int b = 0; b < books.Length; b++) books[b] = Own(SwordFightShapes.Lit(bookCols[b], 0.75f));
            float[][] stacks = { new[] { -6.2f, -6.2f }, new[] { 6.2f, -6.2f }, new[] { -6.2f, 6.2f }, new[] { 6.2f, 6.2f } };
            for (int s = 0; s < stacks.Length; s++)
            {
                float y = DeskY;
                int n = 0;
                while (y < -0.75f)
                {
                    float hh = Mathf.Min(0.55f + ((s * 7 + n * 3) % 4) * 0.12f, -0.65f - y);
                    SwordFightShapes.Part(PrimitiveType.Cube, books[(s + n) % 6], root, new Vector3(stacks[s][0] * k, y + hh / 2f, stacks[s][1] * k),
                        new Vector3(3.2f - (n % 2) * 0.3f, hh, 2.4f + (n % 3) * 0.2f), Quaternion.Euler(0f, ((s + n) % 5 - 2) * 0.06f * Mathf.Rad2Deg, 0f));
                    y += hh;
                    n++;
                }
            }
            // The chess clock (its hands go round with the match clock).
            var clockG = new GameObject("Chess clock").transform;
            clockG.SetParent(root, false);
            clockG.localPosition = new Vector3(11f * k, DeskY, -11f * k);
            clockG.localRotation = Quaternion.Euler(0f, -0.5f * Mathf.Rad2Deg, 0f);
            SwordFightShapes.Part(PrimitiveType.Cube, Own(SwordFightShapes.Lit("#2a1c13", 0.5f)), clockG, new Vector3(0f, 1.1f, 0f), new Vector3(4.2f, 2.2f, 1.6f));
            var faceMat = Own(SwordFightShapes.Lit("#efe6d2", 0.7f));
            var handMat = Own(SwordFightShapes.Lit("#1b1410", 0.6f));
            for (int s = 0; s < 2; s++)
            {
                float sx = s == 0 ? -1f : 1f;
                SwordFightShapes.Part(PrimitiveType.Cylinder, faceMat, clockG, new Vector3(sx, 1.15f, 0.81f), new Vector3(1.5f, 0.01f, 1.5f), Quaternion.Euler(90f, 0f, 0f));
                var pivot = new GameObject("hand").transform;
                pivot.SetParent(clockG, false);
                pivot.localPosition = new Vector3(sx, 1.15f, 0.83f);
                SwordFightShapes.Part(PrimitiveType.Cube, handMat, pivot, new Vector3(0f, 0.3f, 0f), new Vector3(0.05f, 0.6f, 0.02f));
                SwordFightShapes.Part(PrimitiveType.Cube, Brass, clockG, new Vector3(sx, 2.33f, 0f), new Vector3(0.5f, 0.25f, 0.5f));
                if (s == 0) clockLeft = pivot; else clockRight = pivot;
            }
            // The banker's lamp and its warm spot light.
            SwordFightShapes.Part(PrimitiveType.Cylinder, Brass, root, new Vector3(-12.5f * k, DeskY + 10.5f, 10f * k), new Vector3(0.24f, 10.5f, 0.24f));
            var shade = SwordFightShapes.MeshObject("Lamp shade", SwordFightShapes.Lathe(new[] { new Vector2(1.6f, 0f), new Vector2(0.05f, 1.4f) }, 24), Own(SwordFightShapes.Lit("#1f4a35", 0.5f)), root);
            shade.transform.localPosition = new Vector3(-7f * k, 15.2f - 0.7f, 8f * k);
            SwordFightShapes.Part(PrimitiveType.Sphere, Own(SwordFightShapes.Glow("#fff0c8", 1f)), root, new Vector3(-7f * k, 14.7f, 8f * k), Vector3.one * 0.7f);
            SwordFightShapes.Part(PrimitiveType.Cylinder, Brass, root, new Vector3(-9.6f * k, 15.6f, 9f * k), new Vector3(0.2f, 3.6f, 0.2f), Quaternion.Euler(0f, 0f, (Mathf.PI / 2f - 0.4f) * Mathf.Rad2Deg));
            var spot = new GameObject("Lamp light").AddComponent<Light>();
            spot.transform.SetParent(root, false);
            spot.transform.localPosition = new Vector3(-7f * k, 15f, 8f * k);
            spot.transform.LookAt(Vector3.zero);
            spot.type = LightType.Spot;
            spot.color = SwordFightShapes.Hex("#ffcf8a");
            spot.range = 46f;
            spot.intensity = ChessFight.Game.MenuArt.SpotIntensity(spot, 1.6f);   // URP: the spot fades with the distance squared
            spot.spotAngle = 0.8f * 2f * Mathf.Rad2Deg;
            spot.innerSpotAngle = spot.spotAngle * 0.4f;
            // The trays.
            SwordFightShapes.Part(PrimitiveType.Cube, Brass, root, new Vector3(-12f * k, DeskY + 0.075f, 0.5f * k), new Vector3(2.2f, 0.15f, 10f * k));
            SwordFightShapes.Part(PrimitiveType.Cube, Brass, root, new Vector3(12f * k, DeskY + 0.075f, 0.5f * k), new Vector3(2.2f, 0.15f, 10f * k));

            // ---- the room: dark, a little fog, the warm key light, the cool sky and the brown floor's bounce
            var sun = FindFirstObjectByType<Light>();
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
            if (sun != null && sun.type == LightType.Directional)
            {
                sun.color = SwordFightShapes.Hex("#fff1dc");
                sun.intensity = 0.95f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.LookRotation(new Vector3(9f, -20f, 8f));
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = SwordFightShapes.Hex("#b9c4e8") * 0.55f;
            RenderSettings.ambientEquatorColor = SwordFightShapes.Hex("#6b5648") * 0.45f;
            RenderSettings.ambientGroundColor = SwordFightShapes.Hex("#3a2618") * 0.45f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = SwordFightShapes.Hex("#140f0c");
            RenderSettings.fogStartDistance = 34f;
            RenderSettings.fogEndDistance = 85f;
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = SwordFightShapes.Hex("#140f0c");
            }
        }

        T Own<T>(T thing) where T : Object { owned.Add(thing); return thing; }

        void Clear()
        {
            if (root != null) Destroy(root.gameObject);
            foreach (var o in owned) if (o != null) Destroy(o);
            owned.Clear();
            squares.Clear();
        }

        void OnDestroy() => Clear();

        /// <summary>A ghost's warning and drop for a square (the board's plan); NaN for none.</summary>
        public void Plan(int cell, double warn, double drop)
        {
            foreach (var s in squares) if (s.cell == cell) { s.warn = warn; s.drop = drop; }
        }

        public void ResetPlan()
        {
            foreach (var s in squares)
            {
                s.warn = s.drop = double.NaN;
                s.t.gameObject.SetActive(true);
                if (s.rail != null) s.rail.enabled = true;
            }
        }

        /// <summary>The squares at <paramref name="clock"/> (the match clock): glowing, trembling, falling, lying on the desk;
        /// over a rook's collapse trembling, falling and rising with it. The clock's hands.</summary>
        public void Step(double clock)
        {
            const float g = 9.81f;
            float fallTime = Mathf.Sqrt(2f * (-0.3f - (DeskY + 0.3f)) / g);
            foreach (var s in squares)
            {
                Vector3 p = s.home;
                Quaternion turn = Quaternion.identity;
                float glow = 0f, jit = 0f;
                bool shown = true;
                if (!double.IsNaN(s.drop))
                {
                    if (clock >= s.warn && clock < s.drop)
                    {
                        float age = (float)(clock - s.warn), u = age / (float)(s.drop - s.warn);
                        glow = (0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(age * (5f + u * 18f) * Mathf.PI))) * (0.6f + u * 0.8f);
                        if (u > 0.6f) jit = (u - 0.6f) * 0.12f * Mathf.Sin(Time.time * 90f + s.cell * 3f);
                    }
                    else if (clock >= s.drop)
                    {
                        float f = (float)(clock - s.drop);
                        if (f < fallTime)
                        {
                            p = new Vector3(Mathf.Lerp(s.home.x, s.rest.x, f / fallTime), -0.3f - 0.5f * g * f * f, Mathf.Lerp(s.home.z, s.rest.z, f / fallTime));
                            turn = Quaternion.Euler(f * s.spin * Mathf.Rad2Deg, 0f, f * s.spin * 0.7f * Mathf.Rad2Deg);
                        }
                        else
                        {
                            p = s.rest;
                            turn = Quaternion.Euler(0f, 0f, s.spin * 0.3f * Mathf.Rad2Deg);
                        }
                        glow = Mathf.Max(0f, 0.8f - f * 1.5f);
                        if (s.rail != null && s.rail.enabled) s.rail.enabled = false;
                    }
                }
                // Over a rook's collapse (the floor's own phases).
                if (floor != null && (double.IsNaN(s.drop) || clock < s.drop))
                    foreach (var c in floor.Collapses)
                    {
                        if (!c.rect.Contains(new Vector2(s.home.x, s.home.z))) continue;
                        if (c.phase == SwordFightEdgeFloor.Phase.Crack) jit = 0.04f * Mathf.Sin(Time.time * 70f + s.cell);
                        else if (c.phase == SwordFightEdgeFloor.Phase.Open)
                        {
                            float f = c.age - c.crack;
                            p.y = -0.3f - 0.5f * g * f * f;
                            turn = Quaternion.Euler(f * 0.6f * Mathf.Rad2Deg, 0f, 0f);
                            if (p.y < DeskY) shown = false;
                        }
                        else if (c.phase == SwordFightEdgeFloor.Phase.Rise) p.y = Mathf.Lerp(-4f, -0.3f, Ease(c.Rise));
                    }
                if (s.t.gameObject.activeSelf != shown) s.t.gameObject.SetActive(shown);
                s.t.localPosition = new Vector3(p.x + jit, p.y, p.z - jit);
                s.t.localRotation = turn;
                s.mat.color = Color.Lerp(s.baseColor, GlowColor, Mathf.Clamp01(glow * 0.6f));
            }
            if (clockLeft != null) clockLeft.localRotation = Quaternion.Euler(0f, 0f, -(float)(clock / 60.0 * 360.0));
            if (clockRight != null) clockRight.localRotation = Quaternion.Euler(0f, 0f, -(float)(clock / 30.0 * 180.0));
        }

        static float Ease(float u) => u < 0.5f ? 2f * u * u : 1f - Mathf.Pow(-2f * u + 2f, 2f) / 2f;
    }
}
