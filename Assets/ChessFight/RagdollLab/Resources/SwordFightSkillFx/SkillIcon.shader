// A piece's mark over a caster's head (Sword Fight skills R102, design A "잉크 테두리 장난감 체스", the five icons
// 승규 님 sent): the icon traced into two distance fields (r: its outline filled in, g: its dark shape with the light
// lines cut out), drawn flat in the piece's colour with its light lines, an ink edge and a cream sticker rim round it.
// _Flash turns it white. No glow. Built-in render pipeline.
Shader "ChessFight/Skill Icon"
{
    Properties
    {
        _MainTex ("Distance fields (r outline, g shape)", 2D) = "black" {}
        _Fill ("Fill", Color) = (1, 0.83, 0.3, 1)
        _Lines ("Lines", Color) = (1, 0.95, 0.8, 1)
        _InkColor ("Ink", Color) = (0.1, 0.1, 0.2, 1)
        _Rim ("Rim", Color) = (1, 0.96, 0.86, 1)
        _Spread ("Field spread (icon heights)", Float) = 0.12
        _InkWidth ("Ink (icon heights)", Float) = 0.045
        _RimWidth ("Rim (icon heights)", Float) = 0.03
        _LineGrow ("Lines widened (icon heights)", Float) = 0
        _Flash ("White", Range(0, 1)) = 0
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+50" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        ZTest LEqual
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Fill, _Lines, _InkColor, _Rim;
            float _Spread, _InkWidth, _RimWidth, _LineGrow, _Flash, _Alpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 s = tex2D(_MainTex, i.uv).rg;
                float outline = (s.r - 0.5) * 2 * _Spread;   // + inside the icon's outline
                float shape = (s.g - 0.5) * 2 * _Spread;     // + inside its dark shape
                float aa = max(fwidth(outline), 1e-4);
                float aa2 = max(fwidth(shape), 1e-4);
                float inRim = smoothstep(-aa, aa, outline + _InkWidth + _RimWidth);
                float inInk = smoothstep(-aa, aa, outline + _InkWidth);
                float inFill = smoothstep(-aa, aa, outline);
                // The light lines: the holes in the dark shape (kept off the outer edge if they are widened).
                float lines = (1 - smoothstep(-aa2, aa2, shape - _LineGrow)) * smoothstep(-aa, aa, outline - 2 * _LineGrow);
                float3 col = lerp(_Rim.rgb, _InkColor.rgb, inInk);
                col = lerp(col, lerp(_Fill.rgb, _Lines.rgb, lines), inFill);
                col = lerp(col, float3(1, 1, 1), _Flash);
                return float4(col, inRim * _Alpha);
            }
            ENDCG
        }
    }
}
