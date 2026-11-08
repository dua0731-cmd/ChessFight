// F ballroom mirror door glass (R508, user 11-08: "여기는 실제로 반사 되게 유리 넣고"): a live reflection.
// MiniGameMirrorGlass renders what a camera mirrored behind the glass sees into _ReflTex (screen size / 2, its near
// plane clipped to the glass so the tunnel behind the door never shows). That camera has no mirror flip, so its
// picture is the reflection turned left to right: sampled at (1 - u, v) of the screen position.
// Over it: a faint silvering tint, a soft sheen that grows at grazing angles, and the glass's own edge darkening.
// Unlit (like ChessFight/Floor Reflection), so it works in URP.
Shader "ChessFight/Mirror Glass"
{
    Properties
    {
        _ReflTex ("Mirror picture", 2D) = "black" {}
        _Tint ("Silvering tint", Color) = (0.93, 0.95, 0.97, 1)
        _Sheen ("Glass sheen colour", Color) = (1, 0.97, 0.92, 1)
        _SheenStrength ("Sheen straight on", Range(0, 1)) = 0.04
        _SheenGrazing ("Sheen at grazing angles", Range(0, 1)) = 0.22
        _Blur ("Blur (screen fraction)", Float) = 0.0008
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _ReflTex;
            float4 _Tint, _Sheen;
            float _SheenStrength, _SheenGrazing, _Blur;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screen : TEXCOORD0;
                float3 world : TEXCOORD1;
                float3 normal : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            v2f vert (float4 vertex : POSITION, float3 normal : NORMAL, float2 uv : TEXCOORD0)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.screen = ComputeScreenPos(o.pos);
                o.world = mul(unity_ObjectToWorld, vertex).xyz;
                o.normal = UnityObjectToWorldNormal(normal);
                o.uv = uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.screen.xy / i.screen.w;
                uv.x = 1 - uv.x;
                float b = _Blur;
                float3 c = tex2D(_ReflTex, uv).rgb * 0.6
                         + (tex2D(_ReflTex, uv + float2(b, 0)).rgb + tex2D(_ReflTex, uv - float2(b, 0)).rgb
                          + tex2D(_ReflTex, uv + float2(0, b)).rgb + tex2D(_ReflTex, uv - float2(0, b)).rgb) * 0.1;
                c *= _Tint.rgb;
                float3 toEye = normalize(_WorldSpaceCameraPos - i.world);
                float facing = saturate(abs(dot(toEye, normalize(i.normal))));
                float sheen = lerp(_SheenGrazing, _SheenStrength, facing);
                // a soft diagonal streak, as polished glass shows under hall light
                float streak = smoothstep(0.08, 0.0, abs(i.uv.x - i.uv.y * 0.55 - 0.25)) * 0.06;
                c = lerp(c, _Sheen.rgb, saturate(sheen + streak));
                // the bevelled edge reads a little darker
                float2 e = min(i.uv, 1 - i.uv);
                c *= lerp(0.82, 1, smoothstep(0.0, 0.035, min(e.x, e.y)));
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
