// R839 (user 11-25: "이 막대도 튜토리얼 처럼 효과" / "그래픽을 더 좋게"): the tutorial's goal mark (TutorialCourse.BuildMarker).
// One shader for its three kinds of part (_Shape):
//   0  a column of light: an open tube, uv x round it (0..1), uv y up it (0..1). Soft where the tube turns away from
//      the eye (so it reads as a volume, not a pipe), fading upward, with streaks of light rising through it and a
//      brighter foot.
//   1  flat light on the floor (rings, the glow under the column): the vertex colour's alpha as it is.
//   2  a mote: a round soft dot from the quad's uv (the particle system's billboards).
// The vertex colour tints and weights each part, _Fade is set per renderer (MaterialPropertyBlock).
// Premultiplied with a partial cover (_Opacity): the light adds over dark things and still tints gold over the bright
// marble and the sky, where a purely additive beam would wash out to white.
// Built-in render pipeline (drawn by URP's unlit pass like the other effect shaders here).
Shader "ChessFight/Tutorial Beacon"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 0.8, 0.3, 1)
        _Intensity ("Brightness", Float) = 1.6
        _Opacity ("Cover (0 = only adds light)", Range(0, 1)) = 0.55
        _Shape ("0 column, 1 flat, 2 mote", Float) = 0
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            float4 _Color;
            float _Intensity, _Opacity, _Shape;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Fade)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float3 view : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float hash (float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Value noise that repeats every `period` cells along x, so the column has no seam.
            float noise (float2 p, float period)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float x0 = fmod(cell.x, period), x1 = fmod(cell.x + 1.0, period);
                float a = hash(float2(x0, cell.y)), b = hash(float2(x1, cell.y));
                float c = hash(float2(x0, cell.y + 1.0)), d = hash(float2(x1, cell.y + 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float a = i.color.a * UNITY_ACCESS_INSTANCED_PROP(Props, _Fade);
                float hot = 0.0;
                if (_Shape < 0.5)
                {
                    float y = i.uv.y;
                    // The column's sides only: seen level it is soft at its edges; from above it must not turn solid.
                    float3 n = normalize(float3(i.normal.x, 0.0, i.normal.z));
                    float3 v = normalize(float3(i.view.x, 0.0, i.view.z));
                    float facing = abs(dot(n, v));
                    float soft = smoothstep(0.0, 0.8, facing);
                    // Streaks of light rising: two layers of stretched noise at different speeds.
                    float slow = noise(float2(i.uv.x * 12.0, y * 2.2 - _Time.y * 0.35), 12.0);
                    float fast = noise(float2(i.uv.x * 24.0 + 5.0, y * 5.0 - _Time.y * 0.9), 24.0);
                    float streak = smoothstep(0.45, 0.95, slow) + 0.6 * smoothstep(0.6, 1.0, fast);
                    float up = pow(saturate(1.0 - y), 1.7);
                    float foot = 1.0 - smoothstep(0.0, 0.1, y);
                    a *= soft * (up * (0.5 + 0.9 * streak) + 0.6 * foot);
                    hot = saturate(0.5 * streak * up + 0.7 * foot);
                }
                else if (_Shape > 1.5)
                {
                    float d = saturate(1.0 - length(i.uv - 0.5) * 2.0);
                    a *= d * d;
                    hot = d * d;
                }
                a = saturate(a);
                // Hotter parts go toward a pale gold-white.
                float3 rgb = lerp(_Color.rgb, float3(1.0, 0.96, 0.8), hot * 0.6) * i.color.rgb * _Intensity;
                return float4(rgb * a, a * _Opacity);
            }
            ENDCG
        }
    }
}
