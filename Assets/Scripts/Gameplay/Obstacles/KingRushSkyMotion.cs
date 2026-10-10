using UnityEngine;

namespace ChessFight.Gameplay
{
    // Shared clock, reset collision guard and warning rendering.
    public abstract class KingRushSkyMotion : KinematicObstacle
    {
        [SerializeField] protected float offset;
        Renderer surface;
        Collider[] solids;
        MaterialPropertyBlock tint;
        public void ConfigureOffset(float seconds) => offset = seconds;
        public static float Cycle(double time, float offset) => (float)((time + offset) % 8 + 8) % 8;
        protected abstract float ActiveUntil { get; }
        protected virtual bool WarningAt(float cycle) => false;
        protected virtual void ShowVisuals(float cycle) { }
        protected override void Awake()
        {
            base.Awake(); surface = GetComponent<Renderer>();
            solids = GetComponentsInChildren<Collider>(); tint = new MaterialPropertyBlock();
        }
        protected override void FixedUpdate()
        {
            float cycle = Cycle(ObstacleClock.Now + phase, offset);
            // Never sweep a returning part through players during its reset.
            foreach (var solid in solids) solid.enabled = cycle >= .15f && cycle < ActiveUntil;
            if (surface != null)
            {
                tint.Clear();
                if (WarningAt(cycle)) tint.SetColor("_Color", Color.red);
                surface.SetPropertyBlock(tint);
            }
            ShowVisuals(cycle);
            base.FixedUpdate();
        }
    }
}
