// R725 (the user, before merging into main: "천문대 안에서만 밤하늘"): the night the Royal Observatory's slit and balcony
// look out on. RoyalObservatorySky puts it in the scene's skybox while the camera is in the observatory and puts the
// scene's own back after. Worked out from the view direction alone - no texture, so nothing stretches at any heading:
// a deep navy sky lighter toward the horizon, two layers of stars on a direction grid (small and many, few and large),
// each twinkling on its own, and a faint violet Milky Way along a tilted band. Drawn behind everything (Background).
Shader "ChessFight/RoyalObservatory/NightSkybox"
{
    Properties
    {
        _Top ("Zenith", Color) = (0.012, 0.018, 0.07, 1)
        _Horizon ("Horizon", Color) = (0.05, 0.06, 0.17, 1)
        _Below ("Below the horizon", Color) = (0.01, 0.012, 0.04, 1)
        _Band ("Milky Way", Color) = (0.22, 0.15, 0.40, 1)
        _BandAxis ("Milky Way axis", Vector) = (0.35, 0.55, 0.76, 0)
        _Density ("Star density", Range(0, 1)) = 0.55
        _Bright ("Star brightness", Range(0, 4)) = 1.8
        _Twinkle ("Twinkle", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Top, _Horizon, _Below, _Band, _BandAxis;
            float _Density, _Bright, _Twinkle;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash21 (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2 (float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x), lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), u.x), u.y);
            }

            // One layer of stars: the sky cut into cells (heading x elevation, narrower near the poles so they are
            // not crowded there), at most one star in a cell, placed and sized by the cell's hash.
            float stars (float3 d, float cells, float chance, float size)
            {
                float heading = atan2(d.x, d.z) / (2.0 * UNITY_PI) + 0.5;
                float elevation = asin(clamp(d.y, -1.0, 1.0)) / UNITY_PI + 0.5;
                float ring = max(1.0, floor(cells * 2.0 * cos((elevation - 0.5) * UNITY_PI)));
                float2 grid = float2(heading * ring, elevation * cells);
                float2 cell = floor(grid);
                float h = hash21(cell + ring * 0.13);
                if (h > chance) return 0.0;
                float2 at = float2(hash21(cell + 7.1), hash21(cell + 3.7)) * 0.7 + 0.15;
                float r = length(frac(grid) - at);
                float twinkle = 1.0 - _Twinkle * 0.5 * (1.0 + sin(_Time.y * (1.5 + h * 40.0) + h * 300.0));
                float bright = lerp(0.35, 1.0, frac(h * 91.7));
                return smoothstep(size, 0.0, r) * bright * twinkle;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = d.y;
                float3 col = up >= 0.0 ? lerp(_Horizon.rgb, _Top.rgb, pow(saturate(up), 0.55))
                                       : lerp(_Horizon.rgb, _Below.rgb, saturate(-up * 4.0));
                // The Milky Way: a soft band about a tilted great circle, broken up by noise.
                float b = dot(d, normalize(_BandAxis.xyz));
                float heading = atan2(d.x, d.z);
                float cloud = noise2(float2(heading * 3.0, b * 9.0)) * 0.6 + noise2(float2(heading * 9.0, b * 25.0)) * 0.4;
                float band = exp(-b * b * 22.0) * smoothstep(0.25, 0.85, cloud);
                col += _Band.rgb * band * 0.8;
                float sky = smoothstep(-0.05, 0.08, up);   // no stars below the horizon
                float s = stars(d, 180.0, 0.10 * _Density, 0.16) + stars(d, 60.0, 0.06 * _Density, 0.09) * 1.8
                        + stars(d, 320.0, 0.20 * _Density, 0.22) * (0.5 + band * 1.5);
                col += float3(1.0, 0.96, 0.88) * s * _Bright * sky;
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
