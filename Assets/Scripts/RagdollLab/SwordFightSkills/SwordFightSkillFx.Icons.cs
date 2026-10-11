using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The piece's mark over a caster's head (R102, 승규 님 10-10: "스킬을 사용할 때마다 머리 위에 작게 색 별로 위에 사진
    /// 표시 같은게 떴으면 좋겠어 (폰 제외)"): the five icons 승규 님 sent (king, queen, rook, bishop, knight; navy on cream),
    /// traced into outlines (the tool's output below: each contour its depth, then x, y pairs in thousandths of the icon's
    /// height, x from its middle, y up from its foot; depth 0 an outline, 1 a light line cut out of it, 2 dark again inside
    /// that), turned into two distance fields when first needed and drawn by <c>ChessFight/Skill Icon</c> as a sticker in
    /// the piece's colour: its light lines, an ink edge, a cream rim. It pops up over the head with every cast (white for
    /// two frames), bobs, and pops away after a second; the king's mark pops again when his guard takes a cut.
    /// R108 (승규 님: "이거 3D로 넣어줘야지, 전체 다시 만들어줘"): every mark is a solid token now — the same print on a
    /// front and a back face, a wall round its outline in the piece's darker colour (<c>ChessFight/Skill Icon 3D</c>) —
    /// that spins one turn as it pops in and then sways, so it reads as a thick toy cut-out over the head.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        /// <summary>The mark's height in metres (the icon itself; its rim and ink go round it).</summary>
        const float MarkHeight = 0.3f;
        const float MarkLife = 1.2f;
        /// <summary>The fields cover the icon's middle ± <c>IconHalf</c> icon heights (room for its rim and ink), and say distances
        /// up to ± <c>IconSpread</c> (more than the ink and the rim together, or the rim fills the whole square).</summary>
        const float IconHalf = 0.68f, IconSpread = 0.12f;
        const int IconSize = 128;
        /// <summary>The token's thickness and its faces' ink edge, in icon heights (R108).</summary>
        const float IconDepth = 0.2f, IconInk = 0.05f;

        readonly Dictionary<PieceKind, Texture2D> iconTextures = new Dictionary<PieceKind, Texture2D>();
        readonly Dictionary<PieceKind, Mesh> iconMeshes = new Dictionary<PieceKind, Mesh>();
        readonly Dictionary<RagdollPawn, HeadMarkState> headMarks = new Dictionary<RagdollPawn, HeadMarkState>();
        Material iconMat;
        int iconsBuilt;

        static readonly PieceKind[] IconKinds = { PieceKind.King, PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight };

        class HeadMarkState
        {
            public float born, until;
            public int pops;
            public Kit.Fx fx;
        }

        /// <summary>The marks are made one a frame from the start (each takes a few milliseconds), or at once when needed.</summary>
        void BuildIconsSlowly()
        {
            if (iconsBuilt < IconKinds.Length) IconTexture(IconKinds[iconsBuilt++]);
        }

        Material IconMaterial()
        {
            if (iconMat != null) return iconMat;
            var shader = Shader.Find("ChessFight/Skill Icon 3D");
            if (shader == null) shader = Resources.Load<Shader>("SwordFightSkillFx/SkillIcon3D");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            iconMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return iconMat;
        }

        Texture2D IconTexture(PieceKind kind)
        {
            if (iconTextures.TryGetValue(kind, out var tex) && tex != null) return tex;
            short[][] data = kind switch
            {
                PieceKind.King => KingIcon,
                PieceKind.Queen => QueenIcon,
                PieceKind.Rook => RookIcon,
                PieceKind.Bishop => BishopIcon,
                PieceKind.Knight => KnightIcon,
                _ => null,
            };
            if (data == null) return null;
            tex = BuildIcon(data, out var outline);
            tex.name = $"Skill icon ({kind})";
            iconTextures[kind] = tex;
            var mesh = BuildIconMesh(outline);
            mesh.name = $"Skill icon token ({kind})";
            iconMeshes[kind] = mesh;
            return tex;
        }

        /// <summary>The icon as a solid token (R108), in icon heights with its middle at the origin: a face at each end
        /// (z = ∓<see cref="IconDepth"/>/2, the field square, mirrored on the back so it reads from behind) and a wall
        /// where the faces' ink edge ends — the outline's distance field at −<see cref="IconInk"/>, followed cell by cell
        /// (marching squares), so the walls stay clean in the outline's notches. uv2.x is 1 on the walls. The light lines
        /// stay printed on the faces.</summary>
        static Mesh BuildIconMesh(float[] field)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var uv2 = new List<Vector2>();
            var tri = new List<int>();
            float h = IconDepth * 0.5f;
            for (int face = 0; face < 2; face++)
            {
                float z = face == 0 ? -h : h;
                int b = v.Count;
                for (int k = 0; k < 4; k++)
                {
                    float u = k == 1 || k == 2 ? 1f : 0f, w = k >= 2 ? 1f : 0f;
                    v.Add(new Vector3(-IconHalf + 2f * IconHalf * u, -IconHalf + 2f * IconHalf * w, z));
                    n.Add(new Vector3(0f, 0f, face == 0 ? -1f : 1f));
                    uv.Add(new Vector2(face == 0 ? u : 1f - u, w));
                    uv2.Add(Vector2.zero);
                }
                tri.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            float step = 2f * IconHalf / IconSize;
            Vector2 At(int x, int y) => new Vector2(-IconHalf + (x + 0.5f) * step, -IconHalf + (y + 0.5f) * step);
            float F(int x, int y) => field[y * IconSize + x] + IconInk;   // > 0 inside the ink edge
            void Wall(Vector2 a, Vector2 b2, Vector2 outward)
            {
                var o = (Vector3)outward;
                var e = new Vector2(b2.y - a.y, a.x - b2.x);
                if (Vector2.Dot(e, outward) < 0f) (a, b2) = (b2, a);   // a steady turn, though Cull is off
                int s = v.Count;
                v.Add(new Vector3(a.x, a.y, -h)); v.Add(new Vector3(b2.x, b2.y, -h));
                v.Add(new Vector3(b2.x, b2.y, h)); v.Add(new Vector3(a.x, a.y, h));
                for (int k = 0; k < 4; k++) { n.Add(o); uv.Add(Vector2.zero); uv2.Add(Vector2.right); }
                tri.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }
            var cross = new List<Vector2>(4);
            for (int y = 0; y < IconSize - 1; y++)
                for (int x = 0; x < IconSize - 1; x++)
                {
                    float f00 = F(x, y), f10 = F(x + 1, y), f11 = F(x + 1, y + 1), f01 = F(x, y + 1);
                    bool i00 = f00 > 0f, i10 = f10 > 0f, i11 = f11 > 0f, i01 = f01 > 0f;
                    if (i00 == i10 && i10 == i11 && i11 == i01) continue;
                    // The crossings round the cell's edges, in order: bottom, right, top, left.
                    cross.Clear();
                    Vector2 p00 = At(x, y), p10 = At(x + 1, y), p11 = At(x + 1, y + 1), p01 = At(x, y + 1);
                    void Edge(bool ia, bool ib, float fa, float fb, Vector2 pa, Vector2 pb)
                    {
                        if (ia != ib) cross.Add(Vector2.Lerp(pa, pb, fa / (fa - fb)));
                    }
                    Edge(i00, i10, f00, f10, p00, p10);
                    Edge(i10, i11, f10, f11, p10, p11);
                    Edge(i11, i01, f11, f01, p11, p01);
                    Edge(i01, i00, f01, f00, p01, p00);
                    // Outward = down the field.
                    var grad = new Vector2(f10 + f11 - f00 - f01, f01 + f11 - f00 - f10);
                    var outward = grad.sqrMagnitude > 1e-12f ? -grad.normalized : Vector2.up;
                    if (cross.Count == 2) Wall(cross[0], cross[1], outward);
                    else if (cross.Count == 4)
                    {
                        // A saddle: the middle decides which corners join.
                        bool mid = f00 + f10 + f11 + f01 > 0f;
                        if (mid == i00) { Wall(cross[0], cross[1], outward); Wall(cross[2], cross[3], outward); }
                        else { Wall(cross[3], cross[0], outward); Wall(cross[1], cross[2], outward); }
                    }
                }
            var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, uv2);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The traced icon as two signed distance fields (r: inside its outlines, g: inside its dark shape, by
        /// even-odd over every contour), ±<see cref="IconSpread"/> icon heights mapped to 0..1; and the outline's field
        /// unrounded, in icon heights, for the token's walls (R108).</summary>
        static Texture2D BuildIcon(short[][] data, out float[] outlineField)
        {
            var polys = new List<Vector2[]>();
            var depth = new List<int>();
            var boxes = new List<Rect>();
            foreach (var c in data)
            {
                var pts = new Vector2[(c.Length - 1) / 2];
                Vector2 lo = Vector2.one * float.MaxValue, hi = Vector2.one * float.MinValue;
                for (int i = 0; i < pts.Length; i++)
                {
                    pts[i] = new Vector2(c[1 + 2 * i], c[2 + 2 * i]) * 0.001f;
                    lo = Vector2.Min(lo, pts[i]);
                    hi = Vector2.Max(hi, pts[i]);
                }
                polys.Add(pts);
                depth.Add(c[0]);
                boxes.Add(Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y));
            }
            var px = new Color32[IconSize * IconSize];
            outlineField = new float[IconSize * IconSize];
            float step = 2f * IconHalf / IconSize;
            for (int y = 0; y < IconSize; y++)
                for (int x = 0; x < IconSize; x++)
                {
                    var p = new Vector2(-IconHalf + (x + 0.5f) * step, 0.5f - IconHalf + (y + 0.5f) * step);
                    float dOutline = IconSpread, dShape = IconSpread;
                    bool inOutline = false;
                    int inside = 0;
                    for (int k = 0; k < polys.Count; k++)
                    {
                        var b = boxes[k];
                        bool near = p.x > b.xMin - IconSpread && p.x < b.xMax + IconSpread && p.y > b.yMin - IconSpread && p.y < b.yMax + IconSpread;
                        bool across = p.y >= b.yMin && p.y <= b.yMax && p.x <= b.xMax;
                        if (!near && !across) continue;
                        var pts = polys[k];
                        int crossings = 0;
                        float d = IconSpread;
                        for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                        {
                            Vector2 a = pts[j], c = pts[i];
                            if ((c.y > p.y) != (a.y > p.y) && p.x < (a.x - c.x) * (p.y - c.y) / (a.y - c.y) + c.x) crossings++;
                            if (near) d = Mathf.Min(d, SegmentDistance(p, a, c));
                        }
                        if ((crossings & 1) == 1)
                        {
                            inside++;
                            if (depth[k] == 0) inOutline = true;
                        }
                        dShape = Mathf.Min(dShape, d);
                        if (depth[k] == 0) dOutline = Mathf.Min(dOutline, d);
                    }
                    bool inShape = (inside & 1) == 1;
                    outlineField[y * IconSize + x] = inOutline ? dOutline : -dOutline;
                    float r = 0.5f + (inOutline ? dOutline : -dOutline) / (2f * IconSpread);
                    float g = 0.5f + (inShape ? dShape : -dShape) / (2f * IconSpread);
                    px[y * IconSize + x] = new Color32((byte)(Mathf.Clamp01(r) * 255f + 0.5f), (byte)(Mathf.Clamp01(g) * 255f + 0.5f), 0, 255);
                }
            var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, true, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-9f, ab.sqrMagnitude));
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Pop the piece's mark up over <paramref name="pawn"/>'s head (again, if it is still up: white, and bigger
        /// for a moment).</summary>
        void HeadMark(RagdollPawn pawn, PieceKind kind)
        {
            if (pawn == null || kind == PieceKind.Pawn) return;
            var tex = IconTexture(kind);
            if (tex == null) return;
            if (headMarks.TryGetValue(pawn, out var live) && live.fx != null && !live.fx.dead)
            {
                live.born = kit.Clock;
                live.until = kit.Clock + MarkLife;
                live.pops++;
                return;
            }
            var pal = SwordFightSkills.Colors(kind);
            var state = new HeadMarkState { born = kit.Clock, until = kit.Clock + MarkLife };
            var go = new GameObject($"Skill head mark ({kind})");
            go.transform.SetParent(kit.root, false);
            go.AddComponent<MeshFilter>().sharedMesh = iconMeshes.TryGetValue(kind, out var token) && token != null ? token : kit.meshQuad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = IconMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            var block = new MaterialPropertyBlock();
            var head = pawn.bodies[(int)BodyId.Head];
            var headCollider = head.GetComponent<Collider>();
            state.fx = kit.Run(float.MaxValue, (fx, d) =>
            {
                if (pawn == null || head == null) return false;
                float age = kit.Clock - state.born, left = state.until - kit.Clock;
                if (left <= 0f) return false;
                float a = age / F;
                bool first = state.pops == 0;
                // In: 0 → 1.3 by frame 3 → 1 by frame 6, rising into place (again: 1 → 1.4 → 1); out: a little bigger,
                // then gone in six frames.
                float s = Kit.Pop(age, first ? 1.3f : 1.4f, first ? 0f : 1f);
                if (left < 6f * F)
                {
                    float k = 1f - left / (6f * F);
                    s *= k < 0.34f ? 1f + 0.45f * k : 1.15f * (1f - (k - 0.34f) / 0.66f);
                }
                float top = headCollider != null ? headCollider.bounds.max.y : head.position.y + 0.12f;
                float rise = first ? Kit.EaseOutBack(Mathf.Min(1f, a / 8f)) : 1f;
                Vector3 at = new Vector3(head.position.x, top + 0.2f + MarkHeight * 0.5f - 0.12f * (1f - rise) + 0.012f * Mathf.Sin(age * 6.5f), head.position.z);
                // R108, a solid token: it faces the camera upright, spins one whole turn as it pops in (every pop, the
                // king's second one too: fast, then settling), then sways ±25° so its thickness shows.
                var cam = ViewCamera;
                Vector3 look = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.forward;
                if (look.sqrMagnitude < 1e-6f) look = Vector3.forward;
                float spin = 360f * (1f - Kit.EaseOut(Mathf.Min(1f, a / 14f)));
                float sway = 25f * Mathf.Sin(age * 2.8f) * Mathf.Min(1f, a / 14f);
                go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(look, Vector3.up) * Quaternion.Euler(0f, spin + sway, 0f));
                go.transform.localScale = Vector3.one * (MarkHeight * Mathf.Max(0.001f, s));
                block.SetTexture("_MainTex", tex);
                block.SetColor("_Fill", pal.main);
                block.SetColor("_Lines", pal.light);
                block.SetColor("_InkColor", pal.ink);
                block.SetFloat("_Spread", IconSpread);
                block.SetFloat("_InkWidth", IconInk);
                block.SetFloat("_Flash", a < 2f ? 1f : 0f);
                r.SetPropertyBlock(block);
                r.enabled = s > 0.01f;
                return true;
            });
            state.fx.end = () =>
            {
                if (go != null) Destroy(go);
                if (headMarks.TryGetValue(pawn, out var now) && now == state) headMarks.Remove(pawn);
            };
            headMarks[pawn] = state;
        }

        void DestroyIcons()
        {
            foreach (var t in iconTextures.Values) if (t != null) Destroy(t);
            foreach (var m in iconMeshes.Values) if (m != null) Destroy(m);
            iconMeshes.Clear();
            iconTextures.Clear();
            if (iconMat != null) Destroy(iconMat);
        }

        // ---------------------------------------------------------------- the icons (traced by the R102 tool)

        static readonly short[][] KingIcon =
        {
            new short[] { 0, -12, 1000, -18, 994, -22, 975, -29, 968, -45, 966, -52, 955, -48, 939, -45, 936, -27, 932, -20, 920, -20, 871, -39, 835, -46, 826, -54, 803, -63, 788, -67, 769, -79, 741, -84, 680, -90, 674, -96, 674, -105, 680, -141, 718, -175, 741, -194, 746, -213, 756, -262, 759, -264, 758, -302, 756, -346, 737, -366, 722, -382, 706, -395, 689, -412, 657, -420, 617, -420, 572, -412, 523, -401, 500, -399, 489, -380, 453, -355, 420, -317, 384, -281, 360, -281, 356, -291, 348, -344, 347, -349, 345, -357, 335, -359, 305, -357, 303, -357, 66, -359, 64, -359, 45, -357, 38, -348, 27, -308, 19, -238, 15, -164, 6, -92, 4, -90, 2, 130, 4, 132, 6, 185, 8, 238, 15, 289, 17, 342, 25, 353, 32, 357, 42, 357, 328, 353, 341, 344, 347, 289, 348, 283, 350, 277, 358, 287, 367, 319, 388, 355, 424, 387, 472, 401, 500, 404, 519, 416, 551, 418, 621, 414, 640, 399, 678, 384, 701, 359, 725, 332, 742, 289, 758, 262, 758, 260, 759, 221, 758, 177, 742, 143, 722, 116, 697, 96, 672, 90, 672, 84, 678, 80, 714, 65, 775, 56, 792, 52, 807, 35, 841, 27, 850, 16, 877, 18, 922, 27, 932, 41, 936, 48, 941, 50, 956, 45, 964, 29, 968, 18, 977, 12, 996, 5, 1000 },
            new short[] { 1, -253, 78, -249, 81, -228, 87, -200, 91, -177, 91, -175, 93, -111, 93, -109, 95, -56, 95, -54, 97, 45, 97, 46, 95, 154, 93, 156, 91, 198, 89, 232, 81, 241, 74, 234, 68, 224, 66, 171, 64, 170, 62, -103, 61, -105, 62, -175, 62, -177, 64, -226, 66, -245, 70 },
            new short[] { 1, -276, 176, -272, 180, -257, 184, -143, 191, -141, 193, -71, 195, -69, 197, 60, 197, 62, 195, 96, 195, 98, 193, 124, 193, 126, 191, 149, 191, 151, 189, 219, 186, 221, 184, 245, 184, 260, 180, 266, 174, 255, 169, 224, 165, 103, 163, 101, 161, -107, 161, -109, 163, -257, 167, -272, 170 },
            new short[] { 1, -268, 267, -260, 273, -238, 278, -156, 288, 96, 290, 98, 288, 164, 286, 209, 280, 259, 271, 262, 265, 257, 261, 243, 259, 204, 259, 202, 258, -90, 256, -92, 258, -240, 259, -264, 263 },
            new short[] { 1, 332, 697, 355, 672, 370, 640, 374, 623, 374, 564, 370, 544, 363, 527, 357, 504, 325, 453, 296, 422, 262, 396, 230, 379, 221, 377, 198, 366, 135, 354, 80, 356, 69, 358, 52, 366, 48, 369, 45, 381, 52, 422, 56, 466, 67, 500, 71, 527, 80, 545, 88, 574, 98, 589, 99, 598, 116, 631, 137, 659, 177, 697, 202, 712, 238, 722, 274, 722, 291, 716, 298, 716, 313, 710 },
            new short[] { 2, 310, 663, 287, 678, 274, 682, 230, 680, 200, 665, 164, 631, 151, 612, 134, 580, 128, 559, 120, 547, 115, 523, 103, 492, 101, 472, 88, 415, 88, 405, 94, 398, 98, 396, 149, 396, 170, 400, 187, 407, 209, 413, 241, 430, 262, 445, 291, 473, 308, 498, 323, 527, 334, 578, 334, 610, 325, 644 },
            new short[] { 1, -330, 701, -312, 712, -276, 722, -232, 720, -205, 712, -175, 693, -137, 655, -116, 625, -98, 589, -94, 574, -84, 557, -79, 530, -67, 502, -63, 470, -54, 436, -50, 407, -50, 371, -60, 362, -99, 354, -139, 354, -207, 367, -224, 377, -238, 381, -268, 398, -276, 405, -289, 413, -330, 456, -351, 489, -370, 532, -378, 583, -378, 608, -368, 653, -355, 676 },
            new short[] { 2, -308, 667, -321, 653, -334, 629, -338, 612, -338, 578, -334, 553, -323, 521, -302, 485, -262, 443, -241, 428, -213, 413, -166, 398, -151, 396, -103, 396, -96, 400, -92, 409, -98, 434, -98, 453, -101, 472, -111, 496, -115, 525, -124, 544, -130, 566, -137, 578, -147, 600, -166, 629, -200, 663, -234, 680, -276, 682, -285, 680 },
            new short[] { 1, -10, 799, -5, 795, -1, 788, -1, 456, -9, 436, -12, 436, -16, 439, -20, 451, -24, 485, -35, 528, -37, 559, -46, 619, -46, 667, -39, 712, -37, 741, -22, 788 },
        };

        static readonly short[][] QueenIcon =
        {
            new short[] { 0, -519, 833, -533, 808, -538, 788, -535, 762, -519, 729, -496, 710, -483, 704, -454, 700, -442, 690, -440, 683, -435, 629, -423, 571, -419, 517, -408, 471, -402, 404, -390, 344, -390, 42, -381, 27, -352, 21, -302, 19, -300, 17, -277, 17, -275, 15, -254, 15, -190, 6, -154, 6, -152, 4, 98, 2, 100, 4, 185, 6, 188, 8, 208, 8, 298, 19, 329, 19, 365, 23, 383, 31, 390, 44, 390, 348, 394, 367, 396, 402, 410, 475, 412, 512, 425, 569, 429, 623, 440, 667, 442, 688, 456, 702, 481, 704, 504, 717, 525, 740, 535, 779, 527, 819, 523, 827, 502, 848, 469, 862, 440, 862, 408, 850, 383, 825, 373, 792, 375, 760, 383, 738, 406, 712, 408, 704, 394, 677, 331, 585, 327, 575, 312, 556, 296, 527, 283, 517, 277, 517, 271, 523, 267, 542, 262, 604, 260, 606, 258, 671, 246, 760, 246, 781, 254, 796, 275, 804, 302, 829, 312, 852, 315, 883, 310, 906, 304, 919, 290, 935, 273, 948, 250, 954, 217, 954, 200, 950, 188, 944, 167, 925, 156, 904, 150, 883, 150, 869, 154, 852, 167, 825, 198, 798, 200, 781, 192, 754, 183, 740, 175, 708, 167, 694, 160, 667, 148, 642, 146, 627, 133, 600, 127, 573, 117, 552, 115, 542, 106, 529, 100, 529, 92, 544, 92, 552, 79, 594, 75, 627, 62, 667, 58, 702, 46, 740, 42, 775, 29, 812, 31, 840, 42, 850, 54, 856, 75, 883, 79, 902, 79, 933, 75, 950, 58, 975, 25, 996, 6, 1000, -10, 1000, -33, 994, -50, 983, -69, 965, -81, 933, -81, 902, -73, 879, -67, 869, -33, 840, -31, 817, -38, 796, -40, 773, -52, 733, -56, 698, -69, 660, -73, 625, -85, 588, -90, 552, -98, 535, -106, 535, -119, 548, -127, 579, -140, 606, -142, 621, -154, 646, -160, 673, -169, 688, -175, 715, -185, 733, -192, 760, -204, 785, -202, 796, -181, 812, -162, 833, -154, 858, -154, 892, -158, 908, -171, 927, -190, 944, -208, 952, -235, 956, -269, 950, -298, 929, -317, 890, -317, 860, -302, 827, -279, 806, -260, 798, -252, 790, -248, 777, -254, 725, -256, 662, -269, 569, -271, 523, -277, 512, -283, 512, -294, 523, -406, 696, -408, 704, -406, 712, -394, 725, -381, 744, -373, 767, -375, 804, -390, 831, -410, 850, -440, 862, -471, 862, -483, 858, -506, 846 },
            new short[] { 1, -277, 85, -273, 90, -250, 96, -219, 100, -194, 100, -192, 102, -121, 102, -119, 104, -60, 104, -58, 106, 50, 106, 52, 104, 171, 102, 173, 100, 219, 98, 256, 90, 267, 81, 258, 75, 248, 73, 190, 71, 188, 69, -112, 67, -115, 69, -192, 69, -194, 71, -248, 73, -269, 77 },
            new short[] { 1, -302, 194, -298, 198, -281, 202, -156, 210, -154, 212, -77, 215, -75, 217, 67, 217, 69, 215, 106, 215, 108, 212, 138, 212, 140, 210, 165, 210, 167, 208, 242, 204, 244, 202, 271, 202, 288, 198, 294, 192, 281, 185, 248, 181, 115, 179, 112, 177, -117, 177, -119, 179, -281, 183, -298, 188 },
            new short[] { 1, -294, 294, -285, 300, -260, 306, -171, 317, 106, 319, 108, 317, 181, 315, 231, 308, 285, 298, 290, 292, 283, 288, 269, 285, 225, 285, 223, 283, -98, 281, -100, 283, -262, 285, -290, 290 },
            new short[] { 1, -367, 523, -358, 517, -354, 508, -350, 490, -340, 471, -335, 452, -325, 433, -312, 396, -323, 383, -344, 383, -352, 390, -356, 398, -358, 427, -373, 488, -373, 517 },
            new short[] { 1, 358, 531, 360, 529, 360, 512, 350, 492, 342, 460, 327, 427, 327, 421, 312, 392, 302, 383, 281, 383, 273, 388, 269, 396, 279, 417, 290, 429, 298, 448, 304, 454, 327, 496, 340, 510, 346, 525, 352, 531 },
            new short[] { 1, 196, 617, 200, 612, 200, 577, 196, 554, 194, 508, 183, 450, 179, 402, 175, 394, 167, 388, 127, 390, 119, 396, 117, 408, 127, 435, 129, 450, 142, 477, 146, 500, 158, 527, 160, 544, 173, 573, 179, 600, 185, 610 },
            new short[] { 1, -215, 635, -208, 631, -204, 619, -200, 569, -188, 492, -185, 446, -179, 417, -181, 396, -188, 390, -200, 385, -229, 385, -240, 396, -240, 442, -225, 565, -223, 617, -219, 631 },
            new short[] { 1, -10, 704, -4, 698, 0, 683, 0, 410, -4, 400, -15, 392, -65, 390, -79, 398, -81, 417, -75, 433, -71, 469, -58, 508, -54, 542, -42, 581, -40, 606, -25, 658, -23, 681, -17, 700 },
            new short[] { 1, 442, 831, 450, 831, 467, 823, 475, 815, 481, 796, 475, 773, 465, 765, 450, 758, 440, 758, 423, 767, 412, 777, 410, 783, 412, 810, 425, 823 },
            new short[] { 1, -467, 831, -448, 827, -431, 810, -427, 802, -427, 785, -431, 777, -444, 765, -458, 758, -467, 758, -481, 765, -494, 777, -498, 788, -498, 802, -490, 817, -477, 827 },
            new short[] { 1, 223, 923, 244, 917, 256, 904, 260, 896, 260, 879, 256, 871, 240, 854, 227, 850, 208, 854, 196, 867, 190, 881, 190, 894, 194, 906, 206, 919 },
            new short[] { 1, -244, 923, -223, 917, -212, 906, -206, 894, -206, 881, -212, 869, -227, 854, -240, 850, -262, 856, -277, 877, -277, 898, -271, 910, -256, 921 },
            new short[] { 1, -17, 967, 0, 967, 8, 962, 27, 942, 29, 931, 25, 915, 12, 902, 0, 896, -23, 898, -33, 904, -42, 917, -44, 935, -40, 948, -25, 962 },
        };

        static readonly short[][] RookIcon =
        {
            new short[] { 0, -335, 998, -342, 989, -342, 757, -337, 746, -331, 741, -300, 741, -289, 735, -275, 721, -225, 650, -218, 637, -218, 361, -225, 347, -271, 283, -291, 261, -300, 257, -331, 257, -340, 250, -342, 246, -342, 166, -346, 157, -360, 150, -415, 153, -428, 148, -435, 142, -437, 131, -435, 126, -435, 18, -430, 9, -417, 2, 415, 2, 424, 4, 430, 11, 435, 24, 435, 137, 433, 144, 424, 150, 413, 153, 364, 150, 353, 153, 342, 162, 340, 166, 340, 241, 335, 250, 320, 257, 287, 259, 275, 270, 222, 345, 213, 372, 213, 631, 222, 653, 275, 728, 284, 737, 295, 741, 326, 743, 337, 754, 340, 763, 340, 985, 335, 993, 320, 1000, 200, 1000, 189, 991, 187, 985, 187, 938, 180, 927, 171, 923, 118, 923, 112, 925, 103, 934, 101, 987, 98, 993, 87, 1000, -90, 1000, -98, 998, -107, 989, -107, 945, -112, 931, -123, 923, -174, 923, -180, 925, -191, 938, -194, 947, -191, 980, -194, 989, -202, 998, -211, 1000, -331, 1000 },
            new short[] { 1, -289, 199, -280, 208, 273, 208, 280, 206, 289, 195, 280, 184, 273, 181, -278, 181, -287, 188 },
            new short[] { 1, -200, 299, -191, 310, -185, 312, 178, 312, 187, 310, 196, 303, 198, 290, 191, 283, 183, 281, -185, 281, -198, 288 },
            new short[] { 1, -202, 721, -202, 728, -189, 737, 189, 737, 196, 735, 202, 726, 194, 712, 174, 706, -174, 706, -196, 712 },
            new short[] { 1, -289, 827, -278, 838, 273, 838, 284, 832, 287, 819, 280, 812, 264, 808, -273, 808, -287, 816 },
        };

        static readonly short[][] BishopIcon =
        {
            new short[] { 0, -26, 998, -51, 985, -68, 968, -81, 941, -83, 906, -74, 888, -74, 881, -78, 875, -123, 838, -165, 796, -186, 768, -200, 746, -217, 711, -220, 691, -228, 667, -230, 635, -232, 634, -230, 587, -215, 527, -207, 512, -203, 498, -195, 487, -188, 472, -160, 435, -155, 423, -155, 415, -163, 395, -166, 373, -175, 353, -180, 324, -190, 298, -188, 288, -183, 281, -176, 279, -160, 269, -110, 254, -81, 251, -76, 246, -76, 242, -91, 234, -123, 227, -151, 224, -188, 224, -190, 222, -233, 221, -235, 219, -250, 219, -268, 214, -278, 207, -305, 182, -327, 159, -330, 151, -330, 140, -322, 104, -317, 97, -309, 77, -292, 54, -268, 30, -245, 13, -235, 10, -222, 2, -212, 2, -153, 62, -151, 74, -165, 84, -181, 92, -200, 107, -208, 117, -213, 127, -213, 134, -207, 140, -202, 142, -145, 147, -123, 156, -101, 161, -84, 171, -74, 174, -53, 189, -24, 216, -8, 236, 3, 237, 46, 192, 66, 177, 98, 161, 115, 157, 141, 147, 200, 142, 208, 137, 210, 124, 205, 115, 183, 95, 151, 77, 148, 74, 148, 67, 156, 55, 205, 5, 212, 2, 220, 2, 245, 15, 263, 28, 297, 64, 315, 95, 317, 105, 327, 129, 329, 151, 325, 159, 273, 209, 262, 216, 213, 222, 150, 224, 110, 229, 84, 236, 73, 244, 79, 251, 108, 254, 130, 263, 153, 268, 166, 276, 176, 279, 185, 288, 185, 303, 181, 311, 180, 326, 170, 351, 165, 380, 155, 406, 153, 423, 158, 436, 176, 458, 183, 472, 191, 482, 195, 492, 207, 512, 210, 527, 222, 557, 222, 569, 228, 609, 228, 632, 220, 692, 202, 737, 191, 756, 166, 791, 123, 836, 86, 865, 74, 876, 73, 888, 79, 913, 79, 930, 74, 952, 58, 977, 43, 988, 23, 998, 16, 1000 },
            new short[] { 1, 73, 206, 78, 211, 91, 211, 108, 206, 151, 199, 235, 196, 250, 192, 260, 187, 268, 177, 268, 169, 260, 164, 238, 164, 237, 166, 207, 166, 205, 167, 155, 171, 103, 186, 88, 196, 78, 199 },
            new short[] { 1, -76, 207, -76, 204, -83, 197, -86, 197, -110, 184, -121, 182, -148, 172, -190, 167, -208, 167, -210, 166, -238, 166, -240, 164, -262, 164, -272, 171, -270, 179, -262, 187, -248, 194, -218, 196, -217, 197, -191, 197, -190, 199, -155, 199, -111, 206, -93, 211, -79, 211 },
            new short[] { 1, -78, 408, -71, 400, -71, 373, -76, 334, -79, 326, -91, 316, -111, 314, -120, 311, -135, 313, -138, 318, -138, 334, -128, 363, -126, 381, -123, 391, -113, 400, -89, 408 },
            new short[] { 1, -181, 669, -176, 666, -166, 622, -163, 594, -155, 575, -150, 557, -120, 508, -76, 465, -74, 457, -81, 452, -98, 448, -116, 448, -123, 452, -148, 482, -166, 513, -180, 545, -191, 599, -190, 654, -186, 664 },
            new short[] { 1, -6, 823, 9, 819, 18, 809, 18, 744, 23, 734, 29, 731, 93, 731, 99, 726, 103, 719, 103, 704, 99, 697, 94, 694, 33, 694, 23, 691, 18, 682, 18, 513, 19, 507, 18, 500, 13, 495, 4, 492, -11, 492, -19, 500, -19, 684, -21, 687, -31, 694, -94, 694, -106, 706, -105, 721, -96, 729, -91, 731, -29, 731, -21, 737, -19, 742, -19, 809, -18, 814, -11, 821 },
            new short[] { 1, -38, 963, -31, 955, -31, 930, -26, 918, -26, 910, -33, 900, -39, 896, -46, 896, -54, 903, -58, 910, -59, 928, -58, 938, -51, 952, -43, 962 },
        };

        static readonly short[][] KnightIcon =
        {
            new short[] { 0, -120, 967, -127, 956, -127, 942, -120, 914, -120, 872, -124, 860, -136, 851, -157, 844, -192, 812, -201, 793, -208, 760, -215, 747, -285, 677, -341, 609, -345, 600, -397, 551, -413, 528, -417, 512, -417, 498, -410, 470, -403, 460, -397, 435, -385, 416, -357, 386, -343, 377, -320, 367, -276, 370, -241, 391, -210, 419, -190, 426, -131, 458, -76, 465, -38, 479, -10, 498, 1, 498, 8, 488, 8, 479, -10, 444, -57, 391, -157, 298, -187, 260, -215, 214, -231, 172, -238, 112, -231, 35, -224, 16, -206, 2, 373, 2, 390, 12, 394, 21, 399, 49, 399, 74, 410, 147, 413, 198, 415, 200, 415, 295, 413, 298, 410, 340, 403, 360, 394, 419, 383, 447, 378, 472, 366, 493, 359, 521, 348, 540, 338, 567, 301, 635, 264, 688, 141, 828, 124, 853, 117, 881, 94, 926, 59, 974, 36, 998, 15, 1000, 6, 993, 1, 984, -8, 902, -17, 893, -24, 895, -41, 916, -85, 958, -108, 970 },
            new short[] { 1, -350, 498, -350, 507, -338, 516, -308, 519, -303, 514, -303, 507, -317, 493, -331, 486, -345, 491 },
            new short[] { 1, 217, 628, 231, 619, 238, 602, 252, 586, 287, 514, 294, 488, 306, 465, 313, 428, 320, 414, 327, 384, 327, 365, 338, 286, 338, 230, 336, 228, 334, 172, 324, 109, 313, 95, 299, 91, 266, 91, 259, 93, 250, 102, 250, 130, 262, 170, 269, 209, 269, 240, 273, 270, 273, 381, 264, 463, 252, 495, 245, 533, 234, 556, 227, 584, 215, 605, 213, 623 },
            new short[] { 1, -55, 758, -55, 749, -59, 742, -101, 728, -122, 728, -134, 740, -131, 751, -122, 760, -108, 767, -92, 770, -69, 767 },
        };
    }
}
