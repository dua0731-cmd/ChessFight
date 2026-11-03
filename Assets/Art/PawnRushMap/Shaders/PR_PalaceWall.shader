// 승규 11-03 ("검은색이 원래 돌 텍스쳐 흰색이 골드색이야", R337): the hall's architectural walls in cream limestone with gold
// trim, the trim read per pixel from the wall module's moulding map (PalaceTrim.png: white gold, black stone) through
// UVs laid straight onto the asset's front (walls_v33.py) - so the gold's edge is the map's, never a triangle's.
// The stone is PR_PalaceStone.shader's, drawn in world space; the gold is metallic, a little varied, brighter on its
// upper faces. Lit as URP's Lit by Assets/Shaders/URP/CFLit.hlsl.
Shader "ChessFight/Pawn Rush Palace Wall"
{
    Properties
    {
        _TrimMap ("Trim map (white = gold)", 2D) = "black" {}
        _BaseColor ("Stone", Color) = (0.93, 0.87, 0.76, 1)
        _ShadeColor ("Darker patches", Color) = (0.80, 0.71, 0.58, 1)
        _VeinColor ("Veins", Color) = (0.74, 0.62, 0.46, 1)
        _CloudScale ("Patch size (per metre)", Range(0.01, 2)) = 0.18
        _GrainScale ("Grain (per metre)", Range(1, 40)) = 9
        _VeinScale ("Veins (per metre)", Range(0.05, 4)) = 0.45
        _Variation ("Variation", Range(0, 1)) = 0.55
        _Glossiness ("Stone smoothness", Range(0, 1)) = 0.32
        _GoldColor ("Gold", Color) = (0.86, 0.64, 0.28, 1)
        _GoldMetallic ("Gold metallic", Range(0, 1)) = 0.65
        _GoldSmoothness ("Gold smoothness", Range(0, 1)) = 0.7
        _Edge ("Edge softness", Range(0.01, 0.5)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _TrimMap_ST;
        half4 _BaseColor, _ShadeColor, _VeinColor, _GoldColor;
        half _CloudScale, _GrainScale, _VeinScale, _Variation, _Glossiness, _GoldMetallic, _GoldSmoothness, _Edge;
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

            TEXTURE2D(_TrimMap); SAMPLER(sampler_TrimMap);

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
                float w = fbm(p * _VeinScale + fbm(p * _VeinScale * 0.5) * 2.5);
                float vein = 1 - smoothstep(0.0, 0.035, abs(w - 0.5));
                half3 stone = lerp(_BaseColor.rgb, _ShadeColor.rgb, saturate((cloud - 0.35) * 1.6) * _Variation);
                stone = lerp(stone, _VeinColor.rgb, vein * 0.45 * _Variation);
                stone *= 1 + grain * 0.06 * _Variation;

                half m = SAMPLE_TEXTURE2D(_TrimMap, sampler_TrimMap, TRANSFORM_TEX(i.uv, _TrimMap)).r;
                m = smoothstep(0.5 - _Edge, 0.5 + _Edge, m);
                half3 gold = _GoldColor.rgb * (0.9 + 0.2 * cloud) * (1 + 0.12 * saturate(i.normalWS.y));

                half3 albedo = lerp(stone, gold, m);
                half metallic = _GoldMetallic * m;
                half smooth = lerp(_Glossiness * (0.85 + 0.3 * cloud), _GoldSmoothness, m);
                return CFShade(i, albedo, metallic, smooth, 1);
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
