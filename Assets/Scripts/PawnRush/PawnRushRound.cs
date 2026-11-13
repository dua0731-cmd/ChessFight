using System;
using System.Collections.Generic;
using System.Text;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // Puts a round together (2026-11-02 user: seven sections a round, each one prefab drawn at random from
    // its slot's list): draws a prefab per slot (PawnRushSections.Choose, seeded: the host can draw and send
    // only the seed), instantiates them one after another, each section's entry on the previous one's exit,
    // and gives every section its slot, its own seed (the round's mixed with the slot) and the team size.
    // A slot with no prefab yet is skipped (the next section joins the previous one).
    //
    // Offline the round is built when the scene starts, from the clock's seed; twelve start spots go at the
    // first section's entry. Online the match will pass the host's seed and the shared start time.
    [DefaultExecutionOrder(-500)]
    public sealed class PawnRushRound : MonoBehaviour
    {
        [SerializeField] PawnRushSectionSet set;
        [Tooltip("Players per team: which wings and lifts the sections open (3·4 a side: one wing).")]
        [SerializeField, Range(1, 6)] int teamSize = 6;
        [Tooltip("0: a new seed every round (offline: the clock). Otherwise always this one.")]
        [SerializeField] int seed;
        [SerializeField] bool buildOnAwake = true;
        [Tooltip("Six start spots a team at the first section's entry, for the offline playtest.")]
        [SerializeField] bool startSpots = true;

        // The previous round's picks (offline: this session), left out where a slot has another.
        static int[] lastPicks;

        readonly List<PawnRushSection> sections = new List<PawnRushSection>();
        readonly List<string> skipped = new List<string>();
        GameObject holder;

        public static PawnRushRound Current { get; private set; }
        public static event Action<PawnRushRound> Built;

        public IReadOnlyList<PawnRushSection> Sections => sections;
        public IReadOnlyList<string> Skipped => skipped;
        public PawnRushSectionSet Set => set;
        public int Seed { get; private set; }
        public int TeamSize => teamSize;
        public double StartClock { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            lastPicks = null;
            Current = null;
            Built = null;
        }

        void Awake()
        {
            Current = this;
            // R494 (승규 11-06 "open sect test에도 기물 물리엔진 수정 한 거 넣어주고"): a round is a Pawn Rush course, as
            // course 01 (PawnRushCourse) and 승규 님's test scene (SeoungTestCourse) are. While it is loaded the ragdoll
            // side (PawnRushCourseSkills) gives every pawn the Pawn Rush movement - PawnRushPhysicsParams: jump, gravity,
            // low lips, ledges, dive, getting up (seoungtest) - and the piece skills, F being the skill key.
            CourseSkills.PawnRush = true;
            if (buildOnAwake) Build(seed != 0 ? seed : Environment.TickCount);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            CourseSkills.PawnRush = false;
        }

        public void Build(int roundSeed)
        {
            Clear();
            Seed = roundSeed;
            StartClock = ObstacleClock.Now;
            if (set == null)
            {
                Debug.LogError("[PawnRush] PawnRushRound에 구간 목록(PawnRushSections)이 없습니다.");
                return;
            }
            var prefabs = new List<GameObject>();
            var picks = PawnRushSections.Choose(roundSeed, set.Options(prefabs), lastPicks);
            lastPicks = picks;

            holder = new GameObject("Round " + roundSeed);
            holder.SetActive(false);
            holder.transform.SetParent(transform, false);
            var at = new Pose(transform.position, transform.rotation);
            var log = new StringBuilder("[PawnRush] 이번 판 구간 (씨앗 " + roundSeed + ", " + teamSize + "대" + teamSize + "): ");
            for (int slot = 0; slot < picks.Length; slot++)
            {
                string label = slot < set.SlotCount && set[slot] != null ? set[slot].name : "슬롯 " + (slot + 1);
                if (picks[slot] < 0)
                {
                    skipped.Add(label);
                    log.Append(slot + 1).Append(' ').Append(label).Append(" = (프리팹 없음, 건너뜀) · ");
                    continue;
                }
                var prefab = prefabs[picks[slot]];
                var go = Instantiate(prefab, holder.transform);
                go.name = (slot + 1) + " " + prefab.name;
                var section = go.GetComponent<PawnRushSection>();
                if (section == null)
                {
                    Debug.LogError("[PawnRush] " + prefab.name + "에 PawnRushSection이 없습니다. 구간 프리팹의 뿌리에 붙여 주세요.");
                    Destroy(go);
                    continue;
                }
                section.PlaceEntryOn(at);
                section.Configure(slot, PawnRushSections.SectionSeed(roundSeed, slot), teamSize, StartClock);
                at = section.ExitWorld;
                sections.Add(section);
                log.Append(slot + 1).Append(' ').Append(label).Append(" = ").Append(section.DisplayName).Append(" · ");
            }
            if (startSpots && sections.Count > 0) StartSpots(holder.transform, sections[0]);
            holder.SetActive(true);
            Debug.Log(log.ToString());
            Built?.Invoke(this);
        }

        // Six spots a team in two rows across the first section's entry, facing the way on.
        void StartSpots(Transform parent, PawnRushSection first)
        {
            var entry = first.EntryWorld;
            for (int team = 0; team < 2; team++)
            {
                int index = 0;
                foreach (float row in new[] { 8f, 4f })
                    foreach (float x in new[] { 1.5f, 3f, 4.5f })
                    {
                        var spot = new GameObject("Start " + Teams.Name(team) + " " + index);
                        spot.transform.SetParent(parent, false);
                        spot.transform.SetPositionAndRotation(entry.position + entry.rotation * new Vector3(team == Teams.White ? -x : x, 0f, row),
                                                              entry.rotation);
                        spot.AddComponent<SpawnPoint>().Configure(index++, team);
                    }
            }
        }

        // The team size changed (the playtest's key): every section switches its wings and lifts.
        public void SetTeamSize(int size)
        {
            teamSize = Mathf.Clamp(size, 1, 6);
            foreach (var s in sections)
                if (s != null) s.Configure(s.Slot, s.Seed, teamSize, StartClock);
        }

        public void Clear()
        {
            sections.Clear();
            skipped.Clear();
            if (holder == null) return;
            if (Application.isPlaying) Destroy(holder);
            else DestroyImmediate(holder);
            holder = null;
        }

        // The section a world point is in (the nearest whose beats hold it), or null.
        public PawnRushSection SectionAt(Vector3 world, out string beat)
        {
            foreach (var s in sections)
            {
                if (s == null) continue;
                beat = s.BeatAt(world);
                if (beat != null) return s;
            }
            beat = null;
            return null;
        }
    }
}
