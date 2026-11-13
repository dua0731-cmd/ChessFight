// R387 (승규 11-04, the war room's goal platform and a concept tile - a teal panel inlaid with a gold compass star in a
// cream stone rim with gold corners: "마지막 구간 발판을 2번 째 사진으로 바꿔"): a picture laid over the top of a
// panel that has no UVs (Goal24_Star_platform_center, 5.45 m square). It is projected straight down from the panel's own
// middle (the mesh's origin), so it stays put wherever the zone is moved; the picture is symmetric, so the hall's half
// turn does not show. The faces that do not look up are _SideColor. _MSMap: R metallic, A smoothness. Lit by
// Assets/Shaders/URP/CFLit.hlsl.
Shader "ChessFight/Pawn Rush Library Inlay"
{
    Properties
    {
        _MainTex ("Picture (sRGB)", 2D) = "white" {}
        _MSMap ("Metallic (R), smoothness (A)", 2D) = "black" {}
        _Size ("Picture size (m)", Float) = 5.45
        _SideColor ("Sides", Color) = (0.22, 0.40, 0.47, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST, _MSMap_ST;
        half4 _SideColor;
        float _Size;
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
            TEXTURE2D(_MSMap); SAMPLER(sampler_MSMap);

            half4 frag(CFLitVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 o = TransformObjectToWorld(float3(0, 0, 0));
                float2 uv = (i.positionWS.xz - o.xz) / _Size + 0.5;
                // Sampled before the branch (derivatives).
                half3 picture = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                half4 ms = SAMPLE_TEXTURE2D(_MSMap, sampler_MSMap, uv);
                // R391: the faces looking up or down (the ceiling's medallion is seen from below) take the picture.
                if (abs(normalize(i.normalWS).y) < 0.7) return CFShade(i, _SideColor.rgb, 0.05, 0.3, 1);
                return CFShade(i, picture, ms.r, ms.a, 1);
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
