using System;

namespace ChessFight.PawnRush
{
    // R725 R7: once the models hang, the dome's constellations twinkle and are drawn star to star
    // (RoyalObservatoryConstellations; the user 11-20 "천장에 이런 별자리들 효과좀 넣어").
    public sealed partial class RoyalObservatoryDressing
    {
        static readonly Action<RoyalObservatoryDressing> DomeConstellations = dressing =>
            RoyalObservatoryConstellations.AddTo(dressing.gameObject);
    }
}
