// Ending summons previz - the page's entry: window.PV for the recorder, and a simple player for a person.
import { Stage } from './stage.js';
import { CLIPS as BASE } from './clips.js';
import { KING_CLIPS } from './king.js';
const CLIPS = { ...BASE, ...KING_CLIPS };
import { DISP, BODY, HUD_TEXT } from './hud.js';

const st = new Stage(document.getElementById('gl'), document.getElementById('hud'));

// The web fonts' Korean subsets load by the text they will draw: run the clip once without looking, gather every
// string the HUD draws, load those, so that no frame falls back to another font.
async function fonts() {
  const g = st.g, seen = new Set([HUD_TEXT]), fill = g.fillText;
  g.fillText = function (text, ...rest) { seen.add(String(text)); return fill.call(this, text, ...rest); };
  try { for (let t = 0; t <= st.dur; t += 0.1) st.render(t); } finally { g.fillText = fill; }
  const text = [...seen].join(' ');
  await Promise.all([`40px ${DISP}`, `500 20px ${BODY}`, `600 20px ${BODY}`, `700 20px ${BODY}`].map(f => document.fonts.load(f, text)));
}
const nextFrame = () => new Promise(r => requestAnimationFrame(() => r()));
window.PV = {
  ids: Object.keys(CLIPS),
  async load(id) { if (!CLIPS[id]) throw new Error(`no clip ${id}`); st.load(CLIPS[id]); await fonts(); st.render(0); await nextFrame(); return st.dur; },
  duration: () => st.dur,
  sheetTimes: () => (st.clip.sheet ?? Array.from({ length: 9 }, (_, i) => 0.3 + (st.clip.dur - 0.6) * i / 8)).map(t => t + st.slate),
  async render(t) { st.render(t); await nextFrame(); return true; },
};
window.PV.ready = document.fonts.ready.then(() => true);

// ?clip=hand plays it in the page (space: pause, arrows: seek); &t=5 holds a still.
const q = new URLSearchParams(location.search);
if (q.get('clip')) (async () => {
  await window.PV.load(q.get('clip'));
  let t = Number(q.get('t') ?? 0), playing = !q.has('t'), last = performance.now();
  addEventListener('keydown', e => { if (e.code === 'Space') playing = !playing; if (e.code === 'ArrowRight') t += 0.25; if (e.code === 'ArrowLeft') t = Math.max(0, t - 0.25); });
  const loop = now => { if (playing) t = (t + (now - last) / 1000) % st.dur; last = now; st.render(t); document.getElementById('ui').textContent = `${q.get('clip')}  ${t.toFixed(2)} / ${st.dur.toFixed(2)}`; requestAnimationFrame(loop); };
  requestAnimationFrame(loop);
})();
