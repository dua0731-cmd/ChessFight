// The Royal Observatory's abyss under the hall (R6, ro_r6_hall_model.py abyss_layers; RoyalObservatoryDressing.Hall.cs).
// The user (11-19) on the first motion, each layer turned whole: "이게 너무 뭉탱이로 움직여". So nothing turns whole any
// more; the movement is in the picture:
//   swirl  - the texture winds round the disc's middle, faster inside than out (a vortex, not a turntable). Two
//            samples half a cycle apart are blended (a flow map's trick), so it flows on and never winds up.
//   warp   - slow value-noise distortion: the gas billows in place.
//   twinkle- each star (a cell of the texture) brightens and dims on its own beat.
// Unlit (the abyss is lit by nothing); opaque or additive by the material's blend (Hall.cs sets them).
Shader "ChessFight/RoyalObservatory/Abyss Layer"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "black" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Swirl ("Swirl (radians a cycle at the middle)", Float) = 0
        _SwirlSpeed ("Swirl cycles a second", Float) = 0.03
        _Warp ("Billow (uv)", Float) = 0
        _WarpSpeed ("Billow speed", Float) = 0.03
        _Twinkle ("Twinkle", Range(0, 1)) = 0
        _TwinkleSpeed ("Twinkle speed", Float) = 1.5
        _TwinkleCells ("Twinkle cells across the texture", Float) = 120
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Swirl, _SwirlSpeed, _Warp, _WarpSpeed, _Twinkle, _TwinkleSpeed, _TwinkleCells;
                float _SrcBlend, _DstBlend, _ZWrite;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            // The disc's middle is uv (0.5, 0.5) on every layer (ro_r6_hall_model._disc); the turn falls off outward.
            float2 Swirled(float2 uv, float phase)
            {
                float2 c = uv - 0.5;
                float a = _Swirl * (phase - 0.5) / (length(c) * 4 + 0.35);
                float s = sin(a), k = cos(a);
                return 0.5 + float2(c.x * k - c.y * s, c.x * s + c.y * k);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;
                if (_Warp > 0)
                {
                    float2 w = float2(Noise(uv * 3 + t * _WarpSpeed), Noise(uv * 3 + 17.3 - t * _WarpSpeed)) - 0.5;
                    uv += w * _Warp;
                }
                half4 col;
                if (_Swirl != 0)
                {
                    float p1 = frac(t * _SwirlSpeed), p2 = frac(t * _SwirlSpeed + 0.5);
                    half4 a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, Swirled(uv, p1));
                    half4 b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, Swirled(uv, p2));
                    float wa = 1 - abs(1 - 2 * p1);          // 0 where a's cycle restarts, 1 half way
                    col = a * wa + b * (1 - wa);
                }
                else col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                col *= _BaseColor;
                if (_Twinkle > 0)
                {
                    float h = Hash(floor(i.uv * _TwinkleCells));
                    col.rgb *= 1 - _Twinkle * (0.5 + 0.5 * sin(t * _TwinkleSpeed * (0.5 + h) + h * 6.2831));
                }
                return col;
            }
            ENDHLSL
        }
    }
}
