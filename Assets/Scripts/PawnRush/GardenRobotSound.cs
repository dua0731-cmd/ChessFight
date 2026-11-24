using System.Collections.Generic;
using ChessFight.Gameplay;
using ChessFight.Network;
using UnityEngine;

namespace ChessFight.PawnRush
{
    // R768 (승규 11-22, "다 확인 했고 5 6 시작" - the garden's beat 5): a dyke robot's sounds, on its own time on the shared obstacle
    // clock (RobotSweep.Now, RoyalWaterGardenRules.RobotPhase / RobotAngle), so every PC hears them on the same beat.
    //  wind   the cycle starting: the axle winching the arm back to the north, a creak and a heavy chain (RobotWind)
    //  swoosh the ball passing its lowest, on the sweep (full) and on the slower return (softer, lower): one deep
    //         whoosh at the hand, SwooshLead early so its loudest moment is the bottom of the swing (RobotSwoosh)
    //  stop   the arm brought up hard out over the water, and settling back at rest (softer): a dull clunk at the
    //         axle (RobotStop)
    //  hit    a piece the ball knocks away: a heavy thud where it is struck, once a pass a piece (RobotHit). The knock is
    //         the authority's (RobotSweep.FixedUpdate); every PC looks for the pieces it draws in the same reach.
    // waterworks_sounds.py makes the files (R768). GardenPlazaSound adds this to each RobotSweep on waking.
    [RequireComponent(typeof(RobotSweep))]
    public sealed class GardenRobotSound : MonoBehaviour
    {
        const string Folder = "PawnRushGardenSfx/";
        const float SwooshLead = .19f;  // s from RobotSwoosh.wav's start to its loudest 10 ms (waterworks_sounds.py)
        const float StopLead = .02f;    // the same for RobotStop.wav
        const float WindGain = .45f, SwooshGain = .7f, BackGain = .45f, StopGain = .55f, SettleGain = .3f, HitGain = .75f;
        const float Heard = 35f;        // m: past this nobody looks for pieces at the ball

        RobotSweep sweep;
        NearFarSound axle, ball, hit;
        Transform hitAt;
        AudioClip windNear, windFar, swooshNear, swooshFar, stopNear, stopFar, hitNear, hitFar;
        double lastC = double.NaN, lastAngle = double.NaN;
        long lastCycle;
        readonly Collider[] overlaps = new Collider[32];
        readonly HashSet<ICharacterDriver> struck = new HashSet<ICharacterDriver>();
        long struckPass = long.MinValue;
        float pitch;
        static AudioListener listener;

        void Start()
        {
            sweep = GetComponent<RobotSweep>();
            windNear = Load("RobotWind");
            windFar = Load("RobotWindFar");
            swooshNear = Load("RobotSwoosh");
            swooshFar = Load("RobotSwooshFar");
            stopNear = Load("RobotStop");
            stopFar = Load("RobotStopFar");
            hitNear = Load("RobotHit");
            hitFar = Load("RobotHitFar");
            // The axle is 5.8 m over the dyke: full volume out to where a runner below it stands.
            axle = new NearFarSound(transform, "Robot axle sound", null, null, false)
                { NearFull = 7f, NearEnd = 26f, FarStart = 9f, FarEnd = 50f, FarGain = .7f };
            ball = new NearFarSound(sweep.Hand != null ? sweep.Hand : transform, "Robot ball sound", null, null, false)
                { NearFull = 4f, NearEnd = 22f, FarStart = 7f, FarEnd = 45f, FarGain = .6f };
            hitAt = new GameObject("Robot hit sound point").transform;
            hitAt.SetParent(transform, false);
            hit = new NearFarSound(hitAt, "Robot hit sound", null, null, false)
                { NearFull = 4f, NearEnd = 24f, FarStart = 7f, FarEnd = 45f, FarGain = .6f };
            pitch = transform.localPosition.x > 20f ? 1f : .95f;   // robot 1 (x 26) and 2 (x 13) do not sound like one sample
        }

        static AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>(Folder + name);
            if (clip == null) Debug.LogWarning("[정원] 소리 파일이 없습니다: Resources/" + Folder + name);
            return clip;
        }

        void Update()
        {
            double now = sweep.Now, cycleLength = RoyalWaterGardenRules.RobotCycle;
            double c = now % cycleLength;
            if (c < 0) c += cycleLength;
            long cycle = RoyalWaterGardenRules.RobotCycleIndex(now);
            double angle = RoyalWaterGardenRules.RobotAngle(now + SwooshLead);
            bool fresh = !double.IsNaN(lastC);
            if (fresh && Crossed(0, c, cycle) && windNear != null)
                axle.Shot(windNear, windFar, WindGain, pitch * Random.Range(.97f, 1.03f));
            if (fresh && Crossed(RoyalWaterGardenRules.SweepUntil - StopLead, c, cycle) && stopNear != null)
                axle.Shot(stopNear, stopFar, StopGain, pitch * Random.Range(.96f, 1.04f));
            if (fresh && Crossed(RoyalWaterGardenRules.BackUntil - StopLead, c, cycle) && stopNear != null)
                axle.Shot(stopNear, stopFar, SettleGain, pitch * 1.08f * Random.Range(.96f, 1.04f));
            // The ball will be at its lowest SwooshLead from now: down on the sweep (falling angle), up on the return.
            if (!double.IsNaN(lastAngle) && System.Math.Sign(angle) != System.Math.Sign(lastAngle) && swooshNear != null)
            {
                bool sweeping = angle < lastAngle;
                ball.Shot(swooshNear, swooshFar, sweeping ? SwooshGain : BackGain,
                    pitch * (sweeping ? 1f : .88f) * Random.Range(.97f, 1.03f));
            }
            lastC = c;
            lastCycle = cycle;
            lastAngle = angle;
            Watch(now);
        }

        // Once per cycle, when the cycle's time crosses the moment; a late frame inside 0.25 s still sounds, later not.
        bool Crossed(double moment, double c, long cycle) =>
            c >= moment && c < moment + .25 && (cycle != lastCycle || lastC < moment);

        // R768: pieces at the ball while it crosses low (RobotSweep.FixedUpdate's reach), each heard once a pass.
        void Watch(double now)
        {
            if (hitNear == null || sweep.Hand == null) return;
            var swing = RoyalWaterGardenRules.RobotPhase(now, out _);
            bool sweeping = swing == RoyalWaterGardenRules.Swing.Sweep;
            if (!sweeping && swing != RoyalWaterGardenRules.Swing.Back) return;
            if (Mathf.Abs((float)RoyalWaterGardenRules.RobotAngle(now)) > sweep.LowAngle) return;
            if (listener == null || !listener.isActiveAndEnabled) listener = FindAnyObjectByType<AudioListener>();
            var at = sweep.Hand.position;
            if (listener == null || (listener.transform.position - at).sqrMagnitude > Heard * Heard) return;
            long pass = RoyalWaterGardenRules.RobotCycleIndex(now) * 2 + (sweeping ? 0 : 1);
            if (pass != struckPass) { struck.Clear(); struckPass = pass; }
            int n = Physics.OverlapSphereNonAlloc(at, sweep.HandReach, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var driver = overlaps[i].GetComponentInParent<ICharacterDriver>();
                if (driver == null || !struck.Add(driver)) continue;
                hitAt.position = overlaps[i].ClosestPoint(at);
                hit.Shot(hitNear, hitFar, sweeping ? HitGain : HitGain * .8f, pitch * Random.Range(.94f, 1.04f));
            }
        }
    }
}
