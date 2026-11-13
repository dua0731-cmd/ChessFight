using System.Collections.Generic;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The bishop's aim drawn for real (10-26, 승규 님: "비숍 미리보기도 실사로 바꿔줘"; it was rows of violet squares on its two
    /// diagonals, a lavender mitre diamond and a cream ring at its reach). What it will lay, as a see-through ghost where it
    /// will lie: the crossed wooden stakes themselves (the Tripo model it throws) and the two hemp lines they will stretch
    /// along its diagonals at ankle height, each with its soft shadow under it, a faint glint running along the lines; a
    /// soft pool of warm light on the floor under the cross. Its reach is a thin soft shadow line round the bishop. Nothing
    /// where it cannot be laid. On its own screen only, as before.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        static readonly Color HempLine = new Color(0.58f, 0.46f, 0.3f);
        static readonly Color BishopAimLight = new Color(1f, 0.85f, 0.6f);

        void RealBishopAim(RagdollPawn b)
        {
            if (!b.BishopAiming || b != Viewer) return;
            var s = Params;
            float length = s != null ? s.bishopLineLength : 4.2f, range = s != null ? s.bishopRange : 4.5f;
            float height = s != null ? s.bishopHeight : 0.12f;
            EnsureChainMeshes();
            if (crossParts.Count == 0) LoadChainModel("BishopCross", crossParts, "SkillModels");
            kit.Keep(b, "bishop real aim", () =>
            {
                var fx = new Kit.Fx();
                for (int i = 0; i < 2; i++) fx.strips.Add(TintStrip());   // the two hemp lines
                for (int i = 0; i < 2; i++) fx.strips.Add(TintStrip());   // their shadows
                fx.strips.Add(TintStrip());                               // the reach
                fx.tiles.Add(new Kit.Tile(kit, "Bishop aim light pool"));
                var pivot = new GameObject("Bishop aim ghost").transform;
                pivot.SetParent(kit.root, false);
                var ghost = new List<Kit.Prop>();
                if (crossParts.Count > 0)
                    foreach (var (mesh, colors) in crossParts)
                    {
                        var part = new Kit.Prop(kit, mesh, colors.main == Walnut.main ? StakeWood : colors, "Bishop aim ghost stake", pivot) { inkWidth = 0f };
                        part.t.localRotation = Quaternion.Euler(90f, 45f, 0f);
                        part.t.localScale = Vector3.one * 0.9f;
                        ghost.Add(part);
                    }
                else
                {
                    var rod = kit.RoundBox(new Vector3(0.07f, 0.07f, 0.9f), 0.03f);
                    for (int i = 0; i < 2; i++)
                    {
                        var r = new Kit.Prop(kit, rod, StakeWood, "Bishop aim ghost stake", pivot) { inkWidth = 0f };
                        r.t.localRotation = Quaternion.Euler(0f, i == 0 ? 45f : -45f, 0f);
                        ghost.Add(r);
                    }
                }
                fx.props.AddRange(ghost);
                fx.end = () => { if (pivot != null) Destroy(pivot.gameObject); };
                var pts = new List<Vector3>();
                var circle = new List<Vector3>();
                fx.step = (x, d) =>
                {
                    if (b == null || !kit.Held(x)) return false;
                    float fade = Mathf.Clamp01(x.age / 0.2f);
                    bool valid = b.BishopAimValid;
                    float show = valid ? fade : 0f;
                    Vector3 c = b.BishopAimPoint, yaw = Kit.FlatDir(b.BishopAimYaw, Vector3.forward);
                    Vector3 d1 = Quaternion.Euler(0f, 45f, 0f) * yaw, d2 = Quaternion.Euler(0f, -45f, 0f) * yaw;

                    // The ghost of the cross, lying on the floor.
                    pivot.SetPositionAndRotation(c + Vector3.up * 0.05f, Quaternion.LookRotation(yaw));
                    foreach (var g in ghost)
                    {
                        g.Show(valid);
                        g.fade = show * 0.75f;
                        g.flash = 0.15f + 0.1f * Mathf.Sin(kit.Clock * 5f);
                        g.Apply();
                    }

                    // The two lines it will stretch, at ankle height, a glint running out from the middle along each.
                    float run = (kit.Clock % 0.9f) / 0.9f;
                    for (int k = 0; k < 2; k++)
                    {
                        var dir = k == 0 ? d1 : d2;
                        pts.Clear();
                        for (int j = 0; j <= 12; j++)
                        {
                            float off = (j / 12f - 0.5f) * length;
                            pts.Add(Kit.FloorUnder(c + dir * off, 1f) + Vector3.up * height);
                        }
                        var rope = x.strips[k];
                        Tint(rope, Color.Lerp(HempLine, Color.white, 0.25f * Mathf.Sin(run * Mathf.PI)));
                        rope.Build(pts, j => 0.035f, kit.Eye);
                        rope.taper = 0f;
                        rope.opacity = show * 0.85f;
                        rope.Apply();
                        for (int j = 0; j < pts.Count; j++) pts[j] = Kit.FloorUnder(pts[j], 1f) + Vector3.up * 0.012f;
                        var shade = x.strips[2 + k];
                        Tint(shade, RookAimShadow);
                        shade.Build(pts, j => 0.05f, kit.Eye, true);
                        shade.taper = 0f;
                        shade.opacity = show * 0.32f;
                        shade.Apply();
                    }

                    // A soft pool of warm light under the cross.
                    var pool = x.tiles[0];
                    if (valid)
                    {
                        pool.Floor(Kit.FloorUnder(c, 1f) + Vector3.up * 0.011f, yaw, Vector2.one * 1.3f);
                        pool.shape = 2f;
                        pool.round = 1f;
                        PaintPool(pool, BishopAimLight, show * 0.35f);
                    }
                    else pool.Hide();

                    // Its reach: a thin soft shadow line round the bishop.
                    Vector3 feet = b.FeetPoint;
                    circle.Clear();
                    for (int j = 0; j <= 64; j++)
                    {
                        float ang = j * Mathf.PI * 2f / 64f;
                        circle.Add(Kit.FloorUnder(feet + new Vector3(Mathf.Cos(ang), 0.3f, Mathf.Sin(ang)) * range, 1f) + Vector3.up * 0.013f);
                    }
                    var reach = x.strips[4];
                    Tint(reach, RookAimShadow);
                    reach.Build(circle, j => 0.03f, kit.Eye, true);
                    reach.taper = 0f;
                    reach.opacity = fade * 0.18f;
                    reach.Apply();
                    return true;
                };
                return fx;
            });
        }
    }
}
