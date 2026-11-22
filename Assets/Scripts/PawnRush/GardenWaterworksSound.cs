using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R767 (승규 11-22, "1 2 3 4 해봐" - the garden's silent spots, beats 2 .. 4): the water that runs on its own and the
    // east lock's gates. Positions are the builder's, in the garden's frame (this sits on "Plaza sounds", as
    // GardenPlazaSound does; RoyalWaterGardenBuilder.Water and .EastLock, water_tiers.py).
    //  weirs   beat 2: the lake falling 1.3 m into the moat over the two weirs on the canal walk's line (weir E x 7.35 ..
    //          46.5, weir W x -46.5 .. -35.6, z -15.5): one loop each that slides along its crest to the point nearest
    //          the listener, like the moat's (WeirFall)
    //  boats   beat 3: the lake's four royal boats (GardenLakeBoat): a bow-wave-and-creak loop on each, as loud as it
    //          goes (LakeBoat)
    //  lock    beat 4: the shallow water running south over the lock floor (x 27 .. 35, z 10 .. 24): one loop held at the
    //          floor's point nearest the listener (LockFlow)
    //  gates   beat 4: the three sluice gates (ObstacleMotion, MotionKind.Gate, 4.5 s): a chain-and-drip haul as one
    //          starts up (SluiceRise, RiseLead early so its clunk is the top), a heavy wooden boom and splash as it lands
    //          (SluiceSlam, SlamLead early so the boom is the landing), and a piece it drops on knocked aside with the
    //          tall spin bar's wooden bonk
    //          (SpinHitTall), once a drop a piece. The gates' times come from the shared obstacle clock, so every PC
    //          hears them on the same beat; the pieces under a gate every PC finds for itself. Nothing sent.
    //  island  R771 (승규 11-22, "섬 분수만 하자"): the two-tier fountain on the lake's island (R765, RoyalWaterGardenBuilder
    //          "Island fountain" at (-18, 0.6, 16), 2.6 m tall): its water bell and four streams splashing into the
    //          bowls, one loop at its middle (IslandFountain)
    // Every water loop is quiet, under the obstacles, like the plaza's (R537: "아주 조금만 들리게").
    // GardenPlazaSound adds this on waking (the garden is built into the scene, so nothing new has to be built in).
    public sealed class GardenWaterworksSound : MonoBehaviour
    {
        const string Folder = "PawnRushGardenSfx/";
        const float WeirZ = -15.5f, WeirY = -1.2f;
        static readonly Vector2[] Weirs = { new Vector2(7.35f, 46.5f), new Vector2(-46.5f, -35.6f) };
        const float LockX0 = 27.5f, LockX1 = 34.5f, LockZ0 = 10.5f, LockZ1 = 23.5f, LockY = 3.85f;
        const float WeirGain = .2f, LockGain = .2f, BoatGain = .3f, IslandGain = .25f;
        static readonly Vector3 IslandFountainAt = new Vector3(-18f, 1.6f, 16f);
        const float RiseGain = .35f, SlamGain = .5f, ShoveGain = .6f;
        const float SlamLead = .03f;    // s from SluiceSlam.wav's start to its loudest 10 ms (waterworks_sounds.py)
        const float RiseLead = .3f;     // SluiceRise.wav's clunk is at 0.9 .. 1.06 s; the gate is up 0.51 s after it leaves
        const float Heard = 30f;        // m: past this nobody looks for pieces under a gate

        sealed class Line { public float x0, x1; public Transform at; public NearFarSound sound; }
        sealed class Boat { public GardenLakeBoat boat; public NearFarSound sound; public float pitch; }
        sealed class Gate
        {
            public ObstacleMotion motion;
            public NearFarSound sound, shove;
            public Transform shoveAt;
            public readonly List<Collider> parts = new List<Collider>();
            public readonly HashSet<ICharacterDriver> struck = new HashSet<ICharacterDriver>();
            public float last = float.NaN, lastAhead = float.NaN, lastRise = float.NaN, pitch;
        }

        readonly List<Line> weirs = new List<Line>();
        readonly List<Boat> boats = new List<Boat>();
        readonly List<Gate> gates = new List<Gate>();
        readonly Collider[] overlaps = new Collider[48];
        Transform lockAt;
        NearFarSound lockFlow, island;
        AudioClip boatNear, boatFar, riseNear, riseFar, slamNear, slamFar, shoveNear, shoveFar;
        static AudioListener listener;

        void Start()
        {
            AudioClip weirNear = Load("WeirFall"), weirFar = Load("WeirFallFar");
            foreach (var w in Weirs)
            {
                var at = Point("Weir sound point", new Vector3((w.x + w.y) * .5f, WeirY, WeirZ));
                weirs.Add(new Line
                {
                    x0 = w.x, x1 = w.y, at = at,
                    sound = new NearFarSound(at, "Weir fall sound", weirNear, weirFar, true)
                        { NearFull = 3f, NearEnd = 18f, FarStart = 6f, FarEnd = 40f, FarGain = .6f },
                });
            }
            lockAt = Point("Lock flow sound point", new Vector3((LockX0 + LockX1) * .5f, LockY, (LockZ0 + LockZ1) * .5f));
            lockFlow = new NearFarSound(lockAt, "Lock flow sound", Load("LockFlow"), Load("LockFlowFar"), true)
                { NearFull = 2f, NearEnd = 14f, FarStart = 5f, FarEnd = 28f, FarGain = .5f };
            // R771: the island fountain, heard from the drums beside the island and faintly further off
            island = new NearFarSound(Point("Island fountain sound point", IslandFountainAt), "Island fountain sound",
                Load("IslandFountain"), Load("IslandFountainFar"), true)
                { NearFull = 3f, NearEnd = 18f, FarStart = 6f, FarEnd = 35f, FarGain = .5f };
            boatNear = Load("LakeBoat");
            boatFar = Load("LakeBoatFar");
            riseNear = Load("SluiceRise");
            riseFar = Load("SluiceRiseFar");
            slamNear = Load("SluiceSlam");
            slamFar = Load("SluiceSlamFar");
            shoveNear = Load("SpinHitTall");
            shoveFar = Load("SpinHitTallFar");
            var section = GetComponentInParent<PawnRushSection>();
            var root = section != null ? section.transform : transform.root;
            foreach (var motion in root.GetComponentsInChildren<ObstacleMotion>(true))
            {
                if (motion.kind != MotionKind.Gate || !motion.name.StartsWith("Sluice gate")) continue;
                var g = new Gate
                {
                    motion = motion, pitch = .95f + .08f * ((gates.Count * .618034f) % 1f),
                    sound = new NearFarSound(motion.transform, "Sluice gate sound", null, null, false)
                        { NearFull = 4f, NearEnd = 22f, FarStart = 6f, FarEnd = 45f, FarGain = .7f },
                };
                g.shoveAt = new GameObject("Sluice shove sound point").transform;
                g.shoveAt.SetParent(motion.transform, false);
                g.shove = new NearFarSound(g.shoveAt, "Sluice shove sound", null, null, false)
                    { NearFull = 4f, NearEnd = 22f, FarStart = 6f, FarEnd = 40f, FarGain = .6f };
                gates.Add(g);
            }
        }

        Transform Point(string name, Vector3 local)
        {
            var at = new GameObject(name).transform;
            at.SetParent(transform, false);
            at.localPosition = local;
            return at;
        }

        static AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>(Folder + name);
            if (clip == null) Debug.LogWarning("[정원] 소리 파일이 없습니다: Resources/" + Folder + name);
            return clip;
        }

        void Update()
        {
            if (listener == null || !listener.isActiveAndEnabled) listener = FindAnyObjectByType<AudioListener>();
            Vector3 l = listener != null ? transform.InverseTransformPoint(listener.transform.position) : Vector3.zero;
            foreach (var w in weirs)
            {
                if (listener != null) w.at.localPosition = new Vector3(Mathf.Clamp(l.x, w.x0, w.x1), WeirY, WeirZ);
                w.sound.Mix(WeirGain, 1f);
            }
            if (listener != null)
                lockAt.localPosition = new Vector3(Mathf.Clamp(l.x, LockX0, LockX1), LockY, Mathf.Clamp(l.z, LockZ0, LockZ1));
            lockFlow.Mix(LockGain, 1f);
            island.Mix(IslandGain, 1f);
            Boats();
            double now = ObstacleClock.Now;
            foreach (var g in gates) Sluice(g, now);
        }

        // The boats register themselves when they wake (GardenLakeBoat.All); a sound each, made the first time it is seen.
        void Boats()
        {
            foreach (var b in GardenLakeBoat.All)
            {
                if (b == null || boats.Exists(x => x.boat == b)) continue;
                boats.Add(new Boat
                {
                    boat = b, pitch = .92f + .1f * ((boats.Count * .618034f) % 1f),
                    sound = new NearFarSound(b.transform, "Lake boat sound", boatNear, boatFar, true)
                        { NearFull = 3f, NearEnd = 18f, FarStart = 6f, FarEnd = 40f, FarGain = .6f },
                });
            }
            foreach (var b in boats)
            {
                if (b.boat == null || !b.boat.isActiveAndEnabled) { b.sound.Mix(0f, b.pitch); continue; }
                float k = Mathf.Clamp01(b.boat.Speed / 1.3f);
                b.sound.Mix(BoatGain * (.5f + .5f * k), b.pitch * (.95f + .05f * k));
            }
        }

        // How far up a gate is (0 down .. 1 up) at `time`: ObstacleMotion.Evaluate's Gate curve.
        static float Raised(ObstacleMotion m, double time)
        {
            float wave = (float)System.Math.Sin((time / Mathf.Max(.5f, m.period) + m.phase) % 1 * System.Math.PI * 2);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-.35f, .35f, wave));
        }

        void Sluice(Gate g, double now)
        {
            if (g.motion == null) return;
            float up = Raised(g.motion, now), ahead = Raised(g.motion, now + SlamLead), rise = Raised(g.motion, now + RiseLead);
            bool fresh = !float.IsNaN(g.last);
            // It will leave the floor RiseLead from now; it will be on the floor SlamLead from now.
            if (fresh && g.lastRise <= .02f && rise > .02f && riseNear != null)
                g.sound.Shot(riseNear, riseFar, RiseGain, g.pitch * Random.Range(.97f, 1.03f));
            if (fresh && g.lastAhead > .02f && ahead <= .02f && slamNear != null)
                g.sound.Shot(slamNear, slamFar, SlamGain, g.pitch * Random.Range(.96f, 1.04f));
            bool falling = fresh && up < g.last && up < .98f;
            if (fresh && g.last <= .02f && up > .02f) g.struck.Clear();   // a new drop to come
            g.last = up;
            g.lastAhead = ahead;
            g.lastRise = rise;
            if (falling) Shoved(g);
        }

        // R767: pieces the dropping gate is on (its own colliders, as the drawing has them now), each heard once a drop.
        void Shoved(Gate g)
        {
            if (shoveNear == null || listener == null
                || (listener.transform.position - g.motion.transform.position).sqrMagnitude > Heard * Heard) return;
            if (g.parts.Count == 0)
                foreach (var c in g.motion.GetComponentsInChildren<Collider>())
                    if (c.enabled && !c.isTrigger) g.parts.Add(c);
            foreach (var part in g.parts)
            {
                if (part == null || !part.enabled) continue;
                var box = part.bounds;
                int n = Physics.OverlapBoxNonAlloc(box.center, box.extents + Vector3.one * .05f, overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var c = overlaps[i];
                    if (g.parts.Contains(c)) continue;
                    var driver = c.GetComponentInParent<ICharacterDriver>();
                    if (driver == null || !g.struck.Add(driver)) continue;
                    g.shoveAt.position = c.bounds.center;
                    g.shove.Shot(shoveNear, shoveFar, ShoveGain, .82f * Random.Range(.95f, 1.05f));
                }
            }
        }
    }
}
