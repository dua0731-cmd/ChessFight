// The low wall of light riding a landing piece's shockwave (R341, 11-04, I2 of the grand stair hall's falling knights):
// an open ring of upright band at the hit's reach, brightest at the floor and dying away upward, brighter where the eye
// looks along it (the ring's sides) than through it (its middle), so it reads as a thin shell of air pushed out. Added
// onto what is behind it, fading where it meets the floor or a piece (the camera's depth texture). The mesh
// (VfxShockWall) is a ring of radius 1 and height 1 (uv.x round, uv.y up); VfxShockWall scales it to the reach and the
// wall's height and sets _Light per renderer.
Shader "ChessFight/VFX/Shock Wall Additive"
{
    Properties
    {
        [HDR] _Color ("Glow color (HDR)", Color) = (1, 0.5, 0.15, 1)
        [HDR] _CoreColor ("Floor core color (HDR)", Color) = (1, 0.86, 0.66, 1)
        _Falloff ("Dying away upward", Range(0.3, 6)) = 2.2
        _Edge ("Brighter where seen along it", Range(0, 6)) = 2.5
        _Base ("Bright band at the floor (share of the height)", Range(0.01, 0.5)) = 0.12
        _DepthFade ("Soft contact (m, 0 = off)", Range(0, 1)) = 0.12
        _Light ("Light", Float) = 1.5
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
                half _Falloff;
                half _Edge;
                half _Base;
                half _DepthFade;
                half _Light;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = _WorldSpaceCameraPos.xyz - world;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS), v = normalize(input.viewWS);
                half along = pow(1.0 - saturate(abs(dot(n, v))), _Edge);
                half up = pow(saturate(1.0 - input.uv.y), _Falloff);
                half band = exp(-input.uv.y / _Base);
                half3 col = (_Color.rgb * (0.25 + along) * up + _CoreColor.rgb * band * 0.8) * _Light;
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
