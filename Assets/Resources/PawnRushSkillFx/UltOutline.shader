// The driven ultimates' red outline (JY-skill-lab, 2026-11-22; Docs/Skills/PAWN_RUSH_SKILL_SYSTEM.md 4: "쓰는 순간 모든 적이
// 벽 너머로도 빨간 윤곽으로 보이고"): an enemy's skin drawn a second time over itself as red light (PawnRushSkillFx.DriveOutline,
// a second skinned renderer on the same bones). Where something hides it (ZTest Greater) the whole body glows dim with a
// bright edge, so it is seen through walls; where it is in sight only a thin red edge. Added light, no depth written.
// The second pass is URP's (UniversalForward); the first is drawn by any pipeline.
Shader "ChessFight/Ult Outline"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (2.4, 0.16, 0.22, 1)
        _Alpha ("Strength", Range(0, 1)) = 1
        _Fill ("Fill behind walls", Range(0, 1)) = 0.3
        _RimPower ("Edge power", Range(0.5, 8)) = 2.2
        _FillSeen ("Fill in sight (an impact frame)", Range(0, 2)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+20" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        Blend One One
        Cull Back

        CGINCLUDE
        #include "UnityCG.cginc"

        float4 _Color;
        float _Alpha, _Fill, _RimPower, _FillSeen;

        struct appdata
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
        };

        struct v2f
        {
            float4 pos : SV_POSITION;
            float3 normal : TEXCOORD0;
            float3 view : TEXCOORD1;
        };

        v2f vert (appdata v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.normal = UnityObjectToWorldNormal(v.normal);
            o.view = WorldSpaceViewDir(v.vertex);
            return o;
        }

        float Edge (v2f i, float power)
        {
            float facing = saturate(abs(dot(normalize(i.normal), normalize(i.view))));
            return pow(1 - facing, power);
        }

        float4 fragHidden (v2f i) : SV_Target
        {
            float k = _Fill + Edge(i, _RimPower) * 1.3;
            return float4(_Color.rgb * k * _Alpha, 1);
        }

        float4 fragSeen (v2f i) : SV_Target
        {
            return float4(_Color.rgb * (Edge(i, _RimPower + 1.5) * 0.9 + _FillSeen) * _Alpha, 1);
        }
        ENDCG

        Pass
        {
            ZTest Greater
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragHidden
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragSeen
            ENDCG
        }
    }
}
