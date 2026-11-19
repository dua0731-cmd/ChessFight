// The Royal Observatory's glass (ro-hall, 11-20): the great globe's sphere and the great telescope's tube. The user,
// on Unity Play: the glass skin looked odd, "좀 더 그래픽이 좋았으면" - a flat milky film (URP Lit transparent, a white
// tint) instead of glass in a night hall. This is the project's crystal glass (PR_Glass.shader, R332, seen working in
// Unity) with what the observatory needs on top:
//   behind      the scene behind (URP's opaque texture), bent a little by drifting noise, lightly frosted
//   tint        a night tint (navy / violet) by _BaseColor's alpha
//   reflection  reflection probes / sky, Schlick fresnel from _Reflection face-on to 1 at a slant, the main light's highlight
//   edge        _FresnelColor at grazing angles - the cool rim that makes a curved pane read as glass
//   lines       _BaseMap's alpha above _LineFloor as engraved lines in _LineColor (the globe's latitude / longitude grid
//               texture, RO_PlanetGlobeGlass_Albedo); _LineStrength 0 for plain glass (the tube)
//   both sides  _Cull 0 draws the inside too (the globe is walked in); the normal turns to face the viewer
// Drawn in the transparent queue but writes its own colour (Blend One Zero): what is behind is already in it.
Shader "ChessFight/Royal Observatory Glass"
{
    Properties
    {
        _BaseColor ("Tint (alpha = how much)", Color) = (0.55, 0.62, 1.0, 0.06)
        _BaseMap ("Engraving (alpha = lines)", 2D) = "white" {}
        _LineColor ("Line colour", Color) = (0.02, 0.02, 0.04, 1)
        _LineStrength ("Line strength", Range(0, 1)) = 0
        _LineFloor ("Alpha under which there is no line", Range(0, 1)) = 0.1
        _FresnelColor ("Edge colour", Color) = (0.72, 0.8, 1, 1)
        _FresnelPower ("Edge power", Range(1, 8)) = 3.5
        _FresnelStrength ("Edge strength", Range(0, 4)) = 2.2
        _Frost ("Frost", Range(0, 1)) = 0.1
        _FrostRadius ("Frost radius (share of the screen)", Range(0, 0.05)) = 0.012
        _Distortion ("Distortion (screen UV)", Range(0, 0.1)) = 0.018
        _NoiseScale ("Distortion noise scale (per metre)", Range(0.1, 10)) = 0.8
        _NoiseSpeed ("Distortion drift", Range(0, 1)) = 0.04
        _Reflection ("Reflection (face-on)", Range(0, 1)) = 0.18
        _Smoothness ("Smoothness", Range(0, 1)) = 0.96
        _Highlight ("Light highlight", Range(0, 4)) = 1.6
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One Zero
        ZWrite Off
        Cull [_Cull]

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _FresnelColor, _LineColor;
            half _LineStrength, _LineFloor;
            half _FresnelPower, _FresnelStrength, _Frost, _FrostRadius, _Distortion, _NoiseScale, _NoiseSpeed;
            half _Reflection, _Smoothness, _Highlight, _Cull;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fog : TEXCOORD3;
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
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
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

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS);
                n = front ? n : -n;
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float surface = LinearEyeDepth(i.positionCS.z, _ZBufferParams);

                float3 q = i.positionWS * _NoiseScale + _Time.y * _NoiseSpeed;
                float2 bend = float2(noise(q), noise(q + 17.3)) * 2 - 1;
                half ndv = saturate(dot(n, V));
                float2 ruv = uv + bend * _Distortion * (0.4 + 0.6 * ndv);

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

                half3 col = lerp(behind, _BaseColor.rgb * behind + _BaseColor.rgb * 0.04, saturate(_BaseColor.a * 4));

                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half rough = (1 - _Smoothness) * (1 + _Frost);
                half fresnel = pow(1 - ndv, _FresnelPower);
                half reflectance = _Reflection + (1 - _Reflection) * fresnel;
                half3 env = GlossyEnvironmentReflection(reflect(-V, n), saturate(rough), 1.0h);
                col = lerp(col, env, saturate(reflectance * 0.8));

                // the engraving: the texture's alpha above the clear glass's own (the globe's grid)
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a;
                half engr = saturate((a - _LineFloor) / max(1 - _LineFloor, 0.001)) * _LineStrength;
                col = lerp(col, _LineColor.rgb, engr);

                col += _FresnelColor.rgb * fresnel * _FresnelStrength * 0.25 * (1 - engr);

                float3 h = normalize(sun.direction + V);
                half spec = pow(saturate(dot(n, h)), lerp(32, 512, _Smoothness * (1 - _Frost * 0.7))) * _Highlight;
                col += sun.color * spec * sun.shadowAttenuation;

                // the hall's lamps: a soft highlight each, so the glass catches them as it does in the concept
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = uv;
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS);
                    float3 hl = normalize(l.direction + V);
                    col += l.color * l.distanceAttenuation * pow(saturate(dot(n, hl)), 256) * _Highlight * 0.6;
                LIGHT_LOOP_END

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
