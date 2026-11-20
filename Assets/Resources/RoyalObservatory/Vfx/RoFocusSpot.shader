// R725 ro-vfx (the great telescope's focus beams; user 11-20 "먼지 지워주고 빛 그래픽 더 좋게 표현해 실사처럼"): the hot spot
// on the deck as light falling on it, not paint over it - a projected decal (VFX_DecalProjection.hlsl: only surfaces
// facing up inside the box, so it follows the deck model) on a box _Half m either way round the spot's middle, drawn
// multiplying what is there (Blend DstColor One: the deck x (1 + light)), so the marble's veins and the gold lines show
// through, brighter and warmer, as under a real lamp. A pool a little hotter in its middle, a slightly brighter rim
// where the focused light gathers, its edge exactly at _Radius (FocusBeam throws a pawn that stays inside it, so what
// is lit is what throws) with a narrow soft penumbra, and a faint spill just past it. Round all the way: nothing reaches
// the box's edge. _Radius, _Half, _Light and _Age are set per renderer by RoyalObservatoryVfxBeam.
Shader "ChessFight/VFX/RO Focus Spot"
{
    Properties
    {
        [HDR] _Color ("Light (HDR)", Color) = (1, 0.9, 0.72, 1)
        _Gain ("How much brighter it makes the deck", Float) = 2.2
        _Hot ("Hotter middle", Range(0, 1)) = 0.3
        _Rim ("Brighter rim", Range(0, 1)) = 0.35
        _Penumbra ("Soft edge (m)", Range(0.01, 0.3)) = 0.07
        _Spill ("Spill past the edge", Range(0, 1)) = 0.12
        _Upward ("Floor facing up past (cos)", Range(0, 1)) = 0.6
        _EdgeFade ("Fade at the box's top and bottom (share)", Range(0.01, 0.5)) = 0.25
        _Radius ("Spot radius (m)", Float) = 1.2
        _Half ("Box half side (m)", Float) = 1.8
        _Light ("Light", Float) = 1
        _Age ("Age (s)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-9" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Decal"
            Tags { "LightMode" = "UniversalForward" }
            Blend DstColor One
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/VFX/Shaders/VFX_DecalProjection.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Gain;
                half _Hot;
                half _Rim;
                float _Penumbra;
                half _Spill;
                half _Upward;
                half _EdgeFade;
                float _Radius;
                float _Half;
                half _Light;
                float _Age;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half surface;
                float3 local = DecalLocal(input.positionCS, _Upward, _EdgeFade, surface);
                float2 p = local.xz * (2.0 * _Half);
                float r = length(p);
                float k = r / max(_Radius, 0.01);

                half lit = 1.0 - smoothstep(_Radius - _Penumbra, _Radius + _Penumbra * 0.4, r);
                half pool = lerp(1.0 - _Hot, 1.0, exp(-k * k * 1.6));
                half rim = _Rim * exp(-abs(r - (_Radius - _Penumbra * 0.6)) / max(_Penumbra * 0.6, 0.005)) * lit;
                half spill = _Spill * exp(-max(r - _Radius, 0.0) / 0.25) * (1.0 - lit);
                half disc = 1.0 - smoothstep(_Radius + 0.25, min(_Radius + 0.5, _Half * 0.92), r);

                half light = (lit * pool + rim + spill) * _Gain * _Light * surface * disc;
                return half4(_Color.rgb * light, 1.0);
            }
            ENDHLSL
        }
    }
}
