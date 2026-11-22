using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The chess board under the ultimates (JY-skill-lab, 2026-11-22; the "!! Brilliant Move" spec §5): one board for every
    /// ultimate on every PC - the world's axes and the knight's 2.2 m squares (PawnRushSkillFx.UltContract.cs) - drawn in thin
    /// light on the floor like chess.com's own marks.
    ///
    ///   squares     a square's frame and fill (chess.com's from- and to-square highlight) with its coordinate: the from-square
    ///               is the caster's cell at the key, lit until the judgement; the to-square comes at the judgement
    ///   the unfold  at the key the board opens round the from-square ring by ring (real time: it opens at full speed while the
    ///               caster's own time floats); an enemy's square turns red as it is reached, with a tick
    ///   the window  while it drives, the squares round the caster: a faint grid, its neighbours lighting on a beat, its own
    ///               square bright, the squares it crosses fast written on the board behind it; folded at the judgement
    ///   stamps      checker patches that light ring by ring (landings, the judgement's ignition with its ember flicker)
    ///   lines, dots a piece's lines (the bishop's diagonals, the rook's file) and chess.com's legal-move dots (the queen's rays)
    ///   arrows      chess.com's analysis arrows on the floor, straight or the knight's L
    ///   rings       chess.com's capture ring round an enemy, the threat's '??', a king's check glow
    ///   the picker  the pawn's promotion card
    ///
    /// A patch is one dynamic mesh of quads (225 at most) with its colour and alpha per vertex; a square's floor is cast once
    /// per show (64 casts a frame at most), squares more than 1.5 m above or below their patch's floor and quads within 3 m of
    /// the camera are left out. Teal is the caster's move, red what it attacks; red draws after teal.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        Material matBrdFill, matBrdFillRed, matBrdFrameRed, matBrdLine, matBrdLineRed, matBrdDot, matBrdDotRed, matBrdRingRed, matBrdSoftRed, matBrdCard;
        Texture2D texBrdDisc;
        Font brdSymbols;
        readonly List<CellMesh> brdMeshes = new List<CellMesh>();
        readonly RaycastHit[] brdHits = new RaycastHit[8];
        int brdCastFrame = -1, brdCasts, brdLabels, brdThreatTags;
        const int BrdCastsPerFrame = 64;

        void BrdBuild()
        {
            if (matBrdFill != null) return;
            BuildUltParts();
            matBrdFill = Glow(Texture2D.whiteTexture, opacity: 1f);
            matBrdFillRed = Glow(Texture2D.whiteTexture, opacity: 1f);
            matBrdFillRed.renderQueue = matBrdFill.renderQueue + 10;   // (red draws after teal: the spec's caps, item 8)
            matBrdFrameRed = Glow(texFrame);
            matBrdFrameRed.renderQueue = matFrame.renderQueue + 10;
            matBrdLine = Glow(Texture2D.whiteTexture);
            matBrdLineRed = Glow(Texture2D.whiteTexture);
            matBrdLineRed.renderQueue = matBrdLine.renderQueue + 10;
            texBrdDisc = Tex(64, 64, TextureWrapMode.Clamp, (u, v) => White(Mathf.Clamp01((1f - Radius(u, v)) * 9f)));
            matBrdDot = Glow(texBrdDisc);
            matBrdDotRed = Glow(texBrdDisc);
            matBrdDotRed.renderQueue = matBrdDot.renderQueue + 10;
            matBrdRingRed = Glow(texRing);
            matBrdRingRed.renderQueue = matRing.renderQueue + 10;
            matBrdSoftRed = Glow(texSoft, opacity: 1f);
            matBrdSoftRed.renderQueue = matBrdSoftRed.renderQueue + 10;
            matBrdCard = Glow(Texture2D.whiteTexture, opacity: 1f);
        }

        static bool BrdRedish(Color c) => c.r > 0.6f && c.g < 0.45f;

        bool BrdLive(UltShow u) => u != null && !u.gone && ults.Contains(u);

        // ---------------------------------------------------------------- one patch: a mesh of quads

        /// <summary>A patch of quads on the floor drawn as one mesh (frames, fills or dots: its material), each quad's light and
        /// cover per vertex (HDR: SkillGlow multiplies the vertex colour and alpha in). Filled between Begin and End whenever its
        /// content changes; 225 quads at most; a quad within 3 m of the camera is left out.</summary>
        sealed class CellMesh
        {
            public const int MaxQuads = 225;
            readonly GameObject go;
            readonly Mesh mesh;
            readonly MeshRenderer mr;
            readonly List<Vector3> verts = new List<Vector3>(MaxQuads * 4);
            readonly List<Color> cols = new List<Color>(MaxQuads * 4);
            readonly List<Vector2> uvs = new List<Vector2>(MaxQuads * 4);
            readonly List<int> tris = new List<int>(MaxQuads * 6);
            Vector3 eye;
            bool hasEye;

            public CellMesh(Transform parent, string name, Material material)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
                mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                mr.enabled = false;
            }

            public bool Gone => go == null;

            public void Begin(Camera cam)
            {
                verts.Clear();
                cols.Clear();
                uvs.Clear();
                tris.Clear();
                hasEye = cam != null;
                if (hasEye) eye = cam.transform.position;
            }

            /// <summary>A flat square <paramref name="size"/> wide at <paramref name="centre"/>, turned <paramref name="turn"/>°
            /// about the up axis; light <paramref name="rgb"/> × cover <paramref name="alpha"/>.</summary>
            public bool Quad(Vector3 centre, float size, Color rgb, float alpha, float turn = 0f)
            {
                if (alpha <= 0.002f || size <= 0.001f || verts.Count >= MaxQuads * 4) return false;
                if (hasEye && (centre - eye).sqrMagnitude < 9f) return false;
                float h = size * 0.5f;
                Vector3 ax = new Vector3(h, 0f, 0f), az = new Vector3(0f, 0f, h);
                if (Mathf.Abs(turn) > 0.01f)
                {
                    var q = Quaternion.Euler(0f, turn, 0f);
                    ax = q * ax;
                    az = q * az;
                }
                return Corners(centre - ax - az, centre + ax - az, centre + ax + az, centre - ax + az, rgb, alpha, false);
            }

            /// <summary>A quad on four given corners (a strip's piece; a triangle when two are the same).</summary>
            public bool Corners(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color rgb, float alpha, bool nearCheck = true)
            {
                if (alpha <= 0.002f || verts.Count >= MaxQuads * 4) return false;
                if (nearCheck && hasEye && ((a + c) * 0.5f - eye).sqrMagnitude < 9f) return false;
                int i = verts.Count;
                verts.Add(a);
                verts.Add(b);
                verts.Add(c);
                verts.Add(d);
                var col = new Color(rgb.r, rgb.g, rgb.b, Mathf.Clamp01(alpha));
                cols.Add(col);
                cols.Add(col);
                cols.Add(col);
                cols.Add(col);
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(1f, 1f));
                uvs.Add(new Vector2(0f, 1f));
                tris.Add(i);
                tris.Add(i + 2);
                tris.Add(i + 1);
                tris.Add(i);
                tris.Add(i + 3);
                tris.Add(i + 2);
                return true;
            }

            public void End()
            {
                if (go == null) return;
                mesh.Clear();
                if (verts.Count == 0)
                {
                    mr.enabled = false;
                    return;
                }
                mesh.SetVertices(verts);
                mesh.SetColors(cols);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0, true);
                mr.enabled = true;
            }

            public void Hide()
            {
                if (mr != null) mr.enabled = false;
            }

            public void Destroy()
            {
                if (go != null) Object.Destroy(go);
                if (mesh != null) Object.Destroy(mesh);
            }
        }

        CellMesh BrdMesh(string name, Material material)
        {
            BrdBuild();
            var m = new CellMesh(root, name, material);
            brdMeshes.Add(m);
            return m;
        }

        void BrdKill(CellMesh m)
        {
            if (m == null) return;
            m.Destroy();
            brdMeshes.Remove(m);
        }

        /// <summary>A fill's vertex colour: light <paramref name="colour"/> × <paramref name="hdr"/> × <paramref name="fade"/>
        /// over <paramref name="paint"/> of what is behind (SkillGlow multiplies the light by the cover, so it is divided out).</summary>
        static void BrdFillColour(Color colour, float hdr, float paint, float fade, out Color rgb, out float alpha)
        {
            alpha = Mathf.Clamp01(paint * fade);
            float k = hdr / Mathf.Max(0.02f, paint);
            rgb = new Color(colour.r * k, colour.g * k, colour.b * k, 1f);
        }

        static Color BrdTimes(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        /// <summary>chess.com's pop of a square: 0.6 → 1.06 → 1.0 of its size over 0.12 s.</summary>
        static float BrdPop(float a) => a <= 0f ? 0f : a < 0.08f ? Mathf.Lerp(0.6f, 1.06f, a / 0.08f) : Mathf.Lerp(1.06f, 1f, Mathf.Clamp01((a - 0.08f) / 0.04f));

        static int BrdRing(Vector2Int d) => Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y));

        /// <summary>A stable 0..1 number for a square (its flicker's rate and phase).</summary>
        static float BrdHash(Vector2Int c, int salt)
        {
            uint h = (uint)(c.x * 73856093) ^ (uint)(c.y * 19349663) ^ (uint)(salt * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xffff) / 65535f;
        }

        // ---------------------------------------------------------------- the floor's height under the squares

        struct BrdFloor
        {
            public float y, from;
        }

        sealed class BrdShow
        {
            public readonly Dictionary<Vector2Int, BrdFloor> floors = new Dictionary<Vector2Int, BrdFloor>();
            public float refY;
            public bool winShow, winMade;
            public float foldAt = -1f;
        }

        BrdShow BrdOf(UltShow u)
        {
            if (u.brill is BrdShow s) return s;
            s = new BrdShow { refY = Ground(u.castAt).y };
            u.brill = s;
            return s;
        }

        bool BrdCastAllowed()
        {
            if (brdCastFrame != Time.frameCount)
            {
                brdCastFrame = Time.frameCount;
                brdCasts = 0;
            }
            return brdCasts++ < BrdCastsPerFrame;
        }

        /// <summary>The nearest floor hit straight down from <paramref name="from"/> within <paramref name="depth"/> m (pieces
        /// skipped), or NaN.</summary>
        float BrdCastDown(Vector3 from, float depth)
        {
            int n = Physics.RaycastNonAlloc(from, Vector3.down, brdHits, depth, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue, y = float.NaN;
            for (int i = 0; i < n; i++)
            {
                var h = brdHits[i];
                if (h.distance >= best || h.collider == null || h.collider.GetComponentInParent<RagdollPawn>() != null) continue;
                best = h.distance;
                y = h.point.y;
            }
            return y;
        }

        /// <summary>A square's floor near <paramref name="refY"/> (cast from 2 m over it, 4 m down), or NaN when there is none
        /// within 1.5 m of it.</summary>
        float BrdCastFloor(Vector2Int cell, float refY)
        {
            Vector3 p = CellFlat(cell);
            float y = BrdCastDown(new Vector3(p.x, refY + 2f, p.z), 4f);
            return float.IsNaN(y) || Mathf.Abs(y - refY) > 1.5f ? float.NaN : y;
        }

        /// <summary>The floor under a point (up to <paramref name="depth"/> m below it), or NaN.</summary>
        float BrdGroundBelow(Vector3 p, float depth = 40f) => BrdCastDown(p + Vector3.up * 0.3f, depth + 0.3f);

        /// <summary>A board square's floor in a show, near <paramref name="refY"/>: cached per square (cast again only when the
        /// patch's floor has moved a metre), a few casts a frame - a square not cast yet is NaN for a frame or two, and NaN too
        /// where there is no floor within 1.5 m.</summary>
        float BoardFloor(UltShow u, Vector2Int cell, float refY)
        {
            var s = BrdOf(u);
            if (s.floors.TryGetValue(cell, out var f) && Mathf.Abs(f.from - refY) < 1f) return f.y;
            if (!BrdCastAllowed()) return float.NaN;
            float y = BrdCastFloor(cell, refY);
            s.floors[cell] = new BrdFloor { y = y, from = refY };
            return y;
        }

        /// <summary>A board square's floor near the show's floor at its key.</summary>
        float BoardFloor(UltShow u, Vector2Int cell) => BoardFloor(u, cell, BrdOf(u).refY);

        /// <summary>The same at once (one cast, no budget) for a single square's mark; the reference height where none is found.</summary>
        float BoardFloorNow(UltShow u, Vector2Int cell, float refY)
        {
            if (u == null)
            {
                float y0 = BrdCastFloor(cell, refY);
                return float.IsNaN(y0) ? refY : y0;
            }
            var s = BrdOf(u);
            if (s.floors.TryGetValue(cell, out var f) && Mathf.Abs(f.from - refY) < 1f && !float.IsNaN(f.y)) return f.y;
            float y = BrdCastFloor(cell, refY);
            s.floors[cell] = new BrdFloor { y = y, from = refY };
            return float.IsNaN(y) ? refY : y;
        }

        /// <summary>The floor the caster stands over now (the show's floor at the key when it is too high to find one).</summary>
        float BrdCasterFloor(UltShow u)
        {
            var s = BrdOf(u);
            if (u.by == null) return s.refY;
            float y = BrdGroundBelow(u.by.Hips.position, 40f);
            return float.IsNaN(y) ? s.refY : y;
        }

        // ---------------------------------------------------------------- squares

        /// <summary>How much of a mark shows at an age: in over 0.04 s, out over the last 0.3 s of its life or over 0.2 s
        /// once alive() says no.</summary>
        sealed class BrdFade
        {
            readonly float life;
            readonly Func<bool> alive;
            float end = -1f;

            public BrdFade(float life, Func<bool> alive)
            {
                this.life = life;
                this.alive = alive;
            }

            public float K(float age)
            {
                if (alive != null && end < 0f && !alive()) end = age;
                float k = Mathf.Clamp01(age / 0.04f);
                if (end >= 0f) k *= 1f - Mathf.Clamp01((age - end) / 0.2f);
                if (life < 1e8f) k *= Mathf.Clamp01((life - age) / 0.3f);
                return k;
            }

            public bool Over(float age) => age >= life || (end >= 0f && age - end > 0.2f);
        }

        /// <summary>
        /// A square marked like chess.com's (the spec's N1 + N2 + N4): its fill (<paramref name="fill"/> ×
        /// <paramref name="fillHdr"/> over <paramref name="paint"/> of the floor), its frame (<paramref name="frame"/> ×
        /// <paramref name="frameHdr"/>), its coordinate in its corner (<paramref name="label"/>, or none), popping in. For
        /// <paramref name="life"/> s or while <paramref name="alive"/> says so. Red marks draw after teal. Returns the frame.
        /// </summary>
        Shape SquareMark(UltShow u, Vector2Int c, Color fill, float fillHdr, float paint, Color frame, float frameHdr, string label, float life,
                         Func<bool> alive = null, float floorY = float.NaN)
        {
            BrdBuild();
            float refY = u != null ? BrdCasterFloor(u) : 0f;
            float y = !float.IsNaN(floorY) ? floorY : BoardFloorNow(u, c, refY);
            Vector3 centre = CellFlat(c);
            centre.y = y;
            float size = CellSize;
            var fade = new BrdFade(life, alive);
            bool red = BrdRedish(fill) || BrdRedish(frame);
            if (paint > 0f && fillHdr > 0f)
                Add(new Shape(root, "Ult square fill", meshQuad, red ? matBrdFillRed : matBrdFill)
                {
                    life = life, at = centre + Vector3.up * 0.035f, rotation = FlatOnFloor(),
                    animate = (s, t) =>
                    {
                        float k = fade.K(s.Age);
                        if (fade.Over(s.Age)) s.life = 0f;
                        float p = BrdPop(s.Age) * size * 0.96f;
                        s.scale = new Vector3(p, p, 1f);
                        s.color = HdrA(fill, fillHdr);
                        s.bright = k;
                        s.paint = paint * k;
                    },
                });
            var shape = Add(new Shape(root, "Ult square frame", meshQuad, red ? matBrdFrameRed : matFrame)
            {
                life = life, at = centre + Vector3.up * 0.045f, rotation = FlatOnFloor(),
                animate = (s, t) =>
                {
                    float k = fade.K(s.Age);
                    if (fade.Over(s.Age)) s.life = 0f;
                    float p = BrdPop(s.Age) * size * 0.96f;
                    s.scale = new Vector3(p, p, 1f);
                    s.color = HdrA(frame, frameHdr);
                    s.bright = k;
                },
            });
            if (!string.IsNullOrEmpty(label)) BrdLabel(centre, label, life, fade);
            return shape;
        }

        /// <summary>A square's coordinate (the spec's N4): 0.32 m white letters in its far-left corner as this camera sees it,
        /// at most 12 alive.</summary>
        void BrdLabel(Vector3 centre, string label, float life, BrdFade fade)
        {
            if (brdLabels >= 12 || font == null) return;
            brdLabels++;
            var words = new Text3D(root, font, label, new Color(1.1f, 1.1f, 1.1f, 0.8f), 0.32f);
            float size = CellSize;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (fade.Over(age) || age >= life) return false;
                var cam = ViewCamera;
                Vector3 fwd = cam != null ? Flat(cam.transform.forward) : Vector3.forward;
                fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                words.Place(centre + (fwd - right) * (size * 0.32f) + Vector3.up * 0.12f, cam, 1f, fade.K(age));
                return true;
            }, () =>
            {
                words.Destroy();
                brdLabels = Mathf.Max(0, brdLabels - 1);
            }));
        }

        /// <summary>The caster's cell at the key, lit like chess.com's from-square with its coordinate until the judgement: its
        /// frame TealPale ×3.2 and its fill Teal ×1.15 over 0.42 of the floor, the frame down to ×1.3 and the fill to 40 % after
        /// 2 s.</summary>
        void FromSquare(UltShow u)
        {
            if (u == null || u.by == null) return;
            BrdBuild();
            var s = BrdOf(u);
            Vector2Int cell = u.fromCell;
            Vector3 centre = CellFlat(cell);
            centre.y = BoardFloorNow(u, cell, s.refY);
            float size = CellSize, t0 = RealClock;
            var fade = new BrdFade(1e9f, () => BrdLive(u) && u.hitAt < 0f);
            float Later() => Smooth01((RealClock - t0 - 2f) / 0.5f);
            Add(new Shape(root, "Ult from-square fill", meshQuad, matBrdFill)
            {
                life = 1e9f, at = centre + Vector3.up * 0.035f, rotation = FlatOnFloor(),
                animate = (sh, t) =>
                {
                    float k = fade.K(sh.Age);
                    if (fade.Over(sh.Age)) sh.life = 0f;
                    float p = BrdPop(sh.Age) * size * 0.96f, later = Mathf.Lerp(1f, 0.4f, Later());
                    sh.scale = new Vector3(p, p, 1f);
                    sh.color = HdrA(Teal, 1.15f);
                    sh.bright = k * later;
                    sh.paint = 0.42f * k * later;
                },
            });
            Add(new Shape(root, "Ult from-square frame", meshQuad, matFrame)
            {
                life = 1e9f, at = centre + Vector3.up * 0.045f, rotation = FlatOnFloor(),
                animate = (sh, t) =>
                {
                    float k = fade.K(sh.Age);
                    if (fade.Over(sh.Age)) sh.life = 0f;
                    float p = BrdPop(sh.Age) * size * 0.96f;
                    sh.scale = new Vector3(p, p, 1f);
                    sh.color = HdrA(TealPale, Mathf.Lerp(3.2f, 1.3f, Later()));
                    sh.bright = k;
                },
            });
            BrdLabel(centre, UltCoord(u, cell), 1e9f, fade);
        }

        /// <summary>The judgement's to-square for <paramref name="life"/> s: teal like chess.com's (frame TealPale ×3.2, fill
        /// Teal ×1.15 over 0.42) for a good move, miss grey for a bad one; with <paramref name="drain"/> (?!) the teal drains to
        /// grey over 0.4 s.</summary>
        void ToSquare(UltShow u, Vector2Int cell, bool good, float life = 3f, bool drain = false)
        {
            if (u == null) return;
            BrdBuild();
            Vector3 centre = CellFlat(cell);
            centre.y = BoardFloorNow(u, cell, BrdCasterFloor(u));
            float size = CellSize;
            var fade = new BrdFade(life, null);
            Color Tone(float age, Color teal) => good && !drain ? teal : !good && !drain ? MissGreyTone : Color.Lerp(teal, MissGreyTone, Smooth01(age / 0.4f));
            Add(new Shape(root, "Ult to-square fill", meshQuad, matBrdFill)
            {
                life = life, at = centre + Vector3.up * 0.035f, rotation = FlatOnFloor(),
                animate = (sh, t) =>
                {
                    float k = fade.K(sh.Age), p = BrdPop(sh.Age) * size * 0.96f;
                    sh.scale = new Vector3(p, p, 1f);
                    sh.color = HdrA(Tone(sh.Age, Teal), 1.15f);
                    sh.bright = k;
                    sh.paint = 0.42f * k;
                },
            });
            Add(new Shape(root, "Ult to-square frame", meshQuad, matFrame)
            {
                life = life, at = centre + Vector3.up * 0.045f, rotation = FlatOnFloor(),
                animate = (sh, t) =>
                {
                    float k = fade.K(sh.Age), p = BrdPop(sh.Age) * size * 0.96f;
                    sh.scale = new Vector3(p, p, 1f);
                    sh.color = HdrA(Tone(sh.Age, TealPale), good && !drain ? 3.2f : 1.6f);
                    sh.bright = k;
                },
            });
            BrdLabel(centre, UltCoord(u, cell), life, fade);
        }

        static readonly Color MissGreyTone = new Color(0.6f, 0.6f, 0.62f);

        // ---------------------------------------------------------------- rings on the pieces

        /// <summary>chess.com's capture ring round an enemy (the spec's N5): a red ring on the floor 1.9 m across pulsing ±15 % at
        /// 3 Hz, following it; for a threat also the red ring over its head with a small '??' (at most four).</summary>
        void CaptureRing(RagdollPawn e, float life, bool threat = false)
        {
            if (e == null) return;
            BrdBuild();
            float phase = Random.value * Mathf.PI * 2f, floorY = Ground(e.Hips.position - Vector3.up * e.standHeight).y, castAt = -1f;
            Add(new Shape(root, "Ult capture ring", meshQuad, matBrdRingRed)
            {
                life = life, rotation = FlatOnFloor(),
                animate = (s, t) =>
                {
                    if (e == null) { s.bright = 0f; return; }
                    if (s.Age - castAt > 0.1f)
                    {
                        castAt = s.Age;
                        float y = BrdGroundBelow(e.Hips.position, 6f);
                        if (!float.IsNaN(y)) floorY = y;
                    }
                    Vector3 hips = e.Hips.position;
                    s.at = new Vector3(hips.x, floorY + 0.06f, hips.z);
                    float r = 1.9f / 0.86f * (1f + 0.15f * Mathf.Sin(s.Age * Mathf.PI * 6f + phase));   // (the texture's ring sits at 0.86)
                    s.scale = new Vector3(r, r, 1f);
                    s.color = HdrA(Blood, 2.8f);
                    s.bright = Mathf.Clamp01(s.Age / 0.06f) * Mathf.Clamp01((life - s.Age) / 0.25f);
                },
            });
            if (!threat) return;
            Add(new Shape(root, "Ult threat ring", meshQuad, matBrdRingRed)
            {
                life = life, billboard = true,
                animate = (s, t) =>
                {
                    if (e == null) { s.bright = 0f; return; }
                    s.at = HeadOf(e) + Vector3.up * 0.55f;
                    s.scale = new Vector3(0.6f, 0.6f, 1f);
                    s.color = HdrA(Blood, 2.4f);
                    s.bright = Mathf.Clamp01(s.Age / 0.06f) * Mathf.Clamp01((life - s.Age) / 0.25f);
                },
            });
            if (brdThreatTags >= 4 || font == null) return;
            brdThreatTags++;
            var tag = new Text3D(root, font, "??", HdrA(Blood, 2f), 0.22f);
            anims.Add(new Ongoing((age, dt) =>
            {
                if (age >= life || e == null) return false;
                tag.Place(HeadOf(e) + Vector3.up * 1.05f, ViewCamera, 1f, Mathf.Clamp01(age / 0.06f) * Mathf.Clamp01((life - age) / 0.25f));
                return true;
            }, () =>
            {
                tag.Destroy();
                brdThreatTags = Mathf.Max(0, brdThreatTags - 1);
            }));
        }

        /// <summary>A king struck: a red glow over its square for 0.8 s (the spec's N6).</summary>
        void CheckGlow(RagdollPawn king)
        {
            if (king == null) return;
            BrdBuild();
            Vector2Int c = Cell(king.Hips.position);
            Vector3 centre = CellFlat(c);
            float y = BrdGroundBelow(king.Hips.position, 6f);
            centre.y = float.IsNaN(y) ? king.Hips.position.y - king.standHeight : y;
            float size = CellSize * 1.3f;
            Add(new Shape(root, "Ult check glow", meshQuad, matBrdSoftRed)
            {
                life = 0.8f, at = centre + Vector3.up * 0.04f, rotation = FlatOnFloor(), scale = new Vector3(size, size, 1f),
                animate = (s, t) =>
                {
                    float k = Mathf.Clamp01(s.Age / 0.05f) * (1f - Smooth01((t - 0.5f) / 0.5f));
                    s.color = HdrA(Blood, 1.15f);
                    s.bright = k;
                    s.paint = 0.5f * k;
                },
            });
        }

        // ---------------------------------------------------------------- the key's unfold

        /// <summary>
        /// The key's unfold on every screen (real time): the board opens round the from-square in Chebyshev rings 1-7 (out to
        /// 15.4 m), ring i at 0.06 + 0.07·i s, its frames popping at ×2.2; rings 1-4 settle to the window's ×0.55 and give way to
        /// it from 1.0 s, rings 5-7 fade over 0.6 s. An enemy's square turns red as its ring reaches it, with a tick (0.45, its own
        /// screen 0.6; the pitch rising 1.0 → 1.5; six at most).
        /// </summary>
        void BoardUnfold(UltShow u)
        {
            if (u == null || u.by == null) return;
            BrdBuild();
            var s = BrdOf(u);
            var frames = BrdMesh("Ult unfold", matFrame);
            var redFrames = BrdMesh("Ult unfold red", matBrdFrameRed);
            var redFills = BrdMesh("Ult unfold red fill", matBrdFillRed);
            var red = new HashSet<Vector2Int>();
            Vector2Int from = u.fromCell;
            float refY = s.refY, size = CellSize;
            int reached = 0, ticks = 0, cast = 0;
            var caster = u.by;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (!BrdLive(u) || age > 1.35f) return false;
                // The rings reached by now; an enemy's square on the ring just reached turns red.
                while (reached < 7 && age >= 0.06f + 0.07f * (reached + 1))
                {
                    reached++;
                    var me = Viewer;
                    foreach (var p in RagdollPawn.All)
                    {
                        if (p == null || p == caster || caster == null || !Teams.AreEnemies(p.Team, caster.Team)) continue;
                        Vector2Int pc = Cell(p.Hips.position);
                        if (BrdRing(pc - from) != reached || !red.Add(pc)) continue;
                        if (ticks >= 6) continue;
                        float level = (me != null && me == p ? 0.6f : 0.45f) * (me != null && me == caster ? 1f : UltHeard(p.Hips.position));
                        UltClip(UltClipId.Tick, level, 1f + 0.1f * ticks);
                        ticks++;
                    }
                }
                // The floors of the rings to come (a ring or so a frame).
                while (cast < 7 && cast <= reached + 1)
                {
                    int ring = cast + 1;
                    bool done = true;
                    for (int x = -ring; x <= ring && done; x++)
                        for (int z = -ring; z <= ring; z++)
                        {
                            if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != ring) continue;
                            if (float.IsNaN(BoardFloor(u, from + new Vector2Int(x, z), refY)) && !s.floors.ContainsKey(from + new Vector2Int(x, z)))
                            {
                                done = false;
                                break;
                            }
                        }
                    if (!done) break;
                    cast++;
                }
                var cam = ViewCamera;
                frames.Begin(cam);
                redFrames.Begin(cam);
                redFills.Begin(cam);
                float handOver = 1f - Smooth01((age - 1f) / 0.3f);   // (the window takes over)
                for (int ring = 1; ring <= reached; ring++)
                {
                    float a = age - (0.06f + 0.07f * ring), pop = BrdPop(a);
                    float hdr = Mathf.Lerp(2.2f, 0.55f, Smooth01((a - 0.1f) / 0.25f));
                    float k = ring <= 4 ? handOver : 1f - Smooth01((a - 0.1f) / 0.6f);
                    if (k <= 0.002f) continue;
                    for (int x = -ring; x <= ring; x++)
                        for (int z = -ring; z <= ring; z++)
                        {
                            if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != ring) continue;
                            var c = from + new Vector2Int(x, z);
                            if (!s.floors.TryGetValue(c, out var f) || float.IsNaN(f.y)) continue;
                            Vector3 centre = CellFlat(c);
                            centre.y = f.y;
                            if (red.Contains(c))
                            {
                                BrdFillColour(Blood, 0.9f, 0.30f, k, out var rgb, out var alpha);
                                redFills.Quad(centre + Vector3.up * 0.035f, size * 0.96f * pop, rgb, alpha);
                                redFrames.Quad(centre + Vector3.up * 0.05f, size * 0.96f * pop, BrdTimes(Blood, 2.4f), k);
                            }
                            else frames.Quad(centre + Vector3.up * 0.05f, size * 0.96f * pop, BrdTimes(Teal, hdr), k);
                        }
                }
                frames.End();
                redFrames.End();
                redFills.End();
                return true;
            }, () =>
            {
                BrdKill(frames);
                BrdKill(redFrames);
                BrdKill(redFills);
            }) { tick = UltTick.Real });
        }

        // ---------------------------------------------------------------- the window while it drives

        /// <summary>Every frame while a show drives (<paramref name="show"/> false for the queen in the air: she has her rays):
        /// the window of squares round the caster (its own anim, made on the first call).</summary>
        void BoardWindowStep(UltShow u, Camera cam, bool show)
        {
            if (u == null || u.by == null) return;
            var s = BrdOf(u);
            s.winShow = show;
            if (!s.winMade) BrdWindowMake(u, s);
        }

        /// <summary>The judgement: the window folds from the outside in over 0.6 s.</summary>
        void BoardWindowFold(UltShow u)
        {
            if (u == null) return;
            var s = BrdOf(u);
            if (s.foldAt < 0f) s.foldAt = RealClock;
        }

        /// <summary>
        /// The window (§7, every screen): the squares within Chebyshev 4 of the caster's (81 at most) fading to its edge; the
        /// grid ×0.55, the 8 neighbours ×1.3 lighting on a 0.5 s beat, the caster's square ×2.0 (a 0.08 s cross-fade when it
        /// moves on); above 8 m/s every square it crosses lights TealPale ×2.8 → ×0.8 over 0.5 s and stays 2.5 s (the path
        /// written on the board). On an enemy's own screen the teal near it is down to 60 % (its warnings come first). From the
        /// end of the key (0.9 s) until the judgement folds it.
        /// </summary>
        void BrdWindowMake(UltShow u, BrdShow s)
        {
            s.winMade = true;
            var mesh = BrdMesh("Ult window", matFrame);
            var trail = new List<(Vector2Int cell, float y, float at)>();
            float k = 0f, changedAt = -10f, speed = 0f, lastSampleAt = RealClock;
            Vector2Int cell = u.fromCell, prev = u.fromCell;
            Vector3 lastPos = u.by != null ? u.by.Hips.position : u.castAt;
            float refY = s.refY, size = CellSize;
            anims.Add(new Ongoing((age, dt) =>
            {
                bool live = BrdLive(u) && u.by != null;
                float now = RealClock, since = now - u.t0Real, fold = s.foldAt >= 0f ? now - s.foldAt : -1f;
                bool want = live && s.winShow && since >= 0.9f;
                if (fold < 0f) k = Mathf.MoveTowards(k, want ? 1f : 0f, dt / 0.3f);
                if (!live || fold >= 0.6f) return false;
                if (k <= 0.001f && fold < 0f)
                {
                    mesh.Hide();
                    return true;
                }
                // Where the caster is, how fast it goes (from its picture, so a puppet on another PC counts too).
                Vector3 hips = u.by.Hips.position;
                if (now - lastSampleAt >= 0.05f)
                {
                    float step = Flat(hips - lastPos).magnitude / Mathf.Max(1e-3f, now - lastSampleAt);
                    speed = Mathf.Lerp(speed, step / Mathf.Max(0.05f, Time.timeScale > 0f ? Time.timeScale : 1f), 0.5f);
                    lastPos = hips;
                    lastSampleAt = now;
                }
                float floor = BrdGroundBelow(hips, 40f);
                if (!float.IsNaN(floor)) refY = floor;
                Vector2Int here = Cell(hips);
                if (here != cell)
                {
                    prev = cell;
                    cell = here;
                    changedAt = now;
                    if (speed > 8f)
                    {
                        float py = BoardFloor(u, prev, refY);
                        if (!float.IsNaN(py)) trail.Add((prev, py, now));
                        if (trail.Count > 40) trail.RemoveAt(0);
                    }
                }
                for (int i = trail.Count - 1; i >= 0; i--) if (now - trail[i].at > 2.8f) trail.RemoveAt(i);
                var cam = ViewCamera;
                var me = Viewer;
                bool enemyScreen = me != null && me != u.by && Teams.AreEnemies(me.Team, u.by.Team);
                Vector3 mePos = enemyScreen ? me.Hips.position : Vector3.zero;
                float beat = Mathf.Exp(-Mathf.Repeat(since, 0.5f) / 0.12f), cross = Mathf.Clamp01((now - changedAt) / 0.08f);
                mesh.Begin(cam);
                for (int dx = -4; dx <= 4; dx++)
                    for (int dz = -4; dz <= 4; dz++)
                    {
                        int d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz));
                        // The fold: from the outside in (ring 4 goes first, the caster's own square last).
                        float foldK = fold < 0f ? 1f : 1f - Smooth01((fold - 0.6f * (4 - d) / 5f) / 0.12f);
                        if (foldK <= 0.002f) continue;
                        var c = cell + new Vector2Int(dx, dz);
                        float fy = BoardFloor(u, c, refY);
                        if (float.IsNaN(fy)) continue;
                        float edge = d <= 2 ? 1f : d == 3 ? 0.6f : 0.25f;
                        float hdr = d == 0 ? Mathf.Lerp(1.3f, 2f, cross) : d == 1 ? 1.3f * (0.85f + 0.3f * beat) : 0.55f;
                        if (c == prev && d == 1) hdr = Mathf.Max(hdr, Mathf.Lerp(2f, 1.3f, cross));
                        Vector3 centre = CellFlat(c);
                        centre.y = fy;
                        if (enemyScreen && (Flat(centre - mePos)).sqrMagnitude < 64f) hdr *= 0.6f;
                        mesh.Quad(centre + Vector3.up * 0.05f, size * 0.96f, BrdTimes(Teal, hdr), k * edge * foldK);
                    }
                foreach (var (pc, py, at) in trail)
                {
                    float a = now - at, fade = 1f - Smooth01((a - 2.5f) / 0.3f);
                    if (BrdRing(pc - cell) <= 4 || fade <= 0f) continue;   // (inside the window it is drawn there)
                    Vector3 centre = CellFlat(pc);
                    centre.y = py;
                    mesh.Quad(centre + Vector3.up * 0.05f, size * 0.96f, BrdTimes(TealPale, Mathf.Lerp(2.8f, 0.8f, Smooth01(a / 0.5f))), k * fade);
                }
                mesh.End();
                return true;
            }, () =>
            {
                BrdKill(mesh);
                s.winMade = false;
            }) { tick = UltTick.Real });
        }

        // ---------------------------------------------------------------- stamps, lines and dots

        /// <summary>
        /// A checker patch round <paramref name="at"/> (the spec's BoardStamp: N3 + N1): one more Chebyshev ring every
        /// <paramref name="ringDelay"/> s out to <paramref name="rings"/> (13×13 is 6), the frames <paramref name="tint"/> from
        /// ×<paramref name="frameHdr"/> down to ×0.6 over its life, the dark squares filled - <paramref name="tint"/> ×
        /// <paramref name="fillHdr"/> over 0.30 of the floor, or N3's near-black over 0.34 when <paramref name="fillHdr"/> is 0 -
        /// fading from the outside in. With <paramref name="ember"/> every frame flickers ×1.4-2.0 at 7-11 Hz, each its own (the
        /// burning board, in the brilliant colour). On the show clock.
        /// </summary>
        void BoardStamp(Vector3 at, int rings, float ringDelay, float life, Color tint, float frameHdr, float fillHdr, bool ember = false)
        {
            BrdBuild();
            rings = Mathf.Clamp(rings, 0, 7);
            bool red = BrdRedish(tint);
            var frames = BrdMesh("Ult stamp", red ? matBrdFrameRed : matFrame);
            var fills = BrdMesh("Ult stamp fill", red ? matBrdFillRed : matBrdFill);
            var floors = new Dictionary<Vector2Int, float>();
            Vector2Int centreCell = Cell(at);
            float refY = at.y, size = CellSize;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (age >= life) return false;
                var cam = ViewCamera;
                frames.Begin(cam);
                fills.Begin(cam);
                float t = age / life;
                for (int r = 0; r <= rings; r++)
                {
                    float a = age - r * ringDelay;
                    if (a < 0f) break;
                    // Fading from the outside in: the outer ring is gone at 65 % of its life, the middle at its end.
                    float end = life * (1f - 0.35f * r / Mathf.Max(1, rings));
                    float k = Mathf.Clamp01(a / 0.05f) * Mathf.Clamp01((end - age) / Mathf.Max(0.05f, life * 0.25f));
                    if (k <= 0.002f) continue;
                    float pop = BrdPop(a);
                    for (int x = -r; x <= r; x++)
                        for (int z = -r; z <= r; z++)
                        {
                            if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != r) continue;
                            var c = centreCell + new Vector2Int(x, z);
                            if (!floors.TryGetValue(c, out float fy))
                            {
                                if (!BrdCastAllowed()) continue;
                                fy = BrdCastFloor(c, refY);
                                floors[c] = fy;
                            }
                            if (float.IsNaN(fy)) continue;
                            Vector3 centre = CellFlat(c);
                            centre.y = fy;
                            float hdr = Mathf.Lerp(frameHdr, 0.6f, t);
                            if (ember) hdr *= Mathf.Lerp(1.4f, 2f, 0.5f + 0.5f * Mathf.Sin(age * Mathf.PI * 2f * (7f + 4f * BrdHash(c, 1)) + BrdHash(c, 2) * 6.28f)) / 1.7f;
                            frames.Quad(centre + Vector3.up * 0.05f, size * 0.96f * pop, BrdTimes(tint, hdr), k);
                            if (!IsDark(c)) continue;
                            Color rgb;
                            float alpha;
                            if (fillHdr > 0f) BrdFillColour(tint, fillHdr, 0.30f, k, out rgb, out alpha);
                            else BrdFillColour(new Color(0.015f, 0.02f, 0.025f), 1f, 0.34f, k, out rgb, out alpha);
                            fills.Quad(centre + Vector3.up * 0.035f, size * 0.96f * pop, rgb, alpha);
                        }
                }
                frames.End();
                fills.End();
                return true;
            }, () =>
            {
                BrdKill(frames);
                BrdKill(fills);
            }) { tick = UltTick.Show });
        }

        /// <summary>A piece's faint lines from a square (the bishop's 4 diagonals, the rook's file and rank): frames along each
        /// direction for <paramref name="length"/> squares, in over 0.1 s, out over the last 0.5 s of <paramref name="life"/>.</summary>
        void BoardLines(UltShow u, Vector2Int from, Vector2Int[] dirs, int length, Color colour, float hdr, float life)
        {
            if (u == null || dirs == null) return;
            BrdBuild();
            var mesh = BrdMesh("Ult board lines", BrdRedish(colour) ? matBrdFrameRed : matFrame);
            float refY = BrdCasterFloor(u), size = CellSize;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (age >= life || !BrdLive(u)) return false;
                float k = Mathf.Clamp01(age / 0.1f) * Mathf.Clamp01((life - age) / 0.5f);
                mesh.Begin(ViewCamera);
                foreach (var d in dirs)
                    for (int i = 1; i <= length; i++)
                    {
                        var c = from + d * i;
                        float fy = BoardFloor(u, c, refY);
                        if (float.IsNaN(fy)) continue;
                        Vector3 centre = CellFlat(c);
                        centre.y = fy;
                        // A line's far squares a little fainter, so it reads as going out from the piece.
                        mesh.Quad(centre + Vector3.up * 0.05f, size * 0.96f, BrdTimes(colour, hdr * Mathf.Lerp(1f, 0.6f, (i - 1f) / Mathf.Max(1, length - 1))), k);
                    }
                mesh.End();
                return true;
            }, () => BrdKill(mesh)) { tick = UltTick.Show });
        }

        /// <summary>chess.com's legal-move dots (0.5 m discs) along lines from a square that may move (the flying queen's rays
        /// from the square under her): <paramref name="count"/> along each direction, while <paramref name="alive"/> says so.</summary>
        void BoardDots(UltShow u, Func<Vector2Int> from, Vector2Int[] dirs, int count, Color colour, float hdr, Func<bool> alive)
        {
            if (u == null || dirs == null || from == null) return;
            BrdBuild();
            var mesh = BrdMesh("Ult board dots", BrdRedish(colour) ? matBrdDotRed : matBrdDot);
            float k = 0f;
            anims.Add(new Ongoing((age, dt) =>
            {
                bool on = BrdLive(u) && (alive == null || alive());
                k = Mathf.MoveTowards(k, on ? 1f : 0f, dt / 0.15f);
                if (!on && k <= 0f) return false;
                float refY = BrdCasterFloor(u);
                Vector2Int c0 = from();
                mesh.Begin(ViewCamera);
                foreach (var d in dirs)
                    for (int i = 1; i <= count; i++)
                    {
                        var c = c0 + d * i;
                        float fy = BoardFloor(u, c, refY);
                        if (float.IsNaN(fy)) continue;
                        Vector3 centre = CellFlat(c);
                        centre.y = fy + 0.06f;
                        mesh.Quad(centre, 0.5f, BrdTimes(colour, hdr), k);
                    }
                mesh.End();
                return true;
            }, () => BrdKill(mesh)));
        }

        // ---------------------------------------------------------------- chess.com's arrows on the floor

        /// <summary>
        /// chess.com's analysis arrow on the floor (the spec's N7): a shaft and a head 0.06 m over the floor (sampled every 2 m),
        /// a bright 0.07 m outline ×2.6 and a faint fill (0.18 of the floor, ×0.8), straight or a knight's L along the world axes
        /// (its long leg first), drawn on over 0.25 s and following its ends while they move. The summary arrow of a judgement
        /// is bigger (a 1.2 m shaft, a 3 × 2.6 m head).
        /// </summary>
        sealed class FloorArrow : Anim
        {
            readonly PawnRushSkillFx fx;
            readonly Func<Vector3> from, to;
            readonly Func<bool> alive;
            readonly Color colour;
            readonly float shaft, headW, headL, life;
            readonly bool knightL;
            readonly CellMesh fill, edge;
            readonly List<Vector3> path = new List<Vector3>(64);
            readonly List<float> along = new List<float>(64);
            Vector3 builtFrom, builtTo;
            bool built;
            float end = -1f;

            public FloorArrow(PawnRushSkillFx fx, Func<Vector3> from, Func<Vector3> to, Color colour, float shaft, bool knightL, float life,
                              Func<bool> alive, bool summary)
            {
                this.fx = fx;
                this.from = from;
                this.to = to;
                this.colour = colour;
                this.shaft = shaft;
                this.knightL = knightL;
                this.life = life;
                this.alive = alive;
                headW = summary ? 3f : 1.8f;
                headL = summary ? 2.6f : 1.6f;
                bool red = BrdRedish(colour);
                fill = fx.BrdMesh("Ult arrow fill", red ? fx.matBrdFillRed : fx.matBrdFill);
                edge = fx.BrdMesh("Ult arrow edge", red ? fx.matBrdLineRed : fx.matBrdLine);
                tick = UltTick.Show;
            }

            /// <summary>The path's points every 2 m (and at the L's corner) on the floor, 0.06 m over it.</summary>
            void Build(Vector3 a, Vector3 b)
            {
                builtFrom = a;
                builtTo = b;
                built = true;
                path.Clear();
                along.Clear();
                Vector3 fa = Flat(a), fb = Flat(b), corner = fb;
                bool turn = false;
                if (knightL)
                {
                    Vector3 d = fb - fa;
                    turn = Mathf.Abs(d.x) > 0.3f && Mathf.Abs(d.z) > 0.3f;
                    corner = Mathf.Abs(d.x) >= Mathf.Abs(d.z) ? new Vector3(fb.x, 0f, fa.z) : new Vector3(fa.x, 0f, fb.z);
                }
                float y = a.y, s = 0f;
                void Point(Vector3 flat)
                {
                    float g = fx.BrdCastDown(new Vector3(flat.x, y + 2f, flat.z), 4.5f);
                    if (!float.IsNaN(g)) y = g;
                    if (path.Count > 0) s += Flat(flat - path[path.Count - 1]).magnitude;
                    path.Add(new Vector3(flat.x, y + 0.06f, flat.z));
                    along.Add(s);
                }
                void Leg(Vector3 p, Vector3 q, bool first)
                {
                    float len = Flat(q - p).magnitude;
                    int n = Mathf.Max(1, Mathf.CeilToInt(len / 2f));
                    for (int i = first ? 0 : 1; i <= n && path.Count < 60; i++) Point(Vector3.Lerp(p, q, i / (float)n));
                }
                if (turn)
                {
                    Leg(fa, corner, true);
                    Leg(corner, fb, false);
                }
                else Leg(fa, fb, true);
            }

            /// <summary>The point at a distance along the path, and the way it runs there.</summary>
            void At(float s, out Vector3 p, out Vector3 dir)
            {
                int i = 1;
                while (i < path.Count - 1 && along[i] < s) i++;
                Vector3 a = path[i - 1], b = path[i];
                float seg = Mathf.Max(1e-4f, along[i] - along[i - 1]);
                p = Vector3.Lerp(a, b, Mathf.Clamp01((s - along[i - 1]) / seg));
                dir = Flat(b - a);
                dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            }

            static Vector3 Side(Vector3 dir) => new Vector3(dir.z, 0f, -dir.x);

            void Line(Vector3 p, Vector3 q, Color rgb, float alpha)
            {
                Vector3 d = Flat(q - p);
                if (d.sqrMagnitude < 1e-6f) return;
                Vector3 m = Side(d.normalized) * 0.035f;
                edge.Corners(p - m, q - m, q + m, p + m, rgb, alpha);
            }

            public override bool Step(float dt, Camera cam)
            {
                age += dt;
                if (end < 0f && ((alive != null && !alive()) || age >= life)) end = age;
                if (end >= 0f && age - end > 0.2f) return false;
                Vector3 a = from(), b = to();
                if (!built || (a - builtFrom).sqrMagnitude > 0.09f || (b - builtTo).sqrMagnitude > 0.09f) Build(a, b);
                if (path.Count < 2)
                {
                    fill.Hide();
                    edge.Hide();
                    return true;
                }
                float k = Mathf.Clamp01(age / 0.05f) * (end >= 0f ? 1f - Mathf.Clamp01((age - end) / 0.2f) : 1f);
                float total = along[along.Count - 1], shown = total * Smooth01(age / 0.25f);
                float head = Mathf.Min(headL, shown * 0.6f), shaftEnd = Mathf.Max(0f, shown - head), w = shaft * 0.5f;
                BrdFillColour(colour, 0.8f, 0.18f, k, out var fillRgb, out var fillAlpha);
                Color line = BrdTimes(colour, 2.6f);
                fill.Begin(cam);
                edge.Begin(cam);
                // The shaft: one piece between each pair of points before the head's base; at the L's corner the two legs meet
                // on a mitre (the sum of the two sides' offsets), so the pieces neither overlap nor leave a gap.
                Vector3 prevL = default, prevR = default;
                bool hasPrev = false;
                for (int i = 0; i < path.Count && along[i] < shaftEnd; i++)
                {
                    Vector3 p = path[i], off;
                    Vector3 before = i > 0 ? Flat(path[i] - path[i - 1]) : Vector3.zero, after = i + 1 < path.Count ? Flat(path[i + 1] - path[i]) : Vector3.zero;
                    if (before.sqrMagnitude > 1e-6f && after.sqrMagnitude > 1e-6f && Vector3.Dot(before.normalized, after.normalized) < 0.5f)
                        off = (Side(before.normalized) + Side(after.normalized)) * w;
                    else
                    {
                        Vector3 d = after.sqrMagnitude > 1e-6f ? after : before;
                        off = Side(d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward) * w;
                    }
                    Vector3 L = p + off, R = p - off;
                    if (!hasPrev) Line(R, L, line, k);   // (the tail's cap)
                    else
                    {
                        fill.Corners(prevL, L, R, prevR, fillRgb, fillAlpha);
                        Line(prevL, L, line, k);
                        Line(prevR, R, line, k);
                    }
                    prevL = L;
                    prevR = R;
                    hasPrev = true;
                }
                // Up to the head's base, then the head.
                At(shaftEnd, out var bp, out var bdir);
                Vector3 bside = Side(bdir), BL = bp + bside * w, BR = bp - bside * w;
                if (!hasPrev) Line(BR, BL, line, k);
                else if ((BL - prevL).sqrMagnitude > 1e-4f)
                {
                    fill.Corners(prevL, BL, BR, prevR, fillRgb, fillAlpha);
                    Line(prevL, BL, line, k);
                    Line(prevR, BR, line, k);
                }
                if (head > 0.05f)
                {
                    At(shown, out var tip, out _);
                    Vector3 HL = bp + bside * (headW * 0.5f), HR = bp - bside * (headW * 0.5f);
                    fill.Corners(HL, tip, tip, HR, fillRgb, fillAlpha);
                    Line(BL, HL, line, k);
                    Line(BR, HR, line, k);
                    Line(HL, tip, line, k);
                    Line(HR, tip, line, k);
                }
                fill.End();
                edge.End();
                return true;
            }

            public override void Destroy()
            {
                fx.BrdKill(fill);
                fx.BrdKill(edge);
            }
        }

        /// <summary>A floor arrow (the spec's N7) from <paramref name="from"/> to <paramref name="to"/> (followed while they move),
        /// <paramref name="shaft"/> m wide, straight or a knight's L, for <paramref name="life"/> s or while
        /// <paramref name="alive"/> says so; <paramref name="summary"/> for the judgement's one arrow of the whole ultimate. Teal
        /// for the caster's side, Blood on the other side's screens: the caller's colour.</summary>
        FloorArrow UltArrow(Func<Vector3> from, Func<Vector3> to, Color colour, float shaft, bool knightL, float life, Func<bool> alive = null, bool summary = false)
        {
            if (from == null || to == null) return null;
            BrdBuild();
            var arrow = new FloorArrow(this, from, to, colour, shaft, knightL, life, alive, summary);
            anims.Add(arrow);
            return arrow;
        }

        // ---------------------------------------------------------------- the pawn's promotion picker

        /// <summary>
        /// chess.com's promotion picker over the landing square (every screen, on the show clock, so it unfolds slowly for the
        /// caster): a card 1.6 × 6.2 m centred 3.4 m up, off-white, no glow, with ♛ ♞ ♜ ♝ in 1.5 m cells; it unfolds in 0.08 s; at
        /// 0.20 s the ♛ cell fills with the brilliant colour and its piece turns white (the promote sound); at 0.28 s the others
        /// fade; from 0.30 s the ♛ swells ×1 → ×2.5 and is gone into the growing giant.
        /// </summary>
        void PromotionPicker(UltShow u, Vector3 square)
        {
            BrdBuild();
            if (brdSymbols == null) brdSymbols = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Symbol", "Arial Unicode MS", "DejaVu Sans" }, 64);
            Vector3 middle = square + Vector3.up * 3.4f;
            string[] glyphs = { "♛", "♞", "♜", "♝" };
            Vector3 CellAt(int i, Camera cam)
            {
                Vector3 up = cam != null ? cam.transform.up : Vector3.up;
                return middle + up * (2.25f - 1.5f * i);   // (the four cells top to bottom, 1.5 m apart)
            }
            float Unfold(float a) => Smooth01(a / 0.08f);
            float Others(float a) => 1f - Smooth01((a - 0.28f) / 0.12f);
            float CardK(float a) => Unfold(a) * (1f - Smooth01((a - 0.3f) / 0.3f));
            Add(new Shape(root, "Ult promotion card", meshQuad, matBrdCard)
            {
                tick = UltTick.Show, life = 0.75f, billboard = true,
                animate = (s, t) =>
                {
                    float a = s.Age, k = CardK(a);
                    s.at = middle;
                    s.scale = new Vector3(1.6f, 6.2f * Mathf.Max(0.02f, Unfold(a)), 1f);
                    s.color = new Color(0.92f, 0.92f, 0.92f, 1f);
                    s.bright = k;
                    s.paint = 0.9f * k;
                },
            });
            Add(new Shape(root, "Ult promotion queen cell", meshQuad, matBrdCard)
            {
                tick = UltTick.Show, life = 0.75f, billboard = true,
                animate = (s, t) =>
                {
                    float a = s.Age, k = Smooth01((a - 0.2f) / 0.04f) * (1f - Smooth01((a - 0.45f) / 0.25f));
                    var cam = ViewCamera;
                    s.at = CellAt(0, cam) - (cam != null ? cam.transform.forward : Vector3.zero) * 0.01f;
                    s.scale = new Vector3(1.5f, 1.5f, 1f);
                    s.color = HdrA(GradeColors[0], 1f);
                    s.bright = k;
                    s.paint = 0.95f * k;
                },
            });
            var dark = new Text3D[4];
            for (int i = 0; i < 4; i++) dark[i] = new Text3D(root, brdSymbols, glyphs[i], new Color(0.06f, 0.06f, 0.07f, 1f), 1.1f);
            var queenWhite = new Text3D(root, brdSymbols, glyphs[0], Color.white, 1.1f);
            bool promoted = false;
            anims.Add(new Ongoing((age, dt) =>
            {
                if (age > 0.75f) return false;
                var cam = ViewCamera;
                float unfold = Unfold(age), swell = 1f + 1.5f * Smooth01((age - 0.3f) / 0.3f);
                for (int i = 0; i < 4; i++)
                {
                    float k = unfold * (i == 0 ? 1f - Smooth01((age - 0.2f) / 0.04f) : Others(age));
                    dark[i].Place(CellAt(i, cam) - (cam != null ? cam.transform.forward : Vector3.zero) * 0.02f, cam, 1f, k);
                }
                float q = Smooth01((age - 0.2f) / 0.04f) * (1f - Smooth01((age - 0.45f) / 0.3f));
                queenWhite.Place(CellAt(0, cam) - (cam != null ? cam.transform.forward : Vector3.zero) * 0.03f, cam, swell, q);
                if (!promoted && age >= 0.2f)
                {
                    promoted = true;
                    UltClip(UltClipId.Promote, 0.9f * (u != null ? UltAudience(u, square) : 1f));
                }
                return true;
            }, () =>
            {
                foreach (var d in dark) d.Destroy();
                queenWhite.Destroy();
            }) { tick = UltTick.Show });
        }

        // ---------------------------------------------------------------- the end

        void DestroyBrilliant()
        {
            foreach (var m in brdMeshes) m?.Destroy();
            brdMeshes.Clear();
            if (texBrdDisc != null) Destroy(texBrdDisc);
            if (brdSymbols != null) Destroy(brdSymbols);
            brdLabels = brdThreatTags = 0;
        }
    }
}
