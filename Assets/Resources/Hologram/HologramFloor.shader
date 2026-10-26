// The LEDs under the course hologram (10-26, 승규 님: "바닥에 LED 같이 홀로그램과 잘 어울러 졌으면 좋겠어", then "홀로
// 그램이 없는 바닥에는 LED 처리 하지 말고"): amber LEDs in the smoked glass of the plate (LoadingStudio.Course.cs), only
// where the hologram stands (the footprint in _MainTex: r where the course stands, b blurred far), burning as if they
// threw it up, with a pulse as each band of light starts up through the hologram (ChessFight/Hologram's, every
// 5 s), a wave running from the start to the tower and a twinkle; round them, a soft glow of the hologram's light
// on the glass. LEDs too small on screen blend into their average, so the far end does not shimmer. Added on.
// Unlit, so it works in both render pipelines.
Shader "ChessFight/Hologram Floor"
{
    Properties
    {
        _MainTex ("Footprint (r: course, b: far blur)", 2D) = "black" {}
        [HDR] _Color ("LED", Color) = (1, 0.62, 0.24, 1)
        _Cells ("LEDs across, along", Vector) = (56, 135, 0, 0)
        _Dot ("LED radius (share of the pitch)", Range(0.05, 0.5)) = 0.2
        _Lit ("Brightness", Float) = 1.2
        _Pool ("The hologram's light on the glass", Float) = 0.25
        _Wave ("Wave", Float) = 0.8
        _Pulse ("Pulse with the rising band", Float) = 0.6
        _Twinkle ("Twinkle", Range(0, 1)) = 0.3
        _Flicker ("Flicker", Range(0, 1)) = 1
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-1" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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
            float4 _Color, _Cells;
            float _Dot, _Lit, _Pool, _Wave, _Pulse, _Twinkle, _Flicker, _Fade;

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

            float Hash (float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;
                float2 cell = i.uv * _Cells.xy;
                float2 id = floor(cell);
                // the LED: a round dot in its square, edged a pixel soft; there only under the hologram
                float w = max(fwidth(cell.x), fwidth(cell.y));
                float led = 1 - smoothstep(_Dot - w * 0.75, _Dot + w * 0.75, length(frac(cell) - 0.5));
                led = lerp(led, 3.14159 * _Dot * _Dot * 1.6, saturate((w - 0.22) * 5));
                led *= step(0.5, tex2D(_MainTex, (id + 0.5) / _Cells.xy).r);
                // how hard it burns: the pulse, the wave and the twinkle
                float level = _Lit + _Pulse * exp(-frac(t / 5) * 6);
                float wave = i.uv.y - (frac(t / 7) * 1.4 - 0.2);
                level *= 1 + _Wave * exp(-wave * wave * 150);
                level *= 1 + _Twinkle * (Hash(id + floor(t * 4)) - 0.5);
                float3 c = _Color.rgb * led * level;
                // hot LEDs burn whiter
                c = lerp(c, float3(1, 0.93, 0.8) * led * level, saturate(level - 1) * 0.5);
                // the hologram's own light on the glass
                c += _Color.rgb * _Pool * tex2D(_MainTex, i.uv).b;
                float flick = lerp(1, 1 + 0.03 * sin(t * 31.0) + 0.02 * sin(t * 17.3 + 1.7), _Flicker);
                return float4(c * flick * _Fade, 0);
            }
            ENDCG
        }
    }
}
