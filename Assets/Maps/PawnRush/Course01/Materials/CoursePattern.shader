// Pawn Rush course graybox surfaces, patterned in WORLD space so a box of any size looks right
// without textures or UVs:
//   top faces    a 2 m checker (_Color / _Color2), or plain _SideColor when _TopStyle = 1
//   side faces   _SideStyle 0 plain, 1 stone blocks with dark joints (climbable),
//                2 smooth marble with faint veins (cannot be climbed)
// The two wall looks are the course's rule made visible: block joints = you can climb it.
Shader "ChessFight/CoursePattern"
{
    Properties
    {
        _Color ("Checker light", Color) = (0.93, 0.88, 0.76, 1)
        _Color2 ("Checker dark", Color) = (0.55, 0.38, 0.24, 1)
        _SideColor ("Side colour", Color) = (0.85, 0.8, 0.7, 1)
        _LineColor ("Joint / vein colour", Color) = (0.4, 0.35, 0.3, 1)
        _TopStyle ("Top (0 checker, 1 plain)", Float) = 0
        _SideStyle ("Side (0 plain, 1 blocks, 2 marble)", Float) = 0
        _Cell ("Checker cell (m)", Float) = 2
        _Glossiness ("Smoothness", Range(0, 1)) = 0.2
        _Metallic ("Metallic", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        fixed4 _Color, _Color2, _SideColor, _LineColor;
        half _TopStyle, _SideStyle, _Cell, _Glossiness, _Metallic;

        // 1 on a joint line of width w (metres) between blocks of size s, else 0.
        float Joint(float v, float s, float w)
        {
            float f = frac(v / s) * s;
            return step(f, w) + step(s - w, f);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 p = IN.worldPos;
            float3 n = abs(normalize(IN.worldNormal));
            fixed3 c = _SideColor.rgb;
            half gloss = _Glossiness;
            if (n.y > 0.7)
            {
                if (_TopStyle < 0.5)
                {
                    float parity = frac((floor(p.x / _Cell) + floor(p.z / _Cell)) * 0.5) * 2.0;
                    c = lerp(_Color.rgb, _Color2.rgb, parity);
                }
            }
            else
            {
                // The wall's own horizontal axis: x for a face looking along z, z for one along x.
                float u = n.x > n.z ? p.z : p.x;
                if (_SideStyle > 0.5 && _SideStyle < 1.5)
                {
                    float row = floor(p.y / 0.5);
                    float shifted = u + frac(row * 0.5) * 1.0;
                    float joint = saturate(Joint(p.y, 0.5, 0.03) + Joint(shifted, 1.0, 0.03));
                    float grain = frac(sin(dot(floor(float2(shifted, row)), float2(12.9898, 78.233))) * 43758.5453);
                    c = lerp(_SideColor.rgb * (0.9 + 0.15 * grain), _LineColor.rgb, joint);
                }
                else if (_SideStyle > 1.5)
                {
                    float vein = sin(u * 1.7 + p.y * 2.3 + sin(u * 0.6 - p.y * 0.9) * 2.0);
                    c = lerp(_SideColor.rgb, _LineColor.rgb, smoothstep(0.96, 1.0, vein) * 0.5);
                }
            }
            o.Albedo = c;
            o.Metallic = _Metallic;
            o.Smoothness = gloss;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
