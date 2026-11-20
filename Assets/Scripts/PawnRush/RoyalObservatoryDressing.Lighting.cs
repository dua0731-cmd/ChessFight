using System;

namespace ChessFight.PawnRush
{
    // R725 R7: once the models hang, the observatory's own light and screen finish (RoyalObservatoryLighting - moon
    // through the slit, warm fill, navy ambient and haze, grade and bloom, reflection probes; camera-in-section only).
    public sealed partial class RoyalObservatoryDressing
    {
        static readonly Action<RoyalObservatoryDressing> ObservatoryLighting = dressing =>
            RoyalObservatoryLighting.AddTo(dressing.gameObject);
    }
}
