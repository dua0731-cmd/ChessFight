// The knight's fork on the floor where it lands (R179, 10-28, F5 of the fork dive; reference F_04_fork.png): a square
// plate of light under it, four lines running out along the floor from the middles of its sides two squares each way, a
// T at the end of each on to the squares either side of it, and on each of those eight squares (the knight's moves) an
// outline of light, faintly filled, all added onto the board. It lies in the object's XZ plane one object unit a square
// (VfxForkGrid scales the object to the square's size, _Cell metres), +Z the knight's way, +X its right; the squares are
// in the order of SwordFightSkills.KnightMoves. A square with no floor under it is not drawn, nor its half of the T, nor
// a line with neither of its squares; a square with an enemy on it (and the plate, with one stamped on it) has a thicker
// edge in _FoeColor and is _FoeBoost times as bright. How far the lines and the T are drawn, whether the squares are lit,
// the light of the plate, the lines, their running heads and the squares, which squares are on or have an enemy, _Cell
// and _Fade are set per renderer by VfxForkGrid.
Shader "ChessFight/VFX/Fork Grid Additive"
{
    Properties
    {
        [HDR] _Color ("Glow color (HDR)", Color) = (0.659, 0.439, 0.239, 1)
        [HDR] _CoreColor ("Core color (HDR)", Color) = (1, 0.945, 0.867, 1)
        [HDR] _FoeColor ("Enemy square edge glow (HDR)", Color) = (0.761, 0.29, 0.255, 1)
        [HDR] _FoeCoreColor ("Enemy square edge core (HDR)", Color) = (1, 0.851, 0.812, 1)
        _CoreBoost ("Core brightness over the glow", Float) = 3
        _LineWidth ("Line width (m)", Range(0.005, 0.3)) = 0.05
        _EdgeWidth ("Square edge width (m)", Range(0.005, 0.3)) = 0.05
        _FoeEdge ("Enemy square edge width (times)", Range(1, 4)) = 1.8
        _GlowWidth ("Glow reach (m)", Range(0.005, 0.5)) = 0.05
        _Fill ("Fill inside the plate and squares", Range(0, 1)) = 0.12
        _FillEdge ("Fill brighter toward the edge (m)", Range(0.01, 1)) = 0.2
        _PlateSize ("Plate side (share of a square)", Range(0.2, 1)) = 0.9
        _SquareSize ("Fork square side (share of a square)", Range(0.2, 1)) = 0.62
        _HeadSize ("Running head glow (m)", Range(0.01, 0.5)) = 0.12
        _Cell ("Square (m)", Float) = 1.25
        _ArmReveal ("Lines drawn out", Range(0, 1)) = 1
        _TeeReveal ("T drawn out", Range(0, 1)) = 1
        _SquaresLit ("Squares lit", Range(0, 1)) = 1
        _PlateLight ("Plate light", Float) = 2
        _LineLight ("Line light", Float) = 2
        _HeadLight ("Running head light", Float) = 0
        _SquareLight ("Square light", Float) = 2
        _FoeBoost ("Enemy square light (times)", Float) = 1.5
        _PlateFoe ("Enemy on the plate", Range(0, 1)) = 0
        _SquareOnA ("Squares 0-3 on", Vector) = (1, 1, 1, 1)
        _SquareOnB ("Squares 4-7 on", Vector) = (1, 1, 1, 1)
        _SquareFoeA ("Squares 0-3 enemy", Vector) = (0, 0, 0, 0)
        _SquareFoeB ("Squares 4-7 enemy", Vector) = (0, 0, 0, 0)
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
                half4 _FoeColor;
                half4 _FoeCoreColor;
                half _CoreBoost;
                float _LineWidth;
                float _EdgeWidth;
                half _FoeEdge;
                float _GlowWidth;
                half _Fill;
                float _FillEdge;
                float _PlateSize;
                float _SquareSize;
                float _HeadSize;
                float _Cell;
                float _ArmReveal;
                float _TeeReveal;
                half _SquaresLit;
                half _PlateLight;
                half _LineLight;
                half _HeadLight;
                half _SquareLight;
                half _FoeBoost;
                half _PlateFoe;
                float4 _SquareOnA;
                float4 _SquareOnB;
                float4 _SquareFoeA;
                float4 _SquareFoeB;
                half _Fade;
            CBUFFER_END

            // The eight knight moves (across to its right, along its way), as SwordFightSkills.KnightMoves.
            static const float2 Moves[8] =
            {
                float2(1, 2), float2(2, 1), float2(2, -1), float2(1, -2),
                float2(-1, -2), float2(-2, -1), float2(-2, 1), float2(-1, 2)
            };
            // The four lines out of the plate (ahead, right, behind, left) and the two squares at the ends of each one's T.
            static const float2 Ways[4] = { float2(0, 1), float2(1, 0), float2(0, -1), float2(-1, 0) };
            static const int2 WaySquares[4] = { int2(7, 0), int2(1, 2), int2(3, 4), int2(5, 6) };

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 board : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.board = input.positionOS.xz;
                return output;
            }

            // Square i's flag (0..7) out of the two halves of eight.
            half Pick(float4 low, float4 high, float i)
            {
                return dot(low, saturate(1.0 - abs(i - float4(0, 1, 2, 3)))) + dot(high, saturate(1.0 - abs(i - float4(4, 5, 6, 7))));
            }

            // Metres from the edge of the square of half side h round c, below 0 inside.
            float Box(float2 p, float2 c, float h)
            {
                float2 d = abs(p - c) - h;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
            }

            // Metres from the segment a-b.
            float Segment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                return length(pa - ba * saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6)));
            }

            // A line of light d metres from its middle, halfWidth across each way: a crisp hot core, a soft glow round it.
            half3 Glow(float d, float halfWidth, half3 glow, half3 core)
            {
                float aa = max(fwidth(d), 1e-4);
                half hot = 1.0 - smoothstep(halfWidth - aa, halfWidth + aa, d);
                half soft = exp(-max(d - halfWidth, 0.0) / _GlowWidth);
                return glow * soft + core * (hot * _CoreBoost);
            }

            // A square of half side h round c: its edge of light (thicker and in the enemy colours with foe 1) and a faint
            // fill, brighter toward the edge.
            half3 Square(float2 p, float2 c, float h, half foe)
            {
                float d = Box(p, c, h);
                float edge = _EdgeWidth * 0.5 * lerp(1.0, _FoeEdge, foe);
                half3 col = Glow(abs(d), edge, lerp(_Color.rgb, _FoeColor.rgb, foe), lerp(_CoreColor.rgb, _FoeCoreColor.rgb, foe));
                return col + _Color.rgb * (_Fill * step(d, 0.0) * (0.5 + 0.5 * exp(min(d, 0.0) / _FillEdge)));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.board * _Cell;   // metres: x to its right, y along its way
                float plate = _PlateSize * 0.5 * _Cell, square = _SquareSize * 0.5 * _Cell;
                // The bright heads run with the lines' ends, then with the T's, and go as the squares light.
                half armHead = 1.0 - step(0.999, _ArmReveal);
                half teeShown = step(1e-4, _TeeReveal);
                half teeHead = teeShown * (1.0 - step(0.999, _TeeReveal));

                // The plate it lands on.
                half3 col = Square(p, float2(0, 0), plate, _PlateFoe) * (_PlateLight * lerp(1.0, _FoeBoost, _PlateFoe));

                // The four lines out to the middle between two squares, and the T on from there to each of them.
                float dist = 1e4;
                half heads = 0.0;
                [unroll]
                for (int w = 0; w < 4; w++)
                {
                    float2 from = Ways[w] * plate, middle = Ways[w] * (2.0 * _Cell);
                    float2 head = lerp(from, middle, _ArmReveal);
                    half onA = Pick(_SquareOnA, _SquareOnB, WaySquares[w].x);
                    half onB = Pick(_SquareOnA, _SquareOnB, WaySquares[w].y);
                    half used = max(onA, onB);
                    dist = min(dist, lerp(1e4, Segment(p, from, head), used));
                    heads += used * armHead * exp(-length(p - head) / _HeadSize);
                    [unroll]
                    for (int k = 0; k < 2; k++)
                    {
                        int i = k == 0 ? WaySquares[w].x : WaySquares[w].y;
                        half on = k == 0 ? onA : onB;
                        float2 c = Moves[i] * _Cell;
                        float2 tip = middle + normalize(c - middle) * ((length(c - middle) - square) * _TeeReveal);
                        dist = min(dist, lerp(1e4, Segment(p, middle, tip), on * teeShown));
                        heads += on * teeHead * exp(-length(p - tip) / _HeadSize);
                        half foe = Pick(_SquareFoeA, _SquareFoeB, i);
                        col += Square(p, c, square, foe) * (on * _SquaresLit * _SquareLight * lerp(1.0, _FoeBoost, foe));
                    }
                }
                col += Glow(dist, _LineWidth * 0.5, _Color.rgb, _CoreColor.rgb) * _LineLight;
                col += _CoreColor.rgb * (heads * _HeadLight);
                return half4(col * _Fade, 1.0);
            }
            ENDHLSL
        }
    }
}
