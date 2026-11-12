// R608 (승규 님 11-12: mockup B "대형 LED 무대", "LED 전광판에 원 안에 폰 러쉬 라고 해줘", "너가 할 수 있는 가장 좋은 연출 효과
// 그래픽을 내주고 유니티에 적용 시켜줘"): the matching screen's big LED wall (LoadingStudio.Stage.cs). Everything on
// it is worked out per LED: the picture is looked up at each lamp's centre and the lamp is drawn as a round dot, so
// close up it reads as a wall of lamps and from far (or on a small window, where the dots would shimmer) as a flat
// picture of the same brightness.
//   - The wall: chess squares in indigo, brighter towards the stage, each light square breathing on its own, a slow
//     band of light, rings leaving the centre, rays turning behind the ring, each team's colour on its side.
//   - A player comes in (_Pulse): that team's colour runs in from its side to the ring.
//   - Everyone is in (_Look.y): gold runs over the squares and the rays turn gold.
//   - The round screen inside the ring (finer lamps): the mode's name from _TitleTex with a glint crossing it.
// Unlit and written with the built-in includes, so it draws in both render pipelines; colours come in linear.
Shader "ChessFight/Stage LED Wall"
{
    Properties
    {
        [NoScaleOffset] _TitleTex ("Title (white on black)", 2D) = "black" {}
        [NoScaleOffset] _ArtTex ("Picture on the wall", 2D) = "black" {}
        [HideInInspector] _Art ("Picture: half width, bottom, height, how much", Vector) = (16, -4, 14, 0)
        [HideInInspector] _ArtLook ("Picture: brightness, slow drift, team light kept", Vector) = (1.3, 0.012, 0.35, 0)
        [HideInInspector] _Ring ("Ring", Vector) = (0, 0, 3, 0)
        [HideInInspector] _Pitch ("Pitch", Vector) = (0.1, 0.05, 2.3, 0)
        [HideInInspector] _Title ("Title", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Extent ("Extent", Vector) = (23, -4, 9, 0)
        [HideInInspector] _Look ("Look", Vector) = (1, 0, 2, 0)
        [HideInInspector] _Phase ("Phase", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ColDeep ("Deep", Vector) = (0, 0, 0.02, 1)
        [HideInInspector] _ColMid ("Mid", Vector) = (0.07, 0.04, 0.6, 1)
        [HideInInspector] _ColLine ("Line", Vector) = (0.3, 0.37, 1, 1)
        [HideInInspector] _ColWhite ("White side", Vector) = (0.03, 0.4, 1, 1)
        [HideInInspector] _ColBlack ("Black side", Vector) = (1, 0.03, 0.09, 1)
        [HideInInspector] _ColGold ("Gold", Vector) = (1, 0.54, 0.07, 1)
        [HideInInspector] _ColText ("Text", Vector) = (1, 0.89, 0.66, 1)
        [HideInInspector] _ColScreen ("Screen", Vector) = (0, 0, 0.01, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _TitleTex, _ArtTex;
            float4 _Art;       // the picture shown on the wall: half width (m), bottom (from the ring's centre), height, how much of it (0: none)
            float4 _ArtLook;   // its brightness, how far it drifts (share of its size), how much of each team's side light stays over it
            float _CfStageTime;
            float4 _Ring;      // the ring's centre on the panel (x, y, metres), its radius
            float4 _Pitch;     // metres between lamps on the wall, on the round screen, the side of a chess square
            float4 _Title;     // the name's centre (x, y from the ring's centre) and half size, metres
            float4 _Extent;    // half width, bottom and top of the wall from the ring's centre
            float4 _Look;      // brightness, everyone in (0..1), the name's brightness, the newest entrance's punch (0..1)
            float4 _Phase;     // the rays' turn, the gold run's place (both grow with time, faster when everyone is in)
            float4 _Pulse[4];  // x: -1 white came in (from the left), +1 black (from the right), 0 none; y: when
            float4 _ColDeep, _ColMid, _ColLine, _ColWhite, _ColBlack, _ColGold, _ColText, _ColScreen;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 p : TEXCOORD0;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.p = vertex.xy - _Ring.xy;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // What the wall shows at a lamp `c` metres from the ring's centre.
            float3 Wall(float2 c, float t)
            {
                float cheer = _Look.y;
                float hw = _Extent.x;
                float rr = length(c);
                // light gathers low in the middle, over the stage
                float2 gp = float2(c.x / 15.0, (c.y - _Extent.y) / 8.5);
                float glow = exp(-dot(gp, gp));
                float3 col = lerp(_ColDeep.rgb, _ColMid.rgb, glow);

                // the chess squares
                float2 g = c / _Pitch.z;
                float2 q = floor(g);
                float2 e = abs(frac(g) - 0.5);
                float chk = fmod(abs(q.x + q.y), 2.0);
                float h = Hash21(q + 17.0);
                float tw = 0.5 + 0.5 * sin(t * (0.5 + h * 0.8) + h * 40.0);
                float band = frac((c.x * 0.55 + c.y) / 30.0 - t * 0.06) - 0.5;
                float sheen = exp(-band * band * 60.0);
                col *= lerp(0.42, 1.0, chk);
                col += _ColLine.rgb * chk * (0.06 + 0.16 * tw) * (0.3 + glow);
                col += _ColLine.rgb * sheen * (0.05 + 0.2 * chk);
                col += _ColLine.rgb * smoothstep(0.455, 0.5, max(e.x, e.y)) * 0.12 * (0.3 + glow);

                // rings of light leaving the ring, and rays turning behind it
                float away = smoothstep(_Ring.z, _Ring.z + 2.0, rr);
                float rip = frac(rr / 5.5 - t * 0.2) - 0.5;
                col += _ColLine.rgb * exp(-rip * rip * 90.0) * away * exp(-rr / 16.0) * 0.6;
                float ang = atan2(c.y, c.x);
                float rays = pow(0.5 + 0.5 * sin(ang * 14.0 + _Phase.x), 4.0) * exp(-rr / 10.0) * away;
                col += lerp(_ColLine.rgb * 0.22, _ColGold.rgb * 1.3, cheer) * rays;

                // each team's side
                float sw = smoothstep(4.0, hw * 0.75, -c.x), sb = smoothstep(4.0, hw * 0.75, c.x);
                float3 sides = _ColWhite.rgb * sw * 0.32 + _ColBlack.rgb * sb * 0.32;
                col += sides;

                // a picture drawn for the wall (R608: a chess scene) takes the squares' place, drifting a little so it is
                // never quite still; the teams' light stays over it, fainter
                if (_Art.w > 0.0)
                {
                    float2 uv = float2(c.x / (2.0 * _Art.x) + 0.5, (c.y - _Art.y) / _Art.z);
                    float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
                    uv = (uv - 0.5) * (1.0 - 2.0 * _ArtLook.y) + 0.5 + _ArtLook.y * float2(sin(t * 0.07), sin(t * 0.05 + 1.3));
                    float3 pic = tex2Dlod(_ArtTex, float4(uv, 0, 0)).rgb * _ArtLook.x + sides * _ArtLook.z;
                    col = lerp(col, pic, inside * _Art.w);
                }

                // a player has come in: their team's light runs in from its side to the ring
                for (int k = 0; k < 4; k++)
                {
                    float age = t - _Pulse[k].y;
                    float s = _Pulse[k].x;
                    if (age > 0.0 && age < 1.7 && s != 0.0)
                    {
                        float3 tc = s < 0.0 ? _ColWhite.rgb : _ColBlack.rgb;
                        float u = saturate(age / 0.8);
                        float front = s * lerp(hw, 2.0, 1.0 - pow(1.0 - u, 3.0));
                        float d = (c.x - front) * s;                  // above 0: behind the front
                        float lead = exp(-d * d / 1.4);
                        float trail = step(0.0, d) * exp(-d / 5.0);
                        float life = 1.0 - smoothstep(0.75, 1.7, age);
                        float flash = saturate(1.0 - age / 0.2) * step(0.0, c.x * s);
                        col += tc * ((lead * 2.2 + trail * (0.5 + 0.7 * chk)) * life + flash * 0.9);
                    }
                }

                // everyone is in: gold runs over the squares
                float run = step(0.78, frac(q.x * 0.37 + q.y * 0.61 - _Phase.y + h * 0.2));
                col += _ColGold.rgb * cheer * (run * (0.5 + 0.6 * chk) + 0.1 * glow);
                return col;
            }

            // The round screen inside the ring: dark, a board turning slowly behind the glass, and the mode's name.
            float3 Screen(float2 c, float t)
            {
                float rn = length(c) / _Ring.z;
                float3 col = lerp(_ColScreen.rgb, _ColMid.rgb * 0.5, pow(rn, 2.5));
                float a = t * 0.04;
                float sn = sin(a), cs = cos(a);
                float2 q = floor(float2(c.x * cs - c.y * sn, c.x * sn + c.y * cs) / 0.7);
                col *= 0.78 + 0.22 * fmod(abs(q.x + q.y), 2.0);
                float br = frac(rn * 0.9 - t * 0.16) - 0.5;
                col += _ColLine.rgb * exp(-br * br * 70.0) * rn * 0.16;
                col += _ColGold.rgb * _Look.y * 0.12 * rn;

                float2 uv = (c - _Title.xy) / (2.0 * max(_Title.zw, 1e-4)) + 0.5;
                float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0) * step(1e-3, _Title.z);
                float name = tex2Dlod(_TitleTex, float4(uv, 0, 0)).r * inside;
                // a glint crosses the name in under a second, then rests
                float gl = (c.x + c.y * 0.35 - (frac(t / 5.0) * 12.0 - 1.5) * _Ring.z) / 0.5;
                float3 ink = lerp(_ColGold.rgb, _ColText.rgb, saturate(uv.y * 1.1)) * _Look.z;
                ink += _ColText.rgb * (exp(-gl * gl) * 2.4 + _Look.w * 2.0);
                return lerp(col, ink, name);
            }

            half4 frag(v2f i) : SV_Target
            {
                float t = _CfStageTime;
                float2 p = i.p;
                float onScreen = step(length(p), _Ring.z);
                float pitch = lerp(_Pitch.x, _Pitch.y, onScreen);
                float2 g = p / pitch;
                float2 cell = floor(g);
                float2 f = g - cell - 0.5;
                float2 c = (cell + 0.5) * pitch;

                float3 col;
                if (onScreen > 0.5) col = Screen(c, t);
                else col = Wall(c, t);

                // the lamp: a round dot, flattened to its average where the dots are too small to draw
                float2 w = fwidth(p) / pitch;
                float small = saturate((max(w.x, w.y) - 0.25) / 0.3);
                float lamp = smoothstep(0.47, 0.28, length(f));
                float m = lerp(lerp(0.12, 1.25, lamp), 0.62, small);
                col *= 0.95 + 0.05 * Hash21(cell * 0.37 + frac(t * 0.7) * 91.0);
                return half4(col * m * _Look.x, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
