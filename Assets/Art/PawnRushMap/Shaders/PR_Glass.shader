// 승규 11-03 "Unity URP 유리 셰이더 (반사, 왜곡, 프로스티드 효과)" -> the crystal clear look (R332): glass that bends and
// frosts what is behind it and mirrors the sky at a slant.
//   behind      the scene behind (URP's opaque texture, on in both RP assets), pushed about by a drifting value noise
//               (_Distortion, screen UV) - never picking up what stands in front of the glass - and blurred over a
//               12-tap golden-angle disc (_Frost: 0 clear .. 1 a milky pane, at most _FrostRadius of the screen)
//   tint        mixed over it by _BaseColor's alpha, and a little more where it is frosted (frost scatters light back)
//   reflection  the environment (reflection probes / sky) at a roughness from _Smoothness, Schlick fresnel from
//               _Reflection at face-on to 1 at a slant, plus the main light's highlight
//   edge        _FresnelColor at grazing angles (_FresnelPower, _FresnelStrength); _Iridescence turns it into an opal
//               rainbow (0 for crystal clear)
// Drawn in the transparent queue but writes its own colour (Blend One Zero): what is behind is already in it.
Shader "ChessFight/Pawn Rush Glass"
{
    Properties
    {
        _BaseColor ("Tint (alpha = how much)", Color) = (0.910, 0.957, 1.0, 0.10)
        _FresnelColor ("Edge colour", Color) = (1, 1, 1, 1)
        _FresnelPower ("Edge power", Range(1, 8)) = 4
        _FresnelStrength ("Edge strength", Range(0, 4)) = 2.12
        _Iridescence ("Opal rainbow", Range(0, 1)) = 0
        _Frost ("Frost", Range(0, 1)) = 0.37
        _FrostRadius ("Frost radius (share of the screen)", Range(0, 0.05)) = 0.02
        _Distortion ("Distortion (screen UV)", Range(0, 0.1)) = 0.0375
        _NoiseScale ("Distortion noise scale (per metre)", Range(0.1, 10)) = 1.5
        _NoiseSpeed ("Distortion drift", Range(0, 1)) = 0.05
        _Reflection ("Reflection (face-on)", Range(0, 1)) = 0.28
        _Smoothness ("Smoothness", Range(0, 1)) = 0.95
        _Highlight ("Sun highlight", Range(0, 4)) = 1.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One Zero
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
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _FresnelColor;
            half _FresnelPower, _FresnelStrength, _Iridescence, _Frost, _FrostRadius, _Distortion, _NoiseScale, _NoiseSpeed;
            half _Reflection, _Smoothness, _Highlight;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fog : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float hash(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }

            float noise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(hash(i), hash(i + float3(1, 0, 0)), f.x), lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), f.x), lerp(hash(i + float3(0, 1, 1)), hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            // What is behind at uv, unless the glass would pick up something standing in front of itself.
            half3 Behind(float2 uv, float2 home, float surface)
            {
                float d = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                return SampleSceneColor(d < surface ? home : uv);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float surface = LinearEyeDepth(i.positionCS.z, _ZBufferParams);

                // Distortion: two drifting noise lookups give a slanted offset, less at a grazing angle.
                float3 q = i.positionWS * _NoiseScale + _Time.y * _NoiseSpeed;
                float2 bend = float2(noise(q), noise(q + 17.3)) * 2 - 1;
                half ndv = saturate(dot(n, V));
                float2 ruv = uv + bend * _Distortion * (0.4 + 0.6 * ndv);

                // Frost: a 12-tap golden-angle disc around the bent lookup, radius in screen share (aspect kept).
                half3 sharp = Behind(ruv, uv, surface);
                half3 behind = sharp;
                if (_Frost > 0.001)
                {
                    float2 r = _FrostRadius * _Frost * float2(_ScreenParams.y / _ScreenParams.x, 1);
                    half3 sum = sharp;
                    [unroll] for (int k = 1; k < 12; k++)
                    {
                        float a = k * 2.39996, s = sqrt(k / 11.0);
                        sum += Behind(ruv + float2(cos(a), sin(a)) * r * s, uv, surface);
                    }
                    behind = lerp(sharp, sum / 12, saturate(_Frost * 1.6));
                }

                half3 col = lerp(behind, _BaseColor.rgb, saturate(_BaseColor.a * (1 + _Frost)));

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half rough = (1 - _Smoothness) * (1 + _Frost);
                half fresnel = pow(1 - ndv, _FresnelPower);
                half reflectance = _Reflection + (1 - _Reflection) * fresnel;
                half3 env = GlossyEnvironmentReflection(reflect(-V, n), saturate(rough), 1.0h);
                col = lerp(col, env, saturate(reflectance * 0.85));

                half3 rainbow = 0.5 + 0.5 * cos(6.28318 * (fresnel * 1.6 + ndv * 0.7 + half3(0, 0.33, 0.67)));
                half3 edge = lerp(_FresnelColor.rgb, rainbow * _FresnelColor.rgb * 1.4, _Iridescence);
                col += edge * fresnel * _FresnelStrength * 0.25;

                float3 h = normalize(sun.direction + V);
                half spec = pow(saturate(dot(n, h)), lerp(32, 512, _Smoothness * (1 - _Frost * 0.7))) * _Highlight;
                col += sun.color * spec * sun.shadowAttenuation;

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
