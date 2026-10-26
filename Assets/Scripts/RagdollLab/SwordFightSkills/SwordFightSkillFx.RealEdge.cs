using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The edge skills (E) for real, one piece at a time (10-25, 승규 님: "이제 E를 수정 할건데 킹 부터 실사로 그린선 최소화
    /// 해주고 실사로 바꿔줘"): as few lines drawn on the world as can be - the edge it would use is a single faint line of
    /// light along the lip while it can go, and nothing else is drawn; everything else is the stone, the wood, the steel
    /// and the bodies answering: the wall breaking where the sword goes in (<see cref="KingBreach"/>), grit crumbling out
    /// of it while he hangs, chips, the blade's blur, see-through copies of a piece in a fast move, a flash and a cut on
    /// whoever is struck, and the hold, the slow beat and the shake.
    /// </summary>
    public partial class SwordFightSkillFx
    {
        /// <summary>The pieces whose edge skill is drawn for real (10-25: the king first, then the queen, then the rook, the
        /// bishop and the knight - all five now).</summary>
        public static bool RealEdge(PieceKind kind) => kind == PieceKind.King || kind == PieceKind.Queen || kind == PieceKind.Rook || kind == PieceKind.Bishop || kind == PieceKind.Knight;

        /// <summary>The edge a skill would use, while it can go: one thin faint line of light along the lip's top (it was a
        /// wide pulsing band in the piece's colour).</summary>
        void RealGlowEdge(SwordFightSkills owner, string key, EdgeHit e, float strength)
        {
            float top = SwordFightEdgeFloor.Current != null ? SwordFightEdgeFloor.Current.Top : e.point.y;
            float a0 = Mathf.Max(e.from, e.at - 1.8f), a1 = Mathf.Min(e.to, e.at + 1.8f);
            Vector3 start = e.start, along = e.along, inset = -e.outward * 0.05f + Vector3.up * (top - start.y + 0.014f);
            kit.Keep(owner, key, () =>
            {
                var fx = new Kit.Fx();
                var strip = LineStrip();
                fx.strips.Add(strip);
                var pts = new List<Vector3>();
                float released = -1f;
                fx.step = (x, d) =>
                {
                    if (!kit.Held(x) && released < 0f) released = x.age;
                    float r = released >= 0f ? (x.age - released) / F : -1f;
                    if (r >= 8f) return false;
                    pts.Clear();
                    for (int i = 0; i <= 12; i++) pts.Add(start + along * Mathf.Lerp(a0, a1, i / 12f) + inset);
                    float breathe = 0.8f + 0.2f * Mathf.Sin(kit.Clock * 5f);
                    // Thin, fading out at both ends.
                    strip.Build(pts, i => 0.026f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.05f, 0.95f, i / 12f)), kit.Eye, true);
                    strip.head = Kit.EaseOut(x.age / (5f * F));
                    strip.opacity = strength * 0.45f * breathe * (r < 0f ? 1f : 1f - r / 8f);
                    strip.Apply();
                    return true;
                };
                return fx;
            });
        }

        // ---------------------------------------------------------------- the king's return, for real

        /// <summary>While he hangs by his sword in the wall: now and then a little grit crumbles out of the crater round the
        /// blade and falls (it was sparks off the hilt and two glowing bands either side of the lip).</summary>
        void RealKingHang(SwordFightSkills s)
        {
            // Every nine frames of effect time, whatever the frame rate (a frame count missed it at some rates entirely).
            if (kingGrit.TryGetValue(s, out float next) && kit.Clock < next && next - kit.Clock < 1f) return;
            kingGrit[s] = kit.Clock + 9f * F;
            WallChips(s.Hilt + s.EdgeOut * 0.04f, s.EdgeOut * 0.3f + Vector3.down, 2, 0.6f, 0.5f, 0.006f, 0.016f);
        }

        readonly Dictionary<SwordFightSkills, float> kingGrit = new Dictionary<SwordFightSkills, float>();

        /// <summary>E: he sets himself and turns to the edge; light runs down his blade, a small kick of the view (no flash
        /// copy, no ring on the floor, no colour over the screen).</summary>
        void RealEdgeCast(SfFxEvent e)
        {
            var s = e.by;
            HitStop(2);
            Punch(s.Pawn, null, 4f, 12);
            BladeGlint(s.Fighter, 0.22f, 1f);
        }

        /// <summary>His sword goes into the stone under the lip: a flash where steel meets stone, the wall really breaking
        /// round it (<see cref="KingBreach"/>: the crater, chips of its own stone and brass, a piece of the lip), the beat.</summary>
        void RealKingStab(SfFxEvent e)
        {
            var s = e.by;
            Vector3 at = e.at, dir = e.dir;
            // 10-26: it lands as the blade goes home in the stone (its last 0.3 m take three frames, after the cast's own
            // two-frame stop offline), not as he starts the thrust; the effect clock runs on through stops.
            int wait = SwordFightSkills.BodyMotion ? (Online ? 0 : 2) + 3 : 0;
            void Land()
            {
                ClashFlash(at + dir * 0.05f, 0.8f);
                SparkLight(at + dir * 0.2f, 1.2f, 0.08f, 1.8f, new Color(0.95f, 0.95f, 1f));
                KingBreach(s);
                HitStop(5, s.Pawn);
                Shake(s.Pawn, null, 0.2f, 12);
                Beat(s.Pawn, null, 9f);
            }
            if (wait == 0) { Land(); return; }
            // (A life of its own well past the wait: one a frame longer than it was skipped whenever a frame ran long - at
            // under 60 fps or on a hitch - and then nothing broke off the wall at all.)
            kit.Run(1f, (fx, d) =>
            {
                if (fx.age < wait * F) return true;
                if (s != null && s.Pawn != null && s.KingHanging) Land();
                return false;
            });
        }

        /// <summary>He tears the sword out and springs up: the stone spat out of the crater as it comes free, the air knocked
        /// down under him, a short stop and a kick of the view; see-through copies of him every other frame while he is up.</summary>
        void RealKingVault(SfFxEvent e)
        {
            var s = e.by;
            var pawn = s.Pawn;
            if (pawn == null) return;
            ReleaseBreach(s, true);
            Air.Kick(pawn.Hips.position + Vector3.down * 0.3f, Vector3.down + s.EdgeOut * 0.3f, 1.1f);
            BladeBlur(s.Fighter, 0.2f, 0.9f);
            HitStop(3);
            Punch(pawn, null, 7f, 16);
            Shake(pawn, null, 0.12f, 7);
            float next = 0f;
            kit.Run(2f, (fx, d) =>
            {
                if (s == null || pawn == null || s.EdgeStage != SfEdge.Active || s.Piece != PieceKind.King) return false;
                if (fx.age >= next)
                {
                    RealGhost(pawn, 0.28f, 0.18f, Vector3.down);
                    next = fx.age + 2f * F;
                }
                return true;
            });
        }

        /// <summary>He comes down hard and cuts as he lands: splinters of the board thrown up round his feet, his blade's
        /// blur and trail through the cut, a long stop, a big shake and kick of the view. No rings, no cracks drawn, no dust.</summary>
        void RealKingLand(SfFxEvent e)
        {
            var s = e.by;
            Vector3 c = Kit.FloorUnder(e.at + Vector3.up * 0.5f, 1f);
            var keepA = chipA; var keepB = chipB; float keepShare = chipShare;
            Color wood = FloorWood(c);
            chipA = ChipsOf(wood); chipB = ChipsOf(wood, 0.7f); chipShare = 0.6f;
            WallChips(c + Vector3.up * 0.05f, Vector3.up, 14, 3.6f, 0.85f, 0.012f, 0.045f);
            chipA = keepA; chipB = keepB; chipShare = keepShare;
            BladeBlur(s.Fighter, 0.3f, 1.2f);
            BladeGlint(s.Fighter, 0.24f, 0.9f);
            HitStop(7);
            Shake(s.Pawn, null, 0.26f, 12);
            Punch(s.Pawn, null, 10f, 22);
        }

        // ---------------------------------------------------------------- the queen's checkmate cut, for real

        /// <summary>The half second before she goes: one faint line of light along the board from her to whoever she will cut
        /// (it was a pulsing gold band and a gold ring round them), brighter as the moment comes.</summary>
        void RealQueenLine(SwordFightSkills s, SwordFightEdgeParams X)
        {
            var owner = s;
            kit.Keep(s, "queen real line", () =>
            {
                var fx = new Kit.Fx();
                var strip = LineStrip();
                fx.strips.Add(strip);
                var pts = new List<Vector3>();
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null || !kit.Held(x) || owner.EdgeTarget == null) return false;
                    Vector3 a = Kit.FloorUnder(owner.Pawn.Hips.position, 0.6f), b = Kit.FloorUnder(owner.EdgeTarget.Hips.position, 0.6f);
                    pts.Clear();
                    for (int i = 0; i <= 10; i++) pts.Add(Vector3.Lerp(a, b, i / 10f) + Vector3.up * 0.015f);
                    float k = Mathf.Clamp01(owner.EdgeTime / Mathf.Max(0.05f, X.queenLine));
                    strip.Build(pts, i => 0.024f, kit.Eye, true);
                    strip.head = Kit.EaseOut(x.age / (4f * F));
                    strip.opacity = 0.3f + 0.4f * k;
                    strip.Apply();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>
        /// She is through it in an instant (10-25, 승규 님: "칼집에서 뽑는 소리, 베인 뒤 한 박자 늦게 반응, 짧은 슬로 … 멀티 플레이
        /// 고려해서 실사 그래픽으로"): the sound of the draw (Resources/SwordFightSkillSfx/QueenBlink), see-through copies of her
        /// left along the way she went, her blade's blur and bright trail, the cut across the screen as light and a warm
        /// wash, and the short slow beat (<see cref="Beat"/>: offline the game slows, online each screen holds the two
        /// pieces' picture - nobody's game time is touched). Whoever she went through does nothing yet: it is thrown a
        /// beat later on the host (<see cref="RealQueenCut"/>).
        /// </summary>
        void RealQueenBlink(SfFxEvent e)
        {
            var s = e.by;
            var pawn = s.Pawn;
            if (pawn == null) return;
            Vector3 now = pawn.Hips.position, from = s.EdgeStart;
            Vector3 way = from - now;
            for (int i = 1; i <= 5; i++)
            {
                float k = i / 6f;
                RealGhost(pawn, 0.32f * (1f - k * 0.6f), 0.14f + 0.05f * i, Vector3.zero, way * k);
            }
            BladeBlur(s.Fighter, 0.18f, 1.2f);
            BladeGlint(s.Fighter, 0.2f, 1f);
            HitStop(3, pawn, e.target);
            Punch(pawn, e.target, 5f, 12);
            Beat(pawn, e.target, 8f);
            // As light off steel (warm white), not the queen's gold.
            var steel = new Kit.Palette("#FFF4E2", "#FFFFFF", "#E9DCC6", "#9C8E78");
            ScreenSlash(pawn, e.target, steel, true);
            Tint(pawn, e.target, new Color(1f, 0.95f, 0.85f), 0.2f, 16);
        }

        /// <summary>A beat after she went through it, the cut lands (the host throws it; every screen sees it then): a flash
        /// where the blade went in and a fresh cut in its wood (<see cref="RealKnightHit"/>), chips, a hold, a shake.</summary>
        void RealQueenCut(SfFxEvent e)
        {
            RealKnightHit(e);
            Shake(e.by.Pawn, e.target, 0.14f, 9);
            Punch(e.by.Pawn, e.target, 5f, 12);
        }

        /// <summary>The sword gives in the wall (he fell instead of springing): the crater lets it go with a spill of its stone
        /// and he drops, a short stop and a shake.</summary>
        void RealKingDrop(SfFxEvent e)
        {
            var s = e.by;
            ReleaseBreach(s, false);
            WallChips(e.at + e.dir * 0.05f, e.dir + Vector3.up * 0.4f, 8, 2.2f, 0.6f, 0.01f, 0.03f);
            HitStop(3);
            Shake(s.Pawn, e.target, 0.1f, 7);
        }
    }
}
