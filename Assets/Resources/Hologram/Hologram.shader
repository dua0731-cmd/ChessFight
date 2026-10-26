// The entrance screen's course model as a hologram (10-26, 승규 님: "실사적으로 홀로그램 이였으면 좋겠어"): light, not
// matter. Added on (Blend One One, no depth written), so its faces never sort against or fight each other and both
// sides of every face show, as in a body of light: faint everywhere, a little stronger on faces turned up and where
// the eye looks along a face (grazing light gathers there), and densest near the plate it is thrown from. As a
// projection does, it has fine lines across it, a band of light rising through it every few seconds, a slight
// shimmer, and now and then a band of the picture that drops out for a moment. Its edges are the line shader's
// (ChessFight/Hologram Line). With _MainTex, the light it throws on the plate under it (LoadingStudio.Course.cs).
// Unlit, so it works in both render pipelines.
Shader "ChessFight/Hologram"
{
    Properties
    {
        _MainTex ("Picture (white: none)", 2D) = "white" {}
        [HDR] _Color ("Light", Color) = (1, 0.68, 0.28, 1)
        _Fill ("Fill", Float) = 0.07
        _Top ("More on faces turned up", Float) = 0.05
        _Rim ("More at grazing angles", Float) = 0.35
        _Base ("More near the plate", Float) = 0.6
        _BaseHeight ("Near the plate: how high (m)", Float) = 0.25
        _Scan ("Fine lines", Range(0, 1)) = 0.3
        _Sweep ("Rising band", Float) = 0.8
        _SweepFrom ("Rising band: from (world y)", Float) = 0
        _SweepHeight ("Rising band: how far up (m)", Float) = 2.4
        _Grain ("Grain", Range(0, 1)) = 0.1
        _Flicker ("Flicker", Range(0, 1)) = 1
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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

            sampler2D _MainTex;
            float4 _Color;
            float _Fill, _Top, _Rim, _Base, _BaseHeight, _Scan, _Sweep, _SweepFrom, _SweepHeight, _Grain, _Flicker, _Fade;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
                float3 normal : TEXCOORD2;
                float4 screen : TEXCOORD3;
            };

            float Hash (float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            // The projection's unsteadiness at time t for a point at screen height y (0..1).
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
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.normal);
                float3 eye = normalize(_WorldSpaceCameraPos - i.world);
                float grazing = pow(1 - abs(dot(n, eye)), 3);
                float k = _Fill + _Top * abs(n.y) + _Rim * grazing;
                k *= 1 + _Base * exp(-max(0, i.world.y - _SweepFrom) / _BaseHeight);
                float2 s = i.screen.xy / i.screen.w;
                float2 px = s * _ScreenParams.xy;
                // fine lines three pixels apart, drifting up
                k *= 1 - _Scan * (0.5 + 0.5 * cos((px.y - _Time.y * 8) * 2.0944));
                // the band rising from the plate to the crown (weaker on faces turned up: it crosses them all at once)
                float rise = _SweepFrom + frac(_Time.y / 5) * _SweepHeight;
                k *= 1 + _Sweep * exp(-abs(i.world.y - rise) * 40) * (1 - 0.6 * abs(n.y));
                k *= 1 + _Grain * (Hash(floor(px) + frac(_Time.y * 7.31)) * 2 - 1);
                k *= Waver(_Time.y, s.y);
                return float4(_Color.rgb * tex2D(_MainTex, i.uv).rgb * k * _Fade, 0);
            }
            ENDCG
        }
    }
}
