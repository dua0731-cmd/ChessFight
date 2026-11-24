using System;
using System.Collections.Generic;
using UnityEngine;
using Kit = ChessFight.RagdollLab.SkillInkKit;
using Random = UnityEngine.Random;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// The comic layer (2026-11-24; the user: "이전에 구현했던 스킬들도 이펙트를 제대로 만들어줘. 폰러쉬라는 게임에 맞게 웃기고
    /// 코믹한 느낌이되 너무 허접하진 않고 퀄리티 좋게"): design A's inked toy look (SkillInkKit: hard colour bands, a dark ink
    /// rim, no glow) used for slapstick - the props a skill throws are real chunky objects (a boulder, a holy-water flask,
    /// swords, iron weights on a rope, an eight-pointed star), a hit is a white flash that pops for a few frames with the C
    /// half ring and round drops flung the way of the push, things that land or break go up in round cartoon poofs that are
    /// eaten away from outside, and fliers leave inked streaks. Timing over detail: in fast, a hold, out with an ease.
    /// Kept to design A's other promises: no star shapes as sparkle, no words or numbers, no chips, no pillars of light.
    /// </summary>
    public partial class PawnRushSkillFx
    {
        static readonly Kit.Palette CmWhite = new Kit.Palette("#FFFFFF", "#FFFFFF", "#D3DCEB", "#1B2340");
        static readonly Kit.Palette CmIron = new Kit.Palette("#7A746D", "#C2BBB2", "#4A4540", "#16130F");
        static readonly Kit.Palette CmRope = new Kit.Palette("#D9A15A", "#F7DCAA", "#A8743A", "#3A2410");
        static readonly Kit.Palette CmSteel = new Kit.Palette("#DDE5EF", "#FFFFFF", "#8F9EB6", "#1C2436");
        static readonly Kit.Palette CmGold = new Kit.Palette("#FFC93C", "#FFF2B8", "#E08E12", "#3A2208");
        static readonly Kit.Palette CmWater = new Kit.Palette("#36B3F0", "#D9F4FF", "#1374C2", "#062C4D");
        static readonly Kit.Palette CmRock = new Kit.Palette("#A89D8D", "#E3DACB", "#6B6255", "#231E18");
        static readonly Kit.Palette CmDust = new Kit.Palette("#EADFCB", "#FFFFFF", "#BCAC92", "#4A3B28");
        static readonly Kit.Palette CmRed = new Kit.Palette("#F0473E", "#FFD5CF", "#B3241E", "#3A0A08");
        static readonly Kit.Palette CmCrackInk = new Kit.Palette("#4A3B30", "#6E5A4A", "#2A2019", "#0E0A07");

        Mesh cmRock, cmBlade, cmHilt, cmFlask, cmCross, cmStar, cmPuddle;
        Material cmFillMat;

        // ---------------------------------------------------------------- moments

        /// <summary>A cartoon poof: <paramref name="n"/> round puffs popping out of a point (overshooting a little), drifting
        /// out and up, and eaten away from outside (a landing, a break, a burst). Smooth (hardly lumpy) so they read as cloud,
        /// not stone.</summary>
        void CmPoof(Vector3 at, Kit.Palette p, int n, float radius, float size, float life = 0.45f, float up = 0.35f, Vector3 bias = default)
        {
            if (kit == null) return;
            for (int i = 0; i < n; i++)
            {
                float a = (i + Random.value * 0.7f) / n * Mathf.PI * 2f;
                Vector3 dir = (new Vector3(Mathf.Cos(a), up * (0.4f + Random.value), Mathf.Sin(a)) + bias).normalized;
                float s = size * Random.Range(0.75f, 1.25f), r = radius * Random.Range(0.65f, 1.1f), l = life * Random.Range(0.8f, 1.2f);
                var puff = new Kit.Puff(kit, p) { lump = 0.1f, ink = 0.3f };
                Vector3 from = at;
                var f = kit.Run(l, (fx, d) =>
                {
                    float t = fx.age / l;
                    float pop = Kit.EaseOutBack(Mathf.Clamp01(t * 3.2f));
                    puff.t.position = from + dir * (r * Kit.EaseOut(t)) + Vector3.up * (0.15f * t);
                    puff.t.localScale = Vector3.one * (s * pop * (1f - 0.3f * t));
                    puff.dissolve = Mathf.Clamp01((t - 0.4f) / 0.6f);
                    puff.Apply(fx.age);
                    return true;
                });
                f.puffs.Add(puff);
            }
        }

        /// <summary>A white ball that pops at a point for a few frames and is eaten away: the "펑" of a hit.</summary>
        void CmFlash(Vector3 at, float size, int frames = 8, Kit.Palette? colors = null)
        {
            if (kit == null) return;
            var ball = new Kit.Puff(kit, colors ?? CmWhite) { lump = 0.04f, ink = 0.22f };
            float life = frames * Frame;
            var f = kit.Run(life, (fx, d) =>
            {
                float t = fx.age / life;
                ball.t.position = at;
                ball.t.localScale = Vector3.one * (size * (t < 0.25f ? Kit.EaseOut(t / 0.25f) * 1.15f : 1.15f - 0.25f * (t - 0.25f)));
                ball.dissolve = Mathf.Clamp01((t - 0.35f) / 0.65f);
                ball.Apply(fx.age);
                return true;
            });
            f.puffs.Add(ball);
        }

        /// <summary>A comic hit: the white pop, the C half ring flung the way of the push, round drops, the hit one's body
        /// flashing white then <paramref name="p"/>'s colour, a hit stop and a shake (heavier with <paramref name="size"/>).</summary>
        void CmSmack(RagdollPawn target, Vector3 at, Vector3 push, Kit.Palette p, float size = 1f)
        {
            if (kit == null) return;
            Vector3 way = push.sqrMagnitude > 1e-4f ? push.normalized : Vector3.up;
            CmFlash(at, 0.5f * size);
            kit.HalfRing(at, way, p, 0.5f * size, 0.16f * Mathf.Sqrt(size));
            kit.Drops(at, way + Vector3.up * 0.4f, p, Mathf.Clamp(Mathf.RoundToInt(3f + 2f * size), 3, 6), 6f + 2f * size);
            if (target != null) kit.FlashBody(target, p.main);
            HitStop(Mathf.Lerp(3f, 6f, Mathf.InverseLerp(0.6f, 1.6f, size)) * Frame);
            Shake(0.05f * size, 10f * Frame);
        }

        /// <summary>Inked streaks behind a body for <paramref name="seconds"/> (it flies, it dashes, it dives).</summary>
        void CmStreak(RagdollPawn who, Kit.Palette p, float seconds, float width = 0.16f, float tail = 0.16f)
        {
            if (kit == null || who == null) return;
            float until = Clock + seconds;
            InkTrail(() => who != null && Clock < until ? who.Hips.position : (Vector3?)null, p, tail, width, p.light);
        }

        /// <summary>Dust puffed up at a piece's feet every so often while <paramref name="seconds"/> last (a skid, a pull).</summary>
        void CmSkid(RagdollPawn who, float seconds, float every = 0.07f, Kit.Palette? colors = null)
        {
            if (kit == null || who == null) return;
            float next = 0f;
            var p = colors ?? CmDust;
            kit.Run(seconds, (fx, d) =>
            {
                if (who == null) return false;
                if (fx.age < next) return true;
                next = fx.age + every;
                CmPoof(who.FeetPoint + Vector3.up * 0.08f, p, 2, 0.35f, 0.22f, 0.35f, 0.2f);
                return true;
            });
        }

        /// <summary>A chunky inked prop of the kit's (fill and rim), hidden until it is placed.</summary>
        Kit.Prop CmProp(Mesh mesh, Kit.Palette p, string name, float ink = 0.02f)
        {
            var prop = new Kit.Prop(kit, mesh, p, name) { inkWidth = ink };
            prop.Show(false);
            return prop;
        }

        // ---------------------------------------------------------------- shapes (made once, in code)

        /// <summary>A cartoon boulder, 1 m across: a round lump pushed in and out by two soft noises.</summary>
        Mesh CmRockMesh()
        {
            if (cmRock != null) return cmRock;
            var src = kit.meshPuff;
            var v = src.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 n = v[i].normalized;
                float k = 1f + 0.2f * (Mathf.PerlinNoise(n.x * 1.6f + 3.1f, n.y * 1.6f + n.z * 0.7f + 7.3f) - 0.5f)
                             + 0.1f * (Mathf.PerlinNoise(n.z * 3.4f + 1.7f, n.x * 3.4f + n.y * 0.9f + 9.1f) - 0.5f);
                v[i] = n * (0.5f * k);
            }
            cmRock = new Mesh { name = "Comic boulder", hideFlags = HideFlags.HideAndDontSave, vertices = v, triangles = src.triangles };
            cmRock.RecalculateNormals();
            cmRock.RecalculateBounds();
            return cmRock;
        }

        /// <summary>A sword's blade, its grip end at the origin and its point up (+Y) at 1 m: flat, square-ended, pointed.</summary>
        Mesh CmBladeMesh()
        {
            if (cmBlade != null) return cmBlade;
            const float w = 0.06f;
            cmBlade = CmExtrude(new[] { new Vector2(-w, 0f), new Vector2(w, 0f), new Vector2(w, 0.84f), new Vector2(0f, 1f), new Vector2(-w, 0.84f) }, 0.035f, "Comic blade");
            return cmBlade;
        }

        /// <summary>A sword's hilt below the blade: the cross guard, the grip and a round pommel.</summary>
        Mesh CmHiltMesh()
        {
            if (cmHilt != null) return cmHilt;
            var guard = kit.RoundBox(new Vector3(0.34f, 0.07f, 0.09f), 0.03f);
            var grip = kit.RoundBox(new Vector3(0.07f, 0.2f, 0.07f), 0.03f);
            var parts = new[]
            {
                new CombineInstance { mesh = guard, transform = Matrix4x4.Translate(new Vector3(0f, -0.02f, 0f)) },
                new CombineInstance { mesh = grip, transform = Matrix4x4.Translate(new Vector3(0f, -0.15f, 0f)) },
                new CombineInstance { mesh = kit.meshBall, transform = Matrix4x4.TRS(new Vector3(0f, -0.27f, 0f), Quaternion.identity, Vector3.one * 0.1f) },
            };
            cmHilt = new Mesh { name = "Comic hilt", hideFlags = HideFlags.HideAndDontSave };
            cmHilt.CombineMeshes(parts, true, true);
            cmHilt.RecalculateNormals();
            cmHilt.RecalculateBounds();
            Destroy(guard);
            Destroy(grip);
            return cmHilt;
        }

        /// <summary>A round-bottomed flask with a long neck, its foot at the origin (about 0.5 m tall).</summary>
        Mesh CmFlaskMesh()
        {
            if (cmFlask != null) return cmFlask;
            cmFlask = CmLathe(new[]
            {
                new Vector2(0f, 0f), new Vector2(0.1f, 0.012f), new Vector2(0.16f, 0.07f), new Vector2(0.18f, 0.15f), new Vector2(0.16f, 0.23f),
                new Vector2(0.11f, 0.29f), new Vector2(0.06f, 0.32f), new Vector2(0.055f, 0.42f), new Vector2(0.075f, 0.44f), new Vector2(0.07f, 0.47f),
                new Vector2(0f, 0.47f),
            }, 20, "Comic flask");
            return cmFlask;
        }

        /// <summary>The flask's cork and a small cross on its belly, in one mesh (they share a colour).</summary>
        Mesh CmCrossMesh()
        {
            if (cmCross != null) return cmCross;
            var cork = kit.RoundBox(new Vector3(0.1f, 0.08f, 0.1f), 0.025f);
            var bar = kit.RoundBox(new Vector3(0.035f, 0.13f, 0.03f), 0.012f);
            var arm = kit.RoundBox(new Vector3(0.1f, 0.035f, 0.03f), 0.012f);
            var parts = new[]
            {
                new CombineInstance { mesh = cork, transform = Matrix4x4.Translate(new Vector3(0f, 0.5f, 0f)) },
                new CombineInstance { mesh = bar, transform = Matrix4x4.Translate(new Vector3(0f, 0.15f, 0.175f)) },
                new CombineInstance { mesh = arm, transform = Matrix4x4.Translate(new Vector3(0f, 0.17f, 0.175f)) },
            };
            cmCross = new Mesh { name = "Comic cork and cross", hideFlags = HideFlags.HideAndDontSave };
            cmCross.CombineMeshes(parts, true, true);
            cmCross.RecalculateNormals();
            cmCross.RecalculateBounds();
            Destroy(cork);
            Destroy(bar);
            Destroy(arm);
            return cmCross;
        }

        /// <summary>The queen's eight-pointed star, 1 m from the middle to a point, lying flat (Y up), thin.</summary>
        Mesh CmStarMesh()
        {
            if (cmStar != null) return cmStar;
            var pts = new Vector2[16];
            for (int i = 0; i < 16; i++)
            {
                float a = (90f + i * 22.5f) * Mathf.Deg2Rad, r = i % 2 == 0 ? 1f : 0.55f;
                pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            var flat = CmExtrude(pts, 0.02f, "Comic star");
            // Lying down: the outline's plane turned onto the floor.
            var v = flat.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = new Vector3(v[i].x, v[i].z, v[i].y);
            flat.vertices = v;
            var tri = flat.triangles;
            for (int i = 0; i < tri.Length; i += 3) (tri[i + 1], tri[i + 2]) = (tri[i + 2], tri[i + 1]);   // (the swap mirrored it)
            flat.triangles = tri;
            var white = new Color[v.Length];
            for (int i = 0; i < white.Length; i++) white[i] = Color.white;
            flat.colors = white;   // (the see-through fill's shader tints by vertex colour)
            flat.RecalculateNormals();
            flat.RecalculateBounds();
            return cmStar = flat;
        }

        /// <summary>A puddle 1 m across: a wobbly round slab, very thin, lying flat.</summary>
        Mesh CmPuddleMesh()
        {
            if (cmPuddle != null) return cmPuddle;
            var pts = new Vector2[28];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = i * Mathf.PI * 2f / pts.Length;
                float r = 0.5f * (1f + 0.09f * Mathf.Sin(a * 3f + 0.6f) + 0.05f * Mathf.Sin(a * 5f + 2.1f));
                pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            var flat = CmExtrude(pts, 0.02f, "Comic puddle");
            var v = flat.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = new Vector3(v[i].x, v[i].z, v[i].y);
            flat.vertices = v;
            var tri = flat.triangles;
            for (int i = 0; i < tri.Length; i += 3) (tri[i + 1], tri[i + 2]) = (tri[i + 2], tri[i + 1]);
            flat.triangles = tri;
            flat.RecalculateNormals();
            flat.RecalculateBounds();
            return cmPuddle = flat;
        }

        /// <summary>A see-through flat fill (a star's inside), drawn after the floor.</summary>
        Material CmFillMaterial()
        {
            if (cmFillMat != null) return cmFillMat;
            var shader = Shader.Find("Sprites/Default");
            cmFillMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 3001 };
            return cmFillMat;
        }

        /// <summary>A shape round the Y axis from (radius, height) pairs, foot first; outward faces.</summary>
        static Mesh CmLathe(Vector2[] profile, int around, string name)
        {
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int i = 0; i < profile.Length; i++)
                for (int k = 0; k <= around; k++)
                {
                    float a = k * Mathf.PI * 2f / around;
                    v.Add(new Vector3(Mathf.Cos(a) * profile[i].x, profile[i].y, Mathf.Sin(a) * profile[i].x));
                }
            for (int i = 0; i < profile.Length - 1; i++)
                for (int k = 0; k < around; k++)
                {
                    int a = i * (around + 1) + k, b = a + around + 1;
                    tri.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            var m = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>A flat outline (counter-clockwise in X, Y; seen whole from its middle) pushed out along Z: two caps and
        /// hard-edged sides.</summary>
        static Mesh CmExtrude(Vector2[] outline, float depth, string name)
        {
            var v = new List<Vector3>();
            var tri = new List<int>();
            int n = outline.Length;
            Vector2 c = Vector2.zero;
            foreach (var p in outline) c += p;
            c /= n;
            float h = depth * 0.5f;
            int f0 = v.Count;
            v.Add(new Vector3(c.x, c.y, -h));
            foreach (var p in outline) v.Add(new Vector3(p.x, p.y, -h));
            for (int i = 0; i < n; i++) tri.AddRange(new[] { f0, f0 + 1 + (i + 1) % n, f0 + 1 + i });
            int b0 = v.Count;
            v.Add(new Vector3(c.x, c.y, h));
            foreach (var p in outline) v.Add(new Vector3(p.x, p.y, h));
            for (int i = 0; i < n; i++) tri.AddRange(new[] { b0, b0 + 1 + i, b0 + 1 + (i + 1) % n });
            for (int i = 0; i < n; i++)
            {
                Vector2 p = outline[i], q = outline[(i + 1) % n];
                int s = v.Count;
                v.Add(new Vector3(p.x, p.y, -h));
                v.Add(new Vector3(q.x, q.y, -h));
                v.Add(new Vector3(q.x, q.y, h));
                v.Add(new Vector3(p.x, p.y, h));
                tri.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }
            var m = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        void DestroyComic()
        {
            foreach (var m in new UnityEngine.Object[] { cmRock, cmBlade, cmHilt, cmFlask, cmCross, cmStar, cmPuddle, cmFillMat })
                if (m != null) Destroy(m);
            cmRock = cmBlade = cmHilt = cmFlask = cmCross = cmStar = cmPuddle = null;
            cmFillMat = null;
        }
    }
}
