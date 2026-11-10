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
    // first section's entry. Online (R508, PawnRush_Round) the match link builds it before anything starts, from the
    // match's number (PawnRushSections.MatchSeed, the same on every PC, no packet and no "last round"), and moves its
    // start to the lobby's shared go once that is known (SetStartClock): every PC then has the same sections, the same
    // seeds and the same schedules. With finishRace the round has a crown finish on the last section's goal.
    //
    // R517: between two sections a corridor (SectionCorridor, 10 m, in the look of the zone it leaves): the next section's
    // entry goes on the corridor's exit. The same on every PC (it is built from the same picks), nothing on the wire.
    [DefaultExecutionOrder(-500)]
    public sealed class PawnRushRound : MonoBehaviour
    {
        [SerializeField] PawnRushSectionSet set;
        [Tooltip("Players per team: which wings and lifts the sections open (3·4 a side: one wing).")]
        [SerializeField, Range(1, 6)] int teamSize = 6;
        [Tooltip("0: a new seed every round (offline: the clock). Otherwise always this one.")]
        [SerializeField] int seed;
        [SerializeField] bool buildOnAwake = true;
        [Tooltip("The match scene (R508): build when the scene starts unless the match link has built it already (it does so as the scene loads).")]
        [SerializeField] bool buildOnStart;
        [Tooltip("Six start spots a team at the first section's entry (the match's start places too).")]
        [SerializeField] bool startSpots = true;
        [Tooltip("The match scene (R508): a crown finish (CrownFinish) on the last section's goal - a team majority on it wins.")]
        [SerializeField] bool finishRace;

        // The previous round's picks (offline: this session), left out where a slot has another.
        static int[] lastPicks;

        readonly List<PawnRushSection> sections = new List<PawnRushSection>();
        readonly List<Transform> corridors = new List<Transform>();
        readonly List<string> skipped = new List<string>();
        GameObject holder;

        public static PawnRushRound Current { get; private set; }
        public static event Action<PawnRushRound> Built;

        public IReadOnlyList<PawnRushSection> Sections => sections;

        // R525 (user 11-09 build test: "미니게임 즉시 클리어 치트 필요"): a team's next mini-game in course order that is not
        // done yet (its gate still shut), or null. A team cannot pass a mini-game it has not done, so this is the one its
        // players are at or running to.
        public MissionStation NextOpenStation(int team)
        {
            foreach (var section in sections)
            {
                var games = section != null ? section.GetComponent<MiniGameSection>() : null;
                if (games == null || games.Stations == null) continue;
                foreach (var station in games.Stations)
                    if (station != null && station.Team == team && station.Game != null && !station.Game.Completed) return station;
            }
            return null;
        }

        // The development cheat itself (offline F6, or the match host for whoever pressed it): that mini-game is done
        // now, as if the team had played it out (its gate opens; online the host's next state tells every PC). Only
        // where the mini-games are judged: offline, or on the match's host.
        public string CompleteNextMiniGame(int team, string who)
        {
            var station = NextOpenStation(team);
            string line = station == null
                ? $"[PawnRush] 치트(F6): {who} — {Teams.Name(team)}팀에 남은 미니게임이 없습니다"
                : $"[PawnRush] 치트(F6): {who} — {Teams.Name(team)}팀 미니게임 '{station.GetComponentInParent<PawnRushSection>()?.DisplayName}' 완료";
            if (station != null) station.Game.ForceComplete();
            Debug.Log(line);
            return line;
        }
        // The corridors between them (R517): corridor i joins Sections[i] to Sections[i + 1].
        public IReadOnlyList<Transform> Corridors => corridors;
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

        void Start()
        {
            if (buildOnStart && holder == null) Build(seed != 0 ? seed : Environment.TickCount);
        }

        public bool HasBuilt => holder != null;

        void OnDestroy()
        {
            if (Current == this) Current = null;
            CourseSkills.PawnRush = false;
        }

        public void Build(int roundSeed) => Build(roundSeed, ObstacleClock.Now, false);

        // A match's round (R508): the match's seed and start, and no leaving out of this PC's own last round (another PC
        // has another last round), so every PC draws the same sections.
        public void BuildForMatch(int roundSeed, double startClock) => Build(roundSeed, startClock, true);

        void Build(int roundSeed, double startClock, bool fresh)
        {
            Clear();
            Seed = roundSeed;
            StartClock = startClock;
            if (set == null)
            {
                Debug.LogError("[PawnRush] PawnRushRound에 구간 목록(PawnRushSections)이 없습니다.");
                return;
            }
            var prefabs = new List<GameObject>();
            var picks = PawnRushSections.Choose(roundSeed, set.Options(prefabs), fresh ? null : lastPicks);
            if (!fresh) lastPicks = picks;

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
                if (sections.Count > 0)
                {
                    var corridor = SectionCorridor.Build(holder.transform, at, sections[sections.Count - 1], section, corridors.Count + 1);
                    corridor.SetSiblingIndex(go.transform.GetSiblingIndex());   // in the hierarchy as on the way: before it
                    corridors.Add(corridor);
                    at = SectionCorridor.Exit(corridor);
                }
                section.PlaceEntryOn(at);
                section.Configure(slot, PawnRushSections.SectionSeed(roundSeed, slot), teamSize, StartClock);
                at = section.ExitWorld;
                sections.Add(section);
                log.Append(slot + 1).Append(' ').Append(label).Append(" = ").Append(section.DisplayName).Append(" · ");
            }
            if (startSpots && sections.Count > 0) StartSpots(holder.transform, sections[0]);
            holder.SetActive(true);
            if (finishRace) BindFinish();
            Debug.Log(log.ToString());
            CheckWireRange();
            Built?.Invoke(this);
        }

        // R541 (11-11 online test: participants met an unseen wall on the hall's last glass stones while the host walked
        // on): a client draws every pawn from the host's snapshots, and those carry a position only so far (WirePoint:
        // ±8388 m across, ±320 m up). Until R541 it was ±320 m every way, and a seven-section round runs to about 580 m:
        // every pawn past 320 m was drawn on that line. A round that leaves the range again says so as it is built.
        // The margins: how far a section reaches beyond its entry and exit (its wings, its upper floors).
        const float WireMarginFlat = 200f, WireMarginHeight = 60f;

        void CheckWireRange()
        {
            float flat = WirePoint.FlatRange - WireMarginFlat, height = WirePoint.HeightRange - WireMarginHeight;
            Vector3 start = transform.position;
            foreach (var section in sections)
            {
                Vector3 end = section.ExitWorld.position;
                foreach (var p in new[] { start, end })
                {
                    if (Mathf.Abs(p.x) <= flat && Mathf.Abs(p.z) <= flat && Mathf.Abs(p.y) <= height) continue;
                    Debug.LogError($"[PawnRush] 판의 '{section.DisplayName}' 구간({p.x:0}, {p.y:0}, {p.z:0} m)이 온라인 위치 범위 " +
                                   $"(가로 ±{WirePoint.FlatRange:0} m, 높이 ±{WirePoint.HeightRange:0} m)에 너무 가깝거나 밖에 있습니다. " +
                                   "참가자 화면에서는 그 너머의 말이 범위 끝에 멈춰 보입니다 (PITFALLS 38).");
                    return;
                }
                start = end;
            }
        }

        // The crown finish on this round's goal (the last section's FinishZone: the war room's).
        void BindFinish()
        {
            var finish = GetComponent<CrownFinish>();
            if (finish == null) finish = gameObject.AddComponent<CrownFinish>();
            finish.Bind(holder.transform);
        }

        public CrownFinish Finish => finishRace ? GetComponent<CrownFinish>() : null;

        // The match's shared start, once the lobby has it (R508): every section's schedules, mini-games and gates count
        // from it, the same on every PC. Before the race; the mini-games are rebuilt from it.
        public void SetStartClock(double clock)
        {
            if (Math.Abs(clock - StartClock) < 1e-6) return;
            StartClock = clock;
            foreach (var s in sections)
                if (s != null) s.Configure(s.Slot, s.Seed, teamSize, StartClock);
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
            corridors.Clear();
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

        // Is a world point in one of the corridors between the sections (R517).
        public bool InCorridor(Vector3 world)
        {
            foreach (var c in corridors)
                if (SectionCorridor.Holds(c, world)) return true;
            return false;
        }
    }
}
