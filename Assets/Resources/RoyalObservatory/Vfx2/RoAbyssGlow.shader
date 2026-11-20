// R725 ro-vfx2 (the hall, PLAN 5 "심연에서 올라오는 희미한 성운 빛"): faint nebula glow welling up out of the abyss under the
// hall floor - soft clouds of light that rise slowly from deep down, swell, and fade before they reach the floor's level.
// One quad a cloud (RoyalObservatoryVfxHall lays them round the hall's well); each turned to the camera here. Per vertex:
// position the cloud's lowest point (object space), uv the corner, uv2 (size m, phase 0..1, colour 0..1, seed).
// A cloud's life is _Cycle seconds: it rises _Rise metres, swelling from 0.7 to 1.2 times its size, its light a bell
// over its life; inside, two layers of drifting noise, so it reads as gas and not a disc. Soft where it meets the abyss's
// walls and pillars (the camera's depth texture), gone when the camera is in it. Added onto what is behind it.
Shader "ChessFight/VFX/RO Abyss Glow"
{
    Properties
    {
        [HDR] _ColorA ("Violet (HDR)", Color) = (0.42, 0.26, 0.85, 1)
        [HDR] _ColorB ("Teal (HDR)", Color) = (0.18, 0.5, 0.78, 1)
        [HDR] _ColorC ("Rose (HDR)", Color) = (0.7, 0.3, 0.6, 1)
        _Light ("Light", Float) = 0.1
        _Cycle ("Life (s)", Float) = 18
        _Rise ("Rise (m)", Float) = 16
        _DepthFade ("Soft contact (m)", Float) = 3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA;
                half4 _ColorB;
                half4 _ColorC;
                half _Light;
                float _Cycle;
                float _Rise;
                float _DepthFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 corner : TEXCOORD0;
                float4 cloud : TEXCOORD1;   // size, phase, colour, seed
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 color : TEXCOORD1;
                float2 noise : TEXCOORD2;   // seed, age
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float age = frac(_Time.y / max(_Cycle, 1.0) + input.cloud.y);
                float3 centre = TransformObjectToWorld(input.positionOS.xyz + float3(0.0, age * _Rise, 0.0));
                float size = input.cloud.x * (0.7 + 0.5 * age);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 world = centre + (right * (input.corner.x - 0.5) + up * (input.corner.y - 0.5)) * size;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.corner;

                half bell = sin(age * 3.14159265);
                bell *= bell;
                // gone when the camera comes into it
                half inside = smoothstep(size * 0.3, size * 0.8, length(_WorldSpaceCameraPos.xyz - centre));
                float hue = input.cloud.z;
                half3 col = hue < 0.5 ? lerp(_ColorA.rgb, _ColorB.rgb, hue * 2.0) : lerp(_ColorB.rgb, _ColorC.rgb, hue * 2.0 - 1.0);
                output.color = col * (_Light * bell * inside);
                output.noise = float2(input.cloud.w, age);
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p), u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 c = input.uv * 2.0 - 1.0;
                half body = saturate(1.0 - dot(c, c));
                body *= body;
                float2 q = input.uv * 2.6 + input.noise.x * 17.0 + float2(0.0, -input.noise.y * 1.4);
                half gas = Noise(q) * 0.62 + Noise(q * 2.3 + 5.1 + float2(input.noise.y * 0.6, 0.0)) * 0.38;
                gas = smoothstep(0.25, 0.85, gas);

                float2 screen = GetNormalizedScreenSpaceUV(input.positionCS);
                float scene = LinearEyeDepth(SampleSceneDepth(screen), _ZBufferParams);
                float self = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                half soft = saturate((scene - self) / max(_DepthFade, 0.05));
                return half4(input.color * (body * (0.35 + 0.65 * gas) * soft), 0.0);
            }
            ENDHLSL
        }
    }
}
