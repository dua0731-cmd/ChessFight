using System;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Hits from the royal water garden's own obstacles (the geysers, the robot arms, the lotus pads), for the section
    // playtest's HUD and CSV, like RollingSpawner.Hit and ChandelierRain.Hit.
    public static class GardenHits
    {
        public static event Action<GameObject, string> Hit;

        public static void Report(GameObject pawn, string what) => Hit?.Invoke(pawn, what);
    }
}
