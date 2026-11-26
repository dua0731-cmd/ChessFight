namespace ChessFight.PawnRush
{
    // R845's shows for the zones whose partial files are other sessions' (.Servery.cs R842, .Chandeliers.cs R843, .Hall.cs
    // bh-hall), kept apart here so those files are not touched: the waiters' walk (BanquetWaiterShow.cs), the great
    // chandelier's flames and crystals (BanquetGreatChandelierShow.cs), the hall's sconce flames and banners
    // (BanquetHallShow.cs). Pictures only; every collider and mover as built.
    public sealed partial class GrandBanquetHallDressing
    {
        static readonly System.Action<GrandBanquetHallDressing> WaiterShow = d =>
            d.gameObject.AddComponent<BanquetWaiterShow>().Configure(d);

        static readonly System.Action<GrandBanquetHallDressing> GreatChandelierShow = d =>
            d.gameObject.AddComponent<BanquetGreatChandelierShow>().Configure(d);

        static readonly System.Action<GrandBanquetHallDressing> HallShow = d =>
            d.gameObject.AddComponent<BanquetHallShow>().Configure(d);
    }
}
