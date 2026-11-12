// R608: the polished stage deck and chessboard of the matching screen (LoadingStudio.Stage.cs), after the title
// screen's floor (Resources/Intro/FloorReflection.shader): a sheet just over the floor lays on it what a mirror
// camera under the floor sees, so the LED wall, the ring, the beams and the pawns shine back from it. The mirror
// camera has no roll, so its picture is the reflection upside down: read at (u, 1 - v). A polished floor smears what
// it mirrors along the way to the eye more than sideways, hence the taller blur. Stronger at grazing angles.
// _Hole cuts a square out of the sheet (the deck's sheet leaves the board to the board's own, weaker one).
// Unlit and written with the built-in includes, so it draws in both render pipelines.
Shader "ChessFight/Stage Floor"
{
    Properties
    {
        [NoScaleOffset] _ReflTex ("Mirror picture", 2D) = "black" {}
        [HideInInspector] _Gloss ("Strength down, grazing, blur, cover", Vector) = (0.1, 0.7, 0.003, 0.5)
        [HideInInspector] _Tint ("Tint (linear)", Vector) = (1, 1, 1, 1)
        [HideInInspector] _Hole ("Hole x, z, half side, flip", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-60" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _ReflTex;
            float4 _Gloss;   // strength looking straight down, at grazing angles, blur (share of the screen), how much it hides the floor
            float4 _Tint;    // linear
            float4 _Hole;    // world x, z of the square's centre, its half side (0: none), flip the mirror's v (0 or 1)

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screen : TEXCOORD0;
                float3 world : TEXCOORD1;
            };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.screen = ComputeScreenPos(o.pos);
                o.world = mul(unity_ObjectToWorld, vertex).xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 d = abs(i.world.xz - _Hole.xy);
                clip(max(d.x, d.y) - _Hole.z);
                float2 uv = i.screen.xy / i.screen.w;
                uv.y = lerp(1.0 - uv.y, uv.y, _Hole.w);
                float b = _Gloss.z;
                float3 c = tex2D(_ReflTex, uv).rgb * 0.26;
                c += (tex2D(_ReflTex, uv + float2(0, b * 1.4)).rgb + tex2D(_ReflTex, uv - float2(0, b * 1.4)).rgb) * 0.16;
                c += (tex2D(_ReflTex, uv + float2(0, b * 3.4)).rgb + tex2D(_ReflTex, uv - float2(0, b * 3.4)).rgb) * 0.10;
                c += (tex2D(_ReflTex, uv + float2(b, 0)).rgb + tex2D(_ReflTex, uv - float2(b, 0)).rgb) * 0.11;
                float3 toEye = normalize(_WorldSpaceCameraPos - i.world);
                float grazing = pow(1.0 - saturate(toEye.y), 4.0);
                float s = lerp(_Gloss.x, _Gloss.y, grazing);
                return half4(c * _Tint.rgb * s, s * _Gloss.w);
            }
            ENDCG
        }
    }
    Fallback Off
}
