using System;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.Gameplay
{
    // One Queen of the Hill round's pioneer bells, opened paths and checkpoints
    // (DESIGN §3.3 B and C), around the pure QueenHillRules (Core). The scene's
    // Bells, OpenPaths and SectionCheckpoints reach it through Current.
    //
    // Only the machine that simulates the characters judges (Authority): it rings
    // the bells and records who reached which landing. A network client applies
    // what the host sends (Encode / ApplyRemote). Times are the shared obstacle
    // clock's, so a path opens and its head start ends at the same moment on
    // every PC.
    public sealed class QueenHillMatch : MonoBehaviour
    {
        [Tooltip("Sections S1..Sn, each with its bell (Bell.section) and landing (SectionCheckpoint.section).")]
        [SerializeField] int sections = 8;
        [Tooltip("Seconds an opened path belongs to the team that opened it.")]
        [SerializeField] float exclusiveSeconds = (float)QueenHillRules.DefaultExclusiveSeconds;

        QueenHillRules rules;

        public static QueenHillMatch Current { get; private set; }

        // False on a network client: it shows what the host decided.
        public bool Authority { get; set; } = true;

        public QueenHillRules Rules => rules ?? (rules = new QueenHillRules(Mathf.Max(1, sections), exclusiveSeconds));

        public static double Now => ObstacleClock.Now;

        // A section opened (by a bell here, or by the host's word on a client).
        public event Action<int> Opened;

        public int Rings { get; private set; }

        // For matches built from code (test beds): before the first use.
        public void Configure(int sections, float exclusiveSeconds)
        {
            this.sections = Mathf.Max(1, sections);
            this.exclusiveSeconds = Mathf.Max(0f, exclusiveSeconds);
            rules = null;
        }

        void OnEnable() => Current = this;

        void OnDisable()
        {
            if (Current == this) Current = null;
        }

        // A bell rung by a character of `team`. True when it opened the section.
        public bool Ring(int section, ICharacterDriver who, int team)
        {
            if (!Authority || !Rules.TryOpen(section, team, Now)) return false;
            Rings++;
            Opened?.Invoke(section);
            return true;
        }

        // A character stood on a section's landing.
        public void Reach(ICharacterDriver who, int section)
        {
            if (Authority && who != null) Rules.Reach(Id(who), section);
        }

        public static ulong Id(ICharacterDriver who) =>
            who is UnityEngine.Object o ? unchecked((ulong)(uint)o.GetInstanceID()) : 0;

        public bool IsOpen(int section) => Rules.IsOpen(section);
        public int Pioneer(int section) => Rules.Pioneer(section);
        public bool Exclusive(int section) => Rules.Exclusive(section, Now);
        public float ExclusiveLeft(int section) =>
            Exclusive(section) ? (float)(Rules.OpenedAt(section) + Rules.ExclusiveSeconds - Now) : 0f;
        public bool CanUse(int section, int team) => Rules.CanUse(section, team, Now);
        public int PersonalBest(ICharacterDriver who) => Rules.PersonalBest(Id(who));
        public int TeamBest(int team) => Rules.TeamBest(team);

        // Where a character comes back after the water: the landing of the higher of
        // its own checkpoint and one below its team's best. False: the mode's own
        // start (its team's bridge).
        public bool TryRespawn(ICharacterDriver who, int team, out Vector3 position, out Quaternion rotation)
        {
            int section = Rules.RespawnSection(Id(who), team);
            foreach (var landing in SectionCheckpoint.All)
            {
                if (landing == null || landing.Section != section || section <= 0) continue;
                position = landing.FreeSpawnPosition();
                rotation = landing.SpawnRotation;
                return true;
            }
            position = default;
            rotation = Quaternion.identity;
            return false;
        }

        public string Encode() => Rules.Encode();

        // The host's word on a client.
        public void ApplyRemote(string encoded)
        {
            var before = new bool[Rules.Sections + 1];
            for (int s = 1; s <= Rules.Sections; s++) before[s] = Rules.IsOpen(s);
            if (!Rules.Apply(encoded)) return;
            for (int s = 1; s <= Rules.Sections; s++)
                if (!before[s] && Rules.IsOpen(s)) Opened?.Invoke(s);
        }

        // A new round: every section closed, every checkpoint forgotten.
        public void ResetRound() => Rules.Reset();
    }
}
