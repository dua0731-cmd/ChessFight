using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;
using UnityEngine.Rendering;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The knight's fork dive for real (10-25, 승규 님: "나이트 f 도 실사로 바꿔줘", then "판 갈라짐 안해도 되고 네모 칸에 적이
    /// 들어오면 표시를 더 명확하게 … 맞으면 적 기물이 맞았다고도 표시 될 수 있게 사실적으로"): its squares are outlines of
    /// light lying on the board, and a square with an enemy in it lights up warm - a thick amber outline and a pool of
    /// light on the wood under them, breathing - so it is plain at a glance who the fork will take; its flight is a faint
    /// line of light while it aims. It springs off with the air knocked down out from under it, dives leaving see-through
    /// copies of itself with its blade blurring, and lands hard (splinters, a long hold, a heavy shake; the board does not
    /// split). Whoever it strikes shows it: a flash where the blade met them and a fresh cut left in their wood, light
    /// catching its broken edge, fading a moment later, and chips of their own wood.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        /// <summary>The warm light of a fork square with an enemy in it.</summary>
        static readonly Color ForkFoe = new Color(1f, 0.62f, 0.26f);

        /// <summary>A square of light lying on the board: its four edges.</summary>
        void LightSquare(Kit.Strip strip, Vector3 c, Vector3 dir, float side, float opacity, float width = 0.026f)
        {
            dir = Kit.FlatDir(dir, Vector3.forward);
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            float h = side * 0.5f;
            var pts = new List<Vector3>();
            Vector3[] corners = { c + (dir + right) * h, c + (dir - right) * h, c + (-dir - right) * h, c + (-dir + right) * h };
            for (int i = 0; i <= 4; i++)
            {
                Vector3 a = corners[i % 4], b = corners[(i + 1) % 4];
                pts.Add(Kit.FloorUnder(a, 1f) + Vector3.up * 0.015f);
                if (i < 4) pts.Add(Kit.FloorUnder(Vector3.Lerp(a, b, 0.5f), 1f) + Vector3.up * 0.015f);
            }
            strip.Build(pts, j => width, kit.Eye, true);
            strip.opacity = opacity;
            strip.Apply();
        }

        static void TintStrip(Kit.Strip strip, Color c)
        {
            strip.colors = new Kit.Palette { main = c, light = c, deep = c, ink = c };
            strip.core = c;
        }

        /// <summary>A pool of light lying on the board (a soft round glow, the glow texture, added onto the wood).</summary>
        GameObject FloorGlow(string name)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(kit.root, false);
            go.GetComponent<MeshFilter>().sharedMesh = kit.meshQuad;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = FlashMaterial();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return go;
        }

        /// <summary>Its squares while it aims and flies: the square it comes down on and the eight it forks, outlines of
        /// light; a square with an enemy in it warm and plain (thick amber edges, a pool of light under them, breathing);
        /// its flight for its owner while it aims; at the top of its flight the L to each square, faint lines of light.</summary>
        /// <summary>The pool of light under someone the knight will take (on or off).</summary>
        static void ForkGlow(GameObject glow, MaterialPropertyBlock block, bool on, Vector3 at, Vector3 dir, float cell, float alpha, float breathe)
        {
            glow.SetActive(on);
            if (!on) return;
            glow.transform.SetPositionAndRotation(at + Vector3.up * 0.017f, Quaternion.LookRotation(Vector3.down, dir));
            glow.transform.localScale = Vector3.one * cell * 1.05f;
            Color g = ForkFoe * 1.3f;
            g.a = alpha * 0.5f * breathe;
            block.SetColor(LitMaterials.BaseColorId, g);
            glow.GetComponent<MeshRenderer>().SetPropertyBlock(block);
        }

        void RealKnightMarks(SwordFightSkills s, SwordFightSkillParams S, float alpha)
        {
            bool aim = s.Stage == SfStage.Aim, air = s.Stage == SfStage.Active;
            if (!aim && !air) return;
            var owner = s;
            kit.Keep(s, aim ? "knight real aim" : "knight real air", () =>
            {
                var fx = new Kit.Fx();
                int n = SwordFightSkills.KnightMoves.Length;
                for (int i = 0; i < 1 + n * 2 + 1; i++) fx.strips.Add(LineStrip());
                // One light per fork square, and the last for the square it comes down on.
                var glows = new GameObject[n + 1];
                var blocks = new MaterialPropertyBlock[n + 1];
                for (int i = 0; i <= n; i++) { glows[i] = FloorGlow("Fork square light"); blocks[i] = new MaterialPropertyBlock(); }
                fx.end = () => { foreach (var g in glows) if (g != null) Destroy(g); };
                var pts = new List<Vector3>();
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null || !kit.Held(x)) return false;
                    bool aiming = owner.Stage == SfStage.Aim;
                    Vector3 p = aiming ? owner.AimPoint : owner.Point, dir = aiming ? owner.AimDir : owner.Dir;
                    float k = aiming ? 0.3f : Kit.Charge(owner.KnightClock, S.knightRise + S.knightHover);
                    float cell = S.knightCell, glow = 0.55f + 0.45f * k;
                    float breathe = 0.82f + 0.18f * Mathf.Sin(kit.Clock * 7f);
                    // 10-26 (승규 님): an enemy on the landing square is marked like one on a fork square (the stomp takes it).
                    Vector3 land = Kit.FloorUnder(p, 1.5f);
                    bool landFoe = EnemyOn(owner, p, S.knightStomp);
                    TintStrip(x.strips[0], landFoe ? ForkFoe : LightLine);
                    LightSquare(x.strips[0], land, dir, cell * 0.9f, landFoe ? alpha * 0.95f * breathe : alpha * 0.45f * glow, landFoe ? 0.06f : 0.026f);
                    ForkGlow(glows[n], blocks[n], landFoe, land, dir, cell, alpha, breathe);
                    float show = aiming ? 0f : Mathf.Clamp01((owner.KnightClock - S.knightRise) / Mathf.Max(0.05f, S.knightHover));
                    Vector3 c = Kit.FloorUnder(owner.Point, 1.5f) + Vector3.up * 0.015f;
                    for (int i = 0; i < n; i++)
                    {
                        var sq = x.strips[1 + i];
                        var l = x.strips[1 + n + i];
                        if (!owner.KnightSquareOn[i]) { sq.Hide(); l.Hide(); glows[i].SetActive(false); continue; }
                        Vector3 at = Kit.FloorUnder(owner.KnightSquares[i], 1.5f);
                        bool foe = EnemyOn(owner, owner.KnightSquares[i], S.knightSquare + 0.15f);
                        TintStrip(sq, foe ? ForkFoe : LightLine);
                        LightSquare(sq, at, dir, cell * 0.78f, foe ? alpha * 0.95f * breathe : alpha * 0.28f * glow, foe ? 0.06f : 0.026f);
                        // The pool of light under someone the fork will take.
                        ForkGlow(glows[i], blocks[i], foe, at, dir, cell, alpha, breathe);
                        if (show <= 0f) { l.Hide(); continue; }
                        TintStrip(l, foe ? ForkFoe : LightLine);
                        var leg = KnightL(c, owner.Dir, cell, SwordFightSkills.KnightMoves[i], at + Vector3.up * 0.015f);
                        l.Build(leg, j => foe ? 0.035f : 0.02f, kit.Eye, true);
                        l.head = Kit.EaseOut(show);
                        l.opacity = alpha * (foe ? 0.75f : 0.22f);
                        l.Apply();
                    }
                    var path = x.strips[x.strips.Count - 1];
                    if (aiming)
                    {
                        pts.Clear();
                        Vector3 from = owner.Pawn.Hips.position, to = owner.AimPoint + Vector3.up * (owner.Pawn.standHeight + 0.03f);
                        float total = owner.KnightTime;
                        for (int i = 0; i <= 24; i++) pts.Add(SwordFightSkills.KnightFlight(S, from, to, owner.AimDir, total * i / 24f, out _));
                        path.Build(pts, j => 0.018f, kit.Eye);
                        path.opacity = alpha * 0.35f;
                        path.Apply();
                    }
                    else path.Hide();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>The spring: the air knocked down and out from under it and splinters off the square where it pushed
        /// off; then, while it dives, see-through copies of it left behind every other frame, its blade blurred.</summary>
        void RealKnightLeap(SfFxEvent e)
        {
            var s = e.by;
            var pawn = s.Pawn;
            if (pawn == null) return;
            var S = this.S;
            Vector3 at = Kit.FloorUnder(e.at + Vector3.up * 0.5f, 1f);
            Vector3 dir = Kit.FlatDir(s.Dir, Vector3.forward);
            Air.Kick(at + Vector3.up * 0.25f, Vector3.down * 0.8f - dir * 0.6f, 1.3f);
            var keepA = chipA; var keepB = chipB; float keepShare = chipShare;
            Color wood = FloorWood(at);
            chipA = ChipsOf(wood); chipB = ChipsOf(wood, 0.7f); chipShare = 0.6f;
            WallChips(at + Vector3.up * 0.03f, -dir + Vector3.up * 0.9f, 7, 2.4f, 0.6f, 0.01f, 0.03f);
            chipA = keepA; chipB = keepB; chipShare = keepShare;
            Punch(pawn, null, 5f, 14);
            float rise = S != null ? S.knightRise : 0.3f, hover = S != null ? S.knightHover : 0.14f;
            bool top = false, dive = false;
            float nextGhost = 0f;
            kit.Run(1.6f, (fx, d) =>
            {
                bool flying = s != null && pawn != null && s.Stage == SfStage.Active && s.Piece == PieceKind.Knight;
                if (!flying) return false;
                float t = s.KnightClock;
                if (!top && t >= rise)
                {
                    top = true;
                    Punch(pawn, null, 3f, 10);
                }
                if (!dive && t >= rise + hover)
                {
                    dive = true;
                    Punch(pawn, null, 7f, 12);
                    Shake(pawn, null, 0.05f, 4);
                    BladeBlur(s.Fighter, 0.3f, 1f);
                    Vector3 down = (s.Point - pawn.Hips.position).normalized;
                    Air.Kick(pawn.Hips.position - down * 0.3f, -down, 0.8f);
                }
                if (dive && fx.age >= nextGhost)
                {
                    RealGhost(pawn, 0.3f, 0.18f, Vector3.up);
                    nextGhost = fx.age + 2f * F;
                }
                return true;
            });
        }

        /// <summary>The landing, for real: splinters off the square it comes down on, a long hold, a heavy shake and kick of
        /// the view (no cracks: 승규 님 "판 갈라짐 안해도 돼"). Whoever the fork takes shows it with their hit.</summary>
        void RealKnightLand(SfFxEvent e)
        {
            var s = e.by;
            Vector3 c = Kit.FloorUnder(e.at + Vector3.up * 0.5f, 1f);
            var keepA = chipA; var keepB = chipB; float keepShare = chipShare;
            Color wood = FloorWood(c);
            chipA = ChipsOf(wood); chipB = ChipsOf(wood, 0.7f); chipShare = 0.6f;
            WallChips(c + Vector3.up * 0.05f, Vector3.up, 12, 3.4f, 0.85f, 0.012f, 0.04f);
            chipA = keepA; chipB = keepB; chipShare = keepShare;
            HitStop(6);
            Shake(s.Pawn, null, 0.2f, 10);
            Punch(s.Pawn, null, 8f, 18);
        }

        /// <summary>Someone the fork struck: <see cref="RealHit"/>'s chips and weight, and on them a flash where the blade met
        /// them and a cut left in their wood (<see cref="CutMark"/>).</summary>
        void RealKnightHit(SfFxEvent e)
        {
            if (e.target == null) return;
            Vector3 from = e.by != null && e.by.Pawn != null ? e.by.Pawn.Hips.position : e.at - e.dir;
            Vector3 at = OnBody(e.target, from, out Vector3 normal);
            ClashFlash(at + normal * 0.03f, 0.55f);
            CutMark(e.target, at, normal, e.dir);
            var real = e;
            real.at = at;
            RealHit(real, 4, 0.14f);
        }

        /// <summary>Where a blow from <paramref name="from"/> meets <paramref name="pawn"/>'s body at chest height (its own
        /// colliders), and the body's outward face there; a point a body's width in from its middle if nothing is hit.</summary>
        static Vector3 OnBody(RagdollPawn pawn, Vector3 from, out Vector3 normal)
        {
            Vector3 chest = pawn.bodies[(int)BodyId.Chest].position;
            Vector3 toward = from - chest;
            toward.y = 0f;
            toward = toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward;
            Vector3 start = chest + toward * 1.2f;
            float best = float.MaxValue;
            Vector3 point = chest + toward * 0.22f;
            normal = toward;
            foreach (var hit in Physics.RaycastAll(start, -toward, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                var rb = hit.collider.attachedRigidbody;
                if (rb == null || System.Array.IndexOf(pawn.bodies, rb) < 0) continue;
                if (hit.distance < best) { best = hit.distance; point = hit.point; normal = hit.normal; }
            }
            return point;
        }

        Material cutMat;

        /// <summary>
        /// A fresh cut in a struck piece's wood: a dark slit across where the blade went in (slanting with the blow), its
        /// broken edge catching the light, held on the piece as it is thrown (it rides on the body part it is in), for a
        /// second and a half, then fading.
        /// </summary>
        void CutMark(RagdollPawn pawn, Vector3 at, Vector3 normal, Vector3 blow)
        {
            // The body part it is on: the nearest of the chest, the hips and the head.
            Transform on = pawn.bodies[(int)BodyId.Chest].transform;
            float near = (on.position - at).sqrMagnitude;
            foreach (var id in new[] { BodyId.Hips, BodyId.Head })
            {
                var t = pawn.bodies[(int)id].transform;
                float dd = (t.position - at).sqrMagnitude;
                if (dd < near) { near = dd; on = t; }
            }
            if (cutMat == null)
            {
                cutMat = new Material(Shader.Find(LitMaterials.ParticlesUnlitName)) { hideFlags = HideFlags.HideAndDontSave, name = "Cut mark" };
                LitMaterials.MakeTransparent(cutMat);
                cutMat.SetFloat("_Cull", 0f);
            }
            normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.forward;
            // Along the cut: the blow's way laid on the body's face, tipped to a slant.
            Vector3 along = Vector3.ProjectOnPlane(blow.sqrMagnitude > 1e-6f ? blow : Vector3.right, normal);
            if (along.sqrMagnitude < 1e-4f) along = Vector3.Cross(normal, Vector3.up);
            along = Quaternion.AngleAxis(Random.Range(20f, 40f) * (Random.value < 0.5f ? 1f : -1f), normal) * along.normalized;
            var parts = new List<(Transform t, MeshRenderer r, MaterialPropertyBlock b, Color c, float a)>();
            void Part(string name, Vector2 size, Vector3 offset, Color c, float a)
            {
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(on, true);
                go.transform.SetPositionAndRotation(at + normal * 0.006f + offset, Quaternion.LookRotation(-normal, Vector3.Cross(along, -normal)));
                go.transform.localScale = Vector3.one;
                go.GetComponent<MeshFilter>().sharedMesh = kit.meshQuad;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = cutMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                // The quad is 1 x 1 in its own x/y; laid along the cut.
                var parentScale = on.lossyScale;
                go.transform.localScale = new Vector3(size.x / Mathf.Max(1e-3f, parentScale.x), size.y / Mathf.Max(1e-3f, parentScale.y), 1f);
                parts.Add((go.transform, r, new MaterialPropertyBlock(), c, a));
            }
            Vector3 lip = Vector3.Cross(normal, along).normalized;
            Part("Cut", new Vector2(0.32f, 0.026f), Vector3.zero, new Color(0.05f, 0.035f, 0.025f), 0.95f);
            Part("Cut edge", new Vector2(0.3f, 0.008f), lip * 0.016f, new Color(1f, 0.95f, 0.85f), 0.85f);
            var f = kit.Run(1.9f, (fx, d) =>
            {
                float fade = fx.age < 1.5f ? 1f : 1f - (fx.age - 1.5f) / 0.4f;
                float open = Kit.EaseOut(Mathf.Clamp01(fx.age / 0.05f));
                foreach (var (t, r, b, c, a) in parts)
                {
                    if (t == null) continue;
                    Color col = c;
                    col.a = a * fade * open;
                    b.SetColor(LitMaterials.BaseColorId, col);
                    r.SetPropertyBlock(b);
                }
                return true;
            });
            f.end = () => { foreach (var p in parts) if (p.t != null) Destroy(p.t.gameObject); };
        }
    }
}
