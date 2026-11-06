using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // Sample body centers once per character outside collision callbacks.
    public sealed class KingRushPad : MonoBehaviour
    {
        public KingRushMatch match;
        public int index;
        public Vector3 size = new Vector3(2, 1.8f, 2);
        public readonly KingRushPadCharge Charge = new KingRushPadCharge();
        readonly List<ulong> eligible = new List<ulong>(12);
        public bool Claimed => match.Rules.TryGetClaim(index, out _);
        public KingRushPiece Reward => KingRushRules.PadPiece(index);
        public void Step(float dt)
        {
            if (!match.Authority || Claimed) return;
            eligible.Clear();
            var bounds = new Bounds(transform.position + Vector3.up * size.y * .5f, size);
            foreach (var c in match.Characters)
                if (c.Piece == KingRushPiece.Pawn && c.StandingOnPad && bounds.Contains(c.BodyPosition)) eligible.Add(c.Id);
            ulong id = Charge.Step(eligible, dt);
            if (id == 0) return;
            foreach (var c in match.Characters)
                if (c.Id == id && KingRushPieces.CanPromote(c.Piece, Reward) && match.Rules.TryClaim(index, id, c.Team))
                { c.Promote(Reward); break; }
        }
    }
}
