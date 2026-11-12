// R608: what floats in the air of the matching screen's stage (LoadingStudio.Stage.cs), worked out from the time
// alone so it needs no particle system: every quad of the mesh is one mote, its vertex position a place in the unit
// box (stretched to _Box), its second UV two random numbers, its colour its tint.
//   - Dust (_Fall.x = 0): motes drifting slowly through the lights, each twinkling at its own pace.
//   - Glitter (_Fall.x > 0, when everyone is in): foil falling from above, tumbling (a flash each time a piece faces
//     the eye). It starts at _Fall.z: a piece shows only once it has had the time to fall in from the top.
// Unlit and written with the built-in includes, so it draws in both render pipelines; colours come in linear.
Shader "ChessFight/Stage Dust"
{
    // Listed so each material keeps its own values (see StageBeam.shader).
    Properties
    {
        [HideInInspector] _BoxMin ("Box corner (world)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _BoxSize ("Box size", Vector) = (1, 1, 1, 0)
        [HideInInspector] _Mote ("Size, drift, twinkle", Vector) = (0.05, 1, 0.8, 0)
        [HideInInspector] _Fall ("Fall speed, tumble, began, shown", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Color ("Colour x strength (linear)", Vector) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+30" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _CfStageTime;
            float4 _BoxMin, _BoxSize;   // world
            float4 _Mote;               // size (m), drift speed, twinkle (0..1), 0
            float4 _Fall;               // fall speed (m/s; 0: dust), tumble speed, when it began, how much shows (0..1)
            float4 _Color;              // linear colour times strength

            struct appdata
            {
                float4 vertex : POSITION;
                float2 corner : TEXCOORD0;
                float2 rnd : TEXCOORD1;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 corner : TEXCOORD0;
                float4 tint : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float t = _CfStageTime;
                float3 p = v.vertex.xyz;
                float2 corner = v.corner;
                float shown = 1.0, light = 1.0;
                if (_Fall.x > 0.0)
                {
                    float fell = (t - _Fall.z) * _Fall.x * (0.7 + 0.6 * v.rnd.x) / _BoxSize.y;
                    shown = step(1.0 - p.y, fell) * _Fall.w;
                    p.y = 1.0 - frac(p.y + fell);
                    p.x += sin(t * (1.1 + v.rnd.y) + v.rnd.x * 40.0) * 0.012;
                    p.z += cos(t * (0.9 + v.rnd.x) + v.rnd.y * 31.0) * 0.012;
                    float spin = t * _Fall.y * (0.6 + v.rnd.y) + v.rnd.x * 50.0;
                    float turn = t * (1.0 + v.rnd.x) + v.rnd.y * 20.0;
                    float face = cos(spin);
                    corner = float2(corner.x * face, corner.y * 1.7);
                    corner = float2(corner.x * cos(turn) - corner.y * sin(turn), corner.x * sin(turn) + corner.y * cos(turn));
                    light = 0.35 + 2.2 * pow(abs(face), 10.0);
                    shown *= smoothstep(0.0, 0.08, p.y);
                }
                else
                {
                    p.x += sin(t * 0.11 * (0.5 + v.rnd.x) + v.rnd.y * 40.0) * 0.03 + t * 0.004 * _Mote.y;
                    p.y += t * 0.006 * _Mote.y * (0.4 + v.rnd.x);
                    p.z += cos(t * 0.09 * (0.5 + v.rnd.y) + v.rnd.x * 31.0) * 0.03;
                    p = frac(p);
                    float3 rim = smoothstep(0.0, 0.06, p) * smoothstep(1.0, 0.94, p);
                    shown = rim.x * rim.y * rim.z;
                    light = lerp(1.0, 0.5 + 0.5 * sin(t * (1.5 + 3.0 * v.rnd.y) + v.rnd.x * 60.0), _Mote.z);
                }
                float3 world = _BoxMin.xyz + p * _BoxSize.xyz;
                float size = _Mote.x * (0.5 + v.rnd.x);
                world += (UNITY_MATRIX_V[0].xyz * corner.x + UNITY_MATRIX_V[1].xyz * corner.y) * size;
                o.pos = mul(UNITY_MATRIX_VP, float4(world, 1));
                o.corner = v.corner;
                o.tint = float4(v.color.rgb * _Color.rgb * (light * shown), _Fall.x);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // dust is a soft round speck, foil a small flat piece
                float round = saturate(1.0 - length(i.corner));
                float a = i.tint.a > 0.0 ? 1.0 : round * round;
                return half4(i.tint.rgb * a, 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
