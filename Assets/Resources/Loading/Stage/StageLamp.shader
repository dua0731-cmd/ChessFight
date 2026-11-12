// R608: things that give off light on the matching screen's stage (LoadingStudio.Stage.cs): the ring's neon
// tubes, the lamps' lenses, the light strips round the chessboard and along the wall. One flat colour brighter than
// white (the bloom makes the glow), a little hotter where the surface faces the eye, as a tube's core does.
// Unlit and written with the built-in includes, so it draws in both render pipelines; _Color comes in linear.
Shader "ChessFight/Stage Lamp"
{
    // Listed so each material keeps its own value (see StageBeam.shader). A vector, not a colour: it comes in linear.
    Properties
    {
        [HideInInspector] _Color ("Colour x strength (linear), a: hot middle", Vector) = (1, 1, 1, 0)
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

            float4 _Color;   // linear colour times strength; a: how much hotter the middle is (0: flat)

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 world : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // (a mesh without normals stays flat instead of turning into NaNs, which the bloom would spread)
                float len = dot(i.normal, i.normal);
                float facing = len > 1e-6 ? abs(dot(i.normal * rsqrt(len), normalize(_WorldSpaceCameraPos - i.world))) : 0.7071;
                return half4(_Color.rgb * (1.0 + _Color.a * (facing * facing - 0.5)), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
