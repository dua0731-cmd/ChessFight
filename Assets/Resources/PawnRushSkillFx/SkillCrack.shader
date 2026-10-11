// The queen's cracks in the floor (R86, after the reference's stylized explosion). PawnRushSkillFx.Toon.cs draws
// them into a texture: the crack line in R, how far along its crack each point lies in G (0 at the middle, 1 at the
// tips) and a soft glow round the lines in B. _Reveal runs the cracks out from the middle (where G is below it),
// _Cool turns the glowing lines into dark scorched ones, _Fade takes everything away. Premultiplied like the glow
// material: light added on top plus a share that covers the floor, so the colour also shows on a white floor.
Shader "ChessFight/Skill Crack"
{
    Properties
    {
        _MainTex ("Cracks", 2D) = "black" {}
        [HDR] _Hot ("Hot", Color) = (4, 2.4, 0.6, 1)
        _Cold ("Scorched", Color) = (0.12, 0.07, 0.05, 1)
        _Reveal ("Run out (0..1)", Range(0, 1.05)) = 1
        _Cool ("Cooled (0 hot .. 1 scorched)", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
        _HotPaint ("Hot covers", Range(0, 1)) = 0.35
        _ColdPaint ("Scorched covers", Range(0, 1)) = 0.8
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-5" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST, _Hot, _Cold;
            float _Reveal, _Cool, _Fade, _HotPaint, _ColdPaint;

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
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 t = tex2D(_MainTex, i.uv);
                float shown = saturate((_Reveal - t.g) / 0.03) * _Fade;
                float crack = t.r, glow = t.b;
                float3 hot = _Hot.rgb * (crack + glow * 0.45);
                float hotCover = saturate(crack + glow * 0.45) * _HotPaint;
                float3 cold = _Cold.rgb * crack;
                float coldCover = crack * _ColdPaint;
                return float4(lerp(hot, cold, _Cool) * shown, lerp(hotCover, coldCover, _Cool) * shown);
            }
            ENDCG
        }
    }
}
