// Pawn Rush ultimates previz - the page's entry: window.PV for the recorder, and a simple player for a person.
import { Stage } from './stage.js';
import { clipText, DISP, BODY } from './hud.js';
import { CLIPS as ULTS } from './ults.js';
import { CASTS } from './casts.js';
import { EDITS } from './edits.js';
const CLIPS = { ...ULTS, ...CASTS, ...EDITS };

const st = new Stage(document.getElementById('gl'), document.getElementById('hud'));
async function fonts() {
  const text = clipText(st);
  await Promise.all([`86px ${DISP}`, `500 21px ${BODY}`, `600 27px ${BODY}`, `700 21px ${BODY}`].map(f => document.fonts.load(f, text)));
}
const nextFrame = () => new Promise(r => requestAnimationFrame(() => r()));
window.PV = {
  ids: Object.keys(CLIPS),
  async load(id) { st.load(CLIPS[id]); await fonts(); st.render(0); await nextFrame(); return st.dur; },
  duration: () => st.dur,
  specAt: () => st.specAt ?? st.dur - 2.9,
  async render(c) { st.render(c); await nextFrame(); return true; },
};
window.PV.ready = document.fonts.ready.then(() => true);

// ?clip=pawnA plays it in the page (space: pause, arrows: seek); &t=5 holds a still.
const q = new URLSearchParams(location.search);
if (q.get('clip')) (async () => {
  await window.PV.load(q.get('clip'));
  let t = Number(q.get('t') ?? 0), playing = !q.has('t'), last = performance.now();
  addEventListener('keydown', e => { if (e.code === 'Space') playing = !playing; if (e.code === 'ArrowRight') t += 0.5; if (e.code === 'ArrowLeft') t = Math.max(0, t - 0.5); });
  const loop = now => { if (playing) t = (t + (now - last) / 1000) % st.dur; last = now; st.render(t); document.getElementById('ui').textContent = `${q.get('clip')}  ${t.toFixed(2)} / ${st.dur.toFixed(2)}`; requestAnimationFrame(loop); };
  requestAnimationFrame(loop);
})();
