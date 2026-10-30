using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// One burst of small glowing particles each time it is turned on (R159, 10-27): sparks thrown along +Z inside
    /// <see cref="spread"/> (stretched along their way when <see cref="stretch"/> is above 0) or embers drifting up (a
    /// negative <see cref="gravity"/>), from a point or, with <see cref="emitterWidth"/>, from a line along X. A built-in
    /// particle system set up in code, drawn with <see cref="material"/> (VFX_SoftGlowAdd); the count is scaled by
    /// <see cref="VfxCue.Density"/>.
    /// </summary>
    public sealed class VfxSparks : MonoBehaviour
    {
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        public Material material;
        [Min(1)] public int count = 10;
        public Vector2 speed = new Vector2(2f, 5f);
        public Vector2 lifetime = new Vector2(.15f, .35f);
        public Vector2 size = new Vector2(.02f, .04f);
        [Range(0f, 90f), Tooltip("Half-angle of the cone the burst goes out in, around +Z.")]
        public float spread = 35f;
        [Tooltip("0: from a point. Above 0: from a line this long along X (they still go out along +Z).")]
        public float emitterWidth;
        [Tooltip("Gravity multiplier; below 0 they rise.")]
        public float gravity = 1.5f;
        [Tooltip("0: round dots. Above 0: streaks this long per metre a second.")]
        public float stretch = .04f;
        [Tooltip("HDR multiplier on the material's colour (see Docs/VFX §6).")]
        public float intensity = 3f;

        ParticleSystem system;

        void Awake()
        {
            system = gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = .1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            var shape = system.shape;
            if (emitterWidth > 0f)
            {
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(emitterWidth, .02f, .02f);
                shape.randomDirectionAmount = spread / 90f;
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = spread;
                shape.radius = .02f;
            }
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.8f, .4f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var view = GetComponent<ParticleSystemRenderer>();
            view.sharedMaterial = material;
            view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            view.receiveShadows = false;
            if (stretch > 0f)
            {
                view.renderMode = ParticleSystemRenderMode.Stretch;
                view.velocityScale = stretch;
                view.lengthScale = 1f;
            }
            var block = new MaterialPropertyBlock();
            block.SetFloat(IntensityId, intensity);
            view.SetPropertyBlock(block);
        }

        void OnEnable()
        {
            if (system == null) return;
            var emission = system.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, Mathf.RoundToInt(count * VfxCue.Density))) });
            system.Clear(true);
            system.Play(true);
        }
    }
}
