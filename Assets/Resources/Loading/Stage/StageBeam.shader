// R608: a moving head's beam in the stage's haze (LoadingStudio.Stage.cs). The mesh is a unit tube along +Z
// (radius 1, z from 0 to 1); the vertex shader opens it into a cone from _Shape.x at the lamp to _Shape.y at its far
// end, _Shape.z metres away. Both sides are added: where the eye looks through the middle of the cone it sees most
// light, at its rim none, which reads as a soft shaft. The light thins as the cone widens, fades out before its far
// end (the pool on the floor is a real spotlight's) and is broken up by haze drifting through it.
// Unlit and written with the built-in includes, so it draws in both render pipelines; _Color comes in linear.
Shader "ChessFight/Stage Beam"
{
    // Every value is listed here so each material keeps its own (a shader with no properties is drawn by the SRP
    // batcher, which would give every lamp the same colour). Vectors, not colours: they come in linear.
    Properties
    {
        [HideInInspector] _Color ("Colour x strength (linear)", Vector) = (1, 1, 1, 1)
        [HideInInspector] _Shape ("Radius at the lamp, at the far end, length", Vector) = (0.13, 1.5, 20, 0)
        [HideInInspector] _Soft ("Rim power, fade power, haze amount, haze size", Vector) = (1.7, 1.35, 0.55, 0.55)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+20" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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
            float4 _Color;   // linear colour times strength
            float4 _Shape;   // radius at the lamp, radius at the far end, length, 0
            float4 _Soft;    // rim softness (power), fade along the length (power), haze amount, haze size (1 / metres)

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float along : TEXCOORD2;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                float z = vertex.z;
                float r = lerp(_Shape.x, _Shape.y, z);
                float3 local = float3(vertex.xy * r, z * _Shape.z);
                float3 n = float3(vertex.xy * _Shape.z, _Shape.x - _Shape.y);
                o.world = mul(unity_ObjectToWorld, float4(local, 1)).xyz;
                o.normal = mul((float3x3)unity_ObjectToWorld, n);
                o.pos = mul(UNITY_MATRIX_VP, float4(o.world, 1));
                o.along = z;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1) * 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 toEye = normalize(_WorldSpaceCameraPos - i.world);
                float through = pow(abs(dot(normalize(i.normal), toEye)), _Soft.x);
                float r = lerp(_Shape.x, _Shape.y, i.along);
                float thin = pow(_Shape.x / r, 0.55);
                float fade = pow(saturate(1.0 - i.along), _Soft.y) * smoothstep(0.0, 0.015, i.along);
                float3 np = i.world * _Soft.w + float3(0.13, -0.05, 0.08) * _CfStageTime;
                float haze = 0.65 * Noise(np) + 0.35 * Noise(np * 2.7 + 11.0);
                float a = through * thin * fade * lerp(1.0, 0.4 + 1.2 * haze, _Soft.z);
                return half4(_Color.rgb * a, 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
