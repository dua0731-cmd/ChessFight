using System;
using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ChessFight.PawnRush
{
    // R688 (seoungtest), I · 국왕 침실 (MgCheckBlock), 승규 11-17: "8번 9번 0번 vfx 가 필요해 아직 작업 x". The room's own
    // effects in place of ArenaFx's bursts and line rings (the user turned those down: "이런 vfx 말고", and no ground
    // rings at all: "원은 사용 하지 않는 vfx"), made to look real ("실사로"): soft additive particles bright enough to
    // bloom, hot sparks off the brass-and-walnut shield screens, wood and velvet dust, pillow feathers, chips and splinters
    // that fall, short point-light flashes. Bedchamber look: candle-warm gold, violet velvet, walnut.
    // Same method as CheckmateFx / PaintBoardFx: code-drawn textures, URP particles, added by the game on first use (the
    // prefab is unchanged). Pictures only, played by every PC from the game state it already has; nothing is sent.
    //   check line   an energy beam (a hot core with streaks running to the king over a wide soft haze, flickering) in
    //                place of the solid bar: it shoots out from the attacker in 0.2 s and strikes the king (flash, sparks,
    //                a short light); while it holds, pulses run down it and it sizzles where it hits
    //   shield push  past 75 % the beam burns on the screen's face: sparks and a thread of smoke
    //   blocked      a bright strike on the screen (white-gold flash, a light, a spray of metal sparks, walnut splinters,
    //                velvet bits, dust at the wheels); the beam's part behind the screen drains away into the king; the
    //                stopped beam stays dim and smoulders on the screen
    //   knight       the line snaps (flash, sparks, the line drains away); when the knight hits the floor, rug dust,
    //                chips of the piece and gold glints
    //   pawn         a flash, a burst of pillow feathers drifting down, chips and dust; the line drains away
    //   check lifted warm candle-gold motes rise from the king's bed
    //   double check floorboards burst where the two pieces rise (splinters, dust, a red glow), red sparks and two red
    //                light pulses on the king
    //   escape       a warm team-and-gold bloom on the safe square, gold glints rising, dust where the bed slid
    //   all lifted   a soft shaft of warm light on the bed, gold glints, feathers drifting down from the canopy
    //   in check     a slow red pulse of light on the king while any check is on (in place of the red floor ring)
    public sealed class MiniGameBedchamberFx : MonoBehaviour
    {
        const int Rays = 8;
        const float Grow = .2f, TailLife = .38f;

        static Texture2D dot, star, puff, featherTex, chipTex, coreTex, hazeTex, shaftTex;
        static int canDraw;   // 0 unknown, 1 yes, -1 no particle shader

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { dot = star = puff = featherTex = chipTex = coreTex = hazeTex = shaftTex = null; canDraw = 0; }

        // Whether the particle shader is there; without it the game keeps its old bar beams and ring.
        public static bool CanDraw
        {
            get
            {
                if (canDraw == 0) canDraw = Shader.Find(LitMaterials.ParticlesUnlitName) != null ? 1 : -1;
                return canDraw > 0;
            }
        }

        static readonly Color Gold = new Color(1f, .8f, .42f), Candle = new Color(1f, .7f, .38f), Walnut = new Color(.42f, .27f, .16f),
                              Velvet = new Color(.38f, .17f, .46f), RugDust = new Color(.66f, .58f, .5f), WoodDust = new Color(.72f, .6f, .46f),
                              Smoke = new Color(.32f, .29f, .28f), Feather = new Color(.97f, .95f, .9f), CheckRed = new Color(1f, .22f, .16f);

        ParticleSystem hot, zap, pulse, glow, glints, motes, dust, feathers, chips, shaft;
        ParticleSystem.EmitParams e;

        sealed class Ray
        {
            public LineRenderer Core, Haze, Tail;
            public Material CoreMat, HazeMat, TailMat;
            public bool On, Hit;
            public float Born, NextGlow, NextSpark, NextPulse;
            public float TailAt = -10f, TailThick;
            public Vector3 TailFrom, TailTo;
            public Color TailColor;
        }

        sealed class Flash
        {
            public Light L;
            public float At = -10f, Peak, Decay;
        }

        readonly Ray[] rays = new Ray[Rays];
        readonly Flash[] flashes = new Flash[3];
        readonly List<KeyValuePair<float, Action>> later = new List<KeyValuePair<float, Action>>();
        readonly List<Material> owned = new List<Material>();
        Light checkLamp;
        float checkLevel;
        bool checkOn;
        Vector3 checkAt;

        void Awake()
        {
            Textures();
            hot = Particles("I hot sparks", dot, true, ParticleSystemRenderMode.Stretch, 300, 3.2f);
            HotFade(hot);
            Gravity(hot, 1.1f);
            Drag(hot, .6f);

            zap = Particles("I beam sparks", dot, true, ParticleSystemRenderMode.Stretch, 200, 2.8f);
            Fade(zap, 1f, 0f, .02f, .2f);
            Gravity(zap, .25f);

            pulse = Particles("I beam pulses", dot, true, ParticleSystemRenderMode.Stretch, 120, 2.6f);
            Fade(pulse, 1f, .6f, .1f, .5f);

            glow = Particles("I flashes", dot, true, ParticleSystemRenderMode.Billboard, 160, 2.2f);
            Fade(glow, 1f, 0f, .05f, .12f);
            Shrink(glow, .55f, 1.15f);

            glints = Particles("I gold glints", star, true, ParticleSystemRenderMode.Billboard, 200, 3f);
            Fade(glints, 1f, 0f, .15f);
            var twinkle = glints.sizeOverLifetime;
            twinkle.enabled = true;
            twinkle.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(.2f, 1f), new Keyframe(.45f, .55f),
                new Keyframe(.7f, .9f), new Keyframe(1f, 0f)));
            Spin(glints, 1.5f);

            motes = Particles("I candle motes", dot, true, ParticleSystemRenderMode.Billboard, 200, 2f);
            Fade(motes, 1f, 0f, .25f, .5f);
            Gravity(motes, -.04f);
            Wander(motes, .25f, .7f);

            dust = Particles("I dust", puff, false, ParticleSystemRenderMode.Billboard, 200);
            Fade(dust, .6f, 0f, .1f, .25f);
            Shrink(dust, .5f, 1.6f);
            Gravity(dust, -.02f);
            Drag(dust, 2.2f);
            Spin(dust, .6f);

            feathers = Particles("I feathers", featherTex, false, ParticleSystemRenderMode.Billboard, 120);
            Fade(feathers, 1f, 0f, .03f, .8f);
            Gravity(feathers, .06f);
            Drag(feathers, 2.6f);
            Wander(feathers, .55f, .5f);
            Spin(feathers, 3f);

            chips = Particles("I chips", chipTex, false, ParticleSystemRenderMode.Billboard, 160);
            Fade(chips, 1f, 0f, .02f, .75f);
            Gravity(chips, 1.4f);
            Spin(chips, 9f);

            shaft = Particles("I light shaft", shaftTex, true, ParticleSystemRenderMode.VerticalBillboard, 10, 1.5f);
            Fade(shaft, 1f, 0f, .25f, .55f);

            for (int i = 0; i < flashes.Length; i++) flashes[i] = new Flash { L = Lamp("I flash light " + (i + 1)) };
            checkLamp = Lamp("I check light");
        }

        void OnDestroy()
        {
            foreach (var m in owned) if (m != null) Destroy(m);
        }

        // A new position (a new seed or a reset): every beam starts hidden, so the new lines shoot out again.
        public void Clear()
        {
            for (int i = 0; i < Rays; i++)
            {
                var r = rays[i];
                if (r == null) continue;
                r.On = r.Hit = false;
                r.TailAt = -10f;
                r.Core.enabled = r.Haze.enabled = r.Tail.enabled = false;
            }
            later.Clear();
        }

        // ------------------------------------------------------------------ the check lines (called every frame)

        // One attack line, `id` 0..7. `blocked`: stopped on a shield screen at `to` (dim, smouldering there).
        public void Beam(int id, bool on, Vector3 from, Vector3 to, Color color, float thick, bool blocked)
        {
            var r = RayAt(id);
            if (r == null) return;
            if (!on)
            {
                if (r.On) { r.On = false; r.Core.enabled = r.Haze.enabled = false; }
                return;
            }
            float t = Time.time;
            if (!r.On)
            {
                r.On = true;
                r.Hit = false;
                r.Born = t;
                Emit(glow, from, Vector3.zero, .2f, .7f, Tint(color, .7f));
            }
            float g = Mathf.Clamp01((t - r.Born) / Grow);
            g = 1f - (1f - g) * (1f - g);
            var end = Vector3.Lerp(from, to, g);
            if (g >= 1f && !r.Hit)
            {
                r.Hit = true;
                if (!blocked) Strike(to, color);
            }
            float len = (end - from).magnitude;
            float flicker = .82f + .18f * Mathf.PerlinNoise(t * 11f, id * 3.7f);
            var shown = blocked ? Color.Lerp(color, new Color(.62f, .56f, .5f), .55f) : color;
            r.Core.SetPosition(0, from); r.Core.SetPosition(1, end);
            r.Haze.SetPosition(0, from); r.Haze.SetPosition(1, end);
            r.Core.widthMultiplier = thick * (blocked ? .45f : .75f) * flicker;
            r.Haze.widthMultiplier = (thick * 2.4f + .35f) * (blocked ? .6f : 1f) * (.9f + .1f * flicker);
            r.CoreMat.color = Hdr(Color.Lerp(shown, Color.white, .55f), blocked ? .7f : 3.2f * flicker);
            r.HazeMat.color = Hdr(shown, blocked ? .3f : 1.3f);
            r.CoreMat.mainTextureScale = new Vector2(Mathf.Max(.2f, len / 1.6f), 1f);
            r.CoreMat.mainTextureOffset = new Vector2(-t * (blocked ? .6f : 3.6f), 0f);
            r.HazeMat.mainTextureScale = new Vector2(Mathf.Max(.2f, len / 3.5f), 1f);
            r.HazeMat.mainTextureOffset = new Vector2(-t * (blocked ? .2f : 1.1f), 0f);
            r.Core.enabled = r.Haze.enabled = true;
            if (g < 1f) return;

            var dir = (to - from).normalized;
            if (!blocked)
            {
                // where it bites the king: a flickering hot spot, now and then a spark; pulses run down the line
                if (t >= r.NextGlow) { r.NextGlow = t + Random.Range(.08f, .14f); Emit(glow, to, Vector3.zero, .14f, Random.Range(.35f, .55f), Tint(color, .9f)); }
                if (t >= r.NextSpark)
                {
                    r.NextSpark = t + Random.Range(.25f, .45f);
                    Emit(zap, to, (-dir + Random.insideUnitSphere * .9f + Vector3.up * .6f) * Random.Range(1.5f, 3f), Random.Range(.2f, .35f), Random.Range(.03f, .05f), Tint(color, 1f));
                }
                if (t >= r.NextPulse)
                {
                    r.NextPulse = t + Random.Range(.22f, .36f);
                    float speed = 9f;
                    Emit(pulse, from + dir * .2f, dir * speed, Mathf.Max(.05f, (len - .3f) / speed), thick * .9f + .05f, Tint(Color.Lerp(color, Color.white, .5f), 1f));
                }
            }
            else
            {
                // stopped on the screen: it smoulders there
                if (t >= r.NextGlow) { r.NextGlow = t + Random.Range(.1f, .16f); Emit(glow, to - dir * .05f, Vector3.zero, .16f, Random.Range(.3f, .45f), Tint(Color.Lerp(shown, Candle, .5f), .7f)); }
                if (t >= r.NextSpark)
                {
                    r.NextSpark = t + Random.Range(.4f, .7f);
                    Emit(hot, to, (-dir * .6f + Random.insideUnitSphere * .7f + Vector3.up * .8f) * Random.Range(1.2f, 2.4f), Random.Range(.25f, .45f), Random.Range(.025f, .04f), Color.white);
                }
            }
        }

        // The part of a line no longer fed drains away into its end in TailLife s (a line cut by a screen, a knight
        // dragged off, a pawn taken).
        public void CutLine(int id, Vector3 from, Vector3 to, Color color, float thick)
        {
            var r = RayAt(id);
            if (r == null) return;
            r.TailAt = Time.time;
            r.TailFrom = from;
            r.TailTo = to;
            r.TailColor = color;
            r.TailThick = thick;
        }

        // ------------------------------------------------------------------ moments

        // A line reaching the king.
        void Strike(Vector3 at, Color color)
        {
            Emit(glow, at, Vector3.zero, .18f, 1.1f, Tint(color, 1f));
            for (int i = 0; i < 7; i++)
                Emit(zap, at, (Random.insideUnitSphere + Vector3.up * .5f) * Random.Range(2f, 3.6f), Random.Range(.25f, .4f), Random.Range(.03f, .05f), Tint(color, 1f));
            Flare(at, color, 3f, 4.5f, 12f);
        }

        // Past 75 % of the rail the line burns on the screen's face (every ~0.12 s while it rolls).
        public void Scorch(Vector3 face, Vector3 toAttacker, Color color)
        {
            for (int i = 0; i < 3; i++)
                Emit(hot, face, (toAttacker * .7f + Random.insideUnitSphere * .8f + Vector3.up * .7f) * Random.Range(1.5f, 3.2f), Random.Range(.25f, .45f),
                     Random.Range(.025f, .045f), Color.white);
            Emit(glow, face, Vector3.zero, .12f, Random.Range(.4f, .6f), Tint(Color.Lerp(color, Candle, .4f), .9f));
            if (Random.value < .35f)
                Emit(dust, face + Vector3.up * .1f, Vector3.up * Random.Range(.3f, .6f) + Random.insideUnitSphere * .1f, Random.Range(.8f, 1.2f), Random.Range(.18f, .3f), Smoke, Random.Range(0f, 360f));
        }

        // A shield cart locked on its square: the line slams into the screen.
        public void Blocked(int id, Vector3 face, Vector3 toAttacker, Vector3 kingEye, Vector3 cartFoot, Color color, float thick, Vector3 kingFoot)
        {
            Emit(glow, face, Vector3.zero, .2f, 1.7f, Tint(new Color(1f, .9f, .7f), 1f));
            Emit(glow, face, Vector3.zero, .3f, 1.1f, Tint(color, .8f));
            Flare(face + toAttacker * .3f, new Color(1f, .78f, .5f), 6f, 6f, 9f);
            for (int i = 0; i < 26; i++)
                Emit(hot, face + Random.insideUnitSphere * .1f, (toAttacker * .8f + Random.insideUnitSphere + Vector3.up * .5f).normalized * Random.Range(3f, 7f),
                     Random.Range(.4f, .8f), Random.Range(.035f, .065f), Color.white);
            for (int i = 0; i < 10; i++)
                Emit(chips, face + Random.insideUnitSphere * .25f, (toAttacker * .5f + Random.insideUnitSphere + Vector3.up * .8f) * Random.Range(1.5f, 3f),
                     Random.Range(.6f, 1f), Random.Range(.07f, .13f), i % 3 == 0 ? Velvet : Walnut, Random.Range(0f, 360f));
            for (int i = 0; i < 5; i++)
            {
                var side = Around();
                Emit(dust, cartFoot + side * .5f + Vector3.up * .15f, side * Random.Range(.6f, 1.2f) + Vector3.up * .15f, Random.Range(.8f, 1.2f), Random.Range(.4f, .7f), WoodDust, Random.Range(0f, 360f));
            }
            CutLine(id, face, kingEye, color, thick);
            Relief(kingFoot);
        }

        // A knight dragged off: the line snaps now; dust and chips when it has toppled onto the floor (0.4 s).
        public void KnightDown(int id, Vector3 eye, Vector3 kingEye, Vector3 foot, Vector3 away, Color piece, Color color, float thick, Vector3 kingFoot)
        {
            Snap(eye, color);
            CutLine(id, eye, kingEye, color, thick);
            Later(.4f, () =>
            {
                for (int i = 0; i < 8; i++)
                {
                    var side = Around();
                    Emit(dust, foot + side * .4f + Vector3.up * .15f, (side + away * .5f) * Random.Range(.7f, 1.4f) + Vector3.up * .2f, Random.Range(.9f, 1.3f), Random.Range(.45f, .8f), RugDust, Random.Range(0f, 360f));
                }
                for (int i = 0; i < 7; i++)
                    Emit(chips, foot + Vector3.up * .3f, (Around() * Random.Range(1f, 2.2f)) + Vector3.up * Random.Range(1.5f, 3f), Random.Range(.6f, .9f), Random.Range(.06f, .11f), piece, Random.Range(0f, 360f));
                for (int i = 0; i < 4; i++)
                    Emit(glints, foot + Vector3.up * Random.Range(.3f, .9f) + Random.insideUnitSphere * .3f, Vector3.up * Random.Range(.3f, .7f), Random.Range(.5f, .8f), Random.Range(.22f, .34f), Tint(Gold, 1f), Random.Range(0f, 360f));
            });
            Relief(kingFoot);
        }

        // A pawn taken by a dive: a flash and a burst of pillow feathers, chips, dust.
        public void PawnRammed(int id, Vector3 eye, Vector3 kingEye, Vector3 foot, Color piece, Color color, float thick, Vector3 kingFoot)
        {
            Snap(eye, color);
            CutLine(id, eye, kingEye, color, thick);
            var mid = foot + Vector3.up * .55f;
            Emit(glow, mid, Vector3.zero, .16f, 1.3f, Tint(new Color(1f, .92f, .78f), .9f));
            Flare(mid, Candle, 4f, 5f, 10f);
            for (int i = 0; i < 16; i++)
                Emit(feathers, mid + Random.insideUnitSphere * .25f, (Random.insideUnitSphere * 2.2f + Vector3.up * Random.Range(1.5f, 3f)), Random.Range(1.8f, 2.6f),
                     Random.Range(.14f, .24f), Feather, Random.Range(0f, 360f));
            for (int i = 0; i < 8; i++)
                Emit(chips, mid, Around() * Random.Range(1.2f, 2.6f) + Vector3.up * Random.Range(1.5f, 3f), Random.Range(.6f, .9f), Random.Range(.06f, .1f), piece, Random.Range(0f, 360f));
            for (int i = 0; i < 5; i++)
            {
                var side = Around();
                Emit(dust, foot + side * .3f + Vector3.up * .15f, side * Random.Range(.6f, 1.2f) + Vector3.up * .2f, Random.Range(.8f, 1.2f), Random.Range(.4f, .65f), RugDust, Random.Range(0f, 360f));
            }
            Relief(kingFoot);
        }

        // One check lifted: warm candle-gold motes rise from the king's bed.
        void Relief(Vector3 kingFoot)
        {
            for (int i = 0; i < 9; i++)
            {
                var p = Random.insideUnitCircle * .9f;
                Emit(motes, kingFoot + new Vector3(p.x, Random.Range(.9f, 1.4f), p.y), Vector3.up * Random.Range(.25f, .6f), Random.Range(1.2f, 1.8f), Random.Range(.06f, .11f), Tint(Candle, 1f));
            }
            for (int i = 0; i < 3; i++)
                Emit(glints, kingFoot + new Vector3(Random.Range(-.7f, .7f), Random.Range(1.1f, 1.8f), Random.Range(-.7f, .7f)), Vector3.up * .3f, Random.Range(.6f, .9f), Random.Range(.25f, .38f), Tint(Gold, 1f), Random.Range(0f, 360f));
        }

        // The hidden double check rises through the floorboards.
        public void DoubleCheck(Vector3 kingFoot, Vector3 squareA, Vector3 squareB)
        {
            foreach (var sq in new[] { squareA, squareB })
            {
                Emit(glow, sq + Vector3.up * .2f, Vector3.zero, .35f, 1.6f, Tint(CheckRed, .8f));
                for (int i = 0; i < 8; i++)
                    Emit(chips, sq + new Vector3(Random.Range(-.6f, .6f), .1f, Random.Range(-.6f, .6f)), Around() * Random.Range(.8f, 2f) + Vector3.up * Random.Range(2.5f, 4.5f),
                         Random.Range(.7f, 1f), Random.Range(.08f, .14f), Walnut, Random.Range(0f, 360f));
                for (int i = 0; i < 6; i++)
                {
                    var side = Around();
                    Emit(dust, sq + side * .5f + Vector3.up * .2f, side * Random.Range(.6f, 1.3f) + Vector3.up * .5f, Random.Range(1f, 1.4f), Random.Range(.5f, .85f), WoodDust, Random.Range(0f, 360f));
                }
            }
            var head = kingFoot + Vector3.up * 1.4f;
            for (int i = 0; i < 10; i++)
                Emit(zap, head, (Random.insideUnitSphere + Vector3.up * .6f) * Random.Range(2f, 3.5f), Random.Range(.3f, .45f), Random.Range(.035f, .055f), Tint(CheckRed, 1f));
            Flare(head, CheckRed, 5f, 7f, 5f);
            Later(.2f, () => Flare(head, CheckRed, 4f, 6f, 5f));   // the second beat (was the second red ring)
        }

        // The king dragged onto the safe square.
        public void Escape(Vector3 square, Color team)
        {
            var warm = Color.Lerp(team, Candle, .55f);
            Emit(glow, square + Vector3.up * .6f, Vector3.zero, .45f, 2.6f, Tint(warm, .8f));
            Flare(square + Vector3.up * 1.2f, warm, 5f, 7f, 3f);
            for (int i = 0; i < 14; i++)
            {
                var p = Random.insideUnitCircle * .9f;
                Emit(glints, square + new Vector3(p.x, Random.Range(.2f, 1f), p.y), Vector3.up * Random.Range(.5f, 1.2f), Random.Range(.7f, 1.1f), Random.Range(.25f, .4f),
                     Tint(i % 3 == 0 ? team : Gold, 1f), Random.Range(0f, 360f));
            }
            for (int i = 0; i < 8; i++)
            {
                var side = Around();
                Emit(dust, square + side * .9f + Vector3.up * .15f, side * Random.Range(.5f, 1f) + Vector3.up * .15f, Random.Range(.9f, 1.3f), Random.Range(.45f, .75f), RugDust, Random.Range(0f, 360f));
            }
        }

        // Every check lifted: a soft shaft of warm light on the bed, gold glints, feathers drifting down from the canopy.
        public void AllClear(Vector3 kingFoot, Color team)
        {
            Emit(shaft, kingFoot + Vector3.up * 3.2f, Vector3.zero, 2.2f, 6.4f, Tint(Candle, .9f));
            Flare(kingFoot + Vector3.up * 2.2f, Candle, 5f, 8f, 1.6f);
            for (int i = 0; i < 18; i++)
            {
                var p = Random.insideUnitCircle * 1.3f;
                Emit(glints, kingFoot + new Vector3(p.x, Random.Range(.6f, 2.6f), p.y), Vector3.up * Random.Range(.15f, .45f), Random.Range(.9f, 1.4f), Random.Range(.25f, .42f),
                     Tint(i % 4 == 0 ? team : Gold, 1f), Random.Range(0f, 360f));
            }
            for (int i = 0; i < 14; i++)
            {
                var p = Random.insideUnitCircle * 1.2f;
                Emit(feathers, kingFoot + new Vector3(p.x, Random.Range(3.1f, 3.5f), p.y), new Vector3(Random.Range(-.3f, .3f), -Random.Range(.1f, .4f), Random.Range(-.3f, .3f)),
                     Random.Range(2.8f, 3.6f), Random.Range(.14f, .22f), Feather, Random.Range(0f, 360f));
            }
            Relief(kingFoot);
        }

        // While any check is on, a slow red pulse of light on the king (called every frame).
        public void InCheck(Vector3 kingFoot, bool on)
        {
            checkOn = on;
            checkAt = kingFoot + Vector3.up * 1.6f;
        }

        void Snap(Vector3 at, Color color)
        {
            Emit(glow, at, Vector3.zero, .16f, .9f, Tint(color, 1f));
            for (int i = 0; i < 6; i++)
                Emit(zap, at, (Random.insideUnitSphere + Vector3.up * .4f) * Random.Range(1.5f, 3f), Random.Range(.2f, .35f), Random.Range(.03f, .05f), Tint(color, 1f));
        }

        // ------------------------------------------------------------------ per frame: line tails, lights, delayed bits

        void Update()
        {
            float t = Time.time;
            for (int i = later.Count - 1; i >= 0; i--)
            {
                if (t < later[i].Key) continue;
                var a = later[i].Value;
                later.RemoveAt(i);
                a();
            }
            for (int i = 0; i < Rays; i++)
            {
                var r = rays[i];
                if (r == null) continue;
                float u = (t - r.TailAt) / TailLife;
                bool on = u >= 0f && u < 1f;
                if (r.Tail.enabled != on) r.Tail.enabled = on;
                if (!on) continue;
                float ease = u * u;
                var from = Vector3.Lerp(r.TailFrom, r.TailTo, ease);
                r.Tail.SetPosition(0, from);
                r.Tail.SetPosition(1, r.TailTo);
                r.Tail.widthMultiplier = r.TailThick * .8f * (1f - u * .6f);
                r.TailMat.color = Hdr(Color.Lerp(r.TailColor, Color.white, .5f), 3f * (1f - u));
                r.TailMat.mainTextureScale = new Vector2(Mathf.Max(.2f, (r.TailTo - from).magnitude / 1.6f), 1f);
                r.TailMat.mainTextureOffset = new Vector2(-t * 5f, 0f);
            }
            foreach (var f in flashes)
            {
                float since = t - f.At;
                float k = since >= 0f ? f.Peak * Mathf.Exp(-since * f.Decay) : 0f;
                bool lit = k > .03f;
                if (f.L.enabled != lit) f.L.enabled = lit;
                if (lit) f.L.intensity = k;
            }
            checkLevel = Mathf.MoveTowards(checkLevel, checkOn ? 1f : 0f, Time.deltaTime * 2f);
            bool lamp = checkLevel > .01f;
            if (checkLamp.enabled != lamp) checkLamp.enabled = lamp;
            if (lamp)
            {
                checkLamp.transform.position = checkAt;
                checkLamp.color = CheckRed;
                checkLamp.range = 4.5f;
                checkLamp.intensity = checkLevel * (1f + .9f * (.5f + .5f * Mathf.Sin(t * 1.6f * Mathf.PI * 2f)));
            }
        }

        void Later(float delay, Action a) => later.Add(new KeyValuePair<float, Action>(Time.time + delay, a));

        void Flare(Vector3 at, Color color, float peak, float range, float decay)
        {
            Flash pick = flashes[0];
            foreach (var f in flashes) if (f.At < pick.At) pick = f;
            pick.At = Time.time;
            pick.Peak = peak;
            pick.Decay = decay;
            pick.L.transform.position = at;
            pick.L.color = color;
            pick.L.range = range;
            pick.L.intensity = peak;
            pick.L.enabled = true;
        }

        Light Lamp(string name)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(transform, false);
            l.type = LightType.Point;
            l.shadows = LightShadows.None;
            l.intensity = 0f;
            l.enabled = false;
            return l;
        }

        // ------------------------------------------------------------------ parts

        Ray RayAt(int id)
        {
            if (id < 0 || id >= Rays || !CanDraw) return null;
            if (rays[id] != null) return rays[id];
            var r = new Ray();
            r.Haze = Line("I beam haze " + id, hazeTex, out r.HazeMat);
            r.Core = Line("I beam core " + id, coreTex, out r.CoreMat);
            r.Tail = Line("I beam tail " + id, coreTex, out r.TailMat);
            rays[id] = r;
            return r;
        }

        LineRenderer Line(string name, Texture2D texture, out Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.textureMode = LineTextureMode.Stretch;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .05f), new GradientAlphaKey(1f, .96f), new GradientAlphaKey(.5f, 1f) });
            lr.colorGradient = g;
            mat = LitMaterials.NewAdditiveParticles(texture, Color.white);
            if (mat != null) { mat.name = name; owned.Add(mat); }
            lr.sharedMaterial = mat;
            lr.enabled = false;
            return lr;
        }

        void Emit(ParticleSystem ps, Vector3 at, Vector3 velocity, float life, float size, Color color, float rotation = 0f)
        {
            if (ps == null) return;
            e.position = at;
            e.velocity = velocity;
            e.startLifetime = life;
            e.startSize = size;
            e.startColor = color;
            e.rotation = rotation;
            ps.Emit(e, 1);
        }

        static Vector3 Around() { float a = Random.Range(0f, Mathf.PI * 2f); return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }

        static Color Tint(Color c, float k) => new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);

        static Color Hdr(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);

        ParticleSystem Particles(string name, Texture2D texture, bool additive, ParticleSystemRenderMode mode, int max, float brightness = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.maxParticles = max;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
            if (shader != null)
            {
                var m = new Material(shader) { mainTexture = texture, name = name };
                LitMaterials.MakeTransparent(m, additive);
                m.color = new Color(brightness, brightness, brightness, 1f);
                r.sharedMaterial = m;
                owned.Add(m);
            }
            else Debug.LogWarning("[미니게임 I] 파티클 셰이더가 없습니다: " + LitMaterials.ParticlesUnlitName);
            r.renderMode = mode;
            r.velocityScale = .06f;
            r.lengthScale = 1.4f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.alignment = ParticleSystemRenderSpace.View;
            ps.Play();
            return ps;
        }

        static void Fade(ParticleSystem ps, float peak, float end, float rise = .08f, float hold = .08f)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, rise), new GradientAlphaKey(peak, Mathf.Max(rise, hold)),
                              new GradientAlphaKey(end, 1f) });
            col.color = g;
        }

        // Metal sparks: white-hot, cooling through gold to a dull orange as they fall.
        static void HotFade(ParticleSystem ps)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, .78f, .42f), .3f), new GradientColorKey(new Color(1f, .42f, .16f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .55f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        static void Shrink(ParticleSystem ps, float from, float to)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, from, 0f, 4f), new Keyframe(.4f, Mathf.Lerp(from, to, .8f)), new Keyframe(1f, to)));
        }

        static void Gravity(ParticleSystem ps, float g)
        {
            var main = ps.main;
            main.gravityModifier = g;
        }

        static void Drag(ParticleSystem ps, float drag)
        {
            var lv = ps.limitVelocityOverLifetime;
            lv.enabled = true;
            lv.limit = 50f;
            lv.drag = drag;
        }

        static void Wander(ParticleSystem ps, float strength, float frequency)
        {
            var n = ps.noise;
            n.enabled = true;
            n.strength = strength;
            n.frequency = frequency;
            n.scrollSpeed = .3f;
            n.quality = ParticleSystemNoiseQuality.Low;
        }

        static void Spin(ParticleSystem ps, float speed)
        {
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-speed, speed);
        }

        // ------------------------------------------------------------------ textures (drawn here, no files)

        static void Textures()
        {
            if (dot != null) return;
            dot = Draw(64, 64, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)), 1.8f));
            star = Draw(96, 96, (x, y) =>
            {
                float ax = Mathf.Abs(x), ay = Mathf.Abs(y), r = Mathf.Sqrt(x * x + y * y);
                float spikes = Mathf.Max(Mathf.Clamp01(1f - ay * 14f) * Mathf.Clamp01(1f - ax), Mathf.Clamp01(1f - ax * 14f) * Mathf.Clamp01(1f - ay));
                float core = Mathf.Pow(Mathf.Clamp01(1f - r * 2.6f), 1.5f);
                return Mathf.Max(spikes * spikes, core);
            });
            // a soft cloudy puff (dust, smoke)
            puff = Draw(96, 96, (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y), a = Mathf.Atan2(y, x);
                float edge = .78f + .08f * Mathf.Sin(a * 5f) + .05f * Mathf.Sin(a * 9f + 1f);
                float grain = .85f + .15f * Mathf.Sin(x * 23f + Mathf.Sin(y * 17f) * 2f);
                return Mathf.Pow(Mathf.Clamp01(1f - r / edge), 1.4f) * grain;
            });
            // a down feather: a quill up the middle, a soft vane round it with barbs, fluffy at the foot
            featherTex = Draw(64, 64, (x, y) =>
            {
                float v = (y + .95f) / 1.9f;   // 0 at the foot, 1 at the tip
                if (v < 0f || v > 1f) return 0f;
                float half = .42f * Mathf.Sqrt(Mathf.Clamp01(1f - Mathf.Pow(v * 2f - 1f, 2f))) * Mathf.Clamp01(v * 4f);
                float ax = Mathf.Abs(x);
                float vane = Mathf.Clamp01((half - ax) * 14f) * (.7f + .3f * Mathf.Sin(y * 46f + ax * 30f));
                float quill = Mathf.Clamp01(1f - ax / .035f) * Mathf.Clamp01(v * 20f);
                return Mathf.Max(vane * (v < .2f ? .55f : .9f), quill);
            });
            // a splinter / chip: a long rounded sliver
            chipTex = Draw(48, 48, (x, y) =>
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x) - .5f), dy = Mathf.Max(0f, Mathf.Abs(y + x * .15f) - .12f);
                return Mathf.Clamp01((.28f - Mathf.Sqrt(dx * dx + dy * dy)) * 20f);
            });
            // the beam's core: u runs along the line (tiles), v across; a hot centre with streaks of energy along it
            coreTex = Draw(128, 32, (x, y) =>
            {
                float u = (x + 1f) * .5f;
                float across = Mathf.Exp(-(y * y) / (.09f));
                float streak = .6f + .2f * Mathf.Sin(u * Mathf.PI * 2f * 3f) + .12f * Mathf.Sin(u * Mathf.PI * 2f * 7f + 1.3f) + .08f * Mathf.Sin(u * Mathf.PI * 2f * 13f + 2.1f);
                float centre = Mathf.Exp(-(y * y) / .012f);
                return Mathf.Clamp01(across * streak + centre * .6f);
            }, TextureWrapMode.Repeat);
            // the haze round it: soft to the edges, slow swells along it
            hazeTex = Draw(64, 32, (x, y) =>
            {
                float u = (x + 1f) * .5f;
                float across = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(y)), 2.2f);
                return across * (.55f + .25f * Mathf.Sin(u * Mathf.PI * 2f) + .2f * Mathf.Sin(u * Mathf.PI * 2f * 2f + .7f)) * .6f;
            }, TextureWrapMode.Repeat);
            // a soft vertical shaft: bright at the top, gone at the foot
            shaftTex = Draw(128, 128, (x, y) =>
            {
                float side = Mathf.Clamp01(1f - Mathf.Abs(x) / .3f);
                float v = (y + 1f) * .5f;
                return side * side * (3f - 2f * side) * Mathf.Pow(v, .8f) * Mathf.Clamp01(v * 8f);
            });
        }

        static Texture2D Draw(int w, int h, Func<float, float, float> mask, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = wrap, name = "I bedchamber fx" };
            if (wrap == TextureWrapMode.Repeat) texture.wrapModeV = TextureWrapMode.Clamp;
            var pixels = new Color32[w * h];
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    byte b = (byte)(Mathf.Clamp01(mask((i + .5f) / w * 2f - 1f, (j + .5f) / h * 2f - 1f)) * 255f);
                    pixels[j * w + i] = new Color32(255, 255, 255, b);
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }
    }
}
