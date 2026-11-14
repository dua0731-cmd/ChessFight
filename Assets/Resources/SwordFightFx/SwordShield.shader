// R645 (11-14, 사용자 "보호막 그래픽 좀 더 좋게 해"): Sword Fight's respawn protection, in place of the flat blinking
// sphere. A clear shell that only shows at its edge: a warm ivory rim (fresnel) and, on the shell, a faint chessboard
// lattice (8 x 16 squares round it, fading out towards the poles) that lights up where a soft band of light sweeps up
// it and near the rim. Additive, so the piece inside stays plainly visible. _Fade (per renderer, a property block)
// brings it in and out; _Pop flashes it bright for a moment as it forms.
Shader "ChessFight/SwordShield"
{
    Properties
    {
        _Color ("Rim colour", Color) = (0.45, 0.72, 1, 1)
        _Fade ("Fade", Range(0, 1)) = 1
        _Pop ("Pop", Range(0, 1)) = 0
        _RimPower ("Rim power", Range(1, 8)) = 3
        _Lattice ("Lattice strength", Range(0, 2)) = 0.55
        _Sweep ("Sweep speed", Range(0, 4)) = 1.1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Shield"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Fade, _Pop, _RimPower, _Lattice, _Sweep;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 view = normalize(GetWorldSpaceViewDir(i.positionWS));
                half ndv = saturate(dot(n, view));
                half rim = pow(1 - ndv, _RimPower);

                // the lattice: lines between the squares of a chessboard wrapped round the shell
                float3 p = normalize(i.positionOS);
                float lon = atan2(p.z, p.x) / 6.2831853 + 0.5;          // 0..1 round
                float lat = asin(clamp(p.y, -1, 1)) / 3.1415927 + 0.5;  // 0 bottom, 1 top
                float2 cell = float2(lon * 16, lat * 8);
                float2 edge = abs(frac(cell) - 0.5);
                float2 width = fwidth(cell) * 1.2;
                float2 lines2 = 1 - smoothstep(0.5 - width, 0.5, edge);
                half lines = max(lines2.x, lines2.y);
                half poles = smoothstep(0.02, 0.22, lat) * smoothstep(0.98, 0.78, lat);
                lines *= poles;

                // a soft band of light going up the shell, over and over
                float band = frac(_Time.y * _Sweep * 0.5);
                half sweep = exp(-pow((lat - (band * 1.5 - 0.25)) * 7, 2));

                half3 c = _Color.rgb;
                half3 col = c * (rim * 1.1 + 0.03)
                          + c * lines * _Lattice * (0.12 + 0.9 * rim + 1.1 * sweep)
                          + c * sweep * 0.06;
                col *= 1 + _Pop * 2.5;
                return half4(col * _Fade, 1);
            }
            ENDHLSL
        }
    }
}
