using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.ProtectKing
{
    // seoungtest R689: the Royal Library course (WarRoomComplete: the library's books, typewriter, stamps and door, then
    // the giant chess room) as a Pawn Rush round section. The section itself (PawnRush RoyalLibrarySection) cannot be
    // seen from RagdollLab, where the giant chess controller lives, so this small component on the section's root carries
    // what both sides need, the CourseSkills way:
    //   the round        the section's seed and the shared clock time the round started (PawnRushSection.Configure)
    //   the chess start  NaN until the host sees the first pawn reach the chess room; then the clock time every PC counts
    //                    the replay from (GiantChessReplay; the section sends it, rounded to the wire's tenths)
    //   this PC's pawn   for what only this player sees (the portal's veil)
    // With no RoyalLibraryMatch above it the course plays as before: GiantChessCourse's own scene, its own pawns.
    [DisallowMultipleComponent]
    public sealed class RoyalLibraryMatch : MonoBehaviour
    {
        public bool Configured { get; private set; }
        public int Seed { get; private set; } = 1;
        public double Origin { get; private set; } = double.NaN;
        public double ChessStart { get; private set; } = double.NaN;
        public bool ChessStarted => !double.IsNaN(ChessStart);
        // This PC's pawn's follow target (its hips), when it has one.
        public Func<Transform> LocalCharacter;
        public Transform LocalFollow => LocalCharacter != null ? LocalCharacter() : null;
        public event Action Changed;

        public void Configure(int seed, double origin)
        {
            bool changed = !Configured || seed != Seed || !origin.Equals(Origin);
            Configured = true;
            Seed = seed;
            Origin = origin;
            if (changed) Changed?.Invoke();
        }

        // The host: the chess starts at `clock` (rounded as it goes on the wire).
        public void StartChess(double clock)
        {
            if (ChessStarted) return;
            ChessStart = GiantChessReplay.Round(clock, Origin);
        }

        // Another PC: the host's word (NaN = not yet).
        public void SetChessStart(double clock) => ChessStart = clock;
    }
}
