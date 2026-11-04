// R372 (승규 11-04, the dotted rings stayed through R366 ~ R370: "그럼 한번 미니맵에 있는 걸로 해서 해봐 칸 사이즈는
// 줄이고", a shot of the mini-game row's floor): the round hall's spiral way paved as the mini-games' floor - the course
// kit's PRC_Floor_Checker (CoursePattern.shader): light and near-black squares laid on the world's axes, no veins, no
// joints - in 1 m squares instead of its 2 m. The squares are box-filtered over a pixel so far ones blend instead of
// shimmering. R375 ("나선 모양 길도 테두리 부분 텍스쳐 입혀줘"): the way's border dressed. Each band of the way is a
// 0.6 m (the two stairs 1.2 m) thick slab whose edges are rounded over (two facets, 5 cm) with smoothed normals, so its own
// normals tell its parts apart: straight up on the walked top (the checker), leaning out over the rounded top edge (gold,
// a gilded nosing along both sides and round the landings' ends), level on the sides and down underneath (the walls'
// cream limestone of PR_PalaceStone.shader, its grain and veins filtered over a pixel). Lit by
// Assets/Shaders/URP/CFLit.hlsl, as CoursePattern.shader, but with no SSAO (R369: rebuilt from depth alone, it rings on a
// broad top seen low), and the way casts its shadow from _ShadowDrop under itself, so its own top never shadows itself
// (acne) while what stands on it still does.
Shader "ChessFight/Pawn Rush Spiral Checker"
{
    Properties
    {
        _Color ("Checker light (PRC_Floor_Checker)", Color) = (0.8, 0.79, 0.76, 1)
        _Color2 ("Checker dark (PRC_Floor_Checker)", Color) = (0.1, 0.1, 0.13, 1)
        _Cell ("Checker square (m)", Range(0.25, 4)) = 1
        _Smoothness ("Checker smoothness", Range(0, 1)) = 0.7
        _TopCos ("Checker where the normal looks up past (cos)", Range(0.9, 1)) = 0.985
        _GoldColor ("Edge gold", Color) = (0.86, 0.64, 0.28, 1)
        _GoldMetallic ("Edge gold metallic", Range(0, 1)) = 0.85
        _GoldSmoothness ("Edge gold smoothness", Range(0, 1)) = 0.65
        _EdgeCos ("Gold down the rounded edge to (cos)", Range(0, 0.5)) = 0.02
        _BaseColor ("Side stone", Color) = (0.93, 0.87, 0.76, 1)
        _ShadeColor ("Side stone patches", Color) = (0.80, 0.71, 0.58, 1)
        _VeinColor ("Side stone veins", Color) = (0.74, 0.62, 0.46, 1)
        _CloudScale ("Patch size (per metre)", Range(0.01, 2)) = 0.18
        _GrainScale ("Grain (per metre)", Range(1, 40)) = 9
        _VeinScale ("Veins (per metre)", Range(0.05, 4)) = 0.45
        _Variation ("Stone variation", Range(0, 1)) = 0.55
        _SideSmoothness ("Side stone smoothness", Range(0, 1)) = 0.32
        _ShadowDrop ("Shadow cast from this far under the way (m)", Range(0, 0.5)) = 0.2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _Color, _Color2, _GoldColor, _BaseColor, _ShadeColor, _VeinColor;
        half _Cell, _Smoothness, _TopCos, _GoldMetallic, _GoldSmoothness, _EdgeCos;
        half _CloudScale, _GrainScale, _VeinScale, _Variation, _SideSmoothness, _ShadowDrop;
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

            half4 frag(CFLitVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 p = i.positionWS;
                float3 n = normalize(i.normalWS);
                // Worked out before the branches (derivatives): CoursePattern's checker (light where floor(x) + floor(z)
                // is even), its edges box-filtered over the pixel's footprint; the limestone's grain and veins, faded
                // where a pixel holds more than one of them.
                float2 cuv = p.xz / _Cell;
                float2 fw = max(fwidth(cuv), 1e-4);
                float2 tri = 2.0 * (abs(frac((cuv - 0.5 * fw) * 0.5) - 0.5) - abs(frac((cuv + 0.5 * fw) * 0.5) - 0.5)) / fw;
                float cloud = fbm(p * _CloudScale);
                float grain = (vnoise(p * _GrainScale) - 0.5) * saturate(1.5 - length(fwidth(p)) * _GrainScale);
                float3 q = p * _VeinScale;
                float w = fbm(q + fbm(q * 0.5) * 2.5);
                float wAA = fwidth(w);
                float vein = (1 - smoothstep(0.0, 0.035 + wAA, abs(w - 0.5))) * saturate(1 - wAA * 12);
                if (n.y > _TopCos)
                {
                    // The walked top (level, or the ramps' few degrees), lit flat.
                    half3 albedo = lerp(_Color.rgb, _Color2.rgb, saturate(0.5 - 0.5 * tri.x * tri.y));
                    CFLitVaryings top = i;
                    top.normalWS = float3(0, 1, 0);
                    return CFShade(top, albedo, 0, _Smoothness, 1);
                }
                if (n.y > _EdgeCos)
                    return CFShade(i, _GoldColor.rgb * (0.94 + 0.12 * cloud), _GoldMetallic, _GoldSmoothness, 1);
                half3 stone = lerp(_BaseColor.rgb, _ShadeColor.rgb, saturate((cloud - 0.35) * 1.6) * _Variation);
                stone = lerp(stone, _VeinColor.rgb, vein * 0.45 * _Variation);
                stone *= 1 + grain * 0.06 * _Variation;
                return CFShade(i, stone, 0, _SideSmoothness * (0.85 + 0.3 * cloud), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment CFShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Assets/Shaders/URP/CFShadowCaster.hlsl"

            // CFShadowVertex with the mesh lowered _ShadowDrop: the way's bands are 0.6 m thick ramps with no steps, so
            // the lowered band still fills the way's own volume and its shadow below moves a few centimetres.
            CFShadowVaryings ShadowVertex(CFShadowAttributes v)
            {
                CFShadowVaryings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz) - float3(0, _ShadowDrop, 0);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = positionCS;
                return o;
            }
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
