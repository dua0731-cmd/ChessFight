using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace ChessFight.ProtectKing
{
    public enum RoyalFloorState : byte { Stable, Cracking, Collapsing, Missing, Restoring }

    [RequireComponent(typeof(ObstacleContext))]
    public sealed class CrumblingRoyalFloor : MonoBehaviour
    {
        public ObstacleContext context;
        public Transform Visual;
        public BoxCollider SolidCollider, OccupancyArea, RestoreClearanceArea;
        public GameObject CrackVisual, WarningVisual;
        public Transform VFXPoint;
        public bool Active = true, Repeat = true;
        [Min(.05f)] public float CrackDuration = 1.5f;
        [Min(.05f)] public float CollapseVisualDuration = .35f;
        [Min(.05f)] public float MissingDuration = 4;
        [Min(.05f)] public float RestoreWarningDuration = .8f;
        [Min(0)] public float VisualDrop = 1.2f;
        public LayerMask PlayerLayerMask = ~0;
        public UnityEvent OnCrack = new UnityEvent(), OnCollapse = new UnityEvent(), OnRestored = new UnityEvent();
        public RoyalFloorState State { get; private set; }
        public uint NetworkId { get; private set; }
        public float StateStarted { get; private set; }
        public bool WaitingForClearance { get; private set; }
        static readonly List<CrumblingRoyalFloor> instances = new List<CrumblingRoyalFloor>();
        readonly Collider[] overlaps = new Collider[512];
        Vector3 visualRest;
        float previousTime, disabledAt;
        bool ready, wasDisabled;
        float Clock => context != null ? context.Elapsed : Time.time;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearRegistry() => instances.Clear();
        void Awake()
        {
            if (context == null) context = GetComponent<ObstacleContext>();
            if (Visual == null || SolidCollider == null || OccupancyArea == null || RestoreClearanceArea == null)
            { Debug.LogError("CrumblingRoyalFloor: required references missing.", this); enabled = false; return; }
            visualRest = Visual.localPosition;
            OccupancyArea.enabled = RestoreClearanceArea.enabled = false;
            OccupancyArea.isTrigger = RestoreClearanceArea.isTrigger = true;
            uint hash = 2166136261;
            for (var t = transform; t != null; t = t.parent)
                foreach (char c in t.name + ":" + t.GetSiblingIndex()) hash = (hash ^ c) * 16777619;
            NetworkId = hash; ready = true; previousTime = Clock;
            Present(Clock);
        }
        void OnEnable()
        {
            if (!instances.Contains(this)) instances.Add(this);
            if (ready && wasDisabled && context.Authority)
            { StateStarted += Mathf.Max(0, Clock - disabledAt); previousTime = lastPresentation = Clock; }
            wasDisabled = false;
        }
        void OnDisable()
        {
            instances.Remove(this);
            if (ready) { disabledAt = Clock; wasDisabled = true; }
            if (WarningVisual != null) WarningVisual.SetActive(false);
            // Preserve the solid/missing state: re-enabling must not spawn a floor inside a player.
        }
        void FixedUpdate()
        {
            if (!ready) return;
            float now = Clock;
            bool reset = now < previousTime || (false);
            previousTime = now;
            if (reset) { State = RoyalFloorState.Stable; StateStarted = now; WaitingForClearance = false; }
            if (!Active || !context.Running) { StateStarted += Mathf.Max(0, now - lastPresentation); Present(now); return; }
            if (context.Authority)
            {
                float age = now - StateStarted;
                switch (State)
                {
                    case RoyalFloorState.Stable:
                        if (HasPlayer(OccupancyArea, true)) Transition(RoyalFloorState.Cracking, now);
                        break;
                    case RoyalFloorState.Cracking:
                        if (age >= Mathf.Max(.05f, CrackDuration)) Transition(RoyalFloorState.Collapsing, now);
                        break;
                    case RoyalFloorState.Collapsing:
                        if (age >= Mathf.Max(.05f, CollapseVisualDuration)) Transition(RoyalFloorState.Missing, now);
                        break;
                    case RoyalFloorState.Missing:
                        if (Repeat && age >= Mathf.Max(.05f, MissingDuration)) Transition(RoyalFloorState.Restoring, now);
                        break;
                    case RoyalFloorState.Restoring:
                        WaitingForClearance = HasPlayer(RestoreClearanceArea, false);
                        if (!WaitingForClearance && age >= Mathf.Max(.05f, RestoreWarningDuration)) Transition(RoyalFloorState.Stable, now);
                        break;
                }
            }
            Present(now);
        }
        float lastPresentation;
        void Transition(RoyalFloorState state, float started)
        {
            State = state; StateStarted = started; WaitingForClearance = false;
            if (state == RoyalFloorState.Cracking) OnCrack.Invoke();
            if (state == RoyalFloorState.Collapsing) OnCollapse.Invoke();
            if (state == RoyalFloorState.Stable) OnRestored.Invoke();
        }
        bool HasPlayer(BoxCollider volume, bool standing)
        {
            var scale = volume.transform.lossyScale;
            var half = Vector3.Scale(volume.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            int count = Physics.OverlapBoxNonAlloc(volume.transform.TransformPoint(volume.center), half, overlaps,
                volume.transform.rotation, PlayerLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var motor = ImportedObstacleActor.Find(overlaps[i]);
                if (motor == null || !motor.isActiveAndEnabled || motor.gameObject.scene != gameObject.scene) continue;
                if (!standing) return true;
                if (!motor.CanReceiveObstacle || !motor.Grounded) continue;
                var foot = SolidCollider.transform.InverseTransformPoint(motor.Position) - SolidCollider.center;
                float top = SolidCollider.size.y * .5f;
                if (Mathf.Abs(foot.x) <= SolidCollider.size.x * .5f && Mathf.Abs(foot.z) <= SolidCollider.size.z * .5f &&
                    foot.y >= top - .12f && foot.y <= top + .22f) return true;
            }
            // An overfull clearance query must fail closed, never rebuild through an unseen player.
            return !standing && count == overlaps.Length;
        }
        void Present(float now)
        {
            lastPresentation = now;
            bool solid = State == RoyalFloorState.Stable || State == RoyalFloorState.Cracking;
            SolidCollider.enabled = solid;
            Visual.gameObject.SetActive(solid || State == RoyalFloorState.Collapsing);
            float drop = State == RoyalFloorState.Collapsing ? Mathf.Clamp01((now - StateStarted) / Mathf.Max(.05f, CollapseVisualDuration)) : 0;
            Visual.localPosition = visualRest + Vector3.down * (drop * VisualDrop);
            if (CrackVisual != null) CrackVisual.SetActive(State == RoyalFloorState.Cracking);
            if (WarningVisual != null) WarningVisual.SetActive(Active && isActiveAndEnabled &&
                (State == RoyalFloorState.Cracking || State == RoyalFloorState.Restoring) && Mathf.Repeat(now * 4, 1) < .65f);
        }
        public static void Capture(Scene scene, out uint[] ids, out byte[] states, out float[] started)
        {
            int count = 0;
            foreach (var tile in instances) if (tile != null && tile.ready && tile.gameObject.scene == scene) count++;
            if (count > 64) throw new InvalidOperationException("At most 64 crumbling floor tiles per online scene are supported.");
            ids = new uint[count]; states = new byte[count]; started = new float[count]; int i = 0;
            foreach (var tile in instances)
                if (tile != null && tile.ready && tile.gameObject.scene == scene)
                { ids[i] = tile.NetworkId; states[i] = (byte)tile.State; started[i++] = tile.StateStarted; }
        }
        public static void ApplyRemote(Scene scene, uint[] ids, byte[] states, float[] started)
        {
            foreach (var tile in instances)
            {
                if (tile == null || !tile.ready || tile.gameObject.scene != scene || tile.context.Authority) continue;
                for (int i = 0; i < ids.Length; i++) if (ids[i] == tile.NetworkId)
                {
                    if (tile.State != (RoyalFloorState)states[i]) tile.Transition((RoyalFloorState)states[i], started[i]);
                    else tile.StateStarted = started[i];
                    tile.Present(tile.Clock); break;
                }
            }
        }
        void OnDrawGizmosSelected()
        {
            foreach (var area in new[] { OccupancyArea, RestoreClearanceArea })
            {
                if (area == null) continue;
                Gizmos.color = area == OccupancyArea ? Color.yellow : Color.cyan;
                Gizmos.matrix = area.transform.localToWorldMatrix; Gizmos.DrawWireCube(area.center, area.size);
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
