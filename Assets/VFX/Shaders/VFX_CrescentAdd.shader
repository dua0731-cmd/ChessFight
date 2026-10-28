// The cut of light a big sword stroke leaves (R183, 10-28, F3 of the king's counter; reference F_03_counter.png): a
// crescent band round where the stroke turns, the mesh VfxCrescent makes in its own XY plane (uv.x along it from where
// the stroke starts, uv.y across from its inner edge to its outer one where the blade's edge ran, uv.z metres out past
// that edge into the soft glow round it, uv.w its width there), turned _Face of the way toward the camera about its
// middle in the vertex shader so the player behind him still sees a crescent and not its edge. A hot thin line runs
// along the outer edge; inside it a gold glow of fine streaks along the stroke, each its own brightness and length,
// dies away toward the inner edge. It is drawn up to _Reveal along its length, brightest at that head where the blade
// is now; then it burns away (_Burn, 0 none, past 1 all of it) from its start and its inside first, so the edge near its
// end goes last, the burning edge a little brighter. Added onto what is behind it; where it runs into the floor it
// fades over _DepthFade metres (the camera's depth texture; 0 turns that off). _Intensity, _Reveal, _Burn and _Fade are
// set per renderer by VfxCrescent.
Shader "ChessFight/VFX/Crescent Additive"
{
    Properties
    {
        [HDR] _Color ("Glow color (HDR)", Color) = (0.8, 0.52, 0.12, 1)
        [HDR] _EdgeColor ("Edge line color (HDR)", Color) = (1, 0.96, 0.84, 1)
        _EdgeBoost ("Edge line brightness over the glow", Float) = 2
        _EdgeWidth ("Edge line width (m)", Range(0.003, 0.1)) = 0.012
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Range(0, 1)) = 1
        _Reveal ("Drawn along its length", Float) = 2
        _RevealSoft ("Head edge softness (share of the length)", Range(0.005, 0.3)) = 0.04
        _HeadGlow ("Extra brightness at the head", Range(0, 8)) = 1
        _HeadLength ("Head light length (share of the length)", Range(0.01, 0.6)) = 0.18
        _Inner ("Glow falloff toward the inner edge", Range(0.3, 6)) = 1.6
        _Halo ("Soft glow past the edge (m)", Range(0.005, 0.5)) = 0.05
        _Streaks ("Streaks across", Range(2, 80)) = 26
        _StreakSharp ("Streak thinness", Range(1, 16)) = 2
        _StreakMix ("Streak contrast", Range(0, 1)) = 0.5
        _StreakTail ("Shortest streak behind the head (share of the length)", Range(0, 1)) = 0.45
        _TipFade ("Tip fade (share of the length at each end)", Range(0.01, 0.5)) = 0.08
        _Burn ("Burnt away", Range(0, 1.2)) = 0
        _BurnSoft ("Burn edge softness", Range(0.01, 0.5)) = 0.14
        _BurnGlow ("Burning edge brightness", Range(0, 4)) = 0.6
        _Face ("Turned toward the camera (share)", Range(0, 1)) = 0.6
        _DepthFade ("Soft contact (m, 0 = off)", Range(0, 1)) = 0.12
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
                half4 _EdgeColor;
                half _EdgeBoost;
                half _EdgeWidth;
                half _Intensity;
                half _Fade;
                float _Reveal;
                half _RevealSoft;
                half _HeadGlow;
                half _HeadLength;
                half _Inner;
                half _Halo;
                half _Streaks;
                half _StreakSharp;
                half _StreakMix;
                half _StreakTail;
                half _TipFade;
                half _Burn;
                half _BurnSoft;
                half _BurnGlow;
                half _Face;
                half _DepthFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
            };

            float Hash(float n)
            {
                return frac(sin(n * 12.9898 + 4.1414) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 p = input.positionOS.xyz;
                // Its plane (the object's XY) turned _Face of the way toward the camera, about the object's origin.
                float3 toEye = normalize(TransformWorldToObject(GetCameraPositionWS()));
                float3 n = float3(0.0, 0.0, toEye.z >= 0.0 ? 1.0 : -1.0);
                float3 axis = cross(n, toEye);
                float s = length(axis);
                if (s > 1e-4)
                {
                    axis /= s;
                    float angle = atan2(s, dot(n, toEye)) * _Face;
                    float c = cos(angle), sn = sin(angle);
                    p = p * c + cross(axis, p) * sn + axis * dot(axis, p) * (1.0 - c);
                }
                output.positionCS = TransformObjectToHClip(p);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float u = input.uv.x;
                float y = saturate(input.uv.y);
                float past = max(input.uv.z, 0.0);
                float w = max(input.uv.w, 1e-4);

                // The hot line along the edge, by metres from it (inside or out).
                float d = (1.0 - y) * w + past;
                half edge = exp(-(d * d) / (_EdgeWidth * _EdgeWidth));
                // The glow: dying toward the inner edge inside, a short soft fall past the edge.
                half glow = past > 0.0 ? exp(-past / _Halo) : pow(y, _Inner);

                // Streaks along the stroke: rings across, each its own brightness and how far behind the head it reaches;
                // rings narrower than about two pixels melt into an even smear instead of shimmering.
                float ring = y * _Streaks;
                float id = min(floor(ring), _Streaks - 1.0);
                half h1 = Hash(id), h2 = Hash(id + 17.31);
                half sharp = max(_StreakSharp, 1.0);
                half average = sqrt(2.0 / (PI * (sharp + 0.5)));
                half fiber = pow(saturate(sin(PI * frac(ring))), sharp);
                half clear = saturate(1.5 - fwidth(ring) * 2.0);
                half fibers = lerp(average, fiber, clear) / average;
                half tone = lerp(1.0, 0.25 + 1.5 * h1, _StreakMix);
                half streak = tone * lerp(1.0, fibers, _StreakMix);
                float behind = max(_Reveal - u, 0.0);
                float reach = lerp(_StreakTail, 1.3, h2);
                streak *= 1.0 - smoothstep(reach * 0.7, reach, behind);
                half body = past > 0.0 ? 1.0 : streak;

                half shown = 1.0 - smoothstep(_Reveal - _RevealSoft, _Reveal, u);
                half head = exp(-behind / _HeadLength);
                half tips = smoothstep(0.0, _TipFade, u) * smoothstep(0.0, _TipFade, 1.0 - u);

                // Burnt away from its start and its inside first: what is left at the end is the edge near its end.
                float along = saturate(u / max(min(_Reveal, 1.0), 0.05));
                float last = saturate(0.5 * along + 0.5 * y) + (h1 - 0.5) * 0.12;
                half keep = smoothstep(_Burn, _Burn + _BurnSoft, last + _BurnSoft);
                half burning = _Burn > 0.0 ? keep * (1.0 - keep) * 4.0 * _BurnGlow : 0.0;

                half3 col = _Color.rgb * glow * body + _EdgeColor.rgb * edge * _EdgeBoost;
                col *= (1.0 + head * _HeadGlow) * (keep + burning);
                col *= _Intensity * shown * tips * _Fade;
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
