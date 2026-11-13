// The king's call (R146): the shaft of light standing up from the crown over a calling king's head ("금 왕관 신호가
// 기둥처럼 솟아요"), a quad turned to the camera about its upright (PawnRushSkillFx.KingCall.cs). Soft across, brightest by
// the crown and thinning to nothing at its top, faint streaks rising up it. Added on (it only lightens), _Color brighter
// than white at its heart so the test scene's bloom catches it. Built-in render pipeline.
Shader "ChessFight/Skill Beam"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (2, 1.7, 0.9, 1)
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float _Fade;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float x = i.uv.x * 2 - 1;
                float across = exp(-x * x * 7);
                float core = exp(-x * x * 60);
                float up = i.uv.y;
                float along = saturate(1 - up) * saturate(up * 12);
                along *= along;
                float streak = 0.75 + 0.25 * sin(up * 26 - _Time.y * 6 + x * 3);
                float k = (across * 0.55 + core * 0.9) * along * streak * _Fade;
                return float4(_Color.rgb * k, 0);
            }
            ENDCG
        }
    }
}
