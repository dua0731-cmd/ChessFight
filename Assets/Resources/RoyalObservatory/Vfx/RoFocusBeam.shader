// R725 ro-vfx (the great telescope's focus beams; user 11-20 "먼지 지워주고 빛 그래픽 더 좋게 표현해 실사처럼"): the column
// of light from the objective's focus lens down to the hot spot, drawn as light in air rather than as a surface. The
// cone mesh (RoyalObservatoryVfxBeam, a little wider than the light) only bounds it: only its far faces are drawn, so
// every pixel the light could be in is shaded once, and each works out the light along its own ray of sight -
//   where the ray passes nearest the beam's axis (_BeamStart the spot's middle, _BeamEnd the lens), how far that is in
//   the beam's radius there (_SpotRadius at the spot to _LensRadius at the lens), and how long the ray stays in the
//   beam (longer the more nearly it looks along it): light ~ exp(-k q²) x that length, so the column has no edge, fades
//   smoothly at its sides, and grows when seen end-on (from the deck looking up toward the lens).
//   The ray stops at whatever the camera's depth texture saw (the deck, a pawn, a leaf), so what stands in front hides
//   the light behind it. Brighter toward the lens (the same light through less), a faint haze drifting along it and
//   faint streaks along its length - nothing else. Every value but the look is set per renderer.
Shader "ChessFight/VFX/RO Focus Beam"
{
    Properties
    {
        [HDR] _Color ("Light (HDR)", Color) = (1, 0.9, 0.74, 1)
        _Glow ("Light in the air", Float) = 0.55
        _Falloff ("Fall-off across it", Range(0.5, 8)) = 2.6
        _Concentrate ("Brighter toward the lens", Range(0, 1.5)) = 0.7
        _Haze ("Haze drifting in it", Range(0, 1)) = 0.18
        _Light ("Light", Float) = 1
        _BeamStart ("Spot middle (world)", Vector) = (0, 0, 0, 0)
        _BeamEnd ("Lens (world)", Vector) = (0, 1, 0, 0)
        _SpotRadius ("Radius at the spot (m)", Float) = 1.2
        _LensRadius ("Radius at the lens (m)", Float) = 0.18
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
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Glow;
                half _Falloff;
                half _Concentrate;
                half _Haze;
                half _Light;
                float4 _BeamStart;
                float4 _BeamEnd;
                float _SpotRadius;
                float _LensRadius;
                float _Age;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p), u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 eye = _WorldSpaceCameraPos.xyz;
                float3 ray = normalize(input.positionWS - eye);
                float3 a0 = _BeamStart.xyz;
                float3 axis = _BeamEnd.xyz - a0;
                float length_ = max(length(axis), 0.01);
                axis /= length_;

                // How far the eye sees along this ray: to the scene the depth texture saw.
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float raw = SampleSceneDepth(uv);
            #if !UNITY_REVERSED_Z
                raw = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, raw);
            #endif
                float3 scene = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float sceneDist = length(scene - eye);

                // The ray's point nearest the axis (the ray from the eye, the axis as a line), kept in front of the
                // eye and short of the scene.
                float3 w = eye - a0;
                float b = dot(ray, axis), c = dot(ray, w), e = dot(axis, w);
                float across = max(1.0 - b * b, 1e-4);
                float s = clamp((b * e - c) / across, 0.0, sceneDist);
                float3 onRay = eye + ray * s;
                float u = clamp(dot(onRay - a0, axis), 0.0, length_);
                float3 onAxis = a0 + axis * u;
                float t = u / length_;                                  // 0 the spot, 1 the lens
                float radius = lerp(_SpotRadius, _LensRadius, t);
                float q = length(onRay - onAxis) / max(radius, 0.01);

                // The light: dense on the axis, nothing past the radius, longer the more nearly the ray runs along it.
                half density = exp(-_Falloff * q * q) * saturate(1.0 - q * q * 0.6);
                half chord = sqrt(saturate(1.0 - q * q * 0.8)) / sqrt(max(across, 0.0225));
                half concentrate = pow(saturate(_SpotRadius / max(radius, 0.01)), _Concentrate);
                half ends = (1.0 - smoothstep(0.95, 1.0, t)) * smoothstep(0.0, 0.02, t);
                // faint haze drifting down it, and faint streaks along its length
                half haze = 1.0 - _Haze + _Haze * Noise(float2(u * 0.7 + _Age * 0.25, q * 2.0 + 3.0));
                float3 side = onRay - onAxis;
                float3 x0 = normalize(cross(axis, abs(axis.y) < 0.95 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 y0 = cross(axis, x0);
                float turn = atan2(dot(side, y0), dot(side, x0));
                half streak = 0.88 + 0.12 * Noise(float2(cos(turn) * 2.0 + u * 0.12, sin(turn) * 2.0));   // round it: no seam

                half light = _Glow * density * chord * concentrate * haze * streak * ends;
                return half4(_Color.rgb * (light * _Light), 0.0);
            }
            ENDHLSL
        }
    }
}
