// 승규 11-04 (R358, floor options floor_marble_options_v38.py -> "A로 해주고 계단도 바꿔줘"): Calacatta Oro, a polished
// white marble with bold grey veins haloed in gold and fine grey veins between them, drawn in world space so it needs
// no UVs (the hall's floors) and never repeats (the grand stair too). Veins are contour lines of warped noise: the
// bold ones of a slow field, the fine ones of a quicker one; a soft, wider band round each bold vein tints it gold.
// Lit as URP's Lit by Assets/Shaders/URP/CFLit.hlsl.
Shader "ChessFight/Pawn Rush Veined Marble"
{
    Properties
    {
        _BaseColor ("Marble", Color) = (0.95, 0.94, 0.91, 1)
        _ShadeColor ("Clouds", Color) = (0.88, 0.86, 0.82, 1)
        _VeinColor ("Bold veins", Color) = (0.55, 0.53, 0.5, 1)
        _GoldColor ("Gold round the veins", Color) = (0.78, 0.64, 0.4, 1)
        _FineColor ("Fine veins", Color) = (0.7, 0.69, 0.67, 1)
        _CloudScale ("Clouds (per metre)", Range(0.02, 2)) = 0.3
        _VeinScale ("Bold veins (per metre)", Range(0.02, 2)) = 0.22
        _FineScale ("Fine veins (per metre)", Range(0.1, 6)) = 0.9
        _Warp ("Warp", Range(0, 6)) = 2.6
        _VeinWidth ("Bold vein width", Range(0.002, 0.1)) = 0.022
        _FineWidth ("Fine vein width", Range(0.001, 0.05)) = 0.008
        _VeinStrength ("Bold vein strength", Range(0, 1)) = 0.85
        _GoldStrength ("Gold strength", Range(0, 1)) = 0.45
        _FineStrength ("Fine vein strength", Range(0, 1)) = 0.35
        _Glossiness ("Smoothness", Range(0, 1)) = 0.86
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor, _ShadeColor, _VeinColor, _GoldColor, _FineColor;
        half _CloudScale, _VeinScale, _FineScale, _Warp, _VeinWidth, _FineWidth, _VeinStrength, _GoldStrength, _FineStrength, _Glossiness;
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
                [unroll] for (int k = 0; k < 5; k++) { s += a * vnoise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return s;
            }

            half4 frag(CFLitVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 p = i.positionWS;
                float cloud = fbm(p * _CloudScale + 3.7);
                // Bold veins: one level of a slow field, warped by another, so they wander in long lines.
                float3 q = p * _VeinScale;
                float3 warp = float3(fbm(q * 0.6 + 1.3), fbm(q * 0.6 + 7.9), fbm(q * 0.6 + 4.1)) - 0.5;
                float field = fbm(q + warp * _Warp);
                float d = abs(field - 0.5);
                float bold = 1 - smoothstep(_VeinWidth * 0.35, _VeinWidth, d);
                float halo = 1 - smoothstep(_VeinWidth, _VeinWidth * 4.5, d);
                // Fine veins: the same on a quicker field, branching between the bold ones.
                float3 r = p * _FineScale;
                float fineField = fbm(r + warp * _Warp * 1.6 + 11.3);
                float fine = 1 - smoothstep(_FineWidth * 0.3, _FineWidth, abs(fineField - 0.5));
                half3 c = lerp(_BaseColor.rgb, _ShadeColor.rgb, saturate((cloud - 0.35) * 1.8));
                c = lerp(c, _GoldColor.rgb, halo * (1 - bold) * _GoldStrength);
                c = lerp(c, _FineColor.rgb, fine * _FineStrength);
                c = lerp(c, _VeinColor.rgb, bold * _VeinStrength);
                return CFShade(i, c, 0, _Glossiness * (0.92 + 0.08 * cloud), 1);
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
