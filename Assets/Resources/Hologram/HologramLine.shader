// The glowing edges of the course hologram (10-26, with ChessFight/Hologram): every line a quad turned to the camera
// about the line (HoloMesh.Line: both end points ride on every vertex, uv1 holding the other end), _Width metres to
// each side, with a hot core, a soft glow round it deepening to amber as it spreads (light blooming off the edge)
// and, if asked (uv0.w), round ends. Added on. For the route: lit up to _Reveal metres along it (uv0.z), dimmer past
// that, dashes that run along it and a flare at its head. With _Drift, dust in the light instead: every line a mote
// (uv0.z its seed) rising _Drift metres in _DriftTime seconds, glinting, then gone and back at the bottom.
// Unlit, so it works in both render pipelines.
Shader "ChessFight/Hologram Line"
{
    Properties
    {
        [HDR] _Color ("Core", Color) = (1, 0.9, 0.62, 1)
        [HDR] _GlowColor ("Glow", Color) = (1, 0.6, 0.22, 1)
        _Width ("Half width (m)", Float) = 0.015
        _CoreWidth ("Core: share of the width", Range(0.05, 1)) = 0.32
        _Glow ("Glow", Float) = 0.5
        _Reveal ("Lit up to (m along)", Float) = 100000
        _Dim ("Past it", Range(0, 1)) = 1
        _Head ("Flare at the head", Float) = 0
        _Dash ("Dashes", Range(0, 1)) = 0
        _DashLength ("Dash period (m)", Float) = 0.1
        _DashFlow ("Dashes run (m/s)", Float) = 0.12
        _Drift ("Motes: how far they rise (m), 0 for lines", Float) = 0
        _DriftTime ("Motes: seconds to rise", Float) = 10
        _Scan ("Fine lines", Range(0, 1)) = 0
        _Flicker ("Flicker", Range(0, 1)) = 1
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+1" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _Color, _GlowColor;
            float _Width, _CoreWidth, _Glow, _Reveal, _Dim, _Head, _Dash, _DashLength, _DashFlow, _Drift, _DriftTime, _Scan, _Flicker, _Fade;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 uv : TEXCOORD0;     // side (-1, 1), end (0, 1), metres along the path, round ends (0, 1)
                float4 other : TEXCOORD1;  // the other end
                float4 color : COLOR;      // a: strength
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float4 seg : TEXCOORD0;    // across (-1..1), metres along the quad, the quad's length, the round end's length
                float dist : TEXCOORD1;
                float4 screen : TEXCOORD2;
            };

            float Hash (float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            // As ChessFight/Hologram's, so the edges waver with the faces.
            float Waver (float t, float y)
            {
                float f = 1 + 0.03 * sin(t * 31.0) + 0.02 * sin(t * 17.3 + 1.7);
                f *= 1 - 0.15 * step(0.97, Hash(float2(floor(t * 9), 7.3)));
                float slot = floor(t * 4);
                float band = 1 - smoothstep(0.0, 0.025, abs(y - Hash(float2(slot, 2.9))));
                f *= 1 - 0.45 * band * step(0.95, Hash(float2(slot, 1.3)));
                return lerp(1, f, _Flicker);
            }

            v2f vert (appdata v)
            {
                v2f o;
                float3 p = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
                float3 q = mul(unity_ObjectToWorld, float4(v.other.xyz, 1)).xyz;
                float end = v.uv.y;
                float3 d = end < 0.5 ? q - p : p - q;
                float len = length(d);
                float3 dir = d / max(len, 1e-6);
                float3 side = cross(dir, normalize(_WorldSpaceCameraPos - p));
                side /= max(length(side), 1e-5);
                float cap = _Width * v.uv.w;
                p += side * (v.uv.x * _Width) + dir * ((end * 2 - 1) * cap);
                o.color = v.color;
                if (_Drift > 0)
                {
                    float cycle = frac(_Time.y / _DriftTime + v.uv.z);
                    p.y += cycle * _Drift;
                    o.color.a *= sin(cycle * 3.14159) * (0.55 + 0.45 * sin(_Time.y * 5.3 + v.uv.z * 61.0));
                }
                o.pos = mul(UNITY_MATRIX_VP, float4(p, 1));
                o.seg = float4(v.uv.x, end * (len + 2 * cap), len + 2 * cap, cap);
                o.dist = v.uv.z;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float along = i.seg.y, total = i.seg.z, cap = i.seg.w;
                float beyond = cap > 0 ? saturate(1 - min(along, total - along) / cap) : 0;
                float a = sqrt(i.seg.x * i.seg.x + beyond * beyond);
                float core = 1 - smoothstep(_CoreWidth * 0.5, _CoreWidth, a);
                float glow = exp(-a * a * 4.5) * saturate(1 - a);
                float lit = 1 - smoothstep(_Reveal - 0.01, _Reveal, i.dist);
                float k = lerp(_Dim, 1, lit);
                float run = frac((i.dist - _Time.y * _DashFlow * lit) / _DashLength);
                k *= lerp(1, 0.2 + 0.8 * smoothstep(0, 0.1, run) * (1 - smoothstep(0.45, 0.55, run)), _Dash);
                k += _Head * exp(-abs(i.dist - _Reveal) * 25) * step(0.001, _Reveal);
                float2 s = i.screen.xy / i.screen.w;
                k *= 1 - _Scan * (0.5 + 0.5 * cos((s.y * _ScreenParams.y - _Time.y * 8) * 2.0944));
                k *= Waver(_Time.y, s.y);
                float3 bloom = lerp(_GlowColor.rgb * 1.15, _GlowColor.rgb * float3(1.0, 0.62, 0.42), saturate(a * 1.4));
                float3 c = (_Color.rgb * core + bloom * glow * _Glow) * k * i.color.rgb * i.color.a * _Fade;
                return float4(c, 0);
            }
            ENDCG
        }
    }
}
