// R552: the lobby hall's sky, the intro's sunset (Resources/Intro/SunsetSky.png, rendered in Blender with real
// volumetric clouds) laid round the hall as a skybox. The picture is placed by angle so its sun stands where the
// hall's sun light comes from (LobbyStage.Sunset.cs: azimuth from +Z towards +X, elevation), mirrored round
// the horizon beyond its edges, and blends into a dusk colour overhead. Seen through the glass vault and the
// round window.
Shader "ChessFight/Hall Sunset Sky"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Sunset sky", 2D) = "white" {}
        _SunUV ("Sun in the picture (u, v)", Vector) = (0.70, 0.535, 0, 0)
        _SunAngles ("Sun azimuth, elevation (degrees)", Vector) = (6, 22, 0, 0)
        _TileWidth ("Picture width (degrees)", Float) = 110
        _Zenith ("Colour overhead", Color) = (0.45, 0.46, 0.8, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _SunUV, _SunAngles, _Zenith;
                float _TileWidth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float el = degrees(asin(clamp(d.y, -1, 1)));
                float az = degrees(atan2(d.x, d.z));
                float u = _SunUV.x + (az - _SunAngles.x) / _TileWidth;
                float v = _SunUV.y + (el - _SunAngles.y) / (_TileWidth / 3);   // the picture is 3:1
                float t = frac(u * 0.5) * 2;
                u = t < 1 ? t : 2 - t;                                           // mirrored beyond its edges
                float3 c = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, float2(u, clamp(v, 0.003, 0.997)), 0).rgb;
                c = lerp(c, _Zenith.rgb, saturate((v - 1) * 1.2));
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
