// A laid hemp rope (R140, 승규 님 10-14: "밧줄 만 더 그래픽 적으로 밧줄이다! 라고 할 수 있게 만들어줘 색도 실제 밧줄 처럼 되게
// 해주고"): matte natural fibre, not a glossy toy tube. The mesh is three strands twisted round each other
// (PawnRushSkillFx.Rope.cs: a lobed tube whose lobes turn along it), uv.x its length in lays (one strand's full turn),
// uv.y once round it; here each strand is shaded round (light on its crown, dark in the grooves between), its yarns run
// across it at the opposite twist as fine streaks, the fibres are a little uneven and catch light at the silhouette (a
// fuzz). Lit by the effects' sun (_FxSun) with a soft wrap, no highlight. The same property names as Skill Gloss, so a
// SkillInkKit.Prop drives it: _Lit the pale fibre, _Mid the rope, _Shade the grooves, _Flash white, _Color.a fades it by
// a fine dither (it stays opaque and depth-sorted). Built-in render pipeline. Noise: SkillNoise.cginc.
Shader "ChessFight/Skill Rope"
{
    Properties
    {
        _Lit ("Pale fibre", Color) = (0.91, 0.84, 0.66, 1)
        _Mid ("Rope", Color) = (0.76, 0.63, 0.42, 1)
        _Shade ("Grooves", Color) = (0.42, 0.31, 0.18, 1)
        _Flash ("White", Range(0, 1)) = 0
        _Color ("Tint (a = fade)", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        // R142: both sides: where rope presses into rope (a hitch's crossing, turns lying on each other) its inside shows
        // dark, not a hole through to the floor.
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "SkillNoise.cginc"

            float4 _Lit, _Mid, _Shade, _Color, _FxSun;
            float _Flash;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 screen : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = v.uv;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float Bayer4(float2 px)
            {
                int2 c = int2(fmod(px, 4));
                const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                return (m[c.y * 4 + c.x] + 0.5) / 16.0;
            }

            float4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                if (_Color.a < 0.999)
                {
                    float2 px = i.screen.xy / max(i.screen.w, 1e-5) * _ScreenParams.xy;
                    clip(_Color.a - Bayer4(px));
                }

                // Across the three strands: 0 on a strand's crown, 1 in the groove between two.
                float q = 3 * (i.uv.y - i.uv.x);
                float w = frac(q);
                float across = abs(w - 0.5) * 2;
                across = 1 - across;
                float groove = smoothstep(0.55, 1.0, across);
                // A strand's yarns: fine streaks across it at the opposite twist, a little uneven.
                float yarn = frac(w * 5 + i.uv.x * 6);
                float streak = smoothstep(0.0, 0.25, yarn) * (1 - smoothstep(0.55, 1.0, yarn));
                float n = SkillNoise(i.world * 55) * 0.5 + 0.5;
                float fibre = saturate(streak * 0.75 + n * 0.35);

                float3 rope = lerp(_Mid.rgb, _Lit.rgb, fibre * (1 - groove) * 0.55);
                rope = lerp(rope, _Shade.rgb, groove * 0.85);
                rope *= 0.9 + 0.2 * (SkillNoise(i.world * 9) * 0.5 + 0.5);

                float3 N = normalize(i.normal);
                float3 V = normalize(_WorldSpaceCameraPos - i.world);
                float3 L = dot(_FxSun.xyz, _FxSun.xyz) > 1e-6 ? normalize(_FxSun.xyz) : normalize(float3(0.35, 0.79, -0.5));
                // Matte, a soft wrap from the sun, a little sky from above; fibres catch the light at the silhouette.
                float wrap = saturate((dot(N, L) + 0.35) / 1.35);
                float3 col = rope * (0.46 + 0.68 * wrap + 0.08 * saturate(N.y));
                col += _Lit.rgb * pow(1 - saturate(dot(N, V)), 3) * 0.16 * (1 - groove);
                col = lerp(col, float3(1, 1, 1), _Flash);
                col *= _Color.rgb;
                // Its inside, seen where it presses into itself: in shadow.
                if (facing < 0) col = _Shade.rgb * 0.35;
                return float4(col, 1);
            }
            ENDCG
        }

        // R142: the rope casts a shadow (on the floor under the bishop's X, on a piece it is tied round).
        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            float4 _Color;

            struct v2f
            {
                V2F_SHADOW_CASTER;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                clip(_Color.a - 0.5);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}
