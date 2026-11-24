using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // The grand banquet hall's chandeliers as shapes (R818, the user: "샹들리에가 샹들리에처럼 안생겼어. 그레이박스라도
    // 모델링과 텍스쳐를 입히는데 기본이 되는 모양이기 때문에 확실하게 생겨야 해"). The look is the banquet concept art's
    // (Docs/PawnRush/ConceptArt/R787_BanquetHall): gilded crown chandeliers on chains, rings of candles, crystal drops.
    //
    //   Hanging(length): one of the ropes the pawns swing on. Its frame is the rope's pivot (y 0) with the rope hanging
    //     down -y: the hook, the chain, then the last 2.5 m are the chandelier - a crown cap, six chains fanning down to
    //     the main ring, a turned stem, an upper ring of six candles, the main ring (1.2 m) of ten candles with crown
    //     points between them and crystal drops under it, a finial, and at the very end (y = -length) the gilt hoop the
    //     pawn hangs from - the rope's end.
    //   Great(): the great chandelier in the middle of the hall that the pawns rest on. Its frame is its floor's middle
    //     (y 0 = the floor): a wide gilded dish (8.4 m, its top the floor) with a lip of 24 candles and crystal drops
    //     under it, a turned column with eight S-arms of candles and a crown on top, four chains from the crown to the
    //     dish (off the rope lines), a pendant under the dish and the chain up to the ceiling.
    //
    // Each is one mesh with four submeshes (brass, wax, flame, crystal), built once and shared: pictures only, no
    // collider. Built from three kinds of piece: a lathe (a profile turned round the y axis), a torus (rings, chain
    // links) and a tube along a curve (arms, chains).
    public static class BanquetChandelierShape
    {
        public const int Brass = 0, Wax = 1, Flame = 2, Crystal = 3, Parts = 4;

        // The great chandelier's sizes (its floor and column are colliders the builder adds).
        public const float GreatDish = 4.2f, GreatColumn = .55f, GreatColumnTop = 4.6f;

        static readonly Dictionary<string, Mesh> made = new Dictionary<string, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => made.Clear();

        public static Mesh Hanging(float length)
        {
            string key = "hanging " + length.ToString("0.00");
            if (made.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            var b = new Builder();
            float top = -(length - 2.5f);   // where the chain meets the chandelier

            // The hook on the pivot and the chain down to the crown cap: links 0.17 m apart, every other one turned.
            b.Torus(Brass, .12f, .03f, M(V(0f, -.1f, 0f), Quaternion.Euler(90f, 0f, 0f)), 16, 6);
            for (float y = -.26f; y > top + .05f; y -= .17f)
                // A link: a ring stood on edge (its axis turned from y to z), stretched 1.6 times along the chain (its
                // local z after the turn is the hall's -y).
                b.Torus(Brass, .07f, .022f, M(V(0f, y, 0f), Quaternion.Euler(90f, (Mathf.Round(y / .17f) % 2f) * 90f, 0f), V(1f, 1f, 1.6f)), 10, 5);

            // The crown cap, and six chains fanning down to the main ring.
            b.Lathe(Brass, P(0f, .1f, .12f, .08f, .22f, 0f, .18f, -.08f, .06f, -.13f), M(V(0f, top, 0f)), 20);
            const float ring = 1.2f, ringY = -1.25f;
            for (int i = 0; i < 6; i++)
            {
                var d = Dir(i * 60f + 30f);
                b.Tube(Brass, new[] { V(0f, top - .1f, 0f) + d * .12f, V(0f, top + ringY + .04f, 0f) + d * (ring - .03f) }, .022f, 6);
            }

            // The turned stem, top to bottom.
            b.Lathe(Brass, P(.06f, 0f, .06f, -.22f, .14f, -.32f, .06f, -.44f, .05f, -.6f, .17f, -.78f, .24f, -.98f, .2f, -1.14f, .09f, -1.26f,
                             .07f, -1.48f, .16f, -1.62f, .2f, -1.78f, .13f, -1.93f, .05f, -2.03f, 0f, -2.08f), M(V(0f, top - .12f, 0f)), 20);

            // The upper ring of six candles.
            b.Torus(Brass, .62f, .035f, M(V(0f, top - .55f, 0f)), 36, 6);
            for (int i = 0; i < 6; i++) b.Tube(Brass, new[] { V(0f, top - .58f, 0f) + Dir(i * 60f) * .08f, V(0f, top - .58f, 0f) + Dir(i * 60f) * .6f }, .02f, 5);
            for (int i = 0; i < 6; i++) CandleCup(b, V(0f, top - .55f, 0f) + Dir(i * 60f) * .62f, .8f);

            // The main ring: a thick ring and a band under it, four spokes to the stem, ten candles with crown points
            // between them and a crystal drop under each point.
            float y0 = top + ringY;
            b.Torus(Brass, ring, .06f, M(V(0f, y0, 0f)), 48, 8);
            b.Torus(Brass, ring - .05f, .035f, M(V(0f, y0 - .14f, 0f)), 48, 6);
            for (int i = 0; i < 4; i++) b.Tube(Brass, new[] { V(0f, y0 - .07f, 0f) + Dir(i * 90f + 45f) * .2f, V(0f, y0 - .07f, 0f) + Dir(i * 90f + 45f) * (ring - .05f) }, .03f, 6);
            for (int i = 0; i < 10; i++)
            {
                CandleCup(b, V(0f, y0 + .04f, 0f) + Dir(i * 36f) * ring, 1f);
                var point = V(0f, y0 + .05f, 0f) + Dir(i * 36f + 18f) * ring;
                b.Lathe(Brass, P(0f, .26f, .03f, .18f, .045f, .03f, .03f, 0f), M(point), 8);
                b.Lathe(Crystal, P(0f, 0f, .035f, -.05f, .055f, -.13f, 0f, -.24f), M(point + V(0f, -.2f, 0f)), 8);
            }

            // The finial and the hoop at the rope's end.
            b.Lathe(Brass, P(.05f, 0f, .11f, -.08f, .08f, -.16f, .03f, -.26f), M(V(0f, top - 2.2f, 0f)), 16);
            b.Torus(Brass, .14f, .035f, M(V(0f, -length + .175f, 0f), Quaternion.Euler(90f, 0f, 0f)), 18, 6);

            mesh = b.Build("BanquetChandelier " + length.ToString("0.0"));
            made[key] = mesh;
            return mesh;
        }

        public static Mesh Great(float chainUp)
        {
            string key = "great " + chainUp.ToString("0.00");
            if (made.TryGetValue(key, out var mesh) && mesh != null) return mesh;
            var b = new Builder();
            float r = GreatDish;

            // The dish: its top the floor (out to the lip), the lip, its underside down to the pendant's seat.
            b.Lathe(Brass, P(0f, 0f, r - .05f, 0f, r, .14f, r + .13f, .16f, r + .18f, .05f, r + .1f, -.08f, r - .3f, -.22f, 3f, -.45f,
                             1.6f, -.78f, .5f, -1.05f, 0f, -1.12f), M(Vector3.zero), 64);
            for (int i = 0; i < 24; i++)
            {
                var at = Dir(i * 15f + 7.5f) * (r + .12f);
                CandleCup(b, at + V(0f, .17f, 0f), 1.1f);
                b.Lathe(Crystal, P(0f, 0f, .05f, -.07f, .08f, -.19f, 0f, -.36f), M(at + V(0f, -.12f, 0f)), 8);
            }
            // The pendant under it.
            b.Lathe(Crystal, P(0f, -1.05f, .5f, -1.3f, .32f, -1.62f, .16f, -2f, .26f, -2.3f, .1f, -2.62f, 0f, -2.85f), M(Vector3.zero), 24);

            // The column, the S-arms and the crown.
            b.Lathe(Brass, P(0f, GreatColumnTop, .08f, 4.48f, .16f, 4.1f, .3f, 3.8f, .18f, 3.6f, .2f, 3.02f, .36f, 2.8f, .22f, 2.6f, .26f, 2.0f,
                             .44f, 1.72f, .56f, 1.4f, .46f, 1.1f, .32f, .8f, .38f, .32f, .6f, .15f, GreatColumn, 0f), M(Vector3.zero), 32);
            for (int i = 0; i < 8; i++)
            {
                var d = Dir(i * 45f + 22.5f);
                var arm = new List<Vector3>();
                for (int s = 0; s <= 12; s++)
                {
                    float u = s / 12f;
                    // Out from the column, dipping, then rising to the cup: an S in the arm's plane.
                    float out_ = Mathf.Lerp(.3f, 1.9f, u), up = 1.95f - .45f * Mathf.Sin(Mathf.PI * u) + .75f * u * u;
                    arm.Add(d * out_ + V(0f, up, 0f));
                }
                b.Tube(Brass, arm.ToArray(), .05f, 8);
                CandleCup(b, arm[arm.Count - 1] + V(0f, .02f, 0f), 1.3f);
                b.Lathe(Crystal, P(0f, 0f, .045f, -.06f, .07f, -.16f, 0f, -.3f), M(d * 1.15f + V(0f, 1.42f, 0f)), 8);
            }
            b.Torus(Brass, .8f, .06f, M(V(0f, 3.9f, 0f)), 40, 8);
            for (int i = 0; i < 8; i++)
            {
                var at = Dir(i * 45f) * .8f + V(0f, 3.92f, 0f);
                b.Lathe(Brass, P(0f, .5f, .05f, .38f, .08f, .06f, .05f, 0f), M(at), 10);
                b.Lathe(Brass, P(0f, .62f, .06f, .56f, 0f, .5f), M(at), 8);
            }

            // Four chains from the crown to the dish's lip (along the hall's axes, off the two rope lines), and the chain
            // up to the ceiling.
            for (int i = 0; i < 4; i++)
                b.Tube(Brass, new[] { Dir(i * 90f) * .75f + V(0f, 3.95f, 0f), Dir(i * 90f) * (r + .05f) + V(0f, .2f, 0f) }, .035f, 6);
            for (float y = GreatColumnTop + .1f; y < GreatColumnTop + chainUp; y += .26f)
                b.Torus(Brass, .11f, .035f, M(V(0f, y, 0f), Quaternion.Euler(90f, (Mathf.Round(y / .26f) % 2f) * 90f, 0f), V(1f, 1f, 1.6f)), 10, 5);

            mesh = b.Build("BanquetGreatChandelier");
            made[key] = mesh;
            return mesh;
        }

        // A candle cup on a point: a little turned cup, the candle and its flame. `size` 1 is a 0.3 m candle.
        static void CandleCup(Builder b, Vector3 at, float size)
        {
            // Profiles run top to bottom down the outside (Builder.Lathe).
            b.Lathe(Brass, P(.09f * size, .06f * size, .11f * size, .05f * size, .1f * size, .01f * size, .04f * size, -.04f * size, 0f, -.05f * size),
                    M(at), 12);
            b.Lathe(Wax, P(0f, .365f * size, .03f * size, .36f * size, .045f * size, .34f * size, .045f * size, .05f * size), M(at), 10);
            b.Lathe(Flame, P(0f, .58f * size, .03f * size, .5f * size, .045f * size, .43f * size, .02f * size, .38f * size, 0f, .375f * size), M(at), 8);
        }

        // ------------------------------------------------------------------ the pieces

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static Vector3 Dir(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        // (radius, height) pairs.
        static Vector2[] P(params float[] xy)
        {
            var p = new Vector2[xy.Length / 2];
            for (int i = 0; i < p.Length; i++) p[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
            return p;
        }

        static Matrix4x4 M(Vector3 at) => Matrix4x4.Translate(at);

        static Matrix4x4 M(Vector3 at, Quaternion turn) => Matrix4x4.TRS(at, turn, Vector3.one);

        static Matrix4x4 M(Vector3 at, Quaternion turn, Vector3 scale) => Matrix4x4.TRS(at, turn, scale);

        sealed class Builder
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };

            int Add(Vector3 p, Vector3 n)
            {
                vertices.Add(p);
                normals.Add(n.sqrMagnitude > 1e-8f ? n.normalized : Vector3.up);
                return vertices.Count - 1;
            }

            // Two triangles a-b-c, a-c-d facing `outward` (Unity's front face is the one Cross(b - a, c - a) points to).
            void Quad(int part, int a, int b, int c, int d, Vector3 outward)
            {
                var list = triangles[part];
                var n = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(vertices[c] - vertices[a], vertices[d] - vertices[a]);
                if (Vector3.Dot(n, outward) >= 0f) { list.Add(a); list.Add(b); list.Add(c); list.Add(a); list.Add(c); list.Add(d); }
                else { list.Add(a); list.Add(c); list.Add(b); list.Add(a); list.Add(d); list.Add(c); }
            }

            // A profile of (radius, height) points turned round the local y axis. Each segment is its own band, its normal
            // square to it (crisp edges between bands, smooth round the axis); a band's outward side is to the right of the
            // profile walked from its first point to its last when the axis is on the left (top to bottom down the outside).
            public void Lathe(int part, Vector2[] profile, Matrix4x4 m, int sides)
            {
                var nm = m.inverse.transpose;
                for (int i = 1; i < profile.Length; i++)
                {
                    Vector2 a = profile[i - 1], c = profile[i], t = c - a;
                    if (t.sqrMagnitude < 1e-10f) continue;
                    var n2 = new Vector2(-t.y, t.x).normalized;   // (radius, height) of the band's normal
                    int first = vertices.Count;
                    for (int s = 0; s <= sides; s++)
                    {
                        float ang = 2f * Mathf.PI * s / sides, cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                        var normal = nm.MultiplyVector(new Vector3(n2.x * cos, n2.y, n2.x * sin));
                        Add(m.MultiplyPoint3x4(new Vector3(a.x * cos, a.y, a.x * sin)), normal);
                        Add(m.MultiplyPoint3x4(new Vector3(c.x * cos, c.y, c.x * sin)), normal);
                    }
                    for (int s = 0; s < sides; s++)
                    {
                        int i0 = first + 2 * s;
                        Quad(part, i0, i0 + 1, i0 + 3, i0 + 2, normals[i0]);
                    }
                }
            }

            // A ring of radius R round the local y axis, tube radius r.
            public void Torus(int part, float R, float r, Matrix4x4 m, int segments, int sides)
            {
                var nm = m.inverse.transpose;
                int first = vertices.Count;
                for (int u = 0; u <= segments; u++)
                {
                    float a = 2f * Mathf.PI * u / segments;
                    var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    for (int v = 0; v <= sides; v++)
                    {
                        float b = 2f * Mathf.PI * v / sides;
                        var n = radial * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                        Add(m.MultiplyPoint3x4(radial * R + n * r), nm.MultiplyVector(n));
                    }
                }
                int row = sides + 1;
                for (int u = 0; u < segments; u++)
                    for (int v = 0; v < sides; v++)
                    {
                        int a = first + u * row + v;
                        Quad(part, a, a + row, a + row + 1, a + 1, normals[a]);
                    }
            }

            // A tube of radius r along points (world: the mesh's frame).
            public void Tube(int part, Vector3[] points, float r, int sides)
            {
                if (points.Length < 2) return;
                int first = vertices.Count;
                Vector3 along = (points[1] - points[0]).normalized;
                Vector3 side = Vector3.Cross(along, Mathf.Abs(along.y) < .9f ? Vector3.up : Vector3.right).normalized;
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 next = i + 1 < points.Length ? points[i + 1] - points[i] : points[i] - points[i - 1];
                    Vector3 prev = i > 0 ? points[i] - points[i - 1] : next;
                    Vector3 t = (next.normalized + prev.normalized).normalized;
                    side = Vector3.Cross(t, Vector3.Cross(side, t)).normalized;   // carried along, square to the tube
                    Vector3 up = Vector3.Cross(t, side);
                    for (int s = 0; s <= sides; s++)
                    {
                        float a = 2f * Mathf.PI * s / sides;
                        var n = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                        Add(points[i] + n * r, n);
                    }
                }
                int row = sides + 1;
                for (int i = 0; i < points.Length - 1; i++)
                    for (int s = 0; s < sides; s++)
                    {
                        int a = first + i * row + s;
                        Quad(part, a, a + row, a + row + 1, a + 1, normals[a]);
                    }
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.subMeshCount = Parts;
                for (int i = 0; i < Parts; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
