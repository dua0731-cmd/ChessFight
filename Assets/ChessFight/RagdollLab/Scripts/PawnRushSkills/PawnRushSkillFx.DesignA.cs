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
        const float Frame = Kit.F;
        const float Square = 1.5f;

        RagdollPawn Viewer => game != null && game.players != null && game.players.Length > 0 ? game.players[0].pawn : null;
        Vector3 EyePoint => ViewCamera != null ? ViewCamera.transform.position : Vector3.up * 5f;

        void BuildDesignA()
        {
            kit = new Kit(root, () => Viewer, () => EyePoint)
            {
                pawnMesh = chessMeshes != null && chessMeshes.Length > 0 ? chessMeshes[0] : null,
            };
            RagdollPawn.SkillMarksHidden = true;
        }

        void DestroyDesignA()
        {
            kit?.Destroy();
            kit = null;
            RagdollPawn.SkillMarksHidden = false;
        }

        /// <summary>What each piece's skill shows while its state lasts: the warnings, the bishop's throw, the haste.</summary>
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
                    case PieceKind.Queen: QueenWarning(pawn); break;
                    case PieceKind.Rook: RookWarning(pawn); break;
                    case PieceKind.Bishop: BishopAimTiles(pawn); BishopThrowProp(pawn); break;
                    case PieceKind.Knight: KnightMarks(pawn); break;
                }
                if (pawn.HasteLeft > 0f) HeelRibbons(pawn);
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
                    var p = Kit.Queen;
                    // The inner disc and the band round it.
                    var disc = x.tiles[0];
                    disc.Floor(c, Vector3.forward, Vector2.one * (2f * inner));
                    disc.shape = 2f;
                    disc.fade = fade;
                    WarnFill(disc, q, 0.25f * Mathf.Lerp(0.5f, 1f, k), k, 0f);
                    disc.inkWidth = 0f;
                    disc.rim = Color.clear;
                    disc.Apply();
                    var band = x.tiles[1];
                    band.Floor(c - Vector3.up * 0.002f, Vector3.forward, Vector2.one * (2f * outer));
                    band.shape = 1f;
                    band.inner = inner / outer;
                    band.fade = fade;
                    WarnFill(band, q, 0.15f * Mathf.Lerp(0.5f, 1f, k), k, 0f);
                    band.inkWidth = 0f;
                    band.rim = Color.clear;
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

        /// <summary>
        /// The queen's crown ring (A): riding the rim of her ring of fire puffs as it races out to her reach in eight
        /// frames, a gold ring with eight round pearls at the queen's eight ways (a crown seen from above); the knockdown
        /// zone's edge gets a thick bright gold ring (0.35 m) and her reach a thin one (0.1 m).
        /// </summary>
        void QueenCrown(Vector3 c, float inner, float outer)
        {
            var p = Kit.Queen;
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
                var pearl = new Kit.Prop(kit, kit.meshBall, p, "Queen pearl") { inkWidth = 0.014f };
                pearl.Show(false);
                f.props.Add(pearl);
            }
        }

        /// <summary>The queen's flash as she springs open (A): a white ball round her for two frames.</summary>
        void WhiteBall(Vector3 at, float radius)
        {
            var ball = new Kit.Prop(kit, kit.meshPuff, Kit.Queen, "White flash") { inkWidth = 0.02f, flash = 1f };
            ball.Apply();
            var f = kit.Run(2f * Frame, (fx, d) =>
            {
                ball.t.position = at;
                ball.t.localScale = Vector3.one * (2f * radius);
                return true;
            });
            f.props.Add(ball);
            ball.t.position = at;
            ball.t.localScale = Vector3.one * (2f * radius);
        }

        // ---------------------------------------------------------------- rook

        /// <summary>
        /// The rook's warning (A): while it aims, the four squares of its charge light one by one (every six frames) along
        /// its line, the last one standing up against a wall if one is in the way (it stops there), with a rounded chevron
        /// sliding along them; locked, they snap full with a white inner band and a ring of square stone puffs kicks out
        /// round its base; its white edges blink in the last six frames; as it charges they flash white and are gone. In
        /// the air its line leaves the chest (an inked band) to a square where it would meet the floor.
        /// </summary>
        void RookWarning(RagdollPawn r)
        {
            if (r.SkillStage != SkillStage.Windup) return;
            var s = Params;
            float length = s != null ? s.rookSpeed * s.rookTime : 6f;
            bool locked = !r.SkillAiming;
            if (r.RookInAir || (!locked && !r.OnTheFloor))
            {
                RookAirLine(r, length, locked);
                return;
            }
            kit.Keep(r, "rook file", () =>
            {
                var fx = new Kit.Fx();
                for (int i = 0; i < 4; i++) fx.tiles.Add(new Kit.Tile(kit, "Rook warning"));
                for (int i = 0; i < 2; i++) fx.strips.Add(new Kit.Strip(kit, Kit.Rook));
                float released = -1f, lockedAt = -1f;
                Vector3 dir = Vector3.forward;
                var squares = new List<Vector3>();
                bool wall = false;
                Vector3 wallPoint = Vector3.zero, wallNormal = Vector3.back;
                fx.step = (x, d) =>
                {
                    if (r == null) return false;
                    bool held = kit.Held(x);
                    if (!held && released < 0f) released = x.age;
                    float a = released >= 0f ? (x.age - released) / Frame : -1f;
                    if (a >= 6f) return false;
                    bool isLocked = held && !r.SkillAiming;
                    if (isLocked && lockedAt < 0f)
                    {
                        lockedAt = x.age;
                        StoneRing(r.FeetPoint);
                    }
                    if (released < 0f && !isLocked)
                    {
                        // Aiming: the squares follow the aim.
                        dir = Kit.FlatDir(r.SkillDirection, r.Facing);
                        PlanFile(r.FeetPoint, dir, length, squares, out wall, out wallPoint, out wallNormal);
                    }
                    float lockTime = s != null ? s.rookLock : 0.3f;
                    float sinceLock = lockedAt >= 0f ? x.age - lockedAt : 0f;
                    for (int i = 0; i < x.tiles.Count; i++)
                    {
                        var t = x.tiles[i];
                        if (i >= squares.Count) { t.Hide(); continue; }
                        bool onWall = wall && i == squares.Count - 1;
                        if (onWall) t.Facing(wallPoint + wallNormal * 0.02f + Vector3.up * (Square * 0.5f), wallNormal, Vector2.one * Square);
                        else t.Floor(squares[i], dir, Vector2.one * Square);
                        float lit = Mathf.Clamp01((x.age - i * 6f * Frame) / (6f * Frame));
                        float strength = lockedAt < 0f ? Mathf.Lerp(0.2f, 0.55f, lit) : 1f;
                        float flash = a >= 0f && a < 2f ? 1f : 0f;
                        t.shape = 0f;
                        t.round = 0.07f;
                        t.fade = (released < 0f ? lit : 1f - Mathf.Clamp01((a - 2f) / 4f));
                        kit.Warn(t, r, strength, flash);
                        // Locked: a white band inside the rim; its white edges blink in the last six frames.
                        bool blink = lockedAt >= 0f && sinceLock >= lockTime - 6f * Frame && ((int)((sinceLock - (lockTime - 6f * Frame)) / (2f * Frame)) % 2 == 0);
                        t.core = lockedAt >= 0f ? Kit.A(Color.white, 0.9f) : Color.clear;
                        t.coreWidth = 0.05f;
                        if (blink) t.rim = Kit.A(Color.white, 1f);
                        t.Apply();
                    }
                    // The chevron sliding along the file, looping every 0.4 s.
                    float run = (released < 0f) ? (x.age % 0.4f) / 0.4f : 0f;
                    int n = Mathf.Max(1, wall ? squares.Count - 1 : squares.Count);
                    Vector3 from = r.FeetPoint + Vector3.up * 0.03f;
                    Vector3 tip = from + dir * Mathf.Lerp(Square * 0.5f, Square * (n - 0.3f), run) + Vector3.up * 0.005f;
                    Vector3 side = Vector3.Cross(Vector3.up, dir);
                    var pal = kit.PieceColors(r);
                    for (int i = 0; i < 2; i++)
                    {
                        var st = x.strips[i];
                        Vector3 back = tip - dir * 0.3f + side * (i == 0 ? -0.3f : 0.3f);
                        st.colors = pal;
                        st.core = pal.light;
                        st.Build(new List<Vector3> { back, tip }, k => 0.14f, kit.Eye, true);
                        st.taper = 0f;
                        st.fade = released < 0f ? Mathf.Clamp01(x.age / (6f * Frame)) * 0.9f : 0f;
                        st.Apply();
                    }
                    return true;
                };
                return fx;
            });
        }

        /// <summary>The four squares of a straight charge on the floor (the last one on a wall in the way, if any).</summary>
        static void PlanFile(Vector3 feet, Vector3 dir, float length, List<Vector3> squares, out bool wall, out Vector3 wallPoint, out Vector3 wallNormal)
        {
            squares.Clear();
            wall = false;
            wallPoint = Vector3.zero;
            wallNormal = -dir;
            float reach = length;
            var hits = Physics.RaycastAll(feet + Vector3.up * 0.5f, dir, length, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (RagdollPawn.ColliderOwner.ContainsKey(h.collider) || h.normal.y > 0.5f || h.distance >= best) continue;
                var rb = h.collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic) continue;
                best = h.distance;
                wall = true;
                wallPoint = new Vector3(h.point.x, feet.y, h.point.z);
                wallNormal = Kit.FlatDir(h.normal, -dir);
            }
            if (wall) reach = best;
            int n = Mathf.Clamp(Mathf.FloorToInt(reach / Square + 0.25f), 0, 4);
            for (int i = 0; i < n; i++) squares.Add(Kit.FloorUnder(feet + dir * (Square * (i + 0.5f)), 1.5f) + Vector3.up * 0.016f);
            if (wall) squares.Add(wallPoint);
        }

        /// <summary>The rook's line in the air (R80's 3D aim) in A: an inked band from the chest along the aim and a
        /// warning square where it would meet the floor.</summary>
        void RookAirLine(RagdollPawn r, float length, bool locked)
        {
            kit.Keep(r, "rook air line", () =>
            {
                var fx = new Kit.Fx();
                fx.strips.Add(new Kit.Strip(kit, Kit.Rook));
                fx.tiles.Add(new Kit.Tile(kit, "Rook air landing"));
                float released = -1f;
                fx.step = (x, d) =>
                {
                    if (r == null) return false;
                    if (!kit.Held(x) && released < 0f) released = x.age;
                    float a = released >= 0f ? (x.age - released) / Frame : -1f;
                    if (a >= 6f) return false;
                    Vector3 from = r.bodies[(int)BodyId.Chest].position, dir = r.SkillDirection.sqrMagnitude > 1e-4f ? r.SkillDirection.normalized : r.Facing;
                    var st = x.strips[0];
                    var pal = kit.PieceColors(r);
                    st.colors = pal;
                    st.core = Color.white;
                    st.Build(new List<Vector3> { from, from + dir * length }, k => locked ? 0.16f : 0.11f, kit.Eye);
                    st.taper = 0f;
                    st.opacity = locked ? 1f : 0.6f;
                    st.fade = released < 0f ? 1f : 1f - Mathf.Clamp01((a - 2f) / 4f);
                    st.Apply();
                    var t = x.tiles[0];
                    if (Physics.Raycast(from, dir, out var hit, length, ~0, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.5f && !RagdollPawn.ColliderOwner.ContainsKey(hit.collider))
                    {
                        t.Floor(hit.point + Vector3.up * 0.016f, Kit.FlatDir(dir, r.Facing), Vector2.one * Square);
                        t.shape = 0f;
                        t.round = 0.07f;
                        t.fade = released < 0f ? 1f : 1f - Mathf.Clamp01((a - 2f) / 4f);
                        kit.Warn(t, r, locked ? 1f : 0.5f, a >= 0f && a < 2f ? 1f : 0f);
                    }
                    else t.Hide();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>The rook digging in (A's lock): eight square stone puffs pushed out round its base and sinking away.</summary>
        void StoneRing(Vector3 feet)
        {
            var mesh = kit.RoundBox(Vector3.one, 0.22f);
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
                var p = new Kit.Prop(kit, mesh, Kit.Stone, "Stone puff") { inkWidth = 0.014f };
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
            var p = Kit.Rook;
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
                ring.core = Kit.A(p.light, 1f);
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
                    tooth.core = Kit.A(p.light, 1f);
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
            kit.PuffBurst(at + normal * 0.25f, Kit.Stone, 6, 0.6f, 0.26f, 0.5f, 0.2f);
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
                    var p = Kit.Bishop;
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
                    mitre.Apply();
                    var reach = x.tiles[13];
                    reach.Floor(b.FeetPoint + Vector3.up * 0.012f, Vector3.forward, Vector2.one * (2f * range));
                    reach.shape = 1f;
                    reach.inner = 1f - 0.05f / range;
                    reach.fill = Kit.A(Kit.SelfFill, 0.5f);
                    reach.ink = Kit.A(Kit.SelfInk, 0.35f);
                    reach.inkWidth = 0.012f;
                    reach.core = Color.clear;
                    reach.rim = Color.clear;
                    reach.stripe = Color.clear;
                    reach.fade = fade * 0.8f;
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
                var p = Kit.Bishop;
                var rod = kit.RoundBox(new Vector3(0.07f, 0.07f, 0.9f), 0.03f);
                for (int i = 0; i < 2; i++)
                {
                    var r = new Kit.Prop(kit, rod, p, "Bishop rod", pivot) { inkWidth = 0.012f };
                    r.t.localRotation = Quaternion.Euler(0f, i == 0 ? 45f : -45f, 0f);
                    fx.props.Add(r);
                }
                var capColors = new Kit.Palette { main = p.light, light = Color.white, deep = p.main, ink = p.ink };
                for (int i = 0; i < 4; i++)
                {
                    var cap = new Kit.Prop(kit, kit.meshBall, capColors, "Bishop cap", pivot) { inkWidth = 0.012f };
                    float ang = (45f + 90f * i) * Mathf.Deg2Rad;
                    cap.t.localPosition = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * 0.45f;
                    cap.t.localScale = Vector3.one * 0.13f;
                    fx.props.Add(cap);
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
                kit.PuffBurst(end + Vector3.up * 0.08f, Kit.Bishop, 2, 0.15f, 0.12f, 0.35f, 0.1f);
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
                for (int i = 0; i < 4; i++) fx.strips.Add(new Kit.Strip(kit, Kit.Bishop) { core = Color.white });
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
                        t.t.SetPositionAndRotation(at + Vector3.up * 0.004f, rotation * Quaternion.LookRotation(Vector3.down, Vector3.forward));
                        t.Size(Vector2.one * size * 1.0f);
                        t.shape = 0f;
                        t.round = 0.05f;
                        t.fill = Color.clear;
                        t.core = Color.clear;
                        t.rim = Kit.A(side == Kit.Side.Self ? Kit.SelfFill : side == Kit.Side.Ally ? Kit.TeamAlly.main : Kit.TeamEnemy.main, 0.9f);
                        t.rimWidth = 0.06f;
                        t.ink = Kit.A(side == Kit.Side.Self ? Kit.SelfInk : side == Kit.Side.Ally ? Kit.TeamAlly.ink : Kit.TeamEnemy.ink, 0.9f);
                        t.inkWidth = 0.025f;
                        t.stripe = side == Kit.Side.Enemy ? Kit.A(Kit.TeamEnemy.deep, 0.5f) : Color.clear;
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
                        st.colors = new Kit.Palette { main = Color.white, light = Color.white, deep = Kit.Bishop.main, ink = Kit.Bishop.ink };
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

        void SinkPegs(Vector3[] ends, float height)
        {
            if (kit == null) return;
            var mesh = kit.RoundBox(new Vector3(0.07f, 1f, 0.07f), 0.03f);
            var cream = new Kit.Palette { main = Kit.Hex("#EBE6D1"), light = Color.white, deep = Kit.Hex("#B8B09A"), ink = Kit.Bishop.ink };
            var f = kit.Run(8f * Frame, (fx, d) =>
            {
                float k = Mathf.Clamp01(fx.age / (8f * Frame));
                for (int i = 0; i < fx.props.Count; i++)
                {
                    var p = fx.props[i];
                    Vector3 foot = ends[i] - Vector3.up * height;
                    float h = (height + 0.06f) * (1f - k);
                    p.t.position = foot + Vector3.up * (h * 0.5f);
                    p.t.localScale = new Vector3(1f, Mathf.Max(0.001f, h), 1f);
                    p.Show(h > 0.005f);
                }
                return true;
            });
            for (int i = 0; i < 4; i++)
            {
                if (ends[i] == Vector3.zero) continue;
                var p = new Kit.Prop(kit, mesh, cream, "Sinking peg") { inkWidth = 0.01f };
                f.props.Add(p);
                kit.PuffBurst(ends[i] - Vector3.up * (height - 0.05f), Kit.Bishop, 2, 0.15f, 0.12f, 0.35f, 0.1f);
            }
        }

        // ---------------------------------------------------------------- knight

        /// <summary>Where a knight's leap went: from where it took off, through where it kicked off the air (an L).</summary>
        class LeapPath { public Vector3 start; public Vector3? corner; public float launchedAt; }
        readonly Dictionary<RagdollPawn, LeapPath> leapPaths = new Dictionary<RagdollPawn, LeapPath>();

        /// <summary>
        /// The knight's marks in the air (A): the squares of its way to where it comes down glow faintly in its sky blue
        /// (an L once it has kicked off the air), and the landing mark, a horseshoe (a thick ring open to one side: white
        /// core, sky, navy ink), turns on the spot (90°/s) and shrinks from 1.4 m to 1 m exactly at touchdown. The enemy it
        /// would come down on (a second F) gets a red ring at its feet and a pointer over its head, blinking while only
        /// found, steady once on the way.
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
                    fx.tiles.Add(new Kit.Tile(kit, "Horseshoe"));
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
                            t.Floor(squares[i], dirs[i], Vector2.one * Square);
                            t.shape = 0f;
                            t.round = 0.07f;
                            t.fade = Mathf.Clamp01(x.age / (6f * Frame));
                            kit.Warn(t, k, 0.38f);
                        }
                        // The horseshoe: 1.4 m across at take-off, 1 m at touchdown, turning 90°/s.
                        var shoe = x.tiles[8];
                        float r = Mathf.Lerp(0.7f, 0.5f, KnightFlightShare(k));
                        shoe.Floor(floorSpot + Vector3.up * 0.006f, Quaternion.Euler(0f, kit.Clock * 90f, 0f) * Vector3.forward, Vector2.one * (2f * r));
                        Kit.Horseshoe(shoe, Kit.Knight, 1f);
                        var side = kit.SideOf(k);
                        if (side == Kit.Side.Enemy)
                        {
                            shoe.ink = Kit.A(Kit.TeamEnemy.main, 1f);
                            shoe.stripe = Kit.A(Kit.TeamEnemy.deep, 0.5f);
                            shoe.stripePhase = kit.Clock * 0.5f;
                        }
                        shoe.fade = Mathf.Clamp01(x.age / (4f * Frame));
                        shoe.Apply();
                        return true;
                    };
                    return fx;
                });
            }
            var marked = k.KnightMarked;
            if (marked != null)
            {
                kit.Keep(k, "knight lock", () =>
                {
                    var fx = new Kit.Fx();
                    fx.tiles.Add(new Kit.Tile(kit, "Knight lock ring"));
                    fx.strips.Add(new Kit.Strip(kit, Kit.TeamEnemy));
                    fx.step = (x, d) =>
                    {
                        var m = k != null ? k.KnightMarked : null;
                        if (m == null || !kit.Held(x)) return false;
                        float blink = k.KnightHoming ? 1f : 0.55f + 0.45f * Mathf.Sin(kit.Clock * 14f);
                        var ring = x.tiles[0];
                        ring.Floor(m.FeetPoint + Vector3.up * 0.02f, Vector3.forward, Vector2.one * 1.0f * Kit.Pop(x.age, 1.15f));
                        ring.shape = 1f;
                        ring.inner = 0.72f;
                        ring.fill = Kit.A(Kit.TeamEnemy.main, blink);
                        ring.core = Kit.A(Color.white, blink);
                        ring.coreWidth = 0.03f;
                        ring.ink = Kit.A(Kit.TeamEnemy.ink, 1f);
                        ring.inkWidth = 0.03f;
                        ring.rim = Color.clear;
                        ring.stripe = Color.clear;
                        ring.fade = 1f;
                        ring.Apply();
                        var pointer = x.strips[0];
                        Vector3 over = m.bodies[(int)BodyId.Head].position + Vector3.up * 0.32f;
                        pointer.Build(new List<Vector3> { over, over + Vector3.up * 0.4f }, q => 0.22f, kit.Eye);
                        pointer.taper = 1f;
                        pointer.core = Kit.TeamEnemy.main;
                        pointer.coreShare = 0f;
                        pointer.fade = blink;
                        pointer.Apply();
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

        /// <summary>The knight's crouch before a leap off the floor (A): five small wind puffs gathering at its feet.</summary>
        void KnightGather(RagdollPawn k)
        {
            float windup = Params != null ? Params.knightWindup : 0.15f;
            for (int i = 0; i < 5; i++)
            {
                float ang = i * 72f * Mathf.Deg2Rad + 0.4f;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var puff = new Kit.Puff(kit, Kit.Knight) { ink = 0.32f };
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
        /// second time) → 1 and fading over 45 frames, so the two squares of its opening move stay behind it; three
        /// tapered speed wedges stream behind the hips for eight frames; two dust puffs kick back off the heel.
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
                t.core = Kit.A(Kit.Dust.light, 0.95f);
                t.coreWidth = 0.06f;
                t.ink = Kit.A(p.ink, 1f);
                t.inkWidth = 0.05f;
                t.rim = Color.clear;
                t.stripe = Color.clear;
                t.fade = a < 6f ? 1f : 1f - (a - 6f) / 45f;
                t.Apply();
                return true;
            });
            f.tiles.Add(t);
            SpeedWedges(pawn, p, 8f * Frame);
            Vector3 heel = at - dir * 0.25f;
            for (int i = 0; i < 2; i++)
            {
                var puff = new Kit.Puff(kit, Kit.Dust) { ink = 0.3f };
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

        /// <summary>Three tapered wedges (0.9 m, cream core, the colour, ink) streaming behind a dashing piece's hips.</summary>
        void SpeedWedges(RagdollPawn pawn, Kit.Palette p, float seconds)
        {
            var f = kit.Run(seconds + 6f * Frame, (fx, d) =>
            {
                if (pawn == null) return false;
                float fade = fx.age < seconds ? 1f : 1f - (fx.age - seconds) / (6f * Frame);
                Vector3 dir = Kit.FlatDir(pawn.Hips.linearVelocity, Kit.FlatDir(pawn.SkillDirection, pawn.Facing));
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                Vector3 hips = pawn.Hips.position;
                float[] off = { -0.2f, 0.05f, 0.22f }, high = { 0.05f, 0.2f, -0.04f }, len = { 0.9f, 0.7f, 0.8f };
                for (int i = 0; i < 3; i++)
                {
                    var s = fx.strips[i];
                    Vector3 head = hips - dir * 0.25f + side * off[i] + Vector3.up * high[i];
                    s.Build(new List<Vector3> { head - dir * len[i], head }, k => 0.12f, kit.Eye);
                    s.taper = 1f;
                    s.fade = fade;
                    s.Apply();
                }
                return true;
            });
            for (int i = 0; i < 3; i++) f.strips.Add(new Kit.Strip(kit, p) { core = Kit.Dust.light, coreShare = 0.35f, inkShare = 0.24f });
        }

        /// <summary>The pawn's hit (A): straight on, a shove (3-frame stop, a C half ring flung the way it goes, three
        /// drops); on the diagonal, a knockdown (4 frames, the ring tipped down along it, the captured square).</summary>
        void PawnHit(SkillFxEvent e)
        {
            var p = kit.SideColors(e.by);
            Vector3 push = Kit.FlatDir(e.dir, Vector3.forward);
            Vector3 at = e.target != null ? e.target.bodies[(int)BodyId.Chest].position : e.at;
            bool diagonal = e.count == 1;
            kit.HalfRing(at, push, p, diagonal ? 0.8f : 0.7f, 0.16f, diagonal ? 30f : 0f);
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
            var tile = new Kit.Prop(kit, kit.RoundBox(new Vector3(Square, 0.12f, Square), 0.04f), colors, "Help tile") { inkWidth = 0.02f };
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

        /// <summary>The haste after a help-up (A): a short ribbon of the side's colour trailing each foot.</summary>
        void HeelRibbons(RagdollPawn pawn)
        {
            kit.Keep(pawn, "heel ribbons", () =>
            {
                var fx = new Kit.Fx();
                var p = kit.SideColors(pawn);
                bool on = true;
                fx.step = (x, d) =>
                {
                    on = pawn != null && kit.Held(x);
                    return x.age < 0.25f || on;
                };
                foreach (var foot in new[] { BodyId.FootL, BodyId.FootR })
                {
                    var id = foot;
                    InkTrail(() => on && pawn != null && pawn.HasteLeft > 0f ? pawn.bodies[(int)id].position : (Vector3?)null, p, 0.2f, 0.12f, Kit.Dust.light);
                }
                return fx;
            });
        }
    }
}
