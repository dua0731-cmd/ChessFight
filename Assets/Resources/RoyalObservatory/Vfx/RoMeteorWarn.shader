// R725 ro-vfx (user 11-20 "유성 예고 그래픽도 좋게 해줘", then "빨갛게 하지마": gold all the way): the Royal Observatory
// meteor's warning, an astrolabe face projected onto the bridge round the engraved gold ring (RoMeteorRing: a band at
// the shove's reach _Radius with 48 ticks inside it, a line at the knock-down's reach _Inner, four eight-point stars).
// It draws what the model cannot:
//   bleed      the ring's gold light spilling onto the stone either side of the band and the inner line
//   countdown  a glow under the ring's 48 ticks, lighting clockwise from the line's forward end as the meteor comes
//              (_Fill 0 .. 1 over the warning), the next one flickering on
//   fill       a gold wash rising from the middle out to the knock-down's reach with a hot leading edge
//   star chart a dotted circle of 24 stars and 12 faint spokes inside the knock-down, turning slowly
//   target     an eight-point star where it lands, turning and breathing
//   shade      a little darkness under it all (premultiplied), so the gold reads on the gold and marble floor
// When the stone starts down (_Danger 0 .. 1) it stays gold but burns hotter and blinks. A projected decal as
// VFX_ShockDecal (VFX_DecalProjection.hlsl: only surfaces facing up inside the box). Every value but the look is set
// per renderer by RoyalObservatoryVfxMeteor.
Shader "ChessFight/VFX/RO Meteor Warn Decal"
{
    Properties
    {
        [HDR] _Color ("Gold (HDR)", Color) = (1, 0.62, 0.22, 1)
        [HDR] _HotColor ("Hot gold (HDR)", Color) = (1, 0.86, 0.56, 1)
        _BleedLight ("Ring bleed light", Float) = 1.1
        _BleedReach ("Ring bleed reach (m)", Range(0.05, 1)) = 0.3
        _LineLight ("Knock-down line light", Float) = 1.6
        _TickLight ("Countdown light", Float) = 2.2
        _FillLight ("Fill light", Float) = 0.55
        _EdgeLight ("Fill edge light", Float) = 2.4
        _ChartLight ("Star chart light", Float) = 0.7
        _StarLight ("Target star light", Float) = 2.6
        _StarSize ("Target star size (m)", Range(0.1, 1)) = 0.48
        _Shade ("Shade under it", Range(0, 1)) = 0.3
        _DangerBoost ("Light as the stone comes down (times)", Float) = 1.6
        _BlinkHz ("Blinks a second as the stone comes down", Float) = 9
        _Soft ("Line softness (m)", Range(0.002, 0.2)) = 0.025
        _Upward ("Floor facing up past (cos)", Range(0, 1)) = 0.6
        _EdgeFade ("Fade at the box's top and bottom (share)", Range(0.01, 0.5)) = 0.2
        _Radius ("Shove reach (m)", Float) = 2.2
        _Inner ("Knock-down reach (m)", Float) = 1.4
        _Box ("Box side (m)", Float) = 6
        _Fill ("Fill", Range(0, 1)) = 0.5
        _Danger ("Danger", Range(0, 1)) = 0
        _Light ("Light", Range(0, 1)) = 1
        _Turn ("Turn (rad)", Float) = 0
        _Age ("Age (s)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-9" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Decal"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
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
                half4 _HotColor;
                half _BleedLight;
                float _BleedReach;
                half _LineLight;
                half _TickLight;
                half _FillLight;
                half _EdgeLight;
                half _ChartLight;
                half _StarLight;
                float _StarSize;
                half _Shade;
                half _DangerBoost;
                half _BlinkHz;
                float _Soft;
                half _Upward;
                half _EdgeFade;
                float _Radius;
                float _Inner;
                float _Box;
                half _Fill;
                half _Danger;
                half _Light;
                float _Turn;
                float _Age;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            // A line `width` wide where `offset` is 0, soft over _Soft.
            half Line(float offset, float width)
            {
                return 1.0 - smoothstep(width * 0.5, width * 0.5 + _Soft, abs(offset));
            }

            // An eight-point star of `size` (tip radius) at the origin: four long points and four short between.
            half Star(float2 p, float size)
            {
                float r = length(p), a = atan2(p.x, p.y);
                float longPoints = pow(abs(cos(2.0 * a)), 14.0);
                float shortPoints = pow(abs(cos(2.0 * a + 1.5708)), 14.0);
                float edge = size * (0.16 + 0.84 * longPoints + 0.5 * shortPoints);
                return 1.0 - smoothstep(edge - _Soft * 1.5, edge + _Soft * 1.5, r);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half surface;
                float3 local = DecalLocal(input.positionCS, _Upward, _EdgeFade, surface);
                float2 p = local.xz * _Box;
                float r = length(p);
                const float TAU = 6.2831853;
                // 0 at the line's forward end (+z), clockwise seen from above
                float around = frac(atan2(p.x, p.y) / TAU + 1.0);

                // As the stone comes down: the same gold, hotter, blinking.
                half urge = lerp(1.0, _DangerBoost * (0.65 + 0.35 * cos(_Age * _BlinkHz * TAU)), _Danger);

                // bleed: the band (its inner edge 0.16 m in) and the knock-down line spill light both ways
                half band = exp(-max(max(r - _Radius, (_Radius - 0.16) - r), 0.0) / _BleedReach);
                half lineBleed = exp(-abs(r - _Inner) / (_BleedReach * 0.6));
                half knock = Line(r - _Inner, 0.05);

                // countdown: 48 cells under the ring's ticks (between the tick roots and the band), lit as _Fill passes them
                float cell = around * 48.0, inCell = frac(cell), index = floor(cell);
                half tickBand = smoothstep(_Radius - 0.46, _Radius - 0.38, r) * (1.0 - smoothstep(_Radius - 0.18, _Radius - 0.1, r));
                half tickShape = 1.0 - smoothstep(0.22, 0.42, abs(inCell - 0.5));
                float share = (index + 0.5) / 48.0;
                half lit = step(share, _Fill);
                half next = step(_Fill, share) * step(share, _Fill + 1.0 / 48.0) * (0.5 + 0.5 * sin(_Age * 40.0));
                half ticks = tickBand * tickShape * (0.12 + 0.88 * saturate(lit + next * 0.7));

                // fill: a wash from the middle out to the knock-down's reach, its leading edge hot
                float front = _Inner * _Fill;
                half inside = 1.0 - smoothstep(front - _Soft, front + _Soft, r);
                half wash = inside * (0.25 + 0.75 * exp(-max(front - r, 0.0) / 0.3)) * step(0.001, _Fill);
                half edge = Line(r - front, 0.06) * step(0.02, front);

                // star chart: 24 dots on a circle and 12 thin spokes, turning slowly against the target
                float chartR = _Inner * 0.7;
                float chartAround = frac(around + _Age * 0.035 + _Turn / TAU);
                float dotCell = frac(chartAround * 24.0) - 0.5;
                float2 dotAt = float2(dotCell * (TAU * chartR / 24.0), r - chartR);
                half dots = 1.0 - smoothstep(0.025, 0.025 + _Soft * 1.5, length(dotAt));
                half circle = Line(r - chartR, 0.012) * 0.35;
                float spokeOff = (frac(chartAround * 12.0) - 0.5) * (TAU * r / 12.0);   // metres off the nearest spoke
                half spokes = Line(spokeOff, 0.01) * smoothstep(_StarSize * 0.9, _StarSize * 1.3, r)
                            * (1.0 - smoothstep(_Inner - 0.25, _Inner - 0.08, r)) * 0.3;
                half chart = dots + circle + spokes;

                // target: an eight-point star where it lands, turning and breathing, and its halo
                float spin = -_Age * 0.8 + _Turn;
                float2 q = float2(p.x * cos(spin) - p.y * sin(spin), p.x * sin(spin) + p.y * cos(spin));
                half breathe = 0.85 + 0.15 * sin(_Age * 6.0);
                half star = Star(q, _StarSize * breathe);
                half halo = exp(-r / (_StarSize * 0.9)) * 0.35;

                half disc = 1.0 - smoothstep(_Radius + _BleedReach, _Radius + _BleedReach * 2.5, r);
                half3 light = _HotColor.rgb * (knock * _LineLight + edge * _EdgeLight + star * _StarLight)
                            + _Color.rgb * (band * _BleedLight + lineBleed * _LineLight * 0.35 + ticks * _TickLight
                                            + wash * _FillLight + chart * _ChartLight + halo * _StarLight * 0.5);
                half amount = surface * _Light;
                return half4(light * amount * urge, _Shade * disc * amount);
            }
            ENDHLSL
        }
    }
}
