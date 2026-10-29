using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The king's landing foretold while he hangs by his sword (R199, 10-28, E2 of his edge skill; reference
    /// Assets/VFX/Reference/king/E_02_mark.png): on the spot he will land on, a crown of thin gold light draws itself in -
    /// its band, a circle on the board, then its three points up from the band, then the cross on the middle one - each
    /// line a <see cref="VfxPathRibbon"/> child whose path and times the builder saves, the crown's outline under the
    /// child <see cref="CrownName"/> turned to the camera about the up axis so it reads as a crown from every side. A soft
    /// gold glow (the child <see cref="GlowName"/>, VFX_SoftGlowAdd on a quad turned to the camera) breathes where the
    /// sword is buried. This sets the lines' intensity each frame: it breathes too, and when the game says he has landed
    /// (<see cref="Land"/>) it flares and is gone in <see cref="landTime"/> as the seal bursts from it (E4); when he
    /// falls (<see cref="Stop"/>) everything dies away over <see cref="stopTime"/>. Without the game (the workbench, the
    /// capture tool) he lands <see cref="previewLand"/> seconds in.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class VfxCrownMark : MonoBehaviour, IVfxSeekable
    {
        public const string CrownName = "Crown", GlowName = "Hilt glow";
        static readonly int FadeId = Shader.PropertyToID("_Fade"), IntensityId = Shader.PropertyToID("_Intensity");

        [Tooltip("Without the game: seconds until he lands.")]
        [Min(.05f)] public float previewLand = .97f;
        [Tooltip("The lines' brightness swing (share) and how fast it breathes (radians a second) once drawn.")]
        [Range(0f, 1f)] public float breathe = .2f;
        public float breatheRate = 9f;
        [Tooltip("Seconds the lines take to brighten from the first stroke to full.")]
        [Min(0f)] public float brighten = .5f;
        [Tooltip("At the landing: the flare (times their brightness) and the seconds until they are gone.")]
        public float landFlare = 2.2f;
        [Min(.01f)] public float landTime = .1f;
        [Tooltip("When he falls: seconds for it all to die away.")]
        [Min(.01f)] public float stopTime = .25f;
        [Tooltip("In the game: seconds after which it dies away by itself if neither the landing nor the fall came.")]
        [Min(.1f)] public float liveLimit = 1.6f;
        [Tooltip("The glow at the sword: HDR intensity, size (m) and seconds to come up.")]
        public float glowIntensity = 1.4f;
        public float glowSize = .45f;
        [Min(.01f)] public float glowIn = .12f;

        VfxPathRibbon[] lines;
        float[] lineIntensity;
        Transform crown, glow;
        MeshRenderer glowView;
        MaterialPropertyBlock block;
        float age, endAt = -1f;
        bool live, landed;

        void OnEnable()
        {
            age = 0f;
            endAt = -1f;
            landed = false;
            Draw();
        }

        void Update()
        {
            age += Time.deltaTime;
            Draw();
        }

        /// <summary>As it is <paramref name="at"/> seconds after it was turned on (the capture tool; edit mode too).</summary>
        public void Seek(float at)
        {
            age = at;
            Draw();
        }

        /// <summary>The game takes it over: it lasts until <see cref="Land"/> or <see cref="Stop"/>.</summary>
        public void Live() => live = true;

        /// <summary>He has landed on it: it flares and is gone (the seal takes over).</summary>
        public void Land()
        {
            if (endAt >= 0f) return;
            endAt = age;
            landed = true;
        }

        /// <summary>He fell: it dies away.</summary>
        public void Stop()
        {
            if (endAt >= 0f) return;
            endAt = age;
            landed = false;
        }

        /// <summary>Where the sword is buried, for the glow (the prefab keeps a typical place).</summary>
        public void Hilt(Vector3 world)
        {
            if (Ready() && glow != null) glow.position = world;
        }

        bool Ready()
        {
            // Looked up here, not in Awake: a script reload in Play mode clears them (Docs/Environment/PITFALLS.md 53).
            if (lines == null || lines.Length == 0)
            {
                lines = GetComponentsInChildren<VfxPathRibbon>(true);
                lineIntensity = new float[lines.Length];
                for (int i = 0; i < lines.Length; i++) lineIntensity[i] = lines[i].intensity;
            }
            if (crown == null) crown = transform.Find(CrownName);
            if (glow == null)
            {
                glow = transform.Find(GlowName);
                if (glow != null) glowView = glow.GetComponent<MeshRenderer>();
            }
            if (block == null) block = new MaterialPropertyBlock();
            return true;
        }

        /// <summary>The camera this is seen through: the game's, or in the capture tool's preview scene the one there.</summary>
        Camera Eye()
        {
            var cam = Camera.main;
            if (cam != null && cam.gameObject.scene == gameObject.scene) return cam;
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<Camera>();
                if (found != null) return found;
            }
            return cam;
        }

        void Draw()
        {
            if (!Ready()) return;
            if (live && endAt < 0f && age > liveLimit) Stop();
            float end = endAt >= 0f ? endAt : !live ? previewLand : -1f;
            bool landing = endAt >= 0f ? landed : !live;
            // Its brightness: up as it is drawn, breathing, then the flare and gone (or dying away).
            float k = Mathf.Clamp01(age / Mathf.Max(.01f, brighten));
            k = (.45f + .55f * k) * (1f + breathe * Mathf.Sin(age * breatheRate) * k);
            float glowK = Mathf.Clamp01(age / glowIn) * (1f + .35f * Mathf.Sin(age * breatheRate * .8f));
            if (end >= 0f && age >= end)
            {
                float u = (age - end) / (landing ? landTime : stopTime);
                k *= u >= 1f ? 0f : landing ? Mathf.Lerp(landFlare, 0f, u * u) : (1f - u) * (1f - u);
                glowK *= Mathf.Clamp01(1f - (age - end) / stopTime);
            }
            // Set and drawn now, on this clock (they started with it), not a frame late.
            for (int i = 0; i < lines.Length; i++)
                if (lines[i] != null)
                {
                    lines[i].intensity = lineIntensity[i] * k;
                    lines[i].Seek(age);
                }

            var cam = Eye();
            if (cam != null)
            {
                Vector3 to = cam.transform.position - transform.position;
                to.y = 0f;
                if (crown != null && to.sqrMagnitude > 1e-6f) crown.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
                if (glow != null) glow.rotation = cam.transform.rotation;
            }
            if (glowView != null)
            {
                glowView.enabled = glowK > 0f;
                glow.localScale = Vector3.one * glowSize;
                glowView.GetPropertyBlock(block);
                block.SetFloat(FadeId, Mathf.Clamp01(glowK));
                block.SetFloat(IntensityId, glowIntensity * Mathf.Max(1f, glowK));
                glowView.SetPropertyBlock(block);
            }
        }
    }
}
