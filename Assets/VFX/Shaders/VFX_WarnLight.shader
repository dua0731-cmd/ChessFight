// A knight charge's warnings as standing light, not painted on the floor (R354, 11-04, the grand stair hall's guard
// knights: 승규 "데칼 말고 다른 기술로 해줘"). One additive shader for three shapes the parts build (uv2.x picks one):
//   0  a lane curtain  a low wall of light along one side of the lane (uv2.y the side, -1 / 1), uv (along 0..1, up 0..1)
//   1  a chevron       an arrowhead floating _Hover over the lane, its own shape in the vertex position (tip at the
//                      origin, pointing +z, a unit wide), carried down the lane by _Scroll from its phase (uv2.y)
//   2  a ring curtain  a wall of light round where the knight lands, _Radius times uv2.y (short of a wall), uv (round
//                      0..1, up 0..1)
// The lane runs from the landing (z 0) to where the charge stops (z _Length), _Width across. Curtains stand taller and
// brighter as _Fill charges up (on the lane: where the fill has reached), with a bright rim on top, dying away upward;
// on the lane everything behind _Passed (the charging knight) is out. _Urgency blinks it; _Fade fades it. Every value
// is set per renderer by VfxLaneGlow / VfxRingGlow.
Shader "ChessFight/VFX/Warn Light"
{
    Properties
    {
        [HDR] _Color ("Glow color (HDR)", Color) = (1, 0.3, 0.05, 1)
        [HDR] _HotColor ("Hot color (HDR)", Color) = (1, 0.72, 0.42, 1)
        _Light ("Light", Float) = 1.6
        _Falloff ("Dying away upward", Range(0.3, 6)) = 1.8
        _RimLight ("Top rim light", Float) = 1.5
        _ChevronLight ("Chevron light", Float) = 2.2
        _Blink ("Blinks a second when urgent", Float) = 7
        _DepthFade ("Soft contact (m, 0 = off)", Range(0, 1)) = 0.12
        _Length ("Lane length (m)", Float) = 8
        _Width ("Lane width (m)", Float) = 1.4
        _Radius ("Ring radius (m)", Float) = 2
        _Height ("Curtain height (m)", Float) = 0.9
        _Hover ("Chevron height (m)", Float) = 0.35
        _ChevronSize ("Chevron size (m)", Float) = 0.6
        _Fill ("Fill", Range(0, 1)) = 0.5
        _Passed ("Passed", Range(0, 1)) = 0
        _Scroll ("Scroll (lane lengths)", Float) = 0
        _Urgency ("Urgency", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
        _Age ("Age (s)", Float) = 0
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
                half4 _HotColor;
                half _Light;
                half _Falloff;
                half _RimLight;
                half _ChevronLight;
                float _Blink;
                half _DepthFade;
                float _Length;
                float _Width;
                float _Radius;
                float _Height;
                float _Hover;
                float _ChevronSize;
                half _Fill;
                half _Passed;
                float _Scroll;
                half _Urgency;
                half _Fade;
                float _Age;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 shape : TEXCOORD1;    // kind (0 lane curtain, 1 chevron, 2 ring), side or phase
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 tint : TEXCOORD1;      // rgb light, a: 0 curtain look, 1 chevron look
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float kind = input.shape.x;
                float3 p;
                half along = 0.0, lit = 1.0, ends = 1.0;
                if (kind < 0.5)
                {
                    float z = input.uv.x * _Length;
                    along = input.uv.x;
                    lit = along <= _Fill ? 1.0 : 0.35;
                    p = float3(input.shape.y * _Width * 0.5, input.uv.y * _Height * lerp(0.4, 1.0, lit), z);
                }
                else if (kind < 1.5)
                {
                    along = frac(input.shape.y + _Scroll);
                    lit = along <= _Fill ? 1.0 : 0.35;
                    ends = saturate(along * 8.0) * saturate((1.0 - along) * 8.0);
                    p = float3(0.0, _Hover, along * _Length) + input.positionOS.xyz * _ChevronSize;
                }
                else
                {
                    float turn = input.uv.x * 6.2831853;
                    // Each column's share of the radius (short of a wall: VfxRingGlow.Fit).
                    float out_ = _Radius * (input.shape.y > 0.0 ? input.shape.y : 1.0);
                    lit = 1.0;
                    p = float3(out_ * cos(turn), input.uv.y * _Height * lerp(0.3, 1.0, _Fill), out_ * sin(turn));
                }
                half live = kind < 1.5 ? smoothstep(_Passed - 0.03, _Passed, along) : 1.0;
                half blink = 1.0 + _Urgency * 0.9 * (0.5 + 0.5 * cos(_Age * _Blink * 6.2831853));
                half fill = kind > 1.5 ? lerp(0.35, 1.0, _Fill) : lit;
                output.positionCS = TransformObjectToHClip(p);
                output.uv = input.uv;
                output.tint = half4(fill * live * ends * blink * _Fade * _Light, 0, 0, kind > 0.5 && kind < 1.5 ? 1.0 : 0.0);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half k = input.tint.r;
                half3 col;
                if (input.tint.a > 0.5)
                    col = (_Color.rgb + _HotColor.rgb * 0.6) * _ChevronLight;
                else
                {
                    half up = saturate(input.uv.y);
                    half body = pow(1.0 - up, _Falloff);
                    half rim = exp(-(1.0 - up) * 25.0) * _RimLight;
                    half foot = exp(-up * 10.0) * 0.8;
                    col = _Color.rgb * body + _HotColor.rgb * (rim + foot);
                }
                col *= k;
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
