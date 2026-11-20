// R725 ro-vfx (the great globe's solar flare, user 11-20 "대천구 태양은 신경 많이 써서"): the sun lamp's corona and flames,
// a quad at the lamp's middle turned to the camera in the vertex shader (RoyalObservatoryVfxFlare sizes it, _Size m half
// across, and gives the lamp's radius _SunRadius, which swells in the flare). Measured in the sun's radii from its middle:
//   limb     a thin hot ring just over the surface
//   corona   a glow dying away over _CoronaReach radii
//   flames   tongues licking up off the limb, churning and rising (two layers of noise round it, no seam), as tall as
//            _FlameReach radii at their tallest, as strong as _Flames
//   flash    the burn's flash, a wide hot glow (_Flash)
//   burst    (user 11-20 "태양이 폭발 할 때 더 폭발 하는 것처럼 해줘") a ring of light thrown out from the surface
//            (_Shock radii out, _ShockLight) and thin rays shooting out all round (_Rays, as long as _RayReach radii)
// The part over the sun's face is behind the sun (depth tested) and it softens where it meets the sun or anything else
// (the camera's depth texture). Added onto what is behind it.
Shader "ChessFight/VFX/RO Sun Corona"
{
    Properties
    {
        [HDR] _Color ("Corona and flames (HDR)", Color) = (1, 0.52, 0.16, 1)
        [HDR] _HotColor ("Limb, flame roots and flash (HDR)", Color) = (1, 0.88, 0.62, 1)
        _Light ("Light", Float) = 1
        _CoronaReach ("Corona reach (sun radii)", Float) = 0.3
        _Flames ("Flames", Range(0, 1)) = 0.2
        _FlameReach ("Flame reach (sun radii)", Float) = 0.25
        _Flash ("Flash", Float) = 0
        _Shock ("Burst ring (radii out)", Float) = 0
        _ShockLight ("Burst ring light", Float) = 0
        _Rays ("Burst rays", Float) = 0
        _RayReach ("Burst ray reach (radii)", Float) = 1
        _SunRadius ("Sun radius (m)", Float) = 2.2
        _Size ("Half size (m)", Float) = 5
        _DepthFade ("Soft contact (m)", Range(0.01, 2)) = 0.35
        _Age ("Age (s)", Float) = 0
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
                half4 _Color;
                half4 _HotColor;
                half _Light;
                float _CoronaReach;
                half _Flames;
                float _FlameReach;
                half _Flash;
                float _Shock;
                half _ShockLight;
                half _Rays;
                float _RayReach;
                float _SunRadius;
                float _Size;
                float _DepthFade;
                float _Age;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 centre = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 world = centre + (right * (input.uv.x - 0.5) + up * (input.uv.y - 0.5)) * (2.0 * _Size);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Value noise repeating every `period` along x (round the sun: no seam).
            float Noise(float2 p, float period)
            {
                float2 i = floor(p), f = frac(p), u = f * f * (3.0 - 2.0 * f);
                float x0 = i.x - period * floor(i.x / period), x1 = x0 + 1.0 - period * step(period - 1.5, x0);
                return lerp(lerp(Hash(float2(x0, i.y)), Hash(float2(x1, i.y)), u.x),
                            lerp(Hash(float2(x0, i.y + 1.0)), Hash(float2(x1, i.y + 1.0)), u.x), u.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                const float TAU = 6.2831853;
                float2 c = (input.uv * 2.0 - 1.0) * (_Size / max(_SunRadius, 0.01));   // in the sun's radii
                float rho = length(c), x = rho - 1.0;
                clip(x + 0.06);
                float around = frac(atan2(c.x, c.y) / TAU + 1.0);

                half limb = exp(-abs(x - 0.02) / 0.035);
                half corona = exp(-max(x, 0.0) / max(_CoronaReach, 0.02));
                // flames: two layers of noise round the limb, churning and rising
                float n1 = Noise(float2(around * 18.0, x * 2.2 - _Age * 0.9), 18.0);
                float n2 = Noise(float2(around * 42.0, x * 4.5 - _Age * 1.7 + 11.0), 42.0);
                float n = n1 * 0.65 + n2 * 0.35;
                half tongue = saturate(1.0 - max(x, 0.0) / max(n * _FlameReach * 1.5, 0.001)) * smoothstep(0.3, 0.6, n);

                // the burst: a ring thrown out, a little uneven round it, and thin rays all round, longest first
                half shockBand = exp(-abs(x - _Shock) / (0.06 + 0.05 * _Shock)) * step(0.01, _Shock) * (0.75 + 0.5 * n1);
                float rayNoise = Noise(float2(around * 96.0, 3.1), 96.0) * 0.7 + Noise(float2(around * 37.0, 7.9), 37.0) * 0.3;
                half ray = pow(saturate(rayNoise), 6.0) * exp(-max(x, 0.0) / max(_RayReach * (0.4 + 0.6 * rayNoise), 0.02));

                half3 col = _Color.rgb * (corona * (0.55 + 0.45 * n) * 0.8 + tongue * _Flames * 1.8 + shockBand * _ShockLight * 0.6)
                          + _HotColor.rgb * (limb * (0.5 + 0.8 * _Flames) + tongue * _Flames * 0.6
                                             + _Flash * exp(-max(x, 0.0) / max(_CoronaReach * 2.0, 0.05))
                                             + shockBand * _ShockLight + ray * _Rays);

                // soft where it meets the sun or anything else, gone toward the quad's edge
                float2 screen = GetNormalizedScreenSpaceUV(input.positionCS);
                float scene = LinearEyeDepth(SampleSceneDepth(screen), _ZBufferParams);
                float self = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                half soft = saturate((scene - self) / _DepthFade);
                float most = _Size / max(_SunRadius, 0.01);
                half fade = (1.0 - smoothstep(most * 0.82, most * 0.98, rho)) * smoothstep(-0.06, 0.02, x);
                return half4(col * (_Light * soft * fade), 0.0);
            }
            ENDHLSL
        }
    }
}
