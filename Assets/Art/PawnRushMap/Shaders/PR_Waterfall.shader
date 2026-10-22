// Pawn Rush waterfall (승규 10-22 "물이 흐르는 게 아니라 멈춰 있어"): the palace's falls as moving water.
// The ribbon's UV: u across (0..1), v down the fall (one unit = 12 m). Streaks of white water stretched
// down the fall scroll downward in three layers at different speeds, the ribbon's edges fray and
// flicker, the water turns to white spray lower down and thins out at the foot. Transparent, two-sided,
// lit by the sun a little, no textures.
Shader "ChessFight/Pawn Rush Waterfall"
{
    Properties
    {
        _WaterColor ("Water colour", Color) = (0.55, 0.74, 0.86, 0.7)
        _FoamColor ("White water", Color) = (0.97, 0.99, 1, 0.95)
        _Speed ("Fall speed (v per s)", Float) = 2.2
        _StreakWidth ("Streaks across", Float) = 9
        _StreakLength ("Streak length (v)", Float) = 0.5
        _Spray ("Spray from v", Float) = 6
        _Fade ("Fade out by v", Float) = 13
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor, _FoamColor;
                float _Speed, _StreakWidth, _StreakLength, _Spray, _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // value noise, wrapping across u every `wrap` cells so the ribbon has no seam
            float Noise(float2 p, float wrap)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float2 i1 = i + 1;
                i.x = fmod(i.x, wrap); i1.x = fmod(i1.x, wrap);
                float a = Hash(float2(i.x, i.y)), b = Hash(float2(i1.x, i.y));
                float c = Hash(float2(i.x, i1.y)), d = Hash(float2(i1.x, i1.y));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // white streaks: noise stretched down the fall, moving down at `speed`
            float Streaks(float2 uv, float across, float speed, float seed)
            {
                float2 p = float2(uv.x * across, (uv.y - _Time.y * speed) / _StreakLength) + seed;
                float n = Noise(p, 1000) * 0.6 + Noise(p * float2(2.1, 2.3) + 7, 1000) * 0.4;
                return smoothstep(0.42, 0.78, n);
            }

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.uv = v.uv;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag (Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float2 uv = i.uv;
                float fast = Streaks(uv, _StreakWidth, _Speed * 1.15, 0);
                float slow = Streaks(uv, _StreakWidth * 0.55, _Speed * 0.8, 31.7);
                float fine = Streaks(uv, _StreakWidth * 2.3, _Speed * 1.4, 63.1);
                float white = saturate(fast * 0.55 + slow * 0.45 + fine * 0.35);

                // the lower the water, the more it breaks into spray
                float spray = saturate((uv.y - _Spray * 0.25) / _Spray);
                white = saturate(white + spray * 0.65);

                // frayed, flickering edges across the ribbon
                float edgeNoise = Noise(float2(uv.y * 2.0 - _Time.y * _Speed * 0.9, uv.x * 3.0), 1000);
                float across = 1 - abs(uv.x * 2 - 1);
                float edge = smoothstep(0.0, 0.25 + edgeNoise * 0.25, across);
                // a clean lip at the top, fading out at the foot
                float top = smoothstep(0.0, 0.06, uv.y);
                float foot = 1 - smoothstep(_Fade * 0.6, _Fade, uv.y);

                Light sun = GetMainLight();
                float3 n = normalize(i.normalWS) * (front ? 1 : -1);
                half lit = 0.72 + 0.28 * saturate(dot(n, sun.direction));
                half3 col = lerp(_WaterColor.rgb, _FoamColor.rgb, white) * lit * lerp(1, sun.color, 0.4);
                half alpha = lerp(_WaterColor.a, _FoamColor.a, white) * edge * top * foot;
                col = MixFog(col, i.fog);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
