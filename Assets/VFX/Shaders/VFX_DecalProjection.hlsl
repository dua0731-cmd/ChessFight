// What the projected decals share (R341, 11-04: VFX_GroundWarnDecal, VFX_GroundShadowDecal). The renderer is a box (Unity's
// cube, its far faces drawn with ZTest Always); for each pixel it covers, DecalLocal rebuilds the point the camera's depth
// texture saw there, clips it unless it lies inside the box, and returns it in the box's own space (-0.5 .. 0.5 each
// way), with a mask that keeps only surfaces facing up past `upward` (cos) and fades the box's top and bottom `edgeFade`.
// Include after Core.hlsl and DeclareDepthTexture.hlsl.
#ifndef CHESSFIGHT_VFX_DECAL_PROJECTION
#define CHESSFIGHT_VFX_DECAL_PROJECTION

// The world point the camera's depth texture saw at a screen uv.
float3 DecalSceneAt(float2 uv)
{
#if UNITY_REVERSED_Z
    float depth = SampleSceneDepth(uv);
#else
    float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, SampleSceneDepth(uv));
#endif
    return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
}

float3 DecalLocal(float4 positionCS, half upward, half edgeFade, out half mask)
{
    float2 uv = GetNormalizedScreenSpaceUV(positionCS);
    float3 world = DecalSceneAt(uv);
    float3 local = TransformWorldToObject(world);
    clip(0.5 - abs(local));

    // The surface's facing from the neighbouring pixels, each way the side nearer in depth, so a piece's outline does
    // not count as a wall.
    float2 texel = 1.0 / _ScaledScreenParams.xy;
    float3 right = DecalSceneAt(uv + float2(texel.x, 0.0)) - world, left = world - DecalSceneAt(uv - float2(texel.x, 0.0));
    float3 up = DecalSceneAt(uv + float2(0.0, texel.y)) - world, down = world - DecalSceneAt(uv - float2(0.0, texel.y));
    float3 across = dot(right, right) < dot(left, left) ? right : left;
    float3 along = dot(up, up) < dot(down, down) ? up : down;
    float3 normal = normalize(cross(along, across));
    half floorMask = smoothstep(upward - 0.1, upward + 0.1, abs(normal.y));
    half heightMask = 1.0 - smoothstep(0.5 - edgeFade, 0.5, abs(local.y));
    mask = floorMask * heightMask;
    return local;
}

#endif
