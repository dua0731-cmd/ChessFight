using UnityEngine;
using UnityEngine.Rendering;

namespace ChessFight.Game
{
    // The project still carries URP pipeline assets but no URP package, so nothing
    // renders through them. Every scene borrows the built-in pipeline for the
    // play session and hands the original references back when it ends.
    //
    // This used to live in the lobby bootstrap, which left scenes opened on their
    // own (KingRush, RagdollTest) rendering nothing.
    static class RenderPipelineOverride
    {
        static RenderPipelineAsset previous, previousQuality;
        static bool applied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            if (applied) return;
            previous = GraphicsSettings.defaultRenderPipeline;
            previousQuality = QualitySettings.renderPipeline;
            if (previous == null && previousQuality == null) return;
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
            applied = true;
            // Also raised when the Editor leaves Play mode.
            Application.quitting += Restore;
        }

        static void Restore()
        {
            Application.quitting -= Restore;
            if (!applied) return;
            GraphicsSettings.defaultRenderPipeline = previous;
            QualitySettings.renderPipeline = previousQuality;
            applied = false;
        }
    }
}
