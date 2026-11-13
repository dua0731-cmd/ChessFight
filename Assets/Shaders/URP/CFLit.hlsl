// URP lighting for ChessFight's own shaders that work out a surface's colour in code (the course's patterned boxes,
// the skill test ground): CFLitVertex passes the world position and normal on, CFShade lights a colour, metallic,
// smoothness and alpha the way URP's Lit does (main light and its shadows, additional lights, sky, fog).
// Use it from a pass tagged { "LightMode" = "UniversalForward" } with the keywords in CF_LIT_PRAGMAS below.
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

half4 CFShade(CFLitVaryings i, half3 albedo, half metallic, half smoothness, half alpha)
{
    InputData d = (InputData)0;
    d.positionWS = i.positionWS;
    d.positionCS = i.positionCS;
    d.normalWS = NormalizeNormalPerPixel(i.normalWS);
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
    s.occlusion = 1;
    s.alpha = alpha;

    half4 c = UniversalFragmentPBR(d, s);
    c.rgb = MixFog(c.rgb, i.fog);
    return c;
}

#endif
