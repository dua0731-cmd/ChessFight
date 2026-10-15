// Real dust for the skill effects (R138, 승규 님 10-14: "임펙트를 좀 더 사실 적으로 표현해줘 저 사진은 너무 운석이나 돌맹이 같아
// 보여"): the inked cartoon puffs read as rocks, so an impact's dust is drawn as dust: soft camera-facing billows whose
// edges break up into lumps, lit by the sun like a ball (the side toward it bright, the far side and the bottom in a warm
// shade) and in their own lumps (each lump a little brighter where it faces the sun), thinning and fraying as they go,
// soft where they meet the floor or a body (the camera's depth). Flecks of grit are small dots in the shade colour.
// One mesh of quads made each frame by SkillDust.cs: colour = the lit colour (a = how much shows), uv = -1..1 across the
// billow (x along the screen's right), uv1 = seed, age 0..1, turn, density, uv2 = the shade colour (w = 1 for grit).
// Premultiplied. The sun: _FxSun, set by the effects. Built-in render pipeline. Noise: SkillNoise.cginc.
Shader "ChessFight/Skill Dust"
{
    Properties
    {
        _Soft ("Soft into the scene (m)", Float) = 0.22
        _Light ("Sun strength", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+2" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "SkillNoise.cginc"

            float4 _FxSun;
            float _Soft, _Light;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 d : TEXCOORD1;
                float4 shade : TEXCOORD2;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 d : TEXCOORD1;
                float4 shade : TEXCOORD2;
                float4 screen : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.d = v.d;
                o.shade = v.shade;
                o.screen = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.screen.z);
                return o;
            }

            float Fbm(float3 q)
            {
                float n = 0, a = 0.5;
                for (int k = 0; k < 3; k++)
                {
                    n += a * SkillNoise(q);
                    q = q * 2.07 + 17.3;
                    a *= 0.5;
                }
                return n;
            }

            float2 Turn(float2 c, float cs, float sn) { return float2(c.x * cs - c.y * sn, c.x * sn + c.y * cs); }

            float4 frag (v2f i) : SV_Target
            {
                float2 c = i.uv;
                float r2 = dot(c, c);
                if (r2 >= 1) discard;
                float r = sqrt(r2);

                float soft = 1;
                if (_Soft > 0)
                {
                    float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screen)));
                    soft = saturate((scene - i.screen.z) / _Soft);
                }

                if (i.shade.w > 0.5)
                {
                    // A fleck of grit: a small soft-edged dot.
                    float g = (1 - smoothstep(0.5, 1.0, r)) * i.color.a * soft;
                    if (g < 0.003) discard;
                    return float4(i.shade.rgb * g, g);
                }

                float seed = i.d.x, age = i.d.y;
                float cs = cos(i.d.z), sn = sin(i.d.z);
                float3 at = float3(Turn(c, cs, sn) * 1.35, seed + age * 0.9);
                float n = Fbm(at);
                float billow = saturate(0.5 + 0.7 * n);
                // A soft ball whose edge breaks up into lumps, thinning and fraying from the edges in as it ages.
                float edge = 1 - smoothstep(0.22 + 0.42 * billow, 1.0, r);
                float dens = edge * lerp(0.45, 1.2, billow);
                dens = saturate(dens - age * age * (0.35 + 0.55 * (1 - billow)));

                // Lit like a ball, and in its lumps: denser toward the sun than here = this bit is in its own shade.
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz, back = UNITY_MATRIX_V[2].xyz;
                float z = sqrt(saturate(1 - r2));
                float3 nrm = normalize(c.x * right + c.y * up + (z + 0.25) * back);
                float3 L = normalize(_FxSun.xyz + float3(0, 1e-4, 0));
                float2 toSun = float2(dot(L, right), dot(L, up));
                float n2 = Fbm(float3(Turn(c + toSun * 0.22, cs, sn) * 1.35, seed + age * 0.9));
                float self = saturate(0.55 + 1.3 * (n - n2));
                float lit = saturate(dot(nrm, L) * 0.5 + 0.5);
                lit = saturate(lerp(lit, self, 0.45) * _Light);
                // The underside darker still (dust is lit from above).
                lit *= lerp(0.78, 1, saturate(c.y * 0.5 + 0.6));
                float3 col = lerp(i.shade.rgb, i.color.rgb, lit);

                float a = dens * i.d.w * i.color.a * soft;
                if (a < 0.003) discard;
                return float4(col * a, a);
            }
            ENDCG
        }
    }
}
