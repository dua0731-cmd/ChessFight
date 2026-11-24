// JY-gfx (2026-11-24, the graphics upgrade): ChessFight/Gfx Lit's forward pass. URP 17.3's LitForwardPass.hlsl as it
// is (vertex, varyings, InitializeInputData), and a fragment that is URP's LitPassFragment with one step added:
// after the surface and its normal are known, the material's surface set (_GfxSet, CFSurface.hlsl) bends the normal,
// varies the colour and smoothness and darkens the hollows, on the object's own planes so it moves with the object.
// Glass and other see-through surfaces keep their own. Keep it in step with LitPassFragment when URP is updated.
#ifndef CF_GFX_LIT_FORWARD_PASS_INCLUDED
#define CF_GFX_LIT_FORWARD_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
#include "Assets/Shaders/URP/CFSurface.hlsl"

void GfxLitPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_PARALLAXMAP)
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
#else
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
#endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    // JY-gfx: the surface set, before the sky's light is looked up so that follows the bent normal too.
    if (_GfxSet >= 0.0 && !IsSurfaceTypeTransparent(_Surface))
    {
        half hollow;
        CFSurfaceObject(inputData.positionWS, (int)(_GfxSet + 0.5), _GfxSetScale, inputData.normalWS, surfaceData.albedo, surfaceData.smoothness, hollow);
        // The hollows hold the sky's light back a little more than the direct light.
        surfaceData.occlusion *= lerp(1.0, hollow, 0.5);
    }

    InitializeBakedGIData(input, inputData);

#if defined(_PARALLAXMAP) && defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    // JY-gfx: the relief's own shadow from the sun (inside the joints, on the stones' lee sides), from where the
    // view ray met the height map (input.uv is that point now).
    half reliefShadow = 1.0;
    if (gfxReliefFade > 0.0)
    {
        float sgn = input.tangentWS.w * GetOddNegativeScale();
        float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
        float3x3 tbn = float3x3(input.tangentWS.xyz, bitangent, input.normalWS.xyz);
        float3 lightTS = normalize(mul(tbn, _MainLightPosition.xyz));
        reliefShadow = lerp(1.0, GfxReliefShadow(input.uv, gfxReliefHeight, lightTS), gfxReliefFade * _GfxReliefShadow);
    }
#endif

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
#if defined(_PARALLAXMAP) && defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    // That shadow takes its share of the sun's light back out (the sky's light stays).
    if (reliefShadow < 0.999)
    {
        BRDFData brdfData;
        InitializeBRDFData(surfaceData, brdfData);
        Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
        half3 sun = LightingPhysicallyBased(brdfData, mainLight, inputData.normalWS, inputData.viewDirectionWS);
        color.rgb = max(color.rgb - sun * (1.0 - reliefShadow), 0.0);
    }
#endif
    // JY-gfx: leaves let the sun through (the leaf set only).
    if ((int)(_GfxSet + 0.5) == CF_SET_LEAF && !IsSurfaceTypeTransparent(_Surface))
        color.rgb += CFFoliageLight(inputData.normalWS, inputData.viewDirectionWS, surfaceData.albedo, inputData.shadowCoord);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
