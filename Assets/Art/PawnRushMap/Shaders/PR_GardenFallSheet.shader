// Garden waterfalls (R279 sheets; R281: a thin curtain plus round streams, build_v25.py). Vertex colour carries the
// fall data so nothing depends on a second UV channel: r = across the lip 0..1, g = lip -> landing 0..1, b = a seed
// per stream, a = 1 for a stream, 0 for the curtain. UV0 = (metres across or round, metres along the flow).
// The water is clear at the lip (the scene behind is refracted) and takes in air as it falls, so streaks of white
// run down it; the curtain thins into strands while the streams stay whole and catch the light at their edges.
// R283 (승규 11-02: "where the fall meets the water it looks unnatural"): drawn after the water, so the water's depth
// hides what is under the surface; over the last half metre above the moat (_GardenFoot from
// GardenWaterInteraction) the water spreads, slows its streaks, takes the pool's turquoise and thins out unevenly.
Shader "ChessFight/Garden Fall Sheet"
{
    Properties
    {
        _Tint ("Clear water tint", Color) = (0.80, 0.95, 1.0, 1)
        _FoamColor ("Aerated water", Color) = (0.92, 0.97, 1.0, 1)
        _Speed ("Flow (m/s)", Float) = 3.4
        _Refraction ("Refraction", Range(0, 0.1)) = 0.03
        _Wobble ("Wobble (m)", Float) = 0.025
        _Aeration ("Aeration", Range(0, 1)) = 0.6
        _PoolTint ("Pool water at the foot", Color) = (0.66, 0.86, 1.0, 1)
        _FootHeight ("Foot blend height (m)", Float) = 0.55
        _FallHeight ("Own drop for the foot (m; 0 = the moat height _GardenFoot)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" }
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
                half4 _Tint, _FoamColor, _PoolTint;
                float _Speed, _Refraction, _Wobble, _Aeration, _FootHeight, _FallHeight;
            CBUFFER_END
            float4 _GardenFoot;   // x: world height of the moat surface, w: 1 when set (GardenWaterInteraction)
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv0:TEXCOORD0; float4 color:COLOR; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                float2 uv:TEXCOORD2;
                float4 data:TEXCOORD3;
                float fog:TEXCOORD4;
            };
            float FallHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            // R440: a fall given its own drop (_FallHeight, the garden's tiered falls) finds its foot from its data - g, 0 at
            // the brink and 1 at the pool, times the drop - so it works in whichever pool it lands and wherever the zone is
            // placed (PITFALLS 36); the others use the moat height _GardenFoot as before.
            float FootAbove(float wsY, float fall) { return _FallHeight > 0 ? (1 - fall) * _FallHeight : wsY - _GardenFoot.x; }
            float FootOn() { return _FallHeight > 0 ? 1 : _GardenFoot.w; }
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
                // Ripples run down the water and grow as it falls.
                ws+=n*sin(v.uv0.x*2.1+v.uv0.y*1.9-_Time.y*_Speed*1.3+v.color.b*6.2831853)*_Wobble*(0.2+v.color.g);
                // At the foot the water breaks up and pushes outward a little as it hits the pool.
                float low=FootOn()*saturate(1-FootAbove(ws.y,v.color.g)/max(_FootHeight,0.05));
                ws+=n*low*low*0.12*sin(v.uv0.x*5.3-_Time.y*7.0+v.color.b*11.0);
                o.positionWS=ws; o.normalWS=n;
                o.positionCS=TransformWorldToHClip(ws);
                o.uv=v.uv0; o.data=v.color;
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i, bool front:SV_IsFrontFace):SV_Target
            {
                float side=i.data.r, fall=i.data.g, seed=i.data.b, jet=i.data.a;
                float flow=i.uv.y-_Time.y*_Speed*(0.85+0.3*seed);            // features travel down
                float n1=FallNoise(float2(i.uv.x*3.5+seed*17.0,flow*0.45));
                float n2=FallNoise(float2(i.uv.x*9.0+seed*31.0,flow*1.2));
                float n3=FallNoise(float2(i.uv.x*23.0+seed*7.0,flow*3.1));
                // Air is drawn in as the water falls: clear at the lip, streaked white lower down.
                float air=saturate(fall*1.1-0.12+(n1-0.5)*0.6)*_Aeration;
                float streak=smoothstep(0.5,0.9,n2*0.6+n3*0.4);
                float white=saturate(air*(0.45+0.7*streak));
                // The curtain thins into strands as it falls; the streams stay whole.
                float strands=lerp(1.0,smoothstep(0.38,0.62,n1*0.65+n2*0.35),(1-jet)*smoothstep(0.1,0.55,fall));
                half3 normal=normalize(i.normalWS)*(front?1:-1);
                float2 suv=GetNormalizedScreenSpaceUV(i.positionCS);
                half3 behind=SampleSceneColor(suv+(float2(n2,n3)-0.5)*_Refraction);
                Light sun=GetMainLight();
                half3 V=normalize(GetWorldSpaceViewDir(i.positionWS));
                half fres=pow(1-saturate(abs(dot(normal,V))),3);
                half spec=pow(saturate(dot(normal,normalize(sun.direction+V))),80)*(0.4+0.6*n3);
                half3 glass=behind*_Tint.rgb+_Tint.rgb*fres*0.35;
                half3 aerated=_FoamColor.rgb*(0.78+0.22*saturate(dot(normal,sun.direction)));
                half3 c=lerp(glass,aerated,white)+sun.color*spec*0.6;
                // Near the pool the water takes its colour and thins out unevenly into it, instead of ending on a line.
                float above=FootAbove(i.positionWS.y,fall)/max(_FootHeight,0.05);
                half footOn=FootOn();
                half pool=footOn*saturate(1-above);
                c=lerp(c,behind*_PoolTint.rgb+_PoolTint.rgb*0.12,pool*0.7);
                half sink=lerp(1.0,smoothstep(0.0,0.35+0.5*n2,above+(n1-0.5)*0.3),footOn);
                // A veil of a curtain, fuller streams; frayed curtain sides and a soft foot.
                half edge=lerp(smoothstep(0.0,0.06+n3*0.05,side)*smoothstep(1.0,0.94-n3*0.05,side),1.0,jet);
                half foot=(1-smoothstep(0.94,1.0,fall+(n2-0.5)*0.04))*sink;
                half body=lerp(0.35,0.92,jet);
                half alpha=saturate(body*strands*edge*foot*lerp(0.8,1.0,white)+fres*jet*0.1);
                return half4(MixFog(c,i.fog),alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
