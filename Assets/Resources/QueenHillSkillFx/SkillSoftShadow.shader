// A soft shadow on the floor (R130, the Queen of the Hill bishop's carpet: "밑에 그려지는 그림자도 더 그래픽 좋게"): a
// rounded rectangle on a quad lying on the floor, dark in its middle and fading out through a wide soft edge (a blurred
// shape: _Soft metres each side of its outline), so it reads like a real shadow cast by something hovering over it, and
// softer and lighter the higher that is (the effects set _Soft and _Color.a). A faint ring of _Glow round its edge (the
// carpet's magic, 0 = none). Built-in render pipeline.
Shader "ChessFight/Skill Soft Shadow"
{
    Properties
    {
        _Color ("Shadow (a = darkness)", Color) = (0.08, 0.06, 0.14, 0.45)
        _Size ("Shape size (m)", Vector) = (1.3, 0.95, 0, 0)
        _Quad ("Quad size (m)", Vector) = (2, 1.6, 0, 0)
        _Round ("Corner radius (m)", Float) = 0.12
        _Soft ("Softness (m)", Float) = 0.2
        [HDR] _Glow ("Edge glow (a = strength)", Color) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-30" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -4

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _Color, _Size, _Quad, _Glow;
            float _Round, _Soft;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 p : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.p = (v.texcoord.xy - 0.5) * _Quad.xy;
                return o;
            }

            float RoundBox(float2 p, float2 h, float r)
            {
                float2 q = abs(p) - h + r;
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 h = _Size.xy * 0.5;
                float d = RoundBox(i.p, h, min(_Round, min(h.x, h.y)));
                float s = max(_Soft, 1e-3);
                // A blurred edge: an error-function-like falloff across 2 _Soft, darker toward the middle.
                float t = saturate(0.5 - d / (2 * s));
                float a = t * t * (3 - 2 * t);
                a *= lerp(0.85, 1.0, saturate(-d / (h.y + s)));
                float4 c = float4(_Color.rgb, _Color.a * a);
                if (_Glow.a > 0)
                {
                    float g = exp(-pow(d / (s * 0.6), 2)) * _Glow.a;
                    c.rgb = lerp(c.rgb, _Glow.rgb, saturate(g / max(c.a + g, 1e-3)));
                    c.a = saturate(c.a + g * 0.5);
                }
                if (c.a < 0.002) discard;
                return c;
            }
            ENDCG
        }
    }
}
