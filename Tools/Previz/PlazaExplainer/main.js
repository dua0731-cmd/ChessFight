// Plaza explainer - the page's entry: lays the script on one timeline and draws any moment of it.
// window.PV is what the recorder drives; a person can open the page and watch (?t=12 holds a still, ?scene=plaza starts there).
import { layout } from './script.js';
import { World } from './world.js';
import { build } from './scenes.js';
import { clamp, prog, el } from './kit.js';

let voice = {};
try { voice = (await (await fetch('out/timing.json', { cache: 'no-store' })).json()).voice ?? {}; } catch { console.log('no out/timing.json: line lengths are guessed (run tts.mjs)'); }
const L = layout(voice);
const world = new World(document.getElementById('gl'));
const layer = document.getElementById('layer'), $ = id => document.getElementById(id);
const defs = build(world), roots = {};
for (const s of L.scenes) { const root = roots[s.id] = el('div', 'scene', '', layer); defs[s.id].make(root, s); }

const FADE = 0.4;
function render(T) {
  T = clamp(T, 0, L.total - 1e-3);
  const s = L.scenes.find(x => T < x.start + x.dur) ?? L.scenes[L.scenes.length - 1], t = T - s.start;
  for (const id in roots) roots[id].style.display = id === s.id ? 'block' : 'none';
  world.reset();
  const ctx = { dur: s.dur, n: s.lines.length, at: i => s.lines[i].t0, end: i => s.lines[i].t1, lines: s.lines };
  const set = defs[s.id].draw(t, ctx) ?? null;
  world.render(set);
  // chrome: chapter, caption, progress, and the dip between scenes
  $('chapter').textContent = s.chapter; $('chapter').style.visibility = s.chapter ? 'visible' : 'hidden';
  $('note').style.visibility = set ? 'visible' : 'hidden';
  let cap = '', op = 0;
  s.lines.forEach((ln, i) => {
    const from = ln.t0 - 0.12, to = i + 1 < s.lines.length ? s.lines[i + 1].t0 - 0.12 : ln.t1 + 0.5;
    if (t >= from && t < to) { cap = ln.cap; op = Math.min(prog(t, from, from + 0.18), 1 - prog(t, to - 0.14, to)); }
  });
  $('cap').textContent = cap; $('cap').style.opacity = op.toFixed(3); $('cap').style.visibility = op > 0.01 ? 'visible' : 'hidden';
  $('bar').style.width = (T / L.total * 1920).toFixed(1) + 'px';
  $('fade').style.opacity = Math.max(1 - prog(t, 0, FADE), prog(t, s.dur - FADE, s.dur)).toFixed(3);
}

const nextFrame = () => new Promise(r => requestAnimationFrame(() => r()));
async function fonts() {
  const text = document.getElementById('stage').textContent + L.scenes.flatMap(s => s.lines.map(l => l.cap)).join('') + '0123456789:';
  await Promise.all(['168px "Black Han Sans"', '500 28px "IBM Plex Sans KR"', '600 34px "IBM Plex Sans KR"', '700 34px "IBM Plex Sans KR"'].map(f => document.fonts.load(f, text)));
  await document.fonts.ready;
}
window.PV = {
  total: L.total,
  scenes: L.scenes.map(s => ({ id: s.id, start: s.start, dur: s.dur, lines: s.lines.map(l => l.t0) })),
  async render(T) { render(T); await nextFrame(); return true; },
};
window.PV.ready = fonts().then(() => { render(0); return true; });

const q = new URLSearchParams(location.search);
if (!q.has('rec')) (async () => {
  await window.PV.ready;
  const first = L.scenes.find(s => s.id === q.get('scene'));
  let T = Number(q.get('t') ?? (first ? first.start : 0)), playing = !q.has('t'), last = performance.now();
  addEventListener('keydown', e => {
    if (e.code === 'Space') playing = !playing;
    if (e.code === 'ArrowRight') T += e.shiftKey ? 5 : 0.5;
    if (e.code === 'ArrowLeft') T = Math.max(0, T - (e.shiftKey ? 5 : 0.5));
  });
  const loop = now => {
    if (playing) T = (T + (now - last) / 1000) % L.total; last = now;
    render(T);
    const s = L.scenes.find(x => T < x.start + x.dur);
    $('ui').textContent = `${s?.id}  ${T.toFixed(2)} / ${L.total.toFixed(2)}   (space: pause, arrows: seek, shift+arrows: 5 s)`;
    requestAnimationFrame(loop);
  };
  requestAnimationFrame(loop);
})();
