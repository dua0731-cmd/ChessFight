using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Parts the v1.0 mini-games share: chess pieces from primitives (a body that pawns bump into, kinematic and
    // unclimbable - mini-games v1.0 §1.2: anything a pawn touches is moved by the host's numbers, never pushed by
    // physics), board squares, and the station's frame with black's mirror.
    public static class MiniGameParts
    {
        // Black's station is white's mirror across v (§0).
        public static float Mirror(MissionStation station) => station != null && station.Mirrored ? -1f : 1f;

        // A station-board point (white's view) in the game's frame (floor top at 0), mirrored for black.
        public static Vector3 Local(MissionStation station, StationPoint p, float y = 0f) => new Vector3(p.V * Mirror(station), y, p.U);

        // Back from the game's frame to white's view.
        public static StationPoint Board(MissionStation station, Vector3 local) => new StationPoint(local.x * Mirror(station), local.z);

        public static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static float FlatSpeed(Vector3 v) => Mathf.Sqrt(v.x * v.x + v.z * v.z);

        public static Color PieceColor(int team) => team == Teams.Black ? Arena.Hex("#2F2926") : Arena.Hex("#F3EADB");
        public static Material PieceMaterial(int team) => Arena.Lit(team == Teams.Black ? "M_PieceBlack" : "M_PieceWhite", PieceColor(team), 0f, .45f);

        // A chess piece `height` tall standing on `at` (its base) in `parent`, with a ring of `ring` round its base.
        // solid: a kinematic body with a capsule collider and NoClimbSurface. The picture parts have no colliders.
        public static GameObject Piece(Transform parent, string name, PieceKind kind, float height, Material body, Color ring, bool solid, float radius = .35f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var look = new GameObject("Look").transform;
            look.SetParent(root.transform, false);
            float h = height;
            Part(look, PrimitiveType.Cylinder, new Vector3(0f, .05f * h, 0f), new Vector3(.55f * h, .05f * h, .55f * h), body);
            switch (kind)
            {
                case PieceKind.Rook:
                    Part(look, PrimitiveType.Cylinder, new Vector3(0f, .45f * h, 0f), new Vector3(.38f * h, .38f * h, .38f * h), body);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * .5f;
                        Part(look, PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * .14f * h, .9f * h, Mathf.Sin(a) * .14f * h), Vector3.one * .12f * h, body);
                    }
                    break;
                case PieceKind.Bishop:
                    Part(look, PrimitiveType.Capsule, new Vector3(0f, .5f * h, 0f), new Vector3(.3f * h, .38f * h, .3f * h), body);
                    Part(look, PrimitiveType.Sphere, new Vector3(0f, .93f * h, 0f), Vector3.one * .1f * h, body);
                    break;
                case PieceKind.Knight:
                    Part(look, PrimitiveType.Cylinder, new Vector3(0f, .3f * h, 0f), new Vector3(.32f * h, .22f * h, .32f * h), body);
                    Part(look, PrimitiveType.Cube, new Vector3(0f, .66f * h, -.03f * h), new Vector3(.2f * h, .45f * h, .26f * h), body).transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
                    Part(look, PrimitiveType.Cube, new Vector3(0f, .82f * h, .14f * h), new Vector3(.17f * h, .15f * h, .32f * h), body);
                    break;
                case PieceKind.King:
                    Part(look, PrimitiveType.Capsule, new Vector3(0f, .45f * h, 0f), new Vector3(.34f * h, .36f * h, .34f * h), body);
                    Part(look, PrimitiveType.Cube, new Vector3(0f, .93f * h, 0f), new Vector3(.05f * h, .16f * h, .05f * h), body);
                    Part(look, PrimitiveType.Cube, new Vector3(0f, .95f * h, 0f), new Vector3(.14f * h, .05f * h, .05f * h), body);
                    break;
                case PieceKind.Queen:
                    Part(look, PrimitiveType.Capsule, new Vector3(0f, .45f * h, 0f), new Vector3(.34f * h, .36f * h, .34f * h), body);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * Mathf.PI * 2f / 5f;
                        Part(look, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * .13f * h, .86f * h, Mathf.Sin(a) * .13f * h), Vector3.one * .07f * h, body);
                    }
                    break;
                default:   // the pawn
                    Part(look, PrimitiveType.Capsule, new Vector3(0f, .38f * h, 0f), new Vector3(.36f * h, .3f * h, .36f * h), body);
                    Part(look, PrimitiveType.Sphere, new Vector3(0f, .82f * h, 0f), Vector3.one * .3f * h, body);
                    break;
            }
            if (ring.a > 0f)
                Part(look, PrimitiveType.Cylinder, new Vector3(0f, .015f, 0f), new Vector3(.7f * h, .015f, .7f * h), Arena.Lit("M_PieceRing", ring, .4f, .5f)).name = "Ring";
            if (solid)
            {
                var rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.radius = radius;
                capsule.height = Mathf.Max(height, radius * 2f);
                capsule.center = new Vector3(0f, capsule.height * .5f, 0f);
                root.AddComponent<NoClimbSurface>();
            }
            return root;
        }

        public static GameObject Part(Transform parent, PrimitiveType type, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        // A gold ring (a loop of line) lying flat or standing, for windows, rims and handles' looks.
        public static LineRenderer Loop(Transform parent, string name, Vector3 at, float radius, float width, Color color, bool flat = true, int points = 32)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = points;
            line.widthMultiplier = width;
            line.sharedMaterial = Arena.Glow(Color.white);
            line.startColor = line.endColor = color;
            for (int i = 0; i < points; i++)
            {
                float a = i * Mathf.PI * 2f / points;
                line.SetPosition(i, flat ? new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius) : new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
            return line;
        }

        // A grabbable gold handle: a small box with a kinematic body of its own, so a hand's joint hangs on it.
        public static GrabHandle Handle(Transform parent, string name, Vector3 at, Vector3 size, MiniGameBase owner, byte id, GrabHandle.HandleKind kind)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var h = go.AddComponent<GrabHandle>();
            h.Owner = owner;
            h.Id = id;
            h.Kind = kind;
            return h;
        }

        // The pawn's flat distance and speed in the station, and what it is doing, for the dive rules (F, H, I):
        // diving, or fast (a pawn's two steps at 7.5 m/s count), or a rook's charge.
        public static bool Lunging(MissionStation.Pawn p, float speed = 5f)
        {
            var mg = p.Driver as IMiniGamePawn;
            if (mg != null && mg.Diving) return true;
            var v = mg != null ? mg.HipsVelocity : p.Velocity;
            return FlatSpeed(v) >= speed;
        }

        public static PieceKind PieceOf(ICharacterDriver driver) => driver is IPromotable p ? p.Piece : PieceKind.Pawn;
    }
}
