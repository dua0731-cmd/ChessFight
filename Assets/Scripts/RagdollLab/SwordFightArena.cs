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
    /// R191 (10-28): the squares, the edge, the rails, the pedestals' look, the desk and the trays are the Blender kit
    /// (ArtSource/SwordFight, loaded from Resources/SwordFightMap): light oak and walnut squares with rounded edges, a
    /// rounded walnut frame with a bronze line instead of the brass strip, rails with bronze caps. Any model that does not
    /// load (or UseModels = false) falls back to the demo's box. The book stacks, the clock and the lamp stay the demo's.
    /// </summary>
    public sealed class SwordFightArena : MonoBehaviour
    {
        public const float DeskY = -6f;

        /// <summary>R191: build from the Blender kit; false = the demo's boxes as before.</summary>
        public static bool UseModels = true;
        const string Kit = "SwordFightMap/";

        sealed class Square
        {
            public int cell;
            public Transform t;
            public Material mat;
            public Color baseColor;
            /// <summary>The square's wood as a colour (its texture's average with the kit): the skill effects' chips.</summary>
            public Color wood;
            public Vector3 home, rest;
            public float spin;
            public double warn = double.NaN, drop = double.NaN;
            public Collider rail;
            /// <summary>R191: it has landed on the desk (the lamp swayed for it).</summary>
            public bool landed;
            /// <summary>R191: the dust a trembling square still owes (fractions carried between frames).</summary>
            public float puffs, grit;
            /// <summary>10-25: its brass (edge squares only; R191 the frame and its corner): hidden while a rook's collapse
            /// throws it (the skill effects draw it buckling and falling on its own, SwordFightSkillFx.RealEdge.cs).</summary>
            public readonly List<Trim> brass = new List<Trim>();
        }

        /// <summary>One length of the edge on a square: what draws it, and (in the square's space) its middle at the
        /// square's middle height, its long way and its length (0: a corner piece, hidden but not thrown).</summary>
        sealed class Trim
        {
            public Renderer[] parts;
            public Vector3 at, along;
            public float length;
        }

        /// <summary>10-25: the brass along the board's edge on the squares inside <paramref name="rect"/> (x / z), as
        /// (middle, its long way, its length); <paramref name="hide"/> hides those pieces while they are away.</summary>
        public List<(Vector3 at, Vector3 along, float length)> BrassIn(Rect rect, bool hide)
        {
            var list = new List<(Vector3, Vector3, float)>();
            foreach (var s in squares)
            {
                if (!rect.Contains(new Vector2(s.home.x, s.home.z))) continue;
                foreach (var b in s.brass)
                {
                    if (b.length > 0f) list.Add((s.t.TransformPoint(b.at), s.t.TransformDirection(b.along), b.length));
                    foreach (var r in b.parts) if (r != null) r.forceRenderingOff = hide;
                }
            }
            return list;
        }

        readonly List<Square> squares = new List<Square>();
        readonly List<Object> owned = new List<Object>();
        Transform root, clockLeft, clockRight;
        float cell;
        SwordFightEdgeFloor floor;
        static readonly Color GlowColor = SwordFightShapes.Hex("#ffbf45");

        /// <summary>The metal the board's edge is trimmed with: the demo's brass, or (R191 kit) the frame's dark bronze line.</summary>
        public Material Brass { get; private set; }
        public Material Stone { get; private set; }
        /// <summary>Half the board's side (m): the brass runs round it at ±this, its top at the squares' top (y 0).</summary>
        public float HalfSize => SwordBoard.Size * cell * 0.5f;
        /// <summary>How far the edge's face stands out of the board's side and its lip over the board's top (m): the demo's
        /// brass 3 cm / 1 cm, the kit's walnut frame 15 cm / 4 cm (the king's breach draws on that face).</summary>
        public float EdgeOut { get; private set; } = 0.03f;
        public float EdgeLip { get; private set; } = 0.01f;
        /// <summary>R191: the edge is the kit's walnut frame (the rook's collapse throws a copy of it, not a bent brass bar).</summary>
        public bool FrameEdge { get; private set; }
        /// <summary>The frame's wood as a colour (its splinters).</summary>
        public Color FrameWood { get; private set; }
        /// <summary>The frame's middle from its pivot (on the edge line, at its bottom): out and up, m.</summary>
        public static readonly Vector3 FrameMiddle = new Vector3(0.065f, 0.33f, 0f);

        /// <summary>A loose copy of one length of the frame under <paramref name="parent"/>, for the rook's collapse to
        /// throw: its origin at the frame's middle, x out of the board, y up, z along it, <paramref name="length"/> long.
        /// Null when the edge is not the kit's frame.</summary>
        public Transform LooseFrame(Transform parent, float length)
        {
            if (!FrameEdge) return null;
            var pivot = new GameObject("Loose frame").transform;
            pivot.SetParent(parent, false);
            if (Model("SF_Frame_2m", pivot, -FrameMiddle, 0f, new Vector3(1f, 1f, length / 2f), frameMat, Brass) != null) return pivot;
            Destroy(pivot.gameObject);
            return null;
        }

        /// <summary>The tray a team's fallen pieces lie on (white left, black right), slot by slot.</summary>
        public Vector3 TrayPoint(int team, int slot) => new Vector3((team == 0 ? -12f : 12f) * cell / 2f, DeskY + 0.15f, (-3f + (slot % 6) * 1.6f) * cell / 2f);

        public void Build(float cellSize, SwordFightEdgeFloor edgeFloor)
        {
            Clear();
            cell = cellSize;
            floor = edgeFloor;
            float k = cell / 2f, half = SwordBoard.Size * cell * 0.5f;
            root = new GameObject("Sword Fight arena (demo)").transform;   // at the world's origin, the board's centre
            var brass = Own(SwordFightShapes.Lit("#c9a04c", 0.35f, 0.8f));   // the clock's buttons, the lamp
            Stone = Own(SwordFightShapes.Lit("#8d8478", 0.85f));
            var bandDark = Own(SwordFightShapes.Glow("#2b2420", 0.7f));
            var bandLight = Own(SwordFightShapes.Glow("#f3ecdd", 0.7f));
            // The kit's woods (R191): light oak and the user's walnut textures; the demo's colours when they are missing.
            bool kit = kitOn = UseModels && Resources.Load<GameObject>(Kit + "SF_Square_Light") != null;
            Material maple, walnut, railMat, deskMat;
            FrameEdge = false;
            Color mapleWood, walnutWood;
            if (kit)
            {
                maple = Wood("T_SF_Oak", "#b09a7c", 1f, 0.55f);
                walnut = Wood("T_SF_Walnut", "#4e4139", 1f, 0.5f);
                railMat = Wood("T_SF_Walnut", "#5e4a3e", 1f, 0.5f);
                frameMat = Wood("T_SF_Walnut", "#4e4139", 0.85f, 0.4f);
                deskMat = Wood("T_SF_Walnut", "#3a2a20", 0.75f, 0.55f);
                Brass = Own(SwordFightShapes.Lit("#5a4a3a", 0.45f, 0.8f));
                mapleWood = SwordFightShapes.Hex("#b09a7c");
                walnutWood = SwordFightShapes.Hex("#4e4139");
                FrameWood = walnutWood * 0.85f;
                EdgeOut = 0.15f;
                EdgeLip = 0.04f;
            }
            else
            {
                maple = Own(SwordFightShapes.Lit("#ad9c80", 0.62f));
                walnut = Own(SwordFightShapes.Lit("#5a3d2a", 0.5f));
                railMat = Own(SwordFightShapes.Lit("#7a5233", 0.5f));
                frameMat = deskMat = null;
                Brass = brass;
                mapleWood = maple.color;
                walnutWood = walnut.color;
                EdgeOut = 0.03f;
                EdgeLip = 0.01f;
            }
            float frameY = -0.32f;   // the frame's bottom at -0.62, like the demo's brass, in the square's space (its middle at -0.3)

            // ---- the squares
            for (int c = 0; c < SwordBoard.Count; c++)
            {
                int i = SwordBoard.X(c), j = SwordBoard.Y(c);
                SwordBoard.Centre(c, cell, out double cx, out double cz);
                bool light = (i + j) % 2 == 0;   // the demo's colouring
                var mat = Own(new Material(light ? maple : walnut));
                var sq = new Square { cell = c, mat = mat, baseColor = mat.color, wood = light ? mapleWood : walnutWood, home = new Vector3((float)cx, -0.3f, (float)cz) };
                var go = new GameObject($"Square {(char)('a' + i)}{j + 1}");
                go.transform.SetParent(root, false);
                go.transform.localPosition = sq.home;
                sq.t = go.transform;
                // Every other square of a colour a quarter turn round, so the planks do not all run one way.
                float fit = (cell - 0.04f) / 1.96f;
                if (Model(light ? "SF_Square_Light" : "SF_Square_Dark", sq.t, new Vector3(0f, -0.3f, 0f), (i / 2 + j) % 2 == 1 ? 90f : 0f,
                        new Vector3(fit, 1f, fit), mat) == null)
                    SwordFightShapes.Part(PrimitiveType.Cube, mat, sq.t, Vector3.zero, new Vector3(cell - 0.04f, 0.6f, cell - 0.04f));
                // The edge: the kit's frame (its +x out of the board), or the demo's brass.
                if (i == 0) Edge(sq, new Vector3(-k, 0f, 0f), Vector3.forward, 180f, frameY, frameMat);
                if (i == SwordBoard.Size - 1) Edge(sq, new Vector3(k, 0f, 0f), Vector3.forward, 0f, frameY, frameMat);
                if (j == 0) Edge(sq, new Vector3(0f, 0f, -k), Vector3.right, 90f, frameY, frameMat);
                if (j == SwordBoard.Size - 1) Edge(sq, new Vector3(0f, 0f, k), Vector3.right, -90f, frameY, frameMat);
                if (kit && (i == 0 || i == SwordBoard.Size - 1) && (j == 0 || j == SwordBoard.Size - 1))
                {
                    // The frame's rounded corner (its quarter toward +x / +z), on the corner square.
                    float yaw = i > 0 ? (j > 0 ? 0f : 90f) : (j > 0 ? -90f : 180f);
                    var corner = Model("SF_Frame_Corner", sq.t, new Vector3(i > 0 ? k : -k, frameY, j > 0 ? k : -k), yaw, Vector3.one, frameMat, Brass);
                    if (corner != null) sq.brass.Add(new Trim { parts = corner.GetComponentsInChildren<Renderer>(), at = Vector3.zero, along = Vector3.forward, length = 0f });
                }
                // A band on each home rank (dark on white's, light on black's, toward the middle).
                if (j == 0 || j == SwordBoard.Size - 1)
                    SwordFightShapes.Part(PrimitiveType.Quad, j == 0 ? bandDark : bandLight, sq.t, new Vector3(0f, 0.305f, j == 0 ? 0.75f * k : -0.75f * k),
                        new Vector3(1.8f * k, 0.12f, 1f), Quaternion.Euler(90f, 0f, 0f));
                // The low rails: a picture on the square, a collider of its own (it goes when the square drops).
                if (SwordBoard.Rail(c))
                {
                    float sx = i == 0 ? -1f : 1f;
                    if (Model("SF_Rail_1p9", sq.t, new Vector3(sx * 0.9f * k, 0.3f, 0f), 0f, new Vector3(1f, 1f, k), railMat, Brass) == null)
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
                var at = new Vector3((float)cx, 0f, (float)cz);
                var pedestal = SwordFightShapes.Pedestal(Stone, root, at);
                // R191 (10-28): the Tripo chess rook, 0.9 m, in the board's own wood (light oak on white's side, walnut
                // on black's; user: "0.9m 으로 맞추고 재질과 색상도 체스판과 맞춰"), with colliders of its own shape;
                // else the kit's pedestal drawing over the demo's (the same profile, kept as the collider).
                var statue = Model("SF_Prop_Rook", root, at, 0f, Vector3.one, SwordBoard.Y(c) < SwordBoard.Size / 2 ? maple : walnut);
                if (statue != null)
                {
                    Destroy(pedestal);
                    RookCollider(statue, at);
                }
                else if (Model("SF_RookPedestal", root, at, 0f, Vector3.one, Stone) != null)
                    foreach (var r in pedestal.GetComponentsInChildren<Renderer>()) r.enabled = false;
            }

            // ---- the study under it
            // Settled, the desk's long side (80 m) lies along z: a quarter turn puts it along x as the demo has it.
            if (Model("SF_Desk", root, new Vector3(0f, DeskY - 1f, 0f), 90f, Vector3.one, deskMat) == null)
            {
                var desk = Own(SwordFightShapes.Lit("#4a2f1f", 0.55f));
                var deskEdge = Own(SwordFightShapes.Lit("#3a2417", 0.6f));
                SwordFightShapes.Part(PrimitiveType.Cube, desk, root, new Vector3(0f, DeskY - 0.5f, 0f), new Vector3(80f, 1f, 50f));
                for (float z = -24f; z <= 24f; z += 1.6f) SwordFightShapes.Part(PrimitiveType.Cube, deskEdge, root, new Vector3(0f, DeskY + 0.005f, z), new Vector3(80f, 0.02f, 0.05f));
            }
            float[][] stacks = { new[] { -6.2f, -6.2f }, new[] { 6.2f, -6.2f }, new[] { -6.2f, 6.2f }, new[] { 6.2f, 6.2f } };
            for (int s = 0; s < stacks.Length; s++)
            {
                // R191: the kit's stacks (Tripo books, 5.35 m, their top just under the squares), else the demo's boxes.
                var at = new Vector3(stacks[s][0] * k, DeskY, stacks[s][1] * k);
                if (Model("SF_Prop_BookStack_" + "ABCD"[s], root, at, (s % 5 - 2) * 3.4f, Vector3.one) != null) continue;
                float y = DeskY;
                int n = 0;
                while (y < -0.75f)
                {
                    float hh = Mathf.Min(0.55f + ((s * 7 + n * 3) % 4) * 0.12f, -0.65f - y);
                    SwordFightShapes.Part(PrimitiveType.Cube, Own(SwordFightShapes.Lit(BookColours[(s + n) % 6], 0.75f)), root, new Vector3(stacks[s][0] * k, y + hh / 2f, stacks[s][1] * k),
                        new Vector3(3.2f - (n % 2) * 0.3f, hh, 2.4f + (n % 3) * 0.2f), Quaternion.Euler(0f, ((s + n) % 5 - 2) * 0.06f * Mathf.Rad2Deg, 0f));
                    y += hh;
                    n++;
                }
            }
            // The chess clock (its hands go round with the match clock): the kit's (front +z, hands on its dials), or the demo's.
            var clockG = new GameObject("Chess clock").transform;
            clockG.SetParent(root, false);
            clockG.localPosition = new Vector3(11f * k, DeskY, -11f * k);
            clockG.localRotation = Quaternion.Euler(0f, -0.5f * Mathf.Rad2Deg, 0f);
            var handMat = Own(SwordFightShapes.Lit("#1b1410", 0.6f));
            var clockModel = Model("SF_Prop_ChessClock", clockG, Vector3.zero, 0f, Vector3.one);
            Transform[] dials = { Find(clockModel, "Dial_L"), Find(clockModel, "Dial_R") };
            if (clockModel == null)
            {
                SwordFightShapes.Part(PrimitiveType.Cube, Own(SwordFightShapes.Lit("#2a1c13", 0.5f)), clockG, new Vector3(0f, 1.1f, 0f), new Vector3(4.2f, 2.2f, 1.6f));
                var faceMat = Own(SwordFightShapes.Lit("#efe6d2", 0.7f));
                for (int s = 0; s < 2; s++)
                {
                    float sx = s == 0 ? -1f : 1f;
                    SwordFightShapes.Part(PrimitiveType.Cylinder, faceMat, clockG, new Vector3(sx, 1.15f, 0.81f), new Vector3(1.5f, 0.01f, 1.5f), Quaternion.Euler(90f, 0f, 0f));
                    SwordFightShapes.Part(PrimitiveType.Cube, brass, clockG, new Vector3(sx, 2.33f, 0f), new Vector3(0.5f, 0.25f, 0.5f));
                }
            }
            for (int s = 0; s < 2; s++)
            {
                var pivot = new GameObject("hand").transform;
                pivot.SetParent(clockG, false);
                // On the kit's dial (its mark sits on the dial's face; the hand a little in front of it), else the demo's.
                bool onDial = clockModel != null && dials[s] != null;
                pivot.localPosition = onDial ? clockG.InverseTransformPoint(dials[s].position) + new Vector3(0f, 0f, 0.03f) : new Vector3(s == 0 ? -1f : 1f, 1.15f, 0.83f);
                float len = onDial ? 0.4f : 0.6f;
                SwordFightShapes.Part(PrimitiveType.Cube, handMat, pivot, new Vector3(0f, len / 2f, 0f), new Vector3(0.05f, len, 0.02f));
                if (s == 0) clockLeft = pivot; else clockRight = pivot;
            }
            // The banker's lamp and its warm spot light: the kit's floor lamp on the desk, its arm turned toward the
            // board (+z after settling -> (5.5, -2)), the light under its shade; else the demo's pole and cone.
            // R191: all of it hangs on one pivot at the pole's foot, so the lamp can sway on big hits (Jolt).
            lampSwing = new GameObject("Lamp (swings)").transform;
            lampSwing.SetParent(root, false);
            lampSwing.localPosition = new Vector3(-12.5f * k, DeskY, 10f * k);
            var lampModel = Model("SF_Prop_Lamp", lampSwing, Vector3.zero, Mathf.Atan2(5.5f, -2f) * Mathf.Rad2Deg, Vector3.one);
            // The bulb inside the shade: measured on the shade itself (the model's green slot), else its Light_Anchor
            // mark, else the demo's place. The light comes out of the shade's mouth (10-28, user: "실제 램프에서 조명
            // 효과가 나타나게 해"): straight down, leaning a little toward the board's middle.
            var anchor = Find(lampModel, "Light_Anchor");
            Vector3 bulb = new Vector3(-7f * k, 14.7f, 8f * k);
            if (lampModel != null && Shade(lampModel, out var mouth, out float lo, out float hi))
                bulb = root.InverseTransformPoint(new Vector3(mouth.x, lo + 0.35f * (hi - lo), mouth.z));
            else if (anchor != null) bulb = root.InverseTransformPoint(anchor.position);
            Vector3 inward = new Vector3(-bulb.x, 0f, -bulb.z).normalized;
            Vector3 shine = (Vector3.down + inward * Mathf.Tan(LampLean * Mathf.Deg2Rad)).normalized;
            if (lampModel == null)
            {
                SwordFightShapes.Part(PrimitiveType.Cylinder, brass, root, new Vector3(-12.5f * k, DeskY + 10.5f, 10f * k), new Vector3(0.24f, 10.5f, 0.24f)).transform.SetParent(lampSwing, true);
                var shade = SwordFightShapes.MeshObject("Lamp shade", SwordFightShapes.Lathe(new[] { new Vector2(1.6f, 0f), new Vector2(0.05f, 1.4f) }, 24), Own(SwordFightShapes.Lit("#1f4a35", 0.5f)), root);
                shade.transform.localPosition = new Vector3(-7f * k, 15.2f - 0.7f, 8f * k);
                shade.transform.SetParent(lampSwing, true);
                SwordFightShapes.Part(PrimitiveType.Cylinder, brass, root, new Vector3(-9.6f * k, 15.6f, 9f * k), new Vector3(0.2f, 3.6f, 0.2f), Quaternion.Euler(0f, 0f, (Mathf.PI / 2f - 0.4f) * Mathf.Rad2Deg)).transform.SetParent(lampSwing, true);
            }
            SwordFightShapes.Part(PrimitiveType.Sphere, Own(SwordFightShapes.Glow("#fff0c8", 1f)), root, bulb, Vector3.one * (lampModel != null ? 0.5f : 0.7f)).transform.SetParent(lampSwing, true);
            var spot = new GameObject("Lamp light").AddComponent<Light>();
            spot.transform.SetParent(root, false);
            spot.transform.localPosition = bulb;
            spot.transform.rotation = Quaternion.LookRotation(shine, inward);
            spot.type = LightType.Spot;
            spot.color = SwordFightShapes.Hex("#ffcf8a");
            spot.range = 46f;
            // As wide as the shade's mouth lets it out, softer toward its rim.
            spot.spotAngle = LampSpread;
            spot.innerSpotAngle = LampSpread * 0.55f;
            spot.intensity = ChessFight.Game.MenuArt.SpotIntensity(spot, 1.6f);   // URP: the spot fades with the distance squared
            spot.transform.SetParent(lampSwing, true);
            lampLight = spot;
            lampIntensity = spot.intensity;
            // The beam reaches down to 3 m over the board (the skill effects stay clear).
            Beam(spot.transform, Mathf.Min(BeamLength, Mathf.Max(3f, (bulb.y - 3f) / Vector3.Dot(shine, Vector3.down))));
            // The trays.
            for (int s = -1; s <= 1; s += 2)
                if (Model("SF_Tray", root, new Vector3(s * 12f * k, DeskY, 0.5f * k), 0f, new Vector3(1f, 1f, k), Brass) == null)
                    SwordFightShapes.Part(PrimitiveType.Cube, Brass, root, new Vector3(s * 12f * k, DeskY + 0.075f, 0.5f * k), new Vector3(2.2f, 0.15f, 10f * k));

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
            if (sun != null && sun.type == LightType.Directional) RenderSettings.sun = sun;   // the key light, not the moon
            Room();
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = SwordFightShapes.Hex("#140f0c");
            }
        }

        T Own<T>(T thing) where T : Object { owned.Add(thing); return thing; }

        /// <summary>A lit wood material from a kit texture tinted <paramref name="tint"/> (grey), or plain
        /// <paramref name="fallback"/> when the texture is missing.</summary>
        Material Wood(string texture, string fallback, float tint, float roughness)
        {
            var tex = Resources.Load<Texture2D>(Kit + texture);
            if (tex == null) return Own(SwordFightShapes.Lit(fallback, roughness));
            var m = Own(SwordFightShapes.Lit("#ffffff", roughness));
            m.mainTexture = tex;
            m.color = new Color(tint, tint, tint, 1f);
            return m;
        }

        bool kitOn;
        Material frameMat;
        /// <summary>Per model: the quarter turn that lays its long side along z and its offset (the frame's outside, the
        /// corner's quarter) toward +x / +z, whatever axes the FBX import gave it.</summary>
        static readonly Dictionary<string, float> settle = new Dictionary<string, float>();

        /// <summary>A copy of the kit model <paramref name="name"/> under <paramref name="parent"/>: at
        /// <paramref name="at"/>, turned <paramref name="yaw"/> round y, scaled <paramref name="scale"/> in its own axes
        /// (z = its long side), its slots drawn with <paramref name="mats"/> (the last one for any further slot). Null when
        /// the kit is off or the model is missing: the caller draws the demo's box.</summary>
        Transform Model(string name, Transform parent, Vector3 at, float yaw, Vector3 scale, params Material[] mats)
        {
            if (!kitOn) return null;
            var prefab = Resources.Load<GameObject>(Kit + name);
            if (prefab == null)
            {
                Debug.LogWarning($"[SwordFightArena] kit model {Kit}{name} is missing, drawing the demo's box");
                return null;
            }
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.localPosition = at;
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            holder.localScale = scale;
            var turn = new GameObject("settle").transform;
            turn.SetParent(holder, false);
            var copy = Instantiate(prefab, turn, false);
            foreach (var col in copy.GetComponentsInChildren<Collider>()) Destroy(col);
            if (!settle.TryGetValue(name, out float s)) settle[name] = s = Settle(turn, name);
            turn.localRotation = Quaternion.Euler(0f, s, 0f);
            foreach (var r in copy.GetComponentsInChildren<Renderer>())
            {
                var imported = r.sharedMaterials;
                var slots = new Material[imported.Length];
                for (int m = 0; m < slots.Length; m++) slots[m] = mats.Length > 0 ? mats[Mathf.Min(m, mats.Length - 1)] : Named(imported[m]);
                r.sharedMaterials = slots;
            }
            return holder;
        }

        static readonly string[] BookColours = { "#7a2b2b", "#2f4f3a", "#2c3b5e", "#9a7a3a", "#5b3a5e", "#3d3d3d" };
        readonly Dictionary<string, Material> named = new Dictionary<string, Material>();

        /// <summary>A kit prop's slot made from its imported material's name (ArtSource/SwordFight/clean_tripo_v1.py):
        /// SF_rrggbb, SF_rrggbb_m for metal, SF_rrggbb_u unlit (the window's glass); one material per colour. Grey for any
        /// other name.</summary>
        Material Named(Material imported)
        {
            string n = imported != null ? imported.name : "";
            int at = n.IndexOf("SF_", System.StringComparison.Ordinal);
            string hex = at >= 0 && n.Length >= at + 9 ? n.Substring(at + 3, 6) : "808080";
            foreach (char ch in hex) if (!System.Uri.IsHexDigit(ch)) { hex = "808080"; break; }
            string tail = at >= 0 && n.Length >= at + 11 ? n.Substring(at + 9, 2) : "";
            bool metal = tail == "_m", unlit = tail == "_u";
            string key = hex + (metal ? "m" : unlit ? "u" : "");
            if (!named.TryGetValue(key, out var m) || m == null)
                named[key] = m = Own(unlit ? ChessFight.Game.LitMaterials.NewUnlit(SwordFightShapes.Hex("#" + hex))
                    : SwordFightShapes.Lit("#" + hex, metal ? 0.45f : 0.7f, metal ? 0.8f : 0f));
            return m;
        }

        /// <summary>The colour of the lamp's shade slot (ArtSource/SwordFight/clean_tripo_v1.py SHADE).</summary>
        const string ShadeSlot = "1f4a35";

        /// <summary>The lamp's shade under <paramref name="root"/>, measured on its own slot of the mesh (no mark to trust):
        /// the middle of it, and its bottom and top height, in world space. False when there is no such slot.</summary>
        static bool Shade(Transform root, out Vector3 middle, out float bottom, out float top)
        {
            middle = Vector3.zero;
            bottom = top = 0f;
            bool any = false;
            var b = new Bounds();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length && i < mf.sharedMesh.subMeshCount; i++)
                {
                    if (mats[i] == null || !mats[i].name.Contains(ShadeSlot)) continue;
                    var sb = mf.sharedMesh.GetSubMesh(i).bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        var p = r.transform.TransformPoint(sb.center + Vector3.Scale(sb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                        if (any) b.Encapsulate(p); else { b = new Bounds(p, Vector3.zero); any = true; }
                    }
                }
            }
            if (!any || b.size.magnitude < 0.05f) return false;   // no slot, or a mesh without sub-mesh bounds
            middle = b.center;
            bottom = b.min.y;
            top = b.max.y;
            return true;
        }

        /// <summary>The rook statue's outline (radius, height) as fractions of its foot's radius and its height, measured on
        /// the Tripo model (ArtSource/SwordFight/clean_tripo_v2.py): its foot, its body (the waist filled in a little, a
        /// convex collider cannot follow it), its crown.</summary>
        static readonly Vector2[][] RookOutline =
        {
            new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0.083f), new Vector2(0.88f, 0.09f), new Vector2(0.88f, 0.167f), new Vector2(0f, 0.167f) },
            new[] { new Vector2(0f, 0.167f), new Vector2(0.72f, 0.167f), new Vector2(0.6f, 0.27f), new Vector2(0.5f, 0.42f), new Vector2(0.46f, 0.52f),
                new Vector2(0.59f, 0.56f), new Vector2(0.59f, 0.62f), new Vector2(0.62f, 0.79f), new Vector2(0f, 0.79f) },
            new[] { new Vector2(0f, 0.79f), new Vector2(0.81f, 0.81f), new Vector2(0.89f, 0.85f), new Vector2(0.89f, 1f), new Vector2(0f, 1f) },
        };

        /// <summary>Solid stone for the rook statue standing at <paramref name="at"/> (root space): three convex turned
        /// pieces of its outline, sized on the statue as drawn (foot radius, height).</summary>
        void RookCollider(Transform statue, Vector3 at)
        {
            var b = new Bounds(statue.position, Vector3.zero);
            foreach (var r in statue.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            float radius = Mathf.Max(b.extents.x, b.extents.z), height = b.size.y;
            var g = new GameObject("Rook statue (solid)").transform;
            g.SetParent(root, false);
            g.localPosition = at;
            foreach (var part in RookOutline)
            {
                var profile = new Vector2[part.Length];
                for (int i = 0; i < part.Length; i++) profile[i] = new Vector2(part[i].x * radius, part[i].y * height);
                var mesh = Own(SwordFightShapes.Lathe(profile, 16));
                var piece = new GameObject("piece");
                piece.transform.SetParent(g, false);
                var col = piece.AddComponent<MeshCollider>();
                col.sharedMesh = mesh;
                col.convex = true;
            }
        }

        /// <summary>The child of <paramref name="root"/> named <paramref name="name"/> at any depth (an FBX empty), or null.</summary>
        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>()) if (t.name == name) return t;
            return null;
        }

        /// <summary>The quarter turn for <see cref="settle"/>, from the model's mesh bounds in <paramref name="turn"/>'s space
        /// (still unturned). Warns when the model does not stand on its pivot (an FBX axis problem).</summary>
        static float Settle(Transform turn, string name)
        {
            var b = new Bounds();
            bool any = false;
            foreach (var mf in turn.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var m = turn.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                    if (any) b.Encapsulate(p); else { b = new Bounds(p, Vector3.zero); any = true; }
                }
            }
            if (!any) return 0f;
            if (Mathf.Abs(b.min.y) > 0.02f) Debug.LogWarning($"[SwordFightArena] kit model {name} does not stand on its pivot (bottom {b.min.y:0.00} m, size {b.size})");
            // A prop that says where it faces: the lamp's shade (measured), the clock's Front, the lamp's Light_Anchor.
            if (Shade(turn, out var shadeAt, out _, out _))
            {
                Vector3 p = turn.InverseTransformPoint(shadeAt);
                if (p.x * p.x + p.z * p.z > 1e-4f) return -Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
            }
            foreach (var t in turn.GetComponentsInChildren<Transform>())
                if (t.name == "Front" || t.name == "Light_Anchor")
                {
                    Vector3 p = turn.InverseTransformPoint(t.position);
                    if (p.x * p.x + p.z * p.z > 1e-4f) return -Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
                }
            // Else the quarter turn that best lays the long side along z and the offset toward +x / +z.
            float best = float.MaxValue, turnBy = 0f;
            for (int q = 0; q < 4; q++)
            {
                var r = Quaternion.Euler(0f, q * 90f, 0f);
                Vector3 c = r * b.center, e = r * b.size;
                float miss = Mathf.Max(0f, Mathf.Abs(e.x) - Mathf.Abs(e.z)) + Mathf.Max(0f, -c.x) + Mathf.Max(0f, -c.z);
                if (miss < best - 1e-4f) { best = miss; turnBy = q * 90f; }
            }
            return turnBy;
        }

        // ---------------------------------------------------------------- R191 (10-28): the study's atmosphere
        // ArtSource/SwordFight/concept_atmosphere_v1.py renders the same numbers in Blender.

        /// <summary>The lamp's beam: how far it reaches toward the board (m; it stops above the board, the skill effects
        /// stay clear), its half angle (degrees), its strength at the shade (alpha), the dust drifting in it.</summary>
        public static float BeamLength = 15f, BeamAngle = 24f, BeamAlpha = 0.12f;
        /// <summary>The lamp's light: how far it leans from straight down toward the board's middle, and how wide it
        /// spreads (degrees; about the shade's mouth).</summary>
        public static float LampLean = 12f, LampSpread = 80f;
        public static int BeamDust = 140;
        /// <summary>The lamp's sway on a hit of strength 1 (degrees at its foot; the shade is 21 m up), its period and
        /// how fast it dies away (s).</summary>
        public static float SwayDegrees = 0.6f, SwayPeriod = 1.7f, SwayDecay = 1.3f;

        /// <summary>A square landing on the desk (R191): how high it bounces (m), for how long (s), and how much dust
        /// puffs out from under it (particles).</summary>
        public static float LandBounce = 0.25f, LandBounceTime = 0.6f;
        public static int LandDust = 26;

        /// <summary>Dust a trembling square shakes loose (R191, 10-28, user: shaking squares should dust too): grit
        /// sifting out per second per square (puffs at its seams are 0.4 of that).</summary>
        public static float TrembleDustRate = 18f;

        ParticleSystem deskDust, gritDust;
        Material dustMat;
        Transform lampSwing;
        Light lampLight;
        float lampIntensity, swayAmp, swayAge;
        Vector3 swayAxis = Vector3.right;
        readonly HashSet<object> joltedCollapses = new HashSet<object>();

        /// <summary>A big hit somewhere on the board (a square landing on the desk 0.35, the rook's collapse 1, the king's
        /// landing 0.8): the lamp sways and its light dips a little. A new hit only takes over when it is bigger than
        /// what is left of the last one. Each screen sways on its own (the look only).</summary>
        public void Jolt(float strength)
        {
            if (lampSwing == null) return;
            float left = swayAmp * Mathf.Exp(-swayAge / SwayDecay);
            float want = Mathf.Clamp01(strength) * SwayDegrees;
            if (want <= left) return;
            swayAmp = want;
            swayAge = 0f;
            float a = Random.Range(0f, Mathf.PI * 2f);
            swayAxis = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        void Update()
        {
            if (lampSwing == null || swayAmp <= 0f) return;
            swayAge += Time.deltaTime;
            float fade = Mathf.Exp(-swayAge / SwayDecay);
            float swing = Mathf.Sin(swayAge * Mathf.PI * 2f / SwayPeriod);
            lampSwing.localRotation = Quaternion.AngleAxis(swayAmp * fade * swing, swayAxis);
            if (lampLight != null) lampLight.intensity = lampIntensity * (1f - 0.12f * fade * Mathf.Abs(swing));
            if (fade < 0.01f)
            {
                swayAmp = 0f;
                lampSwing.localRotation = Quaternion.identity;
                if (lampLight != null) lampLight.intensity = lampIntensity;
            }
        }

        /// <summary>The beam under the shade at <paramref name="light"/> (on its pivot, so it sways with the lamp): three
        /// nested see-through cones whose warm colour fades from the shade to nothing (soft edges without a custom
        /// shader), and dust drifting slowly inside.</summary>
        void Beam(Transform light, float length)
        {
            var beam = new GameObject("Lamp beam").transform;
            beam.SetParent(light.parent, false);
            beam.position = light.position;
            beam.rotation = light.rotation;   // +z out of the shade's mouth
            var mat = Own(new Material(Shader.Find("Sprites/Default")) { name = "Lamp beam", renderQueue = 3050 });
            float tan = Mathf.Tan(BeamAngle * Mathf.Deg2Rad);
            Color warm = SwordFightShapes.Hex("#ffcf8a");
            foreach (var (size, alpha) in new[] { (1f, 0.25f), (0.72f, 0.33f), (0.45f, 0.42f) })
            {
                var go = SwordFightShapes.MeshObject("Beam layer", BeamCone(length, length * tan * size, warm, BeamAlpha * alpha), mat, beam);
                var r = go.GetComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            if (BeamDust <= 0) return;
            // The dust: a soft dot drifting in the cone, fading in and out.
            var dust = new GameObject("Beam dust");
            dust.transform.SetParent(beam, false);
            var ps = dust.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new Color(1f, 0.93f, 0.8f, 0.7f);
            main.maxParticles = BeamDust * 2;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = ps.emission;
            emission.rateOverTime = BeamDust / 8f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.ConeVolume;
            shape.angle = BeamAngle * 0.9f;
            shape.radius = 0.3f;
            shape.length = length * 0.8f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.3f;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var pr = dust.GetComponent<ParticleSystemRenderer>();
            var dot = Own(new Material(Shader.Find("Sprites/Default")) { name = "Beam dust", mainTexture = Own(SoftDot()) });
            pr.sharedMaterial = dot;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            ps.Play();
        }

        /// <summary>Dust puffing out along the desk from under a square that has just landed at <paramref name="at"/>:
        /// a ring of soft, dusty grey-brown clouds rolling outward, rising a little, growing and fading in about two
        /// seconds (one shared particle system, so a whole falling row costs nothing extra).</summary>
        void DeskDust(Vector3 at)
        {
            if (LandDust <= 0 || root == null) return;
            if (deskDust == null) deskDust = DustSystem("Desk dust", -0.02f, 0.2f, true);   // the warm air lifts it a little
            Vector3 world = root.TransformPoint(new Vector3(at.x, DeskY + 0.15f, at.z));
            Color dust = SwordFightShapes.Hex("#6e5e4e");
            float half = cell / 2f;
            for (int n = 0; n < LandDust; n++)
            {
                float a = n / (float)LandDust * Mathf.PI * 2f + Random.Range(-0.1f, 0.1f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                // From under the square's rim (it is square, the ring is round: the corners reach a little further).
                float rim = half / Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.z)) * 0.9f;
                var p = new ParticleSystem.EmitParams
                {
                    position = world + dir * rim,
                    velocity = dir * Random.Range(1.2f, 2.6f) + Vector3.up * Random.Range(0.1f, 0.6f),
                    startSize = Random.Range(0.5f, 1.1f),
                    startLifetime = Random.Range(1.4f, 2.4f),
                    startColor = new Color(dust.r, dust.g, dust.b, Random.Range(0.35f, 0.6f)),
                    rotation = Random.Range(0f, 360f),
                };
                deskDust.Emit(p, 1);
            }
        }

        /// <summary>A world-space particle system that only plays what Emit puts in (soft dusty dots fading in and out):
        /// <paramref name="gravity"/> (negative lifts), slowed toward <paramref name="drift"/> m/s, growing as it goes or not.</summary>
        ParticleSystem DustSystem(string name, float gravity, float drift, bool grows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;                   // always playing, with no emission of its own: Emit adds bursts
            main.playOnAwake = false;
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = drift;
            drag.dampen = 0.08f;
            if (grows)
            {
                var grow = ps.sizeOverLifetime;
                grow.enabled = true;
                grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.6f));
            }
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            if (dustMat == null) dustMat = Own(new Material(Shader.Find("Sprites/Default")) { name = "Dust", mainTexture = Own(SoftDot()) });
            pr.sharedMaterial = dustMat;
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.sortingFudge = 10f;
            ps.Play();
            return ps;
        }

        /// <summary>A trembling square (a ghost's warning, a rook's crack) at <paramref name="p"/> (root space, its middle)
        /// shaking dust loose: soft puffs out of the seams round its top, and fine grit sifting out of its underside that
        /// falls toward the desk. Rates per second per square (TrembleDustRate), spread over the frames.</summary>
        void TrembleDust(Square s, Vector3 p, float dt)
        {
            if (TrembleDustRate <= 0f || root == null || dt <= 0f) return;
            if (deskDust == null) deskDust = DustSystem("Desk dust", -0.02f, 0.2f, true);
            if (gritDust == null) gritDust = DustSystem("Falling grit", 0.6f, 6f, false);
            Color dust = SwordFightShapes.Hex("#6e5e4e");
            float half = (cell - 0.04f) / 2f;
            s.puffs += dt * TrembleDustRate * 0.4f;
            s.grit += dt * TrembleDustRate;
            for (; s.puffs >= 1f; s.puffs -= 1f)
            {
                // A point on the square's rim, at its top: out through the seam, a little up.
                Vector3 edge = Rim(half);
                var dir = edge.normalized;
                deskDust.Emit(new ParticleSystem.EmitParams
                {
                    position = root.TransformPoint(p + new Vector3(edge.x, 0.3f, edge.z)),
                    velocity = dir * Random.Range(0.1f, 0.35f) + Vector3.up * Random.Range(0.05f, 0.3f),
                    startSize = Random.Range(0.2f, 0.45f),
                    startLifetime = Random.Range(0.8f, 1.4f),
                    startColor = new Color(dust.r, dust.g, dust.b, Random.Range(0.25f, 0.45f)),
                    rotation = Random.Range(0f, 360f),
                }, 1);
            }
            for (; s.grit >= 1f; s.grit -= 1f)
            {
                // Out of the underside, near its edges (the seams), falling.
                Vector3 edge = Rim(half) * Random.Range(0.75f, 1f);
                gritDust.Emit(new ParticleSystem.EmitParams
                {
                    position = root.TransformPoint(p + new Vector3(edge.x, -0.3f, edge.z)),
                    velocity = Vector3.down * Random.Range(0f, 0.6f) + new Vector3(Random.Range(-0.1f, 0.1f), 0f, Random.Range(-0.1f, 0.1f)),
                    startSize = Random.Range(0.04f, 0.1f),
                    startLifetime = Random.Range(1.1f, 1.8f),
                    startColor = new Color(dust.r * 1.2f, dust.g * 1.2f, dust.b * 1.2f, Random.Range(0.5f, 0.8f)),
                }, 1);
            }
        }

        /// <summary>A random point on the rim of a square <paramref name="half"/> wide each way (x / z, y 0).</summary>
        static Vector3 Rim(float half)
        {
            float t = Random.Range(-half, half);
            switch (Random.Range(0, 4))
            {
                case 0: return new Vector3(-half, 0f, t);
                case 1: return new Vector3(half, 0f, t);
                case 2: return new Vector3(t, 0f, -half);
                default: return new Vector3(t, 0f, half);
            }
        }

        /// <summary>An open cone along +z from its tip (0) to <paramref name="length"/>, <paramref name="radius"/> wide at
        /// the end, coloured <paramref name="c"/> with alpha <paramref name="alpha"/> at the tip fading to 0 at the end.</summary>
        static Mesh BeamCone(float length, float radius, Color c, float alpha)
        {
            const int seg = 32, rings = 6;
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float u = r / (float)rings;
                for (int s = 0; s <= seg; s++)
                {
                    float a = s / (float)seg * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * radius * u, Mathf.Sin(a) * radius * u, length * u));
                    cols.Add(new Color(c.r, c.g, c.b, alpha * (1f - u) * (1f - u)));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < seg; s++)
                {
                    int i0 = r * (seg + 1) + s, i1 = i0 + seg + 1;
                    tris.AddRange(new[] { i0, i1, i0 + 1, i0 + 1, i1, i1 + 1 });
                }
            var m = new Mesh { name = "Lamp beam" };
            m.SetVertices(verts);
            m.SetColors(cols);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>A small round soft dot (white, alpha falling off from the middle) for the dust.</summary>
        static Texture2D SoftDot()
        {
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Soft dot", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude / (n / 2f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
                }
            t.Apply();
            return t;
        }

        /// <summary>The room round the desk, far off in the fog: walls of dark bookcases behind the board and on both
        /// sides, a tall window behind the board with pale panes, and the moonlight through it (cool, from behind,
        /// no shadows: the lamp keeps the only shadow). It does not change with the board's size.</summary>
        void Room()
        {
            var room = new GameObject("Room").transform;
            room.SetParent(root, false);
            var wood = Own(SwordFightShapes.Lit("#1c130d", 0.8f));
            string[] spineCols = { "#3a1a16", "#1d2a22", "#1c2236", "#3a3018", "#2a1e2c" };
            var spines = new Material[spineCols.Length];
            for (int i = 0; i < spines.Length; i++) spines[i] = Own(SwordFightShapes.Lit(spineCols[i], 0.9f));
            const float wallZ = 48f, floorY = DeskY;
            for (float x = -60f; x < 60f; x += 8.4f)
            {
                if (x > -14f && x < 14f) continue;   // the window
                Bookcase(room, new Vector3(x + 4f, floorY, wallZ), Quaternion.identity, wood, spines, (int)(x + 60f));
            }
            for (int side = -1; side <= 1; side += 2)
                for (int n = 0; n < 5; n++)
                    Bookcase(room, new Vector3(side * 55f, floorY, 40f - n * 8.4f), Quaternion.Euler(0f, side * 90f, 0f), wood, spines, n * 3 + side);
            // The window: R191 the Tripo arched frame as it is (28 m wide, its Front toward the board; user 10-28: no
            // glass or bars of ours) with the moonlit night behind it as before (one pale unlit sheet behind its
            // openings, "창틀에 아까처럼 배경을 해줘"); else a dark frame and twelve pale panes. Unlit, so the fog still dims it.
            var night = Own(ChessFight.Game.LitMaterials.NewUnlit(SwordFightShapes.Hex("#6f82a6")));
            var frame = Model("SF_Prop_Window", room, new Vector3(0f, floorY + 3f, 50f), 180f, Vector3.one);
            if (frame != null)
            {
                var b = new Bounds(frame.position, Vector3.zero);
                foreach (var r in frame.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                // The moonlit night painted on it (T_SF_Night: sky, the full moon in the arch, stars; ArtSource/
                // SwordFight/make_night_texture.py, user 10-28 "달 배경 만들어"), else the plain blue-grey.
                var sky = Resources.Load<Texture2D>(Kit + "T_SF_Night");
                var sheetMat = night;
                if (sky != null)
                {
                    sheetMat = Own(ChessFight.Game.LitMaterials.NewUnlit(Color.white));
                    sheetMat.mainTexture = sky;
                }
                var sheet = SwordFightShapes.Part(PrimitiveType.Quad, sheetMat, room, Vector3.zero, new Vector3(b.size.x * 0.78f, b.size.y * 0.9f, 1f));
                sheet.name = "Window night";
                sheet.transform.position = new Vector3(b.center.x, b.center.y, b.max.z + 0.5f);   // just behind the frame
                sheet.transform.rotation = Quaternion.identity;                                     // facing -z, the board
                sheet.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            else
            {
                SwordFightShapes.Part(PrimitiveType.Cube, Own(SwordFightShapes.Lit("#120c08", 0.7f)), room, new Vector3(0f, floorY + 20f, 50f), new Vector3(26f, 34f, 1f));
                for (int col = 0; col < 3; col++)
                    for (int row = 0; row < 4; row++)
                        SwordFightShapes.Part(PrimitiveType.Cube, night, room, new Vector3(-8.2f + col * 8.2f, floorY + 6.5f + row * 8f, 49.4f), new Vector3(7.4f, 7.2f, 0.3f));
            }
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.transform.SetParent(room, false);
            moon.type = LightType.Directional;
            moon.color = SwordFightShapes.Hex("#8fa8d8");
            moon.intensity = 0.3f;
            moon.shadows = LightShadows.None;
            moon.transform.rotation = Quaternion.LookRotation(new Vector3(0.2f, -0.55f, -1f));
        }

        /// <summary>One bookcase 8 m wide, 40 m tall, 3 m deep standing on the floor at <paramref name="at"/>, facing
        /// -z turned by <paramref name="turn"/>, with nine rows of books.</summary>
        static void Bookcase(Transform room, Vector3 at, Quaternion turn, Material wood, Material[] spines, int seed)
        {
            var g = new GameObject("Bookcase").transform;
            g.SetParent(room, false);
            g.localPosition = at;
            g.localRotation = turn;
            SwordFightShapes.Part(PrimitiveType.Cube, wood, g, new Vector3(0f, 20f, 0f), new Vector3(8f, 40f, 3f));
            for (int row = 0; row < 9; row++)
                SwordFightShapes.Part(PrimitiveType.Cube, spines[Mathf.Abs(seed + row) % spines.Length], g, new Vector3(0f, 3.5f + row * 4.2f, -1.3f), new Vector3(7.2f, 3f, 0.4f));
        }

        /// <summary>One length of the board's edge on a square, its middle at <paramref name="at"/> on the edge line (square
        /// space), running <paramref name="along"/>: the kit's frame turned <paramref name="yaw"/> (its +x out of the board),
        /// or the demo's brass strip.</summary>
        void Edge(Square sq, Vector3 at, Vector3 along, float yaw, float frameY, Material frameMat)
        {
            float k = cell / 2f;
            var frame = Model("SF_Frame_2m", sq.t, new Vector3(at.x, frameY, at.z), yaw, new Vector3(1f, 1f, k), frameMat, Brass);
            Renderer[] parts;
            if (frame != null)
            {
                parts = frame.GetComponentsInChildren<Renderer>();
                FrameEdge = true;
            }
            else
            {
                var size = along == Vector3.forward ? new Vector3(0.06f, 0.62f, cell) : new Vector3(cell, 0.62f, 0.06f);
                parts = new[] { SwordFightShapes.Part(PrimitiveType.Cube, Brass, sq.t, at, size).GetComponent<Renderer>() };
            }
            sq.brass.Add(new Trim { parts = parts, at = at, along = along, length = cell });
        }

        void Clear()
        {
            if (root != null) Destroy(root.gameObject);
            foreach (var o in owned) if (o != null) Destroy(o);
            owned.Clear();
            named.Clear();
            squares.Clear();
        }

        void OnDestroy() => Clear();

        /// <summary>A ghost's warning and drop for a square (the board's plan); NaN for none.</summary>
        /// <summary>The wood of the square under <paramref name="p"/> (its maple or walnut), the nearest one off the board's
        /// edge (10-25: the king's breach shows it under the brass and throws its chips). False before the board is built.</summary>
        public bool WoodAt(Vector3 p, out Color wood)
        {
            wood = Color.white;
            float best = float.MaxValue;
            foreach (var s in squares)
            {
                float d = new Vector2(s.home.x - p.x, s.home.z - p.z).sqrMagnitude;
                if (d < best) { best = d; wood = s.wood; }
            }
            return squares.Count > 0;
        }

        public void Plan(int cell, double warn, double drop)
        {
            foreach (var s in squares) if (s.cell == cell) { s.warn = warn; s.drop = drop; }
        }

        public void ResetPlan()
        {
            foreach (var s in squares)
            {
                s.warn = s.drop = double.NaN;
                s.landed = false;
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
                bool shown = true, trembling = false;
                if (!double.IsNaN(s.drop))
                {
                    if (clock >= s.warn && clock < s.drop)
                    {
                        float age = (float)(clock - s.warn), u = age / (float)(s.drop - s.warn);
                        glow = (0.25f + 0.75f * (0.5f + 0.5f * Mathf.Sin(age * (5f + u * 18f) * Mathf.PI))) * (0.6f + u * 0.8f);
                        if (u > 0.6f)
                        {
                            jit = (u - 0.6f) * 0.12f * Mathf.Sin(Time.time * 90f + s.cell * 3f);
                            trembling = true;
                        }
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
                            // R191: it hits the desk now (not long ago: a late joiner's board is already lying there):
                            // the lamp sways, dust puffs out from under it, and it bounces twice, smaller each time.
                            float since = f - fallTime;
                            if (!s.landed && since < 0.5f)
                            {
                                Jolt(0.35f);
                                DeskDust(s.rest);
                            }
                            s.landed = true;
                            if (since < LandBounceTime)
                                p.y += LandBounce * Mathf.Exp(-since * 8f) * Mathf.Abs(Mathf.Sin(since * Mathf.PI / 0.18f));
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
                        if (c.phase == SwordFightEdgeFloor.Phase.Crack)
                        {
                            jit = 0.04f * Mathf.Sin(Time.time * 70f + s.cell);
                            trembling = true;
                        }
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
                if (trembling && shown) TrembleDust(s, p, Time.deltaTime);
                s.t.localPosition = new Vector3(p.x + jit, p.y, p.z - jit);
                s.t.localRotation = turn;
                s.mat.color = Color.Lerp(s.baseColor, GlowColor, Mathf.Clamp01(glow * 0.6f));
            }
            // R191: a rook's collapse tearing the edge off sways the lamp, once per collapse.
            if (floor != null)
            {
                if (joltedCollapses.Count > 64) joltedCollapses.Clear();
                foreach (var c in floor.Collapses)
                    if (c.phase == SwordFightEdgeFloor.Phase.Open && joltedCollapses.Add(c)) Jolt(1f);
            }
            if (clockLeft != null) clockLeft.localRotation = Quaternion.Euler(0f, 0f, -(float)(clock / 60.0 * 360.0));
            if (clockRight != null) clockRight.localRotation = Quaternion.Euler(0f, 0f, -(float)(clock / 30.0 * 180.0));
        }

        static float Ease(float u) => u < 0.5f ? 2f * u * u : 1f - Mathf.Pow(-2f * u + 2f, 2f) / 2f;
    }
}
