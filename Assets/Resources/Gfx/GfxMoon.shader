// JY-gfx (2026-11-24): a moon in a section's night sky (JY on the observatory: "맵에 달과 거기서부터 빛이 새어나와서 맵에
// 빛줄기가 비춰지도록"). A quad far out where the moonlight comes from, facing the hall: a bright disc, darker toward its
// limb with a few soft maria, and a soft halo round it; not dimmed by the haze (it is the light's source), drawn added
// so the bloom carries its glow.
Shader "ChessFight/Gfx Moon"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (4, 4.3, 4.8, 1)
        _Disc ("Disc radius (of the quad)", Range(0.05, 0.5)) = 0.24
        _Halo ("Halo strength", Float) = 0.35
        _HaloWidth ("Halo width", Float) = 6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Moon"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Disc;
                half _Halo;
                half _HaloWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv * 2.0 - 1.0;
                return o;
            }

            // A smooth value noise for the maria.
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float d = length(i.uv);
                float r = d / _Disc;
                // The disc: soft edge, a little darker toward the limb, faint maria.
                half disc = saturate((1.0 - r) * 40.0);
                half limb = lerp(0.72, 1.0, sqrt(saturate(1.0 - r * r)));
                float2 p = i.uv / _Disc * 2.2;
                half maria = 1.0 - 0.28 * smoothstep(0.55, 0.75, Noise(p + 3.1) * 0.65 + Noise(p * 2.3 + 7.7) * 0.35);
                // The halo: falls off past the disc.
                half halo = _Halo * exp(-max(0.0, d - _Disc) * _HaloWidth);
                half3 colour = _Color.rgb * (disc * limb * maria + halo * (1.0 - disc));
                return half4(colour, 0);
            }
            ENDHLSL
        }
    }
}
