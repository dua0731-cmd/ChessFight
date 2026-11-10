// Test copy only (prototype): a chess piece's look without a texture or UVs.
// - Colours come from the mesh's vertex colours, painted part by part by the rig script with the old pawn texture's
//   own colours (skin, head, hands, feet, eyes, glints, blush). Pure blue marks the collar: it becomes _TeamColor.
// - The old texture's painted patches (its blocky light and dark squares) are drawn here: cells around the body's
//   axis and up its height, in the rest pose (UV2 holds each vertex's rest position, so they do not swim when the
//   body bends).
// - Lighting is the Standard model, as on the old pawn material (smoothness 0.25).
Shader "ChessFight/PieceSkin"
{
    Properties
    {
        _TeamColor ("Team colour (replaces pure blue)", Color) = (0.314, 0.376, 0.58, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.25
        _DarkGlossiness ("Smoothness of the dark parts", Range(0, 1)) = 0.4
        _Patch ("Painted patches: strength", Range(0, 0.3)) = 0.10
        _PatchAround ("Painted patches: how many around", Range(4, 64)) = 18
        _PatchHeight ("Painted patches: height (m)", Range(0.005, 0.2)) = 0.04
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        fixed4 _TeamColor;
        half _Glossiness;
        half _DarkGlossiness;
        half _Patch;
        half _PatchAround;
        half _PatchHeight;

        struct Input
        {
            float4 color : COLOR;
            float3 rest;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.rest = v.texcoord2.xyz;
        }

        float Hash21(float2 p)
        {
            float3 p3 = frac(p.xyx * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 painted = IN.color.rgb;   // sRGB, as painted
            float team = saturate((painted.b - max(painted.r, painted.g)) * 4.0 - 3.0);
            float3 c = painted;
            #ifndef UNITY_COLORSPACE_GAMMA
            c = GammaToLinearSpace(c);
            #endif
            float3 albedo = lerp(c, _TeamColor.rgb, team);

            float around = atan2(IN.rest.x, IN.rest.z) * 0.15915494 + 0.5;
            float2 cell = float2(floor(around * _PatchAround), floor(IN.rest.y / _PatchHeight));
            albedo *= 1.0 + (Hash21(cell) - 0.5) * 2.0 * _Patch;

            o.Albedo = albedo;
            float lum = dot(painted, float3(0.2126, 0.7152, 0.0722));
            o.Smoothness = lerp(_DarkGlossiness, _Glossiness, saturate(lum * 3.0));
            o.Metallic = 0;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
