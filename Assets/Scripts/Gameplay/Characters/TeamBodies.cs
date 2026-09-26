using System.Collections.Generic;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // Which colliders belong to which team, for things that treat the teams
    // differently without knowing what a character is made of: an opened path
    // that for its first seconds holds up only the team that opened it (OpenPath).
    // A character registers its colliders when its team is set and leaves when it
    // is destroyed.
    public static class TeamBodies
    {
        public readonly struct Entry
        {
            public readonly int Team;
            public readonly Collider[] Colliders;

            public Entry(int team, Collider[] colliders)
            {
                Team = team;
                Colliders = colliders;
            }
        }

        static readonly Dictionary<Object, Entry> bodies = new Dictionary<Object, Entry>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => bodies.Clear();

        public static void Set(Object owner, int team, Collider[] colliders)
        {
            if (owner != null && colliders != null) bodies[owner] = new Entry(team, colliders);
        }

        public static void Remove(Object owner)
        {
            if (owner != null) bodies.Remove(owner);
        }

        public static Dictionary<Object, Entry>.ValueCollection All => bodies.Values;
    }
}
