// JY-gfx (2026-11-24, the graphics upgrade): the game's sky. A 360 degree picture of real, path-traced volumetric
// cumulus over a drawn storybook blue (ArtSource/Gfx/sky.py renders it once, lit by the round's own sun from the
// same direction, so the bright side of every cloud faces the scene's sun). Read with Blender's environment mapping
// (Blender's frame: x right, y away, z up; Unity's (x, y, z) is Blender's (x, z, y)). ChessFight.Game.GameSky puts
// it in every game scene that still has Unity's default sky; the scene's ambient and reflections then come from it.
Shader "ChessFight/Gfx Sky"
{
    Properties
    {
        [NoScaleOffset] _Panorama ("Sky (equirectangular, Blender's frame)", 2D) = "black" {}
        _Exposure ("Brightness", Float) = 1
        _Turn ("Turn about the vertical (degrees)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Panorama); SAMPLER(sampler_Panorama);
            CBUFFER_START(UnityPerMaterial)
            half _Exposure, _Turn;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.direction = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float s, c;
                sincos(radians(_Turn), s, c);
                d.xz = float2(d.x * c - d.z * s, d.x * s + d.z * c);
                // Unity (x, y, z) is Blender (x, z, y); then Blender's environment mapping.
                float3 b = float3(d.x, d.z, d.y);
                float2 uv = float2(0.5 - atan2(b.y, b.x) / (2.0 * PI), atan2(b.z, length(b.xy)) / PI + 0.5);
                half3 col = SAMPLE_TEXTURE2D_LOD(_Panorama, sampler_Panorama, uv, 0).rgb;
                return half4(col * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
