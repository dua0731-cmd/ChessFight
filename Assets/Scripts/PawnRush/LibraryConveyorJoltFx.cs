using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R443 (승규 11-05 "컨베이어 벨트 멈출 때 덜컹 ... 덜컹 할 때 파티클"): what the conveyor carriage throws off when it slams
    // into the end of its run (LibraryShuttleDriver's jolting stop), on each impact the driver counts from the shared
    // clock, so every PC sees it at the same moment (pictures only: nothing collides, nothing is pushed).
    //   dust    shaken off the carriage's leading end and its two sides, thrown on the way it was going and up,
    //           slowing and spreading
    //   chips   small dark bits of wood and grit hopping off the belt and falling back
    //   sparks  from each rail shoe (Carriage_rail_shoe, or the chassis' lower corners), skidding on along the rails
    // Built in code with its own small textures, as the cannons' (LibraryAirCannonFx); LibraryConveyorSound adds it at
    // Play.
    public sealed class LibraryConveyorJoltFx : MonoBehaviour
    {
        static readonly Color Dust = new Color(.6f, .55f, .48f, .5f), Chip = new Color(.22f, .15f, .09f, 1f);
        static readonly Color Spark = new Color(3.2f, 1.7f, .55f);
        static Texture2D dot, puff;

        [SerializeField] LibraryShuttleDriver driver;
        [SerializeField] Transform carriage;

        ParticleSystem dust, chips, sparks;
        Bounds body, deck;                       // in the carriage's own space
        Transform[] shoes;
        int seen;

        public void Configure(LibraryShuttleDriver shuttle, Transform movingCarriage)
        {
            driver = shuttle;
            carriage = movingCarriage;
        }

        void Start()
        {
            if (driver == null || carriage == null) { enabled = false; return; }
            Measure();
            Textures();
            dust = Particles("Conveyor jolt dust", puff, Dust, false, false, 300);
            Fade(dust, 1f, 0f);
            var grow = dust.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 3f));
            var drag = dust.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = .6f;
            drag.dampen = .1f;
            var lift = dust.main;
            lift.gravityModifier = -.03f;
            chips = Particles("Conveyor jolt chips", dot, Chip, false, false, 200);
            Fade(chips, 1f, .6f);
            var fall = chips.main;
            fall.gravityModifier = 1f;
            sparks = Particles("Conveyor jolt sparks", dot, Spark, true, true, 300);
            Fade(sparks, 1f, 0f);
            var drop = sparks.main;
            drop.gravityModifier = 1.3f;
            seen = driver.Jolts;
        }

        // The carriage's box and the belt's top, in the carriage's space; the rail shoes, if the model has them.
        void Measure()
        {
            bool any = false, anyDeck = false;
            var found = new System.Collections.Generic.List<Transform>();
            foreach (var r in carriage.GetComponentsInChildren<Renderer>(true))
            {
                var b = LocalBounds(r);
                if (!any) { body = b; any = true; } else body.Encapsulate(b);
                if (r.name.StartsWith("BELT_LINK_"))
                {
                    if (!anyDeck) { deck = b; anyDeck = true; } else deck.Encapsulate(b);
                }
                if (r.name.StartsWith("Carriage_rail_shoe")) found.Add(r.transform);
            }
            if (!anyDeck) deck = body;
            shoes = found.ToArray();
        }

        Bounds LocalBounds(Renderer r)
        {
            var w = r.bounds;
            var b = new Bounds(carriage.InverseTransformPoint(w.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var c = w.center + Vector3.Scale(w.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                b.Encapsulate(carriage.InverseTransformPoint(c));
            }
            return b;
        }

        void LateUpdate()
        {
            if (driver.Jolts == seen) return;
            seen = driver.Jolts;
            Burst(driver.JoltFront);
        }

        static int Dominant(Vector3 v, int skip)
        {
            int best = -1;
            for (int i = 0; i < 3; i++)
                if (i != skip && (best < 0 || Mathf.Abs(v[i]) > Mathf.Abs(v[best]))) best = i;
            return best;
        }

        void Burst(Vector3 front)
        {
            Vector3 up = Vector3.up, side = Vector3.Cross(up, front).normalized;
            // which of the carriage's own axes is up, which the travel and which across (the FBX may turn them)
            Vector3 upLocal = carriage.InverseTransformDirection(up), f = carriage.InverseTransformDirection(front);
            int ui = Dominant(upLocal, -1), ai = Dominant(f, ui), ci = 3 - ui - ai;
            float upSign = Mathf.Sign(upLocal[ui]), aheadSign = Mathf.Sign(f[ai]);
            float top = upSign > 0 ? deck.max[ui] : deck.min[ui], bottom = upSign > 0 ? body.min[ui] : body.max[ui];
            var e = new ParticleSystem.EmitParams();

            // a point on the carriage: alongFrac -1 .. 1 from its back to its front, acrossFrac over the belt's width,
            // on the belt's top (or the carriage's bottom), lifted `lift` metres
            Vector3 Local(float alongFrac, float acrossFrac, float lift, bool low = false)
            {
                var p = deck.center;
                p[ai] = body.center[ai] + body.extents[ai] * alongFrac * aheadSign;
                p[ci] = deck.center[ci] + deck.extents[ci] * acrossFrac;
                p[ui] = low ? bottom : top;
                return carriage.TransformPoint(p) + up * lift;
            }

            // dust off the leading end, thrown on and up
            for (int i = 0; i < 16; i++)
            {
                e.position = Local(Random.Range(.92f, 1.02f), Random.Range(-1f, 1f), Random.Range(-.2f, .05f));
                e.velocity = front * Random.Range(1.5f, 3.5f) + up * Random.Range(.6f, 1.8f) + side * Random.Range(-.9f, .9f);
                e.startLifetime = Random.Range(1f, 1.7f);
                e.startSize = Random.Range(.35f, .7f);
                e.rotation = Random.Range(0f, 360f);
                e.startColor = Color.white;
                dust.Emit(e, 1);
            }
            // dust shaken off both sides, all along
            for (int i = 0; i < 18; i++)
            {
                float across = Random.value < .5f ? -1.05f : 1.05f;
                e.position = Local(Random.Range(-.9f, .95f), across, Random.Range(-.15f, .05f));
                e.velocity = front * Random.Range(.6f, 1.6f) + up * Random.Range(.3f, 1f) + side * (across * Random.Range(.4f, 1.1f));
                e.startLifetime = Random.Range(.8f, 1.4f);
                e.startSize = Random.Range(.25f, .5f);
                e.rotation = Random.Range(0f, 360f);
                dust.Emit(e, 1);
            }
            e.rotation = 0f;
            // chips hopping off the belt
            for (int i = 0; i < 24; i++)
            {
                e.position = Local(Random.Range(-.9f, .95f), Random.Range(-.9f, .9f), .02f);
                e.velocity = up * Random.Range(1.8f, 3.6f) + front * Random.Range(.6f, 2.2f) + side * Random.Range(-.6f, .6f);
                e.startLifetime = Random.Range(.55f, .95f);
                e.startSize = Random.Range(.03f, .06f);
                chips.Emit(e, 1);
            }
            // sparks skidding on from the rail shoes
            int shoeCount = shoes.Length > 0 ? shoes.Length : 4;
            for (int k = 0; k < shoeCount; k++)
            {
                Vector3 at;
                if (shoes.Length > 0) at = shoes[k].position;
                else at = Local((k & 1) == 0 ? -.95f : .95f, (k & 2) == 0 ? -1f : 1f, .05f, true);
                for (int i = 0; i < 12; i++)
                {
                    e.position = at + Random.insideUnitSphere * .05f;
                    e.velocity = front * Random.Range(2.5f, 6f) + up * Random.Range(.8f, 2.6f) + side * Random.Range(-1.2f, 1.2f);
                    e.startLifetime = Random.Range(.22f, .45f);
                    e.startSize = Random.Range(.025f, .045f);
                    sparks.Emit(e, 1);
                }
            }
        }

        // ------------------------------------------------------------------ parts

        ParticleSystem Particles(string name, Texture2D texture, Color tint, bool additive, bool stretch, int max)
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
                var m = new Material(shader) { mainTexture = texture, color = tint, name = name };
                LitMaterials.MakeTransparent(m, additive);
                r.sharedMaterial = m;
            }
            else Debug.LogWarning("[작전 회의실] 컨베이어 파티클 셰이더가 없습니다: " + LitMaterials.ParticlesUnlitName);
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.velocityScale = .06f;
            r.lengthScale = 1.5f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static void Fade(ParticleSystem ps, float peak, float end)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, .08f), new GradientAlphaKey(end, 1f) });
            col.color = g;
        }

        static void Textures()
        {
            if (dot != null) return;
            dot = Draw(64, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)), 1.6f));
            puff = Draw(128, (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y), a = Mathf.Atan2(y, x);
                float edge = .8f + .1f * Mathf.Sin(a * 3f + 2f) + .06f * Mathf.Sin(a * 7f);
                float body = Mathf.Clamp01(1f - r / edge);
                return Mathf.Pow(body, 1.5f) * (.7f + .3f * Mathf.PerlinNoise(x * 3f + 11f, y * 3f + 5f));
            });
        }

        static Texture2D Draw(int n, System.Func<float, float, float> mask)
        {
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Conveyor jolt fx" };
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
    }
}
