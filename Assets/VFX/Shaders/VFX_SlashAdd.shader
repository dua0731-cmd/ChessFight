// A slash of light hanging in the air (R175, 10-28, the bishop's X where his two swords cross): a crescent ribbon drawn
// by a LineRenderer (u along it from where the cut starts, v across), a narrow hot core in a soft glow, added onto what
// is behind it. It is drawn up to _Reveal along its length (past 1 all of it), brightest at that head where the blade
// is, the extra light behind the head dying over _HeadLength; both tips fade out. Where it runs into the floor or a
// piece it fades over _DepthFade metres (the camera's depth texture; 0 turns that off). _Intensity, _Reveal and _Fade
// are set per renderer by VfxSlash.
Shader "ChessFight/VFX/Slash Additive"
{
    Properties
    {
        [HDR] _Color ("Glow color (HDR)", Color) = (0.76, 0.79, 0.83, 1)
        [HDR] _CoreColor ("Core color (HDR)", Color) = (1, 1, 1, 1)
        _CoreBoost ("Core brightness over the glow", Float) = 3
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _Reveal ("Drawn along its length", Float) = 2
        _RevealSoft ("Head edge softness (share of the length)", Range(0.005, 0.3)) = 0.03
        _HeadGlow ("Extra brightness at the head", Range(0, 8)) = 2
        _HeadLength ("Head light length (share of the length)", Range(0.01, 0.5)) = 0.1
        _CoreWidth ("Core width (share of the width)", Range(0.02, 1)) = 0.24
        _GlowFalloff ("Glow falloff across", Range(0.5, 8)) = 2.2
        _TipFade ("Tip fade (share of the length at each end)", Range(0.01, 0.5)) = 0.12
        _DepthFade ("Soft contact (m, 0 = off)", Range(0, 1)) = 0.1
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
                half _Intensity;
                half _Fade;
                float _Reveal;
                half _RevealSoft;
                half _HeadGlow;
                half _HeadLength;
                half _CoreWidth;
                half _GlowFalloff;
                half _TipFade;
                half _DepthFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float u = input.uv.x;
                half across = abs(input.uv.y * 2.0 - 1.0);
                half core = 1.0 - smoothstep(_CoreWidth * 0.5, _CoreWidth, across);
                half glow = pow(saturate(1.0 - across), _GlowFalloff);
                half shown = 1.0 - smoothstep(_Reveal - _RevealSoft, _Reveal, u);
                half head = exp(-max(_Reveal - u, 0.0) / _HeadLength);
                half tips = smoothstep(0.0, _TipFade, u) * smoothstep(0.0, _TipFade, 1.0 - u);
                half3 col = (_Color.rgb * glow + _CoreColor.rgb * core * _CoreBoost) * (1.0 + head * _HeadGlow);
                col *= _Intensity * shown * tips * _Fade * input.color.rgb * input.color.a;
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
