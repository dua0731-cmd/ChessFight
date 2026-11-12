// R608: light added over the picture on the matching screen's stage (LoadingStudio.Stage.cs): the flare and the
// streak on a lamp turned towards the camera, soft glows. The texture's alpha shapes it, _Color (linear, brighter
// than white allowed) tints it. Unlit and written with the built-in includes, so it draws in both render pipelines.
Shader "ChessFight/Stage Flare"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Shape (alpha)", 2D) = "white" {}
        [HideInInspector] _Color ("Colour x strength (linear)", Vector) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+40" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Color;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float a = tex2D(_MainTex, i.uv).a;
                return half4(_Color.rgb * (a * a), 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
