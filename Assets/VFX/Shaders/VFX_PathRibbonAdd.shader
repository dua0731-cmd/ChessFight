// A ribbon of light drawn along a path over time (R182, 10-28, E3 of the knight's reversal: the long crescent his hind
// hooves draw over the somersault; reference E_03_flip.png). The mesh is the path, two vertices a point (VfxPathRibbon
// builds it): the second the point is drawn at in uv.x, its side (-1, +1) in uv.y, the way along the path in its normal.
// The vertex shader spreads the two across the path square to the camera (here, so every camera sees it whole, the
// capture tool's too), _Width wide where it was drawn last, a little rounded at that head, narrowing to nothing as it
// ages over _Life seconds: a crescent, brightest at its head and dying away from its tail, never closed into a ring.
// A narrow hot core in a crisp glow, added onto what is behind it; nothing is drawn ahead of _Now. Where it runs into
// a piece it fades over _DepthFade metres (the camera's depth texture; 0 turns that off). _Now, _Width, _Taper, _Life
// and _Intensity are set per renderer by VfxPathRibbon.
Shader "ChessFight/VFX/Path Ribbon Additive"
{
    Properties
    {
        [HDR] _Color ("Glow color (HDR)", Color) = (0.659, 0.439, 0.239, 1)
        [HDR] _CoreColor ("Core color (HDR)", Color) = (1, 0.945, 0.867, 1)
        _CoreBoost ("Core brightness over the glow", Float) = 2.5
        _CoreWidth ("Core width (share of the width)", Range(0.02, 1)) = 0.3
        _GlowFalloff ("Glow falloff across (below 1 crisper)", Range(0.3, 8)) = 0.8
        _EdgeSoft ("Edge softness (share of the half width)", Range(0.01, 1)) = 0.3
        _HeadGlow ("Extra brightness at the head", Range(0, 8)) = 1.5
        _HeadTime ("Head light length (s)", Range(0.005, 0.5)) = 0.06
        _HeadRound ("Head rounding (s)", Range(0, 0.2)) = 0.025
        _HeadSoft ("Head edge softness (s)", Range(0.001, 0.1)) = 0.01
        _FadePower ("Dying away toward the tail", Range(0.3, 6)) = 1.5
        _DepthFade ("Soft contact (m, 0 = off)", Range(0, 1)) = 0.05
        _Now ("Now (s)", Float) = 100
        _Width ("Width at the head (m)", Range(0.005, 1)) = 0.14
        _Taper ("Narrowing toward the tail", Range(0.1, 4)) = 0.8
        _Life ("Seconds each part of it stays", Range(0.05, 3)) = 0.55
        _Intensity ("Intensity", Float) = 1
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
                half _CoreWidth;
                half _GlowFalloff;
                half _EdgeSoft;
                half _HeadGlow;
                half _HeadTime;
                half _HeadRound;
                half _HeadSoft;
                half _FadePower;
                half _DepthFade;
                float _Now;
                float _Width;
                half _Taper;
                half _Life;
                half _Intensity;
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
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float age = max(_Now - input.uv.x, 0.0);
                // Widest where it was drawn last (a little rounded right at that head), narrowing to nothing as it ages.
                float left = saturate(1.0 - age / _Life);
                float rounded = _HeadRound > 0.0 ? lerp(0.55, 1.0, saturate(age / _HeadRound)) : 1.0;
                float halfWidth = 0.5 * _Width * pow(left, _Taper) * rounded;
                // Spread across the path square to the camera.
                float3 centre = TransformObjectToWorld(input.positionOS.xyz);
                float3 along = TransformObjectToWorldDir(input.normalOS);
                float3 side = cross(along, _WorldSpaceCameraPos.xyz - centre);
                side = dot(side, side) > 1e-10 ? normalize(side) : float3(0.0, 1.0, 0.0);
                output.positionCS = TransformWorldToHClip(centre + side * (input.uv.y * halfWidth));
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float age = _Now - input.uv.x;
                // Nothing ahead of the head, faded in just behind it.
                half drawn = saturate(age / _HeadSoft);
                half left = saturate(1.0 - max(age, 0.0) / _Life);
                half across = abs(input.uv.y);
                half core = 1.0 - smoothstep(_CoreWidth * 0.5, _CoreWidth, across);
                half body = 1.0 - smoothstep(1.0 - _EdgeSoft, 1.0, across);
                half glow = pow(saturate(1.0 - across), _GlowFalloff) * body;
                half head = exp(-max(age, 0.0) / _HeadTime);
                half3 col = (_Color.rgb * glow + _CoreColor.rgb * (core * _CoreBoost)) * (1.0 + head * _HeadGlow);
                col *= _Intensity * pow(left, _FadePower) * drawn;
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
