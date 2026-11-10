using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// R534 (사용자 11-10, 소드 파이트 멀티: "백팀 플레이어가 흑팀이 스킬 쓸 때, 흑팀 플레이어가 백팀 스킬을 바라 볼 때 빨간색으로
    /// 해서 구분"): the effects of the other side's skills, drawn red on each screen; my own side's keep their colours.
    /// The effects are made in many places, each painting its own colours (a palette, a hot spark, a prefab's gold), so
    /// this does not change them where they are made: it is handed each effect's objects with the piece whose skill they
    /// are (<see cref="SkillInkKit.Owner"/>), and every frame, after the effects have set their colours, turns those of a
    /// foe's objects red: every colour of their see-through materials (through the renderer's property block), their
    /// particles' colours, their trails' and lines' colours and their lights. The red keeps each colour's brightness and
    /// alpha, so a glow stays a glow and a dark ink a dark ink. Solid lit things (a stone tower, a thrown sword, chips of
    /// board) keep their own look: they are things, not light.
    /// </summary>
    public sealed class SkillTeamTint
    {
        /// <summary>The hue of the red (the kit's TeamEnemy, #D9473F, a little off pure red) and the least saturation a
        /// colour gets (white turns a light red, not pink-white).</summary>
        const float Hue = 0.008f, MinSaturation = 0.62f;

        /// <summary>A colour in red, keeping its brightness (HDR too) and its alpha. Red stays red (applied twice it is the
        /// same), so it can be applied every frame to a colour nobody set again.</summary>
        public static Color Red(Color c)
        {
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (m <= 1e-5f) return c;
            Color.RGBToHSV(new Color(c.r / m, c.g / m, c.b / m), out _, out float s, out _);
            var o = Color.HSVToRGB(Hue, Mathf.Max(s, MinSaturation), 1f) * m;
            o.a = c.a;
            return o;
        }

        sealed class Claim
        {
            public RagdollPawn owner;
            public Transform root;
        }

        readonly List<Claim> claims = new List<Claim>();
        readonly HashSet<Transform> claimed = new HashSet<Transform>();
        // What this wrote into a renderer's block last frame, per (renderer, property): if the block still holds it, the
        // effect did not set that colour again, and the material's own colour (which it may have changed) is read instead.
        readonly Dictionary<(int, int), Color> written = new Dictionary<(int, int), Color>();
        readonly Dictionary<Shader, int[]> colorProps = new Dictionary<Shader, int[]>();
        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<ParticleSystem> systems = new List<ParticleSystem>();
        readonly List<Light> lights = new List<Light>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        int frameWritten;

        public int Count => claims.Count;

        public void Add(RagdollPawn owner, Transform root)
        {
            if (owner == null || root == null || !claimed.Add(root)) return;
            claims.Add(new Claim { owner = owner, root = root });
        }

        public void Clear()
        {
            claims.Clear();
            claimed.Clear();
            written.Clear();
        }

        /// <summary>Turns red what belongs to a piece <paramref name="foe"/> says is on the other side. Call it after the
        /// effects have stepped this frame, before the frame is drawn.</summary>
        public void Apply(Func<RagdollPawn, bool> foe)
        {
            // The written colours of renderers gone are dropped now and then (keys are cheap; a long match makes many).
            if (++frameWritten > 600)
            {
                frameWritten = 0;
                written.Clear();
            }
            for (int i = claims.Count - 1; i >= 0; i--)
            {
                var c = claims[i];
                if (c.root == null || c.owner == null)
                {
                    if (c.root != null) claimed.Remove(c.root);
                    else claimed.RemoveWhere(t => t == null);
                    claims.RemoveAt(i);
                    continue;
                }
                if (foe == null || !foe(c.owner)) continue;
                Redden(c.root);
            }
        }

        void Redden(Transform root)
        {
            root.GetComponentsInChildren(true, renderers);
            foreach (var r in renderers)
                if (r != null && (r is ParticleSystemRenderer || !Solid(r))) RedBlock(r);
            root.GetComponentsInChildren(true, systems);
            foreach (var ps in systems) RedParticles(ps);
            root.GetComponentsInChildren(true, lights);
            foreach (var l in lights)
            {
                var red = Red(l.color);
                if (red != l.color) l.color = red;
            }
        }

        /// <summary>A lit surface (URP Lit / Simple Lit / Complex Lit, the built-in Standard): a thing, not light.</summary>
        static bool Solid(Renderer r)
        {
            var m = r.sharedMaterial;
            if (m == null || m.shader == null) return true;
            string n = m.shader.name;
            return n == "Standard" || n.EndsWith("/Lit") && !n.Contains("Particles") || n.EndsWith("Simple Lit") && !n.Contains("Particles") || n.EndsWith("Complex Lit");
        }

        void RedBlock(Renderer r)
        {
            var mat = r.sharedMaterial;
            if (mat == null) return;
            var props = ColorProps(mat.shader);
            if (props.Length > 0)
            {
                r.GetPropertyBlock(block);
                int id = r.GetInstanceID();
                foreach (int p in props)
                {
                    Color from;
                    if (block.HasColor(p) || block.HasVector(p))
                    {
                        from = block.GetColor(p);
                        if (written.TryGetValue((id, p), out var last) && last == from && mat.HasColor(p)) from = mat.GetColor(p);
                    }
                    else if (mat.HasColor(p)) from = mat.GetColor(p);
                    else continue;
                    var red = Red(from);
                    block.SetColor(p, red);
                    written[(id, p)] = red;
                }
                r.SetPropertyBlock(block);
            }
            switch (r)
            {
                case TrailRenderer t:
                {
                    var g = t.colorGradient;
                    if (RedGradient(g)) t.colorGradient = g;
                    break;
                }
                case LineRenderer l:
                {
                    var g = l.colorGradient;
                    if (RedGradient(g)) l.colorGradient = g;
                    break;
                }
            }
        }

        int[] ColorProps(Shader shader)
        {
            if (shader == null) return Array.Empty<int>();
            if (colorProps.TryGetValue(shader, out var ids)) return ids;
            var list = new List<int>();
            for (int i = 0, n = shader.GetPropertyCount(); i < n; i++)
                if (shader.GetPropertyType(i) == ShaderPropertyType.Color) list.Add(shader.GetPropertyNameId(i));
            ids = list.ToArray();
            colorProps[shader] = ids;
            return ids;
        }

        void RedParticles(ParticleSystem ps)
        {
            if (ps == null) return;
            var main = ps.main;
            var start = main.startColor;
            if (RedMinMax(ref start)) main.startColor = start;
            var life = ps.colorOverLifetime;
            if (life.enabled)
            {
                var g = life.color;
                if (RedMinMax(ref g)) life.color = g;
            }
            var speed = ps.colorBySpeed;
            if (speed.enabled)
            {
                var g = speed.color;
                if (RedMinMax(ref g)) speed.color = g;
            }
            var trails = ps.trails;
            if (trails.enabled)
            {
                var a = trails.colorOverLifetime;
                if (RedMinMax(ref a)) trails.colorOverLifetime = a;
                var b = trails.colorOverTrail;
                if (RedMinMax(ref b)) trails.colorOverTrail = b;
            }
        }

        /// <summary>Red <paramref name="g"/> in place; true if it changed.</summary>
        static bool RedMinMax(ref ParticleSystem.MinMaxGradient g)
        {
            switch (g.mode)
            {
                case ParticleSystemGradientMode.Color:
                {
                    var red = Red(g.color);
                    if (red == g.color) return false;
                    g = new ParticleSystem.MinMaxGradient(red);
                    return true;
                }
                case ParticleSystemGradientMode.TwoColors:
                {
                    Color a = Red(g.colorMin), b = Red(g.colorMax);
                    if (a == g.colorMin && b == g.colorMax) return false;
                    g = new ParticleSystem.MinMaxGradient(a, b);
                    return true;
                }
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.RandomColor:
                {
                    var grad = g.gradient;
                    if (grad == null || !RedGradient(grad)) return false;
                    var mode = g.mode;
                    g = new ParticleSystem.MinMaxGradient(grad) { mode = mode };
                    return true;
                }
                case ParticleSystemGradientMode.TwoGradients:
                {
                    Gradient a = g.gradientMin, b = g.gradientMax;
                    bool ca = a != null && RedGradient(a), cb = b != null && RedGradient(b);
                    if (!ca && !cb) return false;
                    g = new ParticleSystem.MinMaxGradient(a, b);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Red a gradient's colour keys in place (its alpha keys stay); true if any changed.</summary>
        static bool RedGradient(Gradient g)
        {
            if (g == null) return false;
            var keys = g.colorKeys;
            bool changed = false;
            for (int i = 0; i < keys.Length; i++)
            {
                var red = Red(keys[i].color);
                if (red == keys[i].color) continue;
                keys[i].color = red;
                changed = true;
            }
            if (changed) g.SetKeys(keys, g.alphaKeys);
            return changed;
        }
    }
}
