using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R506 (user 11-07: "sfx랑 vfx도 해줘", then "이런 vfx 말고" about the shared solid light column and cube bits):
    // A's own effects, soft particles in the paint-and-stone theme instead of ArenaFx's primitives. Pictures only,
    // played by every PC from the board's state (PaintBoard's moments), so nothing is sent for them.
    //   motes     soft glowing specks drifting up from a charging plate's edge (team colour, red while blocked)
    //   splat     a paint splash spreading flat on the square and fading (the square's new stone colour)
    //   drops     paint droplets thrown out, streaking and falling back
    //   ring      a soft light ring gliding out over the floor
    //   stars     four-point twinkles (team colour on a square, gold on a rank)
    //   confetti  a team's finish: team-colour, gold and cream flakes tumbling down from the board's corners
    // Built in code with its own small textures, as LibraryConveyorJoltFx; PaintBoard adds it.
    public sealed class PaintBoardFx : MonoBehaviour
    {
        static Texture2D dot, star, splatTex, ringTex, flake, frameTex, fillTex;
        ParticleSystem motes, splat, drops, ring, stars, confetti, frame, fill;
        ParticleSystem.EmitParams e;                                   // a struct: reused, so not readonly

        void Awake()
        {
            Textures();
            motes = Particles("A paint motes", dot, true, ParticleSystemRenderMode.Billboard, 600, 2.4f);
            Fade(motes, 1f, 0f);
            Shrink(motes, 1f, .3f);
            var m = motes.main;
            m.gravityModifier = -.05f;
            var curl = motes.noise;
            curl.enabled = true;
            curl.strength = .35f;
            curl.frequency = 1.4f;

            splat = Particles("A paint splat", splatTex, false, ParticleSystemRenderMode.HorizontalBillboard, 120);
            Fade(splat, 1f, 0f, .05f, .55f);
            Shrink(splat, .35f, 1f, true);

            drops = Particles("A paint drops", dot, false, ParticleSystemRenderMode.Stretch, 600);
            Fade(drops, 1f, .8f);
            var d = drops.main;
            d.gravityModifier = 1.4f;

            ring = Particles("A light ring", ringTex, true, ParticleSystemRenderMode.HorizontalBillboard, 40, 1.7f);
            Fade(ring, .9f, 0f, .05f);
            Shrink(ring, .15f, 1f, true);

            // The board's own shape for a painted square and a finished team (user, 11-08: "원형으로 효과가 나오는데 채스판에
            // 맞춰서"): a square light frame that starts on the square's edge and glides out, and a square flash of the
            // new stone over the square, both turned with the board.
            frame = Particles("A square frame", frameTex, true, ParticleSystemRenderMode.HorizontalBillboard, 40, 1.8f);
            Fade(frame, 1f, 0f, .04f, .12f);
            Shrink(frame, FrameFrom, 1f, true);
            fill = Particles("A square flash", fillTex, true, ParticleSystemRenderMode.HorizontalBillboard, 40, 1.2f);
            Fade(fill, .8f, 0f, .03f, .1f);

            stars = Particles("A twinkles", star, true, ParticleSystemRenderMode.Billboard, 400, 3f);
            Fade(stars, 1f, 0f, .15f);
            var twinkle = stars.sizeOverLifetime;
            twinkle.enabled = true;
            twinkle.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(.2f, 1f), new Keyframe(.45f, .55f),
                new Keyframe(.7f, .9f), new Keyframe(1f, 0f)));
            var spin = stars.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

            confetti = Particles("A confetti", flake, false, ParticleSystemRenderMode.Billboard, 800);
            Fade(confetti, 1f, 0f, .02f, .8f);
            var c = confetti.main;
            c.gravityModifier = .35f;
            var tumble = confetti.rotationOverLifetime;
            tumble.enabled = true;
            tumble.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var flutter = confetti.noise;
            flutter.enabled = true;
            flutter.strength = .8f;
            flutter.frequency = .8f;
            var drag = confetti.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 2.2f;
            drag.dampen = .08f;
        }

        // ------------------------------------------------------------------ moments

        // One rising speck from a charging plate's edge (PaintBoard calls it a few times a second per square).
        public void Mote(Vector3 at, Color team)
        {
            Emit(motes, at, new Vector3(Random.Range(-.08f, .08f), Random.Range(.5f, .9f), Random.Range(-.08f, .08f)),
                 Random.Range(.6f, 1f), Random.Range(.09f, .16f), Glow(team, 2.2f));
        }

        // Frame and flash textures put their edge at 0.82 of the half size; the frame grows from FrameFrom to 1.
        const float FrameEdge = .82f, FrameFrom = 1f / 1.35f;

        // A square light frame on a `side` m square (turned `yaw` degrees with the board), gliding out to 1.35 times it.
        void Frame(Vector3 at, float side, float yaw, float life, Color color) =>
            Emit(frame, at, Vector3.zero, life, side / FrameEdge / FrameFrom, color, yaw);

        // A square painted: a flash of the new stone over the square, a square light frame running out from its edge,
        // droplets fly and it twinkles over the square.
        public void Painted(Vector3 at, Color team, Color stone, float squareSize, float yaw)
        {
            Emit(fill, at + Vector3.up * .015f, Vector3.zero, .45f, squareSize / FrameEdge, Glow(team, 1.4f), yaw);
            Frame(at + Vector3.up * .02f, squareSize, yaw, .7f, Glow(team, 1.6f));
            for (int i = 0; i < 16; i++)
            {
                var dir = Random.insideUnitCircle.normalized;
                var c = i % 3 == 0 ? Glow(team, 1.2f) : stone;
                Emit(drops, at + new Vector3(dir.x, 0f, dir.y) * .25f + Vector3.up * .05f,
                     new Vector3(dir.x, 0f, dir.y) * Random.Range(1.2f, 2.8f) + Vector3.up * Random.Range(1.6f, 3f), Random.Range(.5f, .8f),
                     Random.Range(.06f, .12f), c);
            }
            var turn = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < 7; i++)
            {
                var p = turn * new Vector3(Random.Range(-.45f, .45f), 0f, Random.Range(-.45f, .45f)) * squareSize;
                p = new Vector3(p.x, p.z, 0f);
                Emit(stars, at + new Vector3(p.x, Random.Range(.1f, .7f), p.y), Vector3.up * Random.Range(.2f, .6f), Random.Range(.5f, .8f),
                     Random.Range(.25f, .45f), Glow(i % 2 == 0 ? team : Color.white, 2.5f), Random.Range(0f, 360f));
            }
        }

        // A rank done, at a point running along it (PaintBoard sweeps it from file a to h): gold dust and twinkles.
        public void RankDust(Vector3 at, Color gold)
        {
            for (int i = 0; i < 2; i++)
                Emit(motes, at + Random.insideUnitSphere * .25f, new Vector3(0f, Random.Range(.4f, 1f), 0f), Random.Range(.6f, 1f),
                     Random.Range(.1f, .18f), Glow(gold, 2.5f));
            if (Random.value < .5f)
                Emit(stars, at + Vector3.up * Random.Range(.1f, .5f), Vector3.up * .3f, .6f, Random.Range(.3f, .5f), Glow(gold, 3f), Random.Range(0f, 360f));
        }

        // Each end of a done rank: a small gold fountain of twinkles and a ring.
        public void RankEnd(Vector3 at, Color gold)
        {
            Emit(ring, at + Vector3.up * .02f, Vector3.zero, .8f, 3f, Glow(gold, 1.8f));
            for (int i = 0; i < 10; i++)
            {
                var dir = Random.insideUnitCircle * .6f;
                Emit(stars, at + Vector3.up * .1f, new Vector3(dir.x, Random.Range(2f, 3.5f), dir.y), Random.Range(.6f, 1f),
                     Random.Range(.3f, .5f), Glow(gold, 3f), Random.Range(0f, 360f));
            }
        }

        // A team's 32 squares done: confetti thrown up from the board's corners, a wide ring and glitter over the board.
        public void TeamDone(Vector3 centre, Vector3[] corners, float radius, Color team, Color gold, Color cream, float yaw)
        {
            Frame(centre + Vector3.up * .03f, radius * 2f, yaw, 1.4f, Glow(team, 1.5f));          // the whole board's outline
            var colours = new[] { team, gold, cream };
            foreach (var corner in corners)
            {
                var inward = (centre - corner);
                inward.y = 0f;
                inward.Normalize();
                for (int i = 0; i < 45; i++)
                {
                    var side = Random.insideUnitCircle * .9f;
                    Emit(confetti, corner + Vector3.up * .3f,
                         (inward * Random.Range(.8f, 2.6f) + new Vector3(side.x, 0f, side.y)) + Vector3.up * Random.Range(5f, 8.5f),
                         Random.Range(2.4f, 3.6f), Random.Range(.16f, .26f), colours[i % 3], Random.Range(0f, 360f));
                }
            }
            for (int i = 0; i < 40; i++)
            {
                var q = Quaternion.Euler(0f, yaw, 0f) * new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * radius;   // over the board, not a disc
                var p = new Vector2(q.x, q.z);
                Emit(stars, centre + new Vector3(p.x, Random.Range(.3f, 2.5f), p.y), Vector3.up * Random.Range(.1f, .5f), Random.Range(.7f, 1.3f),
                     Random.Range(.3f, .6f), Glow(i % 3 == 0 ? gold : team, 2.8f), Random.Range(0f, 360f));
            }
        }

        // A team gate's 25 / 50 / 75 % (user, 11-07, via the C session: each mini-game its own quarter moment, no ring):
        // paint droplets fountain up from both piers of the arch in the team's colour and its stone's, and twinkles
        // climb the arch; more at each quarter. `right` is the gate's across direction, `gate` its floor middle.
        public void GateQuarter(Vector3 gate, Vector3 right, Color team, Color stone, int quarter)
        {
            int count = 10 + 8 * quarter, twinkles = 4 + 4 * quarter;
            foreach (float side in new[] { -1f, 1f })
            {
                var pier = gate + right * side * 2.4f;
                for (int i = 0; i < count; i++)
                {
                    var spread = Random.insideUnitCircle * .7f;
                    Emit(drops, pier + Vector3.up * .2f, new Vector3(spread.x, Random.Range(4f, 6.5f), spread.y) - right * side * Random.Range(.2f, .9f),
                         Random.Range(.8f, 1.2f), Random.Range(.07f, .13f), i % 2 == 0 ? Glow(team, 1.2f) : stone);
                }
            }
            for (int i = 0; i < twinkles; i++)
            {
                float a = Random.Range(0f, Mathf.PI);
                var p = gate + right * Mathf.Cos(a) * 2.6f + Vector3.up * (3.6f + Mathf.Sin(a) * 2.6f);
                Emit(stars, p, Vector3.up * Random.Range(.2f, .6f), Random.Range(.6f, 1.1f), Random.Range(.35f, .6f),
                     Glow(i % 3 == 0 ? Color.white : team, 2.8f), Random.Range(0f, 360f));
            }
        }

        // ------------------------------------------------------------------ parts

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

        // A particle's own colour is 8-bit (EmitParams.startColor), so the glow's HDR boost for bloom is the additive
        // material's tint (Particles' `brightness`); `k` only weighs one colour against another in the same system.
        static Color Glow(Color c, float k) => new Color(Mathf.Clamp01(c.r * k * .4f), Mathf.Clamp01(c.g * k * .4f), Mathf.Clamp01(c.b * k * .4f), 1f);

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
                m.color = new Color(brightness, brightness, brightness, 1f);     // HDR tint: the bloom of the additive parts
                r.sharedMaterial = m;
            }
            else Debug.LogWarning("[미니게임 A] 파티클 셰이더가 없습니다: " + LitMaterials.ParticlesUnlitName);
            r.renderMode = mode;
            r.velocityScale = .08f;
            r.lengthScale = 1.2f;
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

        // Size over the particle's life from `from` to `to` (fast-out when `quick`).
        static void Shrink(ParticleSystem ps, float from, float to, bool quick = false)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = quick
                ? new AnimationCurve(new Keyframe(0f, from, 0f, 6f), new Keyframe(.35f, Mathf.Lerp(from, to, .85f)), new Keyframe(1f, to))
                : AnimationCurve.Linear(0f, from, 1f, to);
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        // ------------------------------------------------------------------ textures (white, the mask in alpha)

        static void Textures()
        {
            if (dot != null) return;
            dot = Draw(64, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)), 1.8f));
            // four-point twinkle: two thin crossed spikes and a soft core
            star = Draw(96, (x, y) =>
            {
                float ax = Mathf.Abs(x), ay = Mathf.Abs(y), r = Mathf.Sqrt(x * x + y * y);
                float spikes = Mathf.Max(Mathf.Clamp01(1f - ay * 14f) * Mathf.Clamp01(1f - ax), Mathf.Clamp01(1f - ax * 14f) * Mathf.Clamp01(1f - ay));
                float core = Mathf.Pow(Mathf.Clamp01(1f - r * 2.6f), 1.5f);
                return Mathf.Max(spikes * spikes, core);
            });
            // a paint splash: a lobed blob with a few satellite drops, soft edge
            splatTex = Draw(128, (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y), a = Mathf.Atan2(y, x);
                float edge = .58f + .1f * Mathf.Sin(a * 5f + 1f) + .06f * Mathf.Sin(a * 9f + 3f) + .04f * Mathf.Sin(a * 13f);
                float blob = Mathf.Clamp01((edge - r) * 18f);
                float sat = 0f;
                for (int i = 0; i < 7; i++)
                {
                    float ang = i * 0.897f + .4f, dist = .72f + .12f * Mathf.Sin(i * 2.3f), rad = .045f + .02f * Mathf.Cos(i * 1.7f);
                    float dx = x - Mathf.Cos(ang) * dist, dy = y - Mathf.Sin(ang) * dist;
                    sat = Mathf.Max(sat, Mathf.Clamp01((rad - Mathf.Sqrt(dx * dx + dy * dy)) * 40f));
                }
                return Mathf.Max(blob, sat);
            });
            // a soft ring of light
            ringTex = Draw(128, (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y);
                return Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(r - .82f) / .14f), 2f);
            });
            // a square light frame: a soft line along the square whose half size is FrameEdge
            frameTex = Draw(256, (x, y) =>
            {
                float d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                return Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(d - FrameEdge) / .07f), 2f);
            });
            // a square flash: full inside the square, a soft edge, brighter toward the edge
            fillTex = Draw(128, (x, y) =>
            {
                float d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                return Mathf.Clamp01((FrameEdge - d) / .03f) * (.35f + .65f * d / FrameEdge);
            });
            // a confetti flake: a rounded rectangle twice as long as wide
            flake = Draw(48, (x, y) =>
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x) - .45f), dy = Mathf.Max(0f, Mathf.Abs(y) - .1f);
                return Mathf.Clamp01((.25f - Mathf.Sqrt(dx * dx + dy * dy)) * 20f);
            });
        }

        static Texture2D Draw(int n, System.Func<float, float, float> mask)
        {
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "A paint fx" };
            var pixels = new Color32[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    byte b = (byte)(Mathf.Clamp01(mask((i + .5f) / n * 2f - 1f, (j + .5f) / n * 2f - 1f)) * 255f);
                    pixels[j * n + i] = new Color32(255, 255, 255, b);
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }
    }
}
