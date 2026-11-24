// Light shafts (JY-gfx, ported from the render lab on JY-look, ChessFight.Game GfxLightShaftsPass): sunlight scattered
// by the air, so the sun's beams through windows, arches and lattices show as shafts and the space gets depth.
//   0 March   half resolution: each pixel's view ray walked through the air from the camera to the surface (or
//             _Shafts.w metres), the main light's shadow map read at each step, Henyey-Greenstein forward scattering
//   1, 2      a 9-tap Gaussian across and down (the march's per-pixel noise becomes a smooth glow)
//   3 Add     the result added onto the camera's colour (before post processing, so bloom and tonemapping see it)
Shader "Hidden/ChessFight/Gfx Light Shafts"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        float4 _Shafts;         // x intensity, y density (1/m), z anisotropy g, w max distance (m)
        float4 _ShaftsColor;
        float _ShaftsSteps;
        float _ShaftsFrame;
        float4 _ShaftsTexel;    // 1/width, 1/height, width, height of the half-resolution target
        SAMPLER(s_linear_clamp_shafts);

        half3 Tap(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, s_linear_clamp_shafts, uv).rgb; }

        half3 Gauss(float2 uv, float2 step)
        {
            half3 c = Tap(uv) * 0.2270270;
            c += (Tap(uv + step * 1.3846154) + Tap(uv - step * 1.3846154)) * 0.3162162;
            c += (Tap(uv + step * 3.2307692) + Tap(uv - step * 3.2307692)) * 0.0702703;
            return c;
        }
        ENDHLSL

        Pass
        {
            Name "March"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMarch
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"

            half4 FragMarch(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
            #if UNITY_REVERSED_Z
                float depth = SampleSceneDepth(uv);
            #else
                float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
            #endif
                float3 surface = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 eye = GetCameraPositionWS();
                float3 ray = surface - eye;
                float rayLength = max(1e-3, sqrt(dot(ray, ray)));
                float3 dir = ray / rayLength;
                float reach = min(rayLength, _Shafts.w);
                // Open sky: only the near air scatters, or the whole sky turns to haze.
            #if UNITY_REVERSED_Z
                bool sky = depth <= 1e-6;
            #else
                bool sky = depth >= 1 - 1e-6;
            #endif
                if (sky) reach = min(reach, _Shafts.w * 0.3);
                int steps = (int)_ShaftsSteps;
                float stepLength = reach / steps;

                Light sun = GetMainLight();
                float g = _Shafts.z;
                float cosTheta = dot(dir, sun.direction);
                float phase = (1.0 - g * g) / (4.0 * PI * pow(max(1e-4, 1.0 + g * g - 2.0 * g * cosTheta), 1.5));

                float noise = InterleavedGradientNoise(input.positionCS.xy, (int)_ShaftsFrame);
                float3 p = eye + dir * stepLength * noise;
                float sigma = _Shafts.y;
                float transmittance = 1.0;
                float lit = 0.0;
                float fade = exp(-sigma * stepLength);
                for (int i = 0; i < steps; i++)
                {
                    float4 shadowCoord = TransformWorldToShadowCoord(p);
                    lit += MainLightRealtimeShadow(shadowCoord) * transmittance;
                    transmittance *= fade;
                    p += dir * stepLength;
                }
                float3 colour = lit * stepLength * sigma * phase * sun.color * _ShaftsColor.rgb * _Shafts.x;
                return half4(colour, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Blur across"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(Gauss(input.texcoord, float2(_ShaftsTexel.x, 0)), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Blur down"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(Gauss(input.texcoord, float2(0, _ShaftsTexel.y)), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Add"
            Blend One One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(Tap(input.texcoord), 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
