using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.PawnRush
{
    // R6 the hall's shell (ArtSource/PawnRush/RoyalObservatory/PLAN.md; ro_r6_hall_model.py, the ro-shell session, 11-19).
    // So far the abyss: the user on the hall floor - "진짜 우주에 온 것 처럼 플레이어가 우주 공포감이 들 수 있게" - so the
    // graybox floor (y -18, looks only; the kill volume is at y -5) stops drawing and a well of deep space goes on down
    // under it to y -110 (RoHallAbyss: its star wall, the nebula round a dark void at its bottom, star and wisp layers
    // between that slide past each other as the camera moves, the gas swirling and billowing, the stars twinkling; the pillars, axles and the great globe's root going on
    // down into the dark; faint gold orbit rings where the floor was).
    // The rest of the hall, every part passed one by one and then the assembly (the user, 11-19: "확인, Unity로"): the
    // dome (star chart, ribs, hoop, crown, sky) over the graybox's ribs; the slit's edges, the dome's outside, the outer
    // wall, the moon and the night; the chess pieces and armillary spheres in the niches; the banners (each on a chain of
    // bones, swung here); and the wall - 104 bays placed here from three models (HallWall).
    public sealed partial class RoyalObservatoryDressing
    {
        static readonly Hang[] Hall =
        {
            new Hang { zone = "Hall", model = "RoHallAbyss", under = "Hall (looks)", covers = new[] { "Hall floor" } },
            new Hang { zone = "Hall", model = "RoHallDome", under = "Hall (looks)", covers = new[] { "Hall wall*", "Dome *" } },
            new Hang { zone = "Hall", model = "RoHallOutside" },
            new Hang { zone = "Hall", model = "RoHallStatues" },
            new Hang { zone = "Hall", model = "RoHallBanners" },
        };

        // ------------------------------------------------------------------ the wall
        // ro_r6_hall_model.py: 36 bays of 10 deg round the hall's middle (Unity (0, 64), the wall's face at r 50), bay
        // edges on the tens so the entry tunnel's opening (170 .. 190) is two whole bays. Each bay is one of three models
        // exported with the hall's middle at the origin and the bay at due south: two arcade tiers (feet y -18, -7.5) and
        // the niches (y 3); over the tunnel the portal from y -7.5 over two arcade bays. A copy turned to its heading is
        // the same mesh (the batcher draws them together). The ledge in front of the niches (the middle arcade's top) gets
        // a light at each lamp globe; the lower ledge's globes only glow.
        const float HallMiddleZ = 64f;
        static readonly float[] ArcadeFeet = { -18f, -7.5f };
        const float NicheFoot = 3f, PortalFoot = -7.5f;

        static readonly Action<RoyalObservatoryDressing> HallWall = dressing =>
        {
            var niche = Resources.Load<GameObject>(ModelFolder + "Hall/RoHallNicheBay");
            var arcade = Resources.Load<GameObject>(ModelFolder + "Hall/RoHallArcadeBay");
            var portal = Resources.Load<GameObject>(ModelFolder + "Hall/RoHallPortal");
            if (niche == null || arcade == null || portal == null) return;
            var arcadeLamps = HallLamps("RoHallArcadeBay");
            var portalLamps = HallLamps("RoHallPortal");
            var root = new GameObject("RoHall wall (model)").transform;
            root.SetParent(dressing.transform, false);
            int bays = 0, lit = 0;
            for (int b = 0; b < 36; b++)
            {
                float heading = 5f + 10f * b;
                bool tunnel = Mathf.Abs(heading - 180f) < 6f;
                dressing.HallBay(root, arcade, heading, ArcadeFeet[0], null, ref lit);
                bays++;
                if (tunnel) continue;
                dressing.HallBay(root, arcade, heading, ArcadeFeet[1], arcadeLamps, ref lit);
                dressing.HallBay(root, niche, heading, NicheFoot, null, ref lit);
                bays += 2;
            }
            dressing.HallBay(root, portal, 180f, PortalFoot, portalLamps, ref lit);
            Debug.Log("[천문대 홀] 벽 " + (bays + 1) + "칸 · 등 " + lit + "개");
        };

        static Vector3[] HallLamps(string model)
        {
            var text = Resources.Load<TextAsset>(ModelFolder + "Hall/" + model + "_lamps");
            var list = new System.Collections.Generic.List<Vector3>();
            if (text == null) return list.ToArray();
            foreach (var line in text.text.Split('\n'))
            {
                var p = line.Trim().Split(' ');
                if (p.Length < 3) continue;
                list.Add(new Vector3(float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture),
                                     float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture),
                                     float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture)));
            }
            return list.ToArray();
        }

        // One bay: a holder on the hall's axis at the tier's foot, turned to the heading; under it the FBX on the
        // export's pivot (a half turn about y, as HangOn), painted with the library; lights at the lamps (holder frame).
        void HallBay(Transform root, GameObject fbx, float heading, float foot, Vector3[] lamps, ref int lit)
        {
            var holder = new GameObject(fbx.name + " " + heading.ToString("000") + " " + foot.ToString("+0.0;-0.0") + " (model)").transform;
            holder.SetParent(root, false);
            holder.localPosition = new Vector3(0f, foot, HallMiddleZ);
            holder.localRotation = Quaternion.Euler(0f, heading - 180f, 0f);
            var pivot = new GameObject("pivot").transform;
            pivot.SetParent(holder, false);
            pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var model = Instantiate(fbx, pivot, false);
            model.name = fbx.name;
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
            PaintModel(model);
            HallLods(model.transform, BayLod);
            if (lamps == null) return;
            foreach (var at in lamps)
            {
                var go = new GameObject("Lamp light (model)");
                go.transform.SetParent(holder, false);
                go.transform.localPosition = at;
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = LampColor;
                light.intensity = LampIntensity;
                light.range = LampRange;
                light.shadows = LightShadows.None;
                lit++;
            }
        }

        // ------------------------------------------------------------------ levels of detail
        // The lightening (11-19, the plan session for the user: "경량화 진행", the hall under 1.2 M triangles): the bays,
        // the statues and the banners come as <name>_LOD0 / _LOD1 / _LOD2 (each passed by the user in pictures, one part
        // at a time). One LODGroup an item, set here (an importer's group, made from the names, is put aside): the
        // screen heights where LOD1 and LOD2 take over; LOD2 is never culled. A bay is about 12 m across, a statue or
        // a banner 3.5 - 4.5 m: LOD1 from about 35 m / 30 m, LOD2 from about 75 m / 75 m with the game camera.
        public static float[] BayLod = { .28f, .12f }, PieceLod = { .09f, .035f };

        static void HallLods(Transform model, float[] heights)
        {
            foreach (var g in model.GetComponentsInChildren<LODGroup>(true))
            {
                g.enabled = false;
                Destroy(g);
            }
            var items = new System.Collections.Generic.Dictionary<string, Renderer[]>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name;
                int at = n.LastIndexOf("_LOD", StringComparison.Ordinal);
                if (at < 0 || at + 4 >= n.Length || !int.TryParse(n.Substring(at + 4), out int level) || level > 2) continue;
                string key = n.Substring(0, at);
                if (!items.TryGetValue(key, out var levels)) items[key] = levels = new Renderer[3];
                levels[level] = r;
            }
            foreach (var levels in items.Values)
            {
                if (levels[0] == null) continue;
                var lods = new System.Collections.Generic.List<LOD>();
                for (int i = 0; i < 3; i++)
                {
                    if (levels[i] == null) continue;
                    float h = i < 2 && i < heights.Length ? heights[i] : 0f;
                    // a level whose next is missing reaches down to the end (never culled)
                    bool last = i == 2 || (levels[i + 1] == null && (i + 2 > 2 || levels[i + 2] == null));
                    lods.Add(new LOD(last ? 0f : h, new[] { levels[i] }));
                }
                var group = levels[0].gameObject.AddComponent<LODGroup>();
                group.SetLODs(lods.ToArray());
                group.RecalculateBounds();
            }
        }

        static readonly Action<RoyalObservatoryDressing> HallStatueLods = dressing =>
        {
            foreach (var m in dressing.FindAll("RoHallStatues")) HallLods(m, PieceLod);
            foreach (var m in dressing.FindAll("RoHallBanners")) HallLods(m, PieceLod);
        };

        // ------------------------------------------------------------------ the banners
        // RoHallBanners: every banner its own skinned mesh on a root bone (on the rod, still) and five down the cloth
        // (ro_r6_hall_model.banner_rig; the cloth's folds are a Blender cloth simulation). Each swings slowly and on its own
        // beat: always a little toward the hall (never into the wall behind it) and a little side to side, more down the
        // banner than at the rod. Degrees a bone at the tip; seconds a swing.
        public static float BannerSwing = 2.2f, BannerSide = 1.1f, BannerPeriod = 4.2f;

        static readonly Action<RoyalObservatoryDressing> HallBanners = dressing =>
        {
            var models = dressing.FindAll("RoHallBanners");
            var axis = dressing.transform.TransformPoint(new Vector3(0f, 0f, HallMiddleZ));
            foreach (var m in models)
                if (m.GetComponent<BannerSway>() == null) m.gameObject.AddComponent<BannerSway>().axis = axis;
        };

        sealed class BannerSway : MonoBehaviour
        {
            public Vector3 axis;

            sealed class Chain
            {
                public Transform[] bones;
                public Quaternion[] rest;
                public Vector3 toHall, along;
                public float sign = 1f, phase, phase2;
            }

            readonly System.Collections.Generic.List<Chain> chains = new System.Collections.Generic.List<Chain>();

            void Start()
            {
                var seen = new System.Collections.Generic.HashSet<Transform>();
                foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var list = new System.Collections.Generic.List<Transform>();
                    for (int i = 1; i <= 5; i++)
                        foreach (var b in smr.bones)
                            if (b != null && b.name.EndsWith(" b" + i, StringComparison.Ordinal)) { list.Add(b); break; }
                    if (list.Count < 2 || !seen.Add(list[0])) continue;      // a banner's LODs share one rig: one chain
                    var c = new Chain { bones = list.ToArray(), rest = new Quaternion[list.Count] };
                    for (int i = 0; i < c.bones.Length; i++) c.rest[i] = c.bones[i].localRotation;
                    var top = c.bones[0].position;
                    var flat = axis - top;
                    flat.y = 0f;
                    c.toHall = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
                    c.along = Vector3.Cross(Vector3.up, c.toHall).normalized;
                    // which way about `along` lifts the banner's foot toward the hall
                    var tip = c.bones[c.bones.Length - 1].position - top;
                    if (Vector3.Dot(Quaternion.AngleAxis(5f, c.along) * tip - tip, c.toHall) < 0f) c.sign = -1f;
                    int h = list[0].name.GetHashCode();
                    c.phase = (h & 1023) / 1023f * 6.2832f;
                    c.phase2 = ((h >> 10) & 1023) / 1023f * 6.2832f;
                    chains.Add(c);
                }
                if (chains.Count == 0) enabled = false;
            }

            void Update()
            {
                float t = Time.time * 6.2832f / Mathf.Max(.5f, BannerPeriod);
                foreach (var c in chains)
                    for (int i = 0; i < c.bones.Length; i++)
                    {
                        var bone = c.bones[i];
                        float k = (i + 1f) / c.bones.Length;
                        float swing = c.sign * BannerSwing * k * (.55f + .45f * Mathf.Sin(t + c.phase - i * .45f));
                        float side = BannerSide * k * Mathf.Sin(t * .73f + c.phase2 - i * .6f);
                        bone.localRotation = c.rest[i];
                        bone.rotation = Quaternion.AngleAxis(swing, c.along) * Quaternion.AngleAxis(side, c.toHall) * bone.rotation;
                    }
            }

            void OnDestroy()
            {
                foreach (var c in chains)
                    for (int i = 0; i < c.bones.Length; i++)
                        if (c.bones[i] != null) c.bones[i].localRotation = c.rest[i];
            }
        }

        // The abyss's own layers (FBX slots "RO | abyss_*"): unlit, made here from the Codex textures beside the FBX.
        // tint: how bright a layer is drawn (the wall's stars dimmer so the nebula reads deeper); additive: the layer
        // adds light over what is under it instead of hiding it.
        // The movement is in the layers' own shader (Resources/RoyalObservatory/Models/Hall/RoHallAbyssLayer.shader) -
        // the user on the first motion, each layer turned whole (11-19): "이게 너무 뭉탱이로 움직여". swirl: radians a
        // cycle at the middle (a vortex, faster inside than out), swirlSpeed: cycles a second; warp: the gas billowing in
        // place; twinkle: each star on its own beat.
        sealed class AbyssLayer
        {
            public string slot, texture;
            public Color tint;
            public bool additive;
            public float swirl, swirlSpeed = .03f, warp, warpSpeed = .03f, twinkle, twinkleSpeed = 1.5f, twinkleCells = 120f;
        }

        static readonly AbyssLayer[] AbyssLayers =
        {
            new AbyssLayer { slot = "abyss_nebula", texture = "RoHallAbyss_Nebula", tint = Color.white, additive = false,
                             swirl = 1.1f, swirlSpeed = .022f, warp = .018f, warpSpeed = .025f },
            new AbyssLayer { slot = "abyss_wall", texture = "RoHallAbyss_Stars", tint = new Color(.55f, .55f, .6f), additive = false,
                             twinkle = .65f, twinkleSpeed = 1.2f, twinkleCells = 110f },
            new AbyssLayer { slot = "abyss_stars", texture = "RoHallAbyss_Stars", tint = new Color(.9f, .9f, 1f), additive = true,
                             swirl = .35f, swirlSpeed = .015f, twinkle = .75f, twinkleSpeed = 1.8f, twinkleCells = 140f },
            new AbyssLayer { slot = "abyss_wisps", texture = "RoHallAbyss_Wisps", tint = new Color(2.2f, 2.2f, 2.4f), additive = true,
                             swirl = .8f, swirlSpeed = .035f, warp = .06f, warpSpeed = .05f },
        };

        static readonly Action<RoyalObservatoryDressing> HallAbyss = dressing =>
        {
            var models = dressing.FindAll("RoHallAbyss");
            if (models.Count == 0) return;
            var made = new System.Collections.Generic.Dictionary<string, Material>();
            int painted = 0;
            foreach (var model in models)
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var layer = mats[i] != null ? AbyssLayerOf(mats[i].name) : null;
                        if (layer == null) continue;
                        if (!made.TryGetValue(layer.slot, out var m))
                            made[layer.slot] = m = AbyssMaterial(layer);
                        if (m != null) { mats[i] = m; painted++; }
                    }
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            Debug.Log("[천문대 홀] 우주 심연 층 " + painted + "개 칠함");
        };

        static AbyssLayer AbyssLayerOf(string materialName)
        {
            string n = materialName.Replace(" (Instance)", "").ToLowerInvariant();
            foreach (var l in AbyssLayers)
                if (n.EndsWith(l.slot, StringComparison.Ordinal)) return l;
            return null;
        }

        static Material AbyssMaterial(AbyssLayer layer)
        {
            var tex = Resources.Load<Texture2D>(ModelFolder + "Hall/" + layer.texture);
            var shader = Resources.Load<Shader>(ModelFolder + "Hall/RoHallAbyssLayer");
            if (shader == null || !shader.isSupported) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null || tex == null)
            {
                Debug.LogWarning("[천문대 홀] 우주 심연 재질을 못 만듦: " + (shader == null ? "셰이더" : layer.texture) + " 없음");
                return null;
            }
            var m = new Material(shader) { name = "RoHallAbyss " + layer.slot };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", layer.tint);
            m.SetFloat("_Swirl", layer.swirl);
            m.SetFloat("_SwirlSpeed", layer.swirlSpeed);
            m.SetFloat("_Warp", layer.warp);
            m.SetFloat("_WarpSpeed", layer.warpSpeed);
            m.SetFloat("_Twinkle", layer.twinkle);
            m.SetFloat("_TwinkleSpeed", layer.twinkleSpeed);
            m.SetFloat("_TwinkleCells", layer.twinkleCells);
            if (layer.additive)
            {
                // additive: the layer adds its light over what is under it (and, should the fallback URP Unlit be in
                // use, its own Transparent / Additive switches)
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 2f);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
            }
            return m;
        }
    }
}
