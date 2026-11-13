// The team divider and the team barriers: see-through, so the other team's lane stays in view
// the whole race (design doc §3), with faint horizontal bands so the wall itself can be seen.
// URP: lit like URP's Lit (Shaders/URP/CFLit.hlsl), faded by its alpha.
Shader "ChessFight/CourseGlass"
{
    Properties
    {
        _Color ("Tint", Color) = (0.6, 0.82, 0.95, 0.18)
        _BandColor ("Band", Color) = (0.85, 0.95, 1, 0.45)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.9
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex CFLitVertex
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Assets/Shaders/URP/CFLit.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Color, _BandColor;
            half _Glossiness;
            CBUFFER_END

            half4 frag(CFLitVaryings i, bool front : SV_IsFrontFace) : SV_Target
            {
                float f = frac(i.positionWS.y / 2.0);
                float band = step(f, 0.04);
                half4 c = lerp(_Color, _BandColor, band);
                // Both faces show (Cull Off): light the back one from its own side.
                if (!front) i.normalWS = -i.normalWS;
                return CFShade(i, c.rgb, 0, _Glossiness, c.a);
            }
            ENDHLSL
        }
    }
}
