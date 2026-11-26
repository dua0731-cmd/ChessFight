using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The way back and the teeter of the set 4판's round 1 under the movement rule (RagdollPawn.SetFourFix.cs; USER_SKILLS
    /// 9.2 and 9.3), on every screen. S4Return's count is its stage plus ten times the skill (1 땅속 헤엄, 2 더 루우욱!,
    /// 3 착석!):
    ///
    ///   0  the print where it was used: the piece's glyph in a ring on the floor, teal to its own side and red to the
    ///      others, glowing and breathing until the piece is back ("찍")
    ///   1  the band (size: its seconds; at: the body, normal: the print): a rubber band of light from the body to the
    ///      print pulled taut ("끼이익"), the body leaning to it; the rook head first pops out of the floor ("뽕!"), the
    ///      throne vanishes where it stands ("펑!")
    ///   2  the flight (size: its seconds): the body stretched long and leaning along the way ("띠용—"), the band shortening
    ///   3  down on the print: a wobble and a little dust, the print gone ("뽁")
    ///   4  the print gone (a reset)
    ///   S4Teeter (at: the toe, dir: the way; size: seconds): at the edge, leaning out and back as if on one foot with its
    ///      arms going, "어, 어, 어!", pebbles dropping from the toe
    ///
    /// The sounds are places only: the nineteen clips' pitches (USER_SKILLS 5.5). Not seen in Unity yet.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        sealed class S4ReturnLook { public bool done; public Vector3 print; public Vector3 face; public int phase; public float since; }
        readonly Dictionary<RagdollPawn, S4ReturnLook> s4Returns = new Dictionary<RagdollPawn, S4ReturnLook>();
        Font s4Glyphs;

        void SetFourFixFx(SkillFxEvent e, float lvl)
        {
            switch (e.kind)
            {
                case SkillFxKind.S4Return: S4ReturnFx(e, lvl); break;
                case SkillFxKind.S4Teeter: S4TeeterFx(e, lvl); break;
            }
        }

        static string PieceGlyph4(PieceKind k) => k switch
        {
            PieceKind.Knight => "♞",
            PieceKind.Bishop => "♝",
            PieceKind.Rook => "♜",
            PieceKind.Queen => "♛",
            PieceKind.King => "♚",
            _ => "♟",
        };

        void S4ReturnFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (by == null || kit == null) return;
            int stage = e.count % 10, skill = e.count / 10;
            switch (stage)
            {
                case 0:
                {
                    // The print, until the piece is back.
                    if (s4Returns.TryGetValue(by, out var old)) old.done = true;
                    var look = new S4ReturnLook { print = Ground(e.at + Vector3.up * 0.5f) + Vector3.up * 0.03f, face = FlatDir(e.dir), since = Clock };
                    s4Returns[by] = look;
                    bool ours = Viewer != null && Viewer.Team == by.Team && by.Team != Teams.None;
                    var pal = ours ? CmTeal : CmRed;
                    var ring = new Kit.Tile(kit, "Return print");
                    if (s4Glyphs == null) s4Glyphs = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Symbol", "Arial Unicode MS", "DejaVu Sans" }, 64);
                    var glyph = s4Glyphs != null ? new Text3D(root, s4Glyphs, PieceGlyph4(by.Piece), Kit.A(pal.light, 0.95f), 0.75f) : null;
                    UltClip(UltClipId.Tap, 0.5f * lvl, 2.2f);   // ("찍")
                    var f = kit.Run(30f, (fx, d) =>
                    {
                        if (look.done || by == null) return false;
                        float t = Clock - look.since, pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f);
                        float grow = Kit.EaseOutBack(Mathf.Clamp01(t / 0.25f));
                        ring.Floor(look.print, look.face, Vector2.one * (1.1f * grow));
                        ring.shape = 1f;
                        ring.inner = 0.72f;
                        ring.fill = Kit.A(pal.main, 0.55f + 0.25f * pulse);
                        ring.ink = Kit.A(pal.ink, 0.8f);
                        ring.inkWidth = 0.03f;
                        ring.core = Color.clear;
                        ring.rim = Color.clear;
                        ring.stripe = Color.clear;
                        ring.fade = 1f;
                        ring.Apply();
                        if (glyph != null)
                        {
                            glyph.Place(look.print + Vector3.up * 0.02f, null, Mathf.Max(0.01f, grow), 0.7f + 0.3f * pulse);
                            glyph.root.rotation = Quaternion.LookRotation(Vector3.down, look.face);
                        }
                        return true;
                    });
                    f.tiles.Add(ring);
                    f.end = () => { if (glyph != null) glyph.Destroy(); };
                    break;
                }
                case 1:
                {
                    // The band pulled taut between the body and the print.
                    if (!s4Returns.TryGetValue(by, out var look)) return;
                    look.phase = 1;
                    float band = Mathf.Max(0.05f, e.size);
                    bool ours = Viewer != null && Viewer.Team == by.Team && by.Team != Teams.None;
                    var pal = ours ? CmTeal : CmRed;
                    if (skill == 2)
                    {
                        CmPoof(by.FeetPoint + Vector3.up * 0.2f, CmDust, 8, 0.6f, 0.4f, 0.5f, 0.6f);
                        JyWord("뽕!", by.Hips.position, JyWordGold, by);
                    }
                    else if (skill == 3)
                    {
                        EndThrone4(by);   // (the throne goes where it stands: PawnRushSkillFx.SetFourC.cs)
                        CmPoof(by.FeetPoint + Vector3.up * 0.5f, CmWhite, 10, 1.2f, 0.55f, 0.5f, 0.5f);
                        JyWord("펑!", by.Hips.position, JyWordGold, by);
                    }
                    UltClip(UltClipId.Wah, 0.55f * lvl, 1.7f);   // ("끼이익")
                    S4Band(by, look, pal, band + 0.5f);
                    float t0 = Clock;
                    kit.Run(band, (fx, d) =>
                    {
                        if (by == null || look.done) return false;
                        // Leaning into the pull, shivering a little.
                        Vector3 to = FlatDir(look.print - by.FeetPoint), right = Vector3.Cross(Vector3.up, to);
                        float k = Mathf.Clamp01((Clock - t0) / band);
                        WantPose(by, Quaternion.AngleAxis(-14f * k + Random.Range(-2f, 2f), right), false);
                        WantStretch(by, new Vector3(1f - 0.08f * k, 1f + 0.12f * k, 1f - 0.08f * k));
                        return true;
                    });
                    break;
                }
                case 2:
                {
                    // Off like a band let go: long and leaning along the way.
                    if (!s4Returns.TryGetValue(by, out var look)) return;
                    look.phase = 2;
                    float fly = Mathf.Max(0.05f, e.size);
                    UltClip(UltClipId.Wah, 0.7f * lvl, 0.75f);   // ("띠용—")
                    JyWord("띠용—", by.Hips.position, JyWordWhite, by);
                    kit.Run(fly + 0.05f, (fx, d) =>
                    {
                        if (by == null || look.done) return false;
                        Vector3 to = FlatDir(look.print - by.FeetPoint), right = Vector3.Cross(Vector3.up, to);
                        WantPose(by, Quaternion.AngleAxis(-65f, right), false);
                        WantStretch(by, new Vector3(0.72f, 1.55f, 0.72f));
                        return true;
                    });
                    break;
                }
                case 3:
                {
                    // Down on the print: a wobble, a little dust, the print gone.
                    if (s4Returns.TryGetValue(by, out var look))
                    {
                        look.done = true;
                        s4Returns.Remove(by);
                    }
                    kit.AddSquash(by, Kit.SquashKind.Spring);
                    CmPoof(Ground(e.at + Vector3.up * 0.5f) + Vector3.up * 0.1f, CmDust, 6, 0.5f, 0.3f, 0.4f, 0.4f);
                    UltClip(UltClipId.Tap, 0.6f * lvl, 1.6f);   // ("뽁")
                    JyWord("뽁", by.Hips.position, JyWordGold, by);
                    break;
                }
                default:
                    if (s4Returns.TryGetValue(by, out var gone))
                    {
                        gone.done = true;
                        s4Returns.Remove(by);
                    }
                    break;
            }
        }

        /// <summary>The rubber band: from the hips to the print, taut while the piece leans to it and shortening as it
        /// flies, gone with the print.</summary>
        void S4Band(RagdollPawn by, S4ReturnLook look, Kit.Palette pal, float life)
        {
            var band = new Kit.Strip(kit, pal) { core = Color.white, coreShare = 0.45f, inkShare = 0.22f };
            var pts = new List<Vector3>(12);
            var f = kit.Run(life + 0.6f, (fx, d) =>
            {
                if (by == null || look.done) return false;
                Vector3 a = by.Hips.position, b = look.print + Vector3.up * 0.15f;
                pts.Clear();
                // Taut: a straight line with a quick shiver while it is pulled.
                float shiver = look.phase == 1 ? 0.04f * Mathf.Sin(Clock * 70f) : 0f;
                Vector3 side = Vector3.Cross(Vector3.up, FlatDir(b - a));
                for (int i = 0; i <= 10; i++)
                {
                    float k = i / 10f;
                    pts.Add(Vector3.Lerp(a, b, k) + side * (shiver * Mathf.Sin(k * Mathf.PI)));
                }
                band.Build(pts, i => 0.075f, kit.Eye, true);
                band.Apply();
                return true;
            });
            f.strips.Add(band);
        }

        void S4TeeterFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (by == null || kit == null) return;
            float life = Mathf.Max(0.1f, e.size);
            Vector3 way = FlatDir(e.dir), right = Vector3.Cross(Vector3.up, way), toe = e.at;
            SayBubble(by, "어, 어, 어!", life + 0.3f);
            UltClip(UltClipId.Wah, 0.45f * lvl, 1.35f);
            // Pebbles off the toe, down the drop.
            for (int i = 0; i < 3; i++)
                CmPoof(toe + way * 0.15f + Vector3.up * 0.05f, CmDust, 3, 0.18f, 0.12f, 0.7f, -1.4f, way * 0.3f);
            float t0 = Clock;
            kit.Run(life, (fx, d) =>
            {
                if (by == null) return false;
                float t = (Clock - t0) / life;
                // Out over the drop and back, faster and smaller as it finds its feet; a little sway side to side.
                float lean = 16f * Mathf.Sin(t * Mathf.PI * 5f) * (1f - t), roll = 7f * Mathf.Sin(t * Mathf.PI * 7f) * (1f - t);
                WantPose(by, Quaternion.AngleAxis(lean, right) * Quaternion.AngleAxis(roll, way), false);
                return true;
            });
        }

        void DestroySetFourFix()
        {
            foreach (var look in s4Returns.Values) look.done = true;
            s4Returns.Clear();
        }
    }
}
