// 승규 11-03 (a reference palace wall, R335): the hall's walls in a cream limestone. They carry no UVs (the bent
// architectural wall, the archway, the columns), and their own relief gives the courses and blocks, so the stone is
// drawn here in world space: a warm cream varied in broad clouds and fine grain, with faint veins - nothing that
// needs to line up with a block. Lit as URP's Lit by Assets/Shaders/URP/CFLit.hlsl (the hall's candle lights too).
Shader "ChessFight/Pawn Rush Palace Stone"
{
    Properties
    {
        _BaseColor ("Stone", Color) = (0.93, 0.87, 0.76, 1)
        _ShadeColor ("Darker patches", Color) = (0.80, 0.71, 0.58, 1)
        _VeinColor ("Veins", Color) = (0.74, 0.62, 0.46, 1)
        _CloudScale ("Patch size (per metre)", Range(0.01, 2)) = 0.18
        _GrainScale ("Grain (per metre)", Range(1, 40)) = 9
        _VeinScale ("Veins (per metre)", Range(0.05, 4)) = 0.45
        _Variation ("Variation", Range(0, 1)) = 0.55
        _Glossiness ("Smoothness", Range(0, 1)) = 0.32
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor, _ShadeColor, _VeinColor;
        half _CloudScale, _GrainScale, _VeinScale, _Variation, _Glossiness;
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

            float hash3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }

            float vnoise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(hash3(i), hash3(i + float3(1, 0, 0)), f.x), lerp(hash3(i + float3(0, 1, 0)), hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(hash3(i + float3(0, 0, 1)), hash3(i + float3(1, 0, 1)), f.x), lerp(hash3(i + float3(0, 1, 1)), hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float fbm(float3 p)
            {
                float s = 0, a = 0.5;
                [unroll] for (int k = 0; k < 4; k++) { s += a * vnoise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return s;
            }

            half4 frag(CFLitVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 p = i.positionWS;
                float cloud = fbm(p * _CloudScale);
                float grain = vnoise(p * _GrainScale) - 0.5;
                // Veins: thin bands where a warped noise crosses one level.
                float w = fbm(p * _VeinScale + fbm(p * _VeinScale * 0.5) * 2.5);
                float vein = 1 - smoothstep(0.0, 0.035, abs(w - 0.5));
                half3 c = lerp(_BaseColor.rgb, _ShadeColor.rgb, saturate((cloud - 0.35) * 1.6) * _Variation);
                c = lerp(c, _VeinColor.rgb, vein * 0.45 * _Variation);
                c *= 1 + grain * 0.06 * _Variation;
                return CFShade(i, c, 0, _Glossiness * (0.85 + 0.3 * cloud), 1);
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
