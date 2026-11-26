using System;
using System.Collections.Generic;
using ChessFight.Game;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // B7 the hall's shell (bh-hall session, seoungtest R838; ArtSource/PawnRush/BanquetHall/bh_b7_hall_model.py, the shapes
    // after the R836 sheets BH_A01 and BH_A02, put together by bh_b7_hall_assembly.py, written by bh_hall_export.py): the
    // walls in two storeys (painted panel bays below, arched window bays above, the walnut frieze), the south doorway (the
    // user's Tripo arch with its own baked textures), the north exit's arch, the pilasters with their sconces and banners,
    // the east wall's three hearths, the coffered ceiling with a rose over every rope. Colliders stay the builder's.
    //
    // Also here, for every hung model of the hall:
    //  - the hearths' fire (the user 11-25: "나무 화로를 넣어서 불에 타고 연기가 나는 연출"): flames, sparks on every pop of
    //    a crackle made in code, smoke going up the flue, a flickering warm light - the lobby braziers' (LobbyStage.Fire)
    //    made bigger for a 4.4 m hearth;
    //  - each mesh's own LODGroup: bh_lod.py / bh_hall_export.py write a part's levels as "<part>_LOD0/1/2"; Unity's
    //    importer puts them all in one LODGroup the size of the whole FBX (the hall's width - it would never switch), so
    //    that one is taken away and every part gets its own.
    public sealed partial class GrandBanquetHallDressing
    {
        static readonly Hang[] HallHangs =
        {
            new Hang { zone = "Hall", model = "bh_hall_walls", covers = new[] { "BH hall wall *", "BH entry lintel", "BH exit lintel" } },
            new Hang { zone = "Hall", model = "bh_hall_entry" },
            new Hang { zone = "Hall", model = "bh_hall_pilasters" },
            new Hang { zone = "Hall", model = "bh_hall_fireplaces" },
            new Hang { zone = "Hall", model = "bh_hall_ceiling", covers = new[] { "BH ceiling rose" } },
            // the floor round the dance floor (user 11-25: "시작구간 바닥"): marble slabs, walnut joints and band
            new Hang { zone = "Hall", model = "bh_hall_floor", covers = new[] { "BH hall floor" } },
            // the way (user 11-25: "안내 글씨 지우고 ...", then "빨간 카펫 지워주고 화살표 방향으로 해줘"): the beats' signs
            // and the builder's red carpets and gold arrows hidden; flowing gold chevrons along the way instead
            // (BanquetWayArrows, the capstan's turn arrows)
            new Hang { zone = "Hall", model = "", covers = new[] { "Label *", "BH carpet", "BH arrow" } },
        };

        // The hearths: the middle of each log pile (bh_b7_hall_model.fireplace_bay: 1.29 m into the east wall behind its
        // face at x 32, 0.45 m up), in the east wall's three southern lower bays (bh_b7_hall_assembly: z 6.17, 16.11, 26.06).
        static readonly Vector3[] Hearths = { new Vector3(33.29f, .45f, 6.171f), new Vector3(33.29f, .45f, 16.114f), new Vector3(33.29f, .45f, 26.057f) };

        static readonly Action<GrandBanquetHallDressing> HearthFires = d =>
        {
            if (Resources.Load<GameObject>(ModelFolder + "Hall/bh_hall_fireplaces") == null) return;   // no hearths, no fire
            d.gameObject.AddComponent<BanquetHearthFires>().Build(Hearths);
        };

        static readonly Action<GrandBanquetHallDressing> PerPartLods = d => d.SplitLods();

        static readonly Action<GrandBanquetHallDressing> WayArrows = d =>
        {
            if (!d.gameObject.AddComponent<BanquetWayArrows>().Build(d.FindAll("BH carpet"), d.FindAll("BH arrow")))
                // no arrows here (their material or texture missing): the builder's carpets and arrows again
                foreach (var name in new[] { "BH carpet", "BH arrow" })
                    foreach (var t in d.FindAll(name))
                        if (t.TryGetComponent<MeshRenderer>(out var r)) r.enabled = true;
        };

        // The Tripo arch's own colour (its 4K bake, 2048 px here): Unity did not take the texture embedded in the FBX,
        // so its material is made here from the library's stone (same shader and light) with the bake as its colour.
        static readonly Action<GrandBanquetHallDressing> EntryArchBake = d =>
        {
            var model = d.FindAll("bh_hall_entry (model)");
            var bake = Resources.Load<Texture2D>(ModelFolder + "Hall/bh_hall_entry_basecolor");
            var stone = Resources.Load<Material>(MaterialFolder + "BH_Stone");
            if (model.Count == 0 || bake == null || stone == null) return;
            var m = new Material(stone) { name = "BH hall entry (Tripo bake)", mainTexture = bake, color = Color.white };
            m.mainTextureScale = Vector2.one;
            m.mainTextureOffset = Vector2.zero;
            if (m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
            d.ownedMaterials.Add(m);
            foreach (var root in model)
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] == null || !mats[i].name.StartsWith("BH", StringComparison.Ordinal)) mats[i] = m;
                    r.sharedMaterials = mats;
                }
        };

        readonly List<Material> ownedMaterials = new List<Material>();

        void OnDestroy()
        {
            foreach (var m in ownedMaterials) if (m != null) Destroy(m);
        }

        readonly HashSet<Transform> lodDone = new HashSet<Transform>();

        // Screen heights (share of the screen) at which a part leaves each level: two levels / three levels.
        static readonly float[] Lod2 = { .1f, 0f }, Lod3 = { .16f, .05f, 0f };

        public void SplitLods()
        {
            int groups = 0;
            foreach (var model in GetComponentsInChildren<Transform>(true))
            {
                if (!IsModel(model) || !lodDone.Add(model)) continue;
                foreach (var g in model.GetComponentsInChildren<LODGroup>(true)) DestroyImmediate(g);
                var parts = new Dictionary<string, SortedDictionary<int, List<Renderer>>>();
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    string n = r.name;
                    int at = n.LastIndexOf("_LOD", StringComparison.Ordinal);
                    if (at < 0 || !int.TryParse(n.Substring(at + 4), out int k)) continue;
                    string key = r.transform.parent.GetInstanceID() + "|" + n.Substring(0, at);
                    if (!parts.TryGetValue(key, out var levels)) parts[key] = levels = new SortedDictionary<int, List<Renderer>>();
                    if (!levels.TryGetValue(k, out var list)) levels[k] = list = new List<Renderer>();
                    list.Add(r);
                }
                foreach (var levels in parts.Values)
                {
                    if (levels.Count < 2) continue;
                    var cut = levels.Count == 2 ? Lod2 : Lod3;
                    var lods = new LOD[levels.Count];
                    int i = 0;
                    Transform first = null;
                    foreach (var kv in levels)
                    {
                        first = first ?? kv.Value[0].transform;
                        lods[i] = new LOD(cut[Mathf.Min(i, cut.Length - 1)], kv.Value.ToArray());
                        i++;
                    }
                    var holder = new GameObject(first.name.Substring(0, first.name.LastIndexOf("_LOD", StringComparison.Ordinal)) + " (lods)");
                    holder.transform.SetParent(first.parent, false);
                    var group = holder.AddComponent<LODGroup>();
                    group.fadeMode = LODFadeMode.None;
                    group.SetLODs(lods);
                    group.RecalculateBounds();
                    groups++;
                }
            }
            if (groups > 0) Debug.Log("[대연회장 입히기] 부품별 LOD " + groups + "개");
        }
    }

    // The way as flowing arrows (user 11-25: "빨간 카펫 지워주고 화살표 방향으로 해줘 미니게임에 화살표 방향으로 도는 거
    // 이미 있잖아"): the builder's red carpets and gold arrows stop drawing, and along each stretch of the way (each
    // carpet's run between the builder's points, TheWay) soft gold chevrons lie just over the surface and keep flowing
    // the way on, each pulsing a little - the capstan's turn arrows (MiniGameTurnArrows: Resources/PawnRushMiniGames/Fx/
    // T_Turn_Chevron on URPVariants/Particles Additive, HDR gold so the bloom takes it). An arrow of the builder's with
    // no carpet under it (the fountain's top, to the loft) pulses where it is. Pictures only, from the clock.
    public sealed class BanquetWayArrows : MonoBehaviour
    {
        const float Spacing = 1.5f, Speed = .9f, Size = .8f, Lift = .03f;     // m, m/s, m, m over the surface
        static readonly Color Gold = new Color(2.6f, 1.75f, .55f, 1f);

        sealed class Run { public Vector3 from, dir; public float length; public readonly List<Renderer> quads = new List<Renderer>(); }

        readonly List<Run> runs = new List<Run>();
        readonly List<Renderer> still = new List<Renderer>();
        Material additive;
        Texture2D chevron;
        MaterialPropertyBlock block;

        public bool Build(List<Transform> carpets, List<Transform> bars)
        {
            var a = Resources.Load<Material>("URPVariants/Particles Additive");
            chevron = Resources.Load<Texture2D>("PawnRushMiniGames/Fx/T_Turn_Chevron");
            if (a == null || chevron == null || carpets.Count == 0) return false;
            additive = new Material(a) { name = "Banquet way arrows" };
            block = new MaterialPropertyBlock();
            foreach (var c in carpets)
            {
                var s = c.lossyScale;
                Vector3 dir = Vector3.ProjectOnPlane(c.forward, Vector3.up).normalized;
                float length = Mathf.Max(0f, s.z - s.x);                 // the builder's carpet runs 0.8 m past each point
                if (length < .5f) continue;
                Vector3 top = c.position + Vector3.up * (s.y * .5f + Lift);
                var run = new Run { from = top - dir * (length * .5f), dir = dir, length = length };
                int n = Mathf.Max(1, Mathf.RoundToInt(length / Spacing));
                for (int i = 0; i < n; i++) run.quads.Add(Quad());
                runs.Add(run);
            }
            // the builder's arrows no carpet lies under: a chevron each, pulsing in place
            for (int i = 0; i + 1 < bars.Count; i += 2)
            {
                Vector3 mid = (bars[i].position + bars[i + 1].position) * .5f;
                bool onCarpet = false;
                foreach (var c in carpets)
                {
                    var local = c.InverseTransformPoint(mid);
                    if (Mathf.Abs(local.x) <= .5f && Mathf.Abs(local.z) <= .5f && Mathf.Abs(local.y) < 10f) { onCarpet = true; break; }
                }
                if (onCarpet) continue;
                Vector3 dir = Vector3.ProjectOnPlane(bars[i].forward + bars[i + 1].forward, Vector3.up).normalized;
                var q = Quad();
                q.transform.SetPositionAndRotation(mid + Vector3.up * (Lift - bars[i].lossyScale.y * .5f), Quaternion.LookRotation(Vector3.down, dir));
                still.Add(q);
            }
            Tick(0f);
            Debug.Log("[대연회장 입히기] 길 안내 화살표 " + runs.Count + "구간 · 흐르는 화살표 " + CountFlowing() + "개 · 제자리 " + still.Count + "개");
            return true;
        }

        int CountFlowing() { int n = 0; foreach (var r in runs) n += r.quads.Count; return n; }

        Renderer Quad()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(go.GetComponent<Collider>());
            go.name = "BH way arrow";
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * Size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = additive;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            block.Clear(); block.SetTexture("_BaseMap", chevron); block.SetColor("_BaseColor", Color.clear); r.SetPropertyBlock(block);
            return r;
        }

        void Update() => Tick(Time.time);

        void Tick(float time)
        {
            int k = 0;
            foreach (var run in runs)
            {
                int n = run.quads.Count;
                float gap = run.length / n;
                for (int i = 0; i < n; i++)
                {
                    float s = Mathf.Repeat(i * gap + time * Speed, run.length);
                    var q = run.quads[i];
                    // flat on the surface, the chevron's point (+V) along the way
                    q.transform.SetPositionAndRotation(run.from + run.dir * s, Quaternion.LookRotation(Vector3.down, run.dir));
                    // in at the run's start, out at its end (0.6 m each), pulsing a little
                    float edge = Mathf.Clamp01(Mathf.Min(s, run.length - s) / .6f);
                    Paint(q, edge * (.6f + .4f * Mathf.Sin(time * 3.2f - (k++) * .9f)));
                }
            }
            foreach (var q in still) Paint(q, .6f + .4f * Mathf.Sin(time * 3.2f));
        }

        void Paint(Renderer r, float a)
        {
            block.Clear(); r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", new Color(Gold.r * a, Gold.g * a, Gold.b * a, a));   // premultiplied (additive)
            r.SetPropertyBlock(block);
            r.enabled = a > .01f;
        }

        void OnDestroy()
        {
            if (additive != null) Destroy(additive);
        }
    }

    // The three hearths burning: the lobby braziers' fire (LobbyStage.Fire - flames, sparks jumping on the crackle's
    // pops, smoke, a flickering light, the crackle made in code) spread along a 4.4 m hearth, the smoke drawn up the flue.
    public sealed class BanquetHearthFires : MonoBehaviour
    {
        sealed class Fire
        {
            public ParticleSystem Sparks;
            public Light Glow;
            public AudioSource Crackle;
            public float BaseIntensity, Seed;
            public int NextPop;
        }

        readonly List<Fire> fires = new List<Fire>();
        readonly List<Material> owned = new List<Material>();
        static Texture2D flameDot, smokePuff;
        static AudioClip crackleClip;
        static float[] crackleTimes;

        public void Build(Vector3[] hearths)
        {
            var flame = LitMaterials.NewAdditiveParticles(FlameDot(), Color.white);
            var smoke = SmokeMaterial();
            if (flame == null || smoke == null) return;
            owned.Add(flame); owned.Add(smoke);
            var clip = CrackleClip();
            for (int i = 0; i < hearths.Length; i++)
            {
                var holder = new GameObject("BH hearth fire " + (i + 1));
                holder.transform.SetParent(transform, false);
                holder.transform.localPosition = hearths[i];
                var fire = new Fire { Seed = i * 7.31f };
                MakeFlames(holder.transform, flame);
                fire.Sparks = MakeSparks(holder.transform, flame);
                MakeSmoke(holder.transform, smoke);
                // the glow: in front of the logs, out into the room
                var glow = new GameObject("BH hearth glow").AddComponent<Light>();
                glow.transform.SetParent(holder.transform, false);
                glow.transform.localPosition = new Vector3(-1.6f, .9f, 0f);
                glow.type = LightType.Point;
                glow.color = new Color(1f, .58f, .26f);
                glow.range = 11f;
                glow.intensity = 3.2f;
                glow.shadows = LightShadows.None;
                fire.Glow = glow;
                fire.BaseIntensity = glow.intensity;
                fire.Crackle = holder.AddComponent<AudioSource>();
                fire.Crackle.clip = clip;
                fire.Crackle.loop = true;
                fire.Crackle.spatialBlend = 1f;
                fire.Crackle.rolloffMode = AudioRolloffMode.Linear;
                fire.Crackle.minDistance = 4f;
                fire.Crackle.maxDistance = 32f;
                fire.Crackle.volume = 0f;
                fire.Crackle.dopplerLevel = 0f;
                SectionSoundGate.Gate(fire.Crackle);   // R844: heard only in the hall, times the effects volume (the gate's)
                fire.Crackle.Play();
                fire.Crackle.time = i * clip.length * .31f;     // out of step with each other
                fire.NextPop = NextPopIndex(fire.Crackle.time);
                fires.Add(fire);
            }
        }

        void Update()
        {
            float t = Time.unscaledTime;
            float volume = LoadingScreenView.Showing ? 0f : .3f;   // R844: the effects volume is SectionSoundGate's now
            foreach (var fire in fires)
            {
                if (fire.Crackle != null) fire.Crackle.volume = volume;
                if (fire.Crackle != null && crackleTimes != null && crackleTimes.Length > 0)
                {
                    float now = fire.Crackle.time;
                    int guard = 0;
                    while (guard++ < 8)
                    {
                        float pop = crackleTimes[fire.NextPop];
                        float last = crackleTimes[(fire.NextPop + crackleTimes.Length - 1) % crackleTimes.Length];
                        bool passed = pop <= now && (last < pop || now < last);
                        if (fire.NextPop == 0) passed = pop <= now && now < crackleTimes[crackleTimes.Length - 1];
                        if (!passed) break;
                        fire.Sparks.Emit(UnityEngine.Random.Range(8, 20));
                        fire.NextPop = (fire.NextPop + 1) % crackleTimes.Length;
                    }
                }
                if (fire.Glow != null)
                {
                    float n = Mathf.PerlinNoise(t * 3.1f, fire.Seed) * .6f + Mathf.PerlinNoise(t * 9.7f, fire.Seed + 3f) * .4f;
                    fire.Glow.intensity = fire.BaseIntensity * (.8f + .3f * n);
                }
            }
        }

        void OnDestroy()
        {
            foreach (var m in owned) if (m != null) Destroy(m);
        }

        static int NextPopIndex(float time)
        {
            if (crackleTimes == null || crackleTimes.Length == 0) return 0;
            for (int i = 0; i < crackleTimes.Length; i++) if (crackleTimes[i] > time) return i;
            return 0;
        }

        // Along the hearth (Unity z) the fire is a long bed; the shapes lie flat (their circle turned up) and are
        // stretched along z.
        static void Bed(ParticleSystem ps, float radius, float along)
        {
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0, 0);
            shape.scale = new Vector3(.45f, along, 1f);
        }

        static void MakeFlames(Transform parent, Material material)
        {
            var ps = NewSystem(parent, "Flames", material);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.5f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.7f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.4f, .85f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-.4f, .4f);
            main.maxParticles = 400;
            var emission = ps.emission; emission.rateOverTime = 130f;
            Bed(ps, .8f, 1.6f);
            var color = ps.colorOverLifetime; color.enabled = true;
            color.color = Gradient(
                new[] { (0f, new Color(1f, .86f, .5f)), (.25f, new Color(1f, .58f, .16f)), (.65f, new Color(.92f, .26f, .05f)), (1f, new Color(.35f, .06f, .02f)) },
                new[] { (0f, 0f), (.12f, .7f), (.6f, .4f), (1f, 0f) });
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, .7f), new Keyframe(.25f, 1f), new Keyframe(1, .25f)));
            var noise = ps.noise; noise.enabled = true; noise.strength = .45f; noise.frequency = 1.3f; noise.scrollSpeed = 1.2f;
        }

        static ParticleSystem MakeSparks(Transform parent, Material material)
        {
            var ps = NewSystem(parent, "Sparks", material);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.9f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.03f, .07f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .78f, .35f), new Color(1f, .5f, .12f));
            main.gravityModifier = .18f;
            main.maxParticles = 300;
            var emission = ps.emission; emission.rateOverTime = 6f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 24f; shape.radius = .6f;
            shape.rotation = new Vector3(-90f, 0, 0); shape.scale = new Vector3(.45f, 1.6f, 1f);
            var color = ps.colorOverLifetime; color.enabled = true;
            color.color = Gradient(new[] { (0f, new Color(1f, .9f, .6f)), (.5f, new Color(1f, .5f, .12f)), (1f, new Color(.6f, .12f, .03f)) },
                                   new[] { (0f, 1f), (.7f, .85f), (1f, 0f) });
            var noise = ps.noise; noise.enabled = true; noise.strength = .9f; noise.frequency = 2.2f; noise.scrollSpeed = .8f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.velocityScale = .05f; renderer.lengthScale = 1.2f;
            return ps;
        }

        static void MakeSmoke(Transform parent, Material material)
        {
            var ps = NewSystem(parent, "Smoke", material);
            ps.transform.localPosition = new Vector3(0, .9f, 0);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.5f, .9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.6f, 1.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 120;
            var emission = ps.emission; emission.rateOverTime = 14f;
            Bed(ps, .6f, 1.4f);
            var color = ps.colorOverLifetime; color.enabled = true;
            color.color = Gradient(new[] { (0f, new Color(.32f, .29f, .26f)), (1f, new Color(.62f, .6f, .58f)) },
                                   new[] { (0f, 0f), (.15f, .32f), (.6f, .18f), (1f, 0f) });
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, .6f), new Keyframe(1, 2.6f)));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.4f, .4f);
            // drawn up the flue (Unity +y) and a little back into the wall (+x), so it does not fill the hall
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(.05f, .15f); velocity.y = new ParticleSystem.MinMaxCurve(.3f, .5f); velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var noise = ps.noise; noise.enabled = true; noise.strength = .2f; noise.frequency = .5f; noise.scrollSpeed = .3f;
        }

        static ParticleSystem NewSystem(Transform parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.loop = true;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            ps.Play();
            return ps;
        }

        static ParticleSystem.MinMaxGradient Gradient((float t, Color c)[] colors, (float t, float a)[] alphas)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[colors.Length];
            for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i].c, colors[i].t);
            var ak = new GradientAlphaKey[alphas.Length];
            for (int i = 0; i < alphas.Length; i++) ak[i] = new GradientAlphaKey(alphas[i].a, alphas[i].t);
            g.SetKeys(ck, ak);
            return new ParticleSystem.MinMaxGradient(g);
        }

        static Texture2D FlameDot()
        {
            if (flameDot != null) return flameDot;
            const int n = 64;
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            flameDot = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "BH hearth flame dot", wrapMode = TextureWrapMode.Clamp };
            flameDot.SetPixels(px); flameDot.Apply(true);
            return flameDot;
        }

        static Material SmokeMaterial()
        {
            var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
            if (shader == null) return null;
            if (smokePuff == null)
            {
                const int n = 96;
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float cloud = Mathf.PerlinNoise(x * .08f + 3.1f, y * .08f + 7.7f) * .65f + Mathf.PerlinNoise(x * .21f, y * .21f) * .35f;
                        float a = Mathf.Clamp01(1f - r) * Mathf.Lerp(.45f, 1f, cloud);
                        px[y * n + x] = new Color(1f, 1f, 1f, a * a);
                    }
                smokePuff = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "BH hearth smoke", wrapMode = TextureWrapMode.Clamp };
                smokePuff.SetPixels(px); smokePuff.Apply(true);
            }
            var m = new Material(shader) { mainTexture = smokePuff, color = Color.white, name = "BH hearth smoke" };
            LitMaterials.MakeTransparent(m, additive: false);
            return m;
        }

        // Eight seconds of a fire crackling (LobbyStage.Fire's): a low rush, ticks and now and then a louder pop; the
        // pops' times kept so the sparks jump with them.
        static AudioClip CrackleClip()
        {
            if (crackleClip != null) return crackleClip;
            const int rate = 44100; const float seconds = 8f;
            int count = (int)(rate * seconds);
            var data = new float[count];
            var random = new System.Random(1125);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < count; i++)
            {
                float white = (float)(random.NextDouble() * 2 - 1);
                lp += (white - lp) * .02f;
                lp2 += (lp - lp2) * .05f;
                float wobble = .7f + .3f * Mathf.Sin(i / (float)rate * 1.3f) * Mathf.Sin(i / (float)rate * .37f);
                data[i] = lp2 * 1.6f * wobble;
            }
            var pops = new List<float>();
            float at = .05f;
            while (at < seconds - .1f)
            {
                bool big = random.NextDouble() < .28;
                float amp = big ? .75f + (float)random.NextDouble() * .25f : .18f + (float)random.NextDouble() * .3f;
                int start = (int)(at * rate);
                int clicks = big ? 1 + random.Next(3) : 1;
                for (int c = 0; c < clicks; c++)
                {
                    int s0 = start + c * random.Next(300, 1400);
                    float decay = big ? .0025f : .0012f;
                    int len = (int)(decay * rate * 6);
                    float tone = 1800f + (float)random.NextDouble() * 2600f;
                    for (int k = 0; k < len && s0 + k < count; k++)
                    {
                        float tt = k / (float)rate;
                        float env = Mathf.Exp(-tt / decay);
                        float noise = (float)(random.NextDouble() * 2 - 1);
                        data[s0 + k] += amp * env * (noise * .7f + Mathf.Sin(2f * Mathf.PI * tone * tt) * .3f) * (c == 0 ? 1f : .55f);
                    }
                }
                if (big) pops.Add(at);
                at += big ? .25f + (float)random.NextDouble() * .7f : .04f + (float)random.NextDouble() * .22f;
            }
            float peak = 0f;
            for (int i = 0; i < count; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = peak > 0f ? .9f / peak : 1f;
            int fade = rate / 20;
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade;
                data[i] = data[i] * w + data[count - fade + i] * (1f - w);
            }
            for (int i = 0; i < count; i++) data[i] *= gain;
            crackleClip = AudioClip.Create("BH hearth crackle", count - fade, 1, rate, false);
            var trimmed = new float[count - fade];
            Array.Copy(data, trimmed, count - fade);
            crackleClip.SetData(trimmed, 0);
            crackleTimes = pops.ToArray();
            return crackleClip;
        }
    }
}
