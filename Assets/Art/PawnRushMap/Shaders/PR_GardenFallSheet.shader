// R279 garden waterfall sheets (Blender build_v23.py: one continuous sheet per fall, curling over the lip and dropping
// on a ballistic path). UV0 = (metres across, metres along the flow), UV1 = (0..1 across, 0..1 from lip to landing).
// Glassy and see-through at the lip (the scene behind is refracted), streaked white as it falls, mostly white water
// and frayed edges at the bottom where it lands; the sheet ripples a little as it falls.
Shader "ChessFight/Garden Fall Sheet"
{
    Properties
    {
        _Tint ("Clear water tint", Color) = (0.78, 0.93, 1.0, 1)
        _FoamColor ("White water", Color) = (0.95, 0.98, 1.0, 1)
        _Speed ("Flow (m/s)", Float) = 3.2
        _Streaks ("Streaks", Range(0, 1)) = 0.55
        _Refraction ("Refraction", Range(0, 0.1)) = 0.02
        _Wobble ("Wobble (m)", Float) = 0.03
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
                half4 _Tint, _FoamColor;
                float _Speed, _Streaks, _Refraction, _Wobble;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv0:TEXCOORD0; float2 uv1:TEXCOORD1; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                float4 uv:TEXCOORD2;
                float fog:TEXCOORD3;
            };
            float FallHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float FallNoise(float2 p)
            {
                float2 a=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(FallHash(a),FallHash(a+float2(1,0)),f.x),lerp(FallHash(a+float2(0,1)),FallHash(a+1),f.x),f.y);
            }
            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 n=TransformObjectToWorldNormal(v.normalOS);
                float3 ws=TransformObjectToWorld(v.positionOS.xyz);
                // Ripples run down the sheet and grow as it falls.
                ws+=n*sin(v.uv0.x*2.3+v.uv0.y*1.7-_Time.y*_Speed*1.4)*_Wobble*(0.25+v.uv1.y);
                o.positionWS=ws; o.normalWS=n;
                o.positionCS=TransformWorldToHClip(ws);
                o.uv=float4(v.uv0,v.uv1);
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i, bool front:SV_IsFrontFace):SV_Target
            {
                float across=i.uv.x, down=i.uv.y-_Time.y*_Speed;      // features travel down the sheet
                float side=i.uv.z, fall=i.uv.w;
                float n1=FallNoise(float2(across*4.0,down*0.35));
                float n2=FallNoise(float2(across*11.0,down*0.9+7.0));
                float n3=FallNoise(float2(across*23.0,down*2.2+3.0));
                float streak=smoothstep(1-_Streaks,1,n1*0.6+n2*0.4);
                // White water grows from a third of the way down and dominates the landing.
                float white=saturate(smoothstep(0.25,1.0,fall)*(0.55+0.6*n2)+streak*0.8);
                half3 normal=normalize(i.normalWS)*(front?1:-1);
                float2 uv=GetNormalizedScreenSpaceUV(i.positionCS);
                half3 behind=SampleSceneColor(uv+(float2(n2,n3)-0.5)*_Refraction);
                Light sun=GetMainLight();
                half lit=0.8+0.2*saturate(dot(normal,sun.direction));
                half3 glass=behind*_Tint.rgb+sun.color*0.05;
                half3 V=normalize(GetWorldSpaceViewDir(i.positionWS));
                half spec=pow(saturate(dot(normal,normalize(sun.direction+V))),60)*(0.3+0.7*n3);
                // A bright roll where the water curls over the lip.
                half lipGlow=smoothstep(0.08,0.0,fall)*0.25;
                half3 c=lerp(glass,_FoamColor.rgb*lit*lerp(half3(1,1,1),sun.color,0.3),white)+sun.color*(spec*0.35+lipGlow);
                // Frayed side edges and a broken-up bottom.
                float fray=n3*0.12;
                half edge=smoothstep(0.0,0.07+fray,side)*smoothstep(1.0,0.93-fray,side);
                half foot=1-smoothstep(0.93,1.0,fall+n2*0.05);
                half alpha=edge*foot*lerp(0.9,1.0,white);
                return half4(MixFog(c,i.fog),alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
