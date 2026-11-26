using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using UnityEngine;
using B = ChessFight.PawnRush.GrandBanquetHallBuilder;

namespace ChessFight.PawnRush
{
    // R844 (user 11-26: "대연회장 사운드 작업 읽고 시작해", after the R843 design Docs/PawnRush/PAWN_RUSH_BANQUET_HALL_SFX.md):
    // the grand banquet hall's sounds - the design's first and second tiers (the six cue sounds and twelve device sounds).
    // Before this the hall had only the hearths' crackle (seoungtest's dressing). Every device is a pure function of the
    // shared obstacle clock, so each sound is played on every PC at the same moment by reading that clock here - nothing
    // is sent, the protocol is as it was. As the observatory's RoyalObservatorySound (R742):
    //   waiters (4)        the kitchen half-door's flap as each tray comes through it; their plate stacks clink (loops)
    //   tablecloth trick   a magic spell charging up as the cloth turns red (2 s; was a drum roll), the pull (cymbal + cloth snap), a whoosh on every
    //                      pawn thrown, the cloth's flap as it is laid again
    //   cloche lifts (2)   a service bell 0.6 s before the cloche comes down (at the table: lower at the dais), the clang
    //                      as it meets the platter, the hoist while it rises / goes down, a ring as it lifts off
    //   waltz lamps        two glockenspiel notes as the boarding lamps light (the stage's alighting lamps: higher)
    //   the waltz          a music-box whirr and silk on the three couples nearest the listener (loops)
    //   stage curtain      the conductor's three taps a second before it opens; the rail and velvet while it moves
    //   chocolate fountain its heavy pour (loop)
    //   chandeliers (10)   a crystal tinkle at each end of each rope's swing (where the next one is caught)
    //   the hall           room tone (2D); a short fanfare for this player only, the first time it stands on the balcony
    // R845 이어서 (user 11-26 "나머지 사운드 작업 시작"), the design's third tier:
    //   stage curtain      a soft deep thud as its halves meet again (the cycle's end)
    //   great chandelier   the quiet shimmer of its crystals (loop) - the resting place heard as one
    //   guests (west row)  spoons on glasses as the cloth lands back under the ware (the trick's cycle end)
    //   double basses      a low string boom when any pawn bumps one (each PC from what it sees: nothing sent)
    //   chocolate          sticky footsteps on the fountain's bowl and tiers (ChocolateFloor, PawnRushStepSfx)
    // Clips: Resources/BanquetHallSfx (ArtSource/PawnRush/BanquetHall/Sound: ElevenLabs takes, bake.py). Every source
    // goes through SectionSoundGate (heard only in the hall, times the effects volume). Added by GrandBanquetHallBuilder
    // in play; works on the grey box (main) and on the dressed hall (seoungtest) alike.
    public sealed class GrandBanquetHallSound : MonoBehaviour
    {
        const string Folder = "BanquetHallSfx/";
        public static bool Enabled = true;

        // (11-26: the user picked the takes by ear on the listening page - picks.json; the pull mixes two takes.)
        // AudioSource volumes from bake.py (`vol`: heard close at the default effects volume as PAWN_RUSH_AUDIO.md
        // section 0 asks; 1 where the take is quieter).
        static readonly Dictionary<string, float> Volume = new Dictionary<string, float>
        {
            { "BhDrumRoll", .425f }, { "BhMagicBuild", .606f }, { "BhClothPull", 1f }, { "BhServiceBell", .771f }, { "BhClocheShut", 1f },
            { "BhLampChime", .856f }, { "BhCurtainCue", 1f },
            { "BhHallAmbience", .249f }, { "BhKitchenDoor", .526f }, { "BhTrayClink", 1f }, { "BhClothThrown", .657f },
            { "BhClothLay", .535f }, { "BhClocheOpen", .718f }, { "BhHoistLoop", 1f }, { "BhGownLoop", .556f },
            { "BhCurtainDraw", .558f }, { "BhChocolateLoop", .395f }, { "BhChandelierSwing", .61f }, { "BhArrival", .388f },
            { "BhCurtainMeet", .535f }, { "BhGreatChandelier", .317f }, { "BhGuestsToast", .416f }, { "BhBassBump", .271f },
        };

