// A skill annotation stamp over a caster's head (R128, shape C: the symbol alone, no plate): a mask baked by
// SkillGradeStamps (r the symbol's fill, g its ink edge, b the ink shadow set off down and right) coloured here, so one
// mask serves every grade colour and the white flash. Drawn over everything (it is a mark for the player, like a HUD
// label), facing the camera. Built-in render pipeline.
Shader "ChessFight/Skill Grade"
{
    Properties
    {
        _MainTex ("Mask (r fill, g ink, b shadow)", 2D) = "black" {}
        _Fill ("Fill", Color) = (0.89, 0.69, 0.24, 1)
        _InkColor ("Ink", Color) = (0.08, 0.1, 0.2, 1)
        _Flash ("White", Range(0, 1)) = 0
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+60" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Fill, _InkColor;
            float _Flash, _Alpha;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 m = tex2D(_MainTex, i.uv).rgb;
                float3 fill = lerp(_Fill.rgb, float3(1, 1, 1), _Flash);
                float3 col = lerp(_InkColor.rgb, fill, m.r);
                float a = max(m.g, m.b * 0.6) * _Alpha * _Fill.a;
                return float4(col, a);
            }
            ENDCG
        }
    }
}
