using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // 7. The grand banquet hall's chandeliers as its guessing stretch (R905, the user: "대계단의 '오징어 게임 다리'처럼
    //    앞서나간 사람이 계속 달리며 앞서 가는게 아니라, 시행착오를 거치게 되면서 뒷 사람들과 자연스럽게 거리가 좁혀지는
    //    요소가 있어야 해 ... 샹들리에 쪽이 ... 뒷사람과 거리가 좁혀지는 구간으로서 작용됐으면 좋겠어"). Of the two
    //    chandeliers of a rank (the west line's and the east line's) one hangs fast and the other's winch is not latched;
    //    they look and swing alike. Which is which comes from the section's seed: the same for the whole round and on
    //    every PC (GuessBridge's way).
    //
    //    A loose one holds until it carries someone through the middle of its swing, out over the dance floor. There its
    //    chain runs out: it stops swinging and drops its `sink` metres in SinkSeconds with whoever hangs on it, who is
    //    shaken off as the chain snaps taut (a hit that lets go: IHitReceiver.ApplyHit - RagdollPawn's LetGo) and falls
    //    to the last checkpoint, the candle beam it set out from. The chandelier stays down, dark and still, for the rest
    //    of the round: whoever comes after sees which of the two not to take. And the one hung fast, once it has carried
    //    someone through the middle of its swing, is proven: in the dressed hall its candles stay lit
    //    (ChandelierRideLights.Proven; each PC marks it from what it sees). The one in front pays for the way; those
    //    behind close up.
    //
    //    A chandelier falls because someone rides it, not on the shared clock: the authority (offline, or the host)
    //    decides, and online the host sends each rank's state in the course state (ISectionNetPart: a byte a rank, 0 up,
    //    else how long ago it fell in tenths of a second). The section's relief (SectionRelief): every chandelier holds,
    //    the fallen ones are wound back up, and all of them show it (ChandelierRideLights.Safe).
    public sealed class LooseChandeliers : MonoBehaviour, ISectionPart, ISectionNetPart
    {
        public const float SinkSeconds = .9f, RaiseSeconds = 2.5f;

        [Tooltip("Rank by rank from the loft: the west line's rope and the east line's.")]
        [SerializeField] RopeLine[] west = new RopeLine[0];
        [SerializeField] RopeLine[] east = new RopeLine[0];
        [Tooltip("Each rank's ropes as the builder made them (a fallen one's swing is switched off, and on again when it is hung again).")]
        [SerializeField] float[] lengths = new float[0];
        [SerializeField] float[] westPhase = new float[0];
        [SerializeField] float[] eastPhase = new float[0];
        [SerializeField] float swing = 40f, period = 5f;
        [Tooltip("How far each rank's loose chandelier drops, metres.")]
        [SerializeField] float[] sink = new float[0];
        [Tooltip("The paid-out chain's picture for each chandelier (the west ranks, then the east): an upright cylinder 2 m long before scaling.")]
        [SerializeField] Transform[] chains = new Transform[0];
        [SerializeField] float chainThickness = .09f;
        [Tooltip("Mixed into the section's seed: this stretch's own stream.")]
        [SerializeField] int salt = 7;

        bool[] westLoose;
        float[] fellAt, lastAngle, lastFast, raiseFrom;     // per rank
        bool[] shook, stopped, restored, proven;            // per rank
        Vector3[] home;                           // per chandelier (the west ranks, then the east): its pivot as built
        ChandelierRideLights[] lights;
        float lightsLookAt;
        bool relieved, receivedState;
        float relievedAt = float.NegativeInfinity;
        GrandBanquetHallSound sound;
        readonly HashSet<IHitReceiver> shaken = new HashSet<IHitReceiver>();
        static readonly Collider[] near = new Collider[32];

        public int Ranks => Mathf.Min(west.Length, east.Length);
        // How many have fallen since the round began (on this PC).
        public int Falls { get; private set; }
        public bool Relieved => relieved;
        public bool WestLoose(int rank) => westLoose != null && rank < westLoose.Length && westLoose[rank];
        public bool Fallen(int rank) => fellAt != null && rank < fellAt.Length && !float.IsNegativeInfinity(fellAt[rank]);
        // The one hung fast has carried someone through the middle of its swing this round (as this PC saw it).
        public bool Proven(int rank) => proven != null && rank < proven.Length && proven[rank];
        public RopeLine Loose(int rank) => WestLoose(rank) ? west[rank] : east[rank];
        public RopeLine Fast(int rank) => WestLoose(rank) ? east[rank] : west[rank];
        public float Sink(int rank) => rank < sink.Length ? sink[rank] : 5f;

        // For the builder: on an inactive object, before Awake.
        public void Configure(RopeLine[] westRopes, RopeLine[] eastRopes, float[] ropeLengths, float[] westPhases, float[] eastPhases,
                              float swingDegrees, float periodSeconds, float[] sinkMetres, Transform[] chainPictures, float chainWidth)
        {
            west = westRopes;
            east = eastRopes;
            lengths = ropeLengths;
            westPhase = westPhases;
            eastPhase = eastPhases;
            swing = swingDegrees;
            period = periodSeconds;
            sink = sinkMetres;
            chains = chainPictures;
            chainThickness = chainWidth;
        }

        void Awake() => Ready();

        // The pivots are where the builder put them the first time this runs (nothing has fallen yet).
        void Ready()
        {
            int n = Ranks;
            if (home != null && home.Length == 2 * n) return;
            westLoose = new bool[n];
            fellAt = new float[n];
            lastAngle = new float[n];
            lastFast = new float[n];
            raiseFrom = new float[n];
            shook = new bool[n];
            stopped = new bool[n];
            restored = new bool[n];
            proven = new bool[n];
            home = new Vector3[2 * n];
            lights = new ChandelierRideLights[2 * n];
            for (int k = 0; k < n; k++)
            {
                fellAt[k] = float.NegativeInfinity;
                lastAngle[k] = lastFast[k] = float.NaN;
                restored[k] = true;
                home[k] = west[k].transform.localPosition;
                home[n + k] = east[k].transform.localPosition;
            }
        }

        public void OnSection(PawnRushSection section)
        {
            Ready();
            var rng = new SeededRandom(SeededSchedule.Hash(section.Seed, salt));
            for (int k = 0; k < Ranks; k++) westLoose[k] = (rng.NextUInt() & 1u) == 0u;
            relieved = receivedState = false;
            relievedAt = float.NegativeInfinity;
            Falls = 0;
            for (int k = 0; k < Ranks; k++) HangAgain(k);
            ShowLights();
        }

        // Rank k as built: both chandeliers at their pivots, swinging.
        void HangAgain(int k)
        {
            fellAt[k] = float.NegativeInfinity;
            lastAngle[k] = lastFast[k] = float.NaN;
            raiseFrom[k] = 0f;
            shook[k] = stopped[k] = proven[k] = false;
            restored[k] = true;
            Swing(k, true, true);
            Swing(k, false, true);
            Place(k, true, 0f);
            Place(k, false, 0f);
        }

        // A chandelier's swing on (as built) or off (its chain hangs straight); switched on, its rope can be taken again.
        void Swing(int k, bool westSide, bool on)
        {
            var rope = westSide ? west[k] : east[k];
            if (rope == null) return;
            rope.Configure(lengths[k], on ? swing : 0f, period, westSide ? westPhase[k] : eastPhase[k], 0f, null);
            if (on && !rope.enabled) rope.enabled = true;
        }

        // A chandelier `depth` under its pivot, and the chain paid out above it.
        void Place(int k, bool westSide, float depth)
        {
            int i = westSide ? k : Ranks + k;
            var rope = westSide ? west[k] : east[k];
            if (rope != null) rope.transform.localPosition = home[i] + Vector3.down * depth;
            var chain = i < chains.Length ? chains[i] : null;
            if (chain == null) return;
            bool shown = depth > .02f;
            if (chain.gameObject.activeSelf != shown) chain.gameObject.SetActive(shown);
            if (!shown) return;
            chain.localPosition = home[i] + Vector3.down * (depth * .5f);
            chain.localScale = new Vector3(chainThickness, depth * .5f, chainThickness);
        }

        // How far rank k's loose chandelier hangs under its pivot: running out faster and faster and stopped dead; wound
        // back up evenly by the relief.
        public float Depth(int rank) => home == null || rank >= Ranks ? 0f : Depth(rank, Time.time);

        float Depth(int k, float now)
        {
            if (Fallen(k))
            {
                float t = Mathf.Clamp01((now - fellAt[k]) / SinkSeconds);
                return Sink(k) * t * t;
            }
            if (restored[k]) return 0f;
            float u = Mathf.Clamp01((now - relievedAt) / RaiseSeconds);
            return raiseFrom[k] * (1f - u * u * (3f - 2f * u));
        }

        // ------------------------------------------------------------------ online

        public int NetBytes => Ranks;

        public void WriteNet(ref NetBlock block)
        {
            for (int k = 0; k < Ranks; k++)
            {
                float ago = Fallen(k) ? Time.time - fellAt[k] : -1f;
                block.U8(ago < 0f ? 0 : Mathf.Clamp(1 + Mathf.FloorToInt(ago * 10f), 1, 254));
            }
        }

        // Not the host: the host's chandeliers. One fallen there falls here (counted from when it did there); one up there
        // (a new round) hangs again here.
        public void ReadNet(ref NetBlock block)
        {
            for (int k = 0; k < Ranks; k++)
            {
                int v = block.ReadU8();
                if (home == null) continue;
                if (v == 0)
                {
                    if (Fallen(k)) HangAgain(k);
                    continue;
                }
                // A relieved stretch's chandeliers stay up here (the host's come up in its next state).
                if (relieved) continue;
                float ago = (v - 1) / 10f, at = Time.time - ago;
                if (!Fallen(k)) Fall(k, at, receivedState && ago <= .4f);
                else if (Mathf.Abs(fellAt[k] - at) > .5f) fellAt[k] = at;
            }
            receivedState = true;
        }

        // ------------------------------------------------------------------ falling

        void Fall(int k, float at, bool heard)
        {
            fellAt[k] = at;
            shook[k] = stopped[k] = false;
            restored[k] = true;
            shaken.Clear();
            Falls++;
            Swing(k, westLoose[k], false);
            ShowLights();
            if (heard) Sound()?.ChandelierFell(Loose(k).Point(lengths[k], ObstacleClock.Now), false);
        }

        // The section's relief: from now on every chandelier holds; the fallen ones are wound back up.
        public void Relieve()
        {
            Ready();
            relieved = true;
            relievedAt = Time.time;
            for (int k = 0; k < Ranks; k++)
            {
                raiseFrom[k] = Depth(k, relievedAt);
                restored[k] = !Fallen(k);
                fellAt[k] = float.NegativeInfinity;
                shook[k] = stopped[k] = false;
            }
            ShowLights();
        }

        void FixedUpdate()
        {
            if (home == null) return;
            double clock = ObstacleClock.Now;
            float now = Time.time;
            bool authority = ObstacleClock.Authority;
            for (int k = 0; k < Ranks; k++)
            {
                // Every PC, from what it sees (nothing is sent): the one hung fast, ridden through the middle of its swing,
                // is proven - its candles stay lit for those who come after.
                var fast = Fast(k);
                if (fast != null && !proven[k])
                {
                    float held = fast.Angle(clock);
                    bool mid = !float.IsNaN(lastFast[k]) && (held > 0f) != (lastFast[k] > 0f);
                    lastFast[k] = held;
                    if (mid && Riders(fast, clock, false) > 0)
                    {
                        proven[k] = true;
                        ShowLights();
                    }
                }
                if (!authority) continue;
                var rope = Loose(k);
                if (rope == null) continue;
                if (!Fallen(k))
                {
                    if (relieved || !restored[k]) continue;
                    // Through the middle of its swing with someone on it: the winch lets go.
                    float angle = rope.Angle(clock);
                    bool crossed = !float.IsNaN(lastAngle[k]) && (angle > 0f) != (lastAngle[k] > 0f);
                    lastAngle[k] = angle;
                    if (crossed && Riders(rope, clock, false) > 0) Fall(k, now, true);
                    continue;
                }
                // The chain snaps taut: whoever still hangs on is shaken off.
                if (now - fellAt[k] >= SinkSeconds) Riders(rope, clock, true);
            }
        }

        // Characters hanging on a chandelier: bodies by the last of its rope and just under its hoop (a pawn hangs from the
        // hoop, its hips 0.6 m under its hands). `shake`: the authority's hit that makes each let go, once a fall.
        int Riders(RopeLine rope, double clock, bool shake)
        {
            float length = rope.Length;
            Vector3 down = rope.Direction(clock);
            Vector3 a = rope.Point(length - 2.3f, clock), b = rope.Point(length, clock) + down * .9f;
            int n = Physics.OverlapCapsuleNonAlloc(a, b, .5f, near, ~0, QueryTriggerInteraction.Ignore), count = 0;
            for (int i = 0; i < n; i++)
            {
                if (near[i] == null || near[i].GetComponentInParent<ILaunchable>() == null) continue;
                count++;
                if (!shake) continue;
                var hits = near[i].GetComponentInParent<IHitReceiver>();
                if (hits != null && shaken.Add(hits)) hits.ApplyHit(Vector3.down * 2f, 0f, 0f, true);
            }
            return count;
        }

        // Every PC: the fallen chandeliers where they hang now, their chains, their lights.
        void Update()
        {
            if (home == null) return;
            float now = Time.time;
            for (int k = 0; k < Ranks; k++)
            {
                bool westSide = westLoose[k];
                Place(k, westSide, Depth(k, now));
                if (Fallen(k))
                {
                    float ago = now - fellAt[k];
                    if (!stopped[k] && ago >= SinkSeconds)
                    {
                        stopped[k] = true;
                        // (one that fell long ago - a late join - makes no sound)
                        if (ago < SinkSeconds + .5f) Sound()?.ChandelierFell(Loose(k).Point(lengths[k], ObstacleClock.Now), true);
                    }
                    // Its hoop cannot be taken again (the authority has shaken its riders off by now).
                    if (!shook[k] && ago >= SinkSeconds + .4f)
                    {
                        shook[k] = true;
                        var rope = Loose(k);
                        if (rope != null && rope.enabled) rope.enabled = false;
                    }
                }
                else if (!restored[k] && now - relievedAt >= RaiseSeconds)
                {
                    restored[k] = true;
                    Swing(k, westSide, true);
                    ShowLights();
                }
            }
            if (now >= lightsLookAt)
            {
                lightsLookAt = now + .5f;
                ShowLights();
            }
        }

        // The dressed hall's lamps and candles on each chandelier (ChandelierRideLights, added once the models hang): out
        // on one that is down, lit on every one that holds once the relief is given.
        void ShowLights()
        {
            if (lights == null) return;
            int n = Ranks;
            for (int i = 0; i < 2 * n; i++)
            {
                int k = i % n;
                bool westSide = i < n;
                var rope = westSide ? west[k] : east[k];
                if (lights[i] == null && rope != null) lights[i] = rope.GetComponentInChildren<ChandelierRideLights>(true);
                if (lights[i] == null) continue;
                bool loose = westSide == westLoose[k], down = loose && (Fallen(k) || !restored[k]);
                lights[i].Out = down;
                lights[i].Safe = relieved && !down;
                lights[i].Proven = !loose && proven[k];
            }
        }

        GrandBanquetHallSound Sound()
        {
            if (sound == null)
            {
                var section = GetComponentInParent<PawnRushSection>();
                if (section != null) sound = section.GetComponentInChildren<GrandBanquetHallSound>();
            }
            return sound;
        }
    }
}
