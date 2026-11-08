using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The heat of the king's sword going into the stone (R196, 10-28, E1 of his edge skill; reference
    /// Assets/VFX/Reference/king/E_00_storyboard.png): the cracks the stab breaks into the wall glow gold, the glow seeping
    /// out along them from the crater's rim to <see cref="reach"/> over <see cref="seep"/>, held <see cref="hold"/>, then
    /// cooling away over <see cref="cool"/>; the crater's broken stone glows with it. The game's breach
    /// (SwordFightSkillFx.Breach, the ChessFight/Skill Breach shader on the real wall) reads it through <see cref="Glow"/>
    /// once it has called <see cref="Live"/>. Without that (the workbench and the capture tool) it draws a breach of its own
    /// under the child <see cref="PreviewName"/>: a plain wall face and top (<see cref="StoneName"/>,
    /// <see cref="StoneTopName"/>) with the shader's face and top quads (<see cref="FaceName"/>, <see cref="TopName"/>) over
    /// them. This object sits where the blade crosses the face, +Z out of it.
    /// </summary>
    [DefaultExecutionOrder(120)]
    public sealed class VfxCrackGlow : MonoBehaviour, IVfxSeekable
    {
        public const string PreviewName = "Preview wall", FaceName = "Face", TopName = "Top", StoneName = "Stone face",
            StoneTopName = "Stone top";

        static readonly int OriginId = Shader.PropertyToID("_Origin"), NormalId = Shader.PropertyToID("_Normal"),
            BoxId = Shader.PropertyToID("_Box"), CraterId = Shader.PropertyToID("_Crater"), CracksId = Shader.PropertyToID("_Cracks"),
            GlowId = Shader.PropertyToID("_Glow"), GlowFrontId = Shader.PropertyToID("_GlowFront"), GlowInsideId = Shader.PropertyToID("_GlowInside"),
            BladeOnId = Shader.PropertyToID("_BladeOn"), SurfaceId = Shader.PropertyToID("_Surface"), InsideId = Shader.PropertyToID("_Inside"),
            SkinId = Shader.PropertyToID("_Skin"), HealId = Shader.PropertyToID("_Heal");

        [Header("Glow")]
        [ColorUsage(false, true), Tooltip("The cracks' heat (HDR, Docs/VFX/README.md §6).")]
        public Color colour = new Color(2.4f, 1.56f, .36f);
        [Min(.005f), Tooltip("Seconds it takes to flare up as the blade goes in.")]
        public float rise = .03f;
        [Min(.01f), Tooltip("Seconds the glow takes to seep out along the cracks to Reach.")]
        public float seep = .3f;
        [Min(0f), Tooltip("Seconds it stays whole once it has seeped out.")]
        public float hold = .08f;
        [Min(.01f), Tooltip("Seconds it takes to cool away.")]
        public float cool = .45f;
        [Min(0f), Tooltip("Metres out past the crater's rim the glow runs (the breach's cracks run 0.55).")]
        public float reach = .55f;
        [Range(0f, 1f), Tooltip("Share of the glow the crater's broken stone takes (all of it glowed white-hot, R196).")]
        public float inside = .2f;
        [Min(.01f), Tooltip("R203 (E6): seconds it goes out in when he falls (Quench).")]
        public float quench = .12f;

        [Header("Preview (the workbench and the captures; the game glows its own wall)")]
        [Min(.05f), Tooltip("The crater's radius and depth (m; SwordFightSkillFx.BreachRadius, BreachDepth).")]
        public float previewRadius = .3f, previewDepth = .2f;
        [Min(0f), Tooltip("Metres its cracks run (SwordFightSkillFx.BreachCracks).")]
        public float previewCracks = .55f;
        [Tooltip("Metres from here up to the wall's top and down to its bottom.")]
        public float previewTop = .17f, previewBottom = .6f;
        public float previewSeed = 7.3f;
        [Tooltip("Under the face's skin (the board's wood behind its brass trim).")]
        public Color previewInside = new Color(.45f, .3f, .18f, 1f);
        [Min(0f), Tooltip("How deep the face's skin goes (m; the brass trim's 0.06).")]
        public float previewSkin = .06f;

        Transform preview;
        MeshRenderer face, top;
        float quenchedAt = -1f;
        MaterialPropertyBlock block;
        float age;
        bool live;

        void OnEnable()
        {
            age = 0f;
            quenchedAt = -1f;
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

        /// <summary>The game takes it over: its own breach glows the real wall, so the preview goes.</summary>
        public void Live()
        {
            live = true;
            Draw();
        }

        /// <summary>R203 (E6): he fell, his sword knocked loose - the glow goes out over <see cref="quench"/> from now.</summary>
        public void Quench()
        {
            if (quenchedAt < 0f) quenchedAt = age;
        }

        /// <summary>How hot the cracks are (0..1) and how far out past the rim the glow has run (m),
        /// <paramref name="at"/> seconds after the blade went in.</summary>
        public void Glow(float at, out float strength, out float front)
        {
            float up = Mathf.Clamp01(at / rise), down = 1f - Mathf.Clamp01((at - seep - hold) / cool);
            strength = at < 0f ? 0f : up * down * down;
            if (quenchedAt >= 0f) strength *= 1f - Mathf.Clamp01((age - quenchedAt) / quench);
            float s = Mathf.Clamp01(at / seep);
            front = reach * (1f - (1f - s) * (1f - s));
        }

        static float Out3(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);

        void Draw()
        {
            // Looked up here, not in Awake: a script reload in Play mode clears them (Docs/Environment/PITFALLS.md 53).
            if (preview == null)
            {
                preview = transform.Find(PreviewName);
                if (preview == null) return;
                var f = preview.Find(FaceName);
                var t = preview.Find(TopName);
                face = f != null ? f.GetComponent<MeshRenderer>() : null;
                top = t != null ? t.GetComponent<MeshRenderer>() : null;
            }
            if (preview.gameObject.activeSelf == live) preview.gameObject.SetActive(!live);
            if (live) return;
            if (block == null) block = new MaterialPropertyBlock();
            // The breach the game would make here, grown as the game grows it (3 and 5 frames).
            Vector3 at = transform.position, n = transform.forward;
            n.y = 0f;
            n = n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.forward;
            Glow(age, out float k, out float front);
            block.SetVector(OriginId, at);
            block.SetVector(NormalId, n);
            block.SetVector(BoxId, new Vector4(at.y + previewTop, at.y - previewBottom, 0f, 0f));
            block.SetVector(CraterId, new Vector4(previewRadius, previewDepth, Out3(age / .05f), previewSeed));
            block.SetVector(CracksId, new Vector4(11f, previewCracks * Out3(age / .083f), .008f, .8f));
            block.SetColor(GlowId, new Color(colour.r, colour.g, colour.b, k));
            block.SetFloat(GlowFrontId, front);
            block.SetFloat(GlowInsideId, inside);
            block.SetFloat(BladeOnId, 0f);
            block.SetColor(InsideId, previewInside);
            block.SetFloat(SkinId, previewSkin);
            block.SetFloat(HealId, 0f);
            if (face != null)
            {
                block.SetFloat(SurfaceId, 0f);
                face.SetPropertyBlock(block);
            }
            if (top != null)
            {
                block.SetFloat(SurfaceId, 1f);
                top.SetPropertyBlock(block);
            }
        }
    }
}
