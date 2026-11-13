// The shadow (and depth-only) pass for ChessFight's own shaders under URP: the mesh as it stands, biased for the
// light it is drawn for, as URP's Lit does it. A pass may define CF_SHADOW_CLIP (a statement such as
// clip(_Color.a - 0.5);) before including this to drop pixels.
//
// Shadow pass:
//     Pass
//     {
//         Name "ShadowCaster"
//         Tags { "LightMode" = "ShadowCaster" }
//         ZWrite On ZTest LEqual ColorMask 0
//         HLSLPROGRAM
//         #pragma vertex CFShadowVertex
//         #pragma fragment CFShadowFragment
//         #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
//         #pragma multi_compile_instancing
//         #include "Assets/Shaders/URP/CFShadowCaster.hlsl"
//         ENDHLSL
//     }
// Depth pass: the same with Tags { "LightMode" = "DepthOnly" }, ColorMask R and #pragma vertex CFDepthVertex.
#ifndef CF_SHADOW_CASTER_INCLUDED
#define CF_SHADOW_CASTER_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct CFShadowAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct CFShadowVaryings
{
    float4 positionCS : SV_POSITION;
};

CFShadowVaryings CFShadowVertex(CFShadowAttributes v)
{
    CFShadowVaryings o;
    UNITY_SETUP_INSTANCE_ID(v);
    float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    o.positionCS = positionCS;
    return o;
}

CFShadowVaryings CFDepthVertex(CFShadowAttributes v)
{
    CFShadowVaryings o;
    UNITY_SETUP_INSTANCE_ID(v);
    o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
    return o;
}

half4 CFShadowFragment(CFShadowVaryings i) : SV_Target
{
#ifdef CF_SHADOW_CLIP
    CF_SHADOW_CLIP
#endif
    return 0;
}

#endif
