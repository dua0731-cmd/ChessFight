// Pawn Rush sky (concept art 2): saturated blue zenith, pale horizon haze, soft puffy cartoon clouds
// above the horizon and a sea of clouds below it (the course floats in the sky), plus a sun glow.
// Procedural (no textures); clouds drift slowly. _SunDir is set from the scene's sun by
// Course01ArtDress.ApplySky (direction TOWARD the sun).
Shader "ChessFight/Pawn Rush Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.16, 0.42, 0.92, 1)
        _Horizon ("Horizon", Color) = (0.66, 0.84, 0.99, 1)
        _CloudLit ("Cloud lit", Color) = (1, 1, 1, 1)
        _CloudShade ("Cloud shade", Color) = (0.70, 0.79, 0.93, 1)
        _SeaDeep ("Cloud sea gap", Color) = (0.50, 0.70, 0.95, 1)
        _SunDir ("Sun direction", Vector) = (0.3, 0.6, -0.7, 0)
        _SunColor ("Sun glow", Color) = (1, 0.95, 0.82, 1)
        _Coverage ("Cloud coverage", Range(0, 1)) = 0.48
        _SeaCoverage ("Cloud sea coverage", Range(0, 1)) = 0.72
        _Scale ("Cloud scale", Float) = 1.6
        _Drift ("Drift speed", Float) = 0.004
        _Edge ("Cloud edge softness", Range(0.01, 0.3)) = 0.05
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Zenith, _Horizon, _CloudLit, _CloudShade, _SeaDeep, _SunDir, _SunColor;
            float _Coverage, _SeaCoverage, _Scale, _Drift, _Edge;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash (float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float noise (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x),
                            lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }

            float fbm (float2 p)
            {
                float v = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { v += a * noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            // Puffy cloud: density from fbm, a crisp-but-soft edge, and shading from a sample shifted
            // toward the sun (thinner toward the sun = lit side).
            float3 clouds (float2 uv, float coverage, float3 sky, float2 sunShift, out float alpha)
            {
                // big round lumps (low octave) with small billows on top = cumulus, not streaks
                float lump = noise(uv * 0.55);
                float d = lump * 0.65 + fbm(uv) * 0.45;
                alpha = smoothstep(1 - coverage, 1 - coverage + _Edge, d);
                float toward = noise((uv + sunShift) * 0.55) * 0.65 + fbm(uv + sunShift) * 0.45;
                float core = smoothstep(1 - coverage, 1 - coverage + 0.35, d);
                float lit = saturate(0.75 + (d - toward) * 4.0 - core * 0.35);
                float3 c = lerp(_CloudShade.rgb, _CloudLit.rgb, lit);
                return lerp(sky, c, alpha);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float3 sun = normalize(_SunDir.xyz);
                float t = _Time.y * _Drift;
                float h = dir.y;

                // sky gradient: pale haze at the horizon, deep blue overhead
                float up = saturate(h);
                float3 sky = lerp(_Horizon.rgb, _Zenith.rgb, pow(up, 0.55));

                float3 col;
                float a;
                float2 sunShift = normalize(sun.xz + 1e-4) * 0.06;
                if (h >= 0)
                {
                    // cloud layer seen as a flat ceiling: uv grows toward the horizon
                    float2 uv = dir.xz / (h + 0.12) * _Scale + float2(t, t * 0.6);
                    col = clouds(uv, _Coverage, sky, sunShift, a);
                    // thin the clouds right at the horizon so the haze stays clean, and overhead
                    float fade = smoothstep(0.0, 0.10, h) * (1 - smoothstep(0.75, 1.0, h) * 0.6);
                    col = lerp(sky, col, fade);
                }
                else
                {
                    // sea of clouds below: dense white puffs with blue gaps, hazing into the horizon
                    float down = -h;
                    float2 uv = dir.xz / (down + 0.08) * (_Scale * 0.8) + float2(t * 0.5, -t * 0.4);
                    float3 gap = lerp(_Horizon.rgb, _SeaDeep.rgb, saturate(down * 2.5));
                    col = clouds(uv, _SeaCoverage, gap, sunShift, a);
                    col = lerp(_Horizon.rgb, col, smoothstep(0.0, 0.18, down));
                }

                // sun glow and a small soft disc
                float s = saturate(dot(dir, sun));
                col += _SunColor.rgb * (pow(s, 6) * 0.25 + pow(s, 64) * 0.5 + smoothstep(0.9993, 0.9997, s) * 1.5);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
