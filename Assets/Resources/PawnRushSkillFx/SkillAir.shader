// The air a piece shoves back as it springs off (R141, 승규 님 10-14: "f 키를 누르면 뒤에(엉덩이 부분)에 임펙트가 나가는데 다른
// 임펙트로 해줘(공기 임팩트)"): a burst of bent air in place of the lines drawn out of its back. The mesh is a ball (radius
// 0.5) stretched along its local +Z, the way the air goes. Each pixel looks through it at the world behind, shifted along
// the burst's way on the screen by how much air the view ray crosses there: most through its middle, nothing at its
// outline (no rim, so it reads as neither a bubble nor a glass ball, as the first refraction rings did in R139). A noise
// streaked along the burst and scrolling back with it ripples that shift and lays a faint white haze over the thick of
// it, so it looks like air rushing back; a little shade there too, as a heat shimmer darkens what is behind it.
// URP (the opaque texture stands in for the GrabPass).
Shader "ChessFight/Skill Air"
{
    Properties
    {
        _Bend ("Bend (share of the screen, 3.5 m away)", Float) = 0.03
        _Haze ("Haze: how much white where it is thickest", Float) = 0.15
        _HazeColor ("Haze colour", Color) = (0.97, 0.98, 1, 1)
        _Shade ("Shade where it is thickest", Float) = 0.08
        _Streak ("Streaks (per metre of the ball's own size)", Float) = 3
        _Phase ("Streak phase (scrolls back)", Float) = 0
        _FloorY ("Nothing below this height", Float) = -10000
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        // The object's own space is used for the ray through it: no batching (it would bake the transform away).
        Tags { "Queue" = "Transparent+4" "RenderType" = "Transparent" "IgnoreProjector" = "True" "DisableBatching" = "True" }
        // URP: what is behind comes from the camera's copy of the opaque picture (_CameraOpaqueTexture), not a GrabPass.

        Pass
        {
            ZWrite Off
            Cull Back
            Blend One OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "SkillNoise.cginc"

            sampler2D _CameraOpaqueTexture;
            float4 _HazeColor;
            float _Bend, _Haze, _Shade, _Streak, _Phase, _FloorY, _Fade;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 grab : TEXCOORD0;
                float3 obj : TEXCOORD1;
                float3 eye : TEXCOORD2;
                float3 way : TEXCOORD3;   // xy: the burst's way on the screen (view space), z: the view depth
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.grab = ComputeScreenPos(o.pos);
                o.obj = v.vertex.xyz;
                o.eye = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                float3 axis = mul((float3x3)UNITY_MATRIX_V, normalize(mul((float3x3)unity_ObjectToWorld, float3(0, 0, 1))));
                o.way = float3(axis.xy, -UnityObjectToViewPos(v.vertex).z);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // The view ray in the ball's own space, and its nearest point to the middle: how far off the middle it
                // passes says how much air it crosses (the chord through a ball, 1 through the middle, 0 at the edge).
                float3 d = normalize(i.obj - i.eye);
                float3 c = i.eye - d * dot(i.eye, d);
                float thick = sqrt(saturate(1 - dot(c, c) * 4));
                float body = thick * thick * (3 - 2 * thick);
                // Nothing under the floor (soft where it meets it).
                float3 world = mul(unity_ObjectToWorld, float4(c, 1)).xyz;
                body *= saturate((world.y - _FloorY) / 0.1);
                if (body < 0.002) discard;

                // A jet, not a ball: narrow where it leaves the body (its local -z end) and fanning out the way it goes,
                // thickest at the source and thinning as it spreads.
                float along = saturate(c.z + 0.5);
                float reachOut = 0.5 * lerp(0.4, 1, sqrt(along));
                body *= saturate(1 - length(c.xy) / reachOut) * lerp(1, 0.5, along);
                body = body * body * (3 - 2 * body);
                if (body < 0.002) discard;
                // Billows running back with it (soft and nearly round: hard streaks along it read as a curtain of rain).
                float n = SkillNoise(float3(c.z * _Streak * 0.9 - _Phase, c.x * _Streak * 1.2, c.y * _Streak * 1.2)) * 0.4 + 0.6;
                float m = SkillNoise(float3(c.z * _Streak * 1.3 - _Phase * 1.4, c.x * _Streak * 2 + 7.3, c.y * _Streak * 2 - 3.1));

                // Its way on the screen (the opaque texture runs the same way up as the view).
                float2 way = i.way.xy;
                float len = length(way);
                way = len > 1e-4 ? way / len : float2(0, 1);
                // A burst of the same size bends less the further away it is (3.5 m: as set).
                float near = saturate(3.5 / max(i.way.z, 0.5));
                float push = _Bend * body * near * _Fade;
                float2 uv = i.grab.xy / i.grab.w + way * push * (0.45 + 0.9 * n) + float2(-way.y, way.x) * push * 0.45 * m;
                float3 col = tex2D(_CameraOpaqueTexture, uv).rgb;
                col *= 1 - _Shade * body * _Fade;
                col = lerp(col, _HazeColor.rgb, saturate(_Haze * body * n * _Fade));
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
