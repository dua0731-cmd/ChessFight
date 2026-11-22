// Plaza explainer - small helpers: easing, a seeded random, and DOM pieces whose look depends only on the time.
export const clamp = (x, a, b) => Math.min(b, Math.max(a, x));
export const lerp = (a, b, t) => a + (b - a) * t;
export const prog = (t, t0, t1) => clamp((t - t0) / (t1 - t0), 0, 1);
export const smooth = t => { t = clamp(t, 0, 1); return t * t * (3 - 2 * t); };
export const easeOut = t => { t = clamp(t, 0, 1); return 1 - Math.pow(1 - t, 3); };
export const easeIn = t => { t = clamp(t, 0, 1); return t * t * t; };
export const back = t => { t = clamp(t, 0, 1); const c = 1.7; return 1 + (c + 1) * Math.pow(t - 1, 3) + c * Math.pow(t - 1, 2); };
export const lerpAng = (a, b, t) => { const d = ((b - a) % (2 * Math.PI) + 3 * Math.PI) % (2 * Math.PI) - Math.PI; return a + d * t; };
/** 0 outside [t0, t1], 1 inside, with soft edges. */
export const win = (t, t0, t1, fi = 0.3, fo = 0.3) => (t < t0 || t > t1) ? 0 : Math.min(prog(t, t0, t0 + fi), 1 - prog(t, t1 - fo, t1));
export function rng(seed) {
  let s = (seed * 2654435761) >>> 0 || 1;
  return () => { s ^= s << 13; s >>>= 0; s ^= s >> 17; s ^= s << 5; s >>>= 0; return s / 4294967296; };
}

/** An element: el('div', 'card big', 'text', parent, {left: 10, top: 20}). Numbers in the style are pixels. */
export function el(tag, cls = '', html = '', parent = null, style = null) {
  const e = document.createElement(tag);
  if (cls) e.className = cls;
  if (html) e.innerHTML = html;
  if (style) css(e, style);
  if (parent) parent.appendChild(e);
  return e;
}
const UNITLESS = new Set(['opacity', 'zIndex', 'fontWeight', 'lineHeight', 'flex', 'flexGrow', 'order']);
export function css(e, style) {
  for (const k in style) { const v = style[k]; e.style[k] = (typeof v === 'number' && !UNITLESS.has(k)) ? v + 'px' : v; }
  return e;
}

/**
 * Shows an element from t0 (to t1 when given): it fades and slides in, and fades out. The element's own CSS
 * position is left alone; only opacity and transform are written, so call it every frame.
 * o: fi, fo (seconds), dx, dy (pixels it comes from), s (scale it comes from), pop (overshoot), keep (extra transform).
 */
export function show(e, t, t0, t1 = 1e9, o = {}) {
  const fi = o.fi ?? 0.4, fo = o.fo ?? 0.3;
  const p = prog(t, t0, t0 + fi), a = o.pop ? back(p) : easeOut(p), out = 1 - prog(t, t1 - fo, t1);
  const op = Math.min(1, p * 1.6) * out;
  e.style.opacity = op.toFixed(3);
  e.style.visibility = op <= 0.002 ? 'hidden' : 'visible';
  const dx = (o.dx ?? 0) * (1 - a), dy = (o.dy ?? 26) * (1 - a), s = lerp(o.s ?? 1, 1, a);
  e.style.transform = `${o.keep ?? ''} translate(${dx.toFixed(1)}px, ${dy.toFixed(1)}px) scale(${s.toFixed(4)})`;
  return op;
}
export const hide = e => { e.style.opacity = '0'; e.style.visibility = 'hidden'; };
/** Puts an element's centre at a screen point. */
export const at = (e, x, y) => { e.style.left = x.toFixed(1) + 'px'; e.style.top = y.toFixed(1) + 'px'; };
export const mmss = s => `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(Math.floor(s % 60)).padStart(2, '0')}`;
