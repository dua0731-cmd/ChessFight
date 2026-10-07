// The skill effects' one material (R79, built-in render pipeline): a shape times an HDR colour, blended as
// premultiplied alpha, so _Opacity 0 is pure light added on top and 1 hides what is behind like paint. On top:
// flowing noise (energy that moves), a dissolve that eats the shape away, a glow on the edges of a mesh
// (+_Rim, a shell of light) or soft edges (-_Rim, a beam), and a soft fade where it cuts into the scene (_Soft,
// needs the camera's depth texture).
Shader "ChessFight/Skill Glow"
{
    Properties
    {
        _MainTex ("Shape", 2D) = "white" {}
        [HDR] _Color ("Colour", Color) = (1, 1, 1, 1)
        _Opacity ("Hides what is behind (0 = light only)", Range(0, 1)) = 0
        _NoiseTex ("Noise", 2D) = "gray" {}
        _NoiseST ("Noise tiling (xy), flow a second (zw)", Vector) = (1, 1, 0, 0)
        _NoiseAmount ("Noise amount", Range(0, 1)) = 0
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _Rim ("Edge glow (+) or soft edges (-)", Range(-1, 1)) = 0
        _RimPower ("Edge power", Range(0.5, 8)) = 2
        _Soft ("Soft into the scene (m)", Range(0, 2)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull [_Cull]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NoiseTex;
            float4 _Color, _NoiseST;
            float _Opacity, _NoiseAmount, _Dissolve, _Rim, _RimPower, _Soft;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 normal : TEXCOORD1;
                float3 view : TEXCOORD2;
                float4 screen : TEXCOORD3;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv.xy, _MainTex);
                o.color = v.color;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = WorldSpaceViewDir(v.vertex);
                o.screen = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.screen.z);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 shape = tex2D(_MainTex, i.uv);
                float n = tex2D(_NoiseTex, i.uv * _NoiseST.xy + _Time.y * _NoiseST.zw).r;
                float k = shape.a * i.color.a;
                k *= lerp(1, n * 2, _NoiseAmount);
                k *= saturate((n - _Dissolve) * 5 + 1 - _Dissolve);

                float facing = abs(dot(normalize(i.normal), normalize(i.view)));
                k *= lerp(1, pow(1 - facing, _RimPower) * 2, saturate(_Rim));
                k *= lerp(1, pow(facing, _RimPower), saturate(-_Rim));

                if (_Soft > 0)
                {
                    float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screen)));
                    k *= saturate((scene - i.screen.z) / _Soft);
                }

                float3 light = _Color.rgb * shape.rgb * i.color.rgb * k;
                return float4(light, saturate(k * _Opacity * _Color.a));
            }
            ENDCG
        }
    }
}
