using System;
using System.Collections.Generic;
using ChessFight.Gameplay;
using UnityEngine;

namespace ChessFight.ProtectKing
{
    // A briefing map becomes a timed barrage. Authored cells are row-major; each row
    // always leaves one column safe. The pattern and telegraph use the shared clock.
    public sealed class TacticalMapBarrage : Obstacle
    {
        [SerializeField] ObstacleContext context;
        [SerializeField, Range(2, 8)] int columns = 3;
        [SerializeField] BoxCollider[] dangerAreas;
        [SerializeField] GameObject[] warningVisuals;
        [SerializeField] GameObject[] safeVisuals;
        [SerializeField] GameObject[] strikeVisuals;
        [SerializeField, Min(.5f)] float warningSeconds = 2.2f;
        [SerializeField, Min(.1f)] float strikeSeconds = .3f;
        [SerializeField, Min(.1f)] float restSeconds = 1;
        [SerializeField] float phaseOffset;
        [SerializeField, Min(0)] float pushForce = 10;
        [SerializeField, Min(0)] float upwardForce = 2;
        [SerializeField] Vector3 localPushDirection = Vector3.back;
        readonly Collider[] overlaps = new Collider[512];
        readonly HashSet<ImportedObstacleActor> struck = new HashSet<ImportedObstacleActor>();
        double lastCycle = double.NaN;
        int ColumnCount => Mathf.Clamp(columns, 2, 8);
        float Rest => Mathf.Max(.1f, restSeconds);
        float Warning => Mathf.Max(.5f, warningSeconds);
        float Strike => Mathf.Max(.1f, strikeSeconds);
        public float Period => Rest + Warning + Strike;
        double Cycle(double time) => Math.Floor((time + phaseOffset) / Period);
        float Phase(double time) => (float)(((time + phaseOffset) % Period + Period) % Period);

        public int SafeColumn(double time, int row)
        {
            // Reduce before casting so a long-running shared clock cannot overflow an int.
            double column = (Cycle(time) % ColumnCount + row % ColumnCount) % ColumnCount;
            return (int)((column + ColumnCount) % ColumnCount);
        }

        void Awake()
        {
            if (context == null) context = GetComponentInParent<ObstacleContext>();
            bool valid = dangerAreas != null && dangerAreas.Length > 0 && dangerAreas.Length % ColumnCount == 0 &&
                warningVisuals != null && warningVisuals.Length == dangerAreas.Length;
            if (valid)
                for (int i = 0; i < dangerAreas.Length; i++)
                    if (dangerAreas[i] == null || warningVisuals[i] == null) { valid = false; break; }
            if (!valid)
            {
                Debug.LogError("TacticalMapBarrage requires complete rows of danger areas and matching warning visuals.", this);
                enabled = false;
            }
            ClearVisuals();
        }

        void Update()
        {
            if (context != null && !context.Running) { ClearVisuals(); return; }
            double now = SharedTime;
            float phase = Phase(now);
            bool showing = phase >= Rest;
            bool impact = phase >= Rest + Warning;
            for (int i = 0; i < dangerAreas.Length; i++)
            {
                bool safe = i % ColumnCount == SafeColumn(now, i / ColumnCount);
                Show(warningVisuals, i, showing && !safe);
                Show(safeVisuals, i, showing && safe);
                Show(strikeVisuals, i, impact && !safe);
            }
        }

        void FixedUpdate()
        {
            double now = SharedTime, cycle = Cycle(now);
            if (cycle != lastCycle) { struck.Clear(); lastCycle = cycle; }
            if ((context != null && (!context.Running || !context.Authority)) || Phase(now) < Rest + Warning) return;
            Vector3 impulse = transform.TransformDirection(localPushDirection.normalized) * Mathf.Max(0, pushForce)
                + Vector3.up * Mathf.Max(0, upwardForce);
            for (int cell = 0; cell < dangerAreas.Length; cell++)
            {
                if (cell % ColumnCount == SafeColumn(now, cell / ColumnCount)) continue;
                var area = dangerAreas[cell];
                if (area == null || !area.gameObject.activeInHierarchy) continue;
                var scale = area.transform.lossyScale;
                int count = Physics.OverlapBoxNonAlloc(area.transform.TransformPoint(area.center),
                    Vector3.Scale(area.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))),
                    overlaps, area.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var actor = ImportedObstacleActor.Find(overlaps[i]);
                    if (actor == null || !actor.CanReceiveObstacle || !ContainsFootprint(area, actor.Position) || !struck.Add(actor)) continue;
                    actor.AddObstacleImpulse(impulse);
                }
            }
        }

        static bool ContainsFootprint(BoxCollider area, Vector3 worldPosition)
        {
            // A limb reaching across a tile edge must not hit a character standing in
            // the safe cell. OverlapBox owns the vertical test: a fallen ragdoll's
            // synthetic LaunchFrom point can lie below the floor even while its body rests on it.
            Vector3 p = area.transform.InverseTransformPoint(worldPosition) - area.center;
            Vector3 half = area.size * .5f;
            return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.z) <= half.z;
        }

        static void Show(GameObject[] visuals, int index, bool active)
        {
            if (visuals != null && index < visuals.Length && visuals[index] != null && visuals[index].activeSelf != active)
                visuals[index].SetActive(active);
        }
        void ClearVisuals()
        {
            if (warningVisuals != null) for (int i = 0; i < warningVisuals.Length; i++) Show(warningVisuals, i, false);
            if (safeVisuals != null) for (int i = 0; i < safeVisuals.Length; i++) Show(safeVisuals, i, false);
            if (strikeVisuals != null) for (int i = 0; i < strikeVisuals.Length; i++) Show(strikeVisuals, i, false);
        }
        void OnDisable() { struck.Clear(); lastCycle = double.NaN; ClearVisuals(); }
    }
}
