// Bloom for the skill test camera (R79): the project runs the built-in render pipeline without a post-process
// package, so this does it by hand. Light above a threshold is cut out (soft knee), blurred down a chain of
// half-size copies and back up, and added over the picture.
Shader "Hidden/ChessFight/Skill Bloom"
{
    Properties
    {
        _MainTex ("", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex, _SourceTex;
    float4 _MainTex_TexelSize;
    half4 _Filter;
    half _Intensity;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vert (appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    half3 Box (float2 uv, float delta)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-delta, delta).xxyy;
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb
                + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25h;
    }

    half3 Prefilter (half3 c)
    {
        c = min(c, 32);   // one hot pixel must not turn into a blinking blob
        half brightness = max(c.r, max(c.g, c.b));
        half soft = clamp(brightness - _Filter.y, 0, _Filter.z);
        soft = soft * soft * _Filter.w;
        half contribution = max(soft, brightness - _Filter.x) / max(brightness, 0.00001h);
        return c * contribution;
    }
    ENDCG

    SubShader
    {
        Cull Off ZTest Always ZWrite Off

        Pass   // 0: threshold and the first half size
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target { return half4(Prefilter(Box(i.uv, 1)), 1); }
            ENDCG
        }

        Pass   // 1: half size again
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target { return half4(Box(i.uv, 1), 1); }
            ENDCG
        }

        Pass   // 2: back up, added to the bigger copy
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target { return half4(Box(i.uv, 0.5), 1); }
            ENDCG
        }

        Pass   // 3: over the picture
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target
            {
                half4 c = tex2D(_SourceTex, i.uv);
                c.rgb += _Intensity * Box(i.uv, 0.5);
                return c;
            }
            ENDCG
        }
    }
}
