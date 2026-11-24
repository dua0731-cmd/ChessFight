using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using ChessFight.ProtectKing;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R768 (승규 11-22, "다 확인 했고 5 6 시작" - the garden's beat 6, the boat crossing): positions are the builder's, in the garden's
    // frame (this sits on "Plaza sounds", as GardenPlazaSound does; RoyalWaterGardenBuilder.BoatCrossing).
    //  boats     the eight reservoir boats (GardenBoat): water along the hull and a plank creak, looped on each, as loud as
    //            it runs and silent as it sinks under a tunnel (no sound for the dive, R482b) (ReservoirBoat)
    //  decks     a piece coming down on a boat's deck: a hollow wooden thump and a slosh, as loud as it fell
    //            (DeckLand). Every PC follows the pieces it draws over the decks as GardenDrumDip.Online does (the host's
    //            by their velocity, a client's puppets by how they move), so nothing is sent.
    //  tunnels   the falls hanging in the two boat tunnels' mouths (x +-18.75, z 33 .. 48.6): one loop a tunnel that
    //            slides along its mouth to the point nearest the listener (TunnelFall)
    //  spillway  the reservoir pouring 3.45 m into the lake over the south wall west of the quay (x -26 .. -10.4, z 33):
    //            one loop along its crest, the garden's biggest fall (SpillwayFall)
    //  water     the reservoir lapping, one quiet loop at its nearest point (the moat's MoatLap)
    // GardenPlazaSound adds this on waking (the garden is built into the scene, so nothing new has to be built in).
    public sealed class GardenReservoirSound : MonoBehaviour
    {
        const string Folder = "PawnRushGardenSfx/";
        const float TunnelX = 18.75f, TunnelY = 4.3f, MouthZ0 = 33.6f, MouthZ1 = 48f;
        const float SpillX0 = -26f, SpillX1 = -10.4f, SpillZ = 33f, SpillY = 1.2f;
        const float PoolX0 = -18f, PoolX1 = 18f, PoolZ0 = 33.3f, PoolZ1 = 48.3f, PoolY = 3f;
        const float BoatGain = .3f, TunnelGain = .22f, SpillGain = .3f, PoolGain = .35f * .2f;
        const float Heard = 30f;                 // m: past this a boat's deck is not watched
        const float Watch = 3f, OnTop = .45f, OffTop = .65f, Rearm = .5f, MinGap = .15f;   // GardenDrumDip.Online's

        sealed class Track { public Vector3 last, velocity; public float seen; public bool on, fresh; }
        sealed class Boat
        {
            public GardenBoat boat;
            public Collider hull;
            public NearFarSound sound, land;
            public float pitch, lastLand = -1f;
            public readonly Dictionary<ImportedObstacleActor, Track> tracks = new Dictionary<ImportedObstacleActor, Track>();
        }
        sealed class Line { public Vector3 a, b; public Transform at; public NearFarSound sound; public float gain; }

        readonly List<Boat> boats = new List<Boat>();
        readonly List<Line> lines = new List<Line>();
        readonly HashSet<ImportedObstacleActor> seen = new HashSet<ImportedObstacleActor>();
        readonly Collider[] hits = new Collider[64];
        AudioClip landNear, landFar;
        Transform poolAt;
        NearFarSound pool;
        static AudioListener listener;

        void Start()
        {
            AudioClip tunnelNear = Load("TunnelFall"), tunnelFar = Load("TunnelFallFar");
            foreach (float s in new[] { -1f, 1f })
                AddLine("Boat tunnel falls sound", new Vector3(s * TunnelX, TunnelY, MouthZ0), new Vector3(s * TunnelX, TunnelY, MouthZ1),
                    tunnelNear, tunnelFar, TunnelGain, 3f, 18f, 40f);
            AddLine("Spillway sound", new Vector3(SpillX0, SpillY, SpillZ), new Vector3(SpillX1, SpillY, SpillZ),
                Load("SpillwayFall"), Load("SpillwayFallFar"), SpillGain, 4f, 24f, 55f);
            poolAt = Point("Reservoir water sound point", new Vector3(0f, PoolY, (PoolZ0 + PoolZ1) * .5f));
            pool = new NearFarSound(poolAt, "Reservoir water sound", Load("MoatLap"), Load("MoatLapFar"), true)
                { NearFull = 3f, NearEnd = 16f, FarStart = 6f, FarEnd = 35f, FarGain = .6f };

            AudioClip boatNear = Load("ReservoirBoat"), boatFar = Load("ReservoirBoatFar");
            landNear = Load("DeckLand");
            landFar = Load("DeckLandFar");
            var section = GetComponentInParent<PawnRushSection>();
            foreach (var b in (section != null ? section.transform : transform.root).GetComponentsInChildren<GardenBoat>(true))
            {
                var hullAt = b.transform.Find("Hull");
                var hull = hullAt != null ? hullAt.GetComponent<Collider>() : b.GetComponentInChildren<Collider>();
                boats.Add(new Boat
                {
                    boat = b, hull = hull, pitch = .94f + .1f * ((boats.Count * .618034f) % 1f),
                    sound = new NearFarSound(b.transform, "Reservoir boat sound", boatNear, boatFar, true)
                        { NearFull = 3f, NearEnd = 18f, FarStart = 6f, FarEnd = 40f, FarGain = .6f },
                    land = new NearFarSound(b.transform, "Boat deck land sound", null, null, false)
                        { NearFull = 3f, NearEnd = 18f, FarStart = 6f, FarEnd = 30f, FarGain = .6f },
                });
            }
        }

        void AddLine(string name, Vector3 a, Vector3 b, AudioClip near, AudioClip far, float gain, float full, float end, float farEnd)
        {
            var at = Point(name + " point", (a + b) * .5f);
            lines.Add(new Line
            {
                a = a, b = b, at = at, gain = gain,
                sound = new NearFarSound(at, name, near, far, true)
                    { NearFull = full, NearEnd = end, FarStart = full + 3f, FarEnd = farEnd, FarGain = .6f },
            });
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
            foreach (var line in lines)
            {
                if (listener != null)
                {
                    var ab = line.b - line.a;
                    float t = Mathf.Clamp01(Vector3.Dot(l - line.a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                    line.at.localPosition = line.a + ab * t;
                }
                line.sound.Mix(line.gain, 1f);
            }
            if (listener != null)
                poolAt.localPosition = new Vector3(Mathf.Clamp(l.x, PoolX0, PoolX1), PoolY, Mathf.Clamp(l.z, PoolZ0, PoolZ1));
            pool.Mix(PoolGain, 1f);
            float time = Time.time, dt = Time.deltaTime;
            foreach (var b in boats)
            {
                if (b.boat == null) continue;
                // 1 afloat .. 0 half a metre under (the dive under a tunnel), as fast as its lane runs (2.0 .. 3.0 m/s, R680)
                float afloat = 1f - Mathf.Clamp01((float)RoyalWaterGardenRules.BoatDrop(b.boat.X) / .5f);
                float k = Mathf.InverseLerp(1.8f, 3.2f, b.boat.LaneVelocity.magnitude);
                b.sound.Mix(BoatGain * afloat * (.75f + .35f * k), b.pitch * (.95f + .1f * k));
                if (afloat > .9f) Deck(b, time, dt);
            }
        }

        // R768: pieces coming down on the deck (GardenDrumDip.Online.Gather's way of following them, its heights).
        void Deck(Boat b, float t, float dt)
        {
            if (b.hull == null || landNear == null || listener == null
                || (listener.transform.position - b.hull.bounds.center).sqrMagnitude > Heard * Heard) return;
            var box = b.hull.bounds;
            float top = box.max.y;
            seen.Clear();
            int n = Physics.OverlapBoxNonAlloc(new Vector3(box.center.x, top + Watch * .5f, box.center.z),
                new Vector3(box.extents.x + .2f, Watch * .5f, box.extents.z + .2f), hits, Quaternion.identity, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var actor = ImportedObstacleActor.Find(hits[i]);
                if (actor == null || !actor.isActiveAndEnabled || !seen.Add(actor)) continue;
                var p = actor.Position;
                bool simulated = actor.CanReceiveObstacle;
                if (!b.tracks.TryGetValue(actor, out var k) || t - k.seen > Rearm)
                {
                    k = new Track { last = p, velocity = simulated ? actor.Velocity : Vector3.zero, fresh = true };
                    b.tracks[actor] = k;
                }
                else if (simulated) k.velocity = actor.Velocity;
                else if (dt > 1e-4f)
                {
                    k.velocity = Vector3.Lerp(k.velocity, (p - k.last) / dt, 1f - Mathf.Exp(-12f * dt));
                    k.fresh = false;
                }
                k.last = p;
                k.seen = t;
                bool over = p.x > box.min.x - .1f && p.x < box.max.x + .1f && p.z > box.min.z - .1f && p.z < box.max.z + .1f
                    && p.y > top - .3f;
                bool on = over && p.y < top + (k.on ? OffTop : OnTop);
                // A puppet first seen already on it came on unseen: nothing to hear.
                if (on && !k.on && !(k.fresh && !simulated) && t - b.lastLand > MinGap)
                {
                    float fall = Mathf.Clamp(-k.velocity.y, 0f, 9f);
                    if (fall > 1f)
                    {
                        b.lastLand = t;
                        float v = Mathf.InverseLerp(1.5f, 8f, fall);
                        b.land.Shot(landNear, landFar, Mathf.Lerp(.35f, .75f, v), b.pitch * Mathf.Lerp(1.04f, .94f, v) * Random.Range(.97f, 1.03f));
                    }
                }
                k.on = on;
            }
            foreach (var pair in b.tracks)
                if (!seen.Contains(pair.Key)) pair.Value.on = false;
            if (b.tracks.Count > 32) b.tracks.Clear();
        }
    }
}
