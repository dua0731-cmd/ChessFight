// JY-gfx: makes ChessFight/Gfx Lit from the project's own URP Lit (com.unity.render-pipelines.universal 17.3, read
// from the project's package cache): the same shader, its material buffer two numbers longer (_GfxSet,
// _GfxSetScale) in every pass so the SRP batcher still takes it, and its forward pass's fragment GfxLitPassFragment
// (GfxLitForwardPass.hlsl, written by hand), which lays a surface set over the surface and the floors' relief
// (parallax occlusion, its sun shadow, dust in the joints) under it. Run it again when URP is
// updated:   node ArtSource/Gfx/make_gfxlit.mjs <Unity project folder> [output folder, default Assets/Shaders/URP]
import fs from 'node:fs';
import path from 'node:path';

const slash = p => p.split('\\').join('/').replace(/\/?$/, '/');
const project = slash(process.argv[2] || '.');
const cache = project + 'Library/PackageCache/';
const urp = cache + fs.readdirSync(cache).find(d => /^com\.unity\.render-pipelines\.universal@/.test(d)) + '/Shaders/';
const out = process.argv[3] ? slash(process.argv[3]) : project + 'Assets/Shaders/URP/';
const nl = s => s.split('\r\n').join('\n');
let failed = false;
function swap(text, from, to, count) {
  const found = text.split(from).length - 1;
  if (found !== count) { console.log(`FAIL found ${found} (want ${count}): ${from.slice(0, 90)}`); failed = true; }
  return text.split(from).join(to);
}

// The material buffer.
let input = nl(fs.readFileSync(urp + 'LitInput.hlsl', 'utf8'));
input = swap(input, '#ifndef UNIVERSAL_LIT_INPUT_INCLUDED\n#define UNIVERSAL_LIT_INPUT_INCLUDED\n',
  '// JY-gfx (2026-11-24): URP 17.3\'s LitInput.hlsl for ChessFight/Gfx Lit, its material buffer two numbers longer:\n' +
  '// _GfxSet (the surface set, CFSurface.hlsl; below 0 none) and _GfxSetScale. Every pass of Gfx Lit includes this\n' +
  '// file, so the buffer is the same in all of them (the SRP batcher wants that). It keeps URP\'s include guard, so\n' +
  '// URP\'s own LitInput.hlsl after it is a no-op. Made by make_gfxlit.mjs; remake it when URP is updated.\n' +
  '#ifndef UNIVERSAL_LIT_INPUT_INCLUDED\n#define UNIVERSAL_LIT_INPUT_INCLUDED\n', 1);
input = swap(input, 'half _Surface;\nUNITY_TEXTURE_STREAMING_DEBUG_VARS;\n',
  'half _Surface;\nhalf _GfxSet;\nhalf _GfxSetScale;\nUNITY_TEXTURE_STREAMING_DEBUG_VARS;\n', 1);
