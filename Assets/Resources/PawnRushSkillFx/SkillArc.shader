// Electric arcs (R142, 승규 님 10-15: "지금 공기 충격파 좋은데 전기+공기 충격파 같은 거였으면 좋겠어"): ribbons turned to the
// camera (SkillArc.cs), uv y across them. A white-hot thread down the middle (_Core, brighter than white so the test
// scene's bloom catches it) in a body of the bolt's own colour (the vertex colour) and a soft glow of it beyond.
// Premultiplied. Drawn after the shock's bent air (Transparent+4), so the air does not bend it.
// R143 (승규 님 10-15, a screen recording of the queen in play: "적용 된거야 난 안된거 같은데"): in play the arcs were faint
// pale threads. Their flicker and their going out faded the whole body, and a half-faded gold over the white floor is a
// pale cream; only the white core showed. Now the body is solid while the bolt lives (the vertex alpha is how much it
// covers: the arc thins out as it ends, SkillArc.cs), its own gold by the core deepening to an amber at its edge so it
// stands off the white floor and the sky; the flicker (uv x) only heats the core, from her gold when dim to white-hot
// when bright, and the glow, which now tints what is behind it a little (_GlowCover) so the bolt reads as light rather
// than a solid gold tube. (Tried at the player's distance on the white floor: a wider white core, or a body of one flat
// gold without the amber edge, went pale again.)
// Built-in render pipeline.
Shader "ChessFight/Skill Arc"
{
    Properties
    {
        [HDR] _Core ("White-hot core", Color) = (3.2, 3.0, 2.6, 1)
        _Edge ("The body's edge, times the bolt's colour (a deeper amber)", Color) = (0.82, 0.55, 0.3, 1)
        _Glow ("Soft glow beyond the body (times the bolt's colour)", Float) = 0.5
        _GlowCover ("How much the glow tints what is behind", Range(0, 1)) = 0.35
        _CoreWidth ("Core: share of the width", Range(0.02, 0.6)) = 0.18
        _Body ("Body: share of the width", Range(0.1, 1)) = 0.66
        _Cover ("How much the body hides what is behind", Range(0, 1)) = 1
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+6" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _Core, _Edge;
            float _Glow, _GlowCover, _CoreWidth, _Body, _Cover, _Fade;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // 0 on the bolt's line, 1 at the ribbon's edge.
                float d = abs(i.uv.y * 2 - 1);
                // How hot it burns this frame (the flicker) and how much of what is behind it hides.
                float heat = saturate(i.uv.x);
                float cover = i.color.a * _Fade;
                float3 tint = i.color.rgb;
                // The hot thread, the body of its colour round it, a soft glow beyond.
                float core = 1 - smoothstep(_CoreWidth * 0.6, _CoreWidth, d);
                float body = 1 - smoothstep(_Body * 0.8, _Body, d);
                float glow = exp(-d * d * 3) * (1 - body);
                float3 coreCol = lerp(min(tint * 1.3 + 0.15, 1.0), _Core.rgb, heat);
                float3 bodyCol = lerp(min(tint * 1.12, 1.0), tint * _Edge.rgb, smoothstep(_CoreWidth, _Body, d));
                float3 col = coreCol * core + bodyCol * body * (1 - core) * _Cover + tint * glow * _Glow * heat;
                float a = saturate(core + body * (1 - core) * _Cover + glow * _GlowCover * heat);
                return float4(col, a) * cover;
            }
            ENDCG
        }
    }
}
