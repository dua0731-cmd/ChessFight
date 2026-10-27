// The shared effect surface (R159, 10-27): an HDR colour added onto what is behind it, broken up by a scrolling noise
// that also eats it away as _Dissolve rises (0 = whole, 1 = gone). Rings, bands and ribbons use it along their UVs:
// u runs along the shape, v across it. The noise wraps every _NoiseScale.x cells along u, so a closed ring has no seam.
// _Fade, _Dissolve, _Intensity and _UFade are usually set per renderer by the component that animates it (VfxRing).
Shader "ChessFight/VFX/Scroll Dissolve Additive"
{
    Properties
    {
        [HDR] _Color ("Color (HDR)", Color) = (1, 0.76, 0.38, 1)
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _ScrollSpeed ("Scroll (cells per second, u v)", Vector) = (1.5, 0, 0, 0)
        _NoiseScale ("Noise cells (u v; u a whole number)", Vector) = (12, 2, 0, 0)
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _EdgeSoftness ("Dissolve edge softness", Range(0.01, 0.5)) = 0.15
        _UFade ("Fade at the u ends", Range(0, 0.5)) = 0
        _VFade ("Fade at the v edges", Range(0, 0.5)) = 0.35
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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Fade;
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
                half fade = 1.0;
                if (_UFade > 0.001) fade *= smoothstep(0.0, _UFade, input.uv.x) * smoothstep(0.0, _UFade, 1.0 - input.uv.x);
                if (_VFade > 0.001) fade *= smoothstep(0.0, _VFade, input.uv.y) * smoothstep(0.0, _VFade, 1.0 - input.uv.y);
                half3 col = _Color.rgb * _Intensity * input.color.rgb * input.color.a * lerp(0.55, 1.25, n);
                return half4(col * mask * fade * _Fade, 1.0);
            }
            ENDHLSL
        }
    }
}
