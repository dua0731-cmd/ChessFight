using System.Collections.Generic;
using ChessFight.Game;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R742 (user 11-21: "천문대 사운드 필요한 곳 확인해" -> "네, 만들어"): the Royal Observatory's sounds. Before this the
    // observatory had none. Every device is a pure function of the shared obstacle clock, so each sound is played on
    // every PC at the same moment by reading that clock here - nothing is sent. The places and moments are the ones
    // Docs/PawnRush/PAWN_RUSH_OBSERVATORY_SFX.md lists:
    //   meteor showers (2)   a chime as a warning ring appears, a whoosh as the stone starts down, a slam as it lands
    //   solar flare          a charge through the warning (1.3 s), the burst as the burn starts; burnt: a sizzle, own PC only
    //   focus beams (2)      one hum where the two light spots are; a pawn thrown: a crackle on it (every PC, from
    //                        RoyalObservatoryVfxBeamHit), and a hit for its own player only
    //   iris gates (4)       ticking through the warning, the scrape as the leaves close, the clang as they meet
    //   variable stars (2)   a rattle through the warning, a clack as each petal drops, the whirr as they come back up
    //   orbits (3), gears (4) a turning machine's loop on each
    //   rolling planet       a rumble while it rolls (louder the faster), a thud as it docks
    //   great globe          a quiet creak of its swinging rings
    //   the hall / balcony   the dome's night room tone, crossing to night wind out on the balcony
    // Clips: Resources/RoyalObservatorySfx (ArtSource/PawnRush/RoyalObservatory/Sound: ElevenLabs takes picked by numbers
    // only, bake.py). Every source goes through SectionSoundGate (heard only in here, times the effects volume). Added
    // by RoyalObservatoryBuilder beside the dressing; works on the graybox too (only the two "hit" sounds need the VFX).
    public sealed class RoyalObservatorySound : MonoBehaviour
    {
        const string Folder = "RoyalObservatorySfx/";
        public static bool Enabled = true;

        // AudioSource volumes from bake.py (`vol`: heard close at the default effects volume as PAWN_RUSH_AUDIO.md
        // section 0 asks - a usual one-off -21 dB, a big hit -18 dB, a loop -30 dB RMS; 1 where the take is quieter).
        static readonly Dictionary<string, float> Volume = new Dictionary<string, float>
        {
            { "RoMeteorWarn", .411f }, { "RoMeteorFall", .525f }, { "RoMeteorImpact", .609f },
            { "RoFlareCharge", .56f }, { "RoFlareBurst", .41f }, { "RoFlareHitMe", .799f },
            { "RoBeamHum", .395f }, { "RoBeamStrike", 1f }, { "RoBeamHitMe", 1f },
            { "RoIrisWarn", .722f }, { "RoIrisScrape", .611f }, { "RoIrisClash", 1f },
            { "RoPetalWarn", 1f }, { "RoPetalFold", .814f }, { "RoPetalRise", 1f },
            { "RoOrbitLoop", .314f }, { "RoGearLoop", .352f }, { "RoPlanetRoll", .539f }, { "RoPlanetDock", 1f },
            { "RoGlobeCreak", .249f }, { "RoHallAmbience", .249f }, { "RoBalconyWind", .314f },
        };

        // A clock step longer than this (the first frame, a hitch, joining late) plays nothing: only moments crossed
        // while watching are heard.
        const float LongestStep = .5f;
        const int Voices = 14;

        // The hall in the section's frame: round (0, 64), radius 53; the balcony is past the dome on the north side.
        static readonly Vector2 HallMiddle = new Vector2(0f, 64f);
        const float BalconyFrom = 49f, BalconyTo = 56f, BalconyNorth = 100f;

        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static readonly HashSet<string> missing = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            clips.Clear();
            missing.Clear();
            Enabled = true;
        }

        sealed class Gate { public IrisLeaf leaf; public Vector3 at; }
        sealed class Star { public List<StarPetal> petals = new List<StarPetal>(); public StarPetal lead; public Vector3 at; }
        sealed class Loop { public AudioSource a; public float volume; }

        readonly List<MeteorShower> showers = new List<MeteorShower>();
        readonly List<SolarFlare> flares = new List<SolarFlare>();
        readonly List<FocusBeam> beams = new List<FocusBeam>();
        readonly List<Gate> gates = new List<Gate>();
        readonly List<Star> stars = new List<Star>();
        readonly List<Loop> loops = new List<Loop>();
        readonly AudioSource[] voices = new AudioSource[Voices];
        int nextVoice;

        RollingPlanet planet;
        AudioSource roll, hum, hall, balcony;
        Vector3 rollLast;
        float rollSpeed;
        Transform frame;
        AudioListener ear;
        double before = double.NaN;
        bool ready;

        // The observatory's sound under a device effect (null on a graybox without this component).
        public static RoyalObservatorySound For(Component inside) => inside != null ? inside.GetComponentInParent<RoyalObservatorySound>() : null;

        void Start()
        {
            if (!Enabled) return;
            var section = GetComponentInParent<PawnRushSection>();
            frame = section != null ? section.transform : transform;
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("Observatory sound " + (i + 1));
                go.transform.SetParent(transform, false);
                voices[i] = Source(go, 3f, 30f);
                voices[i].volume = 1f;
            }

            GetComponentsInChildren(true, showers);
            GetComponentsInChildren(true, flares);
            GetComponentsInChildren(true, beams);

            foreach (var leaf in GetComponentsInChildren<IrisLeaf>(true))
            {
                if (!leaf.name.EndsWith(" W")) continue;   // both leaves of a gate share the timing
                var east = leaf.transform.parent != null ? leaf.transform.parent.Find(leaf.name.Substring(0, leaf.name.Length - 2) + " E") : null;
                var at = east != null ? (leaf.transform.position + east.position) * .5f : leaf.transform.position;
                gates.Add(new Gate { leaf = leaf, at = at + Vector3.up * 1.2f });
            }

            var byStar = new Dictionary<Transform, Star>();
            foreach (var petal in GetComponentsInChildren<StarPetal>(true))
            {
                var parent = petal.transform.parent != null ? petal.transform.parent : petal.transform;
                if (!byStar.TryGetValue(parent, out var star))
                {
                    star = new Star { at = parent.position + Vector3.up * .5f };
                    byStar[parent] = star;
                    stars.Add(star);
                }
                star.petals.Add(petal);
                if (star.lead == null || petal.FoldsAt < star.lead.FoldsAt) star.lead = petal;
            }

            int started = 0;
            foreach (var motion in GetComponentsInChildren<ObstacleMotion>(true))
            {
                string n = motion.name;
                if (n.StartsWith("Device_OrbitRing")) AddLoop(motion.transform, "RoOrbitLoop", 3f, 20f, .96f + .04f * started, started++);
                // R742 (user 11-21 "카시오페이 톱니 장난감 스러운 사운드야 묵직한 걸로해"): a heavy clock-tower take, played
                // a little low (0.84 .. 0.96, each gear its own) and heard a little farther, as big iron should be
                else if (n.StartsWith("Device_StarGear")) AddLoop(motion.transform, "RoGearLoop", 3f, 22f, .84f + .04f * (started % 4), started++);
            }
            var globe = GetComponentInChildren<GlobeArmillary>(true);
            if (globe != null) AddLoop(globe.transform, "RoGlobeCreak", 8f, 32f, 1f, started++);

            planet = GetComponentInChildren<RollingPlanet>(true);
            if (planet != null)
            {
                roll = AddLoop(planet.transform, "RoPlanetRoll", 3f, 26f, 1f, started++)?.a;
                rollLast = planet.transform.position;
            }

            if (beams.Count > 0)
            {
                var go = new GameObject("Focus beam hum");
                go.transform.SetParent(transform, false);
                hum = AddLoop(go.transform, "RoBeamHum", 3f, 22f, 1f, started++)?.a;
            }

            // The room tones, 2D. The hall's sits on the Dipper's first star so the gate counts it as this section's;
            // the balcony is past the section's last beat (the gate would silence it there), so its wind is not gated
            // and is faded here instead: only with the listener out on the balcony, times the effects volume.
            hall = Ambience("Hall room tone", "RoHallAmbience", started++, true);
            balcony = Ambience("Balcony wind", "RoBalconyWind", started++, false);
            ready = true;
            Debug.Log($"[천문대 소리] 유성우 {showers.Count} · 플레어 {flares.Count} · 초점 광선 {beams.Count} · 조리개 {gates.Count} · 변광성 {stars.Count}"
                      + $" · 반복음 {loops.Count}개 (궤도 · 톱니 · 대천구 · 행성 · 광선 · 홀 · 발코니)");
        }

        static AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var clip)) return clip;
            clip = Resources.Load<AudioClip>(Folder + name);
            clips[name] = clip;
            if (clip == null && missing.Add(name)) Debug.LogWarning("[천문대 소리] 소리 파일이 없어 재생하지 않습니다: Resources/" + Folder + name);
            return clip;
        }

        static float VolumeOf(string name) => Volume.TryGetValue(name, out var v) ? v : .5f;

        static AudioSource Source(GameObject on, float near, float far, bool gated = true)
        {
            var a = on.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.spatialBlend = 1f;
            a.rolloffMode = AudioRolloffMode.Linear;
            a.minDistance = near;
            a.maxDistance = far;
            a.dopplerLevel = 0f;
            a.volume = 0f;
            if (gated) SectionSoundGate.Gate(a);
            return a;
        }

        Loop AddLoop(Transform on, string name, float near, float far, float pitch, int index, bool gated = true)
        {
            var clip = Clip(name);
            if (clip == null) return null;
            var go = new GameObject("Sound: " + name);
            go.transform.SetParent(on, false);
            var a = Source(go, near, far, gated);
            a.clip = clip;
            a.loop = true;
            a.pitch = pitch;
            a.volume = VolumeOf(name);
            a.time = clip.length * ((index * .37f) % 1f);   // two of a kind never drone in step
            a.Play();
            var loop = new Loop { a = a, volume = a.volume };
            loops.Add(loop);
            return loop;
        }

        AudioSource Ambience(string label, string name, int index, bool gated)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.transform.position = frame.TransformPoint(new Vector3(-2f, 1f, 18f));
            var loop = AddLoop(go.transform, name, 1f, 2f, 1f, index, gated);
            if (loop == null) return null;
            loop.a.spatialBlend = 0f;
            return loop.a;
        }

        // A one-off at a world spot (3D, `near` .. `far` metres), or flat for this PC alone (twoD).
        void Play(string name, Vector3 at, float near, float far, float scale = 1f, bool twoD = false, float pitch = 1f)
        {
            var clip = Clip(name);
            if (clip == null) return;
            var a = voices[nextVoice];
            nextVoice = (nextVoice + 1) % Voices;
            a.transform.position = at;
            a.spatialBlend = twoD ? 0f : 1f;
            a.minDistance = near;
            a.maxDistance = far;
            a.pitch = pitch * Random.Range(.96f, 1.04f);
            a.volume = 1f;
            a.PlayOneShot(clip, VolumeOf(name) * scale);
        }

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

        // Played in Update, so the gate (its LateUpdate) has each pooled voice's new section the same frame.
        void Update()
        {
            if (!ready) return;
            double now = ObstacleClock.Now;
            double step = now - before;
            bool live = !double.IsNaN(before) && step > 0 && step < LongestStep;
            if (live)
            {
                Meteors(before, now);
                Flares(before, now);
                Irises(before, now);
                Petals(step);
                Planet(step);
            }
            if (double.IsNaN(before) || step > 0) before = now;   // a clock that stands still keeps its last time
            Hum(now);
            Rooms();
        }

        void Meteors(double from, double to)
        {
            foreach (var shower in showers)
            {
                if (shower == null || !shower.isActiveAndEnabled || double.IsNaN(shower.StartClock) || shower.LineCount == 0) continue;
                long current = (long)System.Math.Floor((to - shower.StartClock) / shower.Interval);
                for (long n = System.Math.Max(0L, current - 2); n <= current + 2; n++)
                {
                    var m = shower.At(n);
                    double warn = m.Time - shower.Warning, fall = m.Time - shower.Streak;
                    var at = shower.transform.TransformPoint(shower.Point(m));
                    if (from < warn && warn <= to) Play("RoMeteorWarn", at, 4f, 32f);
                    if (from < fall && fall <= to) Play("RoMeteorFall", at + Vector3.up * 6f, 4f, 36f);
                    if (from < m.Time && m.Time <= to) Play("RoMeteorImpact", at, 5f, 45f);
                }
            }
        }

        void Flares(double from, double to)
        {
            foreach (var flare in flares)
            {
                if (flare == null || !flare.isActiveAndEnabled) continue;
                float a = flare.CycleTime(from), b = flare.CycleTime(to);
                var sun = flare.transform.TransformPoint(flare.Sun);
                // the charge is as long as the warning (1.3 s): heard across the globe's floor
                // R742 (user 11-21 "태양 폭발 하기 전 예고음 크게 해"): 4 dB up (.352 -> .56, heard -18 dB) and full farther
                if (Crossed(a, b, flare.Calm)) Play("RoFlareCharge", sun, 13f, 65f);
                // R742 (user 11-21 "대천구 폭발 살짝 크게 해"): the burst 3 dB up (.29 -> .41, heard -15 dB) and full a little farther
                if (Crossed(a, b, flare.Calm + flare.Warn)) Play("RoFlareBurst", sun, 15f, 75f);
            }
        }

        void Irises(double from, double to)
        {
            foreach (var g in gates)
            {
                if (g.leaf == null || !g.leaf.isActiveAndEnabled) continue;
                float a = g.leaf.CycleAt(from), b = g.leaf.CycleAt(to);
                float warn = g.leaf.OpenTime, close = warn + g.leaf.WarnTime, shut = close + g.leaf.CloseTime;
                if (Crossed(a, b, warn)) Play("RoIrisWarn", g.at, 3f, 26f);
                if (Crossed(a, b, close)) Play("RoIrisScrape", g.at, 3f, 28f);
                if (Crossed(a, b, shut)) Play("RoIrisClash", g.at, 4f, 34f);
            }
        }

        void Petals(double step)
        {
            foreach (var star in stars)
            {
                if (star.lead == null || !star.lead.isActiveAndEnabled) continue;
                foreach (var petal in star.petals)
                {
                    if (petal == null) continue;
                    double now = petal.Now;
                    float a = Cycle(now - step, petal.Cycle), b = Cycle(now, petal.Cycle);
                    if (Crossed(a, b, petal.FoldsAt))
                        Play("RoPetalFold", petal.transform.position, 2.5f, 22f, .75f, pitch: .9f + .05f * star.petals.IndexOf(petal));
                    if (petal != star.lead) continue;
                    if (petal.Warning(now) && !petal.Warning(now - step)) Play("RoPetalWarn", star.at, 2.5f, 22f);
                    if (Crossed(a, b, petal.RisesAt)) Play("RoPetalRise", star.at, 3f, 24f);
                }
            }
        }

        void Planet(double step)
        {
            if (planet == null || !planet.isActiveAndEnabled) return;
            double now = planet.Now;
            float cycle = planet.Cycle, half = planet.Dwell + planet.CrossSeconds;
            float a = Cycle(now - step, cycle), b = Cycle(now, cycle);
            if (Crossed(a, b, half) || b < a) Play("RoPlanetDock", planet.transform.position, 4f, 30f);
        }

        void LateUpdate()
        {
            if (!ready || planet == null || roll == null) return;
            // The rumble with how fast it rolls (measured off what is drawn, 1 at its cruising speed).
            float dt = Time.deltaTime;
            var p = planet.transform.position;
            if (dt > 0f)
            {
                float cruise = planet.CruiseSpeed(Vector3.Scale(planet.Travel, planet.transform.parent != null ? planet.transform.parent.lossyScale : Vector3.one).magnitude);
                float speed = cruise > .01f ? Vector3.Distance(p, rollLast) / dt / cruise : 0f;
                rollSpeed = Mathf.Lerp(rollSpeed, Mathf.Clamp01(speed), 1f - Mathf.Exp(-dt * 8f));
            }
            rollLast = p;
            roll.volume = VolumeOf("RoPlanetRoll") * rollSpeed;
            roll.pitch = Mathf.Lerp(.8f, 1.05f, rollSpeed);
        }

        // The beams' hum sits where their light lands (the deck the pawns climb), between the two spots.
        void Hum(double now)
        {
            if (hum == null) return;
            var sum = Vector3.zero;
            int n = 0;
            foreach (var beam in beams)
            {
                if (beam == null || !beam.isActiveAndEnabled) continue;
                sum += beam.transform.TransformPoint(beam.SpotAt(now));
                n++;
            }
            if (n > 0) hum.transform.position = sum / n + Vector3.up;
        }

        // The hall's room tone, crossing to the night wind out on the balcony, by where the listener is.
        void Rooms()
        {
            if (hall == null && balcony == null) return;
            if (ear == null || !ear.isActiveAndEnabled) ear = FindAnyObjectByType<AudioListener>();
            float out_ = 0f;
            if (ear != null)
            {
                var p = frame.InverseTransformPoint(ear.transform.position);
                float d = Vector2.Distance(new Vector2(p.x, p.z), HallMiddle);
                if (p.z > BalconyNorth && p.z < 140f && p.y > 12f && Mathf.Abs(p.x) < 30f)
                    out_ = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BalconyFrom, BalconyTo, d));
            }
            if (hall != null) hall.volume = VolumeOf("RoHallAmbience") * (1f - .7f * out_);
            // not gated (see Start): the effects volume here, and nothing while the loading screen tours the round
            if (balcony != null)
                balcony.volume = LoadingScreenView.Showing ? 0f : VolumeOf("RoBalconyWind") * out_ * GameSettings.Effects;
        }

        // R742: a pawn thrown by a focus beam (every PC, RoyalObservatoryVfxBeamHit); `mine` adds this player's own hit.
        public void BeamStruck(Vector3 body, bool mine)
        {
            Play("RoBeamStrike", body, 3f, 28f);
            if (mine) Play("RoBeamHitMe", body, 1f, 2f, twoD: true);
        }

        // R742: this PC's pawn caught by the solar flare's burn (RoyalObservatoryVfxFlare, once a flare).
        public void FlareCaught(Vector3 pawn) => Play("RoFlareHitMe", pawn, 1f, 2f, twoD: true);
    }
}
