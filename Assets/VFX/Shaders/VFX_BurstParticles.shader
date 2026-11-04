// A burst of particles worked out from their age alone (R341, 11-04, I1 of the grand stair hall's falling knights: the
// flash, dust, stone chips and sparks as one lands). The mesh (VfxParticleBurst) has a quad a particle with its four
// corners at the particle's start; the vertex shader moves it to where it is _Age seconds after the burst - its start
// velocity slowed by _Drag (1/s) and pulled down by _Gravity (m/s²), kept over _FloorY - grows it from _SizeStart to
// _SizeEnd over its life, turns it to the camera (stretched along its motion by _Stretch seconds of travel, or spun), and
// colours it from _ColorStart to _ColorEnd. Nothing is simulated, so any moment can be drawn straight away and every PC
// draws the same one. Drawn premultiplied: _Additive 1 adds light (a flash, sparks), 0 lays it over (dust, chips). The
// vertex carries, in uv2, the particle's life (s), size (times), delay (s) and spin (deg/s) and, in uv3, its start
// velocity (not the normal, which batching may normalise). Every value here is set per renderer by VfxParticleBurst.
Shader "ChessFight/VFX/Burst Particles"
{
    Properties
    {
        [HDR] _ColorStart ("Color at birth (HDR)", Color) = (1, 1, 1, 1)
        [HDR] _ColorEnd ("Color at death (HDR)", Color) = (1, 1, 1, 1)
        _Light ("Light", Float) = 1
        _AlphaStart ("Opacity at birth", Range(0, 1)) = 1
        _AlphaEnd ("Opacity at death", Range(0, 1)) = 0
        _FadeIn ("Fade in (share of its life)", Range(0.001, 1)) = 0.05
        _SizeStart ("Size at birth (m)", Float) = 0.2
        _SizeEnd ("Size at death (m)", Float) = 0.4
        _Stretch ("Stretch (seconds of travel)", Float) = 0
        _Soft ("Soft edge (share)", Range(0.01, 1)) = 0.6
        _Hard ("Round (0) to square (1)", Range(0, 1)) = 0
        _Additive ("Additive", Range(0, 1)) = 0
        _DepthFade ("Soft contact (m, 0 = off)", Range(0, 1)) = 0
        _Gravity ("Gravity (m/s²)", Float) = 9.8
        _Drag ("Drag (1/s)", Float) = 1
        _FloorY ("Floor (object y)", Float) = 0
        _Age ("Seconds since the burst", Float) = 0
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
                half4 _ColorStart;
                half4 _ColorEnd;
                half _Light;
                half _AlphaStart;
                half _AlphaEnd;
                half _FadeIn;
                float _SizeStart;
                float _SizeEnd;
                float _Stretch;
                half _Soft;
                half _Hard;
                half _Additive;
                half _DepthFade;
                float _Gravity;
                float _Drag;
                float _FloorY;
                float _Age;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 corner : TEXCOORD0;
                float4 particle : TEXCOORD1;   // life (s), size (times), delay (s), spin (deg/s)
                float3 velocity : TEXCOORD2;   // start velocity (m/s, object space)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float t = _Age - input.particle.z;
                float life = max(input.particle.x, 0.001);
                float f = saturate(t / life);
                half alive = step(0.0, t) * step(t, life);
                t = max(t, 0.0);

                // Linear drag: the start velocity dies away as e^(-kt); gravity tends to its terminal speed g/k.
                float k = max(_Drag, 0.001);
                float e = exp(-k * t);
                float3 p = input.positionOS.xyz + input.velocity * ((1.0 - e) / k);
                p.y -= _Gravity / k * (t - (1.0 - e) / k);
                float3 v = input.velocity * e;
                v.y -= _Gravity / k * (1.0 - e);
                // On the floor it slides and stops falling.
                if (p.y < _FloorY) { p.y = _FloorY; v.y = 0.0; }

                float3 world = TransformObjectToWorld(p);
                float3 motion = mul((float3x3)GetObjectToWorldMatrix(), v);
                float3 toEye = normalize(_WorldSpaceCameraPos.xyz - world);
                float3 across = motion - dot(motion, toEye) * toEye;
                float speed = length(across);
                float size = lerp(_SizeStart, _SizeEnd, f) * input.particle.y * alive;
                float3 along, side;
                float reach = size;
                if (_Stretch > 0.0 && speed > 0.001)
                {
                    along = across / speed;
                    side = normalize(cross(toEye, along));
                    reach += speed * _Stretch * alive;
                }
                else
                {
                    float angle = radians(input.particle.w) * t;
                    float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                    along = up * cos(angle) + right * sin(angle);
                    side = right * cos(angle) - up * sin(angle);
                }
                world += side * ((input.corner.x - 0.5) * size) + along * ((input.corner.y - 0.5) * reach);
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.corner;
                half a = lerp(_AlphaStart, _AlphaEnd, f) * saturate(f / _FadeIn) * alive;
                output.color = half4(lerp(_ColorStart.rgb, _ColorEnd.rgb, f) * _Light, a);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 c = input.uv * 2.0 - 1.0;
                half disc = 1.0 - smoothstep(1.0 - _Soft, 1.0, length(c));
                half square = 1.0 - smoothstep(1.0 - _Soft, 1.0, max(abs(c.x), abs(c.y)));
                half a = input.color.a * lerp(disc, square, _Hard);
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
