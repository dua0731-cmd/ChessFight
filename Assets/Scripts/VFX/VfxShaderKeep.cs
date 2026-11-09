using UnityEngine;

namespace ChessFight.VFX
{
    /// <summary>
    /// The effect shaders the course's code makes materials from by name, kept in every player build
    /// (Resources/VfxShaderKeep). R526 (user 11-09, after the first build test: "빌드 셰이더 누락 고치기"): the grand stair
    /// hall's falling pieces, chandeliers and fans make their effects from Resources/VfxSets/Hazards when the menu
    /// ChessFight/VFX/Hazard/Build has made it, else from these shaders by name. That set was never made in the
    /// repository, and a player build takes a shader in only when something in the build references it (Shader.Find
    /// cannot see one it left out): nothing did, so the build drew none of those effects and logged "셰이더가 없습니다"
    /// 103 times, while the Editor, which has every shader, showed them all. This list references the nine, so every build
    /// takes them in; Find looks them up by name, the list after Shader.Find (the Editor still finds them directly).
    /// Tools/run-tests-linux.sh checks that every ChessFight/VFX shader name runtime code asks for is in the list.
    /// </summary>
    public sealed class VfxShaderKeep : ScriptableObject
    {
        public Shader[] shaders;

        const string ResourcePath = "VfxShaderKeep";
        static VfxShaderKeep loaded;

        /// <summary>The shader of that name: Shader.Find, else the kept list's (null when neither has it).</summary>
        public static Shader Find(string name)
        {
            var shader = Shader.Find(name);
            if (shader != null) return shader;
            if (loaded == null) loaded = Resources.Load<VfxShaderKeep>(ResourcePath);
            if (loaded == null || loaded.shaders == null) return null;
            foreach (var kept in loaded.shaders)
                if (kept != null && kept.name == name) return kept;
            return null;
        }
    }
}
