// Inked ribbons for the Queen of the Hill skill effects (R89, design A "잉크 테두리 장난감 체스"): the rook's two-colour
// castling arch, the bishop's shot trail, the knight's wind trail, the pawn's speed wedges. A strip with u along it
// (0 tail .. 1 head) and v across it; only the part between _Tail and _Head shows. Bands from the middle out: a light
// core, the colour, an ink edge on both sides. _Taper narrows it toward the tail (a speed line, a trail).
// Premultiplied: _Opacity 1 covers like paint. Built-in render pipeline.
Shader "ChessFight/Skill Band"
{
    Properties
    {
        _Core ("Core", Color) = (1, 1, 1, 1)
        _Mid ("Colour", Color) = (1, 0.55, 0.12, 1)
        _InkColor ("Ink", Color) = (0.23, 0.09, 0.02, 1)
        _CoreShare ("Core (share of the half width)", Range(0, 1)) = 0.3
        _InkShare ("Ink (share of the half width)", Range(0, 1)) = 0.22
        _Head ("Head along it", Range(0, 1)) = 1
        _Tail ("Tail along it", Range(0, 1)) = 0
        _Taper ("Narrows toward the tail", Range(0, 1)) = 0
        _Opacity ("Covers", Range(0, 1)) = 1
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _Core, _Mid, _InkColor;
            float _CoreShare, _InkShare, _Head, _Tail, _Taper, _Opacity, _Fade;

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
                float span = max(_Head - _Tail, 1e-3);
                float s = (i.uv.x - _Tail) / span;   // 0 tail .. 1 head over the part that shows
                if (s < 0 || s > 1) discard;
                float width = lerp(1, saturate(s * 1.15), _Taper);   // share of the full width here
                float across = abs(i.uv.y - 0.5) * 2;                // 0 middle .. 1 edge
                float inside = width - across;
                float aa = max(fwidth(across), 1e-4);
                float cover = smoothstep(-aa, aa, inside);
                // Round the ends a little so they do not cut off square.
                float ends = smoothstep(0, 0.04, s) * smoothstep(0, 0.04, 1 - s);
                cover *= lerp(1, ends, 1 - _Taper * 0.5);
                if (cover <= 0.001) discard;
                float t = across / max(width, 1e-3);                  // 0 middle .. 1 its own edge
                float3 col = lerp(_Core.rgb, _Mid.rgb, smoothstep(_CoreShare - 0.05, _CoreShare + 0.05, t));
                col = lerp(col, _InkColor.rgb, smoothstep(1 - _InkShare - 0.04, 1 - _InkShare + 0.04, t));
                float a = cover * _Opacity * _Fade;
                return float4(col * a, a);
            }
            ENDCG
        }
    }
}
