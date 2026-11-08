using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    /// <summary>
    /// Materials made in code, for the Universal Render Pipeline the project draws with (since the URP move): URP's
    /// Lit where the Built-in pipeline's Standard shader was used, URP's Unlit for flat colours, URP's particle shader
    /// for additive sprites, and the switches that Standard set through its own property names (_Glossiness, _Mode) set
    /// through Lit's (_Smoothness, _Surface, _Blend). <see cref="Material.color"/> and <see cref="Material.mainTexture"/>
    /// still work (Lit marks _BaseColor and _BaseMap as the main ones); a property block sets
    /// <see cref="SetColor(MaterialPropertyBlock, Color)"/>, which fills both _BaseColor (Lit, Unlit) and _Color (the
    /// project's own shaders).
    /// </summary>
    public static class LitMaterials
    {
        public const string LitName = "Universal Render Pipeline/Lit";
        public const string UnlitName = "Universal Render Pipeline/Unlit";
        public const string ParticlesUnlitName = "Universal Render Pipeline/Particles/Unlit";

        public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        public static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        public static Shader Lit => Shader.Find(LitName);
        public static Shader Unlit => Shader.Find(UnlitName);

        /// <summary>A lit, opaque colour, as the Standard shader drew it (smoothness = Standard's glossiness).</summary>
        public static Material NewLit(Color color, float smoothness = .5f, float metallic = 0f)
        {
            var m = new Material(Lit) { color = color };
            SetSmoothness(m, smoothness);
            m.SetFloat(MetallicId, metallic);
            return m;
        }

        /// <summary>An unlit, opaque colour (was Unlit/Color).</summary>
        public static Material NewUnlit(Color color) => new Material(Unlit) { color = color };

        public static void SetSmoothness(Material m, float smoothness)
        {
            if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, smoothness);
        }

        public static void SetMetallic(Material m, float metallic)
        {
            if (m.HasProperty(MetallicId)) m.SetFloat(MetallicId, metallic);
        }

        /// <summary>Its own light, added on top of its lit colour (Lit's emission, which needs the _EMISSION keyword).</summary>
        public static void SetEmission(Material m, Color emission, bool realtimeGI = false)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor(EmissionId, emission);
            m.globalIlluminationFlags = realtimeGI ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.None;
        }

        /// <summary>No highlight and no reflection of the sky (Lit's switches, as Standard's were).</summary>
        public static void Matte(Material m)
        {
            m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        }

        /// <summary>
        /// Drawn after the solid world and blended over it: alpha-blended, or added (light). Lit, Unlit and the particle
        /// shader share these switches. Set up as URP's own material inspector sets them (so a material asset of the same
        /// kind, Resources/URPVariants, keeps the same shader variant in a build): where the shader can keep its highlight
        /// whole (Lit, Unlit: _BlendModePreserveSpecular), the alpha is applied in the shader (_ALPHAPREMULTIPLY_ON) and
        /// the blend takes the colour as it is; the particle shader blends by its alpha.
        /// </summary>
        public static void MakeTransparent(Material m, bool additive = false, int queue = (int)RenderQueue.Transparent)
        {
            bool preserveSpecular = m.HasProperty("_BlendModePreserveSpecular");
            if (preserveSpecular) m.SetFloat("_BlendModePreserveSpecular", 1f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)(preserveSpecular ? BlendMode.One : BlendMode.SrcAlpha));
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (preserveSpecular) m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            else m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.renderQueue = queue;
        }

        /// <summary>Holes where its texture's alpha is under <paramref name="cutoff"/> (Standard's Cutout mode).</summary>
        public static void MakeCutout(Material m, float cutoff = .5f)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_AlphaToMask", 1f);
            m.SetFloat("_Cutoff", cutoff);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
        }

        /// <summary>An additive, textured sprite material for particles and points (was Legacy Shaders/Particles/Additive).</summary>
        public static Material NewAdditiveParticles(Texture texture, Color tint)
        {
            var shader = Shader.Find(ParticlesUnlitName);
            if (shader == null) return null;
            var m = new Material(shader) { mainTexture = texture, color = tint };
            MakeTransparent(m, additive: true);
            return m;
        }

        /// <summary>A colour through a property block, for URP's shaders (_BaseColor) and the project's own (_Color).</summary>
        public static void SetColor(MaterialPropertyBlock block, Color color)
        {
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
        }
    }
}
