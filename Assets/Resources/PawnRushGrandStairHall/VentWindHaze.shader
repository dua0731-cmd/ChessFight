// R554 (승규 11-10 "지금 있는 거에서 A를 추가해", mockup A "휘어 보이는 공기" https://claude.ai/artifact/4WuSz3WGrrB84d5ciKLGvG):
// the royal fan's blast as bent air on top of its streaks, swirl and motes (VentWindFx). No colour of its own: the wall
// and the way behind it ripple as through a heat shimmer, with a hint of white where it is thickest.
// The mesh is a box round a cone of air along the fan's +z (object space, metres: radius _Radius.x at the fan, _Radius.y
// at _Length). Its back faces are drawn (so it still works with the camera inside it) and each pixel marches its view
// ray through the cone, adding up how much air it crosses and a noise scrolling out with the wind; the opaque picture
// is then looked up shifted by that noise, more where more air is crossed and nothing at the cone's outline (no rim).
// _Amount (0..1) and _Reach (share of _Length the blast has reached) come from the vent's clock.
// URP: what is behind comes from _CameraOpaqueTexture (the render assets keep Opaque Texture on).
Shader "ChessFight/VFX/Vent Wind Haze"
{
    Properties
    {
        _Length ("Length (m)", Float) = 5.5
        _Radius ("Radius at the fan, at the end (m)", Vector) = (0.9, 1.5, 0, 0)
        _Amount ("Amount", Range(0, 1)) = 0
        _Reach ("Reach (share of the length)", Range(0, 1)) = 1
        _Bend ("Bend (share of the screen, 4 m away)", Float) = 0.022
        _Haze ("Haze: white where it is thickest", Float) = 0.05
        _Shade ("Shade where it is thickest", Float) = 0.03
        _Grain ("Ripples a metre", Float) = 1.4
        _Phase ("Ripple phase (scrolls out)", Float) = 0
        _FloorY ("Nothing below this world height", Float) = -10000
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+3" "RenderType" = "Transparent" "IgnoreProjector" = "True" "DisableBatching" = "True" }

        Pass
        {
            ZWrite Off
            Cull Front
            Blend One OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../PawnRushSkillFx/SkillNoise.cginc"

            sampler2D _CameraOpaqueTexture;
            float4 _Radius;
            float _Length, _Amount, _Reach, _Bend, _Haze, _Shade, _Grain, _Phase, _FloorY;

            struct appdata { float4 vertex : POSITION; };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 grab : TEXCOORD0;
                float3 obj : TEXCOORD1;
                float3 eye : TEXCOORD2;
                float depth : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grab = ComputeScreenPos(o.pos);
                o.obj = v.vertex.xyz;
                o.eye = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                o.depth = -UnityObjectToViewPos(v.vertex).z;
                return o;
            }

            // How much air there is at q (0..1): full on the axis, none at the cone's side, soft at the fan and at the
            // blast's front, thinning as it spreads, none under the floor.
            float Air (float3 q)
            {
                float a = q.z / _Length;
                float r = lerp(_Radius.x, _Radius.y, saturate(a));
                float d = 1 - smoothstep(0.3, 1, length(q.xy) / r);
                d *= smoothstep(0, 0.04, a) * smoothstep(_Reach, _Reach - 0.18, a) * lerp(1, 0.55, saturate(a));
                float y = mul(unity_ObjectToWorld, float4(q, 1)).y;
                return d * saturate((y - _FloorY) / 0.15);
            }

            float4 frag (v2f i) : SV_Target
            {
                if (_Amount < 0.001) discard;
                // From where the view ray enters the box (or the eye, inside it) to this back face.
                float3 dir = i.obj - i.eye;
                float3 lo = float3(-_Radius.y - 0.1, -_Radius.y - 0.1, 0), hi = float3(_Radius.y + 0.1, _Radius.y + 0.1, _Length);
                float3 inv = 1 / (dir + (abs(dir) < 1e-5) * 1e-5);
                float3 t0 = (lo - i.eye) * inv, t1 = (hi - i.eye) * inv;
                float enter = saturate(max(max(min(t0.x, t1.x), min(t0.y, t1.y)), min(t0.z, t1.z)));
                const int Steps = 14;
                float stride = (1 - enter) / Steps, metres = stride * length(dir);
                float thick = 0;
                float2 wave = 0;
                [unroll] for (int s = 0; s < Steps; s++)
                {
                    float3 q = i.eye + dir * (enter + (s + 0.5) * stride);
                    float d = Air(q) * metres;
                    if (d <= 0) continue;
                    float3 g = float3(q.x * _Grain, q.y * _Grain, q.z * _Grain * 0.55 - _Phase);
                    wave += d * float2(SkillNoise(g), SkillNoise(g + float3(7.1, -3.3, 11.7)));
                    thick += d;
                }
                if (thick < 0.002) discard;
                float body = (1 - exp(-thick * 1.1)) * _Amount;
                wave /= thick;
                // The same blast bends less the further away it is.
                float near = saturate(4 / max(i.depth, 0.5));
                float2 uv = i.grab.xy / i.grab.w + wave * _Bend * body * near;
                float3 col = tex2D(_CameraOpaqueTexture, uv).rgb;
                col *= 1 - _Shade * body;
                col = lerp(col, float3(1, 0.98, 0.95), saturate(_Haze * body * (0.6 + 0.4 * wave.x)));
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
