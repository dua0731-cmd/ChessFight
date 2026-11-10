// R568 (승규 11-10 "지금은 작은 불씨들이 모아져서 그게 불길을 만들잖아 그게 아니라 진짜 불길처럼 해줘"): one particle is
// one whole flame, not a dot of it. A tall sheet whose fire is drawn here: a heat field, hot at the foot and the middle,
// eaten away upward by domain-warped noise that flows up through it, cut with a sharp edge into licking tongues that
// sway more the higher they reach, coloured by its heat (white-gold core, orange, deep red tips). Premultiplied light
// (Blend One OneMinusSrcAlpha), so a few sheets overlapping read as one fire instead of a white blob.
// Particle streams (CavalryWarnFx.Fire.cs): Position, Color (alpha = its life's fade), UV in TEXCOORD0.xy, StableRandom.x
// in .z (its own pattern), AgePercent in .w (it shrinks as it burns out).
Shader "ChessFight/VFX/Flame Sheet"
{
    Properties
    {
        _CoreColor ("Core", Color) = (1, 0.8, 0.42, 1)
        _MidColor ("Middle", Color) = (1, 0.28, 0.035, 1)
        _EdgeColor ("Tips", Color) = (0.5, 0.05, 0.008, 1)
        _Intensity ("Brightness (HDR)", Float) = 1.4
        _Opacity ("How much it hides what is behind", Range(0, 1)) = 0.22
        _Speed ("Flow up (per s)", Float) = 2.2
        _Sharp ("Edge sharpness", Float) = 12
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Pass
        {
            Name "Flame"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor, _MidColor, _EdgeColor;
                half _Intensity, _Opacity, _Speed, _Sharp;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float s = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    s += a * Noise(p);
                    p = p * 2.03 + float2(1.7, 9.2);
                    a *= 0.5;
                }
                return s;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv.xy;
                float seed = i.uv.z * 61.0;
                float age = i.uv.w;
                float t = _Time.y * _Speed + seed;

                // Noise flowing up through the sheet, warped by a slower one: the tongues' shapes and their sway.
                float2 p = float2(uv.x * 1.7, uv.y * 1.25);
                float warp = Fbm(p * 1.6 + float2(seed, -t * 0.8));
                float n = Fbm(float2(p.x * 4.2, p.y * 3.0) + float2(warp * 1.4 + seed * 1.3, -t * 2.0));

                // Its outline: wide at the foot, narrowing up, swaying and breathing with the slow noise.
                float x = uv.x - 0.5 + (warp - 0.5) * 0.55 * uv.y;
                float width = lerp(0.46, 0.17, pow(saturate(uv.y), 0.8)) * (0.75 + 0.55 * warp);
                float body = 1.0 - saturate(abs(x) / width);
                body = body * body * (3.0 - 2.0 * body);

                // Hot low down, eaten into tongues higher up; nothing outside its outline.
                float heat = body * (1.3 - 1.35 * uv.y + (n - 0.5) * (0.9 + 1.1 * uv.y));
                heat *= smoothstep(0.0, 0.24, uv.y) * (1.0 - smoothstep(0.82, 1.0, uv.y));   // soft foot, no cut top
                heat *= 1.0 - age * age * 0.75;               // it dies down as it burns out
                heat *= i.color.a;

                float f = saturate((heat - 0.08) * _Sharp);
                float hc = heat * (0.75 + 0.5 * n);           // streaks of brighter and duller fire inside
                half3 c = lerp(_EdgeColor.rgb, _MidColor.rgb, smoothstep(0.06, 0.5, hc));
                c = lerp(c, _CoreColor.rgb, smoothstep(0.75, 1.25, hc));
                half3 rgb = c * f * _Intensity * i.color.rgb;
                return half4(rgb, f * _Opacity);
            }
            ENDHLSL
        }
    }
}
