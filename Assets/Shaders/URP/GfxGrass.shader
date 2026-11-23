// JY-gfx (2026-11-24, the graphics upgrade, target Mario Kart World): grass blades over a lawn. ChessFight.Game.GameGrass
// grows them in Play on the lawns' own surfaces: each blade a thin bent strip, uv.y its height (0 root, 1 tip),
// colour r a random per blade, colour g its height in half metres. Lit like the lawn under it (the normal is the
// ground's, so a lawn of blades reads as one soft surface) with the course's lighting (CFLit.hlsl), roots in their
// own shade, tips lighter, swaying in a light wind; blades far off sink into the lawn so only the near ones are drawn
// tall. No shadows cast (they would cost more than they show), shadows received.
Shader "ChessFight/Gfx Grass"
{
    Properties
    {
        _Base("Root colour", Color) = (0.25, 0.35, 0.16, 1)
        _Tip("Tip colour", Color) = (0.58, 0.78, 0.33, 1)
        _Wind("Wind", Float) = 1
        _Fade("Sink from, to (m)", Vector) = (40, 65, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #define CF_SURFACE_NONE
        #include "Assets/Shaders/URP/CFLit.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Base;
            half4 _Tip;
            float _Wind;
            float4 _Fade;
        CBUFFER_END

        struct GrassAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct GrassVaryings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            float fog : TEXCOORD3;
            half4 color : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        GrassVaryings GrassVertex(GrassAttributes v)
        {
            GrassVaryings o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_TRANSFER_INSTANCE_ID(v, o);
            float3 p = TransformObjectToWorld(v.positionOS.xyz);
            float h = v.uv.y;
            float metres = v.color.g * 0.5;
            // A light wind: two waves across the lawn, each blade a little out of step.
            float t = _Time.y;
            float wave = sin(t * 1.6 + p.x * 0.35 + p.z * 0.22) * 0.6 + sin(t * 2.9 + p.x * 0.9 - p.z * 0.7 + v.color.r * 6.283) * 0.4;
            p.xz += float2(0.82, 0.57) * wave * 0.25 * metres * _Wind * h * h;
            // Far blades sink into the lawn.
            float d = distance(p, _WorldSpaceCameraPos.xyz);
            float sink = saturate((d - _Fade.x) / max(_Fade.y - _Fade.x, 1e-3));
            p.y -= h * metres * sink;
            o.positionWS = p;
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.positionCS = TransformWorldToHClip(p);
            o.uv = v.uv;
            o.fog = ComputeFogFactor(o.positionCS.z);
            o.color = v.color;
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GrassVertex
            #pragma fragment GrassFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            half4 GrassFragment(GrassVaryings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half h = saturate(i.uv.y);
                half3 albedo = lerp(_Base.rgb, _Tip.rgb, h * h * (3.0 - 2.0 * h)) * (0.82 + 0.36 * i.color.r);
                // The roots stand in the blades' own shade.
                albedo *= lerp(0.5, 1.0, saturate(h * 1.5));
                CFLitVaryings l = (CFLitVaryings)0;
                l.positionCS = i.positionCS;
                l.positionWS = i.positionWS;
                l.normalWS = i.normalWS;
                l.uv = i.uv;
                l.fog = i.fog;
                return CFShadeSet(l, albedo, 0.0, 0.15, 1.0, 0, 0.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GrassVertex
            #pragma fragment GrassDepth
            #pragma multi_compile_instancing
            half GrassDepth(GrassVaryings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GrassVertex
            #pragma fragment GrassNormals
            #pragma multi_compile_instancing
            half4 GrassNormals(GrassVaryings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0.0); }
            ENDHLSL
        }
    }
}
