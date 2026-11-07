// Indoor soapy water for J Palace Laundry (R509; user 11-08 "색은 좋은데 뭔가 물 처럼 안보여"). A copy of C's moat water
// (PR_MoatWater.shader, R502: thickness colour, refraction, caustics, edge foam), changed for a covered room. The moat's
// caustics and glint all hang on the sun, and indoors the ceiling shadows the sun, so the moat water went flat there
// (a tinted sheet). Here:
//  - the caustic net is lit by a fixed indoor light (_IndoorLight) as well as the sun, so it keeps playing on the tiles;
//  - the room's lights glint on the waves: every point / spot light (the chandeliers' code lights, Forward+ cluster
//    loop) and a soft broad sheen from the windows' side (_WindowDir), both moving with the wave normals;
//  - stronger waves and refraction, so the tile grid under the water wobbles;
//  - the fresnel reflection is a warm room colour (_SkyColor) instead of the sky.
// Needs the URP depth and opaque textures (both on in PC_RPAsset). Writes its own colour (no blending).
Shader "ChessFight/Pawn Rush Soapy Water"
{
    Properties
    {
        _ShallowColor ("Shallow colour", Color) = (0.45, 0.90, 0.92, 1)
        _DeepColor ("Deep colour", Color) = (0.02, 0.50, 0.66, 1)
        _Clarity ("Depth to deep colour (m)", Float) = 0.9
        _SkyColor ("Sky reflection", Color) = (0.72, 0.90, 1.0, 1)
        [Normal][NoScaleOffset] _WaveMap ("Wave normals (periodic)", 2D) = "bump" {}
        _WaveTile ("Wave tile (m)", Float) = 2
        _WaveStrength ("Wave strength", Range(0, 2)) = 0.6
        _WaveSpeed ("Wave drift (m/s)", Float) = 0.12
        [NoScaleOffset] _CausticMap ("Caustics (periodic)", 2D) = "black" {}
        _CausticTile ("Caustic tile (m)", Float) = 1.6
        _CausticStrength ("Caustics on the floor", Range(0, 4)) = 1.4
        _CausticSurface ("Caustics on the surface", Range(0, 1)) = 0.18
        _CausticFade ("Caustic fade depth (m)", Float) = 1.6
        _FoamColor ("Foam colour", Color) = (0.97, 1.0, 0.98, 1)
        _FoamWidth ("Foam width (m of water)", Float) = 0.22
        _FoamBreakup ("Foam break-up", Range(0, 1)) = 0.55
        _Refraction ("Refraction", Range(0, 0.1)) = 0.025
        _FresnelPower ("Fresnel power", Range(1, 8)) = 5
        _Reflection ("Reflection", Range(0, 1)) = 0.55
        _Glint ("Sun glint", Range(0, 12)) = 5
        _GlintSharpness ("Glint sharpness", Range(16, 2048)) = 600
        _IndoorLight ("Indoor light on the caustics", Color) = (1.0, 0.95, 0.85, 1)
        _WindowDir ("Window sheen direction (world)", Vector) = (0.6, 0.75, 0.25, 0)
        _WindowColor ("Window sheen colour", Color) = (1.0, 0.97, 0.9, 1)
        _WindowGlint ("Window sheen", Range(0, 4)) = 1.2
        _WindowSharpness ("Window sheen sharpness", Range(4, 512)) = 90
        _PointGlint ("Room lights glint", Range(0, 12)) = 3
        _PointSharpness ("Room lights glint sharpness", Range(16, 2048)) = 260
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend One Zero
        ZWrite On
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_WaveMap); SAMPLER(sampler_WaveMap);
            TEXTURE2D(_CausticMap); SAMPLER(sampler_CausticMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _SkyColor, _FoamColor;
                float _Clarity, _WaveTile, _WaveStrength, _WaveSpeed;
                float _CausticTile, _CausticStrength, _CausticSurface, _CausticFade;
                float _FoamWidth, _FoamBreakup, _Refraction, _FresnelPower, _Reflection, _Glint, _GlintSharpness;
                half4 _IndoorLight, _WindowColor; float4 _WindowDir;
                float _WindowGlint, _WindowSharpness, _PointGlint, _PointSharpness;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // two drifting copies of the periodic caustic net; their minimum keeps only the lines both agree on,
            // which wander and break like real caustics
            half Caustic (float2 xz, float t)
            {
                float2 uv = xz / _CausticTile;
                half a = SAMPLE_TEXTURE2D(_CausticMap, sampler_CausticMap, uv + float2(t * 0.11, t * 0.07)).r;
                half b = SAMPLE_TEXTURE2D(_CausticMap, sampler_CausticMap, uv * 0.83 + float2(-t * 0.08, t * 0.10) + 0.37).r;
                return min(a, b) * 1.6;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 wuv = i.positionWS.xz / _WaveTile;
                float drift = t * _WaveSpeed / _WaveTile;
                half3 a = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, wuv + float2(drift, drift * 0.6)), _WaveStrength);
                half3 b = UnpackNormalScale(SAMPLE_TEXTURE2D(_WaveMap, sampler_WaveMap, wuv * 1.37 + float2(-drift * 0.7, drift * 0.45) + 0.21), _WaveStrength);
                half2 slope = a.xy + b.xy;
                half up = saturate(i.normalWS.y);
                float3 n = normalize(lerp(i.normalWS, float3(slope.x, 1, slope.y), up));

                // thickness of water in front of the scene behind
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float surfaceEye = i.screenPos.w;
                float2 ruv = suv + slope * _Refraction;
                float rawR = SampleSceneDepth(ruv);
                // never refract something that stands in front of the water
                if (LinearEyeDepth(rawR, _ZBufferParams) < surfaceEye) { ruv = suv; rawR = SampleSceneDepth(suv); }
                float rawS = SampleSceneDepth(suv);
                float thickR = max(0, LinearEyeDepth(rawR, _ZBufferParams) - surfaceEye);
                float thickS = max(0, LinearEyeDepth(rawS, _ZBufferParams) - surfaceEye);

                // the floor seen through the water, in world space, for its caustics
                #if UNITY_REVERSED_Z
                    float deviceR = rawR;
                #else
                    float deviceR = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawR);
                #endif
                float3 floorWS = ComputeWorldSpacePosition(ruv, deviceR, UNITY_MATRIX_I_VP);
                float under = max(0, i.positionWS.y - floorWS.y);

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half lit = lerp(0.6, 1, sun.shadowAttenuation);
                // indoors the sun is shadowed by the ceiling: the indoor light keeps the water lit and its caustics alive
                half3 light = max(sun.color * sun.shadowAttenuation, _IndoorLight.rgb);

                half depthMix = 1 - exp(-thickR / max(_Clarity, 0.01));
                half3 behind = SampleSceneColor(ruv);
                half caust = Caustic(floorWS.xz, t) * _CausticStrength * exp(-under / max(_CausticFade, 0.01));
                half3 bottom = behind * _ShallowColor.rgb + caust * light * _ShallowColor.rgb;
                half3 body = lerp(bottom, _DeepColor.rgb * light, depthMix);
                body += Caustic(i.positionWS.xz * 1.3, t * 1.4) * _CausticSurface * light;

                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                half fres = pow(1 - saturate(dot(n, V)), _FresnelPower) * _Reflection;
                half3 col = lerp(body, _SkyColor.rgb, fres);
                float3 h = normalize(sun.direction + V);
                col += sun.color * pow(saturate(dot(n, h)), _GlintSharpness) * _Glint * sun.shadowAttenuation;
                // a soft broad sheen from the windows' side, sliding over the waves
                float3 hw = normalize(normalize(_WindowDir.xyz) + V);
                col += _WindowColor.rgb * pow(saturate(dot(n, hw)), _WindowSharpness) * _WindowGlint;
                // every room light (the chandeliers) glints on the waves
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = suv;
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS);
                    float3 hl = normalize(l.direction + V);
                    col += l.color * l.distanceAttenuation * pow(saturate(dot(n, hl)), _PointSharpness) * _PointGlint;
                LIGHT_LOOP_END

                // foam line where the water is thin, broken by the caustic net so it reads as bubbles, not paint
                half edge = 1 - saturate(thickS / max(_FoamWidth, 0.001));
                half grain = SAMPLE_TEXTURE2D(_CausticMap, sampler_CausticMap, i.positionWS.xz / (_CausticTile * 0.45) + float2(t * 0.03, 0)).r;
                half foam = saturate(smoothstep(_FoamBreakup * 0.6, 1, edge + grain * _FoamBreakup * edge) * 1.4) * up;
                col = lerp(col, _FoamColor.rgb * lerp(0.75, 1, lit), foam);

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
