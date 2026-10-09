// A piece's mark over a caster's head as a solid token (Sword Fight skills R108, 승규 님: "이거 3D로"): the R102 icon's
// two distance fields printed on its front and back faces (the piece's colour, its light lines, an ink edge; no rim), its
// side walls in a darker shade of the piece's colour with an ink band where they turn away from the light, so the token
// reads as a thick toy cut-out when it spins. Opaque with alpha clip (it hides its own back face). _Flash turns it white.
// uv2.x = 0 for a face, 1 for a wall. Built-in render pipeline, no lights needed (a fixed light in view space).
Shader "ChessFight/Skill Icon 3D"
{
    Properties
    {
        _MainTex ("Distance fields (r outline, g shape)", 2D) = "black" {}
        _Fill ("Fill", Color) = (1, 0.83, 0.3, 1)
        _Lines ("Lines", Color) = (1, 0.95, 0.8, 1)
        _InkColor ("Ink", Color) = (0.1, 0.1, 0.2, 1)
        _Spread ("Field spread (icon heights)", Float) = 0.12
        _InkWidth ("Ink (icon heights)", Float) = 0.05
        _Flash ("White", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+50" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        ZWrite On
        ZTest LEqual
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Fill, _Lines, _InkColor;
            float _Spread, _InkWidth, _Flash;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float side : TEXCOORD1;
                float3 n : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.side = v.uv2.x;
                o.n = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                return o;
            }

            float4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                float3 n = normalize(i.n) * (facing > 0 ? 1 : -1);
                float ndl = dot(n, normalize(float3(-0.45, 0.55, 0.7)));
                float3 col;
                if (i.side > 0.5)
                {
                    // The wall: the piece's colour in shade, ink where it turns from the light (two toon steps).
                    float3 shade = _Fill.rgb * 0.62;
                    col = lerp(_InkColor.rgb, shade, smoothstep(0.05, 0.2, ndl));
                    col = lerp(col, _Fill.rgb * 0.82, smoothstep(0.55, 0.7, ndl));
                }
                else
                {
                    float2 s = tex2D(_MainTex, i.uv).rg;
                    float outline = (s.r - 0.5) * 2 * _Spread;   // + inside the icon's outline
                    float shape = (s.g - 0.5) * 2 * _Spread;     // + inside its dark shape
                    clip(outline + _InkWidth);
                    float aa = max(fwidth(outline), 1e-4);
                    float aa2 = max(fwidth(shape), 1e-4);
                    float inFill = smoothstep(-aa, aa, outline);
                    float lines = (1 - smoothstep(-aa2, aa2, shape)) * inFill;
                    col = lerp(_InkColor.rgb, lerp(_Fill.rgb, _Lines.rgb, lines), inFill);
                    // A face turned away from the light is a little darker, so the spin shows.
                    col *= lerp(0.8, 1.0, saturate(ndl * 1.4 + 0.3));
                }
                col = lerp(col, float3(1, 1, 1), _Flash);
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
