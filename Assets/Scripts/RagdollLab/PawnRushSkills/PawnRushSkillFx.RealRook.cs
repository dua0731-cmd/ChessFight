using System.Collections.Generic;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The rook's chain aim drawn for real (10-25, 승규 님: "룩 미리보기 실사로 바꿔줘"; it was R153's ghost chain with coloured
    /// edges, a dark shadow strip and toon rings at the target's feet and over its head). 10-26 (승규 님: "은은한 쇠사슬이
    /// 목표까지 이어지는 것을 좀더 미리보기 다웠으면 좋겠고 … 우리편 상대편 구분지어서 타겟해주고 최대한 실사적인 VFX 로"; the
    /// spotlight, laser and lit-square looks were dropped): the chain itself, see-through steel, reaching out link by link
    /// from the rook to where it will go each time the aim starts or finds a new piece, a bright glint running out along it
    /// over and over (the throw it is about to make), a see-through hook at its end; its soft shadow on the floor. The piece
    /// it will take is marked as a light would mark it: a pool of light on the floor under it and a ring of light drawing
    /// tight round its feet, its body lit from above - red for an enemy (10-26, 승규 님: "상대편에 걸 때 빨간색이면 좋겠고"), blue for a teammate - and the chain's last
    /// links catch that light. Firmer once the click has fixed it. No ink, no rings in the air.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        static readonly Color RookAimFoe = new Color(1f, 0.1f, 0.07f);
        static readonly Color RookAimAlly = new Color(0.36f, 0.68f, 1f);
        static readonly Color RookAimShadow = new Color(0.16f, 0.14f, 0.13f);

        void RealRookAim(RagdollPawn r, PawnRushSkillParams s)
        {
            float range = s != null ? s.rookChainRange : 8f;
            EnsureChainMeshes();
            kit.Keep(r, "rook real aim", () =>
            {
                var fx = new Kit.Fx();
                fx.strips.Add(TintStrip());    // its soft shadow on the floor
                fx.strips.Add(TintStrip());    // the ring of light round the target's feet
                fx.tiles.Add(new Kit.Tile(kit, "Rook aim light pool"));
                int most = Mathf.CeilToInt((range + 0.5f) / LinkPitch);
                for (int i = 0; i < most; i++)
                {
                    var link = new Kit.Prop(kit, meshChainLink, Steel, "Rook real aim link") { inkWidth = 0f };
                    link.Show(false);
                    fx.props.Add(link);
                }
                // The see-through hook at its end.
                var hook = new List<Kit.Prop>();
                if (hookParts.Count > 0)
                    foreach (var (mesh, colors) in hookParts) hook.Add(new Kit.Prop(kit, mesh, colors, "Rook real aim hook") { inkWidth = 0f });
                else hook.Add(new Kit.Prop(kit, meshChainHook, Steel, "Rook real aim hook") { inkWidth = 0f });
                foreach (var h in hook) { h.Show(false); fx.props.Add(h); }
                var lamp = new GameObject("Rook real aim light").AddComponent<Light>();
                lamp.transform.SetParent(root, false);
                lamp.type = LightType.Point;
                lamp.shadows = LightShadows.None;
                lamp.range = 2.2f;
                lamp.intensity = 0f;
                fx.end = () => { if (lamp != null) Destroy(lamp.gameObject); };
                var line = new List<Vector3>();
                var circle = new List<Vector3>();
                RagdollPawn marked = null;
                float reachStart = 0f, markStart = 0f;
                fx.step = (x, d) =>
                {
                    if (r == null || !kit.Held(x)) return false;
                    Vector3 dir = r.SkillDirection.sqrMagnitude > 1e-4f ? r.SkillDirection.normalized : r.Facing;
                    Vector3 from = r.bodies[(int)BodyId.Chest].position + Kit.FlatDir(dir, r.Facing) * 0.2f;
                    var target = r.RookChainAimTarget;
                    float length = range;
                    if (target != null) length = Mathf.Min(range, Vector3.Distance(from, target.bodies[(int)BodyId.Chest].position) - 0.15f);
                    else if (Physics.Raycast(from, dir, out var hit, range, ~0, QueryTriggerInteraction.Ignore) && !RagdollPawn.ColliderOwner.ContainsKey(hit.collider))
                        length = hit.distance;
                    bool locked = !r.SkillAiming;
                    float fade = Mathf.Clamp01(x.age / (4f * Frame));
                    if (target != marked)
                    {
                        // A new piece (or none): the chain reaches out again and the ring draws tight round it.
                        marked = target;
                        reachStart = x.age;
                        markStart = x.age;
                    }
                    Color side = target == null ? Color.clear : OnSide(r, target) ? RookAimAlly : RookAimFoe;

                    // The chain reaching out link by link over 0.16 s, then a glint running out along it every 0.55 s.
                    float reach = length * Kit.EaseOut((x.age - reachStart) / 0.16f);
                    Vector3 across = Vector3.Cross(Vector3.up, dir);
                    if (across.sqrMagnitude < 1e-4f) across = Vector3.right;
                    Quaternion along = Quaternion.LookRotation(dir, across);
                    int n = Mathf.Min(most, Mathf.FloorToInt(reach / LinkPitch));
                    float run = (kit.Clock % 0.55f) / 0.55f * Mathf.Max(0.5f, length + 0.6f) - 0.3f;
                    for (int i = 0; i < most; i++)
                    {
                        var link = fx.props[i];
                        if (i >= n) { link.Show(false); continue; }
                        float at = (i + 0.5f) * LinkPitch;
                        link.t.SetPositionAndRotation(from + dir * at, along * Quaternion.Euler(0f, 0f, (i % 2) * 90f));
                        link.t.localScale = Vector3.one;
                        float glint = Mathf.Clamp01(1f - Mathf.Abs(at - run) / 0.3f);
                        link.flash = 0.85f * glint * glint;
                        // The last metre catches the target's light.
                        float near = target != null ? Mathf.Clamp01(1f - (length - at) / 1f) : 0f;
                        link.rim = Kit.A(side, 1f) * (0.9f * near);
                        link.fade = fade * (locked ? 0.8f : 0.55f) * (0.8f + 0.2f * glint);
                        link.Show(true);
                        link.Apply();
                    }
                    // The hook at the end once the chain has reached it.
                    bool reached = reach >= length - 0.01f;
                    Vector3 tip = from + dir * (n * LinkPitch + 0.06f);
                    foreach (var h in hook)
                    {
                        h.Show(reached);
                        if (!reached) continue;
                        h.t.SetPositionAndRotation(tip, along);
                        h.t.localScale = Vector3.one * (hookParts.Count > 0 ? 1.4f : 1f);
                        h.rim = Kit.A(side, 1f) * 0.9f;
                        h.flash = 0.25f * Mathf.Clamp01(1f - Mathf.Abs(length - run) / 0.3f);
                        h.fade = fade * (locked ? 0.8f : 0.55f);
                        h.Apply();
                    }

                    // Its soft shadow on the floor, under where the chain will go (a line of light is lost on the white floor).
                    var floorLine = x.strips[0];
                    Tint(floorLine, RookAimShadow);
                    line.Clear();
                    Vector3 flat = Kit.FlatDir(dir, r.Facing);
                    float flatLength = reach * Mathf.Max(0.2f, Vector3.Dot(dir, flat));
                    for (int j = 0; j <= 20; j++)
                        line.Add(Kit.FloorUnder(r.Hips.position + flat * (0.3f + Mathf.Max(0f, flatLength - 0.3f) * j / 20f), 2.5f) + Vector3.up * 0.014f);
                    floorLine.Build(line, j => 0.07f * (1f - 0.4f * j / 20f), kit.Eye, true);
                    floorLine.taper = 0f;
                    floorLine.opacity = fade * (locked ? 0.32f : 0.22f);
                    floorLine.Apply();

                    // The piece it will take: a pool of light under it, a ring of light drawing tight round its feet, its body lit.
                    var pool = x.tiles[0];
                    var ring = x.strips[1];
                    if (target != null)
                    {
                        Vector3 feet = Kit.FloorUnder(target.Hips.position, 1.5f);
                        float breathe = 0.85f + 0.15f * Mathf.Sin(kit.Clock * 6f);
                        float tight = Kit.EaseOut((x.age - markStart) / 0.22f);
                        pool.Floor(feet + Vector3.up * 0.012f, flat, Vector2.one * 1.5f);
                        pool.shape = 2f;
                        pool.round = 1f;
                        PaintPool(pool, side, fade * breathe * (locked ? 0.42f : 0.3f));
                        float radius = Mathf.Lerp(1.1f, 0.55f, tight);
                        circle.Clear();
                        for (int j = 0; j <= 48; j++)
                        {
                            float ang = j * Mathf.PI * 2f / 48f;
                            circle.Add(Kit.FloorUnder(feet + new Vector3(Mathf.Cos(ang), 0.3f, Mathf.Sin(ang)) * radius, 1f) + Vector3.up * 0.016f);
                        }
                        Tint(ring, side);
                        ring.Build(circle, j => 0.05f, kit.Eye, true);
                        ring.taper = 0f;
                        ring.opacity = fade * (locked ? 1f : 0.8f) * Mathf.Lerp(0.4f, 1f, tight);
                        ring.Apply();
                        lamp.color = side;
                        lamp.transform.position = target.bodies[(int)BodyId.Head].position + Vector3.up * 0.45f - flat * 0.25f;
                        lamp.intensity = fade * breathe * (locked ? 2.6f : 1.8f);
                    }
                    else
                    {
                        pool.Hide();
                        ring.Hide();
                        lamp.intensity = 0f;
                    }
                    return true;
                };
                return fx;
            });
        }

        Kit.Strip TintStrip() => LightStrip();

        static void Tint(Kit.Strip st, Color c)
        {
            st.colors = new Kit.Palette { main = c, light = c, deep = c, ink = c };
            st.core = c;
        }

        /// <summary>A pool of light lying on the floor: <paramref name="c"/> at <paramref name="alpha"/> in the middle, no ink,
        /// no walls, no stripes.</summary>
        static void PaintPool(Kit.Tile t, Color c, float alpha)
        {
            t.fill = Kit.A(c, alpha);
            t.core = Color.clear;
            t.ink = Color.clear;
            t.inkWidth = 0f;
            t.rim = Color.clear;
            t.stripe = Color.clear;
            t.chevron = Color.clear;
            t.baseColor = Color.clear;
            t.bevel = Color.clear;
            t.thick = 0f;
            t.shadow = 0f;
            t.metal = 0f;
            t.arc = 1f;
            t.fade = 1f;
            t.Apply();
        }
    }
}
