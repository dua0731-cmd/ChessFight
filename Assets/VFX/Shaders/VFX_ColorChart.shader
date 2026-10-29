// A colour chart for checking the capture pipeline (R172, 10-27): four rows of the reference colours (crimson, cosmic
// blue, gold, white) and columns of brightness from _MinStop to _MaxStop in stops (each stop doubles it, as the HDR colour
// picker's Intensity), drawn on one quad from its UVs. Captured with ACES and Neutral it shows where each colour starts to
// bloom and how far it bleaches toward white. Unlit, opaque by default (blend modes on the material).
Shader "ChessFight/VFX/Color Chart"
{
    Properties
    {
        [HDR] _Color1 ("Row 1 (crimson)", Color) = (1, 0.02, 0.02, 1)
        [HDR] _Color2 ("Row 2 (cosmic blue)", Color) = (0.25, 0.45, 1, 1)
        [HDR] _Color3 ("Row 3 (gold)", Color) = (1, 0.7, 0.3, 1)
        [HDR] _Color4 ("Row 4 (white)", Color) = (1, 1, 1, 1)
        _MinStop ("Left column (stops)", Float) = 0
        _MaxStop ("Right column (stops)", Float) = 4
        _Columns ("Columns", Range(2, 16)) = 9
        _Gap ("Gap between cells", Range(0, 0.4)) = 0.1
        _GapColor ("Gap colour", Color) = (0.01, 0.01, 0.01, 1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Toggle] _ZWrite ("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color1;
                half4 _Color2;
                half4 _Color3;
                half4 _Color4;
                float _MinStop;
                float _MaxStop;
                float _Columns;
                half _Gap;
                half4 _GapColor;
                float _SrcBlend;
                float _DstBlend;
                float _ZWrite;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float columns = max(2.0, floor(_Columns));
                float2 cell = float2(input.uv.x * columns, (1.0 - input.uv.y) * 4.0);
                float2 inCell = frac(cell);
                float2 index = min(floor(cell), float2(columns - 1.0, 3.0));
                half3 base = index.y < 0.5 ? _Color1.rgb : index.y < 1.5 ? _Color2.rgb : index.y < 2.5 ? _Color3.rgb : _Color4.rgb;
                float stops = lerp(_MinStop, _MaxStop, index.x / (columns - 1.0));
                half inside = step(_Gap * 0.5, inCell.x) * step(inCell.x, 1.0 - _Gap * 0.5)
                    * step(_Gap * 0.5, inCell.y) * step(inCell.y, 1.0 - _Gap * 0.5);
                return half4(lerp(_GapColor.rgb, base * exp2(stops), inside), 1.0);
            }
            ENDHLSL
        }
    }
}
