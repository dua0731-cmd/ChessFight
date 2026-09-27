using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // Instance-owned registry: never collect bodies from another loaded mode/scene.
    public sealed class KingRushMatch : MonoBehaviour
    {
        public bool Authority { get; set; } = true;
        public KingRushRules Rules { get; private set; } = new KingRushRules();
        public double Now { get; private set; }
        readonly List<IKingRushCharacter> characters = new List<IKingRushCharacter>();
        public IReadOnlyList<IKingRushCharacter> Characters => characters;
        public void Register(IKingRushCharacter character)
        { if (!characters.Contains(character)) characters.Add(character); }
        public void Unregister(IKingRushCharacter character) => characters.Remove(character);
        public void Advance(float dt) { if (Authority && dt > 0 && !float.IsInfinity(dt)) Now += dt; }
        public bool Complete(int blue, int team) => Authority && Rules.Complete(blue, team, Now);
        public void ResetRound() { Rules = new KingRushRules(); Now = 0; }
    }

    public static class BodyCount
    {
        public static int InBounds(IReadOnlyList<IKingRushCharacter> characters, Bounds bounds, int team = -1)
        {
            int count = 0;
            foreach (var c in characters)
                if (c.CountsAsBody && (team < 0 || c.Team == team) && bounds.Contains(c.BodyPosition)) count++;
            return count;
        }
    }
}
