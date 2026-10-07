using UnityEngine;

namespace ChessFight.RagdollLab
{
    /// <summary>
    /// Bloom on a camera of the skill test scene (R79): the skill effects are drawn brighter than white (HDR) and
    /// this makes them glow, spilling their colour around them. The project uses the built-in render pipeline with
    /// no post-process package, so it is a hand-made one (Resources/PawnRushSkillFx/SkillBloom.shader): light above
    /// <see cref="threshold"/> is blurred down a chain of half-size copies and back up and added over the picture.
    /// The lit test floor measures up to 1.7 (mean 0.9), so the threshold sits above it and only the effects' hot
    /// cores bloom (a first try at 1.15 washed the whole picture white). The test bed adds it; nothing else does.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PawnRushSkillBloom : MonoBehaviour
    {
        public float threshold = 2.2f;
        [Range(0f, 1f)] public float softKnee = 0.3f;
        public float intensity = 1f;
        [Range(1, 8)] public int steps = 6;

        static Material material;
        readonly RenderTexture[] copies = new RenderTexture[8];

        static Material Material
        {
            get
            {
                if (material != null) return material;
                var shader = Shader.Find("Hidden/ChessFight/Skill Bloom");
                if (shader == null) shader = Resources.Load<Shader>("PawnRushSkillFx/SkillBloom");
                if (shader == null || !shader.isSupported) return null;
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                return material;
            }
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var m = Material;
            if (m == null || intensity <= 0f)
            {
                Graphics.Blit(source, destination);
                return;
            }
            float knee = threshold * softKnee + 1e-5f;
            m.SetVector("_Filter", new Vector4(threshold, threshold - knee, 2f * knee, 0.25f / knee));
            m.SetFloat("_Intensity", intensity);

            int width = source.width / 2, height = source.height / 2;
            var format = RenderTextureFormat.DefaultHDR;
            var current = copies[0] = RenderTexture.GetTemporary(width, height, 0, format);
            Graphics.Blit(source, current, m, 0);
            int i = 1;
            for (; i < steps; i++)
            {
                width /= 2;
                height /= 2;
                if (height < 2) break;
                copies[i] = RenderTexture.GetTemporary(width, height, 0, format);
                Graphics.Blit(current, copies[i], m, 1);
                current = copies[i];
            }
            for (i -= 2; i >= 0; i--)
            {
                Graphics.Blit(current, copies[i], m, 2);
                RenderTexture.ReleaseTemporary(current);
                copies[i + 1] = null;
                current = copies[i];
            }
            m.SetTexture("_SourceTex", source);
            Graphics.Blit(current, destination, m, 3);
            RenderTexture.ReleaseTemporary(current);
            copies[0] = null;
        }
    }
}
