// A warning painted onto the floor where something is about to strike (R341, 11-04, W1 of the grand stair hall's falling
// knights: 승규 "기본 데칼 말고 고급 데칼 넣어서 차오르는 애니메이션"). A projected decal of our own, not URP's decal
// feature: the renderer is a box over the floor, and for each pixel the box covers the shader rebuilds the point the
// camera's depth texture saw there, keeps it only if it lies inside the box on a surface facing up (_Upward), and paints
// it by where it lies in the box. So it lands on a step, a slope or a stair's edge as they are and never in the air past
// them, and a piece standing on it is only tinted where it faces up near the floor. Drawn premultiplied: a dark shade
// (_Shade, alpha = how dark) under an added HDR glow, so it reads on cream stone as on dark carpet.
// What it draws, in metres from the box's middle (+Z = the obstacle's forward):
//   the danger disc   _Radius, faint (_IdleShade) from the start, darker (_FillShade) where the fill has reached
//   the fill          from the middle to the rim as _Fill goes 0 -> 1, a hot front line with a glow trailing inward and
//                     a faint checkerboard (_Square) inside it
//   the rim           a bright line with a glow outside it and a thin inner ring (_InnerRing)
//   the ticks         _Ticks round the rim, lit clockwise from the front as _Sweep goes 0 -> 1 (a countdown)
//   the landing mark  a ring and an X where the piece comes down (_Mark)
// _Urgency blinks the rim, ticks and mark; _Strike turns it all red and floods the disc (R534: the charge is done, it
// is coming down); _Fade fades it all. VfxGroundWarn sets _Fill, _Sweep, _Urgency, _Fade, _Age
// (seconds, the blink's clock) and _Radius / _Margin (the box is _Radius * _Margin each way) per renderer.
Shader "ChessFight/VFX/Ground Warn Decal"
{
    Properties
    {
        [HDR] _Color ("Glow color (HDR)", Color) = (1, 0.3, 0.05, 1)
        [HDR] _HotColor ("Hot core color (HDR)", Color) = (1, 0.72, 0.42, 1)
        _Shade ("Shade under the glow (a = how dark)", Color) = (0.2, 0.035, 0.005, 0.42)
        _IdleShade ("Shade ahead of the fill", Range(0, 1)) = 0.3
        _FillShade ("Shade inside the fill", Range(0, 1)) = 1
        _RimWidth ("Rim width (m)", Range(0.01, 0.5)) = 0.1
        _RimGlow ("Rim glow outside (m)", Range(0.01, 1.5)) = 0.35
        _RimLight ("Rim light", Float) = 3
        _InnerRing ("Inner ring (share of the radius)", Range(0.5, 1)) = 0.86
        _InnerLight ("Inner ring light", Float) = 1
        _Ticks ("Ticks round the rim", Float) = 32
        _TickWidth ("Tick width (share of its slot)", Range(0.05, 0.9)) = 0.36
        _TickLight ("Lit tick light", Float) = 2.4
        _TickIdle ("Unlit tick light", Range(0, 1)) = 0.12
        _FrontWidth ("Fill front width (m)", Range(0.01, 0.5)) = 0.08
        _FrontTrail ("Fill front glow inward (m)", Range(0.05, 3)) = 0.8
        _FrontLight ("Fill front light", Float) = 2.6
        _TrailLight ("Trailing glow light", Float) = 0.9
        _Square ("Checker square (m)", Range(0.2, 3)) = 0.75
        _CheckerLight ("Checker light", Range(0, 2)) = 0.3
        _Mark ("Landing mark radius (m)", Range(0.2, 2)) = 0.9
        _MarkWidth ("Landing mark line width (m)", Range(0.01, 0.3)) = 0.06
        _MarkLight ("Landing mark light", Float) = 1.8
        _Blink ("Blinks a second when urgent", Float) = 7
        _Upward ("Floor facing up past (cos)", Range(0, 1)) = 0.6
        _EdgeFade ("Fade at the box's top and bottom (share)", Range(0.01, 0.5)) = 0.2
        _Soft ("Line softness (m)", Range(0.002, 0.1)) = 0.02
        _Radius ("Radius (m)", Float) = 5
        _Margin ("Box half-width over the radius", Range(1, 1.5)) = 1.12
        _Fill ("Fill", Range(0, 1)) = 0.5
        _Sweep ("Ticks lit", Range(0, 1)) = 0.5
        _Urgency ("Urgency", Range(0, 1)) = 0
        [HDR] _StrikeColor ("Glow color once it strikes (HDR)", Color) = (1.8, 0.05, 0.04, 1)
        [HDR] _StrikeHot ("Hot core color once it strikes (HDR)", Color) = (1.6, 0.32, 0.26, 1)
        _StrikeFlood ("Red flood over the disc once it strikes", Range(0, 1)) = 0.45
        _Strike ("Strike", Range(0, 1)) = 0
        _Fade ("Fade", Range(0, 1)) = 1
        _Age ("Age (s)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Decal"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            // The box's far faces, always: they cover what the box covers on screen, the camera inside it too.
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _HotColor;
                half4 _Shade;
                half _IdleShade;
                half _FillShade;
                float _RimWidth;
                float _RimGlow;
                half _RimLight;
                float _InnerRing;
                half _InnerLight;
                float _Ticks;
                half _TickWidth;
                half _TickLight;
                half _TickIdle;
                float _FrontWidth;
                float _FrontTrail;
                half _FrontLight;
                half _TrailLight;
                float _Square;
                half _CheckerLight;
                float _Mark;
                float _MarkWidth;
                half _MarkLight;
                float _Blink;
                half _Upward;
                half _EdgeFade;
                float _Soft;
                float _Radius;
                float _Margin;
                half _Fill;
                half _Sweep;
                half _Urgency;
                half4 _StrikeColor;
                half4 _StrikeHot;
                half _StrikeFlood;
                half _Strike;
                half _Fade;
                float _Age;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            #include "VFX_DecalProjection.hlsl"

            // A line `width` wide where `offset` is 0, soft over _Soft.
            half Line(float offset, float width)
            {
                return 1.0 - smoothstep(width * 0.5, width * 0.5 + _Soft, abs(offset));
            }

            half4 frag(Varyings input) : SV_Target
            {
                half surface;
                float3 local = DecalLocal(input.positionCS, _Upward, _EdgeFade, surface);

                // Metres from the middle on the box's floor plane, +y = forward.
                float2 p = local.xz * (2.0 * _Radius * _Margin);
                float r = length(p);
                float reach = _Fill * _Radius;
                half inDisc = 1.0 - smoothstep(_Radius - _Soft, _Radius, r);
                half filled = (1.0 - smoothstep(reach - _Soft, reach, r)) * step(0.001, _Fill);
                half blink = 1.0 + _Urgency * 0.9 * (0.5 + 0.5 * cos(_Age * _Blink * 6.2831853));

                // The rim, its glow outside, the inner ring.
                half rim = Line(r - _Radius, _RimWidth);
                // Gone before the box's sides, so they never show as a straight edge.
                half boxEdge = 1.0 - smoothstep(_Radius * (_Margin - 0.06), _Radius * _Margin, max(abs(p.x), abs(p.y)));
                half rimGlow = exp(-max(r - _Radius, 0.0) / _RimGlow) * step(_Radius, r) * boxEdge;
                half inner = Line(r - _Radius * _InnerRing, _RimWidth * 0.3);

                // The ticks between the inner ring and the rim, clockwise from the front.
                float turn = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);
                float slot = turn * _Ticks;
                half tickShape = 1.0 - smoothstep(_TickWidth * 0.5, _TickWidth * 0.5 + 0.08, abs(frac(slot) - 0.5));
                half band = step(_Radius * (_InnerRing + 0.025), r) * step(r, _Radius - _RimWidth * 0.8);
                half tickOn = step((floor(slot) + 0.5) / _Ticks, _Sweep);
                half ticks = tickShape * band * lerp(_TickIdle, 1.0, tickOn);

                // The fill: its front, the glow trailing inward, a faint board inside.
                half front = Line(r - reach, _FrontWidth) * step(0.001, _Fill) * inDisc;
                half trail = exp(-max(reach - r, 0.0) / _FrontTrail) * filled * inDisc;
                float2 square = floor(p / _Square);
                half checker = abs(fmod(square.x + square.y, 2.0)) * filled * inDisc * (0.35 + 0.65 * trail);

                // Where the piece comes down: a ring and an X inside it.
                float2 diagonal = float2(p.x - p.y, p.x + p.y) * 0.70710678;
                half mark = Line(r - _Mark, _MarkWidth)
                          + Line(min(abs(diagonal.x), abs(diagonal.y)), _MarkWidth) * step(r, _Mark * 0.72);

                // R534 (승규 11-09 "차징이 다 완료돼서 떨어질 때 빨간색으로 뜨게"): red once it strikes.
                half3 glowColor = lerp(_Color.rgb, _StrikeColor.rgb, _Strike);
                half3 hotColor = lerp(_HotColor.rgb, _StrikeHot.rgb, _Strike);
                half3 glow = glowColor * (rim * _RimLight * blink + rimGlow * 0.6 + inner * _InnerLight
                                         + ticks * _TickLight * blink + trail * _TrailLight + checker * _CheckerLight)
                           + hotColor * (front * _FrontLight + rim * 0.5 * blink + saturate(mark) * _MarkLight * blink)
                           + _StrikeColor.rgb * (_Strike * _StrikeFlood * inDisc);
                half shade = _Shade.a * inDisc * lerp(_IdleShade, _FillShade, filled);

                half mask = surface * _Fade;
                return half4((glow + _Shade.rgb * shade) * mask, shade * mask);
            }
            ENDHLSL
        }
    }
}
