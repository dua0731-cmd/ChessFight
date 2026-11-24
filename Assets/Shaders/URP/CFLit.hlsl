// URP lighting for ChessFight's own shaders that work out a surface's colour in code (the course's patterned boxes,
// the skill test ground): CFLitVertex passes the world position and normal on, CFShade lights a colour, metallic,
// smoothness and alpha the way URP's Lit does (main light and its shadows, additional lights, sky, fog).
// Use it from a pass tagged { "LightMode" = "UniversalForward" } with the keywords in CF_LIT_PRAGMAS below.
//
// JY-gfx (2026-11-24, the graphics upgrade, target Mario Kart World: stylized forms with real surfaces): every
// opaque surface lit here wears a surface set, the fine detail of a real material over the shape and colour the
// shader works out: R roughness, G cavity, B height, A colour variation, and a tangent-space normal, made by
// ArtSource/Gfx/textures.py and handed to every shader as two texture arrays by ChessFight.Game.GameSurfaces
// (CFSurface.hlsl). The set is laid on the three world planes and blended by the surface's facing, so no UVs are needed.
//   A shader picks its set:  #define CF_SURFACE CF_SET_WOOD   before including this file (default: limestone),
//                            #define CF_SURFACE_SCALE 0.3   for how strongly it shows (default 1),
//                            #define CF_SURFACE_NONE        for none;
//   or per call: CFShadeSet(i, albedo, metallic, smoothness, alpha, set, scale). Metals wear brass whatever is
//   asked; see-through surfaces wear nothing. _GfxSurface (x: on, y: strength) is 0 until GameSurfaces sets it,
//   so the editor outside Play and anything without the sets look as before.
#ifndef CF_LIT_INCLUDED
#define CF_LIT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// The keywords a lit pass needs (copy into the pass):
//     #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
//     #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
//     #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
//     #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
//     #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
//     #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
//     #pragma multi_compile_fog
//     #pragma multi_compile_instancing

struct CFLitAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct CFLitVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float2 uv : TEXCOORD2;
    float fog : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

CFLitVaryings CFLitVertex(CFLitAttributes v)
{
    CFLitVaryings o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.positionCS = TransformWorldToHClip(o.positionWS);
    o.uv = v.uv;
    o.fog = ComputeFogFactor(o.positionCS.z);
    return o;
}

// ---------------------------------------------------------------- the surface sets (JY-gfx): CFSurface.hlsl
#include "Assets/Shaders/URP/CFSurface.hlsl"
#ifndef CF_SURFACE
#define CF_SURFACE CF_SET_LIMESTONE
#endif
#ifndef CF_SURFACE_SCALE
#define CF_SURFACE_SCALE 1.0
#endif

half4 CFShadeSet(CFLitVaryings i, half3 albedo, half metallic, half smoothness, half alpha, int set, half scale)
{
    half3 normalWS = NormalizeNormalPerPixel(i.normalWS);
    half hollow = 1.0;
#if !defined(CF_SURFACE_NONE)
    // See-through surfaces (glass, water) keep their own; metals wear brass. Near the switch (a gold trim whose
    // metallic ramps at its edge) the set fades out, so no seam shows where one set gives way to the other.
    if (alpha > 0.99)
        CFSurface(i.positionWS, metallic > 0.5 ? CF_SET_BRASS : set, scale * saturate(abs(metallic - 0.5) * 6.0), normalWS, albedo, smoothness, hollow);
#endif

    InputData d = (InputData)0;
    d.positionWS = i.positionWS;
    d.positionCS = i.positionCS;
    d.normalWS = normalWS;
    d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
    d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
    d.fogCoord = i.fog;
    d.bakedGI = SampleSH(d.normalWS);
    d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
    d.shadowMask = half4(1, 1, 1, 1);

    SurfaceData s = (SurfaceData)0;
    s.albedo = albedo;
    s.metallic = metallic;
    s.smoothness = smoothness;
    // The set's hollows hold the sky's light back a little more than the direct light.
    s.occlusion = lerp(1.0, hollow, 0.5);
    s.alpha = alpha;

    half4 c = UniversalFragmentPBR(d, s);
    c.rgb = MixFog(c.rgb, i.fog);
    return c;
}

half4 CFShade(CFLitVaryings i, half3 albedo, half metallic, half smoothness, half alpha)
{
    return CFShadeSet(i, albedo, metallic, smoothness, alpha, CF_SURFACE, CF_SURFACE_SCALE);
}

#endif
