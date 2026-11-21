// Pawn Rush ultimates previz - what is drawn over the picture: the title card, the cut-in, notation stamps,
// floating labels, the race strip, the team gauge, captions with sound notes, and the closing spec card.
import { W, H, LEN, NAME, GLYPH, clamp, prog, smooth, easeOut, easeIn, fade, rng } from './stage.js';

export const DISP = '"Black Han Sans", "Malgun Gothic", sans-serif';
export const BODY = '"IBM Plex Sans KR", "Malgun Gothic", sans-serif';
const SYM = '"Segoe UI Symbol", "Malgun Gothic", sans-serif';
const C = { ally: '#46d2ff', enemy: '#ff8a2a', gold: '#ffc94a', red: '#ff4a4a' };

function rrect(g, x, y, w, h, r) {
  g.beginPath(); g.moveTo(x + r, y); g.arcTo(x + w, y, x + w, y + h, r); g.arcTo(x + w, y + h, x, y + h, r); g.arcTo(x, y + h, x, y, r); g.arcTo(x, y, x + w, y, r); g.closePath();
}
function chip(g, x, y, text, bg, fg, size = 22) {
  g.font = `700 ${size}px ${BODY}`; const w = g.measureText(text).width + size * 1.1, h = size * 1.6;
  g.fillStyle = bg; rrect(g, x, y, w, h, h / 2); g.fill();
  g.fillStyle = fg; g.textAlign = 'left'; g.textBaseline = 'middle'; g.fillText(text, x + size * 0.55, y + h / 2 + 1);
  return w;
}
function outlined(g, text, x, y, fill, stroke, lw) {
  g.lineJoin = 'round'; g.lineWidth = lw; g.strokeStyle = stroke; g.strokeText(text, x, y); g.fillStyle = fill; g.fillText(text, x, y);
}
export function wrap(g, text, maxW) {
  const out = []; let line = '';
  for (const w of text.split(' ')) { const t = line ? line + ' ' + w : w; if (g.measureText(t).width > maxW && line) { out.push(line); line = w; } else line = t; }
  if (line) out.push(line); return out;
}

function title(g, st, c) {
  const T = st.clip, end = T.titleEnd ?? 2.7, a = fade(c, 0, end, 0.2, 0.35); if (a <= 0) return;
  g.save(); g.globalAlpha = a;
  const gr = g.createLinearGradient(0, 0, 1000, 0); gr.addColorStop(0, 'rgba(8,10,16,0.82)'); gr.addColorStop(0.65, 'rgba(8,10,16,0.45)'); gr.addColorStop(1, 'rgba(8,10,16,0)');
  g.fillStyle = gr; g.fillRect(0, 160, 1100, 360);
  g.translate((1 - easeOut(prog(c, 0, 0.4))) * -70, 0);
  const w = chip(g, 72, 196, T.chip ?? `${NAME[T.kind]} 궁극기 · ${T.opt}안`, C.gold, '#1c1500', 24);
  chip(g, 72 + w + 12, 198, T.tag, 'rgba(255,255,255,0.18)', '#ffffff', 20);
  g.textAlign = 'left'; g.textBaseline = 'alphabetic';
  g.font = `86px ${DISP}`; outlined(g, T.name, 70, 330, '#ffffff', 'rgba(0,0,0,0.6)', 8);
  g.font = `52px ${DISP}`; outlined(g, T.nota, 74, 398, C.gold, 'rgba(0,0,0,0.75)', 8);
  g.font = `500 25px ${BODY}`; g.fillStyle = '#eef3fa';
  wrap(g, T.line, 820).forEach((ln, i) => g.fillText(ln, 76, 448 + i * 34));
  g.restore();
}

