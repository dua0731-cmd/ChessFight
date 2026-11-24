// JY-gfx (2026-11-24): a spot light's beam seen in the haze (JY on the observatory: "이때처럼 안개 효과에 빛줄기 효과가 있으면
// 훨씬 더 멋있을거같아"). A cone from the light (uv.y 0) to where its pool lands (uv.y 1), drawn added over the scene: brightest
// near the light, fading toward its end and its edges (seen side-on), where it meets the floor or walls (the depth
// texture) and right in front of the camera. GfxMapLights builds one for a plan line with Beam over 0.
Shader "ChessFight/Gfx Beam"
{
    Properties
    {
        [HDR] _Color ("Colour", Color) = (1, 1, 1, 1)
        _Strength ("Strength", Float) = 0.3
        _Soft ("Soft meeting (m)", Float) = 1.5
        _Near ("Fade near the camera (m)", Float) = 3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Beam"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Strength;
                half _Soft;
                half _Near;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewWS : TEXCOORD2;
                float4 screen : TEXCOORD3;
                half fog : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.uv = input.uv;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.viewWS = GetWorldSpaceViewDir(p.positionWS);
                o.screen = ComputeScreenPos(p.positionCS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // Along the cone: strongest near the light.
                half along = saturate(1.0h - i.uv.y);
                along = along * along * (3.0h - 2.0h * along) * 0.85h + 0.15h * along;
                // Across: a beam seen through its middle is thicker than at its rim.
                half facing = abs(dot(normalize(i.normalWS), normalize(i.viewWS)));
                half across = facing * facing;
                // Where it meets the scene, and right in front of the camera.
                float2 suv = i.screen.xy / i.screen.w;
                float scene = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float here = i.screen.w;
                half meet = saturate((scene - here) / max(_Soft, 0.01h));
                half near = saturate((here - 0.3h) / max(_Near, 0.01h));
                half3 colour = _Color.rgb * (_Strength * along * across * meet * near);
                // The haze dims it with distance like the rest of the scene (towards black: the beam is added light).
                colour *= ComputeFogIntensity(i.fog);
                return half4(colour, 0);
            }
            ENDHLSL
        }
    }
}
