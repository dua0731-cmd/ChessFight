using UnityEngine;

namespace ChessFight.Gameplay
{
    public sealed class KingRushSkyMotion : Obstacle
    {
        public bool hand;
        public float offset;
        public GameObject shadow;
        Renderer surface;
        Collider[] solids;
        MaterialPropertyBlock tint;
        public static float Cycle(double time, float offset) => (float)((time + offset) % 8 + 8) % 8;
        protected override void Awake()
        { base.Awake(); surface = GetComponent<Renderer>(); solids = GetComponentsInChildren<Collider>(); tint = new MaterialPropertyBlock(); }
        protected override void Evaluate(double time, out Vector3 position, out Quaternion rotation)
        {
            float phaseTime = Cycle(time, offset);
            rotation = StartRotation; position = StartPosition;
            if (hand)
            {
                float across = phaseTime < 1.5f ? -16 : phaseTime < 3.5f ? Mathf.Lerp(-16, 16, (phaseTime - 1.5f) / 2) : 16;
                position += new Vector3(across, phaseTime >= 3.5f ? 10 : 0, 0);
            }
            else if (phaseTime >= 4) position += Vector3.down * Mathf.Min(12, (phaseTime - 4) * (phaseTime - 4) * 8);
            else if (phaseTime >= 3) position += Vector3.up * Mathf.Sin(phaseTime * 40) * .035f;
        }
        protected override void FixedUpdate()
        {
            float cycle = Cycle(ObstacleClock.Now + phase, offset);
            // Hide collision during the instantaneous reset, never sweep a returning part through players.
            foreach (var solid in solids) solid.enabled = cycle >= .15f && cycle < (hand ? 3.5f : 4);
            if (surface != null)
            { tint.Clear(); if (!hand && cycle >= 3 && cycle < 4) tint.SetColor("_Color", Color.red); surface.SetPropertyBlock(tint); }
            if (shadow != null) shadow.SetActive(cycle < 3.5f);
            base.FixedUpdate();
        }
    }
}
