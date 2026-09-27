using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // Opening changes only collision pairs owned by this gate, never global layers.
    public sealed class PawnGate : MonoBehaviour
    {
        public KingRushMatch match;
        public int team, blue;
        public Collider barrier;
        public Bounds exitBounds;
        readonly HashSet<Collider> ignored = new HashSet<Collider>();
        public bool Open => match.Rules.IsOpen(blue, team, match.Now);
        public void Step()
        {
            foreach (var c in match.Characters)
            {
                bool pass = Open && c.Team == team;
                foreach (var body in c.BodyColliders)
                {
                    if (body == null) continue;
                    if (pass && ignored.Add(body)) Physics.IgnoreCollision(barrier, body, true);
                    else if (!pass && ignored.Remove(body)) Physics.IgnoreCollision(barrier, body, false);
                }
                if (!match.Authority || !pass || !exitBounds.Contains(c.BodyPosition)) continue;
                var from = blue == 0 ? KingRushSection.Blue1 : KingRushSection.Blue2;
                if (c.Section != from) continue;
                c.LeaveBlue(); c.Section = blue == 0 ? KingRushSection.Red2 : KingRushSection.Red3;
            }
        }
        void OnDisable()
        {
            if (barrier != null) foreach (var body in ignored) if (body != null) Physics.IgnoreCollision(barrier, body, false);
            ignored.Clear();
        }
    }
}
