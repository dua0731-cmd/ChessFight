// The ink outline of a Skill Ink prop (R89): the same mesh pushed out along its normals by _Width metres and drawn
// inside out in the ink colour, so only a rim shows round the prop. The props' meshes have smooth normals (rounded
// boxes, spheres, tori), so the rim has no cracks at the corners. Built-in render pipeline.
Shader "ChessFight/Skill Ink Line"
{
    Properties
    {
        _InkColor ("Ink", Color) = (0.05, 0.06, 0.14, 1)
        _Width ("Width (m)", Float) = 0.02
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+11" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Cull Front
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _InkColor;
            float _Width;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            float4 vert (appdata v) : SV_POSITION
            {
                float3 w = mul(unity_ObjectToWorld, v.vertex).xyz + normalize(UnityObjectToWorldNormal(v.normal)) * _Width;
                return UnityWorldToClipPos(w);
            }

            float4 frag () : SV_Target
            {
                return float4(_InkColor.rgb, 1);
            }
            ENDCG
        }
    }
}