function corner(g, st, c) {
  const T = st.clip, a = smooth(prog(c, (T.titleEnd ?? 2.7) - 0.2, (T.titleEnd ?? 2.7) + 0.2)); if (a <= 0) return;
  g.save(); g.globalAlpha = a;
  g.fillStyle = 'rgba(8,10,16,0.62)'; rrect(g, 18, 14, 300, 44, 12); g.fill();
  g.font = `26px ${SYM}`; g.fillStyle = '#fff'; g.textBaseline = 'middle'; g.textAlign = 'left'; g.fillText(GLYPH[T.kind], 28, 37);
  const head = T.cornerHead ?? `${NAME[T.kind]} ${T.opt}안`, tail = T.cornerTail ?? T.name;
  g.font = `700 19px ${BODY}`; g.fillStyle = C.gold; g.fillText(head, 58, 37);
  const hw = g.measureText(head + ' ').width; g.fillStyle = '#fff'; g.font = `600 19px ${BODY}`; g.fillText(tail, 58 + hw + 8, 37);
  // the team gauge
  const c0 = T.cut, full = c < c0 ? 1 : 1 - easeIn(prog(c, c0, c0 + 0.6));
  g.fillStyle = 'rgba(8,10,16,0.62)'; rrect(g, 18, 64, 300, 40, 12); g.fill();
  g.font = `600 16px ${BODY}`; g.fillStyle = '#dfe6ef'; g.fillText('팀 게이지', 30, 84);
  g.fillStyle = 'rgba(255,255,255,0.16)'; rrect(g, 108, 78, 150, 12, 6); g.fill();
  const pulse = full >= 1 ? 0.7 + 0.3 * Math.sin(c * 12) : 1;
  g.fillStyle = `rgba(255,201,74,${pulse})`; if (full > 0.01) { rrect(g, 108, 78, 150 * full, 12, 6); g.fill(); }
  g.font = `700 16px ${BODY}`; g.fillStyle = full >= 1 ? C.gold : '#9aa6b5'; g.fillText(full >= 1 ? 'Q 준비' : `${Math.round(full * 100)}%`, 266, 85);
  // the Q press
  const kp = c - c0;
  if (kp > -0.3 && kp < 0.9) {
    const k = 1 + 0.35 * Math.max(0, 1 - Math.abs(kp) * 6), al = fade(c, c0 - 0.3, c0 + 0.9, 0.1, 0.3);
    g.globalAlpha = a * al; g.save(); g.translate(352, 84); g.scale(k, k);
    g.fillStyle = kp >= 0 ? C.gold : '#f2f2f2'; rrect(g, -22, -22, 44, 44, 8); g.fill();
    g.fillStyle = '#121212'; g.font = `30px ${DISP}`; g.textAlign = 'center'; g.fillText('Q', 0, 2); g.restore();
  }
  g.restore();
}

function strip(g, st, c) {
  const a = smooth(prog(c, 0.1, 0.4)); if (a <= 0) return;
  const x0 = 430, x1 = 930, y = 36;
  g.save(); g.globalAlpha = a;
  g.fillStyle = 'rgba(8,10,16,0.58)'; rrect(g, 362, 12, 680, 50, 14); g.fill();
  g.strokeStyle = 'rgba(255,255,255,0.35)'; g.lineWidth = 3; g.beginPath(); g.moveTo(x0, y); g.lineTo(x1, y); g.stroke();
  for (let z = 0; z <= LEN; z += 40) { const x = x0 + (z / LEN) * (x1 - x0); g.fillStyle = 'rgba(255,255,255,0.35)'; g.fillRect(x - 1, y - 5, 2, 10); }
  g.font = `600 15px ${BODY}`; g.textBaseline = 'middle'; g.fillStyle = '#cfd8e3'; g.textAlign = 'right'; g.fillText('출발', x0 - 10, y);
  g.textAlign = 'left'; g.fillStyle = C.gold; g.fillText('왕관', x1 + 10, y);
  let best = { ally: -1e9, enemy: -1e9 };
  for (const ac of st.actors) {
    if (!ac.strip) continue; const n = ac.now, X = x0 + clamp(n.z / LEN, 0, 1) * (x1 - x0), Y = ac.team === 'ally' ? y - 10 : y + 10;
    best[ac.team] = Math.max(best[ac.team], n.z);
    g.beginPath(); g.arc(X, Y, 6, 0, Math.PI * 2);
    g.fillStyle = n.hide && !n.track ? 'rgba(0,0,0,0)' : (ac.team === 'ally' ? '#f3ebdd' : '#2a2421'); g.fill();
    g.lineWidth = 2.5; g.strokeStyle = ac.team === 'ally' ? C.ally : C.enemy; g.stroke();
  }
  const gap = Math.round(best.ally - best.enemy);
  g.font = `600 15px ${BODY}`; g.fillStyle = '#cfd8e3'; g.textAlign = 'left'; g.fillText('선두 차', 970, y - 9);
  g.font = `22px ${DISP}`; g.fillStyle = gap >= 0 ? C.ally : C.red; g.fillText(`${gap >= 0 ? '+' : '−'}${Math.abs(gap)} m`, 970, y + 11);
  g.restore();
}

