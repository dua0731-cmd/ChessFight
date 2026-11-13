// The Sword Fight bishop's energy (R132, 승규 님 10-13, with a picture of a glowing violet orb trailing wavy strands of
// light: "비숍 내가 원한건 이런 디자인이야"): one mesh holds every part of it as quads (four corners at a part's middle,
// world space), opened out here to face whichever camera draws it. Every part is light, added over what is behind
// (premultiplied alpha with alpha 0):
// 1 glow   — a soft light in the energy's colour;
// 2 spark  — a tiny twinkling star (a hot point with four short rays), stretched along its flight when it moves;
// 5 orb    — a ball of energy: a bright rim, a glowing body full of twinkling sparkles drifting through it, a white-hot
//            core with four rays, a halo round it;
// 6 strand — a piece of a thin strand of light (soft across, as long as the quad), laid end to end along a trail.
// Per part: COLOR = tint (rgb) and strength (a); TEXCOORD0 = corner x, corner y, kind, life (0..1);
// TEXCOORD1 = width, height, turn (radians), seed; TEXCOORD2 = axis (world xyz) and 1 to stretch along it;
// TEXCOORD3.x = its own clock (s, for the sparkles). Built-in render pipeline.
Shader "ChessFight/Skill Energy"
{
    Properties
    {
        [HDR] _Edge ("Energy", Color) = (0.78, 0.32, 1.3, 1)
        [HDR] _Lip ("Hot", Color) = (1.4, 0.95, 1.8, 1)
        _Deep ("Deep (covering, so it shows on a light floor)", Color) = (0.24, 0.07, 0.42, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+12" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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

            float4 _Edge, _Lip, _Deep;
            float _Alpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float4 a : TEXCOORD0;   // corner x, corner y, kind, life
                float2 b : TEXCOORD1;   // seed, clock
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 c = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz;
                float3 toCam = normalize(_WorldSpaceCameraPos - c);
                float3 camRight = UNITY_MATRIX_V[0].xyz, camUp = UNITY_MATRIX_V[1].xyz;
                float3 right, up;
                if (v.uv2.w > 0.5)
                {
                    up = normalize(v.uv2.xyz);
                    right = cross(toCam, up);
                    float l = length(right);
                    right = l > 1e-4 ? right / l : camRight;
                }
                else
                {
                    float s, co;
                    sincos(v.uv1.z, s, co);
                    right = camRight * co + camUp * s;
                    up = -camRight * s + camUp * co;
                }
                float3 world = c + right * (v.uv0.x * v.uv1.x * 0.5) + up * (v.uv0.y * v.uv1.y * 0.5);
                o.pos = mul(UNITY_MATRIX_VP, float4(world, 1));
                o.color = v.color;
                o.a = v.uv0;
                o.b = float2(v.uv1.w, v.uv3.x);
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Twinkling sparkles in cells drifting through the orb: a few cells lit, each a small soft star.
            float Sparkles(float2 p, float scale, float2 drift, float clock, float seed, float lit)
            {
                float2 sp = p * scale + drift * clock + seed;
                float2 cell = floor(sp);
                float h1 = Hash(cell), h2 = Hash(cell + 17.3), h3 = Hash(cell + 41.7);
                float2 f = frac(sp) - 0.5 - (float2(h1, h3) - 0.5) * 0.55;
                float twinkle = 0.45 + 0.55 * sin(clock * (7 + 6 * h3) + h1 * 40);
                return step(1 - lit, h2) * saturate(1 - length(f) * 6.5) * twinkle;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 p = i.a.xy;
                float kind = i.a.z, life = saturate(i.a.w), seed = i.b.x, clock = i.b.y;
                float4 tint = i.color;
                // Light added over what is behind, and (so it shows on the light floor too, not only in the dark) a
                // cover of its deep colour.
                float3 col = 0;
                float cover = 0;

                if (kind < 1.5)
                {
                    // Glow.
                    float g = saturate(exp(-dot(p, p) * 3.2) - 0.04);
                    col = _Edge.rgb * g * 0.6;
                    cover = g * 0.28;
                }
                else if (kind < 2.5)
                {
                    // Spark: a hot point and four short rays.
                    float r = length(p);
                    float g = saturate(1 - r);
                    g *= g;
                    float rays = exp(-abs(p.x) * 16) * exp(-abs(p.y) * 3.2) + exp(-abs(p.y) * 16) * exp(-abs(p.x) * 3.2);
                    col = lerp(_Edge.rgb, _Lip.rgb, g) * (g * 1.6 + rays * 0.6);
                    cover = saturate(g * 1.5 + rays * 0.3) * 0.55;
                }
                else if (kind < 5.5)
                {
                    // Orb: the ball fills 0.6 of the quad, its halo the rest.
                    float r = length(p);
                    const float R = 0.6;
                    float inside = saturate(1 - r / R);
                    float body = sqrt(inside);
                    float shell = exp(-pow((r - R * 0.93) / 0.08, 2));
                    float halo = r > R ? pow(saturate(1 - (r - R) / (1 - R)), 2.2) : 1;
                    float stars = Sparkles(p, 7.0, float2(0.6, -0.9), clock, seed, 0.32)
                                + Sparkles(p, 12.0, float2(-0.8, -1.3), clock, seed + 7.1, 0.28) * 0.7;
                    float core = exp(-r * r * 55) * 1.6;
                    float rays = (exp(-abs(p.x) * 42) * exp(-abs(p.y) * 6) + exp(-abs(p.y) * 42) * exp(-abs(p.x) * 6)) * 0.5 * inside;
                    // Its body swirls a little (brighter and darker drifts).
                    float swirl = 0.75 + 0.25 * sin(atan2(p.y, p.x) * 3 + clock * 2.4 + r * 9 + seed);
                    col = _Edge.rgb * (body * 0.3 * swirl + shell * 1.1 + halo * 0.25) + _Lip.rgb * (stars * (0.35 + body) * 1.6 + core + rays);
                    cover = r < R ? saturate(0.55 + body * 0.35 + shell * 0.3) : halo * 0.3;
                }
                else
                {
                    // Strand: thin and soft across, a little brighter in its middle.
                    float across = abs(p.x);
                    float thread = pow(saturate(1 - across), 2.0);
                    col = lerp(_Edge.rgb, _Lip.rgb, 0.5 + 0.3 * thread) * thread * thread * 0.9;
                    cover = thread * 0.75;
                }
                float s = tint.a * _Alpha;
                return float4(_Deep.rgb * cover * s + col * tint.rgb * s, cover * s);
            }
            ENDCG
        }
    }
}
