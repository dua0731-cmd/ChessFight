namespace ChessFight.PawnRush
{
    // B4 the dance floor and the waltz (seoungtest R837; ArtSource/PawnRush/BanquetHall/bh_b4_waltz_model.py): the chessboard
    // floor (BH_T1's chessboard swatch) over the builder's dance floor, and on each of the ten couples' roots the user's
    // figurines (BH_A10 queen and partner) at their own proportions on music-box pedestals - the queen's shoulder plate flat on the gown's top (7.4,
    // r 1.3), the partner's arm out to her plate. The roots turn and travel (WaltzCouple); colliders stay the builder's.
    public sealed partial class GrandBanquetHallDressing
    {
        static readonly Hang[] WaltzHangs = CoupleHangs();

        // The waltz's show (R845, BanquetWaltzShow.cs): the gowns flare and trail, the partners rise and fall with the
        // beat; the queens' shoulders (the floor a pawn stands on) and every collider stay as built.
        static readonly System.Action<GrandBanquetHallDressing> WaltzShow = d =>
            d.gameObject.AddComponent<BanquetWaltzShow>().Configure(d);

        static Hang[] CoupleHangs()
        {
            var list = new Hang[BanquetWaltz.Couples + 1];
            list[0] = new Hang { zone = "B4", model = "bh_dance_floor", covers = new[] { "BH dance floor" } };
            for (int i = 0; i < BanquetWaltz.Couples; i++)
            {
                string root = "BH couple " + (i + 1);
                list[i + 1] = new Hang
                {
                    zone = "B4", model = "bh_couple", on = root, under = root,
                    covers = new[] { "BH queen gown", "BH queen head", "BH queen crown", "BH king", "BH king head", "BH king crown", "BH joined arms" },
                };
            }
            return list;
        }
    }
}
