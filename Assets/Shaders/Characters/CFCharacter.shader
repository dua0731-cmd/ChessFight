// The look of the pawn and the chess pieces (JY 11-09: Tripo's cartoon shader, "카툰 느낌을 유지하되 조금만 더
// 부드러우면", then the studio render that matched the design sheet: a big soft key light, warm shadows, low contrast).
// A soft cel shade, lit the same on every map:
//   - the main light wraps round the form and steps softly into shade (_Wrap, _ShadeEdge, _ShadeSoftness); the shade is
//     not dark grey but the colour times a warm tint (_ShadeColor), and cast shadows fall into that same shade
//   - the sky's ambient on top, held down (_Ambient), a soft highlight (_Gloss) and a thin rim on the lit side
//   - other lights add their own soft step, without the shade floor
//   - with the stripe atlas (RigWork/scripts/pieces/game_export.py: eight stripes across u - body, hands and feet, eye,
//     glint, blush, brow, two spare) the face reads as drawn: eyes glossy (_EyeGloss), eyes, glints and blush partly
//     self-lit (_EyeEmit, _GlintEmit, _BlushEmit) so they keep their colour on the shaded side. _Stripes off for a
//     painted texture (the king's and queen's).
// The teams are two materials on the same mesh: white (cream body, black eyes, white glints) and black (charcoal body,
// ivory eyes without glints), Resources/PieceModels/Black. Shadows and depth as ChessFight's other URP shaders.
Shader "ChessFight/Character"
{
    Properties
    {
        [MainTexture] _BaseMap ("Colours (stripe atlas or texture)", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade (multiplies the colour)", Color) = (0.74, 0.62, 0.57, 1)
        _ShadeEdge ("Where the shade starts", Range(0, 1)) = 0.4
        _ShadeSoftness ("Shade edge softness", Range(0.01, 0.5)) = 0.2
        _Wrap ("Light wrap", Range(0, 1)) = 0.3
        _Ambient ("Sky ambient", Range(0, 1)) = 0.35
        _Gloss ("Highlight", Range(0, 1)) = 0.1
        _GlossSize ("Highlight tightness", Range(4, 128)) = 24
        _RimColor ("Rim colour", Color) = (1, 0.95, 0.88, 1)
        _Rim ("Rim", Range(0, 1)) = 0.1
        [ToggleUI] _Stripes ("Stripe atlas (face details)", Float) = 1
        _EyeGloss ("Eye highlight (a small spot)", Range(0, 2)) = 0.6
        _EyeEmit ("Eyes self-lit", Range(0, 1)) = 0.2
        _GlintEmit ("Glints self-lit", Range(0, 1)) = 0.5
        _BlushEmit ("Blush self-lit", Range(0, 1)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor, _ShadeColor, _RimColor;
        half _ShadeEdge, _ShadeSoftness, _Wrap, _Ambient, _Gloss, _GlossSize, _Rim;
        half _Stripes, _EyeGloss, _EyeEmit, _GlintEmit, _BlushEmit;
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fog : TEXCOORD3;
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
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // One light, softly stepped. The main light's shade keeps the colour times _ShadeColor (the warm fill the
            // design's studio light gives); another light adds nothing on its far side.
            // The eyes' highlight is four times tighter: as wide as the body's, it lit a whole flat eye grey.
            half3 Shade(Light light, float3 n, float3 v, half3 albedo, half gloss, half size, half floorShade)
            {
                half wrapped = (dot(n, light.direction) + _Wrap) / (1 + _Wrap);
                half lit = smoothstep(_ShadeEdge - _ShadeSoftness, _ShadeEdge + _ShadeSoftness, wrapped) * light.shadowAttenuation;
                half3 diffuse = albedo * lerp(_ShadeColor.rgb * floorShade, half3(1, 1, 1), lit);
                float3 h = SafeNormalize(light.direction + v);
                half spec = pow(saturate(dot(n, h)), size) * gloss * lit;
                half rim = pow(1 - saturate(dot(n, v)), 4) * _Rim * lit;
                return (diffuse + spec + rim * _RimColor.rgb) * light.color * light.distanceAttenuation;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, TRANSFORM_TEX(i.uv, _BaseMap)).rgb * _BaseColor.rgb;
                float3 n = NormalizeNormalPerPixel(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // the face details by the stripe their faces sit on
                half stripe = _Stripes > 0.5 ? floor(saturate(i.uv.x) * 7.999) : 0;
                half eye = (stripe == 2 || stripe == 5) ? 1 : 0;
                half emit = eye * _EyeEmit + (stripe == 3 ? _GlintEmit : 0) + (stripe == 4 ? _BlushEmit : 0);
                half gloss = lerp(_Gloss, _EyeGloss, eye);
                half size = _GlossSize * (1 + 3 * eye);

                half4 shadowMask = half4(1, 1, 1, 1);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS), i.positionWS, shadowMask);
                half3 c = Shade(mainLight, n, v, albedo, gloss, size, 1);

                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = v;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint pixelLightCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    c += Shade(GetAdditionalLight(lightIndex, i.positionWS, shadowMask), n, v, albedo, gloss, size, 0);
                }
                #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    c += Shade(GetAdditionalLight(lightIndex, i.positionWS, shadowMask), n, v, albedo, gloss, size, 0);
                LIGHT_LOOP_END
                #endif

                c += SampleSH(n) * albedo * _Ambient;
                c = lerp(c, albedo, saturate(emit));
                c = MixFog(c, i.fog);
                return half4(c, 1);
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
