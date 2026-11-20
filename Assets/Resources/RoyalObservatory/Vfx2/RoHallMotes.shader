// R725 ro-vfx2 (the hall, PLAN 5 "공중 먼지 · 별가루"): dust and star motes hanging in the hall's air. One mesh of motes
// seeded through a unit box (RoyalObservatoryVfxHall makes it); the vertex shader lays that box, _Box metres a side, over
// and over through the world round the camera, so each mote stays where it is in the world (drifting slowly) however
// the camera moves, and only the ones near the camera are drawn. Each is a tiny camera-facing glint:
//   dust    faint warm motes, lit cold and bright where they cross the moon shaft (RoyalObservatoryLighting's own
//           beam, _ShaftTop to _ShaftFoot - this adds no beam of its own, it only shows the motes in it)
//   stars   gold star motes twinkling, a fifth of them
// Only inside the hall (_HallCentre, _HallRadius, _HallBottom to _HallTop). Added onto what is behind it.
// Per vertex: position the mote's seed in the unit box, uv the quad's corner, uv2 (size, phase, kind 0 dust 1 star, speed).
Shader "ChessFight/VFX/RO Hall Motes"
{
    Properties
    {
        [HDR] _Color ("Dust (HDR)", Color) = (1, 0.86, 0.66, 1)
        [HDR] _StarColor ("Star motes (HDR)", Color) = (1, 0.8, 0.45, 1)
        [HDR] _MoonColor ("In the moon shaft (HDR)", Color) = (0.72, 0.84, 1, 1)
        _Light ("Light", Float) = 1
        _DustLight ("Dust out of the shaft", Float) = 0.3
        _ShaftLight ("Dust in the shaft", Float) = 2.6
        _Box ("Box (m)", Float) = 28
        _Size ("Mote size (m)", Float) = 0.03
        _Drift ("Drift (m/s)", Vector) = (0.05, 0.06, 0.03, 0)
        _HallCentre ("Hall centre (world)", Vector) = (0, 0, 64, 0)
        _HallRadius ("Hall radius (m)", Float) = 50
        _HallBottom ("Hall bottom (world y)", Float) = -18
        _HallTop ("Hall top (world y)", Float) = 60
        _ShaftTop ("Shaft top (world)", Vector) = (0, 1000, 0, 0)
        _ShaftFoot ("Shaft foot (world)", Vector) = (0, 1001, 0, 0)
        _ShaftWidth ("Shaft width top, foot (m)", Vector) = (9, 14, 0, 0)
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
                half4 _StarColor;
                half4 _MoonColor;
                half _Light;
                half _DustLight;
                half _ShaftLight;
                float _Box;
                float _Size;
                float4 _Drift;
                float4 _HallCentre;
                float _HallRadius;
                float _HallBottom;
                float _HallTop;
                float4 _ShaftTop;
                float4 _ShaftFoot;
                float4 _ShaftWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 corner : TEXCOORD0;
                float4 mote : TEXCOORD1;   // size, phase, kind, speed
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 color : TEXCOORD1;
                half star : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float time = _Time.y;
                float phase = input.mote.y * 6.2831853;
                float3 eye = _WorldSpaceCameraPos.xyz;
                // The mote's place in the world: its seed through the box, drifting and swaying a little.
                float3 seed = input.positionOS.xyz * _Box + _Drift.xyz * time * (0.6 + 0.8 * input.mote.w)
                            + float3(sin(time * 0.31 + phase), sin(time * 0.23 + phase * 1.7) * 0.6, cos(time * 0.27 + phase)) * 0.35;
                float3 p = eye + (frac((seed - eye) / _Box) - 0.5) * _Box;

                float dist = length(p - eye);
                half near = smoothstep(0.7, 2.2, dist) * (1.0 - smoothstep(_Box * 0.3, _Box * 0.48, dist));
                float r = length(p.xz - _HallCentre.xz);
                half hall = (1.0 - smoothstep(_HallRadius - 4.0, _HallRadius, r))
                          * smoothstep(_HallBottom, _HallBottom + 4.0, p.y) * (1.0 - smoothstep(_HallTop - 8.0, _HallTop, p.y));

                // In the moon shaft: between its ends and within its width there (soft at the edge).
                float3 axis = _ShaftFoot.xyz - _ShaftTop.xyz;
                float len = max(length(axis), 0.01);
                axis /= len;
                float along = dot(p - _ShaftTop.xyz, axis) / len;
                float off = length((p - _ShaftTop.xyz) - axis * along * len);
                float halfWidth = lerp(_ShaftWidth.x, _ShaftWidth.y, saturate(along)) * 0.5;
                half shaft = step(0.0, along) * step(along, 1.0) * (1.0 - smoothstep(halfWidth * 0.45, halfWidth, off))
                           * (1.0 - smoothstep(0.75, 1.0, along));

                half star = input.mote.z;
                half twinkle = 0.55 + 0.45 * sin(time * (1.4 + input.mote.w * 2.2) + phase);
                twinkle = twinkle * twinkle * twinkle;
                half3 dust = _Color.rgb * _DustLight + _MoonColor.rgb * (_ShaftLight * shaft);
                half3 stars = _StarColor.rgb * (1.6 * twinkle) + _MoonColor.rgb * (_ShaftLight * 0.5 * shaft);
                output.color = lerp(dust, stars, star) * (_Light * near * hall);

                // Never under about a pixel and a half far off (no shimmering), a star mote a little bigger; nothing drawn
                // where it is faded out.
                float size = max(_Size * input.mote.x * (1.0 + star * 0.8), dist * 0.0016) * step(0.001, near * hall);
                float3 right = UNITY_MATRIX_V[0].xyz, up = UNITY_MATRIX_V[1].xyz;
                float3 world = p + (right * (input.corner.x - 0.5) + up * (input.corner.y - 0.5)) * size;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.corner;
                output.star = star;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 c = input.uv * 2.0 - 1.0;
                float d = length(c);
                half disc = saturate(1.0 - d);
                disc *= disc;
                // a star mote: a hot core and four thin rays
                half rays = saturate(1.0 - abs(c.x) * 9.0) * saturate(1.0 - abs(c.y)) + saturate(1.0 - abs(c.y) * 9.0) * saturate(1.0 - abs(c.x));
                half a = lerp(disc, disc * 0.6 + rays * 0.5 * saturate(1.0 - d), input.star);
                return half4(input.color * a, 0.0);
            }
            ENDHLSL
        }
    }
}