// The floors' relief (2026-11-24 afternoon): parallax occlusion over a twin's height map in place of URP's one-step
// parallax, the relief's own sun shadow, and dust in its joints (GameSurfaces.Relief gives the twins the maps).
input = swap(input, "void ApplyPerPixelDisplacement(half3 viewDirTS, inout float2 uv)\n{\n#if defined(_PARALLAXMAP)\n    uv += ParallaxMapping(TEXTURE2D_ARGS(_ParallaxMap, sampler_ParallaxMap), viewDirTS, _Parallax, uv);\n#endif\n}\n",
  "// JY-gfx (2026-11-24): the floors' relief. A Gfx Lit twin's _ParallaxMap is its swatch's baked height and occlusion\n// (Resources/Gfx/Height: R height, G occlusion; GameSurfaces.Relief), _Parallax the relief's depth in UV units (one UV\n// unit is the swatch's tile, 2 m for the floors). Parallax occlusion mapping (Unity's ParallaxOcclusionMapping, secant\n// refinement): more steps the flatter the view, the full-resolution derivatives kept for every sample, faded out by\n// the height map's mip level (far floors keep their normal map only). The height found at the end is kept\n// (gfxReliefHeight) for the dust in the joints.\nstatic float gfxReliefHeight = 1.0;\n// The height map's footprint at this pixel (kept for the shadow's reads) and how much of the relief is in (1 near, 0\n// past the fade).\nstatic float2 gfxReliefDx, gfxReliefDy;\nstatic float gfxReliefFade = 0.0;\n// How much of the relief's sun shadow is in (a global, GameSurfaces.ReliefShadow).\nfloat _GfxReliefShadow;\n\nstruct PerPixelHeightDisplacementParam\n{\n    float2 uv;\n    float2 dx;\n    float2 dy;\n};\n\nreal ComputePerPixelHeightDisplacement(real2 texOffsetCurrent, real lod, PerPixelHeightDisplacementParam param)\n{\n    return SAMPLE_TEXTURE2D_GRAD(_ParallaxMap, sampler_ParallaxMap, param.uv + texOffsetCurrent, param.dx, param.dy).r;\n}\n\n#include \"Packages/com.unity.render-pipelines.core/ShaderLibrary/PerPixelDisplacement.hlsl\"\n\n// Mip levels of the 1024 height maps: the relief is whole up to GFX_RELIEF_LOD_FULL and gone by one level more.\n#define GFX_RELIEF_LOD_FULL 3.5\n\nvoid ApplyPerPixelDisplacement(half3 viewDirTS, inout float2 uv)\n{\n#if defined(_PARALLAXMAP)\n    float2 dx = ddx(uv), dy = ddy(uv);\n    float lod = 0.5 * log2(max(max(dot(dx, dx), dot(dy, dy)) * 1048576.0, 1e-8));\n    float3 v = normalize((float3)viewDirTS);\n    if (lod < GFX_RELIEF_LOD_FULL + 1.0 && v.z > 0.02)\n    {\n        v.xy *= _Parallax;\n        int steps = (int)lerp(28.0, 8.0, saturate(v.z));\n        PerPixelHeightDisplacementParam p;\n        p.uv = uv;\n        p.dx = dx;\n        p.dy = dy;\n        real height;\n        uv += ParallaxOcclusionMapping(lod, GFX_RELIEF_LOD_FULL, steps, v, p, height);\n        gfxReliefFade = 1.0 - saturate(lod - GFX_RELIEF_LOD_FULL);\n        gfxReliefHeight = lerp(1.0, height, gfxReliefFade);\n        gfxReliefDx = dx;\n        gfxReliefDy = dy;\n    }\n#endif\n}\n\n// JY-gfx: the relief's shadow from the main light: from the point the view ray found, toward the light (lightTS, in\n// tangent space), whether the height map rises above the ray before the top. Soft: how far it rises, less the further\n// along. 1 lit, 0 in shadow.\n#define GFX_RELIEF_SHADOW_STEPS 8\nhalf GfxReliefShadow(float2 uv, float h, float3 lightTS)\n{\n#if defined(_PARALLAXMAP)\n    if (lightTS.z <= 0.05 || h >= 0.995) return 1.0;\n    float stepH = (1.0 - h) / GFX_RELIEF_SHADOW_STEPS;\n    float2 stepUV = lightTS.xy * _Parallax / lightTS.z * stepH;\n    float rayH = h, blocked = 0.0;\n    [unroll] for (int i = 1; i <= GFX_RELIEF_SHADOW_STEPS; i++)\n    {\n        uv += stepUV;\n        rayH += stepH;\n        float s = SAMPLE_TEXTURE2D_GRAD(_ParallaxMap, sampler_ParallaxMap, uv, gfxReliefDx, gfxReliefDy).r;\n        blocked = max(blocked, (s - rayH) * (1.0 - (float)i / (GFX_RELIEF_SHADOW_STEPS + 1)) * 10.0);\n    }\n    return 1.0 - saturate(blocked);\n#else\n    return 1.0;\n#endif\n}\n", 1);
input = swap(input, "    outSurfaceData.occlusion = SampleOcclusion(uv);\n",
  "    outSurfaceData.occlusion = SampleOcclusion(uv);\n#if defined(_PARALLAXMAP)\n    // JY-gfx: dust in the joints and the low parts of the relief (its own height): darker and rougher, no tint.\n    half low = 1.0 - smoothstep(0.05, 0.5, gfxReliefHeight);\n    outSurfaceData.albedo *= 1.0 - 0.35 * low;\n    outSurfaceData.smoothness *= 1.0 - 0.6 * low;\n#endif\n", 1);
fs.writeFileSync(out + 'GfxLitInput.hlsl', input);

// The shader.
let shader = nl(fs.readFileSync(urp + 'Lit.shader', 'utf8'));
shader = swap(shader, 'Shader "Universal Render Pipeline/Lit"\n{\n    Properties\n    {\n',
  '// JY-gfx (2026-11-24, the graphics upgrade): URP 17.3\'s Lit with a surface set over it (CFSurface.hlsl): the fine\n' +
  '// detail of a real material (stone, wood, iron, cloth...) laid on the object\'s own planes. _GfxSet picks the set\n' +
  '// (GameSurfaces.Sets order; below 0 none), _GfxSetScale how strongly it shows. ChessFight.Game.GameSurfaces gives\n' +
  '// the course\'s URP Lit materials this shader in Play. Made by make_gfxlit.mjs; remake it when URP is updated.\n' +
  'Shader "ChessFight/Gfx Lit"\n{\n    Properties\n    {\n' +
  '        // JY-gfx: the surface set (0 limestone, 1 sand, 2 wood, 3 iron, 4 brass, 5 paint, 6 canvas, 7 leaf, 8 slate).\n' +
  '        _GfxSet("Surface set", Float) = -1\n        _GfxSetScale("Surface set strength", Float) = 1\n\n', 1);
shader = swap(shader, '#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"',
  '#include "Assets/Shaders/URP/GfxLitInput.hlsl"', 9);
shader = swap(shader, '#include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"',
  '#include "Assets/Shaders/URP/GfxLitForwardPass.hlsl"', 1);
shader = swap(shader, '#pragma fragment LitPassFragment\n', '#pragma fragment GfxLitPassFragment\n', 1);
fs.writeFileSync(out + 'GfxLit.shader', shader);
console.log(failed ? 'FAILED' : 'ok', path.basename(urp.slice(0, -9)));
process.exit(failed ? 1 : 0);
