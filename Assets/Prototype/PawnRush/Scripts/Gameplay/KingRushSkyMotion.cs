using ChessFight.Game;
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
<<<<<<< HEAD:Assets/Scripts/Gameplay/Obstacles/KingRushSkyMotion.cs
            {
                tint.Clear();
                if (WarningAt(cycle)) tint.SetColor("_Color", Color.red);
                surface.SetPropertyBlock(tint);
            }
            ShowVisuals(cycle);
=======
            { tint.Clear(); if (!hand && cycle >= 3 && cycle < 4) LitMaterials.SetColor(tint, Color.red); surface.SetPropertyBlock(tint); }
            if (shadow != null) shadow.SetActive(cycle < 3.5f);
>>>>>>> upstream/main:Assets/Prototype/PawnRush/Scripts/Gameplay/KingRushSkyMotion.cs
            base.FixedUpdate();
        }
    }
}
