namespace ChessFight.PawnRush
{
    // B3 the royal dais and the king's balcony (seoungtest R837; ArtSource/PawnRush/BanquetHall/bh_b3_dais_model.py, shapes
    // after the R836 sheet BH_A06): the dais's marble front and top, its balustrades and the gallery's end walls (the
    // boarding lamps' globes stay the builder's - they light - on the models' gold cups), the head tables under draped
    // cloths with their candelabra, the balcony with its canopy, the two thrones. Colliders and floors stay the builder's.
    public sealed partial class GrandBanquetHallDressing
    {
        static readonly Hang[] DaisHangs =
        {
            new Hang { zone = "B3", model = "bh_dais", covers = new[] { "BH royal dais", "BH dais edge" } },
            new Hang { zone = "B3", model = "bh_dais_rails",
                       covers = new[] { "BH balustrade", "BH gallery end W", "BH gallery end E", "BH gallery end NE", "BH boarding post" } },
            new Hang { zone = "B3", model = "bh_head_tables", covers = new[] { "BH head table W", "BH head table E", "BH head table candle", "BH head table flame" } },
            new Hang { zone = "B3", model = "bh_balcony",
                       covers = new[] { "BH balcony", "BH balcony edge", "BH balcony rail W", "BH balcony rail E", "BH canopy" } },
            new Hang { zone = "B3", model = "bh_thrones", covers = new[] { "BH throne" } },
        };

        // R845: the dais's show (BanquetDaisShow.cs): the head tables' candelabra burn, the balcony showers confetti on
        // whoever reaches it (seen on each PC, nothing sent). Pictures only.
        static readonly System.Action<GrandBanquetHallDressing> DaisShow = d =>
            d.gameObject.AddComponent<BanquetDaisShow>().Configure(d);
    }
}
