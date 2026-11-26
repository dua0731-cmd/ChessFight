using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // B2 the long table (B0's test piece first, seoungtest R837; ArtSource/PawnRush/BanquetHall/bh_b0_table_model.py, the
    // shapes after the R836 sheet BH_A04): the table's walnut body, the cloth and runner (hung on the tablecloth trick's
    // picture "BH tablecloth"; R844: BanquetTableShow takes them off it and moves their vertices - drawn out from under
    // the ware into the magician's hands, flung back), the plates, goblets and candelabra, the guests' chairs. Colliders
    // and the trick stay the builder's, but for the plates: R844 makes them rock where they stand (BanquetPlate).
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

        // The table's show (BanquetTableShow.cs, the user 11-25): the cloth drawn out from under the ware and flung back,
        // the plates rocking where they stand (their colliders with them: BanquetPlate), the goblets and candelabra
        // dragged and rocking, the candles put out and lit in turn, the magician leaning. The trick keeps its timing and
        // its throw; its own picture (the emptied graybox cloth it slides and stretches) is no longer followed.
        static readonly Action<GrandBanquetHallDressing> TableShow = d =>
        {
            var trick = d.GetComponentInChildren<TableclothTrick>(true);
            if (trick != null) d.gameObject.AddComponent<BanquetTableShow>().Configure(d, trick);
        };

        // R845: the cloche lifts' show (BanquetLiftShow.cs: the pulleys turn, the lifts jolt as they arrive, steam and a
        // glint as a cloche lifts off) and the seated guests watching the trick (BanquetGuestShow.cs) - pictures only,
        // the lifts' course, the trick and every collider as built.
        static readonly Action<GrandBanquetHallDressing> LiftAndGuestShow = d =>
        {
            d.gameObject.AddComponent<BanquetLiftShow>().Configure(d);
            var trick = d.GetComponentInChildren<TableclothTrick>(true);
            if (trick == null) return;
            d.gameObject.AddComponent<BanquetGuestShow>().Configure(d, trick);
            // R849: magic on the magician's wand and on him through the trick's cue (BanquetWandShow.cs)
            d.gameObject.AddComponent<BanquetWandShow>().Configure(d, trick);
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
}
