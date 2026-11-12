// R608: draws the letters of a dynamic font (their shapes are in the font texture's alpha) white into a texture,
// for the mode's name on the matching screen's LED wall (LoadingStudio.Stage.cs, DrawTitle). The brightest wins
// where two letters' boxes overlap. Works in both render pipelines (drawn with GL, outside any camera).
Shader "ChessFight/Stage Text"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Font texture", 2D) = "white" {}
    }
    SubShader
    {
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            BlendOp Max
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

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
                return half4(a, a, a, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
