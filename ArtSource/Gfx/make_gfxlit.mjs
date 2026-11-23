// JY-gfx: makes ChessFight/Gfx Lit from the project's own URP Lit (com.unity.render-pipelines.universal 17.3, read
// from the project's package cache): the same shader, its material buffer two numbers longer (_GfxSet,
// _GfxSetScale) in every pass so the SRP batcher still takes it, and its forward pass's fragment GfxLitPassFragment
// (GfxLitForwardPass.hlsl, written by hand), which lays a surface set over the surface. Run it again when URP is
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
