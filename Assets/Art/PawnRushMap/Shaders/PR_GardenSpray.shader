// R279 garden waterfall spray: two crossed bands over each landing line (Blender build_v23.py), UV0 = (0..1 along the
// line, 0..1 from the water up). Soft white mist that billows up and thins out with height, frayed at the ends.
Shader "ChessFight/Garden Spray"
{
    Properties
    {
        _Color ("Mist", Color) = (0.9, 0.97, 1.0, 0.3)
        _Rise ("Rise (per second)", Float) = 0.45
        _Puffs ("Puff size", Float) = 5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+10" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Rise, _Puffs;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 positionWS:TEXCOORD1; float fog:TEXCOORD2; };
            float SprayHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float SprayNoise(float2 p)
            {
                float2 a=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(SprayHash(a),SprayHash(a+float2(1,0)),f.x),lerp(SprayHash(a+float2(0,1)),SprayHash(a+1),f.x),f.y);
            }
            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS; o.uv=v.uv;
                o.fog=ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float t=_Time.y*_Rise;
                // Puffs rise (features move up), spread with height and fade out.
                float2 q=float2(i.uv.x*_Puffs*(1.0-0.35*i.uv.y)+i.positionWS.x*0.13, i.uv.y*2.2-t);
                float puff=SprayNoise(q)*0.65+SprayNoise(q*2.3+5.1-t*0.7)*0.35;
                float fade=pow(saturate(1-i.uv.y),1.6)*smoothstep(0.0,0.08,i.uv.y);
                float ends=smoothstep(0.0,0.12,i.uv.x)*smoothstep(1.0,0.88,i.uv.x);
                float a=saturate((puff-0.35)*1.8)*fade*ends*_Color.a;
                Light sun=GetMainLight();
                half3 c=_Color.rgb*(0.85+0.15*sun.color);
                return half4(MixFog(c,i.fog),a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
