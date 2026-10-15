// The king's call (R146, 승규 님 10-15: "팀원이 올 0.6초 동안 올 때 오버워치에서 나노강화제 받는 이펙트 처럼 몸이 킹이 쓰는
// 색으로 변하면서 왔으면 좋겠어"): worn in place of a piece's own materials while it comes in to its king
// (PawnRushSkillFx.KingCall.cs), reading their texture and colour. Two passes:
// - behind whatever stands in front of it (a wall it passes through): a flat glow of the king's colour, brightest at its
//   edge, so it is seen through the wall;
// - the body: its own texture, lit, turned the king's colour (keeping its light and shade, the face still showing), a
//   bright edge, faint bands running up it, a white flash as it takes the boost.
// _Glow takes it in and out (0: the piece as it was). Drawn after the solid world (Geometry+60) so walls are in the depth
// for the first pass. Built-in render pipeline.
Shader "ChessFight/Skill Nano"
{
    Properties
    {
        _MainTex ("Albedo (the piece's own)", 2D) = "white" {}
        _Color ("Colour (the piece's own)", Color) = (1, 1, 1, 1)
        _Tint ("The king's colour", Color) = (0.79, 0.58, 0.18, 1)
        [HDR] _RimColor ("Edge glow", Color) = (2.2, 1.9, 1.2, 1)
        _Glow ("How far into it", Range(0, 1)) = 1
        _Mix ("How much of the body takes the colour", Range(0, 1)) = 0.78
        _XRay ("Seen through walls", Range(0, 1)) = 0.75
        _Flash ("White flash", Range(0, 1)) = 0
        _Flow ("Its clock (s)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+60" "RenderType" = "Opaque" }

        // Behind what stands in front of it.
        Pass
        {
            Tags { "LightMode" = "Always" }
            ZTest Greater
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Tint, _RimColor;
            float _Glow, _XRay, _Flow;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 n : TEXCOORD0;
                float3 w : TEXCOORD1;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.w = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n), v = normalize(_WorldSpaceCameraPos - i.w);
                float rim = 1 - saturate(dot(n, v));
                float3 col = lerp(_Tint.rgb * 1.25, _RimColor.rgb, rim * rim);
                float a = _XRay * _Glow * (0.3 + 0.7 * rim);
                return float4(col, saturate(a));
            }
            ENDCG
        }

        // The body.
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            ZTest LEqual
            ZWrite On
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST, _Color, _Tint, _RimColor;
            float _Glow, _Mix, _Flash, _Flow;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 n : TEXCOORD1;
                float3 w : TEXCOORD2;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.w = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n), v = normalize(_WorldSpaceCameraPos - i.w);
                float3 albedo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                // Its own light: soft wrap from the sun and the sky's ambient.
                float ndl = saturate(dot(n, normalize(_WorldSpaceLightPos0.xyz)) * 0.7 + 0.3);
                float3 lit = albedo * (ShadeSH9(float4(n, 1)) + _LightColor0.rgb * ndl);
                // The king's colour, keeping the body's light and shade (so the face and the folds still show).
                float lum = dot(lit, float3(0.3, 0.59, 0.11));
                // (The first take lifted it to a pale cream on a white piece: now his deep gold.)
                float3 gold = _Tint.rgb * (0.38 + 0.78 * lum);
                float rim = pow(1 - saturate(dot(n, v)), 3.0);
                // Faint bands running up it, the boost's energy.
                float band = 0.5 + 0.5 * sin(i.w.y * 16 - _Flow * 14);
                band = band * band * band;
                float3 col = lerp(lit, gold, _Mix * _Glow);
                col += _RimColor.rgb * rim * _Glow * (0.55 + 0.3 * band);
                col += _Tint.rgb * band * 0.16 * _Glow;
                col = lerp(col, float3(1.6, 1.55, 1.4), saturate(_Flash) * _Glow);
                return float4(col, 1);
            }
            ENDCG
        }
    }
    // Its shadow.
    Fallback "Legacy Shaders/VertexLit"
}
