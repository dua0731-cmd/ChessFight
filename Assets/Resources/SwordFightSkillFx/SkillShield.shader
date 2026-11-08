// The Sword Fight king's guard shield (R132, 승규 님 10-13: "한 곳만 원형이 표시가 되는데 킹 주변을 위에는 제외 하고 양 옆
// 앞 뒤 전체가 방어 쉴드 구축 된 거 마냥 해줘"): a wall standing all round him, open above, built of chessboard squares
// (gold and ivory glass in turn, an ink edge and a bright line inside it, like the skill pads), on an open cylinder 1
// across and 1 tall (uv.x round it, uv.y up; the object is scaled to the wall's radius and height).
// - _Build lays it: the squares pop in (white as they land) from the front round to the back, the bottom row first.
// - _Hit (u, v, age in s, strength) answers a blow: a white ripple running out over the wall from where it struck.
// - _Break takes squares out (from _BreakU round, or from the top down when _BreakTop is 1), _Grey drains its colour.
// Far side drawn first and dimmer, then the near side. Premultiplied alpha. Built-in render pipeline.
Shader "ChessFight/Skill Shield"
{
    Properties
    {
        _Gold ("Squares (gold)", Color) = (0.86, 0.64, 0.22, 1)
        _Ivory ("Squares (ivory)", Color) = (0.98, 0.94, 0.84, 1)
        _Ink ("Ink", Color) = (0.14, 0.14, 0.2, 1)
        [HDR] _Neon ("Bright line", Color) = (1.5, 1.2, 0.6, 1)
        _Cols ("Columns", Float) = 12
        _Rows ("Rows", Float) = 3
        _Radius ("Wall radius (m)", Float) = 0.85
        _Height ("Wall height (m)", Float) = 1.3125
        _Front ("Front (u)", Float) = 0
        _Build ("Built (0 .. 1)", Float) = 1
        _Glass ("Glass", Range(0, 1)) = 0.2
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Flash ("Flash", Range(0, 1)) = 0
        _Hit ("Hit: u, v, age (s), strength", Vector) = (0, 0.5, 9, 0)
        _Break ("Broken", Range(0, 1.2)) = 0
        _BreakU ("Break from (u)", Float) = 0
        _BreakTop ("Break from the top", Float) = 0
        _Grey ("Grey", Range(0, 1)) = 0
        _Sheen ("Sheen phase", Float) = 0
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    float4 _Gold, _Ivory, _Ink, _Neon, _Hit;
    float _Cols, _Rows, _Radius, _Height, _Front, _Build, _Glass, _Alpha, _Flash, _Break, _BreakU, _BreakTop, _Grey, _Sheen;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 n : TEXCOORD1;
        float3 toEye : TEXCOORD2;
    };

    v2f vert(appdata_base v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord.xy;
        o.n = UnityObjectToWorldNormal(v.normal);
        o.toEye = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz;
        return o;
    }

    float Hash(float2 p)
    {
        p = frac(p * float2(123.34, 456.21));
        p += dot(p, p + 45.32);
        return frac(p.x * p.y);
    }

    float EaseOutBack(float t)
    {
        t = saturate(t);
        const float c1 = 1.70158, c3 = c1 + 1;
        return 1 + c3 * pow(t - 1, 3) + c1 * pow(t - 1, 2);
    }

    // How far round the wall from u0 to u1 (0 .. 1, 1 = the other side).
    float Round(float u1, float u0) { return abs(frac(u1 - u0 + 0.5) - 0.5) * 2; }

    float4 Shade(v2f i, float back)
    {
        float2 g = float2(i.uv.x * _Cols, i.uv.y * _Rows);
        float2 cell = floor(g);
        float2 f = frac(g) - 0.5;
        float uc = (cell.x + 0.5) / _Cols;
        float row = cell.y / max(1, _Rows - 1);
        float h = Hash(cell + 7.3);

        // Laid from the front round to the back, the bottom row first; each square pops in over a fifth of the build.
        float order = Round(uc, _Front) * 0.62 + row * 0.38;
        float appear = saturate((_Build - order * 0.8) / 0.2);
        if (appear <= 0) discard;
        float pop = EaseOutBack(appear);

        // Squares going (broken, or crumbling from the top).
        float goOrder = _BreakTop > 0.5 ? (1 - row) * 0.7 + h * 0.3 : Round(uc, _BreakU) * 0.65 + h * 0.35;
        float going = saturate((_Break * 1.15 - goOrder) / 0.15);
        if (going >= 1) discard;
        pop *= 1 - going;

        // The square in its cell: the cell's own proportions (metres), a gap, rounded corners.
        float cw = 6.2831853 * _Radius / _Cols, ch = _Height / _Rows;
        float2 m = f * float2(cw, ch);
        float2 halfS = float2(cw, ch) * 0.5 * 0.9 * pop;
        float rr = min(halfS.x, halfS.y) * 0.22;
        float2 q = abs(m) - (halfS - rr);
        float sd = length(max(q, 0)) + min(max(q.x, q.y), 0) - rr;
        if (sd > 0) discard;
        float inside = -sd;

        float3 N = normalize(i.n) * (back > 0.5 ? -1 : 1);
        float3 V = normalize(i.toEye);
        float fres = pow(1 - saturate(abs(dot(N, V))), 2);

        float checker = fmod(cell.x + cell.y, 2);
        float3 fill = lerp(_Gold.rgb, _Ivory.rgb, checker);
        float ink = 1 - smoothstep(0.014, 0.022, inside);
        float neon = 1 - smoothstep(0.0, 0.014, abs(inside - 0.036));
        // A diagonal sheen sweeping round.
        float sheen = pow(saturate(sin(i.uv.x * 6.2831853 * 2 + i.uv.y * 2.4 - _Sheen) * 0.5 + 0.5), 18);

        float a = _Glass * (0.7 + 0.6 * fres);
        float3 col = fill * a;
        col += fill * 0.35 * fres;
        col = lerp(col, _Ink.rgb * 0.8, ink);
        a = lerp(a, 0.8, ink);
        col += _Neon.rgb * neon * (0.45 + 0.55 * fres) * (1 - ink);
        col += _Neon.rgb * sheen * 0.3 * (1 - ink);

        // The top edge of the wall (open above) and its foot glow a little.
        float topLine = row > 0.99 ? (1 - smoothstep(0.0, 0.05, halfS.y - m.y)) : 0;
        col += _Neon.rgb * topLine * 0.6 * (1 - ink);
        float foot = (1 - i.uv.y) * (1 - i.uv.y);
        col += _Neon.rgb * foot * 0.12;

        // White as a square lands.
        float landing = appear < 1 ? 1 - appear : 0;
        col = lerp(col, float3(1, 1, 1) * max(a, 0.6), landing * 0.85);
        a = max(a, landing * 0.6);

        // A blow: a white ripple running out over the wall from where it struck (4 m/s), the struck squares white.
        float hu = abs(frac(i.uv.x - _Hit.x + 0.5) - 0.5) * 6.2831853 * _Radius;
        float hv = (i.uv.y - _Hit.y) * _Height;
        float hd = length(float2(hu, hv));
        float wave = 1 - smoothstep(0.0, 0.14, abs(hd - _Hit.z * 4.0));
        float near = (1 - smoothstep(0.0, 0.55, hd)) * saturate(1 - _Hit.z / 0.2);
        float hitK = _Hit.w * saturate(1 - _Hit.z / 0.5);
        col += float3(1, 0.97, 0.9) * (wave * 0.9 + near) * hitK * max(a, 0.5);
        a = max(a, (wave * 0.5 + near * 0.7) * hitK);

        // Breaking squares flare as they go.
        col = lerp(col, float3(1, 1, 1) * a, going * 0.7);

        float lum = dot(col, float3(0.3, 0.55, 0.15));
        col = lerp(col, lum * float3(0.78, 0.78, 0.84), _Grey);
        col = lerp(col, float3(1, 1, 1) * max(a, 0.7), _Flash);
        a = max(a, _Flash * 0.7);

        if (back > 0.5) { col *= 0.45; a *= 0.45; }
        return float4(col, a) * _Alpha;
    }
    ENDCG

    SubShader
    {
        Tags { "Queue" = "Transparent+8" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBack
            #pragma target 3.0
            float4 fragBack(v2f i) : SV_Target { return Shade(i, 1); }
            ENDCG
        }

        Pass
        {
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragFront
            #pragma target 3.0
            float4 fragFront(v2f i) : SV_Target { return Shade(i, 0); }
            ENDCG
        }
    }
}
