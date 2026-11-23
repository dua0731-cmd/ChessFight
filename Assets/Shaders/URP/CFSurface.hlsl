// JY-gfx (2026-11-24, the graphics upgrade, target Mario Kart World: stylized forms with real surfaces): the surface
// sets, the fine detail of a real material laid over the shape and colour a shader works out. Each set is a mask
// (R roughness, G cavity, B height, A colour variation) and a tangent-space normal, made by
// ArtSource/Gfx/textures.py and handed to every shader as two texture arrays by ChessFight.Game.GameSurfaces.
// A set is laid on three planes and blended by the surface's facing, so no UVs are needed:
//   CFSurface        on the world's planes (things that stay put: the course's own shaders, CFLit.hlsl),
//   CFSurfaceObject  on the object's own planes, its rotation and scale taken out (Gfx Lit: props that may move).
// _GfxSurface (x: on, y: strength) is 0 until GameSurfaces sets it, so outside Play nothing changes.
#ifndef CF_SURFACE_INCLUDED
#define CF_SURFACE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// The order of the arrays' slices (GameSurfaces.Sets).
#define CF_SET_LIMESTONE 0
#define CF_SET_SAND 1
#define CF_SET_WOOD 2
#define CF_SET_IRON 3
#define CF_SET_BRASS 4
#define CF_SET_PAINT 5
#define CF_SET_CANVAS 6
#define CF_SET_LEAF 7
#define CF_SET_SLATE 8

TEXTURE2D_ARRAY(_GfxSetMask);
SAMPLER(sampler_GfxSetMask);
TEXTURE2D_ARRAY(_GfxSetNormal);
SAMPLER(sampler_GfxSetNormal);
float4 _GfxSurface;     // x: on (GameSurfaces), y: strength

// Metres one image of a set covers, and its typical roughness (textures.py SETS; the means of the R channel).
static const float CFSetTile[9] = { 2.4, 1.6, 1.0, 0.5, 0.3, 0.8, 0.2, 1.2, 1.0 };
static const float CFSetRough[9] = { 0.80, 0.93, 0.60, 0.50, 0.28, 0.38, 0.90, 0.50, 0.50 };

// Smooth value noise, two octaves, 0..1 (the broad patches).
float CFHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float CFNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(CFHash(i), CFHash(i + float2(1, 0)), f.x), lerp(CFHash(i + float2(0, 1)), CFHash(i + 1.0), f.x), f.y);
}

float CFMacro(float2 p)
{
    return CFNoise(p) * 0.6 + CFNoise(p * 2.7 + 17.3) * 0.4;
}

// The set's detail over a surface: the normal bent by the set's relief, the colour varied and darkened in its
// hollows, the smoothness varied about the shader's own; then broad patches. Strength scale (0 = none).
void CFSurface(float3 positionWS, int set, half scale, inout half3 normalWS, inout half3 albedo, inout half smoothness, out half hollow)
{
    hollow = 1.0;
    half strength = _GfxSurface.y * scale;
    if (_GfxSurface.x < 0.5 || strength <= 0.0) return;
    float3 p = positionWS / CFSetTile[set];
    half3 n = normalWS;
    half3 w = abs(n);
    w = w * w;
    w = w * w;
    w /= max(w.x + w.y + w.z, 1e-4);
    // Planes: across x (u along z, v along y), across y (u along x, v along z), across z (u along x, v along y).
    half4 mx = SAMPLE_TEXTURE2D_ARRAY(_GfxSetMask, sampler_GfxSetMask, p.zy, set);
    half4 my = SAMPLE_TEXTURE2D_ARRAY(_GfxSetMask, sampler_GfxSetMask, p.xz, set);
    half4 mz = SAMPLE_TEXTURE2D_ARRAY(_GfxSetMask, sampler_GfxSetMask, p.xy, set);
    half3 tx = SAMPLE_TEXTURE2D_ARRAY(_GfxSetNormal, sampler_GfxSetNormal, p.zy, set).xyz * 2.0 - 1.0;
    half3 ty = SAMPLE_TEXTURE2D_ARRAY(_GfxSetNormal, sampler_GfxSetNormal, p.xz, set).xyz * 2.0 - 1.0;
    half3 tz = SAMPLE_TEXTURE2D_ARRAY(_GfxSetNormal, sampler_GfxSetNormal, p.xy, set).xyz * 2.0 - 1.0;
    half4 mask = mx * w.x + my * w.y + mz * w.z;
    tx.xy *= strength;
    ty.xy *= strength;
    tz.xy *= strength;
    // Each plane's relief leans the surface's own normal (whiteout blend); a height field read on a plane leans a
    // face the same way from either side of it.
    half3 bent = half3(n.x * abs(tx.z), n.y + tx.y, n.z + tx.x) * w.x
               + half3(n.x + ty.x, n.y * abs(ty.z), n.z + ty.y) * w.y
               + half3(n.x + tz.x, n.y + tz.y, n.z * abs(tz.z)) * w.z;
    normalWS = normalize(bent);
    hollow = lerp(1.0, mask.g, 0.55 * saturate(strength));
    albedo *= (1.0 + (mask.a - 0.5) * 0.5 * strength) * hollow;
    smoothness = saturate(smoothness + (CFSetRough[set] - mask.r) * 0.8 * strength);

    // Broad patches: a big floor or wall a little lighter or darker, drier or glossier over ten-odd metres, so it
    // does not read as one flat colour from the game's distance (the fine detail above averages out there).
    // _GfxSurface.z: how much (GameSurfaces.Macro).
    half k = _GfxSurface.z * saturate(strength);
    if (k > 0.0)
    {
        float3 q = positionWS / 13.0;
        half m = CFMacro(q.zy) * w.x + CFMacro(q.xz + 31.7) * w.y + CFMacro(q.xy + 63.1) * w.z;
        albedo *= 1.0 + (m - 0.5) * 0.6 * k;
        smoothness = saturate(smoothness + (0.5 - m) * 0.3 * k);
    }
}

// The same on the object's own planes: its position and normal turned into the object's frame with the scale taken
// out (a metre stays a metre), so the surface moves with a thing that moves. Static-batched meshes are already in
// world space (their matrix is the identity) and get the world's planes.
void CFSurfaceObject(float3 positionWS, int set, half scale, inout half3 normalWS, inout half3 albedo, inout half smoothness, out half hollow)
{
    float4x4 m = GetObjectToWorldMatrix();
    float3 c0 = m._m00_m10_m20, c1 = m._m01_m11_m21, c2 = m._m02_m12_m22;
    c0 *= rsqrt(max(dot(c0, c0), 1e-10));
    c1 *= rsqrt(max(dot(c1, c1), 1e-10));
    c2 *= rsqrt(max(dot(c2, c2), 1e-10));
    float3 d = positionWS - m._m03_m13_m23;
    float3 p = float3(dot(d, c0), dot(d, c1), dot(d, c2));
    half3 n = half3(dot(normalWS, c0), dot(normalWS, c1), dot(normalWS, c2));
    CFSurface(p, set, scale, n, albedo, smoothness, hollow);
    normalWS = normalize(c0 * n.x + c1 * n.y + c2 * n.z);
}

#endif
