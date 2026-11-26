// Plaza explainer - pieces the scenes share: labels pinned to world points, the lobby HUD drawn roughly,
// a mouse pointer, the camera that follows a character, and paths to walk along.
import { el, css, show, hide, at, prog, smooth, easeOut, lerp, clamp, win, mmss } from './kit.js';
import { THREE, padAt } from './world.js';

export const tag = (root, text, cls = '') => el('div', 'tag ' + cls, text, root);
export const bubble = (root, text, cls = '') => el('div', 'bubble ' + cls, text, root);

/** Shows a label with its bottom centre on a world point, from t0 (to t1). */
export function pin(world, e, x, y, z, t, t0, t1 = 1e9, o = {}) {
  const p = world.project(x, y, z);
  if (p.z > 1) { hide(e); return p; }
  at(e, p.x, p.y);
  show(e, t, t0, t1, Object.assign({ keep: 'translate(-50%, -100%)', dy: 16, pop: true, s: 0.55, fi: 0.35 }, o));
  return p;
}

const POINTER = '<svg width="52" height="68" viewBox="0 0 52 68"><path d="M4 3 L4 52 L16 41 L26 63 L36 58 L26 37 L43 37 Z" fill="#2b2522" stroke="#fff8ea" stroke-width="4" stroke-linejoin="round"/></svg>';
/** A mouse pointer and the ring a click leaves. */
export function pointer(root) {
  return { arrow: el('div', '', POINTER, root, { width: 52, height: 68 }), ring: el('div', 'ring', '', root, { width: 110, height: 110 }) };
}
/** Moves the pointer from (x0, y0) to (x1, y1) between t0 and t1, clicks at t1, and leaves by t2. */
export function click(p, t, t0, t1, t2, x0, y0, x1, y1) {
  const k = easeOut(prog(t, t0, t1)), x = lerp(x0, x1, k), y = lerp(y0, y1, k), down = win(t, t1, t1 + 0.18, 0.05, 0.1);
  at(p.arrow, x, y);
  show(p.arrow, t, t0, t2, { dy: 0, fi: 0.2, keep: `scale(${1 - 0.12 * down})` });
  const r = prog(t, t1, t1 + 0.45);
  at(p.ring, x1, y1);
  p.ring.style.visibility = r > 0 && r < 1 ? 'visible' : 'hidden';
  p.ring.style.opacity = (1 - r).toFixed(3);
  p.ring.style.transform = `translate(-50%, -50%) scale(${(0.3 + r * 1.1).toFixed(3)})`;
}

/** The lobby HUD, drawn roughly: profile, mode card, the start button and the chat. */
export function hud(root) {
  const h = {};
  h.profile = el('div', 'hud hud-card', '<b>JY</b> &nbsp;·&nbsp; 친구 3', root, { right: 56, top: 112 });
  h.mode = el('div', 'hud hud-card', '<span class="small">게임 모드</span><br><b>폰 러쉬</b>', root, { right: 56, bottom: 292 });
  h.btn = el('div', 'hud-btn', '게임 시작', root, { right: 56, bottom: 172 });
  h.chip = el('div', 'hud-chip', '', root, { right: 56, bottom: 180 });
  h.chat = el('div', 'hud hud-card', '<span class="small">파티 채팅</span><br>민수: 한 판 더?', root, { left: 56, bottom: 172 });
  h.all = [h.profile, h.mode, h.btn, h.chip, h.chat];
  return h;
}
/** The compact HUD of the plaza: the start button until `pressed`, then only the matching chip counting up. */
export function hudPlaza(h, t, t0, pressed, o = {}) {
  const until = o.until ?? 1e9, speed = o.speed ?? 4, from = o.from ?? 0;
  show(h.profile, t, t0, until, { dy: -14 }); show(h.chat, t, t0, until, { dy: 14 });
  show(h.mode, t, t0, Math.min(pressed + 0.25, until), { dy: 14 }); show(h.btn, t, t0, Math.min(pressed + 0.25, until), { dy: 14, keep: `scale(${1 - 0.06 * win(t, pressed - 0.05, pressed + 0.2, 0.05, 0.12)})` });
  const secs = from + Math.max(0, t - pressed) * speed;
  h.chip.innerHTML = o.text ?? `<span class="dot"></span>매칭 중 <b>${mmss(secs)}</b> &nbsp;·&nbsp; 취소`;
  show(h.chip, t, pressed + 0.15, until, { dy: 20, pop: true, s: 0.7 });
}

