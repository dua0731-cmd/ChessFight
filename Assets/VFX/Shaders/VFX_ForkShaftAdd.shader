// Shafts of light rising off the knight's fork squares (R179, 10-28, F5 of the fork dive; reference F_04_fork.png): on
// each of the eight squares an upright band of light, brightest at the floor and dying away upward, soft at its sides,
// turned round its upright to face the camera (here in the vertex shader, so every camera sees it whole, the capture
// tool's too), added onto what is behind it. The mesh has a quad a square with its four corners all at the square's
// middle on the floor (one object unit a square, x to the knight's right, z along its way), the corner in uv (0..1
// across, 0..1 up) and the square's index in uv2.x (SwordFightSkills.KnightMoves order); a square that is off folds to
// nothing, an enemy's stands _FoeTaller times as tall and _FoeBoost times as bright. Where it runs into a piece it fades
// over _DepthFade metres (the camera's depth texture; 0 turns that off). How far they have risen (_Rise), their light
// and fade, and which squares are on or have an enemy are set per renderer by VfxForkGrid.
Shader "ChessFight/VFX/Fork Shaft Additive"
{
    Properties
    {
        [HDR] _Color ("Glow color (HDR)", Color) = (0.659, 0.439, 0.239, 1)
        [HDR] _CoreColor ("Core color (HDR)", Color) = (1, 0.945, 0.867, 1)
        _CoreBoost ("Core brightness over the glow", Float) = 1
        _Width ("Width (m)", Range(0.05, 2)) = 0.6
        _Height ("Height (m)", Range(0.1, 4)) = 1
        _FoeTaller ("Enemy square's shaft height (times)", Range(1, 3)) = 1.25
        _Falloff ("Dying away upward", Range(0.3, 6)) = 1.8
        _Soft ("Soft sides (share of the width)", Range(0.01, 1)) = 0.7
        _CoreWidth ("Core width (share of the width)", Range(0.01, 1)) = 0.35
        _Base ("Bright band at the floor (m)", Range(0.005, 0.5)) = 0.06
        _BaseGlow ("Bright band's brightness", Range(0, 8)) = 1.5
        _DepthFade ("Soft contact (m, 0 = off)", Range(0, 1)) = 0.05
        _Rise ("Risen", Range(0, 1)) = 1
        _ShaftLight ("Light", Float) = 1.5
        _FoeBoost ("Enemy square light (times)", Float) = 1.5
        _ShaftFade ("Fade", Range(0, 1)) = 1
        _SquareOnA ("Squares 0-3 on", Vector) = (1, 1, 1, 1)
        _SquareOnB ("Squares 4-7 on", Vector) = (1, 1, 1, 1)
        _SquareFoeA ("Squares 0-3 enemy", Vector) = (0, 0, 0, 0)
        _SquareFoeB ("Squares 4-7 enemy", Vector) = (0, 0, 0, 0)
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                half _CoreBoost;
                float _Width;
                float _Height;
                half _FoeTaller;
                half _Falloff;
                half _Soft;
                half _CoreWidth;
                float _Base;
                half _BaseGlow;
                half _DepthFade;
                half _Rise;
                half _ShaftLight;
                half _FoeBoost;
                half _ShaftFade;
                float4 _SquareOnA;
                float4 _SquareOnB;
                float4 _SquareFoeA;
                float4 _SquareFoeB;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 square : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float height : TEXCOORD1;
                half light : TEXCOORD2;
            };

            // Square i's flag (0..7) out of the two halves of eight.
            half Pick(float4 low, float4 high, float i)
            {
                return dot(low, saturate(1.0 - abs(i - float4(0, 1, 2, 3)))) + dot(high, saturate(1.0 - abs(i - float4(4, 5, 6, 7))));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float i = input.square.x;
                half on = Pick(_SquareOnA, _SquareOnB, i), foe = Pick(_SquareFoeA, _SquareFoeB, i);
                // Stood up on the square's middle and turned round the upright there to face the camera.
                float3 foot = TransformObjectToWorld(float3(input.positionOS.x, 0.0, input.positionOS.z));
                float3 toEye = _WorldSpaceCameraPos.xyz - foot;
                float3 side = cross(float3(0.0, 1.0, 0.0), float3(toEye.x, 0.0, toEye.z));
                side = dot(side, side) > 1e-8 ? normalize(side) : float3(1.0, 0.0, 0.0);
                float height = _Height * _Rise * lerp(1.0, _FoeTaller, foe) * on;
                float3 world = foot + side * ((input.uv.x - 0.5) * _Width * on) + float3(0.0, input.uv.y * height, 0.0);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.height = height;
                output.light = _ShaftLight * lerp(1.0, _FoeBoost, foe) * _ShaftFade * on;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half across = abs(input.uv.x * 2.0 - 1.0);
                half sides = 1.0 - smoothstep(1.0 - _Soft, 1.0, across);
                half core = 1.0 - smoothstep(0.0, _CoreWidth, across);
                half up = pow(saturate(1.0 - input.uv.y), _Falloff);
                // A brighter band where it stands on the floor, _Base metres tall.
                half band = exp(-input.uv.y * input.height / _Base) * _BaseGlow;
                half3 col = (_Color.rgb * sides + _CoreColor.rgb * (core * _CoreBoost)) * up + _CoreColor.rgb * (band * sides);
                col *= input.light;
                if (_DepthFade > 0.0)
                {
                    float2 screen = GetNormalizedScreenSpaceUV(input.positionCS);
                    float scene = LinearEyeDepth(SampleSceneDepth(screen), _ZBufferParams);
                    float self = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                    col *= saturate((scene - self) / _DepthFade);
                }
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
