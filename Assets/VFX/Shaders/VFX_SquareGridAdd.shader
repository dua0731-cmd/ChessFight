// A king's squares lit round him on the floor (R190, 10-28, F1 of his counter, the guard; reference F_01_guard_b.png):
// the board's lines round the square he stands on and the eight round it (his moves) as lines of light - a crisp hot
// core in a soft glow - running on a little past the outer edge and dying there, a faint fill in the eight squares
// brighter toward their edges, and a soft brighter band going slowly round, all added onto the board. It lies in the
// object's XZ plane, the quad _Span metres a side (VfxSquareGrid scales it so), +Z the king's way and +X his right, the
// squares _Cell metres. The lines are drawn out from his square to _Reveal squares (the farther of across and along,
// from the middle of his) with a bright head. A square with no floor (its bit in _Floor 0: bit (row + 1) * 3 + column + 1,
// counted from behind him on his left) has no fill, and a line is not drawn where neither square beside it has floor.
// _Intensity, _Reveal, _Phase, _Floor, _Cell, _Span and _Fade are set per renderer by VfxSquareGrid.
Shader "ChessFight/VFX/Square Grid Additive"
{
    Properties
    {
        [HDR] _Color ("Glow color (HDR)", Color) = (0.8, 0.52, 0.12, 1)
        [HDR] _CoreColor ("Core color (HDR)", Color) = (1, 0.957, 0.839, 1)
        _CoreBoost ("Core brightness over the glow", Float) = 1.5
        _LineWidth ("Line width (m)", Range(0.005, 0.2)) = 0.02
        _GlowWidth ("Glow reach (m)", Range(0.005, 0.5)) = 0.025
        _Overrun ("Lines on past the outer edge (squares)", Range(0, 1)) = 0.3
        _Fill ("Fill inside the eight squares", Range(0, 1)) = 0.02
        _FillEdge ("Fill brighter toward the edge (m)", Range(0.01, 1)) = 0.12
        _HeadSize ("Running head glow (squares)", Range(0.01, 1)) = 0.2
        _HeadLight ("Running head light", Float) = 3
        _Shimmer ("Band going round", Range(0, 2)) = 0.35
        _Cell ("Square (m)", Float) = 1.25
        _Span ("Quad side (m)", Float) = 4.75
        _Reveal ("Drawn out (squares)", Float) = 3
        _Phase ("Band angle (radians)", Float) = 0
        _Floor ("Squares with floor (bits)", Float) = 511
        _Intensity ("Intensity", Float) = 1
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                half _CoreBoost;
                float _LineWidth;
                float _GlowWidth;
                float _Overrun;
                half _Fill;
                float _FillEdge;
                float _HeadSize;
                half _HeadLight;
                half _Shimmer;
                float _Cell;
                float _Span;
                float _Reveal;
                float _Phase;
                float _Floor;
                half _Intensity;
                half _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 local : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.local = input.positionOS.xz;
                return output;
            }

            // Whether square (column c, row r) round his (0, 0) has floor; none outside the nine.
            half On(float c, float r)
            {
                if (abs(c) > 1.5 || abs(r) > 1.5) return 0.0;
                uint bit = (uint)((r + 1.0) * 3.0 + (c + 1.0) + 0.5);
                return (half)(((uint)(_Floor + 0.5) >> bit) & 1u);
            }

            // Metres from the nearest lit line of one way: a across the lines (squares), b along them. The lines are at a =
            // -1.5, -0.5, 0.5 and 1.5, from b = -1.5 - _Overrun to 1.5 + _Overrun, lit where a square beside them has floor
            // (alongX: the lines run along x, so b is the column). run is 1 on the lines and dies along the run on.
            float Lines(float a, float b, bool alongX, out half run)
            {
                float at = clamp(floor(a) + 0.5, -1.5, 1.5);
                float past = max(abs(b) - 1.5, 0.0);
                run = 1.0 - saturate(past / max(_Overrun, 1e-3));
                float beside = clamp(round(b), -1.0, 1.0);
                half lit = alongX ? max(On(beside, at - 0.5), On(beside, at + 0.5)) : max(On(at - 0.5, beside), On(at + 0.5, beside));
                return lerp(1e4, abs(a - at) * _Cell, lit * step(past, _Overrun));
            }

            // A line of light d metres from its middle: a crisp hot core, a soft glow round it. aa is a pixel in metres, from
            // the smooth position (d itself jumps where the nearest lit line changes, and its fwidth would light a seam there).
            half3 Glow(float d, float aa)
            {
                float halfWidth = _LineWidth * 0.5;
                half hot = 1.0 - smoothstep(halfWidth - aa, halfWidth + aa, d);
                half soft = exp(-max(d - halfWidth, 0.0) / _GlowWidth);
                return _Color.rgb * soft + _CoreColor.rgb * (hot * _CoreBoost);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 q = input.local * (_Span / _Cell);   // squares: x to his right, y his way, (0, 0) the middle of his
                float aa = max(max(fwidth(q.x), fwidth(q.y)) * _Cell, 1e-4);
                half runX, runZ;
                half3 lines = max(Glow(Lines(q.y, q.x, true, runX), aa) * runX, Glow(Lines(q.x, q.y, false, runZ), aa) * runZ);

                // The faint fill of the eight squares (not his own), brighter toward their edges.
                float2 square = round(q);
                float ring = max(abs(square.x), abs(square.y));
                float2 inSquare = abs(q - square);
                float inside = (0.5 - max(inSquare.x, inSquare.y)) * _Cell;
                half fill = _Fill * step(0.5, ring) * step(ring, 1.5) * On(square.x, square.y) * (0.35 + 0.65 * exp(-inside / _FillEdge));

                // Drawn out from his square, a bright head running with the lines' ends until they are all out.
                float far = max(abs(q.x), abs(q.y));
                half shown = 1.0 - smoothstep(_Reveal - 0.06, _Reveal, far);
                half building = 1.0 - smoothstep(1.5 + _Overrun, 1.6 + _Overrun, _Reveal);
                half head = building * exp(-abs(_Reveal - far) / _HeadSize);
                // A soft band going slowly round.
                half band = pow(0.5 + 0.5 * cos(atan2(q.y, q.x) - _Phase), 6.0) * _Shimmer;

                half3 col = lines * (1.0 + head * _HeadLight + band) + _Color.rgb * (fill * (1.0 + band));
                return half4(col * (shown * _Intensity * _Fade), 1.0);
            }
            ENDHLSL
        }
    }
}
