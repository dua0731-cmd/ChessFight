using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R688 (seoungtest, 승규 11-17: "6번 7번 세탁장 함께 밟기 구간. 8번 9번 0번 vfx 가 필요해 아직 작업 x"): F · 대무도회장's
    // own effects for the en passant hunt (MgEnPassant), in place of ArenaFx's confetti bursts and floor ring (the user's
    // rules: no circles on the floor, "원은 사용 하지 않는 vfx"; not the shared primitive bits, "이런 vfx 말고"; real-looking,
    // "실사로"). The statues are wind-up dancer dolls in a clockwork ballroom - brass, gears, marble, chandeliers - so the
    // moments are told in brass sparks, little gears, parquet dust, sequins and gold glints. Same method as CheckmateFx /
    // PaintBoardFx / YardFx: code-drawn textures, URP particles (HDR tints on additive materials so they bloom), one
    // short-lived point light for the hard hits. Added by the game on first use (the saved prefab is unchanged). Pictures
    // only, played on every PC from the shared obstacle clock and the host's taken bits it already has; nothing is sent.
    //   wind      the 0.5 s warning before a step: the wind-up key's spring glints, a couple of brass sparks off the back
    //   step      each of the two "탁, 탁" taps of the two-square dash: parquet dust shoved out low behind the base, a few
    //             brass sparks skidding off the floor
    //   open      the capture window opens: a gold flare blooms on the doll, glints twinkle round it, chandelier motes fall
    //   close     the window shuts: the gold drains away as a few dimming motes and one fading glint
    //   capture   taken: a warm flash of light, a shower of brass sparks bouncing on the floor, little gears and sequins in
    //             the costume's colour thrown out the way it tips, a puff of plaster dust
    //   clank     a bounce off a statue out of its window: a short white-hot spark spray, a pinpoint flash, a breath of dust
    //   tray      a taken doll lands on the tray bench: dust off the boards, two glints
    public sealed class MiniGameBallroomFx : MonoBehaviour
    {
        const int Sheet = 4;                    // the dust sheet is Sheet x Sheet puffs
        static Texture2D dot, star, flare, sequin, smoke;
        static Mesh gear;

        // Brass and gold, tinted above 1 so the bloom catches them.
        static readonly Color SparkHdr = new Color(4.2f, 2.7f, 1.1f), GlintHdr = new Color(3.6f, 2.9f, 1.5f), FlareHdr = new Color(3f, 2.3f, 1.1f),
                              MoteHdr = new Color(2.2f, 1.8f, 1f);
        static readonly Color Dust = new Color(.86f, .81f, .73f), Brass = new Color(.78f, .58f, .26f), LightGold = new Color(1f, .78f, .45f);

        Transform root;
        ParticleSystem sparks, glints, flares, motes, dust, gears, sequins;
        readonly List<Material> made = new List<Material>();
        readonly Light[] lights = new Light[2];
        readonly float[] lightStart = new float[2], lightLife = new float[2], lightPeak = new float[2];

        void Awake()
        {
            Textures();
            // world-space effects on their own root: never under a mirrored or hidden station transform
            root = new GameObject("Ballroom FX " + name).transform;
            sparks = Sparks();
            glints = Glints();
            flares = Flares();
            motes = Motes();
            dust = Puffs();
            gears = Gears();
            sequins = Sequins();
            for (int i = 0; i < lights.Length; i++)
            {
                var go = new GameObject("Ballroom FX flash " + i);
                go.transform.SetParent(root, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = LightGold;
                l.shadows = LightShadows.None;
                l.intensity = 0f;
                l.enabled = false;
                lights[i] = l;
            }
        }

        static float R(float a, float b) => Random.Range(a, b);
        static Vector3 Around() { float a = R(0f, 6.2832f); return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }
        static Vector3 Flat(Vector3 v, Vector3 fallback) { v.y = 0f; return v.sqrMagnitude > 1e-4f ? v.normalized : fallback; }

        // ------------------------------------------------------------------ moments (at = the statue's foot, world)

        public void Wind(Vector3 at, Vector3 back)
        {
            back = Flat(back, Vector3.forward);
            var key = at + Vector3.up * .55f + back * .2f;
            for (int i = 0; i < 2; i++)
                Emit(glints, key + Random.insideUnitSphere * .06f, Vector3.up * R(.05f, .2f), R(.3f, .45f), R(.16f, .24f), Color.white, R(0f, 360f));
            for (int i = 0; i < 3; i++)
                Emit(sparks, key, (back + Random.insideUnitSphere * .6f + Vector3.up * .5f) * R(.8f, 1.6f), R(.18f, .3f), R(.025f, .04f), Color.white);
        }

        // dir = the way the statue moves (south); `second` is the second tap, a little softer.
        public void Step(Vector3 at, Vector3 dir, bool second)
        {
            dir = Flat(dir, Vector3.back);
            var side = Vector3.Cross(Vector3.up, dir);
            float s = second ? .7f : 1f;
            for (int i = 0; i < (second ? 3 : 4); i++)             // dust shoved out low behind and beside the base
            {
                float k = R(-1f, 1f);
                var v = -dir * R(.6f, 1.3f) + side * k * R(.6f, 1.1f) + Vector3.up * R(.05f, .25f);
                Emit(dust, at + Vector3.up * .06f + side * k * .25f - dir * .15f, v * s, R(.7f, 1.1f), R(.35f, .55f) * s, Dust, R(0f, 360f));
            }
            for (int i = 0; i < (second ? 3 : 5); i++)             // brass skidding on the parquet
            {
                var v = -dir * R(1.5f, 3f) + side * R(-1.2f, 1.2f) + Vector3.up * R(.4f, 1.4f);
                Emit(sparks, at + Vector3.up * .05f + side * R(-.2f, .2f), v, R(.25f, .45f), R(.025f, .04f), Color.white);
            }
        }

        public void WindowOpen(Vector3 at)
        {
            var chest = at + Vector3.up * (MgEnPassant.StatueHeight * .7f);
            Emit(flares, chest, Vector3.zero, .45f, 1.5f, Color.white);
            for (int i = 0; i < 6; i++)
            {
                var p = Around() * R(.25f, .45f);
                Emit(glints, at + p + Vector3.up * R(.2f, MgEnPassant.StatueHeight + .2f), Vector3.up * R(.05f, .3f), R(.5f, .8f), R(.2f, .34f), Color.white, R(0f, 360f));
            }
            for (int i = 0; i < 8; i++)                            // chandelier motes drifting down in the spot
                Emit(motes, at + Around() * R(0f, .6f) + Vector3.up * R(1.4f, 2.4f), Vector3.down * R(.15f, .4f), R(1.2f, 1.8f), R(.03f, .06f), Color.white);
            for (int i = 0; i < 3; i++)                            // the dash's dust settling at its foot
                Emit(dust, at + Around() * .2f + Vector3.up * .05f, Around() * R(.2f, .5f), R(.6f, .9f), R(.25f, .4f), Dust, R(0f, 360f));
        }

        public void WindowClose(Vector3 at)
        {
            Emit(glints, at + Vector3.up * (MgEnPassant.StatueHeight + .15f), Vector3.down * .15f, .35f, .3f, new Color(.8f, .8f, .8f, .8f), R(0f, 360f));
            for (int i = 0; i < 4; i++)
                Emit(motes, at + Around() * R(.1f, .35f) + Vector3.up * R(.4f, MgEnPassant.StatueHeight), Vector3.down * R(.3f, .6f), R(.5f, .8f), R(.025f, .045f),
                     new Color(.7f, .7f, .7f, .7f));
        }

        // away = the way the doll tips over (flat); costume = its costume colour.
        public void Capture(Vector3 at, Vector3 away, Color costume)
        {
            away = Flat(away, Around());
            var hit = at + Vector3.up * .5f;
            Flash(hit, 4.5f, 4f, .32f);
            Emit(flares, hit, Vector3.zero, .3f, 2.2f, Color.white);
            for (int i = 0; i < 24; i++)                           // brass sparks, white-hot, falling and skipping on the floor
            {
                var v = (away * R(.6f, 1.6f) + Random.insideUnitSphere * 1.4f + Vector3.up * R(1f, 2.6f)) * R(1.2f, 2.2f);
                Emit(sparks, hit + Random.insideUnitSphere * .1f, v, R(.45f, .8f), R(.03f, .05f), Color.white);
            }
            for (int i = 0; i < 9; i++)                            // the clockwork's little gears thrown out the way it tips
            {
                var v = away * R(1f, 2.6f) + Random.insideUnitSphere * 1.2f + Vector3.up * R(2f, 3.6f);
                EmitPiece(gears, hit + Random.insideUnitSphere * .15f, v, R(1.6f, 2.3f), R(.07f, .14f));
            }
            var cloth = Color.Lerp(costume, Color.white, .15f);
            for (int i = 0; i < 12; i++)                           // sequins off the costume, fluttering down
            {
                var v = away * R(.5f, 1.8f) + Random.insideUnitSphere * 1.1f + Vector3.up * R(1.2f, 2.6f);
                Emit(sequins, hit + Random.insideUnitSphere * .2f, v, R(1.4f, 2.2f), R(.05f, .09f), i % 4 == 0 ? new Color(1f, .85f, .5f) : cloth, R(0f, 360f));
            }
            for (int i = 0; i < 6; i++)                            // plaster and parquet dust
                Emit(dust, at + Vector3.up * R(.1f, .6f) + Random.insideUnitSphere * .2f, (away * .6f + Random.insideUnitSphere * .5f + Vector3.up * .25f) * R(.6f, 1.2f),
                     R(.9f, 1.4f), R(.4f, .65f), Dust, R(0f, 360f));
            for (int i = 0; i < 8; i++)
                Emit(glints, hit + Random.insideUnitSphere * .35f, Vector3.up * R(.3f, 1.2f) + away * R(0f, .6f), R(.5f, .85f), R(.22f, .38f), Color.white, R(0f, 360f));
        }

        // at = where the pawn met the statue (about knee height).
        public void Clank(Vector3 at)
        {
            Flash(at, 1.6f, 2.5f, .12f);
            Emit(flares, at, Vector3.zero, .14f, .8f, Color.white);
            for (int i = 0; i < 12; i++)
            {
                var v = (Random.insideUnitSphere + Vector3.up * .6f).normalized * R(2f, 4.2f);
                Emit(sparks, at + Random.insideUnitSphere * .08f, v, R(.2f, .4f), R(.025f, .04f), Color.white);
            }
            for (int i = 0; i < 2; i++)
                Emit(dust, at + Random.insideUnitSphere * .15f, Random.insideUnitSphere * .4f, R(.5f, .8f), R(.25f, .4f), Dust, R(0f, 360f));
        }

        public void TrayLand(Vector3 at)
        {
            for (int i = 0; i < 4; i++)
            {
                var d = Around();
                Emit(dust, at + d * .3f + Vector3.up * .05f, d * R(.4f, .8f) + Vector3.up * .1f, R(.6f, .9f), R(.25f, .4f), Dust, R(0f, 360f));
            }
            for (int i = 0; i < 2; i++)
                Emit(glints, at + Around() * R(0f, .3f) + Vector3.up * .2f, Vector3.up * .3f, R(.4f, .6f), R(.18f, .26f), Color.white, R(0f, 360f));
        }

        // ------------------------------------------------------------------ the flash light

        void Flash(Vector3 at, float peak, float range, float life)
        {
            int pick = 0;
            float best = float.MaxValue;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                float left = lightStart[i] + lightLife[i] - Time.time;
                if (left < best) { best = left; pick = i; }
            }
            var l = lights[pick];
            if (l == null) return;
            l.transform.position = at;
            l.range = range;
            lightStart[pick] = Time.time;
            lightLife[pick] = life;
            lightPeak[pick] = peak;
            l.intensity = peak;
            l.enabled = true;
        }

        void Update()
        {
            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null || !l.enabled) continue;
                float k = (Time.time - lightStart[i]) / Mathf.Max(.01f, lightLife[i]);
                if (k >= 1f) { l.enabled = false; continue; }
                l.intensity = lightPeak[i] * (1f - k) * (1f - k);
            }
        }

        // ------------------------------------------------------------------ parts

        void Emit(ParticleSystem ps, Vector3 at, Vector3 velocity, float life, float size, Color color, float rotation = 0f)
        {
            if (ps == null) return;
            ps.Emit(new ParticleSystem.EmitParams
            {
                position = at, velocity = velocity, startLifetime = life, startSize = size, startColor = color, rotation = rotation,
            }, 1);
        }

        void EmitPiece(ParticleSystem ps, Vector3 at, Vector3 velocity, float life, float size)
        {
            if (ps == null) return;
            ps.Emit(new ParticleSystem.EmitParams
            {
                position = at, velocity = velocity, startLifetime = life, startSize = size, startColor = Color.white,
                rotation3D = new Vector3(R(0f, 360f), R(0f, 360f), R(0f, 360f)), angularVelocity3D = Random.insideUnitSphere * 720f,
            }, 1);
        }

        ParticleSystem NewSystem(string name, int max, out ParticleSystemRenderer r)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
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
            r = go.GetComponent<ParticleSystemRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        Material Unlit(string name, Texture texture, Color tint, bool additive)
        {
            var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
            if (shader == null)
            {
                Debug.LogWarning("[미니게임 F] 파티클 셰이더가 없습니다: " + LitMaterials.ParticlesUnlitName);
                return null;
            }
            var m = new Material(shader) { mainTexture = texture, color = tint, name = name };
            LitMaterials.MakeTransparent(m, additive);
            made.Add(m);
            return m;
        }

        static void Fade(ParticleSystem ps, params (float alpha, float at)[] keys)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var a = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++) a[i] = new GradientAlphaKey(keys[i].alpha, keys[i].at);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, a);
            col.color = g;
        }

        static void Floor(ParticleSystem ps, float bounce, float loss)
        {
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.enableDynamicColliders = false;
            collision.bounce = new ParticleSystem.MinMaxCurve(bounce * .6f, bounce);
            collision.dampen = new ParticleSystem.MinMaxCurve(.25f, .5f);
            collision.lifetimeLoss = loss;
            collision.radiusScale = .4f;
            collision.quality = ParticleSystemCollisionQuality.Medium;
        }

        // Sparks: stretched by their speed, white-hot to brass to dull red, skipping on the floor.
        ParticleSystem Sparks()
        {
            var ps = NewSystem("Ballroom FX sparks", 160, out var r);
            r.sharedMaterial = Unlit("Ballroom FX sparks", dot, SparkHdr, true);
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = .045f;
            r.lengthScale = 1.2f;
            var main = ps.main;
            main.gravityModifier = 1.1f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 1f, .92f), 0f), new GradientColorKey(new Color(1f, .7f, .32f), .35f), new GradientColorKey(new Color(.7f, .2f, .05f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .55f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, .4f));
            Floor(ps, .45f, .25f);
            return ps;
        }

        // Glints: four-pointed stars that twinkle (noise on their size) and drift.
        ParticleSystem Glints()
        {
            var ps = NewSystem("Ballroom FX glints", 120, out var r);
            r.sharedMaterial = Unlit("Ballroom FX glints", star, GlintHdr, true);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            Fade(ps, (0f, 0f), (1f, .15f), (.8f, .6f), (0f, 1f));
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .12f;
            noise.frequency = 2f;
            noise.scrollSpeed = 3f;
            noise.sizeAmount = .9f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            return ps;
        }

        // Flares: a soft bright bloom that swells fast and dies.
        ParticleSystem Flares()
        {
            var ps = NewSystem("Ballroom FX flares", 24, out var r);
            r.sharedMaterial = Unlit("Ballroom FX flares", flare, FlareHdr, true);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.maxParticleSize = 3f;
            Fade(ps, (0f, 0f), (1f, .08f), (.5f, .35f), (0f, 1f));
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .35f, 0f, 6f), new Keyframe(.3f, .95f), new Keyframe(1f, 1.15f)));
            return ps;
        }

        // Motes: specks of gold dust drifting in the chandelier light.
        ParticleSystem Motes()
        {
            var ps = NewSystem("Ballroom FX motes", 120, out var r);
            r.sharedMaterial = Unlit("Ballroom FX motes", dot, MoteHdr, true);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            Fade(ps, (0f, 0f), (1f, .2f), (.7f, .7f), (0f, 1f));
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .25f;
            noise.frequency = .8f;
            noise.scrollSpeed = .5f;
            noise.sizeAmount = .6f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            return ps;
        }

        // Dust: a random puff of the sheet, turning slowly, growing (fast, then slower), thinning, slowed by the air.
        ParticleSystem Puffs()
        {
            var ps = NewSystem("Ballroom FX dust", 100, out var r);
            r.sharedMaterial = Unlit("Ballroom FX dust", smoke, Color.white, false);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.maxParticleSize = 1.5f;
            var main = ps.main;
            main.gravityModifier = -.02f;
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = Sheet;
            sheet.numTilesY = Sheet;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, .999f);
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-.5f, .5f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f, 0f, 4.5f), new Keyframe(1f, 2.5f, 0f, 0f)));
            Fade(ps, (0f, 0f), (.75f, .08f), (.4f, .45f), (0f, 1f));
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = .3f;
            drag.dampen = .08f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .3f;
            noise.frequency = .5f;
            noise.scrollSpeed = .25f;
            noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.damping = true;
            return ps;
        }

        // Gears: little brass cogs (a mesh), lit by the room, tumbling, bouncing and settling, then shrinking away.
        ParticleSystem Gears()
        {
            var ps = NewSystem("Ballroom FX gears", 60, out var r);
            var shader = LitMaterials.Lit;
            if (shader != null)
            {
                var m = new Material(shader) { name = "Ballroom FX gears", color = Brass };
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", .9f);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .62f);
                made.Add(m);
                r.sharedMaterial = m;
            }
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = gear;
            r.alignment = ParticleSystemRenderSpace.World;
            var main = ps.main;
            main.gravityModifier = 1f;
            main.startRotation3D = true;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.82f, 1f), new Keyframe(1f, 0f)));
            Floor(ps, .4f, 0f);
            return ps;
        }

        // Sequins: small bright flakes in the costume's colour, fluttering down and turning.
        ParticleSystem Sequins()
        {
            var ps = NewSystem("Ballroom FX sequins", 100, out var r);
            r.sharedMaterial = Unlit("Ballroom FX sequins", sequin, Color.white, false);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            var main = ps.main;
            main.gravityModifier = .35f;
            Fade(ps, (1f, 0f), (1f, .75f), (0f, 1f));
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-7f, 7f);
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 1.2f;
            drag.dampen = .12f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .6f;
            noise.frequency = 1.4f;
            noise.scrollSpeed = 1f;
            noise.sizeAmount = .7f;     // the flicker of a flake turning to the light
            noise.quality = ParticleSystemNoiseQuality.Low;
            return ps;
        }

        // ------------------------------------------------------------------ textures and the gear mesh

        static void Textures()
        {
            if (dot == null) dot = Draw(64, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)), 1.6f));
            if (star == null)
                star = Draw(64, (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    float rays = Mathf.Exp(-Mathf.Abs(x) * 14f) * Mathf.Exp(-y * y * 3f) + Mathf.Exp(-Mathf.Abs(y) * 14f) * Mathf.Exp(-x * x * 3f);
                    return Mathf.Clamp01(rays * .8f + Mathf.Exp(-r * r * 30f)) * Mathf.Clamp01(1f - r);
                });
            if (flare == null)
                flare = Draw(128, (x, y) =>
                {
                    // a hot core in a wide soft halo
                    float r2 = x * x + y * y;
                    return Mathf.Clamp01(Mathf.Exp(-r2 * 22f) + .35f * Mathf.Exp(-r2 * 3.5f)) * Mathf.Clamp01(1f - Mathf.Sqrt(r2));
                });
            if (sequin == null)
                sequin = DrawColor(32, (x, y) =>
                {
                    // a little round sequin: a solid disc, a darker rim, a glint up-left
                    float r = Mathf.Sqrt(x * x + y * y);
                    float hx = x + .3f, hy = y - .3f;
                    float glint = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / .3f);
                    float v = Mathf.Clamp01(.75f + .5f * glint - .25f * Mathf.Clamp01((r - .6f) / .3f));
                    return new Color(v, v, v, Mathf.Clamp01((1f - r) * 10f));
                });
            if (smoke == null) smoke = SmokeSheet(64);
            if (gear == null) gear = Gear();
        }

        static float Fbm(float x, float y)
        {
            float sum = 0f, amp = .5f, norm = 0f;
            for (int i = 0; i < 4; i++)
            {
                sum += Mathf.PerlinNoise(x, y) * amp;
                norm += amp;
                x = x * 2.03f + 17.1f;
                y = y * 2.03f + 9.7f;
                amp *= .5f;
            }
            return sum / norm;
        }

        // Sheet x Sheet puffs of fine dust, `n` pixels each: a soft round body whose edge is pushed about by fractal
        // noise, denser knots inside, a lighter top - grey, so the particle's colour tints it (as YardFx's).
        static Texture2D SmokeSheet(int n)
        {
            int size = n * Sheet;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Ballroom FX dust" };
            var pixels = new Color32[size * size];
            for (int cell = 0; cell < Sheet * Sheet; cell++)
            {
                int cx = cell % Sheet, cy = cell / Sheet;
                float ox = 23.3f * cell + 3.1f, oy = 19.7f * cell + 7.9f;
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        float x = (i + .5f) / n * 2f - 1f, y = (j + .5f) / n * 2f - 1f;
                        float wx = x + .55f * (Fbm(x * 1.6f + ox, y * 1.6f + oy) - .5f);
                        float wy = y + .55f * (Fbm(x * 1.6f + oy + 40f, y * 1.6f + ox + 40f) - .5f);
                        float r = Mathf.Sqrt(wx * wx + wy * wy);
                        float body = Mathf.Clamp01((.92f - r) / .7f);
                        body = body * body * (3f - 2f * body);
                        float knots = Fbm(wx * 2.6f + ox + 80f, wy * 2.6f + oy + 80f);
                        float a = Mathf.Pow(Mathf.Clamp01(body * (.35f + 1.1f * knots) - .08f), 1.25f);
                        float lit = Mathf.Clamp01(.7f + .24f * y + .16f * (knots - .5f) + .1f * (1f - r));
                        byte g = (byte)(lit * 255f);
                        pixels[(cy * n + j) * size + cx * n + i] = new Color32(g, g, g, (byte)(a * 255f));
                    }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        static Texture2D Draw(int n, System.Func<float, float, float> mask) =>
            DrawColor(n, (x, y) => new Color(1f, 1f, 1f, mask(x, y)));

        static Texture2D DrawColor(int n, System.Func<float, float, Color> f)
        {
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Ballroom FX" };
            var pixels = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    var c = f((i + .5f) / n * 2f - 1f, (j + .5f) / n * 2f - 1f);
                    pixels[j * n + i] = new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f),
                                                    (byte)(Mathf.Clamp01(c.a) * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        // A unit cog: eight teeth round a disc with an axle hole, 0.25 thick (flat top and bottom, crisp teeth sides).
        static Mesh Gear()
        {
            const int Teeth = 8, Per = 4, N = Teeth * Per;
            const float Outer = .5f, Root = .38f, Hole = .12f, Half = .125f;
            var rim = new Vector2[N];
            var hole = new Vector2[N];
            for (int k = 0; k < N; k++)
            {
                // root, tip, tip, root across each tooth's arc
                int part = k % Per;
                float a = (k + (part == 1 ? .25f : part == 2 ? -.25f : 0f)) * Mathf.PI * 2f / N;
                float rr = part == 1 || part == 2 ? Outer : Root;
                rim[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                float h = k * Mathf.PI * 2f / N;
                hole[k] = new Vector2(Mathf.Cos(h), Mathf.Sin(h)) * Hole;
            }
            var verts = new List<Vector3>();
            var tris = new List<int>();
            // faces: top (+y) and bottom (-y), each a strip between the hole and the rim
            for (int side = 0; side < 2; side++)
            {
                float y = side == 0 ? Half : -Half;
                int b = verts.Count;
                for (int k = 0; k < N; k++)
                {
                    verts.Add(new Vector3(rim[k].x, y, rim[k].y));
                    verts.Add(new Vector3(hole[k].x, y, hole[k].y));
                }
                for (int k = 0; k < N; k++)
                {
                    int a0 = b + 2 * k, a1 = b + 2 * ((k + 1) % N);
                    if (side == 0) tris.AddRange(new[] { a0, a0 + 1, a1, a1, a0 + 1, a1 + 1 });
                    else tris.AddRange(new[] { a0, a1, a0 + 1, a1, a1 + 1, a0 + 1 });
                }
            }
            // the rim's wall (each quad its own vertices, so the teeth keep sharp edges) and the hole's wall
            for (int k = 0; k < N; k++)
            {
                var p0 = rim[k];
                var p1 = rim[(k + 1) % N];
                int b = verts.Count;
                verts.Add(new Vector3(p0.x, Half, p0.y));
                verts.Add(new Vector3(p1.x, Half, p1.y));
                verts.Add(new Vector3(p1.x, -Half, p1.y));
                verts.Add(new Vector3(p0.x, -Half, p0.y));
                tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                var h0 = hole[k];
                var h1 = hole[(k + 1) % N];
                b = verts.Count;
                verts.Add(new Vector3(h0.x, Half, h0.y));
                verts.Add(new Vector3(h1.x, Half, h1.y));
                verts.Add(new Vector3(h1.x, -Half, h1.y));
                verts.Add(new Vector3(h0.x, -Half, h0.y));
                tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            var mesh = new Mesh { name = "Ballroom FX gear" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
            foreach (var m in made) if (m != null) Destroy(m);
        }
    }
}
