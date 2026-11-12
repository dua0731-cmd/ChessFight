// R608: low haze lying over the deck of the matching screen's stage (LoadingStudio.Stage.cs), lit by the LED
// wall behind it: an upright sheet, thick at the floor and gone by its top, with slow clouds drifting sideways
// through it. Added over the picture; _Color (linear) is its light. Unlit and written with the built-in includes,
// so it draws in both render pipelines.
Shader "ChessFight/Stage Haze"
{
    // Listed so each material keeps its own values (see StageBeam.shader).
    Properties
    {
        [HideInInspector] _Color ("Colour x strength (linear)", Vector) = (0.1, 0.1, 0.2, 1)
        [HideInInspector] _Drift ("Cloud size, drift speed, break-up", Vector) = (0.25, 0.3, 0.75, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _CfStageTime;
            float4 _Color;   // linear colour times strength
            float4 _Drift;   // cloud size (1 / metres), drift speed (m/s), how much the clouds break it up (0..1), 0

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 x)
            {
                float2 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 q = float2(i.world.x + _CfStageTime * _Drift.y, i.world.y * 2.2) * _Drift.x;
                float cloud = 0.55 * Noise(q) + 0.3 * Noise(q * 2.3 + 7.0) + 0.15 * Noise(q * 5.1 + 19.0);
                float low = pow(1.0 - i.uv.y, 2.2) * smoothstep(0.0, 0.05, i.uv.y);
                float side = smoothstep(0.0, 0.2, i.uv.x) * smoothstep(1.0, 0.8, i.uv.x);
                float a = low * side * lerp(1.0, cloud * 1.8, _Drift.z);
                return half4(_Color.rgb * a, 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
