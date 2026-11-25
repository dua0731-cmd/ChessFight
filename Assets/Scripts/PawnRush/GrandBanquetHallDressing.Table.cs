using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // B2 the long table (B0's test piece first, seoungtest R837; ArtSource/PawnRush/BanquetHall/bh_b0_table_model.py, the
    // shapes after the R836 sheet BH_A04): the table's walnut body, the cloth and runner (they ride the tablecloth trick's
    // picture "BH tablecloth", pivoted on its south end: pulled south, hidden, laid again by its z scale), the plates,
    // goblets and candelabra, the guests' chairs. Colliders, the plates' floors and the trick stay the builder's.
    public sealed partial class GrandBanquetHallDressing
    {
        static readonly Hang[] TableHangs =
        {
            new Hang { zone = "B2", model = "bh_table_body", covers = new[] { "BH long table" } },
            new Hang { zone = "B2", model = "bh_table_cloth", on = "BH tablecloth", under = "BH tablecloth", blank = new[] { "BH cloth", "BH runner" } },
            new Hang { zone = "B2", model = "bh_table_ware", covers = new[] { "BH plate *" } },
            new Hang { zone = "B2", model = "bh_table_chairs", covers = new[] { "BH guests' chairs" } },
            // the people (the user's figurines, bh_figures.py): the guests on the chairs, the magician at the south end
            new Hang { zone = "B2", model = "bh_guests", covers = new[] { "BH guest *" } },
            new Hang { zone = "B2", model = "bh_magician", covers = new[] { "BH magician" } },
            // the cloche lifts (bh_b2_cloche_model.py, BH_A05): each part rides its own root; the dumbwaiter's frame
            Lift("bh_cloche", "BH cloche W", "BH cloche band", "BH cloche lid", "BH cloche dome", "BH cloche handle"),
            Lift("bh_cloche", "BH cloche E", "BH cloche band", "BH cloche lid", "BH cloche dome", "BH cloche handle"),
            Lift("bh_platter", "BH cloche platter W", "BH platter", "BH platter lug"),
            Lift("bh_platter", "BH cloche platter E", "BH platter", "BH platter lug"),
            new Hang { zone = "B2", model = "bh_dumbwaiter", covers = new[] { "BH dumbwaiter post", "BH dumbwaiter post cap", "BH dumbwaiter beam", "BH dumbwaiter pulley" } },
        };

        static Hang Lift(string model, string root, params string[] covers) =>
            new Hang { zone = "B2", model = model, on = root, under = root, covers = covers };

        // The lifts' lines (HoistLine, stretched by the builder each frame): the platters' steel cables keep their line,
        // drawn in the library's silver; each cloche's chain is gold links laid from its pulley to the handle's loop
        // (HoistChain), the builder's line hidden.
        static readonly Action<GrandBanquetHallDressing> LiftLines = d =>
        {
            var silver = Resources.Load<Material>(MaterialFolder + "BH_Silver");
            var link = Resources.Load<GameObject>(ModelFolder + "B2/bh_chain_link");
            foreach (var line in d.GetComponentsInChildren<HoistLine>(true))
            {
                var name = line.name;
                if (name.StartsWith("BH platter cable", StringComparison.Ordinal))
                {
                    if (silver != null && line.TryGetComponent<Renderer>(out var r)) r.sharedMaterial = silver;
                }
                else if (name.StartsWith("BH cloche chain ", StringComparison.Ordinal) && link != null)
                {
                    var cloche = d.FindAll("BH cloche " + name.Substring("BH cloche chain ".Length));
                    if (cloche.Count == 0) continue;
                    if (line.TryGetComponent<Renderer>(out var r)) r.enabled = false;
                    var chain = new GameObject(name + " links (model)");
                    chain.transform.SetParent(d.transform, false);
                    chain.AddComponent<HoistChain>().Configure(line, cloche[0], link, d);
                }
            }
        };

        // The trick switches the graybox cloth's renderers itself (hidden while the magician holds it, its material red
        // while the drum rolls); the model follows the graybox cloth's renderer, which the dressing emptied but left on.
        static readonly Action<GrandBanquetHallDressing> TableCloth = d =>
        {
            foreach (var host in d.FindAll("BH tablecloth"))
            {
                var model = ModelOn(host, "bh_table_cloth");
                var source = host.Find("BH cloth");
                if (model == null || source == null || !source.TryGetComponent<Renderer>(out var r)) continue;
                model.gameObject.AddComponent<BanquetClothModel>().Configure(r);
            }
        };
    }

    // A cloche's chain (BH_A05): gold links 0.3 m long (bh_chain_link, 0.236 m apart as they interlock), each turned a
    // quarter to the last, from the pulley down to the loop on the cloche's handle (3.78 m over its root in the model,
    // bh_b2_cloche_model.py), laid again every frame as the lift moves (only the links the length needs are shown).
    [DefaultExecutionOrder(210)]
    public sealed class HoistChain : MonoBehaviour
    {
        public const float Pitch = .236f, LoopOnCloche = 3.78f;
        [SerializeField] HoistLine line;
        [SerializeField] Transform cloche;
        readonly List<Transform> links = new List<Transform>();
        GameObject prefab;
        GrandBanquetHallDressing dressing;

        public void Configure(HoistLine hoist, Transform clocheRoot, GameObject linkModel, GrandBanquetHallDressing d)
        {
            line = hoist;
            cloche = clocheRoot;
            prefab = linkModel;
            dressing = d;
        }

        Transform Link(int i)
        {
            while (links.Count <= i)
            {
                var holder = new GameObject("link").transform;
                holder.SetParent(transform, false);
                var m = Instantiate(prefab, holder, false);
                // the model is built along Blender z (Unity y) round its middle; the observatory's half turn about y, and
                // every other link a quarter more so they interlock
                m.transform.localRotation = Quaternion.Euler(0f, links.Count % 2 == 0 ? 180f : 270f, 0f);
                foreach (var c in m.GetComponentsInChildren<Collider>(true)) Destroy(c);
                dressing.PaintModel(m);
                links.Add(holder);
            }
            return links[i];
        }

        void LateUpdate()
        {
            if (line == null || cloche == null || prefab == null) return;
            Vector3 top = line.PulleyPoint, bottom = cloche.TransformPoint(0f, LoopOnCloche, 0f), d = bottom - top;
            float length = d.magnitude;
            int n = Mathf.Max(0, Mathf.CeilToInt(length / Pitch));
            var turn = length > 1e-4f ? Quaternion.FromToRotation(Vector3.up, d / length) : Quaternion.identity;
            for (int i = 0; i < n; i++)
            {
                var l = Link(i);
                l.gameObject.SetActive(true);
                l.SetPositionAndRotation(bottom - d / length * (Pitch * (i + .5f)), turn);
            }
            for (int i = n; i < links.Count; i++) links[i].gameObject.SetActive(false);
        }
    }

    // The cloth model shown and hidden with the graybox cloth's renderer, and its linen warmed toward red while the drum
    // rolls (the graybox's cue, BanquetDevices.TableclothTrick: its renderer then draws the "rolling" material).
    [DefaultExecutionOrder(200)]
    public sealed class BanquetClothModel : MonoBehaviour
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        [SerializeField] Renderer source;
        Renderer[] mine = Array.Empty<Renderer>();
        Material lying;
        MaterialPropertyBlock block;
        bool tinted;

        public void Configure(Renderer graybox)
        {
            source = graybox;
            lying = graybox.sharedMaterial;
            mine = GetComponentsInChildren<Renderer>(true);
            block = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            if (source == null) return;
            bool shown = source.enabled;
            bool rolling = source.sharedMaterial != lying;
            foreach (var r in mine)
            {
                if (r.enabled != shown) r.enabled = shown;
                if (rolling == tinted || !r.name.EndsWith("cloth", StringComparison.Ordinal)) continue;   // the linen only
                if (rolling)
                {
                    block.Clear();
                    block.SetColor(BaseColor, new Color(1f, .55f, .5f, 1f));
                    r.SetPropertyBlock(block);
                }
                else r.SetPropertyBlock(null);
            }
            tinted = rolling;
        }
    }
}
