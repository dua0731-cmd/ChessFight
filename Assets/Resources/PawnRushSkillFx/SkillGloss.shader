// Glossy toy props for the Pawn Rush skill effects (R125 trial, 승규 님 10-12: "광택 있는 골드색 실버색도"): the queen's
// chess pieces in polished gold and silver, the crown's pearls, the bishop's rods and caps, the knight's daze pawns, the
// little slabs. Painted toy shading in hard bands from the sun, and with _Metal a cartoon chrome: the reflection of a
// bright sky above a sharp horizon glint and a dark ground below, a crisp highlight with a softer sheen round it, so
// gold and silver read as polished metal on any floor without reflection probes. _TwoTone paints the sides of a slab in
// _SideColor (the course's orange pads on navy blocks). Ink where the surface turns away; _Heat warms it (the queen's
// pieces heating in her fire), _Rim lights its edge, _Dissolve eats it away, _Color.a fades it by a fine dither (it
// stays opaque and depth-sorted). Property names match Skill Toon's and Skill Ink's, so the same blocks drive it. The
// sun: _FxSun, set by the effects. Built-in render pipeline. Noise: SkillNoise.cginc.
Shader "ChessFight/Skill Gloss"
{
    Properties
    {
        _Lit ("Lit / sky", Color) = (1, 0.95, 0.72, 1)
        _Mid ("Base", Color) = (0.91, 0.7, 0.24, 1)
        _Shade ("Shadow / ground", Color) = (0.6, 0.41, 0.08, 1)
        _Metal ("Polished metal (0 glossy toy .. 1 chrome)", Range(0, 1)) = 1
        [HDR] _Spec ("Highlight", Color) = (1.5, 1.45, 1.35, 1)
        _Gloss ("Highlight tightness", Float) = 60
        _Ink ("Ink edge (0 = none)", Range(0, 1)) = 0.16
        _InkColor ("Ink", Color) = (0.14, 0.14, 0.18, 1)
        _Flash ("White", Range(0, 1)) = 0
        _Heat ("Heat", Range(0, 1.2)) = 0
        [HDR] _HotColor ("Hot", Color) = (1.35, 0.6, 0.18, 1)
        [HDR] _Rim ("Edge light", Color) = (0, 0, 0, 1)
        _RimPower ("Edge light power", Range(0.5, 8)) = 3
        _Dissolve ("Eaten away", Range(0, 1)) = 0
        _Seed ("Seed (xyz), age (w)", Vector) = (0, 0, 0, 0)
        _Lump ("(unused, for Skill Toon's blocks)", Float) = 0
        _Color ("Tint (a = fade)", Color) = (1, 1, 1, 1)
        _TwoTone ("Sides in the side colour", Float) = 0
        _SideColor ("Side colour", Color) = (0.14, 0.14, 0.18, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Cull Back
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "SkillNoise.cginc"

            float4 _Lit, _Mid, _Shade, _Spec, _InkColor, _HotColor, _Rim, _Seed, _Color, _SideColor, _FxSun;
            float _Metal, _Gloss, _Ink, _Flash, _Heat, _RimPower, _Dissolve, _TwoTone;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
                float3 q : TEXCOORD2;
                float4 screen : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.q = v.vertex.xyz * 2.2 + _Seed.xyz;
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            float Bayer4(float2 px)
            {
                int2 c = int2(fmod(px, 4));
                const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                return (m[c.y * 4 + c.x] + 0.5) / 16.0;
            }

            float4 frag (v2f i) : SV_Target
            {
                if (_Color.a < 0.999)
                {
                    float2 px = i.screen.xy / max(i.screen.w, 1e-5) * _ScreenParams.xy;
                    clip(_Color.a - Bayer4(px));
                }
                if (_Dissolve > 0)
                {
                    float bite = SkillNoise(i.q) * 0.5 + 0.5;
                    clip(bite - _Dissolve * 1.02);
                }

                float3 N = normalize(i.normal);
                float3 V = normalize(_WorldSpaceCameraPos - i.world);
                float3 L = dot(_FxSun.xyz, _FxSun.xyz) > 1e-6 ? normalize(_FxSun.xyz) : normalize(float3(0.35, 0.79, -0.5));
                float ndl = dot(N, L) * 0.5 + 0.5;
                float aa = max(fwidth(ndl), 1e-4);

                // Toy paint: three hard bands from the sun.
                float3 diff = lerp(_Shade.rgb, _Mid.rgb, smoothstep(0.36 - aa, 0.36 + aa, ndl));
                diff = lerp(diff, _Lit.rgb, smoothstep(0.8 - aa, 0.8 + aa, ndl) * 0.55);

                // Chrome: the sky above a sharp horizon, a glint along it, the ground below.
                float3 R = reflect(-V, N);
                float raa = max(fwidth(R.y), 1e-3);
                float sky = smoothstep(-raa, raa, R.y);
                float3 up = lerp(_Mid.rgb * 1.08, _Lit.rgb, smoothstep(0.05, 0.6, R.y));
                float3 down = lerp(_Mid.rgb * 0.62, _Shade.rgb * 0.5, smoothstep(0.0, 0.5, -R.y));
                float3 env = lerp(down, up, sky) + _Lit.rgb * 0.4 * exp(-pow(R.y / 0.04, 2));
                float3 col = lerp(diff, env * lerp(0.75, 1.0, ndl), _Metal * 0.8);

                // A crisp highlight and a soft sheen round it.
                float3 H = normalize(L + V);
                float nh = saturate(dot(N, H));
                float spot = smoothstep(0.45, 0.55, pow(nh, _Gloss));
                float sheen = pow(nh, _Gloss * 0.18) * 0.3 * (0.4 + 0.6 * _Metal);
                col += _Spec.rgb * (spot + sheen);

                // A slab's sides in their own colour (still a touch of highlight).
                if (_TwoTone > 0.5 && N.y < 0.55)
                {
                    float3 s = _SideColor.rgb * lerp(0.78, 1.18, smoothstep(0.3, 0.75, ndl));
                    col = s + _Spec.rgb * spot * 0.25;
                }

                col = lerp(col, _HotColor.rgb, saturate(_Heat) * 0.55);
                float ndv = saturate(dot(N, V));
                float edge = 1 - ndv;
                col += _Rim.rgb * pow(edge, _RimPower);
                float eaa = max(fwidth(edge), 1e-3);
                float ink = smoothstep(1 - _Ink - eaa, 1 - _Ink + eaa, edge) * step(1e-4, _Ink);
                col = lerp(col, _InkColor.rgb, ink);
                col = lerp(col, float3(1, 1, 1), _Flash);
                col *= _Color.rgb;
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