        // A clock step longer than this (the first frame, a hitch, joining late) plays nothing.
        const float LongestStep = .5f;
        const int Voices = 14, GownLoops = 3;
        // Pitches in F major over the take as baked (design section 4): the bell a little lower at the dais, the stage's
        // lamps a major third over the dais's.
        const float BellAtDais = .84f, StageLamp = 1.26f;

        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly HashSet<string> missing = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            clips.Clear();
            missing.Clear();
            Enabled = true;
        }

        sealed class Lift { public ClocheLift platter; public Loop hoist; public float last; }
        sealed class Lamp { public ClockLamp lamp; public Vector3 at; public float pitch; public bool was; }
        sealed class Swing { public RopeLine rope; public float last, rate; }
        sealed class Curtain { public CurtainPleat pleat; public Loop draw; public float last; }
        sealed class Loop { public AudioSource a; public float volume; }
        sealed class Bass { public float z; public Bounds box; public bool touched; public float quietUntil; }
        readonly List<Bass> basses = new List<Bass>();
        static readonly Collider[] near = new Collider[16];
        float bassLookAt;

        readonly List<BanquetWaiter> waiters = new List<BanquetWaiter>();
        readonly List<Lift> lifts = new List<Lift>();
        readonly List<Lamp> lamps = new List<Lamp>();
        readonly List<Swing> swings = new List<Swing>();
        readonly List<Curtain> curtains = new List<Curtain>();
        readonly List<WaltzCouple> couples = new List<WaltzCouple>();
        readonly List<Loop> gowns = new List<Loop>();
        readonly AudioSource[] voices = new AudioSource[Voices];
        int nextVoice;

        TableclothTrick trick;
        Transform frame;
        AudioListener ear;
        double before = double.NaN;
        bool ready, arrived;

        void Start()
        {
            if (!Enabled) return;
            var section = GetComponentInParent<PawnRushSection>();
            frame = section != null ? section.transform : transform;
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("Banquet sound " + (i + 1));
                go.transform.SetParent(transform, false);
                voices[i] = Source(go, 3f, 30f);
                voices[i].volume = 1f;
            }
            int started = 0;

            GetComponentsInChildren(true, waiters);
            foreach (var w in waiters)
            {
                var tray = new GameObject("Tray clink spot");
                tray.transform.SetParent(w.transform, false);
                tray.transform.localPosition = new Vector3(0f, B.TrayY + .6f, 0f);
                AddLoop(tray.transform, "BhTrayClink", 2f, 14f, .94f + .04f * started, started++);
            }

            trick = GetComponentInChildren<TableclothTrick>(true);
            if (trick != null) trick.Threw += Thrown;

            foreach (var lift in GetComponentsInChildren<ClocheLift>(true))
            {
                if (lift.Which != ClocheLift.Part.Platter) continue;
                var spot = new GameObject("Hoist spot");
                spot.transform.SetParent(lift.transform, false);
                spot.transform.localPosition = Vector3.up * .8f;
                var hoist = AddLoop(spot.transform, "BhHoistLoop", 3f, 24f, 1f, started++);
                if (hoist != null) hoist.a.volume = 0f;
                lifts.Add(new Lift { platter = lift, hoist = hoist });
            }

            // Both lamps of a pair light together: one chime between them.
            var pairs = new Dictionary<string, Lamp>();
            foreach (var lamp in GetComponentsInChildren<ClockLamp>(true))
            {
                var post = lamp.transform.parent;
                string pair = post != null ? post.name.Substring(0, post.name.Length - 2) : lamp.name;
                if (pairs.TryGetValue(pair, out var known))
                {
                    known.at = (known.at + lamp.transform.position) * .5f;
                    continue;
                }
                var l = new Lamp { lamp = lamp, at = lamp.transform.position, pitch = pair.Contains("alighting") ? StageLamp : 1f };
                pairs[pair] = l;
                lamps.Add(l);
            }

