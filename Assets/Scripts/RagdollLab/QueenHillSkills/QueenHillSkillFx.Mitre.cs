using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The bishop's mitre shell (10-12, 승규 님: the giant sword "다 맘에 안 든다", then from the three ideas "주교관(비숍 모자)
    /// 포탄: 비숍 모자 모양의 묵직한 은색 포탄을 쏴요. 날아가면서 모자의 갈라진 틈이 빛나요. 떨어질 때 모자가 X로 쪼개지며 바닥에
    /// 세게 박히고, 쪼개진 네 조각이 대각선 네 방향으로 쭉 미끄러져 X 팔을 만들어요", with its sound). The hit is the same X at
    /// the same moment (the shot's flight time).
    ///
    /// - The shell: a polished silver bishop's mitre made of four quarters round its axis, a hair apart, so its cross-shaped
    ///   cleft shows the gold light inside it.
    /// - Aiming: the mitre standing see-through at the spot with its light glowing inside, the dotted arc from the bishop's
    ///   hand to it (R141: polished gold beads; and the light X on the floor of 10-12, "좌클릭을 누르기 전 X 자로 가볍게",
    ///   is gone: "밑에 미리보기로 x 자로 표시된 부분 지워줘").
    /// - The shot: out of the hand base first along a high arc, spinning, its cleft glowing and pulsing, a silver trail with
    ///   a gold core behind it; it rights itself on the way down and lands base first.
    /// - The landing: it slams in, then splits along the cleft: the four quarters slide out along the four diagonals to the
    ///   X's ends, a hit stop, a big shake; they rock to a stop, stand a moment and go.
    ///
    /// R134 (승규 님 10-14: "실제 던졌을 때 오브젝트가 좀 미흡게 퀄리티 높여주고 실제로 바닥이 구멍이 뚤리는 듯한 느낌을 줘 칼로 x
    /// 자도 좀 더 사실 적으로 묘사 해주고", then "바닥이 뚫리는 느낌이 아니라 실제로 바닥이 파이는 연출이랑 x 자도 좀 더 사실 적으로"):
    /// - A real bishop's mitre: taller and pointed (it read as a round helmet), a gold collar and waist band on every
    ///   quarter, a violet gem set in gold on each, a gold ball on its point. In flight a violet glow round its silver trail,
    ///   glints falling off it, a little wobble in its spin.
    /// - It digs (QueenHillSkillFx.Dig.cs): it slams into a crater it breaks in the floor, chips and sparks flying; its
    ///   quarters are blades that plough the X into the floor, two real trenches cut behind them, sparks off their fronts and
    ///   chips thrown up out of the cuts; at the arms' ends they rock to a stop, stand a moment and sink into the floor. The
    ///   gold ball springs off as it splits. The ink gouges, the flat violet X bars, the inked rings and the round drops
    ///   are gone.
    ///
    /// R137 (승규 님 10-14, with a picture on the hill and one beside a wall: "언덕도 자연스럽게 파괴가 되게 해줘 벽도 마찬가지로
    /// 벽도 임팩트가 있게 파괴 되게끔"): the quarters follow the floor as they plough (off a step's edge they fall nose down in a
    /// curve and plough on below, the edge chipped where they went over; a face in their way they slam into and stick in), and
    /// what they and the crater break is broken out of the world itself (QueenHillSkillFx.Dig.cs, Skill Ground): the hill's
    /// edges and faces, a wall's foot.
    ///
    /// R154 (승규 님 10-22: a Tripo model of the mitre, "폴리곤 수 실사 처럼 높여주고 저 사진과 같이 광택과 때깔 나오게"): the
    /// solid mitre is drawn with that model (Resources/QueenHillSkillFx/MitreModel: subdivided to 240k triangles in Blender,
    /// cut into its four quarters and its ball, polished silver, gold and violet gem Standard materials). The quarters and
    /// the ball of the model ride on the procedural ones, so the flight, the split and the ploughing are as they were; the
    /// see-through aim and the white flashes still draw the procedural mitre.
    /// </summary>
    public partial class QueenHillSkillFx
    {
        /// <summary>The mitre's scale (it is 1 m tall at 1), the cleft between its quarters (m), its arc's height over the
        /// higher end (m).</summary>
        const float MitreSize = 0.85f, MitreGap = 0.07f, MitreArc = 1.4f, MitreHeight = 1f;
        /// <summary>After it lands (frames): when it splits, how long the quarters plough. Then (s) how long they stand and
        /// sink.</summary>
        const float SplitAt = 3f, SlideFrames = 10f, PieceStay = 0.5f, PieceSink = 0.35f;
        /// <summary>How solid its see-through self is while the bishop aims.</summary>
        const float AimGhost = 0.6f;
        static readonly Color GhostGrey = new Color(0.55f, 0.57f, 0.63f);
        /// <summary>The light inside it (bright enough to bloom).</summary>
        static readonly Palette MitreLight = new Palette
        {
            main = new Color(2.2f, 1.6f, 0.6f), light = new Color(2.6f, 2.2f, 1.3f), deep = new Color(1.6f, 0.95f, 0.3f), ink = new Color(1.6f, 0.95f, 0.3f),
        };
        static readonly Palette MitreGemA = new Palette("#9B5CF0", "#F0E2FF", "#4B2393", "#24242F");
        static readonly Palette MitreGoldA = new Palette("#FFD34D", "#FFF0A8", "#C7811A", "#3A2208");

        Mesh meshMitre, meshMitreLine, meshMitreGold, meshMitreGem;

        /// <summary>The mitre's outline turned round its axis: (height, radius) from its base up to its point — a rim, a
        /// waist, the swelling cap narrowing to a point (R134: taller and slimmer, a mitre's point rather than a dome).</summary>
        static readonly Vector2[] MitreProfile =
        {
            new Vector2(0f, 0.27f), new Vector2(0.025f, 0.29f), new Vector2(0.06f, 0.3f), new Vector2(0.1f, 0.295f),
            new Vector2(0.13f, 0.27f), new Vector2(0.16f, 0.255f), new Vector2(0.22f, 0.275f), new Vector2(0.32f, 0.3f),
            new Vector2(0.42f, 0.305f), new Vector2(0.52f, 0.29f), new Vector2(0.62f, 0.255f), new Vector2(0.72f, 0.205f),
            new Vector2(0.81f, 0.145f), new Vector2(0.88f, 0.09f), new Vector2(0.94f, 0.04f), new Vector2(0.98f, 0.012f),
            new Vector2(MitreHeight, 0f),
        };

        /// <summary>The mitre's radius at height <paramref name="h"/>.</summary>
        static float MitreRadius(float h)
        {
            var P = MitreProfile;
            for (int i = 1; i < P.Length; i++)
                if (h <= P[i].x) return Mathf.Lerp(P[i - 1].y, P[i].y, Mathf.InverseLerp(P[i - 1].x, P[i].x, h));
            return 0f;
        }

        static Vector3 QuarterDir(float deg) => new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad), 0f, Mathf.Cos(deg * Mathf.Deg2Rad));

        /// <summary>One quarter of the mitre (a 90° wedge round its axis, centred on +Z, the axis along +Y from its base):
        /// the outside smooth, the two cut faces and the base flat. With <paramref name="line"/> the same shape with every
        /// normal pointing out from the wedge's middle, for its ink line (no cracks at its edges).</summary>
        static Mesh MitreQuarter(bool line)
        {
            const int Seg = 8;
            var P = MitreProfile;
            int n = P.Length;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            void Tri(int a, int b, int c, Vector3 nn)
            {
                if (Vector3.Dot(Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]), nn) < 0f) { tris.Add(a); tris.Add(c); tris.Add(b); }
                else { tris.Add(a); tris.Add(b); tris.Add(c); }
            }

            // The outside.
            for (int i = 0; i < n; i++)
            {
                int i0 = Mathf.Max(0, i - 1), i1 = Mathf.Min(n - 1, i + 1);
                float dy = P[i1].x - P[i0].x, dr = P[i1].y - P[i0].y;
                var out2 = new Vector2(dy, -dr).normalized;   // (out from the axis, up)
                for (int j = 0; j <= Seg; j++)
                {
                    Vector3 d = QuarterDir(-45f + 90f * j / Seg);
                    verts.Add(d * P[i].y + Vector3.up * P[i].x);
                    normals.Add((d * out2.x + Vector3.up * out2.y).normalized);
                }
            }
            for (int i = 0; i < n - 1; i++)
                for (int j = 0; j < Seg; j++)
                {
                    int a = i * (Seg + 1) + j, b = a + 1, c = a + Seg + 2, d = a + Seg + 1;
                    Vector3 nn = normals[a] + normals[b] + normals[c] + normals[d];
                    Tri(a, b, c, nn);
                    Tri(a, c, d, nn);
                }
            // The base.
            int cb = verts.Count;
            verts.Add(Vector3.zero);
            normals.Add(Vector3.down);
            for (int j = 0; j <= Seg; j++)
            {
                verts.Add(QuarterDir(-45f + 90f * j / Seg) * P[0].y);
                normals.Add(Vector3.down);
            }
            for (int j = 0; j < Seg; j++) Tri(cb, cb + 1 + j, cb + 2 + j, Vector3.down);
            // The two cut faces (what the cleft shows, and the quarters' insides once they part).
            foreach (float side in new[] { -1f, 1f })
            {
                float deg = 45f * side;
                Vector3 d = QuarterDir(deg);
                Vector3 nn = new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0f, -Mathf.Sin(deg * Mathf.Deg2Rad)) * side;
                int s0 = verts.Count;
                for (int i = 0; i < n; i++)
                {
                    verts.Add(Vector3.up * P[i].x);
                    normals.Add(nn);
                    verts.Add(d * P[i].y + Vector3.up * P[i].x);
                    normals.Add(nn);
                }
                for (int i = 0; i < n - 1; i++)
                {
                    int a = s0 + i * 2;
                    Tri(a, a + 1, a + 3, nn);
                    Tri(a, a + 3, a + 2, nn);
                }
            }
            if (line)
            {
                Vector3 mid = Vector3.zero;
                foreach (var v in verts) mid += v;
                mid /= verts.Count;
                for (int i = 0; i < verts.Count; i++)
                {
                    Vector3 o = verts[i] - mid;
                    normals[i] = o.sqrMagnitude > 1e-8f ? o.normalized : Vector3.up;
                }
            }
            var m = new Mesh { name = line ? "Mitre quarter ink" : "Mitre quarter" };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>The height of a quarter's gem (its middle), on the quarter's middle line.</summary>
        const float MitreGemY = 0.27f;

        /// <summary>
        /// R134: one quarter's gold, in the quarter's own space (centred on +Z), kept a little in from its cut faces: a collar
        /// band standing proud round its foot, a thin band round its waist, and the plate its gem is set on.
        /// </summary>
        static Mesh MitreQuarterGold()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            // A quad wound to face out, its own normal (crisp edges).
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                int s = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                Vector3 nn = Vector3.Cross(b - a, c - a);
                bool ok = Vector3.Dot(nn, outward) > 0f;
                Vector3 nrm = (ok ? nn : -nn).normalized;
                for (int k = 0; k < 4; k++) normals.Add(nrm);
                if (ok) tris.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
                else tris.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
            }
            // A band from y0 to y1 standing out by proud, between two angles: its face smooth round (shared normals).
            void Band(float y0, float y1, float proud, float from, float to, int seg)
            {
                float r0 = MitreRadius(y0), r1 = MitreRadius(y1);
                int s = verts.Count;
                for (int j = 0; j <= seg; j++)
                {
                    Vector3 d = QuarterDir(Mathf.Lerp(from, to, j / (float)seg));
                    verts.Add(d * (r0 + proud) + Vector3.up * y0);
                    verts.Add(d * (r1 + proud) + Vector3.up * y1);
                    Vector3 nrm = (d * (y1 - y0) - Vector3.up * (r1 - r0)).normalized;
                    normals.Add(nrm);
                    normals.Add(nrm);
                }
                for (int j = 0; j < seg; j++)
                {
                    int a = s + j * 2, b = a + 1, c = a + 3, d = a + 2;
                    Vector3 o = QuarterDir(Mathf.Lerp(from, to, (j + 0.5f) / seg));
                    if (Vector3.Dot(Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]), o) > 0f) tris.AddRange(new[] { a, b, c, a, c, d });
                    else tris.AddRange(new[] { a, c, b, a, d, c });
                }
                for (int j = 0; j < seg; j++)
                {
                    Vector3 d0 = QuarterDir(Mathf.Lerp(from, to, j / (float)seg)), d1 = QuarterDir(Mathf.Lerp(from, to, (j + 1f) / seg));
                    Face(d0 * (r1 - 0.01f) + Vector3.up * y1, d1 * (r1 - 0.01f) + Vector3.up * y1, d1 * (r1 + proud) + Vector3.up * y1, d0 * (r1 + proud) + Vector3.up * y1, Vector3.up);
                    Face(d0 * (r0 - 0.01f) + Vector3.up * y0, d1 * (r0 - 0.01f) + Vector3.up * y0, d1 * (r0 + proud) + Vector3.up * y0, d0 * (r0 + proud) + Vector3.up * y0, Vector3.down);
                }
                foreach (var (deg, side) in new[] { (from, -1f), (to, 1f) })
                {
                    Vector3 d = QuarterDir(deg);
                    Vector3 tangent = new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0f, -Mathf.Sin(deg * Mathf.Deg2Rad)) * side;
                    Face(d * (r0 - 0.01f) + Vector3.up * y0, d * (r0 + proud) + Vector3.up * y0, d * (r1 + proud) + Vector3.up * y1, d * (r1 - 0.01f) + Vector3.up * y1, tangent);
                }
            }
            Band(0.03f, 0.12f, 0.016f, -41f, 41f, 12);
            Band(0.435f, 0.47f, 0.011f, -41f, 41f, 12);
            // The gem's setting: a gold hexagon plate on the quarter's middle, standing a little out of it.
            float r = MitreRadius(MitreGemY);
            Vector3 c = new Vector3(0f, MitreGemY, r);
            const float plate = 0.064f, back = -0.012f, front = 0.008f;
            for (int k = 0; k < 6; k++)
            {
                float a0 = k * 60f * Mathf.Deg2Rad, a1 = (k + 1) * 60f * Mathf.Deg2Rad;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * plate, p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * plate;
                Face(c + p0 + Vector3.forward * back, c + p1 + Vector3.forward * back, c + p1 + Vector3.forward * front, c + p0 + Vector3.forward * front, (p0 + p1).normalized);
                Face(c + Vector3.forward * front, c + p0 + Vector3.forward * front, c + p1 + Vector3.forward * front, c + Vector3.forward * front, Vector3.forward);
            }
            var m = new Mesh { name = "Mitre quarter gold" };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>R134: one quarter's gem, a cut violet stone on its gold plate (a six-sided point facing out, every facet
        /// flat so it glints), in the quarter's own space.</summary>
        static Mesh MitreQuarterGem()
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            float r = MitreRadius(MitreGemY);
            Vector3 c = new Vector3(0f, MitreGemY, r + 0.008f);
            Vector3 tip = c + Vector3.forward * 0.036f, table = c + Vector3.forward * 0.027f;
            const float girdle = 0.047f, crown = 0.023f;
            for (int k = 0; k < 6; k++)
            {
                float a0 = (k * 60f + 30f) * Mathf.Deg2Rad, a1 = ((k + 1) * 60f + 30f) * Mathf.Deg2Rad;
                Vector3 g0 = c + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0) * 1.25f, 0f) * girdle, g1 = c + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1) * 1.25f, 0f) * girdle;
                Vector3 t0 = table + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0) * 1.25f, 0f) * crown, t1 = table + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1) * 1.25f, 0f) * crown;
                // The crown's slanted facet and the table's slice (each flat).
                foreach (var tri in new[] { new[] { g0, g1, t1 }, new[] { g0, t1, t0 }, new[] { t0, t1, tip } })
                {
                    int s = verts.Count;
                    Vector3 nn = Vector3.Cross(tri[1] - tri[0], tri[2] - tri[0]);
                    Vector3 outward = (tri[0] + tri[1] + tri[2]) / 3f - (c - Vector3.forward * 0.02f);
                    bool ok = Vector3.Dot(nn, outward) > 0f;
                    verts.AddRange(tri);
                    Vector3 nrm = (ok ? nn : -nn).normalized;
                    normals.Add(nrm); normals.Add(nrm); normals.Add(nrm);
                    if (ok) tris.AddRange(new[] { s, s + 1, s + 2 });
                    else tris.AddRange(new[] { s, s + 2, s + 1 });
                }
            }
            var m = new Mesh { name = "Mitre quarter gem" };
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>The mitre: four silver quarters round its axis (each with its gold and its gem), the gold ball on its
        /// point and the light inside them.</summary>
        class Mitre
        {
            public readonly Prop[] quarters = new Prop[4];
            readonly Prop[] golds = new Prop[4], gems = new Prop[4];
            public readonly Prop ball;
            readonly Prop glow;
            /// <summary>The ball is placed by its owner (it springs off as the mitre splits), not with the mitre.</summary>
            public bool ballFree;

            /// <summary>R154: the model's quarters (each riding on its procedural quarter) and its ball (on the procedural
            /// ball), or nulls when the model is missing.</summary>
            readonly GameObject[] pieces = new GameObject[4];
            GameObject ballModel;

            /// <summary>The ball's middle sits this far (in its own size) below where the procedural ball goes: the model's
            /// ball is on its point, a little lower.</summary>
            const float ModelBallDrop = 0.9f;

            static GameObject model;
            static bool modelTried;

            static GameObject Model()
            {
                if (!modelTried)
                {
                    modelTried = true;
                    model = Resources.Load<GameObject>("QueenHillSkillFx/MitreModel/BishopMitre");
                }
                return model;
            }

            /// <summary>A copy of the model's part <paramref name="from"/> on <paramref name="on"/>, turned by
            /// <paramref name="turn"/>, hidden until Paint shows it.</summary>
            static GameObject Piece(MeshFilter from, Transform on, Quaternion turn, Vector3 at)
            {
                var go = new GameObject(from.name);
                go.transform.SetParent(on, false);
                go.transform.localPosition = at;
                go.transform.localRotation = turn;
                go.AddComponent<MeshFilter>().sharedMesh = from.sharedMesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = from.GetComponent<MeshRenderer>().sharedMaterials;
                go.SetActive(false);
                return go;
            }

            public Mitre(QueenHillSkillFx fx, Fx owner)
            {
                if (fx.meshMitre == null)
                {
                    fx.meshMitre = MitreQuarter(false);
                    fx.meshMitreLine = MitreQuarter(true);
                    fx.meshMitreGold = MitreQuarterGold();
                    fx.meshMitreGem = MitreQuarterGem();
                }
                var silver = fx.board ? BSilver : new Palette("#E6EAF2", "#FFFFFF", "#9AA3B5", "#24242F");
                var gold = fx.board ? BGold : MitreGoldA;
                var gem = fx.board ? BPal("#8E52E0", "#EADCFF", "#4A2389", 0.45f) : MitreGemA;
                for (int i = 0; i < 4; i++)
                {
                    quarters[i] = new Prop(fx, fx.meshMitre, silver, "Mitre quarter", null, fx.meshMitreLine) { inkWidth = 0.02f };
                    golds[i] = new Prop(fx, fx.meshMitreGold, gold, "Mitre gold") { inkWidth = 0.008f };
                    gems[i] = new Prop(fx, fx.meshMitreGem, gem, "Mitre gem") { inkWidth = 0.006f };
                    owner.props.Add(quarters[i]);
                    owner.props.Add(golds[i]);
                    owner.props.Add(gems[i]);
                }
                ball = new Prop(fx, fx.meshPuff, gold, "Mitre ball") { inkWidth = 0.01f };
                owner.props.Add(ball);
                glow = new Prop(fx, fx.meshPuff, MitreLight, "Mitre light") { inkWidth = 0f };
                owner.props.Add(glow);
                // R154: each model quarter turned so its middle faces its quarter's way (+Z), its base on the quarter's.
                var m = Model();
                if (m != null)
                {
                    int next = 0;
                    foreach (var mf in m.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf.name.Contains("Ball"))
                            ballModel = Piece(mf, ball.t, Quaternion.identity, Vector3.down * ModelBallDrop);
                        else if (next < 4)
                        {
                            Vector3 way = mf.sharedMesh.bounds.center;
                            way.y = 0f;
                            // (about the axis only: FromToRotation flips a piece facing straight back over sideways)
                            var turn = Quaternion.AngleAxis(Vector3.SignedAngle(way, Vector3.forward, Vector3.up), Vector3.up);
                            pieces[next] = Piece(mf, quarters[next].t, turn, Vector3.zero);
                            next++;
                        }
                    }
                    if (next < 4 || ballModel == null) Debug.LogWarning("[QueenHillSkillFx] The mitre model has " + next + " quarters and " + (ballModel != null ? "a" : "no") + " ball.");
                }
                Paint(0f, true, 0f, false, 0f, false);
            }

            /// <summary>Which way quarter <paramref name="i"/> faces, from the mitre's own forward (the diagonals).</summary>
            public static Quaternion Turn(int i) => Quaternion.Euler(0f, 45f + 90f * i, 0f);

            /// <summary>Whole: its base's middle at <paramref name="at"/>, its axis along <paramref name="rot"/>'s up, squashed
            /// to <paramref name="squash"/> of its height (and that much wider); its light <paramref name="light"/> of full size.</summary>
            public void Assemble(Vector3 at, Quaternion rot, float size, float squash, float light)
            {
                float wide = 1f + (1f - squash) * 0.6f;
                // Each quarter a little off its two seams: the cleft (half the gap off each seam, at 45° to its middle).
                float off = MitreGap * 0.7071f * size;
                for (int i = 0; i < 4; i++)
                {
                    Quaternion q = rot * Turn(i);
                    quarters[i].t.SetPositionAndRotation(at + q * Vector3.forward * off, q);
                    quarters[i].t.localScale = new Vector3(size * wide, size * squash, size * wide);
                }
                if (!ballFree)
                {
                    ball.t.SetPositionAndRotation(at + rot * Vector3.up * ((MitreHeight + 0.03f) * size * squash), rot);
                    ball.t.localScale = Vector3.one * (0.12f * size);
                }
                PlaceLight(at + rot * Vector3.up * (0.42f * size * squash), size * light);
            }

            public void PlaceLight(Vector3 at, float size)
            {
                glow.t.SetPositionAndRotation(at, Quaternion.identity);
                glow.t.localScale = Vector3.one * (0.6f * Mathf.Max(0f, size));   // fills the inside up to the cleft
            }

            /// <summary>The quarters (with their gold and gems, and the ball) solid (<paramref name="ghost"/> 0) or seen through
            /// at that much (grey unless <paramref name="valid"/>), white by <paramref name="flash"/>; the light on or off, white
            /// by <paramref name="lightFlash"/>. The gold and the gems go where their quarters are.</summary>
            public void Paint(float ghost, bool valid, float flash, bool on, float lightFlash, bool lightOn, bool ballOn = true)
            {
                for (int i = 0; i < 4; i++)
                {
                    var q = quarters[i].t;
                    foreach (var part in new[] { golds[i], gems[i] })
                    {
                        part.t.SetPositionAndRotation(q.position, q.rotation);
                        part.t.localScale = q.localScale;
                    }
                }
                foreach (var set in new[] { quarters, golds, gems })
                    foreach (var p in set) PaintPart(p, ghost, valid, flash, on);
                PaintPart(ball, ghost, valid, flash, on && ballOn);
                // R154: solid and not flashing white, the model is drawn in place of the procedural quarters and ball.
                bool real = pieces[3] != null && ghost <= 0f && flash < 0.5f;
                for (int i = 0; i < 4; i++)
                {
                    if (pieces[i] == null) continue;
                    pieces[i].SetActive(real && on);
                    if (real)
                    {
                        quarters[i].Show(false);
                        golds[i].Show(false);
                        gems[i].Show(false);
                    }
                }
                if (ballModel != null)
                {
                    ballModel.SetActive(real && on && ballOn);
                    if (real) ball.Show(false);
                }
                glow.flash = lightFlash;
                glow.Show(lightOn);
                glow.Apply();
            }

            static void PaintPart(Prop p, float ghost, bool valid, float flash, bool on)
            {
                p.ghost = ghost;
                p.ghostColor = valid ? Color.Lerp(p.colors.main, p.colors.light, 0.3f) : GhostGrey;
                p.flash = flash;
                p.Show(on);
                p.Apply();
            }
        }

        /// <summary>The aim's mitre (BishopAim, each frame): standing see-through at the spot, its light pulsing
        /// inside; grey and dark where the bishop cannot shoot. Returns its point (the dotted arc's end).</summary>
        Vector3 AimMitre(Mitre mitre, Vector3 to, Vector3 yaw, bool valid, float age)
        {
            Vector3 at = to + Vector3.up * (0.04f + 0.03f * Mathf.Sin(clock * 3f));
            float size = MitreSize * Pop(age, 1.08f);
            mitre.Assemble(at, Quaternion.LookRotation(yaw, Vector3.up), size, 1f, 0.85f + 0.12f * Mathf.Sin(clock * 6f));
            mitre.Paint(valid ? AimGhost : 0.35f, valid, 0f, true, 0f, valid);
            return at + Vector3.up * (MitreHeight * size);
        }

        void BishopThrow(QueenHillFxEvent e)
        {
            float time = Mathf.Max(0.1f, e.size), arm = S != null ? S.bishopArm : 1.6f;
            Vector3 yaw = FlatDir(e.dir, Vector3.forward);
            Vector3 from = e.at, c = FloorUnder(e.to, 0.6f);
            float top = Mathf.Max(from.y, c.y) + MitreArc;
            float slideEnd = (SplitAt + SlideFrames) * F;
            var f = Run(time + slideEnd + Mathf.Max(PieceStay + PieceSink, DigHold + DigHeal) + 0.1f, null);
            var mitre = new Mitre(this, f);
            // The trail: a silver ribbon with a gold core, and a soft violet glow round it (R134).
            var aura = new Strip(this, look.Bishop) { core = look.Bishop.light, coreShare = 0.25f, inkShare = 0f, opacity = 0.32f };
            var trail = new Strip(this, board ? BSilver : look.Bishop) { core = MitreLight.main, coreShare = 0.34f, inkShare = 0.18f };
            f.strips.Add(aura);
            f.strips.Add(trail);
            var trailPts = new List<Vector3>();
            var gold = board ? BGold : MitreGoldA;
            Quaternion upright = Quaternion.LookRotation(yaw, Vector3.up);
            Vector3 side = Vector3.Cross(Vector3.up, yaw);
            // The four ways the quarters plough (the diagonals).
            var dirs = new Vector3[4];
            for (int i = 0; i < 4; i++) dirs[i] = upright * Mitre.Turn(i) * Vector3.forward;
            var bladeAt = new Vector3[4];
            var pitch = new float[4];
            Dig dig = null;
            bool slammed = false, split = false, stopped = false;
            float glints = 0f, chipEvery = 0f;
            Vector3 ballAt = c, ballVel = Vector3.zero;
            Vector3 Path(float k)
            {
                k = Mathf.Clamp01(k);
                Vector3 p = Vector3.Lerp(from, c, k);
                p.y = Mathf.Lerp(from.y, c.y, k) + 4f * (top - Mathf.Max(from.y, c.y)) * k * (1f - k);
                return p;
            }
            f.step = (fx, dt) =>
            {
                float k = fx.age / time, since = fx.age - time, a = since / F;
                if (since < 0f)
                {
                    // In flight: base first along its arc, spinning (to a whole number of quarter turns, so it lands square to
                    // the X) with a little wobble, righting itself on the way down; its cleft glowing and pulsing; glints
                    // falling off it.
                    Vector3 p = Path(k);
                    Vector3 v = Path(k + 0.02f) - Path(k - 0.02f);
                    Vector3 axis = v.sqrMagnitude > 1e-8f ? -v.normalized : Vector3.up;
                    axis = Vector3.Slerp(axis, Vector3.up, Mathf.SmoothStep(0f, 1f, (k - 0.65f) / 0.35f)).normalized;
                    Quaternion wobble = Quaternion.AngleAxis(9f * Mathf.Sin(fx.age * 13f) * (1f - k * k), side);
                    Quaternion rot = wobble * Quaternion.FromToRotation(Vector3.up, axis) * Quaternion.AngleAxis(1440f * EaseOut(k), Vector3.up) * upright;
                    float scale = MitreSize * Mathf.Min(1f, 0.6f + fx.age / (4f * F) * 0.4f);
                    mitre.Assemble(p, rot, scale, 1f, 0.9f + 0.15f * Mathf.Sin(fx.age * 40f));
                    mitre.Paint(0f, true, fx.age < 2f * F ? 1f : 0f, true, 0f, true);
                    trailPts.Add(p + rot * Vector3.up * (0.55f * MitreHeight * scale));
                    while (trailPts.Count > 14) trailPts.RemoveAt(0);
                    if (trailPts.Count >= 2)
                    {
                        aura.Build(trailPts, j => 0.5f, Eye);
                        aura.taper = 1f;
                        aura.Apply();
                        trail.Build(trailPts, j => 0.2f, Eye);
                        trail.taper = 1f;
                        trail.Apply();
                    }
                    for (glints += dt * 26f; glints >= 1f; glints -= 1f)
                        Sparkle(p + Random.insideUnitSphere * 0.22f + Vector3.up * 0.3f, Random.value < 0.5f ? look.Bishop : gold);
                    return true;
                }
                // The trail goes within six frames of the landing.
                trail.fade = aura.fade = 1f - Mathf.Clamp01(since / (6f * F));
                trail.Apply();
                aura.Apply();
                if (dig == null)
                {
                    // R137: what it breaks is worked out as it lands (the blades' ways over the floor, the edges and faces round
                    // it) and broken out of the world itself (Skill Ground).
                    dig = NewDig(c, yaw, dirs, arm);
                    var mine = dig;
                    fx.end = () => EndDig(mine);
                }
                float after = since - slideEnd;            // since the quarters stopped
                float keep = slideEnd + DigHold - since;   // how long what breaks off now lies there
                dig.age = since;
                dig.heal = after > DigHold ? (after - DigHold) / DigHeal : 0f;
                dig.glow = !stopped ? 1f : 1f - Mathf.Clamp01(after / 0.35f);
                dig.grown = EaseOut(Mathf.Clamp01((a + 1f) / 3f));
                if (a < SplitAt)
                {
                    // Slammed into the crater it breaks: sunk, squashed, its light flaring; chips and sparks fly; a face beside
                    // it breaks (a wall big), an edge beside it crumbles.
                    if (!slammed)
                    {
                        slammed = true;
                        Chips(c + Vector3.up * 0.06f, Vector3.up, 12, 4.4f, 0.85f, Debris(c, Vector3.up), keep);
                        RimSlabs(dig, keep);
                        Sparks(c + Vector3.up * 0.12f, Vector3.up, 14, 2.6f);
                        Breakaway(dig, e.by, keep);
                    }
                    float squash = Mathf.Lerp(0.72f, 0.9f, a / SplitAt);
                    mitre.Assemble(c - Vector3.up * 0.15f, upright, MitreSize, squash, 1.25f);
                    mitre.Paint(0f, true, a < 2f ? 1f : 0f, true, 0f, true);   // (the light stays its own bright gold: white would dim it)
                    return true;
                }
                // Split: the quarters plough out along the diagonals like blades, digging the X as they go; the gold ball springs
                // off; the light bursts and goes.
                float u = Mathf.Clamp01((a - SplitAt) / SlideFrames), go = EaseOut(u);
                if (!split)
                {
                    split = true;
                    mitre.ballFree = true;
                    ballAt = mitre.ball.t.position;
                    ballVel = Vector3.up * 3.4f + yaw * 0.5f;
                }
                float sink = after < PieceStay ? 0f : Mathf.Clamp01((after - PieceStay) / PieceSink);
                chipEvery += dt * 30f;
                bool chipNow = chipEvery >= 1f;
                if (chipNow) chipEvery -= 1f;
                for (int i = 0; i < 4; i++)
                {
                    var way = dig.arms[i];
                    if (!stopped)
                    {
                        // On along its way (R137: over the floor as it goes: up a slope, off an edge and down, on along the
                        // floor below, until a face stops it), sunk into the trench it cuts while on the floor.
                        dig.reach[i] = Mathf.Min(Mathf.Max(arm, way.stop) * go, way.stop);   // (off an edge near its end it falls on past it)
                        Vector3 foot = FootAt(way, dig.reach[i], out bool inAir);
                        bladeAt[i] = foot - Vector3.up * (inAir ? 0.02f : 0.1f);
                        dig.blade[i] = foot - Vector3.up * 0.06f;
                        foreach (var bump in way.bumps)
                            if (!bump.done && dig.reach[i] >= bump.s - 1e-3f)
                            {
                                bump.done = true;
                                Bumped(dig, i, bump, e.by, keep);
                            }
                        if (u > 0f && u < 1f && !inAir && !way.struck)
                        {
                            Vector3 front = foot + dirs[i] * 0.22f + Vector3.up * 0.04f;
                            Sparks(front, dirs[i], 2);
                            if (chipNow)
                            {
                                float s = (i + (int)a) % 2 == 0 ? 1f : -1f;
                                Vector3 across = Vector3.Cross(Vector3.up, dirs[i]) * s;
                                Chips(front, Vector3.up + across * 0.8f + dirs[i] * 0.3f, 1, 3f, 0.4f, Debris(foot, Vector3.up), keep);
                            }
                        }
                        // Nose down as it falls off an edge, still tipped once it has landed below, leaning into a face it hit.
                        float want = inAir ? 38f : way.struck ? 30f : way.fell ? 24f : 0f;
                        pitch[i] = Mathf.MoveTowards(pitch[i], want, 900f * dt);
                    }
                    float lean = 22f * Mathf.Sin(u * Mathf.PI * 0.5f) + pitch[i];
                    if (after > 0f) lean += 7f * Mathf.Sin(after * 38f) * Mathf.Exp(-after * 7f);   // rocking to a stop
                    Quaternion tilt = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, dirs[i], lean / 90f));
                    var q = mitre.quarters[i];
                    // Standing a moment at the arm's end, then sinking into the floor.
                    q.t.SetPositionAndRotation(bladeAt[i] - Vector3.up * (0.62f * sink * sink * sink), tilt * upright * Mitre.Turn(i));
                    q.t.localScale = Vector3.one * MitreSize;
                }
                if (!stopped && u >= 1f)
                {
                    stopped = true;
                    for (int i = 0; i < 4; i++) PuffBurst(bladeAt[i] + dirs[i] * 0.15f + Vector3.up * 0.15f, look.Dust, 2, 0.35f, 0.18f, 0.4f);
                }
                // The gold ball springs off, tumbling, and goes.
                ballVel += Physics.gravity * dt;
                ballAt += ballVel * dt;
                float ballK = Mathf.Clamp01((since - SplitAt * F) / 0.5f);
                mitre.ball.t.SetPositionAndRotation(ballAt, Quaternion.AngleAxis(fx.age * 720f, side));
                mitre.ball.t.localScale = Vector3.one * (0.12f * MitreSize * (1f - ballK * ballK));
                float burst = Mathf.Clamp01((a - SplitAt) / 4f);
                mitre.PlaceLight(c + Vector3.up * (0.3f * MitreSize), MitreSize * Mathf.Lerp(1.6f, 0f, burst));
                mitre.Paint(0f, true, a < SplitAt + 2f ? 1f : 0f, sink < 0.999f, 0f, burst < 1f, ballK < 0.999f);
                return true;
            };
        }

        /// <summary>The landing (action at full): a white flash at its foot as it slams in, stone dust bursting off the floor
        /// and rolling out round it, a hit stop and a big shake. R134: the crater, the trenches, the chips and the sparks are the
        /// shell's own (BishopThrow, QueenHillSkillFx.Dig.cs); the flat violet X bars, the inked rings and the round drops went.</summary>
        void BishopImpact(QueenHillFxEvent e)
        {
            var bishop = e.by;
            Vector3 floor = FloorUnder(e.at, 0.6f), c = floor + Vector3.up * 0.026f;
            Vector3 yaw = FlatDir(e.dir, Vector3.forward);
            var glare = new Tile(this, "Mitre foot flash");
            var g = Run(5f * F, (fx, d) =>
            {
                float a = fx.age / F;
                glare.Floor(c + Vector3.up * 0.006f, yaw, Vector2.one * Mathf.Lerp(1.2f, 1.9f, a / 5f));
                FlatRing(glare);
                glare.shape = 2f;
                glare.fill = A(Color.white, 0.9f);
                glare.ink = glare.rim = glare.stripe = glare.core = Color.clear;
                glare.fade = a < 2f ? 1f : 1f - (a - 2f) / 3f;
                glare.Apply();
                return true;
            });
            g.tiles.Add(glare);
            var stoneDust = board ? BStone : new Palette("#B9B0C9", "#E8E2F2", "#6E6380", "#2A0F57");
            PuffBurst(c + Vector3.up * 0.25f, stoneDust, 5, 0.75f, 0.36f, 0.6f, 0.45f);
            DustRing(c, 10, 1.6f, look.Dust, 0.24f);
            HitStop(3);
            Shake(bishop, null, 0.22f, 12);
        }
    }
}
