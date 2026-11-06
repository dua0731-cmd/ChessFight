using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R417 (승규 11-05 "대포 vfx 적용 시켜 공기 대포가 팡 터지게"): the war room's blower cannons pop when they fire.
    // LibraryAirCannon drives it from its round on the shared obstacle clock (the same edges as its sounds), so every
    // PC sees the same shot without a packet. Pictures only: no collider, nothing it pushes (the cannon's own blast
    // column does that).
    //   charge (2.6 s since R418, was 1.4 s) pale air spirals in to the muzzle from around it, faster and thicker as
    //                   the fan spins up; rings of air close in on the muzzle, quicker and quicker; a glow fills the
    //                   muzzle and its core heats white; in the last third sparks crackle round the rim, the light
    //                   builds and everything flickers, then a last breath in just before the shot
    //   fire            a white-gold flash at the muzzle, two shock rings thrown out along the shot, air streaks shot
    //                   down the 12 m blast column, a cloud of air puffs bursting out and slowing, a few bright flecks,
    //                   a quick light on the hall
    // Built in code with its own small textures, as the jump pad's (JumpPadFx). It stands at the muzzle on the hall, not
    // on the cannon, so the barrel's kick does not drag the rings back.
    public sealed class LibraryAirCannonFx : MonoBehaviour
    {
        static readonly Color Air = new Color(.85f, 1.15f, 1.25f), Flash = new Color(3.2f, 2.8f, 2.2f);
        static Texture2D dot, ring, puff;
        const float FlashTime = .14f, RingTime = .45f, LightTime = .3f;

        float radius;
        ParticleSystem intake, streaks, puffs, flecks, crackle;
        Renderer glow, core, flash, ringA, ringB;
        // R418: the closing rings of the charge, a small pool reused
        readonly Renderer[] closing = new Renderer[4];
        readonly float[] closingAt = { -10f, -10f, -10f, -10f };
        const float ClosingTime = .5f;
        float nextClosing;
        int closingNext;
        Light lamp;
        MaterialPropertyBlock block;
        float firedAt = -10f, charge;

        public static LibraryAirCannonFx Build(Transform fan, Vector3 dir, Transform parent)
        {
            var go = new GameObject("Air cannon fx");
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(fan.position, Quaternion.LookRotation(dir, Vector3.up));
            var fx = go.AddComponent<LibraryAirCannonFx>();
            float r = 1f;
            var views = fan.GetComponentsInChildren<Renderer>(true);
            if (views.Length > 0)
            {
                var box = views[0].bounds;
                foreach (var v in views) box.Encapsulate(v.bounds);
                var e = box.extents;
                r = Mathf.Clamp(Mathf.Max(e.x, Mathf.Max(e.y, e.z)), .4f, 2f);   // the fan disc is thin along the shot
            }
            fx.Make(r);
            return fx;
        }

        void Make(float r)
        {
            radius = r;
            block = new MaterialPropertyBlock();
            Textures();
            glow = Disc("Air cannon charge glow", ring, r * 2.3f, .05f, true);
            flash = Disc("Air cannon flash", dot, r * 3f, .25f, true);
            ringA = Disc("Air cannon shock ring A", ring, r * 2f, .3f, true);
            ringB = Disc("Air cannon shock ring B", ring, r * 2f, .3f, true);
            core = Disc("Air cannon charge core", dot, r * 1.6f, .08f, true);
            for (int i = 0; i < closing.Length; i++)
            {
                closing[i] = Disc("Air cannon closing ring " + i, ring, r * 4f, 1f, true);
                closing[i].enabled = false;
            }
            glow.enabled = core.enabled = flash.enabled = ringA.enabled = ringB.enabled = false;

            // The intake turns round the shot's axis as it closes in (local, so the orbit is about the muzzle).
            intake = Particles("Air cannon intake", dot, Air * .9f, true, true);
            Fade(intake, 1f, .1f);
            var im = intake.main;
            im.simulationSpace = ParticleSystemSimulationSpace.Local;
            im.maxParticles = 600;
            var orbit = intake.velocityOverLifetime;
            orbit.enabled = true;
            orbit.space = ParticleSystemSimulationSpace.Local;
            orbit.x = orbit.y = orbit.z = new ParticleSystem.MinMaxCurve(0f);
            orbit.orbitalZ = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            crackle = Particles("Air cannon crackle", dot, new Color(2.6f, 2.1f, 1.3f), true, true);
            Fade(crackle, 1f, 0f);
            streaks = Particles("Air cannon streaks", dot, Air * 1.6f, true, true);
            Fade(streaks, 1f, 0f);
            puffs = Particles("Air cannon puffs", puff, new Color(.92f, .96f, 1f, .55f), false, false);
            Fade(puffs, .55f, 0f);
            var grow = puffs.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 2.8f));
            var drag = puffs.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 1.2f;
            drag.dampen = .12f;
            flecks = Particles("Air cannon flecks", dot, new Color(2.4f, 1.9f, 1.1f), true, true);
            Fade(flecks, 1f, 0f);
            var fall = flecks.main;
            fall.gravityModifier = 1.2f;

            var light = new GameObject("Air cannon flash light");
            light.transform.SetParent(transform, false);
            light.transform.localPosition = Vector3.forward * .6f;
            lamp = light.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, .93f, .82f);
            lamp.range = 10f;
            lamp.shadows = LightShadows.None;
            lamp.intensity = 0f;
            lamp.enabled = false;
        }

        // c: 0 .. 1 through the charge, below 0 outside it. Called by the cannon every frame.
        public void Charge(float c)
        {
            charge = c;
            if (c < 0f || !isActiveAndEnabled) return;
            // Pale air drawn in from a ring around and in front of the muzzle, spiralling (the intake's orbit), more and
            // faster as the fan spins up, from wider at the start; a last breath in just before the shot.
            float rate = Mathf.Lerp(14f, 170f, c * c) * (c > .93f ? 1.8f : 1f);
            int n = Mathf.FloorToInt(Time.deltaTime * rate + Random.value);
            var e = new ParticleSystem.EmitParams { startColor = Color.white };
            for (int i = 0; i < n; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var round = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var from = round * (radius * Random.Range(1.4f, Mathf.Lerp(3.4f, 2.4f, c))) + Vector3.forward * Random.Range(.3f, Mathf.Lerp(3f, 1.8f, c));
                var to = round * (radius * .2f);
                float life = Random.Range(.3f, .5f) * Mathf.Lerp(1.5f, .7f, c);
                e.position = from;                       // the intake simulates in the muzzle's own frame
                e.velocity = (to - from) / life;
                e.startLifetime = life;
                e.startSize = Random.Range(.04f, .09f) * Mathf.Lerp(.8f, 1.3f, c);
                intake.Emit(e, 1);
            }
            // The last third: sparks crackling round the rim, thrown round it and a little out.
            if (c > .62f)
            {
                int k = Mathf.FloorToInt(Time.deltaTime * Mathf.Lerp(10f, 70f, (c - .62f) / .38f) + Random.value);
                for (int i = 0; i < k; i++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    var round = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    var spin = new Vector3(-round.y, round.x, 0f);
                    e.position = transform.TransformPoint(round * radius * Random.Range(.85f, 1.05f) + Vector3.forward * .1f);
                    e.velocity = transform.TransformVector(spin * Random.Range(2f, 5f) + round * Random.Range(.3f, 1.2f) + Vector3.forward * Random.Range(0f, 1f));
                    e.startLifetime = Random.Range(.12f, .26f);
                    e.startSize = Random.Range(.025f, .05f);
                    crackle.Emit(e, 1);
                }
            }
        }

        // The shot.
        public void Fire()
        {
            if (!isActiveAndEnabled) return;
            firedAt = Time.time;
            var e = new ParticleSystem.EmitParams { startColor = Color.white };
            Vector3 fwd = transform.forward;
            for (int i = 0; i < 46; i++)
            {
                var p = Random.insideUnitCircle * radius * .85f;
                e.position = transform.TransformPoint(new Vector3(p.x, p.y, Random.Range(0f, .6f)));
                e.velocity = fwd * Random.Range(16f, 27f) + transform.TransformVector(new Vector3(p.x, p.y, 0f)) * Random.Range(.4f, 1.4f);
                e.startLifetime = Random.Range(.32f, .55f);
                e.startSize = Random.Range(.06f, .13f);
                streaks.Emit(e, 1);
            }
            for (int i = 0; i < 20; i++)
            {
                var p = Random.insideUnitCircle * radius * .7f;
                e.position = transform.TransformPoint(new Vector3(p.x, p.y, Random.Range(.2f, 1.4f)));
                e.velocity = fwd * Random.Range(5f, 12f) + Random.insideUnitSphere * 2.2f;
                e.startLifetime = Random.Range(.7f, 1.15f);
                e.startSize = radius * Random.Range(.55f, 1f);
                e.rotation = Random.Range(0f, 360f);
                puffs.Emit(e, 1);
            }
            e.rotation = 0f;
            for (int i = 0; i < 14; i++)
            {
                var p = Random.insideUnitCircle.normalized * radius * Random.Range(.6f, 1f);
                e.position = transform.TransformPoint(new Vector3(p.x, p.y, .3f));
                e.velocity = fwd * Random.Range(5f, 9f) + transform.TransformVector(new Vector3(p.x, p.y, 0f)).normalized * Random.Range(2f, 5f)
                           + Vector3.up * Random.Range(1f, 3f);
                e.startLifetime = Random.Range(.4f, .8f);
                e.startSize = Random.Range(.04f, .07f);
                flecks.Emit(e, 1);
            }
        }

        void LateUpdate()
        {
            // The charge glow grows in with the charge, its core heating white; both flicker in the last third.
            float flicker = 1f;
            if (charge >= 0f)
            {
                if (charge > .62f) flicker = .72f + .28f * Mathf.Sin(Time.time * Mathf.Lerp(40f, 90f, charge)) * Random.Range(.7f, 1f);
                Show(glow, Air * (1.7f * charge * charge * flicker), radius * Mathf.Lerp(2.8f, 2.1f, charge), .05f);
                Show(core, Color.Lerp(Air, Flash, charge) * (1.4f * charge * charge * charge * flicker), radius * Mathf.Lerp(.8f, 1.9f, charge), .08f);
                // Rings of air closing in on the muzzle, every half second at first, every sixth at the end.
                if (Time.time >= nextClosing)
                {
                    closingAt[closingNext] = Time.time;
                    closingNext = (closingNext + 1) % closing.Length;
                    nextClosing = Time.time + Mathf.Lerp(.5f, .17f, charge);
                }
            }
            else
            {
                glow.enabled = core.enabled = false;
                nextClosing = 0f;
            }
            for (int i = 0; i < closing.Length; i++)
            {
                float u = (Time.time - closingAt[i]) / ClosingTime;
                if (u < 0f || u >= 1f || charge < 0f) { closing[i].enabled = false; continue; }
                float ease = u * u;
                float a = Mathf.Sin(u * Mathf.PI) * Mathf.Lerp(.5f, 1.4f, Mathf.Max(charge, 0f));
                Show(closing[i], Air * a, radius * Mathf.Lerp(4.6f, 1.05f, ease), Mathf.Lerp(2.6f, .15f, ease));
            }

            float s = Time.time - firedAt;
            if (s < FlashTime)
            {
                float k = s / FlashTime;
                Show(flash, Flash * (1f - k * k), radius * Mathf.Lerp(2.4f, 4.4f, Mathf.Sqrt(k)), .25f);
            }
            else flash.enabled = false;
            Ring(ringA, s, 3.6f, 5.2f);
            Ring(ringB, s - .08f, 6.5f, 6.5f);
            if (s < LightTime)
            {
                lamp.enabled = true;
                lamp.intensity = 9f * Mathf.Exp(-s * 13f);
            }
            else if (charge > .3f)
            {
                // the light builds through the charge, flickering with the glow
                lamp.enabled = true;
                float b = (charge - .3f) / .7f;
                lamp.intensity = 3.2f * b * b * flicker;
            }
            else if (lamp.enabled)
            {
                lamp.intensity = 0f;
                lamp.enabled = false;
            }
        }

        // A shock ring thrown `travel` metres down the shot, growing to `grow` muzzle radii, fading out.
        void Ring(Renderer r, float s, float travel, float grow)
        {
            if (s < 0f || s >= RingTime) { r.enabled = false; return; }
            float k = s / RingTime;
            float ease = 1f - (1f - k) * (1f - k);
            Show(r, Air * (2.2f * (1f - k)), radius * Mathf.Lerp(1.8f, grow, ease), .3f + travel * ease);
        }

        void Show(Renderer r, Color c, float size, float ahead)
        {
            r.enabled = true;
            r.transform.localPosition = Vector3.forward * ahead;
            r.transform.localScale = Vector3.one * size;
            r.GetPropertyBlock(block);
            LitMaterials.SetColor(block, c);
            r.SetPropertyBlock(block);
            block.Clear();
        }

        // ------------------------------------------------------------------ parts

        Renderer Disc(string name, Texture2D texture, float size, float ahead, bool additive)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = name;
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = Vector3.forward * ahead;   // facing along the shot, both sides drawn
            quad.transform.localScale = Vector3.one * size;
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = MakeMaterial(texture, Color.white, additive);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        ParticleSystem Particles(string name, Texture2D texture, Color tint, bool additive, bool stretch)
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
            main.maxParticles = 400;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MakeMaterial(texture, tint, additive);
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.velocityScale = .05f;
            r.lengthScale = 1.5f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static Material MakeMaterial(Texture2D texture, Color tint, bool additive)
        {
            var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
            if (shader == null)
            {
                Debug.LogWarning("[작전 회의실] 대포 VFX 셰이더가 없습니다: " + LitMaterials.ParticlesUnlitName);
                return null;
            }
            var m = new Material(shader) { mainTexture = texture, color = tint, name = "Air cannon fx" };
            LitMaterials.MakeTransparent(m, additive);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            return m;
        }

        static void Fade(ParticleSystem ps, float peak, float end)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, .1f), new GradientAlphaKey(end, 1f) });
            col.color = g;
        }

        // ------------------------------------------------------------------ textures (white, alpha and colour alike)

        static void Textures()
        {
            if (dot != null) return;
            dot = Draw(64, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)), 2f));
            ring = Draw(128, (x, y) =>
            {
                float d = Mathf.Sqrt(x * x + y * y) - .78f;
                return Mathf.Clamp01(Mathf.Exp(-Mathf.Pow(d / .06f, 2f)) + .3f * Mathf.Exp(-Mathf.Pow(d / .18f, 2f)));
            });
            puff = Draw(128, (x, y) =>
            {
                // A soft, lumpy cloud: a round falloff broken by a few lobes.
                float r = Mathf.Sqrt(x * x + y * y), a = Mathf.Atan2(y, x);
                float edge = .78f + .1f * Mathf.Sin(a * 3f + 1f) + .06f * Mathf.Sin(a * 7f + 2f);
                float body = Mathf.Clamp01(1f - r / edge);
                float lumps = .75f + .25f * Mathf.PerlinNoise(x * 3f + 7f, y * 3f + 3f);
                return Mathf.Pow(body, 1.4f) * lumps;
            });
        }

        static Texture2D Draw(int n, System.Func<float, float, float> mask)
        {
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Air cannon fx" };
            var pixels = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float v = Mathf.Clamp01(mask((i + .5f) / n * 2f - 1f, (j + .5f) / n * 2f - 1f));
                    byte b = (byte)(v * 255f);
                    pixels[j * n + i] = new Color32(255, 255, 255, b);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
