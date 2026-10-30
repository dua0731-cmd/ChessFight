using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;
using UnityEngine.Rendering;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R236, the bishop's diagonal slide drawn (대각선 미끄럼, SwordFightSkills.Edge.cs; 승규 님 10-29: "비숍이 서 있는 자리부터
    /// 색칠된 대각선이 보이고", then "칠하는 범위는 대각선 2칸. 색은 비숍의 고유 색으로"): in his own silver (#C2C9D3, the
    /// board look's BSilver), lying on the board.
    /// - While he picks (E, then a left click goes; E again or a right click calls it off; its owner's screen): every diagonal that goes anywhere from his square, its squares painted (a
    ///   soft square of light and an outline) and joined by a line from under him through their corners; the one nearest
    ///   where the camera looks bold, the others faint. Turning the mouse moves the bold one at once (the owner's own
    ///   camera, online too).
    /// - Once he goes (every screen): the way he slides, bold, each square going out as he passes it, gone as he stops.
    /// The slide's own effects are not made yet; its sounds are SwordFightSkillSfx's (BishopSlide*).
    /// </summary>
    public partial class SwordFightSkillFx
    {
        static readonly Color SlideSilver = Kit.Hex("#C2C9D3");
        /// <summary>The most squares drawn on one diagonal (the skill's own number is 2).</summary>
        const int SlideSquares = 4;

        Material slideSquareMat;
        Texture2D slideSquareTex;

        void BishopSlideMarks(SwordFightSkills s, bool mine)
        {
            if (s.EdgeStage == SfEdge.Aim) { if (mine) BishopWaysAim(s); return; }
            if (s.EdgeStage != SfEdge.None) BishopWayGone(s);
        }

        /// <summary>Its diagonals while he picks: the bold one and the faint ones.</summary>
        void BishopWaysAim(SwordFightSkills s)
        {
            var owner = s;
            kit.Keep(s, "bishop slide aim", () =>
            {
                var fx = new Kit.Fx();
                int n = SwordFightSkills.BishopDiagonals.Length;
                for (int i = 0; i < n * (SlideSquares + 1); i++) fx.strips.Add(LineStrip());
                var pools = new GameObject[n * SlideSquares];
                var blocks = new MaterialPropertyBlock[pools.Length];
                for (int i = 0; i < pools.Length; i++) { pools[i] = SlidePool(); blocks[i] = new MaterialPropertyBlock(); }
                // 10-29: the aim's own sounds, on his screen like the squares: it opens, and it ends without a slide
                // (E again, a right click, knocked over).
                SwordFightSkillSfx.Moment("BishopSlideAim", owner.Pawn.Hips.position, owner);
                fx.end = () =>
                {
                    foreach (var g in pools) if (g != null) Destroy(g);
                    if (owner != null && owner.Pawn != null && owner.EdgeStage == SfEdge.None)
                        SwordFightSkillSfx.Moment("BishopSlideCancel", owner.Pawn.Hips.position, owner);
                };
                var ways = new int[n];
                var pts = new List<Vector3>();
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null || !kit.Held(x)) return false;
                    int pick = owner.BishopWays(out Vector3 square, out float cell, ways);
                    float grow = Kit.EaseOut(x.age / 0.12f), breathe = 0.86f + 0.14f * Mathf.Sin(kit.Clock * 6f);
                    for (int i = 0; i < n; i++)
                    {
                        bool bold = i == pick;
                        var diag = SwordFightSkills.BishopDiagonals[i];
                        Vector3 step = new Vector3(diag.x, 0f, diag.y) * cell;
                        int count = Mathf.Min(ways[i], SlideSquares);
                        float a = grow * (bold ? breathe : 0.3f);
                        for (int k = 0; k < SlideSquares; k++)
                        {
                            var sq = x.strips[i * (SlideSquares + 1) + k];
                            int p = i * SlideSquares + k;
                            if (k >= count) { sq.Hide(); pools[p].SetActive(false); continue; }
                            Vector3 at = Kit.FloorUnder(square + step * (k + 1), 1.5f);
                            SlideSquare(sq, pools[p], blocks[p], at, cell, a, bold);
                        }
                        SlideLine(x.strips[i * (SlideSquares + 1) + SlideSquares], pts, square, step, count, a, bold);
                    }
                    return true;
                };
                return fx;
            });
        }

        /// <summary>The way he slides (every screen): bold, each square going out once he is past its middle; all gone as
        /// he stops.</summary>
        void BishopWayGone(SwordFightSkills s)
        {
            var owner = s;
            kit.Keep(s, "bishop slide way", () =>
            {
                var fx = new Kit.Fx();
                for (int i = 0; i < SlideSquares + 1; i++) fx.strips.Add(LineStrip());
                var pools = new GameObject[SlideSquares];
                var blocks = new MaterialPropertyBlock[SlideSquares];
                for (int i = 0; i < SlideSquares; i++) { pools[i] = SlidePool(); blocks[i] = new MaterialPropertyBlock(); }
                fx.end = () => { foreach (var g in pools) if (g != null) Destroy(g); };
                var pts = new List<Vector3>();
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null || !kit.Held(x)) return false;
                    var floor = SwordFightEdgeFloor.Current;
                    float cell = floor != null && floor.Snap > 0f ? floor.Snap : 2f;
                    Vector3 from = owner.EdgeAt, way = Kit.FlatDir(owner.EdgeLand - from, owner.EdgeOut);
                    float length = new Vector2(owner.EdgeLand.x - from.x, owner.EdgeLand.z - from.z).magnitude;
                    int count = Mathf.Clamp(Mathf.RoundToInt(length / (cell * 1.41421f)), 0, SlideSquares);
                    Vector3 step = count > 0 ? (owner.EdgeLand - from) / count : Vector3.zero;
                    step.y = 0f;
                    // How far along he is (0 at his square, count at the far one).
                    float along = count > 0 ? Vector3.Dot(owner.Pawn.Hips.position - from, way) / (cell * 1.41421f) : 0f;
                    var X = EdgeS;
                    float fade = owner.EdgeStage == SfEdge.Recovery
                        ? 1f - Mathf.Clamp01(owner.EdgeTime / Mathf.Max(0.05f, X != null ? X.bishopRecovery : 0.25f)) : 1f;
                    for (int k = 0; k < SlideSquares; k++)
                    {
                        var sq = x.strips[k];
                        if (k >= count) { sq.Hide(); pools[k].SetActive(false); continue; }
                        // A square goes out once he is past its middle (the last one stays until he stops on it).
                        float passed = k < count - 1 ? Mathf.Clamp01((along - (k + 1)) / 0.5f) : 0f;
                        float a = fade * (1f - passed);
                        if (a <= 0.01f) { sq.Hide(); pools[k].SetActive(false); continue; }
                        Vector3 at = Kit.FloorUnder(from + step * (k + 1), 1.5f);
                        SlideSquare(sq, pools[k], blocks[k], at, cell, a, true);
                    }
                    float lineA = fade * (owner.EdgeStage == SfEdge.Active ? 1f - 0.6f * Mathf.Clamp01(along / Mathf.Max(1, count)) : 1f);
                    SlideLine(x.strips[SlideSquares], pts, from, step, count, lineA, true);
                    return true;
                };
                return fx;
            });
        }

        /// <summary>One square of a diagonal: its outline and the soft square of light on its wood.</summary>
        void SlideSquare(Kit.Strip outline, GameObject pool, MaterialPropertyBlock block, Vector3 at, float cell, float alpha, bool bold)
        {
            TintStrip(outline, SlideSilver);
            LightSquare(outline, at, Vector3.forward, cell * 0.9f, alpha * (bold ? 0.95f : 1f), bold ? 0.07f : 0.03f);
            pool.SetActive(alpha > 0.01f);
            if (alpha <= 0.01f) return;
            pool.transform.SetPositionAndRotation(at + Vector3.up * 0.017f, Quaternion.LookRotation(Vector3.down, Vector3.forward));
            pool.transform.localScale = Vector3.one * cell * 0.94f;
            Color c = SlideSilver * (bold ? 0.75f : 0.5f);
            c.a = alpha * (bold ? 0.6f : 0.35f);
            block.SetColor(LitMaterials.BaseColorId, c);
            pool.GetComponent<MeshRenderer>().SetPropertyBlock(block);
        }

        /// <summary>A diagonal's line: from under him (his square's middle) through the squares' corners to the far one.</summary>
        void SlideLine(Kit.Strip line, List<Vector3> pts, Vector3 square, Vector3 step, int count, float alpha, bool bold)
        {
            if (count <= 0 || alpha <= 0.01f) { line.Hide(); return; }
            pts.Clear();
            for (int j = 0; j <= count * 4; j++) pts.Add(Kit.FloorUnder(square + step * (j / 4f), 1.5f) + Vector3.up * 0.016f);
            TintStrip(line, SlideSilver);
            line.Build(pts, j => bold ? 0.05f : 0.02f, kit.Eye, true);
            line.opacity = alpha * (bold ? 0.9f : 0.8f);
            line.Apply();
        }

        /// <summary>A soft square of light lying on the board (added onto the wood).</summary>
        GameObject SlidePool()
        {
            var go = new GameObject("Bishop slide square", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(kit.root, false);
            go.GetComponent<MeshFilter>().sharedMesh = kit.meshQuad;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = SlideSquareMaterial();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return go;
        }

        Material SlideSquareMaterial()
        {
            if (slideSquareMat != null) return slideSquareMat;
            slideSquareTex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    // Even inside, soft toward the edges (the outline draws the edge itself).
                    float e = Mathf.Min(Mathf.Min(x, 63 - x), Mathf.Min(y, 63 - y)) / 10f;
                    slideSquareTex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e))));
                }
            slideSquareTex.Apply();
            slideSquareMat = new Material(Shader.Find(LitMaterials.ParticlesUnlitName)) { hideFlags = HideFlags.HideAndDontSave, name = "Bishop slide square" };
            LitMaterials.MakeTransparent(slideSquareMat, true);
            slideSquareMat.SetTexture("_BaseMap", slideSquareTex);
            slideSquareMat.SetFloat("_Cull", 0f);
            return slideSquareMat;
        }
    }
}
