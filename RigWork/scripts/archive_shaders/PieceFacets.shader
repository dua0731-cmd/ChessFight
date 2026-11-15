// Test copy only (prototype A): clean low-poly facets drawn by the shader, whatever the model's own topology.
// The rest-pose normal (UV3, in the pawn root's space, y up) is snapped to a grid - _Around steps around the vertical
// axis and _Row degrees per row up from the equator, rounded so one facet faces straight ahead - and the snapped
// normal is lit instead of the smooth one: vertical strips down the body, latitude-longitude quads on the head, like a
// piece turned on a lathe at low resolution. The snapped normal goes to the lighting as a tangent-space normal against
// the rest frame (tangent = up x normal, which the mesh's own tangents also hold), so the facets ride along with
// every bone instead of swimming.
// Colours are the vertex colours the rig script painted; lighting is Standard.
Shader "ChessFight/PieceFacets"
{
    Properties
    {
        _Glossiness ("Smoothness", Range(0, 1)) = 0.2
        _DarkGlossiness ("Smoothness of the dark parts", Range(0, 1)) = 0.45
        _Around ("Facets around", Range(4, 48)) = 16
        _Row ("Facet row (degrees)", Range(4, 45)) = 15
        _Flat ("Flatness (0 smooth, 1 flat facets)", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        half _Glossiness;
        half _DarkGlossiness;
        half _Around;
        half _Row;
        half _Flat;

        struct Input
        {
            float4 color : COLOR;
            float3 restNormal;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.restNormal = v.texcoord3.xyz;
        }

        float3 RestTangent(float3 n)
        {
            float3 t = cross(float3(0, 1, 0), n);
            return dot(t, t) > 1e-6 ? normalize(t) : float3(1, 0, 0);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 n0 = normalize(IN.restNormal);
            float stepA = 6.2831853 / _Around;
            float az = round(atan2(n0.x, n0.z) / stepA) * stepA;
            float stepE = radians(_Row);
            float el = clamp(round(asin(clamp(n0.y, -1.0, 1.0)) / stepE) * stepE, -1.5707963, 1.5707963);
            float3 nq = float3(cos(el) * sin(az), sin(el), cos(el) * cos(az));
            nq = normalize(lerp(n0, nq, _Flat));
            float3 t0 = RestTangent(n0);
            float3 b0 = cross(n0, t0);
            o.Normal = normalize(float3(dot(nq, t0), dot(nq, b0), dot(nq, n0)));

            float3 painted = IN.color.rgb;
            float3 c = painted;
            #ifndef UNITY_COLORSPACE_GAMMA
            c = GammaToLinearSpace(c);
            #endif
            o.Albedo = c;
            float lum = dot(painted, float3(0.2126, 0.7152, 0.0722));
            o.Smoothness = lerp(_DarkGlossiness, _Glossiness, saturate(lum * 3.0));
            o.Metallic = 0;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
