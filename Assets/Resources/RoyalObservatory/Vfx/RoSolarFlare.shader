// R725 ro-vfx (user 11-20 "대천구 태양은 신경 많이 써서 플레이어가 들어갈 수 있는 그림자 효과 등등 신경을 많이 써야 돼"):
// the great globe's solar flare on its floor, a decal over the floor disc (the box's far faces drawn with ZTest Always; for
// each pixel the camera's depth texture gives the point it saw, as VFX_DecalProjection.hlsl does, sampled at mip 0 so it
// can be done inside a branch). Every surface in the box is painted, not only the floor: a pawn standing in a shadow is
// in it. In SolarFlare's frame (P, metres: the floor y 0, the lamp _Sun over its middle):
//   shadows  a point is in a planet's shadow when the line from it to the lamp's middle passes through the planet
//            (_Planets: middle, radius) - the real shadow from a point light, an ellipse on the floor stretched away
//            from under the lamp, the same test SolarFlare.ShelteredBy makes (without its grace, so the drawn edge is
//            never wider than the shelter). Dark (premultiplied) toward _ShadowTint, _ShadeCalm when calm and
//            _ShadeFlare in the warning and the burn (_Alert), a pawn in it _BodyShade of that; its edge on the floor
//            a sharp cool line, brightest in the warning and the burn
//   warning  heat creeping out over the lit floor from under the lamp (_Front, m) with a hot band at its front, and
//            pawns out of the shadows warming on their lamp side (_Warn)
//   burn     the lit floor blazes (_Burn), a ripple of light runs out over it (_Ripple, m), exposed pawns glow hot on
//            their lamp side, and the air over the lit floor shimmers (_CameraOpaqueTexture seen through a wobble)
// Every value but the look is set per renderer by RoyalObservatoryVfxFlare.
Shader "ChessFight/VFX/RO Solar Flare Decal"
{
    Properties
    {
        [HDR] _ShadowTint ("Shadow colour", Color) = (0.012, 0.018, 0.06, 1)
        _ShadeCalm ("Shadow darkness, calm", Range(0, 1)) = 0.5
        _ShadeFlare ("Shadow darkness, warning and burn", Range(0, 1)) = 0.82
        _BodyShade ("Share of it on pawns", Range(0, 1)) = 0.8
        [HDR] _RimColor ("Shadow edge (HDR)", Color) = (0.55, 0.72, 1, 1)
        _RimLight ("Shadow edge light", Float) = 1.4
        _RimWidth ("Shadow edge width (m)", Range(0.01, 0.3)) = 0.05
        [HDR] _HeatColor ("Heat (HDR)", Color) = (1, 0.58, 0.2, 1)
        [HDR] _BlazeColor ("Blaze (HDR)", Color) = (1, 0.88, 0.62, 1)
        _HeatLight ("Warning heat light", Float) = 0.45
        _FrontLight ("Warning front light", Float) = 1.8
        _BlazeLight ("Burn blaze light", Float) = 1.3
        _RippleLight ("Burn ripple light", Float) = 4
        _BodyGlow ("Glow on exposed pawns", Float) = 2.2
        _Shimmer ("Heat shimmer (screen share)", Range(0, 0.02)) = 0.006
        _FloorTop ("Floor surfaces lie below (m)", Float) = 0.14
        _Reach ("Floor radius (m)", Float) = 13.9
        _Box ("Box size (m)", Vector) = (29, 2.8, 29, 0)
        _BoxCentre ("Box middle (m)", Vector) = (0, 0.8, 0, 0)
        _Sun ("Lamp (m, floor frame)", Vector) = (0, 8, 0, 0)
        _SunWorld ("Lamp (world)", Vector) = (0, 0, 0, 0)
        _FloorUp ("Floor up (world)", Vector) = (0, 1, 0, 0)
        _PlanetCount ("Planets", Float) = 0
        _Alert ("Alert", Range(0, 1)) = 0
        _Warn ("Warning", Range(0, 1)) = 0
        _Burn ("Burn", Range(0, 1)) = 0
        _Front ("Front (m)", Float) = 0
        _Ripple ("Ripple (m)", Float) = 0
        _RippleFade ("Ripple fade", Range(0, 1)) = 0
        _Age ("Age (s)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-9" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Decal"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowTint;
                half _ShadeCalm;
                half _ShadeFlare;
                half _BodyShade;
                half4 _RimColor;
                half _RimLight;
                float _RimWidth;
                half4 _HeatColor;
                half4 _BlazeColor;
                half _HeatLight;
                half _FrontLight;
                half _BlazeLight;
                half _RippleLight;
                half _BodyGlow;
                float _Shimmer;
                float _FloorTop;
                float _Reach;
                float4 _Box;
                float4 _BoxCentre;
                float4 _Sun;
                float4 _SunWorld;
                float4 _FloorUp;
                float _PlanetCount;
                half _Alert;
                half _Warn;
                half _Burn;
                float _Front;
                float _Ripple;
                half _RippleFade;
                float _Age;
            CBUFFER_END
            // The planets: middle (m, SolarFlare's frame) and radius (0: none). Set per renderer every frame.
            float4 _Planets[8];

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            // The world point the camera's depth texture saw at a screen uv (mip 0: allowed in a branch).
            float3 SceneAt(float2 uv)
            {
                float raw = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, UnityStereoTransformScreenSpaceTex(uv), 0).r;
            #if UNITY_REVERSED_Z
                float depth = raw;
            #else
                float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, raw);
            #endif
                return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p), u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float3 world = SceneAt(uv);
                float3 local = TransformWorldToObject(world);
                clip(0.5 - abs(local));
                float3 P = local * _Box.xyz + _BoxCentre.xyz;   // metres in SolarFlare's frame
                float rFloor = length(P.xz);
                half onFloor = 1.0 - smoothstep(_Reach, _Reach + 0.3, rFloor);
                clip(onFloor - 0.001);
                half boxFade = (1.0 - smoothstep(0.4, 0.5, local.y)) * smoothstep(-0.5, -0.4, local.y);
                half floorMask = 1.0 - smoothstep(_FloorTop, _FloorTop + 0.12, P.y);
                half body = 1.0 - floorMask;

                // The planets' real shadows: does the line from here to the lamp's middle pass through a planet?
                float3 toSun = _Sun.xyz - P;
                float dd = max(dot(toSun, toSun), 1e-4);
                float sunDist = sqrt(dd);
                half shadow = 0.0;
                float edge = 1e4;
                [unroll] for (int k = 0; k < 8; k++)
                {
                    float4 pl = _Planets[k];
                    float use = step(0.001, pl.w) * step((float)k + 0.5, _PlanetCount);
                    float t = saturate(dot(pl.xyz - P, toSun) / dd);
                    float dist = length(pl.xyz - (P + toSun * t));
                    // metres here from the shadow's edge (minus inside): the planet's units times how much farther from
                    // the lamp this point is than the planet
                    float spread = sunDist / max(length(_Sun.xyz - pl.xyz), 0.01);
                    float signedEdge = (dist - pl.w) * spread;
                    float aa = max(fwidth(signedEdge), 0.003);
                    shadow = max(shadow, use * saturate(0.5 - signedEdge / aa));
                    edge = lerp(edge, min(edge, abs(signedEdge)), use);
                }

                half alert = _Alert;
                half shade = shadow * lerp(_ShadeCalm, _ShadeFlare, alert) * lerp(_BodyShade, 1.0, floorMask);
                half lit = 1.0 - shadow;

                // the shadows' edge on the floor: a sharp cool line
                half rimAA = max(fwidth(edge), 0.004);
                half rim = (1.0 - smoothstep(_RimWidth * 0.5, _RimWidth * 0.5 + rimAA, edge)) * floorMask;
                half3 light = _RimColor.rgb * rim * _RimLight * (0.2 + 0.8 * alert);

                // warning: heat creeping out from under the lamp over the lit floor, a hot band at its front
                half covered = 1.0 - smoothstep(_Front - 0.08, _Front + 0.08, rFloor);
                half band = exp(-abs(rFloor - _Front) / 0.35) * step(0.05, _Front);
                light += _HeatColor.rgb * ((covered * _HeatLight + band * _FrontLight) * _Warn) * lit * floorMask;

                // burn: the lit floor blazes, a ripple of light runs out over it
                half ripple = exp(-abs(rFloor - _Ripple) / 0.7) * step(0.05, _Ripple) * _RippleFade;
                light += _BlazeColor.rgb * (_Burn * _BlazeLight + ripple * _RippleLight) * lit * floorMask;

                half shimmerW = 0.0;
                half3 shimmer = 0.0;
                [branch] if (alert > 0.001)
                {
                    // pawns out of the shadows: their lamp side warms in the warning and glows hot in the burn
                    float2 texel = 1.0 / _ScaledScreenParams.xy;
                    float3 right = SceneAt(uv + float2(texel.x, 0.0)) - world, left = world - SceneAt(uv - float2(texel.x, 0.0));
                    float3 upward = SceneAt(uv + float2(0.0, texel.y)) - world, downward = world - SceneAt(uv - float2(0.0, texel.y));
                    float3 across = dot(right, right) < dot(left, left) ? right : left;
                    float3 along = dot(upward, upward) < dot(downward, downward) ? upward : downward;
                    float3 n = normalize(cross(along, across) + 1e-6);
                    n *= dot(n, _WorldSpaceCameraPos.xyz - world) < 0.0 ? -1.0 : 1.0;   // the side the camera sees
                    half facing = saturate(dot(n, normalize(_SunWorld.xyz - world)));
                    half exposure = _Warn * 0.35 + _Burn;
                    light += lerp(_HeatColor.rgb, _BlazeColor.rgb, _Burn) * facing * body * lit * exposure * _BodyGlow;

                    // burn: the air over the lit floor and the exposed pawns shimmers
                    [branch] if (_Burn > 0.001 && _Shimmer > 0.0)
                    {
                        float2 wobble = float2(Noise(P.xz * 1.6 + float2(0.0, _Age * 2.4)), Noise(P.zx * 1.9 + float2(_Age * 2.0, 3.7))) - 0.5;
                        shimmerW = lit * _Burn * 0.9;
                        float2 seen = saturate(uv + wobble * _Shimmer * shimmerW);
                        shimmer = SAMPLE_TEXTURE2D_X_LOD(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, UnityStereoTransformScreenSpaceTex(seen), 0).rgb * shimmerW;
                    }
                }

                half amount = onFloor * boxFade;
                half a = saturate(shade + shimmerW) * amount;
                return half4((_ShadowTint.rgb * shade + light + shimmer) * amount, a);
            }
            ENDHLSL
        }
    }
}
