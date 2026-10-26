using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The Pawn Rush skill effects in design A "잉크 테두리 장난감 체스" (R90, 승규 님: "A 디자인으로 폰러쉬 스킬 디자인
    /// 맞춰줘"; the design picked 2026-10-08 for all three modes, the Queen of the Hill skills already drawn in it, R89).
    /// The skills' telegraphs move here from the plain lines (SkillMarks) and become real 1.5 m board squares and rings
    /// on the floor along each piece's own way, coloured for the viewer (the piece's colour with a cream rim for mine,
    /// teal for my side's, red with sliding stripes for the other side's): they fill as the windup runs, snap full in its
    /// last six frames, flash white for two and are gone in six. Hits use the shared language of the parts
    /// (<see cref="SkillInkKit"/>): the body white for two frames then the hitter's colour for two, a C half ring flung the
    /// way the piece flies, round drops, the captured square under a fallen piece with four cream dust puffs, three little
    /// cream pawns over a dazed head. The approved looks stay (the queen's gold chess pieces and fire R88, the rook's
    /// lightning and the knight's wind R86, the bishop's squares and wire R82) and only get A's finish: the queen's crown
    /// ring of eight pearls, the rook's battlement ring on a wall, the bishop's X prop and team rim, the knight's horseshoe
    /// landing mark that shrinks to touchdown, the pawn's two stamped squares.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        Kit kit;
        SkillAir air;
        const float Frame = Kit.F;
        const float Square = 1.5f;

        // The person at this PC: P1 offline, this PC's own pawn online (R111).
        RagdollPawn Viewer => game != null ? game.LocalPawn : ViewerOverride != null ? ViewerOverride() : null;
        Vector3 EyePoint => ViewCamera != null ? ViewCamera.transform.position : Vector3.up * 5f;

        void BuildDesignA()
        {
            kit = new Kit(root, () => Viewer, () => EyePoint)
            {
                pawnMesh = chessMeshes != null && chessMeshes.Length > 0 ? chessMeshes[0] : null,
            };
            RagdollPawn.SkillMarksHidden = true;
            BuildBoardLook();
            // R138: real dust in place of the cartoon puffs, the queen's shock wave, the knight's badge (R139: the knight's
            // ghost is gone); R140 the bishop's rope (Skill Rope).
            kit.View = () => ViewCamera;
            kit.UseRealDust();
            // 10-25 (승규 님: "폰 러쉬 퀸 오브 더 힐 먼지 퍼프도 다 지워줘"): no dust or puffs at all.
            kit.NoPuffs = true;
            BuildShock();
            BuildKnightFx();
            BuildRope();
            BuildStake();
            // R141: the air shoved back as a piece springs off (SkillAir), in place of what was drawn out of its back.
            air = new SkillAir(root);
        }

        void DestroyDesignA()
        {
            DestroyShock();
            DestroyReal();
            DestroyLasso();
            DestroyKnightFx();
            DestroyRope();
            DestroyStake();
            DestroyKingCall();
            DestroyChainMeshes();
            air?.Destroy();
            air = null;
            kit?.Destroy();
            kit = null;
            RagdollPawn.SkillMarksHidden = false;
        }

        /// <summary>What each piece's skill shows while its state lasts: the warnings, the bishop's throw, the targets.</summary>
        void WatchDesignA()
        {
            if (kit == null) return;
            RagdollPawn.SkillMarksHidden = effects;
            if (!effects) return;
            foreach (var pawn in RagdollPawn.All)
            {
                if (pawn == null || pawn.PawnRushSkills == null) continue;
                switch (pawn.Piece)
                {
                    case PieceKind.Queen: if (Real(PieceKind.Queen)) RealQueenWarning(pawn); else QueenWarning(pawn); break;
                    case PieceKind.Rook: RookChainFx(pawn); break;   // R153: the chain (PawnRushSkillFx.Chain.cs)
                    case PieceKind.Bishop:
                        if (Params != null && Params.bishopRope)
                        {
                            // 10-26: the X rope (PawnRushSkillFx.BishopRope.cs).
                            BishopLassoAim(pawn);
                            BishopLassoFx(pawn);
                        }
                        else
                        {
                            if (Real(PieceKind.Bishop)) RealBishopAim(pawn); else BishopAimTiles(pawn);
                            BishopThrowProp(pawn);
                        }
                        break;
                    case PieceKind.Knight: KnightMarks(pawn); KnightTargetRing(pawn); break;
                    case PieceKind.Pawn: PawnTargetRing(pawn); break;
                }
            }
            foreach (var kv in tileSets) WireWrapper(kv.Key, kv.Value);
        }

        // ---------------------------------------------------------------- shared pieces of the A layer

        /// <summary>An inked ribbon trailing a moving point while <paramref name="where"/> gives one (<paramref name="seconds"/>
        /// long, <paramref name="width"/> at the head down to nothing at the tail), then shrinking away.</summary>
        void InkTrail(Func<Vector3?> where, Kit.Palette p, float seconds, float width, Color? core = null)
        {
            var strip = new Kit.Strip(kit, p) { core = core ?? p.light, coreShare = 0.3f, inkShare = 0.22f };
            var pts = new List<Vector3>();
            var times = new List<float>();
            bool ended = false;
            var f = kit.Run(30f, (fx, d) =>
            {
                var w = ended ? null : where();
                if (w.HasValue) { pts.Add(w.Value); times.Add(fx.age); }
                else ended = true;
                while (times.Count > 0 && fx.age - times[0] > seconds) { pts.RemoveAt(0); times.RemoveAt(0); }
                if (pts.Count < 2)
                {
                    strip.Hide();
                    return !ended;
                }
                strip.Build(pts, i => width * i / Mathf.Max(1f, pts.Count - 1f), kit.Eye);
                strip.taper = 0f;
                strip.Apply();
                return true;
            });
            f.strips.Add(strip);
        }

        /// <summary>A hit in design A: the body white for two frames then the hitter's colour for two, round drops off
        /// the way it is pushed, a hit stop of so many frames and a shake (the hitter's and the hit one's view).</summary>
        void InkHit(RagdollPawn target, Vector3 at, Vector3 push, Kit.Palette p, int stopFrames, float shakeMetres, int shakeFrames, int drops = 3)
        {
            kit.FlashBody(target, p.main);
            if (drops > 0) kit.Drops(at, push, p, drops);
            HitStop(stopFrames * Frame);
            Shake(shakeMetres, shakeFrames * Frame);
        }

        /// <summary>A flat ring arc swept round a point (a crescent): it opens over <paramref name="sweepFrames"/> to
        /// <paramref name="degrees"/> from <paramref name="startDir"/> (clockwise seen from above), white core, the colour,
        /// ink, then is eaten away; lying level, or standing if <paramref name="normal"/> is given.</summary>
        void Crescent(Vector3 at, Vector3 startDir, Kit.Palette p, float radius, float band, float degrees, int sweepFrames, Vector3 normal = default)
        {
            var t = new Kit.Tile(kit, "Crescent");
            bool level = normal.sqrMagnitude < 1e-6f;
            var f = kit.Run((sweepFrames + 10) * Frame, (fx, d) =>
            {
                float a = fx.age / Frame;
                if (level) t.Floor(at, startDir, Vector2.one * (2f * radius));
                else t.Facing(at, normal, Vector2.one * (2f * radius), startDir);
                t.shape = 1f;
                t.inner = Mathf.Clamp01(1f - band / Mathf.Max(0.05f, radius));
                t.fill = Kit.A(p.main, 1f);
                t.metal = p.metal;
                t.core = Kit.A(p.light, 1f);
                t.coreWidth = Mathf.Min(0.035f, band * 0.3f);
                t.ink = Kit.A(p.ink, 1f);
                t.inkWidth = 0.025f;
                t.rim = Color.clear;
                t.stripe = Color.clear;
                float open = Mathf.Clamp01(a / sweepFrames), eaten = Mathf.Clamp01((a - sweepFrames) / 10f);
                t.arc = degrees / 360f * Kit.EaseOut(open) * (1f - eaten);
                t.fade = 1f;
                t.Apply();
                return true;
            });
            f.tiles.Add(t);
        }

        /// <summary>A level ring of the colour at a point, popping 0.3 → 1.1 → 1 of <paramref name="radius"/> and eaten round
        /// (a stomp at head height, the knight's corner).</summary>
        void FlatImpactRing(Vector3 at, Kit.Palette p, float r0, float r1, Vector3 normal = default)
        {
            var t = new Kit.Tile(kit, "Level ring");
            bool level = normal.sqrMagnitude < 1e-6f;
            var f = kit.Run(14f * Frame, (fx, d) =>
            {
                float a = fx.age / Frame;
                float k = a < 3f ? Mathf.Lerp(0f, 1.1f, a / 3f) : a < 6f ? Mathf.Lerp(1.1f, 1f, (a - 3f) / 3f) : 1f;
                float r = k > 1f ? r1 * k : Mathf.Lerp(r0, r1, k);
                if (level) t.Floor(at, Vector3.forward, Vector2.one * (2f * r));
                else t.Facing(at, normal, Vector2.one * (2f * r));
                t.shape = 1f;
                t.inner = 0.7f;
                t.fill = Kit.A(p.main, 1f);
                t.metal = p.metal;
                t.core = Kit.A(Color.white, 1f);
                t.coreWidth = 0.04f;
                t.ink = Kit.A(p.ink, 1f);
                t.inkWidth = 0.03f;
                t.rim = Color.clear;
                t.stripe = Color.clear;
                t.arc = a < 6f ? 1f : 1f - (a - 6f) / 8f;
                t.fade = 1f;
                t.Apply();
                return true;
            });
            f.tiles.Add(t);
        }

        /// <summary>Paint a warning tile as <see cref="SkillInkKit.Warn"/> does, then set how much its fill covers.</summary>
        void WarnFill(Kit.Tile t, RagdollPawn caster, float fillAlpha, float strength, float flash)
        {
            kit.Warn(t, caster, strength, flash);
            var p = kit.PieceColors(caster);
            t.fill = Kit.A(flash > 0f ? Color.white : p.main, fillAlpha);
            t.Apply();
        }

        // ---------------------------------------------------------------- queen

        /// <summary>
        /// The queen's warning while she charges (A): a dashed ring at her reach (3 m, sixteen dashes), a solid ring at
        /// her inner reach (1.5 m), the inner disc filled a quarter (the knockdown zone) and the band outside it a little
        /// less (the push), in her gold with the viewer's rim (the other side's with sliding stripes); they grow stronger
        /// as she charges with a white sweep running round the edge, snap full in her last six frames, flash white as she
        /// lets go and are gone in six. She coils down while she charges (10% lower), not raising her hands.
        /// </summary>
        void QueenWarning(RagdollPawn q)
        {
            if (q.SkillStage != SkillStage.Windup) return;
            var s = Params;
            float windup = s != null ? s.queenWindup : 0.35f, inner = s != null ? s.queenInner : 1.5f, outer = s != null ? s.queenRadius : 3f;
            float charge = Kit.Charge(q.SkillStageTime, windup);
            kit.Coil(q, Mathf.Clamp01(q.SkillStageTime / Mathf.Max(0.05f, windup)));
            kit.Keep(q, "queen warning", () =>
            {
                var fx = new Kit.Fx();
                for (int i = 0; i < 4 + 16; i++) fx.tiles.Add(new Kit.Tile(kit, "Queen warning"));
                Vector3 c = FloorUnder(q) + Vector3.up * 0.016f;
                float released = -1f, k = 0.3f;
                fx.step = (x, d) =>
                {
                    if (q == null) return false;
                    if (!kit.Held(x) && released < 0f) released = x.age;
                    float a = released >= 0f ? (x.age - released) / Frame : -1f;
                    if (a >= 6f) return false;
                    if (released < 0f) k = Kit.Charge(q.SkillStageTime, windup);
                    float flash = a >= 0f && a < 2f ? 1f : 0f;
                    float fade = released < 0f ? Mathf.Clamp01(x.age / (4f * Frame)) : 1f - Mathf.Clamp01((a - 2f) / 4f);
                    var p = kit.queen;
                    // The inner disc and the band round it.
                    var disc = x.tiles[0];
                    disc.Floor(c, Vector3.forward, Vector2.one * (2f * inner));
                    disc.shape = 2f;
                    disc.thick = 0f;
                    disc.shadow = 0f;
                    disc.fade = fade;
                    WarnFill(disc, q, 0.25f * Mathf.Lerp(0.5f, 1f, k), k, 0f);
                    disc.inkWidth = 0f;
                    disc.rim = Color.clear;
                    disc.metal = 0f;
                    disc.bevel = Color.clear;
                    if (board) BoardZone(disc, q, 0.4f * k, flash);
                    disc.Apply();
                    var band = x.tiles[1];
                    band.Floor(c - Vector3.up * 0.002f, Vector3.forward, Vector2.one * (2f * outer));
                    band.shape = 1f;
                    band.inner = inner / outer;
                    band.thick = 0f;
                    band.shadow = 0f;
                    band.fade = fade;
                    WarnFill(band, q, 0.15f * Mathf.Lerp(0.5f, 1f, k), k, 0f);
                    band.inkWidth = 0f;
                    band.rim = Color.clear;
                    band.metal = 0f;
                    band.bevel = Color.clear;
                    if (board) BoardZone(band, q, 0.24f * k, flash);
                    band.Apply();
                    // The solid inner ring, 0.18 m.
                    var ring = x.tiles[2];
                    ring.Floor(c + Vector3.up * 0.002f, Vector3.forward, Vector2.one * (2f * inner));
                    ring.shape = 1f;
                    ring.inner = 1f - 0.18f / inner;
                    ring.fade = fade;
                    WarnFill(ring, q, Mathf.Lerp(0.55f, 1f, k), k, flash);
                    ring.inkWidth = 0.035f;
                    ring.rim = Color.clear;
                    ring.core = Color.clear;
                    ring.Apply();
                    // The white sweep running round it once as she charges.
                    var sweep = x.tiles[3];
                    float turn = Mathf.Clamp01(q.SkillStageTime / Mathf.Max(0.05f, windup)) * 360f;
                    sweep.Floor(c + Vector3.up * 0.004f, Quaternion.Euler(0f, turn, 0f) * Vector3.forward, Vector2.one * (2f * inner));
                    sweep.shape = 1f;
                    sweep.inner = 1f - 0.18f / inner;
                    sweep.fill = Kit.A(Color.white, 0.9f);
                    sweep.ink = Color.clear;
                    sweep.core = Color.clear;
                    sweep.rim = Color.clear;
                    sweep.stripe = Color.clear;
                    sweep.arc = 0.07f;
                    sweep.baseColor = Color.clear;   // board look: no walls of its own, it lies on the ring's top
                    sweep.shadow = 0f;
                    sweep.fade = released < 0f ? fade : 0f;
                    sweep.Apply();
                    // Sixteen dashes at her reach, 0.12 m wide.
                    for (int i = 0; i < 16; i++)
                    {
                        var dash = x.tiles[4 + i];
                        dash.Floor(c + Vector3.up * 0.002f, Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward, Vector2.one * (2f * outer));
                        dash.shape = 1f;
                        dash.inner = 1f - 0.12f / outer;
                        dash.arc = 1f / 32f;
                        dash.fade = fade;
                        WarnFill(dash, q, Mathf.Lerp(0.55f, 1f, k), k, flash);
                        dash.inkWidth = 0.025f;
                        dash.rim = Color.clear;
                        dash.Apply();
                    }
                    return true;
                };
                return fx;
            });
        }

        /// <summary>The board look's zone (R125): no fill, only sliding stripes (gold for mine and my side's, mauve for
        /// the other side's), so the board's own squares stay clean under it.</summary>
        void BoardZone(Kit.Tile t, RagdollPawn q, float alpha, float flash)
        {
            bool enemy = kit.SideOf(q) == Kit.Side.Enemy;
            t.fill = Kit.A(Color.white, flash > 0f ? 0.6f : 0f);
            t.stripe = Kit.A(enemy ? kit.enemy.main : kit.queen.main, alpha);
            t.stripePhase = kit.Clock * 0.5f;
        }

        /// <summary>
        /// The queen's crown ring (A): riding the front of her shock wave (R138; the rim of her ring of fire puffs before) as
        /// it races out to her reach in eight frames, a gold ring with eight round pearls at the queen's eight ways (a crown
        /// seen from above); the knockdown zone's edge gets a thick bright gold ring (0.35 m) and her reach a thin one (0.1 m).
        /// </summary>
        void QueenCrown(Vector3 c, float inner, float outer)
        {
            var p = kit.queen;
            var f = kit.Run(0.55f, (fx, d) =>
            {
                float a = fx.age / Frame;
                float k = Kit.EaseOut(a / 8f);
                float r = Mathf.Lerp(0.2f, outer, k);
                float fade = a < 14f ? 1f : 1f - Mathf.Clamp01((a - 14f) / 12f);
                var ring = fx.tiles[0];
                ring.Floor(c + Vector3.up * 0.03f, Vector3.forward, Vector2.one * (2f * r));
                ring.shape = 1f;
                ring.inner = Mathf.Clamp01(1f - 0.16f / Mathf.Max(0.3f, r));
                ring.fill = Kit.A(p.main, 1f);
                ring.metal = p.metal;
                ring.core = Kit.A(p.light, 1f);
                ring.coreWidth = 0.035f;
                ring.ink = Kit.A(p.ink, 1f);
                ring.inkWidth = 0.03f;
                ring.rim = Color.clear;
                ring.stripe = Color.clear;
                ring.fade = fade;
                ring.Apply();
                for (int i = 0; i < 8; i++)
                {
                    var pearl = fx.props[i];
                    float ang = i * 45f * Mathf.Deg2Rad;
                    pearl.t.position = c + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * r + Vector3.up * 0.14f;
                    pearl.t.localScale = Vector3.one * 0.24f * Mathf.Lerp(0.2f, 1f, k) * (fade > 0f ? Mathf.Lerp(0.3f, 1f, fade) : 0f);
                    pearl.Show(fade > 0.02f);
                }
                // The two zone edges, popping in as the ring passes and held a moment.
                float zone = a < 26f ? 1f : 1f - Mathf.Clamp01((a - 26f) / 7f);
                var thick = fx.tiles[1];
                float inPop = Kit.Pop(Mathf.Max(0f, fx.age - 4f * Frame), 1.08f, 0.8f);
                thick.Floor(c + Vector3.up * 0.02f, Vector3.forward, Vector2.one * (2f * inner * inPop));
                thick.shape = 1f;
                thick.inner = Mathf.Clamp01(1f - 0.35f / inner);
                thick.fill = Kit.A(p.main, 1f);
                thick.metal = p.metal;
                thick.core = Kit.A(p.light, 1f);
                thick.coreWidth = 0.06f;
                thick.ink = Kit.A(p.ink, 1f);
                thick.inkWidth = 0.035f;
                thick.rim = Color.clear;
                thick.stripe = Color.clear;
                thick.fade = a < 4f ? 0f : zone;
                thick.Apply();
                var thin = fx.tiles[2];
                float outPop = Kit.Pop(Mathf.Max(0f, fx.age - 7f * Frame), 1.05f, 0.9f);
                thin.Floor(c + Vector3.up * 0.02f, Vector3.forward, Vector2.one * (2f * outer * outPop));
                thin.shape = 1f;
                thin.inner = Mathf.Clamp01(1f - 0.1f / outer);
                thin.fill = Kit.A(p.main, 1f);
                thin.metal = p.metal;
                thin.core = Color.clear;
                thin.ink = Kit.A(p.ink, 1f);
                thin.inkWidth = 0.025f;
                thin.rim = Color.clear;
                thin.stripe = Color.clear;
                thin.fade = a < 7f ? 0f : zone;
                thin.Apply();
                return true;
            });
            for (int i = 0; i < 3; i++) f.tiles.Add(new Kit.Tile(kit, "Queen crown"));
            for (int i = 0; i < 8; i++)
            {
                var pearl = new Kit.Prop(kit, kit.meshBall, board ? BPearl : p, "Queen pearl") { inkWidth = 0.014f };
                pearl.Show(false);
                f.props.Add(pearl);
            }
        }

        // ---------------------------------------------------------------- rook

        /// <summary>The rook digging in (A's lock): eight square stone puffs pushed out round its base and sinking away.</summary>
        void StoneRing(Vector3 feet)
        {
            var mesh = board ? kit.RoundBox(new Vector3(1f, 0.45f, 1f), 0.1f) : kit.RoundBox(Vector3.one, 0.22f);
            var f = kit.Run(0.4f, (fx, d) =>
            {
                float a = fx.age / Frame;
                for (int i = 0; i < fx.props.Count; i++)
                {
                    var p = fx.props[i];
                    float ang = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    float out01 = Kit.EaseOut(a / 8f);
                    float size = 0.25f * Kit.Pop(fx.age, 1.15f) * (1f - Mathf.Clamp01((fx.age - 0.22f) / 0.18f));
                    p.t.position = feet + dir * Mathf.Lerp(0.45f, 0.8f, out01) + Vector3.up * (size * 0.5f);
                    p.t.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, 8f * Mathf.Sin(i * 1.7f));
                    p.t.localScale = Vector3.one * Mathf.Max(0.001f, size);
                    p.Show(size > 0.005f);
                }
                return true;
            });
            for (int i = 0; i < 8; i++)
            {
                var p = new Kit.Prop(kit, mesh, board ? BOrange : Kit.Stone, "Stone puff") { inkWidth = 0.014f, twoTone = board };
                p.Apply();
                p.Show(false);
                f.props.Add(p);
            }
        }

        /// <summary>
        /// The rook's battlement ring (A), slammed flat on what stopped it (a wall, a barricade, the floor under an air
        /// charge): a ring of its orange with eight square merlons round it (white core, orange, ink), 0.5 → 1.1 → 1 in six
        /// frames, eaten away from frame 20 to 40; a white flash on the contact for two frames; square stone puffs.
        /// </summary>
        void BattlementRing(Vector3 at, Vector3 normal, float radius)
        {
            var p = board ? BSilver : kit.rook;
            normal = normal.sqrMagnitude > 1e-4f ? normal.normalized : Vector3.up;
            Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            var f = kit.Run(42f * Frame, (fx, d) =>
            {
                float a = fx.age / Frame;
                float k = Kit.Pop(fx.age, 1.1f, 0.5f);
                float eaten = Mathf.Clamp01((a - 20f) / 20f);
                var ring = fx.tiles[0];
                ring.Facing(at + normal * 0.03f, normal, Vector2.one * (2f * radius * k), up);
                ring.shape = 1f;
                ring.inner = 0.72f;
                ring.fill = Kit.A(p.main, 1f);
                ring.metal = p.metal;
                ring.core = Kit.A(board ? BOrange.main : p.light, 1f);
                ring.coreWidth = 0.05f;
                ring.ink = Kit.A(p.ink, 1f);
                ring.inkWidth = 0.035f;
                ring.rim = Color.clear;
                ring.stripe = Color.clear;
                ring.arc = 1f - eaten;
                ring.fade = 1f;
                ring.Apply();
                Vector3 right = Vector3.Cross(up, normal).normalized, upOnWall = Vector3.Cross(normal, right).normalized;
                for (int i = 0; i < 8; i++)
                {
                    var tooth = fx.tiles[1 + i];
                    float ang = i * 45f * Mathf.Deg2Rad;
                    Vector3 radial = right * Mathf.Sin(ang) + upOnWall * Mathf.Cos(ang);
                    float tr = radius * k * 1.06f;
                    tooth.Facing(at + normal * 0.031f + radial * tr, normal, new Vector2(0.28f, 0.28f) * k, radial);
                    tooth.shape = 0f;
                    tooth.round = 0.04f;
                    tooth.fill = Kit.A(p.main, 1f);
                    tooth.metal = p.metal;
                    tooth.core = Kit.A(board ? BOrange.main : p.light, 1f);
                    tooth.coreWidth = 0.03f;
                    tooth.ink = Kit.A(p.ink, 1f);
                    tooth.inkWidth = 0.03f;
                    tooth.rim = Color.clear;
                    tooth.stripe = Color.clear;
                    tooth.arc = 1f;
                    tooth.fade = i / 8f < 1f - eaten ? 1f : 0f;
                    tooth.Apply();
                }
                var flash = fx.tiles[9];
                flash.Facing(at + normal * 0.035f, normal, Vector2.one * (radius * 1.3f), up);
                flash.shape = 2f;
                flash.thick = 0f;
                flash.shadow = 0f;
                flash.fill = Kit.A(Color.white, 1f);
                flash.ink = Color.clear;
                flash.core = Color.clear;
                flash.rim = Color.clear;
                flash.stripe = Color.clear;
                flash.fade = a < 2f ? 1f : 0f;
                flash.Apply();
                return true;
            });
            for (int i = 0; i < 10; i++) f.tiles.Add(new Kit.Tile(kit, "Battlement ring"));
            kit.PuffBurst(at + normal * 0.25f, board ? kit.dust : Kit.Stone, 6, 0.6f, 0.26f, 0.5f, 0.2f);
        }

        /// <summary>The face a charge met, pointing back out of it (R92): a ray along the charge finds the face itself
        /// (a sphere cast's normal leans round an edge it touches); failing that the cast's own normal, or straight back.</summary>
        static Vector3 WallFace(Vector3 at, Vector3 run, Vector3 cast)
        {
            Vector3 n = cast.sqrMagnitude > 1e-4f ? cast.normalized : -run;
            if (NearestHit(at - run * 0.4f, run, 0.9f, out var h)) n = h.normal;
            return Vector3.Dot(n, run) > 0f ? -n : n;
        }

        /// <summary>
        /// Where the battlement ring goes on a wall, and how big (R92): its middle slid along the face by at most 0.6 m
        /// and, if that is not enough, the ring made smaller (down to 0.45 m), so that all of it, merlons and the pop's
        /// 1.1 included, stays on the face the charge met: out of the floor, and not over the face's edges into the air.
        /// onFace false (a barricade, gone the moment it breaks): only the floor counts.
        /// </summary>
        static Vector3 FitOnWall(Vector3 at, Vector3 n, ref float radius, bool onFace)
        {
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, n);
            if (up.sqrMagnitude < 0.25f) return at;   // more floor than wall
            up.Normalize();
            Vector3 right = Vector3.Cross(n, up);
            const float slide = 0.6f;
            float half = RingHalf(radius), look = half + slide + 0.1f;
            float below = look, above = look, left = look, rightRoom = look;
            if (onFace)
            {
                below = FaceRoom(at, n, -up, look);
                above = FaceRoom(at, n, up, look);
                left = FaceRoom(at, n, -right, look);
                rightRoom = FaceRoom(at, n, right, look);
            }
            if (NearestHit(at + n * 0.35f, Vector3.down, 4f, out var floor, true))
                below = Mathf.Min(below, (at.y - floor.point.y - 0.03f) / up.y);
            float fits = Mathf.Min(Room(below, above, slide), Room(left, rightRoom, slide));
            radius = Mathf.Max(0.45f, Mathf.Min(radius, (fits / 1.1f - 0.14f) / 1.06f));
            half = RingHalf(radius);
            return at + up * Slide(below, above, half, slide) + right * Slide(left, rightRoom, half, slide);
        }

        /// <summary>How far the battlement ring reaches from its middle at the pop's peak (1.1): its merlons' outer edges.</summary>
        static float RingHalf(float radius) => 1.1f * (1.06f * radius + 0.14f);

        /// <summary>The biggest half size that fits between lo one way and hi the other, the middle slid at most slide.</summary>
        static float Room(float lo, float hi, float slide) => Mathf.Min((lo + hi) * 0.5f, Mathf.Min(lo, hi) + slide);

        /// <summary>The least slide (at most slide either way) that keeps half each way inside lo and hi.</summary>
        static float Slide(float lo, float hi, float half, float slide) =>
            Mathf.Clamp(Mathf.Max(0f, half - lo) - Mathf.Max(0f, half - hi), -slide, slide);

        /// <summary>How far the face goes from at along dir, up to max: out 0.1 m at a time while a short ray back at it
        /// still meets the same face, square on and where it was.</summary>
        static float FaceRoom(Vector3 at, Vector3 n, Vector3 dir, float max)
        {
            for (float s = 0.1f; s <= max + 1e-3f; s += 0.1f)
                if (!NearestHit(at + dir * s + n * 0.15f, -n, 0.3f, out var h) || Vector3.Dot(h.normal, n) < 0.9f) return s - 0.1f;
            return max;
        }

        static readonly RaycastHit[] wallHits = new RaycastHit[16];

        /// <summary>The nearest solid thing a ray meets, the pieces and loose bodies aside (floor: only what faces up).</summary>
        static bool NearestHit(Vector3 from, Vector3 dir, float length, out RaycastHit hit, bool floor = false)
        {
            hit = default;
            float best = float.MaxValue;
            int count = Physics.RaycastNonAlloc(from, dir, wallHits, length, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var h = wallHits[i];
                if (h.distance >= best || RagdollPawn.ColliderOwner.ContainsKey(h.collider) || (floor && h.normal.y < 0.5f)) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                best = h.distance;
                hit = h;
            }
            return best < float.MaxValue;
        }

        // ---------------------------------------------------------------- bishop

        /// <summary>
        /// The bishop's aim, on its own screen only (A): its two diagonals as rows of small violet squares (0.4 m, every
        /// 0.6 m) with a lavender bishop's-mitre diamond in the middle, fading in over 0.2 s, and a thin cream ring at its
        /// reach. Gone where it cannot be laid.
        /// </summary>
        void BishopAimTiles(RagdollPawn b)
        {
            if (!b.BishopAiming || b != Viewer) return;
            var s = Params;
            float length = s != null ? s.bishopLineLength : 4.2f, range = s != null ? s.bishopRange : 4.5f;
            kit.Keep(b, "bishop aim", () =>
            {
                var fx = new Kit.Fx();
                for (int i = 0; i < 14; i++) fx.tiles.Add(new Kit.Tile(kit, "Bishop aim"));
                fx.step = (x, d) =>
                {
                    if (b == null || !kit.Held(x)) return false;
                    float fade = Mathf.Clamp01(x.age / 0.2f);
                    bool valid = b.BishopAimValid;
                    Vector3 c = b.BishopAimPoint + Vector3.up * 0.02f, yaw = Kit.FlatDir(b.BishopAimYaw, Vector3.forward);
                    Vector3 d1 = Quaternion.Euler(0f, 45f, 0f) * yaw, d2 = Quaternion.Euler(0f, -45f, 0f) * yaw;
                    int k = 0;
                    var p = kit.bishop;
                    foreach (var line in new[] { d1, d2 })
                        for (int i = -3; i <= 3; i++)
                        {
                            if (i == 0) continue;
                            float off = i * 0.6f;
                            if (Mathf.Abs(off) > length * 0.5f) continue;
                            var t = x.tiles[k++];
                            t.Floor(c + line * off, line, Vector2.one * 0.4f);
                            t.shape = 0f;
                            t.round = 0.05f;
                            t.fill = Kit.A(p.main, 0.3f);
                            t.ink = Kit.A(p.ink, 0.9f);
                            t.inkWidth = 0.03f;
                            t.core = Color.clear;
                            t.rim = Color.clear;
                            t.stripe = Color.clear;
                            t.fade = valid ? fade : 0f;
                            if (board)
                            {
                                t.fill = Kit.A(BSquare.main, 0.85f);
                                t.bevel = Kit.A(BSquare.light, 0.8f);
                                t.bevelWidth = 0.04f;
                                t.ink = Kit.A(BNavy, 0.9f);
                                t.thick = 0.03f;
                            }
                            t.Apply();
                        }
                    var mitre = x.tiles[12];
                    mitre.Floor(c + Vector3.up * 0.002f, d1, Vector2.one * 0.6f);
                    mitre.shape = 0f;
                    mitre.round = 0.08f;
                    mitre.fill = Kit.A(p.light, 0.95f);
                    mitre.ink = Kit.A(p.ink, 1f);
                    mitre.inkWidth = 0.035f;
                    mitre.core = Kit.A(p.main, 0.9f);
                    mitre.coreWidth = 0.05f;
                    mitre.rim = Color.clear;
                    mitre.stripe = Color.clear;
                    mitre.fade = valid ? fade : 0f;
                    if (board)
                    {
                        mitre.fill = Kit.A(BSilver.main, 1f);
                        mitre.metal = 1f;
                        mitre.core = Color.clear;
                        mitre.ink = Kit.A(BNavy, 1f);
                    }
                    mitre.Apply();
                    var reach = x.tiles[13];
                    reach.Floor(b.FeetPoint + Vector3.up * 0.012f, Vector3.forward, Vector2.one * (2f * range));
                    reach.shape = 1f;
                    reach.inner = 1f - 0.05f / range;
                    reach.fill = Kit.A(kit.selfFill, 0.5f);
                    reach.ink = Kit.A(kit.selfInk, 0.35f);
                    reach.inkWidth = 0.012f;
                    reach.core = Color.clear;
                    reach.rim = Color.clear;
                    reach.stripe = Color.clear;
                    reach.fade = fade * 0.8f;
                    reach.thick = 0f;
                    reach.shadow = 0f;
                    reach.Apply();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>The bishop's throw (A): its X folded up, two violet rods with lavender balls at the ends, spinning
        /// twice along an arc to where it lands, a violet ribbon behind.</summary>
        void BishopThrowProp(RagdollPawn b)
        {
            if (b.SkillStage != SkillStage.Active) return;
            kit.Keep(b, "bishop throw", () =>
            {
                var fx = new Kit.Fx();
                var pivot = new GameObject("Bishop X").transform;
                pivot.SetParent(kit.root, false);
                var p = kit.bishop;
                // R153 4th (승규 님 10-22, Tripo "wooden cross stake"): the stakes it throws, crossed and tied with hemp, lying
                // flat and 0.9 m across; the violet rods and caps stand in when the model is missing.
                EnsureChainMeshes();
                if (crossParts.Count == 0) LoadChainModel("BishopCross", crossParts, "SkillModels");
                if (crossParts.Count > 0)
                    foreach (var (mesh, colors) in crossParts)
                    {
                        var part = new Kit.Prop(kit, mesh, colors.main == Walnut.main ? StakeWood : colors, "Bishop cross stake", pivot) { inkWidth = 0.008f };
                        part.t.localRotation = Quaternion.Euler(90f, 45f, 0f);
                        part.t.localScale = Vector3.one * 0.9f;
                        fx.props.Add(part);
                    }
                else
                {
                    var rod = kit.RoundBox(new Vector3(0.07f, 0.07f, 0.9f), 0.03f);
                    for (int i = 0; i < 2; i++)
                    {
                        var r = new Kit.Prop(kit, rod, board ? BSquare : p, "Bishop rod", pivot) { inkWidth = 0.012f };
                        r.t.localRotation = Quaternion.Euler(0f, i == 0 ? 45f : -45f, 0f);
                        fx.props.Add(r);
                    }
                    var capColors = board ? BSilver : new Kit.Palette { main = p.light, light = Color.white, deep = p.main, ink = p.ink };
                    for (int i = 0; i < 4; i++)
                    {
                        var cap = new Kit.Prop(kit, kit.meshBall, capColors, "Bishop cap", pivot) { inkWidth = 0.012f };
                        float ang = (45f + 90f * i) * Mathf.Deg2Rad;
                        cap.t.localPosition = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * 0.45f;
                        cap.t.localScale = Vector3.one * 0.13f;
                        fx.props.Add(cap);
                    }
                }
                Vector3 from = b.BishopThrowFrom, to = b.BishopAimPoint;
                float flight = Mathf.Max(0.05f, b.BishopThrowTime);
                Vector3 pos = from;
                InkTrail(() => fx.dead || b == null || b.SkillStage != SkillStage.Active ? (Vector3?)null : pivot.position, p, 0.25f, 0.2f);
                fx.end = () => { if (pivot != null) Destroy(pivot.gameObject); };
                fx.step = (x, d) =>
                {
                    if (b == null || !kit.Held(x)) return false;
                    float t = Mathf.Clamp01(b.SkillStageTime / flight);
                    pos = Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.1f + 0.15f);
                    pivot.position = pos;
                    pivot.rotation = Quaternion.Euler(0f, t * 720f, 20f * Mathf.Sin(t * Mathf.PI));
                    pivot.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, t);
                    return true;
                };
                return fx;
            });
        }

        /// <summary>The bishop's tip puffs as its rods stab the floor (A): four small violet puffs at the ends of its X.</summary>
        void WireLanding(Vector3 center, Vector3 forward, float length)
        {
            Vector3 f = Kit.FlatDir(forward, Vector3.forward);
            float half = length * 0.5f;
            Vector3 d1 = Quaternion.Euler(0f, 45f, 0f) * f, d2 = Quaternion.Euler(0f, -45f, 0f) * f;
            foreach (var end in new[] { center - d1 * half, center + d1 * half, center - d2 * half, center + d2 * half })
                // R149: on the floor under each end (a dais edge), as the pegs.
                kit.PuffBurst((Kit.FloorUnder(end, 1f, out Vector3 floor) && Mathf.Abs(floor.y - end.y) <= 1f ? floor : end) + Vector3.up * 0.08f,
                              board ? kit.dust : kit.bishop, 2, 0.15f, 0.12f, 0.35f, 0.1f);
        }

        /// <summary>
        /// What A adds round the R82 wire while it lasts: a team rim round each of its squares (cream for mine, teal for my
        /// side's, red with stripes for the other side's); armed, a white band running out from the middle to the four
        /// ends in twelve frames; and, when the wire is gone, its four pegs sinking into the floor with two violet puffs
        /// each. (The brightness, 50% while arming, 40% at rest and three blinks in its last second, is the squares' own:
        /// <see cref="WireLevel"/>.)
        /// </summary>
        void WireWrapper(UnityEngine.Object source, Squares3D set)
        {
            if (set.ghost || source == null) return;
            var wire = source as SkillTripwire;
            if (wire == null) return;
            kit.Keep(source, "wire wrapper", () =>
            {
                var fx = new Kit.Fx();
                foreach (var _ in set.SquarePoints) fx.tiles.Add(new Kit.Tile(kit, "Wire team rim"));
                for (int i = 0; i < 4; i++) fx.strips.Add(new Kit.Strip(kit, kit.bishop) { core = Color.white });
                float armedAt = -1f;
                var ends = new Vector3[4];
                var owner = set.owner;
                float height = Params != null ? Params.bishopHeight : 0.25f;
                fx.end = () =>
                {
                    // The wire is gone: its pegs sink into the floor in eight frames, two violet puffs each.
                    SinkPegs(ends, height);
                };
                fx.step = (x, d) =>
                {
                    if (wire == null) return false;
                    if (!kit.Held(x)) return false;
                    for (int line = 0; line < 2; line++)
                    {
                        wire.LinePoints(line, out var a, out _, out var b);
                        ends[line * 2] = a;
                        ends[line * 2 + 1] = b;
                    }
                    if (wire.Armed && armedAt < 0f) armedAt = x.age;
                    float level = WireLevel(wire, armedAt >= 0f ? x.age - armedAt : -1f);
                    var side = kit.SideOf(owner);
                    int i = 0;
                    foreach (var (at, size, rotation) in set.SquarePoints)
                    {
                        var t = x.tiles[i++];
                        if (board) { t.Hide(); continue; }
                        t.t.SetPositionAndRotation(at + Vector3.up * 0.004f, rotation * Quaternion.LookRotation(Vector3.down, Vector3.forward));
                        t.Size(Vector2.one * size * 1.0f);
                        t.shape = 0f;
                        t.round = 0.05f;
                        t.fill = Color.clear;
                        t.core = Color.clear;
                        t.rim = Kit.A(side == Kit.Side.Self ? kit.selfFill : side == Kit.Side.Ally ? kit.ally.main : kit.enemy.main, 0.9f);
                        t.rimWidth = 0.06f;
                        t.ink = Kit.A(side == Kit.Side.Self ? kit.selfInk : side == Kit.Side.Ally ? kit.ally.ink : kit.enemy.ink, 0.9f);
                        t.inkWidth = 0.025f;
                        t.stripe = side == Kit.Side.Enemy ? Kit.A(kit.enemy.deep, 0.5f) : Color.clear;
                        t.stripePhase = kit.Clock * 0.5f;
                        t.fade = Mathf.Clamp01(level / 0.4f) * Mathf.Clamp01(x.age / 0.1f);
                        t.Apply();
                    }
                    // The armed band: white, 0.3 m, from the middle out to each end in twelve frames.
                    float run = armedAt >= 0f ? (x.age - armedAt) / (12f * Frame) : -1f;
                    for (int k = 0; k < 4; k++)
                    {
                        var st = x.strips[k];
                        if (run < 0f || run > 1.2f) { st.Hide(); continue; }
                        int line = k / 2;
                        wire.LinePoints(line, out var a, out var mid, out var b);
                        Vector3 end = k % 2 == 0 ? a : b, from = (a + b) * 0.5f;
                        Vector3 head = Vector3.Lerp(from, end, Mathf.Clamp01(run)), tail = Vector3.Lerp(from, end, Mathf.Clamp01(run - 0.3f / Mathf.Max(0.1f, Vector3.Distance(from, end))));
                        st.Build(new List<Vector3> { tail, head }, q => 0.07f, kit.Eye);
                        st.colors = new Kit.Palette { main = Color.white, light = Color.white, deep = kit.bishop.main, ink = kit.bishop.ink };
                        st.core = Color.white;
                        st.taper = 0.5f;
                        st.fade = 1f - Mathf.Clamp01((run - 1f) / 0.2f);
                        st.Apply();
                    }
                    return true;
                };
                return fx;
            });
        }

        /// <summary>The wire's brightness in A: half while it arms, a pulse to 110% as it arms, 40% at rest, and three
        /// blinks to full in its last second (0.2 s each).</summary>
        static float WireLevel(SkillTripwire wire, float sinceArmed)
        {
            if (wire == null) return 0f;
            if (sinceArmed < 0f) return 0.5f;
            float level = sinceArmed < 0.1f ? Mathf.Lerp(0.5f, 1.1f, sinceArmed / 0.1f)
                : sinceArmed < 0.45f ? Mathf.Lerp(1.1f, 0.4f, (sinceArmed - 0.1f) / 0.35f) : 0.4f;
            float left = wire.TimeLeft;
            if (left < 1f && left > 0f)
            {
                float into = 1f - left;   // 0..1 through the last second
                int blink = Mathf.FloorToInt(into / 0.2f);
                if (blink < 6) level = blink % 2 == 0 ? Mathf.Lerp(0.4f, 1f, Mathf.Sin((into % 0.2f) / 0.2f * Mathf.PI)) : 0.4f;
            }
            return level;
        }

        /// <summary>The wire gone (A): its four pegs sink into the floor in eight frames, two puffs each. R142: the wooden
        /// stakes with their hitches, as they stood (leaning out from the X's middle, each turned its own way), sinking
        /// faster and faster.</summary>
        void SinkPegs(Vector3[] ends, float height)
        {
            if (kit == null) return;
            Vector3 middle = Vector3.zero;
            int count = 0;
            foreach (var end in ends)
            {
                if (end == Vector3.zero) continue;
                middle += end - Vector3.up * height;
                count++;
            }
            if (count == 0) return;
            middle /= count;
            float sink = height + StakeHead + 0.02f;
            for (int i = 0; i < 4; i++)
            {
                if (ends[i] == Vector3.zero) continue;
                Vector3 foot = ends[i] - Vector3.up * height;
                Quaternion stand = StakeStand(foot, middle, Quaternion.identity);
                Quaternion turned = stand * Quaternion.Euler(0f, StakeYaw(i), 0f);
                Vector3 toMiddle = new Vector3(middle.x - foot.x, 0f, middle.z - foot.z);
                Quaternion hitch = stand * Quaternion.LookRotation(toMiddle.sqrMagnitude > 1e-6f ? toMiddle.normalized : Vector3.forward, Vector3.up);
                var stake = StakeProp(height, "Sinking stake");
                var knot = RopeProp(RopeKnotMesh(), "Sinking hitch", 0.006f);
                var f = kit.Run(8f * Frame, (fx, d) =>
                {
                    float k = Mathf.Clamp01(fx.age / (8f * Frame));
                    Vector3 at = foot - Vector3.up * (sink * k * k);
                    stake.t.SetPositionAndRotation(at, turned);
                    knot.t.SetPositionAndRotation(at + stand * (Vector3.up * height), hitch);
                    stake.Apply();
                    knot.Apply();
                    return true;
                });
                f.props.Add(stake);
                f.props.Add(knot);
                kit.PuffBurst(foot + Vector3.up * 0.05f, board ? kit.dust : kit.bishop, 2, 0.15f, 0.12f, 0.35f, 0.1f);
            }
        }

        // ---------------------------------------------------------------- knight

        /// <summary>Where a knight's leap went: from where it took off, through where it kicked off the air (an L).</summary>
        class LeapPath { public Vector3 start; public Vector3? corner; public float launchedAt; }
        readonly Dictionary<RagdollPawn, LeapPath> leapPaths = new Dictionary<RagdollPawn, LeapPath>();

        /// <summary>
        /// The knight's marks in the air (A): the squares of its way to where it comes down glow faintly in its sky blue
        /// (an L once it has kicked off the air), and the landing mark, R138 the horseshoe badge with an arrow over it
        /// (<see cref="LandingBadge"/>; a turning horseshoe ring before), shrinks from 1.3 m to 1 m exactly at touchdown. The enemy it
        /// would come down on (a second F) gets a red square at its feet (R139: the badge is the landing's only, "머리를 밟을
        /// 때나 꺾을 때 하지마 착지 할 때만"; a red badge in R138, a red ring before) and a pointer over its head, blinking while
        /// only found, steady once on the way.
        /// </summary>
        void KnightMarks(RagdollPawn k)
        {
            if (k.SkillStage != SkillStage.Active) return;
            if (k.KnightShowsLanding)
            {
                kit.Keep(k, "knight landing", () =>
                {
                    var fx = new Kit.Fx();
                    for (int i = 0; i < 8; i++) fx.tiles.Add(new Kit.Tile(kit, "Knight way"));
                    fx.tiles.Add(new Kit.Tile(kit, "Knight landing badge"));
                    // R138: the horseshoe badge and its arrow in place of the turning horseshoe ring.
                    var arrow = meshArrow != null ? new Arrow(this) : null;
                    if (arrow != null) fx.end = () => arrow.Destroy();
                    var squares = new List<Vector3>();
                    var dirs = new List<Vector3>();
                    fx.step = (x, d) =>
                    {
                        if (k == null || !kit.Held(x)) return false;
                        Vector3 spot = k.KnightLandingSpot;
                        Vector3 floorSpot = Kit.FloorUnder(spot, 1.5f) + Vector3.up * 0.016f;
                        squares.Clear();
                        dirs.Clear();
                        if (leapPaths.TryGetValue(k, out var path))
                        {
                            Vector3 a = path.start, b = path.corner ?? spot;
                            AddWay(a, b, squares, dirs);
                            if (path.corner.HasValue) AddWay(path.corner.Value, spot, squares, dirs);
                        }
                        for (int i = 0; i < 8; i++)
                        {
                            var t = x.tiles[i];
                            if (i >= squares.Count) { t.Hide(); continue; }
                            // R138: the square it lands in is the badge's (a see-through square drawn over it hid half of it).
                            Vector3 off = squares[i] - floorSpot;
                            off.y = 0f;
                            if (off.magnitude < Square * 0.55f) { t.Hide(); continue; }
                            t.Floor(squares[i], dirs[i], Vector2.one * Square);
                            t.shape = 0f;
                            t.round = 0.07f;
                            t.fade = Mathf.Clamp01(x.age / (6f * Frame));
                            kit.Warn(t, k, 0.38f);
                            if (board)
                            {
                                t.chevron = Kit.A(BNavy, 0.7f);
                                t.chevronPhase = 0f;
                                t.chevronStep = 100f;
                                t.Apply();
                            }
                        }
                        // R138 (승규 님: "착지 할 때 지금 있는 원 모양 말고 마지막 사진 같은 로고로"): the horseshoe badge on the
                        // spot, toe along its way, with the arrow over it (it was a horseshoe ring turning 90°/s).
                        Vector3 way = dirs.Count > 0 ? dirs[dirs.Count - 1] : Kit.FlatDir(k.Hips.linearVelocity, k.Facing);
                        LandingBadge(x, x.tiles[8], arrow, k, floorSpot, way);
                        return true;
                    };
                    return fx;
                });
            }
        }

        static void AddWay(Vector3 a, Vector3 b, List<Vector3> squares, List<Vector3> dirs)
        {
            Vector3 flat = b - a;
            flat.y = 0f;
            float dist = flat.magnitude;
            if (dist < 0.4f) return;
            Vector3 dir = flat / dist;
            int n = Mathf.Clamp(Mathf.RoundToInt(dist / Square), 1, 5);
            float step = dist / n;
            for (int i = 0; i < n && squares.Count < 8; i++)
            {
                squares.Add(Kit.FloorUnder(a + dir * (step * (i + 0.5f)), 2f) + Vector3.up * 0.016f);
                dirs.Add(dir);
            }
        }

        /// <summary>How far through its flight a knight is now, 0 at take-off (or its last kick off the air) to 1 at
        /// touchdown, from its height over the landing spot and its speed.</summary>
        float KnightFlightShare(RagdollPawn k)
        {
            if (!leapPaths.TryGetValue(k, out var path)) return 0f;
            float g = Mathf.Max(0.01f, -Physics.gravity.y);
            float h = k.Hips.position.y - (k.KnightLandingSpot.y + k.standHeight);
            float vy = k.Hips.linearVelocity.y;
            float disc = vy * vy + 2f * g * Mathf.Max(0f, h);
            float left = Mathf.Max(0f, (vy + Mathf.Sqrt(Mathf.Max(0f, disc))) / g);
            float gone = Mathf.Max(0f, Clock - path.launchedAt);
            return Mathf.Clamp01(gone / Mathf.Max(0.05f, gone + left));
        }

        /// <summary>The knight's crouch before a leap off the floor (A): five small wind puffs gathering at its feet. R138: dust
        /// drawn in round its hooves instead (real dust; R139 five billows, not eight).</summary>
        void KnightGather(RagdollPawn k)
        {
            float windup = Params != null ? Params.knightWindup : 0.15f;
            if (kit.NoPuffs) return;   // 10-25: no dust at all
            if (kit.realDust != null && k != null)
            {
                SkillDust.Tint(kit.dust.light, kit.dust.deep, out var lit, out var shade);
                Vector3 c = k.FeetPoint;
                float floor = SkillDust.FloorY(c + Vector3.up * 0.3f);
                for (int i = 0; i < 5; i++)
                {
                    float ang = i * 72f * Mathf.Deg2Rad + Random.Range(-0.3f, 0.3f);
                    var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    kit.realDust.Emit(c + dir * Random.Range(0.55f, 0.75f) + Vector3.up * 0.08f, -dir * Random.Range(2.4f, 3.2f) + Vector3.up * 0.3f,
                                      Random.Range(0.1f, 0.15f), Mathf.Max(0.4f, windup + 0.3f), lit, shade, 1.2f, 0.15f, 0.55f, floor);
                }
                return;
            }
            for (int i = 0; i < 5; i++)
            {
                float ang = i * 72f * Mathf.Deg2Rad + 0.4f;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var puff = new Kit.Puff(kit, kit.knight) { ink = 0.32f };
                var f = kit.Run(Mathf.Max(0.12f, windup + 0.04f), (fx, d) =>
                {
                    float t = fx.age / fx.life;
                    Vector3 c = k != null ? k.FeetPoint : Vector3.zero;
                    puff.t.position = c + dir * Mathf.Lerp(0.7f, 0.15f, t) + Vector3.up * 0.1f;
                    puff.t.localScale = Vector3.one * 0.14f * Kit.Pop(fx.age) * (1f - 0.4f * t);
                    puff.dissolve = Mathf.Clamp01((t - 0.75f) / 0.25f);
                    puff.Apply(fx.age);
                    return true;
                });
                f.puffs.Add(puff);
            }
        }

        // ---------------------------------------------------------------- pawn

        /// <summary>
        /// A step of the pawn's first two steps (A): the square it pushes off from stamps in its side's colour (teal for
        /// mine and my side's, red for theirs) with a cream band inside and an ink edge, popping 0.85 → 1.08 (1.12 the
        /// second time) → 1 and fading over 45 frames, so the two squares of its opening move stay behind it; two dust
        /// puffs kick back off the heel. R141 (승규 님 10-14: "f 키를 누르면 뒤에(엉덩이 부분)에 임펙트가 나가는데 다른 임펙트로
        /// 해줘(공기 임팩트)"): the air it shoves back from behind its hips (<see cref="AirKick"/>) in place of the three
        /// speed wedges drawn there.
        /// </summary>
        void PawnStepStamp(SkillFxEvent e)
        {
            var pawn = e.by;
            if (pawn == null) return;
            var p = kit.SideColors(pawn);
            Vector3 dir = Kit.FlatDir(e.dir, pawn.Facing);
            Vector3 at = Kit.FloorUnder(e.at, 0.6f) + Vector3.up * 0.015f;
            float over = e.count >= 2 ? 1.12f : 1.08f;
            var t = new Kit.Tile(kit, "Pawn stamp");
            var f = kit.Run(51f * Frame, (fx, d) =>
            {
                float a = fx.age / Frame;
                t.Floor(at, dir, Vector2.one * (Square * Kit.Pop(fx.age, over, 0.85f)));
                t.shape = 0f;
                t.round = 0.07f;
                t.fill = Kit.A(p.main, 0.7f);
                t.core = Kit.A(kit.dust.light, 0.95f);
                t.coreWidth = 0.06f;
                t.ink = Kit.A(p.ink, 1f);
                t.inkWidth = 0.05f;
                t.rim = Color.clear;
                t.stripe = Color.clear;
                t.fade = a < 6f ? 1f : 1f - (a - 6f) / 45f;
                if (board)
                {
                    // The board look: an orange pad on its block with an arrow forward (the opening's two squares).
                    t.fill = Kit.A(BOrange.main, 0.96f);
                    t.bevel = Kit.A(BOrange.deep, 0.85f);
                    t.core = Color.clear;
                    t.ink = Kit.A(BNavy, 1f);
                    t.inkWidth = 0.04f;
                    kit.BoardSide(t, pawn);
                    t.chevron = Kit.A(BNavy, 0.8f);
                    t.chevronPhase = 0f;
                    t.chevronStep = 100f;
                    t.chevronSize = 0.55f;
                    t.chevronWidth = 0.13f;
                }
                t.Apply();
                return true;
            });
            f.tiles.Add(t);
            AirKick(pawn.Hips.position, dir, e.count >= 2 ? 0.8f : 0.65f, 0.12f);
            Vector3 heel = at - dir * 0.25f;
            if (kit.NoPuffs) return;   // 10-25: no dust at all
            if (kit.realDust != null)
            {
                // R138: real dust kicked back off the heel (no cartoon puffs).
                HoofDust(heel, -dir, 0.6f);
                return;
            }
            for (int i = 0; i < 2; i++)
            {
                var puff = new Kit.Puff(kit, kit.dust) { ink = 0.3f };
                Vector3 kick = (-dir + Vector3.Cross(Vector3.up, dir) * (i == 0 ? -0.5f : 0.5f)).normalized;
                var pf = kit.Run(20f * Frame, (fx, d) =>
                {
                    float k = fx.age / fx.life;
                    puff.t.position = heel + kick * (0.35f * Kit.EaseOut(k * 1.5f)) + Vector3.up * (0.12f + 0.3f * k);
                    puff.t.localScale = Vector3.one * 0.18f * Kit.Pop(fx.age, 1.2f);
                    puff.dissolve = Mathf.Clamp01((k - 0.3f) / 0.7f);
                    puff.Apply(fx.age);
                    return true;
                });
                pf.puffs.Add(puff);
            }
        }

        /// <summary>
        /// The air a piece shoves back as it springs off (R141, <see cref="SkillAir"/>): a burst of bent air thrown from just
        /// behind <paramref name="hips"/> back against <paramref name="dir"/> (and <paramref name="down"/> toward the floor), with
        /// white vapour torn out of it. In place of the lines drawn out of its back (the pawn's speed wedges, the knight's
        /// force lines).
        /// </summary>
        void AirKick(Vector3 hips, Vector3 dir, float strength, float down)
        {
            if (air == null) return;
            Vector3 way = Kit.FlatDir(dir, Vector3.forward);
            air.Kick(hips - way * 0.26f - Vector3.up * 0.04f, (-way + Vector3.down * down).normalized, strength, kit != null && !kit.NoPuffs ? kit.realDust : null);
        }

        /// <summary>The pawn's hit (A): straight on, a shove (3-frame stop, three drops); on the diagonal, a knockdown (4
        /// frames, the dust as it lands). R138 (승규 님: "적 맞출 때 나오는 반원 이펙트인데 빼주고"): no C half ring.</summary>
        void PawnHit(SkillFxEvent e)
        {
            var p = kit.SideColors(e.by);
            Vector3 push = Kit.FlatDir(e.dir, Vector3.forward);
            Vector3 at = e.target != null ? e.target.bodies[(int)BodyId.Chest].position : e.at;
            bool diagonal = e.count == 1;
            InkHit(e.target, at, push, p, diagonal ? 4 : 3, diagonal ? 0.08f : 0.04f, diagonal ? 6 : 4);
            if (diagonal) kit.Down(e.target, p, e.at, Square);
        }

        /// <summary>Helping a fallen friend up (A): its square becomes a raised tile (teal top, cream bevel, ink) rising
        /// 0.15 m and sinking back, with three teal puffs from its corners; no hoop, no glow round the friend.</summary>
        void PawnHelp(SkillFxEvent e)
        {
            var ally = e.target;
            var p = kit.SideColors(e.by);
            Vector3 at = Kit.FloorUnder(ally != null ? ally.Hips.position : e.at, 0.8f);
            Vector3 along = Kit.FlatDir(e.dir, Vector3.forward);
            // The top catches the light: a light tint of the side's colour (its sides the colour itself).
            var colors = new Kit.Palette { main = p.main, light = Color.Lerp(p.main, Color.white, 0.45f), deep = p.deep, ink = p.ink };
            if (board) colors = BOrange;
            var tile = new Kit.Prop(kit, kit.RoundBox(new Vector3(Square, 0.12f, Square), 0.04f), colors, "Help tile") { inkWidth = 0.02f };
            if (board)
            {
                tile.twoTone = true;
                tile.sideColor = kit.SideOf(e.by) == Kit.Side.Enemy ? kit.enemy.deep : BNavy;
                tile.Apply();
            }
            var f = kit.Run(0.55f, (fx, d) =>
            {
                float a = fx.age / Frame;
                float rise = a < 10f ? Kit.EaseOutBack(a / 10f) : a < 15f ? 1f : 1f - Mathf.Clamp01((a - 15f) / 8f);
                tile.t.SetPositionAndRotation(at + Vector3.up * (0.15f * rise - 0.055f), Quaternion.LookRotation(along));
                tile.Show(a < 24f);
                return true;
            });
            f.props.Add(tile);
            Vector3 side = Vector3.Cross(Vector3.up, along);
            foreach (var corner in new[] { along + side, -along + side, -side + along })
                kit.PuffBurst(at + corner * (Square * 0.42f) + Vector3.up * 0.12f, p, 1, 0.12f, 0.15f, 0.4f, 0.2f);
        }
    }
}
