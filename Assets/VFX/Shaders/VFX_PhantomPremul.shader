// A dark afterimage of a piece (R164, 10-27, 사용자: "잔상도 다시 디자인해줘"): its own shape, near black and see-through,
// with a dark red rim where it turns away from the view, burning away into a 3D noise as _Dissolve rises - the edge of
// what is left glows red as it goes. Premultiplied blending: the body darkens what is behind it, the rim and the burning
// edge add light. VfxPhantom sets _Fade, _Dissolve and _Intensity per renderer.
Shader "ChessFight/VFX/Phantom Premultiplied"
{
    Properties
    {
        _CoreColor ("Body colour (alpha = how solid)", Color) = (0.03, 0.01, 0.01, 0.6)
        [HDR] _Color ("Rim colour (HDR)", Color) = (0.6, 0.07, 0.06, 1)
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _RimPower ("Rim power", Range(0.5, 8)) = 2.5
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _NoiseScale ("Noise cells a metre", Float) = 7
        _BurnWidth ("Burning edge width", Range(0.01, 0.3)) = 0.08
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _Color;
                half _Intensity;
                half _Fade;
                half _RimPower;
                half _Dissolve;
                float _NoiseScale;
                half _BurnWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            float Hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise3(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash3(i), Hash3(i + float3(1, 0, 0)), u.x),
                                 lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), u.x), u.y),
                            lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), u.x),
                                 lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), u.x), u.y), u.z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 q = input.positionOS * _NoiseScale;
                float n = Noise3(q) * 0.65 + Noise3(q * 2.3 + 11.7) * 0.35;
                float left = n - _Dissolve * 1.05;
                half keep = step(0.0, left);
                half burning = keep * (1.0 - saturate(left / _BurnWidth)) * step(0.001, _Dissolve);
                half facing = saturate(dot(normalize(input.normalWS), normalize(input.viewWS)));
                half rim = pow(1.0 - facing, _RimPower);
                half alpha = _CoreColor.a * _Fade * keep * lerp(0.7, 1.0, rim);
                half3 glow = _Color.rgb * _Intensity * (rim + burning * 2.0) * _Fade * keep;
                return half4(_CoreColor.rgb * alpha + glow, alpha);
            }
            ENDHLSL
        }
    }
}
