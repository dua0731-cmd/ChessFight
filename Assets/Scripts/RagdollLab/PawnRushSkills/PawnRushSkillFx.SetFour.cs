using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Network;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The set 4판's moments on every screen (RagdollPawn.SetFour.cs, RagdollPawn.Cc.cs; JY-skill-lab 11-26), in the comic
    /// parts of PawnRushSkillFx.Comic.cs ("웃기고 코믹한 느낌이되 너무 허접하진 않고 퀄리티 좋게"). Not seen in Unity by anyone yet.
    ///
    ///   the hindrances  (S4Cc, the looks of ROUND1 3장) a small name over the head in the hindrance's colour with a bar
    ///                   running down, and on the body: 콰당! a big "콰당!" and four gold stars round its head; 헤롱헤롱 three
    ///                   gold stars going round over its head (ROUND1's stars, not design A's cream pawns); 지침 slumped,
    ///                   sweat flying off and puffs of breath; 무거움 drawn cast iron (baked, as the promotion's giant), a
    ///                   squash and dust thumped up at every step; 묶임 rope wound twice round the ankles with a knot (a hop:
    ///                   dust and a spring); 미끌미끌 soap bubbles rising off the feet and glints; 쪼꼬미 drawn half its size
    ///                   (the promotion's giant drawing, shrunk) with a pop going in and out. Not drawn yet: X eyes, rolling
    ///                   eyes, a tongue out (the pieces have no face to draw them on), 쪼꼬미's high voice
    ///   the words       (S4Say) a white comic speech bubble with an ink edge and a tail at the speaker's head
    ///   폰              앙파상! two rubbery legs shot out sideways with a round shoe each; 벽돌 a brick popping in over the
    ///                   head, a shadow under it, dropping - a bonk and a pink bump on a head, or a thud on the floor;
    ///                   구덩이 a dark hole with clods round it, then a dirt mound up to the caught one's waist
    ///   나이트          포크! a streak along the leap and a ring where it lands; two heads: a pop, "딱!", stars of drops;
    ///                   one: a smack and a flattened head
    ///   비숍            성수병 the 3판 flask and puddle (its own time); 설교 a little bishop hopping onto the enemy's shoulder
    ///                   and preaching in a little bubble ("블라블라~", "어흠!")
    ///   룩              직진! snorts of steam and a crouch, then streaks and dust; a wall: a pop, "쾅!", rubble
    ///                   돌 얹기 a wall stone dropped onto the head, sitting there, tumbling off at the end
    ///   퀸              왕관 헌납 a spinning gold crown along the host's own arc, glinting, sitting crooked on the head it
    ///                   hits; 무엄하다! air streaking into her mouth, then three sound waves rolling over her fan
    /// </summary>
    public partial class PawnRushSkillFx
    {
        static readonly Kit.Palette CmBrickRed = new Kit.Palette("#D9613F", "#FFB59A", "#9C3A22", "#3A140A");
        static readonly Kit.Palette CmDirt = new Kit.Palette("#A0703F", "#DDB07C", "#6B4424", "#2A1A0C");
        static readonly Kit.Palette CmHole = new Kit.Palette("#3B2717", "#55391F", "#22150A", "#120A04");
        static readonly Kit.Palette CmSweat = new Kit.Palette("#7FD3FF", "#FFFFFF", "#3A9BE0", "#0B3A66");
        static readonly Kit.Palette CmSoap = new Kit.Palette("#D6F6FF", "#FFFFFF", "#94DDF7", "#2A6C8C");
        static readonly Kit.Palette CmBump = new Kit.Palette("#FF9DB8", "#FFE3EC", "#E8628A", "#4A1024");
        static readonly Kit.Palette CmSteam = new Kit.Palette("#F7F7F7", "#FFFFFF", "#CDD2DC", "#3A3F4A");
        static readonly Kit.Palette CmShoe = new Kit.Palette("#4A3426", "#7A5A44", "#2E2018", "#120A06");
        static readonly Color BubbleInk = Kit.Hex("#1B2340");

        sealed class CcLook { public RagdollPawn who; public Cc4 kind; public float until; }
        sealed class Brick4Fx { public RagdollPawn by, on; public Vector3 at, pos; public float born, delay, fall, landed; public int state; }
        sealed class Pit4Fx { public RagdollPawn by; public Vector3 at; public float until; public int state; }
        sealed class Crown4Fx { public RagdollPawn by, on; public Vector3 from, to; public float born, flight, landed, until; public int state; }
        sealed class Bubble4 { public Kit.Prop shape; public Text3D text; public float width; }

        readonly Dictionary<RagdollPawn, CcLook> ccLooks = new Dictionary<RagdollPawn, CcLook>();
        readonly List<Brick4Fx> s4Bricks = new List<Brick4Fx>();
        readonly List<Pit4Fx> s4Pits = new List<Pit4Fx>();
        readonly List<Crown4Fx> s4Crowns = new List<Crown4Fx>();
        readonly Dictionary<string, Mesh> s4Bubbles = new Dictionary<string, Mesh>();
        Mesh s4BrickMesh, s4CapMesh, s4CrownMesh, s4MoundMesh, s4ShadeMesh, s4StarMesh;
        Material s4Iron;
        MaterialPropertyBlock s4ShadeBlock;   // made on first use

        // ---------------------------------------------------------------- the moments

        void SetFourFx(SkillFxEvent e)
        {
            S3Build();
            if (kit == null) return;
            float lvl = S3Heard(e.at);
            switch (e.kind)
            {
                case SkillFxKind.S4Cc: CcLookFx(e, lvl); break;
                case SkillFxKind.S4Hop: S4HopFx(e, lvl); break;
                case SkillFxKind.S4Say: S4SayFx(e); break;
                case SkillFxKind.S4Trip: S4TripFx(e, lvl); break;
                case SkillFxKind.S4Brick: S4BrickFx(e, lvl); break;
                case SkillFxKind.S4Pit: S4PitFx(e, lvl); break;
                case SkillFxKind.S4Leap: S4LeapFx(e, lvl); break;
                case SkillFxKind.S4Bonk: S4BonkFx(e, lvl); break;
                case SkillFxKind.S4Sermon: S4SermonFx(e, lvl); break;
                case SkillFxKind.S4Charge: S4ChargeFx(e, lvl); break;
                case SkillFxKind.S4Cap: S4CapFx(e, lvl); break;
                case SkillFxKind.S4Crown: S4CrownFx(e, lvl); break;
                case SkillFxKind.S4Shout: S4ShoutFx(e, lvl); break;
                case SkillFxKind.S4Puddle:
                    CmPuddle(e, Params != null ? Params.water4Seconds : 4f);
                    UltClip(UltClipId.Crack, 0.5f * lvl, 1.8f);
                    break;
                default:
                    SetFourBFx(e, lvl);   // bundle 나 (PawnRushSkillFx.SetFourB.cs)
                    break;
            }
        }

        // ---------------------------------------------------------------- shared

        /// <summary>Where a piece's head is drawn (a 쪼꼬미 is drawn smaller from its feet).</summary>
        Vector3 DrawnHead(RagdollPawn p)
        {
            if (p == null) return Vector3.zero;
            Vector3 head = p.bodies[(int)BodyId.Head].position;
            float k = GiantScaleOf(p);
            if (Mathf.Abs(k - 1f) < 0.01f) return head;
            Vector3 feet = p.FeetPoint;
            return feet + (head - feet) * k;
        }

        /// <summary>The way out of the top of its head (chest to head), whichever way the body lies.</summary>
        static Vector3 HeadUp(RagdollPawn p)
        {
            Vector3 d = p.bodies[(int)BodyId.Head].position - p.bodies[(int)BodyId.Chest].position;
            return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.up;
        }

        static Color CcColor(Cc4 kind)
        {
            switch (kind)
            {
                case Cc4.Flop: return Kit.Hex("#FF6A3D");
                case Cc4.Dizzy: return Kit.Hex("#FFD23F");
                case Cc4.Tired: return Kit.Hex("#6CC8FF");
                case Cc4.Heavy: return Kit.Hex("#C9C2B8");
                case Cc4.BoundHop:
                case Cc4.BoundRoot:
                case Cc4.Pinned: return Kit.Hex("#F0B868");
                case Cc4.Slippery: return Kit.Hex("#7FF0F0");
                case Cc4.Tiny: return Kit.Hex("#FF9AD5");
                default: return Color.white;
            }
        }

        /// <summary>A comic sound word ("콰당!", "딱!", "쾅!") popping out, tilted, rising, shrinking away; it rides along
        /// with <paramref name="follow"/> if given.</summary>
        void PopWord(string word, Vector3 at, Color color, float height, float life, RagdollPawn follow = null, float tilt = 0f)
        {
            if (font == null || kit == null) return;
            var text = new Text3D(root, font, word, color, height);
            Vector3 off = follow != null ? at - follow.Hips.position : Vector3.zero;
            float roll = tilt != 0f ? tilt : Random.Range(-12f, 12f);
            var f = kit.Run(life, (fx, d) =>
            {
                var cam = ViewCamera;
                float t = fx.age / life;
                float pop = Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.14f)), outk = Mathf.Clamp01((t - 0.75f) / 0.25f);
                Vector3 p = (follow != null ? follow.Hips.position + off : at) + Vector3.up * (0.25f * Kit.EaseOut(t));
                text.Place(p, cam, Mathf.Max(0.01f, pop * (1f - 0.9f * outk)), 1f - outk);
                if (cam != null) text.root.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, roll * (1f - 0.5f * t));
                return true;
            });
            f.end = () => text.Destroy();
        }

        // ---------------------------------------------------------------- speech bubbles

        static float WordWidth(string word, float height)
        {
            float w = 0f;
            foreach (char ch in word)
            {
                if (ch >= 0xAC00 && ch <= 0xD7A3) w += 0.95f;
                else if (ch == ' ') w += 0.3f;
                else if (ch == '…') w += 0.9f;
                else if (char.IsDigit(ch) || ch == '~') w += 0.55f;
                else w += 0.4f;
            }
            return w * height;
        }

        /// <summary>A bubble round <paramref name="word"/>: a squircle in the XY plane (seen from -Z), its tail out of the
        /// bottom-left; made once a word.</summary>
        Mesh BubbleMesh(string word, float height, out float width)
        {
            width = WordWidth(word, height) + height * 1.1f;
            string key = word + "|" + height.ToString("0.000");
            if (s4Bubbles.TryGetValue(key, out var m) && m != null) return m;
            float w = width, h = height * 1.75f;
            var pts = new List<Vector2>();
            const int N = 72;   // (5° a point)
            bool tail = false;
            for (int i = 0; i < N; i++)
            {
                float deg = i * 360f / N;
                if (deg > 250f && deg < 270f)
                {
                    if (!tail)
                    {
                        // The tail between the outline's points at 250° and 270°, straight out from the middle through
                        // their midpoint (so the fan from the middle stays inside the shape).
                        tail = true;
                        Vector2 a = Squircle(250f, w, h), b = Squircle(270f, w, h), mid = (a + b) * 0.5f;
                        pts.Add(mid * (1f + height * 0.7f / Mathf.Max(0.01f, mid.magnitude)));
                    }
                    continue;
                }
                pts.Add(Squircle(deg, w, h));
            }
            m = CmExtrude(pts.ToArray(), 0.03f, "Speech bubble");
            s4Bubbles[key] = m;
            return m;
        }

        static Vector2 Squircle(float deg, float w, float h)
        {
            float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            // Rounder than a box, flatter than an ellipse.
            return new Vector2(Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.6f) * w * 0.5f, Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.6f) * h * 0.5f);
        }

        Bubble4 MakeBubble(string word, float height)
        {
            var b = new Bubble4();
            b.shape = CmProp(BubbleMesh(word, height, out b.width), CmWhite, "Speech bubble", 0.016f);
            b.text = new Text3D(root, font, word, BubbleInk, height);
            return b;
        }

        void PlaceBubble(Bubble4 b, Vector3 at, Camera cam, float scale, float roll)
        {
            if (cam == null) return;
            if (scale <= 0.011f)
            {
                b.shape.Show(false);
                b.text.Place(at, cam, 0.001f, 0f);
                return;
            }
            Quaternion face = cam.transform.rotation * Quaternion.Euler(0f, 0f, roll);
            CmPlace(b.shape, at + cam.transform.forward * (0.03f * scale), face, Vector3.one * scale);
            b.text.Place(at - cam.transform.forward * (0.02f * scale), cam, scale, 1f);
            b.text.root.rotation = face;
        }

        void S4SayFx(SkillFxEvent e)
        {
            var who = e.target != null ? e.target : e.by;
            var words = RagdollPawn.Words4;
            if (who == null || font == null || e.count <= 0 || e.count >= words.Length) return;
            SayBubble(who, words[e.count], 1.35f);
        }

        /// <summary>A speech bubble over <paramref name="who"/>'s head, up to its right, its tail at the head: popping in,
        /// wobbling, rising a little, popping out.</summary>
        void SayBubble(RagdollPawn who, string word, float life, float height = 0.26f)
        {
            var b = MakeBubble(word, height);
            float wob = Random.value * 6f;
            var f = kit.Run(life, (fx, d) =>
            {
                if (who == null) return false;
                var cam = ViewCamera;
                if (cam == null) return true;
                float t = fx.age / life;
                float pop = Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.16f)), outk = Mathf.Clamp01((fx.age - (life - 0.14f)) / 0.14f);
                float s = Mathf.Max(0.01f, pop * (1f - outk));
                Vector3 at = DrawnHead(who) + cam.transform.up * (0.5f + 0.08f * t) + cam.transform.right * (0.42f + b.width * 0.5f);
                PlaceBubble(b, at, cam, s, 3f * Mathf.Sin(fx.age * 9f + wob));
                return true;
            });
            f.props.Add(b.shape);
            f.end = () => b.text.Destroy();
        }

        // ---------------------------------------------------------------- the hindrances

        void CcLookFx(SkillFxEvent e, float lvl)
        {
            var who = e.target != null ? e.target : e.by;
            if (who == null) return;
            var kind = (Cc4)e.count;
            if (ccLooks.TryGetValue(who, out var old))
            {
                old.until = Mathf.Min(old.until, Clock);   // (its look ends its own way)
                ccLooks.Remove(who);
            }
            if (kind == Cc4.None) return;
            float seconds = Mathf.Max(0.2f, e.size);
            var look = new CcLook { who = who, kind = kind, until = Clock + seconds };
            ccLooks[who] = look;
            switch (kind)
            {
                case Cc4.Flop: CcFlop(look, lvl); break;
                case Cc4.Dizzy: CcDaze(look, lvl); break;
                case Cc4.Tired: CcTired(look, lvl); break;
                case Cc4.Heavy: CcHeavy(look, lvl); break;
                case Cc4.BoundHop:
                case Cc4.BoundRoot: CcBound(look, lvl); break;
                case Cc4.Slippery: CcSlippery(look, lvl); break;
                case Cc4.Tiny: CcTiny(look, lvl); break;
                case Cc4.Flat: CcFlatLook(look, lvl); break;   // the user's own (PawnRushSkillFx.SetFourJY.cs)
                case Cc4.Frozen: CcFrozenLook(look, lvl); break;
            }
            if (kind != Cc4.Flop) CcLabel(look, seconds);   // (콰당! says itself)
        }

        /// <summary>Its name over the head in its colour, a bar under it running down with the time.</summary>
        void CcLabel(CcLook look, float seconds)
        {
            if (font == null) return;
            var who = look.who;
            Color c = CcColor(look.kind);
            var text = new Text3D(root, font, RagdollPawn.CcName(look.kind), c, 0.24f);
            var bar = new Kit.Strip(kit, new Kit.Palette { main = c, light = Color.white, deep = Kit.A(c * 0.7f, 1f), ink = BubbleInk })
            {
                core = Color.white, coreShare = 0.3f, inkShare = 0.32f,
            };
            var pts = new List<Vector3>(2);
            float total = Mathf.Max(0.2f, seconds);
            var f = kit.Run(total + 0.5f, (fx, d) =>
            {
                if (who == null) return false;
                float left = look.until - Clock;
                float outk = left < 0f ? Mathf.Clamp01(-left / 0.15f) : 0f;
                if (outk >= 1f) return false;
                var cam = ViewCamera;
                if (cam == null) return true;
                float s = Mathf.Max(0.01f, Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.16f)) * (1f - outk));
                if (S4cHidden(who)) s = 0.01f;   // (swallowed: out of sight)
                Vector3 at = DrawnHead(who) + Vector3.up * 0.7f;
                text.Place(at, cam, s, 1f);
                Vector3 right = cam.transform.right, up = cam.transform.up;
                float half = 0.36f * s, frac = Mathf.Clamp01(left / total);
                Vector3 l = at - up * (0.2f * s) - right * half;
                pts.Clear();
                pts.Add(l);
                pts.Add(l + right * (2f * half * Mathf.Max(0.03f, frac)));
                bar.Build(pts, i => 0.075f * s, kit.Eye);
                bar.Apply();
                return true;
            });
            f.strips.Add(bar);
            f.end = () => text.Destroy();
        }

        void CcFlop(CcLook look, float lvl)
        {
            var who = look.who;
            // The thud: a big "콰당!" off the body, dust out from under it.
            CmPoof(who.FeetPoint + Vector3.up * 0.1f, CmDust, 8, 1.1f, 0.42f, 0.5f, 0.25f);
            // (USER_SKILLS 5.2: one word at a time, beside the body; a skill's own word just put on it wins.)
            if (!S4cHidden(who) && !JyWordedRecently(who)) JyWord("콰당!", who.Hips.position + Vector3.up * 0.45f, CcColor(Cc4.Flop), who);
            Shake(0.05f, 8f * Frame);
            UltClip(UltClipId.Crack, 0.6f * lvl, 0.7f);
            // Out cold: stars round its head (ROUND1 3장: "머리 위에 별").
            CcStars(look, 4, 0.34f, 0.5f);
        }

        /// <summary>헤롱헤롱: stars going round over its head (ROUND1 3장: "머리 위에 별이 돌고").</summary>
        void CcDaze(CcLook look, float lvl)
        {
            UltClip(UltClipId.Wah, 0.45f * lvl, 1.5f);
            CcStars(look, 3, 0.28f, 0.7f);
        }

        /// <summary><paramref name="n"/> gold stars circling over its head (one turn in <paramref name="turn"/> s), each spinning
        /// and bobbing, as long as the hindrance lasts; at its end they pop away.</summary>
        void CcStars(CcLook look, int n, float radius, float turn)
        {
            var who = look.who;
            var stars = new Kit.Prop[n];
            for (int i = 0; i < n; i++) stars[i] = CmProp(S4StarMesh(), CmGold, "Cc star", 0.014f);
            bool popped = false;
            var f = kit.Run(Mathf.Max(0.3f, look.until - Clock) + 0.4f, (fx, d) =>
            {
                if (who == null) return false;
                float left = look.until - Clock;
                float k = GiantScaleOf(who);
                Vector3 head = DrawnHead(who);
                if (left < 0f)
                {
                    if (!popped)
                    {
                        popped = true;
                        CmPoof(head + Vector3.up * (0.32f * k), CmGold, 4, 0.4f * k, 0.14f, 0.3f, 0.4f);
                    }
                    return false;
                }
                bool hidden = S4cHidden(who);
                for (int i = 0; i < n; i++)
                {
                    if (hidden)
                    {
                        stars[i].Show(false);
                        continue;
                    }
                    float a = fx.age / turn * Mathf.PI * 2f + i * Mathf.PI * 2f / n;
                    Vector3 at = head + Vector3.up * ((0.32f + 0.04f * Mathf.Sin(fx.age * 9f + i * 2f)) * k)
                                 + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (radius * k);
                    float pop = Kit.Pop(fx.age - i * 0.04f, 1.2f);
                    CmPlace(stars[i], at, Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, fx.age * 220f + i * 40f), Vector3.one * (0.12f * k * Mathf.Max(0.01f, pop)));
                }
                return true;
            });
            foreach (var s in stars) f.props.Add(s);
        }

        void CcTired(CcLook look, float lvl)
        {
            var who = look.who;
            UltClip(UltClipId.Wah, 0.4f * lvl, 0.7f);
            float nextDrop = 0.05f, nextHuff = 0.3f;
            kit.Run(Mathf.Max(0.3f, look.until - Clock) + 0.1f, (fx, d) =>
            {
                if (who == null || Clock > look.until) return false;
                kit.Coil(who, 0.6f);   // slumped (ROUND1 3장: "처진 어깨")
                Vector3 head = DrawnHead(who), face = FlatDir(who.Facing);
                if (fx.age >= nextDrop)
                {
                    // Sweat flying off the top of its head.
                    nextDrop = fx.age + 0.3f;
                    Vector3 side = Vector3.Cross(Vector3.up, face) * (Random.value < 0.5f ? -1f : 1f);
                    kit.Drops(head + Vector3.up * 0.18f, (Vector3.up * 1.3f + side * 0.7f + Random.insideUnitSphere * 0.25f).normalized, CmSweat, 1, 3.4f);
                }
                if (fx.age >= nextHuff)
                {
                    // Puffing: a little cloud of breath out of its face.
                    nextHuff = fx.age + 0.7f;
                    CmPoof(head + face * 0.24f - Vector3.up * 0.06f, CmSteam, 2, 0.32f, 0.16f, 0.5f, 0.15f, face * 1.5f);
                }
                return true;
            });
        }

        void CcHeavy(CcLook look, float lvl)
        {
            var who = look.who;
            kit.AddSquash(who, Kit.SquashKind.Flatten, 0.1f);
            kit.DustRing(who.FeetPoint + Vector3.up * 0.05f, 8, 0.7f, CmDust, 0.3f, 0.5f);
            UltClip(UltClipId.SubDrop, 0.6f * lvl, 1.1f);
            float nextStep = 0.2f;
            var iron = S4IronMaterial();
            kit.Run(Mathf.Max(0.3f, look.until - Clock) + 0.1f, (fx, d) =>
            {
                if (who == null || Clock > look.until) return false;
                // Cast iron all over (ROUND1 3장: "몸이 무쇠 색이 되고"; drawn baked, PawnRushSkillFx.PromotionRun.cs).
                if (iron != null) WantGiant(who, 1f, 8f, iron);
                // Every heavy step thumps up a little dust (while it moves).
                if (fx.age >= nextStep && Flat(who.bodies[0].linearVelocity).sqrMagnitude > 1f)
                {
                    nextStep = fx.age + 0.3f;
                    CmPoof(who.FeetPoint + Vector3.up * 0.05f, CmDust, 3, 0.45f, 0.22f, 0.4f, 0.15f);
                }
                return true;
            });
        }

        void CcBound(CcLook look, float lvl)
        {
            var who = look.who;
            UltClip(UltClipId.Tap, 0.6f * lvl, 0.8f);
            CmPoof(who.FeetPoint + Vector3.up * 0.1f, CmRope, 4, 0.45f, 0.22f, 0.35f, 0.2f);
            var loop1 = CmProp(kit.meshTorus, CmRope, "Bound rope", 0.015f);
            var loop2 = CmProp(kit.meshTorus, CmRope, "Bound rope", 0.015f);
            var knot = CmProp(kit.meshBall, CmRope, "Bound knot", 0.012f);
            var f = kit.Run(Mathf.Max(0.3f, look.until - Clock) + 0.4f, (fx, d) =>
            {
                if (who == null) return false;
                float gone = Clock - look.until;
                if (gone > 0.2f)
                {
                    CmPoof(who.FeetPoint + Vector3.up * 0.15f, CmRope, 4, 0.5f, 0.2f, 0.35f, 0.3f);
                    return false;
                }
                float k = GiantScaleOf(who);
                Vector3 feet = who.FeetPoint, hips = who.Hips.position;
                Vector3 up = (hips - feet).sqrMagnitude > 0.01f ? (hips - feet).normalized : Vector3.up;
                Quaternion lay = Quaternion.FromToRotation(Vector3.up, up);
                // Snapped tight from wide, then held; slipping loose at the end.
                float snap = 1.5f - 0.5f * Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.14f));
                float loose = gone > 0f ? 1f + gone * 3f : 1f;
                CmPlace(loop1, feet + up * (0.15f * k), lay * Quaternion.Euler(6f, fx.age * 30f, 0f), Vector3.one * (0.19f * snap * loose * k));
                CmPlace(loop2, feet + up * (0.27f * k), lay * Quaternion.Euler(-6f, -fx.age * 30f, 0f), Vector3.one * (0.17f * snap * loose * k));
                CmPlace(knot, feet + up * (0.21f * k) + FlatDir(who.Facing) * (0.17f * k), Quaternion.identity, Vector3.one * (0.075f * k));
                return true;
            });
            f.props.Add(loop1);
            f.props.Add(loop2);
            f.props.Add(knot);
        }

        void CcSlippery(CcLook look, float lvl)
        {
            var who = look.who;
            UltClip(UltClipId.Shimmer, 0.45f * lvl, 1.6f);
            float nextBubble = 0f, nextGlint = 0.1f;
            kit.Run(Mathf.Max(0.3f, look.until - Clock) + 0.1f, (fx, d) =>
            {
                if (who == null || Clock > look.until) return false;
                Vector3 feet = who.FeetPoint;
                if (fx.age >= nextBubble)
                {
                    nextBubble = fx.age + 0.15f;
                    Vector2 r = Random.insideUnitCircle * 0.3f;
                    S4SoapBubble(feet + new Vector3(r.x, 0.1f, r.y));
                }
                if (fx.age >= nextGlint)
                {
                    nextGlint = fx.age + 0.32f;
                    Vector2 r = Random.insideUnitCircle * 0.25f;
                    CmFlash(feet + new Vector3(r.x, 0.08f, r.y), 0.12f, 9);
                }
                return true;
            });
        }

        void S4SoapBubble(Vector3 at)
        {
            var b = CmProp(kit.meshBall, CmSoap, "Soap bubble", 0.008f);
            float life = Random.Range(0.6f, 1f), size = Random.Range(0.07f, 0.15f), wob = Random.value * 6f;
            Vector3 drift = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.5f, 0.9f), Random.Range(-0.3f, 0.3f));
            var f = kit.Run(life, (fx, d) =>
            {
                if (fx.age >= life * 0.95f)
                {
                    // Pop.
                    CmFlash(at + drift * fx.age, size * 1.2f, 6, CmSoap);
                    return false;
                }
                Vector3 p = at + drift * fx.age + new Vector3(Mathf.Sin(fx.age * 7f + wob), 0f, Mathf.Cos(fx.age * 6f + wob)) * 0.04f;
                CmPlace(b, p, Quaternion.identity, Vector3.one * (size * Kit.EaseOutBack(Mathf.Clamp01(fx.age / 0.12f))));
                return true;
            });
            f.props.Add(b);
        }

        void CcTiny(CcLook look, float lvl)
        {
            var who = look.who;
            float scale = Params != null ? Mathf.Clamp(Params.ccTinyScale, 0.2f, 1f) : 0.5f;
            CmPoof(who.Hips.position, CmBump, 8, 0.8f, 0.35f, 0.45f, 0.4f);
            CmFlash(who.Hips.position, 0.6f, 8);
            UltClip(UltClipId.Shimmer, 0.7f * lvl, 2.2f);
            bool ended = false;
            kit.Run(Mathf.Max(0.3f, look.until - Clock) + 0.3f, (fx, d) =>
            {
                if (who == null) return false;
                if (Clock > look.until)
                {
                    if (!ended)
                    {
                        ended = true;
                        CmPoof(who.Hips.position, CmBump, 6, 0.7f, 0.3f, 0.4f, 0.4f);
                        UltClip(UltClipId.Shimmer, 0.5f * S3Heard(who.Hips.position), 1.2f);
                    }
                    return false;
                }
                WantGiant(who, scale, 8f);   // (drawn smaller from its feet; PawnRushSkillFx.PromotionRun.cs)
                return true;
            });
        }

        void S4HopFx(SkillFxEvent e, float lvl)
        {
            CmPoof(e.at + Vector3.up * 0.06f, CmDust, 4, 0.45f, 0.22f, 0.35f, 0.2f);
            var who = e.target != null ? e.target : e.by;
            if (who != null) kit.AddSquash(who, Kit.SquashKind.Spring);
            UltClip(UltClipId.Tap, 0.5f * lvl, 1.5f);
        }

        // ---------------------------------------------------------------- 폰

        void S4TripFx(SkillFxEvent e, float lvl)
        {
            if (e.count == 1)
            {
                // Caught on a foot: a white pop at its ankle and dust.
                CmFlash(e.at + Vector3.up * 0.12f, 0.3f, 7);
                CmPoof(e.at + Vector3.up * 0.08f, CmDust, 5, 0.6f, 0.26f, 0.4f, 0.25f);
                UltClip(UltClipId.Tap, 0.8f * lvl, 0.7f);
                return;
            }
            var by = e.by;
            if (by == null) return;
            var p = S3Colors(by);
            float life = Mathf.Max(0.2f, e.size), reach = Params != null ? Params.tripReach : 2f;
            // Both legs shot out sideways like rubber, a round shoe on each end, a puff where each lands.
            var legL = new Kit.Strip(kit, p) { core = p.main, coreShare = 0.2f, inkShare = 0.36f };
            var legR = new Kit.Strip(kit, p) { core = p.main, coreShare = 0.2f, inkShare = 0.36f };
            var shoeL = CmProp(kit.meshBall, CmShoe, "Trip shoe", 0.016f);
            var shoeR = CmProp(kit.meshBall, CmShoe, "Trip shoe", 0.016f);
            var pts = new List<Vector3>();
            kit.AddSquash(by, Kit.SquashKind.Wide);
            UltClip(UltClipId.Whoosh, 0.6f * lvl, 1.6f);
            bool puffed = false;
            var f = kit.Run(life + 0.15f, (fx, d) =>
            {
                if (by == null) return false;
                float t = fx.age;
                float stretch = t < life ? Kit.EaseOutBack(Mathf.Clamp01(t / 0.12f)) : 1f - Mathf.Clamp01((t - life) / 0.15f);
                Vector3 feet = by.FeetPoint + Vector3.up * 0.14f, right = Vector3.Cross(Vector3.up, FlatDir(by.Facing));
                float len = reach * Mathf.Max(0.02f, stretch);
                for (int s = 0; s < 2; s++)
                {
                    var leg = s == 0 ? legL : legR;
                    var shoe = s == 0 ? shoeL : shoeR;
                    float side = s == 0 ? -1f : 1f;
                    pts.Clear();
                    for (int i = 0; i <= 6; i++)
                    {
                        float k = i / 6f;
                        // A little sag and a wiggle along the leg.
                        pts.Add(feet + right * (side * len * k) + Vector3.up * (0.05f * Mathf.Sin(k * Mathf.PI) + 0.02f * Mathf.Sin(t * 30f + k * 6f)));
                    }
                    leg.Build(pts, i => 0.17f, kit.Eye);
                    leg.Apply();
                    CmPlace(shoe, pts[pts.Count - 1] + Vector3.up * 0.03f, Quaternion.LookRotation(right * side), new Vector3(0.2f, 0.15f, 0.3f) * Mathf.Max(0.2f, stretch));
                }
                if (!puffed && t > 0.1f)
                {
                    puffed = true;
                    CmPoof(feet + right * len, CmDust, 3, 0.35f, 0.2f, 0.35f, 0.2f);
                    CmPoof(feet - right * len, CmDust, 3, 0.35f, 0.2f, 0.35f, 0.2f);
                }
                return true;
            });
            f.strips.Add(legL);
            f.strips.Add(legR);
            f.props.Add(shoeL);
            f.props.Add(shoeR);
        }

        void S4BrickFx(SkillFxEvent e, float lvl)
        {
            if (e.count == 0)
            {
                S4BrickDrop(e);
                return;
            }
            // Down, on a head (1) or the floor (2): the thrower's oldest brick still in the air.
            Brick4Fx b = null;
            foreach (var x in s4Bricks)
                if (x.by == e.by && x.state < 2)
                {
                    b = x;
                    break;
                }
            if (e.count == 1)
            {
                if (b != null)
                {
                    b.state = 2;
                    b.on = e.target;
                    b.landed = Clock;
                    b.pos = e.at + Vector3.up * 0.25f;
                }
                if (e.target != null) S4Bump(e.target, Params != null ? Params.brickDizzy : 2f);
                CmFlash(e.at + Vector3.up * 0.22f, 0.4f, 7);
                UltClip(UltClipId.Tock, 0.9f * lvl, 0.8f);
                Shake(0.04f, 8f * Frame);
                return;
            }
            if (b != null)
            {
                b.state = 3;
                b.landed = Clock;
                b.pos = e.at;
            }
            CmPoof(e.at + Vector3.up * 0.1f, CmDust, 6, 0.8f, 0.3f, 0.45f, 0.25f);
            UltClip(UltClipId.Crack, 0.6f * lvl, 1.2f);
        }

        void S4BrickDrop(SkillFxEvent e)
        {
            var b = new Brick4Fx { by = e.by, at = e.at, pos = e.at, born = Clock, delay = Mathf.Max(0.05f, e.size), fall = Params != null ? Params.brickFall : 0.3f };
            s4Bricks.Add(b);
            var brick = CmProp(S4BrickMesh(), CmBrickRed, "Brick", 0.014f);
            Vector3 floor = FloorBelow(e.at, 14f);
            float spin = Random.Range(0f, 360f), side = Random.value < 0.5f ? -1f : 1f;
            CmPoof(e.at, CmDust, 5, 0.5f, 0.25f, 0.35f, 0.3f);
            var f = kit.Run(b.delay + b.fall + 3f, (fx, d) =>
            {
                float age = Clock - b.born;
                switch (b.state)
                {
                    case 0:
                    case 1:
                    {
                        // Hovering (a wobble), then dropping; a shadow growing on the floor under it.
                        float drop = Mathf.Clamp01((age - b.delay) / Mathf.Max(0.05f, b.fall));
                        if (age >= b.delay) b.state = 1;
                        if (age > b.delay + b.fall + 1f) return false;   // (no word from the host)
                        Vector3 at = b.at + Vector3.up * (age < b.delay ? 0.05f * Mathf.Sin(age * 16f) : 0f);
                        at.y = Mathf.Lerp(at.y, floor.y + 0.1f, drop * drop);
                        float pop = Kit.EaseOutBack(Mathf.Clamp01(age / 0.15f));
                        CmPlace(brick, at, Quaternion.Euler(6f * Mathf.Sin(age * 11f), spin, 8f * Mathf.Sin(age * 9f)), Vector3.one * Mathf.Max(0.02f, pop));
                        DrawShade(floor + Vector3.up * 0.02f, Mathf.Lerp(0.22f, 0.4f, Mathf.Max(drop, 0.25f * Mathf.Clamp01(age / b.delay))), Mathf.Lerp(0.18f, 0.42f, drop));
                        return true;
                    }
                    case 2:
                    {
                        // Bonk: off the head it bounces, tumbling, and goes in a poof.
                        float t = Clock - b.landed;
                        if (t > 0.6f)
                        {
                            CmPoof(b.pos, CmBrickRed, 4, 0.4f, 0.2f, 0.3f, 0.3f);
                            return false;
                        }
                        Vector3 away = b.on != null ? Vector3.Cross(Vector3.up, FlatDir(b.on.Facing)) * side : Vector3.right * side;
                        Vector3 at = b.pos + away * (2f * t) + Vector3.up * (3.2f * t - 7f * t * t);
                        CmPlace(brick, at, Quaternion.Euler(t * 700f, spin, t * 300f * side), Vector3.one);
                        return true;
                    }
                    default:
                    {
                        // Thud on the floor: it lies there a moment, then sinks away.
                        float t = Clock - b.landed;
                        if (t > 0.8f) return false;
                        float sink = Mathf.Clamp01((t - 0.5f) / 0.3f);
                        CmPlace(brick, b.pos + Vector3.up * (0.1f - 0.12f * sink), Quaternion.Euler(0f, spin, 0f), Vector3.one * (1f - sink));
                        return true;
                    }
                }
            });
            f.props.Add(brick);
            f.end = () => s4Bricks.Remove(b);
        }

        /// <summary>A pink bump growing out of the top of its head (and back) over <paramref name="seconds"/>.</summary>
        void S4Bump(RagdollPawn who, float seconds)
        {
            var bump = CmProp(kit.meshBall, CmBump, "Bump", 0.012f);
            var f = kit.Run(seconds + 0.3f, (fx, d) =>
            {
                if (who == null) return false;
                float grow = Kit.EaseOutBack(Mathf.Clamp01((fx.age - 0.05f) / 0.2f)), shrink = Mathf.Clamp01((fx.age - seconds) / 0.3f);
                float k = GiantScaleOf(who);
                Vector3 up = HeadUp(who);
                CmPlace(bump, DrawnHead(who) + up * (0.19f * k), Quaternion.FromToRotation(Vector3.up, up), new Vector3(0.15f, 0.12f, 0.15f) * (k * Mathf.Max(0.01f, grow) * (1f - shrink)));
                return true;
            });
            f.props.Add(bump);
        }

        void S4PitFx(SkillFxEvent e, float lvl)
        {
            float size = Params != null ? Params.pitSize : 1.2f;
            if (e.count == 0)
            {
                var pit = new Pit4Fx { by = e.by, at = e.at, until = Clock + Mathf.Max(0.3f, e.size) };
                s4Pits.Add(pit);
                CmPoof(e.at + Vector3.up * 0.1f, CmDirt, 8, size * 0.8f, 0.32f, 0.45f, 0.5f);
                UltClip(UltClipId.Crack, 0.5f * lvl, 0.6f);
                var hole = CmProp(CmPuddleMesh(), CmHole, "Pit hole", 0.012f);
                var clods = new Kit.Prop[6];
                for (int i = 0; i < clods.Length; i++) clods[i] = CmProp(CmRockMesh(), CmDirt, "Pit clod", 0.012f);
                float spin = Random.Range(0f, 360f), born = Clock;
                var f = kit.Run(Mathf.Max(0.3f, e.size) + 1f, (fx, d) =>
                {
                    float grow = Kit.EaseOutBack(Mathf.Clamp01((Clock - born) / 0.2f)), shut = Mathf.Clamp01((Clock - pit.until) / 0.25f);
                    if (shut >= 1f) return false;
                    float s = size * grow * (1f - shut);
                    CmPlace(hole, pit.at + Vector3.up * 0.015f, Quaternion.Euler(0f, spin, 0f), new Vector3(Mathf.Max(0.01f, s), 1f, Mathf.Max(0.01f, s)));
                    for (int i = 0; i < clods.Length; i++)
                    {
                        float a = (spin + i * 60f + 17f * Mathf.Sin(i * 2.3f)) * Mathf.Deg2Rad;
                        Vector3 rim = pit.at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (s * 0.56f);
                        CmPlace(clods[i], rim + Vector3.up * 0.04f, Quaternion.Euler(i * 40f, i * 70f, 0f),
                            new Vector3(0.26f, 0.15f, 0.22f) * (Mathf.Max(0.01f, grow) * (1f - shut) * (0.8f + 0.1f * (i % 3))));
                    }
                    return true;
                });
                f.props.Add(hole);
                foreach (var c in clods) f.props.Add(c);
                f.end = () => s4Pits.Remove(pit);
                return;
            }
            // Caught (1) or closed (2): the digger's nearest open pit.
            Pit4Fx best = null;
            float bd = 1.5f;
            foreach (var p in s4Pits)
            {
                if (p.by != e.by || p.state != 0) continue;
                float dd = (p.at - e.at).magnitude;
                if (dd < bd)
                {
                    bd = dd;
                    best = p;
                }
            }
            if (best != null)
            {
                best.state = e.count;
                best.until = Mathf.Min(best.until, Clock);
            }
            if (e.count == 2)
            {
                CmPoof(e.at + Vector3.up * 0.08f, CmDirt, 5, size * 0.6f, 0.26f, 0.4f, 0.3f);
                return;
            }
            if (e.target != null) S4PitMound(e.target, e.at, Mathf.Max(0.2f, e.size), lvl);
        }

        /// <summary>Caught in the pit: dirt bursts up and a mound rises round it to the waist; out it pops at the end.</summary>
        void S4PitMound(RagdollPawn who, Vector3 at, float hold, float lvl)
        {
            var mound = CmProp(S4MoundMesh(), CmDirt, "Pit mound", 0.014f);
            CmPoof(at + Vector3.up * 0.2f, CmDirt, 9, 0.9f, 0.34f, 0.5f, 0.9f);
            kit.AddSquash(who, Kit.SquashKind.Down);
            Shake(0.05f, 8f * Frame);
            UltClip(UltClipId.SubDrop, 0.7f * lvl, 0.9f);
            float spin = Random.Range(0f, 360f), born = Clock;
            bool popped = false;
            var f = kit.Run(hold + 0.6f, (fx, d) =>
            {
                if (who == null) return false;
                float t = Clock - born;
                float rise = Kit.EaseOutBack(Mathf.Clamp01(t / 0.16f)), sink = Mathf.Clamp01((t - hold) / 0.3f);
                if (!popped && t >= hold)
                {
                    popped = true;
                    CmPoof(at + Vector3.up * 0.35f, CmDirt, 8, 0.8f, 0.3f, 0.45f, 1.1f);
                    UltClip(UltClipId.Tap, 0.6f * S3Heard(at), 1.3f);
                }
                if (sink >= 1f) return false;
                float k = GiantScaleOf(who);
                Vector3 c = new Vector3(who.Hips.position.x, at.y, who.Hips.position.z);
                CmPlace(mound, c, Quaternion.Euler(0f, spin, 0f), new Vector3(1f + 0.15f * sink, Mathf.Max(0.02f, rise * (1f - sink)), 1f + 0.15f * sink) * Mathf.Max(0.5f, k));
                return true;
            });
            f.props.Add(mound);
        }

        // ---------------------------------------------------------------- 나이트

        void S4LeapFx(SkillFxEvent e, float lvl)
        {
            if (e.by == null) return;
            var p = S3Colors(e.by);
            float flight = Mathf.Max(0.2f, e.size), reach = Params != null ? Params.forkReach : 2f;
            Vector3 to = e.normal;
            CmPoof(e.at + Vector3.up * 0.1f, CmDust, 6, 0.8f, 0.3f, 0.4f, 0.25f);
            CmStreak(e.by, p, flight, 0.18f, 0.18f);
            kit.AddSquash(e.by, Kit.SquashKind.Spring);
            kit.FlashRing(to + Vector3.up * 0.04f, reach, p);
            UltClip(UltClipId.Whoosh, 0.7f * lvl, 0.9f);
            // Where it comes down: the fork's reach flashing again halfway through the fall.
            bool again = false;
            kit.Run(flight, (fx, d) =>
            {
                if (!again && fx.age >= flight * 0.5f)
                {
                    again = true;
                    kit.FlashRing(to + Vector3.up * 0.04f, reach, p);
                }
                return true;
            });
        }

        void S4BonkFx(SkillFxEvent e, float lvl)
        {
            if (e.count == 2)
            {
                // Two heads knocked together: a pop between them, "딱!", stars of drops, both squashed.
                Vector3 mid = e.at;
                CmFlash(mid, 0.6f, 9);
                kit.HalfRing(mid, Vector3.up, CmGold, 0.55f, 0.18f);
                kit.Drops(mid, Vector3.up, CmGold, 6, 7f);
                PopWord("딱!", mid + Vector3.up * 0.4f, Kit.Hex("#FFE14D"), 0.46f, 0.85f, null, -8f);
                if (e.target != null) kit.AddSquash(e.target, Kit.SquashKind.Bump);
                var other = PawnNear(e.normal, 0.6f, e.target);
                if (other != null) kit.AddSquash(other, Kit.SquashKind.Bump);
                HitStop(5f * Frame);
                Shake(0.07f, 10f * Frame);
                UltClip(UltClipId.Tock, lvl, 1.25f);
                UltClip(UltClipId.Crack, 0.6f * lvl, 1.6f);
                return;
            }
            // One: stomped on the head.
            if (e.target != null)
            {
                CmSmack(e.target, e.at + Vector3.up * 0.15f, Vector3.down, S3Colors(e.by));
                kit.AddSquash(e.target, Kit.SquashKind.Flatten, 0.25f);
            }
            CmPoof(e.at - Vector3.up * 0.6f, CmDust, 5, 0.6f, 0.26f, 0.4f, 0.25f);
            UltClip(UltClipId.Tock, 0.8f * lvl, 0.9f);
        }

        static RagdollPawn PawnNear(Vector3 head, float within, RagdollPawn not)
        {
            RagdollPawn best = null;
            float bd = within;
            foreach (var o in RagdollPawn.All)
            {
                if (o == null || o == not) continue;
                float d = (o.bodies[(int)BodyId.Head].position - head).magnitude;
                if (d < bd)
                {
                    bd = d;
                    best = o;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- 비숍

        void S4SermonFx(SkillFxEvent e, float lvl)
        {
            if (e.target == null) return;
            var who = e.target;
            float seconds = Mathf.Max(0.3f, e.size);
            var mesh = SkillMeshes.Piece(PieceKind.Bishop);
            float tall = Mathf.Max(0.05f, mesh.bounds.size.y);
            var mini = CmProp(mesh, Kit.Bishop, "Sermon bishop", 0.012f);   // (purple: it reads on any floor)
            Bubble4 blah = font != null ? MakeBubble("블라블라~", 0.14f) : null, ahem = font != null ? MakeBubble("어흠!", 0.14f) : null;
            Vector3 from = e.at;
            float born = Clock;
            const float Hop = 0.35f;
            bool sat = false;
            UltClip(UltClipId.Shimmer, 0.5f * lvl, 1.4f);
            var f = kit.Run(seconds + 0.6f, (fx, d) =>
            {
                if (who == null) return false;
                float t = Clock - born;
                float k = GiantScaleOf(who);
                var view = ViewCamera;
                Vector3 up = HeadUp(who), face = FlatDir(who.Facing);
                // On the shoulder this screen sees (beside the head, a little toward the camera), turned to its ear.
                Vector3 right = view != null ? FlatDir(view.transform.right) : Vector3.Cross(Vector3.up, face);
                Vector3 toCam = view != null ? FlatDir(view.transform.position - DrawnHead(who)) : face;
                Vector3 seat = DrawnHead(who) - up * (0.1f * k) + right * (0.26f * k) + toCam * (0.1f * k);
                Vector3 at;
                if (t < Hop)
                {
                    float h = t / Hop;
                    at = Vector3.Lerp(from, seat, h) + Vector3.up * (4f * 0.8f * h * (1f - h));
                }
                else
                {
                    if (!sat)
                    {
                        sat = true;
                        CmPoof(seat, CmWhite, 3, 0.25f, 0.12f, 0.3f, 0.2f);
                    }
                    // Preaching: bobbing and leaning in.
                    at = seat + Vector3.up * (0.03f * Mathf.Abs(Mathf.Sin(t * 9f)));
                }
                float gone = t - seconds;
                if (gone > 0f)
                {
                    // Hops off and is gone.
                    float g = gone / 0.4f;
                    if (g >= 1f)
                    {
                        CmPoof(at, CmWhite, 4, 0.3f, 0.15f, 0.3f, 0.3f);
                        return false;
                    }
                    at += right * (0.6f * g) + Vector3.up * (1.2f * g - 1.6f * g * g);
                }
                Vector3 toEar = DrawnHead(who) - at;
                Quaternion look = Quaternion.LookRotation(FlatDir(toEar), Vector3.up) * Quaternion.Euler(10f * Mathf.Sin(t * 9f), 0f, 0f);
                float s = 0.38f * k / tall * Kit.EaseOutBack(Mathf.Clamp01(t / 0.2f));
                CmPlace(mini, at, look, Vector3.one * Mathf.Max(0.001f, s));
                var cam = ViewCamera;
                if (blah != null && cam != null)
                {
                    // A little bubble over it: "블라블라~" and "어흠!" by turns.
                    bool talking = t > Hop && gone < 0f;
                    bool first = Mathf.Repeat(t - Hop, 1.6f) < 1.05f;
                    Vector3 over = at + Vector3.up * (0.42f * k) + cam.transform.right * 0.2f;
                    float pop = talking ? Kit.EaseOutBack(Mathf.Clamp01((Mathf.Repeat(t - Hop, 1.6f) - (first ? 0f : 1.05f)) / 0.12f)) : 0f;
                    PlaceBubble(blah, over + cam.transform.right * (blah.width * 0.5f), cam, talking && first ? pop : 0f, 4f * Mathf.Sin(t * 7f));
                    PlaceBubble(ahem, over + cam.transform.right * (ahem.width * 0.5f), cam, talking && !first ? pop : 0f, -4f * Mathf.Sin(t * 7f));
                }
                return true;
            });
            f.props.Add(mini);
            if (blah != null)
            {
                f.props.Add(blah.shape);
                f.props.Add(ahem.shape);
                f.end = () =>
                {
                    blah.text.Destroy();
                    ahem.text.Destroy();
                };
            }
        }

        // ---------------------------------------------------------------- 룩

        void S4ChargeFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            switch (e.count)
            {
                case 0:
                {
                    // Head down like a bull: a crouch, two snorts of steam.
                    if (by == null) return;
                    float bow = Mathf.Max(0.1f, e.size);
                    Snort(by);
                    bool second = false;
                    kit.Run(bow, (fx, d) =>
                    {
                        if (by == null) return false;
                        kit.Coil(by, Mathf.Clamp01(fx.age / bow));
                        if (!second && fx.age > bow * 0.55f)
                        {
                            second = true;
                            Snort(by);
                        }
                        return true;
                    });
                    UltClip(UltClipId.Wind, 0.5f * lvl, 1.6f);
                    break;
                }
                case 1:
                {
                    // Off: streaks, dust off its heels the whole way.
                    if (by == null) return;
                    float run = Mathf.Max(0.2f, e.size);
                    CmStreak(by, S3Colors(by), run, 0.24f, 0.2f);
                    CmSkid(by, run, 0.06f);
                    kit.AddSquash(by, Kit.SquashKind.Spring);
                    UltClip(UltClipId.Castle, 0.7f * lvl, 0.9f);
                    UltClip(UltClipId.Whoosh, 0.6f * lvl, 0.7f);
                    break;
                }
                case 2:
                    // Into a wall: a pop, "쾅!", rubble; the rook squashed flat against it.
                    CmFlash(e.at, 0.7f, 9);
                    kit.HalfRing(e.at, -FlatDir(e.dir) + Vector3.up * 0.3f, CmWhite, 0.6f, 0.2f);
                    CmPoof(e.at, CmRock, 7, 0.9f, 0.34f, 0.45f, 0.4f);
                    PopWord("쾅!", e.at + Vector3.up * 0.6f, Kit.Hex("#FF6A3D"), 0.55f, 0.85f);
                    if (by != null) kit.AddSquash(by, Kit.SquashKind.Wide);
                    Shake(0.09f, 12f * Frame);
                    HitStop(5f * Frame);
                    UltClip(UltClipId.Crack, 0.9f * lvl, 0.6f);
                    break;
                default:
                    CmPoof(e.at + Vector3.up * 0.1f, CmDust, 6, 0.8f, 0.3f, 0.45f, 0.25f);
                    break;
            }
        }

        void Snort(RagdollPawn by)
        {
            Vector3 head = DrawnHead(by), face = FlatDir(by.Facing), side = Vector3.Cross(Vector3.up, face);
            for (int s = -1; s <= 1; s += 2)
                CmPoof(head + face * 0.2f + side * (0.08f * s) - Vector3.up * 0.08f, CmSteam, 2, 0.4f, 0.18f, 0.45f, 0.1f, face + side * (0.6f * s));
        }

        void S4CapFx(SkillFxEvent e, float lvl)
        {
            if (e.target == null) return;
            var who = e.target;
            float seconds = Mathf.Max(0.3f, e.size), born = Clock;
            var stone = CmProp(S4CapMesh(), CmRock, "Wall stone", 0.014f);
            bool landed = false;
            UltClip(UltClipId.Whoosh, 0.4f * lvl, 0.8f);
            var f = kit.Run(seconds + 0.8f, (fx, d) =>
            {
                if (who == null) return false;
                float t = Clock - born, k = GiantScaleOf(who);
                Vector3 head = DrawnHead(who), up = HeadUp(who);
                float drop = Mathf.Clamp01(t / 0.18f);
                if (!landed && drop >= 1f)
                {
                    // 턱: on.
                    landed = true;
                    CmFlash(head + up * (0.25f * k), 0.3f, 6);
                    CmPoof(head + up * (0.2f * k), CmRock, 4, 0.45f, 0.2f, 0.35f, 0.3f);
                    kit.AddSquash(who, Kit.SquashKind.Down);
                    UltClip(UltClipId.Tock, 0.9f * S3Heard(head), 0.7f);
                }
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.LookRotation(FlatDir(who.Facing), Vector3.up) * Quaternion.Euler(0f, 0f, 4f * Mathf.Sin(t * 5f));
                Vector3 at = head + up * (0.18f * k) + Vector3.up * (1.1f * (1f - drop * drop));
                float gone = t - seconds;
                if (gone > 0f)
                {
                    // Off it tumbles, and is gone.
                    float g = gone / 0.5f;
                    if (g >= 1f)
                    {
                        CmPoof(at, CmRock, 4, 0.4f, 0.2f, 0.3f, 0.3f);
                        return false;
                    }
                    at += FlatDir(who.Facing) * (-0.5f * g) + Vector3.up * (0.6f * g - 1.6f * g * g);
                    rot *= Quaternion.Euler(-200f * g, 0f, 60f * g);
                }
                CmPlace(stone, at, rot, Vector3.one * k);
                return true;
            });
            f.props.Add(stone);
        }

        // ---------------------------------------------------------------- 퀸

        void S4CrownFx(SkillFxEvent e, float lvl)
        {
            if (e.count == 0)
            {
                var c = new Crown4Fx { by = e.by, from = e.at, to = e.normal, born = Clock, flight = Mathf.Max(0.1f, e.size) };
                s4Crowns.Add(c);
                var crown = CmProp(S4CrownMesh(), CmGold, "Crown", 0.012f);
                UltClip(UltClipId.Whoosh, 0.6f * lvl, 1.3f);
                float nextGlint = 0f;
                var f = kit.Run(c.flight + 8f, (fx, d) =>
                {
                    switch (c.state)
                    {
                        case 0:
                        {
                            // Flying along the host's own arc, spinning, glinting.
                            float t = Mathf.Clamp01((Clock - c.born) / c.flight);
                            if (Clock - c.born > c.flight + 0.5f) return false;   // (no word from the host)
                            Vector3 at = RagdollPawn.CrownArc(c.from, c.to, t);
                            CmPlace(crown, at, Quaternion.Euler(25f, fx.age * 720f, 0f), Vector3.one * 1.3f);
                            if (fx.age >= nextGlint)
                            {
                                nextGlint = fx.age + 0.07f;
                                CmPoof(at, CmGold, 1, 0.2f, 0.1f, 0.3f, 0.1f);
                            }
                            return true;
                        }
                        case 1:
                        {
                            // On a head: sitting there crooked while the weight lasts.
                            var who = c.on;
                            if (who == null) return false;
                            float gone = Clock - c.until, k = GiantScaleOf(who);
                            Vector3 up = HeadUp(who);
                            if (gone > 0.3f)
                            {
                                CmPoof(DrawnHead(who) + up * (0.25f * k), CmGold, 5, 0.4f, 0.2f, 0.35f, 0.4f);
                                return false;
                            }
                            float sat = Kit.EaseOutBack(Mathf.Clamp01((Clock - c.landed) / 0.15f));
                            Quaternion rot = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.LookRotation(FlatDir(who.Facing), Vector3.up)
                                * Quaternion.Euler(0f, 0f, 14f + 4f * Mathf.Sin(fx.age * 6f));
                            // Big on the head (a pawn's head is about 0.4 m round), sitting low on it.
                            Vector3 at = DrawnHead(who) + up * (0.07f * k) + Vector3.up * (0.25f * (1f - sat));
                            float s = 1.7f * k * (gone > 0f ? 1f - gone / 0.3f : 1f);
                            CmPlace(crown, at, rot, Vector3.one * Mathf.Max(0.01f, s));
                            return true;
                        }
                        default:
                        {
                            // Missed: down it bounces, rolling, gone in a poof.
                            float t = (Clock - c.landed) / 0.5f;
                            if (t >= 1f)
                            {
                                CmPoof(c.to, CmGold, 5, 0.45f, 0.22f, 0.35f, 0.3f);
                                return false;
                            }
                            Vector3 at = c.to + Vector3.up * (0.25f * Mathf.Abs(Mathf.Sin(t * Mathf.PI * 2f)) * (1f - t));
                            CmPlace(crown, at, Quaternion.Euler(70f * t, fx.age * 200f, 30f * t), Vector3.one * 1.3f);
                            return true;
                        }
                    }
                });
                f.props.Add(crown);
                f.end = () => s4Crowns.Remove(c);
                return;
            }
            Crown4Fx flying = null;
            foreach (var x in s4Crowns)
                if (x.by == e.by && x.state == 0)
                {
                    flying = x;
                    break;
                }
            if (e.count == 1)
            {
                if (flying != null)
                {
                    flying.state = 1;
                    flying.on = e.target;
                    flying.landed = Clock;
                    flying.until = Clock + Mathf.Max(0.3f, e.size);
                }
                if (e.target != null)
                {
                    kit.FlashRing(Ground(e.target.FeetPoint) + Vector3.up * 0.05f, 1.1f, CmGold);
                    kit.AddSquash(e.target, Kit.SquashKind.Down);
                }
                CmFlash(e.at + Vector3.up * 0.25f, 0.35f, 7, CmGold);
                UltClip(UltClipId.Promote, 0.6f * lvl, 1.3f);
                UltClip(UltClipId.SubDrop, 0.5f * lvl, 1.2f);
                return;
            }
            if (flying != null)
            {
                flying.state = 2;
                flying.landed = Clock;
                flying.to = e.at;
            }
            UltClip(UltClipId.Tap, 0.6f * lvl, 1.5f);
        }

        void S4ShoutFx(SkillFxEvent e, float lvl)
        {
            var by = e.by;
            if (e.count == 0)
            {
                // A big breath: a crouch and up again, air streaking into her mouth.
                if (by == null) return;
                float breath = Mathf.Max(0.1f, e.size);
                var lines = new Kit.Strip[3];
                for (int i = 0; i < 3; i++) lines[i] = new Kit.Strip(kit, CmWhite) { core = Color.white, coreShare = 0.5f, inkShare = 0.2f };
                var pts = new List<Vector3>(3);
                var f = kit.Run(breath, (fx, d) =>
                {
                    if (by == null) return false;
                    float t = fx.age / breath;
                    kit.Coil(by, 0.6f * Mathf.Sin(t * Mathf.PI));
                    Vector3 face = FlatDir(by.Facing), mouth = DrawnHead(by) + face * 0.22f;
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 from = mouth + Quaternion.AngleAxis((i - 1) * 40f, Vector3.up) * face * 1.1f + Vector3.up * (0.25f * (i - 1));
                        float k = Mathf.Repeat(t * 2f + i * 0.33f, 1f);
                        Vector3 tip = Vector3.Lerp(from, mouth, k), tail = Vector3.Lerp(from, mouth, Mathf.Max(0f, k - 0.35f));
                        pts.Clear();
                        pts.Add(tail);
                        pts.Add((tail + tip) * 0.5f);
                        pts.Add(tip);
                        lines[i].Build(pts, j => 0.05f, kit.Eye);
                        lines[i].Apply();
                    }
                    return true;
                });
                foreach (var l in lines) f.strips.Add(l);
                UltClip(UltClipId.Swell, 0.6f * lvl, 1.4f);
                return;
            }
            // The shout: three sound waves rolling out over her fan, the camera shaking.
            float range = Mathf.Max(1f, e.size), cone = Params != null ? Params.shoutCone : 90f;
            Vector3 at = e.at, dir = FlatDir(e.dir);
            var col = S3Colors(by);
            for (int w = 0; w < 3; w++)
            {
                float delay = w * 0.09f;
                var wave = new Kit.Strip(kit, col) { core = Color.white, coreShare = 0.4f, inkShare = 0.22f };
                var pts = new List<Vector3>(13);
                var f = kit.Run(delay + 0.45f, (fx, d) =>
                {
                    float t = (fx.age - delay) / 0.45f;
                    if (t < 0f)
                    {
                        wave.Hide();
                        return true;
                    }
                    float r = Mathf.Lerp(0.6f, range, Kit.EaseOut(t));
                    pts.Clear();
                    for (int i = 0; i <= 12; i++)
                    {
                        float a = -cone * 0.5f + cone * i / 12f;
                        pts.Add(at + Quaternion.AngleAxis(a, Vector3.up) * dir * r + Vector3.up * (0.15f * Mathf.Sin(i / 12f * Mathf.PI)));
                    }
                    wave.Build(pts, i => 0.22f * (1f - 0.6f * t), kit.Eye);
                    wave.fade = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
                    wave.Apply();
                    return true;
                });
                f.strips.Add(wave);
            }
            CmFlash(at + dir * 0.4f, 0.5f, 7);
            if (by != null) kit.AddSquash(by, Kit.SquashKind.Spring);
            Shake(0.08f, 14f * Frame);
            UltClip(UltClipId.Check, 0.9f * lvl, 0.8f);
            UltClip(UltClipId.Wah, 0.6f * lvl, 0.9f);
        }

        // ---------------------------------------------------------------- the floor's shadow

        /// <summary>The first floor (not a piece) under <paramref name="at"/> within <paramref name="reach"/>.</summary>
        static Vector3 FloorBelow(Vector3 at, float reach)
        {
            Vector3 floor = at - Vector3.up * reach;
            float best = float.MaxValue;
            foreach (var hit in Physics.RaycastAll(at, Vector3.down, reach, ~0, QueryTriggerInteraction.Ignore))
                if (hit.distance < best && !hit.collider.GetComponentInParent<RagdollPawn>())
                {
                    best = hit.distance;
                    floor = hit.point;
                }
            return floor;
        }

        /// <summary>A soft dark spot on the floor this frame (what is about to land there).</summary>
        void DrawShade(Vector3 at, float radius, float alpha)
        {
            var mat = CmFillMaterial();
            if (mat == null) return;
            if (s4ShadeBlock == null) s4ShadeBlock = new MaterialPropertyBlock();
            s4ShadeBlock.SetColor("_Color", new Color(0.08f, 0.06f, 0.12f, Mathf.Clamp01(alpha)));
            var rp = new RenderParams(mat)
            {
                layer = 0, shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false, matProps = s4ShadeBlock,
                worldBounds = new Bounds(at, Vector3.one * (radius * 2f + 1f)),
            };
            Graphics.RenderMesh(rp, S4ShadeMesh(), 0, Matrix4x4.TRS(at, Quaternion.identity, new Vector3(radius * 2f, 1f, radius * 2f)));
        }

        // ---------------------------------------------------------------- shapes (made once, in code)

        /// <summary>A brick, 0.42 × 0.2 × 0.22 m, round-edged, centred.</summary>
        Mesh S4BrickMesh() => s4BrickMesh != null ? s4BrickMesh : s4BrickMesh = kit.RoundBox(new Vector3(0.42f, 0.2f, 0.22f), 0.03f);

        /// <summary>A wall stone (a merlon), 0.4 × 0.24 × 0.36 m, its foot at the origin.</summary>
        Mesh S4CapMesh()
        {
            if (s4CapMesh != null) return s4CapMesh;
            var box = kit.RoundBox(new Vector3(0.4f, 0.24f, 0.36f), 0.04f);
            s4CapMesh = new Mesh { name = "Wall stone", hideFlags = HideFlags.HideAndDontSave };
            s4CapMesh.CombineMeshes(new[] { new CombineInstance { mesh = box, transform = Matrix4x4.Translate(new Vector3(0f, 0.12f, 0f)) } }, true, true);
            s4CapMesh.RecalculateNormals();
            s4CapMesh.RecalculateBounds();
            Destroy(box);
            return s4CapMesh;
        }

        /// <summary>A crown, 0.14 m round: a band with five points, a ball on each, its foot at the origin.</summary>
        Mesh S4CrownMesh()
        {
            if (s4CrownMesh != null) return s4CrownMesh;
            const int Around = 40, Points = 5;
            const float R = 0.14f, Low = 0.07f, Peak = 0.17f, T = 0.022f;
            var v = new List<Vector3>();
            var tri = new List<int>();
            // Each column: outer foot, outer top, inner top, inner foot; the top a zigzag (a point every eighth column).
            for (int k = 0; k <= Around; k++)
            {
                float a = k * Mathf.PI * 2f / Around, phase = Mathf.Repeat(k * Points / (float)Around, 1f);
                float top = Low + (Peak - Low) * (1f - Mathf.Abs(phase * 2f - 1f));
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(d * R);
                v.Add(d * R + Vector3.up * top);
                v.Add(d * (R - T) + Vector3.up * top);
                v.Add(d * (R - T));
            }
            for (int k = 0; k < Around; k++)
            {
                int a = k * 4, b = a + 4;
                tri.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });                     // outside
                tri.AddRange(new[] { a + 1, a + 2, b + 1, b + 1, a + 2, b + 2 });         // top edge
                tri.AddRange(new[] { a + 2, a + 3, b + 2, b + 2, a + 3, b + 3 });         // inside
                tri.AddRange(new[] { a + 3, a, b + 3, b + 3, a, b });                     // foot
            }
            var band = new Mesh { name = "Crown band", hideFlags = HideFlags.HideAndDontSave };
            band.SetVertices(v);
            band.SetTriangles(tri, 0);
            var parts = new List<CombineInstance> { new CombineInstance { mesh = band, transform = Matrix4x4.identity } };
            for (int i = 0; i < Points; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / Points;
                parts.Add(new CombineInstance
                {
                    mesh = kit.meshBall,
                    transform = Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * (R - T * 0.5f), Peak + 0.012f, Mathf.Sin(a) * (R - T * 0.5f)), Quaternion.identity, Vector3.one * 0.04f),
                });
            }
            s4CrownMesh = new Mesh { name = "Crown", hideFlags = HideFlags.HideAndDontSave };
            s4CrownMesh.CombineMeshes(parts.ToArray(), true, true);
            s4CrownMesh.RecalculateNormals();
            s4CrownMesh.RecalculateBounds();
            Destroy(band);
            return s4CrownMesh;
        }

        /// <summary>A dirt mound round a body, 1.2 m across at its foot and 0.47 m high, open at the top, lumpy.</summary>
        Mesh S4MoundMesh()
        {
            if (s4MoundMesh != null) return s4MoundMesh;
            var m = CmLathe(new[]
            {
                new Vector2(0.62f, 0f), new Vector2(0.58f, 0.1f), new Vector2(0.48f, 0.27f), new Vector2(0.37f, 0.41f),
                new Vector2(0.3f, 0.47f), new Vector2(0.25f, 0.45f), new Vector2(0.23f, 0.39f),
            }, 28, "Pit mound");
            var v = m.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 p = v[i];
                float a = Mathf.Atan2(p.z, p.x), c = Mathf.Cos(a), s = Mathf.Sin(a);
                float k = 1f + 0.14f * (Mathf.PerlinNoise(c * 1.7f + 4.2f, s * 1.7f + p.y * 3f + 1.3f) - 0.5f);
                float h = 1f + 0.18f * (Mathf.PerlinNoise(c * 2.3f + 7f, s * 2.3f + 2.2f) - 0.5f);
                v[i] = new Vector3(p.x * k, p.y * h, p.z * k);
            }
            m.vertices = v;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return s4MoundMesh = m;
        }

        /// <summary>A five-pointed star, 1 m from the middle to a point, standing in the XY plane, chunky.</summary>
        Mesh S4StarMesh()
        {
            if (s4StarMesh != null) return s4StarMesh;
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = (90f + i * 36f) * Mathf.Deg2Rad, r = i % 2 == 0 ? 1f : 0.46f;
                pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return s4StarMesh = CmExtrude(pts, 0.32f, "Cc star");
        }

        /// <summary>Cast iron (lit, metallic) for a 무거움 body.</summary>
        Material S4IronMaterial()
        {
            if (s4Iron != null) return s4Iron;
            if (LitMaterials.Lit == null) return null;
            s4Iron = LitMaterials.NewLit(Kit.Hex("#8E8984"), 0.55f, 0.7f);
            s4Iron.hideFlags = HideFlags.HideAndDontSave;
            return s4Iron;
        }

        /// <summary>The puddle's wobbly disc with white vertex colours (the see-through fill tints by them).</summary>
        Mesh S4ShadeMesh()
        {
            if (s4ShadeMesh != null) return s4ShadeMesh;
            s4ShadeMesh = Instantiate(CmPuddleMesh());
            s4ShadeMesh.name = "Comic shade";
            s4ShadeMesh.hideFlags = HideFlags.HideAndDontSave;
            var white = new Color[s4ShadeMesh.vertexCount];
            for (int i = 0; i < white.Length; i++) white[i] = Color.white;
            s4ShadeMesh.colors = white;
            return s4ShadeMesh;
        }

        void DestroySetFour()
        {
            DestroySetFourB();
            ccLooks.Clear();
            s4Bricks.Clear();
            s4Pits.Clear();
            s4Crowns.Clear();
            foreach (var m in new UnityEngine.Object[] { s4BrickMesh, s4CapMesh, s4CrownMesh, s4MoundMesh, s4ShadeMesh, s4StarMesh, s4Iron })
                if (m != null) Destroy(m);
            foreach (var m in s4Bubbles.Values)
                if (m != null) Destroy(m);
            s4Bubbles.Clear();
            s4BrickMesh = s4CapMesh = s4CrownMesh = s4MoundMesh = s4ShadeMesh = s4StarMesh = null;
            s4Iron = null;
        }
    }
}
