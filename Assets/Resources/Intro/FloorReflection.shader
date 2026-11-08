// The title screen's polished board (10-17, 승규 님: "바닥도 이런 광택나는 디자인으로"): laid just over the
// lit board, it blends in what a mirror camera under the floor sees (IntroStage.Gloss.cs), softened by a few
// taps and stronger at grazing angles, so the wall, the pennants and the runners shine back from the board.
// The mirror camera has no roll, so its picture is the reflection turned upside down: sampled at (u, 1 - v).
// Unlit, so it works in both render pipelines.
Shader "ChessFight/Floor Reflection"
{
    Properties
    {
        _ReflTex ("Mirror picture", 2D) = "black" {}
        _Tint ("Tint", Color) = (1, 0.96, 0.9, 1)
        _Strength ("Strength straight down", Range(0, 1)) = 0.22
        _Grazing ("Strength at grazing angles", Range(0, 1)) = 0.6
        _Blur ("Blur (screen fraction)", Float) = 0.0025
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-60" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _ReflTex;
            float4 _Tint;
            float _Strength, _Grazing, _Blur;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screen : TEXCOORD0;
                float3 world : TEXCOORD1;
            };

            v2f vert (float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.screen = ComputeScreenPos(o.pos);
                o.world = mul(unity_ObjectToWorld, vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.screen.xy / i.screen.w;
                uv.y = 1 - uv.y;
                float b = _Blur;
                float3 c = tex2D(_ReflTex, uv).rgb * 0.4
                         + (tex2D(_ReflTex, uv + float2(b, 0)).rgb + tex2D(_ReflTex, uv - float2(b, 0)).rgb
                          + tex2D(_ReflTex, uv + float2(0, b * 1.6)).rgb + tex2D(_ReflTex, uv - float2(0, b * 1.6)).rgb) * 0.15;
                float3 toEye = normalize(_WorldSpaceCameraPos - i.world);
                float fresnel = pow(1 - saturate(toEye.y), 4);
                return fixed4(c * _Tint.rgb, lerp(_Strength, _Grazing, fresnel) * _Tint.a);
            }
            ENDCG
        }
    }
}