function cutin(g, st, c) {
  const T = st.clip, c0 = T.cut, d = T.cutLen ?? 1.0; if (T.cutin === false || c < c0 || c > c0 + d) return;
  const u = (c - c0) / d;
  g.save();
  g.fillStyle = `rgba(0,0,0,${0.5 * Math.min(1, u * 8) * (1 - prog(u, 0.85, 1))})`; g.fillRect(0, 0, W, H);
  const inK = easeOut(prog(u, 0, 0.16)), outK = easeIn(prog(u, 0.84, 1));
  g.translate((1 - inK) * -W * 1.1 + outK * W * 1.1, 0);
  const band = () => { g.beginPath(); g.moveTo(-60, 250); g.lineTo(W + 60, 206); g.lineTo(W + 60, 456); g.lineTo(-60, 500); g.closePath(); };
  const gr = g.createLinearGradient(0, 220, 0, 480); gr.addColorStop(0, '#1592c9'); gr.addColorStop(0.5, '#c6f2ff'); gr.addColorStop(1, '#1592c9');
  band(); g.fillStyle = gr; g.fill();
  g.save(); band(); g.clip();
  for (let i = 0; i < 46; i++) { const yy = 200 + ((i * 97) % 300), len = 120 + (i * 53) % 260, x = ((i * 311 + c * 2400) % (W + 600)) - 300;
    g.fillStyle = `rgba(255,255,255,${0.18 + (i % 4) * 0.07})`; g.fillRect(W - x, yy, len, 3 + (i % 3)); }
  g.font = `270px ${SYM}`; g.textAlign = 'center'; g.textBaseline = 'alphabetic';
  outlined(g, GLYPH[T.kind], 230, 470, '#ffffff', '#0b2a3a', 10);
  g.restore();
  g.strokeStyle = C.gold; g.lineWidth = 6; g.beginPath(); g.moveTo(-60, 250); g.lineTo(W + 60, 206); g.stroke(); g.beginPath(); g.moveTo(-60, 500); g.lineTo(W + 60, 456); g.stroke();
  g.textAlign = 'left';
  g.font = `700 28px ${BODY}`; g.fillStyle = '#0b2233'; g.fillText(`${NAME[T.kind]}의 궁극기`, 420, 296);
  g.font = `96px ${DISP}`; outlined(g, T.name, 416, 392, '#08131f', 'rgba(255,255,255,0.9)', 6);
  g.font = `64px ${DISP}`; outlined(g, T.nota, 420, 458, C.gold, '#1a1206', 10);
  g.restore();
  if (u < 0.07) { g.fillStyle = `rgba(255,255,255,${0.85 * (1 - u / 0.07)})`; g.fillRect(0, 0, W, H); }
}

function stamps(g, st, c) {
  for (const q of st.stamps) {
    if (c < q.c0 || c > q.c0 + q.d) continue;
    const u = c - q.c0, sc = 1 + 1.3 * (1 - easeOut(prog(u, 0, 0.16))), a = fade(c, q.c0, q.c0 + q.d, 0.04, 0.3);
    g.save(); g.globalAlpha = a; g.translate(q.x ?? W / 2, q.y ?? 175); g.rotate((q.rot ?? -4) * Math.PI / 180); g.scale(sc, sc);
    g.font = `${q.size ?? 110}px ${DISP}, "Segoe UI Symbol"`; g.textAlign = 'center'; g.textBaseline = 'middle';
    outlined(g, q.text, 0, 0, q.color ?? C.gold, 'rgba(12,8,4,0.95)', 18);
    g.lineWidth = 3; g.strokeStyle = 'rgba(255,246,216,0.9)'; g.strokeText(q.text, 0, 0); g.fillText(q.text, 0, 0);
    if (q.sub) { g.font = `700 30px ${BODY}`; outlined(g, q.sub, 0, (q.size ?? 110) * 0.66, '#ffffff', 'rgba(0,0,0,0.85)', 7); }
    g.restore();
  }
}

