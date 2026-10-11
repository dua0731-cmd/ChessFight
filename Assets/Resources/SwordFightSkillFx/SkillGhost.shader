// A skill's afterimage (Sword Fight skills R99, design A "잉크 테두리 장난감 체스"): a copy of a piece's body as a flat
// see-through silhouette of the skill's colour with an ink edge where its surface turns away, faded by _Alpha. A depth
// pass first, so only its front surface is drawn (no darker overlaps inside it). No glow. Built-in render pipeline.
Shader "ChessFight/Skill Ghost"
{
    Properties
    {
        _Color ("Fill", Color) = (1, 0.83, 0.3, 1)
        _InkColor ("Ink", Color) = (0.23, 0.13, 0.03, 1)
        _Ink ("Ink edge (0 = none)", Range(0, 1)) = 0.3
        _Alpha ("Alpha", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            ZWrite On
            ColorMask 0
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert (float4 vertex : POSITION) : SV_POSITION { return UnityObjectToClipPos(vertex); }
            fixed4 frag () : SV_Target { return 0; }
            ENDCG
        }

        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color, _InkColor;
            float _Ink, _Alpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 N = normalize(i.normal);
                float3 V = normalize(_WorldSpaceCameraPos - i.world);
                float edge = 1 - saturate(dot(N, V));
                float aa = max(fwidth(edge), 1e-3);
                float ink = smoothstep(1 - _Ink - aa, 1 - _Ink + aa, edge) * step(1e-4, _Ink);
                float3 col = lerp(_Color.rgb, _InkColor.rgb, ink);
                return float4(col, _Alpha * lerp(_Color.a, 1, ink));
            }
            ENDCG
        }
    }
}
