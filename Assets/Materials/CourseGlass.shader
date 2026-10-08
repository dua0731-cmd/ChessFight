// The team divider and the team barriers: see-through, so the other team's lane stays in view
// the whole race (design doc §3), with faint horizontal bands so the wall itself can be seen.
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
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200
        ZWrite Off
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0

        struct Input
        {
            float3 worldPos;
        };

        fixed4 _Color, _BandColor;
        half _Glossiness;

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float f = frac(IN.worldPos.y / 2.0);
            float band = step(f, 0.04);
            fixed4 c = lerp(_Color, _BandColor, band);
            o.Albedo = c.rgb;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
