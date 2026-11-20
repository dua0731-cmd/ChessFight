// R725 ro-vfx2 (the variable star's petals, PLAN 5 "펴질 때 빛 번짐"): gold light bleeding over a petal's top as the
// petals come back up together. A fan laid a hair over the petal's top that rides the petal (RoyalObservatoryVfxPetals
// makes it): uv.x across the petal (0 to 1), uv.y out from the hinge (0) to the rim (1), uv2.x metres to the petal's
// nearest edge (the seams by its neighbours, the rim and the hinge).
//   seams    the light comes first out of the hinge and runs out along the edges, as if it had been held in them
//   front    a soft bright line moving out from the hinge to the rim with the rise (_Front 0 -> 1)
//   wash     a faint glow over the top behind the front, so the motif shows through
//   settle   a short hot flash along the edges as the petal locks level (_Settle 1 -> 0)
// Added onto what is behind it; depth tested, not written.
Shader "ChessFight/VFX/RO Petal Bleed"
{
    Properties
    {
        [HDR] _Color ("Bleed (HDR)", Color) = (1, 0.66, 0.26, 1)
        [HDR] _HotColor ("Front and settle (HDR)", Color) = (1, 0.9, 0.66, 1)
        _Light ("Light", Float) = 1
        _Front ("Front (0 hinge, 1 rim)", Range(0, 1.3)) = 0
        _Settle ("Settle flash", Range(0, 1)) = 0
        _Edge ("Edge glow width (m)", Float) = 0.16
        _Wash ("Wash over the top", Range(0, 1)) = 0.16
        _Age ("Age (s)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _HotColor;
                half _Light;
                half _Front;
                half _Settle;
                float _Edge;
                half _Wash;
                float _Age;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 edge : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float edge : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.edge = input.edge.x;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float v = input.uv.y, u = input.uv.x;
                // How far the bleed has come: everything behind the front, softly.
                half behind = 1.0 - smoothstep(_Front - 0.22, _Front + 0.04, v);
                half front = exp(-abs(v - _Front) / 0.045) * step(_Front, 1.08);
                half edge = exp(-input.edge / max(_Edge, 0.01));
                // a slow shimmer running out along the petal, so it reads as light moving, not a flat sheet
                half shimmer = 0.78 + 0.22 * sin(v * 17.0 - _Age * 7.0 + u * 4.0);
                half bleed = (edge * 0.95 + _Wash) * behind * shimmer + front * (0.35 + edge * 0.6);
                half settle = _Settle * (edge * 1.3 + _Wash * 1.2);
                half3 col = _Color.rgb * bleed + _HotColor.rgb * (front * 0.5 + settle);
                return half4(col * _Light, 0.0);
            }
            ENDHLSL
        }
    }
}
