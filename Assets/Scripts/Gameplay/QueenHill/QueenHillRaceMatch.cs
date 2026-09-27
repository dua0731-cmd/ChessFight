using System;
using System.Collections.Generic;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // One Queen of the Hill round's climb (8th revision, R50) around the pure
    // QueenHillRace (Core): each team's shortcuts, each player's highest rank, and
    // where a player comes back after a fall. The map's TeamBells, TeamPaths,
    // RankPads and VacuumTubes reach it through Current.
    //
    // Only the machine that simulates the characters judges (Authority); a network
    // client applies what the host sends (Encode / ApplyRemote).
    public sealed class QueenHillRaceMatch : MonoBehaviour
    {
        QueenHillRace race;
        readonly Transform[] respawns = new Transform[QueenHillRace.Teams];
        readonly Transform[] bridges = new Transform[QueenHillRace.Teams];

        public static QueenHillRaceMatch Current { get; private set; }

        public bool Authority { get; set; } = true;

        public QueenHillRace Race => race ?? (race = new QueenHillRace(QueenHillCourse.Floors));

        public static double Now => ObstacleClock.Now;

        // A team's shortcut opened: floor, team.
        public event Action<int, int> Opened;
        // A player got up to a new rank: who, rank.
        public event Action<ICharacterDriver, int> Reached;

        void OnEnable() => Current = this;

        void OnDisable()
        {
            if (Current == this) Current = null;
        }

        public static ulong Id(ICharacterDriver who) =>
            who is UnityEngine.Object o ? unchecked((ulong)(uint)o.GetInstanceID()) : 0;

        public static int TeamOf(ICharacterDriver who) => who is ITeamMember member ? member.Team : Teams.None;

        // Where a team comes back after a fall (rank 1, by its tube) and where it
        // started (its bridge), set by the level.
        public void SetRespawn(int team, Transform rankOne) { if (Valid(team)) respawns[team] = rankOne; }
        public void SetBridge(int team, Transform bridge) { if (Valid(team)) bridges[team] = bridge; }

        static bool Valid(int team) => team == Teams.White || team == Teams.Black;

        public bool Ring(int floor, int team, ICharacterDriver who)
        {
            if (!Authority || !Race.TryOpen(floor, team, Id(who), Now)) return false;
            Opened?.Invoke(floor, team);
            return true;
        }

        public void Reach(ICharacterDriver who, int rank)
        {
            if (Authority && who != null && Race.Reach(Id(who), rank)) Reached?.Invoke(who, rank);
        }

        public bool IsOpen(int floor, int team) => Race.IsOpen(floor, team);
        public int ReachedRank(ICharacterDriver who) => Race.ReachedRank(Id(who));
        public int TubeTarget(ICharacterDriver who) => Race.TubeTarget(Id(who));

        // After a fall: rank 1 beside the team's tube, or the team's bridge if it never
        // made it across. False when the level has neither (the caller keeps its own).
        public bool TryRespawn(ICharacterDriver who, int team, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (!Valid(team)) team = Teams.White;
            var at = Race.Landed(Id(who)) ? respawns[team] : bridges[team];
            if (at == null) at = respawns[team];
            if (at == null) return false;
            position = FreeSpot(at.position, at.rotation);
            rotation = at.rotation;
            return true;
        }

        static readonly Vector3[] Around =
        {
            Vector3.zero, new Vector3(1f, 0f, 0f), new Vector3(-1f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f),
            new Vector3(2f, 0f, 0f), new Vector3(-2f, 0f, 0f), new Vector3(1f, 0f, 1f), new Vector3(-1f, 0f, 1f),
        };

        // Characters put back in the same frame on the same spot knock each other over.
        public static Vector3 FreeSpot(Vector3 origin, Quaternion frame)
        {
            foreach (var offset in Around)
            {
                Vector3 at = origin + frame * offset;
                if (!Physics.CheckCapsule(at + Vector3.up * 0.45f, at + Vector3.up * 1f, 0.35f, ~0, QueryTriggerInteraction.Ignore))
                    return at;
            }
            return origin;
        }

        public string Encode() => Race.Encode();

        public void ApplyRemote(string encoded)
        {
            var before = new bool[Race.Floors + 1, QueenHillRace.Teams];
            for (int f = 1; f <= Race.Floors; f++)
                for (int t = 0; t < QueenHillRace.Teams; t++) before[f, t] = Race.IsOpen(f, t);
            if (!Race.Apply(encoded)) return;
            for (int f = 1; f <= Race.Floors; f++)
                for (int t = 0; t < QueenHillRace.Teams; t++)
                    if (!before[f, t] && Race.IsOpen(f, t)) Opened?.Invoke(f, t);
        }

        public void ResetRound() => Race.Reset();
    }
}
