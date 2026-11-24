// R830 (승규 님 11-24: "칼베기 잔상 효과를 넣어줘"): a sword cut's afterimage (SwordSlashTrail.cs) - a ribbon the blade swept,
// uv y from the hilt end (0) to the tip (1), uv x its age (0 new .. 1 gone), the vertex colour its side's colour as this
// screen sees it (white, black or the foe's red) with its alpha already faded by age. Thin and soft at the hilt end,
// densest a little in from the tip, with a soft outer edge; a bright edge along the tip while it is new. Premultiplied,
// so the black side's ribbon darkens what is behind it and the white one lightens it. Built-in render pipeline (drawn
// by URP's unlit pass like the other effect shaders here).
Shader "ChessFight/Sword Slash"
{
    Properties
    {
        _TipEdge ("Bright edge along the tip (share of its colour)", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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

            float _TipEdge;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float across = i.uv.y;
                // Nothing at the hilt end, rising to the tip, a soft last few centimetres at the very tip.
                float body = smoothstep(0.0, 0.5, across) * (1.0 - smoothstep(0.94, 1.0, across));
                float a = saturate(i.color.a * body);
                // A thin bright edge along the tip while the stretch is new (white for white and red, grey for black).
                float edge = smoothstep(0.75, 0.95, across) * (1.0 - i.uv.x) * _TipEdge;
                float3 rgb = lerp(i.color.rgb, float3(1, 1, 1), edge * max(max(i.color.r, i.color.g), max(i.color.b, 0.3)));
                return float4(rgb * a, a);
            }
            ENDCG
        }
    }
}
