// R375 (승규 11-04, a shot of the round hall's dais drum: "원기둥 쪽에서 회색 부분 아직 텍스쳐가 안 입혀졌고 나선 모양
// 길도 테두리 부분 텍스쳐 입혀줘"): the hall model's grey clay trims - the dais tower's sixteen pilasters, its base and
// cornice rings (HallV15_Royal dais tower (arch)) and the mouldings round the marble floors' edges (HallV15_* moulding) -
// in the walls' cream limestone (as PR_PalaceStone.shader) with gold where the trim's shape calls for it. They carry no
// UVs, so a material places the gold two ways: on faces that look up past _GoldUp (a moulding's top fillet), and in up
// to four height bands measured from the mesh's own origin (the hall's frame: z 0 of the Blender hall), for the
// tower's pilaster feet and capitals, its base fillet and its cornice's corona. The bands' edges, the stone's grain and
// its veins are filtered over a pixel. Lit by Assets/Shaders/URP/CFLit.hlsl.
Shader "ChessFight/Pawn Rush Palace Trim"
{
    Properties
    {
        _BaseColor ("Stone", Color) = (0.93, 0.87, 0.76, 1)
        _ShadeColor ("Darker patches", Color) = (0.80, 0.71, 0.58, 1)
        _VeinColor ("Veins", Color) = (0.74, 0.62, 0.46, 1)
        _CloudScale ("Patch size (per metre)", Range(0.01, 2)) = 0.18
        _GrainScale ("Grain (per metre)", Range(1, 40)) = 9
        _VeinScale ("Veins (per metre)", Range(0.05, 4)) = 0.45
        _Variation ("Variation", Range(0, 1)) = 0.55
        _Glossiness ("Stone smoothness", Range(0, 1)) = 0.32
        _GoldColor ("Gold", Color) = (0.86, 0.64, 0.28, 1)
        _GoldMetallic ("Gold metallic", Range(0, 1)) = 0.85
        _GoldSmoothness ("Gold smoothness", Range(0, 1)) = 0.65
        _GoldUp ("Gold on faces looking up past (cos; over 1 = none)", Range(0, 2)) = 2
        _GoldBandsA ("Gold heights over the mesh origin (m): from, to, from, to", Vector) = (-1000, -1000, -1000, -1000)
        _GoldBandsB ("More gold heights: from, to, from, to", Vector) = (-1000, -1000, -1000, -1000)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor, _ShadeColor, _VeinColor, _GoldColor;
        float4 _GoldBandsA, _GoldBandsB;
        half _CloudScale, _GrainScale, _VeinScale, _Variation, _Glossiness, _GoldMetallic, _GoldSmoothness, _GoldUp;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex CFLitVertex
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

            // How much of the pixel lies in [a, b] of h (w: h's change over a pixel).
            float Band(float h, float w, float a, float b) { return saturate((h - a) / w + 0.5) * saturate((b - h) / w + 0.5); }

            half4 frag(CFLitVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 p = i.positionWS;
                // The face's own (flat) normal, from the position's screen derivatives: a fillet looks up whatever the
                // mesh's smoothing.
                float3 c = cross(ddy(p), ddx(p));
                float3 face = c * rsqrt(max(dot(c, c), 1e-20));
                if (dot(face, i.normalWS) < 0) face = -face;
                float h = p.y - TransformObjectToWorld(float3(0, 0, 0)).y;
                float hw = max(fwidth(h), 1e-4);
                float gold = face.y > _GoldUp ? 1 : 0;
                gold = max(gold, max(Band(h, hw, _GoldBandsA.x, _GoldBandsA.y), Band(h, hw, _GoldBandsA.z, _GoldBandsA.w)));
                gold = max(gold, max(Band(h, hw, _GoldBandsB.x, _GoldBandsB.y), Band(h, hw, _GoldBandsB.z, _GoldBandsB.w)));

                // PR_PalaceStone's limestone: broad clouds, fine grain, faint veins - the grain and veins fading where a
                // pixel holds more than one of them.
                float cloud = fbm(p * _CloudScale);
                float grain = (vnoise(p * _GrainScale) - 0.5) * saturate(1.5 - length(fwidth(p)) * _GrainScale);
                float3 q = p * _VeinScale;
                float w = fbm(q + fbm(q * 0.5) * 2.5);
                float wAA = fwidth(w);
                float vein = (1 - smoothstep(0.0, 0.035 + wAA, abs(w - 0.5))) * saturate(1 - wAA * 12);
                half3 stone = lerp(_BaseColor.rgb, _ShadeColor.rgb, saturate((cloud - 0.35) * 1.6) * _Variation);
                stone = lerp(stone, _VeinColor.rgb, vein * 0.45 * _Variation);
                stone *= 1 + grain * 0.06 * _Variation;

                half3 albedo = lerp(stone, _GoldColor.rgb * (0.94 + 0.12 * cloud), gold);
                half smoothness = lerp(_Glossiness * (0.85 + 0.3 * cloud), _GoldSmoothness, gold);
                return CFShade(i, albedo, gold * _GoldMetallic, smoothness, 1);
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
