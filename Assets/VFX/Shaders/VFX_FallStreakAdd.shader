// Speed lines left above a falling piece (R341, 11-04, W2 of the grand stair hall's falling knights): thin upright bands of
// light round its body, brightest at their foot and dying away upward, each turned round its upright to face the camera
// (in the vertex shader, so every camera sees it whole), added onto what is behind it. The mesh (VfxFallStreaks) has a
// quad a streak with its four corners all at the streak's foot, the corner in uv (0..1 across, 0..1 up), the streak's
// share of _Length in uv2.x and of _Light in uv2.y; _Length and _Light are set per renderer as the piece speeds up.
Shader "ChessFight/VFX/Fall Streak Additive"
{
    Properties
    {
        [HDR] _Color ("Glow color (HDR)", Color) = (1, 0.42, 0.12, 1)
        [HDR] _CoreColor ("Core color (HDR)", Color) = (1, 0.86, 0.66, 1)
        _Width ("Width (m)", Range(0.005, 0.3)) = 0.05
        _Falloff ("Dying away upward", Range(0.3, 6)) = 1.6
        _Soft ("Soft sides (share of the width)", Range(0.01, 1)) = 0.8
        _CoreWidth ("Core width (share of the width)", Range(0.01, 1)) = 0.3
        _Length ("Length (m)", Float) = 3
        _Light ("Light", Float) = 2
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
                half4 _CoreColor;
                float _Width;
                half _Falloff;
                half _Soft;
                half _CoreWidth;
                float _Length;
                half _Light;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 streak : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half light : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 foot = TransformObjectToWorld(input.positionOS.xyz);
                float3 toEye = _WorldSpaceCameraPos.xyz - foot;
                float3 side = cross(float3(0.0, 1.0, 0.0), float3(toEye.x, 0.0, toEye.z));
                side = dot(side, side) > 1e-8 ? normalize(side) : float3(1.0, 0.0, 0.0);
                float reach = _Length * input.streak.x;
                float3 world = foot + side * ((input.uv.x - 0.5) * _Width) + float3(0.0, input.uv.y * reach, 0.0);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.light = _Light * input.streak.y * step(0.001, reach);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half across = abs(input.uv.x * 2.0 - 1.0);
                half sides = 1.0 - smoothstep(1.0 - _Soft, 1.0, across);
                half core = 1.0 - smoothstep(0.0, _CoreWidth, across);
                half up = pow(saturate(1.0 - input.uv.y), _Falloff);
                half3 col = (_Color.rgb * sides + _CoreColor.rgb * core) * up * input.light;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
