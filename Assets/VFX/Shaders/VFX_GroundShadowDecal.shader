// The shadow a falling piece throws straight down (R341, 11-04, W2 of the grand stair hall's falling knights): a soft dark
// blob on the floor right under it, wide and faint while it hangs high, small and dark as it comes down, so where and
// when it lands reads from any angle (the sun's own shadow of it lies off to one side). A projected decal like
// VFX_GroundWarnDecal (VFX_DecalProjection.hlsl: only floor facing up inside the box), drawn premultiplied just before
// that warning so the warning's light lies over it. _Radius, _Darkness and _Box (the box's side, m) are set per renderer
// by VfxDropShadow.
Shader "ChessFight/VFX/Ground Shadow Decal"
{
    Properties
    {
        _Color ("Shadow color", Color) = (0.05, 0.02, 0.01, 1)
        _Core ("Dark core (share of the radius)", Range(0, 0.9)) = 0.35
        _Upward ("Floor facing up past (cos)", Range(0, 1)) = 0.6
        _EdgeFade ("Fade at the box's top and bottom (share)", Range(0.01, 0.5)) = 0.2
        _Radius ("Radius (m)", Float) = 1.5
        _Darkness ("Darkness", Range(0, 1)) = 0.5
        _Box ("Box side (m)", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-11" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Decal"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
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
                half _Core;
                half _Upward;
                half _EdgeFade;
                float _Radius;
                half _Darkness;
                float _Box;
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

            half4 frag(Varyings input) : SV_Target
            {
                half surface;
                float3 local = DecalLocal(input.positionCS, _Upward, _EdgeFade, surface);
                float r = length(local.xz * _Box);
                // Even through the core, then dying away smoothly to nothing at the radius.
                half k = saturate((r / max(_Radius, 0.01) - _Core) / (1.0 - _Core));
                half a = _Darkness * (1.0 - k * k) * (1.0 - k * k) * surface;
                return half4(_Color.rgb * a, a);
            }
            ENDHLSL
        }
    }
}
