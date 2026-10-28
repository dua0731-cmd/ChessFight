// A star glint that turns to the camera (R175, 10-28, where the bishop's two swords cross): a small hot core in a soft
// round glow, four long thin rays (along the screen's sides, turned by _Turn degrees) tapering to points and four short
// ones between them, added onto what is behind it. The quad is turned to the camera here in the vertex shader, so it
// faces every camera (the capture tool's too) with no script; its size is the object's scale. _Intensity, _Fade and
// _Turn are set per renderer by VfxGlint.
Shader "ChessFight/VFX/Glint Additive"
{
    Properties
    {
        [HDR] _Color ("Color (HDR)", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _Falloff ("Glow falloff", Range(0.5, 16)) = 6
        _Core ("Core size (share of the radius)", Range(0, 0.5)) = 0.07
        _RayThin ("Ray thinness", Range(4, 400)) = 40
        _ShortRays ("Short rays' length (share of the long ones)", Range(0, 1)) = 0.45
        _RayStrength ("Ray strength", Float) = 1
        _Turn ("Turn (degrees)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Fade;
                half _Falloff;
                half _Core;
                half _RayThin;
                half _ShortRays;
                half _RayStrength;
                float _Turn;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Laid out round the object's place square to the view, as wide as the object's scale.
                float size = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float3 centre = TransformWorldToView(TransformObjectToWorld(float3(0, 0, 0)));
                output.positionCS = TransformWViewToHClip(centre + float3(input.positionOS.xy * size, 0));
                output.uv = input.uv;
                return output;
            }

            // A thin ray along x through the middle, reach long each way, narrowing to a point.
            half Ray(float2 p, half reach)
            {
                half along = saturate(1.0 - abs(p.x) / max(reach, 0.001));
                return exp(-abs(p.y) * _RayThin / max(along, 0.02)) * pow(along, 1.5);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 q = input.uv * 2.0 - 1.0;
                float s, c;
                sincos(radians(_Turn), s, c);
                float2 p = float2(c * q.x + s * q.y, c * q.y - s * q.x);
                float2 d = float2(p.x + p.y, p.y - p.x) * 0.70710678;
                half r = length(q);
                half glow = pow(saturate(1.0 - r), _Falloff);
                half core = saturate(1.0 - r / max(_Core, 0.001));
                half rays = max(max(Ray(p, 1.0), Ray(p.yx, 1.0)), max(Ray(d, _ShortRays), Ray(d.yx, _ShortRays)));
                half lit = glow + core * core + rays * _RayStrength;
                return half4(_Color.rgb * _Intensity * lit * _Fade, 1.0);
            }
            ENDHLSL
        }
    }
}
