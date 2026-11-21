using System;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // A character the course carries along a way and sets down on a spot (the throne contest's summons, JY 2026-11-22:
    // "소환되는 주자는 그냥 앞으로 빠르게 빨려들어가면서 해당 위치까지 가는 연출"): through everything on the way, then stood on
    // the last point facing `face` and held still there until let go. For the PC that simulates the character (offline,
    // the host); online the others draw where it is. The character's own assembly says how (SummonCarry.For: the ragdoll's
    // RagdollSummonCarry); a character without one is simply teleported by the caller.
    public interface ISummonCarry
    {
        // `way`: ground points (where the feet go), the last the spot; `seconds` for the whole way.
        void Carry(Vector3[] way, float seconds, Vector3 face);
        // On its way (not yet set down).
        bool Carrying { get; }
        // Set down and held on its spot (no key moves it) until Release.
        bool Holding { get; }
        void Release();
    }

    public static class SummonCarry
    {
        public static Func<ICharacterDriver, ISummonCarry> For;
        public static ISummonCarry Of(ICharacterDriver driver) => driver != null && For != null ? For(driver) : null;
    }
}