            foreach (var rope in GetComponentsInChildren<RopeLine>(true))
                if (rope.name.StartsWith("BH chandelier")) swings.Add(new Swing { rope = rope, last = rope.Angle(ObstacleClock.Now) });

            // The curtain's two leading pleats carry the rail's sound (they travel furthest).
            foreach (var pleat in GetComponentsInChildren<CurtainPleat>(true))
            {
                if (pleat.name != "BH pleat S1" && pleat.name != "BH pleat N1") continue;
                var spot = new GameObject("Curtain rail spot");
                spot.transform.SetParent(pleat.transform, false);
                spot.transform.localPosition = new Vector3(0f, B.RailY - 1f, 0f);
                var draw = AddLoop(spot.transform, "BhCurtainDraw", 4f, 30f, 1f, started++);
                if (draw != null) draw.a.volume = 0f;
                curtains.Add(new Curtain { pleat = pleat, draw = draw, last = pleat.Open(ObstacleClock.Now) });
            }

            GetComponentsInChildren(true, couples);
            for (int i = 0; i < GownLoops && i < couples.Count; i++)
            {
                var go = new GameObject("Waltz couple spot " + (i + 1));
                go.transform.SetParent(transform, false);
                var loop = AddLoop(go.transform, "BhGownLoop", 2f, 12f, .96f + .04f * i, started++);
                if (loop != null) gowns.Add(loop);
            }

            var fountain = new GameObject("Chocolate fountain spot");
            fountain.transform.SetParent(transform, false);
            fountain.transform.position = frame.TransformPoint(new Vector3(B.Fountain.x, B.TierTop[0], B.Fountain.y));
            AddLoop(fountain.transform, "BhChocolateLoop", 4f, 30f, 1f, started++);

            // R845 이어서: the great chandelier's crystals, over its dish
            var great = new GameObject("Great chandelier spot");
            great.transform.SetParent(transform, false);
            great.transform.position = frame.TransformPoint(new Vector3(0f, B.GreatY + .6f, B.GreatZ));
            AddLoop(great.transform, "BhGreatChandelier", 3f, 18f, 1f, started++);

            // the double basses (their body and neck colliders), bumped
            foreach (var c in GetComponentsInChildren<Collider>(true))
                if (c.name == "BH double bass" || c.name == "BH bass neck")
                {
                    Bass known = null;
                    foreach (var b in basses) if (Mathf.Abs(b.z - c.bounds.center.z) < 1f) known = b;
                    if (known == null) basses.Add(known = new Bass { z = c.bounds.center.z, box = c.bounds });
                    else known.box.Encapsulate(c.bounds);
                }

            // the fountain's chocolate underfoot (its bowl and tiers): sticky steps (PawnRushStepSfx)
            foreach (var c in GetComponentsInChildren<Collider>(true))
                if ((c.name == "BH fountain bowl" || c.name.StartsWith("BH chocolate tier")) && c.GetComponent<ChocolateFloor>() == null)
                    c.gameObject.AddComponent<ChocolateFloor>();

            // The room tone, 2D, on a spot inside the hall so the gate counts it as this section's.
            var room = new GameObject("Hall room tone");
            room.transform.SetParent(transform, false);
            room.transform.position = frame.TransformPoint(new Vector3(0f, 1f, 30f));
            var tone = AddLoop(room.transform, "BhHallAmbience", 1f, 2f, 1f, started++);
            if (tone != null) tone.a.spatialBlend = 0f;

