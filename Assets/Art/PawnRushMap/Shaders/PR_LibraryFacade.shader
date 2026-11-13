// R389 (승규 11-04, the war room's great door concept - cream stone arch with gold bands and a gold keystone, a navy
// tympanum with a gold compass sun, walnut double doors with gold straps, hinges and ring pulls: "문을 저런 택스쳐로
// 해"): the concept picture itself laid on the door (V09_Arched_double_door) and its stone surround, two pieces with no
// UVs whose relief was modelled from that picture (same outline, 8.6 x 9.2 m before their 1.2 widening). It is projected
// straight through the piece: across along the piece's own x axis from its middle, up from its foot (the mesh origin),
// so it stays put wherever the zone is moved; _Size is the piece's width and height in the world. Paint, not metal (the
// picture's gold is painted, never a mirror). Lit by Assets/Shaders/URP/CFLit.hlsl.
Shader "ChessFight/Pawn Rush Library Facade"
{
    Properties
    {
        _MainTex ("Picture (sRGB)", 2D) = "white" {}
        _Size ("Width, height (m)", Vector) = (10.32, 9.2, 0, 0)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST;
        float4 _Size;
        half _Smoothness;
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

            half4 frag(CFLitVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 o = TransformObjectToWorld(float3(0, 0, 0));
                float3 across = normalize(mul((float3x3)GetObjectToWorldMatrix(), float3(1, 0, 0)));
                float3 d = i.positionWS - o;
                float2 uv = float2(dot(d, across) / _Size.x + 0.5, d.y / _Size.y);
                half3 picture = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                return CFShade(i, picture, 0, _Smoothness, 1);
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