function labels(g, st, c, s) {
  for (const q of st.labels) {
    if (s < q.s0 || s > q.s1) continue;
    const p = st.project(typeof q.pos === 'function' ? q.pos(s) : q.pos); if (!p) continue;
    const a = fade(s, q.s0, q.s1, 0.05, 0.2), rise = (q.rise ?? 18) * prog(s, q.s0, q.s1), pop = 1 + 0.4 * (1 - easeOut(prog(s, q.s0, q.s0 + 0.12)));
    g.save(); g.globalAlpha = a; g.translate(p.x, p.y - 26 - rise); g.scale(pop, pop);
    g.font = `${q.size ?? 26}px ${DISP}`; g.textAlign = 'center'; g.textBaseline = 'middle';
    if (q.badge) { const r = (q.size ?? 26) * 0.95; g.beginPath(); g.arc(0, 0, r, 0, Math.PI * 2); g.fillStyle = q.bg; g.fill(); g.lineWidth = 3; g.strokeStyle = 'rgba(255,255,255,0.9)'; g.stroke(); g.fillStyle = '#ffffff'; g.fillText(q.text, 0, 2); g.restore(); continue; }
    const w = g.measureText(q.text).width + 22, h = (q.size ?? 26) + 12;
    g.fillStyle = q.bg ?? 'rgba(10,10,14,0.72)'; rrect(g, -w / 2, -h / 2, w, h, h / 2); g.fill();
    g.fillStyle = q.color ?? '#ffffff'; g.fillText(q.text, 0, 1);
    g.restore();
  }
}

function captions(g, st, c) {
  for (const q of st.caps) {
    if (c < q.c0 || c > q.c1) continue;
    const a = fade(c, q.c0, q.c1, 0.15, 0.2);
    g.save(); g.globalAlpha = a; g.textAlign = 'center'; g.textBaseline = 'middle';
    g.font = `600 27px ${BODY}`; const tw = g.measureText(q.text).width;
    g.font = `500 21px ${BODY}`; const mw = q.memo ? g.measureText('♪ ' + q.memo).width : 0;
    const w = Math.max(tw, mw) + 52, h = q.memo ? 86 : 54, x = (W - w) / 2, y = H - 24 - h;
    g.fillStyle = 'rgba(8,10,16,0.76)'; rrect(g, x, y, w, h, 14); g.fill();
    g.font = `600 27px ${BODY}`; g.fillStyle = '#ffffff'; g.fillText(q.text, W / 2, y + 28);
    if (q.memo) { g.font = `500 21px ${BODY}`; g.fillStyle = '#ffd76a'; g.fillText('♪ ' + q.memo, W / 2, y + 63); }
    g.restore();
  }
}

function spec(g, st, c) {
  const T = st.clip, c0 = st.specAt ?? st.dur - 2.9; if (c < c0) return;
  const a = smooth(prog(c, c0, c0 + 0.3));
  g.save(); g.globalAlpha = a;
  g.fillStyle = 'rgba(0,0,0,0.42)'; g.fillRect(0, 0, W, H);
  g.font = `500 21px ${BODY}`;
  const rows = T.spec.map(([k, v]) => [k, wrap(g, v, 830)]);
  const h = 110 + rows.reduce((n, r) => n + r[1].length * 31 + 14, 0), x = 120, y = Math.max(70, (H - h) / 2 + 20), w = W - 240;
  g.fillStyle = 'rgba(10,13,20,0.9)'; rrect(g, x, y, w, h, 18); g.fill();
  g.strokeStyle = 'rgba(255,201,74,0.6)'; g.lineWidth = 2; rrect(g, x, y, w, h, 18); g.stroke();
  g.textAlign = 'left'; g.textBaseline = 'alphabetic';
  g.font = `40px ${SYM}`; g.fillStyle = '#fff'; g.fillText(GLYPH[T.kind], x + 30, y + 62);
  g.font = `44px ${DISP}`; g.fillStyle = '#ffffff'; g.fillText(T.name, x + 84, y + 62);
  const nw = g.measureText(T.name).width;
  g.font = `34px ${DISP}`; g.fillStyle = C.gold; g.fillText(T.nota, x + 100 + nw, y + 62);
  chip(g, x + w - 230, y + 30, `${NAME[T.kind]} ${T.opt}안 · ${T.tag}`, 'rgba(255,255,255,0.12)', '#e6edf5', 17);
  let yy = y + 112;
  for (const [k, lines] of rows) {
    g.font = `700 21px ${BODY}`; g.fillStyle = C.gold; g.fillText(k, x + 36, yy);
    g.font = `500 21px ${BODY}`; g.fillStyle = '#eef3fa'; lines.forEach((ln, i) => g.fillText(ln, x + 150, yy + i * 31));
    yy += lines.length * 31 + 14;
  }
  g.restore();
}

