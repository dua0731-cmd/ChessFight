using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if CF_URP
using UnityEngine.Rendering.Universal;
#endif

namespace ChessFight.Game
{
    /// <summary>
    /// The graphics options that live on the render pipeline asset under URP (shadow distance and resolution, MSAA):
    /// QualitySettings.shadows / shadowDistance / antiAliasing do nothing there. <see cref="Writable"/> is the asset
    /// in use, to change: in a build the asset itself, in the Editor a copy for the play session, so the project's
    /// own asset is not changed on disk by a settings window or a scene (the copies are handed back when Play ends).
    /// Without the URP package (the Linux compile check) every call does nothing.
    /// </summary>
    public static class PipelineSettings
    {
        /// <summary>Shadows: 0 off, 1 low, 2 medium, 3 high (the settings window's steps).</summary>
        public static void SetShadows(int step)
        {
#if CF_URP
            var urp = Writable();
            if (urp == null) return;
            step = Mathf.Clamp(step, 0, 3);
            urp.shadowDistance = new[] { 0f, 25f, 50f, 90f }[step];
            urp.mainLightShadowmapResolution = step <= 1 ? 1024 : step == 2 ? 2048 : 4096;
#endif
        }

        /// <summary>Multisampling: 1, 2, 4 or 8 samples.</summary>
        public static void SetMsaa(int samples)
        {
#if CF_URP
            var urp = Writable();
            if (urp != null) urp.msaaSampleCount = samples;
#endif
        }

        /// <summary>R792 (settings → 렌더 해상도): the render scale of the asset in use (1 without URP).</summary>
        public static float RenderScale
        {
            get
            {
#if CF_URP
                var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (urp != null) return urp.renderScale;
#endif
                return 1f;
            }
        }

#if CF_URP
        // Each asset's own render scale and upscaling filter (by the project asset's name, a play copy's too), taken
        // before the first change, for RestoreRenderScale.
        static readonly Dictionary<string, KeyValuePair<float, UpscalingFilterSelection>> scaleOriginals =
            new Dictionary<string, KeyValuePair<float, UpscalingFilterSelection>>();

        static string AssetKey(Object asset)
        {
            const string play = " (play)";
            string name = asset.name;
            return name.EndsWith(play) ? name.Substring(0, name.Length - play.Length) : name;
        }
#endif

        /// <summary>R792: draw the 3D picture at this share of the screen's pixels and bring it up with FSR (AMD FidelityFX
        /// Super Resolution 1, URP's own) below 100 %. The UI is drawn after, at the screen's own size.</summary>
        public static void SetRenderScale(float scale)
        {
#if CF_URP
            var urp = Writable();
            if (urp == null) return;
            string key = AssetKey(urp);
            if (!scaleOriginals.ContainsKey(key))
                scaleOriginals[key] = new KeyValuePair<float, UpscalingFilterSelection>(urp.renderScale, urp.upscalingFilter);
            scale = Mathf.Clamp(scale, .1f, 1f);
            urp.renderScale = scale;
            urp.upscalingFilter = scale < .999f ? UpscalingFilterSelection.FSR : scaleOriginals[key].Value;
#endif
        }

        /// <summary>R792: the asset in use back to its own render scale and filter (the settings' "기본값").</summary>
        public static void RestoreRenderScale()
        {
#if CF_URP
            var urp = Writable();
            if (urp == null || !scaleOriginals.TryGetValue(AssetKey(urp), out var own)) return;
            urp.renderScale = own.Key;
            urp.upscalingFilter = own.Value;
#endif
        }

        /// <summary>
        /// The intensity to give a point light of <paramref name="range"/> for it to light what is round it about as the
        /// Built-in pipeline lit it at <paramref name="intensity"/>: the Built-in light faded as 1 / (1 + 25 (d / range)^2),
        /// URP's fades as 1 / d^2 (much brighter close by, dimmer far off), so the two are matched a quarter of the range
        /// out (0.39 there).
        /// </summary>
        public static float PointIntensity(float intensity, float range)
        {
            if (GraphicsSettings.currentRenderPipeline == null) return intensity;
            float d = range * .25f;
            return intensity * .39f * d * d;
        }

        /// <summary>
        /// Light layers (R157): rendering layer 1, "Pieces", is the chess pieces' (every RagdollPawn marks its renderers
        /// with it). A light given only that layer lights the pieces and not the floor round them — the skills' glow
        /// lights, so a flash colours the pieces it reaches without a bright pool on the board. URP's rendering layers are
        /// on in the URP assets.
        /// </summary>
        public const uint PiecesLayer = 1u << 1;

        public static void MarkPiece(GameObject piece)
        {
            foreach (var r in piece.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask |= PiecesLayer;
        }

        public static void LightPiecesOnly(Light light) => light.renderingLayerMask = (int)PiecesLayer;

        /// <summary>How far shadows reach (m), or a negative number when there is no URP asset.</summary>
        public static float ShadowDistance
        {
            get
            {
#if CF_URP
                var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (urp != null) return urp.shadowDistance;
#endif
                return QualitySettings.shadowDistance;
            }
            set
            {
#if CF_URP
                var urp = Writable();
                if (urp != null)
                {
                    urp.shadowDistance = value;
                    return;
                }
#endif
                QualitySettings.shadowDistance = value;
            }
        }

#if CF_URP
        // Quality level -> the project's asset it had, while a play-session copy stands in for it (Editor only).
        static readonly Dictionary<int, RenderPipelineAsset> replaced = new Dictionary<int, RenderPipelineAsset>();
        static readonly HashSet<RenderPipelineAsset> copies = new HashSet<RenderPipelineAsset>();

        public static UniversalRenderPipelineAsset Writable()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null || !Application.isEditor || copies.Contains(urp)) return urp;
            var copy = Object.Instantiate(urp);
            copy.name = urp.name + " (play)";
            copies.Add(copy);
            int level = QualitySettings.GetQualityLevel();
            if (!replaced.ContainsKey(level)) replaced[level] = QualitySettings.renderPipeline;
            if (replaced.Count == 1) Application.quitting += HandBack;
            QualitySettings.renderPipeline = copy;
            return copy;
        }

        // Also raised when the Editor leaves Play mode.
        static void HandBack()
        {
            Application.quitting -= HandBack;
            int current = QualitySettings.GetQualityLevel();
            foreach (var pair in replaced)
            {
                QualitySettings.SetQualityLevel(pair.Key, false);
                QualitySettings.renderPipeline = pair.Value;
            }
            QualitySettings.SetQualityLevel(current, false);
            replaced.Clear();
            foreach (var copy in copies)
                if (copy != null) Object.Destroy(copy);
            copies.Clear();
        }
#endif
    }
}
