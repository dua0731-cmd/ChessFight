using System;
using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R849 (the user 11-26: "마법 연출을 넣어줘 ... 지팡이랑 그 마술사 모델에도 붙여줘"): magic on the tablecloth trick's
    // magician, with the magic cue (BhMagicBuild: chimes and a harp rising for the 2 s from the trick's RollAt to its
    // PullAt). Pictures only - the trick, its timing and every collider as built; everything is a pure function of the
    // trick's phase on ObstacleClock (only the idle twinkle's choice of star is hashed from it too).
    //   - the wand's tip (found in the figurine's mesh: the point farthest from its upright axis between 58 % and 80 % of
    //     its height - the raised wand's ball): a core of light and a halo, faint at rest, swelling through the cue;
    //     sparks circle it in a tightening spiral, faster and more of them as the cue builds; a flash and a burst of
    //     sparks as the cloth is pulled, a smaller one as it is thrown back; a lilac-gold light on it.
    //   - the magician: the figure warms toward lilac through the cue (its renderers' colour, a property block), stars
    //     twinkle on its robe (points of its mesh facing out), and motes of light spiral up round it from its foot.
    // Everything hangs under the magician's model pivot, so it leans with him (BanquetTableShow.Magician).
    [DefaultExecutionOrder(206)]
    public sealed class BanquetWandShow : MonoBehaviour
    {
        const int Orbits = 14, PullSparks = 18, LaySparks = 10, RobeStars = 16, Motes = 20;
        const float BurstLife = 1.1f, FlashLife = .35f, AfterGlow = 1.4f;
        static readonly Color Gold = new Color(1f, .86f, .55f, 1f);
        static readonly Color Lilac = new Color(.8f, .62f, 1f, 1f);
        static readonly Color FigureLit = new Color(1.02f, .97f, 1.32f, 1f);

        TableclothTrick trick;
        BanquetClothRun run;
        Transform model, holder;
        Transform core, halo, flash;
        readonly List<Transform> orbit = new List<Transform>(), pullBurst = new List<Transform>(),
                                 layBurst = new List<Transform>(), robe = new List<Transform>(), motes = new List<Transform>();
        readonly List<Vector3> robeAt = new List<Vector3>();
        readonly List<Renderer> figure = new List<Renderer>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        Light glow;
        MaterialPropertyBlock block;
        Vector3 tip, axis;                                   // in the model's space: the wand's ball, the foot's middle
        float height;
        bool tinted;

        // As last drawn (the checks): the found tip (in the hall's frame), how bright the wand is (0 .. 1+), sparks shown.
        public bool Found { get; private set; }
        public Vector3 TipInFrame { get; private set; }
        public float Brightness { get; private set; }
        public int Sparks { get; private set; }

        public void Configure(GrandBanquetHallDressing dressing, TableclothTrick tableclothTrick)
        {
            trick = tableclothTrick;
            var frame = dressing.transform;
            run = new BanquetClothRun(GrandBanquetHallBuilder.ClothZ1 - GrandBanquetHallBuilder.TableZ0, trick);
            model = GrandBanquetHallDressing.ModelOn(frame, "bh_magician");
            block = new MaterialPropertyBlock();
            if (model == null || !FindTip()) { Debug.Log("[대연회장 지팡이 연출] 마술사 지팡이를 못 찾음"); return; }
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) figure.Add(r);
            Build();
            Found = true;
            TipInFrame = frame.InverseTransformPoint(model.TransformPoint(tip));
            Debug.Log("[대연회장 지팡이 연출] 지팡이 끝 " + TipInFrame.ToString("F2") + " · 키 " + height.ToString("F1") + "m · 망토 별 " + robeAt.Count);
        }

        // The tip and the robe's stars from the figurine's finest mesh, in the model pivot's space.
        bool FindTip()
        {
            MeshFilter best = null;
            foreach (var f in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null || !f.sharedMesh.isReadable) continue;
                if (best == null || f.sharedMesh.vertexCount > best.sharedMesh.vertexCount) best = f;
            }
            if (best == null) return false;
            var mesh = best.sharedMesh;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            mesh.GetVertices(v);
            mesh.GetNormals(n);
            var toModel = model.worldToLocalMatrix * best.transform.localToWorldMatrix;
            float y0 = float.MaxValue, y1 = float.MinValue;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < v.Count; i++)
            {
                v[i] = toModel.MultiplyPoint3x4(v[i]);
                if (i < n.Count) n[i] = toModel.MultiplyVector(n[i]).normalized;
                y0 = Mathf.Min(y0, v[i].y);
                y1 = Mathf.Max(y1, v[i].y);
                sum += v[i];
            }
            height = y1 - y0;
            if (height <= 0f) return false;
            // the upright axis: the middle of the base (its lowest tenth)
            Vector3 foot = Vector3.zero;
            int feet = 0;
            foreach (var p in v)
                if (p.y < y0 + .1f * height) { foot += p; feet++; }
            foot = feet > 0 ? foot / feet : sum / v.Count;
            axis = new Vector3(foot.x, y0, foot.z);
            // the wand's ball: the farthest point from the axis in the band, then the middle of what is near it
            float far = -1f;
            Vector3 end = Vector3.zero;
            foreach (var p in v)
            {
                if (p.y < y0 + .58f * height || p.y > y0 + .8f * height) continue;
                float d = Flat(p);
                if (d > far) { far = d; end = p; }
            }
            if (far <= 0f) return false;
            // the ball's middle: from its outer side, the mean of the points within its radius, again and again (the
            // shell's points pull the guess to the ball's centre)
            tip = end;
            float reach = .026f * height;
            for (int pass = 0; pass < 6; pass++)
            {
                Vector3 ball = Vector3.zero;
                int near = 0;
                foreach (var p in v)
                    if ((p - tip).sqrMagnitude < reach * reach) { ball += p; near++; }
                if (near == 0) break;
                tip = ball / near;
            }
            // the ball's front faces are the finer ones (the guess leans out): a little back toward him
            var outFlat = new Vector3(tip.x - axis.x, 0f, tip.z - axis.z);
            if (outFlat.sqrMagnitude > 1e-6f) tip -= outFlat.normalized * (.012f * height);
            // the robe's stars: points between the knee and the shoulder facing out, spread by a coarse grid
            var taken = new HashSet<long>();
            float cell = .09f * height;
            for (int i = 0; i < v.Count && i < n.Count; i++)
            {
                var p = v[i];
                float h = (p.y - y0) / height;
                if (h < .22f || h > .62f) continue;
                var outward = new Vector3(p.x - axis.x, 0f, p.z - axis.z);
                if (outward.sqrMagnitude < 1e-6f || Vector3.Dot(n[i], outward.normalized) < .6f) continue;
                if ((p - tip).sqrMagnitude < (.15f * height) * (.15f * height)) continue;
                long key = ((long)Mathf.FloorToInt(p.x / cell) * 73856093L) ^ ((long)Mathf.FloorToInt(p.y / cell) * 19349663L) ^
                           ((long)Mathf.FloorToInt(p.z / cell) * 83492791L);
                if (!taken.Add(key)) continue;
                robeAt.Add(p + n[i] * .006f * height);
            }
            // keep an even handful, picked by a fixed stride
            if (robeAt.Count > RobeStars)
            {
                var kept = new List<Vector3>();
                for (int i = 0; i < RobeStars; i++) kept.Add(robeAt[(int)((i + .5f) * robeAt.Count / RobeStars)]);
                robeAt.Clear();
                robeAt.AddRange(kept);
            }
            return true;
        }

        float Flat(Vector3 p) => new Vector2(p.x - axis.x, p.z - axis.z).magnitude;

        void Build()
        {
            var light = BanquetShowKit.GlowMaterial(Color.white, owned);
            if (light == null) return;
            holder = new GameObject("BH wand magic").transform;
            holder.SetParent(model, false);
            core = BanquetShowKit.Drawn("BH wand core", holder, BanquetShowKit.DotMesh(), light);
            halo = BanquetShowKit.Drawn("BH wand halo", holder, BanquetShowKit.SoftDiscMesh(), light);
            flash = Star("BH wand flash", light);
            for (int i = 0; i < Orbits; i++) orbit.Add(Star("BH wand spark", light));
            for (int i = 0; i < PullSparks; i++) pullBurst.Add(Star("BH wand burst", light));
            for (int i = 0; i < LaySparks; i++) layBurst.Add(Star("BH wand burst", light));
            for (int i = 0; i < robeAt.Count; i++) robe.Add(Star("BH robe star", light));
            for (int i = 0; i < Motes; i++)
            {
                var m = BanquetShowKit.Drawn("BH magic mote", holder, BanquetShowKit.SoftDiscMesh(), light);
                m.gameObject.SetActive(false);
                motes.Add(m);
            }
            glow = BanquetShowKit.Point("BH wand light", holder, Gold, .35f * height, 0f);
            glow.transform.localPosition = tip;
        }

        // A four-pointed star: two blades of light across each other (the cloche glints'), 1 across.
        Transform Star(string name, Material m)
        {
            var g = new GameObject(name).transform;
            g.SetParent(holder, false);
            var a = BanquetShowKit.Drawn("ray", g, BanquetShowKit.BladeMesh(), m);
            a.localPosition = new Vector3(0f, -.5f, 0f);
            a.localScale = new Vector3(.16f, 1f, 1f);
            var b = BanquetShowKit.Drawn("ray", g, BanquetShowKit.BladeMesh(), m);
            b.localRotation = Quaternion.Euler(0f, 0f, 90f);
            b.localPosition = new Vector3(.5f, 0f, 0f);
            b.localScale = new Vector3(.16f, 1f, 1f);
            g.gameObject.SetActive(false);
            return g;
        }

        void OnDestroy()
        {
            foreach (var o in owned)
                if (o != null) Destroy(o);
        }

        static float Hash(float x)
        {
            float s = Mathf.Sin(x * 12.9898f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        // ------------------------------------------------------------------ every frame

        void LateUpdate()
        {
            if (holder == null || trick == null || !run.Set) return;
            float u = run.Phase(ObstacleClock.Now);
            var cam = BanquetShowKit.Viewer();
            var eye = cam != null ? cam.transform : null;
            float H = height;

            // the cue's share (0 .. 1 from RollAt to PullAt), then what is left after the pull
            float cue = 0f, after = 0f;
            if (u >= run.RollAt && u < run.PullAt) cue = (u - run.RollAt) / Mathf.Max(.01f, run.PullAt - run.RollAt);
            float sincePull = u - run.PullAt, sinceLay = u - run.LayAt;
            if (sincePull >= 0f) after = Mathf.Exp(-sincePull / AfterGlow * 2.5f);
            float pulse = .5f + .5f * Mathf.Sin(2f * Mathf.PI * (1.2f + 5f * cue) * u);
            float bright = .22f + .08f * pulse + cue * cue * (1.1f + .3f * pulse) + 1.3f * after;
            Brightness = bright;
            int sparks = 0;

            // the tip: core and halo (the halo turned to the camera)
            core.localPosition = tip;
            float cs = H * (.03f + .03f * Mathf.Min(bright, 1.6f));
            core.localScale = new Vector3(cs, cs, cs);
            Tint(core, Color.Lerp(Gold, Color.white, .6f) * Mathf.Min(1f, .6f + .5f * bright));
            halo.localPosition = tip;
            float hs = H * (.08f + .1f * Mathf.Min(bright, 1.8f));
            halo.localScale = new Vector3(hs, hs, hs);
            Face(halo, eye, 0f);
            Tint(halo, Color.Lerp(Gold, Lilac, .45f + .3f * pulse) * Mathf.Min(1f, .35f + .45f * bright));
            glow.intensity = 1.2f * bright;
            glow.color = Color.Lerp(Gold, Lilac, .4f + .3f * pulse);

            // the cue: sparks circling the tip, tightening and quickening, more of them as it builds
            for (int i = 0; i < orbit.Count; i++)
            {
                var s = orbit[i];
                float joins = i / (float)orbit.Count * .6f;
                bool on = cue > joins;
                if (s.gameObject.activeSelf != on) s.gameObject.SetActive(on);
                if (!on) continue;
                sparks++;
                float t = u - run.RollAt, k = Hash(i + 1.7f);
                float r = H * (.11f - .07f * cue) * (.7f + .6f * k);
                float a = 2f * Mathf.PI * (k + t * (.6f + 1.8f * cue) * (i % 2 == 0 ? 1f : -1f));
                float tiltA = 2f * Mathf.PI * Hash(i + 4.1f);
                var ring = new Vector3(Mathf.Cos(a), .55f * Mathf.Sin(a + tiltA), Mathf.Sin(a)) * r;
                s.localPosition = tip + ring;
                float tw = .5f + .5f * Mathf.Sin(2f * Mathf.PI * (3f + 4f * k) * t + 6f * k);
                float size = H * (.03f + .035f * tw) * Mathf.Clamp01((cue - joins) * 6f);
                s.localScale = new Vector3(size, size, size);
                Face(s, eye, 40f * t + 90f * k);
                Tint(s, Color.Lerp(Gold, Lilac, k) * (.55f + .45f * tw));
            }

            // the pull: a flash and a burst; the throw: a smaller burst
            bool flashing = sincePull >= 0f && sincePull < FlashLife;
            if (flash.gameObject.activeSelf != flashing) flash.gameObject.SetActive(flashing);
            if (flashing)
            {
                float f = sincePull / FlashLife, size = H * .32f * Mathf.Sin(Mathf.PI * Mathf.Pow(f, .35f));
                flash.localPosition = tip;
                flash.localScale = new Vector3(size, size, size);
                Face(flash, eye, 25f + 60f * f);
                Tint(flash, Color.Lerp(Color.white, Lilac, f));
                sparks++;
            }
            sparks += Burst(pullBurst, sincePull, 1f, 3.3f, eye);
            sparks += Burst(layBurst, sinceLay, .7f, 9.1f, eye);

            // the robe's stars: a rare twinkle at rest, more and brighter through the cue and after the pull
            float busy = .12f + .88f * Mathf.Max(cue, after);
            for (int i = 0; i < robe.Count; i++)
            {
                var s = robe[i];
                float k = Hash(i + 11.3f), rate = .35f + 1.4f * busy, phase = u * rate + k * 7f, cycle = Mathf.Floor(phase);
                float f = phase - cycle;
                bool chosen = Hash(cycle * 3.1f + i * 1.9f) < .25f + .6f * busy;
                bool on = chosen && f < .5f;
                if (s.gameObject.activeSelf != on) s.gameObject.SetActive(on);
                if (!on) continue;
                sparks++;
                float size = H * (.02f + .035f * busy) * Mathf.Sin(Mathf.PI * f / .5f);
                s.localPosition = robeAt[i];
                s.localScale = new Vector3(size, size, size);
                Face(s, eye, 45f + 90f * f);
                Tint(s, Color.Lerp(Gold, Color.white, .4f) * (.5f + .5f * busy));
            }

            // the motes: spiralling up from his foot round him through the cue, drifting off after the pull
            float moteShare = Mathf.Max(cue, after * .8f);
            for (int i = 0; i < motes.Count; i++)
            {
                var m = motes[i];
                float k = Hash(i + 21.7f), life = 1.6f, age = (u + k * life) % life, f = age / life;
                bool on = moteShare > .05f && Hash(i + 5.5f) < moteShare;
                if (m.gameObject.activeSelf != on) m.gameObject.SetActive(on);
                if (!on) continue;
                sparks++;
                float a = 2f * Mathf.PI * (k + .7f * f) * (i % 2 == 0 ? 1f : -1f);
                float r = H * (.22f + .06f * Mathf.Sin(6f * f + k * 9f));
                m.localPosition = axis + new Vector3(Mathf.Cos(a) * r, H * (.05f + .75f * f), Mathf.Sin(a) * r);
                float size = H * .045f * Mathf.Sin(Mathf.PI * f);
                m.localScale = new Vector3(size, size, size);
                Face(m, eye, 0f);
                Tint(m, Color.Lerp(Lilac, Gold, f) * (.85f * moteShare));
            }
            Sparks = sparks;

            // the figure warms toward lilac through the cue and fades back after the pull
            float warm = Mathf.Max(cue * cue * (.75f + .25f * pulse), after);
            bool tint = warm > .01f;
            if (tint || tinted)
            {
                var c = Color.Lerp(Color.white, FigureLit, warm);
                foreach (var r in figure)
                {
                    if (r == null) continue;
                    if (tint) { block.Clear(); LitMaterials.SetColor(block, c); r.SetPropertyBlock(block); }
                    else r.SetPropertyBlock(null);
                }
                tinted = tint;
            }
        }

        // A burst of sparks from the tip, `since` seconds after it (nothing before), flying out and slowing, sinking.
        int Burst(List<Transform> sparks, float since, float power, float seed, Transform eye)
        {
            bool live = since >= 0f && since < BurstLife;
            int shown = 0;
            for (int i = 0; i < sparks.Count; i++)
            {
                var s = sparks[i];
                if (s.gameObject.activeSelf != live) s.gameObject.SetActive(live);
                if (!live) continue;
                shown++;
                float k = Hash(i * 1.37f + seed), up = Hash(i * 2.11f + seed) * 1.4f - .4f;
                float a = 2f * Mathf.PI * (i + k * .8f) / sparks.Count;
                var dir = new Vector3(Mathf.Cos(a), up, Mathf.Sin(a)).normalized;
                float f = since / BurstLife, travel = height * power * (.16f + .14f * k) * (1f - Mathf.Exp(-since / .18f));
                s.localPosition = tip + dir * travel + Vector3.down * (height * .05f * f * f);
                float size = height * power * (.035f + .02f * k) * (1f - f);
                s.localScale = new Vector3(size, size, size);
                Face(s, eye, 30f * i + 120f * since);
                Tint(s, Color.Lerp(Color.white, i % 3 == 0 ? Lilac : Gold, .3f + .7f * f) * (1f - f * f));
            }
            return shown;
        }

        static void Face(Transform t, Transform eye, float roll)
        {
            if (eye != null) t.rotation = Quaternion.LookRotation(t.position - eye.position) * Quaternion.Euler(0f, 0f, roll);
        }

        readonly List<Renderer> renderers = new List<Renderer>();

        void Tint(Transform t, Color c)
        {
            t.GetComponentsInChildren(renderers);
            foreach (var r in renderers)
            {
                block.Clear();
                LitMaterials.SetColor(block, c);
                r.SetPropertyBlock(block);
            }
        }
    }
}
