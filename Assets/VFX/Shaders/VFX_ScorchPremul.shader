// A burn mark (R164, 10-27, 사용자: "검은색과 빨간색(완전한 빨간색 x)이 바닥에 그어지고"): black char down the middle of a
// strip and a dark red ember glow round it, both broken up by a scrolling noise that also eats it away as _Dissolve rises.
// Premultiplied blending, so the char darkens what is under it while the embers add their light. Strips and rings use it
// along their UVs (u along the shape, v across it) like VFX_ScrollDissolveAdd, and VfxBeam / VfxRing set _Fade, _Dissolve,
// _Intensity and _UFade per renderer the same way. The embers die before the char (they fade twice as fast).
Shader "ChessFight/VFX/Scorch Premultiplied"
{
    Properties
    {
        _CoreColor ("Char colour (alpha = how dark)", Color) = (0.02, 0.008, 0.008, 0.85)
        [HDR] _Color ("Ember colour (HDR)", Color) = (0.6, 0.07, 0.06, 1)
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _CoreWidth ("Char width (share of the half width)", Range(0, 1)) = 0.45
        _Ragged ("Raggedness of the char's edge", Range(0, 0.6)) = 0.25
        _ScrollSpeed ("Scroll (cells per second, u v)", Vector) = (0.6, 0, 0, 0)
        _NoiseScale ("Noise cells (u v; u a whole number)", Vector) = (12, 2, 0, 0)
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _EdgeSoftness ("Dissolve edge softness", Range(0.01, 0.5)) = 0.15
        _UFade ("Fade at the u ends", Range(0, 0.5)) = 0
        _VFade ("Fade at the outer edges", Range(0, 0.5)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _Color;
                half _Intensity;
                half _Fade;
                half _CoreWidth;
                half _Ragged;
                float4 _ScrollSpeed;
                float4 _NoiseScale;
                half _Dissolve;
                half _EdgeSoftness;
                half _UFade;
                half _VFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Value noise whose cells repeat every `period` along x.
            float Noise(float2 p, float period)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float x0 = i.x - period * floor(i.x / period), x1 = x0 + 1.0;
                x1 -= period * floor(x1 / period);
                float a = Hash(float2(x0, i.y)), b = Hash(float2(x1, i.y));
                float c = Hash(float2(x0, i.y + 1.0)), d = Hash(float2(x1, i.y + 1.0));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p, float period)
            {
                return Noise(p, period) * 0.6 + Noise(p * 2.0 + 17.1, period * 2.0) * 0.3 + Noise(p * 4.0 + 5.3, period * 4.0) * 0.1;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float period = max(1.0, round(_NoiseScale.x));
                float2 p = input.uv * float2(period, _NoiseScale.y) + _ScrollSpeed.xy * _Time.y;
                float n = Fbm(p, period);
                half mask = saturate((n - _Dissolve) / _EdgeSoftness + 0.5) * (1.0 - step(0.999, _Dissolve));
                half ends = 1.0;
                if (_UFade > 0.001) ends *= smoothstep(0.0, _UFade, input.uv.x) * smoothstep(0.0, _UFade, 1.0 - input.uv.x);
                half across = abs(input.uv.y * 2.0 - 1.0);
                half edge = _CoreWidth + (n - 0.5) * _Ragged;
                half charred = 1.0 - smoothstep(edge - 0.08, edge + 0.08, across);
                half outer = 1.0 - smoothstep(1.0 - max(_VFade, 0.01), 1.0, across);
                // The ember band round the char, hottest where it meets it.
                half band = (1.0 - charred) * outer;
                half hot = band * lerp(0.45, 1.0, 1.0 - saturate((across - edge) / max(0.05, 1.0 - edge)));
                half k = mask * ends * _Fade * input.color.a;
                half alpha = (charred * _CoreColor.a + band * 0.3) * k;
                half3 glow = _Color.rgb * _Intensity * hot * lerp(0.5, 1.3, n) * input.color.rgb * k * _Fade;
                return half4(_CoreColor.rgb * charred * _CoreColor.a * k + glow, alpha);
            }
            ENDHLSL
        }
    }
}
