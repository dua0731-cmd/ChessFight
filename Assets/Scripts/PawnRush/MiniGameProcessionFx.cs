using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R688 (seoungtest), G · 왕의 행차 회랑 (MgKingEscort): the room's own effects (승규 님 11-17: "8번 9번 0번 vfx 가 필요해 아직
    // 작업 x"), in place of ArenaFx's primitive bursts and line rings ("이런 vfx 말고"; no ground rings: "원은 사용 하지 않는
    // vfx"). Real-looking, short and modest, as YardFx / CheckmateFx: soft dust from a code-drawn puff sheet, small lit
    // chips and sand grains with gravity that bounce and settle, HDR sparks and gold glints that bloom, short flashes.
    // Added by the game on first use, so the prefab carries nothing new; pictures only, played by every PC from the
    // game state it already has (nothing is sent). The door's banners stay MiniGameProcessionGateFx's.
    //   scrape   the sled sliding on the wood road: a low pale dust trail off the runners' tails
    //   sand     on sand: denser sand-coloured dust and grains kicked back from the runners
    //   breakaway the sled breaking loose: a low skirt of dust shoved out under it, a few grains
    //   settle   the sled coming to rest: a soft puff rolling on ahead and sinking
    //   bump     the sled stopped by a rook: a short white flash at the touch, brass sparks, marble chips, a dust puff
    //   grind    a rook dragged along its groove: stone dust at its foot and marble grit skipping off
    //   latch    a rook home in its garage: a gold flash on the brass stop, a spray of brass sparks, a dust skirt shoved
    //            out of the garage, a few gold glints drifting up
    //   arrive   the sled on the gold frame: its dust settling round it and gold glints rising from the frame's corners
    public sealed class MiniGameProcessionFx : MonoBehaviour
    {
        // The road's dust (pale wood and lime), its sand, the rooks' marble, the brass of the stops and frame.
        static readonly Color RoadDust = new Color(.82f, .76f, .66f), SandDust = new Color(.86f, .74f, .50f), Marble = new Color(.84f, .81f, .76f);
        static readonly Color SandGrain = new Color(.80f, .66f, .38f), MarbleChip = new Color(.78f, .76f, .72f);
        static readonly Color SparkHdr = new Color(4.4f, 2.8f, 1.1f), GlintHdr = new Color(4f, 3.1f, 1.3f), FlashHdr = new Color(3.2f, 2.9f, 2.4f);
        const int Sheet = 4;
        static Texture2D smoke, dot, star;
        static Mesh chunk;

        readonly List<Material> made = new List<Material>();
        ParticleSystem dust, grains, chips, sparks, glints, flash;
        ParticleSystem.EmitParams e;
        Color lit = Color.white;
        float litAt = -10f;

        void Awake()
        {
            Textures();
            dust = Puffs("G fx dust", 260);
            grains = Pieces("G fx sand grains", SandGrain, 160, 1.1f, .25f);
            chips = Pieces("G fx marble chips", MarbleChip, 120, 1.15f, .4f);
            sparks = Sparks();
            glints = Glints();
            flash = Flash();
        }

        void OnDestroy()
        {
            foreach (var m in made)
                if (m != null) Destroy(m);
        }

        // R688 (seoungtest, 승규 after a look: "8번 먼지 먼지 효과 x"): no dust in the procession - the dust puffs and the
        // sand grains off; the sparks, chips, flashes and glints of the bump, the latch and the arrival stay.
        static readonly bool DustOn = false, GlowOn = false;

        // ------------------------------------------------------------------ moments (at = on the floor)

        // The sled sliding: `tail` the runners' tails on the floor, `back` the way behind it (flat, unit), `speed01` its
        // speed against full. Called a few times a second while it moves.
        public void Scrape(Vector3 tail, Vector3 back, bool sand, float speed01)
        {
            if (!DustOn) return;   // R688: dust and sand grains only - off
            Relight();
            var side = new Vector3(back.z, 0f, -back.x);
            int puffs = sand ? 2 : 1;
            for (int i = 0; i < puffs; i++)
            {
                var p = tail + side * Random.Range(-.6f, .6f) + Vector3.up * Random.Range(.03f, .12f);
                Puff(p, back * Random.Range(.2f, .5f) * (.5f + speed01) + side * Random.Range(-.15f, .15f) + Vector3.up * Random.Range(.05f, .2f),
                     Random.Range(.9f, 1.4f), Random.Range(.35f, .55f) * (sand ? 1.2f : 1f), sand ? SandDust : RoadDust, sand ? .55f : .32f);
            }
            if (!sand) return;
            int n = Random.Range(2, 4);
            for (int i = 0; i < n; i++)
                Piece(grains, tail + side * Random.Range(-.65f, .65f) + Vector3.up * .05f,
                      back * Random.Range(.6f, 1.4f) * (.6f + speed01) + Vector3.up * Random.Range(.6f, 1.3f) + side * Random.Range(-.3f, .3f),
                      Random.Range(.018f, .032f), Random.Range(.6f, 1f));
        }

        // The sled breaking loose: dust shoved out low on all sides of its base, more at the back.
        public void Breakaway(Vector3 at, Vector3 forward, float half, bool sand)
        {
            if (!DustOn) return;
            Relight();
            var colour = sand ? SandDust : RoadDust;
            for (int i = 0; i < 6; i++)
            {
                float a = (i + Random.value * .6f) * Mathf.PI * 2f / 6f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float k = Vector3.Dot(d, -forward) > .3f ? 1.4f : .8f;
                Puff(at + d * half + Vector3.up * .06f, d * Random.Range(.5f, .9f) * k + Vector3.up * .1f, Random.Range(.8f, 1.2f),
                     Random.Range(.4f, .6f), colour, sand ? .55f : .35f);
            }
            if (sand)
                for (int i = 0; i < 5; i++)
                {
                    var d = Random.insideUnitCircle.normalized;
                    Piece(grains, at + new Vector3(d.x, 0f, d.y) * half + Vector3.up * .05f, new Vector3(d.x, 0f, d.y) * Random.Range(.6f, 1.2f) + Vector3.up * Random.Range(.8f, 1.4f),
                          Random.Range(.018f, .03f), Random.Range(.6f, .9f));
                }
        }

        // The sled coming to rest: the air it pushed rolls a soft puff on ahead of it, low.
        public void Settle(Vector3 front, Vector3 forward, bool sand)
        {
            if (!DustOn) return;
            Relight();
            var side = new Vector3(forward.z, 0f, -forward.x);
            for (int i = 0; i < 3; i++)
                Puff(front + side * Random.Range(-.6f, .6f) + Vector3.up * .06f, forward * Random.Range(.35f, .6f) + side * Random.Range(-.2f, .2f),
                     Random.Range(.9f, 1.3f), Random.Range(.4f, .55f), sand ? SandDust : RoadDust, sand ? .45f : .28f);
        }

        // The sled stopped against a rook: `touch` where its front meets the rook, at runner height.
        public void Bump(Vector3 touch, Vector3 forward)
        {
            Relight();
            // R688: no white flash on the bump (승규 "빛이 너무 강해")
            var side = new Vector3(forward.z, 0f, -forward.x);
            for (int i = 0; i < 7; i++)
                Emit(sparks, touch + Vector3.up * Random.Range(.05f, .35f) + side * Random.Range(-.5f, .5f),
                     -forward * Random.Range(.6f, 1.6f) + side * Random.Range(-1.4f, 1.4f) + Vector3.up * Random.Range(.8f, 2f),
                     Random.Range(.3f, .5f), Random.Range(.025f, .045f), Color.white);
            for (int i = 0; i < 5; i++)
                Piece(chips, touch + Vector3.up * Random.Range(.1f, .4f), -forward * Random.Range(.4f, 1f) + side * Random.Range(-1f, 1f) + Vector3.up * Random.Range(1f, 2f),
                      Random.Range(.03f, .055f), Random.Range(.8f, 1.2f));
            for (int i = 0; i < 2; i++)
                Puff(touch + side * Random.Range(-.4f, .4f) + Vector3.up * .1f, Vector3.up * .15f + side * Random.Range(-.3f, .3f), Random.Range(.8f, 1.1f),
                     Random.Range(.4f, .55f), Marble, .4f);
        }

        // A rook dragged: `foot` its base on the floor, `groove` the way it goes (flat, unit).
        public void Grind(Vector3 foot, Vector3 groove)
        {
            Relight();
            var side = new Vector3(groove.z, 0f, -groove.x);
            Puff(foot - groove * .4f + side * Random.Range(-.3f, .3f) + Vector3.up * .05f, -groove * Random.Range(.15f, .35f) + Vector3.up * .08f,
                 Random.Range(.8f, 1.2f), Random.Range(.3f, .45f), Marble, .35f);
            int n = Random.Range(1, 3);
            for (int i = 0; i < n; i++)
            {
                float s = Random.value < .5f ? -1f : 1f;
                Piece(chips, foot + side * s * .4f + Vector3.up * .04f, side * s * Random.Range(.4f, .9f) - groove * Random.Range(0f, .4f) + Vector3.up * Random.Range(.5f, 1f),
                      Random.Range(.02f, .035f), Random.Range(.6f, .9f));
            }
        }

        // A rook latched: `stop` the brass stop at the garage's end, `groove` the way the rook went in (flat, unit).
        public void Latch(Vector3 stop, Vector3 groove)
        {
            Relight();
            var side = new Vector3(groove.z, 0f, -groove.x);
            var hit = stop + Vector3.up * .15f;
            Emit(flash, hit, Vector3.zero, .14f, 1.1f, new Color(1f, .85f, .55f));
            for (int i = 0; i < 12; i++)
                Emit(sparks, hit + side * Random.Range(-.6f, .6f),
                     -groove * Random.Range(.6f, 1.8f) + side * Random.Range(-1.8f, 1.8f) + Vector3.up * Random.Range(1.2f, 2.6f),
                     Random.Range(.35f, .6f), Random.Range(.025f, .05f), Color.white);
            // the rook's weight shoves the garage's dust out round its foot, low, mostly back the way it came
            var foot = stop - groove * .55f;
            for (int i = 0; i < 6; i++)
            {
                float a = (i + Random.value * .6f) * Mathf.PI * 2f / 6f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float k = Vector3.Dot(d, -groove) > .3f ? 1.3f : .7f;
                Puff(foot + d * .45f + Vector3.up * .06f, d * Random.Range(.5f, .9f) * k + Vector3.up * .12f, Random.Range(.9f, 1.3f),
                     Random.Range(.4f, .6f), Marble, .45f);
            }
            for (int i = 0; i < 6; i++)
                Piece(chips, foot + Random.insideUnitSphere * .3f + Vector3.up * .1f,
                      Random.insideUnitSphere * .8f - groove * .5f + Vector3.up * Random.Range(1f, 1.8f), Random.Range(.025f, .05f), Random.Range(.8f, 1.2f));
            for (int i = 0; i < 4; i++)
                Emit(glints, hit + side * Random.Range(-.6f, .6f) + Vector3.up * Random.Range(0f, .2f), Vector3.up * Random.Range(.2f, .5f),
                     Random.Range(.6f, .9f), Random.Range(.18f, .3f), Color.white, Random.Range(0f, 360f));
        }

        // The sled on the gold frame: `at` its centre on the floor, `edge` the frame's half size.
        public void Arrive(Vector3 at, Vector3 forward, float edge, bool sand)
        {
            Relight();
            var colour = sand ? SandDust : RoadDust;
            for (int i = 0; i < 8; i++)
            {
                float a = (i + Random.value * .5f) * Mathf.PI * 2f / 8f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Puff(at + d * edge + Vector3.up * .06f, d * Random.Range(.4f, .7f) + forward * .2f + Vector3.up * .08f, Random.Range(1f, 1.5f),
                     Random.Range(.45f, .6f), colour, .4f);
            }
            var side = new Vector3(forward.z, 0f, -forward.x);
            foreach (float u in new[] { -1f, 1f })
                foreach (float v in new[] { -1f, 1f })
                {
                    var corner = at + (forward * u + side * v) * edge + Vector3.up * .12f;
                    for (int i = 0; i < 2; i++)
                        Emit(glints, corner + Random.insideUnitSphere * .08f, Vector3.up * Random.Range(.35f, .7f), Random.Range(.7f, 1.1f),
                             Random.Range(.2f, .32f), Color.white, Random.Range(0f, 360f));
                }
        }

        // ------------------------------------------------------------------ emitting

        void Puff(Vector3 at, Vector3 v, float life, float size, Color colour, float alpha)
        {
            if (!DustOn) return;
            var c = new Color(colour.r * lit.r, colour.g * lit.g, colour.b * lit.b, alpha);
            Emit(dust, at, v, life, size, c, Random.Range(0f, 360f));
        }

        void Piece(ParticleSystem ps, Vector3 at, Vector3 v, float size, float life)
        {
            if (ps == null) return;
            e.position = at;
            e.velocity = v;
            e.startLifetime = life;
            e.startSize3D = new Vector3(size, size, size);
            e.rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            e.angularVelocity3D = Random.insideUnitSphere * 540f;
            e.startColor = Color.white;
            ps.Emit(e, 1);
            e.ResetStartSize();
            e.ResetRotation();
            e.ResetAngularVelocity();
        }

        void Emit(ParticleSystem ps, Vector3 at, Vector3 v, float life, float size, Color colour, float rotation = 0f)
        {
            if (ps == null) return;
            // R688 (seoungtest, 승규 after a white blow-out in 8: "그렇게 하지 말고 그냥 8번 빛나는 그 효과 빼"): nothing
            // that glows in the procession - no flash, no brass sparks, no gold glints; the marble chips stay.
            if (!GlowOn && (ps == flash || ps == sparks || ps == glints)) return;
            e.position = at;
            e.velocity = v;
            e.startLifetime = life;
            e.startSize = size;
            e.startColor = colour;
            e.rotation = rotation;
            ps.Emit(e, 1);
        }

        // The dust is unlit: tinted once a second by the room's ambient and its strongest directional light (as YardFx),
        // so it is not brighter than the floor it rises from.
        void Relight()
        {
            if (Time.time - litAt < 1f) return;
            litAt = Time.time;
            var c = RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight
                ? (RenderSettings.ambientSkyColor + RenderSettings.ambientEquatorColor) * .5f
                : RenderSettings.ambientLight;
            Light sun = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.isActiveAndEnabled && (sun == null || l.intensity > sun.intensity)) sun = l;
            if (sun != null) c += sun.color * sun.intensity * .55f * Mathf.Clamp01(.25f - sun.transform.forward.y);
            float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (peak < .45f) c = Color.Lerp(c, Color.white, .45f - peak);
            if (peak > 1f) c /= peak;
            c.a = 1f;
            lit = c;
        }

        // ------------------------------------------------------------------ the systems

        ParticleSystem NewSystem(string name, int max, out ParticleSystemRenderer r)
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
                Debug.LogWarning("[미니게임 G] 파티클 셰이더가 없습니다: " + LitMaterials.ParticlesUnlitName);
                return null;
            }
            var m = new Material(shader) { mainTexture = texture, color = tint, name = name };
            LitMaterials.MakeTransparent(m, additive);
            made.Add(m);
            return m;
        }

        // Dust: a random puff of the sheet, turning slowly, swelling fast then slower, thinning out, slowed by the air.
        ParticleSystem Puffs(string name, int max)
        {
            var ps = NewSystem(name, max, out var r);
            r.sharedMaterial = Unlit(name, smoke, Color.white, false);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.maxParticleSize = 1.5f;
            var main = ps.main;
            main.gravityModifier = -.01f;
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
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .08f), new GradientAlphaKey(.5f, .45f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = .3f;
            drag.dampen = .08f;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .25f;
            noise.frequency = .45f;
            noise.scrollSpeed = .25f;
            noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.damping = true;
            return ps;
        }

        // Grains and chips: small irregular pieces lit by the room, tumbling, bouncing and settling, then shrinking away.
        ParticleSystem Pieces(string name, Color colour, int max, float gravity, float bounce)
        {
            var ps = NewSystem(name, max, out var r);
            var m = LitMaterials.NewLit(colour, .2f);
            m.name = name;
            made.Add(m);
            r.sharedMaterial = m;
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = chunk;
            r.alignment = ParticleSystemRenderSpace.World;
            var main = ps.main;
            main.gravityModifier = gravity;
            main.startRotation3D = true;
            main.startSize3D = true;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.75f, 1f), new Keyframe(1f, 0f)));
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.enableDynamicColliders = false;
            collision.bounce = new ParticleSystem.MinMaxCurve(bounce * .6f, bounce);
            collision.dampen = new ParticleSystem.MinMaxCurve(.35f, .6f);
            collision.radiusScale = .5f;
            collision.quality = ParticleSystemCollisionQuality.Low;
            return ps;
        }

        // Brass sparks: stretched by their speed, white-hot to gold to dull orange, skipping on the floor (HDR: they bloom).
        ParticleSystem Sparks()
        {
            var ps = NewSystem("G fx sparks", 120, out var r);
            r.sharedMaterial = Unlit("G fx sparks", dot, SparkHdr, true);
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = .045f;
            r.lengthScale = 1.2f;
            var main = ps.main;
            main.gravityModifier = 1.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 1f, .9f), 0f), new GradientColorKey(new Color(1f, .74f, .32f), .35f), new GradientColorKey(new Color(.8f, .3f, .06f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, .4f));
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.enableDynamicColliders = false;
            collision.bounce = new ParticleSystem.MinMaxCurve(.3f, .5f);
            collision.dampen = new ParticleSystem.MinMaxCurve(.2f, .4f);
            collision.lifetimeLoss = .25f;
            collision.radiusScale = .3f;
            collision.quality = ParticleSystemCollisionQuality.Low;
            return ps;
        }

        // Gold glints: four-pointed stars that twinkle and drift up (HDR).
        ParticleSystem Glints()
        {
            var ps = NewSystem("G fx glints", 60, out var r);
            r.sharedMaterial = Unlit("G fx glints", star, GlintHdr, true);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            var main = ps.main;
            main.gravityModifier = -.04f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .15f), new GradientAlphaKey(.8f, .6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = .1f;
            noise.frequency = 2f;
            noise.scrollSpeed = 3f;
            noise.sizeAmount = .9f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            return ps;
        }

        // A short light flash: a soft HDR glow that swells a little and is gone in a tenth of a second or so.
        ParticleSystem Flash()
        {
            var ps = NewSystem("G fx flash", 12, out var r);
            r.sharedMaterial = Unlit("G fx flash", dot, FlashHdr, true);
            r.renderMode = ParticleSystemRenderMode.Billboard;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.6f, .35f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .7f, 1f, 1.3f));
            return ps;
        }

        // ------------------------------------------------------------------ textures and the chunk mesh

        static void Textures()
        {
            if (smoke == null) smoke = SmokeSheet(96);
            if (dot == null) dot = Draw(64, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)), 1.6f));
            if (star == null)
                star = Draw(64, (x, y) =>
                {
                    float r = Mathf.Sqrt(x * x + y * y);
                    float rays = Mathf.Exp(-Mathf.Abs(x) * 14f) * Mathf.Exp(-y * y * 3f) + Mathf.Exp(-Mathf.Abs(y) * 14f) * Mathf.Exp(-x * x * 3f);
                    return Mathf.Clamp01(rays * .8f + Mathf.Exp(-r * r * 30f)) * Mathf.Clamp01(1f - r);
                });
            if (chunk == null) chunk = Chunk();
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

        // Sheet x Sheet ragged dust puffs (domain-warped fractal noise, lit from above), grey so the particle tints them.
        static Texture2D SmokeSheet(int n)
        {
            int size = n * Sheet;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "G fx dust" };
            var pixels = new Color32[size * size];
            for (int cell = 0; cell < Sheet * Sheet; cell++)
            {
                int cx = cell % Sheet, cy = cell / Sheet;
                float ox = 23.3f * cell + 3.1f, oy = 13.7f * cell + 7.9f;
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
                        float shade = Mathf.Clamp01(.66f + .26f * y + .18f * (knots - .5f) + .1f * (1f - r));
                        byte g = (byte)(shade * 255f);
                        pixels[(cy * n + j) * size + cx * n + i] = new Color32(g, g, g, (byte)(a * 255f));
                    }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        static Texture2D Draw(int n, System.Func<float, float, float> mask)
        {
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "G fx" };
            var pixels = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    byte b = (byte)(Mathf.Clamp01(mask((i + .5f) / n * 2f - 1f, (j + .5f) / n * 2f - 1f)) * 255f);
                    pixels[j * n + i] = new Color32(255, 255, 255, b);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // A unit chip of marble or a sand grain: a box with its corners pushed about, flat-shaded (24 vertices).
        static Mesh Chunk()
        {
            var rng = new System.Random(688);
            float J() => (float)(rng.NextDouble() - .5) * .45f;
            var corner = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corner[i] = new Vector3(((i & 1) == 0 ? -.5f : .5f) * 1.1f + J(), ((i & 2) == 0 ? -.5f : .5f) * .75f + J(), ((i & 4) == 0 ? -.5f : .5f) + J());
            int[][] faces =
            {
                new[] { 0, 2, 3, 1 }, new[] { 4, 5, 7, 6 }, new[] { 0, 1, 5, 4 },
                new[] { 2, 6, 7, 3 }, new[] { 0, 4, 6, 2 }, new[] { 1, 3, 7, 5 },
            };
            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (var f in faces)
            {
                int b = verts.Count;
                foreach (int k in f) verts.Add(corner[k]);
                tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            var mesh = new Mesh { name = "G fx chunk" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
