// The queen's shock wave (R138, 승규 님 10-14: "퀸이 스킬 쓸 때 저 폭발 같은 거 빼주고 퀸 스킬에 맞는 충격파 같은 게 펑~하고
// 연출해줘"): the air itself pushed out, not fire. What is behind it is bent: the screen is grabbed once a frame and looked
// up a little off, along the slope of a shell (a sphere cut at the floor; _Mode 0) or across a ring on the floor (_Mode 1:
// the quad's middle is the queen's feet, the ring _Radius out, _Width wide). A thin rim of her gold shows where the shell
// is seen edge-on, a hot line rides the ring's front. _Fade 0 = nothing at all (the grab looked up where it was).
// R139 (승규 님: "지금 충격파 그래픽 좋아 좀 더 충격파가 시각 적으로 나타났으면 좋겠어"): easier to see without blooming into a
// white bubble on the white floor: _Tint colours what is behind it a little where the air is thickest (it multiplies, so it
// never goes past white: a light gold on a white floor). (A colour split tried first threw rainbow bands: "무지개 띠처럼
// ㄴㄴ".)
// URP (the opaque texture stands in for the GrabPass).
Shader "ChessFight/Skill Shock"
{
    Properties
    {
        [HDR] _Rim ("Rim, seen edge-on", Color) = (1.4, 1.1, 0.55, 1)
        [HDR] _Front ("Ring front", Color) = (1.5, 1.35, 1.05, 1)
        _Tint ("Tint where the air is thickest (multiplies; a = how much)", Color) = (1, 0.8, 0.45, 0)
        _Bend ("Bend (share of the screen)", Float) = 0.025
        _Shade ("Shade: how much the thick of the air darkens what is behind", Float) = 0.12
        _RimPower ("Rim power", Float) = 3
        _Fade ("Fade", Range(0, 1)) = 1
        _Mode ("0 shell, 1 ring on the floor", Float) = 0
        _Radius ("Ring radius (m)", Float) = 1
        _Width ("Ring width (m)", Float) = 0.4
        _Size ("Ring quad size (m)", Float) = 2
        _FloorY ("Shell: cut below this height", Float) = -10000
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+4" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        // URP: what is behind comes from the camera's copy of the opaque picture (_CameraOpaqueTexture), not a GrabPass.

        Pass
        {
            ZWrite Off
            Cull [_Cull]
            Blend One OneMinusSrcAlpha
            Offset -1, -4

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _CameraOpaqueTexture;
            float4 _Rim, _Front, _Tint;
            float _Bend, _Shade, _RimPower, _Fade, _Mode, _Radius, _Width, _Size, _FloorY;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 grab : TEXCOORD0;
                float3 world : TEXCOORD1;
                float3 normal : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grab = ComputeScreenPos(o.pos);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 dir;
                float bend, shade, tint;
                float3 glow;
                if (_Mode < 0.5)
                {
                    if (i.world.y < _FloorY) discard;
                    float3 n = normalize(i.normal);
                    float3 v = normalize(_WorldSpaceCameraPos - i.world);
                    float edge = pow(1 - saturate(dot(n, v)), _RimPower);
                    float3 nv = mul((float3x3)UNITY_MATRIX_V, n);
                    dir = nv.xy;
                    bend = 0.3 + 0.7 * edge;
                    glow = _Rim.rgb * edge;
                    // Seen edge-on the air is thickest: a faint shade, as a heat shimmer darkens what is behind it.
                    shade = _Shade * edge;
                    // R139: and the queen's gold there, a little way in from the very edge too.
                    tint = saturate(pow(1 - saturate(dot(n, v)), _RimPower * 0.6) * 1.1);
                    // Soft where it meets the floor.
                    float foot = saturate((i.world.y - _FloorY) / 0.12);
                    bend *= foot;
                    glow *= foot;
                    shade *= foot;
                    tint *= foot;
                }
                else
                {
                    float2 p = (i.uv - 0.5) * _Size;   // metres on the floor from the middle
                    float r = length(p);
                    float d = (r - _Radius) / max(_Width, 1e-3);
                    float band = exp(-d * d * 2.5);
                    if (band < 0.01) discard;
                    float3 radial = mul((float3x3)unity_ObjectToWorld, float3(p.x, p.y, 0));
                    float3 rv = mul((float3x3)UNITY_MATRIX_V, radial);
                    dir = normalize(rv.xy + 1e-5);
                    // The air bunched up on the front pushes out, the thinned air behind it pulls in.
                    bend = -d * band * 1.2;
                    float front = exp(-pow((r - _Radius - _Width * 0.3) / max(_Width * 0.2, 1e-3), 2));
                    glow = _Front.rgb * front;
                    // The thick of the wave just behind its front shades the floor a little.
                    shade = _Shade * band * saturate(0.6 - d);
                    // R139: the front coloured gold, fading back through the band.
                    tint = max(front, band * saturate(0.4 - d) * 0.6);
                }
                // Bent the same way as under the GrabPass it had (whose v ran down the screen).
                #if UNITY_UV_STARTS_AT_TOP
                dir.y = -dir.y;
                #endif
                float2 uv = i.grab.xy / i.grab.w + dir * (_Bend * bend * _Fade);
                float3 col = tex2D(_CameraOpaqueTexture, uv).rgb * (1 - shade * _Fade) * lerp(float3(1, 1, 1), _Tint.rgb, saturate(tint * _Tint.a * _Fade)) + glow * _Fade;
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
