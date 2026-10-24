using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R153 (Pawn Rush skill plan v0.3, 승규 님 10-16): what the new skills show on every PC, from the pieces' skill state.
    /// - Rook "직선 사슬 견인" (RagdollPawn.RookChain.cs): while it aims, a see-through white line from its chest (everyone
    ///   sees it) to the 6 m end or the wall in the way; once thrown, a real iron chain of linked steel rings from its right
    ///   hand to the tip: a hook flying out, a T handle held out to a teammate (bobbing, with its seconds burning down as
    ///   the grip darkens), taut while it pulls, slack as it winds back. The pull's end kicks up a little floor dust.
    /// - Pawn: the one its second step would go for gets a small ring at its feet (teal a teammate, orange an enemy).
    /// - Knight: a red dashed ring over the head of the enemy it leaps at (everyone), or, on the knight's own screen
    ///   before the key, the enemy it would leap at (blinking).
    /// Rings here are previews, not hits (승규 님 10-16: the no-round rule is for hits and landings).
    /// </summary>
    public partial class PawnRushSkillFx
    {
        Mesh meshChainLink, meshChainHook, meshChainHandle, meshChainGrip;
        // The hook and the T handle (승규 님 10-16, made in Tripo, cut and lightened in Blender: Resources/PawnRushSkillFx/
        // ChainModels): their parts by name, each painted in the chess materials (walnut grip, gold-leaf band and ring,
        // ebony caps, steel hook). Missing files: the plain shapes below stand in.
        readonly List<(Mesh mesh, Kit.Palette colors)> hookParts = new List<(Mesh, Kit.Palette)>(), handleParts = new List<(Mesh, Kit.Palette)>();
        // Low "metal": full chrome mirrors the empty sky and the steel read as black (stills, 10-16).
        static readonly Kit.Palette Steel = new Kit.Palette("#9AA3AF", "#DDE3EA", "#5D6570", "#22262D") { metal = 0.3f };
        static readonly Kit.Palette GoldLeaf = new Kit.Palette("#D9A441", "#FFE6A0", "#9A6A1C", "#3A2208") { metal = 0.45f };
        static readonly Kit.Palette Walnut = new Kit.Palette("#6E4A2A", "#9C7048", "#40281A", "#1A0F07");
        static readonly Kit.Palette Ebony = new Kit.Palette("#2E2824", "#5E544D", "#161210", "#050403") { metal = 0.2f };
        // R153 4th: the stake's iron band (dark forged iron) and the cross stake's rope (hemp, as the trap's rope).
        static readonly Kit.Palette Iron = new Kit.Palette("#4A4A50", "#8A8A92", "#26262B", "#0E0E12") { metal = 0.35f };
        static readonly Kit.Palette Hemp = new Kit.Palette("#B08E5E", "#D9BC8C", "#6E5333", "#2E2114");
        static readonly Kit.Palette PawnAlly = new Kit.Palette("#2EC4C6", "#D8FFFB", "#167E80", "#0B3D3E");
        static readonly Kit.Palette PawnFoe = new Kit.Palette("#F28C28", "#FFE2BF", "#B85E10", "#3E1E04");
        const float LinkPitch = 0.1f;

        /// <summary>The piece played at this PC (P1 offline, this PC's own piece online).</summary>
        RagdollPawn Me => game != null ? game.LocalPawn : null;

        void EnsureChainMeshes()
        {
            if (meshChainLink != null) return;
            meshChainLink = OvalRing(0.06f, 0.036f, 0.013f, 14, 6, "Chain link");
            meshChainHook = kit.RoundBox(new Vector3(0.05f, 0.05f, 0.14f), 0.02f);
            meshChainHandle = kit.RoundBox(new Vector3(0.36f, 0.04f, 0.04f), 0.018f);
            meshChainGrip = kit.RoundBox(new Vector3(0.14f, 0.055f, 0.055f), 0.025f);
            LoadChainModel("RookChainHook", hookParts);
            LoadChainModel("RookChainHandle", handleParts);
        }

        /// <summary>A Tripo part set: each child mesh, painted by its name (Wood, Gold, Cap, Steel).</summary>
        static void LoadChainModel(string name, List<(Mesh, Kit.Palette)> into, string folder = "ChainModels")
        {
            into.Clear();
            var go = Resources.Load<GameObject>("PawnRushSkillFx/" + folder + "/" + name);
            if (go == null) return;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Kit.Palette colors = mf.name switch
                {
                    "Wood" => Walnut,
                    "Gold" => GoldLeaf,
                    "Cap" => Ebony,
                    "Band" => Iron,
                    "Rope" => Hemp,
                    _ => Steel,
                };
                into.Add((mf.sharedMesh, colors));
            }
        }

        void DestroyChainMeshes()
        {
            foreach (var m in new[] { meshChainLink })
                if (m != null) Destroy(m);
            meshChainLink = null;
        }

        /// <summary>A stretched ring (a chain link): <paramref name="halfLength"/> along z, <paramref name="halfWidth"/> along x,
        /// a round tube of radius <paramref name="tube"/>.</summary>
        static Mesh OvalRing(float halfLength, float halfWidth, float tube, int around, int roundTube, string name)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= around; i++)
            {
                float a = i * Mathf.PI * 2f / around;
                // The centre line: two straight sides joined by half circles (a stadium).
                Vector3 c = new Vector3(Mathf.Cos(a) * halfWidth, 0f, Mathf.Sin(a) * halfLength);
                Vector3 outward = new Vector3(Mathf.Cos(a) / halfWidth, 0f, Mathf.Sin(a) / halfLength).normalized;
                for (int j = 0; j <= roundTube; j++)
                {
                    float b = j * Mathf.PI * 2f / roundTube;
                    Vector3 n = outward * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    verts.Add(c + n * tube);
                    norms.Add(n);
                }
            }
            int ring = roundTube + 1;
            for (int i = 0; i < around; i++)
                for (int j = 0; j < roundTube; j++)
                {
                    int a0 = i * ring + j, a1 = a0 + 1, b0 = a0 + ring, b1 = b0 + 1;
                    tris.Add(a0); tris.Add(a1); tris.Add(b0);
                    tris.Add(a1); tris.Add(b1); tris.Add(b0);
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------------- rook

        void RookChainFx(RagdollPawn r)
        {
            var s = Params;
            if (r.SkillStage == SkillStage.Windup && r.RookChainPhase == ChainPhase.None) ChainAimLine(r, s);
            if (r.RookChainPhase != ChainPhase.None) ChainLinks(r, s);
        }

        /// <summary>
        /// The aim (R153, 승규 님 10-22: "미리보기를 사슬 같이 해줬으면 좋겠어"): a ghost of the chain itself lies along the aim,
        /// from the chest to the piece it will take (or the range, or the wall in the way): see-through steel links turned a
        /// quarter each, a light running out along them from the rook toward the target over and over; firmer once the click
        /// has fixed it. Its dark shadow lies straight under it on the floor, and the piece it will take is ringed at its feet
        /// and over its head (an enemy orange, a teammate teal), the links' edges lit in that colour.
        /// </summary>
        void ChainAimLine(RagdollPawn r, PawnRushSkillParams s)
        {
            float range = s != null ? s.rookChainRange : 8f;
            EnsureChainMeshes();
            kit.Keep(r, "rook chain aim", () =>
            {
                var fx = new Kit.Fx();
                fx.strips.Add(new Kit.Strip(kit, kit.grey));   // its shadow on the floor
                fx.tiles.Add(new Kit.Tile(kit, "Rook chain target"));
                fx.tiles.Add(new Kit.Tile(kit, "Rook chain target head"));
                int most = Mathf.CeilToInt((range + 0.5f) / LinkPitch);
                for (int i = 0; i < most; i++)
                {
                    var link = new Kit.Prop(kit, meshChainLink, Steel, "Rook chain aim link") { inkWidth = 0f };
                    link.Show(false);
                    fx.props.Add(link);
                }
                fx.step = (x, d) =>
                {
                    if (r == null || !kit.Held(x)) return false;
                    Vector3 dir = r.SkillDirection.sqrMagnitude > 1e-4f ? r.SkillDirection.normalized : r.Facing;
                    Vector3 from = r.bodies[(int)BodyId.Chest].position + Kit.FlatDir(dir, r.Facing) * 0.2f;
                    var target = r.RookChainAimTarget;
                    float length = range;
                    if (target != null) length = Mathf.Min(range, Vector3.Distance(from, target.bodies[(int)BodyId.Chest].position));
                    else if (Physics.Raycast(from, dir, out var hit, range, ~0, QueryTriggerInteraction.Ignore) && !RagdollPawn.ColliderOwner.ContainsKey(hit.collider))
                        length = hit.distance;
                    bool locked = !r.SkillAiming;
                    float fade = Mathf.Clamp01(x.age / (4f * Frame));
                    var ink = new Color(0.05f, 0.08f, 0.19f);
                    Kit.Palette? side = target == null ? (Kit.Palette?)null : OnSide(r, target) ? PawnAlly : PawnFoe;

                    // The ghost chain.
                    Vector3 across = Vector3.Cross(Vector3.up, dir);
                    if (across.sqrMagnitude < 1e-4f) across = Vector3.right;
                    Quaternion along = Quaternion.LookRotation(dir, across);
                    int n = Mathf.Min(most, Mathf.FloorToInt(length / LinkPitch));
                    // A light running out along the links, from the rook to the target, every 0.6 s.
                    float run = (kit.Clock % 0.6f) / 0.6f * Mathf.Max(0.5f, length);
                    for (int i = 0; i < most; i++)
                    {
                        var link = fx.props[i];
                        if (i >= n) { link.Show(false); continue; }
                        float at = (i + 0.5f) * LinkPitch;
                        link.t.SetPositionAndRotation(from + dir * at, along * Quaternion.Euler(0f, 0f, (i % 2) * 90f));
                        link.t.localScale = Vector3.one * 0.9f;
                        float glint = Mathf.Clamp01(1f - Mathf.Abs(at - run) / 0.5f);
                        link.flash = 0.55f * glint;
                        link.rim = side.HasValue ? Kit.A(side.Value.main, 1f) * (0.6f + 0.8f * glint) : Color.black;
                        link.fade = fade * (locked ? 0.95f : 0.6f);
                        link.Show(true);
                        link.Apply();
                    }

                    // Its shadow straight under it on the floor.
                    var shadow = x.strips[0];
                    Vector3 a = Kit.FloorUnder(from, 1.5f) + Vector3.up * 0.02f, b = Kit.FloorUnder(from + dir * length, 3f) + Vector3.up * 0.02f;
                    shadow.colors = new Kit.Palette { main = ink, light = ink, deep = ink, ink = ink };
                    shadow.core = ink;
                    shadow.Build(new List<Vector3> { a, b }, k => 0.09f, kit.Eye, true);
                    shadow.taper = 0f;
                    shadow.opacity = 0.3f;
                    shadow.fade = fade;
                    shadow.Apply();

                    // The piece it will take: a ring at its feet and one over its head.
                    var ring = x.tiles[0];
                    var over = x.tiles[1];
                    if (target != null)
                    {
                        var p = side.Value;
                        Vector3 feet = Kit.FloorUnder(target.Hips.position, 1.5f) + Vector3.up * 0.02f;
                        ring.Floor(feet, Kit.FlatDir(target.Hips.position - r.Hips.position, Vector3.forward), Vector2.one * (1.3f + 0.1f * Mathf.Sin(kit.Clock * 10f)));
                        PaintTargetRing(ring, p, ink, 0.76f, fade);
                        Vector3 head = target.bodies[(int)BodyId.Head].position + Vector3.up * 0.38f;
                        over.Floor(head, Quaternion.Euler(0f, kit.Clock * 60f, 0f) * Vector3.forward, Vector2.one * 0.6f);
                        PaintTargetRing(over, p, ink, 0.7f, fade);
                    }
                    else
                    {
                        ring.Hide();
                        over.Hide();
                    }
                    return true;
                };
                return fx;
            });
        }

        static void PaintTargetRing(Kit.Tile t, Kit.Palette p, Color ink, float inner, float fade)
        {
            t.shape = 1f;
            t.inner = inner;
            t.fill = Kit.A(p.main, 1f);
            t.core = Kit.A(p.light, 0.9f);
            t.coreWidth = 0.02f;
            t.ink = Kit.A(ink, 1f);
            t.inkWidth = 0.03f;
            t.rim = Color.clear;
            t.stripe = Color.clear;
            t.arc = 1f;
            t.fade = fade;
            t.Apply();
        }

        /// <summary>The thrown chain: steel links from the hand to the tip, each turned a quarter from the last, sagging a
        /// little while a handle is held out or it winds back; a hook on the tip in flight, a T handle held out to a teammate.</summary>
        void ChainLinks(RagdollPawn r, PawnRushSkillParams s)
        {
            EnsureChainMeshes();
            kit.Keep(r, "rook chain", () =>
            {
                var fx = new Kit.Fx();
                var steel = Steel;
                int most = Mathf.CeilToInt(((s != null ? s.rookChainRange : 6f) + 1.5f) / LinkPitch);
                for (int i = 0; i < most; i++)
                {
                    var link = new Kit.Prop(kit, meshChainLink, steel, "Chain link") { inkWidth = 0f };   // no ink hull: on a 1.3 cm bar it swallowed the steel (it read black)
                    link.Show(false);
                    fx.props.Add(link);
                }
                // The hook (ring at the tip, the hook running on ahead) and the T handle (its ring at the tip, the grip
                // above, across the chain): the Tripo parts, or plain shapes when they are missing.
                var hook = new List<Kit.Prop>();
                if (hookParts.Count > 0)
                {
                    foreach (var (mesh, colors) in hookParts) hook.Add(new Kit.Prop(kit, mesh, colors, "Chain hook") { inkWidth = 0.004f });
                    foreach (var h in hook) h.t.localScale = Vector3.one * 1.4f;   // 0.42 m: it reads at play distance
                }
                else hook.Add(new Kit.Prop(kit, meshChainHook, steel, "Chain hook") { inkWidth = 0.01f });
                var handle = new List<Kit.Prop>();
                if (handleParts.Count > 0)
                    foreach (var (mesh, colors) in handleParts) handle.Add(new Kit.Prop(kit, mesh, colors, "Chain handle") { inkWidth = 0.004f });
                else
                {
                    handle.Add(new Kit.Prop(kit, meshChainHandle, steel, "Chain handle") { inkWidth = 0.01f });
                    handle.Add(new Kit.Prop(kit, meshChainGrip, Walnut, "Chain grip") { inkWidth = 0.01f });
                }
                fx.props.AddRange(hook);
                fx.props.AddRange(handle);
                var rope = new ChainRope();
                fx.step = (x, d) =>
                {
                    if (r == null || !kit.Held(x)) return false;
                    var phase = r.RookChainPhase;
                    Vector3 a = r.RookChainHand, b = r.RookChainTip;
                    Vector3 span = b - a;
                    float length = span.magnitude;
                    Vector3 dir = length > 1e-3f ? span / length : r.Facing;
                    // R153 2nd (승규 님: "체인을 좀 더 유동성 있게"): a hanging rope (Verlet) between the hand and the tip, so
                    // the chain trails and waves as it flies out, swings and sags while the handle is held out, snaps
                    // straight as it pulls and whips slack as it winds back. Drawn here only (every PC simulates its own).
                    float slack = phase == ChainPhase.Offer ? 1.12f : phase == ChainPhase.Rewind ? 1.3f : phase == ChainPhase.Pull ? 1.0f : 1.02f;
                    rope.Step(a, b, length * slack, d);
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
                    int n = 0;
                    for (float along = LinkPitch * 0.5f; n < most && along < rope.Length; along += LinkPitch, n++)
                    {
                        var link = fx.props[n];
                        rope.At(along, out var at, out var tangent);
                        link.t.position = at;
                        link.t.rotation = Quaternion.LookRotation(tangent, side) * Quaternion.Euler(0f, 0f, (n % 2) * 90f);
                        link.t.localScale = Vector3.one;
                        link.Show(true);
                    }
                    for (int i = n; i < most; i++) fx.props[i].Show(false);
                    // The hook points along the chain's last links (it swings with them).
                    dir = rope.TipTangent(dir);
                    bool hooked = phase == ChainPhase.Fly || phase == ChainPhase.Rewind || (phase == ChainPhase.Pull && !OnChainAlly(r));
                    Vector3 flatDir = Kit.FlatDir(dir, r.Facing);
                    foreach (var h in hook)
                    {
                        h.t.position = b;
                        h.t.rotation = Quaternion.LookRotation(dir, Vector3.up);
                        h.Show(hooked);
                    }
                    bool offer = phase == ChainPhase.Offer || (phase == ChainPhase.Pull && OnChainAlly(r));
                    // Held out, the handle bobs and its gold flashes faster as its seconds run down.
                    float bob = phase == ChainPhase.Offer ? 0.03f * Mathf.Sin(kit.Clock * 6f) : 0f;
                    float left = s != null && phase == ChainPhase.Offer ? Mathf.Clamp01(r.RookOfferLeft / Mathf.Max(0.1f, s.rookAllyWindow)) : 1f;
                    float flash = phase == ChainPhase.Offer ? 0.3f * (0.5f + 0.5f * Mathf.Sin(kit.Clock * (6f + 10f * (1f - left)))) : 0f;
                    foreach (var h in handle)
                    {
                        h.t.position = b + Vector3.up * bob;
                        h.t.rotation = Quaternion.LookRotation(flatDir, Vector3.up);
                        h.flash = flash;
                        h.Apply();
                        h.Show(offer);
                    }
                    return true;
                };
                return fx;
            });
        }

        /// <summary>
        /// The chain as a hanging rope (R153 2nd): points joined by fixed lengths (Verlet), gravity, a little damping, the
        /// floor under them; both ends pinned (the hand, the tip). Its length follows the chain out and back in; new points
        /// come in at the hand, the rest keep the shape they have.
        /// </summary>
        sealed class ChainRope
        {
            const float Seg = 0.22f;
            readonly List<Vector3> p = new List<Vector3>(), prev = new List<Vector3>();
            float seg = Seg;
            static readonly Vector3 Gravity = new Vector3(0f, -9.81f, 0f);

            public float Length => seg * Mathf.Max(0, p.Count - 1);

            public void Step(Vector3 a, Vector3 b, float length, float dt)
            {
                int want = Mathf.Clamp(Mathf.CeilToInt(length / Seg) + 1, 3, 80);
                if (p.Count == 0)
                    for (int i = 0; i < want; i++)
                    {
                        Vector3 q = Vector3.Lerp(a, b, i / (want - 1f));
                        p.Add(q);
                        prev.Add(q);
                    }
                while (p.Count < want) { p.Insert(1, p[0]); prev.Insert(1, prev[0]); }
                while (p.Count > want) { p.RemoveAt(1); prev.RemoveAt(1); }
                seg = Mathf.Max(0.02f, length / (p.Count - 1));
                if (dt > 0f)
                {
                    float h = Mathf.Min(dt, 1f / 30f);
                    for (int i = 1; i < p.Count - 1; i++)
                    {
                        Vector3 v = (p[i] - prev[i]) * 0.985f;
                        prev[i] = p[i];
                        p[i] = p[i] + v + Gravity * (h * h);
                    }
                }
                p[0] = prev[0] = a;
                p[p.Count - 1] = prev[p.Count - 1] = b;
                for (int k = 0; k < 14; k++)
                {
                    for (int i = 0; i < p.Count - 1; i++)
                    {
                        Vector3 d = p[i + 1] - p[i];
                        float m = d.magnitude;
                        if (m < 1e-5f) continue;
                        Vector3 fix = d * ((m - seg) / m);
                        bool pinA = i == 0, pinB = i + 1 == p.Count - 1;
                        if (pinA && pinB) continue;
                        if (pinA) p[i + 1] -= fix;
                        else if (pinB) p[i] += fix;
                        else { p[i] += fix * 0.5f; p[i + 1] -= fix * 0.5f; }
                    }
                    p[0] = a;
                    p[p.Count - 1] = b;
                }
                // The floor: the links lie on it, they do not sink through.
                for (int i = 1; i < p.Count - 1; i++)
                {
                    Vector3 q = p[i];
                    if (Physics.Raycast(q + Vector3.up * 0.4f, Vector3.down, out var hit, 0.6f, ~0, QueryTriggerInteraction.Ignore)
                        && !RagdollPawn.ColliderOwner.ContainsKey(hit.collider) && q.y < hit.point.y + 0.03f)
                    {
                        q.y = hit.point.y + 0.03f;
                        p[i] = q;
                    }
                }
            }

            /// <summary>The point and the way along the rope <paramref name="along"/> metres from the hand.</summary>
            public void At(float along, out Vector3 at, out Vector3 tangent)
            {
                float u = along / seg;
                int i = Mathf.Clamp(Mathf.FloorToInt(u), 0, p.Count - 2);
                float f = Mathf.Clamp01(u - i);
                at = Vector3.Lerp(p[i], p[i + 1], f);
                tangent = p[i + 1] - p[i];
                tangent = tangent.sqrMagnitude > 1e-8f ? tangent.normalized : Vector3.forward;
            }

            public Vector3 TipTangent(Vector3 fallback)
            {
                if (p.Count < 2) return fallback;
                Vector3 t = p[p.Count - 1] - p[p.Count - 2];
                return t.sqrMagnitude > 1e-6f ? t.normalized : fallback;
            }
        }

        static bool OnSide(RagdollPawn a, RagdollPawn b) => a.Team != Gameplay.Teams.None && a.Team == b.Team;

        static bool OnChainAlly(RagdollPawn rook) => rook.RookChainOn != null && rook.RookChainOn.Team == rook.Team && rook.Team != Gameplay.Teams.None;

        void RookThrowFx(SkillFxEvent e)
        {
            if (kit == null) return;
            // The swing kicks a puff of air off the hand along the line.
            kit.PuffBurst(e.at + e.dir * 0.3f, kit.dust, 2, 0.08f, 0.1f, 0.25f, 0.05f);
        }

        void RookHookFx(SkillFxEvent e)
        {
            if (kit == null || e.target == null) return;
            if (e.count == 0)
            {
                // An enemy hooked: the chain bites (a short stop and shake on the two pieces' screens).
                HitStop(2f * Frame);
                Shake(0.04f, 4f * Frame);
            }
        }

        void RookPullEndFx(SkillFxEvent e)
        {
            if (kit == null) return;
            kit.DustRing(e.at + Vector3.up * 0.02f, e.count == 1 ? 3 : 4, 0.5f, kit.dust, 0.18f, 0.4f);
        }

        // ---------------------------------------------------------------- pawn and knight targets

        /// <summary>The pawn's second-step target: a small ring at its feet while the first step and the link last.</summary>
        void PawnTargetRing(RagdollPawn pawn)
        {
            var target = pawn.PawnTarget;
            bool on = target != null && (pawn.SkillStage == SkillStage.Link || (pawn.SkillStage == SkillStage.Active && pawn.PawnStepNumber == 1));
            if (!on) return;
            kit.Keep(pawn, "pawn target", () =>
            {
                var fx = new Kit.Fx();
                fx.tiles.Add(new Kit.Tile(kit, "Pawn target ring"));
                fx.step = (x, d) =>
                {
                    var t = pawn != null ? pawn.PawnTarget : null;
                    if (t == null || !kit.Held(x)) return false;
                    bool ally = t.Team == pawn.Team && pawn.Team != Gameplay.Teams.None;
                    var p = ally ? PawnAlly : PawnFoe;
                    var ring = x.tiles[0];
                    Vector3 feet = Kit.FloorUnder(t.Hips.position, 1.5f) + Vector3.up * 0.02f;
                    ring.Floor(feet, Kit.FlatDir(t.Hips.position - pawn.Hips.position, Vector3.forward), Vector2.one * (0.9f * Kit.Pop(x.age, 1.15f)));
                    ring.shape = 1f;
                    ring.inner = 0.78f;
                    ring.fill = Kit.A(p.main, 0.9f);
                    ring.core = Kit.A(p.light, 0.9f);
                    ring.coreWidth = 0.02f;
                    ring.ink = Kit.A(p.ink, 1f);
                    ring.inkWidth = 0.02f;
                    ring.rim = Color.clear;
                    ring.stripe = Color.clear;
                    ring.arc = 1f;
                    ring.fade = 1f;
                    ring.Apply();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>The knight's target: a red dashed ring over its head (eight short arcs turning slowly), steady once the
        /// knight is on its way, blinking while only found (that one on the knight's own screen only).</summary>
        void KnightTargetRing(RagdollPawn k)
        {
            var target = k.KnightTarget;
            bool own = k == Me;
            if (target == null && !(own && k.SkillStage == SkillStage.None && k.KnightMarked != null)) return;
            kit.Keep(k, "knight target ring", () =>
            {
                var fx = new Kit.Fx();
                for (int i = 0; i < 8; i++) fx.tiles.Add(new Kit.Tile(kit, "Knight target dash"));
                fx.step = (x, d) =>
                {
                    if (k == null || !kit.Held(x)) return false;
                    var m = k.KnightTarget != null ? k.KnightTarget : k.KnightMarked;
                    if (m == null) return false;
                    bool locked = k.KnightTarget != null;
                    float blink = locked ? 1f : 0.55f + 0.45f * Mathf.Sin(kit.Clock * 14f);
                    var p = board ? BOrange : kit.enemy;
                    Vector3 over = m.bodies[(int)BodyId.Head].position + Vector3.up * 0.42f;
                    float size = 0.75f * Kit.Pop(x.age, 1.15f);
                    for (int i = 0; i < x.tiles.Count; i++)
                    {
                        var t = x.tiles[i];
                        float ang = i * 45f + kit.Clock * 40f;
                        t.Floor(over, Quaternion.Euler(0f, ang, 0f) * Vector3.forward, Vector2.one * size);
                        t.shape = 1f;
                        t.inner = 0.8f;
                        t.fill = Kit.A(kit.enemy.main, blink);
                        t.core = Kit.A(kit.enemy.light, 0.8f * blink);
                        t.coreWidth = 0.015f;
                        t.ink = Kit.A(p.ink, blink);
                        t.inkWidth = 0.02f;
                        t.rim = Color.clear;
                        t.stripe = Color.clear;
                        t.arc = 0.075f;
                        t.fade = 1f;
                        t.Apply();
                    }
                    return true;
                };
                return fx;
            });
        }
    }
}
