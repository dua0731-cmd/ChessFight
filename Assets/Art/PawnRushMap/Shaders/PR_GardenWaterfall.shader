// World-space flow for the garden FBX, whose merged waterfall meshes have no UVs.
Shader "ChessFight/Garden Waterfall"
{
    Properties
    {
        _WaterColor ("Water", Color) = (0.16, 0.48, 0.53, 0.7)
        _FoamColor ("Foam", Color) = (0.8, 0.94, 0.95, 0.95)
        _Speed ("Flow metres per second", Float) = 1.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
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
                half4 _WaterColor, _FoamColor;
                float _Speed;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                float fog:TEXCOORD2;
            };
            float GardenHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float GardenNoise(float2 p)
            {
                float2 a=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(GardenHash(a),GardenHash(a+float2(1,0)),f.x),lerp(GardenHash(a+float2(0,1)),GardenHash(a+1),f.x),f.y);
            }
            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.fog=ComputeFogFactor(p.positionCS.z); return o;
            }
            half4 frag(Varyings i, bool front:SV_IsFrontFace):SV_Target
            {
                // A feature at y + speed*t stays fixed, so the texture travels downwards.
                float down=i.positionWS.y+_Time.y*_Speed;
                float across=i.positionWS.x+i.positionWS.z;
                float n=GardenNoise(float2(across*3.7,down*.7));
                float fine=GardenNoise(float2(across*9.2,down*1.4+12.0));
                half foam=smoothstep(.3,.85,n*.7+fine*.3);
                Light sun=GetMainLight();
                half3 normal=normalize(i.normalWS)*(front?1:-1);
                half lit=.72+.28*saturate(dot(normal,sun.direction));
                half4 c=lerp(_WaterColor,_FoamColor,foam);
                c.rgb=MixFog(c.rgb*lit*lerp(half3(1,1,1),sun.color,.4),i.fog);
                return c;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
