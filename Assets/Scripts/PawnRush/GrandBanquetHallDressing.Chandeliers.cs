using System;
using ChessFight.Game;
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
                        d.RideLights(root, look, GrandBanquetHallBuilder.RopeL[k]);
                    }
            }
            if (hung > 0) Debug.Log("[대연회장 입히기] 매달린 샹들리에 " + hung + "개");
            d.SplitLods();      // (the after-dress actions run in no set order: these were hung after the hall's pass)
        };

        // A hanging chandelier's lamp and candles, lit while a piece rides it (ChandelierRideLights; the user 11-25:
        // "기물이 샹들리에를 타면 녹색등 밑에 켜주고 위에 촛불은 실제 촛불 색으로 켜줘"). The green is the ring of crystal
        // drops under the main ring (not a lamp in the middle: "원으로 둘러 싸인 부분에서 나와야 해"), in the stage
        // lamps' lit glass (LampGlobes). The flames' colours are in their mesh; the material only brightens them.
        Material rideFlame, rideLampLit;
        bool rideMaterialsMade;

        void RideLights(Transform root, Transform look, float ropeLength)
        {
            if (!root.TryGetComponent<ChessFight.Gameplay.RopeLine>(out var rope)) return;
            if (!rideMaterialsMade)
            {
                rideMaterialsMade = true;
                var shader = Shader.Find(LitMaterials.ParticlesUnlitName);
                if (shader != null)
                {
                    rideFlame = new Material(shader) { name = "BH candle flame", mainTexture = Texture2D.whiteTexture, color = new Color(1.9f, 1.75f, 1.5f, 1f) };
                    LitMaterials.MakeTransparent(rideFlame);
                    ownedMaterials.Add(rideFlame);
                }
                var crystal = Resources.Load<Material>(MaterialFolder + "BH_Crystal");
                if (crystal != null)
                {
                    rideLampLit = new Material(crystal) { name = "BH chandelier drop (lit)" };
                    rideLampLit.color = new Color(.62f, 1f, .72f, .8f);
                    rideLampLit.EnableKeyword("_EMISSION");
                    rideLampLit.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    if (rideLampLit.HasProperty("_EmissionColor")) rideLampLit.SetColor("_EmissionColor", new Color(.49f, 1f, .6f) * 3f);
                    ownedMaterials.Add(rideLampLit);
                }
            }
            look.gameObject.AddComponent<ChandelierRideLights>().Build(rope, ropeLength, rideFlame, rideLampLit);
        }
    }
}
