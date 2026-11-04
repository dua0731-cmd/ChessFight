// 승규 11-03 ("검은색이 원래 돌 텍스쳐 흰색이 골드색이야", R337): the hall's architectural walls in cream limestone with gold
// trim, the trim read per pixel from the wall module's moulding map (PalaceTrim.png: white gold, black stone) through
// UVs laid straight onto the asset's front (walls_v33.py) - so the gold's edge is the map's, never a triangle's. R349:
// only on faces that look out of that front (FrontGold), not on the walls' tops, ends or the pilasters' sides.
// The stone is PR_PalaceStone.shader's, drawn in world space; the gold is metallic, a little varied, brighter on its
// upper faces. Lit as URP's Lit by Assets/Shaders/URP/CFLit.hlsl.
Shader "ChessFight/Pawn Rush Palace Wall"
{
    Properties
    {
        _TrimMap ("Trim map (white = gold)", 2D) = "black" {}
        _BaseColor ("Stone", Color) = (0.93, 0.87, 0.76, 1)
        _ShadeColor ("Darker patches", Color) = (0.80, 0.71, 0.58, 1)
        _VeinColor ("Veins", Color) = (0.74, 0.62, 0.46, 1)
        _CloudScale ("Patch size (per metre)", Range(0.01, 2)) = 0.18
        _GrainScale ("Grain (per metre)", Range(1, 40)) = 9
        _VeinScale ("Veins (per metre)", Range(0.05, 4)) = 0.45
        _Variation ("Variation", Range(0, 1)) = 0.55
        _Glossiness ("Stone smoothness", Range(0, 1)) = 0.32
        _GoldColor ("Gold", Color) = (0.86, 0.64, 0.28, 1)
        _GoldMetallic ("Gold metallic", Range(0, 1)) = 0.65
        _GoldSmoothness ("Gold smoothness", Range(0, 1)) = 0.7
        _Edge ("Edge softness", Range(0.01, 0.5)) = 0.12
        _Threshold ("Gold line cut (higher = thinner lines; R352)", Range(0.3, 0.9)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _TrimMap_ST;
        half4 _BaseColor, _ShadeColor, _VeinColor, _GoldColor;
        half _CloudScale, _GrainScale, _VeinScale, _Variation, _Glossiness, _GoldMetallic, _GoldSmoothness, _Edge, _Threshold;
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

            TEXTURE2D(_TrimMap); SAMPLER(sampler_TrimMap);

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

            // R349 (승규 11-04 "벽쪽 모서리 부분 끝 부분 위에 부분은 금색으로 칠하지마"): the map is laid straight onto the
            // asset's front, so a face that does not look out of it takes one row or column of the map drawn out across
            // the whole face - the wall's tops (the map's white top band), its ends and the pilasters' sides (a white
            // edge line) came out solid gold. Gold only where the face looks out of the front: not on faces looking up
            // or down, and not where the map's u stands still along the face's level direction (a side face) - judged
            // against v up the face, so however a wall is stretched. 1 on the front, 0 on tops, ends and sides; the
            // face's own plane from the screen derivatives (each triangle on its own), its map gradients by
            // Mikkelsen's surface gradient.
            half FrontGold(float3 p, float2 uv)
            {
                // Every derivative first, outside any branch.
                float3 dp1 = ddx(p), dp2 = ddy(p);
                float2 duv1 = ddx(uv), duv2 = ddy(uv);
                float3 n = cross(dp2, dp1);
                float area = length(n);
                n = area > 1e-12 ? n / area : float3(0, 0, 1);
                half level = 1 - smoothstep(0.55, 0.75, abs(n.y));
                float3 h = cross(float3(0, 1, 0), n);
                float hl = length(h);
                h = hl > 1e-4 ? h / hl : float3(1, 0, 0);
                float3 tv = cross(n, h);
                float3 r1 = cross(dp2, n), r2 = cross(n, dp1);
                float det = dot(dp1, r1);
                det = abs(det) > 1e-20 ? det : 1e-20;
                float3 gu = (r1 * duv1.x + r2 * duv2.x) / det;
                float3 gv = (r1 * duv1.y + r2 * duv2.y) / det;
                float across = abs(dot(gu, h)), rise = abs(dot(gv, tv));
                half side = smoothstep(0.08, 0.2, across / max(rise, 1e-12));
                return hl > 1e-4 ? level * side : 0;
            }

            half4 frag(CFLitVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 p = i.positionWS;
                float cloud = fbm(p * _CloudScale);
                float grain = vnoise(p * _GrainScale) - 0.5;
                float w = fbm(p * _VeinScale + fbm(p * _VeinScale * 0.5) * 2.5);
                float vein = 1 - smoothstep(0.0, 0.035, abs(w - 0.5));
                half3 stone = lerp(_BaseColor.rgb, _ShadeColor.rgb, saturate((cloud - 0.35) * 1.6) * _Variation);
                stone = lerp(stone, _VeinColor.rgb, vein * 0.45 * _Variation);
                stone *= 1 + grain * 0.06 * _Variation;

                float2 trimUV = TRANSFORM_TEX(i.uv, _TrimMap);
                half m = SAMPLE_TEXTURE2D(_TrimMap, sampler_TrimMap, trimUV).r;
                // R352 (승규 11-04 "성 벽 금색 좀 더 테두리 얇게 해"): the map's soft lines cut higher than their middle = thinner gold.
                m = smoothstep(_Threshold - _Edge, _Threshold + _Edge, m) * FrontGold(p, i.uv);
                // R349 (승규 "모서리 부근에 아직 이런 부분 남아 있어"): the pilaster at each end of the module has its outer
                // outline on the module's edge (u .018 .. .039 and .962 .. .977, twice the other lines: 0.8 m of gold up a
                // corner of a 41 m wall). Not those - where two modules meet, their twin pilasters read as one framed.
                m *= smoothstep(0.039, 0.043, trimUV.x) * (1 - smoothstep(0.957, 0.961, trimUV.x));
                half3 gold = _GoldColor.rgb * (0.9 + 0.2 * cloud) * (1 + 0.12 * saturate(i.normalWS.y));

                half3 albedo = lerp(stone, gold, m);
                half metallic = _GoldMetallic * m;
                half smooth = lerp(_Glossiness * (0.85 + 0.3 * cloud), _GoldSmoothness, m);
                return CFShade(i, albedo, metallic, smooth, 1);
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
