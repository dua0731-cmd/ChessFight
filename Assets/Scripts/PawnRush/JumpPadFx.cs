using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R355 (승규 11-04, a Unity shot of the hall's jump pad - the glowing platform with its gold arrow: "점프 발판 vfx
    // 적용 시켜"): the pad's effects, on a 02 jump pad whose deck is at local y 0.41 (Course01v4Builder.JumpPad).
    //   idle    the arrow's glow breathes, a gold ring round the deck, three chevrons rise off it along the launch,
    //           gold motes drift up the same way, a warm light over it
    //   launch  a shock ring across the deck, sparks thrown out and up, light streaks shot along the launch, the light
    //           and the arrow flash
    // The launch itself is the host's (ObstacleSurface.Apply); every PC sees a pawn land on the pad, so each one fires
    // the burst when it sees a pawn arrive on the deck - no packet. Pictures only: no collider, nothing it moves.
    public sealed class JumpPadFx : MonoBehaviour
    {
        const float Deck = .41f, Side = 3f, Rearm = .3f;
        static readonly Color Gold = new Color(1f, .74f, .32f), Ember = new Color(1f, .45f, .12f);
        static readonly Color ArrowGlow = Color.white * 3f;     // the pad material's own emission (PadMaterial)
        static Texture2D dot, ring, chevron;

        BoxCollider pad;
        Vector3 launch = Vector3.up;                            // local, normalised
        ParticleSystem motes, sparks, streaks, shock;
        Transform glow;
        readonly Transform[] chevrons = new Transform[3];
        Renderer glowRenderer, arrow;
        readonly Renderer[] chevronRenderers = new Renderer[3];
        int arrowSlot = -1;
        Light lamp;
        MaterialPropertyBlock block;
        float flash = -10f;
        readonly Dictionary<ImportedObstacleActor, float> lastSeen = new Dictionary<ImportedObstacleActor, float>();
        readonly Collider[] hits = new Collider[64];

        void Start()
        {
            block = new MaterialPropertyBlock();
            var surface = GetComponent<ObstacleSurface>();
            if (surface != null && surface.launchVelocity.sqrMagnitude > .01f) launch = surface.launchVelocity.normalized;
            pad = GetComponentInChildren<BoxCollider>();
            FindArrow();
            Textures();

            var flat = new Vector3(launch.x, 0f, launch.z);
            float yaw = flat.sqrMagnitude > 1e-4f ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : 0f;
            glow = Flat("Jump pad glow ring", ring, Side * 1.2f, 0f);
            glow.localPosition = Vector3.up * (Deck + .015f);
            glowRenderer = glow.GetComponent<Renderer>();
            for (int i = 0; i < chevrons.Length; i++)
            {
                chevrons[i] = Flat("Jump pad chevron " + (i + 1), chevron, 1.3f, yaw);
                chevronRenderers[i] = chevrons[i].GetComponent<Renderer>();
            }

            motes = Particles("Jump pad motes", dot, new Color(2.4f, 1.55f, .6f), true);
            var main = motes.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.9f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(.05f, .12f);
            var emission = motes.emission;
            emission.rateOverTime = 16f;
            var shape = motes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = Vector3.up * (Deck + .05f);
            shape.scale = new Vector3(Side * .85f, .05f, Side * .85f);
            var velocity = motes.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(launch.x * 1.8f, launch.x * 3.2f);
            velocity.y = new ParticleSystem.MinMaxCurve(launch.y * 1.8f, launch.y * 3.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(launch.z * 1.8f, launch.z * 3.2f);
            Fade(motes, Gold, Ember);
            Shrink(motes);
            motes.Play();

            sparks = Particles("Jump pad sparks", dot, new Color(3f, 1.9f, .7f), true);
            main = sparks.main;
            main.gravityModifier = 1.4f;
            Fade(sparks, Gold, Ember);
            Shrink(sparks);
            streaks = Particles("Jump pad streaks", dot, new Color(3.2f, 2.3f, 1.1f), true);
            streaks.GetComponent<ParticleSystemRenderer>().velocityScale = .07f;
            Fade(streaks, Color.white, Gold);
            shock = Particles("Jump pad shock ring", ring, new Color(3f, 2f, .8f), false);
            shock.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var size = shock.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, .22f, 1f, 1f));
            Fade(shock, Color.white, Gold);

            lamp = new GameObject("Jump pad light").AddComponent<Light>();
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = Vector3.up * (Deck + .7f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, .66f, .32f);
            lamp.range = 6f;
            lamp.shadows = LightShadows.None;
        }

        void Update()
        {
            if (glow == null) return;
            float t = Time.time;
            float since = t - flash;
            float burst = since < 2f ? Mathf.Exp(-since * 6f) : 0f;
            float breathe = .5f + .5f * Mathf.Sin(t * Mathf.PI * 1.6f);

            Tint(glowRenderer, new Color(2.2f, 1.6f, .8f, .35f + .3f * breathe + .6f * burst));   // HDR: over the bloom
            glow.localScale = Vector3.one * (Side * 1.2f * (1f + .08f * burst));
            var flat = new Vector3(launch.x, 0f, launch.z);
            for (int i = 0; i < chevrons.Length; i++)
            {
                float p = Mathf.Repeat(t * .75f + i / (float)chevrons.Length, 1f);
                chevrons[i].localPosition = Vector3.up * (Deck + .05f + p * 1.6f) + flat * (p * .45f);
                Tint(chevronRenderers[i], new Color(2.4f, 1.7f, .75f, Mathf.Sin(p * Mathf.PI) * (.7f + .3f * burst)));
            }
            lamp.intensity = 1.4f + .6f * breathe + 7f * burst;
            if (arrow != null)
            {
                arrow.GetPropertyBlock(block, arrowSlot);
                block.SetColor("_EmissionColor", ArrowGlow * (.75f + .4f * breathe + 2.5f * burst));
                arrow.SetPropertyBlock(block, arrowSlot);
                block.Clear();
            }
            Watch(t);
        }

        // A pawn arriving on the deck (the host launches it the same physics step): the burst, once per arrival.
        void Watch(float t)
        {
            if (pad == null) return;
            var box = pad.bounds;
            int n = Physics.OverlapBoxNonAlloc(box.center + Vector3.up * (box.extents.y + .25f),
                new Vector3(box.extents.x, .35f, box.extents.z), hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            bool fire = false;
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(hits[i]);
                if (actor == null || actor.Position.y < box.max.y - .2f || actor.Position.y > box.max.y + .4f) continue;
                if (!lastSeen.TryGetValue(actor, out float last) || t - last > Rearm) fire = true;
                lastSeen[actor] = t;
            }
            if (fire) Burst(t);
        }

        void Burst(float t)
        {
            flash = t;
            var deck = transform.TransformPoint(Vector3.up * (Deck + .05f));
            var up = transform.TransformDirection(launch);
            var e = new ParticleSystem.EmitParams();
            for (int i = 0; i < 46; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                e.position = deck + dir * Random.Range(.3f, 1.3f);
                e.velocity = dir * Random.Range(2f, 6f) + Vector3.up * Random.Range(2.5f, 6f);
                e.startLifetime = Random.Range(.45f, .9f);
                e.startSize = Random.Range(.05f, .11f);
                e.startColor = Color.white;
                sparks.Emit(e, 1);
            }
            for (int i = 0; i < 14; i++)
            {
                e.position = deck + new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-1.2f, 1.2f));
                e.velocity = up * Random.Range(9f, 15f) + Random.insideUnitSphere * .8f;
                e.startLifetime = Random.Range(.25f, .42f);
                e.startSize = Random.Range(.07f, .12f);
                streaks.Emit(e, 1);
            }
            e = new ParticleSystem.EmitParams
            {
                position = deck + Vector3.up * .02f, velocity = Vector3.zero, startLifetime = .45f, startSize = Side * 1.7f,
                startColor = Color.white
            };
            shock.Emit(e, 1);
        }

        // ------------------------------------------------------------------ parts

        void FindArrow()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] != null && slots[i].name.StartsWith("Pad JumpPad_Top")) { arrow = r; arrowSlot = i; return; }
            }
        }

        Transform Flat(string name, Texture2D texture, float size, float yaw)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = name;
            quad.transform.SetParent(transform, false);
            quad.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);   // lying on the deck, the texture's top to `yaw`
            quad.transform.localScale = Vector3.one * size;
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = LitMaterials.NewAdditiveParticles(texture, new Color(2.2f, 1.6f, .8f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return quad.transform;
        }

        ParticleSystem Particles(string name, Texture2D texture, Color tint, bool stretch)
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
            main.maxParticles = 300;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = LitMaterials.NewAdditiveParticles(texture, tint);
            r.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            r.velocityScale = .12f;
            r.lengthScale = 1f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static void Fade(ParticleSystem ps, Color from, Color to)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .12f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        static void Shrink(ParticleSystem ps)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, .2f));
        }

        void Tint(Renderer r, Color c)
        {
            r.GetPropertyBlock(block);
            LitMaterials.SetColor(block, c);
            r.SetPropertyBlock(block);
            block.Clear();
        }

        // ------------------------------------------------------------------ textures (white, alpha and colour alike)

        static void Textures()
        {
            if (dot != null) return;
            dot = Draw(64, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return Mathf.Pow(Mathf.Clamp01(1f - r), 2f); });
            ring = Draw(256, (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y);
                float band = Mathf.Exp(-Mathf.Pow((r - .86f) / .035f, 2f));
                float halo = Mathf.Exp(-Mathf.Pow((r - .86f) / .12f, 2f)) * .35f;
                return Mathf.Clamp01((band + halo) * (r < 1f ? 1f : 0f));
            });
            chevron = Draw(128, (x, y) =>
            {
                // A "^" with its tip at the top: two strokes from (-.7, -.35) and (.7, -.35) to (0, .45).
                float d = Mathf.Min(Segment(x, y, -.7f, -.35f, 0f, .45f), Segment(x, y, .7f, -.35f, 0f, .45f));
                return Mathf.Clamp01(1f - Mathf.InverseLerp(.06f, .14f, d)) + .35f * Mathf.Exp(-d * d / .03f);
            });
        }

        static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float h = Mathf.Clamp01(((x - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy));
            float dx = x - ax - vx * h, dy = y - ay - vy * h;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static Texture2D Draw(int n, System.Func<float, float, float> mask)
        {
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Jump pad fx" };
            var pixels = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float v = Mathf.Clamp01(mask((i + .5f) / n * 2f - 1f, (j + .5f) / n * 2f - 1f));
                    byte b = (byte)(v * 255f);
                    pixels[j * n + i] = new Color32(b, b, b, b);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
