// The bishop's giant sword seen through (10-12, 승규 님: "미리보기 할 때 검이 나타나고 반투명으로"): while the bishop aims,
// the sword it will drop hangs over the spot as a pale see-through wash, more solid and lit toward its edges, a band of
// light sliding down it; once it has struck, the same look fades it away. _Color.a is how solid it is, _ScanY the world
// height of the band. The sun: _FxSun, set by the effects. Built-in render pipeline.
Shader "ChessFight/Skill Ghost"
{
    Properties
    {
        _Color ("Colour (a = how solid)", Color) = (0.86, 0.92, 1, 0.4)
        [HDR] _Edge ("Edge light", Color) = (1.25, 1.35, 1.55, 1)
        _EdgePower ("Edge light power", Range(0.5, 8)) = 2.2
        _ScanY ("Band of light (world height)", Float) = -10000
        _ScanWidth ("Band width (m)", Float) = 0.3
        _Flash ("White", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+20" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _Color, _Edge, _FxSun;
            float _EdgePower, _ScanY, _ScanWidth, _Flash;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 world : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 N = normalize(i.normal);
                float3 V = normalize(_WorldSpaceCameraPos - i.world);
                float3 L = dot(_FxSun.xyz, _FxSun.xyz) > 1e-6 ? normalize(_FxSun.xyz) : normalize(float3(0.35, 0.79, -0.5));
                float ndl = dot(N, L) * 0.5 + 0.5;
                float edge = pow(1 - saturate(abs(dot(N, V))), _EdgePower);
                float scan = _ScanWidth > 0 ? saturate(1 - abs(i.world.y - _ScanY) / _ScanWidth) : 0;
                scan *= scan;
                float3 col = _Color.rgb * lerp(0.8, 1.06, ndl) + _Edge.rgb * (edge * 0.8 + scan * 0.55);
                col = lerp(col, float3(1, 1, 1), _Flash);
                float a = saturate(_Color.a * (0.75 + 0.8 * edge + 0.6 * scan) + _Flash * 0.5);
                return float4(col, a);
            }
            ENDCG
        }
    }
}
