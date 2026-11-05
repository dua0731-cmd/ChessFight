import { readFile, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

// Each output is portable: all local presentation code is embedded; only pinned Three.js is remote.
const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const folder = resolve(root, 'Docs/KingRush');
const css = await readFile(resolve(folder, 'PAWN_RUSH_MINIGAME_PREVIEW.css'), 'utf8');
const script = await readFile(resolve(folder, 'PAWN_RUSH_MINIGAME_PREVIEW.js'), 'utf8');
for (const [stage, name, title] of [
  ['mid', 'PAWN_RUSH_MID_MINIGAMES_3D.html', '폰 러시 · 중반 미니게임 3D'],
  ['late', 'PAWN_RUSH_LATE_MINIGAMES_3D.html', '폰 러시 · 후반 미니게임 3D'],
]) {
  const html = `<!doctype html>\n<html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>${title}</title>\n<style>\n${css}\n</style></head>\n<body data-stage="${stage}"><main id="preview"></main>\n<script src="https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js"></script>\n<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"></script>\n<script>\n${script.replaceAll('</script', '<\\/script')}\n</script></body></html>\n`;
  await writeFile(resolve(folder, name), html, 'utf8');
  console.log(`Built ${name}`);
}
