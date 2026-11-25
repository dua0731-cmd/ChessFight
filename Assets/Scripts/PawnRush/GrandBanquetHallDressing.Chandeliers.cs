using System;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // B6 the lamplighters' loft and the chandeliers (bh-hall session, seoungtest R838; ArtSource/PawnRush/BanquetHall/
    // bh_b6_loft_model.py and bh_b6_chandelier_model.py, the shapes after the R836 sheets BH_A02 and BH_A09, put together
    // by bh_b6_assembly.py, written by bh_hall_export.py): the loft's walnut face, its marble balustrade and gilt ladder;
    // the gilt rods from the ceiling down to each rope's pivot; the great chandelier (its dish's top the builder's floor,
    // GreatY); and on each of the ten ropes a hanging chandelier, riding the rope's "BH chandelier look" (ChandelierLook
    // turns it with the rope) - bh_chandelier_65 or _80 for the rope's length (RopeL), its gold hoop where the rope ends.
    // Colliders, floors and the ropes' swing stay the builder's.
    public sealed partial class GrandBanquetHallDressing
    {
        static readonly Hang[] ChandelierHangs =
        {
            new Hang { zone = "Chandeliers", model = "bh_loft",
                       covers = new[] { "BH loft", "BH loft end", "BH loft face", "BH loft rail", "BH loft ladder" } },
            new Hang { zone = "Chandeliers", model = "bh_chandelier_rods", covers = new[] { "BH chandelier rod", "BH chandelier swivel" } },
            new Hang { zone = "Chandeliers", model = "bh_great_chandelier",
                       covers = new[] { "BH great chandelier body", "BH great chandelier carpet", "BH great chandelier column",
                                        "BH great chandelier floor" } },
        };

        // The hanging ones: a Hang can't pick the model by the rope (the looks all share one name), so each rope's root
        // ("BH chandelier W1" .. "E5", k = its row) gets the model for RopeL[k] on its look.
        static readonly Action<GrandBanquetHallDressing> HangingChandeliers = d =>
        {
            int hung = 0;
            for (int k = 0; k < GrandBanquetHallBuilder.RopeL.Length; k++)
            {
                string model = GrandBanquetHallBuilder.RopeL[k] > 7f ? "bh_chandelier_80" : "bh_chandelier_65";
                var fbx = Resources.Load<GameObject>(ModelFolder + "Chandeliers/" + model);
                if (fbx == null) continue;
                foreach (string lane in new[] { "W", "E" })
                    foreach (var root in d.FindAll("BH chandelier " + lane + (k + 1)))
                    {
                        var look = root.Find("BH chandelier look");
                        if (look == null) continue;
                        if (d.HangOn(look, fbx) != null) hung++;
                        Cover(look, "BH chandelier", false);
                    }
            }
            if (hung > 0) Debug.Log("[대연회장 입히기] 매달린 샹들리에 " + hung + "개");
            d.SplitLods();      // (the after-dress actions run in no set order: these were hung after the hall's pass)
        };
    }
}
