using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.VFX;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // R567 (승규 11-10 "처음 소리 구간에 불에 뒤덮히듯이 하고 바닥에 착지 하면 그 잔 불빛과 이런식으로"): the leap burns.
    //   smoulder  a few small flames lick its body as it gathers (the last 0.3 s of the warning)
    //   ignite    with the snort and hoof push (0.2 s into the leap) it bursts into flame: tongues of fire off its whole
    //             body, rising and left behind in the air as it flies, sparks of it streaming off, its own light flickering
    //   landing   the fire splashes out low along the floor and leaves the body in 0.35 s
    //   after     the floor it hit burns on: hot cracks cooling from white-gold to dark (VFX_CrackDecal, as the falling
    //             knights'), small flames licking at a few spots and dying down over about 2 s, embers drifting up, the
    //             warm light sinking - all gone before the next warning
    // The flames come off the knight's own part boxes (its hidden primitive parts under Visual follow its pose), so they
    // hug its shape; no smoke and no dust.
    // R568 (승규 11-10 "지금은 작은 불씨들이 모아져서 그게 불길을 만들잖아 그게 아니라 진짜 불길처럼 해줘"): not hundreds of
    // small soft tongues any more but a few dozen whole flame sheets (Resources/PawnRushHallFx/HallFlameSheet.shader:
    // tongues flowing up, ragged sharp edges, white-gold core to red tips) - 1.3-2.2 m flames off its parts, two or three
    // big sheets over its whole body, carried along with it; the floor's fire is the same flames, small and dying down.
    public sealed partial class CavalryWarnFx
    {
        const string CrackShader = "ChessFight/VFX/Crack Decal";
        const string FlameSheet = "PawnRushHallFx/HallFlameSheet";   // R568: Resources, so a build keeps it
        const float IgniteAt = .2f;           // s into the leap (the push-off in the sound)
        const float BodyOut = .35f;           // s the body's fire takes to leave after the landing
        const float FloorBurn = 2.2f;         // s the floor flames burn after the landing
        const float AfterLight = 2.6f;        // s the warm light sinks over
        static readonly Color FireLight = new Color(1f, .52f, .2f);
        static Texture2D tongue;

        ParticleSystem flames, cinders;
        Light glow;
        VfxCrackDecal scorch;
        readonly List<Transform> hull = new List<Transform>();
        readonly List<float> hullWeight = new List<float>();
        float hullTotal, bodyDebt, coreDebt, floorDebt, cinderDebt, ashDebt;
        Vector3 lastBody = new Vector3(float.NaN, 0f, 0f), carried;
        Vector3[] spots = new Vector3[0];
        bool ignited, splashed;
        float fireRun = -1f;

        void MakeFire()
        {
            if (tongue == null) tongue = Tongue(48, 96);
            // The knight's parts as boxes (their renderers are off: the model is one mesh that cannot be read).
            foreach (var mf in knight.Visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.name == "Eye") continue;
                var b = mf.sharedMesh.bounds;
                var s = Vector3.Scale(b.size, mf.transform.lossyScale);
                float area = s.x * s.y + s.y * s.z + s.x * s.z;
                if (area < .05f || area > 40f) continue;
                if (mf.GetComponent<Renderer>() is Renderer r && r.enabled && !mf.sharedMesh.isReadable && mf.sharedMesh.vertexCount > 5000) continue;
                hull.Add(mf.transform);
                hullWeight.Add(area);
                hullTotal += area;
            }

            // R568: each particle a whole flame sheet (HallFlameSheet.shader draws the fire in it), a few dozen at once.
            flames = System("Knight fire", 400, -.1f);
            var main = flames.main;
            main.startSize3D = true;
            main.startRotation = new ParticleSystem.MinMaxCurve(-.12f, .12f);
            var col = flames.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .12f), new GradientAlphaKey(1f, .55f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = flames.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .7f), new Keyframe(.3f, 1f), new Keyframe(1f, .85f)));
            var noise = flames.noise;
            noise.enabled = true;
            noise.strength = .35f;
            noise.frequency = .8f;
            noise.scrollSpeed = .8f;
            var fr = flames.GetComponent<ParticleSystemRenderer>();
            fr.renderMode = ParticleSystemRenderMode.VerticalBillboard;
            var sheet = Resources.Load<Shader>(FlameSheet);
            if (sheet == null) sheet = Shader.Find("ChessFight/VFX/Flame Sheet");
            if (sheet != null)
            {
                fr.sharedMaterial = new Material(sheet) { name = "Knight fire" };
                fr.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
                    ParticleSystemVertexStream.StableRandomX, ParticleSystemVertexStream.AgePercent
                });
            }
            else
            {
                Debug.LogWarning("[대계단 홀] 불길 셰이더가 없습니다: Resources/" + FlameSheet);
                fr.sharedMaterial = LitMaterials.NewAdditiveParticles(tongue, new Color(2.4f, 1.45f, .6f));
            }
            fr.shadowCastingMode = ShadowCastingMode.Off;
            fr.receiveShadows = false;
            fr.sortingFudge = -2f;
            flames.Play();

            cinders = System("Knight fire cinders", 600, -.12f);
            var cc = cinders.colorOverLifetime;
            cc.enabled = true;
            var cg = new Gradient();
            cg.SetKeys(new[] { new GradientColorKey(new Color(1f, .85f, .55f), 0f), new GradientColorKey(new Color(1f, .4f, .1f), .6f),
                               new GradientColorKey(new Color(.6f, .12f, .03f), 1f) },
                       new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, .7f), new GradientAlphaKey(0f, 1f) });
            cc.color = cg;
            var cn = cinders.noise;
            cn.enabled = true;
            cn.strength = .6f;
            cn.frequency = .9f;
            var cr = cinders.GetComponent<ParticleSystemRenderer>();
            cr.renderMode = ParticleSystemRenderMode.Stretch;
            cr.velocityScale = .03f;
            cr.lengthScale = 1.2f;
            cr.sharedMaterial = LitMaterials.NewAdditiveParticles(dot, new Color(3f, 1.7f, .6f));
            cr.shadowCastingMode = ShadowCastingMode.Off;
            cr.receiveShadows = false;
            cinders.Play();

            var lamp = new GameObject("Knight fire light");
            lamp.transform.SetParent(transform, false);
            glow = lamp.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = FireLight;
            glow.range = 8f;
            glow.intensity = 0f;
            glow.shadows = LightShadows.None;
            glow.enabled = false;

            var shader = Shader.Find(CrackShader);
            if (shader != null)
            {
                var floor = new GameObject("Knight scorch");
                floor.transform.SetParent(transform, false);
                floor.transform.position = knight.LandingPoint.position;
                var part = new GameObject("Knight scorch cracks");
                part.transform.SetParent(floor.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                var view = part.AddComponent<MeshRenderer>();
                view.sharedMaterial = new Material(shader) { name = "Knight scorch cracks" };
                view.shadowCastingMode = ShadowCastingMode.Off;
                view.receiveShadows = false;
                view.lightProbeUsage = LightProbeUsage.Off;
                view.reflectionProbeUsage = ReflectionProbeUsage.Off;
                scorch = part.AddComponent<VfxCrackDecal>();
                scorch.maxReach = 2.3f;
                scorch.grow = .1f;
                scorch.cool = 1.8f;
                scorch.fadeOut = 1f;
                scorch.seed = Mathf.Repeat(transform.position.x * 1.37f + transform.position.z * .71f, 97f);
            }
        }

        void Fire(float a, float leapAt, float landAt, float runEnd)
        {
            if (flames == null) return;
            if (a < fireRun - .5f || a < 0f) { ignited = splashed = false; }
            fireRun = a;
            float dt = Time.deltaTime;
            float since = a - landAt;
            var body = knight.KnightRoot.transform.position;
            var landing = knight.LandingPoint.position;

            if (scorch != null)
            {
                var d = VfxDrop.Default;
                d.lead = landAt;
                scorch.drop = d;
                scorch.life = Mathf.Max(.5f, Mathf.Min(3.6f, runEnd - landAt - .05f));
                scorch.Seek(a < 0f ? -1f : a);
            }
            if (a < 0f)
            {
                glow.enabled = false;
                return;
            }

            // The body's fire: a smoulder as it gathers, a burst at the push-off, burning through the air, leaving it
            // as it lands.
            // R568: whole flame sheets, a few dozen alive at once, carried along with it (most of its speed, so they
            // stream back off it a little instead of hanging in the air).
            var moved = dt > 0f && !float.IsNaN(lastBody.x) ? (body - lastBody) / dt : Vector3.zero;
            if (moved.sqrMagnitude > 400f) moved = Vector3.zero;   // a jump of the clock, not a move
            carried = moved;
            lastBody = body;
            float rate = 0f, core = 0f, small = 1f;
            float ignite = leapAt + IgniteAt;
            if (a >= leapAt - .3f && a < ignite) { rate = 10f; small = .45f; }
            else if (a >= ignite && a < landAt) { rate = 60f; core = 12f; }
            else if (since >= 0f && since < BodyOut)
            {
                float k = (1f - since / BodyOut) * (1f - since / BodyOut);
                rate = 60f * k;
                core = 12f * k;
            }
            if (!ignited && a >= ignite && a < ignite + .3f)
            {
                ignited = true;
                for (int i = 0; i < 18; i++) BodyFlame(1.15f);
                for (int i = 0; i < 4; i++) CoreFlame();
                for (int i = 0; i < 16; i++) BodyCinder();
            }
            Emit(ref bodyDebt, rate * dt, () => BodyFlame(small));
            Emit(ref coreDebt, core * dt, CoreFlame);
            if (core > 4f) Emit(ref cinderDebt, 28f * dt, BodyCinder);

            // The splash on the floor and the burning after it.
            if (!splashed && since >= 0f && since < .3f)
            {
                splashed = true;
                Splash(landing);
            }
            if (since >= 0f && since < FloorBurn)
            {
                float left = 1f - since / FloorBurn;
                Emit(ref floorDebt, 30f * left * dt, () => FloorFlame(landing, left));
            }
            if (since >= 0f && since < AfterLight + .5f)
                Emit(ref ashDebt, 22f * Mathf.Clamp01(1f - since / (AfterLight + .5f)) * dt, () => FloorCinder(landing));

            // The fire's own light: on the knight while it burns, then on the floor it left, sinking.
            float flicker = .8f + .2f * Mathf.PerlinNoise(Time.time * 9f, transform.position.x) + .15f * Mathf.Sin(Time.time * 37f);
            float power = 0f;
            Vector3 at = body + Vector3.up * 1.6f;
            if (a >= leapAt - .3f && a < ignite) power = 1.2f * (a - (leapAt - .3f)) / .3f;
            else if (a >= ignite && a < landAt) power = Mathf.Lerp(7f, 5f, (a - ignite) / Mathf.Max(.05f, landAt - ignite));
            else if (since >= 0f && since < AfterLight)
            {
                float k = since / AfterLight;
                power = since < .08f ? 13f : Mathf.Lerp(9f, 0f, Mathf.Sqrt(k));
                at = landing + Vector3.up * Mathf.Lerp(1.2f, .5f, k);
            }
            glow.enabled = power > .01f;
            glow.intensity = power * flicker;
            glow.transform.position = at;
        }

        // A flame off a random part of its body: its foot on the part, licking up past it.
        void BodyFlame(float scale)
        {
            if (!Surface(out var at, out var outward)) return;
            float w = Random.Range(.8f, 1.3f) * scale, h = Random.Range(1.3f, 2.2f) * scale;
            flames.Emit(new ParticleSystem.EmitParams
            {
                position = at + outward * .08f + Vector3.up * h * .32f,
                velocity = carried * .85f + Vector3.up * Random.Range(.8f, 1.6f) + outward * Random.Range(.1f, .4f),
                startLifetime = Random.Range(.45f, .7f), startSize3D = new Vector3(w, h, 1f), startColor = Color.white
            }, 1);
        }

        // A big sheet over its whole body, so it reads as one burning shape.
        void CoreFlame()
        {
            var at = knight.KnightRoot.transform.position + Vector3.up * 1.9f + Random.insideUnitSphere * .35f;
            float w = Random.Range(1.9f, 2.5f), h = Random.Range(2.8f, 3.6f);
            flames.Emit(new ParticleSystem.EmitParams
            {
                position = at, velocity = carried * .9f + Vector3.up * Random.Range(.4f, .9f),
                startLifetime = Random.Range(.5f, .75f), startSize3D = new Vector3(w, h, 1f), startColor = Color.white
            }, 1);
        }

        void BodyCinder()
        {
            if (!Surface(out var at, out var outward)) return;
            cinders.Emit(new ParticleSystem.EmitParams
            {
                position = at, velocity = Vector3.up * Random.Range(1f, 3f) + outward * Random.Range(.5f, 2f) + Random.insideUnitSphere,
                startLifetime = Random.Range(.5f, 1.1f), startSize = Random.Range(.025f, .05f), startColor = Color.white
            }, 1);
        }

        // The fire thrown out low along the floor as it lands, and the spots that go on burning.
        void Splash(Vector3 landing)
        {
            for (int i = 0; i < 20; i++)
            {
                float t = i / 20f * Mathf.PI * 2f + Random.Range(-.12f, .12f);
                var radial = new Vector3(Mathf.Cos(t), 0f, Mathf.Sin(t));
                float w = Random.Range(1.3f, 1.9f), h = Random.Range(.8f, 1.25f);
                flames.Emit(new ParticleSystem.EmitParams
                {
                    position = landing + radial * Random.Range(.6f, 1f) + Vector3.up * h * .3f,
                    velocity = radial * Random.Range(2.5f, 4.5f) + Vector3.up * Random.Range(.3f, .7f),
                    startLifetime = Random.Range(.35f, .5f), startSize3D = new Vector3(w, h, 1f), startColor = Color.white
                }, 1);
            }
            for (int i = 0; i < 40; i++)
            {
                var d = Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) * .7f + .2f;
                cinders.Emit(new ParticleSystem.EmitParams
                {
                    position = landing + Vector3.up * .3f, velocity = d.normalized * Random.Range(2f, 5f),
                    startLifetime = Random.Range(.6f, 1.3f), startSize = Random.Range(.025f, .05f), startColor = Color.white
                }, 1);
            }
            // Where it goes on burning: a few spots round the hoofprints, more near the middle.
            int n = Random.Range(6, 9);
            spots = new Vector3[n];
            float r = knight.LandingRadius;
            for (int i = 0; i < n; i++)
            {
                float t = Random.value * Mathf.PI * 2f, d = Mathf.Sqrt(Random.value) * r * .85f;
                spots[i] = landing + new Vector3(Mathf.Cos(t) * d, .03f, Mathf.Sin(t) * d);
            }
        }

        void FloorFlame(Vector3 landing, float left)
        {
            if (spots.Length == 0) return;
            var at = spots[Random.Range(0, spots.Length)] + new Vector3(Random.Range(-.15f, .15f), 0f, Random.Range(-.15f, .15f));
            float s = .5f + .5f * left, w = Random.Range(.45f, .8f) * s, h = Random.Range(.7f, 1.25f) * s;
            flames.Emit(new ParticleSystem.EmitParams
            {
                position = at + Vector3.up * h * .33f, velocity = Vector3.up * Random.Range(.3f, .7f),
                startLifetime = Random.Range(.5f, .8f), startSize3D = new Vector3(w, h, 1f), startColor = Color.white
            }, 1);
        }

        void FloorCinder(Vector3 landing)
        {
            var at = spots.Length > 0 ? spots[Random.Range(0, spots.Length)] : landing;
            cinders.Emit(new ParticleSystem.EmitParams
            {
                position = at + Vector3.up * .1f, velocity = Vector3.up * Random.Range(.4f, 1.1f) + Random.insideUnitSphere * .3f,
                startLifetime = Random.Range(1f, 2f), startSize = Random.Range(.018f, .035f), startColor = Color.white
            }, 1);
        }

        // A random point on one of its parts' faces (by area) and that face's outward direction, in the world.
        bool Surface(out Vector3 at, out Vector3 outward)
        {
            at = knight.KnightRoot.transform.position + Vector3.up * 1.5f;
            outward = Vector3.up;
            if (hull.Count == 0) return true;
            float pick = Random.value * hullTotal;
            int i = 0;
            while (i < hull.Count - 1 && (pick -= hullWeight[i]) > 0f) i++;
            var part = hull[i];
            var mf = part.GetComponent<MeshFilter>();
            var b = mf.sharedMesh.bounds;
            var s = Vector3.Scale(b.size, part.lossyScale);
            float ax = s.y * s.z, ay = s.x * s.z, az = s.x * s.y, f = Random.value * (ax + ay + az);
            var local = new Vector3(Random.Range(-.5f, .5f), Random.Range(-.5f, .5f), Random.Range(-.5f, .5f));
            var normal = Vector3.zero;
            float sign = Random.value < .5f ? -1f : 1f;
            if (f < ax) { local.x = .5f * sign; normal.x = sign; }
            else if (f < ax + ay) { local.y = .5f * sign; normal.y = sign; }
            else { local.z = .5f * sign; normal.z = sign; }
            at = part.TransformPoint(b.center + Vector3.Scale(local, b.size));
            outward = part.TransformDirection(normal).normalized;
            return true;
        }

        // A flame tongue: round and bright at its foot, narrowing and wavering to a tip.
        static Texture2D Tongue(int w, int h)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Knight fire tongue" };
            var pixels = new Color32[w * h];
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    float u = (i + .5f) / w * 2f - 1f, v = (j + .5f) / h;
                    float wave = .08f * Mathf.Sin(v * 9f + 1.3f) * v;
                    float width = .82f * Mathf.Pow(1f - v, .75f) * Mathf.Clamp01(v * 3.5f + .45f);
                    float x = Mathf.Abs(u - wave) / Mathf.Max(.001f, width);
                    float body = Mathf.Clamp01(1f - x);
                    body = body * body * (3f - 2f * body);
                    float foot = Mathf.Clamp01(v * 5f);
                    float a = body * foot * (1f - v * v * .6f);
                    byte c = (byte)(Mathf.Clamp01(a) * 255f);
                    pixels[j * w + i] = new Color32(c, c, c, c);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
