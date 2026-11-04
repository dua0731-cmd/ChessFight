// The swirl of air at a vent's mouth (R351, 11-04, the grand stair hall's royal fans): a flat disc on the fan's face,
// faint curling arms that turn with the blades (_Turn, radians, set as the fan spins), thickest between the hub and the
// rim, added over the fan so the blades read through it. _Strength (0 at rest, rising through the warning, 1 while it
// blows) and _Turn are set per renderer by VfxWindSwirl; the disc is a quad one unit across (uv 0..1).
Shader "ChessFight/VFX/Wind Swirl"
{
    Properties
    {
        [HDR] _Color ("Color (HDR)", Color) = (0.86, 0.93, 1, 1)
        _Light ("Light", Float) = 0.9
        _Arms ("Arms", Float) = 5
        _Curl ("Curl (turns from hub to rim)", Float) = 0.6
        _ArmWidth ("Arm width (share)", Range(0.05, 0.9)) = 0.35
        _Hub ("Hub (share of the radius)", Range(0, 0.8)) = 0.22
        _Strength ("Strength", Range(0, 1)) = 1
        _Turn ("Turn (rad)", Float) = 0
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
                half _Light;
                float _Arms;
                float _Curl;
                half _ArmWidth;
                half _Hub;
                half _Strength;
                float _Turn;
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
                float2 c = input.uv * 2.0 - 1.0;
                float r = length(c);
                float turn = atan2(c.y, c.x);
                // Arms curling out from the hub, turning with the blades.
                float s = frac((turn - _Turn) / 6.2831853 * _Arms + r * _Curl * _Arms);
                half arm = 1.0 - smoothstep(_ArmWidth * 0.5, _ArmWidth * 0.5 + 0.25, abs(s - 0.5));
                half ring = smoothstep(_Hub, _Hub + 0.15, r) * (1.0 - smoothstep(0.75, 1.0, r));
                half glow = (arm * 0.8 + 0.2) * ring * _Strength;
                return half4(_Color.rgb * _Light * glow, 0.0);
            }
            ENDHLSL
        }
    }
}