            ready = true;
            Debug.Log($"[대연회장 소리] 웨이터 {waiters.Count} · 식탁보 {(trick != null ? 1 : 0)} · 은 뚜껑 {lifts.Count} · 등 {lamps.Count}"
                      + $" · 샹들리에 {swings.Count} · 막 {curtains.Count} · 춤추는 쌍 {couples.Count}(가까운 {gowns.Count}쌍에 소리)");
        }

        void OnDestroy()
        {
            if (trick != null) trick.Threw -= Thrown;
        }

        static AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var clip)) return clip;
            clip = Resources.Load<AudioClip>(Folder + name);
            clips[name] = clip;
            if (clip == null && missing.Add(name)) Debug.LogWarning("[대연회장 소리] 소리 파일이 없어 재생하지 않습니다: Resources/" + Folder + name);
            return clip;
        }

        static float VolumeOf(string name) => Volume.TryGetValue(name, out var v) ? v : .5f;

        static AudioSource Source(GameObject on, float near, float far)
        {
            var a = on.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = near;
            a.maxDistance = far;
            a.dopplerLevel = 0f;
            a.volume = 0f;
            SectionSoundGate.Gate(a);
            return a;
        }

        Loop AddLoop(Transform on, string name, float near, float far, float pitch, int index)
        {
            var clip = Clip(name);
            if (clip == null) return null;
            var go = new GameObject("Sound: " + name);
            go.transform.SetParent(on, false);
            var a = Source(go, near, far);
            a.clip = clip;
            a.loop = true;
            a.pitch = pitch;
            a.volume = VolumeOf(name);
            a.time = clip.length * ((index * .37f) % 1f);   // two of a kind never drone in step
            a.Play();
            return new Loop { a = a, volume = a.volume };
        }

        // A one-off at a world spot (3D, `near` .. `far` metres), or flat for this PC alone (twoD).
        void Play(string name, Vector3 at, float near, float far, float pitch = 1f, bool twoD = false)
        {
            var clip = Clip(name);
            if (clip == null) return;
            var a = voices[nextVoice];
            nextVoice = (nextVoice + 1) % Voices;
            a.transform.position = at;
            a.spatialBlend = twoD ? 0f : 1f;
            a.minDistance = near;
            a.maxDistance = far;
            a.pitch = pitch * Random.Range(.97f, 1.03f);
            a.volume = 1f;
            a.PlayOneShot(clip, VolumeOf(name));
        }

        Vector3 At(float x, float y, float z) => frame.TransformPoint(new Vector3(x, y, z));

        // Whether `mark` (seconds into a cycle) was passed going from cycle time `from` to `to` (a wrap counts).
        static bool Crossed(float from, float to, float mark)
        {
            if (to >= from) return from < mark && mark <= to;
            return from < mark || mark <= to;
        }

        static float Cycle(double time, float cycle)
        {
            if (cycle <= 0f) return 0f;
            double t = time % cycle;
            return (float)(t < 0 ? t + cycle : t);
        }

        // Played in Update, so the gate (its LateUpdate) has each pooled voice's new spot the same frame.
        void Update()
        {
            if (!ready) return;
            double now = ObstacleClock.Now;
            double step = now - before;
            bool live = !double.IsNaN(before) && step > 0 && step < LongestStep;
            if (live)
            {
                Waiters(before, now);
                Tablecloth(before, now);
                Lamps(now);
                Chandeliers(now);
                Curtains(before, now);
            }
            Lifts(before, now, live, (float)step);
            CurtainRail(now, live, (float)step);
            if (double.IsNaN(before) || step > 0) before = now;   // a clock that stands still keeps its last time
            Gowns();
            Arrival();
            Basses();
        }

        // R845 이어서: a pawn (any character body) touching a double bass booms its low strings, once per touch and at most
        // once a second; looked for ten times a second.
        void Basses()
        {
            if (basses.Count == 0 || Time.time < bassLookAt) return;
            bassLookAt = Time.time + .1f;
            foreach (var b in basses)
            {
                int n = Physics.OverlapBoxNonAlloc(b.box.center, b.box.extents + Vector3.one * .15f, near, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                bool now = false;
                for (int i = 0; i < n && !now; i++)
                    if (near[i] != null && near[i].GetComponentInParent<ILaunchable>() != null) now = true;
                if (now && !b.touched && Time.time >= b.quietUntil)
                {
                    Play("BhBassBump", b.box.center + Vector3.up * 1.5f, 3f, 20f);
                    b.quietUntil = Time.time + 1f;
                }
                b.touched = now;
            }
        }

        // The kitchen half-door flaps as each tray comes out through it (z = KitchenZ1).
        void Waiters(double from, double to)
        {
            float door = B.KitchenZ1 - B.WaiterZ0;
            foreach (var w in waiters)
            {
                if (w == null || !w.isActiveAndEnabled) continue;
                float a = (float)w.Along(from), b = (float)w.Along(to);
                if (Crossed(a, b, door)) Play("BhKitchenDoor", At(29f, 2.4f, B.KitchenZ1), 3f, 22f);
            }
        }

        void Tablecloth(double from, double to)
        {
            if (trick == null || !trick.isActiveAndEnabled) return;
            float a = trick.Phase(from), b = trick.Phase(to);
            // (11-26 the user: "드럼 사운드 보다는 마법 같은 소리가 더 잘어울리겠어") a spell charging up for the two seconds, not the drum
            if (Crossed(a, b, trick.RollAt)) Play("BhMagicBuild", At(B.RunnerX, B.TableY + 1.5f, B.TableZ0 - 1f), 10f, 60f);
            if (Crossed(a, b, trick.PullAt)) Play("BhClothPull", At(B.RunnerX, B.TableY + 1f, (B.TableZ0 + B.ClothZ1) * .5f), 8f, 50f);
            if (Crossed(a, b, trick.LayAt)) Play("BhClothLay", At(B.RunnerX, B.TableY + .5f, B.TableZ0 + 8f), 5f, 36f);
            // R845 이어서: the guests toast the trick as the cloth lands back (the cycle's end; their figurines clap then)
            if (Crossed(a, b, .05f)) Play("BhGuestsToast", At(12.8f, B.TableY + 1f, (B.TableZ0 + B.ClothZ1) * .5f), 4f, 26f);
        }

        // TableclothTrick.Pull, for every pawn it throws (FixedUpdate: the voice is placed now, the gate follows).
        void Thrown(Vector3 from)
        {
            if (ready) Play("BhClothThrown", from, 3f, 28f);
        }

        void Lifts(double from, double to, bool live, float step)
        {
            foreach (var l in lifts)
            {
                var p = l.platter;
                if (p == null || !p.isActiveAndEnabled) continue;
                p.Heights(to, out float platter, out _);
                if (live)
                {
                    float a = p.CycleAt(from), b = p.CycleAt(to);
                    var at = p.transform.position + Vector3.up * 1.2f;
                    // the bell 0.6 s before the cloche starts down; the clang as it lands; a ring as it lifts off
                    if (Crossed(a, b, p.BoardEnds - .6f)) Play("BhServiceBell", at, 3f, 24f);
                    if (Crossed(a, b, p.TopEnds - .6f)) Play("BhServiceBell", at, 3f, 24f, BellAtDais);
                    if (Crossed(a, b, p.ShutAtTable) || Crossed(a, b, p.ShutAtTop)) Play("BhClocheShut", at, 3f, 26f);
                    if (Crossed(a, b, p.UpEnds + .05f) || Crossed(a, b, p.DownEnds + .05f)) Play("BhClocheOpen", at + Vector3.up, 3f, 22f);
                }
                // the hoist as loud as the platter moves fast (1 at its fastest), a little higher going up
                if (l.hoist != null)
                {
                    float fastest = p.Rise * Mathf.PI * .5f / (p.UpEnds - p.ShutAtTable);
                    float speed = live && step > 0f ? (platter - l.last) / step : 0f;
                    float k = Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(.01f, fastest) * 1.6f);
                    l.hoist.a.volume = l.hoist.volume * k;
                    if (Mathf.Abs(speed) > .01f) l.hoist.a.pitch = speed > 0f ? 1.1f : .9f;
                }
                l.last = platter;
            }
        }

        // Two notes as a pair of lamps lights (a queen stands still in front of it 1.2 s later).
        void Lamps(double now)
        {
            foreach (var l in lamps)
            {
                if (l.lamp == null || !l.lamp.isActiveAndEnabled) continue;
                bool lit = l.lamp.Lit(now);
                if (lit && !l.was) Play("BhLampChime", l.at, 3f, 22f, l.pitch);
                l.was = lit;
            }
        }

        // A tinkle at each end of a rope's swing, where it turns back (its angle stops growing). Neighbours swing half a
        // period apart and meet at those ends, so the tinkle is when to change ropes. The long ropes ring lower.
        void Chandeliers(double now)
        {
            foreach (var s in swings)
            {
                if (s.rope == null || !s.rope.isActiveAndEnabled) continue;
                float angle = s.rope.Angle(now), rate = angle - s.last;
                if (s.rate != 0f && Mathf.Sign(rate) != Mathf.Sign(s.rate) && Mathf.Abs(rate) > 0f)
                    Play("BhChandelierSwing", s.rope.Point(s.rope.Length, now), 3f, 16f, s.rope.Length > 7f ? .85f : 1f);
                if (rate != 0f) s.rate = rate;
                s.last = angle;
            }
        }

        // The conductor's taps a second before the curtain opens (heard where its halves meet).
        void Curtains(double from, double to)
        {
            if (curtains.Count == 0) return;
            var c = curtains[0].pleat;
            if (c == null || !c.isActiveAndEnabled) return;
            if (Crossed(c.CycleAt(from), c.CycleAt(to), c.ShutSeconds - 1f))
                Play("BhCurtainCue", At(B.CurtainX + 1f, B.StageY + 2f, B.CurtainZ), 5f, 40f);
            // R845 이어서: the halves meeting again (the closing's end is the cycle's)
            if (Crossed(c.CycleAt(from), c.CycleAt(to), .005f))
                Play("BhCurtainMeet", At(B.CurtainX + .5f, B.StageY + 3f, B.CurtainZ), 4f, 24f);
        }

        // The rail and the velvet while a leading pleat moves, louder the faster, higher drawn back.
        void CurtainRail(double now, bool live, float step)
        {
            foreach (var c in curtains)
            {
                if (c.pleat == null || c.draw == null) continue;
                float open = c.pleat.Open(now);
                float rate = live && step > 0f ? (open - c.last) / step : 0f;
                // the fastest the eased opening runs is 1.5 / 5 s of the way a second (closing: 1.5 / 2 s)
                float k = Mathf.Clamp01(Mathf.Abs(rate) / (rate >= 0f ? .3f : .75f) * 1.4f);
                c.draw.a.volume = c.draw.volume * k;
                if (Mathf.Abs(rate) > .001f) c.draw.a.pitch = rate > 0f ? 1f : 1.25f;
                c.last = open;
            }
        }

        // The music box and silk on the couples nearest the listener (ten couples would only be noise).
        void Gowns()
        {
            if (gowns.Count == 0 || couples.Count == 0) return;
            if (ear == null || !ear.isActiveAndEnabled) ear = FindAnyObjectByType<AudioListener>();
            var e = ear != null ? ear.transform.position : frame.position;
            couples.Sort((x, y) => (x.transform.position - e).sqrMagnitude.CompareTo((y.transform.position - e).sqrMagnitude));
            for (int i = 0; i < gowns.Count; i++)
                gowns[i].a.transform.position = couples[Mathf.Min(i, couples.Count - 1)].transform.position + Vector3.up * 1.5f;
        }

        // This player's pawn on the king's balcony for the first time (again after it has been back down in the hall):
        // a short fanfare, for this PC alone (design section 6, item 3).
        void Arrival()
        {
            var pawn = MiniGameBase.LocalDriver?.FollowTarget;
            if (pawn == null) return;
            var p = frame.InverseTransformPoint(pawn.position);
            bool on = Mathf.Abs(p.x) < B.BalconyX && p.z > B.BalconyZ0 && p.z < B.HallZ1 && p.y > B.BalconyY - .5f && p.y < B.BalconyY + 3f;
            if (on && !arrived)
            {
                arrived = true;
                Play("BhArrival", pawn.position, 1f, 2f, 1f, true);
            }
            else if (arrived && p.y < B.DaisY - 1f) arrived = false;
        }
    }
}
