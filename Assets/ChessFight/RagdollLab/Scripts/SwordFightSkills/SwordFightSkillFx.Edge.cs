using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The edge skills' effects (R103, 승규 님: "기존 스킬 보다 연출이 살짝만 더 … 아무래도 극적이니까"), design A like the
    /// rest. While a skill can be used the edge it would use glows in the piece's colour (and its target is ringed); the
    /// moment it goes the edge flashes white, the mark pops over the head and an afterimage swells off the piece. Each
    /// skill has one dramatic beat - 0.3 s of slow motion (<see cref="SwordFightEdgeParams.slowTime"/>), the view pulled
    /// in and held, a hit stop and a shake: the king's sword going into the stone (sparks, emerald cracks, his crown), the
    /// queen's cut (a gold slash across the screen, a gold ribbon after the piece she throws), the rook's chunk falling
    /// away (it cracks orange and trembles first), the bishop's hands closing on the falling ally (they come out of a
    /// violet hole in the edge; a violet ring where it is set down), the knight's somersault (the view swings round it,
    /// a sky ribbon, a horseshoe stamped where it lands and kicks).
    /// </summary>
    public partial class SwordFightSkillFx
    {
        SwordFightEdgeParams EdgeS => GetComponent<SwordFightSkillBed>() is SwordFightSkillBed b ? b.edge : null;

        Kit.Palette Spark = new Kit.Palette("#FFE38A", "#FFFFFF", "#F2A51A", "#4A2A06");
        static readonly Kit.Palette Ember = new Kit.Palette("#FF8A1F", "#FFE7B0", "#C2410C", "#2B1206");

        // ---------------------------------------------------------------- the view: a held pull and a swing round

        float pullAmp, pullAge = 99f, pullHold;

        /// <summary>The view zooms in by <paramref name="degrees"/> over four frames, holds for <paramref name="hold"/> and
        /// eases back over sixteen (the slow motion's pull).</summary>
        void Pull(RagdollPawn a, RagdollPawn b, float degrees, float hold)
        {
            if (!shake || !Sees(a, b)) return;
            pullAmp = degrees;
            pullAge = 0f;
            pullHold = hold;
        }

        float StepPull()
        {
            pullAge += dt;
            float a = pullAge / F, holdF = pullHold / F;
            if (a >= 4f + holdF + 16f) return 0f;
            float k = a < 4f ? Kit.EaseOut(a / 4f) : a < 4f + holdF ? 1f : 1f - Kit.EaseIn((a - 4f - holdF) / 16f);
            return pullAmp * k;
        }

        RagdollPawn orbitAround;
        float orbitAmp, orbitLeft, orbitTotal;

        /// <summary>The view swings <paramref name="degrees"/> round a piece and back over <paramref name="seconds"/> (effect
        /// time: a slow motion stretches it).</summary>
        void Orbit(RagdollPawn around, float degrees, float seconds)
        {
            if (!shake || around == null || !Sees(around, null)) return;
            orbitAround = around;
            orbitAmp = degrees;
            orbitLeft = orbitTotal = seconds;
        }

        void StepOrbit(Camera cam)
        {
            if (orbitLeft <= 0f || cam == null || orbitAround == null) return;
            orbitLeft -= dt;
            float k = 1f - Mathf.Clamp01(orbitLeft / Mathf.Max(0.01f, orbitTotal));
            cam.transform.RotateAround(orbitAround.Hips.position, Vector3.up, orbitAmp * Mathf.Sin(k * Mathf.PI));
        }

        /// <summary>The beat: slow motion, the view pulled in and held through it. Online (R121, 승규 님's (가)) nothing
        /// slows: the two pieces' picture holds a moment (a hit stop on each screen) and the view is pulled in and held as long.</summary>
        void Beat(RagdollPawn a, RagdollPawn b, float pullDegrees)
        {
            var X = EdgeS;
            float slow = X != null ? X.slowTime : 0.3f, pace = X != null ? X.slowScale : 0.35f;
            if (Online)
            {
                HitStop(OnlineBeatFrames, a, b);
                Pull(a, b, pullDegrees * 1.15f, slow);
                return;
            }
            SlowMo(a, b, slow, pace);
            Pull(a, b, pullDegrees, slow);
        }

        /// <summary>How far each side of the king's sword the edge's glow leaves the lip clear while he hangs (m).</summary>
        const float HoldGap = 0.5f;

        /// <summary>The online beat's hold, in frames (0.1 s).</summary>
        const int OnlineBeatFrames = 6;

        // ---------------------------------------------------------------- watching (each frame)

        void EdgeMarks(SwordFightSkills s, bool mine)
        {
            var X = EdgeS;
            if (X == null) return;
            var pal = PieceColors(s.Piece);
            // Ready: the edge it would use glows, its target is ringed (the owner's view; offline only the player has it).
            if (mine && s.EdgeStage == SfEdge.None && s.EdgeCondition && s.HasEdgeGlow && s.EdgeCooldown <= 0f)
            {
                GlowEdge(s, "edge glow", s.EdgeGlow, pal, 0.75f);
                if (s.EdgeTarget != null) TargetRing(s, s.EdgeTarget, s.Piece == PieceKind.Bishop ? kit.bishop : pal);
                if (s.Piece == PieceKind.Rook) ChunkPreview(s, X);
            }
            switch (s.Piece)
            {
                case PieceKind.King:
                    if (s.KingHanging)
                    {
                        // Either side of the broken lip (10-14: over it, it hid the bite the sword took out of the edge).
                        Vector3 along = Vector3.Cross(Vector3.up, s.EdgeOut), from = s.EdgeAt - along * 1.6f;
                        GlowEdge(s, "edge hold", new EdgeHit { point = s.EdgeAt, outward = s.EdgeOut, along = along, start = from, from = 0f, to = 1.6f - HoldGap, at = 0.8f }, pal, 1f);
                        GlowEdge(s, "edge hold 2", new EdgeHit { point = s.EdgeAt, outward = s.EdgeOut, along = along, start = from, from = 1.6f + HoldGap, to = 3.2f, at = 2.4f }, pal, 1f);
                        // R129 (승규 님 10-13: "원이 그려지고 가운데에 작은 채워진 원이 있는데 지워주고"): no landing marks any
                        // more (the disc, its ring and the reach ring); the spring and the landing say it (KingVault, KingLand).
                        if (((int)(kit.Clock / F) % 5) == 0) HotSparks(s.Hilt, s.EdgeOut + Vector3.up * 0.4f, 1, 2.2f);
                    }
                    break;
                case PieceKind.Queen:
                    if (s.EdgeStage == SfEdge.Windup && s.EdgeTarget != null) QueenLine(s, X);
                    break;
                case PieceKind.Knight:
                    if (s.EdgeStage == SfEdge.Windup)
                    {
                        var w = new Warning { k = Kit.Charge(s.EdgeTime, X.knightCrouch), fill = kit.knight };
                        w.marks.Add(Ring(s.EdgeLand, 0.55f, 0.13f));
                        Warn(s, "knight landing", w);
                        KnightShoe(s, X);
                        kit.Coil(s.Pawn, Mathf.Clamp01(s.EdgeTime / X.knightCrouch));
                    }
                    break;
                case PieceKind.Bishop:
                    if (s.EdgeStage == SfEdge.Active) RescueHands(s, X);
                    break;
            }
        }

        /// <summary>A stretch of the edge's lip (about 3.6 m round the point, along its side) lit in a colour, pulsing.</summary>
        void GlowEdge(SwordFightSkills owner, string key, EdgeHit e, Kit.Palette pal, float strength)
        {
            float top = SwordFightEdgeFloor.Current != null ? SwordFightEdgeFloor.Current.Top : e.point.y;
            float a0 = Mathf.Max(e.from, e.at - 1.8f), a1 = Mathf.Min(e.to, e.at + 1.8f);
            Vector3 start = e.start, along = e.along, inset = -e.outward * 0.07f + Vector3.up * (top - start.y + 0.035f);
            kit.Keep(owner, key, () =>
            {
                var fx = new Kit.Fx();
                var strip = new Kit.Strip(kit, pal) { core = pal.light, coreShare = 0.36f, inkShare = 0.24f };
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
                    float pulse = 0.75f + 0.25f * Mathf.Sin(kit.Clock * 9f);
                    strip.Build(pts, i => 0.13f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.05f, 0.95f, i / 12f)) + 0.03f, kit.Eye, true);
                    strip.head = Kit.EaseOut(x.age / (5f * F));
                    strip.opacity = strength * pulse * (r < 0f ? 1f : 1f - r / 8f);
                    strip.Apply();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>The moment an edge skill goes: its stretch of edge flashes white, widens and is gone.</summary>
        void EdgeFlash(EdgeHit e, Kit.Palette pal)
        {
            float top = SwordFightEdgeFloor.Current != null ? SwordFightEdgeFloor.Current.Top : e.point.y;
            float a0 = Mathf.Max(e.from, e.at - 2.2f), a1 = Mathf.Min(e.to, e.at + 2.2f);
            var pts = new List<Vector3>();
            for (int i = 0; i <= 12; i++) pts.Add(e.start + e.along * Mathf.Lerp(a0, a1, i / 12f) - e.outward * 0.07f + Vector3.up * (top - e.start.y + 0.04f));
            var strip = new Kit.Strip(kit, pal) { core = Color.white, coreShare = 0.5f, inkShare = 0.2f };
            var f = kit.Run(16f * F, (fx, d) =>
            {
                float a = fx.age / F;
                strip.Build(pts, i => (0.12f + 0.16f * Kit.EaseOut(a / 4f)) * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.04f, 0.96f, i / 12f)) + 0.03f, kit.Eye, true);
                strip.core = a < 3f ? Color.white : pal.light;
                strip.opacity = a < 6f ? 1f : 1f - (a - 6f) / 10f;
                strip.Apply();
                return true;
            });
            f.strips.Add(strip);
        }

        /// <summary>A ring of a colour under a piece the skill is aimed at (pops in, turns slowly).</summary>
        void TargetRing(SwordFightSkills owner, RagdollPawn target, Kit.Palette pal)
        {
            var who = target;
            kit.Keep(owner, "edge target", () =>
            {
                var fx = new Kit.Fx();
                fx.tiles.Add(new Kit.Tile(kit, "Edge target"));
                fx.step = (x, d) =>
                {
                    if (who == null || !kit.Held(x) || owner.EdgeTarget != who) return false;
                    var t = x.tiles[0];
                    t.Floor(Kit.FloorUnder(who.Hips.position, 0.6f) + Vector3.up * 0.028f, Quaternion.AngleAxis(kit.Clock * 90f, Vector3.up) * Vector3.forward, Vector2.one * 1.25f * Kit.Pop(x.age, 1.2f, 0.4f));
                    Kit.Horseshoe(t, pal, 0.9f, 60f);
                    t.inner = 0.8f;
                    t.fade = 1f;
                    t.Apply();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>The rook's chunk, faint, while he could break it.</summary>
        void ChunkPreview(SwordFightSkills s, SwordFightEdgeParams X)
        {
            var r = s.EdgeChunk;
            var floor = SwordFightEdgeFloor.Current;
            if (floor == null) return;
            Vector3 c = new Vector3(r.center.x, floor.Top, r.center.y);
            Vector3 o = s.EdgeGlow.outward;
            bool alongX = Mathf.Abs(o.x) > 0.5f;
            var w = new Warning { k = 0.25f, alpha = 0.6f, quiet = true, fill = kit.rook };
            w.marks.Add(Square(c, o, alongX ? r.height : r.width, alongX ? r.width : r.height));
            Warn(s, "rook chunk aim", w);
        }

        // ---------------------------------------------------------------- the casts and the beats

        void EdgeCast(SfFxEvent e)
        {
            var s = e.by;
            var pawn = s.Pawn;
            var pal = PieceColors(s.Piece);
            HeadMark(pawn, s.Piece);
            // 10-14: not for the king's stab - it hid the sword going into the wall and the wall breaking (KingBreach).
            if (s.Piece != PieceKind.King) Afterimage(pawn, pal, 1.75f, 0.34f, 0.7f, true);
            kit.FlashBody(pawn, pal.main);
            if (s.HasEdgeGlow) EdgeFlash(s.EdgeGlow, pal);
            if (pawn.Grounded)
            {
                Vector3 floor = Kit.FloorUnder(pawn.Hips.position, 0.6f);
                FloorShock(floor, 0.3f, 1.6f, pal, 7, 14, 0.22f);
                kit.PuffBurst(floor + Vector3.up * 0.15f, pal, 5, 0.55f, 0.14f, 0.4f, 0.5f);
            }
            HitStop(2);
            Punch(pawn, null, 6f, 14);
            Tint(pawn, null, pal.main, 0.4f, 24);
        }

        // ---- king

        /// <summary>His sword goes into the stone under the lip: sparks, the wall breaking round it (KingBreach: a crater, chips,
        /// a piece of the lip, cracks over the face and the top) - and the beat.</summary>
        void KingStab(SfFxEvent e)
        {
            var s = e.by;
            Vector3 hilt = e.at, o = e.dir;
            HotSparks(hilt, o + Vector3.up * 0.5f, 14, 4.5f);
            HotSparks(hilt, o - Vector3.up * 0.2f, 6, 3f);
            kit.PuffBurst(hilt + o * 0.1f, Kit.Stone, 4, 0.25f, 0.1f, 0.4f, 0.2f);
            // 10-14: the wall really breaks round the sword (a crater, chips, a piece of the lip, cracks; SwordFightSkillFx.Breach),
            // instead of R103's ink cracks over the lip and the gold ring off the face (it covered the break).
            KingBreach(s);
            HitStop(5);
            Shake(s.Pawn, null, 0.2f, 12);
            Beat(s.Pawn, null, 9f);
            Tint(s.Pawn, null, kit.king.main, 0.42f, 26);
        }

        /// <summary>He tears the sword out and springs up (R129, 승규 님: "모션 임펙트 강한걸 넣어줘 부자연스럽지 않게"): stone
        /// chips and sparks burst off the lip, a ring off it, a short stop and a kick of the view as he goes, a gold ribbon
        /// after him and afterimages every other frame (the motion itself: a fast spring, a moment at the top, a heavy fall,
        /// SwordFightSkills.Edge.cs).</summary>
        void KingVault(SfFxEvent e)
        {
            var s = e.by;
            var pawn = s.Pawn;
            ReleaseBreach(s, true);
            kit.PuffBurst(s.EdgeAt + s.EdgeOut * 0.1f, Kit.Stone, 7, 0.45f, 0.14f, 0.45f, 0.4f);
            HotSparks(s.Hilt, s.EdgeOut + Vector3.up, 10, 4f);
            kit.Drops(s.EdgeAt, Vector3.up + s.EdgeOut * 0.3f, Kit.Stone, 6, 6f);
            kit.ImpactRing(s.Hilt + s.EdgeOut * 0.06f, s.EdgeOut, kit.king, 0.2f, 0.9f, false);
            kit.AddSquash(pawn, Kit.SquashKind.Spring);
            Afterimage(pawn, kit.king, 1.5f, 0.28f, 0.65f, true);
            HitStop(3);
            Punch(pawn, null, 8f, 16);
            Shake(pawn, null, 0.12f, 7);
            Tint(pawn, null, kit.king.main, 0.3f, 16);
            Trail(s, pawn, kit.king, 0.4f, () => s.EdgeStage == SfEdge.Active && s.Piece == PieceKind.King, 2);
        }

        /// <summary>He comes down hard: the floor shocked wide, gold cracks running out from his feet, chips and sparks
        /// thrown up, a wall of dust, his answering cut, a long stop, a big shake and kick of the view.</summary>
        void KingLand(SfFxEvent e)
        {
            var s = e.by;
            Vector3 c = Kit.FloorUnder(e.at + Vector3.up * 0.5f, 1f);
            FloorShock(c, 0.4f, e.size * 1.15f, kit.king, 7, 20, 0.38f);
            kit.FlashRing(c, 1.0f, kit.king);
            kit.DustRing(c + Vector3.up * 0.03f, 10, e.size * 0.75f);
            kit.Drops(c + Vector3.up * 0.15f, Vector3.up, Kit.Stone, 8, 7f);
            kit.Drops(c + Vector3.up * 0.1f, Vector3.up, Spark, 6, 5f);
            GroundCracks(c, kit.king, e.size * 0.85f);
            kit.ImpactRing(c + Vector3.up * 0.35f, Vector3.up, kit.king, 0.3f, 1.1f, false);
            SpinCut(s.Pawn, kit.king, Mathf.Min(1.3f, e.size * 0.55f));
            Afterimage(s.Pawn, kit.king, 1.8f, 0.34f, 0.7f, true);
            kit.AddSquash(s.Pawn, Kit.SquashKind.Down);
            HitStop(7);
            Shake(s.Pawn, null, 0.26f, 12);
            Punch(s.Pawn, null, 10f, 22);
            Tint(s.Pawn, null, kit.king.main, 0.4f, 22);
        }

        /// <summary>Cracks running out over the floor from a landing (flat, jagged, in the piece's colour on its ink), drawn
        /// in three frames, gone in 0.8 s.</summary>
        void GroundCracks(Vector3 c, Kit.Palette pal, float reach)
        {
            var lines = new List<List<Vector3>>();
            int n = 7;
            float turn = UnityEngine.Random.value * 360f;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(turn + i * 360f / n + UnityEngine.Random.Range(-14f, 14f), Vector3.up) * Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                var pts = new List<Vector3>();
                Vector3 p = c + dir * 0.18f + Vector3.up * 0.038f;
                pts.Add(p);
                float len = reach * UnityEngine.Random.Range(0.6f, 1f);
                int steps = 4;
                for (int j = 1; j <= steps; j++)
                {
                    p = c + dir * (0.18f + len * j / steps) + side * UnityEngine.Random.Range(-0.12f, 0.12f);
                    p.y = c.y + 0.038f;
                    pts.Add(p);
                }
                lines.Add(pts);
            }
            var strips = new List<Kit.Strip>();
            foreach (var _ in lines) strips.Add(new Kit.Strip(kit, new Kit.Palette { main = pal.ink, light = pal.main, deep = pal.ink, ink = pal.ink }) { core = pal.main, coreShare = 0.45f, inkShare = 0.3f });
            var f = kit.Run(0.8f, (fx, d) =>
            {
                float a = fx.age / F, left = (fx.life - fx.age) / F;
                for (int i = 0; i < lines.Count; i++)
                {
                    var pts = lines[i];
                    strips[i].Build(pts, j => 0.09f * (1f - 0.6f * j / Mathf.Max(1f, pts.Count - 1f)), kit.Eye, true);
                    strips[i].head = Kit.EaseOut(a / 3f);
                    strips[i].core = a < 2f ? Color.white : pal.main;
                    strips[i].opacity = left < 12f ? left / 12f : 1f;
                    strips[i].Apply();
                }
                return true;
            });
            f.strips.AddRange(strips);
        }

        void KingDrop(SfFxEvent e)
        {
            var s = e.by;
            ReleaseBreach(s, false);
            kit.Drops(e.at, e.dir + Vector3.up * 0.6f, Spark, 6, 5f);
            kit.PuffBurst(e.at + e.dir * 0.1f, kit.grey, 5, 0.35f, 0.14f, 0.45f, 0.2f);
            kit.ImpactRing(e.at + e.dir * 0.05f, e.dir, kit.grey, 0.1f, 0.45f, false);
            HitStop(3);
            Shake(s.Pawn, e.target, 0.1f, 7);
        }

        // ---- queen

        /// <summary>The 0.5 s line: gold, from her feet to its feet along the floor, drawn out, pulsing; its ring gold.</summary>
        void QueenLine(SwordFightSkills s, SwordFightEdgeParams X)
        {
            var target = s.EdgeTarget;
            var w = new Warning { k = Kit.Charge(s.EdgeTime, X.queenLine), fill = kit.queen };
            w.marks.Add(Ring(Kit.FloorUnder(target.Hips.position, 0.6f), 0.6f, 0.13f));
            Warn(s, "queen target", w);
            var owner = s;
            kit.Keep(s, "queen line", () =>
            {
                var fx = new Kit.Fx();
                var strip = new Kit.Strip(kit, kit.queen) { core = Color.white, coreShare = 0.42f, inkShare = 0.22f };
                fx.strips.Add(strip);
                var pts = new List<Vector3>();
                float released = -1f;
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null) return false;
                    if (!kit.Held(x) && released < 0f) released = x.age;
                    float r = released >= 0f ? (x.age - released) / F : -1f;
                    if (r >= 6f) return false;
                    var t = owner.EdgeTarget;
                    if (t != null && r < 0f)
                    {
                        Vector3 a = Kit.FloorUnder(owner.Pawn.Hips.position, 0.6f), b = Kit.FloorUnder(t.Hips.position, 0.6f);
                        pts.Clear();
                        for (int i = 0; i <= 10; i++) pts.Add(Vector3.Lerp(a, b, i / 10f) + Vector3.up * 0.04f);
                    }
                    if (pts.Count < 2) return true;
                    float k = Mathf.Clamp01(owner.EdgeTime / Mathf.Max(0.05f, X.queenLine));
                    strip.Build(pts, i => 0.07f + 0.05f * k + 0.02f * Mathf.Sin(kit.Clock * 30f), kit.Eye, true);
                    strip.head = r >= 0f ? 1f : Kit.EaseOut(x.age / (4f * F));
                    strip.core = r >= 0f && r < 2f ? Color.white : Color.Lerp(kit.queen.light, Color.white, k);
                    strip.opacity = r < 0f ? 1f : 1f - r / 6f;
                    strip.Apply();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>She is there in an instant: gold copies of her all along the way, and her way drawn on the floor.</summary>
        void QueenBlink(SfFxEvent e)
        {
            var s = e.by;
            GhostTrail(s.Pawn, s.EdgeStart, s.Pawn.Hips.position, 5, kit.queen);
            Vector3 a = Kit.FloorUnder(s.EdgeStart, 0.8f), b = e.at;
            var pts = new List<Vector3>();
            for (int i = 0; i <= 10; i++) pts.Add(Vector3.Lerp(a, b, i / 10f) + Vector3.up * 0.036f);
            var cut = new Kit.Strip(kit, kit.queen) { core = Color.white, coreShare = 0.45f, inkShare = 0.18f };
            var f = kit.Run(22f * F, (fx, d) =>
            {
                float t = fx.age / F;
                cut.Build(pts, i => 0.05f + 0.22f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.06f, 0.94f, i / 10f)), kit.Eye, true);
                cut.tail = t < 8f ? 0f : Kit.EaseIn((t - 8f) / 14f);
                cut.Apply();
                return true;
            });
            f.strips.Add(cut);
            kit.PuffBurst(a + Vector3.up * 0.12f, kit.dust, 4, 0.4f, 0.16f, 0.4f, 0.2f);
        }

        void QueenCut(SfFxEvent e)
        {
            var s = e.by;
            var target = e.target;
            if (target != null) Slash(target.bodies[(int)BodyId.Chest].position, e.dir, kit.queen, 2.6f);
            Hit(e, kit.queen, 4, 0.2f);
            ScreenSlash(s.Pawn, target, kit.queen);
            Beat(s.Pawn, target, 10f);
            Tint(s.Pawn, target, kit.queen.main, 0.42f, 26);
            Afterimage(s.Pawn, kit.queen, 1.5f, 0.3f, 0.7f, true);
            if (s.EdgeTarget != null || target != null) EdgeFlashAt(target != null ? target.Hips.position : e.at, kit.queen);
            if (target != null) Trail(s, target, kit.queen, 0.4f, null, 0, 1.3f);
        }

        void QueenMiss(SfFxEvent e)
        {
            var s = e.by;
            Vector3 at = s.Pawn.bodies[(int)BodyId.Chest].position + e.dir * 0.6f;
            Slash(at, e.dir, kit.queen, 1.8f);
            kit.PuffBurst(Kit.FloorUnder(at, 1f) + Vector3.up * 0.1f, kit.grey, 4, 0.4f, 0.14f, 0.4f, 0.2f);
            Shake(s.Pawn, e.target, 0.06f, 5);
        }

        void EdgeFlashAt(Vector3 p, Kit.Palette pal)
        {
            var floor = SwordFightEdgeFloor.Current;
            if (floor != null && floor.Nearest(p, out var hit)) EdgeFlash(hit, pal);
        }

        // ---- rook

        void RookBreak(SfFxEvent e)
        {
            var s = e.by;
            Vector3 lip = e.at;
            FloorShock(lip - e.dir * 0.4f, 0.3f, 1.6f, kit.rook, 7, 15, 0.3f);
            kit.PuffBurst(lip - e.dir * 0.3f + Vector3.up * 0.15f, Kit.Stone, 6, 0.6f, 0.2f, 0.5f);
            kit.Drops(lip - e.dir * 0.3f + Vector3.up * 0.15f, Vector3.up + e.dir * 0.3f, Kit.Stone, 6, 6f);
            kit.ImpactRing(lip - e.dir * 0.3f + Vector3.up * 0.05f, Vector3.up, kit.rook, 0.2f, 0.8f, false);
            kit.AddSquash(s.Pawn, Kit.SquashKind.Down);
            HitStop(4);
            Shake(s.Pawn, null, 0.15f, 9);
            Punch(s.Pawn, null, 6f, 16);
        }

        /// <summary>A chunk cracking: an orange warning over it (filling as the crack runs) and orange cracks running from
        /// the lip in and round its three inner sides.</summary>
        void CollapseMarks(SwordFightEdgeFloor.Collapse c)
        {
            var floor = SwordFightEdgeFloor.Current;
            if (floor == null || c.by == null || c.phase != SwordFightEdgeFloor.Phase.Crack) return;
            float k = Mathf.Clamp01(c.age / Mathf.Max(0.05f, c.crack));
            var r = c.rect;
            Vector3 centre = new Vector3(r.center.x, floor.Top, r.center.y);
            bool alongX = Mathf.Abs(c.outward.x) > 0.5f;
            var w = new Warning { k = Kit.Charge(c.age, c.crack), fill = kit.rook };
            w.marks.Add(Square(centre, c.outward, alongX ? r.height : r.width, alongX ? r.width : r.height));
            Warn(c.by, "rook chunk " + c.GetHashCode(), w);
            kit.Keep(c, "cracks", () =>
            {
                var fx = new Kit.Fx();
                var lines = CrackLines(c, floor.Top);
                foreach (var _ in lines) fx.strips.Add(new Kit.Strip(kit, new Kit.Palette { main = kit.rook.ink, light = kit.rook.main, deep = kit.rook.ink, ink = kit.rook.ink }) { core = kit.rook.main, coreShare = 0.42f, inkShare = 0.3f });
                fx.step = (x, d) =>
                {
                    if (!kit.Held(x)) return false;
                    float kk = Mathf.Clamp01(c.age / Mathf.Max(0.05f, c.crack));
                    for (int i = 0; i < lines.Count; i++)
                    {
                        var (pts, seam) = lines[i];
                        var strip = x.strips[i];
                        strip.Build(pts, j => seam ? 0.1f : 0.075f, kit.Eye, true);
                        // The cracks from the lip run first, the seams close the chunk off after.
                        strip.head = seam ? Kit.EaseOut(Mathf.InverseLerp(0.25f, 0.9f, kk)) : Kit.EaseOut(Mathf.InverseLerp(0f, 0.6f, kk));
                        strip.core = kk > 0.85f && ((int)(kit.Clock / F) & 2) == 0 ? Color.white : kit.rook.main;
                        strip.Apply();
                    }
                    return true;
                };
                return fx;
            });
            if (((int)(kit.Clock / F) % 6) == 0 && k > 0.2f)
            {
                // Grit shaken loose along the seams.
                var seam = SeamPoint(c, floor.Top, UnityEngine.Random.value);
                kit.PuffBurst(seam + Vector3.up * 0.05f, Kit.Stone, 1, 0.1f, 0.08f, 0.35f, 0.25f);
            }
        }

        /// <summary>The chunk's cracks: a few from its lip inward (flat, zigzag), and its three inner sides (the seams).</summary>
        List<(List<Vector3> pts, bool seam)> CrackLines(SwordFightEdgeFloor.Collapse c, float top)
        {
            var list = new List<(List<Vector3>, bool)>();
            var r = c.rect;
            Vector3 o = c.outward, t = c.along;
            Vector3 centre = new Vector3(r.center.x, top + 0.035f, r.center.y);
            bool alongX = Mathf.Abs(o.x) > 0.5f;
            float depth = alongX ? r.width : r.height, width = alongX ? r.height : r.width;
            Vector3 lipMid = centre + o * (depth * 0.5f), back = centre - o * (depth * 0.5f);
            for (int i = 0; i < 4; i++)
            {
                var pts = new List<Vector3>();
                Vector3 p = lipMid + t * Mathf.Lerp(-0.4f, 0.4f, (i + 0.5f) / 4f) * width - o * 0.02f;
                pts.Add(p);
                int n = 4;
                for (int j = 1; j <= n; j++)
                {
                    p += -o * (depth * 0.85f / n) + t * UnityEngine.Random.Range(-0.18f, 0.18f);
                    pts.Add(p);
                }
                list.Add((pts, false));
            }
            // The seams: the two sides across the edge and the back, as one zigzag each.
            Vector3 a = lipMid - t * (width * 0.5f), b = back - t * (width * 0.5f), cc = back + t * (width * 0.5f), dd = lipMid + t * (width * 0.5f);
            foreach (var (p0, p1) in new[] { (a, b), (b, cc), (cc, dd) })
            {
                var pts = new List<Vector3>();
                Vector3 n = Vector3.Cross(Vector3.up, (p1 - p0).normalized);
                for (int j = 0; j <= 8; j++) pts.Add(Vector3.Lerp(p0, p1, j / 8f) + n * (j % 2 == 0 ? 0.04f : -0.04f) * (j == 0 || j == 8 ? 0f : 1f));
                list.Add((pts, true));
            }
            return list;
        }

        static Vector3 SeamPoint(SwordFightEdgeFloor.Collapse c, float top, float u)
        {
            var r = c.rect;
            Vector3 centre = new Vector3(r.center.x, top, r.center.y);
            bool alongX = Mathf.Abs(c.outward.x) > 0.5f;
            float depth = alongX ? r.width : r.height, width = alongX ? r.height : r.width;
            Vector3 lipMid = centre + c.outward * (depth * 0.5f), back = centre - c.outward * (depth * 0.5f);
            Vector3 a = lipMid - c.along * (width * 0.5f), b = back - c.along * (width * 0.5f), cc = back + c.along * (width * 0.5f), dd = lipMid + c.along * (width * 0.5f);
            float total = depth * 2f + width, s = u * total;
            if (s < depth) return Vector3.Lerp(a, b, s / depth);
            s -= depth;
            if (s < width) return Vector3.Lerp(b, cc, s / width);
            return Vector3.Lerp(cc, dd, (s - width) / depth);
        }

        /// <summary>The floor's own news: the chunk goes (the beat: a thud, the view pulled in and slowed, dust bursting out
        /// of its seams, stones flying), it comes back (dust, stones up), it is whole again.</summary>
        void OnFloor(SwordFightEdgeFloor.Collapse c)
        {
            if (!effects || kit == null) return;
            var floor = SwordFightEdgeFloor.Current;
            float top = floor != null ? floor.Top : 0f;
            var rook = c.by != null ? c.by.Pawn : null;
            stopBy = rook;
            stopTarget = null;
            switch (c.phase)
            {
                case SwordFightEdgeFloor.Phase.Open:
                    for (int i = 0; i < 9; i++)
                    {
                        Vector3 p = SeamPoint(c, top, (i + 0.5f) / 9f);
                        kit.PuffBurst(p + Vector3.up * 0.1f, Kit.Stone, 2, 0.25f, 0.18f, 0.55f, 0.35f);
                        if (i % 2 == 0) kit.Drops(p, Vector3.up * 0.6f - c.outward * 0.2f, Kit.Stone, 2, 5f);
                    }
                    kit.DustRing(new Vector3(c.rect.center.x, top + 0.05f, c.rect.center.y), 8, 1.4f);
                    HitStop(4);
                    Shake(rook, null, 0.26f, 16);
                    Beat(rook, null, 8f);
                    Tint(rook, null, kit.rook.main, 0.38f, 24);
                    break;
                case SwordFightEdgeFloor.Phase.Rise:
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 p = SeamPoint(c, top, (i + 0.5f) / 6f);
                        kit.PuffBurst(p + Vector3.up * 0.05f, kit.dust, 2, 0.2f, 0.14f, 0.45f, 0.3f);
                    }
                    break;
                case SwordFightEdgeFloor.Phase.Done:
                    for (int i = 0; i < 6; i++) kit.Drops(SeamPoint(c, top, (i + 0.5f) / 6f), Vector3.up, Kit.Stone, 1, 4f);
                    kit.FlashRing(new Vector3(c.rect.center.x, top, c.rect.center.y), 1.2f, kit.rook);
                    Shake(rook, null, 0.08f, 6);
                    break;
            }
        }

        // ---- bishop

        /// <summary>Where the hands come out of the edge: a violet hole just in from the lip.</summary>
        static Vector3 HolePoint(SwordFightSkills s)
        {
            var floor = SwordFightEdgeFloor.Current;
            float top = floor != null ? floor.Top : s.EdgeAt.y;
            return new Vector3(s.EdgeAt.x, top, s.EdgeAt.z) - s.EdgeOut * 0.3f;
        }

        /// <summary>The ally's waist, either side: where a hand holds it (as the hole sees it).</summary>
        static Vector3 WaistGrip(SwordFightSkills s, RagdollPawn ally, float side, out Quaternion rot)
        {
            Vector3 perp = Vector3.Cross(Vector3.up, s.EdgeOut).normalized;
            Vector3 c = Vector3.Lerp(ally.Hips.position, ally.bodies[(int)BodyId.Chest].position, 0.35f);
            Vector3 p = c + perp * (side * 0.2f) - Vector3.up * 0.04f;
            rot = Quaternion.LookRotation(-perp * side, Vector3.up);
            return p;
        }

        /// <summary>The hole on the edge and the two gold hands: out of the hole to the falling ally in nine frames (open),
        /// shut round its waist, arms stretching back to the hole, bending as it is pulled in.</summary>
        void RescueHands(SwordFightSkills s, SwordFightEdgeParams X)
        {
            var owner = s;
            kit.Keep(s, "rescue", () =>
            {
                var fx = new Kit.Fx();
                var hands = new[] { new GrabHand(kit, "Rescue hand L"), new GrabHand(kit, "Rescue hand R") };
                var arms = new[] { ArmStrip(), ArmStrip() };
                fx.strips.AddRange(arms);
                fx.tiles.Add(new Kit.Tile(kit, "Rescue hole"));
                fx.tiles.Add(new Kit.Tile(kit, "Rescue hole rim"));
                fx.end = () => { foreach (var h in hands) h.Destroy(); };
                var pts = new List<Vector3>();
                Vector3 hole = HolePoint(owner), o = owner.EdgeOut;
                RagdollPawn ally = owner.Carried;
                float released = -1f, caught = -1f;
                var last = new Vector3[2];
                var lastRot = new[] { Quaternion.identity, Quaternion.identity };
                fx.step = (x, d) =>
                {
                    if (!kit.Held(x) && released < 0f) released = x.age;
                    float r = released >= 0f ? (x.age - released) / F : -1f;
                    if (r >= 10f) return false;
                    float a = x.age / F;
                    if (owner.CarriedHeld && caught < 0f) caught = x.age;
                    // The hole: pops open, a deep violet disc with an inked violet rim, closing once the hands are back.
                    float open = Kit.Pop(x.age, 1.15f, 0f) * (r < 0f ? 1f : 1f - Kit.EaseIn(r / 10f));
                    var disc = x.tiles[0];
                    disc.Floor(hole + Vector3.up * 0.03f, o, new Vector2(0.95f, 0.55f) * open);
                    disc.shape = 2f;
                    disc.fill = Kit.A(kit.bishop.ink, 0.92f);
                    disc.core = Color.clear;
                    disc.ink = Kit.A(kit.bishop.deep, 1f);
                    disc.inkWidth = 0.06f;
                    disc.rim = Color.clear;
                    disc.stripe = Color.clear;
                    disc.fade = 1f;
                    disc.Apply();
                    var rim = x.tiles[1];
                    rim.Floor(hole + Vector3.up * 0.032f, o, new Vector2(1.05f, 0.65f) * open);
                    Kit.Horseshoe(rim, kit.bishop, 1f, 0f);
                    rim.inner = 0.84f;
                    rim.gap = 0f;
                    rim.fade = 1f;
                    rim.Apply();
                    for (int i = 0; i < 2; i++)
                    {
                        float side = i == 0 ? -1f : 1f;
                        Vector3 start = hole + Vector3.up * 0.05f + Vector3.Cross(Vector3.up, o) * (side * 0.18f);
                        // The waist while it lasts; letting go, the hands open where they were.
                        if (ally != null && r < 0f)
                        {
                            last[i] = WaistGrip(owner, ally, side, out var g);
                            lastRot[i] = g;
                        }
                        Vector3 end = last[i];
                        Quaternion grip = lastRot[i];
                        // Out of the hole and up, then over to the ally (the bishop's diagonal curve), bending as it pulls.
                        Vector3 c1 = start + Vector3.up * 0.7f + o * 0.15f, c2 = end + Vector3.up * 0.55f - o * 0.25f;
                        float t = r >= 0f ? 1f - Kit.EaseIn(r / 8f) : FlyEase(a);
                        Vector3 at = Bezier(start, c1, c2, end, t);
                        Quaternion fly = FlyRotation(BezierTangent(start, c1, c2, end, Mathf.Clamp(t, 0.02f, 0.98f)), o);
                        Quaternion rot = caught < 0f ? Quaternion.Slerp(fly, grip, Mathf.Clamp01((a - (HandFly - 3f)) / 3f)) : grip;
                        float sinceCatch = caught >= 0f ? (x.age - caught) / F : -1f;
                        float curl = r >= 0f ? Mathf.Lerp(80f, -25f, Kit.EaseOut(r / 4f))
                            : sinceCatch < 0f ? -18f : Mathf.Lerp(-18f, 88f, Kit.EaseOut(sinceCatch / 3f)) + (sinceCatch > 3f ? 5f * Mathf.Sin(kit.Clock * 34f) : 0f);
                        float scale = HandHold * (r >= 4f ? 1f - Kit.EaseIn((r - 4f) / 6f) : 1f);
                        hands[i].Place(at, rot, curl, sinceCatch >= 0f && sinceCatch < 2f ? 1f : 0f, scale);
                        pts.Clear();
                        for (int j = 0; j <= 14; j++) pts.Add(Bezier(start, c1, c2, end, t * j / 14f));
                        if (t > 0.04f)
                        {
                            arms[i].Build(pts, j => Mathf.Lerp(0.1f, 0.17f, j / 14f), kit.Eye);
                            arms[i].Apply();
                        }
                        else arms[i].Hide();
                    }
                    return true;
                };
                return fx;
            });
        }

        void BishopReach(SfFxEvent e)
        {
            var s = e.by;
            Vector3 hole = HolePoint(s);
            kit.PuffBurst(hole + Vector3.up * 0.1f, kit.bishop, 5, 0.4f, 0.14f, 0.4f, 0.4f);
            kit.ImpactRing(hole + Vector3.up * 0.05f, Vector3.up, kit.bishop, 0.2f, 0.7f, false);
            Punch(s.Pawn, e.target, 4f, 12);
        }

        void BishopCatch(SfFxEvent e)
        {
            var s = e.by;
            if (e.target != null)
            {
                kit.FlashBody(e.target, kit.bishop.main);
                kit.ImpactRing(e.target.bodies[(int)BodyId.Chest].position, -s.EdgeOut, kit.queen, 0.15f, 0.6f);
                kit.PuffBurst(e.target.Hips.position, kit.bishop, 4, 0.35f, 0.12f, 0.35f, 0.1f);
                Afterimage(e.target, kit.bishop, 1.35f, 0.24f, 0.55f);
            }
            HitStop(3);
            Shake(s.Pawn, e.target, 0.09f, 7);
            Beat(s.Pawn, e.target, 8f);
            Tint(s.Pawn, e.target, kit.bishop.main, 0.4f, 24);
            EdgeFlashAt(HolePoint(s), kit.bishop);
        }

        /// <summary>Set down on the platform: a violet ring bursting under its feet and staying a moment.</summary>
        void BishopSet(SfFxEvent e)
        {
            Vector3 c = Kit.FloorUnder(e.at + Vector3.up * 0.5f, 1f);
            FloorShock(c, 0.3f, 1.3f, kit.bishop, 7, 16, 0.22f);
            kit.FlashRing(c, 0.7f, kit.bishop);
            kit.PuffBurst(c + Vector3.up * 0.1f, kit.bishop, 5, 0.45f, 0.14f, 0.4f, 0.3f);
            if (e.target != null) kit.FlashBody(e.target, kit.bishop.light);
            var ring = new Kit.Tile(kit, "Rescued ring");
            var who = e.target;
            var f = kit.Run(48f * F, (fx, d) =>
            {
                float a = fx.age / F;
                Vector3 at = who != null ? Kit.FloorUnder(who.Hips.position, 0.6f) : c;
                ring.Floor(at + Vector3.up * 0.026f, Vector3.forward, Vector2.one * 1.1f * Kit.Pop(fx.age, 1.2f, 0.3f));
                Kit.Horseshoe(ring, kit.bishop, 0.95f, 0f);
                ring.inner = 0.8f;
                ring.gap = 0f;
                ring.flash = a < 2f ? 1f : 0f;
                ring.fade = a < 34f ? 1f : 1f - (a - 34f) / 14f;
                ring.Apply();
                return true;
            });
            f.tiles.Add(ring);
            Shake(e.by.Pawn, e.target, 0.06f, 5);
        }

        void BishopDrop(SfFxEvent e)
        {
            if (e.target != null) kit.PuffBurst(e.target.Hips.position, kit.grey, 4, 0.35f, 0.14f, 0.4f, 0.1f);
            kit.PuffBurst(HolePoint(e.by) + Vector3.up * 0.1f, kit.grey, 4, 0.35f, 0.14f, 0.4f, 0.3f);
            Shake(e.by.Pawn, e.target, 0.08f, 6);
        }

        // ---- knight

        /// <summary>The horseshoe it will come down on, open toward the cliff, shrinking toward the spring.</summary>
        void KnightShoe(SwordFightSkills s, SwordFightEdgeParams X)
        {
            var owner = s;
            kit.Keep(s, "knight edge shoe", () =>
            {
                var fx = new Kit.Fx();
                fx.tiles.Add(new Kit.Tile(kit, "Knight edge landing"));
                fx.step = (x, d) =>
                {
                    if (!kit.Held(x)) return false;
                    float k = Mathf.Clamp01(owner.EdgeTime / X.knightCrouch);
                    var t = x.tiles[0];
                    t.Floor(owner.EdgeLand + Vector3.up * 0.03f, owner.EdgeOut, Vector2.one * Mathf.Lerp(1.4f, 0.9f, k) * Kit.Pop(x.age, 1.15f, 0.4f));
                    Kit.Horseshoe(t, kit.knight, 0.95f);
                    t.flash = k > 0.85f && ((int)(kit.Clock / F) & 2) == 0 ? 1f : 0f;
                    t.fade = 1f;
                    t.Apply();
                    return true;
                };
                return fx;
            });
        }

        void KnightCrouch(SfFxEvent e)
        {
            kit.FlashRing(Kit.FloorUnder(e.by.Pawn.Hips.position, 0.6f), 0.55f, kit.knight);
            kit.AddSquash(e.by.Pawn, Kit.SquashKind.Down);
        }

        /// <summary>It springs: a stamp where it pushes off, a sky ribbon and afterimages after it, and as it goes over the
        /// top the beat - slow motion with the view swinging round it.</summary>
        void KnightFlip(SfFxEvent e)
        {
            var s = e.by;
            var pawn = s.Pawn;
            var X = EdgeS;
            kit.FlashRing(e.at, 0.65f, kit.knight);
            kit.DustRing(e.at, 5, 0.75f);
            Stamp(e.at, -e.dir, kit.knight, 1.1f);
            kit.AddSquash(pawn, Kit.SquashKind.Spring);
            Punch(pawn, null, 5f, 12);
            Trail(s, pawn, kit.knight, 0.46f, () => s.EdgeStage == SfEdge.Active && s.Piece == PieceKind.Knight, 3);
            float over = (X != null ? X.knightFlip : 0.8f) * SwordFightSkills.FlipOut;
            bool beat = false;
            kit.Run(2f, (fx, d) =>
            {
                if (s == null || s.EdgeStage != SfEdge.Active || s.Piece != PieceKind.Knight) return false;
                if (beat || s.EdgeTime < over) return true;
                beat = true;
                Beat(pawn, s.EdgeTarget, 7f);
                Orbit(pawn, 38f, 1.1f);
                Afterimage(pawn, kit.knight, 1.4f, 0.3f, 0.6f, true);
                return false;
            });
        }

        void KnightKick(SfFxEvent e)
        {
            var s = e.by;
            var pawn = s.Pawn;
            Vector3 c = Kit.FloorUnder(e.at + Vector3.up * 0.5f, 1f);
            Stamp(c, e.dir, kit.knight, 1.25f);
            kit.FlashRing(c, 0.8f, kit.knight);
            kit.DustRing(c, 6, 0.9f);
            Vector3 feet = Vector3.Lerp(pawn.bodies[(int)BodyId.FootL].position, pawn.bodies[(int)BodyId.FootR].position, 0.5f);
            kit.ImpactRing(feet + e.dir * 0.2f, e.dir, kit.knight, 0.12f, 0.6f);
            kit.Drops(feet + e.dir * 0.2f, e.dir + Vector3.up * 0.3f, kit.knight, 5, 6f);
            kit.FlashBody(pawn, kit.knight.main);
            HitStop(4);
            Shake(pawn, e.target, 0.14f, 8);
            Punch(pawn, e.target, 6f, 14);
            Tint(pawn, e.target, kit.knight.main, 0.34f, 20);
        }

        /// <summary>A horseshoe stamped on the floor: white for two frames, held, faded out.</summary>
        void Stamp(Vector3 at, Vector3 open, Kit.Palette pal, float size)
        {
            var shoe = new Kit.Tile(kit, "Stamp");
            var f = kit.Run(26f * F, (fx, d) =>
            {
                float a = fx.age / F;
                shoe.Floor(at + Vector3.up * 0.032f, open, Vector2.one * size * Kit.Pop(fx.age, 1.25f, 0.4f));
                Kit.Horseshoe(shoe, pal, 1f);
                shoe.flash = a < 2f ? 1f : 0f;
                shoe.fade = a < 14f ? 1f : 1f - (a - 14f) / 12f;
                shoe.Apply();
                return true;
            });
            f.tiles.Add(shoe);
        }

        // ---------------------------------------------------------------- shared pieces

        /// <summary>A ribbon after a piece's hips (and an afterimage every <paramref name="ghostEvery"/> frames, 0 = none)
        /// while <paramref name="going"/> says so, or for <paramref name="life"/> seconds.</summary>
        void Trail(SwordFightSkills s, RagdollPawn pawn, Kit.Palette pal, float width, System.Func<bool> going, int ghostEvery, float life = 2f)
        {
            var strip = new Kit.Strip(kit, pal) { core = Color.white, coreShare = 0.36f, inkShare = 0.22f };
            var pts = new List<Vector3>();
            float nextGhost = ghostEvery * F;
            var f = kit.Run(life, (fx, d) =>
            {
                bool on = pawn != null && (going == null ? fx.age < life - 0.35f : going());
                if (on)
                {
                    pts.Add(pawn.bodies[(int)BodyId.Chest].position);
                    if (ghostEvery > 0 && fx.age >= nextGhost)
                    {
                        Afterimage(pawn, pal, 1f, 0.2f, 0.42f);
                        nextGhost = fx.age + ghostEvery * F;
                    }
                }
                else if (pts.Count > 0) pts.RemoveAt(0);
                while (pts.Count > 16) pts.RemoveAt(0);
                if (pts.Count < 2) { strip.Hide(); return on || fx.age < 0.1f; }
                strip.Build(pts, i => width * i / Mathf.Max(1f, pts.Count - 1f), kit.Eye);
                strip.Apply();
                return true;
            });
            f.strips.Add(strip);
        }

        /// <summary>Copies of a piece all along a way it went in an instant (the queen's blink): see-through, the far ones
        /// fading first.</summary>
        void GhostTrail(RagdollPawn pawn, Vector3 from, Vector3 to, int n, Kit.Palette p)
        {
            if (pawn == null || pawn.skin == null) return;
            var mesh = new Mesh { name = "Skill ghost trail" };
            pawn.skin.BakeMesh(mesh, true);
            Transform st = pawn.skin.transform;
            Vector3 pos = st.position;
            Quaternion rot = st.rotation;
            Vector3 back = from - to;
            back.y = 0f;
            var gos = new List<(GameObject go, MeshRenderer r, MaterialPropertyBlock b, float k)>();
            for (int i = 0; i < n; i++)
            {
                float k = (i + 1f) / (n + 1f);
                var go = new GameObject("Ghost");
                go.transform.SetParent(kit.root, false);
                go.transform.SetPositionAndRotation(pos + back * k, rot);
                go.transform.localScale = st.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = GhostMaterial();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                gos.Add((go, r, new MaterialPropertyBlock(), k));
            }
            var f = kit.Run(18f * F, (fx, d) =>
            {
                float a = fx.age / F;
                foreach (var (go, r, b, k) in gos)
                {
                    float life = Mathf.Lerp(16f, 6f, k);
                    float fade = 1f - Mathf.Clamp01(a / life);
                    b.SetColor("_Color", a < 2f ? Color.white : p.main);
                    b.SetColor("_InkColor", p.ink);
                    b.SetFloat("_Ink", 0.3f);
                    b.SetFloat("_Alpha", 0.55f * fade * fade);
                    r.SetPropertyBlock(b);
                    r.enabled = fade > 0.01f;
                }
                return true;
            });
            f.end = () =>
            {
                foreach (var (go, _, _, _) in gos) if (go != null) Destroy(go);
                if (mesh != null) Destroy(mesh);
            };
        }

        /// <summary>A gold slash across the whole view (the queen's cut, R103): an inked band with a white core sweeping
        /// corner to corner in three frames, held, then thinning away. Only the two pieces' own screens.</summary>
        void ScreenSlash(RagdollPawn a, RagdollPawn b, Kit.Palette p)
        {
            if (!Sees(a, b)) return;
            var parts = new List<(GameObject go, Material mat, Color c, float thick)>
            {
                Band(p.ink, 4001, 0.032f),
                Band(p.main, 4002, 0.02f),
                Band(Color.white, 4003, 0.007f),
            };
            const float Angle = -22f;
            var f = kit.Run(18f * F, (fx, d) =>
            {
                var cam = ViewCamera;
                if (cam == null) return false;
                float t = fx.age / F;
                float dist = cam.nearClipPlane + 0.03f;
                float h = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), w = h * Mathf.Max(cam.aspect, 1f);
                float len = Mathf.Sqrt(w * w + h * h) * 1.15f;
                float sweep = Kit.EaseOut(t / 2f), thin = t < 6f ? 1f : 1f - Kit.EaseIn((t - 6f) / 12f);
                Quaternion turn = cam.transform.rotation * Quaternion.Euler(0f, 0f, Angle);
                Vector3 axis = turn * Vector3.right;
                Vector3 mid = cam.transform.position + cam.transform.forward * dist;
                foreach (var (go, mat, c, thick) in parts)
                {
                    go.SetActive(true);
                    go.transform.SetPositionAndRotation(mid + axis * (-len * 0.5f + len * sweep * 0.5f), turn);
                    go.transform.localScale = new Vector3(len * sweep, h * thick * thin * (t < 2f ? 1.5f : 1f), 1f);
                    mat.color = Kit.A(c, Mathf.Clamp01(thin * 1.4f));
                }
                return true;
            });
            f.end = () =>
            {
                foreach (var (go, mat, _, _) in parts)
                {
                    if (go != null) Destroy(go);
                    if (mat != null) Destroy(mat);
                }
            };
        }

        (GameObject, Material, Color, float) Band(Color c, int queue, float thick)
        {
            var mat = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave, renderQueue = queue, color = c };
            var go = new GameObject("Screen slash");
            go.AddComponent<MeshFilter>().sharedMesh = kit.meshQuad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
            return (go, mat, c, thick);
        }

        // ---------------------------------------------------------------- events

        /// <summary>The edge skills' events (from <see cref="OnFx"/>); false if it is not one of them.</summary>
        bool OnEdgeFx(SfFxEvent e)
        {
            switch (e.kind)
            {
                case SfFxKind.EdgeCast: EdgeCast(e); return true;
                case SfFxKind.EdgeCancel:
                    kit.PuffBurst(e.at, kit.grey, 5, 0.4f, 0.16f, 0.45f, 0.2f);
                    if (e.by.Pawn != null) Afterimage(e.by.Pawn, kit.grey, 1.3f, 0.2f, 0.45f);
                    return true;
                case SfFxKind.KingStab: KingStab(e); return true;
                case SfFxKind.KingVault: KingVault(e); return true;
                case SfFxKind.KingLand: KingLand(e); return true;
                case SfFxKind.KingLandHit: Hit(e, kit.king, 0, 0.16f); return true;
                case SfFxKind.KingDrop: KingDrop(e); return true;
                case SfFxKind.QueenMark:
                    if (e.target != null) kit.FlashRing(Kit.FloorUnder(e.target.Hips.position, 0.6f), 0.7f, kit.queen);
                    Punch(e.by.Pawn, e.target, 4f, 12);
                    return true;
                case SfFxKind.QueenBlink: QueenBlink(e); return true;
                case SfFxKind.QueenCut: QueenCut(e); return true;
                case SfFxKind.QueenMiss: QueenMiss(e); return true;
                case SfFxKind.RookBreak: RookBreak(e); return true;
                case SfFxKind.BishopReach: BishopReach(e); return true;
                case SfFxKind.BishopCatch: BishopCatch(e); return true;
                case SfFxKind.BishopSet: BishopSet(e); return true;
                case SfFxKind.BishopDrop: BishopDrop(e); return true;
                case SfFxKind.KnightCrouch: KnightCrouch(e); return true;
                case SfFxKind.KnightFlip: KnightFlip(e); return true;
                case SfFxKind.KnightKick: KnightKick(e); return true;
                case SfFxKind.KnightKickHit: Hit(e, kit.knight, 5, 0.16f); return true;
            }
            return false;
        }
    }
}
