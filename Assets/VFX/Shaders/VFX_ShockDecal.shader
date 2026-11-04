// The shockwave a landing piece sends along the floor (R341, 11-04, I2 of the grand stair hall's falling knights): a
// bright band at exactly the reach the obstacle's hit has at that moment (_Front, m: what it strikes is what is inside
// it), a glow trailing in behind it, flickering a little round the ring, and a faint line at the most it will reach
// (_Radius) so where it stops reads too. A projected decal like VFX_GroundWarnDecal (VFX_DecalProjection.hlsl: only floor
// facing up inside the box), added onto what is under it. _Front, _Radius, _Box (the box's side, m), _Fade and _Age (the
// flicker's clock) are set per renderer by VfxShockDecal.
Shader "ChessFight/VFX/Shock Decal"
{
    Properties
    {
        [HDR] _Color ("Glow color (HDR)", Color) = (1, 0.45, 0.1, 1)
        [HDR] _HotColor ("Front core color (HDR)", Color) = (1, 0.86, 0.66, 1)
        _FrontWidth ("Front width (m)", Range(0.01, 1)) = 0.16
        _FrontLight ("Front light", Float) = 3
        _Trail ("Glow trailing inward (m)", Range(0.05, 4)) = 1.1
        _TrailLight ("Trailing glow light", Float) = 1.2
        _Flicker ("Flicker round the ring", Range(0, 1)) = 0.35
        _LimitWidth ("Most reach line width (m)", Range(0.005, 0.3)) = 0.05
        _LimitLight ("Most reach line light", Float) = 0.6
        _Soft ("Line softness (m)", Range(0.002, 0.2)) = 0.04
        _Upward ("Floor facing up past (cos)", Range(0, 1)) = 0.6
        _EdgeFade ("Fade at the box's top and bottom (share)", Range(0.01, 0.5)) = 0.2
        _Front ("Front (m)", Float) = 2.5
        _Radius ("Most reach (m)", Float) = 5
        _Box ("Box side (m)", Float) = 11
        _Fade ("Fade", Range(0, 1)) = 1
        _Age ("Age (s)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-9" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Decal"
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
            #include "VFX_DecalProjection.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _HotColor;
                float _FrontWidth;
                half _FrontLight;
                float _Trail;
                half _TrailLight;
                half _Flicker;
                float _LimitWidth;
                half _LimitLight;
                float _Soft;
                half _Upward;
                half _EdgeFade;
                float _Front;
                float _Radius;
                float _Box;
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

            // A line `width` wide where `offset` is 0, soft over _Soft.
            half Line(float offset, float width)
            {
                return 1.0 - smoothstep(width * 0.5, width * 0.5 + _Soft, abs(offset));
            }

            half4 frag(Varyings input) : SV_Target
            {
                half surface;
                float3 local = DecalLocal(input.positionCS, _Upward, _EdgeFade, surface);
                float2 p = local.xz * _Box;
                float r = length(p);
                float turn = atan2(p.x, p.y);

                // A few waves of different speeds round the ring, so its light is never quite even.
                half wave = 0.5 + 0.25 * sin(turn * 7.0 + _Age * 23.0) + 0.25 * sin(turn * 13.0 - _Age * 31.0);
                half flicker = 1.0 - _Flicker + _Flicker * 2.0 * wave;

                half inside = step(r, _Front) * step(0.001, _Front);
                half front = Line(r - _Front, _FrontWidth) * step(0.001, _Front);
                half trail = exp(-max(_Front - r, 0.0) / _Trail) * inside;
                half limit = Line(r - _Radius, _LimitWidth);

                half3 glow = _HotColor.rgb * front * _FrontLight * flicker
                           + _Color.rgb * (front * _FrontLight * 0.5 + trail * _TrailLight * flicker + limit * _LimitLight);
                return half4(glow * surface * _Fade, 0.0);
            }
            ENDHLSL
        }
    }
}
