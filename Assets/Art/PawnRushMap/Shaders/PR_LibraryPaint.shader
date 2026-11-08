// 승규 11-04 (the war room's concept art - cream limestone halls, teal painted metal and crates with worn brass corners,
// warm wood shelves and desks full of books, brass lanterns - "이제 맵 택스쳐를 입힐 건데 ... 완전 반짝이는 금색은 안
// 입힐 거야 깃발도 안할거고"): the war room's clay pieces (PawnRushLibraryEnvironment.fbx's "CLAY | uniform neutral
// gray") in the concept's materials. They carry no UVs, so every look is drawn in world space, its fine detail faded
// where a pixel holds more than one of it. One shader for eight kinds - 0 limestone, 1 wood, 2 paint, 3 worn brass
// (never a mirror: metallic .6, smoothness about .4), 4 dark iron, 5 paper, 6 glowing glass, 7 cloth or leather - taken
// from the material (_Kind, _BaseColor) or, on the props Course01v4Builder.LibraryDressing paints part by part
// (LibraryPaint.cs), from the vertex colours: RGB the part's colour (sRGB), A its kind (kind / 7). Lit by
// Assets/Shaders/URP/CFLit.hlsl.
Shader "ChessFight/Pawn Rush Library Paint"
{
    Properties
    {
        _BaseColor ("Colour (times the vertex colour)", Color) = (1, 1, 1, 1)
        _Kind ("Kind: 0 stone 1 wood 2 paint 3 brass 4 iron 5 paper 6 glow 7 cloth", Range(0, 7)) = 0
        _UseVertex ("Kind and colour from the vertex colours (0 / 1)", Range(0, 1)) = 0
        _Glow ("Glass glow", Range(0, 12)) = 4
        _StoneTex ("Stone picture (sRGB, mirrored)", 2D) = "white" {}
        _StoneSize ("Stone picture width, height (m)", Vector) = (0.8, 3.1, 0, 0)
        _StoneRef ("The colour the stone picture is (stone colours are taken against it)", Color) = (0.87, 0.81, 0.74, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor, _StoneRef;
        float4 _StoneTex_ST, _StoneSize;
        half _Kind, _UseVertex, _Glow;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Assets/Shaders/URP/CFLit.hlsl"

            // CFLit's vertex with the vertex colour carried on.
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.color = v.color;
                return o;
            }

            TEXTURE2D(_StoneTex); SAMPLER(sampler_StoneTex);

            float hash3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }

            float vnoise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(hash3(i), hash3(i + float3(1, 0, 0)), f.x), lerp(hash3(i + float3(0, 1, 0)), hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(hash3(i + float3(0, 0, 1)), hash3(i + float3(1, 0, 1)), f.x), lerp(hash3(i + float3(0, 1, 1)), hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float fbm(float3 p)
            {
                float s = 0, a = 0.5;
                [unroll] for (int k = 0; k < 4; k++) { s += a * vnoise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return s;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                CFLitVaryings l = (CFLitVaryings)0;
                l.positionCS = i.positionCS;
                l.positionWS = i.positionWS;
                l.normalWS = i.normalWS;
                l.fog = i.fog;
                float3 p = i.positionWS;
                float3 n = normalize(i.normalWS);
                float3 an = abs(n);

                int kind;
                half3 tint;
                // A prop not painted yet (the scene before Play: LibraryPaint paints in Awake) has no vertex colours.
                if (_UseVertex > 0.5 && any(i.color != 0))
                {
                    kind = (int)round(i.color.a * 7);
                    tint = SRGBToLinear(i.color.rgb);
                }
                else
                {
                    kind = (int)round(_Kind);
                    tint = half3(1, 1, 1);
                }
                half3 base = tint * _BaseColor.rgb;

                // Shared detail, worked out before the kinds branch (derivatives): broad clouds; a pixel's footprint in
                // metres, which fades any detail finer than it.
                float foot = length(fwidth(p));
                float fine = saturate(1.5 - foot * 9);       // detail at about 9 per metre
                float hair = saturate(1.5 - foot * 30);      // detail at about 30 per metre
                float cloud = fbm(p * 0.18);
                float grain = (vnoise(p * 9) - 0.5) * fine;
                // R390 ("지금 내가 딱 원하는 벽 기둥 택스쳐가 저 문 택스쳐야"): the stone is the door concept's own - its
                // left column's painted face (Resources/PawnRushLibrary/Door/LibraryStone.png, 0.8 x 3.1 m, mirrored
                // so it has no seam) wrapped round every face from the world plane it faces most. R391 ("계단 쪽 축이
                // 안맞아서 반짝 거려"): one plane per face, never a blend of three - on the stairs' slopes and nosings the
                // blend laid two crossing crack patterns in bands that swam as the view moved. The plane is chosen from
                // the face's own flat normal (screen derivatives), so a whole tread or riser takes one plane.
                float3 fc = cross(ddy(p), ddx(p));
                float3 fa = abs(fc);
                float2 suv = fa.y >= fa.x && fa.y >= fa.z ? p.xz : (fa.x >= fa.z ? p.zy : p.xy);
                half3 stone = SAMPLE_TEXTURE2D(_StoneTex, sampler_StoneTex, suv / _StoneSize.xy).rgb;

                half3 albedo = base;
                half metallic = 0, smoothness = 0.3;
                half3 glow = 0;
                if (kind == 0)
                {
                    // The picture's colour is the walls'; the floors, joints and marble pieces keep their own shade of it.
                    albedo = stone * base / max(_StoneRef.rgb, 0.01) * (0.96 + 0.08 * cloud);
                    smoothness = 0.22 + 0.06 * cloud;
                }
                else if (kind == 1)
                {
                    // Wood: grain along the plank - up on the uprights, along x on the level faces.
                    float along = an.y > 0.7 ? p.x : p.y;
                    float across = an.y > 0.7 ? p.z : (an.x > an.z ? p.z : p.x);
                    float streak = (vnoise(float3(along * 0.6, across * 30, 0.5)) - 0.5) * hair;
                    float ring = frac(across * 2.4 + fbm(float3(along * 0.3, across * 1.1, 7.3)) * 3.2);
                    ring = smoothstep(0.0, 0.25, ring) * smoothstep(1.0, 0.6, ring);
                    albedo = base * (0.84 + 0.2 * cloud) * lerp(0.82, 1.04, lerp(0.5, ring, fine)) * (1 + streak * 0.35);
                    smoothness = 0.32;
                }
                else if (kind == 2)
                {
                    // Paint on metal: a little uneven, darker where it has worn.
                    float wear = smoothstep(0.62, 0.78, fbm(p * 1.7 + 4.1)) * fine;
                    albedo = base * (0.9 + 0.16 * cloud) * (1 + grain * 0.05);
                    albedo = lerp(albedo, base * 0.55, wear * 0.5);
                    smoothness = 0.34 - 0.1 * wear;
                    metallic = 0.05;
                }
                else if (kind == 3)
                {
                    // Worn brass: browner where the patina sits, never polished to a mirror.
                    float patina = smoothstep(0.35, 0.75, fbm(p * 2.3 + 1.7));
                    albedo = lerp(base * (0.92 + 0.14 * cloud), base * half3(0.62, 0.52, 0.38), patina * 0.55);
                    metallic = 0.6;
                    smoothness = 0.44 - 0.12 * patina;
                }
                else if (kind == 4)
                {
                    // Dark iron.
                    albedo = base * (0.82 + 0.3 * cloud) * (1 + grain * 0.08);
                    metallic = 0.45;
                    smoothness = 0.3 + 0.08 * cloud;
                }
                else if (kind == 5)
                {
                    // Paper and parchment: fibres and a darker edge of age.
                    float age = smoothstep(0.45, 0.8, fbm(p * 3.1 + 9.2));
                    albedo = base * (0.95 + 0.08 * (vnoise(p * 30) - 0.5) * hair) * lerp(1, 0.85, age);
                    smoothness = 0.12;
                }
                else if (kind == 6)
                {
                    // Glass lit from inside.
                    albedo = base * 0.4;
                    glow = base * _Glow * (0.9 + 0.2 * cloud);
                    smoothness = 0.75;
                }
                else
                {
                    // Cloth and leather: soft, slightly mottled.
                    albedo = base * (0.86 + 0.24 * cloud) * (1 + grain * 0.1);
                    smoothness = 0.26;
                }
                half4 c = CFShade(l, albedo, metallic, smoothness, 1);
                c.rgb += glow;
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex CFShadowVertex
            #pragma fragment CFShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Assets/Shaders/URP/CFShadowCaster.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex CFDepthVertex
            #pragma fragment CFShadowFragment
            #pragma multi_compile_instancing
            #include "Assets/Shaders/URP/CFShadowCaster.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
