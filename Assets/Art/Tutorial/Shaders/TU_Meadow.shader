// The first-run tutorial's meadow outside the castle walls (R834, 승규 11-25 "성 밖에 있는 잔디 바닥 좀 더 그래픽 좋게"):
// one 4 m grass map read twice - straight at 4 m and turned 36 degrees at 11.3 m - and blended by a slow 96 m variation
// map, so no tile repeats in sight; the same variation tints the grass lush, sun-dried or shaded in broad patches;
// fine blade normals; lit by the main light with its shadows and the ambient probe, with fog. World-space, so the flat
// ring and the hills (ArtSource/Tutorial, TU_Grounds) need no UVs to match.
Shader "ChessFight/Tutorial Meadow"
{
    Properties
    {
        [MainTexture] _BaseMap ("Grass (detail)", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Normal][NoScaleOffset] _BumpMap ("Grass normals", 2D) = "bump" {}
        [NoScaleOffset] _MacroMap ("Variation (r dry, g mix, b shade)", 2D) = "gray" {}
        _Tile ("Detail tile (m)", Float) = 4
        _Tile2 ("Second detail tile (m)", Float) = 11.3
        _MacroTile ("Variation tile (m)", Float) = 96
        _Lush ("Lush tint", Color) = (0.86, 1.0, 0.80, 1)
        _Dry ("Dry tint", Color) = (1.12, 1.02, 0.66, 1)
        _Shade ("Shaded tint", Color) = (0.68, 0.80, 0.70, 1)
        _NormalStrength ("Blade normals", Range(0, 2)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MacroMap); SAMPLER(sampler_MacroMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _Lush, _Dry, _Shade;
            float _Tile, _Tile2, _MacroTile, _NormalStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 w = i.positionWS.xz;
                float2 uv1 = w / _Tile;
                float2 r = float2(w.x * 0.809 - w.y * 0.588, w.x * 0.588 + w.y * 0.809);
                float2 uv2 = r / _Tile2;
                half3 m = SAMPLE_TEXTURE2D(_MacroMap, sampler_MacroMap, w / _MacroTile).rgb;
                half3 c1 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv1).rgb;
                half3 c2 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv2).rgb;
                half mixK = saturate((m.g - 0.3) * 2.2) * 0.65 + 0.15;
                half3 albedo = lerp(c1, c2, mixK);
                half3 tint = lerp(_Lush.rgb, _Dry.rgb, saturate((m.r - 0.55) * 3.0));
                tint = lerp(tint, _Shade.rgb, saturate((0.42 - m.b) * 3.0));
                albedo *= tint * _BaseColor.rgb;

                half3 b1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv1), _NormalStrength);
                half3 b2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv2), _NormalStrength);
                half2 bump = lerp(b1.xy, b2.xy, mixK);
                half3 N = normalize(normalize(i.normalWS) + half3(bump.x, 0, bump.y));

                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ndl = saturate(dot(N, L.direction));
                half3 col = albedo * (L.color * (ndl * L.shadowAttenuation * L.distanceAttenuation) + SampleSH(N));
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 frag(V i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
