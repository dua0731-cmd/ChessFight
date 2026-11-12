// R608: the row of bulbs round the ring on the matching screen's stage (LoadingStudio.Stage.cs), as on a game
// show's sign. The mesh is a flat band round the ring (its vertices are metres from the ring's centre); the bulbs are
// drawn on it: one in three lit, stepping round the ring (not sliding: a click at a time). A player coming in lights
// them all for a moment (_State.x), everyone in makes every other bulb run twice as fast (_State.y).
// Unlit and written with the built-in includes, so it draws in both render pipelines; colours come in linear.
Shader "ChessFight/Stage Marquee"
{
    // Listed so each material keeps its own values (see StageBeam.shader).
    Properties
    {
        [HideInInspector] _Bulbs ("Count, ring radius, bulb radius", Vector) = (60, 3.5, 0.09, 0)
        [HideInInspector] _State ("All lit, everyone in, brightness, step", Vector) = (0, 0, 1, 0)
        [HideInInspector] _ColOn ("Lit (linear)", Vector) = (3, 2.4, 1, 1)
        [HideInInspector] _ColOff ("Unlit (linear)", Vector) = (0.1, 0.05, 0.01, 1)
        [HideInInspector] _ColPlate ("Plate (linear)", Vector) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Bulbs;   // how many, the radius they sit on (m), a bulb's radius (m), 0
            float4 _State;   // all lit (0..1), everyone in (0..1), brightness, which step the run is on (whole steps count)
            float4 _ColOn, _ColOff, _ColPlate;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 p : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.p = vertex.xy;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float arc = 6.2831853 / _Bulbs.x;
                float k = floor(atan2(i.p.y, i.p.x) / arc + 0.5);
                float2 at = _Bulbs.y * float2(cos(k * arc), sin(k * arc));
                float d = length(i.p - at) / _Bulbs.z;
                float bulb = smoothstep(1.0, 0.8, d);
                float core = smoothstep(0.85, 0.0, d);
                float n = k + _Bulbs.x;
                float step3 = step(fmod(n + floor(_State.w), 3.0), 0.5);
                float step2 = step(fmod(n + floor(_State.w * 2.0), 2.0), 0.5);
                float lit = saturate(lerp(step3, step2, _State.y) + _State.x);
                float3 lamp = lerp(_ColOff.rgb, _ColOn.rgb * (0.55 + 0.9 * core), lit);
                return half4(lerp(_ColPlate.rgb, lamp * _State.z, bulb), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
