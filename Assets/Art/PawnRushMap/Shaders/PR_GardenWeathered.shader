// R480 (승규 11-06, the garden's hand-made pass, step 4: "타일링이 반복돼 보이는 면은 ... 2~3종 텍스처를 섞어서 반복감을
// 없앨 것. 얼룩, 먼지, 이끼, 녹, 물때를 중력과 환경 논리에 맞게 ... 러프니스 · 컬러에 오브젝트별 미세한 랜덤 변화"): the garden
// dressing's stone, tiles and joints. Their GC_ material's picture, colour and smoothness, plus the weathering that
// ArtSource/PawnRush/GardenConcept/handmade_pass.py --step 4 baked into the vertex colours:
//   R  the piece's own tint and roughness (0.5 = as made)
//   G  grime: low on faces near the floor or ledge under them, under ledges, a little on floors
//   B  moss: low and damp, at the water's levels
//   A  water streaks: down a face from the ledge or top over it
// broken up by world-space noise (streaks stretched downward, moss in clumps), and the picture drawn twice - turned and
// rescaled - mixed by a broad noise so its tiling does not show. Lit as URP's Lit by Assets/Shaders/URP/CFLit.hlsl.
Shader "ChessFight/Garden Weathered"
{
    Properties
    {
        _BaseMap ("Picture", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
        _GrimeColor ("Grime (multiplies)", Color) = (0.56, 0.50, 0.44, 1)
        _MossColor ("Moss", Color) = (0.30, 0.38, 0.17, 1)
        _StreakColor ("Water streaks (multiply)", Color) = (0.72, 0.69, 0.66, 1)
        _Weather ("Weathering", Range(0, 1)) = 1
        _AntiTile ("Break up the tiling", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor, _GrimeColor, _MossColor, _StreakColor;
        half _Smoothness, _Weather, _AntiTile;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Assets/Shaders/URP/CFLit.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fog : TEXCOORD3;
                half4 weather : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.weather = v.color;
                return o;
            }

            float hash3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }

            float vnoise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(hash3(i), hash3(i + float3(1, 0, 0)), f.x), lerp(hash3(i + float3(0, 1, 0)), hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(hash3(i + float3(0, 0, 1)), hash3(i + float3(1, 0, 1)), f.x), lerp(hash3(i + float3(0, 1, 1)), hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float fbm(float3 p)
            {
                float s = 0, a = 0.5;
                [unroll] for (int k = 0; k < 4; k++) { s += a * vnoise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return s;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 p = i.positionWS;
                half4 w = i.weather;
                // The picture twice, the second turned and rescaled, mixed by a broad noise: no tiling to see.
                half3 a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                float2 uv2 = float2(i.uv.x * 0.62 - i.uv.y * 0.47, i.uv.x * 0.47 + i.uv.y * 0.62) * 1.13 + 0.37;
                half3 b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv2).rgb;
                float mixer = smoothstep(0.38, 0.62, fbm(p * 0.21));
                half3 c = lerp(a, b, mixer * _AntiTile) * _BaseColor.rgb;
                // Broad discolouring and each piece's own tint.
                c *= lerp(0.94, 1.04, fbm(p * 0.07 + 3.1));
                c *= 1 + (w.r - 0.5) * 0.14;
                // Grime, in blotches.
                float g = saturate(w.g * (0.55 + 0.9 * fbm(p * 0.85 + 7.3))) * _Weather;
                c = lerp(c, c * _GrimeColor.rgb, g);
                // Water streaks: thin runs down the face (stretched noise), strongest just under the ledge.
                float runs = smoothstep(0.42, 0.78, vnoise(float3(p.x * 2.6 + p.z * 1.9, p.y * 0.22, p.z * 2.6 - p.x * 1.3)));
                float s = saturate(w.a * runs * 1.2) * _Weather;
                c = lerp(c, c * _StreakColor.rgb, s);
                // Moss in clumps, more on what faces up.
                float up = saturate(i.normalWS.y * 0.6 + 0.6);
                float m = saturate(w.b * smoothstep(0.42, 0.66, fbm(p * 1.35 + 11.7)) * (0.6 + 0.6 * up) * 1.3) * _Weather;
                c = lerp(c, _MossColor.rgb * (0.8 + 0.4 * vnoise(p * 7.0)), m);
                half smooth = _Smoothness * (1 + (w.r - 0.5) * 0.3) - 0.12 * g - 0.15 * m + 0.18 * s;
                CFLitVaryings l = (CFLitVaryings)0;
                l.positionCS = i.positionCS;
                l.positionWS = i.positionWS;
                l.normalWS = i.normalWS;
                l.uv = i.uv;
                l.fog = i.fog;
                return CFShade(l, c, 0, saturate(smooth), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex CFShadowVertex
            #pragma fragment CFShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Assets/Shaders/URP/CFShadowCaster.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex CFDepthVertex
            #pragma fragment CFShadowFragment
            #pragma multi_compile_instancing
            #include "Assets/Shaders/URP/CFShadowCaster.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