/** A camera: where it is (p), what it looks at (q) and its field of view. */
export const cam = (px, py, pz, qx, qy, qz, fov = 40) => ({ p: [px, py, pz], q: [qx, qy, qz], fov });
/** The camera behind a character: yaw is the way it faces. */
export function behind(x, y, z, yaw, o = {}) {
  const d = o.dist ?? 5.4, h = o.h ?? 2.5, fx = Math.sin(yaw), fz = Math.cos(yaw), side = o.side ?? 0, a = o.ahead ?? 1.6;
  return cam(x - fx * d + fz * side, y + h, z - fz * d - fx * side, x + fx * a, y + (o.lift ?? 0.9), z + fz * a, o.fov ?? 42);
}
/** Looks through camera a, or part of the way (k) to camera b. */
export function look(world, a, b = null, k = 0) {
  const m = (u, v) => (b ? lerp(u, v, k) : u);
  world.view(m(a.p[0], b?.p[0]), m(a.p[1], b?.p[1]), m(a.p[2], b?.p[2]), m(a.q[0], b?.q[0]), m(a.q[1], b?.q[1]), m(a.q[2], b?.q[2]), m(a.fov, b?.fov));
}

/** A smooth path through points [x, z]; walk(u) with u 0..1 gives where and which way. */
export function path(points) {
  const c = new THREE.CatmullRomCurve3(points.map(p => new THREE.Vector3(p[0], 0, p[1])), false, 'catmullrom', 0.5), len = c.getLength();
  return { len, at(u) { u = clamp(u, 0, 1); const p = c.getPointAt(u), g = c.getTangentAt(Math.min(u, 0.999)); return { x: p.x, z: p.z, yaw: Math.atan2(g.x, g.z) }; } };
}
/** A jump's height: 0 at both ends of [t0, t1], h in the middle. */
export const hop = (t, t0, t1, h) => { const k = prog(t, t0, t1); return (t < t0 || t > t1) ? 0 : 4 * h * k * (1 - k); };

// what the three friends of the film wear
export const LOOK = { me: { head: 'party', back: 'cape' }, f1: { head: 'top', face: 'glasses' }, f2: { head: 'flower', back: 'pack' } };
const KNIGHT = [[1, 2], [2, 1], [-1, 2], [-2, 1], [1, -2], [2, -1], [-1, -2], [-2, -1]];
/** The knight pads at one moment: `seq` is the squares stepped on so far; the squares a knight could go to next glow. */
export function padsState(world, seq, pulse, showNext = true) {
  const seen = new Set(seq.map(p => p[0] + ',' + p[1])), [ci, cj] = seq[seq.length - 1];
  for (const p of seq) world.pad(p[0], p[1], 1);
  if (showNext) for (const [di, dj] of KNIGHT) { const i = ci + di, j = cj + dj; if (i >= 0 && i < 5 && j >= 0 && j < 5 && !seen.has(i + ',' + j)) world.pad(i, j, 2, pulse); }
}
/** A piece hopping from pad to pad: returns where it is and how many pads it has reached. */
export function padHops(seq, t, t0, each = 0.9, air = 0.55) {
  const k = clamp(Math.floor((t - t0) / each), -1, seq.length - 2), reached = Math.max(0, k + (t - t0 - k * each >= air ? 1 : 0));
  if (t < t0) { const p = padAt(...seq[0]); return { x: p.x, y: p.y, z: p.z, yaw: Math.PI, reached: 0, air: 0 }; }
  const a = padAt(...seq[Math.min(k, seq.length - 1)]), b = padAt(...seq[Math.min(k + 1, seq.length - 1)]), u = clamp((t - t0 - k * each) / air, 0, 1), e = smooth(u);
  return { x: lerp(a.x, b.x, e), y: a.y + 4 * 1.0 * u * (1 - u), z: lerp(a.z, b.z, e), yaw: Math.atan2(b.x - a.x, b.z - a.z), reached: Math.min(reached, seq.length - 1), air: u > 0 && u < 1 ? 1 : 0 };
}
