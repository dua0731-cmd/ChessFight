// Turns the B1 design component into two plain HTML pages (pawn / rook) with a transparent background,
// framed 512 x 512 around the badge, for a headless browser screenshot.
// With a third argument "icon" (R799): only the ring and the disc with the pieces, no crown and no ribbon,
// framed 380 x 380 - the small marks beside a mode's name, where the whole badge was too small to read.
// Usage: node flatten.js B1.dc.html <out folder> [icon]; then a headless Chrome screenshot at scale 2 with a
// transparent background (--default-background-color=00000000), window 512 x 512 (badge) or 380 x 380 (icon).
const fs = require('fs');
const path = require('path');
const [src, outDir, kind] = process.argv.slice(2);
const icon = kind === 'icon';
const html = fs.readFileSync(src, 'utf8');

let body = html.split('<x-dc>')[1].split('</x-dc>')[0];
body = body.replace(/<helmet>[\s\S]*?<\/helmet>/, '');
// The crown, the ribbon and their studs come after the pieces.
if (icon) body = body.slice(0, body.lastIndexOf('</sc-if>') + '</sc-if>'.length) + '</div>';
const size = icon ? 380 : 512, left = icon ? -90 : -24, top = icon ? -72 : 8;

const TURN = 'repeating-radial-gradient(circle at 50% 50%, rgba(255,255,255,0.08) 0px 1px, rgba(0,0,0,0.06) 1px 3px)';
const GRAIN = 'repeating-linear-gradient(91deg, rgba(0,0,0,0.16) 0px 2px, rgba(0,0,0,0) 2px 9px), repeating-linear-gradient(87deg, rgba(255,226,170,0.07) 0px 1px, rgba(0,0,0,0) 1px 5px)';
const GOLD = '#8a6424, #f6dc9a, #b8873a, #fff3cf, #9c7230, #e9c67a, #7a5520, #f1d48c, #8a6424';
const t = { bg: 'transparent', metal: TURN + ', conic-gradient(from 210deg, ' + GOLD + ')', metalFlat: '#E9C67A', metalHi: '#FFF0C4', metalLo: '#9C7230', ink: '#2A1C0C', glass: 'radial-gradient(circle at 50% 28%, #6b5236 0%, #2e2114 55%, #160f09 100%)', hair: '#E9C67A', wood: GRAIN + ', linear-gradient(180deg, #6b4526 0%, #4a2d17 55%, #3a2312 100%)', woodDark: GRAIN + ', linear-gradient(180deg, #4a2d17 0%, #2c1a0c 100%)', steel: '#EEF2F5', steelLo: '#A9B7C2', grip: '#5A3D2A' };
const w = { fill: '#FBEFD5', shade: '#D9C08A', hi: '#FFFFFF', ink: '#2A1C0C' };
const b = { fill: '#2E2622', shade: '#16110E', hi: '#7A6656', ink: '#E9C67A' };
// R801 (user: "소드 파이트는 잘 구분이 안되니 은색으로 하자"): Sword Fight's badge in silver - rim, crown, hilts,
// the black rook's trim - on a cooler smoked glass; Pawn Rush stays gold.
const SILVER = '#5a5f66, #eef1f4, #8a9099, #ffffff, #6b7079, #d4d8de, #4c5158, #e2e5ea, #5a5f66';
const ts = Object.assign({}, t, { metal: TURN + ', conic-gradient(from 210deg, ' + SILVER + ')', metalFlat: '#C9CED6', metalHi: '#FFFFFF', metalLo: '#7E848D', ink: '#1E1C1B', glass: 'radial-gradient(circle at 50% 28%, #57524e 0%, #272422 55%, #121110 100%)', hair: '#D4D8DE' });
const bs = Object.assign({}, b, { ink: '#D4D8DE' });

for (const mode of ['pawn', 'rook']) {
  const silver = mode === 'rook';
  const vals = { t: silver ? ts : t, w, b: silver ? bs : b, rook: mode === 'rook', pawn: mode === 'pawn' };
  let page = body.replace(/<sc-if value="\{\{(\w+)\}\}"[^>]*>([\s\S]*?)<\/sc-if>/g, (_, key, inner) => vals[key] ? inner : '');
  page = page.replace(/\{\{\s*([\w.]+)\s*\}\}/g, (_, p) => {
    const v = p.split('.').reduce((o, k) => o[k], vals);
    if (v === undefined) throw new Error('no value for ' + p);
    return v;
  });
  const doc = '<!doctype html><html><head><meta charset="utf-8"><style>html,body{margin:0;background:transparent;overflow:hidden}</style></head><body>'
    + '<div style="width:' + size + 'px;height:' + size + 'px;position:relative;overflow:hidden"><div style="position:absolute;left:' + left + 'px;top:' + top + 'px">'
    + page + '</div></div></body></html>';
  fs.writeFileSync(path.join(outDir, mode + (icon ? '_icon' : '') + '.html'), doc);
}
