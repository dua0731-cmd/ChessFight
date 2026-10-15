using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R129 (승규 님 10-13) for the Sword Fight F skills:
    /// - The knight's fork dive, new ("아예 다른 연출과 액션으로 해줘 (포크 강하라는 이름에 맞게)"): the square it will come
    ///   down on and the eight squares a knight attacks from there (gold where an enemy stands), its flight drawn ahead while
    ///   it aims; it springs up in a burst of dust, flashes at the top while the L from the landing square to each of those
    ///   squares is drawn on the floor, dives streaked with afterimages, and lands in a heavy stamp: every L runs out along
    ///   the floor at once and bursts on its square (gold where it struck someone).
    /// - The queen's line past the floor's edge ("거리 조절을 실패하면 장 밖을 떨어져 나갈 수 있게 … 미리보기 수정"): the
    ///   squares she would run over the void on hang there in red with sliding stripes, her stop ring with them.
    /// - The bishop's aim and windup ("미리보기에서 검은색 연기가 작게 피어나는 모습"): small curls of black smoke rising off
    ///   its X and its ring, thicker as the windup fills (R132: the fire's particles make the smoke now,
    ///   SwordFightSkillFx.Bishop.cs).
    /// </summary>
    public partial class SwordFightSkillFx
    {
        /// <summary>Over the void: the queen's squares past the floor's edge.</summary>
        static readonly Kit.Palette Danger = new Kit.Palette("#C9483E", "#F4BDB2", "#842A26", "#24242F");   // R130: the softer red

        // ---------------------------------------------------------------- queen: over the edge

        /// <summary>Her way as aimed: the squares on the floor (as before), and those past its edge hanging red over the void.
        /// R130 (승규 님: "퀸 꼬치 배기 끝에 있는 원형 지워줘"): no ring where she stops any more, on the floor or over it.</summary>
        void QueenLane(SwordFightSkills s, SwordFightSkillParams S, Warning w, Vector3 dir, float reach, float alpha, bool aim)
        {
            float run = Mathf.Max(1f, reach + S.queenHitAhead);
            float edge = SwordFightSkills.FloorDistance(s.Origin, dir, run + 0.5f);
            float onFloor = Mathf.Min(run, edge);
            if (onFloor > 0.4f) w.marks.AddRange(Lane(s.Origin, dir, 0.3f, onFloor, S.queenWidth));
            if (run <= edge + 0.1f) return;
            var d = new Warning { k = w.k, alpha = alpha, quiet = aim, fill = Danger, danger = true };
            d.marks.AddRange(AirLane(s.Origin, dir, Mathf.Max(0.3f, edge), run, S.queenWidth));
            Warn(s, aim ? "queen aim fall" : "queen line fall", d);
        }

        /// <summary>Squares along a line at the floor's height where it starts (over the void: no floor to put them on).</summary>
        static List<Mark> AirLane(Vector3 from, Vector3 dir, float start, float length, float width)
        {
            var list = new List<Mark>();
            int n = Mathf.Max(1, Mathf.CeilToInt((length - start) / 1.5f - 0.15f));
            float each = (length - start) / n;
            for (int i = 0; i < n; i++)
                list.Add(Square(from + dir * (start + each * (i + 0.5f)), dir, width, each - 0.06f));
            return list;
        }

        // ---------------------------------------------------------------- knight: the fork dive

        void KnightMarks(SwordFightSkills s, SwordFightSkillParams S, float alpha)
        {
            bool aim = s.Stage == SfStage.Aim, air = s.Stage == SfStage.Active;
            if (!aim && !air) return;
            Vector3 p = aim ? s.AimPoint : s.Point, dir = aim ? s.AimDir : s.Dir;
            float k = aim ? 0.3f : Kit.Charge(s.StageTime, S.knightRise + S.knightHover);
            float cell = S.knightCell;
            // The square it comes down on.
            var land = new Warning { k = k, alpha = alpha, quiet = aim, fill = kit.knight };
            land.marks.Add(Square(Kit.FloorUnder(p, 1.5f), dir, cell * 0.9f, cell * 0.9f));
            Warn(s, aim ? "knight land aim" : "knight land", land);
            // The eight squares it forks from there: silver, gold where an enemy stands (it will be struck). R133 (승규 님
            // 10-14: "각 칸마다 따로 차징이 돼 … 같이 차징 되게"): all of them charge together, a foe's no longer ahead.
            for (int i = 0; i < SwordFightSkills.KnightMoves.Length; i++)
            {
                if (!s.KnightSquareOn[i]) continue;
                bool foe = EnemyOn(s, s.KnightSquares[i], S.knightSquare + 0.15f);
                var w = new Warning { k = k, alpha = alpha * (foe ? 1f : 0.8f), quiet = aim, fill = foe ? kit.queen : kit.knight };
                w.marks.Add(Square(s.KnightSquares[i], dir, cell * 0.78f, cell * 0.78f));
                Warn(s, (aim ? "knight fork aim " : "knight fork ") + i, w);
            }
            if (aim) KnightPath(s, S);
            else KnightGuides(s, S);
        }

        bool EnemyOn(SwordFightSkills s, Vector3 at, float radius)
        {
            foreach (var o in SwordFightSkills.All)
            {
                if (o == null || o == s || o.Pawn == null || o.Fighter == null || !o.Fighter.Alive || o.Pawn.Team == s.Pawn.Team) continue;
                Vector3 d = o.Pawn.Hips.position - at;
                d.y = 0f;
                if (d.magnitude <= radius) return true;
            }
            return false;
        }

        /// <summary>Its flight as it will fly it, drawn faint for its owner while it aims: up, the beat at the top, down.</summary>
        void KnightPath(SwordFightSkills s, SwordFightSkillParams S)
        {
            var owner = s;
            kit.Keep(s, "knight path", () =>
            {
                var fx = new Kit.Fx();
                fx.strips.Add(new Kit.Strip(kit, kit.knight) { core = Color.white, coreShare = 0.3f });
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null || !kit.Held(x) || owner.Stage != SfStage.Aim) return false;
                    var pts = new List<Vector3>();
                    Vector3 from = owner.Pawn.Hips.position, to = owner.AimPoint + Vector3.up * (owner.Pawn.standHeight + 0.03f);
                    float total = owner.KnightTime;
                    for (int i = 0; i <= 24; i++)
                        pts.Add(SwordFightSkills.KnightFlight(S, from, to, owner.AimDir, total * i / 24f, out _));
                    x.strips[0].Build(pts, j => 0.06f, kit.Eye);
                    x.strips[0].opacity = 0.7f;
                    x.strips[0].Apply();
                    return true;
                };
                return fx;
            });
        }

        /// <summary>The L from a square to the square of a knight move: along its long leg first, then the short one.</summary>
        static Vector3[] KnightL(Vector3 from, Vector3 dir, float cell, Vector2Int move, Vector3 to)
        {
            Vector3 along = Kit.FlatDir(dir, Vector3.forward), right = Vector3.Cross(Vector3.up, along);
            Vector3 leg = Mathf.Abs(move.y) == 2 ? along * (move.y * cell) : right * (move.x * cell);
            Vector3 corner = from + leg;
            corner.y = Mathf.Lerp(from.y, to.y, 0.5f);
            return new[] { from, Vector3.Lerp(from, corner, 0.5f), corner, Vector3.Lerp(corner, to, 0.5f), to };
        }

        /// <summary>At the top of its flight: the L from the landing square to each square it forks, drawn out on the floor
        /// faint as it hangs there (gold to a square with an enemy on it), gone when it lands.</summary>
        void KnightGuides(SwordFightSkills s, SwordFightSkillParams S)
        {
            var owner = s;
            kit.Keep(s, "knight guides", () =>
            {
                var fx = new Kit.Fx();
                for (int i = 0; i < SwordFightSkills.KnightMoves.Length; i++) fx.strips.Add(new Kit.Strip(kit, kit.knight) { core = Color.white, coreShare = 0.35f, inkShare = 0.25f });
                float released = -1f;
                fx.step = (x, d) =>
                {
                    if (owner == null || owner.Pawn == null) return false;
                    if (!kit.Held(x) && released < 0f) released = x.age;
                    if (released >= 0f) return false;   // it has landed: the impact draws them now
                    float show = Mathf.Clamp01((owner.StageTime - S.knightRise) / Mathf.Max(0.05f, S.knightHover));
                    Vector3 c = Kit.FloorUnder(owner.Point, 1.5f) + Vector3.up * 0.03f;
                    for (int i = 0; i < x.strips.Count; i++)
                    {
                        var strip = x.strips[i];
                        if (show <= 0f || !owner.KnightSquareOn[i]) { strip.Hide(); continue; }
                        bool foe = EnemyOn(owner, owner.KnightSquares[i], S.knightSquare + 0.15f);
                        strip.colors = foe ? kit.queen : kit.knight;
                        var pts = KnightL(c, owner.Dir, S.knightCell, SwordFightSkills.KnightMoves[i], owner.KnightSquares[i] + Vector3.up * 0.03f);
                        strip.Build(pts, j => foe ? 0.13f : 0.09f, kit.Eye, true);
                        strip.head = Kit.EaseOut(show);
                        strip.opacity = foe ? 0.95f : 0.6f;
                        strip.Apply();
                    }
                    return true;
                };
                return fx;
            });
        }

        /// <summary>The spring: a burst of dust and a ring where it pushes off, its afterimage; then a silver ribbon after it
        /// all the way, a flash at the top of its flight and afterimages every frame of its dive.</summary>
        void KnightLeap(SfFxEvent e)
        {
            var s = e.by;
            var pawn = s.Pawn;
            var S = this.S;
            Vector3 at = Kit.FloorUnder(e.at + Vector3.up * 0.5f, 1f);
            kit.FlashRing(at, 0.7f, kit.knight);
            kit.DustRing(at + Vector3.up * 0.03f, 6, 0.8f);
            FloorShock(at, 0.25f, 1.2f, kit.knight, 6, 12, 0.22f);
            kit.PuffBurst(at + Vector3.up * 0.1f, kit.dust, 5, 0.5f, 0.18f, 0.45f, 0.6f);
            kit.AddSquash(pawn, Kit.SquashKind.Spring);
            Afterimage(pawn, kit.knight, 1.3f, 0.22f, 0.65f, true);
            Punch(pawn, null, 6f, 14);
            float rise = S != null ? S.knightRise : 0.3f, hover = S != null ? S.knightHover : 0.14f;
            var trail = new Kit.Strip(kit, kit.knight) { core = Color.white, coreShare = 0.32f, inkShare = 0.22f };
            var pts = new List<Vector3>();
            bool top = false, dive = false;
            float nextGhost = 0f;
            var f = kit.Run(1.6f, (fx, d) =>
            {
                bool flying = s != null && pawn != null && s.Stage == SfStage.Active && s.Piece == PieceKind.Knight;
                if (flying)
                {
                    float t = s.StageTime;
                    if (!top && t >= rise)
                    {
                        top = true;
                        // The beat at the top: a flash round it and its afterimage, a small kick of the view.
                        Vector3 chest = pawn.bodies[(int)BodyId.Chest].position;
                        kit.ImpactRing(chest, (kit.Eye - chest).normalized, kit.knight, 0.3f, 1.15f);
                        Afterimage(pawn, kit.knight, 1.5f, 0.26f, 0.6f, true);
                        Tint(pawn, null, kit.knight.main, 0.26f, 14);
                        Punch(pawn, null, 4f, 10);
                    }
                    if (!dive && t >= rise + hover)
                    {
                        dive = true;
                        Punch(pawn, null, 8f, 12);
                        Shake(pawn, null, 0.05f, 4);
                        DiveLine(pawn, s.Point + Vector3.up * 0.05f);
                    }
                    if (dive && fx.age >= nextGhost)
                    {
                        Afterimage(pawn, kit.knight, 1f, 0.16f, 0.5f);
                        nextGhost = fx.age + F;
                    }
                    pts.Add(pawn.Hips.position);
                }
                else if (pts.Count > 0) pts.RemoveAt(0);
                while (pts.Count > 18) pts.RemoveAt(0);
                if (pts.Count < 2) { trail.Hide(); return flying || fx.age < 0.1f; }
                trail.Build(pts, i => (dive ? 0.44f : 0.3f) * i / Mathf.Max(1f, pts.Count - 1f), kit.Eye);
                trail.Apply();
                return true;
            });
            f.strips.Add(trail);
        }

        /// <summary>The dive's line: from where it tips over down to its square, white then silver, gone in ten frames.</summary>
        void DiveLine(RagdollPawn pawn, Vector3 to)
        {
            Vector3 from = pawn.Hips.position;
            var line = new List<Vector3> { from, Vector3.Lerp(from, to, 0.5f), to };
            var strip = new Kit.Strip(kit, kit.knight) { core = Color.white, coreShare = 0.45f, inkShare = 0.2f };
            var f = kit.Run(10f * F, (fx, d) =>
            {
                float a = fx.age / F;
                strip.Build(line, i => 0.16f, kit.Eye);
                strip.head = Kit.EaseOut(a / 3f);
                strip.tail = a < 4f ? 0f : Kit.EaseIn((a - 4f) / 6f);
                strip.core = a < 2f ? Color.white : kit.knight.light;
                strip.Apply();
                return true;
            });
            f.strips.Add(strip);
        }

        /// <summary>The landing: a heavy stamp (a flash, the floor shocked, dust and chips, the view kicked), then the fork:
        /// from the landing square an L runs out along the floor to each square a knight attacks, all at once, and each
        /// square bursts as it gets there (gold where an enemy stood).</summary>
        void KnightLand(SfFxEvent e)
        {
            var s = e.by;
            var S = this.S;
            Vector3 c = Kit.FloorUnder(e.at + Vector3.up * 0.5f, 1f);
            float cell = e.size > 0.1f ? e.size : 1.25f;
            Vector3 dir = s.Dir;
            kit.FlashRing(c, 0.95f, kit.knight);
            FloorShock(c, 0.3f, cell * 1.35f, kit.knight, 7, 16, 0.3f);
            kit.DustRing(c + Vector3.up * 0.03f, 8, 1.0f);
            kit.Drops(c + Vector3.up * 0.15f, Vector3.up, Kit.Stone, 6, 6.5f);
            kit.AddSquash(s.Pawn, Kit.SquashKind.Down);
            Afterimage(s.Pawn, kit.knight, 1.6f, 0.3f, 0.7f, true);
            HitStop(6);
            Shake(s.Pawn, null, 0.2f, 10);
            Punch(s.Pawn, null, 8f, 18);
            Tint(s.Pawn, null, kit.knight.main, 0.32f, 20);
            for (int i = 0; i < SwordFightSkills.KnightMoves.Length; i++)
            {
                if (!s.KnightSquareOn[i]) continue;
                Vector3 sq = s.KnightSquares[i];
                bool foe = S != null && EnemyOn(s, sq, S.knightSquare + 0.15f);
                var pal = foe ? kit.queen : kit.knight;
                var pts = KnightL(c + Vector3.up * 0.036f, dir, cell, SwordFightSkills.KnightMoves[i], sq + Vector3.up * 0.036f);
                var prong = new Kit.Strip(kit, pal) { core = Color.white, coreShare = 0.45f, inkShare = 0.2f };
                var ring = new Kit.Tile(kit, "Fork square");
                bool burst = false;
                var f = kit.Run(18f * F, (fx, d) =>
                {
                    float a = fx.age / F;
                    prong.Build(pts, j => foe ? 0.26f : 0.2f, kit.Eye, true);
                    prong.head = Kit.EaseOut(a / 4f);
                    prong.tail = a < 9f ? 0f : Kit.EaseIn((a - 9f) / 8f);
                    prong.core = a < 2f ? Color.white : pal.light;
                    prong.Apply();
                    if (a >= 4f)
                    {
                        if (!burst)
                        {
                            burst = true;
                            kit.DustRing(sq + Vector3.up * 0.03f, foe ? 5 : 3, 0.55f, null, 0.24f, 0.4f);
                            if (foe)
                            {
                                kit.ImpactRing(sq + Vector3.up * 0.4f, Vector3.up, pal, 0.25f, 0.85f, false);
                                kit.Drops(sq + Vector3.up * 0.2f, Vector3.up, pal, 5, 6f);
                            }
                        }
                        float b = a - 4f;
                        float r = cell * 0.42f * Kit.Pop(b * F, foe ? 1.25f : 1.12f, 0.3f);
                        ring.Floor(sq + Vector3.up * 0.03f, dir, Vector2.one * (2f * r));
                        ring.shape = 1f;
                        ring.inner = Mathf.Clamp01(1f - 0.22f / Mathf.Max(0.25f, r));
                        ring.fill = Kit.A(pal.main, 1f);
                        ring.core = Kit.A(Color.white, 1f);
                        ring.coreWidth = 0.04f;
                        ring.ink = Kit.A(pal.ink, 1f);
                        ring.inkWidth = 0.035f;
                        ring.rim = Color.clear;
                        ring.stripe = Color.clear;
                        ring.flash = b < 2f ? 1f : 0f;
                        ring.arc = b < 6f ? 1f : Mathf.Max(0f, 1f - (b - 6f) / 8f);
                        ring.fade = 1f;
                        ring.Apply();
                    }
                    return true;
                });
                f.strips.Add(prong);
                f.tiles.Add(ring);
            }
        }
    }
}
