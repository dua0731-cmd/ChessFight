using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;
namespace ChessFight.ProtectKing
{
    // StateDrivenMover: an offline book cover; the course owns crush presentation and recovery. In a round section
    // (UseSharedClock, R689) the cover is a pure function of the shared clock instead and the section owns the crush.
    public sealed class ClosingBook : RoyalLibraryObstacle
    {
        [SerializeField] Transform cover;
        [SerializeField] Transform recoveryPoint;
        [SerializeField] BoxCollider crushVolume;
        [SerializeField] Renderer warningStrip;
        [SerializeField, Min(.5f)] float openSeconds = 6;
        [SerializeField, Min(.2f)] float warningSeconds = 1;
        [SerializeField, Min(.1f)] float closeSeconds = .65f;
        [SerializeField, Min(.2f)] float closedSeconds = 2;
        [SerializeField, Min(.1f)] float reopenSeconds = 1.3f;
        [SerializeField, Min(.5f)] float respawnSeconds = 2;
        [SerializeField] float phaseSeconds;
        public event Action<GameObject, Transform, float> Crushed;
        public bool Simulating { get; set; }
        readonly Collider[] overlaps = new Collider[128];
        readonly HashSet<ImportedObstacleActor> seen = new HashSet<ImportedObstacleActor>();
        MaterialPropertyBlock tint;
        float clock;
        // seoungtest R689 (the course as a Pawn Rush round section, 승규 "모든 것을 온라인 기준으로 맞춰"): in a round every PC
        // folds the cover from the shared obstacle clock, counted from the round's start (NaN: the offline course's own
        // pausable clock, as before), and only the authority (offline, or the match's host) crushes.
        double sharedOrigin = double.NaN;
        public bool SharedClock => !double.IsNaN(sharedOrigin);
        public Transform RecoveryPoint => recoveryPoint;
        public BoxCollider CrushVolume => crushVolume;
        public float RespawnSeconds => Mathf.Max(.5f, respawnSeconds);
        public void UseSharedClock(double origin) { sharedOrigin = origin; if (SharedClock) { SharedTick(); Apply(); } }
        void SharedTick() => clock = (float)(ObstacleClock.Now - sharedOrigin) + phaseSeconds;
        public void ResetCycle() { clock = phaseSeconds; if (SharedClock) SharedTick(); Apply(); }
        void Awake() { tint = new MaterialPropertyBlock(); ResetCycle(); }
        void Update() { if (SharedClock) { SharedTick(); Apply(); } }
        // Opening remains hazardous until the cover has lifted clear of a standing pawn.
        public static float Fold(float t, float open, float warning, float closing, float shut, float opening)
        {
            float cycle = open + warning + closing + shut + opening;
            t = (t % cycle + cycle) % cycle;
            if (t < open + warning) return 0;
            t -= open + warning;
            if (t < closing) return t / closing;
            t -= closing;
            if (t < shut) return 1;
            return 1 - (t - shut) / opening;
        }
        float Apply()
        {
            float fold = Fold(clock, Mathf.Max(.5f,openSeconds), Mathf.Max(.2f,warningSeconds),
                Mathf.Max(.1f,closeSeconds), Mathf.Max(.2f,closedSeconds), Mathf.Max(.1f,reopenSeconds));
            if (cover != null) cover.localRotation = Quaternion.Euler(0,0,-180 * fold);
            if (warningStrip != null && tint != null)
            {
                float cycle = Mathf.Max(.5f,openSeconds)+Mathf.Max(.2f,warningSeconds)+Mathf.Max(.1f,closeSeconds)+Mathf.Max(.2f,closedSeconds)+Mathf.Max(.1f,reopenSeconds);
                tint.Clear();
                if ((clock % cycle + cycle) % cycle >= Mathf.Max(.5f,openSeconds)) ChessFight.Game.LitMaterials.SetColor(tint,Color.red);
                warningStrip.SetPropertyBlock(tint);
            }
            return fold;
        }
        void FixedUpdate()
        {
            if (SharedClock) SharedTick();
            else
            {
                if (ObstacleClock.Shared || !Simulating) return;
                clock += Time.fixedDeltaTime;
            }
            if (Apply() < .9f || crushVolume == null || recoveryPoint == null) return;
            if (SharedClock && !ObstacleClock.Authority) return;
            seen.Clear();
            var s = crushVolume.transform.lossyScale;
            int count = Physics.OverlapBoxNonAlloc(crushVolume.transform.TransformPoint(crushVolume.center),
                Vector3.Scale(crushVolume.size*.5f,new Vector3(Mathf.Abs(s.x),Mathf.Abs(s.y),Mathf.Abs(s.z))),
                overlaps,crushVolume.transform.rotation,~0,QueryTriggerInteraction.Ignore);
            for (int i=0;i<count;i++)
            {
                var actor=ImportedObstacleActor.Find(overlaps[i]);
                if(actor==null || !actor.CanReceiveObstacle || !seen.Add(actor)) continue;
                Vector3 p=crushVolume.transform.InverseTransformPoint(actor.Position)-crushVolume.center;
                Vector3 h=crushVolume.size*.5f;
                if(Mathf.Abs(p.x)>h.x || Mathf.Abs(p.y)>h.y || Mathf.Abs(p.z)>h.z) continue;
                Crushed?.Invoke(actor.gameObject,recoveryPoint,Mathf.Max(.5f,respawnSeconds));
            }
        }
    }
}
