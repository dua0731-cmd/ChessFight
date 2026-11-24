#if CF_URP
using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ChessFight.Game
{
    /// <summary>
    /// JY-gfx (2026-11-24): light shafts, ported from the render lab (JY-look fcb0d582; JY: "저때까지의 작업이랑 그래픽
    /// 개선한 방법들은 마음에 들었어. 다른 맵들도 저런식으로 해도 될꺼같아"). Sunlight scattered by the air, so the sun's
    /// beams through windows, arches and lattices show as shafts and every place gets depth. URP 17 has no volumetric
    /// light; this is ours. A volume sets how much (0 = none); GameGrade's volume carries the round's.
    /// </summary>
    [Serializable, VolumeComponentMenu("ChessFight/Gfx Light Shafts")]
    public sealed class GfxLightShafts : VolumeComponent
    {
        [Tooltip("How bright the scattered sunlight is (0 = off).")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 6f);
        [Tooltip("How thick the air is: more density, stronger and shorter shafts.")]
        public ClampedFloatParameter density = new ClampedFloatParameter(.03f, 0f, .4f);
        [Tooltip("How much the light scatters forward, toward a camera looking at the sun (0 = evenly).")]
        public ClampedFloatParameter anisotropy = new ClampedFloatParameter(.55f, 0f, .95f);
        [Tooltip("The shafts' colour over the sun's.")]
        public ColorParameter tint = new ColorParameter(Color.white);
        [Tooltip("How far from the camera the air is marched (m).")]
        public ClampedFloatParameter maxDistance = new ClampedFloatParameter(80f, 5f, 300f);

        public bool IsActive() => intensity.value > 0f;
    }

    /// <summary>
    /// The light shafts' pass (Render Graph). At half resolution it walks each pixel's view ray through the air (32
    /// steps from a per-pixel noise offset), adds the sunlight that reaches each step (the main light's shadow map) with
    /// a forward-scattering phase, blurs the result in two passes and adds it onto the camera's colour before post
    /// processing, so bloom and tonemapping treat it as light. Shader: Resources/Gfx/GfxLightShafts.
    /// </summary>
    sealed class GfxLightShaftsPass : ScriptableRenderPass
    {
        const int Steps = 32;
        static readonly int ParamsId = Shader.PropertyToID("_Shafts");
        static readonly int ColourId = Shader.PropertyToID("_ShaftsColor");
        static readonly int StepsId = Shader.PropertyToID("_ShaftsSteps");
        static readonly int FrameId = Shader.PropertyToID("_ShaftsFrame");
        static readonly int TexelId = Shader.PropertyToID("_ShaftsTexel");

        readonly Material material;

        public bool Ready => material != null;

        public GfxLightShaftsPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            var shader = Resources.Load<Shader>("Gfx/GfxLightShafts");
            if (shader != null && shader.isSupported) material = CoreUtils.CreateEngineMaterial(shader);
            else Debug.LogWarning("[GameShafts] the light shafts' shader (Resources/Gfx/GfxLightShafts) is missing or unsupported");
            ConfigureInput(ScriptableRenderPassInput.Depth);
            requiresIntermediateTexture = true;
        }

        public void Dispose() => CoreUtils.Destroy(material);

        sealed class PassData
        {
            public Material material;
            public TextureHandle source;
            public int pass;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var settings = VolumeManager.instance.stack.GetComponent<GfxLightShafts>();
            if (material == null || settings == null || !settings.IsActive()) return;
            var resources = frameData.Get<UniversalResourceData>();
            var camera = frameData.Get<UniversalCameraData>();
            if (resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid() || !resources.mainShadowsTexture.IsValid()) return;

            var desc = camera.cameraTargetDescriptor;
            desc.width = Mathf.Max(1, desc.width / 2);
            desc.height = Mathf.Max(1, desc.height / 2);
            desc.msaaSamples = 1;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            var a = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_GfxShaftsA", false);
            var b = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_GfxShaftsB", false);

            material.SetVector(ParamsId, new Vector4(settings.intensity.value, settings.density.value, settings.anisotropy.value, settings.maxDistance.value));
            material.SetColor(ColourId, settings.tint.value);
            material.SetFloat(StepsId, Steps);
            material.SetFloat(FrameId, Time.frameCount % 64);
            material.SetVector(TexelId, new Vector4(1f / desc.width, 1f / desc.height, desc.width, desc.height));

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Gfx light shafts: march", out var data))
            {
                data.material = material;
                data.pass = 0;
                builder.UseTexture(resources.cameraDepthTexture);
                builder.UseTexture(resources.mainShadowsTexture);
                builder.SetRenderAttachment(a, 0, AccessFlags.WriteAll);
                builder.SetRenderFunc((PassData d, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, new Vector4(1f, 1f, 0f, 0f), d.material, d.pass));
            }
            Blur(renderGraph, a, b, 1);
            Blur(renderGraph, b, a, 2);
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Gfx light shafts: add", out var data))
            {
                data.material = material;
                data.source = a;
                data.pass = 3;
                builder.UseTexture(a);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc((PassData d, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, d.pass));
            }
        }

        void Blur(RenderGraph renderGraph, TextureHandle from, TextureHandle to, int pass)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(pass == 1 ? "Gfx light shafts: blur across" : "Gfx light shafts: blur down", out var data))
            {
                data.material = material;
                data.source = from;
                data.pass = pass;
                builder.UseTexture(from);
                builder.SetRenderAttachment(to, 0, AccessFlags.WriteAll);
                builder.SetRenderFunc((PassData d, RasterGraphContext context) => Blitter.BlitTexture(context.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, d.pass));
            }
        }
    }

    /// <summary>
    /// Puts the light shafts' pass on the game's cameras in the round's scenes (GameGfx.InScope) while the upgrade is
    /// on; how much comes from the volumes (GameGrade). A camera that draws into a texture is skipped (previews,
    /// mirrors), except CaptureCamera (GfxShots' pictures).
    /// </summary>
    public static class GameShafts
    {
        public static Camera CaptureCamera;
        static GfxLightShaftsPass pass;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            CaptureCamera = null;
            RenderPipelineManager.beginCameraRendering -= Enqueue;
            RenderPipelineManager.beginCameraRendering += Enqueue;
            Application.quitting -= Quit;
            Application.quitting += Quit;
        }

        static void Quit()
        {
            pass?.Dispose();
            pass = null;
        }

        static void Enqueue(ScriptableRenderContext context, Camera camera)
        {
            if (!GameGfx.Upgrade || !GameGfx.InScope() || camera.cameraType != CameraType.Game) return;
            if (camera.targetTexture != null && camera != CaptureCamera) return;
            var data = camera.GetUniversalAdditionalCameraData();
            if (data == null || data.renderType != CameraRenderType.Base || data.scriptableRenderer == null) return;
            if (pass == null) pass = new GfxLightShaftsPass();
            if (pass.Ready) data.scriptableRenderer.EnqueuePass(pass);
        }
    }
}
#endif
