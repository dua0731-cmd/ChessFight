// R411 (승규 11-05, the hall's entrance arch and its grey clay balustrades: "처음 구간 문을 나무 재질로 바꾸고 아직
// 텍스쳐 칠해져 있지 않은 난간 나무 재질로 바꿔"): polished walnut for meshes without UVs (the hand-built entrance arch,
// the hall model's merged balustrades). The walnut grain picture (PR_Wood_Albedo / _Normal, the royal balcony doors')
// is laid on the three world planes and blended by the surface's facing (triplanar), its grain running along the
// world's y on the upright faces - the balusters' and posts' length - and along x on the tops. Lit as URP's Lit by
// Assets/Shaders/URP/CFLit.hlsl, as the hall's other shaders.
Shader "ChessFight/Pawn Rush World Wood"
{
    Properties
    {
        _MainTex ("Wood grain (PR_Wood_Albedo)", 2D) = "white" {}
        _BumpMap ("Wood grain normal (PR_Wood_Normal)", 2D) = "bump" {}
        _Tint ("Tint", Color) = (0.62, 0.42, 0.28, 1)
        _Scale ("Grain picture size (m)", Range(0.2, 6)) = 1.2
        _BumpScale ("Grain relief", Range(0, 2)) = 0.6
        _Glossiness ("Smoothness (varnish)", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _Tint;
        half _Scale, _BumpScale, _Glossiness;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex CFLitVertex
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Assets/Shaders/URP/CFLit.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            half4 frag(CFLitVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 p = i.positionWS / _Scale;
                float3 n = normalize(i.normalWS);
                float3 w = pow(abs(n), 4);
                w /= max(w.x + w.y + w.z, 1e-4);
                // Each plane's picture: its v along the grain (y on the uprights, x on the tops).
                float2 uvX = p.zy, uvY = p.zx, uvZ = p.xy;
                half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvX).rgb * w.x
                        + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvY).rgb * w.y
                        + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvZ).rgb * w.z;
                // The grain's relief, each plane's tangent normal swizzled onto the world (a whiteout-style blend).
                half3 tX = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX), _BumpScale);
                half3 tY = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY), _BumpScale);
                half3 tZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ), _BumpScale);
                float3 nX = float3(0, tX.y, tX.x) + float3(n.x, 0, 0);
                float3 nY = float3(tY.y, 0, tY.x) + float3(0, n.y, 0);
                float3 nZ = float3(tZ.x, tZ.y, 0) + float3(0, 0, n.z);
                float3 bent = normalize(n + (nX - float3(n.x, 0, 0)) * w.x + (nY - float3(0, n.y, 0)) * w.y + (nZ - float3(0, 0, n.z)) * w.z);
                CFLitVaryings lit = i;
                lit.normalWS = bent;
                return CFShade(lit, c * _Tint.rgb, 0, _Glossiness, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex CFShadowVertex
            #pragma fragment CFShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Assets/Shaders/URP/CFShadowCaster.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex CFDepthVertex
            #pragma fragment CFShadowFragment
            #pragma multi_compile_instancing
            #include "Assets/Shaders/URP/CFShadowCaster.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
