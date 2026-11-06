// The war room's north return speed lane painted on its floor (R461, 승규 11-05 "표시를 해줘 데칼이라던가 이런걸로"): a
// projected decal as the grand stair hall's (Assets/VFX/Shaders/VFX_DecalProjection.hlsl: only surfaces facing up inside
// the box - a landing, the gallery, each tread's top, never a riser or a wall), drawn additively in the cannons' teal:
// two edge lines, a faint fill and a chevron every _Spacing metres pointing along the box's +z, a light running along
// them at _Travel m/s (a bright head every _Wave metres, a fading trail behind it). Per renderer (LibraryStairBoostFx):
// _Size = (box width, box length, lane width) in metres, _Start = how far along the route the box's back face is, so
// the chevrons and the light run on unbroken from one box to the next.
Shader "ChessFight/PawnRush/Speed Lane Decal"
{
    Properties
    {
        _Color ("Lane color", Color) = (0.2, 1, 0.75, 1)
        _Fill ("Fill", Range(0, 1)) = 0.12
        _Edge ("Edge lines", Range(0, 4)) = 1.1
        _Chevron ("Chevrons", Range(0, 4)) = 0.7
        _Head ("Running light", Range(0, 8)) = 2.6
        _Spacing ("Chevron spacing (m)", Float) = 1.1
        _Wave ("Metres between running lights", Float) = 6
        _Travel ("Running light speed (m/s)", Float) = 11
        _Upward ("Floor facing up past (cos)", Range(0, 1)) = 0.7
        _EdgeFade ("Fade at the box's top and bottom (share)", Range(0.01, 0.5)) = 0.25
        _Size ("Box width, box length, lane width (m)", Vector) = (2, 10, 1.6, 0)
        _Start ("Route distance at the box's back (m)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-12" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Decal"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/VFX/Shaders/VFX_DecalProjection.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Fill;
                half _Edge;
                half _Chevron;
                half _Head;
                float _Spacing;
                float _Wave;
                float _Travel;
                half _Upward;
                half _EdgeFade;
                float4 _Size;
                float _Start;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half surface;
                float3 local = DecalLocal(input.positionCS, _Upward, _EdgeFade, surface);
                float across = local.x * _Size.x;                       // m from the lane's middle
                float along = (local.z + 0.5) * _Size.y + _Start;        // m along the route
                float half_ = _Size.z * 0.5;
                float ax = abs(across);
                float aa = max(fwidth(across), 1e-4);
                half inside = 1.0 - smoothstep(half_ - aa, half_ + aa, ax);
                // two edge lines just inside the lane's sides
                half edge = 1.0 - smoothstep(0.035, 0.035 + 2.0 * aa, abs(ax - (half_ - 0.06)));
                // a chevron in each cell, its tip forward
                float cell = (frac(along / _Spacing) - 0.5) * _Spacing;
                float v = cell + ax * 0.55 - 0.18;
                float av = max(fwidth(v), 1e-4);
                half chevron = (1.0 - smoothstep(0.07, 0.07 + 2.0 * av, abs(v))) * (1.0 - smoothstep(half_ - 0.22, half_ - 0.12, ax));
                // the running light: just behind a head w is near 1
                float w = frac((along - _Time.y * _Travel) / _Wave);
                half head = w * w * w * w;
                half k = _Fill * (0.6 + head) + _Edge * edge * (0.5 + 0.5 * head) + chevron * (_Chevron + _Head * head);
                return half4(_Color.rgb * (k * inside * surface), 0.0);
            }
            ENDHLSL
        }
    }
}
