using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The set 4판 bundle 다's moments on every screen (RagdollPawn.SetFourC.cs; 11-26). Not seen in Unity by anyone yet.
    ///
    ///   폰      승격 5초 전 a gold crown over the head and a big digit counting down under... over it, popping each second;
    ///           promoted: a gold burst, the pawn drawn gold its seconds; fizzled: the crown slumps off in grey smoke
    ///   나이트  땅속 헤엄 the knight gone under the floor: two ears and a hump of dirt ploughing along, dirt flying; up: a
    ///           burst of dirt
    ///   비숍    핀! a big safety pin spinning along; stuck in the floor where it caught one, a rubber band from it to them
    ///   룩      더 루우욱! a red mark where it will land; down head first: a boom, then the rook drawn stuck upside down,
    ///           kicking; 넘어간다~! the rook drawn growing to its tower's height over its shadow line, falling flat along it
    ///           and lying there its seconds; 돼지 a wooden gate on its front swinging open, air streaking into it, the
    ///           swallowed gone from sight ("꿀꺽") until spat out ("퉤!"); 룩 리프트 a round stone tower with battlements
    ///           rising under it and sinking again
    ///   퀸      행차 a red carpet with gold edges unrolling along the floor from a rolling drum; one it reaches rolled up in
    ///           red; 착석! the throne's shadow and a fanfare, the queen gone up out of sight, down again on a gold throne;
    ///           퀸스 갬빗 five big pieces hanging upside down over their shadows, dropping one after another
    /// </summary>
    public partial class PawnRushSkillFx
    {
        static readonly Kit.Palette CmCarpet = new Kit.Palette("#D9322E", "#FF8A80", "#8E1A16", "#2E0606");
        static readonly Kit.Palette CmWood = new Kit.Palette("#A0703F", "#D9A774", "#6B4424", "#2A1A0C");
        static readonly Kit.Palette CmEarth = new Kit.Palette("#7A5232", "#A8784C", "#4E321C", "#1E140A");
        static readonly Kit.Palette CmKeep = new Kit.Palette("#BDB8AE", "#E6E2DA", "#8C877E", "#2E2C28");
        static readonly Kit.Palette CmKeepDark = new Kit.Palette("#8F8A80", "#B5B0A6", "#6A665E", "#24221F");

        /// <summary>A piece out of sight inside a rook's gate (its hindrance's label and stars not drawn meanwhile).</summary>
        bool S4cHidden(RagdollPawn p) => p != null && s4cSwallowed.Contains(p);

        readonly Dictionary<RagdollPawn, int> s4cPromo = new Dictionary<RagdollPawn, int>();       // 0 counting, 1 done, 2 fizzled
        readonly Dictionary<RagdollPawn, bool> s4cBurrow = new Dictionary<RagdollPawn, bool>();    // under the floor
        readonly Dictionary<RagdollPawn, int> s4cPins = new Dictionary<RagdollPawn, int>();        // a pin flying: 0, caught: 1, lost: 2
        readonly Dictionary<RagdollPawn, int> s4cDives = new Dictionary<RagdollPawn, int>();       // the rook's phase
        readonly Dictionary<RagdollPawn, int> s4cTimbers = new Dictionary<RagdollPawn, int>();     // the tower's phase
        readonly Dictionary<RagdollPawn, int> s4cGates = new Dictionary<RagdollPawn, int>();       // 0 open, 1 shut
        readonly HashSet<RagdollPawn> s4cSwallowed = new HashSet<RagdollPawn>();
        Mesh s4cPinMesh, s4cCylinder, s4cThroneMesh;
        Material s4cGold;

        void SetFourCFx(SkillFxEvent e, float lvl)
        {
            switch (e.kind)
            {
                case SkillFxKind.S4Promo: S4PromoFx(e, lvl); break;
                case SkillFxKind.S4Burrow: S4BurrowFx(e, lvl); break;
                case SkillFxKind.S4Pin: S4PinFx(e, lvl); break;
                case SkillFxKind.S4Dive: S4DiveFx(e, lvl); break;
                case SkillFxKind.S4Timber: S4TimberFx(e, lvl); break;
                case SkillFxKind.S4Pig: S4PigFx(e, lvl); break;
                case SkillFxKind.S4Lift: S4LiftFx(e, lvl); break;
                case SkillFxKind.S4Carpet: S4CarpetFx(e, lvl); break;
                case SkillFxKind.S4Throne: S4ThroneFx(e, lvl); break;
                case SkillFxKind.S4Gambit: S4GambitFx(e, lvl); break;
                default: SetFourJYFx(e, lvl); break;   // the user's own (PawnRushSkillFx.SetFourJY*.cs)
            }
        }

        /// <summary>A piece not drawn while <paramref name="still"/> says so (and drawn again after).</summary>
        void S4cHideWhile(RagdollPawn who, System.Func<bool> still, float most)
        {
            if (who == null || who.skin == null) return;
            var f = kit.Run(most, (fx, d) =>
            {
                if (who == null || who.skin == null) return false;
                if (!still()) return false;
                who.skin.forceRenderingOff = true;
                return true;
            });
            f.end = () => { if (who != null && who.skin != null) who.skin.forceRenderingOff = false; };
        }

        // ---------------------------------------------------------------- 폰

        void S4PromoFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (by == null) return;
            if (e.count != 0)
            {
                s4cPromo[by] = e.count;
                if (e.count == 1)
                {
                    // 승격!: gold everywhere, and the pawn gold its seconds.
                    float seconds = Mathf.Max(0.3f, e.size), range = Params != null ? Params.promoRange : 5f;
                    Vector3 c = by.bodies[(int)BodyId.Chest].position;
                    CmFlash(c, 1.2f, 10, CmGold);
                    kit.FlashRing(Ground(by.FeetPoint) + Vector3.up * 0.05f, range, CmGold);
                    CmPoof(c, CmGold, 12, range * 0.3f, 0.45f, 0.55f, 0.6f);
                    Shake(0.12f, 14f * Frame);
                    HitStop(5f * Frame);
                    UltClip(UltClipId.Promote, lvl, 1f);
                    var gold = S4cGoldMaterial();
                    kit.Run(seconds, (fx, d) =>
                    {
                        if (by == null) return false;
                        if (gold != null) WantGiant(by, 1f, 8f, gold);
                        if (Random.value < 0.25f) CmFlash(by.bodies[(int)BodyId.Chest].position + Random.insideUnitSphere * 0.4f, 0.12f, 8, CmGold);
                        return true;
                    });
                }
                else
                {
                    CmPoof(DrawnHead(by) + Vector3.up * 0.4f, CmSteam, 6, 0.5f, 0.22f, 0.6f, 0.7f);
                    UltClip(UltClipId.Wah, 0.7f * lvl, 0.55f);
                }
                return;
            }
            // Counting down: the crown over its head, the digit over the crown.
            s4cPromo[by] = 0;
            float total = Mathf.Max(0.5f, e.size), born = Clock;
            var crown = CmProp(S4CrownMesh(), CmGold, "Promotion crown", 0.012f);
            Text3D digit = font != null ? new Text3D(root, font, "5", Kit.Hex("#FFD23F"), 0.5f) : null;
            int shown = -1;
            float droop = -1f;
            UltClip(UltClipId.Shimmer, 0.6f * lvl, 1.2f);
            var f = kit.Run(total + 4f, (fx, d) =>
            {
                if (by == null) return false;
                int state = s4cPromo.TryGetValue(by, out var st) ? st : 2;
                float t = Clock - born, k = GiantScaleOf(by);
                Vector3 head = DrawnHead(by), up = HeadUp(by);
                var cam = ViewCamera;
                if (state == 2)
                {
                    // Fizzled: the crown slumps off and is gone.
                    if (droop < 0f) droop = Clock;
                    float g = (Clock - droop) / 0.6f;
                    if (digit != null) digit.Place(head, cam, 0.001f, 0f);
                    if (g >= 1f) return false;
                    CmPlace(crown, head + up * (0.2f * k) + Vector3.down * (0.8f * g * g) + Vector3.right * (0.3f * g), Quaternion.Euler(60f * g, t * 30f, 40f * g), Vector3.one * (1.4f * k * (1f - 0.5f * g)));
                    return true;
                }
                if (state == 1)
                {
                    // Promoted: the crown settles on the head while it is gold.
                    if (digit != null) digit.Place(head, cam, 0.001f, 0f);
                    float g = t - total;
                    if (g > (Params != null ? Params.promoGold : 3f)) { CmPoof(head + up * 0.3f, CmGold, 5, 0.4f, 0.2f, 0.4f, 0.5f); return false; }
                    CmPlace(crown, head + up * (0.08f * k), Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, t * 40f, 0f), Vector3.one * (1.8f * k));
                    return true;
                }
                // Counting: the crown turning over the head, the digit over it popping each second.
                CmPlace(crown, head + up * (0.42f * k) + Vector3.up * (0.04f * Mathf.Sin(t * 5f)), Quaternion.Euler(0f, t * 90f, 0f), Vector3.one * (1.3f * k * Kit.EaseOutBack(Mathf.Clamp01(t / 0.2f))));
                int left = Mathf.Clamp(Mathf.CeilToInt(total - t), 1, 9);
                if (left != shown)
                {
                    shown = left;
                    if (digit != null)
                    {
                        digit.Destroy();
                        digit = new Text3D(root, font, left.ToString(), left <= 1 ? Kit.Hex("#FF6A3D") : Kit.Hex("#FFD23F"), 0.5f);
                    }
                    UltClip(UltClipId.Tick, 0.6f * S3Heard(head), 1f + 0.12f * (5 - left));
                }
                float into = t - Mathf.Floor(t);
                if (digit != null && cam != null) digit.Place(head + Vector3.up * (0.95f * k), cam, Kit.EaseOutBack(Mathf.Clamp01(into / 0.15f)) * (1.2f - 0.2f * into), 1f);
                return t < total + 0.6f;
            });
            f.props.Add(crown);
            f.end = () =>
            {
                digit?.Destroy();
                if (by != null && s4cPromo.TryGetValue(by, out var st) && st != 1) s4cPromo.Remove(by);
            };
        }

        // ---------------------------------------------------------------- 나이트

        void S4BurrowFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (by == null) return;
            if (e.count != 0)
            {
                s4cBurrow[by] = false;
                CmPoof(e.at + Vector3.up * 0.2f, CmDirt, 12, 1f, 0.36f, 0.5f, 1f);
                kit.AddSquash(by, Kit.SquashKind.Spring);
                if (e.size > 0f) Shake(0.06f, 10f * Frame);
                UltClip(UltClipId.Crack, 0.7f * lvl, 0.7f);
                return;
            }
            s4cBurrow[by] = true;
            CmPoof(e.at + Vector3.up * 0.1f, CmDirt, 8, 0.7f, 0.3f, 0.45f, 0.6f);
            UltClip(UltClipId.SubDrop, 0.6f * lvl, 1.3f);
            float most = Mathf.Max(0.5f, e.size) + 1.5f;
            S4cHideWhile(by, () => s4cBurrow.TryGetValue(by, out var on) && on, most);
            var p = S3Colors(by);
            var earL = CmProp(S4cEarMesh(), p, "Knight ear", 0.012f);
            var earR = CmProp(S4cEarMesh(), p, "Knight ear", 0.012f);
            var hump = CmProp(S4MoundMesh(), CmEarth, "Burrow hump", 0.012f);
            float nextDirt = 0f;
            var f = kit.Run(most, (fx, d) =>
            {
                if (by == null || !(s4cBurrow.TryGetValue(by, out var on) && on)) return false;
                Vector3 feet = by.FeetPoint, face = FlatDir(by.Facing), right = Vector3.Cross(Vector3.up, face);
                float wob = Mathf.Sin(fx.age * 18f);
                Quaternion look = Quaternion.LookRotation(face, Vector3.up);
                CmPlace(earL, feet + right * -0.13f + face * 0.08f + Vector3.up * 0.12f, look * Quaternion.Euler(0f, 0f, 14f + 7f * wob), Vector3.one * 1.5f);
                CmPlace(earR, feet + right * 0.13f + face * 0.08f + Vector3.up * 0.12f, look * Quaternion.Euler(0f, 0f, -14f - 7f * wob), Vector3.one * 1.5f);
                CmPlace(hump, feet - face * 0.15f, look, new Vector3(0.55f, 0.35f + 0.05f * wob, 0.75f));
                if (fx.age >= nextDirt && by.HorizontalSpeed > 1f)
                {
                    nextDirt = fx.age + 0.08f;
                    kit.Drops(feet - face * 0.4f + Vector3.up * 0.1f, (Vector3.up * 1.2f - face).normalized, CmDirt, 1, 4f);
                }
                return true;
            });
            f.props.Add(earL);
            f.props.Add(earR);
            f.props.Add(hump);
        }

        Mesh s4cEar;

        /// <summary>A knight's ear: a little cone, 0.25 m tall, its foot at the origin.</summary>
        Mesh S4cEarMesh()
        {
            if (s4cEar != null) return s4cEar;
            return s4cEar = CmLathe(new[] { new Vector2(0f, 0f), new Vector2(0.07f, 0f), new Vector2(0.06f, 0.1f), new Vector2(0.03f, 0.2f), new Vector2(0f, 0.25f) }, 12, "Knight ear");
        }

        // ---------------------------------------------------------------- 비숍

        void S4PinFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (e.count == 0)
            {
                if (by == null) return;
                s4cPins[by] = 0;
                float speed = Mathf.Max(1f, e.size), most = (e.normal - e.at).magnitude / speed + 0.3f;
                Vector3 from = e.at, dir = FlatDir(e.dir);
                var pin = CmProp(S4cPinMesh(), CmSteel, "Safety pin", 0.016f);
                var clasp = CmProp(S4cClaspMesh(), CmGold, "Safety pin clasp", 0.014f);
                UltClip(UltClipId.Whoosh, 0.6f * lvl, 1.6f);
                var f = kit.Run(most, (fx, d) =>
                {
                    if (by == null || !s4cPins.TryGetValue(by, out var st) || st != 0) return false;
                    Quaternion r = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 0f, fx.age * 900f);
                    Vector3 at = from + dir * (speed * fx.age);
                    CmPlace(pin, at, r, Vector3.one * PinSize);
                    CmPlace(clasp, at + r * new Vector3(0f, 0f, 0.24f * PinSize), r, Vector3.one * PinSize);
                    if (Random.value < 0.5f) CmFlash(at, 0.1f, 6);
                    return true;
                });
                f.props.Add(pin);
                f.props.Add(clasp);
                return;
            }
            if (by != null) s4cPins[by] = e.count;
            if (e.count == 2)
            {
                CmPoof(e.at, CmSteel, 3, 0.3f, 0.14f, 0.3f, 0.3f);
                UltClip(UltClipId.Tap, 0.5f * lvl, 1.8f);
                return;
            }
            // Pinned: the pin stuck in the floor at the spot, a rubber band from it to the one pinned.
            var who = e.target;
            float seconds = Mathf.Max(0.3f, e.size);
            Vector3 dir2 = FlatDir(e.dir), spot = Ground(e.at - dir2 * 0.6f);
            var stuck = CmProp(S4cPinMesh(), CmSteel, "Safety pin", 0.016f);
            var stuckClasp = CmProp(S4cClaspMesh(), CmGold, "Safety pin clasp", 0.014f);
            var band = new Kit.Strip(kit, CmBump) { core = CmBump.light, coreShare = 0.3f, inkShare = 0.3f };
            var pts = new List<Vector3>(9);
            CmFlash(spot + Vector3.up * 0.2f, 0.35f, 7);
            UltClip(UltClipId.Tap, 0.9f * lvl, 1.2f);
            UltClip(UltClipId.Tock, 0.5f * lvl, 1.6f);
            var f2 = kit.Run(seconds + 0.4f, (fx, d) =>
            {
                float outk = Mathf.Clamp01((fx.age - seconds) / 0.4f);
                if (outk >= 1f) return false;
                // Upright in the floor, its clasp up, wobbling as the band pulls.
                float k = PinSize * (1f - outk) * Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.15f));
                Quaternion stab = Quaternion.LookRotation(Vector3.up * 0.9f - dir2 * 0.3f, dir2) * Quaternion.Euler(6f * Mathf.Sin(fx.age * 14f), 0f, 0f);
                Vector3 top = spot + stab * new Vector3(0f, 0f, 0.24f * k);
                CmPlace(stuck, spot + Vector3.up * 0.05f, stab, Vector3.one * Mathf.Max(0.02f, k));
                CmPlace(stuckClasp, top + Vector3.up * 0.05f, stab, Vector3.one * Mathf.Max(0.02f, k));
                if (who == null || outk > 0f)
                {
                    band.Hide();
                    return true;
                }
                Vector3 a = top + Vector3.up * 0.05f, b = who.Hips.position;
                float stretch = (b - a).magnitude;
                pts.Clear();
                for (int i = 0; i <= 8; i++)
                {
                    float t = i / 8f;
                    pts.Add(Vector3.Lerp(a, b, t) + Vector3.down * (0.2f * Mathf.Sin(t * Mathf.PI) * Mathf.Clamp01(1.5f - stretch * 0.5f)));
                }
                band.Build(pts, i => Mathf.Lerp(0.09f, 0.035f, Mathf.Clamp01(stretch / 3f)), kit.Eye);
                band.Apply();
                return true;
            });
            f2.props.Add(stuck);
            f2.props.Add(stuckClasp);
            f2.strips.Add(band);
        }

        /// <summary>The pin's drawn size (its mesh is 0.5 m long).</summary>
        const float PinSize = 2.2f;
        Mesh s4cClasp;

        /// <summary>The pin's clasp: a little rounded cap at the origin.</summary>
        Mesh S4cClaspMesh() => s4cClasp != null ? s4cClasp : s4cClasp = kit.RoundBox(new Vector3(0.13f, 0.08f, 0.1f), 0.03f);

        /// <summary>A safety pin, 0.5 m long along +Z: two wires, a coil at the back, a clasp at the front.</summary>
        Mesh S4cPinMesh()
        {
            if (s4cPinMesh != null) return s4cPinMesh;
            var wire = kit.RoundBox(new Vector3(0.025f, 0.025f, 0.46f), 0.012f);
            var parts = new[]
            {
                new CombineInstance { mesh = wire, transform = Matrix4x4.Translate(new Vector3(-0.04f, 0f, 0f)) },
                new CombineInstance { mesh = wire, transform = Matrix4x4.Translate(new Vector3(0.04f, 0f, 0f)) },
                new CombineInstance { mesh = kit.meshTorus, transform = Matrix4x4.TRS(new Vector3(0f, 0f, -0.24f), Quaternion.Euler(0f, 0f, 90f), Vector3.one * 0.1f) },
            };
            s4cPinMesh = new Mesh { name = "Safety pin", hideFlags = HideFlags.HideAndDontSave };
            s4cPinMesh.CombineMeshes(parts, true, true);
            s4cPinMesh.RecalculateNormals();
            s4cPinMesh.RecalculateBounds();
            Destroy(wire);
            return s4cPinMesh;
        }

        // ---------------------------------------------------------------- 룩

        void S4DiveFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (by == null) return;
            switch (e.count)
            {
                case 0:
                {
                    // The mark where it will land, and the crouch.
                    s4cDives[by] = 0;
                    Vector3 mark = Ground(e.at) + Vector3.up * 0.04f;
                    float life = Mathf.Max(0.5f, e.size), crouch = Params != null ? Params.diveCrouch : 0.4f, reach = Params != null ? Params.diveReach : 4f;
                    float nextRing = 0f;
                    kit.Run(life + 0.3f, (fx, d) =>
                    {
                        if (by == null || !s4cDives.TryGetValue(by, out var st) || st >= 2) return false;
                        if (fx.age < crouch) kit.Coil(by, fx.age / crouch);
                        if (fx.age >= nextRing)
                        {
                            nextRing = fx.age + 0.35f;
                            kit.FlashRing(mark, reach, CmRed);
                        }
                        DrawShade(mark, Mathf.Lerp(0.4f, 1.2f, fx.age / life), Mathf.Lerp(0.15f, 0.4f, fx.age / life));
                        return true;
                    });
                    UltClip(UltClipId.Riser, 0.6f * lvl, 0.9f);
                    break;
                }
                case 1:
                    s4cDives[by] = 1;
                    CmPoof(e.at + Vector3.up * 0.1f, CmDust, 6, 0.8f, 0.3f, 0.4f, 0.3f);
                    CmStreak(by, S3Colors(by), Mathf.Max(0.3f, e.size), 0.28f, 0.22f);
                    UltClip(UltClipId.Whoosh, 0.8f * lvl, 0.7f);
                    break;
                case 2:
                {
                    // Down head first: a boom, and stuck upside down kicking its seconds.
                    s4cDives[by] = 2;
                    float stuck = Mathf.Max(0.3f, e.size);
                    float reach = Params != null ? Params.diveReach : 4f;
                    CmFlash(e.at + Vector3.up * 0.4f, 1f, 9);
                    kit.DustRing(e.at + Vector3.up * 0.05f, 16, reach, CmDust, 0.45f, 0.6f);
                    CmPoof(e.at + Vector3.up * 0.3f, CmRock, 8, 1.2f, 0.4f, 0.5f, 0.6f);
                    PopWord("쿵!", e.at + Vector3.up * 1.4f, Kit.Hex("#FF6A3D"), 0.6f, 0.9f);
                    Shake(0.16f, 16f * Frame);
                    HitStop(6f * Frame);
                    UltClip(UltClipId.SubDrop, lvl, 0.6f);
                    UltClip(UltClipId.Crack, 0.8f * lvl, 0.5f);
                    float nextPuff = 0.2f;
                    kit.Run(stuck + 0.2f, (fx, d) =>
                    {
                        if (by == null || !s4cDives.TryGetValue(by, out var st) || st != 2) return false;
                        Vector3 right = Vector3.Cross(Vector3.up, FlatDir(by.Facing));
                        WantPose(by, Quaternion.AngleAxis(180f + 12f * Mathf.Sin(fx.age * 16f), right), true);
                        WantGiant(by, 1f, 8f);
                        if (fx.age >= nextPuff)
                        {
                            nextPuff = fx.age + 0.3f;
                            CmPoof(by.FeetPoint + Vector3.up * 0.1f, CmDust, 2, 0.4f, 0.18f, 0.35f, 0.3f);
                        }
                        return true;
                    });
                    break;
                }
                default:
                    s4cDives.Remove(by);
                    CmPoof(by.FeetPoint + Vector3.up * 0.3f, CmDust, 6, 0.7f, 0.3f, 0.4f, 0.5f);
                    kit.AddSquash(by, Kit.SquashKind.Spring);
                    break;
            }
        }

        void S4TimberFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (by == null) return;
            switch (e.count)
            {
                case 0:
                {
                    // Growing to a tower where it stands; its fall line's shadow laid ahead.
                    s4cTimbers[by] = 0;
                    float grow = Mathf.Max(0.2f, e.size), tall = Params != null ? Params.timberTall : 8f, wide = Params != null ? Params.timberWide : 2f;
                    float fall = Params != null ? Params.timberFall : 0.5f, bridge = Params != null ? Params.timberBridge : 2f;
                    float baseH = Mathf.Max(0.5f, GiantBaseHeight(by)), want = Mathf.Max(1.5f, tall / baseH);
                    Vector3 feet = Ground(e.at), dir = FlatDir(e.dir), right = Vector3.Cross(Vector3.up, dir);
                    float rate = (want - 1f) / (want * grow);
                    float phaseAt = Clock;
                    int lastPhase = 0;
                    UltClip(UltClipId.Riser, 0.8f * lvl, 0.6f);
                    UltClip(UltClipId.Castle, 0.7f * lvl, 0.7f);
                    kit.Run(grow + fall + bridge + 3f, (fx, d) =>
                    {
                        if (by == null || !s4cTimbers.TryGetValue(by, out var ph))
                        {
                            if (by != null) WantGiant(by, 1f, 60f);   // (back to its size at once)
                            return false;
                        }
                        if (ph != lastPhase)
                        {
                            lastPhase = ph;
                            phaseAt = Clock;
                        }
                        float since = Clock - phaseAt;
                        WantGiant(by, want, ph == 0 ? rate : 60f);
                        float tilt = ph == 0 ? 0f : ph == 1 ? 90f * Kit.EaseIn(Mathf.Clamp01(since / fall)) : 90f;
                        if (tilt > 0.01f) WantPose(by, Quaternion.AngleAxis(tilt, right), false);
                        if (ph < 2)
                        {
                            // The shadow of where it will lie, darker as it comes.
                            float k = ph == 0 ? Mathf.Clamp01(since / grow) * 0.6f : 0.6f + 0.4f * Mathf.Clamp01(since / fall);
                            for (int i = 1; i <= 5; i++) DrawShade(feet + dir * (tall * i / 5.5f) + Vector3.up * 0.02f, wide * 0.55f, 0.12f + 0.3f * k);
                        }
                        return true;
                    });
                    break;
                }
                case 1:
                    s4cTimbers[by] = 1;
                    UltClip(UltClipId.Whoosh, 0.8f * lvl, 0.45f);
                    break;
                case 2:
                {
                    s4cTimbers[by] = 2;
                    Vector3 a = Ground(e.at), b = Ground(e.normal);
                    for (int i = 0; i <= 6; i++) CmPoof(Vector3.Lerp(a, b, i / 6f) + Vector3.up * 0.2f, CmDust, 4, 1f, 0.4f, 0.5f, 0.4f);
                    PopWord("쿵!", Vector3.Lerp(a, b, 0.6f) + Vector3.up * 1.6f, Kit.Hex("#FF6A3D"), 0.7f, 1f);
                    Shake(0.2f, 18f * Frame);
                    HitStop(6f * Frame);
                    UltClip(UltClipId.SubDrop, lvl, 0.5f);
                    UltClip(UltClipId.Crack, 0.9f * lvl, 0.45f);
                    break;
                }
                default:
                    // (USER_SKILLS 9.1: up again at its base, where it was used - not at the fallen end.)
                    s4cTimbers.Remove(by);
                    CmPoof(by.FeetPoint + Vector3.up * 0.4f, CmDust, 10, 1.2f, 0.5f, 0.55f, 0.6f);
                    kit.AddSquash(by, Kit.SquashKind.Spring);
                    UltClip(UltClipId.Tap, 0.6f * lvl, 0.8f);
                    break;
            }
        }

        void S4PigFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            switch (e.count)
            {
                case 0:
                {
                    if (by == null) return;
                    // The gate on its front swinging open, air streaking into it.
                    s4cGates[by] = 0;
                    float seconds = Mathf.Max(0.3f, e.size), range = Params != null ? Params.pigRange : 8f, cone = Params != null ? Params.pigCone : 60f;
                    var left = CmProp(kit.RoundBox(new Vector3(0.3f, 0.66f, 0.05f), 0.03f), CmWood, "Gate leaf", 0.014f);
                    var right = CmProp(kit.RoundBox(new Vector3(0.3f, 0.66f, 0.05f), 0.03f), CmWood, "Gate leaf", 0.014f);
                    var door = CmProp(S4cCylinderMesh(), CmHole, "Gate doorway", 0.01f);
                    const int N = 5;
                    var streaks = new Kit.Strip[N];
                    for (int i = 0; i < N; i++) streaks[i] = new Kit.Strip(kit, CmWhite) { core = Color.white, coreShare = 0.5f, inkShare = 0.2f };
                    var pts = new List<Vector3>(3);
                    float openT = -1f, shutT = -1f;
                    UltClip(UltClipId.Wind, 0.8f * lvl, 0.8f);
                    var f = kit.Run(seconds + 6f, (fx, d) =>
                    {
                        if (by == null) return false;
                        int st = s4cGates.TryGetValue(by, out var g) ? g : 1;
                        if (openT < 0f) openT = Clock;
                        if (st == 1 && shutT < 0f) shutT = Clock;
                        float open = Kit.EaseOutBack(Mathf.Clamp01((Clock - openT) / 0.25f)) * (shutT < 0f ? 1f : 1f - Mathf.Clamp01((Clock - shutT) / 0.2f));
                        if (shutT >= 0f && Clock - shutT > 0.5f) return false;
                        Vector3 face = FlatDir(by.Facing), side = Vector3.Cross(Vector3.up, face);
                        Vector3 mouth = by.bodies[(int)BodyId.Chest].position + face * 0.33f + Vector3.down * 0.12f;
                        Quaternion look = Quaternion.LookRotation(face, Vector3.up);
                        // The dark doorway (an upright oval on its front), each leaf hinged at its side, swinging out.
                        CmPlace(door, mouth - face * 0.02f, Quaternion.LookRotation(Vector3.up, face), new Vector3(0.3f * Mathf.Max(0.05f, open), 0.04f, 0.4f * Mathf.Max(0.05f, open)));
                        CmPlace(left, mouth - side * 0.3f + Quaternion.AngleAxis(-100f * open, Vector3.up) * side * 0.15f, look * Quaternion.Euler(0f, -100f * open, 0f), Vector3.one);
                        CmPlace(right, mouth + side * 0.3f - Quaternion.AngleAxis(100f * open, Vector3.up) * side * 0.15f, look * Quaternion.Euler(0f, 100f * open, 0f), Vector3.one);
                        for (int i = 0; i < N; i++)
                        {
                            if (st == 1) { streaks[i].Hide(); continue; }
                            float a = (i - (N - 1) * 0.5f) * cone / N;
                            float k = Mathf.Repeat(fx.age * 1.6f + i * 0.21f, 1f);
                            Vector3 from = mouth + Quaternion.AngleAxis(a, Vector3.up) * face * range * 0.6f;
                            Vector3 tip = Vector3.Lerp(from, mouth, k), tail = Vector3.Lerp(from, mouth, Mathf.Max(0f, k - 0.25f));
                            pts.Clear();
                            pts.Add(tail);
                            pts.Add((tail + tip) * 0.5f);
                            pts.Add(tip);
                            streaks[i].Build(pts, j => 0.07f, kit.Eye);
                            streaks[i].Apply();
                        }
                        return true;
                    });
                    f.props.Add(left);
                    f.props.Add(right);
                    f.props.Add(door);
                    foreach (var s in streaks) f.strips.Add(s);
                    break;
                }
                case 1:
                {
                    // Gulp: it is gone inside.
                    var who = e.target;
                    if (who == null) return;
                    s4cSwallowed.Add(who);
                    CmPoof(e.at, CmWhite, 5, 0.5f, 0.24f, 0.35f, 0.4f);
                    PopWord("꿀꺽", e.at + Vector3.up * 0.9f, Kit.Hex("#FFB0C8"), 0.4f, 0.8f);
                    if (by != null) kit.AddSquash(by, Kit.SquashKind.Bump);
                    UltClip(UltClipId.SubDrop, 0.7f * lvl, 1.6f);
                    S4cHideWhile(who, () => s4cSwallowed.Contains(who), 7f);
                    break;
                }
                case 2:
                {
                    // Spat out behind with a bang.
                    var who = e.target;
                    if (who != null) s4cSwallowed.Remove(who);
                    CmFlash(e.at, 0.5f, 8);
                    CmPoof(e.at, CmWhite, 5, 0.6f, 0.26f, 0.35f, 0.4f);
                    PopWord("퉤!", e.at + Vector3.up * 0.9f, Kit.Hex("#FFB0C8"), 0.45f, 0.8f);
                    if (who != null) CmStreak(who, CmWhite, 0.6f, 0.2f, 0.16f);
                    UltClip(UltClipId.Crack, 0.7f * lvl, 1.3f);
                    break;
                }
                default:
                    if (by != null) s4cGates[by] = 1;
                    break;
            }
        }

        void S4LiftFx(SkillFxEvent e, float lvl)
        {
            float wide = Params != null ? Params.liftWide : 3f, tall = Params != null ? Params.liftTall : 4f, rise = Params != null ? Params.liftRise : 0.5f;
            float stand = Mathf.Max(0.3f, e.size), delay = Mathf.Max(0f, e.normal.x);   // (9.1: up once the rook is aside)
            Vector3 foot = e.at;
            var tower = CmProp(S4cCylinderMesh(), CmKeep, "Lift tower", 0.016f);
            const int N = 12;   // (USER_SKILLS 9.1: the battlements wall the top in; they rise as it does)
            var merlons = new Kit.Prop[N];
            for (int i = 0; i < N; i++) merlons[i] = CmProp(S4CapMesh(), CmKeep, "Lift battlement", 0.014f);
            // A plinth at its foot and a cornice under the battlements; four dark windows half way up.
            float[] courses = { 0.04f, 0.9f };
            var bands = new Kit.Prop[courses.Length];
            for (int i = 0; i < bands.Length; i++) bands[i] = CmProp(S4cCylinderMesh(), CmKeepDark, "Lift course", 0.01f);
            var windows = new Kit.Prop[4];
            for (int i = 0; i < windows.Length; i++) windows[i] = CmProp(S4cCylinderMesh(), CmHole, "Lift window", 0.008f);
            CmPoof(foot + Vector3.up * 0.2f, CmDust, 10, wide * 0.7f, 0.4f, 0.5f, 0.4f);
            Shake(0.06f, 10f * Frame);
            UltClip(UltClipId.Castle, 0.8f * lvl, 0.7f);
            bool sinking = false;
            var f = kit.Run(delay + rise * 2f + stand + 0.2f, (fx, d) =>
            {
                float age = Mathf.Max(0f, fx.age - delay);
                float top = PawnRushLiftTower.TopAt(age, rise, stand, tall);
                if (!sinking && age > rise + stand)
                {
                    sinking = true;
                    CmPoof(foot + Vector3.up * 0.2f, CmDust, 8, wide * 0.6f, 0.36f, 0.45f, 0.4f);
                    UltClip(UltClipId.Castle, 0.5f * S3Heard(foot), 0.5f);
                }
                // (The drum's foot below the floor: only what has risen shows.)
                CmPlace(tower, foot + Vector3.up * (top - tall), Quaternion.identity, new Vector3(wide * 0.5f, tall, wide * 0.5f));
                for (int i = 0; i < bands.Length; i++)
                {
                    float y = top - tall + tall * courses[i];
                    if (y < 0.02f) { bands[i].Show(false); continue; }
                    CmPlace(bands[i], foot + Vector3.up * y, Quaternion.identity, new Vector3(wide * 0.5f + 0.05f, 0.22f, wide * 0.5f + 0.05f));
                }
                for (int i = 0; i < windows.Length; i++)
                {
                    float y = top - tall * 0.42f;
                    if (y < 0.3f) { windows[i].Show(false); continue; }
                    Vector3 o = Quaternion.AngleAxis(45f + i * 90f, Vector3.up) * Vector3.forward;
                    CmPlace(windows[i], foot + o * (wide * 0.5f - 0.01f) + Vector3.up * y, Quaternion.LookRotation(Vector3.up, o), new Vector3(0.16f, 0.04f, 0.26f));
                }
                for (int i = 0; i < N; i++)
                {
                    float a = i * Mathf.PI * 2f / N;
                    Vector3 rim = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (wide * 0.5f - 0.2f);
                    float wallUp = Mathf.Clamp01((top - 0.5f) / Mathf.Max(0.5f, tall - 0.5f));
                    CmPlace(merlons[i], foot + rim + Vector3.up * top, Quaternion.LookRotation(rim, Vector3.up),
                        new Vector3(1.5f, 1.1f + 5.2f * wallUp, 1f) * Mathf.Clamp01(top / 0.5f));
                }
                return true;
            });
            f.props.Add(tower);
            foreach (var m in merlons) f.props.Add(m);
            foreach (var b in bands) f.props.Add(b);
            foreach (var w in windows) f.props.Add(w);
        }

        /// <summary>A closed cylinder, radius 1, from y 0 to 1.</summary>
        Mesh S4cCylinderMesh()
        {
            if (s4cCylinder != null) return s4cCylinder;
            return s4cCylinder = CmLathe(new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) }, 28, "Comic drum");
        }

        // ---------------------------------------------------------------- 퀸

        void S4CarpetFx(SkillFxEvent e, float lvl)
        {
            if (e.count == 1)
            {
                // Rolled up like a gimbap while it lies there.
                var who = e.target;
                if (who == null) return;
                float seconds = Mathf.Max(0.3f, e.size);
                var roll = CmProp(S4cCylinderMesh(), CmCarpet, "Rolled carpet", 0.014f);
                UltClip(UltClipId.Whoosh, 0.6f * lvl, 0.9f);
                var f = kit.Run(seconds + 0.3f, (fx, d) =>
                {
                    if (who == null) return false;
                    float outk = Mathf.Clamp01((fx.age - seconds) / 0.3f);
                    if (outk >= 1f)
                    {
                        CmPoof(who.Hips.position, CmCarpet, 5, 0.6f, 0.26f, 0.4f, 0.3f);
                        return false;
                    }
                    Vector3 hips = who.Hips.position, head = who.bodies[(int)BodyId.Head].position;
                    Vector3 axis = (head - hips).sqrMagnitude > 0.01f ? (head - hips).normalized : Vector3.up;
                    float grow = Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.15f)) * (1f - outk);
                    // (The drum stands from its foot up its axis: from below the hips to past the head.)
                    CmPlace(roll, hips - axis * 0.35f, Quaternion.FromToRotation(Vector3.up, axis), new Vector3(0.34f, 1.05f, 0.34f) * Mathf.Max(0.02f, grow));
                    return true;
                });
                f.props.Add(roll);
                return;
            }
            var by = e.by;
            float speed = Mathf.Max(1f, e.size), length = e.normal.x > 0f ? e.normal.x : 20f, width = e.normal.y > 0f ? e.normal.y : 3f;
            float seconds2 = e.normal.z > 0f ? e.normal.z : 5f, unroll = length / speed;
            Vector3 from = Ground(e.at) + Vector3.up * 0.03f, dir = FlatDir(e.dir), side = Vector3.Cross(Vector3.up, dir);
            var cloth = new Kit.Strip(kit, CmCarpet) { core = CmCarpet.main, coreShare = 0.82f, inkShare = 0.08f };
            var edgeL = new Kit.Strip(kit, CmGold) { core = CmGold.light, coreShare = 0.3f, inkShare = 0.25f };
            var edgeR = new Kit.Strip(kit, CmGold) { core = CmGold.light, coreShare = 0.3f, inkShare = 0.25f };
            var drum = CmProp(S4cCylinderMesh(), CmCarpet, "Carpet roll", 0.014f);
            var pts = new List<Vector3>(2);
            var ptsL = new List<Vector3>(2);
            var ptsR = new List<Vector3>(2);
            UltClip(UltClipId.March, 0.7f * lvl, 1f);
            UltClip(UltClipId.Promote, 0.5f * lvl, 0.9f);
            var f2 = kit.Run(unroll + seconds2 + 0.5f, (fx, d) =>
            {
                float rolled = Mathf.Min(length, speed * fx.age), fade = 1f - Mathf.Clamp01((fx.age - unroll - seconds2) / 0.5f);
                if (fade <= 0f) return false;
                Vector3 end = from + dir * Mathf.Max(0.05f, rolled);
                pts.Clear();
                pts.Add(from);
                pts.Add(end);
                cloth.Build(pts, i => width, kit.Eye, true);
                cloth.fade = fade;
                cloth.Apply();
                ptsL.Clear();
                ptsL.Add(from - side * (width * 0.5f));
                ptsL.Add(end - side * (width * 0.5f));
                edgeL.Build(ptsL, i => 0.14f, kit.Eye, true);
                edgeL.fade = fade;
                edgeL.Apply();
                ptsR.Clear();
                ptsR.Add(from + side * (width * 0.5f));
                ptsR.Add(end + side * (width * 0.5f));
                edgeR.Build(ptsR, i => 0.14f, kit.Eye, true);
                edgeR.fade = fade;
                edgeR.Apply();
                // The roll at the front, thinner as it unrolls, turning as it goes.
                float left = 1f - rolled / length;
                if (left > 0.01f)
                {
                    float r = Mathf.Lerp(0.12f, 0.45f, left);
                    CmPlace(drum, end + side * (width * 0.5f) + Vector3.up * r, Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, fx.age * 400f, 0f),
                        new Vector3(r, width, r));
                }
                else drum.Show(false);
                return true;
            });
            f2.strips.Add(cloth);
            f2.strips.Add(edgeL);
            f2.strips.Add(edgeR);
            f2.props.Add(drum);
        }

        /// <summary>Queens whose throne goes now (USER_SKILLS 9.2: "왕좌는 그 자리에서 펑 사라지고 퀸만 날아와 내려선다").</summary>
        readonly HashSet<RagdollPawn> s4cThroneEnd = new HashSet<RagdollPawn>();

        void EndThrone4(RagdollPawn by)
        {
            if (by != null) s4cThroneEnd.Add(by);
        }

        void S4ThroneFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (by == null) return;
            if (e.count != 0)
            {
                s4cThroneEnd.Remove(by);
                // Down on the throne: a boom; the throne stays a moment.
                float size = Params != null ? Params.throneSize : 5f, sit = Mathf.Max(0.2f, e.size);
                Vector3 at = Ground(e.at);
                CmFlash(at + Vector3.up * 0.5f, 1f, 9, CmGold);
                kit.DustRing(at + Vector3.up * 0.05f, 16, size * 0.5f, CmDust, 0.45f, 0.6f);
                kit.FlashRing(at + Vector3.up * 0.05f, size * 0.5f, CmGold);
                PopWord("쿵!", at + Vector3.up * 1.6f, Kit.Hex("#FFD23F"), 0.6f, 0.9f);
                Shake(0.16f, 16f * Frame);
                HitStop(6f * Frame);
                UltClip(UltClipId.SubDrop, lvl, 0.7f);
                UltClip(UltClipId.Promote, 0.7f * lvl, 1.1f);
                var throne = CmProp(S4cThroneMesh(), CmGold, "Throne", 0.016f);
                Quaternion turn = Quaternion.LookRotation(-FlatDir(by.Facing), Vector3.up);
                var f = kit.Run(sit + 1.5f, (fx, d) =>
                {
                    float outk = Mathf.Clamp01((fx.age - sit - 1.2f) / 0.3f);
                    if (outk >= 1f || (by != null && s4cThroneEnd.Remove(by)))
                    {
                        CmPoof(at + Vector3.up * 0.5f, CmGold, 6, 0.8f, 0.3f, 0.4f, 0.4f);
                        return false;
                    }
                    CmPlace(throne, at - FlatDir(by != null ? by.Facing : Vector3.forward) * 0.25f, turn, Vector3.one * (1.25f * (1f - outk)));
                    return true;
                });
                f.props.Add(throne);
                return;
            }
            // The shadow and a fanfare; the queen up out of sight and coming down with the throne.
            float flight = Mathf.Max(0.3f, e.size), size2 = Params != null ? Params.throneSize : 5f;
            Vector3 spot = Ground(e.at), start = e.normal;
            UltClip(UltClipId.Promote, 0.8f * lvl, 0.9f);
            UltClip(UltClipId.March, 0.6f * lvl, 1.2f);
            CmStreak(by, CmGold, flight, 0.24f, 0.2f);
            var chair = CmProp(S4cThroneMesh(), CmGold, "Throne", 0.016f);
            bool hidden = false;
            var f2 = kit.Run(flight + 0.2f, (fx, d) =>
            {
                if (by == null) return false;
                float k = Mathf.Clamp01(fx.age / flight);
                DrawShade(spot + Vector3.up * 0.03f, size2 * 0.5f * Mathf.Lerp(0.4f, 1f, k), Mathf.Lerp(0.12f, 0.45f, k));
                // Out of sight while high (from a third of the way up to the last 30% of the way down).
                float high = by.Hips.position.y - Mathf.Max(start.y, spot.y);
                bool gone = k > 0.15f && k < 0.75f && high > 2f;
                if (gone && !hidden) CmPoof(by.Hips.position, CmGold, 5, 0.5f, 0.24f, 0.35f, 0.4f);
                hidden = gone;
                if (by.skin != null) by.skin.forceRenderingOff = gone;
                if (k >= 0.75f)
                {
                    // The throne under her as she comes down.
                    CmPlace(chair, by.FeetPoint - Vector3.up * 0.15f - FlatDir(by.Facing) * 0.25f, Quaternion.LookRotation(-FlatDir(by.Facing), Vector3.up), Vector3.one * 1.25f);
                }
                else chair.Show(false);
                return true;
            });
            f2.props.Add(chair);
            f2.end = () => { if (by != null && by.skin != null) by.skin.forceRenderingOff = false; };
        }

        /// <summary>A throne, its seat's front edge at the origin facing +Z: legs, a seat, a tall back, two arms.</summary>
        Mesh S4cThroneMesh()
        {
            if (s4cThroneMesh != null) return s4cThroneMesh;
            var seat = kit.RoundBox(new Vector3(0.7f, 0.14f, 0.6f), 0.04f);
            var back = kit.RoundBox(new Vector3(0.7f, 1f, 0.12f), 0.05f);
            var arm = kit.RoundBox(new Vector3(0.1f, 0.25f, 0.55f), 0.03f);
            var leg = kit.RoundBox(new Vector3(0.1f, 0.42f, 0.1f), 0.03f);
            var parts = new List<CombineInstance>
            {
                new CombineInstance { mesh = seat, transform = Matrix4x4.Translate(new Vector3(0f, 0.45f, -0.3f)) },
                new CombineInstance { mesh = back, transform = Matrix4x4.Translate(new Vector3(0f, 1f, -0.62f)) },
                new CombineInstance { mesh = arm, transform = Matrix4x4.Translate(new Vector3(-0.38f, 0.62f, -0.3f)) },
                new CombineInstance { mesh = arm, transform = Matrix4x4.Translate(new Vector3(0.38f, 0.62f, -0.3f)) },
                new CombineInstance { mesh = kit.meshBall, transform = Matrix4x4.TRS(new Vector3(0f, 1.56f, -0.62f), Quaternion.identity, Vector3.one * 0.16f) },
            };
            foreach (var x in new[] { -0.3f, 0.3f })
                foreach (var z in new[] { -0.05f, -0.55f })
                    parts.Add(new CombineInstance { mesh = leg, transform = Matrix4x4.Translate(new Vector3(x, 0.21f, z)) });
            s4cThroneMesh = new Mesh { name = "Throne", hideFlags = HideFlags.HideAndDontSave };
            s4cThroneMesh.CombineMeshes(parts.ToArray(), true, true);
            s4cThroneMesh.RecalculateNormals();
            s4cThroneMesh.RecalculateBounds();
            foreach (var m in new[] { seat, back, arm, leg }) Destroy(m);
            return s4cThroneMesh;
        }

        void S4GambitFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (e.count != 0)
            {
                // One down: a thud and dust, and whoever was under it pressed flat.
                CmPoof(e.at + Vector3.up * 0.2f, CmDust, 8, 1.4f, 0.4f, 0.5f, 0.3f);
                Shake(0.1f, 10f * Frame);
                if (e.size > 0f)
                {
                    float reach = (Params != null ? Params.gambitPiece : 2f) * 0.5f + 0.4f;
                    foreach (var o in RagdollPawn.All)
                        if (o != null && o != by && Flat(o.Hips.position - e.at).magnitude <= reach && Mathf.Abs(o.Hips.position.y - e.at.y) < 2.5f)
                            kit.AddSquash(o, Kit.SquashKind.Flatten, 0.8f);
                }
                UltClip(UltClipId.SubDrop, 0.8f * lvl, 0.8f + 0.06f * e.count);
                return;
            }
            float delay = Mathf.Max(0.2f, e.size), radius = e.normal.x, every = e.normal.y > 0f ? e.normal.y : 0.3f, high = e.normal.z > 0f ? e.normal.z : 6f;
            float fall = Params != null ? Params.gambitFall : 0.35f, piece = Params != null ? Params.gambitPiece : 2f;
            var kinds = new[] { PieceKind.Queen, PieceKind.Rook, PieceKind.Knight, PieceKind.Bishop, PieceKind.Pawn };
            var props = new Kit.Prop[5];
            var spots = new Vector3[5];
            var sizes = new float[5];
            var p = S3Colors(by);
            for (int i = 0; i < 5; i++)
            {
                var mesh = SkillMeshes.Piece(kinds[i]);
                props[i] = CmProp(mesh, p, "Gambit piece", 0.02f);
                spots[i] = Ground(RagdollPawn.GambitSpot4(e.at, radius, i) + Vector3.up * 2f);
                // Scaled to stand piece × 1.2 tall (its foot at the mesh's origin).
                sizes[i] = piece * 1.2f / Mathf.Max(0.1f, mesh.bounds.size.y);
            }
            UltClip(UltClipId.Riser, 0.7f * lvl, 0.8f);
            var landed = new bool[5];
            var f = kit.Run(delay + 4f * every + fall + 1.5f, (fx, d) =>
            {
                for (int i = 0; i < 5; i++)
                {
                    float start = delay + i * every, t = fx.age - start;
                    float hang = Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.3f));
                    // Hanging upside down, swaying, its shadow under it; then dropping.
                    float drop = t < 0f ? 0f : Mathf.Clamp01(t / fall);
                    if (t > fall + 0.8f)
                    {
                        props[i].Show(false);
                        continue;
                    }
                    if (drop >= 1f && !landed[i])
                    {
                        landed[i] = true;
                        CmPoof(spots[i] + Vector3.up * 0.2f, CmDust, 6, 1.2f, 0.36f, 0.45f, 0.3f);
                    }
                    float sink = Mathf.Clamp01((t - fall - 0.5f) / 0.3f);
                    // Upside down while it hangs, swaying; turning over as it falls, so it lands on its foot.
                    float half = piece * 0.6f;
                    Vector3 mid = spots[i] + Vector3.up * (high * (1f - drop * drop) + half) + Vector3.down * (sink * piece);
                    Quaternion turn = Quaternion.Euler(180f * (1f - Kit.EaseIn(drop)) + (drop < 1f ? 5f * Mathf.Sin(fx.age * 3f + i) : 0f), i * 40f, 0f);
                    float sz = sizes[i] * Mathf.Max(0.02f, hang) * (1f - sink);
                    CmPlace(props[i], mid - turn * Vector3.up * (half * sz / Mathf.Max(0.01f, sizes[i])), turn, Vector3.one * sz);
                    if (drop < 1f) DrawShade(spots[i] + Vector3.up * 0.02f, piece * 0.5f * Mathf.Lerp(0.6f, 1f, drop), Mathf.Lerp(0.15f, 0.45f, drop));
                }
                return true;
            });
            foreach (var pr in props) f.props.Add(pr);
        }

        Material S4cGoldMaterial()
        {
            if (s4cGold != null) return s4cGold;
            if (LitMaterials.Lit == null) return null;
            s4cGold = LitMaterials.NewLit(Kit.Hex("#FFC93C"), 0.75f, 0.85f);
            s4cGold.hideFlags = HideFlags.HideAndDontSave;
            return s4cGold;
        }

        void DestroySetFourC()
        {
            foreach (var p in s4cSwallowed)
                if (p != null && p.skin != null) p.skin.forceRenderingOff = false;
            s4cSwallowed.Clear();
            s4cPromo.Clear();
            s4cBurrow.Clear();
            s4cPins.Clear();
            s4cDives.Clear();
            s4cTimbers.Clear();
            s4cGates.Clear();
            foreach (var m in new UnityEngine.Object[] { s4cPinMesh, s4cCylinder, s4cThroneMesh, s4cEar, s4cClasp, s4cGold })
                if (m != null) Destroy(m);
            s4cPinMesh = s4cCylinder = s4cThroneMesh = s4cEar = s4cClasp = null;
            s4cGold = null;
            DestroySetFourJY();
        }
    }
}
