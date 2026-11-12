// R552 (승규 "원형 창문에서 회전을 하며 빛이 수학적으로 계산이 되며 빛은 인트로 씬에 있는 이런 노을 빛이 실시간으로
// 렌더 되게 해줘"): the sunset coming through the lobby hall's round window as shafts in the air, ray-marched
// every frame. Drawn on a box round the beam (LobbyStage.Hall.Sunset.cs), back faces only, so it also works with the
// camera inside the box. Per pixel the view ray is cut to the box and to the scene's depth, then stepped through.
// At each step the point is traced back towards the sun onto the window's plane and looked up in the window's
// pattern, turned by _WinShape.z like the Rose_Tracery mesh. The penumbra widens with the distance from the window
// because the sun is a disc (a blurrier mip). The sun's shadow map then says whether anything (the balcony, a pawn)
// stands between the point and the sun. Henyey-Greenstein forward scattering: brightest looking into the sun.
// R563 (승규 "회전 창문이 좀 밋밋해 빛 그래픽도 별로고", "플레이어가 압도 할만한 느낌 ... 빛 그래픽도 이거보다 좋게"): the
// great rose. Its pattern comes from _RosePattern (Kit/rose_pattern.py renders it from the tracery and glass meshes:
// RGB the colour of each pane, A how much light gets through), so the light takes each pane's colour. Drifting dust
// (two octaves of value noise) gives the beams texture, and where the beam meets the floor, the stage or a pawn
// the window's coloured image is laid on it (the surface's normal from the depth, lit like the sun lights it).
Shader "ChessFight/Rose Window Shafts"
{
    Properties
    {
        _SunColor ("Sun colour", Color) = (1, 0.61, 0.35, 1)
        _Density ("Density", Float) = 0.02
        _G ("Forward scattering g", Range(0, 0.95)) = 0.55
        _Steps ("Steps", Range(8, 96)) = 64
        _Tint ("How much the glass colours the light", Range(0, 1)) = 0.75
        _Dust ("Dust contrast", Range(0, 1)) = 0.6
        _Surface ("Coloured light on surfaces", Float) = 1
        [NoScaleOffset] _RosePattern ("Window pattern (RGB pane colour, A light through)", 2D) = "black" {}
        [HideInInspector] _WinCentre ("Window centre (world)", Vector) = (0, 7.8, 15.5, 0)
        [HideInInspector] _WinRight ("Window right (world)", Vector) = (1, 0, 0, 0)
        [HideInInspector] _WinUp ("Window up (world)", Vector) = (0, 1, 0, 0)
        [HideInInspector] _WinNormal ("Window normal into the hall (world)", Vector) = (0, 0, -1, 0)
        [HideInInspector] _SunDir ("Way the light travels (world)", Vector) = (0, -0.48, -0.87, 0)
        [HideInInspector] _WinShape ("Pattern half size (m), pattern pixels, turn (rad), sun spread", Vector) = (2.776, 1024, 0, 0.0094)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+50" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Shafts"
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"

            TEXTURE2D(_RosePattern);
            SAMPLER(sampler_RosePattern);

            CBUFFER_START(UnityPerMaterial)
                float4 _SunColor;
                float _Density, _G, _Steps, _Tint, _Dust, _Surface;
                float4 _WinCentre, _WinRight, _WinUp, _WinNormal, _SunDir, _WinShape;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            // What a point in the hall gets of the sun through the window: black where the stone stands in the way,
            // otherwise the colour of the pane it looks through.
            float3 WindowLight(float3 p)
            {
                float3 rel = p - _WinCentre.xyz;
                float ahead = dot(rel, _WinNormal.xyz);              // how far into the hall
                float facing = dot(_SunDir.xyz, _WinNormal.xyz);
                if (ahead <= 0 || facing <= 1e-3) return 0;
                float k = ahead / facing;                            // back along the light to the window's plane
                float3 q = rel - _SunDir.xyz * k;
                float2 w = float2(dot(q, _WinRight.xyz), dot(q, _WinUp.xyz));
                float hs = _WinShape.x;
                if (dot(w, w) > hs * hs) return 0;
                float c = cos(_WinShape.z), s = sin(_WinShape.z);
                w = float2(c * w.x + s * w.y, -s * w.x + c * w.y);    // into the pattern's own turn
                float2 uv = w / (2 * hs) + 0.5;
                float blur = (0.004 + k * _WinShape.w) * _WinShape.y / (2 * hs);   // the sun's disc, in pixels
                float4 t = SAMPLE_TEXTURE2D_LOD(_RosePattern, sampler_RosePattern, uv, log2(max(blur, 1)));
                float3 pane = t.rgb / max(t.a, 1e-3);
                return lerp(_SunColor.rgb, pane, _Tint) * t.a;
            }

            // Dust in the air: slow drifting value noise, about 1 on average.
            float RoseHash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1) * 17;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float RoseNoise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(RoseHash(i), RoseHash(i + float3(1, 0, 0)), f.x), lerp(RoseHash(i + float3(0, 1, 0)), RoseHash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(RoseHash(i + float3(0, 0, 1)), RoseHash(i + float3(1, 0, 1)), f.x), lerp(RoseHash(i + float3(0, 1, 1)), RoseHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Dust(float3 p)
            {
                float3 drift = float3(0.04, 0.09, 0.03) * _Time.y;
                float n = 0.65 * RoseNoise(p * 1.2 + drift) + 0.35 * RoseNoise(p * 3.4 - drift * 1.8);
                return lerp(1, 0.25 + 1.5 * n, _Dust);
            }

            float SunVisible(float3 p)
            {
            #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                float4 c = TransformWorldToShadowCoord(p);
                if (BEYOND_SHADOW_FAR(c)) return 1;
                return SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_LinearClampCompare, c.xyz);
            #else
                return 1;
            #endif
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 cam = GetCameraPositionWS();
                float3 dir = normalize(i.positionWS - cam);
                // the view ray inside the box: slabs in object space, the parameter stays the world distance
                float4x4 w2o = GetWorldToObjectMatrix();
                float3 o = mul(w2o, float4(cam, 1)).xyz;
                float3 d = mul((float3x3)w2o, dir);
                float3 inv = 1.0 / d;
                float3 ta = (-0.5 - o) * inv, tb = (0.5 - o) * inv;
                float3 lo = min(ta, tb), hi = max(ta, tb);
                float tin = max(max(lo.x, lo.y), max(lo.z, 0));
                float tout = min(min(hi.x, hi.y), hi.z);
                // stop at whatever the camera sees there
                float raw = SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS));
                float3 fwd = -UNITY_MATRIX_V[2].xyz;
                float hit = LinearEyeDepth(raw, _ZBufferParams) / max(dot(dir, fwd), 1e-4);
                // the surface the camera sees here and its normal (before any branch: derivatives need the quad)
                float3 surf = cam + dir * hit;
                float3 nrm = normalize(cross(ddy(surf), ddx(surf)));
                if (dot(nrm, dir) > 0) nrm = -nrm;
                bool onSurface = hit < tout;
                tout = min(tout, hit);
                if (tout <= tin) return 0;

                int n = (int)_Steps;
                float stepLen = (tout - tin) / n;
                float jitter = InterleavedGradientNoise(i.positionCS.xy, 0);
                float3 sum = 0;
                [loop] for (int s = 0; s < 96; s++)
                {
                    if (s >= n) break;
                    float3 p = cam + dir * (tin + (s + jitter) * stepLen);
                    float3 m = WindowLight(p);
                    if (max(m.r, max(m.g, m.b)) > 0.001) sum += m * (SunVisible(p) * Dust(p));
                }
                float cosT = dot(_SunDir.xyz, -dir);
                float g = _G;
                float hg = (1 - g * g) / pow(abs(1 + g * g - 2 * g * cosT), 1.5);   // x 4 pi: an even spread would be 1
                float3 col = sum * (stepLen * _Density * hg);
                if (onSurface)
                {
                    float3 lit = WindowLight(surf - nrm * 0.02);
                    float nl = saturate(dot(nrm, -_SunDir.xyz));
                    if (nl > 0 && max(lit.r, max(lit.g, lit.b)) > 0.001)
                        col += lit * (nl * SunVisible(surf + nrm * 0.05) * _Surface);
                }
                return half4(col, 0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
