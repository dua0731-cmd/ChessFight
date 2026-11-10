using System;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // A part of a section that changes with the round: random obstacles take the section's seed and start
    // time, team-size switches take the team size. Called when the section is configured (and again when
    // the playtest changes the round).
    public interface ISectionPart
    {
        void OnSection(PawnRushSection section);
    }

    // A beat of a section (the level design's 비트): a named box in the section's frame, for the HUD and the
    // playtest's CSV. Several boxes may share a name (the two wings of one beat).
    [Serializable]
    public struct SectionBeat
    {
        public string name;
        public Vector3 min, max;

        public SectionBeat(string name, Vector3 min, Vector3 max)
        {
            this.name = name;
            this.min = min;
            this.max = max;
        }

        public bool Contains(Vector3 local) =>
            local.x >= min.x && local.x <= max.x && local.y >= min.y && local.y <= max.y && local.z >= min.z && local.z <= max.z;
    }

    // The root of a section prefab (2026-11-02 user: a round is seven sections, each one prefab drawn from
    // that slot's list and joined to the next). A section knows:
    //   entry / exit  where it joins the sections before and after: the middle of the threshold, on the floor,
    //                 the way on = +z. The round puts the next section's entry on this one's exit.
    //   the round     which slot it is (checkpoint orders count on from earlier sections: slot * 100 + number),
    //                 its seed (PawnRushSections.SectionSeed) and the shared clock time it starts at, and the
    //                 team size. ISectionPart children take them.
    //   beats         the level design's beats, for the HUD and the playtest's CSV.
    // Dropped into a scene on its own it plays as slot 0 with its default seed and six a side.
    [DisallowMultipleComponent]
    public sealed class PawnRushSection : MonoBehaviour
    {
        public enum SlotKind { Obstacle, MiniGame, Final }

        public const int CheckpointsPerSlot = 100;

        [SerializeField] string id = "Section";
        [SerializeField] string displayName = "구간";
        [SerializeField] SlotKind kind = SlotKind.Obstacle;
        [Tooltip("Where the section before joins: the threshold's middle, on the floor, facing the way on (+z).")]
        [SerializeField] Vector3 entry;
        [Tooltip("Where the next section joins.")]
        [SerializeField] Vector3 exit = new Vector3(0f, 0f, 100f);
        [SerializeField] float exitYaw;
        [SerializeField] SectionBeat[] beats = new SectionBeat[0];

        [Header("This round (the round sets these; on its own: slot 0)")]
        [SerializeField] int slot;
        [SerializeField] int seed = 1;
        [SerializeField, Range(1, 6)] int teamSize = 6;

        double startClock = double.NaN;
        bool started;

        public string Id => id;
        public string DisplayName => displayName;
        public SlotKind Kind => kind;
        public int Slot => slot;
        public int Seed => seed;
        public int TeamSize => teamSize;
        // The shared obstacle clock time the round's events count from (the round's start; on its own, when
        // the section starts).
        public double StartClock => startClock;
        public SectionBeat[] Beats => beats;

        public Pose EntryLocal => new Pose(entry, Quaternion.identity);
        public Pose ExitLocal => new Pose(exit, Quaternion.Euler(0f, exitYaw, 0f));
        public Pose EntryWorld => new Pose(transform.TransformPoint(entry), transform.rotation);
        public Pose ExitWorld => new Pose(transform.TransformPoint(exit), transform.rotation * Quaternion.Euler(0f, exitYaw, 0f));

        // The builder's description of the section.
        public void Describe(string id, string displayName, SlotKind kind, Vector3 entry, Vector3 exit, float exitYaw, SectionBeat[] beats)
        {
            this.id = id;
            this.displayName = displayName;
            this.kind = kind;
            this.entry = entry;
            this.exit = exit;
            this.exitYaw = exitYaw;
            this.beats = beats ?? new SectionBeat[0];
        }

        // The round's values. Applied at once when the section is running, else when it starts.
        public void Configure(int slot, int seed, int teamSize, double startClock)
        {
            this.slot = slot;
            this.seed = seed;
            this.teamSize = Mathf.Clamp(teamSize, 1, 6);
            this.startClock = startClock;
            if (started) Apply();
        }

        // Puts the section's root so that its entry sits on `at` (the previous section's exit).
        public void PlaceEntryOn(Pose at)
        {
            var rotation = at.rotation;
            transform.SetPositionAndRotation(at.position - rotation * entry, rotation);
        }

        void Start()
        {
            started = true;
            Apply();
            // R529: the section's sounds are heard only inside it (SectionSoundGate).
            SectionSoundGate.GateAll(transform);
        }

        void OnEnable() => SectionSoundGate.Register(this);
        void OnDisable() => SectionSoundGate.Unregister(this);

        void Apply()
        {
            if (double.IsNaN(startClock)) startClock = ObstacleClock.Now;
            // Checkpoints count on from the sections before: the playtest keeps the highest reached.
            foreach (var cp in GetComponentsInChildren<CourseCheckpoint>(true))
            {
                var checkpoint = cp.GetComponent<Checkpoint>();
                if (checkpoint != null) checkpoint.Configure(slot * CheckpointsPerSlot + cp.Number);
            }
            foreach (var part in GetComponentsInChildren<ISectionPart>(true)) part.OnSection(this);
        }

        // The beat a world point is in, or null.
        public string BeatAt(Vector3 world)
        {
            var local = transform.InverseTransformPoint(world);
            foreach (var beat in beats)
                if (beat.Contains(local)) return beat.name;
            return null;
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(.2f, .9f, .4f, .9f);
            Gizmos.DrawWireCube(entry + Vector3.up * 1.5f, new Vector3(12f, 3f, .2f));
            Gizmos.color = new Color(.95f, .5f, .15f, .9f);
            Gizmos.matrix = transform.localToWorldMatrix * Matrix4x4.TRS(exit, Quaternion.Euler(0f, exitYaw, 0f), Vector3.one);
            Gizmos.DrawWireCube(Vector3.up * 1.5f, new Vector3(12f, 3f, .2f));
        }
    }
}
