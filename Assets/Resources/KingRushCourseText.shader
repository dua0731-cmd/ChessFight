Shader "ChessFight/KingRushCourseText"
{
    Properties { _MainTex ("Font atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Lighting Off Cull Off ZWrite Off ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct Input { float4 position : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            Output vert(Input v)
            {
                Output o; o.position = UnityObjectToClipPos(v.position); o.uv = v.uv; o.color = v.color; return o;
            }
            fixed4 frag(Output i) : SV_Target
            { return fixed4(i.color.rgb, i.color.a * tex2D(_MainTex, i.uv).a); }
            ENDCG
        }
    }
}
