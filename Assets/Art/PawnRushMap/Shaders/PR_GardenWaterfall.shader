// World-space flow for the garden FBX, whose merged waterfall meshes have no UVs.
// R278: clear falling water. The scene behind each sheet is refracted (URP opaque texture) and lightly tinted, with
// long white streaks running down and a sun highlight, instead of the earlier flat, semi-opaque teal.
Shader "ChessFight/Garden Waterfall"
{
    Properties
    {
        _WaterColor ("Water tint (a = tint strength)", Color) = (0.72, 0.95, 0.97, 0.35)
        _FoamColor ("Streaks", Color) = (0.92, 0.98, 1, 1)
        _Speed ("Flow metres per second", Float) = 2.6
        _Streaks ("Streak amount", Range(0, 1)) = 0.5
        _Refraction ("Refraction", Range(0, 0.1)) = 0.025
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor, _FoamColor;
                float _Speed, _Streaks, _Refraction;
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
                // A feature at y + speed*t stays fixed, so the texture travels downwards; streaks are long and thin.
                float down=i.positionWS.y+_Time.y*_Speed;
                float across=i.positionWS.x+i.positionWS.z;
                float n=GardenNoise(float2(across*6.0,down*.45));
                float fine=GardenNoise(float2(across*15.0,down*1.1+12.0));
                half streak=smoothstep(1-_Streaks,1,n*.65+fine*.35);
                half3 normal=normalize(i.normalWS)*(front?1:-1);
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                half3 behind=SampleSceneColor(uv+(float2(n,fine)-.5)*_Refraction);
                Light sun=GetMainLight();
                half lit=.8+.2*saturate(dot(normal,sun.direction));
                half3 water=lerp(behind,behind*_WaterColor.rgb,_WaterColor.a)+sun.color*.06;
                half3 c=lerp(water,_FoamColor.rgb*lit,streak*_FoamColor.a);
                half3 V=normalize(GetWorldSpaceViewDir(i.positionWS));
                half spec=pow(saturate(dot(normal,normalize(sun.direction+V))),48)*.35*(.4+.6*fine);
                c+=sun.color*spec;
                // Mostly opaque: the refracted background already shows through, alpha only softens sheet edges.
                return half4(MixFog(c,i.fog),.92);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
