// Pawn Rush water (승규 10-22 "물 그래픽 사실적으로"): see-through clear water that takes the sky.
// Two copies of a wave normal map scroll across each other in world space (any box size tiles the same),
// fresnel turns grazing views into a sky mirror, the sun leaves a sharp glint, shadows darken it, and
// a little sparkle rides the wave crests. URP forward, transparent, no depth texture needed.
Shader "ChessFight/Pawn Rush Water"
{
    Properties
    {
        _ShallowColor ("Shallow colour", Color) = (0.30, 0.66, 0.78, 0.55)
        _DeepColor ("Deep colour", Color) = (0.05, 0.24, 0.40, 0.85)
        _SkyColor ("Sky reflection", Color) = (0.66, 0.84, 0.99, 1)
        [Normal] _WaveMap ("Wave normals", 2D) = "bump" {}
        _WaveScale ("Wave size (per metre)", Float) = 0.45
        _WaveStrength ("Wave strength", Range(0, 2)) = 0.55
        _WaveSpeed ("Wave speed", Float) = 0.05
        _FresnelPower ("Fresnel power", Range(1, 8)) = 4
        _Glint ("Sun glint", Range(0, 8)) = 5
        _GlintSharpness ("Glint sharpness", Range(16, 1024)) = 320
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_WaveMap); SAMPLER(sampler_WaveMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _SkyColor;
                float4 _WaveMap_ST;
                float _WaveScale, _WaveStrength, _WaveSpeed, _FresnelPower, _Glint, _GlintSharpness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y * _WaveSpeed;
                float2 uv = i.positionWS.xz * _WaveScale;
                half3 a = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, uv + float2(t, t * 0.6)), _WaveStrength);
                half3 b = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, uv * 1.7 + float2(-t * 0.8, t * 0.35)), _WaveStrength);
                half2 slope = a.xy + b.xy;
                // only the top face ripples; the box's sides keep their own normal
                half up = saturate(i.normalWS.y);
                float3 n = normalize(lerp(i.normalWS, float3(slope.x, 1, slope.y), up));

                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                half fres = pow(1 - saturate(dot(n, V)), _FresnelPower);

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half shade = lerp(0.55, 1, sun.shadowAttenuation);
                half lambert = saturate(dot(n, sun.direction)) * 0.35 + 0.65;

                // body: deeper where the waves tilt away, brighter on crests
                half crest = saturate(0.5 + (slope.x + slope.y) * 0.8);
                half3 body = lerp(_DeepColor.rgb, _ShallowColor.rgb, crest) * lambert * shade * sun.color;
                half3 sky = _SkyColor.rgb * (0.85 + 0.15 * sun.color);
                half3 col = lerp(body, sky, fres * 0.85);

                float3 h = normalize(sun.direction + V);
                half glint = pow(saturate(dot(n, h)), _GlintSharpness) * _Glint * sun.shadowAttenuation;
                col += sun.color * glint;

                half alpha = saturate(lerp(lerp(_DeepColor.a, _ShallowColor.a, crest), 1, fres) + glint);
                col = MixFog(col, i.fog);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
