// The air a vent blows (R351, 11-04, the grand stair hall's royal fans: 승규 "선풍기 vfx 적용 시켜줘 데칼 말고"): streaks
// of air, or motes of dust riding it, out of the fan along its front (+z), wound round its axis as the blades throw them
// (_Twist radians a metre near the fan, straightening over _TwistFade metres), spreading (_Spread) and wavering
// (_Wobble) as they go, gone by _Reach. Nothing is simulated: each streak runs out again and again at its own phase while
// the vent blows, and where it is is worked out from _Tau (seconds since the blow started) alone - so any moment can be
// drawn straight away, the same on every PC, and the last runs finish their way after the blow stops. The mesh
// (VfxWindFlow) is a ribbon of rows a streak, all at the streak's lane: uv (0..1 across, 0..1 tail to head), uv2 its lane
// (angle, radius at the fan, phase 0..1, speed times), uv3 its look (length times, width times, light times, seed).
// Drawn premultiplied: _Additive 1 adds light, 0 lays it over; _Round 1 draws each as a round mote instead of a streak.
// Every value is set per renderer by VfxWindFlow.
Shader "ChessFight/VFX/Wind Flow"
{
    Properties
    {
        [HDR] _Color ("Color (HDR)", Color) = (0.86, 0.93, 1, 1)
        _Light ("Light", Float) = 1.5
        _Alpha ("Opacity", Range(0, 1)) = 0.35
        _Additive ("Additive", Range(0, 1)) = 0.5
        _Speed ("Speed (m/s)", Float) = 9
        _Reach ("Reach (m)", Float) = 6
        _Gap ("Rest between runs (s)", Float) = 0.15
        _Length ("Length (m)", Float) = 1.6
        _Width ("Width (m)", Float) = 0.05
        _Twist ("Twist near the fan (rad/m)", Float) = 1.6
        _TwistFade ("Twist dies over (m)", Float) = 2.2
        _Spread ("Spread (radius grows per m)", Float) = 0.1
        _Wobble ("Wobble (m)", Float) = 0.06
        _WobbleRate ("Wobble waves per m", Float) = 1.4
        _HeadBias ("Brighter toward the head", Range(0.2, 4)) = 1.6
        _InFade ("Fade in from the fan (m)", Float) = 0.35
        _OutFade ("Fade out before the reach (m)", Float) = 1.8
        _Round ("Streak (0) or mote (1)", Range(0, 1)) = 0
        _DepthFade ("Soft contact (m, 0 = off)", Range(0, 1)) = 0.1
        _Tau ("Seconds since the blow started", Float) = 1
        _Blow ("Blow (s)", Float) = 3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Light;
                half _Alpha;
                half _Additive;
                float _Speed;
                float _Reach;
                float _Gap;
                float _Length;
                float _Width;
                float _Twist;
                float _TwistFade;
                float _Spread;
                float _Wobble;
                float _WobbleRate;
                half _HeadBias;
                float _InFade;
                float _OutFade;
                half _Round;
                half _DepthFade;
                float _Tau;
                float _Blow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 corner : TEXCOORD0;
                float4 lane : TEXCOORD1;    // angle (rad), radius at the fan (m), phase (0..1), speed (times)
                float4 look : TEXCOORD2;    // length (times), width (times), light (times), seed
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : TEXCOORD1;
            };

            // Where a streak on this lane is `z` metres out from the fan, in the fan's frame.
            float3 PathAt(float z, float4 lane, float seed)
            {
                float r = lane.y * (1.0 + _Spread * z);
                float turn = lane.x + _Twist * _TwistFade * (1.0 - exp(-z / max(_TwistFade, 0.01)));
                float wave = z * _WobbleRate * 6.2831853 + seed * 6.2831853;
                return float3(r * cos(turn) + _Wobble * sin(wave), r * sin(turn) + _Wobble * cos(wave * 1.3), z);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float speed = _Speed * input.lane.w;
                float run = _Length * input.look.x;
                float period = (_Reach + run) / max(speed, 0.01) + _Gap;
                // The last run that started while it blew (none before the blow).
                float last = min(_Tau, _Blow) - input.lane.z * period;
                half started = step(0.0, last);
                float start = input.lane.z * period + floor(max(last, 0.0) / period) * period;
                float head = (_Tau - start) * speed;
                float tail = clamp(head - run, 0.0, _Reach);
                head = clamp(head, 0.0, _Reach);
                half alive = started * step(0.001, head - tail);

                float z = lerp(tail, head, input.corner.y);
                float3 here = PathAt(z, input.lane, input.look.w);
                float3 ahead = PathAt(z + 0.05, input.lane, input.look.w);
                float3 world = TransformObjectToWorld(here);
                float3 along = normalize(TransformObjectToWorld(ahead) - world);
                float3 toEye = normalize(_WorldSpaceCameraPos.xyz - world);
                float3 side = cross(along, toEye);
                side = dot(side, side) > 1e-8 ? normalize(side) : float3(1.0, 0.0, 0.0);
                float width = _Width * input.look.y * lerp(lerp(0.35, 1.0, input.corner.y), 1.0, _Round) * alive;
                world += side * ((input.corner.x - 0.5) * width);
                // A mote is as long as it is wide, round its middle.
                world += along * ((input.corner.y - 0.5) * width * _Round);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.corner;

                half fade = saturate(z / max(_InFade, 0.01)) * saturate((_Reach - z) / max(_OutFade, 0.01));
                half towardHead = lerp(pow(saturate(input.corner.y), _HeadBias), 1.0, _Round);
                half a = _Alpha * input.look.z * fade * towardHead * alive;
                output.color = half4(_Color.rgb * _Light * input.look.z, a);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 c = input.uv * 2.0 - 1.0;
                half streak = 1.0 - smoothstep(0.15, 1.0, abs(c.x));
                half mote = 1.0 - smoothstep(0.3, 1.0, length(c));
                half a = input.color.a * lerp(streak, mote, _Round);
                if (_DepthFade > 0.0)
                {
                    float2 screen = GetNormalizedScreenSpaceUV(input.positionCS);
                    float scene = LinearEyeDepth(SampleSceneDepth(screen), _ZBufferParams);
                    float self = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                    a *= saturate((scene - self) / _DepthFade);
                }
                return half4(input.color.rgb * a, a * (1.0 - _Additive));
            }
            ENDHLSL
        }
    }
}
