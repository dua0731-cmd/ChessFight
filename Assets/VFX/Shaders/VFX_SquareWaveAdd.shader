// The wave rising off the sides of the king's square (10-28, F1 of his counter, the guard; the user: "when the border
// comes up, a ripple like water rises softly as far as the king's belly"): a low wall of gold light standing on the four
// sides of his square, brightest at the floor and thinning to nothing at the height of his belly, with ripples running up
// it that sway a little along the sides, like light on water seen through a curtain. It climbs from the floor to the
// belly over _Rise, then keeps moving. Added onto the scene, a wall of an open box (the mesh's v is the height, 0 floor
// to 1 belly; its u runs once round the four sides, 0 to 4). VfxSquareGrid scales the box to the square and the belly's
// height and sets _Rise, _WaveTime, _Intensity and _Fade per renderer.
Shader "ChessFight/VFX/Square Wave Additive"
{
    Properties
    {
        [HDR] _Color ("Light (HDR)", Color) = (0.8, 0.52, 0.12, 1)
        _Waves ("Ripples up the height", Range(0.5, 8)) = 2.5
        _WaveSpeed ("Ripples rising (turns a second)", Range(0, 3)) = 0.55
        _Sway ("Sideways sway (ripples)", Range(0, 1)) = 0.35
        _Body ("Steady glow under the ripples", Range(0, 1)) = 0.3
        _Reach ("How far up the glow thins (power)", Range(0.5, 4)) = 1.6
        _Rise ("Climbed (0 floor to 1 belly)", Range(0, 1.2)) = 1
        _WaveTime ("Seconds", Float) = 0
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Range(0, 1)) = 1
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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Waves;
                float _WaveSpeed;
                float _Sway;
                half _Body;
                float _Reach;
                float _Rise;
                float _WaveTime;
                half _Intensity;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float height = saturate(input.uv.y);
                float around = input.uv.x;
                // The glow thins as it goes up and is gone at the belly.
                half thin = pow(1.0 - height, _Reach);
                // How far it has climbed: a soft front, nothing above it.
                half climbed = 1.0 - smoothstep(_Rise - 0.15, _Rise, height);
                // The sides sway the ripples a little as they go along (two slow waves of different lengths).
                float sway = sin(around * 12.566 + _WaveTime * 1.3) * 0.5 + sin(around * 18.850 - _WaveTime * 0.9) * 0.3;
                float ripple = 0.5 + 0.5 * sin(6.2832 * (height * _Waves - _WaveTime * _WaveSpeed + sway * _Sway));
                half band = ripple * ripple * ripple;
                half value = thin * climbed * (_Body + (1.0 - _Body) * band);
                return half4(_Color.rgb * (value * _Intensity * _Fade), 1.0);
            }
            ENDHLSL
        }
    }
}
