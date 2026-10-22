using System.Collections.Generic;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The knight's leap told as a horse's (R138, 승규 님 10-14: "나이트 스킬 슬 때 엉덩이 부분에 흰색을 뺴주고 좀 더 나이트다!
    /// 라는 연출을 보여줘. 그리고 착지 할 때 지금 있는 원 모양 말고 마지막 사진 같은 로고로 해줘"): it rears and flies in a
    /// jumping horse's pose (RagdollPawn.PawnRushSkills SkillPose), its hooves leave prints, and its landing mark is the
    /// horseshoe badge (Skill Tile shape 4, the picture's logo redrawn) with an arrow over it.
    /// R139 (승규 님 10-14, with pictures of the chess knight that rose behind it and of the badge: "나이트 F 스킬 쓸 때 뒤에 말
    /// 같은 거 지워주고 뭘 생성은 하지마 그냥 vfx만 다른 연출로 해줘. 그리고 3번 째 사진 착지 표시 지금 완전 다른 그래픽이야
    /// 테두리는 광택나는 검은 색 안에는 광택나는 어두운 금색으로 해줘야지. 그리고 머리를 밟을 때나 꺾을 때 하지마 착지 할 때만
    /// 나오게 해줘 밟을 때 나오는 vfx도 다른 그래픽 디자인으로 만들어줘 밟았다! 라는 느낌으로"):
    /// - Nothing is made any more (the chess knight that rose behind it is gone), and nothing round either (a ring of
    ///   bent air at its hooves, the first take, read as the circle the badge replaced and a soap bubble): its effects are
    ///   force lines, brush strokes of black lacquer with a dark gold core. The take-off: its hind hooves kick back, four
    ///   of them shooting back low from its heels, with the prints where they pushed off and a little dust kicked back;
    ///   kicking off the air ("당"): three shooting back from its hooves.
    /// - The badge is the landing's only (none on a head it stamps on, none on the air where it turns): glossy black
    ///   lacquer round it, in its keyhole and its nail holes, polished dark gold inside (Skill Tile Board _Gloss), its sheen
    ///   sweeping across as it stamps; the arrow over it white with a black edge.
    /// - The stomp ("밟았다!"): R140 (승규 님, of the strokes slamming down and the black flash: "나이트 위에 사진 좀 어색해
    ///   자연스러운 걸로 바꿔줘") told by the bodies, not drawn over them: the head is squashed flat and springs back, a puff
    ///   of dust squeezed out level from under the hooves, a little dust kicked up round the piece's feet as it is driven
    ///   down, the usual white-then-colour hit flash, a 6-frame stop and a hard shake. (R139's five strokes with two out
    ///   sideways and the white-then-black flash read as drawn over the picture.)
    /// - R141 (승규 님 10-14, Pawn Rush and Queen of the Hill both: "f 키를 누르면 뒤에(엉덩이 부분)에 임펙트가 나가는데 다른
    ///   임펙트로 해줘(공기 임팩트)"): the force lines are gone from the take-off and from kicking off the air too: the air
    ///   its spring shoves back from behind its hips (SkillAir: bent air, white vapour; PawnRushSkillFx.AirKick).
    /// </summary>
    public partial class PawnRushSkillFx
    {
        Mesh meshArrow;
        // R153 4th: the bishop's thrown cross stake (Tripo, PawnRushSkillFx.DesignA BishopThrowProp). The Tripo horseshoe was
        // tried on the knight's landing mark and taken back (승규 님 10-22: "나이트는 전에 있는 코드로 하는 게 좋다").
        readonly List<(Mesh mesh, Kit.Palette colors)> crossParts = new List<(Mesh, Kit.Palette)>();

        /// <summary>R139: the badge's polished dark gold and the black lacquer round it (the board look's metal palette).</summary>
        static readonly Kit.Palette BadgeGold = new Kit.Palette("#8C6524", "#D9B45A", "#4A320C", "#141418", 1f);
        static readonly Color Lacquer = Kit.Hex("#141418");

        void BuildKnightFx()
        {
            // An arrow pointing down, its tip at the origin, 1 long: a cone of a head under a round shaft.
            var b = new SkillMeshes.Builder();
            b.Lathe(new[]
            {
                new Vector2(0f, 0f), new Vector2(0.02f, 0.01f), new Vector2(0.2f, 0.34f), new Vector2(0.2f, 0.37f), new Vector2(0.075f, 0.37f),
                new Vector2(0.075f, 0.97f), new Vector2(0.06f, 1f), new Vector2(0f, 1f),
            }, 24);
            meshArrow = b.Build("Knight landing arrow");
        }

        void DestroyKnightFx()
        {
            if (meshArrow != null) Destroy(meshArrow);
        }

        /// <summary>Paint the horseshoe badge on a tile: <paramref name="alpha"/> how solid, <paramref name="flash"/> white.
        /// R139: glossy black lacquer round it and in its holes, polished dark gold inside, for every side (its way's
        /// squares still show whose it is).</summary>
        void PaintBadge(Kit.Tile t, RagdollPawn knight, float alpha, float flash = 0f)
        {
            t.shape = 4f;
            t.gap = 0f;
            t.arc = 1f;
            t.fill = Kit.A(BadgeGold.main, alpha);
            t.core = Kit.A(Lacquer, 1f);
            t.ink = Kit.A(Lacquer, 1f);
            t.inkWidth = 0.09f;
            t.rim = Color.clear;
            t.stripe = Color.clear;
            // Design A's flat tile has no gloss: there the gold is its metal look.
            t.metal = board ? 0f : 1f;
            t.bevel = Color.clear;
            t.bevelWidth = 0.06f;
            t.chevron = Color.clear;
            t.charge = -1f;
            t.glow = Color.clear;
            t.baseColor = Lacquer;
            t.thick = board ? 0.035f : 0f;
            t.shadow = board ? 0.25f : 0f;
            t.gloss = 1f;
            t.sheenAt = -1f;
            t.flash = flash;
        }

        /// <summary>The landing mark's arrow (R138): white with a black edge (R139; navy before), its tip just over the badge,
        /// pointing down at it.</summary>
        class Arrow
        {
            readonly Kit.Prop prop;

            public Arrow(PawnRushSkillFx fx)
            {
                var colors = new Kit.Palette { main = Kit.Hex("#F4F1EA"), light = Color.white, deep = Kit.Hex("#B9B4A8"), ink = Lacquer };
                prop = new Kit.Prop(fx.kit, fx.meshArrow, colors, "Knight landing arrow") { inkWidth = 0.012f };
                prop.Show(false);
            }

            public void Place(Vector3 tip, float length, float alpha)
            {
                prop.t.SetPositionAndRotation(tip, Quaternion.identity);
                prop.t.localScale = new Vector3(length * 0.9f, length, length * 0.9f);
                prop.fade = alpha;
                prop.Show(alpha > 0.02f);
                prop.Apply();
            }

            public void Destroy() => prop.Destroy();
        }

        /// <summary>
        /// The landing mark while the knight flies (R138, in place of the turning horseshoe ring): the horseshoe badge on the
        /// spot it will land, toe along its way, 1.3 m across at take-off and 1 m at touchdown, and the arrow over it, its tip
        /// coming down from 1.2 m to 0.25 m as the knight does, bobbing a little.
        /// </summary>
        void LandingBadge(Kit.Fx x, Kit.Tile badge, Arrow arrow, RagdollPawn k, Vector3 floorSpot, Vector3 along)
        {
            float share = KnightFlightShare(k);
            float size = Mathf.Lerp(1.3f, 1f, share) * Kit.Pop(x.age, 1.1f, 0.6f);
            // Over its way's last square (they lie 0.016 m up): the badge is not hidden under it.
            badge.Floor(floorSpot + Vector3.up * 0.012f, along, Vector2.one * size);
            PaintBadge(badge, k, 1f);
            badge.fade = Mathf.Clamp01(x.age / (4f * Frame));
            badge.Apply();
            if (arrow == null) return;
            float height = Mathf.Lerp(1.2f, 0.3f, share) + 0.05f * Mathf.Sin(kit.Clock * 9f);
            arrow.Place(floorSpot + Vector3.up * (height + (board ? 0.035f : 0f)), Mathf.Lerp(0.8f, 0.55f, share), Mathf.Clamp01(x.age / (4f * Frame)));
        }

        /// <summary>The badge stamped where the knight came down (R138): white for two frames, 1.15 → 1 in six, held, faded
        /// out over its second half-second. R139: its sheen sweeps across it once as it stamps.</summary>
        void BadgeStamp(Vector3 g, Vector3 along, RagdollPawn k)
        {
            var t = new Kit.Tile(kit, "Knight badge stamp");
            var f = kit.Run(60f * Frame, (fx, d) =>
            {
                float a = fx.age / Frame;
                t.Floor(g + Vector3.up * 0.012f, along, Vector2.one * Kit.Pop(fx.age, 1.15f, 1.25f));
                PaintBadge(t, k, 1f, a < 2f ? 1f : 0f);
                t.sheenAt = Mathf.Min(0.999f, 0.15f + fx.age / 0.45f);
                t.fade = a < 30f ? 1f : 1f - (a - 30f) / 30f;
                t.Apply();
                return true;
            });
            f.tiles.Add(t);
        }

        /// <summary>
        /// "밟았다!" (R140, natural: "자연스러운 걸로"): a puff of dust squeezed out level from under the hooves on the head,
        /// fast then hanging, and a little dust kicked up round the piece's feet as it is driven down. (R139 drew five strokes
        /// slamming down and two out sideways over the picture.)
        /// </summary>
        void StompImpact(RagdollPawn target, Vector3 head, Vector3 dir)
        {
            if (kit == null) return;
            Vector3 feet = target != null ? Kit.FloorUnder(target.Hips.position, 0.8f) : Kit.FloorUnder(head, 2f);
            var dust = kit.realDust;
            if (dust != null)
            {
                SkillDust.Tint(kit.dust.light, kit.dust.deep, out var lit, out var shade);
                float turn = Random.Range(0f, 360f);
                for (int i = 0; i < 6; i++)
                {
                    float ang = (turn + i * 60f + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                    var way = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    dust.Emit(head + way * 0.1f - Vector3.up * 0.07f, way * Random.Range(2.4f, 3.4f) + Vector3.up * Random.Range(0.05f, 0.35f),
                              Random.Range(0.06f, 0.09f), Random.Range(0.4f, 0.55f), lit, shade, 1.7f, 0.12f, Random.Range(0.45f, 0.6f), feet.y);
                }
            }
            kit.DustRing(feet + Vector3.up * 0.02f, 3, 0.7f, kit.dust, 0.2f, 0.45f);
        }

        /// <summary>Two hoof prints side by side (R138, as the Queen of the Hill knight's: Skill Tile shape 3, the shoe's U
        /// with its nail holes), <paramref name="ahead"/> metres along <paramref name="dir"/>, pressed in dark and faded out
        /// over <paramref name="life"/> seconds.</summary>
        void Hoofprints(Vector3 at, Vector3 dir, float ahead, float life)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Color dark = board ? Kit.Hex("#5A4738") : kit.dust.ink;
            for (int i = 0; i < 2; i++)
            {
                Vector3 p = Kit.FloorUnder(at + dir * ahead + side * (i == 0 ? -0.17f : 0.17f), 0.8f) + Vector3.up * 0.015f;
                // R157: pressed into the floor as a decal (it follows a step or a slope); the tile where there is none.
                if (SkillDecals.Stamp("Hoof", p, dir, 0.32f, life)) continue;
                var t = new Kit.Tile(kit, "Hoof print");
                var f = kit.Run(life, (fx, d) =>
                {
                    float k = fx.age / fx.life;
                    t.Floor(p, dir, Vector2.one * (0.32f * Kit.Pop(fx.age, 1.15f)));
                    t.shape = 3f;
                    t.gap = 0f;
                    t.thick = 0f;
                    t.shadow = 0f;
                    t.fill = Kit.A(dark, 0.5f);
                    t.metal = 0f;
                    t.core = Color.clear;
                    t.ink = Kit.A(dark, 0.75f);
                    t.inkWidth = 0.005f;
                    t.rim = Color.clear;
                    t.stripe = Color.clear;
                    t.bevel = Color.clear;
                    t.arc = 1f;
                    t.fade = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                    t.Apply();
                    return true;
                });
                f.tiles.Add(t);
            }
        }

        /// <summary>Dust kicked back off the hooves (or a pawn's heel): billows thrown low along <paramref name="back"/>,
        /// spreading a little, with a fleck or two of grit (R138, real dust; R139 fewer).</summary>
        void HoofDust(Vector3 floorAt, Vector3 back, float strength)
        {
            var dust = kit.realDust;
            back = Kit.FlatDir(back, Vector3.back);
            if (dust == null)
            {
                kit.PuffBurst(floorAt + back * 0.2f + Vector3.up * 0.1f, kit.dust, 2, 0.35f, 0.16f, 0.35f, 0.3f);
                return;
            }
            SkillDust.Tint(kit.dust.light, kit.dust.deep, out var lit, out var shade);
            float floor = SkillDust.FloorY(floorAt + Vector3.up * 0.3f);
            if (float.IsNegativeInfinity(floor)) floor = floorAt.y;
            Vector3 side = Vector3.Cross(Vector3.up, back);
            int n = Mathf.Max(1, Mathf.RoundToInt(7 * strength * SkillDust.Amount));
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = (back + side * Random.Range(-0.55f, 0.55f)).normalized;
                float s = Random.Range(0.11f, 0.18f) * Mathf.Lerp(0.8f, 1.2f, strength);
                dust.Emit(new Vector3(floorAt.x, floor + s * 0.5f, floorAt.z) + dir * 0.1f, dir * Random.Range(2.2f, 3.6f) * strength + Vector3.up * Random.Range(0.4f, 0.9f),
                          s, Random.Range(0.55f, 0.8f), lit, shade, Random.Range(1.2f, 1.6f), Random.Range(0.1f, 0.25f), Random.Range(0.55f, 0.72f), floor);
            }
            for (int i = 0; i < Mathf.Max(1, n / 3); i++)
                dust.Grit(new Vector3(floorAt.x, floor + 0.04f, floorAt.z), (back + side * Random.Range(-0.6f, 0.6f)) * Random.Range(1.5f, 2.6f) + Vector3.up * Random.Range(1.2f, 2f),
                          Random.Range(0.012f, 0.02f), Random.Range(0.5f, 0.7f), shade, floor);
        }
    }
}
