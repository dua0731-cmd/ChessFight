using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Game
{
    // Keeps one PawnAvatar instance alive per networked pawn. Instantiating a
    // prefab is unavoidable here: who is in the match is only known at runtime.
    public sealed class PawnSpawner : MonoBehaviour
    {
        [SerializeField] PawnAvatar prefab;
        [Tooltip("Index 0 is BLUE, index 1 is ORANGE.")]
        [SerializeField] Material[] teamMaterials = new Material[2];

        readonly Dictionary<ulong, PawnAvatar> live = new Dictionary<ulong, PawnAvatar>();
        readonly List<ulong> departed = new List<ulong>();

        public IReadOnlyDictionary<ulong, PawnAvatar> Live => live;
        public bool TryGet(ulong id, out PawnAvatar avatar) => live.TryGetValue(id, out avatar);

        public void Configure(PawnAvatar avatarPrefab, Material blue, Material orange)
        {
            if (avatarPrefab != null) prefab = avatarPrefab;
            if (teamMaterials == null || teamMaterials.Length < 2) teamMaterials = new Material[2];
            if (blue != null) teamMaterials[0] = blue;
            if (orange != null) teamMaterials[1] = orange;
        }

        Material Tint(int team) => teamMaterials != null && (uint)team < teamMaterials.Length ? teamMaterials[team] : null;

        public void Sync(IReadOnlyDictionary<ulong, PawnState> states, float dt)
        {
            if (prefab == null) return;
            departed.Clear();
            foreach (var pair in live) if (!states.ContainsKey(pair.Key)) departed.Add(pair.Key);
            foreach (ulong id in departed) { Destroy(live[id].gameObject); live.Remove(id); }

            foreach (var pair in states)
            {
                var state = pair.Value;
                var target = new Vector3(state.X, state.Y, state.Z);
                if (!live.TryGetValue(pair.Key, out var avatar))
                {
                    avatar = Instantiate(prefab, target, Quaternion.identity, transform);
                    avatar.name = (BotIdentity.IsBot(pair.Key) ? "Bot " : "Pawn ") + pair.Key;
                    avatar.Teleport(target);
                    live.Add(pair.Key, avatar);
                }
                // A waiting room can re-slot a pawn onto the other team when a
                // group leaves, so re-tint instead of trusting the spawn colour.
                if (avatar.Team != state.Team)
                    avatar.Bind(pair.Key, state.Team, Tint(state.Team), Tint(1 - state.Team));
                avatar.Follow(target, dt);
            }
        }

        public void Clear()
        {
            foreach (var pair in live) if (pair.Value != null) Destroy(pair.Value.gameObject);
            live.Clear();
        }
    }
}
