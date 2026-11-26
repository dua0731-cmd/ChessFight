using UnityEngine;

namespace ChessFight.PawnRush
{
    // B5 the stage, the curtain, the dessert buffet and the chocolate fountain (bh-hall session, seoungtest R838;
    // ArtSource/PawnRush/BanquetHall/bh_b5_stage_model.py, the shapes after the R836 sheets BH_A07 and BH_A08, put
    // together by bh_b5_assembly.py, written by bh_hall_export.py): the stage's walnut front and its top, the curtain's
    // twenty pleats (each riding its own moving root, CurtainPleat), the brass rail and the velvet pelmet over them,
    // the two double basses, the buffet's marble top and skirts with two cake stands, the fountain's gold basins with
    // the chocolate pouring down them. Colliders, floors, the pleats' and the chocolate's motion and the ripples
    // (ChocolateRipples) stay the builder's.
    public sealed partial class GrandBanquetHallDressing
    {
        static readonly Hang[] StageHangs = StageList();

        // The four lamp globes (the dais's boarding lamps and the stage's alighting lamps - user 11-25: "등 유리구만
        // 바꿔"): still the builder's balls, lit and put out by their ClockLamp at the same moments, but drawn as glass -
        // out, a clear, faintly warm globe; lit, the builder's go-green glowing through the glass. Made from the
        // library's BH_Crystal (transparent); ClockLamp's two materials swapped (its fields, by reflection: it has no
        // setter of its own).
        static readonly System.Action<GrandBanquetHallDressing> LampGlobes = d =>
        {
            var crystal = Resources.Load<Material>(MaterialFolder + "BH_Crystal");
            var litField = typeof(ClockLamp).GetField("lit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var unlitField = typeof(ClockLamp).GetField("unlit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (crystal == null || litField == null || unlitField == null) return;
            var off = new Material(crystal) { name = "BH lamp globe (out)" };
            off.color = new Color(.96f, .92f, .82f, .42f);
            var on = new Material(crystal) { name = "BH lamp globe (lit)" };
            on.color = new Color(.62f, 1f, .72f, .75f);
            on.EnableKeyword("_EMISSION");
            on.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (on.HasProperty("_EmissionColor")) on.SetColor("_EmissionColor", new Color(.49f, 1f, .6f) * 2.4f);
            d.ownedMaterials.Add(off); d.ownedMaterials.Add(on);
            int n = 0;
            foreach (var name in new[] { "BH boarding lamp globe", "BH alighting lamp globe" })
                foreach (var t in d.FindAll(name))
                {
                    if (!t.TryGetComponent<ClockLamp>(out var lamp)) continue;
                    litField.SetValue(lamp, on);
                    unlitField.SetValue(lamp, off);
                    n++;
                }
            if (n > 0) Debug.Log("[대연회장 입히기] 등 유리구 " + n + "개");
        };

        // The stage's show (R845, BanquetStageShow.cs): the pleats' pictures gather and swing, the pelmet's tassels sway,
        // light leaks from the seam and the stage lights follow the curtain; colliders and timing stay the builder's.
        // R845 too: the chocolate fountain's show (BanquetFountainShow.cs: streams from the crown, drops, rings across the
        // pools, the chocolate's surface sliding) - pictures only, the tiers' pull and colliders as built.
        static readonly System.Action<GrandBanquetHallDressing> StageShow = d =>
        {
            d.gameObject.AddComponent<BanquetStageShow>().Configure(d);
            d.gameObject.AddComponent<BanquetFountainShow>().Configure(d);
        };

        static Hang[] StageList()
        {
            var list = new System.Collections.Generic.List<Hang>
            {
                new Hang { zone = "Stage", model = "bh_stage", covers = new[] { "BH stage" } },
                new Hang { zone = "Stage", model = "bh_curtain_rail", covers = new[] { "BH curtain rail", "BH pelmet" } },
                new Hang { zone = "Stage", model = "bh_double_basses", covers = new[] { "BH double bass", "BH bass neck" } },
                new Hang { zone = "Stage", model = "bh_buffet", covers = new[] { "BH dessert buffet", "BH strawberry" } },
                // the orchestra and the two bassists (user 11-25: the grey cylinders left): bh-path's figurines
                new Hang { zone = "Stage", model = "bh_musicians", covers = new[] { "BH musician", "BH musician head", "BH bassist" } },
                new Hang
                {
                    zone = "Stage", model = "bh_fountain",
                    covers = new[] { "BH fountain bowl", "BH chocolate tier 1", "BH chocolate tier 2", "BH chocolate tier 3",
                                     "BH fountain crown", "BH fountain finial" },
                },
            };
            // the alighting lamps' posts at the stage's front (user 11-25: "여기 광등도 모델링 해"): walnut and gold on
            // each lamp's root; the globe stays the builder's (it lights as a queen comes to stand, ClockLamp)
            foreach (string side in new[] { "S", "N" })
                list.Add(new Hang { zone = "Stage", model = "bh_alighting_post", on = "BH alighting lamp " + side,
                                    under = "BH alighting lamp " + side, covers = new[] { "BH alighting post" } });
            foreach (string side in new[] { "S", "N" })
                for (int k = 1; k <= GrandBanquetHallBuilder.Pleats; k++)
                {
                    string root = "BH pleat " + side + k;
                    list.Add(new Hang { zone = "Stage", model = "bh_curtain_pleat", on = root, under = root, covers = new[] { "BH curtain" } });
                }
            return list.ToArray();
        }
    }
}
