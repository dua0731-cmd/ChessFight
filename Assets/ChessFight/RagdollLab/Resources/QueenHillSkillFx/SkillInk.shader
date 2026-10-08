// Chunky cartoon props for the Queen of the Hill skill effects (R89, design A "잉크 테두리 장난감 체스"): the queen's
// sword, the king's sceptre, the bishop's diamond tile and rings, the rook's arch ends. Shaded from the sun in three
// hard bands like painted toys (no glow: they read on the white test floor and the cream board alike); _Flash turns
// them white for a frame. The ink outline is a second, slightly larger copy drawn inside out (Skill Ink Line).
// The sun: _FxSun, set by the effects. Built-in render pipeline.
Shader "ChessFight/Skill Ink"
{
    Properties
    {
        _Lit ("Lit", Color) = (1, 1, 1, 1)
        _Mid ("Middle", Color) = (0.7, 0.7, 0.7, 1)
        _Shade ("Shadow", Color) = (0.4, 0.4, 0.4, 1)
        _Bands ("Band edges: shadow|middle, middle|lit", Vector) = (0.4, 0.72, 0, 0)
        _Flash ("White", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Cull Back
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _Lit, _Mid, _Shade, _Bands, _FxSun;
            float _Flash;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 N = normalize(i.normal);
                float3 L = dot(_FxSun.xyz, _FxSun.xyz) > 1e-6 ? normalize(_FxSun.xyz) : normalize(float3(0.35, 0.79, -0.5));
                float shade = dot(N, L) * 0.5 + 0.5;
                float aa = max(fwidth(shade), 1e-4);
                float3 col = lerp(_Shade.rgb, _Mid.rgb, smoothstep(_Bands.x - aa, _Bands.x + aa, shade));
                col = lerp(col, _Lit.rgb, smoothstep(_Bands.y - aa, _Bands.y + aa, shade));
                col = lerp(col, float3(1, 1, 1), _Flash);
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
