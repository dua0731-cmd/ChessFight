// Test copy only (prototype B): a hand-painted look on the model's smooth normals.
// Light falls in two soft tones: lit, and a cooler, darker shadow tone (_ShadowTint), meeting at a terminator that
// wobbles a little like a brush edge (value noise in the rest pose, UV2, so it stays on the body). A faint grain in
// the paint and a thin rim of light on the lit side finish it. Colours are the vertex colours the rig script painted.
Shader "ChessFight/PiecePainted"
{
    Properties
    {
        _ShadowTint ("Shadow tone", Color) = (0.74, 0.66, 0.72, 1)
        _Mid ("Where light meets shadow", Range(0, 1)) = 0.42
        _Soft ("Softness of that edge", Range(0.005, 0.25)) = 0.05
        _Wobble ("Brushed edge", Range(0, 0.3)) = 0.04
        _Grain ("Paint grain", Range(0, 0.2)) = 0.03
        _Rim ("Rim light", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Painted fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        fixed4 _ShadowTint;
        half _Mid;
        half _Soft;
        half _Wobble;
        half _Grain;
        half _Rim;

        struct Input
        {
            float4 color : COLOR;
            float3 rest;
        };

        struct SurfaceOutputPainted
        {
            fixed3 Albedo;
            fixed3 Normal;
            fixed3 Emission;
            half Specular;
            fixed Gloss;
            fixed Alpha;
            half Edge;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.rest = v.texcoord2.xyz;
        }

        float Hash31(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        float Noise3(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(Hash31(i), Hash31(i + float3(1, 0, 0)), f.x),
                             lerp(Hash31(i + float3(0, 1, 0)), Hash31(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(Hash31(i + float3(0, 0, 1)), Hash31(i + float3(1, 0, 1)), f.x),
                             lerp(Hash31(i + float3(0, 1, 1)), Hash31(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }

        half4 LightingPainted(SurfaceOutputPainted s, half3 lightDir, half3 viewDir, half atten)
        {
            half wrap = dot(s.Normal, lightDir) * 0.5 + 0.5;
            half lit = smoothstep(_Mid - _Soft, _Mid + _Soft, wrap + s.Edge) * atten;
            // the lit side keeps a gentle gradient (a painter's form shading), the shadow side one flat cool tone
            half3 shade = lerp(_ShadowTint.rgb * 0.5, (half3)lerp(0.62, 0.92, wrap), lit);
            half rim = pow(1.0 - saturate(dot(s.Normal, normalize(viewDir))), 4.0) * _Rim * lit;
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * (shade + rim);
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutputPainted o)
        {
            float3 painted = IN.color.rgb;
            float3 c = painted;
            #ifndef UNITY_COLORSPACE_GAMMA
            c = GammaToLinearSpace(c);
            #endif
            // brush-stroke grain: stretched around the body, short up it
            float g = Noise3(IN.rest * float3(55.0, 140.0, 55.0)) * 0.6 + Noise3(IN.rest * 23.0) * 0.4;
            o.Albedo = c * (1.0 + (g - 0.5) * 2.0 * _Grain);
            o.Edge = (Noise3(IN.rest * 9.0) - 0.5) * 2.0 * _Wobble;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