/** The engine's evaluation bar, from the two teams' average progress; it jolts when the move lands (st.evalKick). */
function evalBar(g, st, c, s) {
  if (!st.evalOn) return;
  const a = smooth(prog(c, 0.2, 0.5)); if (a <= 0) return;
  const sum = { ally: 0, enemy: 0 }, n = { ally: 0, enemy: 0 };
  for (const ac of st.actors) if (ac.strip) { sum[ac.team] += ac.now.z; n[ac.team]++; }
  let e = clamp((sum.ally / Math.max(1, n.ally) - sum.enemy / Math.max(1, n.enemy)) / 4, -9.9, 9.9);
  if (st.evalKick !== null && s > st.evalKick) e += 2.6 * Math.sin((s - st.evalKick) * 8) * Math.exp(-(s - st.evalKick) * 2.6);
  const f = 0.5 + 0.5 * Math.tanh(e / 3.5), x = 1222, y = 130, w = 34, h = 450;
  g.save(); g.globalAlpha = a;
  g.fillStyle = 'rgba(8,10,16,0.62)'; rrect(g, x - 10, y - 40, w + 20, h + 58, 12); g.fill();
  g.font = `600 14px ${BODY}`; g.fillStyle = '#cfd8e3'; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText('평가', x + w / 2, y - 22);
  g.fillStyle = '#26221f'; g.fillRect(x, y, w, h); g.fillStyle = '#f4efe6'; g.fillRect(x, y + h * (1 - f), w, h * f);
  g.fillStyle = 'rgba(255,201,74,0.9)'; g.fillRect(x - 3, y + h / 2 - 1, w + 6, 2);
  const txt = (e >= 0 ? '+' : '−') + Math.abs(e).toFixed(1);
  g.font = `16px ${DISP}`; g.fillStyle = e >= 0 ? '#1d1a17' : '#f4efe6'; g.fillText(txt, x + w / 2, e >= 0 ? y + h - 14 : y + 14);
  g.restore();
}
/** Glass cracks over the whole screen (the colour coming back). */
function cracks(g, st, c) {
  for (const q of st.cracks) {
    if (c < q.c0 || c > q.c0 + q.d) continue;
    const u = (c - q.c0) / q.d, r = rng(17);
    g.save(); g.globalAlpha = 1 - u; g.lineJoin = 'round';
    for (let k = 0; k < 18; k++) {
      let x = W / 2, y = H / 2, ang = (k / 18) * Math.PI * 2 + r() * 0.3; g.beginPath(); g.moveTo(x, y);
      for (let j = 0; j < 8; j++) { ang += (r() - 0.5) * 0.6; const L = 40 + r() * 80; x += Math.cos(ang) * L; y += Math.sin(ang) * L; g.lineTo(x, y); }
      g.lineWidth = 6; g.strokeStyle = 'rgba(0,0,0,0.35)'; g.stroke(); g.lineWidth = 2.5; g.strokeStyle = 'rgba(255,255,255,0.95)'; g.stroke();
    }
    for (const rad of [90, 210, 360]) {
      g.beginPath();
      for (let k = 0; k <= 24; k++) { const an = k / 24 * Math.PI * 2, rr = rad * (0.85 + r() * 0.3); g.lineTo(W / 2 + Math.cos(an) * rr, H / 2 + Math.sin(an) * rr); }
      g.lineWidth = 2; g.strokeStyle = 'rgba(255,255,255,0.7)'; g.stroke();
    }
    g.restore();
    if (u < 0.12) { g.fillStyle = `rgba(255,255,255,${0.8 * (1 - u / 0.12)})`; g.fillRect(0, 0, W, H); }
  }
}

export function drawHud(st, c, s) {
  const g = st.g; g.clearRect(0, 0, W, H);
  labels(g, st, c, s); stamps(g, st, c);
  strip(g, st, c); corner(g, st, c); evalBar(g, st, c, s); captions(g, st, c); cracks(g, st, c);
  title(g, st, c); cutin(g, st, c); spec(g, st, c);
}

/** Every string a clip will draw, for loading the web fonts' Korean subsets before the first frame. */
export function clipText(st) {
  const T = st.clip;
  return [T.name, T.nota, T.line, T.tag, NAME[T.kind], '궁극기 안 의 팀 게이지 Q 준비 % 출발 왕관 선두 차 m + − ♪ 0123456789',
    ...st.caps.flatMap(q => [q.text, q.memo || '']), ...st.stamps.flatMap(q => [q.text, q.sub || '']), ...st.labels.map(q => q.text),
    ...T.spec.flat()].join(' ');
}
